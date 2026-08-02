using System;
using KinematicCharacterController;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    /// <summary>
    /// Entity KCC 与 VehicleController 使用同一调度契约：调度内注销必须跳过后续任务，
    /// 单个能力异常不得中断剩余能力，且 ref 输出不得留下半成品。
    /// </summary>
    public sealed class EntityKCCSafetyTests
    {
        private GameObject characterObject;
        private EntityKCCData kcc;

        [SetUp]
        public void SetUp()
        {
            characterObject = new GameObject("EntityKCCSafetyTests");
            characterObject.AddComponent<CapsuleCollider>();
            KinematicCharacterMotor motor = characterObject.AddComponent<KinematicCharacterMotor>();
            kcc = new EntityKCCData
            {
                motor = motor,
                useRootMotion = false,
                preventUpwardDriftWhenIdle = false,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (characterObject != null)
                UnityEngine.Object.DestroyImmediate(characterObject);

            characterObject = null;
            kcc = null;
        }

        [Test]
        public void Scheduler_UnregisteringNextFeature_SkipsItInAllKccPhases()
        {
            var beforeTarget = new RegistrationHolder();
            var rotationTarget = new RegistrationHolder();
            var velocityTarget = new RegistrationHolder();
            bool beforeRan = false;
            bool rotationRan = false;
            bool velocityRan = false;

            beforeTarget.registration = kcc.RegisterMotionFeature(
                new BeforeFeature((_, _, _, _) =>
                {
                    beforeRan = true;
                    return false;
                }),
                new EntityKCCMotionOrder(20, 0, 0));
            kcc.RegisterMotionFeature(
                new BeforeFeature((_, data, _, _) =>
                {
                    data.UnregisterMotionFeature(ref beforeTarget.registration);
                    return false;
                }),
                new EntityKCCMotionOrder(10, 0, 0));

            rotationTarget.registration = kcc.RegisterMotionFeature(
                new RotationFeature((Entity _, EntityKCCData __, Quaternion ___, ref Quaternion ____, float _____) =>
                {
                    rotationRan = true;
                    return false;
                }),
                new EntityKCCMotionOrder(0, 20, 0));
            kcc.RegisterMotionFeature(
                new RotationFeature((Entity _, EntityKCCData data, Quaternion __, ref Quaternion ___, float ____) =>
                {
                    data.UnregisterMotionFeature(ref rotationTarget.registration);
                    return false;
                }),
                new EntityKCCMotionOrder(0, 10, 0));

            velocityTarget.registration = kcc.RegisterMotionFeature(
                new VelocityFeature((Entity _, EntityKCCData __, Vector3 ___, ref Vector3 ____, float _____) =>
                {
                    velocityRan = true;
                    return false;
                }),
                new EntityKCCMotionOrder(0, 0, 20));
            kcc.RegisterMotionFeature(
                new VelocityFeature((Entity _, EntityKCCData data, Vector3 __, ref Vector3 ___, float ____) =>
                {
                    data.UnregisterMotionFeature(ref velocityTarget.registration);
                    return false;
                }),
                new EntityKCCMotionOrder(0, 0, 10));

            kcc.BeforeCharacterUpdate(null, 0.02f);
            Quaternion rotation = Quaternion.identity;
            kcc.UpdateRotation(null, ref rotation, 0.02f);
            Vector3 velocity = Vector3.zero;
            kcc.UpdateVelocity(null, ref velocity, 0.02f);

            Assert.That(beforeRan, Is.False);
            Assert.That(rotationRan, Is.False);
            Assert.That(velocityRan, Is.False);
        }

        [Test]
        public void Scheduler_ThrowingFeature_DoesNotBlockLaterFeature_AndRestoresRefOutputs()
        {
            bool beforeRan = false;
            bool rotationRan = false;
            bool velocityRan = false;
            kcc.RegisterMotionFeature(
                new BeforeFeature((_, _, _, _) => throw new InvalidOperationException("Expected test exception.")),
                new EntityKCCMotionOrder(10, 0, 0));
            kcc.RegisterMotionFeature(
                new BeforeFeature((_, _, _, _) =>
                {
                    beforeRan = true;
                    return false;
                }),
                new EntityKCCMotionOrder(20, 0, 0));

            kcc.RegisterMotionFeature(
                new RotationFeature((Entity _, EntityKCCData __, Quaternion ___, ref Quaternion current, float ____) =>
                {
                    current = Quaternion.Euler(0f, 90f, 0f);
                    throw new InvalidOperationException("Expected test exception.");
                }),
                new EntityKCCMotionOrder(0, 10, 0));
            kcc.RegisterMotionFeature(
                new RotationFeature((Entity _, EntityKCCData __, Quaternion ___, ref Quaternion ____, float _____) =>
                {
                    rotationRan = true;
                    return true;
                }),
                new EntityKCCMotionOrder(0, 20, 0));

            kcc.RegisterMotionFeature(
                new VelocityFeature((Entity _, EntityKCCData __, Vector3 ___, ref Vector3 current, float ____) =>
                {
                    current = Vector3.one;
                    throw new InvalidOperationException("Expected test exception.");
                }),
                new EntityKCCMotionOrder(0, 0, 10));
            kcc.RegisterMotionFeature(
                new VelocityFeature((Entity _, EntityKCCData __, Vector3 ___, ref Vector3 ____, float _____) =>
                {
                    velocityRan = true;
                    return true;
                }),
                new EntityKCCMotionOrder(0, 0, 20));

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                kcc.BeforeCharacterUpdate(null, 0.02f);
                Quaternion rotation = Quaternion.identity;
                kcc.UpdateRotation(null, ref rotation, 0.02f);
                Vector3 velocity = Vector3.zero;
                kcc.UpdateVelocity(null, ref velocity, 0.02f);

                Assert.That(rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(velocity, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }

            Assert.That(beforeRan, Is.True);
            Assert.That(rotationRan, Is.True);
            Assert.That(velocityRan, Is.True);
        }

        private sealed class RegistrationHolder
        {
            public EntityKCCMotionRegistration registration;
        }

        private delegate bool BeforeAction(Entity owner, EntityKCCData data, Vector3 initialPosition, float deltaTime);
        private delegate bool RotationAction(Entity owner, EntityKCCData data, Quaternion initialRotation, ref Quaternion currentRotation, float deltaTime);
        private delegate bool VelocityAction(Entity owner, EntityKCCData data, Vector3 initialVelocity, ref Vector3 currentVelocity, float deltaTime);

        private sealed class BeforeFeature : IEntityKCCBeforeMotion
        {
            private readonly BeforeAction action;

            public BeforeFeature(BeforeAction action)
            {
                this.action = action;
            }

            public bool BeforeCharacterUpdate(Entity owner, EntityKCCData data, Vector3 initialPosition, float deltaTime)
            {
                return action(owner, data, initialPosition, deltaTime);
            }
        }

        private sealed class RotationFeature : IEntityKCCRotationMotion
        {
            private readonly RotationAction action;

            public RotationFeature(RotationAction action)
            {
                this.action = action;
            }

            public bool UpdateRotation(Entity owner, EntityKCCData data, Quaternion initialRotation, ref Quaternion currentRotation, float deltaTime)
            {
                return action(owner, data, initialRotation, ref currentRotation, deltaTime);
            }
        }

        private sealed class VelocityFeature : IEntityKCCVelocityMotion
        {
            private readonly VelocityAction action;

            public VelocityFeature(VelocityAction action)
            {
                this.action = action;
            }

            public bool UpdateVelocity(Entity owner, EntityKCCData data, Vector3 initialVelocity, ref Vector3 currentVelocity, float deltaTime)
            {
                return action(owner, data, initialVelocity, ref currentVelocity, deltaTime);
            }
        }
    }
}
