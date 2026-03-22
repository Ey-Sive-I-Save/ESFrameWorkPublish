using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
        // ==================== 2D混合树基类 ====================

        /// <summary>
        /// 2D混合树基类
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable]
        public abstract class StateAnimationMixCalculatorForBlendTree2D : StateAnimationMixCalculator
        {
            [Serializable]
            public class ClipSample2D
            {
                public AnimationClip clip;
                public Vector2 position;  // 2D空间位置
            }

            public StateParameter parameterX = "DirectionX";
            public StateParameter parameterY = "DirectionY";
            public ClipSample2D[] samples = new ClipSample2D[0];

            [Range(0f, 1f)]
            public float smoothTime = 0.1f;

            protected AnimationCalculatorRuntime.Triangle[] _sharedTriangles;  // 享元三角形数据
            
            /// <summary>
            /// Debug设置（可独立配置，默认使用全局设置）
            /// </summary>
            protected StateMachineDebugSettings debugSettings => StateMachineDebugSettings.Instance;

            public override bool NeedUpdateWhenFadingOut => true;

#if UNITY_EDITOR
            /// <summary>
            /// 编辑器初始化标准采样组 - 为2D混合树提供标准8方向+中心采样点
            /// </summary>
            [Button("初始化标准采样(8方向+中心)"), PropertyOrder(-1)]
            private void InitializeStandardSamples()
            {
                if (samples != null && samples.Length > 0)
                {
                    if (!UnityEditor.EditorUtility.DisplayDialog(
                        "确认初始化",
                        $"当前已有{samples.Length}个采样点，是否覆盖为标准配置？\n标准配置：8方向+中心共17个采样点（内外两圈+中心）",
                        "确认覆盖", "取消"))
                    {
                        return;
                    }
                }

                var sampleList = new System.Collections.Generic.List<ClipSample2D>();
                
                // 中心点
                sampleList.Add(new ClipSample2D { position = Vector2.zero, clip = null });
                
                // 外圈8个方向（半径1）
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * Mathf.Deg2Rad;
                    sampleList.Add(new ClipSample2D 
                    { 
                        position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 
                        clip = null 
                    });
                }
                
                // 内圈8个方向（半径0.5）
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 45f * Mathf.Deg2Rad;
                    sampleList.Add(new ClipSample2D 
                    { 
                        position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f, 
                        clip = null 
                    });
                }

                samples = sampleList.ToArray();

                debugSettings.LogRuntimeInit($"[BlendTree2D] 已初始化{samples.Length}个标准采样点（8方向外圈+8方向内圈+中心）");
            }
