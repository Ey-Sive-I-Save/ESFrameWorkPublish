using System;
using UnityEngine;

namespace Microsoft.Extensions.DependencyInjection
{
    internal class ScopeRoot : IDisposable
    {
#if DEBUG
        private readonly Guid _id;
        public override string ToString() => _id.ToString();
#endif
        public readonly GameObject root;

        public ScopeRoot()
        {
#if DEBUG
            _id = Guid.NewGuid();
#endif
            root = new GameObject(nameof(MonoBehaviourScope) + "_root");
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            UnityEngine.Object.Destroy(root);
        }
    }
}