using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESWeaponShotRuntimeTests
    {
        private static void ConsumeEffectTrigger(ESEffectTriggerEvent evt)
        {
        }

        [Test]
        public void EffectTriggerHub_SteadyStatePublishDoesNotAllocate()
        {
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            hub.Subscribe(ESEffectTriggerKind.Attack, ConsumeEffectTrigger);
            EntityPrimaryAttackEvent attack = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.Started,
                1201,
                default,
                null,
                null,
                null);
            ESEffectTriggerEvent trigger = new ESEffectTriggerEvent(null, attack);
            hub.Publish(trigger);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1024; i++)
                hub.Publish(trigger);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.EqualTo(0L),
                "Hub 稳态发布不应产生托管分配；若失败请检查热路径新增对象/委托/容器。");
        }

        [Test]
        public void AttackSpecialInfo_IsOptionalAndValueOnly()
        {
            EntityPrimaryAttackEvent empty = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.Started, 1202, default, null, null, null);
            Assert.That(empty.specialInfo.IsValid, Is.False);

            ESAttackSpecialInfo special = new ESAttackSpecialInfo(
                7, flags: 3, int0: 2, int1: 5, float0: 1.25f, float1: 0.5f,
                vector: new Vector3(0f, 1f, 0f));
            EntityPrimaryAttackEvent enriched = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.HitResolved, 1203, default, null, null, null,
                specialInfo: special);

            Assert.That(enriched.specialInfo.IsValid, Is.True);
            Assert.That(enriched.specialInfo.schemaId, Is.EqualTo(7));
            Assert.That(enriched.specialInfo.flags, Is.EqualTo(3u));
            Assert.That(enriched.specialInfo.vector, Is.EqualTo(Vector3.up));
            Assert.That(enriched.payload.attackId, Is.EqualTo(1203));
            Assert.That(enriched.payload.specialInfo.schemaId, Is.EqualTo(7));
        }

        [Test]
        public void EntityInteractionStream_IsBoundedAndReusable()
        {
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            ESEntityInteractionStream stream = new ESEntityInteractionStream(2);
            EntityPrimaryAttackEvent first = new EntityPrimaryAttackEvent(EntityPrimaryAttackEventKind.Started, 1, default, null, null, null);
            EntityPrimaryAttackEvent second = new EntityPrimaryAttackEvent(EntityPrimaryAttackEventKind.Started, 2, default, null, null, null);
            EntityPrimaryAttackEvent third = new EntityPrimaryAttackEvent(EntityPrimaryAttackEventKind.Started, 3, default, null, null, null);
            hub.Warmup();
            hub.Publish(new ESEffectTriggerEvent(null, first));
            stream.Append(new ESEffectTriggerEvent(null, first));
            stream.Append(new ESEffectTriggerEvent(null, second));
            stream.Append(new ESEffectTriggerEvent(null, third));

            Assert.That(stream.Count, Is.EqualTo(2));
            Assert.That(stream.FirstSequence, Is.EqualTo(1UL));
            Assert.That(stream.TryRead(0, out ESEffectTriggerEvent value), Is.True);
            Assert.That(value.attack.attackId, Is.EqualTo(2));
            stream.Clear();
            Assert.That(stream.Count, Is.EqualTo(0));
            Assert.That(stream.TryRead(0, out _), Is.False);
        }

        [Test]
        public void GameplayInteractionHub_PublishAppendsToEntityStream()
        {
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            hub.Warmup();
            ESEntityInteractionStream stream = hub.Stream;
            EntityPrimaryAttackEvent attack = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.HitResolved, 4040, default, null, null, null);

            hub.Publish(new ESEffectTriggerEvent(null, attack));

            Assert.That(stream.Count, Is.EqualTo(1));
            Assert.That(stream.TryRead(0, out ESEffectTriggerEvent recorded), Is.True);
            Assert.That(recorded.kind, Is.EqualTo(ESEffectTriggerKind.Attack));
            Assert.That(recorded.attack.attackId, Is.EqualTo(4040));
            Assert.That(recorded.traceId, Is.Not.EqualTo(0UL));
        }

        [Test]
        public void EffectTriggerHub_SupportsChainedDelegatesAndAggregation()
        {
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            int attackCalls = 0;
            int damageCalls = 0;
            ulong attackTrace = 0;
            ulong damageTrace = 0;
            ulong damageParentTrace = 0;
            Action<ESEffectTriggerEvent> attack = evt =>
            {
                attackCalls++;
                attackTrace = evt.traceId;
                hub.Publish(new ESEffectTriggerEvent(
                    null,
                    new ESEntityDamageRequest(null, 1f, 0f, null, Vector3.zero, Vector3.forward, 1204),
                    new ESEntityDamageResult(true, false, 10f, 9f)));
            };
            Action<ESEffectTriggerEvent> damage = evt =>
            {
                damageCalls++;
                damageTrace = evt.traceId;
                damageParentTrace = evt.parentTraceId;
            };

            hub.Subscribe(ESEffectTriggerKind.Attack, attack);
            hub.Subscribe(ESEffectTriggerKind.Damage, damage);
            hub.Publish(new ESEffectTriggerEvent(
                null,
                new EntityPrimaryAttackEvent(EntityPrimaryAttackEventKind.Started, 1204, default, null, null, null)));

            Assert.That(attackCalls, Is.EqualTo(1));
            Assert.That(damageCalls, Is.EqualTo(1));
            Assert.That(hub.GetPublishedCount(ESEffectTriggerKind.Attack), Is.EqualTo(1));
            Assert.That(hub.GetPublishedCount(ESEffectTriggerKind.Damage), Is.EqualTo(1));
            Assert.That(attackTrace, Is.Not.EqualTo(0UL));
            Assert.That(damageTrace, Is.Not.EqualTo(0UL));
            Assert.That(damageTrace, Is.Not.EqualTo(attackTrace));
            Assert.That(damageParentTrace, Is.EqualTo(attackTrace));
        }

        [Test]
        public void EffectTriggerHub_BoundsRecursiveComposition()
        {
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            int calls = 0;
            EntityPrimaryAttackEvent attack = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.Started, 9090, default, null, null, null);
            Action<ESEffectTriggerEvent> loop = _ =>
            {
                calls++;
                hub.Publish(new ESEffectTriggerEvent(null, attack));
            };
            hub.Subscribe(ESEffectTriggerKind.Attack, loop);

            hub.Publish(new ESEffectTriggerEvent(null, attack));

            Assert.That(calls, Is.EqualTo(EntityGameplayInteractionHub.MaxDispatchDepth));
            Assert.That(hub.DispatchDepthRejectCount, Is.EqualTo(1));
        }

        [Test]
        public void EffectTriggerAggregate_ComposesWithoutPerConsumeAllocation()
        {
            ESEffectTriggerSummary aggregate = new ESEffectTriggerSummary();
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            ESEffectTriggerEvent damage = new ESEffectTriggerEvent(
                null,
                new ESEntityDamageRequest(null, 4f, 0f, null, Vector3.zero, Vector3.forward, 77),
                new ESEntityDamageResult(true, false, 20f, 16f));
            hub.Resolved += evt => aggregate.Consume(in evt);
            hub.Publish(in damage);
            hub.Publish(in damage);

            Assert.That(aggregate.DamageCount, Is.EqualTo(2));
            Assert.That(aggregate.AppliedDamage, Is.EqualTo(8f));
            Assert.That(aggregate.LastTraceId, Is.Not.EqualTo(0UL));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1024; index++)
                hub.Publish(in damage);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.EqualTo(0L));

            aggregate.Reset();
            Assert.That(aggregate.DamageCount, Is.EqualTo(0));
        }

        [Test]
        public void HealthDamageAndUnifiedTrigger_SteadyStateDoesNotAllocate()
        {
            GameObject targetObject = new GameObject("DamageGcTarget");
            Entity target = targetObject.AddComponent<Entity>();
            try
            {
                EntityBasicHealthModule health = AddBasicModule(target, new EntityBasicHealthModule());
                health.maxHealth = 100000f;
                health.OnPoolSpawned();
                target.GameplayInteractionHub.Warmup();
                target.GameplayInteractionHub.Resolved += ConsumeEffectTrigger;
                ESEntityDamageRequest request = new ESEntityDamageRequest(
                    null, 0.1f, 0f, null, Vector3.zero, Vector3.forward, 1301);
                health.TryApplyDamage(request, out _);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++)
                    health.TryApplyDamage(request, out _);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(allocated, Is.EqualTo(0L),
                    "Health + unified damage trigger 稳态不应产生托管分配。");
            }
            finally
            {
                target.OnPoolDespawned();
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void AuthorityContracts_DoNotExposeDirectShotLaunchOrBuffMutation()
        {
            Assert.That(typeof(ItemShotModule).GetMethod("Launch", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(ItemShotModule).GetMethod("LaunchTo", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(ItemShotModule).GetMethod("Internal_InitializeSpawn", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(ItemShotModule).GetMethod("Internal_Stop", BindingFlags.Instance | BindingFlags.Public), Is.Null);

            Assert.That(typeof(ESActiveBuffRuntime).GetMethod("TryApply", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(ESActiveBuffRuntime).GetMethod("Apply", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(ESActiveBuffRuntime).GetMethod("Remove", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(EntityBasicHealthModule).GetMethod("Internal_SetCurrentHealth", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(EntityBasicCombatModule).GetMethod("Internal_TryFireWeapon", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(EntityBasicCombatModule).GetMethod("Internal_CancelPrimaryAttack", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(ESItemInstanceTable).GetMethod("Internal_TrySetWeaponState", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(ESShotSpawner).GetMethod("TrySpawn", BindingFlags.Static | BindingFlags.Public), Is.Not.Null);
        }

        [SetUp]
        public void SetUp()
        {
            ResetWeaponTable();
            ResetShotScheduler();
        }

        [TearDown]
        public void TearDown()
        {
            ResetShotScheduler();
            ResetWeaponTable();
        }

        [Test]
        public void DamageConsumer_UsesPreparedWeaponDamageAndResetsHealthOnPoolSpawn()
        {
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.damage-consumer" };
            ItemWeaponSharedData authorDefinition = ItemWeaponSharedData.Default;
            authorDefinition.deliveryMode = WeaponAttackDeliveryMode.HitScan;
            authorDefinition.fire.enabled = true;
            authorDefinition.fire.damage = 25f;
            authorDefinition.fire.impactStrength = 2f;
            ESRuntimeDataGameCore.Weapons.BeginBuild();
            try
            {
                ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    authorDefinition,
                    ItemWeaponVariableData.Default,
                    prefabKey: CreatePrefabKey("tests.prefab.weapon.damage-consumer"));
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
            }

            GameObject attackerObject = new GameObject("DamageConsumerAttacker");
            GameObject targetObject = new GameObject("DamageConsumerTarget");
            BoxCollider targetCollider = targetObject.AddComponent<BoxCollider>();
            Entity attacker = attackerObject.AddComponent<Entity>();
            Entity target = targetObject.AddComponent<Entity>();
            try
            {
                EntityBasicCombatModule combat = AddBasicModule(attacker, new EntityBasicCombatModule());
                EntityBasicHealthModule health = AddBasicModule(target, new EntityBasicHealthModule());
                health.maxHealth = 100f;
                health.OnPoolSpawned();
                target.OnPoolSpawned();

                PublishHit(combat, weaponKey, targetCollider, 501);
                Assert.That(health.CurrentHealth, Is.EqualTo(75f));
                Assert.That(health.LastAttackId, Is.EqualTo(501));
                Assert.That(health.LastImpactStrength, Is.EqualTo(2f));

                authorDefinition.fire.damage = 1f;
                authorDefinition.fire.impactStrength = 9f;
                PublishHit(combat, weaponKey, targetCollider, 502);
                Assert.That(health.CurrentHealth, Is.EqualTo(50f),
                    "作者对象在提交后修改不得污染 Prepared Weapon 伤害。");
                Assert.That(health.LastImpactStrength, Is.EqualTo(2f));

                health.OnPoolDespawned();
                health.OnPoolSpawned();
                Assert.That(health.CurrentHealth, Is.EqualTo(100f));
                Assert.That(health.LastAttackId, Is.Zero);
            }
            finally
            {
                target.OnPoolDespawned();
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(attackerObject);
            }
        }

        [Test]
        public void DamageConsumer_IsolatesListenerFailureWithoutBreakingAppliedDamage()
        {
            GameObject targetObject = new GameObject("DamageListenerIsolationTarget");
            Entity target = targetObject.AddComponent<Entity>();
            try
            {
                EntityBasicHealthModule health = AddBasicModule(target, new EntityBasicHealthModule());
                health.maxHealth = 100f;
                health.OnPoolSpawned();
                int successfulListenerCount = 0;
                health.DamageApplied += _ => throw new InvalidOperationException("damage-listener-failure");
                health.DamageApplied += _ => successfulListenerCount++;
                LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex("damage-listener-failure"));

                bool applied = health.TryApplyDamage(
                    new ESEntityDamageRequest(
                        null,
                        10f,
                        0f,
                        null,
                        Vector3.zero,
                        Vector3.forward,
                        601),
                    out ESEntityDamageResult result);

                Assert.That(applied, Is.True);
                Assert.That(result.applied, Is.True);
                Assert.That(health.CurrentHealth, Is.EqualTo(90f));
                Assert.That(successfulListenerCount, Is.EqualTo(1));
            }
            finally
            {
                target.OnPoolDespawned();
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void HealthDamage_ProducesUnifiedDamageTriggerWithOriginalContext()
        {
            GameObject targetObject = new GameObject("EffectHubHealthTarget");
            Entity target = targetObject.AddComponent<Entity>();
            try
            {
                EntityBasicHealthModule health = AddBasicModule(target, new EntityBasicHealthModule());
                health.maxHealth = 50f;
                health.OnPoolSpawned();

                int receivedAttackId = 0;
                float receivedAmount = 0f;
                ESEffectTriggerKind receivedKind = ESEffectTriggerKind.Attack;
                target.GameplayInteractionHub.Resolved += evt =>
                {
                    receivedKind = evt.kind;
                    receivedAttackId = evt.damageRequest.attackId;
                    receivedAmount = evt.damageRequest.amount;
                };

                ESEntityDamageRequest request = new ESEntityDamageRequest(
                    null,
                    13f,
                    2f,
                    null,
                    new Vector3(1f, 2f, 3f),
                    Vector3.forward,
                    1001);
                Assert.That(health.TryApplyDamage(request, out ESEntityDamageResult result), Is.True);
                Assert.That(result.applied, Is.True);
                Assert.That(receivedKind, Is.EqualTo(ESEffectTriggerKind.Damage));
                Assert.That(receivedAttackId, Is.EqualTo(1001));
                Assert.That(receivedAmount, Is.EqualTo(13f));
            }
            finally
            {
                target.OnPoolDespawned();
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void EffectTriggerHub_DispatchesTypedResultsInOrderAndClears()
        {
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            List<ESEffectTriggerKind> kinds = new List<ESEffectTriggerKind>();
            hub.Resolved += evt => kinds.Add(evt.kind);

            EntityPrimaryAttackSelection selection = new EntityPrimaryAttackSelection(
                EntityPrimaryAttackRoute.Action,
                EntityPrimaryAttackSource.PrimaryWeapon);
            EntityPrimaryAttackEvent attack = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.Started,
                801,
                selection,
                null,
                null,
                null);
            hub.Publish(new ESEffectTriggerEvent(null, attack));
            hub.Publish(new ESEffectTriggerEvent(
                null,
                new ESEntityDamageRequest(null, 1f, 0f, null, Vector3.zero, Vector3.forward, 802),
                new ESEntityDamageResult(true, false, 10f, 9f)));
            ESEntityMovementResult movement = new ESEntityMovementResult(
                null,
                Vector3.zero,
                Vector3.right,
                Vector3.right,
                true,
                803);
            hub.Publish(new ESEffectTriggerEvent(null, movement));
            hub.Publish(new ESEffectTriggerEvent(
                null,
                new ESShotLifecycleEvent(
                    ESShotLifecycleKind.Launched,
                    null,
                    default,
                    default)));

            Assert.That(kinds, Is.EqualTo(new[]
            {
                ESEffectTriggerKind.Attack,
                ESEffectTriggerKind.Damage,
                ESEffectTriggerKind.Movement,
                ESEffectTriggerKind.Spawn
            }));
            Assert.That(hub.TotalPublished, Is.EqualTo(4));
            Assert.That(hub.GetPublishedCount(ESEffectTriggerKind.Damage), Is.EqualTo(1));

            hub.Clear();
            hub.Publish(new ESEffectTriggerEvent(null, attack));
            Assert.That(kinds.Count, Is.EqualTo(4));
            Assert.That(hub.TotalPublished, Is.EqualTo(1));
        }

        [Test]
        public void EffectTriggerHub_RoutedSubscriptionReceivesOnlySelectedKind()
        {
            EntityGameplayInteractionHub hub = new EntityGameplayInteractionHub();
            int damageCount = 0;
            Action<ESEffectTriggerEvent> onDamage = _ => damageCount++;
            hub.Subscribe(ESEffectTriggerKind.Damage, onDamage);

            EntityPrimaryAttackEvent attack = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.Started,
                811,
                default,
                null,
                null,
                null);
            hub.Publish(new ESEffectTriggerEvent(null, attack));
            hub.Publish(new ESEffectTriggerEvent(
                null,
                new ESEntityDamageRequest(null, 2f, 0f, null, Vector3.zero, Vector3.forward, 812),
                new ESEntityDamageResult(true, false, 10f, 8f)));

            Assert.That(damageCount, Is.EqualTo(1));
            hub.Unsubscribe(ESEffectTriggerKind.Damage, onDamage);
            hub.Publish(new ESEffectTriggerEvent(
                null,
                new ESEntityDamageRequest(null, 3f, 0f, null, Vector3.zero, Vector3.forward, 813),
                new ESEntityDamageResult(true, false, 8f, 5f)));
            Assert.That(damageCount, Is.EqualTo(1));
        }

        [Test]
        public void CombatPrimaryAttack_ForwardsToEntityGameplayInteractionHub()
        {
            GameObject ownerObject = new GameObject("EffectHubCombatOwner");
            Entity owner = ownerObject.AddComponent<Entity>();
            try
            {
                EntityBasicCombatModule combat = AddBasicModule(owner, new EntityBasicCombatModule());
                int attackId = 0;
                ESEffectTriggerKind kind = ESEffectTriggerKind.Damage;
                owner.GameplayInteractionHub.Resolved += evt =>
                {
                    kind = evt.kind;
                    attackId = evt.attack.attackId;
                };

                EntityPrimaryAttackSelection selection = new EntityPrimaryAttackSelection(
                    EntityPrimaryAttackRoute.Action,
                    EntityPrimaryAttackSource.PrimaryWeapon);
                EntityPrimaryAttackEvent attack = new EntityPrimaryAttackEvent(
                    EntityPrimaryAttackEventKind.Started,
                    901,
                    selection,
                    null,
                    null,
                    null);
                InvokePrivate<object>(combat, "PublishPrimaryAttackEvent", attack);

                Assert.That(kind, Is.EqualTo(ESEffectTriggerKind.Attack));
                Assert.That(attackId, Is.EqualTo(901));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ShotLifecycle_LaunchedForwardsToEntityGameplayInteractionHub()
        {
            GameObject ownerObject = new GameObject("EffectHubShotOwner");
            Entity owner = ownerObject.AddComponent<Entity>();
            try
            {
                EntityBasicCombatModule combat = AddBasicModule(owner, new EntityBasicCombatModule());
                ESEffectTriggerKind receivedKind = ESEffectTriggerKind.Attack;
                owner.GameplayInteractionHub.Resolved += evt => receivedKind = evt.kind;

                ESShotLaunchContext context = new ESShotLaunchContext(
                    902,
                    owner,
                    default,
                    null,
                    default,
                    lifecycleObserver: null,
                    publishesAttackFinish: false);
                ESShotLifecycleEvent launched = new ESShotLifecycleEvent(
                    ESShotLifecycleKind.Launched,
                    null,
                    context,
                    default);
                InvokePrivate<object>(combat, "HandleShotLifecycle", launched);

                Assert.That(receivedKind, Is.EqualTo(ESEffectTriggerKind.Spawn));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void KccMovementResolved_ForwardsToEntityGameplayInteractionHub()
        {
            GameObject ownerObject = new GameObject("EffectHubMovementOwner");
            Entity owner = ownerObject.AddComponent<Entity>();
            try
            {
                owner.kcc.motor = owner.GetComponent<KinematicCharacterMotor>();
                ESEffectTriggerKind receivedKind = ESEffectTriggerKind.Attack;
                owner.GameplayInteractionHub.Resolved += evt => receivedKind = evt.kind;

                InvokePrivate<object>(owner.kcc, "PublishMovementResolved", owner);

                Assert.That(receivedKind, Is.EqualTo(ESEffectTriggerKind.Movement));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void BeamFinish_ClearsSessionBeforeReentrantListenerRuns()
        {
            GameObject ownerObject = new GameObject("BeamFinishOwner");
            Entity owner = ownerObject.AddComponent<Entity>();
            try
            {
                EntityBasicCombatModule combat = AddBasicModule(owner, new EntityBasicCombatModule());
                var selection = new EntityPrimaryAttackSelection(
                    EntityPrimaryAttackRoute.Beam,
                    EntityPrimaryAttackSource.PrimaryWeapon);
                var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.beam-finish" };
                SetPrivateField(combat, "_activeBeamAttackId", 651);
                SetPrivateField(combat, "_activeBeamSelection", selection);
                SetPrivateField(combat, "_activeBeamWeaponKey", weaponKey);
                int finishedCount = 0;
                combat.PrimaryAttackEvent += evt =>
                {
                    if (evt.kind != EntityPrimaryAttackEventKind.Finished)
                        return;
                    finishedCount++;
                    combat.EndPrimaryAttack();
                };

                Assert.That(InvokePrivate<bool>(combat, "FinishBeamSession"), Is.True);
                Assert.That(finishedCount, Is.EqualTo(1));
                Assert.That(InvokePrivate<bool>(combat, "FinishBeamSession"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ShotPublicSpawn_RejectsInvalidVariableBeforeRuntimeLookup()
        {
            ItemShotVariableData invalid = ItemShotVariableData.Default;
            invalid.speedMultiplier = float.NaN;

            bool spawned = ESShotSpawner.TrySpawnWithVariable(
                new ESShotConfigKey { stringKey = "tests.shot.invalid-public-variable" },
                Vector3.zero,
                Vector3.forward,
                invalid,
                default,
                out ItemShotModule shot,
                out string error);

            Assert.That(spawned, Is.False);
            Assert.That(shot, Is.Null);
            Assert.That(error, Is.EqualTo("Shot VariableData 无效。"));
        }

        [Test]
        public void ShotPattern_FinishesOnlyAfterLastActualMemberEnds()
        {
            GameObject ownerObject = new GameObject("ShotPatternOwner");
            Entity owner = ownerObject.AddComponent<Entity>();
            try
            {
                EntityBasicCombatModule combat = AddBasicModule(owner, new EntityBasicCombatModule());
                var selection = new EntityPrimaryAttackSelection(
                    EntityPrimaryAttackRoute.Shot,
                    EntityPrimaryAttackSource.PrimaryWeapon);
                var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.pattern" };
                int finishedCount = 0;
                combat.PrimaryAttackEvent += evt =>
                {
                    if (evt.kind == EntityPrimaryAttackEventKind.Finished)
                        finishedCount++;
                };

                Assert.That(
                    InvokePrivate<bool>(
                        combat,
                        "TryRegisterShotPattern",
                        701,
                        3,
                        selection,
                        weaponKey),
                    Is.True);
                var context = new ESShotLaunchContext(
                    701,
                    owner,
                    default,
                    weaponKey,
                    selection,
                    lifecycleObserver: null,
                    publishesAttackFinish: false);

                PublishShotTerminal(combat, context);
                PublishShotTerminal(combat, context);
                Assert.That(finishedCount, Is.Zero);
                Assert.That(combat.ActiveShotPatternCount, Is.EqualTo(1));

                PublishShotTerminal(combat, context);
                Assert.That(finishedCount, Is.EqualTo(1));
                Assert.That(combat.ActiveShotPatternCount, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void ShotSimulationBatch_EnforcesCapacityAndMaintainsSwapRemovalIndexes()
        {
            var shots = new ItemShotModule[ESShotSimulationBatch.Capacity + 1];
            for (int index = 0; index < shots.Length; index++)
                shots[index] = new ItemShotModule();

            for (int index = 0; index < ESShotSimulationBatch.Capacity; index++)
                Assert.That(ESShotSimulationBatch.Internal_Register(shots[index]), Is.True);
            Assert.That(ESShotSimulationBatch.Internal_Register(shots[shots.Length - 1]), Is.False);
            Assert.That(ESShotSimulationBatch.ActiveCount, Is.EqualTo(ESShotSimulationBatch.Capacity));
            Assert.That(ESShotSimulationBatch.HighWatermark, Is.EqualTo(ESShotSimulationBatch.Capacity));
            Assert.That(ESShotSimulationBatch.CapacityRejectCount, Is.EqualTo(1));

            ItemShotModule moved = shots[shots.Length - 2];
            shots[0].hitOverflowCount = 2;
            shots[0].hitOverflowStopCount = 1;
            shots[0].resolvedColliderOverflowCount = 3;
            shots[0].impactOverflowCount = 4;
            ESShotSimulationBatch.Internal_Unregister(shots[0]);
            Assert.That(moved.Internal_SimulationIndex, Is.Zero);
            Assert.That(shots[0].Internal_SimulationIndex, Is.EqualTo(-1));
            Assert.That(ESShotSimulationBatch.HitQueryOverflowCount, Is.EqualTo(2));
            Assert.That(ESShotSimulationBatch.HitOverflowStopCount, Is.EqualTo(1));
            Assert.That(ESShotSimulationBatch.ResolvedColliderCapacityRejectCount, Is.EqualTo(3));
            Assert.That(ESShotSimulationBatch.ImpactQueryOverflowCount, Is.EqualTo(4));

            for (int index = 1; index < ESShotSimulationBatch.Capacity; index++)
                ESShotSimulationBatch.Internal_Unregister(shots[index]);
            Assert.That(ESShotSimulationBatch.ActiveCount, Is.Zero);
        }

        [Test]
        public void ShotSimulationBatch_TickProcessesMemberMovedIntoSelfRemovedSlot()
        {
            ItemShotModule first = CreateExpiringShot();
            ItemShotModule second = CreateExpiringShot();
            Assert.That(ESShotSimulationBatch.Internal_Register(first), Is.True);
            Assert.That(ESShotSimulationBatch.Internal_Register(second), Is.True);

            ESShotSimulationBatch.Internal_Tick(1f);

            Assert.That(first.state.launched, Is.False);
            Assert.That(second.state.launched, Is.False,
                "首成员自回收后交换到当前索引的成员必须在同批次继续执行。");
            Assert.That(ESShotSimulationBatch.ActiveCount, Is.Zero);
        }

        [Test]
        public void ShotBounce_DoesNotConsumeRemainingHitsFromPreviousTrajectory()
        {
            GameObject firstObject = new GameObject("BounceFirstHit");
            GameObject staleObject = new GameObject("BounceStaleHit");
            BoxCollider firstCollider = firstObject.AddComponent<BoxCollider>();
            BoxCollider staleCollider = staleObject.AddComponent<BoxCollider>();
            try
            {
                var resolver = new CountingStopResolver();
                var shot = new ItemShotModule
                {
                    sharedData = ItemShotSharedData.Default,
                    state = new ShotMotionState
                    {
                        previousPosition = Vector3.zero,
                        currentPosition = Vector3.forward * 4f,
                        velocity = Vector3.forward * 10f,
                        direction = Vector3.forward,
                        launched = true
                    }
                };
                shot.sharedData.impact.bounceCount = 1;
                shot.OnPoolSpawned();
                shot.SetHitSolver(new FixedHitSolver(firstCollider, staleCollider));
                SetPrivateField(
                    shot,
                    "_launchContext",
                    new ESShotLaunchContext(
                        801,
                        null,
                        default,
                        null,
                        default,
                        hitResolver: resolver));

                var result = new ShotMotionResult
                {
                    kind = ShotMotionKind.Moving,
                    previousPosition = Vector3.zero,
                    currentPosition = Vector3.forward * 4f,
                    velocity = Vector3.forward * 10f
                };
                MethodInfo method = typeof(ItemShotModule).GetMethod(
                    "TryBuildHitCandidate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null);
                object[] arguments = { result };
                method.Invoke(shot, arguments);

                Assert.That(resolver.ResolveCount, Is.EqualTo(1),
                    "反弹改变轨迹后不得继续消费旧 Cast 的剩余命中。");
                Assert.That(shot.state.launched, Is.True);
                Assert.That(shot.state.direction.z, Is.LessThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staleObject);
                UnityEngine.Object.DestroyImmediate(firstObject);
            }
        }

        [Test]
        public void ShotChain_AdvancesFromEachResolvedTargetWithoutDuplicateHits()
        {
            GameObject sourceObject = new GameObject("ChainSource");
            BoxCollider sourceCollider = sourceObject.AddComponent<BoxCollider>();
            GameObject firstObject = new GameObject("ChainFirstTarget");
            BoxCollider firstCollider = firstObject.AddComponent<BoxCollider>();
            Entity firstTarget = firstObject.AddComponent<Entity>();
            GameObject secondObject = new GameObject("ChainSecondTarget");
            BoxCollider secondCollider = secondObject.AddComponent<BoxCollider>();
            Entity secondTarget = secondObject.AddComponent<Entity>();
            firstObject.transform.position = Vector3.right;
            secondObject.transform.position = Vector3.right * 2.4f;
            try
            {
                firstTarget.OnPoolSpawned();
                secondTarget.OnPoolSpawned();
                Physics.SyncTransforms();

                ItemShotSharedData definition = ItemShotSharedData.Default;
                definition.impact.chainRadius = 1.5f;
                definition.impact.chainTargetCount = 2;
                var shot = new ItemShotModule
                {
                    sharedData = definition,
                    hitLayers = ~0
                };
                shot.OnPoolSpawned();
                var resolved = new List<Collider>(2);
                shot.LifecycleEvent += evt =>
                {
                    if (evt.kind == ESShotLifecycleKind.Hit)
                        resolved.Add(evt.hit.collider);
                };
                var sourceHit = new ShotHitCandidate
                {
                    collider = sourceCollider,
                    point = Vector3.zero,
                    normal = Vector3.left,
                    incomingVelocity = Vector3.right
                };

                InvokePrivate<object>(shot, "PublishPreparedImpactHits", sourceHit);

                Assert.That(resolved, Has.Count.EqualTo(2));
                Assert.That(resolved[0], Is.SameAs(firstCollider));
                Assert.That(resolved[1], Is.SameAs(secondCollider));
            }
            finally
            {
                secondTarget.OnPoolDespawned();
                firstTarget.OnPoolDespawned();
                UnityEngine.Object.DestroyImmediate(secondObject);
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
            }
        }

        private static ItemShotModule CreateExpiringShot()
        {
            var shot = new ItemShotModule();
            shot.OnPoolSpawned();
            shot.aimMode = ShotAimMode.Free;
            shot.state = new ShotMotionState
            {
                previousPosition = new Vector3(10000f, 10000f, 10000f),
                currentPosition = new Vector3(10000f, 10000f, 10000f),
                currentRotation = Quaternion.identity,
                direction = Vector3.forward,
                elapsedTime = 1f,
                launched = true
            };
            shot.config = new ShotMotionConfig
            {
                maxLifetime = 0.01f,
                arriveDistance = 0.01f,
                flags = ShotMotionFlags.ClampSpeed
            };
            return shot;
        }

        private static void PublishHit(
            EntityBasicCombatModule combat,
            ESWeaponConfigKey weaponKey,
            Collider target,
            int attackId)
        {
            var evt = new EntityPrimaryAttackEvent(
                EntityPrimaryAttackEventKind.HitResolved,
                attackId,
                new EntityPrimaryAttackSelection(
                    EntityPrimaryAttackRoute.HitScan,
                    EntityPrimaryAttackSource.PrimaryWeapon),
                null,
                weaponKey,
                null,
                target,
                target.transform.position,
                true);
            InvokePrivate<object>(combat, "PublishPrimaryAttackEvent", evt);
        }

        private static void PublishShotTerminal(
            EntityBasicCombatModule combat,
            in ESShotLaunchContext context)
        {
            var evt = new ESShotLifecycleEvent(
                ESShotLifecycleKind.Expired,
                null,
                context,
                default);
            InvokePrivate<object>(combat, "HandleShotLifecycle", evt);
        }

        private static TModule AddBasicModule<TModule>(Entity entity, TModule module)
            where TModule : EntityBasicModuleBase
        {
            entity.EnsureEntityStructure();
            entity.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(entity);
            entity.RegisterDomain(entity.basicDomain);
            entity.basicDomain.TryAddModuleRuntime(module);
            entity.basicDomain.MyModules.ApplyBuffers(true);
            module.Start();
            return module;
        }

        private static TResult InvokePrivate<TResult>(object target, string name, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, name);
            object result = method.Invoke(target, arguments);
            return result is TResult typed ? typed : default;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private sealed class CountingStopResolver : IESShotHitResolver
        {
            public int ResolveCount { get; private set; }

            public ESShotHitDecision Resolve(
                in ESShotLaunchContext context,
                ItemShotSharedData definition,
                in ShotHitCandidate candidate)
            {
                ResolveCount++;
                return ESShotHitDecision.Stop;
            }
        }

        private sealed class FixedHitSolver : IItemShotHitSolver
        {
            private readonly Collider first;
            private readonly Collider second;

            public FixedHitSolver(Collider first, Collider second)
            {
                this.first = first;
                this.second = second;
            }

            public bool IsOverflow => false;

            public int Query(in ItemShotHitQuery query, ShotHitCandidate[] results, int maxResults)
            {
                if (results == null || maxResults < 2)
                    return 0;
                results[0] = new ShotHitCandidate
                {
                    collider = first,
                    point = Vector3.forward,
                    normal = Vector3.back,
                    distance = 1f
                };
                results[1] = new ShotHitCandidate
                {
                    collider = second,
                    point = Vector3.forward * 2f,
                    normal = Vector3.back,
                    distance = 2f
                };
                return 2;
            }
        }

        private static ESAssetReferPrefabConfigKey CreatePrefabKey(string key)
        {
            return new ESAssetReferPrefabConfigKey
            {
                stringKey = key,
                guid = "guid-" + key,
                assetTypeName = typeof(GameObject).FullName
            };
        }

        private static void ResetWeaponTable()
        {
            if (ESRuntimeDataGameCore.Weapons.IsBuilding)
                Assert.Fail("Weapon table leaked an active build transaction.");
            ESRuntimeDataGameCore.Weapons.BeginBuild(true);
            ESRuntimeDataGameCore.Weapons.EndBuild();
        }

        private static void ResetShotScheduler()
        {
            MethodInfo reset = typeof(ESShotSimulationBatch).GetMethod(
                "ResetStatics",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);
            reset.Invoke(null, null);
        }
    }
}
