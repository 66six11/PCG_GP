using System.Collections.Generic;

namespace HexagonalGrids
{
    public class Edge
    {
        public readonly HashSet<HexVertex> endpoints;
        public MidVertex midVertex;
        Edge(HexVertex a, HexVertex b)
        {
            endpoints = new HashSet<HexVertex>() { a, b };
            midVertex = new MidVertex(a, b);
        }

        public static Edge GenerateEdge(HexVertex a, HexVertex b, List<Edge> edges, List<MidVertex> midVertices)
        {
            foreach (var edge in edges)
            {
                if (edge.endpoints.Contains(a) && edge.endpoints.Contains(b))
                {
                    return edge;
                }
            }
            var newEdge = new Edge(a, b);
            edges.Add(newEdge);
            midVertices.Add(newEdge.midVertex);
            return newEdge;

           
        }
    }
}