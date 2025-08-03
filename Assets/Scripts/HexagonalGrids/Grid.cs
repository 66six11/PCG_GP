using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public class Grid
    {
        public static int radius;
        public static Vector3 origin;
        public static float cellSize;
        public readonly List<HexVertex> vertices = new List<HexVertex>();


        public Grid(int radius, float cellSize, Vector3 origin)
        {
            Grid.radius = radius;
            Grid.cellSize = cellSize;
            Grid.origin = origin;
            HexVertex.Hex(vertices, Grid.radius);
        }

      
    }
}