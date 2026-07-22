using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal enum SimpleToolsActionTone
    {
        Neutral,
        Primary,
        Success,
        Warning,
        Danger
    }

    internal enum SimpleToolsMaturity
    {
        Industrial,
        Upgrading,
        Legacy,
        Experimental
    }

    internal sealed class SimpleToolsOperationReport
    {
        public string Title = "最近报告";
        public string Summary;
        public string Detail;
        public readonly List<string> ChangedItems = new List<string>();
        public readonly List<string> FailedItems = new List<string>();
        public readonly List<string> WarningItems = new List<string>();

        public bool HasContent =>
            !string.IsNullOrWhiteSpace(Summary) ||
            !string.IsNullOrWhiteSpace(Detail) ||
            ChangedItems.Count > 0 ||
            FailedItems.Count > 0 ||
            WarningItems.Count > 0;

        public void Clear()
        {
            Summary = null;
            Detail = null;
            ChangedItems.Clear();
            FailedItems.Clear();
            WarningItems.Clear();
        }

        public string ToText()
        {
            var lines = new List<string>
            {
                string.IsNullOrWhiteSpace(Title) ? "最近报告" : Title,
                "时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (!string.IsNullOrWhiteSpace(Summary))
                lines.Add("摘要: " + Summary);
            AppendItems(lines, "已修改", ChangedItems);
            AppendItems(lines, "警告", WarningItems);
            AppendItems(lines, "失败", FailedItems);
            if (!string.IsNullOrWhiteSpace(Detail))
            {
                lines.Add("详情:");
                lines.Add(Detail);
            }

            return string.Join("\n", lines);
        }

        private static void AppendItems(List<string> lines, string title, List<string> items)
        {
            if (items == null || items.Count == 0)
                return;

            lines.Add(title + ": " + items.Count);
            lines.AddRange(items);
        }
    }

    internal static class SimpleToolsPanelUtility
    {
        public const int DefaultPageSize = 30;
        public const int LargeListWarningThreshold = 500;
        public const int HeavyOperationWarningThreshold = 2000;
        public static readonly Color PrimaryColor = new Color(0.28f, 0.52f, 0.85f);
        public static readonly Color SuccessColor = new Color(0.25f, 0.62f, 0.45f);
        public static readonly Color WarningColor = new Color(0.78f, 0.56f, 0.22f);
        public static readonly Color DangerColor = new Color(0.82f, 0.38f, 0.30f);
        public static readonly Color NeutralColor = new Color(0.48f, 0.48f, 0.48f);
        private static readonly List<string> OperationHistory = new List<string>(32);

        public static void DrawToolHeader(string title, string purpose, SimpleToolsMaturity maturity, string risk = null)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(title ?? "未命名工具", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    DrawMaturityBadge(maturity);
                }

                if (!string.IsNullOrWhiteSpace(purpose))
                    EditorGUILayout.LabelField(purpose, EditorStyles.wordWrappedMiniLabel);

                if (!string.IsNullOrWhiteSpace(risk))
                    EditorGUILayout.HelpBox(risk, MessageType.Warning);
            }
        }

        public static void DrawMaturityBadge(SimpleToolsMaturity maturity)
        {
            string label;
            SimpleToolsActionTone tone;
            switch (maturity)
            {
                case SimpleToolsMaturity.Industrial:
                    label = "工业级";
                    tone = SimpleToolsActionTone.Success;
                    break;
                case SimpleToolsMaturity.Upgrading:
                    label = "升级中";
                    tone = SimpleToolsActionTone.Primary;
                    break;
                case SimpleToolsMaturity.Experimental:
                    label = "实验";
                    tone = SimpleToolsActionTone.Warning;
                    break;
                default:
                    label = "旧工具";
                    tone = SimpleToolsActionTone.Danger;
                    break;
            }

            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = GetToneColor(tone);
            GUILayout.Label(label, EditorStyles.miniButton, GUILayout.Width(58), GUILayout.Height(20));
            GUI.backgroundColor = previous;
        }

        public static void DrawLargeListGuard(int totalCount, string itemName = "条目")
        {
            if (totalCount >= HeavyOperationWarningThreshold)
            {
                DrawWarning($"{itemName}数量 {totalCount}，已经属于重操作。建议先用搜索/筛选缩小范围，再执行写入。");
                return;
            }

            if (totalCount >= LargeListWarningThreshold)
                EditorGUILayout.HelpBox($"{itemName}数量 {totalCount}，建议开启分页或只执行勾选项，避免面板卡顿。", MessageType.Info);
        }

        public static void DrawSectionTitle(string title, string subtitle = null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrWhiteSpace(subtitle))
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
        }

        public static void DrawSummary(params string[] items)
        {
            DrawSummary((IEnumerable<string>)items);
        }

        public static void DrawSummary(IEnumerable<string> items)
        {
            string text = string.Join("  |  ", (items ?? Enumerable.Empty<string>()).Where(item => !string.IsNullOrWhiteSpace(item)));
            if (string.IsNullOrEmpty(text))
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
        }

        public static void DrawEmptyState(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                EditorGUILayout.HelpBox(message, MessageType.Info);
        }

        public static void DrawWarning(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                EditorGUILayout.HelpBox(message, MessageType.Warning);
        }

        public static void DrawResultSummary(string title, string summary, string detail = null)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                DrawEmptyState("还没有执行结果。完成一次扫描、预览或批处理后，这里会显示最近结果。");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(title) ? "最近结果" : title, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                if (!string.IsNullOrWhiteSpace(detail))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.TextArea(detail, GUILayout.MinHeight(42), GUILayout.MaxHeight(110));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制结果", EditorStyles.miniButtonLeft, GUILayout.Width(76)))
                        EditorGUIUtility.systemCopyBuffer = BuildLegacyReportText(title, summary, detail);
                    if (GUILayout.Button("保存结果", EditorStyles.miniButtonMid, GUILayout.Width(76)))
                        SaveTextToFile(title, BuildLegacyReportText(title, summary, detail));
                    if (GUILayout.Button("记录历史", EditorStyles.miniButtonRight, GUILayout.Width(76)))
                        AddOperationHistory(BuildLegacyReportText(title, summary, detail));
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private static string BuildLegacyReportText(string title, string summary, string detail)
        {
            var lines = new List<string>
            {
                string.IsNullOrWhiteSpace(title) ? "最近结果" : title,
                "时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            if (!string.IsNullOrWhiteSpace(summary))
                lines.Add("摘要: " + summary);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                lines.Add("详情:");
                lines.Add(detail);
            }

            return string.Join("\n", lines);
        }

        public static void DrawOperationReport(SimpleToolsOperationReport report, int previewLimit = 12)
        {
            if (report == null || !report.HasContent)
            {
                DrawEmptyState("还没有执行结果。完成一次扫描、预览或批处理后，这里会显示最近报告。");
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(report.Title) ? "最近报告" : report.Title, EditorStyles.boldLabel);
                if (!string.IsNullOrWhiteSpace(report.Summary))
                    EditorGUILayout.LabelField(report.Summary, EditorStyles.wordWrappedMiniLabel);

                DrawReportList("已修改", report.ChangedItems, previewLimit, MessageType.Info);
                DrawReportList("警告", report.WarningItems, previewLimit, MessageType.Warning);
                DrawReportList("失败", report.FailedItems, previewLimit, MessageType.Error);

                if (!string.IsNullOrWhiteSpace(report.Detail))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.TextArea(report.Detail, GUILayout.MinHeight(42), GUILayout.MaxHeight(140));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("复制报告", EditorStyles.miniButtonLeft, GUILayout.Width(76)))
                        EditorGUIUtility.systemCopyBuffer = report.ToText();
                    if (GUILayout.Button("保存报告", EditorStyles.miniButtonMid, GUILayout.Width(76)))
                        SaveReportToFile(report);
                    if (GUILayout.Button("记录历史", EditorStyles.miniButtonRight, GUILayout.Width(76)))
                        AddOperationHistory(report.ToText());
                    GUILayout.FlexibleSpace();
                }
            }
        }

        public static void AddOperationHistory(string reportText)
        {
            if (string.IsNullOrWhiteSpace(reportText))
                return;

            OperationHistory.Insert(0, reportText);
            while (OperationHistory.Count > 20)
                OperationHistory.RemoveAt(OperationHistory.Count - 1);
        }

        public static void DrawOperationHistory(int previewCount = 5)
        {
            if (OperationHistory.Count == 0)
                return;

            DrawSectionTitle("操作历史", "仅保存在当前编辑器域内，用于快速复查最近工具报告。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int count = Mathf.Min(Mathf.Max(1, previewCount), OperationHistory.Count);
                for (int i = 0; i < count; i++)
                {
                    string firstLine = OperationHistory[i].Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "报告";
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"{i + 1}. {firstLine}", EditorStyles.miniLabel);
                        if (GUILayout.Button("复制", EditorStyles.miniButton, GUILayout.Width(44)))
                            EditorGUIUtility.systemCopyBuffer = OperationHistory[i];
                    }
                }
            }
        }

        private static void DrawReportList(string title, List<string> items, int previewLimit, MessageType type)
        {
            if (items == null || items.Count == 0)
                return;

            string text = string.Join("\n", items.Take(Mathf.Max(1, previewLimit)));
            if (items.Count > previewLimit)
                text += $"\n... 还有 {items.Count - previewLimit} 项";

            EditorGUILayout.HelpBox($"{title}：{items.Count}\n{text}", type);
        }

        public static List<T> PageItems<T>(IList<T> items, ref int pageIndex, int pageSize, out int totalPages)
        {
            pageSize = Mathf.Max(1, pageSize);
            int count = items != null ? items.Count : 0;
            totalPages = Mathf.Max(1, Mathf.CeilToInt(count / (float)pageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            int start = pageIndex * pageSize;
            int end = Mathf.Min(start + pageSize, count);

            List<T> result = new List<T>(Mathf.Max(0, end - start));
            for (int i = start; i < end; i++)
                result.Add(items[i]);

            return result;
        }

        public static void DrawPager(ref int pageIndex, int totalCount, int pageSize)
        {
            pageSize = Mathf.Max(1, pageSize);
            int totalPages = Mathf.Max(1, Mathf.CeilToInt(totalCount / (float)pageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = pageIndex > 0;
                if (GUILayout.Button("上一页", EditorStyles.miniButtonLeft, GUILayout.Width(64)))
                    pageIndex--;
                GUI.enabled = pageIndex < totalPages - 1;
                if (GUILayout.Button("下一页", EditorStyles.miniButtonMid, GUILayout.Width(64)))
                    pageIndex++;
                GUI.enabled = true;

                GUILayout.Label($"第 {pageIndex + 1}/{totalPages} 页  |  共 {totalCount} 项", EditorStyles.miniLabel);
            }
        }

        public static bool ConfirmHeavyOperation(string title, int targetCount, string actionDescription, string riskDescription)
        {
            string message =
                $"目标数量：{targetCount}\n\n" +
                $"将要执行：{actionDescription}\n\n" +
                $"风险说明：{riskDescription}\n\n" +
                "请确认已经预览过目标和规则。";

            return EditorUtility.DisplayDialog(title, message, "确认执行", "取消");
        }

        private static void SaveReportToFile(SimpleToolsOperationReport report)
        {
            string path = EditorUtility.SaveFilePanel(
                "保存工具报告",
                Application.dataPath,
                (string.IsNullOrWhiteSpace(report.Title) ? "SimpleToolsReport" : SanitizeFileName(report.Title)) + ".txt",
                "txt");

            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, report.ToText());
            EditorUtility.RevealInFinder(path);
        }

        private static void SaveTextToFile(string title, string text)
        {
            string path = EditorUtility.SaveFilePanel(
                "保存工具结果",
                Application.dataPath,
                (string.IsNullOrWhiteSpace(title) ? "SimpleToolsResult" : SanitizeFileName(title)) + ".txt",
                "txt");

            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, text ?? string.Empty);
            EditorUtility.RevealInFinder(path);
        }

        private static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "SimpleToolsReport";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(invalid, '_');

            return fileName;
        }

        public static bool DrawActionButton(string label, SimpleToolsActionTone tone, int height = 28, params GUILayoutOption[] options)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = GetToneColor(tone);
            bool clicked = GUILayout.Button(label, MergeHeight(height, options));
            GUI.backgroundColor = previous;
            return clicked;
        }

        private static Color GetToneColor(SimpleToolsActionTone tone)
        {
            switch (tone)
            {
                case SimpleToolsActionTone.Primary:
                    return PrimaryColor;
                case SimpleToolsActionTone.Success:
                    return SuccessColor;
                case SimpleToolsActionTone.Warning:
                    return WarningColor;
                case SimpleToolsActionTone.Danger:
                    return DangerColor;
                default:
                    return NeutralColor;
            }
        }

        private static GUILayoutOption[] MergeHeight(int height, GUILayoutOption[] options)
        {
            if (options == null || options.Length == 0)
                return new[] { GUILayout.Height(height) };

            var merged = new GUILayoutOption[options.Length + 1];
            merged[0] = GUILayout.Height(height);
            Array.Copy(options, 0, merged, 1, options.Length);
            return merged;
        }
    }
}
