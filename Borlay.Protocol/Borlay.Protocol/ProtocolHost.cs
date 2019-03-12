using Borlay.Handling;
using Borlay.Injection;
using Borlay.Serialization.Converters;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public class ProtocolHost
    {
        public event Action<ProtocolHost, IResolver, IResolverSession> ClientConnected = (h, r, s) => { };
        public event Action<ProtocolHost, IResolver, IResolverSession, AggregateException> ClientDisconnected = (h, r, s, e) => { };
        public event Action<ProtocolHost, Exception> Exception = (h, e) => { };

        private Resolver resolver;
        private HandlerProvider handler;
        private Serializer converter;
        private volatile bool initialized = false;

        public Resolver Resolver => resolver;
        public HandlerProvider HandlerProvider => handler;
        public Serializer Serializer => converter;

        public ProtocolHost()
        {
            resolver = new Resolver();
        }

        public ProtocolHost(IResolver parent)
        {
            resolver = new Resolver(parent);
        }

        public Task StartServerAsync(string ipString, int port, CancellationToken cancellationToken)
        {
            return StartServerAsync(ipString, port, true, cancellationToken);
        }

        public async Task StartServerAsync(string ipString, int port, bool keepSession, CancellationToken cancellationToken)
        {
            if (!initialized)
                throw new Exception($"Call InitializeFromReference first");

            var ipAddress = IPAddress.Parse(ipString);
            var listener = new TcpListener(ipAddress, port);
            listener.Start();
            try
            {
                using (var register = cancellationToken.Register(() => listener.Stop()))
                {
                    do
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        var localResolver = new Resolver(resolver);
                        localResolver.Register(() => new Tuple<TcpClient, Action>(client, () => client.Dispose()));
                        var session = localResolver.CreateSession();
                        try
                        {
                            var listenTask = ClientListenAsync(localResolver, session, keepSession, cancellationToken);
                        }
                        catch
                        {
                        }
                    } while (!cancellationToken.IsCancellationRequested);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        public Task<IResolverSession> StartClientAsync(string host, int port)
        {
            return StartClientAsync(host, port, CancellationToken.None);
        }

        public Task<IResolverSession> StartClientAsync(string host, int port, CancellationToken cancellationToken)
        {
            return StartClientAsync(host, port, true, cancellationToken);
        }

        public async Task<IResolverSession> StartClientAsync(string host, int port, bool keepSession, CancellationToken cancellationToken)
        {
            if (!initialized)
                throw new Exception($"Call InitializeFromReference first");

            var tcpClient = new TcpClient();
            var localResolver = new Resolver(resolver);
            localResolver.Register(() => new Tuple<TcpClient, Action>(tcpClient, () => tcpClient.Dispose()));

            var session = localResolver.CreateSession();

            var client = session.Resolve<TcpClient>();
            await client.ConnectAsync(host, port);

            var task = ClientListenAsync(localResolver, session, keepSession, cancellationToken);

            return session;
        }

        protected Task ClientListenAsync(Resolver localResolver, IResolverSession session, bool keepSession, CancellationToken cancellationToken)
        {
            try
            {
                var client = session.Resolve<TcpClient>();
                var packetStream = new PacketStream(client.GetStream());
                var protocol = new ProtocolStream(packetStream, converter, handler);

                localResolver.Register(protocol);
                protocol.Resolver = localResolver;


                if (keepSession)
                    localResolver.Register(session);

                ClientConnected(this, localResolver, session);


                var listenTask = protocol.ListenAsync(cancellationToken);

                listenTask.ContinueWith((t) =>
                {
                    try
                    {
                        ClientDisconnected(this, localResolver, session, t.Exception);
                    }
                    catch(Exception e)
                    {
                        OnException(e);
                    }
                    finally
                    {
                        if (!session.TryDispose(out var ex))
                            OnException(ex);

                        
                    }
                });
                return listenTask;
            }
            catch(Exception e)
            {
                if (!session.TryDispose(out var ex))
                    OnException(ex);
                OnException(e);
                throw;
            }
        }

        protected virtual void OnException(Exception e)
        {
            try
            {
                Exception(this, e);
            }
            catch { };
        }

        public void InitializeFromReference<T>()
        {
            if (initialized)
                throw new Exception($"Already initialized");

            
            resolver.LoadFromReference<T>();

            handler = new HandlerProvider(resolver);
            handler.LoadFromReference<T>();

            converter = new Serializer();
            converter.LoadFromReference<T>();

            resolver.Register(handler);
            resolver.Register(converter);

            initialized = true;
        }
    }
}
