using System;
using UnityEngine;
using NUnit.Framework;
using System.Collections.Generic;
using HexagonalGrids;

namespace Tests
{
    [TestFixture]
    [TestOf(typeof(Cell))]
    public class CellTransformMatrixTests
    {
        // 共享边集合
        List<SubEdge> sharedEdges = new List<SubEdge>();

        // 测试用例1: 规则立方体顶点
        private static Vector3[] cubeVertices = new Vector3[]
        {
            new Vector3(0, 1, 1), // V1
            new Vector3(1, 1, 1), // V2
            new Vector3(1, 1, 0), // V3
            new Vector3(0, 1, 0), // V4
            new Vector3(0, 0, 1), // V5
            new Vector3(1, 0, 1), // V6
            new Vector3(1, 0, 0), // V7
            new Vector3(0, 0, 0)  // V8
        };

        // 测试用例2: 上下对称六面体顶点
        private static Vector3[] asymmetricVertices = new Vector3[]
        {
            new Vector3(0, 2, 2),
            new Vector3(3, 2, 2),
            new Vector3(3, 2, 0),
            new Vector3(0, 2, 0),
            new Vector3(0, 0, 2),
            new Vector3(3, 0, 2),
            new Vector3(3, 0, 0),
            new Vector3(0, 0, 0),
        };

        // 测试用例3: 倾斜六面体顶点
        private static Vector3[] skewedVertices = new Vector3[]
        {
            new Vector3(0, 1, 1),
            new Vector3(1, 1.2f, 1),
            new Vector3(1.1f, 1.1f, 0),
            new Vector3(0.1f, 0.9f, 0),
            new Vector3(-0.1f, 0, 1.1f),
            new Vector3(1.1f, 0.1f, 0.9f),
            new Vector3(1.2f, -0.1f, -0.1f),
            new Vector3(0, 0, 0)
        };

        [Test]
        public void BuildTransformMatrix_OnCube_CreatesOrthonormalBasis()
        {
            // 构建规则立方体Cell
            Cell cell = CreateCellWithVertices(cubeVertices);
            cell.BuildTransformMatrix();

            // 提取矩阵的轴向量
            Vector3 right = cell.transformMatrix.GetColumn(0);
            Vector3 up = cell.transformMatrix.GetColumn(1);
            Vector3 forward = cell.transformMatrix.GetColumn(2);

            // 验证正交性
            Assert.IsTrue(Vector3.Dot(right, up) < 1e-5f, "右轴与上轴不垂直");
            Assert.IsTrue(Vector3.Dot(up, forward) < 1e-5f, "上轴与前轴不垂直");
            Assert.IsTrue(Vector3.Dot(forward, right) < 1e-5f, "前轴与右轴不垂直");

            // 验证单位化
            Assert.AreEqual(1f, right.magnitude, 1e-5f, "右轴未单位化");
            Assert.AreEqual(1f, up.magnitude, 1e-5f, "上轴未单位化");
            Assert.AreEqual(1f, forward.magnitude, 1e-5f, "前轴未单位化");

            // 验证方向
            Assert.AreEqual(Vector3.right, right, "右轴方向错误");
            Assert.AreEqual(Vector3.up, up, "上轴方向错误");
            Assert.AreEqual(Vector3.forward, forward, "前轴方向错误");
        }

        [Test]
        public void BuildTransformMatrix_OnAsymmetricShape_CorrectForwardDirection()
        {
            Cell cell = CreateCellWithVertices(asymmetricVertices);
            cell.BuildTransformMatrix();

            // 计算期望的前方向
            Vector3 frontCenter = (cell.V1.position + cell.V2.position + cell.V5.position + cell.V6.position) / 4f;
            Vector3 expectedForward = (frontCenter - cell.Center).normalized;

            Vector3 actualForward = cell.transformMatrix.GetColumn(2);

            // 验证前方向计算正确
            Assert.AreEqual(expectedForward.x, actualForward.x, 1e-5f, "前方向X错误");
            Assert.AreEqual(expectedForward.y, actualForward.y, 1e-5f, "前方向Y错误");
            Assert.AreEqual(expectedForward.z, actualForward.z, 1e-5f, "前方向Z错误");
        }

