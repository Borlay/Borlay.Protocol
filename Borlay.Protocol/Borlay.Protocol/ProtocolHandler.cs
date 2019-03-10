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
    public class ProtocolHandler<TActAs> : IInterfaceHandler
    {
        protected readonly TypeMetaData typeMetaData;
        protected readonly IRequestAsync requestAsync;

        public bool IsAsync => true;

        public volatile static int ts;

        public ProtocolHandler(IRequestAsync requestAsync)
        {
            if (requestAsync == null)
                throw new ArgumentNullException(nameof(requestAsync));

            this.requestAsync = requestAsync;

            typeMetaData = TypeMetaDataProvider.GetTypeMetaData<TActAs>();
        }

        public object HandleAsync(string methodName, object[] args)
        {
            var watch = Stopwatch.StartNew();

            var stop = ProtocolWatch.Start("handle-async");

            var pTypes = args.Where(a => a != null).Select(a => a.GetType()).ToArray();
            var metaData = typeMetaData.GetMetaData(methodName, pTypes);

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

            var additionalCount = 1;
            if (metaData.ScopeId != null)
                additionalCount = 2;

            var argumentContexts = new DataContext[metaData.ArgumentIndexes.Length + additionalCount]; // { actionDataContext, dataDataContext };

            argumentContexts[0] = actionDataContext;
            if (metaData.ScopeId != null)
            {
                argumentContexts[1] = new DataContext()
                {
                    DataFlag = DataFlag.Scope,
                    Data = metaData.ScopeId
                };
            }
                
            for (int i = 0; i < metaData.ArgumentIndexes.Length; i++)
            {
                argumentContexts[i + additionalCount] = new DataContext()
                {
                    DataFlag = DataFlag.Data,
                    Data = args[metaData.ArgumentIndexes[i]],
                };
            }

            var result = requestAsync.SendRequestAsync(argumentContexts, cancellationToken);
            stop();
            stop = ProtocolWatch.Start("handle-async");

            var tcsType = metaData.TaskCompletionSourceType;
            var task = WrapTask(result, tcsType, metaData.ReturnType.GenericTypeArguments.Length > 0);

            stop();
            watch.Stop();
            ts += (int)watch.Elapsed.Ticks;

            return task;
        }

        protected virtual object WrapTask(Task<object> result, Type tcsType, bool hasResult)
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
                        if (!hasResult)
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
            return task;
        }
    }

    public static class TypeMetaDataProvider
    {
        private readonly static ConcurrentDictionary<TypeInfo, TypeMetaData> typeMetaDatas = new ConcurrentDictionary<TypeInfo, TypeMetaData>();

        public static TypeMetaData GetTypeMetaData<T>()
        {
            var typeInfo = typeof(T).GetTypeInfo();
            return GetTypeMetaData(typeInfo);
        }

        public static TypeMetaData GetTypeMetaData(TypeInfo typeInfo)
        {
            if (typeMetaDatas.TryGetValue(typeInfo, out var typeMetaData))
                return typeMetaData;

            typeMetaData = new TypeMetaData(typeInfo);
            typeMetaDatas[typeInfo] = typeMetaData;
            return typeMetaData;
        }

    }

    public class TypeMetaData
    {
        protected readonly TypeInfo typeInfo;
        protected readonly Dictionary<string, MethodMetadata[]> methods = new Dictionary<string, MethodMetadata[]>();

        public TypeMetaData(TypeInfo type)
        {
            this.typeInfo = type;

            var methodGroups = typeInfo.GetMethods()
                .Where(m => m.GetCustomAttribute<ActionAttribute>(true) != null).GroupBy(m => m.Name);

            var classScopeAttr = this.typeInfo.GetCustomAttribute<ScopeAttribute>(true);

            foreach (var g in methodGroups)
            {
                var methodMeta = g.Select(m =>
                {
                    if (!typeof(Task).GetTypeInfo().IsAssignableFrom(m.ReturnType))
                        throw new ArgumentNullException($"Method '{m.Name}' return type should be Task based.");

                    var parameters = m.GetParameters();
                    var ptypes = parameters.Select(p => p.ParameterType).ToArray();
                    var actionAttr = m.GetCustomAttribute<ActionAttribute>(true);
                    var methodScopeAttr = m.GetCustomAttribute<ScopeAttribute>(true) ?? classScopeAttr;

                    //var index = -1;
                    var ctIndex = -1;

                    var argumentIndexes = new List<int>();
                    var argumentTypes = new List<Type>();

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        var param = parameters[i];
                        var ptype = param.ParameterType;
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
                            argumentTypes.Add(ptype);
                        }

                        if (ptype == typeof(CancellationToken))
                            ctIndex = i;
                    }

                    var tcsType = typeof(TaskCompletionSource<>);
                    var retType = m.ReturnType.GenericTypeArguments.FirstOrDefault() ?? typeof(bool);
                    var tcsGenType = tcsType.MakeGenericType(retType);

                    var meta = new MethodMetadata()
                    {
                        ParameterTypes = ptypes,
                        ArgumentIndexes = argumentIndexes.ToArray(),
                        ArgumentTypes = argumentTypes.ToArray(),
                        CancellationIndex = ctIndex,
                        ReturnType = m.ReturnType,
                        ActionMeta = actionAttr,
                        ScopeId = methodScopeAttr?.GetScopeId(),
                        TaskCompletionSourceType = tcsGenType
                    };
                    return meta;
                })
                .ToArray();

                methods.Add(g.Key, methodMeta);
            }
        }

        public MethodMetadata GetMetaData(string methodName, Type[] types)
        {
            if (!methods.TryGetValue(methodName, out var methodMetadatas))
                throw new KeyNotFoundException($"Method for name '{methodName}' not found");

            Func<Type[], bool> equal = (_types) =>
            {
                for (int i = 0; i < types.Length; i++)
                    if (types[i] != _types[i])
                        return false;
                return true;
            };

            var metaData = methodMetadatas
                .FirstOrDefault(m => m.ParameterTypes.Length == types.Length && equal(m.ParameterTypes));

            if (metaData == null)
                throw new KeyNotFoundException($"Method for name '{methodName}' not found");

            return metaData;
        }

    }

    public class MethodMetadata
    {
        public Type[] ParameterTypes { get; set; }

        public IActionMeta ActionMeta { get; set; }

        public object ScopeId { get; set; }

        public int[] ArgumentIndexes { get; set; }

        public Type[] ArgumentTypes { get; set; }

        public int CancellationIndex { get; set; }

        public Type ReturnType { get; set; }

        public Type TaskCompletionSourceType { get; set; }
    }
}
