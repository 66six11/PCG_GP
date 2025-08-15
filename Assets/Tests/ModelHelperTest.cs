using NUnit.Framework;
using Utility;

namespace Tests
{
    [TestFixture]
    [TestOf(typeof(ModelHelper))]
    public class ModelHelperTest
    {
        //模型顶点顺序是顺时针方向，从上到下
        // a1 b1
        // d1 c1   上
        //--------------------------
        // a2 b2
        // d2 c2   下
        
        
        [Test]
        public void Rotate90_ShouldRotateCorrectly()
        {
            byte test = 0b10011001;
            //测试y轴旋转90度
            byte expected = 0b00110011;
            byte actual = ModelHelper.Rotate90(test);
            Assert.AreEqual(expected, actual, $"目标旋转90度后的值与预期不符,预期值：{ModelHelper.Byte2String(expected)} 实际值：{ModelHelper.Byte2String(actual)}");
            //测试y轴旋转180度
            expected = 0b01100110;
            actual = ModelHelper.Rotate180(test);
            Assert.AreEqual(expected, actual, $"目标旋转180度后的值与预期不符,预期值：{ModelHelper.Byte2String(expected)} 实际值：{ModelHelper.Byte2String(actual)}");
            //测试y轴旋转270度
            expected = 0b11001100;
            actual = ModelHelper.Rotate270(test);
            Assert.AreEqual(expected, actual, $"目标旋转270度后的值与预期不符,预期值：{ModelHelper.Byte2String(expected)} 实际值：{ModelHelper.Byte2String(actual)}");
        }

        [Test]
        public void FlipX_ShouldFlipCorrectly()
        {
            byte test = 0b10011001;
            //测试X轴翻转
            byte expected = 0b01100110;
            byte actual = ModelHelper.FlipX(test);
            Assert.AreEqual(expected, actual, $"目标翻转X轴后的值与预期不符,预期值：{ModelHelper.Byte2String(expected)} 实际值：{ModelHelper.Byte2String(actual)}");
            //测试Y轴翻转
            expected = 0b01100110;
            actual = ModelHelper.FlipZ(test);
            Assert.AreEqual(expected, actual, $"目标翻转Z轴后的值与预期不符,预期值：{ModelHelper.Byte2String(expected)} 实际值：{ModelHelper.Byte2String(actual)}");
        }
    }
}