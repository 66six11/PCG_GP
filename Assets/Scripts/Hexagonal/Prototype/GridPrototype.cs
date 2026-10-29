using System;
using System.Collections.Generic;
using HexagonalGrids;
using MarchingCube;
using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HexagonalGrids.Prototype
{
    [DefaultExecutionOrder(100)]
    public sealed class GridPrototype : MonoBehaviour
    {
        [SerializeField] private GirdGenerator generator;
        [SerializeField] private ModelLibrary library;
        [SerializeField] private Material surfaceMaterial, gridMaterial, pointMaterial;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private bool showDemoOnStart = true;
        private sealed class View
        {
            public Model Model;
            public ModelBuilder Builder;
            public byte State;
        }
        private readonly Dictionary<Cell, View> views = new Dictionary<Cell, View>();
        private readonly Dictionary<int, Material[]> materialSlots = new Dictionary<int, Material[]>();
        private readonly List<Cell> dirtyCells = new List<Cell>();
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private ModelPool pool;
        private GameObject runtimeRoot, gridObject, pointsObject, cursor;
        private Mesh gridMesh, pointsMesh;
        private MeshRenderer pointsRenderer;
        private GridEditSession session;
        private HexGrid boundGrid;
        private Vertex hovered;
        private int activeLayer = 1;
        private bool addMode = true, showGrid = true, markersDirty;
        private Vector3 center, pivot;
        private float distance, yaw = 35, pitch = 55, baseDistance;
        private Vector2 previousMouse;
        private bool wasPainting, lastPaintValue;
        private string status;
        private GUIStyle titleStyle, textStyle, buttonStyle;
        private Font uiFont;
        public GridEditSession Session => session;
        public int ActiveModelCount => views.Count;
        public int ActiveLayer => activeLayer;
        public int LastUpdatedCells { get; private set; }
        public Vertex HoveredVertex => hovered;
        private Rect Panel => new Rect(16, 16, 280, Mathf.Min(460, Screen.height - 32));

        public void Configure(GirdGenerator gridGenerator, ModelLibrary models, Material surface,
            Material wire, Material points, Camera camera, bool demo = true)
        {
            generator = gridGenerator; library = models; surfaceMaterial = surface;
            gridMaterial = wire; pointMaterial = points; viewCamera = camera; showDemoOnStart = demo;
        }

        private void Start() => Initialize();

        public bool Initialize()
        {
            if (generator == null || library == null)
            { status = "请先配置网格和模型库。"; Debug.LogError(status, this); return false; }
            if (generator._hexGrid == null) generator.Generate();
            for (int i = 1; i < 255; i++)
            {
                var model = library.GetModel((byte)i);
                if (!model.HasValue || model.Value.mesh == null)
                { status = $"模型库缺少状态 {i}，请先重新绑定。"; Debug.LogError(status, this); return false; }
            }
            if (viewCamera == null) viewCamera = Camera.main;
            if (viewCamera == null)
            {
                var cameraObject = new GameObject("Prototype Camera");
                owned.Add(cameraObject); viewCamera = cameraObject.AddComponent<Camera>();
            }
            if (runtimeRoot == null)
            {
                runtimeRoot = new GameObject("PCG Runtime");
                runtimeRoot.hideFlags = HideFlags.DontSave;
                // Keep world-space grid coordinates independent of the controller's transform.
                runtimeRoot.transform.SetParent(transform, true);
                runtimeRoot.transform.position = Vector3.zero;
                runtimeRoot.transform.rotation = Quaternion.identity;
                runtimeRoot.transform.localScale = Vector3.one;
                pool = runtimeRoot.AddComponent<ModelPool>();
                if (surfaceMaterial == null) surfaceMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(0.22f, 0.68f, 0.78f));
                if (gridMaterial == null) gridMaterial = CreateMaterial("Universal Render Pipeline/Unlit", new Color(0.23f, 0.34f, 0.40f));
                if (pointMaterial == null) pointMaterial = CreateMaterial("Universal Render Pipeline/Unlit", new Color(0.98f, 0.69f, 0.24f));
                gridMesh = CreateMesh("Edit plane"); pointsMesh = CreateMesh("Enabled points");
                gridObject = CreateMeshObject("Grid", gridMesh, gridMaterial);
                pointsObject = CreateMeshObject("Points", pointsMesh, pointMaterial);
                pointsRenderer = pointsObject.GetComponent<MeshRenderer>();
                cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cursor.name = "Selected point"; cursor.hideFlags = HideFlags.DontSave;
                cursor.transform.SetParent(runtimeRoot.transform, true);
                var collider = cursor.GetComponent<Collider>(); DestroyOwned(collider);
                cursor.GetComponent<MeshRenderer>().sharedMaterial = pointMaterial;
                cursor.SetActive(false);
            }
            ReleaseViews();
            boundGrid = generator._hexGrid;
            if (boundGrid.layers.Count == 0 || boundGrid.baseLayer.subVertices.Count == 0)
            { status = "当前网格没有可编辑顶点。"; Debug.LogError(status, this); return false; }
            pool.ConfigureCapacity(Mathf.Max(10, boundGrid.cells.Count));
            session = new GridEditSession(boundGrid);
            activeLayer = Mathf.Clamp(activeLayer, 0, boundGrid.layers.Count - 1);
            var bounds = new Bounds(boundGrid.baseLayer.subVertices[0].position, Vector3.zero);
            foreach (var vertex in boundGrid.allSubVertices) bounds.Encapsulate(vertex.position);
            center = bounds.center; pivot = center;
            baseDistance = Mathf.Max(6, bounds.size.magnitude * 0.85f);
            distance = baseDistance;
            viewCamera.orthographic = false;
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(0.035f, 0.055f, 0.075f);
            viewCamera.nearClipPlane = 0.05f;
            viewCamera.farClipPlane = Mathf.Max(200, baseDistance * 10);
            FrameCamera();
            if (showDemoOnStart) LoadDemo();
            session.MarkAllDirty(); RefreshDirtyCells(); RebuildOverlay();
            status = "左键绘制，右键擦除。";
            return true;
        }

        private Material CreateMaterial(string shaderName, Color color)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader) { name = "Prototype Material", hideFlags = HideFlags.DontSave };
            material.color = color; owned.Add(material); return material;
        }
        private Mesh CreateMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            owned.Add(mesh); return mesh;
        }
        private GameObject CreateMeshObject(string name, Mesh mesh, Material material)
        {
            var obj = new GameObject(name); obj.transform.SetParent(runtimeRoot.transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material; return obj;
        }

        public void RefreshDirtyCells()
        {
            if (session == null) return;
            session.DrainDirtyCells(dirtyCells); LastUpdatedCells = dirtyCells.Count;
            foreach (var cell in dirtyCells)
            {
                byte state = cell.GetCellByte();
                if (state == 0 || state == 255)
                {
                    if (views.TryGetValue(cell, out var old)) { pool.Release(old.Model); views.Remove(cell); }
                    continue;
                }
                if (!views.TryGetValue(cell, out var view))
                {
                    var model = pool.Get();
                    if (model == null) throw new InvalidOperationException("Prototype model capacity exhausted.");
                    model.Cell = cell; model.Library = library;
                    view = new View { Model = model, Builder = new ModelBuilder(model) };
                    views.Add(cell, view);
                }
                else if (view.State == state) continue;
                view.Builder.Build(); view.State = state;
                int slots = view.Model.Mesh.subMeshCount;
                if (!materialSlots.TryGetValue(slots, out var materials))
                {
                    materials = new Material[slots];
                    for (int i = 0; i < slots; i++) materials[i] = surfaceMaterial;
                    materialSlots.Add(slots, materials);
                }
                view.Model.MeshRenderer.sharedMaterials = materials;
            }
            if (dirtyCells.Count > 0) markersDirty = true;
        }

        public bool TryPick(Ray ray, int layer, out Vertex vertex)
        {
            vertex = null;
            if (boundGrid == null || layer < 0 || layer >= boundGrid.layers.Count) return false;
            var data = boundGrid.layers[layer];
            if (data.subVertices.Count == 0) return false;
            var plane = new Plane(Vector3.up, data.subVertices[0].position);
            if (!plane.Raycast(ray, out float enter)) return false;
            var point = ray.GetPoint(enter);
            var tree = data.vertexKDTree;
            if (tree == null) data.vertexKDTree = tree = new VertexKdTree(data.subVertices);
            var nearest = tree.QueryNearest(point);
            if (nearest == null || (nearest.position - point).sqrMagnitude > boundGrid.cellSize * boundGrid.cellSize * 0.75f) return false;
            vertex = nearest; return true;
        }

        public void SetLayer(int layer)
        {
            if (session == null) return;
            session.EndStroke(); wasPainting = false;
            activeLayer = Mathf.Clamp(layer, 0, boundGrid.layers.Count - 1);
            hovered = null; RebuildOverlay();
        }
        public void ClearContent() { session?.Replace(vertex => false); status = "已清空，可撤销。"; }
        public void LoadDemo()
        {
            if (session == null) return;
            float extent = boundGrid.radius * boundGrid.cellSize;
            if (boundGrid.IsCoordinateLayout)
            {
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (var vertex in boundGrid.baseLayer.subVertices) { min = Mathf.Min(min, vertex.x); max = Mathf.Max(max, vertex.x); }
                extent = (max - min) * 0.5f;
            }
            float width = Mathf.Min(extent * 0.62f, 3.8f * boundGrid.cellSize);
            width = Mathf.Max(width, boundGrid.cellSize * 1.2f);
            float depth = width * 0.82f;
            session.Replace(vertex =>
            {
                float x = Mathf.Abs(vertex.x - center.x), z = vertex.z - center.z;
                int layer = Mathf.RoundToInt((vertex.y - boundGrid.origin.y) / boundGrid.cellHeight);
                bool inside = x < width && Mathf.Abs(z) < depth;
                if (layer == 0) return inside;
                bool wall = x > width - boundGrid.cellSize || Mathf.Abs(z) > depth - boundGrid.cellSize;
                bool door = x < boundGrid.cellSize * 0.85f && z < -depth + boundGrid.cellSize * 1.1f && layer <= 2;
                return inside && wall && layer < Mathf.Min(3, boundGrid.layers.Count - 1) && !door;
            });
            status = "已载入带入口的示例，可撤销。";
        }

        private void Update()
        {
            if (session == null) return;
            if (!ReferenceEquals(generator._hexGrid, boundGrid)) { Initialize(); return; }
            Vector2 mouse = ReadPointer();
            bool overUi = Panel.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            bool remove = RightHeld(), held = LeftHeld() || remove;
            if (MiddleHeld())
            {
                var delta = mouse - previousMouse;
                if (ShiftHeld()) pivot += (viewCamera.transform.right * -delta.x + viewCamera.transform.up * -delta.y) * distance * 0.0015f;
                else { yaw += delta.x * 0.25f; pitch = Mathf.Clamp(pitch - delta.y * 0.25f, 15, 89); }
            }
            if (!overUi) distance = Mathf.Clamp(distance * Mathf.Exp(-ReadScroll() * 0.001f), 3, baseDistance * 3);
            FrameCamera();
            bool pick = !overUi && !MiddleHeld() && TryPick(viewCamera.ScreenPointToRay(mouse), activeLayer, out hovered);
            if (!pick) hovered = null;
            if (held && pick)
            {
                bool value = remove ? false : addMode;
                if (!wasPainting || lastPaintValue != value) { session.BeginStroke(); lastPaintValue = value; }
                session.SetVertex(hovered, value); wasPainting = true;
            }
            else if (wasPainting) { session.EndStroke(); wasPainting = false; }
            if (cursor != null)
            {
                cursor.SetActive(hovered != null);
                if (hovered != null)
                {
                    cursor.transform.position = hovered.position;
                    cursor.transform.localScale = Vector3.one * boundGrid.cellSize * 0.22f;
                }
            }
            HandleKeys(); previousMouse = mouse;
        }
        private void LateUpdate()
        {
            if (session == null) return;
            if (session.DirtyCount > 0) RefreshDirtyCells();
            if (markersDirty) RebuildOverlay();
        }
        private void FrameCamera()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            viewCamera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }
        private void RebuildOverlay()
        {
            if (session == null) return;
            var layer = boundGrid.layers[activeLayer];
            var linePoints = new List<Vector3>(layer.subEdges.Count * 2);
            foreach (var edge in layer.subEdges)
            { linePoints.Add(edge.a.position + Vector3.up * 0.015f); linePoints.Add(edge.b.position + Vector3.up * 0.015f); }
            SetLines(gridMesh, linePoints);
            var ticks = new List<Vector3>(); float radius = boundGrid.cellSize * 0.08f;
            foreach (var vertex in layer.subVertices)
            {
                if (!vertex.IsEnabled) continue;
                var point = vertex.position + Vector3.up * 0.025f;
                ticks.Add(point - Vector3.right * radius); ticks.Add(point + Vector3.right * radius);
                ticks.Add(point - Vector3.forward * radius); ticks.Add(point + Vector3.forward * radius);
            }
            SetLines(pointsMesh, ticks); gridObject.SetActive(showGrid); pointsRenderer.enabled = showGrid;
            markersDirty = false;
        }
        private static void SetLines(Mesh mesh, List<Vector3> points)
        {
            mesh.Clear(); mesh.SetVertices(points);
            var indices = new int[points.Count]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Lines, 0); mesh.RecalculateBounds();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 16);
            if (uiFont != null) owned.Add(uiFont);
            titleStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 20, fontStyle = FontStyle.Bold };
            textStyle = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 14, wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { font = uiFont, fontSize = 14, fixedHeight = 32 };
        }
        private void OnGUI()
        {
            EnsureStyles(); GUI.Box(Panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(Panel.x + 14, Panel.y + 12, Panel.width - 28, Panel.height - 24));
            GUILayout.Label("PCG 建造原型", titleStyle);
            if (session == null) { GUILayout.Label(status ?? "准备网格…", textStyle); GUILayout.EndArea(); return; }
            GUILayout.Label($"可见模块 {views.Count} / {boundGrid.cells.Count}", textStyle);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(addMode ? "● 添加" : "添加", buttonStyle)) addMode = true;
            if (GUILayout.Button(!addMode ? "● 移除" : "移除", buttonStyle)) addMode = false;
            GUILayout.EndHorizontal();
            GUILayout.Label($"编辑高度 {activeLayer} / {boundGrid.layers.Count - 1}", textStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("降低 [Q]", buttonStyle)) SetLayer(activeLayer - 1);
            if (GUILayout.Button("升高 [E]", buttonStyle)) SetLayer(activeLayer + 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUI.enabled = session.UndoCount > 0; if (GUILayout.Button("撤销", buttonStyle)) session.Undo();
            GUI.enabled = session.RedoCount > 0; if (GUILayout.Button("重做", buttonStyle)) session.Redo();
            GUI.enabled = true; GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("载入示例", buttonStyle)) LoadDemo();
            if (GUILayout.Button("清空", buttonStyle)) ClearContent();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showGrid ? "隐藏网格" : "显示网格", buttonStyle)) { showGrid = !showGrid; RebuildOverlay(); }
            if (GUILayout.Button("视角归位", buttonStyle)) { pivot = center; yaw = 35; pitch = 55; distance = baseDistance; FrameCamera(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("左键绘制 · 右键擦除\n中键旋转 · Shift+中键平移\n滚轮缩放 · Ctrl+Z / Ctrl+Y", textStyle);
            GUILayout.Space(8); GUILayout.Label(status, textStyle);
            GUILayout.Label("编辑内容仅在本次运行中保留。", textStyle);
            GUILayout.EndArea();
        }

        private void HandleKeys()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current; if (keyboard == null) return;
            if (keyboard.qKey.wasPressedThisFrame) SetLayer(activeLayer - 1);
            if (keyboard.eKey.wasPressedThisFrame) SetLayer(activeLayer + 1);
            bool control = keyboard.ctrlKey.isPressed;
            if (control && keyboard.zKey.wasPressedThisFrame) { if (keyboard.shiftKey.isPressed) session.Redo(); else session.Undo(); }
            if (control && keyboard.yKey.wasPressedThisFrame) session.Redo();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Q)) SetLayer(activeLayer - 1);
            if (Input.GetKeyDown(KeyCode.E)) SetLayer(activeLayer + 1);
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Z)) session.Undo();
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Y)) session.Redo();
#endif
        }
        private static Vector2 ReadPointer()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }
        private static bool LeftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }
        private static bool RightHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }
        private static bool MiddleHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.middleButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(2);
#else
            return false;
#endif
        }
        private static bool ShiftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            return false;
#endif
        }
        private static float ReadScroll()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? 0 : Mouse.current.scroll.ReadValue().y;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y * 120;
#else
            return 0;
#endif
        }
        private void OnApplicationFocus(bool focus)
        { if (!focus) { session?.EndStroke(); wasPainting = false; } }
        private void ReleaseViews()
        {
            if (pool != null)
                foreach (var view in views.Values) if (view.Model.gameObject != null) pool.Release(view.Model);
            views.Clear();
        }
        private void OnDestroy()
        {
            ReleaseViews();
            if (pool != null) pool.Clear();
            DestroyOwned(runtimeRoot);
            foreach (var item in owned) DestroyOwned(item);
            owned.Clear();
        }
        private static void DestroyOwned(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
    }
}
