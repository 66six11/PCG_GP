using System.Collections.Generic;
using UnityEngine;

namespace SVO.Runtime.Visualization
{
    [RequireComponent(typeof(LineRenderer))]
    public class PathVisualizer : MonoBehaviour
    {
        [Header("Path Visualization")]
        public Color pathColor = Color.green;
        public float pathWidth = 0.1f;
        public Material pathMaterial;
        
        [Header("Waypoint Visualization")]
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
        /// Setup the line renderer for path visualization
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
        /// Display a path as a line
        /// </summary>
        public void DisplayPath(List<Vector3> path)
        {
            ClearPath();
            
            if (path == null || path.Count == 0)
                return;
                
            currentPath = new List<Vector3>(path);
            
            // Setup line renderer
            lineRenderer.positionCount = path.Count;
            lineRenderer.SetPositions(path.ToArray());
            
            // Create waypoint objects if enabled
            if (showWaypoints)
            {
                CreateWaypoints(path);
            }
        }
        
        /// <summary>
        /// Create waypoint objects along the path
        /// </summary>
        private void CreateWaypoints(List<Vector3> path)
        {
            if (waypointPrefab == null)
            {
                // Create simple sphere waypoints if no prefab is provided
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
        /// Create simple sphere waypoints
        /// </summary>
        private void CreateSimpleWaypoints(List<Vector3> path)
        {
            for (int i = 0; i < path.Count; i++)
            {
                GameObject waypoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                waypoint.transform.position = path[i];
                waypoint.transform.localScale = Vector3.one * waypointScale;
                waypoint.transform.SetParent(transform);
                
                // Set color based on position in path
                Renderer renderer = waypoint.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Standard"));
                    if (i == 0)
                        material.color = Color.green; // Start point
                    else if (i == path.Count - 1)
                        material.color = Color.red; // End point
                    else
                        material.color = pathColor; // Path points
                        
                    renderer.material = material;
                }
                
                waypointObjects.Add(waypoint);
            }
        }
        
        /// <summary>
        /// Clear the current path visualization
        /// </summary>
        public void ClearPath()
        {
            // Clear line renderer
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }
            
            // Destroy waypoint objects
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
        /// Update path visualization settings
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
            
            // Update waypoint visibility
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
        /// Get the current path
        /// </summary>
        public List<Vector3> GetCurrentPath()
        {
            return new List<Vector3>(currentPath);
        }
        
        /// <summary>
        /// Check if a path is currently being displayed
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