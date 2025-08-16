using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarchingCube
{
    [CreateAssetMenu(fileName = "ModelLibrary", menuName = "MarchingCube/ModelLibrary")]
    public class ModelLibrary : ScriptableObject
    {
        public List<ModelInfo> meshes = new List<ModelInfo>();
        
        
    }

    [Serializable]
    public struct ModelInfo
    {
        public byte stateCode;
        public Quaternion rotation;
        public Mesh mesh;
    }
}