using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public class StateCostData : IRuntimeInitializable
    {
        [NonSerialized] private bool _isRuntimeInitialized;
        public bool IsRuntimeInitialized => _isRuntimeInitialized;

        [LabelText("动向代价")]
        [Tooltip("动向代价=激活该状态后对角色整体运动的占用强度，而不是体力消耗；值越高表示该状态占用越多运动意愿，越难与其他需要移动的状态并行，通道重叠时会与其他状态代价相加并影响合并/打断判定，0-100。")]
        [Range(0, 100)]
        public byte costForMotion = 60;

        [LabelText("灵活度代价")]
        [Tooltip("灵活度代价=激活该状态后对角色身体灵活性的占用强度，表示可做其他动作的余量而不是动作本身消耗；值越高表示越难叠加需要高灵活度的动作，通道重叠时会参与总和判断，0-100。")]
        [Range(0, 100)]
        public byte costForAgility = 60;

        [LabelText("目标代价")]
        [Tooltip("目标代价=激活该状态后对目标锁定/瞄准/注视等目标通道的占用强度，值越高表示越难与其他目标相关状态并行，通道重叠时参与代价总和与打断判定，0-100。")]
        [Range(0, 100)]
        public byte costForTarget = 60;

        [LabelText("启用代价计算")]
        [Tooltip("启用后在通道重叠时使用代价总和进行合并/打断判定；代价体现的是动作占用自由度的强弱，而不是资源消耗，关闭后会更偏向规则/优先级判断。")]
        public bool enableCostCalculation = true;

        /// <summary>
        /// 获取总代价（直接相加，不使用权重）
        /// </summary>
        public float GetTotalCost() => costForMotion + costForAgility + costForTarget;

        /// <summary>
        /// 为保留兼容性保留的方法
        /// </summary>
        [HorizontalGroup("Actions")]
        [Button("验证配置合理性", ButtonSizes.Medium)]
        public void ValidateConfig()
        {
            var issues = new List<string>();
            if (costForMotion > 100) issues.Add("motionCost 必须在 [0,100] 范围内。");
            if (costForAgility > 100) issues.Add("agilityCost 必须在 [0,100] 范围内。");
            if (costForTarget > 100) issues.Add("targetCost 必须在 [0,100] 范围内。");

            if (issues.Count == 0) Debug.Log("StateCostData: 配置检查通过。");
            else
            {
                foreach (var s in issues) Debug.LogWarning("StateCostData 配置警告: " + s);
            }
        }

        /// <summary>
        /// 运行时初始化
        /// </summary>
        public void InitializeRuntime()
        {
            if (_isRuntimeInitialized) return;
            
            // StateCostData目前无预计算需求，但保留接口以便未来扩展
            _isRuntimeInitialized = true;
        }
    }
}
