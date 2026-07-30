using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESTagCatalogRuntimeTests
    {
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

                var atomicFailureGrants = new ESTagGrantConfig();
                atomicFailureGrants.tags.Add(ESTagStableReference.From((ESGameTag)13));
                atomicFailureGrants.tags.Add(ESTagStableReference.FromString("tests.missing.tag"));
                using (var atomicFailureLeases = new ESTagLeaseSet())
                {
                    Assert.That(atomicFailureLeases.TryAcquire(entity.Tags, atomicFailureGrants, new object(), out string atomicError), Is.False);
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
    }
}
