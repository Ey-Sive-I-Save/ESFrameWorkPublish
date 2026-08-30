using System;

namespace Microsoft.Extensions.Hosting.Unity
{
    public interface IUnityHostBuilder : IHostBuilder
    {
        public IUnityHostBuilder ConfigureUnityObjects(Action<HostBuilderContext, IUnityObjectsConfigurator> configureDelegate);
    }
}