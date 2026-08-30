using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Hosting.Unity
{
    /// <summary>
    /// Wrapper allows to register an <see cref="IHostedService"/> implementation with a custom alias.<br/>
    /// Without this wrapper it is only possible to resolve the hosted service as <see cref="IHostedService"/>.
    /// This is a workaround due to Microsoft DI container limitations. This might change in the future.
    /// </summary>
    /// <seealso href="https://stackoverflow.com/questions/51254053/how-to-inject-a-reference-to-a-specific-ihostedservice-implementation"/>
    /// <typeparam name="TService"></typeparam>
    internal class HostedServiceWrapper<TService> : IHostedService where TService : class
    {
        private readonly IHostedService _hostedService;

        public HostedServiceWrapper(TService hostedService)
        {
            _hostedService = (IHostedService) hostedService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return _hostedService.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _hostedService.StopAsync(cancellationToken);
        }
    }
}