using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace ES
{
    [Serializable, TypeRegistryItem("标准状态共享数据")]
    public class StateSharedData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;
        
        #region 基础
        // ========================================
        // 基础配置（必填）
        // ========================================
        [TabGroup("基础", "核心"), PropertyOrder(0)]
        [BoxGroup("基础/核心/配置", ShowLabel = false)]
        [InlineProperty, HideLabel]
        [InfoBox("步骤1：设置状态名、流水线、优先级", InfoMessageType.Info)]
        public StateBasicConfig basicConfig = new StateBasicConfig();
        
        // ========================================
        // 动画配置
        // ========================================
        [TabGroup("基础", "动画"), PropertyOrder(1)]
        [BoxGroup("基础/动画/开关", ShowLabel = false)]
        [HorizontalGroup("基础/动画/开关/Toggle")]
        [LabelText("启用动画"), ToggleLeft, GUIColor(0.6f, 1f, 0.8f)]
        [InfoBox("步骤2：需要动画时先勾选启用动画", InfoMessageType.Info)]
        public bool hasAnimation = false;
        
        [BoxGroup("基础/动画/开关", ShowLabel = false)]
        [ShowIf("hasAnimation")]
        [InlineProperty, HideLabel]
        [PropertySpace(SpaceBefore = 5, SpaceAfter = 0)]
        public StateAnimationConfigData animationConfig = new StateAnimationConfigData();

        [BoxGroup("基础/动画/过渡", ShowLabel = true), PropertyOrder(2)]
        [ShowIf("hasAnimation")]
        [InfoBox("步骤3：设置淡入淡出", InfoMessageType.None)]
        [HorizontalGroup("基础/动画/过渡/Toggle")]
        [LabelText("启用淡入淡出"), ToggleLeft, GUIColor(0.8f, 0.9f, 1f)]
        public bool enableFadeInOut = true;

        [ShowIf("@hasAnimation && enableFadeInOut")]
        [BoxGroup("基础/动画/过渡", ShowLabel = true)]
        [HorizontalGroup("基础/动画/过渡/Base", Width = 0.5f)]
        [LabelText("跟随时间缩放"), ToggleLeft]
        [Tooltip("倍速变化时，淡入淡出同步变化")]
        public bool fadeFollowTimeScale = true;

        [ShowIf("@hasAnimation && enableFadeInOut")]
        [BoxGroup("基础/动画/过渡", ShowLabel = true)]
        [HorizontalGroup("基础/动画/过渡/Base", Width = 0.5f)]
        [LabelText("淡入淡出速度"), Range(0.1f, 3f)]
        [Tooltip("1为默认，>1更快，<1更慢")]
        public float fadeSpeedMultiplier = 1f;

        [ShowIf("@hasAnimation && enableFadeInOut")]
        [BoxGroup("基础/动画/过渡", ShowLabel = true)]
        [HorizontalGroup("基础/动画/过渡/Durations", Width = 0.5f)]
        [LabelText("淡入时长"), Range(0f, 2f), SuffixLabel("秒", Overlay = true)]
        public float fadeInDuration = 0.2f;

        [ShowIf("@hasAnimation && enableFadeInOut")]
        [BoxGroup("基础/动画/过渡", ShowLabel = true)]
        [HorizontalGroup("基础/动画/过渡/Durations", Width = 0.5f)]
        [LabelText("淡出时长"), Range(0f, 2f), SuffixLabel("秒", Overlay = true)]
        public float fadeOutDuration = 0.15f;

        [ShowIf("@hasAnimation && enableFadeInOut")]
        [BoxGroup("基础/动画/过渡", ShowLabel = true)]
        [HorizontalGroup("基础/动画/过渡/Advanced", Width = 0.5f)]
        [LabelText("使用曲线"), ToggleLeft]
        [Tooltip("开启后可设置曲线；关闭时为线性")]
        public bool useAdvancedFadeCurve = false;

        [BoxGroup("基础/动画/过渡", ShowLabel = true)]
        [ShowIf("@hasAnimation && enableFadeInOut && useAdvancedFadeCurve")]
        [LabelText("淡入曲线")]
        [Tooltip("自定义淡入权重曲线")]
        [PropertySpace(SpaceBefore = 5)]
        public AnimationCurve fadeInCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [BoxGroup("基础/动画/过渡", ShowLabel = true)]
        [ShowIf("@hasAnimation && enableFadeInOut && useAdvancedFadeCurve")]
        [LabelText("淡出曲线")]
        [Tooltip("自定义淡出权重曲线")]
        public AnimationCurve fadeOutCurve = AnimationCurve.Linear(0, 1, 1, 0);

        // ========================================
        // 标记信息（可选）
        // ========================================
        [TabGroup("基础", "标记"), PropertyOrder(3)]
        [BoxGroup("基础/标记/分类", ShowLabel = true)]
        [LabelText("标签")]
        [InfoBox("可选：用于分类和查询（如 Attack / Movement / Skill）", InfoMessageType.None)]
        [PropertySpace(SpaceBefore = 0, SpaceAfter = 5)]
        public List<string> tags = new List<string>();

        [BoxGroup("基础/标记/分类", ShowLabel = true)]
        [LabelText("分组"), Tooltip("用于UI分组")]
        public string group = "Default";

        [BoxGroup("基础/标记/显示", ShowLabel = true)]
        [LabelText("显示名"), Tooltip("用于UI显示；留空使用状态名")]
        [PropertySpace(SpaceBefore = 5)]
        public string displayName = "";

        [BoxGroup("基础/标记/显示", ShowLabel = true)]
        [LabelText("描述"), MultiLineProperty(3)]
        public string description = "";

        [BoxGroup("基础/标记/显示", ShowLabel = true)]
        [LabelText("图标"), PreviewField(60, ObjectFieldAlignment.Left)]
        [PropertySpace(SpaceAfter = 5)]
        public Sprite icon;

        #endregion

        #region 切换
        // ========================================
        // 切换配置
        // ========================================
        [TabGroup("切换", "冲突"), PropertyOrder(10)]
        [BoxGroup("切换/冲突/规则", ShowLabel = false)]
        [InfoBox("步骤4：设置并行规则和通道占用", InfoMessageType.Info)]
        [InlineProperty, HideLabel]
        [PropertySpace(SpaceBefore = 0, SpaceAfter = 5)]
        public StateMergeData mergeData = new StateMergeData();

        [TabGroup("切换", "代价"), PropertyOrder(11)]
        [BoxGroup("切换/代价/权重", ShowLabel = false)]
        [InfoBox("步骤5：设置代价与权重", InfoMessageType.Info)]
        [InlineProperty, HideLabel]
        [PropertySpace(SpaceBefore = 0, SpaceAfter = 5)]
        public StateCostData costData = new StateCostData();

        #endregion

        #region 扩展
        // ========================================
        // 运行时配置
        // ========================================
        [TabGroup("扩展", "运行时"), PropertyOrder(20)]
        [BoxGroup("扩展/运行时/替换", ShowLabel = true)]
        [HorizontalGroup("扩展/运行时/替换/Row1")]
        [LabelText("允许运行时替换"), ToggleLeft, GUIColor(1f, 0.9f, 0.6f)]
        [Tooltip("运行时允许替换该状态配置")]
        public bool canReplaceAtRuntime = false;

        [BoxGroup("扩展/运行时/替换", ShowLabel = true)]
        [HorizontalGroup("扩展/运行时/替换/Row1")]
        [ShowIf("canReplaceAtRuntime")]
        [LabelText("替换时保留数据"), ToggleLeft]
        [Tooltip("保留当前运行数据")]
        public bool keepDataOnReplace = true;

        [BoxGroup("扩展/运行时/临时", ShowLabel = true)]
        [HorizontalGroup("扩展/运行时/临时/Row1")]
        [LabelText("可作为临时状态"), ToggleLeft, GUIColor(0.8f, 1f, 0.9f)]
        [Tooltip("可通过AddTemporaryAnimation临时加入")]
        [PropertySpace(SpaceBefore = 5)]
        public bool canBeTemporary = true;

        [BoxGroup("扩展/运行时/临时", ShowLabel = true)]
        [HorizontalGroup("扩展/运行时/临时/Row1")]
        [ShowIf("canBeTemporary")]
        [LabelText("完成后自动移除"), ToggleLeft]
        [Tooltip("播放完毕后自动移除")]
        public bool autoRemoveWhenDone = true;

        [BoxGroup("扩展/运行时/覆盖", ShowLabel = true)]
        [HorizontalGroup("扩展/运行时/覆盖/Row1")]
        [LabelText("允许覆盖注册"), ToggleLeft, GUIColor(1f, 0.8f, 0.6f)]
        [Tooltip("注册同名状态时允许覆盖")]
        [PropertySpace(SpaceBefore = 5)]
        public bool allowOverride = false;

        [BoxGroup("扩展/运行时/覆盖", ShowLabel = true)]
        [HorizontalGroup("扩展/运行时/覆盖/Row1")]
        [ShowIf("allowOverride")]
        [LabelText("覆盖时通知"), ToggleLeft]
        [Tooltip("被覆盖时发出通知")]
        public bool notifyOnOverride = true;

        // ========================================
        // 调试配置
        // ========================================
        [TabGroup("扩展", "调试"), PropertyOrder(22)]
        [BoxGroup("扩展/调试/显示", ShowLabel = true)]
        [HorizontalGroup("扩展/调试/显示/Row1")]
        [LabelText("显示调试信息"), ToggleLeft, GUIColor(0.9f, 0.9f, 1f)]
        public bool showDebugInfo = false;

        [BoxGroup("扩展/调试/显示", ShowLabel = true)]
        [HorizontalGroup("扩展/调试/显示/Row1")]
        [LabelText("调试颜色")]
        public Color debugGizmoColor = Color.white;

        #endregion

        /// <summary>
        /// 初始化运行数据
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            
            // 初始化所有子配置
            basicConfig?.InitializeRuntime();
            animationConfig?.InitializeRuntime();
            mergeData?.InitializeRuntime();

            // 计算主状态优先级
            basicConfig?.PrepareMainCriterionValue();
            
            _isRuntimeInitialized = true;
        }

