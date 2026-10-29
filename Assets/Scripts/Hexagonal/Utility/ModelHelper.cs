using System;
using System.Linq;
using UnityEngine;

namespace Utility
{
    public static class ModelHelper
    {
        public static byte GetMeshStateCode(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (!TryGetStateCode(mesh.name, out byte state))
                throw new ArgumentException($"Mesh '{mesh.name}' must end with exactly eight binary state digits.", nameof(mesh));
            return state;
        }

        public static bool TryGetStateCode(string meshName, out byte state)
        {
            state = 0;
            if (string.IsNullOrEmpty(meshName) || meshName.Length < 8) return false;
            int start = meshName.Length - 8;
            // Reject a longer binary suffix rather than silently truncating it.
            if (start > 0 && (meshName[start - 1] == '0' || meshName[start - 1] == '1')) return false;
            for (int i = 0; i < 8; i++)
            {
                char digit = meshName[start + i];
                if (digit != '0' && digit != '1') { state = 0; return false; }
                if (digit == '1') state |= (byte)(1 << i);
            }
            return true;
        }

        /// <summary>
        /// 将状态字符串转换为字节
        /// </summary>
        /// <remarks>
        /// 状态字符串的格式为8位,字节二进制位从右往左排列，每一位表示一个状态，0表示关闭，1表示开启
        /// 例如转换为字节的状态字符串为"10101010"，则返回的字节值为0b01010101
        /// </remarks>
        /// <param name="stateString"> 状态字符串(8位) </param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"> 状态字符串必须为8位 </exception>
        /// <exception cref="ArgumentException"> 状态字符串中只允许0和1 </exception>
        public static byte ConvertStateStringToByte(string stateString)
        {
            if (stateString == null) throw new ArgumentNullException(nameof(stateString));
            if (stateString.Length != 8)
                throw new ArgumentException("状态字符串必须为8位");

            byte result = 0;
            for (int i = 0; i < 8; i++)
            {
                char c = stateString[i];
                if (c != '0' && c != '1')
                    throw new ArgumentException($"无效字符 '{c}'，只允许0和1");

                if (c == '1')
                {
                    result |= (byte)(1 << i);
                }
            }

            return result;
        }

        public static byte Rotate90(byte state) => new MarchingCube.CubeTransform(1).TransformState(state);
        public static byte Rotate180(byte state) => new MarchingCube.CubeTransform(2).TransformState(state);
        public static byte Rotate270(byte state) => new MarchingCube.CubeTransform(3).TransformState(state);
        public static byte FlipX(byte state) => new MarchingCube.CubeTransform(0, true).TransformState(state);
        public static byte FlipZ(byte state) => new MarchingCube.CubeTransform(2, true).TransformState(state);

        public static string Byte2String(byte b)
        {
            string result = Convert.ToString(b, 2).PadLeft(8, '0');
            return result;
        }

        public static string Byte2State(byte b)
        {
            
            string result = Convert.ToString(b, 2).PadLeft(8, '0');
            //反向
            result = new string(result.Reverse().ToArray());
            return result;
        }
    }
}