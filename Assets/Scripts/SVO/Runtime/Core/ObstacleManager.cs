using System.Collections.Generic;
using UnityEngine;

namespace SVO.Runtime.Core
{
    /// <summary>
    /// 障碍物管理器 - 管理八叉树中的障碍物
    /// </summary>
    public class ObstacleManager : MonoBehaviour
    {
        [Header("障碍物设置")]
        public GameObject obstaclePrefab;
        public float obstacleSize = 1f;
        public LayerMask obstacleLayer = 1;
        
        [Header("自动检测")]
        public bool autoDetectObstacles = true;
        public float detectionRadius = 0.5f;
        
        private Dictionary<Vector3, GameObject> obstacleObjects;
        private Octree octree;
        
        void Awake()
        {
            obstacleObjects = new Dictionary<Vector3, GameObject>();
        }
        
        /// <summary>
        /// 设置要管理的八叉树
        /// </summary>
        public void SetOctree(Octree octree)
        {
            this.octree = octree;
            
            if (autoDetectObstacles)
            {
                DetectExistingObstacles();
            }
        }
        
        /// <summary>
        /// 在指定位置添加障碍物
        /// </summary>
        public bool AddObstacle(Vector3 position)
        {
            if (octree == null)
            {
                Debug.LogWarning("八叉树未设置，无法添加障碍物");
                return false;
            }
            
            // 获取节点位置
            OctreeNode node = octree.GetNodeAtPosition(position);
            if (node == null)
            {
                Debug.LogWarning("位置超出八叉树边界");
                return false;
            }
            
            Vector3 nodeCenter = node.Center;
            
            // 检查是否已经有障碍物
            if (obstacleObjects.ContainsKey(nodeCenter))
            {
                Debug.LogWarning("该位置已存在障碍物");
                return false;
            }
            
            // 在八叉树中标记为阻塞
            octree.SetNodeBlocked(nodeCenter, true);
            
            // 创建可视化对象
            GameObject obstacleObj = CreateObstacleObject(nodeCenter);
            if (obstacleObj != null)
            {
                obstacleObjects[nodeCenter] = obstacleObj;
            }
            
            Debug.Log($"在位置 {nodeCenter} 添加了障碍物");
            return true;
        }
        
        /// <summary>
        /// 移除指定位置的障碍物
        /// </summary>
        public bool RemoveObstacle(Vector3 position)
        {
            if (octree == null)
                return false;
                
            OctreeNode node = octree.GetNodeAtPosition(position);
            if (node == null)
                return false;
                
            Vector3 nodeCenter = node.Center;
            
            // 从八叉树中移除阻塞标记
            octree.SetNodeBlocked(nodeCenter, false);
            
            // 销毁可视化对象
            if (obstacleObjects.ContainsKey(nodeCenter))
            {
                GameObject obstacleObj = obstacleObjects[nodeCenter];
                if (obstacleObj != null)
                {
                    if (Application.isPlaying)
                        Destroy(obstacleObj);
                    else
                        DestroyImmediate(obstacleObj);
                }
                obstacleObjects.Remove(nodeCenter);
            }
            
            Debug.Log($"从位置 {nodeCenter} 移除了障碍物");
            return true;
        }
        
        /// <summary>
        /// 清除所有障碍物
        /// </summary>
        public void ClearAllObstacles()
        {
            foreach (var kvp in obstacleObjects)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying)
                        Destroy(kvp.Value);
                    else
                        DestroyImmediate(kvp.Value);
                }
                
                if (octree != null)
                {
                    octree.SetNodeBlocked(kvp.Key, false);
                }
            }
            
            obstacleObjects.Clear();
            Debug.Log("清除了所有障碍物");
        }
        
        /// <summary>
        /// 检测场景中现有的障碍物
        /// </summary>
        public void DetectExistingObstacles()
        {
            if (octree == null)
                return;
                
            // 查找场景中的障碍物
            Collider[] obstacles = Physics.OverlapBox(
                octree.bounds.center, 
                octree.bounds.size * 0.5f, 
                Quaternion.identity, 
                obstacleLayer
            );
            
            foreach (Collider obstacle in obstacles)
            {
                Vector3 obstaclePos = obstacle.bounds.center;
                OctreeNode node = octree.GetNodeAtPosition(obstaclePos);
                if (node != null)
                {
                    Vector3 nodeCenter = node.Center;
                    octree.SetNodeBlocked(nodeCenter, true);
                    
                    if (!obstacleObjects.ContainsKey(nodeCenter))
                    {
                        obstacleObjects[nodeCenter] = obstacle.gameObject;
                    }
                }
            }
            
            Debug.Log($"检测到 {obstacles.Length} 个现有障碍物");
        }
        
        /// <summary>
        /// 创建障碍物对象
        /// </summary>
        private GameObject CreateObstacleObject(Vector3 position)
        {
            GameObject obstacleObj = null;
            
            if (obstaclePrefab != null)
            {
                obstacleObj = Instantiate(obstaclePrefab, position, Quaternion.identity, transform);
            }
            else
            {
                // 创建默认立方体障碍物
                obstacleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacleObj.transform.position = position;
                obstacleObj.transform.SetParent(transform);
                obstacleObj.name = "Obstacle";
                
                // 设置材质
                Renderer renderer = obstacleObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material material = new Material(Shader.Find("Standard"));
                    material.color = Color.red;
                    material.SetFloat("_Metallic", 0.2f);
                    material.SetFloat("_Smoothness", 0.8f);
                    renderer.material = material;
                }
                
                // 设置层级
                obstacleObj.layer = Mathf.RoundToInt(Mathf.Log(obstacleLayer.value, 2));
            }
            
            if (obstacleObj != null)
            {
                obstacleObj.transform.localScale = Vector3.one * obstacleSize;
            }
            
            return obstacleObj;
        }
        
        /// <summary>
        /// 获取最近的障碍物位置
        /// </summary>
        public Vector3? GetNearestObstacle(Vector3 position)
        {
            Vector3? nearest = null;
            float nearestDistance = float.MaxValue;
            
            foreach (var obstaclePos in obstacleObjects.Keys)
            {
                float distance = Vector3.Distance(position, obstaclePos);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = obstaclePos;
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// 获取指定半径内的障碍物数量
        /// </summary>
        public int GetObstacleCountInRadius(Vector3 center, float radius)
        {
            int count = 0;
            
            foreach (var obstaclePos in obstacleObjects.Keys)
            {
                if (Vector3.Distance(center, obstaclePos) <= radius)
                {
                    count++;
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// 获取所有障碍物位置
        /// </summary>
        public List<Vector3> GetAllObstaclePositions()
        {
            return new List<Vector3>(obstacleObjects.Keys);
        }
        
        void OnDrawGizmosSelected()
        {
            if (octree != null)
            {
                // 绘制八叉树边界
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(octree.bounds.center, octree.bounds.size);
                
                // 绘制障碍物位置
                Gizmos.color = Color.red;
                foreach (var obstaclePos in obstacleObjects.Keys)
                {
                    Gizmos.DrawCube(obstaclePos, Vector3.one * obstacleSize);
                }
            }
        }
    }
}