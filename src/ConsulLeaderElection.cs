using System;
using System.Text;
using System.Threading;
using Consul;

namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// 基于Consul的Leader选举实现类
    /// </summary>
    public class ConsulLeaderElection : ILeaderElection
    {
        private string _electionKey = string.Empty;
        private string _sessionId = string.Empty;
        private string _sessionCheckId = string.Empty;
        private string _serviceId = string.Empty;
        private string _leaderElectionServicePrefix = "le:";
        private readonly TimeSpan _defaultWatchInterval = new TimeSpan(0, 0, 60);
        private readonly TimeSpan _confirmWatchInterval = new TimeSpan(0, 0, 10);
        private LeaderElectionOptions _options;

        /// <summary>
        /// 注册到Leader选举中
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="serviceId"></param>
        /// <param name="options"></param>
        public void Register(string serviceName, string serviceId, LeaderElectionOptions options)
        {
            _options = options;
            _electionKey = string.Format("leader-election/{0}/leader", serviceName);
            _serviceId = serviceId;
            _sessionCheckId = ConsulService.RegisterService(_leaderElectionServicePrefix + serviceId, _leaderElectionServicePrefix + serviceName, 10);
        }

        /// <summary>
        /// 发起选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public LeaderElectionResult Elect(CancellationToken cancellationToken = default)
        {
            bool lockResult = false;
            string currentLeaderId = string.Empty;
            bool isCurrentLeaderOnline = false;

            // 创建一个关联到当前节点的Session
            if (!string.IsNullOrWhiteSpace(_sessionId))
            {
                ConsulKV.RemoveSession(_sessionId);
            }
            _sessionId = ConsulKV.CreateSession(_sessionCheckId, _options.ReElectionSilencePeriod);

            // 使用这个Session尝试去锁定选举KV
            var kv = ConsulKV.Get(_electionKey, cancellationToken);
            if (kv == null)
            {
                kv = ConsulKV.Create(_electionKey);
            }

            if (string.IsNullOrWhiteSpace(kv.Session))
            {
                kv.Session = _sessionId;
                kv.Value = Encoding.UTF8.GetBytes(_serviceId);
                lockResult = ConsulKV.Acquire(kv, cancellationToken);
            }

            // 无论参选成功与否，获取当前的Leader
            var leaderKV = ConsulKV.Get(_electionKey, cancellationToken);
            if (leaderKV != null)
            {
                currentLeaderId = Encoding.UTF8.GetString(leaderKV.Value);
                isCurrentLeaderOnline = !string.IsNullOrWhiteSpace(leaderKV.Session);
            }

            return new LeaderElectionResult()
            {
                State = new LeaderElectionState()
                {
                    Data = leaderKV,
                    CurrentLeaderId = currentLeaderId,
                    IsLeaderOnline = isCurrentLeaderOnline,
                },
                IsSuccess = lockResult,
            };
        }

        /// <summary>
        /// 清空选举状态，服务所有部署重新开始选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool Reset(CancellationToken cancellationToken = default)
        {
            return ConsulKV.Delete(_electionKey, cancellationToken);
        }

        /// <summary>
        /// 观察选举状态，直到状态变化或者超时
        /// </summary>
        /// <param name="state">上一次获取的状态，作为实时状态的对比参照，如果两个状态不同则说明选举状态发生了变化</param>
        /// <param name="processLatestState">收到最新状态后的处理方法，可能是状态变化或者超时触发此操作</param>
        /// <param name="cancellationToken"></param>
        public void WatchState(LeaderElectionState state, Action<LeaderElectionState> processLatestState, CancellationToken cancellationToken = default)
        {
            KVPair consulKv = (KVPair)state.Data;
            ulong waitIndex = consulKv == null ? 0 : consulKv.ModifyIndex++;
            TimeSpan waitTime = state.IsLeaderOnline ? _defaultWatchInterval : _confirmWatchInterval;
            var newConsulKv = ConsulKV.BlockGet(_electionKey, waitTime, waitIndex, cancellationToken);

            LeaderElectionState newState = null;
            if (newConsulKv != null)
            {
                newState = new LeaderElectionState
                {
                    Data = newConsulKv,
                    CurrentLeaderId = Encoding.UTF8.GetString(newConsulKv.Value),
                    IsLeaderOnline = !string.IsNullOrWhiteSpace(newConsulKv.Session)
                };
            }
            processLatestState?.Invoke(newState);
        }
    }
}
