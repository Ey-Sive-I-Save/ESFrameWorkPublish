using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Marks a SimpleTools page for the restrained ES tool presentation.
    /// The marker affects editor-only rendering and never changes serialized data.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ESSimpleToolsLayoutAttribute : Attribute
    {
    }

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
        public const int MaxRenderRowsPerPage = 100;
        public const int LargeListWarningThreshold = 500;
        public const int HeavyOperationWarningThreshold = 2000;
        public static readonly Color PrimaryColor = new Color(0.28f, 0.52f, 0.85f);
        public static readonly Color SuccessColor = new Color(0.25f, 0.62f, 0.45f);
        public static readonly Color WarningColor = new Color(0.78f, 0.56f, 0.22f);
        public static readonly Color DangerColor = new Color(0.82f, 0.38f, 0.30f);
        public static readonly Color NeutralColor = new Color(0.48f, 0.48f, 0.48f);
        private static readonly List<string> OperationHistory = new List<string>(32);
        private static readonly List<string> SummaryParts = new List<string>(8);
        private static readonly Dictionary<string, bool> DetailFoldoutStates = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static GUIStyle toolTitleStyle;
        private static GUIStyle sectionTitleStyle;
        private static GUIStyle toolSubtitleStyle;
        private static bool stylesInitialized;
        private static bool stylesProSkin;
        public static void DrawToolHeader(string title, string purpose, SimpleToolsMaturity maturity, string risk = null)
        {
            EnsureStyles();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title ?? "未命名工具", toolTitleStyle);
            if (!string.IsNullOrWhiteSpace(purpose))
                EditorGUILayout.LabelField(purpose, toolSubtitleStyle);

            DrawMaturityBadge(maturity);

            if (!string.IsNullOrWhiteSpace(risk))
            {
                Color previous = GUI.contentColor;
                GUI.contentColor = EditorGUIUtility.isProSkin
                    ? new Color(0.91f, 0.74f, 0.43f)
                    : new Color(0.60f, 0.39f, 0.08f);
                EditorGUILayout.LabelField("注意：" + risk, toolSubtitleStyle);
                GUI.contentColor = previous;
            }

            DrawDivider();
        }

        public static void DrawMaturityBadge(SimpleToolsMaturity maturity)
        {
            EnsureStyles();
            Color previous = GUI.contentColor;
            GUI.contentColor = GetMaturityColor(maturity);
            EditorGUILayout.LabelField("状态：" + GetMaturityText(maturity), toolSubtitleStyle);
            GUI.contentColor = previous;
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
            EnsureStyles();
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(title ?? "未命名分区", sectionTitleStyle);
            if (!string.IsNullOrWhiteSpace(subtitle))
                EditorGUILayout.LabelField(subtitle, toolSubtitleStyle);
            DrawDivider();
        }

        /// <summary>
        /// A section title and divider already establish hierarchy. Use this scope for
        /// ordinary content instead of another help-box frame; warnings and errors use
        /// DrawWarning/DrawEmptyState separately when they carry real state.
        /// </summary>
        public static EditorGUILayout.VerticalScope BeginContentSection()
        {
            return new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true));
        }

        public static void DrawSummary(params string[] items)
        {
            DrawSummary((IEnumerable<string>)items);
        }

        public static void DrawSummary(IEnumerable<string> items)
        {
            SummaryParts.Clear();
            if (items != null)
            {
                foreach (string item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item))
                        SummaryParts.Add(item);
                }
            }

            if (SummaryParts.Count == 0)
                return;

            string text = string.Join("  |  ", SummaryParts);
            EditorGUILayout.LabelField(CompactDisplayText(text, 220), EditorStyles.wordWrappedMiniLabel);
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
                return;

            DrawSectionTitle(string.IsNullOrWhiteSpace(title) ? "最近结果" : title);
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                DrawDetailFoldout(title, detail);

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

        public static void DrawCompactDetail(string title, string detail)
        {
            DrawDetailFoldout(title, detail);
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

            DrawSectionTitle(string.IsNullOrWhiteSpace(report.Title) ? "最近报告" : report.Title);
            using (new EditorGUILayout.VerticalScope())
            {
                if (!string.IsNullOrWhiteSpace(report.Summary))
                    EditorGUILayout.LabelField(report.Summary, EditorStyles.wordWrappedMiniLabel);

                DrawReportList("已修改", report.ChangedItems, previewLimit, MessageType.Info);
                DrawReportList("警告", report.WarningItems, previewLimit, MessageType.Warning);
                DrawReportList("失败", report.FailedItems, previewLimit, MessageType.Error);

                DrawDetailFoldout(report.Title, report.Detail);

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
            using (SimpleToolsPanelUtility.BeginContentSection())
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

            int visibleCount = Mathf.Min(3, Mathf.Max(1, previewLimit));
            string text = string.Join("\n", items.Take(visibleCount).Select(item => CompactDisplayText(item, 120)));
            if (items.Count > visibleCount)
                text += $"\n... 还有 {items.Count - visibleCount} 项";

            if (type == MessageType.Warning || type == MessageType.Error)
            {
                EditorGUILayout.HelpBox($"{title}：{items.Count}\n{text}", type);
                return;
            }

            EditorGUILayout.LabelField($"{title}：{items.Count} · {text.Replace("\n", "  |  ")}", EditorStyles.wordWrappedMiniLabel);
        }

        public static List<T> PageItems<T>(IList<T> items, ref int pageIndex, int pageSize, out int totalPages)
        {
            int start;
            int end;
            GetPageRange(items, ref pageIndex, pageSize, out totalPages, out start, out end);

            List<T> result = new List<T>(Mathf.Max(0, end - start));
            for (int i = start; i < end; i++)
                result.Add(items[i]);

            return result;
        }

        /// <summary>
        /// 只计算当前页的索引范围，不创建临时集合。
        /// 预览表在 OnGUI 的 Layout/Repaint 热路径中应优先使用此方法。
        /// </summary>
        public static void GetPageRange<T>(IList<T> items, ref int pageIndex, int pageSize, out int totalPages, out int start, out int end)
        {
            pageSize = Mathf.Clamp(pageSize, 1, MaxRenderRowsPerPage);
            int count = items != null ? items.Count : 0;
            totalPages = Mathf.Max(1, Mathf.CeilToInt(count / (float)pageSize));
            pageIndex = Mathf.Clamp(pageIndex, 0, totalPages - 1);
            start = pageIndex * pageSize;
            end = Mathf.Min(start + pageSize, count);
        }

        public static void DrawPager(ref int pageIndex, int totalCount, int pageSize)
        {
            pageSize = Mathf.Clamp(pageSize, 1, MaxRenderRowsPerPage);
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

            TrySaveText(path, report.ToText(), "保存工具报告");
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

            TrySaveText(path, text ?? string.Empty, "保存工具结果");
        }

        private static bool TrySaveText(string path, string text, string operationName)
        {
            try
            {
                ESManagedFileIO.WriteTextAtUserSelectedPath(path, text ?? string.Empty, new UTF8Encoding(false));
                EditorUtility.RevealInFinder(path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog(
                    string.IsNullOrWhiteSpace(operationName) ? "保存失败" : operationName + "失败",
                    $"无法写入文件：\n{path}\n\n{ex.Message}\n\n请确认路径存在、文件未被占用且当前用户拥有写入权限。",
                    "知道了");
                return false;
            }
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
            return DrawActionButton(label, null, tone, height, options);
        }

        public static bool DrawActionButton(string label, string tooltip, SimpleToolsActionTone tone, int height = 28, params GUILayoutOption[] options)
        {
            Color previousBackground = GUI.backgroundColor;
            Color previousContent = GUI.contentColor;
            try
            {
                // A page may only promote one true next step. Success/warning/danger are
                // result states, not licenses to create more saturated primary buttons.
                if (tone == SimpleToolsActionTone.Primary)
                {
                    GUI.backgroundColor = PrimaryColor;
                    GUI.contentColor = Color.white;
                }
                return GUILayout.Button(new GUIContent(label, tooltip), EditorStyles.miniButton, MergeHeight(height, options));
            }
            finally
            {
                GUI.backgroundColor = previousBackground;
                GUI.contentColor = previousContent;
            }
        }

        public static bool DrawCompactButton(string label, int width = 96, int height = 24, GUIStyle style = null)
        {
            return DrawCompactButton(label, null, width, height, style);
        }

        public static bool DrawCompactButton(string label, string tooltip, int width = 96, int height = 24, GUIStyle style = null)
        {
            return GUILayout.Button(new GUIContent(label, tooltip), style ?? EditorStyles.miniButton, GUILayout.Width(width), GUILayout.Height(height));
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

        private static void DrawDivider()
        {
            Rect divider = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(
                divider,
                EditorGUIUtility.isProSkin
                    ? new Color(1f, 1f, 1f, 0.11f)
                    : new Color(0f, 0f, 0f, 0.11f));
        }

        private static void EnsureStyles()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            if (stylesInitialized && stylesProSkin == proSkin)
                return;

            stylesInitialized = true;
            stylesProSkin = proSkin;
            toolTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                fixedHeight = 24f,
                alignment = TextAnchor.MiddleLeft
            };
            sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                fixedHeight = 20f,
                alignment = TextAnchor.LowerLeft
            };
            toolSubtitleStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                richText = false
            };
            toolSubtitleStyle.normal.textColor = proSkin
                ? new Color(0.67f, 0.70f, 0.75f)
                : new Color(0.34f, 0.37f, 0.42f);
        }

        private static string GetMaturityText(SimpleToolsMaturity maturity)
        {
            switch (maturity)
            {
                case SimpleToolsMaturity.Industrial:
                    return "稳定可用";
                case SimpleToolsMaturity.Upgrading:
                    return "持续升级";
                case SimpleToolsMaturity.Legacy:
                    return "旧版待迁移";
                default:
                    return "试验功能";
            }
        }

        private static Color GetMaturityColor(SimpleToolsMaturity maturity)
        {
            switch (maturity)
            {
                case SimpleToolsMaturity.Industrial:
                    return EditorGUIUtility.isProSkin
                        ? new Color(0.58f, 0.80f, 0.64f)
                        : new Color(0.18f, 0.46f, 0.24f);
                case SimpleToolsMaturity.Legacy:
                    return EditorGUIUtility.isProSkin
                        ? new Color(0.78f, 0.68f, 0.48f)
                        : new Color(0.56f, 0.38f, 0.08f);
                case SimpleToolsMaturity.Experimental:
                    return EditorGUIUtility.isProSkin
                        ? new Color(0.72f, 0.62f, 0.90f)
                        : new Color(0.39f, 0.23f, 0.60f);
                default:
                    return EditorGUIUtility.isProSkin
                        ? new Color(0.63f, 0.78f, 0.96f)
                        : new Color(0.08f, 0.38f, 0.70f);
            }
        }

        private static GUILayoutOption[] MergeHeight(int height, GUILayoutOption[] options)
        {
            if (options == null || options.Length == 0)
                return new[] { GUILayout.Height(height), GUILayout.MinWidth(82), GUILayout.MaxWidth(160) };

            var merged = new GUILayoutOption[options.Length + 2];
            merged[0] = GUILayout.Height(height);
            Array.Copy(options, 0, merged, 1, options.Length);
            merged[merged.Length - 1] = GUILayout.MaxWidth(180);
            return merged;
        }

        private static void DrawDetailFoldout(string title, string detail)
        {
            if (string.IsNullOrWhiteSpace(detail))
                return;

            string key = string.IsNullOrWhiteSpace(title) ? "SimpleToolsDetail" : title;
            bool expanded = DetailFoldoutStates.TryGetValue(key, out bool value) && value;
            expanded = EditorGUILayout.Foldout(expanded, "详情（可复制或保存完整内容）", true);
            DetailFoldoutStates[key] = expanded;
            if (!expanded)
                return;

            EditorGUILayout.LabelField(CompactDisplayText(detail, 480), EditorStyles.wordWrappedMiniLabel);
        }

        private static string CompactDisplayText(string text, int maximumCharacters)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length <= maximumCharacters)
                return text;

            return text.Substring(0, Mathf.Max(1, maximumCharacters)) + "...";
        }

    }

    /// <summary>
    /// Legacy SimpleTools pages were composed from many Odin boxes, title groups and
    /// per-field InfoBoxes. Remove decorative containers only; stable horizontal and
    /// vertical field columns remain available for dense configuration. Nested result
    /// and table models retain their deliberate table layouts.
    /// </summary>
    [ResolverPriority(-150000)]
    public sealed class ESSimpleToolsLayoutAttributeProcessor : OdinAttributeProcessor
    {
        public override bool CanProcessChildMemberAttributes(InspectorProperty parent, MemberInfo member)
        {
            Type targetType = parent?.Tree?.TargetType;
            return member != null
                   && targetType != null
                   && member.DeclaringType == targetType
                   && Attribute.IsDefined(targetType, typeof(ESSimpleToolsLayoutAttribute), false);
        }

        public override void ProcessChildMemberAttributes(
            InspectorProperty parent,
            MemberInfo member,
            List<Attribute> attributes)
        {
            if (attributes == null)
                return;

            attributes.RemoveAll(IsLegacyPageDecoration);

        }

        private static bool IsLegacyPageDecoration(Attribute attribute)
        {
            return attribute is InfoBoxAttribute
                   || attribute is BoxGroupAttribute
                   || attribute is TitleGroupAttribute
                   || attribute is TabGroupAttribute
                   || attribute is PropertySpaceAttribute;
        }
    }
}
