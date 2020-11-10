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
