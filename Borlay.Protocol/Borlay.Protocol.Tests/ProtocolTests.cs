using Borlay.Arrays;
using Borlay.Handling;
using Borlay.Injection;
using Borlay.Protocol.Converters;
using Borlay.Serialization.Converters;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol.Tests
{
    public class ProtocolTests
    {
        [SetUp]
        public void Setup()
        {
        }


        [Test]
        public void CacheTest()
        {

            var taks = new Task<ProtocolTests>[] { new Task<ProtocolTests>(() => null) };

            var type = taks.GetType();
            var fullName = type.Name;
            //var genericType = type.GetGenericTypeDefinition();
            //var genericFullname = genericType.FullName;
            //var gt = type.GenericTypeArguments[0];
            //var gf = gt.FullName;
            var element = type.GetElementType()?.FullName;

            var cache = new Cache();

            var watch = Stopwatch.StartNew();

            var seed = ByteArray.New(18).Bytes;

            for (int i = 0; i < 1000000; i++)
            {
                var key = ByteArray.Create(seed);
                if (!cache.Contains(key))
                    cache.AddData(key, seed, 0, seed.Length);

                cache.TryGetData(key, out var b);
            }

            watch.Stop();
        }

        // todo test scope
        // todo encryption
        // todo authentication
        // todo inject for receive

        [Test]
        public async Task SocketManyConnectionTest()
        {
            TcpListener listener = new TcpListener(100);
            listener.Start();

            List<ICalculator> calculators = new List<ICalculator>();

            calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));

            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));

            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));

            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));
            //calculators.AddRange(await GetConnections<ICalculator>(listener, CancellationToken.None));


            List<Task<CalculatorResult>> tasks = new List<Task<CalculatorResult>>();

            Stopwatch watch = Stopwatch.StartNew();


            for (int i = 0; i < 1000; i++)
            {
                for (int j = 0; j < calculators.Count; j++)
                {
                    var task = calculators[j].AddAsync(new CalculatorArgument() { Left = 3, Right = 2 }, CancellationToken.None);
                    tasks.Add(task);
                }
            }


            var results = await Task.WhenAll(tasks);

            watch.Stop();

            var tm = TimeSpan.FromTicks(watch.Elapsed.Ticks);

            foreach (var r in results)
                Assert.AreEqual(15, r.Result);

            var ts = TimeSpan.FromTicks(ProtocolInterfaceHandler<ICalculator>.ts);

            var send_request = ProtocolWatch.GetTimestamp("send-request");
            var send_response = ProtocolWatch.GetTimestamp("rp-send-response");

            var receive_request = ProtocolWatch.GetTimestamp("rp-receive-request");
            var receive_response = ProtocolWatch.GetTimestamp("rp-receive-response");

            var handle_request = ProtocolWatch.GetTimestamp("rp-handle-request");
            var handle_response = ProtocolWatch.GetTimestamp("rp-handle-response");

            var receive_packet = ProtocolWatch.GetTimestamp("receive-packet");
            var read_packet = ProtocolWatch.GetTimestamp("read-packet");
            var after_queue = ProtocolWatch.GetTimestamp("after-queue");

            var request_handler = ProtocolWatch.GetTimestamp("rp-request-handler");

            var handle_async = ProtocolWatch.GetTimestamp("handle-async");
            var handle_send_request = ProtocolWatch.GetTimestamp("handle-send-request");

            var stops = ProtocolWatch.Stops;
            var total = ProtocolWatch.GetTotal();

            // s: 10*1k 2s
            // a: 10*1k 0.8s
            // a: 40*1k 3.3s
        }

        [Test]
        public async Task SocketAddTest()
        {
            TcpListener listener = new TcpListener(102);
            listener.Start();

            var calculators = await GetConnections<ICalculator>(listener, CancellationToken.None);

            List<Task<CalculatorResult>> tasks = new List<Task<CalculatorResult>>();

            var task = await calculators[0].AddAsync(new CalculatorArgument() { Left = 3, Right = 2 }, CancellationToken.None);

            Assert.AreEqual(15, task.Result);
        }

        [Test]
        public async Task SocketAddDuoTest()
        {
            TcpListener listener = new TcpListener(104);
            listener.Start();

            var calculators = await GetConnections<ICalculator>(listener, CancellationToken.None);

            List<Task<CalculatorResult>> tasks = new List<Task<CalculatorResult>>();

            var task = await calculators[0].AddAsync(new CalculatorArgument() { Left = 2, Right = 3 }, new CalculatorArgument() { Left = 4, Right = 5 }, CancellationToken.None);

            Assert.AreEqual(24, task.Result);
        }

        [Test]
        public async Task SocketAddCalcMergeTest()
        {
            TcpListener listener = new TcpListener(105);
            listener.Start();

            var calculators = await GetConnections<ICalculatorMerge>(listener, CancellationToken.None);

            List<Task<CalculatorResult>> tasks = new List<Task<CalculatorResult>>();

            var task = await calculators[0].AddAsync(new CalculatorArgument() { Left = 2, Right = 3 }, new CalculatorArgument() { Left = 4, Right = 5 }, CancellationToken.None);

            Assert.AreEqual(24, task.Result);
        }

        [Test]
        public async Task SocketAddZeroTest()
        {
            TcpListener listener = new TcpListener(101);
            listener.Start();

            var calculators = await GetConnections<ICalculator>(listener, CancellationToken.None);

            List<Task<CalculatorResult>> tasks = new List<Task<CalculatorResult>>();

            var task = await calculators[0].AddAsync();

            Assert.AreEqual(10, task.Result);
        }

        [Test]
        public async Task SocketAddStringTest()
        {
            TcpListener listener = new TcpListener(103);
            listener.Start();

            var calculators = await GetConnections<ICalculator>(listener, CancellationToken.None);

            List<Task<CalculatorResult>> tasks = new List<Task<CalculatorResult>>();

            var task = await calculators[0].AddAsync("20");

            Assert.AreEqual(30, task.Result);
        }

        private async Task<TInterface[]> GetConnections<TInterface>(TcpListener listener, CancellationToken cancellationToken) where TInterface : class
        {
            var socketTask = listener.AcceptTcpClientAsync();
            TcpClient tcpClient = new TcpClient("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port);

            var tcpServer = await socketTask;

            //var serverPacketStream = new TestPacketStream();
            //var clientPacketStream = new TestPacketStream();
            //serverPacketStream.Connected(clientPacketStream);
            //clientPacketStream.Connected(serverPacketStream);

            //var resolver1 = GetProtocolResolver(clientPacketStream, true); //tcpClient.GetStream());
            //var resolver2 = GetProtocolResolver(serverPacketStream); // tcpServer.GetStream());

            var resolver1 = GetProtocolResolver(tcpClient.GetStream());
            var resolver2 = GetProtocolResolver(tcpServer.GetStream());
 

            resolver1.Resolver.Register(new CalculatorParameter() { First = 10 });
            resolver2.Resolver.Register(new CalculatorParameter() { First = 10 });

            var calculator1 = InterfaceHandling.CreateHandler<TInterface, ProtocolInterfaceHandler<TInterface>>(resolver1);
            var calculator2 = InterfaceHandling.CreateHandler<TInterface, ProtocolInterfaceHandler<TInterface>>(resolver2);

            return new TInterface[] { calculator1 , calculator2 };

        }

        public IResolverSession GetProtocolResolver(Stream stream)
        {
            return GetProtocolResolver(new PacketStream(stream), CancellationToken.None);
        }

        public IResolverSession GetProtocolResolver(Stream stream, CancellationToken cancellationToken)
        {
            return GetProtocolResolver(new PacketStream(stream), cancellationToken);
        }

        public IResolverSession GetProtocolResolver(IPacketStream packetStream)
        {
            return GetProtocolResolver(packetStream, CancellationToken.None);
        }

        public IResolverSession GetProtocolResolver(IPacketStream packetStream, CancellationToken cancellationToken)
        {
            var resolver = new Resolver();
            resolver.LoadFromReference<ProtocolTests>();
            var session = resolver.CreateSession();

            var handler = new HandlerProvider(session.Resolve<IMethodContextInfoProvider>());
            handler.LoadFromReference<ProtocolTests>();

            var converter = new Serializer();
            converter.LoadFromReference<ProtocolTests>();

            var protocol = new SocketProtocolHandler(session, packetStream, converter, handler);

            session.Resolver.Register(protocol);
            session.Resolver.Register(handler);
            session.Resolver.Register(converter);

            var listenTask = protocol.ListenAsync(cancellationToken);

            return session;
        }

        [Test]
        public async Task InterfaceToMethodProtocolHandlerTest()
        {
            var resolver = new Resolver();
            resolver.LoadFromReference<ProtocolTests>();

            resolver.Register(new CalculatorParameter() { First = 10 });

            var session = resolver.CreateSession();
            //resolver.Register(session);

            var handler = new HandlerProvider(session.Resolve<IMethodContextInfoProvider>());
            handler.LoadFromReference<ProtocolTests>();

            var methodHandler = new MethodProtocolHandler(handler, session);
            resolver.Register(methodHandler, true);
            var calculator = InterfaceHandling.CreateHandler<ICalculator, ProtocolInterfaceHandler<ICalculator>>(session);

            List<Task> tasks = new List<Task>();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++)
            {

                var task = calculator.AddAsync("20");
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);

            watch.Stop();

            // a: 10k 0.2-0.3s

            //Assert.AreEqual(30, task.Result);
        }

        [Test]
        public async Task PlainSocketTest()
        {
            TcpListener listener = new TcpListener(106);
            listener.Start();

            var socketTask = listener.AcceptTcpClientAsync();
            TcpClient tcpClient = new TcpClient("127.0.0.1", 106);

            var tcpServer = await socketTask;

            //var clientStream = tcpClient.GetStream();
            //var serverStream = tcpServer.GetStream();

            var serverStream = new TestPacketStream();
            var clientStream = new TestPacketStream();
            serverStream.Connected(clientStream);
            clientStream.Connected(serverStream);

            byte[] writeBuffer = Borlay.Arrays.ByteArray.New(64).Bytes;
            byte[] readBuffer = new byte[writeBuffer.Length + 2];

            Stopwatch watch = Stopwatch.StartNew();

            for (int i = 0; i < 10000; i++)
            {
                TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();


                clientStream.WritePacket(writeBuffer, (ushort)writeBuffer.Length - 2, true); //, CancellationToken.None);
                var count = serverStream.ReadPacket(readBuffer, CancellationToken.None);

                clientStream.WritePacket(writeBuffer, (ushort)writeBuffer.Length - 2, true); //, CancellationToken.None);
                count = serverStream.ReadPacket(readBuffer, CancellationToken.None);

                //await clientStream.WriteAsync(writeBuffer, 0, (ushort)writeBuffer.Length, CancellationToken.None);
                //var count = await serverStream.ReadAsync(readBuffer, 0, 64, CancellationToken.None);
                //Assert.AreEqual(64, count);
            }

            watch.Stop();

            // 10k 0.3s
            // testPS: 10k 0.03s
        }
    }
}