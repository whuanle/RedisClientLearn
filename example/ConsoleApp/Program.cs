using CZGL.RedisClient;
using System;
using System.Diagnostics;
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
            Console.WriteLine($"总共耗时：{watch.ElapsedMilliseconds} ms");
            Console.ReadKey();
        }
    }
}
