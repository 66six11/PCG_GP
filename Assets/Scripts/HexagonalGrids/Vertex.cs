using System.Collections.Generic;
using UnityEngine;


namespace HexagonalGrids
{
    public interface Vertex
    {
        public Vector3 position { get; set; }
    }

    public class HexVertex : Vertex
    {
        public readonly Coord coord;
        public Vector3 position { get; set; }


        public HexVertex(Coord coord, Vector3 position)
        {
            this.coord = coord;
            this.position = position;
        }
    }

    public class MidVertex : Vertex
    {
        public Vector3 position { get; set; }

        public MidVertex(Vertex a, Vertex b)
        {
            Vector3 midPos = (a.position + b.position) / 2;
            this.position = midPos;
        }
    }

    public class CenterVertex : Vertex
    {
        public Vector3 position { get; set; }

        public CenterVertex(Vertex[] vertices)
        {
            Vector3 centerPos = Vector3.zero;
            foreach (var v in vertices)
            {
                centerPos += v.position;
            }

            this.position = centerPos / vertices.Length;
        }
    }
}