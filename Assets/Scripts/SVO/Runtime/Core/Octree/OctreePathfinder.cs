using System.Collections.Generic;
using UnityEngine;

namespace SVO.Runtime.Core
{
    public static class OctreePathfinder
    {
        /// <summary>
        /// Find a path from start to end position using A* algorithm
        /// </summary>
        public static List<Vector3> FindPath(Octree octree, Vector3 startPos, Vector3 endPos)
        {
            OctreeNode startNode = octree.GetNodeAtPosition(startPos);
            OctreeNode endNode = octree.GetNodeAtPosition(endPos);
            
            if (startNode == null || endNode == null)
            {
                Debug.LogWarning("Start or end position is outside octree bounds");
                return new List<Vector3>();
            }
            
            if (startNode.HasFlag(OctreeNodeFlags.Blocked) || endNode.HasFlag(OctreeNodeFlags.Blocked))
            {
                Debug.LogWarning("Start or end position is blocked");
                return new List<Vector3>();
            }
            
            return FindPathAStar(octree, startNode, endNode);
        }
        
        /// <summary>
        /// A* pathfinding algorithm implementation
        /// </summary>
        private static List<Vector3> FindPathAStar(Octree octree, OctreeNode startNode, OctreeNode endNode)
        {
            // Reset pathfinding data
            octree.ResetPathfindingData();
            
            List<OctreeNode> openSet = new List<OctreeNode>();
            HashSet<OctreeNode> closedSet = new HashSet<OctreeNode>();
            
            // Initialize start node
            startNode.gCost = 0;
            startNode.hCost = CalculateHeuristic(startNode, endNode);
            startNode.SetFlag(OctreeNodeFlags.InOpenSet, true);
            openSet.Add(startNode);
            
            while (openSet.Count > 0)
            {
                // Find node with lowest f-cost
                OctreeNode currentNode = GetLowestFCostNode(openSet);
                
                // Remove from open set and add to closed set
                openSet.Remove(currentNode);
                currentNode.SetFlag(OctreeNodeFlags.InOpenSet, false);
                currentNode.SetFlag(OctreeNodeFlags.InClosedSet, true);
                closedSet.Add(currentNode);
                
                // Check if we reached the target
                if (currentNode == endNode)
                {
                    return ReconstructPath(startNode, endNode);
                }
                
                // Check all neighbors
                List<OctreeNode> neighbors = octree.GetNeighbors(currentNode);
                foreach (OctreeNode neighbor in neighbors)
                {
                    // Skip if blocked or already in closed set
                    if (neighbor.HasFlag(OctreeNodeFlags.Blocked) || closedSet.Contains(neighbor))
                        continue;
                    
                    float tentativeGCost = currentNode.gCost + CalculateDistance(currentNode, neighbor);
                    
                    // If this path to neighbor is better than any previous one
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
            
            // No path found
            Debug.LogWarning("No path found between start and end positions");
            return new List<Vector3>();
        }
        
        /// <summary>
        /// Calculate heuristic distance (Manhattan distance for octree)
        /// </summary>
        private static float CalculateHeuristic(OctreeNode nodeA, OctreeNode nodeB)
        {
            Vector3 a = nodeA.Center;
            Vector3 b = nodeB.Center;
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
        }
        
        /// <summary>
        /// Calculate actual distance between two nodes
        /// </summary>
        private static float CalculateDistance(OctreeNode nodeA, OctreeNode nodeB)
        {
            return Vector3.Distance(nodeA.Center, nodeB.Center);
        }
        
        /// <summary>
        /// Get the node with the lowest f-cost from the open set
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
        /// Reconstruct the path from start to end
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
        /// Smooth the path by removing unnecessary waypoints
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
                
                // Find the farthest point we can reach in a straight line
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
        /// Check if there's a clear path between two points
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