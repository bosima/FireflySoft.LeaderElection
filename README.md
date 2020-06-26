# FireflySoft.LeaderElection
一个.NET Leader选举类库，支持基于Consul和ZooKeeper。

## 基于Consul

### 原理

1、参加选举的程序可以在Consul中创建一个Session，这个Session的存活状态依赖于当前程序的Consul健康检查状态，
一旦健康检查处于Critical状态，则对应的Session就会失效。

2、使用这个Session去锁定某个Consul Key/Value，只有一个Session能成功锁住KV，拥有这个Session的程序即为Leader。

3、Leader选举成功后，所有节点还要继续阻塞查询上边的Consul Key/Value，如果KV绑定的Session失效了，
所有节点可以立即发现并发起一次Leader选举，并选举出1个Leader。

### 使用说明

#### 1、准备Consul环境

这里为了方便使用本机Consul，此程序也支持配置远程Consul地址。

如果本地环境已经配置Consul，保证其正常运行即可。

如果本地环境没有配置Consul，可以下载后以开发模式快速启动，以方便体验Leader选举功能。

下载地址：https://www.consul.io/downloads

启动命令：./consul agent -dev

#### 2、安装Nuget包

NuGet包地址：https://www.nuget.org/packages/FireflySoft.LeaderElection

#### 3、编写Leader选举代码

以控制台程序为例：

```csharp
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("I am ElectionService1.");

            // 参与Leader选举的多个程序应该使用相同的服务名
            // 参与Leader选举的每个程序应该有唯一的服务Id
            LeaderElectionManager electionManager = new LeaderElectionManager("ElectionService", "ElectionService1", new ConsulElectionOptions());
            electionManager.Watch(LeaderElectCompletedEventHandler);

            Console.WriteLine("Start Election...");

            Console.Read();
        }

        private static void LeaderElectCompletedEventHandler(LeaderElectionResult result)
        {
            // 在这里处理Leader选举结果。
            Console.WriteLine($"LeaderElectCompleted, Result: {result.IsSuccess}, Current Leader: {result.State.CurrentLeaderId}.");
        }
    }
```
#### 4、注意事项

ConsulElectionOptions中提供了一个重新选举沉默期：ReElectionSilencePeriod，默认10s。应用场景如下：

当一个程序的Leader状态失效时，它可能仍在处理某些事务，并且不能立即中止。这时候如果马上启动选举，并且开始处理数据，则可能导致数据不一致的状态。


## 基于ZooKeeper

### 原理

1、所有参与选举的程序都在ZooKeeper上发起创建一个相同路径的EPHEMERAL Node，只有一个程序能够创建成功，此程序即为Leader。

2、所有参与选举的程序都Watch上边创建的EPHEMERAL Node，Leader程序在ZooKeeper的会话过期后，这个Node会被删除，所有Watch的程序都会收到通知，从而发起新一轮选举。

### 使用说明

#### 1、准备ZooKeeper环境

如果已经有搭建好的ZooKeeper集群，直接使用对应的地址就可以了。

如果没有，这里给出一个快速搭建ZooKeeper环境的方法：通过docker启动一个单节点ZooKeeper。

docker run --name zoo1 -p 8080:8080 -p 2181:2181 --restart always -d zookeeper

#### 2、编写Leader选举代码

以控制台程序为例：

```csharp
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("I am ElectionService1.");

            // 参与Leader选举的多个程序应该使用相同的服务名
            // 参与Leader选举的每个程序应该有唯一的服务Id
            LeaderElectionManager electionManager = new LeaderElectionManager("ElectionService", "ElectionService1", new ZkElectionOptions());
            electionManager.Watch(LeaderElectCompletedEventHandler);

            Console.WriteLine("Start Election...");

            Console.Read();
        }

        private static void LeaderElectCompletedEventHandler(LeaderElectionResult result)
        {
            // 在这里处理Leader选举结果。
            Console.WriteLine($"LeaderElectCompleted, Result: {result.IsSuccess}, Current Leader: {result.State.CurrentLeaderId}.");
        }
    }
```

## 其它说明

### Leader状态保持

在Consul中Leader状态取决于当前Leader程序的健康状态，该程序的健康状态依赖于程序自身的健康检查状态以及程序注册的Consul Agent的健康检查状态，只要有一个关联的健康检查状态不通过，程序就是非健康的，就会丢失Leader状态。（新版本的Consul中支持健康状态法定数目判定规则，此类库没有使用。）

在ZooKeeper中Leader状态依赖于选举成功时创建的临时ZooKeeper Node，Leader程序如果未在SessioinTimeout时间内与ZooKeeper通信，Node就会被删除，则Leader状态丢失。

无论是Consul健康检查机制，还是ZooKeeper临时Node保持机制，都依赖于应用程序与选举支持程序（即Consul、ZooKeeper等）之间的状态维护机制，这些机制都需要一定的时间进行确认，并非是完全实时的。

### Leader优先选举权

此类库为Leader增加了优先选举权。应用场景如下：

Leader状态失效可能只是一种短暂的中断导致的，系统会很快自动恢复，而业务事务的的启动和中止需要进行复杂的处理，
所以我们仍然期望下一次Leader选举时之前的Leader有优先选举权，避免数据同步和加快系统恢复。

### 防脑裂

Leader断开与选举支持程序之间的连接时，选举支持程序会认为Leader已经下线，从而开启新的选举，选举出新的Leader，而原Leader并不能收到重新选举的通知，仍旧保持Leader状态，则就会同时存在两个Leader，也就是产生了脑裂问题。

此类库对这种问题进行了处理，应用程序会定时访问选举支持程序，一旦出现连接不上的情况，就会自动产生一条Leader选举失败的事件，应用程序可以据此进行降级处理。

