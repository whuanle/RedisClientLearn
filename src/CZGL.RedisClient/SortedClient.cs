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
