
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
        
        public void ApplyEnterParameters(StateMachineContext context)
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
    /// 高级配置 - 打断、退化、备忘
    /// </summary>
    [Serializable]
    public class StateAdvancedConfig
    {
        [ToggleLeft, LabelText("允许弱打断")]
        public bool allowWeakInterrupt = false;
        
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
        

        #endregion
        
        #region 数据管理接口

        internal void Initialize()
        {
            InitializeRuntime();
        }

        /// <summary>
        /// 运行时初始化 - 递归初始化所有子成员（统一入口）
        /// </summary>
        public void InitializeRuntime()
        {
            // 递归初始化StateSharedData（会递归初始化所有子成员）
            sharedData?.InitializeRuntime();
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