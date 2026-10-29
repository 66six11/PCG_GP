#if UNITY_EDITOR
using System;
using System.Linq;
using HexagonalGrids;
using HexagonalGrids.Prototype;
using HexagonalGrids.Render;
using HexagonalGrids.Test;
using MarchingCube;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    public static class GridPrototypeSetup
    {
        public static void SetupSampleScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SetupCurrentScene();
        }

        [MenuItem("Tools/Marching Cube/Prototype/Setup Current Scene %#F8")]
        public static void SetupCurrentScene()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before configuring the scene.");
            var scene = SceneManager.GetActiveScene();
            var generators = UnityEngine.Object.FindObjectsByType<GirdGenerator>()
                .Where(g => g.gameObject.scene == scene).ToArray();
            if (generators.Length != 1) throw new InvalidOperationException("The active scene must contain exactly one enabled grid generator.");
            var generator = generators[0];
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ModelLibraryBinding.DefaultLibrary);
            var errors = ModelLibraryBinding.Validate(library);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure PCG prototype");
            var generatorSettings = new SerializedObject(generator);
            generatorSettings.FindProperty("layerCount").intValue = Mathf.Max(4, generatorSettings.FindProperty("layerCount").intValue);
            generatorSettings.FindProperty("debug").boolValue = false;
            generatorSettings.ApplyModifiedProperties();
            var prototype = UnityEngine.Object.FindObjectsByType<GridPrototype>()
                .FirstOrDefault(p => p.gameObject.scene == scene);
            if (prototype == null)
            {
                var obj = new GameObject("PCG Prototype"); Undo.RegisterCreatedObjectUndo(obj, "Create PCG prototype");
                SceneManager.MoveGameObjectToScene(obj, scene);
                prototype = Undo.AddComponent<GridPrototype>(obj);
            }
            var camera = Camera.main;
            if (camera == null)
            {
                var obj = new GameObject("Main Camera"); Undo.RegisterCreatedObjectUndo(obj, "Create prototype camera");
                obj.tag = "MainCamera"; camera = Undo.AddComponent<Camera>(obj);
                SceneManager.MoveGameObjectToScene(obj, scene);
            }
            var surface = GetMaterial("PrototypeSurface", "Universal Render Pipeline/Lit", new Color(0.22f, 0.68f, 0.78f));
            var grid = GetMaterial("PrototypeGrid", "Universal Render Pipeline/Unlit", new Color(0.23f, 0.34f, 0.40f));
            var points = GetMaterial("PrototypePoints", "Universal Render Pipeline/Unlit", new Color(0.98f, 0.69f, 0.24f));
            Undo.RecordObject(prototype, "Configure prototype references");
            prototype.Configure(generator, library, surface, grid, points, camera);
            EditorUtility.SetDirty(prototype);
            foreach (var test in UnityEngine.Object.FindObjectsByType<VisualMesh>(FindObjectsInactive.Include))
            {
                if (test.gameObject.scene != scene) continue;
                Undo.RecordObject(test, "Disable old mesh preview"); test.enabled = false;
                if (test.modelGo != null)
                { Undo.RecordObject(test.modelGo, "Hide old mesh preview"); test.modelGo.SetActive(false); }
            }
            var oldQuery = generator.GetComponent<KdTreeTest>();
            if (oldQuery != null)
            {
                Undo.RecordObject(oldQuery, "Disable query preview"); oldQuery.enabled = false;
                if (oldQuery.queryGameObject != null)
                { Undo.RecordObject(oldQuery.queryGameObject, "Hide query preview"); oldQuery.queryGameObject.SetActive(false); }
            }
            var oldGridRender = generator.GetComponent<GridRender>();
            if (oldGridRender != null) { Undo.RecordObject(oldGridRender, "Disable old grid renderer"); oldGridRender.enabled = false; }
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); Selection.activeGameObject = prototype.gameObject;
            Debug.Log("PCG prototype ready. Press Play to build, erase, change height and undo edits.", prototype);
        }

        private static Material GetMaterial(string name, string shaderName, Color color)
        {
            string path = "Assets/Art/BaseModel/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{shaderName}' is unavailable.");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
#endif
