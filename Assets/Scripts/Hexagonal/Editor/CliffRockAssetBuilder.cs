#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using HexagonalGrids.Prototype;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Editor
{
    public static class CliffRockAssetBuilder
    {
        public const string Folder = "Assets/Art/PCG/Rocks";
        public const string LibraryPath = Folder + "/CliffRockLibrary.asset";

        public static CliffRockLibrary EnsureLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>(LibraryPath);
            if (library != null && library.generatorVersion == RockModelFactory.Version && library.meshes.Length == RockModelFactory.Names.Length
                && System.Array.TrueForAll(library.meshes, m => m != null && m.isReadable)) return library;
            return Generate();
        }

        [MenuItem("Tools/Marching Cube/Terrain Demo/Generate Rock Model Assets")]
        public static void GenerateAssets() => Generate();

        private static CliffRockLibrary Generate()
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory("Tools/Blender/GeneratedRocks");
            AssetDatabase.Refresh();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Limestone.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.49f, 0.46f, 0.39f) };
                material.SetFloat("_Smoothness", 0.06f); AssetDatabase.CreateAsset(material, Folder + "/Limestone.mat");
            }
            var meshes = new Mesh[RockModelFactory.Names.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                string name = RockModelFactory.Names[i], path = Folder + "/" + name + ".asset";
                var built = new Mesh(); RockModelFactory.Build(built, i);
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing == null) { AssetDatabase.CreateAsset(built, path); meshes[i] = built; }
                else { EditorUtility.CopySerialized(built, existing); Object.DestroyImmediate(built); meshes[i] = existing; EditorUtility.SetDirty(existing); }
                var obj = new GameObject(name);
                try
                {
                    obj.AddComponent<MeshFilter>().sharedMesh = meshes[i]; obj.AddComponent<MeshRenderer>().sharedMaterial = material;
                    var collider = obj.AddComponent<MeshCollider>(); collider.sharedMesh = meshes[i]; collider.convex = true;
                    PrefabUtility.SaveAsPrefabAsset(obj, Folder + "/" + name + ".prefab");
                }
                finally { Object.DestroyImmediate(obj); }
                ExportObj(meshes[i], "Tools/Blender/GeneratedRocks/" + name + ".obj");
            }
            var library = AssetDatabase.LoadAssetAtPath<CliffRockLibrary>(LibraryPath);
            if (library == null) { library = ScriptableObject.CreateInstance<CliffRockLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
            library.meshes = meshes; library.generatorVersion = RockModelFactory.Version; EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets(); return library;
        }

        private static void ExportObj(Mesh mesh, string path)
        {
            var text = new StringBuilder("# Generated fracture rock, Unity Y-up, metres\n");
            foreach (var p in mesh.vertices) text.AppendLine(FormattableString.Invariant($"v {p.x:R} {p.y:R} {p.z:R}"));
            foreach (var p in mesh.normals) text.AppendLine(FormattableString.Invariant($"vn {p.x:R} {p.y:R} {p.z:R}"));
            var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i] + 1, b = triangles[i + 1] + 1, c = triangles[i + 2] + 1;
                text.AppendLine(string.Format(CultureInfo.InvariantCulture, "f {0}//{0} {1}//{1} {2}//{2}", a, b, c));
            }
            File.WriteAllText(path, text.ToString());
        }
    }
}
#endif
