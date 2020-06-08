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
    /// Consul服务管理相关
    /// </summary>
    internal static class ConsulService
    {
        private static ConsulClient client = new ConsulClient();

        /// <summary>
        /// 更新服务健康检查的TTL
        /// </summary>
        /// <returns></returns>
        public static void PassTTL(string serviceId)
        {
            Retry(() =>
            {
                var checkId = "CHECK:" + serviceId;
                client.Agent.PassTTL(checkId, "Alive").Wait();
            }, 2);
        }

        /// <summary>
        /// 注销服务
        /// </summary>
        /// <param name="serviceId"></param>
        public static bool DeregisterService(string serviceId)
        {
            var deRegResult = Retry(() =>
            {
                return client.Agent.ServiceDeregister(serviceId).Result;
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
        public static bool DeregisterServiceCheck(string checkId)
        {
            var deRegResult = Retry(() =>
            {
                return client.Agent.CheckDeregister(checkId).Result;
            }, 2);

            if (deRegResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The ttl pass thread.
        /// </summary>
        private static Thread ttlPassThread;

        /// <summary>
        /// 注册服务，使用此方法注册的服务需要定时Pass TTL
        /// </summary>
        /// <param name="serviceId"></param>
        /// <param name="serviceName"></param>
        /// <param name="ttl"></param>
        /// <returns></returns>
        public static string RegisterService(string serviceId, string serviceName, int ttl)
        {
            var deRegResult = Retry(() =>
            {
                return client.Agent.ServiceDeregister(serviceId).Result;
            }, 2);

            if (deRegResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("程序注册前注销服务失败，返回值：" + deRegResult.StatusCode);
            }

            var regResult = Retry(() =>
            {
                return client.Agent.ServiceRegister(new AgentServiceRegistration()
                {
                    ID = serviceId,
                    Name = serviceName
                }).Result;

            }, 2);

            if (regResult.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception("程序注册失败，返回值：" + regResult.StatusCode);
            }

            string checkId = "CHECK:" + serviceId;
            var regCheckResult = Retry(() =>
            {
                return client.Agent.CheckRegister(new AgentCheckRegistration()
                {
                    ID = checkId,
                    Name = "CHECK " + serviceId,
                    DeregisterCriticalServiceAfter = new TimeSpan(1, 0, 0),
                    Notes = "程序 " + serviceId + " 健康监测",
                    ServiceID = serviceId,
                    Status = HealthStatus.Warning,
                    TTL = new TimeSpan(0, 0, ttl)
                }).Result;
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

        private static void Retry(Action action, int retryTimes)
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

        private static T Retry<T>(Func<T> func, int retryTimes)
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
