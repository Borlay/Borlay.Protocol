using Borlay.Arrays;
using Borlay.Handling;
using Borlay.Handling.Notations;
using Borlay.Injection;
using Borlay.Protocol.Converters;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public class ProtocolInterfaceHandler<TActAs> : IInterfaceHandler
    {
        protected readonly IProtocolHandler protocolHandler;
        protected readonly IResolverSession resolverSession;

        protected readonly TypeContext context;

        public bool IsAsync => true;

        public volatile static int ts;

        public ProtocolInterfaceHandler(IProtocolHandler protocolHandler, ITypeContextProvider contextProvider, IResolverSession resolverSession)
        {
            if (protocolHandler == null)
                throw new ArgumentNullException(nameof(protocolHandler));

            if (resolverSession == null)
                throw new ArgumentNullException(nameof(resolverSession));

            this.protocolHandler = protocolHandler;
            this.resolverSession = resolverSession;

            context = contextProvider.GetTypeContext(typeof(TActAs));
        }

        public object HandleAsync(string methodName, byte[] actionHashBytes, object[] args)
        {
            var watch = Stopwatch.StartNew();

            var stop = ProtocolWatch.Start("handle-async");

            var actionHash = new ByteArray(actionHashBytes);
            var methodContext = context.GetMethodContext(actionHash);

            CancellationToken cancellationToken;
            if (methodContext.CancellationIndex >= 0)
                cancellationToken = (CancellationToken)args[methodContext.CancellationIndex];
            else
            {
                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                cancellationToken = cts.Token;
            }

            stop();

            stop = ProtocolWatch.Start("handle-send-request");

            var actionHashDataContext = new DataContext()
            {
                DataFlag = DataFlag.ActionHash,
                Data = actionHash,
            };

            var additionalCount = 1;

            var argumentContexts = new DataContext[methodContext.ArgumentIndexes.Length + additionalCount]; // { actionDataContext, dataDataContext };

            argumentContexts[0] = actionHashDataContext;
 
            for (int i = 0; i < methodContext.ArgumentIndexes.Length; i++)
            {
                argumentContexts[i + additionalCount] = new DataContext()
                {
                    DataFlag = DataFlag.Data,
                    Data = args[methodContext.ArgumentIndexes[i]],
                };
            }

            var dataContent = new DataContent(argumentContexts);

            var result = protocolHandler.HandleDataAsync(resolverSession, dataContent, cancellationToken); // todo change to Add session
            stop();
            stop = ProtocolWatch.Start("handle-async");

            var tcsType = methodContext.TaskCompletionSourceType;
            var task = WrapTask(result, tcsType, methodContext.ContextInfo.MethodInfo.ReturnType.GenericTypeArguments.Length > 0);

            stop();
            watch.Stop();
            ts += (int)watch.Elapsed.Ticks;

            return task;
        }

        protected virtual object WrapTask(Task<DataContent> result, Type tcsType, bool hasResult)
        {
            var tcs = Activator.CreateInstance(tcsType);

            result.ContinueWith(t =>
            {
                try
                {
                    if (t.IsFaulted)
                    {
                        tcsType
                        .GetRuntimeMethod("TrySetException", new Type[] { typeof(Exception) })
                        .Invoke(tcs, new object[] { t.Exception.InnerException });
                    }
                    else if (t.IsCanceled)
                    {
                        tcsType
                        .GetRuntimeMethod("TrySetCanceled", new Type[] { })
                        .Invoke(tcs, new object[] { });
                    }
                    else
                    {
                        var response = t.Result[DataFlag.Data].SingleOrDefault()?.Data;
                        if (response is ErrorResponse errorResponse)
                        {
                            var exception = new ResponseProtocolException(errorResponse);
                            tcsType
                                .GetRuntimeMethod("TrySetException", new Type[] { typeof(Exception) })
                                .Invoke(tcs, new object[] { exception });
                        }
                        else if(response == null)
                        {
                            tcsType
                                .GetRuntimeMethod("TrySetResult", tcsType.GenericTypeArguments)
                                .Invoke(tcs, new object[] { true });
                        }
                        else
                        {
                            if (!tcsType.GenericTypeArguments[0].GetTypeInfo().IsAssignableFrom(response.GetType()))
                                throw new ProtocolException(ErrorCode.UnknownResponse);

                            tcsType
                                .GetRuntimeMethod("TrySetResult", tcsType.GenericTypeArguments)
                                .Invoke(tcs, new object[] { response });
                        }
                    }
                }
                catch (Exception e)
                {
                    tcsType
                        .GetRuntimeMethod("TrySetException", new Type[] { typeof(Exception) })
                        .Invoke(tcs, new object[] { e });
                }
            });

            var task = tcsType.GetRuntimeMethod("get_Task", new Type[0]).Invoke(tcs, null);
            return task;
        }
    }
}
