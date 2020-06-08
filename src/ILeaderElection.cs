using System;
using System.Threading;

namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举操作接口
    /// </summary>
    public interface ILeaderElection
    {
        /// <summary>
        /// 注册到Leader选举中
        /// </summary>
        /// <param name="serviceName">服务名，参与Leader选举的多个程序应该使用相同的服务名</param>
        /// <param name="serviceId">服务Id，参与Leader选举的每个程序应该有唯一的服务Id</param>
        /// <param name="options">Leader选举选项</param>
        void Register(string serviceName,string serviceId, LeaderElectionOptions options);

        /// <summary>
        /// 发起选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        LeaderElectionResult Elect(CancellationToken cancellationToken);

        /// <summary>
        /// 重新选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        bool Reset(CancellationToken cancellationToken);

        /// <summary>
        /// 当前程序观察选举状态，直到状态变化或者超时
        /// </summary>
        /// <param name="stableState">上一次获取的状态，作为实时状态的对比参照，如果两个状态不同则说明选举状态发生了变化</param>
        /// <param name="processLatestState">收到最新状态后的处理方法，可能是状态变化或者超时触发此操作</param>
        /// <param name="cancellationToken"></param>
        void WatchState(LeaderElectionState stableState, Action<LeaderElectionState> processLatestState, CancellationToken cancellationToken);
    }
}
