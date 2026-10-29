using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Independent surface heights; never changes the building grid's level vertex planes.</summary>
    public sealed class TerrainSiteData
    {
        public HexGrid Grid { get; }
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<int> Triangles = new List<int>();
        public readonly HashSet<Vertex> PlatformVertices = new HashSet<Vertex>();
        public readonly HashSet<Quad> PlatformQuads = new HashSet<Quad>();
        public readonly Dictionary<Vertex, int> Indices = new Dictionary<Vertex, int>();
        public readonly List<CliffSegment> Cliffs = new List<CliffSegment>();
        public readonly Dictionary<Quad, int[]> QuadSurfaceIndices = new Dictionary<Quad, int[]>();
        private float[] heightOffsets;
        public readonly struct CliffSegment
        {
            public readonly Vector3 TopA, TopB, BottomA, BottomB, NormalA, NormalB;
            public readonly float CornerSpanA, CornerSpanB;
            public CliffSegment(Vector3 topA, Vector3 topB, Vector3 bottomA, Vector3 bottomB, Vector3 normalA, Vector3 normalB,
                float cornerSpanA = 0, float cornerSpanB = 0)
            { TopA = topA; TopB = topB; BottomA = bottomA; BottomB = bottomB; NormalA = normalA; NormalB = normalB;
                CornerSpanA = cornerSpanA; CornerSpanB = cornerSpanB; }
        }
        public Rect Footprint { get; private set; }
        public float PlatformHeight { get; private set; }
        public float CliffRise { get; private set; }
        public float CutVolume { get; private set; }
        public float FillVolume { get; private set; }
        public bool Graded { get; private set; }
        public int RockShapeSeed { get; private set; } = 42;

        public TerrainSiteData(HexGrid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            foreach (var vertex in grid.baseLayer.subVertices)
            {
                Indices.Add(vertex, Positions.Count);
                Positions.Add(vertex.position);
            }
            foreach (var quad in grid.baseLayer.subQuads)
            {
                AddTriangle(Indices[quad.a], Indices[quad.b], Indices[quad.c]);
                AddTriangle(Indices[quad.a], Indices[quad.c], Indices[quad.d]);
            }
            heightOffsets = new float[Indices.Count];
        }

        public float[] CopyHeightOffsets() => (float[])heightOffsets.Clone();
        public void RestoreHeightOffsets(float[] values)
        {
            if (values == null || values.Length != heightOffsets.Length) throw new ArgumentException("Height data does not match this polygon grid.");
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value) > 12) throw new ArgumentException("Invalid terrain height.");
            Array.Copy(values, heightOffsets, values.Length);
        }
        public void AdjustQuadHeight(Quad quad, float delta)
        {
            if (!Grid.baseLayer.subQuads.Contains(quad)) throw new ArgumentException("Polygon is not part of this terrain.");
            foreach (var v in quad.vertices) heightOffsets[Indices[v]] = Mathf.Clamp(heightOffsets[Indices[v]] + delta, -12, 12);
        }
        private float EditedHeight(Vertex v) => NaturalHeight(v.position) + heightOffsets[Indices[v]];

        public Quad FindQuad(Vector2 p)
        {
            foreach (var pair in QuadSurfaceIndices)
            {
                var a = Positions[pair.Value[0]]; var b = Positions[pair.Value[1]];
                var c = Positions[pair.Value[2]]; var d = Positions[pair.Value[3]];
                if (ContainsXZ(a, b, c, p) || ContainsXZ(a, c, d, p)) return pair.Key;
            }
            return null;
        }
        private static bool ContainsXZ(Vector3 a, Vector3 b, Vector3 c, Vector2 p)
        {
            float den = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(den) < 1e-7f) return false;
            float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / den;
            float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / den;
            return u >= -1e-5f && v >= -1e-5f && u + v <= 1.00001f;
        }

        private void AddTriangle(int a, int b, int c)
        {
            if (Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]).y < 0)
            { int swap = b; b = c; c = swap; }
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
        }

        public static float NaturalHeight(Vector3 p) => 0.15f * p.x + 0.07f * p.z
            + 0.55f * Mathf.Sin(p.x * 0.25f) * Mathf.Cos(p.z * 0.21f);

        public void UpdateSite(Vector2 center, float offset, bool graded, float cliffHeight = 0, int rockShapeSeed = 42)
        {
            RockShapeSeed = rockShapeSeed;
            Footprint = new Rect(center.x - 3.6f, center.y - 2.8f, 7.2f, 5.6f);
            PlatformVertices.Clear(); PlatformQuads.Clear(); Cliffs.Clear(); QuadSurfaceIndices.Clear();
            if (graded)
                foreach (var quad in Grid.baseLayer.subQuads)
                {
                    bool touched = false;
                    foreach (var vertex in quad.vertices) touched |= Inside(vertex.position);
                    if (!touched) continue;
                    PlatformQuads.Add(quad);
                    foreach (var vertex in quad.vertices) PlatformVertices.Add(vertex);
                }
            float total = 0;
            foreach (var v in PlatformVertices) total += EditedHeight(v);
            PlatformHeight = total / Mathf.Max(1, PlatformVertices.Count) + offset + (graded ? Mathf.Max(0, cliffHeight) : 0);
            CliffRise = 0; Graded = graded;
            Triangles.Clear();
            if (Positions.Count > Indices.Count) Positions.RemoveRange(Indices.Count, Positions.Count - Indices.Count);
            foreach (var vertex in Grid.baseLayer.subVertices)
            {
                var p = vertex.position; float influence = 0;
                if (graded)
                {
                    if (PlatformVertices.Contains(vertex)) influence = 1;
                    else
                    {
                        float distance = float.PositiveInfinity;
                        foreach (var pad in PlatformVertices)
                            distance = Mathf.Min(distance, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pad.x, pad.z)));
                        influence = 1 - Mathf.SmoothStep(0, 1, distance / 2.4f);
                    }
                }
                p.y = Mathf.Lerp(EditedHeight(vertex), PlatformHeight, influence);
                Positions[Indices[vertex]] = p;
            }
            foreach (var quad in Grid.baseLayer.subQuads)
            {
                int a = Indices[quad.a], b = Indices[quad.b], c = Indices[quad.c], d = Indices[quad.d];
                QuadSurfaceIndices.Add(quad, new[] { a, b, c, d });
                AddTriangle(a, b, c); AddTriangle(a, c, d);
            }
            CutVolume = FillVolume = 0;
            for (int i = 0; i < Triangles.Count; i += 3)
            {
                var a = Positions[Triangles[i]]; var b = Positions[Triangles[i + 1]]; var c = Positions[Triangles[i + 2]];
                float area = Mathf.Abs((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x)) * 0.5f;
                float delta = (a.y - NaturalHeight(a) + b.y - NaturalHeight(b) + c.y - NaturalHeight(c)) / 3;
                if (delta > 0) FillVolume += delta * area; else CutVolume -= delta * area;
            }
        }
        private int SurfaceIndex(Vertex vertex, bool upper, Dictionary<Vertex, int> lowerIndices)
            => !upper && lowerIndices.TryGetValue(vertex, out int lower) ? lower : Indices[vertex];

        private static Vector3 EdgeOutward(Edge edge, Quad quad)
        {
            var tangent = edge.b.position - edge.a.position;
            var normal = new Vector3(-tangent.z, 0, tangent.x).normalized;
            if (Vector3.Dot(normal, (edge.a.position + edge.b.position) * 0.5f - quad.center) < 0) normal = -normal;
            return normal;
        }

        public bool Inside(Vector3 p) => Footprint.Contains(new Vector2(p.x, p.z));

        public bool TryHeight(Vector2 p, out float height)
        {
            // Queries use the rendered triangle planes, not bilinear interpolation or nearest vertices.
            for (int i = 0; i < Triangles.Count; i += 3)
            {
                var a = Positions[Triangles[i]]; var b = Positions[Triangles[i + 1]]; var c = Positions[Triangles[i + 2]];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < 1e-7f) continue;
                float u = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.y - c.z)) / denominator;
                float v = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.y - c.z)) / denominator;
                if (u < -1e-5f || v < -1e-5f || u + v > 1.00001f) continue;
                height = u * a.y + v * b.y + (1 - u - v) * c.y; return true;
            }
            height = 0; return false;
        }
    }
}

