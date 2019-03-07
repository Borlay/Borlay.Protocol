using Borlay.Serialization.Converters;

namespace Borlay.Protocol.Converters
{
    public class ConverterHeaderConverter : IConverter
    {
        public void AddBytes(object obj, byte[] bytes, ref int index)
        {
            var header = (ConverterHeader)obj;

            bytes[index++] = header.VersionMajor;
            bytes[index++] = header.VersionMinor;
            bytes[index++] = header.Encryption;
            bytes[index++] = header.Compression;
            bytes[index++] = header.FlagMajor; // additional flags
            bytes[index++] = header.FlagMinor; // additional flags

        }

        public object GetObject(byte[] bytes, ref int index)
        {
            var header = new ConverterHeader()
            {
                VersionMajor = bytes[index++],
                VersionMinor = bytes[index++],
                Encryption = bytes[index++],
                Compression = bytes[index++],
                FlagMajor = bytes[index++],
                FlagMinor = bytes[index++],
            };
            return header;
        }
    }
}
