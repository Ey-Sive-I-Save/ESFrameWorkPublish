using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;

namespace ES
{
    public static class ESConfigKeyProtocol
    {
        public const int DefaultStringRuntimeKeyStart = 30000;
    }

    public enum ESAssetNamingSource
    {
        None = 0,
        AssetGuid = 1000,
        EnumKey = 900,
        StringKey = 800,
        Address = 700,
        AssetPath = 600,
        AssetName = 500
    }

    public static class ESAssetNamingWeight
    {
        public const int GuidAuthority = (int)ESAssetNamingSource.AssetGuid;
        public const int EnumRuntimeKey = (int)ESAssetNamingSource.EnumKey;
        public const int StringConfigKey = (int)ESAssetNamingSource.StringKey;
        public const int AddressKey = (int)ESAssetNamingSource.Address;
        public const int EditorAssetPath = (int)ESAssetNamingSource.AssetPath;
        public const int AssetNameFallback = (int)ESAssetNamingSource.AssetName;
    }

    public interface IESConfigKey
    {
        string StringKey { get; }
        int EnumKeyInt { get; }
    }

    [Serializable]
    public class ESGameCoreConfigKey<TEnumKey> : IESConfigKey where TEnumKey : struct, Enum
    {
        [Searchable]
        public TEnumKey enumKey;

        public string stringKey;

        public string StringKey => stringKey;
        public int EnumKeyInt => EnumToInt(enumKey);
        public bool HasEnumKey => EnumKeyInt != 0;

        public string GetStringKey(string fallbackStringKey)
        {
            return string.IsNullOrEmpty(stringKey) ? fallbackStringKey : stringKey;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EnumToInt(TEnumKey value)
        {
            return Convert.ToInt32(value);
        }
    }

    [Serializable]
    public class ESAssetConfigKey<TEnumKey> : IESConfigKey where TEnumKey : struct, Enum
    {
        [Searchable]
        public TEnumKey enumKey;

        public string stringKey;

        public string guid;
        public long localFileId;
        public string assetTypeName;
        public string address;
        public string groupName;
        public string editorPath;
        public int assetRuntimeKey;
        public bool editorOnly;
        public bool alwaysLoaded;

        public string StringKey => stringKey;
        public int EnumKeyInt => EnumToInt(enumKey);
        public bool HasEnumKey => EnumKeyInt != 0;
        public bool HasGuid => !string.IsNullOrEmpty(guid);
        public bool IsSubAsset => localFileId != 0;

        public string GetStringKey(string fallbackStringKey)
        {
            return string.IsNullOrEmpty(stringKey) ? fallbackStringKey : stringKey;
        }

        public void SetAssetAuthority(string assetGuid, long assetLocalFileId, string typeName, string assetPath)
        {
            guid = assetGuid;
            localFileId = assetLocalFileId;
            assetTypeName = typeName;
            editorPath = assetPath;
        }

        public void ApplyToResKey(ESResKey resKey)
        {
            if (resKey == null)
                return;

            resKey.ConfigEnumKeyInt = EnumKeyInt;
            resKey.ConfigStringKey = stringKey;
            resKey.AssetRuntimeKey = assetRuntimeKey;
            resKey.GUID = guid;
            resKey.LocalFileId = localFileId;
            resKey.Path = editorPath;
            resKey.Address = address;
            resKey.GroupName = groupName;
            resKey.EditorOnly = editorOnly;
            resKey.AlwaysLoaded = alwaysLoaded;
        }

        public void ReadFromResKey(ESResKey resKey)
        {
            if (resKey == null)
                return;

            stringKey = resKey.ConfigStringKey;
            assetRuntimeKey = resKey.AssetRuntimeKey;
            guid = resKey.GUID;
            localFileId = resKey.LocalFileId;
            editorPath = resKey.Path;
            address = resKey.Address;
            groupName = resKey.GroupName;
            editorOnly = resKey.EditorOnly;
            alwaysLoaded = resKey.AlwaysLoaded;
            assetTypeName = resKey.TargetType != null ? resKey.TargetType.FullName : resKey.AssetTypeName;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EnumToInt(TEnumKey value)
        {
            return Convert.ToInt32(value);
        }
    }

    public sealed class ESConfigKeyTable<TData> where TData : class
    {
        private struct Slot
        {
            public int runtimeKey;
            public int enumKey;
            public string stringKey;
            public string debugName;
            public TData data;
            public int version;
            public bool valid;
        }

