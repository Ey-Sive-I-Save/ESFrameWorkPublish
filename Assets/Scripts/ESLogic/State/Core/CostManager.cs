using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 代价管理器 - 管理所有通道的代价占用和释放
    /// 采用精确的浮点数代价系统，支持分批返还和实时查询
    /// </summary>
    [Serializable]
    public class CostManager
    {
        // 三大资源的当前使用值，范围 0..100
        private float _motionUsage = 0f;
        private float _agilityUsage = 0f;
        private float _targetUsage = 0f;

        // 记录当前占用各资源的状态集合（用于简单查询和回收）
        private HashSet<int> _motionOccupiers = new HashSet<int>();
        private HashSet<int> _agilityOccupiers = new HashSet<int>();
        private HashSet<int> _targetOccupiers = new HashSet<int>();

        // 代价返还队列 - 用于逐步释放代价（按资源）
        private List<CostReturnSchedule> _returnSchedules = new List<CostReturnSchedule>();

        public CostManager()
        {
            // 初始使用为0
            _motionUsage = _agilityUsage = _targetUsage = 0f;
        }

        /// <summary>
        /// 检查某个代价需求是否可以满足（简化版）
        /// </summary>
        public bool CanAffordCost(StateCostData cost, int stateId, bool allowInterrupt = false)
        {
            if (cost == null) return true;

            float reqMotion = cost.GetWeightedMotion();
            float reqAgility = cost.GetWeightedAgility();
            float reqTarget = cost.GetWeightedTarget();

            if (reqMotion > 0f && (100f - _motionUsage) < reqMotion && !allowInterrupt) return false;
            if (reqAgility > 0f && (100f - _agilityUsage) < reqAgility && !allowInterrupt) return false;
            if (reqTarget > 0f && (100f - _targetUsage) < reqTarget && !allowInterrupt) return false;

            return true;
        }

        /// <summary>
        /// 消耗代价 - 进入状态时调用
        /// </summary>
        public void ConsumeCost(StateCostData cost, int stateId)
        {
            if (cost == null) return;

            float reqMotion = cost.GetWeightedMotion();
            float reqAgility = cost.GetWeightedAgility();
            float reqTarget = cost.GetWeightedTarget();

            if (reqMotion > 0f)
            {
                _motionUsage = Mathf.Clamp(_motionUsage + reqMotion, 0f, 100f);
                _motionOccupiers.Add(stateId);
            }
            if (reqAgility > 0f)
            {
                _agilityUsage = Mathf.Clamp(_agilityUsage + reqAgility, 0f, 100f);
                _agilityOccupiers.Add(stateId);
            }
            if (reqTarget > 0f)
            {
                _targetUsage = Mathf.Clamp(_targetUsage + reqTarget, 0f, 100f);
                _targetOccupiers.Add(stateId);
            }
        }

        /// <summary>
        /// 立即返还代价 - 强制退出时调用
        /// </summary>
        public void ReturnCostImmediately(StateCostData cost, int stateId)
        {
            if (cost == null) return;

            float reqMotion = cost.GetWeightedMotion();
            float reqAgility = cost.GetWeightedAgility();
            float reqTarget = cost.GetWeightedTarget();

            if (reqMotion > 0f)
            {
                _motionUsage = Mathf.Clamp(_motionUsage - reqMotion, 0f, 100f);
                _motionOccupiers.Remove(stateId);
            }
            if (reqAgility > 0f)
            {
                _agilityUsage = Mathf.Clamp(_agilityUsage - reqAgility, 0f, 100f);
                _agilityOccupiers.Remove(stateId);
            }
            if (reqTarget > 0f)
            {
                _targetUsage = Mathf.Clamp(_targetUsage - reqTarget, 0f, 100f);
                _targetOccupiers.Remove(stateId);
            }
        }

        /// <summary>
        /// 安排代价返还 - 在后摇阶段或退出时调用（简化：按比例在 duration 内返回）
        /// </summary>
        public void ScheduleCostReturn(StateCostData cost, int stateId, float startTime, float duration)
        {
            if (cost == null) return;
            var schedule = new CostReturnSchedule
            {
                stateId = stateId,
                motionAmount = cost.GetWeightedMotion(),
                agilityAmount = cost.GetWeightedAgility(),
                targetAmount = cost.GetWeightedTarget(),
                startTime = startTime,
                duration = duration,
                returnedProgress = 0f
            };
            _returnSchedules.Add(schedule);
        }

        /// <summary>
        /// 更新代价返还进度 - 每帧调用
        /// </summary>
        public void UpdateCostReturns(float currentTime)
        {
            for (int i = _returnSchedules.Count - 1; i >= 0; i--)
            {
                var s = _returnSchedules[i];
                float elapsed = currentTime - s.startTime;
                if (elapsed >= s.duration)
                {
                    // 完成返还
                    ReturnPartial(s.motionAmount * (1f - s.returnedProgress), s.agilityAmount * (1f - s.returnedProgress), s.targetAmount * (1f - s.returnedProgress), s.stateId);
                    _returnSchedules.RemoveAt(i);
                }
                else
                {
                    float progress = s.duration > 0f ? Mathf.Clamp01(elapsed / s.duration) : 1f;
                    float delta = progress - s.returnedProgress;
                    if (delta > 0f)
                    {
                        ReturnPartial(s.motionAmount * delta, s.agilityAmount * delta, s.targetAmount * delta, s.stateId);
                        s.returnedProgress = progress;
                    }
                }
            }
        }

        private void ReturnPartial(float motionDelta, float agilityDelta, float targetDelta, int stateId)
        {
            if (motionDelta > 0f)
            {
                _motionUsage = Mathf.Clamp(_motionUsage - motionDelta, 0f, 100f);
                if (_motionUsage <= 0.001f) _motionOccupiers.Remove(stateId);
            }
            if (agilityDelta > 0f)
            {
                _agilityUsage = Mathf.Clamp(_agilityUsage - agilityDelta, 0f, 100f);
                if (_agilityUsage <= 0.001f) _agilityOccupiers.Remove(stateId);
            }
            if (targetDelta > 0f)
            {
                _targetUsage = Mathf.Clamp(_targetUsage - targetDelta, 0f, 100f);
                if (_targetUsage <= 0.001f) _targetOccupiers.Remove(stateId);
            }
        }

        // 代价返还计划（简化）
        private class CostReturnSchedule
        {
            public int stateId;
            public float motionAmount;
            public float agilityAmount;
            public float targetAmount;
            public float startTime;
            public float duration;
            public float returnedProgress; // 0~1
        }
    }
}
