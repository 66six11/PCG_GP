using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Closed, planar fracture blocks. No radial sphere/pillar inflation.</summary>
    public static class RockModelFactory
    {
        public const int Version = 3;
        public static readonly string[] Names = { "CliffBlock_Tall", "CliffBlock_Slab", "CliffBlock_Split", "CliffBlock_Toe",
            "CliffShard_Long", "CliffShard_Leaning" };

        public static void Build(Mesh mesh, int variant)
        {
            variant = ((variant % Names.Length) + Names.Length) % Names.Length;
            var extents = new[] { new Vector3(0.5f, 0.8f, 0.38f), new Vector3(0.7f, 0.25f, 0.45f),
                new Vector3(0.48f, 0.58f, 0.4f), new Vector3(0.5f, 0.38f, 0.46f),
                new Vector3(0.22f, 1.2f, 0.20f), new Vector3(0.28f, 1.0f, 0.16f) }[variant];
            var faces = Box(extents);
            // Convex clipping creates broad fracture planes and narrow bevels with watertight caps.
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var n = new Vector3(x, 0, z).normalized;
                    float bevel = (0.08f + 0.025f * ((variant + x + z + 4) % 3));
                    Clip(faces, n, (extents.x + extents.z) * 0.70710678f - bevel);
                }
            Clip(faces, new Vector3(0.35f, 1, -0.24f).normalized,
                (extents.y + extents.x * 0.12f) / new Vector3(0.35f, 1, -0.24f).magnitude);
            Clip(faces, new Vector3(-0.65f, 0.8f, 0.5f).normalized,
                (extents.y * 0.8f + extents.x * 0.65f + extents.z * 0.5f) /
                new Vector3(-0.65f, 0.8f, 0.5f).magnitude - (variant == 2 ? 0.26f : 0.12f));
            Clip(faces, new Vector3(0.2f, -1, 0.15f).normalized,
                (extents.y + 0.01f) / new Vector3(0.2f, -1, 0.15f).magnitude);
            var front = new Vector3(variant % 2 == 0 ? 0.42f : -0.31f, 0.16f, 1);
            Clip(faces, front.normalized, extents.z * 0.86f / front.magnitude);
            var fracture = new Vector3(-0.7f, -0.28f, 1);
            Clip(faces, fracture.normalized, (extents.z + extents.x * 0.32f) / fracture.magnitude);
            // Weld across all fracture polygons before triangulation, so nearly coincident plane
            // intersections collapse consistently on both sides of an edge (1 mm authoring tolerance).
            var canonical = new List<Vector3>();
            foreach (var face in faces)
            {
                for (int i = 0; i < face.Count; i++)
                {
                    var p = face[i]; int found = canonical.FindIndex(q => (q - p).sqrMagnitude < 1e-6f);
                    if (found < 0) { canonical.Add(p); found = canonical.Count - 1; }
                    face[i] = canonical[found];
                }
                for (int i = face.Count - 1; i >= 0 && face.Count > 1; i--)
                    if (face[i] == face[(i + 1) % face.Count]) face.RemoveAt(i);
            }
            var vertices = new List<Vector3>(); var indices = new List<int>(); var uv = new List<Vector2>();
            foreach (var face in faces)
            {
                if (face.Count < 3) continue;
                var center = Vector3.zero; foreach (var p in face) center += p; center /= face.Count;
                var normal = Vector3.Cross(face[1] - face[0], center - face[0]).normalized;
                for (int i = 0; i < face.Count; i++)
                {
                    int first = vertices.Count;
                    vertices.Add(center); vertices.Add(face[i]); vertices.Add(face[(i + 1) % face.Count]);
                    for (int j = first; j < first + 3; j++)
                    {
                        var p = vertices[j];
                        uv.Add(Mathf.Abs(normal.y) > 0.7f ? new Vector2(p.x, p.z)
                            : Mathf.Abs(normal.x) > 0.7f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y));
                    }
                    indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
                }
            }
            mesh.Clear(); mesh.name = Names[variant]; mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
            mesh.SetUVs(0, uv); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }

        private static List<List<Vector3>> Box(Vector3 e)
        {
            var result = new List<List<Vector3>>();
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    var face = new List<Vector3>();
                    foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                    {
                        var p = Vector3.zero; p[axis] = e[axis] * sign; p[u] = e[u] * corner.x; p[v] = e[v] * corner.y;
                        face.Add(p);
                    }
                    if (sign < 0) face.Reverse(); result.Add(face);
                }
            return result;
        }

        private static void Clip(List<List<Vector3>> faces, Vector3 normal, float distance)
        {
            var cap = new List<Vector3>();
            bool removedOutside = false;
            for (int f = faces.Count - 1; f >= 0; f--)
            {
                var input = faces[f]; var output = new List<Vector3>();
                for (int i = 0; i < input.Count; i++)
                {
                    var a = input[i]; var b = input[(i + 1) % input.Count];
                    float da = Vector3.Dot(normal, a) - distance, db = Vector3.Dot(normal, b) - distance;
                    if (Mathf.Abs(da) < 1e-5f) da = 0;
                    if (Mathf.Abs(db) < 1e-5f) db = 0;
                    removedOutside |= da > 0;
                    if (da == 0 && !cap.Exists(q => (q - a).sqrMagnitude < 1e-10f)) cap.Add(a);
                    if (da <= 0) output.Add(a);
                    if ((da < 0 && db > 0) || (da > 0 && db < 0))
                    {
                        var p = Vector3.LerpUnclamped(a, b, da / (da - db)); output.Add(p);
                        if (!cap.Exists(q => (q - p).sqrMagnitude < 1e-10f)) cap.Add(p);
                    }
                }
                for (int i = output.Count - 1; i >= 0 && output.Count > 1; i--)
                    if ((output[i] - output[(i + 1) % output.Count]).sqrMagnitude < 1e-10f) output.RemoveAt(i);
                if (output.Count < 3) faces.RemoveAt(f); else faces[f] = output;
            }
            if (!removedOutside || cap.Count < 3) return;
            var center = Vector3.zero; foreach (var p in cap) center += p; center /= cap.Count;
            var right = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            var up = Vector3.Cross(normal, right);
            cap.Sort((a, b) => Mathf.Atan2(Vector3.Dot(a - center, up), Vector3.Dot(a - center, right))
                .CompareTo(Mathf.Atan2(Vector3.Dot(b - center, up), Vector3.Dot(b - center, right))));
            faces.Add(cap);
        }
    }
}
