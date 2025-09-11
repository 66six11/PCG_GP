using UnityEngine;
using System.Collections.Generic;
using SVO.Runtime.Core;

namespace SVO.Runtime.Demo
{
    /// <summary>
    /// Visual demonstration of octree pathfinding using Gizmos
    /// This creates a visual representation that can be seen in the Scene view
    /// </summary>
    public class OctreeGizmoDemo : MonoBehaviour
    {
        [Header("Octree Configuration")]
        public Vector3 octreeSize = new Vector3(16f, 8f, 16f);
        public int maxDepth = 3;
        public float minNodeSize = 2f;
        
        [Header("Demo Points")]
        public Vector3 startPoint = new Vector3(-6f, 0f, -6f);
        public Vector3 endPoint = new Vector3(6f, 0f, 6f);
        
        [Header("Visualization")]
        public Color octreeColor = Color.white;
        public Color obstacleColor = Color.red;
        public Color pathColor = Color.green;
        public Color startColor = Color.blue;
        public Color endColor = Color.magenta;
        public bool showOctreeNodes = true;
        public bool showPath = true;
        
        private Octree octree;
        private List<Vector3> currentPath;
        private List<Vector3> obstaclePositions;
        
        void Start()
        {
            CreateOctreeDemo();
        }
        
        void CreateOctreeDemo()
        {
            // Create octree
            Bounds bounds = new Bounds(transform.position, octreeSize);
            octree = new Octree(bounds, maxDepth, minNodeSize);
            
            // Add some obstacles to make the demo interesting
            obstaclePositions = new List<Vector3>();
            Vector3 center = transform.position;
            
            // Create a wall of obstacles
            for (int x = -2; x <= 2; x++)
            {
                Vector3 obstaclePos = center + new Vector3(x * 2f, 0f, 0f);
                octree.SetNodeBlocked(obstaclePos, true);
                obstaclePositions.Add(obstaclePos);
            }
            
            // Find path
            Vector3 worldStart = transform.position + startPoint;
            Vector3 worldEnd = transform.position + endPoint;
            currentPath = OctreePathfinder.FindPath(octree, worldStart, worldEnd);
            
            if (currentPath.Count > 0)
            {
                Debug.Log($"Pathfinding Demo: Found path with {currentPath.Count} waypoints");
                // Apply smoothing
                currentPath = OctreePathfinder.SmoothPath(currentPath, octree);
                Debug.Log($"After smoothing: {currentPath.Count} waypoints");
            }
            else
            {
                Debug.LogWarning("Pathfinding Demo: No path found");
            }
        }
        
        void OnDrawGizmos()
        {
            if (octree == null)
                return;
                
            // Draw octree bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, octreeSize);
            
            if (showOctreeNodes)
            {
                DrawOctreeNodes();
            }
            
            // Draw obstacles
            Gizmos.color = obstacleColor;
            if (obstaclePositions != null)
            {
                foreach (Vector3 obstacle in obstaclePositions)
                {
                    Gizmos.DrawCube(obstacle, Vector3.one * minNodeSize * 0.8f);
                }
            }
            
            // Draw start and end points
            Vector3 worldStart = transform.position + startPoint;
            Vector3 worldEnd = transform.position + endPoint;
            
            Gizmos.color = startColor;
            Gizmos.DrawSphere(worldStart, 0.5f);
            
            Gizmos.color = endColor;
            Gizmos.DrawSphere(worldEnd, 0.5f);
            
            // Draw path
            if (showPath && currentPath != null && currentPath.Count > 1)
            {
                Gizmos.color = pathColor;
                for (int i = 0; i < currentPath.Count - 1; i++)
                {
                    Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
                    Gizmos.DrawWireSphere(currentPath[i], 0.2f);
                }
                Gizmos.DrawWireSphere(currentPath[currentPath.Count - 1], 0.2f);
            }
        }
        
        void DrawOctreeNodes()
        {
            if (octree == null)
                return;
                
            List<OctreeNode> leafNodes = octree.GetLeafNodes();
            
            foreach (OctreeNode node in leafNodes)
            {
                // Color nodes based on their state
                if (node.HasFlag(OctreeNodeFlags.IsPath))
                {
                    Gizmos.color = Color.Lerp(pathColor, Color.white, 0.7f);
                }
                else if (node.HasFlag(OctreeNodeFlags.Blocked))
                {
                    Gizmos.color = Color.Lerp(obstacleColor, Color.white, 0.5f);
                }
                else
                {
                    Gizmos.color = Color.Lerp(octreeColor, Color.clear, 0.7f);
                }
                
                // Draw node bounds as wireframe
                DrawWireCube(node.bounds.center, node.bounds.size);
            }
        }
        
        void DrawWireCube(Vector3 center, Vector3 size)
        {
            Vector3 halfSize = size * 0.5f;
            
            // Bottom face
            Vector3 p1 = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p2 = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            Vector3 p3 = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            Vector3 p4 = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            
            // Top face
            Vector3 p5 = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p6 = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            Vector3 p7 = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
            Vector3 p8 = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);
            
            // Draw bottom face
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p4);
            Gizmos.DrawLine(p4, p1);
            
            // Draw top face
            Gizmos.DrawLine(p5, p6);
            Gizmos.DrawLine(p6, p7);
            Gizmos.DrawLine(p7, p8);
            Gizmos.DrawLine(p8, p5);
            
            // Draw vertical lines
            Gizmos.DrawLine(p1, p5);
            Gizmos.DrawLine(p2, p6);
            Gizmos.DrawLine(p3, p7);
            Gizmos.DrawLine(p4, p8);
        }
        
        // Button in inspector to regenerate demo
        [ContextMenu("Regenerate Demo")]
        void RegenerateDemo()
        {
            CreateOctreeDemo();
        }
    }
}