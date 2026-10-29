using System;
using System.Collections.Generic;
using Utility;

namespace MarchingCube
{
    public sealed class ModelStateTable
    {
        public List<ModelInfo> Originals { get; }
        public List<ModelInfo> States { get; }
        private ModelStateTable(List<ModelInfo> originals, List<ModelInfo> states)
        { Originals = originals; States = states; }

        /// <summary>Build completely before committing. Direct models win over rotations, then mirrors.</summary>
        public static ModelStateTable Build(IReadOnlyList<ModelInfo> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var originals = new List<ModelInfo>(sources.Count);
            var seen = new Dictionary<byte, string>();
            foreach (var source in sources)
            {
                byte code = ModelHelper.GetMeshStateCode(source.mesh);
                if (seen.TryGetValue(code, out var previous))
                    throw new ArgumentException($"Duplicate state {code}: '{previous}' and '{source.mesh.name}'.");
                seen.Add(code, source.mesh.name);
                // Source names describe canonical geometry, not previously derived transforms.
                originals.Add(new ModelInfo(code, source.mesh));
            }
            originals.Sort((a, b) => a.stateCode.CompareTo(b.stateCode));
            var table = new ModelInfo?[256];
            table[0] = new ModelInfo(0, null);
            table[255] = new ModelInfo(255, null);
            foreach (var source in originals)
                if (source.stateCode != 0 && source.stateCode != 255) table[source.stateCode] = source;
            for (int mirror = 0; mirror < 2; mirror++)
                for (int turns = 0; turns < 4; turns++)
                {
                    var transform = new CubeTransform(turns, mirror != 0);
                    foreach (var source in originals)
                    {
                        byte target = transform.TransformState(source.stateCode);
                        if (table[target].HasValue) continue;
                        table[target] = new ModelInfo(target, source.mesh, transform.Rotation, transform.MirrorX);
                    }
                }
            var missing = new List<byte>();
            for (int i = 0; i < 256; i++) if (!table[i].HasValue) missing.Add((byte)i);
            if (missing.Count > 0)
                throw new InvalidOperationException($"Source models do not cover {missing.Count} states: {string.Join(", ", missing)}");
            var states = new List<ModelInfo>(256);
            for (int i = 0; i < 256; i++) states.Add(table[i].Value);
            return new ModelStateTable(originals, states);
        }
    }
}
