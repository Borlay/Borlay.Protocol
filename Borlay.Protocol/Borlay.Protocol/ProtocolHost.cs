using Borlay.Handling;
using Borlay.Injection;
using Borlay.Serialization;
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
    public class ProtocolHost : IDisposable
    {
        public event Action<ProtocolHost, IResolverSession> Connected = (h, s) => { };
        public event Action<ProtocolHost, IResolverSession, Exception> Disconnected = (h, s, e) => { };

        public event Action<ProtocolHost, IResolverSession> ClientConnected = (h, s) => { };
        public event Action<ProtocolHost, IResolverSession, Exception> ClientDisconnected = (h, s, e) => { };
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
                            ClientListenAsync(client, session, false, cancellationToken);
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
            ClientListenAsync(client, session, true, cancellationToken);

            return session;
        }

        protected async void ClientListenAsync(TcpClient client, IResolverSession session, bool isClient, CancellationToken cancellationToken)
        {
            try
            {
                if (client.Client.RemoteEndPoint is IPEndPoint ep)
                {
                    var epInfo = new EndpointInfo()
                    {
                        Address = ep.Address.ToString(),
                        Port = ep.Port,
                    };
                    session.Resolver.Register(epInfo);
                }


                var packetStream = new PacketStream(client.GetStream());
                var protocol = new SocketProtocolHandler(session, packetStream, Serializer, HandlerProvider);

                session.Resolver.Register(protocol);

                if(!isClient)
                    ClientConnected(this, session);
                else
                    Connected(this, session);

                await protocol.ListenAsync(cancellationToken);
            }
            catch (Exception e)
            {
                if (!session.TryDispose(out var ex))
                    TryOnException(ex);

                TryOnException(e);

                try
                {
                    if (!isClient)
                        ClientDisconnected(this, session, e);
                    else
                        Disconnected(this, session, e);

                }
                catch (Exception de)
                {
                    TryOnException(de);
                }
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

        protected virtual void TryOnException(Exception e)
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
            
            if(Resolver.TryResolveSingletone<IMethodContextInfoProvider>(out var contextProvider))
            {
                HandlerProvider = new HandlerProvider(contextProvider);
                Resolver.Register(HandlerProvider);
            }

            HandlerProvider.LoadFromReference<T>();
            Serializer.LoadFromReference<T>();

            loaded = true;
        }

        public void Dispose()
        {
            Resolver.Dispose();
        }
    }

    public class EndpointInfo
    {
        public string Address { get; set; }

        public int Port { get; set; }
    }
}
