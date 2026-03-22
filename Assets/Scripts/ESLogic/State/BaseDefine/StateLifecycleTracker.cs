// ============================================================================
// 文件：StateLifecycleTracker.cs
// 作用：通用"模块 ←→ 状态"生命周期跟踪器。零 GC，无委托存储。
//       它是 StateBase 在模块侧的标准生命周期搭档；AI / 业务模块不要再自行维护一套 Enter/Exit 布尔守卫。
//
// 解决的核心问题：
//   模块的进入/退出可能来自多个触发源（键盘输入、外部状态打断、实体销毁…），
//   需要保证 OnEnter / OnExit 各执行且只执行一次，不因来源不同而重复或遗漏。
//
// 设计原则：跟踪器只管 _isActive 状态和轮询判断，
//   业务回调由调用方根据返回值自行执行，不存任何委托/闭包字段，彻底零 GC。
//
// 用法模板：
//   tracker = new StateLifecycleTracker();
//   tracker.Bind(sm, state, "stateName");           // Start()
//
//   // 主动进入（将激活结果直接传入，无闭包）
//   if (tracker.TryEnter(sm.TryActivateState(s)))
//       OnMyEnter();
//
//   // 主动退出
//   if (tracker.RequestExit())
//       OnMyExit();
//
//   // Update 首行（捕捉外部打断）
//   if (tracker.CheckExit()) { OnMyExit(); return; }
//
//   // OnDestroy
//   if (tracker.Dispose()) OnMyExit();
//
// Public API：
//   bool IsActive         当前是否处于激活中
//   bool IsBound          当前是否已绑定到状态/状态机上下文
//   StateBase BoundState  当前绑定状态（可空）
//   string BoundStateName 当前绑定状态名/键（可空）
//   void Bind(...)        绑定状态（Start 或状态解析变化时调用）
//   bool TryEnter(bool)   尝试进入（幂等），true = 本次成功进入请执行回调
//   bool RequestExit()    主动请求退出（幂等），true = 本次触发请执行回调
//   bool CheckExit()      每帧检测外部打断（幂等），true = 检测到打断请执行回调
//   bool SyncFromBoundState() 同步当前活跃标记到绑定状态运行态（恢复/重绑时可用）
// ============================================================================

using System.Runtime.CompilerServices;

namespace ES
{
    /// <summary>
    /// 通用"模块←→状态"生命周期跟踪器。零 GC，无委托存储。<br/>
    /// 它是 <see cref="StateBase"/> 在模块侧的标准生命周期包装器：
    /// 模块只应通过它处理 Enter/Exit/外部打断，不应自行再维护一套并行生命周期布尔状态。<br/>
    /// 跟踪器只管 <c>_isActive</c> 状态和外部打断轮询，
    /// 业务回调由调用方在返回值为 <c>true</c> 时自行执行，不存任何委托字段。
    /// </summary>
    public sealed class StateLifecycleTracker
    {
        private bool         _isActive;
        private bool         _hasStateName;
        private StateBase    _state;
        private string       _stateName;
        private StateMachine _sm;

        /// <summary>当前是否处于激活状态（Enter 已执行、Exit 尚未执行）。</summary>
        public bool IsActive => _isActive;

        /// <summary>当前是否已绑定到状态/状态机上下文。</summary>
        public bool IsBound => _sm != null || _state != null || _hasStateName;

        /// <summary>当前绑定的状态对象（可为空，表示仅以状态名绑定）。</summary>
        public StateBase BoundState => _state;

        /// <summary>当前绑定的状态名/键。</summary>
        public string BoundStateName => _stateName;

        /// <summary>
        /// 绑定要跟踪的状态与状态机（在模块 <c>Start</c> 中调用）。
        /// 如果在活跃期间重绑，而新绑定状态并未运行，则会自动清除活跃标记，避免旧生命周期残留。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Bind(StateMachine sm, StateBase state, string stateName)
        {
            _sm        = sm;
            _state     = state;
            _stateName = stateName;

            if ((_stateName == null || _stateName.Length == 0) && state != null)
                _stateName = state.strKey;

            _hasStateName = _stateName != null && _stateName.Length > 0;

            if (_isActive && (_state == null || _state.baseStatus != StateBaseStatus.Running))
                _isActive = false;
        }

        /// <summary>
        /// 尝试进入。幂等：已处于激活时直接返回 <c>false</c>。<br/>
        /// 将状态机激活结果直接传入，无闭包，零 GC。<br/>
        /// 返回 <c>true</c> 时调用方应执行对应的 Enter 业务回调。
        /// <example><code>
        /// if (tracker.TryEnter(sm.TryActivateState(myState)))
        ///     OnMyEnter();
        /// </code></example>
        /// </summary>
        /// <param name="activated">状态机是否成功激活了状态。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnter(bool activated)
        {
            if (_isActive || !activated) return false;
            _isActive = true;
            return true;
        }

        /// <summary>
        /// 主动请求退出（键盘输入 / 业务层调用）。幂等。<br/>
        /// 内部会将激活标志置为 <c>false</c> 并调用 <c>TryDeactivateState</c>（若状态仍在运行）。<br/>
        /// 返回 <c>true</c> 时调用方应执行对应的 Exit 业务回调。
        /// <example><code>
        /// if (tracker.RequestExit())
        ///     OnMyExit();
        /// </code></example>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RequestExit()
        {
            if (!_isActive) return false;
            _isActive = false;

            var state = _state;
            var sm = _sm;
            if (sm == null) return true;

            if (state != null)
            {
                if (state.baseStatus != StateBaseStatus.Running) return true;

                var stateKey = state.strKey;
                sm.TryDeactivateState(stateKey != null && stateKey.Length > 0 ? stateKey : _stateName);
                return true;
            }

            if (_hasStateName)
                sm.TryDeactivateState(_stateName);

            return true;
        }

        /// <summary>
        /// 每帧检查状态是否已退出（被外部打断）。幂等。<br/>
        /// 返回 <c>true</c> = 本帧检测到状态已不在运行，调用方应执行 Exit 回调并立即 <c>return</c>。
        /// <example><code>
        /// if (tracker.CheckExit()) { OnMyExit(); return; }
        /// </code></example>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CheckExit()
        {
            if (!_isActive) return false;

            var state = _state;
            if (state != null && state.baseStatus == StateBaseStatus.Running) return false;

            _isActive = false;
            return true;
        }

        /// <summary>
        /// 将当前活跃标记同步到绑定状态的真实运行态。<br/>
        /// 常用于重绑、延迟解析状态或恢复场景；返回 <c>true</c> 表示同步过程中活跃标记发生了变化。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SyncFromBoundState()
        {
            var state = _state;
            bool nextActive = state != null && state.baseStatus == StateBaseStatus.Running;
            bool changed = _isActive != nextActive;
            _isActive = nextActive;
            return changed;
        }

        /// <summary>
        /// 销毁/清理时调用，保证活跃生命周期被干净结束。幂等。<br/>
        /// 返回 <c>true</c> = 确实有活跃生命周期被清理，调用方应执行 Exit 回调。
        /// <example><code>
        /// if (tracker.Dispose()) OnMyExit();
        /// </code></example>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Dispose()
        {
            if (!_isActive) return false;
            _isActive = false;
            return true;
        }
    }
}
