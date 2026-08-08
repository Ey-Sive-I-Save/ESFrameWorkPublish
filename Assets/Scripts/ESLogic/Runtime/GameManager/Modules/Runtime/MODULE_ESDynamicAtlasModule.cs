using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("系统模块/运行时动态图集")]
    public sealed class ESDynamicAtlasModule : ESSystemModule
    {
        [Title("默认策略")]
        [LabelText("按运行平台使用默认策略")]
        [Tooltip("开启时在实际运行平台选择默认页大小和预算；关闭后使用下方手动策略。")]
        public bool usePlatformDefaultPolicy = true;

        [LabelText("手动策略（关闭上项后生效）")]
        [Tooltip("仅在关闭“按运行平台使用默认策略”后使用。")]
        public ESDynamicAtlasDomainPolicy defaultPolicy = ESDynamicAtlasDomainPolicy.CreatePlatformDefault();

        [NonSerialized] private ESDynamicAtlasRuntime runtime;
        [NonSerialized] private bool providerEventsBound;

        public bool IsReady => runtime != null && runtime.IsAcceptingRequests;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (!Application.isPlaying)
                return;

            EnsureRuntime();
            BindProviderEvents();
            ESGameManager.RefreshStaticCache();
        }

        protected override void OnDisable()
        {
            UnbindProviderEvents();
            DisposeRuntime();
            base.OnDisable();
        }

        protected override void Update()
        {
            if (!Application.isPlaying)
                return;

            if (runtime == null)
            {
                EnsureRuntime();
                BindProviderEvents();
            }

            runtime?.Tick();
        }

        public override void OnDestroy()
        {
            UnbindProviderEvents();
            DisposeRuntime();
            base.OnDestroy();
        }

        public UniTask<ESDynamicAtlasLease> AcquireAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            ESAssetReferTexture2D refer,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            EnsureRuntime();
            EnsureDefaultDomainPolicy(domain);
            return runtime.AcquireAsync(domain, content, refer, request, cancellationToken);
        }

        public UniTask<ESDynamicAtlasLease> AcquireAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            Texture texture,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            EnsureRuntime();
            EnsureDefaultDomainPolicy(domain);
            return runtime.AcquireAsync(domain, content, texture, request, cancellationToken);
        }

        public void ConfigureDomain(ESDynamicAtlasDomainKey domain, ESDynamicAtlasDomainPolicy policy)
        {
            EnsureRuntimeOnly();
            EnsureRuntime();
            runtime.ConfigureDomain(domain, policy);
        }

        public ESDynamicAtlasDomainLease OpenDomain(ESDynamicAtlasDomainKey domain, ESDynamicAtlasDomainPolicy policy)
        {
            EnsureRuntimeOnly();
            EnsureRuntime();
            return runtime.OpenDomain(domain, policy ?? ResolveDefaultPolicy());
        }

        public void CloseDomain(ESDynamicAtlasDomainKey domain)
        {
            EnsureRuntimeOnly();
            runtime?.CloseDomain(domain);
        }

        public ESDynamicAtlasSnapshot CreateSnapshot(int maxEntryDetails = int.MaxValue)
        {
            return runtime?.CreateSnapshot(maxEntryDetails) ?? new ESDynamicAtlasSnapshot();
        }

        private void EnsureRuntime()
        {
            if (runtime == null)
                runtime = new ESDynamicAtlasRuntime();
        }

        private static void EnsureRuntimeOnly()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("ESDynamicAtlasModule 只能在 Play Mode 或 Player 运行时使用；编辑器请使用预览与只读监视器。");
        }

        private void EnsureDefaultDomainPolicy(ESDynamicAtlasDomainKey domain)
        {
            runtime.ConfigureDomainIfMissing(domain, ResolveDefaultPolicy());
        }

        private ESDynamicAtlasDomainPolicy ResolveDefaultPolicy()
        {
            if (usePlatformDefaultPolicy)
                return ESDynamicAtlasDomainPolicy.CreatePlatformDefault();

            if (defaultPolicy == null)
                defaultPolicy = ESDynamicAtlasDomainPolicy.CreatePlatformDefault();
            return defaultPolicy;
        }

        private void BindProviderEvents()
        {
            if (providerEventsBound)
                return;

            ESAssets.RuntimeBackendTransitionStarting += OnRuntimeBackendTransitionStarting;
            ESAssets.RuntimeBackendRebuilt += OnRuntimeBackendRebuilt;
            providerEventsBound = true;
        }

        private void UnbindProviderEvents()
        {
            if (!providerEventsBound)
                return;

            ESAssets.RuntimeBackendTransitionStarting -= OnRuntimeBackendTransitionStarting;
            ESAssets.RuntimeBackendRebuilt -= OnRuntimeBackendRebuilt;
            providerEventsBound = false;
        }

        private void OnRuntimeBackendTransitionStarting()
        {
            runtime?.HandleProviderTransitionStarting();
        }

        private void OnRuntimeBackendRebuilt()
        {
            runtime?.HandleProviderRebuilt();
        }

        private void DisposeRuntime()
        {
            runtime?.Dispose();
            runtime = null;
        }
    }
}
