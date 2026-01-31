using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using UnityEngine;
namespace ES
{
    /// <summary>
    /// 代价参数类型
    /// </summary>
    [System.Serializable]
    public class StateChannelCostPart
    {

        [Header("代价参数")]
        [LabelText("状态占据Mask类型")]
        [Tooltip("该分部占据了哪些通道")]
        public StateChannelMask channelMask;


        [LabelText("进入代价消耗值")]
        [Tooltip("进入这个状态需要多少空闲或者可打断代价")]
        [Range(0f, 1f)]
        public float EnterCostValue = 0.5f;

        [LabelText("返还蒙版")]
        [Tooltip("返还时影响的通道掩码；若为 None 则使用本分部的 channelMask")]
        [InlineButton(nameof(CopyChannelMaskToReturnMask), "复制")]
        public StateChannelMask ReturnMask = StateChannelMask.None;

        private void CopyChannelMaskToReturnMask()
        {
            ReturnMask = channelMask;
        }
        [InfoBox("必须确保整个COST 的 分批返还阶段 生效，否则会直接跳过")]
        [LabelText("启用分批返还阶段")]
        public bool EnableReturnProgress = false;
        [LabelText("返还比例(0-1)"),ShowIf("EnableReturnProgress")]
        [Range(0f, 1f)]
        public float ReturnFraction = 1f;

    }

    [Serializable]
    public class StateCost
    {
        [NonSerialized]
        private List<string> _validationIssuesCache;
        [NonSerialized]
        private bool _validationCacheIsDirty = true;

        public void InvalidateValidationCache()
        {
            _validationCacheIsDirty = true;
        }

        private static int HighestBitIndex(uint mask)
        {
            if (mask == 0u) return 0;
            for (int i = 31; i >= 0; i--)
            {
                if ((mask & (1u << i)) != 0u) return i;
            }
            return 0;
        }


        [BoxGroup("主代价")]
        [InlineProperty]
        [OnValueChanged(nameof(InvalidateValidationCache))]
        [Tooltip("该状态的主代价参数，对于简单状态，单个主状态就能满足需求了")]
        public StateChannelCostPart mainCostPart = new StateChannelCostPart()
        {
            channelMask = StateChannelMask.AllBodyActive,
            EnterCostValue = 0.8f
        };


        [Header("返还时机")]
        [LabelText("启用分批返还阶段")]
        [OnValueChanged(nameof(InvalidateValidationCache))]
        public bool EnableReturnProgress = false;
        

        [InfoBox("$ValidationMessage", InfoMessageType.Warning, VisibleIf = "HasValidationIssues")]
        [HideLabel]
        public string ValidationMessagePlaceholder = ""; // 占位供 InfoBox 表达式使用

        [LabelText("启用分部代价")]
        [OnValueChanged(nameof(InvalidateValidationCache))]
        public bool EnableCostPartList = false;
        [Header("状态代价参数")]
        [LabelText("状态代价分部列表"), ShowIf("EnableCostPartList")]
        [Tooltip("该状态占据的各个通道的代价参数，用来支持复杂情况")]
        [OnCollectionChanged(nameof(InvalidateValidationCache))]
        public List<StateChannelCostPart> costPartList = new List<StateChannelCostPart>();

