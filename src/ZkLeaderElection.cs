using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using static org.apache.zookeeper.KeeperException;
using static org.apache.zookeeper.Watcher.Event;
using static org.apache.zookeeper.ZooDefs;

namespace FireflySoft.LeaderElection
{
    public class ZkLeaderElection : ILeaderElection
    {
        private string _electionRootPath = string.Empty;
        private string _electionFlag = string.Empty;
        private string _electionLeader = string.Empty;
        private string _serviceId = string.Empty;
        private TimeSpan _leaderOfflineConfirmInterval;
        private bool _isDisconnected;

        private ZkElectionOptions _options;
        private ZkElectionClient _zkElectionClient;
        private ZkElectionPathWatcher _electionWatcher;
        private AutoResetEvent _electionDataChangedEvent;

        public ZkLeaderElection()
        {
        }

        /// <summary>
        /// 注册到Leader选举
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="serviceId"></param>
        /// <param name="options"></param>
        public void Register(string serviceName, string serviceId, LeaderElectionOptions options)
        {
            _options = (ZkElectionOptions)options;

            var watcher = new ZkElectionClientWatcher(ProcessZkConnectEvent);
            _zkElectionClient = new ZkElectionClient(_options.ConnectionString, _options.SessionTimeout, watcher);

            _leaderOfflineConfirmInterval = _options.LeaderOfflineConfirmInterval;
            _electionRootPath = string.Format("/leader-election/{0}", serviceName);
            _electionFlag = string.Concat(new string[] { _electionRootPath, "/", "flag" });
            _electionLeader = string.Concat(new string[] { _electionRootPath, "/", "leader" });
            _serviceId = serviceId;
            _electionDataChangedEvent = new AutoResetEvent(false);
            CreateElectionRootPath(_electionRootPath);
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
                // 已经存在选举结果
                string currentLeaderId = _zkElectionClient.GetData(_electionFlag);
                if (currentLeaderId != null)
                {
                    return new LeaderElectionResult()
                    {
                        IsSuccess = false,
                        State = new LeaderElectionState()
                        {
                            CurrentLeaderId = currentLeaderId,
                            Data = currentLeaderId,
                            IsLeaderOnline = true
                        }
                    };
                }

                // 发起选举并选举成功的处理
                currentLeaderId = _serviceId;
                if (_zkElectionClient.Create(_electionFlag, currentLeaderId, CreateMode.EPHEMERAL))
                {
                    if (!_zkElectionClient.Create(_electionLeader, currentLeaderId, CreateMode.PERSISTENT))
                    {
                        _zkElectionClient.Update(_electionLeader, currentLeaderId);
                    }
                    return new LeaderElectionResult()
                    {
                        IsSuccess = true,
                        State = new LeaderElectionState()
                        {
                            CurrentLeaderId = currentLeaderId,
                            Data = currentLeaderId,
                            IsLeaderOnline = true
                        }
                    };
                }

                // 选举失败的处理
                currentLeaderId = _zkElectionClient.GetData(_electionFlag);
                if (currentLeaderId != null)
                {
                    return new LeaderElectionResult()
                    {
                        IsSuccess = false,
                        State = new LeaderElectionState()
                        {
                            CurrentLeaderId = currentLeaderId,
                            Data = currentLeaderId,
                            IsLeaderOnline = true
                        }
                    };
                }

                // 选举失败但是别的节点也没选举成功
                return new LeaderElectionResult()
                {
                    IsSuccess = false,
                    State = new LeaderElectionState()
                    {
                        CurrentLeaderId = string.Empty,
                        Data = string.Empty,
                        IsLeaderOnline = false
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                // 这里认为就是操作ZooKeeper的异常
                return new LeaderElectionResult()
                {
                    IsSuccess = false,
                    State = new LeaderElectionState()
                    {
                        IsDisconnected = true
                    }
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
            // todo:删除flag，删除leader
            throw new NotImplementedException();
        }

        /// <summary>
        /// 观察选举状态，直到状态变化或者超时
        /// </summary>
        /// <param name="state">上一次获取的状态，作为实时状态的对比参照，如果两个状态不同则说明选举状态发生了变化</param>
        /// <param name="processLatestState">收到最新状态后的处理方法，可能是状态变化或者超时触发此操作</param>
        /// <param name="cancellationToken"></param>
        public void WatchState(LeaderElectionState state, Action<LeaderElectionState> processLatestState, CancellationToken cancellationToken = default)
        {
            if (_electionWatcher == null)
            {
                _electionWatcher = new ZkElectionPathWatcher((leaderPath, eventType) =>
                {
                    if (eventType == EventType.NodeDeleted)
                    {
                        ProcessElectionFlagDeleted(processLatestState);
                    }
                }, _electionDataChangedEvent);
            }

            // zk不可用或连接不上
            if (_isDisconnected)
            {
                var newState = new LeaderElectionState()
                {
                    IsDisconnected = _isDisconnected
                };
                processLatestState?.Invoke(newState);
                return;
            }

            var flagExists = _zkElectionClient.Exists(_electionFlag, _electionWatcher);
            if (flagExists != null)
            {
                // zk又连接上了
                if (state != null && state.IsDisconnected && !_isDisconnected)
                {
                    ProcessReconnectWithElectionFlag(processLatestState);
                    return;
                }

                Console.WriteLine("wait election flag change");
                _electionDataChangedEvent.WaitOne();
            }
            else
            {
                // 非leader节点将确认X次，每次确认需要等待一段时间
                Thread.Sleep(_leaderOfflineConfirmInterval);
                ProcessElectionFlag(processLatestState);
            }
        }

        private void ProcessReconnectWithElectionFlag(Action<LeaderElectionState> processLatestState)
        {
            Console.WriteLine("start process reconnect...");

            var stableLeader = _zkElectionClient.GetData(_electionFlag);

            var newState = new LeaderElectionState()
            {
                IsLeaderOnline = stableLeader != null ? true : false,
                CurrentLeaderId = stableLeader,
                Data = stableLeader
            };

            processLatestState?.Invoke(newState);
        }

        private void ProcessElectionFlagDeleted(Action<LeaderElectionState> processLatestState)
        {
            Console.WriteLine("start process election flag deleted...");

            // flag deleted, read stable leader info
            var stableLeader = _zkElectionClient.GetData(_electionLeader);

            var newState = new LeaderElectionState()
            {
                IsLeaderOnline = false,
                CurrentLeaderId = stableLeader,
                Data = stableLeader
            };

            processLatestState?.Invoke(newState);
        }

        private void ProcessElectionFlag(Action<LeaderElectionState> processLatestState)
        {
            Console.WriteLine("start process election flag...");

            var flag = _zkElectionClient.GetData(_electionFlag);

            if (flag != null)
            {
                var newState = new LeaderElectionState()
                {
                    IsLeaderOnline = flag != null ? true : false,
                    CurrentLeaderId = flag,
                    Data = flag
                };
                processLatestState?.Invoke(newState);
            }
            else
            {
                ProcessElectionFlagDeleted(processLatestState);
            }
        }

        private void ProcessElectionFlagCreated(Action<LeaderElectionState> processLatestState)
        {
            Console.WriteLine("start process election flag created...");

            var flag = _zkElectionClient.GetData(_electionFlag);

            var newState = new LeaderElectionState()
            {
                IsLeaderOnline = flag != null ? true : false,
                CurrentLeaderId = flag,
                Data = flag
            };

            processLatestState?.Invoke(newState);
        }

        private void ProcessZkConnectEvent(KeeperState state)
        {
            if (state == KeeperState.SyncConnected)
            {
                _isDisconnected = false;
            }

            if (state == KeeperState.Disconnected)
            {
                _isDisconnected = true;
            }

            if (state == KeeperState.Expired)
            {
                _isDisconnected = true;
                _zkElectionClient.Reconnect();
            }
        }

        private void CreateElectionRootPath(string zkPath)
        {
            zkPath = zkPath.Trim('/');
            var zkDirs = zkPath.Split(',');
            var zkDir = string.Empty;

            if (zkDirs.Length > 0)
            {
                for (int i = 0; i < zkDirs.Length; i++)
                {
                    zkDir += "/" + zkDirs[i];
                    var isExist = _zkElectionClient.Exists(zkDir);
                    if (isExist == null)
                    {
                        _zkElectionClient.Create(zkDir, null, CreateMode.PERSISTENT);
                    }
                }
            }
        }
    }
}
