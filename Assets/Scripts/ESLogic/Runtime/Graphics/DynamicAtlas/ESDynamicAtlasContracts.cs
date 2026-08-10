using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ES
{
    public enum ESDynamicAtlasDomainPreset : byte
    {
        [InspectorName("动态图标")] Icons = 0,
        [InspectorName("头像")] Avatars = 1,
        [InspectorName("自定义")] Custom = 2
    }

    public enum ESDynamicAtlasColorSpace : byte
    {
        [InspectorName("sRGB")] SRGB = 0,
        [InspectorName("线性")] Linear = 1
    }

    public enum ESDynamicAtlasAlphaMode : byte
    {
        [InspectorName("普通透明度")] Straight = 0,
        [InspectorName("预乘透明度")] Premultiplied = 1
    }

    public enum ESDynamicAtlasMaterialMode : byte
    {
        [InspectorName("自动")] Auto = 0,
        [InspectorName("普通透明度材质")] Straight = 1,
        [InspectorName("预乘透明度材质")] Premultiplied = 2,
        [InspectorName("自定义材质")] Custom = 3
    }

    /// <summary>Page 处于隔离时 Lease 不解析，Graphic 应显示占位，直到安全探针结束。</summary>
    public enum ESDynamicAtlasLeaseState : byte
    {
        Invalid = 0,
        Ready = 1,
        Retired = 2,
        Recovering = 3,
        Quarantined = 4,
        Failed = 5,
        Lost = 6
    }

    public enum ESDynamicAtlasEntryState : byte
    {
        PendingSource = 0,
        QueuedUpload = 1,
        WaitingGpuFence = 2,
        Ready = 3,
        Retired = 4,
        Failed = 5,
        Lost = 6,
        Quarantined = 7
    }

    public enum ESDynamicAtlasUploadPath : byte
    {
        CopyTexture = 0,
        PaddingShader = 1,
        DeferredFenceFallback = 2
    }

    [Serializable]
    public readonly struct ESDynamicAtlasDomainKey : IEquatable<ESDynamicAtlasDomainKey>
    {
        public readonly string value;

        public ESDynamicAtlasDomainKey(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public bool IsValid => !string.IsNullOrEmpty(value);

        public bool Equals(ESDynamicAtlasDomainKey other)
            => string.Equals(value, other.value, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is ESDynamicAtlasDomainKey other && Equals(other);

        public override int GetHashCode()
            => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);

        public override string ToString() => value ?? string.Empty;

        public static implicit operator ESDynamicAtlasDomainKey(string value)
            => new ESDynamicAtlasDomainKey(value);
    }

    [Serializable]
    public readonly struct ESDynamicAtlasContentKey : IEquatable<ESDynamicAtlasContentKey>
    {
        public readonly string value;
        public readonly string revision;

        public ESDynamicAtlasContentKey(string value, string revision = null)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            this.revision = string.IsNullOrWhiteSpace(revision) ? string.Empty : revision.Trim();
        }

        public bool IsValid => !string.IsNullOrEmpty(value);

        public bool Equals(ESDynamicAtlasContentKey other)
            => string.Equals(value, other.value, StringComparison.Ordinal)
               && string.Equals(revision, other.revision, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is ESDynamicAtlasContentKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
                return (hash * 397) ^ (revision == null ? 0 : StringComparer.Ordinal.GetHashCode(revision));
            }
        }

        public override string ToString()
            => string.IsNullOrEmpty(revision) ? value ?? string.Empty : $"{value}@{revision}";
    }

    [Serializable]
    public struct ESDynamicAtlasRequest : IEquatable<ESDynamicAtlasRequest>
    {
        [Range(0, 16), InspectorName("边缘留白像素")] public int padding;
        [InspectorName("颜色空间")] 
        public ESDynamicAtlasColorSpace colorSpace;
        [InspectorName("透明度模式")] 
        public ESDynamicAtlasAlphaMode alphaMode;
        [InspectorName("采样方式")] 
        public FilterMode filterMode;

        public static ESDynamicAtlasRequest Default => new ESDynamicAtlasRequest
        {
            padding = 4,
            colorSpace = ESDynamicAtlasColorSpace.SRGB,
            alphaMode = ESDynamicAtlasAlphaMode.Straight,
            filterMode = FilterMode.Bilinear
        };

        internal ESDynamicAtlasRequest Sanitized()
        {
            ESDynamicAtlasRequest result = this;
            result.padding = Mathf.Clamp(result.padding, 0, 16);
            if (result.colorSpace != ESDynamicAtlasColorSpace.SRGB
                && result.colorSpace != ESDynamicAtlasColorSpace.Linear)
            {
                result.colorSpace = ESDynamicAtlasColorSpace.SRGB;
            }
            if (result.alphaMode != ESDynamicAtlasAlphaMode.Straight
                && result.alphaMode != ESDynamicAtlasAlphaMode.Premultiplied)
            {
                result.alphaMode = ESDynamicAtlasAlphaMode.Straight;
            }
            if (result.filterMode != FilterMode.Point
                && result.filterMode != FilterMode.Bilinear)
            {
                result.filterMode = FilterMode.Bilinear;
            }
            return result;
        }

        public bool Equals(ESDynamicAtlasRequest other)
            => padding == other.padding
               && colorSpace == other.colorSpace
               && alphaMode == other.alphaMode
               && filterMode == other.filterMode;

        public override bool Equals(object obj)
            => obj is ESDynamicAtlasRequest other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = padding;
                hash = (hash * 397) ^ (int)colorSpace;
                hash = (hash * 397) ^ (int)alphaMode;
                return (hash * 397) ^ (int)filterMode;
            }
        }
    }

    [Serializable]
    public sealed class ESDynamicAtlasDomainPolicy
    {
        [Min(64)] public int pageSize = 1024;
        [Min(1)] public int maxPages = 4;
        [Min(1048576)] public long maxGpuBytes = 64L * 1024L * 1024L;
        [Min(1)] public int maxUploadsPerFrame = 4;
        [Min(1024)] public int maxUploadPixelsPerFrame = 1024 * 1024;
        [Min(0f)] public float unusedEntryKeepAliveSeconds = 15f;

        public ESDynamicAtlasDomainPolicy CloneSanitized()
        {
            int sanitizedPageSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(64, pageSize)), 64, 4096);
            int sanitizedMaxPages = Mathf.Max(1, maxPages);
            long minimumGpuBytes = (long)sanitizedPageSize * sanitizedPageSize * 4L;
            long maximumGpuBytes = minimumGpuBytes * sanitizedMaxPages;
            long sanitizedGpuBytes = Math.Max(minimumGpuBytes, Math.Min(maximumGpuBytes, maxGpuBytes));

            return new ESDynamicAtlasDomainPolicy
            {
                pageSize = sanitizedPageSize,
                maxPages = sanitizedMaxPages,
                maxGpuBytes = sanitizedGpuBytes,
                maxUploadsPerFrame = Mathf.Max(1, maxUploadsPerFrame),
                maxUploadPixelsPerFrame = Mathf.Max(1024, maxUploadPixelsPerFrame),
                unusedEntryKeepAliveSeconds = Mathf.Max(0f, unusedEntryKeepAliveSeconds)
            };
        }

        public static ESDynamicAtlasDomainPolicy CreatePlatformDefault()
        {
            bool mobile = Application.isMobilePlatform;
            return new ESDynamicAtlasDomainPolicy
            {
                pageSize = mobile ? 1024 : 2048,
                maxPages = mobile ? 2 : 4,
                maxGpuBytes = mobile ? 16L * 1024L * 1024L : 64L * 1024L * 1024L,
                maxUploadsPerFrame = mobile ? 2 : 4,
                maxUploadPixelsPerFrame = mobile ? 512 * 512 : 1024 * 1024,
                unusedEntryKeepAliveSeconds = 15f
            };
        }
    }

    public readonly struct ESDynamicAtlasResolved
    {
        public readonly Texture texture;
        public readonly Rect uvRect;
        public readonly Vector2Int pixelSize;
        public readonly int slotGeneration;
        public readonly int placementRevision;
        public readonly int pageGeneration;
        public readonly ESDynamicAtlasAlphaMode alphaMode;

        internal ESDynamicAtlasResolved(
            Texture texture,
            Rect uvRect,
            Vector2Int pixelSize,
            int slotGeneration,
            int placementRevision,
            int pageGeneration,
            ESDynamicAtlasAlphaMode alphaMode)
        {
            this.texture = texture;
            this.uvRect = uvRect;
            this.pixelSize = pixelSize;
            this.slotGeneration = slotGeneration;
            this.placementRevision = placementRevision;
            this.pageGeneration = pageGeneration;
            this.alphaMode = alphaMode;
        }
    }

    internal interface IESDynamicAtlasLeaseHost
    {
        bool TryResolve(long leaseToken, out ESDynamicAtlasResolved resolved);
        bool TryGetLeaseState(long leaseToken, out ESDynamicAtlasLeaseState state);
        void Release(long leaseToken);
        long Subscribe(long leaseToken, Action changed);
        void Unsubscribe(long observationToken);
    }

    internal interface IESDynamicAtlasDomainHost
    {
        void ReleaseDomain(long token);
    }

    public readonly struct ESDynamicAtlasDomainLease : IDisposable
    {
        private readonly IESDynamicAtlasDomainHost host;
        private readonly long token;

        internal ESDynamicAtlasDomainLease(IESDynamicAtlasDomainHost host, long token)
        {
            this.host = host;
            this.token = token;
        }

        public bool IsValid => host != null && token != 0;

        public void Dispose() => host?.ReleaseDomain(token);
    }

    public readonly struct ESDynamicAtlasObservation : IDisposable
    {
        private readonly IESDynamicAtlasLeaseHost host;
        private readonly long token;

        internal ESDynamicAtlasObservation(IESDynamicAtlasLeaseHost host, long token)
        {
            this.host = host;
            this.token = token;
        }

        public bool IsValid => host != null && token != 0;

        public void Dispose() => host?.Unsubscribe(token);
    }

    public readonly struct ESDynamicAtlasLease : IDisposable
    {
        private readonly IESDynamicAtlasLeaseHost host;
        private readonly long token;

        internal ESDynamicAtlasLease(IESDynamicAtlasLeaseHost host, long token)
        {
            this.host = host;
            this.token = token;
        }

        public static ESDynamicAtlasLease Invalid => default;
        public bool IsValid => host != null && token != 0;

        public ESDynamicAtlasLeaseState State
        {
            get
            {
                TryGetState(out ESDynamicAtlasLeaseState state);
                return state;
            }
        }

        public bool TryGetState(out ESDynamicAtlasLeaseState state)
        {
            if (host != null && token != 0 && host.TryGetLeaseState(token, out state))
                return true;

            state = ESDynamicAtlasLeaseState.Invalid;
            return false;
        }

        public bool TryResolve(out ESDynamicAtlasResolved resolved)
        {
            if (host != null && token != 0)
                return host.TryResolve(token, out resolved);

            resolved = default;
            return false;
        }

        public ESDynamicAtlasObservation Subscribe(Action changed)
        {
            if (host == null || token == 0 || changed == null)
                return default;

            return new ESDynamicAtlasObservation(host, host.Subscribe(token, changed));
        }

        public void Dispose() => host?.Release(token);
    }

    public readonly struct ESDynamicAtlasPageSnapshot
    {
        public readonly int pageId;
        public readonly int size;
        public readonly int usedPixels;
        public readonly int freeRectCount;
        public readonly float fragmentation;
        public readonly int pageGeneration;
        public readonly ESDynamicAtlasColorSpace colorSpace;
        public readonly ESDynamicAtlasAlphaMode alphaMode;

        internal ESDynamicAtlasPageSnapshot(int pageId, int size, int usedPixels, int freeRectCount, float fragmentation,
            int pageGeneration, ESDynamicAtlasColorSpace colorSpace, ESDynamicAtlasAlphaMode alphaMode)
        {
            this.pageId = pageId;
            this.size = size;
            this.usedPixels = usedPixels;
            this.freeRectCount = freeRectCount;
            this.fragmentation = fragmentation;
            this.pageGeneration = pageGeneration;
            this.colorSpace = colorSpace;
            this.alphaMode = alphaMode;
        }

        public float Occupancy => size <= 0 ? 0f : usedPixels / (float)(size * size);
    }

    public readonly struct ESDynamicAtlasEntrySnapshot
    {
        public readonly ESDynamicAtlasDomainKey domain;
        public readonly ESDynamicAtlasContentKey content;
        public readonly ESDynamicAtlasEntryState state;
        public readonly int refCount;
        public readonly int providerGeneration;
        public readonly int pageId;
        public readonly Vector2Int pixelSize;
        public readonly int slotGeneration;
        public readonly int placementRevision;
        public readonly bool sourceHeld;
        public readonly Texture pageTexture;
        public readonly Rect uvRect;
        public readonly ESDynamicAtlasUploadPath uploadPath;
        public readonly GraphicsFormat sourceGraphicsFormat;
        public readonly GraphicsFormat pageGraphicsFormat;
        public readonly string failureMessage;

        internal ESDynamicAtlasEntrySnapshot(ESDynamicAtlasDomainKey domain, ESDynamicAtlasContentKey content,
            ESDynamicAtlasEntryState state, int refCount, int providerGeneration, int pageId,
            Vector2Int pixelSize, int slotGeneration, int placementRevision, bool sourceHeld,
            Texture pageTexture, Rect uvRect, ESDynamicAtlasUploadPath uploadPath,
            GraphicsFormat sourceGraphicsFormat, GraphicsFormat pageGraphicsFormat,
            string failureMessage)
        {
            this.domain = domain;
            this.content = content;
            this.state = state;
            this.refCount = refCount;
            this.providerGeneration = providerGeneration;
            this.pageId = pageId;
            this.pixelSize = pixelSize;
            this.slotGeneration = slotGeneration;
            this.placementRevision = placementRevision;
            this.sourceHeld = sourceHeld;
            this.pageTexture = pageTexture;
            this.uvRect = uvRect;
            this.uploadPath = uploadPath;
            this.sourceGraphicsFormat = sourceGraphicsFormat;
            this.pageGraphicsFormat = pageGraphicsFormat;
            this.failureMessage = failureMessage;
        }
    }

    public sealed class ESDynamicAtlasSnapshot
    {
        public bool acceptingRequests;
        public bool providerReady;
        public int providerGeneration;
        public long estimatedGpuBytes;
        public int pendingCount;
        public int readyCount;
        public int retiredCount;
        public int failedCount;
        public int lostCount;
        public int totalEntryCount;
        public int omittedEntryCount;
        public int waitingFenceCount;
        public int copyTextureCount;
        public int paddingShaderCount;
        public int deferredFenceFallbackCount;
        public int pendingFenceReleaseCount;
        public int quarantinedCount;
        public int quarantinedTerminalCount;
        public int quarantineRetryCount;
        public int quarantineFailureCount;
        public int shutdownQuarantinedCount;
        public int shutdownQuarantineFoldedCount;
        public int pageLostCount;
        public float uploadP50Milliseconds;
        public float uploadP95Milliseconds;
        public float uploadP99Milliseconds;
        public readonly List<ESDynamicAtlasPageSnapshot> pages = new List<ESDynamicAtlasPageSnapshot>();
        public readonly List<ESDynamicAtlasEntrySnapshot> entries = new List<ESDynamicAtlasEntrySnapshot>();
        public readonly List<int> quarantinedPageIds = new List<int>();
        public readonly List<string> quarantineReasons = new List<string>();
    }
}
