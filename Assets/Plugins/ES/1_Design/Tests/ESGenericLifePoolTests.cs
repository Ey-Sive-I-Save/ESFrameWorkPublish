using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class ESGenericLifePoolTests
    {
        [TearDown]
        public void TearDown()
        {
            ESGenericLifePoolTestReceiver.ThrowOnSpawn = false;
            ESGenericLifePoolTestReceiver.ThrowOnDespawn = false;
            ESGenericLifePoolTestReceiver.SpawnAction = null;
            ESGenericLifePoolTestReceiver.DespawnAction = null;
            ESGenericLifePoolTestReceiver.LastSpawned = null;
            ESGenericLifePoolTestReceiver.Calls.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void Pool_NewInstance_DispatchesSpawnOnlyWhileInactiveAfterDespawnBaseline()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_New");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                GameObject instance = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(instance, Is.Not.Null);

                ESGenericLifePoolTestReceiver receiver = instance.GetComponent<ESGenericLifePoolTestReceiver>();
                Assert.That(receiver.spawnCount, Is.EqualTo(1));
                Assert.That(receiver.despawnCount, Is.EqualTo(1), "A new instance must first establish the inactive Despawn baseline.");
                Assert.That(receiver.spawnedOnlyWhileInactive, Is.True);
                Assert.That(pool.PushToPool(instance), Is.True);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_PrewarmAndRepeatedBorrowReturn_KeepOneTrackedLifecycle()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_Prefab");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                pool.Prewarm(prefab, 1, "tests.generic-life.prewarm");
                GameObject first = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(first, Is.Not.Null);

                ESGenericLifePoolTestReceiver receiver = first.GetComponent<ESGenericLifePoolTestReceiver>();
                Assert.That(receiver.spawnCount, Is.EqualTo(1));
                Assert.That(receiver.spawnedOnlyWhileInactive, Is.True);
                Assert.That(receiver.despawnCount, Is.EqualTo(1), "Prewarm must first establish one inactive Despawn baseline.");

                Assert.That(pool.PushToPool(first), Is.True);
                Assert.That(receiver.despawnCount, Is.EqualTo(2));

                GameObject second = pool.GetInPool(prefab, Vector3.one, Quaternion.identity);
                Assert.That(second, Is.SameAs(first));
                Assert.That(receiver.spawnCount, Is.EqualTo(2));
                Assert.That(pool.PushToPool(second), Is.True);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_LifecycleCallbackFailures_NeverReuseAnUntrustedInstance()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_Exception");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                ESGenericLifePoolTestReceiver.ThrowOnSpawn = true;
                LogAssert.ignoreFailingMessages = true;
                Assert.That(pool.GetInPool(prefab, Vector3.zero, Quaternion.identity), Is.Null);

                ESGenericLifePoolTestReceiver.ThrowOnSpawn = false;
                GameObject recovered = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(recovered, Is.Not.Null, "A callback failure must not lose an instance between active and inactive tracking.");

                ESGenericLifePoolTestReceiver.ThrowOnDespawn = true;
                Assert.That(pool.PushToPool(recovered), Is.True);
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats afterFailedDespawn), Is.True);
                Assert.That(afterFailedDespawn.activeCount, Is.Zero);
                Assert.That(afterFailedDespawn.inactiveCount, Is.Zero);
                Assert.That(afterFailedDespawn.createdCount, Is.Zero);

                ESGenericLifePoolTestReceiver.ThrowOnDespawn = false;
                GameObject recoveredAfterDespawnFailure = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(recoveredAfterDespawnFailure, Is.Not.Null);
                Assert.That(recoveredAfterDespawnFailure, Is.Not.SameAs(recovered), "A Despawn failure must discard the untrusted instance instead of reusing it.");
                Assert.That(pool.PushToPool(recoveredAfterDespawnFailure), Is.True);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_NewInstanceWithFailedDespawnBaseline_IsDiscarded()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_FailedBaseline");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                ESGenericLifePoolTestReceiver.ThrowOnDespawn = true;
                LogAssert.ignoreFailingMessages = true;
                Assert.That(pool.GetInPool(prefab, Vector3.zero, Quaternion.identity), Is.Null);
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats failedBaseline), Is.True);
                Assert.That(failedBaseline.totalCount, Is.Zero);
                Assert.That(failedBaseline.createdCount, Is.Zero);

                ESGenericLifePoolTestReceiver.ThrowOnDespawn = false;
                Assert.That(pool.GetInPool(prefab, Vector3.zero, Quaternion.identity), Is.Not.Null);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_FailedAdditionalInstanceBaseline_DoesNotDecrementExistingCreatedCount()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_FailedAdditionalBaseline");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                GameObject first = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(first, Is.Not.Null);

                ESGenericLifePoolTestReceiver.ThrowOnDespawn = true;
                LogAssert.ignoreFailingMessages = true;
                Assert.That(pool.GetInPool(prefab, Vector3.one, Quaternion.identity), Is.Null);

                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats afterFailedAdditionalBaseline), Is.True);
                Assert.That(afterFailedAdditionalBaseline.activeCount, Is.EqualTo(1));
                Assert.That(afterFailedAdditionalBaseline.inactiveCount, Is.Zero);
                Assert.That(afterFailedAdditionalBaseline.totalCount, Is.EqualTo(1));
                Assert.That(afterFailedAdditionalBaseline.createdCount, Is.EqualTo(1));

                ESGenericLifePoolTestReceiver.ThrowOnDespawn = false;
                Assert.That(pool.PushToPool(first), Is.True);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_ClearActiveInstance_DispatchesDespawnBeforeTerminalDestroy()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_ClearActive");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                GameObject instance = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(instance, Is.Not.Null);

                ESGenericLifePoolTestReceiver receiver = instance.GetComponent<ESGenericLifePoolTestReceiver>();
                Assert.That(receiver.despawnCount, Is.EqualTo(1));

                pool.Clear(prefab);

                Assert.That(receiver.despawnCount, Is.EqualTo(2), "Clear must close the active Spawn with exactly one Despawn before destruction.");
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats afterClear), Is.True);
                Assert.That(afterClear.activeCount, Is.Zero);
                Assert.That(afterClear.inactiveCount, Is.Zero);
                Assert.That(afterClear.createdCount, Is.Zero);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_ClearInactiveInstance_DoesNotDispatchDuplicateDespawn()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_ClearInactive");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                GameObject instance = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(instance, Is.Not.Null);

                ESGenericLifePoolTestReceiver receiver = instance.GetComponent<ESGenericLifePoolTestReceiver>();
                Assert.That(pool.PushToPool(instance), Is.True);
                int despawnCountAfterReturn = receiver.despawnCount;

                pool.Clear(prefab);

                Assert.That(receiver.despawnCount, Is.EqualTo(despawnCountAfterReturn), "An inactive instance has already completed Despawn and must not receive it again during Clear.");
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats afterClear), Is.True);
                Assert.That(afterClear.totalCount, Is.Zero);
                Assert.That(afterClear.createdCount, Is.Zero);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_ClearActiveInstanceWithFailedDespawn_StillClosesBookkeeping()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_ClearFailedDespawn");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                GameObject instance = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(instance, Is.Not.Null);

                ESGenericLifePoolTestReceiver receiver = instance.GetComponent<ESGenericLifePoolTestReceiver>();
                ESGenericLifePoolTestReceiver.ThrowOnDespawn = true;
                LogAssert.ignoreFailingMessages = true;

                pool.Clear(prefab);

                Assert.That(receiver.despawnCount, Is.EqualTo(2), "A throwing receiver must still be invoked once for the terminal Despawn edge.");
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats afterClear), Is.True);
                Assert.That(afterClear.activeCount, Is.Zero);
                Assert.That(afterClear.inactiveCount, Is.Zero);
                Assert.That(afterClear.createdCount, Is.Zero);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_ClearRejectsReentrantRentFromDespawnCallback()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_ClearReentrantRent");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                GameObject instance = pool.GetInPool(prefab, Vector3.zero, Quaternion.identity);
                Assert.That(instance, Is.Not.Null);

                GameObject reentrantRent = instance;
                ESGenericLifePoolTestReceiver.DespawnAction = () =>
                    reentrantRent = pool.GetInPool(prefab, Vector3.one, Quaternion.identity);

                pool.Clear(prefab);

                Assert.That(reentrantRent, Is.Null, "Clear must not allow a Despawn callback to repopulate the group it is terminating.");
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats afterClear), Is.True);
                Assert.That(afterClear.totalCount, Is.Zero);
                Assert.That(afterClear.createdCount, Is.Zero);
            }
            finally
            {
                ESGenericLifePoolTestReceiver.DespawnAction = null;
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_ClearRequestedDuringSpawn_DefersTerminalDespawnUntilSpawnCompletes()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_ClearDuringSpawn");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                bool insideSpawn = false;
                bool despawnReenteredSpawn = false;
                int despawnCountWhenClearReturned = -1;
                ESGenericLifePoolTestReceiver.DespawnAction = () => despawnReenteredSpawn |= insideSpawn;
                ESGenericLifePoolTestReceiver.SpawnAction = () =>
                {
                    insideSpawn = true;
                    pool.Clear(prefab);
                    despawnCountWhenClearReturned = ESGenericLifePoolTestReceiver.LastSpawned.despawnCount;
                    insideSpawn = false;
                };

                Assert.That(pool.GetInPool(prefab, Vector3.zero, Quaternion.identity), Is.Null);

                ESGenericLifePoolTestReceiver receiver = ESGenericLifePoolTestReceiver.LastSpawned;
                Assert.That(receiver, Is.Not.Null);
                Assert.That(despawnCountWhenClearReturned, Is.EqualTo(1), "Clear called inside Spawn must defer instead of re-entering Despawn.");
                Assert.That(despawnReenteredSpawn, Is.False);
                Assert.That(receiver.despawnCount, Is.EqualTo(2), "The deferred terminal pass must close the completed Spawn exactly once.");
                Assert.That(pool.TryGetStats(prefab, out ESGameObjectPoolStats afterClear), Is.True);
                Assert.That(afterClear.totalCount, Is.Zero);
                Assert.That(afterClear.createdCount, Is.Zero);
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Pool_ClearAllRequestedDuringSpawn_DefersAndRemovesAllGroups()
        {
            GameObject prefab = CreatePoolPrefab("ESGenericLifePoolTests_ClearAllDuringSpawn");
            ESGameObjectPoolModule pool = new ESGameObjectPoolModule();
            try
            {
                ESGenericLifePoolTestReceiver.SpawnAction = pool.ClearAll;

                Assert.That(pool.GetInPool(prefab, Vector3.zero, Quaternion.identity), Is.Null);

                ESGenericLifePoolTestReceiver receiver = ESGenericLifePoolTestReceiver.LastSpawned;
                Assert.That(receiver, Is.Not.Null);
                Assert.That(receiver.despawnCount, Is.EqualTo(2));
                Assert.That(pool.TryGetStats(prefab, out _), Is.False, "Deferred ClearAll must remove the terminated group from lookup tables.");
            }
            finally
            {
                pool.ClearAll();
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void GenericLife_RegistersTypeUniquePoolExtensions_AndUsesDefinedOrder()
        {
            GameObject root = new GameObject("ESGenericLifePoolTests_Root");
            try
            {
                ESGenericLifePoolTestReceiver receiver = root.AddComponent<ESGenericLifePoolTestReceiver>();
                ESGenericLife life = root.AddComponent<ESGenericLife>();
                ESGenericLifePoolTestExtension extension = new ESGenericLifePoolTestExtension();

                Assert.That(life.BindPoolRoot(receiver), Is.True);
                Assert.That(life.RegisterPoolExtension(extension), Is.True);
                Assert.That(life.RegisterPoolExtension(new ESGenericLifePoolTestExtension()), Is.False);

                Assert.That(InvokePoolDispatch(life, "NotifyPoolSpawned"), Is.True);
                Assert.That(InvokePoolDispatch(life, "NotifyPoolDespawned"), Is.True);
                CollectionAssert.AreEqual(
                    new[] { "root.spawn", "extension.spawn", "extension.despawn", "root.despawn" },
                    ESGenericLifePoolTestReceiver.Calls);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GenericLife_RejectsMultipleUnregisteredRootReceivers()
        {
            GameObject root = new GameObject("ESGenericLifePoolTests_Multiple");
            try
            {
                root.AddComponent<ESGenericLifePoolTestReceiver>();
                root.AddComponent<ESGenericLifePoolTestSecondReceiver>();
                LogAssert.ignoreFailingMessages = true;

                ESGenericLife life = InvokeEnsureForPooledRoot(root);
                Assert.That(life, Is.Not.Null);
                Assert.That(InvokePoolDispatch(life, "NotifyPoolSpawned"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GenericLife_RejectsSerializedPoolRootFromAnotherGameObject()
        {
            GameObject root = new GameObject("ESGenericLifePoolTests_Local");
            GameObject foreign = new GameObject("ESGenericLifePoolTests_Foreign");
            try
            {
                ESGenericLife life = root.AddComponent<ESGenericLife>();
                foreign.AddComponent<ESGenericLifePoolTestReceiver>();
                FieldInfo field = typeof(ESGenericLife).GetField("poolRootLifecycleComponent", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(life, foreign.GetComponent<ESGenericLifePoolTestReceiver>());

                LogAssert.ignoreFailingMessages = true;
                Assert.That(life.ValidatePoolLifecycle(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(foreign);
            }
        }

        private static GameObject CreatePoolPrefab(string name)
        {
            GameObject prefab = new GameObject(name);
            prefab.SetActive(false);
            prefab.AddComponent<ESGenericLifePoolTestReceiver>();
            return prefab;
        }

        private static bool InvokePoolDispatch(ESGenericLife life, string methodName)
        {
            MethodInfo method = typeof(ESGenericLife).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (bool)method.Invoke(life, null);
        }

        private static ESGenericLife InvokeEnsureForPooledRoot(GameObject root)
        {
            MethodInfo method = typeof(ESGenericLife).GetMethod("EnsureForPooledRoot", BindingFlags.Static | BindingFlags.NonPublic);
            return (ESGenericLife)method.Invoke(null, new object[] { root });
        }
    }

    public sealed class ESGenericLifePoolTestReceiver : MonoBehaviour, IESGameObjectPoolLifecycle
    {
        public static readonly List<string> Calls = new List<string>();
        public static bool ThrowOnSpawn;
        public static bool ThrowOnDespawn;
        public static System.Action SpawnAction;
        public static System.Action DespawnAction;
        public static ESGenericLifePoolTestReceiver LastSpawned;

        public int spawnCount;
        public int despawnCount;
        public bool spawnedOnlyWhileInactive = true;

        public void OnPoolSpawned()
        {
            spawnCount++;
            spawnedOnlyWhileInactive &= !gameObject.activeSelf;
            Calls.Add("root.spawn");
            LastSpawned = this;
            SpawnAction?.Invoke();
            if (ThrowOnSpawn)
                throw new System.InvalidOperationException("Pool spawn test exception.");
        }

        public void OnPoolDespawned()
        {
            despawnCount++;
            Calls.Add("root.despawn");
            DespawnAction?.Invoke();
            if (ThrowOnDespawn)
                throw new System.InvalidOperationException("Pool despawn test exception.");
        }
    }

    public sealed class ESGenericLifePoolTestSecondReceiver : MonoBehaviour, IESGameObjectPoolLifecycle
    {
        public void OnPoolSpawned() { }
        public void OnPoolDespawned() { }
    }

    public sealed class ESGenericLifePoolTestExtension : IESGameObjectPoolLifecycle
    {
        public void OnPoolSpawned()
        {
            ESGenericLifePoolTestReceiver.Calls.Add("extension.spawn");
        }

        public void OnPoolDespawned()
        {
            ESGenericLifePoolTestReceiver.Calls.Add("extension.despawn");
        }
    }
}
