using System;
using System.Collections.Generic;

namespace HexagonalGrids.Prototype
{
    /// <summary>Weighted minimum-Shannon-entropy WFC on an arbitrary adjacency graph.
    /// Propagation reaches a fixed point before every observation. Contradictions backtrack;
    /// results are published only after a complete solution, with an explicit search limit.</summary>
    public sealed class WaveFunctionSolver
    {
        public sealed class Result
        {
            public bool Success, BudgetExceeded;
            public int[] Choices;
            public int Observations, Propagations, Backtracks;
        }
        private sealed class Decision
        {
            public int Node;
            public List<int> Alternatives;
            public int TrailStart;
        }
        private readonly struct Removal
        {
            public readonly int Node, Index, Value;
            public Removal(int node, int index, int value) { Node = node; Index = index; Value = value; }
        }
        private uint random;
        public Result Solve(IReadOnlyList<int[]> input, IReadOnlyList<int[]> neighbors,
            Func<int, int, int, int, bool> compatible, Func<int, double> weight, int seed, int maxObservations = 4096,
            Func<int, int, int, int, double> pairWeight = null, Func<int, int, double> unaryWeight = null)
        {
            if (input == null || neighbors == null || compatible == null || weight == null || input.Count != neighbors.Count)
                throw new ArgumentException("WFC domains, graph and rules must agree.");
            if (maxObservations < 1) throw new ArgumentOutOfRangeException(nameof(maxObservations));
            var domains = new List<int>[input.Count];
            for (int i = 0; i < domains.Length; i++)
            {
                if (input[i] == null || neighbors[i] == null) throw new ArgumentException("Null WFC node.");
                domains[i] = new List<int>(input[i]); var seen = new HashSet<int>();
                foreach (int value in domains[i])
                {
                    double valueWeight = unaryWeight == null ? weight(value) : unaryWeight(i,value);
                    if (!seen.Add(value) || !(valueWeight > 0) || double.IsInfinity(valueWeight))
                        throw new ArgumentException("WFC choices need unique IDs and finite positive weights.");
                }
                foreach (int next in neighbors[i])
                    if (next < 0 || next >= domains.Length || next == i) throw new ArgumentException("Invalid WFC adjacency.");
            }
            random = unchecked((uint)seed) ^ 0x9E3779B9u; if (random == 0) random = 1;
            var result = new Result(); var decisions = new Stack<Decision>(); var trail = new List<Removal>();
            double Effective(int node, int value)
            {
                double log = Math.Log(unaryWeight == null ? weight(value) : unaryWeight(node,value));
                if (pairWeight != null) foreach (int next in neighbors[node])
                    if (domains[next].Count == 1) {
                        double factor = pairWeight(node, value, next, domains[next][0]);
                        if (!(factor > 0) || double.IsInfinity(factor)) throw new ArgumentException("Pair weights must be finite and positive for legal candidates.");
                        log += Math.Log(factor);
                    }
                // Protect entropy/weighted selection against products overflowing.
                return Math.Exp(Math.Max(-100, Math.Min(100, log)));
            }
            bool consistent = Propagate(domains, neighbors, compatible, result, trail);
            while (true)
            {
                if (consistent)
                {
                    int node = Select(domains, Effective);
                    if (node < 0)
                    {
                        result.Success = true; result.Choices = new int[domains.Length];
                        for (int i = 0; i < domains.Length; i++) result.Choices[i] = domains[i][0];
                        return result;
                    }
                    decisions.Push(new Decision { Node = node, Alternatives = new List<int>(domains[node]), TrailStart = trail.Count });
                }
                else
                {
                    result.Backtracks++;
                    while (decisions.Count > 0 && decisions.Peek().Alternatives.Count == 0) decisions.Pop();
                    if (decisions.Count == 0) return result;
                }
                if (result.Observations >= maxObservations) { result.BudgetExceeded = true; return result; }
                // Restoring a failed branch can change many domains; a new observation changes only one.
                bool restoredBranch = !consistent;
                var decision = decisions.Peek(); RestoreTrail(domains, trail, decision.TrailStart);
                int choice = Choose(decision.Alternatives, value => Effective(decision.Node, value)); decision.Alternatives.Remove(choice);
                for (int i = domains[decision.Node].Count - 1; i >= 0; i--)
                    if (domains[decision.Node][i] != choice) Remove(domains, trail, decision.Node, i);
                result.Observations++;
                consistent = Propagate(domains, neighbors, compatible, result, trail, restoredBranch ? -1 : decision.Node);
            }
        }
        private double Next()
        { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return random / 4294967296.0; }
        private int Choose(List<int> choices, Func<int, double> weight)
        {
            double total = 0; foreach (int choice in choices) total += weight(choice);
            double target = Next() * total;
            foreach (int choice in choices) { target -= weight(choice); if (target < 0) return choice; }
            return choices[choices.Count - 1];
        }
        private int Select(List<int>[] domains, Func<int, int, double> weight)
        {
            int selected = -1; double minimum = double.PositiveInfinity;
            for (int i = 0; i < domains.Length; i++)
            {
                if (domains[i].Count <= 1) continue;
                double sum = 0, weightedLog = 0;
                foreach (int value in domains[i]) { double w = weight(i, value); sum += w; weightedLog += w * Math.Log(w); }
                double entropy = Math.Log(sum) - weightedLog / sum + Next() * 1e-9;
                if (entropy < minimum) { minimum = entropy; selected = i; }
            }
            return selected;
        }
        private static void Remove(List<int>[] domains, List<Removal> trail, int node, int index)
        {
            trail.Add(new Removal(node, index, domains[node][index])); domains[node].RemoveAt(index);
        }
        private static void RestoreTrail(List<int>[] domains, List<Removal> trail, int start)
        {
            for (int i = trail.Count - 1; i >= start; i--)
            { var entry = trail[i]; domains[entry.Node].Insert(entry.Index, entry.Value); }
            trail.RemoveRange(start, trail.Count - start);
        }
        private static bool Propagate(List<int>[] domains, IReadOnlyList<int[]> neighbors,
            Func<int, int, int, int, bool> compatible, Result result, List<Removal> trail, int changedNode = -1)
        {
            var queue = new Queue<int>(); var queued = new bool[domains.Length];
            if (changedNode >= 0) { queue.Enqueue(changedNode); queued[changedNode] = true; }
            else for (int i = 0; i < domains.Length; i++)
                { if (domains[i].Count == 0) return false; queue.Enqueue(i); queued[i] = true; }
            while (queue.Count > 0)
            {
                int source = queue.Dequeue(); queued[source] = false;
                foreach (int target in neighbors[source])
                {
                    bool changed = false;
                    for (int index = domains[target].Count - 1; index >= 0; index--)
                    {
                        int candidate = domains[target][index]; bool supported = false;
                        foreach (int other in domains[source])
                            if (compatible(source, other, target, candidate)) { supported = true; break; }
                        if (supported) continue;
                        Remove(domains, trail, target, index); result.Propagations++; changed = true;
                    }
                    if (domains[target].Count == 0) return false;
                    if (changed && !queued[target]) { queue.Enqueue(target); queued[target] = true; }
                }
            }
            return true;
        }
    }
}
