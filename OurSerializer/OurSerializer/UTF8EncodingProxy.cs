using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Extending_WCF
{
    public static class UTF8EncodingProxy
    {
        public static string GetString(byte[] bytes, int startPos)
        {
            return UTF8Encoding.UTF8.GetString(bytes);
        }

        public static byte[] GetBytes(string s)
        {
            return UTF8Encoding.UTF8.GetBytes(s);
        }
    }
}
