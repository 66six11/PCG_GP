using System;
using System.Collections;
using System.Collections.Generic;

namespace HexagonalGrids
{
    /// <summary>Undirected identity, independent of endpoint order and positions.</summary>
    public readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        private readonly int a, b;
        public EdgeKey(Vertex a, Vertex b)
        {
            this.a = Math.Min(a.id, b.id);
            this.b = Math.Max(a.id, b.id);
        }
        public bool Equals(EdgeKey other) => a == other.a && b == other.b;
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => unchecked(a * 397 ^ b);
    }

    public sealed class Edge
    {
        public Vertex a { get; }
        public Vertex b { get; }
        private Vertex midpoint;
        public Vertex midVertex => midpoint ?? (midpoint = new Vertex(a, b));
        internal Edge(Vertex a, Vertex b) { this.a = a; this.b = b; }

        public static Edge GenerateEdge(Vertex a, Vertex b, EdgeCollection edges, List<Vertex> midVertices)
        {
            var edge = edges.GetOrAdd(a, b, out bool added);
            if (added) midVertices.Add(edge.midVertex);
            return edge;
        }
    }

    /// <summary>Owns edge identity and insertion order; both indexes stay synchronized.</summary>
    public sealed class EdgeCollection : IReadOnlyList<Edge>
    {
        private readonly List<Edge> items = new List<Edge>();
        private readonly Dictionary<EdgeKey, Edge> lookup = new Dictionary<EdgeKey, Edge>();
        public int Count => items.Count;
        public Edge this[int index] => items[index];
        public Edge GetOrAdd(Vertex a, Vertex b) => GetOrAdd(a, b, out _);

        public Edge GetOrAdd(Vertex a, Vertex b, out bool added)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            if (ReferenceEquals(a, b)) throw new ArgumentException("An edge needs two distinct vertices.");
            var key = new EdgeKey(a, b);
            added = !lookup.TryGetValue(key, out var edge);
            if (added)
            {
                edge = new Edge(a, b);
                lookup.Add(key, edge);
                items.Add(edge);
            }
            return edge;
        }

        public void AddRange(IEnumerable<Edge> edges)
        {
            foreach (var edge in edges)
            {
                var key = new EdgeKey(edge.a, edge.b);
                if (lookup.TryGetValue(key, out var existing))
                {
                    if (!ReferenceEquals(existing, edge))
                        throw new InvalidOperationException("Duplicate edge objects for the same endpoints.");
                    continue;
                }
                lookup.Add(key, edge);
                items.Add(edge);
            }
        }

        public void RemoveWhere(HashSet<Edge> removed)
        {
            foreach (var edge in removed) lookup.Remove(new EdgeKey(edge.a, edge.b));
            items.RemoveAll(removed.Contains);
        }
        public void Clear() { lookup.Clear(); items.Clear(); }
        public List<Edge>.Enumerator GetEnumerator() => items.GetEnumerator();
        IEnumerator<Edge> IEnumerable<Edge>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
