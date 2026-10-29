using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace Job
{
    public enum FlipAxis { X, Y, Z }

    internal struct BilinearSample
    {
        public Vector4 weights;
        public float height;
    }

    /// <summary>Immutable, shared interpolation data. Its owner must dispose it.</summary>
    internal sealed class MeshDeformationTemplate : IDisposable
    {
        public Mesh Source { get; }
        public Quaternion Rotation { get; }
        public bool MirrorX { get; }
        public object SourceRevision { get; }
        public NativeArray<BilinearSample> Samples { get; private set; }
        public bool IsCreated => Samples.IsCreated;
        public bool HasUv { get; }

        public MeshDeformationTemplate(Mesh source, Quaternion rotation, object sourceRevision = null, bool mirrorX = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.isReadable) throw new ArgumentException("Deformation requires a readable source mesh.", nameof(source));
            Source = source;
            Rotation = rotation;
            MirrorX = mirrorX;
            SourceRevision = sourceRevision ?? source;
            HasUv = source.HasVertexAttribute(VertexAttribute.TexCoord0)
                && source.GetVertexAttributeDimension(VertexAttribute.TexCoord0) >= 2;
            var vertices = source.vertices;
            var samples = new NativeArray<BilinearSample>(vertices.Length, Allocator.Persistent);
            try
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    // Orient the template before warping it into an irregular cell.
                    var vertex = vertices[i];
                    if (mirrorX) vertex.x = -vertex.x;
                    vertex = rotation * vertex;
                    float u = Mathf.Clamp01(vertex.x + 0.5f);
                    float w = Mathf.Clamp01(vertex.z + 0.5f);
                    samples[i] = new BilinearSample
                    {
                        weights = new Vector4((1 - u) * w, u * w, u * (1 - w), (1 - u) * (1 - w)),
                        height = vertex.y
                    };
                }
                Samples = samples;
            }
            catch { samples.Dispose(); throw; }
        }

        public void Dispose()
        {
            var samples = Samples;
            if (samples.IsCreated) samples.Dispose();
            Samples = default;
        }
    }

    public static class MeshModifyJob
    {
        [BurstCompile]
        private struct MeshFlipJob : IJobParallelFor
        {
            public FlipAxis axis;
            public NativeArray<Vector3> vertices;
            public NativeArray<Vector3> normals;
            public NativeArray<Vector4> tangents;

            public void Execute(int index)
            {
                var vertex = vertices[index];
                vertex[(int)axis] = -vertex[(int)axis];
                vertices[index] = vertex;
                if (index < normals.Length)
                {
                    var normal = normals[index];
                    normal[(int)axis] = -normal[(int)axis];
                    normals[index] = normal;
                }
                if (index < tangents.Length)
                {
                    var tangent = tangents[index];
                    tangent[(int)axis] = -tangent[(int)axis];
                    tangent.w = -tangent.w;
                    tangents[index] = tangent;
                }
            }
        }

        public static Mesh FlipMeshWithJob(Mesh mesh, FlipAxis axis)
        {
            if (mesh == null) return null;
            if (axis < FlipAxis.X || axis > FlipAxis.Z) throw new ArgumentOutOfRangeException(nameof(axis));
            var result = UnityEngine.Object.Instantiate(mesh);
            try
            {
                using (var vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob))
                using (var normals = new NativeArray<Vector3>(mesh.normals, Allocator.TempJob))
                using (var tangents = new NativeArray<Vector4>(mesh.tangents, Allocator.TempJob))
                {
                    var job = new MeshFlipJob { axis = axis, vertices = vertices, normals = normals, tangents = tangents };
                    job.Run(vertices.Length);
                    result.SetVertices(vertices);
                    if (normals.Length > 0) result.SetNormals(normals);
                    if (tangents.Length > 0) result.SetTangents(tangents);
                }
                ReverseWinding(result);
                result.RecalculateBounds();
                return result;
            }
            catch { DestroyMesh(result); throw; }
        }

        internal static void ReverseWinding(Mesh result)
        {
            for (int i = 0; i < result.subMeshCount; i++)
            {
                if (result.GetTopology(i) != MeshTopology.Triangles) continue;
                var triangles = result.GetTriangles(i);
                for (int j = 0; j < triangles.Length; j += 3)
                    (triangles[j], triangles[j + 2]) = (triangles[j + 2], triangles[j]);
                result.SetTriangles(triangles, i, false);
            }
        }

        [BurstCompile]
        private struct MeshBilinearTransformJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BilinearSample> Samples;
            [WriteOnly] public NativeArray<Vector3> Vertices;
            public Vector3 a, b, c, d;
            public float height;

            public void Execute(int index)
            {
                var sample = Samples[index];
                var weights = sample.weights;
                var point = a * weights.x + b * weights.y + c * weights.z + d * weights.w;
                point.y = sample.height * height;
                Vertices[index] = point;
            }
        }

        internal static void TransformInto(MeshDeformationTemplate template, NativeArray<Vector3> vertices,
            Mesh result, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float height)
        {
            if (!template.IsCreated) throw new ObjectDisposedException(nameof(template));
            var job = new MeshBilinearTransformJob
            {
                Samples = template.Samples, Vertices = vertices, a = a, b = b, c = c, d = d, height = height
            };
            // Synchronous entry point: Run avoids scheduling a job only to immediately wait for it.
            job.Run(vertices.Length);
            result.SetVertices(vertices);
            result.RecalculateBounds();
            if (vertices.Length == 0) return;
            result.RecalculateNormals();
            if (template.HasUv) result.RecalculateTangents();
        }

        /// <summary>One-shot compatibility API. The caller owns the returned mesh.</summary>
        public static Mesh TransformMeshJob(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float height)
        {
            using (var template = new MeshDeformationTemplate(mesh, Quaternion.identity))
            using (var vertices = new NativeArray<Vector3>(mesh.vertexCount, Allocator.TempJob))
            {
                var result = UnityEngine.Object.Instantiate(mesh);
                try { TransformInto(template, vertices, result, a, b, c, d, height); return result; }
                catch { DestroyMesh(result); throw; }
            }
        }

        internal static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
            else UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
