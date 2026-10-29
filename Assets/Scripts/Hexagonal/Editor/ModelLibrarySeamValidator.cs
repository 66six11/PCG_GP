#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MarchingCube;
using UnityEngine;

namespace Editor
{
    /// <summary>Compare surface contours on the six shared planes, in a common face coordinate frame.</summary>
    public static class ModelLibrarySeamValidator
    {
        private const int Scale = 10000;
        private static readonly int[,] FaceCorners = {
            {1,2,5,6}, {0,3,4,7}, {0,1,2,3}, {4,5,6,7}, {0,1,4,5}, {3,2,7,6} };

        private readonly struct Segment : IEquatable<Segment>
        {
            public readonly Vector2Int A, B;
            public Segment(Vector2Int a, Vector2Int b)
            {
                bool sorted = a.x < b.x || (a.x == b.x && a.y <= b.y);
                A = sorted ? a : b; B = sorted ? b : a;
            }
            public bool Equals(Segment other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is Segment other && Equals(other);
            public override int GetHashCode() => unchecked(A.GetHashCode() * 397 ^ B.GetHashCode());
            public override string ToString() => $"{A.x},{A.y}:{B.x},{B.y}";
        }

        public static List<string> Validate(ModelLibrary library)
        {
            var errors = ModelLibraryBinding.Validate(library);
            if (errors.Count != 0) return errors;
            var reference = new Dictionary<(int axis, int mask), (string contour, byte state, int face)>();
            foreach (var model in library.Meshes)
            {
                Vector3Int[] points;
                int[] triangles;
                if (model.mesh == null) { points = Array.Empty<Vector3Int>(); triangles = Array.Empty<int>(); }
                else
                {
                    points = model.mesh.vertices.Select(vertex =>
                    {
                        if (model.mirrorX) vertex.x = -vertex.x;
                        return Vector3Int.RoundToInt(model.rotation * vertex * Scale);
                    }).ToArray();
                    triangles = model.mesh.triangles;
                }
                for (int face = 0; face < 6; face++)
                {
                    int mask = 0;
                    for (int i = 0; i < 4; i++)
                        if ((model.stateCode & (1 << FaceCorners[face, i])) != 0) mask |= 1 << i;
                    string contour = GetContour(points, triangles, face);
                    var key = (face / 2, mask);
                    if (!reference.TryGetValue(key, out var first)) reference.Add(key, (contour, model.stateCode, face));
                    else if (first.contour != contour)
                        errors.Add($"Face contour mismatch: state {model.stateCode}, face {face}, mask {mask:X1}; reference state {first.state}, face {first.face}.");
                }
            }
            return errors;
        }

        private static string GetContour(Vector3Int[] points, int[] triangles, int face)
        {
            int axis = face / 2, plane = (face % 2 == 0 ? 1 : -1) * Scale / 2;
            var coplanar = new Dictionary<Segment, int>();
            var contour = new HashSet<Segment>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = points[triangles[i]]; var b = points[triangles[i + 1]]; var c = points[triangles[i + 2]];
                bool all = a[axis] == plane && b[axis] == plane && c[axis] == plane;
                Add(a,b); Add(b,c); Add(c,a);
                void Add(Vector3Int p, Vector3Int q)
                {
                    if (p[axis] != plane || q[axis] != plane) return;
                    var edge = new Segment(Project(p, axis), Project(q, axis));
                    if (edge.A == edge.B) return;
                    if (!all) contour.Add(edge);
                    else { coplanar.TryGetValue(edge, out int count); coplanar[edge] = count + 1; }
                }
            }
            foreach (var pair in coplanar) if (pair.Value == 1) contour.Add(pair.Key);
            // Ignore harmless subdivisions of an otherwise identical straight boundary segment.
            var segments = contour.ToList();
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < segments.Count && !merged; i++)
                    for (int j = i + 1; j < segments.Count; j++)
                    {
                        var first = segments[i]; var second = segments[j];
                        Vector2Int common, a, b;
                        if (first.A == second.A) { common = first.A; a = first.B; b = second.B; }
                        else if (first.A == second.B) { common = first.A; a = first.B; b = second.A; }
                        else if (first.B == second.A) { common = first.B; a = first.A; b = second.B; }
                        else if (first.B == second.B) { common = first.B; a = first.A; b = second.A; }
                        else continue;
                        var u = a - common; var v = b - common;
                        if ((long)u.x * v.y != (long)u.y * v.x || (long)u.x * v.x + (long)u.y * v.y >= 0) continue;
                        // A branch junction must not be collapsed.
                        if (segments.Count(s => s.A == common || s.B == common) != 2) continue;
                        segments[i] = new Segment(a, b); segments.RemoveAt(j); merged = true; break;
                    }
            } while (merged);
            return string.Join(";", segments.Select(s => s.ToString()).OrderBy(s => s, StringComparer.Ordinal));
        }

        private static Vector2Int Project(Vector3Int point, int axis)
        {
            if (axis == 0) return new Vector2Int(point.y, point.z);
            if (axis == 1) return new Vector2Int(point.x, point.z);
            return new Vector2Int(point.x, point.y);
        }
    }
}
#endif
