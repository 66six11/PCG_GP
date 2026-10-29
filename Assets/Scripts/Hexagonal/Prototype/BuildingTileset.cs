using System;
using System.Collections.Generic;
using MarchingCube;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    [Serializable] public sealed class BuildingNeighbor { public string id; public float weight = 1; }
    [Serializable] public sealed class BuildingFaces
    {
        public BuildingNeighbor[] up, down, front, right, back, left;
        public BuildingNeighbor[] Get(int face) => face switch {
            0 => up, 1 => down, 2 => front, 3 => right, 4 => back, 5 => left,
            _ => throw new ArgumentOutOfRangeException(nameof(face)) };
    }
    [Serializable] public sealed class BuildingModuleSource
    {
        public string id, logicalId, displayName, meshName;
        public int[] corners, yawDegrees;
        public float weight;
        public bool allowMirrorX;
        public BuildingFaces neighbors;
    }
    [Serializable] public sealed class BuildingTilesetSource
    {
        public int schemaVersion;
        public string tilesetId, coordinateSystem, cornerOrder, modelFile;
        public BuildingModuleSource[] modules;
    }
    [Serializable] public sealed class BuildingTileVariant
    {
        public string id, sourceId, logicalId;
        public ModelInfo model;
        public float weight;
        public BuildingFaces neighbors;
    }

    /// <summary>Compiled authoring metadata and validated, independent mesh snapshots.</summary>
    public sealed class BuildingTileset : ScriptableObject
    {
        public int schemaVersion = 1;
        public string tilesetId, sourceJson, sourceHash;
        public BuildingTileVariant[] variants = Array.Empty<BuildingTileVariant>();
        public string[] diagnostics = Array.Empty<string>();
    }

    public static class BuildingTilesetCompiler
    {
        public static readonly string[] FaceNames = { "up", "down", "front", "right", "back", "left" };
        private static readonly Vector3[] Normals = { Vector3.up, Vector3.down, Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        public static int TransformFace(int face, CubeTransform transform)
        {
            var normal = transform.TransformPoint(Normals[face]);
            for (int i = 0; i < 6; i++) if ((normal - Normals[i]).sqrMagnitude < 0.001f) return i;
            throw new InvalidOperationException("Invalid transformed face.");
        }
        public static bool Reserved(string id) => id == "$empty" || id == "$solid" || id == "$outside" || id == "$baseline";
        public static BuildingTileVariant[] Compile(BuildingTilesetSource source, Func<string, Mesh> meshLookup)
        {
            if (source == null || source.schemaVersion != 1 || source.coordinateSystem != "Unity_Y_Up"
                || source.cornerOrder != "V1_V2_V3_V4_V5_V6_V7_V8" || string.IsNullOrWhiteSpace(source.tilesetId)
                || source.modules == null || source.modules.Length == 0) throw new ArgumentException("Invalid tileset schema, coordinate convention or empty modules.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var module in source.modules)
                if (module == null || string.IsNullOrWhiteSpace(module.id) || module.id.StartsWith("$") || !ids.Add(module.id))
                    throw new ArgumentException("Duplicate or invalid module ID.");
            var result = new List<BuildingTileVariant>();
            foreach (var module in source.modules)
            {
                void Fail(string error) => throw new ArgumentException($"{module.id}: {error}");
                if (module.corners == null || module.corners.Length != 8) Fail("Expected V1..V8.");
                byte state = 0;
                for (int i = 0; i < 8; i++) {
                    if (module.corners[i] != 0 && module.corners[i] != 1) Fail("Corners must be 0/1.");
                    state |= (byte)(module.corners[i] << i);
                }
                if (state == 0 || state == 255) Fail("Air/solid are reserved boundary states.");
                if (!(module.weight > 0) || float.IsInfinity(module.weight)) Fail("Weight must be finite and positive.");
                if (module.yawDegrees == null || module.yawDegrees.Length == 0 || module.neighbors == null) Fail("Missing transforms/faces.");
                var turns = new HashSet<int>();
                foreach (int yaw in module.yawDegrees) if (yaw < 0 || yaw > 270 || yaw % 90 != 0 || !turns.Add(yaw)) Fail("Invalid or duplicate yaw.");
                for (int face = 0; face < 6; face++) {
                    var rules = module.neighbors.Get(face); if (rules == null) Fail($"Missing face {FaceNames[face]}.");
                    var targets = new HashSet<string>();
                    foreach (var rule in rules)
                        if (rule == null || string.IsNullOrEmpty(rule.id) || (!ids.Contains(rule.id) && !Reserved(rule.id))
                            || !targets.Add(rule.id) || !(rule.weight > 0) || float.IsInfinity(rule.weight)) Fail($"Invalid candidate on {FaceNames[face]}.");
                }
                var mesh = meshLookup(module.meshName);
                if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0) Fail($"Missing/read-protected mesh {module.meshName}.");
                foreach (var p in mesh.vertices)
                    if (!Finite(p.x) || !Finite(p.y) || !Finite(p.z) || Mathf.Abs(p.x) > .5001f || Mathf.Abs(p.y) > .5001f || Mathf.Abs(p.z) > .5001f) Fail("Geometry outside normalized domain.");
                bool touchesBoundary = false;
                foreach (var p in mesh.vertices)
                    if (Mathf.Abs(p.x) >= .4999f || Mathf.Abs(p.y) >= .4999f || Mathf.Abs(p.z) >= .4999f) touchesBoundary = true;
                if (!touchesBoundary) Fail("Surface module never reaches the Cell boundary. Check FBX unit conversion.");
                float weight = module.weight / (module.yawDegrees.Length * (module.allowMirrorX ? 2 : 1));
                for (int mirror = 0; mirror < (module.allowMirrorX ? 2 : 1); mirror++)
                    foreach (int yaw in module.yawDegrees) {
                        var transform = new CubeTransform(yaw / 90, mirror != 0);
                        var faces = new BuildingNeighbor[6][];
                        for (int face = 0; face < 6; face++) faces[TransformFace(face, transform)] = module.neighbors.Get(face);
                        result.Add(new BuildingTileVariant {
                            id = module.id + ":r" + yaw + (mirror != 0 ? ":mx" : ""), sourceId = module.id, logicalId=module.logicalId,
                            model = new ModelInfo(transform.TransformState(state), mesh, transform.Rotation, transform.MirrorX), weight = weight,
                            neighbors = new BuildingFaces { up=faces[0], down=faces[1], front=faces[2], right=faces[3], back=faces[4], left=faces[5] }
                        });
                    }
            }
            result.Sort((a,b) => StringComparer.Ordinal.Compare(a.id,b.id));
            return result.ToArray();
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
