using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class ESTagCatalogRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetRuntimeCatalogForTest();
        }

        [TearDown]
        public void TearDown()
        {
            ResetRuntimeCatalogForTest();
        }

        [Test]
        public void TagCatalog_EnforcesRuntimeAvailabilityAndIndependentLeases()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.tag.lease", 4096, ESTagAvailability.Runtime);
            ESTagBakeTable changedSchema = CreateCatalog("tests.tag.changed", 4096, ESTagAvailability.Runtime);
            ESTagBakeTable changedLayout = CreateCatalog("tests.tag.lease", 4097, ESTagAvailability.Runtime);
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_Entity");
            GameObject nextEntityObject = null;
            try
            {
                Assert.That(catalog.TryValidate(out string validationError), Is.True, validationError);
                Assert.That(catalog.TryGetRuntimeKey("tests.tag.lease", out int extensionRuntimeKey), Is.True);
                Assert.That(extensionRuntimeKey, Is.EqualTo(4096));

                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                Assert.That(ESTagRuntimeCatalog.TryGetRuntimeKey(ESTagStableReference.FromString("tests.tag.lease"), out int runtimeKey), Is.True);
                Assert.That(runtimeKey, Is.EqualTo(extensionRuntimeKey));
                Assert.That(ESTagRuntimeCatalog.TryGetDeprecatedReplacement(ESTagStableReference.FromString("tests.deprecated"), out ESTagStableReference replacement), Is.True);
                Assert.That(replacement, Is.EqualTo(ESTagStableReference.FromString("tests.tag.lease")));

                using (var genericTags = new ESTagCollection())
                {
                    Assert.That(genericTags.TryAcquireStringKey("tests.tag.lease", new object(), out ESTagLease genericLease), Is.True);
                    Assert.That(genericTags.GetCount(ESTagId.FromInt32(extensionRuntimeKey)), Is.EqualTo(1));
                    Assert.That(genericTags.TryAcquireStringKey("tests.deprecated", new object(), out _), Is.False);
                    genericLease.Dispose();
                    Assert.That(genericTags.GetCount(ESTagId.FromInt32(extensionRuntimeKey)), Is.Zero);
                }

                Entity entity = entityObject.AddComponent<Entity>();
                object buffSource = new object();
                object equipmentSource = new object();
                Assert.That(entity.Tags.TryAcquireStringKey("tests.tag.lease", buffSource, out ESTagLease firstBuffLease), Is.True);
                Assert.That(entity.Tags.TryAcquireStringKey("tests.tag.lease", buffSource, out ESTagLease secondBuffLease), Is.True);
                Assert.That(entity.Tags.TryAcquireStringKey("tests.tag.lease", equipmentSource, out ESTagLease equipmentLease), Is.True);
                ESTagId extensionTag = ESTagId.FromInt32(extensionRuntimeKey);

                Assert.That(entity.Tags.GetCount(extensionTag), Is.EqualTo(3));
                Assert.That(entity.GetGameTagMask().Contains(extensionTag), Is.False,
                    "Sparse Tags must not enter Entity's 64-bit HotSlot mask.");

                ESTagDebugSnapshot snapshot = entity.Tags.GetDebugSnapshot();
                Assert.That(snapshot.SchemaHash, Is.EqualTo(catalog.SchemaHash));
                Assert.That(snapshot.RuntimeLayoutHash, Is.EqualTo(catalog.RuntimeLayoutHash));
                Assert.That(snapshot.SparseTags.Count, Is.EqualTo(1));
                Assert.That(snapshot.SparseTags[0].StableReference, Is.EqualTo("tests.tag.lease"));
                Assert.That(snapshot.SparseTags[0].Tag, Is.EqualTo(extensionTag));
                Assert.That(snapshot.SparseTags[0].Count, Is.EqualTo(3));
                Assert.That(snapshot.LastChange.IsValid, Is.True);
                Assert.That(snapshot.LastChange.CurrentCount, Is.EqualTo(3));

                firstBuffLease.Dispose();
                Assert.That(firstBuffLease.Release(), Is.False, "Repeated release must be idempotent.");
                Assert.That(firstBuffLease.Source, Is.Null, "An inactive Lease must not retain its source object.");
                Assert.That(entity.Tags.GetCount(extensionTag), Is.EqualTo(2));
                Assert.That(entity.Tags.Has(extensionTag), Is.True);

                secondBuffLease.Dispose();
                Assert.That(entity.Tags.GetCount(extensionTag), Is.EqualTo(1));
                equipmentLease.Dispose();
                Assert.That(entity.Tags.GetCount(extensionTag), Is.Zero);
                Assert.That(entity.Tags.Has(extensionTag), Is.False);

                Assert.That(entity.Tags.TryAcquireStringKey("tests.deprecated", new object(), out _), Is.False);
                snapshot = entity.Tags.GetDebugSnapshot();
                Assert.That(snapshot.LastRejected.IsValid, Is.True);
                Assert.That(snapshot.LastRejected.Tag.Value, Is.EqualTo(extensionRuntimeKey + 1));
                Assert.That(snapshot.LastRejected.StableReference, Is.EqualTo("tests.deprecated"));

                ESTagLease coreLease = entity.Tags.Acquire((ESGameTag)12, new object());
                Assert.That(coreLease, Is.Not.Null);
                snapshot = entity.Tags.GetDebugSnapshot();
                Assert.That(snapshot.HotTags.Count, Is.EqualTo(1));
                Assert.That(snapshot.HotTags[0].Tag, Is.EqualTo(ESTagId.FromInt32(12)));
                Assert.That(snapshot.HotTags[0].Count, Is.EqualTo(1));
                Assert.That((snapshot.HotMask & (1UL << 12)) != 0UL, Is.True);

                var requiredConditionConfig = new ESTagConditionConfig();
                requiredConditionConfig.required.Add(ESTagStableReference.From((ESGameTag)12));
                requiredConditionConfig.required.Add(ESTagStableReference.FromString("tests.tag.lease"));
                Assert.That(requiredConditionConfig.TryCompile(out ESTagConditionRuntime requiredCondition, out string compileError), Is.True, compileError);
                Assert.That(entity.Tags.TryMatches(requiredCondition, out bool matches, out string evaluationError), Is.True, evaluationError);
                Assert.That(matches, Is.False, "The HotSlot condition passes, but the missing Sparse Tag must fail the full condition.");
                Assert.That(requiredConditionConfig.TryGetRuntime(out ESTagConditionRuntime cachedCondition, out compileError), Is.True, compileError);
                Assert.That(cachedCondition.SchemaHash, Is.EqualTo(catalog.SchemaHash));
                Assert.That(entity.Tags.TryMatches(requiredConditionConfig, out matches, out evaluationError), Is.True, evaluationError);
                Assert.That(matches, Is.False, "Business code must query stable condition configuration without handling RuntimeKeys.");

                Assert.That(entity.Tags.TryAcquireStringKey("tests.tag.lease", new object(), out ESTagLease conditionLease), Is.True);
                Assert.That(entity.Tags.TryMatches(requiredCondition, out matches, out evaluationError), Is.True, evaluationError);
                Assert.That(matches, Is.True);
                Assert.That(entity.Tags.Matches(requiredConditionConfig), Is.True);

                var runtimeMutableCondition = new ESTagConditionConfig();
                runtimeMutableCondition.required.Add(ESTagStableReference.From((ESGameTag)12));
                Assert.That(entity.Tags.Matches(runtimeMutableCondition), Is.True);
                runtimeMutableCondition.forbidden.Add(ESTagStableReference.From((ESGameTag)12));
                runtimeMutableCondition.InvalidateRuntime();
                Assert.That(entity.Tags.TryMatches(runtimeMutableCondition, out matches, out evaluationError), Is.False);
                Assert.That(matches, Is.False);
                Assert.That(evaluationError, Does.Contain("can never match"));

                var forbiddenConditionConfig = new ESTagConditionConfig();
                forbiddenConditionConfig.required.Add(ESTagStableReference.From((ESGameTag)12));
                forbiddenConditionConfig.forbidden.Add(ESTagStableReference.FromString("tests.tag.lease"));
                Assert.That(forbiddenConditionConfig.TryCompile(out ESTagConditionRuntime forbiddenCondition, out compileError), Is.True, compileError);
                Assert.That(entity.Tags.Matches(forbiddenCondition), Is.False);

                var anyConditionConfig = new ESTagConditionConfig();
                anyConditionConfig.requiredAny.Add(ESTagStableReference.From((ESGameTag)12));
                anyConditionConfig.requiredAny.Add(ESTagStableReference.From((ESGameTag)13));
                anyConditionConfig.requiredAny.Add(ESTagStableReference.FromString("tests.tag.lease"));
                Assert.That(entity.Tags.Matches(anyConditionConfig), Is.True,
                    "HotSlot and Sparse required-any conditions must compose without changing the HotSlot path.");

                var atomicFailureTags = new List<ESTagStableReference>
                {
                    ESTagStableReference.From((ESGameTag)13),
                    ESTagStableReference.FromString("tests.missing.tag")
                };
                using (var atomicFailureLeases = new ESTagLeaseSet())
                {
                    Assert.That(atomicFailureLeases.TryApply(entity.Tags, atomicFailureTags, new object(), out string atomicError), Is.False);
                    Assert.That(atomicError, Does.Contain("tests.missing.tag"));
                    Assert.That(entity.Tags.Has(ESTagId.FromInt32(13)), Is.False,
                        "A partial grant failure must roll back its HotSlot lease.");
                }

                Assert.That(entity.Tags.TryCreateStableSnapshot(ESTagStableTransferScope.SaveGame, out ESTagStableSnapshot stableSnapshot, out string snapshotError), Is.True, snapshotError);
                Assert.That(stableSnapshot.SchemaHash, Is.EqualTo(catalog.SchemaHash));
                Assert.That(stableSnapshot.Tags, Does.Contain(ESTagStableReference.From((ESGameTag)12)));
                Assert.That(stableSnapshot.Tags, Does.Contain(ESTagStableReference.FromString("tests.tag.lease")));
                Assert.That(stableSnapshot.Tags.Select(reference => reference.ToString()), Does.Not.Contain(extensionRuntimeKey.ToString()),
                    "A stable payload must never expose a RuntimeKey as its identity.");

                var unavailableConditionConfig = new ESTagConditionConfig();
                unavailableConditionConfig.required.Add(ESTagStableReference.FromString("tests.deprecated"));
                Assert.That(unavailableConditionConfig.TryCompile(out _, out compileError), Is.False);
                Assert.That(compileError, Does.Contain("runtime-available"));

                ESTagConditionRuntime staleLayoutCondition = requiredCondition;
                staleLayoutCondition.RuntimeLayoutHash = "stale-layout";
                Assert.That(entity.Tags.TryMatches(staleLayoutCondition, out matches, out evaluationError), Is.False);
                Assert.That(matches, Is.False);
                Assert.That(evaluationError, Does.Contain("RuntimeKey layout"));

                conditionLease.Dispose();
                coreLease.Dispose();

                var localControl = new ESLocalControlService();
                var localMode = new ESRuntimeModeService();
                Assert.That(localControl.TryClaim(entity, localMode), Is.True);
                using (ESTagLease projectedCombatLease = entity.Tags.Acquire((ESGameTag)12, new object()))
                {
                    Assert.That(localMode.ContainsTag(ESRuntimeModeTag.Combat), Is.True);
                }
                Assert.That(localMode.ContainsTag(ESRuntimeModeTag.Combat), Is.False);

                nextEntityObject = new GameObject("ESTagCatalogRuntimeTests_NextEntity");
                Entity nextEntity = nextEntityObject.AddComponent<Entity>();

                using (var restoredLeases = new ESTagLeaseSet())
                {
                    Assert.That(stableSnapshot.TryRestoreTo(nextEntity.Tags, ESTagStableTransferScope.SaveGame, restoredLeases, new object(), out snapshotError), Is.True, snapshotError);
                    Assert.That(nextEntity.Tags.Has(ESTagId.FromInt32(12)), Is.True);
                    Assert.That(nextEntity.Tags.Has(extensionTag), Is.True);

                    var hitEligibility = new ESHitTagEligibility();
                    hitEligibility.attackerCondition.required.Add(ESTagStableReference.From((ESGameTag)12));
                    hitEligibility.targetCondition.required.Add(ESTagStableReference.From((ESGameTag)12));
                    using (ESTagLease attackerHitLease = entity.Tags.Acquire((ESGameTag)12, new object()))
                    {
                        Assert.That(hitEligibility.TryAllows(entity, nextEntity, out ESHitTagEligibilityResult hitResult, out string hitError), Is.True, hitError);
                        Assert.That(hitResult, Is.EqualTo(ESHitTagEligibilityResult.Allowed));
                        using ESTagLease targetBlockedLease = nextEntity.Tags.Acquire((ESGameTag)13, new object());
                        hitEligibility.targetCondition.forbidden.Add(ESTagStableReference.From((ESGameTag)13));
                        hitEligibility.targetCondition.InvalidateRuntime();
                        Assert.That(hitEligibility.TryAllows(entity, nextEntity, out hitResult, out hitError), Is.True, hitError);
                        Assert.That(hitResult, Is.EqualTo(ESHitTagEligibilityResult.TargetTagDenied));
                    }
                }

                localControl.SetControlledEntity(nextEntity, localMode);
                Assert.That(localMode.ContainsTag(ESRuntimeModeTag.Combat), Is.False,
                    "Switching the controlled Entity must release old RuntimeMode projections.");
                using (ESTagLease nextProjectedCombatLease = nextEntity.Tags.Acquire((ESGameTag)12, new object()))
                {
                    Assert.That(localMode.ContainsTag(ESRuntimeModeTag.Combat), Is.True);
                }
                Assert.That(localControl.Release(nextEntity), Is.True);
                Assert.That(localMode.ContainsTag(ESRuntimeModeTag.Combat), Is.False);
                localControl.Dispose();

                Assert.Throws<InvalidOperationException>(() => ESTagRuntimeCatalog.Bind(changedSchema, catalog.SchemaHash));
                Assert.That(changedLayout.SchemaHash, Is.EqualTo(catalog.SchemaHash));
                Assert.Throws<InvalidOperationException>(() => ESTagRuntimeCatalog.Bind(changedLayout, catalog.SchemaHash));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(nextEntityObject);
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(changedSchema);
                UnityEngine.Object.DestroyImmediate(changedLayout);
            }
        }

        [Test]
        public void Collection_ClearInvalidatesOldLeaseBeforeSameTagReacquire()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.clear.generation", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    ESTagId tag = ESTagId.FromInt32(12);
                    ESTagLease firstLease = collection.Acquire((ESGameTag)12, new object());
                    Assert.That(firstLease, Is.Not.Null);
                    Assert.That(collection.GetCount(tag), Is.EqualTo(1));

                    collection.Clear();
                    Assert.That(firstLease.IsActive, Is.False, "Clear must invalidate every Lease created before the clear generation.");
                    Assert.That(collection.GetCount(tag), Is.Zero);

                    using (ESTagLease secondLease = collection.Acquire((ESGameTag)12, new object()))
                    {
                        Assert.That(secondLease, Is.Not.Null);
                        Assert.That(collection.GetCount(tag), Is.EqualTo(1));
                        Assert.That(firstLease.Release(), Is.False, "An old generation Lease must not release a new holder.");
                        Assert.That(collection.GetCount(tag), Is.EqualTo(1));
                    }

                    Assert.That(collection.GetCount(tag), Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void LeaseSet_ReleaseAll_IsSafeAgainstReentrantAcquire()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.reentrant.lease", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                using (var leaseSet = new ESTagLeaseSet())
                {
                    var initialTags = new List<ESTagStableReference> { ESTagStableReference.From((ESGameTag)12) };
                    var reentrantTags = new List<ESTagStableReference> { ESTagStableReference.From((ESGameTag)13) };
                    Assert.That(leaseSet.TryApply(collection, initialTags, new object(), out string initialError), Is.True, initialError);

                    bool callbackInvoked = false;
                    bool reentrantAcquireResult = true;
                    string reentrantError = null;
                    var receiver = new TagCountReceiver(link =>
                    {
                        ESTagId tag = link.Tag;
                        int previous = link.PreviousCount;
                        int current = link.CurrentCount;
                        if (tag != ESTagId.FromInt32(12) || previous != 1 || current != 0)
                            return;

                        callbackInvoked = true;
                        reentrantAcquireResult = leaseSet.TryApply(collection, reentrantTags, new object(), out reentrantError);
                    });

                    collection.AddCountChangedReceiver(receiver);
                    try
                    {
                        leaseSet.ReleaseAll();
                    }
                    finally
                    {
                        collection.RemoveCountChangedReceiver(receiver);
                    }

                    Assert.That(callbackInvoked, Is.True);
                    Assert.That(reentrantAcquireResult, Is.False);
                    Assert.That(reentrantError, Does.Contain("Reentrant"));
                    Assert.That(leaseSet.Count, Is.Zero);
                    Assert.That(collection.GetCount(ESTagId.FromInt32(12)), Is.Zero);
                    Assert.That(collection.GetCount(ESTagId.FromInt32(13)), Is.Zero,
                        "A rejected reentrant application must not add a new Lease while release is in progress.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void LeaseSet_TryApply_InvalidReplacementPreservesCurrentLeases()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.apply.transaction", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                using (var leaseSet = new ESTagLeaseSet())
                {
                    ESTagId firstTag = ESTagId.FromInt32(12);
                    var firstTags = new List<ESTagStableReference> { ESTagStableReference.From((ESGameTag)12) };
                    var invalidReplacement = new List<ESTagStableReference>
                    {
                        ESTagStableReference.From((ESGameTag)13),
                        ESTagStableReference.FromString("tests.missing.tag")
                    };

                    Assert.That(leaseSet.TryApply(collection, firstTags, new object(), out string initialError), Is.True, initialError);
                    Assert.That(leaseSet.Contains(firstTag), Is.True);
                    Assert.That(collection.GetCount(firstTag), Is.EqualTo(1));

                    Assert.That(leaseSet.TryApply(collection, invalidReplacement, new object(), out string replacementError), Is.False);
                    Assert.That(replacementError, Does.Contain("tests.missing.tag"));
                    Assert.That(leaseSet.MatchesTags(firstTags), Is.True);
                    Assert.That(leaseSet.Contains(firstTag), Is.True);
                    Assert.That(collection.GetCount(firstTag), Is.EqualTo(1),
                        "An invalid replacement must not clear the previously valid ownership boundary.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Collection_ResetForReuse_InvalidatesLeasesAndClearsDiagnostics()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.reset.reuse", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    ESTagId tag = ESTagId.FromInt32(12);
                    ESTagLease oldLease = collection.Acquire((ESGameTag)12, new object());
                    Assert.That(oldLease, Is.Not.Null);
                    collection.ResetForReuse();

                    Assert.That(oldLease.IsActive, Is.False);
                    Assert.That(collection.GetCount(tag), Is.Zero);
                    Assert.That(collection.GetDebugSnapshot().LastChange.IsValid, Is.False);

                    using (ESTagLease newLease = collection.Acquire((ESGameTag)12, new object()))
                    {
                        Assert.That(newLease, Is.Not.Null);
                        Assert.That(oldLease.Release(), Is.False);
                        Assert.That(collection.GetCount(tag), Is.EqualTo(1));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Collection_HandleFreeHostTag_IsIdempotentAndPreservesExternalLease()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.host.own", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    ESTagId tag = ESTagId.FromInt32(12);
                    int countChanged = 0;
                    int presenceChanged = 0;
                    var countReceiver = new TagCountReceiver(_ => countChanged++);
                    var presenceReceiver = new TagPresenceReceiver(_ => presenceChanged++);
                    collection.AddCountChangedReceiver(countReceiver);
                    collection.AddPresenceChangedReceiver(presenceReceiver);

                    Assert.That(collection.SetTag((ESGameTag)12, true), Is.True);
                    Assert.That(collection.SetTag((ESGameTag)12, true), Is.True,
                        "Repeated Host activation must be idempotent.");
                    Assert.That(collection.HasOwnTag(tag), Is.True);
                    Assert.That(collection.GetCount(tag), Is.EqualTo(1));
                    Assert.That(countChanged, Is.EqualTo(1));
                    Assert.That(presenceChanged, Is.EqualTo(1));

                    ESTagLease externalLease = collection.Acquire((ESGameTag)12, new object());
                    Assert.That(externalLease, Is.Not.Null);
                    Assert.That(collection.GetCount(tag), Is.EqualTo(2));

                    Assert.That(collection.SetTag((ESGameTag)12, false), Is.True);
                    Assert.That(collection.SetTag((ESGameTag)12, false), Is.True,
                        "Repeated Host deactivation must be idempotent.");
                    Assert.That(collection.HasOwnTag(tag), Is.False);
                    Assert.That(collection.GetCount(tag), Is.EqualTo(1),
                        "Removing the Host contribution must preserve an external Lease.");
                    Assert.That(collection.Has(tag), Is.True);
                    Assert.That(presenceChanged, Is.EqualTo(1),
                        "Presence must not fall while an external Lease remains active.");

                    externalLease.Dispose();
                    Assert.That(collection.GetCount(tag), Is.Zero);
                    Assert.That(countChanged, Is.EqualTo(4));
                    Assert.That(presenceChanged, Is.EqualTo(2));

                    ESTagStableReference sparseReference = ESTagStableReference.FromString("tests.host.own");
                    ESTagId sparseTag = ESTagId.FromInt32(4096);
                    Assert.That(collection.SetTag(sparseReference, true), Is.True);
                    Assert.That(collection.HasOwnTag(sparseTag), Is.True);
                    Assert.That(collection.GetCount(sparseTag), Is.EqualTo(1));
                    collection.ResetForReuse();
                    Assert.That(collection.HasOwnTag(sparseTag), Is.False);
                    Assert.That(collection.GetCount(sparseTag), Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Collection_ClearRejectsReentrantWritesAndReturnsEmpty()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.clear.write", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    ESTagId tag = ESTagId.FromInt32(12);
                    ESTagLease oldLease = collection.Acquire((ESGameTag)12, new object());
                    ESTagLease reentrantLease = null;
                    bool reentrantSetResult = true;
                    var receiver = new TagCountReceiver(change =>
                    {
                        if (change.Tag != tag || change.CurrentCount != 0)
                            return;

                        reentrantLease = collection.Acquire((ESGameTag)12, new object());
                        reentrantSetResult = collection.SetTag((ESGameTag)12, true);
                    });
                    collection.AddCountChangedReceiver(receiver);

                    collection.Clear();

                    Assert.That(reentrantLease, Is.Null,
                        "A clear callback must not repopulate the collection with a new Lease.");
                    Assert.That(reentrantSetResult, Is.False,
                        "A clear callback must not repopulate the Host-owned Tag layer.");
                    Assert.That(collection.GetCount(tag), Is.Zero);
                    Assert.That(collection.HasOwnTag(tag), Is.False);
                    Assert.That(oldLease.IsActive, Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Collection_ReentrantMutationQueuesCountAndPresenceInOrder()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.notify.order", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    var counts = new List<string>();
                    var presence = new List<bool>();
                    bool removed = false;
                    var countReceiver = new TagCountReceiver(change =>
                    {
                        counts.Add(change.PreviousCount + "->" + change.CurrentCount);
                        if (!removed && change.PreviousCount == 0 && change.CurrentCount == 1)
                        {
                            removed = true;
                            collection.SetTag(change.Tag, false);
                        }
                    });
                    var presenceReceiver = new TagPresenceReceiver(change => presence.Add(change.IsPresent));
                    collection.AddCountChangedReceiver(countReceiver);
                    collection.AddPresenceChangedReceiver(presenceReceiver);

                    Assert.That(collection.SetTag((ESGameTag)12, true), Is.False,
                        "The outer activation must report that a synchronous receiver removed its contribution.");
                    Assert.That(counts, Is.EqualTo(new[] { "0->1", "1->0" }));
                    Assert.That(presence, Is.EqualTo(new[] { true, false }));
                    Assert.That(collection.GetCount(ESTagId.FromInt32(12)), Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void LeaseSet_ReapplyingSameTagsToAnotherCollectionMovesOwnership()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.lease.target", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var first = new ESTagCollection())
                using (var second = new ESTagCollection())
                using (var leaseSet = new ESTagLeaseSet())
                {
                    ESTagId tag = ESTagId.FromInt32(12);
                    var tags = new List<ESTagStableReference>
                    {
                        ESTagStableReference.From((ESGameTag)12)
                    };

                    Assert.That(leaseSet.TryApply(first, tags, "first", out string firstError), Is.True, firstError);
                    Assert.That(first.GetCount(tag), Is.EqualTo(1));
                    Assert.That(second.GetCount(tag), Is.Zero);

                    Assert.That(leaseSet.TryApply(second, tags, "second", out string secondError), Is.True, secondError);
                    Assert.That(first.GetCount(tag), Is.Zero,
                        "Reusing one LeaseSet for another Host must release the former target.");
                    Assert.That(second.GetCount(tag), Is.EqualTo(1));
                    Assert.That(leaseSet.Source, Is.EqualTo("second"));

                    leaseSet.ReleaseAll();
                    Assert.That(second.GetCount(tag), Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void EntityDefinitionTags_ReapplyPoolReuseAndOverlappingTemporaryTagStayCorrect()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.entity.lifecycle", 4096, ESTagAvailability.Runtime);
            UnityEngine.Object definition = CreateActorDefinition(intrinsicTag: ESTagStableReference.From((ESGameTag)12));
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_LifecycleEntity");
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                ESTagStableReference intrinsicTag = ESTagStableReference.From((ESGameTag)12);

                Entity entity = entityObject.AddComponent<Entity>();
                ESTagId tag = ESTagId.FromInt32(12);
                Assert.That(BindActorDefinition(entity, definition), Is.True);
                Assert.That(entity.IntrinsicTagState, Is.EqualTo(ESTagDefinitionState.Applied));
                Assert.That(entity.HasIntrinsicTag(intrinsicTag), Is.True);
                Assert.That(entity.Tags.GetCount(tag), Is.EqualTo(1));

                int duplicateApplyEvents = 0;
                var receiver = new TagCountReceiver(_ => duplicateApplyEvents++);
                Assert.That(entity.Tags.AddCountChangedReceiver(receiver), Is.True);
                try
                {
                    Assert.That(entity.ApplyIntrinsicTags(), Is.True);
                    Assert.That(duplicateApplyEvents, Is.Zero,
                        "Applying an unchanged definition must not release and reacquire its Tag.");
                }
                finally
                {
                    entity.Tags.RemoveCountChangedReceiver(receiver);
                }

                ESTagLease temporaryLease = entity.Tags.Acquire((ESGameTag)12, new object());
                Assert.That(temporaryLease, Is.Not.Null);
                Assert.That(entity.Tags.GetCount(tag), Is.EqualTo(2),
                    "Definition and temporary ownership must aggregate without hiding either source.");

                Assert.That(entity.TryCreateNonIntrinsicTagSnapshot(
                    ESTagStableTransferScope.SaveGame, out ESTagStableSnapshot snapshot, out string snapshotError), Is.True, snapshotError);
                Assert.That(snapshot.Tags.Any(tagReference => tagReference.Equals(intrinsicTag)), Is.False,
                    "A temporary holder sharing an intrinsic Tag cannot be reconstructed from aggregate presence and must not enter the Entity snapshot.");

                entity.OnPoolDespawned();
                Assert.That(entity.IntrinsicTagState, Is.EqualTo(ESTagDefinitionState.Empty));
                Assert.That(entity.Tags.GetCount(tag), Is.Zero);
                Assert.That(temporaryLease.IsActive, Is.False);

                entity.OnPoolSpawned();
                Assert.That(BindActorDefinition(entity, definition), Is.True);
                Assert.That(entity.Tags.GetCount(tag), Is.EqualTo(1));
                Assert.That(temporaryLease.Release(), Is.False,
                    "A delayed Lease from the previous pool generation must not release the new definition Tag.");
                Assert.That(entity.Tags.GetCount(tag), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ItemDefinitionTags_PoolReuseAndOverlappingTemporaryTagStayCorrect()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.item.lifecycle", 4096, ESTagAvailability.Runtime);
            ESTagStableReference intrinsicTag = ESTagStableReference.From((ESGameTag)12);
            UnityEngine.Object definition = CreateItemDefinition(intrinsicTag);
            GameObject itemObject = new GameObject("ESTagCatalogRuntimeTests_LifecycleItem");
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                Item item = itemObject.AddComponent<Item>();
                ESTagId tag = ESTagId.FromInt32(12);

                SetItemPrefabDefinition(item, definition);
                Assert.That(BindItemDefinition(item, definition), Is.True);
                Assert.That(item.IntrinsicTagState, Is.EqualTo(ESTagDefinitionState.Applied));
                Assert.That(item.HasIntrinsicTag(intrinsicTag), Is.True);
                Assert.That(item.Tags.GetCount(tag), Is.EqualTo(1));

                ESTagLease temporaryLease = item.Tags.Acquire((ESGameTag)12, new object());
                Assert.That(temporaryLease, Is.Not.Null);
                Assert.That(item.Tags.GetCount(tag), Is.EqualTo(2));

                Assert.That(item.TryCreateNonIntrinsicTagSnapshot(
                    ESTagStableTransferScope.SaveGame, out ESTagStableSnapshot snapshot, out string snapshotError), Is.True, snapshotError);
                Assert.That(snapshot.Tags.Any(tagReference => tagReference.Equals(intrinsicTag)), Is.False);

                item.OnPoolDespawned();
                Assert.That(item.Tags.GetCount(tag), Is.Zero);
                Assert.That(temporaryLease.IsActive, Is.False);

                item.OnPoolSpawned();
                Assert.That(item.IntrinsicTagState, Is.EqualTo(ESTagDefinitionState.Applied),
                    "A definition assigned to the Item must be rebound before the next activation.");
                Assert.That(item.Tags.GetCount(tag), Is.EqualTo(1));
                Assert.That(temporaryLease.Release(), Is.False);
                Assert.That(item.Tags.GetCount(tag), Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemObject);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ItemWithoutRuntimeFacts_DoesNotMaterializeTagCollection()
        {
            GameObject itemObject = new GameObject("ESTagCatalogRuntimeTests_EmptyItem");
            try
            {
                Item item = itemObject.AddComponent<Item>();
                FieldInfo tagsField = typeof(Item).GetField("tags", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(tagsField, Is.Not.Null);
                Assert.That(tagsField.GetValue(item), Is.Null,
                    "An Item without a definition or runtime Tag fact must not allocate ESTagCollection during Awake.");

                Assert.That(item.HasTag(ESTagId.FromInt32(12)), Is.False);
                Assert.That(tagsField.GetValue(item), Is.Null,
                    "A read-only false HasTag query must not materialize an empty Item Tag container.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemObject);
            }
        }

        [Test]
        public void Buff_RemoveOpException_StillReleasesItsTagAndLeavesTheDomainConsistent()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.buff.remove.cleanup", 4096, ESTagAvailability.Runtime);
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffCleanup");
            try
            {
                Assert.That(catalog.TryValidate(out string validationError), Is.True, validationError);
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                Assert.That(catalog.TryGetRuntimeKey("tests.buff.remove.cleanup", out int runtimeKey), Is.True);

                Entity entity = entityObject.AddComponent<Entity>();
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = -1f,
                    onRemoveOp = new ESBuffThrowingStartOp()
                };
                data.tags.Add(ESTagStableReference.FromString("tests.buff.remove.cleanup"));

                ESActiveBuffRuntime buff = entity.buffDomain.AddBuff(data);
                Assert.That(buff, Is.Not.Null);
                Assert.That(entity.buffDomain.ActiveBuffCount, Is.EqualTo(1));
                Assert.That(entity.Tags.GetCount(ESTagId.FromInt32(runtimeKey)), Is.EqualTo(1));

                LogAssert.ignoreFailingMessages = true;
                Assert.That(entity.buffDomain.RemoveBuff(ESBuffEnumKey.Custom), Is.True);
                LogAssert.ignoreFailingMessages = false;

                Assert.That(entity.buffDomain.ActiveBuffCount, Is.Zero);
                Assert.That(entity.Tags.GetCount(ESTagId.FromInt32(runtimeKey)), Is.Zero,
                    "A throwing remove Op must not keep this Buff's Tag lease alive.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Buff_ApplyOpException_RollsBackTagAndDoesNotRemainActive()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.buff.apply.rollback", 4096, ESTagAvailability.Runtime);
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffApplyRollback");
            try
            {
                Assert.That(catalog.TryValidate(out string validationError), Is.True, validationError);
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                Assert.That(catalog.TryGetRuntimeKey("tests.buff.apply.rollback", out int runtimeKey), Is.True);

                Entity entity = entityObject.AddComponent<Entity>();
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = -1f,
                    onApplyOp = new ESBuffThrowingStartOp()
                };
                data.tags.Add(ESTagStableReference.FromString("tests.buff.apply.rollback"));

                LogAssert.ignoreFailingMessages = true;
                Assert.That(entity.buffDomain.AddBuff(data), Is.Null);
                LogAssert.ignoreFailingMessages = false;

                Assert.That(entity.buffDomain.ActiveBuffCount, Is.Zero);
                Assert.That(entity.Tags.GetCount(ESTagId.FromInt32(runtimeKey)), Is.Zero,
                    "A rejected Buff application must return its own Tag lease before leaving the domain.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                UnityEngine.Object.DestroyImmediate(entityObject);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Buff_FixedIntervalTick_CapsCatchUpAndDropsBacklog()
        {
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffTickCap");
            try
            {
                Entity entity = entityObject.AddComponent<Entity>();
                var tickOp = new ESBuffCountingStartOp();
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = -1f,
                    tickMode = ESBuffTickMode.FixedInterval,
                    tickInterval = 1f,
                    maxCatchUpTicksPerFrame = 3,
                    onTickOp = tickOp
                };

                ESActiveBuffRuntime buff = entity.buffDomain.AddBuff(data);
                Assert.That(buff, Is.Not.Null);

                Assert.That(buff.Tick(100f), Is.False);
                Assert.That(tickOp.StartCount, Is.EqualTo(3),
                    "A delayed frame must execute no more than the configured catch-up cap.");
                Assert.That(buff.VariableData.tickAccumulator, Is.LessThan(1f),
                    "Only the sub-interval remainder may survive after capped catch-up.");

                Assert.That(buff.Tick(0.9f), Is.False);
                Assert.That(tickOp.StartCount, Is.EqualTo(3));
                Assert.That(buff.Tick(0.2f), Is.False);
                Assert.That(tickOp.StartCount, Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
            }
        }

        [Test]
        public void Buff_LifecycleLink_IsReadOnlyAndReportsApplyRefreshRemove()
        {
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffLifecycleLink");
            try
            {
                Entity entity = entityObject.AddComponent<Entity>();
                var receiver = new BuffChangedReceiver();
                Assert.That(entity.buffDomain.AddBuffChangedReceiver(receiver), Is.True);
                Assert.That(entity.buffDomain.AddBuffChangedReceiver(receiver), Is.False,
                    "Buff lifecycle observers follow the shared Link duplicate-subscription rule.");

                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = -1f,
                    maxStack = 3,
                    stackMode = ESBuffStackMode.StackSameBuff
                };

                ESActiveBuffRuntime buff = entity.buffDomain.AddBuff(data);
                Assert.That(buff, Is.Not.Null);
                Assert.That(entity.buffDomain.AddBuff(data), Is.SameAs(buff));
                Assert.That(entity.buffDomain.RemoveBuff(ESBuffEnumKey.Custom), Is.True);

                Assert.That(receiver.Changes.Count, Is.EqualTo(3));
                Assert.That(receiver.Changes[0].ChangeType, Is.EqualTo(ESBuffChangeType.Applied));
                Assert.That(receiver.Changes[0].DefinitionRuntimeKey, Is.EqualTo((int)ESBuffEnumKey.Custom));
                Assert.That(receiver.Changes[1].ChangeType, Is.EqualTo(ESBuffChangeType.Refreshed));
                Assert.That(receiver.Changes[1].StackCount, Is.EqualTo(2));
                Assert.That(receiver.Changes[2].ChangeType, Is.EqualTo(ESBuffChangeType.Removed));
                Assert.That(receiver.Changes[2].StackCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
            }
        }

        [Test]
        public void BuffFrame_ReconcilesOneOwnerWithoutStackingOrTouchingOrdinaryBuffs()
        {
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffFrame");
            ESBuffConfigKeyTable table = ESRuntimeDataGameCore.Buffs;
            try
            {
                table.BeginBuild(clear: true);
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = 30f,
                    maxStack = 9,
                    stackMode = ESBuffStackMode.StackSameBuff
                };
                Assert.That(table.InjectWith((ESBuffConfigKey)ESBuffEnumKey.Custom, data, new BuffVariableData { level = 2 }), Is.Not.Zero);
                table.EndBuild();

                Entity entity = entityObject.AddComponent<Entity>();
                ESActiveBuffRuntime ordinary = entity.buffDomain.AddBuff(data);
                Assert.That(ordinary, Is.Not.Null);

                object stateOwner = new object();
                Assert.That(entity.buffDomain.BeginBuffFrame(stateOwner), Is.True);
                Assert.That(entity.buffDomain.SetBuff(ESBuffEnumKey.Custom), Is.True);
                Assert.That(entity.buffDomain.SetBuff(ESBuffEnumKey.Custom), Is.True,
                    "The latest write in one Buff frame replaces the earlier write instead of stacking it.");
                Assert.That(entity.buffDomain.EndBuffFrame(), Is.True);
                Assert.That(entity.buffDomain.ActiveBuffCount, Is.EqualTo(2));
                Assert.That(entity.buffDomain.CountBuff(ESBuffEnumKey.Custom), Is.EqualTo(2));

                Assert.That(entity.buffDomain.BeginBuffFrame(stateOwner), Is.True);
                Assert.That(entity.buffDomain.SetBuff(ESBuffEnumKey.Custom), Is.True);
                Assert.That(entity.buffDomain.EndBuffFrame(), Is.True);
                Assert.That(entity.buffDomain.ActiveBuffCount, Is.EqualTo(2),
                    "Reasserting the same state effect keeps its runtime ownership and does not reapply a stack.");

                Assert.That(entity.buffDomain.BeginBuffFrame(stateOwner), Is.True);
                Assert.That(entity.buffDomain.EndBuffFrame(), Is.True,
                    "An empty completed frame is the explicit full-clear for that frame owner.");
                Assert.That(entity.buffDomain.ActiveBuffCount, Is.EqualTo(1));
                Assert.That(entity.buffDomain.CountBuff(ESBuffEnumKey.Custom), Is.EqualTo(1),
                    "The frame must never remove a normal AddBuff lifecycle that happens to use the same Buff key.");
            }
            finally
            {
                if (table.IsBuilding)
                    table.EndBuild();
                table.BeginBuild(clear: true);
                table.EndBuild();
                UnityEngine.Object.DestroyImmediate(entityObject);
            }
        }

        [Test]
        public void BuffFrame_InvalidWriteKeepsThePreviousCommittedState()
        {
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffFrameInvalid");
            ESBuffConfigKeyTable table = ESRuntimeDataGameCore.Buffs;
            try
            {
                table.BeginBuild(clear: true);
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = 30f
                };
                Assert.That(table.InjectWith((ESBuffConfigKey)ESBuffEnumKey.Custom, data, new BuffVariableData()), Is.Not.Zero);
                table.EndBuild();

                Entity entity = entityObject.AddComponent<Entity>();
                object stateOwner = new object();
                Assert.That(entity.buffDomain.BeginBuffFrame(stateOwner), Is.True);
                Assert.That(entity.buffDomain.SetBuff(ESBuffEnumKey.Custom), Is.True);
                Assert.That(entity.buffDomain.EndBuffFrame(), Is.True);
                Assert.That(entity.buffDomain.ActiveBuffCount, Is.EqualTo(1));

                LogAssert.ignoreFailingMessages = true;
                Assert.That(entity.buffDomain.BeginBuffFrame(stateOwner), Is.True);
                Assert.That(entity.buffDomain.SetBuff("tests.buff.frame.missing"), Is.False);
                Assert.That(entity.buffDomain.EndBuffFrame(), Is.False);
                LogAssert.ignoreFailingMessages = false;

                Assert.That(entity.buffDomain.ActiveBuffCount, Is.EqualTo(1),
                    "An invalid full-frame write must not clear the source's already committed effects.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                if (table.IsBuilding)
                    table.EndBuild();
                table.BeginBuild(clear: true);
                table.EndBuild();
                UnityEngine.Object.DestroyImmediate(entityObject);
            }
        }

        [Test]
        public void BuffOperation_ChangesTimeStacksAndLevelWithoutRemoveAndReadd()
        {
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffOperation");
            ESBuffConfigKeyTable table = ESRuntimeDataGameCore.Buffs;
            try
            {
                table.BeginBuild(clear: true);
                var levelReadingOp = new ESBuffLevelReadingStartOp();
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = 10f,
                    maxStack = 5,
                    maxLevel = 5,
                    stackMode = ESBuffStackMode.StackSameBuff,
                    onRefreshOp = levelReadingOp
                };
                Assert.That(table.InjectWith((ESBuffConfigKey)ESBuffEnumKey.Custom, data, new BuffVariableData { level = 2 }), Is.Not.Zero);
                table.EndBuild();

                Entity entity = entityObject.AddComponent<Entity>();
                ESActiveBuffRuntime initial = entity.buffDomain.AddBuff(ESBuffEnumKey.Custom);
                Assert.That(initial, Is.Not.Null);
                Assert.That(initial.StackCount, Is.EqualTo(1));
                Assert.That(initial.RemainingTime, Is.EqualTo(10f));
                Assert.That(initial.Level, Is.EqualTo(2), "A GameCore Buff uses its configured default runtime level on creation.");

                ESActiveBuffRuntime changed = entity.buffDomain.ApplyBuff(
                    ESBuffEnumKey.Custom,
                    ESBuffOperation.Default.AddStack(2).AddDuration(3f).SetLevel(4));

                Assert.That(changed, Is.SameAs(initial), "An operation must update the existing Buff instead of Remove + Add churn.");
                Assert.That(changed.StackCount, Is.EqualTo(3));
                Assert.That(changed.RemainingTime, Is.EqualTo(13f));
                Assert.That(changed.Level, Is.EqualTo(4));
                Assert.That(levelReadingOp.LastObservedLevel, Is.EqualTo(4),
                    "A Buff refresh Op must receive the active Buff Runtime through its support scope.");

                Assert.That(entity.buffDomain.ApplyBuff(ESBuffEnumKey.Custom, ESBuffOperation.Default.ResetDuration()), Is.SameAs(initial));
                Assert.That(initial.RemainingTime, Is.EqualTo(10f));

                Assert.That(entity.buffDomain.ApplyBuff(ESBuffEnumKey.Custom, ESBuffOperation.Remove), Is.Null);
                Assert.That(entity.buffDomain.ActiveBuffCount, Is.Zero);
            }
            finally
            {
                if (table.IsBuilding)
                    table.EndBuild();
                table.BeginBuild(clear: true);
                table.EndBuild();
                UnityEngine.Object.DestroyImmediate(entityObject);
            }
        }

        [Test]
        public void BuffOperationOp_AppliesOneComposedOperationToTheMainTarget()
        {
            GameObject sourceObject = new GameObject("ESTagCatalogRuntimeTests_BuffOperationOpSource");
            GameObject targetObject = new GameObject("ESTagCatalogRuntimeTests_BuffOperationOpTarget");
            ESBuffConfigKeyTable table = ESRuntimeDataGameCore.Buffs;
            ESOpSupport sourceSupport = null;
            try
            {
                var key = new ESBuffConfigKey
                {
                    enumKey = ESBuffEnumKey.Custom,
                    stringKey = "tests.buff.operation.op"
                };
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey
                    {
                        enumKey = ESBuffEnumKey.Custom,
                        stringKey = "tests.buff.operation.op"
                    },
                    duration = 10f,
                    maxStack = 5,
                    maxLevel = 5,
                    stackMode = ESBuffStackMode.StackSameBuff
                };

                table.BeginBuild(clear: true);
                Assert.That(table.InjectWith(key, data, new BuffVariableData { level = 1 }), Is.Not.Zero);
                table.EndBuild();

                Entity source = sourceObject.AddComponent<Entity>();
                Entity target = targetObject.AddComponent<Entity>();
                ESActiveBuffRuntime initial = target.buffDomain.AddBuff(ESBuffEnumKey.Custom);
                Assert.That(initial, Is.Not.Null);
                initial.variableData.remainingTime = 1f;

                sourceSupport = ESOpSupport.CreateStandalone().InitializeEntityOwner(source);
                var targetPack = new ESRuntimeTargetPack()
                    .SetUser(source)
                    .SetEntityMainTarget(target);
                var op = new OpBuff_ApplyToMainTarget
                {
                    buff = key,
                    operation = ESBuffOperation.Default.ResetDuration().AddStack(1).SetLevel(3)
                };

                op._TryStartOp(targetPack, sourceSupport, null);

                Assert.That(target.buffDomain.ActiveBuffCount, Is.EqualTo(1));
                Assert.That(initial.StackCount, Is.EqualTo(2));
                Assert.That(initial.RemainingTime, Is.EqualTo(10f));
                Assert.That(initial.Level, Is.EqualTo(3));
            }
            finally
            {
                sourceSupport?.Dispose();
                if (table.IsBuilding)
                    table.EndBuild();
                table.BeginBuild(clear: true);
                table.EndBuild();
                UnityEngine.Object.DestroyImmediate(sourceObject);
                UnityEngine.Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void Buff_DoesNotRetainTransientSourceSupportAfterApplication()
        {
            GameObject targetObject = new GameObject("ESTagCatalogRuntimeTests_BuffTarget");
            GameObject sourceObject = new GameObject("ESTagCatalogRuntimeTests_BuffSource");
            ESOpSupport transientSource = null;
            try
            {
                Entity target = targetObject.AddComponent<Entity>();
                Entity source = sourceObject.AddComponent<Entity>();
                transientSource = ESOpSupport.CreateStandalone().InitializeEntityOwner(source);
                var removeOp = new ESBuffTargetSnapshotRemoveOp();
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = -1f,
                    onRemoveOp = removeOp
                };

                Assert.That(target.buffDomain.AddBuff(data, sourceSupport: transientSource), Is.Not.Null);
                transientSource.Dispose();

                Assert.That(target.buffDomain.RemoveBuff(ESBuffEnumKey.Custom), Is.True);
                Assert.That(removeOp.ObservedUser, Is.SameAs(source),
                    "A long-lived Buff must retain the source identity snapshot, not the source Support object.");
                Assert.That(removeOp.ObservedHostSupport, Is.SameAs(target.OpSupport),
                    "Refresh/remove operations must run under the target Buff host, never under a disposed attack Support.");
            }
            finally
            {
                transientSource?.Dispose();
                UnityEngine.Object.DestroyImmediate(targetObject);
                UnityEngine.Object.DestroyImmediate(sourceObject);
            }
        }

        [Test]
        public void BuffLogic_UsesAnIsolatedRuntimeForItsFullLifecycle()
        {
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffLogic");
            try
            {
                var logic = new CountingBuffLogic();
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = -1f,
                    maxStack = 3,
                    tickMode = ESBuffTickMode.EveryFrame,
                    logic = logic
                };

                Entity entity = entityObject.AddComponent<Entity>();
                ESActiveBuffRuntime buff = entity.buffDomain.AddBuff(data);
                Assert.That(buff, Is.Not.Null);
                Assert.That(logic.LastRuntime, Is.Not.Null);
                Assert.That(logic.LastRuntime.ApplyCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.ObservedOwner, Is.SameAs(entity));
                Assert.That(logic.LastRuntime.ObservedTarget, Is.Not.Null);

                Assert.That(entity.buffDomain.ApplyBuff(buff, ESBuffOperation.Default.AddStack(1)), Is.True);
                Assert.That(logic.LastRuntime.RefreshCount, Is.EqualTo(1));

                Assert.That(buff.Tick(0.25f), Is.False);
                Assert.That(logic.LastRuntime.TickCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.LastTickDelta, Is.EqualTo(0.25f));

                Assert.That(entity.buffDomain.RemoveBuff(ESBuffEnumKey.Custom), Is.True);
                Assert.That(logic.LastRuntime.RemoveCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.ReleaseCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.ReturnCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.IsRecycled, Is.True);
                Assert.That(logic.LastRuntime.Buff, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
            }
        }

        [Test]
        public void BuffLogic_ApplyRejectionRollsBackAndReturnsItsRuntime()
        {
            GameObject entityObject = new GameObject("ESTagCatalogRuntimeTests_BuffLogicRollback");
            try
            {
                var logic = new RejectingBuffLogic();
                var data = new BuffSharedData
                {
                    key = new ESBuffConfigKey { enumKey = ESBuffEnumKey.Custom },
                    duration = 5f,
                    logic = logic
                };

                Entity entity = entityObject.AddComponent<Entity>();
                Assert.That(entity.buffDomain.AddBuff(data), Is.Null);
                Assert.That(entity.buffDomain.ActiveBuffCount, Is.Zero);
                Assert.That(logic.LastRuntime, Is.Not.Null);
                Assert.That(logic.LastRuntime.ApplyCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.RemoveCount, Is.Zero,
                    "Apply rejection must not run the normal removal gameplay callback.");
                Assert.That(logic.LastRuntime.ReleaseCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.ReturnCount, Is.EqualTo(1));
                Assert.That(logic.LastRuntime.Buff, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(entityObject);
            }
        }

        [Test]
        public void LeaseSet_RejectsDuplicateAliasesForOneRuntimeKey()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.alias", 4096, ESTagAvailability.Runtime);
            try
            {
                FieldInfo entriesField = typeof(ESTagBakeTable).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
                var entries = entriesField?.GetValue(catalog) as List<ESTagBakeTable.Entry>;
                Assert.That(entries, Is.Not.Null);
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].enumGroup == ESTagEnumGroup.Primary && entries[i].enumValue == 12)
                    {
                        ESTagBakeTable.Entry entry = entries[i];
                        entry.key = "tests.alias.core12";
                        entries[i] = entry;
                        break;
                    }
                }

                catalog.BuildRuntimeCache();
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                var aliases = new List<ESTagStableReference>
                {
                    ESTagStableReference.From((ESGameTag)12),
                    ESTagStableReference.FromString("tests.alias.core12")
                };

                Assert.That(ESTagLeaseSet.TryValidateTags(aliases, out string error), Is.False);
                Assert.That(error, Does.Contain("multiple stable aliases"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Collection_ObserverExceptionDoesNotStrandLeaseOrBlockOtherObservers()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.observer.exception", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    int deliveredCount = 0;
                    var throwingObserver = new TagCountReceiver(_ => throw new InvalidOperationException("test observer failure"));
                    var survivingObserver = new TagCountReceiver(_ => deliveredCount++);

                    collection.AddCountChangedReceiver(throwingObserver);
                    collection.AddCountChangedReceiver(survivingObserver);
                    try
                    {
                        using (ESTagLease lease = collection.Acquire((ESGameTag)12, new object()))
                        {
                            Assert.That(lease, Is.Not.Null);
                            Assert.That(lease.IsActive, Is.True);
                            Assert.That(collection.GetCount(ESTagId.FromInt32(12)), Is.EqualTo(1));
                            Assert.That(deliveredCount, Is.EqualTo(1), "A failing observer must not block a later observer.");

                            ESTagObserverExceptionDebugInfo diagnostic = collection.GetDebugSnapshot().LastObserverException;
                            Assert.That(diagnostic.IsValid, Is.True);
                            Assert.That(diagnostic.Tag, Is.EqualTo(ESTagId.FromInt32(12)));
                            Assert.That(diagnostic.EventName, Is.EqualTo("TagCountChanged"));
                            Assert.That(diagnostic.ExceptionType, Does.Contain(nameof(InvalidOperationException)));
                        }

                        Assert.That(collection.GetCount(ESTagId.FromInt32(12)), Is.Zero);
                        Assert.That(deliveredCount, Is.EqualTo(2));
                    }
                    finally
                    {
                        collection.RemoveCountChangedReceiver(survivingObserver);
                        collection.RemoveCountChangedReceiver(throwingObserver);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Collection_LinkSubscriptionsRejectDuplicatesAndCommitChangesNextDispatch()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.link.subscription", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    int firstCount = 0;
                    int secondCount = 0;
                    int addedDuringDispatchCount = 0;
                    TagCountReceiver second = null;
                    var addedDuringDispatch = new TagCountReceiver(_ => addedDuringDispatchCount++);
                    var first = new TagCountReceiver(_ =>
                    {
                        firstCount++;
                        collection.RemoveCountChangedReceiver(second);
                        collection.AddCountChangedReceiver(addedDuringDispatch);
                    });
                    second = new TagCountReceiver(_ => secondCount++);

                    Assert.That(collection.AddCountChangedReceiver(first), Is.True);
                    Assert.That(collection.AddCountChangedReceiver(second), Is.True);
                    Assert.That(collection.AddCountChangedReceiver(second), Is.False, "Duplicate registration must be rejected.");

                    using (ESTagLease lease = collection.Acquire((ESGameTag)12, new object()))
                    {
                        Assert.That(firstCount, Is.EqualTo(1));
                        Assert.That(secondCount, Is.EqualTo(1), "Removal during dispatch must not suppress this round.");
                        Assert.That(addedDuringDispatchCount, Is.Zero, "Addition during dispatch must begin next round.");
                    }

                    Assert.That(firstCount, Is.EqualTo(2));
                    Assert.That(secondCount, Is.EqualTo(1));
                    Assert.That(addedDuringDispatchCount, Is.EqualTo(1));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void LinkPoolableReceiver_RemoveThenReaddDuringDispatch_IsNotRecycled()
        {
            ESTagBakeTable catalog = CreateCatalog("tests.link.poolable", 4096, ESTagAvailability.Runtime);
            try
            {
                ESTagRuntimeCatalog.Bind(catalog, catalog.SchemaHash);
                using (var collection = new ESTagCollection())
                {
                    PoolableTagCountReceiver receiver = null;
                    receiver = new PoolableTagCountReceiver(_ =>
                    {
                        if (receiver.DeliveryCount != 1)
                            return;

                        Assert.That(collection.RemoveCountChangedReceiver(receiver), Is.True);
                        Assert.That(collection.AddCountChangedReceiver(receiver), Is.True);
                    });
                    Assert.That(collection.AddCountChangedReceiver(receiver), Is.True);

                    Assert.That(collection.SetTag((ESGameTag)12, true), Is.True);
                    Assert.That(receiver.IsRecycled, Is.False,
                        "A receiver that is subscribed again at commit must not be returned to its pool.");
                    Assert.That(receiver.RecycleCount, Is.Zero);

                    Assert.That(collection.SetTag((ESGameTag)12, false), Is.True);
                    Assert.That(receiver.DeliveryCount, Is.EqualTo(2),
                        "The re-added receiver must remain active for the next dispatch.");

                    Assert.That(collection.RemoveCountChangedReceiver(receiver), Is.True);
                    Assert.That(receiver.IsRecycled, Is.True,
                        "A final removal outside dispatch must still recycle the receiver.");
                    Assert.That(receiver.RecycleCount, Is.EqualTo(1));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private sealed class TagCountReceiver : IReceiveLink<ESTagCountChangedLink>
        {
            private readonly Action<ESTagCountChangedLink> callback;

            public TagCountReceiver(Action<ESTagCountChangedLink> callback)
            {
                this.callback = callback;
            }

            public void OnLink(ESTagCountChangedLink link)
            {
                callback(link);
            }
        }

        private sealed class TagPresenceReceiver : IReceiveLink<ESTagPresenceChangedLink>
        {
            private readonly Action<ESTagPresenceChangedLink> callback;

            public TagPresenceReceiver(Action<ESTagPresenceChangedLink> callback)
            {
                this.callback = callback;
            }

            public void OnLink(ESTagPresenceChangedLink link)
            {
                callback(link);
            }
        }

        private sealed class ESBuffThrowingStartOp : ESOutputOp
        {
            protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
                throw new InvalidOperationException("Expected Buff remove-op test failure.");
            }
        }

        private sealed class ESBuffCountingStartOp : ESOutputOp
        {
            public int StartCount { get; private set; }

            protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
                StartCount++;
            }
        }

        private sealed class ESBuffLevelReadingStartOp : ESOutputOp
        {
            public int LastObservedLevel { get; private set; }

            protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
                LastObservedLevel = scopeSupport.GetOwner<ESActiveBuffRuntime>()?.Level ?? 0;
            }
        }

        private sealed class ESBuffTargetSnapshotRemoveOp : ESOutputOp
        {
            public Entity ObservedUser { get; private set; }
            public ESOpSupport ObservedHostSupport { get; private set; }

            protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport)
            {
                ObservedUser = target != null ? target.userEntity : null;
                ObservedHostSupport = hostSupport;
            }
        }

        private sealed class CountingBuffLogic : ESBuffLogic
        {
            public CountingBuffLogicRuntime LastRuntime { get; private set; }

            public override ESBuffLogicRuntime RentRuntime()
            {
                LastRuntime = new CountingBuffLogicRuntime(acceptApply: true);
                return LastRuntime;
            }
        }

        private sealed class RejectingBuffLogic : ESBuffLogic
        {
            public CountingBuffLogicRuntime LastRuntime { get; private set; }

            public override ESBuffLogicRuntime RentRuntime()
            {
                LastRuntime = new CountingBuffLogicRuntime(acceptApply: false);
                return LastRuntime;
            }
        }

        private sealed class CountingBuffLogicRuntime : ESBuffLogicRuntime
        {
            private readonly bool acceptApply;

            public int ApplyCount { get; private set; }
            public int RefreshCount { get; private set; }
            public int TickCount { get; private set; }
            public int RemoveCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public int ReturnCount { get; private set; }
            public float LastTickDelta { get; private set; }
            public Entity ObservedOwner { get; private set; }
            public ESRuntimeTargetPack ObservedTarget { get; private set; }

            public CountingBuffLogicRuntime(bool acceptApply)
            {
                this.acceptApply = acceptApply;
            }

            public override bool OnApply()
            {
                ApplyCount++;
                ObservedOwner = Owner;
                ObservedTarget = Target;
                return acceptApply;
            }

            public override void OnRefresh()
            {
                RefreshCount++;
            }

            public override void OnTick(float deltaTime)
            {
                TickCount++;
                LastTickDelta = deltaTime;
            }

            public override void OnRemove()
            {
                RemoveCount++;
            }

            protected override void OnRelease()
            {
                ReleaseCount++;
            }

            public override void TryAutoPushedToPool()
            {
                ReturnCount++;
                IsRecycled = true;
            }
        }

        private sealed class BuffChangedReceiver : IReceiveLink<ESBuffChangedLink>
        {
            public readonly List<ESBuffChangedLink> Changes = new List<ESBuffChangedLink>();

            public void OnLink(ESBuffChangedLink link)
            {
                Changes.Add(link);
            }
        }

        private sealed class PoolableTagCountReceiver : IReceiveLink<ESTagCountChangedLink>, IPoolableAuto
        {
            private readonly Action<ESTagCountChangedLink> callback;

            public bool IsRecycled { get; set; }
            public int DeliveryCount { get; private set; }
            public int RecycleCount { get; private set; }

            public PoolableTagCountReceiver(Action<ESTagCountChangedLink> callback)
            {
                this.callback = callback;
            }

            public void OnLink(ESTagCountChangedLink link)
            {
                DeliveryCount++;
                callback(link);
            }

            public void TryAutoPushedToPool()
            {
                RecycleCount++;
                IsRecycled = true;
            }

            public void OnResetAsPoolable()
            {
            }
        }

        private static ESTagBakeTable CreateCatalog(string tagKey, ushort tagRuntimeKey, ESTagAvailability availability)
        {
            var entries = new List<ESTagBakeTable.Entry>(33);
            for (ushort value = ESGameTagCatalog.FirstDefinedValue; value <= ESGameTagCatalog.LastDefinedValue; value++)
            {
                entries.Add(new ESTagBakeTable.Entry
                {
                    enumGroup = ESTagEnumGroup.Primary,
                    enumValue = value,
                    bakedId = value,
                    storageTier = ESTagStorageTier.HotSlot,
                    availability = ESGameTagCatalog.IsUsableInNewConfiguration((ESGameTag)value)
                        ? ESTagAvailability.Runtime
                        : ESTagAvailability.Deprecated,
                    stableTransferScopes = ESTagStableTransferScope.SaveGame | ESTagStableTransferScope.Network
                });
            }

            entries.Add(new ESTagBakeTable.Entry
            {
                key = tagKey,
                bakedId = tagRuntimeKey,
                storageTier = ESTagStorageTier.Sparse,
                availability = availability,
                stableTransferScopes = ESTagStableTransferScope.SaveGame | ESTagStableTransferScope.Network
            });
            entries.Add(new ESTagBakeTable.Entry
            {
                key = "tests.deprecated",
                bakedId = (ushort)(tagRuntimeKey + 1),
                storageTier = ESTagStorageTier.Sparse,
                availability = ESTagAvailability.Deprecated,
                deprecatedReplacement = ESTagStableReference.FromString(tagKey),
                stableTransferScopes = ESTagStableTransferScope.None
            });

            ESTagBakeTable table = ScriptableObject.CreateInstance<ESTagBakeTable>();
            FieldInfo field = typeof(ESTagBakeTable).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(table, entries);
            table.BuildRuntimeCache();
            return table;
        }

        private static UnityEngine.Object CreateActorDefinition(ESTagStableReference intrinsicTag)
        {
            Type actorDataType = typeof(Entity).Assembly.GetType("ES.ActorDataInfo", true);
            ScriptableObject definition = ScriptableObject.CreateInstance(actorDataType);
            FieldInfo tagsField = actorDataType.GetField("tags", BindingFlags.Instance | BindingFlags.Public);
            var tags = tagsField?.GetValue(definition) as List<ESTagStableReference>;
            Assert.That(tags, Is.Not.Null, "ActorDataInfo must expose its direct tags list.");
            tags.Add(intrinsicTag);
            return definition;
        }

        private static bool BindActorDefinition(Entity entity, UnityEngine.Object definition)
        {
            Type actorDataType = typeof(Entity).Assembly.GetType("ES.ActorDataInfo", true);
            MethodInfo bindDefinition = typeof(Entity).GetMethod(
                "BindDefinition", BindingFlags.Instance | BindingFlags.Public, null, new[] { actorDataType }, null);
            Assert.That(bindDefinition, Is.Not.Null, "Entity must retain the ActorDataInfo definition binding API.");
            return (bool)bindDefinition.Invoke(entity, new object[] { definition });
        }

        private static UnityEngine.Object CreateItemDefinition(ESTagStableReference intrinsicTag)
        {
            Type itemDataType = typeof(Item).Assembly.GetType("ES.ItemDataInfo", true);
            ScriptableObject definition = ScriptableObject.CreateInstance(itemDataType);
            FieldInfo tagsField = itemDataType.GetField("tags", BindingFlags.Instance | BindingFlags.Public);
            var tags = tagsField?.GetValue(definition) as List<ESTagStableReference>;
            Assert.That(tags, Is.Not.Null, "ItemDataInfo must expose its direct tags list.");
            tags.Add(intrinsicTag);
            return definition;
        }

        private static bool BindItemDefinition(Item item, UnityEngine.Object definition)
        {
            Type itemDataType = typeof(Item).Assembly.GetType("ES.ItemDataInfo", true);
            MethodInfo bindDefinition = typeof(Item).GetMethod(
                "BindDefinition", BindingFlags.Instance | BindingFlags.Public, null, new[] { itemDataType }, null);
            Assert.That(bindDefinition, Is.Not.Null, "Item must retain the ItemDataInfo definition binding API.");
            return (bool)bindDefinition.Invoke(item, new object[] { definition });
        }

        private static void SetItemPrefabDefinition(Item item, UnityEngine.Object definition)
        {
            FieldInfo prefabDefinition = typeof(Item).GetField("prefabDefinition", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(prefabDefinition, Is.Not.Null, "Item must expose one direct Prefab definition reference.");
            prefabDefinition.SetValue(item, definition);
        }

        private static void ResetRuntimeCatalogForTest()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            Type catalogType = typeof(ESTagRuntimeCatalog);
            catalogType.GetField("table", flags)?.SetValue(null, null);
            catalogType.GetField("schemaHash", flags)?.SetValue(null, null);
            catalogType.GetField("runtimeLayoutHash", flags)?.SetValue(null, null);
            catalogType.GetField("CatalogBound", flags)?.SetValue(null, null);
        }
    }
}
