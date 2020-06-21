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
        private TimeSpan _confirmWatchInterval = new TimeSpan(0, 0, 10);

        private ZkElectionOptions _options;
        private ZooKeeper _zkClient;
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
            _zkClient = _options.ZkClient;
            _electionRootPath = string.Format("/leader-election/{0}", serviceName);
            _electionFlag = string.Concat(new string[] { _electionRootPath, "/", "flag" });
            _electionLeader = string.Concat(new string[] { _electionRootPath, "/", "leader" });
            _serviceId = serviceId;
            _electionDataChangedEvent = new AutoResetEvent(false);
            CreateZkPath(_electionRootPath, null, CreateMode.PERSISTENT);
        }

        /// <summary>
        /// 发起选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public LeaderElectionResult Elect(CancellationToken cancellationToken = default)
        {
            DataResult flagData;
            string currentLeaderId = string.Empty;
            try
            {
                flagData = _zkClient.getDataAsync(_electionFlag).ConfigureAwait(false).GetAwaiter().GetResult();
                currentLeaderId = Encoding.UTF8.GetString(flagData.Data);
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
            catch (NoNodeException)
            {
                try
                {
                    currentLeaderId = _serviceId;
                    var data = Encoding.UTF8.GetBytes(currentLeaderId);
                    _zkClient.createAsync(_electionFlag, data, Ids.OPEN_ACL_UNSAFE, CreateMode.EPHEMERAL).GetAwaiter().GetResult();

                    if (_zkClient.existsAsync(_electionLeader).ConfigureAwait(false).GetAwaiter().GetResult() == null)
                    {
                        _zkClient.createAsync(_electionLeader, data, Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    else
                    {
                        _zkClient.setDataAsync(_electionLeader, data).ConfigureAwait(false).GetAwaiter().GetResult();
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
                catch (NodeExistsException)
                {
                    flagData = _zkClient.getDataAsync(_electionFlag).ConfigureAwait(false).GetAwaiter().GetResult();
                    currentLeaderId = Encoding.UTF8.GetString(flagData.Data);
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
            }
        }

        /// <summary>
        /// 清空选举状态，服务所有部署重新开始选举
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public bool Reset(CancellationToken cancellationToken = default)
        {
            return false;
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
                _electionWatcher = new ZkElectionPathWatcher(leaderPath =>
                {
                    ProcessElectionFlagDeleted(processLatestState);
                }, _electionDataChangedEvent);
            }

            var flagExists = _zkClient.existsAsync(_electionFlag, _electionWatcher).ConfigureAwait(false).GetAwaiter().GetResult();
            if (flagExists != null)
            {
                _electionDataChangedEvent.WaitOne();
            }
            else
            {
                // 非leader节点将确认3次，每次确认需要等待一段时间
                Thread.Sleep(_confirmWatchInterval);
                ProcessElectionFlagDeleted(processLatestState);
            }
        }

        private void ProcessElectionFlagDeleted(Action<LeaderElectionState> processLatestState)
        {
            // flag deleted, read stable leader info
            var stableLeader = GetElectionStableLeader();

            var newState = new LeaderElectionState()
            {
                IsLeaderOnline = false,
                CurrentLeaderId = stableLeader,
                Data = stableLeader
            };

            processLatestState?.Invoke(newState);
        }

        private string GetElectionStableLeader()
        {
            //try
            //{
            //    DataResult stableLeaderData = _zkClient.getDataAsync(_electionLeader).ConfigureAwait(false).GetAwaiter().GetResult();
            //    return Encoding.UTF8.GetString(stableLeaderData.Data);
            //}
            //catch (NoNodeException)
            //{
            //    // 可能还没有创建
            //}

            return string.Empty;
        }

        private void CreateZkPath(string zkPath, string zkValue, CreateMode createMode)
        {
            zkPath = zkPath.Trim('/');
            var zkDirs = zkPath.Split(',');
            var zkDir = string.Empty;

            if (zkDirs.Length > 0)
            {
                for (int i = 0; i < zkDirs.Length; i++)
                {
                    zkDir += "/" + zkDirs[i];
                    var isExist = _zkClient.existsAsync(zkDir).GetAwaiter().GetResult();
                    if (isExist == null)
                    {
                        byte[] zkData = null;
                        if (!string.IsNullOrWhiteSpace(zkValue))
                        {
                            zkData = Encoding.UTF8.GetBytes(zkValue);
                        }

                        try
                        {
                            _zkClient.createAsync(zkDir, zkData, Ids.OPEN_ACL_UNSAFE, createMode).ConfigureAwait(false).GetAwaiter().GetResult();
                        }
                        catch (NodeExistsException)
                        {
                        }
                    }
                }
            }
        }
    }
}
