#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexagonalGrids.Prototype;
using MarchingCube;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    /// <summary>JSON is natively imported by Unity, so use a sidecar compiler,
    /// not a ScriptedImporter claiming the global .json extension.</summary>
    public sealed class BuildingTilesetImporter : AssetPostprocessor
    {
        private static readonly HashSet<string> Pending = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string,string> BlendMoves = new Dictionary<string,string>(StringComparer.Ordinal);
        private static bool scheduled;
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            for(int i=0;i<moved.Length;i++)
                if(DccBlendImporter.Handles(moved[i]) && DccBlendImporter.Handles(movedFrom[i]))BlendMoves[moved[i]]=movedFrom[i];
            foreach (string path in imported.Concat(moved))
            {
                if (path.EndsWith(".tileset.json", StringComparison.OrdinalIgnoreCase)) Pending.Add(path);
                if (DccBlendImporter.Handles(path)) Pending.Add(path);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tileset.json")) Pending.Add(file.Replace('\\','/'));
            }
            if (Pending.Count == 0 || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += Flush;
        }
        private static void Flush()
        {
            scheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                EditorApplication.playModeStateChanged -= ResumeAfterPlay;
                EditorApplication.playModeStateChanged += ResumeAfterPlay; return;
            }
            var paths = Pending.OrderBy(p=>p,StringComparer.Ordinal).ToArray(); Pending.Clear();
            foreach (string path in paths) try {
                if(DccBlendImporter.Handles(path)) {
                    if(BlendMoves.TryGetValue(path,out var oldPath)) {
                        BlendMoves.Remove(path);
                        string oldOutput=Path.ChangeExtension(oldPath,"tileset.asset"),newOutput=Path.ChangeExtension(path,"tileset.asset");
                        var old=AssetDatabase.LoadAssetAtPath<BuildingTileset>(oldOutput);
                        if(old!=null && old.sourceJson==oldPath) {
                            string failure=AssetDatabase.MoveAsset(oldOutput,newOutput);
                            if(!string.IsNullOrEmpty(failure))throw new InvalidOperationException(failure);
                        }
                    }
                    CompileBlend(path);
                } else Compile(path);
            } catch (Exception e) { Debug.LogError($"Tileset '{path}': {e.Message}. Previous valid asset retained."); }
        }
        private static void ResumeAfterPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= ResumeAfterPlay;
            if (!scheduled) { scheduled = true; EditorApplication.delayCall += Flush; }
        }

        [MenuItem("Tools/Marching Cube/Building Tileset/Compile Selected JSON")]
        private static void CompileSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            try { Selection.activeObject = Compile(path); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public static BuildingTileset Compile(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Compile building modules after leaving Play Mode.");
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".tileset.json", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Select a .tileset.json file.");
            string json = File.ReadAllText(path); var source = JsonUtility.FromJson<BuildingTilesetSource>(json);
            if (source == null || string.IsNullOrEmpty(source.modelFile) || Path.GetFileName(source.modelFile) != source.modelFile)
                throw new ArgumentException("modelFile must be a sibling FBX filename.");
            string modelPath = (Path.GetDirectoryName(path) + "/" + source.modelFile).Replace('\\','/');
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) throw new ArgumentException($"Missing FBX '{modelPath}'.");
            if (!importer.isReadable || Math.Abs(importer.globalScale - 1) > .0001f || !importer.bakeAxisConversion || !importer.useFileScale)
            {
                importer.isReadable = true; importer.globalScale = 1; importer.bakeAxisConversion = true; importer.useFileScale = true;
                importer.SaveAndReimport();
            }
            var meshes = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().ToArray();
            Mesh Lookup(string name) {
                var matches = meshes.Where(m=>m.name == name).ToArray();
                if (matches.Length != 1) throw new ArgumentException($"Expected one mesh '{name}' in '{modelPath}', found {matches.Length}.");
                return matches[0];
            }
            return CompileSource(source,Lookup,path,path.Substring(0,path.Length-5)+".asset",json+AssetDatabase.GetAssetDependencyHash(modelPath));
        }

        [MenuItem("Tools/Marching Cube/Building Tileset/Compile Selected Blend")]
        private static void CompileSelectedBlend()
        {
            try { Selection.activeObject=CompileBlend(AssetDatabase.GetAssetPath(Selection.activeObject)); }
            catch(Exception e) { Debug.LogException(e); }
        }

        public static BuildingTileset CompileBlend(string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Compile after leaving Play Mode.");
            if(!DccBlendImporter.Handles(path))throw new ArgumentException("Place the DCC .blend in Assets/DCCSources/.");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var metadata=model==null?null:model.GetComponent<DccBlendMetadata>();
            if(metadata==null)throw new ArgumentException("DCC metadata missing; reimport the .blend after saving with PCG DCC 2.1.");
            if(!string.IsNullOrEmpty(metadata.importError))throw new ArgumentException(metadata.importError);
            var source=JsonUtility.FromJson<BuildingTilesetSource>(metadata.manifestJson);
            if(source?.modules==null)throw new ArgumentException("Invalid DCC manifest.");
            var byUid=metadata.bindings.ToDictionary(b=>b.uid,b=>b.mesh,StringComparer.Ordinal);
            var byName=source.modules.ToDictionary(m=>m.meshName,m=>m.id,StringComparer.Ordinal);
            Mesh Lookup(string name)=>byUid.TryGetValue(byName[name],out var mesh)?mesh:null;
            return CompileSource(source,Lookup,path,Path.ChangeExtension(path,"tileset.asset"),metadata.manifestJson+AssetDatabase.GetAssetDependencyHash(path));
        }

        private static BuildingTileset CompileSource(BuildingTilesetSource source,Func<string,Mesh> Lookup,string path,string output,string fingerprint)
        {
            var variants = BuildingTilesetCompiler.Compile(source, Lookup);
            ValidateGeometry(variants);
            var covered = new HashSet<byte>(variants.Select(v=>v.model.stateCode));
            var diagnostics = new List<string>();
            int missing = Enumerable.Range(1,254).Count(s=>!covered.Contains((byte)s));
            if (missing > 0) diagnostics.Add($"Missing {missing} surface states. Unsupported states use the baseline library; connect them explicitly with $baseline or the component falls back.");
            var asset = AssetDatabase.LoadAssetAtPath<BuildingTileset>(output);
            if (asset == null && AssetDatabase.LoadMainAssetAtPath(output) != null) throw new ArgumentException($"Output already belongs to another asset: {output}");
            bool create = asset == null;
            if (create) asset = ScriptableObject.CreateInstance<BuildingTileset>();
            // Cook independent mesh snapshots. A malformed FBX reimport must not
            // mutate the geometry referenced by the previous valid tileset.
            var snapshots = new Dictionary<Mesh,Mesh>();
            foreach (var model in variants.Select(v=>v.model.mesh).Distinct()) {
                var snapshot = UnityEngine.Object.Instantiate(model); snapshot.name = model.name; snapshots.Add(model,snapshot);
            }
            var previousMeshes = create ? Array.Empty<Mesh>() : AssetDatabase.LoadAllAssetsAtPath(output).OfType<Mesh>().ToArray();
            // Publish only after every parse, mesh binding and geometry check passed.
            asset.schemaVersion = 1; asset.tilesetId = source.tilesetId; asset.sourceJson = path;
            asset.sourceHash = Hash128.Compute(fingerprint).ToString();
            if (create) AssetDatabase.CreateAsset(asset,output);
            foreach (var entry in snapshots.ToArray()) {
                var existing = previousMeshes.FirstOrDefault(m=>m.name==entry.Value.name);
                if (existing != null) {
                    EditorUtility.CopySerialized(entry.Value,existing);UnityEngine.Object.DestroyImmediate(entry.Value);snapshots[entry.Key]=existing;
                } else AssetDatabase.AddObjectToAsset(entry.Value,asset);
            }
            foreach (var variant in variants) {
                var model=variant.model; model.mesh=snapshots[model.mesh]; variant.model=model;
            }
            asset.variants = variants; asset.diagnostics = diagnostics.ToArray(); EditorUtility.SetDirty(asset);
            foreach (var previous in previousMeshes) if (!snapshots.ContainsValue(previous)) UnityEngine.Object.DestroyImmediate(previous,true);
            AssetDatabase.SaveAssets();
            Debug.Log($"BUILDING_TILESET_OK: {variants.Length} variants → {output}. " + string.Join(" ",diagnostics));
            return asset;
        }

        private static void ValidateGeometry(BuildingTileVariant[] variants)
        {
            // Reuse exactly the contour extractor used by runtime WFC.
            {
                // A small validation catalog needs no complete baseline library.
                var catalog = BuildingWfcCatalog.ForValidation(variants);
                for (int a = 0; a < variants.Length; a++) for (int face = 0; face < 6; face++)
                    foreach (var rule in variants[a].neighbors.Get(face)) {
                        int orientation = FaceOrientation(face);
                        if (rule.id == "$baseline") continue; // Checked against the actual baseline at runtime.
                        if (BuildingTilesetCompiler.Reserved(rule.id)) {
                            if (catalog.Socket(a,face,orientation) != 0) throw new ArgumentException($"{variants[a].id}/{BuildingTilesetCompiler.FaceNames[face]} has geometry against {rule.id}.");
                            if (rule.id != "$outside") foreach (int corner in Enumerable.Range(0,4)) {
                                bool occupied = (variants[a].model.stateCode & (1<<BuildingWfcCatalog.FaceCorners[face,corner])) != 0;
                                if (occupied != (rule.id == "$solid")) throw new ArgumentException($"{variants[a].id}: corner occupancy conflicts with {rule.id}.");
                            }
                            continue;
                        }
                        int other = Opposite(face); bool found = false;
                        for (int b = 0; b < variants.Length; b++) {
                            if (variants[b].sourceId != rule.id || !catalog.SemanticCompatible(a,face,b,other)) continue;
                            if (catalog.Socket(a,face,orientation) != catalog.Socket(b,other,FaceOrientation(other))) continue;
                            bool corners = true;
                            for (int k = 0; k < 4; k++) {
                                int ac = BuildingWfcCatalog.FaceCorners[face,k];
                                Vector3 p = CubeTransform.GetCorner(ac);
                                int axis = face < 2 ? 1 : face == 2 || face == 4 ? 2 : 0; p[axis] *= -1;
                                int bc = Enumerable.Range(0,8).First(i=>(CubeTransform.GetCorner(i)-p).sqrMagnitude < .001f);
                                if (((variants[a].model.stateCode>>ac)&1) != ((variants[b].model.stateCode>>bc)&1)) corners = false;
                            }
                            if (corners) { found = true; break; }
                        }
                        if (!found) throw new ArgumentException($"{variants[a].id}/{BuildingTilesetCompiler.FaceNames[face]} → {rule.id}: no reciprocal, equally weighted, geometrically matching orientation.");
                    }
            }
        }
        private static int Opposite(int face) => new[] {1,0,4,5,2,3}[face];
        private static int FaceOrientation(int face)
        {
            int Compare(int a, int b) {
                var x = CubeTransform.GetCorner(BuildingWfcCatalog.FaceCorners[face,a]); var y = CubeTransform.GetCorner(BuildingWfcCatalog.FaceCorners[face,b]);
                int c=x.x.CompareTo(y.x); if(c==0)c=x.y.CompareTo(y.y); if(c==0)c=x.z.CompareTo(y.z); return c;
            }
            int start = 0; for(int i=1;i<4;i++)if(Compare(i,start)<0)start=i;
            return start*2+(Compare((start+1)%4,(start+3)%4)>0?1:0);
        }
    }
}
#endif
