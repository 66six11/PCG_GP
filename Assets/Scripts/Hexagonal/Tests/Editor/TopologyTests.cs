using System;
using System.Collections.Generic;
using System.Linq;
using HexagonalGrids;
using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

public class TopologyTests
{
    [Test]
    public void NormalRelaxGridEntryUsesBalancedAndInvalidatesDerivedLayers()
    {
        HexGrid Make()
        {
            Random.InitState(42); var grid = Editor.HexGridRelaxationDiagnostics.Layout(12, 8).CreateGrid(2, Vector3.zero, 1, 2);
            grid.RandomMergeTriangles(); grid.SubdivideGrid(); return grid;
        }
        var actual = Make(); var expected = Make();
        actual.BuildLayers(); actual.BuildCells(); actual.RelaxGrid(30, false);
        expected.RelaxBalanced(30, false);
        Assert.That(actual.cells, Is.Empty); Assert.That(actual.layers.Count, Is.EqualTo(1));
        CollectionAssert.AreEqual(expected.baseLayer.subVertices.Select(v => v.position), actual.baseLayer.subVertices.Select(v => v.position));
        Assert.That(Editor.HexGridRelaxationDiagnostics.AreaStatistics(actual).cv, Is.LessThan(0.08));
    }

    [Test]
    public void ArchivedOriginalReproducesHistoricalBaseline()
    {
        Random.InitState(42); var grid = Editor.HexGridRelaxationDiagnostics.Layout(12, 8).CreateGrid(2, Vector3.zero, 1, 0);
        grid.RandomMergeTriangles(); grid.SubdivideGrid(); double area = grid.baseLayer.subQuads.Sum(Editor.HexGridRelaxationDiagnostics.Area);
        Editor.Archive.LegacyQuadRelaxation.Apply(grid, 30, false);
        Assert.That(Editor.HexGridRelaxationDiagnostics.AreaStatistics(grid).cv, Is.EqualTo(0.290465).Within(0.0001));
        Assert.That((grid.baseLayer.subQuads.Sum(Editor.HexGridRelaxationDiagnostics.Area) / area - 1) * 100, Is.EqualTo(19.914719).Within(0.001));
    }
    [TestCase(12, 8, 0)]
    [TestCase(12, 8, 42)]
    [TestCase(24, 16, 0)]
    [TestCase(24, 16, 42)]
    [TestCase(48, 32, 42)]
    public void BalancedRelaxationImprovesSizeDistributionWithoutInflatingMap(int width, int height, int seed)
    {
        Random.InitState(seed);
        var grid = Editor.HexGridRelaxationDiagnostics.Layout(width, height).CreateGrid(2, Vector3.zero, 1.35f, 2);
        grid.RandomMergeTriangles(); grid.SubdivideGrid();
        var boundary = grid.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
        var result = grid.RelaxBalanced(100, false);
        Assert.That(result.AcceptedSteps, Is.GreaterThan(0)); Assert.That(result.FinalEnergy, Is.LessThan(result.InitialEnergy));
        Assert.That(result.FinalAreaCv, Is.LessThan(result.InitialAreaCv * 0.75));
        Assert.That(result.MinAreaRatio, Is.GreaterThan(0.7)); Assert.That(result.MaxAreaRatio, Is.LessThan(1.3));
        Assert.That(Math.Abs(result.AreaChangePercent), Is.LessThan(2));
        Assert.That(Editor.HexGridRelaxationDiagnostics.InvalidFaces(grid), Is.Zero);
        Assert.That(boundary.Any(p => p.Key.position != p.Value), Is.True);
        grid.BuildLayers(); grid.BuildCells(); Assert.That(grid.cells.Count, Is.EqualTo(grid.baseLayer.subQuads.Count * 2));
        TestContext.WriteLine($"BALANCED: {width}x{height}, seed={seed}, cv={result.InitialAreaCv:F5}->{result.FinalAreaCv:F5}, areaChange={result.AreaChangePercent:F5}%, range={result.MinAreaRatio:F5}..{result.MaxAreaRatio:F5}, energy={result.InitialEnergy:F3}->{result.FinalEnergy:F3}");
    }

