using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol.Injections
{
    public interface ISecurityInject
    {
        //void SendSecurity(ConverterHeader ConverterHeader, byte[] data, int headerEndIndex, ref int index);

        //void ReceiveSecurity(ConverterHeader ConverterHeader, byte[] data, int headerEndIndex, ref int index);

        void SendSecurity(SecurityInjectContext securityInjectContext);

        void ReceiveSecurity(SecurityInjectContext securityInjectContext);
    }

    public class SecurityInjectContext
    {
        public ConverterHeader ConverterHeader { get; internal set; }

        public RequestHeader RequestHeader { get; internal set; }

        public int HeaderEndIndex { get; internal set; }

        public byte[] Data { get; internal set; }

        public int Length { get; set; }
    }
}
