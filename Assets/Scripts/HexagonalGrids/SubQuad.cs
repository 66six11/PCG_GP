using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public class SubQuad
    {
        public Vertex a, b, c, d;

        public Vertex[] vertices;

        public SubEdge ab, bc, cd, da;

        public SubEdge[] edges;

        public Vector3 center => (a.position + b.position + c.position + d.position) / 4;

        public SubQuad(Vertex a, Vertex b, Vertex c, Vertex d, List<SubEdge> edges)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            vertices = new Vertex[] { a, b, c, d };
            ab = SubEdge.GenerateSubEdge(a, b, edges);
            bc = SubEdge.GenerateSubEdge(b, c, edges);
            cd = SubEdge.GenerateSubEdge(c, d, edges);
            da = SubEdge.GenerateSubEdge(d, a, edges);
            this.edges = new SubEdge[] { ab, bc, cd, da };
        }
    }
}