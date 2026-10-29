using System;
using System.Collections.Generic;
using MarchingCube;
using UnityEngine;
using UnityEngine.Rendering;

namespace HexagonalGrids.Prototype
{
    /// <summary>Spatial render/collision ownership. Canonical topology and editing remain in the document.</summary>
    public sealed class UnifiedGridChunks : IDisposable
    {
        public sealed class TerrainChunk
        {
            public Mesh Mesh { get; internal set; }
            public MeshCollider Collider { get; internal set; }
            public int Revision { get; internal set; }
            public readonly List<Quad> Polygons = new List<Quad>();
            internal readonly List<int> Columns = new List<int>();
            internal readonly Dictionary<int, int> Local = new Dictionary<int, int>();
            internal readonly List<Edge> Edges = new List<Edge>();
            internal readonly List<int> Markers = new List<int>();
            internal GameObject Root, Ground, Plane, Points;
            internal Mesh GroundMesh, PlaneMesh, PointMesh;
        }
        private sealed class BuildingChunk
        {
            public GameObject Root;
            public Mesh Mesh;
            public MeshCollider Collider;
            public readonly Dictionary<Cell, Model> Models = new Dictionary<Cell, Model>();
        }
        private readonly UnifiedGridDocument document;
        private readonly Transform parent;
        private readonly float size;
        private readonly Material buildingMaterial;
        private readonly List<TerrainChunk> terrain = new List<TerrainChunk>();
        private readonly List<TerrainChunk>[] columnChunks;
        private readonly Dictionary<Vector3Int, BuildingChunk> buildings = new Dictionary<Vector3Int, BuildingChunk>();
        private readonly HashSet<TerrainChunk> dirtyTerrain = new HashSet<TerrainChunk>(), dirtyOverlay = new HashSet<TerrainChunk>();
        private readonly HashSet<BuildingChunk> dirtyBuildings = new HashSet<BuildingChunk>();
        private readonly List<int> changedColumns = new List<int>();
        private readonly List<Vector3> positions = new List<Vector3>(), normals = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<CombineInstance> combine = new List<CombineInstance>();
        private int overlayLayer = -1;
        private bool? gridVisible, planeVisible;
        public IReadOnlyList<TerrainChunk> Terrain => terrain;
        public int ConstructionEdgeCount { get; private set; }
        public int LastTerrainChunks { get; private set; }
        public int LastBuildingChunks { get; private set; }
        public int BuildingChunkCount { get { int count = 0; foreach (var chunk in buildings.Values) if (chunk.Models.Count > 0) count++; return count; } }

