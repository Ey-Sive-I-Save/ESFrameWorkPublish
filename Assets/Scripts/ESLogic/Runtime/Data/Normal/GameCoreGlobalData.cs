using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [CreateAssetMenu(fileName = "GameCoreGlobalData", menuName = "ES/GameCoreGlobalData")]
    [ESCreatePath("全局数据", "GameCore全局数据")]
    public class GameCoreGlobalData : ESEditorGlobalSo<GameCoreGlobalData>
    {
        [Title("说明")]
        [LabelText("用途")]
        [MultiLineProperty(3)]
        public string description = "GameCoreGlobalData 是项目核心语义入口：集中说明 GameMode、ModeTag、GameTag、Input 分类、物理层语义和 AI Command 模板。不挂 StateMachineConfig，不做代码生成，不替代具体业务数据。";

        [Title("GameMode")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreModeRule> gameModes = new List<GameCoreModeRule>();

        [Title("GameModeTag")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreModeTagRule> gameModeTags = new List<GameCoreModeTagRule>();

        [Title("GameTag")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreTagRule> gameTags = new List<GameCoreTagRule>();

        [Title("Input分类")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCoreInputCategoryRule> inputCategories = new List<GameCoreInputCategoryRule>();

        [Title("物理层语义")]
        [ListDrawerSettings(DefaultExpandedState = false)]
        public List<GameCorePhysicsLayerRule> physicsLayers = new List<GameCorePhysicsLayerRule>();

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
            inputCategories = GameCoreDefaultRules.CreateInputCategoryRules();
            physicsLayers = GameCoreDefaultRules.CreatePhysicsLayerRules();
            aiCommandTemplates = GameCoreDefaultRules.CreateAICommandTemplates();
        }
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

        [LabelText("警告")]
        [MultiLineProperty(2)]
        public string warning;
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
                Tag(ESGameTag.None, "None", "无标签。", "通用", "不要用 None 表达真实状态。"),
                Tag(ESGameTag.Reserved32, "Reserved", "预留扩展位。", "GameCore", "新增 GameTag 前先在 GameCoreGlobalData 说明语义，再改枚举。"),
                Tag(ESGameTag.Reserved33, "Reserved", "预留扩展位。", "GameCore", "不要让 AI 随意占用预留位。")
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
                PhysicsLayer("WorldStatic", "场景", "地形、墙、不可移动阻挡。", "角色移动、Shot阻挡、视线检测都可查。不要把触发器塞进此层。"),
                PhysicsLayer("WorldDynamic", "场景", "可移动阻挡物。", "可被角色/Shot阻挡。需要业务决定是否可破坏。"),
                PhysicsLayer("EntityBody", "Entity", "角色主身体。", "用于占位和运动阻挡，不等同受击盒。"),
                PhysicsLayer("EntityHurtbox", "Entity", "角色受击盒。", "武器和 Shot 命中优先查此层。"),
                PhysicsLayer("ItemBody", "Item", "世界逻辑体物体。", "门、机关、箱子、可阻挡 Item。"),
                PhysicsLayer("Interaction", "交互", "交互探测目标。", "交互模块查此层，不要靠 Shot 命中消费。"),
                PhysicsLayer("TriggerZone", "Item/Trap", "陷阱、区域、触发体。", "低频触发或分帧 Overlap。"),
                PhysicsLayer("Shot", "Item/Shot", "飞行物自身。", "通常不被自己的命中查询检测。")
            };
        }

        public static List<GameCoreAICommandTemplate> CreateAICommandTemplates()
        {
            return new List<GameCoreAICommandTemplate>
            {
                Command("新增输入动作", "Input", "请按 GameCoreGlobalData 的 Input 分类规范新增输入动作：\n1. 先说明动作属于哪个 ESInputActionCategory。\n2. 修改输入枚举/默认中文名/默认分类。\n3. 如需绑定，修改对应 ESInputConfig 数据结构或配置入口。\n4. 不要绕过 RuntimeMode 过滤。\n需求：<在这里补充具体动作、默认按键、触发方式>"),
                Command("新增GameTag", "Tag", "请按 GameCoreGlobalData 的 GameTag 规范新增标签：\n1. 先说明分组、语义、归属系统、互斥关系。\n2. 优先使用 Reserved 位，保持数值稳定。\n3. 修改 ESGameTag 枚举和相关说明。\n4. 不要用 Tag 直接替代 Buff/State 的完整逻辑。\n需求：<在这里补充标签用途>"),
                Command("新增物理层语义", "Physics", "请按 GameCoreGlobalData 的物理层语义新增或调整 Layer：\n1. 说明层名、归属、被哪些查询使用。\n2. 更新 GameCoreGlobalData 默认规则说明。\n3. 必要时更新 ESPhysicsLayerConfig 默认字段或使用方。\n4. 不要只在某个业务脚本里硬编码 LayerMask。\n需求：<在这里补充碰撞/查询目标>"),
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

        private static GameCoreTagRule Tag(ESGameTag tag, string group, string meaning, string owner, string warning)
        {
            return new GameCoreTagRule { tag = tag, group = group, meaning = meaning, ownerSystem = owner, warning = warning };
        }

        private static GameCoreInputCategoryRule Input(ESInputActionCategory category, string displayName, string purpose, bool gameplay, bool ui)
        {
            return new GameCoreInputCategoryRule { category = category, displayName = displayName, purpose = purpose, allowedInGameplay = gameplay, allowedInUI = ui };
        }

        private static GameCorePhysicsLayerRule PhysicsLayer(string name, string owner, string usedBy, string rule)
        {
            return new GameCorePhysicsLayerRule { semanticName = name, owner = owner, usedBy = usedBy, rule = rule };
        }

        private static GameCoreAICommandTemplate Command(string title, string category, string text)
        {
            return new GameCoreAICommandTemplate { title = title, category = category, commandText = text };
        }
    }
}
