using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexagonalGrids.Prototype
{
    /// <summary>Upright plugin rocks distributed over continuous crest-to-toe wall fields.</summary>
    public sealed class SlopeRockCover : IDisposable
    {
        private sealed class Chunk
        {
            public GameObject Root;
            public Mesh Mesh;
            public Mesh[] Sources;
            public List<ProceduralCliffMesh.Placement> Placements;
        }
        private readonly Dictionary<int, Chunk> chunks = new Dictionary<int, Chunk>();
        public int ChunkCount => chunks.Count;
        public int RebuiltChunks { get; private set; }
        public int FaceCount { get; private set; }
        public int ToeCount { get; private set; }

        public void Update(TerrainSiteData surface, TerrainSlopeField slopes, Mesh[] sources, Transform parent,
            Material material, int seed, float density, bool visible, float conformity = 0.65f)
        {
            RebuiltChunks = FaceCount = ToeCount = 0;
            var retained = new HashSet<int>();
            var field = visible && density > 0 ? new RockWallField(surface, slopes) : null;
            if (field != null && sources != null && sources.Length > 0)
                foreach (var region in field.Regions)
                {
                    var placements = Compose(surface, region, seed, density, sources, conformity);
                    if (placements.Count == 0) continue;
                    retained.Add(region.Id);
                    foreach (var p in placements) { if (p.IsToe) ToeCount++; else FaceCount++; }
                    if (!chunks.TryGetValue(region.Id, out var chunk))
                    {
                        var root = new GameObject("Slope rock region " + region.Id) { layer = 30 };
                        root.transform.SetParent(parent, false);
                        chunk = new Chunk { Root = root, Mesh = new Mesh { name = "Slope rock cover", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave } };
                        root.AddComponent<MeshFilter>().sharedMesh = chunk.Mesh;
                        root.AddComponent<MeshRenderer>().sharedMaterial = material;
                        root.AddComponent<MeshCollider>(); chunks.Add(region.Id, chunk);
                    }
                    if (Same(chunk, placements, sources)) continue;
                    var collider = chunk.Root.GetComponent<MeshCollider>(); collider.sharedMesh = null;
                    ProceduralCliffMesh.BuildStones(chunk.Mesh, sources, placements); collider.sharedMesh = chunk.Mesh;
                    chunk.Placements = placements; chunk.Sources = (Mesh[])sources.Clone(); RebuiltChunks++;
                }
            var removed = new List<int>();
            foreach (var pair in chunks) if (!retained.Contains(pair.Key)) { Release(pair.Value); removed.Add(pair.Key); }
            foreach (int id in removed) chunks.Remove(id);
        }

        public const float OutsideReach = 1.6f;
        public static float HeightStrength(float rise) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3, 12, rise));
        public static float MainHeightLimit(float rise)
            => rise <= 2.2f ? rise : 2.2f + 2 * (1 - Mathf.Exp(-(rise - 2.2f) / 4));
        public static float MainWidthLimit(float rise) => Mathf.Lerp(1.35f, 1.8f, HeightStrength(rise));
        // Absolute exterior size limits remain independent of cliff height.
        public static float MaximumDimension(float wallDistance, float rise)
        {
            if (wallDistance <= 0.25f)
                return Mathf.Lerp(MainHeightLimit(rise), Mathf.Min(rise, 1.5f), Mathf.SmoothStep(0, 1, wallDistance / 0.25f));
            if (wallDistance <= 0.9f)
                return Mathf.Lerp(Mathf.Min(rise, 1.5f), Mathf.Min(rise, 0.45f), (wallDistance - 0.25f) / 0.65f);
            return Mathf.Lerp(Mathf.Min(rise, 0.45f), Mathf.Min(rise, 0.14f), Mathf.Clamp01((wallDistance - 0.9f) / 0.7f));
        }
        private readonly struct Candidate
        {
            public readonly ProceduralCliffMesh.Placement Placement;
            public readonly uint Priority;
            public readonly float Acceptance;
            public Candidate(ProceduralCliffMesh.Placement placement, uint priority, float acceptance)
            { Placement = placement; Priority = priority; Acceptance = acceptance; }
        }
        private static uint Hash(int x, int z, int seed)
        {
            uint h = unchecked((uint)x * 374761393u + (uint)z * 668265263u + (uint)seed * 2246822519u);
            h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16);
        }
        public static List<ProceduralCliffMesh.Placement> Compose(TerrainSiteData surface, RockWallField.Region region,
            int seed, float density, Mesh[] sources, float conformity = 0.65f)
        {
            var result = new List<ProceduralCliffMesh.Placement>();
            if (sources == null || sources.Length == 0 || density <= 0) return result;
            var candidates = new List<Candidate>();
            var catalog = new RockShapeCatalog(sources);
            float reach = OutsideReach;
            // Independent fields for the primary silhouette, embedded medium rocks and toe rubble.
            // Candidates are keyed in world space; no fixed-size cluster is introduced.
            for (int layer = 0; layer < 3; layer++)
            {
            float spacing = layer == 2 ? 0.27f : layer == 1 ? 0.30f : 0.42f;
            for (int x = Mathf.FloorToInt((region.Bounds.xMin - reach) / spacing); x <= Mathf.CeilToInt((region.Bounds.xMax + reach) / spacing); x++)
                for (int z = Mathf.FloorToInt((region.Bounds.yMin - reach) / spacing); z <= Mathf.CeilToInt((region.Bounds.yMax + reach) / spacing); z++)
                {
                    uint hash = Hash(x, z, unchecked(seed + layer * 10103));
                    var random = new System.Random(unchecked((int)hash));
                    float Next() => (float)random.NextDouble();
                    var point = new Vector2((x + 0.1f + Next() * 0.8f) * spacing, (z + 0.1f + Next() * 0.8f) * spacing);
                    if (!RockWallField.TrySample(region, point, out var field) || !surface.TryHeight(point, out float ground)) continue;
                    float rise = field.Rise;
                    float high = HeightStrength(rise);
                    float elevation = Mathf.Clamp01((ground - field.Toe.y) / Mathf.Max(0.01f, rise));
                    if (layer == 1 && (high < 0.01f || field.WallDistance > 0.05f || elevation < 0.12f || elevation > 0.88f)) continue;
                    if (layer == 2 && (high < 0.01f || elevation > 0.12f || field.WallDistance < 0.12f)) continue;
                    if (rise < 0.65f || ground > field.Crest.y - 0.22f) continue;
                    // Euclidean distance to finite steep triangles, not projection onto an infinite toe plane.
                    float wallDistance = field.WallDistance;
                    float distance = wallDistance / OutsideReach;
                    if (distance >= 1 || ground < field.Toe.y - 0.6f) continue;
                    float curve = Mathf.Pow(1 - distance, 1.7f);
                    float wave = Mathf.PerlinNoise(point.x * 0.27f + seed * 0.013f, point.y * 0.27f + 19.3f);
                    float height = Mathf.Min(rise * curve * Mathf.Lerp(0.72f, 1.08f, Next()), field.Crest.y - ground + 0.08f);
                    float longest = Mathf.Min(height, MaximumDimension(wallDistance, rise));
                    if (layer == 1) longest = Mathf.Min(longest, Mathf.Lerp(0.65f, 1.55f, Next()));
                    if (layer == 2) longest = Mathf.Min(longest, Mathf.Lerp(0.18f, 0.38f, Next()));
                    if (longest < 0.18f) continue;
                    bool rubble = layer == 2 || wallDistance >= 0.9f || longest < 0.45f;
                    var role = rubble ? RockShapeCatalog.Role.Debris
                        : Next() < 0.18f ? RockShapeCatalog.Role.Column : RockShapeCatalog.Role.MainBlock;
                    int source = catalog.Choose(role, random);
                    if (source < 0) continue;
                    // Use the natural thick or slender source shape. All axes are capped, not only height.
                    var size = RockShapeCatalog.SizeFor(sources[source], longest);
                    if (!rubble)
                    {
                        float horizontal = Mathf.Min(1, MainWidthLimit(rise) / Mathf.Max(size.x, size.z));
                        size.x *= horizontal; size.z *= horizontal;
                    }
                    var rotation = Quaternion.LookRotation(field.Outward, Vector3.up)
                        * Quaternion.Euler(-Mathf.Clamp01(conformity) * (rubble ? 0 : 7) + (Next() - 0.5f) * 4,
                            (Next() - 0.5f) * (rubble ? 160 : 60), (Next() - 0.5f) * (rubble ? 12 : 5));
                    // Original assets are all upright: lay their long axis down for actual rubble silhouettes.
                    if (rubble) rotation *= Quaternion.Euler(0, 0, 90);
                    var extents = WorldExtents(rotation, size);
                    var position = new Vector3(point.x, ground + (rubble ? extents.y - longest * 0.12f : extents.y * 0.5f), point.y);
                    if (!Seat(surface, ref position, rotation, size, !rubble, field.Outward)) continue;
                    if (!rubble && !Embed(surface, ref position, rotation, size, field.Outward, rise, layer == 1)) continue;
                    // Inward translation changes the sampled profile at corners. Revalidate at the final XZ.
                    if (!RockWallField.TrySample(region, new Vector2(position.x, position.z), out var settled)) continue;
                    float settledCap = MaximumDimension(settled.WallDistance, settled.Rise);
                    float currentSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    if (currentSize > settledCap) size *= settledCap / currentSize;
                    if (!rubble)
                    {
                        float horizontal = Mathf.Min(1, MainWidthLimit(settled.Rise) / Mathf.Max(size.x, size.z));
                        size.x *= horizontal; size.z *= horizontal;
                    }
                    if (!Seat(surface, ref position, rotation, size, !rubble, settled.Outward)) continue;
                    float top = WorldExtents(rotation, size).y;
                    position.y = Mathf.Min(position.y, Mathf.Min(field.Crest.y, settled.Crest.y) + 0.12f - top);
                    if (!rubble && !HasVisibleFront(surface, position, rotation, size, settled.Outward)) continue;
                    if (NearBuilding(surface, position, Mathf.Max(size.x, size.z) * 0.65f)) continue;
                    float acceptance = Mathf.Lerp(0.16f, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.3f, 0.68f, wave)))
                        * Mathf.Lerp(1, 0.35f, distance);
                    if (layer == 1) acceptance = Mathf.Lerp(0.75f, 1, wave) * high;
                    if (layer == 2) acceptance = Mathf.Lerp(0.5f, 0.95f, wave) * high;
                    candidates.Add(new Candidate(new ProceduralCliffMesh.Placement(position, size, rotation, source, rubble, region.Id, settled.Outward), hash, acceptance));
                }
            }
            // Size-aware spacing operates before the coverage gate, so raising coverage never rerolls existing rocks.
            candidates.Sort((a, b) => { int order = b.Placement.Size.y.CompareTo(a.Placement.Size.y); return order != 0 ? order : a.Priority.CompareTo(b.Priority); });
            var occupied = new List<ProceduralCliffMesh.Placement>();
            foreach (var candidate in candidates)
            {
                var p = candidate.Placement; bool overlaps = false;
                float Radius(ProceduralCliffMesh.Placement q) => Mathf.Max(0.12f, Mathf.Max(q.Size.x, q.Size.z) * 0.36f);
                foreach (var other in occupied)
                {
                    var delta = p.Position - other.Position;
                    bool separateHeight = Mathf.Abs(delta.y) > (WorldExtents(p.Rotation, p.Size).y + WorldExtents(other.Rotation, other.Size).y) * 0.75f;
                    delta.y = 0;
                    if (!separateHeight && delta.magnitude < Radius(p) + Radius(other)) { overlaps = true; break; }
                }
                if (overlaps) continue;
                occupied.Add(p);
                float roll = (Hash(unchecked((int)candidate.Priority), seed, 913) & 0xffffff) / 16777216f;
                if (roll < Mathf.Clamp01(density) * candidate.Acceptance) result.Add(p);
            }
            return result;
        }
        public static float RequestedEmbed(float rise, float depth, bool medium = false)
            => Mathf.Min(0.65f, depth * Mathf.Lerp(medium ? 0.14f : 0.06f, medium ? 0.40f : 0.32f, HeightStrength(rise)));

        private static float Depth(Quaternion rotation, Vector3 size, Vector3 outward)
            => Mathf.Abs(Vector3.Dot(rotation * Vector3.right, outward)) * size.x
                + Mathf.Abs(Vector3.Dot(rotation * Vector3.up, outward)) * size.y
                + Mathf.Abs(Vector3.Dot(rotation * Vector3.forward, outward)) * size.z;

        public static bool HasVisibleFront(TerrainSiteData surface, Vector3 center, Quaternion rotation, Vector3 size, Vector3 outward)
        {
            // Keep the front half of the projected thickness exposed at mid-height.
            float depth = Depth(rotation, size, outward);
            var probe = center;
            return surface.TryHeight(new Vector2(probe.x, probe.z), out float ground) && probe.y >= ground + 0.04f;
        }

        private static bool Embed(TerrainSiteData surface, ref Vector3 center, Quaternion rotation, Vector3 size,
            Vector3 outward, float rise, bool medium)
        {
            if (!HasVisibleFront(surface, center, rotation, size, outward)) return false;
            var original = center;
            float lo = 0, hi = RequestedEmbed(rise, Depth(rotation, size, outward), medium);
            for (int i = 0; i < 9; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (HasVisibleFront(surface, original - outward * mid, rotation, size, outward)) lo = mid; else hi = mid;
            }
            var embedded = original - outward * lo;
            if (Seat(surface, ref embedded, rotation, size, true, outward) && HasVisibleFront(surface, embedded, rotation, size, outward)) center = embedded;
            return true;
        }

        private static Vector3 WorldExtents(Quaternion rotation, Vector3 size)
        {
            var x = rotation * Vector3.right * size.x * 0.5f;
            var y = rotation * Vector3.up * size.y * 0.5f;
            var z = rotation * Vector3.forward * size.z * 0.5f;
            return new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        }
        private static readonly Vector2[] Support = { Vector2.zero, Vector2.left, Vector2.right, Vector2.up, Vector2.down };
        private static bool Seat(TerrainSiteData surface, ref Vector3 center, Quaternion rotation, Vector3 size,
            bool body = false, Vector3 outward = default)
        {
            var extents = WorldExtents(rotation, size); float ceiling = center.y;
            if (body)
            {
                var tangent = Vector3.Cross(Vector3.up, outward);
                float depth = Depth(rotation, size, outward);
                for (int side = -1; side <= 1; side++)
                {
                    var offset = -outward * depth * 0.3f + tangent * side * Mathf.Min(size.x, size.z) * 0.18f
                        - Vector3.up * extents.y * 0.15f;
                    var point = center + offset;
                    if (!surface.TryHeight(new Vector2(point.x, point.z), out float ground)) return false;
                    ceiling = Mathf.Min(ceiling, ground - offset.y - 0.02f);
                }
                center.y = ceiling; return true;
            }
            foreach (var probe in Support)
            {
                var offset = new Vector3(probe.x * extents.x * 0.55f, -extents.y * 0.8f, probe.y * extents.z * 0.55f);
                var point = center + offset;
                if (!surface.TryHeight(new Vector2(point.x, point.z), out float ground)) return false;
                ceiling = Mathf.Min(ceiling, ground - offset.y - 0.02f);
            }
            center.y = ceiling; return true;
        }

        private static bool NearBuilding(TerrainSiteData surface, Vector3 p, float margin)
        {
            if (!surface.Graded) return false;
            var rect = surface.Footprint;
            rect.xMin -= margin; rect.xMax += margin; rect.yMin -= margin; rect.yMax += margin;
            return rect.Contains(new Vector2(p.x, p.z));
        }

        public static bool HasRearContact(TerrainSiteData surface, ProceduralCliffMesh.Placement p)
        {
            if (p.IsToe) return RearContact(surface, p.Position, p.Rotation, p.Size);
            var direction = p.AttachmentOutward.sqrMagnitude > 0.01f ? p.AttachmentOutward
                : Vector3.ProjectOnPlane(p.Rotation * Vector3.forward, Vector3.up).normalized;
            var center = p.Position;
            return Seat(surface, ref center, p.Rotation, p.Size, true, direction) && center.y >= p.Position.y - 0.025f;
        }

        private static bool RearContact(TerrainSiteData surface, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var extents = WorldExtents(rotation, size);
            foreach (var probe in Support)
            {
                var point = center + new Vector3(probe.x * extents.x * 0.55f, -extents.y * 0.8f, probe.y * extents.z * 0.55f);
                if (!surface.TryHeight(new Vector2(point.x, point.z), out float ground) || point.y > ground + 0.025f) return false;
            }
            return true;
        }

        private static bool Same(Chunk chunk, List<ProceduralCliffMesh.Placement> placements, Mesh[] sources)
        {
            if (chunk.Placements == null || chunk.Placements.Count != placements.Count || chunk.Sources.Length != sources.Length) return false;
            for (int i = 0; i < sources.Length; i++) if (sources[i] != chunk.Sources[i]) return false;
            for (int i = 0; i < placements.Count; i++)
            {
                var a = chunk.Placements[i]; var b = placements[i];
                if (a.Position != b.Position || a.Size != b.Size || a.Rotation != b.Rotation || a.SourceIndex != b.SourceIndex) return false;
            }
            return true;
        }
        private static void Destroy(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
        private static void Release(Chunk chunk) { chunk.Root.SetActive(false); Destroy(chunk.Root); Destroy(chunk.Mesh); }
        public void Dispose() { foreach (var chunk in chunks.Values) Release(chunk); chunks.Clear(); }
    }
}

