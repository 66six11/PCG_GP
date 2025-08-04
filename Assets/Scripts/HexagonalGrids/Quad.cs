using System;
using System.Collections.Generic;

namespace HexagonalGrids
{
    public class Quad
    {
        public readonly HexVertex a, b, c, d;

        public readonly HexVertex[] vertices;

        public readonly Edge ab, bc, cd, da;

        public readonly Edge[] edges;

        public Quad(HexVertex a, HexVertex b, HexVertex c, HexVertex d, List<Edge> edges)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            ab = Edge.GenerateEdge(a, b, edges);
            bc = Edge.GenerateEdge(b, c, edges);
            cd = Edge.GenerateEdge(c, d, edges);
            da = Edge.GenerateEdge(d, a, edges);
            this.edges = new[] { ab, bc, cd, da };
            vertices = new HexVertex[] { a, b, c, d };
        }

        public static Quad MergeTriangles(Triangle triangle1, Triangle triangle2, List<Edge> edges)
        {
            Edge sharedEdge = triangle1.GetSharedEdge(triangle2);
          

            int t1EdgeIndex = Array.IndexOf(triangle1.edges, sharedEdge);
            int t2EdgeIndex = Array.IndexOf(triangle2.edges, sharedEdge);


            int t1OtherIndexIndex = (t1EdgeIndex + 2) % 3;
            int t2OtherIndexIndex = (t2EdgeIndex + 2) % 3;
            HexVertex a = triangle1.vertices[t1OtherIndexIndex];
            HexVertex b = triangle2.vertices[(t2OtherIndexIndex + 2) % 3];
            HexVertex c = triangle2.vertices[(t2OtherIndexIndex + 3) % 3];
            HexVertex d = triangle2.vertices[(t2OtherIndexIndex + 4) % 3];

            edges.Remove(sharedEdge);
            return new Quad(a, b, c, d, edges);
        }
    }
}