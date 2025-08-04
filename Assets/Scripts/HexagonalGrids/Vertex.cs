using System.Collections.Generic;
using UnityEngine;


namespace HexagonalGrids
{
    public class Vertex
    {
        public readonly Vector3 position;

        public Vertex(Vector3 position)
        {
            this.position = position;
        }
    }

    public class HexVertex : Vertex
    {
        public readonly Coord coord;


        public HexVertex(Coord coord, Vector3 position) : base(position)
        {
            this.coord = coord;
        }
    }

    public class MidVertex : Vertex
    {
        public MidVertex(Vector3 position) : base(position)
        {
        }
    }

    public class CenterVertex : Vertex
    {
        public CenterVertex(Vector3 position) : base(position)
        {
        }
    }
}