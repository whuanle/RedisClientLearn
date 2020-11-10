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
