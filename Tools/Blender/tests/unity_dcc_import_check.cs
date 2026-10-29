// Copy into the isolated validation project's Assets/Editor and run Run in batch mode.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Editor;
using HexagonalGrids.Prototype;
using MarchingCube;
using UnityEditor;
using UnityEngine;

public static class DccProjectJsonCheck
{
    [Serializable] public sealed class LogicalModule { public string id, logicalId; }
    [Serializable] public sealed class LogicalManifest { public LogicalModule[] modules; }
    [Serializable] public sealed class Report {
        public string blendPath, jsonPath, directBlendBindingError;
        public int blendMeshes, blendVertices, embeddedTextAssets, modules, variants, boolStates, faceCandidates;
        public bool blendImported, jsonImported, meshBindingPassed, cornersPassed, weightsPassed, facesPassed, reimportStable;
        public string[] logicalIds, importedMeshNames;
    }
    private static void Require(bool value,string message) { if(!value)throw new Exception(message); }
    public static void Run()
    {
        var report=new Report {
            blendPath="Assets/DirectBlendCheck/PCG_DCC_Demo.blend",
            jsonPath="Assets/Art/Buildings/DCC/DCCModules.tileset.json"
        };
        AssetDatabase.ImportAsset(report.blendPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var blendAssets=AssetDatabase.LoadAllAssetsAtPath(report.blendPath);
        var blendMeshes=blendAssets.OfType<Mesh>().ToArray();
        report.blendMeshes=blendMeshes.Length;report.blendVertices=blendMeshes.Sum(m=>m.vertexCount);
        report.embeddedTextAssets=blendAssets.OfType<TextAsset>().Count();
        report.blendImported=AssetImporter.GetAtPath(report.blendPath) is ModelImporter && blendMeshes.Length>0;
        report.importedMeshNames=blendMeshes.Select(m=>m.name).ToArray();
        Require(report.blendImported,"Blender project did not import any mesh");

        AssetDatabase.ImportAsset(report.jsonPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var text=AssetDatabase.LoadAssetAtPath<TextAsset>(report.jsonPath);
        Require(text!=null,"JSON did not import as TextAsset");report.jsonImported=true;
        var source=JsonUtility.FromJson<BuildingTilesetSource>(text.text);
        var logical=JsonUtility.FromJson<LogicalManifest>(text.text);
        report.logicalIds=logical.modules.Select(m=>m.logicalId).ToArray();
        report.modules=source.modules.Length;
        var tileset=BuildingTilesetImporter.Compile(report.jsonPath);
        Require(tileset.diagnostics.Length==0,string.Join(";",tileset.diagnostics));
        report.variants=tileset.variants.Length;
        report.boolStates=tileset.variants.Select(v=>v.model.stateCode).Distinct().Count();
        foreach(var module in source.modules) {
            var variants=tileset.variants.Where(v=>v.sourceId==module.id).ToArray();
            int count=module.yawDegrees.Length*(module.allowMirrorX?2:1);
            Require(variants.Length==count,"Wrong variant count: "+module.id);
            Require(Math.Abs(variants.Sum(v=>v.weight)-module.weight)<1e-5f,"Lost base weight: "+module.id);
            byte state=0;for(int i=0;i<8;i++)state|=(byte)(module.corners[i]<<i);
            foreach(var v in variants) {
                Require(v.model.mesh!=null && v.model.mesh.name.StartsWith(module.meshName,StringComparison.Ordinal),"Wrong mesh binding: "+v.id);
                var transform=new CubeTransform(Mathf.RoundToInt(v.model.rotation.eulerAngles.y/90)%4,v.model.mirrorX);
                Require(v.model.stateCode==transform.TransformState(state),"Wrong corners: "+v.id);
                for(int face=0;face<6;face++) {
                    var expected=module.neighbors.Get(face);
                    var actual=v.neighbors.Get(BuildingTilesetCompiler.TransformFace(face,transform));
                    Require(actual.Length==expected.Length,"Lost face candidates: "+v.id);
                    for(int i=0;i<actual.Length;i++)Require(actual[i].id==expected[i].id && actual[i].weight==expected[i].weight,"Wrong candidate ID/weight: "+v.id);
                }
            }
            report.faceCandidates+=Enumerable.Range(0,6).Sum(f=>module.neighbors.Get(f).Length);
        }
        report.meshBindingPassed=report.cornersPassed=report.weightsPassed=report.facesPassed=true;
        string output=report.jsonPath.Substring(0,report.jsonPath.Length-5)+".asset";
        string guid=AssetDatabase.AssetPathToGUID(output),hash=tileset.sourceHash;
        tileset=BuildingTilesetImporter.Compile(report.jsonPath);
        report.reimportStable=guid==AssetDatabase.AssetPathToGUID(output) && hash==tileset.sourceHash;
        Require(report.reimportStable,"Unchanged JSON/model changed compiled identity or source hash");

        // Native .blend is a scene import, not the source-only export: measure
        // whether its meshes can bind this exact manifest rather than assume so.
        var direct=JsonUtility.FromJson<BuildingTilesetSource>(text.text);
        var importer=(ModelImporter)AssetImporter.GetAtPath(report.blendPath);
        importer.isReadable=true;importer.bakeAxisConversion=true;importer.SaveAndReimport();
        blendMeshes=AssetDatabase.LoadAllAssetsAtPath(report.blendPath).OfType<Mesh>().ToArray();
        try {
            BuildingTilesetCompiler.Compile(direct,name=>blendMeshes.SingleOrDefault(m=>m.name==name));
            report.directBlendBindingError="";
        } catch(Exception e) { report.directBlendBindingError=e.Message; }
        string path=Path.GetFullPath("../../DCCRebuild/unity-project-json-report.json");
        File.WriteAllText(path,JsonUtility.ToJson(report,true));
        Debug.Log("DCC_PROJECT_JSON_CHECK_OK: blendMeshes="+report.blendMeshes+", sourceModules="+report.modules+", variants="+report.variants+", boolStates="+report.boolStates+", candidates="+report.faceCandidates+", embeddedTextAssets="+report.embeddedTextAssets+", directBlendBound="+string.IsNullOrEmpty(report.directBlendBindingError));
    }
}
