using System;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using static org.apache.zookeeper.Watcher.Event;

namespace FireflySoft.LeaderElection
{
    internal class ZkElectionPathWatcher : Watcher
    {
        private readonly Action<string, EventType> _processPathChanged;
        private readonly AutoResetEvent _electionDataChangedEvent;

        public ZkElectionPathWatcher(Action<string, EventType> processPathChanged, AutoResetEvent electionDataChangedEvent)
        {
            _processPathChanged = processPathChanged;
            _electionDataChangedEvent = electionDataChangedEvent;
        }

        public override Task process(WatchedEvent @event)
        {
            Console.WriteLine("Type:" + @event.GetType() + ",EventType:" + @event.get_Type() + ",State:" + @event.getState());

            var eventType = @event.get_Type();
            if (eventType == EventType.NodeDeleted)
            {
                return Task.Run(() =>
                {
                    var leaderPath = @event.getPath();
                    try
                    {
                        _processPathChanged?.Invoke(leaderPath, eventType);
                    }
                    catch (KeeperException ex)
                    {
                        Console.WriteLine(ex);
                    }

                    _electionDataChangedEvent.Set();
                });
            }

            return Task.CompletedTask;
        }
    }
}
