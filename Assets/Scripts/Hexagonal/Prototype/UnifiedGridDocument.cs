using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    /// <summary>A full-world construction lattice with a mildly undulating terrain skin.
    /// Contact uses data Cell footprints, never rectangular sites or render-mesh silhouettes.</summary>
    public sealed class UnifiedGridDocument
    {
        [Serializable]
        public sealed class Snapshot
        {
            public int version = 3;
            public string topology;
            public int terrainLayer = 2;
            public int wfcSeed = 42;
            public float[] offsets;
            public int[] enabled;
            public int[] ground; // Compatibility with v1 integer column heights.
        }
        public HexGrid Grid { get; }
        public int ColumnCount => offsets.Length;
        public int TerrainLayer { get; private set; } = 2;
        public int WfcSeed { get; private set; } = 42;
        public int MaxBuildingLevel => Grid.layers.Count - 2;
        public int MaxGroundLevel => MaxBuildingLevel - 3;
        public float MaxUndulation => Grid.cellHeight * 0.22f;
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public bool IsEditing => beforeStroke != null;
        public int TerrainRevision { get { UpdateSurface(); return terrainRevision; } }
        public int LastSurfaceUpdatedColumns { get; private set; }
        public int FoundationCellCount { get { UpdateSurface(); return foundationCells.Count; } }
        public string Status { get; private set; }
        private readonly float[] offsets, heights, influence;
        private readonly List<int>[] neighbors;
        private readonly List<Quad>[] columnPolygons;
        private readonly int[] occupiedLevels, foundationSources;
        private readonly HashSet<int> pendingSurface = new HashSet<int>(), dirtyTerrain = new HashSet<int>();
        private readonly Dictionary<Vertex, int> columns = new Dictionary<Vertex, int>();
        private readonly Dictionary<Vertex, int> pointIndices = new Dictionary<Vertex, int>();
        private readonly HashSet<Quad> polygons, foundationCells = new HashSet<Quad>();
        private readonly HashSet<Cell> dirty = new HashSet<Cell>();
        private readonly List<Snapshot> undo = new List<Snapshot>(), redo = new List<Snapshot>();
        private readonly string topology;
        private Snapshot beforeStroke;
        private bool changed, surfaceInitialized;
        private int terrainRevision;

        public UnifiedGridDocument(HexGrid grid)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (grid.layers.Count < 7 || grid.cells.Count == 0) throw new ArgumentException("Build at least six cell layers first.");
            offsets = new float[grid.baseLayer.subVertices.Count]; heights = new float[ColumnCount]; influence = new float[ColumnCount];
            neighbors = new List<int>[ColumnCount]; polygons = new HashSet<Quad>(grid.baseLayer.subQuads);
            columnPolygons = new List<Quad>[ColumnCount]; occupiedLevels = new int[ColumnCount]; foundationSources = new int[ColumnCount];
            uint hash = 2166136261;
            foreach (var vertex in grid.allSubVertices)
            {
                pointIndices.Add(vertex, pointIndices.Count);
                Hash(ref hash, Mathf.RoundToInt(vertex.x * 10000)); Hash(ref hash, Mathf.RoundToInt(vertex.y * 10000));
                Hash(ref hash, Mathf.RoundToInt(vertex.z * 10000)); vertex.IsEnabled = false;
            }
            for (int i = 0; i < ColumnCount; i++)
            { columns.Add(grid.baseLayer.subVertices[i], i); neighbors[i] = new List<int>(); columnPolygons[i] = new List<Quad>(); pendingSurface.Add(i); }
            foreach (var quad in grid.baseLayer.subQuads)
                foreach (var vertex in quad.vertices) { Hash(ref hash, columns[vertex]); columnPolygons[columns[vertex]].Add(quad); }
            foreach (var edge in grid.baseLayer.subEdges)
            { int a = columns[edge.a], b = columns[edge.b]; neighbors[a].Add(b); neighbors[b].Add(a); }
            topology = hash.ToString("X8", CultureInfo.InvariantCulture); MarkAllDirty();
        }
        private static void Hash(ref uint hash, int value) { unchecked { hash = (hash ^ (uint)value) * 16777619; } }
        public int ColumnOf(Vertex vertex) => pointIndices[vertex] % ColumnCount;
        public int LevelOf(Vertex vertex) => pointIndices[vertex] / ColumnCount;
        public int GroundLevel(int column) => TerrainLayer;
        public float HeightAtLevel(int level) => Grid.origin.y + level * Grid.cellHeight;
        public float NaturalOffset(int column)
        {
            var p = Grid.baseLayer.subVertices[column].position;
            return Grid.cellHeight * (0.10f * Mathf.Sin(p.x * 0.35f) * Mathf.Cos(p.z * 0.3f)
                + 0.035f * Mathf.Cos((p.x + p.z) * 0.55f));
        }
        public float RawGroundHeight(int column) => HeightAtLevel(TerrainLayer)
            + Mathf.Clamp(NaturalOffset(column) + offsets[column], -MaxUndulation, MaxUndulation);
        public Vector3 GroundPosition(int column)
        {
            UpdateSurface(); var position = Grid.baseLayer.subVertices[column].position;
            position.y = heights[column]; return position;
        }
        public bool IsFoundationCell(Quad polygon) { UpdateSurface(); return foundationCells.Contains(polygon); }
        public float FoundationInfluence(int column) { UpdateSurface(); return influence[column]; }
        public IReadOnlyList<Quad> PolygonsAt(int column) => columnPolygons[column];
        /// <summary>Area-weighted canonical normal, shared by every render chunk containing this column.</summary>
        public Vector3 GroundNormal(int column)
        {
            UpdateSurface(); Vector3 normal = Vector3.zero;
            foreach (var polygon in columnPolygons[column])
            {
                int a = columns[polygon.a], b = columns[polygon.b], c = columns[polygon.c], d = columns[polygon.d];
                Add(a, b, c); Add(a, c, d);
            }
            return normal.sqrMagnitude > 0 ? normal.normalized : Vector3.up;
            void Add(int a, int b, int c)
            {
                if (a != column && b != column && c != column) return;
                var face = Vector3.Cross(GroundPosition(b) - GroundPosition(a), GroundPosition(c) - GroundPosition(a));
                normal += face.y < 0 ? -face : face;
            }
        }
        // Visual waves never remove a column from the construction lattice.
        public bool CanBuild(int column, int level) => column >= 0 && column < ColumnCount
            && level >= TerrainLayer && level <= MaxBuildingLevel;

        private void InvalidateTerrain() { for (int i = 0; i < ColumnCount; i++) pendingSurface.Add(i); }
        private void MarkSurfaceRing(int column)
        {
            pendingSurface.Add(column);
            foreach (int next in neighbors[column])
            { pendingSurface.Add(next); foreach (int second in neighbors[next]) pendingSurface.Add(second); }
        }
        private void UpdateFoundation(int column)
        {
            foreach (var polygon in columnPolygons[column])
            {
                bool occupied = false;
                foreach (var vertex in polygon.vertices) occupied |= occupiedLevels[columns[vertex]] > 0;
                bool wasOccupied = foundationCells.Contains(polygon);
                if (occupied == wasOccupied) continue;
                if (occupied) foundationCells.Add(polygon); else foundationCells.Remove(polygon);
                foreach (var vertex in polygon.vertices)
                {
                    int corner = columns[vertex]; foundationSources[corner] += occupied ? 1 : -1;
                    MarkSurfaceRing(corner);
                }
            }
        }
        private float SurfaceInfluence(int column)
        {
            if (foundationSources[column] > 0) return 1;
            foreach (int next in neighbors[column]) if (foundationSources[next] > 0) return 0.5f;
            foreach (int next in neighbors[column])
                foreach (int second in neighbors[next]) if (foundationSources[second] > 0) return 0.15f;
            return 0;
        }
        private void UpdateSurface()
        {
            if (pendingSurface.Count == 0) return;
            LastSurfaceUpdatedColumns = pendingSurface.Count; bool any = false;
            foreach (int column in pendingSurface)
            {
                influence[column] = SurfaceInfluence(column);
                float height = Mathf.Lerp(RawGroundHeight(column), HeightAtLevel(TerrainLayer), influence[column]);
                if (surfaceInitialized && heights[column] == height) continue;
                heights[column] = height; dirtyTerrain.Add(column); any = true;
            }
            pendingSurface.Clear(); surfaceInitialized = true;
            if (any) terrainRevision++;
        }
        /// <summary>Canonical columns whose visible surface changed, including both sides of chunk seams.</summary>
        public void DrainTerrainDirty(List<int> columns)
        { UpdateSurface(); columns.Clear(); columns.AddRange(dirtyTerrain); dirtyTerrain.Clear(); }
        public void BeginStroke() { EndStroke(); beforeStroke = Capture(); changed = false; }
        public bool EndStroke()
        {
            if (beforeStroke == null) return false;
            bool committed = changed && !Equal(beforeStroke, Capture());
            if (committed) { Push(undo, beforeStroke); redo.Clear(); }
            beforeStroke = null; changed = false; return committed;
        }
        private static void Push(List<Snapshot> history, Snapshot snapshot)
        { if (history.Count == 64) history.RemoveAt(0); history.Add(snapshot); }
        public bool SetBuildingPoint(int column, int level, bool value)
        {
            if (!CanBuild(column, level)) { Status = "请在地形基准层或更高的网格层建造。"; return false; }
            var vertex = Grid.layers[level].subVertices[column]; if (vertex.IsEnabled == value) return false;
            bool single = !IsEditing; if (single) BeginStroke();
            ApplyPoint(vertex, value); changed = true;
            Status = value ? "已添加建筑点；对应 Cell 下方地形已自动贴合。" : "已擦除建筑点；空出的 Cell 恢复地形起伏。";
            if (single) EndStroke(); return true;
        }
        private void ApplyPoint(Vertex vertex, bool value)
        {
            if (vertex.IsEnabled == value) return;
            int column = ColumnOf(vertex); bool wasOccupied = occupiedLevels[column] > 0;
            vertex.IsEnabled = value; occupiedLevels[column] += value ? 1 : -1;
            if (wasOccupied != (occupiedLevels[column] > 0)) UpdateFoundation(column);
            foreach (var cell in Grid.Vertex2Cells(vertex)) dirty.Add(cell);
        }
        public bool EditTerrain(Quad polygon, float delta, bool flatten = false)
        {
            if (polygon == null || !polygons.Contains(polygon)) return false;
            if (float.IsNaN(delta) || float.IsInfinity(delta)) throw new ArgumentException("Invalid terrain adjustment.");
            var values = new float[4]; bool any = false;
            for (int i = 0; i < 4; i++)
            {
                int column = columns[polygon.vertices[i]];
                values[i] = flatten ? -NaturalOffset(column) : Mathf.Clamp(offsets[column] + delta, -MaxUndulation, MaxUndulation);
                any |= !Mathf.Approximately(values[i], offsets[column]);
            }
            if (!any) { Status = "已到起伏范围边界或已贴合基准层。"; return false; }
            bool single = !IsEditing; if (single) BeginStroke();
            for (int i = 0; i < 4; i++)
            { int column = columns[polygon.vertices[i]]; offsets[column] = values[i]; pendingSurface.Add(column); }
            changed = true;
            Status = IsFoundationCell(polygon) ? "已记录原始地形编辑；建筑 Cell 下方继续保持贴合。" : "已调整网格轻微起伏。";
            if (single) EndStroke(); return true;
        }
        public bool SetTerrainLayer(int level)
        {
            EndStroke();
            if (level < 1 || level > MaxGroundLevel || level == TerrainLayer) return false;
            int delta = level - TerrainLayer; var state = Capture();
            for (int i = 0; i < state.enabled.Length; i++)
            {
                int target = state.enabled[i] / ColumnCount + delta;
                if (target > MaxBuildingLevel) { Status = "剩余高度层不足，无法整体移动建筑。"; return false; }
                state.enabled[i] += delta * ColumnCount;
            }
            var before = Capture(); state.terrainLayer = level; Restore(state); Push(undo, before); redo.Clear();
            Status = "已设定全域地形基准层，建筑保持相对高度。"; return true;
        }
        public void ClearBuildings()
        {
            EndStroke(); BeginStroke();
            foreach (var vertex in Grid.allSubVertices)
                if (vertex.IsEnabled) { ApplyPoint(vertex, false); changed = true; }
            EndStroke(); Status = "已清空建筑，地形恢复原起伏；可撤销。";
        }
        public Snapshot Capture()
        {
            var enabled = new List<int>();
            for (int i = 0; i < Grid.allSubVertices.Count; i++) if (Grid.allSubVertices[i].IsEnabled) enabled.Add(i);
            return new Snapshot { topology = topology, terrainLayer = TerrainLayer, wfcSeed = WfcSeed,
                offsets = (float[])offsets.Clone(), enabled = enabled.ToArray() };
        }
        private static bool Equal(Snapshot a, Snapshot b)
        {
            if (a.terrainLayer != b.terrainLayer || a.wfcSeed != b.wfcSeed || a.offsets.Length != b.offsets.Length || a.enabled.Length != b.enabled.Length) return false;
            for (int i = 0; i < a.offsets.Length; i++) if (a.offsets[i] != b.offsets[i]) return false;
            for (int i = 0; i < a.enabled.Length; i++) if (a.enabled[i] != b.enabled[i]) return false;
            return true;
        }
        private Snapshot ValidateAndMigrate(Snapshot source)
        {
            if (source == null || (source.version != 1 && source.version != 2 && source.version != 3) || source.topology != topology || source.enabled == null)
                throw new ArgumentException("存档网格或版本不匹配。");
            var state = new Snapshot { topology = topology, terrainLayer = source.version == 1 ? 2 : source.terrainLayer,
                wfcSeed = source.version >= 3 ? source.wfcSeed : 42,
                offsets = new float[ColumnCount], enabled = (int[])source.enabled.Clone() };
            if (state.terrainLayer < 1 || state.terrainLayer > MaxGroundLevel) throw new ArgumentException("无效的地形基准层。");
            if (source.version == 1)
            {
                if (source.ground == null || source.ground.Length != ColumnCount) throw new ArgumentException("旧版地形数据不匹配。");
                foreach (int height in source.ground)
                    if (height < 1 || height > MaxGroundLevel) throw new ArgumentException("无效的旧版地形高度。");
                for (int i = 0; i < ColumnCount; i++) state.offsets[i] = Mathf.Clamp((source.ground[i] - 2) * Grid.cellHeight, -MaxUndulation, MaxUndulation);
            }
            else
            {
                if (source.offsets == null || source.offsets.Length != ColumnCount) throw new ArgumentException("地形顶点数量不匹配。");
                for (int i = 0; i < ColumnCount; i++)
                {
                    float value = source.offsets[i];
                    if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value) > MaxUndulation + 0.00001f)
                        throw new ArgumentException("无效的地形起伏数据。");
                    state.offsets[i] = value;
                }
            }
            var unique = new HashSet<int>();
            for (int i = 0; i < state.enabled.Length; i++)
            {
                int index = state.enabled[i];
                if (index < 0 || index >= Grid.allSubVertices.Count || !unique.Add(index)) throw new ArgumentException("无效或重复的建筑点。");
                int column = index % ColumnCount, level = index / ColumnCount;
                int minimum = source.version == 1 ? source.ground[column] : state.terrainLayer;
                if (level < minimum || level > MaxBuildingLevel) throw new ArgumentException("建筑点超出可编辑层。");
                if (source.version == 1)
                {
                    int migratedLevel = 2 + level - minimum;
                    if (migratedLevel > MaxBuildingLevel) throw new ArgumentException("旧版建筑超出新基准层的高度范围。");
                    state.enabled[i] = migratedLevel * ColumnCount + column;
                }
            }
            Array.Sort(state.enabled); return state;
        }
        public void Restore(Snapshot source)
        {
            var state = ValidateAndMigrate(source);
            if (TerrainLayer != state.terrainLayer) InvalidateTerrain();
            TerrainLayer = state.terrainLayer; WfcSeed = state.wfcSeed;
            for (int column = 0; column < ColumnCount; column++)
                if (offsets[column] != state.offsets[column]) { offsets[column] = state.offsets[column]; pendingSurface.Add(column); }
            var enabled = new HashSet<int>(state.enabled);
            for (int i = 0; i < Grid.allSubVertices.Count; i++)
                if (Grid.allSubVertices[i].IsEnabled != enabled.Contains(i)) ApplyPoint(Grid.allSubVertices[i], enabled.Contains(i));
        }
        public string ExportJson() => JsonUtility.ToJson(Capture(), true);
        public void ImportJson(string json)
        {
            EndStroke(); var before = Capture(); var state = JsonUtility.FromJson<Snapshot>(json); Restore(state);
            if (!Equal(before, Capture())) { Push(undo, before); redo.Clear(); }
            Status = state.version == 1 ? "已将旧存档转换为统一基准层，建筑相对层高保留。" : "已读取全域网格、地形起伏和建筑。";
        }
        public bool Undo()
        {
            EndStroke(); if (undo.Count == 0) return false;
            var current = Capture(); Restore(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1); Push(redo, current); return true;
        }
        public bool Redo()
        {
            EndStroke(); if (redo.Count == 0) return false;
            var current = Capture(); Restore(redo[redo.Count - 1]); redo.RemoveAt(redo.Count - 1); Push(undo, current); return true;
        }
        public void MarkAllDirty() { foreach (var cell in Grid.cells) dirty.Add(cell); }
        public void RerollBuildingModules()
        {
            EndStroke(); var before = Capture(); WfcSeed = unchecked(WfcSeed + 101);
            Push(undo, before); redo.Clear(); Status = "已重新坍塌建筑模块；建筑占用和地形不变，可撤销。";
        }
        public void DrainDirty(List<Cell> cells) { cells.Clear(); cells.AddRange(dirty); dirty.Clear(); }
        public void LoadExample(bool buildings = true)
        {
            EndStroke(); var before = Capture(); var state = Capture(); var points = new List<int>();
            state.terrainLayer = 2; state.offsets = new float[ColumnCount];
            for (int column = 0; column < ColumnCount && buildings; column++)
            {
                var p = Grid.baseLayer.subVertices[column].position;
                for (int house = 0; house < 3; house++)
                {
                    var center = house == 0 ? new Vector2(-7, -3) : house == 1 ? new Vector2(7, 0) : new Vector2(-7, 9);
                    float width = house == 1 ? 3.2f : 2.8f, depth = house == 2 ? 2.8f : 3.2f;
                    float x = Mathf.Abs(p.x - center.x), z = p.z - center.y;
                    if (x >= width || Mathf.Abs(z) >= depth) continue;
                    bool wall = x > width - 1.7f || Mathf.Abs(z) > depth - 1.7f;
                    for (int level = 0; level < 3; level++)
                    {
                        bool door = x < 1.1f && z < -depth + 1.7f && level > 0;
                        if (level == 0 || (wall && !door)) points.Add((2 + level) * ColumnCount + column);
                    }
                }
            }
            state.enabled = points.ToArray(); Restore(state);
            if (!Equal(before, Capture())) { Push(undo, before); redo.Clear(); }
            Status = buildings ? "三处建筑共用地形基准层；建筑 Cell 下方自动贴平，其余网格轻微起伏。" : "全域轻微起伏地形；每个网格 Cell 都可直接建造。";
        }
    }
}

