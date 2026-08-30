using System;
using System.Reflection;

namespace Microsoft.Extensions.Hosting.Unity
{
    /// <summary>
    /// For caching <see cref="UnityEngine.MonoBehaviour"/> custom injection method metadata.
    /// </summary>
    internal readonly struct InjectMethod
    {
        /// <summary>
        /// The injection method.
        /// </summary>
        public readonly MethodInfo method;
        
        /// <summary>
        /// The injection method's parameters. Actual services types to inject.
        /// </summary>
        public readonly Type[] types;

        public InjectMethod(MethodInfo method, Type[] types)
        {
            this.method = method;
            this.types = types;
        }
    }
}