using System;
using UnityEngine.Scripting;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration
{
    /// <summary>
    /// A marker to mark that the <see cref="UnityEngine.ScriptableObject"/> field needs to be picked and registered in the <see cref="Microsoft.Extensions.Hosting.Unity.HostManager"/>.
    /// </summary>
    [Preserve]
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RegisterScriptableObjectAsSingletonAttribute : Attribute
    {
    }
}