using System;
using System.Collections.Generic;
using MarchingCube;

namespace HexagonalGrids.Prototype
{
    /// <summary>WFC module selection over occupied Cell components. Player occupancy is immutable here.</summary>
    public sealed class BuildingWaveFunction
    {
        private sealed class Node
        {
            public Cell Cell;
            public byte State;
            public int Choice = -1;
            public readonly int[] Neighbor = { -1, -1, -1, -1, -1, -1 };
            public readonly int[] OtherFace = new int[6], Orientation = new int[6];
            public bool Active => State != 0 && State != 255;
        }
        private readonly Node[] nodes;
        private readonly Dictionary<Cell, int> indices = new Dictionary<Cell, int>();
        private readonly List<Cell> changes = new List<Cell>();
        private int lastSeed;
        private bool initialized;
        private ulong occupancyHash;
        private readonly Dictionary<int, int> previousChoices = new Dictionary<int, int>();
        private readonly List<Transition> history = new List<Transition>();
        private sealed class Transition
        {
            public ulong From, To;
            public int[] Indices, Before, After;
        }
        public BuildingWfcCatalog Catalog { get; }
        public int LastSolvedCells { get; private set; }
        public int LastComponents { get; private set; }
        public int LastObservations { get; private set; }
        public int LastPropagations { get; private set; }
        public int LastBacktracks { get; private set; }
        public int LastFallbacks { get; private set; }
        public string LastError { get; private set; }
        public int LastLocalAttempts { get; private set; }
        public bool LastUsedFullSolve { get; private set; }
        public int Choice(Cell cell) => nodes[indices[cell]].Choice;
        public ModelInfo Selected(Cell cell)
        {
            int choice = Choice(cell);
            if (choice < 0) throw new InvalidOperationException("This Cell has no collapsed building module.");
            return Catalog.Variants[choice].Model;
        }
        public BuildingWaveFunction(HexGrid grid, ModelLibrary library, BuildingTileset tileset = null)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            Catalog = new BuildingWfcCatalog(library, tileset); nodes = new Node[grid.cells.Count];
            var vertices = new Dictionary<Vertex, int>();
            for (int i = 0; i < grid.allSubVertices.Count; i++) vertices.Add(grid.allSubVertices[i], i);
            var faces = new Dictionary<FaceKey, (int node, int face)>();
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = new Node { Cell = grid.cells[i] }; indices.Add(grid.cells[i], i);
                for (int face = 0; face < 6; face++)
                {
                    var cell = grid.cells[i]; int start = 0;
                    for (int corner = 1; corner < 4; corner++)
                        if (vertices[cell.vertices[BuildingWfcCatalog.FaceCorners[face, corner]]]
                            < vertices[cell.vertices[BuildingWfcCatalog.FaceCorners[face, start]]]) start = corner;
                    bool reverse = vertices[cell.vertices[BuildingWfcCatalog.FaceCorners[face, (start + 1) % 4]]]
                        > vertices[cell.vertices[BuildingWfcCatalog.FaceCorners[face, (start + 3) % 4]]];
                    nodes[i].Orientation[face] = start * 2 + (reverse ? 1 : 0);
                    var key = cell.GetFaceKey(face);
                    if (faces.TryGetValue(key, out var other))
                    {
                        nodes[i].Neighbor[face] = other.node; nodes[i].OtherFace[face] = other.face;
                        nodes[other.node].Neighbor[other.face] = i; nodes[other.node].OtherFace[other.face] = face;
                    }
                    else faces.Add(key, (i, face));
                }
            }
        }
        public IReadOnlyList<Cell> Update(IReadOnlyList<Cell> dirty, int seed)
        {
            changes.Clear();
            if (initialized && dirty.Count == 0 && seed == lastSeed) return changes;
            LastSolvedCells = LastComponents = LastObservations = LastPropagations = LastBacktracks = LastFallbacks = 0;
            LastError = null; LastLocalAttempts = 0; LastUsedFullSolve = false; previousChoices.Clear();
            ulong before = StateKey(lastSeed);
            bool full = !initialized || seed != lastSeed;
            var roots = new HashSet<int>();
            if (full)
                for (int i = 0; i < nodes.Length; i++) { ReadState(i); roots.Add(i); }
            foreach (var cell in dirty)
            {
                int index = indices[cell]; ReadState(index); roots.Add(index);
                foreach (int next in nodes[index].Neighbor) if (next >= 0) roots.Add(next);
            }
            ulong after = StateKey(seed);
            // Sparse reversible patches preserve the exact module choices across undo/redo,
            // without retaining a full-map choice snapshot for every stroke.
            if (initialized && RestoreChoices(before, after))
            {
                LastSolvedCells = changes.Count; initialized = true; lastSeed = seed; return changes;
            }
            // Removed cells can split a component. Its surviving neighbors seed every resulting piece.
            foreach (int root in roots)
                if (!nodes[root].Active && nodes[root].Choice >= 0) SetChoice(root, -1);
            if (!full)
            {
                var region = new HashSet<int>();
                foreach (int root in roots) if (nodes[root].Active) region.Add(root);
                // Pin all choices outside this region. Expand only after a contradiction.
                // Keep ordinary edit searches small; the connected-component path remains a correctness fallback.
                for (int attempt = 0; attempt < 4 && region.Count <= 256; attempt++)
                {
                    var local = new List<int>(region); local.Sort(); LastLocalAttempts++;
                    if (local.Count == 0 || Solve(local, unchecked(seed * 397 ^ local[0]), region, false))
                    { Remember(before, after); initialized = true; lastSeed = seed; return changes; }
                    var expanded = new HashSet<int>(region);
                    foreach (int index in region)
                        foreach (int next in nodes[index].Neighbor) if (next >= 0 && nodes[next].Active) expanded.Add(next);
                    if (expanded.Count == region.Count) break;
                    region = expanded;
                }
            }
            LastUsedFullSolve = true;
            var visited = new bool[nodes.Length];
            for (int root = 0; root < nodes.Length; root++)
            {
                if (!roots.Contains(root) || !nodes[root].Active || visited[root]) continue;
                var component = new List<int>(); var queue = new Queue<int>(); queue.Enqueue(root); visited[root] = true;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue(); component.Add(current);
                    foreach (int next in nodes[current].Neighbor)
                        if (next >= 0 && nodes[next].Active && !visited[next]) { visited[next] = true; queue.Enqueue(next); }
                }
                component.Sort(); Solve(component, unchecked(seed * 397 ^ component[0]), null, true);
            }
            Remember(before, after); initialized = true; lastSeed = seed; return changes;
        }
        private static ulong Mix(ulong value)
        {
            unchecked { value ^= value >> 30; value *= 0xbf58476d1ce4e5b9UL; value ^= value >> 27;
                value *= 0x94d049bb133111ebUL; return value ^ (value >> 31); }
        }
        private ulong StateKey(int seed) => occupancyHash ^ Mix(unchecked((uint)seed) + 0x9e3779b97f4a7c15UL);
        private void ReadState(int index)
        {
            var node = nodes[index]; byte state = node.Cell.GetCellByte();
            if (state == node.State) return;
            if (node.State != 0) occupancyHash ^= Mix(((ulong)index << 8) | node.State);
            if (state != 0) occupancyHash ^= Mix(((ulong)index << 8) | state);
            node.State = state;
        }
        private void SetChoice(int index, int choice)
        {
            var node = nodes[index]; if (node.Choice == choice) return;
            if (!previousChoices.ContainsKey(index)) { previousChoices.Add(index, node.Choice); changes.Add(node.Cell); }
            node.Choice = choice;
        }
        private bool RestoreChoices(ulong from, ulong to)
        {
            if (from == to) return false;
            var queue = new Queue<ulong>(); var paths = new Dictionary<ulong, (ulong previous, Transition entry, bool reverse)>();
            queue.Enqueue(from); paths.Add(from, default);
            while (queue.Count > 0 && !paths.ContainsKey(to))
            {
                ulong current = queue.Dequeue();
                for (int h = history.Count - 1; h >= 0; h--)
                {
                    var entry = history[h]; bool reverse = entry.To == current;
                    if (!reverse && entry.From != current) continue;
                    ulong next = reverse ? entry.From : entry.To;
                    if (paths.ContainsKey(next)) continue;
                    paths.Add(next, (current, entry, reverse)); queue.Enqueue(next);
                }
            }
            if (!paths.ContainsKey(to)) return false;
            var route = new Stack<(Transition entry, bool reverse)>(); ulong key = to;
            while (key != from) { var step = paths[key]; route.Push((step.entry, step.reverse)); key = step.previous; }
            while (route.Count > 0)
            {
                var step = route.Pop(); var entry = step.entry;
                for (int i = 0; i < entry.Indices.Length; i++) SetChoice(entry.Indices[i], step.reverse ? entry.Before[i] : entry.After[i]);
            }
            return true;
        }
        private void Remember(ulong from, ulong to)
        {
            if (from == to || previousChoices.Count == 0) return;
            var entry = new Transition { From = from, To = to, Indices = new int[previousChoices.Count],
                Before = new int[previousChoices.Count], After = new int[previousChoices.Count] };
            int i = 0;
            foreach (var pair in previousChoices)
            { entry.Indices[i] = pair.Key; entry.Before[i] = pair.Value; entry.After[i++] = nodes[pair.Key].Choice; }
            if (history.Count == 64) history.RemoveAt(0); history.Add(entry);
        }
        private bool BoundaryCompatible(int node, int candidate, HashSet<int> region = null)
        {
            for (int face = 0; face < 6; face++)
            {
                int next = nodes[node].Neighbor[face];
                if (next < 0) { if (!Catalog.BoundaryAllowed(candidate, face, "$outside")) return false; }
                else if (!nodes[next].Active && (Catalog.Socket(candidate, face, nodes[node].Orientation[face]) != 0
                    || !Catalog.BoundaryAllowed(candidate, face, nodes[next].State == 255 ? "$solid" : "$empty"))) return false;
                else if (nodes[next].Active && region != null && !region.Contains(next)
                    && (nodes[next].Choice < 0 || !Compatible(node, candidate, next, nodes[next].Choice))) return false;
            }
            return true;
        }
        private bool Compatible(int left, int a, int right, int b)
        {
            for (int face = 0; face < 6; face++)
                if (nodes[left].Neighbor[face] == right)
                {
                    int otherFace = nodes[left].OtherFace[face];
                    return Catalog.SemanticCompatible(a, face, b, otherFace)
                        && Catalog.Socket(a, face, nodes[left].Orientation[face]) == Catalog.Socket(b, otherFace, nodes[right].Orientation[otherFace]);
                }
            throw new ArgumentException("WFC rule requested for non-adjacent Cells.");
        }
        private bool Solve(List<int> component, int seed, HashSet<int> region, bool allowFallback)
        {
            var local = new Dictionary<int, int>(); var domains = new int[component.Count][]; var graph = new int[component.Count][];
            for (int i = 0; i < component.Count; i++) local.Add(component[i], i);
            for (int i = 0; i < component.Count; i++)
            {
                int index = component[i]; var candidates = new List<int>(); var neighbors = new List<int>();
                foreach (int candidate in Catalog.Candidates(nodes[index].State))
                    if (BoundaryCompatible(index, candidate, region)) candidates.Add(candidate);
                foreach (int next in nodes[index].Neighbor) if (next >= 0 && local.TryGetValue(next, out int neighbor)) neighbors.Add(neighbor);
                domains[i] = candidates.ToArray(); graph[i] = neighbors.ToArray();
            }
            var result = new WaveFunctionSolver().Solve(domains, graph,
                (a, av, b, bv) => Compatible(component[a], av, component[b], bv), candidate => Catalog.Variants[candidate].Weight, seed,
                pairWeight: (a, av, b, bv) => PairWeight(component[a], av, component[b], bv),
                unaryWeight: (a, av) => BoundaryWeight(component[a], av, region));
            LastSolvedCells += component.Count; LastComponents++; LastObservations += result.Observations;
            LastPropagations += result.Propagations; LastBacktracks += result.Backtracks;
            int[] choices = result.Choices;
            if (!result.Success)
            {
                if (!allowFallback) return false;
                LastFallbacks++; LastError = result.BudgetExceeded ? "WFC 搜索达到上限，使用已验证的基础模块。" : "WFC 候选冲突，使用已验证的基础模块。";
                choices = new int[component.Count];
                for (int i = 0; i < component.Count; i++) choices[i] = Catalog.Canonical(nodes[component[i]].State);
                for (int i = 0; i < component.Count; i++)
                {
                    if (!BoundaryCompatible(component[i], choices[i])) throw new InvalidOperationException("Base building library has incompatible boundary contours.");
                    foreach (int next in graph[i])
                        if (!Compatible(component[i], choices[i], component[next], choices[next]))
                            throw new InvalidOperationException("Base building library has incompatible shared-face contours.");
                }
            }
            // Commit the entire solved component, never a partially contradictory wave.
            for (int i = 0; i < component.Count; i++)
            {
                SetChoice(component[i], choices[i]);
            }
            return true;
        }
        private double PairWeight(int left, int a, int right, int b)
        {
            for (int face = 0; face < 6; face++) if (nodes[left].Neighbor[face] == right) return Catalog.PairWeight(a, face, b, nodes[left].OtherFace[face]);
            throw new ArgumentException("Non-adjacent Cells.");
        }
        private double BoundaryWeight(int node, int candidate, HashSet<int> region = null)
        {
            double result = Catalog.Variants[candidate].Weight;
            for (int face = 0; face < 6; face++) {
                int next = nodes[node].Neighbor[face];
                if (next < 0) result *= Catalog.RuleWeight(candidate,face,"$outside");
                else if (!nodes[next].Active) result *= Catalog.RuleWeight(candidate,face,nodes[next].State==255?"$solid":"$empty");
                else if (region != null && !region.Contains(next)) result *= PairWeight(node, candidate, next, nodes[next].Choice);
            }
            return result;
        }
        public bool Validate(out string error)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node.State != node.Cell.GetCellByte()) { error = $"Stale WFC occupancy at Cell {i}."; return false; }
                if (!node.Active) continue;
                if (node.Choice < 0 || Catalog.Variants[node.Choice].Model.stateCode != node.State || !BoundaryCompatible(i, node.Choice))
                { error = $"Unresolved or invalid WFC Cell {i}."; return false; }
                for (int face = 0; face < 6; face++)
                {
                    int next = node.Neighbor[face];
                    if (next >= 0 && nodes[next].Active && (nodes[next].Choice < 0 || !Compatible(i, node.Choice, next, nodes[next].Choice)))
                    { error = $"Incompatible WFC face between Cells {i} and {next}."; return false; }
                }
            }
            error = null; return true;
        }
    }
}
