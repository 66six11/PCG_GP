using System.Collections.Generic;
using UnityEngine;
using SVO.Runtime.Core;
using SVO.Runtime.Visualization;

namespace SVO.Runtime.Demo
{
    public class OctreePathfindingDemo : MonoBehaviour
    {
        [Header("演示设置")]
        public bool enableMouseInput = true;
        public LayerMask obstacleLayerMask = -1;
        public float obstacleDetectionRadius = 0.5f;
        
        [Header("寻路设置")]
        public bool smoothPath = true;
        public bool showPathfindingDebug = false;
        
        [Header("视觉反馈")]
        public GameObject startMarkerPrefab;
        public GameObject endMarkerPrefab;
        public GameObject obstacleMarkerPrefab;
        
        [Header("引用")]
        public OctreeVisualizer octreeVisualizer;
        public PathVisualizer pathVisualizer;
        public ObstacleManager obstacleManager;
        
        private Vector3? startPosition;
        private Vector3? endPosition;
        private GameObject startMarker;
        private GameObject endMarker;
        private List<GameObject> obstacleMarkers;
        private Camera playerCamera;
        
        private enum InputMode
        {
            SetStart,    // 设置起点
            SetEnd,      // 设置终点
            AddObstacle, // 添加障碍物
            RemoveObstacle // 移除障碍物
        }
        private InputMode currentInputMode = InputMode.SetStart;
        
        void Start()
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
                playerCamera = FindObjectOfType<Camera>();
                
            obstacleMarkers = new List<GameObject>();
            
            // 初始化组件引用（如果未分配）
            if (octreeVisualizer == null)
                octreeVisualizer = FindObjectOfType<OctreeVisualizer>();
            if (pathVisualizer == null)
                pathVisualizer = FindObjectOfType<PathVisualizer>();
            if (obstacleManager == null)
                obstacleManager = FindObjectOfType<ObstacleManager>();
                
            SetupInitialObstacles();
        }
        
        void Update()
        {
            if (enableMouseInput)
            {
                HandleMouseInput();
            }
            
            HandleKeyboardInput();
        }
        
