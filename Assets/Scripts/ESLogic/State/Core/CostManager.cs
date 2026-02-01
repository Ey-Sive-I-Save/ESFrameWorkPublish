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
        // 每个通道的当前代价值 (0=完全空闲, 1=完全占用)
        private Dictionary<StateChannelMask, float> _channelCostValues;
        
        // 记录当前占用各通道的状态信息
        private Dictionary<StateChannelMask, HashSet<int>> _channelOccupiers;
        
        // 代价返还队列 - 用于逐步释放代价
        private List<CostReturnSchedule> _returnSchedules;

        public CostManager()
        {
            _channelCostValues = new Dictionary<StateChannelMask, float>();
            _channelOccupiers = new Dictionary<StateChannelMask, HashSet<int>>();
            _returnSchedules = new List<CostReturnSchedule>();
            
            InitializeChannels();
        }

        private void InitializeChannels()
        {
            // 初始化所有定义的通道
            foreach (StateChannelMask channel in Enum.GetValues(typeof(StateChannelMask)))
            {
                if (channel != StateChannelMask.None && IsSingleBit(channel))
                {
                    _channelCostValues[channel] = 0f;
                    _channelOccupiers[channel] = new HashSet<int>();
                }
            }
        }

        /// <summary>
        /// 检查某个代价需求是否可以满足
        /// </summary>
        public bool CanAffordCost(StateCostData cost, int stateId, bool allowInterrupt = false)
        {
            if (cost == null) return true;
            
            // 检查主代价
            if (!CanAffordCostPart(cost.mainCostPart, stateId, allowInterrupt))
                return false;
            
            // 检查分部代价
            if (cost.EnableCostPartList && cost.costPartList != null)
            {
                foreach (var part in cost.costPartList)
                {
                    if (!CanAffordCostPart(part, stateId, allowInterrupt))
                        return false;
                }
            }
            
            return true;
        }

        private bool CanAffordCostPart(StateChannelCostPart part, int stateId, bool allowInterrupt)
        {
            if (part == null) return true;
            
            var channels = ExpandChannelMask(part.channelMask);
            foreach (var channel in channels)
            {
                float currentCost = GetChannelCost(channel);
                float availableCost = 1f - currentCost;
                
                // 如果当前通道可用代价不足
                if (availableCost < part.EnterCostValue)
                {
                    // 检查是否允许打断
                    if (!allowInterrupt) return false;
                    
                    // 检查当前占用者是否可被打断（这里需要结合状态优先级系统）
                    // 简化处理：如果不是自己占用的，且代价不足，则失败
                    if (!_channelOccupiers[channel].Contains(stateId))
                        return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// 消耗代价 - 进入状态时调用
        /// </summary>
        public void ConsumeCost(StateCostData cost, int stateId)
        {
            if (cost == null) return;
            
            ConsumeCostPart(cost.mainCostPart, stateId);
            
            if (cost.EnableCostPartList && cost.costPartList != null)
            {
                foreach (var part in cost.costPartList)
                {
                    ConsumeCostPart(part, stateId);
                }
            }
        }

        private void ConsumeCostPart(StateChannelCostPart part, int stateId)
        {
            if (part == null) return;
            
            var channels = ExpandChannelMask(part.channelMask);
            foreach (var channel in channels)
            {
                float current = GetChannelCost(channel);
                _channelCostValues[channel] = Mathf.Clamp01(current + part.EnterCostValue);
                _channelOccupiers[channel].Add(stateId);
            }
        }

        /// <summary>
        /// 安排代价返还 - 在后摇阶段或退出时调用
        /// </summary>
        public void ScheduleCostReturn(StateCostData cost, int stateId, float startTime, float duration)
        {
            if (cost == null) return;
            
            var schedule = new CostReturnSchedule
            {
                stateId = stateId,
                cost = cost,
                startTime = startTime,
                duration = duration,
                returnedAmount = 0f
            };
            
            _returnSchedules.Add(schedule);
        }

        /// <summary>
        /// 立即返还代价 - 强制退出时调用
        /// </summary>
        public void ReturnCostImmediately(StateCostData cost, int stateId)
        {
            if (cost == null) return;
            
            ReturnCostPart(cost.mainCostPart, stateId, 1f);
            
            if (cost.EnableCostPartList && cost.costPartList != null)
            {
                foreach (var part in cost.costPartList)
                {
                    ReturnCostPart(part, stateId, 1f);
                }
            }
            
            // 清除相关的返还计划
            _returnSchedules.RemoveAll(s => s.stateId == stateId);
        }

        private void ReturnCostPart(StateChannelCostPart part, int stateId, float fraction)
        {
            if (part == null) return;
            
            var returnMask = part.ReturnMask != StateChannelMask.None ? part.ReturnMask : part.channelMask;
            var channels = ExpandChannelMask(returnMask);
            
            float returnAmount = part.EnterCostValue * fraction;
            if (part.EnableReturnProgress)
            {
                returnAmount *= part.ReturnFraction;
            }
            
            foreach (var channel in channels)
            {
                float current = GetChannelCost(channel);
                _channelCostValues[channel] = Mathf.Clamp01(current - returnAmount);
                
                // 如果代价完全返还，移除占用者
                if (_channelCostValues[channel] <= 0.01f)
                {
                    _channelOccupiers[channel].Remove(stateId);
                }
            }
        }

        /// <summary>
        /// 更新代价返还进度 - 每帧调用
        /// </summary>
        public void UpdateCostReturns(float currentTime)
        {
            for (int i = _returnSchedules.Count - 1; i >= 0; i--)
            {
                var schedule = _returnSchedules[i];
                float elapsed = currentTime - schedule.startTime;
                
                if (elapsed >= schedule.duration)
                {
                    // 完成返还
                    ReturnScheduleRemaining(schedule);
                    _returnSchedules.RemoveAt(i);
                }
                else
                {
                    // 渐进返还
                    float progress = elapsed / schedule.duration;
                    float targetAmount = progress;
                    float deltaAmount = targetAmount - schedule.returnedAmount;
                    
                    if (deltaAmount > 0.001f)
                    {
                        ProgressiveReturn(schedule, deltaAmount);
                        schedule.returnedAmount = targetAmount;
                    }
                }
            }
        }

        private void ReturnScheduleRemaining(CostReturnSchedule schedule)
        {
            float remaining = 1f - schedule.returnedAmount;
            if (remaining > 0.001f)
            {
                ProgressiveReturn(schedule, remaining);
            }
        }

        private void ProgressiveReturn(CostReturnSchedule schedule, float fraction)
        {
            ReturnCostPart(schedule.cost.mainCostPart, schedule.stateId, fraction);
            
            if (schedule.cost.EnableCostPartList && schedule.cost.costPartList != null)
            {
                foreach (var part in schedule.cost.costPartList)
                {
                    ReturnCostPart(part, schedule.stateId, fraction);
                }
            }
        }

        public float GetChannelCost(StateChannelMask channel)
        {
            return _channelCostValues.TryGetValue(channel, out float value) ? value : 0f;
        }

        /// <summary>
        /// 获取组合通道的最大代价值
        /// </summary>
        public float GetMaxCostInMask(StateChannelMask mask)
        {
            var channels = ExpandChannelMask(mask);
            float maxCost = 0f;
            foreach (var channel in channels)
            {
                maxCost = Mathf.Max(maxCost, GetChannelCost(channel));
            }
            return maxCost;
        }

        // 将组合掩码拆分为单个通道
        private List<StateChannelMask> ExpandChannelMask(StateChannelMask mask)
        {
            var result = new List<StateChannelMask>();
            foreach (StateChannelMask channel in Enum.GetValues(typeof(StateChannelMask)))
            {
                if (channel != StateChannelMask.None && IsSingleBit(channel) && (mask & channel) != 0)
                {
                    result.Add(channel);
                }
            }
            return result;
        }

        private bool IsSingleBit(StateChannelMask mask)
        {
            uint value = (uint)mask;
            return value != 0 && (value & (value - 1)) == 0;
        }

        // 代价返还计划
        private class CostReturnSchedule
        {
            public int stateId;
            public StateCostData cost;
            public float startTime;
            public float duration;
            public float returnedAmount; // 0~1
        }
    }
}