        [Test]
        public void BuildTransformMatrix_OnAsymmetricShape_CorrectUpDirection()
        {
            Cell cell = CreateCellWithVertices(asymmetricVertices);
            cell.BuildTransformMatrix();

            // 计算期望的上方向
            Vector3 topCenter = (cell.V1.position + cell.V2.position + cell.V3.position + cell.V4.position) / 4f;
            Vector3 bottomCenter = (cell.V5.position + cell.V6.position + cell.V7.position + cell.V8.position) / 4f;
            Vector3 expectedUp = (topCenter - bottomCenter).normalized;

            Vector3 actualUp = cell.transformMatrix.GetColumn(1);

            // 验证上方向计算正确
            Assert.AreEqual(expectedUp.x, actualUp.x, 1e-5f, "上方向X错误");
            Assert.AreEqual(expectedUp.y, actualUp.y, 1e-5f, "上方向Y错误");
            Assert.AreEqual(expectedUp.z, actualUp.z, 1e-5f, "上方向Z错误");
        }

        [Test]
        public void BuildTransformMatrix_CenterPositionMatchesCalculatedCenter()
        {
            Cell cell = CreateCellWithVertices(asymmetricVertices);
            cell.BuildTransformMatrix();

            Vector3 matrixCenter = cell.transformMatrix.inverse.GetColumn(3);
            Vector3 expectedCenter = CalculateExpectedCenter(asymmetricVertices);

            // 验证中心点精度
            Assert.AreEqual(expectedCenter.x, matrixCenter.x, 1e-5f, "中心点X坐标错误");
            Assert.AreEqual(expectedCenter.y, matrixCenter.y, 1e-5f, "中心点Y坐标错误");
            Assert.AreEqual(expectedCenter.z, matrixCenter.z, 1e-5f, "中心点Z坐标错误");
        }

        [Test]
        public void BuildTransformMatrix_OnSkewedShape_OrthonormalBasis()
        {
            Cell cell = CreateCellWithVertices(skewedVertices);
            cell.BuildTransformMatrix();

            // 提取矩阵的轴向量
            Vector3 right = cell.transformMatrix.GetColumn(0);
            Vector3 up = cell.transformMatrix.GetColumn(1);
            Vector3 forward = cell.transformMatrix.GetColumn(2);

            // 验证正交性
            Assert.IsTrue(Vector3.Dot(right, up) < 1e-5f, "右轴与上轴不垂直");
            Assert.IsTrue(Vector3.Dot(up, forward) < 1e-5f, "上轴与前轴不垂直");
            Assert.IsTrue(Vector3.Dot(forward, right) < 1e-5f, "前轴与右轴不垂直");

            // 验证单位化
            Assert.AreEqual(1f, right.magnitude, 1e-5f, "右轴未单位化");
            Assert.AreEqual(1f, up.magnitude, 1e-5f, "上轴未单位化");
            Assert.AreEqual(1f, forward.magnitude, 1e-5f, "前轴未单位化");
        }

        [Test]
        public void TransformMatrix_WorldToLocalTransformConsistency()
        {
            Cell cell = CreateCellWithVertices(asymmetricVertices);
            cell.BuildTransformMatrix();

            // 测试点：单元格中心
            TestTransformConsistency(cell, cell.Center);

            // 测试点：顶点
            TestTransformConsistency(cell, cell.V1.position);
            TestTransformConsistency(cell, cell.V5.position);

            // 测试点：内部点
            Vector3 internalPoint = (cell.V1.position + cell.V3.position + cell.V5.position + cell.V7.position) / 4f;
            TestTransformConsistency(cell, internalPoint);
        }

        //测试从局部坐标到世界坐标的转换是否正确
        [Test]
        public void TransformMatrix_LocalToWorldTransformConsistency()
        {
            Cell cell = CreateCellWithVertices(asymmetricVertices);
            cell.BuildTransformMatrix();

            Vector3 localPoint = new Vector3(0, 0, 0);
            Vector3 worldPoint = cell.Center;
            Vector3 expectedWorldPoint = cell.transformMatrix.inverse.MultiplyPoint(localPoint);


            // 验证转换精度
            Assert.AreEqual(expectedWorldPoint.x, worldPoint.x, 1e-5f, "X坐标转换错误");
            Assert.AreEqual(expectedWorldPoint.y, worldPoint.y, 1e-5f, "Y坐标转换错误");
            Assert.AreEqual(expectedWorldPoint.z, worldPoint.z, 1e-5f, "Z坐标转换错误");
        }

