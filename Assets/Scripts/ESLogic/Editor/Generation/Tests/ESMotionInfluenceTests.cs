using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESMotionInfluenceTests
    {
        [Test]
        public void VelocityDelta_IsAppliedOnceAfterBaseVelocity()
        {
            var accumulator = new ESMotionInfluenceAccumulator();
            accumulator.AddVelocity(new Vector3(3f, 2f, 0f));
            Vector3 velocity = new Vector3(4f, 0f, 0f);

            accumulator.Apply(
                ref velocity,
                Vector3.zero,
                0.02f,
                ESMotionReceiverLockState.None,
                100f,
                100f);

            Assert.That(velocity, Is.EqualTo(new Vector3(7f, 2f, 0f)));
            accumulator.Apply(
                ref velocity,
                Vector3.zero,
                0.02f,
                ESMotionReceiverLockState.None,
                100f,
                100f);
            Assert.That(velocity, Is.EqualTo(new Vector3(7f, 2f, 0f)));
        }

        [Test]
        public void LockedVelocityDelta_RequiresMatchingPermission()
        {
            var accumulator = new ESMotionInfluenceAccumulator();
            accumulator.AddVelocity(Vector3.right * 5f);
            accumulator.AddVelocity(
                Vector3.up * 2f,
                ESMotionInfluencePermissions.AllowWhileMounted);
            Vector3 velocity = Vector3.zero;

            accumulator.Apply(
                ref velocity,
                Vector3.zero,
                0.02f,
                ESMotionReceiverLockState.Mounted,
                100f,
                100f);

            Assert.That(velocity, Is.EqualTo(Vector3.up * 2f));
        }

        [Test]
        public void AccelerationFields_AreSummedThenClamped()
        {
            var accumulator = new ESMotionInfluenceAccumulator();
            Assert.That(accumulator.TryAcquireField(new ESMotionFieldRequest
            {
                kind = ESMotionFieldKind.Acceleration,
                acceleration = Vector3.right * 10f
            }, out _), Is.True);
            Assert.That(accumulator.TryAcquireField(new ESMotionFieldRequest
            {
                kind = ESMotionFieldKind.Acceleration,
                acceleration = Vector3.up * 10f
            }, out _), Is.True);
            Vector3 velocity = Vector3.zero;

            accumulator.Apply(
                ref velocity,
                Vector3.zero,
                1f,
                ESMotionReceiverLockState.None,
                5f,
                100f);

            Assert.That(velocity.magnitude, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(velocity.normalized, Is.EqualTo(new Vector3(1f, 1f, 0f).normalized));
        }

        [Test]
        public void OldLease_CannotReleaseReusedSlotAfterReset()
        {
            var accumulator = new ESMotionInfluenceAccumulator(1);
            accumulator.TryAcquireField(new ESMotionFieldRequest
            {
                kind = ESMotionFieldKind.Acceleration,
                acceleration = Vector3.right
            }, out ESMotionFieldLease oldLease);
            accumulator.Reset();
            accumulator.TryAcquireField(new ESMotionFieldRequest
            {
                kind = ESMotionFieldKind.Acceleration,
                acceleration = Vector3.up
            }, out ESMotionFieldLease newLease);

            oldLease.Dispose();

            Assert.That(accumulator.ActiveFieldCount, Is.EqualTo(1));
            newLease.Dispose();
            Assert.That(accumulator.ActiveFieldCount, Is.Zero);
        }

        [Test]
        public void FifthField_UsesOverflowAndKeepsAllLeasesValid()
        {
            var accumulator = new ESMotionInfluenceAccumulator();
            var leases = new ESMotionFieldLease[5];
            for (int i = 0; i < leases.Length; i++)
            {
                Assert.That(accumulator.TryAcquireField(new ESMotionFieldRequest
                {
                    kind = ESMotionFieldKind.Acceleration,
                    acceleration = Vector3.right
                }, out leases[i]), Is.True);
            }

            Assert.That(accumulator.ActiveFieldCount, Is.EqualTo(5));
            for (int i = 0; i < leases.Length; i++)
                leases[i].Dispose();
            Assert.That(accumulator.ActiveFieldCount, Is.Zero);
        }

        [Test]
        public void TargetVelocityAttraction_ConvergesWithoutExceedingAcceleration()
        {
            var settings = new ESMotionAttractionSettings
            {
                model = ESMotionAttractionModel.TargetVelocity,
                stopRadius = 1f,
                maxSpeed = 10f,
                maxAcceleration = 4f,
                response = 2f
            };

            Vector3 acceleration = ESMotionInfluenceSolver.EvaluateAttractionAcceleration(
                Vector3.zero,
                Vector3.zero,
                Vector3.right * 10f,
                settings,
                0.5f);

            Assert.That(acceleration, Is.EqualTo(Vector3.right * 4f));
        }

        [Test]
        public void TargetVelocityAttraction_DeceleratesInsideStopRadius()
        {
            var settings = new ESMotionAttractionSettings
            {
                model = ESMotionAttractionModel.TargetVelocity,
                stopRadius = 1f,
                maxSpeed = 10f,
                maxAcceleration = 4f,
                response = 2f
            };

            Vector3 acceleration = ESMotionInfluenceSolver.EvaluateAttractionAcceleration(
                Vector3.zero,
                Vector3.right * 3f,
                Vector3.right * 0.5f,
                settings,
                0.5f);

            Assert.That(acceleration, Is.EqualTo(Vector3.left * 4f));
        }

        [Test]
        public void SpringDamperAttraction_PreservesTangentialVelocity()
        {
            var settings = new ESMotionAttractionSettings
            {
                model = ESMotionAttractionModel.SpringDamper,
                stopRadius = 0f,
                maxAcceleration = 100f,
                stiffness = 1f,
                damping = 2f
            };

            Vector3 acceleration = ESMotionInfluenceSolver.EvaluateAttractionAcceleration(
                Vector3.zero,
                new Vector3(2f, 3f, 0f),
                Vector3.right * 10f,
                settings,
                0.02f);

            Assert.That(acceleration, Is.EqualTo(Vector3.right * 6f));
        }

        [Test]
        public void MotionOp_DoesNotExposeUnityForceMode()
        {
            Assert.That(typeof(OpMotion_AddVelocity).GetField("permissions"), Is.Not.Null);
            Assert.That(typeof(OpMotion_AddVelocity).GetField("forceMode"), Is.Null);
        }

        [Test]
        public void Resolver_DoesNotTreatBareRigidbodyAsMotionReceiver()
        {
            var target = new GameObject("Bare Rigidbody");
            try
            {
                target.AddComponent<Rigidbody>();

                Assert.That(
                    ESMotionInfluenceReceiverResolver.TryResolve(target, out _),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void SimpleApi_ReturnsFalseForUnsupportedTarget()
        {
            var target = new GameObject("Unsupported Motion Target");
            try
            {
                Assert.That(ESMotion.AddVelocity(target, Vector3.right), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
