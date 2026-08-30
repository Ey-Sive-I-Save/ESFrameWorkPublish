using System;
using UnityEngine;

namespace Microsoft.Extensions.DependencyInjection
{
    public readonly struct MonoBehaviourScope : IServiceScope
    {
#nullable disable
        private readonly IServiceScope _serviceScope;
#nullable enable

        private readonly ScopeRoot _root;

        public MonoBehaviourScope(IServiceScope serviceScope)
        {
            _serviceScope = serviceScope ?? throw new ArgumentNullException(nameof(serviceScope));
            _root = _serviceScope.ServiceProvider.GetRequiredService<ScopeRoot>();
        }

        public IServiceProvider ServiceProvider => _serviceScope.ServiceProvider;

        public void Dispose()
        {
            _serviceScope.Dispose();
            _root.Dispose();
        }
    }
}