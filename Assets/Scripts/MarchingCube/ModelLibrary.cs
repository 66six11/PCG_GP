using System;
using System.Collections.Generic;
using Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Utility;

namespace MarchingCube
{
    [CreateAssetMenu(fileName = "ModelLibrary", menuName = "MarchingCube/ModelLibrary")]
    public class ModelLibrary : ScriptableObject
    {
        [SerializeField] public List<ModelInfo> originalMesh = new List<ModelInfo>();
        [SerializeField] public List<ModelInfo> mesh = new List<ModelInfo>();
        public IReadOnlyList<ModelInfo> Meshes => mesh.AsReadOnly();

        public ModelInfo? GetModel(byte stateCode)
        {
            foreach (var model in Meshes)
            {
                if (model.stateCode == stateCode)
                {
                    return model;
                }
            }

            return null;
        }

        [ContextMenu("更新状态代码")]
        public void UpdateStateCode()
        {
            bool changed = false;

            for (int i = 0; i < originalMesh.Count; i++)
            {
                var model = originalMesh[i];
                var stateCode = ModelHelper.GetMeshStateCode(model.mesh);

                if (model.stateCode != stateCode)
                {
                    model.SetStateCode(stateCode);
                    originalMesh[i] = model; // 更新列表中的值
                    changed = true;

                    Debug.Log(model.mesh.name + "状态代码：" + ModelHelper.Byte2String(stateCode));
                }
            }

            if (changed)
            {
                Debug.Log("更新状态代码完成");
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.Log("没有需要更新的状态代码");
            }
            originalMesh.Sort((a, b) => a.stateCode.CompareTo(b.stateCode));
        }

        [MenuItem("Tools/Marching Cube/Open Importer")]
        public static void OpenImporter()
        {
            MeshImporterWindow.ShowWindow();
        }

        [ContextMenu("Derive All States")]
        public void DeriveAllStates()
        {
            var dict = new Dictionary<byte, ModelInfo>();

            // 遍历所有模型，计算状态代码
            foreach (var model in originalMesh)
            {
                var stateCode = ModelHelper.GetMeshStateCode(model.mesh);
                dict[stateCode] = model;
            }


            //先旋转90度
            for (int i = 0; i < originalMesh.Count; i++)
            {
                var model = originalMesh[i];
                // 计算对称的状态代码
                var stateCode = ModelHelper.Rotate90(model.stateCode);
                // 如果状态代码存在，则跳过
                if (dict.ContainsKey(stateCode))
                {
                    continue;
                }

                // 否则，创建新的模型，并设置状态代码
                var newModel = new ModelInfo(stateCode, model.mesh);
                newModel.SetRotation(Quaternion.Euler(0, 90, 0));
                dict[stateCode] = newModel;
            }

            //再旋转180度
            for (int i = 0; i < originalMesh.Count; i++)
            {
                var model = originalMesh[i];
                // 计算对称的状态代码
                var stateCode = ModelHelper.Rotate180(model.stateCode);
                // 如果状态代码存在，则跳过
                if (dict.ContainsKey(stateCode))
                {
                    continue;
                }

                // 否则，创建新的模型，并设置状态代码
                var newModel = new ModelInfo(stateCode, model.mesh);
                newModel.SetRotation(Quaternion.Euler(0, 180, 0));
                dict[stateCode] = newModel;
            }

            //再旋转270度
            for (int i = 0; i < originalMesh.Count; i++)
            {
                var model = originalMesh[i];
                // 计算对称的状态代码
                var stateCode = ModelHelper.Rotate270(model.stateCode);
                // 如果状态代码存在，则跳过
                if (dict.ContainsKey(stateCode))
                {
                    continue;
                }

                // 否则，创建新的模型，并设置状态代码
                var newModel = new ModelInfo(stateCode, model.mesh);
                newModel.SetRotation(Quaternion.Euler(0, 270, 0));
                dict[stateCode] = newModel;
            }

            for (int i = 0; i < 256; i++)
            {
                var modelState = (byte)i;
                
                if (modelState is 0 or 255)
                {
                    dict[modelState] = new ModelInfo(modelState, null);
                }
                
                if (dict.ContainsKey(modelState))
                {
                    continue;
                }


                //翻转x轴
                var flipXCode = ModelHelper.FlipX(modelState);
                if (dict.TryGetValue(flipXCode, out var valueX))
                {
                    var newModel = new ModelInfo()
                    {
                        stateCode = modelState,
                        mesh = MeshHelper.FlipXMesh(valueX.mesh),
                        rotation = Quaternion.Inverse(valueX.rotation)
                    };
                    dict[modelState] = newModel;
                }

                var flipZCode = ModelHelper.FlipZ(modelState);
                if (dict.TryGetValue(flipZCode, out var valueZ))
                {
                    var newModel = new ModelInfo()
                    {
                        stateCode = modelState,
                        mesh = MeshHelper.FlipZMesh(valueZ.mesh),
                        rotation = Quaternion.Inverse(valueZ.rotation)
                    };
                    dict[modelState] = newModel;
                }
                else if (!dict.ContainsKey(flipXCode))
                {
                    Debug.LogError(modelState + "没有对应的翻转模型");
                }
            }

            mesh = new List<ModelInfo>(dict.Values);
            mesh.Sort((a, b) => a.stateCode.CompareTo(b.stateCode));
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
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

        public ModelInfo(Mesh mesh)
        {
            this.stateCode = 0;
            this.mesh = mesh;
            this.rotation = Quaternion.identity;
            this.stateCodeString = ModelHelper.Byte2State(stateCode);
        }

        public ModelInfo(byte stateCode, Mesh mesh)
        {
            this.stateCode = stateCode;
            this.mesh = mesh;
            this.rotation = Quaternion.identity;
            this.stateCodeString = ModelHelper.Byte2State(stateCode);
        }

        public ModelInfo(byte stateCode, Mesh mesh, Quaternion rotation)
        {
            this.stateCode = stateCode;
            this.mesh = mesh;
            this.rotation = rotation;
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