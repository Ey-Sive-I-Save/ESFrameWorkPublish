using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace Microsoft.Extensions.Hosting.Unity.Logging
{
    public static class UnityLoggerExtensions
    {
        public static ILoggingBuilder AddUnityLogger(this ILoggingBuilder builder)
        {
            builder.AddConfiguration();
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, UnityLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions<UnityLoggerConfiguration, UnityLoggerProvider>(builder.Services);

            return builder;
        }

        public static ILoggingBuilder AddUnityLogger(this ILoggingBuilder builder, Action<UnityLoggerConfiguration> configure)
        {
            builder.AddUnityLogger();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}