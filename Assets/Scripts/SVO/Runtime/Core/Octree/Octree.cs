using System.Collections.Generic;
using UnityEngine;

namespace SVO.Runtime.Core
{
    public class Octree
    {
        public OctreeNode root;
        public int maxDepth;
        public float minNodeSize;
        public Bounds bounds;
        
        private List<OctreeNode> allNodes;
        
        public Octree(Bounds bounds, int maxDepth = 6, float minNodeSize = 1f)
        {
            this.bounds = bounds;
            this.maxDepth = maxDepth;
            this.minNodeSize = minNodeSize;
            this.allNodes = new List<OctreeNode>();
            
            BuildOctree();
        }
        
        /// <summary>
        /// 构建八叉树结构
        /// </summary>
        private void BuildOctree()
        {
            root = new OctreeNode(bounds, 0, false);
            allNodes.Add(root);
            SubdivideNode(root);
        }
        
        /// <summary>
        /// 递归地将节点细分为8个子节点
        /// </summary>
        private void SubdivideNode(OctreeNode node)
        {
            if (node.depth >= maxDepth || node.bounds.size.x <= minNodeSize)
            {
                node.isLeaf = true;
                return;
            }
            
            Vector3 center = node.bounds.center;
            Vector3 size = node.bounds.size * 0.5f;
            
            // 创建8个子节点
            for (int i = 0; i < 8; i++)
            {
                Vector3 childCenter = center + new Vector3(
                    (i & 1) == 0 ? -size.x * 0.5f : size.x * 0.5f,
                    (i & 2) == 0 ? -size.y * 0.5f : size.y * 0.5f,
                    (i & 4) == 0 ? -size.z * 0.5f : size.z * 0.5f
                );
                
                Bounds childBounds = new Bounds(childCenter, size);
                OctreeNode child = new OctreeNode(childBounds, node.depth + 1, false);
                child.parent = node;
                
                node.children[i] = child;
                allNodes.Add(child);
                
                SubdivideNode(child);
            }
        }
        
        /// <summary>
        /// 获取指定位置的叶子节点
        /// </summary>
        public OctreeNode GetNodeAtPosition(Vector3 position)
        {
            return GetNodeAtPositionRecursive(root, position);
        }
        
        private OctreeNode GetNodeAtPositionRecursive(OctreeNode node, Vector3 position)
        {
            if (!node.bounds.Contains(position))
                return null;
                
            if (node.isLeaf)
                return node;
                
            for (int i = 0; i < 8; i++)
            {
                if (node.children[i] != null)
                {
                    var result = GetNodeAtPositionRecursive(node.children[i], position);
                    if (result != null)
                        return result;
                }
            }
            
            return node;
        }
        
        /// <summary>
        /// 获取所有用于寻路的叶子节点
        /// </summary>
        public List<OctreeNode> GetLeafNodes()
        {
            List<OctreeNode> leafNodes = new List<OctreeNode>();
            GetLeafNodesRecursive(root, leafNodes);
            return leafNodes;
        }
        
        private void GetLeafNodesRecursive(OctreeNode node, List<OctreeNode> leafNodes)
        {
            if (node.isLeaf)
            {
                leafNodes.Add(node);
                return;
            }
            
            for (int i = 0; i < 8; i++)
            {
                if (node.children[i] != null)
                {
                    GetLeafNodesRecursive(node.children[i], leafNodes);
                }
            }
        }
        
        /// <summary>
        /// 标记节点为阻塞状态（障碍物）
        /// </summary>
        public void SetNodeBlocked(Vector3 position, bool blocked = true)
        {
            OctreeNode node = GetNodeAtPosition(position);
            if (node != null)
            {
                node.SetFlag(OctreeNodeFlags.Blocked, blocked);
                node.SetFlag(OctreeNodeFlags.Walkable, !blocked);
            }
        }
        
        /// <summary>
        /// 重置所有寻路数据
        /// </summary>
        public void ResetPathfindingData()
        {
            foreach (var node in allNodes)
            {
                node.ResetPathfindingData();
            }
        }
        
        /// <summary>
        /// 获取用于寻路的相邻叶子节点
        /// </summary>
        public List<OctreeNode> GetNeighbors(OctreeNode node)
        {
            List<OctreeNode> neighbors = new List<OctreeNode>();
            Vector3 nodeCenter = node.Center;
            float nodeSize = node.bounds.size.x;
            
            // 检查26个可能的邻居方向（3x3x3 - 1个中心点）
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0)
                            continue;
                            
                        Vector3 neighborPos = nodeCenter + new Vector3(x, y, z) * nodeSize;
                        OctreeNode neighbor = GetNodeAtPosition(neighborPos);
                        
                        if (neighbor != null && neighbor != node && neighbor.isLeaf)
                        {
                            neighbors.Add(neighbor);
                        }
                    }
                }
            }
            
            return neighbors;
        }
    }
}