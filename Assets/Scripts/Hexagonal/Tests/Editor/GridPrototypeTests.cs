using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Editor;
using HexagonalGrids;
using HexagonalGrids.Prototype;
using MarchingCube;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class GridPrototypeTests
{
    private static HexGrid MakeGrid()
    {
        var grid = new HexGrid(2, 1, Vector3.zero, 1, 4);
        var state = UnityEngine.Random.state;
        UnityEngine.Random.InitState(10); grid.RandomMergeTriangles(); UnityEngine.Random.state = state;
        grid.SubdivideGrid(); grid.BuildLayers(); grid.BuildCells(); return grid;
    }

    [Test]
    public void StrokeUndoRedoAndDirtyCellsFollowOnlyChangedVertices()
    {
        var grid = MakeGrid(); var edit = new GridEditSession(grid);
        var vertex = grid.baseLayer.subVertices[0]; var expected = grid.Vertex2Cells(vertex);
        edit.BeginStroke();
        Assert.That(edit.SetVertex(vertex, true), Is.True);
        Assert.That(edit.SetVertex(vertex, true), Is.False);
        edit.EndStroke();
        Assert.That(edit.UndoCount, Is.EqualTo(1)); Assert.That(edit.DirtyCount, Is.EqualTo(expected.Count));
        var dirty = new List<Cell>(); edit.DrainDirtyCells(dirty); CollectionAssert.AreEquivalent(expected, dirty);
        Assert.That(edit.Undo(), Is.True); Assert.That(vertex.IsEnabled, Is.False);
        Assert.That(edit.Redo(), Is.True); Assert.That(vertex.IsEnabled, Is.True);
        edit.Undo(); edit.SetVertex(grid.baseLayer.subVertices[1], true);
        Assert.That(edit.RedoCount, Is.EqualTo(0));
        Assert.Throws<ArgumentException>(() => edit.SetVertex(new Vertex(), true));
    }

    [Test]
    public void NetZeroStrokeAndBulkClearAreUndoable()
    {
        var grid = MakeGrid(); var edit = new GridEditSession(grid); var vertex = grid.baseLayer.subVertices[0];
        edit.BeginStroke(); edit.SetVertex(vertex,true); edit.SetVertex(vertex,false); edit.EndStroke();
        Assert.That(edit.UndoCount, Is.EqualTo(0));
        edit.Replace(v => true); Assert.That(grid.allSubVertices.All(v=>v.IsEnabled), Is.True);
        edit.Replace(v => false); Assert.That(grid.allSubVertices.All(v=>!v.IsEnabled), Is.True);
        edit.Undo(); Assert.That(grid.allSubVertices.All(v=>v.IsEnabled), Is.True);
        edit.Redo(); Assert.That(grid.allSubVertices.All(v=>!v.IsEnabled), Is.True);
    }

    [Test]
    public void PrototypePicksEditsRendersUndoesAndReinitializesExistingGrid()
    {
        var source = new GameObject("Grid"); var host = new GameObject("Prototype"); var cameraObject = new GameObject("Camera");
        Material surface = null, lines = null;
        try
        {
            var generator = source.AddComponent<GirdGenerator>(); generator._hexGrid = MakeGrid();
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ModelLibraryBinding.DefaultLibrary);
            Assert.That(library, Is.Not.Null);
            surface = new Material(Shader.Find("Unlit/Color")); lines = new Material(Shader.Find("Unlit/Color"));
            var camera = cameraObject.AddComponent<Camera>();
            var prototype = host.AddComponent<GridPrototype>();
            prototype.Configure(generator,library,surface,lines,lines,camera,false);
            Assert.That(prototype.Initialize(), Is.True); Assert.That(prototype.ActiveModelCount, Is.EqualTo(0));
            var vertex = generator._hexGrid.layers[1].subVertices[0];
            Assert.That(prototype.TryPick(new Ray(vertex.position+Vector3.up*20,Vector3.down),1,out var picked), Is.True);
            Assert.That(picked, Is.SameAs(vertex));
            Assert.That(prototype.TryPick(new Ray(new Vector3(100,20,100),Vector3.down),1,out _), Is.False);
            prototype.Session.SetVertex(vertex,true); prototype.RefreshDirtyCells();
            Assert.That(prototype.ActiveModelCount, Is.EqualTo(generator._hexGrid.Vertex2Cells(vertex).Count));
            Assert.That(host.GetComponentsInChildren<MarchingCube.MeshDeformation>(true).Any(d=>d.Mesh!=null), Is.True);
            prototype.Session.Undo(); prototype.RefreshDirtyCells(); Assert.That(prototype.ActiveModelCount, Is.EqualTo(0));
            prototype.Session.Redo(); prototype.RefreshDirtyCells(); Assert.That(prototype.ActiveModelCount, Is.GreaterThan(0));
            prototype.LoadDemo(); prototype.RefreshDirtyCells(); Assert.That(prototype.ActiveModelCount, Is.GreaterThan(0));
            prototype.ClearContent(); prototype.RefreshDirtyCells(); Assert.That(prototype.ActiveModelCount, Is.EqualTo(0));
            prototype.SetLayer(999); Assert.That(prototype.ActiveLayer, Is.EqualTo(4));
            generator._hexGrid = MakeGrid(); Assert.That(prototype.Initialize(), Is.True);
            Assert.That(prototype.Session.Grid, Is.SameAs(generator._hexGrid));
        }
        finally
        {
            Object.DestroyImmediate(host); Object.DestroyImmediate(source); Object.DestroyImmediate(cameraObject);
            if(surface!=null) Object.DestroyImmediate(surface); if(lines!=null) Object.DestroyImmediate(lines);
        }
    }
    [UnityTest]
    public IEnumerator ConfiguredSceneRunsAndSupportsEditUndoAndClear()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        yield return new EnterPlayMode();
        yield return null;
        var prototype = Object.FindFirstObjectByType<GridPrototype>();
        Assert.That(prototype, Is.Not.Null);
        Assert.That(prototype.Session, Is.Not.Null);
        Assert.That(prototype.ActiveModelCount, Is.GreaterThan(0));
        int initial = prototype.ActiveModelCount;
        prototype.ClearContent(); yield return null;
        Assert.That(prototype.ActiveModelCount, Is.EqualTo(0));
        prototype.Session.Undo(); yield return null;
        Assert.That(prototype.ActiveModelCount, Is.EqualTo(initial));
        prototype.ClearContent(); yield return null;
        var vertex = prototype.Session.Grid.layers[1].subVertices[0];
        Assert.That(prototype.TryPick(new Ray(vertex.position + Vector3.up * 10, Vector3.down),1,out var picked), Is.True);
        prototype.Session.SetVertex(picked,true); yield return null;
        Assert.That(prototype.ActiveModelCount, Is.GreaterThan(0));
        prototype.Session.Undo(); yield return null;
        Assert.That(prototype.ActiveModelCount, Is.EqualTo(0));
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator LeavePlayModeAfterFailure()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

}
