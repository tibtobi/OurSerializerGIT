using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Extending_WCF;

namespace UnitTestSerializer
{
    class CompareMethode
    {
        private static bool debug_mode = false;

        private static List<byte[]> TransformMStreamsToArrays(MemoryStream Xms1, MemoryStream Xms2)
        {
            List<byte[]> list = new List<byte[]>();
            if (Xms1.Length != Xms2.Length)
                return list;
            Xms1.Position = 0;
            Xms2.Position = 0;

            var msArray1 = Xms1.ToArray();
            var msArray2 = Xms2.ToArray();
            list.Add(msArray1);
            list.Add(msArray2);

            return list;
        }

        private static List<byte[]> testGen<T>(T obj)
        {
            MemoryStream Xm1 = new MemoryStream();
            MemoryStream Xm2 = new MemoryStream();
            MemoryStream MySerMS = new MemoryStream();
            XmlSerializer xmlser = new XmlSerializer(typeof(T));
            List<byte[]> list = new List<byte[]>();
            T retObj;

            xmlser.Serialize(Xm1, obj);
            GenSerializer.Serialize<T>(MySerMS, obj);
            MySerMS.Position = 0;
            retObj = GenSerializer.Deserialize<T>(MySerMS);
            xmlser.Serialize(Xm2, retObj);
            CopyStreamToFile(Xm1, Xm2);
            return TransformMStreamsToArrays(Xm1, Xm2);
        }

        private static void CopyStreamToFile(Stream memory1, Stream memory2 )
        {
            if(!debug_mode)
                return;
            FileStream fs1 = new FileStream("D:\\Debug\fs1.txt", FileMode.Create);
            FileStream fs2 = new FileStream("D:\\Debug\fs2.txt", FileMode.Create);
            memory1.Position = 0;
            memory1.CopyTo(fs1);
            memory2.Position = 0;
            memory2.CopyTo(fs2);
            fs1.Flush();
            fs2.Flush();
            fs1.Close();
            fs2.Close();
        }
    }
}
