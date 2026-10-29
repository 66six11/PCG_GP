using System;
using System.IO;
using System.Linq;
using Editor;
using HexagonalGrids;
using HexagonalGrids.Prototype;
using MarchingCube;
using UnityEditor;
using UnityEngine;

public static class DccNativePipelineCheck
{
    static void Check(bool ok,string reason) { if(!ok)throw new Exception(reason); }
    static string Point(Vector3 p)=>$"{Mathf.RoundToInt(p.x*10000)},{Mathf.RoundToInt(p.y*10000)},{Mathf.RoundToInt(p.z*10000)}";
    static string[] Geometry(Mesh mesh)=>mesh.vertices.Select(Point).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
    static string[] Triangles(Mesh mesh)
    {
        var p=mesh.vertices.Select(Point).ToArray();var t=mesh.triangles;var result=new string[t.Length/3];
        for(int i=0;i<t.Length;i+=3) {
            string a=p[t[i]],b=p[t[i+1]],c=p[t[i+2]];
            result[i/3]=new[]{a+"/"+b+"/"+c,b+"/"+c+"/"+a,c+"/"+a+"/"+b}.OrderBy(x=>x,StringComparer.Ordinal).First();
        }
        return result.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
    }
    public static void Run()
    {
        const string path="Assets/DCCSources/PCG_DCC_Demo.blend";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var metadata=root.GetComponent<DccBlendMetadata>();
        Check(metadata!=null,"Missing native metadata");
        Debug.Log("DCC_NATIVE_METADATA: json="+(metadata.manifestJson?.Length??0)+", meshes="+metadata.bindings.Length+", error="+metadata.importError);
        foreach(var b in metadata.bindings.Take(2))Debug.Log("DCC_NATIVE_MESH: "+b.uid+" bounds="+b.mesh.bounds+" sample="+string.Join(";",b.mesh.vertices.Take(6).Select(Point)));
        var native=BuildingTilesetImporter.CompileBlend(path);
        var delivery=BuildingTilesetImporter.Compile("Assets/Art/Buildings/DCC/DCCModules.tileset.json");
        Check(metadata.bindings.Length==55,"Expected authored sources plus 53 canonical base families");
        Check(root.GetComponentsInChildren<MeshFilter>(true).Length==55,"Preview or author layout leaked into native import hierarchy");
        Check(native.diagnostics.Length==0 && native.variants.Length==delivery.variants.Length,"Native coverage mismatch");
        var right=delivery.variants.ToDictionary(v=>v.id);
        foreach(var a in native.variants) {
            var b=right[a.id];
            Check(a.logicalId==b.logicalId && !string.IsNullOrEmpty(a.logicalId),"Lost logical ID: "+a.id);
            Check(a.sourceId==b.sourceId && a.model.stateCode==b.model.stateCode && a.weight==b.weight,"Rules mismatch: "+a.id);
            for(int f=0;f<6;f++) {
                var ar=a.neighbors.Get(f);var br=b.neighbors.Get(f);
                Check(ar.Length==br.Length && ar.Zip(br,(x,y)=>x.id==y.id&&x.weight==y.weight).All(x=>x),"Face mismatch: "+a.id);
            }
            Check(Geometry(a.model.mesh).SequenceEqual(Geometry(b.model.mesh)),"Geometry differs: "+a.id);
            Check(Triangles(a.model.mesh).SequenceEqual(Triangles(b.model.mesh)),"Triangle topology/winding differs: "+a.id);
        }
        string output=Path.ChangeExtension(path,"tileset.asset"),guid=AssetDatabase.AssetPathToGUID(output),hash=native.sourceHash;
        native=BuildingTilesetImporter.CompileBlend(path);
        Check(guid==AssetDatabase.AssetPathToGUID(output)&&hash==native.sourceHash,"Recompile changed identity or source hash");
        Debug.Log("DCC_NATIVE_PIPELINE_OK: Modules=55, Variants="+native.variants.Length+", States="+native.variants.Select(v=>v.model.stateCode).Distinct().Count()+", NativeAndDeliveryGeometryAndRules=Equal, Recompile=Stable");
        Gameplay(native);Gameplay(delivery);
    }

    static void Gameplay(BuildingTileset tileset)
    {
        var saved=UnityEngine.Random.state;HexGrid grid;
        try { UnityEngine.Random.InitState(42);grid=new HexGrid(6,2,new Vector3(0,-2.7f,0),1.35f,14);grid.RandomMergeTriangles();grid.SubdivideGrid();grid.RelaxGrid(5);grid.BuildLayers();grid.BuildCells(); }
        finally { UnityEngine.Random.state=saved; }
        var doc=new UnifiedGridDocument(grid);doc.LoadExample();var dirty=new System.Collections.Generic.List<Cell>();doc.DrainDirty(dirty);
        var library=AssetDatabase.LoadAssetAtPath<ModelLibrary>("Assets/Art/BaseModel/ModelLibrary.asset");
        var wave=new BuildingWaveFunction(grid,library,tileset);
        foreach(int seed in new[]{42,143,244}) { wave.Update(dirty,seed);Check(wave.Validate(out var error)&&wave.LastFallbacks==0,error??wave.LastError); }
        Debug.Log("DCC_NATIVE_GAMEPLAY_OK: "+tileset.sourceJson+", Seeds=3, Fallbacks=0");
    }

