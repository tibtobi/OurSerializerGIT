using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Extending_WCF;
using System.IO;
using UnitTestSerializer;
using System.Collections.Generic;

namespace UnitTestSerializer
{
    [TestClass]
    public class UnitTestCollection
    {
        [TestMethod]
        public void TestDictionary()
        {
            Dictionary<string, string> testDict = new Dictionary<string, string>();
            testDict.Add("1", "egy");
            testDict.Add("2", "kettő");
            testDict.Add("3", "három");
            testDict.Add("4", "négy");
            List<string> list = new List<string>();
            list = CompareMethode.testGenCollection<Dictionary<string, string>>(testDict);
            Assert.AreEqual(list[0], list[1]);

        }

        [TestMethod]
        public void TestList()
        {
            List<string> testDict = new List<string>();
            testDict.Add("egy");
            testDict.Add("kettő");
            testDict.Add("három");
            testDict.Add("négy");
            List<string> list = new List<string>();
            list = CompareMethode.testGenCollection<List<string>>(testDict);
            Assert.AreEqual(list[0], list[1]);
        }

        [TestMethod]
        public void TestListClass()
        {
            List<Person> testDict = new List<Person>();
            testDict.Add(new Person());
            testDict.Add(new Person());
            testDict.Add(new Person());
            testDict.Add(new Person());
            testDict.Add(new Person());
            //todo
            List<string> list = new List<string>();
            list = CompareMethode.testGenCollection<List<Person>>(testDict);
            Assert.AreEqual(list[0], list[1]);
        }

        [TestMethod]
        public void TestQueue()
        {
            Queue<int> q = new Queue<int>();

            q.Enqueue(5);   // Add 5 to the end of the Queue.
            q.Enqueue(10);  // Then add 10. 5 is at the start.
            q.Enqueue(15);  // Then add 15.
            q.Enqueue(20);
            List<string> list = new List<string>();
            list = CompareMethode.testGenCollection<Queue<int>>(q);
            Assert.AreEqual(list[0], list[1]);
        }

        [TestMethod]
        public void TestStack()
        {
            Stack<int> stack = new Stack<int>();
            stack.Push(100);
            stack.Push(1000);
            stack.Push(10000);

            List<string> list = new List<string>();
            list = CompareMethode.testGenCollection<Stack<int>>(stack);
            Assert.AreEqual(list[0], list[1]);
        }
    }
}
