using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public class StateCostData
    {
        [LabelText("动向代价")]
        [Tooltip("包含身体的基本运动意愿，0-100")]
        [Range(0f, 100f)]
        public float motionCost = 0f;

        [LabelText("灵活度代价")]
        [Tooltip("控制身体灵活度的代价，0-100")]
        [Range(0f, 100f)]
        public float agilityCost = 0f;

        [LabelText("目标代价")]
        [Tooltip("目标锁定相关代价，0-100")]
        [Range(0f, 100f)]
        public float targetCost = 0f;

        [LabelText("启用代价计算")]
        public bool enableCostCalculation = true;

        [LabelText("代价权重"), ShowIf("enableCostCalculation")]
        public Vector3 costWeights = Vector3.one;

        public float GetWeightedMotion() => motionCost * costWeights.x;
        public float GetWeightedAgility() => agilityCost * costWeights.y;
        public float GetWeightedTarget() => targetCost * costWeights.z;

        [HorizontalGroup("Actions")]
        [Button("验证配置合理性", ButtonSizes.Medium)]
        public void ValidateConfig()
        {
            var issues = new List<string>();
            if (motionCost < 0f || motionCost > 100f) issues.Add("motionCost 必须在 [0,100] 范围内。");
            if (agilityCost < 0f || agilityCost > 100f) issues.Add("agilityCost 必须在 [0,100] 范围内。");
            if (targetCost < 0f || targetCost > 100f) issues.Add("targetCost 必须在 [0,100] 范围内。");

            if (issues.Count == 0) Debug.Log("StateCostData: 配置检查通过。");
            else
            {
                foreach (var s in issues) Debug.LogWarning("StateCostData 配置警告: " + s);
            }
        }
    }
}
