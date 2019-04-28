using Borlay.Handling;
using Borlay.Handling.Notations;
using Borlay.Injection;
using Borlay.Serialization.Notations;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Borlay.Protocol
{
    [Resolve(Singletone = true, IncludeBase = true, Priority = 1)]
    public class ProtocolMethodContextProvider : MethodContextProvider
    {
        protected override string ResolveTypeName(Type type)
        {
            var dataAttr = type.GetTypeInfo().GetCustomAttribute<DataAttribute>(true);
            if (dataAttr != null)
                return $"typeId:{dataAttr.TypeId}";

            return base.ResolveTypeName(type);
        }

        protected override byte[] ResolveScopeId(Type type, MethodInfo methodInfo, ScopeAttribute scopeAttr)
        {
            var genericParameters = methodInfo.DeclaringType.GetTypeInfo().GenericTypeParameters;
            if (genericParameters.Length > 0)
            {

            }
            return base.ResolveScopeId(type, methodInfo, scopeAttr);
        }

    }
}
