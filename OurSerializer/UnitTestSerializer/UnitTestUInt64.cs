using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Extending_WCF;
using System.IO;
using UnitTestSerializer;
using System.Collections.Generic;

namespace UnitTestSerializer
{
    [TestClass]
    public class UnitTestUInt64
    {
       
       [TestMethod]
        public void TestDouble()
        {
            double testDouble = 1.7E+30;
            List<byte[]> list = new List<byte[]>();
            list = CompareMethode.testGen<double>(testDouble);
            CollectionAssert.AreEqual(list[0], list[1]);
        }
        
       [TestMethod]
        public void TestSingle()
        {
            float testSingle = 3500000000F;
            List<byte[]> list = new List<byte[]>();
            list = CompareMethode.testGen<float>(testSingle);
            CollectionAssert.AreEqual(list[0], list[1]);
        }
        
       [TestMethod]
        public void TestDateTime()
        {
            DateTime testDateTime = DateTime.Now;
            List<byte[]> list = new List<byte[]>();
            list = CompareMethode.testGen<DateTime>(testDateTime);
            CollectionAssert.AreEqual(list[0], list[1]);
        }

        [TestMethod]
        public void TestString()
        {
            string testString = "Dog";
            List<byte[]> list = new List<byte[]>();
            list = CompareMethode.testGen<string>(testString);
            CollectionAssert.AreEqual(list[0], list[1]);
        }
       [TestMethod]
        public void TestUInt64()
        {   
            UInt64 testUInt64 = 0xF;
            List<byte[]> list = new List<byte[]>();
            list = CompareMethode.testGen<UInt64>(testUInt64);
            CollectionAssert.AreEqual(list[0], list[1]);       
        }
        [TestMethod]
        public void TestTimeSpan()
        {
            TimeSpan testTimeSpan = TimeSpan.FromDays(1);
            List<byte[]> list = new List<byte[]>();
            list = CompareMethode.testGen<TimeSpan>(testTimeSpan);
            CollectionAssert.AreEqual(list[0], list[1]);
        }
       

      }
}
