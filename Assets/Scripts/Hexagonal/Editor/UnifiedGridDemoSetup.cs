#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HexagonalGrids.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editor
{
    public static class UnifiedGridDemoSetup
    {
        public const string ScenePath = "Assets/Scenes/UnifiedGridBuildingDemo.unity";
        public const string Output = "Logs/UnifiedGridDemo";

        [MenuItem("Tools/Marching Cube/Unified Grid Demo/Open Demo %#&F6")]
        public static void OpenDemo()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before opening the demo.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("Save current scene edits before opening the unified grid demo.");
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            SceneManager.SetActiveScene(scene);
            var demo = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<UnifiedGridBuildingDemo>()).Single();
            Selection.activeGameObject = demo.gameObject;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 2, 1), Quaternion.Euler(48, 25, 0), 40);
            Debug.Log("Unified grid demo ready. Press Play: edit polygon terrain and freely build at multiple heights.", demo);
        }

        [MenuItem("Tools/Marching Cube/Unified Grid Demo/Validate and Capture %#&F7")]
        public static void ValidateAndCapture()
        {
            if (!EditorApplication.isPlaying) OpenDemo();
            var demo = UnityEngine.Object.FindAnyObjectByType<UnifiedGridBuildingDemo>();
            Require(demo != null, "Demo controller is missing.");
            demo.Initialize(); var document = demo.Document; var initial = document.Capture();
            var oldTool = demo.Tool; int oldLayer = demo.ActiveLayer;
            Directory.CreateDirectory(Output);
            try
            {
                demo.LoadExample(false); demo.SetLayer(document.TerrainLayer);
                demo.SetTool(UnifiedGridBuildingDemo.EditTool.Raise); demo.ResetCamera();
                float basis = document.HeightAtLevel(document.TerrainLayer);
                Require(Enumerable.Range(0, document.ColumnCount).All(i => document.CanBuild(i, document.TerrainLayer)), "Terrain-plane construction has coverage holes.");
                Require(demo.ConstructionEdgeCount == document.Grid.baseLayer.subEdges.Count, "Construction grid omits polygon edges.");
                var natural = Enumerable.Range(0, document.ColumnCount).Select(i => document.GroundPosition(i).y).ToArray();
                Require(natural.Max() > basis && natural.Min() < basis, "Empty terrain has lost its mild waves.");
                Require(natural.All(h => Mathf.Abs(h - basis) <= document.MaxUndulation), "Terrain relief exceeds the designated plane's mild range.");
                Capture(demo.ViewCamera, Output + "/terrain-only.png");
                demo.LoadExample();
                Require(demo.ActiveModelCount > 0, "No buildings generated.");
                Require(demo.WaveFunction.Validate(out var wfcError), wfcError);
                Require(demo.WaveFunction.LastFallbacks == 0, "Example did not find a WFC solution.");
                Require(document.FoundationCellCount > 0, "No building data Cell contact constraints.");
                foreach (var contactPolygon in document.Grid.baseLayer.subQuads.Where(document.IsFoundationCell))
                    foreach (var contactCorner in contactPolygon.vertices)
                        Require(Mathf.Abs(document.GroundPosition(document.ColumnOf(contactCorner)).y - basis) < 0.00001f,
                            "Terrain does not match the bottom data Cell's plane.");
                Require(demo.TerrainChunks.Sum(c => c.Polygons.Count) == document.Grid.baseLayer.subQuads.Count, "Terrain chunks lost polygons.");
                Require(demo.TerrainChunks.All(c => c.Collider.sharedMesh == c.Mesh), "Terrain chunk collider is stale.");
                Require(demo.GetComponentsInChildren<MeshCollider>().All(c => c.sharedMesh != null), "A visible building has stale collision.");
                Require(demo.GetComponentsInChildren<Transform>(true).All(t => !t.name.Contains("Rock") && !t.name.Contains("Stone")), "Rock generation entered this demo.");
                var json = document.ExportJson(); document.ClearBuildings(); demo.Refresh();
                Require(demo.ActiveModelCount == 0, "Clear failed to return building models to the pool.");
                Require(document.FoundationCellCount == 0 && natural.SequenceEqual(Enumerable.Range(0, document.ColumnCount).Select(i => document.GroundPosition(i).y)),
                    "Removing buildings did not restore the original terrain relief.");
                Require(demo.UndoEdit() && document.ExportJson() == json, "Unified undo failed.");
                Require(demo.RedoEdit() && demo.ActiveModelCount == 0, "Unified redo failed.");
                document.ImportJson(json); demo.Refresh();
                Require(document.ExportJson() == json && demo.ActiveModelCount > 0, "JSON round trip lost building or terrain data.");
                var originalModules = document.Grid.cells.Select(demo.WaveFunction.Choice).ToArray();
                var occupied = document.Capture().enabled;
                document.RerollBuildingModules(); demo.Refresh();
                Require(demo.WaveFunction.Validate(out wfcError), wfcError);
                Require(occupied.SequenceEqual(document.Capture().enabled), "WFC changed player occupancy.");
                Require(!originalModules.SequenceEqual(document.Grid.cells.Select(demo.WaveFunction.Choice)), "WFC reroll did not select new variants.");
                Require(demo.UndoEdit() && document.ExportJson() == json
                    && originalModules.SequenceEqual(document.Grid.cells.Select(demo.WaveFunction.Choice)), "WFC undo is not reproducible.");
                // Verify both picking paths against known world-space topology points.
                var vertex = document.Grid.layers[4].subVertices[document.ColumnCount / 2];
                demo.SetLayer(4);
                Require(demo.TryPickPoint(new Ray(vertex.position + Vector3.up * 40, Vector3.down), out var picked)
                    && picked == vertex, "Building plane picking does not follow canonical columns.");
                var polygon = document.Grid.baseLayer.subQuads[0];
                var center = Vector3.zero;
                foreach (var corner in polygon.vertices) center += document.GroundPosition(document.ColumnOf(corner)) / 4;
                Require(demo.TryPickTerrain(new Ray(center + Vector3.up * 40, Vector3.down), out var pickedPolygon)
                    && pickedPolygon == polygon, "Terrain picking does not follow canonical polygons.");
                demo.SetTool(UnifiedGridBuildingDemo.EditTool.Raise); demo.ResetCamera();
                Capture(demo.ViewCamera, Output + "/overview.png");
                demo.SetTool(UnifiedGridBuildingDemo.EditTool.Add); demo.SetLayer(document.TerrainLayer);
                Capture(demo.ViewCamera, Output + "/building-layer.png");
                var previousPosition = demo.ViewCamera.transform.position; var previousRotation = demo.ViewCamera.transform.rotation;
                try
                {
                    var focus = new Vector3(-7, basis, -3);
                    demo.ViewCamera.transform.position = focus + new Vector3(8, 7, -11); demo.ViewCamera.transform.LookAt(focus);
                    Capture(demo.ViewCamera, Output + "/cell-contact-detail.png");
                }
                finally { demo.ViewCamera.transform.SetPositionAndRotation(previousPosition, previousRotation); }
                File.WriteAllText(Output + "/validation.txt", $"UNIFIED_GRID_DEMO_OK\nMode={(Application.isPlaying ? "Play" : "Edit")}\nColumns={document.ColumnCount}\nPolygons={document.Grid.baseLayer.subQuads.Count}\nBuildingModules={demo.ActiveModelCount}\nVertexPlanes={document.Grid.layers.Count}\nTerrainLayer={document.TerrainLayer}\nFoundationCells={document.FoundationCellCount}\nConstructionEdges={demo.ConstructionEdgeCount}\nWfcVariants={demo.WaveFunction.Catalog.Variants.Count}\nWfcObservations={demo.WaveFunction.LastObservations}\nWfcPropagations={demo.WaveFunction.LastPropagations}\nWfcBacktracks={demo.WaveFunction.LastBacktracks}\nWfcFallbacks={demo.WaveFunction.LastFallbacks}\nWfcFaces=OK\nWfcRerollAndUndo=OK\nFullCoverage=OK\nMildRelief=OK\nCellBottomContact=OK\nReliefRestoration=OK\nTerrainCollider=OK\nBuildingColliders=OK\nUnifiedHistory=OK\nJsonRoundTrip=OK\nPicking=OK\nRocks=0\n");
                Debug.Log("UNIFIED_GRID_DEMO_OK: WFC modules, shared-face constraints, deterministic reroll/undo, full-world grid and data Cell contact validated.");
            }
            finally { document.Restore(initial); demo.SetTool(oldTool); demo.SetLayer(oldLayer); demo.Refresh(); }
        }
        private static void Require(bool valid, string message)
        { if (!valid) throw new InvalidOperationException("Unified grid validation: " + message); }
        private static void Capture(Camera camera, string path)
        {
            var target = RenderTexture.GetTemporary(1440, 1000, 24);
            var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
            var image = new Texture2D(1440, 1000, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1440, 1000), 0, 0); image.Apply();
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