    [Test]
    public void BalancedRelaxationCanPinBoundaryAndReproduceResults()
    {
        HexGrid Make()
        {
            Random.InitState(42); var grid = Editor.HexGridRelaxationDiagnostics.Layout(6, 5).CreateGrid(2, Vector3.zero, 1, 1);
            grid.RandomMergeTriangles(); grid.SubdivideGrid(); return grid;
        }
        var first = Make(); var second = Make();
        var boundary = first.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
        first.RelaxBalanced(30, true); second.RelaxBalanced(30, true);
        foreach (var pair in boundary) Assert.That(pair.Key.position, Is.EqualTo(pair.Value));
        CollectionAssert.AreEqual(first.baseLayer.subVertices.Select(v => v.position), second.baseLayer.subVertices.Select(v => v.position));
        Assert.That(Editor.HexGridRelaxationDiagnostics.InvalidFaces(first), Is.Zero);
    }

    [Test]
    public void BalancedRelaxationRejectsInvalidInputWithoutMutatingOrDroppingLayers()
    {
        var grid = Generate(1, 2); var quad = grid.baseLayer.subQuads[0]; quad.b.position = quad.a.position;
        var positions = grid.baseLayer.subVertices.Select(v => v.position).ToArray(); int cells = grid.cells.Count;
        Assert.Throws<ArgumentException>(() => grid.RelaxBalanced(30));
        CollectionAssert.AreEqual(positions, grid.baseLayer.subVertices.Select(v => v.position));
        Assert.That(grid.cells.Count, Is.EqualTo(cells)); Assert.That(grid.layers.Count, Is.EqualTo(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.RelaxBalanced(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.RelaxBalanced(1, false, new QuadMeshOptimizer.Settings { areaWeight = float.NaN }));
    }

    [Test]
    public void BalancedRelaxationRejectsCrossingBoundariesEvenWithConvexIndividualFaces()
    {
        var grid = new HexGrid(new[] { new Coord(0, 0), new Coord(4, 0) }, 2, Vector3.zero, 1, 0);
        grid.RandomMergeTriangles(); grid.SubdivideGrid();
        var shift = -new Coord(4, 0).ToWorldPosition(2, Vector3.zero) + Vector3.right * 0.7f;
        foreach (var vertex in grid.baseLayer.subVertices) if (vertex.x > 5) vertex.position += shift;
        Assert.That(Editor.HexGridRelaxationDiagnostics.InvalidFaces(grid), Is.Zero);
        Assert.Throws<ArgumentException>(() => grid.RelaxBalanced(30));
    }
    [TestCase(5, 0)]
    [TestCase(5, 42)]
    [TestCase(30, 0)]
    [TestCase(30, 42)]
    public void FreeBoundaryRelaxationMovesOutlineAndKeepsTheseCasesConvex(int iterations, int seed)
    {
        Random.InitState(seed);
        var grid = Editor.HexGridRelaxationDiagnostics.Layout(24, 16).CreateGrid(2, Vector3.zero, 1.35f, 2);
        grid.RandomMergeTriangles(); grid.SubdivideGrid();
        var boundary = grid.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
        int faces = grid.baseLayer.subQuads.Count;
        grid.RelaxGrid(iterations, preserveBoundary: false);
        Assert.That(boundary.Any(p => p.Key.position != p.Value), Is.True);
        Assert.That(Editor.HexGridRelaxationDiagnostics.InvalidFaces(grid), Is.Zero);
        Assert.That(grid.baseLayer.subQuads.Count, Is.EqualTo(faces));
        grid.BuildLayers(); grid.BuildCells(); Assert.That(grid.cells.Count, Is.EqualTo(faces * 2));
    }
    [TestCase(24, 16, 30, 14, 0)]
    [TestCase(24, 16, 30, 14, 42)]
    [TestCase(48, 32, 100, 2, 0)]
    [TestCase(48, 32, 100, 2, 42)]
    [TestCase(1, 64, 100, 1, 0)]
    [TestCase(1, 64, 100, 1, 42)]
    public void LargerAndHighlyRelaxedMapsKeepValidCells(int width, int height, int iterations, int layers, int seed)
    {
        Random.InitState(seed);
        var grid = Editor.HexGridRelaxationDiagnostics.Layout(width, height).CreateGrid(2, Vector3.zero, 1.35f, layers);
        grid.RandomMergeTriangles(); grid.SubdivideGrid();
        var boundary = grid.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
        double area = grid.baseLayer.subQuads.Sum(Editor.HexGridRelaxationDiagnostics.Area);
        grid.RelaxGrid(iterations);
        Assert.That(Editor.HexGridRelaxationDiagnostics.InvalidFaces(grid), Is.Zero);
        foreach (var pair in boundary) Assert.That(pair.Key.position, Is.EqualTo(pair.Value));
        Assert.That(grid.baseLayer.subQuads.Sum(Editor.HexGridRelaxationDiagnostics.Area), Is.EqualTo(area).Within(Math.Abs(area) * 0.00001));
        var timer = System.Diagnostics.Stopwatch.StartNew(); grid.BuildLayers(); grid.BuildCells(); timer.Stop();
        Assert.That(grid.cells.Count, Is.EqualTo(grid.baseLayer.subQuads.Count * layers));
        foreach (var cell in grid.cells)
        {
            Assert.That(cell.vertices.Length, Is.EqualTo(8));
            Assert.That(cell.V1.y - cell.V5.y, Is.EqualTo(grid.cellHeight).Within(0.0001f));
            foreach (var neighbor in cell.neighbours) Assert.That(neighbor.neighbours, Does.Contain(cell));
        }
        TestContext.WriteLine($"LARGE_RELAXED_CELLS: {width}x{height}, iterations={iterations}, seed={seed}, layers={layers}, cells={grid.cells.Count}, buildLayersAndCellsMs={timer.Elapsed.TotalMilliseconds:F3}");
    }
    [TestCase(1, 1)]
    [TestCase(12, 2)]
    [TestCase(3, 9)]
    public void TacticalRectanglesGenerateEveryHexWithoutRadius(int width, int height)
    {
        var layout = new HexGridLayout { width = width, height = height, centerOnOrigin = false };
        var grid = layout.CreateGrid(1, new Vector3(7, 2, -9), 1, 2);
        Assert.That(grid.IsCoordinateLayout, Is.True); Assert.That(grid.HexCoordinates.Count, Is.EqualTo(width * height));
        Assert.That(grid.triangles.Count, Is.EqualTo(width * height * 6));
        float area = grid.triangles.Sum(t => Area(t.vertices));
        Assert.That(area, Is.EqualTo(-width * height * 2 * Mathf.Sqrt(3)).Within(0.001f));
        grid.RandomMergeTriangles(); grid.SubdivideGrid();
        var boundary = grid.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
        grid.RelaxGrid(10);
        foreach (var pair in boundary) Assert.That(pair.Key.position, Is.EqualTo(pair.Value));
        Assert.That(grid.baseLayer.subQuads.All(q => Area(q.vertices) < -0.00001f), Is.True);
        Assert.That(grid.baseLayer.subQuads.Sum(q => Area(q.vertices)), Is.EqualTo(area).Within(0.005f));
        grid.BuildLayers(); grid.BuildCells(); Assert.That(grid.cells.Count, Is.EqualTo(grid.baseLayer.subQuads.Count * 2));
    }

    [Test]
    public void MissingTilesPreserveInteriorHoleAndConcaveBoundary()
    {
        var layout = new HexGridLayout { width = 5, height = 5, centerOnOrigin = false,
            excludedTiles = new List<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(4, 3), new Vector2Int(4, 4) } };
        var grid = layout.CreateGrid(1, Vector3.zero, 1, 1);
        Assert.That(grid.HexCoordinates.Count, Is.EqualTo(22));
        var uses = grid.triangles.SelectMany(t => t.edges).GroupBy(e => e).ToDictionary(g => g.Key, g => g.Count());
        int originalBoundary = uses.Count(p => p.Value == 1); float area = grid.triangles.Sum(t => Area(t.vertices));
        grid.RandomMergeTriangles(); grid.SubdivideGrid(); grid.RelaxGrid(15);
        Assert.That(grid.baseLayer.boundaryEdges.Count, Is.EqualTo(originalBoundary * 2));
        Assert.That(grid.baseLayer.subVertices.Count - grid.baseLayer.subEdges.Count + grid.baseLayer.subQuads.Count, Is.Zero, "One connected region with one hole.");
        Assert.That(grid.baseLayer.subQuads.Sum(q => Area(q.vertices)), Is.EqualTo(area).Within(0.005f));
        var hole = new Coord(2, 1).ToWorldPosition(1, Vector3.zero);
        Assert.That(grid.baseLayer.subQuads.Any(q => Inside(hole, q.a.position, q.b.position, q.c.position)
            || Inside(hole, q.a.position, q.c.position, q.d.position)), Is.False, "The missing hex must not be bridged by a generated face.");
        var edgeUse = grid.baseLayer.subQuads.SelectMany(q => q.edges).GroupBy(e => e);
        Assert.That(edgeUse.All(g => g.Count() <= 2), Is.True);
    }

    [Test]
    public void AdjacentHexesWeldCornersAndGenerateSymmetricCellNeighbors()
    {
        var grid = new HexGrid(new[] { new Coord(-4, 7), new Coord(-3, 7) }, 1, Vector3.zero, 1, 2);
        Assert.That(grid.vertices.Count, Is.EqualTo(12)); Assert.That(grid.edges.Count, Is.EqualTo(23));
        Assert.That(grid.vertices.Select(v => v.position).Distinct().Count(), Is.EqualTo(12));
        grid.RandomMergeTriangles(); grid.SubdivideGrid(); grid.RelaxGrid(5); grid.BuildLayers(); grid.BuildCells();
        foreach (var cell in grid.cells)
        {
            Assert.That(cell.vertices.Distinct().Count(), Is.EqualTo(8));
            CollectionAssert.AreEquivalent(grid.cells.Where(c => c != cell && c.vertices.Intersect(cell.vertices).Count() == 4), cell.neighbours);
            foreach (var next in cell.neighbours) Assert.That(next.neighbours, Does.Contain(cell));
        }
    }

    [Test]
    public void CoordinateOrderDuplicatesAndRebuildsDoNotChangeSeededTopology()
    {
        var tiles = new[] { new Coord(-5, 2), new Coord(-4, 2), new Coord(-4, 3), new Coord(8, -9) };
        HexGrid Make(IEnumerable<Coord> input)
        {
            Random.InitState(123); var grid = new HexGrid(input, 1, Vector3.zero, 1, 1);
            grid.RandomMergeTriangles(); grid.SubdivideGrid(); grid.RelaxGrid(5); grid.BuildLayers(); grid.BuildCells(); return grid;
        }
        var first = Make(tiles); var second = Make(tiles.Reverse().Concat(tiles));
        Assert.That(first.HexCoordinates.Count, Is.EqualTo(4));
        CollectionAssert.AreEqual(first.allSubVertices.Select(v => v.position), second.allSubVertices.Select(v => v.position));
        Assert.That(first.baseLayer.subVertices.Count - first.baseLayer.subEdges.Count + first.baseLayer.subQuads.Count, Is.EqualTo(2));
        first.GenerateHexTriangles(); Assert.That(first.triangles.Count, Is.EqualTo(24));
        Assert.That(first.vertices.Count, Is.EqualTo(first.vertices.Select(v => v.position).Distinct().Count()));
        Assert.Throws<InvalidOperationException>(() => first.RingVertices(0));
    }

    [Test]
    public void InvalidAndEmptyCoordinateInputsAreHandledExplicitly()
    {
        Assert.Throws<ArgumentNullException>(() => new HexGrid((IEnumerable<Coord>)null, 1, Vector3.zero, 1, 1));
        Assert.Throws<ArgumentException>(() => new HexGrid(new Coord[] { null }, 1, Vector3.zero, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HexGridLayout { width = 0 }.GetCoordinates());
        var grid = new HexGrid(Array.Empty<Coord>(), 1, Vector3.zero, 1, 1);
        grid.SubdivideGrid(); grid.BuildLayers(); grid.BuildCells(); Assert.That(grid.cells, Is.Empty);
    }

    private static bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        float Cross(Vector3 u, Vector3 v, Vector3 w) => (v.x-u.x)*(w.z-u.z)-(v.z-u.z)*(w.x-u.x);
        return Cross(a,b,p) <= 0 && Cross(b,c,p) <= 0 && Cross(c,a,p) <= 0;
    }
    private Random.State savedRandomState;
    [SetUp] public void SaveRandom() => savedRandomState = Random.state;
    [TearDown] public void RestoreRandom() => Random.state = savedRandomState;

    private static HexGrid Generate(int radius, int layers, int seed = 123)
    {
        Random.InitState(seed);
        var grid = new HexGrid(radius, 1f, new Vector3(3, 2, -7), 1.5f, layers);
        grid.RandomMergeTriangles();
        grid.SubdivideGrid();
        grid.BuildKdTree();
        grid.BuildLayers();
        grid.BuildCells();
        return grid;
    }

    [Test]
    public void EdgeIdentitySurvivesReversalAndPositionChanges()
    {
        var a = new Vertex(Vector3.zero);
        var b = new Vertex(Vector3.right);
        var edges = new EdgeCollection();
        var edge = edges.GetOrAdd(a, b);
        a.position = Vector3.up;
        Assert.That(edges.GetOrAdd(b, a), Is.SameAs(edge));
        Assert.That(edges.Count, Is.EqualTo(1));
        Assert.Throws<ArgumentException>(() => edges.GetOrAdd(a, a));
    }

    [TestCase(1, 0)]
    [TestCase(2, 17)]
    [TestCase(5, 83)]
    [TestCase(8, 123)]
    public void MergeAndSubdivisionPreserveCoverageAndBoundary(int radius, int seed)
    {
        Random.InitState(seed);
        var grid = new HexGrid(radius, 1, Vector3.zero, 1, 1);
        Assert.That(grid.triangles.Count, Is.EqualTo(6 * radius * radius));
        float originalArea = grid.triangles.Sum(t => Area(t.vertices));
        grid.RandomMergeTriangles();
        Assert.That(grid.triangles.Count + grid.quads.Count * 2, Is.EqualTo(6 * radius * radius));
        float mergedArea = grid.triangles.Sum(t => Area(t.vertices)) + grid.quads.Sum(q => Area(q.vertices));
        Assert.That(mergedArea, Is.EqualTo(originalArea).Within(0.005f));
        foreach (var quad in grid.quads)
        {
            Assert.That(quad.vertices.Distinct().Count(), Is.EqualTo(4));
            Assert.That(Mathf.Sign(Area(quad.vertices)), Is.EqualTo(Mathf.Sign(originalArea)));
        }
        grid.SubdivideGrid();
        var layer = grid.baseLayer;
        Assert.That(layer.subQuads.Count, Is.EqualTo(3 * grid.triangles.Count + 4 * grid.quads.Count));
        Assert.That(layer.subQuads.Sum(q => Area(q.vertices)), Is.EqualTo(originalArea).Within(0.01f));
        Assert.That(layer.boundaryEdges.Count, Is.EqualTo(12 * radius));
        Assert.That(layer.boundaryVertices.Distinct().Count(), Is.EqualTo(layer.boundaryVertices.Count));
        Assert.That(layer.subVertices.Count - layer.subEdges.Count + layer.subQuads.Count, Is.EqualTo(1));
        Assert.That(layer.subEdges.Select(e => new EdgeKey(e.a, e.b)).Distinct().Count(), Is.EqualTo(layer.subEdges.Count));
        foreach (var edge in layer.subEdges)
            Assert.That(layer.subQuads.Count(q => q.edges.Contains(edge)), Is.InRange(1, 2));
    }

    private static float Area(Vertex[] vertices)
    {
        float area = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i].position;
            var b = vertices[(i + 1) % vertices.Length].position;
            area += a.x * b.z - a.z * b.x;
        }
        return area * 0.5f;
    }

    [TestCase(0, 0)]
    [TestCase(0, 2)]
    [TestCase(1, 1)]
    [TestCase(2, 3)]
    [TestCase(4, 2)]
    public void LayerAndCellTopologyMatchesBruteForce(int radius, int layerCount)
    {
        var grid = Generate(radius, layerCount);
        Assert.That(grid.layers.Count, Is.EqualTo(layerCount + 1));
        Assert.That(grid.cells.Count, Is.EqualTo(grid.baseLayer.subQuads.Count * layerCount));
        Assert.That(grid.allSubEdges.Count, Is.EqualTo(grid.baseLayer.subEdges.Count * (layerCount + 1)
            + grid.baseLayer.subVertices.Count * layerCount));
        foreach (var cell in grid.cells)
        {
            Assert.That(cell.vertices.Distinct().Count(), Is.EqualTo(8));
            Assert.That(cell.edges.Distinct().Count(), Is.EqualTo(12));
            var expected = grid.cells.Where(other => other != cell && other.vertices.Intersect(cell.vertices).Count() == 4);
            CollectionAssert.AreEquivalent(expected, cell.neighbours);
        }
        foreach (var vertex in grid.allSubVertices)
            CollectionAssert.AreEquivalent(grid.cells.Where(c => c.Contains(vertex)), grid.Vertex2Cells(vertex));
        foreach (var cell in grid.cells)
            foreach (var edge in cell.edges) Assert.That(grid.allSubEdges.GetOrAdd(edge.a, edge.b), Is.SameAs(edge));
    }

    [Test]
    public void RebuildingStagesDoesNotAccumulateTopology()
    {
        var grid = Generate(3, 3);
        int vertices = grid.allSubVertices.Count, edges = grid.allSubEdges.Count, cells = grid.cells.Count;
        grid.BuildCells();
        grid.BuildCellNeighborsMap();
        Assert.That(grid.allSubEdges.Count, Is.EqualTo(edges));
        Assert.That(grid.cells.Count, Is.EqualTo(cells));
        grid.BuildLayers();
        Assert.That(grid.cells, Is.Empty);
        grid.BuildCells();
        Assert.That(grid.allSubVertices.Count, Is.EqualTo(vertices));
        Assert.That(grid.allSubEdges.Count, Is.EqualTo(edges));
        Assert.That(grid.cells.Count, Is.EqualTo(cells));
        int baseFaces = grid.baseLayer.subQuads.Count;
        grid.SubdivideGrid();
        Assert.That(grid.baseLayer.subQuads.Count, Is.EqualTo(baseFaces));
        Assert.That(grid.layers.Count, Is.EqualTo(1));
        Assert.That(grid.cells, Is.Empty);
        Assert.That(grid.vertexKDTree, Is.Null);
        grid.BuildLayers();
        grid.BuildCells();
        Assert.That(grid.allSubEdges.Count, Is.EqualTo(edges));
        grid.GenerateHexTriangles();
        Assert.That(grid.triangles.Count, Is.EqualTo(54));
        Assert.That(grid.quads, Is.Empty);
        Assert.That(grid.allSubVertices, Is.Empty);
    }

    [Test]
    public void VertexStateAndCellBitOrderingArePreserved()
    {
        var grid = Generate(1, 2);
        var source = grid.baseLayer.subVertices[0];
        source.IsEnabled = true;
        grid.BuildLayers();
        grid.BuildCells();
        for (int layer = 0; layer < grid.layers.Count; layer++)
        {
            var copy = grid.layers[layer].subVertices[0];
            Assert.That(copy.IsEnabled, Is.True);
            Assert.That(copy.position.y, Is.EqualTo(source.position.y + layer * grid.cellHeight).Within(0.0001f));
            if (layer > 0) Assert.That(copy, Is.Not.SameAs(source));
        }
        var cell = grid.cells[0];
        for (int i = 0; i < 8; i++)
        {
            foreach (var vertex in cell.vertices) vertex.IsEnabled = false;
            cell.vertices[i].IsEnabled = true;
            Assert.That(cell.GetCellByte(), Is.EqualTo((byte)(1 << i)));
        }
    }

    [Test]
    public void KdTreeDoesNotReorderTopologyAndQueriesMatchLinearSearch()
    {
        var grid = Generate(3, 3);
        var vertices = grid.allSubVertices;
        var original = vertices.ToArray();
        var tree = new VertexKdTree(vertices);
        CollectionAssert.AreEqual(original, vertices);
        Assert.That(tree.maxDepth, Is.GreaterThan(0));
        for (int i = 0; i < 30; i++)
        {
            var point = new Vector3(Random.Range(-8f, 9f), Random.Range(-2f, 10f), Random.Range(-15f, 1f));
            float best = vertices.Min(v => (v.position - point).sqrMagnitude);
            Assert.That((tree.QueryNearest(point).position - point).sqrMagnitude, Is.EqualTo(best).Within(0.0001f));
            CollectionAssert.AreEquivalent(vertices.Where(v => (v.position - point).sqrMagnitude <= 9), tree.RangeQuery(point, 3));
            var min = point - Vector3.one * 2;
            var max = point + Vector3.one * 2;
            CollectionAssert.AreEquivalent(vertices.Where(v => v.x >= min.x && v.x <= max.x && v.y >= min.y
                && v.y <= max.y && v.z >= min.z && v.z <= max.z), tree.RangeQuery(min, max));
        }
    }

    [Test]
    public void NonManifoldCellFacesAreRejected()
    {
        var grid = Generate(1, 1);
        grid.cells.Add(grid.cells[0]);
        grid.cells.Add(grid.cells[0]);
        Assert.Throws<InvalidOperationException>(() => grid.BuildCellNeighborsMap());
    }

    [TestCase(-1, 1, 1, 1)]
    [TestCase(1, 0, 1, 1)]
    [TestCase(1, 1, 0, 1)]
    [TestCase(1, 1, 1, -1)]
    public void InvalidGridParametersAreRejected(int radius, float size, float height, int layers)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HexGrid(radius, size, Vector3.zero, height, layers));
    }
}
