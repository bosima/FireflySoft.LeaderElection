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
        }

        /// <summary>
        /// Leader选举类型，默认Consul
        /// </summary>
        public EnumLeaderElectionType LeaderElectionType { get; set; } = EnumLeaderElectionType.Consul;

        /// <summary>
        /// Leader下线后，其它节点发起选举前确认原Leader不会再上线的次数
        /// </summary>
        public byte LeaderOfflineConfirmNumber { get; set; } = 3;

        /// <summary>
        /// Leader下线后，其它节点发起选举前每次确认原Leader不会再上线的时间间隔。
        /// 由于实现方式，在基于Consul的选举中如果设置了大于0且小于10的值，则自动替换为10。
        /// </summary>
        public TimeSpan LeaderOfflineConfirmInterval { get; set; } = TimeSpan.FromSeconds(10);
    }
}
