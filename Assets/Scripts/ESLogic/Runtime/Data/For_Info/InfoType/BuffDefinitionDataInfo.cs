using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace ES
{
    [ESCreatePath("数据信息", "Buff定义数据")]
    public class BuffDefinitionDataInfo : SoDataInfo, ISharedAndVariable<BuffSharedData, BuffVariableData>, IGameCoreSO
    {
        [TitleGroup("共享数据", "共享数据", Alignment = TitleAlignments.Left)]
        [HideLabel, InlineProperty]
        public BuffSharedData sharedData = new BuffSharedData();

        [TitleGroup("默认可变数据", "默认可变数据", Alignment = TitleAlignments.Left)]
        [HideLabel, InlineProperty]
        public BuffVariableData variableData = new BuffVariableData();

        public BuffSharedData SharedData
        {
            get => sharedData;
            set => sharedData = value;
        }

        public BuffVariableData VariableData
        {
            get => variableData;
            set => variableData = value;
        }

        [ShowInInspector, ReadOnly, LabelText("GameTag 配置检查")]
        private string GameTagValidationSummary
        {
            get
            {
                string error = null;
                return sharedData != null && sharedData.TryValidateGameTagConfiguration(out error)
                    ? "有效：Buff 只授予 RuntimeFact，施加条件可编译。"
                    : "无效：" + (string.IsNullOrEmpty(error) ? "缺少 SharedData。" : error);
            }
        }

        private void OnValidate()
        {
            sharedData?.EnsureGameTagConfigurationContainers();
        }

        public void InjectGameCoreTables()
        {
            ESBuffGameCoreTable.Inject(this);
        }
    }

    /// <summary>Buff 领域强类型注册入口；根 SO 直接注入，不经过中央类别分发。</summary>
    public static class ESBuffGameCoreTable
    {
        public static ESBuffConfigKeyTable Table => ESRuntimeDataGameCore.Buffs;

        public static void Inject(BuffDefinitionDataInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
                if (info.sharedData == null) throw new InvalidOperationException("Buff 缺少 SharedData：" + info.name);
                if (!info.sharedData.TryValidateGameTagConfiguration(out string gameTagError))
                    throw new InvalidOperationException("Buff GameTag 配置无效：" + info.name + "，" + gameTagError);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild) Table.BeginBuild();
            try
            {
                if (info.sharedData.key == null || !info.sharedData.key.IsConfigured)
                    throw new InvalidOperationException("Buff 必须显式配置 EnumKey 或 StringKey；KeyName 仅供编辑器与策划使用：" + info.name);
                if (Table.TryGet(info.sharedData.key, out ESBuffRuntimeData existing))
                {
                    if (ReferenceEquals(existing.soSource, info)) return;
                    throw new InvalidOperationException("Buff GameCore Key 重复：" + info.name);
                }

                ESBuffRuntimeData data = Table.AcquireRetained(info.sharedData.key);
                try
                {
                    data.keyName = ESConfigKeyMatch.Describe(info.sharedData.key.EnumKeyInt, info.sharedData.key.StringKey);
                    data.displayName = info.name;
                    data.sourcePackage = info.name;
                    data.soSource = info;
                    data.sharedData = info.sharedData;
                    data.defaultVariableData = info.variableData;
                    int runtimeKey = Table.CommitRetained(info.sharedData.key, data, debugName: info.name);
                    if (runtimeKey == 0)
                        throw new InvalidOperationException("Buff GameCore 注入失败：" + info.name);
                }
                catch
                {
                    Table.AbandonRetained(data);
                    throw;
                }
            }
            finally
            {
                if (ownsBuild) Table.EndBuild();
            }
        }
    }

    [Serializable]
    public sealed class BuffSharedData
    {
        [Title("Key")]
        [HideLabel, InlineProperty]
        public ESBuffConfigKey key = new ESBuffConfigKey();

        [Title("GameTag")]
        [LabelText("Buff 授予标签")]
        [Tooltip("Buff 生命周期内持有的统一稳定 Tag。主 Enum Tag 仅可授予 RuntimeFact；StringKey 与第二组 Enum 由 Catalog 定义。")]
        public ESTagGrantConfig tagGrants = new ESTagGrantConfig();

        [FormerlySerializedAs("tags")]
        [SerializeField, HideInInspector]
        private List<ESGameTag> legacyGrantedCoreTags;

        [LabelText("施加目标 Tag 条件")]
        [Tooltip("施加 Buff 前对目标 Entity 的统一 Core + Extension Tag 条件。为空时不限制；条件无效时配置注入和运行时施加均会拒绝。")]
        public ESTagConditionConfig applyTargetTagCondition = new ESTagConditionConfig();

        [FormerlySerializedAs("applyTargetTagRequirement")]
        [SerializeField, HideInInspector]
        private ESGameTagRequirementConfig legacyApplyTargetTagRequirement;

        [LabelText("默认持续时间")]
        public float duration = 5f;

        [Title("叠层 / 互斥 / 来源")]
        [LabelText("Buff Group")]
        public string buffGroup;

        [LabelText("强度")]
        public int strength = 0;

        [LabelText("来源隔离")]
        public ESBuffSourceIsolationMode sourceIsolationMode = ESBuffSourceIsolationMode.IgnoreSource;

        [LabelText("叠层模式")]
        public ESBuffStackMode stackMode = ESBuffStackMode.StackSameBuff;

        [LabelText("时间刷新")]
        public ESBuffTimeRefreshMode timeRefreshMode = ESBuffTimeRefreshMode.ResetDuration;

        [LabelText("Group Conflict")]
        public ESBuffGroupConflictMode groupConflictMode = ESBuffGroupConflictMode.None;

        [LabelText("Max Stack")]
        [Min(1)]
        public int maxStack = 1;

        [Title("Tick")]
        [LabelText("Tick模式")]
        public ESBuffTickMode tickMode = ESBuffTickMode.None;

        [LabelText("Tick间隔")]
        [Min(0f)]
        public float tickInterval = 1f;

        [Title("Op")]
        [LabelText("On Apply Op")]
        [SerializeReference]
        public ESOutputOp onApplyOp;

        [SerializeReference]
        public ESOutputOp onRefreshOp;

        [SerializeReference]
        public ESOutputOp onTickOp;

        [SerializeReference]
        public ESOutputOp onRemoveOp;

        [Title("ValueChange")]
        [LabelText("Float Changes")]
        [SerializeReference]
        public List<ESBuffFloatValueChangeBinding> floatChanges = new List<ESBuffFloatValueChangeBinding>();

        [LabelText("权限变化")]
        [SerializeReference]
        public List<ESBuffPermitValueChangeBinding> permitChanges = new List<ESBuffPermitValueChangeBinding>();

        /// <summary>创建一份具有字段声明默认值的独立 Buff 共享配置。</summary>
        public static BuffSharedData Default => new BuffSharedData();

        /// <summary>
        /// 把 Table 自有的运行时默认对象原位恢复为领域默认值。集合保留容量并清空，
        /// 不得用于重置 SO 或调用方传入的权威 SharedData。
        /// </summary>
        internal void ResetToDefaults()
        {
            key ??= new ESBuffConfigKey();
            key.enumKey = ESBuffEnumKey.None;
            key.stringKey = null;
            key.definitionGuid = null;
            key.definitionLocalFileId = 0;
            key.definitionTypeName = null;

            tagGrants ??= new ESTagGrantConfig();
            tagGrants.tags ??= new List<ESTagStableReference>();
            tagGrants.tags.Clear();
            legacyGrantedCoreTags = null;
            applyTargetTagCondition ??= new ESTagConditionConfig();
            ResetTagCondition(applyTargetTagCondition);
            legacyApplyTargetTagRequirement = null;

            duration = 5f;
            buffGroup = null;
            strength = 0;
            sourceIsolationMode = ESBuffSourceIsolationMode.IgnoreSource;
            stackMode = ESBuffStackMode.StackSameBuff;
            timeRefreshMode = ESBuffTimeRefreshMode.ResetDuration;
            groupConflictMode = ESBuffGroupConflictMode.None;
            maxStack = 1;
            tickMode = ESBuffTickMode.None;
            tickInterval = 1f;
            onApplyOp = null;
            onRefreshOp = null;
            onTickOp = null;
            onRemoveOp = null;

            floatChanges ??= new List<ESBuffFloatValueChangeBinding>();
            permitChanges ??= new List<ESBuffPermitValueChangeBinding>();
            floatChanges.Clear();
            permitChanges.Clear();
        }

        /// <summary>Buff 定义进入 GameCore 前的 Tag 配置校验；运行时直接构造的 SharedData 也复用本规则。</summary>
        public bool TryValidateGameTagConfiguration(out string error)
        {
            error = null;
            EnsureGameTagConfigurationContainers();
            if (tagGrants != null && !tagGrants.TryValidate(out error))
            {
                error = "tagGrants 无效：" + error;
                return false;
            }

            if (tagGrants != null && tagGrants.tags != null)
            {
                for (int i = 0; i < tagGrants.tags.Count; i++)
                {
                    ESTagStableReference reference = tagGrants.tags[i];
                    if (reference.HasEnumKey
                        && reference.enumGroup == ESTagEnumGroup.Primary
                        && !ESGameTagCatalog.CanBeGrantedByBuff((ESGameTag)reference.enumValue))
                    {
                        error = "tagGrants.tags[" + i + "]=" + reference
                                + " 不允许由 Buff 授予；Buff 仅可授予 RuntimeFact 主 Enum Tag，能力入口需由对应组件维护。";
                        return false;
                    }
                }
            }

            if (applyTargetTagCondition != null
                && !applyTargetTagCondition.TryCompile(out _, out error))
            {
                error = "applyTargetTagCondition 无效：" + error;
                return false;
            }

            return true;
        }

        /// <summary>编辑器反序列化旧 Buff 时补齐新增的配置容器，不改变既有业务语义。</summary>
        public void EnsureGameTagConfigurationContainers()
        {
            tagGrants ??= new ESTagGrantConfig();
            tagGrants.tags ??= new List<ESTagStableReference>();
            applyTargetTagCondition ??= new ESTagConditionConfig();
            EnsureTagConditionLists(applyTargetTagCondition);
            MigrateLegacyTagGrants();
            MigrateLegacyTagRequirement();
        }

        /// <summary>把策划配置解析为当前 Catalog 下的统一运行时条件；无条件配置返回空条件。</summary>
        public bool TryGetApplyTargetTagCondition(out ESTagConditionRuntime condition, out string error)
        {
            EnsureGameTagConfigurationContainers();
            condition = default;
            error = null;
            return applyTargetTagCondition == null || applyTargetTagCondition.TryGetRuntime(out condition, out error);
        }

        private void MigrateLegacyTagRequirement()
        {
            if (legacyApplyTargetTagRequirement == null)
                return;

            if (applyTargetTagCondition.IsEmpty)
            {
                CopyStableReferences(legacyApplyTargetTagRequirement.requiredAll, applyTargetTagCondition.required);
                CopyStableReferences(legacyApplyTargetTagRequirement.requiredAny, applyTargetTagCondition.requiredAny);
                CopyStableReferences(legacyApplyTargetTagRequirement.blockedAny, applyTargetTagCondition.forbidden);
            }

            legacyApplyTargetTagRequirement = null;
            applyTargetTagCondition.InvalidateRuntime();
        }

        private void MigrateLegacyTagGrants()
        {
            if (legacyGrantedCoreTags == null)
                return;

            if (tagGrants.tags.Count == 0)
                CopyStableReferences(legacyGrantedCoreTags, tagGrants.tags);

            legacyGrantedCoreTags = null;
        }

        private static void EnsureTagConditionLists(ESTagConditionConfig condition)
        {
            condition.required ??= new List<ESTagStableReference>();
            condition.requiredAny ??= new List<ESTagStableReference>();
            condition.forbidden ??= new List<ESTagStableReference>();
            condition.requiredCore ??= new List<ESGameTag>();
            condition.requiredAnyCore ??= new List<ESGameTag>();
            condition.forbiddenCore ??= new List<ESGameTag>();
            condition.requiredExtensions ??= new List<string>();
            condition.requiredAnyExtensions ??= new List<string>();
            condition.forbiddenExtensions ??= new List<string>();
        }

        private static void ResetTagCondition(ESTagConditionConfig condition)
        {
            EnsureTagConditionLists(condition);
            condition.required.Clear();
            condition.requiredAny.Clear();
            condition.forbidden.Clear();
            condition.requiredCore.Clear();
            condition.requiredAnyCore.Clear();
            condition.forbiddenCore.Clear();
            condition.requiredExtensions.Clear();
            condition.requiredAnyExtensions.Clear();
            condition.forbiddenExtensions.Clear();
            condition.InvalidateRuntime();
        }

        private static void CopyTags(List<ESGameTag> source, List<ESGameTag> destination)
        {
            if (source == null || destination == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                if (!destination.Contains(source[i]))
                    destination.Add(source[i]);
            }
        }

        private static void CopyStableReferences(List<ESGameTag> source, List<ESTagStableReference> destination)
        {
            if (source == null || destination == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                ESTagStableReference reference = ESTagStableReference.From(source[i]);
                if (!destination.Contains(reference))
                    destination.Add(reference);
            }
        }

    }

    [Serializable]
    public sealed class BuffVariableData : IDeepClone<BuffVariableData>
    {
        [LabelText("层数")]
        [Min(1)]
        public int stackCount = 1;

        [LabelText("剩余时间")]
        public float remainingTime;

        [LabelText("Elapsed Time")]
        public float elapsedTime;

        [LabelText("Tick累计")]
        public float tickAccumulator;

        [LabelText("来源Key")]
        public int sourceKey;

        /// <summary>创建一份具有字段声明默认值的独立 Buff 初始变量。</summary>
        public static BuffVariableData Default => new BuffVariableData();

        internal void ResetToDefaults()
        {
            stackCount = 1;
            remainingTime = 0f;
            elapsedTime = 0f;
            tickAccumulator = 0f;
            sourceKey = 0;
        }

        public void DeepCloneFrom(BuffVariableData t)
        {
            if (t == null)
                return;

            stackCount = t.stackCount;
            remainingTime = t.remainingTime;
            elapsedTime = t.elapsedTime;
            tickAccumulator = t.tickAccumulator;
            sourceKey = t.sourceKey;
        }
    }

    [Serializable]
    public sealed class ESBuffFloatValueChangeBinding
    {
        [LabelText("Attribute EnumKey")]
        public ushort attributeEnumKey;

        [LabelText("Stat Key")]
        public string statKey;

        [LabelText("刷新时机")]
        public ESBuffValueChangeRefreshMode refreshMode = ESBuffValueChangeRefreshMode.OnApplyOnly;

        [LabelText("变化")]
        public ESFloatValueChangeExpressionBinding change = new ESFloatValueChangeExpressionBinding();

        public bool IsConfigured => attributeEnumKey != 0 || !string.IsNullOrEmpty(statKey);
    }

    [Serializable]
    public sealed class ESBuffPermitValueChangeBinding
    {
        [LabelText("Attribute EnumKey")]
        public ushort attributeEnumKey;

        [LabelText("Permit Key")]
        public string permitKey;

        [LabelText("刷新时机")]
        public ESBuffValueChangeRefreshMode refreshMode = ESBuffValueChangeRefreshMode.OnApplyOnly;

        [LabelText("变化")]
        public ESPermitValueChangeExpressionBinding change = new ESPermitValueChangeExpressionBinding();

        public bool IsConfigured => attributeEnumKey != 0 || !string.IsNullOrEmpty(permitKey);
    }
}
