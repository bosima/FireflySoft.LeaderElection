using System;
using FireflySoft.LeaderElection;

namespace ElectionService1
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("I am ElectionService1.");
            LeaderElectionManager electionManager = new LeaderElectionManager("ElectionService", "ElectionService1", new ConsulElectionOptions());
            electionManager.Watch(LeaderElectCompletedEventHandler);
            Console.WriteLine("Start Election...");

            Console.Read();
        }

        private static void LeaderElectCompletedEventHandler(LeaderElectionResult result)
        {
            Console.WriteLine($"LeaderElectCompleted, Result: {result.IsSuccess}, Current Leader: {result.State.CurrentLeaderId}.");
        }
    }
}
