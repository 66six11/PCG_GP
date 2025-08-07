using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Test
{
    public class KdTreeTest : MonoBehaviour
    {
        public GirdGenerator grid;

        public GameObject queryGameObject;
        public SphereCollider queryCollider;
        public BoxCollider queryBound;
        public int queryCount = 10000;

        public bool showKDTree = true;

        List<Vertex> queryVertices = new List<Vertex>();

        [ContextMenu("多次测试查询")]
        public void QueryKdTree()
        {
            Debug.Log("KDTree Total Nodes: " + grid._hexGrid.vertexKDTre.totalNodes);
            var timestamp = System.DateTime.Now.Ticks;
            for (int i = 0; i < queryCount; i++)
            {
                grid._hexGrid.vertexKDTre.QueryNearest(queryGameObject.transform.position);
            }

            var elapsed = System.DateTime.Now.Ticks - timestamp;
            Debug.Log("KDTree Query Time: " + (elapsed / 10000) + "ms");
        }

        [ContextMenu("临近点查询")]
        public void QueryKdTreeNearest()
        {
            queryVertices.Clear();
            queryVertices.Add(grid._hexGrid.vertexKDTre.QueryNearest(queryGameObject.transform.position));
        }

        [ContextMenu("球范围查询")]
        public void QueryKdTreeSphere()
        {
            queryVertices.Clear();
            queryVertices.AddRange(grid._hexGrid.vertexKDTre.RangeQuery(queryCollider.transform.position, queryCollider.radius));
        }

        [ContextMenu("矩形范围查询")]
        public void QueryKdTreeBound()
        {
            queryVertices.Clear();
            // 获取世界坐标系下的包围盒
            Bounds worldBounds = queryBound.bounds;
    
            // 直接使用世界坐标的 min/max
            Vector3 minBound = worldBounds.min;
            Vector3 maxBound = worldBounds.max;
          
            queryVertices.AddRange(grid._hexGrid.vertexKDTre.RangeQuery(minBound, maxBound));
        }

        private void OnDrawGizmos()
        {
            if (grid._hexGrid == null)
            {
                return;
            }

            if (grid._hexGrid.vertexKDTre == null)
            {
                return;
            }


            if (showKDTree)
            {
                DrawNodes(grid._hexGrid.vertexKDTre.root);
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(grid._hexGrid.vertexKDTre.root.vertex.position, 0.2f);
            }


            if (queryVertices.Count == 1)
            {
                Gizmos.color = Color.coral;
                foreach (var vertex in queryVertices)
                {
                    Gizmos.DrawSphere(vertex.position, 0.2f);
                    Gizmos.color = Color.aquamarine;
                    Gizmos.DrawLine(queryGameObject.transform.position, vertex.position);
                    var direction = (vertex.position - queryGameObject.transform.position);
                    Gizmos.DrawWireSphere(queryGameObject.transform.position, direction.magnitude);
                }
            }

            if (queryVertices.Count > 1)
            {
                Gizmos.color = Color.coral;
                foreach (var vertex in queryVertices)
                {
                    Gizmos.DrawSphere(vertex.position, 0.2f);
                }
            }
        }


        //Draw the nodes of the KDTree
        private void DrawNodes(Node node)
        {
            //x轴红色
            //y轴绿色
            //z轴蓝色
            Color color = Color.white;
            switch (node.splitAxis)
            {
                case (Axis)0:
                    color = Color.red;
                    break;
                case (Axis)1:
                    color = Color.green;
                    break;
                case (Axis)2:
                    color = Color.blue;
                    break;
            }

            Gizmos.color = color;
            Gizmos.DrawSphere(node.vertex.position, 0.1f);
            //绘制分割线
            // Vector3 start;
            // Vector3 end;
            // switch (node.splitAxis)
            // {
            //     case (Axis)0:
            //         start = new Vector3(node.vertex.position.x, node.vertex.position.y, node.vertex.position.z + 20f);
            //         end = new Vector3(node.vertex.position.x, node.vertex.position.y, node.vertex.position.z - 20f);
            //         break;
            //     case (Axis)1:
            //         start = new Vector3(node.vertex.position.x + 20f, node.vertex.position.y, node.vertex.position.z);
            //         end = new Vector3(node.vertex.position.x - 20f, node.vertex.position.y, node.vertex.position.z);
            //         break;
            //     case (Axis)2:
            //         start = new Vector3(node.vertex.position.x + 20f, node.vertex.position.y, node.vertex.position.z);
            //         end = new Vector3(node.vertex.position.x - 20f, node.vertex.position.y, node.vertex.position.z);
            //         break;
            //
            //
            //     default:
            //         start = Vector3.zero;
            //         end = Vector3.zero;
            //         break;
            // }
            //
            // Gizmos.DrawLine(start, end);
            //连接左右子节点

            if (node.leftChild != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(node.vertex.position, node.leftChild.vertex.position);

                DrawNodes(node.leftChild);
            }

            if (node.rightChild != null)
            {
                Gizmos.color = Color.orange;
                Gizmos.DrawLine(node.vertex.position, node.rightChild.vertex.position);
                DrawNodes(node.rightChild);
            }
        }
    }
}