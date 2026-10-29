using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    /// <summary>One ordered face type for source and subdivided topology.</summary>
    public sealed class Quad
    {
        public readonly Vertex[] vertices;
        public readonly Edge[] edges;
        public Vertex a => vertices[0];
        public Vertex b => vertices[1];
        public Vertex c => vertices[2];
        public Vertex d => vertices[3];
        public Edge ab => edges[0];
        public Edge bc => edges[1];
        public Edge cd => edges[2];
        public Edge da => edges[3];
        public Vector3 center => (a.position + b.position + c.position + d.position) * 0.25f;
        private Vertex cachedCenter;
        public Vertex centerVertex => cachedCenter ?? (cachedCenter = new Vertex(center));

        public Quad(Vertex a, Vertex b, Vertex c, Vertex d, EdgeCollection edgeCollection)
        {
            vertices = new[] { a, b, c, d };
            edges = new[] { edgeCollection.GetOrAdd(a, b), edgeCollection.GetOrAdd(b, c),
                edgeCollection.GetOrAdd(c, d), edgeCollection.GetOrAdd(d, a) };
        }

        public Quad(Vertex a, Vertex b, Vertex c, Vertex d, EdgeCollection edges,
            List<Vertex> midVertices, List<Vertex> centerVertices)
        {
            vertices = new[] { a, b, c, d };
            this.edges = new[] { Edge.GenerateEdge(a, b, edges, midVertices),
                Edge.GenerateEdge(b, c, edges, midVertices), Edge.GenerateEdge(c, d, edges, midVertices),
                Edge.GenerateEdge(d, a, edges, midVertices) };
            centerVertices.Add(centerVertex);
        }

        public static Quad MergeTriangles(Triangle first, Triangle second, EdgeCollection edges,
            List<Vertex> midVertices, List<Vertex> centerVertices)
        {
            var shared = first.GetSharedEdge(second);
            if (shared == null) throw new ArgumentException("Triangles must share an edge.");
            int i = Array.IndexOf(first.edges, shared);
            Vertex opposite = null;
            foreach (var vertex in second.vertices)
                if (vertex != shared.a && vertex != shared.b) opposite = vertex;
            // Preserve the first triangle's winding around the merged perimeter.
            return new Quad(first.vertices[(i + 2) % 3], first.vertices[i], opposite,
                first.vertices[(i + 1) % 3], edges, midVertices, centerVertices);
        }
    }
}
