#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HexagonalGrids.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Editor
{
    public static class HandPlacedRockClusterCapture
    {
        public const string TemplatePath = "Assets/Art/PCG/Rocks/HandPlacedCluster.asset";

        [MenuItem("Tools/Marching Cube/Terrain Demo/Capture Hand-Placed Cluster %&F8")]
        public static void Capture()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before capturing the reference cluster.");
            var library = CliffRockAssetBuilder.EnsureLibrary();
            var roots = SceneManager.GetActiveScene().GetRootGameObjects()
                .Where(go => go.GetComponent<MeshFilter>() != null && Array.IndexOf(library.meshes, go.GetComponent<MeshFilter>().sharedMesh) >= 0)
                .OrderByDescending(go => Array.IndexOf(library.meshes, go.GetComponent<MeshFilter>().sharedMesh) >= 4)
                .ThenByDescending(go => go.GetComponent<Renderer>().bounds.size.y).ToArray();
            if (roots.Length == 0) throw new InvalidOperationException("No top-level hand-placed rock models were found in the active scene.");
            var bounds = roots[0].GetComponent<Renderer>().bounds;
            foreach (var obj in roots) bounds.Encapsulate(obj.GetComponent<Renderer>().bounds);
            var tallCenter = Vector3.zero; var baseCenter = Vector3.zero; int tallCount = 0, baseCount = 0;
            foreach (var obj in roots)
            {
                var center = obj.transform.TransformPoint(obj.GetComponent<MeshFilter>().sharedMesh.bounds.center);
                if (Array.IndexOf(library.meshes, obj.GetComponent<MeshFilter>().sharedMesh) >= 4) { tallCenter += center; tallCount++; }
                else { baseCenter += center; baseCount++; }
            }
            var outward = tallCount > 0 && baseCount > 0 ? baseCenter / baseCount - tallCenter / tallCount : Vector3.zero;
            outward.y = 0;
            var yaw = outward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(outward.normalized, Vector3.up)
                : Quaternion.Euler(0, roots[0].transform.eulerAngles.y, 0);
            float scale = 3.0f / Mathf.Max(bounds.size.y, 0.01f);
            var pieces = new List<RockClusterTemplate.Piece>();
            foreach (var obj in roots)
            {
                var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
                var worldScale = obj.transform.lossyScale;
                worldScale = new Vector3(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z));
                pieces.Add(new RockClusterTemplate.Piece
                {
                    source = mesh,
                    position = Quaternion.Inverse(yaw) * (obj.transform.TransformPoint(mesh.bounds.center) - bounds.center) * scale,
                    size = Vector3.Scale(mesh.bounds.size, worldScale) * scale,
                    rotation = Quaternion.Inverse(yaw) * obj.transform.rotation
                });
            }
            var template = AssetDatabase.LoadAssetAtPath<RockClusterTemplate>(TemplatePath);
            if (template == null) { template = ScriptableObject.CreateInstance<RockClusterTemplate>(); AssetDatabase.CreateAsset(template, TemplatePath); }
            template.pieces = pieces.ToArray(); template.capturedHeight = bounds.size.y;
            EditorUtility.SetDirty(template); library.clusterTemplate = template; EditorUtility.SetDirty(library);
            var root = new GameObject("Hand-Placed Rock Cluster");
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(CliffRockAssetBuilder.Folder + "/Limestone.mat");
                foreach (var piece in pieces)
                {
                    var child = new GameObject(piece.source.name); child.transform.SetParent(root.transform, false);
                    child.transform.localRotation = piece.rotation;
                    child.transform.localScale = new Vector3(piece.size.x / piece.source.bounds.size.x,
                        piece.size.y / piece.source.bounds.size.y, piece.size.z / piece.source.bounds.size.z);
                    child.transform.localPosition = piece.position - piece.rotation * Vector3.Scale(piece.source.bounds.center, child.transform.localScale);
                    child.AddComponent<MeshFilter>().sharedMesh = piece.source; child.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Art/PCG/Rocks/HandPlacedCluster.prefab");
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/TerrainDemo");
            File.WriteAllText("Logs/TerrainDemo/hand-cluster.json", JsonUtility.ToJson(template, true));
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            Debug.Log($"HAND_CLUSTER_CAPTURED: {pieces.Count} pieces, reference height {bounds.size.y:F3} m; source objects preserved.");
        }
    }
}
#endif
