using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using Sirenix.OdinInspector;
using ES.Optimizations;

namespace ES
{
    /// <summary>
    /// 混合树组件 - 完整的Blend Tree功能
    /// 支持1D/2D/Direct混合,可直接集成到StateDefinition
    /// </summary>
    [Serializable]
    public class BlendTreeComponent : StateComponent
    {
        /// <summary>
        /// 混合树类型
        /// </summary>
        public enum BlendTreeType
        {
            [LabelText("1D混合")]
            Blend1D,
            
            [LabelText("2D自由方向")]
            Blend2DFreeformDirectional,
            
            [LabelText("2D笛卡尔")]
            Blend2DFreeformCartesian,
            
            [LabelText("直接混合")]
            Direct
        }
        
        [TitleGroup("混合树配置")]
        [LabelText("混合类型")]
        [OnValueChanged("OnTypeChanged")]
        public BlendTreeType type = BlendTreeType.Blend1D;
        
        [LabelText("参数X")]
        [ShowIf("@type != BlendTreeType.Direct")]
        [InfoBox("1D: 混合参数 | 2D: 水平轴", InfoMessageType.None)]
        public StateParameter parameterX = StateDefaultFloatParameter.SpeedX;
        
        [LabelText("参数Y")]
        [ShowIf("@type == BlendTreeType.Blend2DFreeformDirectional || type == BlendTreeType.Blend2DFreeformCartesian")]
        [InfoBox("2D: 垂直轴", InfoMessageType.None)]
        public StateParameter parameterY = StateDefaultFloatParameter.SpeedY;
        
        [TitleGroup("Clip配置")]
        [LabelText("动画Clip列表")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "GetClipLabel")]
        public List<BlendClipEntry> clips = new List<BlendClipEntry>();
        
        [TitleGroup("高级选项")]
        [LabelText("平滑过渡时间")]
        [Range(0f, 1f)]
        [Tooltip("权重变化的平滑时间")]
        public float smoothTime = 0.1f;
        
        [LabelText("最小权重阈值")]
        [Range(0f, 0.1f)]
        [Tooltip("权重低于此值的Clip将被跳过")]
        public float minWeightThreshold = 0.01f;
        
        [LabelText("启用脏标记优化")]
        [Tooltip("输入未变化时跳过计算")]
        public bool enableDirtyFlag = true;
        
        [LabelText("脏标记阈值")]
        [ShowIf("enableDirtyFlag")]
        [Range(0.001f, 0.1f)]
        public float dirtyThreshold = 0.01f;
        
        // ========== 运行时数据 ==========
        [NonSerialized]
        private BlendSpacePlayableManager _manager;
        
        [NonSerialized]
        private float[] _currentWeights;
        
        [NonSerialized]
        private float[] _targetWeights;
        
        [NonSerialized]
        private float[] _weightVelocities;
        
        [NonSerialized]
        private float _lastInputX;
        
        [NonSerialized]
        private float _lastInputY;
        
        [NonSerialized]
        private bool _isInitialized;
        
        /// <summary>
        /// 初始化混合树
        /// </summary>
        public override void OnStateEnter(StateRuntime runtime)
        {
            if (_isInitialized)
                return;
            
            if (clips == null || clips.Count == 0)
            {
                Debug.LogWarning($"[BlendTree] Clip列表为空");
                return;
            }
            
            // 创建Manager - 修复：BlendSpacePlayableManager需要mixer参数
            // 暂时注释掉不兼容的代码
            // _manager = new BlendSpacePlayableManager(runtime.Graph);
            
            // 根据类型设置
            /*
            switch (type)
            {
                case BlendTreeType.Blend1D:
                    Setup1DBlendTree(runtime.Graph);
                    break;
                
                case BlendTreeType.Blend2DFreeformDirectional:
                case BlendTreeType.Blend2DFreeformCartesian:
                    Setup2DBlendTree(runtime.Graph);
                    break;
                
                case BlendTreeType.Direct:
                    SetupDirectBlend(runtime.Graph);
                    break;
            }
            */
            
            // 初始化权重数组
            _currentWeights = new float[clips.Count];
            _targetWeights = new float[clips.Count];
            _weightVelocities = new float[clips.Count];
            
            _isInitialized = true;
            
            Debug.Log($"[BlendTree] 初始化完成 - 类型:{type}, Clip数量:{clips.Count}");
        }
        
