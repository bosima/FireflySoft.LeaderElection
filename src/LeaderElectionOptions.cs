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
            LeaderElectionType = EnumLeaderElectionType.Consul;
            IsSelfElect = true;
        }

        /// <summary>
        /// Leader选举类型，默认Consul
        /// </summary>
        public EnumLeaderElectionType LeaderElectionType { get; set; }

        /// <summary>
        /// 是否自选举：集成FireflySoft.LeaderElection的程序是否参与选举。
        /// 目前没有使用这个属性，全部是自选举。
        /// </summary>
        public bool IsSelfElect { get; set; }
    }
}
