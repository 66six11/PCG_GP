using System.Collections.Generic;
using UnityEngine;
using Utility;
using Utility.RefCopy;


namespace HexagonalGrids
{
    public class Vertex : ICopyable<Vertex>
    {
        public int id { get; set; } = RefIDGenerator.ID;
        public Vector3 position { get; set; }
        public float x => this.position.x;
        public float y => this.position.y;
        public float z => this.position.z;

        public Vertex Copy()
        {
            return new Vertex() { position = this.position };
        }
    }

    public class HexVertex : Vertex
    {
        public readonly Coord coord;


        public HexVertex(Coord coord, Vector3 position)
        {
            this.coord = coord;
            this.position = position;
        }
    }

    public class MidVertex : Vertex
    {
        public MidVertex(Vertex a, Vertex b)
        {
            Vector3 midPos = (a.position + b.position) / 2;
            this.position = midPos;
        }
    }

    public class CenterVertex : Vertex
    {
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