using System.Collections.Generic;
using UnityEngine;

namespace SVO.Runtime.Core
{
    public static class OctreePathfinder
    {
        /// <summary>
        /// 使用A*算法查找从起始位置到结束位置的路径
        /// </summary>
        public static List<Vector3> FindPath(Octree octree, Vector3 startPos, Vector3 endPos)
        {
            OctreeNode startNode = octree.GetNodeAtPosition(startPos);
            OctreeNode endNode = octree.GetNodeAtPosition(endPos);
            
            if (startNode == null || endNode == null)
            {
                Debug.LogWarning("起始或结束位置在八叉树边界之外");
                return new List<Vector3>();
            }
            
            if (startNode.HasFlag(OctreeNodeFlags.Blocked) || endNode.HasFlag(OctreeNodeFlags.Blocked))
            {
                Debug.LogWarning("起始或结束位置被阻塞");
                return new List<Vector3>();
            }
            
            return FindPathAStar(octree, startNode, endNode);
        }
        
        /// <summary>
        /// A*寻路算法实现
        /// </summary>
        private static List<Vector3> FindPathAStar(Octree octree, OctreeNode startNode, OctreeNode endNode)
        {
            // 重置寻路数据
            octree.ResetPathfindingData();
            
            List<OctreeNode> openSet = new List<OctreeNode>();
            HashSet<OctreeNode> closedSet = new HashSet<OctreeNode>();
            
            // 初始化起始节点
            startNode.gCost = 0;
            startNode.hCost = CalculateHeuristic(startNode, endNode);
            startNode.SetFlag(OctreeNodeFlags.InOpenSet, true);
            openSet.Add(startNode);
            
            while (openSet.Count > 0)
            {
                // 查找f成本最低的节点
                OctreeNode currentNode = GetLowestFCostNode(openSet);
                
                // 从开放集中移除并添加到关闭集
                openSet.Remove(currentNode);
                currentNode.SetFlag(OctreeNodeFlags.InOpenSet, false);
                currentNode.SetFlag(OctreeNodeFlags.InClosedSet, true);
                closedSet.Add(currentNode);
                
                // 检查是否到达目标
                if (currentNode == endNode)
                {
                    return ReconstructPath(startNode, endNode);
                }
                
                // 检查所有邻居
                List<OctreeNode> neighbors = octree.GetNeighbors(currentNode);
                foreach (OctreeNode neighbor in neighbors)
                {
                    // 如果被阻塞或已在关闭集中则跳过
                    if (neighbor.HasFlag(OctreeNodeFlags.Blocked) || closedSet.Contains(neighbor))
                        continue;
                    
                    float tentativeGCost = currentNode.gCost + CalculateDistance(currentNode, neighbor);
                    
                    // 如果到邻居的这条路径比之前任何路径都好
                    if (tentativeGCost < neighbor.gCost)
                    {
                        neighbor.pathParent = currentNode;
                        neighbor.gCost = tentativeGCost;
                        neighbor.hCost = CalculateHeuristic(neighbor, endNode);
                        
                        if (!neighbor.HasFlag(OctreeNodeFlags.InOpenSet))
                        {
                            neighbor.SetFlag(OctreeNodeFlags.InOpenSet, true);
                            openSet.Add(neighbor);
                        }
                    }
                }
            }
            
            // 未找到路径
            Debug.LogWarning("在起始和结束位置之间未找到路径");
            return new List<Vector3>();
        }
        
        /// <summary>
        /// 计算启发式距离（八叉树的曼哈顿距离）
        /// </summary>
        private static float CalculateHeuristic(OctreeNode nodeA, OctreeNode nodeB)
        {
            Vector3 a = nodeA.Center;
            Vector3 b = nodeB.Center;
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
        }
        
        /// <summary>
        /// 计算两个节点之间的实际距离
        /// </summary>
        private static float CalculateDistance(OctreeNode nodeA, OctreeNode nodeB)
        {
            return Vector3.Distance(nodeA.Center, nodeB.Center);
        }
        
        /// <summary>
        /// 从开放集中获取f成本最低的节点
        /// </summary>
        private static OctreeNode GetLowestFCostNode(List<OctreeNode> openSet)
        {
            OctreeNode lowestNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < lowestNode.fCost || 
                    (openSet[i].fCost == lowestNode.fCost && openSet[i].hCost < lowestNode.hCost))
                {
                    lowestNode = openSet[i];
                }
            }
            return lowestNode;
        }
        
        /// <summary>
        /// 重构从起始到结束的路径
        /// </summary>
        private static List<Vector3> ReconstructPath(OctreeNode startNode, OctreeNode endNode)
        {
            List<Vector3> path = new List<Vector3>();
            OctreeNode currentNode = endNode;
            
            while (currentNode != null)
            {
                currentNode.SetFlag(OctreeNodeFlags.IsPath, true);
                path.Add(currentNode.Center);
                currentNode = currentNode.pathParent;
            }
            
            path.Reverse();
            return path;
        }
        
        /// <summary>
        /// 通过移除不必要的路径点来平滑路径
        /// </summary>
        public static List<Vector3> SmoothPath(List<Vector3> path, Octree octree)
        {
            if (path.Count <= 2)
                return path;
                
            List<Vector3> smoothedPath = new List<Vector3>();
            smoothedPath.Add(path[0]);
            
            int currentIndex = 0;
            while (currentIndex < path.Count - 1)
            {
                int farthestIndex = currentIndex + 1;
                
                // 找到我们能直线到达的最远点
                for (int i = currentIndex + 2; i < path.Count; i++)
                {
                    if (HasClearPath(path[currentIndex], path[i], octree))
                    {
                        farthestIndex = i;
                    }
                    else
                    {
                        break;
                    }
                }
                
                smoothedPath.Add(path[farthestIndex]);
                currentIndex = farthestIndex;
            }
            
            return smoothedPath;
        }
        
        /// <summary>
        /// 检查两点之间是否有畅通的路径
        /// </summary>
        private static bool HasClearPath(Vector3 start, Vector3 end, Octree octree)
        {
            Vector3 direction = end - start;
            float distance = direction.magnitude;
            direction.Normalize();
            
            float stepSize = octree.minNodeSize * 0.5f;
            int steps = Mathf.CeilToInt(distance / stepSize);
            
            for (int i = 0; i <= steps; i++)
            {
                Vector3 checkPos = start + direction * (i * stepSize);
                OctreeNode node = octree.GetNodeAtPosition(checkPos);
                
                if (node != null && node.HasFlag(OctreeNodeFlags.Blocked))
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}