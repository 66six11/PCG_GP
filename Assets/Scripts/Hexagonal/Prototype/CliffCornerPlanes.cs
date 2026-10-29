using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Analytic intersection of a vertical corner wedge with one seeded oblique plane.</summary>
    public static class CliffCornerPlanes
    {
        private readonly struct Cut
        {
            public readonly Vector3 normal;
            public readonly float constant, rootT;
            public readonly bool enabled;
            public Cut(Vector3 top, Vector3 bottom, Vector3 outward, Vector3 intoFace, float span, int seed)
            {
                float turn = -Vector3.Dot(intoFace, outward);
                float r = Hash(top, seed + 1907);
                rootT = Mathf.Lerp(0.09f, 0.55f, Mathf.Pow(Hash(top, seed + 2903), 1.35f));
                enabled = turn > 0.025f && r > 0.08f;
                // Actual top-edge chips, not a pinch that returns to zero at the top.
                // Bound inward travel to keep the clipped outer rim beyond the foundation.
                float width = enabled ? Mathf.Min(0.30f, span * 0.30f * Mathf.Max(0, turn))
                    * Mathf.Lerp(0.65f, 1, Hash(top, seed + 3907)) : 0;
                float yaw = Mathf.Asin(Mathf.Clamp01(Mathf.Max(0, turn))) * Mathf.Rad2Deg
                    * (Hash(top, seed + 4903) * 2 - 1) * 0.20f;
                var horizontal = Quaternion.AngleAxis(yaw, Vector3.up) * outward;
                var root = Vector3.Lerp(top, bottom, rootT);
                float vertical = Mathf.Max(0.001f, top.y - root.y);
                normal = horizontal + Vector3.up * ((width - Vector3.Dot(horizontal, top - root)) / vertical);
                constant = Vector3.Dot(normal, root);
            }
            public float Offset(Vector3 p) => enabled ? Mathf.Max(0, Vector3.Dot(normal, p) - constant) : 0;
        }

        public readonly struct Slice
        {
            private readonly Vector3 cornerA, trimA, trimB, cornerB;
            public readonly float cutA, cutB;
            public Slice(Vector3 a, Vector3 ta, Vector3 tb, Vector3 b, float ua, float ub)
            { cornerA = a; trimA = ta; trimB = tb; cornerB = b; cutA = ua; cutB = ub; }
            public Vector3 Sample(float u)
            {
                if (u < cutA) return Vector3.Lerp(cornerA, trimA, u / cutA);
                if (u > 1 - cutB) return Vector3.Lerp(trimB, cornerB, (u - (1 - cutB)) / cutB);
                return Vector3.Lerp(trimA, trimB, (u - cutA) / (1 - cutA - cutB));
            }
        }

        private static void Plans(TerrainSiteData.CliffSegment segment, int seed, out Cut a, out Cut b)
        {
            var direction = Vector3.ProjectOnPlane(segment.TopB - segment.TopA, Vector3.up);
            float length = direction.magnitude; direction /= Mathf.Max(0.001f, length);
            a = new Cut(segment.TopA, segment.BottomA, segment.NormalA, direction,
                segment.CornerSpanA > 0 ? segment.CornerSpanA : length, seed);
            b = new Cut(segment.TopB, segment.BottomB, segment.NormalB, -direction,
                segment.CornerSpanB > 0 ? segment.CornerSpanB : length, seed);
        }

        public static List<float> Depths(IReadOnlyList<TerrainSiteData.CliffSegment> segments, int seed)
        {
            var result = new List<float> { 0, 1 };
            foreach (var segment in segments)
            {
                Plans(segment, seed, out var a, out var b);
                if (a.enabled) AddDepth(result, a.rootT);
                if (b.enabled) AddDepth(result, b.rootT);
            }
            result.Sort(); return result;
        }
        private static void AddDepth(List<float> values, float value)
        { foreach (float old in values) if (Mathf.Abs(old - value) < 1e-6f) return; values.Add(value); }

        public static Slice CrossSection(TerrainSiteData.CliffSegment segment, float t, int seed)
        {
            t = Mathf.Clamp01(t);
            // A plain face meets the platform directly. Only active corner planes create local facets;
            // there is no continuous shoulder or toe bevel wrapped around the entire platform.
            float lateral = t;
            var a = Vector3.Lerp(segment.TopA, segment.BottomA, lateral); a.y = Mathf.Lerp(segment.TopA.y, segment.BottomA.y, t);
            var b = Vector3.Lerp(segment.TopB, segment.BottomB, lateral); b.y = Mathf.Lerp(segment.TopB.y, segment.BottomB.y, t);
            Plans(segment, seed, out var planeA, out var planeB);
            var delta = b - a;
            float offsetA = planeA.Offset(a), offsetB = planeB.Offset(b);
            // Solve the line/plane equation, including height variation along the face.
            float denominatorA = -Vector3.Dot(planeA.normal, delta);
            float denominatorB = Vector3.Dot(planeB.normal, delta);
            float cutA = offsetA > 0 && denominatorA > 1e-6f ? offsetA / denominatorA : 0;
            float cutB = offsetB > 0 && denominatorB > 1e-6f ? offsetB / denominatorB : 0;
            var cornerA = a - segment.NormalA * (offsetA / Mathf.Max(0.1f, Vector3.Dot(planeA.normal, segment.NormalA)));
            var cornerB = b - segment.NormalB * (offsetB / Mathf.Max(0.1f, Vector3.Dot(planeB.normal, segment.NormalB)));
            return new Slice(cornerA, Vector3.Lerp(a, b, cutA), Vector3.Lerp(a, b, 1 - cutB), cornerB, cutA, cutB);
        }

        private static float Hash(Vector3 p, int seed)
        {
            unchecked
            {
                uint h = (uint)(Mathf.RoundToInt(p.x * 1000) * 73856093) ^ (uint)(Mathf.RoundToInt(p.z * 1000) * 19349663) ^ (uint)seed * 83492791u;
                h ^= h >> 16; h *= 2246822519u; h ^= h >> 13;
                return (h & 0x00ffffff) / 16777215f;
            }
        }
    }
}
