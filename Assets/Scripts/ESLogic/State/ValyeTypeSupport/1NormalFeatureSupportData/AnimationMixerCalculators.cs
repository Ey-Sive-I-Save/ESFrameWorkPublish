using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace ES
{
    // 注意：StateDefaultParameter枚举已移至 0EnumSupport/StateDefaultParameter.cs
    // 注意：StateParameter结构体已移至 1NormalFeatureSupportData/StateParameter.cs
    
    /// <summary>
    /// 动画Mixer计算器集合
    /// 包含所有基于Mixer的动画混合计算器
    /// </summary>
    public static class AnimationMixerCalculators
    {
        // 此文件包含StateAnimationConfigData的所有Calculator实现

    }
    
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
    public abstract class AnimationClipPlayableCalculator
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
        /// <param name="context">状态上下文(用于读取参数)</param>
        /// <param name="deltaTime">帧时间</param>
        public abstract void UpdateWeights(AnimationCalculatorRuntime runtime, StateContext context, float deltaTime);

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
        [Serializable]
        public class SimpleClipCalculator : AnimationClipPlayableCalculator
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

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, StateContext context, float deltaTime)
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
        [Serializable]
        public class BlendTree1DCalculator : AnimationClipPlayableCalculator
        {
            [Serializable]
            public struct ClipSampleForBlend1D
            {
                public AnimationClip clip;
                public float threshold;  // 阈值位置
            }

            public StateParameter parameterName = StateDefaultFloatParameter.Speed;
            public ClipSampleForBlend1D[] samples = new ClipSampleForBlend1D[0];

            [Range(0f, 1f)]
            public float smoothTime = 0.1f;

            private bool _isCalculatorInitialized;

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
                for (int i = 0; i < samples.Length; i++)
                {
                    if (samples[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, samples[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        runtime.mixer.SetInputWeight(i, 0f);
                    }
                }

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                runtime.IsInitialized = true;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, StateContext context, float deltaTime)
            {
                if (!runtime.mixer.IsValid() || samples.Length == 0)
                    return;

                // 通过context获取参数（直接传StateParameter，零GC）
                float rawInput = context.GetFloat(parameterName, 0f);

                // 平滑输入(零GC)
                float input = smoothTime > 0.001f
                    ? Mathf.SmoothDamp(runtime.lastInput, rawInput, ref runtime.inputVelocity, smoothTime, float.MaxValue, deltaTime)
                    : rawInput;
                runtime.lastInput = input;

                // 零GC计算权重
                CalculateWeights1D(runtime, input);
            }

            private void CalculateWeights1D(AnimationCalculatorRuntime runtime, float input)
            {
                int count = samples.Length;

                // 边界情况
                if (input <= samples[0].threshold)
                {
                    runtime.mixer.SetInputWeight(0, 1f);
                    for (int i = 1; i < count; i++)
                        runtime.mixer.SetInputWeight(i, 0f);
                    return;
                }

                if (input >= samples[count - 1].threshold)
                {
                    runtime.mixer.SetInputWeight(count - 1, 1f);
                    for (int i = 0; i < count - 1; i++)
                        runtime.mixer.SetInputWeight(i, 0f);
                    return;
                }

                // 二分查找 O(log n)
                int rightIndex = BinarySearchRight(input);
                int leftIndex = rightIndex - 1;

                // 线性插值
                float leftThreshold = samples[leftIndex].threshold;
                float rightThreshold = samples[rightIndex].threshold;
                float t = (input - leftThreshold) / (rightThreshold - leftThreshold);

                // 设置权重(仅2个Clip)
                runtime.mixer.SetInputWeight(leftIndex, 1f - t);
                runtime.mixer.SetInputWeight(rightIndex, t);

                // 其他Clip权重为0
                for (int i = 0; i < count; i++)
                {
                    if (i != leftIndex && i != rightIndex)
                        runtime.mixer.SetInputWeight(i, 0f);
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
        public abstract class BlendTree2DCalculator : AnimationClipPlayableCalculator
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
                for (int i = 0; i < samples.Length; i++)
                {
                    if (samples[i].clip != null)
                    {
                        runtime.playables[i] = AnimationClipPlayable.Create(graph, samples[i].clip);
                        graph.Connect(runtime.playables[i], 0, runtime.mixer, i);
                        runtime.mixer.SetInputWeight(i, 0f);
                    }
                }

                // 三角形数据从享元复制到Runtime(共享引用，不复制数组)
                runtime.triangles = _sharedTriangles;

                // 输出Mixer供父级连接(支持多层级)
                output = runtime.mixer;
                runtime.IsInitialized = true;
                return true;
            }

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, StateContext context, float deltaTime)
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

                // 计算权重
                CalculateWeights2D(runtime, input);
            }

            protected abstract void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input);

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
        [Serializable]
        public class BlendTree2DFreeformDirectionalCalculator : BlendTree2DCalculator
        {
            protected override void ComputeDelaunayTriangulation()
            {
                // 简化的Delaunay三角化(实际项目使用完整算法)
                var triangleList = new List<AnimationCalculatorRuntime.Triangle>();

                // 找中心点
                Vector2 center = Vector2.zero;
                foreach (var sample in samples)
                    center += sample.position;
                center /= samples.Length;

                // 对每个点,连接相邻点形成三角形
                for (int i = 0; i < samples.Length; i++)
                {
                    int next = (i + 1) % samples.Length;

                    var triangle = new AnimationCalculatorRuntime.Triangle
                    {
                        i0 = i,
                        i1 = next,
                        i2 = FindClosestToMidpoint(samples[i].position, samples[next].position),
                        v0 = samples[i].position,
                        v1 = samples[next].position
                    };
                    triangle.v2 = samples[triangle.i2].position;

                    triangleList.Add(triangle);
                }

                _sharedTriangles = triangleList.ToArray();
            }

            private int FindClosestToMidpoint(Vector2 p1, Vector2 p2)
            {
                Vector2 mid = (p1 + p2) * 0.5f;
                float minDist = float.MaxValue;
                int closest = 0;

                for (int i = 0; i < samples.Length; i++)
                {
                    float dist = Vector2.Distance(samples[i].position, mid);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = i;
                    }
                }

                return closest;
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input)
            {
                // 找到包含输入点的三角形
                AnimationCalculatorRuntime.Triangle? containingTriangle = null;
                foreach (var triangle in runtime.triangles)
                {
                    if (IsPointInTriangle(input, triangle))
                    {
                        containingTriangle = triangle;
                        break;
                    }
                }

                if (containingTriangle == null)
                {
                    // 找最近的点
                    int nearest = FindNearestSample(input);
                    runtime.mixer.SetInputWeight(nearest, 1f);
                    for (int i = 0; i < samples.Length; i++)
                    {
                        if (i != nearest)
                            runtime.mixer.SetInputWeight(i, 0f);
                    }
                    return;
                }

                // 计算重心坐标
                var tri = containingTriangle.Value;
                Vector3 bary = CalculateBarycentricCoordinates(input, tri.v0, tri.v1, tri.v2);

                // 应用权重
                runtime.mixer.SetInputWeight(tri.i0, bary.x);
                runtime.mixer.SetInputWeight(tri.i1, bary.y);
                runtime.mixer.SetInputWeight(tri.i2, bary.z);

                // 其他Clip权重为0
                for (int i = 0; i < samples.Length; i++)
                {
                    if (i != tri.i0 && i != tri.i1 && i != tri.i2)
                        runtime.mixer.SetInputWeight(i, 0f);
                }
            }

            private bool IsPointInTriangle(Vector2 p, AnimationCalculatorRuntime.Triangle tri)
            {
                Vector3 bary = CalculateBarycentricCoordinates(p, tri.v0, tri.v1, tri.v2);
                return bary.x >= 0 && bary.y >= 0 && bary.z >= 0;
            }

            private Vector3 CalculateBarycentricCoordinates(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
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
        [Serializable]
        public class BlendTree2DFreeformCartesianCalculator : BlendTree2DCalculator
        {
            protected override void ComputeDelaunayTriangulation()
            {
                // 笛卡尔模式使用网格结构,不需要三角化
                _sharedTriangles = null;
            }

            protected override void CalculateWeights2D(AnimationCalculatorRuntime runtime, Vector2 input)
            {
                // 找到最近的4个点(形成矩形)
                FindClosestGridPoints(input, out int i0, out int i1, out int i2, out int i3);

                if (i0 < 0)
                {
                    // 找最近的单点
                    int nearest = FindNearestSample(input);
                    runtime.mixer.SetInputWeight(nearest, 1f);
                    for (int i = 0; i < samples.Length; i++)
                    {
                        if (i != nearest)
                            runtime.mixer.SetInputWeight(i, 0f);
                    }
                    return;
                }

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

                float w0 = (1 - tx) * (1 - ty);
                float w1 = tx * (1 - ty);
                float w2 = (1 - tx) * ty;
                float w3 = tx * ty;

                // 应用权重
                runtime.mixer.SetInputWeight(i0, w0);
                runtime.mixer.SetInputWeight(i1, w1);
                runtime.mixer.SetInputWeight(i2, w2);
                runtime.mixer.SetInputWeight(i3, w3);

                // 其他Clip权重为0
                for (int i = 0; i < samples.Length; i++)
                {
                    if (i != i0 && i != i1 && i != i2 && i != i3)
                        runtime.mixer.SetInputWeight(i, 0f);
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
        [Serializable]
        public class DirectBlendCalculator : AnimationClipPlayableCalculator
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

            public override void UpdateWeights(AnimationCalculatorRuntime runtime, StateContext context, float deltaTime)
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
