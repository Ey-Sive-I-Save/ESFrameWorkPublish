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
        public const int MaxKeys = 32;

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
        [NonSerialized] private uint[] _masks;
        [NonSerialized] private bool _cacheReady;

#if UNITY_EDITOR
        [TableMatrix(SquareCells = true,Transpose = true, Labels = "GetMatrixLabel")]
        [OnValueChanged("EditorApplyMatrix")]
        [ShowInInspector, PropertyOrder(100)]
        private bool[,] editorMatrix;

        [SerializeField, LabelText("对称模式"), PropertyOrder(99)]
        private bool editorSymmetricMode;

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

        public void AddRelation(string fromKey, string toKey)
        {
            AddRelationNoRebuild(fromKey, toKey);
            _cacheReady = false;
            RebuildCache();
        }

        public void AddRelationTwoWay(string a, string b)
        {
            AddRelationNoRebuild(a, b);
            AddRelationNoRebuild(b, a);
            _cacheReady = false;
            RebuildCache();
        }

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
            return ((_masks[fromIndex] >> toIndex) & 1u) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast(string fromKey, uint targetMask)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (fromKey == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            return (_masks[fromIndex] & targetMask) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast(string key, out uint mask)
        {
            if (!_cacheReady || _index == null || _masks == null || key == null)
            {
                mask = 0u;
                return false;
            }

            if (_index.TryGetValue(key, out var index))
            {
                mask = _masks[index];
                return true;
            }

            mask = 0u;
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
            return ((_masks[fromIndex] >> toIndex) & 1u) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetMaskByIndex(int index)
        {
            EnsureCache();
            if (_masks == null) return 0u;
            if ((uint)index >= (uint)_masks.Length) return 0u;
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
            _masks = count > 0 ? new uint[count] : null;

            for (int i = 0; i < count; i++)
            {
                var key = baseKeys[i] ?? string.Empty;
                if (_index.ContainsKey(key))
                {
                    continue;
                }
                _index.Add(key, i);
            }

            if (relations == null || relations.Count == 0 || _index == null) return;

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                if (!_index.TryGetValue(entry.key ?? string.Empty, out var fromIndex))
                    continue;

                uint mask = 0u;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        var rk = related[r] ?? string.Empty;
                        if (_index.TryGetValue(rk, out var toIndex))
                        {
                            if ((uint)toIndex < MaxKeys)
                                mask |= 1u << toIndex;
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

            ApplySymmetricMode(count);
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

        private void ApplySymmetricMode(int count)
        {
            if (!editorSymmetricMode || editorMatrix == null) return;
            for (int r = 0; r < count; r++)
            {
                for (int c = r + 1; c < count; c++)
                {
                    bool related = editorMatrix[r, c] || editorMatrix[c, r];
                    editorMatrix[r, c] = related;
                    editorMatrix[c, r] = related;
                }
            }
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

        private void AddRelationNoRebuild(string fromKey, string toKey)
        {
            if (relations == null) relations = new List<RelationEntry>();

            for (int i = 0; i < relations.Count; i++)
            {
                if (string.Equals(relations[i].key, fromKey, StringComparison.Ordinal))
                {
                    var entry = relations[i];
                    if (entry.relatedKeys == null) entry.relatedKeys = new List<string>();
                    if (!entry.relatedKeys.Contains(toKey)) entry.relatedKeys.Add(toKey);
                    relations[i] = entry;
                    return;
                }
            }

            relations.Add(new RelationEntry
            {
                key = fromKey,
                relatedKeys = new List<string> { toKey }
            });
        }
    }

    /// <summary>
    /// 关系遮罩容器（整数版）
    /// </summary>
    [Serializable]
    public sealed class RelationMaskIntMap : ISerializationCallbackReceiver
    {
        public const int MaxKeys = 32;

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
        [NonSerialized] private uint[] _masks;
        [NonSerialized] private bool _cacheReady;

#if UNITY_EDITOR
        [TableMatrix(SquareCells = true, Transpose = true, Labels = "GetMatrixLabel")]
        [OnValueChanged("EditorApplyMatrix")]
        [ShowInInspector, PropertyOrder(100)]
        private bool[,] editorMatrix;

        [SerializeField, LabelText("对称模式"), PropertyOrder(99)]
        private bool editorSymmetricMode;

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

        public void AddRelation(int fromKey, int toKey)
        {
            AddRelationNoRebuild(fromKey, toKey);
            _cacheReady = false;
            RebuildCache();
        }

        public void AddRelationTwoWay(int a, int b)
        {
            AddRelationNoRebuild(a, b);
            AddRelationNoRebuild(b, a);
            _cacheReady = false;
            RebuildCache();
        }

        public void AddRelation<TEnum>(TEnum fromKey, TEnum toKey) where TEnum : struct, Enum
        {
            if (!TryEnumToInt32(fromKey, out var from) || !TryEnumToInt32(toKey, out var to)) return;
            AddRelation(from, to);
        }

        public void AddRelationTwoWay<TEnum>(TEnum a, TEnum b) where TEnum : struct, Enum
        {
            if (!TryEnumToInt32(a, out var intA) || !TryEnumToInt32(b, out var intB)) return;
            AddRelationTwoWay(intA, intB);
        }

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
            return ((_masks[fromIndex] >> toIndex) & 1u) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast(int fromKey, uint targetMask)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            return (_masks[fromIndex] & targetMask) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast<TEnum>(TEnum fromKey, uint targetMask) where TEnum : struct, Enum
        {
            return TryEnumToInt32(fromKey, out var from) && IsRelatedMaskFast(from, targetMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedFast<TEnum>(TEnum fromKey, TEnum toKey) where TEnum : struct, Enum
        {
            return TryEnumToInt32(fromKey, out var from) && TryEnumToInt32(toKey, out var to) && IsRelatedFast(from, to);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast(int key, out uint mask)
        {
            if (!_cacheReady || _index == null || _masks == null)
            {
                mask = 0u;
                return false;
            }

            if (_index.TryGetValue(key, out var index))
            {
                mask = _masks[index];
                return true;
            }

            mask = 0u;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast<TEnum>(TEnum key, out uint mask) where TEnum : struct, Enum
        {
            if (TryEnumToInt32(key, out var intKey)) return TryGetMaskFast(intKey, out mask);
            mask = 0u;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetIndex<TEnum>(TEnum key, out int index) where TEnum : struct, Enum
        {
            if (TryEnumToInt32(key, out var intKey)) return TryGetIndex(intKey, out index);
            index = -1;
            return false;
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
            return TryEnumToInt32(fromKey, out var from) && TryEnumToInt32(toKey, out var to) && IsRelated(from, to);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetMask<TEnum>(TEnum key) where TEnum : struct, Enum
        {
            return TryGetIndex(key, out var index) ? GetMaskByIndex(index) : 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetIndexOrMinusOne<TEnum>(TEnum key) where TEnum : struct, Enum
        {
            return TryGetIndex(key, out var index) ? index : -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedByIndex(int fromIndex, int toIndex)
        {
            EnsureCache();
            if (_masks == null) return false;
            if ((uint)fromIndex >= (uint)_masks.Length) return false;
            if ((uint)toIndex >= (uint)_masks.Length) return false;
            return ((_masks[fromIndex] >> toIndex) & 1u) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetMaskByIndex(int index)
        {
            EnsureCache();
            if (_masks == null) return 0u;
            if ((uint)index >= (uint)_masks.Length) return 0u;
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
            _masks = count > 0 ? new uint[count] : null;

            for (int i = 0; i < count; i++)
            {
                int key = baseKeys[i];
                if (_index.ContainsKey(key))
                {
                    continue;
                }
                _index.Add(key, i);
            }

            if (relations == null || relations.Count == 0 || _index == null) return;

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                if (!_index.TryGetValue(entry.key, out var fromIndex))
                    continue;

                uint mask = 0u;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        if (_index.TryGetValue(related[r], out var toIndex))
                        {
                            if ((uint)toIndex < MaxKeys)
                                mask |= 1u << toIndex;
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
                if (TryEnumToInt32((TEnum)values.GetValue(i), out var intValue))
                {
                    baseKeys.Add(intValue);
                }
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

        private static bool TryEnumToInt32<TEnum>(TEnum value, out int result) where TEnum : struct, Enum
        {
            TypeCode code = Type.GetTypeCode(Enum.GetUnderlyingType(typeof(TEnum)));
            switch (code)
            {
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.Int32:
                    result = Convert.ToInt32(value);
                    return true;
                case TypeCode.Int64:
                    long longValue = Convert.ToInt64(value);
                    if (longValue < int.MinValue || longValue > int.MaxValue)
                    {
                        result = 0;
                        return false;
                    }
                    result = (int)longValue;
                    return true;
                case TypeCode.Byte:
                case TypeCode.UInt16:
                    result = Convert.ToInt32(value);
                    return true;
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    ulong ulongValue = Convert.ToUInt64(value);
                    if (ulongValue > int.MaxValue)
                    {
                        result = 0;
                        return false;
                    }
                    result = (int)ulongValue;
                    return true;
                default:
                    result = 0;
                    return false;
            }
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

            ApplySymmetricMode(count);
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

        private void ApplySymmetricMode(int count)
        {
            if (!editorSymmetricMode || editorMatrix == null) return;
            for (int r = 0; r < count; r++)
            {
                for (int c = r + 1; c < count; c++)
                {
                    bool related = editorMatrix[r, c] || editorMatrix[c, r];
                    editorMatrix[r, c] = related;
                    editorMatrix[c, r] = related;
                }
            }
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

        private void AddRelationNoRebuild(int fromKey, int toKey)
        {
            if (relations == null) relations = new List<RelationEntry>();

            for (int i = 0; i < relations.Count; i++)
            {
                if (relations[i].key == fromKey)
                {
                    var entry = relations[i];
                    if (entry.relatedKeys == null) entry.relatedKeys = new List<int>();
                    if (!entry.relatedKeys.Contains(toKey)) entry.relatedKeys.Add(toKey);
                    relations[i] = entry;
                    return;
                }
            }

            relations.Add(new RelationEntry
            {
                key = fromKey,
                relatedKeys = new List<int> { toKey }
            });
        }
    }

    /// <summary>
    /// 关系遮罩容器（枚举版）
    /// </summary>
    [Serializable]
    public class RelationMaskEnumMap<TEnum> : ISerializationCallbackReceiver where TEnum : struct, Enum
    {
        public const int MaxKeys = 32;

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
        [NonSerialized] private uint[] _masks;
        [NonSerialized] private bool _cacheReady;

#if UNITY_EDITOR
        [TableMatrix(SquareCells = true, Transpose = true, Labels = "GetMatrixLabel")]
        [OnValueChanged("EditorApplyMatrix")]
        [ShowInInspector, PropertyOrder(100)]
        private bool[,] editorMatrix;

        [SerializeField, LabelText("对称模式"), PropertyOrder(99)]
        private bool editorSymmetricMode;

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

        public void AddRelationTwoWay(TEnum a, TEnum b)
        {
            AddRelation(a, b);
            AddRelation(b, a);
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
            return ((_masks[fromIndex] >> toIndex) & 1u) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRelatedMaskFast(TEnum fromKey, uint targetMask)
        {
            if (!_cacheReady || _index == null || _masks == null) return false;
            if (!_index.TryGetValue(fromKey, out var fromIndex)) return false;
            return (_masks[fromIndex] & targetMask) != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMaskFast(TEnum key, out uint mask)
        {
            if (!_cacheReady || _index == null || _masks == null)
            {
                mask = 0u;
                return false;
            }

            if (_index.TryGetValue(key, out var index))
            {
                mask = _masks[index];
                return true;
            }

            mask = 0u;
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
            return ((_masks[fromIndex] >> toIndex) & 1u) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetMask(TEnum key)
        {
            return TryGetIndex(key, out var index) ? GetMaskByIndex(index) : 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetMaskByIndex(int index)
        {
            EnsureCache();
            if (_masks == null) return 0u;
            if ((uint)index >= (uint)_masks.Length) return 0u;
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
            _masks = count > 0 ? new uint[count] : null;

            for (int i = 0; i < count; i++)
            {
                TEnum key = baseKeys[i];
                if (_index.ContainsKey(key))
                {
                    continue;
                }
                _index.Add(key, i);
            }

            if (relations == null || relations.Count == 0 || _index == null) return;

            for (int i = 0; i < relations.Count; i++)
            {
                var entry = relations[i];
                if (!_index.TryGetValue(entry.key, out var fromIndex))
                    continue;

                uint mask = 0u;
                var related = entry.relatedKeys;
                if (related != null)
                {
                    for (int r = 0; r < related.Count; r++)
                    {
                        if (_index.TryGetValue(related[r], out var toIndex))
                        {
                            if ((uint)toIndex < MaxKeys)
                                mask |= 1u << toIndex;
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

            ApplySymmetricMode(count);
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

        private void ApplySymmetricMode(int count)
        {
            if (!editorSymmetricMode || editorMatrix == null) return;
            for (int r = 0; r < count; r++)
            {
                for (int c = r + 1; c < count; c++)
                {
                    bool related = editorMatrix[r, c] || editorMatrix[c, r];
                    editorMatrix[r, c] = related;
                    editorMatrix[c, r] = related;
                }
            }
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

            string label = baseKeys[index]._GetInspectorName().ToString();
            return ValueTuple.Create(label, LabelDirection.LeftToRight);
        }
#endif
    }
    

    
}
