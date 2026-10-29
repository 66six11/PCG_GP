using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Continuous faceted wall between exact terrain rims, plus deterministic toe stones.</summary>
    public static class ProceduralCliffMesh
    {
        // Explicit cap band; intermediate rings share identical samples at neighboring edge endpoints.
        private static readonly float[] Rows = { 0, 0.045f, 0.12f, 0.36f, 0.63f, 0.86f, 0.955f, 1 };
        public static int RowCount => Rows.Length;

        public static void BuildStones(Mesh mesh, IReadOnlyList<Mesh> sources, IReadOnlyList<Placement> placements)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var indices = new List<int>();
            foreach (var p in placements) Append(vertices, uv, indices, sources[p.SourceIndex], p.Position, p.Size, p.Rotation);
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        public readonly struct Placement
        {
            public readonly Vector3 Position, Size;
            public readonly Quaternion Rotation;
            public readonly int SourceIndex;
            public readonly bool IsToe;
            public readonly int ClusterId;
            public readonly Vector3 AttachmentOutward;
            public Placement(Vector3 position, Vector3 size, Quaternion rotation, int sourceIndex, bool isToe = false, int clusterId = -1,
                Vector3 attachmentOutward = default)
            { Position = position; Size = size; Rotation = rotation; SourceIndex = sourceIndex; IsToe = isToe; ClusterId = clusterId; AttachmentOutward = attachmentOutward; }
        }

        public static int Build(TerrainSiteData surface, Mesh mesh, int seed, IReadOnlyList<Mesh> sources = null,
            bool includeBlocks = true, IReadOnlyList<Placement> placements = null)
        {
            var temporary = new List<Mesh>();
            if (sources == null || sources.Count == 0)
            {
                for (int i = 0; i < RockModelFactory.Names.Length; i++)
                {
                    var source = new Mesh { hideFlags = HideFlags.DontSave };
                    temporary.Add(source); RockModelFactory.Build(source, i);
                }
                sources = temporary;
            }
            try { return BuildCore(surface, mesh, seed, sources, includeBlocks, placements); }
            finally
            {
                foreach (var source in temporary)
                    if (Application.isPlaying) Object.Destroy(source); else Object.DestroyImmediate(source);
            }
        }

        private static int BuildCore(TerrainSiteData surface, Mesh mesh, int seed, IReadOnlyList<Mesh> sources,
            bool includeBlocks, IReadOnlyList<Placement> placements)
        {
            var vertices = new List<Vector3>(); var rock = new List<int>(); var grass = new List<int>();
            var uv = new List<Vector2>(); int stones = 0;
            foreach (var segment in surface.Cliffs)
            {
                var depths = CliffCornerPlanes.Depths(new[] { segment }, seed);
                for (int col = 0; col < 3; col++)
                {
                    var panelDepths = depths;
                    if (col != 1)
                    {
                        var top = CliffCornerPlanes.CrossSection(segment, 0, seed);
                        if ((col == 0 ? top.cutA : top.cutB) < 1e-6f) continue;
                        panelDepths = new List<float>();
                        foreach (float depth in depths)
                        {
                            panelDepths.Add(depth);
                            var slice = CliffCornerPlanes.CrossSection(segment, depth, seed);
                            if ((col == 0 ? slice.cutA : slice.cutB) < 1e-6f) break;
                        }
                    }
                    var polygon = new List<Vector3>();
                    foreach (float depth in panelDepths)
                    {
                        var slice = CliffCornerPlanes.CrossSection(segment, depth, seed);
                        polygon.Add(slice.Sample(col == 0 ? 0 : col == 1 ? slice.cutA : 1 - slice.cutB));
                    }
                    for (int i = panelDepths.Count - 1; i >= 0; i--)
                    {
                        var slice = CliffCornerPlanes.CrossSection(segment, panelDepths[i], seed);
                        polygon.Add(slice.Sample(col == 0 ? slice.cutA : col == 1 ? 1 - slice.cutB : 1));
                    }
                    var outward = col == 0 ? segment.NormalA : col == 2 ? segment.NormalB : (segment.NormalA + segment.NormalB).normalized;
                    var triangles = CliffPolygon.Triangulate(polygon, outward);
                    for (int i = 0; i < triangles.Count; i += 3)
                        Triangle(vertices, uv, rock, polygon[triangles[i]], polygon[triangles[i + 1]], polygon[triangles[i + 2]], outward);
                }
            }
            if (includeBlocks)
                foreach (var placement in placements ?? CreatePlacements(surface, seed))
                {
                    Append(vertices, uv, rock, sources[placement.SourceIndex % sources.Count], placement.Position,
                        placement.Size, placement.Rotation);
                    if (placement.IsToe) stones++;
                }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.subMeshCount = 2;
            mesh.SetTriangles(rock, 0); mesh.SetTriangles(grass, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return stones;
        }

        public static List<Placement> CreatePlacements(TerrainSiteData surface, int seed, float density = 0.52f,
            IReadOnlyList<Placement> prototype = null)
        {
            var result = new List<Placement>(); var exclusion = new List<Bounds>();
            int clusterId = 0;
            foreach (var segment in surface.Cliffs)
            {
                float height = segment.TopA.y - segment.BottomA.y;
                int blocks = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(segment.TopA, segment.TopB) / 2.4f));
                int bands = height > 5.5f ? 2 : 1;
                bool clusterHere = false;
                for (int col = 0; col < blocks; col++)
                    for (int band = 0; band < bands; band++)
                    {
                        int localSeed = seed + col * 97 + band * 157;
                        if (Noise(segment.TopA, localSeed + 501) > Mathf.Clamp01(density) * 0.65f) continue;
                        float r = Noise(segment.TopB, localSeed + 21);
                        float u = (col + 0.12f + 0.76f * Noise(segment.TopA, localSeed + 32)) / blocks;
                        float depth = (band + 0.32f + 0.42f * r) / bands;
                        var local = CreateClusterPieces(localSeed, Mathf.Min(1, height / 3.4f), prototype);
                        var attached = AttachCluster(segment, u, depth, seed, clusterId, local);
                        var bound = BoundsOf(attached); bound.size *= 0.65f; bool crowded = false;
                        foreach (var other in exclusion) if (other.Intersects(bound)) { crowded = true; break; }
                        if (crowded) continue;
                        exclusion.Add(bound);
                        result.AddRange(attached); clusterHere = true; clusterId++;
                    }
                // Sparse, uneven foot clusters instead of a complete ring of rubble.
                for (int i = 0; i < 2; i++)
                {
                    if (Noise(segment.BottomB, seed + 811 + i * 67) < (clusterHere ? 0.55f : 0.90f)) continue;
                    float u = 0.10f + 0.80f * Noise(segment.BottomA, seed + i * 47);
                    var foot = Vector3.Lerp(segment.BottomA, segment.BottomB, u);
                    var normal = Vector3.Lerp(segment.NormalA, segment.NormalB, u).normalized;
                    var point = foot + normal * (0.35f + Noise(foot, seed + i) * 0.85f);
                    if (!surface.TryHeight(new Vector2(point.x, point.z), out float ground) || ground > foot.y + 0.6f) continue;
                    float scale = 0.26f + Noise(foot, seed + 71 + i) * 0.38f;
                    point.y = ground + scale * 0.18f;
                    result.Add(new Placement(point, new Vector3(scale * 1.5f, scale, scale * 1.1f),
                        Quaternion.Euler(0, Noise(point, seed) * 360, 0), i == 0 ? 3 : 2, true));
                }
            }
            return result;
        }

        public static List<Placement> CreateClusterPieces(int seed, float scale = 1, IReadOnlyList<Placement> prototype = null)
        {
            // Model a coherent cluster first, in X=along cliff, Y=up, Z=outward space.
            // The fallback mirrors the user's two tall stones, front shoulder and low foot blocks.
            if (prototype == null || prototype.Count == 0)
                prototype = new[]
                {
                    new Placement(new Vector3(-0.12f, 0.30f, -0.10f), new Vector3(0.48f, 2.65f, 0.46f), Quaternion.Euler(0, -7, 2), 4),
                    new Placement(new Vector3(0.35f, 0.04f, -0.16f), new Vector3(0.43f, 2.10f, 0.40f), Quaternion.Euler(1, 9, -3), 5),
                    new Placement(new Vector3(-0.35f, -0.40f, 0.19f), new Vector3(0.69f, 1.28f, 0.54f), Quaternion.Euler(0, -9, 3), 0),
                    new Placement(new Vector3(0.38f, -0.75f, 0.20f), new Vector3(0.57f, 0.67f, 0.57f), Quaternion.Euler(0, 12, -2), 2),
                    new Placement(new Vector3(-0.02f, -1.10f, 0.23f), new Vector3(1.17f, 0.38f, 0.79f), Quaternion.Euler(0, -6, 0), 1)
                };
            var result = new List<Placement>();
            for (int i = 0; i < prototype.Count; i++)
            {
                var p = prototype[i]; var key = new Vector3(i * 1.7f, 0, 0.31f);
                var offset = new Vector3((Noise(key, seed + 7) - 0.5f) * 0.10f,
                    (Noise(key, seed + 17) - 0.5f) * 0.15f, (Noise(key, seed + 27) - 0.5f) * 0.06f);
                float sizeFactor = scale * (0.94f + Noise(key, seed + 91) * 0.12f);
                var rotation = p.Rotation * Quaternion.Euler((Noise(key, seed + 11) - 0.5f) * 2,
                    (Noise(key, seed + 31) - 0.5f) * 9, (Noise(key, seed + 51) - 0.5f) * 3);
                result.Add(new Placement((p.Position + offset) * scale, p.Size * sizeFactor, rotation, p.SourceIndex));
            }
            return result;
        }

        public static List<Placement> AttachCluster(TerrainSiteData.CliffSegment segment, float u, float depth,
            int seed, int clusterId, IReadOnlyList<Placement> local)
        {
            var top = Vector3.Lerp(segment.TopA, segment.TopB, u); var bottom = Vector3.Lerp(segment.BottomA, segment.BottomB, u);
            var normal = Vector3.Lerp(segment.NormalA, segment.NormalB, u).normalized;
            var up = Vector3.RotateTowards(Vector3.up, (top - bottom).normalized, 10 * Mathf.Deg2Rad, 0).normalized;
            var tangent = Vector3.Cross(up, normal).normalized; var forward = Vector3.Cross(tangent, up).normalized;
            var frame = Quaternion.LookRotation(forward, up);
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var p in local)
            {
                float halfY = RotatedHalfSize(p.Size, frame * p.Rotation).y;
                float centerY = (frame * p.Position).y;
                low = Mathf.Min(low, centerY - halfY); high = Mathf.Max(high, centerY + halfY);
            }
            float center = Mathf.Clamp(Mathf.Lerp(top.y, bottom.y, depth), bottom.y - low - 0.04f, top.y - high - 0.14f);
            depth = Mathf.Clamp01((top.y - center) / Mathf.Max(0.001f, top.y - bottom.y));
            var anchor = AtDepth(segment, u, depth, seed);
            // Three rear probes on the primary long rock determine one common attachment offset.
            // Every child retains its local position, rotation and size after this rigid transform.
            float gap = float.NegativeInfinity; var main = local[0];
            var mainCenter = anchor + frame * main.Position; var mainRotation = frame * main.Rotation;
            for (int i = -1; i <= 1; i++)
            {
                var probe = mainCenter + mainRotation * Vector3.up * (i * main.Size.y * 0.40f) - forward * main.Size.z * 0.40f;
                float t = Mathf.Clamp01((top.y - probe.y) / Mathf.Max(0.001f, top.y - bottom.y));
                var backing = AtDepth(segment, u, t, seed);
                gap = Mathf.Max(gap, Vector3.Dot(probe - backing, forward));
            }
            anchor -= forward * (gap + 0.06f);
            var result = new List<Placement>();
            foreach (var p in local)
                result.Add(new Placement(anchor + frame * p.Position, p.Size, frame * p.Rotation, p.SourceIndex, false, clusterId));
            return result;
        }

        private static Bounds BoundsOf(IReadOnlyList<Placement> placements)
        {
            var bound = new Bounds(placements[0].Position, Vector3.zero);
            foreach (var p in placements)
            { var half = RotatedHalfSize(p.Size, p.Rotation); bound.Encapsulate(p.Position - half); bound.Encapsulate(p.Position + half); }
            return bound;
        }

        private static Vector3 RotatedHalfSize(Vector3 size, Quaternion rotation)
        {
            var x = rotation * Vector3.right * size.x * 0.5f;
            var y = rotation * Vector3.up * size.y * 0.5f;
            var z = rotation * Vector3.forward * size.z * 0.5f;
            return new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        }

        public static Vector3 Sample(TerrainSiteData.CliffSegment segment, float u, int row, int seed)
            => CliffCornerPlanes.CrossSection(segment, Rows[row], seed).Sample(u);

        public static Vector3 AtDepth(TerrainSiteData.CliffSegment segment, float u, float depth, int seed)
            => CliffCornerPlanes.CrossSection(segment, depth, seed).Sample(u);
        private static float Noise(Vector3 p, int seed)
        {
            // Quantization gives shared endpoints the same sample regardless of edge orientation.
            unchecked
            {
                uint h = (uint)(Mathf.RoundToInt(p.x * 1000) * 73856093)
                    ^ (uint)(Mathf.RoundToInt(p.z * 1000) * 19349663) ^ (uint)seed * 83492791u;
                h ^= h >> 16; h *= 2246822519u; h ^= h >> 13;
                return (h & 0x00ffffff) / 16777215f;
            }
        }

        private static void Append(List<Vector3> vertices, List<Vector2> uv, List<int> indices,
            Mesh source, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var bounds = source.bounds; var scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            int first = vertices.Count;
            foreach (var p in source.vertices)
            { var world = center + rotation * Vector3.Scale(p - bounds.center, scale); vertices.Add(world); uv.Add(new Vector2(world.x + world.z, world.y)); }
            foreach (int index in source.triangles) indices.Add(first + index);
        }

        private static void Triangle(List<Vector3> vertices, List<Vector2> uv, List<int> indices,
            Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { var swap = b; b = c; c = swap; }
            int first = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            uv.Add(new Vector2(a.x + a.z, a.y)); uv.Add(new Vector2(b.x + b.z, b.y)); uv.Add(new Vector2(c.x + c.z, c.y));
            indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
        }
    }
}
