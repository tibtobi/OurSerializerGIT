using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Extending_WCF;
using Newtonsoft.Json;
using System.Security.Permissions;


namespace UnitTestSerializer
{
    
    public class Person
    {
        int age { get; set; }
        public string name { get; set; }
        static int d;

        public Person()
        {
            d++;
            age = d;
            name = "ABCD";
        }

        bool Compare(Person p)
        {
            return age == p.age;
        }
    }

    class CompareMethode
    {
        private static bool debug_mode = true;

        private static List<byte[]> TransformMStreamsToArrays(MemoryStream Xms1, MemoryStream Xms2)
        {
            List<byte[]> list = new List<byte[]>();
            if (Xms1.Length != Xms2.Length)
                return list;
            Xms1.Position = 0;
            Xms2.Position = 0;

            byte[] msArray1 = Xms1.ToArray();
            byte[] msArray2 = Xms2.ToArray();
            list.Add(msArray1);
            list.Add(msArray2);

            return list;
        }

        public static List<byte[]> testGen<T>(T obj)
        {
            MemoryStream Xm1 = new MemoryStream();
            MemoryStream Xm2 = new MemoryStream();
            MemoryStream MySerMS = new MemoryStream();
            XmlSerializer xmlser = new XmlSerializer(typeof(T));
            T retObj;
            Type typeParameterType = typeof(T);
            List<string> result = new List<string>();

            xmlser.Serialize(Xm1, obj);
            GenSerializer.Serialize<T>(MySerMS, obj);
            MySerMS.Position = 0;
            retObj = GenSerializer.Deserialize<T>(MySerMS);
            xmlser.Serialize(Xm2, retObj);
            CopyStreamToFile(Xm1, Xm2, typeParameterType);

            return TransformMStreamsToArrays(Xm1, Xm2);
        }

        public static List<string> testGenCollection<T>(T obj)
        {
           
            MemoryStream MySerMS = new MemoryStream();
            string jsonOriginal;
            string jsonOwn;
            T retObj;
            Type typeParameterType = typeof(T);
            List<string> result = new List<string>();

            jsonOriginal = JsonConvert.SerializeObject(obj);
            GenSerializer.Serialize<T>(MySerMS, obj);
            MySerMS.Position = 0;
            retObj = GenSerializer.Deserialize<T>(MySerMS);
            jsonOwn = JsonConvert.SerializeObject(retObj); ;
            CopyStringToFile(jsonOriginal, jsonOwn, typeParameterType);
            result.Add(jsonOriginal);
            result.Add(jsonOwn);
            return result;
        }

       
        public static T testGenClass<T>(T obj)
        {

            MemoryStream MySerMS = new MemoryStream();
            string jsonOriginal;
            string jsonOwn;
            T retObj;
            Type typeParameterType = typeof(T);
            List<string> result = new List<string>();

            jsonOriginal = JsonConvert.SerializeObject(obj);
            GenSerializer.Serialize<T>(MySerMS, obj);
            MySerMS.Position = 0;
            retObj = GenSerializer.Deserialize<T>(MySerMS);
            jsonOwn = JsonConvert.SerializeObject(retObj); ;
            CopyStringToFile(jsonOriginal, jsonOwn, typeParameterType);
            result.Add("true");
            result.Add(obj.Compare(retObj) ? "true" : "false");
            return result;
        }


        private static void CopyStreamToFile(Stream memory1, Stream memory2, Type typeParameterType)
        {
            if(!debug_mode)
                return;
           
            FileStream fs1 = new FileStream(@"D:\Dev\Önlab\GitHub\OurSerializerGIT\fs1_" + typeParameterType + ".txt", FileMode.OpenOrCreate);
            FileStream fs2 = new FileStream(@"D:\Dev\Önlab\GitHub\OurSerializerGIT\fs2_" + typeParameterType + ".txt", FileMode.OpenOrCreate);
            memory1.Position = 0;
            memory1.CopyTo(fs1);
            memory2.Position = 0;
            memory2.CopyTo(fs2);
            fs1.Flush();
            fs2.Flush();
            fs1.Close();
            fs2.Close();
        }

        private static void CopyStringToFile(string string1, string string2, Type typeParameterType)
        {
            if (!debug_mode)
                return;

            StreamWriter fs1 = new StreamWriter(@"D:\Dev\Önlab\GitHub\OurSerializerGIT\fs1_" + typeParameterType + ".txt");
            StreamWriter fs2 = new StreamWriter(@"D:\Dev\Önlab\GitHub\OurSerializerGIT\fs2_" + typeParameterType + ".txt");

            fs1.WriteLine(string1);
            fs2.WriteLine(string2);
            
            
        }
    }
}
