using System;
using Consul;

namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Consul选举选项
    /// </summary>
    public class ConsulElectionOptions : LeaderElectionOptions
    {
        /// <summary>
        /// 初始化Consul选举选项的一个新实例
        /// </summary>
        /// <param name="consulClient"></param>
        public ConsulElectionOptions(ConsulClient consulClient)
        {
            base.LeaderElectionType = EnumLeaderElectionType.Consul;
            ConsulClient = consulClient;
        }

        /// <summary>
        /// 初始化Consul选举选项的一个新实例，默认连接本机Consul Client。
        /// </summary>
        public ConsulElectionOptions()
        {
            base.LeaderElectionType = EnumLeaderElectionType.Consul;
            ConsulClient = new ConsulClient();
        }

        /// <summary>
        /// 获取当前ConsulCleint
        /// </summary>
        public ConsulClient ConsulClient { get; private set; }

        /// <summary>
        /// 重新选举沉默期：Leader下线后，集群可以重新选举成功的等待时间，单位秒，默认10s
        /// </summary>
        public int ReElectionSilencePeriod { get; set; } = 10;
    }
}
