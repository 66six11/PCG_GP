using System;
using UnityEngine;
using Utility;

namespace MarchingCube
{
    public class ModelSlot : MonoBehaviour
    {
        public byte vertexStates;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;

        public Mesh mesh;

        // 初始化状态
        void Start()
        {
            
        }

        // 从字符串设置状态
        public void SetStatesFromString(string stateString)
        {
            if (stateString.Length != 8)
                throw new ArgumentException("状态字符串必须为8位");

            vertexStates = 0;
            for (int i = 0; i < 8; i++)
            {
                if (stateString[i] == '1')
                {
                    vertexStates |= (byte)(1 << i);
                }
            }
        }

        
        // 检查特定顶点状态
        public bool IsVertexEnabled(int index)
        {
            if (index < 0 || index > 7)
                throw new ArgumentOutOfRangeException(nameof(index) + "必须在0-7之间");

            return (vertexStates & (1 << index)) != 0;
        }

        // 设置特定顶点状态
        public void SetVertexState(int index, bool enabled)
        {
            if (index < 0 || index > 7)
                throw new ArgumentOutOfRangeException(nameof(index) + "必须在0-7之间");

            if (enabled)
            {
                vertexStates |= (byte)(1 << index);
            }
            else
            {
                vertexStates &= (byte)~(1 << index);
            }
        }

        // 从网格名称提取顶点状态
        public void ExtractVertexStateFromMeshName()
        {
            string meshName = meshFilter.sharedMesh.name;

            // 确保名称长度足够
            if (meshName.Length < 8)
            {
                Debug.LogError($"网格名称 '{meshName}' 长度不足8位");
                return;
            }
            
            // 提取状态字符串
            var temp = meshName.Substring(meshName.Length - 8, 8);

            // 转换为字节表示
            vertexStates = ModelHelper.ConvertStateStringToByte(temp);

            Debug.Log($"提取顶点状态: {temp} (字节: {vertexStates})");
        }

        // 将状态字符串转换为字节
      

        private void OnValidate()
        {
            meshFilter.mesh = mesh;
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                ExtractVertexStateFromMeshName();
            }
            else
            {
                Debug.LogWarning("MeshFilter或Mesh未设置");
            }
        }
    }
}