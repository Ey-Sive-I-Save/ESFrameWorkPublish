using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Sirenix.OdinInspector;

namespace ES
{
    /// <summary>
    /// ConfigKey 运行时缺失诊断的只读订阅入口。
    /// Editor 集成必须能够订阅缺失事件，但只有本程序集可以上报诊断。
    /// </summary>
    public static class ESConfigKeyDiagnostics
    {
        public static event Action<string, string> MissingKey;

        /// <summary>
        /// 供 ConfigKey 运行时表及其直接适配层上报缺失诊断。
        /// 普通业务只应订阅 <see cref="MissingKey"/>，不得把它作为通用事件总线。
        /// </summary>
        public static void ReportMissing(string scope, string description)
        {
            try { MissingKey?.Invoke(scope, description); }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
        }
    }

    /// <summary>
    /// 冷路径数据补值委托。必须按 ref 传递，使 class 可被替换、struct 可被原位修改；
    /// 仅用于注入前组装配置，不进入 RuntimeKey 查询热路径。
    /// </summary>
    public delegate void ESDataFiller<T>(ref T value);

    internal static class ESConfigKeyEnumConverter<TEnumKey> where TEnumKey : struct, Enum
    {
        static ESConfigKeyEnumConverter()
        {
            if (Enum.GetUnderlyingType(typeof(TEnumKey)) != typeof(ushort))
                throw new InvalidOperationException("[ESConfigKey][Enum] ConfigKey 枚举必须使用 ushort 底层类型：" + typeof(TEnumKey).FullName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt(TEnumKey value) => Unsafe.As<TEnumKey, ushort>(ref value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TEnumKey FromInt(int value)
        {
            if ((uint)value > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(value), value, "ConfigKey 枚举值必须位于 ushort 范围内。");

            ushort raw = (ushort)value;
            return Unsafe.As<ushort, TEnumKey>(ref raw);
        }
    }

    public static class ESConfigKeyProtocol
    {
        /// <summary>
        /// StringKey RuntimeKey 的保留低位边界。RuntimeKey 由当前表的稳定 scope 和
        /// StableKey 计算；它仍不是序列化协议，不能写入配置、Manifest、存档或网络数据。
        /// </summary>
        public const int DefaultStringRuntimeKeyStart = 30000;

        /// <summary>避免 StringKey 运行时索引与 ushort EnumKey 直接重叠的起点。</summary>
        public const int DeterministicStringRuntimeKeyStart = 1000000;
    }

    /// <summary>
    /// ConfigKey 的稳定身份规则。EnumKey 与 StringKey 都是正式身份；二者同时存在
    /// 时必须指向同一条定义，不能因为 Enum 相同而忽略不同的 StringKey。Enum 只在
    /// 编辑器选择、代码重构和静态检查上更友好，不改变 StringKey 的权威性。
    /// </summary>
    public static class ESConfigKeyMatch
    {
        public static bool IsConfigured(int enumKey, string stringKey)
            => enumKey != 0 || !string.IsNullOrEmpty(stringKey);

        public static bool Matches(int leftEnumKey, string leftStringKey, int rightEnumKey, string rightStringKey)
        {
            bool leftHasEnum = leftEnumKey != 0;
            bool rightHasEnum = rightEnumKey != 0;
            bool leftHasString = !string.IsNullOrEmpty(leftStringKey);
            bool rightHasString = !string.IsNullOrEmpty(rightStringKey);
            bool enumMatches = leftHasEnum && rightHasEnum && leftEnumKey == rightEnumKey;
            bool stringMatches = leftHasString && rightHasString
                                 && string.Equals(leftStringKey, rightStringKey, StringComparison.Ordinal);

            if (leftHasEnum && rightHasEnum && !enumMatches)
                return false;
            if (leftHasString && rightHasString && !stringMatches)
                return false;

            // A partial reference may use either alias. A fully populated reference must agree on both.
            return enumMatches || stringMatches;
        }

        public static string Describe(int enumKey, string stringKey)
        {
            if (enumKey != 0 && !string.IsNullOrEmpty(stringKey))
                return "Enum=" + enumKey + " | String=" + stringKey;
            return enumKey != 0 ? "Enum=" + enumKey : (stringKey ?? string.Empty);
        }
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
        public const int EnumConfigKey = (int)ESAssetNamingSource.EnumKey;
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

    /// <summary>Asset Catalog 装配专用的强类型键初始化契约；普通业务不得直接调用。</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public interface IESAssetConfigKeyInitializer : IESConfigKey
    {
        void InitializeRuntimeKey(
            int runtimeEnumKey,
            string runtimeStringKey,
            string assetGuid,
            long assetLocalFileId,
            string typeName,
            string assetPath);
    }

    [Serializable]
    public class ESGameCoreConfigKey<TEnumKey> : IESConfigKey where TEnumKey : struct, Enum
    {
        [Searchable]
        public TEnumKey enumKey;

        public string stringKey;

        // 编辑器选择与烘焙阶段的精确身份。运行时查表仍只使用 enumKey/stringKey。
        public string definitionGuid;
        public long definitionLocalFileId;
        public string definitionTypeName;

        public string StringKey => stringKey;
        public int EnumKeyInt => EnumToInt(enumKey);
        public bool HasEnumKey => EnumKeyInt != 0;
        public bool IsConfigured => ESConfigKeyMatch.IsConfigured(EnumKeyInt, stringKey);
        public bool HasDefinitionIdentity => !string.IsNullOrEmpty(definitionGuid);

        public string GetStringKey() => stringKey;

        public ESStableKey ToStableKey(string scope)
        {
            return new ESStableKey(scope, (ushort)EnumKeyInt, stringKey);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EnumToInt(TEnumKey value)
        {
            return ESConfigKeyEnumConverter<TEnumKey>.ToInt(value);
        }
    }

    [Serializable]
    public class ESAssetConfigKey<TEnumKey> : IESAssetConfigKeyInitializer where TEnumKey : struct, Enum
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
        public bool editorOnly;
        public bool alwaysLoaded;

        public string StringKey => stringKey;
        public int EnumKeyInt => EnumToInt(enumKey);
        public bool HasEnumKey => EnumKeyInt != 0;
        public bool IsConfigured => ESConfigKeyMatch.IsConfigured(EnumKeyInt, stringKey);
        public bool HasGuid => !string.IsNullOrEmpty(guid);
        public bool IsSubAsset => localFileId != 0;

        public string GetStringKey(string fallbackStringKey)
        {
            return string.IsNullOrEmpty(stringKey) ? fallbackStringKey : stringKey;
        }

        public ESStableKey ToStableKey(string scope, string fallbackStringKey = null)
        {
            return new ESStableKey(scope, (ushort)EnumKeyInt, GetStringKey(fallbackStringKey));
        }

        public void SetAssetAuthority(string assetGuid, long assetLocalFileId, string typeName, string assetPath)
        {
            guid = assetGuid;
            localFileId = assetLocalFileId;
            assetTypeName = typeName;
            editorPath = assetPath;
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void InitializeRuntimeKey(
            int runtimeEnumKey,
            string runtimeStringKey,
            string assetGuid,
            long assetLocalFileId,
            string typeName,
            string assetPath)
        {
            enumKey = ESConfigKeyEnumConverter<TEnumKey>.FromInt(runtimeEnumKey);
            stringKey = runtimeStringKey;
            SetAssetAuthority(assetGuid, assetLocalFileId, typeName, assetPath);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int EnumToInt(TEnumKey value)
        {
            return ESConfigKeyEnumConverter<TEnumKey>.ToInt(value);
        }
    }

    [Serializable]
    public struct ESConfigKeyTableEntry
    {
        public int runtimeKey;
        public int enumKey;
        public string stringKey;
        public string debugName;
    }

    /// <summary>
    /// 当前进程内的强类型查询表。RuntimeKey 只在本实例从构建到 Clear/Rebuild 之前有效；
    /// 外部持久数据必须保存 EnumKey/StringKey，并在新表中重新解析。
    /// </summary>
    public class ESConfigKeyTable<TData> where TData : class
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
        private readonly Dictionary<int, int> slotByEnumKey;
        private readonly Dictionary<string, int> slotByStringKey;
        // Bake 可能先于 Register 被调用；预留表保证同一构建生命周期内重复 Bake
        // 不会为同一个 StringKey 分配多个临时 RuntimeKey。
        private readonly Dictionary<string, int> reservedRuntimeKeyByString;
        private readonly HashSet<int> reservedStringRuntimeKeys;
        private readonly List<ESConfigKeyConflict> conflicts;
        private bool isBuilding;
        private string schemaHash;
        private readonly string keyScope;

        public ESConfigKeyTable(int capacity = 64, string keyScope = null)
        {
            slots = new List<Slot>(capacity);
            slotByRuntimeKey = new Dictionary<int, int>(capacity);
            slotByEnumKey = new Dictionary<int, int>(capacity);
            slotByStringKey = new Dictionary<string, int>(capacity);
            reservedRuntimeKeyByString = new Dictionary<string, int>(capacity, StringComparer.Ordinal);
            reservedStringRuntimeKeys = new HashSet<int>();
            conflicts = new List<ESConfigKeyConflict>(8);
            this.keyScope = string.IsNullOrEmpty(keyScope) ? typeof(TData).FullName : keyScope;
        }

        public int Count => slots.Count;
        public int ConflictCount => conflicts.Count;
        public bool IsBuilding => isBuilding;
        public IReadOnlyList<ESConfigKeyConflict> Conflicts => conflicts;
        public string KeyScope => keyScope;
        public string SchemaHash => schemaHash ?? string.Empty;

        /// <summary>Editor/diagnostic snapshot. Runtime callers should resolve and cache a single key instead.</summary>
        public void CopyEntries(List<ESConfigKeyTableEntry> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));

            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (!slot.valid)
                    continue;

                destination.Add(new ESConfigKeyTableEntry
                {
                    runtimeKey = slot.runtimeKey,
                    enumKey = slot.enumKey,
                    stringKey = slot.stringKey,
                    debugName = slot.debugName
                });
            }
        }

        public ESKeyCatalogHandshake CreateSchemaHandshake()
        {
            if (isBuilding)
                throw new InvalidOperationException("ConfigKeyTable must finish building before its schema is used for a handshake.");
            if (string.IsNullOrEmpty(schemaHash))
                schemaHash = CalculateSchemaHash();
            return new ESKeyCatalogHandshake { catalogName = keyScope, schemaHash = schemaHash };
        }

        public bool IsCompatibleWith(ESKeyCatalogHandshake peer, out string error)
        {
            ESKeyCatalogHandshake local = CreateSchemaHandshake();
            if (!string.Equals(local.catalogName, peer.catalogName, StringComparison.Ordinal))
            {
                error = "Catalog name mismatch. local=" + local.catalogName + ", peer=" + peer.catalogName;
                return false;
            }

            if (!string.Equals(local.schemaHash, peer.schemaHash, StringComparison.OrdinalIgnoreCase))
            {
                error = "Catalog schema mismatch. local=" + local.schemaHash + ", peer=" + peer.schemaHash;
                return false;
            }

            error = null;
            return true;
        }

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
            schemaHash = CalculateSchemaHash();
        }

        public void Clear()
        {
            EnsureCanBuild();
            for (int i = 0; i < slots.Count; i++)
            {
                Slot entry = slots[i];
                if (entry.valid && entry.data != null)
                    OnDataReleased(entry.data);
            }
            slots.Clear();
            slotByRuntimeKey.Clear();
            slotByEnumKey.Clear();
            slotByStringKey.Clear();
            reservedRuntimeKeyByString.Clear();
            reservedStringRuntimeKeys.Clear();
            conflicts.Clear();
            schemaHash = null;
        }

        public int Bake<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key)
            where TEnumKey : struct, Enum
        {
            EnsureCanBuild();
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (!key.IsConfigured)
                throw new InvalidOperationException("GameCore ConfigKey 必须显式配置 EnumKey 或 StringKey；SoDataInfo.KeyName 仅供编辑器与策划使用，禁止作为运行时回退键。");

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0)
            {
                if (slotByEnumKey.TryGetValue(enumKey, out int enumSlot))
                    return slots[enumSlot].runtimeKey;
                return enumKey;
            }

