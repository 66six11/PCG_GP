using System;
using System.Collections.Generic;
using HexagonalGrids;

namespace HexagonalGrids.Prototype
{
    /// <summary>Runtime edit transactions. Owns history and dirty cells, never rendering resources.</summary>
    public sealed class GridEditSession
    {
        private struct Change
        {
            public Vertex Vertex;
            public bool Before, After;
        }
        private readonly HexGrid grid;
        private readonly HashSet<Vertex> vertices;
        private readonly HashSet<Cell> dirty = new HashSet<Cell>();
        private readonly Stack<List<Change>> undo = new Stack<List<Change>>();
        private readonly Stack<List<Change>> redo = new Stack<List<Change>>();
        private readonly Dictionary<Vertex, int> strokeIndexes = new Dictionary<Vertex, int>();
        private List<Change> stroke;
        public HexGrid Grid => grid;
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public int DirtyCount => dirty.Count;
        public bool IsEditing => stroke != null;

        public GridEditSession(HexGrid grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            vertices = new HashSet<Vertex>(grid.allSubVertices);
        }

        public void BeginStroke()
        {
            EndStroke();
            stroke = new List<Change>();
            strokeIndexes.Clear();
        }

        public bool SetVertex(Vertex vertex, bool enabled)
        {
            if (vertex == null || !vertices.Contains(vertex)) throw new ArgumentException("Vertex is not part of this grid.", nameof(vertex));
            if (vertex.IsEnabled == enabled) return false;
            bool single = stroke == null;
            if (single) BeginStroke();
            if (!strokeIndexes.TryGetValue(vertex, out int index))
            {
                strokeIndexes.Add(vertex, stroke.Count);
                stroke.Add(new Change { Vertex = vertex, Before = vertex.IsEnabled, After = enabled });
            }
            else
            {
                var change = stroke[index]; change.After = enabled; stroke[index] = change;
            }
            Apply(vertex, enabled);
            if (single) EndStroke();
            return true;
        }

        public void EndStroke()
        {
            if (stroke == null) return;
            stroke.RemoveAll(change => change.Before == change.After);
            if (stroke.Count > 0) { undo.Push(stroke); redo.Clear(); }
            stroke = null;
            strokeIndexes.Clear();
        }

        public void Replace(Func<Vertex, bool> enabled)
        {
            if (enabled == null) throw new ArgumentNullException(nameof(enabled));
            BeginStroke();
            foreach (var vertex in grid.allSubVertices) SetVertex(vertex, enabled(vertex));
            EndStroke();
        }

        public bool Undo()
        {
            EndStroke();
            if (undo.Count == 0) return false;
            var changes = undo.Pop();
            foreach (var change in changes) Apply(change.Vertex, change.Before);
            redo.Push(changes);
            return true;
        }

        public bool Redo()
        {
            EndStroke();
            if (redo.Count == 0) return false;
            var changes = redo.Pop();
            foreach (var change in changes) Apply(change.Vertex, change.After);
            undo.Push(changes);
            return true;
        }

        public void MarkAllDirty() { foreach (var cell in grid.cells) dirty.Add(cell); }
        public void DrainDirtyCells(List<Cell> target)
        {
            target.Clear(); target.AddRange(dirty); dirty.Clear();
        }

        private void Apply(Vertex vertex, bool enabled)
        {
            vertex.IsEnabled = enabled;
            foreach (var cell in grid.Vertex2Cells(vertex)) dirty.Add(cell);
        }
    }
}
