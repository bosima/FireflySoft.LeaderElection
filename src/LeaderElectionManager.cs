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
        private int _offlineConfirmAmount;
        private LeaderElectionState _electState;

        /// <summary>
        /// 初始化一个新的Leader选举管理器
        /// </summary>
        /// <param name="serviceName">服务名，参与Leader选举的多个程序应该使用相同的服务名</param>
        /// <param name="serviceId">服务Id，参与Leader选举的每个程序应该有唯一的服务Id</param>
        /// <param name="options"></param>
        public LeaderElectionManager(string serviceName, string serviceId, LeaderElectionOptions options)
        {
            _offlineConfirmAmount = _defaultOfflineConfirmAmount;
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
            _electState = Elect(leaderElectCompletedHandler, cancellationToken);

            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    _election.WatchState(_electState, newState =>
                    {
                        _electState = newState;
                        ProcessState(leaderElectCompletedHandler, newState, cancellationToken);
                    }, cancellationToken);
                }
                catch
                {
                    cancellationToken.ThrowIfCancellationRequested();
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

        /// <summary>
        /// 获取当前服务集群Leader选举状态
        /// </summary>
        public LeaderElectionState GetCurrentState()
        {
            return _electState;
        }

        /// <summary>
        /// 处理选举状态
        /// </summary>
        /// <param name="leaderElectCompletedHandler"></param>
        /// <param name="newState"></param>
        /// <param name="cancellationToken"></param>
        private void ProcessState(Action<LeaderElectionResult> leaderElectCompletedHandler, LeaderElectionState newState, CancellationToken cancellationToken)
        {
            // 为空代表全局选举状态不存在或者被移除，则马上启动选举
            if (newState == null)
            {
                _electState = Elect(leaderElectCompletedHandler, cancellationToken);
                return;
            }

            // Leader下线处理
            if (!newState.IsLeaderOnline)
            {
                // 下线的Leader有优先选举权
                if (newState.CurrentLeaderId == _currentServiceId)
                {
                    _electState = Elect(leaderElectCompletedHandler, cancellationToken);
                    return;
                }

                // 其它节点需要确认Leader真的下线了才能发起选举
                // 真的下线通过连续多次监听到的状态为下线进行认定
                if (_offlineConfirmAmount == 0)
                {
                    _electState = Elect(leaderElectCompletedHandler, cancellationToken);

                    // 有选举出新的Leader才需要重新确认
                    if (_electState != null && _electState.IsLeaderOnline)
                    {
                        _offlineConfirmAmount = _defaultOfflineConfirmAmount;
                    }
                    return;
                }

                _offlineConfirmAmount--;

                return;
            }

            _offlineConfirmAmount = _defaultOfflineConfirmAmount;
        }

        /// <summary>
        /// 发起一次选举，并触发选举完成处理程序
        /// </summary>
        /// <param name="leaderElectCompletedHandler"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private LeaderElectionState Elect(Action<LeaderElectionResult> leaderElectCompletedHandler, CancellationToken cancellationToken)
        {
            var electResult = _election.Elect(cancellationToken);
            var electState = electResult.State;
            leaderElectCompletedHandler?.Invoke(electResult);
            return electState;
        }
    }
}
