using System;
using System.Collections.Generic;
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
        private TimeSpan _leaderOfflineConfirmInterval;
        private ConsulElectionOptions _options;
        private ConsulElectionClient _consulElectionClient;

        /// <summary>
        /// 注册到Leader选举
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="serviceId"></param>
        /// <param name="options"></param>
        public void Register(string serviceName, string serviceId, LeaderElectionOptions options)
        {
            _options = (ConsulElectionOptions)options;
            _leaderOfflineConfirmInterval = options.LeaderOfflineConfirmInterval;

            if (options.LeaderOfflineConfirmInterval.TotalSeconds > 0 &&
                options.LeaderOfflineConfirmInterval.TotalSeconds < 10)
            {
                options.LeaderOfflineConfirmInterval = TimeSpan.FromSeconds(10);
            }

            _consulElectionClient = new ConsulElectionClient(_options.ConsulClient);

            _electionKey = string.Format("leader-election/{0}/leader", serviceName);
            _serviceId = serviceId;
            _sessionCheckId = _consulElectionClient.RegisterService(_leaderElectionServicePrefix + serviceId, _leaderElectionServicePrefix + serviceName, 10);
        }

        /// <summary>
        /// 发起选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public LeaderElectionResult Elect(CancellationToken cancellationToken = default)
        {
            try
            {
                bool lockResult = false;
                string currentLeaderId = string.Empty;
                bool isCurrentLeaderOnline = false;

                // 创建一个关联到当前节点的Session
                if (!string.IsNullOrWhiteSpace(_sessionId))
                {
                    _consulElectionClient.RemoveSession(_sessionId);
                }
                _sessionId = _consulElectionClient.CreateSession(new List<string> { _sessionCheckId, "serfHealth" }, _options.ReElectionSilencePeriod);

                // 使用这个Session尝试去锁定选举KV
                var kv = _consulElectionClient.Get(_electionKey, cancellationToken);
                if (kv == null)
                {
                    kv = _consulElectionClient.Create(_electionKey);
                }

                if (string.IsNullOrWhiteSpace(kv.Session))
                {
                    kv.Session = _sessionId;
                    kv.Value = Encoding.UTF8.GetBytes(_serviceId);
                    lockResult = _consulElectionClient.Acquire(kv, cancellationToken);
                }

                // 无论参选成功与否，获取当前的Leader
                var leaderKV = _consulElectionClient.Get(_electionKey, cancellationToken);
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
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                // 这里认为就是操作Consul的异常
                return new LeaderElectionResult()
                {
                    State = new LeaderElectionState()
                    {
                        IsDisconnected = true
                    },
                    IsSuccess = false,
                };
            }
        }

        /// <summary>
        /// 清空选举状态，服务所有部署重新开始选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool Reset(CancellationToken cancellationToken = default)
        {
            return _consulElectionClient.Delete(_electionKey, cancellationToken);
        }

        /// <summary>
        /// 观察选举状态，直到状态变化或者超时
        /// </summary>
        /// <param name="state">上一次获取的状态，作为实时状态的对比参照，如果两个状态不同则说明选举状态发生了变化</param>
        /// <param name="processLatestState">收到最新状态后的处理方法，可能是状态变化或者超时触发此操作</param>
        /// <param name="cancellationToken"></param>
        public void WatchState(LeaderElectionState state, Action<LeaderElectionState> processLatestState, CancellationToken cancellationToken = default)
        {
            KVPair consulKv = state != null ? (KVPair)state.Data : null;
            ulong waitIndex = consulKv == null ? 0 : consulKv.ModifyIndex++;
            TimeSpan waitTime = state.IsLeaderOnline ? _defaultWatchInterval : _leaderOfflineConfirmInterval;
            KVPair newConsulKv = null;
            bool isDisconnected = false;

            try
            {
                if (waitTime.TotalSeconds <= 0)
                {
                    newConsulKv = _consulElectionClient.Get(_electionKey, cancellationToken);
                }
                else
                {
                    newConsulKv = _consulElectionClient.BlockGet(_electionKey, waitTime, waitIndex, cancellationToken);
                }
            }
            catch
            {
                // consul不可用或连接不上
                isDisconnected = true;
            }

            LeaderElectionState newState = new LeaderElectionState()
            {
                IsDisconnected = isDisconnected
            };

            // 有选举数据
            if (newConsulKv != null)
            {
                newState.Data = newConsulKv;
                newState.CurrentLeaderId = Encoding.UTF8.GetString(newConsulKv.Value);
                newState.IsLeaderOnline = !string.IsNullOrWhiteSpace(newConsulKv.Session);
            }

            processLatestState?.Invoke(newState);
        }
    }
}
