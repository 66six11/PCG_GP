using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using MarchingCube;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

public class ModelStateTests
{
    private static readonly Vector3[] Corners = {
        new Vector3(-0.5f,0.5f,0.5f),new Vector3(0.5f,0.5f,0.5f),
        new Vector3(0.5f,0.5f,-0.5f),new Vector3(-0.5f,0.5f,-0.5f),
        new Vector3(-0.5f,-0.5f,0.5f),new Vector3(0.5f,-0.5f,0.5f),
        new Vector3(0.5f,-0.5f,-0.5f),new Vector3(-0.5f,-0.5f,-0.5f) };

    [TestCase(0,false),TestCase(1,false),TestCase(2,false),TestCase(3,false)]
    [TestCase(0,true),TestCase(1,true),TestCase(2,true),TestCase(3,true)]
    public void EveryStateMatchesIndependentGeometricCornerPermutation(int turns, bool mirror)
    {
        var transform = new CubeTransform(turns, mirror);
        for (int state = 0; state < 256; state++)
        {
            int expected = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((state & (1 << i)) == 0) continue;
                var point = Corners[i]; if (mirror) point.x = -point.x;
                point = Quaternion.Euler(0, turns * 90, 0) * point;
                int corner = Array.FindIndex(Corners, c => Vector3.Distance(c, point) < 0.00001f);
                Assert.That(corner, Is.GreaterThanOrEqualTo(0));
                expected |= 1 << corner;
            }
            Assert.That(transform.TransformState((byte)state), Is.EqualTo((byte)expected), $"state {state}");
        }
    }

    [Test]
    public void ReflectionsAndRotationsObeyCubeSymmetries()
    {
        Assert.That(ModelHelper.FlipZ(1), Is.EqualTo(8));
        Assert.That(ModelHelper.FlipX(1), Is.EqualTo(2));
        for (int i = 0; i < 256; i++)
        {
            byte state = (byte)i;
            Assert.That(ModelHelper.FlipZ(ModelHelper.FlipZ(state)), Is.EqualTo(state));
            Assert.That(ModelHelper.FlipX(ModelHelper.FlipX(state)), Is.EqualTo(state));
            Assert.That(ModelHelper.FlipX(ModelHelper.FlipZ(state)), Is.EqualTo(ModelHelper.Rotate180(state)));
            Assert.That(ModelHelper.Rotate90(ModelHelper.Rotate270(state)), Is.EqualTo(state));
            Assert.That(ModelHelper.ConvertStateStringToByte(ModelHelper.Byte2State(state)), Is.EqualTo(state));
        }
    }

    [TestCase("base_10000000",1),TestCase("base_00001000",16),TestCase("11111111",255)]
    public void NamingUsesV1ToV8Order(string name, byte state)
    { Assert.That(ModelHelper.TryGetStateCode(name, out byte actual), Is.True); Assert.That(actual, Is.EqualTo(state)); }

    [TestCase("base_000000001"),TestCase("base_0101010"),TestCase("base_0000000x"),TestCase("base_00000001_Submesh_0")]
    public void InvalidNamesAreRejected(string name)
    { Assert.That(ModelHelper.TryGetStateCode(name, out _), Is.False); }

    [Test]
    public void ActualPcgSourcesCoverEveryStateWithoutGeneratedMeshAssets()
    {
        var scan = ModelLibraryBinding.Scan(new[] { ModelLibraryBinding.DefaultSource });
        Assert.That(scan.Errors, Is.Empty, string.Join("\n", scan.Errors));
        Assert.That(scan.Models.Count, Is.EqualTo(54));
        var table = ModelStateTable.Build(scan.Models);
        var reversed = ModelStateTable.Build(scan.Models.AsEnumerable().Reverse().ToList());
        Assert.That(table.States.Count, Is.EqualTo(256));
        for (int i = 0; i < 256; i++)
        {
            var model = table.States[i];
            Assert.That(model.stateCode, Is.EqualTo(i));
            Assert.That(model.stateCodeString, Is.EqualTo(ModelHelper.Byte2State((byte)i)));
            Assert.That(reversed.States[i].mesh, Is.SameAs(model.mesh));
            Assert.That(reversed.States[i].rotation, Is.EqualTo(model.rotation));
            Assert.That(reversed.States[i].mirrorX, Is.EqualTo(model.mirrorX));
            if (i == 0 || i == 255) { Assert.That(model.mesh, Is.Null); continue; }
            Assert.That(AssetDatabase.Contains(model.mesh), Is.True);
            Assert.That(model.mesh.isReadable, Is.True);
            byte state = ModelHelper.GetMeshStateCode(model.mesh);
            int actual = 0;
            for (int c = 0; c < 8; c++)
            {
                if ((state & (1 << c)) == 0) continue;
                var point = Corners[c]; if (model.mirrorX) point.x = -point.x;
                point = model.rotation * point;
                int index = Array.FindIndex(Corners, x => Vector3.Distance(point, x) < 0.0001f);
                actual |= 1 << index;
            }
            Assert.That(actual, Is.EqualTo(i));
        }
        foreach (var original in table.Originals.Where(m => m.stateCode != 0 && m.stateCode != 255))
        {
            var model = table.States[original.stateCode];
            Assert.That(model.mesh, Is.SameAs(original.mesh));
            Assert.That(model.mirrorX, Is.False);
            Assert.That(model.rotation, Is.EqualTo(Quaternion.identity));
        }
    }

    [Test]
    public void ActualPcgModelsAgreeOnEverySharedFaceContour()
    {
        var scan = ModelLibraryBinding.Scan(new[] { ModelLibraryBinding.DefaultSource });
        Assert.That(scan.Errors, Is.Empty, string.Join("\n", scan.Errors));
        var library = ScriptableObject.CreateInstance<ModelLibrary>();
        try
        {
            library.ReplaceSourceModels(scan.Models);
            var errors = ModelLibrarySeamValidator.Validate(library);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }
        finally { Object.DestroyImmediate(library); }
    }

    [Test]
    public void InvalidSourceSetsDoNotChangeExistingLibrary()
    {
        var library = ScriptableObject.CreateInstance<ModelLibrary>();
        var a = new Mesh { name = "a_10000000" }; var b = new Mesh { name = "b_10000000" };
        try
        {
            library.mesh.Add(new ModelInfo(7, a));
            Assert.Throws<ArgumentException>(() => library.ReplaceSourceModels(new[] { new ModelInfo(a), new ModelInfo(b) }));
            Assert.Throws<InvalidOperationException>(() => library.ReplaceSourceModels(new[] { new ModelInfo(a) }));
            Assert.That(library.mesh.Count, Is.EqualTo(1)); Assert.That(library.mesh[0].stateCode, Is.EqualTo(7));
        }
        finally { Object.DestroyImmediate(library); Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
    }

    [Test]
    public void BoundLibrarySurvivesSaveUnloadReloadAndRepeatedBinding()
    {
        string folder = "Assets/BindingTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        string path = folder + "/Library.asset";
        try
        {
            var library = ScriptableObject.CreateInstance<ModelLibrary>();
            AssetDatabase.CreateAsset(library, path);
            ModelLibraryBinding.Bind(library, new[] { ModelLibraryBinding.DefaultSource });
            Assert.That(ModelLibraryBinding.Validate(library), Is.Empty);
            int mirrorCount = library.mesh.Count(m => m.mirrorX);
            Assert.That(mirrorCount, Is.GreaterThan(0));
            Resources.UnloadAsset(library);
            library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(path);
            Assert.That(ModelLibraryBinding.Validate(library), Is.Empty);
            Assert.That(library.mesh.Count(m => m.mirrorX), Is.EqualTo(mirrorCount));
            ModelLibraryBinding.Bind(library, new[] { ModelLibraryBinding.DefaultSource });
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).Length, Is.EqualTo(1));
            Assert.That(ModelLibraryBinding.Validate(library), Is.Empty);
        }
        finally { AssetDatabase.DeleteAsset(folder); }
    }
}
