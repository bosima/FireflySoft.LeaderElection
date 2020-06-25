using System;
namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举状态
    /// </summary>
    public class LeaderElectionState
    {
        /// <summary>
        /// 选举状态数据
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Leader是否在线
        /// </summary>
        public bool IsLeaderOnline { get; set; }

        /// <summary>
        /// 当前LeaderId
        /// </summary>
        public string CurrentLeaderId { get; set; }

        /// <summary>
        /// 当前节点是否断开连接，默认false
        /// </summary>
        public bool IsDisconnected { get; set; }
    }
}
