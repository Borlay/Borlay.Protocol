using Borlay.Arrays;
using Borlay.Protocol.Converters;
using System.Linq;

namespace Borlay.Protocol
{
    public static class ProtocolStreamExtensions
    {
        public static void InsertLength(this byte[] bytes, int length)
        {
            bytes.AddBytes<ushort>((ushort)length, 2, 0);
        }

        public static T[] Resolve<T>(this ILookup<DataFlag, DataContext> lookup, DataFlag dataFlag)
        {
            var result = lookup[dataFlag].Select(c => c.Data).OfType<T>().ToArray();
            return result;
        }

        public static T ResolveSingle<T>(this ILookup<DataFlag, DataContext> lookup, DataFlag dataFlag)
        {
            if (lookup.TryResolveSingle<T>(dataFlag, out var data))
                return data;

            throw new ProtocolException($"Data for flag '{dataFlag}' not found or found more than 1", ErrorCode.DataNotFound);
        }

        public static bool TryResolve<T>(this ILookup<DataFlag, DataContext> lookup, DataFlag dataFlag, out T[] data)
        {
            data = lookup[dataFlag].Select(c => c.Data).OfType<T>().ToArray();
            return data.Length > 0;
        }

        public static bool TryResolveSingle<T>(this ILookup<DataFlag, DataContext> lookup, DataFlag dataFlag, out T data)
        {
            var array = lookup[dataFlag].Select(c => c.Data).OfType<T>().ToArray();
            data = array.FirstOrDefault();
            return array.Length == 1;
        }
    } 
}