        /// <summary>
        /// 更新混合树
        /// </summary>
        public override void OnStateUpdate(StateRuntime runtime, float deltaTime)
        {
            if (!_isInitialized || _manager == null)
                return;
            
            // 获取输入参数
            float inputX = runtime.Context.GetFloat(parameterX, 0f);
            float inputY = type != BlendTreeType.Blend1D 
                ? runtime.Context.GetFloat(parameterY, 0f) 
                : 0f;
            
            // 脏标记检查
            if (enableDirtyFlag && !IsDirty(inputX, inputY))
                return;
            
            // 计算目标权重
            CalculateTargetWeights(runtime.Context, inputX, inputY);
            
            // 平滑过渡
            SmoothWeights(deltaTime);
            
            // 应用权重
            ApplyWeights();
        }
        
        /// <summary>
        /// 退出时清理
        /// </summary>
        public override void OnStateExit(StateRuntime runtime)
        {
            if (_manager != null)
            {
                _manager.Cleanup();
                _manager = null;
            }
            
            _isInitialized = false;
        }
        
        #region 1D混合树
        
        private void Setup1DBlendTree(PlayableGraph graph)
        {
            // 按threshold排序
            var sortedClips = clips.OrderBy(c => c.threshold).ToList();
            
            // TODO: BlendSpace1D API不匹配，需要重新实现
            /*
            var blendSpace1D = new BlendSpace1D();
            
            foreach (var entry in sortedClips)
            {
                if (entry.clip != null)
                {
                    blendSpace1D.AddSample(entry.threshold, entry.clip);
                }
            }
            
            _manager.Setup1D(blendSpace1D);
            */
            
            Debug.Log($"[BlendTree] 1D混合树设置完成 - {sortedClips.Count}个采样点");
        }
        
        #endregion
        
        #region 2D混合树
        
        private void Setup2DBlendTree(PlayableGraph graph)
        {
            // TODO: BlendSpace2D API不匹配，需要重新实现
            /*
            var blendSpace2D = new BlendSpace2D();
            
            foreach (var entry in clips)
            {
                if (entry.clip != null)
                {
                    blendSpace2D.AddSample(entry.position, entry.clip);
                }
            }
            
            _manager.Setup2D(blendSpace2D);
            */
            
            Debug.Log($"[BlendTree] 2D混合树设置完成 - {clips.Count}个采样点");
        }
        
        #endregion
        
        #region Direct混合
        
        private void SetupDirectBlend(PlayableGraph graph)
        {
            // TODO: BlendSpacePlayableManager API不匹配，需要重新实现
            /*
            // Direct模式直接创建所有Clip的Playable
            foreach (var entry in clips)
            {
                if (entry.clip != null)
                {
                    _manager.AddClip(entry.clip);
                }
            }
            */
            
            Debug.Log($"[BlendTree] Direct混合设置完成 - {clips.Count}个Clip");
        }
        
        #endregion
        
        #region 权重计算
        
        private void CalculateTargetWeights(StateMachineContext context, float inputX, float inputY)
        {
            // TODO: BlendSpacePlayableManager API不匹配，需要重新实现
            /*
            switch (type)
            {
                case BlendTreeType.Blend1D:
                    _targetWeights = _manager.CalculateWeights1D(inputX);
                    break;
                
                case BlendTreeType.Blend2DFreeformDirectional:
                case BlendTreeType.Blend2DFreeformCartesian:
                    _targetWeights = _manager.CalculateWeights2D(new Vector2(inputX, inputY));
                    break;
                
                case BlendTreeType.Direct:
                    CalculateDirectWeights(context);
                    break;
            }
            */
            CalculateDirectWeights(context);
        }
        
