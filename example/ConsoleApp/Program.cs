using CZGL.RedisClient;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using NewLife;
using System.Collections.Generic;

namespace ConsoleApp
{
    class Program
    {
        static RedisClient client = new RedisClient("192.168.0.126", 9999);
        static ConnectionMultiplexer seRedis = ConnectionMultiplexer.Connect("192.168.0.126:9999");
        static FreeRedis.RedisClient freeClient = new FreeRedis.RedisClient("192.168.0.126:9999");
        static IDatabase sedb => seRedis.GetDatabase();
        static NewLife.Caching.Redis ic = new NewLife.Caching.FullRedis();
        static async Task Main(string[] args)
        {
            var a = await client.ConnectAsync();
            //await ThreadOnly();
            Console.WriteLine("---------------");
            await TaskWall();
            Console.WriteLine("---------------");
            ParallelVoid();
            Console.ReadKey();
        }


        public static async Task ThreadOnly()
        {
            var a = await client.ConnectAsync();
            if (!a)
            {
                Console.WriteLine("连接服务器失败");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("连接服务器成功");
            Console.WriteLine("单线程测试，3000 个请求/ 3000 接收");

            var stringClient = client.GetStringClient();
            Stopwatch watch = new Stopwatch();
            watch.Start();
            int errorNum = 0;
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                _ = await stringClient.Set(guid, guid);
                var tmp = await stringClient.Get(guid);
                if (tmp != guid)
                    errorNum += 1;
            }

            watch.Stop();
            Console.WriteLine($"CZGL.RedisClient 总共耗时：{watch.ElapsedMilliseconds} ms，错误次数 {errorNum}");

            errorNum = 0;
            sedb.SetAdd("testetst", "abcd");

            watch.Reset();
            watch.Start();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                _ = sedb.StringSet(guid, guid);
                var tmp = sedb.StringGet(guid);
                if (tmp != guid)
                    errorNum += 1;
            }

            watch.Stop();
            Console.WriteLine($"StackExchange.Redis 总共耗时：{watch.ElapsedMilliseconds} ms，错误次数 {errorNum}");

            errorNum = 0;

            await freeClient.SAddAsync("testetetstets", "testtest");
            watch.Reset();
            watch.Start();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                await freeClient.SetAsync(guid, guid);
                var tmp = await freeClient.GetAsync(guid);
                if (tmp != guid)
                    errorNum += 1;
            }

            watch.Stop();
            Console.WriteLine($"FreeRedis 总共耗时：{watch.ElapsedMilliseconds} ms，错误次数 {errorNum}");

            errorNum = 0;
            ic.Server = "192.168.0.126:9999";

