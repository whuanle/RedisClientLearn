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
