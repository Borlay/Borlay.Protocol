using Borlay.Arrays;

namespace Borlay.Protocol
{
    public static class ProtocolStreamExtensions
    {
        public static void InsertLength(this byte[] bytes, int length)
        {
            bytes.AddBytes<ushort>((ushort)length, 2, 0);
        }
    } 
}
