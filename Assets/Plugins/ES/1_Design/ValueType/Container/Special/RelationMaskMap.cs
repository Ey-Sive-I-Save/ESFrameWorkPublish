using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.Utilities;

namespace ES
{
    /// <summary>
    /// 关系遮罩容器（字符串版）
    /// </summary>
    [Serializable]
    public sealed class RelationMaskStringMap : ISerializationCallbackReceiver
    {
        public const int MaxKeys = 64;

        [Serializable]
        public struct RelationEntry
        {
            public string key;
            public List<string> relatedKeys;
        }

        [SerializeField, OnValueChanged("EditorBuildMatrix", true)]
        private List<string> baseKeys = new List<string>(8);
        [SerializeField, OnValueChanged("EditorBuildMatrix", true)]
        private List<RelationEntry> relations = new List<RelationEntry>(8);

        [NonSerialized] private Dictionary<string, int> _index;
        [NonSerialized] private ulong[] _masks;
        [NonSerialized] private bool _cacheReady;

#if UNITY_EDITOR
        [TableMatrix(SquareCells = true, Labels = "GetMatrixLabel")]
        [OnValueChanged("EditorApplyMatrix")]
        [ShowInInspector, PropertyOrder(100)]
        private bool[,] editorMatrix;

        [NonSerialized] private bool _suppressMatrixApply;

        [Button("从关系生成矩阵"), PropertyOrder(101)]
        private void EditorBuildMatrix()
        {
            BuildEditorMatrix();
        }

        [Button("从矩阵应用关系"), PropertyOrder(102)]
        private void EditorApplyMatrix()
        {
            ApplyEditorMatrix();
        }

        [OnInspectorInit]
        private void EditorInitMatrix()
        {
            if (editorMatrix == null)
            {
                BuildEditorMatrix();
            }
        }
#endif

