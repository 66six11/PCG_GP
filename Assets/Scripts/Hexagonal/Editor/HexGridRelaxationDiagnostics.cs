#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HexagonalGrids;
using Editor.Archive;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Editor
{
    /// <summary>Reproducible generation/relaxation measurements and orthographic topology previews.</summary>
    public static class HexGridRelaxationDiagnostics
    {
        public const string Output = "Logs/HexGridRelaxation";
        public static HexGridLayout Layout(int width, int height)
        {
            var layout = new HexGridLayout { width = width, height = height };
            int holeWidth = Math.Max(1, width / 12), holeHeight = Math.Max(1, height / 8);
            for (int x = width / 2; x < width / 2 + holeWidth; x++)
                for (int y = height / 2; y < height / 2 + holeHeight; y++) layout.excludedTiles.Add(new Vector2Int(x, y));
            for (int x = width - Math.Max(1, width / 6); x < width; x++)
                for (int y = 0; y < Math.Max(1, height / 4); y++) layout.excludedTiles.Add(new Vector2Int(x, y));
            return layout;
        }
        public static double Area(Quad quad)
        {
            double sum = 0;
            for (int i = 0; i < 4; i++)
            { var a = quad.vertices[i].position; var b = quad.vertices[(i + 1) % 4].position; sum += (double)a.x * b.z - (double)a.z * b.x; }
            return sum * 0.5;
        }
        public static int InvalidFaces(HexGrid grid)
        {
            int count = 0;
            foreach (var quad in grid.baseLayer.subQuads)
            {
                bool invalid = false;
                for (int i = 0; i < 4; i++)
                {
                    var a = quad.vertices[i].position; var b = quad.vertices[(i + 1) % 4].position; var c = quad.vertices[(i + 2) % 4].position;
                    if (float.IsNaN(a.x) || float.IsInfinity(a.x) || float.IsNaN(a.z) || float.IsInfinity(a.z)
                        || Vector3.Cross(b - a, c - b).y <= 0.000001f) invalid = true;
                }
                if (invalid) count++;
            }
            return count;
        }
        private static double MeanAngleError(HexGrid grid)
        {
            double sum = 0;
            foreach (var quad in grid.baseLayer.subQuads)
                for (int i = 0; i < 4; i++)
                { var p = quad.vertices[i].position; sum += Math.Abs(Vector3.Angle(quad.vertices[(i + 3) % 4].position - p, quad.vertices[(i + 1) % 4].position - p) - 90); }
            return sum / (4 * grid.baseLayer.subQuads.Count);
        }
        [MenuItem("Tools/Marching Cube/Archive/Relaxation/Measure Original Area and Relaxation %#&F8")]
        public static void Run()
        {
            Directory.CreateDirectory(Output); var saved = UnityEngine.Random.state;
            var report = new StringBuilder("width,height,hexes,quads,iterations,generate_ms,relax_total_ms,invalid_faces,boundary_moved,relative_area_error,mean_angle_error_deg,max_vertex_displacement\n");
            try
            {
                // Warm the managed generation and relaxation path before collecting measurements.
                var warm = Layout(3, 3).CreateGrid(2, Vector3.zero, 1.35f, 0);
                warm.RandomMergeTriangles(); warm.SubdivideGrid(); LegacyQuadRelaxation.Apply(warm, 2);
                foreach (var dimensions in new[] { new Vector2Int(12, 8), new Vector2Int(24, 16), new Vector2Int(48, 32) })
                {
                    UnityEngine.Random.InitState(42); var watch = Stopwatch.StartNew();
                    var grid = Layout(dimensions.x, dimensions.y).CreateGrid(2, Vector3.zero, 1.35f, 0);
                    grid.RandomMergeTriangles(); grid.SubdivideGrid(); watch.Stop(); double generateMs = watch.Elapsed.TotalMilliseconds;
                    var boundary = grid.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
                    var original = grid.baseLayer.subVertices.ToDictionary(v => v, v => v.position);
                    double area = grid.baseLayer.subQuads.Sum(Area), relaxMs = 0; int previous = 0;
                    foreach (int iterations in new[] { 0, 5, 30, 100 })
                    {
                        watch.Restart(); LegacyQuadRelaxation.Apply(grid, iterations - previous); watch.Stop(); relaxMs += watch.Elapsed.TotalMilliseconds; previous = iterations;
                        int invalid = InvalidFaces(grid), moved = boundary.Count(p => p.Key.position != p.Value);
                        double error = Math.Abs(grid.baseLayer.subQuads.Sum(Area) - area) / Math.Abs(area);
                        double displacement = grid.baseLayer.subVertices.Max(v => Vector3.Distance(v.position, original[v]));
                        report.AppendLine(string.Join(",", new[] { dimensions.x.ToString(), dimensions.y.ToString(), grid.HexCoordinates.Count.ToString(),
                            grid.baseLayer.subQuads.Count.ToString(), iterations.ToString(), F(generateMs), F(relaxMs), invalid.ToString(), moved.ToString(), F(error), F(MeanAngleError(grid)), F(displacement) }));
                        File.WriteAllText(Output + "/measurements.csv", report.ToString());
                        if (invalid != 0 || moved != 0 || error > 0.00001) throw new InvalidOperationException($"Invalid relaxed topology: {dimensions}, iterations={iterations}, invalid={invalid}, boundary={moved}, area={error}");
                        Capture(grid, Output + $"/{dimensions.x}x{dimensions.y}-relax-{iterations}.png");
                    }
                }
                Debug.Log("HEX_RELAXATION_OK: 12 scale/iteration cases passed; measurements and previews in " + Output);
            }
            finally { UnityEngine.Random.state = saved; }
        }
        private static string F(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
        [MenuItem("Tools/Marching Cube/Archive/Relaxation/Compare Original Free Boundary %#&F9")]
        public static void CompareFreeBoundary()
        {
            string output = Output + "/free-boundary"; Directory.CreateDirectory(output);
            var saved = UnityEngine.Random.state;
            var report = new StringBuilder("width,height,fixed_boundary,iterations,relax_total_ms,invalid_faces,boundary_moved,boundary_max_displacement,area_change_percent,mean_angle_error_deg\n");
            try
            {
                foreach (var dimensions in new[] { new Vector2Int(12, 8), new Vector2Int(24, 16), new Vector2Int(48, 32) })
                    foreach (bool fixedBoundary in new[] { true, false })
                    {
                        UnityEngine.Random.InitState(42);
                        var grid = Layout(dimensions.x, dimensions.y).CreateGrid(2, Vector3.zero, 1.35f, 0);
                        grid.RandomMergeTriangles(); grid.SubdivideGrid();
                        var boundary = grid.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
                        var bounds = new Bounds(grid.baseLayer.subVertices[0].position, Vector3.zero);
                        foreach (var vertex in grid.baseLayer.subVertices) bounds.Encapsulate(vertex.position);
                        double area = grid.baseLayer.subQuads.Sum(Area), milliseconds = 0; int previous = 0;
                        foreach (int iterations in new[] { 0, 5, 30, 100 })
                        {
                            var timer = Stopwatch.StartNew(); LegacyQuadRelaxation.Apply(grid, iterations - previous, fixedBoundary); timer.Stop();
                            milliseconds += timer.Elapsed.TotalMilliseconds; previous = iterations;
                            int invalid = InvalidFaces(grid), moved = boundary.Count(p => p.Key.position != p.Value);
                            double maximum = boundary.Max(p => Vector3.Distance(p.Key.position, p.Value));
                            double areaChange = (grid.baseLayer.subQuads.Sum(Area) / area - 1) * 100;
                            report.AppendLine(string.Join(",", new[] { dimensions.x.ToString(), dimensions.y.ToString(), fixedBoundary ? "1" : "0",
                                iterations.ToString(), F(milliseconds), invalid.ToString(), moved.ToString(), F(maximum), F(areaChange), F(MeanAngleError(grid)) }));
                            File.WriteAllText(output + "/measurements.csv", report.ToString());
                            Capture(grid, output + $"/{dimensions.x}x{dimensions.y}-{(fixedBoundary ? "fixed" : "free")}-{iterations}.png", bounds);
                        }
                    }
                Debug.Log("HEX_FREE_BOUNDARY_MEASURED: fixed/free comparison complete. See " + output);
            }
            finally { UnityEngine.Random.state = saved; }
        }
        public static (double cv, double minRatio, double maxRatio) AreaStatistics(HexGrid grid)
        {
            var areas = grid.baseLayer.subQuads.Select(q => Math.Abs(Area(q))).ToArray(); double mean = areas.Average();
            double variance = areas.Sum(a => (a - mean) * (a - mean)) / areas.Length;
            return (Math.Sqrt(variance) / mean, areas.Min() / mean, areas.Max() / mean);
        }
        [MenuItem("Tools/Marching Cube/Archive/Relaxation/Compare Original and Balanced %#&F10")]
        public static void CompareBalanced()
        {
            string output = Output + "/balanced"; Directory.CreateDirectory(output); var saved = UnityEngine.Random.state;
            var csv = new StringBuilder("width,height,method,iterations,elapsed_ms,area_cv,min_area_ratio,max_area_ratio,area_change_percent,angle_error,invalid_faces,boundary_moved,accepted_steps,backtracks\n");
            try
            {
                foreach (var size in new[] { new Vector2Int(12, 8), new Vector2Int(24, 16), new Vector2Int(48, 32) })
                    foreach (var method in new[] { "initial", "legacy", "balanced" })
                        foreach (int iterations in method == "initial" ? new[] { 0 } : new[] { 30, 100 })
                        {
                            UnityEngine.Random.InitState(42);
                            var grid = Layout(size.x, size.y).CreateGrid(2, Vector3.zero, 1.35f, 0);
                            grid.RandomMergeTriangles(); grid.SubdivideGrid();
                            var boundary = grid.baseLayer.boundaryVertices.ToDictionary(v => v, v => v.position);
                            var bounds = new Bounds(grid.baseLayer.subVertices[0].position, Vector3.zero);
                            foreach (var vertex in grid.baseLayer.subVertices) bounds.Encapsulate(vertex.position);
                            double area = grid.baseLayer.subQuads.Sum(Area); QuadMeshOptimizer.Result result = null;
                            var timer = Stopwatch.StartNew();
                            if (method == "balanced") result = grid.RelaxBalanced(iterations, false);
                            else if (method == "legacy") LegacyQuadRelaxation.Apply(grid, iterations, false);
                            timer.Stop(); var stats = AreaStatistics(grid);
                            csv.AppendLine(string.Join(",", new[] { size.x.ToString(), size.y.ToString(), method, iterations.ToString(), F(timer.Elapsed.TotalMilliseconds),
                                F(stats.cv), F(stats.minRatio), F(stats.maxRatio), F((grid.baseLayer.subQuads.Sum(Area) / area - 1) * 100),
                                F(MeanAngleError(grid)), InvalidFaces(grid).ToString(), boundary.Count(p => p.Key.position != p.Value).ToString(),
                                (result?.AcceptedSteps ?? 0).ToString(), (result?.Backtracks ?? 0).ToString() }));
                            File.WriteAllText(output + "/measurements.csv", csv.ToString());
                            Capture(grid, output + $"/{size.x}x{size.y}-{method}-{iterations}.png", bounds);
                        }
                Debug.Log("HEX_BALANCED_MEASURED: size/shape optimization comparison complete. See " + output);
            }
            finally { UnityEngine.Random.state = saved; }
        }
        private static void Capture(HexGrid grid, string path, Bounds? framing = null)
        {
            // Render in an isolated preview scene so current-scene objects never enter the comparison.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Topology diagnostic preview"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.25f, 0.69f, 0.61f) };
            Texture2D image = null; RenderTexture target = null; var active = RenderTexture.active;
            try
            {
                var lines = new List<Vector3>();
                foreach (var edge in grid.baseLayer.subEdges) { lines.Add(edge.a.position); lines.Add(edge.b.position); }
                mesh.SetVertices(lines); mesh.SetIndices(Enumerable.Range(0, lines.Count).ToArray(), MeshTopology.Lines, 0); mesh.RecalculateBounds();
                root.AddComponent<MeshFilter>().sharedMesh = mesh; root.AddComponent<MeshRenderer>().sharedMaterial = material;
                var cameraObject = new GameObject("Topology diagnostic camera"); cameraObject.transform.SetParent(root.transform, false);
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.aspect = 1.5f;
                var frame = framing ?? mesh.bounds;
                camera.orthographicSize = Mathf.Max(frame.extents.z, frame.extents.x / camera.aspect) * 1.10f;
                camera.transform.SetPositionAndRotation(frame.center + Vector3.up * 100, Quaternion.Euler(90, 0, 0));
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.055f, 0.085f, 0.11f);
                camera.scene = scene; camera.farClipPlane = 200;
                target = RenderTexture.GetTemporary(1800, 1200, 24); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; image = new Texture2D(1800, 1200, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1800, 1200), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = active; Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); Object.DestroyImmediate(material);
                if (image != null) Object.DestroyImmediate(image); if (target != null) RenderTexture.ReleaseTemporary(target);
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static readonly Queue<(Vector2Int size, int iterations, bool balanced, int sample)> performanceCases
            = new Queue<(Vector2Int, int, bool, int)>();
        private static StringBuilder performanceCsv;
        private static bool performanceRunning;
        [MenuItem("Tools/Marching Cube/Archive/Relaxation/Benchmark Original and Balanced %#&F11")]
        public static void BenchmarkPerformance()
        {
            if (performanceRunning) return;
            Directory.CreateDirectory(Output + "/performance");
            performanceCsv = new StringBuilder("width,height,quads,method,iterations,sample,elapsed_ms,allocated_bytes,area_cv,invalid_faces\n");
            foreach (var size in new[] { new Vector2Int(12, 8), new Vector2Int(24, 16), new Vector2Int(48, 32) })
                foreach (int iterations in new[] { 30, 100 })
                    // Alternate method order to reduce systematic drift from machine load.
                    for (int sample = 0; sample <= 5; sample++)
                        foreach (bool balanced in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
                            performanceCases.Enqueue((size, iterations, balanced, sample));
            performanceRunning = true; EditorApplication.delayCall += RunPerformanceCase;
        }
        [MenuItem("Tools/Marching Cube/Archive/Relaxation/Benchmark Original and Balanced %#&F11", true)]
        public static bool CanBenchmarkPerformance() => !performanceRunning && !EditorApplication.isPlayingOrWillChangePlaymode;
        private static void RunPerformanceCase()
        {
            var saved = UnityEngine.Random.state;
            try
            {
                var item = performanceCases.Dequeue(); UnityEngine.Random.InitState(42);
                var grid = Layout(item.size.x, item.size.y).CreateGrid(2, Vector3.zero, 1.35f, 0);
                grid.RandomMergeTriangles(); grid.SubdivideGrid();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread(); var timer = Stopwatch.StartNew();
                if (item.balanced) grid.RelaxBalanced(item.iterations, false); else LegacyQuadRelaxation.Apply(grid, item.iterations, false);
                timer.Stop(); long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                // Some Unity Mono runtimes expose this API but always return zero. Do not report that as zero allocation.
                long allocated = allocatedBefore == 0 && allocatedAfter == 0 ? -1 : allocatedAfter - allocatedBefore;
                if (item.sample > 0) // One same-size warm-up for each method/iteration pair is excluded.
                {
                    performanceCsv.AppendLine(string.Join(",", new[] { item.size.x.ToString(), item.size.y.ToString(), grid.baseLayer.subQuads.Count.ToString(),
                        item.balanced ? "balanced" : "legacy", item.iterations.ToString(), item.sample.ToString(), F(timer.Elapsed.TotalMilliseconds),
                        allocated.ToString(), F(AreaStatistics(grid).cv), InvalidFaces(grid).ToString() }));
                    File.WriteAllText(Output + "/performance/samples.csv", performanceCsv.ToString());
                }
                if (performanceCases.Count > 0) EditorApplication.delayCall += RunPerformanceCase;
                else { performanceRunning = false; Debug.Log("HEX_PERFORMANCE_BENCHMARK_OK: 60 samples after per-case warm-up; " + Output + "/performance/samples.csv"); }
            }
            catch (Exception error)
            {
                performanceRunning = false; performanceCases.Clear();
                File.WriteAllText(Output + "/performance/error.txt", error.ToString()); Debug.LogException(error);
            }
            finally { UnityEngine.Random.state = saved; }
        }
    }
}
#endif
