using System;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using static org.apache.zookeeper.Watcher.Event;

namespace FireflySoft.LeaderElection
{
    internal class ZkElectionClientWatcher : Watcher
    {
        private readonly Action _processExpired;

        public ZkElectionClientWatcher(Action processExpired)
        {
            _processExpired = processExpired;
        }

        public override Task process(WatchedEvent @event)
        {
            Console.WriteLine("Type:" + @event.GetType() + ",EventType:" + @event.get_Type() + ",State:" + @event.getState());

            if (@event.getState() == KeeperState.Expired)
            {
                Console.WriteLine("ZooKeeper connection expired");
                return Task.Run(() =>
                {
                    _processExpired?.Invoke();
                });
            };

            return Task.CompletedTask;
        }
    }
}
