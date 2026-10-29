using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    [Serializable]
    public class Cell
    {
        //从上到下顺时针的八个个顶点
        public Vertex V1 => vertices[0];
        public Vertex V2 => vertices[1];
        public Vertex V3 => vertices[2];
        public Vertex V4 => vertices[3];
        public Vertex V5 => vertices[4];
        public Vertex V6 => vertices[5];
        public Vertex V7 => vertices[6];
        public Vertex V8 => vertices[7];

        public Vector3 localV1 => localVertices[0];
        public Vector3 localV2 => localVertices[1];
        public Vector3 localV3 => localVertices[2];
        public Vector3 localV4 => localVertices[3];
        public Vector3 localV5 => localVertices[4];
        public Vector3 localV6 => localVertices[5];
        public Vector3 localV7 => localVertices[6];
        public Vector3 localV8 => localVertices[7];

        //8个顶点
        public readonly Vertex[] vertices;
        //8个本地顶点
        public readonly Vector3[] localVertices = new Vector3[8];


        //12个边
        public Edge E1 => this.edges[0];
        public Edge E2 => this.edges[1];
        public Edge E3 => this.edges[2];
        public Edge E4 => this.edges[3];
        public Edge E5 => this.edges[4];
        public Edge E6 => this.edges[5];
        public Edge E7 => this.edges[6];
        public Edge E8 => this.edges[7];
        public Edge E9 => this.edges[8];
        public Edge E10 => this.edges[9];
        public Edge E11 => this.edges[10];
        public Edge E12 => this.edges[11];

        public readonly Edge[] edges;

        [System.NonSerialized]
        public readonly List<Cell> neighbours = new List<Cell>(FaceCount);

        public Quad Q1 { get; }
        public Quad Q2 { get; }

        //中心点
        public Vector3 Center;

        public Matrix4x4 transformMatrix;
        public Vector3 translation;
        public Quaternion rotation;
        public Vector3 scale;

        public Cell(Quad upQuad, Quad downQuad, EdgeCollection edges)
        {
            Q1 = upQuad;
            Q2 = downQuad;

            vertices = new[] { upQuad.a, upQuad.b, upQuad.c, upQuad.d,
                downQuad.a, downQuad.b, downQuad.c, downQuad.d };
            this.edges = new[] { upQuad.ab, upQuad.bc, upQuad.cd, upQuad.da,
                downQuad.ab, downQuad.bc, downQuad.cd, downQuad.da,
                edges.GetOrAdd(V1, V5), edges.GetOrAdd(V2, V6),
                edges.GetOrAdd(V3, V7), edges.GetOrAdd(V4, V8) };

            BuildTransformMatrix();
        }

        public bool Contains(Vertex vertex)
        {
            return (vertex == V1 || vertex == V2 || vertex == V3 || vertex == V4 || vertex == V5 || vertex == V6 || vertex == V7 || vertex == V8);
        }

        public bool Contains(Edge edge)
        {
            return (edge == E1 || edge == E2 || edge == E3 || edge == E4 || edge == E5 || edge == E6 || edge == E7 || edge == E8 || edge == E9 || edge == E10 || edge == E11 || edge == E12);
        }

        public byte GetCellByte()
        {
            if (vertices == null || vertices.Length != 8)
                throw new InvalidOperationException("顶点数组必须包含8个元素");
            byte state = 0; // 00000000

            for (var i = 0; i < 8; i++)
            {
                if (vertices[i].IsEnabled)
                {
                    state |= (byte)(1 << i); // 反转位位置
                }
            }

            return state;
        }

        public void BuildTransformMatrix()
        {
            Center = (Q1.center + Q2.center) * 0.5f;
            // 计算基向量
            // 前方向：前面四个点的中心减去整个Cell的中心
            Vector3 frontCenter = (V1.position + V2.position + V5.position + V6.position) / 4f;
            Vector3 forward = (frontCenter - Center).normalized;

            // 上方向：上下表面中心点差值
            Vector3 topCenter = Q1.center;
            Vector3 bottomCenter = Q2.center;
            Vector3 up = (topCenter - bottomCenter).normalized;

            // 计算右方向（确保正交性）
            Vector3 right = Vector3.Cross(up, forward).normalized;

            // 重新正交化上方向（确保三个轴互相垂直）
            up = Vector3.Cross(forward, right).normalized;

            // 构建4x4变换矩阵
            Matrix4x4 matrix = new Matrix4x4();

            // 设置旋转部分（局部坐标轴在世界空间中的方向）
            matrix.SetColumn(0, new Vector4(right.x, right.y, right.z, 0));       // X轴
            matrix.SetColumn(1, new Vector4(up.x, up.y, up.z, 0));                // Y轴
            matrix.SetColumn(2, new Vector4(forward.x, forward.y, forward.z, 0)); // Z轴

            // 设置平移部分（原点位置）
            matrix.SetColumn(3, new Vector4(Center.x, Center.y, Center.z, 1));

            // 返回世界空间->局部空间的变换矩阵
            transformMatrix = matrix.inverse;

            SplitTransformMatrix();

            SetLocalVertices();
        }

        //拆分 旋转 缩放 平移
        private void SplitTransformMatrix()
        {
            // 1. 提取平移分量（直接取最后一列）
             translation = transformMatrix.GetColumn(3);

            // 2. 提取缩放分量（计算各轴向量长度）

            scale.x = transformMatrix.GetColumn(0).magnitude; // X轴缩放
            scale.y = transformMatrix.GetColumn(1).magnitude; // Y轴缩放
            scale.z = transformMatrix.GetColumn(2).magnitude; // Z轴缩放

            // 将旋转矩阵转换为四元数
             rotation = transformMatrix.rotation;
             //这里是从世界坐标系到本地坐标系的变换，所以需要取它的反向
             rotation = Quaternion.Inverse(rotation);

            // translation：位置偏移（Vector3）
            // rotation：旋转（Quaternion）
            // scale：缩放（Vector3）
        }

        private void SetLocalVertices()
        {
            for (int i = 0; i < vertices.Length; i++)
                localVertices[i] = transformMatrix.MultiplyPoint3x4(vertices[i].position);
        }

        public const int FaceCount = 6;

        public FaceKey GetFaceKey(int face)
        {
            switch (face)
            {
                case 0: return new FaceKey(V1, V2, V3, V4);
                case 1: return new FaceKey(V5, V6, V7, V8);
                case 2: return new FaceKey(V1, V2, V6, V5);
                case 3: return new FaceKey(V2, V3, V7, V6);
                case 4: return new FaceKey(V3, V4, V8, V7);
                case 5: return new FaceKey(V4, V1, V5, V8);
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }
    }

    /// <summary>Canonical four-vertex face identity; no temporary arrays or sets.</summary>
    public readonly struct FaceKey : IEquatable<FaceKey>
    {
        private readonly int a, b, c, d;
        public FaceKey(Vertex v1, Vertex v2, Vertex v3, Vertex v4)
        {
            int a = v1.id, b = v2.id, c = v3.id, d = v4.id;
            SortPair(ref a, ref b);
            SortPair(ref c, ref d);
            SortPair(ref a, ref c);
            SortPair(ref b, ref d);
            SortPair(ref b, ref c);
            this.a = a; this.b = b; this.c = c; this.d = d;
        }
        private static void SortPair(ref int a, ref int b)
        {
            if (a <= b) return;
            int value = a; a = b; b = value;
        }
        public bool Equals(FaceKey other) => a == other.a && b == other.b && c == other.c && d == other.d;
        public override bool Equals(object obj) => obj is FaceKey other && Equals(other);
        public override int GetHashCode() => unchecked(((a * 397 ^ b) * 397 ^ c) * 397 ^ d);
    }
}
