using System;
using System.Collections.Generic;
using System.Linq;

namespace HexagonalGrids
{
    public class Triangle
    {
        public readonly HexVertex a, b, c;

        public readonly HexVertex[] vertices;

        public readonly Edge ab, bc, ca;

        public readonly Edge[] edges;

        public Triangle(HexVertex a, HexVertex b, HexVertex c, List<Edge> edges)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            vertices = new HexVertex[] { a, b, c };
            ab = Edge.GenerateEdge(a, b, edges);
            bc = Edge.GenerateEdge(b, c, edges);
            ca = Edge.GenerateEdge(c, a, edges);
            this.edges = new Edge[] { ab, bc, ca };
        }


        public Edge GetSharedEdge(Triangle other)
        {
            HashSet<Edge> intersection = new HashSet<Edge>(edges);
            intersection.IntersectWith(other.edges);

            return intersection.Count == 1 ? intersection.First() : null;
        }


        public List<Triangle> FindNeighborTriangles(List<Triangle> triangles)
        {
            List<Triangle> result = new List<Triangle>();
            foreach (Triangle triangle in triangles)
            {
                if (GetSharedEdge(triangle) != null)
                {
                    result.Add(triangle);
                }
            }

            return result;
        }
        
       
    }
}