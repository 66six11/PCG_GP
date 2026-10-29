using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>Measured source-shape roles; original meshes and horizontal proportions are preserved.</summary>
    public sealed class RockShapeCatalog
    {
        public enum Role { MainBlock, Column, Debris }
        public readonly struct Shape
        {
            public readonly int Index;
            public readonly Vector3 Size;
            public readonly float Slenderness, TopSpan;
            public readonly bool Block;
            public Shape(int index, Mesh mesh)
            {
                Index = index; Size = mesh.bounds.size;
                Slenderness = Size.y / Mathf.Max(Size.x, Size.z);
                var min = new Vector2(float.MaxValue, float.MaxValue); var max = -min;
                foreach (var vertex in mesh.vertices)
                    if (vertex.y >= mesh.bounds.max.y - Size.y * 0.15f)
                    { var p = new Vector2(vertex.x, vertex.z); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
                TopSpan = Mathf.Max(max.x - min.x, max.y - min.y) / Mathf.Max(Size.x, Size.z);
                Block = Slenderness < 1.9f && TopSpan > 0.35f;
            }
        }
        public readonly List<Shape> Shapes = new List<Shape>();
        private readonly List<int> blocks = new List<int>(), columns = new List<int>();
        public RockShapeCatalog(Mesh[] sources)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;
                var size = sources[i].bounds.size;
                if (Mathf.Min(size.x, Mathf.Min(size.y, size.z)) <= 0.0001f) continue;
                var shape = new Shape(i, sources[i]); Shapes.Add(shape);
                if (shape.Block) blocks.Add(i); else columns.Add(i);
            }
        }
        public int Choose(Role role, System.Random random)
        {
            var pool = role == Role.MainBlock ? blocks : role == Role.Column ? columns : null;
            if (pool != null && pool.Count > 0) return pool[random.Next(pool.Count)];
            return Shapes.Count == 0 ? -1 : Shapes[random.Next(Shapes.Count)].Index;
        }
        public static Vector3 SizeFor(Mesh source, float longestDimension)
            => source.bounds.size * (longestDimension / Mathf.Max(source.bounds.size.x, Mathf.Max(source.bounds.size.y, source.bounds.size.z)));
    }
}
