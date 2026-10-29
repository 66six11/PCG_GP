using UnityEngine;

namespace HexagonalGrids.Prototype
{
    [CreateAssetMenu(menuName = "PCG/Cliff Rock Library")]
    public sealed class CliffRockLibrary : ScriptableObject
    {
        public Mesh[] titanMeshes = new Mesh[0];
        public Mesh[] slopeMeshes = new Mesh[0];
        public Mesh[] meshes = new Mesh[0];
        public int generatorVersion;
        public RockClusterTemplate clusterTemplate;
    }
}
