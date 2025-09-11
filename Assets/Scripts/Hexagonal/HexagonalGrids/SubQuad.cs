using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.RefCopy;

namespace HexagonalGrids
{
    public class SubQuad : IRefCopy<Vertex, SubQuad>
    {
        public Vertex a, b, c, d;

        public Vertex[] vertices;

        public SubEdge ab, bc, cd, da;

        public SubEdge[] edges;

        public Vector3 center => (a.position + b.position + c.position + d.position) / 4;

        public SubQuad(Vertex a, Vertex b, Vertex c, Vertex d, List<SubEdge> edges) : this(a, b, c, d)
        {
            SetEdges(edges);
        }

        public SubQuad(Vertex a, Vertex b, Vertex c, Vertex d)
        {
            
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            vertices = new Vertex[] { a, b, c, d };
        }

        public void SetEdges(List<SubEdge> edges)
        {
            ab = SubEdge.GenerateSubEdge(a, b, edges);
            bc = SubEdge.GenerateSubEdge(b, c, edges);
            cd = SubEdge.GenerateSubEdge(c, d, edges);
            da = SubEdge.GenerateSubEdge(d, a, edges);
            this.edges = new SubEdge[] { ab, bc, cd, da };
        }

        public SubQuad Copy(Dictionary<int, Vertex> dict)
        {
            return new SubQuad(dict[a.id], dict[b.id], dict[c.id], dict[d.id]);
        }
    }
}