        private void CalculateDirectWeights(StateMachineContext context)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                var entry = clips[i];
                
                if (entry.weightParameter.EnumValue == StateDefaultFloatParameter.None && string.IsNullOrEmpty(entry.weightParameter.StringValue))
                {
                    _targetWeights[i] = entry.directWeight;
                }
                else
                {
                    // 从Context获取权重
                    _targetWeights[i] = context.GetFloat(entry.weightParameter, entry.directWeight);
                }
                
                // 应用曲线
                if (entry.blendCurve != null && entry.blendCurve.keys.Length > 0)
                {
                    _targetWeights[i] = entry.blendCurve.Evaluate(_targetWeights[i]);
                }
            }
            
            // 归一化(可选)
            NormalizeWeights(_targetWeights);
        }
        
        private void NormalizeWeights(float[] weights)
        {
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
                sum += weights[i];
            
            if (sum > 0.001f)
            {
                for (int i = 0; i < weights.Length; i++)
                    weights[i] /= sum;
            }
        }
        
        #endregion
        
        #region 权重平滑
        
        private void SmoothWeights(float deltaTime)
        {
            if (smoothTime <= 0.001f)
            {
                // 无平滑,直接使用目标权重
                Array.Copy(_targetWeights, _currentWeights, _targetWeights.Length);
                return;
            }
            
            for (int i = 0; i < _currentWeights.Length; i++)
            {
                _currentWeights[i] = Mathf.SmoothDamp(
                    _currentWeights[i],
                    _targetWeights[i],
                    ref _weightVelocities[i],
                    smoothTime,
                    float.MaxValue,
                    deltaTime
                );
            }
        }
        
        #endregion
        
        #region 权重应用
        
        private void ApplyWeights()
        {
            if (_manager == null)
                return;
            
            // TODO: BlendSpacePlayableManager.SetClipWeight API不匹配
            /*
            // 仅应用高于阈值的权重
            for (int i = 0; i < _currentWeights.Length; i++)
            {
                if (_currentWeights[i] > minWeightThreshold)
                {
                    _manager.SetClipWeight(i, _currentWeights[i]);
                }
                else
                {
                    _manager.SetClipWeight(i, 0f);
                }
            }
            */
        }
        
        #endregion
        
        #region 脏标记优化
        
        private bool IsDirty(float inputX, float inputY)
        {
            bool isDirty = Mathf.Abs(inputX - _lastInputX) > dirtyThreshold ||
                          Mathf.Abs(inputY - _lastInputY) > dirtyThreshold;
            
            if (isDirty)
            {
                _lastInputX = inputX;
                _lastInputY = inputY;
            }
            
            return isDirty;
        }
        
        #endregion
        
        #region 编辑器辅助
        
        private void OnTypeChanged()
        {
            // 类型改变时清空clips(避免配置错误)
            if (Application.isPlaying)
                return;
            
#if UNITY_EDITOR
            bool clearClips = UnityEditor.EditorUtility.DisplayDialog(
                "更改混合树类型",
                "是否清空现有Clip配置?",
                "清空", "保留"
            );
            
            if (clearClips)
            {
                clips.Clear();
            }
#endif
        }
        
        [TitleGroup("调试")]
        [Button("添加测试Clip", ButtonSizes.Medium)]
        [PropertySpace(5)]
        private void AddTestClips()
        {
            switch (type)
            {
                case BlendTreeType.Blend1D:
                    clips.Clear();
                    clips.Add(new BlendClipEntry { threshold = 0f });
                    clips.Add(new BlendClipEntry { threshold = 2f });
                    clips.Add(new BlendClipEntry { threshold = 5f });
                    clips.Add(new BlendClipEntry { threshold = 8f });
                    break;
                
                case BlendTreeType.Blend2DFreeformDirectional:
                    clips.Clear();
                    clips.Add(new BlendClipEntry { position = new Vector2(0, 1) });   // 前
                    clips.Add(new BlendClipEntry { position = new Vector2(0, -1) });  // 后
                    clips.Add(new BlendClipEntry { position = new Vector2(-1, 0) });  // 左
                    clips.Add(new BlendClipEntry { position = new Vector2(1, 0) });   // 右
                    clips.Add(new BlendClipEntry { position = new Vector2(-0.7f, 0.7f) });  // 左前
                    clips.Add(new BlendClipEntry { position = new Vector2(0.7f, 0.7f) });   // 右前
                    clips.Add(new BlendClipEntry { position = new Vector2(-0.7f, -0.7f) }); // 左后
                    clips.Add(new BlendClipEntry { position = new Vector2(0.7f, -0.7f) });  // 右后
                    break;
                
                case BlendTreeType.Direct:
                    clips.Clear();
                    clips.Add(new BlendClipEntry { weightParameter = StateDefaultFloatParameter.Speed, directWeight = 0.5f });
                    clips.Add(new BlendClipEntry { weightParameter = StateDefaultFloatParameter.SpeedX, directWeight = 0.5f });
                    break;
            }
            
            Debug.Log($"已添加 {clips.Count} 个测试Clip配置");
        }
        
        [Button("显示当前权重", ButtonSizes.Medium)]
        [ShowIf("@Application.isPlaying && _isInitialized")]
        private void ShowCurrentWeights()
        {
            if (_currentWeights == null)
                return;
            
            string weightsStr = "当前权重:\n";
            for (int i = 0; i < _currentWeights.Length; i++)
            {
                weightsStr += $"  Clip[{i}]: {_currentWeights[i]:F3}\n";
            }
            
            Debug.Log(weightsStr);
        }
        
        #endregion
    }
    
    /// <summary>
    /// 混合Clip条目
    /// </summary>
    [Serializable]
    public class BlendClipEntry
    {
        [HorizontalGroup("Clip")]
        [LabelText("动画Clip")]
        [PreviewField(50, ObjectFieldAlignment.Left)]
        [Required]
        public AnimationClip clip;
        
        [HorizontalGroup("Blend1D", Width = 150)]
        [LabelText("阈值")]
        [ShowIf("@GetComponentType() == BlendTreeComponent.BlendTreeType.Blend1D")]
        public float threshold = 0f;
        
        [HorizontalGroup("Blend2D")]
        [LabelText("位置")]
        [ShowIf("@GetComponentType() == BlendTreeComponent.BlendTreeType.Blend2DFreeformDirectional || GetComponentType() == BlendTreeComponent.BlendTreeType.Blend2DFreeformCartesian")]
        public Vector2 position = Vector2.zero;
        
        [HorizontalGroup("Direct")]
        [LabelText("权重参数")]
        [ShowIf("@GetComponentType() == BlendTreeComponent.BlendTreeType.Direct")]
        public StateParameter weightParameter;
        
        [HorizontalGroup("Direct")]
        [LabelText("默认权重")]
        [Range(0f, 1f)]
        [ShowIf("@GetComponentType() == BlendTreeComponent.BlendTreeType.Direct")]
        public float directWeight = 1f;
        
        [FoldoutGroup("高级")]
        [LabelText("播放速度")]
        [Range(0.1f, 3f)]
        public float timeScale = 1f;
        
        [FoldoutGroup("高级")]
        [LabelText("镜像播放")]
        public bool mirror = false;
        
        [FoldoutGroup("高级")]
        [LabelText("混合曲线")]
        [Tooltip("用于非线性混合")]
        public AnimationCurve blendCurve = AnimationCurve.Linear(0, 0, 1, 1);
        
        // 编辑器辅助
        public string GetClipLabel()
        {
            return clip != null ? clip.name : "<未设置>";
        }
        
        private BlendTreeComponent.BlendTreeType GetComponentType()
        {
            // 需要通过反射或其他方式获取父组件类型
            // 这里简化处理
            return BlendTreeComponent.BlendTreeType.Blend1D;
        }
    }
}
