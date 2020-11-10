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
