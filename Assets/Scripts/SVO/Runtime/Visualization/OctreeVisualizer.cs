using System.Collections.Generic;
using UnityEngine;
using SVO.Runtime.Core;

namespace SVO.Runtime.Visualization
{
    [RequireComponent(typeof(LineRenderer))]
    public class OctreeVisualizer : MonoBehaviour
    {
        [Header("八叉树设置")]
        public Vector3 octreeSize = new Vector3(20f, 20f, 20f);
        public int maxDepth = 4;
        public float minNodeSize = 1f;
        
        [Header("可视化设置")]
        public bool showOctree = true;
        public bool showOnlyLeafNodes = true;
        public bool showOnlyDataNodes = true; // 新增：仅显示有数据的节点
        public bool showBlockedNodes = true;
        public bool showPathNodes = true;
        
        [Header("颜色")]
        public Color normalNodeColor = Color.white;
        public Color blockedNodeColor = Color.red;
        public Color pathNodeColor = Color.green;
        public Color openSetColor = Color.yellow;
        public Color closedSetColor = Color.blue;
        
        [Header("材质")]
        public Material lineMaterial;
        
        [Header("组件引用")]
        public ObstacleManager obstacleManager;
        
        private Octree octree;
        private List<LineRenderer> nodeRenderers;
        private bool octreeInitialized = false;
        
        void Start()
        {
            InitializeOctree();
            nodeRenderers = new List<LineRenderer>();
        }
        
        void Update()
        {
            if (showOctree && octreeInitialized)
            {
                UpdateVisualization();
            }
        }
        
        /// <summary>
        /// 初始化八叉树
        /// </summary>
        public void InitializeOctree()
        {
            Bounds bounds = new Bounds(transform.position, octreeSize);
            octree = new Octree(bounds, maxDepth, minNodeSize);
            octreeInitialized = true;
            
            Debug.Log($"八叉树已初始化，包含 {octree.GetLeafNodes().Count} 个叶子节点");
            
            // 设置障碍物管理器
            if (obstacleManager == null)
                obstacleManager = GetComponent<ObstacleManager>();
            if (obstacleManager != null)
                obstacleManager.SetOctree(octree);
        }
        
        /// <summary>
        /// 更新八叉树的可视化
        /// </summary>
        private void UpdateVisualization()
        {
            ClearVisualization();
            
            if (octree == null)
                return;
                
            List<OctreeNode> nodesToDraw;
            
            if (showOnlyLeafNodes)
            {
                nodesToDraw = octree.GetLeafNodes();
            }
            else
            {
                nodesToDraw = GetAllNodes();
            }
            
            // 如果启用了仅显示有数据的节点，则过滤节点
            if (showOnlyDataNodes)
            {
                nodesToDraw = FilterNodesWithData(nodesToDraw);
            }
            
            foreach (OctreeNode node in nodesToDraw)
            {
                DrawNode(node);
            }
        }
        
        /// <summary>
        /// 获取八叉树中的所有节点
        /// </summary>
        private List<OctreeNode> GetAllNodes()
        {
            List<OctreeNode> allNodes = new List<OctreeNode>();
            GetAllNodesRecursive(octree.root, allNodes);
            return allNodes;
        }
        
