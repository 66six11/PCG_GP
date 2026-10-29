using System;
using System.Collections.Generic;
using System.Linq;
using MarchingCube;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Preserves source rotations/reflections discarded by the one-model-per-state lookup.
    /// Face sockets contain actual quantized mesh contours, not only the four occupancy bits.</summary>
    public sealed class BuildingWfcCatalog
    {
        public sealed class Variant
        {
            public ModelInfo Model;
            public double Weight;
            public string SourceId, Id;
            internal BuildingFaces Rules;
            internal readonly int[,] Sockets = new int[6, 8];
        }
        public static readonly int[,] FaceCorners = {
            {0,1,2,3}, {4,5,6,7}, {0,1,5,4}, {1,2,6,5}, {2,3,7,6}, {3,0,4,7} };
        public readonly List<Variant> Variants = new List<Variant>();
        private readonly List<int>[] states = new List<int>[256];
        private readonly int[] canonical = new int[256];
        private readonly Dictionary<string, int> socketIds = new Dictionary<string, int> { { "", 0 } };
        public int SocketCount => socketIds.Count;
        public int Canonical(byte state) => canonical[state];
        public IReadOnlyList<int> Candidates(byte state) => states[state];
        public int Socket(int variant, int face, int orientation) => Variants[variant].Sockets[face, orientation];

        public BuildingWfcCatalog(ModelLibrary library, BuildingTileset tileset = null)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            for (int i = 0; i < 256; i++) { states[i] = new List<int>(); canonical[i] = -1; }
            var seen = new HashSet<(Mesh, int, bool)>();
            for (int state = 1; state < 255; state++)
            {
                var model = library.GetModel((byte)state);
                if (!model.HasValue || model.Value.mesh == null) throw new ArgumentException($"Missing building state {state}.");
                canonical[state] = Add(model.Value, 4); seen.Add(Key(model.Value));
            }
            foreach (var source in library.originalMesh.OrderBy(m => m.stateCode))
                for (int mirror = 0; mirror < 2; mirror++)
                    for (int turns = 0; turns < 4; turns++)
                    {
                        if (source.mesh == null) continue;
                        var transform = new CubeTransform(turns, mirror != 0);
                        byte state = transform.TransformState(source.stateCode);
                        if (state == 0 || state == 255) continue;
                        var model = new ModelInfo(state, source.mesh, transform.Rotation, transform.MirrorX);
                        if (seen.Add(Key(model))) Add(model, 1);
                    }
            if (tileset != null)
            {
                var replaced = new HashSet<byte>();
                foreach (var source in tileset.variants)
                {
                    byte state = source.model.stateCode;
                    if (replaced.Add(state)) states[state].Clear();
                    int index = Add(source.model, source.weight);
                    Variants[index].SourceId = source.sourceId; Variants[index].Id = source.id; Variants[index].Rules = source.neighbors;
                }
            }
        }
        public double RuleWeight(int variant, int face, string target)
        {
            var rules = Variants[variant].Rules;
            if (rules == null) return 1;
            foreach (var rule in rules.Get(face)) if (rule.id == target) return rule.weight;
            return 0;
        }
        public bool SemanticCompatible(int a, int face, int b, int otherFace)
        {
            var left = Variants[a]; var right = Variants[b];
            double x = RuleWeight(a, face, right.SourceId ?? "$baseline"), y = RuleWeight(b, otherFace, left.SourceId ?? "$baseline");
            return x > 0 && y > 0 && (left.Rules == null || right.Rules == null || Math.Abs(x-y) < 0.00001);
        }
        public double PairWeight(int a, int face, int b, int otherFace)
            => Variants[a].Rules == null ? RuleWeight(b, otherFace, "$baseline") : RuleWeight(a, face, Variants[b].SourceId ?? "$baseline");
        public bool BoundaryAllowed(int variant, int face, string boundary) => RuleWeight(variant, face, boundary) > 0;
        private BuildingWfcCatalog()
        { for (int i = 0; i < 256; i++) { states[i] = new List<int>(); canonical[i] = -1; } }
        public static BuildingWfcCatalog ForValidation(BuildingTileVariant[] variants)
        {
            var result = new BuildingWfcCatalog();
            foreach (var source in variants) {
                int index = result.Add(source.model,source.weight);
                result.Variants[index].SourceId=source.sourceId; result.Variants[index].Id=source.id; result.Variants[index].Rules=source.neighbors;
            }
            return result;
        }
        private static (Mesh, int, bool) Key(ModelInfo model)
            => (model.mesh, (Mathf.RoundToInt(model.rotation.eulerAngles.y / 90) % 4 + 4) % 4, model.mirrorX);
        private int Add(ModelInfo model, double weight)
        {
            var variant = new Variant { Model = model, Weight = weight };
            var points = model.mesh.vertices.Select(p => {
                if (model.mirrorX) p.x = -p.x;
                return Vector3Int.RoundToInt(model.rotation * p * Precision);
            }).ToArray();
            var triangles = model.mesh.triangles;
            for (int face = 0; face < 6; face++)
            {
                var contour = Extract(points, triangles, face);
                for (int orientation = 0; orientation < 8; orientation++)
                {
                    string signature = string.Join(";", contour.Select(s => new Segment(Map(s.A, orientation), Map(s.B, orientation)).ToString())
                        .OrderBy(s => s, StringComparer.Ordinal));
                    if (!socketIds.TryGetValue(signature, out int socket))
                    { socket = socketIds.Count; socketIds.Add(signature, socket); }
                    variant.Sockets[face, orientation] = socket;
                }
            }
            int index = Variants.Count; Variants.Add(variant); states[model.stateCode].Add(index); return index;
        }
        private const int Precision = 10000;
        private readonly struct Segment : IEquatable<Segment>
        {
            public readonly Vector2Int A, B;
            public Segment(Vector2Int a, Vector2Int b)
            {
                bool forward = a.x < b.x || (a.x == b.x && a.y <= b.y);
                A = forward ? a : b; B = forward ? b : a;
            }
            public bool Equals(Segment other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is Segment other && Equals(other);
            public override int GetHashCode() => unchecked(A.GetHashCode() * 397 ^ B.GetHashCode());
            public override string ToString() => $"{A.x},{A.y}:{B.x},{B.y}";
        }
        private static readonly Vector2Int[] Corners = {
            new Vector2Int(0,0), new Vector2Int(Precision,0), new Vector2Int(Precision,Precision), new Vector2Int(0,Precision) };
        private static Vector2Int Map(Vector2Int p, int orientation)
        {
            int start = orientation / 2; bool reverse = orientation % 2 != 0;
            Vector2Int Target(int local) => Corners[(reverse ? start - local + 4 : local - start + 4) % 4];
            var a = Target(0); var u = Target(1) - a; var v = Target(3) - a;
            return new Vector2Int(a.x + (int)(((long)u.x * p.x + (long)v.x * p.y) / Precision),
                a.y + (int)(((long)u.y * p.x + (long)v.y * p.y) / Precision));
        }
        private static List<Segment> Extract(Vector3Int[] points, int[] triangles, int face)
        {
            var a = Vector3Int.RoundToInt(CubeTransform.GetCorner(FaceCorners[face, 0]) * Precision);
            var b = Vector3Int.RoundToInt(CubeTransform.GetCorner(FaceCorners[face, 1]) * Precision);
            var d = Vector3Int.RoundToInt(CubeTransform.GetCorner(FaceCorners[face, 3]) * Precision);
            int axis = face < 2 ? 1 : face == 2 || face == 4 ? 2 : 0;
            int plane = a[axis]; var u = b - a; var v = d - a;
            Vector2Int Project(Vector3Int p)
            {
                p -= a;
                return new Vector2Int((int)(((long)p.x * u.x + (long)p.y * u.y + (long)p.z * u.z) / Precision),
                    (int)(((long)p.x * v.x + (long)p.y * v.y + (long)p.z * v.z) / Precision));
            }
            var coplanar = new Dictionary<Segment, int>(); var boundary = new HashSet<Segment>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var p = points[triangles[i]]; var q = points[triangles[i + 1]]; var r = points[triangles[i + 2]];
                bool all = p[axis] == plane && q[axis] == plane && r[axis] == plane;
                Add(p, q); Add(q, r); Add(r, p);
                void Add(Vector3Int first, Vector3Int second)
                {
                    if (first[axis] != plane || second[axis] != plane) return;
                    var edge = new Segment(Project(first), Project(second)); if (edge.A == edge.B) return;
                    if (!all) boundary.Add(edge);
                    else { coplanar.TryGetValue(edge, out int count); coplanar[edge] = count + 1; }
                }
            }
            foreach (var entry in coplanar) if (entry.Value == 1) boundary.Add(entry.Key);
            var segments = boundary.ToList(); bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < segments.Count && !merged; i++)
                    for (int j = i + 1; j < segments.Count; j++)
                    {
                        var first = segments[i]; var second = segments[j]; Vector2Int common, p, q;
                        if (first.A == second.A) { common = first.A; p = first.B; q = second.B; }
                        else if (first.A == second.B) { common = first.A; p = first.B; q = second.A; }
                        else if (first.B == second.A) { common = first.B; p = first.A; q = second.B; }
                        else if (first.B == second.B) { common = first.B; p = first.A; q = second.A; }
                        else continue;
                        var du = p - common; var dv = q - common;
                        if ((long)du.x * dv.y != (long)du.y * dv.x || (long)du.x * dv.x + (long)du.y * dv.y >= 0) continue;
                        if (segments.Count(s => s.A == common || s.B == common) != 2) continue;
                        segments[i] = new Segment(p, q); segments.RemoveAt(j); merged = true; break;
                    }
            } while (merged);
            return segments;
        }
    }
}
