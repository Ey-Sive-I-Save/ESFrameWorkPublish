using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting.Unity
{
    /// <summary>
    /// Contains lookup for all the registered services types for the container.
    /// </summary>
    public sealed class ServicesLookup
    {
        public class Description
        {
            public readonly Type type;
            public readonly Type implementationType;
            public readonly ServiceLifetime lifetime;

            internal Description(Type type, Type implementationType, ServiceLifetime lifetime)
            {
                this.type = type;
                this.implementationType = implementationType;
                this.lifetime = lifetime;
            }
        }

        private readonly List<Description> _descriptions;

        public IReadOnlyCollection<Description> Descriptions => _descriptions;
        public IEnumerable<Description> GetDescriptions<T>() => _descriptions.Where(d=>d.type == typeof(T));

        public ServicesLookup(IServiceCollection collection)
        {
            _descriptions = new List<Description>(collection.Count);
            foreach (var descriptor in collection)
            {
                _descriptions.Add(new Description(descriptor.ServiceType, descriptor.ImplementationType, descriptor.Lifetime));
            }
        }
    }
}