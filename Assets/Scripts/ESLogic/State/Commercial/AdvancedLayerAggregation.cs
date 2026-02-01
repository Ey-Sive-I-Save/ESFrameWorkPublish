using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Sirenix.OdinInspector;
using System.Linq;

namespace ES.Commercial
{



































































































































































































































































































































































































































































































































































































































    /// </summary>
    public enum LayerBlendMode
    {
        Override,   // 覆盖模式
        Additive    // 叠加模式
    }
    
    /// <summary>
    /// 扩展的Pipeline类型 - 支持更复杂的分层
    /// </summary>
    public enum ExtendedPipelineType
    {
        Basic = 0,          // 基础层 (全身动画)
        Main = 1,           // 主要层 (战斗/移动)
        Buff = 2,           // Buff层 (增益/特效)
        
        // === 新增商业级Pipeline ===
        UpperBody = 3,      // 上半身层 (独立上身动作)
        LowerBody = 4,      // 下半身层 (独立下身动作)
        Additive = 5,       // 叠加层 (瞄准/后坐力)
        Override = 6,       // 覆盖层 (动作打断)
        IK = 7,             // IK层 (手足IK)
        Facial = 8,         // 面部层 (表情/口型)
        Physics = 9         // 物理层 (布料/头发)
    }
    
    /// <summary>
    /// AvatarMask包装器
    /// </summary>
    [Serializable]
    public class MaskableLayer
    {
        [LabelText("Pipeline类型")]
        public ExtendedPipelineType pipelineType;
        
        [LabelText("AvatarMask")]
        [Tooltip("定义哪些骨骼受此层影响")]
        public AvatarMask avatarMask;
        
        [LabelText("混合模式")]
        public LayerBlendMode blendMode = LayerBlendMode.Override;
        
        [LabelText("默认权重")]
        [Range(0f, 1f)]
        public float defaultWeight = 1f;
        
        [LabelText("优先级")]
        [Tooltip("数值越大越优先,相同Pipeline内部排序")]
        public int priority = 0;
        
        [NonSerialized]
        private AnimationLayerMixerPlayable _mixer;
        
        [NonSerialized]
        private List<AnimationClipPlayable> _activeClips = new List<AnimationClipPlayable>();
        
        public void Setup(PlayableGraph graph, AnimationLayerMixerPlayable rootMixer, int layerIndex)
        {
            _mixer = AnimationLayerMixerPlayable.Create(graph, 1);
            
            if (avatarMask != null)
            {
                _mixer.SetLayerMaskFromAvatarMask((uint)0, avatarMask);
            }
            
            graph.Connect(_mixer, 0, rootMixer, layerIndex);
            rootMixer.SetInputWeight(layerIndex, defaultWeight);
            
            // 设置混合模式
            rootMixer.SetLayerAdditive((uint)layerIndex, blendMode == LayerBlendMode.Additive);
        }
        
        public void AddClip(PlayableGraph graph, AnimationClip clip, float weight)
        {
            var playable = AnimationClipPlayable.Create(graph, clip);
            
            int inputIndex = _mixer.GetInputCount();
            _mixer.SetInputCount(inputIndex + 1);
            
            graph.Connect(playable, 0, _mixer, inputIndex);
            _mixer.SetInputWeight(inputIndex, weight);
            
            _activeClips.Add(playable);
        }
        
        public void SetLayerWeight(AnimationLayerMixerPlayable rootMixer, int layerIndex, float weight)
        {
            rootMixer.SetInputWeight(layerIndex, weight);
        }
    }
    
    /// <summary>
    /// 复杂层级聚合系统
    /// </summary>
    [CreateAssetMenu(fileName = "NewAdvancedStateMachine", menuName = "ES/Animation/Advanced State Machine")]
    public class AdvancedStateMachineData : ScriptableObject
    {
        [TitleGroup("多层级配置")]
        [LabelText("层级定义")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "pipelineType")]
        public List<MaskableLayer> layers = new List<MaskableLayer>();
        
