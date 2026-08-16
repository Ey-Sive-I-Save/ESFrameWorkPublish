using NUnit.Framework;

namespace ES.Tests
{
    public sealed class EntityPrimaryAttackSelectorTests
    {
        [Test]
        public void Select_NullDefinition_ReturnsNone()
        {
            EntityPrimaryAttackSelection selection = EntityPrimaryAttackSelector.Select(
                null,
                "melee.attack");

            Assert.That(selection.IsValid, Is.False);
            Assert.That(selection.route, Is.EqualTo(EntityPrimaryAttackRoute.None));
            Assert.That(selection.source, Is.EqualTo(EntityPrimaryAttackSource.None));
        }

        [Test]
        public void Select_MeleeWithAction_UsesActionRoute()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = ItemWeaponKind.Melee;
            definition.deliveryMode = WeaponAttackDeliveryMode.Action;
            definition.fire.enabled = true;

            EntityPrimaryAttackSelection selection = EntityPrimaryAttackSelector.Select(
                definition,
                "melee.attack");

            Assert.That(selection.route, Is.EqualTo(EntityPrimaryAttackRoute.Action));
            Assert.That(selection.source, Is.EqualTo(EntityPrimaryAttackSource.PrimaryWeapon));
        }

        [Test]
        public void Select_MeleeWithoutAction_ReturnsNone()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = ItemWeaponKind.Melee;
            definition.deliveryMode = WeaponAttackDeliveryMode.Action;

            EntityPrimaryAttackSelection selection = EntityPrimaryAttackSelector.Select(
                definition,
                new ESActionConfigKey());

            Assert.That(selection.IsValid, Is.False);
        }

        [Test]
        public void Select_HitScanDelivery_UsesHitScanRoute()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = ItemWeaponKind.Ranged;
            definition.deliveryMode = WeaponAttackDeliveryMode.HitScan;
            definition.fire.enabled = true;

            EntityPrimaryAttackSelection selection = EntityPrimaryAttackSelector.Select(
                definition,
                null,
                EntityPrimaryAttackSource.SecondaryWeapon);

            Assert.That(selection.route, Is.EqualTo(EntityPrimaryAttackRoute.HitScan));
            Assert.That(selection.source, Is.EqualTo(EntityPrimaryAttackSource.SecondaryWeapon));
        }

        [Test]
        public void Select_DeliveryWithFireDisabled_ReturnsNone()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = ItemWeaponKind.Throwable;
            definition.deliveryMode = WeaponAttackDeliveryMode.Shot;
            definition.fire.enabled = false;
            definition.defaultShot = "tests.shot.throw";

            Assert.That(
                EntityPrimaryAttackSelector.Select(definition, "throw.attack").IsValid,
                Is.False);
        }

        [TestCase(ItemWeaponKind.Melee)]
        [TestCase(ItemWeaponKind.Ranged)]
        [TestCase(ItemWeaponKind.Throwable)]
        [TestCase(ItemWeaponKind.Magic)]
        public void Select_WeaponKindDoesNotChangeConfiguredDelivery(ItemWeaponKind weaponKind)
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = weaponKind;
            definition.deliveryMode = WeaponAttackDeliveryMode.Beam;
            definition.firePolicy = WeaponFirePolicy.Continuous;
            definition.fire.enabled = true;

            Assert.That(
                EntityPrimaryAttackSelector.Select(definition, "custom.attack").route,
                Is.EqualTo(EntityPrimaryAttackRoute.Beam));
        }

        [Test]
        public void Select_ShotDelivery_RequiresConfiguredShotKey()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.deliveryMode = WeaponAttackDeliveryMode.Shot;
            definition.fire.enabled = true;

            Assert.That(EntityPrimaryAttackSelector.Select(definition, null).IsValid, Is.False);

            definition.defaultShot = "tests.shot.projectile";
            Assert.That(
                EntityPrimaryAttackSelector.Select(definition, null).route,
                Is.EqualTo(EntityPrimaryAttackRoute.Shot));
        }

        [Test]
        public void SelectUnarmed_WithAction_UsesUnarmedAction()
        {
            EntityPrimaryAttackSelection selection =
                EntityPrimaryAttackSelector.SelectUnarmed("unarmed.attack");

            Assert.That(selection.route, Is.EqualTo(EntityPrimaryAttackRoute.Action));
            Assert.That(selection.source, Is.EqualTo(EntityPrimaryAttackSource.Unarmed));
        }

        [Test]
        public void SelectUnarmed_WithoutAction_ReturnsNone()
        {
            Assert.That(
                EntityPrimaryAttackSelector.SelectUnarmed(new ESActionConfigKey()).IsValid,
                Is.False);
        }

        [Test]
        public void SelectPairedWeapons_RequiresExplicitPairedAction()
        {
            EntityPrimaryAttackSelection valid =
                EntityPrimaryAttackSelector.SelectPairedWeapons("dual_wield.attack");
            EntityPrimaryAttackSelection invalid =
                EntityPrimaryAttackSelector.SelectPairedWeapons(new ESActionConfigKey());

            Assert.That(valid.route, Is.EqualTo(EntityPrimaryAttackRoute.Action));
            Assert.That(valid.source, Is.EqualTo(EntityPrimaryAttackSource.PairedWeapons));
            Assert.That(invalid.IsValid, Is.False);
        }
    }
}
