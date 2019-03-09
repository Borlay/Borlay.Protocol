using Borlay.Handling;
using Borlay.Handling.Notations;
using Borlay.Injection;
using Borlay.Protocol.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Borlay.Protocol
{
    public class ProtocolHandler<TActAs> : IInterfaceHandler
    {
        protected readonly TypeInfo actAsType;
        protected readonly IRequestAsync requestAsync;
        protected readonly Dictionary<string, MethodMetadata[]> methods = new Dictionary<string, MethodMetadata[]>();

        public bool IsAsync => true;

        public volatile static int ts;

        public ProtocolHandler(IRequestAsync requestAsync)
        {
            if (requestAsync == null)
                throw new ArgumentNullException(nameof(requestAsync));

            this.requestAsync = requestAsync;

            this.actAsType = typeof(TActAs).GetTypeInfo();
            var methodGroups = actAsType.GetMethods()
                .Where(m => m.GetCustomAttribute<ActionAttribute>(true) != null).GroupBy(m => m.Name);

            foreach (var g in methodGroups)
            {
                var methodMeta = g.Select(m =>
                {
                    if (!typeof(Task).GetTypeInfo().IsAssignableFrom(m.ReturnType))
                        throw new ArgumentNullException($"Method '{m.Name}' return type should be Task based.");

                    var parameters = m.GetParameters();
                    var types = parameters.Select(p => p.ParameterType).ToArray();
                    var actionAttr = m.GetCustomAttribute<ActionAttribute>(true);

                    //var index = -1;
                    var ctIndex = -1;

                    var argumentIndexes = new List<int>();
                    var argumentTypes = new List<Type>();

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var param = parameters[i];
                        var type = param.ParameterType;
                        var typeInfo = param.ParameterType.GetTypeInfo();
                        if (
                        param.GetCustomAttribute<InjectAttribute>(true) == null
                            &&
                            typeInfo.GetCustomAttribute<InjectAttribute>(true) == null
                            &&
                            param.ParameterType != typeof(CancellationToken)
                            &&
                            !typeof(IResolver).GetTypeInfo().IsAssignableFrom(param.ParameterType)
                        )
                        {
                            argumentIndexes.Add(i);
                            argumentTypes.Add(type);
                        }

                        if (type == typeof(CancellationToken))
                            ctIndex = i;
                    }

                    var tcsType = typeof(TaskCompletionSource<>);
                    var retType = m.ReturnType.GenericTypeArguments.FirstOrDefault() ?? typeof(bool);
                    var tcsGenType = tcsType.MakeGenericType(retType);

                    var meta = new MethodMetadata()
                    {
                        ParameterTypes = types,
                        ArgumentIndexes = argumentIndexes.ToArray(),
                        ArgumentTypes = argumentTypes.ToArray(),
                        CancellationIndex = ctIndex,
                        ReturnType = m.ReturnType,
                        ActionMeta = actionAttr,
                        TaskCompletionSourceType = tcsGenType
                    };
                    return meta;
                })
                //.Where(m => m.ArgumentIndex >= 0)
                .ToArray();

                methods.Add(g.Key, methodMeta);
            }
        }

        public object HandleAsync(string methodName, object[] args)
        {
            var watch = Stopwatch.StartNew();

            var stop = ProtocolWatch.Start("handle-async");

            if (!methods.TryGetValue(methodName, out var methodMetadatas))
                throw new KeyNotFoundException($"Method for name '{methodName}' not found");

            Func<Type[], bool> equal = (types) =>
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i].GetType() != types[i])
                        return false;
                return true;
            };

            var metaData = methodMetadatas
                .FirstOrDefault(m => m.ParameterTypes.Length == args.Length && equal(m.ParameterTypes));

            if(metaData == null)
                throw new KeyNotFoundException($"Method for name '{methodName}' not found");

            CancellationToken cancellationToken;
            if (metaData.CancellationIndex >= 0)
                cancellationToken = (CancellationToken)args[metaData.CancellationIndex];
            else
            {
                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                cancellationToken = cts.Token;
            }

            stop();

            stop = ProtocolWatch.Start("handle-send-request");

            var actionDataContext = new DataContext()
            {
                DataFlag = DataFlag.Action,
                Data = metaData.ActionMeta.GetActionId(),
            };

            var argumentContexts = new DataContext[metaData.ArgumentIndexes.Length + 1]; // { actionDataContext, dataDataContext };

            argumentContexts[0] = actionDataContext;
            for (int i = 0; i < metaData.ArgumentIndexes.Length; i++)
            {
                argumentContexts[i + 1] = new DataContext()
                {
                    DataFlag = DataFlag.Data,
                    Data = args[metaData.ArgumentIndexes[i]],
                };
            }

            var result = requestAsync.SendRequestAsync(argumentContexts, cancellationToken);
            stop();
            stop = ProtocolWatch.Start("handle-async");

            var tcsType = metaData.TaskCompletionSourceType;
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
                        if (metaData.ReturnType.GenericTypeArguments.Length == 0)
                        {
                            tcsType
                                .GetRuntimeMethod("TrySetResult", tcsType.GenericTypeArguments)
                                .Invoke(tcs, new object[] { true });
                        }
                        else
                        {
                            if (!tcsType.GenericTypeArguments[0].GetTypeInfo().IsAssignableFrom(t.Result.GetType()))
                                throw new ProtocolException(ErrorCode.UnknownResponse);

                            tcsType
                                .GetRuntimeMethod("TrySetResult", tcsType.GenericTypeArguments)
                                .Invoke(tcs, new object[] { t.Result });
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

            stop();
            watch.Stop();
            ts += (int)watch.Elapsed.Ticks;

            return task;
        }
    }

    public class MethodMetadata
    {
        public Type[] ParameterTypes { get; set; }

        public IActionMeta ActionMeta { get; set; }

        public int[] ArgumentIndexes { get; set; }

        public Type[] ArgumentTypes { get; set; }

        public int CancellationIndex { get; set; }

        public Type ReturnType { get; set; }

        public Type TaskCompletionSourceType { get; set; }
    }
}
