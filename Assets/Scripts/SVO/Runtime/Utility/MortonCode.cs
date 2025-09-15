using System;

namespace SVO.Runtime.Utility
{
    public static class MortonCode
    {
        // 编码三维坐标 (x, y, z) 为莫顿码
        public static ulong Encode3D(uint x, uint y, uint z)
        {
            // 1. 将每个坐标扩展到64位（实际使用21位）
            ulong xx = ExpandBits(x);
            ulong yy = ExpandBits(y);
            ulong zz = ExpandBits(z);

            // 2. 交错比特位：Z->Y->X 顺序
            return (zz << 2) | (yy << 1) | xx;
        }

        // 将21位整数扩展为64位（间隔2个0）
        private static ulong ExpandBits(uint v)
        {
            ulong x = v & 0x1FFFFF; // 确保只使用21位
            x = (x | (x << 32)) & 0x1F00000000FFFF;
            x = (x | (x << 16)) & 0x1F0000FF0000FF;
            x = (x | (x << 8)) & 0x100F00F00F00F00F;
            x = (x | (x << 4)) & 0x10C30C30C30C30C3;
            x = (x | (x << 2)) & 0x1249249249249249;
            return x;
        }

        // 解码莫顿码为三维坐标
        public static (uint x, uint y, uint z) Decode3D(ulong morton)
        {
            uint x = CompactBits(morton);
            uint y = CompactBits(morton >> 1);
            uint z = CompactBits(morton >> 2);
            return (x, y, z);
        }

        // 压缩比特位（扩展的逆操作）
        private static uint CompactBits(ulong x)
        {
            x &= 0x1249249249249249;
            x = (x | (x >> 2)) & 0x10C30C30C30C30C3;
            x = (x | (x >> 4)) & 0x100F00F00F00F00F;
            x = (x | (x >> 8)) & 0x1F0000FF0000FF;
            x = (x | (x >> 16)) & 0x1F00000000FFFF;
            x = (x | (x >> 32)) & 0x1FFFFF;
            return (uint)x;
        }
    }
}