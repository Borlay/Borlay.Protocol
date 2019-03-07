using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol
{
    public class RequestHeader
    {
        public RequestType RequestType { get; set; }
        public bool CanBeCached { get; set; }
        public byte RezervedFlag { get; set; }
        public int RequestId { get; set; }
    }

    public class ConverterHeader
    {
        public byte VersionMajor { get; set; }
        public byte VersionMinor { get; set; }

        public byte Encryption { get; set; }
        public byte Compression { get; set; }

        public byte FlagMajor { get; set; }
        public byte FlagMinor { get; set; }
    }

    public enum RequestType : byte
    {
        Request = 1,
        Response = 2
    }
}
