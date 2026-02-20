using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    /// <summary>
    /// 动画计算器统一运行时数据 - 商业级高性能实现
    /// 
    /// 设计原则：
    /// 1. 每个状态独占一个Runtime实例（不共享）
    /// 2. Runtime与Calculator绑定后不变
    /// 3. 支持运行时Clip覆盖，但索引保持固定
    /// 4. 零GC设计：所有数组预分配，对象池回收，避免动态分配
    /// 5. 所有Calculator类型共享此Runtime，简化类型转换
    /// 6. 支持IK/MatchTarget扩展（商业级特性）
    /// 
    /// 字段使用规则：
    /// - SimpleClip:    singlePlayable
    /// - BlendTree1D:   mixer, playables[], lastInput, inputVelocity
    /// - BlendTree2D:   mixer, playables[], lastInput2D, inputVelocity2D, triangles[]
    /// - DirectBlend:   mixer, playables[], currentWeights[], targetWeights[], weightVelocities[]
    /// - Sequential:    mixer, sequencePhaseIndex, sequencePhaseTime, phaseRuntimes[]
    /// - MixerWrapper:  childRuntime
    /// </summary>
    public class AnimationCalculatorRuntime : IPoolableAuto
    {
        public static bool debugMatchTarget = true;

        /// <summary>
        /// Runtime 所属的 StateBase（非序列化）。
        /// 用途：允许 Calculator 在低频事件时向 State 写入运行时信息（例如阶段同步）。
        /// 对象池回收时会被清空。
        /// </summary>
        [System.NonSerialized]
        public StateBase ownerState;
        // ==================== 对象池 ====================
        /// <summary>
        /// AnimationCalculatorRuntime 对象池
        /// 容量：200，预热：5
        /// </summary>
        public static readonly ESSimplePool<AnimationCalculatorRuntime> Pool = new ESSimplePool<AnimationCalculatorRuntime>(
            factoryMethod: () => new AnimationCalculatorRuntime(),
            resetMethod: (obj) => obj.OnResetAsPoolable(),
            initCount: 5,
            maxCount: -1,
            poolDisplayName: "AnimCalcRuntime Pool"
        );

        public bool IsRecycled { get; set; }

        public void OnResetAsPoolable()
        {
            // 注意：Reset不销毁Playable，仅重置数据状态
            // Playable销毁在Cleanup()中完成
            ResetDataOnly();
        }

        public void TryAutoPushedToPool()
        {
            if (!IsRecycled)
            {
                // ★ 不在这里设置 IsRecycled = true
                // PushToPool 内部流程：检查IsRecycled → resetMethod → 设置IsRecycled=true → 入栈
                // 如果这里提前设置，PushToPool会误判为"已回收"而拒绝入池
                Pool.PushToPool(this);
            }
        }
        // ==================== 初始化与类型标识 ====================

        /// <summary>
        /// 是否已初始化（绑定Calculator后为true）
        /// </summary>
        public bool IsInitialized;

        /// <summary>
        /// 绑定的计算器类型（用于类型安全的字段访问检查）
        /// </summary>
        public StateAnimationMixerKind BoundCalculatorKind;

        // ==================== 权重控制 ====================

        /// <summary>
        /// 当前总权重（用于外部淡入淡出/权重控制）
        /// </summary>
        public float totalWeight = 1f;

        /// <summary>
        /// 是否使用内部Mixer权重控制
        /// </summary>
        public bool usesInternalWeight = true;

        /// <summary>
        /// 最近一次应用到Mixer的总权重（避免重复SetInputWeight）
        /// </summary>
        public float lastAppliedTotalWeight = 1f;

        // ==================== 通用Playable ====================

        /// <summary>
        /// 输出Playable - 最终连接到Layer Mixer的节点
        /// 可能是singlePlayable、mixer、或LayerMixerPlayable（带AvatarMask时）
        /// </summary>
        public Playable outputPlayable;

        /// <summary>
        /// Mixer - 用于BlendTree和Direct（多输入混合）
        /// </summary>
        public AnimationMixerPlayable mixer;

        /// <summary>
        /// Clip数组 - 用于BlendTree和Direct
        /// 索引固定不变，支持运行时覆盖Clip内容
        /// </summary>
        public AnimationClipPlayable[] playables;

        /// <summary>
        /// 单个Clip - 用于SimpleClip
        /// </summary>
        public AnimationClipPlayable singlePlayable;

        // ==================== 1D混合树数据 ====================

        /// <summary>上一帧输入值（平滑用）</summary>
        public float lastInput;
        /// <summary>输入变化速度（SmoothDamp用）</summary>
        public float inputVelocity;

        // ==================== 2D混合树数据 ====================

        /// <summary>上一帧2D输入</summary>
        public Vector2 lastInput2D;
        /// <summary>2D输入变化速度</summary>
        public Vector2 inputVelocity2D;
        /// <summary>Delaunay三角形缓存（享元引用，不独占）</summary>
        public Triangle[] triangles;

        // ==================== Direct混合数据 ====================

        /// <summary>当前权重（Direct模式专用）</summary>
        public float[] currentWeights;
        /// <summary>目标权重（Direct模式专用）</summary>
        public float[] targetWeights;
        /// <summary>权重变化速度（SmoothDamp用）</summary>
        public float[] weightVelocities;

        // ==================== 权重缓存（BlendTree通用） ====================

        /// <summary>权重缓存（避免每帧查询Mixer）</summary>
        public float[] weightCache;
        /// <summary>目标权重缓存</summary>
        public float[] weightTargetCache;
        /// <summary>权重速度缓存（平滑用）</summary>
        public float[] weightVelocityCache;
        /// <summary>是否启用权重平滑</summary>
        public bool useSmoothing = true;

        // ==================== 嵌套支持 ====================

        /// <summary>
        /// 子Calculator的Runtime（MixerCalculator嵌套用）
        /// 推荐深度≤2层
        /// </summary>
        public AnimationCalculatorRuntime childRuntime;

        // ==================== 序列混合器数据（SequentialClipMixer） ====================

        /// <summary>当前序列阶段：0=Entry, 1=Main, 2=Exit</summary>
        public int sequencePhase;
        /// <summary>当前阶段已运行时间</summary>
        public float phaseStartTime;
        /// <summary>整个序列是否已完成</summary>
        public bool sequenceCompleted;

        // ==================== 序列阶段数据（SequentialStates高级序列） ====================

        /// <summary>当前序列阶段索引（SequentialStates专用，与sequencePhase分离避免冲突）</summary>
        public int sequencePhaseIndex;
        /// <summary>当前阶段已运行时间（SequentialStates专用）</summary>
        public float sequencePhaseTime;
        /// <summary>序列总运行时间</summary>
        public float sequenceTotalTime;
        /// <summary>上一个阶段索引（用于阶段切换过渡）</summary>
        public int sequencePrevPhase = -1;
        /// <summary>当前过渡已运行时间</summary>
        public float sequenceTransitionTime;
        /// <summary>是否处于阶段过渡中</summary>
        public bool sequenceInTransition;

        /// <summary>每个阶段的子Runtime（阶段内嵌套计算器用）</summary>
        public AnimationCalculatorRuntime[] phaseRuntimes;
        /// <summary>每个阶段的输出Playable</summary>
        public Playable[] phaseOutputs;
        /// <summary>每个阶段的本地Mixer（主/次Clip混合）</summary>
        public AnimationMixerPlayable[] phaseMixers;
        /// <summary>每个阶段的主Clip Playable</summary>
        public AnimationClipPlayable[] phasePrimaryPlayables;
        /// <summary>每个阶段的次Clip Playable</summary>
        public AnimationClipPlayable[] phaseSecondaryPlayables;
        /// <summary>每个阶段是否使用子计算器</summary>
        public bool[] phaseUsesCalculator;
        /// <summary>阶段内主次混合权重</summary>
        public float[] phaseBlendWeights;
        /// <summary>阶段内主次混合速度</summary>
        public float[] phaseBlendVelocities;

        // ==================== SequentialStates Mixer权重写入缓存（性能关键） ====================

        /// <summary>
        /// SequentialStates：最近一次写入到 runtime.mixer 的“当前阶段索引”。
        /// 用于将每帧 O(N) 的 SetInputWeight 降到仅更新当前/上一阶段（最多2个输入）。
        /// </summary>
        public int sequenceLastAppliedPhaseIndex = -1;

        /// <summary>
        /// SequentialStates：最近一次写入到 runtime.mixer 的“上一阶段索引”（过渡用）。
        /// </summary>
        public int sequenceLastAppliedPrevPhaseIndex = -1;

        /// <summary>SequentialStates：最近一次写入的当前阶段权重</summary>
        public float sequenceLastAppliedPhaseWeight = -1f;

        /// <summary>SequentialStates：最近一次写入的上一阶段权重</summary>
        public float sequenceLastAppliedPrevWeight = -1f;

        // ==================== IK支持（结构体合并，提升缓存命中率） ====================

        /// <summary>IK运行时数据（所有IK字段合并为单一结构体）</summary>
        public IKRuntimeData ik;

        // ==================== MatchTarget支持（结构体合并） ====================

        /// <summary>MatchTarget运行时数据（所有MT字段合并为单一结构体）</summary>
        public MatchTargetRuntimeData matchTarget;

        // ==================== IK结构体定义 ====================

        /// <summary>
        /// IK运行时数据结构体 - 合并22个字段为单一连续内存块
        /// 提升缓存命中率，减少类字段数量
        /// </summary>
        public struct IKRuntimeData
        {
            /// <summary>IK是否启用</summary>
            public bool enabled;
            /// <summary>IK总权重（0=纯动画, 1=纯IK）</summary>
            public float weight;
            /// <summary>IK目标权重（平滑过渡用）</summary>
            public float targetWeight;
            /// <summary>IK权重变化速度</summary>
            public float weightVelocity;

            // Per-limb权重
            public float leftHandWeight, rightHandWeight, leftFootWeight, rightFootWeight;
            /// <summary>注视IK权重</summary>
            public float lookAtWeight;

            // 目标位置
            public Vector3 leftHandPosition, rightHandPosition, leftFootPosition, rightFootPosition;
            /// <summary>注视目标位置</summary>
            public Vector3 lookAtPosition;

            // 目标旋转
            public Quaternion leftHandRotation, rightHandRotation, leftFootRotation, rightFootRotation;

            // 提示位置（肘/膝）
            public Vector3 leftHandHintPosition, rightHandHintPosition, leftFootHintPosition, rightFootHintPosition;

            /// <summary>重置所有IK数据到默认值</summary>
            public void Reset()
            {
                enabled = false;
                weight = 0f;
                targetWeight = 0f;
                weightVelocity = 0f;
                leftHandWeight = rightHandWeight = leftFootWeight = rightFootWeight = lookAtWeight = 0f;
                leftHandPosition = rightHandPosition = leftFootPosition = rightFootPosition = lookAtPosition = Vector3.zero;
                leftHandRotation = rightHandRotation = leftFootRotation = rightFootRotation = Quaternion.identity;
                leftHandHintPosition = rightHandHintPosition = leftFootHintPosition = rightFootHintPosition = Vector3.zero;
            }
        }

        // ==================== MatchTarget结构体定义 ====================

        /// <summary>
        /// MatchTarget运行时数据结构体 - 合并9个字段为单一连续内存块
        /// </summary>
        public struct MatchTargetRuntimeData
        {
            /// <summary>MatchTarget是否激活</summary>
            public bool active;
            /// <summary>MatchTarget目标位置</summary>
            public Vector3 position;
            /// <summary>MatchTarget目标旋转</summary>
            public Quaternion rotation;
            /// <summary>MatchTarget影响的身体部位掩码</summary>
            public AvatarTarget bodyPart;
            /// <summary>MatchTarget开始归一化时间</summary>
            public float startTime;
            /// <summary>MatchTarget结束归一化时间</summary>
            public float endTime;
            /// <summary>MatchTarget位置权重掩码（XYZ分别控制）</summary>
            public Vector3 positionWeight;
            /// <summary>MatchTarget旋转权重</summary>
            public float rotationWeight;
            /// <summary>MatchTarget是否完成</summary>
            public bool completed;

            /// <summary>重置所有MatchTarget数据到默认值</summary>
            public void Reset()
            {
                active = false;
                position = Vector3.zero;
                rotation = Quaternion.identity;
                bodyPart = AvatarTarget.Root;
                startTime = 0f;
                endTime = 1f;
                positionWeight = Vector3.one;
                rotationWeight = 1f;
                completed = false;
            }
        }

        // ==================== 三角形结构体 ====================

        public struct Triangle
        {
            public int i0, i1, i2;
            public Vector2 v0, v1, v2;
        }

        // ==================== 核心方法 ====================

        /// <summary>
        /// 清理所有Playable资源（销毁节点，释放引用）
        /// 注意：清理顺序很重要 —— 先断开子节点，再销毁父节点
        /// </summary>
        public void Cleanup()
        {
            // ★ 先清理子Runtime（递归深度优先）
            if (childRuntime != null)
            {
                childRuntime.Cleanup();
                childRuntime = null;
            }

            // 清理阶段子Runtime
            if (phaseRuntimes != null)
            {
                for (int i = 0; i < phaseRuntimes.Length; i++)
                {
                    if (phaseRuntimes[i] != null)
                    {
                        phaseRuntimes[i].Cleanup();
                        phaseRuntimes[i] = null;
                    }
                }
                phaseRuntimes = null;
            }

            // ★ 清理阶段Mixer中的子Playable（先断开再销毁）
            if (phaseMixers != null)
            {
                for (int i = 0; i < phaseMixers.Length; i++)
                {
                    if (phaseMixers[i].IsValid())
                    {
                        // 断开所有输入后再销毁
                        int inputCount = phaseMixers[i].GetInputCount();
                        for (int j = 0; j < inputCount; j++)
                        {
                            if (phaseMixers[i].GetInput(j).IsValid())
                                phaseMixers[i].GetGraph().Disconnect(phaseMixers[i], j);
                        }
                        phaseMixers[i].Destroy();
                    }
                }
                phaseMixers = null;
            }

            // 清理阶段PlayableClip（已从mixer断开，可安全销毁）
            DestroyClipPlayableArray(ref phasePrimaryPlayables);
            DestroyClipPlayableArray(ref phaseSecondaryPlayables);

            phaseOutputs = null;
            phaseUsesCalculator = null;
            phaseBlendWeights = null;
            phaseBlendVelocities = null;

            // ★ 清理主Mixer中的子Playable（先断开再销毁mixer）
            if (mixer.IsValid())
            {
                int inputCount = mixer.GetInputCount();
                for (int i = 0; i < inputCount; i++)
                {
                    if (mixer.GetInput(i).IsValid())
                        mixer.GetGraph().Disconnect(mixer, i);
                }
            }

            // 清理Playable数组（已从mixer断开）
            DestroyClipPlayableArray(ref playables);

            // 清理单个Playable
            if (singlePlayable.IsValid())
            {
                singlePlayable.Destroy();
                singlePlayable = default;
            }

            // ★ 最后销毁Mixer本身
            if (mixer.IsValid())
            {
                mixer.Destroy();
                mixer = default;
            }

            // 清理输出Playable引用（不销毁，因为它指向上面已销毁的节点）
            outputPlayable = Playable.Null;

            // 重置所有数据字段
            ResetDataOnly();

            IsInitialized = false;
        }

        /// <summary>
        /// 仅重置数据字段（不销毁Playable）
        /// 用于对象池回收时的快速重置
        /// </summary>
        private void ResetDataOnly()
        {
            ownerState = null;
            BoundCalculatorKind = StateAnimationMixerKind.Unknown;

            // 权重
            totalWeight = 1f;
            lastAppliedTotalWeight = 1f;
            usesInternalWeight = true;

            // 1D数据
            lastInput = 0f;
            inputVelocity = 0f;

            // 2D数据
            lastInput2D = Vector2.zero;
            inputVelocity2D = Vector2.zero;
            triangles = null;

            // Direct数据
            currentWeights = null;
            targetWeights = null;
            weightVelocities = null;

            // 权重缓存
            weightCache = null;
            weightTargetCache = null;
            weightVelocityCache = null;
            useSmoothing = true;

            // 序列数据（SequentialClipMixer）
            sequencePhase = 0;
            phaseStartTime = 0f;
            sequenceCompleted = false;

            // 序列数据（SequentialStates）
            sequencePhaseIndex = 0;
            sequencePhaseTime = 0f;
            sequenceTotalTime = 0f;
            sequencePrevPhase = -1;
            sequenceTransitionTime = 0f;
            sequenceInTransition = false;

            // SequentialStates：Mixer权重写入缓存
            sequenceLastAppliedPhaseIndex = -1;
            sequenceLastAppliedPrevPhaseIndex = -1;
            sequenceLastAppliedPhaseWeight = -1f;
            sequenceLastAppliedPrevWeight = -1f;

            // IK数据
            ik.Reset();

            // MatchTarget数据
            matchTarget.Reset();
        }

        /// <summary>
        /// 重置IK数据到默认值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetIKData()
        {
            ik.Reset();
        }

        /// <summary>
        /// 重置MatchTarget数据到默认值
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetMatchTargetData()
        {
            matchTarget.Reset();
        }

        /// <summary>
        /// 安全销毁一个AnimationClipPlayable数组中的所有有效Playable
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DestroyClipPlayableArray(ref AnimationClipPlayable[] array)
        {
            if (array == null) return;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].IsValid())
                    array[i].Destroy();
            }
            array = null;
        }

        // ==================== 查询方法 ====================

        /// <summary>
        /// 判断是否存在可承载内部权重的Mixer（含子Runtime）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasInternalWeightMixer()
        {
            if (mixer.IsValid())
                return true;

            if (childRuntime != null)
                return childRuntime.HasInternalWeightMixer();

            return false;
        }

        /// <summary>
        /// 获取输出Playable（优先返回缓存的outputPlayable）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Playable GetOutputPlayable()
        {
            if (outputPlayable.IsValid())
                return outputPlayable;
            if (mixer.IsValid())
                return mixer;
            if (singlePlayable.IsValid())
                return singlePlayable;
            return Playable.Null;
        }

        /// <summary>
        /// 检查Runtime是否处于有效可用状态
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            if (!IsInitialized) return false;

            // 至少有一个有效的Playable输出
            return singlePlayable.IsValid() || mixer.IsValid() || outputPlayable.IsValid();
        }

        /// <summary>
        /// 获取当前活跃的Playable数量（用于性能监控）
        /// </summary>
        public int GetActivePlayableCount()
        {
            int count = 0;
            if (singlePlayable.IsValid()) count++;
            if (mixer.IsValid()) count++;
            if (playables != null)
            {
                for (int i = 0; i < playables.Length; i++)
                    if (playables[i].IsValid()) count++;
            }
            if (phaseMixers != null)
            {
                for (int i = 0; i < phaseMixers.Length; i++)
                    if (phaseMixers[i].IsValid()) count++;
            }
            if (phasePrimaryPlayables != null)
            {
                for (int i = 0; i < phasePrimaryPlayables.Length; i++)
                    if (phasePrimaryPlayables[i].IsValid()) count++;
            }
            if (phaseSecondaryPlayables != null)
            {
                for (int i = 0; i < phaseSecondaryPlayables.Length; i++)
                    if (phaseSecondaryPlayables[i].IsValid()) count++;
            }
            if (childRuntime != null)
                count += childRuntime.GetActivePlayableCount();
            return count;
        }

        // ==================== IK便捷方法 ====================

        /// <summary>
        /// 设置IK目标（带平滑过渡）
        /// </summary>
        /// <param name="goal">IK目标类型</param>
        /// <param name="position">目标位置</param>
        /// <param name="rotation">目标旋转</param>
        /// <param name="weight">权重 (0-1)</param>
        public void SetIKGoal(IKGoal goal, Vector3 position, Quaternion rotation, float weight)
        {
            ik.enabled = true;
            switch (goal)
            {
                case IKGoal.LeftHand:
                    ik.leftHandPosition = position;
                    ik.leftHandRotation = rotation;
                    ik.leftHandWeight = Mathf.Clamp01(weight);
                    break;
                case IKGoal.RightHand:
                    ik.rightHandPosition = position;
                    ik.rightHandRotation = rotation;
                    ik.rightHandWeight = Mathf.Clamp01(weight);
                    break;
                case IKGoal.LeftFoot:
                    ik.leftFootPosition = position;
                    ik.leftFootRotation = rotation;
                    ik.leftFootWeight = Mathf.Clamp01(weight);
                    break;
                case IKGoal.RightFoot:
                    ik.rightFootPosition = position;
                    ik.rightFootRotation = rotation;
                    ik.rightFootWeight = Mathf.Clamp01(weight);
                    break;
            }
        }

        /// <summary>
        /// 设置IK提示位置（肘/膝方向）
        /// </summary>
        public void SetIKHintPosition(IKGoal goal, Vector3 position)
        {
            switch (goal)
            {
                case IKGoal.LeftHand:
                    ik.leftHandHintPosition = position;
                    break;
                case IKGoal.RightHand:
                    ik.rightHandHintPosition = position;
                    break;
                case IKGoal.LeftFoot:
                    ik.leftFootHintPosition = position;
                    break;
                case IKGoal.RightFoot:
                    ik.rightFootHintPosition = position;
                    break;
            }
        }

        /// <summary>
        /// 设置注视目标
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetLookAtTarget(Vector3 position, float weight)
        {
            ik.enabled = true;
            ik.lookAtPosition = position;
            ik.lookAtWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// 平滑更新IK权重（每帧调用）
        /// </summary>
        public void UpdateIKWeight(float smoothTime, float deltaTime)
        {
            if (!ik.enabled) return;

            if (smoothTime > 0.001f)
            {
                ik.weight = Mathf.SmoothDamp(ik.weight, ik.targetWeight, ref ik.weightVelocity, smoothTime, float.MaxValue, deltaTime);
            }
            else
            {
                ik.weight = ik.targetWeight;
            }
        }

        // ==================== MatchTarget便捷方法 ====================

        /// <summary>
        /// 启动MatchTarget（根动作对齐）
        /// </summary>
        public void StartMatchTarget(Vector3 targetPos, Quaternion targetRot, AvatarTarget bodyPart,
            float startNormTime, float endNormTime, Vector3 posWeight, float rotWeight = 1f)
        {
            matchTarget.active = true;
            matchTarget.completed = false;
            matchTarget.position = targetPos;
            matchTarget.rotation = targetRot;
            matchTarget.bodyPart = bodyPart;
            matchTarget.startTime = Mathf.Clamp01(startNormTime);
            matchTarget.endTime = Mathf.Clamp01(endNormTime);
            matchTarget.positionWeight = posWeight;
            matchTarget.rotationWeight = Mathf.Clamp01(rotWeight);

#if UNITY_EDITOR
            if (debugMatchTarget)
            {
                Debug.Log(
                    $"[MatchTargetRuntime] Start | pos={targetPos:F3} rot={targetRot.eulerAngles:F1} " +
                    $"body={bodyPart} time=[{matchTarget.startTime:F2},{matchTarget.endTime:F2}] " +
                    $"posW={posWeight:F2} rotW={matchTarget.rotationWeight:F2}");
            }
#endif
        }

        /// <summary>
        /// 检查MatchTarget是否在有效时间范围内
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMatchTargetInRange(float normalizedTime)
        {
            bool inRange = matchTarget.active && !matchTarget.completed
                && normalizedTime >= matchTarget.startTime
                && normalizedTime <= matchTarget.endTime;
#if UNITY_EDITOR
            if (debugMatchTarget && !inRange)
            {
                Debug.Log(
                    $"[MatchTargetRuntime] Gate: out of range | active={matchTarget.active} completed={matchTarget.completed} " +
                    $"t={normalizedTime:F2} range=[{matchTarget.startTime:F2},{matchTarget.endTime:F2}]");
            }
#endif
            return inRange;
        }

        /// <summary>
        /// 标记MatchTarget完成
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CompleteMatchTarget()
        {
            matchTarget.completed = true;
            matchTarget.active = false;
#if UNITY_EDITOR
            if (debugMatchTarget)
            {
                Debug.Log("[MatchTargetRuntime] Complete");
            }
#endif
        }

        // ==================== 内存统计 ====================

        /// <summary>
        /// 获取当前使用的内存大小 (字节，近似值)
        /// </summary>
        public int GetMemoryFootprint()
        {
            int size = 0;

            // 固定字段开销（bool/float/Vector/Quaternion等）
            // IsInitialized(1) + BoundCalculatorKind(4) + totalWeight(4) + usesInternalWeight(1) + lastApplied(4)
            size += 14;
            // 1D: lastInput(4) + inputVelocity(4)
            size += 8;
            // 2D: lastInput2D(8) + inputVelocity2D(8)
            size += 16;
            // Sequence: sequencePhase(4) + phaseStartTime(4) + sequenceCompleted(1) + ...
            size += 25;
            // IK: ~180 bytes (floats + vectors + quaternions)
            size += 180;
            // MatchTarget: ~80 bytes
            size += 80;

            // Playable引用 (每个PlayableHandle约16字节)
            size += 16; // singlePlayable
            size += 16; // mixer
            size += 16; // outputPlayable
            if (playables != null)
                size += playables.Length * 16 + 16; // array + header
            if (phasePrimaryPlayables != null)
                size += phasePrimaryPlayables.Length * 16 + 16;
            if (phaseSecondaryPlayables != null)
                size += phaseSecondaryPlayables.Length * 16 + 16;
            if (phaseMixers != null)
                size += phaseMixers.Length * 16 + 16;

            // float数组
            size += GetArraySize(currentWeights);
            size += GetArraySize(targetWeights);
            size += GetArraySize(weightVelocities);
            size += GetArraySize(weightCache);
            size += GetArraySize(weightTargetCache);
            size += GetArraySize(weightVelocityCache);
            size += GetArraySize(phaseBlendWeights);
            size += GetArraySize(phaseBlendVelocities);

            // Triangle数组 (每个Triangle: 3*int(12) + 3*Vector2(24) = 36)
            if (triangles != null)
                size += triangles.Length * 36 + 16;

            // 子Runtime
            if (childRuntime != null)
                size += childRuntime.GetMemoryFootprint();

            // phaseRuntimes
            if (phaseRuntimes != null)
            {
                size += 16; // array header
                for (int i = 0; i < phaseRuntimes.Length; i++)
                {
                    if (phaseRuntimes[i] != null)
                        size += phaseRuntimes[i].GetMemoryFootprint();
                }
            }

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetArraySize(float[] arr)
        {
            return arr != null ? arr.Length * 4 + 16 : 0; // data + array header
        }

        // ==================== 调试 ====================

#if UNITY_EDITOR
        /// <summary>
        /// 获取调试信息字符串
        /// </summary>
        public string GetDebugSummary()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.Append($"[Runtime] Kind={BoundCalculatorKind} Init={IsInitialized} Valid={IsValid()}");
            sb.Append($" | Weight={totalWeight:F2} Smoothing={useSmoothing}");
            sb.Append($" | Playables={GetActivePlayableCount()}");

            if (ik.enabled)
                sb.Append($" | IK={ik.weight:F2}");
            if (matchTarget.active)
                sb.Append($" | MT={matchTarget.bodyPart} [{matchTarget.startTime:F2}-{matchTarget.endTime:F2}]");

            sb.Append($" | Mem={GetMemoryFootprint()}B");
            return sb.ToString();
        }
#endif
    }
}
