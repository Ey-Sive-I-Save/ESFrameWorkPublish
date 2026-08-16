using NUnit.Framework;

namespace ES.Tests
{
    public sealed class ESInstanceTableTests
    {
        [Test]
        public void AddGetAndCapacityFailure_AreDeterministic()
        {
            ESInstanceTable<int, int, int, int> table = new ESInstanceTable<int, int, int, int>(2);
            Assert.That(table.TryAdd(10, 100, 1, 2, out ESInstanceHandle first), Is.True);
            Assert.That(table.TryAdd(20, 200, 1, 2, out ESInstanceHandle second), Is.True);
            Assert.That(table.TryAdd(30, 300, 2, 2, out _), Is.False);
            Assert.That(table.TryGet(first, out int firstValue), Is.True);
            Assert.That(table.TryGet(second, out int secondValue), Is.True);
            Assert.That(firstValue, Is.EqualTo(10));
            Assert.That(secondValue, Is.EqualTo(20));
            Assert.That(table.Count, Is.EqualTo(2));
        }

        [Test]
        public void SwapRemove_PreservesMovedHandleAndInvalidatesRemovedHandle()
        {
            ESInstanceTable<int, int, int, int> table = new ESInstanceTable<int, int, int, int>(3);
            Assert.That(table.TryAdd(10, 100, 1, 2, out ESInstanceHandle first), Is.True);
            Assert.That(table.TryAdd(20, 200, 1, 2, out ESInstanceHandle second), Is.True);
            Assert.That(table.TryAdd(30, 300, 2, 2, out ESInstanceHandle third), Is.True);

            Assert.That(table.TryRemove(second, out int removed), Is.True);
            Assert.That(removed, Is.EqualTo(20));
            Assert.That(table.TryGet(second, out _), Is.False);
            Assert.That(table.TryGet(first, out int firstValue), Is.True);
            Assert.That(table.TryGet(third, out int thirdValue), Is.True);
            Assert.That(firstValue, Is.EqualTo(10));
            Assert.That(thirdValue, Is.EqualTo(30));
            Assert.That(table.Count, Is.EqualTo(2));
        }

        [Test]
        public void SlotReuse_RejectsOldGeneration()
        {
            ESInstanceTable<int, int, int, int> table = new ESInstanceTable<int, int, int, int>(1);
            Assert.That(table.TryAdd(10, 100, 1, 2, out ESInstanceHandle oldHandle), Is.True);
            Assert.That(table.TryRemove(oldHandle, out _), Is.True);
            Assert.That(table.TryAdd(20, 200, 1, 2, out ESInstanceHandle newHandle), Is.True);
            Assert.That(newHandle.slot, Is.EqualTo(oldHandle.slot));
            Assert.That(newHandle.slotGeneration, Is.Not.EqualTo(oldHandle.slotGeneration));
            Assert.That(table.TryGet(oldHandle, out _), Is.False);
            Assert.That(table.TryGet(newHandle, out int value), Is.True);
            Assert.That(value, Is.EqualTo(20));
        }

        [Test]
        public void Clear_AdvancesTableEpochAndInvalidatesAllHandles()
        {
            ESInstanceTable<int, int, int, int> table = new ESInstanceTable<int, int, int, int>(2);
            Assert.That(table.TryAdd(10, 100, 1, 2, out ESInstanceHandle first), Is.True);
            uint epoch = table.TableEpoch;
            table.Clear();

            Assert.That(table.TableEpoch, Is.EqualTo(epoch + 1));
            Assert.That(table.TryGet(first, out _), Is.False);
            Assert.That(table.Count, Is.Zero);
            Assert.That(table.TryAdd(30, 300, 1, 2, out ESInstanceHandle replacement), Is.True);
            Assert.That(replacement.tableEpoch, Is.EqualTo(epoch + 1));
        }

        [Test]
        public void DifferentTables_RejectEachOthersHandles()
        {
            ESInstanceTable<int, int, int, int> left = new ESInstanceTable<int, int, int, int>(1);
            ESInstanceTable<int, int, int, int> right = new ESInstanceTable<int, int, int, int>(1);
            Assert.That(left.TryAdd(10, 100, 1, 2, out ESInstanceHandle handle), Is.True);
            Assert.That(right.TryGet(handle, out _), Is.False);
        }

        [Test]
        public void DifferentClosedGenericTables_HaveProcessWideDistinctTokens()
        {
            ESInstanceTable<int, int, int, int> itemTable = new ESInstanceTable<int, int, int, int>(1);
            ESInstanceTable<long, int, int, int> buffTable = new ESInstanceTable<long, int, int, int>(1);
            Assert.That(itemTable.TryAdd(10, 100, 1, 2, out ESInstanceHandle itemHandle), Is.True);
            Assert.That(buffTable.TryAdd(20L, 200, 1, 2, out ESInstanceHandle buffHandle), Is.True);

            Assert.That(itemTable.TableToken, Is.Not.EqualTo(buffTable.TableToken));
            Assert.That(itemHandle, Is.Not.EqualTo(buffHandle));
            Assert.That(buffTable.TryGet(itemHandle, out _), Is.False);
            Assert.That(itemTable.TryGet(buffHandle, out _), Is.False);
        }

        [Test]
        public void PersistentDefinitionAndOwnerIndexes_ReturnCurrentHandles()
        {
            ESInstanceTable<int, int, int, int> table = new ESInstanceTable<int, int, int, int>(3);
            Assert.That(table.TryAdd(10, 100, 7, 9, out ESInstanceHandle first), Is.True);
            Assert.That(table.TryAdd(20, 200, 7, 9, out ESInstanceHandle second), Is.True);
            Assert.That(table.TryGetByPersistentId(200, out ESInstanceHandle byPersistent), Is.True);
            Assert.That(byPersistent, Is.EqualTo(second));
            Assert.That(table.TryGetDefinitionBucket(7, out ESInstanceHandle definitionFirst, out int definitionCount), Is.True);
            Assert.That(definitionFirst, Is.EqualTo(first));
            Assert.That(definitionCount, Is.EqualTo(2));
            Assert.That(table.TryGetOwnerBucket(9, out ESInstanceHandle ownerFirst, out int ownerCount), Is.True);
            Assert.That(ownerFirst, Is.EqualTo(first));
            Assert.That(ownerCount, Is.EqualTo(2));
            Assert.That(table.TryGetNextByDefinition(first, out ESInstanceHandle next), Is.True);
            Assert.That(next, Is.EqualTo(second));
        }
    }
}
