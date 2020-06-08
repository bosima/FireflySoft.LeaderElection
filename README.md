# FireflySoft.LeaderElection
一个基于Consul的.NET Leader选举类库。


## 基于Consul的Leader选举原理

1、参加选举的程序可以在Consul中创建一个Session，这个Session的存活状态依赖于当前程序的Consul健康检查状态，
一旦健康检查处于Critical状态，则对应的Session就会失效。

2、使用这个Session去锁定某个Consul Key/Value，只有一个Session能成功锁住KV，拥有这个Session的程序即为Leader。

3、Leader选举成功后，所有节点还要继续阻塞查询上边的Consul Key/Value，如果KV绑定的Session失效了，
所有节点可以立即发现并发起一次Leader选举，并选举出1个Leader。

## 使用说明

### 1、启动本机Consul

当前的版本依赖本机Consul，后续会支持配置远程Consul地址。

如果本地环境已经配置Consul，保证其正常运行即可。

如果本地环境没有配置Consul，可以下载后以开发模式快速启动，以方便体验Leader选举功能。

下载地址：https://www.consul.io/downloads

启动命令：./consul agent -dev

### 2、安装Nuget包

NuGet包地址：https://www.nuget.org/packages/FireflySoft.LeaderElection

### 3、编写Leader选举代码

以控制台程序为例：

```csharp
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("I am ElectionService1.");

            // 参与Leader选举的多个程序应该使用相同的服务名
            // 参与Leader选举的每个程序应该有唯一的服务Id
            LeaderElectionManager electionManager = new LeaderElectionManager("ElectionService", "ElectionService1", new LeaderElectionOptions());
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

### 4、注意事项

#### 选举沉默期

LeaderElectionOptions中提供了一个重新选举沉默期：ReElectionSilencePeriod，默认15s。应用场景如下：

当一个程序的Leader状态失效时，它可能仍在处理某些事务，并且不能立即中止。
这时候如果其它节点马上选举成为Leader，并且开始处理数据，则可能导致数据不一致的状态。

#### Leader优先选举权

此类库为Leader增加了优先选举权。应用场景如下：

Leader状态失效可能只是一种短暂的中断导致的，系统会很快自动恢复，而业务事务的的启动和中止需要进行复杂的处理，
所以我们仍然期望下一次Leader选举时之前的Leader有优先选举权，避免数据同步和加快系统恢复。