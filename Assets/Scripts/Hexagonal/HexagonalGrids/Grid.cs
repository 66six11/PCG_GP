using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids
{
    public class GridLayer
    {
        public readonly List<Quad> subQuads = new List<Quad>();
        public readonly EdgeCollection subEdges = new EdgeCollection();
        public readonly List<Vertex> subVertices = new List<Vertex>();
        public readonly List<Edge> boundaryEdges = new List<Edge>();
        public readonly List<Vertex> boundaryVertices = new List<Vertex>();
        public VertexKdTree vertexKDTree;
    }

    public class HexGrid
    {
        public readonly int radius;
        public readonly Vector3 origin;
        public readonly float cellSize;
        public readonly float cellHeight;
        public readonly int layerCount;
        private readonly IReadOnlyList<Coord> hexCoordinates;
        public IReadOnlyList<Coord> HexCoordinates => hexCoordinates;
        public bool IsCoordinateLayout => hexCoordinates != null;

        public readonly List<Vertex> vertices = new List<Vertex>();
        public readonly List<Vertex> midVertices = new List<Vertex>();
        public readonly List<Vertex> centerVertices = new List<Vertex>();


        public readonly List<Triangle> triangles = new List<Triangle>();
        public readonly List<Quad> quads = new List<Quad>();
        public readonly EdgeCollection edges = new EdgeCollection();


        // 网格层
        public readonly List<Quad> allSubQuads = new List<Quad>();
        public readonly EdgeCollection allSubEdges = new EdgeCollection();
        public readonly List<Vertex> allSubVertices = new List<Vertex>();

        public GridLayer baseLayer { get; private set; }
        public readonly List<GridLayer> layers = new List<GridLayer>(); // <--- 多层网格 --->


        public readonly List<Cell> cells = new List<Cell>(); // <--- 网格单元 --->

        public VertexKdTree vertexKDTree;


        private readonly Dictionary<Vertex, List<Cell>> vertex2cellsMap = new Dictionary<Vertex, List<Cell>>(); //点到网格单元的映射


        public HexGrid(int radius, float cellSize, Vector3 origin, float cellHeight, int layerCount)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!(cellSize > 0) || float.IsInfinity(cellSize)) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (!(cellHeight > 0) || float.IsInfinity(cellHeight)) throw new ArgumentOutOfRangeException(nameof(cellHeight));
            if (layerCount < 0) throw new ArgumentOutOfRangeException(nameof(layerCount));
            this.radius = radius;
            this.cellSize = cellSize;
            this.origin = origin;
            this.cellHeight = cellHeight;
            this.layerCount = layerCount;
            Init();
        }

        /// <summary>Build actual hexagonal tiles from axial center coordinates, independent of rings or radius.</summary>
        public HexGrid(IEnumerable<Coord> coordinates, float cellSize, Vector3 origin, float cellHeight, int layerCount)
        {
            if (coordinates == null) throw new ArgumentNullException(nameof(coordinates));
            if (!(cellSize > 0) || float.IsInfinity(cellSize)) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (!(cellHeight > 0) || float.IsInfinity(cellHeight)) throw new ArgumentOutOfRangeException(nameof(cellHeight));
            if (layerCount < 0) throw new ArgumentOutOfRangeException(nameof(layerCount));
            radius = -1; this.cellSize = cellSize; this.origin = origin; this.cellHeight = cellHeight; this.layerCount = layerCount;
            var unique = new HashSet<(int q, int r)>(); var sorted = new List<Coord>();
            foreach (var coord in coordinates)
            {
                if (ReferenceEquals(coord, null)) throw new ArgumentException("A hex coordinate cannot be null.", nameof(coordinates));
                if (unique.Add((coord.q, coord.r))) sorted.Add(new Coord(coord.q, coord.r));
            }
            // Input enumeration order must not change seeded matching, topology indices, or saved-map identity.
            sorted.Sort((a, b) => a.q != b.q ? a.q.CompareTo(b.q) : a.r.CompareTo(b.r));
            hexCoordinates = sorted.AsReadOnly(); GenerateHexTriangles();
        }


        private void Init()
        {
            foreach (var coord in Coord.HexCoord(radius))
            {
                // 通过网格配置计算世界坐标
                vertices.Add(new Vertex(coord, coord.ToWorldPosition(cellSize, origin)));
            }

            GenerateHexTriangles();
        }

        public void BuildKdTree()
        {
            baseLayer.vertexKDTree = new VertexKdTree(baseLayer.subVertices);
        }

        public List<Vertex> RingVertices(int radius)
        {
            if (IsCoordinateLayout) throw new InvalidOperationException("Coordinate maps do not have radius rings.");
            if (radius < 0 || radius > this.radius) throw new ArgumentOutOfRangeException(nameof(radius));
            List<Vertex> result = new List<Vertex>();
            var count = radius > 0 ? 6 * radius : 1;
            var index = radius == 0 ? 0 : 1 + 3 * radius * (radius - 1);
            for (int i = 0; i < count; i++)
            {
                result.Add(vertices[index + i]);
            }

            return result;
        }


        public void GenerateHexTriangles()
        {
            InvalidateSubdivision();
            triangles.Clear();
            quads.Clear();
            edges.Clear();
            midVertices.Clear();
            centerVertices.Clear();
            if (IsCoordinateLayout) { GenerateCoordinateTriangles(); return; }
            // 六边形网格的三角形生成算法

            List<Vertex> inRingVertices;
            List<Vertex> outRingVertices;

            for (int i = 0; i < radius; i++)
            {
                inRingVertices = RingVertices(i);
                outRingVertices = RingVertices(i + 1);


                triangles.AddRange(RingTriangles(i, inRingVertices, outRingVertices, edges, midVertices, centerVertices));
            }
        }

        private void GenerateCoordinateTriangles()
        {
            vertices.Clear();
            // Integer corner keys weld adjacent tiles exactly, including negative coordinates.
            // World X = key.x * size / sqrt(3), world Z = key.z * size.
            var corners = new Dictionary<(long x, long z), Vertex>();
            int[] dx = { 2, 1, -1, -2, -1, 1 }, dz = { 0, -1, -1, 0, 1, 1 };
            foreach (var coord in hexCoordinates)
            {
                var center = new Vertex(coord, coord.ToWorldPosition(cellSize, origin)); vertices.Add(center);
                var ring = new Vertex[6];
                for (int i = 0; i < 6; i++)
                {
                    var key = (x: 3L * coord.q + dx[i], z: -2L * coord.r - coord.q + dz[i]);
                    if (!corners.TryGetValue(key, out var corner))
                    {
                        corner = new Vertex(new Vector3((float)(key.x * (double)cellSize / Math.Sqrt(3)), 0, (float)(key.z * (double)cellSize)) + origin);
                        corners.Add(key, corner); vertices.Add(corner);
                    }
                    ring[i] = corner;
                }
                for (int i = 0; i < 6; i++)
                    triangles.Add(new Triangle(center, ring[i], ring[(i + 1) % 6], edges, midVertices, centerVertices));
            }
        }

        //随机合并两个相邻的三角形
        public void RandomMergeTriangles()
        {
            if (triangles.Count == 0) return;
            InvalidateSubdivision();
            var owners = new Dictionary<Edge, Triangle>();
            var candidates = new List<(Edge edge, Triangle first, Triangle second)>();
            foreach (var triangle in triangles)
            {
                foreach (var edge in triangle.edges)
                {
                    if (owners.TryGetValue(edge, out var first))
                        candidates.Add((edge, first, triangle));
                    else owners.Add(edge, triangle);
                }
            }

            // Randomized greedy matching: each shared edge is considered exactly once.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var candidate = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = candidate;
            }
            var consumed = new HashSet<Triangle>();
            var removedEdges = new HashSet<Edge>();
            var removedVertices = new HashSet<Vertex>();
            foreach (var pair in candidates)
            {
                if (consumed.Contains(pair.first) || consumed.Contains(pair.second)) continue;
                quads.Add(Quad.MergeTriangles(pair.first, pair.second, edges, midVertices, centerVertices));
                consumed.Add(pair.first);
                consumed.Add(pair.second);
                removedEdges.Add(pair.edge);
                removedVertices.Add(pair.edge.midVertex);
                removedVertices.Add(pair.first.center);
                removedVertices.Add(pair.second.center);
            }
            triangles.RemoveAll(consumed.Contains);
            edges.RemoveWhere(removedEdges);
            midVertices.RemoveAll(removedVertices.Contains);
            centerVertices.RemoveAll(removedVertices.Contains);
        }

        public static List<Triangle> RingTriangles(int inRadius, List<Vertex> inRingVertices, List<Vertex> outRingVertices, EdgeCollection outEdges, List<Vertex> midVertices, List<Vertex> centerVertices)
        {
            List<Triangle> result = new List<Triangle>();


            //拐角点的索引间隔
            int inCornerVertexIndexInterval = inRadius == 0 ? 0 : inRingVertices.Count / 6;


            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j <= inCornerVertexIndexInterval; j++)
                {
                    int a = i * inCornerVertexIndexInterval + j;
                    int b = a + i;
                    int c = b + 1;
                    if (a > inRingVertices.Count - 1)
                    {
                        a = 0;
                    }

                    if (c > outRingVertices.Count - 1)
                    {
                        c = 0;
                    }

                    result.Add(new Triangle(inRingVertices[a], outRingVertices[b], outRingVertices[c], outEdges, midVertices, centerVertices));

                    if (inRadius != 0)
                    {
                        if (a != i * inCornerVertexIndexInterval && a % inCornerVertexIndexInterval == 0) continue;
                        b = c;
                        c = a + 1;
                        if (c > inRingVertices.Count - 1)
                        {
                            c = 0;
                        }

                        result.Add(new Triangle(inRingVertices[a], outRingVertices[b], inRingVertices[c], outEdges, midVertices, centerVertices));
                    }
                }
            }


            return result;
        }

        //细分三角形
        private void SubdivideTriangle(Triangle triangle)
        {
            Vertex a = triangle.a;
            Vertex b = triangle.b;
            Vertex c = triangle.c;

            Vertex abMid = triangle.ab.midVertex;
            Vertex bcMid = triangle.bc.midVertex;
            Vertex caMid = triangle.ca.midVertex;
            Vertex triangleCenter = triangle.center;


            baseLayer.subQuads.Add(new Quad(caMid, a, abMid, triangleCenter, baseLayer.subEdges));
            baseLayer.subQuads.Add(new Quad(abMid, b, bcMid, triangleCenter, baseLayer.subEdges));
            baseLayer.subQuads.Add(new Quad(bcMid, c, caMid, triangleCenter, baseLayer.subEdges));
        }

        //细分四边形
        private void SubdivideQuad(Quad quad)
        {
            Vertex a = quad.a;
            Vertex b = quad.b;
            Vertex c = quad.c;
            Vertex d = quad.d;

            Vertex abMid = quad.ab.midVertex;
            Vertex bcMid = quad.bc.midVertex;
            Vertex cdMid = quad.cd.midVertex;
            Vertex daMid = quad.da.midVertex;

            Vertex center = quad.centerVertex;

            baseLayer.subQuads.Add(new Quad(daMid, a, abMid, center, baseLayer.subEdges));
            baseLayer.subQuads.Add(new Quad(abMid, b, bcMid, center, baseLayer.subEdges));
            baseLayer.subQuads.Add(new Quad(bcMid, c, cdMid, center, baseLayer.subEdges));
            baseLayer.subQuads.Add(new Quad(cdMid, d, daMid, center, baseLayer.subEdges));
        }

        //细分网格
        public void SubdivideGrid()
        {
            InvalidateSubdivision();
            foreach (var triangle in triangles) SubdivideTriangle(triangle);
            foreach (var quad in quads) SubdivideQuad(quad);

            var seenVertices = new HashSet<Vertex>();
            var edgeUseCounts = new Dictionary<Edge, int>();
            foreach (var quad in baseLayer.subQuads)
            {
                foreach (var vertex in quad.vertices)
                    if (seenVertices.Add(vertex)) baseLayer.subVertices.Add(vertex);
                foreach (var edge in quad.edges)
                {
                    edgeUseCounts.TryGetValue(edge, out int count);
                    edgeUseCounts[edge] = count + 1;
                }
            }
            var boundaryVertices = new HashSet<Vertex>();
            foreach (var edge in baseLayer.subEdges)
            {
                if (edgeUseCounts[edge] != 1) continue;
                baseLayer.boundaryEdges.Add(edge);
                if (boundaryVertices.Add(edge.a)) baseLayer.boundaryVertices.Add(edge.a);
                if (boundaryVertices.Add(edge.b)) baseLayer.boundaryVertices.Add(edge.b);
            }
            ResetToBaseLayer();
        }

        private void InvalidateSubdivision()
        {
            baseLayer = new GridLayer();
            ResetToBaseLayer();
        }

        private void ResetToBaseLayer()
        {
            layers.Clear();
            layers.Add(baseLayer);
            allSubEdges.Clear();
            allSubVertices.Clear();
            allSubQuads.Clear();
            AddLayerData(baseLayer);
            cells.Clear();
            vertex2cellsMap.Clear();
            vertexKDTree = null;
        }

        private void AddLayerData(GridLayer layer)
        {
            allSubEdges.AddRange(layer.subEdges);
            allSubVertices.AddRange(layer.subVertices);
            allSubQuads.AddRange(layer.subQuads);
        }

        /// <summary>Production relaxation uses the balanced size/shape optimizer.</summary>
        public void RelaxGrid(int times, bool? preserveBoundary = null)
            => RelaxBalanced(times, preserveBoundary ?? IsCoordinateLayout);

        internal void InvalidateDerivedGeometry()
        { ResetToBaseLayer(); baseLayer.vertexKDTree = null; }

        public QuadMeshOptimizer.Result RelaxBalanced(int times, bool preserveBoundary = false, QuadMeshOptimizer.Settings settings = null)
        {
            // Validate and optimize before discarding any existing layer/cell data.
            var result = new QuadMeshOptimizer().Optimize(baseLayer, times, preserveBoundary, settings);
            if (result.AcceptedSteps > 0)
                InvalidateDerivedGeometry();
            return result;
        }

        //复制出其他层的网格
        private GridLayer CopySubGrid(int layerIndex)
        {
            var result = new GridLayer();
            var vertexMap = new Dictionary<Vertex, Vertex>(baseLayer.subVertices.Count);
            foreach (var source in baseLayer.subVertices)
            {
                var copy = source.Copy();
                copy.position += Vector3.up * (cellHeight * layerIndex);
                vertexMap.Add(source, copy);
                result.subVertices.Add(copy);
            }
            foreach (var quad in baseLayer.subQuads)
                result.subQuads.Add(new Quad(vertexMap[quad.a], vertexMap[quad.b],
                    vertexMap[quad.c], vertexMap[quad.d], result.subEdges));
            foreach (var vertex in baseLayer.boundaryVertices)
                result.boundaryVertices.Add(vertexMap[vertex]);
            foreach (var edge in baseLayer.boundaryEdges)
                result.boundaryEdges.Add(result.subEdges.GetOrAdd(vertexMap[edge.a], vertexMap[edge.b]));
            result.vertexKDTree = new VertexKdTree(result.subVertices);
            return result;
        }

        public void BuildLayers()
        {
            ResetToBaseLayer();
            // layerCount is the number of cell layers; there are layerCount + 1 vertex planes.
            for (int i = 1; i <= layerCount; i++)
            {
                var layer = CopySubGrid(i);
                layers.Add(layer);
                AddLayerData(layer);
            }
            vertexKDTree = new VertexKdTree(allSubVertices);
        }

        public void BuildCells()
        {
            cells.Clear();
            vertex2cellsMap.Clear();
            // Drop previous vertical edges while retaining the canonical edges of every plane.
            allSubEdges.Clear();
            foreach (var layer in layers) allSubEdges.AddRange(layer.subEdges);
            for (int i = 1; i < layers.Count; i++)
                for (int j = 0; j < baseLayer.subQuads.Count; j++)
                    cells.Add(new Cell(layers[i].subQuads[j], layers[i - 1].subQuads[j], allSubEdges));
            BuildVertex2CellsMap();
            BuildCellNeighborsMap();
        }

        private void BuildVertex2CellsMap()
        {
            foreach (var vertex in allSubVertices) vertex2cellsMap.Add(vertex, new List<Cell>(8));
            foreach (var cell in cells)
                foreach (var vertex in cell.vertices) vertex2cellsMap[vertex].Add(cell);
        }

        public List<Cell> Vertex2Cells(Vertex vertex) => vertex2cellsMap[vertex];

        public void BuildCellNeighborsMap()
        {
            foreach (var cell in cells) cell.neighbours.Clear();
            var owners = new Dictionary<FaceKey, Cell>();
            var matched = new HashSet<FaceKey>();
            foreach (var cell in cells)
            {
                for (int face = 0; face < Cell.FaceCount; face++)
                {
                    var key = cell.GetFaceKey(face);
                    if (matched.Contains(key)) throw new InvalidOperationException("Non-manifold face shared by more than two cells.");
                    if (owners.TryGetValue(key, out var neighbor))
                    {
                        cell.neighbours.Add(neighbor);
                        neighbor.neighbours.Add(cell);
                        matched.Add(key);
                    }
                    else owners.Add(key, cell);
                }
            }
        }
    }
}
