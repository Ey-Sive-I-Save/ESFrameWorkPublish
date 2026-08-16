using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESRecentHistoryTests
    {
        [Test]
        public void AddWithinCapacity_DoesNotReportRemoval()
        {
            var history = new ESRecentHistory<string>(3);

            Assert.That(history.Add("one", out string removed), Is.False);
            Assert.That(removed, Is.Null);
            history.Add("two");

            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history.TryGetOldest(out string oldest), Is.True);
            Assert.That(history.TryGetLatest(out string latest), Is.True);
            Assert.That(oldest, Is.EqualTo("one"));
            Assert.That(latest, Is.EqualTo("two"));
        }

        [Test]
        public void AddBeyondCapacity_RemovesOldestAndKeepsChronologicalOrder()
        {
            var history = new ESRecentHistory<int>(3);
            history.Add(10);
            history.Add(20);
            history.Add(30);

            Assert.That(history.Add(40, out int removed), Is.True);
            Assert.That(removed, Is.EqualTo(10));
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history[0], Is.EqualTo(20));
            Assert.That(history[1], Is.EqualTo(30));
            Assert.That(history[2], Is.EqualTo(40));

            var values = new List<int>();
            foreach (int value in history)
                values.Add(value);
            Assert.That(values, Is.EqualTo(new[] { 20, 30, 40 }));
        }

        [Test]
        public void NullOldest_IsStillReportedAsRemoved()
        {
            var history = new ESRecentHistory<string>(2);
            history.Add(null);
            history.Add("second");

            Assert.That(history.Add("third", out string removed), Is.True);
            Assert.That(removed, Is.Null);
            Assert.That(history[0], Is.EqualTo("second"));
            Assert.That(history[1], Is.EqualTo("third"));
        }

        [Test]
        public void CopyAndClear_PreservePublicHistoryContract()
        {
            var history = new ESRecentHistory<int>(3);
            history.Add(1);
            history.Add(2);
            history.Add(3);
            history.Add(4);
            var destination = new int[5];

            history.CopyTo(destination, 1);

            Assert.That(destination, Is.EqualTo(new[] { 0, 2, 3, 4, 0 }));
            history.Clear();
            Assert.That(history.Count, Is.Zero);
            Assert.That(history.TryGetOldest(out _), Is.False);
            Assert.That(history.TryGetLatest(out _), Is.False);
        }

        [Test]
        public void WarmedOverwriteAndRead_HaveNoManagedThreadAllocation()
        {
            var history = new ESRecentHistory<int>(8);
            for (int i = 0; i < history.Capacity; i++)
                history.Add(i);
            for (int i = 0; i < 32; i++)
                history.Add(i, out _);

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                history.Add(i, out _);
                history.TryGetOldest(out _);
                history.TryGetLatest(out _);
                history.TryGetAt(3, out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }
    }
}
