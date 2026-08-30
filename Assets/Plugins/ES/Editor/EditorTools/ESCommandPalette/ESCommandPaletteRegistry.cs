using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESCommandPaletteRegistry
    {
        public const int MaximumFavorites = 200;
        public const int MaximumRecent = 30;
        public const int MaximumProviders = 64;
        public const int MaximumProviderItems = 4096;
        public const int MaximumTotalItems = 100000;
        private const int MaximumPersistedIdsCharacters = 65536;
        private const int MaximumProviderIdCharacters = 128;
        private const int MaximumItemIdCharacters = 512;
        private const int MaximumTitleCharacters = 1024;
        private const int MaximumCategoryCharacters = 256;
        private const int MaximumTargetIdCharacters = 4096;

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
        private static readonly ReadOnlyCollection<ESCommandPaletteItem> OrderedItemsView = OrderedItems.AsReadOnly();
        private static readonly ReadOnlyCollection<ESCommandPaletteRegistrationDiagnostic> DiagnosticsView = Diagnostics.AsReadOnly();
        private static readonly ReadOnlyCollection<string> FavoriteIdsView = FavoriteIds.AsReadOnly();
        private static readonly ReadOnlyCollection<string> RecentIdsView = RecentIds.AsReadOnly();

        private static bool initialized;
        private static bool initializing;
        private static bool refreshing;
        private static IReadOnlyDictionary<string, ESCommandPaletteItem> refreshObservationItems;
        private static IReadOnlyList<ESCommandPaletteItem> refreshObservationOrdered;
        private static IReadOnlyList<ESCommandPaletteRegistrationDiagnostic> refreshObservationDiagnostics;
        private static IReadOnlyList<string> refreshObservationFavorites;
        private static IReadOnlyList<string> refreshObservationRecent;
        private static int refreshObservationProviderCount = -1;

        public static IReadOnlyList<ESCommandPaletteItem> AllItems
        {
            get
            {
                EnsureInitialized();
                return refreshing && refreshObservationOrdered != null
                    ? refreshObservationOrdered
                    : OrderedItemsView;
            }
        }

        public static IReadOnlyList<ESCommandPaletteRegistrationDiagnostic> RegistrationDiagnostics
        {
            get
            {
                EnsureInitialized();
                return refreshing && refreshObservationDiagnostics != null
                    ? refreshObservationDiagnostics
                    : DiagnosticsView;
            }
        }

        public static int ProviderCount
        {
            get
            {
                EnsureInitialized();
                return refreshing && refreshObservationProviderCount >= 0
                    ? refreshObservationProviderCount
                    : ProviderOrder.Count;
            }
        }

        public static int ItemCount
        {
            get
            {
                EnsureInitialized();
                return refreshing && refreshObservationOrdered != null
                    ? refreshObservationOrdered.Count
                    : OrderedItems.Count;
            }
        }

        public static IReadOnlyList<string> Favorites
        {
            get
            {
                EnsureInitialized();
                return refreshing && refreshObservationFavorites != null
                    ? refreshObservationFavorites
                    : FavoriteIdsView;
            }
        }

        public static IReadOnlyList<string> Recent
        {
            get
            {
                EnsureInitialized();
                return refreshing && refreshObservationRecent != null
                    ? refreshObservationRecent
                    : RecentIdsView;
            }
        }

        public static void EnsureInitialized()
        {
            if (initialized || initializing)
            {
                return;
            }

            initializing = true;
            Dictionary<string, ProviderRegistration> previousProviders = null;
            List<ProviderRegistration> previousProviderOrder = null;
            Dictionary<string, ESCommandPaletteItem> previousItems = null;
            List<ESCommandPaletteItem> previousOrderedItems = null;
            List<ESCommandPaletteRegistrationDiagnostic> previousDiagnostics = null;
            try
            {
                previousProviders = new Dictionary<string, ProviderRegistration>(Providers, StringComparer.Ordinal);
                previousProviderOrder = new List<ProviderRegistration>(ProviderOrder);
                previousItems = new Dictionary<string, ESCommandPaletteItem>(Items, StringComparer.Ordinal);
                previousOrderedItems = new List<ESCommandPaletteItem>(OrderedItems);
                previousDiagnostics = new List<ESCommandPaletteRegistrationDiagnostic>(Diagnostics);
                RegisterProviderCore(new WindowProvider());
                RegisterProviderCore(new AICommandProvider());
                RegisterProviderCore(new SceneProvider());
                RegisterProviderCore(new GlobalDataProvider());
                initialized = true;
                LoadAndCleanState();
            }
            catch (Exception exception)
            {
                if (previousProviders != null)
                {
                    Providers.Clear();
                    foreach (KeyValuePair<string, ProviderRegistration> pair in previousProviders)
                        Providers.Add(pair.Key, pair.Value);
                }
                if (previousProviderOrder != null)
                {
                    ProviderOrder.Clear();
                    ProviderOrder.AddRange(previousProviderOrder);
                }
                if (previousItems != null)
                {
                    Items.Clear();
                    foreach (KeyValuePair<string, ESCommandPaletteItem> pair in previousItems)
                        Items.Add(pair.Key, pair.Value);
                }
                if (previousOrderedItems != null)
                {
                    OrderedItems.Clear();
                    OrderedItems.AddRange(previousOrderedItems);
                }
                if (previousDiagnostics != null)
                {
                    Diagnostics.Clear();
                    Diagnostics.AddRange(previousDiagnostics);
                }
                initialized = false;
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 命令索引初始化失败，已回滚部分注册状态。", exception));
            }
            finally
            {
                initializing = false;
            }
        }

        public static ESCommandPaletteRegistrationResult RegisterProvider(IESCommandPaletteProvider provider)
        {
            EnsureInitialized();
            if (initializing || refreshing)
            {
                var rejected = new ESCommandPaletteRegistrationResult();
                string providerId = string.Empty;
                try
                {
                    providerId = provider?.ProviderId ?? string.Empty;
                }
                catch (Exception exception)
                {
                    Debug.LogException(new InvalidOperationException(
                        "[ESCommandPalette] 重入拒绝诊断读取 ProviderId 失败。", exception));
                }
                AddDiagnostic(rejected, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    providerId, string.Empty,
                    "命令索引正在初始化或刷新，已拒绝重入注册 Provider");
                return rejected;
            }
            try
            {
                return RegisterProviderCore(provider);
            }
            catch (Exception exception)
            {
                var failed = new ESCommandPaletteRegistrationResult();
                AddDiagnostic(failed, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    string.Empty, string.Empty,
                    "Provider 注册边界异常，已安全拒绝：" + exception.Message);
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] Provider 注册边界异常，已安全拒绝。", exception));
                return failed;
            }
        }

        public static void Refresh()
        {
            EnsureInitialized();
            if (initializing)
            {
                Debug.LogWarning("[ESCommandPalette] 命令索引正在初始化，已拒绝 Refresh。", null);
                return;
            }
            if (refreshing)
            {
                Debug.LogWarning("[ESCommandPalette] 命令索引正在刷新，已拒绝嵌套 Refresh。", null);
                return;
            }

            refreshing = true;
            try
            {
            var previousItems = new Dictionary<string, ESCommandPaletteItem>(Items, StringComparer.Ordinal);
            var previousOrderedItems = new List<ESCommandPaletteItem>(OrderedItems);
            var previousDiagnostics = new List<ESCommandPaletteRegistrationDiagnostic>(Diagnostics);
            refreshObservationItems = previousItems;
            refreshObservationOrdered = previousOrderedItems.AsReadOnly();
            refreshObservationDiagnostics = previousDiagnostics.AsReadOnly();
            refreshObservationFavorites = new List<string>(FavoriteIds).AsReadOnly();
            refreshObservationRecent = new List<string>(RecentIds).AsReadOnly();
            refreshObservationProviderCount = ProviderOrder.Count;
            Items.Clear();
            OrderedItems.Clear();
            Diagnostics.Clear();

            bool refreshSucceeded = true;
            try
            {
                for (int i = 0; i < ProviderOrder.Count; i++)
                {
                    if (!RebuildProviderItems(ProviderOrder[i], null))
                        refreshSucceeded = false;
                }
            }
            catch (Exception exception)
            {
                refreshSucceeded = false;
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] 命令索引刷新失败，保留旧索引。", exception));
            }

            if (!refreshSucceeded)
            {
                Items.Clear();
                foreach (KeyValuePair<string, ESCommandPaletteItem> pair in previousItems)
                    Items.Add(pair.Key, pair.Value);
                OrderedItems.Clear();
                OrderedItems.AddRange(previousOrderedItems);
                Diagnostics.Clear();
                Diagnostics.AddRange(previousDiagnostics);
                AddDiagnostic(null, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    string.Empty, string.Empty, "命令索引刷新失败，已保留上一次有效索引。");
                return;
            }

            LoadAndCleanState();
            }
            finally
            {
                refreshObservationItems = null;
                refreshObservationOrdered = null;
                refreshObservationDiagnostics = null;
                refreshObservationFavorites = null;
                refreshObservationRecent = null;
                refreshObservationProviderCount = -1;
                refreshing = false;
            }
        }

        public static bool TryGet(string stableId, out ESCommandPaletteItem item)
        {
            EnsureInitialized();
            if (refreshing && refreshObservationItems != null)
                return refreshObservationItems.TryGetValue(stableId ?? string.Empty, out item);
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
            if (refreshing)
            {
                Debug.LogWarning("[ESCommandPalette] 命令索引刷新期间拒绝修改收藏状态。", null);
                return;
            }
            if (string.IsNullOrEmpty(stableId) || !ContainsObservableItem(stableId))
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
            if (refreshing)
            {
                Debug.LogWarning("[ESCommandPalette] 命令索引刷新期间拒绝写入最近使用状态。", null);
                return;
            }
            if (string.IsNullOrEmpty(stableId) || !ContainsObservableItem(stableId))
            {
                return;
            }

            RecentIds.Remove(stableId);
            RecentIds.Insert(0, stableId);
            TrimList(RecentIds, MaximumRecent);
            RebuildRecentRanks();
            SaveIds("recent", RecentIds, true);
        }

        private static bool ContainsObservableItem(string stableId)
        {
            if (refreshing && refreshObservationItems != null)
                return refreshObservationItems.ContainsKey(stableId ?? string.Empty);
            return Items.ContainsKey(stableId ?? string.Empty);
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

            if (providerId.Length > MaximumProviderIdCharacters
                || prefix != null && prefix.Length > MaximumProviderIdCharacters)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    providerId, string.Empty, "ProviderId 或 Prefix 超过长度上限，已拒绝注册");
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

            if (ProviderOrder.Count >= MaximumProviders)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    providerId, string.Empty,
                    "Provider 数量超过上限 " + MaximumProviders + "，已拒绝注册");
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

            // Palette admission only: acceptedIds means a menu item was
            // discovered/registered; it never declares task completion or
            // grants execution authority.
            var acceptedIds = new HashSet<string>(StringComparer.Ordinal);
            int totalCandidateCount;
            try
            {
                totalCandidateCount = candidates.Count;
            }
            catch (Exception exception)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    registration.ProviderId, string.Empty,
                    "Provider 候选项数量读取失败：" + exception.Message);
                return false;
            }
            if (totalCandidateCount < 0)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    registration.ProviderId, string.Empty,
                    "Provider 候选项数量无效（不能为负数），已拒绝注册");
                return false;
            }
            int candidateCount = Mathf.Min(totalCandidateCount, MaximumProviderItems);
            var stagedItems = new List<ESCommandPaletteItem>(candidateCount);
            for (int i = 0; i < candidateCount; i++)
            {
                try
                {
                    if (Items.Count + stagedItems.Count >= MaximumTotalItems)
                    {
                        AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                            registration.ProviderId, string.Empty,
                            "命令项总数超过上限 " + MaximumTotalItems + "，已截断后续项");
                        break;
                    }
                    ESCommandPaletteItem item = candidates[i];
                    if (!TryValidateItem(registration, item, out ESCommandPaletteRegistrationCode code, out string reason))
                    {
                        AddDiagnostic(result, code, registration.ProviderId, item?.ItemId, reason);
                        continue;
                    }

                    string stableId = string.Concat(registration.ProviderId, ":", item.ItemId);
                    if (acceptedIds.Contains(item.ItemId) || Items.ContainsKey(stableId))
                    {
                        AddDiagnostic(result, ESCommandPaletteRegistrationCode.DuplicateItemId, registration.ProviderId, item.ItemId,
                            "providerId + itemId 重复，拒绝后注册项");
                        continue;
                    }

                    item.ProviderId = registration.ProviderId;
                    acceptedIds.Add(item.ItemId);
                    stagedItems.Add(item);
                }
                catch (Exception exception)
                {
                    AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                        registration.ProviderId, string.Empty,
                        "Provider 单项校验失败，已跳过该项：" + exception.Message);
                }
            }

            if (totalCandidateCount > MaximumProviderItems)
            {
                AddDiagnostic(result, ESCommandPaletteRegistrationCode.ProviderBuildFailed,
                    registration.ProviderId, string.Empty,
                    "Provider 候选项超过上限 " + MaximumProviderItems + "，已截断后续项");
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

            if (item.ItemId.Length > MaximumItemIdCharacters
                || item.Title.Length > MaximumTitleCharacters
                || item.Category.Length > MaximumCategoryCharacters
                || item.TargetId.Length > MaximumTargetIdCharacters)
            {
                code = ESCommandPaletteRegistrationCode.ProviderBuildFailed;
                reason = "命令项字段超过长度上限，已拒绝注册";
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
            var previousFavorites = new List<string>(FavoriteIds);
            var previousRecent = new List<string>(RecentIds);
            try
            {
                LoadIds("favorites", false, MaximumFavorites, FavoriteIds);
                RebuildFavoriteSet();
                LoadIds("recent", true, MaximumRecent, RecentIds);
                RebuildRecentRanks();
            }
            catch (Exception exception)
            {
                FavoriteIds.Clear();
                FavoriteIds.AddRange(previousFavorites);
                RebuildFavoriteSet();
                RecentIds.Clear();
                RecentIds.AddRange(previousRecent);
                RebuildRecentRanks();
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] Favorites/Recent 状态恢复失败，已保留上一版内存状态。", exception));
            }
        }

        private static void LoadIds(string suffix, bool session, int maximum, List<string> destination)
        {
            destination.Clear();
            string raw = session
                ? SessionState.GetString(PreferenceKey(suffix), string.Empty)
                : EditorPrefs.GetString(PreferenceKey(suffix), string.Empty);
            bool changed = raw.Length > MaximumPersistedIdsCharacters;
            if (changed)
                raw = raw.Substring(0, MaximumPersistedIdsCharacters);
            string[] values = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var seen = new HashSet<string>(StringComparer.Ordinal);
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
            try
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
            catch (Exception exception)
            {
                Debug.LogException(new InvalidOperationException(
                    "[ESCommandPalette] " + suffix + " 状态保存失败，已保留当前内存状态。", exception));
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
            refreshing = false;
            refreshObservationItems = null;
            refreshObservationOrdered = null;
            refreshObservationDiagnostics = null;
            refreshObservationFavorites = null;
            refreshObservationRecent = null;
            refreshObservationProviderCount = -1;
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
            public string ProviderId => "es.aicommands";
            public string DisplayName => "AI 命令";
            public string Prefix => "$";

            public IReadOnlyList<ESCommandPaletteItem> BuildItems()
            {
                var result = new List<ESCommandPaletteItem>();
                if (!ESCommandPalettePathPolicy.TryReadAICommandCatalog(
                        out List<ESAICommandCatalogEntry> entries, out _, out _))
                {
                    return result;
                }

                for (int index = 0; index < entries.Count; index++)
                {
                    ESAICommandCatalogEntry entry = entries[index];
                    result.Add(new ESCommandPaletteItem(
                        entry.id,
                        entry.title,
                        BuildDescription(entry),
                        "AICommand",
                        entry.keywords + " " + entry.role + " " + entry.riskLevel + " " + entry.writeMode
                            + " " + entry.path,
                        "$",
                        entry.path,
                        ESCommandPaletteActionKind.OpenFile));
                }

                return result;
            }

            private static string BuildDescription(ESAICommandCatalogEntry entry)
            {
                return entry.summary + " · " + entry.riskLevel + " · " + entry.writeMode;
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
                        string title = ResolveTitle(type, rawTitle);
                        string folderName = Path.GetFileName(Path.GetDirectoryName(path));
                        string keywords = type.Name + " " + rawTitle + " " + title + " " + folderName + " " + path;
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

            private static string ResolveTitle(Type type, string rawTitle)
            {
                ESCreatePathAttribute createPath = Attribute.GetCustomAttribute(
                    type,
                    typeof(ESCreatePathAttribute),
                    true) as ESCreatePathAttribute;
                if (createPath != null && !string.IsNullOrWhiteSpace(createPath.MyName))
                {
                    return createPath.MyName.Trim();
                }

                CreateAssetMenuAttribute createMenu = Attribute.GetCustomAttribute(
                    type,
                    typeof(CreateAssetMenuAttribute),
                    true) as CreateAssetMenuAttribute;
                string menuTitle = GetLastMenuSegment(createMenu?.menuName);
                if (ContainsChinese(menuTitle))
                {
                    return menuTitle;
                }

                if (ChineseTitles.TryGetValue(rawTitle, out string chineseTitle))
                {
                    return chineseTitle;
                }

                return !string.IsNullOrWhiteSpace(menuTitle) ? menuTitle : rawTitle;
            }

            private static string GetLastMenuSegment(string menuName)
            {
                if (string.IsNullOrWhiteSpace(menuName))
                {
                    return string.Empty;
                }

                string normalized = menuName.Trim().Trim('/');
                int separatorIndex = normalized.LastIndexOf('/');
                return separatorIndex >= 0
                    ? normalized.Substring(separatorIndex + 1).Trim()
                    : normalized;
            }

            private static bool ContainsChinese(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }

                for (int i = 0; i < value.Length; i++)
                {
                    char character = value[i];
                    if (character >= '\u3400' && character <= '\u9fff')
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
