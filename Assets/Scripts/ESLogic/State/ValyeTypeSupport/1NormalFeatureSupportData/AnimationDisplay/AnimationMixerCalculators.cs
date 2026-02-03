using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sirenix.OdinInspector;

namespace ES
{
    // 注意：StateDefaultParameter枚举已移至 0EnumSupport/StateDefaultParameter.cs
    // 注意：StateParameter结构体已移至 1NormalFeatureSupportData/StateParameter.cs
    
    
    /// <summary>
    /// 动画Clip计算器基类 - 零GC高性能抽象
    /// 配置数据(享元),运行时数据分离
    /// 设计原则:
    /// 1. Calculator不可变，作为共享配置数据
    /// 2. Runtime可变，每个状态独占一个
    /// 3. Runtime绑定Calculator后不变，但Clip可被override(索引保持)
    /// 4. 支持多层级Mixer嵌套，可接入其他Mixer
    /// 5. 参数获取通过StateContext，支持枚举+字符串方式
    /// </summary>
    [Serializable]
    public abstract class StateAnimationMixCalculator
    {
        /// <summary>
        /// 创建运行时数据
        /// Runtime与Calculator绑定后不变，保证索引稳定性
        /// </summary>
        public virtual AnimationCalculatorRuntime CreateRuntimeData()
        {
            return new AnimationCalculatorRuntime();
        }

        /// <summary>
        /// 初始化运行时Playable - 连接到PlayableGraph或父Mixer
        /// 支持多层级：可以连接到主Graph或父级Mixer
        /// 注意：每个Runtime独立调用，不共享Playable实例
        /// </summary>
        /// <param name="runtime">运行时数据(绑定后不变)</param>
        /// <param name="graph">PlayableGraph引用</param>
        /// <param name="output">输出Playable(可被父Mixer连接)</param>
        /// <returns>是否初始化成功</returns>
        public abstract bool InitializeRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output);

