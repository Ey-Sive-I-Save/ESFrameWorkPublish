using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Microsoft.Extensions.Hosting.Unity
{
    internal static class Reflection
    {
        public static bool TryGetInjectionMethod(this Type type, string methodName, out InjectMethod injectable)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (!method.Name.Equals(methodName, StringComparison.Ordinal))
                    continue;

                if (method.ReturnParameter?.ParameterType != typeof(void))
                    continue;

                if (method.IsAbstract || method.IsVirtual || method.IsConstructor || method.ContainsGenericParameters)
                    continue;

                var parameters = method.GetParameters();

                if (parameters.Length == 0)
                    continue;

                injectable = new InjectMethod(method, GetParametersTypes(parameters));
                return true;
            }

            injectable = default;
            return false;
        }

        private static Type[] GetParametersTypes(IReadOnlyList<ParameterInfo> parameters)
        {
            var types = new Type[parameters.Count];

            for (var i = 0; i < parameters.Count; i++)
            {
                var type = parameters[i].ParameterType;

                if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                    throw new NotSupportedException("Value types, enums, strings can not be injected into MonoBehavoiur parameterized Awake");

                types[i] = type;
            }

            return types;
        }
    }
}