using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ES.Tests.DynamicAtlas
{
    public sealed class ESDynamicAtlasAllocatorTests
    {
        [Test]
        public void Free_ReusesReleasedSlot()
        {
            var allocator = new ESDynamicAtlasAllocator(64, 64);

            Assert.That(allocator.TryAllocate(24, 24, out RectInt first), Is.True);
            Assert.That(allocator.TryAllocate(24, 24, out _), Is.True);
            allocator.Free(first);

            Assert.That(allocator.TryAllocate(24, 24, out RectInt reused), Is.True);
            Assert.That(reused, Is.EqualTo(first));
        }

        [Test]
        public void Free_AllAllocations_MergesBackToWholePage()
        {
            var allocator = new ESDynamicAtlasAllocator(64, 64);
            Assert.That(allocator.TryAllocate(32, 64, out RectInt left), Is.True);
            Assert.That(allocator.TryAllocate(32, 64, out RectInt right), Is.True);

            allocator.Free(left);
            allocator.Free(right);

            Assert.That(allocator.UsedPixels, Is.Zero);
            Assert.That(allocator.FreeRectCount, Is.EqualTo(1));
            Assert.That(allocator.TryAllocate(64, 64, out RectInt whole), Is.True);
            Assert.That(whole, Is.EqualTo(new RectInt(0, 0, 64, 64)));
        }

        [Test]
        public void Allocate_RejectsOversizedContentWithoutChangingUsage()
        {
            var allocator = new ESDynamicAtlasAllocator(64, 64);

            Assert.That(allocator.TryAllocate(65, 16, out _), Is.False);
            Assert.That(allocator.UsedPixels, Is.Zero);
            Assert.That(allocator.FreeRectCount, Is.EqualTo(1));
        }

        [Test]
        public void Free_DuplicateRelease_DoesNotCorruptUsage()
        {
            var allocator = new ESDynamicAtlasAllocator(64, 64);
            Assert.That(allocator.TryAllocate(16, 16, out RectInt slot), Is.True);

            allocator.Free(slot);
            allocator.Free(slot);

            Assert.That(allocator.UsedPixels, Is.Zero);
            Assert.That(allocator.FreeRectCount, Is.EqualTo(1));
            Assert.That(allocator.TryAllocate(64, 64, out _), Is.True);
        }

        [Test]
        public void AllocateAndFree_TwoThousandMixedRects_RestoresWholePage()
        {
            const int pageSize = 2048;
            const int entryCount = 2000;
            var allocator = new ESDynamicAtlasAllocator(pageSize, pageSize);
            var allocations = new List<RectInt>(entryCount);
            var random = new System.Random(20260808);

            for (int index = 0; index < entryCount; index++)
            {
                int width = random.Next(8, 49);
                int height = random.Next(8, 49);
                Assert.That(allocator.TryAllocate(width, height, out RectInt rect), Is.True,
                    $"第 {index} 个混合尺寸区域无法分配。");
                allocations.Add(rect);
            }

            // Deliberately free in a different order so adjacent-merge behavior is
            // exercised through fragmented holes rather than only stack unwinding.
            for (int index = allocations.Count - 1; index >= 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                RectInt rect = allocations[swapIndex];
                allocations[swapIndex] = allocations[index];
                allocations[index] = rect;
                allocator.Free(rect);
            }

            Assert.That(allocator.UsedPixels, Is.Zero);
            Assert.That(allocator.FreeRectCount, Is.EqualTo(1));
            Assert.That(allocator.TryAllocate(pageSize, pageSize, out RectInt whole), Is.True);
            Assert.That(whole, Is.EqualTo(new RectInt(0, 0, pageSize, pageSize)));
        }
    }
}
