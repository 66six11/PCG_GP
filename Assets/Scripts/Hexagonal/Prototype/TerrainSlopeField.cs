using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Read-only analysis of the actual continuous surface, independent of buildings and decoration.</summary>
    public sealed class TerrainSlopeField
    {
        public readonly struct Sample
        {
            public readonly int Id, A, B, C;
            public readonly Vector3 Normal;
            public readonly float Degrees, Rock, Relief;
            public Sample(int id, int a, int b, int c, Vector3 normal, float degrees, float rock, float relief)
            { Id = id; A = a; B = b; C = c; Normal = normal; Degrees = degrees; Rock = rock; Relief = relief; }
        }
        public readonly List<Sample> Faces = new List<Sample>();
        public readonly Color[] Colors;
        public int RockyFaces { get; private set; }
        public static float RockWeight(float slope) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(30, 60, slope));
        public TerrainSlopeField(TerrainSiteData surface)
        {
            Colors = new Color[surface.Positions.Count];
            var sums = new float[Colors.Length]; var areas = new float[Colors.Length];
            for (int i = 0; i < surface.Triangles.Count; i += 3)
            {
                int a = surface.Triangles[i], b = surface.Triangles[i + 1], c = surface.Triangles[i + 2];
                var pa = surface.Positions[a]; var pb = surface.Positions[b]; var pc = surface.Positions[c];
                var cross = Vector3.Cross(pb - pa, pc - pa); var normal = cross.normalized;
                float degrees = Mathf.Acos(Mathf.Clamp01(normal.y)) * Mathf.Rad2Deg, rock = RockWeight(degrees);
                float relief = Mathf.Max(pa.y, pb.y, pc.y) - Mathf.Min(pa.y, pb.y, pc.y);
                Faces.Add(new Sample(i / 3, a, b, c, normal, degrees, rock, relief));
                if (degrees > 43 && relief > 0.35f) RockyFaces++;
                foreach (int v in new[] { a, b, c }) { sums[v] += rock * cross.magnitude; areas[v] += cross.magnitude; }
            }
            for (int i = 0; i < Colors.Length; i++)
                Colors[i] = new Color(areas[i] > 0 ? sums[i] / areas[i] : 0, 0, 0, 1);
        }
    }
}
