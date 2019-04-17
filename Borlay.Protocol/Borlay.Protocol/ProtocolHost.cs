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

        private volatile bool loaded = false;

        public Resolver Resolver { get; }
        public HandlerProvider HandlerProvider { get; private set; }
        public Serializer Serializer { get; private set; }

        public ProtocolHost()
        {
            Resolver = new Resolver();
            Initialize();
        }

        public ProtocolHost(IResolver parent)
        {
            Resolver = new Resolver(parent);
            Initialize();
        }

        public async Task StartServerAsync(string ipString, int port, CancellationToken cancellationToken)
        {
            if (!loaded)
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
                            var session = Resolver.CreateSession();
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
            if (!loaded)
                throw new Exception($"Call InitializeFromReference first");

            var client = new TcpClient();
            var session = Resolver.CreateSession();
            session.Resolver.Register(client, false);
            session.Resolver.AddDisposable(client);

            await client.ConnectAsync(host, port);
            var task = ClientListenAsync(client, session, true, cancellationToken);

            return session;
        }

        protected Task ClientListenAsync(TcpClient client, IResolverSession session, bool isClient, CancellationToken cancellationToken)
        {
            try
            {
                var packetStream = new PacketStream(client.GetStream());
                var protocol = new SocketProtocolHandler(session, packetStream, Serializer, HandlerProvider);

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

        public bool RegisterHandler<T>(bool isSingletone)
        {
            return RegisterHandler(typeof(T), isSingletone);
        }

        public bool RegisterHandler(Type type, bool isSingletone)
        {
            if (HandlerProvider.RegisterHandler(type))
            {
                Resolver.Register(type, true, isSingletone);
                return true;
            }
            return false;
        }

        public bool RegisterHandler(object instance, bool includeBase)
        {
            if (HandlerProvider.RegisterHandler(instance.GetType()))
            {
                Resolver.Register(instance, includeBase);
                return true;
            }
            return false;
        }

        protected virtual void OnException(Exception e)
        {
            try
            {
                Exception(this, e);
            }
            catch { };
        }

        private void Initialize()
        {
            HandlerProvider = new HandlerProvider();
            Serializer = new Serializer();

            Resolver.Register(HandlerProvider);
            Resolver.Register(Serializer);
        }

        public void LoadFromReference<T>()
        {
            if (loaded)
                throw new Exception($"Already initialized");

            Resolver.LoadFromReference<T>();
            HandlerProvider.LoadFromReference<T>();
            Serializer.LoadFromReference<T>();

            loaded = true;
        }
    }
}
