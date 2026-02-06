using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ES
{
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
        public byte priority = 0;

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
        [NonSerialized, ShowInInspector]
        public Dictionary<StateBase, int> stateToSlotMap = new Dictionary<StateBase, int>(64);

        /// <summary>
        /// 正在淡入的状态字典 - 用于淡入效果更新
        /// </summary>
        [NonSerialized]
        public Dictionary<StateBase, StateFadeData> fadeInStates = new Dictionary<StateBase, StateFadeData>();

        /// <summary>
        /// 正在淡出的状态字典 - 用于淡出效果更新
        /// </summary>
        [NonSerialized]
        public Dictionary<StateBase, StateFadeData> fadeOutStates = new Dictionary<StateBase, StateFadeData>();

        /// <summary>
        /// 淡入移除缓存（零GC）
        /// </summary>
        [NonSerialized]
        public List<StateBase> fadeInToRemoveCache = new List<StateBase>(8);

        /// <summary>
        /// 淡出移除缓存（零GC）
        /// </summary>
        [NonSerialized]
        public List<StateBase> fadeOutToRemoveCache = new List<StateBase>(8);

        /// <summary>
        /// 槽位映射排序缓存（零GC）
        /// </summary>
        [NonSerialized]
        private readonly List<KeyValuePair<StateBase, int>> _stateToSlotListCache = new List<KeyValuePair<StateBase, int>>(64);

        /// <summary>
        /// 最大预分配槽位数 - 避免无限增长
        /// </summary>
        [LabelText("最大Playable槽位")]
        public int maxPlayableSlots = 32;

        // ===== FallBack支持标记系统（每个流水线独立配置）=====
        [LabelText("Grounded FallBack")]
        public int FallBackForGrounded = -1;
        [LabelText("Crouched FallBack")]
        public int FallBackForCrouched = -1;
        [LabelText("Prone FallBack")]
        public int FallBackForProne = -1;
        [LabelText("Swimming FallBack")]
        public int FallBackForSwimming = -1;
        [LabelText("Flying FallBack")]
        public int FallBackForFlying = -1;
        [LabelText("Mounted FallBack")]
        public int FallBackForMounted = -1;
        [LabelText("Climbing FallBack")]
        public int FallBackForClimbing = -1;
        [LabelText("SpecialInteraction FallBack")]
        public int FallBackForSpecialInteraction = -1;
        [LabelText("Observer FallBack")]
        public int FallBackForObserver = -1;
        [LabelText("Dead FallBack")]
        public int FallBackForDead = -1;
        [LabelText("Transition FallBack")]
        public int FallBackForTransition = -1;

        // ===== Dirty机制（流水线级别的脏标记）=====
        /// <summary>
        /// 流水线Dirty标记（独立运作，不再使用等级制）
        /// </summary>
        [LabelText("Dirty标记"), ShowInInspector, ReadOnly]
        [NonSerialized]
        public PipelineDirtyFlags dirtyFlags = PipelineDirtyFlags.None;

        /// <summary>
        /// 上次Dirty时间（用于自动衰减）
        /// </summary>
        [NonSerialized]
        private float lastDirtyTime = 0f;

        /// <summary>
        /// 是否为Dirty状态
        /// </summary>
        public bool IsDirty => dirtyFlags != PipelineDirtyFlags.None;

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
        /// 获取指定状态在本流水线Mixer中的当前权重
        /// </summary>
        public float GetStateWeight(StateBase state)
        {
            if (state == null || !mixer.IsValid()) return 0f;
            if (!stateToSlotMap.TryGetValue(state, out int slot)) return 0f;
            if (slot < 0 || slot >= mixer.GetInputCount()) return 0f;
            return mixer.GetInputWeight(slot);
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

            // 热拔插从Playable图
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
        /// 获取指定支持标记的FallBack状态ID
        /// 当流水线运行状态为空时，返回此状态ID作为默认回退状态
        /// </summary>
        /// <param name="supportFlag">支持标记（传 None 使用 StateMachine 当前SupportFlags）</param>
        /// <returns>FallBack状态ID，-1表示无FallBack</returns>
        public int GetFallBack(StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var originalFlag = supportFlag;
            if (supportFlag == StateSupportFlags.None)
            supportFlag = stateMachine != null ? stateMachine.currentSupportFlags : StateSupportFlags.Grounded;

            supportFlag = NormalizeSingleFlag(supportFlag);

            int result;
            switch (supportFlag)
            {
                case StateSupportFlags.Grounded: result = FallBackForGrounded; break;
                case StateSupportFlags.Crouched: result = FallBackForCrouched; break;
                case StateSupportFlags.Prone: result = FallBackForProne; break;
                case StateSupportFlags.Swimming: result = FallBackForSwimming; break;
                case StateSupportFlags.Flying: result = FallBackForFlying; break;
                case StateSupportFlags.Mounted: result = FallBackForMounted; break;
                case StateSupportFlags.Climbing: result = FallBackForClimbing; break;
                case StateSupportFlags.SpecialInteraction: result = FallBackForSpecialInteraction; break;
                case StateSupportFlags.Observer: result = FallBackForObserver; break;
                case StateSupportFlags.Dead: result = FallBackForDead; break;
                case StateSupportFlags.Transition: result = FallBackForTransition; break;
                default:
#if STATEMACHINEDEBUG
                        Debug.LogWarning($"[FallBack-Get] ⚠ [{pipelineType}] SupportFlag无效({supportFlag})，回退到Grounded");
#endif
                        result = ResolveFallBackByFlag(StateSupportFlags.Grounded);
                    break;
            }

#if STATEMACHINEDEBUG
            Debug.Log($"[FallBack-Get] [{pipelineType}] GetFallBack(原始Flag={originalFlag}, 实际Flag={supportFlag}) -> StateID={result}");
#endif
            return result;
        }

        /// <summary>
        /// 设置指定支持标记的FallBack状态ID
        /// </summary>
        /// <param name="stateID">状态ID，-1表示无FallBack</param>
        /// <param name="supportFlag">支持标记（传 None 使用 StateMachine 当前SupportFlags）</param>
        public void SetFallBack(int stateID, StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            var originalFlag = supportFlag;
            if (supportFlag == StateSupportFlags.None)
            supportFlag = stateMachine != null ? stateMachine.currentSupportFlags : StateSupportFlags.Grounded;

            supportFlag = NormalizeSingleFlag(supportFlag);

#if STATEMACHINEDEBUG
            Debug.Log($"[FallBack-Set] [{pipelineType}] SetFallBack(StateID={stateID}, 原始Flag={originalFlag}, 实际Flag={supportFlag})");
#endif

            switch (supportFlag)
            {
                case StateSupportFlags.Grounded: FallBackForGrounded = stateID; break;
                case StateSupportFlags.Crouched: FallBackForCrouched = stateID; break;
                case StateSupportFlags.Prone: FallBackForProne = stateID; break;
                case StateSupportFlags.Swimming: FallBackForSwimming = stateID; break;
                case StateSupportFlags.Flying: FallBackForFlying = stateID; break;
                case StateSupportFlags.Mounted: FallBackForMounted = stateID; break;
                case StateSupportFlags.Climbing: FallBackForClimbing = stateID; break;
                case StateSupportFlags.SpecialInteraction: FallBackForSpecialInteraction = stateID; break;
                case StateSupportFlags.Observer: FallBackForObserver = stateID; break;
                case StateSupportFlags.Dead: FallBackForDead = stateID; break;
                case StateSupportFlags.Transition: FallBackForTransition = stateID; break;
                default:
#if STATEMACHINEDEBUG
                    Debug.LogError($"[FallBack-Set] ✗ [{pipelineType}] 无效的SupportFlag: {supportFlag}");
#endif
                    break;
            }
        }

        /// <summary>
        /// 检查指定支持标记是否配置了FallBack状态
        /// </summary>
        public bool HasFallBack(StateSupportFlags supportFlag = StateSupportFlags.None)
        {
            return GetFallBack(supportFlag) >= 0;
        }

        private static StateSupportFlags NormalizeSingleFlag(StateSupportFlags flag)
        {
            if (flag == StateSupportFlags.None) return StateSupportFlags.None;

            ushort value = (ushort)flag;
            ushort lowest = (ushort)(value & (ushort)(-(short)value));
            return (StateSupportFlags)lowest;
        }

        private int ResolveFallBackByFlag(StateSupportFlags flag)
        {
            switch (flag)
            {
                case StateSupportFlags.Grounded: return FallBackForGrounded;
                case StateSupportFlags.Crouched: return FallBackForCrouched;
                case StateSupportFlags.Prone: return FallBackForProne;
                case StateSupportFlags.Swimming: return FallBackForSwimming;
                case StateSupportFlags.Flying: return FallBackForFlying;
                case StateSupportFlags.Mounted: return FallBackForMounted;
                case StateSupportFlags.Climbing: return FallBackForClimbing;
                case StateSupportFlags.SpecialInteraction: return FallBackForSpecialInteraction;
                case StateSupportFlags.Observer: return FallBackForObserver;
                case StateSupportFlags.Dead: return FallBackForDead;
                case StateSupportFlags.Transition: return FallBackForTransition;
                default: return FallBackForGrounded;
            }
        }

        /// <summary>
        /// 标记Dirty状态（独立Flag）
        /// </summary>
        public void MarkDirty(PipelineDirtyFlags flags)
        {
            if (flags == PipelineDirtyFlags.None) return;

            dirtyFlags |= flags;
            lastDirtyTime = Time.time;
#if STATEMACHINEDEBUG
            Debug.Log($"[Pipeline-Dirty] [{pipelineType}] Dirty添加: {flags} -> {dirtyFlags}");
#endif
        }

        /// <summary>
        /// 清除Dirty标记（不传入则清空全部）
        /// </summary>
        public void ClearDirty(PipelineDirtyFlags flags = PipelineDirtyFlags.None)
        {
            if (flags == PipelineDirtyFlags.None)
            {
                if (dirtyFlags == PipelineDirtyFlags.None) return;
#if STATEMACHINEDEBUG
                Debug.Log($"[Pipeline-Dirty] [{pipelineType}] Dirty已清空 (旧={dirtyFlags})");
#endif
                dirtyFlags = PipelineDirtyFlags.None;
                return;
            }

            if ((dirtyFlags & flags) != 0)
            {
                dirtyFlags &= ~flags;
#if STATEMACHINEDEBUG
                Debug.Log($"[Pipeline-Dirty] [{pipelineType}] Dirty移除: {flags} -> {dirtyFlags}");
#endif
            }
        }

        /// <summary>
        /// 是否包含指定Dirty标记
        /// </summary>
        public bool HasDirtyFlag(PipelineDirtyFlags flags)
        {
            return (dirtyFlags & flags) != 0;
        }

        /// <summary>
        /// 更新Dirty自动衰减（仅对中/高优先级降级为FallBack检查）
        /// </summary>
        public void UpdateDirtyDecay()
        {
            if (dirtyFlags == PipelineDirtyFlags.None) return;

            var decayFlags = PipelineDirtyFlags.MediumPriority | PipelineDirtyFlags.HighPriority;
            if ((dirtyFlags & decayFlags) == 0) return;

            float elapsed = Time.time - lastDirtyTime;
            if (elapsed >= 1.0f)
            {
                dirtyFlags &= ~decayFlags;
                dirtyFlags |= PipelineDirtyFlags.FallbackCheck;
#if STATEMACHINEDEBUG
                Debug.Log($"[Pipeline-Dirty] [{pipelineType}] Dirty衰减 -> {dirtyFlags}");
#endif
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
            sb.AppendLine($"│  SupportFlag: Grounded={FallBackForGrounded}, Crouched={FallBackForCrouched}, Prone={FallBackForProne}, Swimming={FallBackForSwimming}, Flying={FallBackForFlying}, Mounted={FallBackForMounted}, Climbing={FallBackForClimbing}, SpecialInteraction={FallBackForSpecialInteraction}, Observer={FallBackForObserver}, Dead={FallBackForDead}, Transition={FallBackForTransition}");
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
                var slotList = _stateToSlotListCache;
                slotList.Clear();
                foreach (var kvp in stateToSlotMap)
                {
                    slotList.Add(kvp);
                }
                slotList.Sort((a, b) => a.Value.CompareTo(b.Value));
                for (int i = 0; i < slotList.Count; i++)
                {
                    var kvp = slotList[i];
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
#if STATEMACHINEDEBUG
            UnityEngine.Debug.Log(GetMixerConnectionInfo());
#endif
        }
#endif
    }
}
