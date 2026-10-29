#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HexagonalGrids.Prototype;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>Opt-in native .blend development imports. Never launches a converter itself.</summary>
    public sealed class DccBlendImporter : AssetPostprocessor
    {
        private string manifest,error;
        private readonly Dictionary<GameObject,string> modules=new Dictionary<GameObject,string>();
        public override uint GetVersion()=>2;
        public static bool Handles(string path)=>path.EndsWith(".blend",StringComparison.OrdinalIgnoreCase)
            && path.StartsWith("Assets/DCCSources/",StringComparison.OrdinalIgnoreCase);
        private void OnPreprocessModel()
        {
            if(!Handles(assetPath))return;
            var importer=(ModelImporter)assetImporter;
            importer.isReadable=true;importer.globalScale=1;importer.useFileScale=true;
            importer.bakeAxisConversion=true;importer.importAnimation=false;
        }
        private void OnPostprocessGameObjectWithUserProperties(GameObject go,string[] names,object[] values)
        {
            if(!Handles(assetPath))return;
            string Read(string name) { int i=Array.IndexOf(names,name);return i<0?null:values[i]?.ToString(); }
            string kind=Read("pcg_kind");
            if(kind=="manifest") {
                if(manifest!=null || error!=null) { error="Multiple DCC scenes/manifests in one .blend are not supported.";return; }
                manifest=Read("pcg_manifest_json");error=Read("pcg_transport_error");
                if(string.IsNullOrEmpty(manifest)&&string.IsNullOrEmpty(error))error="DCC manifest is empty.";
            }
            if(kind=="module")modules.Add(go,Read("pcg_uid"));
        }
        private void OnPostprocessModel(GameObject root)
        {
            if(!Handles(assetPath))return;
            var metadata=root.AddComponent<DccBlendMetadata>();
            metadata.manifestJson=manifest;
            if(string.IsNullOrEmpty(manifest)&&string.IsNullOrEmpty(error))error="No DCC snapshot found. Enable PCG DCC 2.1 and save the Blender project.";
            var bindings=new List<DccBlendBinding>();var seen=new HashSet<string>();
            try {
                if(!string.IsNullOrEmpty(error))throw new ArgumentException(error);
                var source=JsonUtility.FromJson<BuildingTilesetSource>(manifest);
                if(source?.modules==null || source.modules.Length!=modules.Count)throw new ArgumentException("DCC mesh/manifest count mismatch.");
                foreach(var pair in modules) {
                    if(string.IsNullOrEmpty(pair.Value)||!seen.Add(pair.Value))throw new ArgumentException("Duplicate or missing DCC UUID.");
                    var entry=source.modules.Single(m=>m.id==pair.Value);
                    var filter=pair.Key.GetComponent<MeshFilter>();
                    if(filter==null || filter.sharedMesh==null)throw new ArgumentException("Missing mesh: "+pair.Value);
                    // Mutate the import-owned mesh. Arbitrary clones created here
                    // are not registered as persistent model subassets by Unity.
                    var mesh=filter.sharedMesh;mesh.name=entry.meshName;
                    var matrix=root.transform.worldToLocalMatrix*pair.Key.transform.localToWorldMatrix;
                    var vertices=mesh.vertices;
                    for(int i=0;i<vertices.Length;i++)vertices[i]=matrix.MultiplyPoint3x4(vertices[i]);
                    mesh.vertices=vertices;
                    var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;
                    for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
                    mesh.normals=normals;
                    if(matrix.determinant<0)for(int sub=0;sub<mesh.subMeshCount;sub++) {
                        var indices=mesh.GetTriangles(sub);for(int i=0;i<indices.Length;i+=3)(indices[i+1],indices[i+2])=(indices[i+2],indices[i+1]);mesh.SetTriangles(indices,sub);
                    }
                    mesh.RecalculateBounds();mesh.RecalculateTangents();filter.sharedMesh=mesh;
                    pair.Key.transform.SetParent(root.transform,false);
                    pair.Key.transform.localPosition=Vector3.zero;pair.Key.transform.localRotation=Quaternion.identity;pair.Key.transform.localScale=Vector3.one;
                    pair.Key.name=entry.meshName;
                    var renderer=pair.Key.GetComponent<Renderer>();if(renderer!=null)renderer.enabled=true;
                    bindings.Add(new DccBlendBinding {uid=pair.Value,mesh=mesh});
                }
                metadata.bindings=bindings.OrderBy(b=>b.uid,StringComparer.Ordinal).ToArray();
            } catch(Exception e) { metadata.importError=e.Message;Debug.LogWarning("DCC blend snapshot invalid: "+assetPath+": "+e.Message); }
            // Only the transfer source meshes survive in the imported model hierarchy.
            var keep=new HashSet<GameObject>(modules.Keys);
            foreach(Transform child in root.transform.Cast<Transform>().ToArray())
                if(!keep.Contains(child.gameObject))UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }
}
#endif
