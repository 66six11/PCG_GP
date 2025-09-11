using System.Collections.Generic;
using UnityEngine;

namespace SVO.Runtime.Visualization
{
    [RequireComponent(typeof(LineRenderer))]
    public class PathVisualizer : MonoBehaviour
    {
        [Header("路径可视化")]
        public Color pathColor = Color.green;
        public float pathWidth = 0.1f;
        public Material pathMaterial;
        
        [Header("路径点可视化")]
        public bool showWaypoints = true;
        public GameObject waypointPrefab;
        public float waypointScale = 0.2f;
        
        private LineRenderer lineRenderer;
        private List<GameObject> waypointObjects;
        private List<Vector3> currentPath;
        
        void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            waypointObjects = new List<GameObject>();
            currentPath = new List<Vector3>();
            
            SetupLineRenderer();
        }
        
        /// <summary>
        /// 为路径可视化设置线条渲染器
        /// </summary>
        private void SetupLineRenderer()
        {
            if (lineRenderer == null)
                return;
                
            lineRenderer.material = pathMaterial;
            lineRenderer.color = pathColor;
            lineRenderer.startWidth = pathWidth;
            lineRenderer.endWidth = pathWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 0;
        }
        
        /// <summary>
        /// 将路径显示为线条
        /// </summary>
        public void DisplayPath(List<Vector3> path)
        {
            ClearPath();
            
            if (path == null || path.Count == 0)
                return;
                
            currentPath = new List<Vector3>(path);
            
            // 设置线条渲染器
            lineRenderer.positionCount = path.Count;
            lineRenderer.SetPositions(path.ToArray());
            
            // 如果启用，创建路径点对象
            if (showWaypoints)
            {
                CreateWaypoints(path);
            }
        }
        
        /// <summary>
        /// 沿路径创建路径点对象
        /// </summary>
        private void CreateWaypoints(List<Vector3> path)
        {
            if (waypointPrefab == null)
            {
                // 如果未提供预制体，创建简单的球形路径点
                CreateSimpleWaypoints(path);
                return;
            }
            
            for (int i = 0; i < path.Count; i++)
            {
                GameObject waypoint = Instantiate(waypointPrefab, path[i], Quaternion.identity, transform);
                waypoint.transform.localScale = Vector3.one * waypointScale;
                waypointObjects.Add(waypoint);
            }
        }
        
        /// <summary>
        /// 创建简单的球形路径点
        /// </summary>
        private void CreateSimpleWaypoints(List<Vector3> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                GameObject waypoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                waypoint.transform.position = path[i];
                waypoint.transform.localScale = Vector3.one * waypointScale;
                waypoint.transform.SetParent(transform);
                
                // 根据在路径中的位置设置颜色
                Renderer renderer = waypoint.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Standard"));
                    if (i == 0)
                        material.color = Color.green; // 起始点
                    else if (i == path.Count - 1)
                        material.color = Color.red; // 结束点
                    else
                        material.color = pathColor; // 路径点
                        
                    renderer.material = material;
                }
                
                waypointObjects.Add(waypoint);
            }
        }
        
        /// <summary>
        /// 清除当前的路径可视化
        /// </summary>
        public void ClearPath()
        {
            // 清除线条渲染器
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }
            
            // 销毁路径点对象
            foreach (GameObject waypoint in waypointObjects)
            {
                if (waypoint != null)
                {
                    DestroyImmediate(waypoint);
                }
            }
            waypointObjects.Clear();
            currentPath.Clear();
        }
        
        /// <summary>
        /// 更新路径可视化设置
        /// </summary>
        public void UpdateVisualization()
        {
            if (lineRenderer != null)
            {
                lineRenderer.color = pathColor;
                lineRenderer.startWidth = pathWidth;
                lineRenderer.endWidth = pathWidth;
                lineRenderer.material = pathMaterial;
            }
            
            // 更新路径点可见性
            foreach (GameObject waypoint in waypointObjects)
            {
                if (waypoint != null)
                {
                    waypoint.SetActive(showWaypoints);
                    waypoint.transform.localScale = Vector3.one * waypointScale;
                }
            }
        }
        
        /// <summary>
        /// 获取当前路径
        /// </summary>
        public List<Vector3> GetCurrentPath()
        {
            return new List<Vector3>(currentPath);
        }
        
        /// <summary>
        /// 检查当前是否正在显示路径
        /// </summary>
        public bool HasPath()
        {
            return currentPath.Count > 0;
        }
        
        void OnDestroy()
        {
            ClearPath();
        }
    }
}