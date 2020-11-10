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
