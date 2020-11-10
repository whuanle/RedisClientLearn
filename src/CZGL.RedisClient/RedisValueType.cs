using System;
using System.Collections.Generic;
using System.Text;

namespace CZGL.RedisClient
{
    /// <summary>
    /// RESP Response 类型
    /// </summary>
    public static class RedisValueType
    {
        public const byte Errors = (byte)'-';
        public const byte SimpleStrings = (byte)'+';
        public const byte Integers = (byte)':';
        public const byte BulkStrings = (byte)'$';
        public const byte Arrays = (byte)'*';


        public const byte R = (byte)'\r';
        public const byte N = (byte)'\n';
    }
}
