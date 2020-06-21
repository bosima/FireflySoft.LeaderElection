using System;
namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举类型
    /// </summary>
    public enum EnumLeaderElectionType
    {
        /// <summary>
        /// 基于Consul
        /// </summary>
        Consul = 0,

        /// <summary>
        /// 基于ZooKeeper
        /// </summary>
        ZooKeeper = 1,
    }
}