        public IReadOnlyList<string> BaseKeys => baseKeys;
        public IReadOnlyList<RelationEntry> Relations => relations;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedOneWay(string fromKey, string toKey)
        {
            return IsRelated(fromKey, toKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedTwoWay(string a, string b)
        {
            return IsRelated(a, b) && IsRelated(b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedFast(string fromKey, string toKey)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (fromKey == null || toKey == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            if (!_index.TryGetValue(toKey, out var toIndex)) return false;
            return ((_masks[fromIndex] >> toIndex) & 1UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast(string fromKey, ulong targetMask)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (fromKey == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            return (_masks[fromIndex] & targetMask) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast(string key, out ulong mask)
        {
            if (!_cacheReady || _index == null || _masks == null || key == null)
            {
                mask = 0UL;
                return false;
            }

            if (_index.TryGetValue(key, out var index))
            {
                mask = _masks[index];
                return true;
            }

            mask = 0UL;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetIndex(string key, out int index)
        {
            EnsureCache();
            if (_index == null || key == null)
            {
                index = -1;
                return false;
            }

            return _index.TryGetValue(key, out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelated(string fromKey, string toKey)
        {
            return TryGetIndex(fromKey, out var fromIndex) && TryGetIndex(toKey, out var toIndex) && IsRelatedByIndex(fromIndex, toIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedByIndex(int fromIndex, int toIndex)
        {
            EnsureCache();
            if (_masks == null) return false;
            if ((uint)fromIndex >= (uint)_masks.Length) return false;
            if ((uint)toIndex >= (uint)_masks.Length) return false;
            return ((_masks[fromIndex] >> toIndex) & 1UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetMaskByIndex(int index)
        {
            EnsureCache();
            if (_masks == null) return 0UL;
            if ((uint)index >= (uint)_masks.Length) return 0UL;
            return _masks[index];
        }

        public void RebuildCache()
        {
            _cacheReady = true;
            int count = baseKeys != null ? baseKeys.Count : 0;
#if UNITY_EDITOR
            if (count > MaxKeys)
            {
                Debug.LogWarning($"[RelationMaskStringMap] 基准数量超出{MaxKeys}，仅使用前{MaxKeys}个。");
            }
#endif
            if (count > MaxKeys) count = MaxKeys;

            _index = count > 0 ? new Dictionary<string, int>(count) : null;
            _masks = count > 0 ? new ulong[count] : null;

            for (int i = 0; i < count; i++)
            {
                var key = baseKeys[i] ?? string.Empty;
                _index[key] = i;
            }

            if (relations == null || relations.Count == 0 || _index == null) return;

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                if (!_index.TryGetValue(entry.key ?? string.Empty, out var fromIndex))
                    continue;

                ulong mask = 0UL;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        var rk = related[r] ?? string.Empty;
                        if (_index.TryGetValue(rk, out var toIndex))
                        {
                            if ((uint)toIndex < MaxKeys)
                                mask |= 1UL << toIndex;
                        }
                    }
                }

                _masks[fromIndex] = mask;
            }
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _cacheReady = false;
            RebuildCache();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCache()
        {
            if (_cacheReady) return;
            RebuildCache();
        }

#if UNITY_EDITOR
        private void BuildEditorMatrix()
        {
            _suppressMatrixApply = true;
            RebuildCache();
            int count = baseKeys.Count;
            if (count > MaxKeys) count = MaxKeys;
            editorMatrix = new bool[count, count];

            for (int r = 0; r < count; r++)
            {
                for (int c = 0; c < count; c++)
                {
                    editorMatrix[r, c] = IsRelatedByIndex(r, c);
                }
            }

            _suppressMatrixApply = false;
        }

        private void ApplyEditorMatrix()
        {
            if (_suppressMatrixApply) return;
            int count = baseKeys.Count;
            if (count > MaxKeys) count = MaxKeys;
            if (editorMatrix == null || editorMatrix.GetLength(0) != count || editorMatrix.GetLength(1) != count)
            {
                BuildEditorMatrix();
                return;
            }

            relations.Clear();
            for (int r = 0; r < count; r++)
            {
                var entry = new RelationEntry
                {
                    key = baseKeys[r],
                    relatedKeys = new List<string>()
                };

                for (int c = 0; c < count; c++)
                {
                    if (editorMatrix[r, c])
                    {
                        entry.relatedKeys.Add(baseKeys[c]);
                    }
                }

                relations.Add(entry);
            }

            _cacheReady = false;
        }

        private ValueTuple<string, LabelDirection> GetMatrixLabel(TableAxis axis, int index)
        {
            int count = baseKeys != null ? baseKeys.Count : 0;
            if (count > MaxKeys) count = MaxKeys;
            if (count <= 0) return ValueTuple.Create(string.Empty, LabelDirection.LeftToRight);

            if ((uint)index >= (uint)count)
            {
                return ValueTuple.Create(string.Empty, LabelDirection.LeftToRight);
            }

            string label = baseKeys[index] ?? string.Empty;
            return ValueTuple.Create(label, LabelDirection.LeftToRight);
        }
#endif
    }

    /// <summary>
    /// 关系遮罩容器（整数版）
    /// </summary>
    [Serializable]
    public sealed class RelationMaskIntMap : ISerializationCallbackReceiver
    {
        public const int MaxKeys = 64;

        [Serializable]
        public struct RelationEntry
        {
            public int key;
            public List<int> relatedKeys;
        }

        [SerializeField, OnValueChanged("EditorBuildMatrix", true)]
        private List<int> baseKeys = new List<int>(8);
        [SerializeField, OnValueChanged("EditorBuildMatrix", true)]
        private List<RelationEntry> relations = new List<RelationEntry>(8);

        [NonSerialized] private Dictionary<int, int> _index;
        [NonSerialized] private ulong[] _masks;
        [NonSerialized] private bool _cacheReady;

#if UNITY_EDITOR
        [TableMatrix(SquareCells = true, Labels = "GetMatrixLabel")]
        [OnValueChanged("EditorApplyMatrix")]
        [ShowInInspector, PropertyOrder(100)]
        private bool[,] editorMatrix;

        [NonSerialized] private bool _suppressMatrixApply;

        [Button("从关系生成矩阵"), PropertyOrder(101)]
        private void EditorBuildMatrix()
        {
            BuildEditorMatrix();
        }

        [Button("从矩阵应用关系"), PropertyOrder(102)]
        private void EditorApplyMatrix()
        {
            ApplyEditorMatrix();
        }

        [OnInspectorInit]
        private void EditorInitMatrix()
        {
            if (editorMatrix == null)
            {
                BuildEditorMatrix();
            }
        }
#endif

        public IReadOnlyList<int> BaseKeys => baseKeys;
        public IReadOnlyList<RelationEntry> Relations => relations;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedOneWay(int fromKey, int toKey)
        {
            return IsRelated(fromKey, toKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedOneWay<TEnum>(TEnum fromKey, TEnum toKey) where TEnum : struct, Enum
        {
            return IsRelated(fromKey, toKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedTwoWay(int a, int b)
        {
            return IsRelated(a, b) && IsRelated(b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedTwoWay<TEnum>(TEnum a, TEnum b) where TEnum : struct, Enum
        {
            return IsRelated(a, b) && IsRelated(b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedFast(int fromKey, int toKey)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            if (!_index.TryGetValue(toKey, out var toIndex)) return false;
            return ((_masks[fromIndex] >> toIndex) & 1UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast(int fromKey, ulong targetMask)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            return (_masks[fromIndex] & targetMask) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast<TEnum>(TEnum fromKey, ulong targetMask) where TEnum : struct, Enum
        {
            return IsRelatedMaskFast(Convert.ToInt32(fromKey), targetMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedFast<TEnum>(TEnum fromKey, TEnum toKey) where TEnum : struct, Enum
        {
            return IsRelatedFast(Convert.ToInt32(fromKey), Convert.ToInt32(toKey));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast(int key, out ulong mask)
        {
            if (!_cacheReady || _index == null || _masks == null)
            {
                mask = 0UL;
                return false;
            }

            if (_index.TryGetValue(key, out var index))
            {
                mask = _masks[index];
                return true;
            }

            mask = 0UL;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast<TEnum>(TEnum key, out ulong mask) where TEnum : struct, Enum
        {
            return TryGetMaskFast(Convert.ToInt32(key), out mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetIndex<TEnum>(TEnum key, out int index) where TEnum : struct, Enum
        {
            return TryGetIndex(Convert.ToInt32(key), out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetIndex(int key, out int index)
        {
            EnsureCache();
            if (_index == null)
            {
                index = -1;
                return false;
            }

            return _index.TryGetValue(key, out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelated(int fromKey, int toKey)
        {
            return TryGetIndex(fromKey, out var fromIndex) && TryGetIndex(toKey, out var toIndex) && IsRelatedByIndex(fromIndex, toIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelated<TEnum>(TEnum fromKey, TEnum toKey) where TEnum : struct, Enum
        {
            return IsRelated(Convert.ToInt32(fromKey), Convert.ToInt32(toKey));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetMask<TEnum>(TEnum key) where TEnum : struct, Enum
        {
            return TryGetIndex(Convert.ToInt32(key), out var index) ? GetMaskByIndex(index) : 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndexOrMinusOne<TEnum>(TEnum key) where TEnum : struct, Enum
        {
            return TryGetIndex(Convert.ToInt32(key), out var index) ? index : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedByIndex(int fromIndex, int toIndex)
        {
            EnsureCache();
            if (_masks == null) return false;
            if ((uint)fromIndex >= (uint)_masks.Length) return false;
            if ((uint)toIndex >= (uint)_masks.Length) return false;
            return ((_masks[fromIndex] >> toIndex) & 1UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetMaskByIndex(int index)
        {
            EnsureCache();
            if (_masks == null) return 0UL;
            if ((uint)index >= (uint)_masks.Length) return 0UL;
            return _masks[index];
        }

        public void RebuildCache()
        {
            _cacheReady = true;
            int count = baseKeys != null ? baseKeys.Count : 0;
#if UNITY_EDITOR
            if (count > MaxKeys)
            {
                Debug.LogWarning($"[RelationMaskIntMap] 基准数量超出{MaxKeys}，仅使用前{MaxKeys}个。");
            }
#endif
            if (count > MaxKeys) count = MaxKeys;

            _index = count > 0 ? new Dictionary<int, int>(count) : null;
            _masks = count > 0 ? new ulong[count] : null;

            for (int i = 0; i < count; i++)
            {
                _index[baseKeys[i]] = i;
            }

            if (relations == null || relations.Count == 0 || _index == null) return;

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                if (!_index.TryGetValue(entry.key, out var fromIndex))
                    continue;

                ulong mask = 0UL;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        if (_index.TryGetValue(related[r], out var toIndex))
                        {
                            if ((uint)toIndex < MaxKeys)
                                mask |= 1UL << toIndex;
                        }
                    }
                }

                _masks[fromIndex] = mask;
            }
        }

        /// <summary>
        /// 使用枚举生成基准键列表（会清空原有 baseKeys）
        /// </summary>
        public void SetBaseKeysFromEnum<TEnum>() where TEnum : struct, Enum
        {
            if (baseKeys == null)
            {
                baseKeys = new List<int>();
            }

            baseKeys.Clear();
            var values = Enum.GetValues(typeof(TEnum));
            for (int i = 0; i < values.Length; i++)
            {
                baseKeys.Add(Convert.ToInt32(values.GetValue(i)));
            }

            _cacheReady = false;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _cacheReady = false;
            RebuildCache();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCache()
        {
            if (_cacheReady) return;
            RebuildCache();
        }

#if UNITY_EDITOR
        private void BuildEditorMatrix()
        {
            _suppressMatrixApply = true;
            RebuildCache();
            int count = baseKeys.Count;
            if (count > MaxKeys) count = MaxKeys;
            editorMatrix = new bool[count, count];

            for (int r = 0; r < count; r++)
            {
                for (int c = 0; c < count; c++)
                {
                    editorMatrix[r, c] = IsRelatedByIndex(r, c);
                }
            }

            _suppressMatrixApply = false;
        }

        private void ApplyEditorMatrix()
        {
            if (_suppressMatrixApply) return;
            int count = baseKeys.Count;
            if (count > MaxKeys) count = MaxKeys;
            if (editorMatrix == null || editorMatrix.GetLength(0) != count || editorMatrix.GetLength(1) != count)
            {
                BuildEditorMatrix();
                return;
            }

            relations.Clear();
            for (int r = 0; r < count; r++)
            {
                var entry = new RelationEntry
                {
                    key = baseKeys[r],
                    relatedKeys = new List<int>()
                };

                for (int c = 0; c < count; c++)
                {
                    if (editorMatrix[r, c])
                    {
                        entry.relatedKeys.Add(baseKeys[c]);
                    }
                }

                relations.Add(entry);
            }

            _cacheReady = false;
        }

        private ValueTuple<string, LabelDirection> GetMatrixLabel(TableAxis axis, int index)
        {
            int count = baseKeys != null ? baseKeys.Count : 0;
            if (count > MaxKeys) count = MaxKeys;
            if (count <= 0) return ValueTuple.Create(string.Empty, LabelDirection.LeftToRight);

            if ((uint)index >= (uint)count)
            {
                return ValueTuple.Create(string.Empty, LabelDirection.LeftToRight);
            }

            string label = baseKeys[index].ToString();
            return ValueTuple.Create(label, LabelDirection.LeftToRight);
        }
#endif
    }

    /// <summary>
    /// 关系遮罩容器（枚举版）
    /// </summary>
    [Serializable]
    public class RelationMaskEnumMap<TEnum> : ISerializationCallbackReceiver where TEnum : struct, Enum
    {
        public const int MaxKeys = 64;

        [Serializable]
        public struct RelationEntry
        {
            public TEnum key;
            public List<TEnum> relatedKeys;
        }

        [SerializeField, OnValueChanged("EditorBuildMatrix", true)]
        private List<TEnum> baseKeys = new List<TEnum>(8);
        [SerializeField, OnValueChanged("EditorBuildMatrix", true)]
        private List<RelationEntry> relations = new List<RelationEntry>(8);

        [NonSerialized] private Dictionary<TEnum, int> _index;
        [NonSerialized] private ulong[] _masks;
        [NonSerialized] private bool _cacheReady;

#if UNITY_EDITOR
        [TableMatrix(SquareCells = true, Labels = "GetMatrixLabel")]
        [OnValueChanged("EditorApplyMatrix")]
        [ShowInInspector, PropertyOrder(100)]
        private bool[,] editorMatrix;

        [NonSerialized] private bool _suppressMatrixApply;

        [Button("从关系生成矩阵"), PropertyOrder(101)]
        private void EditorBuildMatrix()
        {
            BuildEditorMatrix();
        }

        [Button("从矩阵应用关系"), PropertyOrder(102)]
        private void EditorApplyMatrix()
        {
            ApplyEditorMatrix();
        }

        [OnInspectorInit]
        private void EditorInitMatrix()
        {
            if (editorMatrix == null)
            {
                BuildEditorMatrix();
            }
        }
#endif

        public IReadOnlyList<TEnum> BaseKeys => baseKeys;
        public IReadOnlyList<RelationEntry> Relations => relations;

        [Button("枚举默认初始化")]
        public void InitEnumDefault()
        {
            SetBaseKeysFromEnum();
            ResetRelationsEmpty();
        }

        public void AddRelation(TEnum fromKey, TEnum toKey)
        {
            if (relations == null)
            {
                relations = new List<RelationEntry>();
            }

            for (int i = 0; i < relations.Count; i++)
            {
                if (EqualityComparer<TEnum>.Default.Equals(relations[i].key, fromKey))
                {
                    var entry = relations[i];
                    if (entry.relatedKeys == null)
                    {
                        entry.relatedKeys = new List<TEnum>();
                    }

                    if (!entry.relatedKeys.Contains(toKey))
                    {
                        entry.relatedKeys.Add(toKey);
                    }

                    relations[i] = entry;

                    _cacheReady = false;
                    RebuildCache();
                    return;
                }
            }

            relations.Add(new RelationEntry
            {
                key = fromKey,
                relatedKeys = new List<TEnum> { toKey }
            });

            _cacheReady = false;
            RebuildCache();
        }

        public void AddRelations(TEnum fromKey, IEnumerable<TEnum> toKeys)
        {
            if (toKeys == null) return;
            if (relations == null)
            {
                relations = new List<RelationEntry>();
            }

            int entryIndex = -1;
            for (int i = 0; i < relations.Count; i++)
            {
                if (EqualityComparer<TEnum>.Default.Equals(relations[i].key, fromKey))
                {
                    entryIndex = i;
                    break;
                }
            }

            if (entryIndex < 0)
            {
                relations.Add(new RelationEntry
                {
                    key = fromKey,
                    relatedKeys = new List<TEnum>()
                });
                entryIndex = relations.Count - 1;
            }

            var targetEntry = relations[entryIndex];
            if (targetEntry.relatedKeys == null)
            {
                targetEntry.relatedKeys = new List<TEnum>();
            }

            foreach (var toKey in toKeys)
            {
                if (!targetEntry.relatedKeys.Contains(toKey))
                {
                    targetEntry.relatedKeys.Add(toKey);
                }
            }

            relations[entryIndex] = targetEntry;

            _cacheReady = false;
            RebuildCache();
        }

        public void ResetRelationsEmpty()
        {
            if (relations == null)
            {
                relations = new List<RelationEntry>();
            }

            relations.Clear();

            int count = baseKeys != null ? baseKeys.Count : 0;
            for (int i = 0; i < count; i++)
            {
                relations.Add(new RelationEntry
                {
                    key = baseKeys[i],
                    relatedKeys = new List<TEnum>()
                });
            }

            _cacheReady = false;
            RebuildCache();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedOneWay(TEnum fromKey, TEnum toKey)
        {
            return IsRelated(fromKey, toKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedTwoWay(TEnum a, TEnum b)
        {
            return IsRelated(a, b) && IsRelated(b, a);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedFast(TEnum fromKey, TEnum toKey)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            if (!_index.TryGetValue(toKey, out var toIndex)) return false;
            return ((_masks[fromIndex] >> toIndex) & 1UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast(TEnum fromKey, ulong targetMask)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            return (_masks[fromIndex] & targetMask) != 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast(TEnum key, out ulong mask)
        {
            if (!_cacheReady || _index == null || _masks == null)
            {
                mask = 0UL;
                return false;
            }

            if (_index.TryGetValue(key, out var index))
            {
                mask = _masks[index];
                return true;
            }

            mask = 0UL;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetIndex(TEnum key, out int index)
        {
            EnsureCache();
            if (_index == null)
            {
                index = -1;
                return false;
            }

            return _index.TryGetValue(key, out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelated(TEnum fromKey, TEnum toKey)
        {
            return TryGetIndex(fromKey, out var fromIndex) && TryGetIndex(toKey, out var toIndex) && IsRelatedByIndex(fromIndex, toIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedByIndex(int fromIndex, int toIndex)
        {
            EnsureCache();
            if (_masks == null) return false;
            if ((uint)fromIndex >= (uint)_masks.Length) return false;
            if ((uint)toIndex >= (uint)_masks.Length) return false;
            return ((_masks[fromIndex] >> toIndex) & 1UL) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetMask(TEnum key)
        {
            return TryGetIndex(key, out var index) ? GetMaskByIndex(index) : 0UL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetMaskByIndex(int index)
        {
            EnsureCache();
            if (_masks == null) return 0UL;
            if ((uint)index >= (uint)_masks.Length) return 0UL;
            return _masks[index];
        }

        public void RebuildCache()
        {
            _cacheReady = true;
            int count = baseKeys != null ? baseKeys.Count : 0;
#if UNITY_EDITOR
            if (count > MaxKeys)
            {
                Debug.LogWarning($"[RelationMaskEnumMap] 基准数量超出{MaxKeys}，仅使用前{MaxKeys}个。");
            }
#endif
            if (count > MaxKeys) count = MaxKeys;

            _index = count > 0 ? new Dictionary<TEnum, int>(count, EqualityComparer<TEnum>.Default) : null;
            _masks = count > 0 ? new ulong[count] : null;

            for (int i = 0; i < count; i++)
            {
                _index[baseKeys[i]] = i;
            }

            if (relations == null || relations.Count == 0 || _index == null) return;

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                if (!_index.TryGetValue(entry.key, out var fromIndex))
                    continue;

                ulong mask = 0UL;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        if (_index.TryGetValue(related[r], out var toIndex))
                        {
                            if ((uint)toIndex < MaxKeys)
                                mask |= 1UL << toIndex;
                        }
                    }
                }

                _masks[fromIndex] = mask;
            }
        }

        /// <summary>
        /// 使用枚举生成基准键列表（会清空原有 baseKeys）
        /// </summary>
        public void SetBaseKeysFromEnum()
        {
            if (baseKeys == null)
            {
                baseKeys = new List<TEnum>();
            }

            baseKeys.Clear();
            var values = Enum.GetValues(typeof(TEnum));
            for (int i = 0; i < values.Length; i++)
            {
                baseKeys.Add((TEnum)values.GetValue(i));
            }

            _cacheReady = false;
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            _cacheReady = false;
            RebuildCache();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCache()
        {
            if (_cacheReady) return;
            RebuildCache();
        }

#if UNITY_EDITOR
        private void BuildEditorMatrix()
        {
            _suppressMatrixApply = true;
            RebuildCache();
            int count = baseKeys.Count;
            if (count > MaxKeys) count = MaxKeys;
            editorMatrix = new bool[count, count];

            for (int r = 0; r < count; r++)
            {
                for (int c = 0; c < count; c++)
                {
                    editorMatrix[r, c] = IsRelatedByIndex(r, c);
                }
            }

            _suppressMatrixApply = false;
        }

        private void ApplyEditorMatrix()
        {
            if (_suppressMatrixApply) return;
            int count = baseKeys.Count;
            if (count > MaxKeys) count = MaxKeys;
            if (editorMatrix == null || editorMatrix.GetLength(0) != count || editorMatrix.GetLength(1) != count)
            {
                BuildEditorMatrix();
                return;
            }

            relations.Clear();
            for (int r = 0; r < count; r++)
            {
                var entry = new RelationEntry
                {
                    key = baseKeys[r],
                    relatedKeys = new List<TEnum>()
                };

                for (int c = 0; c < count; c++)
                {
                    if (editorMatrix[r, c])
                    {
                        entry.relatedKeys.Add(baseKeys[c]);
                    }
                }

                relations.Add(entry);
            }

            _cacheReady = false;
        }

        private ValueTuple<string, LabelDirection> GetMatrixLabel(TableAxis axis, int index)
        {
            int count = baseKeys != null ? baseKeys.Count : 0;
            if (count > MaxKeys) count = MaxKeys;
            if (count <= 0) return ValueTuple.Create(string.Empty, LabelDirection.LeftToRight);

            if ((uint)index >= (uint)count)
            {
                return ValueTuple.Create(string.Empty, LabelDirection.LeftToRight);
            }

            string label = baseKeys[index].ToString();
            return ValueTuple.Create(label, LabelDirection.LeftToRight);
        }
#endif
    }
    

    
}
