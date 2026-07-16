#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        #region Serial Child Row Synchronization
        private static void AddImportedChildKey(Dictionary<ScriptableObject, HashSet<string>> importedChildKeysByOwner, ScriptableObject owner, string rowKey)
        {
            if (importedChildKeysByOwner == null || owner == null || string.IsNullOrWhiteSpace(rowKey))
                return;

            if (!importedChildKeysByOwner.TryGetValue(owner, out HashSet<string> keys))
            {
                keys = new HashSet<string>(StringComparer.Ordinal);
                importedChildKeysByOwner[owner] = keys;
            }

            keys.Add(rowKey);
        }

        private static void AddKeylessChildCount(Dictionary<ScriptableObject, int> keylessChildCountByOwner, ScriptableObject owner)
        {
            if (keylessChildCountByOwner == null || owner == null)
                return;

            keylessChildCountByOwner.TryGetValue(owner, out int count);
            keylessChildCountByOwner[owner] = count + 1;
        }

        private int PruneMissingChildRows(Dictionary<ScriptableObject, HashSet<string>> importedChildKeysByOwner, Dictionary<ScriptableObject, int> keylessChildCountByOwner = null)
        {
            if (importedChildKeysByOwner == null || importedChildKeysByOwner.Count == 0)
                return 0;

            int changedCount = 0;
            foreach (KeyValuePair<ScriptableObject, HashSet<string>> pair in importedChildKeysByOwner)
            {
                ScriptableObject owner = pair.Key;
                HashSet<string> keepKeys = pair.Value;
                if (owner == null || keepKeys == null)
                    continue;
                if (keylessChildCountByOwner != null && keylessChildCountByOwner.ContainsKey(owner))
                    continue;

                int removed = PruneMissingChildRows(owner, keepKeys);
                if (removed > 0)
                {
                    EditorUtility.SetDirty(owner);
                    changedCount += removed;
                }
            }

            return changedCount;
        }

        private int PruneKeylessChildRowsByCount(Dictionary<ScriptableObject, int> keylessChildCountByOwner)
        {
            if (keylessChildCountByOwner == null || keylessChildCountByOwner.Count == 0)
                return 0;

            int changedCount = 0;
            foreach (KeyValuePair<ScriptableObject, int> pair in keylessChildCountByOwner)
            {
                ScriptableObject owner = pair.Key;
                int keepCount = pair.Value;
                if (owner == null || keepCount < 0 || rowBinding == null || !rowBinding.IsListElementRow)
                    continue;

                if (RowBridge.EnsureDictionary(owner, rowBinding) != null)
                    continue;

                IList list = RowBridge.EnsureContainer(owner, rowBinding);
                if (list == null || list.Count <= keepCount)
                    continue;

                Undo.RecordObject(owner, "SO Table Prune Keyless Child Rows");
                int removed = 0;
                for (int i = list.Count - 1; i >= keepCount; i--)
                {
                    list.RemoveAt(i);
                    removed++;
                }

                if (removed > 0)
                {
                    EditorUtility.SetDirty(owner);
                    changedCount += removed;
                }
            }

            return changedCount;
        }

        private int PruneMissingChildRows(ScriptableObject owner, HashSet<string> keepKeys)
        {
            if (owner == null || keepKeys == null || rowBinding == null || !rowBinding.IsListElementRow)
                return 0;

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
            {
                var removeKeys = new List<object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    string key = entry.Key != null ? entry.Key.ToString() : string.Empty;
                    if (!keepKeys.Contains(key))
                        removeKeys.Add(entry.Key);
                }

                if (removeKeys.Count == 0)
                    return 0;

                Undo.RecordObject(owner, "SO Table Prune Child Rows");
                for (int i = 0; i < removeKeys.Count; i++)
                    dictionary.Remove(removeKeys[i]);
                return removeKeys.Count;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
                return 0;

            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                object element = list[i];
                string key = ESRowBindingReflectionUtility.GetMemberValue(element, rowBinding.elementKeyFieldPath)?.ToString();
                if (string.IsNullOrWhiteSpace(key) || keepKeys.Contains(key))
                    continue;

                if (removed == 0)
                    Undo.RecordObject(owner, "SO Table Prune Child Rows");
                list.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private bool EnsureChildRowsClearedForRebuild(ScriptableObject owner, HashSet<ScriptableObject> rebuiltOwners, out string reason)
        {
            reason = string.Empty;
            if (owner == null)
            {
                reason = "宿主 SO 为空。";
                return false;
            }
            if (rebuiltOwners == null)
                return true;
            if (rebuiltOwners.Contains(owner))
                return true;
            if (rowBinding == null || !rowBinding.IsListElementRow)
            {
                reason = "当前规则不是子级行模式。";
                return false;
            }

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
            {
                Undo.RecordObject(owner, "SO Table Rebuild Child Rows");
                dictionary.Clear();
                rebuiltOwners.Add(owner);
                return true;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "无法获取子级 List：" + rowBinding.listFieldPath;
                return false;
            }

            Undo.RecordObject(owner, "SO Table Rebuild Child Rows");
            list.Clear();
            rebuiltOwners.Add(owner);
            return true;
        }

        private bool TryCreateAppendedChildRow(ScriptableObject owner, out object rowObject, out string reason)
        {
            rowObject = null;
            reason = string.Empty;
            if (owner == null)
            {
                reason = "宿主 SO 为空。";
                return false;
            }
            if (rowBinding == null || !rowBinding.IsListElementRow)
            {
                reason = "当前规则不是子级行模式。";
                return false;
            }
            if (RowBridge.EnsureDictionary(owner, rowBinding) != null)
            {
                reason = "Dictionary 子级不能使用空 Key；请填写 Key，或改用 List 子级。";
                return false;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "无法获取子级 List：" + rowBinding.listFieldPath;
                return false;
            }

            Type elementType = ESRowBindingReflectionUtility.GetListElementType(list.GetType());
            if (elementType == null)
            {
                reason = "无法推导 List 元素类型：" + rowBinding.listFieldPath;
                return false;
            }

            rowObject = Activator.CreateInstance(elementType);
            if (!string.IsNullOrWhiteSpace(rowBinding.elementKeyFieldPath))
                ESRowBindingReflectionUtility.SetMemberValue(rowObject, rowBinding.elementKeyFieldPath, string.Empty);
            list.Add(rowObject);
            return true;
        }

        private bool CanImportEmptyChildKeyForRebuild(ScriptableObject owner)
        {
            if (owner == null || rowBinding == null || !rowBinding.IsListElementRow || !rowBinding.allowEmptyRowKey)
                return false;
            if (GetActiveSerialChildImportSyncMode() != ESTableSerialChildImportSyncMode.RebuildTouchedOwners)
                return false;

            return RowBridge.EnsureDictionary(owner, rowBinding) == null;
        }

        private bool CanImportEmptyChildKeyByOrder(ScriptableObject owner)
        {
            if (owner == null || rowBinding == null || !rowBinding.IsListElementRow || !rowBinding.allowEmptyRowKey)
                return false;

            return RowBridge.EnsureDictionary(owner, rowBinding) == null;
        }

        private bool TryGetOrCreateKeylessChildRowByOrder(ScriptableObject owner, Dictionary<ScriptableObject, int> keylessChildCursorByOwner, out object rowObject, out string reason)
        {
            rowObject = null;
            reason = string.Empty;
            if (owner == null)
            {
                reason = "Owner SO is null.";
                return false;
            }
            if (rowBinding == null || !rowBinding.IsListElementRow || !rowBinding.allowEmptyRowKey)
            {
                reason = "Current rule does not allow keyless child rows.";
                return false;
            }
            if (RowBridge.EnsureDictionary(owner, rowBinding) != null)
            {
                reason = "Dictionary child rows require a key.";
                return false;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "Cannot get child List: " + rowBinding.listFieldPath;
                return false;
            }

            int index = 0;
            if (keylessChildCursorByOwner != null)
                keylessChildCursorByOwner.TryGetValue(owner, out index);

            if (index < list.Count)
            {
                rowObject = list[index];
            }
            else
            {
                Type elementType = ESRowBindingReflectionUtility.GetListElementType(list.GetType());
                if (elementType == null)
                {
                    reason = "Cannot infer List element type: " + rowBinding.listFieldPath;
                    return false;
                }

                rowObject = Activator.CreateInstance(elementType);
                if (!string.IsNullOrWhiteSpace(rowBinding.elementKeyFieldPath))
                    ESRowBindingReflectionUtility.SetMemberValue(rowObject, rowBinding.elementKeyFieldPath, string.Empty);
                list.Add(rowObject);
            }

            if (keylessChildCursorByOwner != null)
                keylessChildCursorByOwner[owner] = index + 1;
            return true;
        }

        private bool TryDeleteKeylessChildRowByOrder(ScriptableObject owner, Dictionary<ScriptableObject, int> keylessChildCursorByOwner, out string reason)
        {
            reason = string.Empty;
            if (owner == null)
            {
                reason = "Owner SO is null.";
                return false;
            }
            if (rowBinding == null || !rowBinding.IsListElementRow || !rowBinding.allowEmptyRowKey)
            {
                reason = "Current rule does not allow keyless child rows.";
                return false;
            }
            if (RowBridge.EnsureDictionary(owner, rowBinding) != null)
            {
                reason = "Dictionary child rows require a key.";
                return false;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "Cannot get child List: " + rowBinding.listFieldPath;
                return false;
            }

            int index = 0;
            if (keylessChildCursorByOwner != null)
                keylessChildCursorByOwner.TryGetValue(owner, out index);

            if (index < 0 || index >= list.Count)
            {
                reason = "Keyless child index is out of range: " + index;
                return false;
            }

            Undo.RecordObject(owner, "SO Table Delete Keyless Child Row");
            list.RemoveAt(index);
            return true;
        }

        private bool TryGetOrCreateChildRowCached(ScriptableObject owner, string rowKey, Dictionary<ScriptableObject, Dictionary<string, object>> childRowsByOwner, out object rowObject, out string reason)
        {
            rowObject = null;
            reason = string.Empty;

            if (owner == null)
            {
                reason = "Owner SO is null.";
                return false;
            }
            if (rowBinding == null || !rowBinding.IsListElementRow)
            {
                reason = "Current rule is not in child row mode.";
                return false;
            }
            if (!RowBridge.IsValidRowKey(rowKey, rowBinding, out reason))
                return false;

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
                return RowBridge.TryGetOrCreateRow(owner, rowKey, rowBinding, out rowObject, out reason);

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "Cannot get child List: " + rowBinding.listFieldPath;
                return false;
            }

            Dictionary<string, object> rowsByKey = GetOrBuildChildRowCache(owner, list, childRowsByOwner);
            if (!string.IsNullOrWhiteSpace(rowKey) && rowsByKey.TryGetValue(rowKey, out rowObject))
                return true;

            if (!rowBinding.createMissingElement)
            {
                reason = "Child row not found: " + rowKey;
                return false;
            }

            Type elementType = ESRowBindingReflectionUtility.GetListElementType(list.GetType());
            if (elementType == null)
            {
                reason = "Cannot infer List element type: " + rowBinding.listFieldPath;
                return false;
            }

            rowObject = Activator.CreateInstance(elementType);
            ESRowBindingReflectionUtility.SetMemberValue(rowObject, rowBinding.elementKeyFieldPath, rowKey);
            list.Add(rowObject);
            if (!string.IsNullOrWhiteSpace(rowKey))
                rowsByKey[rowKey] = rowObject;
            return true;
        }

        private Dictionary<string, object> GetOrBuildChildRowCache(ScriptableObject owner, IList list, Dictionary<ScriptableObject, Dictionary<string, object>> childRowsByOwner)
        {
            if (childRowsByOwner == null)
                return BuildChildRowCache(list);

            if (childRowsByOwner.TryGetValue(owner, out Dictionary<string, object> cache))
                return cache;

            cache = BuildChildRowCache(list);
            childRowsByOwner[owner] = cache;
            return cache;
        }

        private Dictionary<string, object> BuildChildRowCache(IList list)
        {
            var cache = new Dictionary<string, object>(StringComparer.Ordinal);
            if (list == null || rowBinding == null)
                return cache;

            for (int i = 0; i < list.Count; i++)
            {
                object element = list[i];
                string key = ESRowBindingReflectionUtility.GetMemberValue(element, rowBinding.elementKeyFieldPath)?.ToString();
                if (!string.IsNullOrWhiteSpace(key) && !cache.ContainsKey(key))
                    cache[key] = element;
            }

            return cache;
        }

        private static void RemoveCachedChildRow(Dictionary<ScriptableObject, Dictionary<string, object>> childRowsByOwner, ScriptableObject owner, string rowKey)
        {
            if (childRowsByOwner == null || owner == null || string.IsNullOrWhiteSpace(rowKey))
                return;

            if (childRowsByOwner.TryGetValue(owner, out Dictionary<string, object> cache))
                cache.Remove(rowKey);
        }

        private bool TryDeleteChildRow(ScriptableObject owner, string rowKey, out string reason)
        {
            reason = string.Empty;
            if (owner == null)
            {
                reason = "宿主 SO 为空。";
                return false;
            }
            if (rowBinding == null || !rowBinding.IsListElementRow)
            {
                reason = "当前规则不是 List 子级行。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(rowKey))
            {
                reason = "子级 Key 为空。";
                return false;
            }

            IDictionary dictionary = RowBridge.EnsureDictionary(owner, rowBinding);
            if (dictionary != null)
            {
                if (!dictionary.Contains(rowKey))
                {
                    reason = "找不到子级 Key：" + rowKey;
                    return false;
                }

                Undo.RecordObject(owner, "SO Table Delete Child Row");
                dictionary.Remove(rowKey);
                return true;
            }

            IList list = RowBridge.EnsureContainer(owner, rowBinding);
            if (list == null)
            {
                reason = "无法获取子级容器：" + rowBinding.listFieldPath;
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                object element = list[i];
                string elementKey = ESRowBindingReflectionUtility.GetMemberValue(element, rowBinding.elementKeyFieldPath)?.ToString();
                if (string.Equals(elementKey, rowKey, StringComparison.Ordinal))
                {
                    Undo.RecordObject(owner, "SO Table Delete Child Row");
                    list.RemoveAt(i);
                    return true;
                }
            }

            reason = "找不到子级 Key：" + rowKey;
            return false;
        }

        #endregion
    }
}
#endif
