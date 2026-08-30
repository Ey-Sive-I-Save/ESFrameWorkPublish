using System;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration
{
    [AttributeUsage(AttributeTargets.Field), UnityEngine.Scripting.Preserve]
    public sealed class ConfigurationKeyAttribute : Attribute
    {
        public ConfigurationKeyAttribute(string name) => Name = name;

        public string Name { get; }
    }
}