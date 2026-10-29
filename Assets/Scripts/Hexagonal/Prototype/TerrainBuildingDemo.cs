using System;
using System.Collections.Generic;
using MarchingCube;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexagonalGrids.Prototype
{
    public sealed class TerrainBuildingDemo : MonoBehaviour
    {
        [SerializeField] private ModelLibrary library;
        [SerializeField] private CliffRockLibrary cliffRocks;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private int seed = 42;
        [SerializeField, Range(1, 8)] private float cliffHeight = 4.5f;
        [SerializeField, Range(0, 1), InspectorName("岩石覆盖量")] private float rockDensity = 0.52f;
        [SerializeField, Range(0, 1), InspectorName("贴坡程度")] private float rockConformity = 0.65f;
        private int rockSeedOffset;
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private readonly List<Model> models = new List<Model>();
        [SerializeField, HideInInspector] private GameObject generated;
        private GameObject buildingRoot, wireObject, cliffObject, selectionObject;
        private ModelPool pool;
        private Mesh terrainMesh, wireMesh, cliffMesh, selectionMesh;
        private MeshCollider terrainCollider, cliffCollider;
        private SlopeRockCover rockMass;
        private Material soil, wall, roof, lines, rock;
        private Vector2 siteCenter;
        private float heightOffset, yaw = 28, distance = 36, pitch = 42, editStep = 0.5f;
        private bool graded = false, showWire = true, rocky = true, rockDetail = true;
        public enum EditTool { Select, Raise, Lower, PlaceBuilding }
        private EditTool tool = EditTool.Select;
        private Quad selectedQuad;
        private readonly Stack<EditState> undo = new Stack<EditState>(), redo = new Stack<EditState>();
        private EditState rockSliderBefore;
        [Serializable] private sealed class EditState
        {
            public int version = 3, gridSeed, rockVariation;
            public float[] heights;
            public Vector2 center;
            public float offset, rise;
            public float coverage, conformity;
            public bool building, cliff, stones;
        }
        private GUIStyle heading, label, button;
        private string status;
        private Vector2 panelScroll;
        public TerrainSiteData Surface { get; private set; }
        public TerrainSlopeField Slopes { get; private set; }
        public int ModelCount => models.Count;
        public Camera ViewCamera => viewCamera;
        public float FloorHeight => Surface.PlatformHeight;
        public GameObject BuildingRoot => buildingRoot;
        public Mesh CliffMesh => cliffMesh;
        public Mesh TerrainMesh => terrainMesh;
        public MeshCollider TerrainCollider => terrainCollider;
        public MeshCollider CliffCollider => cliffCollider;
        public int ToeStoneCount { get; private set; }
        public int FaceStoneCount { get; private set; }
        public int RockChunkCount => rockMass?.ChunkCount ?? 0;
        public int RebuiltRockChunks => rockMass?.RebuiltChunks ?? 0;
        private Rect Panel => new Rect(18, 18, 320, Mathf.Min(780, Screen.height - 36));
        public string EditSavePath => System.IO.Path.Combine(Application.persistentDataPath, "editable-terrain-demo.json");
        public int UndoCount => undo.Count;
        public bool HasBuilding => graded;
        public float RockCoverage => rockDensity;
        public float RockConformity => rockConformity;
        public void RefreshPreview() { if (Surface == null) Initialize(); else Rebuild(); }

        public void CreateSlopeExample()
        {
            Edit(() => {
                graded = false; heightOffset = 0;
                var heights = new float[Surface.Indices.Count];
                foreach (var pair in Surface.Indices)
                {
                    var p = pair.Key.position;
                    // Broad plateau spanning multiple editable quads, with a complete perimeter.
                    float edge = Mathf.Max(Mathf.Abs(p.x + 1) - 3.0f, Mathf.Abs(p.z) - 2.5f);
                    heights[pair.Value] = 5.8f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 1.65f, edge)));
                }
                Surface.RestoreHeightOffsets(heights); rockDetail = true; Rebuild(); FrameCamera();
            });
        }

        public void Configure(ModelLibrary modelsLibrary, Camera camera, CliffRockLibrary rocks = null)
        { library = modelsLibrary; viewCamera = camera; cliffRocks = rocks; }
        public void SetRockLibrary(CliffRockLibrary rocks)
        {
            cliffRocks = rocks;
            if (Surface != null) { rockMass?.Dispose(); rockMass = new SlopeRockCover(); Rebuild(); }
        }

        private void Start() { if (Surface == null) Initialize(); }
        private void OnEnable() { if (Application.isPlaying && library != null && viewCamera != null && Surface == null) Initialize(); }

        public void Initialize()
        {
            if (Surface != null) return;
            if (library == null || viewCamera == null)
                throw new InvalidOperationException("Terrain demo requires a model library and camera.");
            // Older previews used an untracked DontSave root. Such roots can survive scene reloads;
            // remove only this demo's explicitly named transient output before creating a fresh instance.
            foreach (var previous in Resources.FindObjectsOfTypeAll<GameObject>())
                if (previous.name == "Terrain Demo Generated" && previous.transform.parent == null
                    && (previous.hideFlags & HideFlags.DontSave) == HideFlags.DontSave)
                { previous.SetActive(false); DestroyResource(previous); }
            for (int i = 1; i < 255; i++)
                if (!library.GetModel((byte)i).HasValue || library.GetModel((byte)i).Value.mesh == null)
                    throw new InvalidOperationException($"Terrain demo: missing model state {i}.");
            var previousRandom = UnityEngine.Random.state;
            HexGrid grid;
            try
            {
                UnityEngine.Random.InitState(seed);
                grid = new HexGrid(6, 2, Vector3.zero, 1.35f, 4);
                grid.RandomMergeTriangles(); grid.SubdivideGrid(); grid.RelaxGrid(5);
                grid.BuildLayers(); grid.BuildCells();
            }
            finally { UnityEngine.Random.state = previousRandom; }
            Surface = new TerrainSiteData(grid);
            generated = new GameObject("Terrain Demo Generated") { hideFlags = HideFlags.DontSave };
            // Grid positions are world-space; do not inherit controller transform/scale.
            soil = Material("Soil and exposed rock", new Color(0.34f, 0.46f, 0.24f));
            var slopeShader = Shader.Find("PCG/Terrain Slope Blend");
            if (slopeShader == null) throw new InvalidOperationException("Terrain slope shader is missing.");
            soil.shader = slopeShader;
            rock = Material("Exposed limestone", new Color(0.48f, 0.46f, 0.40f));
            wall = Material("Warm plaster", new Color(0.88f, 0.74f, 0.49f));
            roof = Material("Clay roof", new Color(0.47f, 0.18f, 0.105f));
            lines = Material("Grid", new Color(0.14f, 0.22f, 0.12f), true);
            terrainMesh = NewMesh("Procedural polygon soil");
            var terrainObject = MeshObject("Soil surface", terrainMesh, soil);
            terrainCollider = terrainObject.AddComponent<MeshCollider>();
            cliffMesh = NewMesh("Continuous faceted cliff and toe stones");
            cliffObject = MeshObject("Exposed rock faces", cliffMesh, rock);
            cliffObject.GetComponent<MeshRenderer>().sharedMaterials = new[] { rock, soil };
            cliffCollider = cliffObject.AddComponent<MeshCollider>();
            rockMass = new SlopeRockCover();
            wireMesh = NewMesh("Shared polygon edges");
            wireObject = MeshObject("Polygon overlay", wireMesh, lines);
            selectionMesh = NewMesh("Selected terrain polygon");
            selectionObject = MeshObject("Selected polygon", selectionMesh, Material("Selection", new Color(1, 0.80f, 0.17f), true));
            buildingRoot = new GameObject("Building on terrain");
            buildingRoot.transform.SetParent(generated.transform, false);
            pool = buildingRoot.AddComponent<ModelPool>(); pool.ConfigureCapacity(grid.cells.Count);
            Rebuild(); FrameCamera();
        }

        private Material Material(string name, Color color, bool unlit = false)
        {
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No terrain demo shader available.");
            var material = new Material(shader) { name = name, color = color, hideFlags = HideFlags.DontSave };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            owned.Add(material); return material;
        }

        private Mesh NewMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            owned.Add(mesh); return mesh;
        }

        private GameObject MeshObject(string name, Mesh mesh, Material material)
        {
            var obj = new GameObject(name); obj.transform.SetParent(generated.transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material; return obj;
        }

        public void SetGraded(bool value) { graded = value; Rebuild(); }
        public void SetHeightOffset(float value) { heightOffset = Mathf.Clamp(value, -12, 12); Rebuild(); }
        public void SetWire(bool value) { showWire = value; if (wireObject != null) wireObject.SetActive(value); }
        public void SetRocky(bool value) { rocky = value; Rebuild(); FrameCamera(); }
        public void SetRockDetail(bool value) { rockDetail = value; Rebuild(); }
        public void RandomizeRocks() { rockSeedOffset += 101; Rebuild(); }
        public void SetRockCoverage(float value) { rockDensity = Mathf.Clamp01(value); Rebuild(); }
        public void SetRockConformity(float value) { rockConformity = Mathf.Clamp01(value); Rebuild(); }
        public void SetCliffHeight(float value)
        { cliffHeight = Mathf.Clamp(value, 1, 8); Rebuild(); FrameCamera(); }

        public bool MoveSite(Vector2 point)
        {
            if (Surface == null) return false;
            // Keep the complete building and blend band inside this bounded demonstration patch.
            if (point.magnitude > 5.5f)
            { status = "请在中央区域放置，给外围坡面留出过渡空间。"; return false; }
            siteCenter = point; Rebuild(); return true;
        }

        private EditState CaptureEdit() => new EditState { gridSeed = seed, heights = Surface.CopyHeightOffsets(),
            center = siteCenter, offset = heightOffset, rise = cliffHeight, building = graded, cliff = rocky, stones = rockDetail,
            coverage = rockDensity, conformity = rockConformity, rockVariation = rockSeedOffset };
        private void RestoreEdit(EditState state)
        {
            if (state == null || (state.version != 1 && state.version != 2 && state.version != 3) || state.gridSeed != seed || state.center.magnitude > 5.5f
                || float.IsNaN(state.offset) || Mathf.Abs(state.offset) > 12 || float.IsNaN(state.rise) || state.rise < 1 || state.rise > 8)
                throw new InvalidOperationException("保存的数据与当前示例不匹配。 ");
            if (state.version >= 3 && (float.IsNaN(state.coverage) || state.coverage < 0 || state.coverage > 1
                || float.IsNaN(state.conformity) || state.conformity < 0 || state.conformity > 1))
                throw new InvalidOperationException("无效的岩石覆盖参数。");
            Surface.RestoreHeightOffsets(state.heights);
            if (state.version >= 3) { rockDensity = state.coverage; rockConformity = state.conformity; rockSeedOffset = state.rockVariation; }
            siteCenter = state.center; heightOffset = state.offset; cliffHeight = state.rise;
            graded = state.building; rocky = state.cliff; rockDetail = state.stones; Rebuild();
        }
        private void Edit(Action action)
        {
            var before = CaptureEdit();
            try { action(); undo.Push(before); redo.Clear(); }
            catch { RestoreEdit(before); throw; }
        }
        public void AdjustPolygon(Quad quad, float delta)
        {
            if (Surface == null || quad == null || !Surface.QuadSurfaceIndices.ContainsKey(quad)) return;
            if (float.IsNaN(delta) || float.IsInfinity(delta)) throw new ArgumentException("Invalid height change.");
            selectedQuad = quad;
            Edit(() => {
                if (graded && Surface.PlatformQuads.Contains(quad)) heightOffset = Mathf.Clamp(heightOffset + delta, -12, 12);
                else Surface.AdjustQuadHeight(quad, delta);
                Rebuild();
            });
        }
        public bool PlaceBuilding(Vector2 point)
        {
            if (point.magnitude > 5.5f) { status = "请在中央区域放置建筑，给外围留出过渡空间。"; return false; }
            Edit(() => { graded = true; siteCenter = point; Rebuild(); }); return true;
        }
        public void RemoveBuilding() { if (graded) Edit(() => SetGraded(false)); }
        public bool UndoEdit()
        {
            if (undo.Count == 0) return false;
            var current = CaptureEdit(); RestoreEdit(undo.Pop()); redo.Push(current); return true;
        }
        public bool RedoEdit()
        {
            if (redo.Count == 0) return false;
            var current = CaptureEdit(); RestoreEdit(redo.Pop()); undo.Push(current); return true;
        }
        public void SaveEdits()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Application.persistentDataPath);
                System.IO.File.WriteAllText(EditSavePath, ExportEdits());
                status = "已保存地形和建筑位置。";
            }
            catch (Exception ex) { status = "保存失败：" + ex.Message; }
        }
        public string ExportEdits() => JsonUtility.ToJson(CaptureEdit(), true);
        public void ImportEdits(string json) => Edit(() => RestoreEdit(JsonUtility.FromJson<EditState>(json)));
        public void LoadEdits()
        {
            if (!System.IO.File.Exists(EditSavePath)) { status = "还没有保存的示例。"; return; }
            try
            {
                ImportEdits(System.IO.File.ReadAllText(EditSavePath)); status = "已读取保存的地形和建筑位置。";
            }
            catch (Exception ex) { status = "读取失败：" + ex.Message; }
        }
        private void UpdateSelection()
        {
            selectionMesh.Clear();
            if (selectedQuad == null || !Surface.QuadSurfaceIndices.TryGetValue(selectedQuad, out var corners)) return;
            var points = new List<Vector3>(); var indices = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                indices.Add(points.Count); points.Add(Surface.Positions[corners[i]] + Vector3.up * 0.055f);
                indices.Add(points.Count); points.Add(Surface.Positions[corners[(i + 1) % 4]] + Vector3.up * 0.055f);
            }
            selectionMesh.SetVertices(points); selectionMesh.SetIndices(indices, MeshTopology.Lines, 0); selectionMesh.RecalculateBounds();
        }

        private void Rebuild()
        {
            if (Surface == null) return;
            Surface.UpdateSite(siteCenter, heightOffset, graded, 0, seed);
            terrainCollider.sharedMesh = null;
            terrainMesh.Clear(); terrainMesh.SetVertices(Surface.Positions); terrainMesh.SetTriangles(Surface.Triangles, 0);
            var uv = new List<Vector2>();
            foreach (var p in Surface.Positions) uv.Add(new Vector2(p.x, p.z) * 0.2f);
            Slopes = new TerrainSlopeField(Surface); terrainMesh.colors = Slopes.Colors;
            terrainMesh.SetUVs(0, uv); terrainMesh.RecalculateNormals(); terrainMesh.RecalculateBounds();
            terrainCollider.sharedMesh = terrainMesh;
            cliffCollider.sharedMesh = null;
            // Soil and the continuous core stay separate from the locally cached rock chunks.
            cliffMesh.Clear(); // Legacy cut wall output is retired; the terrain is one shared surface.
            var rockSources = cliffRocks != null ? cliffRocks.slopeMeshes : null;
            rockMass.Update(Surface, Slopes, rockSources, generated.transform, rock, seed + rockSeedOffset, rockDensity, rockDetail, rockConformity);
            FaceStoneCount = rockMass.FaceCount; ToeStoneCount = rockMass.ToeCount;
            cliffObject.SetActive(Surface.Cliffs.Count > 0);
            if (cliffMesh.vertexCount > 0) cliffCollider.sharedMesh = cliffMesh;
            var points = new List<Vector3>(); var indices = new List<int>();
            foreach (var polygon in Surface.QuadSurfaceIndices.Values)
                for (int i = 0; i < 4; i++)
                {
                    indices.Add(points.Count); points.Add(Surface.Positions[polygon[i]] + Vector3.up * 0.025f);
                    indices.Add(points.Count); points.Add(Surface.Positions[polygon[(i + 1) % 4]] + Vector3.up * 0.025f);
                }
            wireMesh.Clear(); wireMesh.SetVertices(points); wireMesh.SetIndices(indices, MeshTopology.Lines, 0);
            wireMesh.RecalculateBounds(); SetWire(showWire);
            UpdateSelection();
            if (graded) BuildHouse();
            buildingRoot.SetActive(graded);
            foreach (var node in generated.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 30;
            status = "坡度越陡，岩土材质越明显；岩石模型覆盖陡坡，地形本身保持连续。";
        }

        private void BuildHouse()
        {
            foreach (var model in models) pool.Release(model);
            models.Clear();
            var grid = Surface.Grid;
            foreach (var vertex in grid.allSubVertices)
            {
                int layer = Mathf.RoundToInt(vertex.y / grid.cellHeight);
                float x = Mathf.Abs(vertex.x - siteCenter.x), z = vertex.z - siteCenter.y;
                bool inside = Surface.Inside(vertex.position);
                bool edge = x > 2.1f || Mathf.Abs(z) > 1.3f;
                bool door = x < 1.15f && z < -1.2f && layer <= 2;
                vertex.IsEnabled = inside && (layer == 0 || layer == 3 || (layer < 3 && edge && !door));
            }
            foreach (var cell in grid.cells)
            {
                byte state = cell.GetCellByte(); if (state == 0 || state == 255) continue;
                var model = pool.Get();
                if (model == null) throw new InvalidOperationException("Terrain demo model capacity exhausted.");
                model.Cell = cell; model.Library = library; new ModelBuilder(model).Build();
                model.gameObject.transform.position += Vector3.up * FloorHeight;
                var slots = new Material[model.Mesh.subMeshCount];
                for (int i = 0; i < slots.Length; i++) slots[i] = cell.Center.y > grid.cellHeight * 2.4f ? roof : wall;
                model.MeshRenderer.sharedMaterials = slots;
                var collider = model.gameObject.GetComponent<MeshCollider>();
                if (collider == null) collider = model.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = null; collider.sharedMesh = model.Mesh;
                models.Add(model);
            }
        }

        private void FrameCamera()
        {
            if (viewCamera == null) return;
            var target = new Vector3(1.8f, graded ? Surface.PlatformHeight * 0.5f : 0.4f, 0);
            viewCamera.transform.position = target + Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -distance);
            viewCamera.transform.LookAt(target);
        }

        private void Update()
        {
            if (Surface == null) return;
            Vector2 pointer = Vector2.zero; bool click = false; float scroll = 0;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            { pointer = Mouse.current.position.ReadValue(); click = Mouse.current.leftButton.wasPressedThisFrame; scroll = Mouse.current.scroll.ReadValue().y; }
#elif ENABLE_LEGACY_INPUT_MANAGER
            pointer = Input.mousePosition; click = Input.GetMouseButtonDown(0); scroll = Input.mouseScrollDelta.y * 120;
#endif
            if (Panel.Contains(new Vector2(pointer.x, Screen.height - pointer.y))) return;
            if (click)
            {
                var ray = viewCamera.ScreenPointToRay(pointer);
                bool terrainHit = terrainCollider.Raycast(ray, out var hit, 500);
                float rockDistance = float.PositiveInfinity;
                if (cliffObject.activeSelf && cliffCollider.Raycast(ray, out var cliffHit, 500)) rockDistance = cliffHit.distance;
                // Rock silhouettes protrude from the height surface; do not select through them.
                if (terrainHit && rockDistance >= hit.distance)
                {
                    var point = new Vector2(hit.point.x, hit.point.z);
                    selectedQuad = Surface.FindQuad(point); UpdateSelection();
                    if (tool == EditTool.PlaceBuilding) PlaceBuilding(point);
                    else if (tool == EditTool.Raise) AdjustPolygon(selectedQuad, editStep);
                    else if (tool == EditTool.Lower) AdjustPolygon(selectedQuad, -editStep);
                }
            }
            if (Mathf.Abs(scroll) > 0) { distance = Mathf.Clamp(distance - scroll * 0.018f, 18, 60); FrameCamera(); }
        }

        private void OnGUI()
        {
            if (heading == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 16); owned.Add(font);
                heading = new GUIStyle(GUI.skin.label) { font = font, fontSize = 21, fontStyle = FontStyle.Bold };
                label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 14, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 14, fixedHeight = 34 };
            }
            GUI.Box(Panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(Panel.x + 16, Panel.y + 14, Panel.width - 32, Panel.height - 28));
            GUILayout.Label("多边形地形编辑", heading);
            GUILayout.Label("选择工具，再点击地面多边形", label); GUILayout.Space(8);
            if (Surface == null) { GUILayout.Label("准备示例…", label); GUILayout.EndArea(); return; }
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            tool = (EditTool)GUILayout.SelectionGrid((int)tool, new[] { "选择", "升高", "降低", "放置建筑" }, 2, button);
            GUILayout.Label($"每次调整 {editStep:F2} m", label);
            editStep = Mathf.Round(GUILayout.HorizontalSlider(editStep, 0.25f, 2) * 4) / 4;
            if (selectedQuad != null)
            {
                float total = 0;
                foreach (int i in Surface.QuadSurfaceIndices[selectedQuad]) total += Surface.Positions[i].y;
                GUILayout.Label($"已选多边形 · 平均高度 {total / 4:F2} m", label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("降低选中", button)) AdjustPolygon(selectedQuad, -editStep);
                if (GUILayout.Button("升高选中", button)) AdjustPolygon(selectedQuad, editStep);
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUI.enabled = undo.Count > 0;
            if (GUILayout.Button("撤销", button)) UndoEdit();
            GUI.enabled = redo.Count > 0;
            if (GUILayout.Button("重做", button)) RedoEdit();
            GUI.enabled = true; GUILayout.EndHorizontal();
            GUILayout.Space(8);
            if (GUILayout.Button(showWire ? "隐藏多边形网格" : "显示多边形网格", button)) SetWire(!showWire);
            if (GUILayout.Button("载入陡坡示例（可撤销）", button)) CreateSlopeExample();
            if (graded && GUILayout.Button("移除建筑，继续编辑地形", button)) RemoveBuilding();
            if (!graded) GUILayout.Label("放置建筑后自动找平该区域。", label);
            if (GUILayout.Button(rockDetail ? "隐藏坡面岩石" : "显示坡面岩石", button)) Edit(() => SetRockDetail(!rockDetail));
            GUILayout.Label($"岩石覆盖量 {rockDensity:P0}", label);
            float nextCoverage = Mathf.Round(GUILayout.HorizontalSlider(rockDensity, 0, 1) * 100) / 100;
            GUILayout.Label($"贴坡程度 {rockConformity:P0}", label);
            float nextConformity = Mathf.Round(GUILayout.HorizontalSlider(rockConformity, 0, 1) * 100) / 100;
            if (!Mathf.Approximately(nextCoverage, rockDensity) || !Mathf.Approximately(nextConformity, rockConformity))
            {
                if (rockSliderBefore == null) rockSliderBefore = CaptureEdit();
                rockDensity = nextCoverage; rockConformity = nextConformity; Rebuild();
            }
            if (rockSliderBefore != null && GUIUtility.hotControl == 0)
            { undo.Push(rockSliderBefore); redo.Clear(); rockSliderBefore = null; }
            if (GUILayout.Button("换一组石形组合", button)) Edit(RandomizeRocks);
            GUILayout.Label($"岩壁区域 {RockChunkCount}  ·  主/侧岩 {FaceStoneCount}  ·  碎石 {ToeStoneCount}", label);
            if (graded)
            {
                GUILayout.Label("建筑区域高度", label);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("降低平台", button)) Edit(() => SetHeightOffset(heightOffset - editStep));
                if (GUILayout.Button("升高平台", button)) Edit(() => SetHeightOffset(heightOffset + editStep));
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8); GUILayout.Label("环绕 / 俯仰", label);
            float nextYaw = GUILayout.HorizontalSlider(yaw, -180, 180);
            float nextPitch = GUILayout.HorizontalSlider(pitch, 22, 75);
            if (!Mathf.Approximately(nextYaw, yaw) || !Mathf.Approximately(nextPitch, pitch))
            { yaw = nextYaw; pitch = nextPitch; FrameCamera(); }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存示例", button)) SaveEdits();
            if (GUILayout.Button("读取示例", button)) LoadEdits();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("重置地形高度", button)) Edit(() => {
                Surface.RestoreHeightOffsets(new float[Surface.Indices.Count]); heightOffset = 0; Rebuild(); });
            GUILayout.Space(8); GUILayout.Label(status, label);
            GUILayout.Space(8); GUILayout.Label("滚轮缩放。放置工具可移动一栋示例建筑。\n建筑区域整体升降以保持找平；外围逐格编辑。\n保存后可在下次运行读取。", label);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        private void OnDisable() => ReleaseGenerated();
        private void OnDestroy() => ReleaseGenerated();

        private void ReleaseGenerated()
        {
            if (generated != null) generated.SetActive(false);
            rockMass?.Dispose(); rockMass = null;
            if (pool != null) pool.Clear(); models.Clear();
            DestroyResource(generated);
            foreach (var item in owned) DestroyResource(item);
            owned.Clear(); Surface = null; generated = null; pool = null;
            undo.Clear(); redo.Clear(); selectedQuad = null;
            rockSliderBefore = null;
            heading = null; label = null; button = null;
        }

        private static void DestroyResource(UnityEngine.Object item)
        { if (item == null) return; if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
    }
}



