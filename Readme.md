
最近叶老板写了个 FreeRedis，功能强悍，性能惊人，比子弹更快，比引擎更有力，刚好前段时间在学习 Redis，于是跟风试试也写一个简单的 RedisClient。

FreeRedis 项目地址：[https://github.com/2881099/FreeRedis](https://github.com/2881099/FreeRedis)

本文教程源码 Github 地址：[https://github.com/whuanle/RedisClientLearn](https://github.com/whuanle/RedisClientLearn)

由于代码简单，不考虑太多功能，不支持密码登录；不支持集群；不支持并发；



首先之行在自己电脑安装 redis，Windows 版本下载地址：https://github.com/MicrosoftArchive/redis/releases

然后下载 Windows 版的 Redis 管理器

Windows 版本的 **Redis** Desktop Manager 64位 2019.1(中文版) 下载地址 https://www.7down.com/soft/233274.html

官方正版最新版本下载地址 https://redisdesktop.com/download



### 0，关于 Redis RESP

**RESP** 全称 REdis Serialization Protocol ，即 Redis 序列化协议，用于协定客户端使用 socket 连接 Redis 时，数据的传输规则。

官方协议说明：https://redis.io/topics/protocol 



那么 RESP 协议在与 Redis 通讯时的 请求-响应 方式如下：

- 客户端将**命令**作为 RESP 大容量字符串数组(即 **C# 中使用 byte[] 存储字符串命令**)发送到 Redis 服务器。
- 服务器根据命令实现以 **RESP 类型**进行回复。

RESP 中的类型并不是指 Redis 的基本数据类型，而是指数据的响应格式：

在 RESP 中，某些数据的类型取决于第一个字节：

- 对于**简单字符串**，答复的第一个字节为“ +”
- 对于**错误**，回复的第一个字节为“-”
- 对于**整数**，答复的第一个字节为“：”
- 对于**批量字符串**，答复的第一个字节为“ $”
- 对于**数组**，回复的第一个字节为“ `*`”



对于这些，可能初学者不太了解，下面我们来实际操作一下。

我们打开 Redis Desktop Manager ，然后点击控制台，输入：

```csharp
set a 12
set b 12
set c 12
MGET abc
```

以上命令每行按一下回车键。MGET 是 Redis 中一次性取出多个键的值的命令。

输出结果如下：

```csharp
本地:0>SET a 12
"OK"
本地:0>SET b 12
"OK"
本地:0>SET c 12
"OK"
本地:0>MGET a b c
 1)  "12"
 2)  "12"
 3)  "12"
```

但是这个管理工具以及去掉了 RESP 中的协议标识符，我们来写一个 demo 代码，还原 RESP 的本质。

```csharp
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IPAddress IP = IPAddress.Parse("127.0.0.1");
            IPEndPoint IPEndPoint = new IPEndPoint(IP, 6379);
            Socket client = new Socket(IP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await client.ConnectAsync(IPEndPoint);

            if (!client.Connected)
            {
                Console.WriteLine("连接 Redis 服务器失败！");
                Console.Read();
            }

            Console.WriteLine("恭喜恭喜，连接 Redis 服务器成功");


            // 后台接收消息
            new Thread(() =>
            {
                while (true)
                {
                    byte[] data = new byte[100];
                    int size = client.Receive(data);
                    Console.WriteLine();
                    Console.WriteLine(Encoding.UTF8.GetString(data));
                    Console.WriteLine();
                }
            }).Start();

            while (true)
            {
                Console.Write("$> ");
                string command = Console.ReadLine();
                // 发送的命令必须以 \r\n 结尾
                int size = client.Send(Encoding.UTF8.GetBytes(command + "\r\n"));
                Thread.Sleep(100);
            }
        }
    }
}
```

输入以及输出结果：

```csharp
$> SET a 123456789
+OK
$> SET b 123456789
+OK
$> SET c 123456789
+OK
$> MGET a b c

*3
$9
123456789
$9
123456789
$9
123456789
```

可见，Redis 响应的消息内容，是以 $、*、+ 等字符开头的，并且使用 \r\n 分隔。

我们写 Redis Client 的方法就是接收 socket 内容，然后从中解析出实际的数据。

每次发送设置命令成功，都会返回 +OK；*3 表示有三个数组；$9 表示接收的数据长度是 9；

大概就是这样了，下面我们来写一个简单的 Redis Client 框架，然后睡觉。

记得使用 netstandard2.1，因为有些 byte[] 、string、`ReadOnlySpan<T>` 的转换，需要 netstandard2.1 才能更加方便。



### 1，定义数据类型

根据前面的 demo，我们来定义一个类型，存储那些特殊符号：

```csharp
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
```



### 2，定义异步消息状态机

创建一个 MessageStrace 类，作用是作为消息响应的异步状态机，并且具有解析数据流的功能。



```csharp
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
```



### 3，定义命令发送模板

由于 Redis 命令非常多，为了更加好的封装，我们定义一个消息发送模板，规定五种类型分别使用五种类型发送 Client。

定义一个统一的模板类：

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CZGL.RedisClient
{
    /// <summary>
    /// 命令发送模板
    /// </summary>
    public abstract class CommandClient<T> where T : CommandClient<T>
    {
        protected RedisClient _client;
        protected CommandClient()
        {

        }
        protected CommandClient(RedisClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 复用
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        internal virtual CommandClient<T> Init(RedisClient client)
        {
            _client = client;
            return this;
        }


        /// <summary>
        /// 请求是否成功
        /// </summary>
        /// <param name="value">响应的消息</param>
        /// <returns></returns>
        protected bool IsOk(string value)
        {
            if (value[0].CompareTo('+') != 0 || value[1].CompareTo('O') != 0 || value[2].CompareTo('K') != 0)
                return false;
            return true;
        }

        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="command">发送的命令</param>
        /// <param name="strace">数据类型客户端</param>
        /// <returns></returns>
        protected Task SendCommand<TStrace>(string command, out TStrace strace) where TStrace : MessageStrace, new()
        {
            strace = new TStrace();
            return _client.SendAsync(strace, command);
        }
    }
}
```



### 4，定义 Redis Client

RedisClient 类用于发送 Redis 命令，然后将任务放到队列中；接收 Redis 返回的数据内容，并将数据流写入内存中，调出队列，设置异步任务的返回值。

Send 过程可以并发，但是接收消息内容使用单线程。为了保证消息的顺序性，采用队列来记录 Send - Receive 的顺序。

C# 的 Socket 比较操蛋，想搞并发和高性能 Socket 不是那么容易。

以下代码有三个地方注释了，后面继续编写其它代码会用到。

```csharp
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

        //private readonly Lazy<StringClient> stringClient;
        //private readonly Lazy<HashClient> hashClient;
        //private readonly Lazy<ListClient> listClient;
        //private readonly Lazy<SetClient> setClient;
        //private readonly Lazy<SortedClient> sortedClient;

        // 数据流请求队列
        private readonly ConcurrentQueue<MessageStrace> StringTaskQueue = new ConcurrentQueue<MessageStrace>();

        public RedisClient(string ip, int port)
        {
            IP = IPAddress.Parse(ip);
            IPEndPoint = new IPEndPoint(IP, port);

            //stringClient = new Lazy<StringClient>(() => new StringClient(this));
            //hashClient = new Lazy<HashClient>(() => new HashClient(this));
            //listClient = new Lazy<ListClient>(() => new ListClient(this));
            //setClient = new Lazy<SetClient>(() => new SetClient(this));
            //sortedClient = new Lazy<SortedClient>(() => new SortedClient(this));

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

        ///// <summary>
        ///// 获取字符串请求客户端
        ///// </summary>
        ///// <returns></returns>
        //public StringClient GetStringClient()
        //{
        //    return stringClient.Value;
        //}

        //public HashClient GetHashClient()
        //{
        //    return hashClient.Value;
        //}

        //public ListClient GetListClient()
        //{
        //    return listClient.Value;
        //}

        //public SetClient GetSetClient()
        //{
        //    return setClient.Value;
        //}

        //public SortedClient GetSortedClient()
        //{
        //    return sortedClient.Value;
        //}
    }
}
```



### 5，实现简单的 RESP 解析

下面使用代码来实现对 Redis RESP 消息的解析，时间问题，我只实现 +、-、$、* 四个符号的解析，其它符号可以自行参考完善。

创建一个 MessageStraceAnalysis`.cs ，其代码如下：

```csharp
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
```



### 6，实现命令发送客户端

由于 Redis 命令太多，如果直接将所有命令封装到 RedisClient 中，必定使得 API 过的，而且代码难以维护。因此，我们可以拆分，根据 string、hash、set 等 redis 类型，来设计客户端。

下面来设计一个 StringClient：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CZGL.RedisClient
{
    /// <summary>
    /// 字符串类型
    /// </summary>
    public class StringClient : CommandClient<StringClient>
    {
        internal StringClient()
        {

        }

        internal StringClient(RedisClient client) : base(client)
        {
        }

        /// <summary>
        /// 设置键值
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="value">value</param>
        /// <returns></returns>
        public async Task<bool> Set(string key, string value)
        {
            await SendCommand<MessageStraceAnalysis<string>>($"{StringCommand.SET} {key} {value}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }

        /// <summary>
        /// 获取一个键的值
        /// </summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public async Task<string> Get(string key)
        {
            await SendCommand($"{StringCommand.GET} {key}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return result;
        }

        /// <summary>
        /// 从指定键的值中截取指定长度的数据
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="start">开始下标</param>
        /// <param name="end">结束下标</param>
        /// <returns></returns>
        public async Task<string> GetRance(string key, uint start, int end)
        {
            await SendCommand($"{StringCommand.GETRANGE} {key} {start} {end}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return result;
        }

        /// <summary>
        /// 设置一个值并返回旧的值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        public async Task<string> GetSet(string key, string newValue)
        {
            await SendCommand($"{StringCommand.GETSET} {key} {newValue}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return result;
        }

        /// <summary>
        /// 获取二进制数据中某一位的值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <returns>0 或 1</returns>
        public async Task<int> GetBit(string key, uint index)
        {
            await SendCommand($"{StringCommand.GETBIT} {key} {index}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// 设置某一位为 1 或 0
        /// </summary>
        /// <param name="key"></param>
        /// <param name="index"></param>
        /// <param name="value">0或1</param>
        /// <returns></returns>
        public async Task<bool> SetBit(string key, uint index, uint value)
        {
            await SendCommand($"{StringCommand.SETBIT} {key} {index} {value}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }


        /// <summary>
        /// 获取多个键的值
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string[]> MGet(params string[] key)
        {
            await SendCommand($"{StringCommand.MGET} {string.Join(" ", key)}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return result.Split("\r\n");
        }



        private static class StringCommand
        {
            public const string SET = "SET";
            public const string GET = "GET";
            public const string GETRANGE = "GETRANGE";
            public const string GETSET = "GETSET";
            public const string GETBIT = "GETBIT";
            public const string SETBIT = "SETBIT";
            public const string MGET = "MGET";
            // ... ... 更多 字符串的命令
        }
    }
}
```

StringClient 实现了 7个 Redis String 类型的命令，其它命令触类旁通。

 我们打开 RedisClient.cs，解除以下部分代码的注释：

```csharp
private readonly Lazy<StringClient> stringClient;	// 24 行

stringClient = new Lazy<StringClient>(() => new StringClient(this));  // 38 行

         // 146 行
        /// <summary>
        /// 获取字符串请求客户端
        /// </summary>
        /// <returns></returns>
        public StringClient GetStringClient()
        {
            return stringClient.Value;
        }
```



### 7，如何使用

RedisClient 使用示例：

```csharp
        static async Task Main(string[] args)
        {
            RedisClient client = new RedisClient("127.0.0.1", 6379);
            var a = await client.ConnectAsync();
            if (!a)
            {
                Console.WriteLine("连接服务器失败");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("连接服务器成功");

            var stringClient = client.GetStringClient();
            var result = await stringClient.Set("a", "123456789");

            Console.Read();
        }
```

封装的消息命令支持异步。

### 8，更多客户端

光 String 类型不过瘾，我们继续封装更多的客户端。

哈希：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CZGL.RedisClient
{
    public class HashClient : CommandClient<HashClient>
    {
        internal HashClient(RedisClient client) : base(client)
        {
        }

        /// <summary>
        /// 设置哈希
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="values">字段-值列表</param>
        /// <returns></returns>
        public async Task<bool> HmSet(string key, Dictionary<string, string> values)
        {
            await SendCommand($"{StringCommand.HMSET} {key} {string.Join(" ", values.Select(x => $"{x.Key} {x.Value}").ToArray())})", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }

        public async Task<bool> HmSet<T>(string key, T values)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
            foreach (var item in typeof(T).GetProperties())
            {
                dic.Add(item.Name, (string)item.GetValue(values));
            }
            await SendCommand($"{StringCommand.HMSET} {key} {string.Join(" ", dic.Select(x => $"{x.Key} {x.Value}").ToArray())})", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }

        public async Task<object> HmGet(string key, string field)
        {
            await SendCommand($"{StringCommand.HMGET} {key} {field}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }

        private static class StringCommand
        {
            public const string HMSET = "HMSET ";
            public const string HMGET = "HMGET";
            // ... ... 更多 字符串的命令
        }
    }
}
```



列表：

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CZGL.RedisClient
{
    public class ListClient : CommandClient<ListClient>
    {
        internal ListClient(RedisClient client) : base(client)
        {

        }


        /// <summary>
        /// 设置键值
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="value">value</param>
        /// <returns></returns>
        public async Task<bool> LPush(string key, string value)
        {
            await SendCommand($"{StringCommand.LPUSH} {key} {value}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }


        public async Task<string> LRange(string key, int start, int end)
        {
            await SendCommand($"{StringCommand.LRANGE} {key} {start} {end}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return result;
        }

        private static class StringCommand
        {
            public const string LPUSH = "LPUSH";
            public const string LRANGE = "LRANGE";
            // ... ... 更多 字符串的命令
        }
    }
}
```



集合：

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CZGL.RedisClient
{
    public class SetClient : CommandClient<SetClient>
    {
        internal SetClient() { }
        internal SetClient(RedisClient client) : base(client)
        {

        }

        public async Task<bool> SAdd(string key, string value)
        {
            await SendCommand($"{StringCommand.SADD} {key} {value}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }

        public async Task<string> SMembers(string key)
        {
            await SendCommand($"{StringCommand.SMEMBERS} {key}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return result;
        }


        private static class StringCommand
        {
            public const string SADD = "SADD";
            public const string SMEMBERS = "SMEMBERS";
            // ... ... 更多 字符串的命令
        }
    }
}
```



有序集合：

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CZGL.RedisClient
{
    public class SortedClient : CommandClient<SortedClient>
    {
        internal SortedClient(RedisClient client) : base(client)
        {

        }

        public async Task<bool> ZAdd(string key, string value)
        {
            await SendCommand($"{StringCommand.ZADD} {key} {value}", out MessageStraceAnalysis<string> strace);
            var result = await strace.Task;
            return IsOk(result);
        }

        private static class StringCommand
        {
            public const string ZADD = "ZADD";
            public const string SMEMBERS = "SMEMBERS";
            // ... ... 更多 字符串的命令
        }
    }
}
```



这样，我们就有一个具有简单功能的 RedisClient 框架了。



### 9，更多测试

为了验证功能是否可用，我们写一些示例：

```csharp
        static RedisClient client = new RedisClient("127.0.0.1", 6379);
        static async Task Main(string[] args)
        {
            var a = await client.ConnectAsync();
            if (!a)
            {
                Console.WriteLine("连接服务器失败");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("连接服务器成功");

            await StringSETGET();
            await StringGETRANGE();
            await StringGETSET();
            await StringMGet();
            Console.ReadKey();
        }

        static async Task StringSETGET()
        {
            var stringClient = client.GetStringClient();
            var b = await stringClient.Set("seta", "6666");
            var c = await stringClient.Get("seta");
            if (c == "6666")
            {
                Console.WriteLine("true");
            }
        }

        static async Task StringGETRANGE()
        {
            var stringClient = client.GetStringClient();
            var b = await stringClient.Set("getrance", "123456789");
            var c = await stringClient.GetRance("getrance", 0, -1);
            if (c == "123456789")
            {
                Console.WriteLine("true");
            }
            var d = await stringClient.GetRance("getrance", 0, 3);
            if (d == "1234")
            {
                Console.WriteLine("true");
            }
        }

        static async Task StringGETSET()
        {
            var stringClient = client.GetStringClient();
            var b = await stringClient.Set("getrance", "123456789");
            var c = await stringClient.GetSet("getrance", "987654321");
            if (c == "123456789")
            {
                Console.WriteLine("true");
            }
        }

        static async Task StringMGet()
        {
            var stringClient = client.GetStringClient();
            var a = await stringClient.Set("stra", "123456789");
            var b = await stringClient.Set("strb", "123456789");
            var c = await stringClient.Set("strc", "123456789");
            var d = await stringClient.MGet("stra", "strb", "strc");
            if (d.Where(x => x == "123456789").Count() == 3)
            {
                Console.WriteLine("true");
            }
        }
```



### 10，性能测试

因为只是写得比较简单，而且是单线程，并且内存比较浪费，我觉得性能会比较差。但真相如何呢？我们来测试一下：

```csharp
        static RedisClient client = new RedisClient("127.0.0.1", 6379);
        static async Task Main(string[] args)
        {
            var a = await client.ConnectAsync();
            if (!a)
            {
                Console.WriteLine("连接服务器失败");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("连接服务器成功");

            var stringClient = client.GetStringClient();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                _ = await stringClient.Set(guid, guid);
                _ = await stringClient.Get(guid);
            }

            watch.Stop();
            Console.WriteLine($"总共耗时：{watch.ElapsedMilliseconds/10} ms");
            Console.ReadKey();
        }
```

耗时：

```
总共耗时：1003 ms
```

大概就是 1s，3000 个 SET 和 3000 个 GET 共 6000 个请求。看来单线程性能也是很强的。

不知不觉快 11 点了，不写了，赶紧睡觉去了。



笔者其它 Redis 文章：

[搭建分布式 Redis Cluster 集群与 Redis 入门](https://www.cnblogs.com/whuanle/p/13837153.html)

[Redis 入门与 ASP.NET Core 缓存](https://www.cnblogs.com/whuanle/p/13843208.html)



### 11，关于 NCC

.NET Core Community （.NET 中心社区，简称 NCC）是一个基于并围绕着 .NET 技术栈展开组织和活动的非官方、非盈利性的民间开源社区。我们希望通过我们 NCC 社区的努力，与各个开源社区一道为 .NET 生态注入更多活力。

加入 NCC，里面一大把框架作者，教你写框架，参与开源项目，做出你的贡献。记得加入 NCC 哟~