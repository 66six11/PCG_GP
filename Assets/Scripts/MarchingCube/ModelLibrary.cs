using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCube
{
    [CreateAssetMenu(fileName = "ModelLibrary", menuName = "MarchingCube/ModelLibrary")]
    public class ModelLibrary : ScriptableObject
    {
        public List<Mesh> meshes = new List<Mesh>();
        
        
    }
}