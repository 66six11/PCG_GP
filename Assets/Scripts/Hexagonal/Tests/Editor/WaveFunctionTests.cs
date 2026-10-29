using System;
using System.Collections.Generic;
using System.Linq;
using HexagonalGrids;
using HexagonalGrids.Prototype;
using MarchingCube;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class WaveFunctionTests
{
    [TestCase("Assets/Art/Buildings/BaseKit/BaseKit.tileset.asset")]
    [TestCase("Assets/Art/Buildings/DCC/DCCModules.tileset.asset")]
    public void LocalEditsRespectImportedModuleRules(string path)
    {
        var document = Document(); document.LoadExample();
        var tileset = AssetDatabase.LoadAssetAtPath<BuildingTileset>(path); Assert.That(tileset, Is.Not.Null);
        var wave = new BuildingWaveFunction(document.Grid, Library(), tileset); var dirty = new List<Cell>();
        document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        var random = new System.Random(42);
        document.BeginStroke();
        for (int i = 0; i < 24; i++)
        {
            int column = random.Next(document.ColumnCount), layer = random.Next(2, 6);
            document.SetBuildingPoint(column, layer, !document.Grid.layers[layer].subVertices[column].IsEnabled);
            document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
            Assert.That(wave.Validate(out var error), Is.True, error);
            if (wave.LastFallbacks > 0)
            {
                // Some imported semantic rules cannot represent arbitrary occupancy edits.
                // A local edit must never introduce a fallback that a fresh full solve avoids.
                var reference = new BuildingWaveFunction(document.Grid, Library(), tileset);
                reference.Update(document.Grid.cells, document.WfcSeed);
                TestContext.WriteLine($"IMPORTED_FALLBACK: edit={i}, local={wave.LastFallbacks}, full={reference.LastFallbacks}, path={path}");
                Assert.That(reference.Validate(out error), Is.True, error);
                Assert.That(wave.LastFallbacks, Is.LessThanOrEqualTo(reference.LastFallbacks), "Local boundaries introduced a new fallback.");
            }
        }
        document.EndStroke(); document.Undo(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        Assert.That(wave.Validate(out var undoError), Is.True, undoError);
    }

    [Test]
    public void IndependentObservationsDoNotRescanEveryEdgeForPropagation()
    {
        const int count = 256; long checks = 0;
        var domains = Enumerable.Range(0, count).Select(_ => new[] { 0, 1, 2, 3 }).ToArray();
        var graph = Enumerable.Range(0, count).Select(i => new[] { i - 1, i + 1 }.Where(j => j >= 0 && j < count).ToArray()).ToArray();
        var result = new WaveFunctionSolver().Solve(domains, graph, (a, av, b, bv) => { checks++; return true; }, _ => 1, 42);
        Assert.That(result.Success, Is.True); Assert.That(result.Observations, Is.EqualTo(count));
        Assert.That(checks, Is.LessThan(count * 40), "Propagation must start at the observed node, not the entire graph.");
    }

    [TestCase(6)]
    [TestCase(12)]
    public void EditingALargeConnectedBuildingKeepsDistantChoicesPinned(int radius)
    {
        var document = Document(radius); document.BeginStroke();
        for (int column = 0; column < document.ColumnCount; column++)
            for (int level = 2; level <= 4; level++) document.SetBuildingPoint(column, level, true);
        document.EndStroke();
        var wave = new BuildingWaveFunction(document.Grid, Library()); var dirty = new List<Cell>();
        document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        Assert.That(wave.Validate(out var error), Is.True, error);
        int active = document.Grid.cells.Count(c => wave.Choice(c) >= 0);
        var initial = document.Grid.cells.ToDictionary(c => c, wave.Choice);
        int target = Enumerable.Range(0, document.ColumnCount).OrderBy(i => document.Grid.baseLayer.subVertices[i].position.sqrMagnitude).First();
        document.SetBuildingPoint(target, 4, false); document.DrainDirty(dirty);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var changed = new HashSet<Cell>(wave.Update(dirty, document.WfcSeed)); timer.Stop();
        TestContext.WriteLine($"LOCAL_WFC: radius={radius}, mapCells={document.Grid.cells.Count}, activeCells={active}, solvedCells={wave.LastSolvedCells}, changedCells={changed.Count}, ms={timer.Elapsed.TotalMilliseconds:F3}");
        Assert.That(wave.Validate(out error), Is.True, error);
        Assert.That(wave.LastUsedFullSolve, Is.False); Assert.That(wave.LastSolvedCells, Is.LessThan(active / 2));
        foreach (var cell in document.Grid.cells.Where(c => !changed.Contains(c))) Assert.That(wave.Choice(cell), Is.EqualTo(initial[cell]));
    }

    [Test]
    public void MultiFrameStrokeUndoAndRedoRestoreExactChoices()
    {
        var document = Document(); document.LoadExample(); var wave = new BuildingWaveFunction(document.Grid, Library());
        var dirty = new List<Cell>(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        var original = document.Grid.cells.Select(wave.Choice).ToArray();
        document.BeginStroke();
        foreach (int column in new[] { 0, 1, 2, 3 })
        { document.SetBuildingPoint(column, 3, true); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed); }
        document.EndStroke(); var edited = document.Grid.cells.Select(wave.Choice).ToArray();
        document.Undo(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        Assert.That(wave.Validate(out var error), Is.True, error); CollectionAssert.AreEqual(original, document.Grid.cells.Select(wave.Choice));
        document.Redo(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        Assert.That(wave.Validate(out error), Is.True, error); CollectionAssert.AreEqual(edited, document.Grid.cells.Select(wave.Choice));
    }
    [Test]
    public void LockedChoicePropagatesAlongGraphWithoutRandomObservation()
    {
        var domains = new[] { new[] { 0 }, new[] { 0, 1 }, new[] { 0, 1 } };
        var graph = new[] { new[] { 1 }, new[] { 0, 2 }, new[] { 1 } };
        var result = new WaveFunctionSolver().Solve(domains, graph, (a, av, b, bv) => av == bv, _ => 1, 42);
        Assert.That(result.Success, Is.True); CollectionAssert.AreEqual(new[] { 0, 0, 0 }, result.Choices);
        Assert.That(result.Observations, Is.Zero); Assert.That(result.Propagations, Is.EqualTo(2));
        CollectionAssert.AreEqual(new[] { 0, 1 }, domains[1]);
    }
    [Test]
    public void WeightedObservationBacktracksFromAContradictionToAValidAlternative()
    {
        var domains = new[] { new[] { 0, 1 }, new[] { 2, 3 }, new[] { 2, 3 } };
        var graph = new[] { new[] { 1, 2 }, new[] { 0, 2 }, new[] { 0, 1 } };
        bool Rule(int a, int av, int b, int bv)
        {
            if (a == 0) return av == 1 || bv == 2;
            if (b == 0) return bv == 1 || av == 2;
            return av != bv;
        }
        var result = new WaveFunctionSolver().Solve(domains, graph, Rule, choice => choice == 0 ? 1e12 : 1, 42);
        Assert.That(result.Success, Is.True); Assert.That(result.Choices[0], Is.EqualTo(1));
        Assert.That(result.Choices[1], Is.Not.EqualTo(result.Choices[2])); Assert.That(result.Backtracks, Is.GreaterThan(0));
    }
    [Test]
    public void UnsatisfiableOddCycleReturnsNoPartialAssignment()
    {
        var domains = new[] { new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 1 } };
        var graph = new[] { new[] { 1, 2 }, new[] { 0, 2 }, new[] { 0, 1 } };
        var result = new WaveFunctionSolver().Solve(domains, graph, (a, av, b, bv) => av != bv, _ => 1, 15);
        Assert.That(result.Success, Is.False); Assert.That(result.Choices, Is.Null); Assert.That(result.Backtracks, Is.GreaterThan(0));
    }
    [Test]
    public void SearchBudgetAndEmptyDomainsAreExplicitFailures()
    {
        var domains = new[] { new[] { 0, 1 }, new[] { 0, 1 } }; var graph = new[] { Array.Empty<int>(), Array.Empty<int>() };
        var result = new WaveFunctionSolver().Solve(domains, graph, (a, av, b, bv) => true, _ => 1, 42, 1);
        Assert.That(result.Success, Is.False); Assert.That(result.BudgetExceeded, Is.True); Assert.That(result.Choices, Is.Null);
        result = new WaveFunctionSolver().Solve(new[] { Array.Empty<int>() }, new[] { Array.Empty<int>() }, (a, av, b, bv) => true, _ => 1, 42);
        Assert.That(result.Success, Is.False); Assert.That(result.Choices, Is.Null);
    }
    [Test]
    public void FixedSeedsReproduceResultsAndDifferentSeedsExploreCandidates()
    {
        var domains = Enumerable.Range(0, 12).Select(_ => new[] { 0, 1, 2 }).ToArray();
        var graph = domains.Select(_ => Array.Empty<int>()).ToArray(); var solver = new WaveFunctionSolver();
        var first = solver.Solve(domains, graph, (a, av, b, bv) => true, _ => 1, 42);
        var second = solver.Solve(domains, graph, (a, av, b, bv) => true, _ => 1, 42);
        var third = solver.Solve(domains, graph, (a, av, b, bv) => true, _ => 1, 143);
        CollectionAssert.AreEqual(first.Choices, second.Choices); Assert.That(first.Choices.SequenceEqual(third.Choices), Is.False);
        Assert.Throws<ArgumentException>(() => solver.Solve(domains, graph, (a, av, b, bv) => true, _ => 0, 42));
    }
    private static ModelLibrary Library() => AssetDatabase.LoadAssetAtPath<ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset");
    private static UnifiedGridDocument Document(int radius = 6)
    {
        var previous = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(42);
            var grid = new HexGrid(radius, 2, new Vector3(0, -2.7f, 0), 1.35f, 14);
            grid.RandomMergeTriangles(); grid.SubdivideGrid(); grid.RelaxGrid(5); grid.BuildLayers(); grid.BuildCells();
            return new UnifiedGridDocument(grid);
        }
        finally { UnityEngine.Random.state = previous; }
    }
    [Test]
    public void CatalogRetainsMultipleRealSourceTransformsForOccupiedStates()
    {
        var catalog = new BuildingWfcCatalog(Library());
        Assert.That(catalog.Variants.Count, Is.GreaterThan(254)); Assert.That(catalog.SocketCount, Is.GreaterThan(1));
        for (int state = 1; state < 255; state++)
        {
            Assert.That(catalog.Candidates((byte)state), Does.Contain(catalog.Canonical((byte)state)));
            foreach (int candidate in catalog.Candidates((byte)state)) Assert.That(catalog.Variants[candidate].Model.stateCode, Is.EqualTo(state));
        }
        Assert.That(Enumerable.Range(1, 254).Count(state => catalog.Candidates((byte)state).Count > 1), Is.GreaterThan(0));
    }
    [Test]
    public void RealLibraryCollapsesEverySharedFaceWithoutChangingOccupancyOrTerrain()
    {
        var document = Document(); document.LoadExample(); var before = document.ExportJson();
        var wave = new BuildingWaveFunction(document.Grid, Library()); var dirty = new List<Cell>(); document.DrainDirty(dirty);
        wave.Update(dirty, document.WfcSeed);
        Assert.That(wave.Validate(out string error), Is.True, error); Assert.That(wave.LastFallbacks, Is.Zero);
        Assert.That(wave.LastSolvedCells, Is.GreaterThan(0)); Assert.That(wave.LastObservations, Is.GreaterThan(0));
        Assert.That(document.ExportJson(), Is.EqualTo(before));
        foreach (var cell in document.Grid.cells.Where(c => c.GetCellByte() != 0 && c.GetCellByte() != 255))
            Assert.That(wave.Selected(cell).stateCode, Is.EqualTo(cell.GetCellByte()));
    }
    [Test]
    public void LocalEditPreservesOtherBuildingsAndUndoRestoresTheOriginalCollapse()
    {
        var document = Document(); document.LoadExample(); var wave = new BuildingWaveFunction(document.Grid, Library());
        var dirty = new List<Cell>(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        var initial = document.Grid.cells.ToDictionary(c => c, wave.Choice); int initialCount = wave.LastSolvedCells;
        int column = Enumerable.Range(0, document.ColumnCount).OrderBy(i =>
            (document.Grid.baseLayer.subVertices[i].position - new Vector3(-7, -2.7f, -3)).sqrMagnitude).First();
        var vertex = document.Grid.layers[2].subVertices[column]; document.SetBuildingPoint(column, 2, !vertex.IsEnabled);
        document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        Assert.That(wave.Validate(out string error), Is.True, error); Assert.That(wave.LastSolvedCells, Is.LessThan(initialCount));
        foreach (var cell in document.Grid.cells.Where(c => c.Center.x > 3)) Assert.That(wave.Choice(cell), Is.EqualTo(initial[cell]));
        document.Undo(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        foreach (var cell in document.Grid.cells) Assert.That(wave.Choice(cell), Is.EqualTo(initial[cell]));
        wave.Update(Array.Empty<Cell>(), document.WfcSeed); Assert.That(wave.LastSolvedCells, Is.GreaterThan(0)); // idle keeps the last report
    }
    [Test]
    public void RerollSeedIsUndoableAndSavedAlongsideTheUnchangedBuildingData()
    {
        var document = Document(); document.LoadExample(); var wave = new BuildingWaveFunction(document.Grid, Library());
        var dirty = new List<Cell>(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        var original = document.ExportJson(); var choices = document.Grid.cells.Select(wave.Choice).ToArray();
        var occupied = document.Capture().enabled; document.RerollBuildingModules(); wave.Update(Array.Empty<Cell>(), document.WfcSeed);
        Assert.That(wave.Validate(out string error), Is.True, error);
        CollectionAssert.AreEqual(occupied, document.Capture().enabled);
        Assert.That(choices.SequenceEqual(document.Grid.cells.Select(wave.Choice)), Is.False);
        var clone = Document(); clone.ImportJson(document.ExportJson()); Assert.That(clone.WfcSeed, Is.EqualTo(document.WfcSeed));
        document.Undo(); document.DrainDirty(dirty); wave.Update(dirty, document.WfcSeed);
        Assert.That(document.ExportJson(), Is.EqualTo(original)); CollectionAssert.AreEqual(choices, document.Grid.cells.Select(wave.Choice));
    }
}
