using System.Collections.Generic;
using System.Linq;
using Utility.RefCopy;

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

    public class SubEdge : IRefCopy<Vertex, SubEdge>
    {
        public readonly HashSet<Vertex> endpoints;
       
        SubEdge(Vertex a, Vertex b)
        {
            endpoints = new HashSet<Vertex>() { a, b };
            
        }

        public static SubEdge GenerateSubEdge(Vertex a, Vertex b, List<SubEdge> subEdges)
        {
            foreach (var subEdge in subEdges)
            {
                if (subEdge.endpoints.Contains(a) && subEdge.endpoints.Contains(b))
                {
                    return subEdge;
                }
            }

            var newSubEdge = new SubEdge(a, b);
            subEdges.Add(newSubEdge);
            return newSubEdge;
        }

        public SubEdge Copy(Dictionary<int, Vertex> dict)
        {
            return new SubEdge(dict[endpoints.ToArray()[0].id], dict[endpoints.ToArray()[1].id]);
        }
    }
}