    static int phase,ticks;
    static DateTime deadline;
    static string guid,roofUid,updatedHash,fixtures;
    static System.Collections.Generic.Dictionary<string,string> geometry;
    const string Blend="Assets/DCCSources/PCG_DCC_Demo.blend";
    const string Moved="Assets/DCCSources/Renamed.blend";
    static string Output(string path)=>Path.ChangeExtension(path,"tileset.asset");
    static void CopyFixture(string name)
    {
        ticks=0;File.Copy(Path.Combine(fixtures,name+".blend"),Blend,true);
        AssetDatabase.ImportAsset(Blend,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
    }
    public static void RunLifecycle()
    {
        try {
            Run();
            fixtures=Path.GetFullPath("../../DCCRebuild/native-fixtures");File.Copy(Blend,Path.Combine(fixtures,"Original.blend"),true);
            var initial=AssetDatabase.LoadAssetAtPath<BuildingTileset>(Output(Blend));
            guid=AssetDatabase.AssetPathToGUID(Output(Blend));roofUid=initial.variants.First(v=>v.logicalId=="building.roofs").sourceId;
            geometry=initial.variants.GroupBy(v=>v.sourceId).ToDictionary(g=>g.Key,g=>string.Join(";",Geometry(g.First().model.mesh)));
            phase=0;deadline=DateTime.UtcNow.AddSeconds(120);CopyFixture("Updated");
            EditorApplication.update+=Poll;
        } catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static void Poll()
    {
        try {
            if(DateTime.UtcNow>deadline)throw new Exception("Automatic native compilation timed out");
            if(++ticks<5)return;
            string path=phase==3?Moved:Blend;
            var asset=AssetDatabase.LoadAssetAtPath<BuildingTileset>(Output(path));
            if(phase==0) {
                if(asset==null || !asset.variants.Any(v=>v.logicalId=="building.roofs.updated.中文"))return;
                Check(AssetDatabase.AssetPathToGUID(Output(Blend))==guid,"Save update replaced compiled GUID");
                Check(Math.Abs(asset.variants.Where(v=>v.sourceId==roofUid).Sum(v=>v.weight)-2)<1e-5,"Saved base-weight update missing");
                foreach(var group in asset.variants.GroupBy(v=>v.sourceId))Check(geometry[group.Key]==string.Join(";",Geometry(group.First().model.mesh)),"Author layout TRS leaked into canonical geometry");
                updatedHash=asset.sourceHash;phase=1;CopyFixture("Invalid");
            } else if(phase==1) {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(Blend).GetComponent<DccBlendMetadata>();
                Check(!string.IsNullOrEmpty(model.importError),"Invalid save silently used stale native snapshot");
                bool failed=false;try { BuildingTilesetImporter.CompileBlend(Blend); }catch(ArgumentException) { failed=true; }
                Check(failed && asset.sourceHash==updatedHash,"Invalid snapshot replaced previous valid tileset");
                foreach(var group in asset.variants.GroupBy(v=>v.sourceId))Check(geometry[group.Key]==string.Join(";",Geometry(group.First().model.mesh)),"Invalid reimport mutated cooked meshes");
                phase=2;CopyFixture("Original");
            } else if(phase==2) {
                if(asset==null || !asset.variants.Any(v=>v.logicalId=="building.roofs"))return;
                Check(AssetDatabase.AssetPathToGUID(Output(Blend))==guid,"Recovery lost compiled GUID");
                phase=3;ticks=0;Check(string.IsNullOrEmpty(AssetDatabase.MoveAsset(Blend,Moved)),"Could not move source");
            } else if(phase==3) {
                if(asset==null || asset.sourceJson!=Moved)return;
                Check(AssetDatabase.AssetPathToGUID(Output(Moved))==guid,"Source move lost compiled GUID");
                phase=4;ticks=0;Check(string.IsNullOrEmpty(AssetDatabase.MoveAsset(Moved,Blend)),"Could not restore source");
            } else {
                if(asset==null || asset.sourceJson!=Blend)return;
                Check(AssetDatabase.AssetPathToGUID(Output(Blend))==guid,"Move back lost compiled GUID");
                EditorApplication.update-=Poll;
                Debug.Log("DCC_NATIVE_LIFECYCLE_OK: AutomaticSaveUpdate, LogicalIdAndWeightUpdate, StableUUID, LayoutTRSIgnored, InvalidSaveRetainsCookedMeshes, Recovery, MovePreservesGUID");
                EditorApplication.Exit(0);
            }
        } catch(Exception e) { EditorApplication.update-=Poll;Debug.LogException(e);EditorApplication.Exit(1); }
    }
}
