using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public class Cell
    {
        //从上到下顺时针的八个个顶点
        public Vertex V1;
        public Vertex V2;
        public Vertex V3;
        public Vertex V4;
        public Vertex V5;
        public Vertex V6;
        public Vertex V7;
        public Vertex V8;

        public Vertex[] vertices;

        //12个边
        public SubEdge E1;
        public SubEdge E2;
        public SubEdge E3;
        public SubEdge E4;
        public SubEdge E5;
        public SubEdge E6;
        public SubEdge E7;
        public SubEdge E8;
        public SubEdge E9;
        public SubEdge E10;
        public SubEdge E11;
        public SubEdge E12;

        public SubEdge[] edges;

        public List<Cell> neighbours = new List<Cell>();


        public SubQuad Q1;
        public SubQuad Q2;


        //中心点
        public Vector3 Center;

        public Matrix4x4 transformMatrix;
        public Vector3 translation;
        public Quaternion rotation;
        public Vector3 scale;

        public Cell(SubQuad upQuad, SubQuad downQuad, List<SubEdge> edges)
        {
            Q1 = upQuad;
            Q2 = downQuad;

            V1 = upQuad.a;
            V2 = upQuad.b;
            V3 = upQuad.c;
            V4 = upQuad.d;

            V5 = downQuad.a;
            V6 = downQuad.b;
            V7 = downQuad.c;
            V8 = downQuad.d;


            vertices = new Vertex[] { V1, V2, V3, V4, V5, V6, V7, V8 };

            E1 = upQuad.edges[0];
            E2 = upQuad.edges[1];
            E3 = upQuad.edges[2];
            E4 = upQuad.edges[3];

            E5 = downQuad.edges[0];
            E6 = downQuad.edges[1];
            E7 = downQuad.edges[2];
            E8 = downQuad.edges[3];

            E9 = SubEdge.GenerateSubEdge(V1, V5, edges);
            E10 = SubEdge.GenerateSubEdge(V2, V6, edges);
            E11 = SubEdge.GenerateSubEdge(V3, V7, edges);
            E12 = SubEdge.GenerateSubEdge(V4, V8, edges);

            this.edges = new SubEdge[] { E1, E2, E3, E4, E5, E6, E7, E8, E9, E10, E11, E12 };


            Center = (V1.position + V2.position + V3.position + V4.position + V5.position + V6.position + V7.position + V8.position) / 8;
        }

        public bool Contains(Vertex vertex)
        {
            return (vertex == V1 || vertex == V2 || vertex == V3 || vertex == V4 || vertex == V5 || vertex == V6 || vertex == V7 || vertex == V8);
        }

        public bool Contains(SubEdge edge)
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

            // 3. 提取旋转分量（创建归一化的旋转矩阵）
            Matrix4x4 rotationMatrix = new Matrix4x4();

            // 归一化各轴向量
            Vector3 xAxis = transformMatrix.GetColumn(0) / scale.x;
            Vector3 yAxis = transformMatrix.GetColumn(1) / scale.y;
            Vector3 zAxis = transformMatrix.GetColumn(2) / scale.z;

            // 设置旋转矩阵
            rotationMatrix.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0));
            rotationMatrix.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0));
            rotationMatrix.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0));
            rotationMatrix.SetColumn(3, new Vector4(0, 0, 0, 1));

            // 将旋转矩阵转换为四元数
             rotation = rotationMatrix.rotation;

            
            // translation：位置偏移（Vector3）
            // rotation：旋转（Quaternion）
            // scale：缩放（Vector3）
        }
    }
}