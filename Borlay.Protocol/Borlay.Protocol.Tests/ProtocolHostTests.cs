using Borlay.Arrays;
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
            // todo localResolver try dispose
            // todo iject
            // todo add resolver to session as session resolver with parent ()
            // todo visur paduoti sesija (i handleri ir t.t), greiciausiai ne nes rizika naudoti sesija for db

            var host = new ProtocolHost();
            host.InitializeFromReference<ProtocolHostTests>();
            host.Resolver.Register(new CalculatorParameter() { First = 10 });
            
            var serverTask = host.StartServerAsync("127.0.0.1", 90, CancellationToken.None);

            host.ClientDisconnected += (h, s, c , e) =>
            {
                var isClient = c;
            };

            using (var session = await host.StartClientAsync("127.0.0.1", 90))
            {
                var calculator = session.CreateChannel<ICalculator>();

                var result = await calculator.AddAsync("9");

                Assert.AreEqual(19, result.Result);
            }

            await serverTask;
        }
    }
}
