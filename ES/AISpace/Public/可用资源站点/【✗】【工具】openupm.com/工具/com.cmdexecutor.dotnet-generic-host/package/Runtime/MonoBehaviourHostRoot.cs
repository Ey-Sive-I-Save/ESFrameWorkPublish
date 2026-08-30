using UnityEngine;

namespace Microsoft.Extensions.Hosting.Unity
{
    /// <summary>
    /// Root GameObject for MonoBehaviour components.
    /// </summary>
    internal interface IMonoBehaviourHostRoot
    {
        GameObject GameObject { get; }
        T AddComponent<T>() where T : MonoBehaviour;
    }

    /// <inheritdoc cref="Microsoft.Extensions.Hosting.Unity.IMonoBehaviourHostRoot" />
    internal class MonoBehaviourHostRoot : MonoBehaviour, IMonoBehaviourHostRoot
    {
        private GameObject _root;

        private void Awake()
        {
            DontDestroyOnLoad(this);
            _root = gameObject;
        }

        public GameObject GameObject => _root;
        public T AddComponent<T>() where T : MonoBehaviour => _root.AddComponent<T>();
    }
}