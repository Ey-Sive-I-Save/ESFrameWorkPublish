using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES.Optimizations
{
    /// <summary>
    /// 变速Clip控制器 - 支持速度匹配和自适应
    /// </summary>
    [Serializable]
    public class SpeedControlledClip
    {
        public AnimationClip clip;
        
        [Header("变速配置")]
        public bool enableSpeedControl = true;
        public string speedParameterName = "PlaybackSpeed";
        public float minSpeed = 0.5f;
        public float maxSpeed = 2.0f;
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);
        
        [Header("移动速度匹配")]
        [Tooltip("自动根据角色移动速度调整播放速度")]
        public bool matchLocomotionSpeed = false;
        public float referenceSpeed = 5.0f;
        public string locomotionSpeedParam = "MoveSpeed";
        
        [NonSerialized]
        private AnimationClipPlayable _playable;
        
        public void Setup(PlayableGraph graph)
        {
            _playable = AnimationClipPlayable.Create(graph, clip);
        }
        
        public void UpdateSpeed(StateMachineContext context, float normalizedTime)
        {
            if (!_playable.IsValid() || !enableSpeedControl)
                return;
            
            float speed = CalculateSpeed(context, normalizedTime);
            _playable.SetSpeed(speed);
        }
        
        private float CalculateSpeed(StateMachineContext context, float normalizedTime)
        {
            float speed = 1f;
            
            if (matchLocomotionSpeed)
            {
                float moveSpeed = context.GetFloat(locomotionSpeedParam, 0f);
                speed = referenceSpeed > 0 ? moveSpeed / referenceSpeed : 1f;
            }
            else
            {
                speed = context.GetFloat(speedParameterName, 1f);
            }
            
            speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
            
            // 应用曲线调制
            float curveModifier = speedCurve.Evaluate(normalizedTime);
            return speed * curveModifier;
        }
        
        public AnimationClipPlayable GetPlayable() => _playable;
        
        public void Cleanup()
        {
            if (_playable.IsValid())
                _playable.Destroy();
        }
    }
    
    /// <summary>
    /// 智能过渡混合器 - 自适应过渡时间
    /// </summary>
    public class SmartTransitionBlender
    {
        private struct ClipTransition
        {
            public AnimationClipPlayable playable;
            public float currentWeight;
            public float targetWeight;
            public float blendSpeed;
            public int mixerInputIndex;
        }
        
        private List<ClipTransition> _transitions;
        private AnimationMixerPlayable _mixer;
        private PlayableGraph _graph;
        
        public SmartTransitionBlender(PlayableGraph graph, AnimationMixerPlayable mixer)
        {
            _graph = graph;
            _mixer = mixer;
            _transitions = new List<ClipTransition>(4);
        }
        
        /// <summary>
        /// 自适应过渡时长 - 根据动画特征调整
        /// </summary>
        public float CalculateAdaptiveDuration(AnimationClip from, AnimationClip to)
        {
            if (from == null || to == null)
                return 0.3f;
            
            float baseDuration = 0.3f;
            
            // 1. 长度相似性
            float lengthDiff = Mathf.Abs(from.length - to.length);
            float maxLength = Mathf.Max(from.length, to.length);
            float lengthRatio = lengthDiff / maxLength;
            
            if (lengthRatio < 0.1f)
                baseDuration *= 0.7f;  // 长度相似，快速过渡
            else if (lengthRatio > 0.5f)
                baseDuration *= 1.3f;  // 长度差异大，慢速过渡
            
            // 2. 循环状态
            if (from.isLooping && to.isLooping)
                baseDuration *= 0.8f;  // 两个循环动画，可以更快
            else if (!from.isLooping && !to.isLooping)
                baseDuration *= 1.2f;  // 两个单次动画，需要更平滑
            
            // 3. 动画类型匹配 (通过名称推断)
            if (AreSimilarAnimations(from.name, to.name))
                baseDuration *= 0.6f;  // 相似动画(如Walk->Run)
            
            return Mathf.Clamp(baseDuration, 0.1f, 1.0f);
        }
        
        private bool AreSimilarAnimations(string name1, string name2)
        {
            string[] similarGroups = new[]
            {
                "idle,walk,run,sprint",
                "attack,combo,hit",
                "jump,fall,land"
            };
            
            name1 = name1.ToLower();
            name2 = name2.ToLower();
            
            foreach (var group in similarGroups)
            {
                var keywords = group.Split(',');
                bool has1 = false, has2 = false;
                
                foreach (var keyword in keywords)
                {
                    if (name1.Contains(keyword)) has1 = true;
                    if (name2.Contains(keyword)) has2 = true;
                }
                
                if (has1 && has2) return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 开始过渡到新Clip
        /// </summary>
        public void StartTransition(AnimationClip targetClip, float duration)
        {
            if (targetClip == null) return;
            
            // 检查是否已存在
            for (int i = 0; i < _transitions.Count; i++)
            {
                var t = _transitions[i];
                if (t.playable.GetAnimationClip() == targetClip)
                {
                    t.targetWeight = 1f;
                    t.blendSpeed = 1f / duration;
                    _transitions[i] = t;
                    return;
                }
            }
            
            // 创建新的Playable
            var playable = AnimationClipPlayable.Create(_graph, targetClip);
            int inputIndex = _mixer.GetInputCount();
            _mixer.SetInputCount(inputIndex + 1);
            _graph.Connect(playable, 0, _mixer, inputIndex);
            _mixer.SetInputWeight(inputIndex, 0f);
            
            _transitions.Add(new ClipTransition
            {
                playable = playable,
                currentWeight = 0f,
                targetWeight = 1f,
                blendSpeed = 1f / duration,
                mixerInputIndex = inputIndex
            });
            
            // 所有其他Clip淡出
            for (int i = 0; i < _transitions.Count - 1; i++)
            {
                var t = _transitions[i];
                t.targetWeight = 0f;
                _transitions[i] = t;
            }
        }
        
        /// <summary>
        /// 每帧更新混合权重
        /// </summary>
        public void Update(float deltaTime)
        {
            for (int i = _transitions.Count - 1; i >= 0; i--)
            {
                var t = _transitions[i];
                
                // 平滑插值
                float delta = t.blendSpeed * deltaTime;
                if (t.currentWeight < t.targetWeight)
                {
                    t.currentWeight = Mathf.Min(t.currentWeight + delta, t.targetWeight);
                }
                else if (t.currentWeight > t.targetWeight)
                {
                    t.currentWeight = Mathf.Max(t.currentWeight - delta, t.targetWeight);
                }
                
                // 应用权重
                if (t.playable.IsValid())
                {
                    _mixer.SetInputWeight(t.mixerInputIndex, t.currentWeight);
                }
                
                // 清理完全淡出的Clip
                if (t.currentWeight <= 0.001f && t.targetWeight == 0f)
                {
                    if (t.playable.IsValid())
                    {
                        _graph.Disconnect(_mixer, t.mixerInputIndex);
                        t.playable.Destroy();
                    }
                    _transitions.RemoveAt(i);
                }
                else
                {
                    _transitions[i] = t;
                }
            }
        }
        
        public void Cleanup()
        {
            foreach (var t in _transitions)
            {
                if (t.playable.IsValid())
                    t.playable.Destroy();
            }
            _transitions.Clear();
        }
    }
    
    /// <summary>
    /// 动态时间拉伸 - 同步动画到特定时长
    /// </summary>
    public class DynamicTimeStretch
    {
        /// <summary>
        /// 计算需要的速度以在指定时间内完成动画
        /// </summary>
        public static float CalculateSpeedToFit(AnimationClip clip, float targetDuration)
        {
            if (clip == null || targetDuration <= 0f)
                return 1f;
            
            return clip.length / targetDuration;
        }
        
        /// <summary>
        /// 同步两个动画的播放速度
        /// </summary>
        public static void SynchronizePlayback(
            AnimationClipPlayable playable1, AnimationClip clip1,
            AnimationClipPlayable playable2, AnimationClip clip2)
        {
            if (!playable1.IsValid() || !playable2.IsValid()) return;
            if (clip1 == null || clip2 == null) return;
            
            // 使用较长的动画作为基准
            if (clip1.length > clip2.length)
            {
                playable1.SetSpeed(1f);
                playable2.SetSpeed(clip2.length / clip1.length);
            }
            else
            {
                playable2.SetSpeed(1f);
                playable1.SetSpeed(clip1.length / clip2.length);
            }
        }
    }
}
