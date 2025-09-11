using UnityEngine;

namespace SVO.Runtime.Core
{
    public class OctreeNode
    {
        public OctreeNode[] children;
        public OctreeNode parent;
        
        public int depth;
        public Bounds bounds;
        public bool isLeaf;
        public OctreeNodeFlags flags;
        
        // Pathfinding data
        public float gCost; // Distance from start node
        public float hCost; // Distance to target node  
        public float fCost => gCost + hCost; // Total cost
        public OctreeNode pathParent; // Parent in the path (different from tree parent)
        
        // Node center for pathfinding calculations
        public Vector3 Center => bounds.center;
        
        public OctreeNode(Bounds bounds, int depth, bool isLeaf)
        {
            this.bounds = bounds;
            this.depth = depth;
            this.isLeaf = isLeaf;
            this.children = new OctreeNode[8];
            this.flags = OctreeNodeFlags.Walkable; // Default to walkable
            
            // Initialize pathfinding data
            this.gCost = float.MaxValue;
            this.hCost = 0f;
            this.pathParent = null;
        }
        
        /// <summary>
        /// Reset pathfinding data for a new search
        /// </summary>
        public void ResetPathfindingData()
        {
            gCost = float.MaxValue;
            hCost = 0f;
            pathParent = null;
            flags &= ~(OctreeNodeFlags.Visited | OctreeNodeFlags.InOpenSet | OctreeNodeFlags.InClosedSet | OctreeNodeFlags.IsPath);
        }
        
        /// <summary>
        /// Check if this node has specific flags
        /// </summary>
        public bool HasFlag(OctreeNodeFlags flag)
        {
            return (flags & flag) == flag;
        }
        
        /// <summary>
        /// Set specific flags on this node
        /// </summary>
        public void SetFlag(OctreeNodeFlags flag, bool value)
        {
            if (value)
                flags |= flag;
            else
                flags &= ~flag;
        }
    }
}