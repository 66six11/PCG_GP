using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Job
{
    public enum FlipAxis
    {
        X,
        Y,
        Z
    }

    public class MeshModifyJob
    {
     
       
        public struct MeshFlipJob : IJobParallelFor
        {
            public FlipAxis axis;
            public NativeArray<Vector3> vertices;
            public NativeArray<Vector3> normals;
            public NativeArray<Vector4> tangents;

            public void Execute(int index)
            {
                // 翻转顶点
                Vector3 vertex = vertices[index];
                switch (axis)
                {
                    case FlipAxis.X:
                        vertex.x = -vertex.x;
                        break;
                    case FlipAxis.Y:
                        vertex.y = -vertex.y;
                        break;
                    case FlipAxis.Z:
                        vertex.z = -vertex.z;
                        break;
                }

                vertices[index] = vertex;

                // 翻转法线
                if (normals.IsCreated && index < normals.Length)
                {
                    Vector3 normal = normals[index];
                    switch (axis)
                    {
                        case FlipAxis.X:
                            normal.x = -normal.x;
                            break;
                        case FlipAxis.Y:
                            normal.y = -normal.y;
                            break;
                        case FlipAxis.Z:
                            normal.z = -normal.z;
                            break;
                    }

                    normals[index] = normal;
                }

                // 翻转切线
                if (tangents.IsCreated && index < tangents.Length)
                {
                    Vector4 tangent = tangents[index];
                    switch (axis)
                    {
                        case FlipAxis.X:
                            tangent.x = -tangent.x;
                            break;
                        case FlipAxis.Y:
                            tangent.y = -tangent.y;
                            break;
                        case FlipAxis.Z:
                            tangent.z = -tangent.z;
                            break;
                    }

                    tangents[index] = tangent;
                }
            }
        }
        [BurstCompile]
        public static Mesh FlipMeshWithNativeArray(Mesh mesh, FlipAxis axis)
        {
            if (mesh == null) return null;

            // 获取网格数据
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            int vertexCount = vertices.Length;

            // 创建 NativeArray
            var nativeVertices = new NativeArray<Vector3>(vertexCount, Allocator.TempJob);
            var nativeNormals = normals != null && normals.Length == vertexCount
                ? new NativeArray<Vector3>(vertexCount, Allocator.TempJob)
                : new NativeArray<Vector3>(0, Allocator.TempJob);
            var nativeTangents = tangents != null && tangents.Length == vertexCount
                ? new NativeArray<Vector4>(vertexCount, Allocator.TempJob)
                : new NativeArray<Vector4>(0, Allocator.TempJob);

            // 复制数据到 NativeArray
            nativeVertices.CopyFrom(vertices);
            if (nativeNormals.Length > 0) nativeNormals.CopyFrom(normals);
            if (nativeTangents.Length > 0) nativeTangents.CopyFrom(tangents);

            // 创建并调度 Job
            var job = new MeshFlipJob
            {
                axis = axis,
                vertices = nativeVertices,
                normals = nativeNormals,
                tangents = nativeTangents
            };

            JobHandle handle = job.Schedule(vertexCount, 64);
            handle.Complete();

            // 创建新网格
            Mesh result = new Mesh();

            // 应用修改后的数据
            result.vertices = nativeVertices.ToArray();
            if (nativeNormals.Length > 0) result.normals = nativeNormals.ToArray();
            if (nativeTangents.Length > 0) result.tangents = nativeTangents.ToArray();

            // 复制其他数据
            result.uv = mesh.uv;
            result.uv2 = mesh.uv2;
            result.uv3 = mesh.uv3;
            result.uv4 = mesh.uv4;
            result.uv5 = mesh.uv5;
            result.uv6 = mesh.uv6;
            result.uv7 = mesh.uv7;
            result.uv8 = mesh.uv8;
            result.colors = mesh.colors;
            result.colors32 = mesh.colors32;
            result.bindposes = mesh.bindposes;
            result.boneWeights = mesh.boneWeights;
            result.subMeshCount = mesh.subMeshCount;

            // 复制三角形数据
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                result.SetTriangles(mesh.GetTriangles(i), i);
            }

            // 释放 NativeArray
            nativeVertices.Dispose();
            nativeNormals.Dispose();
            nativeTangents.Dispose();

            // 反转三角面顺序
            ReversalTriangles(result);

            // 重新计算包围盒
            result.RecalculateBounds();

            return result;
        }

       
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