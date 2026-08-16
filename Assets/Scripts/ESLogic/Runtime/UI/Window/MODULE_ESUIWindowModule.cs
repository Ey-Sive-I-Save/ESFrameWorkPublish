using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// GameManager system module for registered UI roots. It routes public open requests and owns
    /// the shared resource scope required by globally pooled window prefabs.
    /// </summary>
    [Serializable, TypeRegistryItem("系统模块/运行时UI窗口")]
    public sealed class ESUIWindowModule : ESSystemModule
    {
        internal const string PoolResourceScopeKey = "ui:window-pool";

        [NonSerialized] private Dictionary<string, ESUIRootCoordinator> roots;
        [NonSerialized] private List<ESUIRootCoordinator> rootBuffer;
        [NonSerialized] private HashSet<GameObject> pooledPrefabs;
        [NonSerialized] private bool providerEventsBound;
        [NonSerialized] private int nextRegistrationGeneration;
        [NonSerialized] private int poolScopeGeneration;

        public int RootCount => roots != null ? roots.Count : 0;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!Application.isPlaying)
                return;

            EnsureRuntimeState();
            BindProviderEvents();
        }

        protected override void OnDisable()
        {
            StopAllRoots();
            ClearSharedPool();
            UnbindProviderEvents();
            base.OnDisable();
        }

        public override void OnDestroy()
        {
            StopAllRoots();
            ClearSharedPool();
            UnbindProviderEvents();
            base.OnDestroy();
        }

        public UniTask<ESUIWindowLease> OpenAsync(
            string rootKey,
            ESUIWindowId windowId,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return OpenAsync(rootKey, ESUIWindowIdentity.FromBuiltIn(windowId), userData, cancellationToken);
        }

        public UniTask<ESUIWindowLease> OpenAsync(
            string rootKey,
            string windowKey,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return OpenAsync(rootKey, ESUIWindowIdentity.FromString(windowKey), userData, cancellationToken);
        }

        internal UniTask<ESUIWindowLease> OpenAsync(
            string rootKey,
            ESUIWindowIdentity identity,
            object userData,
            CancellationToken cancellationToken)
        {
            EnsureRuntimeOnly();
            EnsureRuntimeState();
            if (string.IsNullOrEmpty(rootKey) || !roots.TryGetValue(rootKey, out ESUIRootCoordinator root))
            {
                return UniTask.FromException<ESUIWindowLease>(
                    new InvalidOperationException("未找到已注册的 UI Root：" + rootKey));
            }

            return root.OpenAsync(identity, userData, cancellationToken);
        }

        internal ESUIRootLease RegisterRoot(ESUIRootCoordinator root)
        {
            EnsureRuntimeOnly();
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            EnsureRuntimeState();
            string rootKey = root.RootKey;
            if (roots.TryGetValue(rootKey, out ESUIRootCoordinator existing))
            {
                if (ReferenceEquals(existing, root))
                    throw new InvalidOperationException("UI Root 已经注册：" + rootKey);

                throw new InvalidOperationException("UI Root Key 重复：" + rootKey);
            }

            int registrationGeneration = NextPositive(ref nextRegistrationGeneration);
            roots.Add(rootKey, root);
            return new ESUIRootLease(this, root, rootKey, registrationGeneration);
        }

        internal bool IsRootRegistrationCurrent(ESUIRootLease registration)
        {
            if (registration == null || registration.IsReleased || roots == null)
                return false;

            return roots.TryGetValue(registration.RootKey, out ESUIRootCoordinator root)
                   && ReferenceEquals(root, registration.Root);
        }

        internal void UnregisterRoot(ESUIRootLease registration)
        {
            if (registration == null || roots == null)
                return;

            if (roots.TryGetValue(registration.RootKey, out ESUIRootCoordinator root)
                && ReferenceEquals(root, registration.Root))
            {
                roots.Remove(registration.RootKey);
            }
        }

        internal async UniTask<GameObject> LoadPoolPrefabAsync(
            ESUIWindowDefinition definition,
            CancellationToken cancellationToken)
        {
            EnsureRuntimeOnly();
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (definition.Prefab == null || !definition.Prefab.IsValid)
                throw new InvalidOperationException("PoolOnClose 窗口缺少有效 Prefab 资源引用：" + definition.name);

            if (!ESAssets.IsReady)
                throw new OperationCanceledException("资源 Provider 尚未就绪，不能加载 PoolOnClose 窗口。", CancellationToken.None);

            int expectedProviderGeneration = ESAssets.RuntimeBackendGeneration;
            int expectedPoolScopeGeneration = poolScopeGeneration;
            GameObject prefab = await ESAssets.LoadAsync(definition.Prefab, PoolResourceScopeKey, cancellationToken);
            if (prefab == null)
                throw new InvalidOperationException("窗口 Prefab 加载结果为空：" + definition.name);

            EnsurePoolLoadCurrent(expectedPoolScopeGeneration, expectedProviderGeneration);
            EnsureRuntimeState();
            pooledPrefabs.Add(prefab);
            return prefab;
        }

        internal bool TryGetPoolModule(out ESGameObjectPoolModule poolModule)
        {
            return ESGameManager.TryGetModule(out poolModule) && poolModule != null;
        }

        private void OnRuntimeBackendTransitionStarting()
        {
            if (roots == null || roots.Count == 0)
            {
                ClearSharedPool();
                return;
            }

            SnapshotRoots();
            for (int i = 0; i < rootBuffer.Count; i++)
                rootBuffer[i]?.HandleProviderTransitionStarting();
            rootBuffer.Clear();
            ClearSharedPool();
        }

        private void StopAllRoots()
        {
            if (roots == null || roots.Count == 0)
                return;

            SnapshotRoots();
            roots.Clear();
            for (int i = 0; i < rootBuffer.Count; i++)
                rootBuffer[i]?.HandleModuleStopped();
            rootBuffer.Clear();
        }

        private void SnapshotRoots()
        {
            EnsureRuntimeState();
            rootBuffer.Clear();
            foreach (ESUIRootCoordinator root in roots.Values)
                rootBuffer.Add(root);
        }

        private void ClearSharedPool()
        {
            poolScopeGeneration = NextPositive(ref poolScopeGeneration);
            if (pooledPrefabs == null)
                return;

            if (TryGetPoolModule(out ESGameObjectPoolModule poolModule))
            {
                foreach (GameObject prefab in pooledPrefabs)
                {
                    if (prefab != null)
                        poolModule.Clear(prefab);
                }
            }

            pooledPrefabs.Clear();
            if (ESAssets.IsReady)
                ESAssets.ReleaseScope(PoolResourceScopeKey);
        }

        private void EnsurePoolLoadCurrent(int expectedPoolScopeGeneration, int expectedProviderGeneration)
        {
            if (expectedPoolScopeGeneration != poolScopeGeneration
                || !ESAssets.IsReady
                || expectedProviderGeneration != ESAssets.RuntimeBackendGeneration)
            {
                throw new OperationCanceledException(
                    "UI Pool Prefab 加载跨越了资源 Provider 或共享 Pool Scope 的代际。",
                    CancellationToken.None);
            }
        }

        private void BindProviderEvents()
        {
            if (providerEventsBound)
                return;

            ESAssets.RuntimeBackendTransitionStarting += OnRuntimeBackendTransitionStarting;
            providerEventsBound = true;
        }

        private void UnbindProviderEvents()
        {
            if (!providerEventsBound)
                return;

            ESAssets.RuntimeBackendTransitionStarting -= OnRuntimeBackendTransitionStarting;
            providerEventsBound = false;
        }

        private void EnsureRuntimeState()
        {
            if (roots == null)
                roots = new Dictionary<string, ESUIRootCoordinator>(4, StringComparer.Ordinal);
            if (rootBuffer == null)
                rootBuffer = new List<ESUIRootCoordinator>(4);
            if (pooledPrefabs == null)
                pooledPrefabs = new HashSet<GameObject>();
        }

        private static void EnsureRuntimeOnly()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("ESUIWindowModule 只能在 Play Mode 或 Player 运行时使用。");
        }

        private static int NextPositive(ref int value)
        {
            value = value == int.MaxValue ? 1 : value + 1;
            return value;
        }
    }

    /// <summary>Generation-safe registration ownership held by one runtime UI root.</summary>
    public sealed class ESUIRootLease : IDisposable
    {
        private ESUIWindowModule module;
        private bool released;

        internal ESUIRootLease(
            ESUIWindowModule module,
            ESUIRootCoordinator root,
            string rootKey,
            int registrationGeneration)
        {
            this.module = module;
            Root = root;
            RootKey = rootKey;
            RegistrationGeneration = registrationGeneration;
        }

        internal ESUIRootCoordinator Root { get; }
        internal string RootKey { get; }
        internal int RegistrationGeneration { get; }
        internal bool IsReleased => released;
        public bool IsValid => !released && module != null && module.IsRootRegistrationCurrent(this);

        public void Dispose()
        {
            if (released)
                return;

            released = true;
            ESUIWindowModule currentModule = module;
            module = null;
            currentModule?.UnregisterRoot(this);
        }
    }
}
