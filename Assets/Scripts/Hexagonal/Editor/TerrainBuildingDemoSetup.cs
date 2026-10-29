#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HexagonalGrids.Prototype;
using MarchingCube;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    public static class TerrainBuildingDemoSetup
    {
        public const string ScenePath = "Assets/Scenes/TerrainBuildingDemo.unity";

        [MenuItem("Tools/Marching Cube/Terrain Demo/Clear Generated Stones %&F7")]
        public static void ClearGeneratedStones()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) return;
            foreach (var demo in scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TerrainBuildingDemo>()).ToArray())
            {
                demo.SetRockDetail(false);
                demo.Initialize();
                Require(demo.FaceStoneCount == 0 && demo.ToeStoneCount == 0, "Generated stones were not cleared.");
            }
            SceneView.RepaintAll();
            Debug.Log("Generated cliff and terrain stones cleared; backing terrain and hand-placed reference objects preserved.");
        }

        [MenuItem("Tools/Marching Cube/Terrain Demo/Create or Open Demo %&F9")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var rocks = CliffRockAssetBuilder.EnsureLibrary();
            TitanCliffAssetBuilder.Ensure(rocks);
            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(ModelLibraryBinding.DefaultLibrary);
            var errors = ModelLibraryBinding.Validate(library);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            }
            SceneManager.SetActiveScene(scene);
            var demo = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TerrainBuildingDemo>()).FirstOrDefault();
            if (demo == null)
            {
                var host = new GameObject("Terrain and Building Demo"); SceneManager.MoveGameObjectToScene(host, scene);
                demo = host.AddComponent<TerrainBuildingDemo>();
                var cameraObject = new GameObject("Terrain Demo Camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f, 0.17f, 0.19f);
                camera.cullingMask = 1 << 30; camera.depth = 100; camera.nearClipPlane = 0.1f; camera.farClipPlane = 200;
                camera.fieldOfView = 48;
                var sunObject = new GameObject("Terrain Demo Sun"); SceneManager.MoveGameObjectToScene(sunObject, scene);
                var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional;
                sun.intensity = 1.6f; sun.color = new Color(1, 0.92f, 0.78f); sun.cullingMask = 1 << 30;
                sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(48, -35, 0);
                demo.Configure(library, camera, rocks);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            // Keep original scenes and unsaved edits loaded. The demo camera isolates its generated geometry.
            bool preserveUnsavedEdits = scene.isDirty;
            demo.SetRockLibrary(rocks);
            EditorUtility.SetDirty(demo);
            if (!preserveUnsavedEdits) EditorSceneManager.SaveScene(scene, ScenePath);
            demo.Initialize(); Selection.activeGameObject = demo.gameObject;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 1, 0), Quaternion.Euler(48, 28, 0), 25);
            Debug.Log("Terrain demo ready: open TerrainBuildingDemo.unity by itself and press Play. Original scenes remain loaded.", demo);
        }

        [MenuItem("Tools/Marching Cube/Terrain Demo/Validate and Capture %&F10")]
        public static void ValidateAndCapture()
        {
            CreateOrOpen();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var demo = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TerrainBuildingDemo>()).Single();
            demo.CreateSlopeExample(); demo.SetWire(true);
            Require(demo.Surface.Cliffs.Count == 0 && demo.CliffMesh.vertexCount == 0, "Legacy cut wall remains active.");
            Require(demo.Surface.Positions.Count == demo.Surface.Indices.Count, "Continuous terrain has duplicate rims.");
            Require(demo.TerrainCollider.sharedMesh == demo.TerrainMesh, "Stale terrain collision.");
            Require(demo.Slopes.RockyFaces > 0 && demo.FaceStoneCount > 0, "No slope-driven rock coverage.");
            Require(demo.BuildingRoot.transform.Find("Foundation") == null, "Foundation slab remains.");
            for (int i = 0; i < demo.Surface.Triangles.Count; i += 3)
            {
                var a = demo.Surface.Positions[demo.Surface.Triangles[i]];
                var b = demo.Surface.Positions[demo.Surface.Triangles[i + 1]];
                var c = demo.Surface.Positions[demo.Surface.Triangles[i + 2]]; var p = (a + b + c) / 3;
                Require(Vector3.Cross(b - a, c - a).y > 0, "Flipped terrain triangle.");
                Require(demo.Surface.TryHeight(new Vector2(p.x, p.z), out float h) && Mathf.Abs(h - p.y) < 0.0001f, "Height query mismatch.");
            }
            var before = demo.TerrainMesh.vertices;
            Directory.CreateDirectory("Logs/TerrainDemo");
            Capture(demo.ViewCamera, "Logs/TerrainDemo/slope-cover-grid.png");
            demo.SetWire(false); Capture(demo.ViewCamera, "Logs/TerrainDemo/slope-cover.png");
            var oldPosition = demo.ViewCamera.transform.position; var oldRotation = demo.ViewCamera.transform.rotation;
            try
            {
                var focus = new Vector3(-2, 3, 0);
                demo.ViewCamera.transform.position = focus + new Vector3(11, 7, -14);
                demo.ViewCamera.transform.LookAt(focus);
                Capture(demo.ViewCamera, "Logs/TerrainDemo/slope-cover-close.png");
            }
            finally { demo.ViewCamera.transform.SetPositionAndRotation(oldPosition, oldRotation); }
            demo.SetRockDetail(false);
            Require(before.SequenceEqual(demo.TerrainMesh.vertices), "Rock visibility modified terrain geometry.");
            Capture(demo.ViewCamera, "Logs/TerrainDemo/slope-material.png");
            demo.SetRockDetail(true);
            Require(demo.PlaceBuilding(new Vector2(-2, 0)), "Building placement rejected.");
            Require(demo.ModelCount > 0 && Mathf.Abs(demo.FloorHeight - demo.Surface.PlatformHeight) < 1e-5f, "Building not seated on soil.");
            Capture(demo.ViewCamera, "Logs/TerrainDemo/slope-building.png");
            demo.UndoEdit();
            var heights = demo.Surface.CopyHeightOffsets();
            oldPosition = demo.ViewCamera.transform.position; oldRotation = demo.ViewCamera.transform.rotation;
            try
            {
                var tall = heights.Select(h => Mathf.Clamp(h * 12 / 5.8f, -12, 12)).ToArray();
                demo.Surface.RestoreHeightOffsets(tall); demo.RefreshPreview(); demo.SetWire(false);
                var focus = new Vector3(-1, 5.5f, 0);
                demo.ViewCamera.transform.position = focus + new Vector3(15, 12, -18);
                demo.ViewCamera.transform.LookAt(focus);
                Capture(demo.ViewCamera, "Logs/TerrainDemo/tall-plateau-cover.png");
                File.WriteAllText("Logs/TerrainDemo/tall-plateau.txt", $"HIGH_PLATEAU_OK\nBodies={demo.FaceStoneCount}\nRubble={demo.ToeStoneCount}\n");
            }
            finally
            {
                demo.Surface.RestoreHeightOffsets(heights); demo.RefreshPreview(); demo.SetWire(true);
                demo.ViewCamera.transform.SetPositionAndRotation(oldPosition, oldRotation);
            }
            File.WriteAllText("Logs/TerrainDemo/validation.txt", $"TERRAIN_DEMO_OK\nContinuousVertices={demo.Surface.Positions.Count}\nRockyTriangles={demo.Slopes.RockyFaces}\nRockRegions={demo.RockChunkCount}\nRockModels={demo.FaceStoneCount + demo.ToeStoneCount}\nToeRocks={demo.ToeStoneCount}\nCutWalls={demo.CliffMesh.vertexCount}\n");
            Debug.Log("TERRAIN_DEMO_OK: continuous terrain, slope material and model coverage validated.");
        }
        [MenuItem("Tools/Marching Cube/Terrain Demo/Play Demo Alone %&F11")]
        public static void PlayDemoAlone()
        {
            if (EditorApplication.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene edits before opening the terrain demo alone.");
            if (!File.Exists(ScenePath)) CreateOrOpen();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException("Terrain demo validation: " + message); }

        private static void CaptureRockLibrary(CliffRockLibrary library, Camera camera)
        {
            var root = new GameObject("Rock gallery capture") { hideFlags = HideFlags.DontSave };
            var originalPosition = camera.transform.position; var originalRotation = camera.transform.rotation;
            bool orthographic = camera.orthographic; float size = camera.orthographicSize;
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(CliffRockAssetBuilder.Folder + "/Limestone.mat");
                for (int i = 0; i < library.meshes.Length; i++)
                {
                    var obj = new GameObject(RockModelFactory.Names[i]); obj.layer = 30; obj.transform.SetParent(root.transform);
                    obj.transform.position = new Vector3(50 + i * 2, library.meshes[i].bounds.extents.y, 0);
                    obj.transform.rotation = Quaternion.Euler(0, -25, 0);
                    obj.AddComponent<MeshFilter>().sharedMesh = library.meshes[i]; obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                float centerX = 50 + library.meshes.Length - 1;
                camera.orthographic = true; camera.orthographicSize = Mathf.Max(3.1f, library.meshes.Length * 0.75f);
                camera.transform.position = new Vector3(centerX, 4, -9); camera.transform.LookAt(new Vector3(centerX, 0.9f, 0));
                Directory.CreateDirectory("Logs/TerrainDemo"); Capture(camera, "Logs/TerrainDemo/rocks-library.png");
                if (library.clusterTemplate != null)
                {
                    foreach (var node in root.GetComponentsInChildren<MeshRenderer>()) node.gameObject.SetActive(false);
                    var prototype = library.clusterTemplate.GetPlacements(library);
                    for (int cluster = 0; cluster < 3; cluster++)
                        foreach (var p in ProceduralCliffMesh.CreateClusterPieces(42 + cluster * 101, 1, prototype))
                        {
                            var mesh = library.meshes[p.SourceIndex];
                            var obj = new GameObject("Reference cluster piece"); obj.layer = 30; obj.transform.SetParent(root.transform);
                            obj.transform.rotation = p.Rotation;
                            obj.transform.localScale = new Vector3(p.Size.x / mesh.bounds.size.x, p.Size.y / mesh.bounds.size.y, p.Size.z / mesh.bounds.size.z);
                            obj.transform.position = new Vector3(51 + cluster * 2.4f, 1.6f, 0) + p.Position
                                - p.Rotation * Vector3.Scale(mesh.bounds.center, obj.transform.localScale);
                            obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                        }
                    camera.orthographicSize = 3.3f;
                    camera.transform.position = new Vector3(55.2f, 3.4f, 9); camera.transform.LookAt(new Vector3(53.4f, 1.4f, 0));
                    Capture(camera, "Logs/TerrainDemo/rock-clusters.png");
                }
            }
            finally
            {
                camera.orthographic = orthographic; camera.orthographicSize = size;
                camera.transform.SetPositionAndRotation(originalPosition, originalRotation); UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void Capture(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(1400, 960, 24);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var image = new Texture2D(1400, 960, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1400, 960), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image); RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif

