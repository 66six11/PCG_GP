using UnityEngine;

namespace HexagonalGrids
{
    public class SubQuad
    {
        public Vertex a, b, c, d;

        public Vector3 center;

        public SubQuad(Vertex a, Vertex b, Vertex c, Vertex d)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.d = d;
            center = (a.position + b.position + c.position + d.position) / 4;
        }
    }
}