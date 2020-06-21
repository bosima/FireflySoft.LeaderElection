using System;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using static org.apache.zookeeper.Watcher.Event;

namespace FireflySoft.LeaderElection
{
    internal class ZkElectionPathWatcher : Watcher
    {
        private readonly Action<string> _processPathDeleted;
        private readonly AutoResetEvent _electionDataChangedEvent;

        public ZkElectionPathWatcher(Action<string> processPathDeleted, AutoResetEvent electionDataChangedEvent)
        {
            _processPathDeleted = processPathDeleted;
            _electionDataChangedEvent = electionDataChangedEvent;
        }

        public override Task process(WatchedEvent @event)
        {
            Console.WriteLine("Type:" + @event.GetType() + ",EventType:" + @event.get_Type() + ",State:" + @event.getState());

            if (@event.get_Type() == EventType.NodeDeleted)
            {
                var leaderPath = @event.getPath();
                try
                {
                    _processPathDeleted?.Invoke(leaderPath);
                }
                catch (KeeperException ex)
                {
                    Console.WriteLine(ex);
                }

                _electionDataChangedEvent.Set();
            }

            return Task.CompletedTask;
        }
    }
}