        /// 校验方法,编辑器时显示警告信息
        private List<string> GetValidationIssues()
        {
            // 返回缓存（若未标脏）以避免频繁重算
            if (!_validationCacheIsDirty && _validationIssuesCache != null)
            {
                return _validationIssuesCache;
            }

            var issues = new List<string>();

            if (mainCostPart != null)
            {
                if (mainCostPart.EnterCostValue < 0f || mainCostPart.EnterCostValue > 1f)
                    issues.Add("主代价 (mainCostPart) 的 进入代价消耗值 (EnterCostValue) 必须在 [0,1] 范围内。");
                if (mainCostPart.ReturnFraction < 0f || mainCostPart.ReturnFraction > 1f)
                    issues.Add("主代价 (mainCostPart) 的 返还比例 (ReturnFraction) 必须在 [0,1] 范围内。");
            }

            if (EnableCostPartList && costPartList != null)
            {
                for (int i = 0; i < costPartList.Count; i++)
                {
                    var p = costPartList[i];
                    if (p == null) { issues.Add($"状态代价分部列表 (costPartList)[{i}] 为 null。\n"); continue; }
                    if (p.EnterCostValue < 0f || p.EnterCostValue > 1f) issues.Add($"状态代价分部列表 (costPartList)[{i}] 的 进入代价消耗值 (EnterCostValue) 必须在 [0,1]。\n");
                    if (p.ReturnFraction < 0f || p.ReturnFraction > 1f) issues.Add($"状态代价分部列表 (costPartList)[{i}] 的 返还比例 (ReturnFraction) 必须在 [0,1]。\n");
                }

                // 通道级别的合计校验（简单近似）：对于每一位通道，计算被占用的代价总和，不应显著超过 1
                const float EPS = 1e-5f;
                // 优化：仅枚举到最高使用位
                int maxBit = 0;
                if (mainCostPart != null) maxBit = Math.Max(maxBit, HighestBitIndex((uint)mainCostPart.channelMask));
                foreach (var p in costPartList) if (p != null) maxBit = Math.Max(maxBit, HighestBitIndex((uint)p.channelMask));
                maxBit = Math.Min(31, Math.Max(0, maxBit));

                for (int bit = 0; bit <= maxBit; bit++)
                {
                    StateChannelMask bitMask = (StateChannelMask)(1u << bit);
                    float sum = 0f;
                    if (mainCostPart != null && (mainCostPart.channelMask & bitMask) != 0) sum += mainCostPart.EnterCostValue;
                    foreach (var p in costPartList)
                    {
                        if (p != null && (p.channelMask & bitMask) != 0) sum += p.EnterCostValue;
                    }
                    if (sum > 1f + EPS) issues.Add($"通道位 {bit} 的总代价 {sum:F2} 超过容量 1.0，可能会导致冲突（检查 mainCostPart / costPartList 的 EnterCostValue）。\n");
                }
            }

            _validationIssuesCache = issues;
            _validationCacheIsDirty = false;
            return _validationIssuesCache;
        }

        public bool HasValidationIssues => GetValidationIssues().Count > 0;
        public string ValidationMessage => string.Join("\n", GetValidationIssues().ToArray());

        [HorizontalGroup("Actions")]
        [Button("验证配置合理性", ButtonSizes.Medium)]
        [GUIColor(0.9f, 0.85f, 0.5f)]
        public void ValidateConfig()
        {

            var issues = GetValidationIssues();
            if (issues.Count == 0)
            {
                Debug.Log("StateCost: 配置检查通过。");
            }
            else
            {
                ESDesignUtility.SafeEditor.Wrap_DisplayDialog("StateCost 配置警告(可以在控制台逐条查看)", string.Join("\n", issues.ToArray()), "确定");
                foreach (var s in issues) Debug.LogWarning("StateCost 配置警告: " + s);
            }
        }


        [HorizontalGroup("Actions")]
        [Button("智能补全一个缺失通道(先四肢)", ButtonSizes.Medium)]
        [GUIColor(0.6f, 0.9f, 0.6f)]
        public void AutoFillMissingChannelParts()
        {
            if (costPartList == null) costPartList = new List<StateChannelCostPart>();

            uint occupied = 0u;
            if (mainCostPart != null) occupied |= (uint)mainCostPart.channelMask;
            for (int i = 0; i < costPartList.Count; i++)
            {
                var p = costPartList[i];
                if (p != null) occupied |= (uint)p.channelMask;
            }

            // 优先级：先四肢，再手腿，头/躯干/感官/心智/目标
            var priorities = new StateChannelMask[] {
                StateChannelMask.FourLimbs,
                StateChannelMask.DoubleHand,
                StateChannelMask.DoubleLeg,
                StateChannelMask.RightHand,
                StateChannelMask.LeftHand,
                StateChannelMask.RightLeg,
                StateChannelMask.LeftLeg,
                StateChannelMask.Head,
                StateChannelMask.BodySpine,
                StateChannelMask.Eye,
                StateChannelMask.Ear,
                StateChannelMask.Heart,
                StateChannelMask.Target
            };

            bool added = false;
            foreach (var mask in priorities)
            {
                uint m = (uint)mask;
                if ((occupied & m) == 0u)
                {
                    costPartList.Add(new StateChannelCostPart() { channelMask = mask, EnterCostValue = 0.5f, ReturnFraction = 1f });
                    occupied |= m;
                    added = true;
                    InvalidateValidationCache();
                    Debug.Log($"已补全缺失通道: {mask}。");
                    break; // 只补一个
                }
            }

            if (!added)
                Debug.Log("无缺失通道需要补全。");
        }
    }
}
