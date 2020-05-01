using Borlay.Arrays;
using Borlay.Injection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol.Tests
{
    public class ProtocolHostTests
    {
        [Test]
        public async Task HostAndCreateChannel()
        {
            // Create host
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
                // Session of client connected to server.
                clientSession = s;
            };

            // Can be used in Program.Main to run server. Task ends when server socket stops listening.
            // await serverTask;


            // Connect client. Should be skiped on server side.
            var session = await host.StartClientAsync("127.0.0.1", 90);

            // Create channel for interface to call method from client to server.
            var serverSidecalculator = session.CreateChannel<IAddMethod>();

            // Create Stopwatch for performance measure.
            var watch = Stopwatch.StartNew();

            var tasks = new List<Task<CalculatorResult>>();

            // Run asynchronous 10k requests.
            for (int i = 0; i < 10000; i++)
            {
                // From client call method on server side.
                var task = serverSidecalculator.AddAsync(10, 9);
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
            Assert.IsTrue(watch.ElapsedMilliseconds < 1000, $"Elapsed time is more than one second. Elapsed time in milliseconds: {watch.ElapsedMilliseconds}");

            // Create channel for interface and call method from server to client.
            var clientSideCalculator = clientSession.CreateChannel<IAddMethod>();
            var clientResult = await clientSideCalculator.AddAsync(10, 10);
            Assert.AreEqual(20, clientResult.Result);

            // Close client connection.
            session.Dispose();
        }

        private void Host_ClientConnected(ProtocolHost arg1, Borlay.Injection.IResolverSession arg2)
        {
            throw new NotImplementedException();
        }

        [Test]
        public async Task HostAndChannelFactory()
        {
            var host = new ProtocolHost();
            host.LoadFromReference<ProtocolHostTests>();
            host.Resolver.Register(new CalculatorParameter() { First = 10 });

            var cts = new CancellationTokenSource();
            var serverTask = host.StartServerAsync("127.0.0.1", 91, cts.Token);

            var channelProvider = new ChannelProvider("127.0.0.1", 91);
            channelProvider.LoadFromReference<ProtocolHostTests>();
            var calculator = await channelProvider.GetChannelAsync<ICalculator>();

            var result = await calculator.AddAsync("9");
            Assert.AreEqual(19, result.Result);

            cts.Cancel();
            try
            {
                await serverTask;
            }
            catch { };

            host = new ProtocolHost();
            host.LoadFromReference<ProtocolHostTests>();
            host.Resolver.Register(new CalculatorParameter() { First = 10 });

            serverTask = host.StartServerAsync("127.0.0.1", 91, CancellationToken.None);

            var channelProviderCollection = new ChannelProviderCollection(channelProvider);

            calculator = await channelProviderCollection.GetChannelAsync<ICalculator>();
            result = await calculator.AddAsync("9");
            Assert.AreEqual(19, result.Result);
        }
    }
}
