// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration
{
    public static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddScriptableObjectConfiguration<T>(this IConfigurationBuilder builder, string resourcesPath) where T : UnityEngine.ScriptableObject
        {
            return builder.Add(new ScriptableObjectConfigurationSource(typeof(T), resourcesPath));
        }
        
        public static IConfigurationBuilder AddScriptableObjectConfiguration<T>(this IConfigurationBuilder builder, T scriptableObject) where T : UnityEngine.ScriptableObject
        {
            return builder.Add(new ScriptableObjectConfigurationSource(typeof(T), scriptableObject));
        }
    }
}