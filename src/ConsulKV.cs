using Consul;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Consul KV相关
    /// </summary>
    internal class ConsulKV
    {
        private readonly ConsulClient client;

        public ConsulKV(ConsulClient client)
        {
            this.client = client;
        }

        /// <summary>
        /// 创建一个KVPair实例
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public KVPair Create(string key)
        {
            return new KVPair(key);
        }

        /// <summary>
        /// 阻塞获取对应Key的值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="waitTime"></param>
        /// <param name="waitIndex"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public KVPair BlockGet(string key, TimeSpan waitTime, ulong waitIndex, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Retry(() =>
            {
                return client.KV.Get(key, new QueryOptions()
                {
                    WaitTime = waitTime,
                    WaitIndex = waitIndex
                }, cancellationToken).Result.Response;
            }, 1);
        }

        /// <summary>
        /// 获取对应Key的字符串值
        /// </summary>
        /// <param name="kv"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool Acquire(KVPair kv, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Retry(() =>
            {
                return client.KV.Acquire(kv, cancellationToken).Result.Response;
            }, 1);
        }

        /// <summary>
        /// 获取对应Key的字符串值
        /// </summary>
        /// <returns>The get.</returns>
        /// <param name="key">Key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public KVPair Get(string key, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Retry(() =>
            {
                return client.KV.Get(key, cancellationToken).Result.Response;
            }, 2);
        }

        /// <summary>
        /// 删除对应Key
        /// </summary>
        /// <param name="key"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool Delete(string key, CancellationToken cancellationToken = default)
        {
            return Retry(() =>
            {
                return client.KV.Delete(key, cancellationToken).Result.Response;
            }, 2);
        }

        /// <summary>
        /// 创建Session
        /// </summary>
        /// <returns>The session.</returns>
        /// <param name="checkId">Check identifier.</param>
        /// <param name="lockDelay">Lock delay.</param>
        public string CreateSession(string checkId, int lockDelay = 15)
        {
            return Retry(() =>
            {
                return client.Session.Create(new SessionEntry()
                {
                    Checks = new List<string> { checkId },
                    LockDelay = new TimeSpan(0, 0, lockDelay),

                }).Result.Response;
            }, 2);
        }

        /// <summary>
        /// 移除Session
        /// </summary>
        /// <returns></returns>
        public bool RemoveSession(string sessionId)
        {
            return Retry(() =>
            {
                return client.Session.Destroy(sessionId).Result.Response;
            }, 2);
        }

        private T Retry<T>(Func<T> func, int retryTimes)
        {
            int i = retryTimes;
            while (i > 0)
            {
                try
                {
                    return func();
                }
                catch
                {
                    i--;

                    if (i == 0)
                    {
                        throw;
                    }

                    Thread.Sleep(1000);
                }
            }

            return default(T);
        }
    }
}
