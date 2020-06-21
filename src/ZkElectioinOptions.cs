﻿using System;
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
        public ZkElectionOptions(string connectionString = "127.0.0.1:2181", int sessionTimeout = 30000)
        {
            base.LeaderElectionType = EnumLeaderElectionType.ZooKeeper;
            ZkSessionTimeout = sessionTimeout;
            var watcher = new ZkElectionClientWatcher(null, null);
            ZkClient = new ZooKeeper(connectionString, ZkSessionTimeout, watcher);
        }

        /// <summary>
        /// 获取当前ZooKeeper Client
        /// </summary>
        public ZooKeeper ZkClient { get; private set; }

        /// <summary>
        /// 获取Session超时时间
        /// </summary>
        public int ZkSessionTimeout { get; private set; }
    }
}