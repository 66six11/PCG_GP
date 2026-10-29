#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HexagonalGrids;
using UnityEngine;

namespace Editor.Archive
{
    /// <summary>Frozen original per-face relaxation. Historical comparisons only; excluded from Player builds.</summary>
    public static class LegacyQuadRelaxation
    {
        public static void Apply(HexGrid grid, int times, bool? preserveBoundary = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (times <= 0) return;
            grid.InvalidateDerivedGeometry();
            var fixedVertices = (preserveBoundary ?? grid.IsCoordinateLayout)
                ? new HashSet<Vertex>(grid.baseLayer.boundaryVertices) : null;
            for (int i = 0; i < times; i++)
                foreach (var quad in grid.baseLayer.subQuads) RelaxSubQuad(quad, fixedVertices, grid.cellSize);
        }

        //优化网格
        private static void RelaxSubQuad(Quad subQuad, HashSet<Vertex> fixedVertices, float cellSize)
        {
            Vector3 center2a = subQuad.a.position - subQuad.center;
            Vector3 center2b = subQuad.b.position - subQuad.center;
            Vector3 center2c = subQuad.c.position - subQuad.center;
            Vector3 center2d = subQuad.d.position - subQuad.center;

            Vector3 aDesired = DesiredPosition(center2a, center2b, center2c, center2d);
            Vector3 bDesired = DesiredPosition(center2b, center2c, center2d, center2a);
            Vector3 cDesired = DesiredPosition(center2c, center2d, center2a, center2b);
            Vector3 dDesired = DesiredPosition(center2d, center2a, center2b, center2c);

            if (fixedVertices == null || !fixedVertices.Contains(subQuad.a)) subQuad.a.position += (aDesired - subQuad.a.position) * 0.1f;
            if (fixedVertices == null || !fixedVertices.Contains(subQuad.b)) subQuad.b.position += (bDesired - subQuad.b.position) * 0.1f;
            if (fixedVertices == null || !fixedVertices.Contains(subQuad.c)) subQuad.c.position += (cDesired - subQuad.c.position) * 0.1f;
            if (fixedVertices == null || !fixedVertices.Contains(subQuad.d)) subQuad.d.position += (dDesired - subQuad.d.position) * 0.1f;

            //计算a的期望位置
            Vector3 DesiredPosition(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4)
            {
                Vector3 sum = Vector3.zero;
                //v2旋转270度
                Vector3 v2_ = YRotationVector(v2, 270);
                //v3旋转180度
                Vector3 v3_ = YRotationVector(v3, 180);
                //v4旋转90度
                Vector3 v4_ = YRotationVector(v4, 90);
                //计算四个顶点的加权平均值
                sum += v1;
                sum += v2_;
                sum += v3_;
                sum += v4_;
                sum /= 4;
                //向量长度设置为根号2/2的cellSize
                sum.Normalize();

                sum *= cellSize * 0.707f;


                return subQuad.center + sum;
            }

            //旋转向量
            Vector3 YRotationVector(Vector3 v, float angle)
            {
                //绕Y轴旋转
                Quaternion q = Quaternion.AngleAxis(angle, Vector3.up);
                return q * v;
            }
        }

    }
}
#endif
