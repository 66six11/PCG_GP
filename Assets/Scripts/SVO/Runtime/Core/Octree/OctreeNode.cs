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
        
        // 寻路数据
        public float gCost; // 从起始节点的距离
        public float hCost; // 到目标节点的距离  
        public float fCost => gCost + hCost; // 总成本
        public OctreeNode pathParent; // 路径中的父节点（不同于树的父节点）
        
        // 寻路计算的节点中心
        public Vector3 Center => bounds.center;
        
        public OctreeNode(Bounds bounds, int depth, bool isLeaf)
        {
            this.bounds = bounds;
            this.depth = depth;
            this.isLeaf = isLeaf;
            this.children = new OctreeNode[8];
            this.flags = OctreeNodeFlags.Walkable; // 默认为可行走
            
            // 初始化寻路数据
            this.gCost = float.MaxValue;
            this.hCost = 0f;
            this.pathParent = null;
        }
        
        /// <summary>
        /// 为新的搜索重置寻路数据
        /// </summary>
        public void ResetPathfindingData()
        {
            gCost = float.MaxValue;
            hCost = 0f;
            pathParent = null;
            flags &= ~(OctreeNodeFlags.Visited | OctreeNodeFlags.InOpenSet | OctreeNodeFlags.InClosedSet | OctreeNodeFlags.IsPath);
        }
        
        /// <summary>
        /// 检查此节点是否具有特定标志
        /// </summary>
        public bool HasFlag(OctreeNodeFlags flag)
        {
            return (flags & flag) == flag;
        }
        
        /// <summary>
        /// 在此节点上设置特定标志
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