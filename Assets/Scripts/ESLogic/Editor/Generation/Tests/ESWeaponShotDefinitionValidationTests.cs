using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESWeaponShotDefinitionValidationTests
    {
        [Test]
        public void WeaponDefinition_RejectsUnknownWeaponKind()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = (ItemWeaponKind)byte.MaxValue;

            Assert.That(definition.ValidateDefinition(out string error), Is.False);
            StringAssert.Contains("武器类型", error);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void WeaponDefinition_RejectsNonFiniteFireInterval(float value)
        {
            ItemWeaponSharedData definition = CreateValidRangedWeapon();
            definition.fire.interval = value;

            Assert.That(definition.ValidateDefinition(out string error), Is.False);
            StringAssert.Contains("有限", error);
        }

        [Test]
        public void WeaponDefinition_RejectsNonFiniteInitialState()
        {
            ItemWeaponSharedData definition = CreateValidRangedWeapon();
            ItemWeaponVariableData state = ItemWeaponVariableData.Default;
            state.heat = float.NaN;

            Assert.That(definition.ValidateInitialState(state, out string error), Is.False);
            StringAssert.Contains("有限", error);
        }

        [Test]
        public void WeaponDefinition_RejectsEmptyFireHitMask()
        {
            ItemWeaponSharedData definition = CreateValidRangedWeapon();
            definition.fire.hitMask = 0;

            Assert.That(definition.ValidateDefinition(out string error), Is.False);
            StringAssert.Contains("命中层", error);
        }

        [Test]
        public void WeaponDefinition_RejectsUnknownTriggerInteraction()
        {
            ItemWeaponSharedData definition = CreateValidRangedWeapon();
            definition.fire.triggerInteraction = (QueryTriggerInteraction)byte.MaxValue;

            Assert.That(definition.ValidateDefinition(out string error), Is.False);
            StringAssert.Contains("触发器", error);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ShotDefinition_RejectsNonFiniteMotionValues(float value)
        {
            ItemShotSharedData definition = ItemShotSharedData.Default;
            definition.speed = value;

            Assert.That(definition.ValidateDefinition(out string error), Is.False);
            StringAssert.Contains("有限", error);
        }

        [Test]
        public void ShotDefinition_RejectsUnresolvableHitTagEligibility()
        {
            ItemShotSharedData definition = ItemShotSharedData.Default;
            definition.hitTagEligibility.attackerCondition.required.Add(default);

            Assert.That(definition.ValidateDefinition(out string error), Is.False);
            StringAssert.Contains("Tag", error);
            Assert.That(definition.hitTagEligibility.IsPrepared, Is.False);
        }

        [Test]
        public void ShotDefinition_PreparesEmptyHitTagEligibility()
        {
            ItemShotSharedData definition = ItemShotSharedData.Default;

            Assert.That(definition.ValidateDefinition(out string error), Is.True, error);
            Assert.That(definition.hitTagEligibility.IsPrepared, Is.True);
        }

        [Test]
        public void ItemDefinition_RejectsStringOnlyShotPrefabIdentity()
        {
            ItemDataInfo info = ScriptableObject.CreateInstance<ItemDataInfo>();
            try
            {
                info.itemKey = new ESItemConfigKey { stringKey = "tests.item.shot.string-only-prefab" };
                info.baseConfig = new ItemBaseConfig
                {
                    kind = ItemKind.Shot,
                    prefabKey = new ESAssetReferPrefabConfigKey
                    {
                        stringKey = "tests.prefab.shot.string-only"
                    }
                };
                info.interactConfig = new ItemInteractConfig();
                info.logicConfig = new ItemLogicConfig();
                info.kindData = new ItemShotDataBlock
                {
                    key = new ESShotConfigKey { stringKey = "tests.shot.string-only-prefab" },
                    sharedData = ItemShotSharedData.Default,
                    initialState = ItemShotVariableData.Default
                };

                Assert.That(
                    info.ValidateConfiguration(includeEditorMetadata: false),
                    Is.EqualTo(ESItemDataValidationCode.MissingShotPrefab));
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        [Test]
        public void ShotModule_TagPrepareFailure_DoesNotPartiallyApplyDefinition()
        {
            var module = new ItemShotModule();
            ItemShotSharedData previousDefinition = ItemShotSharedData.Default;
            module.sharedData = previousDefinition;
            module.variableData = ItemShotVariableData.Default;
            module.aimMode = ShotAimMode.MustHit;
            module.blockMode = ShotBlockMode.None;
            module.hitLayers = 1 << 7;
            module.castRadius = 0.75f;
            ShotMotionConfig previousConfig = module.config;

            ItemShotSharedData invalidDefinition = ItemShotSharedData.Default;
            invalidDefinition.aimMode = ShotAimMode.Scan;
            invalidDefinition.radius = 3f;
            invalidDefinition.hitTagEligibility.attackerCondition.required.Add(default);
            ItemShotVariableData invalidVariable = ItemShotVariableData.Default;
            invalidVariable.logicSeed = 99;

            Assert.Throws<System.InvalidOperationException>(
                () => module.ApplyShotData(invalidDefinition, invalidVariable));

            Assert.That(module.sharedData, Is.SameAs(previousDefinition));
            Assert.That(module.variableData.logicSeed, Is.Zero);
            Assert.That(module.aimMode, Is.EqualTo(ShotAimMode.MustHit));
            Assert.That(module.blockMode, Is.EqualTo(ShotBlockMode.None));
            Assert.That(module.hitLayers.value, Is.EqualTo(1 << 7));
            Assert.That(module.castRadius, Is.EqualTo(0.75f));
            Assert.That(module.config.speed, Is.EqualTo(previousConfig.speed));
            Assert.That(module.config.maxLifetime, Is.EqualTo(previousConfig.maxLifetime));
        }

        [Test]
        public void ShotModule_InvalidNumericDefinition_DoesNotPartiallyApplyDefinition()
        {
            var module = new ItemShotModule();
            ItemShotSharedData previousDefinition = ItemShotSharedData.Default;
            module.sharedData = previousDefinition;
            module.aimMode = ShotAimMode.MustHit;
            module.castRadius = 0.5f;

            ItemShotSharedData invalidDefinition = ItemShotSharedData.Default;
            invalidDefinition.speed = float.NaN;

            Assert.Throws<System.InvalidOperationException>(
                () => module.ApplyShotData(invalidDefinition, ItemShotVariableData.Default));

            Assert.That(module.sharedData, Is.SameAs(previousDefinition));
            Assert.That(module.aimMode, Is.EqualTo(ShotAimMode.MustHit));
            Assert.That(module.castRadius, Is.EqualTo(0.5f));
        }

        [Test]
        public void LongBarPrefab_RejectsAdditionalItemInChildHierarchy()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                ESLongBarMeleeWeaponBuilder.WeaponPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            try
            {
                GameObject child = new GameObject("Unexpected Item");
                child.transform.SetParent(instance.transform, false);
                child.AddComponent<Item>();

                System.InvalidOperationException exception = Assert.Throws<System.InvalidOperationException>(
                    () => ESLongBarMeleeWeaponBuilder.ValidateLongBarPrefabForAuthoring(instance));
                StringAssert.Contains("Item", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ItemDefinition_RejectsForcedMustHitWhenSharedDefinitionDisallowsIt()
        {
            ItemDataInfo info = ScriptableObject.CreateInstance<ItemDataInfo>();
            try
            {
                info.SetKey("tests.item.shot.must_hit_contract");
                info.itemKey = new ESItemConfigKey { stringKey = "tests.item.shot.must_hit_contract" };
                info.baseConfig = new ItemBaseConfig
                {
                    kind = ItemKind.Shot,
                    prefabKey = new ESAssetReferPrefabConfigKey
                    {
                        stringKey = "tests.prefab.shot.must_hit_contract"
                    },
                    iconKey = new ESAssetReferSpriteConfigKey()
                };
                info.interactConfig = new ItemInteractConfig();
                info.logicConfig = new ItemLogicConfig();
                info.moveConfig = new ItemMoveConfig();

                ItemShotSharedData shared = ItemShotSharedData.Default;
                shared.allowMustHit = false;
                ItemShotVariableData initialState = ItemShotVariableData.Default;
                initialState.forceMustHit = true;
                info.kindData = new ItemShotDataBlock
                {
                    key = new ESShotConfigKey { stringKey = "tests.shot.must_hit_contract" },
                    sharedData = shared,
                    initialState = initialState
                };

                Assert.That(
                    info.ValidateConfiguration(),
                    Is.EqualTo(ESItemDataValidationCode.InvalidShotConfig));
            }
            finally
            {
                Object.DestroyImmediate(info);
            }
        }

        private static ItemWeaponSharedData CreateValidRangedWeapon()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = ItemWeaponKind.Ranged;
            definition.deliveryMode = WeaponAttackDeliveryMode.Shot;
            definition.firePolicy = WeaponFirePolicy.Automatic;
            definition.defaultShot = new ESShotConfigKey { stringKey = "tests.shot.valid" };
            definition.fire.enabled = true;
            return definition;
        }
    }
}
