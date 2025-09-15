using UnityEngine;

namespace SVO.Runtime.Core.Octree
{
    public class OctreeNode<T>
    {
        public OctreeNode<T>[] children;

        public int depth;

        public Bounds bounds;

        public OctreeNodeFlags flags;

        public T value;

        public bool IsLeaf => children == null || children.Length == 0;

        public OctreeNode(int depth, Bounds bounds, OctreeNodeFlags flags)
        {
            this.depth = depth;
            this.bounds = bounds;
            this.flags = flags;
            value = default;
        }

        public OctreeNode(int depth, Bounds bounds, OctreeNodeFlags flags, T value)
        {
            this.depth = depth;
            this.bounds = bounds;
            this.flags = flags;
            this.children = null;
            this.value = value;
        }
        
        
    }
}