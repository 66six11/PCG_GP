using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarchingCube;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class MeshImporterWindow : EditorWindow
    {
        private ModelLibrary targetLibrary;
        private List<Object> selectedAssets = new List<Object>();
        private Vector2 scrollPos;
        private bool includeSubmeshes = true;
        private bool autoUpdateStateCodes = true;

        [MenuItem("Tools/Marching Cube/Batch Mesh Importer")]
        public static void ShowWindow()
        {
            GetWindow<MeshImporterWindow>("Mesh Importer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Model Library Settings", EditorStyles.boldLabel);

            // 选择目标ScriptableObject
            EditorGUILayout.BeginHorizontal();
            targetLibrary = (ModelLibrary)EditorGUILayout.ObjectField(
                "Target Library",
                targetLibrary,
                typeof(ModelLibrary),
                false);

            if (GUILayout.Button("Create New", GUILayout.Width(100)))
            {
                CreateNewLibrary();
            }

            EditorGUILayout.EndHorizontal();

            // 选项设置
            GUILayout.Space(10);
            GUILayout.Label("导入选项", EditorStyles.boldLabel);
            includeSubmeshes = EditorGUILayout.Toggle("包括子网格体", includeSubmeshes);
            autoUpdateStateCodes = EditorGUILayout.Toggle("自动更新状态代码", autoUpdateStateCodes);

            // 显示当前选择的资产
            GUILayout.Space(10);
            GUILayout.Label($"选定的资产({selectedAssets.Count})", EditorStyles.boldLabel);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
            foreach (var asset in selectedAssets)
            {
                EditorGUILayout.ObjectField(asset, typeof(Object), false);
            }

            EditorGUILayout.EndScrollView();

            // 操作按钮
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新选择"))
            {
                RefreshSelection();
            }

            GUI.enabled = targetLibrary != null && selectedAssets.Count > 0;
            if (GUILayout.Button("导入到库", GUILayout.Height(30)))
            {
                ImportAssets();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void OnSelectionChange()
        {
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            selectedAssets.Clear();

            // 获取所有选中的资产
            var selectedObjects = Selection.objects;

            // 过滤出Mesh和FBX文件
            foreach (var obj in selectedObjects)
            {
                if (obj is Mesh || obj is GameObject)
                {
                    selectedAssets.Add(obj);
                }
                else
                {
                    // 检查是否是FBX文件
                    var path = AssetDatabase.GetAssetPath(obj);
                    if (!string.IsNullOrEmpty(path) &&
                        (Path.GetExtension(path).ToLower() == ".fbx" ||
                         Path.GetExtension(path).ToLower() == ".obj"))
                    {
                        selectedAssets.Add(obj);
                    }
                }
            }

            Repaint();
        }

        private void CreateNewLibrary()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "创建模型库",
                "ModelLibrary.asset",
                "asset",
                "选择保存位置");

            if (!string.IsNullOrEmpty(path))
            {
                targetLibrary = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(targetLibrary, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = targetLibrary;
            }
        }

        private void ImportAssets()
        {
            if (targetLibrary == null) return;

            Undo.RecordObject(targetLibrary, "导入网格");

            int addedCount = 0;

            foreach (var asset in selectedAssets)
            {
                if (asset is Mesh mesh)
                {
                    // 直接添加Mesh
                    AddMeshToLibrary(mesh);
                    addedCount++;
                }
                else if (asset is GameObject prefab)
                {
                    // 处理Prefab中的Mesh
                    addedCount += ProcessGameObject(prefab);
                }
                else
                {
                    // 处理FBX文件路径
                    var path = AssetDatabase.GetAssetPath(asset);
                    if (!string.IsNullOrEmpty(path))
                    {
                        addedCount += ProcessFBXFile(path);
                    }
                }
            }

            if (autoUpdateStateCodes)
            {
                targetLibrary.UpdateStateCode();
            }

            EditorUtility.SetDirty(targetLibrary);
            Debug.Log($"成功将 {addedCount} 网格导入库");
        }

        private int ProcessGameObject(GameObject gameObject)
        {
            int added = 0;

            // 获取Prefab中的所有MeshFilter
            var meshFilters = gameObject.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in meshFilters)
            {
                if (filter.sharedMesh != null)
                {
                    AddMeshToLibrary(filter.sharedMesh);
                    added++;

                    // 处理子网格
                    if (includeSubmeshes && filter.sharedMesh.subMeshCount > 1)
                    {
                        for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                        {
                            var submesh = CreateSubmesh(filter.sharedMesh, i);
                            AddMeshToLibrary(submesh);
                            added++;
                        }
                    }
                }
            }

            // 获取Prefab中的所有SkinnedMeshRenderer
            var skinnedRenderers = gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var renderer in skinnedRenderers)
            {
                if (renderer.sharedMesh != null)
                {
                    AddMeshToLibrary(renderer.sharedMesh);
                    added++;

                    // 处理子网格
                    if (includeSubmeshes && renderer.sharedMesh.subMeshCount > 1)
                    {
                        for (int i = 0; i < renderer.sharedMesh.subMeshCount; i++)
                        {
                            var submesh = CreateSubmesh(renderer.sharedMesh, i);
                            AddMeshToLibrary(submesh);
                            added++;
                        }
                    }
                }
            }

            return added;
        }

        private int ProcessFBXFile(string path)
        {
            int added = 0;

            // 加载FBX中的所有资产
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            foreach (var asset in assets)
            {
                if (asset is Mesh mesh)
                {
                    AddMeshToLibrary(mesh);
                    added++;
                }
            }

            return added;
        }

        private void AddMeshToLibrary(Mesh mesh)
        {
            // 检查是否已存在
            if (targetLibrary.originalMesh.All(m => m.mesh != mesh))
            {
                var modelInfo = new ModelInfo(mesh);
                targetLibrary.originalMesh.Add(modelInfo);
            }
        }

        private Mesh CreateSubmesh(Mesh sourceMesh, int submeshIndex)
        {
            // 创建子网格副本
            var submesh = new Mesh();
            submesh.name = $"{sourceMesh.name}_Submesh_{submeshIndex}";

            // 获取子网格的三角形
            int[] triangles = sourceMesh.GetTriangles(submeshIndex);
            submesh.SetTriangles(triangles, 0);

            // 复制顶点数据
            submesh.vertices = sourceMesh.vertices;
            submesh.normals = sourceMesh.normals;
            submesh.tangents = sourceMesh.tangents;
            submesh.uv = sourceMesh.uv;
            submesh.uv2 = sourceMesh.uv2;
            submesh.uv3 = sourceMesh.uv3;
            submesh.uv4 = sourceMesh.uv4;
            submesh.colors = sourceMesh.colors;
            submesh.colors32 = sourceMesh.colors32;
            submesh.bindposes = sourceMesh.bindposes;
            submesh.boneWeights = sourceMesh.boneWeights;

            return submesh;
        }
    }
}