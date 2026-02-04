using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
    #region 辅助结构定义

    /// <summary>
    /// 状态激活测试结果 - 描述状态能否激活以及需要执行的操作
    /// </summary>
    [Serializable]
    public struct StateActivationResult
    {
        /// <summary>
        /// 是否可以激活
        /// </summary>
        public bool canActivate;

        /// <summary>
        /// 是否需要打断当前状态
        /// </summary>
        public bool requiresInterruption;

        /// <summary>
        /// 需要打断的状态列表
        /// </summary>
        public List<StateBase> statesToInterrupt;

        /// <summary>
        /// 是否可以直接合并（多个状态并行）
        /// </summary>
        public bool canMerge;

        /// <summary>
        /// 是否可以直接合并（无需打断）
        /// </summary>
        public bool mergeDirectly;

        /// <summary>
        /// 可以合并的状态列表
        /// </summary>
        public List<StateBase> statesToMergeWith;

        /// <summary>
        /// 打断数量（便于快速判断）
        /// </summary>
        public int interruptCount;

        /// <summary>
        /// 合并数量（便于快速判断）
        /// </summary>
        public int mergeCount;

        /// <summary>
        /// 失败原因
        /// </summary>
        public string failureReason;

        /// <summary>
        /// 目标流水线
        /// </summary>
        public StatePipelineType targetPipeline;

        /// <summary>
        /// 注意：statesToInterrupt / statesToMergeWith 为内部复用列表引用，不建议外部长期持有。
        /// </summary>

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static StateActivationResult Success(StatePipelineType pipeline, bool merge = false)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = merge,
                mergeDirectly = merge,
                statesToInterrupt = new List<StateBase>(),
                statesToMergeWith = new List<StateBase>(),
                interruptCount = 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建打断结果
        /// </summary>
        public static StateActivationResult Interrupt(StatePipelineType pipeline, List<StateBase> toInterrupt)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = true,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = toInterrupt,
                statesToMergeWith = new List<StateBase>(),
                interruptCount = toInterrupt?.Count ?? 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建合并结果
        /// </summary>
        public static StateActivationResult Merge(StatePipelineType pipeline, List<StateBase> mergeWith)
        {
            return new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = true,
                mergeDirectly = true,
                statesToInterrupt = new List<StateBase>(),
                statesToMergeWith = mergeWith,
                interruptCount = 0,
                mergeCount = mergeWith?.Count ?? 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static StateActivationResult Failure(string reason)
        {
            return new StateActivationResult
            {
                canActivate = false,
                requiresInterruption = false,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = new List<StateBase>(),
                statesToMergeWith = new List<StateBase>(),
                interruptCount = 0,
                mergeCount = 0,
                failureReason = reason,
                targetPipeline = StatePipelineType.Basic
            };
        }
    }

    /// <summary>
    /// 流水线数据 - 管理单个流水线中的状态
    /// </summary>
    public class StatePipelineRuntime
    {
        /// <summary>
        /// 所属状态机引用
        /// </summary>
        [NonSerialized]
        public StateMachine stateMachine;

        [LabelText("流水线类型")]
        public StatePipelineType pipelineType;

        [LabelText("主状态"), ShowInInspector, ReadOnly]

        public StateBase mainState;

        [LabelText("空转反馈状态"), ShowInInspector]
        public StateBase feedbackState;

        [LabelText("当前运行状态集合"), ShowInInspector, ReadOnly]
        [NonSerialized]
        public HashSet<StateBase> runningStates = new HashSet<StateBase>();

        [LabelText("流水线权重"), Range(0f, 1f)]
        public float weight = 1f;

        [LabelText("是否启用")]
        public bool isEnabled = true;

        [LabelText("优先级"), Tooltip("数值越大优先级越高")]
        public int priority = 0;

        /// <summary>
        /// Playable混合器 - 该流水线的动画混合器
        /// </summary>
        [NonSerialized]
        public AnimationMixerPlayable mixer;

        /// <summary>
        /// 该流水线在RootMixer中的输入索引
        /// </summary>
        [NonSerialized]
        public int rootInputIndex = -1;

        /// <summary>
        /// Playable槽位池 - 记录空闲的输入索引（用于复用）
        /// </summary>
        [NonSerialized]
        public Stack<int> freeSlots = new Stack<int>(64);

        /// <summary>
        /// 状态到输入索引的映射 - 用于快速查找和卸载
        /// </summary>
        [NonSerialized,ShowInInspector]
        public Dictionary<StateBase, int> stateToSlotMap = new Dictionary<StateBase, int>(64);

        /// <summary>
        /// 正在淡入的状态字典 - 用于淡入效果更新
        /// </summary>
        [NonSerialized]
        public Dictionary<StateBase, FadeData> fadeInStates = new Dictionary<StateBase, FadeData>();

        /// <summary>
        /// 正在淡出的状态字典 - 用于淡出效果更新
        /// </summary>
        [NonSerialized]
        public Dictionary<StateBase, FadeData> fadeOutStates = new Dictionary<StateBase, FadeData>();

        /// <summary>
        /// 最大预分配槽位数 - 避免无限增长
        /// </summary>
        [LabelText("最大Playable槽位")]
        public int maxPlayableSlots = 32;

        // ===== FallBack多通道系统（每个流水线独立配置）=====
        // 当流水线运行状态为空时，使用FallBack作为默认回退状态
        // 使用int映射到具体的状态ID，每个Channel对应不同流水线的FallBack
        // 玩家可自定义：Channel0=地面Idle，Channel1=空中Idle，Channel2=水下Idle，Channel3=载具Idle，Channel4=特殊Idle
        // 默认使用Channel0，最多支持5个独立流水线的FallBack配置
        [LabelText("默认FallBack通道")]
        public int DefaultFallBackChannel = 0; // 默认FallBack通道索引
        [LabelText("Channel0 FallBack")]
        public int FallBackForChannel0 = -1; // 默认通道FallBack状态ID（-1表示无FallBack）
        [LabelText("Channel1 FallBack")]
        public int FallBackForChannel1 = -1; // 扩展通道1 FallBack状态ID
        [LabelText("Channel2 FallBack")]
        public int FallBackForChannel2 = -1; // 扩展通道2 FallBack状态ID
        [LabelText("Channel3 FallBack")]
        public int FallBackForChannel3 = -1; // 扩展通道3 FallBack状态ID
        [LabelText("Channel4 FallBack")]
        public int FallBackForChannel4 = -1; // 扩展通道4 FallBack状态ID

        // ===== Dirty机制（流水线级别的脏标记）=====
        /// <summary>
        /// Dirty等级（用于标记流水线需要更新，Update时根据此等级执行不同任务）
        /// 使用 MarkDirty(level) 来标记：
        ///   level > 0: 标记为Dirty，取当前值和新值的较大值
        ///   level <= 0: 清除Dirty（0=清除，-1=Apply后清除）
        /// 
        /// 建议的等级划分（可自定义）：
        ///   3 = 重要状态变更（如状态激活/停用）
        ///   2 = 中等变更（如热插拔操作）
        ///   1 = 轻度变更（如自动衰减）
        /// 
        /// Update时根据等级执行任务：
        ///   if (dirtyLevel >= 3) { 执行高优先级任务... }
        ///   if (dirtyLevel >= 1) { 执行FallBack自动激活等任务... }
        /// </summary>
        [LabelText("Dirty等级"), ShowInInspector, ReadOnly]
        [NonSerialized]
        public int dirtyLevel = 0;

        /// <summary>
        /// 上次Dirty时间（用于自动衰减）
        /// </summary>
        [NonSerialized]
        private float lastDirtyTime = 0f;
        
        /// <summary>
        /// 是否为Dirty状态
        /// </summary>
        public bool IsDirty => dirtyLevel > 0;

        /// <summary>
        /// 流水线是否有活动状态
        /// </summary>
        public bool HasActiveStates => runningStates.Count > 0;

        public StatePipelineRuntime(StatePipelineType type, StateMachine machine)
        {
            pipelineType = type;
            stateMachine = machine;
            runningStates = new HashSet<StateBase>();
        }

        /// <summary>
        /// 更新流水线权重到根部Mixer
        /// </summary>
        public void UpdatePipelineMixer()
        {
            if (stateMachine == null || !stateMachine.IsPlayableGraphValid) return;

            var rootMixer = stateMachine.rootMixer;
            if (!rootMixer.IsValid()) return;

            if (rootInputIndex >= 0 && rootInputIndex < rootMixer.GetInputCount())
            {
                rootMixer.SetInputWeight(rootInputIndex, weight);
            }
        }

        /// <summary>
        /// 激活状态并加载到Mixer
        /// </summary>
        public bool ActivateState(StateBase state)
        {
            if (state == null || stateMachine == null) return false;

            if (runningStates.Contains(state)) return false;

            // 激活状态
            runningStates.Add(state);
            stateMachine.runningStates.Add(state);

            // 热插拔到Playable图
            if (stateMachine.IsPlayableGraphValid && mixer.IsValid())
            {
                stateMachine.HotPlugStateToPlayable(state, this);
            }

            return true;
        }

        /// <summary>
        /// 停用状态并从 Mixer卸载
        /// </summary>
        public bool DeactivateState(StateBase state)
        {
            if (state == null || stateMachine == null) return false;

            if (!runningStates.Contains(state)) return false;

            // 热拔插从Pla<b图
            if (stateMachine.IsPlayableGraphValid && mixer.IsValid())
            {
                stateMachine.HotUnplugStateFromPlayable(state, this);
            }

            // 停用状态
            runningStates.Remove(state);
            stateMachine.runningStates.Remove(state);

            return true;
        }

        /// <summary>
        /// 获取指定通道的FallBack状态ID
        /// 当流水线运行状态为空时，返回此状态ID作为默认回退状态
        /// </summary>
        /// <param name="channel">通道索引（0-4），传入-1使用DefaultFallBackChannel</param>
        /// <returns>FallBack状态ID，-1表示无FallBack</returns>
        public int GetFallBack(int channel = -1)
        {
            // 如果传入-1，使用默认通道
            int originalChannel = channel;
            if (channel < 0)
                channel = DefaultFallBackChannel;
            
            int result;
            switch (channel)
            {
                case 0: result = FallBackForChannel0; break;
                case 1: result = FallBackForChannel1; break;
                case 2: result = FallBackForChannel2; break;
                case 3: result = FallBackForChannel3; break;
                case 4: result = FallBackForChannel4; break;
                default: 
                    Debug.LogWarning($"[FallBack-Get] ⚠ [{pipelineType}] 通道索引超出范围({channel})，回退到Channel0");
                    result = FallBackForChannel0;
                    break;
            }
            
            Debug.Log($"[FallBack-Get] [{pipelineType}] GetFallBack(原始Channel={originalChannel}, 实际Channel={channel}) -> StateID={result}");
            return result;
        }
        
        /// <summary>
        /// 设置指定通道的FallBack状态ID
        /// </summary>
        /// <param name="stateID">状态ID，-1表示无FallBack</param>
        /// <param name="channel">通道索引（0-4），传入-1使用DefaultFallBackChannel</param>
        public void SetFallBack(int stateID, int channel = -1)
        {
            // 如果传入-1，使用默认通道
            int originalChannel = channel;
            if (channel < 0)
                channel = DefaultFallBackChannel;
            
            Debug.Log($"[FallBack-Set] [{pipelineType}] SetFallBack(StateID={stateID}, 原始Channel={originalChannel}, 实际Channel={channel})");
            
            switch (channel)
            {
                case 0: 
                    FallBackForChannel0 = stateID;
                    Debug.Log($"[FallBack-Set]   Ch0: {stateID}");
                    break;
                case 1: 
                    FallBackForChannel1 = stateID;
                    Debug.Log($"[FallBack-Set]   Ch1: {stateID}");
                    break;
                case 2: 
                    FallBackForChannel2 = stateID;
                    Debug.Log($"[FallBack-Set]   Ch2: {stateID}");
                    break;
                case 3: 
                    FallBackForChannel3 = stateID;
                    Debug.Log($"[FallBack-Set]   Ch3: {stateID}");
                    break;
                case 4: 
                    FallBackForChannel4 = stateID;
                    Debug.Log($"[FallBack-Set]   Ch4: {stateID}");
                    break;
                default:
                    Debug.LogError($"[FallBack-Set] ✗ [{pipelineType}] 无效的通道索引: {channel}");
                    break;
            }
        }
        
        /// <summary>
        /// 设置默认FallBack通道索引
        /// </summary>
        /// <param name="channel">通道索引（0-4）</param>
        public void SetFallBackChannel(int channel)
        {
            if (channel >= 0 && channel <= 4)
                DefaultFallBackChannel = channel;
        }
        
        /// <summary>
        /// 获取当前默认FallBack通道索引
        /// </summary>
        public int GetFallBackChannel()
        {
            return DefaultFallBackChannel;
        }
        
        /// <summary>
        /// 检查指定通道是否配置了FallBack状态
        /// </summary>
        /// <param name="channel">通道索引（0-4），传入-1使用DefaultFallBackChannel</param>
        public bool HasFallBack(int channel = -1)
        {
            return GetFallBack(channel) >= 0;
        }

        /// <summary>
        /// 标记Dirty状态（通用方法，可在任何需要标记流水线变更的地方调用）
        /// level > 0: 标记为Dirty，取当前值和新值的较大值（高优先级不被低优先级覆盖）
        /// level <= 0: 清除Dirty（0=清除，-1=Apply后清除）
        /// 
        /// 使用示例：
        ///   MarkDirty(3);  // 标记为等级3的Dirty
        ///   MarkDirty(1);  // 如果当前已经是3，则保持3；如果是0，则变为1
        ///   MarkDirty(0);  // 清除Dirty
        /// </summary>
        public void MarkDirty(int level)
        {
            if (level > 0)
            {
                // 取最大值
                if (level > dirtyLevel)
                {
                    dirtyLevel = level;
                    lastDirtyTime = Time.time;
                    Debug.Log($"[Pipeline-Dirty] [{pipelineType}] Dirty等级: {dirtyLevel}");
                }
            }
            else
            {
                // 关闭/应用Dirty
                Debug.Log($"[Pipeline-Dirty] [{pipelineType}] Dirty已应用/关闭 (旧等级={dirtyLevel})");
                dirtyLevel = 0;
            }
        }

        /// <summary>
        /// 更新Dirty自动标记（每秒自动Dirty=1，用于持续触发低优先级任务检查）
        /// 在StateMachine的Update中调用，确保流水线定期检查FallBack等低优先级任务
        /// 
        /// 工作原理：
        /// - 如果 dirtyLevel > 1（2或3），经过1秒后降级到1
        /// - 如果 dirtyLevel = 1，保持不变
        /// - 如果 dirtyLevel = 0，不会自动变为1（需要手动MarkDirty）
        /// 
        /// 用途：保持最低Dirty等级1，确保FallBack等任务能够持续检查
        /// </summary>
        public void UpdateDirtyDecay()
        {
            if (dirtyLevel > 1)
            {
                float elapsed = Time.time - lastDirtyTime;
                if (elapsed >= 1.0f)
                {
                    // 降级到等级1（保持最低Dirty等级）
                    MarkDirty(1);
                }
            }
        }

        /// <summary>
        /// 获取Mixer连接详细信息
        /// </summary>
        public string GetMixerConnectionInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"╔═══════════════════════════════════════════════════════════════╗");
            sb.AppendLine($"║  [{pipelineType}] 流水线连接信息");
            sb.AppendLine($"╚═══════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            // 基本信息
            sb.AppendLine($"┌─ 基本配置 ────────────────────────────────────────────────┐");
            sb.AppendLine($"│  流水线类型: {pipelineType}");
            sb.AppendLine($"│  权重: {weight:F3} | 启用: {(isEnabled ? "✓" : "✗")} | 优先级: {priority}");
            sb.AppendLine($"│  FallBack通道: Ch{DefaultFallBackChannel} | Ch0={FallBackForChannel0}, Ch1={FallBackForChannel1}, Ch2={FallBackForChannel2}, Ch3={FallBackForChannel3}, Ch4={FallBackForChannel4}");
            sb.AppendLine($"│  主状态: {(mainState != null ? $"{mainState.strKey} (ID:{mainState.intKey})" : "无")}");
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // RootMixer连接状态
            sb.AppendLine($"┌─ RootMixer连接 ───────────────────────────────────────────┐");
            if (stateMachine != null && stateMachine.rootMixer.IsValid())
            {
                var rootMixer = stateMachine.rootMixer;
                bool isConnected = rootInputIndex >= 0 && rootInputIndex < rootMixer.GetInputCount();
                sb.AppendLine($"│  状态: {(isConnected ? "✓ 已连接" : "✗ 未连接")}");
                sb.AppendLine($"│  索引: {rootInputIndex}");
                if (isConnected)
                {
                    float actualWeight = rootMixer.GetInputWeight(rootInputIndex);
                    sb.AppendLine($"│  实际权重: {actualWeight:F3} {(Mathf.Approximately(actualWeight, weight) ? "" : $"(配置: {weight:F3})")}");
                    var input = rootMixer.GetInput(rootInputIndex);
                    sb.AppendLine($"│  输入Playable: {(input.IsValid() ? $"有效 ({input.GetPlayableType().Name})" : "无效")}");
                }
            }
            else
            {
                sb.AppendLine($"│  状态: ✗ StateMachine或RootMixer无效");
            }
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // Mixer自身信息
            sb.AppendLine($"┌─ 流水线Mixer ─────────────────────────────────────────────┐");
            if (mixer.IsValid())
            {
                sb.AppendLine($"│  状态: ✓ 有效");
                sb.AppendLine($"│  输入槽位数: {mixer.GetInputCount()} / {maxPlayableSlots} (最大)");
                sb.AppendLine($"│  已使用槽位: {stateToSlotMap.Count}");
                sb.AppendLine($"│  空闲槽位数: {freeSlots.Count}");
            }
            else
            {
                sb.AppendLine($"│  状态: ✗ 无效");
            }
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // 运行状态列表
            sb.AppendLine($"┌─ 运行状态 ({runningStates.Count}) ─────────────────────────────────────────┐");
            if (runningStates.Count > 0)
            {
                int index = 0;
                foreach (var state in runningStates)
                {
                    string prefix = index == runningStates.Count - 1 ? "└─" : "├─";
                    string stateInfo = $"{state.strKey} (ID:{state.intKey})";
                    
                    // 检查是否有动画连接
                    bool hasAnimation = state.stateSharedData?.hasAnimation ?? false;
                    string animStatus = hasAnimation ? "有动画" : "无动画";
                    
                    // 检查槽位映射
                    string slotInfo = "";
                    if (hasAnimation && stateToSlotMap.TryGetValue(state, out int slot))
                    {
                        if (mixer.IsValid() && slot < mixer.GetInputCount())
                        {
                            float slotWeight = mixer.GetInputWeight(slot);
                            var slotInput = mixer.GetInput(slot);
                            slotInfo = $"→ Slot[{slot}] 权重:{slotWeight:F3} {(slotInput.IsValid() ? "✓" : "✗")}";
                        }
                        else
                        {
                            slotInfo = $"→ Slot[{slot}] ⚠无效";
                        }
                    }
                    else if (hasAnimation)
                    {
                        slotInfo = "→ ⚠未映射";
                    }

                    sb.AppendLine($"│ {prefix} {stateInfo} [{animStatus}] {slotInfo}");
                    index++;
                }
            }
            else
            {
                sb.AppendLine($"│  (空闲)");
            }
            sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            sb.AppendLine();

            // 槽位详细映射
            if (stateToSlotMap.Count > 0)
            {
                sb.AppendLine($"┌─ 槽位映射详情 ({stateToSlotMap.Count}) ──────────────────────────────────┐");
                var sortedSlots = stateToSlotMap.OrderBy(kvp => kvp.Value);
                foreach (var kvp in sortedSlots)
                {
                    var state = kvp.Key;
                    var slot = kvp.Value;
                    string slotStatus = "✓";
                    string weightInfo = "";
                    
                    if (mixer.IsValid() && slot < mixer.GetInputCount())
                    {
                        var input = mixer.GetInput(slot);
                        if (!input.IsValid())
                        {
                            slotStatus = "✗ 断开";
                        }
                        else
                        {
                            float w = mixer.GetInputWeight(slot);
                            weightInfo = $"权重:{w:F3}";
                        }
                    }
                    else
                    {
                        slotStatus = "⚠ 越界";
                    }
                    
                    sb.AppendLine($"│  Slot[{slot,2}] {slotStatus} ← {state.strKey,-20} {weightInfo}");
                }
                sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
                sb.AppendLine();
            }

            // 空闲槽位池
            if (freeSlots.Count > 0)
            {
                sb.AppendLine($"┌─ 空闲槽位池 ({freeSlots.Count}) ──────────────────────────────────────┐");
                var freeArray = freeSlots.ToArray();
                sb.Append($"│  ");
                for (int i = 0; i < freeArray.Length; i++)
                {
                    sb.Append($"[{freeArray[i]}]");
                    if ((i + 1) % 10 == 0 && i < freeArray.Length - 1)
                    {
                        sb.AppendLine();
                        sb.Append($"│  ");
                    }
                    else if (i < freeArray.Length - 1)
                    {
                        sb.Append(" ");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"└───────────────────────────────────────────────────────────┘");
            }

            return sb.ToString();
        }

#if UNITY_EDITOR
        [Button("输出Mixer连接信息", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintMixerConnection()
        {
            UnityEngine.Debug.Log(GetMixerConnectionInfo());
        }
#endif
    }

    /// <summary>
    /// 状态退出测试结果
    /// </summary>
    [Serializable]
    public struct StateExitResult
    {
        public bool canExit;
        public string failureReason;
        public StatePipelineType pipeline;

        public static StateExitResult Success(StatePipelineType pipelineType)
        {
            return new StateExitResult { canExit = true, failureReason = string.Empty, pipeline = pipelineType };
        }

        public static StateExitResult Failure(string reason, StatePipelineType pipelineType)
        {
            return new StateExitResult { canExit = false, failureReason = reason, pipeline = pipelineType };
        }
    }

    /// <summary>
    /// 淡入淡出数据 - 用于动画权重淡入淡出效果
    /// </summary>
    public class FadeData
    {
        /// <summary>
        /// 已经过的时间
        /// </summary>
        public float elapsedTime;

        /// <summary>
        /// 淡入淡出持续时间
        /// </summary>
        public float duration;

        /// <summary>
        /// Playable槽位索引
        /// </summary>
        public int slotIndex;

        /// <summary>
        /// 起始权重（用于淡出）
        /// </summary>
        public float startWeight = 1f;
    }

    #endregion

    /// <summary>
    /// 状态机基类 - 专为Entity提供的高性能并行状态管理系统
    /// 设计思路参考UE的状态机，支持流水线、并行状态、动画混合等高级特性
    /// 功能完备，无需子类重写核心逻辑
    /// </summary>
    [Serializable]
    public class StateMachine
    {
        #region 核心引用与宿主

        /// <summary>
        /// 宿主Entity - 状态机所属的实体对象
        /// </summary>
        [NonSerialized]
        public Entity hostEntity;

        /// <summary>
        /// 状态机唯一标识键
        /// </summary>
        [LabelText("状态机键"), ShowInInspector]
        public string stateMachineKey;

        /// <summary>
        /// 状态上下文 - 统一管理运行时数据、参数、标记等（整合了原StateMachineContext）
        /// </summary>
        [LabelText("状态上下文"), ShowInInspector]
        [NonSerialized]
        public StateMachineContext stateContext;

        /// <summary>
        /// 是否持续输出统计信息（用于调试）
        /// </summary>
        [LabelText("持续输出统计"), Tooltip("每帧在Console输出状态机统计信息")]
        [NonSerialized]
        public bool enableContinuousStats = false;

        #endregion

        #region 扩展回调与策略

        /// <summary>
        /// 自定义激活测试（若返回非默认值将覆盖内置逻辑）
        /// </summary>
        public Func<StateBase, StatePipelineType, StateActivationResult> CustomActivationTest;

        /// <summary>
        /// 自定义合并判定
        /// </summary>
        public Func<StateBase, StateBase, bool> CanMergeEvaluator;

        /// <summary>
        /// 状态进入回调
        /// </summary>
        public Action<StateBase, StatePipelineType> OnStateEntered;

        /// <summary>
        /// 状态退出回调
        /// </summary>
        public Action<StateBase, StatePipelineType> OnStateExited;

        /// <summary>
        /// 流水线初始化回调
        /// </summary>
        public Action<StatePipelineRuntime> OnPipelineInitialized;

        /// <summary>
        /// 自定义退出测试
        /// </summary>
        public Func<StateBase, StatePipelineType, StateExitResult> CustomExitTest;

        /// <summary>
        /// 自定义通道占用计算
        /// </summary>
        public Func<IEnumerable<StateBase>, StateChannelMask> CustomChannelMaskEvaluator;

        /// <summary>
        /// 自定义代价计算
        /// </summary>
        public Func<IEnumerable<StateBase>, StateCostSummary> CustomCostEvaluator;

        #endregion

        #region 生命周期状态

        /// <summary>
        /// 状态机是否正在运行
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("运行状态")]
        public bool isRunning { get; protected set; }

        /// <summary>
        /// 状态机是否已初始化
        /// </summary>
        [NonSerialized]
        protected bool isInitialized = false;

        #endregion

        #region 流水线与并行状态管理

        /// <summary>
        /// 所有运行中的状态集合 - 支持多状态并行
        /// </summary>
        [ShowInInspector, ReadOnly, LabelText("当前运行状态")]
        [NonSerialized]
        public HashSet<StateBase> runningStates = new HashSet<StateBase>();

        /// <summary>
        /// String键到状态的映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("String映射")]
        [SerializeReference]
        public Dictionary<string, StateBase> stringToStateMap = new Dictionary<string, StateBase>();

        /// <summary>
        /// Int键到状态的映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("Int映射")]
        [SerializeReference]
        public Dictionary<int, StateBase> intToStateMap = new Dictionary<int, StateBase>();

        /// <summary>
        /// 状态归属流水线映射
        /// </summary>
        [ShowInInspector, FoldoutGroup("状态字典"), LabelText("状态管线映射")]
        [NonSerialized]
        public Dictionary<StateBase, StatePipelineType> statePipelineMap = new Dictionary<StateBase, StatePipelineType>();

        [NonSerialized]
        private readonly List<StateBase> _tmpStateBuffer = new List<StateBase>(16);

        [NonSerialized]
        private readonly List<StateBase> _tmpInterruptStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly List<StateBase> _tmpMergeStates = new List<StateBase>(8);

        [NonSerialized]
        private readonly Dictionary<StateBase, StateActivationCache> _activationCache = new Dictionary<StateBase, StateActivationCache>(64);

        [NonSerialized]
        private int _dirtyVersion = 0;

        [NonSerialized]
        private StateDirtyReason _lastDirtyReason = StateDirtyReason.Unknown;

        /// <summary>
        /// 自动分配ID的起始值（避免与预设ID冲突）
        /// </summary>
        [NonSerialized]
        private int _nextAutoIntId = 10000;

        [NonSerialized]
        private int _nextAutoStringIdSuffix = 1;

        [NonSerialized]
        private StateChannelMask _cachedChannelMask = StateChannelMask.None;

        [NonSerialized]
        private int _cachedChannelMaskVersion = -1;

        [NonSerialized]
        private StateCostSummary _cachedCostSummary;

        [NonSerialized]
        private int _cachedCostVersion = -1;

        /// <summary>
        /// 默认状态键 - 状态机启动时进入的状态
        /// </summary>
        [LabelText("默认状态键"), ValueDropdown("GetAllStateKeys")]
        public string defaultStateKey;

        #endregion

        #region 流水线直接声明与管理

        /// <summary>
        /// 基础流水线 - 基础状态层
        /// </summary>
        [ShowInInspector, LabelText("基础流水线")]
        protected StatePipelineRuntime basicPipeline;

        /// <summary>
        /// 主流水线 - 主要动作层
        /// </summary>
        [ShowInInspector, LabelText("主流水线")]
        protected StatePipelineRuntime mainPipeline;

        /// <summary>
        /// Buff流水线 - 增益/减益效果层
        /// </summary>
        [ShowInInspector, LabelText("Buff流水线")]
        protected StatePipelineRuntime buffPipeline;

        /// <summary>
        /// 流水线混合模式 - 控制Main线和Basic线如何混合
        /// </summary>
        [TitleGroup("流水线混合设置", Order = 1)]
        [LabelText("混合模式"), InfoBox("Override: Main覆盖Basic（推荐）\nAdditive: 权重叠加\nMultiplicative: Main调制Basic")]
        [EnumToggleButtons]
        public PipelineBlendMode pipelineBlendMode = PipelineBlendMode.Override;

        /// <summary>
        /// 通过枚举获取对应的流水线
        /// </summary>
        private StatePipelineRuntime GetPipelineByType(StatePipelineType pipelineType)
        {
            switch (pipelineType)
            {
                case StatePipelineType.Basic:
                    return basicPipeline;
                case StatePipelineType.Main:
                    return mainPipeline;
                case StatePipelineType.Buff:
                    return buffPipeline;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 设置流水线引用
        /// </summary>
        private void SetPipelineByType(StatePipelineType pipelineType, StatePipelineRuntime pipeline)
        {
            switch (pipelineType)
            {
                case StatePipelineType.Basic:
                    basicPipeline = pipeline;
                    break;
                case StatePipelineType.Main:
                    mainPipeline = pipeline;
                    break;
                case StatePipelineType.Buff:
                    buffPipeline = pipeline;
                    break;
            }
        }

        /// <summary>
        /// 获取所有流水线（用于遍历）
        /// 注意：basicPipeline和mainPipeline必须不为null，否则视为崩溃
        /// </summary>
        private IEnumerable<StatePipelineRuntime> GetAllPipelines()
        {
            yield return basicPipeline;
            yield return mainPipeline;
            if (buffPipeline != null) yield return buffPipeline;
        }

        #endregion

        #region Playable动画系统

        /// <summary>
        /// PlayableGraph引用 - 用于动画播放
        /// </summary>
        [NonSerialized]
        protected PlayableGraph playableGraph;

        /// <summary>
        /// 绑定的Animator
        /// </summary>
        [NonSerialized]
        protected Animator boundAnimator;

        /// <summary>
        /// Animator输出
        /// </summary>
        [NonSerialized]
        protected AnimationPlayableOutput animationOutput;

        /// <summary>
        /// 根动画混合器 - 支持多层动画混合
        /// </summary>
        [NonSerialized]
        internal AnimationMixerPlayable rootMixer;

        /// <summary>
        /// 是否拥有PlayableGraph所有权
        /// </summary>
        [NonSerialized]
        protected bool ownsPlayableGraph = false;

        public bool IsPlayableGraphValid => playableGraph.IsValid();

        public bool IsPlayableGraphPlaying => playableGraph.IsValid() && playableGraph.IsPlaying();

        public Animator BoundAnimator => boundAnimator;

        #endregion

        #region 性能优化相关

        /// <summary>
        /// 状态转换缓存 - 避免频繁的字典查找
        /// </summary>
        [NonSerialized]
        protected Dictionary<string, StateBase> transitionCache = new Dictionary<string, StateBase>();

        /// <summary>
        /// 脏标记 - 标识是否需要更新
        /// </summary>
        [NonSerialized]
        protected bool isDirty = false;

        private sealed class StateActivationCache
        {
            public int[] versions;
            public StateActivationResult[] results;
            public List<StateBase>[] interruptLists;
            public List<StateBase>[] mergeLists;
        }

        public enum StateDirtyReason
        {
            Unknown = 0,
            Enter = 1,
            Exit = 2,
            Release = 3,
            CostChanged = 4,
            RuntimeChanged = 5
        }

        [Serializable]
        public struct StateCostSummary
        {
            public float motionCost;
            public float agilityCost;
            public float targetCost;
            public float weightedMotion;
            public float weightedAgility;
            public float weightedTarget;
            public float totalWeighted;

            public void Clear()
            {
                motionCost = 0f;
                agilityCost = 0f;
                targetCost = 0f;
                weightedMotion = 0f;
                weightedAgility = 0f;
                weightedTarget = 0f;
                totalWeighted = 0f;
            }
        }

        #endregion

        #region 初始化与销毁

        /// <summary>
        /// 初始化状态机
        /// </summary>
        /// <param name="entity">宿主Entity</param>
        /// <param name="graph">PlayableGraph，如果为default则自动创建</param>
        /// <param name="root">外部RootMixer（可选）</param>
        public void Initialize(Entity entity, PlayableGraph graph = default, AnimationMixerPlayable root = default)
        {
            if (isInitialized) return;

            hostEntity = entity;

            // 初始化StateContext（整合了原StateMachineContext和动画参数）
            if (stateContext == null)
            {
                stateContext = new StateMachineContext();
                stateContext.contextID = Guid.NewGuid().ToString();
                stateContext.creationTime = Time.time;
                stateContext.lastUpdateTime = Time.time;
            }

            // 初始化流水线
            InitializePipelines(graph, root);

            // 初始化所有状态（注意：状态初始化依赖流水线已创建，所以必须在InitializePipelines之后）
            foreach (var kvp in stringToStateMap)
            {
                InitializeState(kvp.Value);
            }

            // 标记所有流水线Dirty=2，表示初始化完成，触发首次FallBack检查
            basicPipeline?.MarkDirty(2);
            mainPipeline?.MarkDirty(2);
            buffPipeline?.MarkDirty(2);

            isInitialized = true;
        }

        /// <summary>
        /// 初始化状态机并绑定Animator
        /// </summary>
        public void Initialize(Entity entity, Animator animator, PlayableGraph graph = default, AnimationMixerPlayable root = default)
        {
            Initialize(entity, graph, root);
            BindToAnimator(animator);
        }

        /// <summary>
        /// 初始化流水线系统
        /// </summary>
        private void InitializePipelines(PlayableGraph graph, AnimationMixerPlayable root)
        {
            // Playable初始化
            if (graph.IsValid())
            {
                playableGraph = graph;
                ownsPlayableGraph = false;
            }
            else
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                ownsPlayableGraph = true;
            }

            int pipelineCount = (int)StatePipelineType.Count;

            // 创建/绑定根Mixer
            if (playableGraph.IsValid())
            {
                if (root.IsValid())
                {
                    rootMixer = root;
                    if (rootMixer.GetInputCount() < pipelineCount)
                    {
                        rootMixer.SetInputCount(pipelineCount);
                    }
                }
                else
                {
                    rootMixer = AnimationMixerPlayable.Create(playableGraph, pipelineCount);
                }
            }

            // 使用封装方法直接装填所有流水线
            InitializeAllPipelines();
        }

        /// <summary>
        /// 初始化单个流水线
        /// </summary>
        private StatePipelineRuntime InitializeSinglePipeline(StatePipelineType pipelineType)
        {
            Debug.Log($"[StateMachine] 开始初始化流水线: {pipelineType}");
            var pipeline = new StatePipelineRuntime(pipelineType, this);
            SetPipelineByType(pipelineType, pipeline);

            // 如果有PlayableGraph,为流水线创建Mixer并接入Root
            if (playableGraph.IsValid())
            {
                pipeline.mixer = AnimationMixerPlayable.Create(playableGraph, 0);
                pipeline.rootInputIndex = (int)pipelineType;
                playableGraph.Connect(pipeline.mixer, 0, rootMixer, pipeline.rootInputIndex);
                rootMixer.SetInputWeight(pipeline.rootInputIndex, pipeline.weight);
                Debug.Log($"[StateMachine] ✓ {pipelineType}流水线Mixer创建成功 | Valid:{pipeline.mixer.IsValid()} | RootIndex:{pipeline.rootInputIndex}");
            }
            else
            {
                Debug.LogWarning($"[StateMachine] ✗ {pipelineType}流水线Mixer创建失败 - PlayableGraph无效");
            }

            OnPipelineInitialized?.Invoke(pipeline);
            return pipeline;
        }

        /// <summary>
        /// 初始化所有流水线 - 直接装填枚举
        /// </summary>
        private void InitializeAllPipelines()
        {
            // 直接装填每个枚举值
            basicPipeline = InitializeSinglePipeline(StatePipelineType.Basic);
            mainPipeline = InitializeSinglePipeline(StatePipelineType.Main);
            buffPipeline = InitializeSinglePipeline(StatePipelineType.Buff);
        }

        /// <summary>
        /// 初始化单个状态
        /// </summary>
        public void InitializeState(StateBase state)
        {
            if (state == null) return;
            state.host = this;
            state.Initialize(this);
        }

        /// <summary>
        /// 绑定PlayableGraph到Animator
        /// </summary>
        public bool BindToAnimator(Animator animator)
        {
            if (animator == null)
            {
                Debug.LogError("绑定Animator失败：Animator为空");
                return false;
            }

            if (!playableGraph.IsValid())
            {
                playableGraph = PlayableGraph.Create($"StateMachine_{stateMachineKey}");
                playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                ownsPlayableGraph = true;
            }

            if (!rootMixer.IsValid())
            {
                int pipelineCount = (int)StatePipelineType.Count;
                rootMixer = AnimationMixerPlayable.Create(playableGraph, pipelineCount);
            }

            boundAnimator = animator;

            if (!animationOutput.IsOutputValid())
            {
                animationOutput = AnimationPlayableOutput.Create(playableGraph, "StateMachine", animator);
            }
            else
            {
                animationOutput.SetTarget(animator);
            }

            animationOutput.SetSourcePlayable(rootMixer);
            // ★ 确保Output权重为1.0，否则动画不会输出
            animationOutput.SetWeight(1.0f);
            
            Debug.Log($"[StateMachine] Animator绑定成功: {animator.gameObject.name}");
            return true;
        }

        /// <summary>
        /// 标记Dirty（用于缓存失效）
        /// </summary>
        public void MarkDirty(StateDirtyReason reason = StateDirtyReason.Unknown)
        {
            _dirtyVersion++;
            isDirty = true;
            _lastDirtyReason = reason;
        }

        /// <summary>
        /// 清除Dirty标记（仅影响外部查询，不影响版本号）
        /// </summary>
        public void ClearDirty()
        {
            isDirty = false;
        }

        private StateActivationCache GetOrCreateActivationCache(StateBase targetState)
        {
            if (targetState == null) return null;

            if (!_activationCache.TryGetValue(targetState, out var cache) || cache == null)
            {
                cache = new StateActivationCache();
                _activationCache[targetState] = cache;
            }

            int pipelineCount = (int)StatePipelineType.Count;
            if (cache.versions == null || cache.versions.Length != pipelineCount)
            {
                cache.versions = new int[pipelineCount];
                cache.results = new StateActivationResult[pipelineCount];
                cache.interruptLists = new List<StateBase>[pipelineCount];
                cache.mergeLists = new List<StateBase>[pipelineCount];

                for (int i = 0; i < pipelineCount; i++)
                {
                    cache.versions[i] = -1;
                    cache.interruptLists[i] = new List<StateBase>(4);
                    cache.mergeLists[i] = new List<StateBase>(4);
                }
            }

            return cache;
        }

        public StateChannelMask GetTotalChannelMask()
        {
            if (_cachedChannelMaskVersion != _dirtyVersion || isDirty)
            {
                _cachedChannelMask = EvaluateChannelMask();
                _cachedChannelMaskVersion = _dirtyVersion;
            }

            return _cachedChannelMask;
        }

        public StateCostSummary GetTotalCostSummary()
        {
            if (_cachedCostVersion != _dirtyVersion || isDirty)
            {
                _cachedCostSummary = EvaluateCostSummary();
                _cachedCostVersion = _dirtyVersion;
            }

            return _cachedCostSummary;
        }

        private StateChannelMask EvaluateChannelMask()
        {
            if (CustomChannelMaskEvaluator != null)
            {
                return CustomChannelMaskEvaluator(runningStates);
            }

            StateChannelMask mask = StateChannelMask.None;
            foreach (var state in runningStates)
            {
                var mergeData = state?.stateSharedData?.mergeData;
                if (mergeData != null)
                {
                    mask |= mergeData.stateChannelMask;
                }
            }

            return mask;
        }

        private StateCostSummary EvaluateCostSummary()
        {
            if (CustomCostEvaluator != null)
            {
                return CustomCostEvaluator(runningStates);
            }

            StateCostSummary sum = new StateCostSummary();
            foreach (var state in runningStates)
            {
                var costData = state?.stateSharedData?.costData;
                if (costData == null || !costData.enableCostCalculation)
                {
                    continue;
                }

                sum.motionCost += costData.motionCost;
                sum.agilityCost += costData.agilityCost;
                sum.targetCost += costData.targetCost;
                sum.weightedMotion += costData.GetWeightedMotion();
                sum.weightedAgility += costData.GetWeightedAgility();
                sum.weightedTarget += costData.GetWeightedTarget();
            }

            sum.totalWeighted = sum.weightedMotion + sum.weightedAgility + sum.weightedTarget;
            return sum;
        }

        /// <summary>
        /// 通知代价层变化
        /// </summary>
        public void NotifyCostChanged()
        {
            MarkDirty(StateDirtyReason.CostChanged);
        }

        /// <summary>
        /// 销毁状态机，释放资源
        /// </summary>
        public void Dispose()
        {
            // 停用所有运行中的状态
            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                state.OnStateExit();
            }

            runningStates.Clear();

            // 清理流水线
            foreach (var pipeline in GetAllPipelines())
            {
                pipeline.runningStates.Clear();
                
                // 清理Playable槽位映射
                pipeline.stateToSlotMap?.Clear();
                pipeline.freeSlots?.Clear();
                
                if (pipeline.mixer.IsValid())
                {
                    pipeline.mixer.Destroy();
                }
            }

            basicPipeline = null;
            mainPipeline = null;
            buffPipeline = null;

            // 清理Playable资源
            if (ownsPlayableGraph && playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }

            animationOutput = default;
            boundAnimator = null;

            // 清理映射
            stringToStateMap.Clear();
            intToStateMap.Clear();
            transitionCache.Clear();
            statePipelineMap.Clear();
            _activationCache.Clear();

            // 清理上下文
            stateContext?.Clear();

            isRunning = false;
            isInitialized = false;
        }

        #endregion

        #region 状态机生命周期

        /// <summary>
        /// 启动状态机
        /// </summary>
        public void StartStateMachine()
        {
            if (isRunning) return;
            if (!isInitialized)
            {
                Debug.LogWarning($"状态机 {stateMachineKey} 未初始化，无法启动");
                return;
            }

            isRunning = true;

            // 播放PlayableGraph
            if (playableGraph.IsValid())
            {
                playableGraph.Play();
            }

            // 进入默认状态
            if (!string.IsNullOrEmpty(defaultStateKey))
            {
                TryActivateState(defaultStateKey, StatePipelineType.Basic);
            }
        }

        /// <summary>
        /// 停止状态机
        /// </summary>
        public void StopStateMachine()
        {
            if (!isRunning) return;

            // 停止所有流水线
            foreach (var pipeline in GetAllPipelines())
            {
                DeactivatePipeline(pipeline.pipelineType);
            }

            // 停止PlayableGraph
            if (playableGraph.IsValid())
            {
                playableGraph.Stop();
            }

            isRunning = false;
        }

        /// <summary>
        /// 更新状态机 - 每帧调用
        /// 注意：Animator需要设置为：
        /// 1. Update Mode = Normal (允许脚本控制)
        /// 2. Culling Mode = Always Animate (即使不可见也更新)
        /// 3. 不要勾选 Apply Root Motion（除非需要根运动）
        /// 
        /// Dirty机制：根据各流水线的Dirty等级执行不同任务
        /// - Dirty >= 1: 执行FallBack自动激活检查
        /// - Dirty >= 2: 执行中等优先级任务（预留）
        /// - Dirty >= 3: 执行高优先级任务（预留）
        /// </summary>
        public void UpdateStateMachine()
        {
            if (!isRunning) return;

            float deltaTime = Time.deltaTime;

            // 更新上下文时间
            stateContext.lastUpdateTime = Time.time;

            // 更新所有运行中的状态
            var statesToDeactivate = new List<StateBase>(); // 收集需要自动退出的状态
            foreach (var state in runningStates)
            {
                if (state != null && state.baseStatus == StateBaseStatus.Running)
                {
                    state.OnStateUpdate();
                    
                    // ★ 更新动画权重 - 2D混合树等需要通过StateContext获取参数
                    state.UpdateAnimationWeights(stateContext, deltaTime);
                    
                    // ★ 检查是否应该自动退出（按持续时间模式）
                    if (state.ShouldAutoExit(Time.time))
                    {
                        statesToDeactivate.Add(state);
                    }
                }
            }

            // 自动退出已完成的状态
            foreach (var state in statesToDeactivate)
            {
                // 查找状态所在流水线并停用
                foreach (var pipeline in GetAllPipelines())
                {
                    if (pipeline.runningStates.Contains(state))
                    {
                        TryDeactivateState(state.strKey);
                        break;
                    }
                }
            }

            // ★ 更新淡入淡出效果
            UpdateFades(deltaTime);

            // 更新三个流水线的MainState
            UpdatePipelineMainState(basicPipeline);
            UpdatePipelineMainState(mainPipeline);
            UpdatePipelineMainState(buffPipeline);
            
            // 更新流水线Dirty自动标记（高等级降级到1，保持最低Dirty用于持续检查FallBack）
            basicPipeline?.UpdateDirtyDecay();
            mainPipeline?.UpdateDirtyDecay();
            buffPipeline?.UpdateDirtyDecay();
            
            // 根据Dirty等级处理不同任务（包括FallBack自动激活）
            ProcessDirtyTasks(basicPipeline, StatePipelineType.Basic);
            ProcessDirtyTasks(mainPipeline, StatePipelineType.Main);
            ProcessDirtyTasks(buffPipeline, StatePipelineType.Buff);

            // ★ 应用流水线混合模式（Main与Basic的混合策略）
            ApplyPipelineBlendMode();
           
            // ★ 关键：手动推进PlayableGraph，将动画输出到Animator
            // PlayableGraph.SetTimeUpdateMode设置为GameTime时，Evaluate会自动使用deltaTime
            // 这确保动画持续更新并应用到Animator
            if (playableGraph.IsValid())
            {
                playableGraph.Evaluate(deltaTime);
            }

            // 持续输出统计信息（可选）
            if (enableContinuousStats)
            {
                OutputContinuousStats();
            }
        }

        /// <summary>
        /// 应用流水线混合模式 - 控制Main线和Basic线的混合权重
        /// </summary>
        private void ApplyPipelineBlendMode()
        {
            if (!rootMixer.IsValid() || basicPipeline == null || mainPipeline == null)
                return;

            float basicWeight = basicPipeline.weight;
            float mainWeight = mainPipeline.weight;
            
            // 计算Main线的实际激活度（有运行状态则视为激活）
            float mainActivation = (mainPipeline.runningStates.Count > 0) ? mainWeight : 0f;

            switch (pipelineBlendMode)
            {
                case PipelineBlendMode.Override:
                    // 覆盖模式：Main激活时完全覆盖Basic
                    if (mainActivation > 0.001f)
                    {
                        rootMixer.SetInputWeight(basicPipeline.rootInputIndex, 0f);
                        rootMixer.SetInputWeight(mainPipeline.rootInputIndex, 1f);
                    }
                    else
                    {
                        rootMixer.SetInputWeight(basicPipeline.rootInputIndex, 1f);
                        rootMixer.SetInputWeight(mainPipeline.rootInputIndex, 0f);
                    }
                    break;

                case PipelineBlendMode.Additive:
                    // 叠加模式：直接使用各自的权重（默认行为）
                    rootMixer.SetInputWeight(basicPipeline.rootInputIndex, basicWeight);
                    rootMixer.SetInputWeight(mainPipeline.rootInputIndex, mainWeight);
                    break;

                case PipelineBlendMode.Multiplicative:
                    // 乘法模式：Basic权重被Main激活度调制
                    float modulatedBasicWeight = basicWeight * (1f - mainActivation);
                    rootMixer.SetInputWeight(basicPipeline.rootInputIndex, modulatedBasicWeight);
                    rootMixer.SetInputWeight(mainPipeline.rootInputIndex, mainWeight);
                    break;
            }

            // Buff线始终使用自身权重（不受混合模式影响）
            if (buffPipeline != null && buffPipeline.mixer.IsValid())
            {
                rootMixer.SetInputWeight(buffPipeline.rootInputIndex, buffPipeline.weight);
            }
        }

        #region 淡入淡出系统

        /// <summary>
        /// 应用淡入效果到新激活的状态
        /// </summary>
        private void ApplyFadeIn(StateBase state, StatePipelineRuntime pipeline)
        {
            if (state?.stateSharedData == null || !state.stateSharedData.enableFadeInOut)
                return;

            float fadeInDuration = state.stateSharedData.fadeInDuration;
            if (fadeInDuration <= 0f || !pipeline.stateToSlotMap.ContainsKey(state))
                return;

            // 初始化淡入：权重从0开始
            int slotIndex = pipeline.stateToSlotMap[state];
            pipeline.mixer.SetInputWeight(slotIndex, 0f);

            // 记录淡入数据（需要在StatePipelineRuntime中添加字段）
            if (!pipeline.fadeInStates.ContainsKey(state))
            {
                pipeline.fadeInStates[state] = new FadeData
                {
                    elapsedTime = 0f,
                    duration = fadeInDuration,
                    slotIndex = slotIndex
                };

                StateMachineDebugSettings.Global.LogFade(
                    $"[淡入] 状态 {state.strKey} 开始淡入，时长 {fadeInDuration:F2}秒");
            }
        }

        /// <summary>
        /// 应用淡出效果到即将停用的状态
        /// </summary>
        private void ApplyFadeOut(StateBase state, StatePipelineRuntime pipeline)
        {
            if (state?.stateSharedData == null || !state.stateSharedData.enableFadeInOut)
                return;

            float fadeOutDuration = state.stateSharedData.fadeOutDuration;
            if (fadeOutDuration <= 0f || !pipeline.stateToSlotMap.ContainsKey(state))
                return;

            // 记录淡出数据
            int slotIndex = pipeline.stateToSlotMap[state];
            float currentWeight = pipeline.mixer.GetInputWeight(slotIndex);
            
            if (!pipeline.fadeOutStates.ContainsKey(state))
            {
                pipeline.fadeOutStates[state] = new FadeData
                {
                    elapsedTime = 0f,
                    duration = fadeOutDuration,
                    slotIndex = slotIndex,
                    startWeight = currentWeight
                };

                state.OnFadeOutStarted();
                StateMachineDebugSettings.Global.LogFade(
                    $"[淡出] 状态 {state.strKey} 开始淡出，时长 {fadeOutDuration:F2}秒，起始权重 {currentWeight:F2}");
            }
        }

        /// <summary>
        /// 更新所有流水线的淡入淡出效果
        /// </summary>
        private void UpdateFades(float deltaTime)
        {
            UpdatePipelineFades(basicPipeline, deltaTime);
            UpdatePipelineFades(mainPipeline, deltaTime);
            UpdatePipelineFades(buffPipeline, deltaTime);
        }

        /// <summary>
        /// 更新单个流水线的淡入淡出效果
        /// </summary>
        private void UpdatePipelineFades(StatePipelineRuntime pipeline, float deltaTime)
        {
            if (pipeline == null || !pipeline.mixer.IsValid())
                return;

            // 更新淡入状态
            var fadeInToRemove = new List<StateBase>();
            foreach (var kvp in pipeline.fadeInStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float weight = Mathf.Lerp(0f, 1f, t);

                pipeline.mixer.SetInputWeight(fadeData.slotIndex, weight);

                if (t >= 1f)
                {
                    fadeInToRemove.Add(state);
                    state.OnFadeInComplete();
                    StateMachineDebugSettings.Global.LogFade(
                        $"[淡入完成] 状态 {state.strKey}");
                }
            }

            // 移除已完成的淡入状态
            foreach (var state in fadeInToRemove)
            {
                pipeline.fadeInStates.Remove(state);
            }

            // 更新淡出状态
            var fadeOutToRemove = new List<StateBase>();
            foreach (var kvp in pipeline.fadeOutStates)
            {
                var state = kvp.Key;
                var fadeData = kvp.Value;

                fadeData.elapsedTime += deltaTime;
                float t = Mathf.Clamp01(fadeData.elapsedTime / fadeData.duration);
                float weight = Mathf.Lerp(fadeData.startWeight, 0f, t);

                pipeline.mixer.SetInputWeight(fadeData.slotIndex, weight);

                if (t >= 1f)
                {
                    fadeOutToRemove.Add(state);
                    StateMachineDebugSettings.Global.LogFade(
                        $"[淡出完成] 状态 {state.strKey}");
                }
            }

            // 移除已完成的淡出状态
            foreach (var state in fadeOutToRemove)
            {
                pipeline.fadeOutStates.Remove(state);
            }
        }

        #endregion

        /// <summary>
        /// 输出持续统计信息 - 简洁版，不干扰游戏运行
        /// </summary>
        private void OutputContinuousStats()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append($"[Stats] 运行:{runningStates.Count} |");
            
            foreach (var pipeline in GetAllPipelines())
            {
                if (pipeline.runningStates.Count > 0)
                {
                    sb.Append($" {pipeline.pipelineType}:{pipeline.runningStates.Count}");
                }
            }
            
            if (runningStates.Count > 0)
            {
                sb.Append(" | 状态:");
                foreach (var state in runningStates)
                {
                    if (state != null)
                    {
                        sb.Append($" [{state.strKey}]");
                    }
                }
            }
            
            Debug.Log(sb.ToString());
        }
        
        /// <summary>
        /// 根据流水线的Dirty等级处理不同的任务
        /// </summary>
        private void ProcessDirtyTasks(StatePipelineRuntime pipelineData, StatePipelineType pipeline)
        {
            if (pipelineData == null || !pipelineData.IsDirty) return;

            // Dirty >= 3: 高优先级任务（预留）
            if (pipelineData.dirtyLevel >= 3)
            {
                // 可在此添加高优先级任务
            }

            // Dirty >= 2: 中等优先级任务（预留）
            if (pipelineData.dirtyLevel >= 2)
            {
                // 可在此添加中等优先级任务
            }

            // Dirty >= 1: FallBack自动激活检查
            if (pipelineData.dirtyLevel >= 1)
            {
                // 如果流水线空闲，尝试激活FallBack状态
                if (pipelineData.runningStates.Count == 0)
                {
                    Debug.Log($"[FallBack-Activate] ⚠ [{pipeline}] 流水线已空，检查FallBack配置...");
                    Debug.Log($"[FallBack-Activate]   DefaultChannel={pipelineData.DefaultFallBackChannel}");
                    
                    // 使用多通道FallBack系统
                    int fallbackStateId = pipelineData.GetFallBack(); // 使用默认通道（内部会打印日志）
                    
                    if (fallbackStateId >= 0)
                    {
                        Debug.Log($"[FallBack-Activate] 🔍 查找FallBack状态: StateID={fallbackStateId}");
                        var fallbackState = GetStateByInt(fallbackStateId);
                        
                        if (fallbackState != null)
                        {
                            Debug.Log($"[FallBack-Activate] ✓ 找到FallBack状态: {fallbackState.strKey}, 尝试激活...");
                            bool activated = TryActivateState(fallbackState, pipeline);
                            Debug.Log($"[FallBack-Activate] {(activated ? "✓" : "✗")} FallBack激活{(activated ? "成功" : "失败")}");
                            
                            if (activated)
                            {
                                // 激活成功后清除Dirty
                                pipelineData.MarkDirty(0);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[FallBack-Activate] ✗ 未找到FallBack状态(ID={fallbackStateId})，流水线将保持空闲");
                        }
                    }
                    else
                    {
                        Debug.Log($"[FallBack-Activate] ⊘ [{pipeline}] 未配置FallBack状态(StateID={fallbackStateId})，流水线保持空闲");
                    }
                }
                else
                {
                    Debug.Log($"[FallBack-Activate] [{pipeline}] 流水线仍有{pipelineData.runningStates.Count}个运行状态，无需FallBack");
                    // 流水线非空时也清除Dirty
                    pipelineData.MarkDirty(0);
                }
            }
        }

        /// <summary>
        /// 诊断模式更新 - 打印详细日志帮助排查问题
        /// 使用方法：临时替换UpdateStateMachine()调用为UpdateStateMachineWithDiagnostics()
        /// </summary>
        public void UpdateStateMachineWithDiagnostics()
        {
            Debug.Log("==================== [状态机诊断] 开始 ====================");
            
            if (!isRunning)
            {
                Debug.LogError("[Diagnostics] ✗ 状态机未运行！请先调用StartStateMachine()");
                return;
            }

            float deltaTime = Time.deltaTime;
            stateContext.lastUpdateTime = Time.time;

            // === 1. 检查PlayableGraph ===
            Debug.Log($"[1. PlayableGraph]");
            if (!playableGraph.IsValid())
            {
                Debug.LogError("  ✗ PlayableGraph无效！");
                return;
            }
            Debug.Log($"  ✓ PlayableGraph有效: True");
            
            bool isPlaying = playableGraph.IsPlaying();
            if (isPlaying)
            {
                Debug.Log($"  ✓ PlayableGraph播放中: True");
            }
            else
            {
                Debug.LogWarning("  ⚠ PlayableGraph未播放，尝试启动...");
                playableGraph.Play();
            }
            Debug.Log($"  - Graph名称: {playableGraph.GetEditorName()}");

            // === 2. 检查Animator绑定 ===
            Debug.Log($"\n[2. Animator绑定]");
            if (boundAnimator == null)
            {
                Debug.LogError("  ✗ 未绑定Animator！请调用BindToAnimator()或Initialize(entity, animator)");
            }
            else
            {
                Debug.Log($"  ✓ Animator已绑定: {boundAnimator.gameObject.name}");
                Debug.Log($"  - Animator.enabled: {boundAnimator.enabled}");
                Debug.Log($"  - Animator.isActiveAndEnabled: {boundAnimator.isActiveAndEnabled}");
                Debug.Log($"  - Has Avatar: {boundAnimator.avatar != null}");
                Debug.Log($"  - Avatar IsValid: {(boundAnimator.avatar != null ? boundAnimator.avatar.isValid : false)}");
                Debug.Log($"  - Avatar IsHuman: {(boundAnimator.avatar != null ? boundAnimator.avatar.isHuman : false)}");
                
                if (!boundAnimator.enabled)
                {
                    Debug.LogWarning("  ⚠ Animator已禁用！动画不会播放");
                }
            }

            // === 3. 检查AnimationOutput ===
            Debug.Log($"\n[3. AnimationOutput]");
            if (!animationOutput.IsOutputValid())
            {
                Debug.LogError("  ✗ AnimationOutput无效！");
            }
            else
            {
                Debug.Log($"  ✓ AnimationOutput有效: True");
                float weight = animationOutput.GetWeight();
                Debug.Log($"  - Output权重: {weight:F3}");
                
                if (weight < 0.99f)
                {
                    Debug.LogWarning($"  ⚠ Output权重过低({weight:F3})，自动设置为1.0");
                    animationOutput.SetWeight(1.0f);
                }
                else
                {
                    Debug.Log("  ✓ Output权重正常: 1.0");
                }
                
                var sourcePlayable = animationOutput.GetSourcePlayable();
                Debug.Log($"  - 源Playable有效: {sourcePlayable.IsValid()}");
            }

            // === 4. 检查RootMixer连接状态 ===
            Debug.Log($"\n[4. RootMixer连接]");
            if (!rootMixer.IsValid())
            {
                Debug.LogError("  ✗ RootMixer无效！");
            }
            else
            {
                Debug.Log($"  ✓ RootMixer有效: True");
                int inputCount = rootMixer.GetInputCount();
                Debug.Log($"  - 输入槽位数: {inputCount}");
                
                for (int i = 0; i < inputCount; i++)
                {
                    var input = rootMixer.GetInput(i);
                    float weight = rootMixer.GetInputWeight(i);
                    StatePipelineType pipelineType = (StatePipelineType)i;
                    
                    Debug.Log($"  [槽位{i} - {pipelineType}]");
                    Debug.Log($"    - 输入有效: {input.IsValid()}");
                    Debug.Log($"    - 权重: {weight:F3}");
                    
                    if (input.IsValid() && input.IsPlayableOfType<AnimationMixerPlayable>())
                    {
                        var mixer = (AnimationMixerPlayable)input;
                        int subCount = mixer.GetInputCount();
                        Debug.Log($"    - 子Mixer输入数: {subCount}");
                        
                        var pipeline = GetPipelineByType(pipelineType);
                        if (pipeline != null)
                        {
                            Debug.Log($"    - 运行状态数: {pipeline.runningStates.Count}");
                            Debug.Log($"    - 槽位映射数: {pipeline.stateToSlotMap.Count}");
                        }
                    }
                }
            }

            // === 5. 检查当前运行状态及动画配置 ===
            Debug.Log($"\n[5. 运行状态 & 动画配置]");
            Debug.Log($"  - 总运行状态数: {runningStates.Count}");
            
            if (runningStates.Count == 0)
            {
                Debug.LogWarning("  ⚠ 没有运行中的状态！");
            }
            else
            {
                int index = 0;
                foreach (var state in runningStates)
                {
                    if (state != null)
                    {
                        index++;
                        Debug.Log($"  [状态{index}] {state.strKey} (ID:{state.intKey})");
                        Debug.Log($"    - 状态: {state.baseStatus}");
                        Debug.Log($"    - 有动画: {state.stateSharedData?.hasAnimation}");
                        
                        if (state.stateSharedData?.hasAnimation == true)
                        {
                            var animConfig = state.stateSharedData.animationConfig;
                            if (animConfig != null)
                            {
                                var calculator = animConfig.calculator;
                                Debug.Log($"    - Calculator类型: {calculator?.GetType().Name ?? "null"}");
                                
                                if (calculator is StateAnimationMixCalculatorForSimpleClip simpleClip)
                                {
                                    Debug.Log($"    - Clip: {simpleClip.clip?.name ?? "null"}");
                                    Debug.Log($"    - Clip长度: {(simpleClip.clip != null ? simpleClip.clip.length : 0):F2}秒");
                                    Debug.Log($"    - 播放速度: {simpleClip.speed}");
                                }
                            }
                            else
                            {
                                Debug.LogWarning($"    ⚠ animationConfig为null");
                            }
                            
                            // 检查状态是否在Pipeline映射中
                            if (statePipelineMap.TryGetValue(state, out var pipeline))
                            {
                                Debug.Log($"    - 所属管线: {pipeline}");
                                var pipelineRuntime = GetPipelineByType(pipeline);
                                if (pipelineRuntime != null)
                                {
                                    bool inSlotMap = pipelineRuntime.stateToSlotMap.ContainsKey(state);
                                    Debug.Log($"    - 在槽位映射中: {inSlotMap}");
                                    if (inSlotMap)
                                    {
                                        int slotIndex = pipelineRuntime.stateToSlotMap[state];
                                        Debug.Log($"    - 槽位索引: {slotIndex}");
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // === 6. 正常更新流程 ===
            Debug.Log($"\n[6. 执行更新]");
            foreach (var state in runningStates)
            {
                if (state != null && state.baseStatus == StateBaseStatus.Running)
                {
                    state.OnStateUpdate();
                    state.UpdateAnimationWeights(stateContext, deltaTime);
                }
            }

            UpdatePipelineMainState(basicPipeline);
            UpdatePipelineMainState(mainPipeline);
            UpdatePipelineMainState(buffPipeline);

            playableGraph.Evaluate(deltaTime);
            Debug.Log($"  ✓ PlayableGraph.Evaluate完成 (deltaTime={deltaTime:F4}秒)");
            
            Debug.Log("==================== [状态机诊断] 完成 ====================\n");
        }

        #endregion

        #region 状态注册与管理

        /// <summary>
        /// 注册新状态（String键）
        /// </summary>
        public bool RegisterState(string stateKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (string.IsNullOrEmpty(stateKey))
            {
                Debug.LogError("状态键不能为空");
                return false;
            }

            if (state == null)
            {
                Debug.LogError($"状态实例不能为空: {stateKey}");
                return false;
            }

            if (stringToStateMap.ContainsKey(stateKey))
            {
                Debug.LogWarning($"状态 {stateKey} 已存在，跳过注册");
                return false;
            }

            // 自动分配IntKey（从SharedData获取或自动生成）
            int autoIntKey = GenerateUniqueIntKey(state);
            if (intToStateMap.ContainsKey(autoIntKey))
            {
                Debug.LogWarning($"自动生成的IntKey {autoIntKey} 已存在，跳过注册");
                return false;
            }

            return RegisterStateCore(stateKey, autoIntKey, state, pipeline);
        }

        /// <summary>
        /// 注册新状态（Int键）
        /// </summary>
        public bool RegisterState(int stateKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (state == null)
            {
                Debug.LogError($"状态实例不能为空: {stateKey}");
                return false;
            }

            if (intToStateMap.ContainsKey(stateKey))
            {
                Debug.LogWarning($"状态ID {stateKey} 已存在，跳过注册");
                return false;
            }

            // 自动生成StringKey（从SharedData获取或自动生成）
            string autoStrKey = GenerateUniqueStringKey(state);
            if (stringToStateMap.ContainsKey(autoStrKey))
            {
                Debug.LogWarning($"自动生成的StringKey {autoStrKey} 已存在，跳过注册");
                return false;
            }

            return RegisterStateCore(autoStrKey, stateKey, state, pipeline);
        }

        /// <summary>
        /// 同时注册String和Int键
        /// </summary>
        public bool RegisterState(string stringKey, int intKey, StateBase state, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (string.IsNullOrEmpty(stringKey))
            {
                Debug.LogError("状态键不能为空");
                return false;
            }

            if (state == null)
            {
                Debug.LogError($"状态实例不能为空: {stringKey}");
                return false;
            }

            if (stringToStateMap.ContainsKey(stringKey))
            {
                Debug.LogWarning($"状态 {stringKey} 已存在，跳过注册");
                return false;
            }

            if (intToStateMap.ContainsKey(intKey))
            {
                Debug.LogWarning($"状态ID {intKey} 已存在，跳过注册");
                return false;
            }

            return RegisterStateCore(stringKey, intKey, state, pipeline);
        }

        /// <summary>
        /// 注销状态（String键）
        /// </summary>
        public bool UnregisterState(string stateKey)
        {
            if (!stringToStateMap.TryGetValue(stateKey, out var state))
            {
                return false;
            }

            return UnregisterStateCore(state);
        }

        /// <summary>
        /// 生成唯一的IntKey
        /// </summary>
        private int GenerateUniqueIntKey(StateBase state)
        {
            // 优先从SharedData.basicConfig.stateId获取
            if (state?.stateSharedData?.basicConfig != null)
            {
                int configId = state.stateSharedData.basicConfig.stateId;
                if (configId > 0 && !intToStateMap.ContainsKey(configId))
                {
                    return configId;
                }
            }

            // 自动分配
            while (intToStateMap.ContainsKey(_nextAutoIntId))
            {
                _nextAutoIntId++;
            }
            return _nextAutoIntId++;
        }

        /// <summary>
        /// 生成唯一的StringKey
        /// </summary>
        private string GenerateUniqueStringKey(StateBase state)
        {
            // 优先从SharedData.basicConfig.stateName获取
            if (state?.stateSharedData?.basicConfig != null)
            {
                string configName = state.stateSharedData.basicConfig.stateName;
                if (!string.IsNullOrEmpty(configName) && !stringToStateMap.ContainsKey(configName))
                {
                    return configName;
                }
            }

            // 自动分配
            string baseName = "State";
            string candidateName;
            do
            {
                candidateName = $"{baseName}_{_nextAutoStringIdSuffix++}";
            }
            while (stringToStateMap.ContainsKey(candidateName));
            
            return candidateName;
        }

        /// <summary>
        /// 检查并设置Fallback状态
        /// </summary>
        private void CheckAndSetFallbackState(StateBase state, StatePipelineType pipeline)
        {
            if (state?.stateSharedData?.basicConfig == null) return;

            // 检查是否可以作为Fallback状态
            if (state.stateSharedData.basicConfig.canBeFeedback)
            {
                // 获取Fallback通道索引（默认0）
                int fallbackChannel = state.stateSharedData.basicConfig.fallbackChannelIndex;
                
                // 获取目标流水线运行时
                var pipelineRuntime = GetPipelineByType(pipeline);
                if (pipelineRuntime != null)
                {
                    // 设置到流水线的对应通道
                    pipelineRuntime.SetFallBack(state.intKey, fallbackChannel);
                    Debug.Log($"[FallBack-Register] ✓ [{pipeline}] Channel{fallbackChannel} <- State '{state.strKey}' (ID:{state.intKey})");
                    Debug.Log($"[FallBack-Register]   当前通道配置: Ch0={pipelineRuntime.FallBackForChannel0}, Ch1={pipelineRuntime.FallBackForChannel1}, Ch2={pipelineRuntime.FallBackForChannel2}, Ch3={pipelineRuntime.FallBackForChannel3}, Ch4={pipelineRuntime.FallBackForChannel4}");
                }
                else
                {
                    Debug.LogError($"[FallBack-Register] ✗ 无法获取流水线运行时: {pipeline}");
                }
            }
        }

        /// <summary>
        /// 注册状态核心逻辑（私有，供三个RegisterState重载调用）
        /// </summary>
        private bool RegisterStateCore(string stringKey, int intKey, StateBase state, StatePipelineType pipeline)
        {
            // 同时注册到两个字典
            stringToStateMap[stringKey] = state;
            intToStateMap[intKey] = state;
            state.strKey = stringKey;
            state.intKey = intKey;
            state.host = this;
            statePipelineMap[state] = pipeline;

            // 检查并设置Fallback状态
            CheckAndSetFallbackState(state, pipeline);

            MarkDirty(StateDirtyReason.RuntimeChanged);

            if (isInitialized)
            {
                InitializeState(state);
            }

            Debug.Log($"[StateMachine] 注册状态: {stringKey} (IntKey:{intKey}, Pipeline:{pipeline})");
            return true;
        }

        /// <summary>
        /// 注销状态核心逻辑（私有，供UnregisterState重载调用）
        /// </summary>
        private bool UnregisterStateCore(StateBase state)
        {
            if (state == null) return false;

            // 如果状态正在运行，先停用
            if (runningStates.Contains(state))
            {
                TryDeactivateState(state.strKey);
            }

            // 同时从两个字典移除
            if (!string.IsNullOrEmpty(state.strKey))
            {
                stringToStateMap.Remove(state.strKey);
            }
            if (state.intKey != -1)
            {
                intToStateMap.Remove(state.intKey);
            }
            
            transitionCache.Remove(state.strKey);
            statePipelineMap.Remove(state);
            _activationCache.Remove(state);
            MarkDirty(StateDirtyReason.Release);
            return true;
        }

        /// <summary>
        /// 注销状态（Int键）
        /// </summary>
        public bool UnregisterState(int stateKey)
        {
            if (!intToStateMap.TryGetValue(stateKey, out var state))
            {
                return false;
            }

            return UnregisterStateCore(state);
        }

        /// <summary>
        /// 获取状态（通过String键）
        /// </summary>
        public StateBase GetStateByString(string stateKey)
        {
            if (string.IsNullOrEmpty(stateKey)) return null;

            // 优先从缓存获取
            if (transitionCache.TryGetValue(stateKey, out var cachedState))
            {
                return cachedState;
            }

            // 从字典获取并缓存
            if (stringToStateMap.TryGetValue(stateKey, out var state))
            {
                transitionCache[stateKey] = state;
                return state;
            }

            return null;
        }

        /// <summary>
        /// 获取状态（通过Int键）
        /// </summary>
        public StateBase GetStateByInt(int stateKey)
        {
            return intToStateMap.TryGetValue(stateKey, out var state) ? state : null;
        }

        /// <summary>
        /// 检查状态是否存在（String键）
        /// </summary>
        public bool HasState(string stateKey)
        {
            return stringToStateMap.ContainsKey(stateKey);
        }

        /// <summary>
        /// 检查状态是否存在（Int键）
        /// </summary>
        public bool HasState(int stateKey)
        {
            return intToStateMap.ContainsKey(stateKey);
        }

        /// <summary>
        /// 设置Fallback状态
        /// </summary>
        public void SetFallbackState(StatePipelineType pipelineType, int stateId, int channel = 0)
        {
            var pipeline = GetPipelineByType(pipelineType);
            if (pipeline != null)
            {
                pipeline.SetFallBack(stateId, channel);
            }
        }

        /// <summary>
        /// 获取流水线
        /// </summary>
        public StatePipelineRuntime GetPipeline(StatePipelineType pipelineType)
        {
            return GetPipelineByType(pipelineType);
        }

        /// <summary>
        /// 设置流水线权重
        /// </summary>
        public void SetPipelineWeight(StatePipelineType pipelineType, float weight)
        {
            var pipeline = GetPipelineByType(pipelineType);
            if (pipeline != null)
            {
                pipeline.weight = Mathf.Clamp01(weight);
                // 更新Playable权重
                pipeline.UpdatePipelineMixer();
            }
        }

        #endregion

        #region 临时动画热拔插

        /// <summary>
        /// 临时动画状态跟踪
        /// </summary>
        [NonSerialized]
        private Dictionary<string, StateBase> _temporaryStates = new Dictionary<string, StateBase>();

#if UNITY_EDITOR
        // === 编辑器测试字段 ===
        [FoldoutGroup("临时动画测试", expanded: false)]
        [LabelText("测试键"), Tooltip("临时状态的唯一标识")]
        public string testTempKey = "TestAnim";
        
        [FoldoutGroup("临时动画测试")]
        [LabelText("测试Clip"), AssetsOnly]
        public AnimationClip testClip;
        
        [FoldoutGroup("临时动画测试")]
        [LabelText("目标管线")]
        public StatePipelineType testPipeline = StatePipelineType.Main;
        
        [FoldoutGroup("临时动画测试")]
        [LabelText("播放速度"), Range(0.1f, 3f)]
        public float testSpeed = 1.0f;
        
        [FoldoutGroup("临时动画测试")]
        [LabelText("循环播放"), Tooltip("勾选后动画循环播放，不勾选则播放一次后自动退出")]
        public bool testLoopable = false;

        [FoldoutGroup("临时动画测试")]
        [Button("添加临时动画", ButtonSizes.Medium), GUIColor(0.4f, 0.8f, 1f)]
        private void EditorAddTemporaryAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("请在运行时测试！");
                return;
            }

            if (testClip == null)
            {
                Debug.LogError("请先指定Clip！");
                return;
            }

            AddTemporaryAnimation(testTempKey, testClip, testPipeline, testSpeed, testLoopable);
        }

        [FoldoutGroup("临时动画测试")]
        [Button("移除临时动画", ButtonSizes.Medium), GUIColor(1f, 0.7f, 0.4f)]
        private void EditorRemoveTemporaryAnimation()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("请在运行时测试！");
                return;
            }

            RemoveTemporaryAnimation(testTempKey);
        }
#endif

        /// <summary>
        /// 添加临时动画 - 快速热拔插（自动注册+激活）
        /// </summary>
        /// <param name="tempKey">临时状态键（唯一标识）</param>
        /// <param name="clip">动画Clip</param>
        /// <param name="pipeline">目标流水线</param>
        /// <param name="speed">播放速度</param>
        /// <param name="loopable">是否循环播放（false=播放一次后自动退出，true=持续循环）</param>
        /// <returns>是否添加成功</returns>
        public bool AddTemporaryAnimation(string tempKey, AnimationClip clip, StatePipelineType pipeline = StatePipelineType.Main, float speed = 1.0f, bool loopable = false)
        {
            if (string.IsNullOrEmpty(tempKey))
            {
                Debug.LogError("[TempAnim] 临时状态键不能为空");
                return false;
            }

            if (clip == null)
            {
                Debug.LogError("[TempAnim] AnimationClip不能为空");
                return false;
            }

            // 检查是否已存在
            if (_temporaryStates.ContainsKey(tempKey))
            {
                Debug.LogWarning($"[TempAnim] 临时状态 {tempKey} 已存在，先移除旧的");
                RemoveTemporaryAnimation(tempKey);
            }

            // 创建临时StateBase
            var tempState = new StateBase();
            tempState.strKey = $"__temp_{tempKey}";
            
            // 创建SharedData
            tempState.stateSharedData = new StateSharedData();
            tempState.stateSharedData.hasAnimation = true;
            
            // 创建BasicConfig（根据loopable配置播放模式）
            tempState.stateSharedData.basicConfig = new StateBasicConfig();
            tempState.stateSharedData.basicConfig.stateName = tempKey;
            tempState.stateSharedData.basicConfig.durationMode = loopable 
                ? StateDurationMode.Infinite  // 循环播放
                : StateDurationMode.UntilAnimationEnd; // 播放一次后自动退出
            tempState.stateSharedData.basicConfig.pipelineType = pipeline;
            
            // 创建AnimationConfig
            tempState.stateSharedData.animationConfig = new StateAnimationConfigData();
            var calculator = new StateAnimationMixCalculatorForSimpleClip
            {
                clip = clip,
                speed = speed
            };
            tempState.stateSharedData.animationConfig.calculator = calculator;
            
            // 初始化SharedData
            tempState.stateSharedData.InitializeRuntime();

            // 注册状态
            if (!RegisterState(tempState.strKey, tempState, pipeline))
            {
                Debug.LogError($"[TempAnim] 注册临时状态失败: {tempKey}");
                return false;
            }

            // 激活状态
            if (!TryActivateState(tempState, pipeline))
            {
                Debug.LogError($"[TempAnim] 激活临时状态失败: {tempKey}");
                UnregisterState(tempState.strKey);
                return false;
            }

            // 记录到临时状态集合
            _temporaryStates[tempKey] = tempState;
            Debug.Log($"[TempAnim] ✓ 添加临时动画: {tempKey} | Clip:{clip.name} | Pipeline:{pipeline}");
            return true;
        }

        /// <summary>
        /// 移除临时动画
        /// </summary>
        /// <param name="tempKey">临时状态键</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveTemporaryAnimation(string tempKey)
        {
            if (!_temporaryStates.TryGetValue(tempKey, out var tempState))
            {
                Debug.LogWarning($"[TempAnim] 临时状态 {tempKey} 不存在");
                return false;
            }

            // 停用并注销状态
            if (runningStates.Contains(tempState))
            {
                TryDeactivateState(tempState.strKey);
            }
            UnregisterState(tempState.strKey);

            // 从临时集合移除
            _temporaryStates.Remove(tempKey);
            Debug.Log($"[TempAnim] ✓ 移除临时动画: {tempKey}");
            return true;
        }

        /// <summary>
        /// 一键清除所有临时动画
        /// </summary>
        public void ClearAllTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                Debug.Log("[TempAnim] 没有临时动画需要清除");
                return;
            }

            Debug.Log($"[TempAnim] 开始清除 {_temporaryStates.Count} 个临时动画");
            
            // 复制键列表避免迭代时修改字典
            var keys = new List<string>(_temporaryStates.Keys);
            foreach (var key in keys)
            {
                RemoveTemporaryAnimation(key);
            }

            _temporaryStates.Clear();
            Debug.Log("[TempAnim] ✓ 所有临时动画已清除");
        }

        /// <summary>
        /// 检查临时动画是否存在
        /// </summary>
        public bool HasTemporaryAnimation(string tempKey)
        {
            return _temporaryStates.ContainsKey(tempKey);
        }

        /// <summary>
        /// 获取临时动画数量
        /// </summary>
        public int GetTemporaryAnimationCount()
        {
            return _temporaryStates.Count;
        }

        #endregion

        #region 状态激活测试与执行

        /// <summary>
        /// 测试状态能否激活（不执行）
        /// </summary>
        public StateActivationResult TestStateActivation(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (targetState == null)
            {
                return StateActivationResult.Failure("目标状态为空");
            }

            if (!isRunning)
            {
                return StateActivationResult.Failure("状态机未运行");
            }

            if (CustomActivationTest != null)
            {
                return CustomActivationTest(targetState, pipeline);
            }

            int pipelineCount = (int)StatePipelineType.Count;
            int pipelineIndex = (int)pipeline;
            if (pipelineIndex < 0 || pipelineIndex >= pipelineCount)
            {
                return StateActivationResult.Failure($"流水线索引非法: {pipeline}");
            }

            var cache = GetOrCreateActivationCache(targetState);
            if (cache != null && cache.versions[pipelineIndex] == _dirtyVersion)
            {
                return cache.results[pipelineIndex];
            }

            // 检查该状态是否已在运行
            if (runningStates.Contains(targetState))
            {
                var failure = StateActivationResult.Failure("状态已在运行中");
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            // 获取目标流水线
            var targetPipeline = GetPipelineByType(pipeline);
            if (targetPipeline == null)
            {
                var failure = StateActivationResult.Failure($"流水线 {pipeline} 不存在");
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            if (!targetPipeline.isEnabled)
            {
                var failure = StateActivationResult.Failure($"流水线 {pipeline} 未启用");
                if (cache != null)
                {
                    cache.results[pipelineIndex] = failure;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return failure;
            }

            // 获取该流水线中当前运行的状态
            var pipelineStates = targetPipeline.runningStates;

            // 如果流水线为空，直接成功
            if (pipelineStates.Count == 0)
            {
                var success = StateActivationResult.Success(pipeline, false);
                if (cache != null)
                {
                    cache.results[pipelineIndex] = success;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return success;
            }

            // 检查合并和冲突（复用列表，减少GC）
            var interruptList = cache != null ? cache.interruptLists[pipelineIndex] : _tmpInterruptStates;
            var mergeList = cache != null ? cache.mergeLists[pipelineIndex] : _tmpMergeStates;
            interruptList.Clear();
            mergeList.Clear();

            foreach (var existingState in pipelineStates)
            {
                // 这里可以添加自定义的冲突/合并逻辑
                // 示例：检查StateSharedData的mergeData
                if (existingState.stateSharedData != null && targetState.stateSharedData != null)
                {
                    // 简化的合并判断逻辑
                    bool canMerge = CheckStateMergeCompatibility(existingState, targetState);
                    if (canMerge)
                    {
                        mergeList.Add(existingState);
                    }
                    else
                    {
                        interruptList.Add(existingState);
                    }
                }
                else
                {
                    // 默认：打断
                    interruptList.Add(existingState);
                }
            }

            // 如果可以合并
            if (mergeList.Count > 0 && interruptList.Count == 0)
            {
                var success = new StateActivationResult
                {
                    canActivate = true,
                    requiresInterruption = false,
                    canMerge = true,
                    mergeDirectly = true,
                    statesToInterrupt = interruptList,
                    statesToMergeWith = mergeList,
                    interruptCount = 0,
                    mergeCount = mergeList.Count,
                    failureReason = string.Empty,
                    targetPipeline = pipeline
                };
                if (cache != null)
                {
                    cache.results[pipelineIndex] = success;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return success;
            }

            // 如果需要打断
            if (interruptList.Count > 0)
            {
                var success = new StateActivationResult
                {
                    canActivate = true,
                    requiresInterruption = true,
                    canMerge = false,
                    mergeDirectly = false,
                    statesToInterrupt = interruptList,
                    statesToMergeWith = mergeList,
                    interruptCount = interruptList.Count,
                    mergeCount = 0,
                    failureReason = string.Empty,
                    targetPipeline = pipeline
                };
                if (cache != null)
                {
                    cache.results[pipelineIndex] = success;
                    cache.versions[pipelineIndex] = _dirtyVersion;
                }
                return success;
            }

            var defaultSuccess = new StateActivationResult
            {
                canActivate = true,
                requiresInterruption = false,
                canMerge = false,
                mergeDirectly = false,
                statesToInterrupt = interruptList,
                statesToMergeWith = mergeList,
                interruptCount = 0,
                mergeCount = 0,
                failureReason = string.Empty,
                targetPipeline = pipeline
            };
            if (cache != null)
            {
                cache.results[pipelineIndex] = defaultSuccess;
                cache.versions[pipelineIndex] = _dirtyVersion;
            }
            return defaultSuccess;
        }

        /// <summary>
        /// 更新流水线的MainState - 选择总代价最高的状态
        /// </summary>
        private void UpdatePipelineMainState(StatePipelineRuntime pipeline)
        {
            if (pipeline == null || pipeline.runningStates.Count == 0)
            {
                if (pipeline != null) pipeline.mainState = null;
                return;
            }

            StateBase maxCostState = null;
            float maxTotalCost = float.MinValue;

            foreach (var state in pipeline.runningStates)
            {
                if (state?.stateSharedData?.costData == null || !state.stateSharedData.costData.enableCostCalculation)
                {
                    continue;
                }

                float totalCost = state.stateSharedData.costData.GetTotalCost();

                if (totalCost > maxTotalCost)
                {
                    maxTotalCost = totalCost;
                    maxCostState = state;
                }
            }

            // 如果没有有效代价的状态，选择第一个
            pipeline.mainState = maxCostState ?? pipeline.runningStates.FirstOrDefault();
        }

        /// <summary>
        /// 检查两个状态是否可以合并
        /// </summary>
        private bool CheckStateMergeCompatibility(StateBase existing, StateBase incoming)
        {
            if (CanMergeEvaluator != null)
            {
                return CanMergeEvaluator(existing, incoming);
            }

            // 默认简单实现：不同状态可以合并
            return existing != incoming;
        }

        /// <summary>
        /// 检查流水线中的状态是否可以与新状态合并
        /// 基于Channel重合度：如果总代价不超过1，则可以合并
        /// </summary>
        private bool CanMergeByChannelOverlap(StatePipelineRuntime pipeline, StateBase incomingState)
        {
            if (pipeline == null || incomingState?.stateSharedData == null) return false;

            var incomingMergeData = incomingState.stateSharedData.mergeData;
            var incomingCostData = incomingState.stateSharedData.costData;
            if (incomingMergeData == null || incomingCostData == null) return false;

            float totalOverlapCost = 0f;

            // 遍历流水线中的所有状态
            foreach (var existingState in pipeline.runningStates)
            {
                if (existingState?.stateSharedData == null) continue;

                var existingMergeData = existingState.stateSharedData.mergeData;
                var existingCostData = existingState.stateSharedData.costData;
                if (existingMergeData == null || existingCostData == null) continue;

                // 检查Channel是否有重合
                StateChannelMask overlap = existingMergeData.stateChannelMask & incomingMergeData.stateChannelMask;
                if (overlap != StateChannelMask.None)
                {
                    // 有重合，累加代价
                    if (existingCostData.enableCostCalculation)
                    {
                        totalOverlapCost += existingCostData.GetTotalCost();
                    }
                }
            }

            // 加上新状态的代价
            if (incomingCostData.enableCostCalculation)
            {
                totalOverlapCost += incomingCostData.GetTotalCost();
            }

            // 如果总代价不超过1，则可以合并
            return totalOverlapCost <= 1.0f;
        }

        /// <summary>
        /// 执行状态激活（根据测试结果）
        /// </summary>
        public bool ExecuteStateActivation(StateBase targetState, StateActivationResult result)
        {
            Debug.Log($"[StateMachine] === 开始执行状态激活 ===");
            Debug.Log($"[StateMachine]   状态: {targetState?.strKey} (ID:{targetState?.intKey})");
            Debug.Log($"[StateMachine]   目标管线: {result.targetPipeline}");
            
            if (!result.canActivate)
            {
                Debug.LogWarning($"[StateMachine] ✗ 状态激活失败: {result.failureReason}");
                return false;
            }

            var pipeline = GetPipelineByType(result.targetPipeline);
            if (pipeline == null)
            {
                Debug.LogError($"[StateMachine] ✗ 获取流水线失败: {result.targetPipeline}");
                return false;
            }

            Debug.Log($"[StateMachine]   流水线状态: Mixer有效={pipeline.mixer.IsValid()}, 运行状态数={pipeline.runningStates.Count}");

            // 执行打断
            if (result.requiresInterruption && result.statesToInterrupt != null)
            {
                Debug.Log($"[StateMachine]   打断 {result.statesToInterrupt.Count} 个状态");
                foreach (var stateToInterrupt in result.statesToInterrupt)
                {
                    TruelyDeactivateState(stateToInterrupt, result.targetPipeline);
                }
            }

            // 激活目标状态
            targetState.OnStateEnter();
            runningStates.Add(targetState);
            pipeline.runningStates.Add(targetState);
            Debug.Log($"[StateMachine]   ✓ 状态已添加到运行集合");

            // 如果状态有动画，热插拔到Playable图
            HotPlugStateToPlayable(targetState, pipeline);

            // ★ 应用淡入逻辑（如果启用）
            ApplyFadeIn(targetState, pipeline);

            // 重新评估MainState
            UpdatePipelineMainState(pipeline);

            OnStateEntered?.Invoke(targetState, result.targetPipeline);
            MarkDirty(StateDirtyReason.Enter);

            Debug.Log($"[StateMachine] === 状态激活完成 ===");
            return true;
        }

        /// <summary>
        /// 尝试激活状态（通过键）
        /// </summary>
        public bool TryActivateState(string stateKey, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            var state = GetStateByString(stateKey);
            if (state == null)
            {
                Debug.LogWarning($"状态 {stateKey} 不存在");
                return false;
            }

            return TryActivateState(state, pipeline);
        }

        /// <summary>
        /// 尝试激活状态（通过Int键）
        /// </summary>
        public bool TryActivateState(int stateKey, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            var state = GetStateByInt(stateKey);
            if (state == null)
            {
                Debug.LogWarning($"状态ID {stateKey} 不存在");
                return false;
            }

            return TryActivateState(state, pipeline);
        }

        /// <summary>
        /// 尝试激活状态（通过实例 + 指定流水线）
        /// </summary>
        public bool TryActivateState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (targetState == null) return false;

            var result = TestStateActivation(targetState, pipeline);
            return ExecuteStateActivation(targetState, result);
        }

        /// <summary>
        /// 尝试激活状态（通过实例，使用注册时的默认流水线）
        /// </summary>
        public bool TryActivateState(StateBase targetState)
        {
            if (targetState == null) return false;

            if (statePipelineMap.TryGetValue(targetState, out var pipeline))
            {
                return TryActivateState(targetState, pipeline);
            }

            return TryActivateState(targetState, StatePipelineType.Basic);
        }

        /// <summary>
        /// 停用状态（内部方法）
        /// </summary>
        private void TruelyDeactivateState(StateBase state, StatePipelineType pipeline)
        {
            if (state == null) return;

            // ★ 应用淡出逻辑（如果启用）
            var pipelineData = GetPipelineByType(pipeline);
            if (pipelineData != null)
            {
                ApplyFadeOut(state, pipelineData);
            }

            // 从Playable图中卸载状态动画
            if (pipelineData != null)
            {
                HotUnplugStateFromPlayable(state, pipelineData);
            }

            state.OnStateExit();
            runningStates.Remove(state);

            if (pipelineData != null)
            {
                pipelineData.runningStates.Remove(state);
                
                // 重新评估MainState
                UpdatePipelineMainState(pipelineData);
                
                // 标记Dirty=1，让Update中检查FallBack
                pipelineData.MarkDirty(1);
            }

            OnStateExited?.Invoke(state, pipeline);
            MarkDirty(StateDirtyReason.Exit);
        }

        /// <summary>
        /// 尝试停用状态（通过String键）
        /// </summary>
        public bool TryDeactivateState(string stateKey)
        {
            var state = GetStateByString(stateKey);
            if (state == null || !runningStates.Contains(state))
            {
                return false;
            }

            // 查找该状态所在的流水线 - 零GC优化
            foreach (var pipeline in GetAllPipelines())
            {
                if (pipeline.runningStates.Contains(state))
                {
                    TruelyDeactivateState(state, pipeline.pipelineType);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试停用状态（通过Int键）
        /// </summary>
        public bool TryDeactivateState(int stateKey)
        {
            var state = GetStateByInt(stateKey);
            if (state == null || !runningStates.Contains(state))
            {
                return false;
            }

            // 查找该状态所在的流水线 - 零GC优化
            foreach (var pipeline in GetAllPipelines())
            {
                if (pipeline.runningStates.Contains(state))
                {
                    TruelyDeactivateState(state, pipeline.pipelineType);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 进入验证测试（不执行）
        /// </summary>
        public StateActivationResult TestEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            return TestStateActivation(targetState, pipeline);
        }

        /// <summary>
        /// 测试进入（验证后执行进入）
        /// </summary>
        public bool TryEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            return TryActivateState(targetState, pipeline);
        }

        /// <summary>
        /// 强制进入（不做验证）
        /// </summary>
        public bool ForceEnterState(StateBase targetState, StatePipelineType pipeline = StatePipelineType.Basic)
        {
            if (targetState == null) return false;

            var pipelineData = GetPipelineByType(pipeline);
            if (pipelineData == null)
            {
                return false;
            }

            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(pipelineData.runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                TruelyDeactivateState(state, pipeline);
            }

            targetState.OnStateEnter();
            runningStates.Add(targetState);
            pipelineData.runningStates.Add(targetState);
            
            // 重新评估MainState
            UpdatePipelineMainState(pipelineData);

            OnStateEntered?.Invoke(targetState, pipeline);
            return true;
        }

        /// <summary>
        /// 退出验证测试（不执行）
        /// </summary>
        public StateExitResult TestExitState(StateBase targetState)
        {
            if (targetState == null)
            {
                return StateExitResult.Failure("目标状态为空", StatePipelineType.Basic);
            }

            if (!runningStates.Contains(targetState))
            {
                return StateExitResult.Failure("状态未在运行中", StatePipelineType.Basic);
            }

            if (!statePipelineMap.TryGetValue(targetState, out var pipeline))
            {
                pipeline = StatePipelineType.Basic;
            }

            if (CustomExitTest != null)
            {
                return CustomExitTest(targetState, pipeline);
            }

            return StateExitResult.Success(pipeline);
        }

        /// <summary>
        /// 测试退出（验证后执行退出）
        /// </summary>
        public bool TryExitState(StateBase targetState)
        {
            var result = TestExitState(targetState);
            if (!result.canExit)
            {
                return false;
            }

            TruelyDeactivateState(targetState, result.pipeline);
            return true;
        }

        /// <summary>
        /// 强制退出（不做验证）
        /// </summary>
        public void ForceExitState(StateBase targetState)
        {
            if (targetState == null) return;

            if (statePipelineMap.TryGetValue(targetState, out var pipeline))
            {
                TruelyDeactivateState(targetState, pipeline);
                return;
            }

            // 查找该状态所在的流水线 - 零GC优化
            foreach (var pipelineRuntime in GetAllPipelines())
            {
                if (pipelineRuntime.runningStates.Contains(targetState))
                {
                    TruelyDeactivateState(targetState, pipelineRuntime.pipelineType);
                    return;
                }
            }
        }

        /// <summary>
        /// 停用流水线中的所有状态
        /// </summary>
        public void DeactivatePipeline(StatePipelineType pipeline)
        {
            var pipelineData = GetPipelineByType(pipeline);
            if (pipelineData == null)
            {
                return;
            }

            _tmpStateBuffer.Clear();
            _tmpStateBuffer.AddRange(pipelineData.runningStates);
            foreach (var state in _tmpStateBuffer)
            {
                TruelyDeactivateState(state, pipeline);
            }
        }

        #endregion

        #region Playable动画管理

        /// <summary>
        /// 热插拔状态到Playable图（运行时动态添加）- 高性能版本
        /// </summary>
        internal void HotPlugStateToPlayable(StateBase state, StatePipelineRuntime pipeline)
        {
            Debug.Log($"[HotPlug] === 开始热插拔状态到Playable ===");
            Debug.Log($"[HotPlug]   状态: {state?.strKey} | 流水线: {pipeline?.pipelineType}");
            
            if (state == null || pipeline == null)
            {
                Debug.LogWarning($"[HotPlug] ✗ 状态或流水线为空 - State:{state != null}, Pipeline:{pipeline != null}");
                return;
            }
            
            // 检查状态是否有动画
            if (state.stateSharedData?.hasAnimation != true)
            {
                Debug.Log($"[HotPlug]   状态无动画，跳过热插拔");
                return;
            }

            // 检查是否已经插入过
            if (pipeline.stateToSlotMap.ContainsKey(state))
            {
                Debug.Log($"[HotPlug]   状态已在槽位映射中，跳过");
                return; // 已存在，跳过
            }

            // 确保PlayableGraph和流水线Mixer有效
            Debug.Log($"[HotPlug]   检查Playable有效性:");
            Debug.Log($"[HotPlug]     PlayableGraph有效: {playableGraph.IsValid()}");
            Debug.Log($"[HotPlug]     Pipeline.mixer有效: {pipeline.mixer.IsValid()}");
            
            if (!playableGraph.IsValid() || !pipeline.mixer.IsValid())
            {
                Debug.LogError($"[HotPlug] ✗✗✗ 无法插入状态动画：PlayableGraph({playableGraph.IsValid()})或Mixer({pipeline.mixer.IsValid()})无效 ✗✗✗");
                Debug.LogError($"[HotPlug]   这是问题所在！流水线: {pipeline.pipelineType}");
                Debug.LogError($"[HotPlug]   StateMachine初始化状态: {isInitialized}");
                Debug.LogError($"[HotPlug]   StateMachine运行状态: {isRunning}");
                return;
            }

            // 获取状态的动画配置
            var animConfig = state.stateSharedData.animationConfig;
            if (animConfig == null)
            {
                Debug.LogWarning($"状态 {state.strKey} 标记了hasAnimation=true，但没有animationConfig");
                return;
            }

            // 创建Playable节点
            var statePlayable = CreateStatePlayable(state, animConfig);
            if (!statePlayable.IsValid())
            {
                Debug.LogWarning($"无法为状态 {state.strKey} 创建有效的Playable节点");
                return;
            }

            int inputIndex;

            // 优先从空闲槽位池获取
            if (pipeline.freeSlots.Count > 0)
            {
                inputIndex = pipeline.freeSlots.Pop();
                
                // 断开旧连接（如果有）
                if (pipeline.mixer.GetInput(inputIndex).IsValid())
                {
                    playableGraph.Disconnect(pipeline.mixer, inputIndex);
                }
            }
            else
            {
                // 检查是否达到最大槽位限制
                int currentCount = pipeline.mixer.GetInputCount();
                if (currentCount >= pipeline.maxPlayableSlots)
                {
                    Debug.LogWarning($"流水线 {pipeline.pipelineType} 已达到最大Playable槽位限制 {pipeline.maxPlayableSlots}，无法添加新状态");
                    statePlayable.Destroy();
                    return;
                }

                // 分配新槽位
                inputIndex = currentCount;
                pipeline.mixer.SetInputCount(inputIndex + 1);
            }
            Debug.Log($"[HotPlug]   插入状态Playable到Mixer槽位 {inputIndex}");
            // 连接到流水线Mixer
            playableGraph.Connect(statePlayable, 0, pipeline.mixer, inputIndex);
            pipeline.mixer.SetInputWeight(inputIndex, 1.0f);

            // 记录映射
            pipeline.stateToSlotMap[state] = inputIndex;
            Debug.Log($"[HotPlug]   状态 {state.strKey} 映射到槽位 {inputIndex}");
            
            // 标记Dirty（热插拔 = Dirty2）
            pipeline.MarkDirty(2);
        }

        /// <summary>
        /// 从Playable图中卸载状态（运行时动态移除）- 高性能版本
        /// </summary>
        internal void HotUnplugStateFromPlayable(StateBase state, StatePipelineRuntime pipeline)
        {
            if (state == null || pipeline == null) return;

            // 只有有动画的状态才需要卸载
            if (state.stateSharedData?.hasAnimation != true)
            {
                return;
            }

            // 查找状态对应的槽位
            if (!pipeline.stateToSlotMap.TryGetValue(state, out int slotIndex))
            {
                return; // 未找到，可能未插入过
            }

            // 确保Mixer有效
            if (!pipeline.mixer.IsValid())
            {
                return;
            }

            // 断开连接
            var inputPlayable = pipeline.mixer.GetInput(slotIndex);
            if (inputPlayable.IsValid())
            {
                playableGraph.Disconnect(pipeline.mixer, slotIndex);
            }

            // 清除权重
            pipeline.mixer.SetInputWeight(slotIndex, 0f);

            // 移除映射
            pipeline.stateToSlotMap.Remove(state);

            // 将槽位回收到池中
            pipeline.freeSlots.Push(slotIndex);
            
            // 标记Dirty（热拔插 = Dirty2）
            pipeline.MarkDirty(2);

            // 让StateBase销毁自己的Playable资源（包括嵌套的Mixer等）
            state.DestroyPlayable();
        }

        /// <summary>
        /// 为状态创建Playable节点 - 委托给StateBase处理
        /// StateBase会使用其SharedData中的混合计算器生成Playable
        /// </summary>
        protected virtual Playable CreateStatePlayable(StateBase state, StateAnimationConfigData animConfig)
        {
            if (state == null) return Playable.Null;

            // 委托给StateBase创建Playable
            if (state.CreatePlayable(playableGraph, out Playable output))
            {
                Debug.Log($"[StateMachine] ✓ 状态 {state.strKey} Playable创建成功 | Valid:{output.IsValid()}");
                return output;
            }

            Debug.LogWarning($"[StateMachine] ✗ 状态 {state.strKey} Playable创建失败");
            return Playable.Null;
        }

        /// <summary>
        /// 为状态创建AnimationClipPlayable
        /// </summary>
        protected virtual AnimationClipPlayable CreateClipPlayable(AnimationClip clip)
        {
            if (!playableGraph.IsValid() || clip == null)
            {
                return default;
            }

            return AnimationClipPlayable.Create(playableGraph, clip);
        }

        /// <summary>
        /// 为状态创建AnimationMixerPlayable
        /// </summary>
        protected virtual AnimationMixerPlayable CreateMixerPlayable(int inputCount)
        {
            if (!playableGraph.IsValid())
            {
                return default;
            }

            return AnimationMixerPlayable.Create(playableGraph, inputCount);
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 获取所有状态键（用于编辑器下拉框）
        /// </summary>
        protected IEnumerable<string> GetAllStateKeys()
        {
            return stringToStateMap.Keys;
        }

        /// <summary>
        /// 检查是否完全空闲（所有流水线都没有运行状态）
        /// </summary>
        public bool IsIdle()
        {
            return runningStates.Count == 0;
        }

        /// <summary>
        /// 检查特定流水线是否空闲
        /// </summary>
        public bool IsPipelineIdle(StatePipelineType pipelineType)
        {
            var pipeline = GetPipelineByType(pipelineType);
            return pipeline != null && !pipeline.HasActiveStates;
        }

        /// <summary>
        /// 获取所有运行中的状态数量
        /// </summary>
        public int GetRunningStateCount()
        {
            return runningStates.Count;
        }

        /// <summary>
        /// 获取特定流水线中运行的状态数量
        /// </summary>
        public int GetPipelineStateCount(StatePipelineType pipelineType)
        {
            var pipeline = GetPipelineByType(pipelineType);
            return pipeline != null ? pipeline.runningStates.Count : 0;
        }

        /// <summary>
        /// 获取RootMixer的测试输出信息 - 用于调试动画输出链路
        /// </summary>
        public string GetRootMixerDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("========== RootMixer调试信息 ==========");
            
            // PlayableGraph状态
            sb.AppendLine($"PlayableGraph有效: {playableGraph.IsValid()}");
            if (playableGraph.IsValid())
            {
                sb.AppendLine($"PlayableGraph运行中: {playableGraph.IsPlaying()}");
                sb.AppendLine($"PlayableGraph名称: {playableGraph.GetEditorName()}");
            }
            
            // RootMixer状态
            sb.AppendLine($"\nRootMixer有效: {rootMixer.IsValid()}");
            if (rootMixer.IsValid())
            {
                int inputCount = rootMixer.GetInputCount();
                sb.AppendLine($"RootMixer输入数: {inputCount}");
                
                // 遍历所有输入槽位
                for (int i = 0; i < inputCount; i++)
                {
                    var input = rootMixer.GetInput(i);
                    float weight = rootMixer.GetInputWeight(i);
                    StatePipelineType pipelineType = (StatePipelineType)i;
                    
                    sb.AppendLine($"\n  槽位[{i}] - {pipelineType}:");
                    sb.AppendLine($"    输入有效: {input.IsValid()}");
                    sb.AppendLine($"    权重: {weight:F3}");
                    
                    if (input.IsValid())
                    {
                        // 如果是Mixer，显示其子输入
                        if (input.IsPlayableOfType<AnimationMixerPlayable>())
                        {
                            var mixer = (AnimationMixerPlayable)input;
                            int subInputCount = mixer.GetInputCount();
                            sb.AppendLine($"    子输入数: {subInputCount}");
                            
                            var pipeline = GetPipelineByType(pipelineType);
                            if (pipeline != null)
                            {
                                sb.AppendLine($"    运行状态数: {pipeline.runningStates.Count}");
                            }
                        }
                    }
                }
            }
            
            // Animator输出
            sb.AppendLine($"\nAnimator绑定: {boundAnimator != null}");
            if (boundAnimator != null)
            {
                sb.AppendLine($"Animator启用: {boundAnimator.enabled}");
                sb.AppendLine($"Animator路径: {boundAnimator.gameObject.name}");
            }
            
            sb.AppendLine($"\nOutput有效: {animationOutput.IsOutputValid()}");
            if (animationOutput.IsOutputValid())
            {
                var sourcePlayable = animationOutput.GetSourcePlayable();
                sb.AppendLine($"Output源Playable有效: {sourcePlayable.IsValid()}");
                sb.AppendLine($"Output权重: {animationOutput.GetWeight():F3}");
            }
            
            sb.AppendLine("========================================");
            return sb.ToString();
        }

        #endregion

        #region 调试支持

#if UNITY_EDITOR
        /// <summary>
        /// 获取状态机调试信息
        /// </summary>
        public string GetDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== 状态机调试信息 ==========");
            sb.AppendLine($"状态机ID: {stateMachineKey}");
            sb.AppendLine($"运行状态: {(isRunning ? "运行中" : "已停止")}");
            sb.AppendLine($"宿主Entity: 无");
            sb.AppendLine($"\n========== 上下文信息 ==========");
            sb.AppendLine($"上下文ID: {stateContext?.contextID}");
            sb.AppendLine($"创建时间: {stateContext?.creationTime}");
            sb.AppendLine($"最后更新: {stateContext?.lastUpdateTime}");
            sb.AppendLine($"\n========== 状态统计 ==========");
            sb.AppendLine($"注册状态数(String): {stringToStateMap.Count}");
            sb.AppendLine($"注册状态数(Int): {intToStateMap.Count}");
            sb.AppendLine($"运行中状态总数: {runningStates.Count}");

            sb.AppendLine($"\n========== 流水线状态 ==========");
            foreach (var pipeline in GetAllPipelines())
            {
                sb.AppendLine($"- {pipeline.pipelineType}: {pipeline.runningStates.Count}个状态 | 权重:{pipeline.weight:F2} | {(pipeline.isEnabled ? "启用" : "禁用")}");
                foreach (var state in pipeline.runningStates)
                {
                    sb.AppendLine($"  └─ {state.strKey}");
                }
            }

            return sb.ToString();
        }

        [Button("输出调试信息", ButtonSizes.Large), PropertyOrder(-1)]
        private void DebugPrint()
        {
            Debug.Log(GetDebugInfo());
        }

        [Button("输出所有状态", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintAllStates()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("========== 所有注册状态 ==========");
            foreach (var kvp in stringToStateMap)
            {
                sb.AppendLine($"[{kvp.Key}] -> {kvp.Value.GetType().Name} (运行:{runningStates.Contains(kvp.Value)})");
            }
            Debug.Log(sb.ToString());
        }

        [Button("测试RootMixer输出", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintRootMixer()
        {
            Debug.Log(GetRootMixerDebugInfo());
        }

        [Button("切换持续统计输出", ButtonSizes.Medium), PropertyOrder(-1)]
        [GUIColor("@enableContinuousStats ? new Color(0.4f, 1f, 0.4f) : new Color(0.7f, 0.7f, 0.7f)")]
        private void ToggleContinuousStats()
        {
            enableContinuousStats = !enableContinuousStats;
            Debug.Log($"[StateMachine] 持续统计输出: {(enableContinuousStats ? "开启" : "关闭")}");
        }

        [Button("打印临时动画列表", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugPrintTemporaryAnimations()
        {
            if (_temporaryStates.Count == 0)
            {
                Debug.Log("[TempAnim] 无临时动画");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"========== 临时动画列表 ({_temporaryStates.Count}个) ==========");
            foreach (var kvp in _temporaryStates)
            {
                var state = kvp.Value;
                bool isRunning = runningStates.Contains(state);
                var clip = state.stateSharedData?.animationConfig?.calculator as StateAnimationMixCalculatorForSimpleClip;
                string clipName = clip?.clip?.name ?? "未知";
                sb.AppendLine($"[{kvp.Key}] Clip:{clipName} | 运行:{isRunning}");
            }
            Debug.Log(sb.ToString());
        }

        [Button("一键清除临时动画", ButtonSizes.Medium), PropertyOrder(-1)]
        private void DebugClearTemporaryAnimations()
        {
            ClearAllTemporaryAnimations();
        }
#endif

        #endregion

        #region StateContext便捷访问

        /// <summary>
        /// 设置Float参数 - 用于动画混合（如2D混合的X/Y输入）
        /// </summary>
        public void SetFloat(StateParameter parameter, float value)
        {
            stateContext?.SetFloat(parameter, value);
        }

        /// <summary>
        /// 设置Float参数 - 字符串重载
        /// </summary>
        public void SetFloat(string paramName, float value)
        {
            stateContext?.SetFloat(paramName, value);
        }

        /// <summary>
        /// 获取Float参数
        /// </summary>
        public float GetFloat(StateParameter parameter, float defaultValue = 0f)
        {
            return stateContext?.GetFloat(parameter, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// 获取Float参数 - 字符串重载
        /// </summary>
        public float GetFloat(string paramName, float defaultValue = 0f)
        {
            return stateContext?.GetFloat(paramName, defaultValue) ?? defaultValue;
        }

        #endregion
    }
}
//     #region 最原始定义
//     //原始定义
//     public abstract class BaseOriginalStateMachine : IESHosting<BaseOriginalStateMachine>, IStateMachine, IESModule,IESOriginalModule<BaseOriginalStateMachine>
//     {
//         #region 杂货
//         public static IState NullState = new ESNanoStateMachine_StringKey() { key_ = "空状态" };
//         public static IState NullState_Micro = new BaseESMicroStateOverrideRunTimeLogic_StringKey() { key = "空状态" };
//         [DisplayAsString(FontSize = 25), HideLabel, PropertyOrder(-1)]
//         public string Des_ => "我是一个状态机噢耶";//随便描述一下而已
//         public bool CheckThisStateCanUpdating
//         {
//             get
//             {
//                 if (AsThis != null && AsThis != this)
//                 {
//                     if (!AsThis.CheckThisStateCanUpdating) return false;
//                 }
//                 if (GetHost != null)
//                 {
//                     if (GetHost is IStateMachine machine)
//                     {
//                         if (!machine.CheckThisStateCanUpdating)
//                         {
//                             return false;
//                         }
//                         else if (machine is ESMicroStateMachine_StringKey mS && (mS.SelfRunningStates?.Contains(this) ?? false)) return true;
//                         return false;
//                     }
//                 }
//                 return true;
//             }
//         }

//         //默认进入状态
//         [NonSerialized] public IState StartWithState;
//         [LabelText("设置默认状态的键(这里设置没用)"),ShowInInspector] public object defaultStateKey = null;
//         //Host获取哈
//         protected bool OnSubmitHostingAsNormal(BaseOriginalStateMachine hosting)
//         {
//             host = hosting;
//             return true;
//         }
//         //获取初始状态
//         protected IState GetStartWith()
//         {
//             if (StartWithState != null && StartWithState != NullState)
//             {
//                 return StartWithState;
//             }
//             else
//             {
//                 return GetDefaultState();
//             }
//         }
//         public IState GetDefaultState()
//         {
//             if (defaultStateKey == null) return null;
//             return GetStateByKey(defaultStateKey);
//         }
//         public bool IsIdle()
//         {
//             return !IsStateNotNull(_SelfRunningState);
//         }
//         public IState AsThis
//         {
//             get => thisState;
//             set { /*Debug.LogWarning("修改状态机自状态是危险的，但是允许");*/ thisState = value; }
//         }

//         [LabelText("状态机自基准状态"), SerializeReference, FoldoutGroup("子状态")] public IState thisState = NullState_Micro;//基准状态不准是纳米的，起码是微型的罢

//         #endregion

//         #region 父子关系与保持的运行
//         //基准状态
//         //获得根状态机--非常好的东西
//         public BaseOriginalStateMachine Root { get { if (GetHost is BaseOriginalStateMachine machine) return machine.Root; else return this; } }

//         public IState SelfRunningMainState { get => _SelfRunningState; set => _SelfRunningState = value; }
//         [SerializeReference, LabelText("当前运行状态"), FoldoutGroup("子状态")] public IState _SelfRunningState = null;
//         //总状态机(冲突合并需要的)--只有标准状态机需要
//         /*[SerializeReference, LabelText("总状态机运行中状态")]
//         public HashSet<IESNanoState> MainRunningStates;*/

//         //自己掌控运行--至少是微型状态机才有
//         /**/
//         #endregion

//         #region 生命周期_状态专属的
//         [ShowInInspector, ReadOnly, LabelText("状态进入 是/否"), FoldoutGroup("是否状况收集")]
//         public bool IsRunning { get; set; }
//         public void OnStateEnter()
//         {
//             if (IsRunning) return;
//             IsRunning = true;//状态更新
//             /*MainRunningStates = Root.MainRunningStates;//使用统一的根处引用*/
//             AsThis?.OnStateEnter();//自基准状态
//             _Expand_PreparedHappenDesign();//扩展Prepare时间
//             /*_SelfRunningStates?.Update();//自己层级下的运行中状态--子状态机也是状态哈--这个更新就是删除多余的的加入新的
//              */
//             if (host is ESMicroStateMachine_StringKey parent)//您是微型状态机
//             {
//                 parent._SelfRunningState = this;
//                 parent._SelfRunningStates.TryAdd(this);//如果自己是子，那么也要加入到父级的运行状态中
//             }
//             OnStateEnter();//状态机没啥好额外准备的罢
//         }
//         //进入时--都不是必要功能
//         protected virtual void OnStateEnter()
//         {
//             IState state = GetStartWith();
//             if (state != null)
//             {
//                 bool b = TryActiveState(state);
//                 if (b == false) OnStateExit();
//                 StartWithState = null;
//             }
//         }

//         //退出时,状态退出
//         public virtual void OnStateExit()
//         {
//             if (!IsRunning) return;
//             IsRunning = false;//状态更新

//             AsThis?.OnStateExit();//自基准状态


//             _Expand_ExitHappenDesign();//扩展Exit时机

//             if (host is ESMicroStateMachine_StringKey parent)
//             {
//                 if (parent._SelfRunningState == this)
//                 {
//                     parent._SelfRunningState = null;
//                 }
//                 parent._SelfRunningStates.TryRemove(this);//如果自己是子，那么也要从父级的运行状态中移除
//             }
//             _SelfRunningState = NullState;
//         }
//         //更新时--状态更新
//         public void OnStateUpdate()
//         {
//             AsThis?.OnStateUpdate();
//             _Expand_UpdateHappenDesign();//扩展更新Update的时机
//             /* _SelfRunningStates?.Update();
//              if (_SelfRunningStates != null)
//              {
//                  foreach (var i in _SelfRunningStates.valuesNow_)
//                  {
//                      if (i != null)
//                      {
//                          if(i is IESMicroState ESMicro)
//                          {
//                              if (ESMicro.RunningStatus != EnumStateRunningStatus.StateUpdate) continue;
//                          }
//                          else if(!i.HasPrepared)
//                          {
//                              continue;
//                          }
//                          i.OnStateUpdate();
//                      }
//                  }
//              }*/
//         }
//         #endregion

//         #region 生命周期_作为模块的
//         protected override void OnEnable()
//         {
//             base.OnEnable();
//             if (GetHost is BaseOriginalStateMachine machine) { }//如果有父状态机--》受父级更新控制
//             else OnStateEnter();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }

//         protected override void OnDisable()
//         {
//             base.OnDisable();
//             if (GetHost is BaseOriginalStateMachine machine) { }//如果有父状态机--》受父级更新控制
//             else OnStateExit();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }
//         protected override void Update()
//         {
//             base.Update();
//             if (GetHost is BaseOriginalStateMachine machine) { Debug.Log(2); }//如果有父状态机--》受父级更新控制
//             else OnStateUpdate();//如果没有父级或者父级不是状态机--》自己就完成控制了
//         }
//         #endregion

//         #region 对基准状态的包装
//         public object key_;
//         public void SetKey(object key)
//         {
//             AsThis?.SetKey(key);
//             key_ = key;
//         }
//         public object GetKey()
//         {
//             if (!IsStateNotNull(AsThis)) return key_;
//             return AsThis.GetKey();
//         }
//         public bool IsStateNotNull(IState state)
//         {
//             if (state == null || state == NullState || state == NullState_Micro) return false;
//             return true;
//         }

//         public EnumStateRunningStatus RunningStatus => AsThis?.RunningStatus??(IsRunning?EnumStateRunningStatus.StateUpdate:EnumStateRunningStatus.StateExit);
//         IStateSharedData IState.SharedData
//         {
//             get => AsThis?.SharedData; set { if (AsThis != null) AsThis.SharedData = value; }
//         }
//         IStateVariableData IState.VariableData
//         {
//             get => AsThis?.VariableData; set { if (AsThis != null) AsThis.VariableData = value; }
//         }
//         #endregion

//         #region 状态切换支持
//         public abstract bool TryActiveState(IState use);//通用
//         public abstract bool TryInActiveState(IState use);//通用
//         public abstract bool TryActiveStateByKey(object key_);//通用
//         public abstract string[] KeysWithLayer(string atFirst="");
//         public abstract void RegisterNewState_Original(object key, IState aState);
//         public abstract bool TryInActiveState(object key_);
//         public abstract bool TryInActiveStateByKey(string key_);
//         public abstract IState GetStateByKey(object o);
//         public abstract IState GetStateByKey(string o);
//         #endregion

//         #region 设计层扩展重写状态机逻辑
//         protected virtual void _Expand_PreparedHappenDesign()
//         {
//             host._SelfRunningState = this;
//         }
//         protected virtual void _Expand_UpdateHappenDesign()
//         {
//             if (_SelfRunningState != null)
//             {
//                 _SelfRunningState.OnStateUpdate();
//             }
//             else
//             {
//                 if (IsIdle())
//                 {
//                     var default_ = GetDefaultState();
//                     if (default_ != null)
//                     {
//                        bool b= TryActiveState(default_);
//                         if (b == false) OnStateExit();
//                     }
//                 }
//             }
//         }
//         protected virtual void _Expand_ExitHappenDesign()
//         {

//             if (_SelfRunningState != null)
//             {
//                 _RightlyExitTheState(_SelfRunningState);
//             }
//         }
//         #endregion

//         #region 测试方法
//         [Button("初始化"), FoldoutGroup("状态机测试按钮"), Tooltip("定义初始化的状态")]
//         public void WithEnterState(IState state)
//         {
//             if (state != null)
//                 StartWithState = state;
//         }


//         #endregion

//         #region 注册状态


//         #endregion

//         #region 状态切换辅助等
//         protected void _RightlyPreparedTheState(IState use)
//         {
//             if (use != null)
//             {
//                 use.OnStateEnter();
//                 _SelfRunningState = use;
//             }
//         }
//         protected void _RightlyExitTheState(IState use)
//         {
//             if (use != null)
//             {
//                 if (_SelfRunningState == use) { _SelfRunningState = null; };
//                 use.OnStateExit();
//             }
//         }

//         bool IESOriginalModule<BaseOriginalStateMachine>.OnSubmitHosting(BaseOriginalStateMachine host)
//         {
//             this.host = host;
//             return true;
//         }
//         #endregion
//     }
//     #endregion

//     #region 纳米级别状态机--空的-泛型键-状态机-只能是纳米了--给键哈
//     public abstract class BaseESNanoStateMachine<Key_> : BaseOriginalStateMachine
//     {
//         #region 杂货与基准定义
//         #endregion

//         #region 字典表
//         public override string[] KeysWithLayer(string atFirst)
//         {
//             List<string> all = new List<string>();

//             //加键
//             foreach (var i in allStates.Keys)
//             {
//                 all.Add(atFirst + i);
//             };
//             //遍历
//             foreach (var i in allStates.Values)
//             {
//                 if (i is BaseOriginalStateMachine machine)
//                 {
//                     all.AddRange(machine.KeysWithLayer(atFirst + i.GetKey()));
//                 }
//             }
//             return all.ToArray();
//         }
//         [SerializeReference, LabelText("全部状态字典")]
//         public Dictionary<Key_, IState> allStates = new Dictionary<Key_, IState>();
//         public Dictionary<Key_, IState> AllStates => allStates;
//         public override IState GetStateByKey(object o)
//         {
//             if(o is Key_ thekey&&allStates.ContainsKey(thekey))
//             {
//                 return allStates[thekey];
//             }
//             return null;
//         }
//         public override IState GetStateByKey(string s)
//         {
//             if (s is Key_ thekey && allStates.ContainsKey(thekey))
//             {
//                 return allStates[thekey];
//             }
//             var use = ESDesignUtility.Matcher.SystemObjectToT<Key_>(s);
//             if (use != null && allStates.ContainsKey(use))
//             {
//                 return allStates[use];
//             }
//             return null;
//         }
//         #endregion

//         #region 定义

//         #endregion
//         /* [GUIColor("@OLDESDesignUtilityOLD.ColorSelector.Color_03"),LabelText("当前状态")]*/
//         /* public IESMicroState CurrentState =>currentFirstState?? NullState;*/


//         [Button("测试切换状态"), Tooltip("该方法建议用于运行时，不然得话会立刻调用状态的Enter，请切换使用WithEnterState")]
//         public void TryActiveStateByKey(Key_ key, IState ifNULL = null)
//         {
//             if (allStates == null)
//             {
//                 allStates = new Dictionary<Key_, IState>();
//                 if (ifNULL != null)
//                 {
//                     RegisterNewState_Original(key, ifNULL);
//                     OnlyESNanoPrivate_SwitchStateToFromByKey(key);
//                     //新建 没啥
//                 }
//                 return;
//             }
//             if (allStates.ContainsKey(key))
//             {
//                 TryActiveState(allStates[key]);
//             }
//             else
//             {
//                 if (ifNULL != null)
//                 {
//                     RegisterNewState_Original(key, ifNULL);
//                     TryActiveState(ifNULL);
//                 }
//             }
//         }
//         public override bool TryActiveStateByKey(object key_)
//         {
//             if(key_ is Key_ key_1)
//             {
//                 return TryActiveStateByKey(key_1);
//             }
//             return false;
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFromByKey(Key_ to, Key_ from = default)
//         {
//             if (allStates.ContainsKey(to))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(allStates[to]);
//             }
//         }
//         protected void OnlyESNanoPrivate_SwitchStateToFrom(IState to, IState from = default)
//         {
//             if (allStates.Values.Contains(to))
//             {
//                 IState willUse = to;
//                 if (SelfRunningMainState == willUse) return;//同一个？无意义

//                 _RightlyExitTheState(SelfRunningMainState);//过去的真退了

//                 _RightlyPreparedTheState(to);//我真来了

//                 if (SelfRunningMainState == null)
//                 {
//                     Debug.LogError("状态为空！键是" + to.GetKey());
//                 }
//             }
//         }

//         public bool TryActiveStateByKeyWithLayer(string layerKey)
//         {
//             if (layerKey.Contains('/')||layerKey.Contains('?')||layerKey.Contains(".."))
//             {
//                 string[] layers = layerKey.Split('/');
//                 BaseOriginalStateMachine TheMachine = this;
//                 IState TheState=null;
//                 Queue<IState> TheStates=null;
//                 bool endBack = true;

//                 for(int index = 0; index < layers.Length; index++)
//                 {
//                     string i = layers[index];
//                     Debug.Log("by" + i+index);
//                     if (i == "..")//回退一级
//                     {
//                         Debug.Log("回退");
//                         var parent = TheMachine.GetHost as BaseOriginalStateMachine;
//                         if (parent != null)
//                         {
//                             TheMachine = parent;
//                             continue;
//                         }
//                     }
//                     var aState = i;
//                     TheStates = null;
//                     if (i.Contains('?'))
//                     {
//                         TheStates = new Queue<IState>();
//                         string[] Addstates = i.Split('?');
//                         foreach (var a in Addstates)
//                         {
//                             var state = TheMachine.GetStateByKey(a);
//                             if (state != null)
//                                 TheStates.Enqueue(state);
//                         }
//                         aState = Addstates[0];
//                     }

//                     TheState = TheMachine.GetStateByKey(aState);

//                     if (TheState != null)
//                     {
//                         if (index == layers.Length - 1) { Debug.Log("last" + index+ TheState.GetKey()); break; }
//                         if (TheState is BaseOriginalStateMachine nextMachine)
//                         {
//                             TheMachine = nextMachine;
//                             Debug.Log("next"+index);
//                             continue;
//                         }
//                         else
//                         {
//                             Debug.Log("state" + index);
//                             break;
//                         }
//                     }
//                     else
//                     {
//                         Debug.Log("null" + index);
//                         endBack = false;
//                         break;
//                     }
//                 }
//                /* foreach(var i in layers)
//                 {

//                 }*/
//                 if (TheStates != null&&TheStates.Count>0)
//                 {
//                     while (TheStates.Count > 0)
//                     {
//                         var use = TheStates.Dequeue();
//                         if (TheMachine.TryActiveState(use)) return true;
//                     }
//                 }
//                 else if (TheState != null)
//                 {
//                    return TheMachine.TryActiveState(TheState);
//                 }
//                 return endBack;
//             }
//             else
//             {
//                return TryActiveStateByKey(layerKey);
//             }
//         }
//         public bool TryInActiveStateByKeyWithLayer(string layerKey)
//         {
//             if (layerKey.Contains('/') || layerKey.Contains('?') || layerKey.Contains(".."))
//             {
//                 string[] layers = layerKey.Split('/');
//                 BaseOriginalStateMachine TheMachine = this;
//                 IState TheState = null;
//                 Queue<IState> TheStates = null;
//                 bool endBack = true;

//                 for (int index = 0; index < layers.Length; index++)
//                 {
//                     string i = layers[index];
//                     Debug.Log("by" + i + index);
//                     if (i == "..")//回退一级
//                     {
//                         Debug.Log("回退");
//                         var parent = TheMachine.GetHost as BaseOriginalStateMachine;
//                         if (parent != null)
//                         {
//                             TheMachine = parent;
//                             continue;
//                         }
//                     }
//                     var aState = i;
//                     TheStates = null;
//                     if (i.Contains('?'))
//                     {
//                         TheStates = new Queue<IState>();
//                         string[] Addstates = i.Split('?');
//                         foreach (var a in Addstates)
//                         {
//                             var state = TheMachine.GetStateByKey(a);
//                             if (state != null)
//                                 TheStates.Enqueue(state);
//                         }
//                         aState = Addstates[0];
//                     }

//                     TheState = TheMachine.GetStateByKey(aState);

//                     if (TheState != null)
//                     {
//                         if (index == layers.Length - 1) break;
//                         if (TheState is BaseOriginalStateMachine nextMachine)
//                         {
//                             TheMachine = nextMachine;
//                             Debug.Log("next" + index);
//                             continue;
//                         }
//                         else
//                         {
//                             Debug.Log("state" + index);
//                             break;
//                         }
//                     }
//                     else
//                     {
//                         Debug.Log("null" + index);
//                         endBack = false;
//                         break;
//                     }
//                 }
//                 /* foreach(var i in layers)
//                  {

//                  }*/
//                 if (TheStates != null && TheStates.Count > 0)
//                 {
//                     while (TheStates.Count > 0)
//                     {
//                         var use = TheStates.Dequeue();
//                         if (TheMachine.TryInActiveState(use)) return true;
//                     }
//                 }
//                 else if (TheState != null)
//                 {
//                     return TheMachine.TryInActiveState(TheState);
//                 }
//                 return endBack;
//             }
//             else
//             {
//                 return TryInActiveStateByKey(layerKey);
//             }
//         }
//         public override bool TryInActiveState(IState use)
//         {
//             if (allStates.ContainsValue(use))
//             {
//                 _RightlyExitTheState(use);
//             }
//             return false;
//         }
//         public bool TryActiveStateByKey(string key_)
//         {
//             if (allStates.ContainsKey(key_))
//             {

//                 return TryActiveState(allStates[key_]);
//             }
//             return false;
//         }

//         public override bool TryActiveStateByKey(object key_)
//         {
//             return TryActiveStateByKey(key_.ToString());
//         }

//         public override bool TryActiveState(IState use)
//         {
//             if (use.IsRunning) return true;
//             if (!this.IsRunning && host is BaseOriginalStateMachine originalStateMachine)
//             { 
//                 this.WithEnterState(use);
//                 return originalStateMachine.TryActiveState(this);
//             }
//             if (allStates.Values.Contains(use))
//             {
//                 OnlyESNanoPrivate_SwitchStateToFrom(use);
//                 return true;
//             }
//             else
//             {
//                 if (use.GetKey().ToString() == "玩家格挡")
//                 {
//                     Debug.Log("RESIST-SI");
//                 }
//                 Debug.LogError("暂时不支持活动为注册的状态");
//                 return false;
//             }
//         }
//         #endregion

//         #region 注册注销
//         public override void RegisterNewState_Original(object key, IState aState)
//         {
//             RegisterNewState(key.ToString(), aState);
//         }
//         public void RegisterNewState(string key, IState logic)
//         {
//             if (allStates.ContainsKey(key))
//             {
//                // Debug.LogError("重复注册状态?键是" + key);
//             }
//             else
//             {
//                 allStates.Add(key, logic);
//                 logic.SetKey(key);
//                 if (StartWithState == null || StartWithState == NullState)
//                 {
//                     //新的
//                     StartWithState = logic;
//                 }
//                 if (logic is IESOriginalModule<BaseOriginalStateMachine> logic1)
//                 {
//                     logic1.OnSubmitHosting(this);
//                     //Debug.Log("注册成功？" + logic.GetKey());
//                 }
//                 else if (logic is BaseOriginalStateMachine machine)
//                 {
//                     machine.TrySubmitHosting(this, false);
//                     //Debug.Log("注册状态机成功？" + logic.GetKey());
//                 }
//                 else
//                 {
//                     Debug.Log("啥也不是？");
//                 }

//             }
//         }


//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             base._Expand_PreparedHappenDesign();
//         }

//         public override bool TryInActiveState(object key_)
//         {
//            return TryInActiveStateByKey(key_.ToString());
//         }

//         public override bool TryInActiveStateByKey(string key_)
//         {
//             Debug.Log("尝试关闭" + key_);
//             var it = GetStateByKey(key_);
//             if (it == null) return false;
//             TryInActiveState(it);
//             return false;
//         }

//         #endregion
//     }
//     //微型状态机定义
//     [Serializable, TypeRegistryItem("微型状态机(String)")]
//     public class ESMicroStateMachine_StringKey : ESNanoStateMachine_StringKey, IStateMachine
//     {
//         #region 当前并行
//         [LabelText("自己的运行中状态"),FoldoutGroup("子状态")]
//         public SafeUpdateList_EasyQueue_SeriNot_Dirty<IState> _SelfRunningStates = new SafeUpdateList_EasyQueue_SeriNot_Dirty<IState>();
//         public IEnumerable<IState> SelfRunningStates => _SelfRunningStates.valuesNow_;
//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             // base._Expand_PreparedHappenDesign();
//         }
//         protected override void _Expand_UpdateHappenDesign()
//         {
//             if (_SelfRunningStates != null)
//             {
//                 _SelfRunningStates.Update();
//                 int count = _SelfRunningStates.valuesNow_.Count;
//                 if (count == 0)
//                 {
//                     if (IsIdle())
//                     {
//                         var default_ = GetDefaultState();
//                         if (default_ != null)
//                         {
//                             bool b = TryActiveState(default_);
//                             if (b == false)
//                             {
//                                 if (GetHost is not BaseOriginalStateMachine machine) return;//最高级您
//                                 OnStateExit();
//                             }
//                         }
//                         else
//                         {
//                             if (GetHost is not BaseOriginalStateMachine machine) return;//最高级您
//                             OnStateExit();
//                         }
//                     }
//                 }
//                 else
//                 {

//                     for (int iq = 0; iq < count; iq++)
//                     {
//                         var i = _SelfRunningStates.valuesNow_[iq];
//                         /*                    if (i is IESMicroState ESMicro)
//                                             {
//                                                 if (ESMicro.RunningStatus != EnumStateRunningStatus.StateUpdate) continue;
//                                             }
//                                             else if (!i.HasPrepared)
//                                             {
//                                                 continue;
//                                             }*/
//                         if (i.RunningStatus == EnumStateRunningStatus.StateUpdate)
//                             i.OnStateUpdate();
//                     }
//                 }

//             }





//             //base._Expand_UpdateHappenDesign();不需要您
//         }
//         protected override void _Expand_ExitHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             if (_SelfRunningStates != null)
//             {
//                 foreach (var i in _SelfRunningStates.valuesNow_)
//                 {
//                     if (i != null)
//                     {
//                         _RightlyExitTheState(i);
//                     }
//                 }
//             }
//             //base._Expand_ExitHappenDesign();
//         }
//         #endregion

//         #region Active重写
//         public override bool TryActiveState(IState use)
//         {
//             if (use is IState ESMicro)
//             {
//                 //空状态：直接使用
//                 if (SelfRunningStates.Count() == 0) return base.TryActiveState(ESMicro);
//                 //已经包含-就取消
//                 if (SelfRunningStates.Contains(ESMicro))
//                 {
//                     return ESMicro.IsRunning;
//                 }

//                 Debug.Log("-----------《《《《合并测试开始------来自" + ESMicro.GetKey().ToString());
//                 //单状态，简易判断
//                 if (SelfRunningStates.Count() == 1)
//                 {
//                     IState state = SelfRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                       //  Debug.Log("单-合并--测试");
//                         if (state.SharedData is IStateSharedData left && ESMicro.SharedData is IStateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = ESMicro.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 ESMicro.OnStateEnter();
//                                 _SelfRunningState = ESMicro;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                                 return true;
//                             }
//                             else if (back == HandleMergeBack.MergeComplete)
//                             {
//                                 ESMicro.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                                 return true;
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                                 return false;
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(ESMicro);
//                             Debug.Log("不具有");
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (ESMicro.SharedData is IStateSharedData right)
//                     {
//                         string rightKey = ESMicro.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in SelfRunningStates)
//                         {
//                             if (i.SharedData is IStateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left, right, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                    // Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                       //  Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         ESMicro.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                           //  Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                         //    Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                         return true;
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(ESMicro);
//                     }
//                 }
//                 return false;
//             }
//             return base.TryActiveState(use);
//         }

//         #endregion
//     }

//     //标准状态机
//     [Serializable, TypeRegistryItem("标准状态机(String)")]
//     public class ESStandardStateMachine_StringKey : ESMicroStateMachine_StringKey, IStateMachine
//     {
//         #region 总状态机
//         [OdinSerialize, LabelText("总状态机运行中状态"),ShowInInspector, FoldoutGroup("子状态")]
//         public HashSet<IState> MainRunningStates=new HashSet<IState>();
//         public HashSet<IState> RootAllRunningStates => MainRunningStates;
//         #endregion

//         #region 设计层
//         protected override void _Expand_PreparedHappenDesign()
//         {
//             if (MainRunningStates ==null&&Root == this)
//             {
//                 MainRunningStates = new HashSet<IState>();
//             }
//             if (Root is ESStandardStateMachine_StringKey standMachine)
//             {
//                 MainRunningStates = standMachine.MainRunningStates;
//             }
//             base._Expand_PreparedHappenDesign();
//         }
//         protected override void OnEnable()
//         {
//             base.OnEnable();
//             if (Root is ESStandardStateMachine_StringKey standMachine)
//             {
//                 MainRunningStates = standMachine.MainRunningStates;
//             }
//         }
//       /*  protected override void _Expand_UpdateHappenDesign()
//         {
//             base._Expand_UpdateHappenDesign();//照常更新
//         }*/
//         protected override void _Expand_ExitHappenDesign()
//         {
//             _SelfRunningStates?.Update();
//             base._Expand_ExitHappenDesign();//照常更新
//         }
//         #endregion

//         #region Active重写
//         public override bool TryActiveState(IState use)
//         {

//             if (use is IState stand)
//             {
//                 //空状态：直接使用
//                 if (RootAllRunningStates.Count == 0) { return base.TryActiveState(stand); }
//                 //已经包含-就取消
//                 if (RootAllRunningStates.Contains(stand)||SelfRunningStates.Contains(stand))
//                 {
//                     return stand.IsRunning;
//                 }

//                 //Debug.Log("-----------《《《《合并测试开始------来自" + stand.GetKey().ToString());
//                 //单状态，简易判断
//                 if (RootAllRunningStates.Count == 1)
//                 {
//                     IState state = RootAllRunningStates.First();
//                     {
//                         //state的共享数据有的不是标准的哈/
//                         //标准情形
//                       //  Debug.Log("单-合并--测试");
//                         if (state.SharedData is IStateSharedData left && stand.SharedData is IStateSharedData right)
//                         {
//                             string leftKey = state.GetKey().ToString();
//                             string rightKey = stand.GetKey().ToString();
//                             var back = StateSharedData.HandleMerge(left.MergePart_, right.MergePart_, leftKey, rightKey);
//                             if (back == HandleMergeBack.HitAndReplace)
//                             {
//                                 state.OnStateExit();
//                                 stand.OnStateEnter();
//                                 _SelfRunningState = stand;
//                                 Debug.Log("单-合并--打断  原有的  " + leftKey + " 被 新的  " + rightKey + "  打断!");
//                                 return true;
//                             }
//                             else if (back == HandleMergeBack.MergeComplete)
//                             {
//                                 stand.OnStateEnter();
//                                 Debug.Log("单-合并--成功  原有的  " + leftKey + " 和  新的  " + rightKey + "  合并!");
//                                 return true;
//                             }
//                             else //合并失败
//                             {
//                                 Debug.Log("单-合并--失败  原有的  " + leftKey + " 阻止了  新的  " + rightKey + "  !");
//                                 return false;
//                             }
//                         }
//                         else //有的不是标准状态
//                         {
//                             base.TryActiveState(stand);
//                         }
//                     }
//                 }
//                 else  //多项目
//                 {
//                     if (stand.SharedData is IStateSharedData right)
//                     {
//                         string rightKey = stand.GetKey().ToString();
//                         List<IState> hit = new List<IState>();
//                         List<string> merge = new List<string>();
//                         foreach (var i in RootAllRunningStates)
//                         {
//                             if (i.SharedData is IStateSharedData left)
//                             {
//                                 string leftKey = i.GetKey().ToString();
//                                 var back = StateSharedData.HandleMerge(left, right, leftKey, rightKey);
//                                 if (back == HandleMergeBack.HitAndReplace)
//                                 {
//                                     hit.Add(i);

//                                     //打断一个捏
//                                 }
//                                 else if (back == HandleMergeBack.MergeComplete)
//                                 {
//                                     //正常的
//                                     merge.Add(leftKey);

//                                 }
//                                 else //合并失败
//                                 {
//                                    // Debug.LogWarning("多-合并--失败" + leftKey + " 阻止了 " + rightKey + "的本次合并测，无事发生试!");
//                                     return false;
//                                 }
//                             }
//                         }
//                         //成功合并了
//                       //  Debug.Log("---√多-合并--完全成功！来自" + rightKey + "以下是细则：");
//                         stand.OnStateEnter();
//                         foreach (var i in merge)
//                         {
//                           //  Debug.Log("     --合并细则  本次合并-合并了了" + i);
//                         }
//                         foreach (var i in hit)
//                         {
//                         //    Debug.Log("     --合并细则  本次合并-打断了" + i.GetKey());
//                             i.OnStateExit();
//                         }
//                         return true;
//                     }
//                     else //不是标准状态滚
//                     {
//                         base.TryActiveState(stand);
//                     }
//                 }
//                 return false;
//             }
//             return base.TryActiveState(use);
//         }

//         #endregion

//         #region 测试方法

//         [Button("输出全部状态"), FoldoutGroup("状态机测试按钮")]
//         public void Test_OutPutAllStateRunning(string befo = "状态机：")
//         {
//             string all = befo + "现在运行的有：";
//             foreach (var i in MainRunningStates)
//             {
//                 all += i.GetKey() + " , ";
//             }
//             /*Debug.LogWarning(all);*/
//         }
//         #endregion
//     }

//     #endregion


// }
