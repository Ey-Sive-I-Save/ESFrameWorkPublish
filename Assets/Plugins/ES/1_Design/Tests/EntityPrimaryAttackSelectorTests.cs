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

            EntityPrimaryAttackSelection selection = EntityPrimaryAttackSelector.Select(
                definition,
                new ESActionConfigKey());

            Assert.That(selection.IsValid, Is.False);
        }

        [Test]
        public void Select_RangedWithFireEnabled_UsesWeaponFire()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = ItemWeaponKind.Ranged;
            definition.fire.enabled = true;

            EntityPrimaryAttackSelection selection = EntityPrimaryAttackSelector.Select(
                definition,
                null,
                EntityPrimaryAttackSource.SecondaryWeapon);

            Assert.That(selection.route, Is.EqualTo(EntityPrimaryAttackRoute.WeaponFire));
            Assert.That(selection.source, Is.EqualTo(EntityPrimaryAttackSource.SecondaryWeapon));
        }

        [Test]
        public void Select_ThrowableWithFireDisabled_ReturnsNone()
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = ItemWeaponKind.Throwable;
            definition.fire.enabled = false;

            Assert.That(
                EntityPrimaryAttackSelector.Select(definition, "throw.attack").IsValid,
                Is.False);
        }

        [TestCase(ItemWeaponKind.Throwable)]
        [TestCase(ItemWeaponKind.Magic)]
        [TestCase(ItemWeaponKind.None)]
        public void Select_NonRangedDefinitionWithFireEnabled_DoesNotFallIntoHitscan(ItemWeaponKind weaponKind)
        {
            ItemWeaponSharedData definition = ItemWeaponSharedData.Default;
            definition.weaponKind = weaponKind;
            definition.fire.enabled = true;

            Assert.That(
                EntityPrimaryAttackSelector.Select(definition, "custom.attack").IsValid,
                Is.False);
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
