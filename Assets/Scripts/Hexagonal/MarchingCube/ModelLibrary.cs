using System;
using System.Collections.Generic;
using Job;
using Utility;
#if UNITY_EDITOR
using Editor;
using UnityEditor;
#endif
using UnityEngine;

namespace MarchingCube
{
    [CreateAssetMenu(fileName = "ModelLibrary", menuName = "MarchingCube/ModelLibrary")]
    public class ModelLibrary : ScriptableObject
    {
        public List<ModelInfo> originalMesh = new List<ModelInfo>();
        public List<ModelInfo> mesh = new List<ModelInfo>();
        public IReadOnlyList<ModelInfo> Meshes => mesh;
        private ModelInfo?[] stateLookup;
        private List<ModelInfo> indexedModels;
        private int indexedCount = -1;
        private object sourceRevision = new object();
        private readonly Dictionary<(Mesh mesh, Quaternion rotation, bool mirrorX), MeshDeformationTemplate> templates
            = new Dictionary<(Mesh, Quaternion, bool), MeshDeformationTemplate>();

        public ModelInfo? GetModel(byte stateCode)
        {
            if (stateLookup == null || !ReferenceEquals(indexedModels, mesh) || indexedCount != mesh.Count)
            {
                InvalidateRuntimeCache();
                stateLookup = new ModelInfo?[256];
                foreach (var model in mesh)
                    if (!stateLookup[model.stateCode].HasValue) stateLookup[model.stateCode] = model;
                indexedModels = mesh;
                indexedCount = mesh.Count;
            }
            return stateLookup[stateCode];
        }

        internal MeshDeformationTemplate GetTemplate(ModelInfo model)
        {
            var key = (model.mesh, model.rotation, model.mirrorX);
            if (!templates.TryGetValue(key, out var template))
            {
                template = new MeshDeformationTemplate(model.mesh, model.rotation, sourceRevision, model.mirrorX);
                templates.Add(key, template);
            }
            return template;
        }

        /// <summary>Call after editing a source mesh or replacing entries in the public authoring lists.</summary>
        public void InvalidateRuntimeCache()
        {
            foreach (var template in templates.Values) template.Dispose();
            templates.Clear();
            sourceRevision = new object();
            stateLookup = null;
            indexedModels = null;
            indexedCount = -1;
        }

        private void OnValidate() => InvalidateRuntimeCache();
        private void OnDisable() => InvalidateRuntimeCache();

#if UNITY_EDITOR
        [ContextMenu("更新状态代码")]
        public void UpdateStateCode() => DeriveAllStates();

        [MenuItem("Tools/Marching Cube/Open Importer")]
        public static void OpenImporter() => MeshImporterWindow.ShowWindow();
#endif

        [ContextMenu("Derive All States")]
        public void DeriveAllStates() => ReplaceSourceModels(originalMesh);

        public void ReplaceSourceModels(IReadOnlyList<ModelInfo> sources)
        {
            var result = ModelStateTable.Build(sources);
#if UNITY_EDITOR
            Undo.RecordObject(this, "Rebuild marching cube states");
#endif
            InvalidateRuntimeCache();
            originalMesh = result.Originals;
            mesh = result.States;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (AssetDatabase.Contains(this)) AssetDatabase.SaveAssets();
#endif
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            InvalidateRuntimeCache();
            originalMesh.Clear();
            mesh.Clear();
        }
    }

    [Serializable]
    public struct ModelInfo
    {
        public byte stateCode;
        public string stateCodeString;
        public Mesh mesh;

        public Quaternion rotation;
        public bool mirrorX;

        public ModelInfo(Mesh mesh)
        {
            this.stateCode = 0;
            this.mesh = mesh;
            this.rotation = Quaternion.identity;
            this.mirrorX = false;
            this.stateCodeString = ModelHelper.Byte2State(stateCode);
        }

        public ModelInfo(byte stateCode, Mesh mesh)
        {
            this.stateCode = stateCode;
            this.mesh = mesh;
            this.rotation = Quaternion.identity;
            this.mirrorX = false;
            this.stateCodeString = ModelHelper.Byte2State(stateCode);
        }

        public ModelInfo(byte stateCode, Mesh mesh, Quaternion rotation, bool mirrorX = false)
        {
            this.stateCode = stateCode;
            this.mesh = mesh;
            this.rotation = rotation;
            this.mirrorX = mirrorX;
            this.stateCodeString = ModelHelper.Byte2State(stateCode);
        }

        public void SetStateCode(byte stateCode)
        {
            this.stateCode = stateCode;
            this.stateCodeString = ModelHelper.Byte2State(stateCode);
        }

        public void SetMesh(Mesh mesh)
        {
            this.mesh = mesh;
        }

        public void SetRotation(Quaternion rotation)
        {
            this.rotation = rotation;
        }
    }
}