using UnityEngine;
using Utility;

namespace HexagonalGrids.Test
{
    public class VisualMesh: MonoBehaviour
    {
        
        public Mesh originalMesh;
        public Mesh finalMesh;
        
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;

        public void Start()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter.mesh = originalMesh;
            FlipXMesh();
            
        }
        [ContextMenu("Flip X Mesh")]
        public void FlipXMesh()
        {
            finalMesh = MeshHelper.FlipXMesh(originalMesh);
            meshFilter.mesh = finalMesh;
        }
    }
}