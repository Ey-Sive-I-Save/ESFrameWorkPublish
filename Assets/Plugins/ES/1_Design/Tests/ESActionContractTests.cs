using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESActionContractTests
    {
        private readonly List<ScriptableObject> created = new List<ScriptableObject>();
        private ESActionConfigKeyTable localTable;

        [SetUp]
        public void SetUp()
        {
            localTable = new ESActionConfigKeyTable(8);
            ESActionPoolLifecycleDiagnostics.Clear();
            ESActionPresentationMappingTable.Clear();
            ESActionGameCoreTable.Table.BeginBuild(true);
        }

        [TearDown]
        public void TearDown()
        {
            ESActionGameCoreTable.Table.EndBuild();

            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    UnityEngine.Object.DestroyImmediate(created[i]);
            }

            created.Clear();
            localTable = null;
        }

        [Test]
        public void InjectGameCoreTable_ResolvesThreePhaseTemplate()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "test.attack",
                Phase(ESActionPhaseKind.Startup, 0.1f),
                Phase(ESActionPhaseKind.Active, 0.15f),
                Phase(ESActionPhaseKind.Recovery, 0.2f));

            ESActionGameCoreTable.Inject(info);

            Assert.That(ESActionGameCoreTable.Table.TryGet(info.actionKey, out ESActionRuntimeData data), Is.True);
            Assert.That(data.phases.Count, Is.EqualTo(3));
            Assert.That(data.comboTransitions, Is.Not.Empty);
            Assert.That(data.presentationBindings[0].skillTrackKey.IsConfigured, Is.True);
        }

        [Test]
        public void DuplicateKey_IsHardFailure()
        {
            ActionTemplateDataInfo first = CreateInfo(
                "duplicate.attack",
                Phase(ESActionPhaseKind.Startup),
                Phase(ESActionPhaseKind.Active),
                Phase(ESActionPhaseKind.Recovery));
            ActionTemplateDataInfo second = CreateInfo(
                "duplicate.attack",
                Phase(ESActionPhaseKind.Startup),
                Phase(ESActionPhaseKind.Active),
                Phase(ESActionPhaseKind.Recovery));

            ESActionGameCoreTable.Inject(first);

            Assert.Throws<InvalidOperationException>(() => ESActionGameCoreTable.Inject(second));
        }

        [Test]
        public void EmptyKey_IsHardFailure()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "empty.attack",
                Phase(ESActionPhaseKind.Startup),
                Phase(ESActionPhaseKind.Active),
                Phase(ESActionPhaseKind.Recovery));
            info.actionKey = new ESActionConfigKey();

            Assert.Throws<InvalidOperationException>(() => ESActionGameCoreTable.Inject(info));
        }

        [Test]
        public void InvalidPhase_IsHardFailure()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "invalid.attack",
                Phase(ESActionPhaseKind.Startup),
                Phase(ESActionPhaseKind.Active),
                Phase(ESActionPhaseKind.Recovery));
            info.phases[1].duration = 0f;

            Assert.Throws<InvalidOperationException>(() => ESActionGameCoreTable.Inject(info));
        }

        [Test]
        public void SkillTrackBinding_WithoutStableKey_IsHardFailure()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "invalidtrack.attack",
                Phase(ESActionPhaseKind.Startup),
                Phase(ESActionPhaseKind.Active),
                Phase(ESActionPhaseKind.Recovery));
            info.presentationBindings[0].owner = ESActionPresentationOwner.SkillTrack;
            info.presentationBindings[0].skillTrackKey = new ESSkillTrackConfigKey();

            Assert.Throws<InvalidOperationException>(() => ESActionGameCoreTable.Inject(info));
        }

        [Test]
        public void Runtime_ResolvesTemplateAndRejectsWrongHandle()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "runtime.attack",
                Phase(ESActionPhaseKind.Startup, 0.1f),
                Phase(ESActionPhaseKind.Active, 0.15f),
                Phase(ESActionPhaseKind.Recovery, 0.2f));
            InjectLocal(info);

            var events = new ESActionEventChannel();
            var eventsSeen = new List<ESActionEvent>();
            events.Published += eventsSeen.Add;

            var runtime = new ESActionRuntime(localTable, events);
            var intent = new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 1, null);

            Assert.That(runtime.TrySubmit(intent, out string submitError), Is.True, submitError);
            Assert.That(
                eventsSeen.Exists(evt => evt.kind == ESActionEventKind.ActionStarted && evt.sourcePulseId == 1),
                Is.True);
            ESActionRuntimeHandle handle = runtime.CurrentHandle;
            Assert.That(handle.IsValid, Is.True);
            Assert.That(runtime.TryResolveHit(handle, null, out _), Is.False);

            runtime.Tick(0.11f);
            runtime.Tick(0.05f);
            Assert.That(runtime.TryResolveHit(handle, null, out ESActionHitResult hit), Is.True);
            Assert.That(hit.damageMultiplier, Is.EqualTo(1f));

            runtime.ResetForLifecycle();
            Assert.That(runtime.TryResolveHit(handle, null, out _), Is.False);
            Assert.That(eventsSeen.Exists(evt => evt.kind == ESActionEventKind.ActionStarted), Is.True);
            Assert.That(eventsSeen.Exists(evt => evt.kind == ESActionEventKind.HitResolved), Is.True);
        }

        [Test]
        public void Runtime_WrongCatalogIsRejected()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "othercatalog.attack",
                Phase(ESActionPhaseKind.Startup),
                Phase(ESActionPhaseKind.Active),
                Phase(ESActionPhaseKind.Recovery));
            var otherTable = new ESActionConfigKeyTable(4);
            InjectLocal(info, otherTable);

            var runtime = new ESActionRuntime(localTable, new ESActionEventChannel());
            var intent = new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 1, null);

            Assert.That(runtime.TrySubmit(intent, out string error), Is.False);
            Assert.That(error, Does.Contain("未注册"));
        }

        [Test]
        public void Runtime_BuffersInsideWindowAndRejectsLateInput()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "buffer.attack",
                Phase(ESActionPhaseKind.Startup, 0.1f, 0.05f),
                Phase(ESActionPhaseKind.Active, 0.15f),
                Phase(ESActionPhaseKind.Recovery, 0.2f));
            InjectLocal(info);

            var runtime = new ESActionRuntime(localTable, new ESActionEventChannel());
            var first = new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 1, null);
            Assert.That(runtime.TrySubmit(first, out _), Is.True);

            runtime.Tick(0.03f);
            var buffered = new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 2, null);
            Assert.That(runtime.TrySubmit(buffered, out _), Is.True);
            Assert.That(runtime.HasBufferedIntent, Is.True);

            runtime.Tick(0.08f);
            var late = new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 3, null);
            Assert.That(runtime.TrySubmit(late, out string error), Is.False);
            Assert.That(error, Does.Contain("缓冲窗口"));
        }

        [Test]
        public void Runtime_ReplacingBufferedIntentReportsOldPulseAndStartsNewestPulse()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "buffer.replace.attack",
                Phase(ESActionPhaseKind.Startup, 0.1f, 0.08f),
                Phase(ESActionPhaseKind.Active, 0.1f),
                Phase(ESActionPhaseKind.Recovery, 0.1f));
            InjectLocal(info);

            var events = new ESActionEventChannel();
            var eventsSeen = new List<ESActionEvent>();
            events.Published += eventsSeen.Add;
            var runtime = new ESActionRuntime(localTable, events);

            Assert.That(
                runtime.TrySubmit(
                    new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 41, null),
                    out _,
                    out int firstReplacedPulse),
                Is.True);
            Assert.That(firstReplacedPulse, Is.Zero);

            runtime.Tick(0.03f);
            Assert.That(
                runtime.TrySubmit(
                    new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 42, null),
                    out _,
                    out int secondReplacedPulse),
                Is.True);
            Assert.That(secondReplacedPulse, Is.Zero);

            Assert.That(
                runtime.TrySubmit(
                    new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 43, null),
                    out _,
                    out int thirdReplacedPulse),
                Is.True);
            Assert.That(thirdReplacedPulse, Is.EqualTo(42));

            runtime.Tick(0.2f);
            runtime.Tick(0.2f);
            runtime.Tick(0.2f);

            Assert.That(
                eventsSeen.Exists(evt => evt.kind == ESActionEventKind.ActionStarted && evt.sourcePulseId == 41),
                Is.True);
            Assert.That(
                eventsSeen.Exists(evt => evt.kind == ESActionEventKind.ActionStarted && evt.sourcePulseId == 43),
                Is.True);
            Assert.That(
                eventsSeen.Exists(evt => evt.kind == ESActionEventKind.ActionStarted && evt.sourcePulseId == 42),
                Is.False);
        }

        [Test]
        public void Runtime_CancelRuleAllowsCancelInsideWindow()
        {
            ActionTemplateDataInfo info = CreateInfo(
                "cancel.attack",
                Phase(ESActionPhaseKind.Startup, 0.1f, 0.1f),
                Phase(ESActionPhaseKind.Active, 0.15f),
                Phase(ESActionPhaseKind.Recovery, 0.2f));
            info.cancelRules.Add(new ESActionCancelRuleData
            {
                sourcePhase = ESActionPhaseKind.Startup,
                targetCategory = ESActionCategory.Dodge,
                windowDuration = 0.1f,
            });
            InjectLocal(info);

            var runtime = new ESActionRuntime(localTable, new ESActionEventChannel());
            var intent = new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 1, null);
            Assert.That(runtime.TrySubmit(intent, out _), Is.True);

            runtime.Tick(0.03f);
            Assert.That(
                runtime.TrySubmit(
                    new ESActionIntent(info.actionKey, runtime.LifecycleGeneration, 2, null),
                    out string submitError),
                Is.True,
                submitError);
            Assert.That(runtime.HasBufferedIntent, Is.True);
            Assert.That(runtime.BufferedSourcePulseId, Is.EqualTo(2));

            Assert.That(runtime.TryCancel(ESActionCategory.Dodge, null, out string error), Is.True, error);
            Assert.That(runtime.IsRunning, Is.False);
            Assert.That(runtime.HasBufferedIntent, Is.False);
            Assert.That(runtime.BufferedSourcePulseId, Is.Zero);
        }

        [Test]
        public void HitstopRuntime_StoresAndConsumesPending()
        {
            var hitstop = new ESActionHitstopRuntime();
            hitstop.Request("action.1", 0.08f);
            hitstop.Request("action.2", 0.12f);

            Assert.That(hitstop.PendingSeconds, Is.EqualTo(0.12f));
            Assert.That(hitstop.ConsumePending(), Is.EqualTo(0.12f));
            Assert.That(hitstop.PendingSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void Bridge_PoolReEnableRebindsOwnerAndResolver()
        {
            var firstChannel = new ESActionEventChannel();
            var firstBridge = new ESActionPresentationBridge(
                firstChannel,
                null,
                null,
                null,
                () => null);
            Assert.That(firstBridge.HasOwnerDependencies, Is.True);

            firstBridge.Dispose();
            Assert.That(firstBridge.HasOwnerDependencies, Is.False);

            var secondChannel = new ESActionEventChannel();
            var secondBridge = new ESActionPresentationBridge(
                secondChannel,
                null,
                null,
                null,
                () => null);
            Assert.That(secondBridge.HasOwnerDependencies, Is.True);

            secondBridge.Dispose();
            Assert.That(secondBridge.HasOwnerDependencies, Is.False);
        }

        [Test]
        public void CombatModule_PoolReEnableRebuildsBridgeAndActionRuntime()
        {
            GameObject go = new GameObject("Pool Combat Entity");
            try
            {
                Entity entity = go.AddComponent<Entity>();
                entity.EnsureEntityStructure();
                EntityBasicDomain domain = entity.basicDomain;
                domain._Editor_RegisterAllButOnlyCreateRelationship(entity);
                entity.RegisterDomain(domain);

                var combat = new EntityBasicCombatModule();
                domain.TryAddModuleRuntime(combat);
                combat.TrySubmitAction(default);

                Assert.That(combat.HasActionRuntime, Is.True);
                Assert.That(combat.ActionPresentationBridge, Is.Not.Null);
                Assert.That(combat.ActionPresentationBridge.HasOwnerDependencies, Is.True);
                Assert.That(combat.ActionPresentationBridge.WeaponMountResolver, Is.Not.Null);

                domain.TryRemoveModuleRuntimeWithoutTypeMatch(combat);
                combat.Signal_IsActiveAndEnable = false;

                Assert.That(combat.ActionPresentationBridge, Is.Null);
                Assert.That(combat.HasActionRuntime, Is.False);

                domain.TryAddModuleRuntime(combat);
                combat.TrySubmitAction(default);

                Assert.That(combat.HasActionRuntime, Is.True);
                Assert.That(combat.ActionPresentationBridge, Is.Not.Null);
                Assert.That(combat.ActionPresentationBridge.HasOwnerDependencies, Is.True);
                Assert.That(combat.ActionPresentationBridge.WeaponMountResolver, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CombatModule_PoolEntityCallbacksDispatchOnceAndReleaseBridge()
        {
            GameObject go = new GameObject("Pool Entity Dispatch");
            try
            {
                Entity entity = go.AddComponent<Entity>();
                entity.EnsureEntityStructure();
                EntityBasicDomain domain = entity.basicDomain;
                domain._Editor_RegisterAllButOnlyCreateRelationship(entity);
                entity.RegisterDomain(domain);

                var combat = new EntityBasicCombatModule();
                domain.TryAddModuleRuntime(combat);

                entity.OnPoolSpawned();
                Assert.That(combat.PoolSpawnCount, Is.EqualTo(1));
                combat.TrySubmitAction(default);
                Assert.That(combat.HasActionRuntime, Is.True);
                Assert.That(combat, Is.Not.InstanceOf<IESGameObjectPoolLifecycle>());

                entity.OnPoolDespawned();
                Assert.That(combat.PoolDespawnCount, Is.EqualTo(1));
                Assert.That(combat.ActionPresentationBridge, Is.Null);

                entity.OnPoolSpawned();
                entity.OnPoolDespawned();
                Assert.That(combat.PoolSpawnCount, Is.EqualTo(2));
                Assert.That(combat.PoolDespawnCount, Is.EqualTo(2));
                Assert.That(combat.ActionPresentationBridge, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Entity_PoolDespawnReleasesCombatBridgeBeforeEntityCleanup()
        {
            GameObject go = new GameObject("Pool Entity Order");
            try
            {
                Entity entity = go.AddComponent<Entity>();
                entity.EnsureEntityStructure();
                EntityBasicDomain domain = entity.basicDomain;
                domain._Editor_RegisterAllButOnlyCreateRelationship(entity);
                entity.RegisterDomain(domain);

                var combat = new EntityBasicCombatModule();
                domain.TryAddModuleRuntime(combat);

                entity.OnPoolSpawned();
                ESActionPoolLifecycleDiagnostics.Clear();
                entity.OnPoolDespawned();

                Assert.That(combat.ActionPresentationBridge, Is.Null);
                Assert.That(
                    ESActionPoolLifecycleDiagnostics.Sequence,
                    Does.Contain("Combat.BridgeDispose"));
                AssertDespawnCleanupAfter("Combat.BridgeDispose");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Entity_PoolDespawnDispatchesInteractionOnceAndClearsCandidate()
        {
            GameObject go = new GameObject("Pool Interaction Entity");
            try
            {
                Entity entity = go.AddComponent<Entity>();
                entity.EnsureEntityStructure();
                EntityBasicDomain domain = entity.basicDomain;
                domain._Editor_RegisterAllButOnlyCreateRelationship(entity);
                entity.RegisterDomain(domain);

                var interaction = new EntityBasicInteractionModule
                {
                    autoDetect = false,
                };
                domain.TryAddModuleRuntime(interaction);

                entity.OnPoolSpawned();
                Assert.That(interaction.PoolSpawnCount, Is.EqualTo(1));
                Assert.That(interaction.currentCandidate, Is.Null);

                interaction.isInteracting = true;
                entity.OnPoolDespawned();
                Assert.That(interaction.PoolDespawnCount, Is.EqualTo(1));
                Assert.That(interaction.isInteracting, Is.False);
                Assert.That(interaction.currentCandidate, Is.Null);

                entity.OnPoolSpawned();
                entity.OnPoolDespawned();
                Assert.That(interaction.PoolSpawnCount, Is.EqualTo(2));
                Assert.That(interaction.PoolDespawnCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MappingPriority_ActionWeaponWinsOverActionAndGlobal()
        {
            ESActionPresentationMappingTable.Inject(new[]
            {
                new ESActionPresentationMappingEntry
                {
                    eventKind = ESActionEventKind.HitResolved,
                    channel = ESActionPresentationChannel.Hitstop,
                    owner = ESActionPresentationOwner.Direct,
                    hitstopSeconds = 0.03f,
                },
                new ESActionPresentationMappingEntry
                {
                    eventKind = ESActionEventKind.HitResolved,
                    channel = ESActionPresentationChannel.Hitstop,
                    owner = ESActionPresentationOwner.Direct,
                    actionKey = "melee.attack",
                    hitstopSeconds = 0.05f,
                },
                new ESActionPresentationMappingEntry
                {
                    eventKind = ESActionEventKind.HitResolved,
                    channel = ESActionPresentationChannel.Hitstop,
                    owner = ESActionPresentationOwner.Direct,
                    actionKey = "melee.attack",
                    weaponKey = "sword",
                    hitstopSeconds = 0.08f,
                },
            });

            var context = new ESActionEventContext(
                default,
                1,
                ESActionEventKind.HitResolved,
                ESActionPresentationChannel.Hitstop,
                "melee.attack",
                "sword");

            Assert.That(
                ESActionPresentationMappingTable.TryResolve(
                    context,
                    ESActionPresentationOwner.Direct,
                    out ESActionResolvedPresentationPayload payload,
                    out string error),
                Is.True,
                error);
            Assert.That(payload.audioCueKey.StringKey, Is.EqualTo("specific.hit"));
        }

        [Test]
        public void MappingActionStarted_AudioResolves()
        {
            ESActionPresentationMappingTable.Inject(new[]
            {
                new ESActionPresentationMappingEntry
                {
                    eventKind = ESActionEventKind.ActionStarted,
                    channel = ESActionPresentationChannel.Audio,
                    owner = ESActionPresentationOwner.Direct,
                    actionKey = "melee.attack",
                    audioCueKey = "swing.audio",
                },
            });

            var context = new ESActionEventContext(
                default,
                1,
                ESActionEventKind.ActionStarted,
                ESActionPresentationChannel.Audio,
                "melee.attack",
                null);

            Assert.That(
                ESActionPresentationMappingTable.TryResolve(
                    context,
                    ESActionPresentationOwner.Direct,
                    out ESActionResolvedPresentationPayload payload,
                    out string error),
                Is.True,
                error);
            Assert.That(payload.audioCueKey.StringKey, Is.EqualTo("swing.audio"));
        }

        [Test]
        public void MappingConflict_SamePriorityHardFailure()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ESActionPresentationMappingTable.Inject(new[]
                {
                    new ESActionPresentationMappingEntry
                    {
                        eventKind = ESActionEventKind.HitResolved,
                        channel = ESActionPresentationChannel.Audio,
                        owner = ESActionPresentationOwner.Direct,
                        actionKey = "melee.attack",
                        audioCueKey = "first.hit",
                    },
                    new ESActionPresentationMappingEntry
                    {
                        eventKind = ESActionEventKind.HitResolved,
                        channel = ESActionPresentationChannel.Audio,
                        owner = ESActionPresentationOwner.Direct,
                        actionKey = "melee.attack",
                        audioCueKey = "second.hit",
                    },
                }));
        }

        [Test]
        public void MappingMissing_NoneSilent_DirectHardFailure()
        {
            var context = new ESActionEventContext(
                default,
                1,
                ESActionEventKind.HitResolved,
                ESActionPresentationChannel.Camera,
                "melee.attack",
                "sword");

            Assert.That(
                ESActionPresentationMappingTable.TryResolve(
                    context,
                    ESActionPresentationOwner.None,
                    out ESActionResolvedPresentationPayload silent,
                    out _),
                Is.True);
            Assert.That(silent.IsSilent, Is.True);

            Assert.That(
                ESActionPresentationMappingTable.TryResolve(
                    context,
                    ESActionPresentationOwner.Direct,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("未找到"));
        }

        [Test]
        public void Mapping_RejectsOwnerMismatch()
        {
            ESActionPresentationMappingTable.Inject(new[]
            {
                new ESActionPresentationMappingEntry
                {
                    eventKind = ESActionEventKind.ActionStarted,
                    channel = ESActionPresentationChannel.Audio,
                    owner = ESActionPresentationOwner.SkillTrack,
                    actionKey = "melee.attack",
                },
            });

            var context = new ESActionEventContext(
                default,
                1,
                ESActionEventKind.ActionStarted,
                ESActionPresentationChannel.Audio,
                "melee.attack",
                null);

            Assert.That(
                ESActionPresentationMappingTable.TryResolve(
                    context,
                    ESActionPresentationOwner.Direct,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("Owner"));
        }

        [Test]
        public void Mapping_DirectAudioWithoutCueKey_IsHardFailure()
        {
            Assert.Throws<InvalidOperationException>(() =>
                ESActionPresentationMappingTable.Inject(new[]
                {
                    new ESActionPresentationMappingEntry
                    {
                        eventKind = ESActionEventKind.ActionStarted,
                        channel = ESActionPresentationChannel.Audio,
                        owner = ESActionPresentationOwner.Direct,
                    },
                }));
        }

        [Test]
        public void Mapping_AudioPayloadDoesNotDeclareOtherChannels()
        {
            ESActionPresentationMappingTable.Inject(new[]
            {
                new ESActionPresentationMappingEntry
                {
                    eventKind = ESActionEventKind.ActionStarted,
                    channel = ESActionPresentationChannel.Audio,
                    owner = ESActionPresentationOwner.Direct,
                    actionKey = "melee.attack",
                    audioCueKey = "melee.swing",
                    vfxPrefabKey = new ESAssetReferPrefabConfigKey
                    {
                        stringKey = "must.not.dispatch",
                    },
                    cameraShakeAmplitude = 1f,
                    hitstopSeconds = 0.1f,
                },
            });

            var context = new ESActionEventContext(
                default,
                1,
                ESActionEventKind.ActionStarted,
                ESActionPresentationChannel.Audio,
                "melee.attack",
                null);

            Assert.That(
                ESActionPresentationMappingTable.TryResolve(
                    context,
                    ESActionPresentationOwner.Direct,
                    out ESActionResolvedPresentationPayload payload,
                    out string error),
                Is.True,
                error);
            Assert.That(payload.audioState.isDeclared, Is.True);
            Assert.That(payload.vfxState.isDeclared, Is.False);
            Assert.That(payload.cameraState.isDeclared, Is.False);
            Assert.That(payload.hitstopState.isDeclared, Is.False);
        }

        [Test]
        public void MappingDataInfo_InjectIntoCatalog_Resolves()
        {
            ActionPresentationMappingDataInfo mapping = ScriptableObject.CreateInstance<ActionPresentationMappingDataInfo>();
            mapping.name = "Mapping_Test";
            mapping.entries.Add(new ESActionPresentationMappingEntry
            {
                eventKind = ESActionEventKind.HitResolved,
                channel = ESActionPresentationChannel.Audio,
                owner = ESActionPresentationOwner.Direct,
                actionKey = "melee.attack",
                weaponKey = "sword",
                audioCueKey = "mapping.hit",
            });
            created.Add(mapping);

            mapping.InjectGameCoreTables();

            var context = new ESActionEventContext(
                default,
                1,
                ESActionEventKind.HitResolved,
                ESActionPresentationChannel.Audio,
                "melee.attack",
                "sword");
            Assert.That(
                ESActionPresentationMappingTable.TryResolve(
                    context,
                    ESActionPresentationOwner.Direct,
                    out ESActionResolvedPresentationPayload payload,
                    out string error),
                Is.True,
                error);
            Assert.That(payload.audioCueKey.StringKey, Is.EqualTo("mapping.hit"));
            Assert.That(payload.audioState.isDeclared, Is.True);
            Assert.That(payload.audioState.requiresCatalogHandle, Is.True);
            Assert.That(payload.audioState.owner, Is.EqualTo(ESActionPresentationOwner.Direct));
        }

        private ActionTemplateDataInfo CreateInfo(string key, params ESActionPhaseData[] phases)
        {
            ActionTemplateDataInfo info = ScriptableObject.CreateInstance<ActionTemplateDataInfo>();
            info.name = "Action_" + key;
            info.actionKey = key;
            info.category = ESActionCategory.Attack;
            info.phases.AddRange(phases);
            info.comboTransitions.Add(new ESActionComboTransitionData
            {
                fromStep = 0,
                toStep = 1,
                inputBufferWindow = 0.1f,
            });
            info.presentationBindings.Add(new ESActionPresentationBindingData
            {
                eventKind = ESActionEventKind.HitResolved,
                channel = ESActionPresentationChannel.Hitstop,
                owner = ESActionPresentationOwner.Direct,
                skillTrackKey = "melee.attack.track",
            });
            created.Add(info);
            return info;
        }

        private static ESActionPhaseData Phase(ESActionPhaseKind kind, float duration = 0.2f, float bufferWindow = 0f)
        {
            return new ESActionPhaseData
            {
                kind = kind,
                duration = duration,
                inputBufferWindow = bufferWindow,
                hitWindow = new ESActionHitWindowData
                {
                    enabled = kind == ESActionPhaseKind.Active,
                    radius = 1f,
                    forwardDistance = 1f,
                    damageMultiplier = 1f,
                },
            };
        }

        private void InjectLocal(ActionTemplateDataInfo info, ESActionConfigKeyTable table = null)
        {
            ESActionConfigKeyTable target = table ?? localTable;
            target.InjectWith(
                info.actionKey,
                info,
                info.category,
                info.phases,
                info.comboTransitions,
                info.cancelRules,
                info.presentationBindings,
                info.allowBufferedInput,
                info.globalInputBufferWindow,
                info.name);
        }

        private static void AssertDespawnCleanupAfter(string combatMarker)
        {
            List<string> sequence = ESActionPoolLifecycleDiagnostics.Sequence;
            string[] laterMarkers =
            {
                "Entity.CameraRelease",
                "Entity.DefaultCameraRelease",
                "Entity.TagCatalogUnsubscribe",
                "Entity.AttributeCatalogUnsubscribe",
                "Entity.BuffClear",
                "Entity.ClearDefinition",
                "Entity.ValueChangeReset",
                "Entity.TagReset",
            };

            int combatIndex = sequence.IndexOf(combatMarker);
            Assert.That(combatIndex, Is.GreaterThanOrEqualTo(0));

            for (int i = 0; i < laterMarkers.Length; i++)
            {
                int markerIndex = sequence.IndexOf(laterMarkers[i]);
                Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0), "missing marker: " + laterMarkers[i]);
                Assert.That(
                    combatIndex,
                    Is.LessThan(markerIndex),
                    "marker must run after combat bridge release: " + laterMarkers[i]);
            }
        }
    }
}
