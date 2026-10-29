using System.Collections.Generic;

namespace HexagonalGrids
{
    public sealed class Triangle
    {
        public readonly Vertex[] vertices;
        public readonly Edge[] edges;
        public readonly Vertex center;
        public Vertex a => vertices[0];
        public Vertex b => vertices[1];
        public Vertex c => vertices[2];
        public Edge ab => edges[0];
        public Edge bc => edges[1];
        public Edge ca => edges[2];

        public Triangle(Vertex a, Vertex b, Vertex c, EdgeCollection edges,
            List<Vertex> midVertices, List<Vertex> centerVertices)
        {
            vertices = new[] { a, b, c };
            this.edges = new[] { Edge.GenerateEdge(a, b, edges, midVertices),
                Edge.GenerateEdge(b, c, edges, midVertices), Edge.GenerateEdge(c, a, edges, midVertices) };
            center = new Vertex((a.position + b.position + c.position) / 3f);
            centerVertices.Add(center);
        }

        public Edge GetSharedEdge(Triangle other)
        {
            if (ReferenceEquals(this, other)) return null;
            foreach (var edge in edges)
                foreach (var candidate in other.edges)
                    if (ReferenceEquals(edge, candidate)) return edge;
            return null;
        }
    }
}
