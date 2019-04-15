using Borlay.Injection;
using System;
using System.Collections.Generic;
using System.Text;

namespace Borlay.Protocol.Injection
{
    public interface ISecurityInject
    {
        void SendSecurity(IResolverSession session, SecurityInjectContext securityInjectContext);

        void ReceiveSecurity(IResolverSession session, SecurityInjectContext securityInjectContext);
    }

    public class ActionSecurityInjection : ISecurityInject
    {
        Action<IResolverSession, SecurityInjectContext> sendSecurity;
        Action<IResolverSession, SecurityInjectContext> receiveSecurity;

        public ActionSecurityInjection(Action<IResolverSession, SecurityInjectContext> sendSecurity, Action<IResolverSession, SecurityInjectContext> receiveSecurity)
        {
            this.sendSecurity = sendSecurity;
            this.receiveSecurity = receiveSecurity;
        }

        public void ReceiveSecurity(IResolverSession session, SecurityInjectContext securityInjectContext)
        {
            receiveSecurity?.Invoke(session, securityInjectContext);
        }

        public void SendSecurity(IResolverSession session, SecurityInjectContext securityInjectContext)
        {
            sendSecurity?.Invoke(session, securityInjectContext);
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