        private readonly List<Slot> slots;
        private readonly Dictionary<int, int> slotByRuntimeKey;
        private readonly Dictionary<string, int> slotByStringKey;
        private readonly List<ESConfigKeyConflict> conflicts;
        private int nextStringRuntimeKey;
        private bool isBuilding;

        public ESConfigKeyTable(int capacity = 64)
        {
            slots = new List<Slot>(capacity);
            slotByRuntimeKey = new Dictionary<int, int>(capacity);
            slotByStringKey = new Dictionary<string, int>(capacity);
            conflicts = new List<ESConfigKeyConflict>(8);
            nextStringRuntimeKey = ESConfigKeyProtocol.DefaultStringRuntimeKeyStart;
        }

        public int Count => slots.Count;
        public int ConflictCount => conflicts.Count;
        public bool IsBuilding => isBuilding;
        public IReadOnlyList<ESConfigKeyConflict> Conflicts => conflicts;

        public void BeginBuild(bool clear = false)
        {
            if (isBuilding)
                throw new InvalidOperationException("ESConfigKeyTable is already building.");

            isBuilding = true;
            if (clear)
                Clear();
        }

        public void EndBuild()
        {
            isBuilding = false;
        }

        public void Clear()
        {
            EnsureCanBuild();
            slots.Clear();
            slotByRuntimeKey.Clear();
            slotByStringKey.Clear();
            conflicts.Clear();
            nextStringRuntimeKey = ESConfigKeyProtocol.DefaultStringRuntimeKeyStart;
        }

        public int Bake<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            EnsureCanBuild();
            if (key == null)
                return 0;

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0 && !slotByRuntimeKey.ContainsKey(enumKey))
                return enumKey;

            if (enumKey != 0)
                AddConflict(enumKey, key.GetStringKey(fallbackStringKey), "Enum runtime key is duplicated. Try string key fallback.");

            return BakeStringRuntimeKey(key.GetStringKey(fallbackStringKey));
        }

        public bool Register<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            int enumKey = key != null ? key.EnumKeyInt : 0;
            string stringKey = key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey;
            int runtimeKey = Bake(key, fallbackStringKey);
            return RegisterInternal(runtimeKey, enumKey, stringKey, data, stringKey);
        }

