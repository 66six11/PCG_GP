using UnityEngine;
using SVO.Runtime.Core;
using SVO.Runtime.Visualization;

namespace SVO.Runtime.Demo
{
    /// <summary>
    /// 八叉树场景设置助手 - 自动设置八叉树演示场景
    /// </summary>
    public class OctreeSceneSetup : MonoBehaviour
    {
        [Header("自动设置")]
        public bool setupOnStart = true;
        public bool createDefaultObstacles = true;
        
        [Header("八叉树设置")]
        public Vector3 octreeSize = new Vector3(20f, 10f, 20f);
        public int maxDepth = 4;
        public float minNodeSize = 1f;
        
        [Header("默认障碍物")]
        public int obstacleCount = 5;
        public float obstacleSpacing = 3f;
        
        private OctreeVisualizer visualizer;
        private ObstacleManager obstacleManager;
        private OctreePathfindingDemo demo;
        
        void Start()
        {
            if (setupOnStart)
            {
                SetupScene();
            }
        }
        
        /// <summary>
        /// 设置整个演示场景
        /// </summary>
        [ContextMenu("设置演示场景")]
        public void SetupScene()
        {
            // 创建或配置八叉树可视化器
            SetupOctreeVisualizer();
            
            // 创建或配置障碍物管理器
            SetupObstacleManager();
            
            // 创建或配置寻路演示
            SetupPathfindingDemo();
            
            // 创建默认障碍物
            if (createDefaultObstacles)
            {
                CreateDefaultObstacles();
            }
            
            Debug.Log("八叉树演示场景设置完成");
        }
        
        /// <summary>
        /// 设置八叉树可视化器
        /// </summary>
        private void SetupOctreeVisualizer()
        {
            visualizer = GetComponent<OctreeVisualizer>();
            if (visualizer == null)
            {
                visualizer = gameObject.AddComponent<OctreeVisualizer>();
            }
            
            // 配置可视化器设置
            visualizer.octreeSize = octreeSize;
            visualizer.maxDepth = maxDepth;
            visualizer.minNodeSize = minNodeSize;
            visualizer.showOctree = true;
            visualizer.showOnlyLeafNodes = true;
            visualizer.showOnlyDataNodes = true; // 仅显示有数据的节点
            visualizer.showBlockedNodes = true;
            visualizer.showPathNodes = true;
            
            // 设置颜色
            visualizer.normalNodeColor = new Color(1f, 1f, 1f, 0.3f);
            visualizer.blockedNodeColor = Color.red;
            visualizer.pathNodeColor = Color.green;
            visualizer.openSetColor = Color.yellow;
            visualizer.closedSetColor = Color.blue;
            
            // 初始化八叉树
            visualizer.InitializeOctree();
        }
        
        /// <summary>
        /// 设置障碍物管理器
        /// </summary>
        private void SetupObstacleManager()
        {
            obstacleManager = GetComponent<ObstacleManager>();
            if (obstacleManager == null)
            {
                obstacleManager = gameObject.AddComponent<ObstacleManager>();
            }
            
            // 配置障碍物管理器
            obstacleManager.obstacleSize = minNodeSize * 0.8f;
            obstacleManager.autoDetectObstacles = true;
            
            // 将八叉树关联到障碍物管理器
            if (visualizer != null && visualizer.GetOctree() != null)
            {
                obstacleManager.SetOctree(visualizer.GetOctree());
            }
        }
        
        /// <summary>
        /// 设置寻路演示
        /// </summary>
        private void SetupPathfindingDemo()
        {
            demo = GetComponent<OctreePathfindingDemo>();
            if (demo == null)
            {
                demo = gameObject.AddComponent<OctreePathfindingDemo>();
            }
            
            // 配置演示组件引用
            demo.octreeVisualizer = visualizer;
            demo.obstacleManager = obstacleManager;
            
            // 查找或创建路径可视化器
            PathVisualizer pathVis = FindObjectOfType<PathVisualizer>();
            if (pathVis == null)
            {
                GameObject pathVisObj = new GameObject("PathVisualizer");
                pathVis = pathVisObj.AddComponent<PathVisualizer>();
                pathVis.gameObject.AddComponent<LineRenderer>();
            }
            demo.pathVisualizer = pathVis;
            
            // 配置演示设置
            demo.enableMouseInput = true;
            demo.smoothPath = true;
        }
        
        /// <summary>
        /// 创建默认障碍物来演示功能
        /// </summary>
        private void CreateDefaultObstacles()
        {
            if (obstacleManager == null)
                return;
                
            Vector3 center = transform.position;
            
            // 创建一个简单的障碍物墙
            for (int i = 0; i < obstacleCount; i++)
            {
                Vector3 obstaclePos = center + new Vector3(
                    (i - obstacleCount / 2) * obstacleSpacing,
                    0,
                    0
                );
                
                obstacleManager.AddObstacle(obstaclePos);
            }
            
            // 添加一些散布的障碍物
            for (int i = 0; i < 3; i++)
            {
                Vector3 randomPos = center + new Vector3(
                    Random.Range(-octreeSize.x * 0.3f, octreeSize.x * 0.3f),
                    0,
                    Random.Range(-octreeSize.z * 0.3f, octreeSize.z * 0.3f)
                );
                
                obstacleManager.AddObstacle(randomPos);
            }
        }
        
        /// <summary>
        /// 清除所有障碍物
        /// </summary>
        [ContextMenu("清除障碍物")]
        public void ClearObstacles()
        {
            if (obstacleManager != null)
            {
                obstacleManager.ClearAllObstacles();
                Debug.Log("已清除所有障碍物");
            }
        }
        
        /// <summary>
        /// 重新创建默认障碍物
        /// </summary>
        [ContextMenu("重新创建障碍物")]
        public void RecreateObstacles()
        {
            ClearObstacles();
            CreateDefaultObstacles();
        }
        
        void OnDrawGizmosSelected()
        {
            // 绘制八叉树边界
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, octreeSize);
            
            // 绘制网格参考
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Vector3 center = transform.position;
            
            // 绘制网格线
            for (float x = -octreeSize.x * 0.5f; x <= octreeSize.x * 0.5f; x += minNodeSize)
            {
                Vector3 start = center + new Vector3(x, 0, -octreeSize.z * 0.5f);
                Vector3 end = center + new Vector3(x, 0, octreeSize.z * 0.5f);
                Gizmos.DrawLine(start, end);
            }
            
            for (float z = -octreeSize.z * 0.5f; z <= octreeSize.z * 0.5f; z += minNodeSize)
            {
                Vector3 start = center + new Vector3(-octreeSize.x * 0.5f, 0, z);
                Vector3 end = center + new Vector3(octreeSize.x * 0.5f, 0, z);
                Gizmos.DrawLine(start, end);
            }
        }
    }
}