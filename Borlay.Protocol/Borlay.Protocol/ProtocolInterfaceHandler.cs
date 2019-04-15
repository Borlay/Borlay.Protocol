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
        protected readonly TypeMetaData typeMetaData;
        protected readonly IRequestAsync requestAsync;

        public bool IsAsync => true;

        public volatile static int ts;

        public ProtocolInterfaceHandler(IRequestAsync requestAsync)
        {
            if (requestAsync == null)
                throw new ArgumentNullException(nameof(requestAsync));

            this.requestAsync = requestAsync;

            typeMetaData = TypeMetaDataProvider.GetTypeMetaData<TActAs>();
        }

        public object HandleAsync(string methodName, byte[] methodHashBytes, object[] args)
        {
            
            var watch = Stopwatch.StartNew();

            var stop = ProtocolWatch.Start("handle-async");

            var methodHash = new ByteArray(methodHashBytes);
            var metaData = typeMetaData.GetMetaData(methodName, methodHash);

            CancellationToken cancellationToken;
            if (metaData.CancellationIndex >= 0)
                cancellationToken = (CancellationToken)args[metaData.CancellationIndex];
            else
            {
                var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                cancellationToken = cts.Token;
            }

            stop();

            stop = ProtocolWatch.Start("handle-send-request");

            var actionDataContext = new DataContext()
            {
                DataFlag = DataFlag.Action,
                Data = metaData.ActionId,
            };

            var hashDataContext = new DataContext()
            {
                DataFlag = DataFlag.MethodHash,
                Data = methodHash,
            };

            var additionalCount = 2;
            if (metaData.ScopeId != null)
                additionalCount = 3;

            var argumentContexts = new DataContext[metaData.ArgumentIndexes.Length + additionalCount]; // { actionDataContext, dataDataContext };

            argumentContexts[0] = actionDataContext;
            argumentContexts[1] = hashDataContext;
            if (metaData.ScopeId != null)
            {
                argumentContexts[2] = new DataContext()
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
        private readonly static ConcurrentDictionary<Type, TypeMetaData> typeMetaDatas = new ConcurrentDictionary<Type, TypeMetaData>();

        public static TypeMetaData GetTypeMetaData<T>()
        {
            var type = typeof(T);
            return GetTypeMetaData(type);
        }

        public static TypeMetaData GetTypeMetaData(Type type)
        {
            if (typeMetaDatas.TryGetValue(type, out var typeMetaData))
                return typeMetaData;

            typeMetaData = new TypeMetaData(type);
            typeMetaDatas[type] = typeMetaData;
            return typeMetaData;
        }

    }

    public class TypeMetaData
    {
        protected readonly Dictionary<string, Dictionary<ByteArray, MethodMetadata>> methods = new Dictionary<string, Dictionary<ByteArray, MethodMetadata>>();

        public TypeMetaData(Type type)
        {
            var methodGroups = type.GetInterfacesMethods().Distinct()
                .Where(m => m.GetCustomAttribute<ActionAttribute>(true) != null).GroupBy(m => m.Name);

            var classScopeAttr = type.GetTypeInfo().GetCustomAttribute<ScopeAttribute>(true);

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

                    var methodHash = TypeHasher.GetMethodHash(argumentTypes.ToArray(), m.ReturnType);

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
                        ActionId = actionAttr?.GetActionId(),
                        ScopeId = methodScopeAttr?.GetScopeId(),
                        MethodHash = methodHash,
                        TaskCompletionSourceType = tcsGenType
                    };
                    return meta;
                })
                .ToDictionary(m => m.MethodHash);

                methods.Add(g.Key, methodMeta);
            }
        }

        public MethodMetadata GetMetaData(string methodName, ByteArray methodHash)
        {
            if (!methods.TryGetValue(methodName, out var methodMetadatas))
                throw new KeyNotFoundException($"Method for name '{methodName}' not found");

            if (!methodMetadatas.TryGetValue(methodHash, out var metaData))
                throw new KeyNotFoundException($"Method for name '{methodName}' not found");

            return metaData;
        }

    }

    public class MethodMetadata
    {
        public Type[] ParameterTypes { get; set; }

        public ByteArray MethodHash { get; set; }

        public object ActionId { get; set; }

        public object ScopeId { get; set; }

        public int[] ArgumentIndexes { get; set; }

        public Type[] ArgumentTypes { get; set; }

        public int CancellationIndex { get; set; }

        public Type ReturnType { get; set; }

        public Type TaskCompletionSourceType { get; set; }
    }
}
