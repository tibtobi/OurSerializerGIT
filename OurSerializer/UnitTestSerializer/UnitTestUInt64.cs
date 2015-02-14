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
        public void TestMethod1()
        {
            //Init           
            UInt64 testNum = 0x2;
            UInt64 ansNum;
            var s = new MemoryStream();

            //Serialize
            GenSerializer.Serialize<UInt64>(s, testNum);
            s.Position = 0;

            //Deserialize
            ansNum = (UInt64)GenSerializer.Deserialize<UInt64>(s);
            Assert.AreEqual(testNum, ansNum, 1);
        }
    }
}
