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
        public event Action<ProtocolHost, IResolverSession, bool> ClientConnected = (h, s, c) => { };
        public event Action<ProtocolHost, IResolverSession, bool, AggregateException> ClientDisconnected = (h, s, c, e) => { };
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

        public async Task StartServerAsync(string ipString, int port, CancellationToken cancellationToken)
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
                        try
                        {
                            var session = resolver.CreateSession();
                            session.Resolver.Register(client, false);
                            session.Resolver.AddDisposable(client);
                            var listenTask = ClientListenAsync(client, session, false, cancellationToken);
                        }
                        catch
                        {
                            try
                            {
                                client.Dispose();
                            }
                            catch { };
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

        public async Task<IResolverSession> StartClientAsync(string host, int port, CancellationToken cancellationToken)
        {
            if (!initialized)
                throw new Exception($"Call InitializeFromReference first");

            var client = new TcpClient();
            var session = resolver.CreateSession();
            session.Resolver.Register(client, false);
            session.Resolver.AddDisposable(client);
            // todo add to dispose
            //var localResolver = new Resolver(resolver);
            //localResolver.Register((s) => new Tuple<TcpClient, Action>(tcpClient, null));

            //var session = localResolver.CreateSession();

            await client.ConnectAsync(host, port);
            var task = ClientListenAsync(client, session, true, cancellationToken);

            return session;
        }

        protected Task ClientListenAsync(TcpClient client, IResolverSession session, bool isClient, CancellationToken cancellationToken)
        {
            try
            {
                var packetStream = new PacketStream(client.GetStream());
                var protocol = new ProtocolStream(session, packetStream, converter, handler);

                session.Resolver.Register(protocol);


                ClientConnected(this, session, isClient);


                var listenTask = protocol.ListenAsync(cancellationToken);

                listenTask.ContinueWith((t) =>
                {
                    try
                    {
                        ClientDisconnected(this, session, isClient, t.Exception);
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

            handler = new HandlerProvider();
            handler.LoadFromReference<T>();

            converter = new Serializer();
            converter.LoadFromReference<T>();

            resolver.Register(handler);
            resolver.Register(converter);

            initialized = true;
        }
    }
}