        /// <summary>
        /// 处理设置起始/结束点和障碍物的鼠标输入
        /// </summary>
        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(0)) // Left click
            {
                Vector3? hitPoint = GetMouseWorldPosition();
                if (hitPoint.HasValue)
                {
                    switch (currentInputMode)
                    {
                        case InputMode.SetStart:
                            SetStartPosition(hitPoint.Value);
                            currentInputMode = InputMode.SetEnd;
                            break;
                        case InputMode.SetEnd:
                            SetEndPosition(hitPoint.Value);
                            FindAndDisplayPath();
                            break;
                        case InputMode.AddObstacle:
                            AddObstacle(hitPoint.Value);
                            break;
                        case InputMode.RemoveObstacle:
                            RemoveObstacle(hitPoint.Value);
                            break;
                    }
                }
            }
        }
        
        /// <summary>
        /// 处理模式切换的键盘输入
        /// </summary>
        private void HandleKeyboardInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                currentInputMode = InputMode.SetStart;
                Debug.Log("模式：设置起始位置");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                currentInputMode = InputMode.SetEnd;
                Debug.Log("模式：设置结束位置");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                currentInputMode = InputMode.AddObstacle;
                Debug.Log("模式：添加障碍物");
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                currentInputMode = InputMode.RemoveObstacle;
                Debug.Log("模式：移除障碍物");
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                FindAndDisplayPath();
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                ClearAll();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                RefreshOctree();
            }
        }
        
        /// <summary>
        /// 从鼠标光标获取世界位置
        /// </summary>
        private Vector3? GetMouseWorldPosition()
        {
            if (playerCamera == null)
                return null;
                
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            
            // 对水平面（y=0）或现有几何体进行射线投射
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                return hit.point;
            }
            
            // 如果没有命中，投射到八叉树中心的平面
            if (octreeVisualizer != null)
            {
                Plane plane = new Plane(Vector3.up, octreeVisualizer.transform.position);
                float distance;
                if (plane.Raycast(ray, out distance))
                {
                    return ray.GetPoint(distance);
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 设置寻路的起始位置
        /// </summary>
        public void SetStartPosition(Vector3 position)
        {
            startPosition = position;
            
            // 更新起始标记
            if (startMarker != null)
                DestroyImmediate(startMarker);
                
            if (startMarkerPrefab != null)
            {
                startMarker = Instantiate(startMarkerPrefab, position, Quaternion.identity);
            }
            else
            {
                startMarker = CreateSimpleMarker(position, Color.green);
            }
            
            Debug.Log($"起始位置设置为：{position}");
        }
        
        /// <summary>
        /// 设置寻路的结束位置
        /// </summary>
        public void SetEndPosition(Vector3 position)
        {
            endPosition = position;
            
            // 更新结束标记
            if (endMarker != null)
                DestroyImmediate(endMarker);
                
            if (endMarkerPrefab != null)
            {
                endMarker = Instantiate(endMarkerPrefab, position, Quaternion.identity);
            }
            else
            {
                endMarker = CreateSimpleMarker(position, Color.red);
            }
            
            Debug.Log($"结束位置设置为：{position}");
        }
        
        /// <summary>
        /// 在指定位置添加障碍物
        /// </summary>
        public void AddObstacle(Vector3 position)
        {
            bool success = false;
            
            // 优先使用障碍物管理器
            if (obstacleManager != null)
            {
                success = obstacleManager.AddObstacle(position);
            }
            else if (octreeVisualizer != null)
            {
                octreeVisualizer.SetNodeBlocked(position, true);
                success = true;
                
                // 创建可视化标记
                GameObject obstacleMarker;
                if (obstacleMarkerPrefab != null)
                {
                    obstacleMarker = Instantiate(obstacleMarkerPrefab, position, Quaternion.identity);
                }
                else
                {
                    obstacleMarker = CreateSimpleMarker(position, Color.red, PrimitiveType.Cube);
                }
                
                obstacleMarkers.Add(obstacleMarker);
            }
            
            if (success)
            {
                Debug.Log($"在位置 {position} 添加了障碍物");
            }
            else
            {
                Debug.LogWarning($"无法在位置 {position} 添加障碍物");
            }
        }
        
        /// <summary>
        /// 移除指定位置的障碍物
        /// </summary>
        public void RemoveObstacle(Vector3 position)
        {
            bool success = false;
            
            // 优先使用障碍物管理器
            if (obstacleManager != null)
            {
                success = obstacleManager.RemoveObstacle(position);
            }
            else if (octreeVisualizer != null)
            {
                octreeVisualizer.SetNodeBlocked(position, false);
                success = true;
                
                // 查找并移除附近的障碍物标记
                for (int i = obstacleMarkers.Count - 1; i >= 0; i--)
                {
                    if (obstacleMarkers[i] != null && 
                        Vector3.Distance(obstacleMarkers[i].transform.position, position) < 1f)
                    {
                        DestroyImmediate(obstacleMarkers[i]);
                        obstacleMarkers.RemoveAt(i);
                        break;
                    }
                }
            }
            
            if (success)
            {
                Debug.Log($"从位置 {position} 移除了障碍物");
            }
            else
            {
                Debug.LogWarning($"无法从位置 {position} 移除障碍物");
            }
        }
        
        /// <summary>
        /// 查找并显示起始和结束位置之间的路径
        /// </summary>
        public void FindAndDisplayPath()
        {
            if (!startPosition.HasValue || !endPosition.HasValue)
            {
                Debug.LogWarning("必须先设置起始和结束位置才能进行寻路");
                return;
            }
            
            if (octreeVisualizer == null || octreeVisualizer.GetOctree() == null)
            {
                Debug.LogWarning("未找到八叉树可视化器或八叉树未初始化");
                return;
            }
            
            Octree octree = octreeVisualizer.GetOctree();
            
            // 查找路径
            List<Vector3> path = OctreePathfinder.FindPath(octree, startPosition.Value, endPosition.Value);
            
            if (path.Count > 0)
            {
                // 如果启用，应用路径平滑
                if (smoothPath)
                {
                    path = OctreePathfinder.SmoothPath(path, octree);
                }
                
                // 显示路径
                if (pathVisualizer != null)
                {
                    pathVisualizer.DisplayPath(path);
                }
                
                Debug.Log($"找到包含 {path.Count} 个路径点的路径");
            }
            else
            {
                Debug.LogWarning("在起始和结束位置之间未找到路径");
                if (pathVisualizer != null)
                {
                    pathVisualizer.ClearPath();
                }
            }
        }
        
        /// <summary>
        /// 创建简单的标记对象
        /// </summary>
        private GameObject CreateSimpleMarker(Vector3 position, Color color, PrimitiveType primitiveType = PrimitiveType.Sphere)
        {
            GameObject marker = GameObject.CreatePrimitive(primitiveType);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * 0.5f;
            
            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = color;
                renderer.material = material;
            }
            
            return marker;
        }
        
        /// <summary>
        /// 为演示设置初始障碍物
        /// </summary>
        private void SetupInitialObstacles()
        {
            // 添加一些初始障碍物作为演示
            if (octreeVisualizer != null)
            {
                Vector3 center = octreeVisualizer.transform.position;
                
                // 创建简单的墙壁障碍物
                for (int i = -2; i <= 2; i++)
                {
                    AddObstacle(center + new Vector3(i * 2f, 0, 0));
                }
            }
        }
        
        /// <summary>
        /// 清除所有标记和路径
        /// </summary>
        public void ClearAll()
        {
            startPosition = null;
            endPosition = null;
            
            if (startMarker != null)
                DestroyImmediate(startMarker);
            if (endMarker != null)
                DestroyImmediate(endMarker);
                
            foreach (GameObject obstacle in obstacleMarkers)
            {
                if (obstacle != null)
                    DestroyImmediate(obstacle);
            }
            obstacleMarkers.Clear();
            
            if (pathVisualizer != null)
                pathVisualizer.ClearPath();
                
            currentInputMode = InputMode.SetStart;
            Debug.Log("演示已清除");
        }
        
        /// <summary>
        /// 刷新八叉树
        /// </summary>
        public void RefreshOctree()
        {
            if (octreeVisualizer != null)
            {
                octreeVisualizer.RefreshOctree();
                Debug.Log("八叉树已刷新");
            }
        }
        
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("八叉树寻路演示", new GUIStyle() { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Label($"当前模式：{GetModeDisplayName(currentInputMode)}");
            GUILayout.Space(10);
            
            GUILayout.Label("控制:");
            GUILayout.Label("1 - 设置起始位置");
            GUILayout.Label("2 - 设置结束位置");
            GUILayout.Label("3 - 添加障碍物");
            GUILayout.Label("4 - 移除障碍物");
            GUILayout.Label("空格 - 查找路径");
            GUILayout.Label("C - 清除所有");
            GUILayout.Label("R - 刷新八叉树");
            
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// 获取模式的中文显示名称
        /// </summary>
        private string GetModeDisplayName(InputMode mode)
        {
            switch (mode)
            {
                case InputMode.SetStart: return "设置起始位置";
                case InputMode.SetEnd: return "设置结束位置";
                case InputMode.AddObstacle: return "添加障碍物";
                case InputMode.RemoveObstacle: return "移除障碍物";
                default: return mode.ToString();
            }
        }
    }
}