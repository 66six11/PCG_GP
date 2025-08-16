using UnityEngine;
using Job;

namespace Utility
{
    public static class MeshHelper
    {
        public static Mesh FlipXMesh(Mesh mesh) => MeshModifyJob.FlipMeshWithJob(mesh, FlipAxis.X);
        public static Mesh FlipYMesh(Mesh mesh) => MeshModifyJob.FlipMeshWithJob(mesh, FlipAxis.Y);
        public static Mesh FlipZMesh(Mesh mesh) => MeshModifyJob.FlipMeshWithJob(mesh, FlipAxis.Z);

        public static Mesh TransformMesh(this Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float height) => MeshModifyJob.TransformMeshJob(mesh, a, b, c, d, height);

        //反转三角面
        // 反转三角面顺序（改变法线方向）
        public static void ReversalTriangles(Mesh mesh)
        {
            if (mesh == null) return;

            // 1. 获取所有子网格的三角形数据
            int subMeshCount = mesh.subMeshCount;
            int[][] triangles = new int[subMeshCount][];

            for (int i = 0; i < subMeshCount; i++)
            {
                triangles[i] = mesh.GetTriangles(i);
            }

            // 2. 反转每个子网格的三角形顺序
            for (int i = 0; i < subMeshCount; i++)
            {
                int[] subTriangles = triangles[i];

                // 反转每个三角形的顶点顺序
                for (int j = 0; j < subTriangles.Length; j += 3)
                {
                    // 交换第一个和第三个顶点
                    (subTriangles[j], subTriangles[j + 2]) = (subTriangles[j + 2], subTriangles[j]);
                }

                // 设置修改后的三角形数据
                mesh.SetTriangles(subTriangles, i);
            }

            // 3. 重新计算法线（可选）
            mesh.RecalculateNormals();
        }
    }
}