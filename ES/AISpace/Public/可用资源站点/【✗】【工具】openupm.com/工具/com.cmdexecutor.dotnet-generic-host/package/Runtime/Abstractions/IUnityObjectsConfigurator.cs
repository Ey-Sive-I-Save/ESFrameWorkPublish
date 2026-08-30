using System;
using UnityEngine;

namespace Microsoft.Extensions.Hosting.Unity
{
    public interface IUnityObjectsConfigurator
    {
        IUnityObjectsConfigurator AddMonoBehaviourSingleton(MonoBehaviour component, Type type = default, bool useHostLifetime = false);
        IUnityObjectsConfigurator AddMonoBehaviourSingleton<T>() where T : MonoBehaviour;
        
        /// <summary>
        /// Add a MonoBehaviour as a separate GameObject which won't be destroyed on load.
        /// </summary>
        IUnityObjectsConfigurator AddDetachedMonoBehaviourSingleton<T>() where T : MonoBehaviour;
        IUnityObjectsConfigurator AddMonoBehaviourHostedService<T>() where T : MonoBehaviour, IHostedService;
        IUnityObjectsConfigurator AddMonoBehaviourHostedService<T, TImpl>() where T : MonoBehaviour, IHostedService where TImpl : MonoBehaviour, T;
        IUnityObjectsConfigurator AddMonoBehaviourSingleton<T, TImpl>(TImpl component, bool useHostLifetime = false) where TImpl : MonoBehaviour, T where T : class;
        IUnityObjectsConfigurator AddMonoBehaviourSingleton<T, TImpl>() where T : class where TImpl : MonoBehaviour, T;
        IUnityObjectsConfigurator AddMonoBehaviourTransient<T>() where T : MonoBehaviour;
        IUnityObjectsConfigurator AddMonoBehaviourTransient<T, TImpl>() where T : class where TImpl : MonoBehaviour, T;
        
        IUnityObjectsConfigurator AddScriptableObjectSingleton(ScriptableObject scriptableObject, Type type);
        IUnityObjectsConfigurator AddScriptableObjectSingleton<T>(T scriptableObject) where T : ScriptableObject;
        IUnityObjectsConfigurator AddScriptableObjectSingleton<T, TImpl>(TImpl scriptableObject) where T : class where TImpl : ScriptableObject, T;

        /// <summary>
        /// Load prefab from Resources and register as singleton.
        /// </summary>
        /// <param name="resourcesPath">Resources asset path.</param>
        /// <typeparam name="T">MonoBehaviour type.</typeparam>
        /// <returns></returns>
        IUnityObjectsConfigurator AddMonoBehaviourPrefabSingleton<T>(string resourcesPath) where T : MonoBehaviour;
        
        /// <summary>
        /// Load prefab from Resources and register as transient (Spawns in scene root).
        /// </summary>
        /// <param name="resourcesPath">Resources asset path.</param>
        /// <typeparam name="T">MonoBehaviour type.</typeparam>
        /// <returns></returns>
        IUnityObjectsConfigurator AddMonoBehaviourPrefabTransient<T>(string resourcesPath) where T : MonoBehaviour;
        
        /// <summary>
        /// Load prefab from Resources and register as scoped.
        /// </summary>
        /// <param name="resourcesPath">Resources asset path.</param>
        /// <typeparam name="T">MonoBehaviour type.</typeparam>
        /// <returns></returns>
        IUnityObjectsConfigurator AddMonoBehaviourPrefabScoped<T>(string resourcesPath) where T : MonoBehaviour;
    }
}