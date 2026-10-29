#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MarchingCube;
using UnityEditor;
using UnityEngine;
using Utility;

namespace Editor
{
    public sealed class ModelBindingReport
    {
        public readonly List<ModelInfo> Models = new List<ModelInfo>();
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>Bind persistent model subassets; material submeshes remain part of their parent mesh.</summary>
    public static class ModelLibraryBinding
    {
        public const string DefaultSource = "Assets/Art/BaseModel/PCG.fbx";
        public const string DefaultLibrary = "Assets/Art/BaseModel/ModelLibrary.asset";

        public static ModelBindingReport Scan(IReadOnlyList<string> paths)
        {
            var report = new ModelBindingReport();
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            var seenStates = new Dictionary<byte, string>();
            foreach (var path in paths)
            {
                if (!seenPaths.Add(path)) continue;
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                { report.Errors.Add($"Select a model file, not a scene object or folder: '{path}'."); continue; }
                int found = 0;
                foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().OrderBy(m => m.name, StringComparer.Ordinal))
                {
                    if (!ModelHelper.TryGetStateCode(source.name, out byte state))
                    { report.Warnings.Add($"Ignored '{path}/{source.name}': expected an eight-bit binary suffix."); continue; }
                    found++;
                    if (seenStates.TryGetValue(state, out var previous))
                    { report.Errors.Add($"Duplicate state {state}: '{previous}' and '{path}/{source.name}'."); continue; }
                    seenStates.Add(state, path + "/" + source.name);
                    if (state != 0 && state != 255 && source.vertexCount == 0)
                        report.Errors.Add($"Nonempty state {state} has no vertices: {source.name}.");
                    var bounds = source.bounds;
                    if (bounds.min.x < -0.501f || bounds.max.x > 0.501f || bounds.min.z < -0.501f
                        || bounds.max.z > 0.501f || bounds.min.y < -0.501f || bounds.max.y > 0.501f)
                        report.Errors.Add($"'{source.name}' is outside the normalized [-0.5, 0.5] template domain.");
                    if (!source.isReadable) report.Warnings.Add($"Read/Write will be enabled for '{path}'.");
                    report.Models.Add(new ModelInfo(state, source));
                }
                if (found == 0) report.Errors.Add($"No named state meshes found in '{path}'.");
            }
            report.Models.Sort((a, b) => a.stateCode.CompareTo(b.stateCode));
            if (report.Errors.Count == 0)
            {
                try { ModelStateTable.Build(report.Models); }
                catch (Exception error) { report.Errors.Add(error.Message); }
            }
            return report;
        }

        public static ModelBindingReport Bind(ModelLibrary library, IReadOnlyList<string> paths)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            var preview = Scan(paths);
            if (!preview.IsValid) throw new InvalidOperationException(string.Join("\n", preview.Errors));
            foreach (var path in paths.Distinct(StringComparer.Ordinal))
            {
                if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
                { importer.isReadable = true; importer.SaveAndReimport(); }
            }
            // Reimport can replace subasset objects, so acquire all references again.
            var report = Scan(paths);
            if (!report.IsValid) throw new InvalidOperationException(string.Join("\n", report.Errors));
            if (report.Models.Any(model => !model.mesh.isReadable))
                throw new InvalidOperationException("Source meshes must be readable for runtime deformation.");
            library.ReplaceSourceModels(report.Models);
            return report;
        }

        public static List<string> Validate(ModelLibrary library)
        {
            var errors = new List<string>();
            if (library == null) { errors.Add("Select a model library."); return errors; }
            var seen = new HashSet<byte>();
            foreach (var model in library.Meshes)
            {
                if (!seen.Add(model.stateCode)) errors.Add($"Duplicate state {model.stateCode}.");
                if (model.stateCodeString != ModelHelper.Byte2State(model.stateCode))
                    errors.Add($"State string is stale for {model.stateCode}.");
                if (model.stateCode == 0 || model.stateCode == 255)
                {
                    if (model.mesh != null) errors.Add($"Empty surface state {model.stateCode} must have no mesh.");
                    continue;
                }
                if (model.mesh == null) { errors.Add($"State {model.stateCode} has no source mesh."); continue; }
                if (!AssetDatabase.Contains(model.mesh)) errors.Add($"State {model.stateCode} references a transient mesh.");
                if (!model.mesh.isReadable) errors.Add($"State {model.stateCode} references an unreadable mesh.");
                if (!ModelHelper.TryGetStateCode(model.mesh.name, out byte source))
                { errors.Add($"State {model.stateCode} has an invalid source name."); continue; }
                bool consistent = false;
                for (int turns = 0; turns < 4; turns++)
                {
                    var transform = new CubeTransform(turns, model.mirrorX);
                    if (Quaternion.Angle(model.rotation, transform.Rotation) < 0.01f
                        && transform.TransformState(source) == model.stateCode) consistent = true;
                }
                if (!consistent) errors.Add($"State {model.stateCode} does not match its source transform.");
            }
            for (int i = 0; i < 256; i++) if (!seen.Contains((byte)i)) errors.Add($"Missing state {i}.");
            return errors;
        }

        [MenuItem("Tools/Marching Cube/Rebind PCG Library")]
        public static void RebindDefault()
        {
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(DefaultLibrary);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(library, DefaultLibrary);
            }
            var report = Bind(library, new[] { DefaultSource });
            Debug.Log($"Bound {report.Models.Count} PCG source meshes; all 256 states are available.", library);
            Selection.activeObject = library;
        }
    }
}
#endif
