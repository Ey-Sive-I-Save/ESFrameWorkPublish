using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    /// <summary>
    /// Executes one user-triggered multi-target SerializedObject mutation as an isolated Undo group.
    /// Callers still own property resolution and validation; this helper only owns commit and rollback.
    /// </summary>
    internal static class ESEditorSerializedMutation
    {
        internal static bool TryApply<T>(
            IReadOnlyList<T> entries,
            string undoName,
            Func<T, UnityEngine.Object> getTarget,
            Func<T, SerializedObject> getSerializedObject,
            Action<T, int> mutate,
            Action refreshView,
            out string error)
        {
            error = null;
            if (entries == null || entries.Count == 0)
            {
                error = "没有可写入的序列化目标。";
                return false;
            }
            if (getTarget == null || getSerializedObject == null || mutate == null)
            {
                error = "序列化批量写入缺少必要回调。";
                return false;
            }

            var undoTargets = new UnityEngine.Object[entries.Count];
            var serializedObjects = new SerializedObject[entries.Count];
            try
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    UnityEngine.Object target = getTarget(entries[index]);
                    SerializedObject serializedObject = getSerializedObject(entries[index]);
                    if (target == null || serializedObject == null)
                    {
                        error = "第 " + (index + 1) + " 个序列化目标已经失效。";
                        return false;
                    }
                    undoTargets[index] = target;
                    serializedObjects[index] = serializedObject;
                }
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + "：" + exception.Message;
                return false;
            }

            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(undoName);
                Undo.RegisterCompleteObjectUndo(undoTargets, undoName);

                for (int index = 0; index < entries.Count; index++)
                {
                    T entry = entries[index];
                    mutate(entry, index);
                    serializedObjects[index].ApplyModifiedProperties();
                }

                for (int index = 0; index < entries.Count; index++)
                {
                    UnityEngine.Object target = undoTargets[index];
                    EditorUtility.SetDirty(target);
                    if (PrefabUtility.IsPartOfPrefabInstance(target))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }

                refreshView?.Invoke();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                string rollbackFailure = null;
                if (undoGroup >= 0)
                {
                    try
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackFailure = rollbackException.GetType().Name + "：" + rollbackException.Message;
                    }
                }

                string refreshFailure = RefreshAfterFailure(serializedObjects, refreshView);
                error = exception.GetType().Name + "：" + exception.Message;
                if (!string.IsNullOrEmpty(rollbackFailure))
                    error += "\n回滚失败，部分目标可能仍已修改：" + rollbackFailure;
                if (!string.IsNullOrEmpty(refreshFailure))
                    error += "\n回滚后的视图同步失败：" + refreshFailure;
                return false;
            }
        }

        private static string RefreshAfterFailure(
            IReadOnlyList<SerializedObject> serializedObjects,
            Action refreshView)
        {
            string firstFailure = null;
            for (int index = 0; index < serializedObjects.Count; index++)
            {
                try
                {
                    serializedObjects[index]?.Update();
                }
                catch (Exception exception)
                {
                    if (firstFailure == null)
                        firstFailure = exception.GetType().Name + "：" + exception.Message;
                }
            }

            try
            {
                refreshView?.Invoke();
            }
            catch (Exception exception)
            {
                if (firstFailure == null)
                    firstFailure = exception.GetType().Name + "：" + exception.Message;
            }
            return firstFailure;
        }
    }
}
