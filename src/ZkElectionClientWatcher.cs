using System;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using static org.apache.zookeeper.Watcher.Event;

namespace FireflySoft.LeaderElection
{
    internal class ZkElectionClientWatcher : Watcher
    {
        private readonly Action<KeeperState> _processConnect;

        public ZkElectionClientWatcher(Action<KeeperState> processConnect)
        {
            _processConnect = processConnect;
        }

        public override Task process(WatchedEvent @event)
        {
            Console.WriteLine("Type:" + @event.GetType() + ",EventType:" + @event.get_Type() + ",State:" + @event.getState());

            var state = @event.getState();
            if (state == KeeperState.Disconnected
                || state == KeeperState.Expired
                || state == KeeperState.SyncConnected)
            {
                return Task.Run(() =>
                {
                    _processConnect?.Invoke(state);
                });
            };

            return Task.CompletedTask;
        }
    }
}
