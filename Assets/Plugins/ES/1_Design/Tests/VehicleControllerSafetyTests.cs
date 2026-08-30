using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ES.Tests
{
    public sealed class VehicleControllerSafetyTests
    {
        private static readonly MethodInfo DispatchBeforeMotionMethod = typeof(VehicleController).GetMethod(
            "DispatchBeforeMotion",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DispatchRotationMethod = typeof(VehicleController).GetMethod(
            "DispatchRotation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DispatchVelocityMethod = typeof(VehicleController).GetMethod(
            "DispatchVelocity",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo DispatchAfterMotionMethod = typeof(VehicleController).GetMethod(
            "DispatchAfterMotion",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ClearExpiredDriverInputMethod = typeof(VehicleController).GetMethod(
            "ClearExpiredDriverInput",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnDestroyMethod = typeof(VehicleController).GetMethod(
            "OnDestroy",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo InputStateField = typeof(VehicleController).GetField(
            "inputState",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CurrentDriverSeatField = typeof(VehicleController).GetField(
            "currentDriverSeat",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject vehicleObject;
        private VehicleController controller;

        [SetUp]
        public void SetUp()
        {
            vehicleObject = new GameObject("VehicleControllerSafetyTests");
            vehicleObject.AddComponent<Rigidbody>();
            controller = vehicleObject.AddComponent<VehicleController>();
            Assert.That(controller.Initialize(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            if (vehicleObject != null)
                UnityEngine.Object.DestroyImmediate(vehicleObject);

            vehicleObject = null;
            controller = null;
        }

        [Test]
        public void Controller_ClearsExpiredDriverInput()
        {
            var input = new VehicleInputState();
            input.Set(Vector3.forward, Vector3.forward, 0f);
            input.frameIndex = Time.frameCount - VehicleInputState.MaxInputAgeFrames - 1;

            Assert.That(InputStateField, Is.Not.Null);
            Assert.That(ClearExpiredDriverInputMethod, Is.Not.Null);
            InputStateField.SetValue(controller, input);
            ClearExpiredDriverInputMethod.Invoke(controller, null);

            Assert.That(controller.InputState.frameIndex, Is.EqualTo(-1));
            Assert.That(controller.InputState.moveWorld, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Controller_OnDestroy_ClearsDriverInputAndIsIdempotent()
        {
            var input = new VehicleInputState();
            input.Set(Vector3.forward, Vector3.right, 1f);
            Assert.That(OnDestroyMethod, Is.Not.Null);
            Assert.That(CurrentDriverSeatField, Is.Not.Null);
            InputStateField.SetValue(controller, input);
            EntityMountable seat = vehicleObject.AddComponent<EntityMountable>();
            CurrentDriverSeatField.SetValue(controller, seat);

            // 直接覆盖“对象已禁用后销毁”场景，不能依赖 Unity 先调用 OnDisable。
            OnDestroyMethod.Invoke(controller, null);

            Assert.That(controller.InputState.frameIndex, Is.EqualTo(-1));
            Assert.That(controller.InputState.moveWorld, Is.EqualTo(Vector3.zero));
            Assert.That(controller.InputState.lookWorld, Is.EqualTo(Vector3.zero));
            Assert.That(controller.InputState.verticalInput, Is.EqualTo(0f));
            Assert.That(CurrentDriverSeatField.GetValue(controller), Is.Null);

            // OnDisable 随后可能再次触发；清理必须幂等且不抛异常。
            Assert.DoesNotThrow(() => OnDestroyMethod.Invoke(controller, null));
            Assert.That(controller.InputState.frameIndex, Is.EqualTo(-1));
        }

        [Test]
        public void KinematicController_DisableUnbinds_AndEnableRebindsMotor()
        {
            Rigidbody body = vehicleObject.GetComponent<Rigidbody>();
            body.isKinematic = true;
            vehicleObject.AddComponent<CapsuleCollider>();
            KinematicCharacterController.KinematicCharacterMotor motor =
                vehicleObject.AddComponent<KinematicCharacterController.KinematicCharacterMotor>();
            controller.motionBackend = VehicleMotionBackend.KinematicCharacterMotor;
            controller.physicsBody = body;
            controller.kinematicMotor = motor;

            Assert.That(controller.Initialize(), Is.True);
            Assert.That(motor.CharacterController, Is.SameAs(controller));

            controller.enabled = false;
            Assert.That(motor.CharacterController, Is.Null);
            Assert.That(controller.IsReady, Is.False);

            controller.enabled = true;
            Assert.That(motor.CharacterController, Is.SameAs(controller));
            Assert.That(controller.IsReady, Is.True);
        }

        [Test]
        public void VehicleScheduler_UnregisteringNextFeature_SkipsItInEveryPhase()
        {
            var beforeTarget = new RegistrationHolder();
            var rotationTarget = new RegistrationHolder();
            var velocityTarget = new RegistrationHolder();
            var afterTarget = new RegistrationHolder();
            bool beforeRan = false;
            bool rotationRan = false;
            bool velocityRan = false;
            bool afterRan = false;

            beforeTarget.registration = controller.RegisterMotionFeature(
                new BeforeFeature(_ => beforeRan = true),
                new VehicleMotionOrder(20, 0, 0, 0));
            controller.RegisterMotionFeature(
                new BeforeFeature(vehicle => vehicle.UnregisterMotionFeature(ref beforeTarget.registration)),
                new VehicleMotionOrder(10, 0, 0, 0));

            rotationTarget.registration = controller.RegisterMotionFeature(
                new RotationFeature((VehicleController _, Quaternion __, ref Quaternion ___, float ____) =>
                {
                    rotationRan = true;
                    return false;
                }),
                new VehicleMotionOrder(0, 20, 0, 0));
            controller.RegisterMotionFeature(
                new RotationFeature((VehicleController vehicle, Quaternion _, ref Quaternion __, float ___) =>
                {
                    vehicle.UnregisterMotionFeature(ref rotationTarget.registration);
                    return false;
                }),
                new VehicleMotionOrder(0, 10, 0, 0));

            velocityTarget.registration = controller.RegisterMotionFeature(
                new VelocityFeature((VehicleController _, Vector3 __, ref Vector3 ___, float ____) =>
                {
                    velocityRan = true;
                    return false;
                }),
                new VehicleMotionOrder(0, 0, 20, 0));
            controller.RegisterMotionFeature(
                new VelocityFeature((VehicleController vehicle, Vector3 _, ref Vector3 __, float ___) =>
                {
                    vehicle.UnregisterMotionFeature(ref velocityTarget.registration);
                    return false;
                }),
                new VehicleMotionOrder(0, 0, 10, 0));

            afterTarget.registration = controller.RegisterMotionFeature(
                new AfterFeature(_ => afterRan = true),
                new VehicleMotionOrder(0, 0, 0, 20));
            controller.RegisterMotionFeature(
                new AfterFeature(vehicle => vehicle.UnregisterMotionFeature(ref afterTarget.registration)),
                new VehicleMotionOrder(0, 0, 0, 10));

            DispatchBeforeMotion(controller);
            Quaternion rotation = Quaternion.identity;
            DispatchRotation(controller, ref rotation);
            Vector3 velocity = Vector3.zero;
            DispatchVelocity(controller, ref velocity);
            DispatchAfterMotion(controller);

            Assert.That(beforeRan, Is.False);
            Assert.That(rotationRan, Is.False);
            Assert.That(velocityRan, Is.False);
            Assert.That(afterRan, Is.False);
        }

        [Test]
        public void VehicleScheduler_ThrowingFeature_DoesNotBlockLaterFeatureInEveryPhase()
        {
            bool beforeRan = false;
            bool rotationRan = false;
            bool velocityRan = false;
            bool afterRan = false;
            controller.RegisterMotionFeature(
                new BeforeFeature(_ => throw new InvalidOperationException("Expected test exception.")),
                new VehicleMotionOrder(10, 0, 0, 0));
            controller.RegisterMotionFeature(
                new BeforeFeature(_ => beforeRan = true),
                new VehicleMotionOrder(20, 0, 0, 0));

            controller.RegisterMotionFeature(
                new RotationFeature((VehicleController _, Quaternion __, ref Quaternion current, float ___) =>
                {
                    current = Quaternion.Euler(0f, 90f, 0f);
                    throw new InvalidOperationException("Expected test exception.");
                }),
                new VehicleMotionOrder(0, 10, 0, 0));
            controller.RegisterMotionFeature(
                new RotationFeature((VehicleController _, Quaternion __, ref Quaternion ___, float ____) =>
                {
                    rotationRan = true;
                    return false;
                }),
                new VehicleMotionOrder(0, 20, 0, 0));

            controller.RegisterMotionFeature(
                new VelocityFeature((VehicleController _, Vector3 __, ref Vector3 current, float ___) =>
                {
                    current = Vector3.one;
                    throw new InvalidOperationException("Expected test exception.");
                }),
                new VehicleMotionOrder(0, 0, 10, 0));
            controller.RegisterMotionFeature(
                new VelocityFeature((VehicleController _, Vector3 __, ref Vector3 ___, float ____) =>
                {
                    velocityRan = true;
                    return false;
                }),
                new VehicleMotionOrder(0, 0, 20, 0));

            controller.RegisterMotionFeature(
                new AfterFeature(_ => throw new InvalidOperationException("Expected test exception.")),
                new VehicleMotionOrder(0, 0, 0, 10));
            controller.RegisterMotionFeature(
                new AfterFeature(_ => afterRan = true),
                new VehicleMotionOrder(0, 0, 0, 20));

            bool previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                DispatchBeforeMotion(controller);
                Quaternion rotation = Quaternion.identity;
                DispatchRotation(controller, ref rotation);
                Vector3 velocity = Vector3.zero;
                DispatchVelocity(controller, ref velocity);
                DispatchAfterMotion(controller);

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
            Assert.That(afterRan, Is.True);
        }

        [Test]
        public void MountedActionContract_RejectsGroundedStateThatCanReenter()
        {
            var config = new StateBasicConfig
            {
                stateName = "GroundAttack",
                stateSupportFlag = StateSupportFlags.Grounded,
                disableActiveOnSupportFlagSwitching = false,
                deactivateOnSupportFlagSwitching = true,
            };

            Assert.That(EntityBasicMountModule.ValidateMountedActionConfig(config, out _), Is.False);

            config.disableActiveOnSupportFlagSwitching = true;
            Assert.That(EntityBasicMountModule.ValidateMountedActionConfig(config, out string error), Is.True, error);
        }

        private static void DispatchBeforeMotion(VehicleController target)
        {
            Assert.That(DispatchBeforeMotionMethod, Is.Not.Null);
            DispatchBeforeMotionMethod.Invoke(target, new object[] { 0.02f });
        }

        private static void DispatchRotation(VehicleController target, ref Quaternion rotation)
        {
            Assert.That(DispatchRotationMethod, Is.Not.Null);
            object[] args = { rotation, 0.02f };
            DispatchRotationMethod.Invoke(target, args);
            rotation = (Quaternion)args[0];
        }

        private static void DispatchVelocity(VehicleController target, ref Vector3 velocity)
        {
            Assert.That(DispatchVelocityMethod, Is.Not.Null);
            object[] args = { velocity, 0.02f };
            DispatchVelocityMethod.Invoke(target, args);
            velocity = (Vector3)args[0];
        }

        private static void DispatchAfterMotion(VehicleController target)
        {
            Assert.That(DispatchAfterMotionMethod, Is.Not.Null);
            DispatchAfterMotionMethod.Invoke(target, new object[] { 0.02f });
        }

        private sealed class RegistrationHolder
        {
            public VehicleMotionRegistration registration;
        }

        private sealed class BeforeFeature : IVehicleBeforeMotion
        {
            private readonly Action<VehicleController> action;

            public BeforeFeature(Action<VehicleController> action)
            {
                this.action = action;
            }

            public void BeforeVehicleMotion(VehicleController vehicle, float deltaTime)
            {
                action(vehicle);
            }
        }

        private delegate bool RotationAction(
            VehicleController vehicle,
            Quaternion initialRotation,
            ref Quaternion currentRotation,
            float deltaTime);

        private sealed class RotationFeature : IVehicleRotationMotion
        {
            private readonly RotationAction action;

            public RotationFeature(RotationAction action)
            {
                this.action = action;
            }

            public bool UpdateVehicleRotation(
                VehicleController vehicle,
                Quaternion initialRotation,
                ref Quaternion currentRotation,
                float deltaTime)
            {
                return action(vehicle, initialRotation, ref currentRotation, deltaTime);
            }
        }

        private delegate bool VelocityAction(
            VehicleController vehicle,
            Vector3 initialVelocity,
            ref Vector3 currentVelocity,
            float deltaTime);

        private sealed class VelocityFeature : IVehicleVelocityMotion
        {
            private readonly VelocityAction action;

            public VelocityFeature(VelocityAction action)
            {
                this.action = action;
            }

            public bool UpdateVehicleVelocity(
                VehicleController vehicle,
                Vector3 initialVelocity,
                ref Vector3 currentVelocity,
                float deltaTime)
            {
                return action(vehicle, initialVelocity, ref currentVelocity, deltaTime);
            }
        }

        private sealed class AfterFeature : IVehicleAfterMotion
        {
            private readonly Action<VehicleController> action;

            public AfterFeature(Action<VehicleController> action)
            {
                this.action = action;
            }

            public void AfterVehicleMotion(VehicleController vehicle, float deltaTime)
            {
                action(vehicle);
            }
        }
    }
}
