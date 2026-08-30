using System;
using System.Reflection;
using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESVfxOpSupportLifecycleTests
    {
        [Test]
        public void VfxHandle_IsOwnedBySupportScope_AndCanBeTakenExactlyOnce()
        {
            ESOpSupport support = ESOpSupport.CreateStandalone();
            TestVfxOperation operation = new TestVfxOperation();
            ESVfxHandle handle = CreateHandle(17, 3);

            support.SetVfxHandle(operation, handle);

            Assert.That(support.TryTakeVfxHandle(operation, out ESVfxHandle taken), Is.True);
            Assert.That(taken, Is.EqualTo(handle));
            Assert.That(support.TryTakeVfxHandle(operation, out _), Is.False);

            support.Dispose();
        }

        [Test]
        public void InvalidVfxHandle_DoesNotCreateOwnershipEntry()
        {
            ESOpSupport support = ESOpSupport.CreateStandalone();
            TestVfxOperation operation = new TestVfxOperation();

            support.SetVfxHandle(operation, default);

            Assert.That(support.TryTakeVfxHandle(operation, out _), Is.False);
            support.Dispose();
        }

        [Test]
        public void SetVfxHandle_ReplacesPreviousHandleForSameOperation()
        {
            ESOpSupport support = ESOpSupport.CreateStandalone();
            TestVfxOperation operation = new TestVfxOperation();
            ESVfxHandle first = CreateHandle(21, 1);
            ESVfxHandle second = CreateHandle(22, 1);

            support.SetVfxHandle(operation, first);
            support.SetVfxHandle(operation, second);

            Assert.That(support.TryTakeVfxHandle(operation, out ESVfxHandle taken), Is.True);
            Assert.That(taken, Is.EqualTo(second));
            Assert.That(support.TryTakeVfxHandle(operation, out _), Is.False);
            support.Dispose();
        }

        [Test]
        public void AddVfxHandle_CanAccumulateWithoutOverwriting()
        {
            ESOpSupport support = ESOpSupport.CreateStandalone();
            TestVfxOperation operation = new TestVfxOperation();
            ESVfxHandle first = CreateHandle(31, 1);
            ESVfxHandle second = CreateHandle(32, 1);

            support.AddVfxHandle(operation, first);
            support.AddVfxHandle(operation, second);

            Assert.That(support.TryTakeVfxHandle(operation, out ESVfxHandle takenSecond), Is.True);
            Assert.That(takenSecond, Is.EqualTo(second));
            Assert.That(support.TryTakeVfxHandle(operation, out ESVfxHandle takenFirst), Is.True);
            Assert.That(takenFirst, Is.EqualTo(first));
            Assert.That(support.TryTakeVfxHandle(operation, out _), Is.False);

            support.Dispose();
        }

        [Test]
        public void VfxDiagnostics_DescribesPoolGenerationFailure()
        {
            Assert.That(ESVfxDiagnostics.DescribeFailure(ESVfxFailureCode.PoolReturnRejected),
                Does.Contain("代际"));
        }

        [Test]
        public void AudioHandles_CanAccumulateWithoutOverwriting()
        {
            ESOpSupport support = ESOpSupport.CreateStandalone();
            TestVfxOperation operation = new TestVfxOperation();
            ESAudioVoiceHandle first = CreateAudioHandle(11, 1);
            ESAudioVoiceHandle second = CreateAudioHandle(12, 1);

            support.AddAudioVoiceHandle(operation, first);
            support.AddAudioVoiceHandle(operation, second);

            Assert.That(support.TryTakeAudioVoiceHandle(operation, out ESAudioVoiceHandle takenSecond), Is.True);
            Assert.That(takenSecond, Is.EqualTo(second));
            Assert.That(support.TryTakeAudioVoiceHandle(operation, out ESAudioVoiceHandle takenFirst), Is.True);
            Assert.That(takenFirst, Is.EqualTo(first));
            Assert.That(support.TryTakeAudioVoiceHandle(operation, out _), Is.False);

            support.Dispose();
        }

        private static ESVfxHandle CreateHandle(int id, int generation)
        {
            ConstructorInfo constructor = typeof(ESVfxHandle).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (ESVfxHandle)constructor.Invoke(new object[] { id, generation });
        }

        private static ESAudioVoiceHandle CreateAudioHandle(int id, int generation)
        {
            ConstructorInfo constructor = typeof(ESAudioVoiceHandle).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(int) },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (ESAudioVoiceHandle)constructor.Invoke(new object[] { id, generation });
        }

        [Serializable]
        private sealed class TestVfxOperation : ESOutputOp
        {
            protected override void StartOperation(ESRuntimeTargetPack target, ESOpSupport scopeSupport, ESOpSupport hostSupport) { }
        }
    }
}
