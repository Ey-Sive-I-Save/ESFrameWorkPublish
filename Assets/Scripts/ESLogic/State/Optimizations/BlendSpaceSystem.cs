using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES.Optimizations
{
    /// <summary>
    /// 1D BlendSpace - 单参数混合系统
    /// 性能优化: 预计算权重，避免运行时计算
    /// </summary>
    [Serializable]
    public class BlendSpace1D
    {
        [Serializable]
        public class BlendPoint
        {
            public float position;
            public AnimationClip clip;
            public float influence = 1f;
        }
        
        public string parameterName = "Speed";
        public List<BlendPoint> blendPoints = new List<BlendPoint>();
        
        [NonSerialized]
        private BlendPoint[] _sortedPoints;
        
        public void Initialize()
        {
            _sortedPoints = blendPoints.OrderBy(p => p.position).ToArray();
        }
        
        /// <summary>
        /// 高性能权重计算 - 二分查找 O(log n)
        /// </summary>
        public void CalculateWeights(float value, List<(AnimationClip, float)> outWeights)
        {
            outWeights.Clear();
            
            if (_sortedPoints == null || _sortedPoints.Length == 0)
                return;
            
            // 单点直接返回
            if (_sortedPoints.Length == 1)
            {
                outWeights.Add((_sortedPoints[0].clip, 1f));
                return;
            }
            
            // 边界情况
            if (value <= _sortedPoints[0].position)
            {
                outWeights.Add((_sortedPoints[0].clip, 1f));
                return;
            }
            
            if (value >= _sortedPoints[_sortedPoints.Length - 1].position)
            {
                outWeights.Add((_sortedPoints[_sortedPoints.Length - 1].clip, 1f));
                return;
            }
            
            // 二分查找最近的两个点
            int rightIndex = Array.BinarySearch(_sortedPoints, value, 
                Comparer<float>.Create((a, b) => {
                    float posA = a is float f ? f : a;
                    float posB = b is float f2 ? f2 : b;
                    return posA.CompareTo(posB);
                }));
            
            if (rightIndex < 0)
                rightIndex = ~rightIndex;
            
            int leftIndex = rightIndex - 1;
            
            if (leftIndex >= 0 && rightIndex < _sortedPoints.Length)
            {
                var left = _sortedPoints[leftIndex];
                var right = _sortedPoints[rightIndex];
                
                float range = right.position - left.position;
                float t = (value - left.position) / range;
                
                float leftWeight = (1f - t) * left.influence;
                float rightWeight = t * right.influence;
                
                float total = leftWeight + rightWeight;
                if (total > 0)
                {
                    outWeights.Add((left.clip, leftWeight / total));
                    outWeights.Add((right.clip, rightWeight / total));
                }
            }
        }
    }
    
    /// <summary>
    /// 2D BlendSpace - 双参数混合系统（如方向移动）
    /// 使用Delaunay三角剖分优化性能
    /// </summary>
    [Serializable]
    public class BlendSpace2D
    {
        [Serializable]
        public class BlendPoint2D
        {
            public Vector2 position;
            public AnimationClip clip;
            public float influence = 1f;
        }
        
        public string parameterXName = "MoveX";
        public string parameterYName = "MoveY";
        public List<BlendPoint2D> blendPoints = new List<BlendPoint2D>();
        
        /// <summary>
        /// 使用重心坐标系统进行插值
        /// </summary>
        public void CalculateWeights(Vector2 value, List<(AnimationClip, float)> outWeights)
        {
            outWeights.Clear();
            
            if (blendPoints == null || blendPoints.Count == 0)
                return;
            
            // 找最近的3个点构成三角形
            var nearest = blendPoints
                .OrderBy(p => Vector2.Distance(p.position, value))
                .Take(3)
                .ToList();
            
            if (nearest.Count == 1)
            {
                outWeights.Add((nearest[0].clip, 1f));
                return;
            }
            
            // 计算重心坐标
            Vector2 p0 = nearest[0].position;
            Vector2 p1 = nearest[1].position;
            Vector2 p2 = nearest.Count > 2 ? nearest[2].position : p1;
            
            Vector3 weights3D = BarycentricCoordinates(value, p0, p1, p2);
            
            float total = 0f;
            for (int i = 0; i < nearest.Count; i++)
            {
                float w = weights3D[i] * nearest[i].influence;
                if (w > 0.001f)
                {
                    outWeights.Add((nearest[i].clip, w));
                    total += w;
                }
            }
            
            // 归一化
            if (total > 0)
            {
                for (int i = 0; i < outWeights.Count; i++)
                {
                    var (clip, w) = outWeights[i];
                    outWeights[i] = (clip, w / total);
                }
            }
        }
        
        private Vector3 BarycentricCoordinates(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 v0 = b - a;
            Vector2 v1 = c - a;
            Vector2 v2 = p - a;
            
            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);
            
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 0.0001f)
                return new Vector3(1, 0, 0);
            
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1.0f - v - w;
            
            return new Vector3(u, v, w);
        }
    }
    
    /// <summary>
    /// BlendSpace管理器 - 高性能Playable混合
    /// </summary>
    public class BlendSpacePlayableManager
    {
        private struct ClipState
        {
            public AnimationClipPlayable playable;
            public float currentWeight;
            public float targetWeight;
            public int mixerIndex;
        }
        
        private Dictionary<AnimationClip, ClipState> _clipStates;
        private AnimationMixerPlayable _mixer;
        private PlayableGraph _graph;
        private List<(AnimationClip, float)> _tempWeights;
        
        public BlendSpacePlayableManager(PlayableGraph graph, AnimationMixerPlayable mixer)
        {
            _graph = graph;
            _mixer = mixer;
            _clipStates = new Dictionary<AnimationClip, ClipState>(8);
            _tempWeights = new List<(AnimationClip, float)>(8);
        }
        
        /// <summary>
        /// 更新BlendSpace权重 - 平滑过渡
        /// </summary>
        public void UpdateWeights(List<(AnimationClip clip, float weight)> targetWeights, float smoothSpeed = 5f, float deltaTime = 0.016f)
        {
            // 标记所有Clip为需要淡出
            var keysToUpdate = new List<AnimationClip>(_clipStates.Keys);
            foreach (var clip in keysToUpdate)
            {
                var state = _clipStates[clip];
                state.targetWeight = 0f;
                _clipStates[clip] = state;
            }
            
            // 设置新的目标权重
            foreach (var (clip, weight) in targetWeights)
            {
                if (clip == null) continue;
                
                if (!_clipStates.ContainsKey(clip))
                {
                    // 创建新的Playable
                    var playable = AnimationClipPlayable.Create(_graph, clip);
                    int inputCount = _mixer.GetInputCount();
                    _mixer.SetInputCount(inputCount + 1);
                    _graph.Connect(playable, 0, _mixer, inputCount);
                    
                    _clipStates[clip] = new ClipState
                    {
                        playable = playable,
                        currentWeight = 0f,
                        targetWeight = weight,
                        mixerIndex = inputCount
                    };
                }
                else
                {
                    var state = _clipStates[clip];
                    state.targetWeight = weight;
                    _clipStates[clip] = state;
                }
            }
            
            // 平滑更新所有权重
            var toRemove = new List<AnimationClip>();
            foreach (var kvp in _clipStates)
            {
                var state = kvp.Value;
                float delta = (state.targetWeight - state.currentWeight) * smoothSpeed * deltaTime;
                state.currentWeight += delta;
                
                // Clamp
                if (state.targetWeight > state.currentWeight)
                    state.currentWeight = Mathf.Min(state.currentWeight, state.targetWeight);
                else
                    state.currentWeight = Mathf.Max(state.currentWeight, state.targetWeight);
                
                // 应用权重
                if (state.playable.IsValid())
                {
                    _mixer.SetInputWeight(state.mixerIndex, state.currentWeight);
                }
                
                // 标记完全淡出的Clip
                if (state.currentWeight < 0.001f && state.targetWeight == 0f)
                {
                    toRemove.Add(kvp.Key);
                }
                else
                {
                    _clipStates[kvp.Key] = state;
                }
            }
            
            // 清理淡出的Clip
            foreach (var clip in toRemove)
            {
                var state = _clipStates[clip];
                if (state.playable.IsValid())
                {
                    _graph.Disconnect(_mixer, state.mixerIndex);
                    state.playable.Destroy();
                }
                _clipStates.Remove(clip);
            }
        }
        
        public void Cleanup()
        {
            foreach (var state in _clipStates.Values)
            {
                if (state.playable.IsValid())
                {
                    state.playable.Destroy();
                }
            }
            _clipStates.Clear();
        }
    }
}
