using System;
using System.Collections.Generic;
using System.IO;
using MarchingCube;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexagonalGrids.Prototype
{
    [DefaultExecutionOrder(100)]
    public sealed class UnifiedGridBuildingDemo : MonoBehaviour
    {
        [SerializeField] private ModelLibrary library;
        [SerializeField] private BuildingTileset buildingTileset;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private int seed = 42;
        [SerializeField] private bool showExampleOnStart = true;
        [SerializeField, Min(4)] private float chunkSize = 16;
        [SerializeField, Min(1)] private int gridRadius = 6;
        [SerializeField] private bool useCoordinateLayout = true;
        [SerializeField] private HexGridLayout mapLayout = new HexGridLayout();
        [SerializeField, Min(0)] private int relaxIterations = 30;
        [SerializeField] private bool preserveBoundary;
        public enum EditTool { Raise, Lower, Level, Add, Erase }
        public UnifiedGridDocument Document { get; private set; }
        public Camera ViewCamera => viewCamera;
        public IReadOnlyList<UnifiedGridChunks.TerrainChunk> TerrainChunks => chunks?.Terrain;
        public int LastTerrainChunks => chunks == null ? 0 : chunks.LastTerrainChunks;
        public int LastBuildingChunks => chunks == null ? 0 : chunks.LastBuildingChunks;
        public int BuildingChunkCount => chunks == null ? 0 : chunks.BuildingChunkCount;
        public int ActiveLayer { get; private set; } = 2;
        public int ActiveModelCount => views.Count;
        public int ConstructionEdgeCount => chunks == null ? 0 : chunks.ConstructionEdgeCount;
        public int LastUpdatedCells { get; private set; }
        public BuildingWaveFunction WaveFunction { get; private set; }
        public EditTool Tool { get; private set; } = EditTool.Add;
        public string SavePath => Path.Combine(Application.persistentDataPath, "unified-grid-building-v3.json");
        private sealed class View { public Model model; public ModelBuilder builder; public byte state; public int variant = -1; }
        private readonly Dictionary<Cell, View> views = new Dictionary<Cell, View>();
        private readonly List<Cell> dirty = new List<Cell>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly HashSet<Quad> paintedPolygons = new HashSet<Quad>();
        private GameObject generated, cursor;
        private ModelPool pool;
        private Mesh selectionMesh;
        private UnifiedGridChunks chunks;
        private Material buildingMaterial, cursorMaterial;
        private bool showGrid = true, painting, lastErase;
        private Quad hoveredQuad;
        private Vertex hoveredPoint;
        private Vector2 previousMouse, scrollPosition;
        private Vector3 pivot = new Vector3(-2, 1.8f, 1);
        private float yaw = 25, pitch = 48, distance = 58;
        private string status;
        private GUIStyle heading, text, button;
        private Rect Panel => new Rect(16, 16, 304, Mathf.Max(100, Mathf.Min(690, Screen.height - 32)));
        private bool BuildingTool => Tool == EditTool.Add || Tool == EditTool.Erase;

        public void Configure(ModelLibrary models, Camera camera, bool example = true, BuildingTileset tileset = null)
        { library = models; viewCamera = camera; showExampleOnStart = example; buildingTileset = tileset; }
        public void ConfigureLayout(HexGridLayout layout)
        {
            if (Document != null) throw new InvalidOperationException("Release the preview before changing the map layout.");
            mapLayout = layout ?? throw new ArgumentNullException(nameof(layout)); useCoordinateLayout = true;
        }
        public void ConfigureRelaxation(int iterations, bool preserveBoundary = false)
        {
            if (Document != null) throw new InvalidOperationException("Release the preview before changing relaxation.");
            if (iterations < 0) throw new ArgumentOutOfRangeException(nameof(iterations));
            relaxIterations = iterations;
            this.preserveBoundary = preserveBoundary;
        }
        private void Start() { if (Document == null) Initialize(); }
        private void OnEnable()
        { if (Application.isPlaying && Document == null && library != null && viewCamera != null) Initialize(); }
        public void Initialize()
        {
            if (Document != null) return;
            if (library == null || viewCamera == null) throw new InvalidOperationException("Unified demo requires the PCG library and a camera.");
            for (int i = 1; i < 255; i++)
                if (!library.GetModel((byte)i).HasValue || library.GetModel((byte)i).Value.mesh == null)
                    throw new InvalidOperationException($"Missing PCG model state {i}.");
            var random = UnityEngine.Random.state;
            HexGrid grid;
            try
            {
                UnityEngine.Random.InitState(seed);
                grid = useCoordinateLayout ? mapLayout.CreateGrid(2, new Vector3(0, -2.7f, 0), 1.35f, 14)
                    : new HexGrid(Mathf.Max(1, gridRadius), 2, new Vector3(0, -2.7f, 0), 1.35f, 14);
                if (grid.triangles.Count == 0) throw new InvalidOperationException("The map layout must contain at least one hexagonal tile.");
                grid.RandomMergeTriangles(); grid.SubdivideGrid();
                grid.RelaxGrid(relaxIterations, useCoordinateLayout && preserveBoundary);
                grid.BuildLayers(); grid.BuildCells();
            }
            finally { UnityEngine.Random.state = random; }
            Document = new UnifiedGridDocument(grid);
            generated = new GameObject("Unified Grid Generated") { hideFlags = HideFlags.DontSave };
            generated.transform.SetParent(transform, true); generated.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            generated.transform.localScale = Vector3.one;
            var soil = NewMaterial("Grass terrain", new Color(0.38f, 0.52f, 0.28f));
            buildingMaterial = NewMaterial("PCG warm plaster", new Color(0.86f, 0.69f, 0.43f));
            var wire = NewMaterial("Ground grid", new Color(0.16f, 0.24f, 0.12f), true);
            var plane = NewOverlayMaterial("Full-world build plane", new Color(0.25f, 0.68f, 0.76f, 0.65f));
            var point = NewOverlayMaterial("Enabled points", new Color(1f, 0.72f, 0.2f));
            cursorMaterial = NewOverlayMaterial("Brush preview", new Color(0.2f, 0.95f, 0.7f));
            chunks = new UnifiedGridChunks(Document, generated.transform, chunkSize, soil, buildingMaterial, wire, plane, point);
            selectionMesh = NewMesh("Brush polygon"); MeshObject("Polygon selection", selectionMesh, point);
            cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere); cursor.name = "Building point preview"; cursor.layer = 30;
            cursor.transform.SetParent(generated.transform, false); DestroyResource(cursor.GetComponent<Collider>());
            cursor.GetComponent<MeshRenderer>().sharedMaterial = cursorMaterial; cursor.transform.localScale = Vector3.one * 0.3f;
            cursor.SetActive(false);
            var buildings = new GameObject("Freeform PCG buildings"); buildings.transform.SetParent(generated.transform, false);
            pool = buildings.AddComponent<ModelPool>(); pool.ConfigureCapacity(grid.cells.Count);
            WaveFunction = new BuildingWaveFunction(grid, library, buildingTileset);
            if (showExampleOnStart) Document.LoadExample();
            Refresh(); FrameCamera();
            status = "全地形可建筑 · Cell 底部自动贴地 · 岩石已隔离";
        }

        private Material NewOverlayMaterial(string name, Color color)
        {
            var shader = Shader.Find("PCG/Construction Grid Overlay");
            if (shader == null) throw new InvalidOperationException("Construction grid overlay shader is missing.");
            var result = new Material(shader) { name = name, color = color, hideFlags = HideFlags.DontSave };
            owned.Add(result); return result;
        }
        private Material NewMaterial(string name, Color color, bool unlit = false)
        {
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var result = new Material(shader) { name = name, color = color, hideFlags = HideFlags.DontSave };
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", 0.05f);
            owned.Add(result); return result;
        }
        private Mesh NewMesh(string name)
        {
            var result = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            owned.Add(result); return result;
        }
        private GameObject MeshObject(string name, Mesh mesh, Material material)
        {
            var result = new GameObject(name) { layer = 30 }; result.transform.SetParent(generated.transform, false);
            result.AddComponent<MeshFilter>().sharedMesh = mesh; result.AddComponent<MeshRenderer>().sharedMaterial = material;
            return result;
        }
        private static void SetLines(Mesh mesh, List<Vector3> points)
        {
            mesh.Clear(); mesh.SetVertices(points); var indices = new int[points.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Lines, 0); mesh.RecalculateBounds();
        }
        public void Refresh()
        {
            if (Document == null) return;
            ActiveLayer = Mathf.Clamp(ActiveLayer, Document.TerrainLayer, Document.MaxBuildingLevel);
            chunks.RefreshTerrain();
            Document.DrainDirty(dirty);
            var moduleChanges = WaveFunction.Update(dirty, Document.WfcSeed);
            if (moduleChanges.Count > 0)
            {
                var unique = new HashSet<Cell>(dirty);
                foreach (var cell in moduleChanges) if (unique.Add(cell)) dirty.Add(cell);
            }
            LastUpdatedCells = dirty.Count;
            foreach (var cell in dirty)
            {
                byte state = cell.GetCellByte();
                if (state == 0 || state == 255)
                {
                    if (views.TryGetValue(cell, out var old))
                    { chunks.SetBuilding(cell, null); pool.Release(old.model); views.Remove(cell); }
                    continue;
                }
                if (!views.TryGetValue(cell, out var view))
                {
                    var model = pool.Get(); if (model == null) throw new InvalidOperationException("PCG model pool exhausted.");
                    model.gameObject.layer = 30;
                    model.Cell = cell; model.Library = library;
                    // Per-cell meshes are deformation sources; only spatial chunks render and collide.
                    model.MeshRenderer.enabled = false;
                    view = new View { model = model, builder = new ModelBuilder(model) };
                    views.Add(cell, view);
                }
                else if (view.state == state && view.variant == WaveFunction.Choice(cell)) continue;
                view.builder.Build(WaveFunction.Selected(cell));
                view.state = state; view.variant = WaveFunction.Choice(cell);
                chunks.SetBuilding(cell, view.model);
            }
            chunks.RefreshBuildings();
            chunks.RefreshOverlay(ActiveLayer, showGrid, BuildingTool, dirty);
        }
        private void RebuildOverlay()
            => chunks?.RefreshOverlay(ActiveLayer, showGrid, BuildingTool);
        public void SetLayer(int level)
        {
            FinishStroke(); ActiveLayer = Mathf.Clamp(level, Document.TerrainLayer, Document.MaxBuildingLevel);
            hoveredPoint = null; cursor.SetActive(false); RebuildOverlay();
        }
        public void SetTool(EditTool tool)
        {
            FinishStroke(); Tool = tool; hoveredPoint = null; hoveredQuad = null;
            cursor.SetActive(false); selectionMesh.Clear(); RebuildOverlay();
        }
        public bool TryPickPoint(Ray ray, out Vertex vertex)
        {
            vertex = null; var layer = Document.Grid.layers[ActiveLayer];
            if (!new Plane(Vector3.up, layer.subVertices[0].position).Raycast(ray, out float enter)) return false;
            var p = ray.GetPoint(enter);
            // Use the full polygon domain, including long irregular quads and outermost cells.
            // A distance cutoff around each vertex used to leave unpickable holes inside valid cells.
            if (!chunks.ContainsXZ(p)) return false;
            if (layer.vertexKDTree == null) layer.vertexKDTree = new VertexKdTree(layer.subVertices);
            var nearest = layer.vertexKDTree.QueryNearest(p);
            if (nearest == null) return false;
            vertex = nearest; return true;
        }
        public bool TryPickTerrain(Ray ray, out Quad polygon)
            => chunks.TryPickTerrain(ray, out polygon);
        public void SampleGroundLayer(Ray ray)
        {
            if (!TryPickTerrain(ray, out _)) return;
            SetLayer(Document.TerrainLayer); status = "已回到全域地形基准层；所有网格均可直接建造。";
        }
        private void Preview(Ray ray)
        {
            hoveredPoint = null; hoveredQuad = null;
            if (BuildingTool) TryPickPoint(ray, out hoveredPoint); else TryPickTerrain(ray, out hoveredQuad);
            cursor.SetActive(hoveredPoint != null);
            if (hoveredPoint != null)
            {
                cursor.transform.position = ActiveLayer == Document.TerrainLayer
                    ? Document.GroundPosition(Document.ColumnOf(hoveredPoint)) + Vector3.up * 0.06f : hoveredPoint.position;
                cursorMaterial.color = Document.CanBuild(Document.ColumnOf(hoveredPoint), ActiveLayer)
                    ? new Color(0.2f, 0.95f, 0.7f) : new Color(1, 0.18f, 0.12f);
            }
            var lines = new List<Vector3>();
            if (hoveredQuad != null)
                for (int i = 0; i < 4; i++)
                {
                    lines.Add(Document.GroundPosition(Document.ColumnOf(hoveredQuad.vertices[i])) + Vector3.up * 0.07f);
                    lines.Add(Document.GroundPosition(Document.ColumnOf(hoveredQuad.vertices[(i + 1) % 4])) + Vector3.up * 0.07f);
                }
            SetLines(selectionMesh, lines);
        }
        private void Paint(Ray ray, bool erase)
        {
            if (BuildingTool)
            {
                if (TryPickPoint(ray, out var vertex))
                {
                    Document.SetBuildingPoint(Document.ColumnOf(vertex), ActiveLayer, !erase && Tool == EditTool.Add);
                    status = Document.Status;
                }
            }
            else if (TryPickTerrain(ray, out var polygon) && paintedPolygons.Add(polygon))
            {
                Document.EditTerrain(polygon, erase || Tool == EditTool.Lower ? -0.10f : 0.10f,
                    !erase && Tool == EditTool.Level); status = Document.Status;
            }
        }
        private void FinishStroke()
        { Document?.EndStroke(); painting = false; paintedPolygons.Clear(); }
        private void Update()
        {
            if (Document == null) return;
            ReadInput(out var mouse, out bool left, out bool right, out bool middle, out bool shift, out float scroll);
            bool overUi = Panel.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            HandleKeys(mouse);
            if (middle && !overUi)
            {
                FinishStroke(); var delta = mouse - previousMouse;
                if (shift) pivot += (-viewCamera.transform.right * delta.x - viewCamera.transform.up * delta.y) * distance * 0.0013f;
                else { yaw += delta.x * 0.25f; pitch = Mathf.Clamp(pitch - delta.y * 0.25f, 18, 85); }
            }
            if (!overUi) distance = Mathf.Clamp(distance * Mathf.Exp(-scroll * 0.001f), 9, 110);
            FrameCamera();
            if (!overUi && !middle)
            {
                Preview(viewCamera.ScreenPointToRay(mouse));
                if (left || right)
                {
                    bool starting = !painting || lastErase != right;
                    if (starting) { FinishStroke(); Document.BeginStroke(); painting = true; lastErase = right; }
                    // Sample the cursor segment so fast drags cannot skip whole polygons or points.
                    int steps = starting ? 1 : Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(mouse, previousMouse) / 6), 1, 64);
                    for (int i = 1; i <= steps; i++)
                        Paint(viewCamera.ScreenPointToRay(starting ? mouse : Vector2.Lerp(previousMouse, mouse, i / (float)steps)), right);
                }
                else FinishStroke();
            }
            else { FinishStroke(); cursor.SetActive(false); selectionMesh.Clear(); }
            previousMouse = mouse; Refresh();
        }
        private void FrameCamera()
        {
            if (viewCamera == null) return;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            // Offset focus slightly so the left-side editing panel does not cover the examples.
            viewCamera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }
        public void ResetCamera() { yaw = 25; pitch = 48; distance = 58; pivot = new Vector3(-2, 1.8f, 1); FrameCamera(); }
        public void LoadExample(bool buildings = true)
        { FinishStroke(); Document.LoadExample(buildings); status = Document.Status; Refresh(); }
        public bool UndoEdit() { FinishStroke(); bool result = Document.Undo(); Refresh(); status = "已撤销。"; return result; }
        public bool RedoEdit() { FinishStroke(); bool result = Document.Redo(); Refresh(); status = "已重做。"; return result; }
        public void SetTerrainBaseToActiveLayer()
        {
            FinishStroke();
            if (Document.SetTerrainLayer(ActiveLayer)) { status = Document.Status; Refresh(); }
            else status = Document.Status ?? "当前层已是地形基准层。";
        }
        public void SaveEdits()
        {
            FinishStroke();
            try { Directory.CreateDirectory(Application.persistentDataPath); File.WriteAllText(SavePath, Document.ExportJson()); status = "地形和全部建筑点已保存。"; }
            catch (Exception exception) { status = "保存失败：" + exception.Message; }
        }
        public void LoadEdits()
        {
            FinishStroke();
            try
            {
                var version2 = Path.Combine(Application.persistentDataPath, "unified-grid-building-v2.json");
                var version1 = Path.Combine(Application.persistentDataPath, "unified-grid-building-v1.json");
                Document.ImportJson(File.ReadAllText(File.Exists(SavePath) ? SavePath : File.Exists(version2) ? version2 : version1));
                Refresh(); status = Document.Status;
            }
            catch (Exception exception) { status = "读取失败：" + exception.Message; }
        }
        private void OnGUI()
        {
            if (heading == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 16); owned.Add(font);
                heading = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, fontStyle = FontStyle.Bold };
                text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 14, fixedHeight = 32 };
            }
            GUI.Box(Panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(Panel.x + 14, Panel.y + 12, Panel.width - 28, Panel.height - 24));
            GUILayout.Label("网格地形 × 自由建造", heading);
            if (Document == null) { GUILayout.Label("准备网格…", text); GUILayout.EndArea(); return; }
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            GUILayout.Label("全域网格 · 地形轻微起伏 · Cell 贴合", text);
            int mode = GUILayout.SelectionGrid(BuildingTool ? 1 : 0, new[] { "地形编辑", "建筑编辑" }, 2, button);
            if (mode != (BuildingTool ? 1 : 0)) SetTool(mode == 1 ? EditTool.Add : EditTool.Raise);
            if (BuildingTool)
            {
                int tool = GUILayout.SelectionGrid(Tool == EditTool.Add ? 0 : 1, new[] { "添加点", "擦除点" }, 2, button);
                if (tool != (Tool == EditTool.Add ? 0 : 1)) SetTool(tool == 0 ? EditTool.Add : EditTool.Erase);
            }
            else
            {
                int tool = GUILayout.SelectionGrid((int)Tool, new[] { "抬高地表 0.10 m", "降低地表 0.10 m", "贴平到地形基准层" }, 1, button);
                if (tool != (int)Tool) SetTool((EditTool)tool);
            }
            GUILayout.Space(6);
            GUILayout.Label($"编辑层 {ActiveLayer} / {Document.MaxBuildingLevel}   ·   {Document.HeightAtLevel(ActiveLayer):F2} m", text);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("降低 [Q]", button)) SetLayer(ActiveLayer - 1);
            if (GUILayout.Button("升高 [E]", button)) SetLayer(ActiveLayer + 1);
            GUILayout.EndHorizontal();
            GUILayout.Label($"地形基准层 {Document.TerrainLayer} · 起伏 ±{Document.MaxUndulation:F2} m", text);
            GUI.enabled = ActiveLayer != Document.TerrainLayer && ActiveLayer <= Document.MaxGroundLevel;
            if (GUILayout.Button("设当前层为全域地形基准", button)) SetTerrainBaseToActiveLayer();
            GUI.enabled = true;
            if (GUILayout.Button("回到地形基准层 [F]", button)) SetLayer(Document.TerrainLayer);
            GUILayout.BeginHorizontal();
            GUI.enabled = Document.UndoCount > 0 || Document.IsEditing; if (GUILayout.Button("撤销", button)) UndoEdit();
            GUI.enabled = Document.RedoCount > 0; if (GUILayout.Button("重做", button)) RedoEdit();
            GUI.enabled = true; GUILayout.EndHorizontal();
            GUILayout.Space(6);
            if (GUILayout.Button("载入三处建筑示例", button)) LoadExample();
            if (GUILayout.Button("WFC 换一组建筑模块", button))
            { FinishStroke(); Document.RerollBuildingModules(); Refresh(); status = Document.Status; }
            if (GUILayout.Button("载入纯地形", button)) LoadExample(false);
            if (GUILayout.Button("清空建筑，保留地形", button)) { FinishStroke(); Document.ClearBuildings(); Refresh(); status = Document.Status; }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showGrid ? "隐藏网格" : "显示网格", button)) { showGrid = !showGrid; RebuildOverlay(); }
            if (GUILayout.Button("视角归位", button)) ResetCamera();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存", button)) SaveEdits();
            if (GUILayout.Button("读取", button)) LoadEdits();
            GUILayout.EndHorizontal();
            GUILayout.Label($"建筑模块 {views.Count} · 贴合 Cell {Document.FoundationCellCount}", text);
            GUILayout.Label($"建筑块 {BuildingChunkCount} · 地形块 {chunks.Terrain.Count}\n本帧更新：地形 {LastTerrainChunks} 块 · 建筑 {LastBuildingChunks} 块", text);
            GUILayout.Label($"WFC 候选 {WaveFunction.Catalog.Variants.Count} · 求解 {WaveFunction.LastSolvedCells} Cell\n约束传播 {WaveFunction.LastPropagations} · 回溯 {WaveFunction.LastBacktracks}", text);
            if (WaveFunction.LastFallbacks > 0) GUILayout.Label(WaveFunction.LastError, text);
            GUILayout.Space(6); GUILayout.Label(status, text);
            GUILayout.Label("左键拖动编辑 · 右键擦除/降低\n中键旋转 · Shift+中键平移\n滚轮缩放 · Ctrl+Z / Ctrl+Y\n青色为当前建造层，黄色为已启用点。", text);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void HandleKeys(Vector2 mouse)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.qKey.wasPressedThisFrame) SetLayer(ActiveLayer - 1);
            if (keyboard.eKey.wasPressedThisFrame) SetLayer(ActiveLayer + 1);
            if (keyboard.fKey.wasPressedThisFrame && !Panel.Contains(new Vector2(mouse.x, Screen.height - mouse.y))) SampleGroundLayer(viewCamera.ScreenPointToRay(mouse));
            if (keyboard.ctrlKey.isPressed && keyboard.zKey.wasPressedThisFrame) { if (keyboard.shiftKey.isPressed) RedoEdit(); else UndoEdit(); }
            if (keyboard.ctrlKey.isPressed && keyboard.yKey.wasPressedThisFrame) RedoEdit();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Q)) SetLayer(ActiveLayer - 1);
            if (Input.GetKeyDown(KeyCode.E)) SetLayer(ActiveLayer + 1);
            if (Input.GetKeyDown(KeyCode.F) && !Panel.Contains(new Vector2(mouse.x, Screen.height - mouse.y))) SampleGroundLayer(viewCamera.ScreenPointToRay(mouse));
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z)) UndoEdit();
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Y)) RedoEdit();
#endif
        }
        private static void ReadInput(out Vector2 mouse, out bool left, out bool right, out bool middle, out bool shift, out float scroll)
        {
            mouse = Vector2.zero; left = right = middle = shift = false; scroll = 0;
#if ENABLE_INPUT_SYSTEM
            var pointer = Mouse.current;
            if (pointer != null) { mouse = pointer.position.ReadValue(); left = pointer.leftButton.isPressed; right = pointer.rightButton.isPressed; middle = pointer.middleButton.isPressed; scroll = pointer.scroll.ReadValue().y; }
            shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            mouse = Input.mousePosition; left = Input.GetMouseButton(0); right = Input.GetMouseButton(1); middle = Input.GetMouseButton(2);
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift); scroll = Input.mouseScrollDelta.y * 120;
#endif
        }
        private void OnApplicationFocus(bool focus) { if (!focus) FinishStroke(); }
        // Editor tools may initialize a preview without entering MonoBehaviour's runtime lifecycle.
        public void ReleasePreview() => Release();
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
        private void Release()
        {
            FinishStroke(); if (generated != null) generated.SetActive(false);
            chunks?.Dispose(); chunks = null;
            if (pool != null) pool.Clear(); views.Clear();
            DestroyResource(generated); foreach (var item in owned) DestroyResource(item); owned.Clear();
            Document = null; WaveFunction = null; generated = null; pool = null; heading = text = button = null;
        }
        private static void DestroyResource(UnityEngine.Object resource)
        { if (resource == null) return; if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource); }
    }
}
