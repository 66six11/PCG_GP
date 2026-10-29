using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Connected steep regions and crest-to-toe profiles. Read-only terrain analysis.</summary>
    public sealed class RockWallField
    {
        public sealed class Region
        {
            public int Id;
            public readonly List<Profile> Profiles = new List<Profile>();
            public readonly List<Vector2[]> WallTriangles = new List<Vector2[]>();
            public float DistanceToWall(Vector2 point)
            {
                float best = float.MaxValue;
                foreach (var t in WallTriangles)
                {
                    float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
                    float u = Cross(t[1] - t[0], point - t[0]);
                    float v = Cross(t[2] - t[1], point - t[1]);
                    float w = Cross(t[0] - t[2], point - t[2]);
                    if ((u >= 0 && v >= 0 && w >= 0) || (u <= 0 && v <= 0 && w <= 0)) return 0;
                    for (int i = 0; i < 3; i++)
                    {
                        var a = t[i]; var delta = t[(i + 1) % 3] - a;
                        float along = delta.sqrMagnitude < 1e-10f ? 0 : Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
                        best = Mathf.Min(best, Vector2.Distance(point, a + delta * along));
                    }
                }
                return best;
            }
            public Rect Bounds;
            public float MaximumRise;
        }
        public readonly struct Profile
        {
            public readonly Vector3 Crest, Toe, Outward;
            public float Rise => Crest.y - Toe.y;
            public Profile(Vector3 crest, Vector3 toe, Vector3 outward)
            { Crest = crest; Toe = toe; Outward = outward; }
        }
        public readonly struct Sample
        {
            public readonly Vector3 Crest, Toe, Outward;
            public readonly float WallDistance;
            public float Rise => Crest.y - Toe.y;
            public Sample(Vector3 crest, Vector3 toe, Vector3 outward, float wallDistance)
            { Crest = crest; Toe = toe; Outward = outward; WallDistance = wallDistance; }
        }
        public readonly List<Region> Regions = new List<Region>();
        private static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);
        public RockWallField(TerrainSiteData surface, TerrainSlopeField slopes)
        {
            var candidates = new Dictionary<int, TerrainSlopeField.Sample>();
            var edges = new Dictionary<ulong, List<int>>();
            foreach (var f in slopes.Faces)
            {
                // No per-triangle relief threshold: tessellation must not determine the wall height.
                if (f.Degrees < 38) continue;
                candidates.Add(f.Id, f);
                Edge(f.A, f.B, f.Id); Edge(f.B, f.C, f.Id); Edge(f.C, f.A, f.Id);
            }
            void Edge(int a, int b, int id)
            {
                ulong key = ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                if (!edges.TryGetValue(key, out var list)) edges[key] = list = new List<int>();
                list.Add(id);
            }
            var adjacent = new Dictionary<int, List<int>>();
            foreach (int id in candidates.Keys) adjacent.Add(id, new List<int>());
            foreach (var edge in edges.Values)
                for (int i = 0; i < edge.Count; i++) for (int j = i + 1; j < edge.Count; j++)
                {
                    adjacent[edge[i]].Add(edge[j]); adjacent[edge[j]].Add(edge[i]);
                }
            var visited = new HashSet<int>();
            foreach (var pair in candidates)
            {
                if (!visited.Add(pair.Key)) continue;
                var region = new Region { Id = pair.Key };
                var queue = new Queue<int>(); queue.Enqueue(pair.Key);
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                while (queue.Count > 0)
                {
                    int id = queue.Dequeue(); region.Id = Math.Min(region.Id, id);
                    var f = candidates[id];
                    var center = (surface.Positions[f.A] + surface.Positions[f.B] + surface.Positions[f.C]) / 3;
                    var outward = Vector3.ProjectOnPlane(f.Normal, Vector3.up).normalized;
                    var crest = Trace(surface, center, -outward, true);
                    var toe = Trace(surface, center, outward, false);
                    var profile = new Profile(crest, toe, outward);
                    if (profile.Rise > 0.6f)
                    {
                        region.Profiles.Add(profile);
                        region.WallTriangles.Add(new[] { XZ(surface.Positions[f.A]), XZ(surface.Positions[f.B]), XZ(surface.Positions[f.C]) });
                        region.MaximumRise = Mathf.Max(region.MaximumRise, profile.Rise);
                        min = Vector2.Min(min, Vector2.Min(XZ(crest), XZ(toe)));
                        max = Vector2.Max(max, Vector2.Max(XZ(crest), XZ(toe)));
                    }
                    foreach (int next in adjacent[id]) if (visited.Add(next)) queue.Enqueue(next);
                }
                if (region.Profiles.Count == 0) continue;
                region.Bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y); Regions.Add(region);
            }
        }
        private static Vector3 Trace(TerrainSiteData surface, Vector3 start, Vector3 direction, bool uphill)
        {
            const float step = 0.2f;
            var previous = start; var edge = start; int gentleSteps = 0;
            for (int i = 1; i <= 80; i++)
            {
                var next = start + direction * (step * i);
                if (!surface.TryHeight(XZ(next), out float height)) break;
                next.y = height;
                float rise = (next.y - previous.y) * (uphill ? 1 : -1);
                if (rise < -0.02f) break; // Never cross a valley into the next hill.
                if (rise / step < 0.47f) gentleSteps++; else gentleSteps = 0;
                if (gentleSteps >= 2) break;
                if (gentleSteps == 0) edge = next;
                previous = next;
            }
            return edge;
        }
        public static bool TrySample(Region region, Vector2 point, out Sample sample)
        {
            float best = float.MaxValue; Profile nearest = default;
            foreach (var p in region.Profiles)
            {
                var segment = XZ(p.Toe - p.Crest);
                float t = segment.sqrMagnitude < 0.0001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - XZ(p.Crest), segment) / segment.sqrMagnitude);
                float distance = (point - (XZ(p.Crest) + segment * t)).sqrMagnitude;
                if (distance < best) { best = distance; nearest = p; }
            }
            float wallDistance = region.DistanceToWall(point);
            if (wallDistance > SlopeRockCover.OutsideReach) { sample = default; return false; }
            // Blend neighboring profiles facing the same way, smoothing triangle boundaries and corners.
            Vector3 crest = Vector3.zero, toe = Vector3.zero, normal = Vector3.zero; float sum = 0;
            foreach (var p in region.Profiles)
            {
                if (Vector3.Dot(p.Outward, nearest.Outward) < 0.65f) continue;
                var segment = XZ(p.Toe - p.Crest);
                float t = segment.sqrMagnitude < 0.0001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - XZ(p.Crest), segment) / segment.sqrMagnitude);
                float distance = (point - (XZ(p.Crest) + segment * t)).sqrMagnitude;
                if (distance > best + 1) continue;
                float weight = 1 / (0.08f + distance * distance);
                crest += p.Crest * weight; toe += p.Toe * weight; normal += p.Outward * weight; sum += weight;
            }
            sample = new Sample(crest / sum, toe / sum, normal.normalized, wallDistance); return true;
        }
    }
}
