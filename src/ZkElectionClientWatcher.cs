using System;
using System.Threading;
using System.Threading.Tasks;
using org.apache.zookeeper;
using static org.apache.zookeeper.Watcher.Event;

namespace FireflySoft.LeaderElection
{
    internal class ZkElectionClientWatcher : Watcher
    {
        private readonly Action _processDisconnected;
        private readonly AutoResetEvent _electionDataChangedEvent;

        public ZkElectionClientWatcher(Action processDisconnected, AutoResetEvent electionDataChangedEvent)
        {
            _processDisconnected = processDisconnected;
            _electionDataChangedEvent = electionDataChangedEvent;
        }

        public override Task process(WatchedEvent @event)
        {
            Console.WriteLine("Type:" + @event.GetType() + ",EventType:" + @event.get_Type() + ",State:" + @event.getState());

            if (@event.getState() == KeeperState.Disconnected)
            {
                _processDisconnected?.Invoke();
                _electionDataChangedEvent?.Set();
            }

            if (@event.getState() == KeeperState.Expired)
            {
               // todo create new client
            }

            return Task.CompletedTask;
        }
    }
}
