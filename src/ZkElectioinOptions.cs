using System;
using org.apache.zookeeper;

namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// ZooKeeper选举选项
    /// </summary>
    public class ZkElectionOptions : LeaderElectionOptions
    {
        /// <summary>
        /// 初始化ZooKeeper选举选项的一个新实例，默认连接本机ZooKeeper
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="sessionTimeout"></param>
        public ZkElectionOptions(string connectionString = "127.0.0.1:2181", int sessionTimeout = 10000)
        {
            base.LeaderElectionType = EnumLeaderElectionType.ZooKeeper;
            SessionTimeout = sessionTimeout;
            ConnectionString = connectionString;
        }

        /// <summary>
        /// ZooKeeper Server连接字符串
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// 获取Session超时时间
        /// </summary>
        public int SessionTimeout { get; private set; }
    }
}
