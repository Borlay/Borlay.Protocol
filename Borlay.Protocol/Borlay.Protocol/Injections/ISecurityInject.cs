using Borlay.Injection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol.Injections
{
    public interface ISecurityInject
    {
        void SendSecurity(IResolver resolver, SecurityInjectContext securityInjectContext);

        void ReceiveSecurity(IResolver resolver, SecurityInjectContext securityInjectContext);
    }

    public class ActionSecurityInjection : ISecurityInject
    {
        Action<IResolver, SecurityInjectContext> sendSecurity;
        Action<IResolver, SecurityInjectContext> receiveSecurity;

        public ActionSecurityInjection(Action<IResolver, SecurityInjectContext> sendSecurity, Action<IResolver, SecurityInjectContext> receiveSecurity)
        {
            this.sendSecurity = sendSecurity;
            this.receiveSecurity = receiveSecurity;
        }

        public void ReceiveSecurity(IResolver resolver, SecurityInjectContext securityInjectContext)
        {
            receiveSecurity?.Invoke(resolver, securityInjectContext);
        }

        public void SendSecurity(IResolver resolver, SecurityInjectContext securityInjectContext)
        {
            sendSecurity?.Invoke(resolver, securityInjectContext);
        }
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
