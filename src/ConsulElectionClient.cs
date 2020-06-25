using System;
using System.Collections.Generic;
using System.Threading;
using Consul;

namespace FireflySoft.LeaderElection
{
    public class ConsulElectionClient
    {
        private readonly ConsulClient _client;

        /// <summary>
        /// The ttl pass thread.
        /// </summary>
        private Thread ttlPassThread;

        public ConsulElectionClient(ConsulClient _client)
        {
            this._client = _client;
        }

        #region Agent

        /// <summary>
        /// 更新服务健康检查的TTL
        /// </summary>
        /// <returns></returns>
        public void PassTTL(string serviceId)
        {
            Retry(() =>
            {
                var checkId = "CHECK:" + serviceId;
                _client.Agent.PassTTL(checkId, "Alive").Wait();
            }, 2);
        }

        /// <summary>
        /// 注销服务
        /// </summary>
        /// <param name="serviceId"></param>
        public bool DeregisterService(string serviceId)
        {
            var deRegResult = Retry(() =>
            {
                return _client.Agent.ServiceDeregister(serviceId).ConfigureAwait(false).GetAwaiter().GetResult();
            }, 2);

            if (deRegResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 注销健康检测
        /// </summary>
        /// <param name="checkId"></param>
        /// <returns></returns>
        public bool DeregisterServiceCheck(string checkId)
        {
            var deRegResult = Retry(() =>
            {
                return _client.Agent.CheckDeregister(checkId).ConfigureAwait(false).GetAwaiter().GetResult();
            }, 2);

            if (deRegResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 注册服务，使用此方法注册的服务需要定时Pass TTL
        /// </summary>
        /// <param name="serviceId"></param>
        /// <param name="serviceName"></param>
        /// <param name="ttl"></param>
        /// <returns></returns>
        public string RegisterService(string serviceId, string serviceName, int ttl)
        {
            var deRegResult = Retry(() =>
            {
                return _client.Agent.ServiceDeregister(serviceId).ConfigureAwait(false).GetAwaiter().GetResult();
            }, 2);

            if (deRegResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("程序注册前注销服务失败，返回值：" + deRegResult.StatusCode);
            }

            var regResult = Retry(() =>
            {
                return _client.Agent.ServiceRegister(new AgentServiceRegistration()
                {
                    ID = serviceId,
                    Name = serviceName
                }).ConfigureAwait(false).GetAwaiter().GetResult();

            }, 2);

            if (regResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("程序注册失败，返回值：" + regResult.StatusCode);
            }

            string checkId = "CHECK:" + serviceId;
            var regCheckResult = Retry(() =>
            {
                return _client.Agent.CheckRegister(new AgentCheckRegistration()
                {
                    ID = checkId,
                    Name = "CHECK " + serviceId,
                    DeregisterCriticalServiceAfter = new TimeSpan(1, 0, 0),
                    Notes = "程序 " + serviceId + " 健康监测",
                    ServiceID = serviceId,
                    Status = HealthStatus.Warning,
                    TTL = new TimeSpan(0, 0, ttl)
                }).ConfigureAwait(false).GetAwaiter().GetResult();
            }, 2);

            if (regCheckResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("程序健康检查注册失败，返回值：" + regCheckResult.StatusCode);
            }

            if (ttlPassThread == null)
            {
                ttlPassThread = new Thread(new ThreadStart(() =>
                {
                    var sleepTime = (ttl / 2 - 1) * 1000;

                    while (true)
                    {
                        try
                        {
                            PassTTL(serviceId);
                        }
                        catch
                        {
                        }

                        Thread.Sleep(sleepTime);
                    }
                }));
                ttlPassThread.IsBackground = true;

                ttlPassThread.Start();
            }

            return checkId;
        }
        #endregion

        #region KeyValue
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
        public KVPair BlockGet(string key, TimeSpan waitTime, ulong waitIndex, CancellationToken cancellationToken = default)
        {
            return Retry(() =>
            {
                return _client.KV.Get(key, new QueryOptions()
                {
                    WaitTime = waitTime,
                    WaitIndex = waitIndex
                }, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult().Response;
            }, 1);
        }

        /// <summary>
        /// 获取对应Key的字符串值
        /// </summary>
        /// <param name="kv"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool Acquire(KVPair kv, CancellationToken cancellationToken = default)
        {
            return Retry(() =>
            {
                return _client.KV.Acquire(kv, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult().Response;
            }, 1);
        }

        /// <summary>
        /// 获取对应Key的字符串值
        /// </summary>
        /// <returns>The get.</returns>
        /// <param name="key">Key.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public KVPair Get(string key, CancellationToken cancellationToken = default)
        {
            return Retry(() =>
            {
                return _client.KV.Get(key, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult().Response;
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
                return _client.KV.Delete(key, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult().Response;
            }, 2);
        }

        /// <summary>
        /// 创建Session
        /// </summary>
        /// <returns>The session.</returns>
        /// <param name="checkIds">Check identifier.</param>
        /// <param name="lockDelay">Lock delay.</param>
        public string CreateSession(List<string> checkIds, int lockDelay = 15)
        {
            return Retry(() =>
            {
                return _client.Session.Create(new SessionEntry()
                {
                    Checks = checkIds,
                    LockDelay = new TimeSpan(0, 0, lockDelay),

                }).ConfigureAwait(false).GetAwaiter().GetResult().Response;
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
                return _client.Session.Destroy(sessionId).ConfigureAwait(false).GetAwaiter().GetResult().Response;
            }, 2);
        }
        #endregion

        private void Retry(Action action, int retryTimes)
        {
            int i = retryTimes;
            while (i > 0)
            {
                try
                {
                    action();
                    break;
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
