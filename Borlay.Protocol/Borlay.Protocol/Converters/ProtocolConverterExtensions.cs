
namespace Borlay.Protocol.Converters
{
    public static class ProtocolConverterExtensions
    {
        public static T Resolve<T>(this IProtocolConverter protocolConverter, byte[] source, ref int index)
        {
            var dataContext = protocolConverter.Resolve(source, ref index);
            return (T)dataContext.Data;
        }

        public static T Resolve<T>(this IProtocolConverter protocolConverter, byte[] source, ref int index, DataFlag checkDataFlag)
        {
            var dataContext = protocolConverter.Resolve(source, ref index);

            if (dataContext.DataFlag != checkDataFlag)
                throw new ProtocolException($"Data flag should be '{checkDataFlag}' bus is ''{dataContext.DataFlag}", ErrorCode.BadRequest);

            return (T)dataContext.Data;
        }

        public static void ApplyHeader(this IProtocolConverter protocolConverter,
            byte[] destination, ref int index, object data)
        {
            Apply(protocolConverter, destination, ref index, data, DataFlag.Header);
        }

        public static void ApplyData(this IProtocolConverter protocolConverter,
            byte[] destination, ref int index, object data)
        {
            Apply(protocolConverter, destination, ref index, data, DataFlag.Data);
        }

        public static void Apply(this IProtocolConverter protocolConverter,
            byte[] destination, ref int index, object data, DataFlag dataFlag)
        {
            protocolConverter.Apply(destination, ref index, new DataContext()
            {
                Data = data,
                DataFlag = dataFlag
            });
        }
    }
}
