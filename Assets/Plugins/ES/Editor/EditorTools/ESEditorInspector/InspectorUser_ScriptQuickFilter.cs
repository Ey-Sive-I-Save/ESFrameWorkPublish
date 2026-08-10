using System;
using System.Collections.Generic;
using System.Text;
using ES.EditorInternal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 在 Inspector 头部提供组件快速筛选，避免大量组件时只能从完整组件列表里逐个翻找。
    /// 隐藏会临时设置 HideInInspector，并在保存/Domain Reload/Play/Quit 前恢复，避免写入场景序列化。
    /// </summary>
    public sealed class InspectorUser_ScriptQuickFilter : ESEditorInspectorUser
    {
        private const string EnabledPrefKey = "ES.ScriptQuickFilter.Enabled";
        private const string SearchKeyPrefix = "ES.ScriptQuickFilter.Search.";
        private const string ShowAllKeyPrefix = "ES.ScriptQuickFilter.ShowAll.";
        private const string CategoryKeyPrefix = "ES.ScriptQuickFilter.Category.";
        private const string PanelVisibleKeyPrefix = "ES.ScriptQuickFilter.PanelVisible.";
        private const string HiddenStorageKeyPrefix = "ES.ScriptQuickFilter.Hidden.";
        private const string HiddenOriginalFlagsStorageKeyPrefix = "ES.ScriptQuickFilter.HiddenOriginalFlags.";
        private const int MinComponentSlotCount = 2;
        private const int VisibleRowLimit = 20;
        private const int MaxGameObjectCaches = 256;
        private const int CacheCleanupAccessInterval = 128;
        private const string NavigatorSessionKeyPrefix = "ES.EditorSectionNavigator.Window.";
        private const string MenuRoot = MenuItemPathDefine.DEVELOPMENT_MAINTENANCE_PATH + "检查器/组件筛选/";

        private static readonly Color DisabledLabelColor = new Color(0.62f, 0.62f, 0.62f, 0.9f);
        private static readonly Color MissingScriptColor = new Color(0.92f, 0.36f, 0.32f, 1f);
        private static readonly Color EsOriginColor = new Color(0.42f, 0.72f, 1f, 1f);
        private static readonly Color ProjectOriginColor = new Color(0.42f, 0.85f, 0.62f, 1f);
        private static readonly Color ThirdPartyOriginColor = new Color(0.95f, 0.72f, 0.38f, 1f);
        private static readonly Color UnknownOriginColor = new Color(0.72f, 0.74f, 0.78f, 1f);
        private static readonly Color UnityNativeOriginColor = new Color(0.58f, 0.85f, 0.72f, 1f);
        private static GUIStyle scriptLabelStyle;
        private static int scriptLabelStyleSkinGeneration = -1;
        private static readonly Dictionary<int, GameObjectScriptCache> GameObjectCaches =
            new Dictionary<int, GameObjectScriptCache>();
        private static bool gameObjectCacheEventsInstalled;
        private static long gameObjectCacheAccessCounter;
        private static int cacheAccessCount;
        private static HashSet<string> cachedHiddenIds;
        private static Dictionary<string, HideFlags> cachedHiddenOriginalFlags;
        private static bool hiddenIdsLoaded;

        public override int Order => 100;

        [MenuItem(MenuRoot + "开启 Inspector 组件筛选")]
        public static void EnableScriptQuickFilter()
        {
            EditorPrefs.SetBool(EnabledPrefKey, true);
            RebuildInspectorViews();
            Debug.Log("[组件筛选] Inspector 组件筛选已开启");
        }

        [MenuItem(MenuRoot + "开启 Inspector 组件筛选", true)]
        private static bool ValidateEnableScriptQuickFilter()
        {
            return !Application.isBatchMode && !EditorPrefs.GetBool(EnabledPrefKey, true);
        }

        [MenuItem(MenuRoot + "关闭 Inspector 组件筛选")]
        public static void DisableScriptQuickFilter()
        {
            EditorPrefs.SetBool(EnabledPrefKey, false);
            TeardownGameObjectCacheEvents();
            RebuildInspectorViews();
            Debug.Log("[组件筛选] Inspector 组件筛选已关闭");
        }

        [MenuItem(MenuRoot + "关闭 Inspector 组件筛选", true)]
        private static bool ValidateDisableScriptQuickFilter()
        {
            return !Application.isBatchMode && EditorPrefs.GetBool(EnabledPrefKey, true);
        }

        [MenuItem(MenuRoot + "恢复全部隐藏组件")]
        public static void RestoreAllHiddenScripts()
        {
            HashSet<string> hiddenIds = LoadHiddenScriptIds();
            if (hiddenIds.Count == 0)
            {
                Debug.Log("[组件筛选] 当前没有隐藏的组件。");
                return;
            }

            RestoreAllHiddenComponents();
            int restoredCount = hiddenIds.Count;
            SaveHiddenScriptIds(
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, HideFlags>(StringComparer.Ordinal));
            RebuildInspectorViews();
            Debug.Log("[组件筛选] 已恢复全部隐藏组件：" + restoredCount + " 条；默认 Inspector 可见性已恢复。");
        }

        private static void RestoreAllHiddenComponents()
        {
            HashSet<string> hiddenIds = LoadHiddenScriptIds();
            if (hiddenIds.Count == 0)
                return;

            Dictionary<string, HideFlags> hiddenOriginalFlags = LoadHiddenOriginalFlags();
            foreach (string hiddenId in hiddenIds)
            {
                Component component = ResolveHiddenComponent(hiddenId);
                if (component == null)
                    continue;

                if (hiddenOriginalFlags.TryGetValue(hiddenId, out HideFlags originalFlags))
                    component.hideFlags = originalFlags;
                else
                    component.hideFlags &= ~HideFlags.HideInInspector;
            }
        }

        [MenuItem(MenuRoot + "恢复全部隐藏组件", true)]
        private static bool ValidateRestoreAllHiddenScripts()
        {
            return !Application.isBatchMode;
        }

        [MenuItem(MenuRoot + "恢复默认启用状态")]
        public static void ResetToDefaultEnabled()
        {
            EditorPrefs.DeleteKey(EnabledPrefKey);
            RebuildInspectorViews();
            Debug.Log("[组件筛选] 已恢复默认启动状态：开启。");
        }

        [MenuItem(MenuRoot + "自检组件筛选")]
        public static void RunSelfTest()
        {
            var report = new StringBuilder(512);
            bool hasEnabledOverride = EditorPrefs.HasKey(EnabledPrefKey);
            bool currentEnabled = EditorPrefs.GetBool(EnabledPrefKey, true);
            report.AppendLine("[组件筛选自检] 默认启动：开启");
            report.AppendLine("HasEnabledOverride=" + hasEnabledOverride + " / Current=" + currentEnabled);
            report.AppendLine("RouterType=" + typeof(InspectorUser_ScriptQuickFilter).IsSubclassOf(typeof(ESEditorInspectorUser)));
            report.AppendLine("BatchMode=" + Application.isBatchMode);

            HashSet<string> hiddenIds = LoadHiddenScriptIds();
            int resolvedHiddenCount = 0;
            foreach (string hiddenId in hiddenIds)
            {
                if (ResolveHiddenComponent(hiddenId) != null)
                    resolvedHiddenCount++;
            }

            report.AppendLine("HiddenRecords=" + hiddenIds.Count + " / Resolved=" + resolvedHiddenCount);

            GameObject selected = Selection.activeGameObject;
            if (selected == null && Selection.activeObject is Component selectedComponent)
                selected = selectedComponent.gameObject;

            if (selected == null)
            {
                report.AppendLine("Selection=<none>");
                report.AppendLine("SelectionType=<none>");
                report.AppendLine("SelectionScene=<none>");
            }
            else
            {
                List<ScriptEntry> scripts = CollectScripts(selected, out int totalComponentSlots);
                int stableIdCount = 0;
                int mismatchedIdCount = 0;
                for (int i = 0; i < scripts.Count; i++)
                {
                    ScriptEntry entry = scripts[i];
                    if (!entry.CanHide)
                        continue;

                    stableIdCount++;
                    if (ResolveHiddenComponent(entry.GlobalObjectIdString) != entry.SourceComponent)
                        mismatchedIdCount++;
                }

                report.AppendLine("Selection=" + selected.name);
                report.AppendLine("SelectionType=" + selected.GetType().Name);
                UnityEngine.SceneManagement.Scene selectedScene = selected.scene;
                report.AppendLine("SelectionScene=" + (selectedScene.IsValid() ? selectedScene.path : "<asset/no-scene>"));
                report.AppendLine("TotalComponentSlots=" + totalComponentSlots);
                report.AppendLine("ScriptSlots=" + scripts.Count + " / MinSlots=" + MinComponentSlotCount
                    + " / PanelShown=" + (totalComponentSlots >= MinComponentSlotCount));
                if (totalComponentSlots < MinComponentSlotCount)
                    report.AppendLine("PanelHiddenReason=当前对象组件槽位不足 " + MinComponentSlotCount);
                report.AppendLine("StableIds=" + stableIdCount + " / Mismatch=" + mismatchedIdCount);
            }

            Debug.Log(report.ToString().TrimEnd());
        }

        [MenuItem(MenuRoot + "自检组件筛选", true)]
        private static bool ValidateRunSelfTest()
        {
            return !Application.isBatchMode;
        }

        public override bool Apply(ESEditorInspectorContext context)
        {
            if (Application.isBatchMode)
                return false;

            if (!EditorPrefs.GetBool(EnabledPrefKey, true))
            {
                TeardownGameObjectCacheEvents();
                return false;
            }

            if (context.Kind != ESEditorInspectorContextKind.GameObjectMainHeader
                && context.Kind != ESEditorInspectorContextKind.StandaloneComponentInspector)
            {
                return false;
            }

            if (context.Targets == null || context.Targets.Count != 1)
                return false;

            GameObject gameObject = context.Target as GameObject;
            if (gameObject == null && context.Target is Component component && component != null)
                gameObject = component.gameObject;

            if (gameObject == null)
                return false;

            List<ScriptEntry> scripts = CollectScripts(gameObject, out int totalComponentSlots);
            if (totalComponentSlots < MinComponentSlotCount)
                return false;

            HashSet<string> hiddenIds = LoadHiddenScriptIds();
            DrawPanel(gameObject, scripts, totalComponentSlots, hiddenIds);
            return false;
        }

        private static List<ScriptEntry> CollectScripts(GameObject gameObject)
        {
            return CollectScripts(gameObject, out _);
        }

        private static List<ScriptEntry> CollectScripts(
            GameObject gameObject,
            out int totalComponentSlots)
        {
            GameObjectScriptCache objectCache = GetOrCreateGameObjectCache(gameObject);
            gameObject.GetComponents<Component>(objectCache.Components);
            totalComponentSlots = objectCache.Components.Count;

            if (objectCache.Components.Count != objectCache.LastComponentCount)
            {
                objectCache.Entries.Clear();
                objectCache.LastComponentCount = objectCache.Components.Count;
            }

            objectCache.ActiveScriptComponentIds.Clear();
            var scripts = new List<ScriptEntry>(objectCache.Components.Count);
            for (int i = 0; i < objectCache.Components.Count; i++)
            {
                Component component = objectCache.Components[i];
                if (component == null)
                {
                    scripts.Add(new ScriptEntry(null));
                    continue;
                }

                int instanceId = component.GetInstanceID();
                objectCache.ActiveScriptComponentIds.Add(instanceId);
                if (objectCache.Entries.TryGetValue(instanceId, out ScriptEntry cachedEntry)
                    && cachedEntry != null
                    && cachedEntry.SourceComponent == component)
                {
                    scripts.Add(cachedEntry);
                }
                else
                {
                    ScriptEntry entry = new ScriptEntry(component);
                    objectCache.Entries[instanceId] = entry;
                    scripts.Add(entry);
                }
            }

            if (objectCache.Entries.Count > 0
                && !objectCache.ActiveScriptComponentIds.SetEquals(objectCache.Entries.Keys))
            {
                var staleKeys = new List<int>(objectCache.Entries.Count);
                foreach (KeyValuePair<int, ScriptEntry> pair in objectCache.Entries)
                {
                    if (!objectCache.ActiveScriptComponentIds.Contains(pair.Key))
                        staleKeys.Add(pair.Key);
                }

                for (int i = 0; i < staleKeys.Count; i++)
                    objectCache.Entries.Remove(staleKeys[i]);
            }

            return scripts;
        }

        private static void DrawPanel(
            GameObject gameObject,
            List<ScriptEntry> scripts,
            int totalComponentSlots,
            HashSet<string> hiddenIds)
        {
            string stateKey = GetStableObjectKey(gameObject);
            string search = SessionState.GetString(GetSearchKey(stateKey), string.Empty);
            bool showAll = GetPanelBoolState(GetShowAllKey(stateKey), false);
            bool panelVisible = GetPanelBoolState(GetPanelVisibleKey(stateKey), true);

            EditorGUILayout.Space(2);
            using (new EditorGUILayout.VerticalScope(ESEditorPresentation.SurfaceStyle))
            {
                if (!panelVisible)
                {
                    DrawCollapsedPanelHeader(stateKey, totalComponentSlots);
                    return;
                }

                DrawPanelHeader(stateKey, totalComponentSlots);
                EditorGUILayout.Space(1f);
                DrawSearchRow(stateKey, ref search);
                DrawOriginSummary(scripts, search, hiddenIds);
                EditorGUILayout.Space(1f);

                List<ESEditorSectionNavigatorItem> categoryItems = BuildCategoryItems(scripts, search, hiddenIds);
                string category = ESEditorSectionNavigatorIMGUI.Draw(
                    GetCategoryKey(stateKey),
                    "all",
                    categoryItems);

                Rect dividerRect = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
                ESEditorPresentation.DrawDivider(dividerRect);

                List<ScriptEntry> visibleScripts = FilterScripts(scripts, search, category, hiddenIds);
                if (visibleScripts.Count == 0)
                {
                    EditorGUILayout.HelpBox("没有匹配的组件。", MessageType.Info);
                    return;
                }

                int shownCount = 0;
                for (int i = 0; i < visibleScripts.Count; i++)
                {
                    if (!showAll && shownCount >= VisibleRowLimit)
                    {
                        if (GUILayout.Button("展开全部结果 (" + visibleScripts.Count + ")", ESEditorInspectorControls.Button))
                        {
                            showAll = true;
                            SetPanelBoolState(GetShowAllKey(stateKey), true);
                        }
                        break;
                    }

                    DrawScriptEntry(visibleScripts[i], hiddenIds);
                    shownCount++;
                }
            }
        }

        private static void DrawPanelHeader(string stateKey, int componentSlotCount)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("组件筛选", ESEditorPresentation.HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(componentSlotCount + " 个组件槽位", ESEditorPresentation.MetaStyle);
            if (GUILayout.Button(
                    new GUIContent("收起", "收起组件筛选面板"),
                    ESEditorInspectorControls.Button,
                    GUILayout.Width(48)))
            {
                SetPanelBoolState(GetPanelVisibleKey(stateKey), false);
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSearchRow(string stateKey, ref string search)
        {
            EditorGUILayout.BeginHorizontal();
            string nextSearch = EditorGUILayout.TextField(
                search,
                ESEditorInspectorControls.ToolbarTextField,
                GUILayout.MinWidth(100),
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button(
                    new GUIContent("清空", "清空搜索条件"),
                    ESEditorInspectorControls.Button,
                    GUILayout.Width(44)))
            {
                nextSearch = string.Empty;
            }

            if (!string.Equals(nextSearch, search, StringComparison.Ordinal))
            {
                search = nextSearch;
                SessionState.SetString(GetSearchKey(stateKey), search);
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawOriginSummary(
            List<ScriptEntry> scripts,
            string search,
            HashSet<string> hiddenIds)
        {
            int esCount = 0;
            int projectCount = 0;
            int thirdPartyCount = 0;
            int unknownCount = 0;
            int nativeCount = 0;
            int missingCount = 0;

            for (int i = 0; i < scripts.Count; i++)
            {
                ScriptEntry entry = scripts[i];
                if (!MatchesSearch(entry, search))
                    continue;

                if (entry.IsMissing)
                {
                    missingCount++;
                    continue;
                }

                if (IsPanelHidden(entry, hiddenIds))
                    continue;

                switch (entry.OriginKind)
                {
                    case ESInspectorScriptOriginKind.ES:
                        esCount++;
                        break;
                    case ESInspectorScriptOriginKind.Project:
                        projectCount++;
                        break;
                    case ESInspectorScriptOriginKind.Unknown:
                        unknownCount++;
                        break;
                    case ESInspectorScriptOriginKind.UnityNative:
                        nativeCount++;
                        break;
                    default:
                        thirdPartyCount++;
                        break;
                }
            }

            string summary = "ES " + esCount + " · 项目 " + projectCount + " · 包 " + thirdPartyCount;
            if (missingCount > 0)
                summary += " · 丢失 " + missingCount;
            if (unknownCount > 0)
                summary += " · 未知 " + unknownCount;
            if (nativeCount > 0)
                summary += " · 原生 " + nativeCount;

            GUILayout.Label(summary, ESEditorPresentation.MetaStyle);
        }

        private static void DrawCollapsedPanelHeader(string stateKey, int componentSlotCount)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("组件筛选", ESEditorPresentation.HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(componentSlotCount + " 个组件槽位", ESEditorPresentation.MetaStyle);
            if (GUILayout.Button(
                    new GUIContent("展开", "展开组件筛选面板"),
                    ESEditorInspectorControls.Button,
                    GUILayout.Width(48)))
                SetPanelBoolState(GetPanelVisibleKey(stateKey), true);
            EditorGUILayout.EndHorizontal();
        }

        private static List<ESEditorSectionNavigatorItem> BuildCategoryItems(
            List<ScriptEntry> scripts,
            string search,
            HashSet<string> hiddenIds)
        {
            int allCount = 0;
            int enabledCount = 0;
            int disabledCount = 0;
            int esCount = 0;
            int projectCount = 0;
            int thirdPartyCount = 0;
            int unknownCount = 0;
            int nativeCount = 0;
            int missingCount = 0;
            int hiddenCount = 0;

            for (int i = 0; i < scripts.Count; i++)
            {
                ScriptEntry entry = scripts[i];
                if (!MatchesSearch(entry, search))
                    continue;

                if (entry.IsMissing)
                {
                    allCount++;
                    missingCount++;
                    continue;
                }

                if (IsPanelHidden(entry, hiddenIds))
                {
                    hiddenCount++;
                    continue;
                }

                allCount++;
                if (entry.CanToggle && entry.Behaviour.enabled)
                    enabledCount++;
                else if (entry.CanToggle && !entry.Behaviour.enabled)
                    disabledCount++;

                switch (entry.OriginKind)
                {
                    case ESInspectorScriptOriginKind.ES:
                        esCount++;
                        break;
                    case ESInspectorScriptOriginKind.Project:
                        projectCount++;
                        break;
                    case ESInspectorScriptOriginKind.Unknown:
                        unknownCount++;
                        break;
                    case ESInspectorScriptOriginKind.UnityNative:
                        nativeCount++;
                        break;
                    default:
                        thirdPartyCount++;
                        break;
                }
            }

            var items = new List<ESEditorSectionNavigatorItem>(8)
            {
                new ESEditorSectionNavigatorItem("all", "全部", "显示所有匹配组件。", allCount.ToString()),
                new ESEditorSectionNavigatorItem("enabled", "已启用", "只显示已启用的脚本组件。", enabledCount.ToString())
            };

            if (disabledCount > 0)
                items.Add(new ESEditorSectionNavigatorItem("disabled", "已禁用", "只显示被禁用的脚本组件。", disabledCount.ToString()));

            if (esCount > 0)
                items.Add(new ESEditorSectionNavigatorItem("es", "ES", "只显示 Assets/Plugins/ES/ 下的脚本。", esCount.ToString()));

            if (projectCount > 0)
                items.Add(new ESEditorSectionNavigatorItem("project", "项目", "只显示其余 Assets/ 下的项目脚本。", projectCount.ToString()));

            if (thirdPartyCount > 0)
                items.Add(new ESEditorSectionNavigatorItem("thirdparty", "包", "只显示 Packages/ 下的第三方包脚本。", thirdPartyCount.ToString()));

            if (unknownCount > 0)
                items.Add(new ESEditorSectionNavigatorItem("unknown", "未知", "只显示来源未知的组件。", unknownCount.ToString()));

            if (nativeCount > 0)
                items.Add(new ESEditorSectionNavigatorItem("native", "原生", "只显示 Unity 原生组件。", nativeCount.ToString()));

            items.Add(new ESEditorSectionNavigatorItem("missing", "丢失", "只显示 Missing Script。", missingCount.ToString()));

            if (hiddenCount > 0)
                items.Add(new ESEditorSectionNavigatorItem("hidden", "隐藏", "只显示已通过此工具隐藏的组件。", hiddenCount.ToString()));

            return items;
        }

        private static List<ScriptEntry> FilterScripts(
            List<ScriptEntry> scripts,
            string search,
            string category,
            HashSet<string> hiddenIds)
        {
            var result = new List<ScriptEntry>(scripts.Count);
            for (int i = 0; i < scripts.Count; i++)
            {
                ScriptEntry entry = scripts[i];
                if (!MatchesSearch(entry, search))
                    continue;

                if (!MatchesCategory(entry, category, hiddenIds))
                    continue;

                result.Add(entry);
            }

            return result;
        }

        private static bool MatchesSearch(ScriptEntry entry, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            string query = search.Trim().ToLowerInvariant();
            return entry.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesCategory(
            ScriptEntry entry,
            string category,
            HashSet<string> hiddenIds)
        {
            switch (category)
            {
                case "all":
                    return !IsPanelHidden(entry, hiddenIds);
                case "enabled":
                    return entry.CanToggle
                        && !entry.IsMissing
                        && !IsPanelHidden(entry, hiddenIds)
                        && entry.Behaviour.enabled;
                case "disabled":
                    return entry.CanToggle
                        && !entry.IsMissing
                        && !IsPanelHidden(entry, hiddenIds)
                        && !entry.Behaviour.enabled;
                case "es":
                    return !entry.IsMissing
                        && entry.OriginKind == ESInspectorScriptOriginKind.ES
                        && !IsPanelHidden(entry, hiddenIds);
                case "project":
                    return !entry.IsMissing
                        && entry.OriginKind == ESInspectorScriptOriginKind.Project
                        && !IsPanelHidden(entry, hiddenIds);
                case "thirdparty":
                    return !entry.IsMissing
                        && entry.OriginKind == ESInspectorScriptOriginKind.ThirdParty
                        && !IsPanelHidden(entry, hiddenIds);
                case "unknown":
                    return !entry.IsMissing
                        && entry.OriginKind == ESInspectorScriptOriginKind.Unknown
                        && !IsPanelHidden(entry, hiddenIds);
                case "native":
                    return !entry.IsMissing
                        && entry.OriginKind == ESInspectorScriptOriginKind.UnityNative
                        && !IsPanelHidden(entry, hiddenIds);
                case "missing":
                    return entry.IsMissing;
                case "hidden":
                    return entry.CanHide && hiddenIds.Contains(entry.GlobalObjectIdString);
                default:
                    return false;
            }
        }

        private static bool IsPanelHidden(ScriptEntry entry, HashSet<string> hiddenIds)
        {
            return entry != null
                && entry.CanHide
                && hiddenIds != null
                && hiddenIds.Contains(entry.GlobalObjectIdString);
        }

        private static void DrawScriptEntry(ScriptEntry entry, HashSet<string> hiddenIds)
        {
            if (entry.IsMissing)
            {
                DrawMissingScriptEntry();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            bool isHidden = entry.CanHide && hiddenIds.Contains(entry.GlobalObjectIdString);
            if (entry.CanToggle)
            {
                EditorGUI.BeginDisabledGroup(Application.isPlaying);
                bool nextEnabled = EditorGUILayout.Toggle(GUIContent.none, entry.Behaviour.enabled, GUILayout.Width(20));
                EditorGUI.EndDisabledGroup();
                if (nextEnabled != entry.Behaviour.enabled)
                {
                    Undo.RecordObject(entry.Behaviour, "Toggle Component");
                    entry.Behaviour.enabled = nextEnabled;
                    EditorUtility.SetDirty(entry.Behaviour);
                }
            }
            else
            {
                GUILayout.Space(20);
            }

            Color previousColor = GUI.color;
            bool visualDisabled = isHidden || (entry.CanToggle && !entry.Behaviour.enabled);
            if (visualDisabled)
                GUI.color = DisabledLabelColor;
            else
                GUI.color = GetOriginColor(entry.OriginKind);

            GUILayout.Label(entry.OriginShortName, ScriptLabelStyle, GUILayout.Width(32));

            GUI.color = previousColor;
            if (visualDisabled)
                GUI.color = DisabledLabelColor;

            GUILayout.Label(
                new GUIContent(entry.DisplayName, entry.Tooltip),
                ScriptLabelStyle,
                GUILayout.MinWidth(40),
                GUILayout.ExpandWidth(true));

            GUI.color = previousColor;

            if (GUILayout.Button(
                    new GUIContent("选", "选中此组件"),
                    ESEditorInspectorControls.Button,
                    GUILayout.Width(28)))
                Selection.activeObject = entry.SourceComponent;

            if (entry.CanOpenCode
                && GUILayout.Button(
                    new GUIContent("码", "在 Project 中定位脚本"),
                    ESEditorInspectorControls.Button,
                    GUILayout.Width(28)))
            {
                MonoScript script = TryGetMonoScript(entry.Behaviour);
                if (script != null)
                    EditorGUIUtility.PingObject(script);
            }

            EditorGUI.BeginDisabledGroup(Application.isPlaying || !entry.CanHide);
            if (GUILayout.Button(
                    new GUIContent(isHidden ? "显" : "隐", isHidden ? "从组件筛选面板显示" : "从组件筛选面板隐藏"),
                    ESEditorInspectorControls.Button,
                    GUILayout.Width(28)))
                SetScriptHidden(entry, hiddenIds, !isHidden);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawMissingScriptEntry()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);

            Color previousColor = GUI.color;
            GUI.color = MissingScriptColor;
            GUILayout.Label(new GUIContent("Missing Script", "组件引用已丢失，请在默认 Inspector 中处理。"), ScriptLabelStyle);
            GUI.color = previousColor;

            GUILayout.FlexibleSpace();
            GUILayout.Label("丢失", ESEditorPresentation.MetaStyle);
            EditorGUILayout.EndHorizontal();
        }

        private static Color GetOriginColor(ESInspectorScriptOriginKind kind)
        {
            switch (kind)
            {
                case ESInspectorScriptOriginKind.ES:
                    return EsOriginColor;
                case ESInspectorScriptOriginKind.Project:
                    return ProjectOriginColor;
                case ESInspectorScriptOriginKind.ThirdParty:
                    return ThirdPartyOriginColor;
                case ESInspectorScriptOriginKind.Unknown:
                    return UnknownOriginColor;
                case ESInspectorScriptOriginKind.UnityNative:
                    return UnityNativeOriginColor;
                default:
                    return MissingScriptColor;
            }
        }

        private static MonoScript TryGetMonoScript(MonoBehaviour behaviour)
        {
            try
            {
                return MonoScript.FromMonoBehaviour(behaviour);
            }
            catch
            {
                return null;
            }
        }

        private static HashSet<string> LoadHiddenScriptIds()
        {
            if (hiddenIdsLoaded)
                return cachedHiddenIds ?? new HashSet<string>(StringComparer.Ordinal);

            cachedHiddenIds = new HashSet<string>(StringComparer.Ordinal);
            cachedHiddenOriginalFlags = new Dictionary<string, HideFlags>(StringComparer.Ordinal);
            string raw = EditorPrefs.GetString(GetHiddenStorageKey(), string.Empty);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                string[] parts = raw.Split('\n');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    if (!string.IsNullOrWhiteSpace(part))
                        cachedHiddenIds.Add(part);
                }
            }

            string flagsRaw = EditorPrefs.GetString(GetHiddenOriginalFlagsStorageKey(), string.Empty);
            if (!string.IsNullOrWhiteSpace(flagsRaw))
            {
                string[] parts = flagsRaw.Split('\n');
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    int separator = part.IndexOf('|');
                    if (separator <= 0 || separator >= part.Length - 1)
                        continue;

                    string id = part.Substring(0, separator);
                    if (int.TryParse(part.Substring(separator + 1), out int flagsValue))
                        cachedHiddenOriginalFlags[id] = (HideFlags)flagsValue;
                }
            }

            hiddenIdsLoaded = true;
            return cachedHiddenIds;
        }

        private static void SaveHiddenScriptIds(
            HashSet<string> hiddenIds,
            Dictionary<string, HideFlags> hiddenOriginalFlags)
        {
            InvalidateHiddenIdsCache();
            if (hiddenIds == null || hiddenIds.Count == 0
                || hiddenOriginalFlags == null || hiddenOriginalFlags.Count == 0)
            {
                EditorPrefs.DeleteKey(GetHiddenStorageKey());
                EditorPrefs.DeleteKey(GetHiddenOriginalFlagsStorageKey());
                return;
            }

            EditorPrefs.SetString(GetHiddenStorageKey(), string.Join("\n", hiddenIds));
            var flagLines = new List<string>(hiddenOriginalFlags.Count);
            foreach (KeyValuePair<string, HideFlags> pair in hiddenOriginalFlags)
            {
                if (hiddenIds.Contains(pair.Key))
                    flagLines.Add(pair.Key + "|" + (int)pair.Value);
            }
            EditorPrefs.SetString(GetHiddenOriginalFlagsStorageKey(), string.Join("\n", flagLines));
        }

        private static void InvalidateHiddenIdsCache()
        {
            cachedHiddenIds = null;
            cachedHiddenOriginalFlags = null;
            hiddenIdsLoaded = false;
        }

        private static void SetScriptHidden(
            ScriptEntry entry,
            HashSet<string> hiddenIds,
            bool hidden)
        {
            if (entry == null || !entry.CanHide || hiddenIds == null)
                return;

            Dictionary<string, HideFlags> hiddenOriginalFlags = LoadHiddenOriginalFlags();
            if (hidden)
            {
                hiddenIds.Add(entry.GlobalObjectIdString);
                ApplyHideInInspector(entry, hiddenOriginalFlags);
            }
            else
            {
                hiddenIds.Remove(entry.GlobalObjectIdString);
                RestoreHideInInspector(entry, hiddenOriginalFlags);
            }

            SaveHiddenScriptIds(hiddenIds, hiddenOriginalFlags);
            RebuildInspectorViews();
        }

        private static void ApplyHideInInspector(
            ScriptEntry entry,
            Dictionary<string, HideFlags> hiddenOriginalFlags)
        {
            Component component = entry?.SourceComponent;
            if (component == null || string.IsNullOrEmpty(entry.GlobalObjectIdString))
                return;

            if (!hiddenOriginalFlags.ContainsKey(entry.GlobalObjectIdString))
                hiddenOriginalFlags[entry.GlobalObjectIdString] = component.hideFlags;
            component.hideFlags |= HideFlags.HideInInspector;
        }

        private static void RestoreHideInInspector(
            ScriptEntry entry,
            Dictionary<string, HideFlags> hiddenOriginalFlags)
        {
            Component component = entry?.SourceComponent;
            if (component == null || string.IsNullOrEmpty(entry.GlobalObjectIdString))
                return;

            if (hiddenOriginalFlags.TryGetValue(entry.GlobalObjectIdString, out HideFlags originalFlags))
            {
                component.hideFlags = originalFlags;
                hiddenOriginalFlags.Remove(entry.GlobalObjectIdString);
            }
            else
            {
                component.hideFlags &= ~HideFlags.HideInInspector;
            }
        }

        private static Component ResolveHiddenComponent(string globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
                return null;

            try
            {
                if (!GlobalObjectId.TryParse(globalObjectId, out GlobalObjectId parsedId))
                    return null;

                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsedId) as Component;
            }
            catch
            {
                return null;
            }
        }

        private static string GetHiddenStorageKey()
        {
            return HiddenStorageKeyPrefix + GetProjectHash();
        }

        private static string GetHiddenOriginalFlagsStorageKey()
        {
            return HiddenOriginalFlagsStorageKeyPrefix + GetProjectHash();
        }

        private static Dictionary<string, HideFlags> LoadHiddenOriginalFlags()
        {
            LoadHiddenScriptIds();
            return cachedHiddenOriginalFlags ?? new Dictionary<string, HideFlags>(StringComparer.Ordinal);
        }

        private static void RebuildInspectorViews()
        {
            if (ActiveEditorTracker.sharedTracker != null)
                ActiveEditorTracker.sharedTracker.ForceRebuild();

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static string GetProjectHash()
        {
            unchecked
            {
                int hash = 17;
                string value = Application.dataPath ?? string.Empty;
                for (int i = 0; i < value.Length; i++)
                    hash = hash * 31 + value[i];

                return hash.ToString("X8");
            }
        }

        private static GUIStyle ScriptLabelStyle
        {
            get
            {
                if (scriptLabelStyle == null || scriptLabelStyleSkinGeneration != ESEditorPresentation.SkinGeneration)
                {
                    scriptLabelStyle = new GUIStyle(ESEditorPresentation.MetaStyle)
                    {
                        wordWrap = false,
                        clipping = TextClipping.Clip,
                        fontSize = 12
                    };
                    scriptLabelStyle.normal.textColor = ESEditorPresentation.SectionTextColor;
                    scriptLabelStyle.hover.textColor = ESEditorPresentation.SectionSelectedTextColor;
                    scriptLabelStyle.normal.background = ESEditorPresentation.SurfaceStyle.normal.background;
                    scriptLabelStyleSkinGeneration = ESEditorPresentation.SkinGeneration;
                }

                return scriptLabelStyle;
            }
        }

        private static string GetSearchKey(string stateKey)
        {
            return SearchKeyPrefix + GetProjectHash() + "." + stateKey;
        }

        private static string GetShowAllKey(string stateKey)
        {
            return ShowAllKeyPrefix + GetProjectHash() + "." + stateKey;
        }

        private static string GetCategoryKey(string stateKey)
        {
            return CategoryKeyPrefix + GetProjectHash() + "." + stateKey;
        }

        private static string GetPanelVisibleKey(string stateKey)
        {
            return PanelVisibleKeyPrefix + GetProjectHash() + "." + stateKey;
        }

        private static bool GetPanelBoolState(string key, bool defaultValue)
        {
            string raw = SessionState.GetString(key, null);
            if (raw == null)
                return defaultValue;

            return raw == "1";
        }

        private static void SetPanelBoolState(string key, bool value)
        {
            SessionState.SetString(key, value ? "1" : "0");
        }

        private static bool HasPanelBoolState(string key)
        {
            return SessionState.GetString(key, null) != null;
        }

        private static void ErasePanelBoolState(string key)
        {
            SessionState.EraseString(key);
        }

        private static void EnsureGameObjectCacheEvents()
        {
            if (gameObjectCacheEventsInstalled)
                return;

            EditorApplication.hierarchyChanged -= OnHierarchyChangedForGameObjectCaches;
            EditorApplication.hierarchyChanged += OnHierarchyChangedForGameObjectCaches;
            EditorSceneManager.sceneSaved -= OnSceneSavedForGameObjectCaches;
            EditorSceneManager.sceneSaved += OnSceneSavedForGameObjectCaches;
            EditorSceneManager.sceneSaving -= OnSceneSavingForGameObjectCaches;
            EditorSceneManager.sceneSaving += OnSceneSavingForGameObjectCaches;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChangedForVisibility;
            EditorApplication.playModeStateChanged += OnPlayModeStateChangedForVisibility;
            AssemblyReloadEvents.beforeAssemblyReload -= RestoreAllHiddenComponents;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAllHiddenComponents;
            AssemblyReloadEvents.afterAssemblyReload -= ReapplyHiddenComponents;
            AssemblyReloadEvents.afterAssemblyReload += ReapplyHiddenComponents;
            EditorApplication.wantsToQuit -= OnWantsToQuitForVisibility;
            EditorApplication.wantsToQuit += OnWantsToQuitForVisibility;
            gameObjectCacheEventsInstalled = true;
        }

        private static void TeardownGameObjectCacheEvents()
        {
            RestoreAllHiddenComponents();
            SaveHiddenScriptIds(
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, HideFlags>(StringComparer.Ordinal));

            if (gameObjectCacheEventsInstalled)
            {
                EditorApplication.hierarchyChanged -= OnHierarchyChangedForGameObjectCaches;
                EditorSceneManager.sceneSaved -= OnSceneSavedForGameObjectCaches;
                EditorSceneManager.sceneSaving -= OnSceneSavingForGameObjectCaches;
                EditorApplication.playModeStateChanged -= OnPlayModeStateChangedForVisibility;
                AssemblyReloadEvents.beforeAssemblyReload -= RestoreAllHiddenComponents;
                AssemblyReloadEvents.afterAssemblyReload -= ReapplyHiddenComponents;
                EditorApplication.wantsToQuit -= OnWantsToQuitForVisibility;
            }

            gameObjectCacheEventsInstalled = false;
            GameObjectCaches.Clear();
            cachedHiddenIds = null;
            cachedHiddenOriginalFlags = null;
            hiddenIdsLoaded = false;
            cacheAccessCount = 0;
            gameObjectCacheAccessCounter = 0;
        }

        private static void OnHierarchyChangedForGameObjectCaches()
        {
            PruneDestroyedGameObjectCaches();
            MarkProvisionalStableKeysDirty();
        }

        private static void OnSceneSavedForGameObjectCaches(UnityEngine.SceneManagement.Scene scene)
        {
            MarkProvisionalStableKeysDirty();
            ReapplyHiddenComponents();
        }

        private static void OnSceneSavingForGameObjectCaches(
            UnityEngine.SceneManagement.Scene scene,
            string path)
        {
            RestoreAllHiddenComponents();
        }

        private static void OnPlayModeStateChangedForVisibility(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                RestoreAllHiddenComponents();
            else if (state == PlayModeStateChange.EnteredEditMode)
                ReapplyHiddenComponents();
        }

        private static bool OnWantsToQuitForVisibility()
        {
            RestoreAllHiddenComponents();
            return true;
        }

        private static void ReapplyHiddenComponents()
        {
            HashSet<string> hiddenIds = LoadHiddenScriptIds();
            if (hiddenIds.Count == 0)
                return;

            foreach (string hiddenId in hiddenIds)
            {
                Component component = ResolveHiddenComponent(hiddenId);
                if (component != null)
                    component.hideFlags |= HideFlags.HideInInspector;
            }
        }

        private static void MarkProvisionalStableKeysDirty()
        {
            foreach (KeyValuePair<int, GameObjectScriptCache> pair in GameObjectCaches)
            {
                if (pair.Value != null && pair.Value.StableKeyProvisional)
                    pair.Value.StableKeyDirty = true;
            }
        }

        private static void RefreshStableObjectKey(GameObjectScriptCache cache)
        {
            if (cache == null || cache.GameObject == null)
                return;

            string previousKey = cache.StableKey;
            string nextKey = BuildStableObjectKey(cache.GameObject, out bool provisional);
            if (!string.Equals(previousKey, nextKey, StringComparison.Ordinal))
                MigratePanelStateKeys(previousKey, nextKey);

            cache.StableKey = nextKey;
            cache.StableKeyProvisional = provisional;
            cache.StableKeyDirty = false;
        }

        private static void MigratePanelStateKeys(string oldStateKey, string newStateKey)
        {
            if (string.IsNullOrWhiteSpace(oldStateKey)
                || string.Equals(oldStateKey, newStateKey, StringComparison.Ordinal))
            {
                return;
            }

            string oldSearchKey = GetSearchKey(oldStateKey);
            string newSearchKey = GetSearchKey(newStateKey);
            string search = SessionState.GetString(oldSearchKey, null);
            if (search != null)
            {
                if (SessionState.GetString(newSearchKey, null) == null)
                    SessionState.SetString(newSearchKey, search);
                SessionState.EraseString(oldSearchKey);
            }

            MigratePanelBoolState(GetShowAllKey(oldStateKey), GetShowAllKey(newStateKey), false);
            MigratePanelBoolState(GetPanelVisibleKey(oldStateKey), GetPanelVisibleKey(newStateKey), true);

            string oldNavigatorKey = GetNavigatorCategoryKey(oldStateKey);
            string category = SessionState.GetString(oldNavigatorKey, null);
            if (category != null)
            {
                if (SessionState.GetString(GetNavigatorCategoryKey(newStateKey), null) == null)
                    SessionState.SetString(GetNavigatorCategoryKey(newStateKey), category);
                SessionState.EraseString(oldNavigatorKey);
            }
        }

        private static void MigratePanelBoolState(string oldKey, string newKey, bool defaultValue)
        {
            if (HasPanelBoolState(oldKey))
            {
                bool oldValue = GetPanelBoolState(oldKey, defaultValue);
                if (!HasPanelBoolState(newKey))
                    SetPanelBoolState(newKey, oldValue);

                ErasePanelBoolState(oldKey);
            }
            else
            {
                bool legacyValue = SessionState.GetBool(oldKey, defaultValue);
                if (legacyValue != defaultValue && !HasPanelBoolState(newKey))
                    SetPanelBoolState(newKey, legacyValue);
            }

            SessionState.EraseBool(oldKey);
        }

        private static string GetNavigatorCategoryKey(string stateKey)
        {
            return NavigatorSessionKeyPrefix + GetCategoryKey(stateKey);
        }

        private static void PruneGameObjectCachesIfNeeded()
        {
            PruneDestroyedGameObjectCaches();
            if (GameObjectCaches.Count <= MaxGameObjectCaches)
                return;

            var ordered = new List<KeyValuePair<int, GameObjectScriptCache>>(GameObjectCaches);
            ordered.Sort((left, right) => left.Value.LastAccessOrder.CompareTo(right.Value.LastAccessOrder));

            int removeCount = GameObjectCaches.Count - MaxGameObjectCaches;
            for (int i = 0; i < removeCount; i++)
                GameObjectCaches.Remove(ordered[i].Key);
        }

        private static void PruneDestroyedGameObjectCaches()
        {
            if (GameObjectCaches.Count == 0)
                return;

            var staleKeys = new List<int>();
            foreach (KeyValuePair<int, GameObjectScriptCache> pair in GameObjectCaches)
            {
                if (pair.Value == null || pair.Value.GameObject == null)
                    staleKeys.Add(pair.Key);
            }

            for (int i = 0; i < staleKeys.Count; i++)
                GameObjectCaches.Remove(staleKeys[i]);
        }

        private static void PruneDestroyedGameObjectCachesOnAccess()
        {
            if (GameObjectCaches.Count == 0)
                return;

            cacheAccessCount++;
            if ((cacheAccessCount % CacheCleanupAccessInterval) != 0)
                return;

            PruneDestroyedGameObjectCaches();
        }

        private static string GetStableObjectKey(GameObject gameObject)
        {
            return GetOrCreateGameObjectCache(gameObject).StableKey;
        }

        private static GameObjectScriptCache GetOrCreateGameObjectCache(GameObject gameObject)
        {
            EnsureGameObjectCacheEvents();
            PruneDestroyedGameObjectCachesOnAccess();

            int instanceId = gameObject.GetInstanceID();
            if (GameObjectCaches.TryGetValue(instanceId, out GameObjectScriptCache cached)
                && cached != null
                && cached.GameObject == gameObject
                && cached.GameObject != null)
            {
                if (cached.StableKeyDirty)
                    RefreshStableObjectKey(cached);

                cached.LastAccessOrder = ++gameObjectCacheAccessCounter;
                return cached;
            }

            var cache = new GameObjectScriptCache
            {
                GameObject = gameObject,
                StableKey = BuildStableObjectKey(gameObject, out bool provisional),
                StableKeyProvisional = provisional,
                LastAccessOrder = ++gameObjectCacheAccessCounter
            };
            GameObjectCaches[instanceId] = cache;
            PruneGameObjectCachesIfNeeded();
            return cache;
        }

        private static string BuildStableObjectKey(GameObject gameObject, out bool provisional)
        {
            if (gameObject == null)
            {
                provisional = true;
                return "null";
            }

            try
            {
                GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
                if (id.identifierType != 0
                    && id.targetObjectId != 0
                    && GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) == gameObject)
                {
                    provisional = false;
                    return id.ToString();
                }
            }
            catch
            {
            }

            UnityEngine.SceneManagement.Scene scene = gameObject.scene;
            string scenePath = scene.IsValid() ? scene.path : string.Empty;
            if (!string.IsNullOrEmpty(scenePath))
            {
                string hierarchyPath = GetHierarchyPath(gameObject);
                if (!string.IsNullOrEmpty(hierarchyPath))
                {
                    provisional = true;
                    return scenePath + "|" + hierarchyPath;
                }
            }

            provisional = true;
            return "instance:" + gameObject.GetInstanceID();
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            if (gameObject == null)
                return string.Empty;

            var names = new List<string>(8);
            Transform current = gameObject.transform;
            while (current != null)
            {
                names.Add(current.name + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private sealed class GameObjectScriptCache
        {
            public GameObject GameObject;
            public string StableKey;
            public bool StableKeyProvisional;
            public bool StableKeyDirty;
            public long LastAccessOrder;
            public readonly List<Component> Components = new List<Component>(16);
            public readonly Dictionary<int, ScriptEntry> Entries = new Dictionary<int, ScriptEntry>();
            public readonly HashSet<int> ActiveScriptComponentIds = new HashSet<int>();
            public int LastComponentCount = -1;
        }

        private sealed class ScriptEntry
        {
            public readonly MonoBehaviour Behaviour;
            public readonly Component SourceComponent;
            public readonly Type Type;
            public readonly string DisplayName;
            public readonly string Namespace;
            public readonly string SearchText;
            public readonly string GlobalObjectIdString;
            public readonly string AssemblyName;
            public readonly string AssetPath;
            public readonly ESInspectorScriptOriginKind OriginKind;
            public readonly bool IsMissing;
            public bool CanHide => !IsMissing && !string.IsNullOrEmpty(GlobalObjectIdString);
            public bool IsMonoBehaviour => Behaviour != null;
            public bool CanToggle => IsMonoBehaviour;
            public bool CanOpenCode => IsMonoBehaviour;
            public string OriginShortName => ESInspectorScriptOriginClassifier.GetDisplayName(OriginKind);
            public string Tooltip => ESInspectorScriptOriginClassifier.GetTooltip(OriginKind, AssetPath, AssemblyName);

            public ScriptEntry(Component component)
            {
                SourceComponent = component;
                if (component == null)
                {
                    IsMissing = true;
                    DisplayName = "Missing Script";
                    Namespace = "<missing>";
                    SearchText = "missing script";
                    AssemblyName = string.Empty;
                    AssetPath = string.Empty;
                    OriginKind = ESInspectorScriptOriginKind.Missing;
                    return;
                }

                Type = component.GetType();
                DisplayName = Type.Name;
                Namespace = string.IsNullOrEmpty(Type.Namespace) ? "<global>" : Type.Namespace;
                AssemblyName = TryGetAssemblyName(Type);
                if (component is MonoBehaviour behaviour)
                {
                    Behaviour = behaviour;
                    MonoScript script = TryGetMonoScript(behaviour);
                    try
                    {
                        AssetPath = script != null
                            ? AssetDatabase.GetAssetPath(script)
                            : string.Empty;
                    }
                    catch
                    {
                        AssetPath = string.Empty;
                    }

                    OriginKind = ESInspectorScriptOriginClassifier.Classify(AssetPath, AssemblyName, false);
                }
                else
                {
                    AssetPath = string.Empty;
                    OriginKind = ESInspectorScriptOriginKind.UnityNative;
                }

                SearchText = (Type.FullName ?? DisplayName).ToLowerInvariant()
                    + " " + Namespace.ToLowerInvariant()
                    + " " + OriginShortName.ToLowerInvariant()
                    + " " + (AssemblyName ?? string.Empty).ToLowerInvariant();
                GlobalObjectIdString = TryGetGlobalObjectId(component);
            }

            private static string TryGetGlobalObjectId(Component component)
            {
                try
                {
                    GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(component);
                    return id.identifierType != 0 && id.targetObjectId != 0
                        ? id.ToString()
                        : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }

            private static string TryGetAssemblyName(Type type)
            {
                try
                {
                    return type?.Assembly?.GetName()?.Name ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }
}
