using System;
namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举结果
    /// </summary>
    public class LeaderElectionResult
    {
        /// <summary>
        /// 是否选举成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 选举状态
        /// </summary>
        public LeaderElectionState State { get; set; }
    }
}
