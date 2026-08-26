using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace ES
{
    public sealed class ESResWindowEditorBridgeInitializer : EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            ESResWindow.RegisterEditorBridge();
        }
    }

    public class ESResWindow : ESOdinMenuTreeWindow<ESResWindow> //OdinMenuEditorWindow
    {
        public override string ESWindow_PresentationShortTitle => "资源";
        public override bool UseScrollView => true;
        protected override string ESWindow_MigrationId => "resource.window";

        internal static void SetRemotePlanPreflightStatus(string status, string detail)
        {
            Page_Root_GlobalSetting.SetRemotePlanPreflightStatus(status, detail);
        }

        private bool consumerConfigurationWarningShown;

        internal static void RegisterEditorBridge()
        {
            ESAssetReferEditorBridge.OpenRegistryPage = OpenAndSelectAssetPage;
            ESAssetReferEditorBridge.OpenAssetRegistration = asset =>
                ESResourceCollectionWorkflowWindow.OpenForAssetRegistration(asset);
            ESAssetReferEditorBridge.OpenAssetKeyUpdate = (page, enumKey, stringKey) =>
                ESResourceCollectionWorkflowWindow.OpenForAssetKeyUpdate(page, enumKey, stringKey);
            ESAssetReferEditorBridge.OpenGameCoreRootRegistration = (source, consumer) =>
                ESResourceCollectionWorkflowWindow.OpenForGameCoreRootRegistration(source, consumer);
            ESAssetReferEditorBridge.OpenConsumerSynchronization = consumer =>
                ESResourceCollectionWorkflowWindow.OpenForConsumerSynchronization(consumer);
            ESAssetReferEditorBridge.IsAuthoringWriteLocked = () =>
                ESContentRegistrationAuthoring.TryGetAuthoringWriteBlockReason(out _);
        }

        private static void OpenAndSelectAssetPage(ESAssetPage page)
        {
            if (page == null)
                return;

            OpenWindow();
            ESResWindow expectedWindow = UsingWindow;
            EditorApplication.CallbackFunction selectPageCallback = null;
            selectPageCallback = () =>
            {
                EditorApplication.delayCall -= selectPageCallback;
                if (expectedWindow == null
                    || !ReferenceEquals(UsingWindow, expectedWindow)
                    || menuTree == null)
                    return;
                foreach (OdinMenuItem item in menuTree.EnumerateTree())
                {
                    if (!(item.Value is ESAssetPage candidate))
                        continue;
                    bool identityMatches = !string.IsNullOrEmpty(page.AssetGuid)
                        && candidate.AssetGuid == page.AssetGuid
                        && candidate.LocalFileId == page.LocalFileId;
                    if (!ReferenceEquals(candidate, page) && !identityMatches)
                        continue;
                    menuTree.Selection.Clear();
                    menuTree.Selection.Add(item);
                    item.Select();
                    UsingWindow?.Repaint();
                    return;
                }
            };
            EditorApplication.delayCall += selectPageCallback;
        }

        [MenuItem(MenuItemPathDefine.RESOURCE_WINDOW_PATH, false, 0)]
        public static void TryOpenWindow()
        {
            ESWindowCommandRegistry.RecordOpened("asset_window");
            OpenWindow();
        }

        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "资产管理窗口", false, 0)]
        public static void TryOpenWindowFromQuickWindows()
        {
            ESWindowCommandRegistry.RecordOpened("asset_window");
            OpenWindow();
        }


        #region 数据缓存
        public const string MenuNameForLibraryRoot = "资源库";

        public ESLibraryWindowMenuTemplate<ESAssetLibraryConsumer, ESAssetLibrary, ESAssetBook, ESAssetPage> menuTemplate = new ESLibraryWindowMenuTemplate<ESAssetLibraryConsumer, ESAssetLibrary, ESAssetBook, ESAssetPage>();
        public Page_Root_GlobalSetting page_root_GlobalSettings;

        // public Page_Root_Build page_index_Build;
        #endregion

        public override void ES_SaveData()
        {
            base.ES_SaveData();
            AssetDatabase.Refresh();
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            PartPage_Library(tree);
            PartPage_Setting(tree);
            PartPage_Build(tree);
        }

        protected override void DrawEditors()
        {
            if (MenuTree?.Selection?.SelectedValue is Page_Root_GlobalSetting settingsPage)
            {
                Matrix4x4 previousMatrix = GUI.matrix;
                Color previousColor = GUI.color;
                Color previousContentColor = GUI.contentColor;
                Color previousBackgroundColor = GUI.backgroundColor;
                bool previousEnabled = GUI.enabled;
                int previousIndentLevel = EditorGUI.indentLevel;
                float previousLabelWidth = EditorGUIUtility.labelWidth;
                float previousFieldWidth = EditorGUIUtility.fieldWidth;
                try
                {
                    // Odin and embedded inspectors are allowed to be nested here. Keep this
                    // page from inheriting a leaked transform/indent from another drawer.
                    GUI.matrix = Matrix4x4.identity;
                    EditorGUI.indentLevel = 0;
                    EditorGUIUtility.labelWidth = 0f;
                    EditorGUIUtility.fieldWidth = 0f;
                    settingsPage.DrawPageWithoutScroll();
                }
                finally
                {
                    GUI.matrix = previousMatrix;
                    GUI.color = previousColor;
                    GUI.contentColor = previousContentColor;
                    GUI.backgroundColor = previousBackgroundColor;
                    GUI.enabled = previousEnabled;
                    EditorGUI.indentLevel = previousIndentLevel;
                    EditorGUIUtility.labelWidth = previousLabelWidth;
                    EditorGUIUtility.fieldWidth = previousFieldWidth;
                }
                return;
            }

            base.DrawEditors();
        }

        void PartPage_Library(OdinMenuTree tree)
        {
            string issue = GetResourceMenuConfigurationIssue();
            if (!string.IsNullOrEmpty(issue))
            {
                if (!consumerConfigurationWarningShown)
                {
                    consumerConfigurationWarningShown = true;
                    Debug.LogWarning(
                        "[ESResWindow] " + issue
                        + " 已跳过资源库/Consumer 菜单生成；窗口不会自动创建、改名或设置 Consumer，请手动修正后刷新窗口。");
                }
                return;
            }
            menuTemplate.ApplyTemplateToMenuTree(this, tree, MenuNameForLibraryRoot);
        }

        private static string GetResourceMenuConfigurationIssue()
        {
            List<ESAssetLibrary> libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>()
                ?.Where(item => item != null)
                .ToList() ?? new List<ESAssetLibrary>();
            var duplicateLibraryName = libraries
                .GroupBy(item => item.Name, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateLibraryName != null)
                return "存在同名 Library [" + duplicateLibraryName.Key + "]。";

            List<ESAssetLibraryConsumer> consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                ?.Where(item => item != null)
                .ToList() ?? new List<ESAssetLibraryConsumer>();

            if (consumers.Count == 0)
                return "未找到 Consumer。";

            int totalConsumerCount = consumers.Count(item => item.IsTotalConsumer);
            if (totalConsumerCount > 1)
                return "检测到 " + totalConsumerCount + " 个 Total Consumer。";
            if (totalConsumerCount == 0)
                return "存在 Consumer，但未设置 Total Consumer。";

            var duplicateName = consumers
                .GroupBy(item => item.Name, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateName != null)
                return "存在同名 Consumer [" + duplicateName.Key + "]。";

            foreach (ESAssetLibraryConsumer consumer in consumers)
                if (string.IsNullOrWhiteSpace(consumer.ConsumerId))
                    return "Consumer [" + consumer.Name + "] 缺少稳定 ID。";

            return null;
        }

        void PartPage_Setting(OdinMenuTree tree)
        {
            QuickBuildMigrationRootMenu(
                tree,
                "resource.window",
                "resource.settings-build",
                "设置与构建",
                ref page_root_GlobalSettings,
                SdfIconType.GearFill);
        }

        void PartPage_Build(OdinMenuTree tree)
        {
            //  QuickBuildRootMenu(tree, "构建", ref page_index_Build, SdfIconType.Building);
        }

        public class Page_Root_GlobalSetting : ESWindowPageBase
        {
            internal static void SetRemotePlanPreflightStatus(string status, string detail)
            {
                ESResWindow.UsingWindow?.Repaint();
            }

            private const string ResourcePipelineTaskKey = "ES.ResourcePipeline";
            private const string SelectedLibraryGuidSessionKey = "ES.ResWindow.Settings.SelectedLibraryGuid";
            private const int LibraryVisibleLimit = 5;

            /*
             直接绘制本体了哈 ESGlobalResSetting
             */
            private OdinEditor editor;
            private SerializedObject resSettingSerializedObject;
            private bool showFullSettings;
            private PipelineStageState currentPipelineStageState;
            private bool pipelineStageStateAvailable;
            private string pipelineStageStateError = string.Empty;
            private float currentLeftColumnWidth;

            private struct PipelineStageState
            {
                public bool CatalogPassed;
                public bool PlanPassed;
                public bool BuildExists;
                public bool PublishPassed;
                public bool ConsumerReleasePrepared;
            }

            internal void DrawPageWithoutScroll()
            {
                DrawPublishSettings();
                EditorGUILayout.Space(8f);

                // Use the width Odin assigned to this content area. The window width
                // includes the menu and outer padding, and feeding it back into GUILayout
                // can make the parent layout grow on every repaint.
                float availableWidth = EditorGUIUtility.currentViewWidth;
                availableWidth = Mathf.Max(0f, availableWidth);

                if (availableWidth < 720f)
                {
                    currentLeftColumnWidth = availableWidth;
                    DrawLeftColumn();
                    EditorGUILayout.Space(8f);
                    DrawRightColumn();
                    return;
                }

                float leftWidth = Mathf.Clamp(availableWidth * 0.46f, 280f, 520f);
                currentLeftColumnWidth = leftWidth;

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(
                        GUILayout.Width(leftWidth),
                        GUILayout.MinWidth(0f),
                        GUILayout.MaxWidth(leftWidth)))
                        DrawLeftColumn();

                    EditorGUILayout.Space(8f);

                    using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                        DrawRightColumn();
                }
            }

            private void DrawPublishSettings()
            {
                SimpleToolsPanelUtility.DrawSectionTitle(
                    "发布设置",
                    "平台、模式和版本在这里统一配置。");
                DrawCompactPublishSettings();
                showFullSettings = EditorGUILayout.Foldout(showFullSettings, "完整设置", true);
                if (!showFullSettings)
                    return;

                editor ??= OdinEditor.CreateEditor(ESGlobalResSetting.Instance, typeof(OdinEditor)) as OdinEditor;
                if (editor != null)
                {
                    editor.DrawDefaultInspector();
                }
            }

            private void DrawCompactPublishSettings()
            {
                ESGlobalResSetting settings = ESGlobalResSetting.Instance;
                if (settings == null)
                {
                    EditorGUILayout.HelpBox("未找到全局资源设置。", MessageType.Error);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawPublishSummaryValue("平台", settings.applyPlatform.ToString());
                    DrawPublishSummaryValue("模式", GetAssetRunModeDisplayName(settings.AssetRunMode));
                    DrawPublishSummaryValue("版本", settings.Version);
                }
            }

            private static string GetAssetRunModeDisplayName(ESAssetRunMode mode)
            {
                switch (mode)
                {
                    case ESAssetRunMode.EditorDirect: return "编辑器直连";
                    case ESAssetRunMode.EditorSimulateBuild: return "编辑器模拟发布";
                    case ESAssetRunMode.LocalBuild: return "本地构建资源";
                    case ESAssetRunMode.HotUpdate: return "热更新资源";
                    default: return mode.ToString();
                }
            }

            private static void DrawPublishSummaryValue(string label, string value)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
                {
                    EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(value) ? "未设置" : value);
                }
            }

            private void EnsureResSettingSerializedObject()
            {
                ESGlobalResSetting settings = ESGlobalResSetting.Instance;
                if (settings == null)
                {
                    ReleaseResSettingSerializedObject();
                    return;
                }

                if (resSettingSerializedObject == null
                    || resSettingSerializedObject.targetObject != settings)
                {
                    ReleaseResSettingSerializedObject();
                    resSettingSerializedObject = new SerializedObject(settings);
                }
            }

            private void ReleaseResSettingSerializedObject()
            {
                try { resSettingSerializedObject?.Dispose(); }
                catch (Exception exception) { Debug.LogException(exception); }
                finally { resSettingSerializedObject = null; }
            }

            private void DrawResSettingProperty(string propertyPath, string label)
            {
                EnsureResSettingSerializedObject();
                if (resSettingSerializedObject == null)
                    return;

                SerializedProperty property = resSettingSerializedObject.FindProperty(propertyPath);
                if (property == null)
                    return;

                EditorGUILayout.PropertyField(property, new GUIContent(label), true);
                resSettingSerializedObject.ApplyModifiedProperties();
            }

            public override void OnPageDisable()
            {
                base.OnPageDisable();
                showFullSettings = false;
                reorderableListForLibraries = null;
                filteredLibraries.Clear();
                visibleLibraries.Clear();
                ReleaseResSettingSerializedObject();
                if (editor != null)
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                    editor = null;
                }
            }
            private ReorderableList reorderableListForLibraries;
            private List<ESAssetLibrary> libraries;
            private List<ESAssetLibrary> filteredLibraries = new List<ESAssetLibrary>();
            private List<ESAssetLibrary> visibleLibraries = new List<ESAssetLibrary>();
            private string selectedLibraryGuid = string.Empty;
            private string librarySearch = string.Empty;
            private bool onlyBuildEnabled;
            private bool onlyHasIssues;
            private ESAssetLibrary selectedLibrary;
            private Dictionary<ESAssetLibrary, LibrarySummary> librarySummaries =
                new Dictionary<ESAssetLibrary, LibrarySummary>();

            private void DrawLeftColumn()
            {
                DrawLibs();
                EditorGUILayout.Space(6f);
                DrawBuildAndRun();
            }

            public void DrawLibs()
            {
                float columnWidth = currentLeftColumnWidth > 0f
                    ? currentLeftColumnWidth
                    : Mathf.Max(0f, EditorGUIUtility.currentViewWidth);
                using (new EditorGUILayout.VerticalScope(
                    GUILayout.MinWidth(0f),
                    GUILayout.MaxWidth(columnWidth),
                    GUILayout.ExpandWidth(true)))
                {
                    SirenixEditorGUI.BeginBox();
                SimpleToolsPanelUtility.DrawSectionTitle(
                    "资源库",
                    "选择要查看或发布参与的 Library；参与构建的库会进入发布链路。");
                EditorGUI.BeginChangeCheck();
                librarySearch = EditorGUILayout.TextField("搜索", librarySearch);
                using (new EditorGUILayout.HorizontalScope())
                {
                    onlyBuildEnabled = EditorGUILayout.ToggleLeft("仅参与构建", onlyBuildEnabled, GUILayout.Width(110f));
                    onlyHasIssues = EditorGUILayout.ToggleLeft("仅异常", onlyHasIssues, GUILayout.Width(90f));
                }
                if (EditorGUI.EndChangeCheck())
                    RefreshVisibleLibraries(revealSelection: true);

                if (filteredLibraries.Count > visibleLibraries.Count)
                {
                    EditorGUILayout.HelpBox(
                        "筛选结果过多，仅显示 5 个。请使用搜索或过滤缩小范围。",
                        MessageType.Info);
                }

                if (reorderableListForLibraries != null) reorderableListForLibraries.DoLayoutList();
                SirenixEditorGUI.EndBox();
                }

            }

            private void DrawBuildAndRun()
            {
                SirenixEditorGUI.BeginBox();
                SimpleToolsPanelUtility.DrawSectionTitle(
                    "构建与运行",
                    "平台、运行模式和版本会直接参与发布链路。");
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawResSettingProperty("applyPlatform", "应用平台");
                    DrawResSettingProperty("AssetRunMode", "资源加载模式");
                }
                DrawResSettingProperty("Version", "游戏版本号");
                DrawResSettingProperty("EnableResVerboseLog", "输出资源详细流程日志");
                SirenixEditorGUI.EndBox();
            }

            public override ESWindowPageBase ES_Refresh()
            {
                libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>();
                if (libraries != null)
                {
                    librarySummaries = new Dictionary<ESAssetLibrary, LibrarySummary>();
                    for (int index = 0; index < libraries.Count; index++)
                    {
                        ESAssetLibrary library = libraries[index];
                        if (library == null)
                            continue;
                        List<ESAssetBook> books = library.GetAllUseableBooks()
                            ?.Where(book => book != null)
                            .ToList() ?? new List<ESAssetBook>();
                        librarySummaries[library] = new LibrarySummary
                        {
                            Folder = library.LibFolderName,
                            BundleCode = library.AssetBundleCode ?? string.Empty,
                            ContainsBuild = library.ContainsBuild,
                            BookCount = books.Count,
                            PageCount = books.Sum(book => book.pages?.Count ?? 0),
                            HasIssue = library.ContainsBuild
                                && (string.IsNullOrWhiteSpace(library.AssetBundleCode)
                                    || books.Sum(book => book.pages?.Count ?? 0) == 0
                                    || !IsCatalogValidAtPath(library))
                        };
                    }

                    selectedLibraryGuid = SessionState.GetString(SelectedLibraryGuidSessionKey, string.Empty);
                    if (!string.IsNullOrEmpty(selectedLibraryGuid))
                    {
                        selectedLibrary = libraries.FirstOrDefault(library =>
                            library != null
                            && string.Equals(
                                GetLibraryGuid(library),
                                selectedLibraryGuid,
                                StringComparison.OrdinalIgnoreCase));
                    }
                    if (selectedLibrary == null || !libraries.Contains(selectedLibrary))
                    {
                        selectedLibrary = libraries.FirstOrDefault(library => library != null && library.ContainsBuild)
                            ?? libraries.FirstOrDefault(library => library != null);
                        selectedLibraryGuid = selectedLibrary == null ? string.Empty : GetLibraryGuid(selectedLibrary);
                        SessionState.SetString(SelectedLibraryGuidSessionKey, selectedLibraryGuid);
                    }
                    RefreshVisibleLibraries(revealSelection: true);
                    reorderableListForLibraries = new ReorderableList(visibleLibraries, typeof(ESAssetLibrary))
                    {
                        draggable = false,
                        displayAdd = false,
                        displayRemove = false,
                        elementHeight = 46f
                    };
                    SetupCallBackLibs();
                    reorderableListForLibraries.index = visibleLibraries.IndexOf(selectedLibrary);
                }

                return base.ES_Refresh();

            }
            private void RefreshVisibleLibraries(bool revealSelection = false)
            {
                filteredLibraries = (libraries ?? new List<ESAssetLibrary>())
                    .Where(library => library != null && IsLibraryVisible(library))
                    .ToList();

                visibleLibraries = filteredLibraries
                    .Take(LibraryVisibleLimit)
                    .ToList();
                if (revealSelection
                    && selectedLibrary != null
                    && filteredLibraries.Contains(selectedLibrary)
                    && !visibleLibraries.Contains(selectedLibrary))
                {
                    if (visibleLibraries.Count < LibraryVisibleLimit)
                        visibleLibraries.Add(selectedLibrary);
                    else
                        visibleLibraries[LibraryVisibleLimit - 1] = selectedLibrary;
                }
                if (reorderableListForLibraries != null)
                {
                    reorderableListForLibraries.list = visibleLibraries;
                    reorderableListForLibraries.index = visibleLibraries.IndexOf(selectedLibrary);
                }
                ESResWindow.UsingWindow?.Repaint();
            }

            private bool IsLibraryVisible(ESAssetLibrary library)
            {
                if (library == null)
                    return false;
                if (onlyBuildEnabled && !library.ContainsBuild)
                    return false;
                librarySummaries.TryGetValue(library, out LibrarySummary summary);
                if (onlyHasIssues && summary is { HasIssue: false })
                    return false;
                if (!string.IsNullOrWhiteSpace(librarySearch))
                {
                    string needle = librarySearch.Trim();
                    if (library.Name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0
                        && (library.LibFolderName ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;
                }
                return true;
            }

            private static bool IsCatalogValidAtPath(ESAssetLibrary library)
            {
                string path = Path.Combine(
                    ESAssetPipelineIO.LibraryBakeFolder(library.LibFolderName),
                    ESAssetPipelineIO.CatalogFileName);
                return TryReadJson(path, out ESAssetLibraryCatalog catalog)
                    && ESResourcePipelineStageValidators.IsCatalogValid(catalog);
            }

            private static string GetLibraryGuid(ESAssetLibrary library)
            {
                if (library == null)
                    return string.Empty;
                string path = AssetDatabase.GetAssetPath(library);
                return string.IsNullOrWhiteSpace(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path);
            }

            private static Color colorBL = Color.blue._WithAlpha(0.05f);
            private void SetupCallBackLibs()
            {
                reorderableListForLibraries.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(
                        rect,
                        "显示 " + visibleLibraries.Count
                        + " / 筛选结果 " + filteredLibraries.Count
                        + " / 全部 " + (libraries?.Count ?? 0));
                };

                reorderableListForLibraries.onSelectCallback = list =>
                {
                    if (visibleLibraries == null || list.index < 0 || list.index >= visibleLibraries.Count)
                        return;
                    selectedLibrary = visibleLibraries[list.index];
                    selectedLibraryGuid = GetLibraryGuid(selectedLibrary);
                    SessionState.SetString(SelectedLibraryGuidSessionKey, selectedLibraryGuid);
                    ESResWindow.UsingWindow?.Repaint();
                };

                reorderableListForLibraries.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (visibleLibraries == null) return;
                    var lib = visibleLibraries[index];
                    if (lib == null) return;
                    librarySummaries.TryGetValue(lib, out LibrarySummary summary);
                    bool selected = ReferenceEquals(lib, selectedLibrary);

                    Rect border = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
                    EditorGUI.DrawRect(border, selected
                        ? new Color(0.22f, 0.55f, 0.95f, 0.18f)
                        : new Color(0f, 0f, 0f, 0.04f));

                    Rect toggleRect = new Rect(rect.x + 6f, rect.y + 4f, 78f, 18f);
                    bool containsBuild = EditorGUI.ToggleLeft(
                        toggleRect,
                        "参与构建",
                        summary?.ContainsBuild ?? lib.ContainsBuild);
                    if (containsBuild != (summary?.ContainsBuild ?? lib.ContainsBuild))
                    {
                        Undo.RecordObject(lib, "Change Library Build Inclusion");
                        lib.ContainsBuild = containsBuild;
                        if (summary != null)
                            summary.ContainsBuild = containsBuild;
                        EditorUtility.SetDirty(lib);
                    }

                    Rect nameRect = new Rect(rect.x + 88f, rect.y + 4f, rect.width - 98f, 18f);
                    EditorGUI.LabelField(nameRect, new GUIContent(lib.Name, lib.Name));

                    Rect summaryRect = new Rect(rect.x + 8f, rect.y + 25f, rect.width - 16f, 18f);
                    string summaryText = "Book "
                        + (summary?.BookCount ?? 0)
                        + " | Page "
                        + (summary?.PageCount ?? 0)
                        + " | AB "
                        + (string.IsNullOrWhiteSpace(summary?.BundleCode) ? "未设置" : summary.BundleCode);
                    EditorGUI.LabelField(
                        summaryRect,
                        new GUIContent(summaryText, summaryText),
                        EditorStyles.miniLabel);
                };
            }

            private sealed class LibrarySummary
            {
                public string Folder;
                public string BundleCode;
                public bool ContainsBuild;
                public int BookCount;
                public int PageCount;
                public bool HasIssue;
            }

            private void DrawActionGateSummary(PipelineStageState state)
            {
                if (!state.CatalogPassed)
                {
                    EditorGUILayout.HelpBox("先完成 1. 烘焙引用，规划与后续步骤会保持禁用。", MessageType.Warning);
                    return;
                }
                if (!state.PlanPassed)
                {
                    EditorGUILayout.HelpBox("Catalog 已有效；下一步执行 2. 规划并标记。", MessageType.Info);
                    return;
                }
                if (!state.BuildExists)
                {
                    EditorGUILayout.HelpBox("规划已通过；下一步执行 3. 构建资源包。", MessageType.Info);
                    return;
                }
                if (!state.ConsumerReleasePrepared)
                {
                    EditorGUILayout.HelpBox("资源包产物已存在；发布前先执行 Consumer 代码包准备。", MessageType.Warning);
                    return;
                }
                if (!state.PublishPassed)
                {
                    EditorGUILayout.HelpBox("构建与 Consumer 准备已满足；下一步执行 4. 发布资源包。", MessageType.Info);
                    return;
                }
                EditorGUILayout.HelpBox("本地发布已通过；第五步将打开远端发布工具。", MessageType.Info);
            }

            private static bool IsResourcePipelineBusy()
            {
                return ESEditorHandle.IsSimpleTaskKeyActive(ResourcePipelineTaskKey)
                    || ESEditorHandle.IsLongTaskKeyActive(ResourcePipelineTaskKey);
            }

            private static List<ESAssetLibraryConsumer> GetConsumers()
            {
                return ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                    ?.Where(item => item != null)
                    .ToList() ?? new List<ESAssetLibraryConsumer>();
            }

            private bool TryGetPipelineStageState(
                out PipelineStageState state,
                out string error)
            {
                state = default;
                error = string.Empty;
                try
                {
                    string platform = ESAssetPipelineIO.PlatformName;
                    bool catalogPassed = HasCatalogStage(platform);
                    bool planPassed = HasPlanStage(platform);
                    bool buildExists = HasBuildStage(platform);
                    bool publishPassed = HasPublishStage(platform);
                    state = new PipelineStageState
                    {
                        CatalogPassed = catalogPassed,
                        PlanPassed = planPassed,
                        BuildExists = buildExists,
                        PublishPassed = publishPassed,
                        ConsumerReleasePrepared = HasConsumerReleasePreparation(platform)
                    };
                    return true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }

            private static bool HasConsumerReleasePreparation(string platform)
            {
                try
                {
                    ESCodeModuleEditorIntegration.ValidateConsumerReleasePrepared(
                        GetConsumers(),
                        platform);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private static bool HasCatalogStage(string platform)
            {
                return ESResourcePipelineStageValidators.HasCatalogStage(
                    ESEditorSO.GetGroupOfType<ESAssetLibrary>(),
                    library => Path.Combine(
                        ESAssetPipelineIO.LibraryBakeFolder(library.LibFolderName),
                        ESAssetPipelineIO.CatalogFileName),
                    library => Path.Combine(
                        ESAssetPipelineIO.LibraryBakeFolder(library.LibFolderName),
                        ESAssetPipelineIO.ReferenceGraphFileName));
            }

            private static bool HasPlanStage(string platform)
            {
                return ESResourcePipelineStageValidators.HasPlanStage(
                    Path.Combine(ESAssetPipelineIO.PlanRoot(platform), ESAssetPipelineIO.PlanFileName),
                    Path.Combine(ESAssetPipelineIO.PlanRoot(platform), ESAssetPipelineIO.AssetListFileName));
            }

            private static bool HasBuildStage(string platform)
            {
                string path = Path.Combine(
                    ESAssetPipelineIO.StagingRoot(platform),
                    ESAssetPipelineIO.BuildSetFileName);
                return TryReadJson(path, out ESAssetBuildSet _);
            }

            private static bool HasPublishStage(string platform)
            {
                string rootPath = Path.Combine(
                    ESAssetPipelineIO.LocalTestRoot(platform),
                    ESAssetPipelineIO.ReleaseManifestFileName);
                if (!TryReadJson(rootPath, out ESAssetReleaseManifest release)
                    || string.IsNullOrWhiteSpace(release.releaseVersion)
                    || string.IsNullOrWhiteSpace(release.totalConsumerUrl))
                    return false;

                string releaseFolder = Path.Combine(
                    ESAssetPipelineIO.LocalTestRoot(platform),
                    release.releaseVersion);
                string consumerUrl = release.totalConsumerUrl.Replace('\\', '/');
                int slash = consumerUrl.LastIndexOf('/');
                string consumerFileName = slash >= 0 ? consumerUrl.Substring(slash + 1) : consumerUrl;
                string consumerPath = Path.Combine(releaseFolder, "Consumers", consumerFileName);
                return ESResourcePipelineStageValidators.HasPublishStage(rootPath, releaseFolder, consumerPath);
            }

            private static bool TryReadJson<T>(string path, out T value) where T : class
            {
                value = null;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return false;
                try
                {
                    value = ESAssetPipelineIO.ReadJson<T>(path);
                    return value != null;
                }
                catch
                {
                    return false;
                }
            }

            private const float PipelineActionButtonHeight = 30f;

            private void DrawRightColumn()
            {
                DrawPublishActions();
                EditorGUILayout.Space(6f);
                DrawFolderPaths();
            }

            private void DrawPublishActions()
            {
                SimpleToolsPanelUtility.DrawSectionTitle(
                    "五步发布",
                    "按 1~5 顺序执行；后续步骤依赖前置门禁。");

                pipelineStageStateAvailable = TryGetPipelineStageState(
                    out currentPipelineStageState,
                    out pipelineStageStateError);

                SirenixEditorGUI.BeginBox();
                try
                {
                    if (IsResourcePipelineBusy())
                    {
                        EditorGUILayout.HelpBox("资源任务执行中，其他管线操作暂时锁定。", MessageType.Info);
                    }
                    else if (!pipelineStageStateAvailable)
                    {
                        EditorGUILayout.HelpBox(
                            "当前无法读取阶段门禁：" + pipelineStageStateError,
                            MessageType.Error);
                    }
                    else
                    {
                        DrawActionGateSummary(currentPipelineStageState);
                    }

                    AnalyzeAndAssignAssetPaths();
                    BuildAssetBundlesAndDependencies();
                    Click_Server();
                    Click_PrepareConsumerCode();
                    Click_ALL();
                    Click_RemotePublish();
                }
                finally
                {
                    SirenixEditorGUI.EndBox();
                }
            }

            public void AnalyzeAndAssignAssetPaths()
            {
                bool pipelineBusy = IsResourcePipelineBusy();
                using (new EditorGUI.DisabledScope(pipelineBusy || !pipelineStageStateAvailable))
                {
                    if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "1. 烘焙引用", GUILayout.Height(PipelineActionButtonHeight)))
                    {
                        ESEditorHandle.AddSimpleHandleTask(() =>
                        {
                            ESContentRegistrationResult result = ESContentRegistrationAuthoring.ExecuteBakeWithConfirmation();
                            if (result == null)
                            {
                                Debug.LogWarning("已取消资源引用 Bake。");
                            }
                            else if (!result.success)
                            {
                                Debug.LogError("资源引用 Bake 启动失败：" + result.message);
                            }
                            else
                            {
                                Debug.Log("资源引用 Bake 已通过统一内容注册入口入队，RunId=" + result.runId + "。入队不代表完成。");
                            }
                        }, key: ResourcePipelineTaskKey);
                    }
                }
            }

            public void BuildAssetBundlesAndDependencies()
            {
                bool pipelineBusy = IsResourcePipelineBusy();
                bool stageAllowed = pipelineStageStateAvailable && currentPipelineStageState.CatalogPassed;
                using (new EditorGUI.DisabledScope(pipelineBusy || !stageAllowed))
                {
                    if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "2. 规划并标记", GUILayout.Height(PipelineActionButtonHeight)))
                    {
                        ESEditorHandle.AddSimpleHandleTask(() =>
                        {
                            try
                            {
                                if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始-规划并标记 AB", "读取烘焙结果，生成可审查计划并仅修改 ES 管理标签。", "开始", "取消"))
                                {
                                    ESAssetBundleBuildPlanner.PlanAndMark();
                                    Debug.Log("资源包构建规划完成。");
                                }
                                else
                                {
                                    Debug.LogWarning("已取消资源包构建规划。");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.LogError($"资源包构建规划失败: {ex.Message}");
                            }
                        }, key: ResourcePipelineTaskKey);
                    }
                }
            }

            public void Click_Server()
            {
                bool pipelineBusy = IsResourcePipelineBusy();
                bool stageAllowed = pipelineStageStateAvailable && currentPipelineStageState.PlanPassed;
                using (new EditorGUI.DisabledScope(pipelineBusy || !stageAllowed))
                {
                    if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "3. 构建资源包", GUILayout.Height(PipelineActionButtonHeight)))
                    {
                        ESEditorHandle.AddSimpleHandleTask(() =>
                        {
                            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始构建资源包", "校验标签与计划一致后，构建到资源包暂存目录。", "开始", "取消"))
                            {
                                ESAssetBundleBuilder.Build();
                            }
                            else
                            {
                                Debug.LogWarning("放弃-<上传到服务器>");
                            }
                        }, key: ResourcePipelineTaskKey);
                    }
                }
            }

            public void Click_PrepareConsumerCode()
            {
                bool pipelineBusy = IsResourcePipelineBusy();
                bool stageAllowed = pipelineStageStateAvailable && currentPipelineStageState.BuildExists;
                using (new EditorGUI.DisabledScope(pipelineBusy || !stageAllowed))
                {
                    if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "Consumer 代码包准备", GUILayout.Height(PipelineActionButtonHeight)))
                    {
                        ESEditorHandle.AddSimpleHandleTask(() =>
                        {
                            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog(
                                "准备 Consumer 代码包",
                                "会按当前 Consumer 配置生成代码包并写入幂等准备标记。没有热更包的 Consumer 也会写入已准备标记，避免发布误拒绝。",
                                "准备",
                                "取消"))
                            {
                                List<ESAssetLibraryConsumer> consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>()
                                    ?.Where(item => item != null)
                                    .ToList() ?? new List<ESAssetLibraryConsumer>();
                                ESCodeModuleEditorIntegration.PrepareConsumerReleaseCode(
                                    consumers,
                                    ESAssetPipelineIO.PlatformName);
                                Debug.Log("Consumer 代码包准备完成。");
                            }
                            else
                            {
                                Debug.LogWarning("已取消 Consumer 代码包准备。");
                            }
                        }, key: ResourcePipelineTaskKey);
                    }
                }
            }

            public void Click_ALL()
            {
                bool pipelineBusy = IsResourcePipelineBusy();
                bool stageAllowed = pipelineStageStateAvailable
                    && currentPipelineStageState.BuildExists
                    && currentPipelineStageState.ConsumerReleasePrepared;
                using (new EditorGUI.DisabledScope(pipelineBusy || !stageAllowed))
                {
                    if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "4. 发布资源包", GUILayout.Height(PipelineActionButtonHeight)))
                    {
                        ESEditorHandle.AddSimpleHandleTask(() =>
                        {
                            if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始发布资源包", "只发布已经校验的暂存产物，并在最后写入根发布清单。", "开始", "取消"))
                            {
                                ESAssetBundlePublisher.Publish();
                            }
                            else
                            {
                                Debug.LogWarning("放弃-<一键完成>");
                            }
                        }, key: ResourcePipelineTaskKey);
                    }
                }
            }

            public void Click_RemotePublish()
            {
                bool pipelineBusy = IsResourcePipelineBusy();
                bool stageAllowed = pipelineStageStateAvailable && currentPipelineStageState.PublishPassed;
                using (new EditorGUI.DisabledScope(pipelineBusy || !stageAllowed))
                {
                    if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "5. 打开远端发布工具", GUILayout.Height(PipelineActionButtonHeight)))
                        ESAssetReleaseUploadWindow.Open();
                }
            }

            private void DrawFolderPaths()
            {
                SirenixEditorGUI.BeginBox();
                SimpleToolsPanelUtility.DrawSectionTitle(
                    "文件夹",
                    "资源库根目录、远端地址与发布管线目录。");
                DrawResSettingProperty("Path_Net", "服务器网络路径");
                DrawResSettingProperty("Path_AssetLibraryFolder", "默认资源库放置文件夹");
                DrawResSettingProperty("Path_ABHelperCodeGen", "AB 帮助代码生成文件夹");
                DrawResSettingProperty("GlobalExcludedFolderPaths", "全局排除文件夹");
                DrawResSettingProperty("Path_Sub_DownloadRelative_", "下载持久相对路径");
                ESGlobalResSetting.Instance?.DrawGeneratedFolderShortcuts();
                SirenixEditorGUI.EndBox();
            }

        }
    }
}
