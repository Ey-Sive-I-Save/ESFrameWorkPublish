using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ES.Tests.DynamicAtlas.PlayMode
{
    // 这里只验证内存 DirectProvider 的代际与 Lease 契约；不代表 Manifest、下载、缓存或正式热更新 Provider 验收。
    public sealed class ESDynamicAtlasProviderAcceptanceTests
    {
        private sealed class DirectProvider : IESRuntimeDirectAssetProvider
        {
            private readonly Texture2D texture;

            public DirectProvider(Texture2D texture)
            {
                this.texture = texture;
            }

            public UniTask<Object> LoadAsync(ESAssetIdentity id, CancellationToken cancellationToken)
                => UniTask.FromResult<Object>(texture);

            public UniTask<Scene> LoadSceneAsync(
                ESAssetIdentity id,
                LoadSceneMode mode,
                CancellationToken cancellationToken)
                => UniTask.FromResult(default(Scene));
        }

        [UnityTest]
        public IEnumerator ResourceProviderTransition_RetiresOldLeaseAndUsesNewGeneration()
        {
            RequireIsolatedRuntimeEnvironment();
            var runtime = new ESDynamicAtlasRuntime();
            var firstTexture = CreateSolidTexture(new Color32(240, 24, 24, 255));
            var secondTexture = CreateSolidTexture(new Color32(24, 64, 240, 255));
            var firstMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            var secondMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            var firstLoader = new ESRuntimeAssetLoader(firstMap, null,
                ESRuntimeRetryPolicy.Default, new DirectProvider(firstTexture));
            var secondLoader = new ESRuntimeAssetLoader(secondMap, null,
                ESRuntimeRetryPolicy.Default, new DirectProvider(secondTexture));
            ESDynamicAtlasLease oldLease = default;
            ESDynamicAtlasLease newLease = default;

            ESAssets.RuntimeBackendTransitionStarting += runtime.HandleProviderTransitionStarting;
            ESAssets.RuntimeBackendRebuilt += runtime.HandleProviderRebuilt;
            try
            {
                ESAssets.BeginProviderTransition();
                ESAssets.AttachRuntimeBackend(firstLoader);
                ESAssets.EndProviderTransition();
                Assert.That(ESAssets.IsReady, Is.True);

                var refer = new ESAssetReferTexture2D();
                refer.InitializeGeneratedReference(
                    "fake-provider-guid", 0, ESAssetReferKind.Texture2D, 0, null);
                var domain = new ESDynamicAtlasDomainKey("test.provider.transition");
                var content = new ESDynamicAtlasContentKey("avatar:provider-user", "revision-a");
                var request = new ESDynamicAtlasRequest
                {
                    padding = 0,
                    colorSpace = ESDynamicAtlasColorSpace.Linear,
                    alphaMode = ESDynamicAtlasAlphaMode.Straight,
                    filterMode = FilterMode.Point
                };
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 2,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                Task<ESDynamicAtlasLease> firstPending = runtime.AcquireAsync(
                    domain, content, refer, request, default).AsTask();
                for (int frame = 0; frame < 180 && !firstPending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(firstPending.IsCompleted && !firstPending.IsFaulted, Is.True,
                    firstPending.Exception == null ? "Provider 第一代动态图集上传失败。" : firstPending.Exception.ToString());
                oldLease = firstPending.GetAwaiter().GetResult();
                Assert.That(oldLease.TryResolve(out _), Is.True);

                ESAssets.BeginProviderTransition();
                ESAssets.ResetScopesForProviderTransition();
                Assert.That(oldLease.TryResolve(out _), Is.True,
                    "Provider 切换期间，已完成旧图应继续作为 Retired 画面显示。 ");
                Assert.That(oldLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Retired),
                    "Provider 切换期间旧 Lease 必须处于 Retired 可观察状态。 ");

                ESAssets.AttachRuntimeBackend(secondLoader);
                ESAssets.EndProviderTransition();
                Assert.That(ESAssets.IsReady, Is.True);

                var secondRefer = new ESAssetReferTexture2D();
                secondRefer.InitializeGeneratedReference(
                    "fake-provider-guid", 0, ESAssetReferKind.Texture2D, 0, null);
                Task<ESDynamicAtlasLease> secondPending = runtime.AcquireAsync(
                    domain, content, secondRefer, request, default).AsTask();
                for (int frame = 0; frame < 180 && !secondPending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(secondPending.IsCompleted && !secondPending.IsFaulted, Is.True,
                    secondPending.Exception == null ? "Provider 第二代动态图集上传失败。" : secondPending.Exception.ToString());
                newLease = secondPending.GetAwaiter().GetResult();
                Assert.That(newLease.TryResolve(out _), Is.True);

                ESDynamicAtlasSnapshot snapshot = runtime.CreateSnapshot();
                Assert.That(snapshot.retiredCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(snapshot.readyCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(snapshot.entries.Exists(entry => entry.providerGeneration >= 1), Is.True);
                Assert.That(oldLease.TryResolve(out _), Is.True,
                    "新 Provider 就绪后，旧 Retired Lease 的兼容显示契约仍应成立。 ");
                Assert.That(oldLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Retired),
                    "新 Provider 就绪后旧 Lease 仍应保持 Retired 兼容状态。 ");
            }
            finally
            {
                oldLease.Dispose();
                newLease.Dispose();
                ESAssets.BeginProviderTransition();
                ESAssets.ResetScopesForProviderTransition();
                ESAssets.DetachRuntimeBackend(secondLoader);
                ESAssets.EndProviderTransition();
                ESAssets.RuntimeBackendTransitionStarting -= runtime.HandleProviderTransitionStarting;
                ESAssets.RuntimeBackendRebuilt -= runtime.HandleProviderRebuilt;
                runtime.Dispose();
                secondLoader.Dispose();
                firstLoader.Dispose();
                Object.Destroy(firstMap);
                Object.Destroy(secondMap);
                Object.Destroy(firstTexture);
                Object.Destroy(secondTexture);
            }
        }

        [UnityTest]
        public IEnumerator ResourcePageLoss_KeepsLeasePlacementAndRecoversInPlace()
        {
            RequireIsolatedRuntimeEnvironment();
            var runtime = new ESDynamicAtlasRuntime
            {
                ForceAsyncGpuReadbackCompletionForTests = true
            };
            var texture = CreateSolidTexture(new Color32(240, 24, 24, 255));
            var map = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            var loader = new ESRuntimeAssetLoader(map, null,
                ESRuntimeRetryPolicy.Default, new DirectProvider(texture));
            ESDynamicAtlasLease lease = default;
            ESDynamicAtlasResolved originalResolved = default;
            int originalPageId = 0;
            int originalPlacementRevision = 0;

            ESAssets.RuntimeBackendTransitionStarting += runtime.HandleProviderTransitionStarting;
            ESAssets.RuntimeBackendRebuilt += runtime.HandleProviderRebuilt;
            try
            {
                ESAssets.BeginProviderTransition();
                ESAssets.AttachRuntimeBackend(loader);
                ESAssets.EndProviderTransition();
                Assert.That(ESAssets.IsReady, Is.True);

                var domain = new ESDynamicAtlasDomainKey("test.provider.page-loss-recover");
                runtime.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 1,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                var refer = new ESAssetReferTexture2D();
                refer.InitializeGeneratedReference(
                    "fake-provider-page-loss-guid", 0, ESAssetReferKind.Texture2D, 0, null);
                var content = new ESDynamicAtlasContentKey("avatar:page-loss-recover", "v1");
                Task<ESDynamicAtlasLease> pending = runtime.AcquireAsync(
                    domain, content, refer, ESDynamicAtlasRequest.Default, default).AsTask();
                for (int frame = 0; frame < 180 && !pending.IsCompleted; frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(pending.IsCompleted && !pending.IsFaulted, Is.True,
                    pending.Exception == null ? "Page Lost 恢复测试首代上传失败。" : pending.Exception.ToString());
                lease = pending.GetAwaiter().GetResult();
                Assert.That(lease.TryResolve(out originalResolved), Is.True);
                ESDynamicAtlasSnapshot originalSnapshot = runtime.CreateSnapshot();
                ESDynamicAtlasEntrySnapshot originalEntry = originalSnapshot.entries.Find(
                    entry => entry.content.Equals(content));
                Assert.That(originalEntry, Is.Not.Null);
                originalPageId = originalEntry.pageId;
                originalPlacementRevision = originalResolved.placementRevision;

                ((RenderTexture)originalResolved.texture).Release();
                Assert.That(lease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Lost),
                    "Page 释放后、Tick 处理前，旧 Lease 应进入 Lost 可观察状态。 ");
                runtime.Tick();
                yield return null;

                Assert.That(lease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Recovering),
                    "可重载 Entry 的 Page Lost 恢复期间，旧 Lease 应处于 Recovering 可观察状态。 ");

                for (int frame = 0; frame < 180 && !lease.TryResolve(out _); frame++)
                {
                    runtime.Tick();
                    yield return null;
                }

                Assert.That(lease.TryResolve(out ESDynamicAtlasResolved recoveredResolved), Is.True,
                    "可重载且仍持有 Lease 的 Entry 应在 Page Lost 后原位恢复。 ");
                Assert.That(recoveredResolved.uvRect, Is.EqualTo(originalResolved.uvRect),
                    "原位恢复不得改变 UV/placement。 ");
                Assert.That(recoveredResolved.placementRevision, Is.EqualTo(originalPlacementRevision),
                    "原位恢复不应通过重新分配改变 placement revision。 ");
                ESDynamicAtlasSnapshot recoveredSnapshot = runtime.CreateSnapshot();
                ESDynamicAtlasEntrySnapshot recoveredEntry = recoveredSnapshot.entries.Find(
                    entry => entry.content.Equals(content));
                Assert.That(recoveredEntry, Is.Not.Null);
                Assert.That(recoveredEntry.pageId, Is.EqualTo(originalPageId),
                    "可重载 Entry 必须保留原 Page 槽位。 ");
            }
            finally
            {
                lease.Dispose();
                ESAssets.BeginProviderTransition();
                ESAssets.ResetScopesForProviderTransition();
                ESAssets.DetachRuntimeBackend(loader);
                ESAssets.EndProviderTransition();
                ESAssets.RuntimeBackendTransitionStarting -= runtime.HandleProviderTransitionStarting;
                ESAssets.RuntimeBackendRebuilt -= runtime.HandleProviderRebuilt;
                runtime.Dispose();
                loader.Dispose();
                Object.Destroy(map);
                Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator Graphic_ManualBindingDoesNotAutoRefreshOnProviderRebuilt()
        {
            RequireIsolatedRuntimeEnvironment();
            GameObject managerObject = null;
            bool createdManager = ESGameManager.Instance == null;
            if (createdManager)
            {
                managerObject = new GameObject("ES Dynamic Atlas Manual Binding Manager",
                    typeof(ESGameManager));
            }

            var canvasObject = new GameObject("ES Dynamic Atlas Manual Refresh Canvas",
                typeof(Canvas));
            var gameObject = new GameObject("ES Dynamic Atlas Manual Refresh Test",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(ESDynamicAtlasGraphic));
            var firstTexture = CreateSolidTexture(new Color32(240, 24, 24, 255));
            var secondTexture = CreateSolidTexture(new Color32(24, 64, 240, 255));
            var firstMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            var secondMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            var firstLoader = new ESRuntimeAssetLoader(firstMap, null,
                ESRuntimeRetryPolicy.Default, new DirectProvider(firstTexture));
            var secondLoader = new ESRuntimeAssetLoader(secondMap, null,
                ESRuntimeRetryPolicy.Default, new DirectProvider(secondTexture));
            ESDynamicAtlasLease oldLease = default;
            ESDynamicAtlasLease newLease = default;
            ESDynamicAtlasModule module = null;
            try
            {
                module = ESGameManager.GetOrCreateModule<ESDynamicAtlasModule>();
                Assert.That(module, Is.Not.Null,
                    "Graphic 手动绑定测试必须创建隔离的 DynamicAtlas 模块。 ");

                ESAssets.BeginProviderTransition();
                ESAssets.AttachRuntimeBackend(firstLoader);
                ESAssets.EndProviderTransition();
                Assert.That(ESAssets.IsReady, Is.True);

                var domain = new ESDynamicAtlasDomainKey("test.manual-binding");
                ESDynamicAtlas.ConfigureDomain(domain, new ESDynamicAtlasDomainPolicy
                {
                    pageSize = 64,
                    maxPages = 2,
                    maxUploadsPerFrame = 1,
                    maxUploadPixelsPerFrame = 64 * 64,
                    unusedEntryKeepAliveSeconds = 0f
                });

                gameObject.transform.SetParent(canvasObject.transform, false);
                ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                SetPrivateField(graphic, "autoAcquire", false);
                SetPrivateField(graphic, "domainPreset", ESDynamicAtlasDomainPreset.Custom);
                SetPrivateField(graphic, "customDomainKey", domain.value);
                InvokePrivate(graphic, "OnEnable");

                var firstRefer = new ESAssetReferTexture2D();
                firstRefer.InitializeGeneratedReference(
                    "fake-manual-binding-guid-v1", 0, ESAssetReferKind.Texture2D, 0, null);
                Task firstPending = graphic.SetAsync(firstRefer, "v1").AsTask();
                for (int frame = 0; frame < 180 && !firstPending.IsCompleted; frame++)
                {
                    InvokePrivate(module, "Update");
                    yield return null;
                }

                Assert.That(firstPending.IsCompleted && !firstPending.IsFaulted, Is.True,
                    "Graphic 真实 SetAsync 首代 Provider 绑定失败。 ");
                oldLease = GetPrivateField<ESDynamicAtlasLease>(graphic, "lease");
                Assert.That(oldLease.TryResolve(out ESDynamicAtlasResolved oldResolved), Is.True,
                    "Graphic 手动 SetAsync 后必须持有可解析 Lease。 ");
                Assert.That(graphic.HasContent, Is.True);

                ESDynamicAtlasSnapshot firstSnapshot = module.CreateSnapshot();
                ESDynamicAtlasEntrySnapshot firstEntry = firstSnapshot.entries.Find(
                    entry => entry.pageTexture == oldResolved.texture
                             && entry.placementRevision == oldResolved.placementRevision);
                Assert.That(firstSnapshot.entries.Exists(
                        entry => entry.pageTexture == oldResolved.texture
                                 && entry.placementRevision == oldResolved.placementRevision),
                    Is.True,
                    "真实手动绑定后快照必须能定位到对应 Page/UV 条目。 ");

                int revisionBeforeRebuild = GetPrivateField<int>(graphic, "requestRevision");

                ESAssets.BeginProviderTransition();
                ESAssets.ResetScopesForProviderTransition();
                Assert.That(oldLease.TryResolve(out ESDynamicAtlasResolved retiredDuringTransition), Is.True,
                    "Provider Transition 期间，旧手动 Lease 应继续作为 Retired 画面解析。 ");
                Assert.That(oldLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Retired),
                    "Provider Transition 期间旧手动 Lease 必须处于 Retired。 ");

                ESAssets.AttachRuntimeBackend(secondLoader);
                ESAssets.EndProviderTransition();
                yield return null;

                Assert.That(GetPrivateField<int>(graphic, "requestRevision"),
                    Is.EqualTo(revisionBeforeRebuild),
                    "手动绑定/autoAcquire=false 的 Graphic 在 Provider 重建后不得隐式发起第二次自动加载。 ");
                Assert.That(oldLease.TryResolve(out ESDynamicAtlasResolved retiredAfterRebuild), Is.True,
                    "Provider 重建后旧手动 Lease 仍应保持 Retired 兼容解析。 ");
                Assert.That(oldLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Retired));
                Assert.That(retiredAfterRebuild.uvRect, Is.EqualTo(oldResolved.uvRect),
                    "Provider 重建不得改变旧手动 Lease 的 UV。 ");
                Assert.That(retiredAfterRebuild.placementRevision, Is.EqualTo(oldResolved.placementRevision),
                    "Provider 重建不得改变旧手动 Lease 的 placement。 ");

                ESDynamicAtlasSnapshot afterRebuildSnapshot = module.CreateSnapshot();
                ESDynamicAtlasEntrySnapshot retiredEntry = afterRebuildSnapshot.entries.Find(
                    entry => entry.pageTexture == oldResolved.texture
                             && entry.placementRevision == oldResolved.placementRevision);
                Assert.That(afterRebuildSnapshot.entries.Exists(
                        entry => entry.pageTexture == oldResolved.texture
                                 && entry.placementRevision == oldResolved.placementRevision),
                    Is.True,
                    "旧手动 Lease 对应的 Page 在 Provider 重建后仍应存在。 ");
                Assert.That(retiredEntry.pageId, Is.EqualTo(firstEntry.pageId),
                    "旧手动 Lease 的 Page 在 Provider 重建后必须保持 Retired 页面。 ");
                Assert.That(retiredEntry.state, Is.EqualTo(ESDynamicAtlasEntryState.Retired));

                var secondRefer = new ESAssetReferTexture2D();
                secondRefer.InitializeGeneratedReference(
                    "fake-manual-binding-guid-v2", 0, ESAssetReferKind.Texture2D, 0, null);
                Task secondPending = graphic.SetAsync(secondRefer, "v2").AsTask();
                for (int frame = 0; frame < 180 && !secondPending.IsCompleted; frame++)
                {
                    InvokePrivate(module, "Update");
                    yield return null;
                }

                Assert.That(secondPending.IsCompleted && !secondPending.IsFaulted, Is.True,
                    "显式再次 SetAsync 后必须切换到新 Provider 内容。 ");
                newLease = GetPrivateField<ESDynamicAtlasLease>(graphic, "lease");
                Assert.That(newLease.TryResolve(out ESDynamicAtlasResolved newResolved), Is.True,
                    "显式再次 SetAsync 后 Graphic 必须持有新可解析 Lease。 ");
                Assert.That(newLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Ready));
                Assert.That(oldLease.State, Is.EqualTo(ESDynamicAtlasLeaseState.Retired),
                    "显式再次绑定后旧 Lease 必须继续处于 Retired。 ");
                Assert.That(GetPrivateField<string>(graphic, "status"),
                    Is.EqualTo("已完成：正在使用动态图集"));

                Assert.That(newResolved.texture, Is.Not.SameAs(oldResolved.texture),
                    "显式再次绑定必须使用新 Provider 的 Page 内容。 ");
            }
            finally
            {
                if (gameObject != null)
                {
                    ESDynamicAtlasGraphic graphic = gameObject.GetComponent<ESDynamicAtlasGraphic>();
                    if (graphic != null)
                        InvokePrivate(graphic, "OnDisable");
                }
                oldLease.Dispose();
                newLease.Dispose();
                ESAssets.BeginProviderTransition();
                ESAssets.ResetScopesForProviderTransition();
                ESAssets.DetachRuntimeBackend(secondLoader);
                ESAssets.EndProviderTransition();
                if (createdManager && managerObject != null)
                    Object.DestroyImmediate(managerObject);
                UnityEngine.Object.Destroy(gameObject);
                UnityEngine.Object.Destroy(canvasObject);
                firstLoader.Dispose();
                secondLoader.Dispose();
                Object.Destroy(firstMap);
                Object.Destroy(secondMap);
                Object.Destroy(firstTexture);
                Object.Destroy(secondTexture);
            }
        }

        private static Texture2D CreateSolidTexture(Color32 color)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[16];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = color;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static void RequireIsolatedRuntimeEnvironment()
        {
            if (ESGameManager.Instance != null || ESAssets.IsReady)
            {
                Assert.Ignore(
                    "该测试会临时替换全局 ESAssets 后端，只能在无 GameManager、无活动 Provider 的隔离环境运行。 ");
            }
        }

        private static void InvokePrivate(object target, string methodName)
        {
            System.Reflection.MethodInfo method = target.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "缺少测试方法：" + methodName);
            method.Invoke(target, null);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "缺少测试字段：" + fieldName);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "缺少测试字段：" + fieldName);
            return (T)field.GetValue(target);
        }
    }
}
