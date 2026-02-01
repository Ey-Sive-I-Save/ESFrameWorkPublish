using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ES
{
    /*
     CoreStateSystem.cs
     - 目标：为Playable动画系统提供一个可扩展的状态系统骨架，覆盖 StateSharedData / StateVariableData 的基本实现，
       提供代价（Cost）计算、备忘（Memo）与尝试队列（AttemptList）、以及一个轻量级的 BaseStateMachine。
     - 注意：这是一个初始版（约 1000+ 行草案），用于迭代与集成。最终用于生产时需要更多单元测试、完善的序列化和性能优化。

     设计原则：
     - 明确分离“共享数据”（不可频繁改变，决定状态间优先级/通道/可击中性等）与“变量数据”（运行时变化，快照化可选）。
     - 代价（Cost）系统：代价值为浮点数，进入一个动作需要占据/消耗代价；后摇/退出要完整归还。
     - 备忘（Memo）与尝试（Attempt）：尝试进入的状态先加入尝试列表，只有在发生关键事件（退出、退化、后摇完成）时才刷新备忘。
     - 提供 Hook、事件与抽象点，便于将来挂接 Playable 混合与 IK 绑定。
    */

    #region Core Data Implementations

   
    

    #endregion

    #region Cost System

    [Serializable]
    public class CostBank
    {
        // 管理一个实体的代价池（例如人的代价由四肢/意愿等构成）
        // 总池：最大容量
        public float MaxCost { get; private set; } = 100f;
        // 当前可用代价
        public float Current { get; private set; } = 100f;

        // 记录所有占用者的 id 与消耗量
        private Dictionary<string, float> holders = new Dictionary<string, float>();

        public CostBank(float max = 100f)
        {
            MaxCost = max;
            Current = max;
        }

        // 尝试申请代价（瞬时）
        public bool TryAcquire(string holderId, float amount)
        {
            if (amount <= 0) return true;
            if (holders.ContainsKey(holderId)) return false; // 已经持有，不允许重复申请

            if (Current >= amount)
            {
                holders[holderId] = amount;
                Current -= amount;
                return true;
            }

            return false;
        }

        // 释放代价（全量释放）
        public bool Release(string holderId)
        {
            if (!holders.TryGetValue(holderId, out var amt)) return false;
            holders.Remove(holderId);
            Current = Math.Min(MaxCost, Current + amt);
            return true;
        }

        // 部分释放（例如后摇阶段逐步返还）
        public bool PartialRelease(string holderId, float amount)
        {
            if (!holders.TryGetValue(holderId, out var amt)) return false;
            float releaseAmount = Math.Min(amount, amt);
            amt -= releaseAmount;
            holders[holderId] = amt;
            Current = Math.Min(MaxCost, Current + releaseAmount);
            if (amt <= 0f)
            {
                holders.Remove(holderId);
            }
            return true;
        }

        public float GetHeldAmount(string holderId)
        {
            return holders.TryGetValue(holderId, out var v) ? v : 0f;
        }

        public IEnumerable<KeyValuePair<string, float>> EnumerateHolders() => holders;
    }

    #endregion

    #region Memo & Attempt Lists

    [Serializable]
    public class AttemptEntry
    {
        public string StateKey;
        public int Priority; // 用于同路/优先级判断
        public float RequestedAt; // 时间戳
        public float CostRequested;
        public object Extra; // 扩展数据
    }

    [Serializable]
    public class MemoList
    {
        // 备忘列表：当多次尝试并且未立即进入时，记录候选项
        private List<AttemptEntry> memos = new List<AttemptEntry>();

        public void AddOrUpdate(AttemptEntry entry)
        {
            var exist = memos.FirstOrDefault(m => m.StateKey == entry.StateKey);
            if (exist != null)
            {
                exist.Priority = entry.Priority;
                exist.RequestedAt = entry.RequestedAt;
                exist.CostRequested = entry.CostRequested;
                exist.Extra = entry.Extra;
                return;
            }
            memos.Add(entry);
        }

        public bool Remove(string stateKey)
        {
            var e = memos.FirstOrDefault(m => m.StateKey == stateKey);
            if (e == null) return false;
            memos.Remove(e);
            return true;
        }

        public void Clear() => memos.Clear();

        public IEnumerable<AttemptEntry> Enumerate() => memos.OrderBy(m => m.Priority).ThenBy(m => m.RequestedAt);
    }

    #endregion

    #region Base State & Machine

    /// <summary>
    /// BaseStateExtended: 在 `BaseState` 之上加入代价与备忘支持的基础实现，
    /// 提供一个轻量级的 Hook API 方便继承者实现进入/后摇/退化逻辑。
    /// </summary>
    public abstract class BaseStateExtended : StateBase
    {
        // 代价标记 ID，用于 CostBank 的持有者识别

        protected string costHolderId => GetKey();

        // 状态的固定代价请求（进入时请求）
        [ShowInInspector]
        public float EnterCost = 10f;

        // 后摇阶段每秒释放代价（可以为 EnterCost 的分割合计）
        [ShowInInspector]
        public float ReleasePerSecond = 5f;

        // 引用到宿主机器的 CostBank（外部注入）
        [NonSerialized]
        public CostBank HostCostBank;

        // 标记当前是否持有代价
        protected bool hasCost = false;

        // 生命周期扩展点
        protected override void RunStatePreparedLogic()
        {
            base.RunStatePreparedLogic();
            TryAcquireCostOnEnter();
        }

        protected override void RunStateExitLogic()
        {
            base.RunStateExitLogic();
            // 退出时确保释放
            if (hasCost && HostCostBank != null)
            {
                HostCostBank.Release(costHolderId);
                hasCost = false;
            }
        }

        // 默认实现：尝试申请代价，失败的话调用 OnEnterFailed
        protected virtual void TryAcquireCostOnEnter()
        {
            if (HostCostBank == null)
            {
                // 若未注入 CostBank，直接允许进入（保守策略可改为拒绝）
                hasCost = true;
                return;
            }

            if (HostCostBank.TryAcquire(costHolderId, EnterCost))
            {
                hasCost = true;
            }
            else
            {
                hasCost = false;
                OnEnterFailed();
            }
        }

        // 进入失败回调（默认放入尝试/备忘，由外部机器处理）
        protected virtual void OnEnterFailed()
        {
            // 子类可覆盖：例如记录 AttemptList 或触发回退
        }

        // 后摇阶段：按 ReleasePerSecond 逐步释放代价（在 Update 中调用）
        public void UpdateRelease(float deltaTime)
        {
            if (!hasCost || HostCostBank == null) return;
            float amount = ReleasePerSecond * deltaTime;
            HostCostBank.PartialRelease(costHolderId, amount);
        }
    }

    /// <summary>
    /// BaseStateMachine: 轻量的单路径状态机实现，管理三条主要线路（Base/Main/Buff），
    /// 并提供 Attempt/Memo 的生命周期钩子。
    /// </summary>
    public class StateMachineBase
    {
        // IState 接口实现（此状态机也可作为一个状态嵌套）
        public bool IsRunning { get; set; }

        public StateBase SelfRunningMainState { get; set; }

        public IEnumerable<StateBase> SelfRunningStates => new StateBase[] { SelfRunningMainState };

        public HashSet<StateBase> RootAllRunningStates { get; } = new HashSet<StateBase>();

        // 三条线路分离管理
        public BaseStateExtended BaseLineState { get; private set; }
        public BaseStateExtended MainLineState { get; private set; }
        public List<BaseStateExtended> BuffLineStates { get; private set; } = new List<BaseStateExtended>();

        // Host CostBank（集中管理）
        public CostBank costBank = new CostBank(100f);

        // Memo 与 Attempt 列表
        private MemoList memo = new MemoList();
        private List<AttemptEntry> attempts = new List<AttemptEntry>();

        // Context 参数（运行时）
        // 事件钩子
        public event Action<string> OnStateEntered;
        public event Action<string> OnStateExited;
        public event Action<AttemptEntry> OnAttemptFailed;

        // 初始化
        public void Initialize(BaseStateExtended baseline = null)
        {
            BaseLineState = baseline;
            if (BaseLineState != null)
            {
                BindState(BaseLineState);
            }
        }

        // 绑定状态，将 CostBank/SharedData/VariableData 注入
        public void BindState(BaseStateExtended s)
        {
            if (s == null) return;
            s.HostCostBank = costBank;
           // s.SharedData = s.SharedData ?? new SimpleStateSharedData();
           // s.VariableData = s.VariableData ?? new SimpleStateVariableData();
        }

        // 请求进入主线或 Buff（公开 API）
        public bool RequestEnterMain(BaseStateExtended state)
        {
            // 1) 同线判断：如果当前有主线占用且新状态与当前为同路需做退化逻辑
            if (MainLineState != null && IsSameRoad(MainLineState, state))
            {
                // 同路：以弱退化优先（示例策略：若新优先级更高则替换，否则退化）
                if (ComparePriority(state, MainLineState) < 0)
                {
                    // 新状态优先，尝试替换
                    ForceExitState(MainLineState);
                }
                else
                {
                    // 记录备忘并返回
                    AddAttempt(state, priority: state.SharedData?.Order ?? 0);
                    return false;
                }
            }

            // 2) 申请代价
            if (costBank.TryAcquire(state.key, state.EnterCost))
            {
                // 进入，注入并触发生命
                BindState(state);
                if (MainLineState != null)
                {
                    ForceExitState(MainLineState);
                }
                MainLineState = state;
                state.OnStateEnter();
                OnStateEntered?.Invoke(state.GetKey());
                return true;
            }

            // 3) 申请失败：加入尝试队列
            AddAttempt(state, priority: state.SharedData?.Order ?? 0);
            OnAttemptFailed?.Invoke(new AttemptEntry() { StateKey = state.key, Priority = state.SharedData?.Order ?? 0, RequestedAt = Time.time });
            return false;
        }

        // 基本的 Buff 入队（支持多 Buff 并行）
        public bool RequestEnterBuff(BaseStateExtended state)
        {
            // Buff 不占主线，可并行申请
            if (costBank.TryAcquire(state.key, state.EnterCost))
            {
                BindState(state);
                BuffLineStates.Add(state);
                state.OnStateEnter();
                OnStateEntered?.Invoke(state.GetKey());
                return true;
            }
            AddAttempt(state, priority: state.SharedData?.Order ?? 0);
            return false;
        }

        // 每帧 update，用于处理 attempt 刷新与后摇释放
        public void Update()
        {
            float dt = Time.deltaTime;

            // 1) 更新当前各状态的后摇释放
            MainLineState?.UpdateRelease(dt);
            BaseLineState?.UpdateRelease(dt);
            for (int i = BuffLineStates.Count - 1; i >= 0; i--)
            {
                var b = BuffLineStates[i];
                b.UpdateRelease(dt);
                // 可选：当 buff 的持有量为 0 且状态自标记退出时，清理
            }

            // 2) 处理 Attempt 刷新逻辑：仅在有关键事件发生时才处理（简化：每帧评估，但外部可触发 RefreshAttempts）
            RefreshAttemptsIfNeeded();
        }

        // 强制退出某个状态（立即释放代价并触发 OnStateExit）
        public void ForceExitState(BaseStateExtended state)
        {
            if (state == null) return;
            state.OnStateExit();
            costBank.Release(state.key);
            if (MainLineState == state) MainLineState = null;
            if (BaseLineState == state) BaseLineState = null;
            if (BuffLineStates.Contains(state)) BuffLineStates.Remove(state);
            OnStateExited?.Invoke(state.key);
            // 刷新备忘
            RefreshAttemptsIfNeeded();
        }

        // 判定是否为同路（可通过 Channel 或 Key 前缀来判断）
        protected bool IsSameRoad(BaseStateExtended a, BaseStateExtended b)
        {
            if (a == null || b == null) return false;
            // 优先使用 Channel
            if (a.SharedData?.Channel != null && b.SharedData?.Channel != null)
            {
                return a.SharedData.Channel.Equals(b.SharedData.Channel);
            }
            // 退化为 Key 前缀匹配规则（例如 run_walk_run 前缀）
            return a.key != null && b.key != null && a.key.Split('.').First() == b.key.Split('.').First();
        }

        protected int ComparePriority(BaseStateExtended a, BaseStateExtended b)
        {
            return (a.SharedData?.Order ?? 0) - (b.SharedData?.Order ?? 0);
        }

        protected void AddAttempt(BaseStateExtended state, int priority)
        {
            var e = new AttemptEntry()
            {
                StateKey = state.key,
                Priority = priority,
                RequestedAt = Time.time,
                CostRequested = state.EnterCost
            };
            attempts.Add(e);
            memo.AddOrUpdate(e);
        }

        // 刷新尝试队列（简化逻辑：优先级高的先尝试）
        protected void RefreshAttemptsIfNeeded()
        {
            if (attempts.Count == 0) return;

            // 先排序
            var ordered = attempts.OrderBy(a => a.Priority).ThenBy(a => a.RequestedAt).ToList();
            var consumed = new List<AttemptEntry>();
            foreach (var at in ordered)
            {
                // 查找对应状态实例（此处假设状态可以通过 key 全局查找或由外部管理）
                var state = FindStateByKey(at.StateKey);
                if (state == null) continue; // 可能已销毁

                // 首先尝试主线->基本线->buff的进入策略
                if (RequestEnterMain(state))
                {
                    consumed.Add(at);
                    continue;
                }
                if (RequestEnterBuff(state))
                {
                    consumed.Add(at);
                    continue;
                }
                // 否则继续保留在 attempts 中
            }

            // 移除已消费的 attempts
            foreach (var c in consumed) attempts.Remove(c);
        }

        // 由外部提供的查找函数（默认返回 null），在集成时应覆盖或注入实现
        protected virtual BaseStateExtended FindStateByKey(string key) { return null; }

        #region IState 接口（Minimal）
        public void OnStateEnter()
        {
            IsRunning = true;
        }

        public void OnStateUpdate()
        {
            // Drive by MonoBehaviour.Update
        }

        public void OnStateExit()
        {
            IsRunning = false;
        }

        public void SetKey(string key) { /* not used */ }
        public string GetKey() { return "BaseStateMachine"; }
        public StateBase AsThis ;
        public bool CheckThisStateCanUpdating => true;
        
        public EnumStateRunningStatus RunningStatus => EnumStateRunningStatus.StateUpdate;
        #endregion
    }

    #endregion

    #region Utilities & Helpers

    // 全局注册管理器示例（用于按 key 查找状态的地址簿）
    public static class StateRegistry
    {
        private static Dictionary<string, BaseStateExtended> registry = new Dictionary<string, BaseStateExtended>();

        public static void Register(BaseStateExtended s)
        {
            if (s == null || string.IsNullOrEmpty(s.key)) return;
            registry[s.key] = s;
        }

        public static void Unregister(BaseStateExtended s)
        {
            if (s == null || string.IsNullOrEmpty(s.key)) return;
            if (registry.ContainsKey(s.key)) registry.Remove(s.key);
        }

        public static BaseStateExtended Find(string key)
        {
            registry.TryGetValue(key, out var s);
            return s;
        }
    }

    #endregion

}
