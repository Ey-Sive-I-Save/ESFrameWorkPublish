using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    /// <summary>运行时动态图集业务门面。显式 Load/Configure 会初始化模块，查询不会。</summary>
    public static class ESDynamicAtlas
    {
        public static readonly ESDynamicAtlasDomainKey UIIcons = new ESDynamicAtlasDomainKey("ui.icons");
        public static readonly ESDynamicAtlasDomainKey UIAvatars = new ESDynamicAtlasDomainKey("ui.avatars");

        public static UniTask<ESDynamicAtlasLease> LoadAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            ESAssetReferTexture2D refer,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            return LoadAsync(domain, content, refer, ESDynamicAtlasRequest.Default, cancellationToken);
        }

        public static UniTask<ESDynamicAtlasLease> LoadAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            ESAssetReferTexture2D refer,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            return RequireModule().AcquireAsync(domain, content, refer, request, cancellationToken);
        }

        /// <summary>
        /// 调用方持有的临时 Texture 上传入口。GPU Fence 完成后图集不再引用源 Texture；
        /// 此入口无法在 RenderTexture Page 丢失后自动重载，长期内容应优先使用 ESAssetReferTexture2D。
        /// </summary>
        public static UniTask<ESDynamicAtlasLease> CopyAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            Texture texture,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            return CopyAsync(domain, content, texture, ESDynamicAtlasRequest.Default, cancellationToken);
        }

        public static UniTask<ESDynamicAtlasLease> CopyAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            Texture texture,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureRuntimeOnly();
            return RequireModule().AcquireAsync(domain, content, texture, request, cancellationToken);
        }

        public static void ConfigureDomain(ESDynamicAtlasDomainKey domain, ESDynamicAtlasDomainPolicy policy)
        {
            EnsureRuntimeOnly();
            RequireModule().ConfigureDomain(domain, policy);
        }

        /// <summary>把页面生命周期绑定到场景、UI Root 或其他系统域；最后一个 Domain Lease 释放时自动关闭 Domain。</summary>
        public static ESDynamicAtlasDomainLease OpenDomain(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasDomainPolicy policy = null)
        {
            EnsureRuntimeOnly();
            return RequireModule().OpenDomain(domain, policy);
        }

        public static void CloseDomain(ESDynamicAtlasDomainKey domain)
        {
            EnsureRuntimeOnly();
            if (ESGameManager.TryGetModule(out ESDynamicAtlasModule module))
                module.CloseDomain(domain);
        }

        public static bool TryGetSnapshot(out ESDynamicAtlasSnapshot snapshot, int maxEntryDetails = int.MaxValue)
        {
            if (ESGameManager.TryGetModule(out ESDynamicAtlasModule module))
            {
                snapshot = module.CreateSnapshot(maxEntryDetails);
                return true;
            }

            snapshot = null;
            return false;
        }

        private static ESDynamicAtlasModule RequireModule()
        {
            ESDynamicAtlasModule module = ESGameManager.GetOrCreateModule<ESDynamicAtlasModule>();
            if (module == null)
                throw new InvalidOperationException("ESGameManager 尚未就绪，无法初始化运行时动态图集模块。 ");
            return module;
        }

        private static void EnsureRuntimeOnly()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("ESDynamicAtlas 运行时请求只能在 Play Mode 或 Player 中执行。编辑器请使用预览接口。 ");
        }
    }
}
