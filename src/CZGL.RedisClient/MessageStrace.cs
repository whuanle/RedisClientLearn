using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CZGL.RedisClient
{
    /// <summary>
    /// 自定义消息队列状态机
    /// </summary>
    public abstract class MessageStrace
    {
        protected MessageStrace()
        {
            TaskCompletionSource = new TaskCompletionSource<string>();
            Task = TaskCompletionSource.Task;
        }

        protected readonly TaskCompletionSource<string> TaskCompletionSource;

        /// <summary>
        /// 标志任务是否完成，并接收 redis 响应的字符串数据流
        /// </summary>
        public Task<string> Task { get; private set; }

        /// <summary>
        /// 接收数据流
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length">实际长度</param>
        public abstract void Receive(MemoryStream stream, int length);

        /// <summary>
        /// 响应已经完成
        /// </summary>
        /// <param name="data"></param>
        protected void SetValue(string data)
        {
            TaskCompletionSource.SetResult(data);
        }


        /// <summary>
        /// 解析 $ 或 * 符号后的数字，必须传递符后后一位的下标
        /// </summary>
        /// <param name="data"></param>
        /// <param name="index">解析到的位置</param>
        /// <returns></returns>
        protected int BulkStrings(ReadOnlySpan<byte> data, ref int index)
        {

            int start = index;
            int end = start;

            while (true)
            {
                if (index + 1 >= data.Length)
                    throw new ArgumentOutOfRangeException("溢出");

                // \r\n
                if (data[index].CompareTo(RedisValueType.R) == 0 && data[index + 1].CompareTo(RedisValueType.N) == 0)
                {
                    index += 2;     // 指向 \n 的下一位
                    break;
                }
                end++;
                index++;
            }

            // 截取 $2    *3  符号后面的数字
            return Convert.ToInt32(Encoding.UTF8.GetString(data.Slice(start, end - start).ToArray()));
        }
    }
}
