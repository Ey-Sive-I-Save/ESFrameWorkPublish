using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Unity.Configuration;

namespace Microsoft.Extensions.Hosting.Unity
{
    /// <summary>
    /// <see cref="IHostBuilder"/> extensions for Unity3D.
    /// </summary>
    public static class HostBuilderExtensions
    {
        private const string INJECTABLE_METHOD_NAME = "AwakeServices";

        /// <summary>
        /// Allows to register a specific <see cref="IHostedService"/> implementation to be injected.
        /// </summary>
        /// <param name="services"></param>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        public static IServiceCollection AddHostedService<TService, TImplementation>(this IServiceCollection services)
            where TService : class
            where TImplementation : class, IHostedService, TService
        {
            services.AddSingleton<TService, TImplementation>();
            services.AddHostedService<HostedServiceWrapper<TService>>();
            return services;
        }

        /// <summary>
        /// Allows to register a specific <see cref="IHostedService"/> implementation to be injected.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="implementationFactory">The factory that creates the service.</param>
        /// <typeparam name="TService"></typeparam>
        /// <typeparam name="TImplementation"></typeparam>
        public static IServiceCollection AddHostedService<TService, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> implementationFactory)
            where TService : class
            where TImplementation : class, IHostedService, TService
        {
            services.AddSingleton<TService, TImplementation>(implementationFactory);
            services.AddHostedService<HostedServiceWrapper<TService>>();
            return services;
        }

        /// <summary>
        /// Will suppress host lifetime messages logging if <paramref name="suppressStatusMessages"/> is set to true.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="suppressStatusMessages">Will not log host lifetime messages if true.</param>
        /// <returns></returns>
        public static IServiceCollection SuppressStatusMessages(this IServiceCollection services, bool suppressStatusMessages)
        {
            services.Configure<ConsoleLifetimeOptions>(options => options.SuppressStatusMessages = suppressStatusMessages);
            return services;
        }

        /// <summary>
        /// Sets ApplicationName for the HostingEnvironment.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IHostBuilder SetApplicationName(this IHostBuilder builder, string name)
        {
            builder.ConfigureAppConfiguration((context, _) => context.HostingEnvironment.ApplicationName = name);
            return builder;
        }

        /// <summary>
        /// Sets EnvironmentName for the HostingEnvironment.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static IHostBuilder SetEnvironmentName(this IHostBuilder builder, string name)
        {
            builder.ConfigureAppConfiguration((context, _) => context.HostingEnvironment.EnvironmentName = name);
            return builder;
        }

        /// <summary>
        /// Use Config/appsettings.json to store settings.
        /// </summary>
        /// <param name="hostBuilder"></param>
        /// <typeparam name="TSettings"></typeparam>
        // ReSharper disable once CognitiveComplexity
        // todo convert to persistent storage settings
        public static void UseGlobalSettings<TSettings>(this IHostBuilder hostBuilder) where TSettings : GlobalSettings, new()
        {
            const string globalSettingsConfigured = "GLOBALSETTINGS_CONFIGURED";
            const string globalSettingsSource = "GLOBALSETTINGS_SOURCE";

            if (hostBuilder.Properties.TryGetValue(globalSettingsSource, out _))
                throw new InvalidOperationException("GlobalSettings already configured");

            hostBuilder.ConfigureAppConfiguration((context, builder) =>
            {
                var source = new GlobalSettingsConfigurationSource<TSettings>();
                builder.Add(source);

                if (!context.Properties.ContainsKey(globalSettingsSource))
                    hostBuilder.Properties.Add(globalSettingsSource, source);
            });
            hostBuilder.ConfigureServices((context, collection) =>
            {
                if (context.Properties.TryGetValue(globalSettingsSource, out var source))
                {
                    if (source is GlobalSettingsConfigurationSource<TSettings> s)
                        collection.AddSingleton<TSettings>(s.Settings);
                }
            });
        }
    }
}