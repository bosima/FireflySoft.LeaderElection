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
        /// <param name="consulClientConfigOverride"></param>
        public ConsulElectionOptions(Action<ConsulClientConfiguration> consulClientConfigOverride)
        {
            base.LeaderElectionType = EnumLeaderElectionType.Consul;
            if (consulClientConfigOverride == null)
            {
                ConsulClient = new ConsulClient();
            }
            else
            {
                ConsulClient= new ConsulClient(consulClientConfigOverride);
            }
        }

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
        /// 初始化Consul选举选项的一个新实例
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
    }
}
