using NUnit.Framework;
using UnityEngine;
using System;

namespace SVO.Runtime.Utility.Tests
{
    [TestFixture]
    [TestOf(typeof(MortonCode))]
    public class MortonCodeTest
    {
        private const uint Max21Bit = 0x1FFFFF; // 2^21 - 1

        [Test]
        public void 编码解码_全零()
        {
            ulong code = MortonCode.Encode3D(0, 0, 0);
            Assert.AreEqual(0UL, code, "编code(0,0,0) 应该等于 0");
            var (x, y, z) = MortonCode.Decode3D(code);
            Assert.AreEqual(0u, x, "X 应为 0");
            Assert.AreEqual(0u, y, "Y 应为 0");
            Assert.AreEqual(0u, z, "Z 应为 0");
        }

        [Test]
        public void 编码解码_基础值()
        {
            uint originalX = 1, originalY = 2, originalZ = 3;
            ulong code = MortonCode.Encode3D(originalX, originalY, originalZ);
            var (x, y, z) = MortonCode.Decode3D(code);
            Assert.AreEqual(originalX, x, "X 不匹配");
            Assert.AreEqual(originalY, y, "Y 不匹配");
            Assert.AreEqual(originalZ, z, "Z 不匹配");
        }

        [Test]
        public void 编码解码_最大值()
        {
            uint originalX = Max21Bit;
            uint originalY = Max21Bit;
            uint originalZ = Max21Bit;
            ulong code = MortonCode.Encode3D(originalX, originalY, originalZ);
            var (x, y, z) = MortonCode.Decode3D(code);
            Assert.AreEqual(originalX, x, "最大值 X 不匹配");
            Assert.AreEqual(originalY, y, "最大值 Y 不匹配");
            Assert.AreEqual(originalZ, z, "最大值 Z 不匹配");
        }

        [Test]
        public void 单调性_X轴递增且YZ为零()
        {
            ulong previous = MortonCode.Encode3D(0, 0, 0);
            for (uint x = 1; x < 2000; x++)
            {
                ulong current = MortonCode.Encode3D(x, 0, 0);
                Assert.Greater(current, previous, $"当仅 X 递增时 Morton code应递增，x={x}");
                previous = current;
            }
        }

        [Test]
        public void 单调性_Y轴递增且XZ为零()
        {
            ulong previous = MortonCode.Encode3D(0, 0, 0);
            for (uint y = 1; y < 2000; y++)
            {
                ulong current = MortonCode.Encode3D(0, y, 0);
                Assert.Greater(current, previous, $"当仅 Y 递增时 Morton code应递增，y={y}");
                previous = current;
            }
        }

        [Test]
        public void 单调性_Z轴递增且XY为零()
        {
            ulong previous = MortonCode.Encode3D(0, 0, 0);
            for (uint z = 1; z < 2000; z++)
            {
                ulong current = MortonCode.Encode3D(0, 0, z);
                Assert.Greater(current, previous, $"当仅 Z 递增时 Morton code应递增，z={z}");
                previous = current;
            }
        }

        [Test]
        public void 随机往返_一万次()
        {
            System.Random random = new System.Random(12345);
            for (int i = 0; i < 10000; i++)
            {
                uint originalX = (uint)random.Next(0, (int)Max21Bit + 1);
                uint originalY = (uint)random.Next(0, (int)Max21Bit + 1);
                uint originalZ = (uint)random.Next(0, (int)Max21Bit + 1);

                ulong code = MortonCode.Encode3D(originalX, originalY, originalZ);
                var (x, y, z) = MortonCode.Decode3D(code);

                if (x != originalX || y != originalY || z != originalZ)
                {
                    Assert.Fail($"随机往返失败: 原=({originalX},{originalY},{originalZ}) 解=({x},{y},{z}) code={code}");
                }
            }
            Assert.Pass("随机 10000 次往返均成功");
        }

        [Test]
        public void 超范围_截断行为()
        {
            uint originalX = Max21Bit + 5;
            uint originalY = Max21Bit + 7;
            uint originalZ = Max21Bit + 9;

            ulong code = MortonCode.Encode3D(originalX, originalY, originalZ);
            var (x, y, z) = MortonCode.Decode3D(code);

            Assert.AreEqual(originalX & Max21Bit, x, "X 截断结果不符预期");
            Assert.AreEqual(originalY & Max21Bit, y, "Y 截断结果不符预期");
            Assert.AreEqual(originalZ & Max21Bit, z, "Z 截断结果不符预期");
        }

        [Test]
        public void 性能测试_二十万次()
        {
            int times = 200_000;
            uint lastX = 0, lastY = 0, lastZ = 0;
            for (int i = 0; i < times; i++)
            {
                lastX = (uint)(i & (int)Max21Bit);
                lastY = (uint)((i * 31) & (int)Max21Bit);
                lastZ = (uint)((i * 17) & (int)Max21Bit);

                ulong code = MortonCode.Encode3D(lastX, lastY, lastZ);
                var (dx, dy, dz) = MortonCode.Decode3D(code);
                if (dx != lastX || dy != lastY || dz != lastZ)
                {
                    Assert.Fail("性能测试检测中出现不匹配");
                }
            }

            // 防止 JIT/优化删除循环
            Assert.AreEqual(lastX, (uint)((times - 1) & (int)Max21Bit), "末尾验证失败");
        }
    }
}