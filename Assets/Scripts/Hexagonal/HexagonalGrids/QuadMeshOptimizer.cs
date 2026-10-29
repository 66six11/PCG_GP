using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    /// <summary>Fixed-topology XZ optimization. Trials are synchronous and published only after validation.</summary>
    public sealed class QuadMeshOptimizer
    {
        [Serializable]
        public sealed class Settings
        {
            [Min(0)] public float areaWeight = 8;
            [Min(0)] public float edgeWeight = 0.2f;
            [Min(0)] public float angleWeight = 0.3f;
            [Min(0)] public float totalAreaWeight = 20;
            [Min(0.001f)] public float step = 0.25f;
        }
        public sealed class Result
        {
            public int AcceptedSteps, Backtracks;
            public double InitialEnergy, FinalEnergy, TargetArea, InitialAreaCv, FinalAreaCv;
            public double MinAreaRatio, MaxAreaRatio, AreaChangePercent;
        }
        private readonly struct Segment
        {
            public readonly int A, B;
            public Segment(int a, int b) { A = a; B = b; }
        }
        private Vector2[] points, trial, gradient, direction;
        private int[][] faces;
        private double[] orientation;
        private int[] degree;
        private bool[] pinned;
        private Segment[] edges, boundary;
        private double targetArea, targetLength, originalArea;
        private Settings settings;

        public Result Optimize(GridLayer layer, int iterations, bool preserveBoundary = false, Settings options = null)
        {
            if (layer == null) throw new ArgumentNullException(nameof(layer));
            if (iterations < 0) throw new ArgumentOutOfRangeException(nameof(iterations));
            settings = options ?? new Settings(); ValidateSettings();
            var result = new Result();
            if (layer.subQuads.Count == 0) return result;
            int count = layer.subVertices.Count;
            points = new Vector2[count]; trial = new Vector2[count]; gradient = new Vector2[count]; direction = new Vector2[count];
            degree = new int[count]; pinned = new bool[count];
            var indices = new Dictionary<Vertex, int>();
            for (int i = 0; i < count; i++)
            {
                var p = layer.subVertices[i].position;
                if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z)) throw new ArgumentException("Mesh positions must be finite.");
                points[i] = new Vector2(p.x, p.z); indices.Add(layer.subVertices[i], i);
            }
            if (preserveBoundary) foreach (var vertex in layer.boundaryVertices) pinned[indices[vertex]] = true;
            faces = new int[layer.subQuads.Count][]; orientation = new double[faces.Length]; originalArea = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                var face = new int[4];
                for (int j = 0; j < 4; j++) { face[j] = indices[layer.subQuads[i].vertices[j]]; degree[face[j]]++; }
                faces[i] = face; double area = SignedArea(points, face);
                orientation[i] = area < 0 ? -1 : 1; originalArea += Math.Abs(area);
            }
            targetArea = originalArea / faces.Length; targetLength = Math.Sqrt(targetArea);
            if (!(targetArea > 0) || double.IsInfinity(targetArea)) throw new ArgumentException("Mesh area must be finite and positive.");
            edges = Segments(layer.subEdges, indices); boundary = Segments(layer.boundaryEdges, indices);
            if (!Valid(points)) throw new ArgumentException("Balanced relaxation requires a convex, non-intersecting initial mesh.");
            result.TargetArea = targetArea; result.InitialAreaCv = AreaCv(points);
            double energy = Energy(points, true); result.InitialEnergy = energy;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                if (iteration != 0) energy = Energy(points, true);
                double directionalDerivative = 0;
                for (int i = 0; i < count; i++)
                {
                    var delta = pinned[i] ? Vector2.zero : -gradient[i] * (float)(targetArea / Math.Max(1, degree[i]));
                    float maximum = (float)(targetLength * 0.15);
                    if (delta.magnitude > maximum) delta = delta.normalized * maximum;
                    direction[i] = delta; directionalDerivative += Vector2.Dot(gradient[i], delta);
                }
                if (!(directionalDerivative < -1e-12)) break;
                double step = settings.step; bool accepted = false;
                for (int search = 0; search < 24; search++, step *= 0.5)
                {
                    bool moved = false;
                    for (int i = 0; i < count; i++)
                    { trial[i] = points[i] + direction[i] * (float)step; moved |= trial[i] != points[i]; }
                    if (!moved) break;
                    double next = Energy(trial, false);
                    if (next <= energy + 0.0001 * step * directionalDerivative && Valid(trial))
                    {
                        var swap = points; points = trial; trial = swap; energy = next;
                        result.AcceptedSteps++; accepted = true; break;
                    }
                    result.Backtracks++;
                }
                if (!accepted) break;
            }
            result.FinalEnergy = energy; result.FinalAreaCv = AreaCv(points);
            double total = 0, minimum = double.PositiveInfinity, maximumArea = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                double area = SignedArea(points, faces[i]) * orientation[i]; total += area;
                minimum = Math.Min(minimum, area); maximumArea = Math.Max(maximumArea, area);
            }
            result.MinAreaRatio = minimum / targetArea; result.MaxAreaRatio = maximumArea / targetArea;
            result.AreaChangePercent = (total / originalArea - 1) * 100;
            // Preserve original Y values; this optimizer owns only the planar topology positions.
            for (int i = 0; i < count; i++)
            { var p = layer.subVertices[i].position; p.x = points[i].x; p.z = points[i].y; layer.subVertices[i].position = p; }
            return result;
        }
        private void ValidateSettings()
        {
            if (!Finite(settings.areaWeight) || settings.areaWeight < 0 || !Finite(settings.edgeWeight) || settings.edgeWeight < 0
                || !Finite(settings.angleWeight) || settings.angleWeight < 0 || !Finite(settings.totalAreaWeight) || settings.totalAreaWeight < 0
                || !Finite(settings.step) || settings.step <= 0) throw new ArgumentOutOfRangeException(nameof(settings));
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Segment[] Segments(IEnumerable<Edge> source, Dictionary<Vertex, int> indices)
        {
            var result = new List<Segment>();
            foreach (var edge in source) result.Add(new Segment(indices[edge.a], indices[edge.b]));
            return result.ToArray();
        }
        private static double Cross(Vector2 a, Vector2 b) => (double)a.x * b.y - (double)a.y * b.x;
        private static double SignedArea(Vector2[] p, int[] f)
        {
            // Translate to a corner to keep area evaluation stable far from the world origin.
            return (Cross(p[f[1]] - p[f[0]], p[f[2]] - p[f[0]]) + Cross(p[f[2]] - p[f[0]], p[f[3]] - p[f[0]])) * 0.5;
        }
        private double AreaCv(Vector2[] p)
        {
            double sum = 0, squares = 0;
            for (int i = 0; i < faces.Length; i++)
            { double area = SignedArea(p, faces[i]) * orientation[i]; sum += area; squares += area * area; }
            double mean = sum / faces.Length;
            return Math.Sqrt(Math.Max(0, squares / faces.Length - mean * mean)) / mean;
        }
        private double Energy(Vector2[] p, bool derivatives)
        {
            if (derivatives) Array.Clear(gradient, 0, gradient.Length);
            double total = 0;
            for (int i = 0; i < faces.Length; i++) total += SignedArea(p, faces[i]) * orientation[i];
            double globalError = total / originalArea - 1;
            double energy = settings.totalAreaWeight * faces.Length * globalError * globalError;
            for (int i = 0; i < faces.Length; i++)
            {
                var face = faces[i]; double area = SignedArea(p, face) * orientation[i], error = area / targetArea - 1;
                energy += settings.areaWeight * error * error;
                if (derivatives)
                {
                    double coefficient = (2 * settings.areaWeight * error + 2 * settings.totalAreaWeight * globalError) / targetArea;
                    for (int j = 0; j < 4; j++)
                    {
                        var prev = p[face[(j + 3) % 4]]; var next = p[face[(j + 1) % 4]];
                        gradient[face[j]] += new Vector2(next.y - prev.y, prev.x - next.x) * (float)(0.5 * orientation[i] * coefficient);
                    }
                }
                for (int j = 0; j < 4; j++)
                {
                    int a = face[(j + 3) % 4], b = face[j], c = face[(j + 1) % 4];
                    var u = p[a] - p[b]; var v = p[c] - p[b];
                    double uu = u.sqrMagnitude, vv = v.sqrMagnitude;
                    if (!(uu > 0 && vv > 0)) return double.PositiveInfinity;
                    double lengths = Math.Sqrt(uu * vv), cosine = Vector2.Dot(u, v) / lengths;
                    energy += settings.angleWeight * cosine * cosine;
                    if (derivatives)
                    {
                        var gu = (v / (float)lengths - u * (float)(cosine / uu)) * (float)(2 * settings.angleWeight * cosine);
                        var gv = (u / (float)lengths - v * (float)(cosine / vv)) * (float)(2 * settings.angleWeight * cosine);
                        gradient[a] += gu; gradient[c] += gv; gradient[b] -= gu + gv;
                    }
                }
            }
            foreach (var edge in edges)
            {
                var difference = p[edge.A] - p[edge.B]; double length = difference.magnitude;
                if (!(length > 0)) return double.PositiveInfinity;
                double error = length / targetLength - 1; energy += settings.edgeWeight * error * error;
                if (derivatives)
                {
                    var force = difference * (float)(2 * settings.edgeWeight * error / (targetLength * length));
                    gradient[edge.A] += force; gradient[edge.B] -= force;
                }
            }
            return energy;
        }
        private bool Valid(Vector2[] p)
        {
            for (int i = 0; i < p.Length; i++) if (!Finite(p[i].x) || !Finite(p[i].y)) return false;
            for (int i = 0; i < faces.Length; i++)
            {
                var f = faces[i];
                if (SignedArea(p, f) * orientation[i] <= targetArea * 0.01) return false;
                for (int j = 0; j < 4; j++)
                {
                    var a = p[f[j]]; var b = p[f[(j + 1) % 4]]; var c = p[f[(j + 2) % 4]];
                    if ((b - a).sqrMagnitude < targetArea * 0.0001 || Cross(b - a, c - b) * orientation[i] <= targetArea * 0.0001) return false;
                }
            }
            return !BoundaryIntersects(p);
        }
        private bool BoundaryIntersects(Vector2[] p)
        {
            // Uniform spatial buckets avoid checking every boundary edge against every other edge.
            double width = targetLength * 2, epsilon = targetArea * 1e-7;
            var buckets = new Dictionary<(int x, int y), List<int>>(); var seen = new HashSet<ulong>();
            for (int i = 0; i < boundary.Length; i++)
            {
                var edge = boundary[i]; var a = p[edge.A]; var b = p[edge.B];
                int minX = (int)Math.Floor(Math.Min(a.x, b.x) / width), maxX = (int)Math.Floor(Math.Max(a.x, b.x) / width);
                int minY = (int)Math.Floor(Math.Min(a.y, b.y) / width), maxY = (int)Math.Floor(Math.Max(a.y, b.y) / width);
                for (int x = minX; x <= maxX; x++) for (int y = minY; y <= maxY; y++)
                {
                    if (!buckets.TryGetValue((x, y), out var candidates)) { candidates = new List<int>(); buckets.Add((x, y), candidates); }
                    foreach (int other in candidates)
                    {
                        if (!seen.Add(((ulong)(uint)i << 32) | (uint)other)) continue;
                        var next = boundary[other];
                        if (edge.A == next.A || edge.A == next.B || edge.B == next.A || edge.B == next.B) continue;
                        if (Intersects(a, b, p[next.A], p[next.B], epsilon)) return true;
                    }
                    candidates.Add(i);
                }
            }
            return false;
        }
        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d, double epsilon)
        {
            double abC = Cross(b - a, c - a), abD = Cross(b - a, d - a), cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            if (((abC > epsilon && abD < -epsilon) || (abC < -epsilon && abD > epsilon))
                && ((cdA > epsilon && cdB < -epsilon) || (cdA < -epsilon && cdB > epsilon))) return true;
            return Math.Abs(abC) <= epsilon && OnSegment(a, b, c) || Math.Abs(abD) <= epsilon && OnSegment(a, b, d)
                || Math.Abs(cdA) <= epsilon && OnSegment(c, d, a) || Math.Abs(cdB) <= epsilon && OnSegment(c, d, b);
        }
        private static bool OnSegment(Vector2 a, Vector2 b, Vector2 p)
            => p.x >= Math.Min(a.x, b.x) && p.x <= Math.Max(a.x, b.x) && p.y >= Math.Min(a.y, b.y) && p.y <= Math.Max(a.y, b.y);
    }
}
