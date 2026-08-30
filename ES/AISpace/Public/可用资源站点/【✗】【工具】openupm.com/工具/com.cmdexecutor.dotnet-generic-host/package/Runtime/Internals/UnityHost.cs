using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.Scripting;

namespace Microsoft.Extensions.Hosting.Unity
{
    [Preserve]
    public static class UnityHost
    {
        [Preserve]
        public static IHostBuilder CreateDefaultBuilder(string servicesInjectionMethodName) => CreateDefaultBuilder(servicesInjectionMethodName, args: null);
        
        [Preserve]
        public static IUnityHostBuilder CreateDefaultBuilder(string servicesInjectionMethodName, string[] args)
        {
            var builder = new UnityHostBuilder(servicesInjectionMethodName);
            return builder.ConfigureDefaults(args);
        }
        
        private static IUnityHostBuilder ConfigureDefaults(this IUnityHostBuilder builder, string[] args)
        {
            builder.UseContentRoot(Application.dataPath);
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddEnvironmentVariables(prefix: "DOTNET_");
                if (args is { Length: > 0 })
                {
                    config.AddCommandLine(args);
                }
            });

            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                IHostEnvironment env = hostingContext.HostingEnvironment;

                config.AddJsonFile("appsettings.json", optional: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

                if (env.IsDevelopment() && env.ApplicationName is { Length: > 0 })
                {
                    var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                    if (appAssembly is not null)
                    {
                        config.AddUserSecrets(appAssembly, optional: true);
                    }
                }

                config.AddEnvironmentVariables();

                if (args is { Length: > 0 })
                {
                    config.AddCommandLine(args);
                }
            })
            .ConfigureLogging((hostingContext, logging) =>
            {
                logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));

                logging.AddDebug();
                logging.AddEventSourceLogger();

                logging.Configure(options =>
                {
                    options.ActivityTrackingOptions =
                        ActivityTrackingOptions.SpanId |
                        ActivityTrackingOptions.TraceId |
                        ActivityTrackingOptions.ParentId;
                });

            })
            .UseDefaultServiceProvider((context, options) =>
            {
                bool isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = isDevelopment;
                options.ValidateOnBuild = isDevelopment;
            });

            builder.ConfigureServices(services => services.AddSingleton<IHostLifetime, UnityLifetime>());

            return builder;
        }
    }
}