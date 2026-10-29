using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public enum HexGridLayoutMode { Rectangle, Coordinates }

    /// <summary>Authoring input for tactical hex maps. Rectangle masks use column/row; explicit tiles use axial q/r.</summary>
    [Serializable]
    public sealed class HexGridLayout
    {
        public HexGridLayoutMode mode = HexGridLayoutMode.Rectangle;
        [Min(1)] public int width = 12;
        [Min(1)] public int height = 8;
        public bool centerOnOrigin = true;
        [Tooltip("Rectangle mode: missing tiles in offset (column, row) coordinates, starting at zero.")]
        public List<Vector2Int> excludedTiles = new List<Vector2Int>();
        [Tooltip("Coordinates mode: existing hex centers in axial (q, r) coordinates.")]
        public List<Vector2Int> coordinates = new List<Vector2Int>();

        public List<Coord> GetCoordinates()
        {
            var result = new List<Coord>();
            if (mode == HexGridLayoutMode.Coordinates)
            {
                if (coordinates != null) foreach (var point in coordinates) result.Add(new Coord(point.x, point.y));
                return result;
            }
            if (mode != HexGridLayoutMode.Rectangle) throw new ArgumentOutOfRangeException(nameof(mode));
            if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width), "Map width and height must be positive.");
            var excluded = excludedTiles == null ? new HashSet<Vector2Int>() : new HashSet<Vector2Int>(excludedTiles);
            for (int column = 0; column < width; column++)
                for (int row = 0; row < height; row++)
                    if (!excluded.Contains(new Vector2Int(column, row)))
                        result.Add(new Coord(column, row - (column - (column & 1)) / 2));
            return result;
        }

        public HexGrid CreateGrid(float cellSize, Vector3 origin, float cellHeight, int layers)
        {
            var tiles = GetCoordinates(); var gridOrigin = origin;
            if (centerOnOrigin && tiles.Count > 0)
            {
                var bounds = new Bounds(tiles[0].ToWorldPosition(cellSize, Vector3.zero), Vector3.zero);
                foreach (var tile in tiles) bounds.Encapsulate(tile.ToWorldPosition(cellSize, Vector3.zero));
                gridOrigin -= bounds.center;
            }
            return new HexGrid(tiles, cellSize, gridOrigin, cellHeight, layers);
        }
    }
}
