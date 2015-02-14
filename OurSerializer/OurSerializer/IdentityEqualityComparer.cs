using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;

namespace Extending_WCF
{
    public class IdentityEqualityComparer<T> : IEqualityComparer<T>
    where T : class
    {
        public int GetHashCode(T value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }

        public bool Equals(T left, T right)
        {
            return object.ReferenceEquals(left, right);
        }
    }
}