            watch.Reset();
            watch.Start();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                ic.Set(guid, guid);
                var tmp = ic.Get<string>(guid);
                if (tmp != guid)
                    errorNum += 1;
            }

            watch.Stop();
            Console.WriteLine($"NewLife.Redis 总共耗时：{watch.ElapsedMilliseconds} ms，错误次数 {errorNum}");
        }

        /// <summary>
        /// Task 并发
        /// </summary>
        /// <returns></returns>
        public static async Task TaskWall()
        {
            Console.WriteLine("Task.WaitAll 并发测试，3000 个请求/ 3000 接收");

            var stringClient = client.GetStringClient();
            Stopwatch watch = new Stopwatch();
            watch.Start();

            List<Task> task1_r = new List<Task>();
            List<Task> task1_sp = new List<Task>();
            var guid1 = Enumerable.Range(1, 3000).Select(x => Guid.NewGuid().ToString()).ToArray();
            foreach (var guid in guid1)
            {
                task1_r.Add(Task.Run(async () =>
                {
                    await stringClient.Set(guid, guid);
                }));
            }

            await Task.WhenAll(task1_r.ToArray());
            foreach (var guid in guid1)
            {
                task1_sp.Add(Task.Run(async () =>
                {
                    await stringClient.Get(guid);
                }));
            }

            await Task.WhenAll(task1_sp.ToArray());

            watch.Stop();
            Console.WriteLine($"CZGL.RedisClient 总共耗时：{watch.ElapsedMilliseconds} ms");


            sedb.SetAdd("testetst", "abcd");

            watch.Reset();
            watch.Start();
            List<Task> task2_r = new List<Task>();
            List<Task> task2_sp = new List<Task>();
            var guid2 = Enumerable.Range(1, 3000).Select(x => Guid.NewGuid().ToString()).ToArray();
            foreach (var guid in guid2)
            {
                task2_r.Add(Task.Run(() => { sedb.StringSet(guid, guid); }));
            }
            await Task.WhenAll(task2_r.ToArray());

            foreach (var guid in guid2)
            {
                task2_sp.Add(Task.Run(() => { sedb.StringGet(guid); }));
            }
            await Task.WhenAll(task2_sp.ToArray());
            watch.Stop();
            Console.WriteLine($"StackExchange.Redis 总共耗时：{watch.ElapsedMilliseconds} ms");


            await freeClient.SAddAsync("testetetstets", "testtest");
            watch.Reset();
            watch.Start();

            List<Task> task3_r = new List<Task>();
            List<Task> task3_sp = new List<Task>();
            var guid3 = Enumerable.Range(1, 3000).Select(x => Guid.NewGuid().ToString()).ToArray();
            foreach (var guid in guid3)
            {
                task3_r.Add(Task.Run(async () =>
                {
                    await freeClient.SetAsync(guid, guid);
                }));
            }

            await Task.WhenAll(task3_r.ToArray());
            foreach (var guid in guid3)
            {
                task3_sp.Add(Task.Run(async () =>
                {
                    await freeClient.GetAsync(guid);
                }));
            }
            await Task.WhenAll(task3_sp.ToArray());
            watch.Stop();
            Console.WriteLine($"FreeRedis 总共耗时：{watch.ElapsedMilliseconds} ms");

            ic.Server = "192.168.0.126:9999";

            watch.Reset();
            watch.Start();
            List<Task> task4_r = new List<Task>();
            List<Task> task4_sp = new List<Task>();
            var guid4 = Enumerable.Range(1, 3000).Select(x => Guid.NewGuid().ToString()).ToArray();
            foreach (var guid in guid4)
            {
                task4_r.Add(Task.Run(() => { ic.Set(guid, guid); }));
            }
            await Task.WhenAll(task4_r.ToArray());
            foreach (var guid in guid4)
            {
                task4_sp.Add(Task.Run(() => { ic.Get<string>(guid); }));
            }
            await Task.WhenAll(task4_sp.ToArray());
            watch.Stop();
            Console.WriteLine($"NewLife.Redis 总共耗时：{watch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// Parallel 并发
        /// </summary>
        public static void ParallelVoid()
        {
            Console.WriteLine("Parallel 并发测试，3000 个请求/ 3000 接收");

            var stringClient = client.GetStringClient();
            Stopwatch watch = new Stopwatch();
            watch.Start();

            List<string> guid1 = new List<string>();
            List<Action> action1_r = new List<Action>();
            List<Action> action1_sp = new List<Action>();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                guid1.Add(guid);
                action1_r.Add(async () => { await stringClient.Set(guid, guid); });
            }

            Parallel.Invoke(action1_r.ToArray());
            for (int i = 0; i < 3000; i++)
            {
                action1_sp.Add(async () => { await stringClient.Get(guid1[i]); });
            }

            Parallel.Invoke(action1_sp.ToArray());

            watch.Stop();
            Console.WriteLine($"CZGL.RedisClient 总共耗时：{watch.ElapsedMilliseconds} ms");



            watch.Reset();
            watch.Start();
            List<Action> action2_r = new List<Action>();
            List<Action> action2_sp = new List<Action>();
            List<string> guid2 = new List<string>();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                guid2.Add(guid);
                action2_r.Add(() => { sedb.StringSet(guid, guid); });
            }

            Parallel.Invoke(action2_r.ToArray());

            for (int i = 0; i < 3000; i++)
            {
                action2_sp.Add(() => { sedb.StringGet(guid2[i]); });
            }
            Parallel.Invoke(action2_sp.ToArray());
            watch.Stop();
            Console.WriteLine($"StackExchange.Redis 总共耗时：{watch.ElapsedMilliseconds} ms");



            watch.Reset();
            watch.Start();

            List<string> guid3 = new List<string>();
            List<Action> action3_r = new List<Action>();
            List<Action> action3_sp = new List<Action>();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                guid3.Add(guid);
                action3_r.Add(async () => { await freeClient.SetAsync(guid, guid); });
            }

            Parallel.Invoke(action3_r.ToArray());

            for (int i = 0; i < 3000; i++)
            {
                action3_sp.Add(async () => { await freeClient.GetAsync(guid3[i]); });
            }
            Parallel.Invoke(action3_sp.ToArray());
            watch.Stop();
            Console.WriteLine($"FreeRedis 总共耗时：{watch.ElapsedMilliseconds} ms");



            watch.Reset();
            watch.Start();

            List<string> guid4 = new List<string>();
            List<Action> action4_r = new List<Action>();
            List<Action> action4_sp = new List<Action>();
            for (int i = 0; i < 3000; i++)
            {
                var guid = Guid.NewGuid().ToString();
                guid4.Add(guid);
                action4_r.Add(() => { ic.Set(guid, guid); });
            }

            Parallel.Invoke(action4_r.ToArray());

            for (int i = 0; i < 3000; i++)
            {
                action4_sp.Add(() => { ic.Get<string>(guid4[i]); });
            }
            Parallel.Invoke(action4_sp.ToArray());
            watch.Stop();
            Console.WriteLine($"NewLife.Redis 总共耗时：{watch.ElapsedMilliseconds} ms");
        }
    }
}
