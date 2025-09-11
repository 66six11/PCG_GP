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
        /// Get world position from mouse cursor
        /// </summary>
        private Vector3? GetMouseWorldPosition()
        {
            if (playerCamera == null)
                return null;
                
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            
            // Raycast against a horizontal plane at y=0 or against existing geometry
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                return hit.point;
            }
            
            // If no hit, project onto a plane at the octree center
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
        /// Set the start position for pathfinding
        /// </summary>
        public void SetStartPosition(Vector3 position)
        {
            startPosition = position;
            
            // Update start marker
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
            
            Debug.Log($"Start position set to: {position}");
        }
        
        /// <summary>
        /// Set the end position for pathfinding
        /// </summary>
        public void SetEndPosition(Vector3 position)
        {
            endPosition = position;
            
            // Update end marker
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
            
            Debug.Log($"End position set to: {position}");
        }
        
        /// <summary>
        /// Add an obstacle at the specified position
        /// </summary>
        public void AddObstacle(Vector3 position)
        {
            if (octreeVisualizer != null)
            {
                octreeVisualizer.SetNodeBlocked(position, true);
                
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
                Debug.Log($"Obstacle added at: {position}");
            }
        }
        
        /// <summary>
        /// Remove obstacle at the specified position
        /// </summary>
        public void RemoveObstacle(Vector3 position)
        {
            if (octreeVisualizer != null)
            {
                octreeVisualizer.SetNodeBlocked(position, false);
                
                // Find and remove nearby obstacle marker
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
                
                Debug.Log($"Obstacle removed at: {position}");
            }
        }
        
        /// <summary>
        /// Find and display the path between start and end positions
        /// </summary>
        public void FindAndDisplayPath()
        {
            if (!startPosition.HasValue || !endPosition.HasValue)
            {
                Debug.LogWarning("Start and end positions must be set before pathfinding");
                return;
            }
            
            if (octreeVisualizer == null || octreeVisualizer.GetOctree() == null)
            {
                Debug.LogWarning("Octree visualizer not found or octree not initialized");
                return;
            }
            
            Octree octree = octreeVisualizer.GetOctree();
            
            // Find path
            List<Vector3> path = OctreePathfinder.FindPath(octree, startPosition.Value, endPosition.Value);
            
            if (path.Count > 0)
            {
                // Apply path smoothing if enabled
                if (smoothPath)
                {
                    path = OctreePathfinder.SmoothPath(path, octree);
                }
                
                // Display the path
                if (pathVisualizer != null)
                {
                    pathVisualizer.DisplayPath(path);
                }
                
                Debug.Log($"Path found with {path.Count} waypoints");
            }
            else
            {
                Debug.LogWarning("No path found between start and end positions");
                if (pathVisualizer != null)
                {
                    pathVisualizer.ClearPath();
                }
            }
        }
        
        /// <summary>
        /// Create a simple marker object
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
        /// Setup initial obstacles for demonstration
        /// </summary>
        private void SetupInitialObstacles()
        {
            // Add some initial obstacles for demonstration
            if (octreeVisualizer != null)
            {
                Vector3 center = octreeVisualizer.transform.position;
                
                // Create a simple wall obstacle
                for (int i = -2; i <= 2; i++)
                {
                    AddObstacle(center + new Vector3(i * 2f, 0, 0));
                }
            }
        }
        
        /// <summary>
        /// Clear all markers and paths
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
            Debug.Log("Demo cleared");
        }
        
        /// <summary>
        /// Refresh the octree
        /// </summary>
        public void RefreshOctree()
        {
            if (octreeVisualizer != null)
            {
                octreeVisualizer.RefreshOctree();
                Debug.Log("Octree refreshed");
            }
        }
        
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Octree Pathfinding Demo", new GUIStyle() { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Label($"Current Mode: {currentInputMode}");
            GUILayout.Space(10);
            
            GUILayout.Label("Controls:");
            GUILayout.Label("1 - Set Start Position");
            GUILayout.Label("2 - Set End Position");
            GUILayout.Label("3 - Add Obstacle");
            GUILayout.Label("4 - Remove Obstacle");
            GUILayout.Label("Space - Find Path");
            GUILayout.Label("C - Clear All");
            GUILayout.Label("R - Refresh Octree");
            
            GUILayout.EndArea();
        }
    }
}