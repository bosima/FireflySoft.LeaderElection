using System;
namespace FireflySoft.LeaderElection
{
    /// <summary>
    /// Leader选举类工厂
    /// </summary>
    public class LeaderElectionFactory
    {
        /// <summary>
        /// 创建一个Leader选举实现类
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public ILeaderElection Create(LeaderElectionOptions options)
        {
            if (options == null || options.LeaderElectionType == EnumLeaderElectionType.Consul)
            {
                return new ConsulLeaderElection();
            }

            if (options == null || options.LeaderElectionType == EnumLeaderElectionType.ZooKeeper)
            {
                return new ZkLeaderElection();
            }

            throw new NotImplementedException("未实现指定的Leader选举类型");
        }
    }
}
