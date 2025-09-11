using UnityEngine;

namespace SVO.Runtime.Core
{
    public class OctreeNode
    {
        public OctreeNode[] children;

        public int depth;

        public Bounds bounds;

        public bool isLeaf;

        public OctreeNodeFlags flags;
        
        public OctreeNode(Bounds bounds, int depth, bool isLeaf)
        {
            this.bounds = bounds;
            this.depth = depth;
            this.isLeaf = isLeaf;
            this.children = new OctreeNode[8];
            this.flags = new OctreeNodeFlags();
        }
    }
}