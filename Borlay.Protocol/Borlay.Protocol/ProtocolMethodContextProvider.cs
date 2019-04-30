using Borlay.Handling;
using Borlay.Handling.Notations;
using Borlay.Injection;
using Borlay.Serialization.Notations;
using System;
using System.Collections.Generic;
using System.Linq;
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

        protected override string ResolveScope(Type type, MethodInfo methodInfo, ScopeAttribute scopeAttr)
        {
            var scope = base.ResolveScope(type, methodInfo, scopeAttr);

            var genericTypeArguments = methodInfo.DeclaringType.GenericTypeArguments;
            if (genericTypeArguments.Length > 0)
            {
                scope += ":generic";
                foreach(var gType in genericTypeArguments)
                {
                    var typeName = gType.Name;
                    var dataAttr = gType.GetTypeInfo().GetCustomAttribute<DataAttribute>(true);
                    if (dataAttr != null)
                        typeName =  $"typeId:{dataAttr.TypeId}";

                    scope += ":" + typeName;
                }

                
            }
            
            return scope;
        }

    }
}
