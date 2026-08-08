using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Flags]
    public enum ESGenericProfileDebugEventMask
    {
        None = 0,
        Enabled = 1 << 0,
        Disabled = 1 << 1
    }

    public enum ESGenericProfileLogLevel
    {
        Log = 0,
        Warning = 1,
        Error = 2
    }

    [Serializable]
    public abstract class ESGenericProfileExtensionSettings
        : IESCollectionDefaultOrder, IESNameTitle
    {
        [SerializeField, LabelText("名称")]
        [Tooltip("留空时使用当前 Extension 类型提供的默认名称；填写后作为此实例的自定义名称。")]
        private string nameTitle;

        public abstract string TypeId { get; }
        public abstract int SchemaVersion { get; }
        public abstract int SupportedSchemaVersion { get; }
        public abstract int DefaultOrder { get; }
        public abstract string NameTitleDefault { get; }
        public abstract bool Enabled { get; }

        public string NameTitle
        {
            get
            {
                if (string.IsNullOrWhiteSpace(nameTitle))
                    return NameTitleDefault;

                return nameTitle.Trim();
            }
            set
            {
                nameTitle = string.IsNullOrWhiteSpace(value)
                    ? null
                    : value.Trim();
            }
        }

        protected internal virtual void OnProfileAwake(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
        }

        protected internal virtual void OnProfileEnable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
        }

        protected internal virtual void OnProfileDisable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
        }

        protected internal virtual void OnProfilePoolSpawned(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
        }

        protected internal virtual void OnProfilePoolDespawned(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
        }

        protected internal virtual void OnProfileDestroy(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
        }

        protected internal virtual bool Validate(
            ESGenericProfile profile,
            ESGenericProfileSettings settings,
            List<string> issues)
        {
            return true;
        }
    }

    [Serializable]
    [TypeRegistryItem("Generic Profile/Player 初始化")]
    public sealed class ESGenericProfilePlayerInitializationSettings : ESGenericProfileExtensionSettings
    {
        public const string StableTypeId = "es.generic.player-initialization";
        public const int CurrentSchemaVersion = 1;
        public const int DefaultOrderValue = -100;
        public const string DefaultNameTitle = "Player 初始化";

        [SerializeField, ReadOnly, LabelText("Schema 版本")]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField, LabelText("启用")]
        private bool enabled;

        [SerializeField, LabelText("销毁 Profile 组件")]
        [Tooltip("只在非 Editor 的 Player 初始化阶段销毁 Profile 组件自身。")]
        private bool destroyProfileComponent;

        public override string TypeId => StableTypeId;
        public override int SchemaVersion => schemaVersion;
        public override int SupportedSchemaVersion => CurrentSchemaVersion;
        public override int DefaultOrder => DefaultOrderValue;
        public override string NameTitleDefault => DefaultNameTitle;
        public override bool Enabled => enabled;
        public bool DestroyProfileComponent => destroyProfileComponent;

        protected internal override void OnProfileAwake(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.TrySchedulePlayerDestroy(destroyProfileComponent);
        }
    }

    [Serializable]
    [TypeRegistryItem("Generic Profile/Debug 边缘日志")]
    public sealed class ESGenericProfileDebugSettings : ESGenericProfileExtensionSettings
    {
        public const string StableTypeId = "es.generic.debug-edges";
        public const int CurrentSchemaVersion = 1;
        public const int DefaultOrderValue = 0;
        public const string DefaultNameTitle = "Debug 边缘日志";

        [SerializeField, ReadOnly, LabelText("Schema 版本")]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField, LabelText("启用")]
        private bool enabled;

        [SerializeField, LabelText("事件")]
        private ESGenericProfileDebugEventMask eventMask = ESGenericProfileDebugEventMask.Enabled;

        [SerializeField, LabelText("日志级别")]
        private ESGenericProfileLogLevel logLevel = ESGenericProfileLogLevel.Log;

        [SerializeField, LabelText("消息")]
        private string message = "ESGenericProfile lifecycle edge.";

        [SerializeField, LabelText("仅 Development")]
        [Tooltip("启用后只在 Editor 或 Development Build 输出。")]
        private bool developmentOnly = true;

        public override string TypeId => StableTypeId;
        public override int SchemaVersion => schemaVersion;
        public override int SupportedSchemaVersion => CurrentSchemaVersion;
        public override int DefaultOrder => DefaultOrderValue;
        public override string NameTitleDefault => DefaultNameTitle;
        public override bool Enabled => enabled;
        public ESGenericProfileDebugEventMask EventMask => eventMask;
        public ESGenericProfileLogLevel LogLevel => logLevel;
        public string Message => message;
        public bool DevelopmentOnly => developmentOnly;

        protected internal override void OnProfileEnable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.WriteConfiguredDebug(this, ESGenericProfileDebugEventMask.Enabled);
        }

        protected internal override void OnProfileDisable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.WriteConfiguredDebug(this, ESGenericProfileDebugEventMask.Disabled);
        }
    }

    [Serializable]
    [TypeRegistryItem("Generic Profile/子 Prefab 装配")]
    public sealed class ESGenericProfileChildPrefabSettings : ESGenericProfileExtensionSettings
    {
        public const string StableTypeId = "es.generic.child-prefab";
        public const int CurrentSchemaVersion = 1;
        public const int DefaultOrderValue = 10;
        public const string DefaultNameTitle = "子 Prefab 装配";

        [SerializeField, ReadOnly, LabelText("Schema 版本")]
        private int schemaVersion = CurrentSchemaVersion;

        [SerializeField, LabelText("启用")]
        private bool enabled;

        [SerializeField, LabelText("Prefab")]
        private GameObject prefab;

        [SerializeField, LabelText("父节点")]
        [Tooltip("必须是 Profile 根 Transform 或其后代；实例生命周期跟随 Profile，重复生命周期通知不会重复创建。")]
        private Transform parent;

        public override string TypeId => StableTypeId;
        public override int SchemaVersion => schemaVersion;
        public override int SupportedSchemaVersion => CurrentSchemaVersion;
        public override int DefaultOrder => DefaultOrderValue;
        public override string NameTitleDefault => DefaultNameTitle;
        public override bool Enabled => enabled;
        public GameObject Prefab => prefab;
        public Transform Parent => parent;

        protected internal override void OnProfileAwake(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.EnsureInstantiatedChildActive(this);
        }

        protected internal override void OnProfileEnable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.EnsureInstantiatedChildActive(this);
        }

        protected internal override void OnProfileDisable(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.DeactivateInstantiatedChild();
        }

        protected internal override void OnProfilePoolSpawned(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.EnsureInstantiatedChildActive(this);
        }

        protected internal override void OnProfilePoolDespawned(
            ESGenericProfile profile,
            ESGenericProfileRuntimeContext context)
        {
            profile.DeactivateInstantiatedChild();
        }

        protected internal override bool Validate(
            ESGenericProfile profile,
            ESGenericProfileSettings settings,
            List<string> issues)
        {
            if (!Enabled)
                return true;

            bool valid = true;
            if (prefab == null)
            {
                issues?.Add("Child Prefab Extension 已启用，但 Prefab 为空。");
                valid = false;
            }

            if (parent == null)
            {
                issues?.Add("Child Prefab Extension 已启用，但 Parent 为空。");
                valid = false;
            }
            else if (profile == null || !profile.IsProfileRootOrDescendant(parent))
            {
                issues?.Add("Child Prefab Parent 必须是 Profile 根 Transform 或其后代。");
                valid = false;
            }

            ESGenericProfilePlayerInitializationSettings player =
                settings.GetExtension<ESGenericProfilePlayerInitializationSettings>();
            if (player != null && player.Enabled && player.DestroyProfileComponent)
            {
                issues?.Add("销毁 Profile 组件会触发 OnDestroy 清理 Child Prefab；两个 Extension 不能同时启用。");
                valid = false;
            }

            return valid;
        }
    }

    [Serializable]
    public sealed class ESGenericProfileSettings
    {
        [SerializeField, LabelText("Auto Awake")]
        [Tooltip("默认开启。Awake 时自动转发 OnProfileAwake；关闭后由外部调用 Profile.NotifyAwake。")]
        private bool autoAwake = true;

        [SerializeField, LabelText("Auto Enable")]
        [Tooltip("默认开启。OnEnable/OnDisable 自动转发对应 Profile 生命周期；关闭后由外部调用 NotifyEnable/NotifyDisable。")]
        private bool autoEnable = true;

        [SerializeField, LabelText("Auto Pool")]
        [Tooltip("默认开启。Pool Spawn/Despawn 自动转发对应 Profile 生命周期；关闭后由外部调用 NotifyPoolSpawned/NotifyPoolDespawned。")]
        private bool autoPoolLifecycle = true;

        [SerializeReference]
        [ESCollectionDrawStyle(
            ESCollectionDrawMode.FeelList,
            EnabledMemberName = "enabled",
            AllowDuplicateItems = false,
            EnforceDefaultOrder = true)]
        [LabelText("扩展块")]
        [Tooltip("Extension List 是唯一配置权威；顺序、重复、Schema、依赖与互斥关系在 Validate/生命周期转发前校验。")]
        private List<ESGenericProfileExtensionSettings> extensions =
            new List<ESGenericProfileExtensionSettings>(0);

        public bool AutoAwake => autoAwake;
        public bool AutoEnable => autoEnable;
        public bool AutoPoolLifecycle => autoPoolLifecycle;
        public IReadOnlyList<ESGenericProfileExtensionSettings> Extensions => extensions;
        public int ExtensionCount => extensions?.Count ?? 0;

        public bool HasExtension<T>() where T : ESGenericProfileExtensionSettings
        {
            return TryGetExtension<T>(out _);
        }

        public T GetExtension<T>() where T : ESGenericProfileExtensionSettings
        {
            return TryGetExtension<T>(out T extension) ? extension : null;
        }

        public bool TryGetExtension<T>(out T extension)
            where T : ESGenericProfileExtensionSettings
        {
            extension = null;
            if (extensions == null)
                return false;

            for (int i = 0; i < extensions.Count; i++)
            {
                if (!(extensions[i] is T candidate))
                    continue;

                extension = candidate;
                return true;
            }

            return false;
        }

        internal void EnsureDefaults()
        {
            extensions ??= new List<ESGenericProfileExtensionSettings>(0);
        }

        internal bool ValidateExtensions(ESGenericProfile profile, List<string> issues)
        {
            if (extensions == null)
            {
                issues?.Add("Extensions 列表不能为空；允许使用空列表，但列表实例不能为 null。");
                return false;
            }

            bool valid = true;
            int previousDefaultOrder = int.MinValue;
            for (int i = 0; i < extensions.Count; i++)
            {
                ESGenericProfileExtensionSettings extension = extensions[i];
                if (extension == null)
                {
                    issues?.Add("Extensions[" + i + "] 为空或对应 SerializeReference 类型已缺失。");
                    valid = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(extension.TypeId))
                {
                    issues?.Add("Extensions[" + i + "] 的稳定 TypeId 为空。");
                    valid = false;
                }

                if (string.IsNullOrWhiteSpace(extension.NameTitleDefault))
                {
                    issues?.Add(extension.TypeId + " 的 NameTitleDefault 不能为空。");
                    valid = false;
                }

                if (extension.SchemaVersion != extension.SupportedSchemaVersion)
                {
                    issues?.Add(
                        extension.TypeId + " SchemaVersion 无效：当前 " + extension.SchemaVersion
                        + "，要求 " + extension.SupportedSchemaVersion + "。");
                    valid = false;
                }

                if (extension.DefaultOrder < previousDefaultOrder)
                {
                    issues?.Add(
                        extension.TypeId + " 顺序非法；扩展必须按稳定 DefaultOrder 从小到大排列。");
                    valid = false;
                }
                previousDefaultOrder = extension.DefaultOrder;

                for (int previousIndex = 0; previousIndex < i; previousIndex++)
                {
                    ESGenericProfileExtensionSettings previous = extensions[previousIndex];
                    if (previous == null
                        || !string.Equals(previous.TypeId, extension.TypeId, StringComparison.Ordinal))
                        continue;

                    issues?.Add(extension.TypeId + " 已存在；同一稳定 TypeId 不能重复添加。");
                    valid = false;
                }
            }

            for (int i = 0; i < extensions.Count; i++)
            {
                ESGenericProfileExtensionSettings extension = extensions[i];
                if (extension != null && !extension.Validate(profile, this, issues))
                    valid = false;
            }

            return valid;
        }
    }
}
