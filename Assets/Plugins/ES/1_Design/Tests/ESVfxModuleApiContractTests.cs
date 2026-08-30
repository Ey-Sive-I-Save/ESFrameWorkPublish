using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESVfxModuleApiContractTests
    {
        [Test]
        public void VfxPublicApiContainsAudioParityConvenienceEntrypoints()
        {
            var methods = new HashSet<string>(typeof(ESVfxModule).GetMethods()
                .Where(method => method.IsPublic && !method.IsStatic)
                .Select(method => method.Name), StringComparer.Ordinal);

            Assert.That(methods, Does.Contain("PlayOneShot"));
            Assert.That(methods, Does.Contain("PlayAttached"));
            Assert.That(methods, Does.Contain("PlayAtPosition"));
            Assert.That(methods, Does.Contain("PlayLoop"));
            Assert.That(methods, Does.Contain("StopAll"));
            Assert.That(methods, Does.Contain("StopCategory"));
            Assert.That(methods, Does.Contain("TryGetVfxStatus"));
            Assert.That(methods, Does.Contain("CopyDiagnostics"));
            Assert.That(methods, Does.Contain("CopyVfxDiagnostics"));
            Assert.That(methods, Does.Contain("CopyRecentFailures"));
            Assert.That(typeof(ESVfxDiagnostic).GetField("PoolVersion"), Is.Not.Null);
            Assert.That(typeof(ESVfxDiagnostic).GetField("Category"), Is.Not.Null);
            Assert.That(typeof(ESVfxDiagnostic).GetField("Loop"), Is.Not.Null);
            Assert.That(typeof(ESVfxDiagnostic).GetField("Priority"), Is.Not.Null);
            Assert.That(typeof(ESVfxModule).GetMethods().Count(method => method.Name == "Play"), Is.GreaterThanOrEqualTo(2));
            Assert.That(typeof(ESVfxModule).GetMethods().Any(method => method.Name == "PlayOneShot" && method.GetParameters().Length >= 2 && method.GetParameters()[0].ParameterType == typeof(ESAssetReferPrefabConfigKey)), Is.True);
            Assert.That(typeof(ESVfxModule).GetMethods().Any(method => method.Name == "PlayAttached" && method.GetParameters().Length >= 3 && method.GetParameters()[0].ParameterType == typeof(ESAssetReferPrefabConfigKey)), Is.True);
            Assert.That(typeof(ESVfxModule).GetMethods().Any(method => method.Name == "PlayAtPosition" && method.GetParameters().Length >= 3 && method.GetParameters()[0].ParameterType == typeof(ESAssetReferPrefabConfigKey)), Is.True);
            Assert.That(typeof(ESVfxModule).GetMethods().Any(method => method.Name == "PlayLoop" && method.GetParameters().Length >= 3 && method.GetParameters()[0].ParameterType == typeof(ESAssetReferPrefabConfigKey)), Is.True);
        }

        [Test]
        public void VfxSecondTrackRequiresScopedPrefabLeaseAndValidatedPolicy()
        {
            var config = new ESVfxPrefabPlayConfig();
            Assert.That(config.TryValidate(out string error), Is.True, error);

            config.budgetKey = string.Empty;
            Assert.That(config.TryValidate(out _), Is.False);

            config.budgetKey = "tests.vfx.direct";
            config.loop = true;
            Assert.That(config.TryValidate(out _), Is.False);
        }

        [Test]
        public void VfxDiagnosticLabelsCoverObservableLifecycle()
        {
            Assert.That(ESVfxDiagnosticText.GetChineseState(ESVfxState.PendingLoad), Is.EqualTo("等待资源加载"));
            Assert.That(ESVfxDiagnosticText.GetChineseState(ESVfxState.Playing), Is.EqualTo("正在播放"));
            Assert.That(ESVfxDiagnosticText.GetChineseState(ESVfxState.Ended), Is.EqualTo("已结束"));
            Assert.That(ESVfxDiagnosticText.GetChineseEndReason(ESVfxEndReason.NaturalEnd), Is.EqualTo("自然结束"));
            Assert.That(ESVfxDiagnosticText.GetChineseEndReason(ESVfxEndReason.OwnerDespawned), Is.EqualTo("Owner 已回收到对象池"));
        }

        [Test]
        public void VfxFailureContractIncludesMissingOwner()
        {
            Assert.That(ESVfxDiagnostics.DescribeFailure(ESVfxFailureCode.MissingOwner), Is.EqualTo("附着播放缺少有效 Transform"));
        }

        [Test]
        public void VfxOwnerPoolGuardIncludesSpawnStateAndGeneration()
        {
            string sourcePath = Path.Combine("Assets", "Scripts", "ESLogic", "Runtime", "GameManager", "Modules", "Runtime", "MODULE_ESVfxModule.cs");
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Contain("!item.ownerPool.IsSpawned"));
            Assert.That(source, Does.Contain("item.ownerPool.Version != item.ownerPoolVersion"));
            Assert.That(source, Does.Contain("pool.PushToPool(instance, acquiredPoolVersion)"));
            Assert.That(source, Does.Contain("public override void OnDestroy()"));
            Assert.That(source, Does.Contain("StopAll(ESVfxEndReason.ModuleDisabled)"));
            Assert.That(source, Does.Contain("ESAssets.RuntimeBackendTransitionStarting += OnRuntimeBackendTransitionStarting"));
            Assert.That(source, Does.Contain("ESAssets.RuntimeBackendTransitionStarting -= OnRuntimeBackendTransitionStarting"));
            Assert.That(source, Does.Contain("ESAssets.ActivePlanAssetOwnershipEnding += OnActivePlanAssetOwnershipEnding"));
            Assert.That(source, Does.Contain("ESAssets.ActivePlanAssetOwnershipEnding -= OnActivePlanAssetOwnershipEnding"));
            Assert.That(source, Does.Contain("public override void OnDestroy()"));
            Assert.That(source, Does.Contain("ESVfxEndReason.ResourceOwnerReleased"));
        }

        [Test]
        public void AudioVfxSharedPlaybackSurfaceRemainsNameAligned()
        {
            var audioNames = new HashSet<string>(typeof(ESAudioModule).GetMethods()
                .Where(method => method.IsPublic && !method.IsStatic)
                .Select(method => method.Name), StringComparer.Ordinal);
            var vfxNames = new HashSet<string>(typeof(ESVfxModule).GetMethods()
                .Where(method => method.IsPublic && !method.IsStatic)
                .Select(method => method.Name), StringComparer.Ordinal);

            string[] shared = { "PlayOneShot", "PlayAttached", "PlayAtPosition", "PlayLoop", "Stop", "StopAll", "StopCategory", "CopyRecentFailures" };
            for (int i = 0; i < shared.Length; i++)
            {
                Assert.That(audioNames, Does.Contain(shared[i]), "Audio missing shared API: " + shared[i]);
                Assert.That(vfxNames, Does.Contain(shared[i]), "VFX missing shared API: " + shared[i]);
            }
            Assert.That(audioNames, Does.Contain("CopyVoiceDiagnostics"));
            Assert.That(vfxNames, Does.Contain("CopyVfxDiagnostics"));
        }

        [Test]
        public void AudioVfxStatusAndDiagnosticsExposeStableLifecycleData()
        {
            string[] statusFields = { "Handle", "State", "EndReason", "FailureCode" };
            for (int i = 0; i < statusFields.Length; i++)
            {
                Assert.That(typeof(ESAudioVoiceStatus).GetField(statusFields[i]), Is.Not.Null,
                    "Audio status field missing: " + statusFields[i]);
                Assert.That(typeof(ESVfxStatus).GetField(statusFields[i]), Is.Not.Null,
                    "VFX status field missing: " + statusFields[i]);
            }

            string[] diagnosticFields = { "Category", "Loop", "Priority" };
            for (int i = 0; i < diagnosticFields.Length; i++)
                Assert.That(typeof(ESVfxDiagnostic).GetField(diagnosticFields[i]), Is.Not.Null,
                    "VFX diagnostic field missing: " + diagnosticFields[i]);

            Assert.That(typeof(ESAudioVoiceDiagnostic).GetField("Category"), Is.Not.Null);
            Assert.That(typeof(ESAudioVoiceDiagnostic).GetField("IsLoop"), Is.Not.Null);
            Assert.That(typeof(ESAudioVoiceDiagnostic).GetField("Priority"), Is.Not.Null);
        }

        [Test]
        public void VfxOperationUsesExplicitAttachedOrPositionApi()
        {
            string sourcePath = Path.Combine("Assets", "Scripts", "ESLogic", "Runtime", "Operation", "Operations", "09_GameObjectVFX", "OpGameObjectVfx.cs");
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Contain("ESGameManager.Vfx.PlayAttached(vfxKey"));
            Assert.That(source, Does.Contain("ESGameManager.Vfx.PlayAtPosition(vfxKey"));
            Assert.That(source, Does.Not.Contain("ESGameManager.Vfx.Play(vfxKey, new ESVfxPlayRequest"));
        }

        [Test]
        public void VfxPrefabKeyPathPinsAndReleasesConfigPayloadLease()
        {
            string sourcePath = Path.Combine("Assets", "Scripts", "ESLogic", "Runtime", "GameManager", "Modules", "Runtime", "MODULE_ESVfxModule.cs");
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Contain("RuntimePrefabAssets.TryAcquireReady(prefabKey"));
            Assert.That(source, Does.Contain("prefabLease?.Dispose()"));
            Assert.That(source, Does.Contain("prefabLease = prefabLease"));
            Assert.That(source, Does.Contain("item.prefabLease?.Dispose()"));
        }

        [Test]
        public void VfxHandleCountersWrapWithoutProducingZero()
        {
            string sourcePath = Path.Combine("Assets", "Scripts", "ESLogic", "Runtime", "GameManager", "Modules", "Runtime", "MODULE_ESVfxModule.cs");
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Contain("private int NextVfxId()"));
            Assert.That(source, Does.Contain("private int NextVfxGeneration()"));
            Assert.That(source, Does.Contain("nextId == int.MaxValue ? 1 : nextId + 1"));
            Assert.That(source, Does.Contain("nextGeneration == int.MaxValue ? 1 : nextGeneration + 1"));
            Assert.That(source, Does.Not.Contain("new ESVfxHandle(nextId++, nextGeneration++)"));
        }

        [Test]
        public void VfxHandleCountersWrapBehaviorallyAtMaxValue()
        {
            ESVfxModule module = new ESVfxModule();
            FieldInfo idField = typeof(ESVfxModule).GetField("nextId", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo generationField = typeof(ESVfxModule).GetField("nextGeneration", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo nextId = typeof(ESVfxModule).GetMethod("NextVfxId", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo nextGeneration = typeof(ESVfxModule).GetMethod("NextVfxGeneration", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(idField, Is.Not.Null);
            Assert.That(generationField, Is.Not.Null);
            Assert.That(nextId, Is.Not.Null);
            Assert.That(nextGeneration, Is.Not.Null);

            idField.SetValue(module, int.MaxValue);
            generationField.SetValue(module, int.MaxValue);
            Assert.That((int)nextId.Invoke(module, null), Is.EqualTo(int.MaxValue));
            Assert.That((int)nextId.Invoke(module, null), Is.EqualTo(1));
            Assert.That((int)nextGeneration.Invoke(module, null), Is.EqualTo(int.MaxValue));
            Assert.That((int)nextGeneration.Invoke(module, null), Is.EqualTo(1));
        }

        [Test]
        public void VfxUsageDocumentationNamesCurrentPublicEntrypoints()
        {
            string source = File.ReadAllText(Path.Combine(
                "Documentation", "AIKnowledge", "entries", "audio-vfx-runtime.md"));
            string[] documentedCalls = { "PlayAttached(vfxKey", "TryGetVfxStatus(handle", "Stop(handle)", "CopyRecentFailures(failures" };
            for (int i = 0; i < documentedCalls.Length; i++)
                Assert.That(source, Does.Contain(documentedCalls[i]), "Documentation call drifted: " + documentedCalls[i]);
        }
    }
}
