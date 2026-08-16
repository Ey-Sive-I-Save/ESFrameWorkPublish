using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESFixedQueueTests
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
            Assert.Throws<ArgumentOutOfRangeException>(() => new ESFixedQueue<int>(capacity));
        }

        [Test]
        public void EnqueueAndDequeue_PreserveFifoAndRejectOverflowWithoutMutation()
        {
            var queue = new ESFixedQueue<int>(3);

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
        public void WrappedStorage_IndexesEnumeratesAndCopiesInLogicalOrder()
        {
            var queue = new ESFixedQueue<int>(4);
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
        public void RemoveFromWrappedStorage_PreservesRemainingOrderAndClearsReleasedSlot()
        {
            var queue = new ESFixedQueue<ReferenceItem>(5);
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
            var queue = new ESFixedQueue<ReferenceItem>(4);
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
            var queue = new ESFixedQueue<int>(3);
            queue.TryEnqueue(1);
            queue.TryEnqueue(2);
            ESFixedQueue<int>.Enumerator enumerator = queue.GetEnumerator();
            Assert.That(enumerator.MoveNext(), Is.True);

            queue.TryDequeue(out _);

            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        [Test]
        public void WarmedSteadyOperations_HaveNoManagedThreadAllocation()
        {
            var queue = new ESFixedQueue<int>(8);
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

        private static ReferenceItem[] GetStorage(ESFixedQueue<ReferenceItem> queue)
        {
            FieldInfo field = typeof(ESFixedQueue<ReferenceItem>).GetField(
                "items",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (ReferenceItem[])field.GetValue(queue);
        }
    }
}
