![Preview](Editor/Gizmos/host_service_icon.png)
 
[![Twitter Follow](https://img.shields.io/twitter/follow/cmdexecutor?color=blue&label=Follow%20on%20Twitter&logo=%20&logoColor=%20&style=flat-square)](https://twitter.com/cmdexecutor)

# .NET Generic Host for Unity3D
#### This package allows to use DI Hosting (_Microsoft.Extensions.Hosting_) in Unity projects.
The documentation can be found here: [Tutorial: Use dependency injection in .NET](https://docs.microsoft.com/ru-ru/dotnet/core/extensions/dependency-injection-usage)

## Install

### By git URL:
Add `https://github.com/amelkor/Microsoft.Extensions.Hosting.Unity.git?path=Packages/com.cmdexecutor.dotnet-generic-host` to Unity Package Manager

Or add `"com.cmdexecutor.dotnet-generic-host": "https://github.com/amelkor/Microsoft.Extensions.Hosting.Unity.git?path=Packages/com.cmdexecutor.dotnet-generic-host"` to `Packages/manifest.json`.

### By OpenUPM

The package is available on the [openupm registry](https://openupm.com). It's recommended to install it via [openupm-cli](https://github.com/openupm/openupm-cli).

```
openupm add com.cmdexecutor.dotnet-generic-host
```

Or add scoped registry to `Packages/manifest.json`

```json
    "scopedRegistries": [
        {
            "name": "package.openupm.com",
            "url": "https://package.openupm.com",
            "scopes": [
                "com.cmdexecutor.dotnet-generic-host"
            ]
        }
    ],
```

### By npmjs

Add scoped registry to `Packages/manifest.json`

```json
    "scopedRegistries": [
        {
            "name": "npmjs.com",
            "url": "https://registry.npmjs.org",
            "scopes": [
                "com.cmdexecutor.dotnet-generic-host"
            ]
        }
    ],
```

## Getting started

> The [repository](https://github.com/amelkor/Microsoft.Extensions.Hosting.Unity) contains demo Unity project.

Injection into `Monobehaviour` classes happens via custom defined private method which name is specified as `Services Injection Method Name` parameter in Inspector or ``. The default name is `AwakeServices`. Could be adjusted in the Unity Inspector window.

To get started, derive from `HostManager` and add the derived class to a GameObject on scene, that's mostly it.

```cs
    public class SampleHost : HostManager
    {
        [Tooltip("Could be used for referencing ScriptableObjects and as configuration provider")]
        [SerializeField] private ConfigurationScriptableObject configuration;
        
        [Tooltip("UI instance from scene of from SampleHost GameObject hierarchy")]
        [SerializeField] private SampleUI sampleUI;

        protected override void OnAwake()
        {
            // Awake called before host build
            // if buildOnAwake is false (can be controlled through Inspector) then host needs to be build manually
            // By default builds on Awake
            buildOnAwake = false;

            // Use custom methods name for Unity Objects (default is AwakeServices)
            // The services will be injected into ctor
            // servicesInjectionMethodName = "CustomInjectionMethodName";

            // Call from the desired place to build the host if buildOnAwake = false;
            BuildManually();

            // Stops the host
            // StopManually(); or StopManuallyAsync();
        }

        protected override void ConfigureAppConfiguration(HostBuilderContext context, IConfigurationBuilder builder)
        {
            Debug.Log("ConfigureAppConfiguration");
            
            // Add SO as configuration source, will parse all attributes from the SO and make them accessible from IConfiguration
            builder.AddScriptableObjectConfiguration<ConfigurationScriptableObject>(configuration);
        }

        protected override void ConfigureLogging(HostBuilderContext context, ILoggingBuilder builder)
        {
            Debug.Log("ConfigureLogging");
        }

        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Register ordinary C# classes
            Debug.Log("ConfigureServices");
        }

        protected override void ConfigureUnityObjects(HostBuilderContext context, IUnityObjectsConfigurator services)
        {
            Debug.Log("ConfigureUnityObjects");
            
            foreach (var (type, so) in configuration.GetScriptableObjectsToRegisterAsSingeltons())
                services.AddScriptableObjectSingleton(so, type);

            // Registers already instantiated instance under HostRoot GameObject
            services.AddMonoBehaviourSingleton(sampleUI);

            // Register as hosted service (will be resolved and start automatically when host start)
            services.AddMonoBehaviourHostedService<MonoHostedService>();

            // Creates new instances under HostRoot GameObject
            services.AddMonoBehaviourSingleton<MonoSingleton>();
            services.AddMonoBehaviourTransient<MonoTransient>();
            
            // Or creates new instance in scene root
            // services.AddDetachedMonoBehaviourSingleton<MonoSingleton>();

            Debug.Log($"SampleInteger config value from registered SO: {context.Configuration["SampleInteger"]}");
            
            // Loads from Resources (spawns detached)
            services.AddMonoBehaviourPrefabTransient<RandomMoveObject>(configuration.movingObjectPrefabName);
        }
    }
```

In case if `Host` needs to be composed manually, a minimal configuration could be similar to following:
```cs
            var hostBuilder = UnityHost.CreateDefaultBuilder(servicesInjectionMethodName, cmdArguments)
                .ConfigureAppConfiguration(builder =>
                {
                    builder.DisableFileConfigurationSourceReloadOnChange();
                    builder.AddCommandLine(cmdArguments);
                });
                .ConfigureLogging((_, loggingBuilder) =>
                {
                    loggingBuilder.SetMinimumLevel(logLevel);
                });
                .ConfigureUnityObjects((context, configurator) => {/*Add Mono services*/})
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IMonoBehaviourHostRoot, MonoBehaviourHostRoot>(provider =>
                    {
                        var lifetime = provider.GetRequiredService<IHostApplicationLifetime>();

                        var root = new GameObject($"{nameof(MonoBehaviourHostRoot)} (host root)");
                        var component = root.AddComponent<MonoBehaviourHostRoot>();

                        lifetime.ApplicationStopped.Register(() =>
                        {
                            if (!root)
                                return;

                            UnityEngine.Object.Destroy(root.gameObject);
                        });

                        return component;
                    });
                });

            // add services
                // hostBuilder.ConfigureServices
                // hostBuilder.ConfigureMonoBehaviours

            // build the host
            var host = hostBuilder.Build();

            // start the host
            await host.StartAsync();

            // stop the host
            await host.StopAsync()
```

## Licensing
- The asset: MIT License, see  [LICENSE.md](LICENSE.md) for more information.
- .NET libraries: MIT License, see  [Runtime/lib/LICENSE.md](Runtime/lib/LICENSE.txt) for more information.
