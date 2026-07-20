using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    [Serializable]
    public class Page_SceneTextRepair : ESWindowPageBase
    {
        private static readonly Regex ObjectHeaderRegex = new Regex(@"^--- !u!\d+ &(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex FileIdOnlyLineRegex = new Regex(@"^\s*-\s*\{fileID:\s*(-?\d+)\}\s*$", RegexOptions.Compiled);

        [Title("场景文本修复", "修复 .unity 文本中 SceneRoots 残留的无效本地 fileID 引用", bold: true, titleAlignment: TitleAlignments.Centered)]
        [InfoBox("只处理 SceneRoots.m_Roots 里指向不存在对象声明的根节点 fileID。不会扫描或修改组件字段、Prefab Override、资源引用。")]
        [ShowInInspector, ReadOnly, LabelText("最近扫描结果"), MultiLineProperty(12)]
        private string lastReport = "尚未扫描。";

        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawResultSummary("最近场景文本修复", lastResultSummary, lastResultDetail);
        }

        [HorizontalGroup("SelectedActions")]
        [Button("扫描选中场景", ButtonHeight = 34), GUIColor(0.35f, 0.65f, 1f)]
        public void ScanSelectedScenes()
        {
            ScanPaths(GetSelectedScenePaths(), showDialog: true);
        }

        [HorizontalGroup("SelectedActions")]
        [Button("修复选中场景（会备份）", ButtonHeight = 34), GUIColor(0.95f, 0.65f, 0.25f)]
        public void FixSelectedScenes()
        {
            FixPathsWithConfirm(GetSelectedScenePaths());
        }

        [HorizontalGroup("OpenActions")]
        [Button("扫描已打开场景", ButtonHeight = 34), GUIColor(0.35f, 0.75f, 0.55f)]
        public void ScanOpenScenes()
        {
            ScanPaths(GetOpenScenePaths(), showDialog: true);
        }

        [HorizontalGroup("OpenActions")]
        [Button("修复已打开场景（会备份）", ButtonHeight = 34), GUIColor(0.95f, 0.55f, 0.35f)]
        public void FixOpenScenes()
        {
            FixPathsWithConfirm(GetOpenScenePaths());
        }

        private void ScanPaths(List<string> assetPaths, bool showDialog)
        {
            if (assetPaths.Count == 0)
            {
                lastReport = "没有找到可扫描的 .unity 场景。请选中场景资源，或先保存当前打开场景。";
                lastResultSummary = "扫描取消: 没有可扫描场景";
                lastResultDetail = lastReport;
                if (showDialog)
                    EditorUtility.DisplayDialog("场景文本修复", lastReport, "确定");
                return;
            }

            var reports = AnalyzePaths(assetPaths, out int issueCount);
            lastReport = BuildReportText(reports, issueCount, fixedFile: false);
            lastResultSummary = $"扫描完成: 场景 {reports.Count} 个 | 损坏条目 {issueCount} 个";
            lastResultDetail = BuildResultDetail(reports, issueCount);
            Debug.Log(lastReport);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "场景文本修复",
                    issueCount == 0 ? "没有发现损坏的 SceneRoots fileID 条目。" : $"发现 {issueCount} 个损坏条目，详情见页面和 Console。",
                    "确定");
            }
        }

        private void FixPathsWithConfirm(List<string> assetPaths)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                lastResultSummary = "修复取消: 项目未启用 Force Text";
                lastResultDetail = "请先在 Project Settings / Editor 中把 Asset Serialization 设置为 Force Text。";
                EditorUtility.DisplayDialog("需要文本序列化",
                    "这个工具只修复文本格式的 .unity 文件。\n请先在 Project Settings / Editor 中把 Asset Serialization 设置为 Force Text。",
                    "知道了");
                return;
            }

            if (assetPaths == null || assetPaths.Count == 0)
            {
                lastReport = "没有找到可修复的 .unity 场景。请选中场景资源，或先保存当前打开场景。";
                lastResultSummary = "修复取消: 没有可修复场景";
                lastResultDetail = lastReport;
                EditorUtility.DisplayDialog("场景文本修复", lastReport, "确定");
                return;
            }

            var reports = AnalyzePaths(assetPaths, out int issueCount);
            if (issueCount == 0)
            {
                lastReport = BuildReportText(reports, issueCount, fixedFile: false);
                lastResultSummary = $"修复跳过: 场景 {reports.Count} 个 | 未发现损坏条目";
                lastResultDetail = BuildResultDetail(reports, issueCount);
                EditorUtility.DisplayDialog("场景文本修复", "没有发现损坏的 SceneRoots fileID 条目。", "确定");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "场景文本修复",
                $"将从 {reports.Count} 个场景文件中移除 {issueCount} 个损坏的 SceneRoots fileID 条目。\n\n执行前会在原文件旁生成 .bak 备份，只删除同一场景文本内不存在对象声明的根节点列表行。",
                "备份并修复",
                "取消");

            if (!confirmed)
            {
                lastResultSummary = $"修复取消: 待修复 {issueCount} 个条目";
                lastResultDetail = BuildResultDetail(reports, issueCount);
                return;
            }

            if (HasDirtyOpenTargetScene(reports.Select(report => report.AssetPath)) &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                lastResultSummary = "修复取消: 目标场景有未保存改动";
                lastResultDetail = "用户取消保存未保存场景，工具未写入任何场景文本。";
                EditorUtility.DisplayDialog("场景文本修复", "已取消修复。目标场景有未保存改动，需先保存或放弃改动后再写入场景文件。", "知道了");
                return;
            }

            int fixedCount = 0;
            int failedCount = 0;
            var failedMessages = new List<string>();
            foreach (var report in reports)
            {
                if (report.MissingRootFileIds.Count == 0)
                    continue;

                try
                {
                    fixedCount += FixScene(report);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    failedMessages.Add($"{report.AssetPath}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();
            lastReport = BuildReportText(reports, fixedCount, fixedFile: true);
            lastResultSummary = $"修复完成: 修复 {fixedCount} 个条目 | 失败 {failedCount} 个场景";
            lastResultDetail = BuildResultDetail(reports, fixedCount);
            if (failedMessages.Count > 0)
                lastResultDetail += "\n\n失败项:\n" + SimpleToolsSafetyUtility.JoinPreview(failedMessages, 8);
            Debug.Log(lastReport);
            string detail = failedMessages.Count > 0 ? "\n\n失败项：\n" + SimpleToolsSafetyUtility.JoinPreview(failedMessages, 8) : string.Empty;
            EditorUtility.DisplayDialog("场景文本修复", $"已修复 {fixedCount} 个损坏条目，失败 {failedCount} 个场景。{detail}", "确定");
        }

        private static List<SceneRootReferenceReport> AnalyzePaths(List<string> assetPaths, out int issueCount)
        {
            var reports = new List<SceneRootReferenceReport>(assetPaths.Count);
            issueCount = 0;

            foreach (string assetPath in assetPaths)
            {
                if (!IsSafeSceneAssetPath(assetPath))
                    continue;

                var report = AnalyzeScene(assetPath);
                reports.Add(report);
                issueCount += report.MissingRootFileIds.Count;
            }

            return reports;
        }

        private static SceneRootReferenceReport AnalyzeScene(string assetPath)
        {
            string fullPath = ToFullPath(assetPath);
            string text = File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : string.Empty;
            string[] lines = SplitLines(text);

            var declaredIds = new HashSet<long>();
            for (int i = 0; i < lines.Length; i++)
            {
                Match headerMatch = ObjectHeaderRegex.Match(lines[i]);
                if (headerMatch.Success && long.TryParse(headerMatch.Groups[1].Value, out long id))
                    declaredIds.Add(id);
            }

            var missingRootIds = new List<MissingRootReference>();
            bool inSceneRoots = false;
            bool inRootsList = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    inSceneRoots = line.StartsWith("--- !u!1660057539 ", StringComparison.Ordinal);
                    inRootsList = false;
                    continue;
                }

                if (!inSceneRoots)
                    continue;

                if (line.StartsWith("  m_Roots:", StringComparison.Ordinal))
                {
                    inRootsList = true;
                    continue;
                }

                if (!inRootsList)
                    continue;

                if (!line.StartsWith("  - ", StringComparison.Ordinal))
                {
                    inRootsList = false;
                    continue;
                }

                Match fileIdMatch = FileIdOnlyLineRegex.Match(line);
                if (!fileIdMatch.Success || !long.TryParse(fileIdMatch.Groups[1].Value, out long rootId))
                    continue;

                if (!declaredIds.Contains(rootId))
                    missingRootIds.Add(new MissingRootReference(i + 1, rootId));
            }

            return new SceneRootReferenceReport(assetPath, fullPath, missingRootIds);
        }

        private static int FixScene(SceneRootReferenceReport report)
        {
            string text = File.ReadAllText(report.FullPath, Encoding.UTF8);
            string backupPath = report.FullPath + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak";
            File.WriteAllText(backupPath, text, new UTF8Encoding(false));

            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            string[] lines = SplitLines(text);

            var removeLineNumbers = new HashSet<int>();
            foreach (var missing in report.MissingRootFileIds)
                removeLineNumbers.Add(missing.LineNumber);

            var output = new List<string>(lines.Length - removeLineNumbers.Count);
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNumber = i + 1;
                if (!removeLineNumbers.Contains(lineNumber))
                    output.Add(lines[i]);
            }

            File.WriteAllText(report.FullPath, string.Join(newline, output), new UTF8Encoding(false));
            return report.MissingRootFileIds.Count;
        }

        private static string BuildReportText(List<SceneRootReferenceReport> reports, int issueCount, bool fixedFile)
        {
            var sb = new StringBuilder();
            sb.Append(fixedFile ? "修复完成" : "扫描完成")
                .Append(" | 条目数=")
                .Append(issueCount)
                .AppendLine();

            foreach (var report in reports)
            {
                if (report.MissingRootFileIds.Count == 0)
                {
                    sb.Append("OK: ").Append(report.AssetPath).AppendLine();
                    continue;
                }

                sb.Append(report.AssetPath).AppendLine();
                foreach (var missing in report.MissingRootFileIds)
                {
                    sb.Append("  Line ")
                        .Append(missing.LineNumber)
                        .Append(": fileID ")
                        .Append(missing.FileId)
                        .AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string BuildResultDetail(List<SceneRootReferenceReport> reports, int issueCount)
        {
            if (reports == null || reports.Count == 0)
                return "无场景。";

            var lines = reports.Select(report =>
            {
                int count = report.MissingRootFileIds != null ? report.MissingRootFileIds.Count : 0;
                return count == 0 ? $"OK: {report.AssetPath}" : $"{report.AssetPath} | 损坏条目 {count}";
            });

            return $"条目数: {issueCount}\n" + SimpleToolsSafetyUtility.JoinPreview(lines, 12);
        }

        private static List<string> GetSelectedScenePaths()
        {
            var paths = new List<string>();
            foreach (var sceneAsset in Selection.GetFiltered<SceneAsset>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(sceneAsset);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);
            }

            return paths;
        }

        private static List<string> GetOpenScenePaths()
        {
            var paths = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                    paths.Add(scene.path);
            }

            return paths;
        }

        private static bool HasDirtyOpenTargetScene(IEnumerable<string> assetPaths)
        {
            var targetPaths = new HashSet<string>(
                assetPaths.Select(SimpleToolsSafetyUtility.NormalizeAssetPath),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() &&
                    scene.isDirty &&
                    targetPaths.Contains(SimpleToolsSafetyUtility.NormalizeAssetPath(scene.path)))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ToFullPath(string assetPath)
        {
            if (!IsSafeSceneAssetPath(assetPath))
                throw new ArgumentException("Scene path must be a .unity file under Assets: " + assetPath);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            if (!fullPath.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Scene path resolved outside Assets: " + assetPath);

            return fullPath;
        }

        private static bool IsSafeSceneAssetPath(string assetPath)
        {
            assetPath = SimpleToolsSafetyUtility.NormalizeAssetPath(assetPath);
            return assetPath.StartsWith("Assets/", StringComparison.Ordinal) &&
                   !assetPath.Contains("../", StringComparison.Ordinal) &&
                   assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        private static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private readonly struct SceneRootReferenceReport
        {
            public readonly string AssetPath;
            public readonly string FullPath;
            public readonly List<MissingRootReference> MissingRootFileIds;

            public SceneRootReferenceReport(string assetPath, string fullPath, List<MissingRootReference> missingRootFileIds)
            {
                AssetPath = assetPath;
                FullPath = fullPath;
                MissingRootFileIds = missingRootFileIds;
            }
        }

        private readonly struct MissingRootReference
        {
            public readonly int LineNumber;
            public readonly long FileId;

            public MissingRootReference(int lineNumber, long fileId)
            {
                LineNumber = lineNumber;
                FileId = fileId;
            }
        }
    }
}
