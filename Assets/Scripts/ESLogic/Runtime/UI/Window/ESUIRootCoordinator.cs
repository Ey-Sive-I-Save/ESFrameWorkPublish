using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ES
{
    /// <summary>
    /// One UI World. A root may contain several Canvas hierarchies through its explicit layer
    /// hosts; it is not a synonym for one Canvas. Different roots use independent operation lanes
    /// and independent ui:&lt;root&gt; resource scopes.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/UI/UI Root")]
    public sealed class ESUIRootCoordinator : MonoBehaviour
    {
        private sealed class WindowLane
        {
            internal readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
            internal Transform host;
            internal ESUIWindowInstance activePage;
        }

        [Header("稳定身份")]
        [SerializeField] private string rootKey = ESUI.MainRootKey;
        [SerializeField] private ESUIWindowCatalog catalog;
        [SerializeField, Min(0)] private int maxRetainedInactiveWindows = 8;
        [SerializeField, Min(0.1f)] private float transitionTimeoutSeconds = 10f;

        [Header("Layer Hosts（可分别放在不同 Canvas 下）")]
        [SerializeField] private Transform hudHost;
        [SerializeField] private Transform pageHost;
        [SerializeField] private Transform modalHost;
        [SerializeField] private Transform popupHost;
        [SerializeField] private Transform toastHost;
        [SerializeField] private Transform systemHost;

        private readonly WindowLane[] lanes =
        {
            new WindowLane(), new WindowLane(), new WindowLane(),
            new WindowLane(), new WindowLane(), new WindowLane()
        };
        private readonly Dictionary<ESUIWindowDefinition, ESUIWindowInstance> activeSingletons =
            new Dictionary<ESUIWindowDefinition, ESUIWindowInstance>();
        private readonly Dictionary<ESUIWindowDefinition, ESUIWindowInstance> inactiveSingletons =
            new Dictionary<ESUIWindowDefinition, ESUIWindowInstance>();
        private readonly HashSet<ESUIWindowInstance> allInstances = new HashSet<ESUIWindowInstance>();
        private readonly List<ESUIWindowInstance> instanceBuffer = new List<ESUIWindowInstance>(16);

        private ESUIRootLease rootRegistration;
        private CancellationTokenSource lifetimeCancellation;
        private string resourceScopeKey;
        private int rootGeneration;
        private int nextInstanceGeneration;
        private int nextLeaseToken;
        private long nextOperationId;
        private bool isShuttingDown;
        private ESUIPageNavigator pageNavigator;
        private ESUIFocusCoordinator focusCoordinator;
        private ESUIOverlayArbiter overlayArbiter;
        private ESUITransitionCoordinator transitionCoordinator;
        private ESUIWindowLifecycleEvents lifecycleEvents;
        private ESUIBootstrapCoordinator bootstrapCoordinator;

        internal string RootKey => rootKey;
        public string ResourceScopeKey => resourceScopeKey;
        public ESUIWindowCatalog Catalog => catalog;
        public bool IsRegistered => rootRegistration != null && rootRegistration.IsValid;
        public int RootGeneration => rootGeneration;
        public ESUIPageNavigator PageNavigator => pageNavigator;
        public ESUIFocusCoordinator FocusCoordinator => focusCoordinator;
        public ESUIOverlayArbiter OverlayArbiter => overlayArbiter;
        public ESUITransitionCoordinator TransitionCoordinator => transitionCoordinator;
        public ESUIWindowLifecycleEvents LifecycleEvents => lifecycleEvents;
        public string BootstrapKey => rootKey + "#registration:" + rootGeneration;
        public bool IsBootstrapInFlight =>
            bootstrapCoordinator != null && bootstrapCoordinator.IsInFlight(BootstrapKey);
        public bool TryGetBootstrapAttempt(out long attempt)
        {
            if (bootstrapCoordinator == null)
            {
                attempt = 0;
                return false;
            }

            return bootstrapCoordinator.TryGetAttempt(BootstrapKey, out attempt);
        }
        public ESUIBootstrapCoordinator BootstrapCoordinator => bootstrapCoordinator;

        public bool NotifyFocus(ESUIWindowLease lease) => TryNotifyLifecycle(lease, c => lifecycleEvents?.RaiseFocused(c));
        public bool NotifyBlur(ESUIWindowLease lease) => TryNotifyLifecycle(lease, c => lifecycleEvents?.RaiseBlurred(c));
        public bool NotifyPause(ESUIWindowLease lease) => TryNotifyLifecycle(lease, c => lifecycleEvents?.RaisePaused(c));
        public bool NotifyResume(ESUIWindowLease lease) => TryNotifyLifecycle(lease, c => lifecycleEvents?.RaiseResumed(c));
        public bool NotifyRebind(ESUIWindowLease lease) => TryNotifyLifecycle(lease, c => lifecycleEvents?.RaiseRebound(c));

        public UniTask<ESUIWindowLease> OpenAsync(
            ESUIWindowId windowId,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return OpenAsync(ESUIWindowIdentity.FromBuiltIn(windowId), userData, cancellationToken);
        }

        public UniTask<ESUIWindowLease> OpenAsync(
            string windowKey,
            object userData = null,
            CancellationToken cancellationToken = default)
        {
            return OpenAsync(ESUIWindowIdentity.FromString(windowKey), userData, cancellationToken);
        }

        internal UniTask<ESUIWindowLease> OpenAsync(
            ESUIWindowIdentity identity,
            object userData,
            CancellationToken cancellationToken)
        {
            EnsureRuntimeOnly();
            if (!IsRegistered)
            {
                return UniTask.FromException<ESUIWindowLease>(
                    new InvalidOperationException("UI Root 尚未注册或已关闭：" + rootKey));
            }

            if (catalog == null || !catalog.TryGet(identity, out ESUIWindowDefinition definition))
            {
                return UniTask.FromException<ESUIWindowLease>(
                    new KeyNotFoundException("当前 UI Root 未配置窗口：" + identity));
            }

            return OpenCoreAsync(definition, userData, cancellationToken);
        }

        internal bool IsLeaseCurrent(ESUIWindowLease lease)
        {
            return TryGetCurrentLeaseInstance(lease, out _);
        }

        internal bool TryGetLeaseState(ESUIWindowLease lease, out ESUIWindowState state)
        {
            if (TryGetCurrentLeaseInstance(lease, out ESUIWindowInstance instance))
            {
                state = instance.state;
                return true;
            }

            state = ESUIWindowState.Closed;
            return false;
        }

        internal async UniTask CloseAsync(ESUIWindowLease lease, ESUIWindowCloseEffect requestedEffect)
        {
            if (!TryGetCurrentLeaseInstance(lease, out ESUIWindowInstance instance))
            {
                lease?.MarkReleased();
                return;
            }

            WindowLane lane = GetLane(instance.definition.Layer);
            await lane.gate.WaitAsync();
            try
            {
                if (!TryGetCurrentLeaseInstance(lease, out instance))
                {
                    lease?.MarkReleased();
                    return;
                }

                bool isLastLease = instance.leaseTokens.Count == 1;
                if (requestedEffect != ESUIWindowCloseEffect.Default && !isLastLease)
                {
                    throw new InvalidOperationException(
                        "不能对仍被其他 Lease 持有的窗口强制关闭策略。请先释放其他 Lease。");
                }

                if (!isLastLease)
                {
                    ReleaseLeaseToken(instance, lease);
                    return;
                }

                ESUIWindowCloseEffect effect = ResolveCloseEffect(instance.definition, requestedEffect);
                ValidateCloseEffect(instance, effect);
                if (effect == ESUIWindowCloseEffect.ReturnToPool)
                    await EnsurePoolPrefabAsync(instance.definition, CancellationToken.None);

                if (!TryGetCurrentLeaseInstance(lease, out instance))
                {
                    lease?.MarkReleased();
                    return;
                }

                ReleaseLeaseToken(instance, lease);
                await CloseInstanceAsync(instance, effect);
            }
            finally
            {
                lane.gate.Release();
            }
        }

        internal void CloseImmediately(ESUIWindowLease lease)
        {
            if (!TryGetCurrentLeaseInstance(lease, out ESUIWindowInstance instance))
            {
                lease?.MarkReleased();
                return;
            }

            ReleaseLeaseToken(instance, lease);
            if (instance.leaseTokens.Count > 0)
                return;

            ESUIWindowCloseEffect effect = ResolveCloseEffect(
                instance.definition,
                ESUIWindowCloseEffect.Default);
            CloseInstanceImmediately(instance, effect);
        }

        internal void NotifyViewDestroyed(ESUIWindowView view, ESUIWindowContext context)
        {
            if (view == null || context == null || !ReferenceEquals(context.Root, this))
                return;

            ESUIWindowLease lease = context.Lease;
            ESUIWindowInstance instance = lease != null ? lease.Instance : null;
            if (instance == null || !ReferenceEquals(instance.view, view))
                return;

            GameObject destroyedObject = instance.gameObject;
            if (instance.isPoolManaged
                && !string.IsNullOrEmpty(instance.poolKey)
                && TryGetModule(out ESUIWindowModule module)
                && module.TryGetPoolModule(out ESGameObjectPoolModule poolModule))
            {
                poolModule.NotifyPooledInstanceDestroyed(instance.poolKey, destroyedObject);
            }

            instance.gameObject = null;
            instance.view = null;
            instance.context = null;
            instance.lifetimeCancellation?.Cancel();
            instance.lifetimeCancellation?.Dispose();
            instance.lifetimeCancellation = null;
            instance.runtimeModeLease?.Dispose();
            instance.runtimeModeLease = null;
            InvalidateAllLeases(instance);
            RemoveActiveMappings(instance);
            RemoveInactiveMapping(instance);
            allInstances.Remove(instance);
            instance.isPoolManaged = false;
            instance.poolPrefab = null;
            instance.poolKey = null;
            instance.state = ESUIWindowState.Closed;
        }

        internal void HandleProviderTransitionStarting()
        {
            ShutdownLocalState();
        }

        internal void HandleModuleStopped()
        {
            rootRegistration = null;
            ShutdownLocalState();
        }

        private void Awake()
        {
            ConfigureLanes();
            lifecycleEvents = new ESUIWindowLifecycleEvents();
            pageNavigator = new ESUIPageNavigator(
                (identity, data, token) => OpenAsync(identity, data, token),
                ResolveCanonicalIdentity);
            overlayArbiter = new ESUIOverlayArbiter();
            transitionCoordinator = new ESUITransitionCoordinator();
            focusCoordinator = new ESUIFocusCoordinator(EventSystem.current);
            bootstrapCoordinator = new ESUIBootstrapCoordinator();
        }

        /// <summary>Starts one idempotent asynchronous bootstrap for this registered root.</summary>
        public UniTask<ESUIBootstrapResult> BootstrapAsync(
            Func<CancellationToken, UniTask> prepare,
            CancellationToken cancellationToken = default)
        {
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            if (!IsRegistered)
                return UniTask.FromResult(ESUIBootstrapResult.Create(
                    rootKey, ESUIBootstrapState.Stopped, null, 0));

            return bootstrapCoordinator.StartAsync(BootstrapKey, prepare, cancellationToken);
        }

        /// <summary>
        /// Bootstraps a root with an optional staged context. The snapshot is only consumed when
        /// the caller's prepare/restore delegate completes successfully.
        /// </summary>
        public UniTask<ESUIBootstrapResult> BootstrapAsync(
            ESUICanonicalId canonicalId,
            string scopeKey,
            int schemaVersion,
            ESUIContextStore contextStore,
            Func<ESUIContextSnapshot?, CancellationToken, UniTask> prepare,
            CancellationToken cancellationToken = default)
        {
            if (contextStore == null) throw new ArgumentNullException(nameof(contextStore));
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));

            return BootstrapAsync(async token =>
            {
                ESUIContextSnapshot staged;
                ESUIContextSnapshot? context = contextStore.TryPeek(
                    canonicalId, scopeKey, schemaVersion, out staged)
                    ? staged
                    : (ESUIContextSnapshot?)null;
                await prepare(context, token);
                if (context.HasValue)
                    contextStore.Consume(context.Value);
            }, cancellationToken);
        }

        /// <summary>Restores navigation entries from a staged context using caller-owned decoding.</summary>
        public UniTask<ESUIBootstrapResult> RestoreNavigationAsync(
            ESUICanonicalId canonicalId,
            string scopeKey,
            int schemaVersion,
            ESUIContextStore contextStore,
            Func<string, IReadOnlyList<ESUIPageNavigationEntry>> deserialize,
            CancellationToken cancellationToken = default)
        {
            if (contextStore == null) throw new ArgumentNullException(nameof(contextStore));
            if (deserialize == null) throw new ArgumentNullException(nameof(deserialize));
            if (pageNavigator == null)
                return UniTask.FromResult(ESUIBootstrapResult.Create(rootKey, ESUIBootstrapState.Stopped, null, 0));

            return BootstrapAsync(canonicalId, scopeKey, schemaVersion, contextStore,
                async (snapshot, token) =>
                {
                    if (!snapshot.HasValue) return;
                    IReadOnlyList<ESUIPageNavigationEntry> entries = deserialize(snapshot.Value.Payload);
                    if (entries == null || !await pageNavigator.RestoreAsync(entries, token))
                        throw new InvalidOperationException("UI 导航上下文为空或恢复失败。");
                }, cancellationToken);
        }

        private ESUICanonicalId ResolveCanonicalIdentity(ESUIWindowIdentity identity)
        {
            if (!ESUIWindowIdentityResolver.TryResolve(catalog, identity, out ESUICanonicalId canonicalId, out _, out string error))
                throw new InvalidOperationException(error);
            return canonicalId;
        }

        private bool TryNotifyLifecycle(ESUIWindowLease lease, Action<ESUIWindowContext> callback)
        {
            if (callback == null || !TryGetCurrentLeaseInstance(lease, out ESUIWindowInstance instance) || instance.context == null)
                return false;
            callback(instance.context);
            return true;
        }

        private void OnEnable()
        {
            ConfigureLanes();
            if (focusCoordinator != null && EventSystem.current != null)
                focusCoordinator.Attach(EventSystem.current);
            if (Application.isPlaying)
                TryRegister();
        }

        private void Start()
        {
            if (Application.isPlaying && !IsRegistered)
                TryRegister();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            UnregisterAndShutdown();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
                UnregisterAndShutdown();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!Application.isPlaying || lifecycleEvents == null)
                return;

            instanceBuffer.Clear();
            foreach (ESUIWindowInstance instance in allInstances)
                instanceBuffer.Add(instance);
            for (int i = 0; i < instanceBuffer.Count; i++)
            {
                ESUIWindowInstance instance = instanceBuffer[i];
                if (instance?.context == null || instance.state == ESUIWindowState.Closed)
                    continue;
                if (pauseStatus)
                    lifecycleEvents.RaisePaused(instance.context);
                else
                    lifecycleEvents.RaiseResumed(instance.context);
            }
            instanceBuffer.Clear();
        }

        private async UniTask<ESUIWindowLease> OpenCoreAsync(
            ESUIWindowDefinition definition,
            object userData,
            CancellationToken cancellationToken)
        {
            WindowLane lane = GetLane(definition.Layer);
            using (CancellationTokenSource linkedCancellation =
                   CancellationTokenSource.CreateLinkedTokenSource(
                       cancellationToken,
                       GetLifetimeCancellationToken()))
            {
                bool gateAcquired = false;
                try
                {
                    await lane.gate.WaitAsync(linkedCancellation.Token);
                    gateAcquired = true;
                    linkedCancellation.Token.ThrowIfCancellationRequested();
                    int expectedRootGeneration = rootGeneration;
                    int expectedProviderGeneration = ESAssets.RuntimeBackendGeneration;
                    EnsureCanServe(expectedRootGeneration, expectedProviderGeneration);

                    if (!definition.AllowMultipleInstances
                        && activeSingletons.TryGetValue(definition, out ESUIWindowInstance activeInstance))
                    {
                        return CreateLease(activeInstance);
                    }

                    if (definition.Layer == ESUIWindowLayer.Page && lane.activePage != null)
                        await CloseExclusivePageAsync(lane.activePage);

                    if (!definition.AllowMultipleInstances
                        && inactiveSingletons.TryGetValue(definition, out ESUIWindowInstance inactiveInstance))
                    {
                        inactiveSingletons.Remove(definition);
                        return await ReopenInactiveAsync(
                            inactiveInstance,
                            expectedRootGeneration,
                            expectedProviderGeneration,
                            userData,
                            linkedCancellation.Token);
                    }

                    return await CreateInstanceAsync(
                        definition,
                        expectedRootGeneration,
                        expectedProviderGeneration,
                        userData,
                        linkedCancellation.Token);
                }
                finally
                {
                    if (gateAcquired)
                        lane.gate.Release();
                }
            }
        }

        private async UniTask<ESUIWindowLease> CreateInstanceAsync(
            ESUIWindowDefinition definition,
            int expectedRootGeneration,
            int expectedProviderGeneration,
            object userData,
            CancellationToken cancellationToken)
        {
            ESUIWindowInstance instance = new ESUIWindowInstance
            {
                root = this,
                definition = definition,
                state = ESUIWindowState.Acquiring,
                rootGeneration = expectedRootGeneration,
                instanceGeneration = NextPositive(ref nextInstanceGeneration),
                providerGeneration = expectedProviderGeneration,
                operationId = NextOperationId(),
                isPoolManaged = false
            };
            ESUIWindowLease lease = null;

            try
            {
                GameObject prefab = definition.ClosePolicy == ESUIWindowClosePolicy.PoolOnClose
                    ? await EnsurePoolPrefabAsync(definition, cancellationToken)
                    : await ESAssets.LoadAsync(definition.Prefab, resourceScopeKey, cancellationToken);
                EnsureCanContinue(instance);
                if (prefab == null)
                    throw new InvalidOperationException("窗口 Prefab 加载结果为空：" + definition.name);

                instance.state = ESUIWindowState.Materializing;
                Transform host = GetLane(definition.Layer).host;
                if (definition.ClosePolicy == ESUIWindowClosePolicy.PoolOnClose)
                {
                    if (!TryGetModule(out ESUIWindowModule module)
                        || !module.TryGetPoolModule(out ESGameObjectPoolModule poolModule))
                    throw new InvalidOperationException("PoolOnClose 需要已启用 ESGameObjectPoolModule。");

                    instance.poolPrefab = prefab;
                    instance.poolKey = module.GetPoolKey(definition);
                    instance.gameObject = poolModule.GetInPool(
                        instance.poolKey,
                        Vector3.zero,
                        Quaternion.identity,
                        host);
                    instance.isPoolManaged = instance.gameObject != null;
                }
                else
                {
                    instance.gameObject = Instantiate(prefab, host, false);
                }

                if (instance.gameObject == null)
                    throw new InvalidOperationException("无法创建 UI Window 实例：" + definition.name);

                instance.view = instance.gameObject.GetComponent<ESUIWindowView>();
                if (instance.view == null)
                {
                    throw new InvalidOperationException(
                        "窗口 Prefab 根节点必须挂载 ESUIWindowView：" + definition.name);
                }

                allInstances.Add(instance);
                lease = await BindAndEnterAsync(instance, userData, cancellationToken);
                RegisterActiveInstance(instance);
                return lease;
            }
            catch
            {
                lease?.MarkReleased();
                CloseInstanceImmediately(instance, ESUIWindowCloseEffect.Destroy);
                throw;
            }
        }

        private async UniTask<ESUIWindowLease> ReopenInactiveAsync(
            ESUIWindowInstance instance,
            int expectedRootGeneration,
            int expectedProviderGeneration,
            object userData,
            CancellationToken cancellationToken)
        {
            instance.rootGeneration = expectedRootGeneration;
            instance.instanceGeneration = NextPositive(ref nextInstanceGeneration);
            instance.providerGeneration = expectedProviderGeneration;
            instance.operationId = NextOperationId();
            instance.isRetainedInactive = false;
            instance.state = ESUIWindowState.Binding;

            ESUIWindowLease lease = null;
            try
            {
                EnsureCanContinue(instance);
                lease = await BindAndEnterAsync(instance, userData, cancellationToken);
                RegisterActiveInstance(instance);
                return lease;
            }
            catch
            {
                lease?.MarkReleased();
                CloseInstanceImmediately(instance, ESUIWindowCloseEffect.Destroy);
                throw;
            }
        }

        private async UniTask<ESUIWindowLease> BindAndEnterAsync(
            ESUIWindowInstance instance,
            object userData,
            CancellationToken cancellationToken)
        {
            ESUIWindowLease lease = CreateLease(instance);
            if (!ESUIWindowIdentityResolver.TryResolve(catalog, instance.definition.Identity, out ESUICanonicalId canonicalId, out _, out string identityError))
                throw new InvalidOperationException(identityError);
            instance.context = new ESUIWindowContext(this, instance.definition, lease, userData, instance.operationId, canonicalId);
            instance.lifetimeCancellation = new CancellationTokenSource();
            bool templatePrepared = false;

            try
            {
                using (CancellationTokenSource prepareCancellation =
                       CancellationTokenSource.CreateLinkedTokenSource(
                           cancellationToken,
                           instance.lifetimeCancellation.Token))
                {
                    await instance.view.PrepareTemplateAsync(prepareCancellation.Token);
                    templatePrepared = true;
                }

                if (instance.definition.AcquireRuntimeMode)
                {
                    ESRuntimeModeService runtimeMode = ESGameManager.RuntimeMode;
                    if (runtimeMode == null)
                        throw new InvalidOperationException("窗口要求 RuntimeMode，但 ESGameManager.RuntimeMode 尚未就绪。");

                    instance.runtimeModeLease = runtimeMode.AcquireModeLease(instance.definition.RuntimeMode, instance);
                }

                instance.state = ESUIWindowState.Binding;
                instance.gameObject.SetActive(true);
                instance.gameObject.transform.SetAsLastSibling();
                instance.view.Bind(instance.context);
                lifecycleEvents?.RaiseOpened(instance.context);
                EnsureCanContinue(instance);
                await instance.view.CommitTemplateAsync(instance.lifetimeCancellation.Token);
                EnsureCanContinue(instance);

                instance.state = ESUIWindowState.Entering;
                using (CancellationTokenSource linkedCancellation =
                       CancellationTokenSource.CreateLinkedTokenSource(
                           cancellationToken,
                           instance.lifetimeCancellation.Token))
                {
                    await transitionCoordinator.EnterAsync(
                        instance.view,
                        instance.context,
                        TimeSpan.FromSeconds(transitionTimeoutSeconds),
                        linkedCancellation.Token);
                }
                EnsureCanContinue(instance);

                instance.state = ESUIWindowState.Visible;
                lifecycleEvents?.RaiseShown(instance.context);
                lifecycleEvents?.RaiseFocused(instance.context);
                return lease;
            }
            catch
            {
                if (templatePrepared)
                {
                    try { await instance.view.RollbackTemplateAsync(CancellationToken.None); }
                    catch (Exception rollbackException) { Debug.LogException(rollbackException, this); }
                }
                ReleaseLeaseToken(instance, lease);
                throw;
            }
        }

        private async UniTask CloseExclusivePageAsync(ESUIWindowInstance instance)
        {
            ESUIWindowCloseEffect effect = ResolveCloseEffect(
                instance.definition,
                ESUIWindowCloseEffect.Default);
            if (effect == ESUIWindowCloseEffect.ReturnToPool)
                await EnsurePoolPrefabAsync(instance.definition, CancellationToken.None);

            InvalidateAllLeases(instance);
            await CloseInstanceAsync(instance, effect);
        }

        private async UniTask CloseInstanceAsync(ESUIWindowInstance instance, ESUIWindowCloseEffect effect)
        {
            if (instance == null
                || (instance.state == ESUIWindowState.Closed && !instance.isRetainedInactive))
                return;

            instance.state = ESUIWindowState.Exiting;
            Exception exitException = null;
            bool closeEffectApplied = true;
            try
            {
                if (instance.view != null && instance.context != null)
                {
                    CancellationToken cancellationToken = instance.lifetimeCancellation != null
                        ? instance.lifetimeCancellation.Token
                        : CancellationToken.None;
                    await transitionCoordinator.ExitAsync(
                        instance.view,
                        effect,
                        TimeSpan.FromSeconds(transitionTimeoutSeconds),
                        cancellationToken);
                }
            }
            catch (Exception exception)
            {
                exitException = exception;
            }
            finally
            {
                if (instance.context != null)
                    lifecycleEvents?.RaiseBlurred(instance.context);
                if (instance.context != null)
                    lifecycleEvents?.RaiseClosed(instance.context, effect);
                closeEffectApplied = CloseInstanceImmediately(instance, effect);
            }

            if (exitException != null)
                throw exitException;
            if (!closeEffectApplied)
            {
                throw new InvalidOperationException(
                    "窗口已终止，但请求的关闭效果无法安全完成：" + effect);
            }
        }

        private bool CloseInstanceImmediately(ESUIWindowInstance instance, ESUIWindowCloseEffect effect)
        {
            if (instance == null
                || (instance.state == ESUIWindowState.Closed && !instance.isRetainedInactive))
                return true;

            InvalidateAllLeases(instance);
            ESUIWindowDefinition definition = instance.definition;
            RemoveActiveMappings(instance);
            RemoveInactiveMapping(instance);
            instance.lifetimeCancellation?.Cancel();
            instance.lifetimeCancellation?.Dispose();
            instance.lifetimeCancellation = null;
            instance.runtimeModeLease?.Dispose();
            instance.runtimeModeLease = null;
            instance.view?.Unbind();
            instance.context = null;

            bool closeEffectApplied = true;
            bool canKeepInactive = effect == ESUIWindowCloseEffect.KeepInactive
                                   && definition != null
                                   && !definition.AllowMultipleInstances
                                   && instance.gameObject != null;
            if (canKeepInactive)
            {
                if (instance.isPoolManaged && !TryDetachPooledInstance(instance))
                {
                    closeEffectApplied = false;
                    Debug.LogError("[ESUI] 窗口无法安全脱离对象池以保留为 inactive，实例将被终止。", instance.gameObject);
                }

                if (closeEffectApplied && instance.gameObject != null)
                {
                    instance.gameObject.SetActive(false);
                    instance.isRetainedInactive = true;
                    instance.state = ESUIWindowState.Closed;
                    instance.inactiveOrder = NextOperationId();
                    inactiveSingletons[definition] = instance;
                    TrimRetainedInactiveWindows();
                    return true;
                }
            }

            allInstances.Remove(instance);
            instance.isRetainedInactive = false;
            instance.inactiveOrder = 0;
            instance.state = ESUIWindowState.Closed;

            GameObject gameObject = instance.gameObject;
            instance.gameObject = null;
            instance.view = null;
            if (gameObject == null)
            {
                ClearPoolMetadata(instance);
                return closeEffectApplied;
            }

            if (effect == ESUIWindowCloseEffect.ReturnToPool)
            {
                if (TryReturnToPool(instance, gameObject))
                {
                    ClearPoolMetadata(instance);
                    return closeEffectApplied;
                }

                Debug.LogError("[ESUI] 窗口无法安全归还对象池，实例将被销毁。", gameObject);
                closeEffectApplied = false;
            }
            else if (effect == ESUIWindowCloseEffect.Destroy
                     && instance.isPoolManaged)
            {
                if (TryDestroyPooledInstance(instance, gameObject))
                {
                    ClearPoolMetadata(instance);
                    return closeEffectApplied;
                }

                Debug.LogError("[ESUI] 窗口无法通过对象池终止，实例将被直接销毁。", gameObject);
                closeEffectApplied = false;
            }

            Destroy(gameObject);
            ClearPoolMetadata(instance);
            return closeEffectApplied;
        }

        private bool TryDetachPooledInstance(ESUIWindowInstance instance)
        {
            if (instance == null
                || instance.gameObject == null
                || !TryGetModule(out ESUIWindowModule module)
                || !module.TryGetPoolModule(out ESGameObjectPoolModule poolModule)
                || !poolModule.DetachPooledInstance(instance.gameObject))
            {
                return false;
            }

            instance.isPoolManaged = false;
            try
            {
                instance.gameObject.transform.SetParent(GetLane(instance.definition.Layer).host, false);
            }
            catch (Exception exception)
            {
                // Detachment itself already succeeded. Retaining the object under the pool root
                // is safe because this coordinator still owns and will destroy the instance.
                Debug.LogException(exception, instance.gameObject);
            }
            return true;
        }

        private bool TryReturnToPool(ESUIWindowInstance instance, GameObject gameObject)
        {
            if (instance == null
                || gameObject == null
                || !TryGetModule(out ESUIWindowModule module)
                || !module.TryGetPoolModule(out ESGameObjectPoolModule poolModule))
            {
                return false;
            }

            if (instance.isPoolManaged)
                return poolModule.PushToPool(gameObject);

            return instance.poolPrefab != null
                   && !string.IsNullOrEmpty(instance.poolKey)
                   && poolModule.TryAttachInactiveInstance(
                       instance.poolPrefab,
                       instance.poolKey,
                       gameObject);
        }

        private bool TryDestroyPooledInstance(ESUIWindowInstance instance, GameObject gameObject)
        {
            return instance != null
                   && gameObject != null
                   && TryGetModule(out ESUIWindowModule module)
                   && module.TryGetPoolModule(out ESGameObjectPoolModule poolModule)
                   && poolModule.DestroyPooledInstance(gameObject);
        }

        private static void ClearPoolMetadata(ESUIWindowInstance instance)
        {
            if (instance == null)
                return;

            instance.isPoolManaged = false;
            instance.poolPrefab = null;
            instance.poolKey = null;
        }

        private static void ValidateCloseEffect(
            ESUIWindowInstance instance,
            ESUIWindowCloseEffect effect)
        {
            if (effect == ESUIWindowCloseEffect.KeepInactive
                && instance.definition.AllowMultipleInstances)
            {
                throw new InvalidOperationException(
                    "多实例窗口不能保留为 inactive。请使用 Destroy 或 ReturnToPool。" );
            }

            if (effect == ESUIWindowCloseEffect.ReturnToPool
                && (instance.poolPrefab == null || string.IsNullOrEmpty(instance.poolKey)))
            {
                throw new InvalidOperationException(
                    "此窗口没有 PoolOnClose 所需的 Prefab/Pool Key，不能强制回池。");
            }
        }

        private void RegisterActiveInstance(ESUIWindowInstance instance)
        {
            instance.isRetainedInactive = false;
            if (!instance.definition.AllowMultipleInstances)
                activeSingletons[instance.definition] = instance;

            if (instance.definition.Layer == ESUIWindowLayer.Page)
                GetLane(ESUIWindowLayer.Page).activePage = instance;
        }

        private void RemoveActiveMappings(ESUIWindowInstance instance)
        {
            if (instance == null || instance.definition == null)
                return;

            if (activeSingletons.TryGetValue(instance.definition, out ESUIWindowInstance active)
                && ReferenceEquals(active, instance))
            {
                activeSingletons.Remove(instance.definition);
            }

            WindowLane lane = GetLane(instance.definition.Layer);
            if (ReferenceEquals(lane.activePage, instance))
                lane.activePage = null;
        }

        private void RemoveInactiveMapping(ESUIWindowInstance instance)
        {
            if (instance == null || instance.definition == null)
                return;

            if (inactiveSingletons.TryGetValue(instance.definition, out ESUIWindowInstance inactive)
                && ReferenceEquals(inactive, instance))
            {
                inactiveSingletons.Remove(instance.definition);
            }
        }

        private void TrimRetainedInactiveWindows()
        {
            while (inactiveSingletons.Count > maxRetainedInactiveWindows)
            {
                ESUIWindowInstance oldest = null;
                foreach (ESUIWindowInstance instance in inactiveSingletons.Values)
                {
                    if (oldest == null || instance.inactiveOrder < oldest.inactiveOrder)
                        oldest = instance;
                }

                if (oldest == null)
                    return;

                CloseInstanceImmediately(oldest, ESUIWindowCloseEffect.Destroy);
            }
        }

        private ESUIWindowLease CreateLease(ESUIWindowInstance instance)
        {
            int token = NextPositive(ref nextLeaseToken);
            while (instance.leaseTokens.Contains(token))
                token = NextPositive(ref nextLeaseToken);

            instance.leaseTokens.Add(token);
            return new ESUIWindowLease(this, instance, token);
        }

        private void ReleaseLeaseToken(ESUIWindowInstance instance, ESUIWindowLease lease)
        {
            if (instance == null || lease == null)
                return;

            instance.leaseTokens.Remove(lease.Token);
            lease.MarkReleased();
        }

        private static void InvalidateAllLeases(ESUIWindowInstance instance)
        {
            instance?.leaseTokens.Clear();
        }

        private ESUIWindowCloseEffect ResolveCloseEffect(
            ESUIWindowDefinition definition,
            ESUIWindowCloseEffect requestedEffect)
        {
            if (requestedEffect != ESUIWindowCloseEffect.Default)
                return requestedEffect;

            switch (definition.ClosePolicy)
            {
                case ESUIWindowClosePolicy.DestroyOnClose:
                    return ESUIWindowCloseEffect.Destroy;
                case ESUIWindowClosePolicy.PoolOnClose:
                    return ESUIWindowCloseEffect.ReturnToPool;
                case ESUIWindowClosePolicy.KeepInactive:
                    return ESUIWindowCloseEffect.KeepInactive;
                default:
                    throw new InvalidOperationException("窗口关闭策略无效：" + definition.ClosePolicy);
            }
        }

        private async UniTask<GameObject> EnsurePoolPrefabAsync(
            ESUIWindowDefinition definition,
            CancellationToken cancellationToken)
        {
            if (!IsRegistered || rootRegistration.Root == null)
                throw new InvalidOperationException("UI Root 已关闭，不能转入对象池。");
            if (!TryGetModule(out ESUIWindowModule module))
                throw new InvalidOperationException("ESUIWindowModule 不可用，不能转入对象池。");
            if (!module.TryGetPoolModule(out _))
                throw new InvalidOperationException("PoolOnClose 需要已启用 ESGameObjectPoolModule。");

            return await module.LoadPoolPrefabAsync(definition, cancellationToken);
        }

        private bool TryGetCurrentLeaseInstance(ESUIWindowLease lease, out ESUIWindowInstance instance)
        {
            instance = lease != null ? lease.Instance : null;
            if (lease == null || lease.IsReleased || instance == null || isShuttingDown)
                return false;
            if (!ReferenceEquals(lease.Root, this)
                || lease.RootGeneration != rootGeneration
                || lease.InstanceGeneration != instance.instanceGeneration
                || !ReferenceEquals(instance.root, this)
                || instance.rootGeneration != rootGeneration
                || instance.providerGeneration != ESAssets.RuntimeBackendGeneration
                || instance.state == ESUIWindowState.Closed
                || instance.state == ESUIWindowState.Failed)
            {
                return false;
            }

            return instance.leaseTokens.Contains(lease.Token);
        }

        private void EnsureCanServe(int expectedRootGeneration, int expectedProviderGeneration)
        {
            if (!IsRegistered || isShuttingDown || expectedRootGeneration != rootGeneration)
                throw new OperationCanceledException("UI Root 已关闭或已进入新代。", CancellationToken.None);
            if (!ESAssets.IsReady || expectedProviderGeneration != ESAssets.RuntimeBackendGeneration)
                throw new OperationCanceledException("资源 Provider 尚未就绪或已切换。", CancellationToken.None);
        }

        private void EnsureCanContinue(ESUIWindowInstance instance)
        {
            EnsureCanServe(instance.rootGeneration, instance.providerGeneration);
            if (!ReferenceEquals(instance.root, this)
                || instance.instanceGeneration <= 0)
            {
                throw new OperationCanceledException("UI Window 实例已失效。", CancellationToken.None);
            }
        }

        private void TryRegister()
        {
            if (IsRegistered)
                return;

            if (!TryPrepareRegistration(out string error))
            {
                Debug.LogError("[ESUI] UI Root 注册失败：" + error, this);
                return;
            }

            if (ESGameManager.Instance == null)
            {
                Debug.LogError("[ESUI] UI Root 需要场景中的 ESGameManager。", this);
                return;
            }

            ESUIWindowModule module = ESGameManager.GetOrCreateModule<ESUIWindowModule>();
            if (module == null)
            {
                Debug.LogError("[ESUI] 无法创建 ESUIWindowModule。", this);
                return;
            }

            try
            {
                rootRegistration = module.RegisterRoot(this);
                rootGeneration = NextPositive(ref rootGeneration);
                if (bootstrapCoordinator == null)
                    bootstrapCoordinator = new ESUIBootstrapCoordinator();
                lifetimeCancellation?.Cancel();
                lifetimeCancellation = new CancellationTokenSource();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private bool TryPrepareRegistration(out string error)
        {
            if (!IsValidRootKey(rootKey))
            {
                error = "Root Key 必须是稳定的小写标识，例如 main 或 player-2。";
                return false;
            }

            if (catalog == null)
            {
                error = "缺少 ESUIWindowCatalog。";
                return false;
            }

            if (maxRetainedInactiveWindows < 0)
            {
                error = "保留 inactive 窗口数量不能为负数。";
                return false;
            }

            if (float.IsNaN(transitionTimeoutSeconds) || float.IsInfinity(transitionTimeoutSeconds)
                || transitionTimeoutSeconds <= 0f)
            {
                error = "UI 转场超时必须是有限正数。";
                return false;
            }

            if (!catalog.TryBuild(out error))
                return false;

            resourceScopeKey = "ui:" + rootKey;
            error = null;
            return true;
        }

        private void UnregisterAndShutdown()
        {
            ESUIRootLease registration = rootRegistration;
            rootRegistration = null;
            ShutdownLocalState();
            registration?.Dispose();
        }

        private void ShutdownLocalState()
        {
            if (isShuttingDown)
                return;

            isShuttingDown = true;
            rootGeneration = NextPositive(ref rootGeneration);
            lifetimeCancellation?.Cancel();
            lifetimeCancellation = null;
            bootstrapCoordinator?.Dispose();
            bootstrapCoordinator = null;
            instanceBuffer.Clear();
            foreach (ESUIWindowInstance instance in allInstances)
                instanceBuffer.Add(instance);

            for (int i = 0; i < instanceBuffer.Count; i++)
            {
                ESUIWindowInstance instance = instanceBuffer[i];
                CloseInstanceImmediately(
                    instance,
                    instance.isPoolManaged
                        ? ESUIWindowCloseEffect.ReturnToPool
                        : ESUIWindowCloseEffect.Destroy);
            }

            instanceBuffer.Clear();
            activeSingletons.Clear();
            inactiveSingletons.Clear();
            for (int i = 0; i < lanes.Length; i++)
                lanes[i].activePage = null;

            if (!string.IsNullOrEmpty(resourceScopeKey) && ESAssets.IsReady)
                ESAssets.ReleaseScope(resourceScopeKey);

            isShuttingDown = false;
        }

        private void ConfigureLanes()
        {
            lanes[(int)ESUIWindowLayer.Hud].host = hudHost != null ? hudHost : transform;
            lanes[(int)ESUIWindowLayer.Page].host = pageHost != null ? pageHost : transform;
            lanes[(int)ESUIWindowLayer.Modal].host = modalHost != null ? modalHost : transform;
            lanes[(int)ESUIWindowLayer.Popup].host = popupHost != null ? popupHost : transform;
            lanes[(int)ESUIWindowLayer.Toast].host = toastHost != null ? toastHost : transform;
            lanes[(int)ESUIWindowLayer.System].host = systemHost != null ? systemHost : transform;
        }

        private WindowLane GetLane(ESUIWindowLayer layer)
        {
            int index = (int)layer;
            if (index < 0 || index >= lanes.Length)
                throw new ArgumentOutOfRangeException(nameof(layer), layer, "未知的 UI Window Layer。");
            return lanes[index];
        }

        private bool TryGetModule(out ESUIWindowModule module)
        {
            return ESGameManager.TryGetModule(out module) && module != null;
        }

        private static bool IsValidRootKey(string value)
        {
            if (string.IsNullOrEmpty(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsWhiteSpace(character) || char.IsUpper(character) || character == ':')
                    return false;
            }

            return true;
        }

        private long NextOperationId()
        {
            nextOperationId = nextOperationId == long.MaxValue ? 1 : nextOperationId + 1;
            return nextOperationId;
        }

        private CancellationToken GetLifetimeCancellationToken()
        {
            if (lifetimeCancellation == null && IsRegistered && !isShuttingDown)
                lifetimeCancellation = new CancellationTokenSource();

            return lifetimeCancellation != null
                ? lifetimeCancellation.Token
                : CancellationToken.None;
        }

        private static int NextPositive(ref int value)
        {
            value = value == int.MaxValue ? 1 : value + 1;
            return value;
        }

        private static void EnsureRuntimeOnly()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("ESUIRootCoordinator 只能在 Play Mode 或 Player 运行时打开窗口。");
        }
    }
}
