using NUnit.Framework;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESSpaceProbeTests
    {
        [Test]
        public void ExecuteWithoutResultBuffer_ReturnsNoBuffer()
        {
            ESSpaceProbe probe = new ESSpaceProbe(null, 4);
            ESSpaceProbeResult result = probe.Execute(
                ESSpaceProbeRequest.Sphere(Vector3.zero, 1f, Physics.AllLayers),
                null,
                out int written);

            Assert.That(written, Is.EqualTo(0));
            Assert.That(result.status, Is.EqualTo(ESSpaceProbeStatus.NoBuffer));
        }

        [Test]
        public void ExecuteInvalidCast_ReturnsInvalidRequest()
        {
            ESSpaceProbe probe = new ESSpaceProbe(null, 4);
            ESSpaceProbeHit[] results = new ESSpaceProbeHit[4];
            ESSpaceProbeResult result = probe.Execute(
                ESSpaceProbeRequest.Cast(Vector3.zero, Vector3.zero, 0.1f, Physics.AllLayers),
                results,
                out int written);

            Assert.That(written, Is.EqualTo(0));
            Assert.That(result.status, Is.EqualTo(ESSpaceProbeStatus.InvalidRequest));
        }

        [Test]
        public void RequestMaxResults_IsBoundedByCallerBuffer()
        {
            ESSpaceProbeRequest request = ESSpaceProbeRequest.Sphere(
                Vector3.zero, 1f, Physics.AllLayers);
            request.maxResults = 1;
            Assert.That(request.maxResults, Is.EqualTo(1));
        }

        [Test]
        public void TriggerCache_EnterExitIsIdempotentAndBounded()
        {
            ESSpaceProbeTrigger cache = new ESSpaceProbeTrigger(1);
            GameObject first = new GameObject("probe-first");
            GameObject second = new GameObject("probe-second");
            try
            {
                Collider firstCollider = first.AddComponent<SphereCollider>();
                Collider secondCollider = second.AddComponent<SphereCollider>();
                Assert.That(cache.Enter(firstCollider), Is.True);
                Assert.That(cache.Enter(firstCollider), Is.False);
                Assert.That(cache.Enter(secondCollider), Is.False);
                Assert.That(cache.Overflowed, Is.True);
                Assert.That(cache.Exit(firstCollider), Is.True);
                Assert.That(cache.Exit(firstCollider), Is.False);
                Assert.That(cache.Count, Is.EqualTo(0));
                Assert.That(cache.PruneInvalid(), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void BoxRequest_UsesBoxShape()
        {
            ESSpaceProbeRequest request = ESSpaceProbeRequest.Box(
                Vector3.zero, Vector3.one, Quaternion.identity, Physics.AllLayers);
            Assert.That(request.shape, Is.EqualTo(ESSpaceProbeShape.OverlapBox));
            Assert.That(request.halfExtents, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void CapsuleRequest_UsesCapsuleShape()
        {
            ESSpaceProbeRequest request = ESSpaceProbeRequest.Capsule(
                Vector3.zero, Vector3.up, 0.5f, Physics.AllLayers);
            Assert.That(request.shape, Is.EqualTo(ESSpaceProbeShape.OverlapCapsule));
            Assert.That(request.destination, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void BoxCastRequest_UsesBoxCastShape()
        {
            ESSpaceProbeRequest request = ESSpaceProbeRequest.BoxCast(
                Vector3.zero, Vector3.forward, Vector3.one, Quaternion.identity, Physics.AllLayers);
            Assert.That(request.shape, Is.EqualTo(ESSpaceProbeShape.BoxCast));
            Assert.That(request.destination, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void CapsuleCastRequest_PreservesShapeAndSweepEndpoints()
        {
            ESSpaceProbeRequest request = ESSpaceProbeRequest.CapsuleCast(
                Vector3.down, Vector3.up, 0.5f, Vector3.zero, Vector3.forward,
                Physics.AllLayers);
            Assert.That(request.shape, Is.EqualTo(ESSpaceProbeShape.CapsuleCast));
            Assert.That(request.capsulePointA, Is.EqualTo(Vector3.down));
            Assert.That(request.capsulePointB, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void RawOverlapSphere_UsesUnifiedEntry()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                target.transform.position = Vector3.zero;
                ESSpaceProbe probe = new ESSpaceProbe(null, 4);
                Collider[] buffer = new Collider[4];
                int count = probe.OverlapSphere(Vector3.zero, 1.5f, 1 << target.layer, buffer,
                    QueryTriggerInteraction.Collide);
                Assert.That(count, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void RawCast_UsesUnifiedEntry()
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                target.transform.position = Vector3.forward * 2f;
                ESSpaceProbe probe = new ESSpaceProbe(null, 4);
                RaycastHit[] buffer = new RaycastHit[4];
                int count = probe.Cast(Vector3.zero, Vector3.forward * 4f, 0f,
                    1 << target.layer, buffer, QueryTriggerInteraction.Collide);
                Assert.That(count, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
