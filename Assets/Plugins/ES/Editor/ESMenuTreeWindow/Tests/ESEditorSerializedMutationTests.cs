using System;
using System.Collections.Generic;
using ES.EditorInternal;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ES.Tests
{
    public sealed class ESEditorSerializedMutationTarget : ScriptableObject
    {
        public int Value;
    }

    public sealed class ESEditorSerializedMutationTests
    {
        private readonly List<MutationEntry> entries = new List<MutationEntry>();

        [SetUp]
        public void SetUp()
        {
            entries.Add(CreateEntry(3));
            entries.Add(CreateEntry(7));
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            for (int index = 0; index < entries.Count; index++)
            {
                entries[index].SerializedObject.Dispose();
                UnityEngine.Object.DestroyImmediate(entries[index].Target);
            }
            entries.Clear();
        }

        [Test]
        public void TryApply_CommitsAllTargetsInOneUndoGroup()
        {
            bool changed = ESEditorSerializedMutation.TryApply(
                entries,
                "测试批量序列化写入",
                entry => entry.Target,
                entry => entry.SerializedObject,
                (entry, index) => entry.ValueProperty.intValue = 20 + index,
                null,
                out string error);

            Assert.That(changed, Is.True, error);
            Assert.That(entries[0].Target.Value, Is.EqualTo(20));
            Assert.That(entries[1].Target.Value, Is.EqualTo(21));

            Undo.PerformUndo();
            entries[0].SerializedObject.Update();
            entries[1].SerializedObject.Update();
            Assert.That(entries[0].Target.Value, Is.EqualTo(3));
            Assert.That(entries[1].Target.Value, Is.EqualTo(7));
        }

        [Test]
        public void TryApply_WhenLaterTargetFails_RollsBackAppliedAndPendingValues()
        {
            int refreshCount = 0;
            bool changed = ESEditorSerializedMutation.TryApply(
                entries,
                "测试批量序列化回滚",
                entry => entry.Target,
                entry => entry.SerializedObject,
                (entry, index) =>
                {
                    entry.ValueProperty.intValue = 30 + index;
                    if (index == 1)
                        throw new InvalidOperationException("预期的第二目标失败");
                },
                () => refreshCount++,
                out string error);

            Assert.That(changed, Is.False);
            Assert.That(error, Does.Contain("InvalidOperationException"));
            Assert.That(error, Does.Contain("预期的第二目标失败"));
            Assert.That(error, Does.Not.Contain("回滚失败"));
            Assert.That(entries[0].Target.Value, Is.EqualTo(3));
            Assert.That(entries[1].Target.Value, Is.EqualTo(7));
            Assert.That(entries[0].ValueProperty.intValue, Is.EqualTo(3));
            Assert.That(entries[1].ValueProperty.intValue, Is.EqualTo(7));
            Assert.That(refreshCount, Is.EqualTo(1));
        }

        [Test]
        public void TryApply_WhenRefreshFails_RollsBackCommittedValues()
        {
            int refreshCount = 0;
            bool changed = ESEditorSerializedMutation.TryApply(
                entries,
                "测试刷新失败回滚",
                entry => entry.Target,
                entry => entry.SerializedObject,
                (entry, index) => entry.ValueProperty.intValue = 40 + index,
                () =>
                {
                    refreshCount++;
                    throw new InvalidOperationException("预期的视图刷新失败");
                },
                out string error);

            Assert.That(changed, Is.False);
            Assert.That(error, Does.Contain("预期的视图刷新失败"));
            Assert.That(error, Does.Contain("回滚后的视图同步失败"));
            Assert.That(entries[0].Target.Value, Is.EqualTo(3));
            Assert.That(entries[1].Target.Value, Is.EqualTo(7));
            Assert.That(entries[0].ValueProperty.intValue, Is.EqualTo(3));
            Assert.That(entries[1].ValueProperty.intValue, Is.EqualTo(7));
            Assert.That(refreshCount, Is.EqualTo(2));
        }

        [Test]
        public void TryApply_RejectsSerializedObjectBoundToAnotherTarget()
        {
            ESEditorSerializedMutationTarget foreignTarget =
                ScriptableObject.CreateInstance<ESEditorSerializedMutationTarget>();
            SerializedObject foreignSerializedObject = new SerializedObject(foreignTarget);
            try
            {
                bool changed = ESEditorSerializedMutation.TryApply(
                    new[] { entries[0] },
                    "测试拒绝错误序列化目标",
                    entry => entry.Target,
                    entry => foreignSerializedObject,
                    (entry, index) => foreignSerializedObject.FindProperty(nameof(ESEditorSerializedMutationTarget.Value)).intValue = 99,
                    null,
                    out string error);

                Assert.That(changed, Is.False);
                Assert.That(error, Does.Contain("未绑定对应目标"));
                Assert.That(entries[0].Target.Value, Is.EqualTo(3));
                Assert.That(foreignTarget.Value, Is.EqualTo(0));
            }
            finally
            {
                foreignSerializedObject.Dispose();
                UnityEngine.Object.DestroyImmediate(foreignTarget);
            }
        }

        private static MutationEntry CreateEntry(int value)
        {
            ESEditorSerializedMutationTarget target =
                ScriptableObject.CreateInstance<ESEditorSerializedMutationTarget>();
            target.Value = value;
            var serializedObject = new SerializedObject(target);
            return new MutationEntry(
                target,
                serializedObject,
                serializedObject.FindProperty(nameof(ESEditorSerializedMutationTarget.Value)));
        }

        private readonly struct MutationEntry
        {
            public readonly ESEditorSerializedMutationTarget Target;
            public readonly SerializedObject SerializedObject;
            public readonly SerializedProperty ValueProperty;

            public MutationEntry(
                ESEditorSerializedMutationTarget target,
                SerializedObject serializedObject,
                SerializedProperty valueProperty)
            {
                Target = target;
                SerializedObject = serializedObject;
                ValueProperty = valueProperty;
            }
        }

    }
}