        [Test]
        public void TestGetCellByte()
        {
            Cell cell = CreateCellWithVertices(asymmetricVertices);

            //随机设置任意几个顶点为启用状态
            cell.V1.IsEnabled = true;
            cell.V2.IsEnabled = true;
            cell.V5.IsEnabled = true;
            cell.V6.IsEnabled = true;


            //字节顺序是从右到左
            byte expectByte = 0b00110011;
            byte cellByte = cell.GetCellByte();

            Assert.AreEqual(expectByte, cellByte, 1e-5f, $"顶点状态错误,期望值{ByteToBinaryString1(expectByte)},实际值{ByteToBinaryString1(cellByte)}");
        }

        //验证旋转 缩放 平移
        [Test]
        public void TestTransform()
        {
            Cell cell = CreateCellWithVertices(asymmetricVertices);
            cell.BuildTransformMatrix();

            // 获取局部到世界的变换矩阵（逆矩阵）
            Matrix4x4 localToWorld = cell.transformMatrix.inverse;

            // 测试X轴方向向量
            Vector3 localX = new Vector3(1, 0, 0);

            // 1. 先缩放
            Vector3 scaledX = new Vector3(
                localX.x * cell.scale.x,
                localX.y * cell.scale.y,
                localX.z * cell.scale.z
            );

            // 2. 再旋转
            Vector3 rotatedScaledX = cell.rotation * scaledX;

            // 3. 对于方向向量，不应该应用平移！
            // 获取矩阵的X轴列（方向向量）
            Vector4 matrixX = localToWorld.GetColumn(0);
            Vector3 matrixXVec = new Vector3(matrixX.x, matrixX.y, matrixX.z);

            // 比较结果（使用容差比较）
            Assert.IsTrue(Vector3.Distance(rotatedScaledX, matrixXVec) < 0.001f,
                "X轴方向不匹配");

            // 测试原点位置
            Vector3 localOrigin = Vector3.zero;

            // 1. 先缩放
            Vector3 scaledOrigin = new Vector3(
                localOrigin.x * cell.scale.x,
                localOrigin.y * cell.scale.y,
                localOrigin.z * cell.scale.z
            );

            // 2. 再旋转
            Vector3 rotatedScaledOrigin = cell.rotation * scaledOrigin;

            // 3. 最后平移
            Vector3 worldOrigin = rotatedScaledOrigin + cell.translation;

            // 获取矩阵的平移部分（最后一列）
            Vector4 matrixTranslation = localToWorld.GetColumn(3);
            Vector3 matrixTranslationVec = new Vector3(-matrixTranslation.x, -matrixTranslation.y, -matrixTranslation.z);

            // 比较结果
            Assert.IsTrue(Vector3.Distance(worldOrigin, matrixTranslationVec) < 0.001f,
                $"原点位置不匹配 {worldOrigin} vs {matrixTranslationVec}");
        }

        // 辅助方法：字节转二进制字符串
        private static string ByteToBinaryString1(byte value)
        {
            return Convert.ToString(value, 2).PadLeft(8, '0');
        }

        // 辅助方法：测试变换一致性
        private void TestTransformConsistency(Cell cell, Vector3 worldPoint)
        {
            // 世界坐标 -> 局部坐标
            Vector3 localPoint = cell.transformMatrix.MultiplyPoint(worldPoint);

            // 局部坐标 -> 世界坐标
            Matrix4x4 inverseMatrix = cell.transformMatrix.inverse;
            Vector3 restoredWorldPoint = inverseMatrix.MultiplyPoint(localPoint);

            // 验证还原精度
            Assert.AreEqual(worldPoint.x, restoredWorldPoint.x, 1e-5f, "X坐标还原错误");
            Assert.AreEqual(worldPoint.y, restoredWorldPoint.y, 1e-5f, "Y坐标还原错误");
            Assert.AreEqual(worldPoint.z, restoredWorldPoint.z, 1e-5f, "Z坐标还原错误");
        }

        // 辅助方法：根据顶点创建Cell实例
        private Cell CreateCellWithVertices(Vector3[] positions)
        {
            List<Vertex> vertices = new List<Vertex>();
            foreach (var pos in positions)
            {
                vertices.Add(new Vertex(pos));
            }

            // 创建上下两个四边形
            var upQuad = new SubQuad(vertices[0], vertices[1], vertices[2], vertices[3], sharedEdges);
            var downQuad = new SubQuad(vertices[4], vertices[5], vertices[6], vertices[7], sharedEdges);

            return new Cell(upQuad, downQuad, sharedEdges);
        }


        // 辅助方法：计算预期中心点
        private Vector3 CalculateExpectedCenter(Vector3[] verts)
        {
            Vector3 sum = Vector3.zero;
            foreach (var v in verts) sum += v;
            return sum / verts.Length;
        }
    }
}