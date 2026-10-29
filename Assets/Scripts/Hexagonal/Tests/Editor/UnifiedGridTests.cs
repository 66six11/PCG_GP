using System;
using System.Collections.Generic;
using System.Linq;
using HexagonalGrids;
using HexagonalGrids.Prototype;
using MarchingCube;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

public class UnifiedGridTests
{
    [Test]
    public void MaskedHexMapSupportsPickingBuildingAndHistoryWithoutFillingHoles()
    {
        var host = new GameObject("Masked hex map"); var camera = new GameObject("Masked map camera");
        UnifiedGridBuildingDemo demo = null;
        try
        {
            demo = host.AddComponent<UnifiedGridBuildingDemo>();
            var layout = new HexGridLayout { width = 6, height = 5, centerOnOrigin = true,
                excludedTiles = new List<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(5, 0), new Vector2Int(5, 1) } };
            demo.Configure(AssetDatabase.LoadAssetAtPath<ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset"), camera.AddComponent<Camera>(), false);
            demo.ConfigureLayout(layout); demo.ConfigureRelaxation(30, false); demo.Initialize(); var doc = demo.Document;
            Assert.That(doc.Grid.IsCoordinateLayout, Is.True); Assert.That(doc.Grid.HexCoordinates.Count, Is.EqualTo(27));
            var hole = new Coord(2, 1).ToWorldPosition(doc.Grid.cellSize, doc.Grid.origin);
            Assert.That(demo.TryPickPoint(new Ray(hole + Vector3.up * 50, Vector3.down), out _), Is.False);
            Assert.That(demo.TryPickTerrain(new Ray(hole + Vector3.up * 50, Vector3.down), out _), Is.False);
            foreach (var polygon in doc.Grid.baseLayer.subQuads)
            {
                var p = polygon.center + Vector3.up * 50;
                Assert.That(demo.TryPickTerrain(new Ray(p, Vector3.down), out var picked), Is.True);
                Assert.That(picked, Is.SameAs(polygon));
            }
            var baseline = doc.ExportJson(); int column = doc.ColumnCount / 2;
            doc.SetBuildingPoint(column, doc.TerrainLayer, true); demo.Refresh();
            Assert.That(demo.ActiveModelCount, Is.GreaterThan(0)); Assert.That(demo.WaveFunction.Validate(out var error), Is.True, error);
            demo.UndoEdit(); Assert.That(doc.ExportJson(), Is.EqualTo(baseline)); Assert.That(demo.ActiveModelCount, Is.Zero);
            demo.RedoEdit(); Assert.That(demo.WaveFunction.Validate(out error), Is.True, error);
        }
        finally { if (demo != null) demo.ReleasePreview(); Object.DestroyImmediate(host); Object.DestroyImmediate(camera); }
    }
    [Test]
    public void UpperFloorEditsWithUnchangedFootprintsDoNotDirtyTerrain()
    {
        var doc = Make(radius: 6); var changed = new List<int>(); doc.DrainTerrainDirty(changed);
        doc.SetBuildingPoint(5, 2, true); doc.DrainTerrainDirty(changed);
        Assert.That(changed.Count, Is.GreaterThan(0)); Assert.That(changed.Count, Is.LessThan(doc.ColumnCount));
        int revision = doc.TerrainRevision;
        doc.SetBuildingPoint(5, 4, true); doc.DrainTerrainDirty(changed);
        Assert.That(changed, Is.Empty); Assert.That(doc.TerrainRevision, Is.EqualTo(revision));
        doc.SetBuildingPoint(5, 2, false); doc.DrainTerrainDirty(changed); Assert.That(changed, Is.Empty);
        doc.SetBuildingPoint(5, 4, false); doc.DrainTerrainDirty(changed); Assert.That(changed, Is.Not.Empty);
    }

    [Test]
    public void IncrementalSurfaceMatchesFreshReconstructionAfterMixedEdits()
    {
        var doc = Make(); var changed = new List<int>(); doc.DrainTerrainDirty(changed);
        for (int i = 0; i < 24; i++)
        {
            doc.SetBuildingPoint((i * 13) % doc.ColumnCount, 2 + i % 4, i % 3 != 0);
            doc.EditTerrain(doc.Grid.baseLayer.subQuads[i % doc.Grid.baseLayer.subQuads.Count], i % 2 == 0 ? 0.1f : -0.1f);
            doc.DrainTerrainDirty(changed);
            var clone = Make(); clone.ImportJson(doc.ExportJson());
            CollectionAssert.AreEqual(Surface(clone), Surface(doc));
            if (i % 5 == 0) { doc.Undo(); doc.DrainTerrainDirty(changed); clone.ImportJson(doc.ExportJson()); CollectionAssert.AreEqual(Surface(clone), Surface(doc)); }
        }
    }

