using System;

namespace Utility
{
    public static class ModelHelper
    {
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

        /// <summary>
        ///  顺旋转模型得到的状态字节
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static byte Rotate90(byte b)
        {
            // 分离低4位和高4位
            int low = b & 0x0F;         // 获取低4位
            int high = (b >> 4) & 0x0F; // 获取高4位

            // 对低4位循环左移一位：组首移至组尾
            low = ((low << 1) | (low >> 3)) & 0x0F;
            // 对高4位循环左移一位：组首移至组尾
            high = ((high << 1) | (high >> 3)) & 0x0F;

            // 重新组合并返回结果
            return (byte)((high << 4) | low);
        }

        public static byte Rotate180(byte b)
        {
            //旋转两次
            b = Rotate90(b);
            b = Rotate90(b);
            return b;
        }

        public static byte Rotate270(byte b)
        {
            //旋转四次
            b = Rotate90(b);
            b = Rotate90(b);
            b = Rotate90(b);
            return b;
        }

        /// <summary>
        ///  水平翻转模型得到的状态字节
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static byte FlipX(byte b)
        {
            //v1 v2交换
            //v3 v4交换
            //v5 v6交换
            //v7 v8交换
            // 交换相邻的两位（每两位为一组）
            return (byte)(
                ((b & 0xAA) >> 1) | // 提取奇数位并右移
                ((b & 0x55) << 1)   // 提取偶数位并左移
            );
        }

        /// <summary>
        ///  前后翻转模型得到的状态字节
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static byte FlipZ(byte b)
        {
             // 交换v1和v3
             // 交换v2和v4
             // 交换v5和v7
             // 交换v6和v8
             // 提取每一位的值
             int bit0 = (b >> 0) & 1; // v1
             int bit1 = (b >> 1) & 1; // v2
             int bit2 = (b >> 2) & 1; // v3
             int bit3 = (b >> 3) & 1; // v4
             int bit4 = (b >> 4) & 1; // v5
             int bit5 = (b >> 5) & 1; // v6
             int bit6 = (b >> 6) & 1; // v7
             int bit7 = (b >> 7) & 1; // v8

             // 组合并返回结果
             return (byte)(
                 (bit2 << 0) | // v3 -> v1 位置
                 (bit3 << 1) | // v4 -> v2 位置
                 (bit0 << 2) | // v1 -> v3 位置
                 (bit1 << 3) | // v2 -> v4 位置
                 (bit6 << 4) | // v7 -> v5 位置
                 (bit7 << 5) | // v8 -> v6 位置
                 (bit4 << 6) | // v5 -> v7 位置
                 (bit5 << 7)   // v6 -> v8 位置
             );
        }

        public static string Byte2String(byte b)
        {
            string result = Convert.ToString(b, 2).PadLeft(8, '0');
            return result;
        }
    }
}