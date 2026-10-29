using System.Collections.Generic;
using System.Linq;
using HexagonalGrids;
using Job;
using MarchingCube;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class MeshDeformationTests
{
    private readonly List<Object> cleanup = new List<Object>();
    private T Own<T>(T value) where T : Object { cleanup.Add(value); return value; }
    [TearDown] public void Cleanup()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
            if (cleanup[i] != null) Object.DestroyImmediate(cleanup[i]);
        cleanup.Clear();
    }

    private Mesh CreateMesh()
    {
        var mesh = Own(new Mesh { name = "Template", indexFormat = IndexFormat.UInt32 });
        mesh.vertices = new[] { new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f) };
        mesh.subMeshCount = 2;
        mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
        mesh.SetTriangles(new[] { 0, 2, 3 }, 1);
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.SetUVs(3, new List<Vector4> { Vector4.one, Vector4.zero, Vector4.one * 2, Vector4.one * 3 });
        mesh.colors = new[] { Color.red, Color.green, Color.blue, Color.white };
        mesh.RecalculateNormals(); mesh.RecalculateTangents();
        return mesh;
    }

    private MeshDeformation CreateOwner()
    {
        var go = Own(new GameObject("Model"));
        return go.AddComponent<MeshDeformation>();
    }

    private static readonly Vector3 A = new Vector3(-2, 0, 2);
    private static readonly Vector3 B = new Vector3(3, 0, 1);
    private static readonly Vector3 C = new Vector3(2, 0, -1);
    private static readonly Vector3 D = new Vector3(-1, 0, -2);

    private static Vector3 Warp(Vector3 v, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float height)
    {
        float u = Mathf.Clamp01(v.x + 0.5f), w = Mathf.Clamp01(v.z + 0.5f);
        var result = Vector3.Lerp(Vector3.Lerp(d, c, u), Vector3.Lerp(a, b, u), w);
        result.y = v.y * height;
        return result;
    }

    [Test]
    public void RepeatedDeformationReusesMeshAndPreservesSourceAndSubMeshes()
    {
        var source = CreateMesh();
        var original = source.vertices;
        var owner = CreateOwner();
        var output = owner.Apply(source, Quaternion.identity, A, B, C, D, 2);
        for (int i = 0; i < 30; i++)
        {
            var a = A + Vector3.right * (i * 0.01f);
            Assert.That(owner.Apply(source, Quaternion.identity, a, B, C, D, 2), Is.SameAs(output));
            var actual = output.vertices;
            for (int v = 0; v < original.Length; v++)
                Assert.That(Vector3.Distance(actual[v], Warp(original[v], a, B, C, D, 2)), Is.LessThan(0.00001f));
        }
        CollectionAssert.AreEqual(original, source.vertices);
        Assert.That(output.subMeshCount, Is.EqualTo(2));
        Assert.That(output.indexFormat, Is.EqualTo(IndexFormat.UInt32));
        for (int i = 0; i < 2; i++) CollectionAssert.AreEqual(source.GetTriangles(i), output.GetTriangles(i));
        CollectionAssert.AreEqual(source.colors, output.colors);
        var uv = new List<Vector4>(); var expectedUv = new List<Vector4>();
        output.GetUVs(3, uv); source.GetUVs(3, expectedUv);
        CollectionAssert.AreEqual(expectedUv, uv);
        Assert.That(output.GetVertexAttributeDimension(VertexAttribute.TexCoord3), Is.EqualTo(4));
    }

    [TestCase(0,false),TestCase(90,false),TestCase(180,false),TestCase(270,false)]
    [TestCase(0,true),TestCase(90,true),TestCase(180,true),TestCase(270,true)]
    public void ModelOrientationIsAppliedBeforeWarping(int degrees, bool mirrorX)
    {
        var source = CreateMesh();
        var library = Own(ScriptableObject.CreateInstance<ModelLibrary>());
        var rotation = Quaternion.Euler(0, degrees, 0);
        library.mesh.Add(new ModelInfo(1, source, rotation, mirrorX));
        var cell = CreateCell(); cell.V1.IsEnabled = true;
        var owner = CreateOwner();
        var model = new Model(library, cell, owner.GetComponent<MeshFilter>());
        var builder = new ModelBuilder(model);
        builder.Build();
        var output = model.Mesh;
        var actual = output.vertices;
        var input = source.vertices;
        for (int i = 0; i < input.Length; i++)
        {
            var point = input[i]; if (mirrorX) point.x = -point.x;
            var expected = Warp(rotation * point, cell.localV1, cell.localV2, cell.localV3, cell.localV4, 2);
            Assert.That(Vector3.Distance(actual[i], expected), Is.LessThan(0.0001f));
        }
        Assert.That(Quaternion.Angle(model.gameObject.transform.rotation, cell.rotation), Is.LessThan(0.001f));
        Assert.That(output.normals.All(n => n.y > 0), Is.True);
        builder.Build();
        Assert.That(model.Mesh, Is.SameAs(output));
    }

    private static Cell CreateCell()
    {
        var edges = new EdgeCollection();
        var bottom = new Quad(new Vertex(A), new Vertex(B), new Vertex(C), new Vertex(D), edges);
        var top = new Quad(new Vertex(A + Vector3.up * 2), new Vertex(B + Vector3.up * 2),
            new Vertex(C + Vector3.up * 2), new Vertex(D + Vector3.up * 2), edges);
        return new Cell(top, bottom, edges);
    }

    [Test]
    public void EmptyStatesHideAndRestoreWithoutReallocatingTheMesh()
    {
        var source = CreateMesh();
        var library = Own(ScriptableObject.CreateInstance<ModelLibrary>());
        library.mesh.Add(new ModelInfo(1, source));
        var cell = CreateCell(); cell.V1.IsEnabled = true;
        var owner = CreateOwner();
        var model = new Model(library, cell, owner.GetComponent<MeshFilter>());
        var builder = new ModelBuilder(model); builder.Build();
        var output = model.Mesh;
        cell.V1.IsEnabled = false; builder.Build();
        Assert.That(model.MeshFilter.sharedMesh, Is.Null);
        foreach (var vertex in cell.vertices) vertex.IsEnabled = true;
        builder.Build(); Assert.That(model.MeshFilter.sharedMesh, Is.Null);
        foreach (var vertex in cell.vertices) vertex.IsEnabled = false;
        cell.V1.IsEnabled = true; builder.Build();
        Assert.That(model.MeshFilter.sharedMesh, Is.SameAs(output));
        var updatedUv = new[] { Vector2.one, Vector2.up, Vector2.zero, Vector2.right };
        source.uv = updatedUv;
        library.InvalidateRuntimeCache(); builder.Build();
        Assert.That(output == null, Is.True);
        CollectionAssert.AreEqual(updatedUv, model.Mesh.uv);
    }

    [Test]
    public void LookupRefreshesAfterAuthoringChanges()
    {
        var source = CreateMesh();
        var library = Own(ScriptableObject.CreateInstance<ModelLibrary>());
        library.mesh.Add(new ModelInfo(1, source));
        Assert.That(library.GetModel(1).Value.mesh, Is.SameAs(source));
        library.mesh.Add(new ModelInfo(2, source));
        Assert.That(library.GetModel(2).Value.mesh, Is.SameAs(source));
        library.mesh[0] = new ModelInfo(3, source);
        library.InvalidateRuntimeCache();
        Assert.That(library.GetModel(1), Is.Null);
        Assert.That(library.GetModel(3).Value.mesh, Is.SameAs(source));
        library.Clear(); Assert.That(library.GetModel(3), Is.Null);
    }

    [Test]
    public void DestroyingOwnerReleasesOnlyGeneratedMesh()
    {
        var source = CreateMesh(); var owner = CreateOwner();
        var output = owner.Apply(source, Quaternion.identity, A, B, C, D, 2);
        Object.DestroyImmediate(owner.gameObject);
        Assert.That(output == null, Is.True);
        Assert.That(source != null, Is.True);
    }

    [Test]
    public void ChangingTemplateReleasesPreviousOutput()
    {
        var source = CreateMesh(); var other = CreateMesh(); var owner = CreateOwner();
        var output = owner.Apply(source, Quaternion.identity, A, B, C, D, 2);
        var next = owner.Apply(other, Quaternion.identity, A, B, C, D, 2);
        Assert.That(output == null, Is.True);
        Assert.That(next != null, Is.True);
        Assert.That(source != null && other != null, Is.True);
    }

    [TestCase(FlipAxis.X)]
    [TestCase(FlipAxis.Y)]
    [TestCase(FlipAxis.Z)]
    public void ReflectionPreservesSubMeshesAndFlipsTangentHandedness(FlipAxis axis)
    {
        var source = CreateMesh(); var flipped = Own(MeshModifyJob.FlipMeshWithJob(source, axis));
        Assert.That(flipped.indexFormat, Is.EqualTo(source.indexFormat));
        Assert.That(flipped.subMeshCount, Is.EqualTo(2));
        var expected = source.vertices; var actual = flipped.vertices;
        for (int i = 0; i < expected.Length; i++)
        {
            expected[i][(int)axis] = -expected[i][(int)axis];
            Assert.That(actual[i], Is.EqualTo(expected[i]));
            Assert.That(flipped.tangents[i].w, Is.EqualTo(-source.tangents[i].w));
        }
        for (int sub = 0; sub < 2; sub++)
        {
            var triangles = source.GetTriangles(sub);
            (triangles[0], triangles[2]) = (triangles[2], triangles[0]);
            CollectionAssert.AreEqual(triangles, flipped.GetTriangles(sub));
        }
    }

    [Test]
    public void MeshWithoutUvCanBeDeformed()
    {
        var source = CreateMesh(); source.uv = null; var owner = CreateOwner();
        Assert.That(owner.Apply(source, Quaternion.identity, A, B, C, D, 2).vertexCount, Is.EqualTo(4));
    }

    [Test]
    public void InteriorVertexMatchesBilinearReferenceAndCompatibilityApi()
    {
        var source = Own(new Mesh());
        source.vertices = new[] { new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.1f, -0.2f, -0.15f) };
        source.triangles = new[] { 0, 1, 4, 1, 2, 4, 2, 3, 4, 3, 0, 4 };
        var owner = CreateOwner();
        var output = owner.Apply(source, Quaternion.identity, A, B, C, D, 3);
        var oneShot = Own(MeshModifyJob.TransformMeshJob(source, A, B, C, D, 3));
        for (int i = 0; i < source.vertexCount; i++)
        {
            var expected = Warp(source.vertices[i], A, B, C, D, 3);
            Assert.That(Vector3.Distance(output.vertices[i], expected), Is.LessThan(0.00001f));
            Assert.That(Vector3.Distance(oneShot.vertices[i], expected), Is.LessThan(0.00001f));
        }
    }

    [Test]
    public void PoolReusesOutputAndClearReleasesResources()
    {
        var source = CreateMesh();
        var poolGo = Own(new GameObject("Pool"));
        var pool = poolGo.AddComponent<ModelPool>();
        pool.PreWarm();
        pool.PreWarm();
        Assert.That(pool.CountInactive, Is.EqualTo(pool.PreWarmCount));
        Assert.That(poolGo.GetComponentsInChildren<ModelPool>(true).Length, Is.EqualTo(1));
        var model = pool.Get();
        var output = model.Deformation.Apply(source, Quaternion.identity, A, B, C, D, 2);
        pool.Release(model);
        Assert.That(model.MeshFilter.sharedMesh, Is.Null);
        var acquired = new List<Model>(); Model reused = null;
        // The pool is FIFO, so consume the prewarmed entries before the released model returns.
        for (int i = 0; i < pool.PreWarmCount; i++)
        {
            var next = pool.Get(); acquired.Add(next);
            if (ReferenceEquals(next, model)) reused = next;
        }
        Assert.That(reused, Is.SameAs(model));
        Assert.That(reused.Deformation.Apply(source, Quaternion.identity, A, B, C, D, 2), Is.SameAs(output));
        pool.Clear();
        Assert.That(output == null, Is.True);
        Assert.That(source != null, Is.True);
        Assert.That(pool.CountActive + pool.CountInactive, Is.EqualTo(0));
    }
}
