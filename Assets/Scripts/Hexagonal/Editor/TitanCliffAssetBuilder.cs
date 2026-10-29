#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HexagonalGrids.Prototype;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class TitanCliffAssetBuilder
    {
        public static void Ensure(CliffRockLibrary library)
        {
            Mesh[] Family(string type) => AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/RockTools/Resources/Meshes/" + type })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<Mesh>).Where(m => m != null && m.isReadable).ToArray();
            var cubic = Family("Type01"); var sharp = Family("Type02");
            if (cubic.Length == 0 || sharp.Length == 0)
                throw new InvalidOperationException("Titan source meshes are missing or unreadable.");
            var sources = cubic.Concat(sharp).ToArray();
            Directory.CreateDirectory("Logs/TerrainDemo");
            var catalog = new RockShapeCatalog(sources);
            var lines = new System.Collections.Generic.List<string> { "Asset,SizeX,SizeY,SizeZ,Slenderness,TopSpan,PrimaryRole" };
            foreach (var shape in catalog.Shapes)
                lines.Add(FormattableString.Invariant($"{AssetDatabase.GetAssetPath(sources[shape.Index])},{shape.Size.x:F4},{shape.Size.y:F4},{shape.Size.z:F4},{shape.Slenderness:F3},{shape.TopSpan:F3},{(shape.Block ? "MainBlock" : "Column")}"));
            File.WriteAllLines("Logs/TerrainDemo/rock-shape-audit.csv", lines);
            if (library.slopeMeshes != null && library.slopeMeshes.SequenceEqual(sources)) return;
            // Bind original plugin assets. Never normalize, reshape or duplicate their geometry.
            library.slopeMeshes = sources;
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Marching Cube/Terrain Demo/Build Titan Rock Mass %&F6")]
        public static void BuildPreview()
        {
            var existingScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TerrainBuildingDemoSetup.ScenePath);
            if (existingScene.isLoaded && existingScene.isDirty)
            {
                Directory.CreateDirectory("Logs/TerrainDemo");
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(existingScene, "Logs/TerrainDemo/BeforeTitan.unity", true);
            }
            TerrainBuildingDemoSetup.CreateOrOpen();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TerrainBuildingDemoSetup.ScenePath);
            var demo = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<TerrainBuildingDemo>()).Single();
            demo.SetRockDetail(true);
            SceneView.RepaintAll();
            Debug.Log($"TITAN_ROCK_MASS_OK: face={demo.FaceStoneCount}, toe={demo.ToeStoneCount}, chunks={demo.RockChunkCount}");
        }
    }
}
#endif
