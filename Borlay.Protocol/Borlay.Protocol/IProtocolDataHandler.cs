using Borlay.Arrays;
using Borlay.Handling;
using Borlay.Injection;
using Borlay.Protocol.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public interface IProtocolDataHandler
    {
        Task<DataContext[]> HandleDataAsync(IResolverSession session, DataContext[] contexts, CancellationToken cancellationToken);
    }

    // todo repositories:
    // Borlay.Protocol.Handling

    // Borlay.Protocol.Interfaces
    // Borlay.Protocol.Methods
    // Borlay.Handling.Interfaces
    // Borlay.Handling.Methods

    public class HandlerProviderDataHandler : IProtocolDataHandler
    {
        private readonly IHandlerProvider handlerProvider;

        public async Task<DataContext[]> HandleDataAsync(IResolverSession session, DataContext[] contextArray, CancellationToken cancellationToken)
        {
            var contexts = contextArray.ToLookup(c => c.DataFlag);

            var actionId = contexts[DataFlag.Action].First()?.Data;
            var scopeId = contexts[DataFlag.Scope].FirstOrDefault()?.Data;
            var methodHash = contexts[DataFlag.MethodHash].FirstOrDefault()?.Data;
            var request = contexts[DataFlag.Data].Select(d => d.Data).ToArray();

            if (methodHash == null || !(methodHash is ByteArray mhash))
                throw new ProtocolException("Parameter hash is null or not ByteArray", ErrorCode.BadRequest);

            if (!handlerProvider.TryGetHandler(scopeId ?? "", actionId ?? "", mhash, out var handlerItem))
                throw new ProtocolException($"Handler for scope {scopeId} action {actionId} hash {methodHash} not found", ErrorCode.BadRequest);

            object response = await handlerItem.HandleAsync(session, request, cancellationToken);
            if (response == null)
            {
                response = new EmptyResponse();
            }

            return new DataContext[]
            {
                new DataContext()
                {
                    Data = response,
                    DataFlag = DataFlag.Data,
                }
            };
        }
    }
}
