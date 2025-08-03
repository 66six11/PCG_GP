using System.Collections.Generic;

namespace HexagonalGrids
{
    public class Vertex
    {
    }

    public class HexVertex : Vertex
    {
        public readonly Coord coord;

        public HexVertex(Coord coord)
        {
            this.coord = coord;
        }

        public static void Hex(List<HexVertex> vertices, int radius)
        {
            foreach (var coord in Coord.HexCoord(radius))
            {
                vertices.Add(new HexVertex(coord));
            }
        }
    }
}