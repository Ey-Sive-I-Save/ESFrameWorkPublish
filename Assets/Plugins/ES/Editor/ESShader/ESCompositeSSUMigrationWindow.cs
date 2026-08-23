using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES.EditorInternal
{
    public sealed class ESCompositeSSUMigrationWindow : EditorWindow, IESWindowPresentationShortTitle
    {
        public string ESWindow_PresentationShortTitle => "迁移";
        #region Window State And Drawing

        private static readonly string[] TargetLabels = { "自动识别", "ES 2D", "ES UI", "ES 3D Lit" };
        private static readonly string[] BlendLabels = { "自动识别", "透明", "叠加", "预乘透明", "正片叠底" };

        private readonly List<Material> sourceMaterials = new List<Material>();
        private readonly List<ESCompositeSSUMigrationReport> reports = new List<ESCompositeSSUMigrationReport>();
        private readonly HashSet<int> expandedReports = new HashSet<int>();
        private Vector2 scroll;
        private ESCompositeSSUTargetMode targetMode;
        private ESCompositeSSUBlendMode blendMode;
        private bool allowLossy;
        private bool collectionWasCanceled;
        private string outputFolder = "Assets/ES Migrated Materials";

        [MenuItem(MenuItemPathDefine.CONTENT_CREATION_PATH + "Shader/SSU 材质迁移...", false, 2101)]
        public static void Open()
        {
            ESCompositeSSUMigrationWindow window = GetWindow<ESCompositeSSUMigrationWindow>("SSU 材质迁移");
            window.minSize = new Vector2(620f, 440f);
            window.Show();
        }

        private void OnEnable()
        {
            ES.ESWindowFoundation.BindWithStandardSystemHost(
                this,
                ES.ESWindowFoundation.EnsureStandardSystemActionBar(this));
        }

        private void OnDisable()
        {
            ES.ESWindowFoundation.Unbind(this, true);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawConfiguration();
            DrawPrimaryActions();
            DrawReports();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("SSU -> ES Composite 材质迁移", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "源材质只读。工具先预览 m_SavedProperties，再生成唯一命名的新 Material；不会覆盖、改 Shader 或保存源材质。",
                MessageType.Info);

            GetReportCounts(out int ready, out int warnings, out int errors);
            string status = sourceMaterials.Count == 0
                ? "尚未读取材质。选择材质或文件夹后点击“读取当前选择”。"
                : "已分析 " + sourceMaterials.Count + " 个材质：可迁移 " + ready
                    + "，需确认 " + warnings + "，阻止迁移 " + errors + "。";
            if (collectionWasCanceled) status += " 上次读取已取消，当前结果不完整。";
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        }

        private void DrawConfiguration()
        {
            EditorGUI.BeginChangeCheck();
            targetMode = (ESCompositeSSUTargetMode)EditorGUILayout.Popup(
                new GUIContent("目标 Shader", "自动模式按 SSU Sprite、GUI 或 3D Lit Shader 选择对应 ES 目标。"),
                (int)targetMode,
                TargetLabels);
            using (new EditorGUI.DisabledScope(targetMode == ESCompositeSSUTargetMode.Lit))
            {
                blendMode = (ESCompositeSSUBlendMode)EditorGUILayout.Popup(
                    new GUIContent("混合模式", "Shader 缺失或需要覆盖自动结果时手动选择；3D Lit 按来源选择透明或裁剪。"),
                    (int)blendMode,
                    BlendLabels);
            }
            if (EditorGUI.EndChangeCheck()) RebuildReports();

            if (targetMode == ESCompositeSSUTargetMode.Lit)
                EditorGUILayout.HelpBox("3D Lit 忽略手动混合模式，并按来源 Shader 选择透明混合或透明裁剪。", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            outputFolder = EditorGUILayout.TextField(
                new GUIContent("输出目录", "必须位于当前项目 Assets 下；不存在时仅在确认迁移后创建。"),
                outputFolder);
            if (GUILayout.Button(new GUIContent("选择", "选择项目内输出目录"), GUILayout.MinWidth(56f)))
                SelectOutputFolder();
            using (new EditorGUI.DisabledScope(!AssetDatabase.IsValidFolder(outputFolder)))
            {
                if (GUILayout.Button(new GUIContent("定位", "在 Project 窗口定位输出目录"), GUILayout.MinWidth(56f)))
                {
                    UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(outputFolder);
                    Selection.activeObject = folder;
                    EditorGUIUtility.PingObject(folder);
                }
            }
            EditorGUILayout.EndHorizontal();

            allowLossy = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "允许有警告的近似迁移",
                    "只放行 Warning；多 Fade、双平铺等 Error 仍会阻止迁移。所有近似结果都必须视觉复核。"),
                allowLossy);
        }

        private void DrawPrimaryActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("读取当前选择", "读取选中的 Material，并显式扫描选中的文件夹。"), GUILayout.Height(24f)))
                CollectCurrentSelection();

            int migratable = CountMigratable();
            using (new EditorGUI.DisabledScope(migratable == 0 || !IsProjectAssetFolder(outputFolder)))
            {
                if (GUILayout.Button(
                    new GUIContent("生成迁移材质 (" + migratable + ")", "在输出目录创建新材质，不修改来源。"),
                    GUILayout.Height(24f)))
                    MigrateReadyMaterials();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrWhiteSpace(outputFolder) && !IsProjectAssetFolder(outputFolder))
                EditorGUILayout.HelpBox("输出目录必须是 Assets 或其子目录。", MessageType.Error);
        }

        private void DrawReports()
        {
            if (reports.Count == 0) return;
            EditorGUILayout.Space(4f);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < reports.Count; i++)
                DrawReport(reports[i]);
            EditorGUILayout.EndScrollView();
        }

        private void DrawReport(ESCompositeSSUMigrationReport report)
        {
            Material source = report.SourceMaterial;
            int key = source != null ? source.GetInstanceID() : report.GetHashCode();
            MessageType messageType = report.HasErrors
                ? MessageType.Error
                : report.HasWarnings
                    ? MessageType.Warning
                    : MessageType.Info;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(source, typeof(Material), false);
            string target = string.IsNullOrEmpty(report.TargetShaderName) ? "未解析" : report.TargetShaderName;
            GUILayout.Label(target, EditorStyles.miniLabel, GUILayout.MinWidth(180f));
            bool expanded = expandedReports.Contains(key);
            bool nextExpanded = GUILayout.Toggle(expanded, expanded ? "收起" : "详情", EditorStyles.miniButton, GUILayout.MinWidth(48f));
            if (nextExpanded != expanded)
            {
                if (nextExpanded) expandedReports.Add(key);
                else expandedReports.Remove(key);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "同名属性 " + report.DirectPropertyCount
                + "；近似映射 " + report.RemappedPropertyCount
                + "；参数不完整效果 " + report.PartiallyCompatibleEffectCount
                + "；范围夹取 " + report.ClampedPropertyCount
                + "；目标缺失效果 " + report.UnsupportedEnabledEffectCount + "。",
                messageType);
            if (nextExpanded)
            {
                EditorGUILayout.SelectableLabel(
                    "来源 Shader: " + (string.IsNullOrEmpty(report.SourceShaderName) ? "<missing>" : report.SourceShaderName),
                    EditorStyles.miniLabel,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
                for (int i = 0; i < report.Issues.Count; i++)
                {
                    ESCompositeSSUMigrationIssue issue = report.Issues[i];
                    EditorGUILayout.HelpBox(issue.Message, ToMessageType(issue.Severity));
                }
            }
            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Selection And Analysis

        private void CollectCurrentSelection()
        {
            sourceMaterials.Clear();
            reports.Clear();
            expandedReports.Clear();
            collectionWasCanceled = false;
            var ids = new HashSet<int>();
            UnityEngine.Object[] selected = Selection.objects;
            try
            {
                for (int i = 0; i < selected.Length; i++)
                {
                    UnityEngine.Object value = selected[i];
                    if (value is Material material)
                    {
                        AddMaterial(material, ids);
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(value);
                    if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) continue;
                    string[] guids = AssetDatabase.FindAssets("t:Material", new[] { path });
                    for (int g = 0; g < guids.Length; g++)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                            "读取 SSU 材质",
                            path + " (" + (g + 1) + "/" + guids.Length + ")",
                            guids.Length == 0 ? 1f : (float)g / guids.Length))
                        {
                            collectionWasCanceled = true;
                            break;
                        }
                        Material child = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[g]));
                        AddMaterial(child, ids);
                    }
                    if (collectionWasCanceled) break;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            RebuildReports();
            Repaint();
        }

        private void AddMaterial(Material material, HashSet<int> ids)
        {
            if (material != null && ids.Add(material.GetInstanceID())) sourceMaterials.Add(material);
        }

        private void RebuildReports()
        {
            reports.Clear();
            ESCompositeSSUBlendMode effectiveBlendMode = targetMode == ESCompositeSSUTargetMode.Lit
                ? ESCompositeSSUBlendMode.Auto
                : blendMode;
            for (int i = 0; i < sourceMaterials.Count; i++)
            {
                ESCompositeSSUMigrationReport report =
                    ESCompositeSSUMaterialMigration.Analyze(sourceMaterials[i], targetMode, effectiveBlendMode);
                reports.Add(report);
                if (report.HasErrors && report.SourceMaterial != null)
                    expandedReports.Add(report.SourceMaterial.GetInstanceID());
            }
        }

        #endregion

        #region Migration Execution

        private void MigrateReadyMaterials()
        {
            RebuildReports();
            int migratable = CountMigratable();
            if (migratable == 0) return;
            if (!TryNormalizeAssetFolder(outputFolder, out string normalizedOutputFolder))
            {
                ESDialog.InfoModal(
                    "es.composite.ssu-migration.invalid-output-folder",
                    "生成失败",
                    "输出目录必须是当前项目 Assets 内的规范路径。",
                    tone: ESDialogTone.Danger,
                    host: ESDialogHost.Editor,
                    owner: this);
                return;
            }
            if (!ESDialog.ConfirmModal(
                "es.composite.ssu-migration.confirm",
                "生成 SSU 迁移材质",
                "将在 " + normalizedOutputFolder + " 创建 " + migratable
                + " 个唯一命名的材质副本。源材质不会修改，是否继续？",
                "生成",
                "取消",
                tone: ESDialogTone.Warning,
                host: ESDialogHost.Editor,
                owner: this))
                return;

            outputFolder = normalizedOutputFolder;
            if (!EnsureAssetFolder(normalizedOutputFolder))
            {
                ESDialog.InfoModal(
                    "es.composite.ssu-migration.create-output-folder-failed",
                    "生成失败",
                    "无法创建输出目录：" + normalizedOutputFolder,
                    tone: ESDialogTone.Danger,
                    host: ESDialogHost.Editor,
                    owner: this);
                return;
            }

            var created = new List<Material>();
            int skipped = 0;
            int failed = 0;
            bool canceled = false;
            try
            {
                for (int i = 0; i < reports.Count; i++)
                {
                    ESCompositeSSUMigrationReport preview = reports[i];
                    if (EditorUtility.DisplayCancelableProgressBar(
                        "生成 SSU 迁移材质",
                        preview.SourceMaterial != null ? preview.SourceMaterial.name : "<missing>",
                        reports.Count == 0 ? 1f : (float)i / reports.Count))
                    {
                        canceled = true;
                        break;
                    }
                    if (!preview.CanMigrate(allowLossy))
                    {
                        skipped++;
                        continue;
                    }
                    Material migrated = ESCompositeSSUMaterialMigration.CreateMigratedMaterial(
                        preview.SourceMaterial,
                        targetMode,
                        targetMode == ESCompositeSSUTargetMode.Lit ? ESCompositeSSUBlendMode.Auto : blendMode,
                        allowLossy,
                        out ESCompositeSSUMigrationReport finalReport);
                    if (migrated == null || !finalReport.CanMigrate(allowLossy))
                    {
                        if (migrated != null) DestroyImmediate(migrated);
                        skipped++;
                        continue;
                    }

                    string fileName = SanitizeFileName(preview.SourceMaterial.name) + " ES.mat";
                    string path = AssetDatabase.GenerateUniqueAssetPath(normalizedOutputFolder + "/" + fileName);
                    try
                    {
                        AssetDatabase.CreateAsset(migrated, path);
                        if (!AssetDatabase.Contains(migrated))
                            throw new InvalidOperationException("AssetDatabase 未确认新材质资产。");
                    }
                    catch (Exception exception)
                    {
                        if (migrated != null && !AssetDatabase.Contains(migrated))
                            DestroyImmediate(migrated);
                        failed++;
                        Debug.LogError(
                            "[ES Composite] SSU 材质迁移失败：" + preview.SourceMaterial.name
                            + " -> " + path + "\n" + exception);
                        continue;
                    }

                    created.Add(migrated);
                    try
                    {
                        Undo.RegisterCreatedObjectUndo(migrated, "生成 SSU 迁移材质");
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[ES Composite] 已创建迁移材质，但无法注册 Undo：" + path
                            + "\n" + exception);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            for (int i = 0; i < created.Count; i++)
                AssetDatabase.SaveAssetIfDirty(created[i]);
            if (created.Count > 0)
            {
                Selection.objects = created.ToArray();
                EditorGUIUtility.PingObject(created[0]);
            }
            ESDialog.InfoModal(
                "es.composite.ssu-migration.completed",
                "SSU 材质迁移完成",
                "已创建 " + created.Count + " 个材质；跳过 " + skipped
                + " 个；失败 " + failed + " 个。请逐个对照源材质完成视觉验收。"
                + (canceled ? "\n迁移已取消，未处理的材质保持不变。" : string.Empty)
                + (failed > 0 ? "\n失败详情已写入 Console。" : string.Empty),
                "确定",
                host: ESDialogHost.Editor,
                owner: this);
        }

        #endregion

        #region Counts And Asset Paths

        private int CountMigratable()
        {
            int count = 0;
            for (int i = 0; i < reports.Count; i++)
                if (reports[i].CanMigrate(allowLossy)) count++;
            return count;
        }

        private void GetReportCounts(out int ready, out int warnings, out int errors)
        {
            ready = 0;
            warnings = 0;
            errors = 0;
            for (int i = 0; i < reports.Count; i++)
            {
                if (reports[i].HasErrors) errors++;
                else if (reports[i].HasWarnings) warnings++;
                else ready++;
            }
        }

        private void SelectOutputFolder()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string currentAbsolute = IsProjectAssetFolder(outputFolder)
                ? Path.GetFullPath(Path.Combine(projectRoot, outputFolder))
                : Application.dataPath;
            string selected = EditorUtility.OpenFolderPanel("选择 SSU 迁移输出目录", currentAbsolute, string.Empty);
            if (string.IsNullOrEmpty(selected)) return;
            string assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            string fullSelected = Path.GetFullPath(selected).Replace('\\', '/').TrimEnd('/');
            if (!fullSelected.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullSelected, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                ESDialog.InfoModal(
                    "es.composite.ssu-migration.folder-outside-assets",
                    "目录无效",
                    "输出目录必须位于当前项目 Assets 下。",
                    tone: ESDialogTone.Danger,
                    host: ESDialogHost.Editor,
                    owner: this);
                return;
            }
            string relative = fullSelected.Substring(assetsRoot.Length).TrimStart('/');
            outputFolder = string.IsNullOrEmpty(relative) ? "Assets" : "Assets/" + relative;
        }

        private static bool IsProjectAssetFolder(string path)
        {
            return TryNormalizeAssetFolder(path, out _);
        }

        private static bool EnsureAssetFolder(string path)
        {
            if (!TryNormalizeAssetFolder(path, out string normalized)) return false;
            if (AssetDatabase.IsValidFolder(normalized)) return true;
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid)) return false;
                }
                current = next;
            }
            return AssetDatabase.IsValidFolder(normalized);
        }

        private static bool TryNormalizeAssetFolder(string path, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) return false;
            try
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
                string fullPath = Path.GetFullPath(Path.Combine(projectRoot, path.Trim()))
                    .Replace('\\', '/')
                    .TrimEnd('/');
                if (!string.Equals(fullPath, assetsRoot, StringComparison.OrdinalIgnoreCase)
                    && !fullPath.StartsWith(assetsRoot + "/", StringComparison.OrdinalIgnoreCase))
                    return false;

                string relative = fullPath.Substring(assetsRoot.Length).TrimStart('/');
                normalized = string.IsNullOrEmpty(relative) ? "Assets" : "Assets/" + relative;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string SanitizeFileName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "SSU Material" : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++) result = result.Replace(invalid[i], '_');
            return result;
        }

        private static MessageType ToMessageType(ESCompositeSSUMigrationSeverity severity)
        {
            switch (severity)
            {
                case ESCompositeSSUMigrationSeverity.Error: return MessageType.Error;
                case ESCompositeSSUMigrationSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        #endregion
    }
}
