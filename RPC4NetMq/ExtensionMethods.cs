using System;

namespace RPC4NetMq
{    
    internal static class ExtensionMethods
    {
        internal static object ConvertToCorrectTypeValue(this object value, Type type)
        {
            if (value == null)
            {
                return null;
            }
            if (type.IsPrimitive)
            {
                return Convert.ChangeType(value, type);
            }
            return value;
        }
    }
}