            return BakeStringRuntimeKey(key.StringKey);
        }

        public bool Register<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string debugName = null)
            where TEnumKey : struct, Enum
        {
            return RegisterAndGetRuntimeKey(key, data, debugName) != 0;
        }

        /// <summary>
        /// 单条 GameCore 数据的一行式注入入口。没有外层构建周期时自动开启并关闭；
        /// 批量构建中调用时直接复用现有周期。返回当前表生成的临时 RuntimeKey；
        /// 无效输入或 Key 冲突时抛出异常，允许失败的流程应使用 TryInject。
        /// </summary>
        public int Inject<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string debugName = null)
            where TEnumKey : struct, Enum
        {
            if (TryInject(key, data, out int runtimeKey, debugName))
                return runtimeKey;

            throw CreateInjectException(key, data, "GameCore");
        }

        /// <summary>
        /// GameCore 单条安全注入入口。成功时返回当前表 RuntimeKey；无效配置或
        /// 同 Key 被不同数据实例占用时返回 false，不替换已有数据。
        /// </summary>
        public bool TryInject<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, out int runtimeKey, string debugName = null)
            where TEnumKey : struct, Enum
        {
            runtimeKey = 0;
            if (key == null || !key.IsConfigured || data == null)
                return false;

            bool ownsBuild = !isBuilding;
            if (ownsBuild)
                BeginBuild(false);

            try
            {
                runtimeKey = RegisterAndGetRuntimeKey(key, data, debugName);
                return runtimeKey != 0;
            }
            finally
            {
                if (ownsBuild)
                    EndBuild();
            }
        }

        /// <summary>
        /// 直接使用 StringKey 注入。StringKey 只在当前 TData 强类型表内唯一；
        /// 返回的 RuntimeKey 仍是当前表、当前生命周期内的临时加速索引。
        /// </summary>
        public int Inject(string stringKey, TData data, string debugName = null)
        {
            if (TryInject(stringKey, data, out int runtimeKey, debugName))
                return runtimeKey;

            string keyDescription = string.IsNullOrEmpty(stringKey) ? "<empty>" : stringKey;
            string dataType = data != null ? data.GetType().FullName : "<null>";
            throw new InvalidOperationException(
                "[ESConfigKeyTable][StringKey] 注入失败：StringKey 未配置、数据为空，或同 Key 已被不同数据实例占用。"
                + " key=" + keyDescription
                + ", dataType=" + dataType
                + "。允许失败的流程请使用 TryInject；是否允许替换由具体表类型的生命周期规则决定。");
        }

        /// <summary>直接使用 StringKey 安全注入；失败时不替换已有数据。</summary>
        public bool TryInject(string stringKey, TData data, out int runtimeKey, string debugName = null)
        {
            runtimeKey = 0;
            if (string.IsNullOrEmpty(stringKey) || data == null)
                return false;

            bool ownsBuild = !isBuilding;
            if (ownsBuild)
                BeginBuild(false);

            try
            {
                runtimeKey = BakeStringRuntimeKey(stringKey);
                bool registered = RegisterInternal(
                    runtimeKey,
                    0,
                    stringKey,
                    data,
                    string.IsNullOrEmpty(debugName) ? stringKey : debugName);
                if (!registered)
                    runtimeKey = 0;
                return registered;
            }
            finally
            {
                if (ownsBuild)
                    EndBuild();
            }
        }

        /// <summary>
        /// 在当前表构建周期内一次性解析并注册 GameCore Key，返回本次实际写入槽位的
        /// RuntimeKey。RuntimeKey 仅是当前类型表、当前生命周期内的加速索引，不具备
        /// 跨重建、跨进程、存档或网络权威。
        /// </summary>
        public int RegisterAndGetRuntimeKey<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string debugName = null)
            where TEnumKey : struct, Enum
        {
            int enumKey = key != null ? key.EnumKeyInt : 0;
            string stringKey = key != null ? key.StringKey : null;
            int runtimeKey = Bake(key);
            if (!RegisterInternal(runtimeKey, enumKey, stringKey, data, string.IsNullOrEmpty(debugName) ? stringKey : debugName))
                return 0;

            // 补充别名时可能复用既有槽位，其真实 RuntimeKey 不一定等于本次 Bake 值。
            return TryGetRuntimeKeyCore(enumKey, stringKey, out int committedRuntimeKey)
                ? committedRuntimeKey
                : 0;
        }

        public bool Upsert<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string debugName = null)
            where TEnumKey : struct, Enum
        {
            int enumKey = key != null ? key.EnumKeyInt : 0;
            string stringKey = key != null ? key.StringKey : null;
            int runtimeKey = Bake(key);
            return UpsertInternal(runtimeKey, enumKey, stringKey, data, string.IsNullOrEmpty(debugName) ? stringKey : debugName);
        }

        public int Bake<TEnumKey>(ESAssetConfigKey<TEnumKey> key, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            EnsureCanBuild();
            if (key == null)
                return 0;

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0)
            {
                if (slotByEnumKey.TryGetValue(enumKey, out int enumSlot))
                    return slots[enumSlot].runtimeKey;
                return enumKey;
            }

            return BakeStringRuntimeKey(key.GetStringKey(fallbackStringKey));
        }

        public bool Register<TEnumKey>(ESAssetConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            return RegisterAndGetRuntimeKey(key, data, fallbackStringKey) != 0;
        }

        /// <summary>
        /// 单条资产配置的一行式注入入口。没有外层构建周期时自动开启并关闭；
        /// 批量构建中调用时直接复用现有周期。返回当前表生成的临时 RuntimeKey；
        /// 无效输入或 Key 冲突时抛出异常，允许失败的流程应使用 TryInject。
        /// </summary>
        public int Inject<TEnumKey>(ESAssetConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            if (TryInject(key, data, out int runtimeKey, fallbackStringKey))
                return runtimeKey;

            throw CreateInjectException(
                key,
                data,
                "Asset",
                key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey);
        }

        /// <summary>
        /// 资产配置单条安全注入入口。成功时返回当前表 RuntimeKey；无效配置或
        /// 同 Key 被不同数据实例占用时返回 false，不替换已有数据。
        /// </summary>
        public bool TryInject<TEnumKey>(ESAssetConfigKey<TEnumKey> key, TData data, out int runtimeKey, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            runtimeKey = 0;
            string stringKey = key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey;
            if (key == null || data == null || !ESConfigKeyMatch.IsConfigured(key.EnumKeyInt, stringKey))
                return false;

            bool ownsBuild = !isBuilding;
            if (ownsBuild)
                BeginBuild(false);

            try
            {
                runtimeKey = RegisterAndGetRuntimeKey(key, data, fallbackStringKey);
                return runtimeKey != 0;
            }
            finally
            {
                if (ownsBuild)
                    EndBuild();
            }
        }

        /// <summary>
        /// 由资产业务键在当前表内自动生成临时 RuntimeKey 并原子注册。
        /// 调用方不得传入、恢复或持久化 RuntimeKey。
        /// </summary>
        public int RegisterAndGetRuntimeKey<TEnumKey>(ESAssetConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            string stringKey = key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey;
            return RegisterConfiguredAndGetRuntimeKey(key, data, stringKey, stringKey);
        }

        /// <summary>
        /// 派生的强类型资产表在 Catalog/Page 冷路径中使用。调用方必须先完成类型校验；
        /// 本方法复用正式注册的 RuntimeKey、别名和冲突语义，不向普通业务代码开放。
        /// </summary>
        protected int RegisterConfiguredAndGetRuntimeKey(
            IESConfigKey key,
            TData data,
            string effectiveStringKey,
            string debugName)
        {
            EnsureCanBuild();
            if (key == null)
                return 0;

            int enumKey = key.EnumKeyInt;
            int runtimeKey;
            if (enumKey != 0)
            {
                runtimeKey = slotByEnumKey.TryGetValue(enumKey, out int enumSlot)
                    ? slots[enumSlot].runtimeKey
                    : enumKey;
            }
            else
            {
                runtimeKey = BakeStringRuntimeKey(effectiveStringKey);
            }

            if (!RegisterInternal(runtimeKey, enumKey, effectiveStringKey, data, debugName))
                return 0;

            return TryGetRuntimeKeyCore(enumKey, effectiveStringKey, out int committedRuntimeKey)
                ? committedRuntimeKey
                : 0;
        }

        public bool Upsert<TEnumKey>(ESAssetConfigKey<TEnumKey> key, TData data, string fallbackStringKey = null)
            where TEnumKey : struct, Enum
        {
            int enumKey = key != null ? key.EnumKeyInt : 0;
            string stringKey = key != null ? key.GetStringKey(fallbackStringKey) : fallbackStringKey;
            int runtimeKey = Bake(key, fallbackStringKey);
            return UpsertInternal(runtimeKey, enumKey, stringKey, data, stringKey);
        }

        public virtual bool TryGet(int runtimeKey, out TData data)
        {
            if (TryGetCore(runtimeKey, out data))
                return true;

            ESConfigKeyDiagnostics.ReportMissing(keyScope, "RuntimeKey=" + runtimeKey);
            return false;
        }

        protected bool TryGetCore(int runtimeKey, out TData data)
        {
            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int slot))
                return TryGetBySlotCore(slot, out data);

            data = null;
            return false;
        }

        public virtual bool TryGet<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, out TData data)
            where TEnumKey : struct, Enum
        {
            if (key == null)
            {
                data = null;
                return false;
            }

            if (TryGetRuntimeKey(key, out int runtimeKey))
                return TryGet(runtimeKey, out data);

            data = null;
            ESConfigKeyDiagnostics.ReportMissing(keyScope, key.ToString());
            return false;
        }

        public virtual bool TryGet<TEnumKey>(ESAssetConfigKey<TEnumKey> key, out TData data)
            where TEnumKey : struct, Enum
        {
            if (key == null)
            {
                data = null;
                return false;
            }

            if (TryGetRuntimeKey(key, out int runtimeKey))
                return TryGet(runtimeKey, out data);

            data = null;
            ESConfigKeyDiagnostics.ReportMissing(keyScope, key.ToString());
            return false;
        }

        public virtual TData Get(int runtimeKey)
        {
            return TryGet(runtimeKey, out TData data) ? data : null;
        }

        public virtual bool TryGetRuntimeKey(string stringKey, out int runtimeKey)
        {
            if (TryGetRuntimeKeyCore(stringKey, out runtimeKey))
                return true;

            ESConfigKeyDiagnostics.ReportMissing(keyScope, "StringKey=" + stringKey);
            return false;
        }

        protected bool TryGetRuntimeKeyCore(string stringKey, out int runtimeKey)
        {
            if (TryGetSlotByStringKeyCore(stringKey, out int slot))
            {
                runtimeKey = slots[slot].runtimeKey;
                return runtimeKey != 0;
            }

            runtimeKey = 0;
            return false;
        }

        /// <summary>
        /// Resolves either stable alias. When both aliases are supplied they must already map to
        /// the same entry; a mismatched EnumKey/StringKey pair is rejected instead of silently
        /// preferring the enum.
        /// </summary>
        public virtual bool TryGetRuntimeKey(int enumKey, string stringKey, out int runtimeKey)
        {
            return TryGetRuntimeKeyCore(enumKey, stringKey, out runtimeKey);
        }

        protected bool TryGetRuntimeKeyCore(int enumKey, string stringKey, out int runtimeKey)
        {
            int enumRuntimeKey = 0;
            int stringRuntimeKey = 0;
            bool requestedEnum = enumKey != 0;
            bool requestedString = !string.IsNullOrEmpty(stringKey);
            bool hasEnum = enumKey != 0 && slotByEnumKey.TryGetValue(enumKey, out int enumSlot)
                           && (enumRuntimeKey = slots[enumSlot].runtimeKey) != 0;
            bool hasString = !string.IsNullOrEmpty(stringKey) && TryGetRuntimeKeyCore(stringKey, out stringRuntimeKey);

            if ((requestedEnum && !hasEnum) || (requestedString && !hasString))
            {
                runtimeKey = 0;
                return false;
            }

            if (hasEnum && hasString && enumRuntimeKey != stringRuntimeKey)
            {
                runtimeKey = 0;
                return false;
            }

            runtimeKey = hasEnum ? enumRuntimeKey : stringRuntimeKey;
            return runtimeKey != 0;
        }

        /// <summary>
        /// 从已经注入当前强类型表的 ConfigKey 获取本表 RuntimeKey。
        /// 只读当前表，不触发注册、分配或任何持久化行为。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual bool TryGetRuntimeKey(IESConfigKey key, out int runtimeKey)
        {
            return TryGetRuntimeKeyCore(key, out runtimeKey);
        }

        protected bool TryGetRuntimeKeyCore(IESConfigKey key, out int runtimeKey)
        {
            if (key == null)
            {
                runtimeKey = 0;
                return false;
            }

            int enumKey = key.EnumKeyInt;
            if (enumKey != 0)
            {
                if (slotByEnumKey.TryGetValue(enumKey, out int enumSlot))
                {
                    Slot entry = slots[enumSlot];
                    runtimeKey = entry.runtimeKey;
                    if (!string.IsNullOrEmpty(key.StringKey)
                        && ((!string.IsNullOrEmpty(entry.stringKey)
                             && !string.Equals(entry.stringKey, key.StringKey, StringComparison.Ordinal))
                            || (TryGetRuntimeKeyCore(key.StringKey, out int stringRuntimeKey)
                                && stringRuntimeKey != runtimeKey)))
                    {
                        runtimeKey = 0;
                        return false;
                    }

                    return runtimeKey != 0;
                }

                // A fully populated reference cannot downgrade to its StringKey when its EnumKey
                // does not exist in this catalog; that would hide an alias mismatch.
                runtimeKey = 0;
                return false;
            }

            return TryGetRuntimeKeyCore(key.StringKey, out runtimeKey);
        }

        /// <summary>
        /// 获取已经注入当前强类型表的 ConfigKey 对应 RuntimeKey。
        /// 未注入时抛出异常，适合初始化完成后一次获取并缓存到热路径。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual int GetRuntimeKey(IESConfigKey key)
        {
            if (TryGetRuntimeKey(key, out int runtimeKey))
                return runtimeKey;

            string keyDescription = key == null
                ? "<null>"
                : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
            throw new KeyNotFoundException("[ESConfigKeyTable] ConfigKey 尚未注入当前强类型表：" + keyDescription);
        }

        public virtual bool TryGetByStringKey(string stringKey, out TData data)
        {
            if (TryGetSlotByStringKeyCore(stringKey, out int slot)
                && TryGetBySlotCore(slot, out data))
                return true;

            data = null;
            ESConfigKeyDiagnostics.ReportMissing(keyScope, "StringKey=" + stringKey);
            return false;
        }

        public virtual bool TryGetSlot(int runtimeKey, out int slot)
        {
            return slotByRuntimeKey.TryGetValue(runtimeKey, out slot);
        }

        public virtual bool TryGetSlotByEnumKey(int enumKey, out int slot)
        {
            return TryGetSlotByEnumKeyCore(enumKey, out slot);
        }

        protected bool TryGetSlotByEnumKeyCore(int enumKey, out int slot)
        {
            if (enumKey == 0)
            {
                slot = -1;
                return false;
            }

            return slotByEnumKey.TryGetValue(enumKey, out slot);
        }

        public virtual bool TryGetSlotByStringKey(string stringKey, out int slot)
        {
            return TryGetSlotByStringKeyCore(stringKey, out slot);
        }

        protected bool TryGetSlotByStringKeyCore(string stringKey, out int slot)
        {
            if (string.IsNullOrEmpty(stringKey))
            {
                slot = -1;
                return false;
            }

            return slotByStringKey.TryGetValue(stringKey, out slot);
        }

        public virtual bool TryGetBySlot(int slot, out TData data)
        {
            return TryGetBySlotCore(slot, out data);
        }

        protected bool TryGetBySlotCore(int slot, out TData data)
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

        public virtual bool TryGetRuntimeKeyBySlot(int slot, out int runtimeKey)
        {
            if ((uint)slot < (uint)slots.Count && slots[slot].valid)
            {
                runtimeKey = slots[slot].runtimeKey;
                return runtimeKey != 0;
            }

            runtimeKey = 0;
            return false;
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
            if (entry.enumKey != 0)
                slotByEnumKey.Remove(entry.enumKey);
            if (!string.IsNullOrEmpty(entry.stringKey))
            {
                slotByStringKey.Remove(entry.stringKey);
                if (reservedRuntimeKeyByString.TryGetValue(entry.stringKey, out int reservedRuntimeKey))
                {
                    reservedRuntimeKeyByString.Remove(entry.stringKey);
                    reservedStringRuntimeKeys.Remove(reservedRuntimeKey);
                }
            }

            entry.valid = false;
            entry.version++;
            TData removedData = entry.data;
            entry.runtimeKey = 0;
            entry.enumKey = 0;
            entry.stringKey = null;
            entry.debugName = null;
            entry.data = null;
            slots[slot] = entry;
            OnDataReleased(removedData);
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
            if (TryGetRuntimeKeyCore(enumKey, stringKey, out int existingRuntimeKey))
                return existingRuntimeKey;
            if (enumKey != 0)
                return enumKey;

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

            if (!CanRegisterData(enumKey, stringKey, data))
            {
                AddConflict(runtimeKey, debugName, "The stable business key is already bound to another data instance.");
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

                if (!BindAliasesToSlot(runtimeSlot, enumKey, stringKey))
                {
                    AddConflict(runtimeKey, debugName, "EnumKey/StringKey aliases point at different data slots.");
                    return false;
                }

                ConsumeStringRuntimeKeyReservation(stringKey);
                OnDataRegistered(runtimeKey, enumKey, stringKey, data);
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

                bool bound = BindAliasesToSlot(stringSlot, enumKey, stringKey);
                if (bound)
                {
                    ConsumeStringRuntimeKeyReservation(stringKey);
                    OnDataRegistered(existing.runtimeKey, enumKey, stringKey, data);
                }
                return bound;
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
            if (enumKey != 0)
                slotByEnumKey[enumKey] = slot;
            if (!string.IsNullOrEmpty(stringKey))
                slotByStringKey[stringKey] = slot;

            ConsumeStringRuntimeKeyReservation(stringKey);
            OnDataRegistered(runtimeKey, enumKey, stringKey, data);
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

            if (!CanRegisterData(enumKey, stringKey, data))
            {
                AddConflict(runtimeKey, debugName, "The stable business key is already bound to another data instance.");
                return false;
            }

            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int runtimeSlot))
            {
                ReplaceSlotData(runtimeSlot, runtimeKey, enumKey, stringKey, data, debugName);
                ConsumeStringRuntimeKeyReservation(stringKey);
                return true;
            }

            if (!string.IsNullOrEmpty(stringKey) && slotByStringKey.TryGetValue(stringKey, out int stringSlot))
            {
                ReplaceSlotData(stringSlot, slots[stringSlot].runtimeKey, enumKey, stringKey, data, debugName);
                ConsumeStringRuntimeKeyReservation(stringKey);
                return true;
            }

            return RegisterInternal(runtimeKey, enumKey, stringKey, data, debugName);
        }

        private void ReplaceSlotData(int slot, int runtimeKey, int enumKey, string stringKey, TData data, string debugName)
        {
            Slot entry = slots[slot];
            TData replacedData = entry.data;

            if (entry.runtimeKey != runtimeKey)
            {
                if (entry.runtimeKey != 0)
                    slotByRuntimeKey.Remove(entry.runtimeKey);

                slotByRuntimeKey[runtimeKey] = slot;
            }

            if (entry.enumKey != enumKey)
            {
                if (entry.enumKey != 0)
                    slotByEnumKey.Remove(entry.enumKey);
                if (enumKey != 0)
                    slotByEnumKey[enumKey] = slot;
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
            OnDataRegistered(runtimeKey, enumKey, stringKey, data);
            if (replacedData != null && !ReferenceEquals(replacedData, data))
                OnDataReleased(replacedData);
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

        /// <summary>
        /// Adds missing aliases without changing the entry's RuntimeKey. This preserves process
        /// handles when a partial Enum/String reference is resolved after the canonical entry.
        /// </summary>
        private bool BindAliasesToSlot(int slot, int enumKey, string stringKey)
        {
            if ((uint)slot >= (uint)slots.Count || !slots[slot].valid)
                return false;
            if (enumKey != 0 && slotByEnumKey.TryGetValue(enumKey, out int enumSlot) && enumSlot != slot)
                return false;
            if (!string.IsNullOrEmpty(stringKey)
                && slotByStringKey.TryGetValue(stringKey, out int stringSlot)
                && stringSlot != slot)
                return false;

            Slot entry = slots[slot];
            if (entry.enumKey == 0 && enumKey != 0)
                entry.enumKey = enumKey;
            if (string.IsNullOrEmpty(entry.stringKey) && !string.IsNullOrEmpty(stringKey))
                entry.stringKey = stringKey;

            if (enumKey != 0)
                slotByEnumKey[enumKey] = slot;
            if (!string.IsNullOrEmpty(stringKey))
                slotByStringKey[stringKey] = slot;
            slots[slot] = entry;
            return true;
        }

        private int BakeStringRuntimeKey(string stringKey)
        {
            if (string.IsNullOrEmpty(stringKey))
                return 0;

            if (TryGetRuntimeKeyCore(stringKey, out int runtimeKey))
                return runtimeKey;

            if (reservedRuntimeKeyByString.TryGetValue(stringKey, out runtimeKey))
                return runtimeKey;

            runtimeKey = CalculateDeterministicStringRuntimeKey(stringKey);
            if (slotByRuntimeKey.TryGetValue(runtimeKey, out int occupiedSlot)
                && !string.Equals(slots[occupiedSlot].stringKey, stringKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "[ESConfigKeyTable] StringKey RuntimeKey hash collision in scope " + keyScope
                    + ". Existing=" + (slots[occupiedSlot].stringKey ?? string.Empty)
                    + ", incoming=" + stringKey + ". Rename one stable StringKey.");
            }

            foreach (KeyValuePair<string, int> reservation in reservedRuntimeKeyByString)
            {
                if (reservation.Value == runtimeKey && !string.Equals(reservation.Key, stringKey, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "[ESConfigKeyTable] Pending StringKey RuntimeKey hash collision in scope " + keyScope
                        + ". Existing=" + reservation.Key + ", incoming=" + stringKey + ". Rename one stable StringKey.");
            }

            reservedRuntimeKeyByString[stringKey] = runtimeKey;
            reservedStringRuntimeKeys.Add(runtimeKey);
            return runtimeKey;
        }

        private int CalculateDeterministicStringRuntimeKey(string stringKey)
        {
            ulong hash = ESKeyHash.Fnv1A64(keyScope + "\u001F" + stringKey);
            long available = (long)int.MaxValue - ESConfigKeyProtocol.DeterministicStringRuntimeKeyStart;
            return ESConfigKeyProtocol.DeterministicStringRuntimeKeyStart + (int)(hash % (ulong)available);
        }

        private void ConsumeStringRuntimeKeyReservation(string stringKey)
        {
            if (string.IsNullOrEmpty(stringKey)
                || !reservedRuntimeKeyByString.TryGetValue(stringKey, out int reservedRuntimeKey))
                return;

            reservedRuntimeKeyByString.Remove(stringKey);
            reservedStringRuntimeKeys.Remove(reservedRuntimeKey);
        }

        private string CalculateSchemaHash()
        {
            List<Slot> activeSlots = new List<Slot>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].valid)
                    activeSlots.Add(slots[i]);
            }

            activeSlots.Sort((left, right) =>
            {
                int enumCompare = left.enumKey.CompareTo(right.enumKey);
                return enumCompare != 0
                    ? enumCompare
                    : string.CompareOrdinal(left.stringKey, right.stringKey);
            });

            ulong hash = ESKeyHash.Fnv1A64("ESConfigKeyTable/v2");
            hash = ESKeyHash.Append(hash, keyScope);
            hash = ESKeyHash.Append(hash, typeof(TData).FullName);
            for (int i = 0; i < activeSlots.Count; i++)
            {
                Slot entry = activeSlots[i];
                hash = ESKeyHash.Append(hash, (ushort)entry.enumKey);
                hash = ESKeyHash.Append(hash, entry.stringKey);
            }

            return hash.ToString("X16");
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

        /// <summary>派生表在领域冷路径预检中记录确定性冲突；不得从查询热路径调用。</summary>
        protected void RecordConflict(int runtimeKey, string stringKey, string reason)
        {
            AddConflict(runtimeKey, stringKey, reason);
        }

        private static InvalidOperationException CreateInjectException(
            IESConfigKey key,
            TData data,
            string domain,
            string effectiveStringKey = null)
        {
            string keyDescription = key == null
                ? "<null>"
                : ESConfigKeyMatch.Describe(key.EnumKeyInt, effectiveStringKey ?? key.StringKey);
            string dataType = data != null ? data.GetType().FullName : "<null>";
            return new InvalidOperationException(
                "[ESConfigKeyTable][" + domain + "] 注入失败：Key 未配置、数据为空，或同 Key 已被不同数据实例占用。"
                + " key=" + keyDescription
                + ", dataType=" + dataType
                + "。允许失败的流程请使用 TryInject；是否允许替换由具体表类型的生命周期规则决定。");
        }

        /// <summary>注册前的数据实例约束；稳定驻留表用它禁止同一业务 Key 切换实例。</summary>
        protected virtual bool CanRegisterData(int enumKey, string stringKey, TData data) => true;

        /// <summary>数据成功进入当前表生命周期后触发；RuntimeKey 是最终活动槽位的实际值。</summary>
        protected virtual void OnDataRegistered(int runtimeKey, int enumKey, string stringKey, TData data)
        {
            OnDataRegistered(enumKey, stringKey, data);
        }

        /// <summary>保留给既有扩展的兼容钩子；新实现应重写包含 RuntimeKey 的重载。</summary>
        protected virtual void OnDataRegistered(int enumKey, string stringKey, TData data) { }

        /// <summary>Table 放弃某条数据的活动状态时触发。</summary>
        protected virtual void OnDataReleased(TData data) { }

        /// <summary>
        /// 仅供冷路径事务回滚判断：确认某个数据实例是否仍属于活动槽位。
        /// 使用引用比较，不参与正常查询热路径，也不产生托管分配。
        /// </summary>
        protected bool ContainsRegisteredDataReference(TData data)
        {
            if (data == null)
                return false;

            for (int i = 0; i < slots.Count; i++)
            {
                Slot entry = slots[i];
                if (entry.valid && ReferenceEquals(entry.data, data))
                    return true;
            }

            return false;
        }

        private void EnsureCanBuild()
        {
            if (!isBuilding)
                throw new InvalidOperationException("ESConfigKeyTable is locked. Use BeginBuild/EndBuild during initialization or hot-load only.");
        }
    }

    /// <summary>
    /// 按类别内 EnumKey/StringKey 稳定绑定数据实例的标准 ConfigKey 表。
    /// <para>
    /// “驻留”只保证同一业务 Key 始终取得同一个 <typeparamref name="TData"/> 对象引用；
    /// 不代表对象内部的领域载荷常驻。Clear/Remove 仅移除当前活动槽位，驻留映射继续保留，
    /// 同 Key 后续可通过 <see cref="AcquireRetained"/> 取得原实例并重新注册。
    /// </para>
    /// <para>
    /// 本层只实现稳定实例约束和生命周期钩子，不管理对象池、Ready、RuntimeKey 字段、
    /// Unity 资产、Loader、Handle 或 AssetBundle。派生领域必须在钩子中实现自己的载荷生命周期。
    /// RuntimeKey 仍只属于当前强类型表和当前进程，不是驻留身份，也不得持久化。
    /// </para>
    /// </summary>
    /// <typeparam name="TData">按业务 Key 稳定驻留的引用类型。</typeparam>
    public class ESRetainedConfigKeyTable<TData> : ESConfigKeyTable<TData> where TData : class
    {
        private readonly Dictionary<int, TData> retainedByEnumKey;
        private readonly Dictionary<string, TData> retainedByStringKey;

        public ESRetainedConfigKeyTable(int capacity = 64, string keyScope = null) : base(capacity, keyScope)
        {
            retainedByEnumKey = new Dictionary<int, TData>(capacity);
            retainedByStringKey = new Dictionary<string, TData>(capacity, StringComparer.Ordinal);
        }

        /// <summary>当前驻留的 EnumKey/StringKey 绑定总数；同一实例拥有两个别名时计为两项。</summary>
        public int RetainedCount => retainedByEnumKey.Count + retainedByStringKey.Count;

        /// <summary>
        /// 取得业务 Key 对应的稳定实例。已有绑定时不会调用工厂；首次绑定时工厂必须返回非空实例。
        /// EnumKey 与 StringKey 已分别指向不同实例时抛出异常，禁止静默合并或替换。
        /// </summary>
        public TData AcquireRetained(IESConfigKey key, Func<TData> factory)
        {
            if (key == null || !ESConfigKeyMatch.IsConfigured(key.EnumKeyInt, key.StringKey))
                throw new ArgumentException("稳定驻留外壳必须配置 EnumKey 或 StringKey。", nameof(key));

            if (TryAcquireRetained(key, factory, out TData data))
                return data;

            throw new InvalidOperationException(
                "EnumKey/StringKey 已分别绑定到不同驻留实例，或首次创建工厂为空/返回 null。 key="
                + ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey));
        }

        /// <summary>
        /// 尝试取得业务 Key 对应的稳定实例。该入口只用于初始化、重建等冷路径；
        /// 成功后仍需由领域代码填充载荷并注册到当前活动 Table。
        /// </summary>
        public bool TryAcquireRetained(IESConfigKey key, Func<TData> factory, out TData data)
        {
            data = null;
            if (key == null || !ESConfigKeyMatch.IsConfigured(key.EnumKeyInt, key.StringKey))
                return false;

            int enumKey = key.EnumKeyInt;
            string stringKey = key.StringKey;
            TData byEnum = null;
            TData byString = null;
            bool hasEnum = enumKey != 0 && retainedByEnumKey.TryGetValue(enumKey, out byEnum);
            bool hasString = !string.IsNullOrEmpty(stringKey)
                && retainedByStringKey.TryGetValue(stringKey, out byString);

            if (hasEnum && hasString && !ReferenceEquals(byEnum, byString))
                return false;

            data = hasEnum ? byEnum : hasString ? byString : factory?.Invoke();
            if (data == null)
                return false;

            if (enumKey != 0)
                retainedByEnumKey[enumKey] = data;
            if (!string.IsNullOrEmpty(stringKey))
                retainedByStringKey[stringKey] = data;

            OnRetainedAcquired(data);
            return true;
        }

        protected sealed override bool CanRegisterData(int enumKey, string stringKey, TData data)
        {
            bool enumValid = enumKey == 0
                || !retainedByEnumKey.TryGetValue(enumKey, out TData retainedByEnum)
                || ReferenceEquals(retainedByEnum, data);
            bool stringValid = string.IsNullOrEmpty(stringKey)
                || !retainedByStringKey.TryGetValue(stringKey, out TData retainedByString)
                || ReferenceEquals(retainedByString, data);
            return enumValid && stringValid && CanRegisterRetainedData(enumKey, stringKey, data);
        }

        protected sealed override void OnDataRegistered(
            int runtimeKey,
            int enumKey,
            string stringKey,
            TData data)
        {
            if (enumKey != 0)
                retainedByEnumKey[enumKey] = data;
            if (!string.IsNullOrEmpty(stringKey))
                retainedByStringKey[stringKey] = data;

            OnRetainedRegistered(runtimeKey, enumKey, stringKey, data);
        }

        protected sealed override void OnDataReleased(TData data)
        {
            OnRetainedReleased(data);
        }

        /// <summary>每次成功取得稳定实例后触发；不得在此创建热路径对象或改变业务 Key 绑定。</summary>
        protected virtual void OnRetainedAcquired(TData data) { }

        /// <summary>
        /// 派生领域的附加注册约束。稳定 Key 的实例一致性已经由本层先行校验，
        /// 派生类不得用该钩子实现替换、解绑或绕过稳定实例规则。
        /// </summary>
        protected virtual bool CanRegisterRetainedData(int enumKey, string stringKey, TData data) => true;

        /// <summary>实例成功进入当前活动表后触发；RuntimeKey 为活动槽位的实际临时索引。</summary>
        protected virtual void OnRetainedRegistered(
            int runtimeKey,
            int enumKey,
            string stringKey,
            TData data) { }

        /// <summary>
        /// 实例退出当前活动表时触发。派生类应在此释放领域载荷，但不得删除驻留绑定或替换实例。
        /// </summary>
        protected virtual void OnRetainedReleased(TData data) { }
    }

    /// <summary>
    /// GameCore 定义数据的稳定外壳基类。对象由业务 Key 首次出现时创建一次，随后随强类型表稳定驻留；
    /// 它不是运行实例，不进入对象池。Clear/Remove/Provider 切换只会令其 Ready=false 并释放重量级载荷。
    /// </summary>
    public abstract class ESGameCoreRuntimeData
    {
        /// <summary>
        /// 当前类型表、当前表生命周期内的临时加速索引。由 Table 成功提交时写入；
        /// 禁止持久化、联网同步或跨进程解释。Ready=false 时不得用于读取业务载荷。
        /// </summary>
        [NonSerialized] public int runtimeKey;

        /// <summary>
        /// 当前稳定记录是否已经进入活动 Table。允许缓存 RuntimeData 引用，但读取业务字段前
        /// 必须检查 Ready；Clear/Remove 后原对象身份保持不变，但重量级业务载荷会被释放。
        /// </summary>
        [field: NonSerialized]
        public bool Ready { get; private set; }

        internal void MarkReady(int committedRuntimeKey)
        {
            runtimeKey = committedRuntimeKey;
            Ready = true;
        }

        internal void MarkNotReady()
        {
            Ready = false;
            ReleaseRuntimePayload();
        }

        /// <summary>
        /// 稳定外壳退出活动态时断开其资源与配置载荷强引用。底层 Lease/Handle 仍由 AssetScope 统一释放；
        /// 此处不得直接调用 Loader，避免重复 Release。
        /// </summary>
        protected abstract void ReleaseRuntimePayload();
    }

    /// <summary>
    /// 按业务 Key 稳定驻留的 GameCore 表。成功绑定的对象不会被删除或分配给其他 Key；
    /// Clear/Remove 会置 Ready=false 并释放重量级载荷，同 Key 重建时复用原实例。
    /// </summary>
    public class ESGameCoreConfigKeyTable<TData> : ESRetainedConfigKeyTable<TData>
        where TData : ESGameCoreRuntimeData, new()
    {
        private static readonly Func<TData> DataFactory = CreateData;

        public ESGameCoreConfigKeyTable(int capacity = 64, string keyScope = null) : base(capacity, keyScope) { }

        private static TData CreateData() => new TData();

        /// <summary>
        /// 驻留 RuntimeData 的统一强失败提交入口。成功时先同步 RuntimeKey，再置 Ready=true；
        /// 失败或异常时自动释放本次填入的载荷，并保持稳定外壳 Ready=false。
        /// </summary>
        public int CommitRetained<TEnumKey>(
            ESGameCoreConfigKey<TEnumKey> key,
            TData data,
            string debugName = null)
            where TEnumKey : struct, Enum
        {
            try
            {
                return base.Inject(key, data, debugName);
            }
            catch
            {
                AbandonRetained(data);
                throw;
            }
        }

        /// <summary>
        /// 驻留 RuntimeData 的统一可失败提交入口。失败返回 false，且不会留下本次载荷引用；
        /// 非业务冲突异常仍向上抛出，但抛出前同样完成回滚。
        /// </summary>
        public bool TryCommitRetained<TEnumKey>(
            ESGameCoreConfigKey<TEnumKey> key,
            TData data,
            out int runtimeKey,
            string debugName = null)
            where TEnumKey : struct, Enum
        {
            try
            {
                if (base.TryInject(key, data, out runtimeKey, debugName))
                    return true;

                AbandonRetained(data);
                return false;
            }
            catch
            {
                runtimeKey = 0;
                AbandonRetained(data);
                throw;
            }
        }

        /// <summary>
        /// 显式放弃一次尚未提交或提交失败的驻留注入。该操作幂等，只释放业务载荷，
        /// 不回收稳定外壳，也不解除业务 Key 与外壳的稳定绑定。
        /// </summary>
        public void AbandonRetained(TData data)
        {
            // 事务回滚不得破坏已提交槽位。扫描仅发生在显式放弃或失败路径。
            if (ContainsRegisteredDataReference(data))
                return;

            data?.MarkNotReady();
        }

        // 保留既有公共 API 名称，同时令驻留表的所有常用注入入口统一获得事务语义。
        public new int Inject<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string debugName = null)
            where TEnumKey : struct, Enum
            => CommitRetained(key, data, debugName);

        public new bool TryInject<TEnumKey>(
            ESGameCoreConfigKey<TEnumKey> key,
            TData data,
            out int runtimeKey,
            string debugName = null)
            where TEnumKey : struct, Enum
            => TryCommitRetained(key, data, out runtimeKey, debugName);

        public new bool Register<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, TData data, string debugName = null)
            where TEnumKey : struct, Enum
            => TryCommitRetained(key, data, out _, debugName);

        public new int RegisterAndGetRuntimeKey<TEnumKey>(
            ESGameCoreConfigKey<TEnumKey> key,
            TData data,
            string debugName = null)
            where TEnumKey : struct, Enum
        {
            try
            {
                int committedRuntimeKey = base.RegisterAndGetRuntimeKey(key, data, debugName);
                if (committedRuntimeKey != 0)
                    return committedRuntimeKey;

                AbandonRetained(data);
                return 0;
            }
            catch
            {
                AbandonRetained(data);
                throw;
            }
        }

        public TData AcquireRetained<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key)
            where TEnumKey : struct, Enum
        {
            if (TryAcquireRetained(key, out TData data))
                return data;

            throw new InvalidOperationException(
                "[ESGameCore][Retained] Key 无效、当前仍为 Ready，或稳定别名发生冲突："
                + (key == null ? "<null>" : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey)));
        }

        public bool TryAcquireRetained<TEnumKey>(ESGameCoreConfigKey<TEnumKey> key, out TData data)
            where TEnumKey : struct, Enum
        {
            data = null;
            if (key == null || !key.IsConfigured || TryGet(key, out _))
                return false;

            return base.TryAcquireRetained(key, DataFactory, out data);
        }

        protected override void OnRetainedAcquired(TData data)
        {
            data.MarkNotReady();
        }

        protected override void OnRetainedRegistered(int runtimeKey, int enumKey, string stringKey, TData data)
        {
            data.MarkReady(runtimeKey);
        }

        protected override void OnRetainedReleased(TData data)
        {
            data?.MarkNotReady();
        }
    }

    [Serializable]
    public struct ESConfigKeyConflict
    {
        [NonSerialized] public int runtimeKey;
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

