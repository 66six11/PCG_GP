using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using SVO.Runtime.Core;
using SVO.Runtime.Visualization;

namespace SVO.Editor
{
    /// <summary>
    /// 八叉树编辑器可视化工具，仅显示有数据的节点
    /// </summary>
    [CustomEditor(typeof(OctreeVisualizer))]
    public class OctreeEditor : UnityEditor.Editor
    {
        private OctreeVisualizer visualizer;
        private bool showSettings = true;
        private bool showStatistics = true;
        private bool showControls = true;

        void OnEnable()
        {
            visualizer = (OctreeVisualizer)target;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            // 设置面板
            showSettings = EditorGUILayout.Foldout(showSettings, "八叉树设置", true);
            if (showSettings)
            {
                EditorGUI.indentLevel++;
                DrawSettingsPanel();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 统计信息面板
            showStatistics = EditorGUILayout.Foldout(showStatistics, "统计信息", true);
            if (showStatistics)
            {
                EditorGUI.indentLevel++;
                DrawStatisticsPanel();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 控制面板
            showControls = EditorGUILayout.Foldout(showControls, "控制", true);
            if (showControls)
            {
                EditorGUI.indentLevel++;
                DrawControlsPanel();
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// 绘制设置面板
        /// </summary>
        private void DrawSettingsPanel()
        {
            EditorGUILayout.LabelField("可视化选项:", EditorStyles.boldLabel);
            
            visualizer.showOctree = EditorGUILayout.Toggle("显示八叉树", visualizer.showOctree);
            visualizer.showOnlyLeafNodes = EditorGUILayout.Toggle("仅显示叶子节点", visualizer.showOnlyLeafNodes);
            visualizer.showBlockedNodes = EditorGUILayout.Toggle("显示阻塞节点", visualizer.showBlockedNodes);
            visualizer.showPathNodes = EditorGUILayout.Toggle("显示路径节点", visualizer.showPathNodes);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("颜色设置:", EditorStyles.boldLabel);
            
            visualizer.normalNodeColor = EditorGUILayout.ColorField("普通节点颜色", visualizer.normalNodeColor);
            visualizer.blockedNodeColor = EditorGUILayout.ColorField("阻塞节点颜色", visualizer.blockedNodeColor);
            visualizer.pathNodeColor = EditorGUILayout.ColorField("路径节点颜色", visualizer.pathNodeColor);
            visualizer.openSetColor = EditorGUILayout.ColorField("开放集颜色", visualizer.openSetColor);
            visualizer.closedSetColor = EditorGUILayout.ColorField("关闭集颜色", visualizer.closedSetColor);
        }

        /// <summary>
        /// 绘制统计信息面板
        /// </summary>
        private void DrawStatisticsPanel()
        {
            if (visualizer.GetOctree() != null)
            {
                var octree = visualizer.GetOctree();
                var leafNodes = octree.GetLeafNodes();
                var dataNodes = GetNodesWithData(leafNodes);

                EditorGUILayout.LabelField("八叉树信息:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"最大深度: {octree.maxDepth}");
                EditorGUILayout.LabelField($"最小节点大小: {octree.minNodeSize:F2}");
                EditorGUILayout.LabelField($"边界: {octree.bounds}");
                
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("节点统计:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"总叶子节点数: {leafNodes.Count}");
                EditorGUILayout.LabelField($"有数据的节点数: {dataNodes.Count}");
                EditorGUILayout.LabelField($"阻塞节点数: {CountNodesWithFlag(leafNodes, OctreeNodeFlags.Blocked)}");
                EditorGUILayout.LabelField($"路径节点数: {CountNodesWithFlag(leafNodes, OctreeNodeFlags.IsPath)}");
                EditorGUILayout.LabelField($"已访问节点数: {CountNodesWithFlag(leafNodes, OctreeNodeFlags.Visited)}");
            }
            else
            {
                EditorGUILayout.HelpBox("八叉树未初始化", MessageType.Warning);
            }
        }

        /// <summary>
        /// 绘制控制面板
        /// </summary>
        private void DrawControlsPanel()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("初始化八叉树"))
            {
                visualizer.InitializeOctree();
                EditorUtility.SetDirty(visualizer);
            }
            
            if (GUILayout.Button("刷新八叉树"))
            {
                visualizer.RefreshOctree();
                EditorUtility.SetDirty(visualizer);
            }
            
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("障碍物工具:", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("在场景视图中Shift+左键点击添加障碍物\nCtrl+左键点击移除障碍物", MessageType.Info);
        }

        /// <summary>
        /// 获取有数据的节点（阻塞、路径、已访问等）
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

        /// <summary>
        /// 计算具有特定标志的节点数量
        /// </summary>
        private int CountNodesWithFlag(List<OctreeNode> nodes, OctreeNodeFlags flag)
        {
            int count = 0;
            foreach (var node in nodes)
            {
                if (node.HasFlag(flag))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 场景视图中的可视化
        /// </summary>
        void OnSceneGUI()
        {
            if (visualizer.GetOctree() == null || !visualizer.showOctree)
                return;

            var octree = visualizer.GetOctree();
            var leafNodes = octree.GetLeafNodes();
            var dataNodes = GetNodesWithData(leafNodes);

            // 只绘制有数据的节点
            foreach (var node in dataNodes)
            {
                Color nodeColor = GetNodeColor(node);
                DrawNodeInScene(node, nodeColor);
            }

            // 处理用户输入
            HandleSceneInput();
        }

        /// <summary>
        /// 在场景中绘制节点
        /// </summary>
        private void DrawNodeInScene(OctreeNode node, Color color)
        {
            Handles.color = color;
            
            // 绘制节点边界
            Vector3 center = node.bounds.center;
            Vector3 size = node.bounds.size;
            
            // 绘制立方体轮廓
            DrawWireCube(center, size);
            
            // 如果是阻塞节点，填充一部分
            if (node.HasFlag(OctreeNodeFlags.Blocked))
            {
                Handles.color = new Color(color.r, color.g, color.b, 0.3f);
                Handles.CubeHandleCap(0, center, Quaternion.identity, size.x * 0.8f, EventType.Repaint);
            }
        }

        /// <summary>
        /// 绘制线框立方体
        /// </summary>
        private void DrawWireCube(Vector3 center, Vector3 size)
        {
            Vector3 halfSize = size * 0.5f;
            
            // 计算立方体的8个顶点
            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z);
            corners[1] = center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z);
            corners[2] = center + new Vector3(halfSize.x, -halfSize.y, halfSize.z);
            corners[3] = center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z);
            corners[4] = center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z);
            corners[5] = center + new Vector3(halfSize.x, halfSize.y, -halfSize.z);
            corners[6] = center + new Vector3(halfSize.x, halfSize.y, halfSize.z);
            corners[7] = center + new Vector3(-halfSize.x, halfSize.y, halfSize.z);
            
            // 绘制立方体的12条边
            Handles.DrawLine(corners[0], corners[1]); // 底面
            Handles.DrawLine(corners[1], corners[2]);
            Handles.DrawLine(corners[2], corners[3]);
            Handles.DrawLine(corners[3], corners[0]);
            
            Handles.DrawLine(corners[4], corners[5]); // 顶面
            Handles.DrawLine(corners[5], corners[6]);
            Handles.DrawLine(corners[6], corners[7]);
            Handles.DrawLine(corners[7], corners[4]);
            
            Handles.DrawLine(corners[0], corners[4]); // 垂直边
            Handles.DrawLine(corners[1], corners[5]);
            Handles.DrawLine(corners[2], corners[6]);
            Handles.DrawLine(corners[3], corners[7]);
        }

        /// <summary>
        /// 根据节点状态获取颜色
        /// </summary>
        private Color GetNodeColor(OctreeNode node)
        {
            if (node.HasFlag(OctreeNodeFlags.IsPath))
                return visualizer.pathNodeColor;
            else if (node.HasFlag(OctreeNodeFlags.Blocked))
                return visualizer.blockedNodeColor;
            else if (node.HasFlag(OctreeNodeFlags.InOpenSet))
                return visualizer.openSetColor;
            else if (node.HasFlag(OctreeNodeFlags.InClosedSet))
                return visualizer.closedSetColor;
            else
                return visualizer.normalNodeColor;
        }

        /// <summary>
        /// 处理场景视图中的用户输入
        /// </summary>
        private void HandleSceneInput()
        {
            Event e = Event.current;
            
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Vector3 mousePos = e.mousePosition;
                Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
                
                // 投射到与八叉树中心同高度的平面
                Plane plane = new Plane(Vector3.up, visualizer.transform.position);
                float distance;
                
                if (plane.Raycast(ray, out distance))
                {
                    Vector3 hitPoint = ray.GetPoint(distance);
                    
                    if (e.shift)
                    {
                        // Shift + 左键：添加障碍物
                        visualizer.SetNodeBlocked(hitPoint, true);
                        EditorUtility.SetDirty(visualizer);
                        e.Use();
                    }
                    else if (e.control)
                    {
                        // Ctrl + 左键：移除障碍物
                        visualizer.SetNodeBlocked(hitPoint, false);
                        EditorUtility.SetDirty(visualizer);
                        e.Use();
                    }
                }
            }
        }
    }
}