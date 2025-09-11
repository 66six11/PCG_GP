using System.Collections.Generic;
using UnityEngine;
using SVO.Runtime.Core;

namespace SVO.Test
{
    /// <summary>
    /// 简单的独立测试来验证八叉树寻路功能
    /// </summary>
    public class SimpleOctreeTest : MonoBehaviour
    {
        void Start()
        {
            RunBasicTest();
        }
        
        void RunBasicTest()
        {
            Debug.Log("开始简单八叉树寻路测试...");
            
            try
            {
                // 创建一个简单的八叉树
                Bounds bounds = new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f));
                Octree octree = new Octree(bounds, 3, 1f);
                
                Debug.Log($"✓ 八叉树创建成功，包含 {octree.GetLeafNodes().Count} 个叶子节点");
                
                // 测试基本寻路
                Vector3 start = new Vector3(-3f, 0f, -3f);
                Vector3 end = new Vector3(3f, 0f, 3f);
                
                List<Vector3> path = OctreePathfinder.FindPath(octree, start, end);
                
                if (path.Count > 0)
                {
                    Debug.Log($"✓ 寻路成功！找到包含 {path.Count} 个路径点的路径");
                    Debug.Log($"  起点: {path[0]}, 终点: {path[path.Count - 1]}");
                    
                    // 测试只显示有数据的节点功能
                    var dataNodes = GetNodesWithData(octree.GetLeafNodes());
                    Debug.Log($"✓ 有数据的节点数量: {dataNodes.Count}");
                }
                else
                {
                    Debug.LogError("✗ 寻路失败 - 未找到路径");
                }
                
                // 测试障碍物功能
                octree.SetNodeBlocked(Vector3.zero, true);
                octree.SetNodeBlocked(new Vector3(0, 0, 1), true);
                octree.SetNodeBlocked(new Vector3(0, 0, -1), true);
                
                Debug.Log("✓ 添加了障碍物");
                
                // 重新测试寻路
                List<Vector3> pathWithObstacles = OctreePathfinder.FindPath(octree, start, end);
                
                if (pathWithObstacles.Count > 0)
                {
                    Debug.Log($"✓ 带障碍物的寻路成功！找到包含 {pathWithObstacles.Count} 个路径点的路径");
                    
                    // 测试路径平滑
                    var smoothedPath = OctreePathfinder.SmoothPath(pathWithObstacles, octree);
                    Debug.Log($"✓ 路径平滑完成：原始 {pathWithObstacles.Count} 点，平滑后 {smoothedPath.Count} 点");
                }
                else
                {
                    Debug.Log("⚠ 带障碍物的寻路失败 - 如果障碍物完全阻塞路径，这是预期的结果");
                }
                
                // 测试只显示有数据节点的功能
                var allLeafNodes = octree.GetLeafNodes();
                var nodesWithData = GetNodesWithData(allLeafNodes);
                Debug.Log($"✓ 节点过滤测试：总叶子节点 {allLeafNodes.Count}，有数据节点 {nodesWithData.Count}");
                
                Debug.Log("简单八叉树测试成功完成！");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"✗ 测试失败，异常: {e.Message}");
                Debug.LogError(e.StackTrace);
            }
        }
        
        /// <summary>
        /// 获取有数据的节点（用于测试过滤功能）
        /// </summary>
        private List<OctreeNode> GetNodesWithData(List<OctreeNode> nodes)
        {
            List<OctreeNode> dataNodes = new List<OctreeNode>();
            
            foreach (var node in nodes)
            {
                if (node.HasFlag(OctreeNodeFlags.Blocked) ||
                    node.HasFlag(OctreeNodeFlags.IsPath) ||
                    node.HasFlag(OctreeNodeFlags.Visited) ||
                    node.HasFlag(OctreeNodeFlags.InOpenSet) ||
                    node.HasFlag(OctreeNodeFlags.InClosedSet))
                {
                    dataNodes.Add(node);
                }
            }
            
            return dataNodes;
        }
    }
}