    [Test]
    public void ChunkSeamsShareNormalsAndOnlyNearbyCollisionMeshesRebuild()
    {
        var host = new GameObject("Chunk test"); var camera = new GameObject("Chunk camera");
        UnifiedGridBuildingDemo demo = null;
        try
        {
            demo = host.AddComponent<UnifiedGridBuildingDemo>();
            demo.Configure(AssetDatabase.LoadAssetAtPath<ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset"), camera.AddComponent<Camera>(), false);
            demo.Initialize(); var doc = demo.Document;
            Assert.That(demo.TerrainChunks.Count, Is.GreaterThan(1));
            var revisions = demo.TerrainChunks.Select(c => c.Revision).ToArray();
            var polygon = doc.Grid.baseLayer.subQuads.OrderBy(q => q.center.x + q.center.z).First();
            doc.EditTerrain(polygon, 0.1f); demo.Refresh();
            Assert.That(demo.LastTerrainChunks, Is.GreaterThan(0)); Assert.That(demo.LastTerrainChunks, Is.LessThan(demo.TerrainChunks.Count));
            int editedChunks = demo.LastTerrainChunks;
            Assert.That(demo.TerrainChunks.Where((c, i) => c.Revision == revisions[i]).Any(), Is.True);
            var shared = new Dictionary<Vector2, (Vector3 position, Vector3 normal)>(); int seamCopies = 0;
            foreach (var chunk in demo.TerrainChunks)
            {
                Assert.That(chunk.Collider.sharedMesh, Is.SameAs(chunk.Mesh));
                var points = chunk.Mesh.vertices; var normals = chunk.Mesh.normals;
                for (int i = 0; i < points.Length; i++)
                {
                    var key = new Vector2(points[i].x, points[i].z);
                    if (shared.TryGetValue(key, out var other))
                    { seamCopies++; Assert.That(points[i], Is.EqualTo(other.position)); Assert.That(normals[i], Is.EqualTo(other.normal)); }
                    else shared.Add(key, (points[i], normals[i]));
                }
                foreach (var face in chunk.Polygons)
                {
                    var center = face.vertices.Aggregate(Vector3.zero, (sum, v) => sum + doc.GroundPosition(doc.ColumnOf(v))) / 4;
                    Assert.That(demo.TryPickTerrain(new Ray(center + Vector3.up * 30, Vector3.down), out var picked), Is.True);
                    Assert.That(picked, Is.SameAs(face));
                }
            }
            Assert.That(seamCopies, Is.GreaterThan(0));
            demo.LoadExample(); Assert.That(demo.BuildingChunkCount, Is.LessThan(demo.ActiveModelCount));
            TestContext.WriteLine($"CHUNK_WORK: terrainChunks={demo.TerrainChunks.Count}, terrainEditChunks={editedChunks}, buildingModules={demo.ActiveModelCount}, buildingChunks={demo.BuildingChunkCount}");
            Assert.That(host.GetComponentsInChildren<MeshRenderer>().Count(r => r.enabled && r.name.StartsWith("Building chunk")), Is.EqualTo(demo.BuildingChunkCount));
            demo.Refresh(); Assert.That(demo.LastTerrainChunks, Is.Zero); Assert.That(demo.LastBuildingChunks, Is.Zero);
        }
        finally { if (demo != null) demo.ReleasePreview(); Object.DestroyImmediate(host); Object.DestroyImmediate(camera); }
    }
    [MenuItem("Tools/Marching Cube/Unified Grid Demo/Run Regression Tests %#&F5")]
    public static void RunTests()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>(); var callback = new Results(api); api.RegisterCallbacks(callback);
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { nameof(UnifiedGridTests), nameof(WaveFunctionTests), nameof(BuildingTilesetTests), nameof(GridPrototypeTests), nameof(TopologyTests), nameof(MeshDeformationTests), nameof(TerrainSiteTests) } })
            { runSynchronously = true });
    }
    private sealed class Results : ICallbacks
    {
        private readonly TestRunnerApi api;
        public Results(TestRunnerApi runner) { api = runner; }
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            System.IO.Directory.CreateDirectory("Logs/UnifiedGridDemo");
            TestRunnerApi.SaveResultToFile(result, "Logs/UnifiedGridDemo/tests.xml");
            Debug.Log($"UNIFIED_GRID_TESTS: passed={result.PassCount}, failed={result.FailCount}");
            api.UnregisterCallbacks(this); Object.DestroyImmediate(api);
        }
    }
    private static UnifiedGridDocument Make(int seed = 42, int radius = 2)
    {
        var random = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(seed);
            var grid = new HexGrid(radius, 2, new Vector3(0, -2.7f, 0), 1.35f, 14);
            grid.RandomMergeTriangles(); grid.SubdivideGrid(); grid.RelaxGrid(5); grid.BuildLayers(); grid.BuildCells();
            return new UnifiedGridDocument(grid);
        }
        finally { UnityEngine.Random.state = random; }
    }
    private static float[] Surface(UnifiedGridDocument doc)
        => Enumerable.Range(0, doc.ColumnCount).Select(i => doc.GroundPosition(i).y).ToArray();

    [Test]
    public void MildWavesStraddleTerrainPlaneButEveryColumnRemainsBuildable()
    {
        var doc = Make(); float basis = doc.HeightAtLevel(doc.TerrainLayer); var heights = Surface(doc);
        Assert.That(heights.Min(), Is.LessThan(basis)); Assert.That(heights.Max(), Is.GreaterThan(basis));
        Assert.That(heights.All(h => Mathf.Abs(h - basis) <= doc.MaxUndulation), Is.True);
        for (int column = 0; column < doc.ColumnCount; column++)
        {
            Assert.That(doc.CanBuild(column, doc.TerrainLayer), Is.True);
            Assert.That(doc.CanBuild(column, doc.TerrainLayer - 1), Is.False);
        }
        Assert.That(doc.FoundationCellCount, Is.Zero);
    }
    [Test]
    public void BuildingFlattensItsActualDataCellFootprintsAndRemovalRestoresRelief()
    {
        var doc = Make(radius: 6); int column = doc.ColumnCount / 2;
        var original = Surface(doc); var planes = doc.Grid.allSubVertices.Select(v => v.position).ToArray();
        var expected = doc.Grid.baseLayer.subQuads.Where(q => q.vertices.Any(v => doc.ColumnOf(v) == column)).ToArray();
        Assert.That(doc.SetBuildingPoint(column, doc.TerrainLayer, true), Is.True);
        Assert.That(doc.FoundationCellCount, Is.EqualTo(expected.Length));
        foreach (var polygon in expected)
        {
            Assert.That(doc.IsFoundationCell(polygon), Is.True);
            foreach (var corner in polygon.vertices)
                Assert.That(doc.GroundPosition(doc.ColumnOf(corner)).y, Is.EqualTo(doc.HeightAtLevel(doc.TerrainLayer)).Within(1e-6));
        }
        var transition = Enumerable.Range(0, doc.ColumnCount).Where(i => doc.FoundationInfluence(i) > 0 && doc.FoundationInfluence(i) < 1).ToArray();
        Assert.That(transition.Length, Is.GreaterThan(0));
        foreach (int i in transition)
        {
            float expectedHeight = Mathf.Lerp(original[i], doc.HeightAtLevel(doc.TerrainLayer), doc.FoundationInfluence(i));
            Assert.That(doc.GroundPosition(i).y, Is.EqualTo(expectedHeight).Within(1e-6));
        }
        CollectionAssert.AreEqual(planes, doc.Grid.allSubVertices.Select(v => v.position));
        doc.SetBuildingPoint(column, doc.TerrainLayer, false);
        Assert.That(doc.FoundationCellCount, Is.Zero); CollectionAssert.AreEqual(original, Surface(doc));
        doc.Undo(); Assert.That(doc.FoundationCellCount, Is.EqualTo(expected.Length));
        doc.Redo(); CollectionAssert.AreEqual(original, Surface(doc));
    }
    [Test]
    public void FoundationConstraintsPreserveUnderlyingTerrainEditsInsteadOfBlockingThem()
    {
        var doc = Make(); var polygon = doc.Grid.baseLayer.subQuads[0]; int column = doc.ColumnOf(polygon.a);
        doc.SetBuildingPoint(column, doc.TerrainLayer, true);
        Assert.That(doc.EditTerrain(polygon, 0.10f), Is.True);
        foreach (var vertex in polygon.vertices)
            Assert.That(doc.GroundPosition(doc.ColumnOf(vertex)).y, Is.EqualTo(doc.HeightAtLevel(doc.TerrainLayer)).Within(1e-6));
        var raw = polygon.vertices.Select(v => doc.RawGroundHeight(doc.ColumnOf(v))).ToArray();
        doc.SetBuildingPoint(column, doc.TerrainLayer, false);
        CollectionAssert.AreEqual(raw, polygon.vertices.Select(v => doc.GroundPosition(doc.ColumnOf(v)).y));
        doc.Undo(); Assert.That(doc.IsFoundationCell(polygon), Is.True);
        doc.Undo(); Assert.That(doc.Capture().offsets.All(value => value == 0), Is.True);
    }
    [Test]
    public void OneStrokeAndHistoryCoverTerrainAndMultipleBuildings()
    {
        var doc = Make(); var polygon = doc.Grid.baseLayer.subQuads[0];
        doc.EditTerrain(polygon, 0.10f); var land = doc.ExportJson();
        doc.BeginStroke(); doc.SetBuildingPoint(0, 2, true); doc.SetBuildingPoint(doc.ColumnCount - 1, 4, true); doc.EndStroke();
        var built = doc.ExportJson(); Assert.That(doc.UndoCount, Is.EqualTo(2));
        doc.Undo(); Assert.That(doc.ExportJson(), Is.EqualTo(land));
        doc.Redo(); Assert.That(doc.ExportJson(), Is.EqualTo(built));
        doc.Undo(); doc.Undo(); Assert.That(doc.Capture().offsets.All(h => h == 0), Is.True);
        doc.SetBuildingPoint(2, 3, true); Assert.That(doc.RedoCount, Is.Zero);
    }
    [Test]
    public void NetZeroStrokeDoesNotPolluteHistoryOrLeaveAFoundation()
    {
        var doc = Make(); var original = Surface(doc);
        doc.BeginStroke(); doc.SetBuildingPoint(0, 2, true); doc.SetBuildingPoint(0, 2, false);
        Assert.That(doc.EndStroke(), Is.False); Assert.That(doc.UndoCount, Is.Zero);
        Assert.That(doc.FoundationCellCount, Is.Zero); CollectionAssert.AreEqual(original, Surface(doc));
        Assert.That(doc.SetBuildingPoint(0, doc.Grid.layers.Count - 1, true), Is.False);
    }
    [Test]
    public void TerrainBrushStaysMildAndFlattenUsesDesignatedTerrainPlane()
    {
        var doc = Make(); var polygon = doc.Grid.baseLayer.subQuads[0];
        doc.EditTerrain(polygon, 1000);
        foreach (var vertex in polygon.vertices)
            Assert.That(Mathf.Abs(doc.GroundPosition(doc.ColumnOf(vertex)).y - doc.HeightAtLevel(doc.TerrainLayer)), Is.LessThanOrEqualTo(doc.MaxUndulation + 1e-6));
        doc.EditTerrain(polygon, -1000);
        foreach (var vertex in polygon.vertices)
            Assert.That(doc.Capture().offsets[doc.ColumnOf(vertex)], Is.EqualTo(-doc.MaxUndulation));
        doc.EditTerrain(polygon, 0, true);
        foreach (var vertex in polygon.vertices)
            Assert.That(doc.GroundPosition(doc.ColumnOf(vertex)).y, Is.EqualTo(doc.HeightAtLevel(doc.TerrainLayer)).Within(1e-6));
        string before = doc.ExportJson(); Assert.Throws<ArgumentException>(() => doc.EditTerrain(polygon, float.NaN));
        Assert.That(doc.ExportJson(), Is.EqualTo(before));
    }
    [Test]
    public void TerrainLayerMovesWholeLatticeContentAndUndoRestoresItAtomically()
    {
        var doc = Make(); doc.SetBuildingPoint(0, 2, true); doc.SetBuildingPoint(1, 4, true);
        string original = doc.ExportJson(); var raw = Surface(doc);
        Assert.That(doc.SetTerrainLayer(4), Is.True); Assert.That(doc.TerrainLayer, Is.EqualTo(4));
        Assert.That(doc.Grid.layers[4].subVertices[0].IsEnabled, Is.True);
        Assert.That(doc.Grid.layers[6].subVertices[1].IsEnabled, Is.True);
        for (int i = 0; i < doc.ColumnCount; i++) Assert.That(doc.GroundPosition(i).y - raw[i], Is.EqualTo(2 * doc.Grid.cellHeight).Within(1e-5));
        doc.Undo(); Assert.That(doc.ExportJson(), Is.EqualTo(original));
        doc.SetBuildingPoint(3, doc.MaxBuildingLevel, true); original = doc.ExportJson();
        Assert.That(doc.SetTerrainLayer(3), Is.False); Assert.That(doc.ExportJson(), Is.EqualTo(original));
    }
    [Test]
    public void DirtyCellsStayLocalWhileBuildingEditsAlsoInvalidateTerrainContact()
    {
        var doc = Make(); var cells = new List<Cell>(); doc.DrainDirty(cells);
        int revision = doc.TerrainRevision; var vertex = doc.Grid.layers[3].subVertices[5];
        doc.SetBuildingPoint(5, 3, true); doc.DrainDirty(cells);
        CollectionAssert.AreEquivalent(doc.Grid.Vertex2Cells(vertex), cells); Assert.That(doc.TerrainRevision, Is.GreaterThan(revision));
        var polygon = doc.Grid.baseLayer.subQuads.First(q => q.vertices.All(v => doc.ColumnOf(v) != 5));
        doc.EditTerrain(polygon, 0.1f); doc.DrainDirty(cells);
        Assert.That(cells, Is.Empty); Assert.That(vertex.IsEnabled, Is.True);
    }
    [Test]
    public void JsonRecreatesContactAndRejectsInvalidPayloadsBeforeMutation()
    {
        var doc = Make(); doc.EditTerrain(doc.Grid.baseLayer.subQuads[0], 0.1f); doc.SetBuildingPoint(0, 2, true);
        string json = doc.ExportJson(); var positions = Surface(doc); var reloaded = Make(); reloaded.ImportJson(json);
        Assert.That(reloaded.ExportJson(), Is.EqualTo(json)); CollectionAssert.AreEqual(positions, Surface(reloaded));
        var invalid = doc.Capture(); invalid.enabled = new[] { doc.Grid.allSubVertices.Count + 1 };
        Assert.Throws<ArgumentException>(() => doc.Restore(invalid)); Assert.That(doc.ExportJson(), Is.EqualTo(json));
        invalid = doc.Capture(); invalid.offsets[0] = float.NaN;
        Assert.Throws<ArgumentException>(() => doc.Restore(invalid)); Assert.That(doc.ExportJson(), Is.EqualTo(json));
        invalid = doc.Capture(); invalid.enabled = new[] { 0 };
        Assert.Throws<ArgumentException>(() => doc.Restore(invalid)); Assert.That(doc.ExportJson(), Is.EqualTo(json));
        invalid = doc.Capture(); invalid.terrainLayer = 99;
        Assert.Throws<ArgumentException>(() => doc.Restore(invalid)); Assert.That(doc.ExportJson(), Is.EqualTo(json));
        Assert.Throws<ArgumentException>(() => Make(91).ImportJson(json));
    }
    [Test]
    public void OldPerColumnSaveMigratesToUnifiedBaseWithoutChangingRelativeStoreys()
    {
        var doc = Make(); var old = doc.Capture(); old.version = 1; old.offsets = null;
        old.ground = Enumerable.Repeat(2, doc.ColumnCount).ToArray(); old.ground[1] = 4;
        old.enabled = new[] { 2 * doc.ColumnCount, 4 * doc.ColumnCount + 1, 5 * doc.ColumnCount + 1 };
        doc.ImportJson(JsonUtility.ToJson(old));
        Assert.That(doc.Capture().version, Is.EqualTo(3)); Assert.That(doc.TerrainLayer, Is.EqualTo(2));
        CollectionAssert.AreEqual(new[] { 2 * doc.ColumnCount, 2 * doc.ColumnCount + 1, 3 * doc.ColumnCount + 1 }, doc.Capture().enabled);
        var clone = Make(); clone.ImportJson(doc.ExportJson()); Assert.That(clone.ExportJson(), Is.EqualTo(doc.ExportJson()));
    }
    [Test]
    public void ThreeDisconnectedExampleBuildingsShareOneTerrainBase()
    {
        var doc = Make(radius: 6); doc.LoadExample();
        var enabled = new HashSet<Vertex>(doc.Grid.allSubVertices.Where(v => v.IsEnabled));
        var adjacency = enabled.ToDictionary(v => v, v => new List<Vertex>());
        foreach (var edge in doc.Grid.allSubEdges)
            if (enabled.Contains(edge.a) && enabled.Contains(edge.b)) { adjacency[edge.a].Add(edge.b); adjacency[edge.b].Add(edge.a); }
        int components = 0; var floors = new HashSet<int>();
        while (enabled.Count > 0)
        {
            components++; var queue = new Queue<Vertex>(); var first = enabled.First(); queue.Enqueue(first); enabled.Remove(first);
            int floor = int.MaxValue;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue(); floor = Mathf.Min(floor, doc.LevelOf(current));
                foreach (var next in adjacency[current]) if (enabled.Remove(next)) queue.Enqueue(next);
            }
            floors.Add(floor);
        }
        Assert.That(components, Is.EqualTo(3)); CollectionAssert.AreEquivalent(new[] { doc.TerrainLayer }, floors);
        string json = doc.ExportJson(); doc.ClearBuildings(); Assert.That(doc.FoundationCellCount, Is.Zero);
        doc.Undo(); Assert.That(doc.ExportJson(), Is.EqualTo(json));
    }
    [Test]
    public void ControllerCoversAndPicksEveryPolygonAndRefreshesContactCollision()
    {
        var host = new GameObject("Unified test"); var cameraObject = new GameObject("Unified test camera");
        UnifiedGridBuildingDemo demo = null;
        try
        {
            demo = host.AddComponent<UnifiedGridBuildingDemo>();
            demo.Configure(AssetDatabase.LoadAssetAtPath<ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset"), cameraObject.AddComponent<Camera>(), false);
            demo.Initialize(); var doc = demo.Document; int column = doc.ColumnCount / 2; demo.SetLayer(doc.TerrainLayer);
            Assert.That(demo.ConstructionEdgeCount, Is.EqualTo(doc.Grid.baseLayer.subEdges.Count));
            foreach (var polygon in doc.Grid.baseLayer.subQuads)
            {
                var center = polygon.center; center.y = doc.HeightAtLevel(doc.TerrainLayer);
                Assert.That(demo.TryPickPoint(new Ray(center + Vector3.up * 30, Vector3.down), out _), Is.True, "Unpickable cell center.");
                foreach (var corner in polygon.vertices)
                    Assert.That(demo.TryPickPoint(new Ray(new Vector3(corner.x, center.y + 30, corner.z), Vector3.down), out var picked)
                        && doc.ColumnOf(picked) == doc.ColumnOf(corner), Is.True, "Unpickable cell boundary corner.");
            }
            var original = demo.TerrainChunks.SelectMany(c => c.Mesh.vertices).ToArray();
            doc.SetBuildingPoint(column, doc.TerrainLayer, true); demo.Refresh();
            Assert.That(demo.ActiveModelCount, Is.EqualTo(doc.Grid.Vertex2Cells(doc.Grid.layers[doc.TerrainLayer].subVertices[column]).Count));
            Assert.That(host.GetComponentsInChildren<MeshCollider>().All(c => c.sharedMesh != null), Is.True);
            Assert.That(demo.TerrainChunks.SelectMany(c => c.Mesh.vertices).SequenceEqual(original), Is.False);
            Assert.That(demo.ConstructionEdgeCount, Is.EqualTo(doc.Grid.baseLayer.subEdges.Count));
            Assert.That(demo.TerrainChunks.All(c => c.Collider.sharedMesh == c.Mesh), Is.True);
            Assert.That(demo.TryPickPoint(new Ray(new Vector3(1000, 50, 1000), Vector3.down), out _), Is.False);
            doc.ClearBuildings(); demo.Refresh(); Assert.That(demo.ActiveModelCount, Is.Zero);
            CollectionAssert.AreEqual(original, demo.TerrainChunks.SelectMany(c => c.Mesh.vertices));
            demo.UndoEdit(); Assert.That(demo.ActiveModelCount, Is.GreaterThan(0));
            demo.ReleasePreview(); Assert.That(demo.Document, Is.Null);
            Assert.That(host.GetComponentsInChildren<MeshCollider>(), Is.Empty);
            demo.Initialize(); Assert.That(demo.Document, Is.Not.Null);
        }
        finally { if (demo != null) demo.ReleasePreview(); Object.DestroyImmediate(host); Object.DestroyImmediate(cameraObject); }
    }
    [Test]
    public void OverlayShaderCompiles()
    {
        var shader = Shader.Find("PCG/Construction Grid Overlay");
        Assert.That(shader, Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
    }
}

