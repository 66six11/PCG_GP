using System;
using System.Threading;
using UnityEngine;

namespace HexagonalGrids
{
    /// <summary>A shared topology vertex, regardless of the generation stage.</summary>
    [Serializable]
    public sealed class Vertex
    {
        private static int nextId;
        public int id { get; } = Interlocked.Increment(ref nextId);
        public readonly Coord coord;
        public Vector3 position;
        public bool IsEnabled;
        public float x => position.x;
        public float y => position.y;
        public float z => position.z;

        public Vertex() { }
        public Vertex(Vector3 position) => this.position = position;
        public Vertex(Coord coord, Vector3 position) : this(position) => this.coord = coord;
        public Vertex(Vertex a, Vertex b) : this((a.position + b.position) * 0.5f) { }
        public Vertex Copy() => new Vertex(coord, position) { IsEnabled = IsEnabled };
    }
}
