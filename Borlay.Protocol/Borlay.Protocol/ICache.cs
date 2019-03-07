using Borlay.Arrays;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    public interface ICache
    {
        void AddData(ByteArray key, byte[] bytes, int offset, int count);

        bool TryGetData(ByteArray key, out byte[] bytes);

        bool RemoveData(ByteArray key);

        bool TryRemoveFirst(out KeyValuePair<ByteArray, byte[]> keyValue);

        bool Contains(ByteArray key);
    }

    public class Cache : ICache
    {
        public const int DefaultCacheSize = 100000;

        protected readonly Dictionary<ByteArray, byte[]> cache = new Dictionary<ByteArray, byte[]>();
        protected readonly Queue<ByteArray> keyQueue = new Queue<ByteArray>();

        protected readonly int cacheSize;

        public Cache()
            : this(DefaultCacheSize)
        {
        }

        public Cache(int cacheSize)
        {
            this.cacheSize = cacheSize;
        }

        public virtual bool Contains(ByteArray key)
        {
            lock (cache)
            {
                return cache.ContainsKey(key);
            }
        }

        public virtual void AddData(ByteArray key, byte[] responseBytes, int offset, int count)
        {
            lock (cache)
            {
                var data = new byte[count];
                Array.Copy(responseBytes, offset, data, 0, count);
                cache[key] = data;

                keyQueue.Enqueue(key);

                if (keyQueue.Count > cacheSize)
                {
                    var removeKey = keyQueue.Dequeue();
                    cache.Remove(removeKey);
                }
            }
        }

        public virtual bool TryGetData(ByteArray key, out byte[] bytes)
        {
            lock (cache)
            {
                return cache.TryGetValue(key, out bytes);
            }
        }

        public virtual bool TryRemoveFirst(out KeyValuePair<ByteArray, byte[]> keyValue)
        {
            lock(cache)
            {
                while(keyQueue.Count > 0)
                {
                    var key = keyQueue.Dequeue();
                    if(cache.TryGetValue(key, out var value))
                    {
                        cache.Remove(key);
                        keyValue = new KeyValuePair<ByteArray, byte[]>(key, value);
                        return true;
                    }
                }
                return false;
            }
        }

        public virtual bool RemoveData(ByteArray key)
        {
            lock(cache)
            {
                return cache.Remove(key);
            }
        }
    }
}
