using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESCommandPaletteRegistry
    {
        public const int MaximumFavorites = 200;
        public const int MaximumRecent = 30;

        private static readonly Dictionary<string, ProviderRegistration> Providers =
            new Dictionary<string, ProviderRegistration>(StringComparer.Ordinal);
        private static readonly List<ProviderRegistration> ProviderOrder = new List<ProviderRegistration>();
        private static readonly Dictionary<string, ESCommandPaletteItem> Items =
            new Dictionary<string, ESCommandPaletteItem>(StringComparer.Ordinal);
        private static readonly List<ESCommandPaletteItem> OrderedItems = new List<ESCommandPaletteItem>();
        private static readonly List<ESCommandPaletteRegistrationDiagnostic> Diagnostics =
            new List<ESCommandPaletteRegistrationDiagnostic>();
        private static readonly List<string> FavoriteIds = new List<string>();
        private static readonly HashSet<string> FavoriteSet = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> RecentIds = new List<string>();
        private static readonly Dictionary<string, int> RecentRanks = new Dictionary<string, int>(StringComparer.Ordinal);

        private static bool initialized;
        private static bool initializing;

        public static IReadOnlyList<ESCommandPaletteItem> AllItems
        {
            get
            {
                EnsureInitialized();
                return OrderedItems;
            }
        }

        public static IReadOnlyList<ESCommandPaletteRegistrationDiagnostic> RegistrationDiagnostics
        {
            get
            {
                EnsureInitialized();
                return Diagnostics;
            }
        }

        public static int ProviderCount
        {
            get
            {
                EnsureInitialized();
                return ProviderOrder.Count;
            }
        }

        public static int ItemCount
        {
            get
            {
                EnsureInitialized();
                return OrderedItems.Count;
            }
        }

        public static IReadOnlyList<string> Favorites
        {
            get
            {
                EnsureInitialized();
                return FavoriteIds;
            }
        }

        public static IReadOnlyList<string> Recent
        {
            get
            {
                EnsureInitialized();
                return RecentIds;
            }
        }

        public static void EnsureInitialized()
        {
            if (initialized || initializing)
            {
                return;
            }

            initializing = true;
            try
            {
                RegisterProviderCore(new WindowProvider());
                RegisterProviderCore(new AICommandProvider());
                RegisterProviderCore(new SceneProvider());
                RegisterProviderCore(new GlobalDataProvider());
                initialized = true;
                LoadAndCleanState();
            }
            finally
            {
                initializing = false;
            }
        }

        public static ESCommandPaletteRegistrationResult RegisterProvider(IESCommandPaletteProvider provider)
        {
            EnsureInitialized();
            return RegisterProviderCore(provider);
        }

        public static void Refresh()
        {
            EnsureInitialized();
            Items.Clear();
            OrderedItems.Clear();
            Diagnostics.Clear();

            for (int i = 0; i < ProviderOrder.Count; i++)
            {
                RebuildProviderItems(ProviderOrder[i], null);
            }

            LoadAndCleanState();
        }

        public static bool TryGet(string stableId, out ESCommandPaletteItem item)
        {
            EnsureInitialized();
            return Items.TryGetValue(stableId ?? string.Empty, out item);
        }

        public static bool IsFavorite(string stableId)
        {
            EnsureInitialized();
            return !string.IsNullOrEmpty(stableId) && FavoriteSet.Contains(stableId);
        }

        public static bool TryGetRecentRank(string stableId, out int rank)
        {
            EnsureInitialized();
            return RecentRanks.TryGetValue(stableId ?? string.Empty, out rank);
        }

        public static void ToggleFavorite(string stableId)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(stableId) || !Items.ContainsKey(stableId))
            {
                return;
            }

            if (FavoriteSet.Remove(stableId))
            {
                FavoriteIds.Remove(stableId);
            }
            else
            {
                FavoriteSet.Add(stableId);
                FavoriteIds.Remove(stableId);
                FavoriteIds.Insert(0, stableId);
                TrimList(FavoriteIds, MaximumFavorites);
                RebuildFavoriteSet();
            }

            SaveIds("favorites", FavoriteIds, false);
        }

        public static void RecordRecent(string stableId)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(stableId) || !Items.ContainsKey(stableId))
            {
                return;
            }

            RecentIds.Remove(stableId);
            RecentIds.Insert(0, stableId);
            TrimList(RecentIds, MaximumRecent);
            RebuildRecentRanks();
            SaveIds("recent", RecentIds, true);
        }

        private static ESCommandPaletteRegistrationResult RegisterProviderCore(IESCommandPaletteProvider provider)
        {
            var result = new ESCommandPaletteRegistrationResult();
            if (provider == null)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.NullProvider, string.Empty, string.Empty, "Provider 为空");
                return result;
            }

            string providerId = provider.ProviderId?.Trim();
            string prefix = provider.Prefix?.Trim();
            if (string.IsNullOrEmpty(providerId))
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.EmptyProviderId, string.Empty, string.Empty, "ProviderId 为空");
                return result;
            }

            if (Providers.ContainsKey(providerId))
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.DuplicateProviderId, providerId, string.Empty, "ProviderId 已注册，拒绝后注册项");
                return result;
            }

            if (string.IsNullOrEmpty(prefix))
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.EmptyProviderPrefix, providerId, string.Empty, "Provider Prefix 为空");
                return result;
            }

            var registration = new ProviderRegistration(providerId, provider.DisplayName, prefix, provider);
            if (!RebuildProviderItems(registration, result))
            {
                return result;
            }

            Providers.Add(providerId, registration);
            ProviderOrder.Add(registration);
            result.ProviderAccepted = true;
            AddDiagnostic(result, ESCommandPaletteRegistrationCode.Accepted, providerId, string.Empty,
                "Provider 已注册，接受 " + result.AcceptedItemCount + " 个命令项");
            return result;
        }

        private static bool RebuildProviderItems(ProviderRegistration registration, ESCommandPaletteRegistrationResult result)
        {
            IReadOnlyList<ESCommandPaletteItem> candidates;
            try
            {
                candidates = registration.Provider.BuildItems() ?? Array.Empty<ESCommandPaletteItem>();
            }
            catch (Exception exception)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed, registration.ProviderId, string.Empty,
                    "Provider 构建失败：" + exception.Message);
                return false;
            }

            var acceptedIds = new HashSet<string>(StringComparer.Ordinal);
            var stagedItems = new List<ESCommandPaletteItem>(candidates.Count);
            try
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ESCommandPaletteItem item = candidates[i];
                    if (!TryValidateItem(registration, item, out ESCommandPaletteRegistrationCode code, out string reason))
                    {
                        AddDiagnostic(result, code, registration.ProviderId, item?.ItemId, reason);
                        continue;
                    }

                    string stableId = string.Concat(registration.ProviderId, ":", item.ItemId);
                    if (!acceptedIds.Add(item.ItemId) || Items.ContainsKey(stableId))
                    {
                        AddDiagnostic(result, ESCommandPaletteRegistrationCode.DuplicateItemId, registration.ProviderId, item.ItemId,
                            "providerId + itemId 重复，拒绝后注册项");
                        continue;
                    }

                    item.ProviderId = registration.ProviderId;
                    stagedItems.Add(item);
                }
            }
            catch (Exception exception)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed, registration.ProviderId, string.Empty,
                    "Provider 命令项校验失败，未提交任何项：" + exception.Message);
                return false;
            }

            for (int i = 0; i < stagedItems.Count; i++)
            {
                ESCommandPaletteItem item = stagedItems[i];
                Items.Add(item.StableId, item);
                OrderedItems.Add(item);
            }

            if (result != null)
            {
                result.AcceptedItemCount = stagedItems.Count;
            }

            return true;
        }

        private static bool TryValidateItem(
            ProviderRegistration provider,
            ESCommandPaletteItem item,
            out ESCommandPaletteRegistrationCode code,
            out string reason)
        {
            if (item == null)
            {
                code = ESCommandPaletteRegistrationCode.NullItem;
                reason = "命令项为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.ItemId))
            {
                code = ESCommandPaletteRegistrationCode.EmptyItemId;
                reason = "itemId 为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Title))
            {
                code = ESCommandPaletteRegistrationCode.EmptyTitle;
                reason = "title 为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.Category))
            {
                code = ESCommandPaletteRegistrationCode.EmptyCategory;
                reason = "category 为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(item.TargetId))
            {
                code = ESCommandPaletteRegistrationCode.EmptyTargetId;
                reason = "targetId 为空";
                return false;
            }

            if (!string.Equals(item.Prefix, provider.Prefix, StringComparison.Ordinal))
            {
                code = ESCommandPaletteRegistrationCode.PrefixMismatch;
                reason = "命令项 Prefix 与 Provider Prefix 不一致";
                return false;
            }

            if (item.IsMutating)
            {
                code = ESCommandPaletteRegistrationCode.MutatingItemRejected;
                reason = "v1 不索引 isMutating=true 的命令项";
                return false;
            }

            if (item.RequiresConfirmation)
            {
                code = ESCommandPaletteRegistrationCode.ConfirmationItemRejected;
                reason = "v1 不索引需要确认或可能产生副作用的命令项";
                return false;
            }

            if (!MatchesCategoryAndAction(item))
            {
                code = ESCommandPaletteRegistrationCode.CategoryMismatch;
                reason = "Prefix、Category 与 actionKind 不匹配";
                return false;
            }

            switch (item.ActionKind)
            {
                case ESCommandPaletteActionKind.OpenMenu:
                    if (!ESCommandPaletteMenuRegistry.IsWhitelisted(item.TargetId))
                    {
                        code = ESCommandPaletteRegistrationCode.MenuNotWhitelisted;
                        reason = "OpenMenu 目标不在显式只读白名单";
                        return false;
                    }
                    break;

                case ESCommandPaletteActionKind.OpenWindow:
                    if (!ESWindowRegistry.TryResolve(item.TargetId, out _))
                    {
                        code = ESCommandPaletteRegistrationCode.WindowNotRegistered;
                        reason = "OpenWindow 的 windowId 未注册";
                        return false;
                    }
                    break;

                case ESCommandPaletteActionKind.OpenFile:
                case ESCommandPaletteActionKind.OpenAsset:
                case ESCommandPaletteActionKind.CopyText:
                case ESCommandPaletteActionKind.CopyPath:
                    if (item.ActionKind == ESCommandPaletteActionKind.OpenAsset)
                    {
                        if (!ESCommandPalettePathPolicy.IsRegisteredGlobalData(item.TargetId))
                        {
                            code = ESCommandPaletteRegistrationCode.FileNotAllowed;
                            reason = "GlobalData 资产不在受管根或不存在";
                            return false;
                        }
                    }
                    else
                    {
                        if (!ESCommandPalettePathPolicy.TryValidateAICommandFile(item.TargetId, out _, out string fileReason))
                        {
                            code = ESCommandPaletteRegistrationCode.FileNotAllowed;
                            reason = fileReason;
                            return false;
                        }
                    }
                    break;

                case ESCommandPaletteActionKind.Select:
                    if (!ESCommandPalettePathPolicy.IsRegisteredScene(item.TargetId)
                        && !ESCommandPalettePathPolicy.IsRegisteredGlobalData(item.TargetId)
                        && !ESCommandPalettePathPolicy.TryValidateAICommandFile(item.TargetId, out _, out _))
                    {
                        code = ESCommandPaletteRegistrationCode.SceneNotRegistered;
                        reason = "场景、GlobalData 或 AICommand 不在受管根";
                        return false;
                    }
                    break;

                default:
                    code = ESCommandPaletteRegistrationCode.UnsupportedAction;
                    reason = "actionKind 不受 v1 支持";
                    return false;
            }

            code = ESCommandPaletteRegistrationCode.Accepted;
            reason = string.Empty;
            return true;
        }

        private static bool MatchesCategoryAndAction(ESCommandPaletteItem item)
        {
            if (item.Prefix == "@")
            {
                return item.ActionKind == ESCommandPaletteActionKind.OpenWindow
                    || item.ActionKind == ESCommandPaletteActionKind.OpenMenu;
            }

            if (item.Prefix == "$")
            {
                return string.Equals(item.Category, "AICommand", StringComparison.Ordinal)
                    && (item.ActionKind == ESCommandPaletteActionKind.OpenFile
                        || item.ActionKind == ESCommandPaletteActionKind.CopyText
                        || item.ActionKind == ESCommandPaletteActionKind.CopyPath);
            }

            if (item.Prefix == "#")
            {
                return string.Equals(item.Category, "场景", StringComparison.Ordinal)
                    && item.ActionKind == ESCommandPaletteActionKind.Select;
            }

            if (item.Prefix == "G" || item.Prefix == "g")
            {
                return string.Equals(item.Category, "GlobalData", StringComparison.Ordinal)
                    && (item.ActionKind == ESCommandPaletteActionKind.OpenAsset
                        || item.ActionKind == ESCommandPaletteActionKind.Select);
            }

            return false;
        }

        private static void AddDiagnostic(
            ESCommandPaletteRegistrationResult result,
            ESCommandPaletteRegistrationCode code,
            string providerId,
            string itemId,
            string message)
        {
            var diagnostic = new ESCommandPaletteRegistrationDiagnostic(code, providerId, itemId, message);
            Diagnostics.Add(diagnostic);
            result?.Add(diagnostic);
        }

        private static void LoadAndCleanState()
        {
            LoadIds("favorites", false, MaximumFavorites, FavoriteIds);
            RebuildFavoriteSet();
            LoadIds("recent", true, MaximumRecent, RecentIds);
            RebuildRecentRanks();
        }

        private static void LoadIds(string suffix, bool session, int maximum, List<string> destination)
        {
            destination.Clear();
            string raw = session
                ? SessionState.GetString(PreferenceKey(suffix), string.Empty)
                : EditorPrefs.GetString(PreferenceKey(suffix), string.Empty);
            string[] values = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i].Trim();
                if (destination.Count >= maximum || !Items.ContainsKey(value) || !seen.Add(value))
                {
                    changed = true;
                    continue;
                }

                destination.Add(value);
            }

            if (changed || destination.Count != values.Length)
            {
                SaveIds(suffix, destination, session);
            }
        }

        private static void SaveIds(string suffix, List<string> ids, bool session)
        {
            string value = string.Join("\n", ids);
            if (session)
            {
                SessionState.SetString(PreferenceKey(suffix), value);
            }
            else
            {
                EditorPrefs.SetString(PreferenceKey(suffix), value);
            }
        }

        private static void RebuildFavoriteSet()
        {
            FavoriteSet.Clear();
            for (int i = 0; i < FavoriteIds.Count; i++)
            {
                FavoriteSet.Add(FavoriteIds[i]);
            }
        }

        private static void RebuildRecentRanks()
        {
            RecentRanks.Clear();
            for (int i = 0; i < RecentIds.Count; i++)
            {
                RecentRanks.Add(RecentIds[i], i);
            }
        }

        private static void TrimList(List<string> ids, int maximum)
        {
            if (ids.Count > maximum)
            {
                ids.RemoveRange(maximum, ids.Count - maximum);
            }
        }

        private static string PreferenceKey(string suffix)
        {
            unchecked
            {
                uint hash = 2166136261;
                string value = Application.dataPath ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }

                return "ES.CommandPalette." + hash.ToString("X8") + "." + suffix;
            }
        }

        internal static void ResetForTests(bool includeBuiltIns)
        {
            Providers.Clear();
            ProviderOrder.Clear();
            Items.Clear();
            OrderedItems.Clear();
            Diagnostics.Clear();
            FavoriteIds.Clear();
            FavoriteSet.Clear();
            RecentIds.Clear();
            RecentRanks.Clear();
            initialized = true;
            initializing = false;
            if (includeBuiltIns)
            {
                RegisterProviderCore(new WindowProvider());
                RegisterProviderCore(new AICommandProvider());
                RegisterProviderCore(new SceneProvider());
                RegisterProviderCore(new GlobalDataProvider());
                LoadAndCleanState();
            }
        }

        internal static void SetStoredIdsForTests(IReadOnlyList<string> favorites, IReadOnlyList<string> recent)
        {
            EditorPrefs.SetString(PreferenceKey("favorites"), JoinIds(favorites));
            SessionState.SetString(PreferenceKey("recent"), JoinIds(recent));
            LoadAndCleanState();
        }

        private static string JoinIds(IReadOnlyList<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return string.Empty;
            }

            var copy = new string[ids.Count];
            for (int i = 0; i < ids.Count; i++)
            {
                copy[i] = ids[i];
            }
            return string.Join("\n", copy);
        }

        private sealed class ProviderRegistration
        {
            public ProviderRegistration(string providerId, string displayName, string prefix, IESCommandPaletteProvider provider)
            {
                ProviderId = providerId;
                DisplayName = displayName ?? providerId;
                Prefix = prefix;
                Provider = provider;
            }

            public string ProviderId { get; }
            public string DisplayName { get; }
            public string Prefix { get; }
            public IESCommandPaletteProvider Provider { get; }
        }

        private sealed class WindowProvider : IESCommandPaletteProvider
        {
            public string ProviderId => "es.windows";
            public string DisplayName => "ES 窗口";
            public string Prefix => "@";

            public IReadOnlyList<ESCommandPaletteItem> BuildItems()
            {
                IReadOnlyList<ESWindowDescriptor> descriptors = ESWindowRegistry.All;
                var result = new List<ESCommandPaletteItem>(descriptors.Count);
                for (int i = 0; i < descriptors.Count; i++)
                {
                    ESWindowDescriptor descriptor = descriptors[i];
                    result.Add(new ESCommandPaletteItem(
                        descriptor.WindowId,
                        descriptor.Title,
                        "打开显式注册的 ES 窗口",
                        descriptor.Category,
                        descriptor.Keywords,
                        Prefix,
                        descriptor.WindowId,
                        ESCommandPaletteActionKind.OpenWindow));
                }

                return result;
            }
        }

        private sealed class AICommandProvider : IESCommandPaletteProvider
        {
            private const int MaximumIndexedFiles = 200;

            public string ProviderId => "es.aicommands";
            public string DisplayName => "AI 命令";
            public string Prefix => "$";

            public IReadOnlyList<ESCommandPaletteItem> BuildItems()
            {
                var result = new List<ESCommandPaletteItem>();
                string root = Path.Combine(
                    ESCommandPalettePathPolicy.ProjectRoot,
                    ESCommandPalettePathPolicy.AICommandRoot.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(root))
                {
                    return result;
                }

                string[] files = Directory.GetFiles(root, "*.md", SearchOption.TopDirectoryOnly);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                int count = Math.Min(files.Length, MaximumIndexedFiles);
                for (int i = 0; i < count; i++)
                {
                    string relativePath = ToProjectRelativePath(files[i]);
                    string title = Path.GetFileNameWithoutExtension(files[i]);
                    result.Add(CreateFileItem(relativePath, title, "open", "打开 AICommand 文件", ESCommandPaletteActionKind.OpenFile));
                    result.Add(CreateFileItem(relativePath, title + "（复制文本）", "copy-text", "复制 AICommand 文本", ESCommandPaletteActionKind.CopyText));
                    result.Add(CreateFileItem(relativePath, title + "（复制路径）", "copy-path", "复制 AICommand 项目路径", ESCommandPaletteActionKind.CopyPath));
                }

                return result;
            }

            private static ESCommandPaletteItem CreateFileItem(
                string relativePath,
                string title,
                string operation,
                string description,
                ESCommandPaletteActionKind actionKind)
            {
                return new ESCommandPaletteItem(
                    relativePath + ":" + operation,
                    title,
                    description,
                    "AICommand",
                    relativePath,
                    "$",
                    relativePath,
                    actionKind);
            }

            private static string ToProjectRelativePath(string fullPath)
            {
                string projectRoot = ESCommandPalettePathPolicy.ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return fullPath.Substring(projectRoot.Length + 1).Replace('\\', '/');
            }
        }

        private sealed class SceneProvider : IESCommandPaletteProvider
        {
            public string ProviderId => "es.scenes";
            public string DisplayName => "构建场景";
            public string Prefix => "#";

            public IReadOnlyList<ESCommandPaletteItem> BuildItems()
            {
                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                var result = new List<ESCommandPaletteItem>(scenes.Length);
                for (int i = 0; i < scenes.Length; i++)
                {
                    EditorBuildSettingsScene scene = scenes[i];
                    if (scene == null || !scene.enabled || !ESCommandPalettePathPolicy.IsRegisteredScene(scene.path))
                    {
                        continue;
                    }

                    result.Add(new ESCommandPaletteItem(
                        scene.path,
                        Path.GetFileNameWithoutExtension(scene.path),
                        "在 Project 中定位场景资产",
                        "场景",
                        scene.path,
                        Prefix,
                        scene.path,
                        ESCommandPaletteActionKind.Select));
                }

                return result;
            }
        }

        private sealed class GlobalDataProvider : IESCommandPaletteProvider
        {
            private const int MaximumIndexedAssets = 200;
            private static readonly Dictionary<string, string> ChineseTitles =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "ESCmdAgent", "命令代理" },
                    { "ESGlobalEditorDefaultConfi", "全局编辑器流程配置" },
                    { "ESGlobalEditorLocation", "全局资产定位" },
                    { "ESGlobalEditorTheme", "编辑器主题" },
                    { "ESGlobalProjectAssetGuideData", "项目资产引导" },
                    { "ESGlobalResSetting", "全局资源管理设置" },
                    { "ESGlobalResToolsSupportConfig", "资产工具支持配置" },
                    { "ESSceneGlobalData", "场景管理器数据" },
                    { "ESTagBakeTable", "GameTag 烘焙表" },
                    { "ESTagCatalogGameCore", "GameTag 目录" },
                    { "GameCoreEditorGlobalData", "GameCore 编辑器全局数据" },
                    { "StateMachineConfig", "状态机配置" },
                    { "TrackSequenceEditorSettings", "轨道序列编辑器设置" },
                };

            public string ProviderId => "es.globaldata";
            public string DisplayName => "全局配置";
            public string Prefix => "G";

            public IReadOnlyList<ESCommandPaletteItem> BuildItems()
            {
                var result = new List<ESCommandPaletteItem>();
                var typeSet = new HashSet<Type>();
                foreach (Type type in ESEditorSO.AllGlobalSoNames.Values)
                {
                    if (type != null)
                    {
                        typeSet.Add(type);
                    }
                }

                // AllGlobalSoNames is populated by AssemblyStream and may be only partially
                // available when the palette first opens. TypeCache is an in-memory type
                // catalog, so always merge it instead of using it only when the map is empty.
                foreach (Type derived in TypeCache.GetTypesDerivedFrom<IESGlobalData>())
                {
                    if (derived != null)
                    {
                        typeSet.Add(derived);
                    }
                }

                var types = new List<Type>(typeSet);
                types.Sort((left, right) => string.Compare(
                    left == null ? string.Empty : left.Name,
                    right == null ? string.Empty : right.Name,
                    StringComparison.Ordinal));
                var addedPaths = new HashSet<string>(StringComparer.Ordinal);
                int indexedAssets = 0;
                for (int i = 0; i < types.Count; i++)
                {
                    Type type = types[i];
                    if (type == null
                        || type.IsAbstract
                        || !typeof(ESSO).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    List<ESSO> instances;
                    try
                    {
                        ESEditorSO.EnsureTypeLoaded(type);
                        instances = ESEditorSO.GetGroup<ESSO>(type);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "[ESCommandPalette] GlobalData 类型加载失败：" + type.Name
                            + " | " + exception.Message);
                        continue;
                    }

                    if (instances == null || instances.Count == 0)
                    {
                        continue;
                    }

                    for (int j = 0; j < instances.Count; j++)
                    {
                        ESSO instance = instances[j];
                        if (instance == null)
                        {
                            continue;
                        }

                        string path = AssetDatabase.GetAssetPath(instance);
                        if (!ESCommandPalettePathPolicy.IsRegisteredGlobalData(path)
                            || !addedPaths.Add(path))
                        {
                            continue;
                        }

                        string rawTitle = Path.GetFileNameWithoutExtension(path);
                        string title = ChineseTitles.TryGetValue(rawTitle, out string chineseTitle)
                            ? chineseTitle
                            : rawTitle;
                        string folderName = Path.GetFileName(Path.GetDirectoryName(path));
                        string keywords = rawTitle + " " + title + " " + folderName + " " + path;
                        result.Add(new ESCommandPaletteItem(
                            path,
                            title,
                            "打开全局配置资产；无专用打开器时定位到 Inspector",
                            "GlobalData",
                            keywords,
                            Prefix,
                            path,
                            ESCommandPaletteActionKind.OpenAsset));

                        if (++indexedAssets >= MaximumIndexedAssets)
                        {
                            return result;
                        }
                    }
                }

                return result;
            }
        }
    }
}
