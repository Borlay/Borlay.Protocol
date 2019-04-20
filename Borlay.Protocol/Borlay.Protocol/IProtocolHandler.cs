using Borlay.Arrays;
using Borlay.Handling;
using Borlay.Injection;
using Borlay.Protocol.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public interface IProtocolHandler
    {
        Task<DataContent> HandleDataAsync(IResolverSession session, DataContent dataContent, CancellationToken cancellationToken);
    }

    // todo test for ProtocolInterfaceHandler + MethodProtocolHandler

    // todo repositories:
    // Borlay.Protocol.Handling

    // Borlay.Protocol.Interfaces
    // Borlay.Protocol.Methods
    // Borlay.Handling.Interfaces
    // Borlay.Handling.Methods

    // Borlay.Protocol.Host
}
