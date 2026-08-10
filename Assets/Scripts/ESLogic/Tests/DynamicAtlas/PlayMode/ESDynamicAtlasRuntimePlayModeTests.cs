using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ES.Tests.DynamicAtlas.PlayMode
{
    public sealed class ESDynamicAtlasRuntimePlayModeTests
    {
        [UnityTest]
        public IEnumerator DirectTextureCopy_WaitsForGpuCompletionAndResolvesLease()
        {
            var runtime = new ESDynamicAtlasRuntime();
            var source = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode");
            var content = new ESDynamicAtlasContentKey("texture:playmode-direct");
            var request = new ESDynamicAtlasRequest
            {
                padding = 0,
                colorSpace = ESDynamicAtlasColorSpace.Linear,
                alphaMode = ESDynamicAtlasAlphaMode.Straight,
                filterMode = FilterMode.Bilinear
            };
            ESDynamicAtlasLease lease = default;
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, request, default).AsTask();
                runtime.Tick();
                yield return null;

                if (!pending.IsCompleted)
                {
                    ESDynamicAtlasSnapshot inFlightSnapshot = runtime.CreateSnapshot();
                    Assert.That(inFlightSnapshot.entries.Count, Is.EqualTo(1));
                    Assert.That(inFlightSnapshot.entries[0].sourceHeld, Is.True,
                        "GPU 完成前不应释放动态图集 Source Hold。");
                }

                for (int frame = 0; frame < 120 && !pending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted, Is.True, "动态图集上传在 120 帧内没有完成。");
                Assert.That(pending.IsFaulted, Is.False,
                    pending.Exception == null ? "动态图集上传失败。" : pending.Exception.ToString());
                lease = pending.GetAwaiter().GetResult();

                for (int frame = 0; frame < 30 && !lease.TryResolve(out _); frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(lease.TryResolve(out ESDynamicAtlasResolved resolved), Is.True,
                    "GPU 完成令牌确认后仍无法解析动态图集 Lease。");
                Assert.That(resolved.texture, Is.Not.Null);
                Assert.That(resolved.pixelSize, Is.EqualTo(new Vector2Int(8, 4)));
                Assert.That(resolved.uvRect.width, Is.EqualTo(8f / 64f).Within(0.0001f));
                Assert.That(resolved.uvRect.height, Is.EqualTo(4f / 64f).Within(0.0001f));

                ESDynamicAtlasSnapshot completedSnapshot = runtime.CreateSnapshot();
                Assert.That(completedSnapshot.entries[0].sourceHeld, Is.False,
                    "GPU 完成后 Source Hold 应释放。");
                Assert.That(completedSnapshot.pendingFenceReleaseCount, Is.Zero,
                    "活动 Runtime 不应遗留待释放 GPU 上传。 ");
            }
            finally
            {
                lease.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_DestroyedBeforeUploadFailsWithoutHanging()
        {
            var runtime = new ESDynamicAtlasRuntime();
            var source = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.copy-owner-contract");
            var content = new ESDynamicAtlasContentKey("texture:playmode-copy-owner-contract");
            var request = new ESDynamicAtlasRequest
            {
                padding = 0,
                colorSpace = ESDynamicAtlasColorSpace.Linear,
                alphaMode = ESDynamicAtlasAlphaMode.Straight,
                filterMode = FilterMode.Bilinear
            };
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, request, default).AsTask();
                bool queued = false;
                for (int frame = 0; frame < 10 && !queued; frame++)
                {
                    ESDynamicAtlasSnapshot queuedSnapshot = runtime.CreateSnapshot();
                    queued = queuedSnapshot.entries.Count == 1
                             && queuedSnapshot.entries[0].state == ESDynamicAtlasEntryState.QueuedUpload;
                    if (!queued)
                        yield return null;
                }

                Assert.That(queued, Is.True,
                    "测试前提：未执行 Runtime.Tick() 前，源纹理应已进入待上传队列。 ");
                Object.Destroy(source);
                source = null;
                yield return null;
                runtime.Tick();

                for (int frame = 0;
                     frame < 180
                     && (!pending.IsCompleted || runtime.CreateSnapshot().totalEntryCount != 0);
                     frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted, Is.True,
                    "调用方提前销毁源纹理后，CopyAsync 等待必须在有限帧内结束。 ");
                Assert.That(pending.IsFaulted, Is.True,
                    "CopyAsync 不拥有源纹理；调用方提前销毁时不得交付 Lease。 ");
                Assert.That(pending.IsCompletedSuccessfully, Is.False,
                    "失败请求不得以成功结果形式存在可解析 Lease。 ");

                ESDynamicAtlasSnapshot finalSnapshot = runtime.CreateSnapshot();
                Assert.That(finalSnapshot.totalEntryCount, Is.Zero,
                    "没有 Lease 的失败 Copy 条目必须清理，不能残留可解析上传记录。 ");
                Assert.That(finalSnapshot.entries.Exists(entry => entry.sourceHeld), Is.False,
                    "失败完成后不得继续持有已销毁源纹理。 ");
                Assert.That(finalSnapshot.waitingFenceCount, Is.Zero,
                    "失败完成后不得残留 inFlight GPU Fence 等待。 ");
                Assert.That(finalSnapshot.pendingFenceReleaseCount, Is.Zero,
                    "失败完成后不得残留待释放 Fence。 ");
                Assert.That(finalSnapshot.quarantinedCount, Is.Zero,
                    "调用方主动销毁源纹理应有限失败并清理；若进入隔离，也必须明确 Quarantined 诊断。 ");
            }
            finally
            {
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_AsyncGpuReadbackFallbackKeepsSourceUntilResolved()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("当前图形后端不支持 AsyncGPUReadback，无法执行回退路径验收。");

            var runtime = new ESDynamicAtlasRuntime
            {
                ForceAsyncGpuReadbackCompletionForTests = true
            };
            var source = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.async-readback");
            var content = new ESDynamicAtlasContentKey("texture:playmode-async-readback");
            var request = new ESDynamicAtlasRequest
            {
                padding = 0,
                colorSpace = ESDynamicAtlasColorSpace.Linear,
                alphaMode = ESDynamicAtlasAlphaMode.Straight,
                filterMode = FilterMode.Bilinear
            };
            ESDynamicAtlasLease lease = default;
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, request, default).AsTask();
                runtime.Tick();
                yield return null;

                ESDynamicAtlasSnapshot inFlightSnapshot = runtime.CreateSnapshot();
                Assert.That(inFlightSnapshot.deferredFenceFallbackCount, Is.EqualTo(1));
                Assert.That(inFlightSnapshot.entries.Count, Is.EqualTo(1));
                Assert.That(inFlightSnapshot.entries[0].sourceHeld, Is.True);
                Assert.That(inFlightSnapshot.entries[0].uploadPath,
                    Is.EqualTo(ESDynamicAtlasUploadPath.DeferredFenceFallback));

                for (int frame = 0; frame < 120 && !pending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted, Is.True,
                    "AsyncGPUReadback 回退在 120 帧内没有完成。 ");
                Assert.That(pending.IsFaulted, Is.False,
                    pending.Exception == null ? "AsyncGPUReadback 回退失败。" : pending.Exception.ToString());
                lease = pending.GetAwaiter().GetResult();
                Assert.That(lease.TryResolve(out _), Is.True);

                ESDynamicAtlasSnapshot completedSnapshot = runtime.CreateSnapshot();
                Assert.That(completedSnapshot.deferredFenceFallbackCount, Is.EqualTo(1));
                Assert.That(completedSnapshot.entries[0].sourceHeld, Is.False);
                Assert.That(completedSnapshot.entries[0].uploadPath,
                    Is.EqualTo(ESDynamicAtlasUploadPath.DeferredFenceFallback));
            }
            finally
            {
                lease.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_UnknownGpuSubmissionKeepsSourceUntilCompletionProbe()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("当前图形后端不支持 AsyncGPUReadback，无法执行未知提交保护验收。");

            var runtime = new ESDynamicAtlasRuntime
            {
                ForceAsyncGpuReadbackCompletionForTests = true,
                ForceUnknownGpuSubmissionForTests = true
            };
            var source = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.unknown-submission");
            var content = new ESDynamicAtlasContentKey("texture:playmode-unknown-submission");
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, ESDynamicAtlasRequest.Default, default).AsTask();
                yield return null;
                runtime.Tick();

                Assert.That(pending.IsFaulted, Is.True,
                    "提交状态未知时不得把请求伪装成已完成的 Lease。 ");
                ESDynamicAtlasSnapshot inFlightSnapshot = runtime.CreateSnapshot();
                Assert.That(inFlightSnapshot.entries.Count, Is.EqualTo(1));
                Assert.That(inFlightSnapshot.entries[0].sourceHeld, Is.True,
                    "提交状态未知时，Source Hold 必须持续到完成探针返回。 ");
                Assert.That(inFlightSnapshot.deferredFenceFallbackCount, Is.EqualTo(1));

                for (int frame = 0; frame < 120 && runtime.CreateSnapshot().totalEntryCount != 0; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(runtime.CreateSnapshot().totalEntryCount, Is.Zero,
                    "完成探针返回后，失败上传应释放 Source Hold 并清理无引用条目。 ");
            }
            finally
            {
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_CompletionTokenFailureQuarantinesAndRecovers()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("当前图形后端不支持 AsyncGPUReadback，无法执行上传隔离验收。");

            var runtime = new ESDynamicAtlasRuntime
            {
                ForceAsyncGpuReadbackCompletionForTests = true,
                ForceUnknownGpuSubmissionForTests = true,
                ForceAsyncGpuReadbackFailureForTests = true
            };
            var source = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            var blockedSource = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.completion-quarantine");
            var content = new ESDynamicAtlasContentKey("texture:playmode-completion-quarantine");
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, ESDynamicAtlasRequest.Default, default).AsTask();
                yield return null;
                runtime.Tick();
                yield return null;

                Assert.That(pending.IsFaulted, Is.True,
                    "无法创建 GPU 完成令牌时不得交付 Lease。 ");
                ESDynamicAtlasSnapshot quarantined = runtime.CreateSnapshot();
                Assert.That(quarantined.quarantinedCount, Is.EqualTo(1),
                    "未知 GPU 完成状态必须从活动上传队列迁入隔离队列。 ");
                Assert.That(quarantined.entries.Count, Is.EqualTo(1));
                Assert.That(quarantined.entries[0].state, Is.EqualTo(ESDynamicAtlasEntryState.Quarantined));
                Assert.That(quarantined.entries[0].sourceHeld, Is.True,
                    "隔离期间不得释放 Source Hold。 ");
                Assert.That(quarantined.quarantinedPageIds,
                    Does.Contain(quarantined.entries[0].pageId),
                    "快照必须暴露被隔离的 Page。 ");

                Task<ESDynamicAtlasLease> blockedPending = runtime.AcquireAsync(
                    domain,
                    new ESDynamicAtlasContentKey("texture:playmode-quarantine-blocked"),
                    blockedSource,
                    ESDynamicAtlasRequest.Default,
                    default).AsTask();
                for (int frame = 0; frame < 30 && !blockedPending.IsCompleted; frame++)
                    yield return null;
                Assert.That(blockedPending.IsFaulted, Is.True,
                    "隔离 Page 不得复用；达到页数上限时新上传必须明确失败。 ");

                // 先验证至少一次探针失败仍不会释放，再允许后续探针建立安全完成令牌。
                runtime.Tick();
                yield return null;
                ESDynamicAtlasSnapshot afterProbeFailure = runtime.CreateSnapshot();
                Assert.That(afterProbeFailure.quarantinedCount, Is.EqualTo(1));
                Assert.That(afterProbeFailure.quarantineRetryCount, Is.GreaterThan(0));
                Assert.That(afterProbeFailure.quarantineFailureCount, Is.GreaterThan(0));
                ESDynamicAtlasEntrySnapshot afterProbeEntry = afterProbeFailure.entries.Find(
                    entry => entry.content.Equals(content));
                Assert.That(afterProbeEntry.content, Is.EqualTo(content));
                Assert.That(afterProbeEntry.sourceHeld, Is.True);

                runtime.ForceAsyncGpuReadbackFailureForTests = false;
                for (int frame = 0; frame < 180 && runtime.CreateSnapshot().quarantinedCount != 0; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                ESDynamicAtlasSnapshot recovered = runtime.CreateSnapshot();
                Assert.That(recovered.quarantinedCount, Is.Zero,
                    "安全探针完成后必须退出活动隔离队列。 ");
                Assert.That(recovered.quarantineRetryCount, Is.GreaterThan(0));
                Assert.That(recovered.quarantineFailureCount, Is.GreaterThan(0));
                Assert.That(recovered.totalEntryCount, Is.Zero,
                    "没有 Lease 的失败条目在安全确认后必须可被清理。 ");
            }
            finally
            {
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
                if (blockedSource != null)
                    Object.Destroy(blockedSource);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_PageQuarantineMarksExistingLeaseQuarantined()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("当前图形后端不支持 AsyncGPUReadback，无法执行 Lease 隔离状态验收。");

            var runtime = new ESDynamicAtlasRuntime
            {
                ForceAsyncGpuReadbackCompletionForTests = true
            };
            var firstSource = new Texture2D(8, 4, TextureFormat.RGBA32, false);
            var secondSource = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.lease-quarantine");
            ESDynamicAtlasLease firstLease = default;
            ESDynamicAtlasObservation firstObservation = default;
            Task<ESDynamicAtlasLease> firstPending = null;
            Task<ESDynamicAtlasLease> secondPending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 2,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                firstPending = runtime.AcquireAsync(
                    domain,
                    new ESDynamicAtlasContentKey("texture:lease-quarantine-first"),
                    firstSource,
                    ESDynamicAtlasRequest.Default,
                    default).AsTask();
                for (int frame = 0; frame < 120 && !firstPending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(firstPending.IsCompleted && !firstPending.IsFaulted, Is.True,
                    firstPending.Exception == null ? "首张纹理上传失败。" : firstPending.Exception.ToString());
                firstLease = firstPending.GetAwaiter().GetResult();
                Assert.That(firstLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Ready));
                int observerChangeCount = 0;
                firstObservation = firstLease.Subscribe(() => observerChangeCount++);

                runtime.ForceUnknownGpuSubmissionForTests = true;
                runtime.ForceAsyncGpuReadbackFailureForTests = true;
                secondPending = runtime.AcquireAsync(
                    domain,
                    new ESDynamicAtlasContentKey("texture:lease-quarantine-second"),
                    secondSource,
                    ESDynamicAtlasRequest.Default,
                    default).AsTask();
                yield return null;
                runtime.Tick();
                yield return null;

                Assert.That(secondPending.IsFaulted, Is.True,
                    "同 Page 的隔离上传不得交付 Lease。 ");
                Assert.That(runtime.CreateSnapshot().quarantinedCount, Is.EqualTo(1));
                Assert.That(firstLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Quarantined),
                    "Page 进入隔离后，同一 Page 的既有 Ready Lease 应可观察为 Quarantined。 ");
                Assert.That(firstLease.TryResolve(out _), Is.False,
                    "Page 处于隔离状态时，旧 Lease 不得继续渲染旧像素。 ");
                Assert.That(observerChangeCount, Is.GreaterThan(0),
                    "Page 进入隔离时必须通知同页既有 Lease 的观察者。 ");

                runtime.ForceUnknownGpuSubmissionForTests = false;
                runtime.ForceAsyncGpuReadbackFailureForTests = false;
                for (int frame = 0; frame < 180 && runtime.CreateSnapshot().quarantinedCount != 0; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(runtime.CreateSnapshot().quarantinedCount, Is.Zero);
                Assert.That(firstLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Ready),
                    "隔离探针安全完成后，Page 可恢复并让既有 Lease 回到 Ready。 ");
                Assert.That(firstLease.TryResolve(out _), Is.True);
                Assert.That(observerChangeCount, Is.GreaterThan(1),
                    "Page 退出隔离时必须再次通知同页既有 Lease 的观察者。 ");
            }
            finally
            {
                firstObservation.Dispose();
                firstLease.Dispose();
                runtime.Dispose();
                if (firstSource != null)
                    Object.Destroy(firstSource);
                if (secondSource != null)
                    Object.Destroy(secondSource);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_ConcurrentRequestsShareEntry()
        {
            var runtime = new ESDynamicAtlasRuntime();
            var source = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.shared");
            var content = new ESDynamicAtlasContentKey("texture:playmode-shared", "revision-a");
            var request = ESDynamicAtlasRequest.Default;
            ESDynamicAtlasLease first = default;
            ESDynamicAtlasLease second = default;
            Task<ESDynamicAtlasLease> firstPending = null;
            Task<ESDynamicAtlasLease> secondPending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                firstPending = runtime.AcquireAsync(domain, content, source, request, default).AsTask();
                secondPending = runtime.AcquireAsync(domain, content, source, request, default).AsTask();
                for (int frame = 0; frame < 120 && (!firstPending.IsCompleted || !secondPending.IsCompleted); frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(firstPending.IsCompleted, Is.True);
                Assert.That(secondPending.IsCompleted, Is.True);
                first = firstPending.GetAwaiter().GetResult();
                second = secondPending.GetAwaiter().GetResult();

                Assert.That(first.TryResolve(out ESDynamicAtlasResolved firstResolved), Is.True);
                Assert.That(second.TryResolve(out ESDynamicAtlasResolved secondResolved), Is.True);
                Assert.That(firstResolved.texture, Is.SameAs(secondResolved.texture));
                Assert.That(firstResolved.uvRect, Is.EqualTo(secondResolved.uvRect));
                Assert.That(runtime.CreateSnapshot().entries.Count, Is.EqualTo(1));

                ESDynamicAtlasSnapshot summaryOnly = runtime.CreateSnapshot(maxEntryDetails: 0);
                Assert.That(summaryOnly.totalEntryCount, Is.EqualTo(1));
                Assert.That(summaryOnly.entries, Is.Empty);
                Assert.That(summaryOnly.omittedEntryCount, Is.EqualTo(1));
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_ReusedSlotNeverResolvesOldLease()
        {
            var runtime = new ESDynamicAtlasRuntime();
            if (SystemInfo.supportsAsyncGPUReadback)
                runtime.ForceAsyncGpuReadbackCompletionForTests = true;

            var firstSource = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var secondSource = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.slot-reuse");
            var request = new ESDynamicAtlasRequest
            {
                padding = 0,
                colorSpace = ESDynamicAtlasColorSpace.Linear,
                alphaMode = ESDynamicAtlasAlphaMode.Straight,
                filterMode = FilterMode.Bilinear
            };
            ESDynamicAtlasLease first = default;
            ESDynamicAtlasLease oldLeaseCopy = default;
            ESDynamicAtlasLease second = default;
            Task<ESDynamicAtlasLease> firstPending = null;
            Task<ESDynamicAtlasLease> secondPending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                firstPending = runtime.AcquireAsync(domain,
                    new ESDynamicAtlasContentKey("texture:playmode-slot-a"), firstSource, request, default).AsTask();
                for (int frame = 0; frame < 120 && !firstPending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(firstPending.IsCompleted && !firstPending.IsFaulted, Is.True,
                    firstPending?.Exception == null ? "首个动态图集上传失败。" : firstPending.Exception.ToString());
                first = firstPending.GetAwaiter().GetResult();
                Assert.That(first.TryResolve(out ESDynamicAtlasResolved firstResolved), Is.True);
                oldLeaseCopy = first;
                first.Dispose();
                runtime.Tick();
                yield return null;

                secondPending = runtime.AcquireAsync(domain,
                    new ESDynamicAtlasContentKey("texture:playmode-slot-b"), secondSource, request, default).AsTask();
                for (int frame = 0; frame < 120 && !secondPending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(secondPending.IsCompleted && !secondPending.IsFaulted, Is.True,
                    secondPending?.Exception == null ? "复用槽位后的动态图集上传失败。" : secondPending.Exception.ToString());
                second = secondPending.GetAwaiter().GetResult();
                Assert.That(second.TryResolve(out ESDynamicAtlasResolved secondResolved), Is.True);
                Assert.That(oldLeaseCopy.TryResolve(out _), Is.False,
                    "已释放的旧 Lease 不得在槽位复用后重新解析。 ");
                Assert.That(secondResolved.slotGeneration, Is.Not.EqualTo(firstResolved.slotGeneration),
                    "每次分配必须获得新的 Slot Generation。 ");
            }
            finally
            {
                first.Dispose();
                oldLeaseCopy.Dispose();
                second.Dispose();
                runtime.Dispose();
                if (firstSource != null)
                    Object.Destroy(firstSource);
                if (secondSource != null)
                    Object.Destroy(secondSource);
            }
        }

        [UnityTest]
        public IEnumerator DomainClose_DuringGpuCompletionKeepsSourceUntilProbeCompletes()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("当前图形后端不支持 AsyncGPUReadback，无法执行 Domain 关闭上传门禁验收。");

            var runtime = new ESDynamicAtlasRuntime
            {
                ForceAsyncGpuReadbackCompletionForTests = true
            };
            var source = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.domain-close-in-flight");
            var content = new ESDynamicAtlasContentKey("texture:playmode-domain-close-in-flight");
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, ESDynamicAtlasRequest.Default, default).AsTask();
                for (int frame = 0; frame < 30 && runtime.CreateSnapshot().waitingFenceCount == 0; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                ESDynamicAtlasSnapshot beforeClose = runtime.CreateSnapshot();
                Assert.That(beforeClose.waitingFenceCount, Is.EqualTo(1));
                Assert.That(beforeClose.entries[0].sourceHeld, Is.True);

                runtime.CloseDomain(domain);

                Assert.That(pending.IsFaulted, Is.True,
                    "关闭中的 Domain 不得把旧代上传交付给调用方。 ");
                ESDynamicAtlasSnapshot afterClose = runtime.CreateSnapshot();
                Assert.That(afterClose.entries[0].sourceHeld, Is.True,
                    "Domain 已关闭不代表 GPU 已停止读取源纹理。 ");

                for (int frame = 0; frame < 120 && runtime.CreateSnapshot().totalEntryCount != 0; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(runtime.CreateSnapshot().totalEntryCount, Is.Zero,
                    "完成探针返回后，关闭 Domain 的无引用上传应安全清理。 ");
            }
            finally
            {
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_CancellingOneWaiterKeepsSharedUploadAlive()
        {
            var runtime = new ESDynamicAtlasRuntime();
            if (SystemInfo.supportsAsyncGPUReadback)
                runtime.ForceAsyncGpuReadbackCompletionForTests = true;

            var source = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.cancel");
            var content = new ESDynamicAtlasContentKey("texture:playmode-cancel");
            using var cancellation = new System.Threading.CancellationTokenSource();
            ESDynamicAtlasLease survivingLease = default;
            UniTask<ESDynamicAtlasLease> cancelledWaiter = default;
            Task<ESDynamicAtlasLease> survivingWaiter = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                cancelledWaiter = runtime.AcquireAsync(domain, content, source,
                    ESDynamicAtlasRequest.Default, cancellation.Token);
                survivingWaiter = runtime.AcquireAsync(domain, content, source,
                    ESDynamicAtlasRequest.Default, default).AsTask();
                cancellation.Cancel();

                for (int frame = 0; frame < 120 && !survivingWaiter.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                // This UniTask version maps cancellation to a faulted Task in
                // AsTask(), so validate the public UniTask contract directly.
                Assert.That(cancelledWaiter.Status, Is.EqualTo(UniTaskStatus.Canceled),
                    "调用者取消应只取消自己的等待。 ");
                Assert.That(survivingWaiter.IsCompleted, Is.True);
                Assert.That(survivingWaiter.IsFaulted, Is.False,
                    survivingWaiter.Exception == null ? "共享上传被错误取消。" : survivingWaiter.Exception.ToString());
                survivingLease = survivingWaiter.GetAwaiter().GetResult();
                Assert.That(survivingLease.TryResolve(out _), Is.True);
                Assert.That(runtime.CreateSnapshot().totalEntryCount, Is.EqualTo(1));
            }
            finally
            {
                survivingLease.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTextureCopy_ContentRevisionCreatesDistinctEntry()
        {
            var runtime = new ESDynamicAtlasRuntime();
            if (SystemInfo.supportsAsyncGPUReadback)
                runtime.ForceAsyncGpuReadbackCompletionForTests = true;

            var firstSource = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var secondSource = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.revision");
            var request = ESDynamicAtlasRequest.Default;
            ESDynamicAtlasLease first = default;
            ESDynamicAtlasLease second = default;
            Task<ESDynamicAtlasLease> firstPending = null;
            Task<ESDynamicAtlasLease> secondPending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 2,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                firstPending = runtime.AcquireAsync(domain,
                    new ESDynamicAtlasContentKey("avatar:test-user", "revision-a"),
                    firstSource, request, default).AsTask();
                secondPending = runtime.AcquireAsync(domain,
                    new ESDynamicAtlasContentKey("avatar:test-user", "revision-b"),
                    secondSource, request, default).AsTask();
                for (int frame = 0; frame < 120 && (!firstPending.IsCompleted || !secondPending.IsCompleted); frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(firstPending.IsCompleted, Is.True);
                Assert.That(secondPending.IsCompleted, Is.True);
                first = firstPending.GetAwaiter().GetResult();
                second = secondPending.GetAwaiter().GetResult();
                Assert.That(first.TryResolve(out ESDynamicAtlasResolved firstResolved), Is.True);
                Assert.That(second.TryResolve(out ESDynamicAtlasResolved secondResolved), Is.True);
                Assert.That(firstResolved.uvRect, Is.Not.EqualTo(secondResolved.uvRect));
                Assert.That(runtime.CreateSnapshot().totalEntryCount, Is.EqualTo(2));
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                runtime.Dispose();
                if (firstSource != null)
                    Object.Destroy(firstSource);
                if (secondSource != null)
                    Object.Destroy(secondSource);
            }
        }

        [UnityTest]
        public IEnumerator DirectTexturePageLoss_InvalidatesLeaseAndReportsLost()
        {
            var runtime = new ESDynamicAtlasRuntime();
            var source = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.page-loss");
            var content = new ESDynamicAtlasContentKey("texture:playmode-page-loss");
            ESDynamicAtlasLease lease = default;
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, ESDynamicAtlasRequest.Default, default).AsTask();
                for (int frame = 0; frame < 120 && !pending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted, Is.True);
                lease = pending.GetAwaiter().GetResult();
                Assert.That(lease.TryResolve(out ESDynamicAtlasResolved resolved), Is.True);
                Assert.That(resolved.texture, Is.TypeOf<RenderTexture>());

                ((RenderTexture)resolved.texture).Release();
                runtime.Tick();
                yield return null;

                Assert.That(lease.TryResolve(out _), Is.False);
                Assert.That(lease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Failed),
                    "不可重载的直接纹理 Page Lost 后，旧 Lease 应进入终态 Failed 状态。 ");
                Assert.That(runtime.CreateSnapshot().lostCount, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                lease.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DirectTexturePageLoss_ReleasesTerminalPlacementAndAllowsReallocation()
        {
            var runtime = new ESDynamicAtlasRuntime();
            var source = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.page-loss-terminal-recycle");
            var content = new ESDynamicAtlasContentKey("texture:playmode-page-loss-terminal-recycle");
            ESDynamicAtlasLease lease = default;
            ESDynamicAtlasLease replacementLease = default;
            Task<ESDynamicAtlasLease> pending = null;
            Task<ESDynamicAtlasLease> replacementPending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source,
                    ESDynamicAtlasRequest.Default, default).AsTask();
                for (int frame = 0; frame < 120 && !pending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted, Is.True);
                lease = pending.GetAwaiter().GetResult();
                Assert.That(lease.TryResolve(out ESDynamicAtlasResolved resolved), Is.True);

                ((RenderTexture)resolved.texture).Release();
                runtime.Tick();
                yield return null;

                Assert.That(lease.TryResolve(out _), Is.False);
                ESDynamicAtlasSnapshot afterLoss = runtime.CreateSnapshot();
                Assert.That(afterLoss.lostCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(afterLoss.pages.Count, Is.Zero,
                    "不可重载的终态 Page Lost 条目应在确认无在途 GPU 后归还槽位并允许页面回收。 ");

                lease.Dispose();
                replacementPending = runtime.AcquireAsync(domain, content, source,
                    ESDynamicAtlasRequest.Default, default).AsTask();
                for (int frame = 0; frame < 120 && !replacementPending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(replacementPending.IsCompleted, Is.True);
                replacementLease = replacementPending.GetAwaiter().GetResult();
                Assert.That(replacementLease.TryResolve(out _), Is.True);
                ESDynamicAtlasSnapshot afterReplacement = runtime.CreateSnapshot();
                Assert.That(afterReplacement.pages.Count, Is.EqualTo(1));
                Assert.That(afterReplacement.pages[0].usedPixels, Is.GreaterThan(0));
            }
            finally
            {
                replacementLease.Dispose();
                lease.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator DomainClose_InvalidatesExistingLease()
        {
            var runtime = new ESDynamicAtlasRuntime();
            var source = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var domain = new ESDynamicAtlasDomainKey("test.playmode.domain-close");
            var content = new ESDynamicAtlasContentKey("texture:playmode-domain-close");
            ESDynamicAtlasLease lease = default;
            Task<ESDynamicAtlasLease> pending = null;

            try
            {
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                pending = runtime.AcquireAsync(domain, content, source, ESDynamicAtlasRequest.Default, default).AsTask();
                for (int frame = 0; frame < 120 && !pending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted, Is.True);
                lease = pending.GetAwaiter().GetResult();
                Assert.That(lease.TryResolve(out _), Is.True);

                runtime.CloseDomain(domain);

                Assert.That(lease.TryResolve(out _), Is.False);
                Assert.That(runtime.CreateSnapshot().retiredCount, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                lease.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        [UnityTest]
        public IEnumerator Graphic_UsesCanvasGroupAndMaskRenderingState()
        {
            Shader shader = Shader.Find("UI/Default");
            if (shader == null)
                Assert.Ignore("当前 Unity 环境没有 UI/Default Shader。");

            var canvasObject = new GameObject("ES Dynamic Atlas Canvas", typeof(RectTransform), typeof(Canvas));
            var maskObject = new GameObject("ES Dynamic Atlas Mask", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Mask), typeof(CanvasGroup));
            var graphicObject = new GameObject("ES Dynamic Atlas Graphic", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var customMaterial = new Material(shader);
            try
            {
                maskObject.transform.SetParent(canvasObject.transform, false);
                graphicObject.transform.SetParent(maskObject.transform, false);

                CanvasGroup group = maskObject.GetComponent<CanvasGroup>();
                group.alpha = 0.35f;
                ESDynamicAtlasGraphic graphic = graphicObject.GetComponent<ESDynamicAtlasGraphic>();
                graphic.material = customMaterial;

                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();

                Assert.That(graphic.canvasRenderer.GetInheritedAlpha(), Is.EqualTo(group.alpha).Within(0.01f),
                    "CanvasGroup 透明度应传递到动态图集 Graphic。 ");
                Assert.That(graphic.materialForRendering, Is.Not.SameAs(customMaterial),
                    "Mask 下的动态图集 Graphic 应取得 Stencil 修饰后的渲染材质。 ");
            }
            finally
            {
                UnityEngine.Object.Destroy(customMaterial);
                UnityEngine.Object.Destroy(canvasObject);
            }
        }

        [UnityTest]
        public IEnumerator Graphic_NarrowCopyAsync_NullTextureThrowsArgumentNull()
        {
            var canvasObject = new GameObject("ES Dynamic Atlas Narrow Copy Canvas",
                typeof(Canvas));
            var gameObject = new GameObject("ES Dynamic Atlas Narrow Copy Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            try
            {
                gameObject.transform.SetParent(canvasObject.transform, false);
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();

                Assert.Throws<System.ArgumentNullException>(() =>
                    graphic.CopyAsync((Texture)null, "v1"));
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
                UnityEngine.Object.Destroy(canvasObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Graphic_InvalidManualRequestDoesNotCancelAutoAcquire()
        {
            var canvasObject = new GameObject("ES Dynamic Atlas Invalid Manual Request Canvas",
                typeof(Canvas));
            var gameObject = new GameObject("ES Dynamic Atlas Invalid Manual Request Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var autoAcquireCancellation = new CancellationTokenSource();
            var invalidTexture = new Cubemap(4, TextureFormat.RGBA32, false);
            try
            {
                gameObject.transform.SetParent(canvasObject.transform, false);
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "autoAcquireCancellation", autoAcquireCancellation);

                Assert.Throws<System.ArgumentException>(() =>
                    graphic.SetAsync(new ESAssetReferTexture2D(), "v1"));
                Assert.That(autoAcquireCancellation.IsCancellationRequested, Is.False,
                    "无效资源引用不能取消已经运行的自动加载。 ");

                Assert.Throws<System.ArgumentException>(() => graphic.CopyAsync(invalidTexture, "v1"));
                Assert.That(autoAcquireCancellation.IsCancellationRequested, Is.False,
                    "无效 Texture 不能取消已经运行的自动加载。 ");
            }
            finally
            {
                SetPrivateField<CancellationTokenSource>(gameObject.GetComponent<ESDynamicAtlasGraphic>(),
                    "autoAcquireCancellation", null);
                autoAcquireCancellation.Dispose();
                UnityEngine.Object.Destroy(invalidTexture);
                UnityEngine.Object.Destroy(gameObject);
                UnityEngine.Object.Destroy(canvasObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Graphic_AutoAcquireCancellationDoesNotOverwriteNewerStatus()
        {
            var canvasObject = new GameObject("ES Dynamic Atlas Auto Acquire Race Canvas",
                typeof(Canvas));
            var gameObject = new GameObject("ES Dynamic Atlas Auto Acquire Race Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var oldCancellation = new CancellationTokenSource();
            var replacementCancellation = new CancellationTokenSource();
            try
            {
                gameObject.transform.SetParent(canvasObject.transform, false);
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "autoAcquireCancellation", oldCancellation);
                InvokePrivate(graphic, "AcquireFromInspectorAsync", oldCancellation.Token);
                yield return null;

                SetPrivateField(graphic, "status", "new-request-status");
                SetPrivateField(graphic, "autoAcquireCancellation", replacementCancellation);
                oldCancellation.Cancel();
                for (int frame = 0; frame < 5; frame++)
                    yield return null;

                Assert.That(GetPrivateField<string>(graphic, "status"),
                    Is.EqualTo("new-request-status"),
                    "旧自动加载被取消且被新代取代后，不得覆盖新请求状态。 ");
            }
            finally
            {
                oldCancellation.Dispose();
                replacementCancellation.Dispose();
                UnityEngine.Object.Destroy(gameObject);
                UnityEngine.Object.Destroy(canvasObject);
            }
        }

        [UnityTest]
        public IEnumerator PaddingCopy_PreservesEdgesWithoutAdjacentColorBleed()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("当前图形后端不支持 AsyncGPUReadback，无法执行动态图集像素验收。 ");

            ESDynamicAtlasColorSpace[] colorSpaces =
            {
                ESDynamicAtlasColorSpace.SRGB,
                ESDynamicAtlasColorSpace.Linear
            };
            ESDynamicAtlasAlphaMode[] alphaModes =
            {
                ESDynamicAtlasAlphaMode.Straight,
                ESDynamicAtlasAlphaMode.Premultiplied
            };

            for (int colorSpaceIndex = 0; colorSpaceIndex < colorSpaces.Length; colorSpaceIndex++)
            {
                for (int alphaModeIndex = 0; alphaModeIndex < alphaModes.Length; alphaModeIndex++)
                {
                    ESDynamicAtlasColorSpace colorSpace = colorSpaces[colorSpaceIndex];
                    ESDynamicAtlasAlphaMode alphaMode = alphaModes[alphaModeIndex];
                    var runtime = new ESDynamicAtlasRuntime();
                    Texture2D firstSource = null;
                    Texture2D secondSource = null;
                    ESDynamicAtlasLease firstLease = default;
                    ESDynamicAtlasLease secondLease = default;

                    try
                    {
                        var domain = new ESDynamicAtlasDomainKey(
                            $"test.playmode.padding-pixels.{colorSpace}.{alphaMode}");
                        runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                        {
                            pageSize = 64,
                            maxPages = 1,
                            maxUploadsPerFrame = 2,
                            maxUploadPixelsPerFrame = 64 * 64,
                            unusedEntryKeepAliveSeconds = 0f
                        });

                        // 半透明且高对比的两块纯色，能同时检验 Alpha 路径和相邻区域隔离。
                        firstSource = CreateSolidTexture(new Color32(240, 24, 24, 128),
                            colorSpace == ESDynamicAtlasColorSpace.Linear);
                        secondSource = CreateSolidTexture(new Color32(24, 64, 240, 128),
                            colorSpace == ESDynamicAtlasColorSpace.Linear);
                        var request = new ESDynamicAtlasRequest
                        {
                            padding = 4,
                            colorSpace = colorSpace,
                            alphaMode = alphaMode,
                            filterMode = FilterMode.Bilinear
                        };

                        Task<ESDynamicAtlasLease> firstPending = runtime.AcquireAsync(
                            domain, new ESDynamicAtlasContentKey("pixel:first", "v1"),
                            firstSource, request, default).AsTask();
                        Task<ESDynamicAtlasLease> secondPending = runtime.AcquireAsync(
                            domain, new ESDynamicAtlasContentKey("pixel:second", "v1"),
                            secondSource, request, default).AsTask();

                        for (int frame = 0; frame < 180
                             && (!firstPending.IsCompleted || !secondPending.IsCompleted); frame++)
                        {
                            runtime.Tick();
                            yield return null;
                        }

                        Assert.That(firstPending.IsCompleted && !firstPending.IsFaulted, Is.True,
                            firstPending.Exception == null ? "第一块像素纹理上传失败。 " : firstPending.Exception.ToString());
                        Assert.That(secondPending.IsCompleted && !secondPending.IsFaulted, Is.True,
                            secondPending.Exception == null ? "第二块像素纹理上传失败。 " : secondPending.Exception.ToString());
                        firstLease = firstPending.GetAwaiter().GetResult();
                        secondLease = secondPending.GetAwaiter().GetResult();
                        Assert.That(firstLease.TryResolve(out ESDynamicAtlasResolved firstResolved), Is.True);
                        Assert.That(secondLease.TryResolve(out ESDynamicAtlasResolved secondResolved), Is.True);

                        ESDynamicAtlasSnapshot snapshot = runtime.CreateSnapshot();
                        Assert.That(snapshot.paddingShaderCount, Is.GreaterThanOrEqualTo(2),
                            "带 Padding 的上传必须走 GPU Padding Shader 路径。 ");
                        Assert.That(firstResolved.texture, Is.SameAs(secondResolved.texture));
                        Assert.That(firstResolved.pixelSize, Is.EqualTo(new Vector2Int(4, 4)));
                        Assert.That(secondResolved.pixelSize, Is.EqualTo(new Vector2Int(4, 4)));

                        var page = (RenderTexture)firstResolved.texture;
                        AsyncGPUReadbackRequest readback = AsyncGPUReadback.Request(page, 0);
                        for (int frame = 0; frame < 120 && !readback.done; frame++)
                        {
                            runtime.Tick();
                            yield return null;
                        }

                        Assert.That(readback.done, Is.True, "动态图集像素回读在 120 帧内没有完成。 ");
                        Assert.That(readback.hasError, Is.False, "动态图集页面像素回读失败。 ");
                        NativeArray<byte> pixels = readback.GetData<byte>();
                        int pageSize = page.width;
                        int firstX = Mathf.RoundToInt(firstResolved.uvRect.x * pageSize);
                        int firstY = Mathf.RoundToInt(firstResolved.uvRect.y * pageSize);
                        int secondX = Mathf.RoundToInt(secondResolved.uvRect.x * pageSize);
                        int secondY = Mathf.RoundToInt(secondResolved.uvRect.y * pageSize);

                        Color32 firstCenter = ReadPixel(pixels, pageSize, firstX + 2, firstY + 2);
                        Color32 secondCenter = ReadPixel(pixels, pageSize, secondX + 2, secondY + 2);
                        Assert.That(ColorDistance(firstCenter, secondCenter), Is.GreaterThan(20f),
                            "两块确定性纹理在图集中不应变成同一颜色。 ");

                        AssertPaddingMatchesContent(pixels, pageSize, firstX, firstY, firstCenter,
                            "第一块纹理");
                        AssertPaddingMatchesContent(pixels, pageSize, secondX, secondY, secondCenter,
                            "第二块纹理");
                    }
                    finally
                    {
                        firstLease.Dispose();
                        secondLease.Dispose();
                        runtime.Dispose();
                        if (firstSource != null)
                            Object.Destroy(firstSource);
                        if (secondSource != null)
                            Object.Destroy(secondSource);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator Graphic_RectMask2DClipsAtlasPixels()
        {
            if (!SystemInfo.supportsAsyncGPUReadback)
                Assert.Ignore("当前图形后端不支持 AsyncGPUReadback，无法执行 RectMask2D 像素验收。 ");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("当前运行在无图形设备后端，无法执行 RectMask2D 像素验收。 ");

            var runtime = new ESDynamicAtlasRuntime();
            Texture2D source = null;
            RenderTexture target = null;
            ESDynamicAtlasLease lease = default;
            GameObject canvasObject = null;
            GameObject maskObject = null;
            GameObject graphicObject = null;
            GameObject cameraObject = null;

            try
            {
                var domain = new ESDynamicAtlasDomainKey("test.playmode.rect-mask");
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                source = CreateSolidTexture(new Color32(240, 24, 24, 255), false);
                Task<ESDynamicAtlasLease> pending = runtime.AcquireAsync(
                    domain,
                    new ESDynamicAtlasContentKey("rect-mask:source", "v1"),
                    source,
                    new ESDynamicAtlasRequest
                    {
                        padding = 0,
                        colorSpace = ESDynamicAtlasColorSpace.Linear,
                        alphaMode = ESDynamicAtlasAlphaMode.Straight,
                        filterMode = FilterMode.Point
                    },
                    default).AsTask();

                for (int frame = 0; frame < 120 && !pending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted && !pending.IsFaulted, Is.True,
                    pending.Exception == null ? "RectMask2D 夹具的动态图集上传失败。 " : pending.Exception.ToString());
                lease = pending.GetAwaiter().GetResult();
                Assert.That(lease.TryResolve(out _), Is.True);

                target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                target.Create();

                cameraObject = new GameObject("ES Dynamic Atlas RectMask Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 32f;
                camera.aspect = 1f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.targetTexture = target;

                canvasObject = new GameObject("ES Dynamic Atlas RectMask Canvas", typeof(RectTransform), typeof(Canvas));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(64f, 64f);
                canvasObject.transform.position = new Vector3(-32f, -32f, 0f);

                maskObject = new GameObject("ES Dynamic Atlas RectMask", typeof(RectTransform), typeof(RectMask2D));
                maskObject.transform.SetParent(canvasObject.transform, false);
                RectTransform maskRect = maskObject.GetComponent<RectTransform>();
                maskRect.anchorMin = new Vector2(0.5f, 0.5f);
                maskRect.anchorMax = new Vector2(0.5f, 0.5f);
                maskRect.pivot = new Vector2(0.5f, 0.5f);
                maskRect.anchoredPosition = new Vector2(32f, 32f);
                maskRect.sizeDelta = new Vector2(20f, 20f);

                graphicObject = new GameObject("ES Dynamic Atlas RectMask Graphic",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
                graphicObject.transform.SetParent(maskObject.transform, false);
                RectTransform graphicRect = graphicObject.GetComponent<RectTransform>();
                graphicRect.anchorMin = new Vector2(0.5f, 0.5f);
                graphicRect.anchorMax = new Vector2(0.5f, 0.5f);
                graphicRect.pivot = new Vector2(0.5f, 0.5f);
                graphicRect.anchoredPosition = Vector2.zero;
                graphicRect.sizeDelta = new Vector2(40f, 40f);

                ESDynamicAtlasGraphic graphic = graphicObject.GetComponent<ESDynamicAtlasGraphic>();
                InvokePrivateBind(graphic, lease);
                lease = default;
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                camera.Render();

                AsyncGPUReadbackRequest readback = AsyncGPUReadback.Request(target, 0);
                for (int frame = 0; frame < 120 && !readback.done; frame++)
                    yield return null;

                Assert.That(readback.done, Is.True, "RectMask2D RenderTexture 回读在 120 帧内没有完成。 ");
                Assert.That(readback.hasError, Is.False, "RectMask2D RenderTexture 回读失败。 ");
                NativeArray<byte> pixels = readback.GetData<byte>();
                Color32 center = ReadPixel(pixels, 64, 32, 32);
                Color32 outside = ReadPixel(pixels, 64, 5, 5);
                Assert.That(center.a, Is.GreaterThan(200), "RectMask2D 内部像素应保持可见。 ");
                Assert.That(outside.a, Is.LessThan(8), "RectMask2D 外部像素不应显示动态图集内容。 ");
            }
            finally
            {
                if (graphicObject != null)
                    Object.Destroy(graphicObject);
                if (maskObject != null)
                    Object.Destroy(maskObject);
                if (canvasObject != null)
                    Object.Destroy(canvasObject);
                if (cameraObject != null)
                    Object.Destroy(cameraObject);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                lease.Dispose();
                runtime.Dispose();
                if (source != null)
                    Object.Destroy(source);
            }
        }

        private static Texture2D CreateSolidTexture(Color32 color, bool linear)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, linear)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[16];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = color;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Color32 ReadPixel(NativeArray<byte> pixels, int width, int x, int y)
        {
            Assert.That(x, Is.GreaterThanOrEqualTo(0));
            Assert.That(y, Is.GreaterThanOrEqualTo(0));
            int offset = (y * width + x) * 4;
            Assert.That(offset + 3, Is.LessThan(pixels.Length),
                "动态图集像素坐标超出页面回读范围。 ");
            return new Color32(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
        }

        private static void AssertPaddingMatchesContent(
            NativeArray<byte> pixels,
            int pageSize,
            int contentX,
            int contentY,
            Color32 expected,
            string label)
        {
            // 当前夹具为 4x4 内容、4px Padding；四个方向各取一像素，
            // 只要出现邻区颜色就说明边缘扩张或 UV 采样发生串色。
            Color32 left = ReadPixel(pixels, pageSize, contentX - 1, contentY + 2);
            Color32 right = ReadPixel(pixels, pageSize, contentX + 4, contentY + 2);
            Color32 bottom = ReadPixel(pixels, pageSize, contentX + 2, contentY - 1);
            Color32 top = ReadPixel(pixels, pageSize, contentX + 2, contentY + 4);
            Assert.That(ColorDistance(left, expected), Is.LessThan(8f), $"{label} 左侧 Padding 串色。 ");
            Assert.That(ColorDistance(right, expected), Is.LessThan(8f), $"{label} 右侧 Padding 串色。 ");
            Assert.That(ColorDistance(bottom, expected), Is.LessThan(8f), $"{label} 下侧 Padding 串色。 ");
            Assert.That(ColorDistance(top, expected), Is.LessThan(8f), $"{label} 上侧 Padding 串色。 ");
        }

        private static float ColorDistance(Color32 left, Color32 right)
        {
            float red = left.r - right.r;
            float green = left.g - right.g;
            float blue = left.b - right.b;
            float alpha = left.a - right.a;
            return Mathf.Sqrt(red * red + green * green + blue * blue + alpha * alpha);
        }

        private static void InvokePrivate(
            object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "缺少测试方法：" + methodName);
            method.Invoke(target, arguments);
        }

        private static void SetPrivateField<T>(
            object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "缺少测试字段：" + fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "缺少测试字段：" + fieldName);
            return (T)field.GetValue(target);
        }

        private static void InvokePrivateBind(ESDynamicAtlasGraphic graphic, ESDynamicAtlasLease lease)
        {
            MethodInfo bind = typeof(ESDynamicAtlasGraphic).GetMethod(
                "Bind", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bind, Is.Not.Null, "动态图集 Graphic 缺少内部 Lease 绑定入口。 ");
            bind.Invoke(graphic, new object[] { lease });
        }
    }
}
