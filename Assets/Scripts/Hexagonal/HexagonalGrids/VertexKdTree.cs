using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public enum Axis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    public class Node
    {
        public Vertex vertex;
        public Node parent;
        public Node leftChild;
        public Node rightChild;
        public Axis splitAxis;

        // public Node(Vertex vertex, Axis splitAxis)
        // {
        //     this.vertex = vertex;
        //     this.splitAxis = splitAxis;
        // }
    }

    public class VertexKdTree
    {
        public Node root;
        public int totalNodes;
        public int maxDepth;


        public VertexKdTree(List<Vertex> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            totalNodes = vertices.Count;
            // Partition a private array: building a spatial index must not reorder topology data.
            root = BuildTree(vertices.ToArray(), 0, vertices.Count, 0);
        }

        private Node BuildTree(Vertex[] vertices, int start, int count, int depth, Node parent = null)
        {
            if (count == 0) return null;
            maxDepth = Math.Max(maxDepth, depth);
            Axis axis = ChooseBestSplitAxis(vertices, start, count);
            int median = start + count / 2;
            SelectMedian(vertices, start, start + count - 1, median, axis);
            var node = new Node { vertex = vertices[median], splitAxis = axis, parent = parent };
            node.leftChild = BuildTree(vertices, start, median - start, depth + 1, node);
            node.rightChild = BuildTree(vertices, median + 1, start + count - median - 1, depth + 1, node);
            return node;
        }

        private void SelectMedian(Vertex[] vertices, int left, int right, int median, Axis axis)
        {
            while (left < right)
            {
                var pivot = vertices[left + (right - left) / 2];
                int i = left, j = right;
                while (i <= j)
                {
                    while (Compare(vertices[i], pivot, axis) < 0) i++;
                    while (Compare(vertices[j], pivot, axis) > 0) j--;
                    if (i > j) break;
                    var value = vertices[i]; vertices[i] = vertices[j]; vertices[j] = value;
                    i++; j--;
                }
                if (median <= j) right = j;
                else if (median >= i) left = i;
                else return;
            }
        }

        private int Compare(Vertex a, Vertex b, Axis axis)
        {
            int result = GetAxisValue(a.position, axis).CompareTo(GetAxisValue(b.position, axis));
            return result != 0 ? result : a.id.CompareTo(b.id);
        }

        private float GetAxisValue(Vector3 position, Axis axis1)
        {
            switch (axis1)
            {
                case Axis.X: return position.x;
                case Axis.Y: return position.y;
                default: return position.z;
            }
        }

        private Axis ChooseBestSplitAxis(Vertex[] vertices, int start, int count)
        {
            var min = vertices[start].position;
            var max = min;
            for (int i = start + 1; i < start + count; i++)
            {
                min = Vector3.Min(min, vertices[i].position);
                max = Vector3.Max(max, vertices[i].position);
            }
            var size = max - min;
            if (size.x >= size.y && size.x >= size.z) return Axis.X;
            return size.y >= size.z ? Axis.Y : Axis.Z;
        }

        public void Insert(Vertex vertex)
        {
            if (root == null)
            {
                root = new Node
                {
                    vertex = vertex,
                    splitAxis = Axis.X,
                    leftChild = null,
                    rightChild = null
                };
                totalNodes = 1;
                return;
            }

            // 递归插入
            InsertRecursive(root, vertex, 0);
            totalNodes++;
        }

        //TODO: 目前插入属于不平衡树，需要调整
        private void InsertRecursive(Node node, Vertex vertex, int depth)
        {
            Axis axis = node.splitAxis;

            // 计算比较值（当前轴）
            float nodeValue = GetAxisValue(node.vertex.position, axis);
            float newValue = GetAxisValue(vertex.position, axis);


            if (newValue <= nodeValue)
            {
                if (node.leftChild == null)
                {
                    // 创建新节点
                    node.leftChild = new Node
                    {
                        vertex = vertex,
                        splitAxis = (Axis)((depth + 1) % 3), // 使用下一深度决定分割轴
                        leftChild = null,
                        rightChild = null
                    };
                }
                else
                {
                    InsertRecursive(node.leftChild, vertex, depth + 1);
                }
            }
            else // newValue > nodeValue
            {
                if (node.rightChild == null)
                {
                    // 创建新节点
                    node.rightChild = new Node
                    {
                        vertex = vertex,
                        splitAxis = (Axis)((depth + 1) % 3), // 使用下一深度决定分割轴
                        leftChild = null,
                        rightChild = null
                    };
                }
                else
                {
                    InsertRecursive(node.rightChild, vertex, depth + 1);
                }
            }
        }

        /// <summary>
        /// 矩形范围查询
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public List<Vertex> RangeQuery(Vector3 min, Vector3 max)
        {
            List<Vertex> result = new List<Vertex>();

            RangeQueryRecursive(root, min.x, max.x, min.y, max.y, min.z, max.z, result);
            return result;
        }

        /// <summary>
        /// 球体范围查询
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public List<Vertex> RangeQuery(Vector3 center, float radius)
        {
            List<Vertex> result = new List<Vertex>();
            float sqrRadius = radius * radius; // 使用平方避免开方计算
            RadiusSearch(root, center, sqrRadius,  result);
            return result;
        }

        private void RadiusSearch(Node node, Vector3 queryPoint, float sqrRadius,  List<Vertex> results)
        {
            if (node == null) return;

            // 1. 计算当前点到查询点的平方距离
            Vector3 diff = node.vertex.position - queryPoint;
            float sqrDist = diff.sqrMagnitude;

            // 2. 如果点在半径范围内，添加到结果
            if (sqrDist <= sqrRadius)
            {
                results.Add(node.vertex);
            }

            // 3. 获取当前节点的分割轴
            Axis axis = node.splitAxis;

            // 4. 计算查询点到当前节点分割平面的距离
            float axisDist = GetAxisDistance(queryPoint, node.vertex.position, axis);

            // 5. 确定近子树和远子树
            Node nearChild = axisDist <= 0 ? node.leftChild : node.rightChild;
            Node farChild = axisDist <= 0 ? node.rightChild : node.leftChild;

            // 6. 优先搜索近子树
            if (nearChild != null)
            {
                RadiusSearch(nearChild, queryPoint, sqrRadius, results);
            }

            // 7. 检查是否需要搜索远子树
            if (farChild != null)
            {
                // 关键条件：分割平面距离的平方 <= 搜索半径的平方
                if (axisDist * axisDist <= sqrRadius)
                {
                    RadiusSearch(farChild, queryPoint, sqrRadius, results);
                }
            }
        }

        private void RangeQueryRecursive(Node node,
                                         float minX, float maxX,
                                         float minY, float maxY,
                                         float minZ, float maxZ,
                                         List<Vertex> results)
        {
            if (node == null) return;

            Vertex p = node.vertex;

            var x = p.position.x;
            var y = p.position.y;
            var z = p.position.z;

            // 检查当前点是否在范围内
            if (x >= minX && x <= maxX &&
                y >= minY && y <= maxY &&
                z >= minZ && z <= maxZ)
            {
                results.Add(p);
            }

            // 根据当前轴决定搜索顺序
            switch (node.splitAxis)
            {
                case Axis.X:
                    if (minX <= x)
                        RangeQueryRecursive(node.leftChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    if (maxX >= x)
                        RangeQueryRecursive(node.rightChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    break;

                case Axis.Y:
                    if (minY <= y)
                        RangeQueryRecursive(node.leftChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    if (maxY >= y)
                        RangeQueryRecursive(node.rightChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    break;

                case Axis.Z:
                    if (minZ <= z)
                        RangeQueryRecursive(node.leftChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    if (maxZ >= z)
                        RangeQueryRecursive(node.rightChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    break;
            }
        }

        public Vertex QueryNearest(Vector3 position)
        {
            if (root == null) return null;

            // 初始化最佳结果
            NearestResult best = new NearestResult
            {
                vertex = null,
                sqrDistance = float.MaxValue
            };
            NearestRecursive(root, position, ref best);
            return best.vertex;
        }

        struct NearestResult
        {
            public Vertex vertex;
            public float sqrDistance;
        }

        private void NearestRecursive(Node node, Vector3 position, ref NearestResult best)
        {
            if (node == null) return;

            Node p = node;

            float sqrDist = (p.vertex.position - position).sqrMagnitude;
            if (sqrDist < best.sqrDistance)
            {
                best.vertex = p.vertex;
                best.sqrDistance = sqrDist;
            }

            float axisDist = GetAxisDistance(position, node.vertex.position, node.splitAxis);

            Node nearChild = axisDist <= 0 ? node.leftChild : node.rightChild;
            Node farChild = axisDist <= 0 ? node.rightChild : node.leftChild;

            if (nearChild != null)
            {
                NearestRecursive(nearChild, position, ref best);
            }

            // 6. 回溯检查：是否需要搜索远分支
            if (farChild != null)
            {
                // 关键回溯条件：分割平面距离 < 当前最佳距离
                if (axisDist * axisDist < best.sqrDistance)
                {
                    NearestRecursive(farChild, position, ref best);
                }
            }
        }

        private float GetAxisDistance(Vector3 query, Vector3 nodePos, Axis axis)
        {
            switch (axis)
            {
                case Axis.X: return query.x - nodePos.x;
                case Axis.Y: return query.y - nodePos.y;
                case Axis.Z: return query.z - nodePos.z;
                default: return 0;
            }
        }
    }
}
