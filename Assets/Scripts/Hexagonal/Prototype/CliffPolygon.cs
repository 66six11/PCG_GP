using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Small boundary polygons, triangulated without propagating another corner's height bands.</summary>
    public static class CliffPolygon
    {
        public static void Clean(List<Vector3> points)
        {
            bool changed = true;
            while (changed && points.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < points.Count; i++)
                {
                    var a = points[(i + points.Count - 1) % points.Count];
                    var b = points[i]; var c = points[(i + 1) % points.Count];
                    var ab = b - a; var bc = c - b;
                    if (ab.sqrMagnitude < 1e-10f || bc.sqrMagnitude < 1e-10f ||
                        (Vector3.Cross(ab, bc).sqrMagnitude < 1e-10f * ab.sqrMagnitude * bc.sqrMagnitude
                            && Vector3.Dot(ab, bc) >= 0))
                    { points.RemoveAt(i); changed = true; break; }
                }
            }
        }

        public static List<int> Triangulate(List<Vector3> points, Vector3 facing)
        {
            Clean(points); var result = new List<int>();
            if (points.Count < 3) return result;
            var normal = Vector3.zero;
            for (int i = 0; i < points.Count; i++) normal += Vector3.Cross(points[i], points[(i + 1) % points.Count]);
            if (normal.sqrMagnitude < 1e-12f) return result;
            if (Vector3.Dot(normal, facing) < 0) { points.Reverse(); normal = -normal; }
            normal.Normalize();
            var remaining = new List<int>(); for (int i = 0; i < points.Count; i++) remaining.Add(i);
            while (remaining.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Side(points[a], points[b], points[c], normal) <= 1e-8f) continue;
                    bool occupied = false;
                    foreach (int v in remaining)
                    {
                        if (v == a || v == b || v == c) continue;
                        if (Side(points[a], points[b], points[v], normal) >= -1e-8f &&
                            Side(points[b], points[c], points[v], normal) >= -1e-8f &&
                            Side(points[c], points[a], points[v], normal) >= -1e-8f)
                        { occupied = true; break; }
                    }
                    if (occupied) continue;
                    result.Add(a); result.Add(b); result.Add(c); remaining.RemoveAt(i); found = true; break;
                }
                if (!found) throw new InvalidOperationException("Invalid cliff boundary polygon; cannot triangulate without overlap.");
            }
            result.Add(remaining[0]); result.Add(remaining[1]); result.Add(remaining[2]); return result;
        }

        private static float Side(Vector3 a, Vector3 b, Vector3 c, Vector3 n) => Vector3.Dot(Vector3.Cross(b - a, c - a), n);
    }
}
