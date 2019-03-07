using Borlay.Arrays;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    public interface IRequestKeyCache
    {
        void SaveRequestKey(int requestId, ByteArray key);

        bool TryGetRequestKey(int requestId, out ByteArray key);

        bool TryRemoveRequestKey(int requestId, out ByteArray key);

        bool RemoveRequestKey(int requestId);

        ByteArray GetKey(byte[] bytes, int offset, int count);
    }

    public class RequestKeyCache : IRequestKeyCache
    {
        protected readonly ConcurrentDictionary<int, ByteArray> requestKeys = new ConcurrentDictionary<int, ByteArray>();

        public virtual ByteArray GetKey(byte[] bytes, int offset, int count)
        {
            var keyBytes = new byte[count];
            Array.Copy(bytes, offset, keyBytes, 0, count);
            return new ByteArray(keyBytes);
        }

        public virtual bool TryGetRequestKey(int requestId, out ByteArray key)
        {
            return requestKeys.TryGetValue(requestId, out key);
        }

        public virtual bool TryRemoveRequestKey(int requestId, out ByteArray key)
        {
            return requestKeys.TryRemove(requestId, out key);
        }

        public virtual bool RemoveRequestKey(int requestId)
        {
            return requestKeys.TryRemove(requestId, out var key);
        }

        public virtual void SaveRequestKey(int requestId, ByteArray key)
        {
            requestKeys[requestId] = key;
        }
    }
}
