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
    public class ProtocolInjectionTests
    {
        [Test]
        public async Task CacheTest()
        {
            // update resolver after session fixes
            // also in handler project
            // localResolver try dispose

            var host = new ProtocolHost();
            host.InitializeFromReference<ProtocolInjectionTests>();
            host.Resolver.Register(new CalculatorParameter() { First = 10 });
            var serverTask = host.StartServerAsync("127.0.0.1", 90, true, CancellationToken.None);

            using (var session = await host.StartClientAsync("127.0.0.1", 90))
            {
                var calculator = session.CreateChannel<ICalculator>();

                var result = await calculator.AddAsync("9");

                Assert.AreEqual(19, result.Result);
            }
        }
    }
}