        /// <summary>
        /// 零GC更新权重 - 每帧调用
        /// 通过StateContext获取参数，支持枚举+字符串方式
        /// 建议：常用参数使用枚举，动态参数使用字符串
        /// </summary>
        /// <param name="runtime">运行时数据</param>
        /// <param name="context">状态上下文(用于读取参数，in修饰避免复制)</param>
        /// <param name="deltaTime">帧时间</param>
        public abstract void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime);

        /// <summary>
        /// 获取当前播放的主Clip(用于调试)
        /// </summary>
        public abstract AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime);
        
        /// <summary>
        /// 运行时覆盖Clip - 索引不变，仅替换内容
        /// 用于动态替换动画(如换装/武器切换)
        /// </summary>
        /// <param name="runtime">运行时数据</param>
        /// <param name="clipIndex">Clip索引(必须在有效范围内)</param>
        /// <param name="newClip">新的AnimationClip</param>
        /// <returns>是否替换成功</returns>
        public virtual bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
        {
            // 默认实现：不支持覆盖
            Debug.LogWarning($"[{GetType().Name}] 不支持Clip覆盖");
            return false;
        }
    }
    // ==================== 扩展StateAnimationConfigData ====================


        // ==================== 单Clip计算器 ====================

        /// <summary>
        /// 简单Clip - 直接播放单个AnimationClip
        /// 支持运行时Clip覆盖，可接入任意Mixer
        /// </summary>
        [Serializable, TypeRegistryItem("单一Clip播放器")]
        public class StateAnimationMixCalculatorForSimpleClip : StateAnimationMixCalculator
        {
            public AnimationClip clip;

            [Range(0f, 3f)]
            public float speed = 1f;

            public override bool InitializeRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                if (clip == null)
                {
                    Debug.LogError("[SimpleClip] Clip未设置");
                    return false;
                }

                // 创建Playable(可被任意Mixer连接)
                runtime.singlePlayable = AnimationClipPlayable.Create(graph, clip);
                runtime.singlePlayable.SetSpeed(speed);

                // 输出Playable供父级连接(支持多层级)
                output = runtime.singlePlayable;
                runtime.IsInitialized = true;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                // 单Clip无需更新权重，但可通过context支持动态速度
                // 示例: runtime.singlePlayable.SetSpeed(context.GetFloat("Speed", speed));
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                // 如果Clip被覆盖，返回运行时的Clip
                if (runtime.singlePlayable.IsValid())
                {
                    return runtime.singlePlayable.GetAnimationClip();
                }
                return clip;
            }
            
            /// <summary>
            /// 运行时覆盖Clip - 索引0固定
            /// </summary>
            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (clipIndex != 0)
                {
                    Debug.LogError($"[SimpleClip] 索引无效: {clipIndex}，仅支持索引0");
                    return false;
                }
                
                if (!runtime.singlePlayable.IsValid())
                {
                    Debug.LogError("[SimpleClip] Runtime未初始化");
                    return false;
                }
                
                if (newClip == null)
                {
                    Debug.LogError("[SimpleClip] 新Clip为null");
                    return false;
                }
                
                // 零GC替换：销毁旧Playable，创建新Playable
                var graph = runtime.singlePlayable.GetGraph();
                var oldSpeed = runtime.singlePlayable.GetSpeed();
                var oldTime = runtime.singlePlayable.GetTime();
                
                runtime.singlePlayable.Destroy();
                runtime.singlePlayable = AnimationClipPlayable.Create(graph, newClip);
                runtime.singlePlayable.SetSpeed(oldSpeed);
                runtime.singlePlayable.SetTime(oldTime);
                
                return true;
            }
        }

        // ==================== 1D混合树 ====================

        /// <summary>
        /// 1D混合树 - 单参数线性混合
        /// 典型应用: Idle → Walk → Run → Sprint
        /// 性能: O(log n)查找 + O(1)插值
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable, TypeRegistryItem("1D混合树")]
        public class StateAnimationMixCalculatorForBlendTree1D : StateAnimationMixCalculator
        {
            [Serializable]
            public struct ClipSampleForBlend1D
            {
                public AnimationClip clip;
                public float threshold;  // 阈值位置
            }

            public StateParameter parameterFloat = StateDefaultFloatParameter.Speed;
            public ClipSampleForBlend1D[] samples = new ClipSampleForBlend1D[0];

            [Range(0f, 1f)]
            public float smoothTime = 0.1f;

            private bool _isCalculatorInitialized;

#if UNITY_EDITOR
            /// <summary>
            /// 编辑器初始化标准采样组 - 为1D混合树提供标准阈值采样点
            /// </summary>
            [Button("初始化标准采样(Idle-Walk-Run)"), PropertyOrder(-1)]
            private void InitializeStandardSamples()
            {
                if (samples != null && samples.Length > 0)
                {
                    if (!UnityEditor.EditorUtility.DisplayDialog(
                        "确认初始化",
                        "当前已有采样点，是否覆盖为标准配置？\n标准配置：Idle(0) → Walk(0.5) → Run(1.0)",
                        "确认覆盖", "取消"))
                    {
                        return;
                    }
                }

                samples = new ClipSampleForBlend1D[3]
                {
                    new ClipSampleForBlend1D { threshold = 0f, clip = null },
                    new ClipSampleForBlend1D { threshold = 0.5f, clip = null },
                    new ClipSampleForBlend1D { threshold = 1.0f, clip = null }
                };
                
                
                Debug.Log("[BlendTree1D] 已初始化3个标准采样点");
            }
#endif

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享
            /// </summary>
            public void InitializeCalculator()
            {
                if (_isCalculatorInitialized || samples == null || samples.Length == 0)
                    return;

                // 按阈值排序(享元数据，一次性计算)
                Array.Sort(samples, (a, b) => a.threshold.CompareTo(b.threshold));
                _isCalculatorInitialized = true;
            }

            public override bool InitializeRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                // 确保Calculator已初始化
                InitializeCalculator();

                if (samples == null || samples.Length == 0)
                {
                    Debug.LogError("[BlendTree1D] 采样点为空");
                    return false;
                }

                // 创建Mixer(可被父Mixer连接)
                runtime.mixer = AnimationMixerPlayable.Create(graph, samples.Length);

                // 创建所有Clip的Playable(索引固定)
                runtime.playables = new AnimationClipPlayable[samples.Length];
                
                // 初始化权重缓存系统
                runtime.weightCache = new float[samples.Length];
                runtime.weightTargetCache = new float[samples.Length];
                runtime.weightVelocityCache = new float[samples.Length];
                runtime.useSmoothing = smoothTime > 0.001f;
                
                for (int i = 0; i < samples.Length; i++)
                {
                    if (samples[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, samples[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        runtime.mixer.SetInputWeight(i, 0f);
                        runtime.weightCache[i] = 0f;
                    }
                }

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                runtime.IsInitialized = true;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                if (!runtime.mixer.IsValid() || samples.Length == 0)
                    return;

                // 通过context获取参数（直接传StateParameter，零GC）
                float rawInput = context.GetFloat(parameterFloat, 0f);

                // 平滑输入(零GC)
                float input = smoothTime > 0.001f
                    ? Mathf.SmoothDamp(runtime.lastInput, rawInput, ref runtime.inputVelocity, smoothTime, float.MaxValue, deltaTime)
                    : rawInput;
                runtime.lastInput = input;

                // 零GC计算权重（使用缓存和平滑）
                CalculateWeights1D(runtime, input, deltaTime);
            }

            private void CalculateWeights1D(AnimationCalculatorRuntime runtime, float input, float deltaTime)
            {
                int count = samples.Length;

                // 计算目标权重（不直接应用，先缓存）
                for (int i = 0; i < count; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 边界情况
                if (input <= samples[0].threshold)
                {
                    runtime.weightTargetCache[0] = 1f;
                }
                else if (input >= samples[count - 1].threshold)
                {
                    runtime.weightTargetCache[count - 1] = 1f;
                }
                else
                {
                    // 二分查找 O(log n)
                    int rightIndex = BinarySearchRight(input);
                    int leftIndex = rightIndex - 1;

                    // 线性插值
                    float leftThreshold = samples[leftIndex].threshold;
                    float rightThreshold = samples[rightIndex].threshold;
                    float t = (input - leftThreshold) / (rightThreshold - leftThreshold);

                    // 设置权重(仅2个Clip)
                    runtime.weightTargetCache[leftIndex] = 1f - t;
                    runtime.weightTargetCache[rightIndex] = t;
                }

                // 平滑过渡到目标权重（避免僵硬跳变）
                float smoothingSpeed = smoothTime > 0.001f ? smoothTime * 0.5f : 0.001f;
                for (int i = 0; i < count; i++)
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

            private int BinarySearchRight(float value)
            {
                int left = 0;
                int right = samples.Length - 1;

                while (left < right)
                {
                    int mid = (left + right) / 2;
                    if (samples[mid].threshold < value)
                        left = mid + 1;
                    else
                        right = mid;
                }

                return right;
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
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
                if (clipIndex < 0 || clipIndex >= samples.Length)
                {
                    Debug.LogError($"[BlendTree1D] 索引越界: {clipIndex} (有效范围: 0-{samples.Length - 1})");
                    return false;
                }
                
                if (runtime.playables == null || !runtime.playables[clipIndex].IsValid())
                {
                    Debug.LogError($"[BlendTree1D] Playable[{clipIndex}]无效");
                    return false;
                }
                
                if (newClip == null)
                {
                    Debug.LogError("[BlendTree1D] 新Clip为null");
                    return false;
                }
                
                // 零GC替换：保持索引和连接，仅替换Playable
                var graph = runtime.playables[clipIndex].GetGraph();
                var oldSpeed = runtime.playables[clipIndex].GetSpeed();
                var oldTime = runtime.playables[clipIndex].GetTime();
                
                // 断开连接
                runtime.mixer.DisconnectInput(clipIndex);
                runtime.playables[clipIndex].Destroy();
                
                // 创建新Playable并重新连接
                runtime.playables[clipIndex] = AnimationClipPlayable.Create(graph, newClip);
                runtime.playables[clipIndex].SetSpeed(oldSpeed);
                runtime.playables[clipIndex].SetTime(oldTime);
                graph.Connect(runtime.playables[clipIndex], 0, runtime.mixer, clipIndex);
                
                return true;
            }
        }

        // ==================== 2D混合树基类 ====================

        /// <summary>
        /// 2D混合树基类
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable]
        public abstract class StateAnimationMixCalculatorForBlendTree2D : StateAnimationMixCalculator
        {
            [Serializable]
            public struct ClipSample2D
            {
                public AnimationClip clip;
                public Vector2 position;  // 2D空间位置
            }

            public StateParameter parameterX = "DirectionX";
            public StateParameter parameterY = "DirectionY";
            public ClipSample2D[] samples = new ClipSample2D[0];

            [Range(0f, 1f)]
            public float smoothTime = 0.1f;

            protected bool _isCalculatorInitialized;
            protected AnimationCalculatorRuntime.Triangle[] _sharedTriangles;  // 享元三角形数据

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
                _isCalculatorInitialized = false; // 重置初始化标记
                

                Debug.Log($"[BlendTree2D] 已初始化{samples.Length}个标准采样点（8方向外圈+8方向内圈+中心）");
            }
#endif

            /// <summary>
            /// 计算器初始化 - 仅执行一次，所有Runtime共享（预计算三角化）
            /// </summary>
            public virtual void InitializeCalculator()
            {
                if (_isCalculatorInitialized)
                    return;

                // 预计算三角化(一次性计算，享元数据)
                ComputeDelaunayTriangulation();
                _isCalculatorInitialized = true;
            }

            public override bool InitializeRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                // 确保Calculator已初始化
                InitializeCalculator();

                if (samples == null || samples.Length < 3)
                {
                    Debug.LogError("[BlendTree2D] 至少需要3个采样点");
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
                runtime.IsInitialized = true;
                
                Debug.Log($"[BlendTree2D] Runtime初始化完成: {samples.Length}个采样点, 中心点索引={centerIndex}, 三角形数量={_sharedTriangles?.Length ?? 0}");
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                if (!runtime.mixer.IsValid() || samples.Length == 0)
                    return;

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

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
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
                if (clipIndex < 0 || clipIndex >= samples.Length)
                {
                    Debug.LogError($"[BlendTree2D] 索引越界: {clipIndex} (有效范围: 0-{samples.Length - 1})");
                    return false;
                }
                
                if (runtime.playables == null || !runtime.playables[clipIndex].IsValid())
                {
                    Debug.LogError($"[BlendTree2D] Playable[{clipIndex}]无效");
                    return false;
                }
                
                if (newClip == null)
                {
                    Debug.LogError("[BlendTree2D] 新Clip为null");
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
        }

        // ==================== 2D自由方向混合 ====================

        /// <summary>
        /// 2D自由方向混合 - 适用于移动动画
        /// 典型应用: 8方向移动(前后左右+4个斜向)
        /// 性能: O(n)三角形查找 + O(1)重心坐标
        /// </summary>
        [Serializable, TypeRegistryItem("2D混合树-方向型")]
        public class StateAnimationMixCalculatorForBlendTree2DFreeformDirectional : StateAnimationMixCalculatorForBlendTree2D
        {
            protected override void ComputeDelaunayTriangulation()
            {
                if (samples == null || samples.Length < 3)
                {
                    _sharedTriangles = null;
                    return;
                }

                var triangleList = new List<AnimationCalculatorRuntime.Triangle>();

                // 找到最接近原点的点作为中心点（通常是Idle）
                int centerIndex = -1;
                float minDistToOrigin = float.MaxValue;
                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = samples[i].position.sqrMagnitude;
                    if (dist < minDistToOrigin)
                    {
                        minDistToOrigin = dist;
                        centerIndex = i;
                    }
                }

                if (centerIndex < 0) return;

                // 收集所有非中心点
                var outerPoints = new List<int>();
                for (int i = 0; i < samples.Length; i++)
                {
                    if (i != centerIndex)
                    {
                        outerPoints.Add(i);
                    }
                }

                if (outerPoints.Count < 2)
                {
                    _sharedTriangles = null;
                    return;
                }

                // 按角度排序外圈点（形成顺时针或逆时针环）
                outerPoints.Sort((a, b) =>
                {
                    Vector2 vecA = samples[a].position - samples[centerIndex].position;
                    Vector2 vecB = samples[b].position - samples[centerIndex].position;
                    float angleA = Mathf.Atan2(vecA.y, vecA.x);
                    float angleB = Mathf.Atan2(vecB.y, vecB.x);
                    return angleA.CompareTo(angleB);
                });

                // 构建三角形扇：中心点连接每对相邻的外圈点
                for (int i = 0; i < outerPoints.Count; i++)
                {
                    int next = (i + 1) % outerPoints.Count;
                    
                    var triangle = new AnimationCalculatorRuntime.Triangle
                    {
                        i0 = centerIndex,
                        i1 = outerPoints[i],
                        i2 = outerPoints[next],
                        v0 = samples[centerIndex].position,
                        v1 = samples[outerPoints[i]].position,
                        v2 = samples[outerPoints[next]].position
                    };

                    triangleList.Add(triangle);
                }

                _sharedTriangles = triangleList.ToArray();
                Debug.Log($"[BlendTree2D-Directional] 三角化完成: {_sharedTriangles.Length}个三角形，中心点索引={centerIndex}");
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime)
            {
                if (samples == null || samples.Length == 0)
                {
                    Debug.LogWarning("[BlendTree2D-Directional] samples为空，无法计算权重");
                    return;
                }

                // 先计算目标权重，全部置零
                for (int i = 0; i < samples.Length; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 找到包含输入点的三角形
                AnimationCalculatorRuntime.Triangle? containingTriangle = null;
                if (runtime.triangles != null && runtime.triangles.Length > 0)
                {
                    foreach (var triangle in runtime.triangles)
                    {
                        if (IsPointInTriangle(input, triangle))
                        {
                            containingTriangle = triangle;
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"[BlendTree2D-Directional] 三角形数据为空！samples.Length={samples.Length}");
                }

                if (containingTriangle.HasValue)
                {
                    // 计算重心坐标
                    var tri = containingTriangle.Value;
                    Vector3 bary = CalculateBarycentricCoordinates(input, tri.v0, tri.v1, tri.v2);

                    // 确保权重有效且归一化
                    float sum = bary.x + bary.y + bary.z;
                    if (sum > 0.0001f)
                    {
                        bary /= sum;
                        runtime.weightTargetCache[tri.i0] = Mathf.Max(0f, bary.x);
                        runtime.weightTargetCache[tri.i1] = Mathf.Max(0f, bary.y);
                        runtime.weightTargetCache[tri.i2] = Mathf.Max(0f, bary.z);
                        
                        // Debug.Log($"[BlendTree2D-Directional] 输入({input.x:F2}, {input.y:F2}) → 三角形[{tri.i0},{tri.i1},{tri.i2}] 权重[{bary.x:F2},{bary.y:F2},{bary.z:F2}]");
                    }
                    else
                    {
                        // 重心坐标计算失败，回退到最近点
                        int nearest = FindNearestSample(input);
                        runtime.weightTargetCache[nearest] = 1f;
                        Debug.LogWarning($"[BlendTree2D-Directional] 重心坐标无效，使用最近点 #{nearest}");
                    }
                }
                else
                {
                    // 未找到包含三角形，使用最近点
                    int nearest = FindNearestSample(input);
                    runtime.weightTargetCache[nearest] = 1f;
                    Debug.Log($"[BlendTree2D-Directional] 输入({input.x:F2}, {input.y:F2}) 不在任何三角形内，使用最近点 #{nearest}");
                }

                // 平滑过渡到目标权重
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

            private bool IsPointInTriangle(Vector2 p, AnimationCalculatorRuntime.Triangle triangle)
            {
                // 使用重心坐标法判断点是否在三角形内
                Vector3 bary = CalculateBarycentricCoordinates(p, triangle.v0, triangle.v1, triangle.v2);
                
                // 如果三个重心坐标都非负（带容差），则点在三角形内
                return bary.x >= -0.001f && bary.y >= -0.001f && bary.z >= -0.001f;
            }

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
        }

        // ==================== 2D笛卡尔混合 ====================

        /// <summary>
        /// 2D笛卡尔混合 - 适用于瞄准偏移
        /// 典型应用: Aim Offset (Yaw/Pitch独立混合)
        /// 性能: O(1)查找最近4个点 + O(1)双线性插值
        /// </summary>
        [Serializable, TypeRegistryItem("2D混合树-笛卡尔型")]
        public class StateAnimationMixCalculatorForBlendTree2DFreeformCartesian : StateAnimationMixCalculatorForBlendTree2D
        {
            protected override void ComputeDelaunayTriangulation()
            {
                // 笛卡尔模式使用网格结构,不需要三角化
                _sharedTriangles = null;
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input, float deltaTime)
            {
                // 先计算目标权重，全部置零
                for (int i = 0; i < samples.Length; i++)
                    runtime.weightTargetCache[i] = 0f;

                // 找到最近的4个点(形成矩形)
                FindClosestGridPoints(input, out int i0, out int i1, out int i2, out int i3);

                if (i0 < 0)
                {
                    // 找最近的单点
                    int nearest = FindNearestSample(input);
                    runtime.weightTargetCache[nearest] = 1f;
                }
                else
                {
                    // 双线性插值
                    Vector2 p0 = samples[i0].position;
                    Vector2 p1 = samples[i1].position;
                    Vector2 p2 = samples[i2].position;
                    Vector2 p3 = samples[i3].position;

                    // 计算插值权重
                    float tx = (input.x - p0.x) / (p1.x - p0.x);
                    float ty = (input.y - p0.y) / (p2.y - p0.y);
                    tx = Mathf.Clamp01(tx);
                    ty = Mathf.Clamp01(ty);

                    runtime.weightTargetCache[i0] = (1 - tx) * (1 - ty);
                    runtime.weightTargetCache[i1] = tx * (1 - ty);
                    runtime.weightTargetCache[i2] = (1 - tx) * ty;
                    runtime.weightTargetCache[i3] = tx * ty;
                }

                // 平滑过渡到目标权重
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

            private void FindClosestGridPoints(Vector2 input, out int i0, out int i1, out int i2, out int i3)
            {
                // 简化实现:找最近的4个点
                i0 = i1 = i2 = i3 = -1;

                float minDist0 = float.MaxValue;
                float minDist1 = float.MaxValue;
                float minDist2 = float.MaxValue;
                float minDist3 = float.MaxValue;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.SqrMagnitude(input - samples[i].position);

                    if (dist < minDist0)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = minDist1;
                        i2 = i1;
                        minDist1 = minDist0;
                        i1 = i0;
                        minDist0 = dist;
                        i0 = i;
                    }
                    else if (dist < minDist1)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = minDist1;
                        i2 = i1;
                        minDist1 = dist;
                        i1 = i;
                    }
                    else if (dist < minDist2)
                    {
                        minDist3 = minDist2;
                        i3 = i2;
                        minDist2 = dist;
                        i2 = i;
                    }
                    else if (dist < minDist3)
                    {
                        minDist3 = dist;
                        i3 = i;
                    }
                }
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
        }

        // ==================== Direct混合 ====================

        /// <summary>
        /// Direct混合 - 每个Clip独立控制权重
        /// 典型应用: 面部表情、手指动画、多层BlendShape
        /// 性能: O(n)权重更新
        /// 支持多层级嵌套，Mixer可连接到父Mixer
        /// </summary>
        [Serializable, TypeRegistryItem("直接混合器")]
        public class StateAnimationMixCalculatorForDirectBlend : StateAnimationMixCalculator
        {
            [Serializable]
            public struct DirectClip
            {
                public AnimationClip clip;
                public string weightParameter;  // 权重参数名
                public float defaultWeight;     // 默认权重
            }

            public DirectClip[] clips = new DirectClip[0];
            public bool autoNormalize = false;  // 是否自动归一化权重

            [Range(0f, 1f)]
            public float smoothTime = 0.05f;

#if UNITY_EDITOR
            /// <summary>
            /// 编辑器初始化标准采样组 - 为Direct混合提供标准插槽
            /// </summary>
            [Button("初始化标准采样(4个插槽)"), PropertyOrder(-1)]
            private void InitializeStandardSamples()
            {
                if (clips != null && clips.Length > 0)
                {
                    if (!UnityEditor.EditorUtility.DisplayDialog(
                        "确认初始化",
                        $"当前已有{clips.Length}个Clip，是否覆盖为标准配置？\n标准配置：4个独立控制插槽",
                        "确认覆盖", "取消"))
                    {
                        return;
                    }
                }

                clips = new DirectClip[4]
                {
                    new DirectClip { clip = null, weightParameter = "Weight0", defaultWeight = 0f },
                    new DirectClip { clip = null, weightParameter = "Weight1", defaultWeight = 0f },
                    new DirectClip { clip = null, weightParameter = "Weight2", defaultWeight = 0f },
                    new DirectClip { clip = null, weightParameter = "Weight3", defaultWeight = 0f }
                };
                

                Debug.Log("[DirectBlend] 已初始化4个标准插槽");
            }
#endif

            public override bool InitializeRuntime(AnimationCalculatorRuntime runtime, PlayableGraph graph, ref Playable output)
            {
                if (clips == null || clips.Length == 0)
                {
                    Debug.LogError("[DirectBlend] Clip列表为空");
                    return false;
                }

                // 创建Mixer(可被父Mixer连接)
                runtime.mixer = AnimationMixerPlayable.Create(graph, clips.Length);

                // 创建所有Clip的Playable(索引固定)
                runtime.playables = new AnimationClipPlayable[clips.Length];
                runtime.currentWeights = new float[clips.Length];
                runtime.targetWeights = new float[clips.Length];
                runtime.weightVelocities = new float[clips.Length];

                for (int i = 0; i < clips.Length; i++)
                {
                    if (clips[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, clips[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        runtime.mixer.SetInputWeight(i, clips[i].defaultWeight);
                        runtime.currentWeights[i] = clips[i].defaultWeight;
                    }
                }

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                runtime.IsInitialized = true;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, in StateMachineContext context, float deltaTime)
            {
                if (!runtime.mixer.IsValid())
                    return;

                // 通过context读取所有参数 - 零GC
                for (int i = 0; i < clips.Length; i++)
                {
                    string paramName = clips[i].weightParameter;
                    runtime.targetWeights[i] = string.IsNullOrEmpty(paramName)
                        ? clips[i].defaultWeight
                        : context.GetFloat(paramName, clips[i].defaultWeight);
                }

                // 归一化(可选)
                if (autoNormalize)
                {
                    float sum = 0f;
                    for (int i = 0; i < runtime.targetWeights.Length; i++)
                        sum += runtime.targetWeights[i];

                    if (sum > 0.001f)
                    {
                        for (int i = 0; i < runtime.targetWeights.Length; i++)
                            runtime.targetWeights[i] /= sum;
                    }
                }

                // 平滑过渡 - 零GC
                for (int i = 0; i < clips.Length; i++)
                {
                    if (smoothTime > 0.001f)
                    {
                        runtime.currentWeights[i] = Mathf.SmoothDamp(
                            runtime.currentWeights[i],
                            runtime.targetWeights[i],
                            ref runtime.weightVelocities[i],
                            smoothTime,
                            float.MaxValue,
                            deltaTime
                        );
                    }
                    else
                    {
                        runtime.currentWeights[i] = runtime.targetWeights[i];
                    }

                    runtime.mixer.SetInputWeight(i, runtime.currentWeights[i]);
                }
            }

            public override AnimationClip GetCurrentClip(AnimationCalculatorRuntime runtime)
            {
                // 返回权重最高的Clip
                if (clips.Length == 0 || runtime.currentWeights == null || runtime.playables == null)
                    return null;

                int maxIndex = 0;
                float maxWeight = runtime.currentWeights[0];

                for (int i = 1; i < runtime.currentWeights.Length; i++)
                {
                    if (runtime.currentWeights[i] > maxWeight)
                    {
                        maxWeight = runtime.currentWeights[i];
                        maxIndex = i;
                    }
                }

                return runtime.playables[maxIndex].GetAnimationClip();
            }
            
            /// <summary>
            /// 运行时覆盖Clip - 索引固定，仅替换内容
            /// </summary>
            public override bool OverrideClip(AnimationCalculatorRuntime runtime, int clipIndex, AnimationClip newClip)
            {
                if (clipIndex < 0 || clipIndex >= clips.Length)
                {
                    Debug.LogError($"[DirectBlend] 索引越界: {clipIndex} (有效范围: 0-{clips.Length - 1})");
                    return false;
                }
                
                if (runtime.playables == null || !runtime.playables[clipIndex].IsValid())
                {
                    Debug.LogError($"[DirectBlend] Playable[{clipIndex}]无效");
                    return false;
                }
                
                if (newClip == null)
                {
                    Debug.LogError("[DirectBlend] 新Clip为null");
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
        }
    
}
