using System;
using System.Collections.Generic;
using SVO.Runtime.Utility;
using UnityEngine;

namespace SVO.Runtime.Core.Octree
{
    /// <summary>
    /// 通用八叉树实现，可用于空间分区、体素、碰撞检测等。
    /// </summary>
    public class Octree<T>
    {
        public OctreeNode<T> Root { get; private set; }
        private readonly int _maxDepth;

        public Octree(Bounds initialBounds, int maxDepth)
        {
            if (maxDepth <= 0)
                throw new ArgumentException("最大深度必须大于0", nameof(maxDepth));

            _maxDepth = maxDepth;
            Root = new OctreeNode<T>(0, initialBounds, OctreeNodeFlags.Empty);
        }

        /// <summary>
        /// 插入一个点到八叉树，标记为 Blocked
        /// </summary>
        public bool Insert(Vector3 position)
        {
            return Insert(Root, position);
        }

        private bool Insert(OctreeNode<T> node, Vector3 position)
        {
            if (!node.bounds.Contains(position)) return false;

            if (node.depth < _maxDepth)
            {
                if (node.IsLeaf)
                    Subdivide(node);

                foreach (var child in node.children)
                {
                    if (Insert(child, position))
                        return true;
                }
                return false;
            }
            else
            {
                if (node.flags != OctreeNodeFlags.Blocked)
                {
                    node.flags = OctreeNodeFlags.Blocked;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 插入一个包围盒到八叉树，标记为 Blocked
        /// </summary>
        public bool Insert(Bounds bounds)
        {
            return Insert(Root, bounds);
        }

        private bool Insert(OctreeNode<T> node, Bounds bounds)
        {
            if (!node.bounds.Intersects(bounds)) return false;

            if (bounds.ContainsBounds(node.bounds))
            {
                if (node.flags != OctreeNodeFlags.Blocked)
                {
                    node.children = null;
                    node.flags = OctreeNodeFlags.Blocked;
                    return true;
                }
                return false;
            }

            if (node.IsLeaf && node.depth < _maxDepth)
                Subdivide(node);

            bool changed = false;
            if (!node.IsLeaf)
            {
                foreach (var child in node.children)
                    changed |= Insert(child, bounds);
            }
            else
            {
                if (node.flags != OctreeNodeFlags.Blocked)
                {
                    node.flags = OctreeNodeFlags.Blocked;
                    changed = true;
                }
            }

            if (changed)
                UpdateNodeFlags(node);

            return changed;
        }

        /// <summary>
        /// 插入带数据的点
        /// </summary>
        public bool Insert(T value, Vector3 position)
        {
            return Insert(Root, value, position);
        }

        private bool Insert(OctreeNode<T> node, T value, Vector3 position)
        {
            if (!node.bounds.Contains(position)) return false;

            if (node.depth < _maxDepth)
            {
                if (node.IsLeaf)
                    Subdivide(node);

                foreach (var child in node.children)
                {
                    if (Insert(child, value, position))
                        return true;
                }
                return false;
            }
            else
            {
                if (node.flags != OctreeNodeFlags.Blocked)
                {
                    node.value = value;
                    node.flags = OctreeNodeFlags.Blocked;
                    return true;
                }
                return false;
            }
        }

        private void Subdivide(OctreeNode<T> node)
        {
            if (!node.IsLeaf || node.depth >= _maxDepth) return;

            node.children = new OctreeNode<T>[8];
            node.flags = OctreeNodeFlags.Mixed;

            var size = node.bounds.size * 0.5f;
            var center = node.bounds.center;

            for (int i = 0; i < 8; i++)
            {
                var offset = new Vector3(
                    ((i & 1) == 0 ? -1 : 1) * size.x * 0.5f,
                    ((i & 2) == 0 ? -1 : 1) * size.y * 0.5f,
                    ((i & 4) == 0 ? -1 : 1) * size.z * 0.5f
                );
                var childCenter = center + offset;
                node.children[i] = new OctreeNode<T>(node.depth + 1, new Bounds(childCenter, size), OctreeNodeFlags.Empty);
            }
        }

        private void UpdateNodeFlags(OctreeNode<T> node)
        {
            if (node.IsLeaf) return;
            int blocked = 0, empty = 0;
            foreach (var child in node.children)
            {
                if (child.flags == OctreeNodeFlags.Blocked) blocked++;
                else if (child.flags == OctreeNodeFlags.Empty) empty++;
            }
            if (blocked == 8)
            {
                node.flags = OctreeNodeFlags.Blocked;
                node.children = null;
            }
            else if (empty == 8)
            {
                node.flags = OctreeNodeFlags.Empty;
                node.children = null;
            }
            else
            {
                node.flags = OctreeNodeFlags.Mixed;
            }
        }

        /// <summary>
        /// 移除一个点
        /// </summary>
        public bool Remove(Vector3 position)
        {
            return Remove(Root, position);
        }

        private bool Remove(OctreeNode<T> node, Vector3 position)
        {
            if (!node.bounds.Contains(position)) return false;

            if (!node.IsLeaf)
            {
                bool removed = false;
                foreach (var child in node.children)
                    removed |= Remove(child, position);

                if (removed)
                    UpdateNodeFlags(node);

                return removed;
            }
            else if (node.flags == OctreeNodeFlags.Blocked)
            {
                node.flags = OctreeNodeFlags.Empty;
                node.value = default;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空八叉树
        /// </summary>
        public void Clear()
        {
            Clear(Root);
        }

        private void Clear(OctreeNode<T> node)
        {
            if (!node.IsLeaf)
            {
                foreach (var child in node.children)
                    Clear(child);
                node.children = null;
            }
            node.flags = OctreeNodeFlags.Empty;
            node.value = default;
        }

        /// <summary>
        /// 获取所有 Blocked 的包围盒
        /// </summary>
        public List<Bounds> GetBlockedBounds()
        {
            var result = new List<Bounds>();
            GetBlockedBounds(Root, result);
            return result;
        }

        public void GetBlockedBounds(OctreeNode<T> node, List<Bounds> result)
        {
            if (node.flags == OctreeNodeFlags.Blocked)
            {
                result.Add(node.bounds);
            }
            else if (!node.IsLeaf)
            {
                foreach (var child in node.children)
                    GetBlockedBounds(child, result);
            }
        }
    }
}