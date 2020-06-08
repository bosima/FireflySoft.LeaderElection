using System;
namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举设置项
    /// </summary>
    public class LeaderElectionOptions
    {
        /// <summary>
        /// Leader选举类型：0 Consul（默认）
        /// </summary>
        public int LeaderElectionType { get; set; }

        /// <summary>
        /// 重新选举沉默期：Leader状态丢失后，集群可以重新选举成功的等待时间，单位秒，默认15s
        /// </summary>
        public int ReElectionSilencePeriod { get; set; } = 15;
    }
}
