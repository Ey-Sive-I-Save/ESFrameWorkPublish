using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace ES
{
    public enum ESActionCategory : byte
    {
        None = 0,
        Attack = 1,
        Skill = 2,
        Dodge = 3,
        Block = 4,
        Interact = 5,
        Item = 6,
    }

    public enum ESActionPhaseKind : byte
    {
        None = 0,
        Startup = 1,
        Active = 2,
        Recovery = 3,
        Channel = 4,
    }

    public enum ESActionEventKind : byte
    {
        None = 0,
        ActionStarted = 1,
        PhaseEntered = 2,
        HitWindowOpened = 3,
        HitResolved = 4,
        ActionCancelled = 5,
        ActionInterrupted = 6,
        ActionFinished = 7,
    }

    public enum ESActionPresentationChannel : byte
    {
        None = 0,
        Audio = 1,
        Vfx = 2,
        Camera = 3,
        Hitstop = 4,
        Animation = 5,
    }

    public enum ESActionPresentationOwner : byte
    {
        None = 0,
        Direct = 1,
        SkillTrack = 2,
    }

    [Serializable]
    public sealed class ESActionHitWindowData
    {
        public bool enabled = true;
        [MinValue(0.001f)] public float radius = 1f;
        [MinValue(0.001f)] public float forwardDistance = 1f;
        [MinValue(0f)] public float damageMultiplier = 1f;
    }

    [Serializable]
    public sealed class ESActionPhaseData
    {
        public ESActionPhaseKind kind;
        [MinValue(0.001f)] public float duration = 0.2f;
        [MinValue(0f)] public float inputBufferWindow;
        [MinValue(0f)] public float hitstopSeconds;
        public ESActionHitWindowData hitWindow = new ESActionHitWindowData();
    }

    [Serializable]
    public sealed class ESActionComboTransitionData
    {
        public int fromStep;
        public int toStep;
        [HideLabel, InlineProperty]
        public ESActionConfigKey targetActionKey = new ESActionConfigKey();
        [MinValue(0f)] public float inputBufferWindow;
    }

    [Serializable]
    public sealed class ESActionCancelRuleData
    {
        public ESActionPhaseKind sourcePhase;
        public ESActionCategory targetCategory;
        [HideLabel, InlineProperty]
        public ESActionConfigKey targetActionKey = new ESActionConfigKey();
        [MinValue(0f)] public float windowStart;
        [MinValue(0f)] public float windowDuration;
        public int priority;
        public bool consumeBufferedIntent;
    }

    [Serializable]
    public sealed class ESActionPresentationBindingData
    {
        public ESActionEventKind eventKind;
        public ESActionPresentationChannel channel;
        public ESActionPresentationOwner owner;
        public ESSkillTrackConfigKey skillTrackKey = new ESSkillTrackConfigKey();
    }

    [ESCreatePath("数据信息", "动作模板数据信息")]
    public sealed class ActionTemplateDataInfo : SoDataInfo, IGameCoreSO
    {
        [ESEditorSection("identity", "稳定身份", -100f)]
        [Title("稳定身份")]
        [HideLabel, InlineProperty]
        public ESActionConfigKey actionKey = new ESActionConfigKey();

        [ESEditorSection("definition", "动作定义", -50f)]
        [Title("动作定义")]
        public ESActionCategory category = ESActionCategory.Attack;
        public bool allowBufferedInput = true;
        [MinValue(0f)] public float globalInputBufferWindow;

        [ESEditorSection("phases", "阶段与窗口", -40f)]
        [Title("阶段")]
        public List<ESActionPhaseData> phases = new List<ESActionPhaseData>();

        [ESEditorSection("combo", "连段转换", -30f)]
        [Title("连段转换")]
        public List<ESActionComboTransitionData> comboTransitions = new List<ESActionComboTransitionData>();

        [ESEditorSection("cancel", "取消规则", -20f)]
        [Title("取消规则")]
        public List<ESActionCancelRuleData> cancelRules = new List<ESActionCancelRuleData>();

        [ESEditorSection("presentation", "表现通道所有权", -10f)]
        [Title("表现绑定")]
        [InfoBox("运行时只允许持有稳定 Key；Inspector 可显示对象，但保存的是 Key。")]
        public List<ESActionPresentationBindingData> presentationBindings = new List<ESActionPresentationBindingData>();

        public void InjectGameCoreTables()
        {
            ESActionGameCoreTable.Inject(this);
        }
    }

    public static class ESActionGameCoreTable
    {
        public static ESActionConfigKeyTable Table => ESRuntimeDataGameCore.Actions;

        public static void Inject(ActionTemplateDataInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            ValidateDefinition(info);

            bool ownsBuild = !Table.IsBuilding;
            if (ownsBuild)
                Table.BeginBuild();

            try
            {
                if (Table.TryGet(info.actionKey, out ESActionRuntimeData existing))
                {
                    if (ReferenceEquals(existing.soSource, info))
                        return;
                    throw new InvalidOperationException("Action GameCore Key 重复：" + info.name);
                }

                int runtimeKey = Table.InjectWith(
                    info.actionKey,
                    info,
                    info.category,
                    info.phases,
                    info.comboTransitions,
                    info.cancelRules,
                    info.presentationBindings,
                    info.allowBufferedInput,
                    info.globalInputBufferWindow,
                    info.name);
                if (runtimeKey == 0)
                    throw new InvalidOperationException("Action GameCore 注入失败：" + info.name);

                RegisterTrackBindings(info);
            }
            finally
            {
                if (ownsBuild)
                    Table.EndBuild();
            }
        }

        private static void ValidateDefinition(ActionTemplateDataInfo info)
        {
            if (info.actionKey == null || !info.actionKey.IsConfigured)
                throw new InvalidOperationException("Action 必须显式配置 EnumKey 或 StringKey：" + info.name);

            if (info.category == ESActionCategory.None)
                throw new InvalidOperationException("Action 必须指定 Category：" + info.name);

            if (info.phases == null || info.phases.Count == 0)
                throw new InvalidOperationException("Action 至少需要一个 Phase：" + info.name);

            for (int i = 0; i < info.phases.Count; i++)
            {
                ESActionPhaseData phase = info.phases[i];
                if (phase == null || phase.kind == ESActionPhaseKind.None)
                    throw new InvalidOperationException("Action Phase 非法：" + info.name + " index=" + i);
                if (phase.duration <= 0f)
                    throw new InvalidOperationException("Action Phase 时长必须大于 0：" + info.name + " index=" + i);
                if (phase.hitWindow != null && phase.hitWindow.enabled
                    && (phase.hitWindow.radius <= 0f || phase.hitWindow.forwardDistance <= 0f))
                    throw new InvalidOperationException("Action HitWindow 非法：" + info.name + " index=" + i);
            }

            if (info.presentationBindings != null)
            {
                for (int i = 0; i < info.presentationBindings.Count; i++)
                {
                    ESActionPresentationBindingData binding = info.presentationBindings[i];
                    if (binding == null || binding.eventKind == ESActionEventKind.None
                        || binding.channel == ESActionPresentationChannel.None
                        || binding.owner == ESActionPresentationOwner.None)
                        throw new InvalidOperationException("Action PresentationBinding 非法：" + info.name + " index=" + i);
                    if (binding.owner == ESActionPresentationOwner.SkillTrack
                        && (binding.skillTrackKey == null || !binding.skillTrackKey.IsConfigured))
                        throw new InvalidOperationException("Action SkillTrack Binding 必须配置稳定 Key：" + info.name + " index=" + i);
                }
            }
        }

        private static void RegisterTrackBindings(ActionTemplateDataInfo info)
        {
            if (info.presentationBindings == null)
                return;

            for (int i = 0; i < info.presentationBindings.Count; i++)
            {
                ESActionPresentationBindingData binding = info.presentationBindings[i];
                if (binding != null
                    && binding.owner == ESActionPresentationOwner.SkillTrack
                    && binding.skillTrackKey != null
                    && binding.skillTrackKey.IsConfigured)
                    ESSkillTrackGameCoreTable.Inject(binding.skillTrackKey, "Action Track " + binding.skillTrackKey.ToString());
            }
        }
    }
}
