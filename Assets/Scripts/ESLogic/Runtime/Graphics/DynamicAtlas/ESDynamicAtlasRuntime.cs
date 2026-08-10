using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Unity.Profiling;
using UnityEngine.Rendering;

namespace ES
{
    internal sealed class ESDynamicAtlasRuntime : IESDynamicAtlasLeaseHost, IESDynamicAtlasDomainHost, IDisposable
    {
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("ES.DynamicAtlas.Tick");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("ES.DynamicAtlas.Upload");

        private readonly struct EntryKey : IEquatable<EntryKey>
        {
            public readonly ESDynamicAtlasDomainKey domain;
            public readonly ESDynamicAtlasContentKey content;
            public readonly ESDynamicAtlasRequest request;
            public readonly int providerGeneration;
            public readonly int domainGeneration;

            public EntryKey(ESDynamicAtlasDomainKey domain, ESDynamicAtlasContentKey content,
                ESDynamicAtlasRequest request, int providerGeneration, int domainGeneration)
            {
                this.domain = domain;
                this.content = content;
                this.request = request;
                this.providerGeneration = providerGeneration;
                this.domainGeneration = domainGeneration;
            }

            public bool Equals(EntryKey other)
                => domain.Equals(other.domain)
                   && content.Equals(other.content)
                   && request.Equals(other.request)
                   && providerGeneration == other.providerGeneration
                   && domainGeneration == other.domainGeneration;

            public override bool Equals(object obj) => obj is EntryKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = domain.GetHashCode();
                    hash = (hash * 397) ^ content.GetHashCode();
                    hash = (hash * 397) ^ request.GetHashCode();
                    hash = (hash * 397) ^ providerGeneration;
                    return (hash * 397) ^ domainGeneration;
                }
            }
        }

        private sealed class SourceDescriptor
        {
            public ESAssetReferTexture2D refer;
            public Texture directTexture;
            public bool IsResourceBacked => refer != null;
            public bool CanReload => refer != null || directTexture != null;
        }

        private sealed class SourceHold : IDisposable
        {
            public Texture texture;
            public ESAssetTemporaryLease<Texture2D> temporaryLease;
            public bool ownsTemporaryLease;

            public void Dispose()
            {
                if (ownsTemporaryLease)
                    temporaryLease.Dispose();
                ownsTemporaryLease = false;
                temporaryLease = default;
                texture = null;
            }
        }

        private sealed class Page
        {
            public int id;
            public ESDynamicAtlasDomainKey domain;
            public ESDynamicAtlasColorSpace colorSpace;
            public ESDynamicAtlasAlphaMode alphaMode;
            public FilterMode filterMode;
            public int size;
            public int generation;
            public RenderTexture texture;
            public ESDynamicAtlasAllocator allocator;
            public int inFlightUploadCount;
            public int quarantinedUploadCount;
            public bool recoveryPending;

            public bool IsQuarantined => quarantinedUploadCount > 0;
            public bool HasUnsafeGpuUse => inFlightUploadCount > 0 || quarantinedUploadCount > 0;

            public GraphicsFormat GraphicsFormat => colorSpace == ESDynamicAtlasColorSpace.SRGB
                ? GraphicsFormat.R8G8B8A8_SRGB
                : GraphicsFormat.R8G8B8A8_UNorm;

            public bool Matches(ESDynamicAtlasDomainKey targetDomain, in ESDynamicAtlasRequest request)
                => domain.Equals(targetDomain)
                   && colorSpace == request.colorSpace
                   && alphaMode == request.alphaMode
                   && filterMode == request.filterMode;

            public void CreateTexture()
            {
                if (texture == null)
                {
                    var descriptor = new RenderTextureDescriptor(size, size)
                    {
                        graphicsFormat = GraphicsFormat,
                        depthBufferBits = 0,
                        msaaSamples = 1,
                        mipCount = 1,
                        useMipMap = false,
                        autoGenerateMips = false,
                        enableRandomWrite = false,
                        sRGB = colorSpace == ESDynamicAtlasColorSpace.SRGB
                    };
                    texture = new RenderTexture(descriptor)
                    {
                        name = $"ES Dynamic Atlas {domain} Page {id}",
                        filterMode = filterMode,
                        wrapMode = TextureWrapMode.Clamp,
                        anisoLevel = 0,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                if (!texture.IsCreated())
                    texture.Create();
                generation++;
            }

            public void Dispose()
            {
                if (texture == null)
                    return;

                texture.Release();
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(texture);
                else
                    UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
            }
        }

        private sealed class Entry
        {
            public EntryKey key;
            public SourceDescriptor source;
            public ESDynamicAtlasEntryState state;
            public Page page;
            public RectInt allocatedRect;
            public RectInt contentRect;
            public int slotGeneration;
            public int placementRevision;
            public int refCount;
            public float lastReleaseTime;
            public int sourceLoadSerial;
            public bool providerRetired;
            public bool initialCompletionSettled;
            public bool sourceHeld;
            public bool pageRecoveryPending;
            public bool pageLost;
            public bool placementReleasePending;
            public bool resolveInvalidated;
            public string failureMessage;
            public ESDynamicAtlasUploadPath uploadPath;
            public GraphicsFormat sourceGraphicsFormat;
            public SourceHold sourceHold;
            public readonly UniTaskCompletionSource<bool> initialCompletion = new UniTaskCompletionSource<bool>();
            public readonly Dictionary<long, Action> observers = new Dictionary<long, Action>();

            public Vector2Int PixelSize => new Vector2Int(contentRect.width, contentRect.height);
        }

        private readonly struct LeaseRecord
        {
            public readonly Entry entry;
            public readonly int slotGeneration;

            public LeaseRecord(Entry entry)
            {
                this.entry = entry;
                slotGeneration = entry.slotGeneration;
            }
        }

        private readonly struct ObservationRecord
        {
            public readonly Entry entry;
            public readonly long leaseToken;

            public ObservationRecord(Entry entry, long leaseToken)
            {
                this.entry = entry;
                this.leaseToken = leaseToken;
            }
        }

        private sealed class UploadJob
        {
            public Entry entry;
            public Page page;
            public SourceHold sourceHold;
            public GraphicsFence fence;
            public bool hasFence;
            public AsyncGPUReadbackRequest readbackRequest;
            public bool hasReadbackRequest;
            public Exception completionFailure;
            public float startedAt;
            public bool quarantined;
            public bool quarantineTerminal;
            public int quarantineFailureCount;
            public float lastProbeTime;
        }

        private sealed class ShutdownQuarantine
        {
            public readonly List<UploadJob> uploads;
            public readonly List<Page> pages;
            public readonly Material paddingMaterial;
            public readonly string reason;

            public ShutdownQuarantine(List<UploadJob> uploads, List<Page> pages,
                Material paddingMaterial, string reason)
            {
                this.uploads = uploads;
                this.pages = pages;
                this.paddingMaterial = paddingMaterial;
                this.reason = reason;
            }
        }

        private sealed class ShutdownQuarantineDiagnostic
        {
            public readonly int uploadCount;
            public readonly List<int> pageIds;
            public readonly string reason;

            public ShutdownQuarantineDiagnostic(int uploadCount, List<int> pageIds, string reason)
            {
                this.uploadCount = uploadCount;
                this.pageIds = pageIds;
                this.reason = reason;
            }
        }

        private sealed class AcquireWaiter
        {
            public readonly UniTaskCompletionSource<ESDynamicAtlasLease> completion
                = new UniTaskCompletionSource<ESDynamicAtlasLease>();
            public readonly CancellationToken cancellationToken;
            public CancellationTokenRegistration cancellationRegistration;

            public AcquireWaiter(CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }
        }

        private enum UploadCompletionState : byte
        {
            Pending = 0,
            Completed = 1,
            Failed = 2
        }

        private const float QuarantineProbeIntervalSeconds = 0.25f;
        private const int MaxQuarantineProbeFailures = 3;
        private const int MaxShutdownQuarantineDiagnostics = 16;

        private static readonly List<ShutdownQuarantine> shutdownQuarantines
            = new List<ShutdownQuarantine>();
        private static readonly List<ShutdownQuarantineDiagnostic> shutdownQuarantineDiagnostics
            = new List<ShutdownQuarantineDiagnostic>();
        private static int shutdownQuarantineFoldedCount;
        private readonly Dictionary<EntryKey, Entry> entries = new Dictionary<EntryKey, Entry>();
        private readonly Dictionary<ESDynamicAtlasDomainKey, ESDynamicAtlasDomainPolicy> policies
            = new Dictionary<ESDynamicAtlasDomainKey, ESDynamicAtlasDomainPolicy>();
        private readonly List<Page> pages = new List<Page>();
        private readonly Queue<Entry> uploadQueue = new Queue<Entry>();
        private readonly List<UploadJob> inFlightUploads = new List<UploadJob>();
        private readonly List<UploadJob> quarantinedUploads = new List<UploadJob>();
        private readonly Stack<CommandBuffer> reusableUploadCommandBuffers = new Stack<CommandBuffer>();
        private readonly Dictionary<long, LeaseRecord> leases = new Dictionary<long, LeaseRecord>();
        private readonly Dictionary<long, ObservationRecord> observations = new Dictionary<long, ObservationRecord>();

        private static readonly Action<object> CancelAcquireWaiterCallback = CancelAcquireWaiter;
        private readonly Dictionary<long, ESDynamicAtlasDomainKey> domainLeaseTokens
            = new Dictionary<long, ESDynamicAtlasDomainKey>();
        private readonly Dictionary<ESDynamicAtlasDomainKey, int> domainLeaseCounts
            = new Dictionary<ESDynamicAtlasDomainKey, int>();
        private readonly Dictionary<ESDynamicAtlasDomainKey, int> domainGenerations
            = new Dictionary<ESDynamicAtlasDomainKey, int>();
        private readonly List<float> uploadSamplesMilliseconds = new List<float>(256);
        private readonly List<float> sortedUploadSamplesMilliseconds = new List<float>(256);
        private readonly List<Entry> domainRemovalBuffer = new List<Entry>(64);
        private readonly List<Entry> recoveryRemovalBuffer = new List<Entry>(64);
        private readonly List<Entry> evictionRemovalBuffer = new List<Entry>(64);
        private readonly List<Entry> evictionCandidatesBuffer = new List<Entry>(64);
        private readonly List<long> observationRemovalBuffer = new List<long>(16);
        private readonly Stack<List<Action>> observerCallbackBufferPool = new Stack<List<Action>>(4);
        private readonly Dictionary<ESDynamicAtlasDomainKey, int> frameUploadCounts
            = new Dictionary<ESDynamicAtlasDomainKey, int>();
        private readonly Dictionary<ESDynamicAtlasDomainKey, int> frameUploadPixels
            = new Dictionary<ESDynamicAtlasDomainKey, int>();

        private Material paddingMaterial;
        private MaterialPropertyBlock paddingProperties;
        private bool acceptingRequests = true;
        private bool disposed;
        private int nextPageId;
        private int nextSlotGeneration;
        private long nextLeaseToken;
        private long nextObservationToken;
        private long nextDomainLeaseToken;
        private int copyTextureCount;
        private int paddingShaderCount;
        private int deferredFenceFallbackCount;
        private int pageLostCount;
        private int deferredFenceReleaseCount;
        private int quarantineRetryCount;
        private int quarantineFailureCount;
        private bool graphicsFencePollingUnavailable;

        public bool IsAcceptingRequests => !disposed && acceptingRequests && ESAssets.IsReady;

        private bool IsAcceptingDirectRequests => !disposed && acceptingRequests;

#if UNITY_EDITOR
        // Editor-only white-box controls keep GPU failure-path coverage out of
        // Player builds. They are internal and visible only to the dedicated
        // DynamicAtlas test assemblies.
        internal bool ForceAsyncGpuReadbackCompletionForTests
        {
            get => graphicsFencePollingUnavailable;
            set => graphicsFencePollingUnavailable = value;
        }

        internal bool ForceUnknownGpuSubmissionForTests { get; set; }
        internal bool ForceAsyncGpuReadbackFailureForTests { get; set; }
#endif

        public void ConfigureDomain(ESDynamicAtlasDomainKey domain, ESDynamicAtlasDomainPolicy policy)
        {
            EnsureRuntimeOnly();
            if (!domain.IsValid)
                throw new ArgumentException("动态图集 Domain Key 不能为空。", nameof(domain));

            policies[domain] = (policy ?? ESDynamicAtlasDomainPolicy.CreatePlatformDefault()).CloneSanitized();
        }

        public void ConfigureDomainIfMissing(ESDynamicAtlasDomainKey domain, ESDynamicAtlasDomainPolicy policy)
        {
            if (!policies.ContainsKey(domain))
                ConfigureDomain(domain, policy);
        }

        public ESDynamicAtlasDomainLease OpenDomain(ESDynamicAtlasDomainKey domain, ESDynamicAtlasDomainPolicy policy)
        {
            EnsureRuntimeOnly();
            if (disposed)
                throw new ObjectDisposedException(nameof(ESDynamicAtlasRuntime));
            if (!domain.IsValid)
                throw new ArgumentException("动态图集 Domain Key 不能为空。", nameof(domain));

            ConfigureDomainIfMissing(domain, policy);
            long token = NextDomainLeaseToken();
            domainLeaseTokens.Add(token, domain);
            domainLeaseCounts.TryGetValue(domain, out int count);
            domainLeaseCounts[domain] = count + 1;
            return new ESDynamicAtlasDomainLease(this, token);
        }

        public void ReleaseDomain(long token)
        {
            if (token == 0 || !domainLeaseTokens.TryGetValue(token, out ESDynamicAtlasDomainKey domain))
                return;

            domainLeaseTokens.Remove(token);
            if (!domainLeaseCounts.TryGetValue(domain, out int count) || count <= 1)
            {
                domainLeaseCounts.Remove(domain);
                CloseDomain(domain);
            }
            else
            {
                domainLeaseCounts[domain] = count - 1;
            }
        }

        public UniTask<ESDynamicAtlasLease> AcquireAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            ESAssetReferTexture2D refer,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken)
        {
            EnsureRuntimeOnly();
            if (refer == null || !refer.IsValid)
                throw new ArgumentException("动态图集资源引用无效。", nameof(refer));

            var source = new SourceDescriptor { refer = refer };
            return BeginAcquire(domain, content, source, request,
                requiresProvider: true, cancellationToken: cancellationToken);
        }

        public UniTask<ESDynamicAtlasLease> AcquireAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            Texture texture,
            ESDynamicAtlasRequest request,
            CancellationToken cancellationToken)
        {
            EnsureRuntimeOnly();
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));
            if (texture.dimension != TextureDimension.Tex2D)
                throw new ArgumentException("动态图集只接受二维 Texture。", nameof(texture));

            var source = new SourceDescriptor { directTexture = texture };
            return BeginAcquire(domain, content, source, request,
                requiresProvider: false, cancellationToken: cancellationToken);
        }

        private UniTask<ESDynamicAtlasLease> BeginAcquire(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            SourceDescriptor source,
            ESDynamicAtlasRequest request,
            bool requiresProvider,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled<ESDynamicAtlasLease>(cancellationToken);

            var waiter = new AcquireWaiter(cancellationToken);
            if (cancellationToken.CanBeCanceled)
            {
                waiter.cancellationRegistration = cancellationToken.RegisterWithoutCaptureExecutionContext(
                    CancelAcquireWaiterCallback, waiter);
            }

            CompleteAcquireAsync(waiter, domain, content, source, request, requiresProvider).Forget();
            return waiter.completion.Task;
        }

        private async UniTaskVoid CompleteAcquireAsync(
            AcquireWaiter waiter,
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            SourceDescriptor source,
            ESDynamicAtlasRequest request,
            bool requiresProvider)
        {
            try
            {
                await UniTask.SwitchToMainThread();
                if (waiter.cancellationToken.IsCancellationRequested)
                    return;

                EnsureCanAcquire(domain, content, requiresProvider);
                int providerGeneration = requiresProvider ? ESAssets.RuntimeBackendGeneration : -1;
                ESDynamicAtlasLease lease = await AcquireCoreAsync(
                    domain, content, source, request, providerGeneration);
                if (!waiter.completion.TrySetResult(lease))
                {
                    // The caller cancelled after the shared upload completed. The
                    // upload stays cacheable, but the unobservable Lease must not
                    // retain the Entry.
                    lease.Dispose();
                }
            }
            catch (Exception exception)
            {
                if (exception is OperationCanceledException && waiter.cancellationToken.IsCancellationRequested)
                    waiter.completion.TrySetCanceled(waiter.cancellationToken);
                else
                    waiter.completion.TrySetException(exception);
            }
            finally
            {
                waiter.cancellationRegistration.Dispose();
            }
        }

        private static void CancelAcquireWaiter(object state)
        {
            var waiter = (AcquireWaiter)state;
            waiter.completion.TrySetCanceled(waiter.cancellationToken);
        }

        private async UniTask<ESDynamicAtlasLease> AcquireCoreAsync(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            SourceDescriptor source,
            ESDynamicAtlasRequest request,
            int providerGeneration)
        {
            request = request.Sanitized();
            var key = new EntryKey(domain, content, request, providerGeneration, GetDomainGeneration(domain));
            if (entries.TryGetValue(key, out Entry failedEntry)
                && failedEntry.state == ESDynamicAtlasEntryState.Failed
                && failedEntry.refCount == 0)
            {
                // A failed entry has no lease to preserve. Remove it before a retry so
                // callers do not depend on a later Tick() merely to make the same key
                // loadable again. EvictEntry still refuses unsafe GPU/page states.
                EvictEntry(failedEntry);
            }
            if (!entries.TryGetValue(key, out Entry entry))
            {
                entry = new Entry
                {
                    key = key,
                    source = source,
                    state = ESDynamicAtlasEntryState.PendingSource
                };
                entries.Add(key, entry);
                BeginSourceLoad(entry);
            }

            await entry.initialCompletion.Task;
            await UniTask.SwitchToMainThread();

            if (disposed || !entries.TryGetValue(key, out Entry current) || !ReferenceEquals(entry, current)
                || (entry.state != ESDynamicAtlasEntryState.Ready && entry.state != ESDynamicAtlasEntryState.Retired))
            {
                throw new InvalidOperationException($"动态图集条目 {content} 在取得 Lease 前已经失效。");
            }

            return CreateLease(entry);
        }

        public void Tick()
        {
            if (disposed)
                return;

            using (TickMarker.Auto())
            {
                CompleteFinishedUploads();
                ProbeQuarantinedUploads();
                DetectLostPages();
                StartBudgetedUploads();
                EvictUnusedEntries();
            }
        }

        public void HandleProviderTransitionStarting()
        {
            if (disposed)
                return;

            acceptingRequests = false;
            // Notify() can synchronously cause a Graphic to release its Lease;
            // Release() may then evict the Entry. Never enumerate the live
            // dictionary while invoking user callbacks.
            List<Entry> transitionEntries = new List<Entry>(entries.Values);
            for (int index = 0; index < transitionEntries.Count; index++)
            {
                Entry entry = transitionEntries[index];
                if (!entries.TryGetValue(entry.key, out Entry current)
                    || !ReferenceEquals(current, entry))
                    continue;
                if (!entry.source.IsResourceBacked)
                    continue;

                entry.providerRetired = true;
                entry.resolveInvalidated = false;
                if (entry.state == ESDynamicAtlasEntryState.Ready)
                {
                    entry.state = ESDynamicAtlasEntryState.Retired;
                    Notify(entry);
                    continue;
                }

                if (entry.state == ESDynamicAtlasEntryState.WaitingGpuFence)
                {
                    if (!entry.initialCompletionSettled)
                    {
                        entry.initialCompletionSettled = true;
                        entry.initialCompletion.TrySetException(
                            new OperationCanceledException("资源 Provider 正在切换，动态图集旧代上传不再交付新 Lease。"));
                    }
                    continue;
                }

                if (entry.state == ESDynamicAtlasEntryState.Quarantined)
                {
                    Notify(entry);
                    continue;
                }

                if (entry.state == ESDynamicAtlasEntryState.Retired || entry.state == ESDynamicAtlasEntryState.Failed)
                    continue;

                entry.sourceLoadSerial++;
                entry.sourceHold?.Dispose();
                entry.sourceHold = null;
                entry.sourceHeld = false;
                FailEntry(entry, new OperationCanceledException("资源 Provider 正在切换，动态图集旧代上传已取消。"));
            }
        }

        public void HandleProviderRebuilt()
        {
            if (!disposed)
                acceptingRequests = true;
        }

        public void CloseDomain(ESDynamicAtlasDomainKey domain)
        {
            if (!domain.IsValid || disposed)
                return;

            domainGenerations.TryGetValue(domain, out int currentGeneration);
            domainGenerations[domain] = currentGeneration + 1;

            domainRemovalBuffer.Clear();
            List<Entry> domainEntries = new List<Entry>(entries.Values);
            for (int index = 0; index < domainEntries.Count; index++)
            {
                Entry entry = domainEntries[index];
                if (!entries.TryGetValue(entry.key, out Entry current)
                    || !ReferenceEquals(current, entry))
                    continue;
                if (!entry.key.domain.Equals(domain))
                    continue;

                entry.providerRetired = true;
                entry.resolveInvalidated = true;
                entry.sourceLoadSerial++;
                if (entry.state == ESDynamicAtlasEntryState.WaitingGpuFence)
                {
                    if (!entry.initialCompletionSettled)
                    {
                        entry.initialCompletionSettled = true;
                        entry.initialCompletion.TrySetException(
                            new OperationCanceledException($"动态图集 Domain {domain} 已关闭。"));
                    }
                    continue;
                }

                if (entry.state == ESDynamicAtlasEntryState.Quarantined)
                {
                    Notify(entry);
                    continue;
                }

                entry.sourceHold?.Dispose();
                entry.sourceHold = null;
                entry.sourceHeld = false;
                if (entry.state != ESDynamicAtlasEntryState.Ready
                    && entry.state != ESDynamicAtlasEntryState.Retired)
                {
                    FailEntry(entry, new OperationCanceledException($"动态图集 Domain {domain} 已关闭。"));
                }
                else
                {
                    entry.state = ESDynamicAtlasEntryState.Retired;
                    Notify(entry);
                }

                if (entry.refCount == 0)
                    domainRemovalBuffer.Add(entry);
            }

            for (int i = 0; i < domainRemovalBuffer.Count; i++)
                EvictEntry(domainRemovalBuffer[i]);
            policies.Remove(domain);
            RemoveEmptyPages(domain);
        }

        public ESDynamicAtlasSnapshot CreateSnapshot(int maxEntryDetails = int.MaxValue)
        {
            maxEntryDetails = Mathf.Max(0, maxEntryDetails);
            var snapshot = new ESDynamicAtlasSnapshot
            {
                acceptingRequests = IsAcceptingRequests,
                providerReady = ESAssets.IsReady,
                providerGeneration = ESAssets.RuntimeBackendGeneration,
                copyTextureCount = copyTextureCount,
                paddingShaderCount = paddingShaderCount,
                deferredFenceFallbackCount = deferredFenceFallbackCount,
                pendingFenceReleaseCount = deferredFenceReleaseCount,
                quarantineRetryCount = quarantineRetryCount,
                quarantineFailureCount = quarantineFailureCount,
                pageLostCount = pageLostCount
            };

            for (int i = 0; i < pages.Count; i++)
            {
                Page page = pages[i];
                int freePixels = page.allocator.FreePixels;
                float fragmentation = freePixels <= 0
                    ? 0f
                    : 1f - page.allocator.LargestFreeRectPixels / (float)freePixels;
                snapshot.pages.Add(new ESDynamicAtlasPageSnapshot(page.id, page.size,
                    page.allocator.UsedPixels, page.allocator.FreeRectCount, fragmentation, page.generation,
                    page.colorSpace, page.alphaMode));
                snapshot.estimatedGpuBytes += (long)page.size * page.size * 4L;
            }

            foreach (Entry entry in entries.Values)
            {
                snapshot.totalEntryCount++;
                ESDynamicAtlasEntryState diagnosticState = entry.pageLost
                    ? ESDynamicAtlasEntryState.Lost
                    : entry.state;
                switch (diagnosticState)
                {
                    case ESDynamicAtlasEntryState.Ready: snapshot.readyCount++; break;
                    case ESDynamicAtlasEntryState.Retired: snapshot.retiredCount++; break;
                    case ESDynamicAtlasEntryState.Failed: snapshot.failedCount++; break;
                    case ESDynamicAtlasEntryState.Lost: snapshot.lostCount++; snapshot.pendingCount++; break;
                    case ESDynamicAtlasEntryState.PendingSource:
                    case ESDynamicAtlasEntryState.QueuedUpload:
                        snapshot.pendingCount++;
                        break;
                    case ESDynamicAtlasEntryState.WaitingGpuFence:
                        snapshot.pendingCount++;
                        snapshot.waitingFenceCount++;
                        break;
                    case ESDynamicAtlasEntryState.Quarantined:
                        snapshot.pendingCount++;
                        break;
                }

                if (snapshot.entries.Count < maxEntryDetails)
                {
                    Rect uvRect = default;
                    Texture pageTexture = entry.page?.texture;
                    GraphicsFormat pageFormat = entry.page != null ? entry.page.GraphicsFormat : GraphicsFormat.None;
                    if (entry.page != null && entry.page.size > 0)
                    {
                        float inverse = 1f / entry.page.size;
                        uvRect = new Rect(entry.contentRect.x * inverse, entry.contentRect.y * inverse,
                            entry.contentRect.width * inverse, entry.contentRect.height * inverse);
                    }

                    snapshot.entries.Add(new ESDynamicAtlasEntrySnapshot(entry.key.domain, entry.key.content,
                        diagnosticState, entry.refCount, entry.key.providerGeneration, entry.page?.id ?? 0,
                        entry.PixelSize, entry.slotGeneration, entry.placementRevision, entry.sourceHeld,
                        pageTexture, uvRect, entry.uploadPath, entry.sourceGraphicsFormat, pageFormat,
                        entry.failureMessage));
                }
            }

            snapshot.omittedEntryCount = snapshot.totalEntryCount - snapshot.entries.Count;

            CalculatePercentiles(out snapshot.uploadP50Milliseconds,
                out snapshot.uploadP95Milliseconds, out snapshot.uploadP99Milliseconds);
            AppendQuarantineDiagnostics(snapshot);
            return snapshot;
        }

        internal static bool TryCreateShutdownQuarantineSnapshot(out ESDynamicAtlasSnapshot snapshot)
        {
            if (shutdownQuarantineDiagnostics.Count == 0 && shutdownQuarantineFoldedCount == 0)
            {
                snapshot = null;
                return false;
            }

            snapshot = new ESDynamicAtlasSnapshot();
            AppendShutdownQuarantineDiagnostics(snapshot);
            return true;
        }

        private void AppendQuarantineDiagnostics(ESDynamicAtlasSnapshot snapshot)
        {
            for (int i = 0; i < quarantinedUploads.Count; i++)
            {
                UploadJob job = quarantinedUploads[i];
                snapshot.quarantinedCount++;
                if (job.quarantineTerminal)
                    snapshot.quarantinedTerminalCount++;
                AddQuarantineDiagnostic(snapshot, job.page, job.completionFailure?.Message);
            }

            AppendShutdownQuarantineDiagnostics(snapshot);
        }

        private static void AppendShutdownQuarantineDiagnostics(ESDynamicAtlasSnapshot snapshot)
        {
            snapshot.shutdownQuarantineFoldedCount = shutdownQuarantineFoldedCount;
            for (int quarantineIndex = 0; quarantineIndex < shutdownQuarantineDiagnostics.Count; quarantineIndex++)
            {
                ShutdownQuarantineDiagnostic diagnostic = shutdownQuarantineDiagnostics[quarantineIndex];
                snapshot.shutdownQuarantinedCount += diagnostic.uploadCount;
                snapshot.quarantinedTerminalCount += diagnostic.uploadCount;
                AddQuarantineDiagnostic(snapshot, null, diagnostic.reason);
                AddQuarantineDiagnostic(snapshot, diagnostic.pageIds);
            }
        }

        private static void AddQuarantineDiagnostic(
            ESDynamicAtlasSnapshot snapshot,
            Page page,
            string reason)
        {
            if (page != null && page.id != 0 && !snapshot.quarantinedPageIds.Contains(page.id))
                snapshot.quarantinedPageIds.Add(page.id);
            if (!string.IsNullOrWhiteSpace(reason))
                snapshot.quarantineReasons.Add(reason);
        }

        private static void AddQuarantineDiagnostic(
            ESDynamicAtlasSnapshot snapshot,
            List<int> pageIds)
        {
            if (pageIds == null)
                return;

            for (int pageIndex = 0; pageIndex < pageIds.Count; pageIndex++)
            {
                int pageId = pageIds[pageIndex];
                if (pageId != 0 && !snapshot.quarantinedPageIds.Contains(pageId))
                    snapshot.quarantinedPageIds.Add(pageId);
            }
        }

        private static void RetainShutdownQuarantine(
            List<UploadJob> uploads,
            List<Page> pendingPages,
            Material pendingPaddingMaterial,
            Exception exception)
        {
            if (uploads == null || uploads.Count == 0)
                return;

            string reason = exception?.Message ?? "动态图集 Runtime 关闭时无法确认 GPU 完成状态。";
            shutdownQuarantines.Add(new ShutdownQuarantine(
                uploads,
                pendingPages,
                pendingPaddingMaterial,
                reason));
            var pageIds = new List<int>();
            if (pendingPages != null)
            {
                for (int pageIndex = 0; pageIndex < pendingPages.Count; pageIndex++)
                {
                    Page page = pendingPages[pageIndex];
                    if (page != null && page.id != 0)
                        pageIds.Add(page.id);
                }
            }
            shutdownQuarantineDiagnostics.Add(new ShutdownQuarantineDiagnostic(
                uploads.Count,
                pageIds,
                reason));
            if (shutdownQuarantineDiagnostics.Count > MaxShutdownQuarantineDiagnostics)
            {
                shutdownQuarantineDiagnostics.RemoveAt(0);
                shutdownQuarantineFoldedCount++;
            }
            Debug.LogError($"[ES动态图集] Runtime 已关闭，但仍有 {uploads.Count} 个 GPU 上传处于隔离状态；" +
                           "源 Texture Lease 和 Page 已保留到进程结束。原因：" + reason);
        }

        public bool TryResolve(long leaseToken, out ESDynamicAtlasResolved resolved)
        {
            if (!disposed && leases.TryGetValue(leaseToken, out LeaseRecord lease))
            {
                Entry entry = lease.entry;
                Page page = entry.page;
                if (lease.slotGeneration == entry.slotGeneration
                    && !entry.pageLost
                    && !entry.resolveInvalidated
                    && (entry.state == ESDynamicAtlasEntryState.Ready || entry.state == ESDynamicAtlasEntryState.Retired)
                    && page != null && page.texture != null && page.texture.IsCreated()
                    && !page.IsQuarantined)
                {
                    float inverse = 1f / page.size;
                    var uv = new Rect(entry.contentRect.x * inverse, entry.contentRect.y * inverse,
                        entry.contentRect.width * inverse, entry.contentRect.height * inverse);
                    resolved = new ESDynamicAtlasResolved(page.texture, uv, entry.PixelSize,
                        entry.slotGeneration, entry.placementRevision, page.generation, entry.key.request.alphaMode);
                    return true;
                }
            }

            resolved = default;
            return false;
        }

        public bool TryGetLeaseState(long leaseToken, out ESDynamicAtlasLeaseState state)
        {
            if (disposed || !leases.TryGetValue(leaseToken, out LeaseRecord lease))
            {
                state = ESDynamicAtlasLeaseState.Invalid;
                return false;
            }

            Entry entry = lease.entry;
            if (entry.page != null && entry.page.IsQuarantined)
            {
                state = ESDynamicAtlasLeaseState.Quarantined;
                return true;
            }
            if (lease.slotGeneration != entry.slotGeneration
                || entry.resolveInvalidated
                || entry.page == null
                || entry.page.texture == null
                || !entry.page.texture.IsCreated())
            {
                if (entry.state == ESDynamicAtlasEntryState.Quarantined)
                {
                    state = ESDynamicAtlasLeaseState.Quarantined;
                }
                else if (entry.source.CanReload
                         && (entry.pageRecoveryPending
                             || entry.state == ESDynamicAtlasEntryState.PendingSource
                             || entry.state == ESDynamicAtlasEntryState.QueuedUpload
                             || entry.state == ESDynamicAtlasEntryState.WaitingGpuFence))
                {
                    state = ESDynamicAtlasLeaseState.Recovering;
                }
                else if (entry.state == ESDynamicAtlasEntryState.Ready
                         || entry.state == ESDynamicAtlasEntryState.Retired)
                {
                    state = ESDynamicAtlasLeaseState.Lost;
                }
                else
                {
                    state = ESDynamicAtlasLeaseState.Failed;
                }
                return true;
            }

            switch (entry.state)
            {
                case ESDynamicAtlasEntryState.Ready:
                    state = ESDynamicAtlasLeaseState.Ready;
                    break;
                case ESDynamicAtlasEntryState.Retired:
                    state = ESDynamicAtlasLeaseState.Retired;
                    break;
                case ESDynamicAtlasEntryState.Quarantined:
                    state = ESDynamicAtlasLeaseState.Quarantined;
                    break;
                case ESDynamicAtlasEntryState.Failed:
                    state = ESDynamicAtlasLeaseState.Failed;
                    break;
                case ESDynamicAtlasEntryState.Lost:
                    state = ESDynamicAtlasLeaseState.Lost;
                    break;
                default:
                    state = ESDynamicAtlasLeaseState.Recovering;
                    break;
            }
            return true;
        }

        public void Release(long leaseToken)
        {
            if (leaseToken == 0 || !leases.TryGetValue(leaseToken, out LeaseRecord lease))
                return;

            leases.Remove(leaseToken);
            RemoveObservationsForLease(leaseToken);
            Entry entry = lease.entry;
            entry.refCount = Mathf.Max(0, entry.refCount - 1);
            if (entry.refCount != 0)
                return;

            entry.lastReleaseTime = Time.realtimeSinceStartup;
            if (entry.providerRetired || entry.state == ESDynamicAtlasEntryState.Failed)
                EvictEntry(entry);
        }

        public long Subscribe(long leaseToken, Action changed)
        {
            if (changed == null || !leases.TryGetValue(leaseToken, out LeaseRecord lease))
                return 0;

            long token = NextObservationToken();
            observations.Add(token, new ObservationRecord(lease.entry, leaseToken));
            lease.entry.observers[token] = changed;
            return token;
        }

        public void Unsubscribe(long observationToken)
        {
            if (observationToken == 0 || !observations.TryGetValue(observationToken, out ObservationRecord record))
                return;

            observations.Remove(observationToken);
            record.entry.observers.Remove(observationToken);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            acceptingRequests = false;
            // Completing a shared load can resume a caller synchronously. Use a
            // snapshot so those continuations cannot invalidate the dictionary
            // enumeration while shutdown is notifying them.
            List<Entry> disposeEntries = new List<Entry>(entries.Values);
            for (int index = 0; index < disposeEntries.Count; index++)
            {
                Entry entry = disposeEntries[index];
                entry.sourceLoadSerial++;
                entry.sourceHold?.Dispose();
                entry.sourceHold = null;
                entry.sourceHeld = false;
                if (!entry.initialCompletionSettled)
                {
                    entry.initialCompletionSettled = true;
                    entry.initialCompletion.TrySetException(new ObjectDisposedException(nameof(ESDynamicAtlasRuntime)));
                }
            }

            List<UploadJob> pendingUploads = inFlightUploads.Count == 0 && quarantinedUploads.Count == 0
                ? null
                : new List<UploadJob>(inFlightUploads.Count + quarantinedUploads.Count);
            if (pendingUploads != null)
            {
                pendingUploads.AddRange(inFlightUploads);
                pendingUploads.AddRange(quarantinedUploads);
            }
            List<Page> pendingPages = pages.Count == 0
                ? null
                : new List<Page>(pages);
            Material pendingPaddingMaterial = paddingMaterial;
            inFlightUploads.Clear();
            quarantinedUploads.Clear();
            pages.Clear();

            if (pendingUploads != null && pendingUploads.Count > 0)
            {
                deferredFenceReleaseCount += pendingUploads.Count;
                DisposeGpuResourcesAfterFencesAsync(
                    pendingUploads,
                    pendingPages,
                    pendingPaddingMaterial).Forget();
            }
            else
            {
                DisposePages(pendingPages);
                DestroyPaddingMaterial(pendingPaddingMaterial);
            }
            paddingMaterial = null;
            entries.Clear();
            uploadQueue.Clear();
            DisposeReusableUploadCommandBuffers();
            leases.Clear();
            observations.Clear();
            domainLeaseTokens.Clear();
            domainLeaseCounts.Clear();
            domainGenerations.Clear();
            recoveryRemovalBuffer.Clear();
            domainRemovalBuffer.Clear();
            evictionRemovalBuffer.Clear();
            evictionCandidatesBuffer.Clear();

        }

        private async UniTaskVoid DisposeGpuResourcesAfterFencesAsync(
            List<UploadJob> uploads,
            List<Page> pendingPages,
            Material pendingPaddingMaterial)
        {
            try
            {
                while (!AllUploadsCompleted(uploads, out Exception completionFailure))
                {
                    if (completionFailure != null)
                        throw completionFailure;
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
            catch (Exception exception)
            {
                // Releasing a source or page after an unknown GPU completion state is unsafe.
                // Retain them in the process-level diagnostic quarantine instead of losing
                // ownership after this Runtime has been disposed.
                RetainShutdownQuarantine(uploads, pendingPages, pendingPaddingMaterial, exception);
                return;
            }

            for (int i = 0; i < uploads.Count; i++)
            {
                UploadJob job = uploads[i];
                job.sourceHold?.Dispose();
                job.sourceHold = null;
                if (job.entry != null)
                    job.entry.sourceHeld = false;
            }

            DisposePages(pendingPages);
            DestroyPaddingMaterial(pendingPaddingMaterial);
            deferredFenceReleaseCount = Mathf.Max(0, deferredFenceReleaseCount - uploads.Count);
        }

        private bool AllUploadsCompleted(List<UploadJob> uploads, out Exception completionFailure)
        {
            completionFailure = null;
            if (uploads == null)
                return true;

            for (int i = 0; i < uploads.Count; i++)
            {
                UploadJob job = uploads[i];
                if (job != null && job.quarantined)
                {
                    completionFailure = job.completionFailure ?? new InvalidOperationException(
                        "动态图集 Runtime 关闭时仍有 GPU 完成状态未知的隔离上传。 ");
                    return false;
                }
                UploadCompletionState state = GetUploadCompletionState(job, out completionFailure);
                if (state == UploadCompletionState.Completed)
                    continue;

                return false;
            }

            return true;
        }

        private static void DisposePages(List<Page> pendingPages)
        {
            if (pendingPages == null)
                return;

            for (int i = 0; i < pendingPages.Count; i++)
                pendingPages[i]?.Dispose();
        }

        private static void DestroyPaddingMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);
        }

        private void EnsureCanAcquire(
            ESDynamicAtlasDomainKey domain,
            ESDynamicAtlasContentKey content,
            bool requiresProvider)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ESDynamicAtlasRuntime));
            if (!domain.IsValid)
                throw new ArgumentException("动态图集 Domain Key 不能为空。", nameof(domain));
            if (!content.IsValid)
                throw new ArgumentException("动态图集 Content Key 不能为空。", nameof(content));
            if (requiresProvider ? !IsAcceptingRequests : !IsAcceptingDirectRequests)
                throw new InvalidOperationException("资源 Provider 尚未就绪或正在切换，动态图集暂不接受新请求。");
        }

        private ESDynamicAtlasLease CreateLease(Entry entry)
        {
            long token = NextLeaseToken();
            leases.Add(token, new LeaseRecord(entry));
            entry.refCount++;
            return new ESDynamicAtlasLease(this, token);
        }

        private void BeginSourceLoad(Entry entry)
        {
            if (disposed || entry == null || entry.providerRetired || !entry.source.CanReload)
            {
                FailEntry(entry, new InvalidOperationException("动态图集条目没有可重新加载的稳定 Source。"));
                return;
            }

            entry.state = ESDynamicAtlasEntryState.PendingSource;
            if (entry.initialCompletionSettled)
                entry.resolveInvalidated = true;
            int serial = ++entry.sourceLoadSerial;
            LoadSourceAsync(entry, serial).Forget();
        }

        private async UniTaskVoid LoadSourceAsync(Entry entry, int serial)
        {
            SourceHold hold = null;
            try
            {
                if (entry.source.IsResourceBacked)
                {
                    ESAssetTemporaryLease<Texture2D> lease = await ESAssets.LoadTemporaryAsync(entry.source.refer, CancellationToken.None);
                    hold = new SourceHold
                    {
                        texture = lease.Asset,
                        temporaryLease = lease,
                        ownsTemporaryLease = true
                    };
                }
                else
                {
                    hold = new SourceHold { texture = entry.source.directTexture };
                }

                await UniTask.SwitchToMainThread();
                if (disposed || entry.sourceLoadSerial != serial || entry.providerRetired
                    || (entry.source.IsResourceBacked && entry.key.providerGeneration != ESAssets.RuntimeBackendGeneration))
                {
                    hold.Dispose();
                    return;
                }

                if (hold.texture == null)
                    throw new InvalidOperationException($"动态图集 Source {entry.key.content} 加载结果为空。 ");

                ValidateSourceTexture(entry, hold.texture);

                entry.sourceGraphicsFormat = hold.texture.graphicsFormat;

                if (entry.page == null && !TryAllocate(entry, hold.texture.width, hold.texture.height))
                    throw new InvalidOperationException($"动态图集没有足够页面容纳 {entry.key.content} ({hold.texture.width}x{hold.texture.height})。 ");

                entry.sourceHold?.Dispose();
                entry.sourceHold = hold;
                entry.sourceHeld = true;
                hold = null;
                entry.state = ESDynamicAtlasEntryState.QueuedUpload;
                uploadQueue.Enqueue(entry);
            }
            catch (Exception exception)
            {
                hold?.Dispose();
                await UniTask.SwitchToMainThread();
                if (!disposed && entry.sourceLoadSerial == serial)
                    FailEntry(entry, exception);
            }
        }

        private void ValidateSourceTexture(Entry entry, Texture source)
        {
            if (source.dimension != TextureDimension.Tex2D)
                throw new InvalidOperationException($"动态图集 Source {entry.key.content} 不是二维纹理。 ");
            if (source.width <= 0 || source.height <= 0)
                throw new InvalidOperationException($"动态图集 Source {entry.key.content} 尺寸无效。 ");

            ESDynamicAtlasDomainPolicy policy = GetPolicy(entry.key.domain);
            int padding = entry.key.request.padding;
            if (source.width + padding * 2 > policy.pageSize
                || source.height + padding * 2 > policy.pageSize)
            {
                throw new InvalidOperationException(
                    $"动态图集 Source {entry.key.content} ({source.width}x{source.height}) 加留白后超过页面上限 {policy.pageSize}。 ");
            }
            if (source.graphicsFormat == GraphicsFormat.None)
                throw new InvalidOperationException($"动态图集 Source {entry.key.content} 没有可识别的 GraphicsFormat。 ");
        }

        private bool TryAllocate(Entry entry, int width, int height)
        {
            int padding = entry.key.request.padding;
            int allocatedWidth = width + padding * 2;
            int allocatedHeight = height + padding * 2;
            ESDynamicAtlasDomainPolicy policy = GetPolicy(entry.key.domain);
            if (allocatedWidth > policy.pageSize || allocatedHeight > policy.pageSize)
                return false;

            for (int i = 0; i < pages.Count; i++)
            {
                Page page = pages[i];
                if (page.IsQuarantined
                    || !page.Matches(entry.key.domain, entry.key.request)
                    || !page.allocator.TryAllocate(allocatedWidth, allocatedHeight, out RectInt allocated))
                {
                    continue;
                }

                SetPlacement(entry, page, allocated, width, height, padding);
                return true;
            }

            int pageCount = CountDomainPages(entry.key.domain);
            long pageBytes = (long)policy.pageSize * policy.pageSize * 4L;
            bool pageLimitReached = pageCount >= policy.maxPages;
            bool memoryLimitReached = pageBytes > 0
                && ((long)pageCount + 1L) * pageBytes > policy.maxGpuBytes;
            if (pageLimitReached || memoryLimitReached)
            {
                if (TryAllocateAfterCacheEviction(entry, allocatedWidth, allocatedHeight,
                        width, height, padding))
                {
                    return true;
                }
                pageCount = CountDomainPages(entry.key.domain);
                pageLimitReached = pageCount >= policy.maxPages;
                memoryLimitReached = pageBytes > 0
                    && ((long)pageCount + 1L) * pageBytes > policy.maxGpuBytes;
                if (pageLimitReached || memoryLimitReached)
                    return false;
            }

            Page created = CreatePage(entry.key.domain, entry.key.request, policy.pageSize);
            if (!created.allocator.TryAllocate(allocatedWidth, allocatedHeight, out RectInt result))
                return false;

            SetPlacement(entry, created, result, width, height, padding);
            return true;
        }

        private bool TryAllocateAfterCacheEviction(Entry target, int allocatedWidth, int allocatedHeight,
            int contentWidth, int contentHeight, int padding)
        {
            evictionCandidatesBuffer.Clear();
            foreach (Entry entry in entries.Values)
            {
                if (ReferenceEquals(entry, target) || entry.refCount != 0
                    || !entry.key.domain.Equals(target.key.domain)
                    || (entry.state != ESDynamicAtlasEntryState.Ready
                        && entry.state != ESDynamicAtlasEntryState.Retired))
                {
                    continue;
                }
                evictionCandidatesBuffer.Add(entry);
            }

            evictionCandidatesBuffer.Sort((left, right) => left.lastReleaseTime.CompareTo(right.lastReleaseTime));
            for (int i = 0; i < evictionCandidatesBuffer.Count; i++)
            {
                EvictEntry(evictionCandidatesBuffer[i]);
                for (int pageIndex = 0; pageIndex < pages.Count; pageIndex++)
                {
                    Page page = pages[pageIndex];
                    if (page.IsQuarantined
                        || !page.Matches(target.key.domain, target.key.request)
                        || !page.allocator.TryAllocate(allocatedWidth, allocatedHeight, out RectInt allocated))
                    {
                        continue;
                    }

                    SetPlacement(target, page, allocated, contentWidth, contentHeight, padding);
                    return true;
                }
            }

            RemoveEmptyPages(target.key.domain);
            return false;
        }

        private void SetPlacement(Entry entry, Page page, RectInt allocated, int width, int height, int padding)
        {
            entry.page = page;
            entry.allocatedRect = allocated;
            entry.contentRect = new RectInt(allocated.x + padding, allocated.y + padding, width, height);
            if (entry.slotGeneration == 0)
            {
                nextSlotGeneration++;
                if (nextSlotGeneration == 0)
                    nextSlotGeneration++;
                entry.slotGeneration = nextSlotGeneration;
            }
            entry.resolveInvalidated = false;
            entry.placementReleasePending = false;
            entry.placementRevision++;
            Notify(entry);
        }

        private Page CreatePage(ESDynamicAtlasDomainKey domain, in ESDynamicAtlasRequest request, int size)
        {
            var page = new Page
            {
                id = ++nextPageId,
                domain = domain,
                colorSpace = request.colorSpace,
                alphaMode = request.alphaMode,
                filterMode = request.filterMode,
                size = size,
                allocator = new ESDynamicAtlasAllocator(size, size)
            };
            page.CreateTexture();
            pages.Add(page);
            return page;
        }

        private void StartBudgetedUploads()
        {
            if (uploadQueue.Count == 0)
                return;

            frameUploadCounts.Clear();
            frameUploadPixels.Clear();
            int safety = uploadQueue.Count;
            while (uploadQueue.Count > 0 && safety-- > 0)
            {
                Entry entry = uploadQueue.Dequeue();
                if (entry.state != ESDynamicAtlasEntryState.QueuedUpload)
                    continue;

                if (entry.sourceHold?.texture == null)
                {
                    string sourceKind = entry.source.IsResourceBacked ? "资源加载结果" : "调用方提供的 Texture";
                    FailEntry(entry, new InvalidOperationException(
                        "动态图集上传开始前 " + sourceKind + " 已失效；请求不会继续等待或交付 Lease。"));
                    continue;
                }

                if (entry.page != null && (entry.page.recoveryPending || entry.page.IsQuarantined))
                {
                    uploadQueue.Enqueue(entry);
                    continue;
                }

                ESDynamicAtlasDomainPolicy policy = GetPolicy(entry.key.domain);
                int uploadPixels = entry.allocatedRect.width * entry.allocatedRect.height;
                frameUploadCounts.TryGetValue(entry.key.domain, out int domainCount);
                frameUploadPixels.TryGetValue(entry.key.domain, out int domainPixels);
                if (domainCount >= policy.maxUploadsPerFrame
                    || (domainCount > 0 && domainPixels + uploadPixels > policy.maxUploadPixelsPerFrame))
                {
                    uploadQueue.Enqueue(entry);
                    continue;
                }

                try
                {
                    StartUpload(entry);
                    frameUploadCounts[entry.key.domain] = domainCount + 1;
                    frameUploadPixels[entry.key.domain] = domainPixels + uploadPixels;
                }
                catch (Exception exception)
                {
                    FailEntry(entry, exception);
                }
            }
        }

        private void StartUpload(Entry entry)
        {
            using (UploadMarker.Auto())
            {
                Page page = entry.page ?? throw new InvalidOperationException("动态图集上传没有目标页面。 ");
                if (page.IsQuarantined)
                    throw new InvalidOperationException("动态图集 Page 正处于 GPU 完成隔离，不能开始新上传。 ");
                if (page.texture == null || !page.texture.IsCreated())
                {
                    if (page.recoveryPending)
                        throw new InvalidOperationException("动态图集 Page 正在等待前一代 GPU Fence，暂不能开始新上传。 ");
                    page.CreateTexture();
                }

                bool canPollFence = SystemInfo.supportsGraphicsFence && !graphicsFencePollingUnavailable;
                if (!canPollFence && !SystemInfo.supportsAsyncGPUReadback)
                {
                    throw new InvalidOperationException(
                        "当前图形后端既不能轮询 GraphicsFence，也不支持 AsyncGPUReadback，已拒绝上传以保护源 Texture Lease。 ");
                }

                SourceHold uploadHold = entry.sourceHold;
                Texture source = uploadHold.texture;
                CommandBuffer command = GetUploadCommandBuffer($"ES Dynamic Atlas Upload {entry.key.content}");
                try
                {
                    ESDynamicAtlasUploadPath path;
                    if (CanUseCopyTexture(source, page, entry))
                    {
                        command.CopyTexture(source, 0, 0, 0, 0, source.width, source.height,
                            page.texture, 0, 0, entry.contentRect.x, entry.contentRect.y);
                        path = ESDynamicAtlasUploadPath.CopyTexture;
                        copyTextureCount++;
                    }
                    else
                    {
                        Material material = GetPaddingMaterial();
                        paddingProperties ??= new MaterialPropertyBlock();
                        paddingProperties.Clear();
                        MaterialPropertyBlock properties = paddingProperties;
                        properties.SetTexture("_MainTex", source);
                        properties.SetVector("_ESAtlasCopyData", new Vector4(
                            entry.key.request.padding,
                            entry.key.request.padding,
                            entry.contentRect.width,
                            entry.contentRect.height));
                        properties.SetFloat("_ESAtlasPremultiply",
                            entry.key.request.alphaMode == ESDynamicAtlasAlphaMode.Premultiplied ? 1f : 0f);
                        command.SetRenderTarget(page.texture);
                        command.SetViewport(new Rect(entry.allocatedRect.x, entry.allocatedRect.y,
                            entry.allocatedRect.width, entry.allocatedRect.height));
                        command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, properties);
                        path = ESDynamicAtlasUploadPath.PaddingShader;
                        paddingShaderCount++;
                    }

                    GraphicsFence fence = default;
                    bool hasFence = false;
                    Exception fenceCreationFailure = null;
                    if (canPollFence)
                    {
                        try
                        {
                            // A fixed frame delay is never a source Lease boundary. Fence the
                            // complete command buffer so both CopyTexture and the padding draw
                            // are covered when this backend allows polling GraphicsFence.passed.
                            fence = command.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation,
                                SynchronisationStageFlags.AllGPUOperations);
                            hasFence = true;
                        }
                        catch (Exception exception)
                        {
                            fenceCreationFailure = exception;
                        }
                    }

                    if (!hasFence && !SystemInfo.supportsAsyncGPUReadback)
                    {
                        throw new InvalidOperationException(
                            "当前图形后端无法建立可轮询的 GraphicsFence，且不支持 AsyncGPUReadback，已拒绝上传以保护源 Texture Lease。",
                            fenceCreationFailure);
                    }

                    try
                    {
#if UNITY_EDITOR
                        if (ForceUnknownGpuSubmissionForTests)
                        {
                            throw new InvalidOperationException(
                                "测试注入：无法确认动态图集上传命令是否已提交到 GPU。 ");
                        }
#endif

                        Graphics.ExecuteCommandBuffer(command);
                    }
                    catch (Exception exception)
                    {
                        PreserveUnknownSubmission(entry, page, uploadHold, exception);
                        return;
                    }
                    finally
                    {
                        if (command != null)
                        {
                            ReturnUploadCommandBuffer(command);
                            command = null;
                        }
                    }

                    entry.sourceHold = null;
                    entry.state = ESDynamicAtlasEntryState.WaitingGpuFence;
                    page.inFlightUploadCount++;
                    entry.uploadPath = path;
                    var job = new UploadJob
                    {
                        entry = entry,
                        page = page,
                        sourceHold = uploadHold,
                        fence = fence,
                        hasFence = hasFence,
                        startedAt = Time.realtimeSinceStartup
                    };
                    inFlightUploads.Add(job);

                    if (!hasFence && !TryStartAsyncGpuReadbackFallback(job, fenceCreationFailure, out Exception failure))
                        QuarantineUpload(job, failure);
                }
                finally
                {
                    if (command != null)
                        ReturnUploadCommandBuffer(command);
                }
            }
        }

        private CommandBuffer GetUploadCommandBuffer(string name)
        {
            CommandBuffer command = reusableUploadCommandBuffers.Count > 0
                ? reusableUploadCommandBuffers.Pop()
                : new CommandBuffer();
            command.Clear();
            command.name = name;
            return command;
        }

        private void ReturnUploadCommandBuffer(CommandBuffer command)
        {
            if (command == null)
                return;

            command.Clear();
            if (disposed)
            {
                command.Dispose();
                return;
            }

            reusableUploadCommandBuffers.Push(command);
        }

        private void DisposeReusableUploadCommandBuffers()
        {
            while (reusableUploadCommandBuffers.Count > 0)
                reusableUploadCommandBuffers.Pop().Dispose();
        }

        private void PreserveUnknownSubmission(
            Entry entry,
            Page page,
            SourceHold uploadHold,
            Exception submissionFailure)
        {
            // ExecuteCommandBuffer can fail after the native side has accepted some
            // work. Do not infer that the source is safe to release from the thrown
            // managed exception. A fresh target-page readback is the only fallback
            // completion token we can trust for this branch.
            entry.sourceHold = null;
            entry.sourceHeld = uploadHold != null;
            page.inFlightUploadCount++;
            var job = new UploadJob
            {
                entry = entry,
                page = page,
                sourceHold = uploadHold,
                startedAt = Time.realtimeSinceStartup
            };
            inFlightUploads.Add(job);

            FailEntry(entry, new InvalidOperationException(
                "动态图集上传命令提交状态未知；为保护源 Texture Lease，本次请求不会交付 Lease。", submissionFailure),
                preserveInFlightSourceHold: true);

            if (!TryStartAsyncGpuReadbackFallback(job, submissionFailure, out Exception completionFailure))
                QuarantineUpload(job, completionFailure);
        }

        private static bool CanUseCopyTexture(Texture source, Page page, Entry entry)
        {
            CopyTextureSupport support = SystemInfo.copyTextureSupport;
            bool sourceIsRenderTexture = source is RenderTexture;
            bool supportsCopyToPage = sourceIsRenderTexture
                ? (support & CopyTextureSupport.Basic) != 0
                : (support & (CopyTextureSupport.TextureToRT | CopyTextureSupport.DifferentTypes)) != 0;
            return entry.key.request.padding == 0
                   && entry.key.request.alphaMode == ESDynamicAtlasAlphaMode.Straight
                   && supportsCopyToPage
                   && source != null
                   && source.graphicsFormat == page.GraphicsFormat;
        }

        private Material GetPaddingMaterial()
        {
            if (paddingMaterial != null)
                return paddingMaterial;

            Shader shader = Shader.Find("Hidden/ES/DynamicAtlasCopyPadding");
            if (shader == null)
                throw new InvalidOperationException("缺少 Shader Hidden/ES/DynamicAtlasCopyPadding，无法执行 GPU Padding 上传。 ");

            paddingMaterial = new Material(shader)
            {
                name = "ES Dynamic Atlas Copy Padding",
                hideFlags = HideFlags.HideAndDontSave
            };
            return paddingMaterial;
        }

        private UploadCompletionState GetUploadCompletionState(UploadJob job, out Exception completionFailure)
        {
            completionFailure = null;
            if (job == null)
                return UploadCompletionState.Completed;
            if (job.completionFailure != null && !job.quarantined)
            {
                completionFailure = job.completionFailure;
                return UploadCompletionState.Failed;
            }

            if (job.hasReadbackRequest)
            {
                try
                {
                    if (!job.readbackRequest.done)
                        return UploadCompletionState.Pending;
                    if (!job.readbackRequest.hasError)
                        return UploadCompletionState.Completed;

                    completionFailure = new InvalidOperationException(
                        "动态图集 GPU 完成回退的 AsyncGPUReadback 返回错误；为保护源 Texture Lease，条目不会提前释放。 ");
                }
                catch (Exception exception)
                {
                    completionFailure = new InvalidOperationException(
                        "动态图集 GPU 完成回退状态无法读取；为保护源 Texture Lease，条目不会提前释放。", exception);
                }

                job.completionFailure = completionFailure;
                return UploadCompletionState.Failed;
            }

            if (job.hasFence)
            {
                try
                {
                    return job.fence.passed
                        ? UploadCompletionState.Completed
                        : UploadCompletionState.Pending;
                }
                catch (Exception exception)
                {
                    // Some DX11 configurations report supportsGraphicsFence but reject
                    // GraphicsFence.passed because AsyncQueueSynchronisation is unavailable.
                    // Cache that capability fact and use a target-page readback completion token.
                    graphicsFencePollingUnavailable = true;
                    job.hasFence = false;
                    if (!TryStartAsyncGpuReadbackFallback(job, exception, out completionFailure))
                    {
                        job.completionFailure = completionFailure;
                        return UploadCompletionState.Failed;
                    }

                    return GetUploadCompletionState(job, out completionFailure);
                }
            }

            if (!TryStartAsyncGpuReadbackFallback(job, null, out completionFailure))
            {
                job.completionFailure = completionFailure;
                return UploadCompletionState.Failed;
            }

            return GetUploadCompletionState(job, out completionFailure);
        }

        private bool TryStartAsyncGpuReadbackFallback(
            UploadJob job,
            Exception fenceFailure,
            out Exception completionFailure)
        {
            completionFailure = null;
            if (job == null)
            {
                completionFailure = new InvalidOperationException("动态图集缺少待完成的上传任务。 ");
                return false;
            }
            if (job.hasReadbackRequest)
                return true;
            if (!SystemInfo.supportsAsyncGPUReadback)
            {
                completionFailure = new InvalidOperationException(
                    "当前图形后端无法轮询 GraphicsFence，且不支持 AsyncGPUReadback，不能安全释放源 Texture Lease。",
                    fenceFailure);
                return false;
            }

            RenderTexture pageTexture = job.page?.texture;
            Entry entry = job.entry;
            if (pageTexture == null || !pageTexture.IsCreated() || entry == null
                || entry.contentRect.width <= 0 || entry.contentRect.height <= 0)
            {
                completionFailure = new InvalidOperationException(
                    "动态图集缺少可读取的 Page 子区域，不能建立安全的 GPU 完成回退。",
                    fenceFailure);
                return false;
            }

            try
            {
#if UNITY_EDITOR
                if (ForceAsyncGpuReadbackFailureForTests)
                {
                    completionFailure = new InvalidOperationException(
                        "测试注入：无法创建动态图集 AsyncGPUReadback 完成令牌。", fenceFailure);
                    return false;
                }
#endif

                // The request is issued after the upload command buffer. One target-page
                // pixel is sufficient as a GPU queue completion token; the uploaded image
                // is never read as CPU data and no full-image readback is requested.
                job.readbackRequest = AsyncGPUReadback.Request(pageTexture, 0,
                    entry.contentRect.x, 1,
                    entry.contentRect.y, 1,
                    0, 1, null);
                job.hasReadbackRequest = true;
                job.hasFence = false;
                entry.uploadPath = ESDynamicAtlasUploadPath.DeferredFenceFallback;
                if (!job.quarantined)
                    deferredFenceFallbackCount++;
                return true;
            }
            catch (Exception exception)
            {
                completionFailure = new InvalidOperationException(
                    "动态图集无法创建 AsyncGPUReadback 完成回退，不能安全释放源 Texture Lease。", exception);
                return false;
            }
        }

        private static void ClearReadbackCompletionToken(UploadJob job)
        {
            if (job == null)
                return;

            job.hasReadbackRequest = false;
            job.readbackRequest = default;
            job.hasFence = false;
        }

        private UploadCompletionState GetQuarantineProbeCompletionState(
            UploadJob job,
            out Exception completionFailure)
        {
            completionFailure = null;
            if (job == null || !job.hasReadbackRequest)
            {
                completionFailure = new InvalidOperationException(
                    "动态图集隔离上传缺少 AsyncGPUReadback 探针。 ");
                return UploadCompletionState.Failed;
            }

            try
            {
                if (!job.readbackRequest.done)
                    return UploadCompletionState.Pending;
                if (!job.readbackRequest.hasError)
                    return UploadCompletionState.Completed;

                completionFailure = new InvalidOperationException(
                    "动态图集隔离上传的 AsyncGPUReadback 探针返回错误；继续保留 Source Lease 与 Page。 ");
                return UploadCompletionState.Failed;
            }
            catch (Exception exception)
            {
                completionFailure = new InvalidOperationException(
                    "动态图集隔离上传的 AsyncGPUReadback 探针状态无法读取；继续保留 Source Lease 与 Page。",
                    exception);
                return UploadCompletionState.Failed;
            }
        }

        private void QuarantineUpload(UploadJob job, Exception completionFailure)
        {
            if (job == null || job.quarantined)
                return;

            job.completionFailure ??= completionFailure ?? new InvalidOperationException(
                "动态图集上传无法确认 GPU 完成状态。 ");
            bool removedFromInFlight = inFlightUploads.Remove(job);
            job.quarantined = true;
            job.lastProbeTime = float.NegativeInfinity;
            ClearReadbackCompletionToken(job);
            bool pageEnteredQuarantine = false;
            if (job.page != null)
            {
                pageEnteredQuarantine = job.page.quarantinedUploadCount == 0;
                if (removedFromInFlight)
                    job.page.inFlightUploadCount = Mathf.Max(0, job.page.inFlightUploadCount - 1);
                job.page.quarantinedUploadCount++;
            }
            quarantinedUploads.Add(job);

            Entry entry = job.entry;
            if (entry != null)
            {
                entry.sourceHeld = job.sourceHold != null;
                entry.resolveInvalidated = true;
                entry.state = ESDynamicAtlasEntryState.Quarantined;
                entry.failureMessage = "动态图集 GPU 完成状态未知，上传已隔离；Source Lease 与目标 Page 会保留至安全探针确认。原因："
                    + job.completionFailure.Message;
                if (!entry.initialCompletionSettled)
                {
                    entry.initialCompletionSettled = true;
                    entry.initialCompletion.TrySetException(new InvalidOperationException(
                        "动态图集上传 GPU 完成状态未知，当前请求不会交付 Lease。", job.completionFailure));
                }
            }

            if (pageEnteredQuarantine)
                NotifyPageEntries(job.page);
            else if (entry != null)
                Notify(entry);
        }

        private void ProbeQuarantinedUploads()
        {
            if (quarantinedUploads.Count == 0)
                return;

            float now = Time.realtimeSinceStartup;
            for (int i = quarantinedUploads.Count - 1; i >= 0; i--)
            {
                UploadJob job = quarantinedUploads[i];
                if (job == null)
                {
                    quarantinedUploads.RemoveAt(i);
                    continue;
                }
                if (job.quarantineTerminal)
                    continue;

                if (job.hasReadbackRequest)
                {
                    UploadCompletionState completion = GetQuarantineProbeCompletionState(job, out Exception probeFailure);
                    if (completion == UploadCompletionState.Pending)
                        continue;
                    if (completion == UploadCompletionState.Completed)
                    {
                        CompleteQuarantinedUpload(i);
                        continue;
                    }

                    ClearReadbackCompletionToken(job);
                    RecordQuarantineProbeFailure(job, probeFailure, now);
                    continue;
                }

                if (now - job.lastProbeTime < QuarantineProbeIntervalSeconds)
                    continue;

                job.lastProbeTime = now;
                quarantineRetryCount++;
                if (!TryStartAsyncGpuReadbackFallback(job, job.completionFailure, out Exception requestFailure))
                {
                    RecordQuarantineProbeFailure(job, requestFailure, now);
                    continue;
                }

                UploadCompletionState immediate = GetQuarantineProbeCompletionState(job, out Exception immediateFailure);
                if (immediate == UploadCompletionState.Completed)
                {
                    CompleteQuarantinedUpload(i);
                }
                else if (immediate == UploadCompletionState.Failed)
                {
                    ClearReadbackCompletionToken(job);
                    RecordQuarantineProbeFailure(job, immediateFailure, now);
                }
            }
        }

        private void RecordQuarantineProbeFailure(UploadJob job, Exception failure, float now)
        {
            if (job == null)
                return;

            job.quarantineFailureCount++;
            job.lastProbeTime = now;
            quarantineFailureCount++;
            if (job.quarantineFailureCount < MaxQuarantineProbeFailures)
                return;

            job.quarantineTerminal = true;
            Entry entry = job.entry;
            string reason = failure?.Message ?? job.completionFailure?.Message ?? "未返回详细错误。";
            if (entry != null)
            {
                entry.failureMessage = "动态图集 GPU 完成状态连续探针失败，已进入终态隔离；"
                    + "该 Page 不会接收新上传，Source Lease 与原生对象会保留到进程结束。最后原因：" + reason;
                Notify(entry);
            }

            Debug.LogError("[ES动态图集] GPU 完成状态终态隔离：Page " + (job.page?.id ?? 0)
                + "，连续探针失败 " + job.quarantineFailureCount + " 次。原因：" + reason);
        }

        private void CompleteQuarantinedUpload(int index)
        {
            if (index < 0 || index >= quarantinedUploads.Count)
                return;

            UploadJob job = quarantinedUploads[index];
            quarantinedUploads.RemoveAt(index);
            job.quarantined = false;
            ClearReadbackCompletionToken(job);
            bool pageExitedQuarantine = job.page != null && job.page.quarantinedUploadCount == 1;
            if (job.page != null)
                job.page.quarantinedUploadCount = Mathf.Max(0, job.page.quarantinedUploadCount - 1);

            job.sourceHold?.Dispose();
            job.sourceHold = null;
            Entry entry = job.entry;
            if (entry != null)
            {
                entry.sourceHeld = false;
                if (entry.state == ESDynamicAtlasEntryState.Quarantined)
                {
                    entry.state = ESDynamicAtlasEntryState.Failed;
                    entry.placementReleasePending = true;
                    entry.failureMessage = "动态图集隔离探针已确认 GPU 不再使用 Source；原上传不会交付 Lease。原因："
                        + (job.completionFailure?.Message ?? "未知完成状态。");
                    if (!pageExitedQuarantine)
                        Notify(entry);
                    if (entry.refCount == 0)
                        EvictEntry(entry);
                }
            }

            if (pageExitedQuarantine)
                NotifyPageEntries(job.page);

            if (job.page != null && !job.page.HasUnsafeGpuUse)
                ReleasePendingPlacements(job.page);

            if (job.page != null && job.page.recoveryPending && !job.page.HasUnsafeGpuUse)
                RecoverLostPage(job.page);
        }

        private void CompleteFinishedUploads()
        {
            for (int i = inFlightUploads.Count - 1; i >= 0; i--)
            {
                UploadJob job = inFlightUploads[i];
                UploadCompletionState completion = GetUploadCompletionState(job, out Exception completionFailure);
                if (completion == UploadCompletionState.Pending)
                    continue;

                if (completion == UploadCompletionState.Failed)
                {
                    QuarantineUpload(job, completionFailure);
                    continue;
                }

                inFlightUploads.RemoveAt(i);
                Entry entry = job.entry;
                if (job.page != null)
                    job.page.inFlightUploadCount = Mathf.Max(0, job.page.inFlightUploadCount - 1);
                job.sourceHold?.Dispose();
                job.sourceHold = null;
                if (entry != null)
                    entry.sourceHeld = false;

                if (job.page != null && job.page.recoveryPending
                    && !job.page.HasUnsafeGpuUse)
                {
                    RecoverLostPage(job.page);
                }

                if (disposed || entry == null || entry.state != ESDynamicAtlasEntryState.WaitingGpuFence)
                    continue;

                if (entry.providerRetired)
                {
                    entry.state = ESDynamicAtlasEntryState.Retired;
                    Notify(entry);
                    if (entry.refCount == 0)
                        EvictEntry(entry);
                    continue;
                }

                if (entry.pageRecoveryPending)
                {
                    // RecoverLostPage() handles all entries on the page after the
                    // final in-flight fence. Until then, keep this entry pending.
                    entry.state = ESDynamicAtlasEntryState.PendingSource;
                    continue;
                }

                if (!entry.source.IsResourceBacked)
                    entry.source.directTexture = null;
                entry.state = ESDynamicAtlasEntryState.Ready;
                entry.pageLost = false;
                entry.resolveInvalidated = false;
                if (entry.refCount == 0)
                    entry.lastReleaseTime = Time.realtimeSinceStartup;
                AddUploadSample((Time.realtimeSinceStartup - job.startedAt) * 1000f);
                if (!entry.initialCompletionSettled)
                {
                    entry.initialCompletionSettled = true;
                    entry.initialCompletion.TrySetResult(true);
                }
                Notify(entry);
            }
        }

        private void DetectLostPages()
        {
            for (int i = 0; i < pages.Count; i++)
            {
                Page page = pages[i];
                if (page.texture != null && page.texture.IsCreated())
                    continue;
                if (page.recoveryPending)
                    continue;

                pageLostCount++;
                page.recoveryPending = page.HasUnsafeGpuUse;
                List<Entry> pageEntries = new List<Entry>(entries.Values);
                for (int entryIndex = 0; entryIndex < pageEntries.Count; entryIndex++)
                {
                    Entry entry = pageEntries[entryIndex];
                    if (!entries.TryGetValue(entry.key, out Entry current)
                        || !ReferenceEquals(current, entry))
                        continue;
                    if (!ReferenceEquals(entry.page, page))
                        continue;

                    entry.pageLost = true;
                    entry.resolveInvalidated = true;

                    if (entry.state == ESDynamicAtlasEntryState.PendingSource
                        || entry.state == ESDynamicAtlasEntryState.QueuedUpload
                        || entry.state == ESDynamicAtlasEntryState.Failed)
                    {
                        Notify(entry);
                        continue;
                    }
                    if (entry.state == ESDynamicAtlasEntryState.WaitingGpuFence)
                    {
                        entry.pageRecoveryPending = true;
                        Notify(entry);
                        continue;
                    }
                    if (entry.state == ESDynamicAtlasEntryState.Quarantined)
                    {
                        entry.pageRecoveryPending = true;
                        Notify(entry);
                        continue;
                    }
                    if (entry.providerRetired || !entry.source.CanReload)
                    {
                        entry.state = ESDynamicAtlasEntryState.Failed;
                        entry.placementReleasePending = true;
                        Notify(entry);
                        continue;
                    }
                    entry.pageRecoveryPending = true;
                    Notify(entry);
                }

                if (!page.recoveryPending)
                {
                    ReleasePendingPlacements(page);
                    RecoverLostPage(page);
                }
            }
        }

        private void ReleasePendingPlacements(Page page)
        {
            if (page == null || page.HasUnsafeGpuUse)
                return;

            List<Entry> pageEntries = new List<Entry>(entries.Values);
            for (int entryIndex = 0; entryIndex < pageEntries.Count; entryIndex++)
            {
                Entry entry = pageEntries[entryIndex];
                if (!entries.TryGetValue(entry.key, out Entry current)
                    || !ReferenceEquals(current, entry))
                {
                    continue;
                }
                if (!ReferenceEquals(entry.page, page) || !entry.placementReleasePending)
                    continue;

                ReleasePlacement(entry);
            }
        }

        private bool ReleasePlacement(Entry entry, bool notify = true)
        {
            if (entry == null || entry.page == null || entry.page.HasUnsafeGpuUse)
                return false;

            Page page = entry.page;
            page.allocator.Free(entry.allocatedRect);
            entry.page = null;
            entry.allocatedRect = default;
            entry.contentRect = default;
            entry.placementReleasePending = false;
            entry.pageLost = true;
            entry.resolveInvalidated = true;
            if (entry.slotGeneration == 0)
            {
                nextSlotGeneration++;
                if (nextSlotGeneration == 0)
                    nextSlotGeneration++;
                entry.slotGeneration = nextSlotGeneration;
            }
            else
            {
                entry.slotGeneration++;
            }
            if (notify)
                Notify(entry);
            return true;
        }

        private void RecoverLostPage(Page page)
        {
            if (page == null || page.HasUnsafeGpuUse)
                return;

            ReleasePendingPlacements(page);
            if (page.texture == null || !page.texture.IsCreated())
                page.CreateTexture();
            page.recoveryPending = false;
            recoveryRemovalBuffer.Clear();

            List<Entry> pageEntries = new List<Entry>(entries.Values);
            for (int entryIndex = 0; entryIndex < pageEntries.Count; entryIndex++)
            {
                Entry entry = pageEntries[entryIndex];
                if (!entries.TryGetValue(entry.key, out Entry current)
                    || !ReferenceEquals(current, entry))
                    continue;
                if (!ReferenceEquals(entry.page, page) || !entry.pageRecoveryPending)
                    continue;

                entry.pageRecoveryPending = false;
                entry.sourceHold?.Dispose();
                entry.sourceHold = null;
                entry.sourceHeld = false;
                entry.pageLost = true;
                entry.resolveInvalidated = true;

                if (entry.providerRetired)
                {
                    entry.state = ESDynamicAtlasEntryState.Retired;
                    Notify(entry);
                    if (entry.refCount == 0)
                        recoveryRemovalBuffer.Add(entry);
                    continue;
                }

                if (!entry.source.CanReload)
                {
                    FailEntry(entry, new InvalidOperationException(
                        $"动态图集 Page Lost 后无法重新加载临时 Source {entry.key.content}。"));
                    continue;
                }

                BeginSourceLoad(entry);
                Notify(entry);
            }

            // Evict only after the dictionary enumeration has completed.  A
            // provider transition can leave several retired, unreferenced
            // entries on the lost page; removing them inside the foreach would
            // invalidate the enumerator and turn recovery into an exception.
            for (int i = 0; i < recoveryRemovalBuffer.Count; i++)
                EvictEntry(recoveryRemovalBuffer[i]);
            recoveryRemovalBuffer.Clear();
        }

        private void EvictUnusedEntries()
        {
            float now = Time.realtimeSinceStartup;
            evictionRemovalBuffer.Clear();
            foreach (Entry entry in entries.Values)
            {
                if (entry.refCount != 0 || entry.state == ESDynamicAtlasEntryState.PendingSource
                    || entry.state == ESDynamicAtlasEntryState.QueuedUpload
                    || entry.state == ESDynamicAtlasEntryState.WaitingGpuFence
                    || entry.state == ESDynamicAtlasEntryState.Quarantined
                    || entry.pageRecoveryPending)
                {
                    continue;
                }

                ESDynamicAtlasDomainPolicy policy = GetPolicy(entry.key.domain);
                if (entry.providerRetired || entry.state == ESDynamicAtlasEntryState.Failed
                    || now - entry.lastReleaseTime >= policy.unusedEntryKeepAliveSeconds)
                {
                    evictionRemovalBuffer.Add(entry);
                }
            }

            for (int i = 0; i < evictionRemovalBuffer.Count; i++)
                EvictEntry(evictionRemovalBuffer[i]);
            RemoveEmptyPages(default);
        }

        private void EvictEntry(Entry entry)
        {
            if (entry == null || entry.refCount != 0)
                return;

            if (entry.state == ESDynamicAtlasEntryState.WaitingGpuFence
                || entry.state == ESDynamicAtlasEntryState.Quarantined
                || entry.pageRecoveryPending
                || (entry.page != null && entry.page.HasUnsafeGpuUse))
            {
                return;
            }

            if (!entries.TryGetValue(entry.key, out Entry current) || !ReferenceEquals(current, entry))
                return;

            entries.Remove(entry.key);
            entry.sourceLoadSerial++;
            entry.sourceHold?.Dispose();
            entry.sourceHold = null;
            entry.sourceHeld = false;
            if (entry.page != null)
                entry.page.allocator.Free(entry.allocatedRect);
            entry.slotGeneration++;
            entry.state = ESDynamicAtlasEntryState.Retired;
            Notify(entry);
            entry.observers.Clear();
        }

        private void FailEntry(Entry entry, Exception exception, bool preserveInFlightSourceHold = false)
        {
            if (entry == null)
                return;

            if (!preserveInFlightSourceHold)
            {
                entry.sourceHold?.Dispose();
                entry.sourceHold = null;
                entry.sourceHeld = false;
            }
            entry.state = ESDynamicAtlasEntryState.Failed;
            entry.failureMessage = exception?.Message ?? "动态图集条目失败。";
            if (!entry.initialCompletionSettled)
            {
                entry.initialCompletionSettled = true;
                entry.initialCompletion.TrySetException(exception ?? new InvalidOperationException("动态图集条目失败。 "));
            }

            // A page that was lost has no usable pixels at this placement. If its
            // recovery then fails, keeping the rectangle until an old Lease is
            // disposed turns a terminal failure into silent atlas exhaustion.
            // Releasing the placement does not release or mutate the caller Lease;
            // it only makes that Lease observably Failed/Lost and returns the slot
            // after all GPU use is known to be safe.
            if (entry.pageLost && entry.page != null)
            {
                entry.placementReleasePending = true;
                if (!entry.page.HasUnsafeGpuUse)
                    ReleasePlacement(entry, notify: false);
            }
            Notify(entry);
        }

        private void NotifyPageEntries(Page page)
        {
            if (page == null)
                return;

            List<Entry> pageEntries = new List<Entry>(entries.Values);
            for (int entryIndex = 0; entryIndex < pageEntries.Count; entryIndex++)
            {
                Entry entry = pageEntries[entryIndex];
                if (!entries.TryGetValue(entry.key, out Entry current)
                    || !ReferenceEquals(current, entry))
                {
                    continue;
                }
                if (!ReferenceEquals(entry.page, page))
                    continue;

                Notify(entry);
            }
        }

        private void Notify(Entry entry)
        {
            if (entry == null || entry.observers.Count == 0)
                return;

            // A callback can bind or clear another Graphic and recursively notify
            // this runtime. Pool a private snapshot for each nesting level so the
            // normal notification path stays allocation-free after warmup.
            List<Action> callbacks = observerCallbackBufferPool.Count > 0
                ? observerCallbackBufferPool.Pop()
                : new List<Action>(4);
            try
            {
                foreach (Action callback in entry.observers.Values)
                    callbacks.Add(callback);

                for (int i = 0; i < callbacks.Count; i++)
                {
                    try
                    {
                        callbacks[i]?.Invoke();
                    }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
            finally
            {
                callbacks.Clear();
                observerCallbackBufferPool.Push(callbacks);
            }
        }

        private void RemoveObservationsForLease(long leaseToken)
        {
            if (observations.Count == 0)
                return;

            observationRemovalBuffer.Clear();
            foreach (KeyValuePair<long, ObservationRecord> pair in observations)
                if (pair.Value.leaseToken == leaseToken)
                    observationRemovalBuffer.Add(pair.Key);

            for (int i = 0; i < observationRemovalBuffer.Count; i++)
                Unsubscribe(observationRemovalBuffer[i]);
        }

        private ESDynamicAtlasDomainPolicy GetPolicy(ESDynamicAtlasDomainKey domain)
        {
            if (!policies.TryGetValue(domain, out ESDynamicAtlasDomainPolicy policy))
            {
                policy = ESDynamicAtlasDomainPolicy.CreatePlatformDefault().CloneSanitized();
                policies.Add(domain, policy);
            }
            return policy;
        }

        private static void EnsureRuntimeOnly()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("ESDynamicAtlas Runtime 只能在 Play Mode 或 Player 中执行。 ");
        }

        private int CountDomainPages(ESDynamicAtlasDomainKey domain)
        {
            int count = 0;
            for (int i = 0; i < pages.Count; i++)
                if (pages[i].domain.Equals(domain))
                    count++;
            return count;
        }

        private int GetDomainGeneration(ESDynamicAtlasDomainKey domain)
        {
            return domainGenerations.TryGetValue(domain, out int generation) ? generation : 0;
        }

        private void RemoveEmptyPages(ESDynamicAtlasDomainKey onlyDomain)
        {
            for (int i = pages.Count - 1; i >= 0; i--)
            {
                Page page = pages[i];
                if (onlyDomain.IsValid && !page.domain.Equals(onlyDomain))
                    continue;
                if (page.allocator.UsedPixels != 0)
                    continue;
                if (page.HasUnsafeGpuUse || page.recoveryPending)
                    continue;

                page.Dispose();
                pages.RemoveAt(i);
            }
        }

        private long NextLeaseToken()
        {
            long token;
            do
            {
                token = ++nextLeaseToken;
                if (token == 0)
                    token = ++nextLeaseToken;
            }
            while (leases.ContainsKey(token));
            return token;
        }

        private long NextObservationToken()
        {
            long token;
            do
            {
                token = ++nextObservationToken;
                if (token == 0)
                    token = ++nextObservationToken;
            }
            while (observations.ContainsKey(token));
            return token;
        }

        private long NextDomainLeaseToken()
        {
            long token;
            do
            {
                token = ++nextDomainLeaseToken;
                if (token == 0)
                    token = ++nextDomainLeaseToken;
            }
            while (domainLeaseTokens.ContainsKey(token));
            return token;
        }

        private void AddUploadSample(float milliseconds)
        {
            if (uploadSamplesMilliseconds.Count >= 256)
                uploadSamplesMilliseconds.RemoveAt(0);
            uploadSamplesMilliseconds.Add(Mathf.Max(0f, milliseconds));
        }

        private void CalculatePercentiles(out float p50, out float p95, out float p99)
        {
            if (uploadSamplesMilliseconds.Count == 0)
            {
                p50 = p95 = p99 = 0f;
                return;
            }

            sortedUploadSamplesMilliseconds.Clear();
            sortedUploadSamplesMilliseconds.AddRange(uploadSamplesMilliseconds);
            sortedUploadSamplesMilliseconds.Sort();
            p50 = Percentile(sortedUploadSamplesMilliseconds, 0.50f);
            p95 = Percentile(sortedUploadSamplesMilliseconds, 0.95f);
            p99 = Percentile(sortedUploadSamplesMilliseconds, 0.99f);
        }

        private static float Percentile(List<float> sorted, float percentile)
        {
            int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * percentile) - 1, 0, sorted.Count - 1);
            return sorted[index];
        }
    }
}
