#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MarchingCube;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class MeshImporterWindow : EditorWindow
    {
        private ModelLibrary library;
        private UnityEngine.Object source;
        private readonly List<string> paths = new List<string>();
        private ModelBindingReport report;
        private string status;
        private Vector2 scroll;

        [MenuItem("Tools/Marching Cube/Batch Mesh Importer")]
        public static void ShowWindow() => GetWindow<MeshImporterWindow>("Marching Cube Models");

        private void OnEnable()
        {
            paths.Clear();
            library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ModelLibraryBinding.DefaultLibrary);
            source = AssetDatabase.LoadMainAssetAtPath(ModelLibraryBinding.DefaultSource);
            if (source != null) paths.Add(ModelLibraryBinding.DefaultSource);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("按模型名称绑定状态", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("模型名以 8 位 0/1 结尾，依次对应 V1–V8。读取模型文件内的 Mesh，保留材质子网格。", MessageType.Info);
            library = (ModelLibrary)EditorGUILayout.ObjectField("模型库", library, typeof(ModelLibrary), false);
            EditorGUI.BeginChangeCheck();
            source = EditorGUILayout.ObjectField("源模型文件", source, typeof(UnityEngine.Object), false);
            if (EditorGUI.EndChangeCheck())
            {
                paths.Clear(); report = null;
                if (source != null) paths.Add(AssetDatabase.GetAssetPath(source));
            }
            if (GUILayout.Button("使用 Project 中选中的模型文件"))
            {
                paths.Clear();
                paths.AddRange(Selection.objects.Select(AssetDatabase.GetAssetPath).Where(p => !string.IsNullOrEmpty(p)).Distinct());
                report = null;
            }
            foreach (var path in paths) EditorGUILayout.LabelField(path);
            using (new EditorGUI.DisabledScope(paths.Count == 0))
            {
                if (GUILayout.Button("扫描名称与检查覆盖"))
                { report = ModelLibraryBinding.Scan(paths); status = null; }
            }
            using (new EditorGUI.DisabledScope(library == null || report == null || !report.IsValid))
            {
                if (GUILayout.Button("绑定并保存全部 256 状态"))
                {
                    try
                    {
                        report = ModelLibraryBinding.Bind(library, paths);
                        status = $"已绑定 {report.Models.Count} 个源模型，已保存全部 256 状态。";
                    }
                    catch (Exception error) { status = error.Message; Debug.LogException(error); }
                }
            }
            using (new EditorGUI.DisabledScope(library == null))
            {
                if (GUILayout.Button("校验已保存的模型库"))
                {
                    var errors = ModelLibraryBinding.Validate(library);
                    status = errors.Count == 0 ? "校验通过：256 状态完整，源资源与变换一致。" : string.Join("\n", errors);
                }
            }
            using (new EditorGUI.DisabledScope(library == null))
            {
                if (GUILayout.Button("检查共享面接缝"))
                {
                    var errors = ModelLibrarySeamValidator.Validate(library);
                    status = errors.Count == 0 ? "共享面轮廓检查通过。" : $"发现 {errors.Count} 处轮廓差异，详见 Console。";
                    if (errors.Count > 0) Debug.LogWarning(string.Join("\n", errors), library);
                }
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
            if (report == null) return;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var error in report.Errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (var warning in report.Warnings.Distinct()) EditorGUILayout.HelpBox(warning, MessageType.Warning);
            foreach (var model in report.Models)
                EditorGUILayout.LabelField($"{model.stateCode:D3}  {model.stateCodeString}  {model.mesh.name}");
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
