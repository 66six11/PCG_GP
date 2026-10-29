using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexagonalGrids.Prototype
{
    /// <summary>Local rock chunks own their meshes and colliders; soil topology remains independent.</summary>
    public sealed class CliffRockMass : IDisposable
    {
        private sealed class Chunk
        {
            public GameObject root;
            public Mesh mesh;
            public MeshCollider collider;
            public List<ProceduralCliffMesh.Placement> placements;
            public Mesh[] sources;
        }
        private readonly Dictionary<(Vector2, Vector2), Chunk> chunks = new Dictionary<(Vector2, Vector2), Chunk>();
        public int RebuiltChunks { get; private set; }
        public int ChunkCount => chunks.Count;
        public int FaceCount { get; private set; }
        public int ToeCount { get; private set; }

        public void Update(TerrainSiteData surface, Mesh[] sources, Transform parent, Material material,
            int seed, float density, bool visible)
        {
            RebuiltChunks = 0; FaceCount = 0; ToeCount = 0;
            var retained = new HashSet<(Vector2, Vector2)>();
            if (visible && sources != null && sources.Length > 0)
                foreach (var segment in surface.Cliffs)
                {
                    var key = (new Vector2(segment.TopA.x, segment.TopA.z), new Vector2(segment.TopB.x, segment.TopB.z));
                    retained.Add(key);
                    var placements = Compose(surface, segment, seed, density, sources.Length);
                    foreach (var p in placements) { if (p.IsToe) ToeCount++; else FaceCount++; }
                    if (!chunks.TryGetValue(key, out var chunk))
                    {
                        var root = new GameObject("Rock mass chunk") { layer = 30 };
                        root.transform.SetParent(parent, false);
                        chunk = new Chunk { root = root, mesh = new Mesh { name = "Local rock mass", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave } };
                        root.AddComponent<MeshFilter>().sharedMesh = chunk.mesh;
                        root.AddComponent<MeshRenderer>().sharedMaterial = material;
                        chunk.collider = root.AddComponent<MeshCollider>(); chunks.Add(key, chunk);
                    }
                    if (Same(chunk.placements, placements) && SameSources(chunk.sources, sources)) continue;
                    chunk.collider.sharedMesh = null;
                    ProceduralCliffMesh.BuildStones(chunk.mesh, sources, placements);
                    chunk.collider.sharedMesh = chunk.mesh;
                    chunk.placements = placements; chunk.sources = (Mesh[])sources.Clone(); RebuiltChunks++;
                }
            var removed = new List<(Vector2, Vector2)>();
            foreach (var pair in chunks) if (!retained.Contains(pair.Key)) { Release(pair.Value); removed.Add(pair.Key); }
            foreach (var key in removed) chunks.Remove(key);
        }

        public static List<ProceduralCliffMesh.Placement> Compose(TerrainSiteData surface,
            TerrainSiteData.CliffSegment segment, int seed, float density, int sourceCount)
        {
            var result = new List<ProceduralCliffMesh.Placement>();
            if (sourceCount <= 0) return result;
            // Segment identity, rather than traversal order, keeps unaffected chunks deterministic.
            int localSeed;
            unchecked { localSeed = seed ^ Mathf.RoundToInt(segment.TopA.x * 1000) * 73856093 ^ Mathf.RoundToInt(segment.TopA.z * 1000) * 19349663; }
            var random = new System.Random(localSeed);
            float length = Vector3.Distance(segment.TopA, segment.TopB);
            int columns = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Lerp(1.65f, 0.95f, density)));
            for (int column = 0; column < columns; column++)
            {
                float u = (column + Mathf.Lerp(0.32f, 0.68f, Next(random))) / columns;
                var top = Vector3.Lerp(segment.TopA, segment.TopB, u);
                var foot = Vector3.Lerp(segment.BottomA, segment.BottomB, u);
                var normal = Vector3.Lerp(segment.NormalA, segment.NormalB, u); normal.y = 0; normal.Normalize();
                var tangent = Vector3.Cross(Vector3.up, normal);
                float rise = Mathf.Max(0.1f, top.y - foot.y);
                float width = length / columns * Mathf.Lerp(1.05f, 1.35f, Next(random));
                float height = rise * Mathf.Lerp(0.76f, 1.01f, Next(random));
                float thickness = Mathf.Lerp(0.9f, 1.45f, Next(random));
                var frame = Quaternion.LookRotation(normal, Vector3.up);
                // The continuous cliff is the primary stone; these are embedded secondary faces.
                height *= Mathf.Lerp(0.48f, 0.78f, Next(random));
                var main = foot;
                main.y = foot.y + height * 0.5f - 0.12f;
                main = FitToMainStone(segment, u, main.y, height, thickness, normal, surface.RockShapeSeed);
                result.Add(new ProceduralCliffMesh.Placement(main, new Vector3(width, height, thickness),
                    frame * Quaternion.Euler(Signed(random, 3), Signed(random, 12), Signed(random, 3)), random.Next(sourceCount), false, column));
                // Satellites inherit the cluster frame and get progressively shorter toward the front.
                int shoulders = Mathf.CeilToInt(width / 0.55f);
                for (int i = 0; i < shoulders; i++)
                {
                    float side = Mathf.Lerp(-0.55f, 0.55f, (i + 0.5f) / shoulders) * width;
                    float h = height * Mathf.Lerp(0.24f, 0.66f, Next(random));
                    float w = Mathf.Lerp(0.35f, 0.70f, Next(random));
                    float sideU = Mathf.Clamp01(u + side / Mathf.Max(length, 0.001f));
                    var p = FitToMainStone(segment, sideU, foot.y + h * 0.5f - 0.1f, h, w * 0.9f, normal, surface.RockShapeSeed);
                    result.Add(new ProceduralCliffMesh.Placement(p, new Vector3(w, h, w * 0.9f),
                        frame * Quaternion.Euler(Signed(random, 4), Signed(random, 18), Signed(random, 4)), random.Next(sourceCount), false, column));
                }
                int rubble = Mathf.CeilToInt(width / 0.32f * density);
                for (int i = 0; i < rubble; i++)
                {
                    var p = foot + tangent * Signed(random, width * 0.8f) + normal * Mathf.Lerp(0.5f, 1.5f, Next(random));
                    if (!surface.TryHeight(new Vector2(p.x, p.z), out float ground) || ground > foot.y + 0.6f) continue;
                    float size = Mathf.Lerp(0.16f, 0.44f, Next(random)); p.y = ground + size * 0.22f;
                    result.Add(new ProceduralCliffMesh.Placement(p, new Vector3(size * 1.4f, size * 0.6f, size),
                        Quaternion.Euler(Signed(random, 8), Next(random) * 360, Signed(random, 8)), random.Next(sourceCount), true, column));
                }
            }
            return result;
        }
        private static Vector3 FitToMainStone(TerrainSiteData.CliffSegment segment, float u,
            float centerY, float height, float thickness, Vector3 outward, int seed)
        {
            var top = Vector3.Lerp(segment.TopA, segment.TopB, u);
            var bottom = Vector3.Lerp(segment.BottomA, segment.BottomB, u);
            float rise = Mathf.Max(0.001f, top.y - bottom.y);
            float depth = Mathf.Clamp01((top.y - centerY) / rise);
            var anchor = ProceduralCliffMesh.AtDepth(segment, u, depth, seed);
            anchor.y = centerY;
            // Fit three heights to the actual sculpted backing, rather than the foot line.
            // Keep the rearmost probes inside the primary stone at every sampled height.
            float inset = 0;
            for (int i = -1; i <= 1; i++)
            {
                float y = centerY + i * height * 0.36f;
                var backing = ProceduralCliffMesh.AtDepth(segment, u, Mathf.Clamp01((top.y - y) / rise), seed);
                inset = Mathf.Max(inset, Vector3.Dot(anchor - backing, outward));
            }
            return anchor + outward * (thickness * 0.10f - inset - 0.04f);
        }
        private static float Next(System.Random random) => (float)random.NextDouble();
        private static float Signed(System.Random random, float magnitude) => (Next(random) * 2 - 1) * magnitude;
        private static bool SameSources(Mesh[] a, Mesh[] b)
        { if (a == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        private static bool Same(List<ProceduralCliffMesh.Placement> a, List<ProceduralCliffMesh.Placement> b)
        {
            if (a == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Position != b[i].Position || a[i].Size != b[i].Size || a[i].Rotation != b[i].Rotation || a[i].SourceIndex != b[i].SourceIndex) return false;
            return true;
        }
        private static void Release(Chunk chunk) { Destroy(chunk.root); Destroy(chunk.mesh); }
        private static void Destroy(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) { if (value is GameObject root) root.SetActive(false); UnityEngine.Object.Destroy(value); } else UnityEngine.Object.DestroyImmediate(value); }
        public void Dispose() { foreach (var chunk in chunks.Values) Release(chunk); chunks.Clear(); }
    }
}
