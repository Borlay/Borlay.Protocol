using Borlay.Injection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public interface IChannelProvider : IDisposable
    {
        Task<TInterface> GetChannelAsync<TInterface>(bool force = false) where TInterface : class;
        bool HashChannel<TInterface>();
    }

    public class ChannelProvider : IChannelProvider
    {
        private readonly Dictionary<Type, object> channels = new Dictionary<Type, object>();

        private readonly string hostOrIp;
        private readonly int port;

        public ProtocolHost Host { get; }
        private IResolverSession session;

        private readonly SemaphoreSlim slim = new SemaphoreSlim(1);

        public ChannelProvider(string hostOrIp, int port)
        {
            this.hostOrIp = hostOrIp;
            this.port = port;

            Host = new ProtocolHost();
        }

        public virtual void LoadFromReference<T>()
        {
            Host.LoadFromReference<T>();
        }

        private async Task ConnectAsync()
        {
            TryDispose();
            session = await Host.StartClientAsync(hostOrIp, port, CancellationToken.None);
        }


        public async Task<TInterface> GetChannelAsync<TInterface>(bool force = false) where TInterface : class
        {
            if (!HashChannel<TInterface>())
                ThrowNotFound<TInterface>();

            await slim.WaitAsync();
            try
            {
                if (session == null || session.IsDisposed || force)
                {
                    channels.Clear();
                    await ConnectAsync();
                }

                if (channels.TryGetValue(typeof(TInterface), out var value))
                    return (TInterface)value;

                var channel = session.CreateChannel<TInterface>();
                channels[typeof(TInterface)] = channel;
                return channel;
            }
            finally
            {
                slim.Release();
            }
        }

        public virtual bool HashChannel<TInterface>()
        {
            return true;
        }

        protected virtual void ThrowNotFound<TInterface>()
        {
            throw new KeyNotFoundException($"Channel for interface {typeof(TInterface)} not found");
        }

        private void TryDispose()
        {
            try
            {
                session?.Dispose();
                session = null;
            }
            catch
            {
                // do nothing
            }
        }

        public void Dispose()
        {
            slim.Wait();
            try
            {
                session?.Dispose();
                session = null;
            }
            finally
            {
                slim.Release();
            }
        }
    }

    public class ChannelProviderCollection : IChannelProvider
    {
        public IChannelProvider[] Providers { get; }

        public ChannelProviderCollection(params IChannelProvider[] factories)
        {
            this.Providers = factories;
        }

        public async Task<TInterface> GetChannelAsync<TInterface>(bool force = false) where TInterface : class
        {
            foreach (var provider in Providers)
            {
                if (provider.HashChannel<TInterface>())
                    return await provider.GetChannelAsync<TInterface>();
            }

            throw new KeyNotFoundException($"Channel for interface {typeof(TInterface)} not found");
        }

        public bool HashChannel<TInterface>()
        {
            foreach (var provider in Providers)
            {
                if (provider.HashChannel<TInterface>())
                    return true;
            }

            return false;
        }

        public void Dispose()
        {
            List<Exception> exceptions = new List<Exception>();
            foreach (var factory in Providers)
            {
                try
                {
                    factory.Dispose();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 0)
                throw new AggregateException(exceptions);
        } 
    }

}
