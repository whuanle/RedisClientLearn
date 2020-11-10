using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace CZGL.RedisClient
{
    /// <summary>
    /// Redis 客户端
    /// </summary>
    public class RedisClient
    {
        private readonly IPAddress IP;
        private readonly IPEndPoint IPEndPoint;
        private readonly Socket client;

        private readonly Lazy<StringClient> stringClient;
        private readonly Lazy<HashClient> hashClient;
        private readonly Lazy<ListClient> listClient;
        private readonly Lazy<SetClient> setClient;
        private readonly Lazy<SortedClient> sortedClient;

        // 数据流请求队列
        private readonly ConcurrentQueue<MessageStrace> StringTaskQueue = new ConcurrentQueue<MessageStrace>();

        public RedisClient(string ip, int port)
        {
            IP = IPAddress.Parse(ip);
            IPEndPoint = new IPEndPoint(IP, port);

            stringClient = new Lazy<StringClient>(() => new StringClient(this));
            hashClient = new Lazy<HashClient>(() => new HashClient(this));
            listClient = new Lazy<ListClient>(() => new ListClient(this));
            setClient = new Lazy<SetClient>(() => new SetClient(this));
            sortedClient = new Lazy<SortedClient>(() => new SortedClient(this));

            client = new Socket(IP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// 开始连接 Redis
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            await client.ConnectAsync(IPEndPoint);
            new Thread(() => { ReceiveQueue(); })
            {
                IsBackground = true
            }.Start();
            return client.Connected;
        }

        /// <summary>
        /// 发送一个命令，将其加入队列
        /// </summary>
        /// <param name="task"></param>
        /// <param name="command"></param>
        /// <returns></returns>
        internal Task<int> SendAsync(MessageStrace task, string command)
        {
            var buffer = Encoding.UTF8.GetBytes(command + "\r\n");
            var result = client.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), SocketFlags.None);
            StringTaskQueue.Enqueue(task);
            return result;
        }


        /*
         
        Microsoft 对缓冲区输入不同大小的数据，测试响应时间。

        1024 - real 0m0,102s; user  0m0,018s; sys   0m0,009s
        2048 - real 0m0,112s; user  0m0,017s; sys   0m0,009s
        8192 - real 0m0,163s; user  0m0,017s; sys   0m0,007s
         256 - real 0m0,101s; user  0m0,019s; sys   0m0,008s
          16 - real 0m0,144s; user  0m0,016s; sys   0m0,010s


        .NET Socket，默认缓冲区的大小为 8192 字节。
        Socket.ReceiveBufferSize: An Int32 that contains the size, in bytes, of the receive buffer. The default is 8192.
        

        但响应中有很多只是 "+OK\r\n" 这样的响应，并且 MemoryStream 刚好默认是 256(当然，可以自己设置大小)，缓冲区过大，浪费内存；
        超过 256 这个大小，MemoryStream 会继续分配新的 256 大小的内存区域，会消耗性能。
        BufferSize 设置为 256 ，是比较合适的做法。
         */

        private const int BufferSize = 256;

        /// <summary>
        /// 单线程串行接收数据流，调出任务队列完成任务
        /// </summary>
        private void ReceiveQueue()
        {
            while (true)
            {
                MemoryStream stream = new MemoryStream(BufferSize);  // 内存缓存区

                byte[] data = new byte[BufferSize];        // 分片，每次接收 N 个字节

                int size = client.Receive(data);           // 等待接收一个消息
                int length = size;                         // 数据流总长度

                while (true)
                {
                    stream.Write(data, 0, size);            // 分片接收的数据流写入内存缓冲区

                    // 数据流接收完毕
                    if (size < BufferSize)      // 存在 Bug ，当数据流的大小或者数据流分片最后一片的字节大小刚刚好为 BufferSize 大小时，无法跳出 Receive
                    {
                        break;
                    }

                    length += client.Receive(data);       // 还没有接收完毕，继续接收
                }

                stream.Seek(0, SeekOrigin.Begin);         // 重置游标位置

                // 调出队列
                StringTaskQueue.TryDequeue(out var tmpResult);

                // 处理队列中的任务
                tmpResult.Receive(stream, length);
            }
        }

        /// <summary>
        /// 复用
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <returns></returns>
        public T GetClient<T>(T client) where T : CommandClient<T>
        {
            client.Init(this);
            return client;
        }

        /// <summary>
        /// 获取字符串请求客户端
        /// </summary>
        /// <returns></returns>
        public StringClient GetStringClient()
        {
            return stringClient.Value;
        }

        public HashClient GetHashClient()
        {
            return hashClient.Value;
        }

        public ListClient GetListClient()
        {
            return listClient.Value;
        }

        public SetClient GetSetClient()
        {
            return setClient.Value;
        }

        public SortedClient GetSortedClient()
        {
            return sortedClient.Value;
        }
    }
}
