using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    [Parallelizable(ParallelScope.None)]
    public sealed class ESItemInstanceTableTests
    {
        private static ESAssetReferPrefabConfigKey CreateWeaponPrefabKey(string key)
        {
            return new ESAssetReferPrefabConfigKey
            {
                stringKey = key,
                guid = "guid-" + key,
                assetTypeName = typeof(GameObject).FullName
            };
        }

        [SetUp]
        public void SetUp()
        {
            ClearDefinitionTables();
        }

        [TearDown]
        public void TearDown()
        {
            ClearDefinitionTables();
        }

        [Test]
        public void CreateMoveAndDestroy_PreservesHandleAndOwnerIndex()
        {
            var table = new ESItemInstanceTable(4);
            var request = new ESItemInstanceCreateRequest(
                itemDefinitionRuntimeKey: 10,
                ownerId: 100,
                quantity: 3,
                location: ESItemInstanceLocation.Inventory,
                relationSlot: 2);

            Assert.That(table.TryCreate(request, out ESInstanceHandle handle), Is.True);
            Assert.That(table.TryGet(handle, out ESItemInstanceRecord record), Is.True);
            Assert.That(record.quantity, Is.EqualTo(3));
            Assert.That(record.location, Is.EqualTo(ESItemInstanceLocation.Inventory));

            Assert.That(table.TryMove(
                handle,
                ownerId: 200,
                location: ESItemInstanceLocation.Equipped,
                relationSlot: 0), Is.True);
            Assert.That(table.TryGetIdentity(handle, out _, out int definitionKey, out int ownerId), Is.True);
            Assert.That(definitionKey, Is.EqualTo(10));
            Assert.That(ownerId, Is.EqualTo(200));
            Assert.That(table.TryGetOwnerBucket(100, out _, out _), Is.False);
            Assert.That(table.TryGetOwnerBucket(200, out ESInstanceHandle ownerFirst, out int ownerCount), Is.True);
            Assert.That(ownerFirst, Is.EqualTo(handle));
            Assert.That(ownerCount, Is.EqualTo(1));

            Assert.That(table.TryRemove(handle, out _), Is.True);
            Assert.That(table.TryGet(handle, out _), Is.False);
            Assert.That(table.Count, Is.Zero);
        }

        [Test]
        public void ExplicitPersistentId_IsRejectedWhenAlreadyInUse()
        {
            var table = new ESItemInstanceTable(2);
            var firstRequest = new ESItemInstanceCreateRequest(1, 1, persistentId: 9001);
            var secondRequest = new ESItemInstanceCreateRequest(1, 1, persistentId: 9001);

            Assert.That(table.TryCreate(firstRequest, out _), Is.True);
            Assert.That(table.TryCreate(secondRequest, out _), Is.False);
            Assert.That(table.Count, Is.EqualTo(1));
        }

        [Test]
        public void AutomaticPersistentId_SkipsAllLoadedSequentialIds()
        {
            const int loadedCount = 1024;
            var table = new ESItemInstanceTable(loadedCount + 1);
            for (ulong persistentId = 1; persistentId <= loadedCount; persistentId++)
            {
                var loadedRequest = new ESItemInstanceCreateRequest(
                    itemDefinitionRuntimeKey: 1,
                    ownerId: 1,
                    persistentId: persistentId);
                Assert.That(table.TryCreate(loadedRequest, out _), Is.True);
            }

            var runtimeRequest = new ESItemInstanceCreateRequest(1, 1);
            Assert.That(table.TryCreate(runtimeRequest, out ESInstanceHandle handle), Is.True);
            Assert.That(table.TryGetPersistentId(handle, out ulong allocatedId), Is.True);
            Assert.That(allocatedId, Is.EqualTo((ulong)loadedCount + 1));
        }

        [Test]
        public void WorldViewTransfer_MovesInstanceAndViewAsOneOperation()
        {
            var table = new ESItemInstanceTable(1);
            var request = new ESItemInstanceCreateRequest(
                itemDefinitionRuntimeKey: 1,
                ownerId: 10,
                location: ESItemInstanceLocation.Inventory);
            Assert.That(table.TryCreate(request, out ESInstanceHandle handle), Is.True);

            GameObject root = new GameObject("ItemView");
            GameObject target = new GameObject("WorldAnchor");
            EntityWeaponBinding binding = root.AddComponent<EntityWeaponBinding>();
            Transform grip = new GameObject("GripPivot").transform;
            grip.SetParent(root.transform, false);
            binding.ConfigureReferences(grip, null, null, null, root);

            try
            {
                var transfer = new ESItemInstanceViewTransferRequest(
                    root.transform,
                    binding,
                    target.transform,
                    ownerId: 20,
                    location: ESItemInstanceLocation.World,
                    relationSlot: -1,
                    visible: true);
                Assert.That(ESItemInstanceViewTransfer.TryCommit(table, handle, transfer, out string error), Is.True, error);
                Assert.That(root.transform.parent, Is.EqualTo(target.transform));
                Assert.That(table.TryGetIdentity(handle, out _, out _, out int ownerId), Is.True);
                Assert.That(ownerId, Is.EqualTo(20));
            }
            finally
            {
                Object.DestroyImmediate(grip.gameObject);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void StableItemKey_ResolvesIndependentItemAndWeaponRuntimeKeys()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.weapon.long_bar" };
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.long_bar" };
            var dummyWeaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.padding" };

            ESRuntimeDataGameCore.Items.BeginBuild();
            ESRuntimeDataGameCore.Weapons.BeginBuild();
            int itemRuntimeKey;
            int weaponRuntimeKey;
            try
            {
                ESRuntimeDataGameCore.Weapons.InjectWith(
                    dummyWeaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default,
                    prefabKey: CreateWeaponPrefabKey("tests.prefab.weapon.padding"));
                weaponRuntimeKey = ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default,
                    prefabKey: CreateWeaponPrefabKey("tests.prefab.weapon.long-bar"));
                itemRuntimeKey = ESRuntimeDataGameCore.Items.InjectWith(
                    itemKey,
                    ItemKind.Weapon,
                    new ItemBaseConfig { kind = ItemKind.Weapon },
                    weaponKey: weaponKey);
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
                ESRuntimeDataGameCore.Items.EndBuild();
            }

            Assert.That(itemRuntimeKey, Is.Not.EqualTo(weaponRuntimeKey));
            var table = new ESItemInstanceTable(1);
            Assert.That(table.TryCreate(itemKey, ownerId: 10, out ESInstanceHandle handle), Is.True);
            Assert.That(
                table.TryGetDefinitionRuntimeKeys(
                    handle,
                    out int resolvedItemRuntimeKey,
                    out int resolvedWeaponRuntimeKey),
                Is.True);
            Assert.That(resolvedItemRuntimeKey, Is.EqualTo(itemRuntimeKey));
            Assert.That(resolvedWeaponRuntimeKey, Is.EqualTo(weaponRuntimeKey));
        }

        [Test]
        public void WeaponUse_ConsumesOnlyBoundItemStateAndRefreshesCooldownAndHeat()
        {
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.instance_state" };
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.deliveryMode = WeaponAttackDeliveryMode.HitScan;
            definition.fire.enabled = true;
            definition.cooldown = 0f;
            definition.fire.interval = 1f;
            definition.fire.ammoCost = 2;
            definition.fire.durabilityCost = 0.1f;
            definition.fire.heatPerUse = 3f;
            definition.fire.maxHeat = 5f;
            definition.fire.heatDissipationPerSecond = 1f;
            ItemWeaponVariableData initialState = ItemWeaponVariableData.Default;
            initialState.ammo = 3;

            ESRuntimeDataGameCore.Weapons.BeginBuild();
            int weaponRuntimeKey;
            try
            {
                weaponRuntimeKey = ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    definition,
                    initialState,
                    prefabKey: CreateWeaponPrefabKey("tests.prefab.weapon.instance-state"));
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
            }

            var table = new ESItemInstanceTable(1);
            var request = new ESItemInstanceCreateRequest(
                itemDefinitionRuntimeKey: 10,
                ownerId: 20,
                weaponDefinitionRuntimeKey: weaponRuntimeKey,
                location: ESItemInstanceLocation.Equipped,
                relationSlot: 0);
            Assert.That(table.TryCreate(request, out ESInstanceHandle handle), Is.True);

            ItemWeaponSharedData wrongDefinition = ItemWeaponSharedData.Default;
            wrongDefinition.deliveryMode = WeaponAttackDeliveryMode.HitScan;
            wrongDefinition.fire.enabled = true;
            Assert.That(
                table.TryConsumeWeaponUse(
                    handle,
                    wrongDefinition,
                    9f,
                    out _,
                    out ESWeaponUseFailure wrongDefinitionFailure),
                Is.False);
            Assert.That(wrongDefinitionFailure, Is.EqualTo(ESWeaponUseFailure.MissingWeaponDefinition));

            Assert.That(
                table.TryConsumeWeaponUse(
                    handle,
                    definition,
                    10f,
                    out ItemWeaponVariableData consumed,
                    out ESWeaponUseFailure firstFailure),
                Is.True,
                firstFailure.ToString());
            Assert.That(consumed.ammo, Is.EqualTo(1));
            Assert.That(consumed.durability, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(consumed.heat, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(consumed.cooldownLeft, Is.EqualTo(1f).Within(0.0001f));

            Assert.That(
                table.TryConsumeWeaponUse(
                    handle,
                    definition,
                    10.5f,
                    out ItemWeaponVariableData cooling,
                    out ESWeaponUseFailure cooldownFailure),
                Is.False);
            Assert.That(cooldownFailure, Is.EqualTo(ESWeaponUseFailure.Cooldown));
            Assert.That(cooling.cooldownLeft, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(cooling.heat, Is.EqualTo(2.5f).Within(0.0001f));

            Assert.That(
                table.TryConsumeWeaponUse(
                    handle,
                    definition,
                    11.1f,
                    out ItemWeaponVariableData noAmmo,
                    out ESWeaponUseFailure ammoFailure),
                Is.False);
            Assert.That(ammoFailure, Is.EqualTo(ESWeaponUseFailure.Ammo));
            Assert.That(noAmmo.ammo, Is.EqualTo(1));
            Assert.That(noAmmo.cooldownLeft, Is.Zero);
            Assert.That(noAmmo.heat, Is.EqualTo(1.9f).Within(0.0001f));
        }

        [Test]
        public void ShotHitSolver_ZeroRadiusRaycastSortsCandidatesByTravelDistance()
        {
            GameObject near = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject far = GameObject.CreatePrimitive(PrimitiveType.Cube);
            near.name = "NearShotHit";
            far.name = "FarShotHit";
            near.layer = ESPhysicsLayers.Sensor;
            far.layer = ESPhysicsLayers.Sensor;
            Vector3 origin = new Vector3(12345f, 12345f, 12345f);
            near.transform.position = origin + new Vector3(0f, 0f, 2f);
            far.transform.position = origin + new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            try
            {
                var solver = new ItemShotPhysicsHitSolver(8);
                var results = new ShotHitCandidate[8];
                var query = new ItemShotHitQuery
                {
                    from = origin,
                    to = origin + new Vector3(0f, 0f, 10f),
                    radius = 0f,
                    hitLayers = ESPhysicsLayers.SensorMask,
                    triggerInteraction = QueryTriggerInteraction.Ignore
                };

                int count = solver.Query(query, results, results.Length);

                Assert.That(count, Is.EqualTo(2));
                Assert.That(results[0].collider.gameObject, Is.EqualTo(near));
                Assert.That(results[1].collider.gameObject, Is.EqualTo(far));
                Assert.That(results[0].distance, Is.LessThan(results[1].distance));
            }
            finally
            {
                Object.DestroyImmediate(near);
                Object.DestroyImmediate(far);
            }
        }

        [Test]
        public void ShotHitSolver_SaturatedQueryKeepsItsPreallocatedBuffer()
        {
            Vector3 origin = new Vector3(16000f, 16000f, 16000f);
            var colliders = new GameObject[4];
            try
            {
                for (int index = 0; index < colliders.Length; index++)
                {
                    GameObject candidate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    candidate.name = "SaturatedShotHit_" + index;
                    candidate.layer = ESPhysicsLayers.Sensor;
                    candidate.transform.position = origin + new Vector3(0f, 0f, 1f + index * 1.5f);
                    colliders[index] = candidate;
                }
                Physics.SyncTransforms();

                var solver = new ItemShotPhysicsHitSolver(2);
                var results = new ShotHitCandidate[2];
                var query = new ItemShotHitQuery
                {
                    from = origin,
                    to = origin + new Vector3(0f, 0f, 10f),
                    radius = 0f,
                    hitLayers = ESPhysicsLayers.SensorMask,
                    triggerInteraction = QueryTriggerInteraction.Ignore
                };
                System.Reflection.FieldInfo bufferField = typeof(ItemShotPhysicsHitSolver).GetField(
                    "_hitBuffer",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(bufferField, Is.Not.Null);
                var warmedBuffer = (RaycastHit[])bufferField.GetValue(solver);

                int count = solver.Query(query, results, results.Length);

                Assert.That(count, Is.EqualTo(2));
                Assert.That(solver.IsOverflow, Is.True);
                Assert.That(results[0].collider.gameObject, Is.EqualTo(colliders[0]),
                    "饱和回退必须保留路径上的最近命中。");
                Assert.That(results[1].collider.gameObject, Is.EqualTo(colliders[1]),
                    "饱和回退必须按距离返回固定容量内的最近候选。");
                Assert.That(results[0].distance, Is.LessThan(results[1].distance));
                Assert.That(bufferField.GetValue(solver), Is.SameAs(warmedBuffer),
                    "命中饱和不得在 Shot 热路径扩容或替换查询数组。");
            }
            finally
            {
                for (int index = 0; index < colliders.Length; index++)
                {
                    if (colliders[index] != null)
                        Object.DestroyImmediate(colliders[index]);
                }
            }
        }

        [Test]
        public void ScanShot_RespectsLaunchDelayAndWarmupBeforeArriving()
        {
            GameObject shotObject = new GameObject("DelayedScanShot");
            shotObject.transform.position = new Vector3(18000f, 18000f, 18000f);

            try
            {
                Item item = shotObject.AddComponent<Item>();
                item.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(item);
                item.RegisterDomain(item.basicDomain);
                ItemShotModule shot = item.GetMoudle<ItemShotModule>();
                item.basicDomain.MyModules.ApplyBuffers(true);
                shot.Start();
                shot.aimMode = ShotAimMode.Scan;
                shot.hitLayers = ESPhysicsLayers.SensorMask;
                shot.config = ShotMotionConfig.Straight(10f, 1f);
                shot.config.launchDelay = 0.2f;
                shot.config.warmupTime = 0.3f;
                shot.state = new ShotMotionState
                {
                    currentPosition = shotObject.transform.position,
                    previousPosition = shotObject.transform.position,
                    currentRotation = shotObject.transform.rotation,
                    direction = Vector3.forward,
                    launched = true
                };

                var lifecycle = new List<ESShotLifecycleKind>();
                shot.LifecycleEvent += evt => lifecycle.Add(evt.kind);
                System.Reflection.MethodInfo tick = typeof(ItemShotModule).GetMethod(
                    "Tick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(tick, Is.Not.Null);

                tick.Invoke(shot, new object[] { 0.11f });
                Assert.That(shot.latestResult.kind, Is.EqualTo(ShotMotionKind.Delayed));
                Assert.That(shot.state.launched, Is.True);
                Assert.That(lifecycle, Is.Empty);

                tick.Invoke(shot, new object[] { 0.2f });
                Assert.That(shot.latestResult.kind, Is.EqualTo(ShotMotionKind.Warmup));
                Assert.That(shot.state.launched, Is.True);
                Assert.That(lifecycle, Is.Empty);

                tick.Invoke(shot, new object[] { 0.2f });
                Assert.That(shot.latestResult.kind, Is.EqualTo(ShotMotionKind.Arrived));
                Assert.That(shot.state.launched, Is.False);
                Assert.That(lifecycle, Is.EqualTo(new[] { ESShotLifecycleKind.Arrived }));
            }
            finally
            {
                Object.DestroyImmediate(shotObject);
            }
        }

        [Test]
        public void ShotTickPolicy_AccumulatesSkippedDeltaTimeBeforeNextStep()
        {
            var shotObject = new GameObject("DecimatedShot");
            Vector3 origin = new Vector3(17000f, 17000f, 17000f);
            shotObject.transform.position = origin;

            try
            {
                Item item = shotObject.AddComponent<Item>();
                item.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(item);
                item.RegisterDomain(item.basicDomain);
                ItemShotModule shot = item.GetMoudle<ItemShotModule>();
                item.basicDomain.MyModules.ApplyBuffers(true);
                shot.Start();
                shot.SetTickPolicy(new EverySecondShotTickPolicy());
                shot.aimMode = ShotAimMode.Free;
                shot.hitLayers = ESPhysicsLayers.SensorMask;
                shot.castRadius = 0f;
                shot.config = ShotMotionConfig.Straight(10f, 2f);
                shot.state = new ShotMotionState
                {
                    previousPosition = origin,
                    currentPosition = origin,
                    currentRotation = shotObject.transform.rotation,
                    velocity = Vector3.forward * 10f,
                    direction = Vector3.forward,
                    launched = true
                };

                System.Reflection.MethodInfo tick = typeof(ItemShotModule).GetMethod(
                    "Tick",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(tick, Is.Not.Null);

                tick.Invoke(shot, new object[] { 0.1f });
                Assert.That(shot.state.elapsedTime, Is.Zero);
                Assert.That(shot.state.currentPosition, Is.EqualTo(origin));

                tick.Invoke(shot, new object[] { 0.1f });
                Assert.That(shot.state.elapsedTime, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(shot.state.currentPosition.z, Is.EqualTo(origin.z + 2f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(shotObject);
            }
        }

        [Test]
        public void WeaponRaycast_SkipsOwnerColliderAndSelectsNearestExternalHit()
        {
            GameObject ownerObject = new GameObject("WeaponRayOwner");
            GameObject ownerColliderObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Vector3 origin = new Vector3(15000f, 15000f, 15000f);
            ownerColliderObject.transform.SetParent(ownerObject.transform, false);
            ownerColliderObject.transform.localPosition = new Vector3(0f, 0f, 1f);
            targetObject.transform.position = origin + new Vector3(0f, 0f, 3f);
            ownerObject.transform.position = origin;
            Physics.SyncTransforms();

            try
            {
                Entity owner = ownerObject.AddComponent<Entity>();
                owner.EnsureEntityStructure();
                EntityBasicDomain domain = owner.basicDomain;
                domain._Editor_RegisterAllButOnlyCreateRelationship(owner);
                owner.RegisterDomain(domain);
                var combat = new EntityBasicCombatModule();
                domain.TryAddModuleRuntime(combat);
                combat.Start();

                System.Reflection.MethodInfo resolveRaycast = typeof(EntityBasicCombatModule).GetMethod(
                    "TryResolveWeaponRaycast",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(resolveRaycast, Is.Not.Null);
                object[] arguments =
                {
                    origin,
                    Vector3.forward,
                    10f,
                    (LayerMask)Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore,
                    default(RaycastHit)
                };

                Assert.That((bool)resolveRaycast.Invoke(combat, arguments), Is.True);
                var hit = (RaycastHit)arguments[5];
                Assert.That(hit.collider, Is.Not.Null);
                Assert.That(hit.collider.gameObject, Is.EqualTo(targetObject));
            }
            finally
            {
                Object.DestroyImmediate(ownerColliderObject);
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void WeaponRaycast_SaturatedBufferUsesNearestExternalFallbackWithoutReplacingBuffer()
        {
            GameObject ownerObject = new GameObject("SaturatedWeaponOwner");
            GameObject ownerColliderObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject farTargetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Vector3 origin = new Vector3(15100f, 15100f, 15100f);
            ownerColliderObject.transform.SetParent(ownerObject.transform, false);
            ownerColliderObject.transform.localPosition = new Vector3(0f, 0f, 1f);
            targetObject.transform.position = origin + new Vector3(0f, 0f, 3f);
            farTargetObject.transform.position = origin + new Vector3(0f, 0f, 5f);
            ownerObject.transform.position = origin;
            Physics.SyncTransforms();

            try
            {
                Entity owner = ownerObject.AddComponent<Entity>();
                owner.EnsureEntityStructure();
                EntityBasicDomain domain = owner.basicDomain;
                domain._Editor_RegisterAllButOnlyCreateRelationship(owner);
                owner.RegisterDomain(domain);
                var combat = new EntityBasicCombatModule
                {
                    weaponFireHitBufferCapacity = 1
                };
                domain.TryAddModuleRuntime(combat);
                combat.Start();

                System.Reflection.FieldInfo bufferField = typeof(EntityBasicCombatModule).GetField(
                    "_weaponFireHits",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.MethodInfo resolveRaycast = typeof(EntityBasicCombatModule).GetMethod(
                    "TryResolveWeaponRaycast",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(bufferField, Is.Not.Null);
                Assert.That(resolveRaycast, Is.Not.Null);
                var warmedBuffer = (RaycastHit[])bufferField.GetValue(combat);
                object[] arguments =
                {
                    origin,
                    Vector3.forward,
                    10f,
                    (LayerMask)Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore,
                    default(RaycastHit)
                };

                Assert.That((bool)resolveRaycast.Invoke(combat, arguments), Is.True);
                var hit = (RaycastHit)arguments[5];
                Assert.That(hit.collider, Is.Not.Null);
                Assert.That(hit.collider.gameObject, Is.EqualTo(targetObject));
                Assert.That(combat.weaponFireHitOverflowCount, Is.EqualTo(1));
                Assert.That(combat.weaponFireFallbackTruncationCount, Is.Zero);
                Assert.That(bufferField.GetValue(combat), Is.SameAs(warmedBuffer));
            }
            finally
            {
                Object.DestroyImmediate(ownerColliderObject);
                Object.DestroyImmediate(ownerObject);
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(farTargetObject);
            }
        }

        [Test]
        public void ShotResolvedColliderCapacity_StopsWithoutGrowingThePreparedSet()
        {
            GameObject shotObject = new GameObject("ResolvedColliderCapacityShot");
            GameObject firstTarget = new GameObject("ResolvedColliderTargetA");
            GameObject secondTarget = new GameObject("ResolvedColliderTargetB");
            Collider firstCollider = firstTarget.AddComponent<BoxCollider>();
            Collider secondCollider = secondTarget.AddComponent<BoxCollider>();

            try
            {
                Item item = shotObject.AddComponent<Item>();
                item.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(item);
                item.RegisterDomain(item.basicDomain);
                ItemShotModule shot = item.GetMoudle<ItemShotModule>();
                item.basicDomain.MyModules.ApplyBuffers(true);
                shot.resolvedColliderCapacity = 1;
                shot.Start();
                shot.state = new ShotMotionState { launched = true };

                System.Reflection.FieldInfo contextField = typeof(ItemShotModule).GetField(
                    "_launchContext",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.FieldInfo resolvedIdsField = typeof(ItemShotModule).GetField(
                    "_resolvedColliderIds",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.MethodInfo resolveHit = typeof(ItemShotModule).GetMethod(
                    "ResolveHit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(contextField, Is.Not.Null);
                Assert.That(resolvedIdsField, Is.Not.Null);
                Assert.That(resolveHit, Is.Not.Null);
                contextField.SetValue(shot, new ESShotLaunchContext(
                    1,
                    null,
                    default,
                    null,
                    default,
                    hitResolver: AlwaysPierceHitResolver.Instance));
                var preparedIds = (int[])resolvedIdsField.GetValue(shot);
                var result = new ShotMotionResult
                {
                    kind = ShotMotionKind.Moving,
                    currentPosition = Vector3.forward,
                    velocity = Vector3.forward
                };

                object[] firstArguments =
                {
                    new ShotHitCandidate { collider = firstCollider, point = Vector3.forward },
                    result
                };
                Assert.That((bool)resolveHit.Invoke(shot, firstArguments), Is.True);

                object[] secondArguments =
                {
                    new ShotHitCandidate { collider = secondCollider, point = Vector3.forward * 2f },
                    firstArguments[1]
                };
                Assert.That((bool)resolveHit.Invoke(shot, secondArguments), Is.False,
                    "去重容量耗尽时必须确定性停止，不能扩容或继续产生重复命中。");
                Assert.That(shot.state.launched, Is.False);
                Assert.That(shot.resolvedColliderOverflowCount, Is.EqualTo(1));
                Assert.That(resolvedIdsField.GetValue(shot), Is.SameAs(preparedIds));
            }
            finally
            {
                Object.DestroyImmediate(shotObject);
                Object.DestroyImmediate(firstTarget);
                Object.DestroyImmediate(secondTarget);
            }
        }

        [Test]
        public void ShotQueryOverflow_StopsAtTheLastKnownBoundaryAfterPierce()
        {
            GameObject shotObject = new GameObject("OverflowBoundaryShot");
            GameObject targetObject = new GameObject("OverflowBoundaryTarget");
            Collider targetCollider = targetObject.AddComponent<BoxCollider>();

            try
            {
                Item item = shotObject.AddComponent<Item>();
                item.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(item);
                item.RegisterDomain(item.basicDomain);
                ItemShotModule shot = item.GetMoudle<ItemShotModule>();
                item.basicDomain.MyModules.ApplyBuffers(true);
                shot.Start();
                shot.SetHitSolver(new SaturatedPierceHitSolver(targetCollider));
                shot.state = new ShotMotionState { launched = true };

                System.Reflection.FieldInfo contextField = typeof(ItemShotModule).GetField(
                    "_launchContext",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.MethodInfo buildHit = typeof(ItemShotModule).GetMethod(
                    "TryBuildHitCandidate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(contextField, Is.Not.Null);
                Assert.That(buildHit, Is.Not.Null);
                contextField.SetValue(shot, new ESShotLaunchContext(
                    1,
                    null,
                    default,
                    null,
                    default,
                    hitResolver: AlwaysPierceHitResolver.Instance));
                object[] arguments =
                {
                    new ShotMotionResult
                    {
                        kind = ShotMotionKind.Moving,
                        previousPosition = Vector3.zero,
                        currentPosition = Vector3.forward * 10f,
                        velocity = Vector3.forward
                    }
                };

                buildHit.Invoke(shot, arguments);

                var result = (ShotMotionResult)arguments[0];
                Assert.That(shot.state.launched, Is.False);
                Assert.That(result.kind, Is.EqualTo(ShotMotionKind.Blocked));
                Assert.That(result.currentPosition, Is.EqualTo(Vector3.forward * 2f));
                Assert.That(shot.hitOverflowCount, Is.EqualTo(1));
                Assert.That(shot.hitOverflowStopCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(shotObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void MustHit_DoesNotSynthesizeTargetAfterPhysicalTargetHit()
        {
            GameObject shotObject = new GameObject("MustHitShot");
            GameObject targetObject = new GameObject("MustHitTarget");
            Collider targetCollider = targetObject.AddComponent<BoxCollider>();
            Entity target = targetObject.AddComponent<Entity>();

            try
            {
                Item item = shotObject.AddComponent<Item>();
                item.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(item);
                item.RegisterDomain(item.basicDomain);
                ItemShotModule shot = item.GetMoudle<ItemShotModule>();
                item.basicDomain.MyModules.ApplyBuffers(true);
                shot.Start();
                shot.sharedData = ItemShotSharedData.Default;
                shot.sharedData.blockMode = ShotBlockMode.WorldOnly;
                shot.aimMode = ShotAimMode.MustHit;
                shot.state = new ShotMotionState { launched = true };

                System.Reflection.FieldInfo targetField = typeof(ItemShotModule).GetField(
                    "_targetTransform",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.MethodInfo resolveHit = typeof(ItemShotModule).GetMethod(
                    "ResolveHit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.MethodInfo buildMustHit = typeof(ItemShotModule).GetMethod(
                    "TryBuildMustHitCandidate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(targetField, Is.Not.Null);
                Assert.That(resolveHit, Is.Not.Null);
                Assert.That(buildMustHit, Is.Not.Null);
                targetField.SetValue(shot, target.transform);

                int hitEventCount = 0;
                shot.LifecycleEvent += evt =>
                {
                    if (evt.kind == ESShotLifecycleKind.Hit)
                        hitEventCount++;
                };
                var physicalHit = new ShotHitCandidate
                {
                    collider = targetCollider,
                    point = targetObject.transform.position,
                    layer = ESPhysicsLayers.EntityHurtbox
                };
                var physicalResult = new ShotMotionResult
                {
                    kind = ShotMotionKind.Moving,
                    currentPosition = targetObject.transform.position,
                    velocity = Vector3.forward
                };
                object[] resolveArguments = { physicalHit, physicalResult };
                Assert.That((bool)resolveHit.Invoke(shot, resolveArguments), Is.True);
                Assert.That(hitEventCount, Is.EqualTo(1));

                var arrivedResult = (ShotMotionResult)resolveArguments[1];
                arrivedResult.kind = ShotMotionKind.Arrived;
                arrivedResult.hasHitCandidate = false;
                object[] mustHitArguments = { arrivedResult };
                buildMustHit.Invoke(shot, mustHitArguments);
                arrivedResult = (ShotMotionResult)mustHitArguments[0];

                Assert.That(arrivedResult.hasHitCandidate, Is.False);
                Assert.That(hitEventCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(shotObject);
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void WorldOnlyHitResolver_PiercesEntityAndStopsWorldBlocker()
        {
            GameObject hurtbox = new GameObject("Hurtbox");
            GameObject wall = new GameObject("Wall");
            Collider hurtboxCollider = hurtbox.AddComponent<BoxCollider>();
            Collider wallCollider = wall.AddComponent<BoxCollider>();
            hurtbox.AddComponent<Entity>();
            hurtbox.layer = ESPhysicsLayers.EntityHurtbox;
            wall.layer = ESPhysicsLayers.Wall;
            ItemShotSharedData definition = ItemShotSharedData.Default;
            definition.blockMode = ShotBlockMode.WorldOnly;
            Assert.That(definition.ValidateDefinition(out string definitionError), Is.True, definitionError);
            var context = new ESShotLaunchContext(
                1,
                null,
                default,
                null,
                default);

            try
            {
                Assert.That(
                    ESDefaultShotHitResolver.Instance.Resolve(
                        context,
                        definition,
                        new ShotHitCandidate
                        {
                            collider = hurtboxCollider,
                            layer = ESPhysicsLayers.EntityHurtbox
                        }),
                    Is.EqualTo(ESShotHitDecision.Pierce));
                Assert.That(
                    ESDefaultShotHitResolver.Instance.Resolve(
                        context,
                        definition,
                        new ShotHitCandidate
                        {
                            collider = wallCollider,
                            layer = ESPhysicsLayers.Wall
                        }),
                    Is.EqualTo(ESShotHitDecision.Stop));
            }
            finally
            {
                Object.DestroyImmediate(hurtbox);
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void NoneHitResolver_PiercesEntityAndIgnoresWorldBlocker()
        {
            GameObject hurtbox = new GameObject("Hurtbox");
            GameObject wall = new GameObject("Wall");
            Collider hurtboxCollider = hurtbox.AddComponent<BoxCollider>();
            Collider wallCollider = wall.AddComponent<BoxCollider>();
            hurtbox.AddComponent<Entity>();
            hurtbox.layer = ESPhysicsLayers.EntityHurtbox;
            wall.layer = ESPhysicsLayers.Wall;
            ItemShotSharedData definition = ItemShotSharedData.Default;
            definition.blockMode = ShotBlockMode.None;
            Assert.That(definition.ValidateDefinition(out string definitionError), Is.True, definitionError);
            var context = new ESShotLaunchContext(
                1,
                null,
                default,
                null,
                default);

            try
            {
                Assert.That(
                    ESDefaultShotHitResolver.Instance.Resolve(
                        context,
                        definition,
                        new ShotHitCandidate
                        {
                            collider = hurtboxCollider,
                            layer = ESPhysicsLayers.EntityHurtbox
                        }),
                    Is.EqualTo(ESShotHitDecision.Pierce));
                Assert.That(
                    ESDefaultShotHitResolver.Instance.Resolve(
                        context,
                        definition,
                        new ShotHitCandidate
                        {
                            collider = wallCollider,
                            layer = ESPhysicsLayers.Wall
                        }),
                    Is.EqualTo(ESShotHitDecision.Ignore));
            }
            finally
            {
                Object.DestroyImmediate(hurtbox);
                Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void TargetedShotSpawner_RejectsMissingTargetBeforePoolAccess()
        {
            var shotKey = new ESShotConfigKey { stringKey = "tests.shot.target.required" };
            var prefabKey = new ESAssetReferPrefabConfigKey
            {
                stringKey = "tests.shot.target.prefab",
                guid = "guid-tests-shot-target-prefab",
                assetTypeName = typeof(GameObject).FullName
            };
            ItemShotSharedData definition = ItemShotSharedData.Default;
            definition.aimMode = ShotAimMode.MustHit;

            ESRuntimeDataGameCore.Shots.BeginBuild();
            try
            {
                ESRuntimeDataGameCore.Shots.InjectWith(
                    shotKey,
                    definition,
                    ItemShotVariableData.Default,
                    prefabKey: prefabKey);
            }
            finally
            {
                ESRuntimeDataGameCore.Shots.EndBuild();
            }

            Assert.That(
                ESShotSpawner.TrySpawn(
                    shotKey,
                    Vector3.zero,
                    Vector3.forward,
                    new ESShotLaunchContext(801, null, default, null, default),
                    out ItemShotModule shot,
                    out string error),
                Is.False);
            Assert.That(shot, Is.Null);
            Assert.That(error, Does.Contain("必须提供有效目标"));
        }

        [Test]
        public void ShotSpawner_BorrowsActivePlanPrefabWithoutPerShotLease()
        {
            const string prefabBusinessKey = "tests.shot.prefab.runtime";
            var runtimeMap = ScriptableObject.CreateInstance<ESGlobalAssetRuntimeMap>();
            var provider = new ESRuntimeAssetLoader(
                runtimeMap,
                null,
                ESRuntimeRetryPolicy.Default,
                new ESRuntimeEditorDirectAssetProvider());
            var loadingService = new ESRuntimeDataAssetLoadingService();
            var catalog = new ESRuntimeCatalog();
            var prefab = new GameObject("RuntimeShotPrefab");
            GameObject managerObject = null;
            ItemShotModule spawnedShot = null;
            var prefabIdentity = new ESAssetIdentity("guid-tests-shot-runtime-prefab");
            bool activePlanRegistered = false;
            bool loadingDisposed = false;
            var lifecycle = new List<ESShotLifecycleKind>();
            var shotKey = new ESShotConfigKey { stringKey = "tests.shot.runtime.spawn" };
            var prefabKey = new ESAssetReferPrefabConfigKey
            {
                stringKey = prefabBusinessKey,
                guid = prefabIdentity.Guid,
                localFileId = prefabIdentity.LocalFileId,
                assetTypeName = typeof(GameObject).FullName
            };

            catalog.assets.Add(new ESRuntimeCatalogEntry
            {
                identity = new ESRuntimeCatalogIdentity { guid = "guid-tests-shot-runtime-prefab" },
                assetTypeName = typeof(GameObject).FullName,
                kind = ESAssetReferKind.Prefab.ToString(),
                stringKey = prefabBusinessKey,
                isBusinessAsset = true
            });

            try
            {
                Assert.That(ESAssets.IsReady, Is.False, "Shot spawn 测试要求独立资源环境。");
                Assert.That(ESGameManager.Instance, Is.Null, "Shot spawn 测试要求独立 GameManager 环境。");
                Assert.That(ESRuntimeDataModule.ShotInstanceTable.Count, Is.Zero);

                Item prefabItem = prefab.AddComponent<Item>();
                prefabItem.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(prefabItem);
                prefabItem.RegisterDomain(prefabItem.basicDomain);
                ItemShotModule prefabShot = prefabItem.GetMoudle<ItemShotModule>();
                prefabItem.basicDomain.MyModules.ApplyBuffers(true);
                Assert.That(prefabShot, Is.Not.Null, "测试 Shot Prefab 没有创建 ItemShotModule。");
                Assert.That(prefabItem.basicDomain.FindMyModule<ItemShotModule>(), Is.SameAs(prefabShot));

                InitializeAssetLoadingForTest(
                    loadingService,
                    provider,
                    () => ESRuntimeDataAsset.RebuildAssetConfigTablesFromCatalogs(new[] { catalog }));
                SetLoadedPrefabForTest(prefabBusinessKey, prefab);
                ESAssets.RegisterActivePlanAsset(prefabIdentity, prefab);
                activePlanRegistered = true;

                ItemShotSharedData definition = ItemShotSharedData.Default;
                definition.enabled = true;
                definition.aimMode = ShotAimMode.Free;
                definition.blockMode = ShotBlockMode.AnyBlocker;
                ESRuntimeDataGameCore.Shots.BeginBuild();
                int runtimeKey;
                try
                {
                    runtimeKey = ESRuntimeDataGameCore.Shots.InjectWith(
                        shotKey,
                        definition,
                        ItemShotVariableData.Default,
                        prefabKey: prefabKey);
                }
                finally
                {
                    ESRuntimeDataGameCore.Shots.EndBuild();
                }

                managerObject = new GameObject("ShotSpawnerRuntimeManager");
                ESGameManager manager = managerObject.AddComponent<ESGameManager>();
                manager.dontDestroyOnLoad = false;
                manager.autoCreateCommandModule = false;
                manager.autoCreateInputModule = false;
                manager.autoCreateAudioModule = false;
                manager.autoCreateVfxModule = false;
                manager.autoCreateCameraModule = false;
                manager.autoCreatePhysicsQueryModule = false;
                manager.autoCreateLODModule = false;
                manager.autoCreateWorldMapModule = false;
                InvokeAwakeForEditModeTest(manager);
                Assert.That(
                    ESGameManager.TryGetModule(out ESRuntimeDataModule runtimeDataModule),
                    Is.True,
                    "测试 GameManager 没有注册 RuntimeData 模块。");
                Assert.That(runtimeDataModule, Is.Not.Null);
                Assert.That(
                    ESGameManager.TryGetModule(out ESGameObjectPoolModule poolModule),
                    Is.True,
                    "测试 GameManager 没有注册 Pool 模块。");
                Assert.That(poolModule, Is.Not.Null);
                int assetReaderCountBeforeSpawn = ESRuntimeDataAsset.ActiveAssetConfigReaderCount;

                var context = new ESShotLaunchContext(
                    701,
                    null,
                    default,
                    null,
                    default,
                    lifecycleObserver: evt => lifecycle.Add(evt.kind));
                Assert.That(
                    ESShotSpawner.TrySpawn(
                        shotKey,
                        new Vector3(10000f, 10000f, 10000f),
                        Vector3.forward,
                        context,
                        out spawnedShot,
                        out string error),
                    Is.True,
                    error);

                Assert.That(spawnedShot, Is.Not.Null, "ShotSpawner 成功返回但 Shot 模块为空。");
                Assert.That(spawnedShot.sharedData, Is.Not.SameAs(definition));
                Assert.That(spawnedShot.sharedData.speed, Is.EqualTo(definition.speed));
                Assert.That(spawnedShot.RuntimeInstanceHandle.IsValid, Is.True);
                spawnedShot.Start();
                Assert.That(spawnedShot.sharedData, Is.Not.SameAs(definition),
                    "Shot 必须持有提交时冻结的 Prepared RuntimeData，而不是作者对象。");
                Assert.That(spawnedShot.RuntimeInstanceHandle.IsValid, Is.True,
                    "首次池化实例的 Start 不得终止已发射的 Shot 生命周期。");
                Assert.That(ESRuntimeDataModule.ShotInstanceTable.Count, Is.EqualTo(1));
                Assert.That(ESRuntimeDataModule.ShotInstanceTable.TryGetInstance(
                    spawnedShot.RuntimeInstanceHandle,
                    out Item registeredItem), Is.True);
                Assert.That(registeredItem, Is.SameAs(spawnedShot.MyCore));
                Assert.That(lifecycle, Is.EqualTo(new[] { ESShotLifecycleKind.Launched }));
                Assert.That(
                    ESRuntimeDataAsset.ActiveAssetConfigReaderCount,
                    Is.EqualTo(assetReaderCountBeforeSpawn),
                    "每发 Shot 不得取得独立 AssetConfig PayloadLease。");
                Assert.That(poolModule.TryGetStats(prefab, out ESGameObjectPoolStats activeStats), Is.True);
                Assert.That(activeStats.activeCount, Is.EqualTo(1));

                spawnedShot.Internal_Stop();

                Assert.That(lifecycle, Is.EqualTo(new[]
                {
                    ESShotLifecycleKind.Launched,
                    ESShotLifecycleKind.Stopped,
                    ESShotLifecycleKind.Despawned
                }));
                Assert.That(ESRuntimeDataModule.ShotInstanceTable.Count, Is.Zero);
                Assert.That(
                    ESRuntimeDataAsset.ActiveAssetConfigReaderCount,
                    Is.EqualTo(assetReaderCountBeforeSpawn));
                Assert.That(poolModule.TryGetStats(prefab, out ESGameObjectPoolStats returnedStats), Is.True);
                Assert.That(returnedStats.activeCount, Is.Zero);

                lifecycle.Clear();
                Assert.That(
                    ESShotSpawner.TrySpawn(
                        shotKey,
                        new Vector3(10000f, 10000f, 10000f),
                        Vector3.forward,
                        context,
                        out spawnedShot,
                        out error),
                    Is.True,
                    error);
                ESAssets.UnregisterActivePlanAsset(prefabIdentity);
                activePlanRegistered = false;
                Assert.That(lifecycle, Is.EqualTo(new[]
                {
                    ESShotLifecycleKind.Launched,
                    ESShotLifecycleKind.Stopped,
                    ESShotLifecycleKind.Despawned
                }));
                Assert.That(ESRuntimeDataModule.ShotInstanceTable.Count, Is.Zero);
                Assert.That(
                    ESRuntimeDataAsset.ActiveAssetConfigReaderCount,
                    Is.EqualTo(assetReaderCountBeforeSpawn));

                loadingService.Dispose();
                loadingDisposed = true;
                Assert.That(loadingService.IsInitialized, Is.False);
                Assert.That(runtimeKey, Is.GreaterThan(0));
            }
            finally
            {
                if (spawnedShot != null && spawnedShot.state.launched)
                    spawnedShot.Internal_Stop(false);
                if (managerObject != null)
                    Object.DestroyImmediate(managerObject);
                if (activePlanRegistered)
                    ESAssets.UnregisterActivePlanAsset(prefabIdentity);
                if (!loadingDisposed && loadingService.IsInitialized
                    && ESRuntimeDataAsset.ActiveAssetConfigReaderCount == 0)
                    loadingService.Dispose();
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(runtimeMap);
                ESRuntimeDataModule.ShotInstanceTable.Clear();
            }
        }

        [Test]
        public void ItemGameCoreProjectionConflict_RollsBackNewBaseProjection()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.weapon.rollback" };
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.rollback" };
            ESRuntimeDataGameCore.Weapons.BeginBuild();
            ESWeaponRuntimeData existingWeapon;
            try
            {
                int existingRuntimeKey = ESRuntimeDataGameCore.Weapons.InjectWith(
                    weaponKey,
                    ItemWeaponSharedData.Default,
                    ItemWeaponVariableData.Default,
                    prefabKey: CreateWeaponPrefabKey("tests.prefab.weapon.rollback"));
                Assert.That(
                    ESRuntimeDataGameCore.Weapons.TryGet(existingRuntimeKey, out existingWeapon),
                    Is.True);
            }
            finally
            {
                ESRuntimeDataGameCore.Weapons.EndBuild();
            }

            ItemDataInfo info = CreateWeaponInfo(itemKey, weaponKey);
            try
            {
                Assert.Throws<System.InvalidOperationException>(() => ESItemGameCoreTable.Inject(info));
                Assert.That(ESRuntimeDataGameCore.Items.TryGet(itemKey, out _), Is.False);
                Assert.That(ESRuntimeDataGameCore.Weapons.TryGet(weaponKey, out ESWeaponRuntimeData current), Is.True);
                Assert.That(current, Is.SameAs(existingWeapon));
                Assert.That(current.Ready, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        [Test]
        public void ItemAndWeaponProjection_ClearReleasesBothRetainedPayloads()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.weapon.release" };
            var weaponKey = new ESWeaponConfigKey { stringKey = "tests.weapon.release" };
            ItemDataInfo info = CreateWeaponInfo(itemKey, weaponKey);
            try
            {
                ESRuntimeDataGameCore.Weapons.BeginBuild();
                try
                {
                    ESRuntimeDataGameCore.Weapons.InjectWith(
                        new ESWeaponConfigKey { stringKey = "tests.weapon.release.padding" },
                        ItemWeaponSharedData.Default,
                        ItemWeaponVariableData.Default,
                        prefabKey: CreateWeaponPrefabKey("tests.prefab.weapon.release-padding"));
                }
                finally
                {
                    ESRuntimeDataGameCore.Weapons.EndBuild();
                }

                ESItemGameCoreTable.Inject(info);
                Assert.That(ESRuntimeDataGameCore.Items.TryGet(itemKey, out ESItemRuntimeData itemData), Is.True);
                Assert.That(ESRuntimeDataGameCore.Weapons.TryGet(weaponKey, out ESWeaponRuntimeData weaponData), Is.True);
                Assert.That(itemData.Ready, Is.True);
                Assert.That(weaponData.Ready, Is.True);
                Assert.That(itemData.runtimeKey, Is.Not.EqualTo(weaponData.runtimeKey));

                ClearDefinitionTables();

                Assert.That(itemData.Ready, Is.False);
                Assert.That(itemData.soSource, Is.Null);
                Assert.That(itemData.baseConfig, Is.Null);
                Assert.That(itemData.tags, Is.Null);
                Assert.That(itemData.weaponKey, Is.Null);
                Assert.That(weaponData.Ready, Is.False);
                Assert.That(weaponData.soSource, Is.Null);
                Assert.That(weaponData.sharedData, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        [Test]
        public void NormalItem_InjectsOnlyBaseItemProjection()
        {
            var itemKey = new ESItemConfigKey { stringKey = "tests.item.prop.base_only" };
            ItemDataInfo info = ScriptableObject.CreateInstance<ItemDataInfo>();
            info.itemKey = itemKey;
            info.baseConfig.kind = ItemKind.Prop;
            info.EnsureActiveKindData();

            try
            {
                ESItemGameCoreTable.Inject(info);

                Assert.That(ESRuntimeDataGameCore.Items.TryGet(itemKey, out ESItemRuntimeData itemData), Is.True);
                Assert.That(itemData.Ready, Is.True);
                Assert.That(itemData.kind, Is.EqualTo(ItemKind.Prop));
                Assert.That(ESRuntimeDataGameCore.Shots.Count, Is.Zero);
                Assert.That(ESRuntimeDataGameCore.Weapons.Count, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        private sealed class EverySecondShotTickPolicy : IItemShotTickPolicy
        {
            private int _calls;

            public bool ShouldTick(in ShotMotionState state, int frameCount)
            {
                _calls++;
                return (_calls & 1) == 0;
            }
        }

        private sealed class AlwaysPierceHitResolver : IESShotHitResolver
        {
            public static readonly AlwaysPierceHitResolver Instance = new AlwaysPierceHitResolver();

            public ESShotHitDecision Resolve(
                in ESShotLaunchContext context,
                ItemShotSharedData definition,
                in ShotHitCandidate candidate)
            {
                return ESShotHitDecision.Pierce;
            }
        }

        private sealed class SaturatedPierceHitSolver : IItemShotHitSolver
        {
            private readonly Collider _collider;

            public SaturatedPierceHitSolver(Collider collider)
            {
                _collider = collider;
            }

            public bool IsOverflow => true;

            public int Query(in ItemShotHitQuery query, ShotHitCandidate[] results, int maxResults)
            {
                if (_collider == null || results == null || maxResults <= 0)
                    return 0;

                results[0] = new ShotHitCandidate
                {
                    collider = _collider,
                    point = Vector3.forward * 2f,
                    normal = Vector3.back,
                    distance = 2f,
                    layer = _collider.gameObject.layer,
                    isTrigger = _collider.isTrigger
                };
                return 1;
            }
        }

        private static ItemDataInfo CreateWeaponInfo(
            ESItemConfigKey itemKey,
            ESWeaponConfigKey weaponKey)
        {
            ItemDataInfo info = ScriptableObject.CreateInstance<ItemDataInfo>();
            info.itemKey = itemKey;
            info.baseConfig.kind = ItemKind.Weapon;
            info.baseConfig.prefabKey = CreateWeaponPrefabKey("tests.prefab." + weaponKey.StringKey);
            info.EnsureActiveKindData();
            ItemWeaponDataBlock weapon = (ItemWeaponDataBlock)info.kindData;
            weapon.key = weaponKey;
            weapon.sharedData = ItemWeaponSharedData.Default;
            weapon.initialState = ItemWeaponVariableData.Default;
            return info;
        }

        private static void ClearDefinitionTables()
        {
            if (ESRuntimeDataGameCore.Items.IsBuilding
                || ESRuntimeDataGameCore.Shots.IsBuilding
                || ESRuntimeDataGameCore.Weapons.IsBuilding)
            {
                Assert.Fail("Item/Shot/Weapon definition table leaked an active build transaction.");
            }
            ESRuntimeDataGameCore.Items.BeginBuild(true);
            ESRuntimeDataGameCore.Shots.BeginBuild(true);
            ESRuntimeDataGameCore.Weapons.BeginBuild(true);
            ESRuntimeDataGameCore.Weapons.EndBuild();
            ESRuntimeDataGameCore.Shots.EndBuild();
            ESRuntimeDataGameCore.Items.EndBuild();
        }

        private static void InitializeAssetLoadingForTest(
            ESRuntimeDataAssetLoadingService loadingService,
            ESRuntimeAssetLoader provider,
            System.Action rebuildTables)
        {
            System.Reflection.MethodInfo initialize = null;
            System.Reflection.MethodInfo[] methods = typeof(ESRuntimeDataAssetLoadingService).GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            for (int index = 0; index < methods.Length; index++)
            {
                System.Reflection.MethodInfo candidate = methods[index];
                if (candidate.Name == "InitializeAsync" && candidate.GetParameters().Length == 3)
                {
                    initialize = candidate;
                    break;
                }
            }

            Assert.That(initialize, Is.Not.Null, "未找到 RuntimeData 资产加载服务的原子初始化入口。");
            object pending = initialize.Invoke(
                loadingService,
                new object[] { provider, rebuildTables, CancellationToken.None });
            System.Type pendingType = pending?.GetType();
            System.Type extensionsType = pendingType?.Assembly.GetType(
                "Cysharp.Threading.Tasks.UniTaskExtensions");
            System.Reflection.MethodInfo asTask = null;
            System.Reflection.MethodInfo[] extensionMethods = extensionsType?.GetMethods(
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public)
                ?? System.Array.Empty<System.Reflection.MethodInfo>();
            for (int index = 0; index < extensionMethods.Length; index++)
            {
                System.Reflection.MethodInfo candidate = extensionMethods[index];
                System.Reflection.ParameterInfo[] parameters = candidate.GetParameters();
                if (candidate.Name == "AsTask"
                    && !candidate.IsGenericMethod
                    && parameters.Length == 1
                    && parameters[0].ParameterType == pendingType)
                {
                    asTask = candidate;
                    break;
                }
            }
            Assert.That(asTask, Is.Not.Null, "RuntimeData 资产加载服务没有返回可等待的 UniTask。");
            object task = asTask.Invoke(null, new[] { pending });
            Assert.That(task, Is.Not.Null, "UniTask.AsTask 反射调用返回了空对象。");
            System.Reflection.MethodInfo getAwaiter = task.GetType().GetMethod(
                "GetAwaiter",
                System.Type.EmptyTypes);
            Assert.That(getAwaiter, Is.Not.Null, "UniTask.AsTask 返回值没有可等待入口。");
            object awaiter = getAwaiter.Invoke(task, null);
            System.Reflection.MethodInfo getResult = awaiter?.GetType().GetMethod(
                "GetResult",
                System.Type.EmptyTypes);
            Assert.That(getResult, Is.Not.Null, "Task awaiter 没有 GetResult 入口。");
            getResult.Invoke(awaiter, null);
        }

        private static void InvokeAwakeForEditModeTest(ESGameManager manager)
        {
            System.Reflection.MethodInfo awake = typeof(ESGameManager).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null, "ESGameManager 没有可供 EditMode 测试调用的 Awake 生命周期入口。");
            awake.Invoke(manager, null);
        }

        private static void SetLoadedPrefabForTest(string businessKey, GameObject prefab)
        {
            object reader = ESRuntimeDataAsset.Prefabs;
            System.Reflection.MethodInfo acquire = null;
            System.Reflection.MethodInfo[] methods = reader.GetType().GetMethods(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
            for (int index = 0; index < methods.Length; index++)
            {
                System.Reflection.MethodInfo candidate = methods[index];
                if (candidate.Name == "TryAcquireConfigData" && candidate.GetParameters().Length == 2)
                {
                    acquire = candidate;
                    break;
                }
            }

            Assert.That(acquire, Is.Not.Null, "未找到 Prefab ConfigData 测试读入口。");
            var arguments = new object[] { businessKey, null };
            Assert.That((bool)acquire.Invoke(reader, arguments), Is.True);
            object lease = arguments[1];
            Assert.That(lease, Is.Not.Null);
            try
            {
                System.Reflection.PropertyInfo dataProperty = lease.GetType().GetProperty(
                    "Data",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic);
                object configData = dataProperty?.GetValue(lease);
                Assert.That(configData, Is.Not.Null);
                System.Reflection.MethodInfo setLoadedAsset = configData.GetType().GetMethod(
                    "SetLoadedAsset",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public);
                Assert.That(setLoadedAsset, Is.Not.Null);
                setLoadedAsset.Invoke(configData, new object[] { prefab });
            }
            finally
            {
                (lease as System.IDisposable)?.Dispose();
            }
        }
    }
}
