using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    [System.Serializable]
    public sealed class ESAICommandCatalogEntry
    {
        public string id = string.Empty;
        public string path = string.Empty;
        public string title = string.Empty;
        public string summary = string.Empty;
        public string role = string.Empty;
        public string riskLevel = string.Empty;
        public string writeMode = string.Empty;
        public string keywords = string.Empty;
    }

    [System.Serializable]
    internal sealed class ESAICommandCatalogDocument
    {
        public int schemaVersion;
        public string catalogTitle = string.Empty;
        public string catalogPurpose = string.Empty;
        public ESAICommandCatalogEntry[] commands = System.Array.Empty<ESAICommandCatalogEntry>();
    }

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
            RegisterBuiltIn("asset_window", MenuItemPathDefine.RESOURCE_WINDOW_PATH, "资产管理窗口", "资源与发布", "Library Catalog 资源");
            RegisterBuiltIn("so_data_window", MenuItemPathDefine.SO_DATA_WINDOW_PATH, "SO 数据窗口", "内容制作", "ScriptableObject 配置");
            RegisterBuiltIn("simple_tools", MenuItemPathDefine.SIMPLE_TOOLS_WINDOW_PATH, "简单工具集", "自动化与开发", "工具 批处理");
            RegisterBuiltIn("runtime_watch", MenuItemPathDefine.RUNTIME_WATCH_WINDOW_PATH, "RuntimeWatch", "验证与诊断", "运行时 观察 监控");
            RegisterBuiltIn("track_editor", MenuItemPathDefine.TRACK_EDITOR_WINDOW_PATH, "轨道编辑器", "内容制作", "技能 Timeline Clip");
            RegisterBuiltIn("stable_graph_v2", MenuItemPathDefine.STABLE_GRAPH_WINDOW_PATH, "稳定图编辑器 V2", "内容制作", "Graph 流程 行为树");
            RegisterBuiltIn("font_workbench", MenuItemPathDefine.FONT_WORKBENCH_WINDOW_PATH, "字体资产工具", "内容制作", "TMP 字符集 Fallback");
            RegisterBuiltIn("cmd_agent", MenuItemPathDefine.AGENT_WORKBENCH_WINDOW_PATH, "Agent 控制台", "自动化与开发", "Codex 命令 AI Agent");
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
        public const string AICommandCatalogPath = AICommandRoot + "/AICommandCatalog.json";
        public const string GlobalDataRoot = "Assets/ESNormalAssets/Data/GlobalData";
        private const int StableReadAttempts = 2;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly HashSet<string> AllowedAICommandRoles = new HashSet<string>(StringComparer.Ordinal)
        {
            "information", "review", "controlled-execution", "candidate-generation", "handover"
        };
        private static readonly HashSet<string> AllowedAICommandWriteModes = new HashSet<string>(StringComparer.Ordinal)
        {
            "read-only", "scoped-write", "candidate-only", "documentation-write", "external-run"
        };

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
            return TryResolveAICommandFile(projectRelativePath, ".md", true, out normalizedPath,
                out _, out reason);
        }

        /// <summary>
        /// Loads the small discovery catalog only. It validates paths and catalog metadata, but it
        /// deliberately does not read every Markdown contract. The selected contract is read and
        /// hashed later, immediately before it is handed to an AI session.
        /// </summary>
        public static bool TryReadAICommandCatalog(out List<ESAICommandCatalogEntry> entries,
            out string catalogHash, out string reason)
        {
            entries = new List<ESAICommandCatalogEntry>();
            catalogHash = string.Empty;
            reason = string.Empty;
            if (!TryResolveAICommandFile(AICommandCatalogPath, ".json", true, out _, out string fullPath,
                    out reason))
            {
                return false;
            }

            if (!TryReadStableUtf8File(fullPath, out string text, out catalogHash, out reason))
            {
                return false;
            }

            ESAICommandCatalogDocument document;
            try
            {
                document = JsonUtility.FromJson<ESAICommandCatalogDocument>(text);
            }
            catch (Exception exception)
            {
                reason = "AICommand 目录 JSON 解析失败：" + exception.Message;
                return false;
            }

            if (document == null || document.schemaVersion != 1 || document.commands == null)
            {
                reason = "AICommand 目录 schemaVersion 必须为 1 且包含 commands";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < document.commands.Length; index++)
            {
                ESAICommandCatalogEntry entry = document.commands[index];
                if (!TryValidateAICommandCatalogEntry(entry, ids, paths, out reason))
                {
                    reason = "AICommand 目录第 " + (index + 1) + " 项无效：" + reason;
                    return false;
                }
                if (!TryResolveAICommandFile(entry.path, ".md", false, out string normalizedPath,
                        out _, out reason))
                {
                    reason = "AICommand 目录项 " + entry.id + " 指向无效合同：" + reason;
                    return false;
                }
                entry.path = normalizedPath;
                entries.Add(entry);
            }

            if (entries.Count == 0)
            {
                reason = "AICommand 目录没有可选择的任务合同";
                return false;
            }
            return true;
        }

        public static bool TryCreateAICommandReference(string commandId, string expectedPath,
            string expectedCatalogHash, string expectedCommandHash, out ESAICommandCatalogEntry entry,
            out string reference, out string reason)
        {
            entry = null;
            reference = string.Empty;
            reason = string.Empty;
            if (!TryReadAICommandCatalog(out List<ESAICommandCatalogEntry> entries, out string catalogHash,
                    out reason))
            {
                return false;
            }
            if (!string.Equals(catalogHash, expectedCatalogHash, StringComparison.Ordinal))
            {
                reason = "AICommand 目录已变化；请重新选择任务合同后再发送";
                return false;
            }

            for (int index = 0; index < entries.Count; index++)
            {
                ESAICommandCatalogEntry candidate = entries[index];
                if (string.Equals(candidate.id, commandId, StringComparison.Ordinal)
                    && string.Equals(candidate.path, expectedPath, StringComparison.Ordinal))
                {
                    entry = candidate;
                    break;
                }
            }
            if (entry == null)
            {
                reason = "已选 AICommand 不再位于当前目录；请重新选择";
                return false;
            }

            if (!TryReadAICommandContract(entry.path, out _, out string commandHash, out reason))
            {
                return false;
            }
            if (!string.Equals(commandHash, expectedCommandHash, StringComparison.Ordinal))
            {
                reason = "AICommand 正文已变化；请重新选择并确认当前合同后再发送";
                return false;
            }

            reference = "合同版本：1\n"
                + "合同 ID：" + entry.id + "\n"
                + "合同角色：" + entry.role + "\n"
                + "风险等级：" + entry.riskLevel + "\n"
                + "写入模式：" + entry.writeMode + "\n"
                + "合同路径（项目相对路径）：" + entry.path + "\n"
                + "合同 SHA-256：" + commandHash + "\n"
                + "目录 SHA-256：" + catalogHash + "\n"
                + "摘要：" + entry.summary + "\n"
                + "执行门禁：必须先用 UTF-8 读取该 Markdown 全文并重新计算 SHA-256；"
                + "若与上述 Hash 不同，停止执行并报告合同漂移。该目录摘要不替代正文，也不扩大用户当前授权。";
            return true;
        }

        public static bool TryReadAICommandContract(string projectRelativePath, out string text,
            out string sha256, out string reason)
        {
            text = string.Empty;
            sha256 = string.Empty;
            if (!TryResolveAICommandFile(projectRelativePath, ".md", true, out _, out string fullPath,
                    out reason))
            {
                return false;
            }
            return TryReadStableUtf8File(fullPath, out text, out sha256, out reason);
        }

        private static bool TryValidateAICommandCatalogEntry(ESAICommandCatalogEntry entry,
            ISet<string> ids, ISet<string> paths, out string reason)
        {
            reason = string.Empty;
            if (entry == null)
            {
                reason = "目录项为空";
                return false;
            }
            if (!IsSafeCatalogId(entry.id) || !ids.Add(entry.id))
            {
                reason = "id 必须唯一且只使用小写字母、数字、点和连字符：" + (entry.id ?? string.Empty);
                return false;
            }
            if (string.IsNullOrWhiteSpace(entry.title) || entry.title.Trim().Length > 80
                || string.IsNullOrWhiteSpace(entry.summary) || entry.summary.Trim().Length > 240)
            {
                reason = "title 或 summary 缺失或超过目录展示上限";
                return false;
            }
            if (!AllowedAICommandRoles.Contains(entry.role ?? string.Empty)
                || !AllowedAICommandWriteModes.Contains(entry.writeMode ?? string.Empty)
                || !(entry.riskLevel == "L1" || entry.riskLevel == "L2" || entry.riskLevel == "L3"))
            {
                reason = "role、writeMode 或 riskLevel 不在允许枚举内";
                return false;
            }
            if (string.IsNullOrWhiteSpace(entry.keywords) || entry.keywords.Trim().Length > 320)
            {
                reason = "keywords 缺失或超过目录展示上限";
                return false;
            }
            if (!paths.Add(entry.path ?? string.Empty))
            {
                reason = "path 重复：" + (entry.path ?? string.Empty);
                return false;
            }
            return true;
        }

        private static bool IsSafeCatalogId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 3 || value.Length > 80)
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed = character >= 'a' && character <= 'z'
                    || character >= '0' && character <= '9'
                    || character == '.' || character == '-';
                if (!allowed)
                    return false;
            }
            return value[0] != '.' && value[0] != '-';
        }

        private static bool TryResolveAICommandFile(string projectRelativePath, string requiredExtension,
            bool validateUtf8, out string normalizedPath, out string fullPath, out string reason)
        {
            normalizedPath = string.Empty;
            fullPath = string.Empty;
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
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index] == ".." || segments[index] == "." || segments[index].Length == 0)
                {
                    reason = "路径包含空段或目录穿越";
                    return false;
                }
            }
            if (!candidate.StartsWith(AICommandRoot + "/", StringComparison.Ordinal)
                || !candidate.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                reason = "文件不在 AICommand 受管根或扩展名不允许";
                return false;
            }

            string projectRoot = ProjectRoot;
            string managedRoot = Path.GetFullPath(Path.Combine(projectRoot,
                AICommandRoot.Replace('/', Path.DirectorySeparatorChar)));
            string candidateFullPath = Path.GetFullPath(Path.Combine(projectRoot,
                candidate.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsSameOrChildPath(managedRoot, candidateFullPath) || !File.Exists(candidateFullPath))
            {
                reason = "文件不存在或越出 AICommand 受管根";
                return false;
            }
            if (ContainsReparsePoint(projectRoot, candidateFullPath))
            {
                reason = "路径穿过 junction 或 symlink";
                return false;
            }
            try
            {
                var fileInfo = new FileInfo(candidateFullPath);
                if (fileInfo.Length > MaximumFileBytes)
                {
                    reason = "文件超过 1 MiB 上限";
                    return false;
                }
                if (validateUtf8)
                    StrictUtf8.GetString(File.ReadAllBytes(candidateFullPath));
            }
            catch (Exception exception)
            {
                reason = "文件不是严格 UTF-8 或无法读取：" + exception.Message;
                return false;
            }
            normalizedPath = candidate;
            fullPath = candidateFullPath;
            return true;
        }

        private static bool TryReadStableUtf8File(string fullPath, out string text, out string sha256,
            out string reason)
        {
            text = string.Empty;
            sha256 = string.Empty;
            reason = string.Empty;
            for (int attempt = 0; attempt < StableReadAttempts; attempt++)
            {
                try
                {
                    byte[] first = File.ReadAllBytes(fullPath);
                    string firstHash = ComputeSha256(first);
                    string decoded = StrictUtf8.GetString(first);
                    byte[] second = File.ReadAllBytes(fullPath);
                    if (string.Equals(firstHash, ComputeSha256(second), StringComparison.Ordinal))
                    {
                        text = decoded;
                        sha256 = firstHash;
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    reason = "AICommand 读取失败：" + exception.Message;
                    return false;
                }
            }
            reason = "AICommand 在读取期间发生变化；请等待写入完成后重试";
            return false;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes ?? System.Array.Empty<byte>());
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
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
