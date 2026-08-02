using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using ES.Internal;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class ESCharacterAttributeRoutingTests
    {
        private readonly List<GameObject> createdEntities = new List<GameObject>();

        private Entity CreateEntity()
        {
            var gameObject = new GameObject("ESCharacterAttributeRoutingTests.Entity");
            createdEntities.Add(gameObject);
            return gameObject.AddComponent<Entity>();
        }

        private Item CreateItem()
        {
            var gameObject = new GameObject("ESCharacterAttributeRoutingTests.Item");
            createdEntities.Add(gameObject);
            return gameObject.AddComponent<Item>();
        }

        private static ESSuperAttributeCatalog CreateItemAttributeCatalog()
        {
            ESSuperAttributeTable table = new ESSuperAttributeTable
            {
                catalogScope = ESAttributeBakeTable.ItemScope,
                floatAttributes = new List<ESSuperFloatAttributeDefinition>
                {
                    new ESSuperFloatAttributeDefinition
                    {
                        enumKey = 700,
                        key = "Item.Test.EffectPower",
                        storagePolicy = ESKeyStoragePolicy.HotSlot
                    }
                },
                permitAttributes = new List<ESSuperPermitAttributeDefinition>
                {
                    new ESSuperPermitAttributeDefinition
                    {
                        enumKey = 701,
                        key = "Item.Test.CanUse",
                        storagePolicy = ESKeyStoragePolicy.HotSlot
                    }
                }
            };

            Assert.That(table.TryBuildCatalog(out ESSuperAttributeCatalog catalog, out string error), Is.True, error);
            return catalog;
        }

        private static void BindItemAttributeCatalogForTest(Item item, ESSuperAttributeCatalog catalog)
        {
            MethodInfo method = typeof(Item).GetMethod("BindItemAttributeCatalog", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(item, new object[] { catalog });
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = createdEntities.Count - 1; i >= 0; i--)
            {
                if (createdEntities[i] != null)
                    Object.DestroyImmediate(createdEntities[i]);
            }

            createdEntities.Clear();
        }
        [Test]
        public void Catalog_MapsEachCanonicalKeyToExactlyOneTypedSlot()
        {
            Assert.That(
                ESCharacterAttributeCatalog.TryGetFloatId(
                    ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed,
                    out ESCharacterFloatAttributeId speedId),
                Is.True);
            Assert.That(speedId, Is.EqualTo(ESCharacterFloatAttributeId.GroundMaxMoveSpeed));
            Assert.That(ESCharacterAttributeCatalog.GetKey(speedId), Is.EqualTo(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed));

            Assert.That(
                ESCharacterAttributeCatalog.TryGetPermitId(
                    ESCharacterSuperAttributeKeys.Jump,
                    out ESCharacterPermitAttributeId jumpId),
                Is.True);
            Assert.That(jumpId, Is.EqualTo(ESCharacterPermitAttributeId.Jump));
            Assert.That(ESCharacterAttributeCatalog.TryGetFloatId(ESCharacterSuperAttributeKeys.Jump, out _), Is.False);
            Assert.That(ESCharacterAttributeCatalog.TryGetPermitId(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, out _), Is.False);
        }

        [Test]
        public void DefaultFixedCharacterRows_AreHotAndExposeTypedApiNames()
        {
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();

            for (int i = 0; i < table.floatAttributes.Count; i++)
            {
                ESSuperFloatAttributeDefinition definition = table.floatAttributes[i];
                Assert.That(definition.fixedApiName, Is.Not.Empty);
                Assert.That(definition.storagePolicy, Is.EqualTo(ESKeyStoragePolicy.HotSlot));
                Assert.That(ESCharacterAttributeCatalog.TryGetFloatId(definition.enumKey, out _), Is.True);
            }

            for (int i = 0; i < table.permitAttributes.Count; i++)
            {
                ESSuperPermitAttributeDefinition definition = table.permitAttributes[i];
                Assert.That(definition.fixedApiName, Is.Not.Empty);
                Assert.That(definition.storagePolicy, Is.EqualTo(ESKeyStoragePolicy.HotSlot));
                Assert.That(ESCharacterAttributeCatalog.TryGetPermitId(definition.enumKey, out _), Is.True);
            }
        }

        [Test]
        public void CharacterScope_AddsMissingCompiledBuiltinsWithoutOverwritingAuthoredRows()
        {
            var table = new ESSuperAttributeTable
            {
                catalogScope = ESAttributeBakeTable.CharacterScope,
                floatAttributes = new List<ESSuperFloatAttributeDefinition>(),
                permitAttributes = new List<ESSuperPermitAttributeDefinition>()
            };

            ESCharacterAttributeCatalog.EnsureCharacterScope(table);
            Assert.That(table.floatAttributes.Count, Is.EqualTo(ESCharacterAttributeCatalog.FloatCount));
            Assert.That(table.permitAttributes.Count, Is.EqualTo(ESCharacterAttributeCatalog.PermitCount));

            table.floatAttributes[0].overrideBaseValue = true;
            table.floatAttributes[0].baseValue = 42f;
            ESCharacterAttributeCatalog.EnsureCharacterScope(table);
            Assert.That(table.floatAttributes.Count, Is.EqualTo(ESCharacterAttributeCatalog.FloatCount));
            Assert.That(table.floatAttributes[0].baseValue, Is.EqualTo(42f));
        }

        [Test]
        public void AttributeBake_RejectsFixedApiOutsideCharacterHotSlot()
        {
            ESSuperAttributeTable character = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            character.floatAttributes[0].storagePolicy = ESKeyStoragePolicy.Sparse;
            ESSuperAttributeTable item = new ESSuperAttributeTable
            {
                catalogScope = ESAttributeBakeTable.ItemScope,
                floatAttributes = new List<ESSuperFloatAttributeDefinition>(),
                permitAttributes = new List<ESSuperPermitAttributeDefinition>()
            };

            Assert.That(ESAttributeBakeTable.TryValidateSources(character, item, out string error), Is.False);
            Assert.That(error, Does.Contain("fixedApiName"));
        }

        [Test]
        public void BuffValueChanges_RequireCharacterCatalogAndExactValueKind()
        {
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            Assert.That(table.TryBuildCatalog(out ESSuperAttributeCatalog catalog, out string catalogError), Is.True, catalogError);

            var valid = new BuffSharedData
            {
                floatChanges = new List<ESBuffFloatValueChangeBinding>
                {
                    new ESBuffFloatValueChangeBinding
                    {
                        attributeEnumKey = (ushort)ESCharacterAttributeEnumKey.GroundMaxMoveSpeed,
                        statKey = ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed,
                        change = new ESFloatValueChangeExpressionBinding()
                    }
                },
                permitChanges = new List<ESBuffPermitValueChangeBinding>
                {
                    new ESBuffPermitValueChangeBinding
                    {
                        attributeEnumKey = (ushort)ESCharacterAttributeEnumKey.Jump,
                        permitKey = ESCharacterSuperAttributeKeys.Jump,
                        change = new ESPermitValueChangeExpressionBinding()
                    }
                }
            };

            Assert.That(valid.TryValidateValueChangeConfiguration(catalog, out string validError), Is.True, validError);

            valid.floatChanges[0].attributeEnumKey = (ushort)ESCharacterAttributeEnumKey.Jump;
            valid.floatChanges[0].statKey = ESCharacterSuperAttributeKeys.Jump;
            Assert.That(valid.TryValidateValueChangeConfiguration(catalog, out string wrongKindError), Is.False);
            Assert.That(wrongKindError, Does.Contain("Float"));

            valid.floatChanges[0].attributeEnumKey = (ushort)ESCharacterAttributeEnumKey.GroundMaxMoveSpeed;
            valid.floatChanges[0].statKey = ESCharacterSuperAttributeKeys.Jump;
            Assert.That(valid.TryValidateValueChangeConfiguration(catalog, out string mismatchError), Is.False);
            Assert.That(mismatchError, Does.Contain("双别名"));
        }

        [Test]
        public void AttributeBake_InvalidReplacementKeepsPreviousSchemaAndHotSparseLayout()
        {
            ESAttributeBakeTable bakeTable = ScriptableObject.CreateInstance<ESAttributeBakeTable>();
            try
            {
                ESSuperAttributeTable character = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
                ESSuperAttributeTable item = new ESSuperAttributeTable
                {
                    catalogScope = ESAttributeBakeTable.ItemScope,
                    floatAttributes = new List<ESSuperFloatAttributeDefinition>
                    {
                        new ESSuperFloatAttributeDefinition
                        {
                            enumKey = 1,
                            key = "Item.Combat.Damage",
                            storagePolicy = ESKeyStoragePolicy.HotSlot,
                            overrideBaseValue = true,
                            baseValue = 20f
                        },
                        new ESSuperFloatAttributeDefinition
                        {
                            enumKey = 2,
                            key = "Item.Enchant.Sharpness",
                            storagePolicy = ESKeyStoragePolicy.Sparse,
                            overrideBaseValue = true,
                            baseValue = 3f
                        }
                    }
                };

                Assert.That(bakeTable.TryReplaceFrom(character, item, out string bakeError), Is.True, bakeError);
                string previousSchemaHash = bakeTable.SchemaHash;
                Assert.That(bakeTable.TryBuildCatalogs(out _, out ESSuperAttributeCatalog itemCatalog, out string catalogError), Is.True, catalogError);
                Assert.That(itemCatalog.TryGetRuntimeKey(1, "Item.Combat.Damage", out int hotRuntimeKey), Is.True);
                Assert.That(itemCatalog.TryGetRuntimeKey(2, "Item.Enchant.Sharpness", out int sparseRuntimeKey), Is.True);
                Assert.That(itemCatalog.TryGetFloatHotSlot(hotRuntimeKey, out _), Is.True);
                Assert.That(itemCatalog.TryGetFloatHotSlot(sparseRuntimeKey, out _), Is.False);

                item.floatAttributes[1].formula = "not-supported";
                Assert.That(bakeTable.TryReplaceFrom(character, item, out string rejectedError), Is.False);
                Assert.That(rejectedError, Does.Contain("formula"));
                Assert.That(bakeTable.SchemaHash, Is.EqualTo(previousSchemaHash));
                Assert.That(bakeTable.TryValidate(out string retainedError), Is.True, retainedError);
            }
            finally
            {
                Object.DestroyImmediate(bakeTable);
            }
        }

        [Test]
        public void CatalogDefinedCharacterHotAttribute_UsesHotArrayWithoutCompiledEnumMapping()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            table.floatAttributes.Add(new ESSuperFloatAttributeDefinition
            {
                enumKey = 501,
                key = "Character.Combat.CritDamage",
                storagePolicy = ESKeyStoragePolicy.HotSlot,
                overrideBaseValue = true,
                baseValue = 1.5f,
                minValue = 0f,
                maxValue = 10f
            });
            entity.BindSuperAttributeTable(table);

            Assert.That(table.TryGetRuntimeKey(501, "Character.Combat.CritDamage", out int runtimeKey), Is.True);
            Assert.That(entity.GetFloatStatValue(runtimeKey), Is.EqualTo(1.5f));
            Assert.That(entity.TryGetFloatStat(runtimeKey, out _), Is.False);
            Assert.That(entity.GetFloatStat(runtimeKey), Is.Not.Null);
            Assert.That(entity.GetFloatStatValue(501, "Character.Combat.CritDamage"), Is.EqualTo(1.5f));
            Assert.That(entity.TryGetFloatStat(runtimeKey, out ESFloatValueChangeSet set), Is.True);
            set.Add(ESFloatValueChangeOp.Add, 0.5f);
            Assert.That(entity.GetFloatStatValue(501, "Character.Combat.CritDamage"), Is.EqualTo(2f));
        }

        [Test]
        public void FixedSlots_RemainLazyAndCustomStatsRemainSparse()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            table.floatAttributes.Add(new ESSuperFloatAttributeDefinition
            {
                enumKey = 501,
                key = "Item.Enchantment.Sharpness",
                storagePolicy = ESKeyStoragePolicy.Sparse,
                overrideBaseValue = true,
                baseValue = 3f
            });
            entity.BindSuperAttributeTable(table);

            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(5f));
            Assert.That(entity.TryGetFloatStat(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, out _), Is.False);

            ESFloatValueChangeSet fixedSet = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            fixedSet.Add(ESFloatValueChangeOp.Add, 2f);
            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(7f));
            Assert.That(entity.TryGetFloatStat(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, out _), Is.True);

            Assert.That(entity.GetFloatStat("Item.Enchantment.Unregistered", 3f), Is.Null);
            ESFloatValueChangeSet enchantmentSet = entity.GetFloatStat(501, "Item.Enchantment.Sharpness", 0f);
            enchantmentSet.Add(ESFloatValueChangeOp.Add, 4f);
            Assert.That(entity.GetFloatStatValue(501, "Item.Enchantment.Sharpness", 0f), Is.EqualTo(7f));
            Assert.That(entity.GetPermit(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed), Is.Null);
            Assert.That(entity.GetFloatStat(ESCharacterSuperAttributeKeys.Jump), Is.Null);
        }

        [Test]
        public void Entity_FloatStatDebugSnapshot_IsReadOnlyAndShowsHotAndSparseStats()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            ESSuperFloatAttributeDefinition speedDefinition = table.floatAttributes[(int)ESCharacterFloatAttributeId.GroundMaxMoveSpeed];
            speedDefinition.overrideBaseValue = true;
            speedDefinition.baseValue = 12f;
            speedDefinition.minValue = 2f;
            speedDefinition.maxValue = 20f;
            table.floatAttributes.Add(new ESSuperFloatAttributeDefinition
            {
                enumKey = 501,
                key = "Item.Enchantment.Sharpness",
                displayName = "锐利",
                storagePolicy = ESKeyStoragePolicy.Sparse,
                overrideBaseValue = true,
                baseValue = 3f,
                minValue = 0f,
                maxValue = 8f
            });
            entity.BindSuperAttributeTable(table);

            Assert.That(entity.TryGetFloatStat(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, out _), Is.False);
            Assert.That(entity.TryGetFloatStat("Item.Enchantment.Sharpness", out _), Is.False);

            Assert.That(entity.TryGetFloatStatDebugSnapshot(
                (ushort)ESCharacterAttributeEnumKey.GroundMaxMoveSpeed,
                ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed,
                5f,
                out ESFloatStatSnapshot speedSnapshot), Is.True);
            Assert.That(speedSnapshot.value, Is.EqualTo(12f));
            Assert.That(speedSnapshot.definitionMinimum, Is.EqualTo(2f));
            Assert.That(speedSnapshot.definitionMaximum, Is.EqualTo(20f));

            Assert.That(entity.TryGetFloatStatDebugSnapshot(501, "Item.Enchantment.Sharpness", 0f, out ESFloatStatSnapshot sparseSnapshot), Is.True);
            Assert.That(sparseSnapshot.value, Is.EqualTo(3f));
            Assert.That(entity.TryGetFloatStat("Item.Enchantment.Sharpness", out _), Is.False);

            List<ESFloatStatDebugEntry> entries = new List<ESFloatStatDebugEntry>();
            entity.CopyFloatStatDebugEntriesTo(entries, 5f);

            bool foundSpeed = false;
            bool foundSharpness = false;
            for (int i = 0; i < entries.Count; i++)
            {
                ESFloatStatDebugEntry entry = entries[i];
                if (entry.stringKey == ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed)
                {
                    foundSpeed = true;
                    Assert.That(entry.storagePolicy, Is.EqualTo(ESKeyStoragePolicy.HotSlot));
                    Assert.That(entry.isMaterialized, Is.False);
                    Assert.That(entry.runtimeKey, Is.GreaterThan(0));
                    Assert.That(entry.stat.value, Is.EqualTo(12f));
                }
                else if (entry.stringKey == "Item.Enchantment.Sharpness")
                {
                    foundSharpness = true;
                    Assert.That(entry.displayName, Is.EqualTo("锐利"));
                    Assert.That(entry.storagePolicy, Is.EqualTo(ESKeyStoragePolicy.Sparse));
                    Assert.That(entry.isMaterialized, Is.False);
                    Assert.That(entry.stat.value, Is.EqualTo(3f));
                }
            }

            Assert.That(foundSpeed, Is.True);
            Assert.That(foundSharpness, Is.True);

            entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f)
                .Add(ESFloatValueChangeOp.Add, 4f);
            entity.GetFloatStat(501, "Item.Enchantment.Sharpness", 0f)
                .Add(ESFloatValueChangeOp.Add, 9f);

            entity.CopyFloatStatDebugEntriesTo(entries, 5f);
            for (int i = 0; i < entries.Count; i++)
            {
                ESFloatStatDebugEntry entry = entries[i];
                if (entry.stringKey == ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed)
                {
                    Assert.That(entry.isMaterialized, Is.True);
                    Assert.That(entry.stat.value, Is.EqualTo(16f));
                }
                else if (entry.stringKey == "Item.Enchantment.Sharpness")
                {
                    Assert.That(entry.isMaterialized, Is.True);
                    Assert.That(entry.stat.value, Is.EqualTo(8f));
                }
            }
        }

        [Test]
        public void Entity_FloatStatDebugEntries_KeepLiveCharacterSlotsVisibleWithASparseOnlyTable()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = new ESSuperAttributeTable
            {
                floatAttributes = new List<ESSuperFloatAttributeDefinition>
                {
                    new ESSuperFloatAttributeDefinition
                    {
                        enumKey = 501,
                        key = "Item.Enchantment.Sharpness",
                        storagePolicy = ESKeyStoragePolicy.Sparse,
                        overrideBaseValue = true,
                        baseValue = 3f
                    }
                }
            };
            entity.BindSuperAttributeTable(table);
            entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f)
                .Add(ESFloatValueChangeOp.Add, 2f);
            Assert.That(entity.TryGetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, out _), Is.True);

            List<ESFloatStatDebugEntry> entries = new List<ESFloatStatDebugEntry>();
            entity.CopyFloatStatDebugEntriesTo(entries, 5f);

            bool foundCharacterSpeed = false;
            bool foundSparse = false;
            for (int i = 0; i < entries.Count; i++)
            {
                ESFloatStatDebugEntry entry = entries[i];
                if (entry.stringKey == ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed)
                {
                    foundCharacterSpeed = true;
                    Assert.That(entry.isMaterialized, Is.True);
                    Assert.That(entry.stat.value, Is.EqualTo(7f));
                }
                else if (entry.stringKey == "Item.Enchantment.Sharpness")
                {
                    foundSparse = true;
                }
            }

            Assert.That(foundCharacterSpeed, Is.True);
            Assert.That(foundSparse, Is.True);
        }

        [Test]
        public void FixedPermitSlot_UsesSameResolverAndDoesNotCreateOnRead()
        {
            Entity entity = CreateEntity();
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, false), Is.False);
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.Jump, out _), Is.False);

            ESPermitSet set = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, false);
            set.Add(ESPermitLaw.HardEnable);
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, false), Is.True);
            Assert.That(entity.GetPermitResult(ESCharacterSuperAttributeKeys.Jump, false).decision, Is.EqualTo(ESPermitLaw.HardEnable));
        }

        [Test]
        public void FixedSlots_CacheDefinitionBaseOverridesWithoutCreatingResolvers()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            table.floatAttributes[(int)ESCharacterFloatAttributeId.GroundMaxMoveSpeed].overrideBaseValue = true;
            table.floatAttributes[(int)ESCharacterFloatAttributeId.GroundMaxMoveSpeed].baseValue = 12f;
            table.permitAttributes[(int)ESCharacterPermitAttributeId.Jump].overrideFallbackValue = true;
            table.permitAttributes[(int)ESCharacterPermitAttributeId.Jump].fallbackValue = false;
            entity.BindSuperAttributeTable(table);

            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(12f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.False);
            Assert.That(entity.TryGetFloatStat(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, out _), Is.False);
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.Jump, out _), Is.False);
        }

        [Test]
        public void ExplicitRuntimeBasesOutrankDefinitionDefaultsWithoutMaterializingFixedSlots()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            table.floatAttributes[(int)ESCharacterFloatAttributeId.GroundMaxMoveSpeed].overrideBaseValue = true;
            table.floatAttributes[(int)ESCharacterFloatAttributeId.GroundMaxMoveSpeed].baseValue = 12f;
            table.permitAttributes[(int)ESCharacterPermitAttributeId.Jump].overrideFallbackValue = true;
            table.permitAttributes[(int)ESCharacterPermitAttributeId.Jump].fallbackValue = false;
            table.floatAttributes.Add(new ESSuperFloatAttributeDefinition
            {
                enumKey = 501,
                key = "Item.Enchantment.Sharpness",
                storagePolicy = ESKeyStoragePolicy.Sparse,
                overrideBaseValue = true,
                baseValue = 3f
            });
            entity.BindSuperAttributeTable(table);

            entity.SetCharacterFloatStatBaseValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 7f);
            entity.SetCharacterPermitFallbackValue(ESCharacterPermitAttributeId.Jump, true);
            entity.SetFloatStatBaseValue(501, "Item.Enchantment.Sharpness", 9f);

            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(7f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, false), Is.True);
            Assert.That(entity.TryGetFloatStat(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, out _), Is.False);
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.Jump, out _), Is.False);
            Assert.That(entity.TryGetFloatStat((ushort)501, "Item.Enchantment.Sharpness", out _), Is.False);
            Assert.That(entity.GetFloatStatValue(501, "Item.Enchantment.Sharpness", 0f), Is.EqualTo(9f));
        }

        [Test]
        public void ExplicitRuntimeFloatBasesRejectNonFiniteValues()
        {
            Entity entity = CreateEntity();

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                entity.SetCharacterFloatStatBaseValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                entity.SetFloatStatBaseValue(0, "Custom.Stat", float.PositiveInfinity));
        }

        [Test]
        public void DisabledSuperAttributeTableFallsBackToBuiltInCharacterDefaults()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            table.enabled = false;
            entity.BindSuperAttributeTable(table);

            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(5f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, false), Is.False);
            Assert.That(entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.Not.Null);
        }

        [Test]
        public void EffectLease_ReleasesEveryOwnedModifierAndGuardsTableReset()
        {
            Entity entity = CreateEntity();
            ESEffectLease lease = entity.CreateValueChangeEffectLease();
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            Assert.That(lease.TryAddFloat(speed, ESFloatValueChangeOp.Add, 3f, 0, 0, true, out _), Is.True);
            Assert.That(lease.TryAddPermit(jump, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);

            Assert.That(entity.ActiveValueChangeEffectCount, Is.EqualTo(1));
            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(8f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => entity.ClearValueChanges());

            lease.Dispose();
            Assert.That(entity.ActiveValueChangeEffectCount, Is.Zero);
            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(5f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.True);
            Assert.DoesNotThrow(() => entity.ClearValueChanges());
        }

        [Test]
        public void EntityDestroy_InvalidatesActiveEffectLease()
        {
            Entity entity = CreateEntity();
            ESEffectLease lease = entity.CreateValueChangeEffectLease();
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            Assert.That(lease.TryAddFloat(speed, ESFloatValueChangeOp.Add, 3f, 0, 0, true, out _), Is.True);

            Object.DestroyImmediate(entity.gameObject);

            Assert.That(lease.TryRelease(), Is.False);
        }

        [Test]
        public void ReleaseEffect_CleansEverySetAndAllowsANewLease_WhenChangedThrows()
        {
            Entity entity = CreateEntity();
            ESEffectLease lease = entity.CreateValueChangeEffectLease();
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            Assert.That(lease.TryAddFloat(speed, ESFloatValueChangeOp.Add, 3f, 0, 0, true, out _), Is.True);
            Assert.That(lease.TryAddPermit(jump, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);

            System.Action<ESFloatValueChangeSet> throwFloat = _ => throw new System.InvalidOperationException("Float release listener failure.");
            System.Action<ESPermitSet> throwPermit = _ => throw new System.InvalidOperationException("Permit release listener failure.");
            speed.Changed += throwFloat;
            jump.Changed += throwPermit;
            LogAssert.Expect(LogType.Exception, new Regex("Float release listener failure"));
            LogAssert.Expect(LogType.Exception, new Regex("Permit release listener failure"));

            Assert.DoesNotThrow(() => lease.Dispose());
            Assert.That(speed.Count, Is.Zero);
            Assert.That(jump.Count, Is.Zero);
            Assert.That(entity.ActiveValueChangeEffectCount, Is.Zero);

            speed.Changed -= throwFloat;
            jump.Changed -= throwPermit;

            ESEffectLease nextLease = entity.CreateValueChangeEffectLease();
            Assert.That(nextLease.IsValid, Is.True);
            Assert.That(nextLease.TryAddFloat(speed, ESFloatValueChangeOp.Add, 1f, 0, 0, true, out _), Is.True);
            Assert.That(nextLease.TryAddPermit(jump, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);

            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(6f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.False);
            nextLease.Dispose();
        }

        [Test]
        public void EntityDestroy_InvalidatesLeasesAndClearsEverySet_WhenChangedThrows()
        {
            Entity entity = CreateEntity();
            ESEffectLease lease = entity.CreateValueChangeEffectLease();
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            Assert.That(lease.TryAddFloat(speed, ESFloatValueChangeOp.Add, 3f, 0, 0, true, out _), Is.True);
            Assert.That(lease.TryAddPermit(jump, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);

            speed.Changed += _ => throw new System.InvalidOperationException("Float destroy listener failure.");
            jump.Changed += _ => throw new System.InvalidOperationException("Permit destroy listener failure.");
            LogAssert.Expect(LogType.Exception, new Regex("Float destroy listener failure"));
            LogAssert.Expect(LogType.Exception, new Regex("Permit destroy listener failure"));

            Assert.DoesNotThrow(() => Object.DestroyImmediate(entity.gameObject));
            Assert.That(speed.Count, Is.Zero);
            Assert.That(jump.Count, Is.Zero);
            Assert.That(lease.TryRelease(), Is.False);
        }

        [Test]
        public void ClearValueChanges_RejectsReentrantEffectCreation()
        {
            Entity entity = CreateEntity();
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            speed.Add(ESFloatValueChangeOp.Add, 1f);
            bool rejected = false;
            speed.Changed += _ =>
            {
                try
                {
                    entity.CreateValueChangeEffectLease();
                }
                catch (System.InvalidOperationException)
                {
                    rejected = true;
                }
            };

            entity.ClearValueChanges();

            Assert.That(rejected, Is.True);
            Assert.That(entity.ActiveValueChangeEffectCount, Is.Zero);
        }

        [Test]
        public void ClearValueChanges_RejectsReentrantStatAndPermitWritesAndClearsEverySet()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            table.floatAttributes.Add(new ESSuperFloatAttributeDefinition
            {
                enumKey = 501,
                key = "Item.Enchantment.Primary",
                storagePolicy = ESKeyStoragePolicy.Sparse
            });
            table.floatAttributes.Add(new ESSuperFloatAttributeDefinition
            {
                enumKey = 502,
                key = "Item.Enchantment.Secondary",
                storagePolicy = ESKeyStoragePolicy.Sparse
            });
            entity.BindSuperAttributeTable(table);

            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESFloatValueChangeSet primary = entity.GetFloatStat(501, "Item.Enchantment.Primary", 0f);
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            speed.Add(ESFloatValueChangeOp.Add, 1f);
            primary.Add(ESFloatValueChangeOp.Add, 1f);
            jump.Add(ESPermitLaw.HardDisable);

            bool characterRejected = false;
            bool sparseRejected = false;
            bool permitRejected = false;
            bool baseRejected = false;
            speed.Changed += _ =>
            {
                try
                {
                    entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f)
                        .Add(ESFloatValueChangeOp.Add, 10f);
                }
                catch (System.InvalidOperationException)
                {
                    characterRejected = true;
                }

                try
                {
                    entity.SetCharacterFloatStatBaseValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 99f);
                }
                catch (System.InvalidOperationException)
                {
                    baseRejected = true;
                }

                try
                {
                    entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true)
                        .Add(ESPermitLaw.HardEnable);
                }
                catch (System.InvalidOperationException)
                {
                    permitRejected = true;
                }
            };
            primary.Changed += _ =>
            {
                try
                {
                    entity.GetFloatStat(502, "Item.Enchantment.Secondary", 0f)
                        .Add(ESFloatValueChangeOp.Add, 10f);
                }
                catch (System.InvalidOperationException)
                {
                    sparseRejected = true;
                }
            };

            Assert.DoesNotThrow(() => entity.ClearValueChanges());

            Assert.That(characterRejected, Is.True);
            Assert.That(sparseRejected, Is.True);
            Assert.That(permitRejected, Is.True);
            Assert.That(baseRejected, Is.True);
            Assert.That(speed.Count, Is.Zero);
            Assert.That(primary.Count, Is.Zero);
            Assert.That(jump.Count, Is.Zero);
            Assert.That(entity.TryGetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, out _), Is.False);
            Assert.That(entity.TryGetFloatStat(501, "Item.Enchantment.Primary", out _), Is.False);
            Assert.That(entity.TryGetFloatStat(502, "Item.Enchantment.Secondary", out _), Is.False);
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.Jump, out _), Is.False);
        }

        [Test]
        public void PoolDespawn_ReusesWarmStatStorageWithoutLeakingPreviousLifecycleState()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            table.floatAttributes.Add(new ESSuperFloatAttributeDefinition
            {
                enumKey = 501,
                key = "Item.Enchantment.Pooled",
                storagePolicy = ESKeyStoragePolicy.Sparse
            });
            entity.BindSuperAttributeTable(table);

            ESEffectLease previousLease = entity.CreateValueChangeEffectLease();
            ESFloatValueChangeSet previousHot = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESFloatValueChangeSet previousSparse = entity.GetFloatStat(501, "Item.Enchantment.Pooled", 0f);
            ESPermitSet previousPermit = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            int previousObserverNotifications = 0;
            previousHot.Changed += _ => previousObserverNotifications++;
            Assert.That(previousLease.TryAddFloat(previousHot, ESFloatValueChangeOp.Add, 3f, 0, 0, true, out _), Is.True);
            Assert.That(previousLease.TryAddFloat(previousSparse, ESFloatValueChangeOp.Add, 4f, 0, 0, true, out _), Is.True);
            Assert.That(previousLease.TryAddPermit(previousPermit, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);
            entity.SetCharacterFloatStatBaseValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 17f);
            entity.SetCharacterPermitFallbackValue(ESCharacterPermitAttributeId.Jump, false);

            entity.OnPoolDespawned();

            Assert.That(entity.TryGetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, out _), Is.False);
            Assert.That(entity.TryGetFloatStat(501, "Item.Enchantment.Pooled", out _), Is.False);
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.Jump, out _), Is.False);
            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(5f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.True);
            Assert.That(previousLease.TryRelease(), Is.False);

            // A stale raw Set reference is unsupported across lifetimes, but an accidental write
            // while the Entity is inactive must still be discarded before the next activation.
            previousHot.Add(ESFloatValueChangeOp.Add, 99f);
            previousSparse.Add(ESFloatValueChangeOp.Add, 99f);
            previousPermit.Add(ESPermitLaw.HardDisable);

            ESEffectLease nextLease = entity.CreateValueChangeEffectLease();
            ESFloatValueChangeSet nextHot = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESFloatValueChangeSet nextSparse = entity.GetFloatStat(501, "Item.Enchantment.Pooled", 0f);
            ESPermitSet nextPermit = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            Assert.That(ReferenceEquals(previousHot, nextHot), Is.True);
            Assert.That(ReferenceEquals(previousSparse, nextSparse), Is.True);
            Assert.That(ReferenceEquals(previousPermit, nextPermit), Is.True);

            Assert.That(nextLease.TryAddFloat(nextHot, ESFloatValueChangeOp.Add, 2f, 0, 0, true, out _), Is.True);
            Assert.That(nextLease.TryAddFloat(nextSparse, ESFloatValueChangeOp.Add, 1f, 0, 0, true, out _), Is.True);
            Assert.That(nextLease.TryAddPermit(nextPermit, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);
            Assert.That(previousLease.TryRelease(), Is.False);
            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(7f));
            Assert.That(entity.GetFloatStatValue(501, "Item.Enchantment.Pooled", 0f), Is.EqualTo(1f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.False);
            Assert.That(previousObserverNotifications, Is.EqualTo(3));

            nextLease.Dispose();
        }

        [Test]
        public void EffectLease_RejectsDelayedWriteAfterItsSlotIsReused()
        {
            Entity entity = CreateEntity();
            ESEffectLease first = entity.CreateValueChangeEffectLease();
            ESEffectLease staleCopy = first;
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);

            Assert.That(first.TryAddFloat(speed, ESFloatValueChangeOp.Add, 2f, 0, 0, true, out _), Is.True);
            first.Dispose();

            ESEffectLease next = entity.CreateValueChangeEffectLease();
            Assert.That(staleCopy.IsValid, Is.False);
            Assert.That(staleCopy.TryAddFloat(speed, ESFloatValueChangeOp.Add, 99f, 0, 0, true, out _), Is.False);
            Assert.That(next.TryAddFloat(speed, ESFloatValueChangeOp.Add, 1f, 0, 0, true, out _), Is.True);
            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(6f));

            next.Dispose();
        }

        [Test]
        public void EffectLease_RejectsDelayedPermitWriteAfterItsSlotIsReused()
        {
            Entity entity = CreateEntity();
            ESEffectLease first = entity.CreateValueChangeEffectLease();
            ESEffectLease staleCopy = first;
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);

            Assert.That(first.TryAddPermit(jump, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);
            first.Dispose();

            ESEffectLease next = entity.CreateValueChangeEffectLease();
            Assert.That(staleCopy.IsValid, Is.False);
            Assert.That(staleCopy.TryAddPermit(jump, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.False);
            Assert.That(next.TryAddPermit(jump, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.False);

            next.Dispose();
        }

        [Test]
        public void EffectLease_RejectsFloatAndPermitSetsOwnedByAnotherEntity()
        {
            Entity first = CreateEntity();
            Entity second = CreateEntity();
            ESEffectLease lease = first.CreateValueChangeEffectLease();
            ESFloatValueChangeSet otherFloat = second.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESPermitSet otherPermit = second.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);

            Assert.That(lease.TryAddFloat(otherFloat, ESFloatValueChangeOp.Add, 99f, 0, 0, true, out _), Is.False);
            Assert.That(lease.TryAddPermit(otherPermit, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.False);
            Assert.That(otherFloat.Count, Is.Zero);
            Assert.That(otherPermit.Count, Is.Zero);

            lease.Dispose();
        }

        [Test]
        public void ItemEffectLease_RejectsDelayedFloatAndPermitWritesAfterSlotIsReused()
        {
            Item item = CreateItem();
            BindItemAttributeCatalogForTest(item, CreateItemAttributeCatalog());
            Assert.That(item.TryGetAttributeRuntimeKey(700, "Item.Test.EffectPower", out int floatRuntimeKey), Is.True);
            Assert.That(item.TryGetAttributeRuntimeKey(701, "Item.Test.CanUse", out int permitRuntimeKey), Is.True);

            ESFloatValueChangeSet power = item.GetFloatStat(floatRuntimeKey);
            ESPermitSet canUse = item.GetPermit(permitRuntimeKey, true);
            ESEffectLease first = item.CreateAttributeEffectLease();
            ESEffectLease staleCopy = first;
            Assert.That(first.TryAddFloat(power, ESFloatValueChangeOp.Add, 4f, 0, 0, true, out _), Is.True);
            Assert.That(first.TryAddPermit(canUse, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);
            first.Dispose();

            ESEffectLease next = item.CreateAttributeEffectLease();
            Assert.That(staleCopy.TryAddFloat(power, ESFloatValueChangeOp.Add, 99f, 0, 0, true, out _), Is.False);
            Assert.That(staleCopy.TryAddPermit(canUse, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.False);
            Assert.That(next.TryAddFloat(power, ESFloatValueChangeOp.Add, 1f, 0, 0, true, out _), Is.True);
            Assert.That(next.TryAddPermit(canUse, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.True);
            Assert.That(item.GetFloatStatValue(floatRuntimeKey), Is.EqualTo(1f));
            Assert.That(item.GetPermitValue(permitRuntimeKey, true), Is.False);

            next.Dispose();
        }

        [Test]
        public void ItemEffectLease_RejectsFloatAndPermitSetsOwnedByAnotherItem()
        {
            ESSuperAttributeCatalog catalog = CreateItemAttributeCatalog();
            Item first = CreateItem();
            Item second = CreateItem();
            BindItemAttributeCatalogForTest(first, catalog);
            BindItemAttributeCatalogForTest(second, catalog);
            Assert.That(second.TryGetAttributeRuntimeKey(700, "Item.Test.EffectPower", out int floatRuntimeKey), Is.True);
            Assert.That(second.TryGetAttributeRuntimeKey(701, "Item.Test.CanUse", out int permitRuntimeKey), Is.True);

            ESFloatValueChangeSet otherFloat = second.GetFloatStat(floatRuntimeKey);
            ESPermitSet otherPermit = second.GetPermit(permitRuntimeKey, true);
            ESEffectLease lease = first.CreateAttributeEffectLease();

            Assert.That(lease.TryAddFloat(otherFloat, ESFloatValueChangeOp.Add, 99f, 0, 0, true, out _), Is.False);
            Assert.That(lease.TryAddPermit(otherPermit, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.False);
            Assert.That(otherFloat.Count, Is.Zero);
            Assert.That(otherPermit.Count, Is.Zero);

            lease.Dispose();
        }

        [Test]
        public void EffectLease_RejectsUnboundStandaloneFloatAndPermitSets()
        {
            Entity entity = CreateEntity();
            ESEffectLease lease = entity.CreateValueChangeEffectLease();
            ESFloatValueChangeSet standaloneFloat = new ESFloatValueChangeSet();
            ESPermitSet standalonePermit = new ESPermitSet();

            Assert.That(lease.TryAddFloat(standaloneFloat, ESFloatValueChangeOp.Add, 99f, 0, 0, true, out _), Is.False);
            Assert.That(lease.TryAddPermit(standalonePermit, ESPermitLaw.HardDisable, 0, 0, true, out _), Is.False);
            Assert.That(standaloneFloat.Count, Is.Zero);
            Assert.That(standalonePermit.Count, Is.Zero);

            lease.Dispose();
        }

        [Test]
        public void FixedSlots_ApplyDefinitionBoundsWithAndWithoutResolvers()
        {
            Entity entity = CreateEntity();
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            ESSuperFloatAttributeDefinition speed = table.floatAttributes[(int)ESCharacterFloatAttributeId.GroundMaxMoveSpeed];
            speed.overrideBaseValue = true;
            speed.baseValue = 20f;
            speed.minValue = 2f;
            speed.maxValue = 10f;
            entity.BindSuperAttributeTable(table);

            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(10f));
            ESFloatValueChangeSet set = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            set.Add(ESFloatValueChangeOp.Override, -10f);
            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(2f));
        }

        [Test]
        public void SuperAttributeCatalog_RejectsUnsupportedFormula()
        {
            ESSuperAttributeTable table = new ESSuperAttributeTable
            {
                floatAttributes = new List<ESSuperFloatAttributeDefinition>
                {
                    new ESSuperFloatAttributeDefinition { key = "Combat.Attack", formula = "Level * 2" }
                }
            };

            Assert.That(table.TryBuildCatalog(out _, out string error), Is.False);
            Assert.That(error, Does.Contain("formula is not supported"));
        }

        [Test]
        public void SuperAttributeTable_IsGenericAndRejectsDuplicateAndCrossKindDefinitions()
        {
            ESSuperAttributeTable table = new ESSuperAttributeTable
            {
                floatAttributes = new List<ESSuperFloatAttributeDefinition>
                {
                    new ESSuperFloatAttributeDefinition { key = "Vehicle.Engine.MaxTorque" },
                    new ESSuperFloatAttributeDefinition { key = "Vehicle.Engine.MaxTorque" }
                },
                permitAttributes = new List<ESSuperPermitAttributeDefinition>
                {
                    new ESSuperPermitAttributeDefinition { key = "Vehicle.Engine.MaxTorque" }
                }
            };

            Assert.That(table.ValidateDefinitions(out string error), Is.False);
            Assert.That(error, Does.Contain("重复"));
            Assert.That(error, Does.Contain("同一 Key 同时声明为 Float 和 Permit"));
        }

        [Test]
        public void SuperAttributeCatalog_BindsEnumAndStringToOneRuntimeKey()
        {
            ESSuperAttributeTable table = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();

            Assert.That(table.TryBuildCatalog(out ESSuperAttributeCatalog catalog, out string error), Is.True, error);
            Assert.That(catalog.TryGetRuntimeKey(
                (ushort)ESCharacterAttributeEnumKey.GroundMaxMoveSpeed,
                ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed,
                out int runtimeKey), Is.True);
            Assert.That(catalog.TryGetRuntimeKey(
                0,
                ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed,
                out int stringRuntimeKey), Is.True);
            Assert.That(runtimeKey, Is.EqualTo(stringRuntimeKey));
            Assert.That(catalog.TryGetFloatDefinition(runtimeKey, out ESSuperFloatAttributeDefinition definition), Is.True);
            Assert.That(definition.storagePolicy, Is.EqualTo(ESKeyStoragePolicy.HotSlot));
            Assert.That(catalog.SchemaHash, Is.Not.Empty);
        }

        [Test]
        public void SuperAttributeCatalog_RejectsDuplicateStableAliases()
        {
            ESSuperAttributeTable table = new ESSuperAttributeTable
            {
                floatAttributes = new List<ESSuperFloatAttributeDefinition>
                {
                    new ESSuperFloatAttributeDefinition { enumKey = 42, key = "Equipment.Power" },
                    new ESSuperFloatAttributeDefinition { enumKey = 42, key = "Equipment.OtherPower" }
                }
            };

            Assert.That(table.TryBuildCatalog(out _, out string error), Is.False);
            Assert.That(error, Does.Contain("EnumKey"));
        }

        [Test]
        public void InputActionCatalog_SeparatesZeroBasedHotSlotFromStableIdentity()
        {
            List<ESInputActionDefine> actions = new List<ESInputActionDefine>
            {
                new ESInputActionDefine
                {
                    id = ESInputActionId.Move,
                    actionName = "Move",
                    valueType = ESInputValueType.Vector2
                },
                new ESInputActionDefine
                {
                    id = ESInputActionId.Dynamic,
                    actionName = "Project.Mod.CustomAction",
                    valueType = ESInputValueType.Button
                }
            };

            Assert.That(ESInputActionCatalog.TryCreate(actions, out ESInputActionCatalog catalog, out string error), Is.True, error);
            Assert.That(catalog.TryGetRuntimeKey(ESInputActionId.Move, "Move", out int moveRuntimeKey), Is.True);
            Assert.That(moveRuntimeKey, Is.GreaterThan(0));
            Assert.That(catalog.TryGetRuntimeKey(ESInputActionId.Dynamic, "Project.Mod.CustomAction", out int dynamicRuntimeKey), Is.True);
            Assert.That(dynamicRuntimeKey, Is.Not.EqualTo(moveRuntimeKey));
        }

        [Test]
        public void InputActionCatalog_RejectsDuplicateBindingId()
        {
            ESInputBindingDefine firstBinding = ESInputBindingDefine.InputSystem("KeyboardMouse", "<Keyboard>/space", bindingId: "Jump.Primary");
            ESInputBindingDefine secondBinding = ESInputBindingDefine.InputSystem("KeyboardMouse", "<Keyboard>/j", bindingId: "Jump.Primary");
            List<ESInputActionDefine> actions = new List<ESInputActionDefine>
            {
                new ESInputActionDefine
                {
                    id = ESInputActionId.Jump,
                    actionName = "Jump",
                    valueType = ESInputValueType.Button,
                    bindings = new List<ESInputBindingDefine> { firstBinding, secondBinding }
                }
            };

            Assert.That(ESInputActionCatalog.TryCreate(actions, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("bindingId"));
        }

        [Test]
        public void InputSchemeCatalog_BindsBuiltInAliasesAndRejectsMismatches()
        {
            List<ESInputSchemeDefine> schemes = new List<ESInputSchemeDefine>
            {
                new ESInputSchemeDefine
                {
                    enumKey = ESInputSchemeEnumKey.KeyboardMouse,
                    schemeId = ESInputSchemeIds.KeyboardMouse,
                    bindingGroup = ESInputSchemeIds.KeyboardMouse
                },
                new ESInputSchemeDefine
                {
                    enumKey = ESInputSchemeEnumKey.None,
                    schemeId = "Project.Platform.CloudInput",
                    bindingGroup = "CloudInput",
                    deviceKind = ESInputDeviceKind.Custom
                }
            };

            Assert.That(ESInputSchemeCatalog.TryCreate(schemes, out ESInputSchemeCatalog catalog, out string error), Is.True, error);
            Assert.That(catalog.TryGetRuntimeKey(ESInputSchemeIds.KeyboardMouse, out int keyboardRuntimeKey), Is.True);
            Assert.That(catalog.TryGetRuntimeKey("Project.Platform.CloudInput", out int customRuntimeKey), Is.True);
            Assert.That(customRuntimeKey, Is.Not.EqualTo(keyboardRuntimeKey));

            schemes[0].schemeId = ESInputSchemeIds.Gamepad;
            Assert.That(ESInputSchemeCatalog.TryCreate(schemes, out _, out error), Is.False);
            Assert.That(error, Does.Contain("aliases disagree"));
        }

        [Test]
        public void InputBindingProfile_PersistsOnlyStableConfigAndSchemaIdentity()
        {
            ESInputConfig config = ScriptableObject.CreateInstance<ESInputConfig>();
            try
            {
                config.configId = "Input.Test.Default";
                config.ApplyDefaultGameplayConfig();

                ESInputBindingProfile legacyProfile = ESInputUtility.CreateDefaultProfile();
                Assert.That(config.TryPrepareBindingProfile(legacyProfile, out string error), Is.True, error);
                Assert.That(legacyProfile.sourceConfigId, Is.EqualTo(config.configId));
                Assert.That(legacyProfile.sourceSchemeSchemaHash, Is.Not.Empty);
                Assert.That(legacyProfile.sourceActionSchemaHash, Is.Not.Empty);

                ESInputBindingProfile foreignProfile = ESInputUtility.CreateDefaultProfile();
                foreignProfile.BindToInputSchema("Input.Other", "0000000000000000", "0000000000000000");
                Assert.That(config.TryPrepareBindingProfile(foreignProfile, out error), Is.False);
                Assert.That(error, Does.Contain("different config or schema"));

                ESInputBindingProfile invalidLegacyProfile = ESInputUtility.CreateDefaultProfile();
                invalidLegacyProfile.activeSchemeId = "Project.UnknownScheme";
                Assert.That(config.TryPrepareBindingProfile(invalidLegacyProfile, out error), Is.False);
                Assert.That(error, Does.Contain("undeclared active scheme"));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void StateNumericParameters_UseDenseRuntimeKeysInsteadOfStableEnumValues()
        {
            Assert.That(StateDefaultNumericParameterCatalog.TryGetIndex(StateDefaultIntParameter.MovementMode, out int movementRuntimeKey), Is.True);
            Assert.That(StateDefaultNumericParameterCatalog.TryGetIndex(StateDefaultIntParameter.WeaponSlot, out int weaponRuntimeKey), Is.True);
            Assert.That(movementRuntimeKey, Is.EqualTo(1));
            Assert.That(weaponRuntimeKey, Is.EqualTo(StateDefaultNumericParameterCatalog.IntRuntimeKeyCount));
            Assert.That(weaponRuntimeKey, Is.LessThan((int)StateDefaultIntParameter.WeaponSlot));

            Assert.That(StateDefaultNumericParameterCatalog.TryGetIndex(StateDefaultBoolParameter.IsAiming, out int aimingRuntimeKey), Is.True);
            Assert.That(aimingRuntimeKey, Is.EqualTo(1));
            Assert.That(StateDefaultNumericParameterCatalog.BoolRuntimeKeyCount, Is.EqualTo(5));
        }

        [Test]
        public void AttributeSchemaHash_IsCultureInvariant()
        {
            ESSuperAttributeTable table = new ESSuperAttributeTable
            {
                floatAttributes = new List<ESSuperFloatAttributeDefinition>
                {
                    new ESSuperFloatAttributeDefinition
                    {
                        enumKey = 7,
                        key = "Vehicle.Engine.Torque",
                        baseValue = 12.5f,
                        minValue = 0.25f,
                        maxValue = 1234.75f
                    }
                }
            };

            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                Assert.That(table.TryBuildCatalog(out ESSuperAttributeCatalog frenchCatalog, out string frenchError), Is.True, frenchError);
                string frenchHash = frenchCatalog.SchemaHash;

                table.InvalidateCache();
                CultureInfo.CurrentCulture = new CultureInfo("en-US");
                Assert.That(table.TryBuildCatalog(out ESSuperAttributeCatalog englishCatalog, out string englishError), Is.True, englishError);
                Assert.That(englishCatalog.SchemaHash, Is.EqualTo(frenchHash));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }
    }
}
