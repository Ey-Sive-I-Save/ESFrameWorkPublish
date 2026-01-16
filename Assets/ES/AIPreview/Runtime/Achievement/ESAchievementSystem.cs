using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES.AIPreview.Achievement
{
    /// <summary>
    /// 成就定义资产：
    /// - 可通过配置条件 Key + 参数的方式实现数据驱动；
    /// - 运行时由 AchievementSystem 解释这些条件。
    /// </summary>
    [CreateAssetMenu(menuName = "ES/Preview/Achievement/AchievementDefinition")]
    public class AchievementDefinition : ScriptableObject
    {
        public string Id;
        public string Title;
        [TextArea]
        public string Description;
        public Sprite Icon;

        [Header("条件 Key，由运行时解释")]
        public string ConditionKey;
        public int TargetValue = 1;
    }

    /// <summary>
    /// 运行时成就进度。
    /// </summary>
    [Serializable]
    public class AchievementProgress
    {
        public string AchievementId;
        public int CurrentValue;
        public bool Unlocked;
    }

    /// <summary>
    /// 成就系统原型：
    /// - 接收外部事件（通过 IncreaseProgress）；
    /// - 当某条件满足时标记成就达成并触发回调。
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public List<AchievementDefinition> Definitions = new List<AchievementDefinition>();

        private readonly Dictionary<string, AchievementProgress> _progress = new Dictionary<string, AchievementProgress>();

        public event Action<AchievementDefinition> OnUnlocked;

        private void Awake()
        {
            foreach (var def in Definitions)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (!_progress.ContainsKey(def.Id))
                {
                    _progress[def.Id] = new AchievementProgress
                    {
                        AchievementId = def.Id,
                        CurrentValue = 0,
                        Unlocked = false
                    };
                }
            }
        }

        /// <summary>
        /// 提升某个条件 Key 对应的所有成就的进度。
        /// 比如：ConditionKey = "KillEnemy"，调用 IncreaseProgress("KillEnemy", 1)。
        /// </summary>
        public void IncreaseProgress(string conditionKey, int delta)
        {
            if (delta <= 0) return;

            foreach (var def in Definitions)
            {
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (!string.Equals(def.ConditionKey, conditionKey, StringComparison.Ordinal))
                    continue;

                if (!_progress.TryGetValue(def.Id, out var p))
                {
                    p = new AchievementProgress
                    {
                        AchievementId = def.Id,
                        CurrentValue = 0,
                        Unlocked = false
                    };
                    _progress[def.Id] = p;
                }

                if (p.Unlocked)
                    continue;

                p.CurrentValue += delta;
                if (p.CurrentValue >= def.TargetValue)
                {
                    p.Unlocked = true;
                    OnUnlocked?.Invoke(def);
                }
            }
        }

        public bool IsUnlocked(string achievementId)
        {
            return _progress.TryGetValue(achievementId, out var p) && p.Unlocked;
        }

        public int GetProgress(string achievementId)
        {
            return _progress.TryGetValue(achievementId, out var p) ? p.CurrentValue : 0;
        }
    }
}
