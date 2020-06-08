using System;
using System.Threading;

namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举管理者
    /// </summary>
    public class LeaderElectionManager
    {
        private readonly ILeaderElection _election;
        private readonly string _currentServiceId;
        private const int _defaultOfflineConfirmAmount = 3;


        /// <summary>
        /// 初始化一个新的Leader选举管理器
        /// </summary>
        /// <param name="serviceName">服务名，参与Leader选举的多个程序应该使用相同的服务名</param>
        /// <param name="serviceId">服务Id，参与Leader选举的每个程序应该有唯一的服务Id</param>
        /// <param name="options"></param>
        public LeaderElectionManager(string serviceName, string serviceId, LeaderElectionOptions options)
        {
            _currentServiceId = serviceId;
            _election = new LeaderElectionFactory().Create(options);
            _election.Register(serviceName, serviceId, options);
        }

        /// <summary>
        /// 持续观察Leader状态，并在合适的时机发起选举
        /// </summary>
        /// <param name="leaderElectCompletedHandler">leader选举完毕处理方法</param>
        /// <param name="cancellationToken"></param>
        public void Watch(Action<LeaderElectionResult> leaderElectCompletedHandler, CancellationToken cancellationToken = default)
        {
            // 上来就先选举一次，以获取要监控的状态
            var electState = Elect(leaderElectCompletedHandler, cancellationToken);
            var offlineConfirmAmount = _defaultOfflineConfirmAmount;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _election.WatchState(electState, newState =>
                    {
                        // 为空代表全局选举状态不存在或者被移除，则马上启动选举
                        if (newState == null)
                        {
                            electState = Elect(leaderElectCompletedHandler, cancellationToken);
                            return;
                        }

                        // Leader下线处理
                        if (!newState.IsLeaderOnline)
                        {
                            // 下线的Leader有优先选举权
                            if (newState.CurrentLeaderId == _currentServiceId)
                            {
                                electState = Elect(leaderElectCompletedHandler, cancellationToken);
                                return;
                            }

                            // 其它节点需要确认Leader真的下线了才能发起选举
                            if (offlineConfirmAmount == 0)
                            {
                                electState = Elect(leaderElectCompletedHandler, cancellationToken);

                                // 有选举出新的Leader才需要重新确认
                                if (electState != null && electState.IsLeaderOnline)
                                {
                                    offlineConfirmAmount = _defaultOfflineConfirmAmount;
                                }
                                return;
                            }

                            offlineConfirmAmount--;

                            return;
                        }

                        offlineConfirmAmount = _defaultOfflineConfirmAmount;

                    }, cancellationToken);
                }
                catch
                {
                    Thread.Sleep(3000);
                }
            }
            while (true);
        }

        /// <summary>
        /// 重设Leader选举：清空已有选举状态，马上发起一次新的选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        public void Reset(CancellationToken cancellationToken = default)
        {
            _election.Reset(cancellationToken);
        }

        private LeaderElectionState Elect(Action<LeaderElectionResult> leaderElectCompletedHandler, CancellationToken cancellationToken)
        {
            var electResult = _election.Elect(cancellationToken);
            var electState = electResult.State;
            leaderElectCompletedHandler?.Invoke(electResult);
            return electState;
        }
    }
}
