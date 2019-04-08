using System;
using Borlay.Arrays;
using Borlay.Serialization.Converters;

namespace Borlay.Protocol.Converters
{
    public class RequestHeaderConverter : IConverter
    {
        public void AddBytes(object obj, byte[] bytes, ref int index)
        {
            var header = (RequestHeader)obj;

            bytes[index++] = (byte)(header.RequestType == RequestType.Request ? 1 : 2);
            bytes[index++] = (byte)(header.CanBeCached ? 1 : 0);
            bytes[index++] = header.RezervedFlag; // additional flags
            bytes.AddBytes<int>(header.RequestId, 4, ref index); // request id
        }

        public object GetObject(byte[] bytes, ref int index)
        {
            var header = new RequestHeader()
            {
                RequestType = (RequestType)bytes[index++],
                CanBeCached = bytes[index++] == 1 ? true : false,
                RezervedFlag = bytes[index++],
                RequestId = bytes.GetValue<int>(4, ref index)
            };
            return header;
        }

        public Type GetType(byte[] bytes, int index)
        {
            return typeof(RequestHeader);
        }
    }
}
