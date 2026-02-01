using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES.Optimizations
{
    /// <summary>
    /// 高性能同路状态链 - O(1)操作
    /// </summary>
    [Serializable]
    public class SamePathChain
    {
        public string chainName;
        public List<int> stateIds = new List<int>(); // 从低到高排序: [Idle, Walk, Run, Sprint]
        
        [NonSerialized]
        private int[] _stateIdArray; // 用于快速访问
        
        [NonSerialized]
        private Dictionary<int, int> _stateToLevel; // stateId -> level (0~n)
        
        [NonSerialized]
        private int _maxLevel;
        
        public void Initialize()
        {
            _stateIdArray = stateIds.ToArray();
            _maxLevel = _stateIdArray.Length - 1;
            
            _stateToLevel = new Dictionary<int, int>(_stateIdArray.Length);
            for (int i = 0; i < _stateIdArray.Length; i++)
            {
                _stateToLevel[_stateIdArray[i]] = i;
            }
        }
        
        /// <summary>
        /// O(1) 获取状态等级
        /// </summary>
        public bool TryGetLevel(int stateId, out int level)
        {
            return _stateToLevel.TryGetValue(stateId, out level);
        }
        
        /// <summary>
        /// O(1) 获取退化目标
        /// </summary>
        public int GetDegradeTarget(int currentStateId, int steps = 1)
        {
            if (!_stateToLevel.TryGetValue(currentStateId, out int level))
                return -1;
            
            int targetLevel = Mathf.Max(0, level - steps);
            return _stateIdArray[targetLevel];
        }
        
        /// <summary>
        /// O(1) 获取升级目标
        /// </summary>
        public int GetUpgradeTarget(int currentStateId, int steps = 1)
        {
            if (!_stateToLevel.TryGetValue(currentStateId, out int level))
                return -1;
            
            int targetLevel = Mathf.Min(_maxLevel, level + steps);
            return _stateIdArray[targetLevel];
        }
        
        /// <summary>
        /// O(1) 检查两个状态是否同路
        /// </summary>
        public bool IsSamePath(int stateId1, int stateId2)
        {
            return _stateToLevel.ContainsKey(stateId1) && _stateToLevel.ContainsKey(stateId2);
        }
        
        /// <summary>
        /// O(1) 比较两个状态的等级
        /// </summary>
        public int CompareLevel(int stateId1, int stateId2)
        {
            bool has1 = _stateToLevel.TryGetValue(stateId1, out int level1);
            bool has2 = _stateToLevel.TryGetValue(stateId2, out int level2);
            
            if (!has1 && !has2) return 0;
            if (!has1) return -1;
            if (!has2) return 1;
            
            return level1.CompareTo(level2);
        }
        
        /// <summary>
        /// 根据代价计算智能退化级别
        /// </summary>
        public int CalculateSmartDegrade(int currentStateId, float availableCost, float requiredCost)
        {
            if (!_stateToLevel.TryGetValue(currentStateId, out int currentLevel))
                return -1;
            
            // 代价比率
            float costRatio = availableCost / requiredCost;
            
            // 根据代价不足程度决定退化级别
            int degradeSteps;
            if (costRatio < 0.2f)
                degradeSteps = currentLevel; // 退化到最低级
            else if (costRatio < 0.4f)
                degradeSteps = Mathf.Max(1, currentLevel / 2); // 退化到中间级
            else if (costRatio < 0.6f)
                degradeSteps = 1; // 退化一级
            else
                return -1; // 不退化
            
            return GetDegradeTarget(currentStateId, degradeSteps);
        }
    }
    
    /// <summary>
    /// 同路状态管理器
    /// </summary>
    public class SamePathManager
    {
        private Dictionary<string, SamePathChain> _chains;
        private Dictionary<int, string> _stateToChain;
        
        public SamePathManager()
        {
            _chains = new Dictionary<string, SamePathChain>();
            _stateToChain = new Dictionary<int, string>();
        }
        
        public void RegisterChain(SamePathChain chain)
        {
            if (chain == null || string.IsNullOrEmpty(chain.chainName))
                return;
            
            chain.Initialize();
            _chains[chain.chainName] = chain;
            
            foreach (int stateId in chain.stateIds)
            {
                _stateToChain[stateId] = chain.chainName;
            }
        }
        
        public void RegisterChains(IEnumerable<SamePathChain> chains)
        {
            foreach (var chain in chains)
            {
                RegisterChain(chain);
            }
        }
        
        /// <summary>
        /// O(1) 获取状态所属的链
        /// </summary>
        public SamePathChain GetChain(int stateId)
        {
            if (_stateToChain.TryGetValue(stateId, out string chainName))
            {
                return _chains[chainName];
            }
            return null;
        }
        
        /// <summary>
        /// 智能退化决策 - 综合考虑代价、等级、优先级
        /// </summary>
        public int DecideDegrade(int currentStateId, int incomingStateId, float currentCost, float incomingCost)
        {
            var chain = GetChain(currentStateId);
            if (chain == null)
                return -1;
            
            // 如果来袭状态也在同路上
            if (chain.IsSamePath(currentStateId, incomingStateId))
            {
                int comparison = chain.CompareLevel(currentStateId, incomingStateId);
                
                if (comparison > 0) // 当前状态级别更高
                {
                    // 退化到来袭状态的级别
                    if (chain.TryGetLevel(incomingStateId, out int incomingLevel))
                    {
                        if (chain.TryGetLevel(currentStateId, out int currentLevel))
                        {
                            int steps = currentLevel - incomingLevel;
                            return chain.GetDegradeTarget(currentStateId, steps);
                        }
                    }
                }
                else if (comparison < 0) // 当前状态级别更低，不应该被打断
                {
                    return -1;
                }
            }
            
            // 根据代价智能退化
            return chain.CalculateSmartDegrade(currentStateId, currentCost, incomingCost);
        }
        
        /// <summary>
        /// 自动升级检查 - 当代价恢复时尝试升级状态
        /// </summary>
        public int TryAutoUpgrade(int currentStateId, float availableCost)
        {
            var chain = GetChain(currentStateId);
            if (chain == null)
                return -1;
            
            // 如果代价充足，尝试升级
            if (availableCost > 0.8f) // 80%以上代价可用
            {
                return chain.GetUpgradeTarget(currentStateId, 1);
            }
            
            return -1;
        }
    }
}
