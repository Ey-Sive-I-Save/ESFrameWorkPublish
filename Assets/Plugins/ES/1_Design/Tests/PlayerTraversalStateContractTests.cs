using NUnit.Framework;

namespace ES.Tests
{
    public sealed class PlayerTraversalStateContractTests
    {
        [Test]
        public void ClimbingStateContract_RequiresClimbingEnvironmentAndSwitchPolicy()
        {
            var config = new StateBasicConfig
            {
                stateSupportFlag = StateSupportFlags.Climbing,
                resetSupportFlagOnEnter = true,
                deactivateOnSupportFlagSwitching = true,
            };

            Assert.That(EntityBasicClimbModule.ValidateClimbingStateConfig(config, out string error), Is.True, error);

            config.stateSupportFlag = StateSupportFlags.Grounded;
            Assert.That(EntityBasicClimbModule.ValidateClimbingStateConfig(config, out error), Is.False);
            Assert.That(error, Does.Contain("Climbing"));

            config.stateSupportFlag = StateSupportFlags.Climbing;
            config.resetSupportFlagOnEnter = false;
            Assert.That(EntityBasicClimbModule.ValidateClimbingStateConfig(config, out error), Is.False);
            Assert.That(error, Does.Contain("resetSupportFlagOnEnter"));
        }

        [Test]
        public void ClimbJumpStateContract_RequiresGroundedEnvironment()
        {
            var config = new StateBasicConfig
            {
                stateSupportFlag = StateSupportFlags.Grounded,
                resetSupportFlagOnEnter = true,
                deactivateOnSupportFlagSwitching = true,
            };

            Assert.That(EntityBasicClimbModule.ValidateClimbJumpStateConfig(config, out string error), Is.True, error);

            config.stateSupportFlag = StateSupportFlags.Climbing;
            Assert.That(EntityBasicClimbModule.ValidateClimbJumpStateConfig(config, out error), Is.False);
            Assert.That(error, Does.Contain("Grounded"));
        }
    }
}
