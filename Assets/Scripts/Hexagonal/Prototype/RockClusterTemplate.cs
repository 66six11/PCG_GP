using System;
using System.Collections.Generic;
using UnityEngine;

namespace HexagonalGrids.Prototype
{
    [CreateAssetMenu(menuName = "PCG/Rock Cluster Template")]
    public sealed class RockClusterTemplate : ScriptableObject
    {
        [Serializable]
        public struct Piece
        {
            public Mesh source;
            public Vector3 position, size;
            public Quaternion rotation;
        }
        public Piece[] pieces = new Piece[0];
        public float capturedHeight;

        public List<ProceduralCliffMesh.Placement> GetPlacements(CliffRockLibrary library)
        {
            var result = new List<ProceduralCliffMesh.Placement>();
            foreach (var piece in pieces)
            {
                int index = Array.IndexOf(library.meshes, piece.source);
                if (index < 0) throw new InvalidOperationException($"Cluster source '{piece.source}' is missing from the rock library.");
                result.Add(new ProceduralCliffMesh.Placement(piece.position, piece.size, piece.rotation, index));
            }
            return result;
        }
    }
}