#endif

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享（预计算三角化）
            /// </summary>
            public override void InitializeCalculator()
            {
                // 预计算三角化(一次性计算，享元数据)
                ComputeDelaunayTriangulation();
            }

            protected override bool InitializeRuntimeInternal(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                if (samples == null || samples.Length == 0)
                {
                    debugSettings.LogError("[BlendTree2D] 采样点为空");
                    return false;
                }

                if (samples.Length == 1 && samples[0].clip != null)
                {
                    runtime.singlePlayable = AnimationClipPlayable.Create(graph, samples[0].clip);
                    output = runtime.singlePlayable;
                    return true;
                }

                if (samples.Length < 3)
                {
                    debugSettings.LogError("[BlendTree2D] 至少需要3个采样点");
                    return false;
                }

                // 创建Mixer(可被父Mixer连接)
                runtime.mixer = AnimationMixerPlayable.Create(graph, samples.Length);

                // 创建所有Clip的Playable(索引固定)
                runtime.playables = new AnimationClipPlayable[samples.Length];
                
                // 初始化权重缓存系统（与1D一致）
                runtime.weightCache = new float[samples.Length];
                runtime.weightTargetCache = new float[samples.Length];
                runtime.weightVelocityCache = new float[samples.Length];
                runtime.useSmoothing = smoothTime > 0.001f;
                
                // 找到中心点（最接近原点的点）
                int centerIndex = 0;
                float minDist = float.MaxValue;
                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = samples[i].position.sqrMagnitude;
                    if (dist < minDist)
                    {
                        minDist = dist;
                        centerIndex = i;
                    }
                }
                
                for (int i = 0; i < samples.Length; i++)
                {
                    if (samples[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, samples[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        
                        // 初始化时给中心点100%权重（默认播放Idle）
                        float initialWeight = (i == centerIndex) ? 1f : 0f;
                        runtime.mixer.SetInputWeight(i, initialWeight);
                        runtime.weightCache[i] = initialWeight;
                        runtime.weightTargetCache[i] = initialWeight;
                    }
                }

                // 三角形数据从享元复制到Runtime(共享引用，不复制数组)
                runtime.triangles = _sharedTriangles;

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                
                debugSettings.LogRuntimeInit($"[BlendTree2D] Runtime初始化完成: {samples.Length}个采样点, 中心点索引={centerIndex}, 三角形数量={_sharedTriangles?.Length ?? 0}");
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
               // Debug.Log($"[BlendTree2D] 更新动画权重: Runtime={runtime}, DeltaTime={deltaTime}");
                if (!runtime.mixer.IsValid() || samples.Length == 0){
                    debugSettings.LogWarning("[BlendTree2D] Mixer无效或采样点为空，跳过权重更新");
                    return;
                }
                   

                // 通过context获取参数（直接传StateParameter，零GC）
                float paramX = context.GetFloat(parameterX, 0f);
                float paramY = context.GetFloat(parameterY, 0f);
                    
                Vector2 rawInput = new Vector2(paramX, paramY);

                // 平滑输入(零GC)
                Vector2 input = smoothTime > 0.001f
                    ? Vector2.SmoothDamp(runtime.lastInput2D, rawInput, ref runtime.inputVelocity2D, smoothTime, float.MaxValue, deltaTime)
                    : rawInput;
                runtime.lastInput2D = input;
                // 计算权重（带平滑过渡）
                CalculateWeights2D(runtime, input, deltaTime);
            }

            protected abstract void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime);

            /// <summary>
            /// 预计算Delaunay三角化 - 一次性计算，享元数据
            /// </summary>
            protected abstract void ComputeDelaunayTriangulation();

            /// <summary>
            /// BlendTree2D 首帧立即更新 — 跳过输入SmoothDamp，
            /// 关闭 useSmoothing 后调用 CalculateWeights2D 使内部权重直接到位，
            /// 同时重置 inputVelocity2D 避免残留速度影响后续帧。
            /// </summary>
            public override void ImmediateUpdate(AnimationCalculatorRuntime runtime, in StateMachineContext context)
            {
                if (runtime == null || !runtime.mixer.IsValid() || samples.Length == 0) return;

                // 直接读取参数，跳过输入平滑
                float paramX = context.GetFloat(parameterX, 0f);
                float paramY = context.GetFloat(parameterY, 0f);
                Vector2 rawInput = new Vector2(paramX, paramY);

                // 立即设置 lastInput2D（后续帧从此值开始平滑）
                runtime.lastInput2D = rawInput;
                runtime.inputVelocity2D = Vector2.zero;

                // 关闭权重平滑，直接计算到位
                bool prevSmoothing = runtime.useSmoothing;
                runtime.useSmoothing = false;
                CalculateWeights2D(runtime, rawInput, 0f);
                runtime.useSmoothing = prevSmoothing;

                // 重置权重速度缓存
                if (runtime.weightVelocityCache != null)
                {
                    for (int i = 0; i < runtime.weightVelocityCache.Length; i++)
                        runtime.weightVelocityCache[i] = 0f;
                }
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                if (runtime.singlePlayable.IsValid())
                {
                    return runtime.singlePlayable.GetAnimationClip();
                }

                // 返回权重最大的Clip
                if (samples.Length == 0 || runtime.playables == null)
                    return null;
                    
                int maxWeightIndex = 0;
                float maxWeight = runtime.mixer.GetInputWeight(0);
                
                for (int i = 1; i < samples.Length; i++)
                {
                    float weight = runtime.mixer.GetInputWeight(i);
                    if (weight > maxWeight)
                    {
                        maxWeight = weight;
                        maxWeightIndex = i;
                    }
                }
                
                return runtime.playables[maxWeightIndex].GetAnimationClip();
            }
            
            /// <summary>
            /// 运行时覆盖Clip - 索引固定，仅替换内容
            /// </summary>
            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (samples.Length == 1 && clipIndex == 0 && runtime.singlePlayable.IsValid())
                {
                    var singleGraph = runtime.singlePlayable.GetGraph();
                    var singleOldSpeed = runtime.singlePlayable.GetSpeed();
                    var singleOldTime = runtime.singlePlayable.GetTime();

                    runtime.singlePlayable.Destroy();
                    runtime.singlePlayable = AnimationClipPlayable.Create(singleGraph, newClip);
                    runtime.singlePlayable.SetSpeed(singleOldSpeed);
                    runtime.singlePlayable.SetTime(singleOldTime);

                    samples[0].clip = newClip;
                    return true;
                }

                if (clipIndex < 0 || clipIndex >= samples.Length)
                {
                    debugSettings.LogError($"[BlendTree2D] 索引越界: {clipIndex} (有效范围: 0-{samples.Length - 1})");
                    return false;
                }
                
                if (runtime.playables == null || !runtime.playables[clipIndex].IsValid())
                {
                    debugSettings.LogError($"[BlendTree2D] Playable[{clipIndex}]无效");
                    return false;
                }
                
                if (newClip == null)
                {
                    debugSettings.LogError("[BlendTree2D] 新Clip为null");
                    return false;
                }
                
                // 零GC替换：保持索引和连接，仅替换Playable
                var graph = runtime.playables[clipIndex].GetGraph();
                var oldSpeed = runtime.playables[clipIndex].GetSpeed();
                var oldTime = runtime.playables[clipIndex].GetTime();
                var currentWeight = runtime.mixer.GetInputWeight(clipIndex);
                
                // 断开连接
                runtime.mixer.DisconnectInput(clipIndex);
                runtime.playables[clipIndex].Destroy();
                
                // 创建新Playable并重新连接
                runtime.playables[clipIndex] = AnimationClipPlayable.Create(graph, newClip);
                runtime.playables[clipIndex].SetSpeed(oldSpeed);
                runtime.playables[clipIndex].SetTime(oldTime);
                graph.Connect(runtime.playables[clipIndex], 0, runtime.mixer, clipIndex);
                runtime.mixer.SetInputWeight(clipIndex, currentWeight);
                
                return true;
            }

        public override float GetStandardDuration(AnimationCalculatorRuntime runtime)
        {
            if (samples == null || samples.Length == 0)
                return 0f;

            int maxWeightIndex = 0;
            float maxWeight = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float weight = runtime.weightCache != null && i < runtime.weightCache.Length
                    ? runtime.weightCache[i]
                    : runtime.mixer.GetInputWeight(i);

                if (weight > maxWeight)
                {
                    maxWeight = weight;
                    maxWeightIndex = i;
                }
            }

            var clip = samples[maxWeightIndex].clip;
            return clip != null ? clip.length : 0f;
        }
        }

        // ==================== 2D自由方向混合 ====================

        /// <summary>
        /// 2D自由方向混合 - 适用于移动动画
        /// 典型应用: 8方向移动(前后左右+4个斜向)
        /// 性能: O(n)三角形查找 + O(1)重心坐标
        /// </summary>
        [Serializable, TypeRegistryItem("2D混合树(方向型)")]
        // 对应枚举：BlendTree2D_Directional
        public class StateAnimationMixCalculatorForBlendTree2DFreeformDirectional : StateAnimationMixCalculatorForBlendTree2D
        {
            public override StateAnimationMixerKind CalculatorKind => StateAnimationMixerKind.BlendTree2D_Directional;

            protected override string GetUsageHelp()
            {
                 return "适用：方向移动（8向/多向）。\n" +
                        "必填：采样点（二维位置 + 动画片段），以及 2 个用于驱动的 float 参数（横向/纵向）。\n" +
                        "建议：中心 Idle 在 (0,0)，外圈为跑，内圈为走。";
            }

            protected override string GetCalculatorDisplayName()
            {
                return "2D混合树(方向型)";
            }
            // 8方向 Walk + 8方向 Run + 中心Idle 的环形缓存
            [NonSerialized] private bool _ringsPrepared;
            [NonSerialized] private int _centerIndex = -1;
            [NonSerialized] private int[] _innerRing = Array.Empty<int>();
            [NonSerialized] private int[] _outerRing = Array.Empty<int>();
            [NonSerialized] private float[] _innerAngles = Array.Empty<float>();
            [NonSerialized] private float[] _outerAngles = Array.Empty<float>();
            [NonSerialized] private float _innerRadius;
            [NonSerialized] private float _outerRadius;

            /// <summary>
            /// 重写初始化：预计算中心点、内外环、角度缓存
            /// </summary>
            public override void InitializeCalculator()
            {
                PrepareRings();
            }

            protected override void ComputeDelaunayTriangulation()
            {
                // 本算法不使用三角化，保留空实现，避免上层逻辑依赖
                _sharedTriangles = null;
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime)
            {
                if (samples == null || samples.Length == 0)
                {
                    debugSettings.LogWarning("【混合树】samples为空，无法计算权重");
                    return;
                }

                if (!_ringsPrepared)
                    PrepareRings();

                if (_centerIndex < 0 || _innerRing.Length < 2)
                {
                    int fallback = FindNearestSample(input);
                    for (int i = 0; i < samples.Length; i++)
                        runtime.weightTargetCache[i] = 0f;
                    runtime.weightTargetCache[fallback] = 1f;
                    ApplyWeights(runtime, deltaTime);
                    return;
                }

                // 计算输入角度与半径
                float inputMagnitude = input.magnitude;
                float angle = Mathf.Atan2(input.y, input.x);
                if (angle < 0f) angle += Mathf.PI * 2f;

                // 限制输入到外圈半径范围内（保证稳定）
                if (_outerRadius > 0.001f && inputMagnitude > _outerRadius)
                {
                    inputMagnitude = _outerRadius;
                }

                // 清空目标权重
                for (int i = 0; i < samples.Length; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 中心权重（Idle）
                float centerRadius = _innerRing.Length >= 2 ? _innerRadius : _outerRadius;
                float centerWeight = 0f;
                if (centerRadius > 0.001f)
                {
                    centerWeight = Mathf.Clamp01(1f - (inputMagnitude / centerRadius));
                }
                runtime.weightTargetCache[_centerIndex] = centerWeight;

                // 方向权重（内环/外环）
                float radialT = 1f;
                if (_innerRing.Length >= 2 && _outerRadius > _innerRadius + 0.0001f)
                {
                    radialT = Mathf.Clamp01((inputMagnitude - _innerRadius) / (_outerRadius - _innerRadius));
                }

                // 内环权重（存在时参与混合）
                if (_innerRing.Length >= 2)
                {
                    GetDirectionalPair(_innerRing, _innerAngles, angle, out int iA, out int iB, out float t);
                    float innerScale = (1f - centerWeight) * (1f - radialT);
                    runtime.weightTargetCache[iA] += (1f - t) * innerScale;
                    runtime.weightTargetCache[iB] += t * innerScale;
                }

                // 外环权重
                if (_outerRing.Length >= 2)
                {
                    GetDirectionalPair(_outerRing, _outerAngles, angle, out int oA, out int oB, out float t);
                    float outerScale = (1f - centerWeight) * radialT;
                    runtime.weightTargetCache[oA] += (1f - t) * outerScale;
                    runtime.weightTargetCache[oB] += t * outerScale;
                }

                NormalizeWeights(runtime);
                ApplyWeights(runtime, deltaTime);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool IsPointInTriangle(Vector2 p, AnimationCalculatorRuntime.Triangle triangle)
            {
                // 使用重心坐标法判断点是否在三角形内
                Vector3 bary = CalculateBarycentricCoordinates(p, triangle.v0, triangle.v1, triangle.v2);
                
                // 如果三个重心坐标都非负（带容差），则点在三角形内
                return bary.x >= -0.001f && bary.y >= -0.001f && bary.z >= -0.001f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private Vector3 CalculateBarycentricCoordinates(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                // 使用叉积法计算重心坐标（更稳定）
                Vector2 v0 = c - a;
                Vector2 v1 = b - a;
                Vector2 v2 = p - a;

                float d00 = Vector2.Dot(v0, v0);
                float d01 = Vector2.Dot(v0, v1);
                float d11 = Vector2.Dot(v1, v1);
                float d20 = Vector2.Dot(v2, v0);
                float d21 = Vector2.Dot(v2, v1);

                float denom = d00 * d11 - d01 * d01;
                
                // 防止除零
                if (Mathf.Abs(denom) < 0.0001f)
                {
                    // 三角形退化，返回最近顶点
                    float distA = Vector2.Distance(p, a);
                    float distB = Vector2.Distance(p, b);
                    float distC = Vector2.Distance(p, c);
                    
                    if (distA <= distB && distA <= distC)
                        return new Vector3(1, 0, 0);
                    else if (distB <= distC)
                        return new Vector3(0, 1, 0);
                    else
                        return new Vector3(0, 0, 1);
                }

                float invDenom = 1f / denom;
                float w = (d11 * d20 - d01 * d21) * invDenom;
                float v = (d00 * d21 - d01 * d20) * invDenom;
                float u = 1f - v - w;

                return new Vector3(u, v, w);
            }

            private int FindNearestSample(Vector2 input)
            {
                float minDist = float.MaxValue;
                int nearest = 0;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.SqrMagnitude(input - samples[i].position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = i;
                    }
                }

                return nearest;
            }

            private void PrepareRings()
            {
                _ringsPrepared = true;
                _centerIndex = -1;
                _innerRing = Array.Empty<int>();
                _outerRing = Array.Empty<int>();
                _innerAngles = Array.Empty<float>();
                _outerAngles = Array.Empty<float>();
                _innerRadius = 0f;
                _outerRadius = 0f;

                if (samples == null || samples.Length < 3)
                {
                    debugSettings.LogWarning("【混合树】samples不足，无法初始化环形混合");
                    return;
                }

                // 找中心点（最近原点）
                float minDist = float.MaxValue;
                for (int i = 0; i < samples.Length; i++)
                {
                    float d = samples[i].position.sqrMagnitude;
                    if (d < minDist)
                    {
                        minDist = d;
                        _centerIndex = i;
                    }
                }

                if (_centerIndex < 0)
                    return;

                // 采集非中心点并按半径排序
                var others = new List<int>(samples.Length - 1);
                for (int i = 0; i < samples.Length; i++)
                {
                    if (i != _centerIndex)
                        others.Add(i);
                }

                others.Sort((a, b) =>
                {
                    float da = samples[a].position.sqrMagnitude;
                    float db = samples[b].position.sqrMagnitude;
                    return da.CompareTo(db);
                });

                if (others.Count < 2)
                    return;

                // 动态分割内/外环：寻找半径最大间隙
                float maxGap = 0f;
                int splitIndex = -1;
                float maxRadius = Mathf.Sqrt(samples[others[others.Count - 1]].position.sqrMagnitude);
                for (int i = 1; i < others.Count; i++)
                {
                    float r0 = samples[others[i - 1]].position.magnitude;
                    float r1 = samples[others[i]].position.magnitude;
                    float gap = r1 - r0;
                    if (gap > maxGap)
                    {
                        maxGap = gap;
                        splitIndex = i;
                    }
                }

                // 若间隙不足，则视为单环（仅外环）
                float gapThreshold = Mathf.Max(0.05f, maxRadius * 0.15f);
                if (splitIndex <= 0 || splitIndex >= others.Count || maxGap < gapThreshold)
                {
                    _outerRing = others.ToArray();
                    SortByAngle(_outerRing);
                    _outerAngles = BuildAngles(_outerRing);
                    _outerRadius = AverageRadius(_outerRing);
                    _innerRing = Array.Empty<int>();
                    _innerAngles = Array.Empty<float>();
                    _innerRadius = 0f;
                }
                else
                {
                    _innerRing = others.GetRange(0, splitIndex).ToArray();
                    _outerRing = others.GetRange(splitIndex, others.Count - splitIndex).ToArray();

                    // 角度排序
                    SortByAngle(_innerRing);
                    SortByAngle(_outerRing);

                    _innerAngles = BuildAngles(_innerRing);
                    _outerAngles = BuildAngles(_outerRing);
                    _innerRadius = AverageRadius(_innerRing);
                    _outerRadius = AverageRadius(_outerRing);

                    // 若内环不足2点，降级为单外环
                    if (_innerRing.Length < 2)
                    {
                        _outerRing = others.ToArray();
                        SortByAngle(_outerRing);
                        _outerAngles = BuildAngles(_outerRing);
                        _outerRadius = AverageRadius(_outerRing);
                        _innerRing = Array.Empty<int>();
                        _innerAngles = Array.Empty<float>();
                        _innerRadius = 0f;
                    }
                }

                debugSettings.LogRuntimeInit($"[BlendTree2D-Directional] 环形初始化完成: center={_centerIndex}, inner={_innerRing.Length}, outer={_outerRing.Length}, innerR={_innerRadius:F2}, outerR={_outerRadius:F2}, gap={maxGap:F2}");
            }

            private void SortByAngle(int[] indices)
            {
                Array.Sort(indices, (a, b) =>
                {
                    float angleA = Mathf.Atan2(samples[a].position.y, samples[a].position.x);
                    float angleB = Mathf.Atan2(samples[b].position.y, samples[b].position.x);
                    if (angleA < 0f) angleA += Mathf.PI * 2f;
                    if (angleB < 0f) angleB += Mathf.PI * 2f;
                    return angleA.CompareTo(angleB);
                });
            }

            private float[] BuildAngles(int[] indices)
            {
                var angles = new float[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    float a = Mathf.Atan2(samples[indices[i]].position.y, samples[indices[i]].position.x);
                    if (a < 0f) a += Mathf.PI * 2f;
                    angles[i] = a;
                }
                return angles;
            }

            private float AverageRadius(int[] indices)
            {
                if (indices.Length == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < indices.Length; i++)
                    sum += samples[indices[i]].position.magnitude;
                return sum / indices.Length;
            }

            private void GetDirectionalPair(int[] ring, float[] angles, float angle, out int iA, out int iB, out float t)
            {
                iA = ring[0];
                iB = ring[0];
                t = 0f;

                if (ring.Length == 1)
                {
                    iA = ring[0];
                    iB = ring[0];
                    t = 0f;
                    return;
                }

                // 找到角度区间
                int idx = 0;
                for (int i = 0; i < angles.Length; i++)
                {
                    if (angles[i] >= angle)
                    {
                        idx = i;
                        break;
                    }
                    if (i == angles.Length - 1)
                        idx = 0;
                }

                int prev = (idx - 1 + ring.Length) % ring.Length;
                int next = idx;

                float a0 = angles[prev];
                float a1 = angles[next];
                if (a1 < a0) a1 += Mathf.PI * 2f;
                float a = angle;
                if (a < a0) a += Mathf.PI * 2f;

                float span = a1 - a0;
                t = span > 0.0001f ? Mathf.Clamp01((a - a0) / span) : 0f;

                iA = ring[prev];
                iB = ring[next];
            }

            private void NormalizeWeights(AnimationCalculatorRuntime runtime)
            {
                float sum = 0f;
                for (int i = 0; i < samples.Length; i++)
                    sum += runtime.weightTargetCache[i];

                if (sum > 0.0001f)
                {
                    float inv = 1f / sum;
                    for (int i = 0; i < samples.Length; i++)
                        runtime.weightTargetCache[i] *= inv;
                }
            }

            private void ApplyWeights(AnimationCalculatorRuntime runtime, float deltaTime)
            {
                float smoothingSpeed = smoothTime > 0.001f ? smoothTime * 0.5f : 0.001f;
                for (int i = 0; i < samples.Length; i++)
                {
                    if (runtime.useSmoothing)
                    {
                        runtime.weightCache[i] = Mathf.SmoothDamp(
                            runtime.weightCache[i],
                            runtime.weightTargetCache[i],
                            ref runtime.weightVelocityCache[i],
                            smoothingSpeed,
                            float.MaxValue,
                            deltaTime
                        );
                    }
                    else
                    {
                        runtime.weightCache[i] = runtime.weightTargetCache[i];
                    }
                    runtime.mixer.SetInputWeight(i, runtime.weightCache[i]);
                }
            }
        }

     
}