#if UNITY_EDITOR
    #region 编辑器工具
    // ========================================
    // 编辑器工具
    // ========================================

        /// <summary>
        /// 编辑器手动初始化
        /// </summary>
        [Button("初始化运行数据", ButtonSizes.Medium), PropertyOrder(-2)]
        [InfoBox("初始化后可查看主状态优先级", InfoMessageType.Warning, "@_isRuntimeInitialized")]
        private void EditorInitialize()
        {
            _isRuntimeInitialized = false; // 允许重新初始化
            InitializeRuntime();
            Debug.Log($"[StateSharedData] 初始化完成 | 主状态优先级: {basicConfig.preparedMainCriterionValue:F2}");
        }

        private enum CommonStatePreset
        {
            Default,
            Idle,
            Walk,
            Run,
            Sprint,
            Jump,
            Fall,
            Land,
            Attack,
            Skill,
            Hit,
            Die,
            Revive,
            Swim,
            Fly
        }

        [Button("预设菜单", ButtonSizes.Medium), PropertyOrder(-1)]
        [GUIColor(0.7f, 0.85f, 1f)]
        private void OpenPresetMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("通用默认"), false, () => ApplyCommonPreset(CommonStatePreset.Default));
            menu.AddItem(new GUIContent("待机"), false, () => ApplyCommonPreset(CommonStatePreset.Idle));
            menu.AddItem(new GUIContent("行走"), false, () => ApplyCommonPreset(CommonStatePreset.Walk));
            menu.AddItem(new GUIContent("奔跑"), false, () => ApplyCommonPreset(CommonStatePreset.Run));
            menu.AddItem(new GUIContent("冲刺"), false, () => ApplyCommonPreset(CommonStatePreset.Sprint));
            menu.AddItem(new GUIContent("跳跃"), false, () => ApplyCommonPreset(CommonStatePreset.Jump));
            menu.AddItem(new GUIContent("下落"), false, () => ApplyCommonPreset(CommonStatePreset.Fall));
            menu.AddItem(new GUIContent("落地"), false, () => ApplyCommonPreset(CommonStatePreset.Land));
            menu.AddItem(new GUIContent("攻击"), false, () => ApplyCommonPreset(CommonStatePreset.Attack));
            menu.AddItem(new GUIContent("技能"), false, () => ApplyCommonPreset(CommonStatePreset.Skill));
            menu.AddItem(new GUIContent("受击"), false, () => ApplyCommonPreset(CommonStatePreset.Hit));
            menu.AddItem(new GUIContent("死亡"), false, () => ApplyCommonPreset(CommonStatePreset.Die));
            menu.AddItem(new GUIContent("复活"), false, () => ApplyCommonPreset(CommonStatePreset.Revive));
            menu.AddItem(new GUIContent("游泳"), false, () => ApplyCommonPreset(CommonStatePreset.Swim));
            menu.AddItem(new GUIContent("飞行"), false, () => ApplyCommonPreset(CommonStatePreset.Fly));
            menu.ShowAsContext();
        }

        [Button("应用攻击预设", ButtonSizes.Medium), PropertyOrder(-1)]
        [GUIColor(1f, 0.6f, 0.6f)]
        private void ApplyAttackPreset() => ApplyCommonPreset(CommonStatePreset.Attack);

        [Button("应用移动预设", ButtonSizes.Medium), PropertyOrder(-1)]
        [GUIColor(0.6f, 1f, 0.6f)]
        private void ApplyMovementPreset() => ApplyCommonPreset(CommonStatePreset.Run);

        [Button("应用临时技能预设", ButtonSizes.Medium), PropertyOrder(-1)]
        [GUIColor(0.8f, 0.8f, 1f)]
        private void ApplyTemporarySkillPreset() => ApplyCommonPreset(CommonStatePreset.Skill);

        private void ApplyCommonPreset(CommonStatePreset preset)
        {
            SetTags();
            group = "Default";
            displayName = "";
            description = "";

            hasAnimation = true;
            enableFadeInOut = true;
            fadeFollowTimeScale = true;
            fadeSpeedMultiplier = 1f;
            fadeInDuration = 0.2f;
            fadeOutDuration = 0.2f;
            useAdvancedFadeCurve = false;

            canBeTemporary = false;
            autoRemoveWhenDone = true;

            switch (preset)
            {
                case CommonStatePreset.Default:
                    displayName = "通用";
                    description = "通用基础状态";
                    break;
                case CommonStatePreset.Idle:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "待机";
                    description = "站立待机";
                    break;
                case CommonStatePreset.Walk:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "行走";
                    description = "正常行走";
                    break;
                case CommonStatePreset.Run:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "奔跑";
                    description = "快速移动";
                    fadeInDuration = 0.15f;
                    fadeOutDuration = 0.15f;
                    break;
                case CommonStatePreset.Sprint:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "冲刺";
                    description = "短时间加速";
                    fadeInDuration = 0.1f;
                    fadeOutDuration = 0.12f;
                    break;
                case CommonStatePreset.Jump:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "跳跃";
                    description = "起跳阶段";
                    fadeInDuration = 0.05f;
                    fadeOutDuration = 0.1f;
                    break;
                case CommonStatePreset.Fall:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "下落";
                    description = "空中下落";
                    fadeInDuration = 0.05f;
                    fadeOutDuration = 0.1f;
                    break;
                case CommonStatePreset.Land:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "落地";
                    description = "落地过渡";
                    fadeInDuration = 0.05f;
                    fadeOutDuration = 0.1f;
                    break;
                case CommonStatePreset.Attack:
                    SetTags("Attack");
                    group = "Combat";
                    displayName = "攻击";
                    description = "普通攻击";
                    fadeInDuration = 0.1f;
                    fadeOutDuration = 0.15f;
                    break;
                case CommonStatePreset.Skill:
                    SetTags("Skill");
                    group = "Skills";
                    displayName = "技能";
                    description = "临时技能";
                    canBeTemporary = true;
                    autoRemoveWhenDone = true;
                    fadeInDuration = 0.05f;
                    fadeOutDuration = 0.1f;
                    break;
                case CommonStatePreset.Hit:
                    SetTags("Hit");
                    group = "Combat";
                    displayName = "受击";
                    description = "受击反应";
                    canBeTemporary = true;
                    autoRemoveWhenDone = true;
                    fadeInDuration = 0.05f;
                    fadeOutDuration = 0.1f;
                    break;
                case CommonStatePreset.Die:
                    SetTags("Dead");
                    group = "Combat";
                    displayName = "死亡";
                    description = "死亡状态";
                    fadeInDuration = 0.05f;
                    fadeOutDuration = 0.2f;
                    break;
                case CommonStatePreset.Revive:
                    SetTags("Revive");
                    group = "Combat";
                    displayName = "复活";
                    description = "复活过程";
                    canBeTemporary = true;
                    autoRemoveWhenDone = true;
                    fadeInDuration = 0.1f;
                    fadeOutDuration = 0.1f;
                    break;
                case CommonStatePreset.Swim:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "游泳";
                    description = "水中移动";
                    break;
                case CommonStatePreset.Fly:
                    SetTags("Movement");
                    group = "Locomotion";
                    displayName = "飞行";
                    description = "空中移动";
                    break;
            }

            Debug.Log($"[StateSharedData] 已应用预设: {preset}");
        }

        private void SetTags(params string[] newTags)
        {
            if (tags == null) tags = new List<string>();
            tags.Clear();
            if (newTags == null) return;
            for (int i = 0; i < newTags.Length; i++)
            {
                var tag = newTags[i];
                if (string.IsNullOrEmpty(tag)) continue;
                tags.Add(tag);
            }
        }

        #endregion
