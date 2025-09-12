using UnityEngine;

namespace SVO.Runtime.Core.Octree
{
    public class OctreeNode
    {
        public OctreeNode[] children;
        public bool hasChildren;

        public int depth;
        public Bounds bounds;

        public bool isLeaf => children.Length == 0 || children == null;
        public OctreeNodeFlags flags;

        public OctreeNode(int depth, Bounds bounds, OctreeNodeFlags flags)
        {
            this.depth = depth;
            this.bounds = bounds;
            this.flags = flags;
        }
    }
}