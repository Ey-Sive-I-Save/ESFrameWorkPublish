using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 备忘状态 - 记录哪些状态曾尝试进入但被禁止
    /// 用于优化性能,避免每帧重复测试相同的失败条件
    /// </summary>
    public class MemoizationSystem
    {
        // 备忘记录: stateId -> 上次尝试进入的时间戳
        private Dictionary<int, float> _deniedStates;
        
        // 备忘记录: stateId -> 禁止原因
        private Dictionary<int, DenialReason> _denialReasons;
        
        // 脏标记 - 当发生状态退出/退化/后摇时设置
        private bool _isDirty;
        
        // 上次刷新时间
        private float _lastRefreshTime;
        
        // 备忘超时时间(秒) - 超过这个时间自动清除备忘
        private const float MEMO_TIMEOUT = 1f;

        public MemoizationSystem()
        {
            _deniedStates = new Dictionary<int, float>();
            _denialReasons = new Dictionary<int, DenialReason>();
            _isDirty = false;
        }

        /// <summary>
        /// 检查状态是否在禁止列表中
        /// </summary>
        public bool IsStateDenied(int stateId, float currentTime)
        {
            if (_isDirty)
                return false; // 脏标记时允许重新测试
            
            if (_deniedStates.TryGetValue(stateId, out float deniedTime))
            {
                // 检查是否超时
                if (currentTime - deniedTime > MEMO_TIMEOUT)
                {
                    _deniedStates.Remove(stateId);
                    _denialReasons.Remove(stateId);
                    return false;
                }
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// 记录状态被拒绝
        /// </summary>
        public void RecordDenial(int stateId, DenialReason reason, float currentTime)
        {
            _deniedStates[stateId] = currentTime;
            _denialReasons[stateId] = reason;
        }

        /// <summary>
        /// 标记为脏 - 当状态机发生重要变化时调用
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 刷新备忘状态 - 清除脏标记,重置禁止列表
        /// </summary>
        public void Refresh(float currentTime)
        {
            if (!_isDirty)
                return;
            
            _deniedStates.Clear();
            _denialReasons.Clear();
            _isDirty = false;
            _lastRefreshTime = currentTime;
        }

        /// <summary>
        /// 移除特定状态的备忘
        /// </summary>
        public void RemoveMemo(int stateId)
        {
            _deniedStates.Remove(stateId);
            _denialReasons.Remove(stateId);
        }

        /// <summary>
        /// 获取拒绝原因
        /// </summary>
        public DenialReason GetDenialReason(int stateId)
        {
            return _denialReasons.TryGetValue(stateId, out var reason) ? reason : DenialReason.None;
        }

        /// <summary>
        /// 清空所有备忘
        /// </summary>
        public void Clear()
        {
            _deniedStates.Clear();
            _denialReasons.Clear();
            _isDirty = false;
        }

        public bool IsDirty => _isDirty;
    }

    /// <summary>
    /// 拒绝原因枚举
    /// </summary>
    public enum DenialReason
    {
        None,                   // 无拒绝
        ConditionNotMet,        // 条件不满足
        PriorityTooLow,         // 优先级太低
        SamePathDegrading,      // 同路退化中
        InTransition,           // 正在过渡中
        ManualBlock             // 手动阻止
    }
}
