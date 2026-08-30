using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting.Unity
{
    internal class UnityHostBuilder : HostBuilder, IUnityHostBuilder
    {
        // ReSharper disable once ArrangeObjectCreationWhenTypeEvident
        private readonly List<Action<HostBuilderContext, IUnityObjectsConfigurator>> _configureDelegates = new List<Action<HostBuilderContext, IUnityObjectsConfigurator>>();
        private readonly UnityObjectsConfigurator _unityObjectsConfigurator;

        public UnityHostBuilder(string serviceInjectionMethodName)
        {
            _unityObjectsConfigurator = new UnityObjectsConfigurator(serviceInjectionMethodName);
        }
        
        /// <summary>
        /// Register <see cref="UnityEngine.MonoBehaviour"/> classes to this <see cref="IHost"/> services.
        /// </summary>
        /// <param name="configureDelegate">Configure action.</param>
        /// <returns></returns>
        public IUnityHostBuilder ConfigureUnityObjects(Action<HostBuilderContext, IUnityObjectsConfigurator> configureDelegate)
        {
            if(configureDelegate == null)
                throw new ArgumentNullException(nameof(configureDelegate));

            _configureDelegates.Add(configureDelegate);

            return this;
        }

        public new IHost Build()
        {
            ConfigureServices((context, services) => services.AddScoped<ScopeRoot>());
            ConfigureServices((context, services) =>
            {
                foreach (var configure in _configureDelegates)
                {
                    configure.Invoke(context, _unityObjectsConfigurator);
                }
                
                _unityObjectsConfigurator.Configure(context, services);
            });

            return base.Build();
        }
    }
}