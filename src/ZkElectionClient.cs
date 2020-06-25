using System;
using System.Text;
using org.apache.zookeeper;
using org.apache.zookeeper.data;
using static org.apache.zookeeper.KeeperException;
using static org.apache.zookeeper.ZooDefs;

namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// ZooKeeper Election Client
    /// </summary>
    public class ZkElectionClient
    {
        private readonly string _zkConnectionString;
        private readonly int _sessionTimeout;
        private ZooKeeper _zk;
        private Watcher _zkClientWatcher;

        /// <summary>
        /// 创建ZkElectionClient的一个新实例
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="sessionTimeout"></param>
        public ZkElectionClient(string connectionString = "127.0.0.1:2181", int sessionTimeout = 30000)
        {
            _zkConnectionString = connectionString;
            _sessionTimeout = sessionTimeout;
            _zkClientWatcher = new ZkElectionClientWatcher(() =>
            {
                ConnectServer();
            });
            ConnectServer();
        }

        /// <summary>
        /// 判断路径是否存在
        /// </summary>
        /// <param name="path"></param>
        /// <param name="watcher"></param>
        /// <returns></returns>
        public Stat Exists(string path, Watcher watcher = null)
        {
            if (watcher == null)
            {
                return _zk.existsAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            return _zk.existsAsync(path, watcher).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 获取指定路径的值
        /// </summary>
        /// <param name="path"></param>
        /// <param name="watcher"></param>
        /// <returns></returns>
        public string GetData(string path, Watcher watcher = null)
        {
            try
            {
                if (watcher == null)
                {
                    var data = _zk.getDataAsync(path).ConfigureAwait(false).GetAwaiter().GetResult();
                    return Encoding.UTF8.GetString(data.Data);
                }
                else
                {
                    var data = _zk.getDataAsync(path, watcher).ConfigureAwait(false).GetAwaiter().GetResult();
                    return Encoding.UTF8.GetString(data.Data);
                }
            }
            catch (NoNodeException)
            {
            }

            return null;
        }

        /// <summary>
        /// 创建一个路径
        /// </summary>
        /// <param name="path"></param>
        /// <param name="value"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public bool Create(string path, string value, CreateMode mode)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(value);
                _zk.createAsync(path, data, Ids.OPEN_ACL_UNSAFE, mode).GetAwaiter().GetResult();
                return true;
            }
            catch (NodeExistsException)
            {
            }

            return false;
        }

        /// <summary>
        /// 更新指定路径对应的值
        /// </summary>
        /// <param name="path"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public Stat Update(string path, string value)
        {
            var data = Encoding.UTF8.GetBytes(value);
            return _zk.setDataAsync(path, data).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 连接Zookeeper Server
        /// </summary>
        private void ConnectServer()
        {
            _zk = new ZooKeeper(_zkConnectionString, _sessionTimeout, _zkClientWatcher);
            Console.WriteLine("ZooKeeper Server have Connected");
        }
    }
}
