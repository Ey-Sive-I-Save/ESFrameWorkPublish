
using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    #region 核心数据模块定义
    
    /// <summary>
    /// 状态基础配置 - 标识与生命周期
    /// </summary>
    [Serializable]
    public class StateBasicConfig
    {
        [HorizontalGroup("Identity", Width = 0.4f)]
        [VerticalGroup("Identity/Left")]
        [LabelText("状态ID")]
        public int stateId;
        
        [VerticalGroup("Identity/Left")]
        [LabelText("状态名称")]
        public string stateName = "新状态";
        
        [VerticalGroup("Identity/Right")]
        [LabelText("优先级"), Range(0, 100)]
        public int priority = 50;
        
        [VerticalGroup("Identity/Right")]
        [LabelText("所属流水线")]
        public StatePipelineType pipelineType = StatePipelineType.Basic;
        
        [LabelText("状态描述"), TextArea(2, 3)]
        public string description = "";
        
        [HorizontalGroup("Lifecycle")]
        [LabelText("持续时间(秒)"), MinValue(0)]
        public float duration = 0f;
        
        [HorizontalGroup("Lifecycle")]
        [LabelText("后摇开始(归一化)"), Range(0, 1)]
        public float recoveryStartTime = 0.7f;
        
        [HorizontalGroup("Lifecycle")]
        [LabelText("后摇时长(秒)"), MinValue(0)]
        public float recoveryDuration = 0.3f;
    }
    
    /// <summary>
    /// 动画配置 - Clip与BlendTree
    /// </summary>
    [Serializable]
    public class StateAnimationConfig
    {
        [LabelText("动画模式")]
        public StateAnimationMode mode = StateAnimationMode.SingleClip;
        
        [LabelText("单个Clip"), ShowIf("@mode == StateAnimationMode.SingleClip")]
        [AssetSelector]
        public AnimationClip singleClip;
        
        [LabelText("Clip键"), ShowIf("@mode == StateAnimationMode.ClipFromTable")]
        public string clipKey = "";
        
        [HorizontalGroup("Playback")]
        [LabelText("播放速度"), Range(0.1f, 3f)]
        public float playbackSpeed = 1f;
        
        [HorizontalGroup("Playback")]
        [LabelText("循环播放")]
        public bool loopClip = false;
        
        [LabelText("BlendTree样本"), ShowIf("@mode == StateAnimationMode.BlendTree")]
        [ListDrawerSettings(ShowIndexLabels = true)]
        public List<BlendTreeSample> blendTreeSamples = new List<BlendTreeSample>();
        
        [HorizontalGroup("Advanced")]
        [LabelText("平滑时间"), Range(0f, 1f)]
        public float smoothTime = 0.1f;
        
        [HorizontalGroup("Advanced")]
        [LabelText("脏标记阈值"), Range(0.001f, 0.1f)]
        public float dirtyThreshold = 0.01f;
    }
    
    /// <summary>
    /// 参数配置 - 进入参数与监听参数
    /// </summary>
    [Serializable]
    public class StateParameterConfig
    {
        [LabelText("进入时Float参数")]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine)]
        public Dictionary<string, float> enterFloats = new Dictionary<string, float>();
        
        [LabelText("进入时Bool参数")]
        [DictionaryDrawerSettings(DisplayMode = DictionaryDisplayOptions.OneLine)]
        public Dictionary<string, bool> enterBools = new Dictionary<string, bool>();
        
        [LabelText("进入时触发Trigger")]
        public List<string> enterTriggers = new List<string>();
        
        [LabelText("监听参数变化")]
        public List<StateParameter> watchedParameters = new List<StateParameter>();
    }
    
    /// <summary>
    /// 过渡配置 - 过渡设置与自动转换
    /// </summary>
    [Serializable]
    public class StateTransitionConfig
    {
        [HorizontalGroup("Settings")]
        [LabelText("过渡时长(秒)"), MinValue(0)]
        public float transitionDuration = 0.3f;
        
        [HorizontalGroup("Settings")]
        [LabelText("过渡模式")]
        public TransitionMode transitionMode = TransitionMode.Blend;
        
        [LabelText("过渡曲线")]
        public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        
        [LabelText("自动转换规则")]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = false)]
        public List<StateTransition> autoTransitions = new List<StateTransition>();
    }
    
    /// <summary>
    /// 条件配置 - 进入/保持/退出条件
    /// </summary>
    [Serializable]
    public class StateConditionConfig
    {
        [LabelText("进入条件")]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = false)]
        [SerializeReference]
        public List<StateCondition> enterConditions = new List<StateCondition>();
        
        [LabelText("保持条件")]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = false)]
        [SerializeReference]
        public List<StateCondition> keepConditions = new List<StateCondition>();
        
        [LabelText("退出条件")]
        [ListDrawerSettings(ShowIndexLabels = true, ShowFoldout = false)]
        [SerializeReference]
        public List<StateCondition> exitConditions = new List<StateCondition>();
    }
    
    /// <summary>
    /// 高级配置 - 打断、退化、备忘
    /// </summary>
    [Serializable]
    public class StateAdvancedConfig
    {
        [ToggleLeft, LabelText("允许弱打断")]
        public bool allowWeakInterrupt = false;
        
        [LabelText("同路类型"), ShowIf("allowWeakInterrupt")]
        public SamePathType samePathType = SamePathType.None;
        
        [LabelText("退化目标ID"), ShowIf("allowWeakInterrupt")]
        public int degradeTargetId = -1;
        
        [ToggleLeft, LabelText("启用备忘优化")]
        public bool enableMemoization = true;
        
        [LabelText("备忘失效时间(秒)"), ShowIf("enableMemoization")]
        public float memoizationTimeout = 0.5f;
        
        [ToggleLeft, LabelText("调试日志")]
        public bool enableDebugLog = false;
        
        [LabelText("日志颜色"), ShowIf("enableDebugLog")]
        public Color debugLogColor = Color.cyan;
    }
    
    #endregion
    
    /// <summary>
    /// 动画状态数据信息 - State的享元数据配置
    /// 商业级状态数据SO，使用模块化设计减少碎片
    /// </summary>
    [ESCreatePath("数据信息", "动画状态数据信息")]
    public class StateAniDataInfo : SoDataInfo
    {
        #region 核心数据模块
        
        [TabGroup("核心", "基础配置")]
        [InlineProperty, HideLabel]
        public StateBasicConfig basicConfig = new StateBasicConfig();
        
        [TabGroup("核心", "动画配置")]
        [InlineProperty, HideLabel]
        public StateAnimationConfig animationConfig = new StateAnimationConfig();
        
        [TabGroup("核心", "参数配置")]
        [InlineProperty, HideLabel]
        public StateParameterConfig parameterConfig = new StateParameterConfig();
        
        #endregion
        
        #region 行为与代价
        
        [TabGroup("行为", "过渡配置")]
        [InlineProperty, HideLabel]
        public StateTransitionConfig transitionConfig = new StateTransitionConfig();
        
        [TabGroup("行为", "条件配置")]
        [InlineProperty, HideLabel]
        public StateConditionConfig conditionConfig = new StateConditionConfig();
        
        [TabGroup("行为", "代价配置")]
        [ToggleLeft, LabelText("忽略代价计算")]
        public bool ignoreInCostCalculation = false;
        
        [TabGroup("行为", "代价配置")]
        [InlineProperty, HideLabel, ShowIf("@!ignoreInCostCalculation")]
        public StateCostData costData = new StateCostData();
        
        #endregion
        
        #region 高级选项
        
        [TabGroup("高级", "高级配置")]
        [InlineProperty, HideLabel]
        public StateAdvancedConfig advancedConfig = new StateAdvancedConfig();
        
        #endregion
        
        #region 统一初始化接口
        
        /// <summary>
        /// 统一初始化入口 - 商业级初始化方法
        /// </summary>
        public void Initialize(int id, string name, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            // 基础配置
            basicConfig.stateId = id;
            basicConfig.stateName = name;
            basicConfig.pipelineType = pipeline;
            basicConfig.priority = 50;
            basicConfig.duration = 1f;
            basicConfig.recoveryStartTime = 0.7f;
            basicConfig.recoveryDuration = 0.3f;
            
            // 动画配置
            animationConfig.mode = StateAnimationMode.SingleClip;
            animationConfig.playbackSpeed = 1f;
            animationConfig.loopClip = false;
            animationConfig.smoothTime = 0.1f;
            animationConfig.dirtyThreshold = 0.01f;
            
            // 过渡配置
            transitionConfig.transitionDuration = 0.3f;
            transitionConfig.transitionMode = TransitionMode.Blend;
            transitionConfig.transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            
            // 代价配置
            ignoreInCostCalculation = false;
            if (costData == null)
                costData = new StateCostData();
            
            // 高级配置
            advancedConfig.enableMemoization = true;
            advancedConfig.memoizationTimeout = 0.5f;
            advancedConfig.enableDebugLog = false;
            
            Debug.Log($"<color=green>状态 [{name}] 初始化完成</color>");
        }
        
        /// <summary>
        /// 验证数据完整性
        /// </summary>
        public bool Validate(out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();
            
            // 基础验证
            if (string.IsNullOrEmpty(basicConfig.stateName))
                errors.Add("状态名称不能为空");
            
            if (basicConfig.duration < 0)
                errors.Add("持续时间不能为负数");
            
            // 动画验证
            if (animationConfig.mode == StateAnimationMode.SingleClip && animationConfig.singleClip == null)
                warnings.Add("未设置动画Clip");
            
            if (animationConfig.mode == StateAnimationMode.ClipFromTable && string.IsNullOrEmpty(animationConfig.clipKey))
                warnings.Add("未设置Clip键");
            
            if (animationConfig.mode == StateAnimationMode.BlendTree && 
                (animationConfig.blendTreeSamples == null || animationConfig.blendTreeSamples.Count == 0))
                errors.Add("启用了BlendTree但未配置样本");
            
            // 代价验证
            if (!ignoreInCostCalculation && costData != null)
            {
                if (costData.mainCostPart.EnterCostValue < 0 || costData.mainCostPart.EnterCostValue > 1)
                    errors.Add("主代价值必须在[0,1]范围内");
            }
            
            // 过渡验证
            if (transitionConfig.transitionDuration < 0)
                errors.Add("过渡时间不能为负数");
            
            return errors.Count == 0;
        }
        
        #endregion
        
        #region 编辑器工具
        
        [Button("初始化状态", ButtonSizes.Large)]
        [PropertySpace(10)]
        [GUIColor(0.3f, 0.8f, 1f)]
        private void EditorInitialize()
        {
            Initialize(basicConfig.stateId, $"新状态_{basicConfig.stateId}");
        }
        
        [Button("验证数据", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        private void EditorValidate()
        {
            bool isValid = Validate(out var errors, out var warnings);
            
            if (!isValid)
            {
                Debug.LogError($"[{basicConfig.stateName}] 数据验证失败：\n" + string.Join("\n", errors));
            }
            else if (warnings.Count > 0)
            {
                Debug.LogWarning($"[{basicConfig.stateName}] 数据验证警告：\n" + string.Join("\n", warnings));
            }
            else
            {
                Debug.Log($"<color=green>[{basicConfig.stateName}] 数据验证通过！</color>");
            }
        }
        
        #endregion
    }
    
    #region 辅助数据结构
    
    /// <summary>
    /// 动画模式
    /// </summary>
    public enum StateAnimationMode
    {
        [LabelText("单个Clip")]
        SingleClip,
        
        [LabelText("从Clip表获取")]
        ClipFromTable,
        
        [LabelText("使用BlendTree")]
        BlendTree
    }
    
    /// <summary>
    /// 过渡模式
    /// </summary>
    public enum TransitionMode
    {
        [LabelText("平滑混合")]
        Blend,
        
        [LabelText("硬切")]
        Cut,
        
        [LabelText("交叉淡化")]
        CrossFade
    }
    
    /// <summary>
    /// BlendTree样本
    /// </summary>
    [Serializable]
    public class BlendTreeSample
    {
        [HorizontalGroup("Main", Width = 0.6f)]
        [LabelText("Clip"), PreviewField(50, ObjectFieldAlignment.Left)]
        public AnimationClip clip;
        
        [VerticalGroup("Main/Settings")]
        [LabelText("阈值/位置")]
        public Vector2 position = Vector2.zero;
        
        [VerticalGroup("Main/Settings")]
        [LabelText("权重参数")]
        public StateParameter weightParameter;
        
        [VerticalGroup("Main/Settings")]
        [LabelText("时间缩放"), Range(0.1f, 3f)]
        public float timeScale = 1f;
    }
    
    #endregion
}

//ES已修正