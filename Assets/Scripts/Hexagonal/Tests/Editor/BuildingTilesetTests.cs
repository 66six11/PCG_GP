using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Editor;
using HexagonalGrids;
using HexagonalGrids.Prototype;
using MarchingCube;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BuildingTilesetTests
{
    private Mesh mesh;
    [SetUp] public void Setup()
    {
        mesh=new Mesh { name="wall" };
        mesh.vertices=new[] {new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
        mesh.triangles=new[] {0,1,2,0,2,3};mesh.RecalculateBounds();
    }
    [TearDown] public void Teardown() { UnityEngine.Object.DestroyImmediate(mesh); }
    private BuildingModuleSource Module(string id="wall") => new BuildingModuleSource {
        id=id, meshName="wall", corners=new[] {1,1,0,0,1,1,0,0}, weight=8,
        yawDegrees=new[] {0,90,180,270},allowMirrorX=true,
        neighbors=new BuildingFaces {
            up=Array.Empty<BuildingNeighbor>(),down=Array.Empty<BuildingNeighbor>(),
            front=new[] {new BuildingNeighbor {id="$solid",weight=1}},right=Array.Empty<BuildingNeighbor>(),
            back=new[] {new BuildingNeighbor {id="$empty",weight=1}},left=Array.Empty<BuildingNeighbor>()
        }
    };
    private BuildingTilesetSource Source(params BuildingModuleSource[] modules) => new BuildingTilesetSource {
        schemaVersion=1,tilesetId="test",coordinateSystem="Unity_Y_Up",cornerOrder="V1_V2_V3_V4_V5_V6_V7_V8",modules=modules
    };

    [Test] public void SymmetriesRotateOccupancyFacesAndDivideModuleWeight()
    {
        var variants=BuildingTilesetCompiler.Compile(Source(Module()),_=>mesh);
        Assert.That(variants.Length,Is.EqualTo(8));Assert.That(variants.Sum(v=>v.weight),Is.EqualTo(8));
        foreach(var variant in variants) {
            int yaw=Mathf.RoundToInt(variant.model.rotation.eulerAngles.y/90);
            var transform=new CubeTransform(yaw,variant.model.mirrorX);
            Assert.That(variant.model.stateCode,Is.EqualTo(transform.TransformState(51)));
            int face=BuildingTilesetCompiler.TransformFace(2,transform);
            Assert.That(variant.neighbors.Get(face).Single().id,Is.EqualTo("$solid"));
        }
    }
    [Test] public void SameOccupancyVariantsHaveIndependentStableIds()
    {
        var variants=BuildingTilesetCompiler.Compile(Source(Module("plain"),Module("window")),_=>mesh);
        Assert.That(variants.Length,Is.EqualTo(16));Assert.That(variants.Select(v=>v.id).Distinct().Count(),Is.EqualTo(16));
        Assert.Throws<ArgumentException>(()=>BuildingTilesetCompiler.Compile(Source(Module(),Module()),_=>mesh));
    }
    [Test] public void BadReferencesAndTransformsNeverCompile()
    {
        var module=Module();module.neighbors.up=new[] {new BuildingNeighbor {id="missing",weight=1}};
        Assert.Throws<ArgumentException>(()=>BuildingTilesetCompiler.Compile(Source(module),_=>mesh));
        module=Module();module.yawDegrees=new[] {45};
        Assert.Throws<ArgumentException>(()=>BuildingTilesetCompiler.Compile(Source(module),_=>mesh));
        module=Module();module.weight=float.NaN;
        Assert.Throws<ArgumentException>(()=>BuildingTilesetCompiler.Compile(Source(module),_=>mesh));
    }
    [Test] public void PairWeightsBiasChoiceWithoutChangingAllowedDomains()
    {
        var domains=new[] {new[] {0},new[] {1,2}};var graph=new[] {new[] {1},new[] {0}};
        int preferred=0;
        for(int seed=0;seed<100;seed++) {
            var result=new WaveFunctionSolver().Solve(domains,graph,(a,av,b,bv)=>true,_=>1,seed,
                pairWeight:(a,av,b,bv)=>av==1||bv==1?1000:1);
            Assert.That(result.Success,Is.True);if(result.Choices[1]==1)preferred++;
        }
        Assert.That(preferred,Is.GreaterThan(95));CollectionAssert.AreEqual(new[] {1,2},domains[1]);
        preferred=0;
        for(int seed=0;seed<100;seed++) {
            var result=new WaveFunctionSolver().Solve(new[] {new[] {1,2}},new[] {Array.Empty<int>()},(a,av,b,bv)=>true,_=>1,seed,
                unaryWeight:(node,value)=>value==1?1000:1);
            if(result.Choices[0]==1)preferred++;
        }
        Assert.That(preferred,Is.GreaterThan(95),"Boundary/unary preferences must affect selection too.");
    }
    [Test] public void AuthoredModulesParticipateInRealGridAndContradictionsFallBackAsWholeComponents()
    {
        var library=AssetDatabase.LoadAssetAtPath<ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset");
        var previous=UnityEngine.Random.state;HexGrid grid;
        try {
            UnityEngine.Random.InitState(42);grid=new HexGrid(6,2,new Vector3(0,-2.7f,0),1.35f,14);
            grid.RandomMergeTriangles();grid.SubdivideGrid();grid.RelaxGrid(5);grid.BuildLayers();grid.BuildCells();
        } finally { UnityEngine.Random.state=previous; }
        var document=new UnifiedGridDocument(grid);document.LoadExample();
        byte state=grid.cells.Select(c=>c.GetCellByte()).Where(s=>s!=0&&s!=255).GroupBy(s=>s).OrderByDescending(g=>g.Count()).First().Key;
        BuildingNeighbor[] Rules() => new[] {"a","b","$baseline","$empty","$solid","$outside"}.Select(id=>new BuildingNeighbor {id=id,weight=1}).ToArray();
        var tileset=ScriptableObject.CreateInstance<BuildingTileset>();
        try {
            tileset.variants=new[] {"a","b"}.Select(id=>new BuildingTileVariant {
                id=id,sourceId=id,model=library.GetModel(state).Value,weight=1,
                neighbors=new BuildingFaces {up=Rules(),down=Rules(),front=Rules(),right=Rules(),back=Rules(),left=Rules()}
            }).ToArray();
            var dirty=new List<Cell>();document.DrainDirty(dirty);
            var wave=new BuildingWaveFunction(grid,library,tileset);wave.Update(dirty,42);
            Assert.That(wave.Validate(out var error),Is.True,error);Assert.That(wave.LastFallbacks,Is.Zero);
            Assert.That(grid.cells.Where(c=>c.GetCellByte()==state).All(c=>wave.Catalog.Variants[wave.Choice(c)].SourceId!=null),Is.True);
            foreach(var variant in tileset.variants) variant.neighbors=new BuildingFaces {
                up=Array.Empty<BuildingNeighbor>(),down=Array.Empty<BuildingNeighbor>(),front=Array.Empty<BuildingNeighbor>(),
                right=Array.Empty<BuildingNeighbor>(),back=Array.Empty<BuildingNeighbor>(),left=Array.Empty<BuildingNeighbor>()
            };
            wave=new BuildingWaveFunction(grid,library,tileset);wave.Update(dirty,42);
            Assert.That(wave.LastFallbacks,Is.GreaterThan(0));Assert.That(wave.Validate(out error),Is.True,error);
            Assert.That(grid.cells.Where(c=>c.GetCellByte()==state).All(c=>wave.Catalog.Variants[wave.Choice(c)].SourceId==null),Is.True);
        } finally { UnityEngine.Object.DestroyImmediate(tileset); }
    }
    [Test] public void ExportedBlenderBundleBindsMeshesAndPreservesLastGoodAssetOnFailure()
    {
        const string path="Assets/Art/Buildings/AuthoringDemo/BuildingKit.tileset.json";
        Assert.That(File.Exists(path),Is.True,"Generate Blender authoring demo first.");
        var asset=BuildingTilesetImporter.Compile(path);
        Assert.That(asset.variants.Length,Is.GreaterThan(8));
        Assert.That(asset.variants.All(v=>v.model.mesh!=null&&v.model.mesh.isReadable),Is.True);
        Assert.That(asset.variants.All(v=>v.model.mesh.vertices.Any(p=>Mathf.Abs(p.x)>.4999f||Mathf.Abs(p.y)>.4999f||Mathf.Abs(p.z)>.4999f)),Is.True);
        string output=path.Substring(0,path.Length-5)+".asset";
        Assert.That(asset.variants.All(v=>AssetDatabase.GetAssetPath(v.model.mesh)==output),Is.True,"Compiled meshes must survive malformed source reimports.");
        string guid=AssetDatabase.AssetPathToGUID(output),hash=asset.sourceHash,json=File.ReadAllText(path);
        try {
            File.WriteAllText(path,"{\"schemaVersion\":999}");
            Assert.Throws<ArgumentException>(()=>BuildingTilesetImporter.Compile(path));
            Assert.That(asset.sourceHash,Is.EqualTo(hash));
        } finally { File.WriteAllText(path,json); }
        var again=BuildingTilesetImporter.Compile(path);
        Assert.That(AssetDatabase.AssetPathToGUID(output),Is.EqualTo(guid));Assert.That(again.sourceHash,Is.EqualTo(hash));
    }
    [Test] public void CompleteBlenderBaseKitCoversAllStatesAndSolvesIrregularBuildingsWithoutFallback()
    {
        var tileset=BuildingTilesetImporter.Compile("Assets/Art/Buildings/BaseKit/BaseKit.tileset.json");
        Assert.That(tileset.variants.Select(v=>v.model.stateCode).Distinct().Count(),Is.EqualTo(254));
        Assert.That(tileset.diagnostics,Is.Empty);
        Assert.That(BuildingWfcCatalog.ForValidation(tileset.variants).SocketCount,Is.GreaterThan(1),"Geometry must reach the normalized faces.");
        var previous=UnityEngine.Random.state;HexGrid grid;
        try {
            UnityEngine.Random.InitState(42);grid=new HexGrid(6,2,new Vector3(0,-2.7f,0),1.35f,14);
            grid.RandomMergeTriangles();grid.SubdivideGrid();grid.RelaxGrid(5);grid.BuildLayers();grid.BuildCells();
        } finally { UnityEngine.Random.state=previous; }
        var document=new UnifiedGridDocument(grid);document.LoadExample();var dirty=new List<Cell>();document.DrainDirty(dirty);
        var library=AssetDatabase.LoadAssetAtPath<ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset");
        var wave=new BuildingWaveFunction(grid,library,tileset);
        foreach(int seed in new[] {42,143,244}) {
            wave.Update(dirty,seed);Assert.That(wave.Validate(out var error),Is.True,error);
            Assert.That(wave.LastFallbacks,Is.Zero,wave.LastError);
            Assert.That(grid.cells.Where(c=>c.GetCellByte()!=0&&c.GetCellByte()!=255).All(c=>wave.Catalog.Variants[wave.Choice(c)].SourceId!=null),Is.True);
        }
        var host=new GameObject("Imported tileset demo validation");
        try {
            var camera=host.AddComponent<Camera>();var demo=host.AddComponent<UnifiedGridBuildingDemo>();
            demo.Configure(library,camera,true,tileset);demo.Initialize();
            Assert.That(demo.ActiveModelCount,Is.GreaterThan(0));Assert.That(demo.WaveFunction.LastFallbacks,Is.Zero);
            Assert.That(demo.WaveFunction.Validate(out var error),Is.True,error);
            Assert.That(demo.GetComponentsInChildren<MeshCollider>().All(c=>c.sharedMesh!=null),Is.True);
        } finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
