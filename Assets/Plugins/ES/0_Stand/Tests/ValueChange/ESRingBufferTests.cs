using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;

namespace ES.Tests
{
    public sealed class ESRingBufferTests
    {
        private sealed class ReferenceItem
        {
            public readonly int value;

            public ReferenceItem(int value)
            {
                this.value = value;
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void Constructor_RejectsNonPositiveCapacity(int capacity)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ESRingBuffer<int>(capacity));
        }

        [Test]
        public void EnqueueAndDequeue_PreserveFifoAndRejectOverflowWithoutMutation()
        {
            var queue = new ESRingBuffer<int>(3);

            Assert.That(queue.TryEnqueue(10), Is.True);
            Assert.That(queue.TryEnqueue(20), Is.True);
            Assert.That(queue.TryEnqueue(30), Is.True);
            Assert.That(queue.TryEnqueue(40), Is.False);
            Assert.That(queue.Count, Is.EqualTo(3));
            Assert.That(queue.AvailableCapacity, Is.Zero);
            Assert.That(queue.TryPeekOldest(out int oldest), Is.True);
            Assert.That(queue.TryPeekNewest(out int newest), Is.True);
            Assert.That(oldest, Is.EqualTo(10));
            Assert.That(newest, Is.EqualTo(30));

            Assert.That(queue.TryDequeue(out int first), Is.True);
            Assert.That(queue.TryDequeue(out int second), Is.True);
            Assert.That(queue.TryDequeue(out int third), Is.True);
            Assert.That(queue.TryDequeue(out _), Is.False);
            Assert.That(new[] { first, second, third }, Is.EqualTo(new[] { 10, 20, 30 }));
            Assert.That(queue.IsEmpty, Is.True);
        }

        [Test]
        public void EnqueueOverwrite_ReportsRemovalAndKeepsNewestItems()
        {
            var buffer = new ESRingBuffer<int>(3);

            Assert.That(buffer.EnqueueOverwrite(10, out int beforeFull), Is.False);
            Assert.That(beforeFull, Is.Zero);
            buffer.EnqueueOverwrite(20, out _);
            buffer.EnqueueOverwrite(30, out _);

            Assert.That(buffer.EnqueueOverwrite(40, out int removed), Is.True);
            Assert.That(removed, Is.EqualTo(10));
            Assert.That(buffer.Count, Is.EqualTo(3));
            Assert.That(buffer[0], Is.EqualTo(20));
            Assert.That(buffer[1], Is.EqualTo(30));
            Assert.That(buffer[2], Is.EqualTo(40));
        }

        [Test]
        public void EnqueueOverwrite_ReleasesEvictedReferenceFromStorage()
        {
            var buffer = new ESRingBuffer<ReferenceItem>(2);
            var first = new ReferenceItem(1);
            var second = new ReferenceItem(2);
            var third = new ReferenceItem(3);
            buffer.TryEnqueue(first);
            buffer.TryEnqueue(second);
            ReferenceItem[] storage = GetStorage(buffer);

            Assert.That(buffer.EnqueueOverwrite(third, out ReferenceItem removed), Is.True);

            Assert.That(removed, Is.SameAs(first));
            Assert.That(Array.IndexOf(storage, first), Is.EqualTo(-1));
            Assert.That(Array.FindAll(storage, item => item != null).Length, Is.EqualTo(2));
        }

        [Test]
        public void CapacityOneAndNullValues_ReportReplacementUnambiguously()
        {
            var buffer = new ESRingBuffer<string>(1);

            Assert.That(buffer.EnqueueOverwrite(null, out string firstRemoved), Is.False);
            Assert.That(firstRemoved, Is.Null);
            Assert.That(buffer.EnqueueOverwrite("next", out string removedNull), Is.True);
            Assert.That(removedNull, Is.Null);
            Assert.That(buffer.EnqueueOverwrite("latest", out string removedNext), Is.True);
            Assert.That(removedNext, Is.EqualTo("next"));
            Assert.That(buffer[0], Is.EqualTo("latest"));
        }

