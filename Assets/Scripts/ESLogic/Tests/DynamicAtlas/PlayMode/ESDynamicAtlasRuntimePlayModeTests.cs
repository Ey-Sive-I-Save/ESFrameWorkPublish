using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
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

                Assert.That(graphic.canvasRenderer.GetAlpha(), Is.EqualTo(group.alpha).Within(0.01f),
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
    }
}
