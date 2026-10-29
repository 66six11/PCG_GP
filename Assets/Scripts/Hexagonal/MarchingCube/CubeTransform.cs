using System;
using UnityEngine;

namespace MarchingCube
{
    /// <summary>Horizontal cube symmetry: mirror in local X first, then rotate around Y.</summary>
    public readonly struct CubeTransform
    {
        public int QuarterTurns { get; }
        public bool MirrorX { get; }
        public Quaternion Rotation => Quaternion.Euler(0, QuarterTurns * 90, 0);
        private static readonly int[,] CornerPermutations = BuildPermutations();

        public CubeTransform(int quarterTurns, bool mirrorX = false)
        {
            QuarterTurns = (quarterTurns % 4 + 4) % 4;
            MirrorX = mirrorX;
        }

        public Vector3 TransformPoint(Vector3 point)
        {
            if (MirrorX) point.x = -point.x;
            switch (QuarterTurns)
            {
                case 1: return new Vector3(point.z, point.y, -point.x);
                case 2: return new Vector3(-point.x, point.y, -point.z);
                case 3: return new Vector3(-point.z, point.y, point.x);
                default: return point;
            }
        }

        public byte TransformState(byte state)
        {
            int result = 0, transform = QuarterTurns + (MirrorX ? 4 : 0);
            for (int i = 0; i < 8; i++)
                if ((state & (1 << i)) != 0) result |= 1 << CornerPermutations[transform, i];
            return (byte)result;
        }

        public static Vector3 GetCorner(int index)
        {
            if (index < 0 || index >= 8) throw new ArgumentOutOfRangeException(nameof(index));
            int face = index & 3;
            return new Vector3(face == 0 || face == 3 ? -0.5f : 0.5f,
                index < 4 ? 0.5f : -0.5f, face < 2 ? 0.5f : -0.5f);
        }

        private static int[,] BuildPermutations()
        {
            var result = new int[8, 8];
            for (int transform = 0; transform < 8; transform++)
            {
                var symmetry = new CubeTransform(transform & 3, transform >= 4);
                for (int i = 0; i < 8; i++)
                {
                    var corner = symmetry.TransformPoint(GetCorner(i));
                    int face = corner.z > 0 ? (corner.x < 0 ? 0 : 1) : (corner.x > 0 ? 2 : 3);
                    result[transform, i] = face + (corner.y > 0 ? 0 : 4);
                }
            }
            return result;
        }
    }
}
