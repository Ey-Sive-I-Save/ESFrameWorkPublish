using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 保存 Demo/Test 场景显式生成的区域集合。
    /// 集合由场景 Builder 写入，不在运行时扫描层级。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESDemoTestAreaSet : MonoBehaviour
    {
        [SerializeField, Min(1)] private int minimumAreaCount = 20;
        [SerializeField] private List<ESDemoTestAreaMarker> areas = new List<ESDemoTestAreaMarker>();

        public int MinimumAreaCount => minimumAreaCount;
        public IReadOnlyList<ESDemoTestAreaMarker> Areas => areas;

        public void ConfigureForAuthoring(int minimumCount, IReadOnlyList<ESDemoTestAreaMarker> markers)
        {
            minimumAreaCount = Mathf.Max(1, minimumCount);
            areas.Clear();
            if (markers == null)
                return;

            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i] != null)
                    areas.Add(markers[i]);
            }
        }

        public bool ValidateForAuthoring(out string report)
        {
            var issues = new StringBuilder();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            if (areas.Count < minimumAreaCount)
                issues.AppendLine($"测试区域数量不足：{areas.Count}/{minimumAreaCount}。");

            for (int i = 0; i < areas.Count; i++)
            {
                ESDemoTestAreaMarker marker = areas[i];
                if (marker == null)
                {
                    issues.AppendLine($"区域集合索引 {i} 为空。");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(marker.AreaId))
                    issues.AppendLine($"区域 {marker.AreaNumber:00} 缺少稳定 ID。");
                else if (!ids.Add(marker.AreaId))
                    issues.AppendLine($"区域 ID 重复：{marker.AreaId}。");

                if (string.IsNullOrWhiteSpace(marker.AreaTitle))
                    issues.AppendLine($"区域 {marker.AreaNumber:00} 缺少标题。");
            }

            report = issues.Length == 0
                ? $"测试区域集合有效：{areas.Count} 个区域，ID 唯一。"
                : issues.ToString().TrimEnd();
            return issues.Length == 0;
        }

        private void OnValidate()
        {
            minimumAreaCount = Mathf.Max(1, minimumAreaCount);
        }
    }
}
