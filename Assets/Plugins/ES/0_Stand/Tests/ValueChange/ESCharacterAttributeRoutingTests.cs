using System.Collections.Generic;
using System.Globalization;
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
                    ESCharacterSuperAttributeKeys.CanJump,
                    out ESCharacterPermitAttributeId jumpId),
                Is.True);
            Assert.That(jumpId, Is.EqualTo(ESCharacterPermitAttributeId.Jump));
            Assert.That(ESCharacterAttributeCatalog.TryGetFloatId(ESCharacterSuperAttributeKeys.CanJump, out _), Is.False);
            Assert.That(ESCharacterAttributeCatalog.TryGetPermitId(ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, out _), Is.False);
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
            Assert.That(entity.GetFloatStat(ESCharacterSuperAttributeKeys.CanJump), Is.Null);
        }

        [Test]
        public void FixedPermitSlot_UsesSameResolverAndDoesNotCreateOnRead()
        {
            Entity entity = CreateEntity();
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, false), Is.False);
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.CanJump, out _), Is.False);

            ESPermitSet set = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, false);
            set.Add(ESPermitLaw.HardEnable);
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, false), Is.True);
            Assert.That(entity.GetPermitResult(ESCharacterSuperAttributeKeys.CanJump, false).decision, Is.EqualTo(ESPermitLaw.HardEnable));
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
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.CanJump, out _), Is.False);
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
            Assert.That(entity.TryGetPermit(ESCharacterSuperAttributeKeys.CanJump, out _), Is.False);
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
            ESEffectLease lease = entity.CreateValueChangeEffectLease(out int ownerId);
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            speed.Add(ESFloatValueChangeOp.Add, 3f, ownerId: ownerId);
            jump.Add(ESPermitLaw.HardDisable, ownerId: ownerId);

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
            ESEffectLease lease = entity.CreateValueChangeEffectLease(out int ownerId);
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            speed.Add(ESFloatValueChangeOp.Add, 3f, ownerId: ownerId);

            Object.DestroyImmediate(entity.gameObject);

            Assert.That(lease.TryRelease(), Is.False);
        }

        [Test]
        public void ReleaseEffect_CleansEverySetAndOwnerIdCanBeReused_WhenChangedThrows()
        {
            Entity entity = CreateEntity();
            ESEffectLease lease = entity.CreateValueChangeEffectLease(out int ownerId);
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            speed.Add(ESFloatValueChangeOp.Add, 3f, ownerId: ownerId);
            jump.Add(ESPermitLaw.HardDisable, ownerId: ownerId);

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

            ESEffectLease nextLease = entity.CreateValueChangeEffectLease(out int nextOwnerId);
            Assert.That(nextOwnerId, Is.EqualTo(ownerId));
            speed.Add(ESFloatValueChangeOp.Add, 1f, ownerId: nextOwnerId);
            jump.Add(ESPermitLaw.HardDisable, ownerId: nextOwnerId);

            Assert.That(entity.GetCharacterFloatStatValue(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f), Is.EqualTo(6f));
            Assert.That(entity.GetCharacterPermitValue(ESCharacterPermitAttributeId.Jump, true), Is.False);
            nextLease.Dispose();
        }

        [Test]
        public void EntityDestroy_InvalidatesLeasesAndClearsEverySet_WhenChangedThrows()
        {
            Entity entity = CreateEntity();
            ESEffectLease lease = entity.CreateValueChangeEffectLease(out int ownerId);
            ESFloatValueChangeSet speed = entity.GetCharacterFloatStat(ESCharacterFloatAttributeId.GroundMaxMoveSpeed, 5f);
            ESPermitSet jump = entity.GetCharacterPermit(ESCharacterPermitAttributeId.Jump, true);
            speed.Add(ESFloatValueChangeOp.Add, 3f, ownerId: ownerId);
            jump.Add(ESPermitLaw.HardDisable, ownerId: ownerId);

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
                    entity.CreateValueChangeEffectLease(out int ignoredOwnerId);
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
