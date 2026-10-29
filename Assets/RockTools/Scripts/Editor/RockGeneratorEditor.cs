using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RockTools
{
    [CustomEditor(typeof(RockGenerator))]
    public class RockGeneratorEditor : UnityEditor.Editor
    {
        private const string KPropertyPathRandomSeed = "rndSeed";
        private const bool KOptimize = true;

        private RockGenerator rockGen;
        private SerializedProperty randomSeed;
        private SerializedProperty rockType;
        private SerializedProperty material;
        private readonly LogicEditorBase[] rockEditors = new LogicEditorBase[RockTypeExtensions.RockTypesLenght];

        private int tmpRandomSeed;
        private ERockType tmpRockType;
        private Object tmpMaterial;

        private const bool AddCollider = false;

        private void OnEnable()
        {
            rockGen = target as RockGenerator;

            InitializeProperties();

            SceneView.duringSceneGui += DuringSceneGui;

            UpdateTmpValues();
        }

        private void InitializeProperties()
        {
            randomSeed = serializedObject.FindProperty("rndSeed");
            rockType = serializedObject.FindProperty("type");
            material = serializedObject.FindProperty("material");
        }

        private void UpdateTmpValues()
        {
            tmpRandomSeed = randomSeed.intValue;
            tmpRockType = (ERockType) rockType.intValue;
            tmpMaterial = material.objectReferenceValue;
            InitializeRockTypeEditors();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            ShutDownRockTypeEditors();
        }

        public override void OnInspectorGUI()
        {
            if (rockGen == null)
            {
                return;
            }

            if (!rockGen.isActiveAndEnabled)
            {
                EditorGUILayout.HelpBox("Please enable rock generator's game object before editing!", MessageType.Warning);
            }

            EditorGUI.BeginDisabledGroup(!rockGen.isActiveAndEnabled);

            serializedObject.Update();
            DrawProperties();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Bake", EditorStyles.boldLabel);
            //addCollider = EditorGUILayout.Toggle("Add Collider", addCollider);

            if (GUILayout.Button("Bake"))
            {
                PreBake(Bake);
                GUIUtility.ExitGUI();
                return;
            }

            if (tmpRandomSeed != randomSeed.intValue)
            {
                rockGen.UpdateRock();
            }
            else if (tmpRockType != (ERockType) rockType.intValue)
            {
                rockGen.UpdateRock();
            }
            else if (tmpMaterial != material.objectReferenceValue)
            {
                rockGen.UpdateMaterials();
            }

            UpdateTmpValues();

            EditorGUI.EndDisabledGroup();
        }

        private void DrawProperties()
        {
            DrawRandomSeedField();

            var iterator = serializedObject.GetIterator();
            var propertyToExclude = new[] {"m_Script", KPropertyPathRandomSeed, "logic"};
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (!propertyToExclude.Contains(iterator.name))
                {
                    EditorGUILayout.PropertyField(iterator, true);
                }
            }

            rockEditors[rockGen.type.GetTypeIndex()]?.OnInspectorGUI();
        }

        private void InitializeRockTypeEditors()
        {
            for (var i = 0; i < rockEditors.Length; i++)
            {
                if (ReferenceEquals(rockEditors[i], null))
                {
                    rockEditors[i] = CreateEditor(rockGen.logics[i]) as LogicEditorBase;
                    rockEditors[i].OnPropertyChanged += () => { rockGen.UpdateRock(); };
                }
            }
        }

        private void ShutDownRockTypeEditors()
        {
            foreach (var editor in rockEditors)
            {
                if (!ReferenceEquals(editor, null))
                {
                    DestroyImmediate(editor);
                }
            }
        }

        private void DrawRandomSeedField()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Random Seed");

            if (GUILayout.Button("Randomize"))
            {
                rockGen.Randomize(1);
            }

            rockGen.rndSeed = EditorGUILayout.IntField(rockGen.rndSeed);
            EditorGUILayout.EndHorizontal();
        }

        private void DuringSceneGui(SceneView obj)
        {
            if (rockGen != null)
            {
                var vertexCount = rockGen.GetVertexCount();
                if (vertexCount >= 0)
                {
                    Handles.BeginGUI();
                    GUILayout.BeginArea(new Rect(20, 20, 300, 60));
                    GUILayout.BeginVertical("Box");
                    GUILayout.Label($"{rockGen.name}");
                    GUILayout.Label($"Vertex Count (Before Bake): {vertexCount}");
                    GUILayout.EndVertical();
                    GUILayout.EndArea();
                    Handles.EndGUI();
                }
            }
        }

        private async void PreBake(Action<string> preBakeDone)
        {
            var scenePath = SceneManager.GetActiveScene().path;

            // check if scene has a valid path
            if (string.IsNullOrEmpty(scenePath))
            {
                if (EditorUtility.DisplayDialog("The untitled scene needs saving",
                        "You need to save the scene before baking rock.", "Save Scene", "Cancel"))
                    scenePath = EditorUtility.SaveFilePanel("Save Scene", "Assets/", "", "unity");

                scenePath = FileUtil.GetProjectRelativePath(scenePath);

                if (string.IsNullOrEmpty(scenePath))
                {
                    Debug.LogWarning("Scene was not saved, bake canceled.");
                    return;
                }

                var saveOk = EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), scenePath);

                if (!saveOk)
                {
                    Debug.LogWarning("Scene was not saved, bake canceled.");
                    return;
                }

                AssetDatabase.Refresh();
                await Task.Delay(100);
            }

            scenePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath))
            {
                return;
            }

            var assetPath = $"{Path.ChangeExtension(scenePath, null)}-generated-mesh/baked-rock.asset";
            var assetDir = Path.GetDirectoryName(assetPath);

            if (string.IsNullOrEmpty(assetDir))
            {
                return;
            }

            if (!Directory.Exists(assetDir))
            {
                Directory.CreateDirectory(assetDir);
                AssetDatabase.Refresh();
                await Task.Delay(100);
            }

            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            preBakeDone.Invoke(assetPath);
        }

        private void Bake(string path)
        {
            var bakedMeshFilter = new GameObject("Baked-Rock").AddComponent<MeshFilter>();
            var bakedMeshRenderer = bakedMeshFilter.gameObject.AddComponent<MeshRenderer>();
            bakedMeshRenderer.sharedMaterial = rockGen.pMeshRenderer.sharedMaterial;
            var parameters = new BakeParameters {addCollider = AddCollider, path = path, mergeVerticesThreshold = 0.1f, generateSecondaryUVSet = true, optimize = KOptimize};
            RockBaker.Bake(rockGen, parameters, bakedMeshFilter);
        }
    }
}