        public UnifiedGridChunks(UnifiedGridDocument document, Transform parent, float chunkSize,
            Material soil, Material building, Material wire, Material plane, Material point)
        {
            this.document = document; this.parent = parent; size = Mathf.Max(4, chunkSize); buildingMaterial = building;
            columnChunks = new List<TerrainChunk>[document.ColumnCount];
            for (int i = 0; i < columnChunks.Length; i++) columnChunks[i] = new List<TerrainChunk>();
            var spatial = new Dictionary<Vector3Int, TerrainChunk>();
            var edgeOwners = new HashSet<Edge>();
            foreach (var polygon in document.Grid.baseLayer.subQuads)
            {
                var key = Key(polygon.center, false);
                if (!spatial.TryGetValue(key, out var chunk))
                {
                    var mesh = NewMesh("Terrain " + key);
                    var root = MeshObject("Terrain chunk " + key, mesh, soil);
                    chunk = new TerrainChunk { Root = root, Mesh = mesh, Collider = root.AddComponent<MeshCollider>(),
                        GroundMesh = NewMesh("Terrain edges " + key), PlaneMesh = NewMesh("Build plane " + key), PointMesh = NewMesh("Build points " + key) };
                    chunk.Ground = MeshObject("Ground edges " + key, chunk.GroundMesh, wire);
                    chunk.Plane = MeshObject("Construction edges " + key, chunk.PlaneMesh, plane);
                    chunk.Points = MeshObject("Enabled points " + key, chunk.PointMesh, point);
                    spatial.Add(key, chunk); terrain.Add(chunk); dirtyTerrain.Add(chunk); dirtyOverlay.Add(chunk);
                }
                chunk.Polygons.Add(polygon);
                foreach (var vertex in polygon.vertices)
                {
                    int column = document.ColumnOf(vertex);
                    if (chunk.Local.ContainsKey(column)) continue;
                    chunk.Local.Add(column, chunk.Columns.Count); chunk.Columns.Add(column);
                    if (columnChunks[column].Count == 0) chunk.Markers.Add(column);
                    columnChunks[column].Add(chunk);
                }
                foreach (var edge in polygon.edges) if (edgeOwners.Add(edge)) { chunk.Edges.Add(edge); ConstructionEdgeCount++; }
            }
        }
        private Vector3Int Key(Vector3 point, bool vertical)
            => new Vector3Int(Mathf.FloorToInt(point.x / size), vertical ? Mathf.FloorToInt(point.y / size) : 0, Mathf.FloorToInt(point.z / size));
        private static Mesh NewMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic(); return mesh;
        }
        private GameObject MeshObject(string name, Mesh mesh, Material material)
        {
            var root = new GameObject(name) { layer = 30, hideFlags = HideFlags.DontSave };
            root.transform.SetParent(parent, false); root.AddComponent<MeshFilter>().sharedMesh = mesh;
            root.AddComponent<MeshRenderer>().sharedMaterial = material; return root;
        }
        public void RefreshTerrain()
        {
            document.DrainTerrainDirty(changedColumns);
            foreach (int column in changedColumns)
                // A changed corner also changes normals at the other corners of its incident faces.
                foreach (var polygon in document.PolygonsAt(column))
                    foreach (var vertex in polygon.vertices)
                        foreach (var chunk in columnChunks[document.ColumnOf(vertex)]) dirtyTerrain.Add(chunk);
            LastTerrainChunks = dirtyTerrain.Count;
            foreach (var chunk in dirtyTerrain) { BuildTerrain(chunk); dirtyOverlay.Add(chunk); }
            dirtyTerrain.Clear();
        }
        private void BuildTerrain(TerrainChunk chunk)
        {
            positions.Clear(); normals.Clear(); uv.Clear(); triangles.Clear();
            foreach (int column in chunk.Columns)
            {
                var p = document.GroundPosition(column); positions.Add(p); normals.Add(document.GroundNormal(column)); uv.Add(new Vector2(p.x, p.z) * 0.2f);
            }
            foreach (var polygon in chunk.Polygons)
            {
                int a = chunk.Local[document.ColumnOf(polygon.a)], b = chunk.Local[document.ColumnOf(polygon.b)],
                    c = chunk.Local[document.ColumnOf(polygon.c)], d = chunk.Local[document.ColumnOf(polygon.d)];
                Triangle(a, b, c); Triangle(a, c, d);
            }
            chunk.Collider.sharedMesh = null; chunk.Mesh.Clear(); chunk.Mesh.SetVertices(positions);
            chunk.Mesh.SetTriangles(triangles, 0); chunk.Mesh.SetNormals(normals); chunk.Mesh.SetUVs(0, uv);
            chunk.Mesh.RecalculateBounds(); chunk.Collider.sharedMesh = chunk.Mesh; chunk.Revision++;
        }
        private void Triangle(int a, int b, int c)
        {
            if (Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]).y < 0) { int swap = b; b = c; c = swap; }
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
        public bool TryPickTerrain(Ray ray, out Quad polygon)
        {
            polygon = null; float nearest = float.PositiveInfinity;
            foreach (var chunk in terrain)
            {
                if (!chunk.Mesh.bounds.IntersectRay(ray) || !chunk.Collider.Raycast(ray, out var hit, 500) || hit.distance >= nearest) continue;
                int index = hit.triangleIndex / 2;
                if (index < 0 || index >= chunk.Polygons.Count) continue;
                nearest = hit.distance; polygon = chunk.Polygons[index];
            }
            return polygon != null;
        }
        public bool ContainsXZ(Vector3 point)
        {
            foreach (var chunk in terrain)
            {
                var bounds = chunk.Mesh.bounds;
                if (point.x < bounds.min.x - 0.001f || point.x > bounds.max.x + 0.001f || point.z < bounds.min.z - 0.001f || point.z > bounds.max.z + 0.001f) continue;
                foreach (var polygon in chunk.Polygons)
                    if (Contains(point, polygon.a.position, polygon.b.position, polygon.c.position)
                        || Contains(point, polygon.a.position, polygon.c.position, polygon.d.position)) return true;
            }
            return false;
        }
        private static bool Contains(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float denominator = (b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
            if (Mathf.Abs(denominator) < 0.000001f) return false;
            float u = ((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/denominator;
            float v = ((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/denominator;
            return u >= -0.0001f && v >= -0.0001f && u+v <= 1.0001f;
        }
        public void SetBuilding(Cell cell, Model model)
        {
            var key = Key(cell.Center, true);
            if (!buildings.TryGetValue(key, out var chunk))
            {
                if (model == null) return;
                var mesh = NewMesh("Building mesh " + key); var root = MeshObject("Building chunk " + key, mesh, buildingMaterial);
                chunk = new BuildingChunk { Root = root, Mesh = mesh, Collider = root.AddComponent<MeshCollider>() }; buildings.Add(key, chunk);
            }
            if (model == null) chunk.Models.Remove(cell); else chunk.Models[cell] = model;
            dirtyBuildings.Add(chunk);
        }
        public void RefreshBuildings()
        {
            LastBuildingChunks = dirtyBuildings.Count;
            foreach (var chunk in dirtyBuildings)
            {
                chunk.Collider.sharedMesh = null; chunk.Root.SetActive(chunk.Models.Count > 0); combine.Clear();
                foreach (var model in chunk.Models.Values)
                    for (int sub = 0; sub < model.Mesh.subMeshCount; sub++)
                        combine.Add(new CombineInstance { mesh = model.Mesh, subMeshIndex = sub,
                            transform = chunk.Root.transform.worldToLocalMatrix * model.MeshFilter.transform.localToWorldMatrix });
                chunk.Mesh.Clear();
                if (combine.Count == 0) continue;
                chunk.Mesh.CombineMeshes(combine.ToArray(), true, true); chunk.Collider.sharedMesh = chunk.Mesh;
            }
            dirtyBuildings.Clear();
        }
        public void RefreshOverlay(int layer, bool showGrid, bool buildingTool, IReadOnlyList<Cell> dirtyCells = null)
        {
            if (layer != overlayLayer) { overlayLayer = layer; foreach (var chunk in terrain) dirtyOverlay.Add(chunk); }
            if (dirtyCells != null)
                foreach (var cell in dirtyCells) foreach (var vertex in cell.vertices)
                    foreach (var chunk in columnChunks[document.ColumnOf(vertex)]) dirtyOverlay.Add(chunk);
            foreach (var chunk in dirtyOverlay)
            {
                positions.Clear();
                foreach (var edge in chunk.Edges)
                { positions.Add(document.GroundPosition(document.ColumnOf(edge.a)) + Vector3.up * 0.025f); positions.Add(document.GroundPosition(document.ColumnOf(edge.b)) + Vector3.up * 0.025f); }
                Lines(chunk.GroundMesh);
                positions.Clear();
                foreach (var edge in chunk.Edges)
                { positions.Add(PlanePoint(document.ColumnOf(edge.a), layer)); positions.Add(PlanePoint(document.ColumnOf(edge.b), layer)); }
                Lines(chunk.PlaneMesh);
                positions.Clear();
                foreach (int column in chunk.Markers)
                {
                    if (!document.Grid.layers[layer].subVertices[column].IsEnabled) continue;
                    var p = PlanePoint(column, layer) + Vector3.up * 0.035f; const float radius = 0.15f;
                    positions.Add(p - Vector3.right * radius); positions.Add(p + Vector3.right * radius);
                    positions.Add(p - Vector3.forward * radius); positions.Add(p + Vector3.forward * radius);
                }
                Lines(chunk.PointMesh);
            }
            dirtyOverlay.Clear();
            bool plane = showGrid && buildingTool;
            if (gridVisible != showGrid || planeVisible != plane)
            {
                foreach (var chunk in terrain)
                { chunk.Ground.SetActive(showGrid); chunk.Plane.SetActive(plane); chunk.Points.SetActive(showGrid); }
                gridVisible = showGrid; planeVisible = plane;
            }
        }
        private Vector3 PlanePoint(int column, int layer) => (layer == document.TerrainLayer
            ? document.GroundPosition(column) : document.Grid.layers[layer].subVertices[column].position) + Vector3.up * 0.04f;
        private void Lines(Mesh mesh)
        {
            mesh.Clear(); mesh.SetVertices(positions); triangles.Clear();
            for (int i = 0; i < positions.Count; i++) triangles.Add(i);
            mesh.SetIndices(triangles, MeshTopology.Lines, 0); mesh.RecalculateBounds();
        }
        public void Dispose()
        {
            foreach (var chunk in terrain)
            {
                chunk.Collider.sharedMesh = null; Destroy(chunk.Root); Destroy(chunk.Ground); Destroy(chunk.Plane); Destroy(chunk.Points);
                Destroy(chunk.Mesh); Destroy(chunk.GroundMesh); Destroy(chunk.PlaneMesh); Destroy(chunk.PointMesh);
            }
            foreach (var chunk in buildings.Values) { chunk.Collider.sharedMesh = null; Destroy(chunk.Root); Destroy(chunk.Mesh); }
            terrain.Clear(); buildings.Clear(); dirtyTerrain.Clear(); dirtyOverlay.Clear(); dirtyBuildings.Clear();
        }
        private static void Destroy(UnityEngine.Object value)
        { if (value == null) return; if (value is GameObject root) root.SetActive(false); if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
