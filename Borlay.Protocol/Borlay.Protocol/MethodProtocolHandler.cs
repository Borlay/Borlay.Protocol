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
    public class MethodProtocolHandler : IProtocolHandler
    {
        private readonly IHandlerProvider handlerProvider;
        private readonly IResolverSession resolverSession;


        public MethodProtocolHandler(IHandlerProvider handlerProvider, IResolverSession resolverSession)
        {
            if (handlerProvider == null)
                throw new ArgumentNullException(nameof(handlerProvider));

            if (resolverSession == null)
                throw new ArgumentNullException(nameof(resolverSession));

            this.handlerProvider = handlerProvider;
            //this.resolverSession = resolverSession;
        }

        public async Task<DataContent> HandleDataAsync(IResolverSession session, DataContent dataContent, CancellationToken cancellationToken)
        {
            var actionHashData = dataContent[DataFlag.ActionHash].First()?.Data;
            var request = dataContent[DataFlag.Data].Select(d => d.Data).ToArray();

            if (actionHashData == null || !(actionHashData is ByteArray actionHash))
                throw new ProtocolException("Parameter hash is null or not ByteArray", ErrorCode.BadRequest);

            if (!handlerProvider.TryGetHandler(actionHash, out var handlerItem))
                throw new ProtocolException($"Handler for action hash '{actionHash}' not found", ErrorCode.BadRequest);

            object response = await handlerItem.HandleAsync(session ?? resolverSession, request, cancellationToken);
            if (response == null)
                return new DataContent();

            return new DataContent(new DataContext()
            {
                Data = response,
                DataFlag = DataFlag.Data
            });
        }
    }
}
