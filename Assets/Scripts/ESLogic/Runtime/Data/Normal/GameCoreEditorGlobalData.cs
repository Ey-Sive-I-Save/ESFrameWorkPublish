using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateAssetMenu(fileName = "GameCoreEditorGlobalData", menuName = "【ES】/项目设置/GameCore/编辑器全局数据")]
    [ESCreatePath("全局数据", "GameCore编辑器全局数据")]
    public class GameCoreEditorGlobalData : ESEditorGlobalSo<GameCoreEditorGlobalData>
    {
        private const string LegacyEditorDataName = "GameCoreGlobalData";
        private const string EditorDataName = "GameCoreEditorGlobalData";
        private const string LegacyDescription = "GameCoreEditorGlobalData 是项目的编辑器语义入口：集中说明 GameMode、ModeTag、GameTag、Input 分类、物理层语义和 AI Command 模板。不进入运行时配置链，不替代具体业务数据。";
        private const string CurrentDescription = "GameCoreEditorGlobalData 是项目的编辑期唯一配置入口：集中定义 GameMode、GameTag、角色属性与物品属性 Schema、Input 分类、物理层语义和 AI Command 模板。运行时只消费对应 Bake 产物，不直接依赖此资产。";

        [ESEditorSection("overview", "概览", -100f, "GameCore 的编辑期唯一配置入口。运行时只消费 Bake 产物，绝不反向依赖此资产。")]
        [Title("说明")]
        [LabelText("用途")]
        [MultiLineProperty(3)]
        public string description = CurrentDescription;

        [ESEditorSection("mode", "GameMode", 0f, "项目模式及其标签语义。")]
        [Title("GameMode")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreModeRule> gameModes = new List<GameCoreModeRule>();

        [ESEditorSection("mode-tags", "GameModeTag", 5f, "模式投影标签，只表达当前本地控制语境。")]
        [Title("GameModeTag")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreModeTagRule> gameModeTags = new List<GameCoreModeTagRule>();

        [ESEditorSection("tags", "GameTag", 10f, "可组合、可查询、可撤销的运行时事实。")]
        [Title("GameTag")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreTagRule> gameTags = new List<GameCoreTagRule>();

        [ESEditorSection("tag-definitions", "GameTag 定义", 11f, "唯一 Tag 配置源；Bake 生成 Catalog、RuntimeKey 与 SchemaHash。")]
        [Title("GameTag定义（唯一配置源）")]
        [InfoBox("在这里声明 EnumKey/StringKey、HotSlot/Sparse、运行时可用性与稳定传输范围。BakeTable、RuntimeKey、SchemaHash 均由此列表生成，禁止手改 BakeTable。")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreTagDefinition> tagDefinitions = new List<GameCoreTagDefinition>();

        [ESEditorSection("character-attributes", "角色属性", 20f, "角色 Float/Permit 的唯一 Schema。HotSlot 用于固定角色热读，Sparse 用于可选扩展属性。")]
        [Title("角色属性集（唯一 Schema）")]
        [InfoBox("常规属性：填写身份、显示名、存储策略和默认值后，直接 Bake。")]
        [InfoBox("固定访问名：只给角色固定 HotSlot 使用。新增、删除、改名或修改稳定身份后，先生成代码；其余修改无需生成。")]
        [HideLabel, InlineProperty]
        public ESSuperAttributeTable characterAttributes = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();

        [ESEditorSection("item-attributes", "物品属性", 30f, "Item Float/Permit 的唯一 Schema。普通 Item 默认按 Sparse 按需创建，固定高频 Item 由 Bake 明确声明 HotSlot。")]
        [Title("物品属性集（唯一 Schema）")]
        [InfoBox("这里定义所有 Item 可使用的属性身份、类型、默认值和范围；具体 ItemDataInfo 只填写自己的基础值覆盖，不复制 Schema。")]
        [HideLabel, InlineProperty]
        public ESSuperAttributeTable itemAttributes = new ESSuperAttributeTable { catalogScope = "Attribute.Item" };

        [ESEditorSection("input", "Input 分类", 40f, "输入分类与语义说明。")]
        [Title("Input分类")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreInputCategoryRule> inputCategories = new List<GameCoreInputCategoryRule>();

        [ESEditorSection("physics", "物理层", 50f, "物理碰撞与查询职责，不表达业务状态。")]
        [Title("物理层语义")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCorePhysicsLayerRule> physicsLayers = new List<GameCorePhysicsLayerRule>();

        [ESEditorSection("ai", "AI Command", 60f, "项目 AI 协作命令模板。")]
        [Title("AI Command模板")]
        [InfoBox("这里存放给开发者复制给 AI 的修改命令模板。目标是让开发者提出需求，AI 按项目法则改代码，而不是盲写。")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreAICommandTemplate> aiCommandTemplates = new List<GameCoreAICommandTemplate>();

        [Button("初始化推荐配置")]
        public void ResetDefaultRules()
        {
            gameModes = GameCoreDefaultRules.CreateModeRules();
            gameModeTags = GameCoreDefaultRules.CreateModeTagRules();
            gameTags = GameCoreDefaultRules.CreateGameTagRules();
            tagDefinitions = new List<GameCoreTagDefinition>();
            EnsureTagDefinitions();
            characterAttributes = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            itemAttributes = CreateDefaultItemAttributeTable();
            inputCategories = GameCoreDefaultRules.CreateInputCategoryRules();
            physicsLayers = GameCoreDefaultRules.CreatePhysicsLayerRules();
            aiCommandTemplates = GameCoreDefaultRules.CreateAICommandTemplates();
            MigrateLegacyEditorNaming();
        }

        /// <summary>
        /// 仅补齐缺失的核心 Tag 规则，不覆盖项目已写入的说明。用于旧资产升级，
        /// 避免“初始化推荐配置”重置物理层或 AI Command。
        /// </summary>
        [Button("补齐缺失的 GameTag 规则")]
        public void EnsureGameTagRules()
        {
            gameTags ??= new List<GameCoreTagRule>();
            List<GameCoreTagRule> defaults = GameCoreDefaultRules.CreateGameTagRules();
            for (int i = 0; i < defaults.Count; i++)
            {
                GameCoreTagRule expected = defaults[i];
                bool exists = false;
                for (int j = 0; j < gameTags.Count; j++)
                {
                    if (gameTags[j] != null && gameTags[j].tag == expected.tag)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    gameTags.Add(expected);
            }
        }

        /// <summary>
        /// One-time migration for the original primary Enum documentation. New Tag declarations
        /// must be authored in tagDefinitions; Bake never infers StringKey or storage policy from
        /// the legacy documentation list.
        /// </summary>
        public void EnsureTagDefinitions()
        {
            tagDefinitions ??= new List<GameCoreTagDefinition>();
            if (gameTags == null)
                return;

            for (int i = 0; i < gameTags.Count; i++)
            {
                GameCoreTagRule rule = gameTags[i];
                if (rule == null || rule.tag == ESGameTag.None)
                    continue;

                ESTagStableReference reference = ESTagStableReference.From(rule.tag);
                bool exists = false;
                for (int j = 0; j < tagDefinitions.Count; j++)
                {
                    if (tagDefinitions[j] != null && tagDefinitions[j].stableReference.Equals(reference))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    tagDefinitions.Add(new GameCoreTagDefinition
                    {
                        stableReference = reference,
                        storageTier = ESTagStorageTier.HotSlot,
                        availability = ESGameTagCatalog.IsUsableInNewConfiguration(rule.tag)
                            ? ESTagAvailability.Runtime
                            : ESTagAvailability.Deprecated,
                        stableTransferScopes = rule.stableTransferScopes,
                        group = rule.group,
                        meaning = rule.meaning,
                        ownerSystem = rule.ownerSystem,
                        warning = rule.warning
                    });
                }
            }
        }

        [Button("补齐属性表基础结构")]
        public void EnsureAttributeSchemas()
        {
            if (characterAttributes == null)
                characterAttributes = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            ESCharacterAttributeCatalog.EnsureCharacterScope(characterAttributes);

            if (itemAttributes == null)
                itemAttributes = CreateDefaultItemAttributeTable();
            if (string.IsNullOrWhiteSpace(itemAttributes.catalogScope))
                itemAttributes.catalogScope = "Attribute.Item";
            itemAttributes.floatAttributes ??= new List<ESSuperFloatAttributeDefinition>();
            itemAttributes.permitAttributes ??= new List<ESSuperPermitAttributeDefinition>();
        }

        [Button("验证角色与物品属性表")]
        public bool ValidateAttributeSchemas()
        {
            EnsureAttributeSchemas();
            if (!ESAttributeBakeTable.TryValidateSources(characterAttributes, itemAttributes, out string error))
            {
                Debug.LogError("[GameCoreAttribute] 属性表无效：" + error);
                return false;
            }

            return true;
        }

        private static ESSuperAttributeTable CreateDefaultItemAttributeTable()
        {
            return new ESSuperAttributeTable
            {
                catalogScope = "Attribute.Item",
                floatAttributes = new List<ESSuperFloatAttributeDefinition>(),
                permitAttributes = new List<ESSuperPermitAttributeDefinition>()
            };
        }

        /// <summary>
        /// 兼容既有资产中的说明文本；不影响运行时数据，也不修改业务内容。
        /// </summary>
        public void MigrateLegacyEditorNaming()
        {
            if (string.Equals(description, LegacyDescription, StringComparison.Ordinal))
                description = CurrentDescription;
            description = ReplaceLegacyEditorDataName(description);

            if (gameTags != null)
            {
                for (int i = 0; i < gameTags.Count; i++)
                {
                    GameCoreTagRule rule = gameTags[i];
                    if (rule != null)
                        rule.warning = ReplaceLegacyEditorDataName(rule.warning);
                }
            }

            if (aiCommandTemplates != null)
            {
                for (int i = 0; i < aiCommandTemplates.Count; i++)
                {
                    GameCoreAICommandTemplate template = aiCommandTemplates[i];
                    if (template != null)
                        template.commandText = ReplaceLegacyEditorDataName(template.commandText);
                }
            }
        }

        private static string ReplaceLegacyEditorDataName(string value)
        {
            return string.IsNullOrEmpty(value)
                ? value
                : value.Replace(LegacyEditorDataName, EditorDataName);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            MigrateLegacyEditorNaming();
            EnsureTagDefinitions();
            EnsureAttributeSchemas();
        }
#endif
    }

    [Serializable]
    public sealed class GameCoreModeRule
    {
        [LabelText("模式")]
        public ESRuntimeMode mode;

        [LabelText("中文名")]
        public string displayName;

        [LabelText("用途")]
        [MultiLineProperty(2)]
        public string purpose;

        [LabelText("是否阻断Gameplay输入")]
        public bool blocksGameplayInput;

        [LabelText("是否暂停世界时间")]
        public bool pausesWorldTime;

        [LabelText("允许输入分类")]
        public List<ESInputActionCategory> allowedInputCategories = new List<ESInputActionCategory>();
    }

    [Serializable]
    public sealed class GameCoreModeTagRule
    {
        [LabelText("模式标签")]
        public ESRuntimeModeTag tag;

        [LabelText("中文名")]
        public string displayName;

        [LabelText("用途")]
        [MultiLineProperty(2)]
        public string purpose;

        [LabelText("影响")]
        [MultiLineProperty(2)]
        public string effect;
    }

    [Serializable]
    public sealed class GameCoreTagRule
    {
        [LabelText("GameTag")]
        public ESGameTag tag;

        [LabelText("分组")]
        public string group;

        [LabelText("语义")]
        [MultiLineProperty(2)]
        public string meaning;

        [LabelText("归属系统")]
        public string ownerSystem;

        [LabelText("使用策略")]
        public ESGameTagUsagePolicy usagePolicy = ESGameTagUsagePolicy.RuntimeFact;

        [LabelText("稳定传输范围")]
        [Tooltip("仅允许将已声明范围的稳定 EnumKey/StringKey 写入存档或网络。RuntimeKey、Count 和 Lease Source 永不传输。")]
        public ESTagStableTransferScope stableTransferScopes = ESTagStableTransferScope.None;

        [LabelText("警告")]
        [MultiLineProperty(2)]
        public string warning;
    }

    [Serializable]
    public sealed class GameCoreTagDefinition
    {
        [LabelText("稳定身份")]
        [Tooltip("可填 EnumKey、StringKey，或两者绑定为同一 Tag；至少填写一个。")]
        public ESTagStableReference stableReference;

        [LabelText("运行时存储")]
        public ESTagStorageTier storageTier = ESTagStorageTier.Sparse;

        [LabelText("运行时可用性")]
        public ESTagAvailability availability = ESTagAvailability.Runtime;

        [LabelText("废弃迁移目标")]
        [Tooltip("仅 Deprecated Tag 使用；配置旧稳定身份需要显式迁移时，指定其替换 Tag。留空表示没有安全的自动替换。")]
        public ESTagStableReference deprecatedReplacement;

        [LabelText("稳定传输范围")]
        public ESTagStableTransferScope stableTransferScopes = ESTagStableTransferScope.None;

        [LabelText("分组")] public string group;
        [LabelText("语义"), MultiLineProperty(2)] public string meaning;
        [LabelText("归属系统")] public string ownerSystem;
        [LabelText("警告"), MultiLineProperty(2)] public string warning;
    }

    [Serializable]
    public sealed class GameCoreInputCategoryRule
    {
        [LabelText("输入分类")]
        public ESInputActionCategory category;

        [LabelText("中文名")]
        public string displayName;

        [LabelText("用途")]
        [MultiLineProperty(2)]
        public string purpose;

        [LabelText("Gameplay默认允许")]
        public bool allowedInGameplay = true;

        [LabelText("UI默认允许")]
        public bool allowedInUI;
    }

    [Serializable]
    [Flags]
    public enum GameCorePhysicsQueryRole
    {
        None = 0,
        Movement = 1 << 0,
        GroundProbe = 1 << 1,
        ShotHit = 1 << 2,
        MeleeHit = 1 << 3,
        InteractionProbe = 1 << 4,
        TriggerZoneProbe = 1 << 5,
        CameraObstacle = 1 << 6,
        AIVisibility = 1 << 7,
        AITarget = 1 << 8,
        ClimbProbe = 1 << 9,
        MountProbe = 1 << 10,
        FootIK = 1 << 11,
    }

    [Serializable]
    public sealed class GameCorePhysicsLayerRule
    {
        [LabelText("语义名")]
        public string semanticName;

        [LabelText("建议Unity Layer")]
        public int unityLayer = -1;

        [LabelText("归属")]
        public string owner;

        [LabelText("用途")]
        [MultiLineProperty(2)]
        public string usedBy;

        [LabelText("必须为 Trigger")]
        [Tooltip("此项描述挂在该 Layer 上的玩法 Collider 的默认要求。Hurtbox、交互盒和区域必须为 Trigger；身体与世界阻挡必须不是 Trigger。")]
        public bool mustBeTrigger;

        [LabelText("参与查询")]
        [EnumToggleButtons]
        [Tooltip("哪些框架语义查询允许把此 Layer 放入 LayerMask。None 表示该层不应成为玩法查询目标。")]
        public GameCorePhysicsQueryRole queryRoles;

        [LabelText("禁止物理碰撞层")]
        [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
        [Tooltip("填写 Layer 语义名，而非阵营名。这里描述 Collision Matrix 中必须关闭的物理/Trigger 碰撞对；主动 Raycast、SphereCast 仍由 Query Mask 单独决定。")]
        public List<string> forbiddenCollisionLayers = new List<string>();

        [LabelText("规则")]
        [MultiLineProperty(3)]
        public string rule;
    }

    [Serializable]
    public sealed class GameCoreAICommandTemplate
    {
        [LabelText("标题")]
        public string title;

        [LabelText("分类")]
        public string category;

        [LabelText("命令模板")]
        [TextArea(5, 12)]
        public string commandText;
    }

    public static class GameCoreDefaultRules
    {
        public static List<GameCoreModeRule> CreateModeRules()
        {
            return new List<GameCoreModeRule>
            {
                Mode(ESRuntimeMode.Gameplay, "游戏中", "主玩法模式。角色、Shot、交互、陷阱等正常运行。", false, false, ESInputActionCategory.Move, ESInputActionCategory.CameraLook, ESInputActionCategory.Combat, ESInputActionCategory.Interaction, ESInputActionCategory.SpecialMove),
                Mode(ESRuntimeMode.PauseMenu, "暂停菜单", "暂停世界，主要允许 UI 输入。", true, true, ESInputActionCategory.UI),
                Mode(ESRuntimeMode.Loading, "加载中", "阻断大多数玩家输入，避免加载中触发 gameplay 行为。", true, true),
                Mode(ESRuntimeMode.Cutscene, "过场", "过场或演出控制玩家输入，保留必要 UI 跳过输入。", true, false, ESInputActionCategory.UI),
                Mode(ESRuntimeMode.Dialogue, "对话", "对话期间通常阻断战斗和移动，允许 UI/交互推进。", true, false, ESInputActionCategory.UI, ESInputActionCategory.Interaction),
                Mode(ESRuntimeMode.Inventory, "背包", "背包 UI 模式。", true, true, ESInputActionCategory.UI),
                Mode(ESRuntimeMode.Map, "地图", "地图 UI 模式。", true, true, ESInputActionCategory.UI),
                Mode(ESRuntimeMode.PhotoMode, "拍照模式", "拍照模式可冻结世界，允许相机/UI 控制。", true, true, ESInputActionCategory.UI, ESInputActionCategory.CameraLook)
            };
        }

        public static List<GameCoreModeTagRule> CreateModeTagRules()
        {
            return new List<GameCoreModeTagRule>
            {
                ModeTag(ESRuntimeModeTag.Combat, "战斗", "角色正在战斗语境中。", "可影响输入、AI警戒、音乐、UI提示。"),
                ModeTag(ESRuntimeModeTag.Aiming, "瞄准", "角色处于瞄准/锁定语境。", "可影响相机、移动速度、射击散布。"),
                ModeTag(ESRuntimeModeTag.Mounted, "骑乘", "角色处于骑乘或载具控制语境。", "可改输入解释和角色运动控制权。"),
                ModeTag(ESRuntimeModeTag.Climbing, "攀爬", "角色处于攀爬语境。", "可限制跳转状态和输入分类。"),
                ModeTag(ESRuntimeModeTag.Dead, "死亡", "角色死亡或不可控。", "应阻断多数 gameplay 输入。"),
                ModeTag(ESRuntimeModeTag.Stunned, "眩晕", "角色暂时不可控。", "应限制移动/战斗输入。"),
                ModeTag(ESRuntimeModeTag.NetworkBusy, "网络繁忙", "等待网络确认或同步。", "可冻结部分交互或重复提交。")
            };
        }

        public static List<GameCoreTagRule> CreateGameTagRules()
        {
            return new List<GameCoreTagRule>
            {
                Tag(ESGameTag.None, "None", "无标签。", "通用", "不要用 None 表达真实状态。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.生命类_死亡, "生命", "实体已死亡，不能再接受常规控制、战斗或交互输入。", "生命域", "实体事实；需要同步输入/UI 时由 Player Runtime Context 单向投影到 ESRuntimeModeTag.Dead。", ESGameTagUsagePolicy.RuntimeFact, ESTagStableTransferScope.SaveGame | ESTagStableTransferScope.Network),
                Tag(ESGameTag.控制类_眩晕, "控制", "实体被眩晕，通常禁止移动与战斗。", "控制/Buff域", "Buff 用 Lease 授予和撤销；不要只改动画状态或 ESRuntimeModeTag。"),
                Tag(ESGameTag.控制类_沉默, "控制", "实体不能施放受沉默约束的技能。", "控制/Buff域", "技能系统应查询该 Tag；具体技能白名单仍由技能规则维护。"),
                Tag(ESGameTag.控制类_定身, "控制", "实体不能自主移动，但不必然禁止旋转、施法或交互。", "控制/Buff域", "不要把定身误当眩晕；对应 Permit 由 Buff 或控制域实施。"),
                Tag(ESGameTag.防御类_霸体, "防御", "实体抵抗特定受击/控制结果。", "战斗/控制域", "只作为命中结算输入，实际免疫规则由 HitResolver 决定。"),
                Tag(ESGameTag.防御类_无敌, "防御", "实体当前免疫约定范围内的伤害或命中。", "战斗/Buff域", "必须由 HitResolver 解释；不能用 Layer 或 Collider 开关替代。"),
                Tag(ESGameTag.感知类_隐身, "感知", "实体处于隐身或不可被常规感知的状态。", "感知/AI/Buff域", "AI 仍需按感知规则处理近距、真视、声音等例外。"),
                Tag(ESGameTag.元素类_燃烧, "状态异常", "实体正在承受燃烧状态。", "Buff域", "状态存在由 Buff Lease 维护；元素伤害类型本身不是该 Tag。"),
                Tag(ESGameTag.元素类_冰冻, "状态异常", "实体正在承受冰冻状态。", "Buff域", "控制、伤害和表现由冰冻 Buff 的具体规则实现。"),
                Tag(ESGameTag.元素类_中毒, "状态异常", "实体正在承受中毒状态。", "Buff域", "持续伤害与层数由 Buff 实例维护，Tag 只表达状态存在。"),
                Tag(ESGameTag.元素类_感电, "状态异常", "实体正在承受感电状态。", "Buff域", "连锁、麻痹等效果由 Buff/HitResolver 决定。"),
                Tag(ESGameTag.战斗类_战斗中, "战斗", "实体处于战斗语境。", "战斗域", "仅实体事实；当前控制玩家需要 UI/输入投影时再同步 ESRuntimeModeTag.Combat。"),
                Tag(ESGameTag.战斗类_瞄准中, "战斗", "实体正在瞄准或锁定瞄准。", "战斗域", "镜头/输入表现由控制玩家 RuntimeModeTag.Aiming 管理，不能双向写入。"),
                Tag(ESGameTag.技能类_施法中, "技能", "实体正在施放技能。", "技能域", "由技能实例的开始/结束路径授予和撤销。"),
                Tag(ESGameTag.技能类_引导中, "技能", "实体正在引导持续型技能。", "技能域", "中断、结束或死亡必须保证撤销来源。"),
                Tag(ESGameTag.移动类_冲刺中, "移动", "实体处于冲刺过程。", "运动/技能域", "不要用动画 State 字符串替代。"),
                Tag(ESGameTag.移动类_跳跃中, "移动", "实体处于跳跃过程。", "运动域", "KCC 落地、取消和强制位移都必须维护该事实。"),
                Tag(ESGameTag.移动类_下落中, "移动", "实体处于下落过程。", "运动域", "这是实体事实，不等同 State 的动画标签。"),
                Tag(ESGameTag.移动类_攀爬中, "移动", "实体处于攀爬控制权中。", "运动域", "控制玩家可单向投影 ESRuntimeModeTag.Climbing，投影句柄必须随状态结束释放。"),
                Tag(ESGameTag.移动类_骑乘中, "移动", "实体处于骑乘或载具控制权中。", "运动域", "控制玩家可单向投影 ESRuntimeModeTag.Mounted，不能以 Tag 代替挂点/控制权逻辑。"),
                Tag(ESGameTag.交互类_可锁定, "能力", "目标允许进入锁定候选。", "Targeting", "能力 Tag；距离、阵营、遮挡和优先级仍由 Targeting 系统判断。", ESGameTagUsagePolicy.Capability),
                Tag(ESGameTag.交互类_可交互, "能力", "对象允许作为交互候选。", "Interaction", "能力 Tag；具体交互资格、占用和任务条件由 Interaction 系统判断。", ESGameTagUsagePolicy.Capability),
                Tag(ESGameTag.交互类_可受击, "能力", "对象允许进入常规伤害候选。", "DamageReceiver", "能力 Tag；无敌、阵营、自伤和部位倍率仍由 HitResolver 判断。", ESGameTagUsagePolicy.Capability),
                Tag(ESGameTag.交互类_可被治疗, "能力", "对象允许进入治疗候选。", "Health/HealResolver", "能力 Tag；治疗归属、上限和特殊规则由治疗系统判断。", ESGameTagUsagePolicy.Capability),
                Tag(ESGameTag.阵营类_友方, "废弃", "旧阵营标签，仅用于迁移读取。", "Faction", "禁止为新功能写入。友敌关系是相对关系，必须使用 FactionId + Relation 服务。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.阵营类_敌方, "废弃", "旧阵营标签，仅用于迁移读取。", "Faction", "禁止为新功能写入。友敌关系是相对关系，必须使用 FactionId + Relation 服务。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.阵营类_中立, "废弃", "旧阵营标签，仅用于迁移读取。", "Faction", "禁止为新功能写入。友敌关系是相对关系，必须使用 FactionId + Relation 服务。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.身份类_玩家, "废弃", "旧身份标签，仅用于迁移读取。", "Spawn/ActorData", "禁止为新功能写入。玩家身份由 ActorDataKind 或 Spawn Archetype 定义。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.身份类_NPC, "废弃", "旧身份标签，仅用于迁移读取。", "Spawn/Archetype", "禁止为新功能写入。NPC 身份由 Spawn Archetype 定义。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.身份类_召唤物, "废弃", "旧身份标签，仅用于迁移读取。", "Spawn/Archetype", "禁止为新功能写入。召唤物归属与生命周期由 Spawn/召唤系统定义。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.身份类_投射物, "废弃", "旧身份标签，仅用于迁移读取。", "Item/Shot", "禁止为新功能写入。投射物是 Item/Shot，不应伪装成 Entity 身份标签。", ESGameTagUsagePolicy.Deprecated),
                Tag(ESGameTag.Reserved32, "保留", "32–63 是未分配的核心位。", "GameCore", "不能运行时写入、不能由 ESTagId 绕过；新增前必须补齐规则、验证与消费者。", ESGameTagUsagePolicy.Deprecated)
            };
        }

        public static List<GameCoreInputCategoryRule> CreateInputCategoryRules()
        {
            return new List<GameCoreInputCategoryRule>
            {
                Input(ESInputActionCategory.Common, "通用", "不明显归属的公共输入。", true, false),
                Input(ESInputActionCategory.Move, "移动", "角色移动、跳跃、蹲伏等。", true, false),
                Input(ESInputActionCategory.CameraLook, "视角", "相机和观察方向。", true, false),
                Input(ESInputActionCategory.Combat, "战斗", "攻击、瞄准、技能、武器槽。", true, false),
                Input(ESInputActionCategory.Interaction, "交互", "门、拾取、对话、机关。", true, false),
                Input(ESInputActionCategory.SpecialMove, "特殊移动", "攀爬、飞行、骑乘等。", true, false),
                Input(ESInputActionCategory.UI, "UI", "菜单、背包、地图、对话 UI。", false, true)
            };
        }

        public static List<GameCorePhysicsLayerRule> CreatePhysicsLayerRules()
        {
            return new List<GameCorePhysicsLayerRule>
            {
                PhysicsLayer("Default", ESPhysicsLayers.Default, "通用", "临时原型或第三方资源。", false, GameCorePhysicsQueryRole.None,
                    "正式玩法对象不得依赖 Default；迁入框架前必须改为明确语义层。", "EntityBody", "EntityHurtbox", "Shot", "Interaction", "TriggerZone", "Sensor"),
                PhysicsLayer("IgnoreRaycast", ESPhysicsLayers.IgnoreRaycast, "表现", "纯表现、装饰、特效。", false, GameCorePhysicsQueryRole.None,
                    "不参与任何框架玩法查询或物理碰撞。", "Default", "IgnoreRaycast", "Water", "UI", "Ground", "Wall", "WorldDynamic", "EntityBody", "EntityHurtbox", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("Water", ESPhysicsLayers.Water, "场景", "水体与游泳/涉水检测。", true, GameCorePhysicsQueryRole.TriggerZoneProbe,
                    "水体使用专用 Trigger 查询；不能作为地面、常规 Shot 阻挡或 Hurtbox。", "Default", "IgnoreRaycast", "Water", "UI", "Ground", "Wall", "WorldDynamic", "EntityBody", "EntityHurtbox", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("UI", ESPhysicsLayers.UI, "UI", "世界空间 UI 或 UI 辅助对象。", false, GameCorePhysicsQueryRole.None,
                    "不得参与 3D 物理、命中、交互或相机避障。", "Default", "IgnoreRaycast", "Water", "UI", "Ground", "Wall", "WorldDynamic", "EntityBody", "EntityHurtbox", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("EntityBody", ESPhysicsLayers.EntityBody, "Entity", "角色 KCC 主身体与占位 Collider。", false, GameCorePhysicsQueryRole.Movement,
                    "用于运动阻挡，不等同受击盒。默认不与其他角色身体、Hurtbox、Shot 或 Trigger 类层发生物理碰撞。", "EntityBody", "EntityHurtbox", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("Ground", ESPhysicsLayers.Ground, "场景", "Terrain、地面与可站立静态面。", false,
                    GameCorePhysicsQueryRole.Movement | GameCorePhysicsQueryRole.GroundProbe | GameCorePhysicsQueryRole.ShotHit | GameCorePhysicsQueryRole.CameraObstacle | GameCorePhysicsQueryRole.AIVisibility | GameCorePhysicsQueryRole.FootIK,
                    "可作为移动、落脚、Shot 阻挡与视线遮挡；不得放 Trigger。", "EntityHurtbox", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("Wall", ESPhysicsLayers.Wall, "场景", "墙、建筑与不可站立静态阻挡。", false,
                    GameCorePhysicsQueryRole.Movement | GameCorePhysicsQueryRole.ShotHit | GameCorePhysicsQueryRole.CameraObstacle | GameCorePhysicsQueryRole.AIVisibility | GameCorePhysicsQueryRole.ClimbProbe,
                    "可作为移动、Shot 阻挡、视线遮挡与攀爬探测候选；攀爬资格仍需 ClimbableSurface。", "EntityHurtbox", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("WorldDynamic", ESPhysicsLayers.WorldDynamic, "场景", "可移动阻挡物、动态门、平台。", false,
                    GameCorePhysicsQueryRole.Movement | GameCorePhysicsQueryRole.GroundProbe | GameCorePhysicsQueryRole.ShotHit | GameCorePhysicsQueryRole.CameraObstacle | GameCorePhysicsQueryRole.AIVisibility | GameCorePhysicsQueryRole.ClimbProbe | GameCorePhysicsQueryRole.FootIK,
                    "可阻挡角色与 Shot；是否可破坏由 DamageReceiver 决定，不新增专用 Layer。", "EntityHurtbox", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("EntityHurtbox", ESPhysicsLayers.EntityHurtbox, "Entity", "角色头、躯干、四肢等受击 Collider。", true,
                    GameCorePhysicsQueryRole.ShotHit | GameCorePhysicsQueryRole.MeleeHit | GameCorePhysicsQueryRole.AITarget,
                    "只供命中/目标查询。不得承担移动阻挡；阵营、自伤、无敌与部位倍率由 HitResolver 判断。", "Default", "IgnoreRaycast", "Water", "UI", "EntityBody", "Ground", "Wall", "WorldDynamic", "EntityHurtbox", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("ItemBody", ESPhysicsLayers.ItemBody, "Item", "箱子、机关、可破坏物、可阻挡道具。", false,
                    GameCorePhysicsQueryRole.Movement | GameCorePhysicsQueryRole.GroundProbe | GameCorePhysicsQueryRole.ShotHit | GameCorePhysicsQueryRole.MeleeHit | GameCorePhysicsQueryRole.CameraObstacle | GameCorePhysicsQueryRole.AIVisibility | GameCorePhysicsQueryRole.FootIK,
                    "可被角色与世界阻挡；若需要交互，额外建立 Interaction 子 Collider。", "EntityHurtbox", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("Interaction", ESPhysicsLayers.Interaction, "交互", "门、拾取、对话、载具与机关的交互代理 Collider。", true,
                    GameCorePhysicsQueryRole.InteractionProbe | GameCorePhysicsQueryRole.MountProbe,
                    "只供交互/骑乘探测；不作为 Shot、近战或移动阻挡。", "Default", "IgnoreRaycast", "Water", "UI", "EntityBody", "EntityHurtbox", "Ground", "Wall", "WorldDynamic", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("TriggerZone", ESPhysicsLayers.TriggerZone, "Item/Trap", "陷阱、区域、检查点与环境触发器。", true, GameCorePhysicsQueryRole.TriggerZoneProbe,
                    "只供区域检测；不作为常规交互、Shot、近战或移动阻挡。", "Default", "IgnoreRaycast", "Water", "UI", "EntityBody", "EntityHurtbox", "Ground", "Wall", "WorldDynamic", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("Shot", ESPhysicsLayers.Shot, "Item/Shot", "飞行物自身的表现与逻辑根节点。", false, GameCorePhysicsQueryRole.None,
                    "Shot 使用主动 Raycast/SphereCast 命中，禁止依赖 OnCollision/OnTrigger；自身不可进入 ShotHitMask。", "Default", "IgnoreRaycast", "Water", "UI", "EntityBody", "EntityHurtbox", "Ground", "Wall", "WorldDynamic", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("CameraBlocker", ESPhysicsLayers.CameraBlocker, "相机", "只用于第三人称相机避障的简化 Collider。", false, GameCorePhysicsQueryRole.CameraObstacle,
                    "不参与角色、Shot、交互或伤害物理；只由相机查询读取。", "Default", "IgnoreRaycast", "Water", "UI", "EntityBody", "EntityHurtbox", "Ground", "Wall", "WorldDynamic", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor"),
                PhysicsLayer("Sensor", ESPhysicsLayers.Sensor, "AI", "AI 感知/警戒体积。", true, GameCorePhysicsQueryRole.None,
                    "仅作为传感器本体；目标采集查询 EntityHurtbox，视线遮挡查询世界阻挡层。", "Default", "IgnoreRaycast", "Water", "UI", "EntityBody", "EntityHurtbox", "Ground", "Wall", "WorldDynamic", "ItemBody", "Interaction", "TriggerZone", "Shot", "CameraBlocker", "Sensor")
            };
        }

        public static List<GameCoreAICommandTemplate> CreateAICommandTemplates()
        {
            return new List<GameCoreAICommandTemplate>
            {
                Command("新增输入动作", "Input", "请按 GameCoreEditorGlobalData 的 Input 分类规范新增输入动作：\n1. 先说明动作属于哪个 ESInputActionCategory。\n2. 修改输入枚举/默认中文名/默认分类。\n3. 如需绑定，修改对应 ESInputConfig 数据结构或配置入口。\n4. 不要绕过 RuntimeMode 过滤。\n需求：<在这里补充具体动作、默认按键、触发方式>"),
                Command("新增或调整GameTag", "Tag", "请按 GameCoreEditorGlobalData 的 GameTag 规范处理标签：\n1. 先判断它是否适合表达可组合、可查询、可撤销的运行时事实或条件；阵营关系、身份、数值、资源定位和状态机执行仍归各自领域。\n2. 只在 GameCoreTagDefinition 声明稳定身份（EnumKey、StringKey 或两者绑定）、StorageTier（HotSlot/Sparse）、Availability 与 SaveGame/Network 传输范围；点击 Bake 后生成 RuntimeKey。Enum 与 String 地位相等，HotSlot 与 Sparse 只决定性能；RuntimeKey 仅当前 Schema 进程可用，禁止写入配置、存档或网络。\n3. 条件统一使用 ESTagConditionConfig 的 required、requiredAny、forbidden；从统一 Picker 选择 ESTagStableReference。业务调用 ESTagCollection.Matches(config) 或 TryMatches(config, out matches, out error)，禁止裸 RuntimeKey、手动编译和按 Enum/String 拆分字段。\n4. P0：策划、业务代码和 Inspector 高频接触的 Tag 名称、字段、菜单与 Picker 文案必须使用直接常用词。P1：禁止为单一 tags 列表创建无职责 Config/Data/Info 包装。写入者直接在真实配置对象上持有 List<ESTagStableReference> tags；ESTagGrantConfig 已删除，不得恢复或兼容。\n5. 写入分三层：Host 自身幂等事实用 Tags.SetTag(tag, active)，无需句柄且只撤销自身一次贡献；Buff、装备、状态环境、区域、任务等外部生命周期使用自己的 ESTagLeaseSet.TryApply/Dispose；只有确需独立释放时才使用 Tags.Acquire 返回 Lease。禁止直接减聚合计数或按 Tag 强制移除。固有 Tag 只由 ActorDataInfo、MonsterDataInfo、NpcDataInfo、ItemDataInfo 的直接 tags 配置。\n6. ESTagCollection 是通用容器；Entity 与 Item 是当前正式 Host。Lease 来源只用于生命周期与按需诊断，不参与 Tag 身份、存储和查询。\n7. Skill、State、AI、Interaction 配置接入 ESTagConditionConfig；HitResolver 只调用 ESHitTagEligibility 决定命中资格，物理候选、伤害、阵营、友伤、部位倍率仍归各自领域。\n8. 存档/联机只传 ESTagStableSnapshot 的稳定引用和 SchemaHash。先在 Catalog 的 stableTransferScopes 声明 SaveGame/Network，再以明确 LeaseSet 恢复；禁止传 RuntimeKey、Count、HotMask 或 Source。\n9. 控制玩家需要输入/UI 投影时，只能由 ESGameManager.LocalControl 声明/切换本地控制实体，禁止任意 Entity 自行创建 Projector。\n10. 交付前运行“验证GameTag规则”“验证全部Buff的GameTag配置”、稳定 Key 审计和核心自检；说明写入者、消费者、Snapshot Scope 与验证结果。\n需求：<在这里补充标签用途>"),
                Command("新增或迁移稳定Key", "Key Governance", "请按项目 Stable Key 治理新增或迁移 Key：\n1. 先判定它是稳定业务身份，还是对象/资源/局部容器键。GUID、LocalFileId、InstanceID、池句柄、Context 临时键不得伪装成业务 ConfigKey。\n2. 稳定业务身份必须声明 Scope、EnumKey（如适用）、StringKey（如适用）、值类型、存储策略、默认值/范围/公式/迁移策略和声明所有者。EnumKey 只在编辑器配置更强，不能压过正式 StringKey。\n3. EnumKey 与 StringKey 同时存在时必须绑定同一条定义；禁止“同 Enum 不同 String”或“同 String 不同类型”静默通过。配置、存档、跨版本数据只保存稳定 Key，禁止保存 RuntimeKey。\n4. 运行时必须先由 ESKeyCatalog 或领域 Catalog 解析 RuntimeKey；HotSlot 与 Sparse 只决定内存策略，不改变身份权威性。每个 StringKey RuntimeKey 必须不依赖注册顺序。\n5. 联机/热更新边界校验 CatalogName + SchemaHash；不得假定两端 RuntimeKey 相同。\n6. 更新或新增编辑器诊断：声明者、读取者、写入者、未使用项、别名冲突、类型冲突和迁移遗漏。\n7. 对属性使用 ESSuperAttributeCatalog；对 Tag 使用 ESTagBakeTable；对 GameCore/资产使用有 Scope 的 ESConfigKeyTable。先运行对应自检和编译验证，再交付变更。\n需求：<这里填写领域、Key、值类型、存档/网络/热更要求>"),
                Command("修正游戏3D Layer规划", "Physics", "请把 GameCoreEditorGlobalData.physicsLayers 作为 FPS/TPS、ARPG、开放世界、潜行、生存、平台、竞速等标准 3D 游戏的核心物理分类；Layer 只表达 Physics 碰撞/查询职责，不表达阵营、职业、阵营关系、伤害类型或业务身份。先读取既有规则和 TagManager/DynamicsManager，再实施修改。\n\n固定核心 Layer：0 Default（仅临时/第三方）、2 Ignore Raycast（纯表现）、4 Water（Trigger 专用查询）、5 UI、6 EntityBody（KCC 主身体，非 Trigger）、8 Ground（可站立静态面）、9 Wall（不可站立静态阻挡）、10 WorldDynamic（门、平台、载具主体、动态阻挡）、11 EntityHurtbox（Trigger）、12 ItemBody（箱子/机关/可破坏物）、13 Interaction（Trigger）、14 TriggerZone（Trigger）、15 Shot（主动 Cast，非 Trigger）、16 CameraBlocker、17 Sensor（Trigger）。\n\n跨游戏类型复用原则：\n1. 玩家、NPC、敌人、Boss 共用 EntityBody + EntityHurtbox；同一对象需要阻挡、受击、交互时，使用主 Collider + 受击/交互子 Trigger Collider，不合并职责。\n2. 地形/地板用 Ground，墙体/建筑用 Wall；门、移动平台、车辆物理主体用 WorldDynamic；可破坏、可阻挡道具用 ItemBody。Ragdoll 默认继承 EntityBody 与世界交互；只有确实需要 Ragdoll 间独立物理关系时，才申请扩展层。\n3. 子弹、箭、投掷物、魔法弹和近战投射体根节点都用 Shot；命中使用主动 Raycast/SphereCast，不依赖 OnCollision/OnTrigger。交互、拾取、载具入口用 Interaction；陷阱、检查点、毒圈、水体等区域用 TriggerZone/Water。\n4. Collider 的 isTrigger 必须遵循 physicsLayers.mustBeTrigger；世界/身体/ItemBody/Shot 不可为 Trigger，Hurtbox/Interaction/TriggerZone/Sensor/Water 必须为 Trigger。\n5. 主动查询只用 ESPhysicsLayers 或 ESPhysicsLayerConfig 的语义 Mask：ShotHit=Ground|Wall|WorldDynamic|EntityHurtbox|ItemBody；交互只查 Interaction；区域/水体只查 TriggerZone/Water；AI 目标查 EntityHurtbox，视线查 WorldBlocker。不得使用 ~0。\n6. 阵营、自伤、友伤、无敌、部位倍率、拾取资格和任务状态属于 HitResolver/业务规则，不能靠新 Layer 或碰撞矩阵表达。\n\n扩展政策：18–23 为项目审核后的物理扩展位。仅当新对象无法用上述“主 Collider + 玩法子 Collider”组合表达，且确实需要一套新的碰撞矩阵或查询目标时，才能占用；必须同时登记编号、用途、Trigger、QueryRole、禁止碰撞对，更新 ESPhysicsLayers/ESPhysicsLayerConfig，并同步 Unity Layer 与碰撞矩阵。2D 项目使用独立 Physics2D 规划，不复用本 3D 矩阵。\n\n交付时说明：复用或新增的 Layer、Collider Trigger 状态、使用的查询 Mask、变更的碰撞对，以及同步/验证结果。\n需求：<在此补充对象、命中/交互/移动需求>"),
                Command("新增飞行物类型", "Item/Shot", "请按 Item Shot shared/variable 规范新增飞行物能力：\n1. Shared 放同类共享模板，Variable 放每发独有变量。\n2. ItemShotModule 只产生命中候选，不直接做伤害/VFX/池回收。\n3. 随机性必须由 logicSeed 或发射输入决定。\n4. 必中是合法模式，不是碰撞特例。\n需求：<在这里补充弹道、命中、表现和网络要求>")
            };
        }

        private static GameCoreModeRule Mode(ESRuntimeMode mode, string displayName, string purpose, bool blocksGameplayInput, bool pausesWorldTime, params ESInputActionCategory[] allowed)
        {
            GameCoreModeRule rule = new GameCoreModeRule
            {
                mode = mode,
                displayName = displayName,
                purpose = purpose,
                blocksGameplayInput = blocksGameplayInput,
                pausesWorldTime = pausesWorldTime
            };
            if (allowed != null)
                rule.allowedInputCategories.AddRange(allowed);
            return rule;
        }

        private static GameCoreModeTagRule ModeTag(ESRuntimeModeTag tag, string displayName, string purpose, string effect)
        {
            return new GameCoreModeTagRule { tag = tag, displayName = displayName, purpose = purpose, effect = effect };
        }

        private static GameCoreTagRule Tag(
            ESGameTag tag,
            string group,
            string meaning,
            string owner,
            string warning,
            ESGameTagUsagePolicy usagePolicy = ESGameTagUsagePolicy.RuntimeFact,
            ESTagStableTransferScope stableTransferScopes = ESTagStableTransferScope.None)
        {
            return new GameCoreTagRule
            {
                tag = tag,
                group = group,
                meaning = meaning,
                ownerSystem = owner,
                warning = warning,
                usagePolicy = usagePolicy,
                stableTransferScopes = stableTransferScopes
            };
        }

        private static GameCoreInputCategoryRule Input(ESInputActionCategory category, string displayName, string purpose, bool gameplay, bool ui)
        {
            return new GameCoreInputCategoryRule { category = category, displayName = displayName, purpose = purpose, allowedInGameplay = gameplay, allowedInUI = ui };
        }

        private static GameCorePhysicsLayerRule PhysicsLayer(
            string name,
            int unityLayer,
            string owner,
            string usedBy,
            bool mustBeTrigger,
            GameCorePhysicsQueryRole queryRoles,
            string rule,
            params string[] forbiddenCollisionLayers)
        {
            return new GameCorePhysicsLayerRule
            {
                semanticName = name,
                unityLayer = unityLayer,
                owner = owner,
                usedBy = usedBy,
                mustBeTrigger = mustBeTrigger,
                queryRoles = queryRoles,
                forbiddenCollisionLayers = forbiddenCollisionLayers == null
                    ? new List<string>()
                    : new List<string>(forbiddenCollisionLayers),
                rule = rule
            };
        }

        private static GameCoreAICommandTemplate Command(string title, string category, string text)
        {
            return new GameCoreAICommandTemplate
            {
                title = title,
                category = category,
                commandText = string.IsNullOrEmpty(text)
                    ? text
                    : text.Replace("GameCoreGlobalData", "GameCoreEditorGlobalData")
            };
        }
    }
}
