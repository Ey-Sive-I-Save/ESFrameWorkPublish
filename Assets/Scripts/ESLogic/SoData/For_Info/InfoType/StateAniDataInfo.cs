
using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ES
{
    #region 核心数据模块定义
    
      
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
        [Tooltip("Loop动画：结束后自动循环 | 非Loop动画：播放一次后进入释放阶段")]
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
        
        // 运行时预计算数据（缓存，避免GC）
        [NonSerialized] private float _cachedClipLength;
        [NonSerialized] private int _cachedClipFrameCount;
        [NonSerialized] private bool _isInitialized;
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault()
        {
            mode = StateAnimationMode.SingleClip;
            singleClip = null;
            clipKey = string.Empty;
            playbackSpeed = 1f;
            loopClip = false;
            smoothTime = 0.1f;
            dirtyThreshold = 0.01f;
            if (blendTreeSamples == null)
                blendTreeSamples = new List<BlendTreeSample>();
            else
                blendTreeSamples.Clear();
        }
        
        /// <summary>
        /// 初始化预计算数据（运行时调用，避免每帧计算）
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            // 预计算动画长度（避免运行时频繁访问clip.length）
            if (mode == StateAnimationMode.SingleClip && singleClip != null)
            {
                _cachedClipLength = singleClip.length;
                _cachedClipFrameCount = Mathf.RoundToInt(singleClip.length * singleClip.frameRate);
            }
            
            _isInitialized = true;
        }
        
        public float GetClipLength() => _cachedClipLength;
        public int GetClipFrameCount() => _cachedClipFrameCount;
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
        
        // 运行时预计算数据（缓存参数名，避免装箱和字符串分配）
        [NonSerialized] private string[] _cachedFloatKeys;
        [NonSerialized] private float[] _cachedFloatValues;
        [NonSerialized] private string[] _cachedBoolKeys;
        [NonSerialized] private bool[] _cachedBoolValues;
        [NonSerialized] private bool _isInitialized;
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault()
        {
            if (enterFloats == null)
                enterFloats = new Dictionary<string, float>();
            else
                enterFloats.Clear();
                
            if (enterBools == null)
                enterBools = new Dictionary<string, bool>();
            else
                enterBools.Clear();
                
            if (enterTriggers == null)
                enterTriggers = new List<string>();
            else
                enterTriggers.Clear();
                
            if (watchedParameters == null)
                watchedParameters = new List<StateParameter>();
            else
                watchedParameters.Clear();
        }
        
        /// <summary>
        /// 初始化预计算数据（运行时调用，避免Dictionary迭代GC）
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            // 预缓存Float参数（避免运行时Dictionary查找和装箱）
            int floatCount = enterFloats.Count;
            _cachedFloatKeys = new string[floatCount];
            _cachedFloatValues = new float[floatCount];
            int index = 0;
            foreach (var kvp in enterFloats)
            {
                _cachedFloatKeys[index] = kvp.Key;
                _cachedFloatValues[index] = kvp.Value;
                index++;
            }
            
            // 预缓存Bool参数
            int boolCount = enterBools.Count;
            _cachedBoolKeys = new string[boolCount];
            _cachedBoolValues = new bool[boolCount];
            index = 0;
            foreach (var kvp in enterBools)
            {
                _cachedBoolKeys[index] = kvp.Key;
                _cachedBoolValues[index] = kvp.Value;
                index++;
            }
            
            _isInitialized = true;
        }
        
        public void ApplyEnterParameters(StateContext context)
        {
            // 零GC应用参数
            for (int i = 0; i < _cachedFloatKeys.Length; i++)
                context.SetFloat(_cachedFloatKeys[i], _cachedFloatValues[i]);
            for (int i = 0; i < _cachedBoolKeys.Length; i++)
                context.SetBool(_cachedBoolKeys[i], _cachedBoolValues[i]);
        }
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
        
        // 运行时预计算数据（缓存曲线采样，避免每帧Evaluate）
        [NonSerialized] private float[] _cachedCurveSamples;
        [NonSerialized] private const int CURVE_SAMPLE_COUNT = 32;
        [NonSerialized] private bool _isInitialized;
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault()
        {
            transitionDuration = 0.3f;
            transitionMode = TransitionMode.Blend;
            transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            if (autoTransitions == null)
                autoTransitions = new List<StateTransition>();
            else
                autoTransitions.Clear();
        }
        
        /// <summary>
        /// 初始化预计算数据（运行时调用，避免每帧曲线采样）
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            // 预采样过渡曲线（避免运行时Evaluate开销）
            _cachedCurveSamples = new float[CURVE_SAMPLE_COUNT];
            for (int i = 0; i < CURVE_SAMPLE_COUNT; i++)
            {
                float t = i / (float)(CURVE_SAMPLE_COUNT - 1);
                _cachedCurveSamples[i] = transitionCurve.Evaluate(t);
            }
            
            _isInitialized = true;
        }
        
        public float SampleTransitionCurve(float normalizedTime)
        {
            if (_cachedCurveSamples == null) return normalizedTime;
            
            float index = normalizedTime * (CURVE_SAMPLE_COUNT - 1);
            int i0 = Mathf.FloorToInt(index);
            int i1 = Mathf.Min(i0 + 1, CURVE_SAMPLE_COUNT - 1);
            float t = index - i0;
            return Mathf.Lerp(_cachedCurveSamples[i0], _cachedCurveSamples[i1], t);
        }
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
        
        // 运行时预计算数据（缓存条件数量，避免Count访问）
        [NonSerialized] private int _enterConditionCount;
        [NonSerialized] private int _keepConditionCount;
        [NonSerialized] private int _exitConditionCount;
        [NonSerialized] private bool _isInitialized;
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault()
        {
            if (enterConditions == null)
                enterConditions = new List<StateCondition>();
            else
                enterConditions.Clear();
                
            if (keepConditions == null)
                keepConditions = new List<StateCondition>();
            else
                keepConditions.Clear();
                
            if (exitConditions == null)
                exitConditions = new List<StateCondition>();
            else
                exitConditions.Clear();
        }
        
        /// <summary>
        /// 初始化预计算数据（运行时调用）
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            _enterConditionCount = enterConditions?.Count ?? 0;
            _keepConditionCount = keepConditions?.Count ?? 0;
            _exitConditionCount = exitConditions?.Count ?? 0;
            
            _isInitialized = true;
        }
        
        public bool HasEnterConditions() => _enterConditionCount > 0;
        public bool HasKeepConditions() => _keepConditionCount > 0;
        public bool HasExitConditions() => _exitConditionCount > 0;
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
        
        // 运行时预计算数据（缓存HTML颜色字符串，避免每帧ToHtmlStringRGB）
        [NonSerialized] private string _cachedLogColorHtml;
        [NonSerialized] private bool _isInitialized;
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefault()
        {
            allowWeakInterrupt = false;
            samePathType = SamePathType.None;
            degradeTargetId = -1;
            enableMemoization = true;
            memoizationTimeout = 0.5f;
            enableDebugLog = false;
            debugLogColor = Color.cyan;
        }
        
        /// <summary>
        /// 初始化预计算数据（运行时调用）
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            
            // 预计算HTML颜色字符串（避免运行时ColorUtility调用）
            if (enableDebugLog)
            {
                _cachedLogColorHtml = ColorUtility.ToHtmlStringRGB(debugLogColor);
            }
            
            _isInitialized = true;
        }
        
        public string GetLogColorHtml() => _cachedLogColorHtml;
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
        
        [HideLabel,InlineProperty]
        public StateSharedData sharedData = new StateSharedData();
        

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
        
        [TabGroup("行为", "冲突与合并(代价)")]
        [ToggleLeft, LabelText("忽略代价计算")]
        public bool ignoreInCostCalculation = false;
        
        [TabGroup("行为", "冲突与合并(代价)")]
        [InlineProperty, HideLabel, ShowIf("@!ignoreInCostCalculation")]
        public StateCostData costData = new StateCostData();
        
        #endregion
        
        #region 高级选项
        
        [TabGroup("高级", "高级配置")]
        [InlineProperty, HideLabel]
        public StateAdvancedConfig advancedConfig = new StateAdvancedConfig();
        
        #endregion
        
        #region 数据管理接口
        
        /// <summary>
        /// 重置为默认值 - 编辑器使用
        /// </summary>
        public void ResetToDefault(int id, string name, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            // 基础配置
            basicConfig.stateId = id;
            basicConfig.stateName = name;
            basicConfig.pipelineType = pipeline;
            basicConfig.priority = 50;
            basicConfig.durationMode = StateDurationMode.UntilAnimationEnd;
            basicConfig.timedDuration = 1f;
            if (basicConfig.phaseConfig == null)
                basicConfig.phaseConfig = new StatePhaseConfig();
            basicConfig.phaseConfig.returnStartTime = 0.7f;
            basicConfig.phaseConfig.releaseStartTime = 0.9f;
            basicConfig.phaseConfig.returnCostFraction = 0.5f;
            
            // 动画配置 - 重置
            if (animationConfig == null)
                animationConfig = new StateAnimationConfig();
            animationConfig.ResetToDefault();
            
            // 参数配置 - 重置
            if (parameterConfig == null)
                parameterConfig = new StateParameterConfig();
            parameterConfig.ResetToDefault();
            
            // 过渡配置 - 重置
            if (transitionConfig == null)
                transitionConfig = new StateTransitionConfig();
            transitionConfig.ResetToDefault();
            
            // 条件配置 - 重置
            if (conditionConfig == null)
                conditionConfig = new StateConditionConfig();
            conditionConfig.ResetToDefault();
            
            // 代价配置
            ignoreInCostCalculation = false;
            if (costData == null)
                costData = new StateCostData();
            
            // 高级配置 - 重置
            if (advancedConfig == null)
                advancedConfig = new StateAdvancedConfig();
            advancedConfig.ResetToDefault();
            
            Debug.Log($"状态 [{name}] 已重置为默认值");
        }
        
        /// <summary>
        /// 初始化预计算数据 - 运行时使用（避免每帧计算）
        /// </summary>
        public void InitializeRuntimeCache()
        {
            // 动画配置预计算
            animationConfig?.Initialize();
            
            // 参数配置预计算
            parameterConfig?.Initialize();
            
            // 过渡配置预计算
            transitionConfig?.Initialize();
            
            // 条件配置预计算
            conditionConfig?.Initialize();
            
            // 高级配置预计算
            advancedConfig?.Initialize();
        }
        
        #endregion
        
        #region 数据验证
        
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
            
            if (basicConfig.durationMode == StateDurationMode.Timed && basicConfig.timedDuration < 0)
                errors.Add("定时持续时间不能为负数");
            
            if (basicConfig.phaseConfig != null)
            {
                if (basicConfig.phaseConfig.returnStartTime > basicConfig.phaseConfig.releaseStartTime)
                    errors.Add("返还阶段开始时间不能晚于释放阶段开始时间");
            }
            
            // 动画验证
            if (animationConfig.mode == StateAnimationMode.SingleClip && animationConfig.singleClip == null)
                warnings.Add("未设置动画Clip");
            
            if (animationConfig.mode == StateAnimationMode.ClipFromTable && string.IsNullOrEmpty(animationConfig.clipKey))
                warnings.Add("未设置Clip键");
            
            if (animationConfig.mode == StateAnimationMode.BlendTree && 
                (animationConfig.blendTreeSamples == null || animationConfig.blendTreeSamples.Count == 0))
                errors.Add("启用了BlendTree但未配置样本");
            
         
            
            // 过渡验证
            if (transitionConfig.transitionDuration < 0)
                errors.Add("过渡时间不能为负数");
            
            return errors.Count == 0;
        }
        
        #endregion
        
        #region 编辑器工具
        
        [Button("重置状态数据", ButtonSizes.Large)]
        [PropertySpace(10)]
        [GUIColor(0.3f, 0.8f, 1f)]
        private void EditorReset()
        {
            ResetToDefault(basicConfig.stateId, $"新状态_{basicConfig.stateId}");
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

        internal void Initialize()
        {
           
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