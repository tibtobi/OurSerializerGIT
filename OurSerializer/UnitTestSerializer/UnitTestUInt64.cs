using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Extending_WCF;
using System.IO;

namespace UnitTestSerializer
{
    [TestClass]
    public class UnitTestUInt64
    {
        [TestMethod]
        public void TestUInt64()
        {
            //Init           
            UInt64 testUInt64 = 0xFFFFFFFFEEEEEE;
            UInt64 ansUInt64Num;
            var s = new MemoryStream();

            //Serialize
            GenSerializer.Serialize<UInt64>(s, testUInt64);
            s.Position = 0;

            //Deserialize
            ansUInt64Num = (UInt64)GenSerializer.Deserialize<UInt64>(s);
            Assert.AreEqual(testUInt64, ansUInt64Num);
        }

        [TestMethod]
        public void TestDoubleSmall()
        {
            //Init           
            double testDouble = 0.000000000000000001;
            double ansDoubleNum;
            var s = new MemoryStream();

            //Serialize
            GenSerializer.Serialize<double>(s, testDouble);
            s.Position = 0;

            //Deserialize
            ansDoubleNum = (double)GenSerializer.Deserialize<UInt64>(s);
            Assert.AreEqual(testDouble, ansDoubleNum);
        }

        [TestMethod]
        public void TestDoubleBig()
        {
            //Init           
            double testDouble = 123456789123456789;
            double ansDoubleNum;
            var s = new MemoryStream();

            //Serialize
            GenSerializer.Serialize<double>(s, testDouble);
            s.Position = 0;

            //Deserialize
            ansDoubleNum = (double)GenSerializer.Deserialize<UInt64>(s);
            Assert.AreEqual(testDouble, ansDoubleNum);
        }

        [TestMethod]
        public void TestSingle()
        {
            //Init           
            double testSingle = 0.2f;
            double ansSingleNum;
            var s = new MemoryStream();

            //Serialize
            GenSerializer.Serialize<double>(s, testSingle);
            s.Position = 0;

            //Deserialize
            ansSingleNum = (double)GenSerializer.Deserialize<UInt64>(s);
            Assert.AreEqual(testSingle, ansSingleNum);
        }
    }
}