#endif

        #region 便捷接口（运行时使用）
        // ========================================
        // 便捷接口（运行时使用）
        // ========================================
        
        /// <summary>
        /// 检查是否包含指定标签
        /// </summary>
        public bool HasTag(string tag)
        {
            return tags != null && tags.Contains(tag);
        }

        /// <summary>
        /// 添加标签
        /// </summary>
        public void AddTag(string tag)
        {
            if (tags == null)
                tags = new List<string>();
            if (!tags.Contains(tag))
                tags.Add(tag);
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public void RemoveTag(string tag)
        {
            tags?.Remove(tag);
        }

        /// <summary>
        /// 获取显示名称（未设置则返回默认名）
        /// </summary>
        public string GetDisplayName(string fallbackKey = "")
        {
            return !string.IsNullOrEmpty(displayName) ? displayName : fallbackKey;
        }

        /// <summary>
        /// 克隆配置（用于运行时替换保留数据）
        /// </summary>
        public StateSharedData Clone()
        {
            // 浅拷贝足够（子配置对象不会被修改引用）
            return (StateSharedData)this.MemberwiseClone();
        }
        
        /// <summary>
        /// 克隆并替换动画 - 同一配置复用不同动画
        /// 常见场景: 相同逻辑但不同动画
        /// </summary>
        /// <param name="newStateName">新状态名</param>
        /// <param name="newStateId">新状态ID（-1自动分配）</param>
        /// <param name="newAnimation">新动画配置</param>
        /// <returns>克隆的StateSharedData</returns>
        public StateSharedData CloneWithAnimation(string newStateName, int newStateId, StateAnimationConfigData newAnimation)
        {
            var cloned = Clone();
            cloned._isRuntimeInitialized = false; // 需要重新初始化
            
            // 手动复制基础配置的重要字段（避免引用共享）
            var originalBasic = this.basicConfig;
            cloned.basicConfig = new StateBasicConfig
            {
                stateName = newStateName,
                stateId = newStateId,
                pipelineType = originalBasic.pipelineType,
                priority = originalBasic.priority,
                durationMode = originalBasic.durationMode,
                timedDuration = originalBasic.timedDuration,
                phaseConfig = originalBasic.phaseConfig, // 阶段配置可共享
                canBeFeedback = originalBasic.canBeFeedback,
                stateSupportFlag = originalBasic.stateSupportFlag,
               
            };
            
            // 替换动画
            cloned.animationConfig = newAnimation;
            cloned.hasAnimation = newAnimation != null;
            
            // 其他配置共享（mergeData, costData等保持引用）
            return cloned;
        }
        
        /// <summary>
        /// 克隆并批量替换动画 - 生成多个同配置不同动画的状态
        /// </summary>
        /// <param name="baseNamePrefix">状态名前缀（如 "Walk_"）</param>
        /// <param name="baseIdStart">起始ID（-1则自动分配）</param>
        /// <param name="animations">动画配置数组</param>
        /// <param name="nameSuffixes">名称后缀（可选，如 ["Forward", "Backward"]）</param>
        /// <returns>克隆的StateSharedData数组</returns>
        public StateSharedData[] CloneWithAnimations(
            string baseNamePrefix, 
            int baseIdStart, 
            StateAnimationConfigData[] animations, 
            string[] nameSuffixes = null)
        {
            if (animations == null || animations.Length == 0)
                return new StateSharedData[0];
            
            var results = new StateSharedData[animations.Length];
            for (int i = 0; i < animations.Length; i++)
            {
                string suffix = (nameSuffixes != null && i < nameSuffixes.Length) 
                    ? nameSuffixes[i] 
                    : i.ToString();
                    
                string newName = baseNamePrefix + suffix;
                int newId = baseIdStart > 0 ? baseIdStart + i : -1; // -1触发自动分配
                
                results[i] = CloneWithAnimation(newName, newId, animations[i]);
            }
            
            return results;
        }
        
        /// <summary>
        /// 快速创建动画变体 - 仅替换单个AnimationClip
        /// 简单复用: 同一配置 + 不同Clip
        /// </summary>
        /// <param name="newStateName">新状态名</param>
        /// <param name="newStateId">新状态ID（-1自动分配）</param>
        /// <param name="newClip">新Clip</param>
        /// <returns>克隆的StateSharedData</returns>
        public StateSharedData CloneWithClip(string newStateName, int newStateId, AnimationClip newClip)
        {
            if (newClip == null)
            {
                Debug.LogWarning($"[StateSharedData] CloneWithClip: Clip为空，使用原动画配置");
                return CloneWithAnimation(newStateName, newStateId, animationConfig);
            }
            
            // 创建新动画配置（SimpleClip模式）
            var newAnimConfig = new StateAnimationConfigData
            {
                calculator = new StateAnimationMixCalculatorForSimpleClip
                {
                    clip = newClip
                }
            };
            
            return CloneWithAnimation(newStateName, newStateId, newAnimConfig);
        }

        #endregion


    }

    [Serializable, TypeRegistryItem("标准状态运行状态")]
    //目前还没有特殊的玩意
    public class StateVariableData : IDeepClone<StateVariableData>
    {
        [LabelText("状态开始时间")] public float hasEnterTime;
        public void DeepCloneFrom(StateVariableData t)
        {
            if (t is StateVariableData svd)
            {
                hasEnterTime = svd.hasEnterTime;
            }
        }
    }

}
