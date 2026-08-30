using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting.Unity
{
    public static class ServiceProviderServiceExtensions
    {
        public static MonoBehaviourScope CreateMonoBehaviourScope(this IServiceProvider provider) => new MonoBehaviourScope(provider.CreateScope());
        
        public static ServicesLookup GetServicesLookup(this IServiceProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            return provider.GetService<ServicesLookup>();
        }
    }
}