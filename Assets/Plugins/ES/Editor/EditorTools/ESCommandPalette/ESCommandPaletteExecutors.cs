using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public sealed class ESWindowDescriptor
    {
        public ESWindowDescriptor(string windowId, string menuPath, string title, string category, string keywords)
        {
            WindowId = windowId;
            MenuPath = menuPath;
            Title = title;
            Category = category;
            Keywords = keywords ?? string.Empty;
        }

        public string WindowId { get; }
        public string MenuPath { get; }
        public string Title { get; }
        public string Category { get; }
        public string Keywords { get; }
    }

    /// <summary>
    /// Descriptor-only window registry used by the command palette. It intentionally does not
    /// reuse ESWindowCommandRegistry because that legacy launcher accepts executable callbacks.
    /// </summary>
    public static class ESWindowRegistry
    {
        private static readonly Dictionary<string, ESWindowDescriptor> Descriptors =
            new Dictionary<string, ESWindowDescriptor>(StringComparer.Ordinal);
        private static readonly List<ESWindowDescriptor> OrderedDescriptors = new List<ESWindowDescriptor>();

        static ESWindowRegistry()
        {
            RegisterBuiltIn("asset_window", MenuItemPathDefine.QUICK_WINDOWS_PATH + "资产管理窗口", "资产管理窗口", "资源与发布", "Library Catalog 资源");
            RegisterBuiltIn("so_data_window", MenuItemPathDefine.QUICK_WINDOWS_PATH + "SO 数据窗口", "SO 数据窗口", "内容制作", "ScriptableObject 配置");
            RegisterBuiltIn("simple_tools", MenuItemPathDefine.QUICK_WINDOWS_PATH + "简单工具集", "简单工具集", "开发与维护", "工具 批处理");
            RegisterBuiltIn("runtime_watch", MenuItemPathDefine.QUICK_WINDOWS_PATH + "RuntimeWatch", "RuntimeWatch", "运行时诊断", "运行时 观察 监控");
            RegisterBuiltIn("track_editor", MenuItemPathDefine.QUICK_WINDOWS_PATH + "轨道编辑器", "轨道编辑器", "内容制作", "技能 Timeline Clip");
            RegisterBuiltIn("stable_graph_v2", MenuItemPathDefine.QUICK_WINDOWS_PATH + "稳定图编辑器 V2", "稳定图编辑器 V2", "内容制作", "Graph 流程 行为树");
            RegisterBuiltIn("font_workbench", MenuItemPathDefine.QUICK_WINDOWS_PATH + "字体资产工作台", "字体资产工作台", "内容制作", "TMP 字符集 Fallback");
            RegisterBuiltIn("cmd_agent", MenuItemPathDefine.QUICK_WINDOWS_PATH + "Cmd Agent", "Cmd Agent", "自动化", "Codex 命令 AI");
        }

        public static IReadOnlyList<ESWindowDescriptor> All => OrderedDescriptors;

        public static bool RegisterWindow(
            string windowId,
            string menuPath,
            string title,
            string category,
            string keywords,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                reason = "windowId 为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(menuPath)
                || !menuPath.StartsWith(MenuItemPathDefine.ROOT_PATH, StringComparison.Ordinal))
            {
                reason = "menuPath 不在 【ES】/ 受管根";
                return false;
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(category))
            {
                reason = "title 或 category 为空";
                return false;
            }

            if (Descriptors.ContainsKey(windowId))
            {
                reason = "windowId 已注册：" + windowId;
                return false;
            }

            var descriptor = new ESWindowDescriptor(
                windowId,
                menuPath,
                title,
                category,
                keywords ?? string.Empty);
            Descriptors.Add(windowId, descriptor);
            OrderedDescriptors.Add(descriptor);
            reason = string.Empty;
            return true;
        }

        public static bool TryResolve(string windowId, out ESWindowDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                descriptor = null;
                return false;
            }

            return Descriptors.TryGetValue(windowId, out descriptor);
        }

        private static void RegisterBuiltIn(string windowId, string menuPath, string title, string category, string keywords)
        {
            if (string.IsNullOrWhiteSpace(windowId)
                || string.IsNullOrWhiteSpace(menuPath)
                || !menuPath.StartsWith(MenuItemPathDefine.ROOT_PATH, StringComparison.Ordinal)
                || Descriptors.ContainsKey(windowId))
            {
                throw new InvalidOperationException("ESWindowRegistry 内置描述无效或重复：" + windowId);
            }

            var descriptor = new ESWindowDescriptor(windowId, menuPath, title, category, keywords);
            Descriptors.Add(windowId, descriptor);
            OrderedDescriptors.Add(descriptor);
        }
    }

    public static class ESCommandPaletteMenuRegistry
    {
        // v1 has no generic menu command. Additions must be reviewed as read-only and explicitly listed here.
        private static readonly HashSet<string> ReadOnlyMenuPaths = new HashSet<string>(StringComparer.Ordinal);

        public static bool IsWhitelisted(string menuPath)
        {
            return !string.IsNullOrWhiteSpace(menuPath)
                && menuPath.StartsWith(MenuItemPathDefine.ROOT_PATH, StringComparison.Ordinal)
                && ReadOnlyMenuPaths.Contains(menuPath);
        }
    }

    public static class ESCommandPalettePathPolicy
    {
        public const int MaximumFileBytes = 1024 * 1024;
        public const string AICommandRoot = "Assets/Plugins/ES/AICommands";
        public const string GlobalDataRoot = "Assets/ESNormalAssets/Data/GlobalData";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static string ProjectRoot
        {
            get
            {
                string dataPath = Path.GetFullPath(Application.dataPath);
                return Directory.GetParent(dataPath)?.FullName ?? dataPath;
            }
        }

        public static bool TryValidateAICommandFile(string projectRelativePath, out string normalizedPath, out string reason)
        {
            normalizedPath = string.Empty;
            reason = string.Empty;

            if (string.IsNullOrWhiteSpace(projectRelativePath))
            {
                reason = "文件路径为空";
                return false;
            }

            if (Path.IsPathRooted(projectRelativePath))
            {
                reason = "拒绝绝对路径";
                return false;
            }

            string candidate = projectRelativePath.Replace('\\', '/').Trim();
            string[] segments = candidate.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == ".." || segments[i] == "." || segments[i].Length == 0)
                {
                    reason = "路径包含空段或目录穿越";
                    return false;
                }
            }

            if (!candidate.StartsWith(AICommandRoot + "/", StringComparison.Ordinal)
                || !candidate.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                reason = "文件不在 AICommand 受管根或扩展名不允许";
                return false;
            }

            string projectRoot = ProjectRoot;
            string managedRoot = Path.GetFullPath(Path.Combine(projectRoot, AICommandRoot.Replace('/', Path.DirectorySeparatorChar)));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, candidate.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsSameOrChildPath(managedRoot, fullPath) || !File.Exists(fullPath))
            {
                reason = "文件不存在或越出 AICommand 受管根";
                return false;
            }

            if (ContainsReparsePoint(projectRoot, fullPath))
            {
                reason = "路径穿过 junction 或 symlink";
                return false;
            }

            try
            {
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > MaximumFileBytes)
                {
                    reason = "文件超过 1 MiB 上限";
                    return false;
                }

                StrictUtf8.GetString(File.ReadAllBytes(fullPath));
            }
            catch (Exception exception)
            {
                reason = "文件不是严格 UTF-8 或无法读取：" + exception.Message;
                return false;
            }

            normalizedPath = candidate;
            return true;
        }

        public static bool IsRegisteredScene(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath)
                || Path.IsPathRooted(projectRelativePath)
                || !projectRelativePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !projectRelativePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = projectRelativePath.Replace('\\', '/');
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];
                if (scene != null && scene.enabled && string.Equals(scene.path, normalized, StringComparison.Ordinal))
                {
                    string fullPath = Path.Combine(ProjectRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
                    return File.Exists(fullPath) && !ContainsReparsePoint(ProjectRoot, fullPath);
                }
            }

            return false;
        }

        public static bool IsRegisteredGlobalData(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath)
                || Path.IsPathRooted(projectRelativePath)
                || !projectRelativePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !projectRelativePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string candidate = projectRelativePath.Replace('\\', '/').Trim();
            string[] segments = candidate.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == ".." || segments[i] == "." || segments[i].Length == 0)
                {
                    return false;
                }
            }

            if (!candidate.StartsWith(GlobalDataRoot + "/", StringComparison.Ordinal))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(Path.Combine(ProjectRoot, candidate.Replace('/', Path.DirectorySeparatorChar)));
            return File.Exists(fullPath) && !ContainsReparsePoint(ProjectRoot, fullPath);
        }

        private static bool IsSameOrChildPath(string rootPath, string candidatePath)
        {
            string normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedRoot, candidatePath, StringComparison.OrdinalIgnoreCase)
                || candidatePath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsReparsePoint(string projectRoot, string targetPath)
        {
            string current = targetPath;
            string normalizedRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!string.IsNullOrEmpty(current) && IsSameOrChildPath(normalizedRoot, current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }
                }
                catch
                {
                    return true;
                }

                if (string.Equals(current.TrimEnd(Path.DirectorySeparatorChar), normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            return false;
        }
    }

    public static class ESCommandPaletteExecutors
    {
        public static ESCommandPaletteResult Execute(ESCommandPaletteItem item)
        {
            if (item == null)
            {
                return ESCommandPaletteResult.Fail("命令不存在", "刷新命令面板索引");
            }

            if (item.IsMutating || item.RequiresConfirmation)
            {
                return ESCommandPaletteResult.Fail("v1 拒绝执行带写入或确认要求的命令", "移除该注册项");
            }

            switch (item.ActionKind)
            {
                case ESCommandPaletteActionKind.OpenMenu:
                    return OpenMenuExecutor.Execute(item);
                case ESCommandPaletteActionKind.OpenWindow:
                    return OpenWindowExecutor.Execute(item);
                case ESCommandPaletteActionKind.OpenFile:
                    return OpenFileExecutor.Execute(item);
                case ESCommandPaletteActionKind.OpenAsset:
                    return OpenAssetExecutor.Execute(item);
                case ESCommandPaletteActionKind.CopyText:
                    return CopyTextExecutor.Execute(item);
                case ESCommandPaletteActionKind.CopyPath:
                    return CopyTextExecutor.CopyPath(item);
                case ESCommandPaletteActionKind.Select:
                    return SelectExecutor.Execute(item);
                default:
                    return ESCommandPaletteResult.Fail("不支持的命令动作");
            }
        }
    }

    internal static class OpenMenuExecutor
    {
        public static ESCommandPaletteResult Execute(ESCommandPaletteItem item)
        {
            if (item == null
                || item.ActionKind != ESCommandPaletteActionKind.OpenMenu
                || item.IsMutating
                || !ESCommandPaletteMenuRegistry.IsWhitelisted(item.TargetId))
            {
                return ESCommandPaletteResult.Fail("ES 菜单未进入只读白名单", "移除或修正 Provider 注册项");
            }

            return EditorApplication.ExecuteMenuItem(item.TargetId)
                ? ESCommandPaletteResult.Ok("已执行 " + item.Title)
                : ESCommandPaletteResult.Fail("ES 菜单当前不可用：" + item.TargetId, "确认 Unity 已完成导入");
        }
    }

    internal static class OpenWindowExecutor
    {
        public static ESCommandPaletteResult Execute(ESCommandPaletteItem item)
        {
            if (item == null
                || item.ActionKind != ESCommandPaletteActionKind.OpenWindow
                || !ESWindowRegistry.TryResolve(item.TargetId, out ESWindowDescriptor descriptor))
            {
                return ESCommandPaletteResult.Fail("窗口 ID 未注册", "刷新命令面板索引");
            }

            return EditorApplication.ExecuteMenuItem(descriptor.MenuPath)
                ? ESCommandPaletteResult.Ok("已打开 " + descriptor.Title)
                : ESCommandPaletteResult.Fail("窗口菜单当前不可用：" + descriptor.MenuPath, "确认 Unity 已完成导入");
        }
    }

    internal static class OpenFileExecutor
    {
        public static ESCommandPaletteResult Execute(ESCommandPaletteItem item)
        {
            if (item == null || item.ActionKind != ESCommandPaletteActionKind.OpenFile)
            {
                return ESCommandPaletteResult.Fail("文件命令类型无效");
            }

            if (!ESCommandPalettePathPolicy.TryValidateAICommandFile(item.TargetId, out string normalizedPath, out string reason))
            {
                return ESCommandPaletteResult.Fail("文件路径未通过受管根校验：" + reason, "刷新 AICommand 索引");
            }

            try
            {
                string fullPath = Path.Combine(ESCommandPalettePathPolicy.ProjectRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
                EditorUtility.OpenWithDefaultApp(fullPath);
                return ESCommandPaletteResult.Ok("已打开 " + normalizedPath);
            }
            catch (Exception exception)
            {
                return ESCommandPaletteResult.Fail("文件打开失败：" + exception.Message, "检查系统文件关联和访问权限");
            }
        }
    }

    internal static class OpenAssetExecutor
    {
        public static ESCommandPaletteResult Execute(ESCommandPaletteItem item)
        {
            if (item == null
                || item.ActionKind != ESCommandPaletteActionKind.OpenAsset
                || !ESCommandPalettePathPolicy.IsRegisteredGlobalData(item.TargetId))
            {
                return ESCommandPaletteResult.Fail("GlobalData 资产未通过受管根校验", "刷新 GlobalData 索引");
            }

            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.TargetId);
            if (asset == null)
            {
                return ESCommandPaletteResult.Fail("GlobalData 资产无法加载：" + item.TargetId, "刷新 AssetDatabase");
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            if (!AssetDatabase.OpenAsset(asset))
            {
                return ESCommandPaletteResult.Ok(
                    "GlobalData 无专用打开器，已在 Project 和 Inspector 中定位："
                    + item.TargetId);
            }

            return ESCommandPaletteResult.Ok("已打开 GlobalData " + item.TargetId);
        }
    }

    internal static class CopyTextExecutor
    {
        public static ESCommandPaletteResult Execute(ESCommandPaletteItem item)
        {
            if (item == null || item.ActionKind != ESCommandPaletteActionKind.CopyText)
            {
                return ESCommandPaletteResult.Fail("复制文本命令类型无效");
            }

            if (!ESCommandPalettePathPolicy.TryValidateAICommandFile(item.TargetId, out string normalizedPath, out string reason))
            {
                return ESCommandPaletteResult.Fail("文件路径未通过受管根校验：" + reason, "刷新 AICommand 索引");
            }

            try
            {
                string fullPath = Path.Combine(ESCommandPalettePathPolicy.ProjectRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
                GUIUtility.systemCopyBuffer = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
                return ESCommandPaletteResult.Ok("已复制 " + normalizedPath + " 的文本");
            }
            catch (Exception exception)
            {
                return ESCommandPaletteResult.Fail("文件读取失败：" + exception.Message, "检查文件编码和访问权限");
            }
        }

        public static ESCommandPaletteResult CopyPath(ESCommandPaletteItem item)
        {
            if (item == null || item.ActionKind != ESCommandPaletteActionKind.CopyPath)
            {
                return ESCommandPaletteResult.Fail("复制路径命令类型无效");
            }

            if (!ESCommandPalettePathPolicy.TryValidateAICommandFile(item.TargetId, out string normalizedPath, out string reason))
            {
                return ESCommandPaletteResult.Fail("文件路径未通过受管根校验：" + reason, "刷新 AICommand 索引");
            }

            GUIUtility.systemCopyBuffer = normalizedPath;
            return ESCommandPaletteResult.Ok("已复制路径 " + normalizedPath);
        }
    }

    internal static class SelectExecutor
    {
        public static ESCommandPaletteResult Execute(ESCommandPaletteItem item)
        {
            if (item == null || item.ActionKind != ESCommandPaletteActionKind.Select)
            {
                return ESCommandPaletteResult.Fail("定位命令类型无效", "刷新命令面板索引");
            }

            if (!ESCommandPalettePathPolicy.IsRegisteredScene(item.TargetId)
                && !ESCommandPalettePathPolicy.IsRegisteredGlobalData(item.TargetId)
                && !ESCommandPalettePathPolicy.TryValidateAICommandFile(item.TargetId, out _, out _))
            {
                return ESCommandPaletteResult.Fail("场景、GlobalData 或 AICommand 未通过受管根校验", "刷新命令面板索引");
            }

            UnityEngine.Object sceneAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.TargetId);
            if (sceneAsset == null)
            {
                return ESCommandPaletteResult.Fail("资产无法加载：" + item.TargetId, "刷新 AssetDatabase");
            }

            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            return ESCommandPaletteResult.Ok("已定位资产 " + item.TargetId);
        }
    }
}