        private void GetAllNodesRecursive(OctreeNode node, List<OctreeNode> nodes)
        {
            nodes.Add(node);
            
            if (!node.isLeaf)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (node.children[i] != null)
                    {
                        GetAllNodesRecursive(node.children[i], nodes);
                    }
                }
            }
        }
        
        /// <summary>
        /// 过滤只有数据的节点（阻塞、路径、已访问等）
        /// </summary>
        private List<OctreeNode> FilterNodesWithData(List<OctreeNode> nodes)
        {
            List<OctreeNode> dataNodes = new List<OctreeNode>();
            
            foreach (var node in nodes)
            {
                // 检查节点是否包含有意义的数据
                if (HasMeaningfulData(node))
                {
                    dataNodes.Add(node);
                }
            }
            
            return dataNodes;
        }
        
        /// <summary>
        /// 检查节点是否包含有意义的数据
        /// </summary>
        private bool HasMeaningfulData(OctreeNode node)
        {
            return node.HasFlag(OctreeNodeFlags.Blocked) ||
                   node.HasFlag(OctreeNodeFlags.IsPath) ||
                   node.HasFlag(OctreeNodeFlags.Visited) ||
                   node.HasFlag(OctreeNodeFlags.InOpenSet) ||
                   node.HasFlag(OctreeNodeFlags.InClosedSet);
        }
        
        /// <summary>
        /// 将节点绘制为线框立方体
        /// </summary>
        private void DrawNode(OctreeNode node)
        {
            Color nodeColor = GetNodeColor(node);
            DrawWireframeCube(node.bounds, nodeColor);
        }
        
        /// <summary>
        /// 根据节点状态获取相应的颜色
        /// </summary>
        private Color GetNodeColor(OctreeNode node)
        {
            if (node.HasFlag(OctreeNodeFlags.IsPath) && showPathNodes)
                return pathNodeColor;
            else if (node.HasFlag(OctreeNodeFlags.Blocked) && showBlockedNodes)
                return blockedNodeColor;
            else if (node.HasFlag(OctreeNodeFlags.InOpenSet))
                return openSetColor;
            else if (node.HasFlag(OctreeNodeFlags.InClosedSet))
                return closedSetColor;
            else
                return normalNodeColor;
        }
        
        /// <summary>
        /// 为节点绘制线框立方体
        /// </summary>
        private void DrawWireframeCube(Bounds bounds, Color color)
        {
            Vector3 center = bounds.center;
            Vector3 size = bounds.size;
            
            // 计算立方体的8个顶点
            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-size.x, -size.y, -size.z) * 0.5f; // Bottom-back-left
            corners[1] = center + new Vector3(size.x, -size.y, -size.z) * 0.5f;  // Bottom-back-right
            corners[2] = center + new Vector3(size.x, -size.y, size.z) * 0.5f;   // Bottom-front-right
            corners[3] = center + new Vector3(-size.x, -size.y, size.z) * 0.5f;  // Bottom-front-left
            corners[4] = center + new Vector3(-size.x, size.y, -size.z) * 0.5f;  // Top-back-left
            corners[5] = center + new Vector3(size.x, size.y, -size.z) * 0.5f;   // Top-back-right
            corners[6] = center + new Vector3(size.x, size.y, size.z) * 0.5f;    // Top-front-right
            corners[7] = center + new Vector3(-size.x, size.y, size.z) * 0.5f;   // Top-front-left
            
            // 绘制立方体的12条边
            DrawLine(corners[0], corners[1], color); // Bottom edges
            DrawLine(corners[1], corners[2], color);
            DrawLine(corners[2], corners[3], color);
            DrawLine(corners[3], corners[0], color);
            
            DrawLine(corners[4], corners[5], color); // Top edges
            DrawLine(corners[5], corners[6], color);
            DrawLine(corners[6], corners[7], color);
            DrawLine(corners[7], corners[4], color);
            
            DrawLine(corners[0], corners[4], color); // Vertical edges
            DrawLine(corners[1], corners[5], color);
            DrawLine(corners[2], corners[6], color);
            DrawLine(corners[3], corners[7], color);
        }
        
        /// <summary>
        /// 使用Debug.DrawLine绘制线条（在场景视图中可见）
        /// </summary>
        private void DrawLine(Vector3 start, Vector3 end, Color color)
        {
            Debug.DrawLine(start, end, color);
        }
        
        /// <summary>
        /// 清除所有可视化
        /// </summary>
        private void ClearVisualization()
        {
            foreach (LineRenderer renderer in nodeRenderers)
            {
                if (renderer != null)
                    DestroyImmediate(renderer.gameObject);
            }
            nodeRenderers.Clear();
        }
        
        /// <summary>
        /// 获取八叉树实例
        /// </summary>
        public Octree GetOctree()
        {
            return octree;
        }
        
        /// <summary>
        /// 在指定位置设置节点为阻塞状态
        /// </summary>
        public void SetNodeBlocked(Vector3 position, bool blocked = true)
        {
            if (octree != null)
            {
                octree.SetNodeBlocked(position, blocked);
                
                // 同步障碍物管理器
                if (obstacleManager != null)
                {
                    if (blocked)
                        obstacleManager.AddObstacle(position);
                    else
                        obstacleManager.RemoveObstacle(position);
                }
            }
        }
        
        /// <summary>
        /// 刷新八叉树和可视化
        /// </summary>
        public void RefreshOctree()
        {
            InitializeOctree();
        }
        
        void OnDrawGizmos()
        {
            // 绘制八叉树边界
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, octreeSize);
        }
    }
}