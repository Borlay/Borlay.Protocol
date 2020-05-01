# Borlay.Protocol
It is fast multithread duplex protocol. It's safe to send many requests at same time from one client and at the same time you can receive requests from server. 
On my laptop it handles more than 10k request per second, it's couple times faster than WCF or .Net Core Rest service. Redis is slower as well. And data serialization is very compact.

## Example

```cs
// create host
var host = new ProtocolHost();

// Load controllers from reference.
host.LoadFromReference<ProtocolHostTests>();

// Register object to dependency injection.
host.Resolver.Register(new CalculatorParameter() { First = 10 });

// Start server. Should be skiped on client side.
var serverTask = host.StartServerAsync("127.0.0.1", 90, CancellationToken.None);


IResolverSession clientSession = null;
// Handle client connections.
host.ClientConnected += (h, s) =>
{
    // Session of a client connected to server.
    clientSession = s;
};

// Can be used in Program.Main to run server. Task ends when server socket stops listening.
// await serverTask;


// Connect client. Should be skiped on server side.
var session = await host.StartClientAsync("127.0.0.1", 90);

// Create channel for interface and call method from client to server.
var serverSidecalculator = session.CreateChannel<IAddMethod>();
var serverResult = await serverSidecalculator.AddAsync(10, 9);
Assert.AreEqual(19, serverResult.Result);

// Create channel for interface and call method from server to client.
var clientSideCalculator = clientSession.CreateChannel<IAddMethod>();
var clientResult = await clientSideCalculator.AddAsync(10, 10);
Assert.AreEqual(20, clientResult.Result);

// close client connection.
session.Dispose();

```

## Example interface and implementation:
[calculator implementation](https://github.com/Borlay/Borlay.Protocol/blob/master/Borlay.Protocol/Borlay.Protocol.Tests/TestData.cs)

