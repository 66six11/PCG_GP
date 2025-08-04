using System.Collections.Generic;

namespace HexagonalGrids
{
    public class Edge
    {
        public readonly HashSet<HexVertex> endpoints;

        Edge(HexVertex a, HexVertex b)
        {
            endpoints = new HashSet<HexVertex>() { a, b };
        }

        public static Edge GenerateEdge(HexVertex a, HexVertex b, List<Edge> edges)
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
            return newEdge;

           
        }
    }
}