using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Reflection;

namespace Extending_WCF
{
    public class IntegerConverter
    {
        static Stream s = new MemoryStream();
        FileStream fs = new FileStream("D:\\7bit.txt", FileMode.Create);

        public void GetBytes(int value, Stream s)
        {
            BinaryWriter bw = new BinaryWriter(s);
            bw.Write(value);
        }

        public void GetBytes(bool value, Stream s)
        {
            BinaryWriter bw = new BinaryWriter(s);
            bw.Write(value);
        }

        public int ToInt32(Stream s)
        {
            BinaryReader br = new BinaryReader(s);
            return br.ReadInt32();
        }

        public bool ToBoolean(Stream s)
        {
            BinaryReader br = new BinaryReader(s);
            return br.ReadBoolean();
        }

        private void Write(byte b)
        {
            s.WriteByte(b);
        }

        private byte ReadByte()
        {
            return (byte) s.ReadByte();
        }

        public void Write7BitEncodedInt(long value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            bool isNegative = (value < 0);
            ulong v;
            if (isNegative)
            {
                v = (ulong)(-value);
            }
            else
            {
                v = (ulong)value;
            }
            while (v >= 0x40)
            {
                Write((byte)(v | 0x80));
                v >>= 7;
            }
            if (isNegative)
            {
                v |= 0x40;
            }
            Write((byte)v);
            s.Position=0;
            s.CopyTo(fs);
            fs.Close();
            s.Position = 0;
        }

        public long Read7BitEncodedInt()
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            long count = 0;
            int shift = 0;
            byte b;
            b = ReadByte();
            while ((b & 0x80) != 0)
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                //if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                //    throw new FormatException(Environment.GetResourceString("Format_Bad7BitInt32"));

                // ReadByte handles end of stream cases for us.
                count |= ((long)(b & 0x7F) << shift);
                shift += 7;
                b = ReadByte();
            }
            count |= ((long)(b & 0x3F) << shift);
            if ((b & 0x40) == 0x40)
            {
                count = -count;
            }
            return count;
        }

        public static void testmethod(Object o)
        {
            Nullable<int> a = null;
            Console.WriteLine(a.HasValue);
            //Console.WriteLine(a.GetType().AssemblyQualifiedName);
        }
    }
}
