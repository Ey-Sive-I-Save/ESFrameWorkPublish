using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    public enum ESZoneMemberEnterResult
    {
        Ignored = 0,
        Entered = 1,
        Failed = 2
    }

    [Serializable]
    public abstract class ESZoneProfileExtensionSettings
        : IESCollectionDefaultOrder, IESNameTitle
    {
        [SerializeField, LabelText("名称")]
        private string nameTitle;

        public abstract string TypeId { get; }
        public abstract int SchemaVersion { get; }
        public abstract int SupportedSchemaVersion { get; }
        public abstract int DefaultOrder { get; }
        public abstract string NameTitleDefault { get; }
        public abstract bool Enabled { get; }

        public string NameTitle
        {
            get => string.IsNullOrWhiteSpace(nameTitle) ? NameTitleDefault : nameTitle.Trim();
            set => nameTitle = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public abstract ESZoneProfileExtensionRuntime CreateRuntime();

        protected internal virtual bool Validate(
            ESZoneProfile profile,
            ESZoneProfileSettings settings,
            List<string> issues)
        {
            return true;
        }
    }

    /// <summary>One per-Profile runtime instance created from one Extension Settings entry.</summary>
    public abstract class ESZoneProfileExtensionRuntime
    {
        public virtual void OnProfileAwake(ESZoneProfile profile, ESZoneProfileRuntimeContext context) { }
        public virtual void OnProfileEnable(ESZoneProfile profile, ESZoneProfileRuntimeContext context) { }
        public virtual void OnProfileDisable(ESZoneProfile profile, ESZoneProfileRuntimeContext context) { }
        public virtual void OnProfilePoolSpawned(ESZoneProfile profile, ESZoneProfileRuntimeContext context) { }
        public virtual void OnProfilePoolDespawned(ESZoneProfile profile, ESZoneProfileRuntimeContext context) { }
        public virtual void OnProfileDestroy(ESZoneProfile profile, ESZoneProfileRuntimeContext context) { }

        /// <summary>Failure must leave this runtime as if the member had not entered.</summary>
        public virtual ESZoneMemberEnterResult TryEnterMember(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context,
            ESZoneMember member,
            out string error)
        {
            error = null;
            return ESZoneMemberEnterResult.Ignored;
        }

        public virtual void ExitMember(
            ESZoneProfile profile,
            ESZoneProfileRuntimeContext context,
            ESZoneMember member)
        {
        }
    }

    [Serializable]
    public sealed class ESZoneProfileSettings
    {
        [SerializeField, LabelText("Auto Awake")]
        private bool autoAwake = true;

        [SerializeField, LabelText("Auto Enable")]
        private bool autoEnable = true;

        [SerializeField, LabelText("Auto Pool")]
        private bool autoPoolLifecycle = true;

        [SerializeField, LabelText("区域语义 Tag")]
        private List<ESTagStableReference> semanticTags = new List<ESTagStableReference>();

        [SerializeField, LabelText("区域优先级")]
        [Tooltip("供具体能力处理重叠区域时比较；Zone 本身不执行跨领域仲裁。")]
        private int priority;

        [SerializeField, LabelText("输出能力告警")]
        private bool logExtensionFailures = true;

        [SerializeReference]
        [ESCollectionDrawStyle(
            ESCollectionDrawMode.FeelList,
            EnabledMemberName = "enabled",
            AllowDuplicateItems = false,
            EnforceDefaultOrder = true)]
        [LabelText("能力扩展")]
        private List<ESZoneProfileExtensionSettings> extensions =
            new List<ESZoneProfileExtensionSettings>(0);

        public bool AutoAwake => autoAwake;
        public bool AutoEnable => autoEnable;
        public bool AutoPoolLifecycle => autoPoolLifecycle;
        public IReadOnlyList<ESTagStableReference> SemanticTags => semanticTags;
        public int Priority => priority;
        public bool LogExtensionFailures => logExtensionFailures;
        public IReadOnlyList<ESZoneProfileExtensionSettings> Extensions => extensions;
        public int ExtensionCount => extensions?.Count ?? 0;

        public bool HasSemanticTag(ESTagStableReference tag)
        {
            if (semanticTags == null)
                return false;

            for (int i = 0; i < semanticTags.Count; i++)
            {
                if (semanticTags[i].Equals(tag))
                    return true;
            }

            return false;
        }

        public T GetExtension<T>() where T : ESZoneProfileExtensionSettings
        {
            if (extensions == null)
                return null;

            for (int i = 0; i < extensions.Count; i++)
            {
                if (extensions[i] is T result)
                    return result;
            }

            return null;
        }

        internal void EnsureDefaults()
        {
            semanticTags ??= new List<ESTagStableReference>();
            extensions ??= new List<ESZoneProfileExtensionSettings>(0);
        }

        internal bool ValidateExtensions(ESZoneProfile profile, List<string> issues)
        {
            bool valid = true;
            if (semanticTags == null)
            {
                issues?.Add("区域语义 Tag 列表不能为 null。");
                valid = false;
            }
            else
            {
                for (int i = 0; i < semanticTags.Count; i++)
                {
                    if (semanticTags[i].IsEmpty)
                    {
                        issues?.Add("第 " + (i + 1) + " 个区域语义 Tag 未配置。");
                        valid = false;
                    }

                    for (int previous = 0; previous < i; previous++)
                    {
                        if (!semanticTags[previous].Equals(semanticTags[i]))
                            continue;

                        issues?.Add("第 " + (i + 1) + " 个区域语义 Tag 重复配置。");
                        valid = false;
                    }
                }
            }

            if (extensions == null)
            {
                issues?.Add("Extensions 列表不能为 null；纯标记 Zone 应使用空列表。");
                return false;
            }

            if (extensions.Count > ESZoneProfileRuntimeContext.MaxExtensionCount)
            {
                issues?.Add("Zone Profile 最多支持 "
                    + ESZoneProfileRuntimeContext.MaxExtensionCount
                    + " 个 Extension；成员进入状态使用固定 64 位掩码。");
                valid = false;
            }

            int previousOrder = int.MinValue;
            for (int i = 0; i < extensions.Count; i++)
            {
                ESZoneProfileExtensionSettings extension = extensions[i];
                if (extension == null)
                {
                    issues?.Add("Extensions[" + i + "] 为空或 SerializeReference 类型已缺失。");
                    valid = false;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(extension.TypeId))
                {
                    issues?.Add("Extensions[" + i + "] 的稳定 TypeId 为空。");
                    valid = false;
                }

                if (extension.SchemaVersion != extension.SupportedSchemaVersion)
                {
                    issues?.Add(extension.TypeId + " SchemaVersion 无效：当前 "
                        + extension.SchemaVersion + "，要求 " + extension.SupportedSchemaVersion + "。");
                    valid = false;
                }

                if (extension.DefaultOrder < previousOrder)
                {
                    issues?.Add(extension.TypeId + " 顺序非法；Extension 必须按 DefaultOrder 排列。");
                    valid = false;
                }
                previousOrder = extension.DefaultOrder;

                for (int previous = 0; previous < i; previous++)
                {
                    ESZoneProfileExtensionSettings existing = extensions[previous];
                    if (existing == null
                        || !string.Equals(existing.TypeId, extension.TypeId, StringComparison.Ordinal))
                        continue;

                    issues?.Add(extension.TypeId + " 已存在；同一稳定 TypeId 不能重复添加。");
                    valid = false;
                }
            }

            for (int i = 0; i < extensions.Count; i++)
            {
                ESZoneProfileExtensionSettings extension = extensions[i];
                if (extension != null && !extension.Validate(profile, this, issues))
                    valid = false;
            }

            return valid;
        }
    }
}
