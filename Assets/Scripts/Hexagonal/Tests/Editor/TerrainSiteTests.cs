using System.Collections.Generic;
using System.Linq;
using HexagonalGrids;
using HexagonalGrids.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

public class TerrainSiteTests
{
    [MenuItem("Tools/Marching Cube/Terrain Demo/Run Terrain Regression Tests %&F12")]
    public static void RunRegressionTests()
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callback = new ResultWriter(api); api.RegisterCallbacks(callback);
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { nameof(TerrainSiteTests) } }) { runSynchronously = true });
    }

    private sealed class ResultWriter : ICallbacks
    {
        private readonly TestRunnerApi api;
        public ResultWriter(TestRunnerApi api) { this.api = api; }
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            System.IO.Directory.CreateDirectory("Logs/TerrainDemo");
            TestRunnerApi.SaveResultToFile(result, "Logs/TerrainDemo/tests.xml");
            Debug.Log($"TERRAIN_TESTS: passed={result.PassCount}, failed={result.FailCount}");
            api.UnregisterCallbacks(this); Object.DestroyImmediate(api);
        }
    }

    private static TerrainSiteData MakeSurface()
    {
        var previous = Random.state;
        try
        {
            Random.InitState(42);
            var grid = new HexGrid(6, 2, Vector3.zero, 1.35f, 4);
            grid.RandomMergeTriangles(); grid.SubdivideGrid(); grid.RelaxGrid(5);
            grid.BuildLayers(); grid.BuildCells(); return new TerrainSiteData(grid);
        }
        finally { Random.state = previous; }
    }

    [Test]
    public void PlatformCoversBuildingCellsWithoutWarpingBuildingLevels()
    {
        var data = MakeSurface(); var originals = new Dictionary<Vertex, Vector3>();
        foreach (var v in data.Grid.allSubVertices) originals.Add(v, v.position);
        data.UpdateSite(Vector2.zero, 0.5f, true);
        Assert.That(data.PlatformQuads.Count, Is.GreaterThan(0));
        foreach (var quad in data.Grid.baseLayer.subQuads)
        {
            bool occupied = false;
            foreach (var v in quad.vertices) occupied |= data.Inside(v.position);
            if (occupied) Assert.That(data.PlatformQuads.Contains(quad), Is.True);
        }
        foreach (var v in data.PlatformVertices)
            Assert.That(data.Positions[data.Indices[v]].y, Is.EqualTo(data.PlatformHeight).Within(1e-5f));
        foreach (var pair in originals) Assert.That(pair.Key.position, Is.EqualTo(pair.Value));
    }

    [Test]
    public void HeightQueriesMatchTrianglesAndNaturalModeRestoresSurface()
    {
        var data = MakeSurface(); data.UpdateSite(new Vector2(2, -1), 0, true);
        for (int i = 0; i < data.Triangles.Count; i += 3)
        {
            var a = data.Positions[data.Triangles[i]]; var b = data.Positions[data.Triangles[i + 1]]; var c = data.Positions[data.Triangles[i + 2]];
            Assert.That(Vector3.Cross(b - a, c - a).y, Is.GreaterThan(0));
            var center = (a + b + c) / 3;
            Assert.That(data.TryHeight(new Vector2(center.x, center.z), out float height), Is.True);
            Assert.That(height, Is.EqualTo(center.y).Within(1e-4f));
        }
        Assert.That(data.TryHeight(new Vector2(1000, 1000), out _), Is.False);
        data.UpdateSite(Vector2.zero, 1, false);
        foreach (var v in data.Grid.baseLayer.subVertices)
            Assert.That(data.Positions[data.Indices[v]].y, Is.EqualTo(TerrainSiteData.NaturalHeight(v.position)).Within(1e-5f));
        Assert.That(data.CutVolume + data.FillVolume, Is.EqualTo(0).Within(1e-4f));
    }

    [Test]
    public void PolygonHeightEditsKeepSharedGridAndSurviveSiteChanges()
    {
        var data = MakeSurface(); data.UpdateSite(Vector2.zero, 0, false);
        var quad = data.Grid.baseLayer.subQuads[data.Grid.baseLayer.subQuads.Count / 2];
        var before = data.CopyHeightOffsets();
        data.AdjustQuadHeight(quad, 1.25f); data.UpdateSite(Vector2.zero, 0, false);
        foreach (var v in quad.vertices)
            Assert.That(data.Positions[data.Indices[v]].y, Is.EqualTo(TerrainSiteData.NaturalHeight(v.position) + 1.25f).Within(1e-5f));
        var edited = data.CopyHeightOffsets();
        data.UpdateSite(new Vector2(1, 1), 0, true, 4.5f);
        data.UpdateSite(Vector2.zero, 0, false); CollectionAssert.AreEqual(edited, data.CopyHeightOffsets());
        var indices = data.QuadSurfaceIndices[quad];
        var center = (data.Positions[indices[0]] + data.Positions[indices[1]] + data.Positions[indices[2]]) / 3;
        Assert.That(data.FindQuad(new Vector2(center.x, center.z)), Is.SameAs(quad));
        data.RestoreHeightOffsets(before); data.UpdateSite(Vector2.zero, 0, false);
        foreach (var v in quad.vertices)
            Assert.That(data.Positions[data.Indices[v]].y, Is.EqualTo(TerrainSiteData.NaturalHeight(v.position)).Within(1e-5f));
    }

    [Test]
    public void EditableDemoSupportsHistoryBuildingPlacementAndSaveRoundTripWithoutSlab()
    {
        var host = new GameObject("Editable terrain test"); var cameraHost = new GameObject("Editable terrain test camera");
        var demo = host.AddComponent<TerrainBuildingDemo>();
        try
        {
            var library = AssetDatabase.LoadAssetAtPath<MarchingCube.ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset");
            var rocks = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset");
            demo.Configure(library, cameraHost.AddComponent<Camera>(), rocks); demo.Initialize();
            Assert.That(demo.BuildingRoot.transform.Find("Foundation"), Is.Null);
            Assert.That(demo.FloorHeight, Is.EqualTo(demo.Surface.PlatformHeight));
            demo.RemoveBuilding(); Assert.That(demo.HasBuilding, Is.False);
            var quad = demo.Surface.Grid.baseLayer.subQuads[0];
            demo.AdjustPolygon(quad, 0.75f); var edited = demo.Surface.CopyHeightOffsets();
            Assert.That(demo.UndoEdit(), Is.True);
            foreach (float h in demo.Surface.CopyHeightOffsets()) Assert.That(h, Is.Zero);
            Assert.That(demo.RedoEdit(), Is.True); CollectionAssert.AreEqual(edited, demo.Surface.CopyHeightOffsets());
            Assert.That(demo.PlaceBuilding(new Vector2(1, -1)), Is.True); Assert.That(demo.HasBuilding, Is.True);
            Assert.That(demo.PlaceBuilding(new Vector2(100, 100)), Is.False);
            demo.SetRockCoverage(0.31f); demo.SetRockConformity(0.78f);
            string saved = demo.ExportEdits();
            demo.RemoveBuilding(); demo.AdjustPolygon(quad, 0.5f);
            demo.SetRockCoverage(0.9f); demo.SetRockConformity(0.1f);
            demo.ImportEdits(saved);
            Assert.That(demo.RockCoverage, Is.EqualTo(0.31f).Within(1e-5f));
            Assert.That(demo.RockConformity, Is.EqualTo(0.78f).Within(1e-5f));
            Assert.That(demo.HasBuilding, Is.True); CollectionAssert.AreEqual(edited, demo.Surface.CopyHeightOffsets());
            Assert.That(demo.BuildingRoot.GetComponent<MeshCollider>(), Is.Null, "No invisible foundation collider may remain.");
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(cameraHost); }
    }

    [Test]
    public void GeneratedRockModelsAreClosedPlanarSolidsWithStableGeometry()
    {
        for (int variant = 0; variant < RockModelFactory.Names.Length; variant++)
        {
            var mesh = new Mesh();
            try
            {
                RockModelFactory.Build(mesh, variant); var vertices = mesh.vertices; var triangles = mesh.triangles;
                var edges = new Dictionary<(Vector3, Vector3), int>(); double volume = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                    Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(1e-12f),
                        $"variant={variant}, triangle={i / 3}, a={a:F8}, b={b:F8}, c={c:F8}");
                    volume += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
                    foreach (var pair in new[] { (a, b), (b, c), (c, a) })
                    {
                        var p = Quantize(pair.Item1); var q = Quantize(pair.Item2);
                        bool reverse = p.x > q.x || (p.x == q.x && (p.y > q.y || (p.y == q.y && p.z > q.z)));
                        var key = reverse ? (q, p) : (p, q); edges.TryGetValue(key, out int count); edges[key] = count + 1;
                    }
                }
                Assert.That(volume, Is.GreaterThan(0));
                foreach (int count in edges.Values) Assert.That(count, Is.EqualTo(2), "Rock mesh has an open or non-manifold edge.");
                RockModelFactory.Build(mesh, variant); CollectionAssert.AreEqual(vertices, mesh.vertices);
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }

    [Test]
    public void CapturedHandPlacedClusterRetainsItsSourcesAndUprightMainAxes()
    {
        var library = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset");
        Assert.That(library.clusterTemplate, Is.Not.Null);
        var reference = library.clusterTemplate.GetPlacements(library);
        Assert.That(reference.Count, Is.GreaterThanOrEqualTo(3));
        var local = ProceduralCliffMesh.CreateClusterPieces(42, 1, reference);
        var segment = new TerrainSiteData.CliffSegment(new Vector3(-2, 4.5f, 0), new Vector3(2, 4.5f, 0),
            new Vector3(-2, 0, 1.6f), new Vector3(2, 0, 1.6f), Vector3.forward, Vector3.forward);
        var attached = ProceduralCliffMesh.AttachCluster(segment, 0.5f, 0.5f, 42, 1, local);
        for (int i = 0; i < reference.Count; i++)
        {
            Assert.That(local[i].SourceIndex, Is.EqualTo(reference[i].SourceIndex));
            Assert.That(Vector3.Distance(local[i].Position, reference[i].Position), Is.LessThan(0.11f));
            if (attached[i].SourceIndex >= 4)
                Assert.That(Vector3.Angle(attached[i].Rotation * Vector3.up, Vector3.up), Is.LessThan(27));
        }
    }

    [Test]
    public void ContinuousTerrainNeverCreatesCutWallsOrDuplicateHeightRims()
    {
        var data = MakeSurface();
        for (int seed = 1; seed <= 8; seed++)
        {
            data.UpdateSite(new Vector2(seed % 2, 0), 0, true, seed, seed);
            Assert.That(data.Positions.Count, Is.EqualTo(data.Indices.Count));
            Assert.That(data.Triangles.Count, Is.EqualTo(data.Grid.baseLayer.subQuads.Count * 6));
            Assert.That(data.Cliffs, Is.Empty);
            foreach (var pair in data.Indices)
            {
                var p = data.Positions[pair.Value];
                Assert.That(p.x, Is.EqualTo(pair.Key.x)); Assert.That(p.z, Is.EqualTo(pair.Key.z));
            }
            for (int i = 0; i < data.Triangles.Count; i += 3)
            {
                var a = data.Positions[data.Triangles[i]]; var b = data.Positions[data.Triangles[i + 1]];
                var c = data.Positions[data.Triangles[i + 2]]; var p = (a + b + c) / 3;
                Assert.That(data.TryHeight(new Vector2(p.x, p.z), out float h), Is.True);
                Assert.That(h, Is.EqualTo(p.y).Within(1e-4f));
            }
        }
    }

    [Test]
    public void SlopeMaterialWeightsAreContinuousAndDoNotModifyTerrainGeometry()
    {
        Assert.That(TerrainSlopeField.RockWeight(20), Is.Zero);
        Assert.That(TerrainSlopeField.RockWeight(45), Is.EqualTo(0.5f).Within(1e-5f));
        Assert.That(TerrainSlopeField.RockWeight(70), Is.EqualTo(1));
        float previous = 0;
        for (int i = 0; i <= 90; i++) { float w = TerrainSlopeField.RockWeight(i); Assert.That(w, Is.GreaterThanOrEqualTo(previous)); previous = w; }
        var data = SteepSurface(); var positions = data.Positions.ToArray(); var triangles = data.Triangles.ToArray();
        var slopes = new TerrainSlopeField(data);
        Assert.That(slopes.RockyFaces, Is.GreaterThan(0));
        CollectionAssert.AreEqual(positions, data.Positions); CollectionAssert.AreEqual(triangles, data.Triangles);
        foreach (var color in slopes.Colors) Assert.That(color.r, Is.InRange(0f, 1f));
    }

    private static TerrainSiteData SteepSurface()
    {
        var data = MakeSurface(); var heights = new float[data.Indices.Count];
        foreach (var pair in data.Indices) heights[pair.Value] = -TerrainSiteData.NaturalHeight(pair.Key.position);
        data.RestoreHeightOffsets(heights);
        Quad middle = null; float closest = float.PositiveInfinity;
        foreach (var quad in data.Grid.baseLayer.subQuads)
            if (quad.center.sqrMagnitude < closest) { closest = quad.center.sqrMagnitude; middle = quad; }
        data.AdjustQuadHeight(middle, 7); data.UpdateSite(Vector2.zero, 0, false); return data;
    }

    [Test]
    public void RockCoverUsesConnectedWallsWithStableUprightRocksAndReleasesOnFlattening()
    {
        var data = SteepSurface(); var slopes = new TerrainSlopeField(data);
        var source = new Mesh(); RockModelFactory.Build(source, 4);
        var parent = new GameObject("Slope cover test");
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var cover = new SlopeRockCover();
        try
        {
            int count = 0;
            foreach (var region in new RockWallField(data, slopes).Regions)
            {
                var first = SlopeRockCover.Compose(data, region, 42, 1, new[] { source });
                var again = SlopeRockCover.Compose(data, region, 42, 1, new[] { source });
                Assert.That(first.Count, Is.EqualTo(again.Count)); count += first.Count;
                for (int i = 0; i < first.Count; i++)
                {
                    Assert.That(first[i].Position, Is.EqualTo(again[i].Position));
                    if (!first[i].IsToe) Assert.That(Vector3.Angle(first[i].Rotation * Vector3.up, Vector3.up), Is.LessThan(18));
                    Assert.That(SlopeRockCover.HasRearContact(data, first[i]), Is.True, "Lower rear support probes must be inside the continuous terrain.");
                }
            }
            Assert.That(count, Is.GreaterThan(0), "Rocks must appear on edited slopes without any building or platform boundary.");
            var sources = new[] { source };
            cover.Update(data, slopes, sources, parent.transform, material, 42, 1, true);
            Assert.That(cover.FaceCount + cover.ToeCount, Is.EqualTo(count)); Assert.That(cover.ChunkCount, Is.GreaterThan(0));
            cover.Update(data, slopes, sources, parent.transform, material, 42, 1, true);
            Assert.That(cover.RebuiltChunks, Is.Zero);
            foreach (var collider in parent.GetComponentsInChildren<MeshCollider>())
                Assert.That(collider.sharedMesh, Is.SameAs(collider.GetComponent<MeshFilter>().sharedMesh));
            var flat = new float[data.Indices.Count];
            foreach (var pair in data.Indices) flat[pair.Value] = -TerrainSiteData.NaturalHeight(pair.Key.position);
            data.RestoreHeightOffsets(flat); data.UpdateSite(Vector2.zero, 0, false);
            cover.Update(data, new TerrainSlopeField(data), sources, parent.transform, material, 42, 1, true);
            Assert.That(cover.ChunkCount, Is.Zero); Assert.That(parent.transform.childCount, Is.Zero);
        }
        finally { cover.Dispose(); Object.DestroyImmediate(parent); Object.DestroyImmediate(source); Object.DestroyImmediate(material); }
    }

    [Test]
    public void CoverageAddsStableMixedShapesAndZeroProducesNoModels()
    {
        var data = SteepSurface(); var slopes = new TerrainSlopeField(data);
        var sources = new HashSet<int>(); int lowerCount = 0, higherCount = 0;
        var meshes = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset").slopeMeshes;
        foreach (var region in new RockWallField(data, slopes).Regions)
        {
            Assert.That(SlopeRockCover.Compose(data, region, 42, 0, meshes), Is.Empty);
            var low = SlopeRockCover.Compose(data, region, 42, 0.25f, meshes);
            var high = SlopeRockCover.Compose(data, region, 42, 1, meshes);
            lowerCount += low.Count; higherCount += high.Count;
            foreach (var a in low)
                Assert.That(high.Exists(b => a.Position == b.Position && a.Size == b.Size && a.Rotation == b.Rotation && a.SourceIndex == b.SourceIndex), Is.True,
                    "Coverage must not reroll existing candidates.");
            foreach (var p in high)
            {
                sources.Add(p.SourceIndex);
                var bounds = meshes[p.SourceIndex].bounds.size;
                var scale = new Vector3(p.Size.x / bounds.x, p.Size.y / bounds.y, p.Size.z / bounds.z);
                Assert.That(scale.x, Is.EqualTo(scale.z).Within(0.00001f), "Horizontal proportions survive Titan-style tallness adjustment.");
                Assert.That(p.Size.y, Is.GreaterThan(0.17f));
                Assert.That(SlopeRockCover.HasRearContact(data, p), Is.True);
            }
        }
        Assert.That(higherCount, Is.GreaterThan(lowerCount));
        Assert.That(sources.Count, Is.GreaterThan(3));
        Assert.That(sources.Any(s => AssetDatabase.GetAssetPath(meshes[s]).Contains("/Type01/")) && sources.Any(s => AssetDatabase.GetAssetPath(meshes[s]).Contains("/Type02/")), Is.True, "Mix thick and slender source models.");

    }

    private static TerrainSiteData LongSlope()
    {
        var data = MakeSurface(); data.UpdateSite(Vector2.zero, 0, false);
        for (int i = 0; i < data.Positions.Count; i++)
        {
            var p = data.Positions[i]; p.y = Mathf.Clamp01((p.x + 2) / 4) * 6; data.Positions[i] = p;
        }
        return data;
    }

    [Test]
    public void WallProfilesMeasureWholeSlopeAndSurviveTriangleRefinement()
    {
        var data = LongSlope(); var field = new RockWallField(data, new TerrainSlopeField(data));
        Assert.That(field.Regions.Count, Is.EqualTo(1));
        float rise = field.Regions[0].MaximumRise;
        Assert.That(rise, Is.GreaterThan(5.5f));
        var original = data.Triangles.ToArray(); data.Triangles.Clear();
        for (int i = 0; i < original.Length; i += 3)
        {
            int a = original[i], b = original[i + 1], c = original[i + 2], mid = data.Positions.Count;
            data.Positions.Add((data.Positions[a] + data.Positions[b] + data.Positions[c]) / 3);
            data.Triangles.AddRange(new[] { a, b, mid, b, c, mid, c, a, mid });
        }
        var refined = new RockWallField(data, new TerrainSlopeField(data));
        Assert.That(refined.Regions.Count, Is.EqualTo(1));
        Assert.That(refined.Regions[0].MaximumRise, Is.EqualTo(rise).Within(0.3f), "Wall rise is independent of triangle relief.");
    }

    [Test]
    public void WallDistanceControlsScaleAndCrestEnvelopeWithoutMutatingTerrain()
    {
        var data = LongSlope(); var before = data.Positions.ToArray();
        var field = new RockWallField(data, new TerrainSlopeField(data));
        var meshes = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset").slopeMeshes;
        var rocks = SlopeRockCover.Compose(data, field.Regions[0], 42, 1, meshes);
        var near = new List<float>(); var far = new List<float>();
        foreach (var rock in rocks)
        {
            var point = new Vector2(rock.Position.x, rock.Position.z);
            Assert.That(RockWallField.TrySample(field.Regions[0], point, out var local), Is.True);
            float d = local.WallDistance / SlopeRockCover.OutsideReach;
            if (d < 0.2f && rock.Size.y > 1) near.Add(rock.Size.y);
            if (d > 0.65f) far.Add(rock.Size.y);
            var vertices = meshes[rock.SourceIndex].vertices; var bounds = meshes[rock.SourceIndex].bounds;
            foreach (var vertex in vertices)
            {
                var p = rock.Position + rock.Rotation * Vector3.Scale(vertex - bounds.center,
                    new Vector3(rock.Size.x / bounds.size.x, rock.Size.y / bounds.size.y, rock.Size.z / bounds.size.z));
                Assert.That(p.y, Is.LessThanOrEqualTo(local.Crest.y + 0.121f), "No rocks perched above the crest envelope.");
            }
        }
        Assert.That(near.Count, Is.GreaterThan(0)); Assert.That(far.Count, Is.GreaterThan(0));
        Assert.That(near.Average(), Is.GreaterThan(far.Average() * 2));
        CollectionAssert.AreEqual(before, data.Positions);
    }

    [Test]
    public void ShapeClassificationUsesActualGeometryAndRubbleKeepsUniformScale()
    {
        var meshes = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset").slopeMeshes;
        var catalog = new RockShapeCatalog(meshes);
        Assert.That(catalog.Shapes.Count(s => s.Block), Is.GreaterThan(15));
        Assert.That(catalog.Shapes.Count(s => !s.Block), Is.GreaterThan(15));
        var random = new System.Random(42);
        for (int i = 0; i < 100; i++)
        {
            int block = catalog.Choose(RockShapeCatalog.Role.MainBlock, random);
            int column = catalog.Choose(RockShapeCatalog.Role.Column, random);
            Assert.That(catalog.Shapes.Single(s => s.Index == block).Block, Is.True);
            Assert.That(catalog.Shapes.Single(s => s.Index == column).Block, Is.False);
            var size = RockShapeCatalog.SizeFor(meshes[column], 0.35f);
            Assert.That(Mathf.Max(size.x, Mathf.Max(size.y, size.z)), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(size.y / size.x, Is.EqualTo(meshes[column].bounds.size.y / meshes[column].bounds.size.x).Within(0.0001f));
        }
    }

    [Test]
    public void PlateauCornersUseFiniteWallDistanceAndRemoteRocksHaveAHardSizeLimit()
    {
        var data = MakeSurface(); data.UpdateSite(Vector2.zero, 0, false);
        for (int i = 0; i < data.Positions.Count; i++)
        {
            var p = data.Positions[i];
            float edge = Mathf.Max(Mathf.Abs(p.x + 1) - 3, Mathf.Abs(p.z) - 2.5f);
            p.y = 5.8f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 1.65f, edge)));
            data.Positions[i] = p;
        }
        var field = new RockWallField(data, new TerrainSlopeField(data));
        var meshes = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset").slopeMeshes;
        int remote = 0;
        foreach (var region in field.Regions)
        {
            foreach (var p in SlopeRockCover.Compose(data, region, 42, 1, meshes))
            {
                var point = new Vector2(p.Position.x, p.Position.z);
                float actual = region.DistanceToWall(point);
                Assert.That(RockWallField.TrySample(region, point, out var sample), Is.True);
                Assert.That(sample.WallDistance, Is.EqualTo(actual).Within(0.0001f));
                float longest = Mathf.Max(p.Size.x, Mathf.Max(p.Size.y, p.Size.z));
                Assert.That(longest, Is.LessThanOrEqualTo(SlopeRockCover.MaximumDimension(actual, sample.Rise) + 0.0001f));
                if (actual >= 0.9f)
                {
                    remote++; Assert.That(p.IsToe, Is.True);
                    Assert.That(longest, Is.LessThanOrEqualTo(0.451f));
                    Assert.That(Mathf.Abs((p.Rotation * Vector3.up).y), Is.LessThan(0.25f), "Rubble long axis lies down.");
                }
            }
        }
        Assert.That(remote, Is.GreaterThan(0), "Must exercise the outer rubble zone.");
        // A point beside a finite wall end is far away even if its normal projection is zero.
        var finite = new RockWallField.Region();
        finite.WallTriangles.Add(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) });
        Assert.That(finite.DistanceToWall(new Vector2(0, 4)), Is.EqualTo(3).Within(0.0001f));
    }

    private static TerrainSiteData HeightPlateau(float height)
    {
        var data = MakeSurface(); data.UpdateSite(Vector2.zero, 0, false);
        for (int i = 0; i < data.Positions.Count; i++)
        {
            var p = data.Positions[i];
            float edge = Mathf.Max(Mathf.Abs(p.x + 1) - 3, Mathf.Abs(p.z) - 2.5f);
            p.y = height * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 1.65f, edge)));
            data.Positions[i] = p;
        }
        return data;
    }

    [Test]
    public void TallPlateauBoundsPrimarySizeAndRetainsVisibleEmbeddedFaces()
    {
        var data = HeightPlateau(12); var original = data.Positions.ToArray();
        var meshes = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset").slopeMeshes;
        int bodies = 0;
        foreach (var region in new RockWallField(data, new TerrainSlopeField(data)).Regions)
            foreach (var p in SlopeRockCover.Compose(data, region, 42, 1, meshes))
            {
                if (p.IsToe) continue; bodies++;
                Assert.That(p.Size.y, Is.LessThanOrEqualTo(4.201f), "Height must saturate instead of matching a 12m cliff.");
                Assert.That(Mathf.Max(p.Size.x, p.Size.z), Is.LessThanOrEqualTo(1.801f), "Thickness and width have an independent cap.");
                Assert.That(RockWallField.TrySample(region, new Vector2(p.Position.x, p.Position.z), out var sample), Is.True);
                Assert.That(SlopeRockCover.HasRearContact(data, p), Is.True);
                Assert.That(SlopeRockCover.HasVisibleFront(data, p.Position, p.Rotation, p.Size, sample.Outward), Is.True,
                    "Embedded rocks must keep an exposed middle face.");
            }
        Assert.That(bodies, Is.GreaterThan(10));
        CollectionAssert.AreEqual(original, data.Positions);
        Assert.That(SlopeRockCover.RequestedEmbed(12, 2), Is.GreaterThan(SlopeRockCover.RequestedEmbed(3, 2)));
        Assert.That(SlopeRockCover.RequestedEmbed(100, 100, true), Is.LessThanOrEqualTo(0.65f));
    }

    [Test]
    public void TallPlateauAddsMediumCoverageAndToeRubbleWithoutEnlargingRemoteRocks()
    {
        var meshes = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset").slopeMeshes;
        int[] mediums = new int[2], rubble = new int[2];
        for (int h = 0; h < 2; h++)
        {
            var data = HeightPlateau(h == 0 ? 3 : 12);
            foreach (var region in new RockWallField(data, new TerrainSlopeField(data)).Regions)
                for (int seed = 42; seed <= 44; seed++)
                    foreach (var p in SlopeRockCover.Compose(data, region, seed, 1, meshes))
                    {
                        if (p.IsToe) rubble[h]++;
                        else if (p.Size.y < 1.6f && p.Size.y > 0.45f) mediums[h]++;
                        float distance = region.DistanceToWall(new Vector2(p.Position.x, p.Position.z));
                        if (distance >= 0.9f)
                            Assert.That(Mathf.Max(p.Size.x, Mathf.Max(p.Size.y, p.Size.z)), Is.LessThanOrEqualTo(0.451f));
                    }
        }
        Assert.That(mediums[1], Is.GreaterThan(mediums[0]), "Higher walls need more medium rocks across their faces.");
        Assert.That(rubble[1], Is.GreaterThan(rubble[0]), "Higher walls need more small debris at the foot.");
    }

    [Test]
    public void MixedRockLibraryContainsBothPluginFamilies()
    {
        var library = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>("Assets/Art/PCG/Rocks/CliffRockLibrary.asset");
        Assert.That(library.slopeMeshes.Length, Is.EqualTo(51));
        foreach (var mesh in library.slopeMeshes) Assert.That(mesh, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(library.slopeMeshes[0]), Does.StartWith("Assets/RockTools/Resources/Meshes/Type01/"));
        Assert.That(AssetDatabase.GetAssetPath(library.slopeMeshes[30]), Does.StartWith("Assets/RockTools/Resources/Meshes/Type02/"));
    }

    [Test]
    public void SlopeBlendShaderCompilesForTheProjectPipeline()
    {
        var shader = Shader.Find("PCG/Terrain Slope Blend");
        Assert.That(shader, Is.Not.Null); Assert.That(shader.isSupported, Is.True);
        Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
    }

    private static Vector3 Quantize(Vector3 p) => new Vector3(Mathf.Round(p.x * 100000), Mathf.Round(p.y * 100000), Mathf.Round(p.z * 100000));
}
