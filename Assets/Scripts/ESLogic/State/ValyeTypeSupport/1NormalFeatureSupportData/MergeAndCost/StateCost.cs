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
        [Tooltip("包含身体的基本运动意愿，0-100")]
        [Range(0, 100)]
        public byte costForMotion = 60;

        [LabelText("灵活度代价")]
        [Tooltip("控制身体灵活度的代价，0-100")]
        [Range(0, 100)]
        public byte costForAgility = 60;

        [LabelText("目标代价")]
        [Tooltip("目标锁定相关代价，0-100")]
        [Range(0, 100)]
        public byte costForTarget = 60;

        [LabelText("启用代价计算")]
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
