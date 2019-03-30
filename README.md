# Borlay.Protocol
It is fast multithread duplex protocol. It's safe to send many requests at same time from one client and at the same time you can receive requests from server. 
On my laptop it handles more than 10k request per second, it's couple times faster than WCF or .Net Core Rest service. Redis is slower as well. And data serialization is very compact.

## Example

```cs
// create host
var host = new ProtocolHost();
// load controllers from reference
host.InitializeFromReference<ProtocolHostTests>();
// register object to inject into controller
host.Resolver.Register(new CalculatorParameter() { First = 10 });

// start server
var serverTask = host.StartServerAsync("127.0.0.1", 90, CancellationToken.None);

// handle client disconnections
host.ClientDisconnected += (h, s, c , e) =>
{
    var isClient = c;
};

// connect client
using (var session = await host.StartClientAsync("127.0.0.1", 90))
{
    // create channel for calculator interface
    var calculator = session.CreateChannel<ICalculator>();
    
    // call method
    var task1 = calculator.AddAsync("5");
    var task2 = calculator.AddAsync("9");
    
    await Task.WhenAll(task1, task2);
    
    // check result
    Assert.AreEqual(19, task2.Result);
}

```

## Example interface and implementation:
[calculator implementation](https://github.com/Borlay/Borlay.Protocol/blob/master/Borlay.Protocol/Borlay.Protocol.Tests/TestData.cs)

## Help
If you are interested in this project feel free to write me email or open issue