        [TitleGroup("叠加姿态")]
        [LabelText("叠加姿态库")]
        [InfoBox("用于瞄准偏移、受击反馈等叠加动画")]
        public List<AdditiveLayer> additiveLayers = new List<AdditiveLayer>();
        
        [TitleGroup("复杂聚合")]
        [LabelText("启用同时播放")]
        [Tooltip("允许多个Pipeline同时激活(如飞行+游泳+瞄准+交互)")]
        public bool enableSimultaneousPlayback = true;
        
        [LabelText("最大同时层数")]
        [ShowIf("enableSimultaneousPlayback")]
        [Range(1, 10)]
        public int maxSimultaneousLayers = 4;
        
        [Button("创建默认层级", ButtonSizes.Large)]
        [PropertySpace(10)]
        private void CreateDefaultLayers()
        {
            layers.Clear();
            
            // 基础全身层
            layers.Add(new MaskableLayer
            {
                pipelineType = ExtendedPipelineType.Basic,
                avatarMask = null, // 全身
                blendMode = LayerBlendMode.Override,
                defaultWeight = 1f,
                priority = 0
            });
            
            // 上半身层 (射击/攻击)
            layers.Add(new MaskableLayer
            {
                pipelineType = ExtendedPipelineType.UpperBody,
                avatarMask = null, // 需要设置上半身Mask
                blendMode = LayerBlendMode.Override,
                defaultWeight = 0f,
                priority = 5
            });
            
            // 下半身层 (独立行走)
            layers.Add(new MaskableLayer
            {
                pipelineType = ExtendedPipelineType.LowerBody,
                avatarMask = null, // 需要设置下半身Mask
                blendMode = LayerBlendMode.Override,
                defaultWeight = 0f,
                priority = 5
            });
            
            // 叠加层 (瞄准偏移)
            layers.Add(new MaskableLayer
            {
                pipelineType = ExtendedPipelineType.Additive,
                avatarMask = null,
                blendMode = LayerBlendMode.Additive,
                defaultWeight = 0f,
                priority = 10
            });
            
            // IK层
            layers.Add(new MaskableLayer
            {
                pipelineType = ExtendedPipelineType.IK,
                avatarMask = null,
                blendMode = LayerBlendMode.Override,
                defaultWeight = 0f,
                priority = 15
            });
            
            // 面部层
            layers.Add(new MaskableLayer
            {
                pipelineType = ExtendedPipelineType.Facial,
                avatarMask = null, // 面部Mask
                blendMode = LayerBlendMode.Override,
                defaultWeight = 1f,
                priority = 20
            });
            
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"已创建 {layers.Count} 个默认层级");
#endif
        }
    }
    
    /// <summary>
    /// 叠加动画层
    /// </summary>
    [Serializable]
    public class AdditiveLayer
    {
        [LabelText("名称")]
        public string layerName;
        
        [LabelText("叠加Clip")]
        public AnimationClip additiveClip;
        
        [LabelText("参考姿态")]
        [Tooltip("用于计算差值的参考姿态")]
        public AnimationClip referencePose;
        
        [LabelText("权重参数")]
        public string weightParameter = "AdditiveWeight";
        
        [LabelText("2D混合")]
        [Tooltip("用于瞄准偏移等2D混合")]
        public bool use2DBlend = false;
        
        [ShowIf("use2DBlend")]
        [LabelText("水平参数")]
        public string horizontalParameter = "AimHorizontal";
        
        [ShowIf("use2DBlend")]
        [LabelText("垂直参数")]
        public string verticalParameter = "AimVertical";
        
        [ShowIf("use2DBlend")]
        [LabelText("Clip网格")]
        [TableList(AlwaysExpanded = true)]
        public List<BlendClip2D> clipGrid = new List<BlendClip2D>();
        
        [Serializable]
        public class BlendClip2D
        {
            [LabelText("水平值")]
            public float horizontal;
            
            [LabelText("垂直值")]
            public float vertical;
            
            [LabelText("Clip")]
            public AnimationClip clip;
        }
    }
    
    /// <summary>
    /// 高级状态机控制器
    /// </summary>
    public class AdvancedStateMachineController : MonoBehaviour
    {
        [LabelText("状态机数据")]
        [Required]
        public AdvancedStateMachineData stateMachineData;
        
        [LabelText("Animator")]
        [Required]
        public Animator animator;
        
        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _rootMixer;
        private List<MaskableLayer> _activeLayers = new List<MaskableLayer>();
        
        private void Awake()
        {
            Initialize();
        }
        
        private void Initialize()
        {
            if (stateMachineData == null || animator == null)
            {
                Debug.LogError("状态机数据或Animator未设置");
                return;
            }
            
            // 创建PlayableGraph
            _graph = PlayableGraph.Create($"AdvancedStateMachine_{gameObject.name}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            
            // 创建根Mixer
            _rootMixer = AnimationLayerMixerPlayable.Create(_graph, stateMachineData.layers.Count);
            
            // 创建输出
            var output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            output.SetSourcePlayable(_rootMixer);
            
            // 初始化所有层
            for (int i = 0; i < stateMachineData.layers.Count; i++)
            {
                var layer = stateMachineData.layers[i];
                layer.Setup(_graph, _rootMixer, i);
            }
            
            _graph.Play();
            
            Debug.Log($"✓ 高级状态机初始化完成 - {stateMachineData.layers.Count} 个层级");
        }
        
        /// <summary>
        /// 激活多个Pipeline
        /// </summary>
        public void ActivatePipelines(params ExtendedPipelineType[] pipelineTypes)
        {
            if (!stateMachineData.enableSimultaneousPlayback)
            {
                Debug.LogWarning("同时播放未启用");
                return;
            }
            
            if (pipelineTypes.Length > stateMachineData.maxSimultaneousLayers)
            {
                Debug.LogWarning($"超出最大同时层数限制: {stateMachineData.maxSimultaneousLayers}");
                return;
            }
            
            // 按优先级排序
            var sortedTypes = pipelineTypes
                .Select(type => stateMachineData.layers.FirstOrDefault(l => l.pipelineType == type))
                .Where(l => l != null)
                .OrderByDescending(l => l.priority)
                .ToList();
            
            foreach (var layer in sortedTypes)
            {
                int index = stateMachineData.layers.IndexOf(layer);
                if (index >= 0)
                {
                    layer.SetLayerWeight(_rootMixer, index, layer.defaultWeight);
                }
            }
            
            Debug.Log($"✓ 激活了 {sortedTypes.Count} 个Pipeline");
        }
        
        /// <summary>
        /// 设置层级权重
        /// </summary>
        public void SetLayerWeight(ExtendedPipelineType pipelineType, float weight)
        {
            var layer = stateMachineData.layers.FirstOrDefault(l => l.pipelineType == pipelineType);
            if (layer != null)
            {
                int index = stateMachineData.layers.IndexOf(layer);
                layer.SetLayerWeight(_rootMixer, index, weight);
            }
        }
        
        /// <summary>
        /// 示例:飞行+游泳+瞄准+交互组合
        /// </summary>
        [Button("测试复杂聚合", ButtonSizes.Large)]
        private void TestComplexAggregation()
        {
            // 基础飞行动画(全身)
            SetLayerWeight(ExtendedPipelineType.Basic, 1f);
            
            // 上半身瞄准(覆盖上半身)
            SetLayerWeight(ExtendedPipelineType.UpperBody, 0.8f);
            
            // 叠加后坐力(Additive)
            SetLayerWeight(ExtendedPipelineType.Additive, 0.5f);
            
            // IK握持武器
            SetLayerWeight(ExtendedPipelineType.IK, 1f);
            
            Debug.Log("✓ 已激活复杂聚合: 飞行+瞄准+后坐力+IK");
        }
        
        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }
}
