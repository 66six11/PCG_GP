using System.Collections.Generic;
using UnityEngine;
using SVO.Runtime.Core;

namespace SVO.Runtime.Visualization
{
    [RequireComponent(typeof(LineRenderer))]
    public class OctreeVisualizer : MonoBehaviour
    {
        [Header("Octree Settings")]
        public Vector3 octreeSize = new Vector3(20f, 20f, 20f);
        public int maxDepth = 4;
        public float minNodeSize = 1f;
        
        [Header("Visualization Settings")]
        public bool showOctree = true;
        public bool showOnlyLeafNodes = true;
        public bool showBlockedNodes = true;
        public bool showPathNodes = true;
        
        [Header("Colors")]
        public Color normalNodeColor = Color.white;
        public Color blockedNodeColor = Color.red;
        public Color pathNodeColor = Color.green;
        public Color openSetColor = Color.yellow;
        public Color closedSetColor = Color.blue;
        
        [Header("Materials")]
        public Material lineMaterial;
        
        private Octree octree;
        private List<LineRenderer> nodeRenderers;
        private bool octreeInitialized = false;
        
        void Start()
        {
            InitializeOctree();
            nodeRenderers = new List<LineRenderer>();
        }
        
        void Update()
        {
            if (showOctree && octreeInitialized)
            {
                UpdateVisualization();
            }
        }
        
        /// <summary>
        /// Initialize the octree
        /// </summary>
        public void InitializeOctree()
        {
            Bounds bounds = new Bounds(transform.position, octreeSize);
            octree = new Octree(bounds, maxDepth, minNodeSize);
            octreeInitialized = true;
            
            Debug.Log($"Octree initialized with {octree.GetLeafNodes().Count} leaf nodes");
        }
        
        /// <summary>
        /// Update the visualization of the octree
        /// </summary>
        private void UpdateVisualization()
        {
            ClearVisualization();
            
            if (octree == null)
                return;
                
            List<OctreeNode> nodesToDraw = showOnlyLeafNodes ? octree.GetLeafNodes() : GetAllNodes();
            
            foreach (OctreeNode node in nodesToDraw)
            {
                DrawNode(node);
            }
        }
        
        /// <summary>
        /// Get all nodes from the octree
        /// </summary>
        private List<OctreeNode> GetAllNodes()
        {
            List<OctreeNode> allNodes = new List<OctreeNode>();
            GetAllNodesRecursive(octree.root, allNodes);
            return allNodes;
        }
        
        private void GetAllNodesRecursive(OctreeNode node, List<OctreeNode> nodes)
        {
            nodes.Add(node);
            
            if (!node.isLeaf)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (node.children[i] != null)
                    {
                        GetAllNodesRecursive(node.children[i], nodes);
                    }
                }
            }
        }
        
        /// <summary>
        /// Draw a single octree node as a wireframe cube
        /// </summary>
        private void DrawNode(OctreeNode node)
        {
            Color nodeColor = GetNodeColor(node);
            DrawWireframeCube(node.bounds, nodeColor);
        }
        
        /// <summary>
        /// Get the appropriate color for a node based on its state
        /// </summary>
        private Color GetNodeColor(OctreeNode node)
        {
            if (node.HasFlag(OctreeNodeFlags.IsPath) && showPathNodes)
                return pathNodeColor;
            else if (node.HasFlag(OctreeNodeFlags.Blocked) && showBlockedNodes)
                return blockedNodeColor;
            else if (node.HasFlag(OctreeNodeFlags.InOpenSet))
                return openSetColor;
            else if (node.HasFlag(OctreeNodeFlags.InClosedSet))
                return closedSetColor;
            else
                return normalNodeColor;
        }
        
        /// <summary>
        /// Draw a wireframe cube for a node
        /// </summary>
        private void DrawWireframeCube(Bounds bounds, Color color)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            
            // Calculate the 8 corners of the cube
            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-size.x, -size.y, -size.z) * 0.5f; // Bottom-back-left
            corners[1] = center + new Vector3(size.x, -size.y, -size.z) * 0.5f;  // Bottom-back-right
            corners[2] = center + new Vector3(size.x, -size.y, size.z) * 0.5f;   // Bottom-front-right
            corners[3] = center + new Vector3(-size.x, -size.y, size.z) * 0.5f;  // Bottom-front-left
            corners[4] = center + new Vector3(-size.x, size.y, -size.z) * 0.5f;  // Top-back-left
            corners[5] = center + new Vector3(size.x, size.y, -size.z) * 0.5f;   // Top-back-right
            corners[6] = center + new Vector3(size.x, size.y, size.z) * 0.5f;    // Top-front-right
            corners[7] = center + new Vector3(-size.x, size.y, size.z) * 0.5f;   // Top-front-left
            
            // Draw the 12 edges of the cube
            DrawLine(corners[0], corners[1], color); // Bottom edges
            DrawLine(corners[1], corners[2], color);
            DrawLine(corners[2], corners[3], color);
            DrawLine(corners[3], corners[0], color);
            
            DrawLine(corners[4], corners[5], color); // Top edges
            DrawLine(corners[5], corners[6], color);
            DrawLine(corners[6], corners[7], color);
            DrawLine(corners[7], corners[4], color);
            
            DrawLine(corners[0], corners[4], color); // Vertical edges
            DrawLine(corners[1], corners[5], color);
            DrawLine(corners[2], corners[6], color);
            DrawLine(corners[3], corners[7], color);
        }
        
        /// <summary>
        /// Draw a line using Debug.DrawLine (visible in Scene view)
        /// </summary>
        private void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            Debug.DrawLine(start, end, color);
        }
        
        /// <summary>
        /// Clear all visualization
        /// </summary>
        private void ClearVisualization()
        {
            foreach (LineRenderer renderer in nodeRenderers)
            {
                if (renderer != null)
                    DestroyImmediate(renderer.gameObject);
            }
            nodeRenderers.Clear();
        }
        
        /// <summary>
        /// Get the octree instance
        /// </summary>
        public Octree GetOctree()
        {
            return octree;
        }
        
        /// <summary>
        /// Set a node as blocked at the specified position
        /// </summary>
        public void SetNodeBlocked(Vector3 position, bool blocked = true)
        {
            if (octree != null)
            {
                octree.SetNodeBlocked(position, blocked);
            }
        }
        
        /// <summary>
        /// Refresh the octree and visualization
        /// </summary>
        public void RefreshOctree()
        {
            InitializeOctree();
        }
        
        void OnDrawGizmos()
        {
            // Draw octree bounds
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, octreeSize);
        }
    }
}