        public bool Upsert<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            int enumKey = key != null ? key.EnumKeyInt : 0;
            string stringKey = key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey;
            int runtimeKey = Bake(key, fallbackStringKey);
            return UpsertInternal(runtimeKey, enumKey, stringKey, data, stringKey);
        }

        public int Bake<TEnumKey>(ESAssetConfigKey<TEnumKey> key, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            EnsureCanBuild();
            if (key == null)
                return 0;

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0 && !slotByRuntimeKey.ContainsKey(enumKey))
                return enumKey;

            if (key.assetRuntimeKey != 0 && !slotByRuntimeKey.ContainsKey(key.assetRuntimeKey))
                return key.assetRuntimeKey;

            if (enumKey != 0)
                AddConflict(enumKey, key.GetStringKey(fallbackStringKey), "Asset enum runtime key is duplicated. Try asset runtime key or string key fallback.");

            return BakeStringRuntimeKey(key.GetStringKey(fallbackStringKey));
        }

        public bool Register<TEnumKey>(ESAssetConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            int enumKey = key != null ? key.EnumKeyInt : 0;
            string stringKey = key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey;
            int runtimeKey = Bake(key, fallbackStringKey);
            return RegisterInternal(runtimeKey, enumKey, stringKey, data, stringKey);
        }

        public bool Upsert<TEnumKey>(ESAssetConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            int enumKey = key != null ? key.EnumKeyInt : 0;
            string stringKey = key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey;
            int runtimeKey = Bake(key, fallbackStringKey);
            return UpsertInternal(runtimeKey, enumKey, stringKey, data, stringKey);
        }

        public bool Register(int runtimeKey, TData data, string debugName = null)
        {
            return RegisterInternal(runtimeKey, runtimeKey < ESConfigKeyProtocol.DefaultStringRuntimeKeyStart ? runtimeKey : 0, debugName, data, debugName);
        }

        public bool Upsert(int runtimeKey, TData data, string debugName = null)
        {
            return UpsertInternal(runtimeKey, runtimeKey < ESConfigKeyProtocol.DefaultStringRuntimeKeyStart ? runtimeKey : 0, debugName, data, debugName);
        }

        public bool TryGet(int runtimeKey, out TData data)
        {
            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int slot))
                return TryGetBySlot(slot, out data);

            data = null;
            return false;
        }

        public bool TryGet<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, out TData data)
            where TEnumKey : struct, Enum
        {
            if (key == null)
            {
                data = null;
                return false;
            }

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0 && TryGet(enumKey, out data))
                return true;

            return TryGetByStringKey(key.StringKey, out data);
        }

        public bool TryGet<TEnumKey>(ESAssetConfigKey<TEnumKey> key, out TData data)
            where TEnumKey : struct, Enum
        {
            if (key == null)
            {
                data = null;
                return false;
            }

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0 && TryGet(enumKey, out data))
                return true;

            if (key.assetRuntimeKey != 0 && TryGet(key.assetRuntimeKey, out data))
                return true;

            return TryGetByStringKey(key.StringKey, out data);
        }

        public TData Get(int runtimeKey)
        {
            return TryGet(runtimeKey, out TData data) ? data : null;
        }

        public bool TryGetRuntimeKey(string stringKey, out int runtimeKey)
        {
            if (TryGetSlotByStringKey(stringKey, out int slot))
            {
                runtimeKey = slots[slot].runtimeKey;
                return runtimeKey != 0;
            }

            runtimeKey = 0;
            return false;
        }

        public bool TryGetByStringKey(string stringKey, out TData data)
        {
            if (TryGetSlotByStringKey(stringKey, out int slot))
                return TryGetBySlot(slot, out data);

            data = null;
            return false;
        }

        public bool TryGetSlot(int runtimeKey, out int slot)
        {
            return slotByRuntimeKey.TryGetValue(runtimeKey, out slot);
        }

        public bool TryGetSlotByStringKey(string stringKey, out int slot)
        {
            if (string.IsNullOrEmpty(stringKey))
            {
                slot = -1;
                return false;
            }

            return slotByStringKey.TryGetValue(stringKey, out slot);
        }

        public bool TryGetBySlot(int slot, out TData data)
        {
            if ((uint)slot < (uint)slots.Count)
            {
                Slot entry = slots[slot];
                if (entry.valid)
                {
                    data = entry.data;
                    return data != null;
                }
            }

            data = null;
            return false;
        }

        public bool TryGetRuntimeKeyBySlot(int slot, out int runtimeKey)
        {
            if ((uint)slot < (uint)slots.Count && slots[slot].valid)
            {
                runtimeKey = slots[slot].runtimeKey;
                return runtimeKey != 0;
            }

            runtimeKey = 0;
            return false;
        }

        public bool RebindRuntimeKey(int oldRuntimeKey, int newRuntimeKey)
        {
            EnsureCanBuild();
            if (oldRuntimeKey == 0 || newRuntimeKey == 0)
                return false;

            if (!slotByRuntimeKey.TryGetValue(oldRuntimeKey, out int slot))
                return false;

            return BindRuntimeKeyToSlot(slot, newRuntimeKey, true);
        }

        public bool ForceBindRuntimeKeyToString(int runtimeKey, string stringKey)
        {
            EnsureCanBuild();
            if (runtimeKey == 0 || string.IsNullOrEmpty(stringKey))
                return false;

            if (!slotByStringKey.TryGetValue(stringKey, out int slot))
                return false;

            return BindRuntimeKeyToSlot(slot, runtimeKey, true);
        }

        public bool ForceSetRuntimeKeyForStringKey(string stringKey, int runtimeKey)
        {
            return ForceSetRuntimeKeyForStringKey(stringKey, runtimeKey, out _, out _);
        }

        public bool ForceSetRuntimeKeyForStringKey(string stringKey, int runtimeKey, out int oldRuntimeKey, out int slot)
        {
            EnsureCanBuild();
            oldRuntimeKey = 0;
            slot = -1;

            if (runtimeKey == 0 || string.IsNullOrEmpty(stringKey))
                return false;

            if (!slotByStringKey.TryGetValue(stringKey, out slot))
                return false;

            oldRuntimeKey = slots[slot].runtimeKey;
            return BindRuntimeKeyToSlot(slot, runtimeKey, true);
        }

        public bool Remove(int runtimeKey)
        {
            EnsureCanBuild();
            if (!slotByRuntimeKey.TryGetValue(runtimeKey, out int slot))
                return false;

            Slot entry = slots[slot];
            if (!entry.valid)
                return false;

            slotByRuntimeKey.Remove(entry.runtimeKey);
            if (!string.IsNullOrEmpty(entry.stringKey))
                slotByStringKey.Remove(entry.stringKey);

            entry.valid = false;
            entry.version++;
            slots[slot] = entry;
            return true;
        }

        public string GetDebugName(int runtimeKey)
        {
            return slotByRuntimeKey.TryGetValue(runtimeKey, out int slot) ? slots[slot].debugName : null;
        }

        public string GetConflictReport()
        {
            if (conflicts.Count == 0)
                return string.Empty;

            System.Text.StringBuilder builder = new System.Text.StringBuilder(conflicts.Count * 96);
            for (int i = 0; i < conflicts.Count; i++)
            {
                ESConfigKeyConflict conflict = conflicts[i];
                builder.Append('[').Append(i).Append("] key=").Append(conflict.runtimeKey)
                    .Append(", string=").Append(conflict.stringKey)
                    .Append(", reason=").Append(conflict.reason)
                    .AppendLine();
            }

            return builder.ToString();
        }

        public int BakeRaw(int enumKey, string stringKey)
        {
            EnsureCanBuild();
            if (enumKey != 0 && !slotByRuntimeKey.ContainsKey(enumKey))
                return enumKey;

            if (enumKey != 0)
                AddConflict(enumKey, stringKey, "Enum runtime key is duplicated. Try string key fallback.");

            return BakeStringRuntimeKey(stringKey);
        }

        private bool RegisterInternal(int runtimeKey, int enumKey, string stringKey, TData data, string debugName)
        {
            EnsureCanBuild();
            if (runtimeKey == 0 || data == null)
            {
                AddConflict(runtimeKey, debugName, "Empty runtime key or empty data. Skipped.");
                return false;
            }

            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int runtimeSlot))
            {
                Slot existing = slots[runtimeSlot];
                if (!ReferenceEquals(existing.data, data))
                {
                    AddConflict(runtimeKey, debugName, "Runtime key is duplicated. New data skipped.");
                    return false;
                }

                return true;
            }

            if (!string.IsNullOrEmpty(stringKey) && slotByStringKey.TryGetValue(stringKey, out int stringSlot))
            {
                Slot existing = slots[stringSlot];
                if (!ReferenceEquals(existing.data, data))
                {
                    AddConflict(runtimeKey, stringKey, "String key already maps to another data slot. Skipped.");
                    return false;
                }

                return BindRuntimeKeyToSlot(stringSlot, runtimeKey, false);
            }

            int slot = slots.Count;
            slots.Add(new Slot
            {
                runtimeKey = runtimeKey,
                enumKey = enumKey,
                stringKey = stringKey,
                debugName = debugName,
                data = data,
                version = 1,
                valid = true
            });

            slotByRuntimeKey[runtimeKey] = slot;
            if (!string.IsNullOrEmpty(stringKey))
                slotByStringKey[stringKey] = slot;

            return true;
        }

        private bool UpsertInternal(int runtimeKey, int enumKey, string stringKey, TData data, string debugName)
        {
            EnsureCanBuild();
            if (runtimeKey == 0 || data == null)
            {
                AddConflict(runtimeKey, debugName, "Empty runtime key or empty data. Upsert skipped.");
                return false;
            }

            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int runtimeSlot))
            {
                ReplaceSlotData(runtimeSlot, runtimeKey, enumKey, stringKey, data, debugName);
                return true;
            }

            if (!string.IsNullOrEmpty(stringKey) && slotByStringKey.TryGetValue(stringKey, out int stringSlot))
            {
                ReplaceSlotData(stringSlot, runtimeKey, enumKey, stringKey, data, debugName);
                return true;
            }

            return RegisterInternal(runtimeKey, enumKey, stringKey, data, debugName);
        }

        private void ReplaceSlotData(int slot, int runtimeKey, int enumKey, string stringKey, TData data, string debugName)
        {
            Slot entry = slots[slot];

            if (entry.runtimeKey != runtimeKey)
            {
                if (entry.runtimeKey != 0)
                    slotByRuntimeKey.Remove(entry.runtimeKey);

                slotByRuntimeKey[runtimeKey] = slot;
            }

            if (!string.Equals(entry.stringKey, stringKey, StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(entry.stringKey))
                    slotByStringKey.Remove(entry.stringKey);

                if (!string.IsNullOrEmpty(stringKey))
                    slotByStringKey[stringKey] = slot;
            }

            entry.runtimeKey = runtimeKey;
            entry.enumKey = enumKey;
            entry.stringKey = stringKey;
            entry.debugName = debugName;
            entry.data = data;
            entry.version++;
            entry.valid = true;
            slots[slot] = entry;
        }

        private bool BindRuntimeKeyToSlot(int slot, int runtimeKey, bool replaceExisting)
        {
            if ((uint)slot >= (uint)slots.Count || runtimeKey == 0)
                return false;

            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int existingSlot) && existingSlot != slot)
            {
                if (!replaceExisting)
                    return false;

                Slot existing = slots[existingSlot];
                existing.runtimeKey = 0;
                existing.version++;
                slots[existingSlot] = existing;
            }

            Slot entry = slots[slot];
            if (!entry.valid)
                return false;

            if (entry.runtimeKey != 0)
                slotByRuntimeKey.Remove(entry.runtimeKey);

            slotByRuntimeKey[runtimeKey] = slot;
            entry.runtimeKey = runtimeKey;
            entry.version++;
            slots[slot] = entry;
            return true;
        }

        private int BakeStringRuntimeKey(string stringKey)
        {
            if (string.IsNullOrEmpty(stringKey))
                return 0;

            if (TryGetRuntimeKey(stringKey, out int runtimeKey))
                return runtimeKey;

            runtimeKey = AllocateStringRuntimeKey();
            return runtimeKey;
        }

        private int AllocateStringRuntimeKey()
        {
            int runtimeKey = nextStringRuntimeKey;
            while (slotByRuntimeKey.ContainsKey(runtimeKey))
                runtimeKey++;

            nextStringRuntimeKey = runtimeKey + 1;
            return runtimeKey;
        }

        private void AddConflict(int runtimeKey, string stringKey, string reason)
        {
            conflicts.Add(new ESConfigKeyConflict
            {
                runtimeKey = runtimeKey,
                stringKey = stringKey,
                reason = reason
            });
        }

        private void EnsureCanBuild()
        {
            if (!isBuilding)
                throw new InvalidOperationException("ESConfigKeyTable is locked. Use BeginBuild/EndBuild during initialization or hot-load only.");
        }
    }    [Serializable]
    public struct ESConfigKeyConflict
    {
        public int runtimeKey;
        public string stringKey;
        public string reason;
    }

    [Serializable]
    public struct ESRuntimeInstanceHandle
    {
        public int id;
        public int version;

        public bool IsValid => id > 0;
    }

    public sealed class ESRuntimeInstanceIndex<TInstance> where TInstance : class
    {
        private readonly Dictionary<int, TInstance> instanceById;
        private readonly Dictionary<int, List<TInstance>> instancesByRuntimeKey;
        private int nextInstanceId;

        public ESRuntimeInstanceIndex(int capacity = 64)
        {
            instanceById = new Dictionary<int, TInstance>(capacity);
            instancesByRuntimeKey = new Dictionary<int, List<TInstance>>(capacity);
            nextInstanceId = 1;
        }

        public int Count => instanceById.Count;

        public ESRuntimeInstanceHandle Add(int runtimeKey, TInstance instance)
        {
            if (runtimeKey == 0 || instance == null)
                return default;

            int id = nextInstanceId++;
            if (nextInstanceId <= 0)
                nextInstanceId = 1;

            instanceById[id] = instance;
            if (!instancesByRuntimeKey.TryGetValue(runtimeKey, out List<TInstance> list))
            {
                list = new List<TInstance>(4);
                instancesByRuntimeKey.Add(runtimeKey, list);
            }

            list.Add(instance);
            return new ESRuntimeInstanceHandle { id = id, version = 1 };
        }

        public bool Remove(int runtimeKey, ESRuntimeInstanceHandle handle, TInstance instance)
        {
            if (!handle.IsValid)
                return false;

            bool removed = instanceById.Remove(handle.id);
            if (runtimeKey != 0 && instancesByRuntimeKey.TryGetValue(runtimeKey, out List<TInstance> list))
            {
                int index = list.IndexOf(instance);
                if (index >= 0)
                {
                    int last = list.Count - 1;
                    list[index] = list[last];
                    list.RemoveAt(last);
                }
            }

            return removed;
        }

        public bool TryGet(ESRuntimeInstanceHandle handle, out TInstance instance)
        {
            if (handle.IsValid)
                return instanceById.TryGetValue(handle.id, out instance);

            instance = null;
            return false;
        }

        public bool TryGetInstances(int runtimeKey, out List<TInstance> instances)
        {
            return instancesByRuntimeKey.TryGetValue(runtimeKey, out instances);
        }

        public void Clear()
        {
            instanceById.Clear();
            instancesByRuntimeKey.Clear();
            nextInstanceId = 1;
        }
    }

}

