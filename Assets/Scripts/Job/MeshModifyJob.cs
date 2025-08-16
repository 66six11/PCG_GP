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

    [BurstCompile]
    public static class MeshModifyJob
    {
        private struct MeshFlipJob : IJobParallelFor
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

        /// <summary>
        ///  多线程翻转网格数据并返回新网格
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static Mesh FlipMeshWithJob(Mesh mesh, FlipAxis axis)
        {
            if (mesh == null) return null;
            Mesh result = new Mesh()
            {
                vertices = mesh.vertices,
                normals = mesh.normals,
                tangents = mesh.tangents,
                colors = mesh.colors,
                uv = mesh.uv,
                uv2 = mesh.uv2,
                uv3 = mesh.uv3,
                uv4 = mesh.uv4,
                uv5 = mesh.uv5,
                uv6 = mesh.uv6,
                uv7 = mesh.uv7,
                uv8 = mesh.uv8,
                subMeshCount = mesh.subMeshCount,
            };

            // 获取网格数据
            Vector3[] vertices = result.vertices;
            Vector3[] normals = result.normals;
            Vector4[] tangents = result.tangents;
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

            // 应用修改后的数据
            result.vertices = nativeVertices.ToArray();
            if (nativeNormals.Length > 0) result.normals = nativeNormals.ToArray();
            if (nativeTangents.Length > 0) result.tangents = nativeTangents.ToArray();


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

        private static void ReversalTriangles(Mesh mesh)
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
        }


        public struct BilinearConfig
        {
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;
            public Vector3 d;
            public float height;
            public Vector3 minBounds { get; set; }
            public Vector3 maxBounds { get; set; }
        }


        private struct MeshBilinearTransformJob : IJobParallelFor
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<Vector3> Vertices;

            [ReadOnly] public NativeReference<BilinearConfig> config;

            public void Execute(int index)
            {
                Vector3 vertex = Vertices[index];


                BilinearConfig cfg = config.Value;

                float u = Mathf.InverseLerp(cfg.minBounds.x, cfg.maxBounds.x, vertex.x),
                    w = Mathf.InverseLerp(cfg.minBounds.z, cfg.maxBounds.z, vertex.z);

                Vector3 x1 = Vector3.Lerp(cfg.a, cfg.b, u);
                Vector3 x2 = Vector3.Lerp(cfg.d, cfg.c, u);

                Vector3 posX = Vector3.Lerp(x2, x1, w);


                posX.y = vertex.y * cfg.height;

                Vertices[index] = posX;
            }
        }

        // 执行双线性变换的静态方法（优化版）
        public static Mesh TransformMeshJob(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float height)
        {
            // 创建新网格（避免修改原始网格）
            Mesh result = new Mesh();

            // 直接复制所有基础数据（避免深度序列化问题）
            result.vertices = mesh.vertices;
            result.triangles = mesh.triangles;
            result.bounds = mesh.bounds;

            // 选择性复制UV通道（避免复制未使用的通道）
            if (mesh.uv != null && mesh.uv.Length > 0) result.uv = mesh.uv;
            if (mesh.uv2 != null && mesh.uv2.Length > 0) result.uv2 = mesh.uv2;
            if (mesh.uv3 != null && mesh.uv3.Length > 0) result.uv3 = mesh.uv3;
            if (mesh.uv4 != null && mesh.uv4.Length > 0) result.uv4 = mesh.uv4;
            if (mesh.uv5 != null && mesh.uv5.Length > 0) result.uv5 = mesh.uv5;
            if (mesh.uv6 != null && mesh.uv6.Length > 0) result.uv6 = mesh.uv6;
            if (mesh.uv7 != null && mesh.uv7.Length > 0) result.uv7 = mesh.uv7;
            if (mesh.uv8 != null && mesh.uv8.Length > 0) result.uv8 = mesh.uv8;

            // 复制颜色（如果存在）
            if (mesh.colors != null && mesh.colors.Length > 0) result.colors = mesh.colors;

            // 不复制法线和切线（后面会重新计算）
            int vertexCount = result.vertexCount;

            // 使用NativeArray直接操作顶点数据
            NativeArray<Vector3> verticesArray = new NativeArray<Vector3>(result.vertices, Allocator.TempJob);

            // 创建配置引用
            var configRef = new NativeReference<BilinearConfig>(Allocator.TempJob);
            configRef.Value = new BilinearConfig
            {
                a = a,
                b = b,
                c = c,
                d = d,
                height = height,
                // 计算包围盒

                minBounds = mesh.bounds.min,
                maxBounds = mesh.bounds.max
            };
          
            // 设置并执行Job
            var job = new MeshBilinearTransformJob
            {
                Vertices = verticesArray,
                config = configRef
            };

            // 调度并行Job
            JobHandle handle = job.Schedule(vertexCount, 64);
            handle.Complete();

            // 应用顶点变换
            result.SetVertices(verticesArray);

            // 释放Native资源
            verticesArray.Dispose();
            configRef.Dispose();

            // 重新计算网格属性
            result.RecalculateBounds();
            result.RecalculateNormals();
            result.RecalculateTangents();

            return result;
        }
    }
}