using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CZGL.RedisClient
{
    /// <summary>
    /// RESP 解析数据流
    /// </summary>
    public class MessageStraceAnalysis<T> : MessageStrace
    {
        public MessageStraceAnalysis()
        {

        }

        /// <summary>
        /// 解析协议
        /// </summary>
        /// <param name="data"></param>
        public override void Receive(MemoryStream stream, int length)
        {
            byte firstChar = (byte)stream.ReadByte(); // 首位字符，由于游标已经到 1，所以后面 .GetBuffer()，都是从1开始截断，首位字符舍弃;

            if (firstChar.CompareTo(RedisValueType.SimpleStrings) == 0)    // 简单字符串
            {
                SetValue(Encoding.UTF8.GetString(stream.GetBuffer()));
                return;
            }

            else if (firstChar.CompareTo(RedisValueType.Errors) == 0)
            {
                TaskCompletionSource.SetException(new InvalidOperationException(Encoding.UTF8.GetString(stream.GetBuffer())));
                return;
            }

            // 不是 + 和 - 开头

            stream.Position = 0;
            int index = 0;
            ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(stream.GetBuffer());

            string tmp = Analysis(data, ref index);
            SetValue(tmp);
        }

        // 进入递归处理流程
        private string Analysis(ReadOnlySpan<byte> data, ref int index)
        {
            // *
            if (data[index].CompareTo(RedisValueType.Arrays) == 0)
            {
                string value = default;
                index++;
                int size = BulkStrings(data, ref index);

                if (size == 0)
                    return string.Empty;
                else if (size == -1)
                    return null;

                for (int i = 0; i < size; i++)
                {
                    var tmp = Analysis(data, ref index);
                    value += tmp + ((i < (size - 1)) ? "\r\n" : string.Empty);
                }
                return value;
            }

            // $..
            else if (data[index].CompareTo(RedisValueType.BulkStrings) == 0)
            {
                index++;
                int size = BulkStrings(data, ref index);

                if (size == 0)
                    return string.Empty;
                else if (size == -1)
                    return null;
                var value = Encoding.UTF8.GetString(data.Slice(index, size).ToArray());
                index += size + 2; // 脱离之前，将指针移动到 \n 后
                return value;
            }

            throw new ArgumentException("解析错误");
        }
    }
}
