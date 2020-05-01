# Borlay.Protocol
It is fast multithread duplex protocol. It's safe to send many requests from client and at the same time you can receive requests from server. 
On my laptop it handles more than 10k request per second. It is couple times faster than WCF or .Net Core Rest service. Redis is slower as well. And data serialization is fast and compact. All performance tests were made while server and client was running on the same computer.

## Example

```cs
// create host
var host = new ProtocolHost();

// Load controllers from reference.
host.LoadFromReference<ProtocolHostTests>();

// Register object to dependency injection. Just an example. This test doesn't require this.
host.Resolver.Register(new CalculatorParameter() { First = 10 });

// Start server. Should be skiped on client side.
var serverTask = host.StartServerAsync("127.0.0.1", 90, CancellationToken.None);


IResolverSession clientSession = null;
// Handle client connections.
host.ClientConnected += (h, s) =>
{
    // Session of client connected to server.
    clientSession = s;
};

// Can be used in Program.Main to run server. Task ends when server socket stops listening.
// await serverTask;


// Connect client. Should be skiped on server side.
var session = await host.StartClientAsync("127.0.0.1", 90);

// Create channel of IAddMethod interface on client side and call method from client to server.
var serverSideCalculator = session.CreateChannel<IAddMethod>();

// Create Stopwatch for performance measure.
var watch = Stopwatch.StartNew();

var tasks = new List<Task<CalculatorResult>>();

// Run asynchronous 10k requests.
for (int i = 0; i < 10000; i++)
{
    // From client call method on server side.
    var task = serverSideCalculator.AddAsync(10, 9);
    tasks.Add(task);
}

// Wait when all 10k requests ends.
var serverResults = await Task.WhenAll(tasks);

// Stop Stopwatch.
watch.Stop();

// Check result.
foreach(var serverResult in serverResults)
    Assert.AreEqual(19, serverResult.Result);

// Check if elapsed time is less than one second.
Assert.IsTrue(watch.ElapsedMilliseconds < 1000, $"Elapsed time is more than one second. Elapsed time in milliseconds:{watch.ElapsedMilliseconds}");

// Since we created both server and client from same ProtocolHost, they have same controllers that we can call.
// Create channel of IAddMethod interface on server side and call method from server to client.
var clientSideCalculator = clientSession.CreateChannel<IAddMethod>();
var clientResult = await clientSideCalculator.AddAsync(10, 10);
Assert.AreEqual(20, clientResult.Result);

// Close client connection.
session.Dispose();

```

## Example interface and implementation:
[calculator implementation](https://github.com/Borlay/Borlay.Protocol/blob/master/Borlay.Protocol/Borlay.Protocol.Tests/TestData.cs)

