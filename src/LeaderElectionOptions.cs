using System;
namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举选项
    /// </summary>
    public abstract class LeaderElectionOptions
    {
        /// <summary>
        /// 初始化Leader选举选项的一个新实例
        /// </summary>
        public LeaderElectionOptions()
        {
            ReElectionSilencePeriod = 10;
            LeaderElectionType = EnumLeaderElectionType.Consul;
            IsSelfElect = true;
        }

        /// <summary>
        /// Leader选举类型，默认Consul
        /// </summary>
        public EnumLeaderElectionType LeaderElectionType { get; set; }

        /// <summary>
        /// 重新选举沉默期：Leader状态丢失后，集群可以重新选举成功的等待时间，单位秒，默认10s
        /// </summary>
        public int ReElectionSilencePeriod { get; set; }

        /// <summary>
        /// 是否自选举：集成FireflySoft.LeaderElection的程序是否参与选举
        /// </summary>
        public bool IsSelfElect { get; set; }
    }
}
