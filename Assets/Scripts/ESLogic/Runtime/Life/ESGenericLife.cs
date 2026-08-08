using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Pool is one capability of <see cref="ESGenericLife"/>, not the whole lifetime model.
    /// A root runtime object or an injected extension implements this capability only when it
    /// needs the two pool edges.
    /// </summary>
    public interface IESGameObjectPoolLifecycle
    {
        /// <summary>Called while the instance is inactive and is about to be handed out.</summary>
        void OnPoolSpawned();

        /// <summary>Called before the instance is deactivated and returned to its pool.</summary>
        void OnPoolDespawned();
    }

    /// <summary>
    /// Cold-path installer for a root component that is explicitly an injected Pool extension.
    /// It lets Profile cover inactive source Prefabs without making the Pool recognize concrete
    /// feature types or scan child hierarchies.
    /// </summary>
    public interface IESGameObjectPoolLifecycleExtensionInstaller
    {
        bool TryInstallPoolLifecycleExtension(ESGenericLife life);
    }

    /// <summary>
    /// Generic root-life organizer for one GameObject. It owns no Entity, Item, Tag, Pool, or
    /// business state. Concrete capabilities are added only when a real caller exists. Pool is
    /// the first capability: one root receiver plus optional, type-unique injected receivers.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/场景与对象/生命周期/ES 通用生命周期")]
    public sealed class ESGenericLife : MonoBehaviour
    {
        [SerializeField, Tooltip("对象池主生命周期接收者；必须与 ESGenericLife 位于同一根 GameObject。扩展接收者通过注册注入，不写入这里。")]
        private MonoBehaviour poolRootLifecycleComponent;

        [NonSerialized] private IESGameObjectPoolLifecycle poolRootLifecycle;
        [NonSerialized] private List<IESGameObjectPoolLifecycle> poolExtensions;
        [NonSerialized] private bool poolSpawned;
        [NonSerialized] private bool dispatchingPoolLifecycle;
        [NonSerialized] private bool poolLifecycleInvalid;

        /// <summary>The one root receiver for the Pool capability; extensions are not serialized here.</summary>
        public MonoBehaviour PoolRootLifecycleComponent => poolRootLifecycleComponent;

        /// <summary>Extensions are runtime-only and allocated only after the first registration.</summary>
        public int PoolExtensionCount => poolExtensions != null ? poolExtensions.Count : 0;

        public bool IsPoolSpawned => poolSpawned;

        /// <summary>
        /// Explicitly binds the one root receiver of the Pool capability. The receiver must live
        /// on this root GameObject. Any other root component that implements the same capability
        /// must already have been registered as an extension.
        /// </summary>
        public bool BindPoolRoot(IESGameObjectPoolLifecycle candidate)
        {
            if (!TryGetRootComponent(candidate, out MonoBehaviour candidateComponent))
            {
                Debug.LogError("[ESGenericLife] Pool root receiver must be a component on the same GameObject.", this);
                return false;
            }

            if (poolSpawned || dispatchingPoolLifecycle)
            {
                Debug.LogError("[ESGenericLife] Pool root receiver may only change while the object is inactive.", this);
                return false;
            }

            if (!TryValidatePoolRoot(candidate, true, out string error))
            {
                poolLifecycleInvalid = true;
                Debug.LogError("[ESGenericLife] " + error, this);
                return false;
            }

            poolRootLifecycleComponent = candidateComponent;
            poolRootLifecycle = candidate;
            MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
            if (!TryInstallDeclaredPoolExtensions(components)
                || !TryValidatePoolRoot(candidate, false, out error))
            {
                poolLifecycleInvalid = true;
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError("[ESGenericLife] " + error, this);
                return false;
            }

            poolLifecycleInvalid = false;
            return true;
        }

        /// <summary>
        /// Injects one optional Pool receiver. Extensions may be regular C# runtime objects or
        /// MonoBehaviours; they are not discovered by hierarchy scanning. Exactly one extension
        /// of a concrete type may be registered on one ESGenericLife.
        /// </summary>
        public bool RegisterPoolExtension(IESGameObjectPoolLifecycle extension)
        {
            if (!IsAlive(extension))
            {
                Debug.LogError("[ESGenericLife] Cannot register an empty Pool lifecycle extension.", this);
                return false;
            }

            if (poolSpawned || dispatchingPoolLifecycle)
            {
                Debug.LogError("[ESGenericLife] Pool extensions may only change while the object is inactive.", this);
                return false;
            }

            RemoveDeadPoolExtensions();
            if (ReferenceEquals(GetPoolRootLifecycle(), extension))
            {
                Debug.LogError("[ESGenericLife] The Pool root receiver cannot also be registered as an extension.", this);
                return false;
            }

            Type extensionType = extension.GetType();
            if (poolExtensions != null)
            {
                for (int i = 0; i < poolExtensions.Count; i++)
                {
                    IESGameObjectPoolLifecycle existing = poolExtensions[i];
                    if (IsAlive(existing) && existing.GetType() == extensionType)
                    {
                        Debug.LogError("[ESGenericLife] Duplicate Pool lifecycle extension type: " + extensionType.Name, this);
                        return false;
                    }
                }
            }

            poolExtensions ??= new List<IESGameObjectPoolLifecycle>(2);
            poolExtensions.Add(extension);
            return true;
        }

        /// <summary>Removes an injected Pool receiver while the object is inactive.</summary>
        public bool UnregisterPoolExtension(IESGameObjectPoolLifecycle extension)
        {
            if (extension == null || poolExtensions == null || poolSpawned || dispatchingPoolLifecycle)
                return false;

            for (int i = 0; i < poolExtensions.Count; i++)
            {
                if (!ReferenceEquals(poolExtensions[i], extension))
                    continue;

                poolExtensions.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Cold-path structural validation. It examines this root only, never child nodes. Use it
        /// in editor/build validation or when a root Prefab is first bound; do not call it every
        /// pool borrow/return.
        /// </summary>
        public bool ValidatePoolLifecycle()
        {
            IESGameObjectPoolLifecycle root = GetPoolRootLifecycle();
            if (root == null)
            {
                if (poolExtensions == null || poolExtensions.Count == 0)
                {
                    poolLifecycleInvalid = false;
                    return true;
                }

                poolLifecycleInvalid = true;
                Debug.LogError("[ESGenericLife] Pool extensions require one root Pool lifecycle receiver.", this);
                return false;
            }

            if (!TryValidatePoolRoot(root, false, out string error))
            {
                poolLifecycleInvalid = true;
                Debug.LogError("[ESGenericLife] " + error, this);
                return false;
            }

            poolLifecycleInvalid = false;
            return true;
        }

        /// <summary>Explicit setup helper for a root MonoBehaviour that owns the Pool capability.</summary>
        public static ESGenericLife EnsureAndBindPoolRoot(MonoBehaviour rootComponent)
        {
            if (!(rootComponent is IESGameObjectPoolLifecycle rootLifecycle))
            {
                Debug.LogError("[ESGenericLife] The supplied component does not implement IESGameObjectPoolLifecycle.", rootComponent);
                return null;
            }

            ESGenericLife life = rootComponent.GetComponent<ESGenericLife>();
            if (life == null)
                life = rootComponent.gameObject.AddComponent<ESGenericLife>();

            return life.BindPoolRoot(rootLifecycle) ? life : null;
        }

        /// <summary>
        /// Cold migration for existing pooled Prefabs. It inspects components on this root only.
        /// If an explicit root is already configured it validates it; otherwise exactly one
        /// unregistered Pool receiver becomes the root. Multiple unregistered receivers are a
        /// configuration error, never an arbitrary selection.
        /// </summary>
        internal static ESGenericLife EnsureForPooledRoot(GameObject gameObject)
        {
            if (gameObject == null)
                return null;

            MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
            ESGenericLife life = gameObject.GetComponent<ESGenericLife>();
            if (life == null)
            {
                IESGameObjectPoolLifecycle discovered = FindSingleUnregisteredPoolRoot(
                    components,
                    null,
                    true,
                    out bool foundAny,
                    out string discoveryError);
                if (!string.IsNullOrEmpty(discoveryError))
                {
                    // Return an invalid bridge instead of null so the Pool can reject this
                    // malformed Prefab safely. Null remains reserved for objects that simply do
                    // not participate in the Pool lifecycle capability.
                    life = gameObject.AddComponent<ESGenericLife>();
                    life.poolLifecycleInvalid = true;
                    Debug.LogError("[ESGenericLife] " + discoveryError, life);
                    return life;
                }

                if (!foundAny)
                    return null;

                life = gameObject.AddComponent<ESGenericLife>();
                if (!life.BindPoolRoot(discovered))
                    return life;

                return life;
            }

            if (life.poolRootLifecycleComponent != null)
            {
                if (!life.TryInstallDeclaredPoolExtensions(components))
                {
                    life.poolLifecycleInvalid = true;
                    return life;
                }

                life.ValidatePoolLifecycle();
                return life;
            }

            IESGameObjectPoolLifecycle root = FindSingleUnregisteredPoolRoot(
                components,
                life,
                true,
                out bool hasRoot,
                out string error);
            if (!string.IsNullOrEmpty(error))
            {
                life.poolLifecycleInvalid = true;
                Debug.LogError("[ESGenericLife] " + error, life);
                return life;
            }

            if (hasRoot)
            {
                life.BindPoolRoot(root);
            }
            else
                life.ValidatePoolLifecycle();

            return life;
        }

        /// <summary>Pool-specific dispatch; normal borrow performs one root call and one small extension loop.</summary>
        internal bool NotifyPoolSpawned()
        {
            if (!CanDispatchPoolLifecycle("Spawn"))
                return false;

            poolSpawned = true;
            dispatchingPoolLifecycle = true;
            bool success = true;
            try
            {
                success &= InvokePoolSpawned(GetPoolRootLifecycle());
                int extensionCount = poolExtensions != null ? poolExtensions.Count : 0;
                for (int i = 0; i < extensionCount; i++)
                    success &= InvokePoolSpawned(poolExtensions[i]);
            }
            finally
            {
                dispatchingPoolLifecycle = false;
            }

            return success;
        }

        /// <summary>Pool-specific dispatch; extensions release in reverse order before the root receiver.</summary>
        internal bool NotifyPoolDespawned()
        {
            if (dispatchingPoolLifecycle)
            {
                Debug.LogError("[ESGenericLife] Pool Despawn cannot re-enter lifecycle dispatch.", this);
                return false;
            }

            bool success = !poolLifecycleInvalid;
            dispatchingPoolLifecycle = true;
            try
            {
                if (poolExtensions != null)
                {
                    for (int i = poolExtensions.Count - 1; i >= 0; i--)
                        success &= InvokePoolDespawned(poolExtensions[i]);
                }

                success &= InvokePoolDespawned(GetPoolRootLifecycle());
            }
            finally
            {
                poolSpawned = false;
                dispatchingPoolLifecycle = false;
                RemoveDeadPoolExtensions();
            }

            return success;
        }

        private bool CanDispatchPoolLifecycle(string action)
        {
            if (dispatchingPoolLifecycle || poolSpawned || poolLifecycleInvalid)
            {
                Debug.LogError("[ESGenericLife] Pool " + action + " dispatch is invalid for the current lifecycle state.", this);
                return false;
            }

            IESGameObjectPoolLifecycle root = GetPoolRootLifecycle();
            if (root == null)
                return poolExtensions == null || poolExtensions.Count == 0;

            if (!TryGetRootComponent(root, out _))
            {
                poolLifecycleInvalid = true;
                Debug.LogError("[ESGenericLife] The Pool root receiver no longer belongs to this root GameObject.", this);
                return false;
            }

            return true;
        }

        private bool TryValidatePoolRoot(
            IESGameObjectPoolLifecycle candidate,
            bool ignoreDeclaredExtensions,
            out string error)
        {
            error = null;
            if (!TryGetRootComponent(candidate, out _))
            {
                error = "Pool root receiver must be a component on the same GameObject.";
                return false;
            }

            MonoBehaviour[] components = gameObject.GetComponents<MonoBehaviour>();
            IESGameObjectPoolLifecycle discovered = FindSingleUnregisteredPoolRoot(
                components,
                this,
                ignoreDeclaredExtensions,
                out bool foundAny,
                out string discoveryError);
            if (!string.IsNullOrEmpty(discoveryError))
            {
                error = discoveryError;
                return false;
            }

            if (!foundAny || !ReferenceEquals(discovered, candidate))
            {
                error = "The configured Pool root receiver is not the unique unregistered receiver on this root GameObject.";
                return false;
            }

            return true;
        }

        private static IESGameObjectPoolLifecycle FindSingleUnregisteredPoolRoot(
            MonoBehaviour[] components,
            ESGenericLife life,
            bool ignoreDeclaredExtensions,
            out bool foundAny,
            out string error)
        {
            foundAny = false;
            error = null;
            IESGameObjectPoolLifecycle found = null;
            for (int i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IESGameObjectPoolLifecycle candidate))
                    continue;

                if (ignoreDeclaredExtensions && components[i] is IESGameObjectPoolLifecycleExtensionInstaller)
                    continue;

                if (life != null && life.IsRegisteredPoolExtension(candidate))
                    continue;

                if (found != null && !ReferenceEquals(found, candidate))
                {
                    error = "A pooled root has multiple unregistered Pool lifecycle receivers. Bind one root receiver and register the others as extensions.";
                    return null;
                }

                found = candidate;
                foundAny = true;
            }

            return found;
        }

        private bool TryInstallDeclaredPoolExtensions(MonoBehaviour[] components)
        {
            if (components == null || gameObject.activeSelf || poolRootLifecycleComponent == null)
                return components == null || !HasDeclaredPoolExtension(components);

            bool success = true;
            for (int i = 0; i < components.Length; i++)
            {
                if (!(components[i] is IESGameObjectPoolLifecycleExtensionInstaller installer))
                    continue;

                try
                {
                    success &= installer.TryInstallPoolLifecycleExtension(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, components[i]);
                    success = false;
                }
            }

            return success;
        }

        private static bool HasDeclaredPoolExtension(MonoBehaviour[] components)
        {
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IESGameObjectPoolLifecycleExtensionInstaller)
                    return true;
            }

            return false;
        }

        private bool IsRegisteredPoolExtension(IESGameObjectPoolLifecycle candidate)
        {
            if (poolExtensions == null)
                return false;

            for (int i = 0; i < poolExtensions.Count; i++)
            {
                if (ReferenceEquals(poolExtensions[i], candidate))
                    return true;
            }

            return false;
        }

        private bool InvokePoolSpawned(IESGameObjectPoolLifecycle receiver)
        {
            if (!IsAlive(receiver))
                return true;

            try
            {
                receiver.OnPoolSpawned();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private bool InvokePoolDespawned(IESGameObjectPoolLifecycle receiver)
        {
            if (!IsAlive(receiver))
                return true;

            try
            {
                receiver.OnPoolDespawned();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private IESGameObjectPoolLifecycle GetPoolRootLifecycle()
        {
            if (poolRootLifecycleComponent == null)
            {
                poolRootLifecycle = null;
                return null;
            }

            if (IsAlive(poolRootLifecycle) && ReferenceEquals(poolRootLifecycle, poolRootLifecycleComponent))
                return poolRootLifecycle;

            poolRootLifecycle = poolRootLifecycleComponent as IESGameObjectPoolLifecycle;
            return IsAlive(poolRootLifecycle) ? poolRootLifecycle : null;
        }

        private bool TryGetRootComponent(IESGameObjectPoolLifecycle lifecycle, out MonoBehaviour component)
        {
            component = lifecycle as MonoBehaviour;
            return component != null && component.gameObject == gameObject;
        }

        private void RemoveDeadPoolExtensions()
        {
            if (poolExtensions == null || dispatchingPoolLifecycle)
                return;

            for (int i = poolExtensions.Count - 1; i >= 0; i--)
            {
                if (!IsAlive(poolExtensions[i]))
                    poolExtensions.RemoveAt(i);
            }
        }

        private static bool IsAlive(IESGameObjectPoolLifecycle lifecycle)
        {
            if (lifecycle == null)
                return false;

            return !(lifecycle is UnityEngine.Object unityObject) || unityObject != null;
        }
    }
}
