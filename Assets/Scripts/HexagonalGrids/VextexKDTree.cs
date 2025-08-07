using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HexagonalGrids
{
    public enum Axis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    public class VextexKDTree
    {
        public Node root;
        public int totalNodes;
        public int maxDepth;

        public VextexKDTree(List<Vertex> vertices)
        {
            root = BuildTree(vertices, 0);
            totalNodes = vertices.Count;
        }

        public Node BuildTree(List<Vertex> vertices, int depth)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return null;
            }

            maxDepth = depth;
            Axis axis = (Axis)(depth % 3);

            List<Vertex> sortedPoints = Sort(vertices, axis);

            int medianIndex = sortedPoints.Count / 2;
            Vertex median = sortedPoints[medianIndex];

            Node node = new Node
            {
                vertex = median,
                splitAxis = axis
            };

            // 递归构建子树（跳过中位数）
            var leftVertices = sortedPoints.GetRange(0, medianIndex);
            var rightVertices = sortedPoints.GetRange(medianIndex + 1, sortedPoints.Count - medianIndex - 1);
            node.leftChild = BuildTree(leftVertices, depth + 1);
            node.rightChild = BuildTree(rightVertices, depth + 1);

            return node;
        }

        private List<Vertex> Sort(List<Vertex> vertices, Axis axis)
        {
            switch (axis)
            {
                case Axis.X:
                    vertices.Sort((v1, v2) => v1.position.x.CompareTo(v2.position.x));
                    break;
                case Axis.Y:
                    vertices.Sort((v1, v2) => v1.position.y.CompareTo(v2.position.y));
                    break;
                case Axis.Z:
                    vertices.Sort((v1, v2) => v1.position.z.CompareTo(v2.position.z));
                    break;
            }

            return vertices;
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


            float GetAxisValue(Vector3 position, Axis p1)
            {
                switch (axis)
                {
                    case Axis.X: return position.x;
                    case Axis.Y: return position.y;
                    default: return position.z;
                }
            }

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
            RadiusSearch(root, center, sqrRadius, 0, result);
            return result;
        }

        private void RadiusSearch(Node node, Vector3 queryPoint, float sqrRadius, int depth, List<Vertex> results)
        {
            if (node == null) return;
            Vector3 diff = node.vertex.position - queryPoint;
            float sqrDistance = diff.sqrMagnitude;

            // 检查当前点是否在范围内
            if (sqrDistance <= sqrRadius)
            {
                results.Add(node.vertex);
            }

            Axis axis = (Axis)(depth % 3);
            float axisDistance = 0f;

            // 计算查询点在当前分割轴上的位置差异
            if (axis == Axis.X)
                axisDistance = queryPoint.x - node.vertex.position.x;
            else if (axis == Axis.Y)
                axisDistance = queryPoint.y - node.vertex.position.y;
            else
                axisDistance = queryPoint.z - node.vertex.position.z;

            Node nearChild = axisDistance <= 0 ? node.leftChild : node.rightChild;
            Node farChild = axisDistance <= 0 ? node.rightChild : node.leftChild;

            // 先搜索近子树
            if (nearChild != null)
            {
                RadiusSearch(nearChild, queryPoint, sqrRadius, depth + 1, results);
            }

            // 如果分割面与查询点的距离小于半径，搜索远子树
            // 因为远子树可能有符合条件的点（球面可能与分割面相交）
            if (farChild != null && (axisDistance * axisDistance) <= sqrRadius)
            {
                RadiusSearch(farChild, queryPoint, sqrRadius, depth + 1, results);
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
                    if (minZ <= y)
                        RangeQueryRecursive(node.leftChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    if (maxZ >= y)
                        RangeQueryRecursive(node.rightChild, minX, maxX, minY, maxY, minZ, maxZ, results);
                    break;
            }
        }

        public Vertex QueryNearest(Vector3 position)
        {
            return NearestRecursive(root, position, float.MaxValue);
        }

        private Vertex NearestRecursive(Node node, Vector3 position, float maxValue)
        {
            if (node == null) return null;

            Vertex p = node.vertex;

            float dist = (p.position - position).sqrMagnitude;
            if (dist < maxValue)
            {
                maxValue = dist;
            }

            // 根据当前轴决定搜索顺序
            switch (node.splitAxis)
            {
                case Axis.X:
                    if (position.x - p.position.x < 0)
                    {
                        Vertex leftNearest = NearestRecursive(node.leftChild, position, maxValue);
                        if (leftNearest != null)
                        {
                            return leftNearest;
                        }
                    }
                    else
                    {
                        Vertex rightNearest = NearestRecursive(node.rightChild, position, maxValue);
                        if (rightNearest != null)
                        {
                            return rightNearest;
                        }
                    }

                    break;

                case Axis.Y:
                    if (position.y - p.position.y < 0)
                    {
                        Vertex leftNearest = NearestRecursive(node.leftChild, position, maxValue);
                        if (leftNearest != null)
                        {
                            return leftNearest;
                        }
                    }
                    else
                    {
                        Vertex rightNearest = NearestRecursive(node.rightChild, position, maxValue);
                        if (rightNearest != null)
                        {
                            return rightNearest;
                        }
                    }

                    break;

                case Axis.Z:
                    if (position.z - p.position.z < 0)
                    {
                        Vertex leftNearest = NearestRecursive(node.leftChild, position, maxValue);
                        if (leftNearest != null)
                        {
                            return leftNearest;
                        }
                    }
                    else
                    {
                        Vertex rightNearest = NearestRecursive(node.rightChild, position, maxValue);
                        if (rightNearest != null)
                        {
                            return rightNearest;
                        }
                    }

                    break;
            }

            return p;
        }

        public string Print()
        {
            if (root == null)
                return "KD-Tree is empty";

            StringBuilder result = new StringBuilder();
            Queue<(Node node, int depth, string prefix, bool isLeft)> queue = new Queue<(Node, int, string, bool)>();
            queue.Enqueue((root, 0, "", true));

            // 使用符号表示不同方向的子树
            const string Horizontal = "─ ";
            const string Corner = "└─";
            const string Vertical = "│ ";

            while (queue.Count > 0)
            {
                (Node current, int depth, string prefix, bool isLeft) = queue.Dequeue();

                // 打印当前节点信息
                string positionStr = $"[{current.vertex.position.x:F2},{current.vertex.position.y:F2},{current.vertex.position.z:F2}]";
                result.Append(prefix);
                result.Append(isLeft ? Corner : "┌─");
                result.Append($"{Horizontal}{positionStr} (Axis: {current.splitAxis}, Depth: {depth})");
                result.AppendLine();

                // 准备子节点前缀
                string newPrefix = prefix + (isLeft ? "  " : Vertical + " ");

                // 按顺序添加右子树（先右后左，因为打印是从上到下）
                if (current.rightChild != null)
                {
                    queue.Enqueue((current.rightChild, depth + 1, newPrefix + " ", false));
                }

                if (current.leftChild != null)
                {
                    queue.Enqueue((current.leftChild, depth + 1, newPrefix + " ", true));
                }
            }

            return result.ToString();
        }
    }

    public class Node
    {
        public Vertex vertex;
        public Node leftChild;
        public Node rightChild;
        public Axis splitAxis;

        // public Node(Vertex vertex, Axis splitAxis)
        // {
        //     this.vertex = vertex;
        //     this.splitAxis = splitAxis; 
        // }   
    }
}