        [Test]
        public void CustomComparer_DrivesContainsAndStableRemoval()
        {
            var buffer = new ESRingBuffer<string>(3, StringComparer.OrdinalIgnoreCase);
            buffer.TryEnqueue("Alpha");
            buffer.TryEnqueue("Beta");

            Assert.That(buffer.Contains("alpha"), Is.True);
            Assert.That(buffer.TryRemove("BETA", out string removed), Is.True);
            Assert.That(removed, Is.EqualTo("Beta"));
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void WrappedStorage_IndexesEnumeratesAndCopiesInLogicalOrder()
        {
            var queue = new ESRingBuffer<int>(4);
            queue.TryEnqueue(1);
            queue.TryEnqueue(2);
            queue.TryEnqueue(3);
            queue.TryDequeue(out _);
            queue.TryDequeue(out _);
            queue.TryEnqueue(4);
            queue.TryEnqueue(5);
            queue.TryEnqueue(6);

            Assert.That(queue[0], Is.EqualTo(3));
            Assert.That(queue[3], Is.EqualTo(6));
            Assert.That(queue.TryGetAt(4, out _), Is.False);

            var enumerated = new List<int>();
            foreach (int value in queue)
                enumerated.Add(value);
            Assert.That(enumerated, Is.EqualTo(new[] { 3, 4, 5, 6 }));

            var copied = new int[6];
            queue.CopyTo(copied, 1);
            Assert.That(copied, Is.EqualTo(new[] { 0, 3, 4, 5, 6, 0 }));
        }

        [Test]
        public void IndexAndCopy_RejectInvalidRangesWithoutMutation()
        {
            var buffer = new ESRingBuffer<int>(2);
            buffer.TryEnqueue(7);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[-1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = buffer[1]);
            Assert.Throws<ArgumentNullException>(() => buffer.CopyTo(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.CopyTo(new int[2], -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.CopyTo(new int[2], 3));
            Assert.Throws<ArgumentException>(() => buffer.CopyTo(new int[1], 1));

            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer[0], Is.EqualTo(7));
        }

        [Test]
        public void RemoveFromWrappedStorage_PreservesRemainingOrderAndClearsReleasedSlot()
        {
            var queue = new ESRingBuffer<ReferenceItem>(5);
            var one = new ReferenceItem(1);
            var two = new ReferenceItem(2);
            var three = new ReferenceItem(3);
            var four = new ReferenceItem(4);
            var five = new ReferenceItem(5);
            var six = new ReferenceItem(6);
            queue.TryEnqueue(one);
            queue.TryEnqueue(two);
            queue.TryEnqueue(three);
            queue.TryEnqueue(four);
            queue.TryDequeue(out _);
            queue.TryDequeue(out _);
            queue.TryEnqueue(five);
            queue.TryEnqueue(six);

            ReferenceItem[] storage = GetStorage(queue);
            Assert.That(queue.TryRemove(four, out ReferenceItem removed), Is.True);
            Assert.That(removed, Is.SameAs(four));
            Assert.That(queue.Count, Is.EqualTo(3));
            Assert.That(queue[0], Is.SameAs(three));
            Assert.That(queue[1], Is.SameAs(five));
            Assert.That(queue[2], Is.SameAs(six));
            Assert.That(Array.FindAll(storage, item => item != null).Length, Is.EqualTo(3));
        }

        [Test]
        public void DequeueAndClear_ReleaseStoredReferences()
        {
            var queue = new ESRingBuffer<ReferenceItem>(4);
            queue.TryEnqueue(new ReferenceItem(1));
            queue.TryEnqueue(new ReferenceItem(2));
            queue.TryEnqueue(new ReferenceItem(3));
            ReferenceItem[] storage = GetStorage(queue);

            queue.TryDequeue(out _);
            Assert.That(Array.FindAll(storage, item => item != null).Length, Is.EqualTo(2));
            queue.Clear();

            Assert.That(queue.Count, Is.Zero);
            Assert.That(Array.FindAll(storage, item => item != null).Length, Is.Zero);
        }

        [Test]
        public void Enumerator_RejectsMutationAfterEnumerationStarts()
        {
            var queue = new ESRingBuffer<int>(3);
            queue.TryEnqueue(1);
            queue.TryEnqueue(2);
            ESRingBuffer<int>.Enumerator enumerator = queue.GetEnumerator();
            Assert.That(enumerator.MoveNext(), Is.True);

            queue.TryDequeue(out _);

            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        [Test]
        public void RejectedMutation_DoesNotInvalidateEnumerator()
        {
            var buffer = new ESRingBuffer<int>(2);
            buffer.TryEnqueue(1);
            buffer.TryEnqueue(2);
            ESRingBuffer<int>.Enumerator enumerator = buffer.GetEnumerator();

            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(buffer.TryEnqueue(3), Is.False);
            Assert.That(enumerator.MoveNext(), Is.True);
            Assert.That(enumerator.Current, Is.EqualTo(2));
        }

        [Test]
        public void DeterministicMixedOperations_MatchReferenceQueue()
        {
            const int capacity = 17;
            const int operationCount = 20000;
            var random = new Random(7919);
            var queue = new ESRingBuffer<int>(capacity);
            var reference = new List<int>(capacity);

            for (int operation = 0; operation < operationCount; operation++)
            {
                switch (random.Next(8))
                {
                    case 0:
                    {
                        int value = random.Next(32);
                        bool expected = reference.Count < capacity;
                        Assert.That(queue.TryEnqueue(value), Is.EqualTo(expected));
                        if (expected)
                            reference.Add(value);
                        break;
                    }
                    case 1:
                    {
                        bool expected = reference.Count > 0;
                        Assert.That(queue.TryDequeue(out int value), Is.EqualTo(expected));
                        if (expected)
                        {
                            Assert.That(value, Is.EqualTo(reference[0]));
                            reference.RemoveAt(0);
                        }
                        break;
                    }
                    case 2:
                    {
                        int index = random.Next(-1, capacity + 1);
                        bool expected = index >= 0 && index < reference.Count;
                        Assert.That(queue.TryRemoveAt(index, out int value), Is.EqualTo(expected));
                        if (expected)
                        {
                            Assert.That(value, Is.EqualTo(reference[index]));
                            reference.RemoveAt(index);
                        }
                        break;
                    }
                    case 3:
                    {
                        int value = random.Next(32);
                        int index = reference.IndexOf(value);
                        bool expected = index >= 0;
                        Assert.That(queue.TryRemove(value, out int removed), Is.EqualTo(expected));
                        if (expected)
                        {
                            Assert.That(removed, Is.EqualTo(value));
                            reference.RemoveAt(index);
                        }
                        break;
                    }
                    case 4:
                    {
                        int value = random.Next(32);
                        bool expectedRemoval = reference.Count == capacity;
                        int expectedRemoved = expectedRemoval ? reference[0] : default;
                        if (expectedRemoval)
                            reference.RemoveAt(0);
                        reference.Add(value);

                        Assert.That(queue.EnqueueOverwrite(value, out int removed), Is.EqualTo(expectedRemoval));
                        if (expectedRemoval)
                            Assert.That(removed, Is.EqualTo(expectedRemoved));
                        break;
                    }
                    case 5:
                        if (random.Next(8) == 0)
                        {
                            queue.Clear();
                            reference.Clear();
                        }
                        break;
                    case 6:
                        Assert.That(queue.TryPeekOldest(out int oldest), Is.EqualTo(reference.Count > 0));
                        if (reference.Count > 0)
                            Assert.That(oldest, Is.EqualTo(reference[0]));
                        break;
                    default:
                        Assert.That(queue.TryPeekNewest(out int newest), Is.EqualTo(reference.Count > 0));
                        if (reference.Count > 0)
                            Assert.That(newest, Is.EqualTo(reference[reference.Count - 1]));
                        break;
                }

                Assert.That(queue.Count, Is.EqualTo(reference.Count));
                for (int i = 0; i < reference.Count; i++)
                    Assert.That(queue[i], Is.EqualTo(reference[i]));
            }
        }

        [Test]
        public void WarmedSteadyOperations_HaveNoManagedThreadAllocation()
        {
            var queue = new ESRingBuffer<int>(8);
            for (int i = 0; i < 32; i++)
            {
                queue.TryEnqueue(i);
                queue.TryDequeue(out _);
            }

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                queue.TryEnqueue(i);
                queue.TryPeekOldest(out _);
                queue.TryPeekNewest(out _);
                queue.TryDequeue(out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void WarmedContainsRemoveAndConcreteEnumeration_HaveNoManagedThreadAllocation()
        {
            var queue = new ESRingBuffer<int>(8);
            for (int i = 0; i < queue.Capacity; i++)
                queue.TryEnqueue(i);
            queue.Contains(3);
            queue.Remove(3);
            queue.TryEnqueue(3);
            int checksum = 0;
            foreach (int value in queue)
                checksum += value;

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                queue.Contains(3);
                queue.Remove(3);
                queue.TryEnqueue(3);
                foreach (int value in queue)
                    checksum += value;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            GC.KeepAlive(checksum);
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void WarmedOverwriteRejectAndRead_HaveNoManagedThreadAllocation()
        {
            var buffer = new ESRingBuffer<int>(8);
            for (int i = 0; i < buffer.Capacity; i++)
                buffer.TryEnqueue(i);
            for (int i = 0; i < 32; i++)
                buffer.EnqueueOverwrite(i, out _);

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            int checksum = 0;
            for (int i = 0; i < 10000; i++)
            {
                buffer.TryEnqueue(i);
                buffer.EnqueueOverwrite(i, out int removed);
                buffer.TryPeekOldest(out int oldest);
                buffer.TryPeekNewest(out int newest);
                buffer.TryGetAt(3, out int indexed);
                checksum += removed + oldest + newest + indexed;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            GC.KeepAlive(checksum);
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void UnityProfiler_WarmedSteadyOperations_RecordNoGcAllocSamples()
        {
            var buffer = new ESRingBuffer<int>(8);
            for (int i = 0; i < buffer.Capacity; i++)
                buffer.TryEnqueue(i);
            for (int i = 0; i < 64; i++)
            {
                buffer.EnqueueOverwrite(i, out _);
                buffer.Contains(i);
                foreach (int value in buffer)
                    GC.KeepAlive(value);
            }

            var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                1,
                ProfilerRecorderOptions.StartImmediately
                | ProfilerRecorderOptions.CollectOnlyOnCurrentThread
                | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            int checksum = 0;
            for (int i = 0; i < 10000; i++)
            {
                buffer.TryEnqueue(i);
                buffer.EnqueueOverwrite(i, out int removed);
                buffer.TryPeekOldest(out int oldest);
                buffer.TryPeekNewest(out int newest);
                buffer.TryGetAt(3, out int indexed);
                buffer.Contains(i);
                foreach (int value in buffer)
                    checksum += value;
                checksum += removed + oldest + newest + indexed;
            }
            recorder.Stop();
            bool recorderValid = recorder.Valid;
            int allocationSampleCount = recorder.Count;
            long lastAllocationSize = recorder.LastValue;
            recorder.Dispose();

            GC.KeepAlive(checksum);
            Assert.That(recorderValid, Is.True, "Unity Profiler does not expose the GC.Alloc marker on this editor/platform.");
            Assert.That(
                allocationSampleCount,
                Is.Zero,
                "Warmed ESRingBuffer steady operations emitted GC.Alloc samples. Last sample bytes: " + lastAllocationSize);
        }

        private static ReferenceItem[] GetStorage(ESRingBuffer<ReferenceItem> queue)
        {
            FieldInfo field = typeof(ESRingBuffer<ReferenceItem>).GetField(
                "items",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (ReferenceItem[])field.GetValue(queue);
        }
    }
}
