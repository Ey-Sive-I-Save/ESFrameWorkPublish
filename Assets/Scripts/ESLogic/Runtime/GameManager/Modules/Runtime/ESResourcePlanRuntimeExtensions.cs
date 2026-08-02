using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ES
{
    public interface IESResourcePlanExtensionLease { void Release(); }

    public sealed class ESResourcePlanExtensionContext
    {
        private readonly Func<ESAssetIdentity, UnityEngine.Object> getLoaded;
        internal ESResourcePlanExtensionContext(Func<ESAssetIdentity, UnityEngine.Object> getLoaded) { this.getLoaded = getLoaded; }
        public bool TryGetLoadedAsset<T>(ESAssetIdentity identity, out T asset) where T : UnityEngine.Object
        {
            asset = getLoaded?.Invoke(identity) as T;
            return asset != null;
        }
    }

    public interface IESResourcePlanRuntimeExtension
    {
        string ProviderId { get; }
        int SchemaVersion { get; }
        UniTask<IESResourcePlanExtensionLease> PrepareAsync(ESResourcePlanInfo plan, ESResourcePlanBakedExtensionEntry entry, ESResourcePlanExtensionContext context, CancellationToken cancellationToken);
    }

    public static class ESResourcePlanRuntimeExtensions
    {
        private static readonly Dictionary<string, IESResourcePlanRuntimeExtension> Extensions = new Dictionary<string, IESResourcePlanRuntimeExtension>(StringComparer.Ordinal);
        public static void Register(IESResourcePlanRuntimeExtension extension)
        {
            if (extension == null || string.IsNullOrWhiteSpace(extension.ProviderId) || extension.SchemaVersion <= 0)
                throw new ArgumentException("ResourcePlan Runtime extension 无效。", nameof(extension));
            if (!Extensions.TryAdd(extension.ProviderId, extension))
                throw new InvalidOperationException("ResourcePlan Runtime extension ProviderId 重复：" + extension.ProviderId);
        }
        internal static IESResourcePlanRuntimeExtension Resolve(string providerId, int schemaVersion)
        {
            if (!Extensions.TryGetValue(providerId ?? string.Empty, out IESResourcePlanRuntimeExtension extension))
                throw new InvalidOperationException("ResourcePlan Runtime extension 未注册：" + providerId);
            if (extension.SchemaVersion != schemaVersion)
                throw new InvalidOperationException("ResourcePlan Runtime extension SchemaVersion 不兼容：" + providerId);
            return extension;
        }
    }
}
