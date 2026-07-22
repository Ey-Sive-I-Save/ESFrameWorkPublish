using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace ES
{
    public class ESAssetPackageBakeWindowLifecycleInitializer : EditorInvoker_Level2
    {
        public override void InitInvoke()
        {
            ESAssetPackagePreviewWorkflow.RegisterLifecycle();
        }
    }

    public class ESAssetPackageBakeWindow : ESMenuTreeWindowAB<ESAssetPackageBakeWindow>
    {
        internal const string CodeVersion = "ESAssetBakePreview_20260721_0310_PreviewWorkflow";
        private const string PageNameHome = "资产包分离";
        private const string PageNameCurrent = "当前分离配置";
        private const string PrefKeySelectedBakeGuid = "ES.AssetPackageBakeWindow.SelectedBakeGuid";
        private const string PrefKeyLastPageKind = "ES.AssetPackageBakeWindow.LastPageKind";
        private const string PrefKeyLastCategory = "ES.AssetPackageBakeWindow.LastCategory";
        private const string PageKindHome = "Home";
        private const string PageKindIndex = "Index";
        private const string PageKindCategory = "Category";

        [NonSerialized] public Page_AssetPackageBakeHome homePage;
        private static ESAssetPackageBakeData selectedBake;

        [MenuItem(MenuItemPathDefine.RESOURCE_PIPELINE_PATH + "资产包分离窗口", false, 20)]
        public static void TryOpenWindow()
        {
            OpenWindow();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 资产包分离 [" + CodeVersion + "]", "按资源包分离数据管理资产复制、分类预览和使用选择");
        }

        internal void ReleaseInstancePreviewResources()
        {
            if (MenuTree == null)
                return;

            foreach (OdinMenuItem item in MenuTree.EnumerateTree())
            {
                if (item?.Value is Page_AssetPackageBakeCategory categoryPage)
                    categoryPage.ReleasePreviewResources();
            }
        }

        public static void SelectBake(ESAssetPackageBakeData bake, bool refreshWindow)
        {
            selectedBake = bake;
            SaveSelectedBakeGuid(bake);
            if (refreshWindow)
                ES_RefreshWindow();
        }

        public override void ESWindow_OnOpen()
        {
            LoadSelectedBakeFromPrefs();
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            LoadSelectedBakeFromPrefs();
            base.ES_OnBuildMenuTree(tree);

            QuickBuildRootMenu(tree, PageNameHome, ref homePage, SdfIconType.Box);
            homePage.selectedBake = selectedBake;

            if (selectedBake == null)
                return;

            RegisterAndAddPage(tree, PageNameCurrent + "/总览", new Page_AssetPackageBakeIndex(selectedBake), SdfIconType.Grid);

            foreach (ESAssetPackageCategory category in Enum.GetValues(typeof(ESAssetPackageCategory)))
            {
                int count = selectedBake.records != null ? selectedBake.records.Count(r => r != null && r.category == category) : 0;
                if (count <= 0)
                    continue;

                string menuName = $"{PageNameCurrent}/{GetCategoryDisplayName(category)} ({count})";
                RegisterAndAddPage(tree, menuName, new Page_AssetPackageBakeCategory(selectedBake, category), GetCategoryIcon(category));
            }

            ScheduleRestoreMenuSelection();
        }

        protected override void DrawEditors()
        {
            float rawPageWidth = position.width - MenuWidth - 36f;
            if (float.IsNaN(rawPageWidth) || float.IsInfinity(rawPageWidth))
                rawPageWidth = 1120f;
            float pageWidth = Mathf.Clamp(rawPageWidth, 760f, 1380f);
            if (MenuTree?.Selection?.SelectedValue is Page_AssetPackageBakeHome home)
            {
                SaveCurrentPage(PageKindHome, null);
                home.DrawHomePage(pageWidth);
                return;
            }

            if (MenuTree?.Selection?.SelectedValue is Page_AssetPackageBakeCategory categoryPage)
            {
                SaveCurrentPage(PageKindCategory, categoryPage.category);
                categoryPage.DrawPreviewPage(pageWidth);
                return;
            }

            if (MenuTree?.Selection?.SelectedValue is Page_AssetPackageBakeIndex indexPage)
            {
                SaveCurrentPage(PageKindIndex, null);
                indexPage.DrawIndexPage(pageWidth);
                return;
            }

            base.DrawEditors();
        }

        public override void ES_LoadData()
        {
            LoadSelectedBakeFromPrefs();
        }

        public override void ES_SaveData()
        {
            SaveSelectedBakeGuid(selectedBake);
            if (MenuTree?.Selection?.SelectedValue is Page_AssetPackageBakeCategory categoryPage)
                SaveCurrentPage(PageKindCategory, categoryPage.category);
            else if (MenuTree?.Selection?.SelectedValue is Page_AssetPackageBakeIndex)
                SaveCurrentPage(PageKindIndex, null);
            else if (MenuTree?.Selection?.SelectedValue is Page_AssetPackageBakeHome)
                SaveCurrentPage(PageKindHome, null);
        }

        private static void LoadSelectedBakeFromPrefs()
        {
            if (selectedBake != null)
                return;

            string guid = EditorPrefs.GetString(PrefKeySelectedBakeGuid, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                selectedBake = AssetDatabase.LoadAssetAtPath<ESAssetPackageBakeData>(path);
        }

        private static void SaveSelectedBakeGuid(ESAssetPackageBakeData bake)
        {
            if (bake == null)
                return;

            string path = AssetDatabase.GetAssetPath(bake);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                EditorPrefs.SetString(PrefKeySelectedBakeGuid, guid);
        }

        private static void SaveCurrentPage(string pageKind, ESAssetPackageCategory? category)
        {
            EditorPrefs.SetString(PrefKeyLastPageKind, pageKind ?? PageKindHome);
            if (category.HasValue)
                EditorPrefs.SetInt(PrefKeyLastCategory, (int)category.Value);
        }

        private void ScheduleRestoreMenuSelection()
        {
            string pageKind = EditorPrefs.GetString(PrefKeyLastPageKind, PageKindHome);
            int categoryValue = EditorPrefs.GetInt(PrefKeyLastCategory, -1);
            EditorApplication.delayCall += () =>
            {
                if (UsingWindow != this || MenuTree == null)
                    return;

                OdinMenuItem item = null;
                if (pageKind == PageKindIndex)
                {
                    MenuItems.TryGetValue(PageNameCurrent + "/总览", out item);
                }
                else if (pageKind == PageKindCategory && Enum.IsDefined(typeof(ESAssetPackageCategory), categoryValue))
                {
                    string prefix = PageNameCurrent + "/" + GetCategoryDisplayName((ESAssetPackageCategory)categoryValue);
                    item = MenuItems.FirstOrDefault(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal)).Value;
                }
                else
                {
                    MenuItems.TryGetValue(PageNameHome, out item);
                }

                if (item == null)
                    return;

                SetMenuItemExpandedRecursive(item, true);
                MenuTree.Selection.Clear();
                MenuTree.Selection.Add(item);
                Repaint();
            };
        }

        private static void SetMenuItemExpandedRecursive(OdinMenuItem item, bool expanded)
        {
            OdinMenuItem current = item;
            while (current != null)
            {
                SetMenuItemExpanded(current, expanded);
                current = GetParentMenuItem(current);
            }
        }

        private static OdinMenuItem GetParentMenuItem(OdinMenuItem item)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            return item?.GetType().GetProperty("Parent", flags)?.GetValue(item, null) as OdinMenuItem;
        }

        private static void SetMenuItemExpanded(OdinMenuItem item, bool expanded)
        {
            if (item == null)
                return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = item.GetType();
            PropertyInfo property = type.GetProperty("Toggled", flags)
                ?? type.GetProperty("IsExpanded", flags)
                ?? type.GetProperty("Expanded", flags);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
            {
                property.SetValue(item, expanded);
                return;
            }

            FieldInfo field = type.GetField("Toggled", flags)
                ?? type.GetField("IsExpanded", flags)
                ?? type.GetField("Expanded", flags);
            if (field != null && field.FieldType == typeof(bool))
                field.SetValue(item, expanded);
        }

        private static SdfIconType GetCategoryIcon(ESAssetPackageCategory category)
        {
            switch (category)
            {
                case ESAssetPackageCategory.Prefab: return SdfIconType.Box;
                case ESAssetPackageCategory.Scene: return SdfIconType.Map;
                case ESAssetPackageCategory.Material: return SdfIconType.Palette;
                case ESAssetPackageCategory.Texture: return SdfIconType.Image;
                case ESAssetPackageCategory.Model: return SdfIconType.Grid;
                case ESAssetPackageCategory.Audio: return SdfIconType.Play;
                case ESAssetPackageCategory.Animation: return SdfIconType.Play;
                case ESAssetPackageCategory.ScriptableObject: return SdfIconType.FileEarmarkCode;
                case ESAssetPackageCategory.Shader: return SdfIconType.Lightbulb;
                case ESAssetPackageCategory.Font: return SdfIconType.FileEarmarkCode;
                case ESAssetPackageCategory.Video: return SdfIconType.Play;
                default: return SdfIconType.Search;
            }
        }

        public static string GetCategoryDisplayName(ESAssetPackageCategory category)
        {
            switch (category)
            {
                case ESAssetPackageCategory.Prefab: return "预制体";
                case ESAssetPackageCategory.Scene: return "场景";
                case ESAssetPackageCategory.Material: return "材质";
                case ESAssetPackageCategory.Texture: return "贴图";
                case ESAssetPackageCategory.Model: return "模型";
                case ESAssetPackageCategory.Audio: return "音频";
                case ESAssetPackageCategory.Animation: return "动画";
                case ESAssetPackageCategory.ScriptableObject: return "SO资产";
                case ESAssetPackageCategory.Shader: return "Shader";
                case ESAssetPackageCategory.Font: return "字体";
                case ESAssetPackageCategory.Video: return "视频";
                default: return "其他";
            }
        }
    }

    internal enum ESAssetPackageSortMode
    {
        [InspectorName("名称")] Name,
        [InspectorName("路径")] Path,
        [InspectorName("类型")] Type,
        [InspectorName("大小从小到大")] SizeSmallToLarge,
        [InspectorName("大小从大到小")] SizeLargeToSmall,
        [InspectorName("已使用优先")] UsedFirst,
        [InspectorName("未使用优先")] UnusedFirst,
        [InspectorName("动作分类")] AnimationClass,
        [InspectorName("动作时长从短到长")] AnimationLengthShortToLong,
        [InspectorName("动作时长从长到短")] AnimationLengthLongToShort
    }

    internal enum ESAssetPackageCopyFilter
    {
        All,
        Used,
        Copied,
        NotCopied,
        MissingTarget
    }

    internal enum ESAssetPackageAnimationClass
    {
        [InspectorName("全部")] All,
        [InspectorName("待机&等待")] Idle,
        [InspectorName("步行")] Walk,
        [InspectorName("慢跑&奔跑")] JogRun,
        [InspectorName("移动开始")] MoveStart,
        [InspectorName("移动停止")] MoveStop,
        [InspectorName("移动结束")] MoveEnd,
        [InspectorName("原地转向")] Turn,
        [InspectorName("弧线移动")] ArcMove,
        [InspectorName("冲刺")] Dash,
        [InspectorName("加速&Boost")] Boost,
        [InspectorName("蓄力&强冲")] ChargePower,
        [InspectorName("闪避&侧移")] EscapeDodge,
        [InspectorName("翻滚")] Roll,
        [InspectorName("滑行")] Slide,
        [InspectorName("跳跃&空中")] JumpAir,
        [InspectorName("落地")] Land,
        [InspectorName("墙面&攀爬")] WallParkour,
        [InspectorName("体操&翻越")] TumbleAcrobat,
        [InspectorName("近战攻击")] AttackMelee,
        [InspectorName("空中攻击")] AttackAir,
        [InspectorName("受击&硬直")] DamageHit,
        [InspectorName("死亡")] Death,
        [InspectorName("武器动作")] Weapon,
        [InspectorName("出生/出现")] Spawn,
        [InspectorName("其他")] Other
    }

    [Serializable]
    public class Page_AssetPackageBakeHome : ESWindowPageBase
    {
        [HideInInspector]
        public string title = "资产包分离 / 烘焙数据";

        [HideInInspector]
        public string readMe = "先选择已有烘焙，或新建一个烘焙并指向导入资源包文件夹。烘焙后左侧子菜单按资产分类显示。";

        [HideInInspector]
        public ESAssetPackageBakeData selectedBake;

        [HideInInspector]
        public string newBakeName = "新资产包";

        [HideInInspector]
        public string newBakeTargetFolder = "Assets";

        [HideInInspector]
        public bool bakeImmediately = true;
        [NonSerialized] private Vector2 bakeListScroll;
        [NonSerialized] private bool showBakeList = true;

        public void DrawHomePage(float pageWidth)
        {
            pageWidth = Mathf.Max(760f, pageWidth);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(pageWidth), GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField(title, new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 });
                EditorGUILayout.LabelField(readMe, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(8);

                DrawBakeChooser(pageWidth);
                EditorGUILayout.Space(8);
                DrawCreateBakePanel(pageWidth);
            }
        }

        public void CreateBake()
        {
            CreateBakeInternal();
        }

        private void CreateBakeInternal()
        {
            string parent = GetBakeParentFolder();
            EnsureAssetFolder(parent);

            string assetName = string.IsNullOrWhiteSpace(newBakeName) ? "资产包烘焙数据" : newBakeName.Trim();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{parent}/{SanitizeFileName(assetName)}.asset");
            var bake = ScriptableObject.CreateInstance<ESAssetPackageBakeData>();
            bake.displayName = assetName;
            bake.exportConfigName = assetName;
            bake.targetFolderPath = NormalizeAssetPath(newBakeTargetFolder);
            bake.exportRootPath = "Assets/_ESAssetPackageExport/" + SanitizeFileName(assetName);
            bake.EnsureCategoryFolderSettings();
            AssetDatabase.CreateAsset(bake, path);

            if (bakeImmediately)
                ESAssetPackageBakeUtility.Bake(bake);
            else
                EditorUtility.SetDirty(bake);

            AssetDatabase.SaveAssets();
            Selection.activeObject = bake;
            EditorGUIUtility.PingObject(bake);
            ESAssetPackageBakeWindow.SelectBake(bake, true);
        }

        public void RefreshWindow()
        {
            ESAssetPackageBakeWindow.SelectBake(selectedBake, true);
        }

        public void PingCurrentBake()
        {
            EditorGUIUtility.PingObject(selectedBake);
            Selection.activeObject = selectedBake;
        }

        private void DrawBakeChooser(float pageWidth)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(pageWidth)))
            {
                EditorGUILayout.LabelField("CODE_VERSION: " + ESAssetPackageBakeWindow.CodeVersion, EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    showBakeList = EditorGUILayout.Foldout(showBakeList, "已有烘焙资产", true);
                    GUILayout.FlexibleSpace();
                    if (selectedBake != null)
                        EditorGUILayout.LabelField("当前: " + GetBakeTitle(selectedBake), EditorStyles.miniBoldLabel, GUILayout.Width(260));
                    if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(58)))
                        RefreshWindow();
                    using (new EditorGUI.DisabledScope(selectedBake == null))
                    {
                        if (GUILayout.Button("Ping 当前", EditorStyles.toolbarButton, GUILayout.Width(72)))
                            PingCurrentBake();
                    }
                }

                if (!showBakeList)
                    return;

                List<ESAssetPackageBakeData> bakes = FindAllBakes();
                float listHeight = Mathf.Clamp(44f + bakes.Count * 34f, 100f, 300f);
                bakeListScroll = EditorGUILayout.BeginScrollView(bakeListScroll, GUILayout.Height(listHeight));
                for (int i = 0; i < bakes.Count; i++)
                    DrawBakeRow(bakes[i], pageWidth - 24f);
                if (bakes.Count == 0)
                    EditorGUILayout.HelpBox("还没有资产包烘焙数据。", MessageType.Info);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawBakeRow(ESAssetPackageBakeData bake, float rowWidth)
        {
            if (bake == null)
                return;

            Rect rect = GUILayoutUtility.GetRect(rowWidth, 30f, GUILayout.Width(rowWidth), GUILayout.Height(30f));
            bool current = selectedBake == bake;
            EditorGUI.DrawRect(rect, current ? new Color(0.16f, 0.28f, 0.42f, 1f) : new Color(0.10f, 0.105f, 0.115f, 1f));

            Rect nameRect = new Rect(rect.x + 8f, rect.y + 6f, Mathf.Max(180f, rect.width * 0.32f), 18f);
            Rect countRect = new Rect(nameRect.xMax + 8f, rect.y + 6f, 92f, 18f);
            Rect folderRect = new Rect(countRect.xMax + 8f, rect.y + 6f, Mathf.Max(140f, rect.width - countRect.xMax - 158f), 18f);
            Rect selectRect = new Rect(rect.xMax - 142f, rect.y + 5f, 62f, 20f);
            Rect pingRect = new Rect(rect.xMax - 74f, rect.y + 5f, 62f, 20f);

            GUI.Label(nameRect, GetBakeTitle(bake), EditorStyles.boldLabel);
            GUI.Label(countRect, bake.totalAssetCount + " 个", EditorStyles.miniLabel);
            GUI.Label(folderRect, ShortFolderLabel(bake.targetFolderPath), EditorStyles.miniLabel);
            if (GUI.Button(selectRect, current ? "已选择" : "选择"))
            {
                selectedBake = bake;
                ESAssetPackageBakeWindow.SelectBake(bake, true);
            }
            if (GUI.Button(pingRect, "Ping"))
            {
                Selection.activeObject = bake;
                EditorGUIUtility.PingObject(bake);
            }
        }

        private void DrawCreateBakePanel(float pageWidth)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(pageWidth)))
            {
                EditorGUILayout.LabelField("快捷新建", EditorStyles.boldLabel);
                newBakeName = EditorGUILayout.TextField("烘焙名称", newBakeName);
                using (new EditorGUILayout.HorizontalScope())
                {
                    newBakeTargetFolder = EditorGUILayout.TextField("目标资源包文件夹", newBakeTargetFolder);
                    if (GUILayout.Button("选择", GUILayout.Width(58)))
                    {
                        string selected = EditorUtility.OpenFolderPanel("选择资源包文件夹", Application.dataPath, string.Empty);
                        if (!string.IsNullOrEmpty(selected) && selected.Replace("\\", "/").StartsWith(Application.dataPath.Replace("\\", "/"), StringComparison.OrdinalIgnoreCase))
                            newBakeTargetFolder = "Assets" + selected.Replace("\\", "/").Substring(Application.dataPath.Replace("\\", "/").Length);
                    }
                }
                bakeImmediately = EditorGUILayout.ToggleLeft("创建后立即烘焙", bakeImmediately);
                if (GUILayout.Button("新建资产包烘焙", GUILayout.Height(32)))
                    CreateBakeInternal();
            }
        }

        private void OnSelectedBakeChanged()
        {
            ESAssetPackageBakeWindow.SelectBake(selectedBake, true);
        }

        private IEnumerable<ValueDropdownItem<ESAssetPackageBakeData>> GetBakeDropdown()
        {
            yield return new ValueDropdownItem<ESAssetPackageBakeData>("未选择", null);
            foreach (var bake in FindAllBakes())
            {
                string label = $"{GetBakeTitle(bake)}  |  {bake.totalAssetCount} 个  |  {ShortFolderLabel(bake.targetFolderPath)}";
                yield return new ValueDropdownItem<ESAssetPackageBakeData>(label, bake);
            }
        }

        private static string GetBakeTitle(ESAssetPackageBakeData bake)
        {
            if (bake == null)
                return "<空>";

            return string.IsNullOrWhiteSpace(bake.displayName) ? bake.name : bake.displayName;
        }

        private static string ShortFolderLabel(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            if (string.IsNullOrEmpty(assetPath))
                return "<无目标文件夹>";

            string name = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(name))
                return assetPath;

            string parent = Path.GetFileName(Path.GetDirectoryName(assetPath)?.Replace("\\", "/") ?? string.Empty);
            return string.IsNullOrEmpty(parent) ? name : parent + "/" + name;
        }

        private static List<ESAssetPackageBakeData> FindAllBakes()
        {
            var result = ESEditorSO.SOS.GetNewGroupOfType<ESAssetPackageBakeData>() ?? new List<ESAssetPackageBakeData>(0);

            result.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static string GetBakeParentFolder()
        {
            string path = ESGlobalEditorDefaultConfi.Instance != null
                ? ESGlobalEditorDefaultConfi.Instance.Path_AssetPackageBakeParent
                : string.Empty;

            return string.IsNullOrWhiteSpace(path) ? "Assets/ESNormalAssets/Data/AssetPackageBake" : NormalizeAssetPath(path);
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder);
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrWhiteSpace(name) ? "AssetPackageBake" : name;
        }
    }

    [Serializable]
    public class Page_AssetPackageBakeIndex : ESWindowPageBase
    {
        [HideInInspector]
        public ESAssetPackageBakeData bake;
        [NonSerialized] private Vector2 scroll;

        public Page_AssetPackageBakeIndex(ESAssetPackageBakeData bake)
        {
            this.bake = bake;
        }

        public void DrawIndexPage(float pageWidth)
        {
            pageWidth = Mathf.Max(760f, pageWidth);
            if (bake == null)
            {
                EditorGUILayout.HelpBox("未选择烘焙数据。", MessageType.Warning);
                return;
            }

            bake.EnsureCategoryFolderSettings();

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Width(pageWidth), GUILayout.ExpandHeight(true));
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(pageWidth - 8f)))
            {
                DrawHeader(pageWidth - 8f);
                EditorGUILayout.Space(8);
                DrawPathConfig(pageWidth - 8f);
                EditorGUILayout.Space(8);
                DrawExportConfig(pageWidth - 8f);
                EditorGUILayout.Space(8);
                DrawCategorySummary(pageWidth - 8f);
                EditorGUILayout.Space(8);
                DrawExportSummary(pageWidth - 8f);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(bake.displayName) ? bake.name : bake.displayName, new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 });
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"总数 {bake.totalAssetCount} | 已使用 {bake.selectedUseCount} | 最后烘焙 {SafeText(bake.lastBakeTime)}", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("保存配置", GUILayout.Width(76)))
                        SaveBake();
                    if (GUILayout.Button("重新烘焙", GUILayout.Width(76)))
                    {
                        ESAssetPackageBakeUtility.Bake(bake);
                        ESAssetPackageBakeWindow.SelectBake(bake, true);
                    }
                    if (GUILayout.Button("复制勾选资产", GUILayout.Width(96)))
                    {
                        ESAssetPackageBakeUtility.ExportSelectedAssetsByCategory(bake);
                        ESAssetPackageBakeWindow.SelectBake(bake, true);
                    }
                    using (new EditorGUI.DisabledScope(bake.exportSessions == null || bake.exportSessions.Count == 0))
                    {
                        if (GUILayout.Button("回退最近导出", GUILayout.Width(98)))
                        {
                            ESAssetPackageBakeUtility.RollbackLastExport(bake);
                            ESAssetPackageBakeWindow.SelectBake(bake, true);
                        }
                    }
                }
            }
        }

        private void DrawPathConfig(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField("基础配置", EditorStyles.boldLabel);
                bake.displayName = EditorGUILayout.TextField("显示名称", bake.displayName);
                bake.exportConfigName = EditorGUILayout.TextField("配置名称", string.IsNullOrWhiteSpace(bake.exportConfigName) ? bake.displayName : bake.exportConfigName);
                DrawFolderPathRow("目标资源包", ref bake.targetFolderPath);
                bake.includeSubFolders = EditorGUILayout.ToggleLeft("包含子文件夹", bake.includeSubFolders);
            }
        }

        private void DrawExportConfig(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField("导出配置", EditorStyles.boldLabel);
                DrawFolderPathRow("导出根目录", ref bake.exportRootPath);
                bake.exportFileNamePrefix = EditorGUILayout.TextField("导出文件名前缀", string.IsNullOrWhiteSpace(bake.exportFileNamePrefix) ? "ES选用_" : bake.exportFileNamePrefix);
                bake.previewFallbackMaterial = (Material)EditorGUILayout.ObjectField("坏材质预览兜底", bake.previewFallbackMaterial, typeof(Material), false);
                StateMachineConfig stateMachineConfig = StateMachineConfig.Instance;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("全局预览模型", stateMachineConfig != null ? stateMachineConfig.previewModel : null, typeof(UnityEngine.Object), false);
                    EditorGUILayout.ObjectField("全局预览 Avatar", stateMachineConfig != null ? stateMachineConfig.previewAvatar : null, typeof(Avatar), false);
                    EditorGUILayout.ObjectField("全局预览兜底材质", stateMachineConfig != null ? stateMachineConfig.previewFallbackMaterial : null, typeof(Material), false);
                }
                bake.animationPreviewModel = (GameObject)EditorGUILayout.ObjectField("本烘焙覆盖模型", bake.animationPreviewModel, typeof(GameObject), false);
                bake.animationPreviewAvatar = (Avatar)EditorGUILayout.ObjectField("本烘焙覆盖 Avatar", bake.animationPreviewAvatar, typeof(Avatar), false);
                bake.exportDependencies = EditorGUILayout.ToggleLeft("导出依赖资源", bake.exportDependencies);
                bake.remapExportedGuids = EditorGUILayout.ToggleLeft("重映射导出内部 GUID", bake.remapExportedGuids);
                bake.overwriteExistingExport = EditorGUILayout.ToggleLeft("重复导出时覆盖旧目标", bake.overwriteExistingExport);

                DrawCategoryFolderConfig();

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(bake.exportRootPath)))
                    {
                        if (GUILayout.Button("Ping 导出目录", GUILayout.Width(96)))
                            PingFolder(bake.exportRootPath);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawCategorySummary(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField("分类统计", EditorStyles.boldLabel);
                if (bake.categoryCounts == null || bake.categoryCounts.Count == 0)
                {
                    EditorGUILayout.HelpBox("暂无分类统计，请先烘焙。", MessageType.Info);
                    return;
                }

                foreach (ESAssetPackageCategory category in Enum.GetValues(typeof(ESAssetPackageCategory)))
                {
                    int count = bake.categoryCounts.TryGetValue(category, out int value) ? value : 0;
                    if (count <= 0)
                        continue;

                    int selected = bake.records != null ? bake.records.Count(r => r != null && r.category == category && r.selectedForUse) : 0;
                    EditorGUILayout.LabelField(ESAssetPackageBakeWindow.GetCategoryDisplayName(category), $"总数 {count} | 已使用 {selected}");
                }
            }
        }

        private void DrawExportSummary(float width)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width)))
            {
                EditorGUILayout.LabelField("导出链路", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("最近导出", string.IsNullOrEmpty(bake.lastExportTime) ? "无" : $"{bake.lastExportTime} | 总数 {bake.lastExportAssetCount} | 依赖 {bake.lastExportDependencyCount}");
                EditorGUILayout.LabelField("链路数量", bake.exportLinks != null ? bake.exportLinks.Count.ToString() : "0");
                EditorGUILayout.LabelField("链路字典", bake.exportChainBySourceGuid != null ? bake.exportChainBySourceGuid.Count.ToString() : "0");
                EditorGUILayout.LabelField("会话数量", bake.exportSessions != null ? bake.exportSessions.Count.ToString() : "0");

                if (bake.exportSessions != null && bake.exportSessions.Count > 0)
                {
                    ESAssetPackageExportSession last = bake.exportSessions[bake.exportSessions.Count - 1];
                    EditorGUILayout.LabelField("最近会话", $"{last.sessionId} | 新增 {last.createdCount} | 更新 {last.updatedCount} | 失败 {last.errorCount}");
                }
            }
        }

        private static void DrawFolderPathRow(string label, ref string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                path = NormalizeAssetPath(EditorGUILayout.TextField(label, path));
                if (GUILayout.Button("选择", GUILayout.Width(54)))
                {
                    string startPath = AssetPathToFullPath(string.IsNullOrWhiteSpace(path) ? "Assets" : path);
                    string selected = EditorUtility.OpenFolderPanel("选择" + label, Directory.Exists(startPath) ? startPath : Application.dataPath, string.Empty);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        string assetPath = FullPathToAssetPath(selected);
                        if (!string.IsNullOrEmpty(assetPath))
                            path = assetPath;
                    }
                }
            }
        }

        private void SaveBake()
        {
            bake.EnsureCategoryFolderSettings();
            bake.RebuildStats();
            EditorUtility.SetDirty(bake);
            AssetDatabase.SaveAssets();
            ESAssetPackageBakeWindow.UsingWindow?.Repaint();
        }

        private void DrawCategoryFolderConfig()
        {
            bake.EnsureCategoryFolderSettings();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("按类型自动分目录", EditorStyles.miniBoldLabel);

            foreach (ESAssetPackageCategory category in Enum.GetValues(typeof(ESAssetPackageCategory)))
            {
                ESAssetPackageCategoryFolderSetting setting = GetCategoryFolderSetting(category);
                if (setting == null)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(ESAssetPackageBakeWindow.GetCategoryDisplayName(category), GUILayout.Width(90));
                    setting.folderName = EditorGUILayout.TextField(setting.folderName);
                    if (GUILayout.Button("默认", GUILayout.Width(48)))
                        setting.folderName = ESAssetPackageBakeData.GetDefaultExportSubFolder(category);
                }
            }
        }

        private ESAssetPackageCategoryFolderSetting GetCategoryFolderSetting(ESAssetPackageCategory category)
        {
            bake.EnsureCategoryFolderSettings();
            return bake.categoryFolderSettings.FirstOrDefault(x => x != null && x.category == category);
        }

        private static void PingFolder(string path)
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NormalizeAssetPath(path));
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "无" : value;
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/").TrimEnd('/');
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            assetPath = NormalizeAssetPath(assetPath);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot) ? assetPath : Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string FullPathToAssetPath(string fullPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(fullPath))
                return string.Empty;

            string normalizedFullPath = Path.GetFullPath(fullPath).Replace("\\", "/");
            string normalizedProjectRoot = Path.GetFullPath(projectRoot).Replace("\\", "/").TrimEnd('/');
            if (!normalizedFullPath.StartsWith(normalizedProjectRoot + "/", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            return normalizedFullPath.Substring(normalizedProjectRoot.Length + 1);
        }
    }

    [Serializable]
    public class Page_AssetPackageBakeCategory : ESWindowPageBase
    {
        private const int MinPreviewSize = 64;
        private const int MaxPreviewSize = 220;
        private const string PrefPrefix = "ES.AssetPackageBakeWindow.Category.";
        private const string PrefKeyRecordPreviewModelGuid = "ES.AssetPackageRecordPreviewWindow.ModelGuid";
        private const int GridAnimationPreviewHardLimit = 48;
        private const double GridAnimationPlayerKeepAliveSeconds = 30d;
        private static readonly bool GridAnimationPreviewEnabled = true;

        [NonSerialized] private Vector2 gridScroll;
        [NonSerialized] private int currentPage;
        [NonSerialized] private string searchText = string.Empty;
        [NonSerialized] private bool onlyUsed;
        [NonSerialized] private ESAssetPackageCopyFilter copyFilter = ESAssetPackageCopyFilter.All;
        [NonSerialized] private ESAssetPackageSortMode assetSortMode = ESAssetPackageSortMode.Name;
        [NonSerialized] private ESAssetPackageAnimationClass animationClassFilter = ESAssetPackageAnimationClass.All;
        [NonSerialized] private int previewSize = 150;
        [NonSerialized] private int columnsPerRow = 6;
        [NonSerialized] private int itemsPerPage = 36;
        [NonSerialized] private ESAssetPackageBakeRecord selectedRecord;
        [NonSerialized] private bool stateLoaded;
        [NonSerialized] private bool gridAnimationSlowPreview = false;
        [NonSerialized] private float gridAnimationSlowSpeed = 0.5f;
        [NonSerialized] private int gridAnimationMaxActive = 24;
        [NonSerialized] private int gridAnimationViewIndex = 0;
        [NonSerialized] private string gridAnimationPriorityScope = string.Empty;
        [NonSerialized] private int gridAnimationPriorityGeneration;
        [NonSerialized] private readonly Dictionary<string, ESAssetPackageAnimationPreviewPlayer> gridAnimationPlayers = new Dictionary<string, ESAssetPackageAnimationPreviewPlayer>();
        [NonSerialized] private readonly Dictionary<string, double> gridAnimationLastSeen = new Dictionary<string, double>();
        [NonSerialized] private readonly HashSet<string> gridAnimationVisibleKeys = new HashSet<string>();

        [HideInInspector]
        public ESAssetPackageBakeData bake;

        [HideInInspector]
        public ESAssetPackageCategory category;

        private string Summary => bake == null
            ? "未选择烘焙"
            : BuildSummary();

        private List<ESAssetPackageBakeRecord> CategoryRecords
        {
            get
            {
                if (bake == null || bake.records == null)
                    return new List<ESAssetPackageBakeRecord>();

                return bake.records.Where(r => r != null && r.category == category).ToList();
            }
        }

        private List<ESAssetPackageBakeRecord> FilteredRecords
        {
            get
            {
                IEnumerable<ESAssetPackageBakeRecord> query = CategoryRecords;

                query = ApplyCopyFilter(query);

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    string key = searchText.Trim();
                    query = query.Where(r =>
                        ContainsIgnoreCase(r.assetName, key) ||
                        ContainsIgnoreCase(r.assetPath, key) ||
                        ContainsIgnoreCase(r.typeName, key) ||
                        ContainsIgnoreCase(GetAnimationClassDisplayName(ClassifyAnimationRecord(r)), key));
                }

                if (category == ESAssetPackageCategory.Animation && animationClassFilter != ESAssetPackageAnimationClass.All)
                    query = query.Where(r => ClassifyAnimationRecord(r) == animationClassFilter);

                return SortRecords(query).ToList();
            }
        }

        private string BuildSummary()
        {
            List<ESAssetPackageBakeRecord> records = CategoryRecords;
            int used = records.Count(r => r != null && r.selectedForUse);
            int copied = records.Count(IsCopiedRecord);
            int missing = records.Count(HasMissingCopyTarget);
            return $"{ESAssetPackageBakeWindow.GetCategoryDisplayName(category)} | 总数 {records.Count} | 当前筛选 {FilteredRecords.Count} | 勾选 {used} | 已复制 {copied} | 目标丢失 {missing}";
        }

        private IEnumerable<ESAssetPackageBakeRecord> ApplyCopyFilter(IEnumerable<ESAssetPackageBakeRecord> query)
        {
            switch (copyFilter)
            {
                case ESAssetPackageCopyFilter.Used:
                    return query.Where(r => r != null && r.selectedForUse);
                case ESAssetPackageCopyFilter.Copied:
                    return query.Where(IsCopiedRecord);
                case ESAssetPackageCopyFilter.NotCopied:
                    return query.Where(r => r != null && FindExportLink(r) == null);
                case ESAssetPackageCopyFilter.MissingTarget:
                    return query.Where(HasMissingCopyTarget);
                default:
                    return query;
            }
        }

        private bool IsCopiedRecord(ESAssetPackageBakeRecord record)
        {
            ESAssetPackageExportLink link = FindExportLink(record);
            return link != null && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(link.targetAssetPath) != null;
        }

        private bool HasMissingCopyTarget(ESAssetPackageBakeRecord record)
        {
            ESAssetPackageExportLink link = FindExportLink(record);
            return link != null && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(link.targetAssetPath) == null;
        }

        public Page_AssetPackageBakeCategory(ESAssetPackageBakeData bake, ESAssetPackageCategory category)
        {
            this.bake = bake;
            this.category = category;
            LoadState();
        }

        public void DrawPreviewPage()
        {
            DrawPreviewPage(Mathf.Max(760f, EditorGUIUtility.currentViewWidth));
        }

        public void DrawPreviewPage(float pageWidth)
        {
            if (bake == null)
            {
                EditorGUILayout.HelpBox("未选择烘焙数据。", MessageType.Warning);
                return;
            }

            LoadState();
            pageWidth = Mathf.Max(760f, pageWidth);
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(pageWidth), GUILayout.ExpandHeight(true)))
            {
                DrawToolbar(pageWidth);

                List<ESAssetPackageBakeRecord> records = FilteredRecords;
                int totalPages = Mathf.Max(1, Mathf.CeilToInt(records.Count / (float)Mathf.Max(1, itemsPerPage)));
                currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

                EditorGUILayout.Space(6);
                DrawPreviewGrid(records, totalPages, pageWidth);
            }
        }

        public void MarkAllUsed()
        {
            SetCategoryUse(true);
        }

        public void UnmarkAllUsed()
        {
            SetCategoryUse(false);
        }

        public void SelectUsedInCategory()
        {
            var assets = new List<UnityEngine.Object>();
            foreach (var record in CategoryRecords)
            {
                if (record == null || !record.selectedForUse)
                    continue;

                UnityEngine.Object asset = record.LoadAsset();
                if (asset != null)
                    assets.Add(asset);
            }

            Selection.objects = assets.ToArray();
            if (assets.Count > 0)
                EditorGUIUtility.PingObject(assets[0]);
        }

        public void SaveBake()
        {
            if (bake == null)
                return;

            bake.RebuildStats();
            EditorUtility.SetDirty(bake);
            AssetDatabase.SaveAssets();
        }

        private void SetCategoryUse(bool value)
        {
            if (bake == null || bake.records == null)
                return;

            foreach (var record in bake.records)
            {
                if (record != null && record.category == category)
                    record.selectedForUse = value;
            }
            SaveBake();
        }

        private void DrawCopyFilterButton(string label, ESAssetPackageCopyFilter filter, float width)
        {
            bool selected = copyFilter == filter;
            bool newSelected = GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(width));
            if (newSelected && copyFilter != filter)
            {
                copyFilter = filter;
                onlyUsed = false;
                currentPage = 0;
                SaveState();
            }
        }

        public override void OnPageDisable()
        {
            SuspendGridAnimationPlayers();
        }

        public void ReleasePreviewResources()
        {
            ClearGridAnimationPlayers();
        }

        private void DrawToolbar(float pageWidth)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(pageWidth)))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(Summary, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("本类全用", EditorStyles.toolbarButton, GUILayout.Width(74)))
                        MarkAllUsed();
                    if (GUILayout.Button("本类全不使用", EditorStyles.toolbarButton, GUILayout.Width(90)))
                        UnmarkAllUsed();
                    if (GUILayout.Button("选中已使用", EditorStyles.toolbarButton, GUILayout.Width(86)))
                        SelectUsedInCategory();
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(52)))
                        SaveBake();
                    if (GUILayout.Button("复制勾选资产", EditorStyles.toolbarButton, GUILayout.Width(96)))
                    {
                        ESAssetPackageBakeUtility.ExportSelectedAssetsByCategory(bake);
                        ESAssetPackageBakeWindow.SelectBake(bake, true);
                    }
                    using (new EditorGUI.DisabledScope(bake == null || bake.exportSessions == null || bake.exportSessions.Count == 0))
                    {
                        if (GUILayout.Button("回退导出", EditorStyles.toolbarButton, GUILayout.Width(74)))
                        {
                            ESAssetPackageBakeUtility.RollbackLastExport(bake);
                            ESAssetPackageBakeWindow.SelectBake(bake, true);
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", GUILayout.Width(34));
                    string newSearch = EditorGUILayout.TextField(searchText);
                    if (newSearch != searchText)
                    {
                        searchText = newSearch;
                        currentPage = 0;
                        SaveState();
                    }

                    DrawCopyFilterButton("全部", ESAssetPackageCopyFilter.All, 46);
                    DrawCopyFilterButton("勾选", ESAssetPackageCopyFilter.Used, 46);
                    DrawCopyFilterButton("已复制", ESAssetPackageCopyFilter.Copied, 58);
                    DrawCopyFilterButton("未复制", ESAssetPackageCopyFilter.NotCopied, 58);
                    DrawCopyFilterButton("目标丢失", ESAssetPackageCopyFilter.MissingTarget, 70);


                    if (GUILayout.Button("刷新预览", GUILayout.Width(76)))
                    {
                        ClearGridAnimationPlayers();
                        ESAssetPackagePreviewWorkflow.RefreshStaticPreviewCache(Mathf.Max(256, itemsPerPage * 4));
                        ESAssetPackageBakeWindow.UsingWindow?.Repaint();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("排序", GUILayout.Width(34));
                    ESAssetPackageSortMode newSortMode = (ESAssetPackageSortMode)EditorGUILayout.EnumPopup(assetSortMode, GUILayout.Width(150));
                    if (newSortMode != assetSortMode)
                    {
                        assetSortMode = newSortMode;
                        currentPage = 0;
                        SaveState();
                    }

                    if (category == ESAssetPackageCategory.Animation)
                    {
                        EditorGUILayout.LabelField("动作分类", GUILayout.Width(58));
                        ESAssetPackageAnimationClass newClassFilter = (ESAssetPackageAnimationClass)EditorGUILayout.EnumPopup(animationClassFilter, GUILayout.Width(150));
                        if (newClassFilter != animationClassFilter)
                        {
                            animationClassFilter = newClassFilter;
                            currentPage = 0;
                            SaveState();
                        }
                    }
                }

                if (bake != null && !string.IsNullOrEmpty(bake.lastExportTime))
                    EditorGUILayout.LabelField($"最近导出: {bake.lastExportTime} | 总数 {bake.lastExportAssetCount} | 依赖 {bake.lastExportDependencyCount} | {bake.lastExportRootPath}", EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    int newPreviewSize = EditorGUILayout.IntSlider("缩略图尺寸", previewSize, MinPreviewSize, MaxPreviewSize);
                    int newColumnsPerRow = EditorGUILayout.IntSlider("每行数量", columnsPerRow, 3, 10);
                    int newItemsPerPage = EditorGUILayout.IntSlider("每页数量", itemsPerPage, 12, 300);
                    if (newPreviewSize != previewSize || newColumnsPerRow != columnsPerRow || newItemsPerPage != itemsPerPage)
                    {
                        previewSize = newPreviewSize;
                        columnsPerRow = newColumnsPerRow;
                        itemsPerPage = newItemsPerPage;
                        SaveState();
                    }
                }

                if (category == ESAssetPackageCategory.Animation)
                    DrawGridAnimationPreviewSettings();
            }
        }

        private void DrawGridAnimationPreviewSettings()
        {
            if (!GridAnimationPreviewEnabled)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("网格动画预览已停用：当前 Humanoid 编辑器采样只能可靠得到 Root 位移/旋转，不能作为有效动作格子预览。", EditorStyles.miniLabel);
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newEnabled = GUILayout.Toggle(gridAnimationSlowPreview, "网格慢放", EditorStyles.toolbarButton, GUILayout.Width(78));
                if (newEnabled != gridAnimationSlowPreview)
                {
                    gridAnimationSlowPreview = newEnabled;
                    if (!gridAnimationSlowPreview)
                        ClearGridAnimationPlayers();
                    SaveState();
                }

                using (new EditorGUI.DisabledScope(!gridAnimationSlowPreview))
                {
                    EditorGUILayout.LabelField("慢放速度", GUILayout.Width(58));
                    float newSpeed = GUILayout.HorizontalSlider(gridAnimationSlowSpeed, 0.1f, 1f, GUILayout.Width(120));
                    newSpeed = Mathf.Round(newSpeed * 20f) / 20f;
                    EditorGUILayout.LabelField(newSpeed.ToString("F2") + "x", GUILayout.Width(46));

                    EditorGUILayout.LabelField("同时播放", GUILayout.Width(58));
                    int newMaxActive = EditorGUILayout.IntSlider(gridAnimationMaxActive, 1, GridAnimationPreviewHardLimit, GUILayout.Width(170));

                    if (!Mathf.Approximately(newSpeed, gridAnimationSlowSpeed) || newMaxActive != gridAnimationMaxActive)
                    {
                        gridAnimationSlowSpeed = newSpeed;
                        gridAnimationMaxActive = newMaxActive;
                        SaveState();
                    }
                }

                if (GUILayout.Button("清内存帧", EditorStyles.toolbarButton, GUILayout.Width(78)))
                {
                    ESAssetPackagePreviewWorkflow.ClearGridFrameMemory();
                    ESAssetPackageBakeWindow.UsingWindow?.Repaint();
                }

                if (GUILayout.Button("重生成当前页", EditorStyles.toolbarButton, GUILayout.Width(104)))
                {
                    RebuildVisibleGridAnimationFrames();
                    ESAssetPackageBakeWindow.UsingWindow?.Repaint();
                }

                GUILayout.Space(6f);
                DrawGridAnimationViewButton("正面", 0);
                DrawGridAnimationViewButton("侧面", 1);
                DrawGridAnimationViewButton("背面", 2);

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("帧缓存预览：允许Root位移，当前可见格异步生成", EditorStyles.miniLabel, GUILayout.Width(260));
            }

            string cacheStatus = ESAssetPackagePreviewWorkflow.GetGridFrameLoadingStatus();
            if (!string.IsNullOrEmpty(cacheStatus))
                EditorGUILayout.HelpBox(cacheStatus, MessageType.Info);
        }

        private void RebuildVisibleGridAnimationFrames()
        {
            if (category != ESAssetPackageCategory.Animation)
                return;

            List<ESAssetPackageBakeRecord> records = FilteredRecords;
            int start = Mathf.Clamp(currentPage * Mathf.Max(1, itemsPerPage), 0, Mathf.Max(0, records.Count));
            int end = Mathf.Min(records.Count, start + Mathf.Max(1, itemsPerPage));
            float yaw = GetGridAnimationViewYaw();
            for (int i = start; i < end; i++)
            {
                ESAssetPackageBakeRecord record = records[i];
                UnityEngine.Object asset = record != null ? record.LoadAsset() : null;
                AnimationClip clip = ResolveGridAnimationClip(asset);
                clip = ESAssetPackagePreviewUtility.ResolveVisualMotionClip(clip);
                UnityEngine.Object model = ResolveGridAnimationPreviewModel(asset);
                if (clip == null || model == null)
                    continue;

                ESAssetPackageGridAnimationFrameCache.DeletePersistentFrames(clip, model, yaw);
            }

            ESAssetPackagePreviewWorkflow.ClearGridFrameMemory();
        }

        private void DrawGridAnimationViewButton(string label, int index)
        {
            bool selected = gridAnimationViewIndex == index;
            bool newSelected = GUILayout.Toggle(selected, label, EditorStyles.toolbarButton, GUILayout.Width(48));
            if (!newSelected || selected)
                return;

            gridAnimationViewIndex = index;
            gridAnimationPriorityScope = string.Empty;
            RebuildVisibleGridAnimationFrames();
            ESAssetPackagePreviewWorkflow.ClearGridFrameMemory();
            SaveState();
            ESAssetPackageBakeWindow.UsingWindow?.Repaint();
        }

        private void DrawPreviewGrid(List<ESAssetPackageBakeRecord> records, int totalPages, float pageWidth)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(pageWidth), GUILayout.ExpandHeight(true)))
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button("上一页", EditorStyles.toolbarButton, GUILayout.Width(60)))
                        currentPage = Mathf.Max(0, currentPage - 1);

                    GUILayout.Label($"{currentPage + 1}/{totalPages}  共 {records.Count} 个", EditorStyles.miniLabel, GUILayout.Width(130));

                    if (GUILayout.Button("下一页", EditorStyles.toolbarButton, GUILayout.Width(60)))
                        currentPage = Mathf.Min(totalPages - 1, currentPage + 1);

                    GUILayout.FlexibleSpace();
                }

                const float gutter = 10f;
                const float minCardWidth = 132f;
                const float maxGridHeight = 720f;
                int start = currentPage * itemsPerPage;
                int end = Mathf.Min(records.Count, start + itemsPerPage);
                int visibleCount = Mathf.Max(0, end - start);
                RefreshGridAnimationQueuePriority(start, end, records.Count);
                float viewHeight = Mathf.Min(maxGridHeight, Mathf.Max(420f, visibleCount > 0 ? 520f : 180f));

                Rect outerRect = GUILayoutUtility.GetRect(pageWidth, viewHeight, GUILayout.Width(pageWidth), GUILayout.MinHeight(180f));
                float availableWidth = Mathf.Max(minCardWidth, pageWidth - 18f);
                int maxColumnsThatFit = Mathf.Max(1, Mathf.FloorToInt((availableWidth + gutter) / (minCardWidth + gutter)));
                int columns = Mathf.Clamp(columnsPerRow, 1, maxColumnsThatFit);
                float cardWidth = Mathf.Floor((availableWidth - gutter * (columns + 1)) / columns);
                float imageSize = Mathf.Clamp(Mathf.Min(previewSize, cardWidth - 18f), MinPreviewSize, MaxPreviewSize);
                float cardHeight = imageSize + 78f;
                int rows = Mathf.Max(1, Mathf.CeilToInt(visibleCount / (float)columns));
                float contentHeight = gutter + rows * (cardHeight + gutter);

                Rect viewRect = new Rect(0f, 0f, availableWidth, contentHeight);
                gridScroll.x = 0f;
                gridAnimationVisibleKeys.Clear();
                int activeGridAnimationCount = 0;
                gridScroll = GUI.BeginScrollView(outerRect, gridScroll, viewRect, false, false);
                for (int i = 0; i < visibleCount; i++)
                {
                    int row = i / columns;
                    int col = i % columns;
                    Rect cardRect = new Rect(
                        gutter + col * (cardWidth + gutter),
                        gutter + row * (cardHeight + gutter),
                        cardWidth,
                        cardHeight);
                    bool cardVisibleInScroll = cardRect.yMax >= gridScroll.y && cardRect.y <= gridScroll.y + outerRect.height;
                    DrawAssetCard(records[start + i], cardRect, imageSize, cardVisibleInScroll, ref activeGridAnimationCount);
                }
                GUI.EndScrollView();
                SweepGridAnimationPlayers();
            }
        }

        private void RefreshGridAnimationQueuePriority(int start, int end, int totalCount)
        {
            if (category != ESAssetPackageCategory.Animation)
                return;

            string scope = GetBakeGuid() + "|" + category + "|page=" + currentPage + "|range=" + start + "-" + end + "|total=" + totalCount + "|view=" + gridAnimationViewIndex + "|search=" + (searchText ?? string.Empty) + "|used=" + onlyUsed + "|sort=" + assetSortMode + "|animClass=" + animationClassFilter;
            if (string.Equals(gridAnimationPriorityScope, scope, StringComparison.Ordinal))
                return;

            gridAnimationPriorityScope = scope;
            gridAnimationPriorityGeneration = ESAssetPackageGridAnimationFrameCache.BeginVisiblePagePriority(scope);
        }

        private void DrawAssetCard(ESAssetPackageBakeRecord record, Rect cardRect, float imageSize, bool cardVisibleInScroll, ref int activeGridAnimationCount)
        {
            bool isSelected = ReferenceEquals(selectedRecord, record);
            UnityEngine.Object asset = record.LoadAsset();
            Texture preview = ShouldUseGridAnimationPreview(record, asset, cardVisibleInScroll, activeGridAnimationCount)
                ? null
                : GetGridStaticPreviewTexture(record, asset, imageSize);
            bool hover = cardRect.Contains(Event.current.mousePosition);
            Color bg = isSelected
                ? new Color(0.13f, 0.23f, 0.34f, 1f)
                : hover ? new Color(0.12f, 0.13f, 0.15f, 1f) : new Color(0.085f, 0.09f, 0.10f, 1f);
            Color border = isSelected ? new Color(0.28f, 0.52f, 0.78f, 1f) : new Color(0.20f, 0.22f, 0.25f, 1f);

            EditorGUI.DrawRect(cardRect, bg);
            DrawBorder(cardRect, border);

            Rect imageRect = new Rect(cardRect.x + 8f, cardRect.y + 8f, cardRect.width - 16f, imageSize);
            EditorGUI.DrawRect(imageRect, new Color(0.055f, 0.06f, 0.07f, 1f));
            if (TryDrawGridAnimationPreview(record, asset, imageRect, cardVisibleInScroll, ref activeGridAnimationCount))
            {
            }
            else if (preview != null)
                GUI.DrawTexture(imageRect, preview, ScaleMode.ScaleToFit);
            else
                GUI.Label(imageRect, AssetPreview.IsLoadingAssetPreview(asset != null ? asset.GetInstanceID() : 0) ? "生成中..." : "无预览", EditorStyles.centeredGreyMiniLabel);

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            Rect useRect = new Rect(cardRect.x + 8f, imageRect.yMax + 6f, 52f, 18f);
            bool newUse = EditorGUI.ToggleLeft(useRect, "使用", record.selectedForUse);
            EditorGUI.indentLevel = oldIndent;
            if (newUse != record.selectedForUse)
            {
                record.selectedForUse = newUse;
                SetSelectedRecord(record);
                EditorUtility.SetDirty(bake);
                Event.current.Use();
            }

            Rect sizeRect = new Rect(cardRect.x + cardRect.width - 58f, imageRect.yMax + 7f, 50f, 16f);
            GUI.Label(sizeRect, record.fileSize, EditorStyles.miniLabel);
            if (record.category == ESAssetPackageCategory.Animation)
            {
                Rect classRect = new Rect(useRect.xMax + 4f, imageRect.yMax + 7f, Mathf.Max(24f, sizeRect.x - useRect.xMax - 8f), 16f);
                GUI.Label(classRect, GetAnimationClassDisplayName(ClassifyAnimationRecord(record)), EditorStyles.miniLabel);
            }

            ESAssetPackageExportLink exportLink = FindExportLink(record);
            if (exportLink != null && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(exportLink.targetAssetPath) != null)
            {
                Rect exportedRect = new Rect(cardRect.x + cardRect.width - 62f, cardRect.y + 10f, 54f, 18f);
                EditorGUI.DrawRect(exportedRect, new Color(0.16f, 0.34f, 0.22f, 0.92f));
                GUI.Label(exportedRect, "已复制", EditorStyles.centeredGreyMiniLabel);
            }

            Rect nameRect = new Rect(cardRect.x + 8f, useRect.yMax + 3f, cardRect.width - 16f, 18f);
            GUI.Label(nameRect, ShortName(record.assetName, Mathf.Max(10, Mathf.FloorToInt(cardRect.width / 8f))), EditorStyles.boldLabel);

            float buttonWidth = Mathf.Floor((cardRect.width - 22f) * 0.5f);
            Rect previewButtonRect = new Rect(cardRect.x + 8f, cardRect.yMax - 27f, buttonWidth, 20f);
            Rect pingButtonRect = new Rect(previewButtonRect.xMax + 6f, previewButtonRect.y, buttonWidth, 20f);
            if (GUI.Button(previewButtonRect, "预览"))
            {
                SetSelectedRecord(record);
                SelectLoadedAsset(asset);
                ESAssetPackageRecordPreviewWindow.Open(bake, record);
                Event.current.Use();
            }
            if (GUI.Button(pingButtonRect, "Ping"))
            {
                SetSelectedRecord(record);
                SelectLoadedAsset(asset);
                record.Ping();
                Event.current.Use();
            }

            Event evt = Event.current;
            bool clickOnControl = useRect.Contains(evt.mousePosition) ||
                                  previewButtonRect.Contains(evt.mousePosition) ||
                                  pingButtonRect.Contains(evt.mousePosition);
            if (evt.type == EventType.MouseDown && cardRect.Contains(evt.mousePosition) && !clickOnControl)
            {
                SetSelectedRecord(record);
                SelectLoadedAsset(asset);
                if (evt.clickCount >= 2 && asset != null)
                    ESAssetPackageRecordPreviewWindow.Open(bake, record);
                ESAssetPackageBakeWindow.UsingWindow?.Repaint();
                evt.Use();
            }
        }

        private static void SelectLoadedAsset(UnityEngine.Object asset)
        {
            if (asset != null)
                Selection.activeObject = asset;
        }

        private Texture GetGridStaticPreviewTexture(ESAssetPackageBakeRecord record, UnityEngine.Object asset, float imageSize)
        {
            int pixels = Mathf.CeilToInt(imageSize);
            if (record != null && record.category == ESAssetPackageCategory.Animation)
            {
                UnityEngine.Object model = ResolveGridAnimationPreviewModel(asset);
                GameObject go = ResolveGridPreviewGameObject(model);
                if (go != null)
                {
                    Texture preview = ESEditorPreviewUtility.GetAssetPreviewOrMini(go, () => ESAssetPackageBakeWindow.UsingWindow?.Repaint());
                    return preview;
                }

                return null;
            }

            return ESAssetPackagePreviewUtility.GetPreviewTexture(asset, record != null ? record.category : ESAssetPackageCategory.Other, pixels, bake);
        }

        private static GameObject ResolveGridPreviewGameObject(UnityEngine.Object source)
        {
            if (source is GameObject go)
                return go;

            string path = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private bool ShouldUseGridAnimationPreview(ESAssetPackageBakeRecord record, UnityEngine.Object asset, bool cardVisibleInScroll, int activeGridAnimationCount)
        {
            if (!GridAnimationPreviewEnabled)
                return false;

            if (!cardVisibleInScroll || !gridAnimationSlowPreview || category != ESAssetPackageCategory.Animation || record == null || asset == null)
                return false;

            if (activeGridAnimationCount >= Mathf.Clamp(gridAnimationMaxActive, 1, GridAnimationPreviewHardLimit))
                return false;

            return ResolveGridAnimationClip(asset) != null && ResolveGridAnimationPreviewModel(asset) != null;
        }

        private bool TryDrawGridAnimationPreview(ESAssetPackageBakeRecord record, UnityEngine.Object asset, Rect imageRect, bool cardVisibleInScroll, ref int activeGridAnimationCount)
        {
            if (!ShouldUseGridAnimationPreview(record, asset, cardVisibleInScroll, activeGridAnimationCount))
                return false;

            AnimationClip clip = ResolveGridAnimationClip(asset);
            clip = ESAssetPackagePreviewUtility.ResolveVisualMotionClip(clip);
            UnityEngine.Object model = ResolveGridAnimationPreviewModel(asset);
            if (clip == null || model == null)
                return false;

            float viewYaw = GetGridAnimationViewYaw();
            string key = GetGridAnimationPreviewKey(record, clip, model) + "|view=" + gridAnimationViewIndex;
            gridAnimationVisibleKeys.Add(key);
            gridAnimationLastSeen[key] = EditorApplication.timeSinceStartup;
            int priorityIndex = activeGridAnimationCount;
            activeGridAnimationCount++;

            ESAssetPackageGridAnimationFrameCache.Draw(
                imageRect,
                key,
                clip,
                model,
                ResolveGridAnimationPreviewFallbackMaterial(),
                ResolveGridAnimationPreviewAvatar(),
                Mathf.CeilToInt(imageRect.width),
                gridAnimationSlowSpeed,
                ESAssetPackageGridAnimationFrameCache.GetGridFrameCount(clip),
                ESAssetPackageGridAnimationFrameCache.GridMaxPixels,
                viewYaw,
                GetGridAnimationViewName(),
                GetGridAnimationViewName() + "小格",
                gridAnimationPriorityGeneration,
                priorityIndex);
            return true;
        }

        private string GetGridAnimationViewName()
        {
            switch (gridAnimationViewIndex)
            {
                case 1:
                    return "侧面";
                case 2:
                    return "背面";
                default:
                    return "正面";
            }
        }

        private float GetGridAnimationViewYaw()
        {
            switch (gridAnimationViewIndex)
            {
                case 1:
                    return 90f;
                case 2:
                    return 0f;
                default:
                    return 180f;
            }
        }

        private ESAssetPackageAnimationPreviewPlayer GetOrCreateGridAnimationPlayer(string key)
        {
            if (gridAnimationPlayers.TryGetValue(key, out ESAssetPackageAnimationPreviewPlayer player) && player != null)
                return player;

            player = new ESAssetPackageAnimationPreviewPlayer();
            gridAnimationPlayers[key] = player;
            return player;
        }

        private string GetGridAnimationPreviewKey(ESAssetPackageBakeRecord record, AnimationClip clip, UnityEngine.Object model)
        {
            string recordId = record != null ? (!string.IsNullOrEmpty(record.guid) ? record.guid : record.assetPath) : string.Empty;
            return "Grid|" + recordId + "|" + AssetDatabase.GetAssetPath(clip) + "|" + clip.name + "|" + AssetDatabase.GetAssetPath(model);
        }

        private void SweepGridAnimationPlayers()
        {
            if (gridAnimationPlayers.Count == 0)
                return;

            double now = EditorApplication.timeSinceStartup;
            List<string> removeKeys = null;
            foreach (string key in gridAnimationPlayers.Keys)
            {
                if (gridAnimationVisibleKeys.Contains(key))
                    continue;

                if (gridAnimationPlayers.TryGetValue(key, out ESAssetPackageAnimationPreviewPlayer player))
                    player?.SuspendRepaint();

                double lastSeen = gridAnimationLastSeen.TryGetValue(key, out double value) ? value : 0d;
                if (now - lastSeen < GridAnimationPlayerKeepAliveSeconds)
                    continue;

                removeKeys ??= new List<string>();
                removeKeys.Add(key);
            }

            if (removeKeys == null)
                return;

            for (int i = 0; i < removeKeys.Count; i++)
            {
                if (gridAnimationPlayers.TryGetValue(removeKeys[i], out ESAssetPackageAnimationPreviewPlayer player))
                    player?.Dispose();
                gridAnimationPlayers.Remove(removeKeys[i]);
                gridAnimationLastSeen.Remove(removeKeys[i]);
            }
        }

        private void SuspendGridAnimationPlayers()
        {
            foreach (ESAssetPackageAnimationPreviewPlayer player in gridAnimationPlayers.Values)
                player?.SuspendRepaint();
            gridAnimationVisibleKeys.Clear();
        }

        private void ClearGridAnimationPlayers()
        {
            foreach (ESAssetPackageAnimationPreviewPlayer player in gridAnimationPlayers.Values)
                player?.Dispose();
            gridAnimationPlayers.Clear();
            gridAnimationLastSeen.Clear();
            gridAnimationVisibleKeys.Clear();
        }

        private static AnimationClip ResolveGridAnimationClip(UnityEngine.Object asset)
        {
            if (asset is AnimationClip clip)
                return clip;

            string path = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return null;

            return LoadAnimationClipsFromAnyAssetPath(path)
                .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
        }

        internal static AnimationClip[] LoadAnimationClipsFromAnyAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Array.Empty<AnimationClip>();

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .Concat(AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .GroupBy(c => c.name)
                .Select(g => g.First())
                .ToArray();
        }

        private UnityEngine.Object ResolveGridAnimationPreviewModel(UnityEngine.Object currentAsset)
        {
            UnityEngine.Object configuredModel = ESAssetPackagePreviewWorkflow.ResolveConfiguredAnimationPreviewModel(bake);
            if (configuredModel != null)
                return configuredModel;

            UnityEngine.Object rememberedModel = ESAssetPackagePreviewWorkflow.LoadRememberedPreviewModel(PrefKeyRecordPreviewModelGuid);
            if (rememberedModel != null)
                return rememberedModel;

            return null;
        }

        private Avatar ResolveGridAnimationPreviewAvatar()
        {
            return ESAssetPackagePreviewWorkflow.ResolveAnimationPreviewAvatar(bake);
        }

        private Material ResolveGridAnimationPreviewFallbackMaterial()
        {
            return ESAssetPackagePreviewWorkflow.ResolveAnimationPreviewFallbackMaterial(bake);
        }

        private ESAssetPackageExportLink FindExportLink(ESAssetPackageBakeRecord record)
        {
            if (bake == null || bake.exportLinks == null || record == null)
                return null;

            for (int i = 0; i < bake.exportLinks.Count; i++)
            {
                ESAssetPackageExportLink link = bake.exportLinks[i];
                if (link == null)
                    continue;

                if (!string.IsNullOrEmpty(record.guid) && string.Equals(link.sourceGuid, record.guid, StringComparison.OrdinalIgnoreCase))
                    return link;

                if (string.Equals(link.sourceAssetPath, record.assetPath, StringComparison.OrdinalIgnoreCase))
                    return link;
            }

            return null;
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static bool ContainsIgnoreCase(string source, string key)
        {
            return !string.IsNullOrEmpty(source) && source.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private IEnumerable<ESAssetPackageBakeRecord> SortRecords(IEnumerable<ESAssetPackageBakeRecord> records)
        {
            records ??= Enumerable.Empty<ESAssetPackageBakeRecord>();
            switch (assetSortMode)
            {
                case ESAssetPackageSortMode.Path:
                    return records.OrderBy(r => r != null ? r.assetPath : string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.Type:
                    return records.OrderBy(r => r != null ? r.typeName : string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.SizeSmallToLarge:
                    return records.OrderBy(GetAssetFileBytes)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.SizeLargeToSmall:
                    return records.OrderByDescending(GetAssetFileBytes)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.UsedFirst:
                    return records.OrderByDescending(r => r != null && r.selectedForUse)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.UnusedFirst:
                    return records.OrderBy(r => r != null && r.selectedForUse)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.AnimationClass:
                    return records.OrderBy(r => ClassifyAnimationRecord(r))
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.AnimationLengthShortToLong:
                    return records.OrderBy(GetAnimationClipLength)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                case ESAssetPackageSortMode.AnimationLengthLongToShort:
                    return records.OrderByDescending(GetAnimationClipLength)
                        .ThenBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase);
                default:
                    return records.OrderBy(r => r != null ? r.assetName : string.Empty, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r != null ? r.assetPath : string.Empty, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static long GetAssetFileBytes(ESAssetPackageBakeRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.assetPath))
                return 0;

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return 0;

                string fullPath = Path.Combine(projectRoot, record.assetPath.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static float GetAnimationClipLength(ESAssetPackageBakeRecord record)
        {
            if (record == null || record.category != ESAssetPackageCategory.Animation)
                return 0f;

            AnimationClip clip = null;
            if (!string.IsNullOrEmpty(record.assetPath))
            {
                clip = LoadAnimationClipsFromAnyAssetPath(record.assetPath)
                    .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));
            }

            if (clip == null)
                clip = record.LoadAsset() as AnimationClip;

            return clip != null ? clip.length : 0f;
        }

        private static ESAssetPackageAnimationClass ClassifyAnimationRecord(ESAssetPackageBakeRecord record)
        {
            if (record == null || record.category != ESAssetPackageCategory.Animation)
                return ESAssetPackageAnimationClass.Other;

            string fileName = string.IsNullOrEmpty(record.assetPath) ? string.Empty : Path.GetFileNameWithoutExtension(record.assetPath);
            string text = ((record.assetName ?? string.Empty) + " " + fileName).ToLowerInvariant();

            if (HasAny(text, "die", "death", "flydie", "jumpdie", "slidedie", "standdie", "turndie"))
                return ESAssetPackageAnimationClass.Death;
            if (HasAny(text, "dam_", "damage", "hit", "stumble", "fail", "standup", "counter", "finisher"))
                return ESAssetPackageAnimationClass.DamageHit;
            if (HasAny(text, "hiwall", "lowall", "wall_", "wall@", "wall", "climb", "lhold", "rhold", "lland", "rland", "lfall", "rfall"))
                return ESAssetPackageAnimationClass.WallParkour;
            if (HasAny(text, "tum_", "handspring", "rackspring"))
                return ESAssetPackageAnimationClass.TumbleAcrobat;
            if (HasAny(text, "jattack", "airdrop", "airfront"))
                return ESAssetPackageAnimationClass.AttackAir;
            if (HasAny(text, "wpattack", "attack", "kick"))
                return ESAssetPackageAnimationClass.AttackMelee;
            if (HasAny(text, "spawn"))
                return ESAssetPackageAnimationClass.Spawn;
            if (HasAny(text, "slide"))
                return ESAssetPackageAnimationClass.Slide;
            if (HasAny(text, "roll"))
                return ESAssetPackageAnimationClass.Roll;
            if (HasAny(text, "dodge", "esc_", "standmove", "turnDodge".ToLowerInvariant()))
                return ESAssetPackageAnimationClass.EscapeDodge;
            if (HasAny(text, "land"))
                return ESAssetPackageAnimationClass.Land;
            if (HasAny(text, "jmp", "jump", "air", "fall"))
                return ESAssetPackageAnimationClass.JumpAir;
            if (HasAny(text, "wp_", "weapon"))
                return ESAssetPackageAnimationClass.Weapon;
            if (HasAny(text, "charge", "power", "pwcharge"))
                return ESAssetPackageAnimationClass.ChargePower;
            if (HasAny(text, "arc_"))
                return ESAssetPackageAnimationClass.ArcMove;
            if (HasAny(text, "turnl", "turnr", "_turn", "turn_"))
                return ESAssetPackageAnimationClass.Turn;
            if (HasAny(text, "start"))
                return ESAssetPackageAnimationClass.MoveStart;
            if (HasAny(text, "stop"))
                return ESAssetPackageAnimationClass.MoveStop;
            if (HasAny(text, "end"))
                return ESAssetPackageAnimationClass.MoveEnd;
            if (HasAny(text, "boost"))
                return ESAssetPackageAnimationClass.Boost;
            if (HasAny(text, "dash"))
                return ESAssetPackageAnimationClass.Dash;
            if (HasAny(text, "jog", "run"))
                return ESAssetPackageAnimationClass.JogRun;
            if (HasAny(text, "walk"))
                return ESAssetPackageAnimationClass.Walk;
            if (HasAny(text, "idle", "wait", "prone"))
                return ESAssetPackageAnimationClass.Idle;

            return ESAssetPackageAnimationClass.Other;
        }

        private static bool HasAny(string text, params string[] keys)
        {
            if (string.IsNullOrEmpty(text) || keys == null)
                return false;

            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (!string.IsNullOrEmpty(key) && text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string GetAnimationClassDisplayName(ESAssetPackageAnimationClass animationClass)
        {
            switch (animationClass)
            {
                case ESAssetPackageAnimationClass.All: return "全部";
                case ESAssetPackageAnimationClass.Idle: return "待机&等待";
                case ESAssetPackageAnimationClass.Walk: return "步行";
                case ESAssetPackageAnimationClass.JogRun: return "慢跑&奔跑";
                case ESAssetPackageAnimationClass.MoveStart: return "移动开始";
                case ESAssetPackageAnimationClass.MoveStop: return "移动停止";
                case ESAssetPackageAnimationClass.MoveEnd: return "移动结束";
                case ESAssetPackageAnimationClass.Turn: return "原地转向";
                case ESAssetPackageAnimationClass.ArcMove: return "弧线移动";
                case ESAssetPackageAnimationClass.Dash: return "冲刺";
                case ESAssetPackageAnimationClass.Boost: return "加速&Boost";
                case ESAssetPackageAnimationClass.ChargePower: return "蓄力&强冲";
                case ESAssetPackageAnimationClass.EscapeDodge: return "闪避&侧移";
                case ESAssetPackageAnimationClass.Roll: return "翻滚";
                case ESAssetPackageAnimationClass.Slide: return "滑行";
                case ESAssetPackageAnimationClass.JumpAir: return "跳跃&空中";
                case ESAssetPackageAnimationClass.Land: return "落地";
                case ESAssetPackageAnimationClass.WallParkour: return "墙面&攀爬";
                case ESAssetPackageAnimationClass.TumbleAcrobat: return "体操&翻越";
                case ESAssetPackageAnimationClass.AttackMelee: return "近战攻击";
                case ESAssetPackageAnimationClass.AttackAir: return "空中攻击";
                case ESAssetPackageAnimationClass.DamageHit: return "受击&硬直";
                case ESAssetPackageAnimationClass.Death: return "死亡";
                case ESAssetPackageAnimationClass.Weapon: return "武器动作";
                case ESAssetPackageAnimationClass.Spawn: return "出生/出现";
                default: return "其他";
            }
        }

        private static string ShortName(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
                return value;
            return value.Substring(0, Mathf.Max(1, maxLength - 1)) + "...";
        }

        private void SetSelectedRecord(ESAssetPackageBakeRecord record)
        {
            selectedRecord = record;
            SaveState();
        }

        private void LoadState()
        {
            if (stateLoaded || bake == null)
                return;

            stateLoaded = true;
            previewSize = Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("PreviewSize"), previewSize), MinPreviewSize, MaxPreviewSize);
            columnsPerRow = Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("Columns"), columnsPerRow), 3, 10);
            itemsPerPage = Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("ItemsPerPage"), itemsPerPage), 12, 300);
            currentPage = Mathf.Max(0, EditorPrefs.GetInt(GetStateKey("Page"), currentPage));
            onlyUsed = EditorPrefs.GetBool(GetStateKey("OnlyUsed"), onlyUsed);
            searchText = EditorPrefs.GetString(GetStateKey("Search"), searchText ?? string.Empty);
            copyFilter = (ESAssetPackageCopyFilter)Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("CopyFilter"), (int)copyFilter), 0, Enum.GetValues(typeof(ESAssetPackageCopyFilter)).Length - 1);
            if (onlyUsed && copyFilter == ESAssetPackageCopyFilter.All)
            {
                copyFilter = ESAssetPackageCopyFilter.Used;
                onlyUsed = false;
            }
            assetSortMode = (ESAssetPackageSortMode)Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("SortMode"), (int)assetSortMode), 0, Enum.GetValues(typeof(ESAssetPackageSortMode)).Length - 1);
            animationClassFilter = (ESAssetPackageAnimationClass)Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("AnimationClassFilter"), (int)animationClassFilter), 0, Enum.GetValues(typeof(ESAssetPackageAnimationClass)).Length - 1);
            gridAnimationSlowPreview = EditorPrefs.GetBool(GetStateKey("GridAnimationSlowPreview"), gridAnimationSlowPreview);
            gridAnimationSlowSpeed = Mathf.Clamp(EditorPrefs.GetFloat(GetStateKey("GridAnimationSlowSpeed"), gridAnimationSlowSpeed), 0.1f, 1f);
            if (gridAnimationSlowSpeed < 0.5f)
                gridAnimationSlowSpeed = 0.5f;
            gridAnimationMaxActive = Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("GridAnimationMaxActive"), gridAnimationMaxActive), 1, GridAnimationPreviewHardLimit);
            gridAnimationViewIndex = Mathf.Clamp(EditorPrefs.GetInt(GetStateKey("GridAnimationViewIndex"), gridAnimationViewIndex), 0, 2);

            string selectedId = EditorPrefs.GetString(GetStateKey("SelectedRecord"), string.Empty);
            if (!string.IsNullOrEmpty(selectedId))
            {
                selectedRecord = CategoryRecords.FirstOrDefault(r =>
                    r != null &&
                    (string.Equals(r.guid, selectedId, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(r.assetPath, selectedId, StringComparison.OrdinalIgnoreCase)));
            }
        }

        private void SaveState()
        {
            if (bake == null)
                return;

            EditorPrefs.SetInt(GetStateKey("PreviewSize"), previewSize);
            EditorPrefs.SetInt(GetStateKey("Columns"), columnsPerRow);
            EditorPrefs.SetInt(GetStateKey("ItemsPerPage"), itemsPerPage);
            EditorPrefs.SetInt(GetStateKey("Page"), currentPage);
            EditorPrefs.SetBool(GetStateKey("OnlyUsed"), onlyUsed);
            EditorPrefs.SetString(GetStateKey("Search"), searchText ?? string.Empty);
            EditorPrefs.SetInt(GetStateKey("CopyFilter"), (int)copyFilter);
            EditorPrefs.SetInt(GetStateKey("SortMode"), (int)assetSortMode);
            EditorPrefs.SetInt(GetStateKey("AnimationClassFilter"), (int)animationClassFilter);
            EditorPrefs.SetBool(GetStateKey("GridAnimationSlowPreview"), gridAnimationSlowPreview);
            EditorPrefs.SetFloat(GetStateKey("GridAnimationSlowSpeed"), gridAnimationSlowSpeed);
            EditorPrefs.SetInt(GetStateKey("GridAnimationMaxActive"), gridAnimationMaxActive);
            EditorPrefs.SetInt(GetStateKey("GridAnimationViewIndex"), gridAnimationViewIndex);
            if (selectedRecord != null)
                EditorPrefs.SetString(GetStateKey("SelectedRecord"), !string.IsNullOrEmpty(selectedRecord.guid) ? selectedRecord.guid : selectedRecord.assetPath ?? string.Empty);
        }

        private string GetStateKey(string name)
        {
            string bakeGuid = GetBakeGuid();
            return PrefPrefix + bakeGuid + "." + category + "." + name;
        }

        private string GetBakeGuid()
        {
            string path = bake == null ? string.Empty : AssetDatabase.GetAssetPath(bake);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? "NoBake" : guid;
        }
    }

    public class ESAssetPackageRecordPreviewWindow : EditorWindow
    {
        private const string PrefKeyBakeGuid = "ES.AssetPackageRecordPreviewWindow.BakeGuid";
        private const string PrefKeyRecordId = "ES.AssetPackageRecordPreviewWindow.RecordId";
        private const string PrefKeyModelGuid = "ES.AssetPackageRecordPreviewWindow.ModelGuid";
        private const string PrefKeyClipIndex = "ES.AssetPackageRecordPreviewWindow.ClipIndex";
        private const string PrefKeyDebug = "ES.AssetPackageRecordPreviewWindow.Debug";
        private ESAssetPackageBakeData bake;
        private ESAssetPackageBakeRecord record;
        private Vector2 scroll;
        private UnityEngine.Object animationPreviewModel;
        private int animationClipIndex;
        private bool showAnimationDebug;
        private bool showAnimationClipInfo;
        private bool showAnimationHeaderInfo;
        private Vector2 animationDebugScroll;
        private readonly ESAssetPackageAnimationPreviewPlayer animationPreview = new ESAssetPackageAnimationPreviewPlayer();

        public static void Open(ESAssetPackageBakeData bake, ESAssetPackageBakeRecord record)
        {
            if (record == null)
                return;

            ESAssetPackageRecordPreviewWindow window = GetWindow<ESAssetPackageRecordPreviewWindow>("资产完整预览");
            window.bake = bake;
            window.record = record;
            window.RestoreWindowState();
            window.scroll = Vector2.zero;
            window.animationDebugScroll = Vector2.zero;
            window.showAnimationClipInfo = false;
            window.showAnimationDebug = false;
            window.SaveWindowState();
            window.titleContent = new GUIContent("预览: " + record.assetName);
            window.minSize = new Vector2(520f, 640f);
            UnityEngine.Object asset = record.LoadAsset();
            if (asset != null)
                Selection.activeObject = asset;
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            RestoreWindowState();
        }

        private void OnDisable() 
        {
            SaveWindowState();
            ReleasePreviewResources();
        }

        public void ReleasePreviewResources()
        {
            animationPreview.Dispose();
        }

        private void OnGUI()
        {
            RestoreWindowState();
            if (record == null)
            {
                EditorGUILayout.HelpBox("没有可预览资源。", MessageType.Warning);
                return;
            }

            UnityEngine.Object asset = record.LoadAsset();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField("资源", asset, typeof(UnityEngine.Object), false);
                EditorGUILayout.LabelField("路径", record.assetPath, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField("分类", ESAssetPackageBakeWindow.GetCategoryDisplayName(record.category));
                EditorGUILayout.LabelField("类型", record.typeName);
                EditorGUILayout.LabelField("大小", record.fileSize);
                DrawExportLinkInfo();

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool newUse = GUILayout.Toggle(record.selectedForUse, record.selectedForUse ? "已标记使用" : "未标记使用", EditorStyles.toolbarButton);
                    if (newUse != record.selectedForUse)
                    {
                        record.selectedForUse = newUse;
                        SaveBake();
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(70)))
                        record.Ping();
                    if (GUILayout.Button("选中", GUILayout.Width(70)))
                        record.SelectAsset();
                    if (GUILayout.Button("打开", GUILayout.Width(70)) && asset != null)
                        AssetDatabase.OpenAsset(asset);
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawCategoryDetail(asset, record);
            EditorGUILayout.EndScrollView();
        }

        private void SaveBake()
        {
            if (bake == null)
                return;

            bake.RebuildStats();
            EditorUtility.SetDirty(bake);
            AssetDatabase.SaveAssets();
        }

        private void DrawExportLinkInfo()
        {
            ESAssetPackageExportLink link = FindExportLink(record);
            if (link == null)
            {
                EditorGUILayout.LabelField("复制状态", "未复制");
                EditorGUILayout.LabelField("源路径", record != null ? record.assetPath : string.Empty, EditorStyles.wordWrappedMiniLabel);
                return;
            }

            bool exists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(link.targetAssetPath) != null;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("复制状态", exists ? "已复制" : "链路存在但目标丢失");
                using (new EditorGUI.DisabledScope(!exists))
                {
                    if (GUILayout.Button("Ping 复制目标", GUILayout.Width(96)))
                    {
                        UnityEngine.Object target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(link.targetAssetPath);
                        Selection.activeObject = target;
                        EditorGUIUtility.PingObject(target);
                    }
                }
            }

            EditorGUILayout.LabelField("目标存在", exists ? "是" : "否");
            EditorGUILayout.LabelField("复制次数", link.exportCount.ToString());
            EditorGUILayout.LabelField("最近复制时间", string.IsNullOrEmpty(link.lastExportTime) ? "无" : link.lastExportTime);
            EditorGUILayout.LabelField("源路径", link.sourceAssetPath, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("复制目标", link.targetAssetPath, EditorStyles.wordWrappedMiniLabel);
        }

        private ESAssetPackageExportLink FindExportLink(ESAssetPackageBakeRecord targetRecord)
        {
            if (bake == null || bake.exportLinks == null || targetRecord == null)
                return null;

            for (int i = 0; i < bake.exportLinks.Count; i++)
            {
                ESAssetPackageExportLink link = bake.exportLinks[i];
                if (link == null)
                    continue;

                if (!string.IsNullOrEmpty(targetRecord.guid) && string.Equals(link.sourceGuid, targetRecord.guid, StringComparison.OrdinalIgnoreCase))
                    return link;

                if (string.Equals(link.sourceAssetPath, targetRecord.assetPath, StringComparison.OrdinalIgnoreCase))
                    return link;
            }

            return null;
        }

        private void DrawCategoryDetail(UnityEngine.Object asset, ESAssetPackageBakeRecord targetRecord)
        {
            switch (targetRecord.category)
            {
                case ESAssetPackageCategory.Texture:
                    ESAssetPackagePreviewUtility.DrawTextureDetail(asset as Texture2D, targetRecord.assetPath);
                    break;
                case ESAssetPackageCategory.Material:
                    ESAssetPackagePreviewUtility.DrawMaterialDetail(asset as Material);
                    break;
                case ESAssetPackageCategory.Prefab:
                case ESAssetPackageCategory.Model:
                    ESAssetPackagePreviewUtility.DrawObjectPreviewDetail(asset, targetRecord.assetPath);
                    if (HasAnimationClips(asset))
                        DrawAnimationDetail(asset, targetRecord);
                    break;
                case ESAssetPackageCategory.Audio:
                    ESAssetPackagePreviewUtility.DrawAudioDetail(asset as AudioClip, targetRecord.assetPath);
                    break;
                case ESAssetPackageCategory.Animation:
                    DrawAnimationDetail(asset, targetRecord);
                    break;
                case ESAssetPackageCategory.Shader:
                    ESAssetPackagePreviewUtility.DrawShaderDetail(asset as Shader, bake);
                    break;
                case ESAssetPackageCategory.Font:
                    ESAssetPackagePreviewUtility.DrawFontDetail(asset);
                    break;
                case ESAssetPackageCategory.Video:
                    ESAssetPackagePreviewUtility.DrawVideoDetail(asset);
                    break;
                default:
                    ESAssetPackagePreviewUtility.DrawObjectPreviewDetail(asset, targetRecord.assetPath);
                    break;
            }
        }

        private void DrawAnimationDetail(UnityEngine.Object asset, ESAssetPackageBakeRecord targetRecord)
        {
            AnimationClip clip = ResolveAnimationClip(asset);
            AnimationClip previewClip = ESAssetPackagePreviewUtility.ResolveVisualMotionClip(clip);
            showAnimationClipInfo = EditorGUILayout.Foldout(showAnimationClipInfo, "Clip 信息与引用", true);
            if (showAnimationClipInfo)
            {
                DrawAnimationClipReferencePanel(asset, clip, previewClip);
                ESAssetPackagePreviewUtility.DrawAnimationClipInfo(previewClip != null ? previewClip : clip);
            }
            if (previewClip != clip && previewClip != null)
                EditorGUILayout.HelpBox("当前 Clip 更像 RootMotion/IK 数据，预览已自动切换到同名视觉动作 Clip：" + previewClip.name, MessageType.Info);
            animationPreview.RepaintOwner = Repaint;

            if (animationPreviewModel == null)
                animationPreviewModel = ResolveDefaultAnimationPreviewModel(asset);

            UnityEngine.Object newPreviewModel = EditorGUILayout.ObjectField("预览模型", animationPreviewModel, typeof(GameObject), false);
            if (newPreviewModel != animationPreviewModel)
            {
                if (newPreviewModel == null || CanUseAsAnimationPreviewModel(newPreviewModel))
                {
                    animationPreview.Stop();
                    animationPreviewModel = newPreviewModel;
                    SaveWindowState();
                }
                else
                {
                    EditorGUILayout.HelpBox("这个资源没有 Animator，不适合作为 Humanoid 动作预览模型。请使用带 Animator 的角色预制体/模型。", MessageType.Warning);
                }
            }

            EditorGUILayout.ObjectField("当前模型", animationPreviewModel, typeof(UnityEngine.Object), false);
            string previewKey = targetRecord.assetPath + "|" + (previewClip != null ? previewClip.name : string.Empty);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(previewClip == null || animationPreviewModel == null))
                {
                    if (GUILayout.Button(animationPreview.IsPlaying(previewKey) ? "暂停播放" : "播放动画"))
                        animationPreview.Toggle(previewKey, previewClip, animationPreviewModel, ResolveAnimationPreviewFallbackMaterial(), ResolveAnimationPreviewAvatar());
                }

                if (GUILayout.Button("停止"))
                    animationPreview.Stop(true);

                using (new EditorGUI.DisabledScope(!ESAssetPackagePreviewWorkflow.CanUseAsAnimationPreviewModel(asset)))
                {
                    if (GUILayout.Button("用当前资源作模型"))
                    {
                        animationPreview.Stop();
                        animationPreviewModel = asset;
                        SaveWindowState();
                    }
                }

                if (GUILayout.Button("恢复默认模型"))
                {
                    animationPreview.Stop();
                    animationPreviewModel = null;
                    EditorPrefs.DeleteKey(PrefKeyModelGuid);
                    animationPreviewModel = ResolveDefaultAnimationPreviewModel(asset);
                    SaveWindowState();
                }

                using (new EditorGUI.DisabledScope(StateMachineConfig.Instance == null || ESAssetPackagePreviewWorkflow.ResolvePreviewGameObject(animationPreviewModel) == null))
                {
                    if (GUILayout.Button("保存到状态机预览"))
                    {
                        StateMachineConfig stateMachineConfig = StateMachineConfig.Instance;
                        if (stateMachineConfig != null)
                        {
                            stateMachineConfig.previewModel = animationPreviewModel;
                            EditorUtility.SetDirty(stateMachineConfig);
                        }
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            animationPreview.ConfigureExpandedPreviewQuality();
            animationPreview.DrawPreviewOptions();

            if (previewClip == null)
                EditorGUILayout.HelpBox("没有找到可播放 AnimationClip。FBX 需要在导入设置里启用动画并生成 Clip。", MessageType.Warning);
            else if (animationPreviewModel == null)
                EditorGUILayout.HelpBox("没有预览模型。可直接拖入一个带 Animator/骨骼的角色模型。", MessageType.Warning);
            else
                EditorGUILayout.LabelField("播放状态", animationPreview.LastStatus, EditorStyles.miniLabel);

            Rect rect = GUILayoutUtility.GetRect(480, 320, GUILayout.ExpandWidth(true));
            animationPreview.Draw(rect, previewKey, previewClip, animationPreviewModel, ResolveAnimationPreviewFallbackMaterial(), ResolveAnimationPreviewAvatar());

            DrawAnimationDebug(previewClip, animationPreviewModel, previewKey);
        }

        private void DrawAnimationClipReferencePanel(UnityEngine.Object sourceAsset, AnimationClip selectedClip, AnimationClip previewClip)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("AnimationClip 引用", EditorStyles.boldLabel);
                DrawClipObjectRow("当前选择 Clip", selectedClip);
                if (previewClip != null && previewClip != selectedClip)
                    DrawClipObjectRow("实际播放 Clip", previewClip);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField("原始资源", sourceAsset, typeof(UnityEngine.Object), false);
                    using (new EditorGUI.DisabledScope(sourceAsset == null))
                    {
                        if (GUILayout.Button("Ping", GUILayout.Width(54)))
                            EditorGUIUtility.PingObject(sourceAsset);
                    }
                }

                string clipPath = selectedClip == null ? string.Empty : AssetDatabase.GetAssetPath(selectedClip);
                if (!string.IsNullOrEmpty(clipPath))
                    EditorGUILayout.LabelField("Clip 路径", clipPath, EditorStyles.miniLabel);
            }
        }

        private static void DrawClipObjectRow(string label, AnimationClip clip)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(label, clip, typeof(AnimationClip), false);
                using (new EditorGUI.DisabledScope(clip == null))
                {
                    if (GUILayout.Button("选中", GUILayout.Width(54)))
                    {
                        Selection.activeObject = clip;
                        EditorGUIUtility.PingObject(clip);
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(54)))
                        EditorGUIUtility.PingObject(clip);
                }
            }
        }

        private void DrawAnimationDebug(AnimationClip clip, UnityEngine.Object model, string previewKey)
        {
            bool newShowAnimationDebug = EditorGUILayout.Foldout(showAnimationDebug, "动画 Debug", true);
            if (newShowAnimationDebug != showAnimationDebug)
            {
                showAnimationDebug = newShowAnimationDebug;
                SaveWindowState();
            }
            if (!showAnimationDebug)
                return;

            string report = animationPreview.GetDebugReport(clip, model, previewKey);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("诊断信息", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("复制 Debug", GUILayout.Width(86)))
                        EditorGUIUtility.systemCopyBuffer = report;
                }

                animationDebugScroll = EditorGUILayout.BeginScrollView(animationDebugScroll, GUILayout.MinHeight(170), GUILayout.MaxHeight(260));
                EditorGUILayout.TextArea(report, EditorStyles.wordWrappedMiniLabel, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private AnimationClip ResolveAnimationClip(UnityEngine.Object asset)
        {
            if (asset is AnimationClip directClip)
                return directClip;

            AnimationClip[] assetClips = LoadAnimationClipsFromAsset(asset);
            if (assetClips.Length > 0)
            {
                animationClipIndex = Mathf.Clamp(animationClipIndex, 0, assetClips.Length - 1);
                string[] names = assetClips.Select(c => c != null ? c.name : "<空>").ToArray();
                int newAnimationClipIndex = EditorGUILayout.Popup("播放片段", animationClipIndex, names);
                if (newAnimationClipIndex != animationClipIndex)
                {
                    animationClipIndex = newAnimationClipIndex;
                    SaveWindowState();
                }
                return assetClips[animationClipIndex];
            }

            if (asset is RuntimeAnimatorController controller)
            {
                AnimationClip[] clips = controller.animationClips ?? Array.Empty<AnimationClip>();
                if (clips.Length == 0)
                {
                    EditorGUILayout.HelpBox("这个动画控制器里没有可直接预览的 Clip。", MessageType.Info);
                    return null;
                }

                animationClipIndex = Mathf.Clamp(animationClipIndex, 0, clips.Length - 1);
                string[] names = clips.Select(c => c != null ? c.name : "<空>").ToArray();
                int newAnimationClipIndex = EditorGUILayout.Popup("播放片段", animationClipIndex, names);
                if (newAnimationClipIndex != animationClipIndex)
                {
                    animationClipIndex = newAnimationClipIndex;
                    SaveWindowState();
                }
                return clips[animationClipIndex];
            }

            EditorGUILayout.HelpBox("当前动画资源不是 AnimationClip，也不是可读取片段的动画控制器。", MessageType.Info);
            return null;
        }

        private static bool HasAnimationClips(UnityEngine.Object asset)
        {
            return LoadAnimationClipsFromAsset(asset).Length > 0;
        }

        private static AnimationClip[] LoadAnimationClipsFromAsset(UnityEngine.Object asset)
        {
            string path = asset == null ? string.Empty : AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return Array.Empty<AnimationClip>();

            return Page_AssetPackageBakeCategory.LoadAnimationClipsFromAnyAssetPath(path);
        }

        private UnityEngine.Object FindDefaultPreviewModel(UnityEngine.Object currentAsset)
        {
            if (currentAsset is GameObject currentGo && currentGo.GetComponentsInChildren<Renderer>(true).Length > 0)
                return currentGo;

            if (bake == null || bake.records == null)
                return null;

            ESAssetPackageBakeRecord modelRecord = bake.records.FirstOrDefault(r =>
                r != null && (r.category == ESAssetPackageCategory.Model || r.category == ESAssetPackageCategory.Prefab));
            return modelRecord?.LoadAsset();
        }

        private UnityEngine.Object ResolveDefaultAnimationPreviewModel(UnityEngine.Object currentAsset)
        {
            UnityEngine.Object configuredModel = ESAssetPackagePreviewWorkflow.ResolveConfiguredAnimationPreviewModel(bake);
            if (configuredModel != null)
                return configuredModel;

            return FindDefaultPreviewModel(currentAsset);
        }

        private static GameObject ResolvePreviewGameObject(UnityEngine.Object source)
        {
            return ESAssetPackagePreviewWorkflow.ResolvePreviewGameObject(source);
        }

        private static bool CanUseAsAnimationPreviewModel(UnityEngine.Object source)
        {
            return ESAssetPackagePreviewWorkflow.CanUseAsAnimationPreviewModel(source);
        }

        private Avatar ResolveAnimationPreviewAvatar()
        {
            return ESAssetPackagePreviewWorkflow.ResolveAnimationPreviewAvatar(bake);
        }

        private Material ResolveAnimationPreviewFallbackMaterial()
        {
            return ESAssetPackagePreviewWorkflow.ResolveAnimationPreviewFallbackMaterial(bake);
        }

        private void RestoreWindowState()
        {
            if (bake == null)
            {
                string bakeGuid = EditorPrefs.GetString(PrefKeyBakeGuid, string.Empty);
                if (!string.IsNullOrEmpty(bakeGuid))
                {
                    string bakePath = AssetDatabase.GUIDToAssetPath(bakeGuid);
                    if (!string.IsNullOrEmpty(bakePath))
                        bake = AssetDatabase.LoadAssetAtPath<ESAssetPackageBakeData>(bakePath);
                }
            }

            if (record == null && bake != null && bake.records != null)
            {
                string recordId = EditorPrefs.GetString(PrefKeyRecordId, string.Empty);
                if (!string.IsNullOrEmpty(recordId))
                {
                    record = bake.records.FirstOrDefault(r =>
                        r != null &&
                        (string.Equals(r.guid, recordId, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(r.assetPath, recordId, StringComparison.OrdinalIgnoreCase)));
                    if (record != null)
                        titleContent = new GUIContent("预览: " + record.assetName);
                }
            }

            if (animationPreviewModel == null)
                animationPreviewModel = LoadObjectBySavedGuid<GameObject>(PrefKeyModelGuid);

            animationClipIndex = Mathf.Max(0, EditorPrefs.GetInt(PrefKeyClipIndex, animationClipIndex));
            showAnimationDebug = EditorPrefs.GetBool(PrefKeyDebug, showAnimationDebug);
        }

        private void SaveWindowState()
        {
            SaveObjectGuid(PrefKeyBakeGuid, bake);
            if (record != null)
                EditorPrefs.SetString(PrefKeyRecordId, !string.IsNullOrEmpty(record.guid) ? record.guid : record.assetPath ?? string.Empty);
            SaveObjectGuid(PrefKeyModelGuid, animationPreviewModel);
            EditorPrefs.SetInt(PrefKeyClipIndex, animationClipIndex);
            EditorPrefs.SetBool(PrefKeyDebug, showAnimationDebug);
        }

        private static void SaveObjectGuid(string key, UnityEngine.Object obj)
        {
            if (obj == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            string path = AssetDatabase.GetAssetPath(obj);
            string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                EditorPrefs.SetString(key, guid);
        }

        private static T LoadObjectBySavedGuid<T>(string key) where T : UnityEngine.Object
        {
            string guid = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }

    internal static class ESAssetPackagePreviewUtility
    {
        private static readonly Dictionary<string, Texture2D> PreviewCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> ModelPreviewCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, int> PreviewMissCounts = new Dictionary<string, int>();
        private static readonly Dictionary<string, List<string>> ShaderMaterialCache = new Dictionary<string, List<string>>();
        private static Material previewFallbackMaterial;
        private static MethodInfo playClipMethod;
        private static MethodInfo stopClipMethod;

        public static void ClearPreviewCache()
        {
            ESAssetPackageGridAnimationFrameCache.Clear();
            PreviewCache.Clear();
            foreach (Texture2D texture in ModelPreviewCache.Values)
            {
                if (texture != null)
                    ESEditorPreviewUtility.DestroyObject(texture);
            }
            ModelPreviewCache.Clear();
            PreviewMissCounts.Clear();
            ShaderMaterialCache.Clear();
            if (previewFallbackMaterial != null)
            {
                ESEditorPreviewUtility.DestroyObject(previewFallbackMaterial);
                previewFallbackMaterial = null;
            }
        }

        public static Texture GetPreviewTexture(UnityEngine.Object asset, ESAssetPackageCategory category)
        {
            return GetPreviewTexture(asset, category, 160);
        }

        public static Texture GetPreviewTexture(UnityEngine.Object asset, ESAssetPackageCategory category, int previewPixels)
        {
            return GetPreviewTexture(asset, category, previewPixels, null);
        }

        public static Texture GetPreviewTexture(UnityEngine.Object asset, ESAssetPackageCategory category, int previewPixels, ESAssetPackageBakeData bake)
        {
            if (asset == null)
                return null;

            if (asset is Texture2D texture && category == ESAssetPackageCategory.Texture)
                return texture;

            string path = AssetDatabase.GetAssetPath(asset);
            if ((category == ESAssetPackageCategory.Model || category == ESAssetPackageCategory.Prefab) && asset is GameObject go)
            {
                int fallbackId = bake != null && bake.previewFallbackMaterial != null ? bake.previewFallbackMaterial.GetInstanceID() : 0;
                string key = path + "|" + Mathf.Clamp(previewPixels, 96, 256) + "|" + fallbackId;
                if (ModelPreviewCache.TryGetValue(key, out Texture2D modelCached) && modelCached != null)
                    return modelCached;

                Texture2D modelPreview = RenderGameObjectPreview(go, Mathf.Clamp(previewPixels, 96, 256), bake != null ? bake.previewFallbackMaterial : null);
                if (modelPreview != null)
                {
                    ModelPreviewCache[key] = modelPreview;
                    return modelPreview;
                }
            }

            if (!string.IsNullOrEmpty(path) && PreviewCache.TryGetValue(path, out Texture2D cached) && cached != null)
                return cached;

            Texture2D preview = AssetPreview.GetAssetPreview(asset);
            if (preview == null && AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID()))
            {
                QueuePreviewRepaint();
                return null;
            }

            if (preview == null && NeedsRichPreview(category))
            {
                string key = string.IsNullOrEmpty(path) ? asset.GetInstanceID().ToString() : path;
                PreviewMissCounts.TryGetValue(key, out int missCount);
                if (missCount < 90)
                {
                    if (Event.current == null || Event.current.type == EventType.Repaint)
                        PreviewMissCounts[key] = missCount + 1;
                    QueuePreviewRepaint();
                    return null;
                }
            }

            if (preview == null)
                preview = ESEditorPreviewUtility.GetAssetPreviewOrMini(asset, QueuePreviewRepaint);

            if (!string.IsNullOrEmpty(path) && preview != null)
            {
                PreviewCache[path] = preview;
                PreviewMissCounts.Remove(path);
            }

            return preview;
        }

        private static Texture2D RenderGameObjectPreview(GameObject source, int size, Material configuredFallbackMaterial)
        {
            if (source == null)
                return null;

            PreviewRenderUtility utility = null;
            GameObject instance = null;
            try
            {
                utility = new PreviewRenderUtility();
                utility.cameraFieldOfView = 28f;
                instance = UnityEngine.Object.Instantiate(source);
                ESEditorPreviewUtility.SetHideFlagsRecursive(instance.transform, ESEditorPreviewUtility.PreviewHideFlags);
                ESEditorPreviewUtility.TryMarkPreviewObject(instance, "AssetPackagePreview", "Asset package static preview model.", out _);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                ESEditorPreviewUtility.DisableRuntimeBehaviours(instance);
                ApplyPreviewFallbackMaterials(instance, configuredFallbackMaterial);
                utility.AddSingleGO(instance);

                Bounds bounds = ESEditorPreviewUtility.CalculateBounds(instance);
                Vector3 center = bounds.center;
                float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                utility.camera.aspect = 1f;
                utility.camera.transform.position = center + new Vector3(0f, radius * 0.25f, -radius * 2.8f);
                utility.camera.transform.LookAt(center);
                utility.camera.nearClipPlane = 0.01f;
                utility.camera.farClipPlane = radius * 10f;
                utility.camera.clearFlags = CameraClearFlags.Color;
                utility.camera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                utility.lights[0].intensity = 1.25f;
                utility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                utility.lights[1].intensity = 0.75f;

                Rect rect = new Rect(0f, 0f, size, size);
                utility.BeginPreview(rect, GUIStyle.none);
                utility.Render();
                Texture rendered = utility.EndPreview();
                return CopyPreviewTexture(rendered, size, size);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                utility?.Cleanup();
            }
        }

        private static Texture2D CopyPreviewTexture(Texture source, int width, int height)
        {
            return ESEditorPreviewUtility.CopyTexture(source, width, height, "ES Asset Package Static Preview");
        }

        public static void ApplyPreviewFallbackMaterials(GameObject root, Material configuredFallbackMaterial)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                bool changed = false;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (IsProblematicPreviewMaterial(materials[m]))
                    {
                        materials[m] = GetPreviewFallbackMaterial(materials[m], configuredFallbackMaterial);
                        changed = true;
                    }
                }

                if (changed)
                    renderers[i].sharedMaterials = materials;
            }
        }

        public static bool IsProblematicPreviewMaterial(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported)
                return true;

            string shaderName = material.shader.name;
            return shaderName.IndexOf("Hidden/InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static AnimationClip ResolveVisualMotionClip(AnimationClip clip)
        {
            if (clip == null)
                return null;

            string clipPath = AssetDatabase.GetAssetPath(clip);
            string siblingPath = FindSiblingVisualMotionAssetPath(clipPath);
            if (!string.IsNullOrEmpty(siblingPath))
            {
                AnimationClip siblingClip = Page_AssetPackageBakeCategory.LoadAnimationClipsFromAnyAssetPath(siblingPath)
                    .FirstOrDefault(c => c != null && !LooksLikeRootOnlyHumanoidClip(c));
                if (siblingClip != null)
                    return siblingClip;
            }

            if (!LooksLikeRootOnlyHumanoidClip(clip))
                return clip;

            AnimationClip sameAssetClip = Page_AssetPackageBakeCategory.LoadAnimationClipsFromAnyAssetPath(clipPath)
                .FirstOrDefault(c => c != null && c != clip && !LooksLikeRootOnlyHumanoidClip(c));
            if (sameAssetClip != null)
                return sameAssetClip;

            return clip;
        }

        private static string FindSiblingVisualMotionAssetPath(string clipPath)
        {
            if (string.IsNullOrEmpty(clipPath))
                return string.Empty;

            string directory = Path.GetDirectoryName(clipPath)?.Replace("\\", "/") ?? string.Empty;
            string fileName = Path.GetFileNameWithoutExtension(clipPath);
            string extension = Path.GetExtension(clipPath);
            if (fileName.EndsWith("_Root", StringComparison.OrdinalIgnoreCase))
            {
                string visualName = fileName.Substring(0, fileName.Length - "_Root".Length);
                string exactCandidate = directory + "/" + visualName + extension;
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(exactCandidate)))
                    return exactCandidate;

                string fuzzyCandidate = FindBestNonRootSiblingByName(directory, visualName, extension);
                if (!string.IsNullOrEmpty(fuzzyCandidate))
                    return fuzzyCandidate;
            }

            return string.Empty;
        }

        private static string FindBestNonRootSiblingByName(string directory, string visualName, string extension)
        {
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(visualName))
                return string.Empty;

            string fullDirectory = AssetPathToFullPathForPreview(directory);
            if (string.IsNullOrEmpty(fullDirectory) || !Directory.Exists(fullDirectory))
                return string.Empty;

            string[] files = Directory.GetFiles(fullDirectory, "*" + extension, SearchOption.TopDirectoryOnly);
            string bestAssetPath = string.Empty;
            int bestScore = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string candidateName = Path.GetFileNameWithoutExtension(files[i]);
                if (candidateName.EndsWith("_Root", StringComparison.OrdinalIgnoreCase))
                    continue;

                int score = ScoreVisualMotionSibling(visualName, candidateName);
                if (score <= bestScore)
                    continue;

                string assetPath = (directory + "/" + Path.GetFileName(files[i])).Replace("\\", "/");
                if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)))
                    continue;

                bestScore = score;
                bestAssetPath = assetPath;
            }

            return bestScore >= 30 ? bestAssetPath : string.Empty;
        }

        private static int ScoreVisualMotionSibling(string wantedName, string candidateName)
        {
            string[] wantedTokens = SplitMotionNameTokens(wantedName);
            string[] candidateTokens = SplitMotionNameTokens(candidateName);
            if (wantedTokens.Length == 0 || candidateTokens.Length == 0)
                return 0;

            int score = 0;
            for (int i = 0; i < wantedTokens.Length; i++)
            {
                string wanted = wantedTokens[i];
                for (int j = 0; j < candidateTokens.Length; j++)
                {
                    string candidate = candidateTokens[j];
                    if (string.Equals(wanted, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        score += IsDirectionToken(wanted) ? 14 : 8;
                        break;
                    }

                    if (wanted.Length >= 5 && candidate.Length >= 5 &&
                        (wanted.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0 ||
                         candidate.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        score += 5;
                        break;
                    }
                }
            }

            if (GetAfterAt(wantedName).StartsWith(GetAfterAt(candidateName).Substring(0, Mathf.Min(GetAfterAt(candidateName).Length, 4)), StringComparison.OrdinalIgnoreCase))
                score += 4;

            return score;
        }

        private static string[] SplitMotionNameTokens(string name)
        {
            if (string.IsNullOrEmpty(name))
                return Array.Empty<string>();

            string afterAt = GetAfterAt(name);
            return afterAt.Split(new[] { '_', '-', ' ', '@' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string GetAfterAt(string name)
        {
            int at = string.IsNullOrEmpty(name) ? -1 : name.IndexOf('@');
            return at >= 0 && at + 1 < name.Length ? name.Substring(at + 1) : name ?? string.Empty;
        }

        private static bool IsDirectionToken(string token)
        {
            return string.Equals(token, "Front", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Back", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Left", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "Right", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "L", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "R", StringComparison.OrdinalIgnoreCase) ||
                   token.EndsWith("45", StringComparison.OrdinalIgnoreCase) ||
                   token.EndsWith("90", StringComparison.OrdinalIgnoreCase) ||
                   token.EndsWith("180", StringComparison.OrdinalIgnoreCase);
        }

        private static string AssetPathToFullPathForPreview(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            return string.IsNullOrEmpty(projectRoot) ? string.Empty : Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static bool LooksLikeRootOnlyHumanoidClip(AnimationClip clip)
        {
            if (clip == null || !clip.humanMotion)
                return false;

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length == 0)
                return false;

            int humanoidPoseCurves = 0;
            int rootOrIkCurves = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                string property = bindings[i].propertyName ?? string.Empty;
                if (IsRootOrIkHumanoidPropertyForDebug(property))
                    rootOrIkCurves++;
                else
                    humanoidPoseCurves++;
            }

            return rootOrIkCurves > 0 && humanoidPoseCurves == 0;
        }

        internal static bool IsRootOrIkHumanoidPropertyForDebug(string propertyName)
        {
            return propertyName.StartsWith("RootT.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("RootQ.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("LeftFootT.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("LeftFootQ.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("RightFootT.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("RightFootQ.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("LeftHandT.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("LeftHandQ.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("RightHandT.", StringComparison.Ordinal) ||
                   propertyName.StartsWith("RightHandQ.", StringComparison.Ordinal);
        }

        private static Material GetPreviewFallbackMaterial(Material source, Material configuredFallbackMaterial)
        {
            if (configuredFallbackMaterial != null)
                return configuredFallbackMaterial;

            if (previewFallbackMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                                Shader.Find("Universal Render Pipeline/Simple Lit") ??
                                Shader.Find("HDRP/Lit") ??
                                Shader.Find("Standard") ??
                                Shader.Find("Diffuse");
                previewFallbackMaterial = new Material(shader);
                previewFallbackMaterial.hideFlags = HideFlags.HideAndDontSave;
                previewFallbackMaterial.color = source != null && source.HasProperty("_Color") ? source.color : new Color(0.72f, 0.72f, 0.72f, 1f);
            }

            return previewFallbackMaterial;
        }

        private static bool NeedsRichPreview(ESAssetPackageCategory category)
        {
            return category == ESAssetPackageCategory.Prefab ||
                   category == ESAssetPackageCategory.Model ||
                   category == ESAssetPackageCategory.Material ||
                   category == ESAssetPackageCategory.Texture;
        }

        private static void QueuePreviewRepaint()
        {
            EditorApplication.delayCall -= RepaintAssetPackageWindow;
            EditorApplication.delayCall += RepaintAssetPackageWindow;
        }

        private static void RepaintAssetPackageWindow()
        {
            ESAssetPackageBakeWindow.UsingWindow?.Repaint();
        }

        public static void DrawTextureDetail(Texture2D texture, string path)
        {
            DrawLargePreview(texture, 240);
            if (texture != null)
            {
                EditorGUILayout.LabelField("尺寸", $"{texture.width} x {texture.height}");
                EditorGUILayout.LabelField("格式", texture.format.ToString());
            }

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                EditorGUILayout.LabelField("TextureType", importer.textureType.ToString());
                EditorGUILayout.LabelField("最大尺寸", importer.maxTextureSize.ToString());
                EditorGUILayout.LabelField("Mipmap", importer.mipmapEnabled ? "开启" : "关闭");
                EditorGUILayout.LabelField("压缩", importer.textureCompression.ToString());
                EditorGUILayout.LabelField("Read/Write", importer.isReadable ? "开启" : "关闭");
            }
        }

        public static void DrawMaterialDetail(Material material)
        {
            DrawLargePreview(material, 180);
            if (material == null)
                return;

            EditorGUILayout.LabelField("Shader", material.shader != null ? material.shader.name : "<无>");
            if (material.shader == null || !material.shader.isSupported)
                EditorGUILayout.HelpBox("材质 Shader 缺失或当前工程不支持，模型会显示为紫色。", MessageType.Error);
            else if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null && material.shader.name == "Standard")
                EditorGUILayout.HelpBox("当前工程使用 SRP 管线，但材质仍是内置 Standard Shader，预览或运行时可能显示紫色。建议转换到当前管线的 Lit/Simple Lit。", MessageType.Warning);

            EditorGUILayout.ObjectField("主贴图", material.mainTexture, typeof(Texture), false);
            if (material.mainTexture == null)
                EditorGUILayout.HelpBox("材质没有绑定主贴图。如果模型依赖贴图表现，这个材质本身是不完整的。", MessageType.Warning);

            EditorGUILayout.LabelField("RenderQueue", material.renderQueue.ToString());
            EditorGUILayout.LabelField("Pass", material.passCount.ToString());
        }

        public static void DrawObjectPreviewDetail(UnityEngine.Object asset, string path)
        {
            DrawLargePreview(asset, 220);
            string[] deps = string.IsNullOrEmpty(path) ? Array.Empty<string>() : AssetDatabase.GetDependencies(path, false);
            EditorGUILayout.LabelField("直接依赖", deps.Length.ToString());
            for (int i = 0; i < Mathf.Min(8, deps.Length); i++)
                EditorGUILayout.LabelField(Path.GetFileName(deps[i]), EditorStyles.miniLabel);
        }

        public static void DrawAudioDetail(AudioClip clip, string path)
        {
            DrawLargePreview(clip, 96);
            if (clip != null)
            {
                EditorGUILayout.LabelField("时长", $"{clip.length:F2} 秒");
                EditorGUILayout.LabelField("声道", clip.channels.ToString());
                EditorGUILayout.LabelField("频率", clip.frequency.ToString());
                EditorGUILayout.LabelField("采样数", clip.samples.ToString());
            }

            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                EditorGUILayout.LabelField("加载方式", settings.loadType.ToString());
                EditorGUILayout.LabelField("压缩格式", settings.compressionFormat.ToString());
                EditorGUILayout.LabelField("质量", settings.quality.ToString("F2"));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(clip == null))
                {
                    if (GUILayout.Button("播放"))
                        PlayAudioClip(clip);
                    if (GUILayout.Button("停止"))
                        StopAudioClip();
                }
            }
        }

        public static void DrawAnimationClipInfo(AnimationClip clip)
        {
            DrawLargePreview(clip, 96);
            if (clip == null)
                return;

            EditorGUILayout.LabelField("时长", $"{clip.length:F2} 秒");
            EditorGUILayout.LabelField("帧率", clip.frameRate.ToString("F1"));
            EditorGUILayout.LabelField("循环", clip.isLooping ? "是" : "否");
            EditorGUILayout.LabelField("曲线", AnimationUtility.GetCurveBindings(clip).Length.ToString());
            EditorGUILayout.LabelField("对象引用曲线", AnimationUtility.GetObjectReferenceCurveBindings(clip).Length.ToString());
        }

        public static void DrawShaderDetail(Shader shader, ESAssetPackageBakeData bake)
        {
            DrawLargePreview(shader, 96);
            if (shader == null)
                return;

            EditorGUILayout.LabelField("名称", shader.name);
            EditorGUILayout.LabelField("支持", shader.isSupported ? "是" : "否");
            EditorGUILayout.LabelField("LOD", shader.maximumLOD.ToString());
            DrawShaderKeywords(shader);

            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (!ShaderMaterialCache.TryGetValue(shaderPath, out List<string> materialPaths))
            {
                materialPaths = new List<string>();
                if (bake != null && bake.records != null)
                {
                    foreach (var record in bake.records)
                    {
                        if (record == null || record.category != ESAssetPackageCategory.Material)
                            continue;

                        var mat = record.LoadAsset() as Material;
                        if (mat != null && mat.shader == shader)
                            materialPaths.Add(record.assetPath);
                    }
                }
                ShaderMaterialCache[shaderPath] = materialPaths;
            }

            EditorGUILayout.LabelField("包内引用材质", materialPaths.Count.ToString());
            for (int i = 0; i < Mathf.Min(10, materialPaths.Count); i++)
                EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(materialPaths[i]), EditorStyles.miniLabel);
        }

        private static void DrawShaderKeywords(Shader shader)
        {
            try
            {
                object keywordSpace = typeof(Shader).GetProperty("keywordSpace")?.GetValue(shader, null);
                string[] keywordNames = keywordSpace?.GetType().GetProperty("keywordNames")?.GetValue(keywordSpace, null) as string[];
                if (keywordNames == null || keywordNames.Length == 0)
                {
                    EditorGUILayout.LabelField("关键字", "无");
                    return;
                }

                EditorGUILayout.LabelField("关键字数量", keywordNames.Length.ToString());
                for (int i = 0; i < Mathf.Min(8, keywordNames.Length); i++)
                    EditorGUILayout.LabelField(keywordNames[i], EditorStyles.miniLabel);
            }
            catch
            {
                EditorGUILayout.LabelField("关键字", "当前 Unity 版本无法读取");
            }
        }

        public static void DrawFontDetail(UnityEngine.Object asset)
        {
            DrawLargePreview(asset, 120);
            if (asset is Font font)
            {
                EditorGUILayout.LabelField("字体名", font.fontNames != null && font.fontNames.Length > 0 ? string.Join(", ", font.fontNames) : font.name);
                EditorGUILayout.LabelField("动态字体", font.dynamic ? "是" : "否");
                EditorGUILayout.LabelField("材质", font.material != null ? font.material.name : "<无>");
            }
        }

        public static void DrawVideoDetail(UnityEngine.Object asset)
        {
            DrawLargePreview(asset, 160);
            if (asset is UnityEngine.Video.VideoClip video)
            {
                EditorGUILayout.LabelField("时长", $"{video.length:F2} 秒");
                EditorGUILayout.LabelField("尺寸", $"{video.width} x {video.height}");
                EditorGUILayout.LabelField("帧率", video.frameRate.ToString("F2"));
                EditorGUILayout.LabelField("帧数", video.frameCount.ToString());
            }
        }

        private static void DrawLargePreview(UnityEngine.Object asset, float height)
        {
            Texture preview = GetPreviewTexture(asset, ESAssetPackageCategory.Other);
            DrawLargePreview(preview, height);
        }

        private static void DrawLargePreview(Texture texture, float height)
        {
            Rect rect = GUILayoutUtility.GetRect(260, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.075f, 0.08f, 0.09f, 1f));
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            else
                GUI.Label(rect, "无预览", EditorStyles.centeredGreyMiniLabel);
        }

        private static void PlayAudioClip(AudioClip clip)
        {
            if (clip == null)
                return;

            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            playClipMethod ??= audioUtil?.GetMethod("PlayPreviewClip", flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? audioUtil?.GetMethod("PlayClip", flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? audioUtil?.GetMethod("PlayClip", flags, null, new[] { typeof(AudioClip) }, null);

            if (playClipMethod == null)
                return;

            ParameterInfo[] parameters = playClipMethod.GetParameters();
            if (parameters.Length == 3)
                playClipMethod.Invoke(null, new object[] { clip, 0, false });
            else
                playClipMethod.Invoke(null, new object[] { clip });
        }

        private static void StopAudioClip()
        {
            Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            stopClipMethod ??= audioUtil?.GetMethod("StopAllPreviewClips", flags)
                ?? audioUtil?.GetMethod("StopAllClips", flags);
            stopClipMethod?.Invoke(null, null);
        }
    }

    internal enum ESAssetPackagePreviewBaselinePlatform
    {
        Desktop,
        Mobile,
        Fast
    }

    internal static class ESAssetPackagePreviewWorkflow
    {
        private const double ReloadDomainPersistentFrameBlockSeconds = 8d;

        public static void RegisterLifecycle()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseForAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseForAssemblyReload;
            EditorApplication.quitting -= ReleaseForEditorQuit;
            EditorApplication.quitting += ReleaseForEditorQuit;
            ESAssetPackageGridAnimationFrameCache.BlockPersistentFrameLoading(ReloadDomainPersistentFrameBlockSeconds);
        }

        public static void RefreshStaticPreviewCache(int assetPreviewTextureCacheSize)
        {
            ClearAllInMemoryPreviewData(unloadUnusedAssets: false);
            AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(256, assetPreviewTextureCacheSize));
        }

        public static void ClearGridFrameMemory()
        {
            ESAssetPackageGridAnimationFrameCache.Clear();
        }

        public static string GetGridFrameLoadingStatus()
        {
            return ESAssetPackageGridAnimationFrameCache.GetPersistentFrameLoadingStatus();
        }

        public static UnityEngine.Object ResolveConfiguredAnimationPreviewModel(ESAssetPackageBakeData bake)
        {
            if (bake != null && bake.animationPreviewModel != null)
                return bake.animationPreviewModel;

            StateMachineConfig stateMachineConfig = StateMachineConfig.Instance;
            if (stateMachineConfig != null && stateMachineConfig.previewModel != null)
                return ResolvePreviewGameObject(stateMachineConfig.previewModel) ?? stateMachineConfig.previewModel;

            return null;
        }

        public static UnityEngine.Object LoadRememberedPreviewModel(string editorPrefsKey)
        {
            string guid = EditorPrefs.GetString(editorPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(guid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static Avatar ResolveAnimationPreviewAvatar(ESAssetPackageBakeData bake)
        {
            if (bake != null && bake.animationPreviewAvatar != null)
                return bake.animationPreviewAvatar;

            StateMachineConfig stateMachineConfig = StateMachineConfig.Instance;
            return stateMachineConfig != null ? stateMachineConfig.previewAvatar : null;
        }

        public static Material ResolveAnimationPreviewFallbackMaterial(ESAssetPackageBakeData bake)
        {
            if (bake != null && bake.previewFallbackMaterial != null)
                return bake.previewFallbackMaterial;

            StateMachineConfig stateMachineConfig = StateMachineConfig.Instance;
            return stateMachineConfig != null ? stateMachineConfig.previewFallbackMaterial : null;
        }

        public static GameObject ResolvePreviewGameObject(UnityEngine.Object source)
        {
            if (source is GameObject go)
                return go;

            string path = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        public static bool CanUseAsAnimationPreviewModel(UnityEngine.Object source)
        {
            GameObject go = ResolvePreviewGameObject(source);
            return go != null && go.GetComponentInChildren<Animator>(true) != null;
        }

        private static void ReleaseForAssemblyReload()
        {
            ESAssetPackageGridAnimationFrameCache.BlockPersistentFrameLoading(ReloadDomainPersistentFrameBlockSeconds);
            ReleaseAllPreviewResources("AssemblyReload");
        }

        private static void ReleaseForEditorQuit()
        {
            ReleaseAllPreviewResources("EditorQuit");
        }

        private static void ReleaseAllPreviewResources(string reason)
        {
            try
            {
                foreach (ESAssetPackageBakeWindow window in Resources.FindObjectsOfTypeAll<ESAssetPackageBakeWindow>())
                    window?.ReleaseInstancePreviewResources();
                foreach (ESAssetPackageRecordPreviewWindow window in Resources.FindObjectsOfTypeAll<ESAssetPackageRecordPreviewWindow>())
                    window?.ReleasePreviewResources();

                ClearAllInMemoryPreviewData(unloadUnusedAssets: true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ESAssetBakePreviewWorkflow] Release preview resources failed. reason=" + reason + " error=" + ex.Message);
            }
        }

        private static void ClearAllInMemoryPreviewData(bool unloadUnusedAssets)
        {
            ESAssetPackagePreviewUtility.ClearPreviewCache();
            if (unloadUnusedAssets)
                EditorUtility.UnloadUnusedAssetsImmediate(false);
        }
    }

    internal sealed class ESAssetPackagePreviewSceneContext : IDisposable
    {
        public const int PreviewRenderLayer = 31;
        private const string Owner = "AssetPackagePreview";
        private const float PreviewGroupSpacing = 100f;
        private const float PreviewCameraFarClip = 80f;
        private const int MaxCellProbeAttempts = 4096;
        private static readonly object AllocationLock = new object();
        private static readonly HashSet<Vector2Int> OccupiedCells = new HashSet<Vector2Int>();
        private static readonly Queue<Vector2Int> ReleasedCells = new Queue<Vector2Int>();
        private static int nextAllocationId;
        private readonly bool usePreviewScene;
        private readonly int allocationId;
        private readonly Vector2Int allocatedCell;
        private readonly Vector3 groupOrigin;
        private readonly string allocationReport;
        private Scene previewScene;
        private GameObject cameraObject;
        private GameObject keyLightObject;
        private GameObject fillLightObject;
        private RenderTexture renderTexture;
        private int renderTextureWidth;
        private int renderTextureHeight;
        private ESAssetPackagePreviewBaselinePlatform renderTexturePlatform;
        private double lastRenderTime;
        private bool disposed;

        public Camera Camera { get; private set; }
        public string LastStatus { get; private set; } = "Preview context not created.";
        public string LastMarkerStatus { get; private set; } = "Cleanup marker not requested.";
        public string LastObjectFlowStatus { get; private set; } = "Preview object flow not requested.";
        public bool CleanupMarkerAvailable { get; private set; }
        public int MarkedObjectCount { get; private set; }
        public bool CameraSceneBound { get; private set; }
        public bool UsePreviewScene => usePreviewScene;
        public Vector3 GroupOrigin => groupOrigin;
        public bool IsReady => Camera != null && (!usePreviewScene || previewScene.IsValid());
        public string IsolationReport => IsReady
            ? (usePreviewScene
                ? "PreviewScene=" + previewScene.name + ", Layer=" + PreviewRenderLayer + ", CameraMask=0x" + Camera.cullingMask.ToString("X") + ", CameraScene=" + (CameraSceneBound ? "bound" : "layer-only")
                : "NormalEditorScene, Layer=" + PreviewRenderLayer + ", CameraMask=0x" + Camera.cullingMask.ToString("X") + ", CameraScene=normal-scene, Origin=" + FormatVector(groupOrigin) + ", Cell=" + allocatedCell + ", FarClip=" + PreviewCameraFarClip.ToString("F0") + "m, " + allocationReport)
            : (usePreviewScene ? "PreviewScene not ready." : "Normal editor preview context not ready.");

        public ESAssetPackagePreviewSceneContext(bool usePreviewScene = true)
        {
            this.usePreviewScene = usePreviewScene;
            allocationId = System.Threading.Interlocked.Increment(ref nextAllocationId);
            allocatedCell = AllocateCell(allocationId, out allocationReport);
            groupOrigin = new Vector3(allocatedCell.x * PreviewGroupSpacing, 0f, allocatedCell.y * PreviewGroupSpacing);
        }

        public void Ensure()
        {
            if (Camera != null && (!usePreviewScene || previewScene.IsValid()))
                return;

            EnsurePreviewScene();
            CreateCamera();
            CreateLights();
            LastStatus = usePreviewScene
                ? "Preview context ready. Scene isolated, layer locked."
                : "Preview context ready. Normal editor scene hidden objects, layer locked.";
        }

        public void PreparePreviewObject(GameObject obj)
        {
            if (obj == null)
                return;

            Ensure();
            ApplyPreviewObjectLifecycle(obj, "Asset package preview model.", samplingTarget: true);
        }

        public bool MoveToPreviewScene(GameObject obj)
        {
            if (obj == null)
                return false;

            if (!usePreviewScene)
                return MoveToActiveScene(obj);

            EnsurePreviewScene();
            SceneManager.MoveGameObjectToScene(obj, previewScene);
            return obj.scene == previewScene;
        }

        public bool Render(Rect rect, Vector3 center, float radius, float renderScale, float yaw, float pitch, float zoom, ESAssetPackagePreviewBaselinePlatform baselinePlatform, double minRenderInterval = 0d)
        {
            Ensure();
            if (Camera == null)
                return false;

            Camera.aspect = Mathf.Max(0.25f, rect.width / Mathf.Max(1f, rect.height));
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            float distance = Mathf.Max(1.2f, radius * 2.8f * zoom);
            Camera.transform.position = center + orbit * new Vector3(0f, radius * 0.18f, distance);
            Camera.transform.LookAt(center);
            Camera.nearClipPlane = 0.01f;
            Camera.farClipPlane = PreviewCameraFarClip;
            Camera.cullingMask = 1 << PreviewRenderLayer;
            CameraSceneBound = usePreviewScene && TrySetCameraScene(Camera, previewScene);
            ApplyCameraPlatform(Camera, baselinePlatform);

            if (Event.current == null || Event.current.type != EventType.Repaint)
                return true;

            float scale = Mathf.Clamp(EditorGUIUtility.pixelsPerPoint * renderScale, 1f, 4f);
            int width = Mathf.Max(1, Mathf.CeilToInt(rect.width * scale));
            int height = Mathf.Max(1, Mathf.CeilToInt(rect.height * scale));
            EnsureRenderTexture(width, height, baselinePlatform);
            if (renderTexture == null)
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (minRenderInterval > 0d && lastRenderTime > 0d && now - lastRenderTime < minRenderInterval)
            {
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
                return true;
            }

            RenderTexture oldTarget = Camera.targetTexture;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                Camera.targetTexture = renderTexture;
                Camera.Render();
                lastRenderTime = now;
                GUI.DrawTexture(rect, renderTexture, ScaleMode.StretchToFill, false);
            }
            finally
            {
                Camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
            }

            return true;
        }

        public Texture2D RenderSnapshot(int width, int height, Vector3 center, float radius, float yaw, float pitch, float zoom, ESAssetPackagePreviewBaselinePlatform baselinePlatform)
        {
            Ensure();
            if (Camera == null)
                return null;

            width = Mathf.Clamp(width, 64, 1024);
            height = Mathf.Clamp(height, 64, 1024);
            Camera.aspect = width / (float)Mathf.Max(1, height);
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            float distance = Mathf.Max(1.2f, radius * 2.8f * zoom);
            Camera.transform.position = center + orbit * new Vector3(0f, radius * 0.18f, distance);
            Camera.transform.LookAt(center);
            Camera.nearClipPlane = 0.01f;
            Camera.farClipPlane = PreviewCameraFarClip;
            Camera.cullingMask = 1 << PreviewRenderLayer;
            CameraSceneBound = usePreviewScene && TrySetCameraScene(Camera, previewScene);
            ApplyCameraPlatform(Camera, baselinePlatform);
            EnsureRenderTexture(width, height, baselinePlatform);
            if (renderTexture == null)
                return null;

            return ESEditorPreviewUtility.RenderCameraSnapshot(
                Camera,
                renderTexture,
                width,
                height,
                "ES Asset Package Animation Grid Frame");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ReleaseRenderTexture();
            DestroyObject(cameraObject);
            DestroyObject(keyLightObject);
            DestroyObject(fillLightObject);
            Camera = null;
            ReleaseCell(allocatedCell);

            if (previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                previewScene = default;
            }
        }

        private void EnsurePreviewScene()
        {
            if (!usePreviewScene)
                return;

            if (previewScene.IsValid())
                return;

            previewScene = EditorSceneManager.NewPreviewScene();
        }

        private void CreateCamera()
        {
            if (Camera != null)
                return;

            cameraObject = usePreviewScene
                ? EditorUtility.CreateGameObjectWithHideFlags("ES Asset Package Preview Camera", HideFlags.HideAndDontSave, typeof(Camera))
                : new GameObject("ES Asset Package Preview Camera", typeof(Camera));
            ApplyPreviewObjectLifecycle(cameraObject, "Asset package preview camera.", samplingTarget: false);
            Camera = cameraObject.GetComponent<Camera>();
            Camera.enabled = false;
            Camera.fieldOfView = 30f;
            Camera.clearFlags = CameraClearFlags.Color;
            Camera.backgroundColor = new Color(0.06f, 0.065f, 0.075f, 1f);
            Camera.cullingMask = 1 << PreviewRenderLayer;
            Camera.allowHDR = true;
            Camera.allowMSAA = true;
            Camera.renderingPath = RenderingPath.Forward;
            CameraSceneBound = usePreviewScene && TrySetCameraScene(Camera, previewScene);
            AddAndConfigureUniversalCameraData(Camera);
        }

        private void CreateLights()
        {
            if (keyLightObject == null)
                keyLightObject = CreateDirectionalLight("ES Asset Package Preview Key Light", 1.2f, Quaternion.Euler(35f, 35f, 0f));
            if (fillLightObject == null)
                fillLightObject = CreateDirectionalLight("ES Asset Package Preview Fill Light", 0.55f, Quaternion.Euler(340f, 210f, 0f));
        }

        private GameObject CreateDirectionalLight(string name, float intensity, Quaternion rotation)
        {
            GameObject go = usePreviewScene
                ? EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, typeof(Light))
                : new GameObject(name, typeof(Light));
            ApplyPreviewObjectLifecycle(go, "Asset package preview light.", samplingTarget: false);
            Light light = go.GetComponent<Light>();
            if (usePreviewScene)
            {
                light.type = LightType.Directional;
                light.intensity = intensity;
            }
            else
            {
                light.type = LightType.Spot;
                light.intensity = intensity * 5f;
                light.range = 60f;
                light.spotAngle = 75f;
            }
            light.cullingMask = 1 << PreviewRenderLayer;
            light.transform.rotation = rotation;
            if (!usePreviewScene)
                light.transform.position = groupOrigin - light.transform.forward * 18f + Vector3.up * 8f;
            return go;
        }

        private void EnsureRenderTexture(int width, int height, ESAssetPackagePreviewBaselinePlatform baselinePlatform)
        {
            if (renderTexture != null && renderTextureWidth == width && renderTextureHeight == height && renderTexturePlatform == baselinePlatform)
                return;

            ReleaseRenderTexture();
            renderTextureWidth = width;
            renderTextureHeight = height;
            renderTexturePlatform = baselinePlatform;
            renderTexture = ESEditorPreviewUtility.CreateRenderTexture(
                width,
                height,
                24,
                GetAntiAliasing(baselinePlatform),
                "ES Asset Package Preview RT");
        }

        private static int GetAntiAliasing(ESAssetPackagePreviewBaselinePlatform baselinePlatform)
        {
            switch (baselinePlatform)
            {
                case ESAssetPackagePreviewBaselinePlatform.Desktop:
                    return 8;
                case ESAssetPackagePreviewBaselinePlatform.Mobile:
                    return 4;
                case ESAssetPackagePreviewBaselinePlatform.Fast:
                    return 1;
                default:
                    return Mathf.Max(4, QualitySettings.antiAliasing);
            }
        }

        private static void ApplyCameraPlatform(Camera camera, ESAssetPackagePreviewBaselinePlatform baselinePlatform)
        {
            if (camera == null)
                return;

            switch (baselinePlatform)
            {
                case ESAssetPackagePreviewBaselinePlatform.Desktop:
                    camera.allowHDR = true;
                    camera.allowMSAA = true;
                    break;
                case ESAssetPackagePreviewBaselinePlatform.Mobile:
                    camera.allowHDR = false;
                    camera.allowMSAA = true;
                    break;
                case ESAssetPackagePreviewBaselinePlatform.Fast:
                    camera.allowHDR = false;
                    camera.allowMSAA = false;
                    break;
            }
        }

        private void ReleaseRenderTexture()
        {
            ESEditorPreviewUtility.ReleaseRenderTexture(ref renderTexture);
            renderTextureWidth = 0;
            renderTextureHeight = 0;
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            ESEditorPreviewUtility.DestroyObject(obj);
        }

        private static Vector2Int AllocateCell(int allocationId, out string report)
        {
            unchecked
            {
                int seed = allocationId * 73856093 ^ Environment.TickCount * 19349663 ^ Guid.NewGuid().GetHashCode();
                var random = new System.Random(seed);
                lock (AllocationLock)
                {
                    while (ReleasedCells.Count > 0)
                    {
                        Vector2Int reusable = ReleasedCells.Dequeue();
                        if (OccupiedCells.Contains(reusable))
                            continue;

                        OccupiedCells.Add(reusable);
                        report = "CellAlloc=reused, Cell=" + reusable + ", Free=" + ReleasedCells.Count + ", Occupied=" + OccupiedCells.Count;
                        return reusable;
                    }

                    for (int attempt = 0; attempt < MaxCellProbeAttempts; attempt++)
                    {
                        int hash = seed ^ (attempt * 83492791);
                        int ring = 1 + Mathf.Abs(hash % 128);
                        int x = ((hash >> 8) % (ring * 2 + 1)) - ring + random.Next(-2, 3);
                        int y = ((hash >> 20) % (ring * 2 + 1)) - ring + random.Next(-2, 3);
                        var candidate = new Vector2Int(x, y);
                        if (OccupiedCells.Contains(candidate))
                            continue;

                        OccupiedCells.Add(candidate);
                        report = "CellAlloc=hash-random, Attempt=" + attempt + ", Occupied=" + OccupiedCells.Count;
                        return candidate;
                    }

                    for (int ring = 0; ring < 512; ring++)
                    {
                        for (int x = -ring; x <= ring; x++)
                        {
                            var top = new Vector2Int(x, ring);
                            if (!OccupiedCells.Contains(top))
                            {
                                OccupiedCells.Add(top);
                                report = "CellAlloc=spiral-fallback, Ring=" + ring + ", Occupied=" + OccupiedCells.Count;
                                return top;
                            }

                            var bottom = new Vector2Int(x, -ring);
                            if (!OccupiedCells.Contains(bottom))
                            {
                                OccupiedCells.Add(bottom);
                                report = "CellAlloc=spiral-fallback, Ring=" + ring + ", Occupied=" + OccupiedCells.Count;
                                return bottom;
                            }
                        }

                        for (int y = -ring + 1; y <= ring - 1; y++)
                        {
                            var left = new Vector2Int(-ring, y);
                            if (!OccupiedCells.Contains(left))
                            {
                                OccupiedCells.Add(left);
                                report = "CellAlloc=spiral-fallback, Ring=" + ring + ", Occupied=" + OccupiedCells.Count;
                                return left;
                            }

                            var right = new Vector2Int(ring, y);
                            if (!OccupiedCells.Contains(right))
                            {
                                OccupiedCells.Add(right);
                                report = "CellAlloc=spiral-fallback, Ring=" + ring + ", Occupied=" + OccupiedCells.Count;
                                return right;
                            }
                        }
                    }
                }
            }

            report = "CellAlloc=fallback-zero, Occupied=unknown";
            return Vector2Int.zero;
        }

        private static void ReleaseCell(Vector2Int cell)
        {
            lock (AllocationLock)
            {
                if (!OccupiedCells.Remove(cell))
                    return;

                ReleasedCells.Enqueue(cell);
            }
        }

        private void ApplyPreviewObjectLifecycle(GameObject obj, string note, bool samplingTarget)
        {
            if (obj == null)
                return;

            bool movedBeforeHide = usePreviewScene ? MoveToPreviewScene(obj) : MoveToActiveScene(obj);
            Scene sceneBeforeHide = obj.scene;
            HideFlags hideFlags = GetPreviewObjectHideFlags(samplingTarget);
            ESEditorPreviewUtility.SetHideFlagsRecursive(obj.transform, hideFlags);
            bool movedAfterHide = usePreviewScene ? obj.scene == previewScene : MoveToActiveScene(obj);
            ESEditorPreviewUtility.SetLayerRecursive(obj.transform, PreviewRenderLayer);
            RegisterCleanupMarker(obj, note);

            LastObjectFlowStatus =
                "Object=" + obj.name
                + ", HideFlags=" + hideFlags
                + ", SamplingTarget=" + samplingTarget
                + ", HidePolicy=" + (samplingTarget ? "SamplingSafeDontSave" : "HideAndDontSave")
                + ", SceneBeforeHide=" + FormatScene(sceneBeforeHide)
                + ", SceneAfterHide=" + FormatScene(obj.scene)
                + ", MoveBeforeHide=" + movedBeforeHide
                + ", MoveAfterHide=" + movedAfterHide
                + ", Layer=" + PreviewRenderLayer;
        }

        private HideFlags GetPreviewObjectHideFlags(bool samplingTarget)
        {
            if (!samplingTarget)
                return HideFlags.HideAndDontSave;

            return HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.DontUnloadUnusedAsset;
        }

        private static bool MoveToActiveScene(GameObject obj)
        {
            if (obj == null)
                return false;

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
                return obj.scene.IsValid();

            if (obj.scene != activeScene)
            {
                try
                {
                    SceneManager.MoveGameObjectToScene(obj, activeScene);
                }
                catch
                {
                }
            }

            return obj.scene.IsValid();
        }

        private static string FormatScene(Scene scene)
        {
            if (!scene.IsValid())
                return "<invalid>";

            return string.IsNullOrEmpty(scene.name) ? "<untitled-active-scene>" : scene.name;
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("F1") + ", " + value.y.ToString("F1") + ", " + value.z.ToString("F1") + ")";
        }

        private void RegisterCleanupMarker(GameObject obj, string note)
        {
            if (obj == null)
                return;

            CleanupMarkerAvailable = ESEditorPreviewUtility.TryMarkPreviewObject(obj, Owner, note, out string markerStatus);
            if (CleanupMarkerAvailable)
            {
                MarkedObjectCount++;
                LastMarkerStatus = "Registered " + MarkedObjectCount + " preview objects to ES cleanup scope.";
                return;
            }

            LastMarkerStatus = markerStatus;
        }

        private static bool TrySetCameraScene(Camera camera, Scene scene)
        {
            if (camera == null || !scene.IsValid())
                return false;

            PropertyInfo sceneProperty = typeof(Camera).GetProperty("scene", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (sceneProperty != null && sceneProperty.CanWrite && sceneProperty.PropertyType == typeof(Scene))
            {
                sceneProperty.SetValue(camera, scene);
                return true;
            }

            return false;
        }

        private static void AddAndConfigureUniversalCameraData(Camera camera)
        {
            if (camera == null)
                return;

            Type cameraDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (cameraDataType == null)
                return;

            Component cameraData = camera.GetComponent(cameraDataType);
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent(cameraDataType);

            SetBoolPropertyIfExists(cameraData, "renderPostProcessing", false);
            SetBoolPropertyIfExists(cameraData, "renderShadows", true);
            SetEnumPropertyIfExists(cameraData, "renderType", "Base");
        }

        private static void SetBoolPropertyIfExists(object target, string propertyName, bool value)
        {
            PropertyInfo property = target?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
                property.SetValue(target, value);
        }

        private static void SetEnumPropertyIfExists(object target, string propertyName, string valueName)
        {
            PropertyInfo property = target?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum)
                return;

            try
            {
                object enumValue = Enum.Parse(property.PropertyType, valueName);
                property.SetValue(target, enumValue);
            }
            catch
            {
            }
        }
    }

    internal static class ESAssetPackageGridAnimationFrameCache
    {
        private const string CacheVersion = "PersistentGridFrames_v2_ViewAware";
        private const string PersistentRootFolder = "ESAssetPackagePreviewFrames/AssetPackageBake";
        private const int ShortClipSampleRate = 24;
        private const int LongClipSampleRate = 10;
        private const float LongClipThresholdSeconds = 2.4f;
        private const int GridMinFrameCount = 24;
        private const int GridMaxFrameCount = 36;
        public const int GridMaxPixels = 128;
        private const int MaxEntries = 48;
        private const int MaxFramesPerEditorUpdate = 1;
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
        private static readonly List<Entry> BuildQueue = new List<Entry>();
        private static Entry ActiveBuildEntry;
        private static bool updateRegistered;
        private static string visiblePriorityScope = string.Empty;
        private static int visiblePriorityGeneration;
        private static long queueSerial;
        private static double blockPersistentFrameLoadingUntil;

        public static int GetGridFrameCount(AnimationClip clip)
        {
            return ResolveFrameCount(clip, GridMinFrameCount, GridMaxFrameCount);
        }

        public static void BlockPersistentFrameLoading(double seconds)
        {
            blockPersistentFrameLoadingUntil = Math.Max(blockPersistentFrameLoadingUntil, EditorApplication.timeSinceStartup + Math.Max(0d, seconds));
        }

        private static bool IsPersistentFrameLoadingBlocked()
        {
            return EditorApplication.isCompiling ||
                   EditorApplication.isUpdating ||
                   EditorApplication.timeSinceStartup < blockPersistentFrameLoadingUntil;
        }

        public static string GetPersistentFrameLoadingStatus()
        {
            if (EditorApplication.isCompiling)
                return "ReloadDomain保护：Unity正在编译，暂停加载小格子磁盘帧，避免PNG解码造成内存峰值。";
            if (EditorApplication.isUpdating)
                return "ReloadDomain保护：AssetDatabase正在刷新，暂停加载小格子磁盘帧。";

            double remaining = blockPersistentFrameLoadingUntil - EditorApplication.timeSinceStartup;
            if (remaining > 0d)
                return "ReloadDomain保护：约 " + remaining.ToString("F1") + " 秒后恢复加载小格子磁盘帧。";

            return string.Empty;
        }

        public static int BeginVisiblePagePriority(string scope)
        {
            scope ??= string.Empty;
            if (string.Equals(visiblePriorityScope, scope, StringComparison.Ordinal))
                return visiblePriorityGeneration;

            visiblePriorityScope = scope;
            visiblePriorityGeneration++;
            if (visiblePriorityGeneration <= 0)
                visiblePriorityGeneration = 1;

            if (ActiveBuildEntry != null && !ActiveBuildEntry.IsReady && ActiveBuildEntry.CanBuild)
            {
                QueueBuild(ActiveBuildEntry, ActiveBuildEntry.PriorityGeneration, ActiveBuildEntry.PriorityIndex);
                ActiveBuildEntry = null;
            }

            RegisterUpdate();
            return visiblePriorityGeneration;
        }

        private static int ResolveFrameCount(AnimationClip clip, int minFrames, int maxFrames)
        {
            float length = clip != null ? Mathf.Max(0.001f, clip.length) : 1f;
            int sampleRate = length > LongClipThresholdSeconds ? LongClipSampleRate : ShortClipSampleRate;
            int frames = Mathf.CeilToInt(length * sampleRate);
            return Mathf.Clamp(frames, minFrames, maxFrames);
        }

        public static void Draw(Rect rect, string key, AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar, int pixels, float playbackSpeed, int frameCount, int maxPixels, float yaw, string viewName, string label, int priorityGeneration, int priorityIndex)
        {
            if (IsPersistentFrameLoadingBlocked())
            {
                DrawStaticModelFallback(rect, model);
                DrawStatus(rect, "ReloadDomain保护：暂停加载帧缓存", new Color(0.18f, 0.13f, 0.04f, 0.78f));
                return;
            }

            frameCount = Mathf.Clamp(frameCount, GridMinFrameCount, GridMaxFrameCount);
            pixels = Mathf.Clamp(pixels, 64, Mathf.Clamp(maxPixels, 64, 1024));
            yaw = NormalizeYaw(yaw);
            string cacheKey = key + "|" + CacheVersion + "|frames=" + frameCount + "|px=" + pixels + "|yaw=" + Mathf.RoundToInt(yaw);
            Entry entry = GetOrCreateEntry(cacheKey);
            string persistentDirectory = GetPersistentDirectory(clip, model, yaw, viewName);
            string manifest = BuildManifest(clip, model, frameCount, pixels, yaw);
            entry.Configure(clip, model, fallbackMaterial, overrideAvatar, pixels, playbackSpeed, frameCount, yaw, persistentDirectory, manifest);
            if (entry.IsReady)
            {
                int frameIndex = GetFrameIndex(clip, playbackSpeed, entry.FrameCount);
                Texture2D frame = entry.GetFrameOrNearest(frameIndex);
                if (frame != null)
                    GUI.DrawTexture(rect, frame, ScaleMode.ScaleToFit);
                else
                    DrawStaticModelFallback(rect, model);
                DrawBadge(rect, label + " " + Mathf.Clamp(playbackSpeed, 0.1f, 1f).ToString("F2") + "x", new Color(0f, 0f, 0f, 0.55f));
                ESAssetPackageBakeWindow.UsingWindow?.Repaint();
                return;
            }

            Texture2D latestFrame = entry.GetPlayableFrame(clip, playbackSpeed);
            if (latestFrame != null)
                GUI.DrawTexture(rect, latestFrame, ScaleMode.ScaleToFit);
            else
                DrawStaticModelFallback(rect, model);
            if (!string.IsNullOrEmpty(entry.error))
                DrawStatus(rect, entry.error, new Color(0.35f, 0.10f, 0.08f, 0.72f));
            else
                DrawStatus(rect, entry.StatusText, new Color(0f, 0f, 0f, 0.55f));

            QueueBuild(entry, priorityGeneration, priorityIndex);
        }

        public static void Clear()
        {
            foreach (Entry entry in Entries.Values)
                entry.Dispose();
            Entries.Clear();
            BuildQueue.Clear();
            ActiveBuildEntry = null;
            visiblePriorityScope = string.Empty;
            visiblePriorityGeneration = 0;
            UnregisterUpdate();
        }

        public static void DeletePersistentFrames(AnimationClip clip, UnityEngine.Object model, float yaw)
        {
            string resourceDirectory = GetResourcePersistentDirectory(clip, model);
            if (string.IsNullOrEmpty(resourceDirectory) || !Directory.Exists(resourceDirectory))
                return;

            string yawSuffix = "_" + Mathf.RoundToInt(NormalizeYaw(yaw)).ToString();
            try
            {
                string[] viewDirectories = Directory.GetDirectories(resourceDirectory, "*", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < viewDirectories.Length; i++)
                {
                    string name = Path.GetFileName(viewDirectories[i]);
                    if (!string.IsNullOrEmpty(name) && name.EndsWith(yawSuffix, StringComparison.Ordinal))
                        Directory.Delete(viewDirectories[i], true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ES资产包帧缓存] 删除持久帧失败: " + ex.Message);
            }
        }

        private static Entry GetOrCreateEntry(string key)
        {
            if (Entries.TryGetValue(key, out Entry entry))
                return entry;

            if (Entries.Count >= MaxEntries)
            {
                string firstKey = Entries.Keys.FirstOrDefault();
                if (!string.IsNullOrEmpty(firstKey))
                {
                    Entries[firstKey].Dispose();
                    Entries.Remove(firstKey);
                }
            }

            entry = new Entry();
            Entries[key] = entry;
            return entry;
        }

        private static void QueueBuild(Entry entry, int priorityGeneration, int priorityIndex)
        {
            if (entry == null || entry.IsReady || !entry.CanBuild)
                return;

            entry.SetQueuePriority(priorityGeneration, priorityIndex);
            if (!entry.Queued)
            {
                entry.Queued = true;
                entry.QueueSerial = ++queueSerial;
                BuildQueue.Add(entry);
            }

            RegisterUpdate();
        }

        private static void RegisterUpdate()
        {
            if (updateRegistered)
                return;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            updateRegistered = true;
        }

        private static void UnregisterUpdate()
        {
            if (!updateRegistered)
                return;

            EditorApplication.update -= OnEditorUpdate;
            updateRegistered = false;
        }

        private static void OnEditorUpdate()
        {
            Entry activeEntry = GetOrTakeActiveEntry();
            if (activeEntry != null)
            {
                activeEntry.GenerateFrames(MaxFramesPerEditorUpdate);
                if (activeEntry.IsReady || !activeEntry.CanBuild)
                    ActiveBuildEntry = null;
                else
                    ActiveBuildEntry = activeEntry;
            }

            ESAssetPackageBakeWindow.UsingWindow?.Repaint();
            if (ActiveBuildEntry == null && BuildQueue.Count == 0)
                UnregisterUpdate();
        }

        private static Entry GetOrTakeActiveEntry()
        {
            if (ActiveBuildEntry != null && !ActiveBuildEntry.IsReady && ActiveBuildEntry.CanBuild)
                return ActiveBuildEntry;

            ActiveBuildEntry = null;
            while (BuildQueue.Count > 0)
            {
                int bestIndex = FindBestQueueIndex();
                Entry entry = bestIndex >= 0 ? BuildQueue[bestIndex] : null;
                if (bestIndex >= 0)
                    BuildQueue.RemoveAt(bestIndex);
                if (entry != null)
                    entry.Queued = false;

                if (entry == null || entry.IsReady || !entry.CanBuild)
                    continue;

                ActiveBuildEntry = entry;
                return ActiveBuildEntry;
            }

            return null;
        }

        private static int FindBestQueueIndex()
        {
            int bestIndex = -1;
            for (int i = 0; i < BuildQueue.Count; i++)
            {
                Entry entry = BuildQueue[i];
                if (entry == null)
                    continue;

                if (bestIndex < 0 || HasHigherPriority(entry, BuildQueue[bestIndex]))
                    bestIndex = i;
            }

            return bestIndex;
        }

        private static bool HasHigherPriority(Entry left, Entry right)
        {
            if (right == null)
                return true;

            if (left.PriorityGeneration != right.PriorityGeneration)
                return left.PriorityGeneration > right.PriorityGeneration;

            if (left.PriorityIndex != right.PriorityIndex)
                return left.PriorityIndex < right.PriorityIndex;

            return left.QueueSerial < right.QueueSerial;
        }

        private static int GetFrameIndex(AnimationClip clip, float playbackSpeed, int frameCount)
        {
            if (clip == null || clip.length <= 0.001f || frameCount <= 0)
                return 0;

            float time = (float)(EditorApplication.timeSinceStartup * Mathf.Clamp(playbackSpeed, 0.1f, 1f));
            float normalized = Mathf.Repeat(time, clip.length) / clip.length;
            return Mathf.FloorToInt(normalized * frameCount) % frameCount;
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f)
                yaw += 360f;
            return yaw;
        }

        private static string GetPersistentRoot()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return string.Empty;

            return Path.Combine(projectRoot, PersistentRootFolder.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string GetResourcePersistentDirectory(AnimationClip clip, UnityEngine.Object model)
        {
            string root = GetPersistentRoot();
            if (string.IsNullOrEmpty(root) || clip == null)
                return string.Empty;

            string clipPath = AssetDatabase.GetAssetPath(clip);
            string modelPath = model != null ? AssetDatabase.GetAssetPath(model) : string.Empty;
            string hash = StableShortHash(clipPath + "|" + clip.name + "|" + modelPath);
            return Path.Combine(root, SanitizeFileName(clip.name) + "_" + hash);
        }

        private static string GetPersistentDirectory(AnimationClip clip, UnityEngine.Object model, float yaw, string viewName)
        {
            string resourceDirectory = GetResourcePersistentDirectory(clip, model);
            if (string.IsNullOrEmpty(resourceDirectory))
                return string.Empty;

            string safeView = SanitizeFileName(string.IsNullOrWhiteSpace(viewName) ? "视角" : viewName);
            return Path.Combine(resourceDirectory, safeView + "_" + Mathf.RoundToInt(NormalizeYaw(yaw)).ToString());
        }

        private static string BuildManifest(AnimationClip clip, UnityEngine.Object model, int frameCount, int pixels, float yaw)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine("version=" + CacheVersion);
            sb.AppendLine("clipPath=" + (clip != null ? AssetDatabase.GetAssetPath(clip) : string.Empty));
            sb.AppendLine("clipName=" + (clip != null ? clip.name : string.Empty));
            sb.AppendLine("modelPath=" + (model != null ? AssetDatabase.GetAssetPath(model) : string.Empty));
            sb.AppendLine("frameCount=" + frameCount);
            sb.AppendLine("pixels=" + pixels);
            sb.AppendLine("yaw=" + Mathf.RoundToInt(NormalizeYaw(yaw)));
            return sb.ToString();
        }

        private static string GetManifestPath(string directory)
        {
            return string.IsNullOrEmpty(directory) ? string.Empty : Path.Combine(directory, "manifest.txt");
        }

        private static string GetFramePath(string directory, int frameIndex)
        {
            return Path.Combine(directory, "preview_" + (frameIndex + 1).ToString("000") + ".png");
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                builder.Append(invalid.Contains(c) ? '_' : c);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "Unnamed" : result;
        }

        private static string StableShortHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < (value ?? string.Empty).Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash.ToString("X8");
            }
        }

        private static void DrawStaticModelFallback(Rect rect, UnityEngine.Object model)
        {
            Texture preview = null;
            GameObject go = ResolvePreviewGameObject(model);
            if (go != null)
                preview = ESEditorPreviewUtility.GetAssetPreviewOrMini(go, null);

            if (preview != null)
                GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
            else
                GUI.Label(rect, "生成动作预览", EditorStyles.centeredGreyMiniLabel);
        }

        private static GameObject ResolvePreviewGameObject(UnityEngine.Object source)
        {
            if (source is GameObject go)
                return go;

            string path = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void DrawBadge(Rect rect, string text, Color color)
        {
            Rect badgeRect = new Rect(rect.x + 6f, rect.y + 6f, 76f, 18f);
            EditorGUI.DrawRect(badgeRect, color);
            GUI.Label(badgeRect, text, EditorStyles.centeredGreyMiniLabel);
        }

        private static void DrawStatus(Rect rect, string text, Color color)
        {
            Rect statusRect = new Rect(rect.x + 6f, rect.yMax - 22f, rect.width - 12f, 18f);
            EditorGUI.DrawRect(statusRect, color);
            GUI.Label(statusRect, text, EditorStyles.centeredGreyMiniLabel);
        }

        private sealed class Entry
        {
            public Texture2D[] frames;
            public string error;
            public bool Queued;
            public int PriorityGeneration { get; private set; }
            public int PriorityIndex { get; private set; } = int.MaxValue;
            public long QueueSerial;
            public int FrameCount { get; private set; }
            public bool IsReady => frames != null && frames.Length > 0 && completedFrames >= frames.Length;
            public bool CanBuild => clip != null && model != null && FrameCount > 0 && pixels > 0;
            public string StatusText => IsReady ? "已缓存" : "异步生成 " + completedFrames + "/" + Mathf.Max(1, FrameCount);

            private AnimationClip clip;
            private UnityEngine.Object model;
            private Material fallbackMaterial;
            private Avatar overrideAvatar;
            private int pixels;
            private float playbackSpeed;
            private float yaw;
            private int completedFrames;
            private int retryCountForCurrentFrame;
            private ESAssetPackageAnimationPreviewPlayer player;
            private string persistentDirectory;
            private string manifestContent;
            private bool preparedPersistentWrite;

            public void SetQueuePriority(int priorityGeneration, int priorityIndex)
            {
                PriorityGeneration = Mathf.Max(0, priorityGeneration);
                PriorityIndex = Mathf.Max(0, priorityIndex);
            }

            public void Configure(AnimationClip newClip, UnityEngine.Object newModel, Material newFallbackMaterial, Avatar newOverrideAvatar, int newPixels, float newPlaybackSpeed, int newFrameCount, float newYaw, string newPersistentDirectory, string newManifestContent)
            {
                newYaw = NormalizeYaw(newYaw);
                if (clip == newClip && model == newModel && fallbackMaterial == newFallbackMaterial && overrideAvatar == newOverrideAvatar && pixels == newPixels && FrameCount == newFrameCount && Mathf.Approximately(yaw, newYaw) && string.Equals(persistentDirectory, newPersistentDirectory, StringComparison.Ordinal))
                {
                    playbackSpeed = newPlaybackSpeed;
                    return;
                }

                DisposeFramesOnly();
                clip = newClip;
                model = newModel;
                fallbackMaterial = newFallbackMaterial;
                overrideAvatar = newOverrideAvatar;
                pixels = newPixels;
                playbackSpeed = newPlaybackSpeed;
                FrameCount = newFrameCount;
                yaw = newYaw;
                persistentDirectory = newPersistentDirectory;
                manifestContent = newManifestContent ?? string.Empty;
                frames = new Texture2D[FrameCount];
                completedFrames = 0;
                retryCountForCurrentFrame = 0;
                preparedPersistentWrite = false;
                error = string.Empty;
                TryLoadPersistentFrames();
            }

            public Texture2D GetLatestFrame()
            {
                if (frames == null)
                    return null;

                for (int i = Mathf.Min(completedFrames - 1, frames.Length - 1); i >= 0; i--)
                {
                    if (frames[i] != null)
                        return frames[i];
                }

                return null;
            }

            public Texture2D GetPlayableFrame(AnimationClip sourceClip, float speed)
            {
                if (frames == null || frames.Length == 0)
                    return null;

                int available = Mathf.Clamp(completedFrames, 0, frames.Length);
                if (available <= 0)
                    return null;

                if (available == 1)
                    return frames[0];

                int frameIndex = GetFrameIndex(sourceClip, speed, available);
                frameIndex = Mathf.Clamp(frameIndex, 0, available - 1);
                if (frames[frameIndex] != null)
                    return frames[frameIndex];

                for (int i = available - 1; i >= 0; i--)
                {
                    if (frames[i] != null)
                        return frames[i];
                }

                return null;
            }

            public Texture2D GetFrameOrNearest(int index)
            {
                if (frames == null || frames.Length == 0)
                    return null;

                index = Mathf.Clamp(index, 0, frames.Length - 1);
                if (frames[index] != null)
                    return frames[index];

                for (int offset = 1; offset < frames.Length; offset++)
                {
                    int next = (index + offset) % frames.Length;
                    if (frames[next] != null)
                        return frames[next];

                    int prev = (index - offset + frames.Length) % frames.Length;
                    if (frames[prev] != null)
                        return frames[prev];
                }

                return null;
            }

            public void GenerateNextFrame()
            {
                GenerateFrames(1);
            }

            public int GenerateFrames(int maxFrames)
            {
                if (IsReady || !CanBuild)
                    return 0;

                try
                {
                    player ??= new ESAssetPackageAnimationPreviewPlayer();
                    player.ConfigureGridPreview(playbackSpeed, yaw);
                    int generated = player.RenderSnapshotBatch(
                        clip,
                        model,
                        fallbackMaterial,
                        overrideAvatar,
                        pixels,
                        yaw,
                        completedFrames,
                        FrameCount,
                        Mathf.Max(1, maxFrames),
                        frames);

                    if (generated <= 0)
                    {
                        retryCountForCurrentFrame++;
                        error = string.IsNullOrEmpty(player.LastStatus)
                            ? "等待有效姿态 " + completedFrames + "/" + FrameCount
                            : player.LastStatus;
                        if (retryCountForCurrentFrame < 3)
                            return 0;

                        completedFrames++;
                        retryCountForCurrentFrame = 0;
                        return 0;
                    }

                    completedFrames += generated;
                    SaveGeneratedFrames(completedFrames - generated, generated);
                    retryCountForCurrentFrame = 0;
                    error = string.Empty;
                    if (IsReady)
                    {
                        WritePersistentManifest();
                        player.Dispose();
                        player = null;
                    }

                    return generated;
                }
                catch (Exception ex)
                {
                    error = "帧生成失败 " + ex.GetType().Name;
                    completedFrames = FrameCount;
                    player?.Dispose();
                    player = null;
                    return 0;
                }
            }

            public void SetFrames(Texture2D[] newFrames)
            {
                Dispose();
                frames = newFrames;
                completedFrames = frames != null ? frames.Length : 0;
            }

            public void Dispose()
            {
                DisposeFramesOnly();
                player?.Dispose();
                player = null;
            }

            private void DisposeFramesOnly()
            {
                if (frames != null)
                {
                    for (int i = 0; i < frames.Length; i++)
                    {
                        if (frames[i] != null)
                            UnityEngine.Object.DestroyImmediate(frames[i]);
                    }
                }

                frames = null;
                completedFrames = 0;
                Queued = false;
                preparedPersistentWrite = false;
            }

            private void TryLoadPersistentFrames()
            {
                if (string.IsNullOrEmpty(persistentDirectory))
                    return;

                string manifestPath = GetManifestPath(persistentDirectory);
                if (!File.Exists(manifestPath))
                    return;

                try
                {
                    string existingManifest = File.ReadAllText(manifestPath, Encoding.UTF8);
                    if (!string.Equals(existingManifest, manifestContent, StringComparison.Ordinal))
                        return;

                    var loaded = new Texture2D[FrameCount];
                    for (int i = 0; i < FrameCount; i++)
                    {
                        string framePath = GetFramePath(persistentDirectory, i);
                        if (!File.Exists(framePath))
                        {
                            DisposeLoadedFrames(loaded);
                            return;
                        }

                        byte[] bytes = File.ReadAllBytes(framePath);
                        var texture = new Texture2D(2, 2)
                        {
                            name = "ES Persistent Grid Frame " + (i + 1).ToString("000"),
                            hideFlags = HideFlags.HideAndDontSave,
                            filterMode = FilterMode.Bilinear
                        };
                        if (!texture.LoadImage(bytes, true))
                        {
                            UnityEngine.Object.DestroyImmediate(texture);
                            DisposeLoadedFrames(loaded);
                            return;
                        }

                        loaded[i] = texture;
                    }

                    DisposeFramesOnly();
                    frames = loaded;
                    completedFrames = FrameCount;
                    error = string.Empty;
                }
                catch (Exception ex)
                {
                    error = "读取磁盘帧失败 " + ex.GetType().Name;
                }
            }

            private static void DisposeLoadedFrames(Texture2D[] loaded)
            {
                if (loaded == null)
                    return;

                for (int i = 0; i < loaded.Length; i++)
                {
                    if (loaded[i] != null)
                        UnityEngine.Object.DestroyImmediate(loaded[i]);
                }
            }

            private void PreparePersistentWrite()
            {
                if (preparedPersistentWrite || string.IsNullOrEmpty(persistentDirectory))
                    return;

                try
                {
                    if (Directory.Exists(persistentDirectory))
                        Directory.Delete(persistentDirectory, true);
                    Directory.CreateDirectory(persistentDirectory);
                    preparedPersistentWrite = true;
                }
                catch (Exception ex)
                {
                    error = "准备磁盘帧失败 " + ex.GetType().Name;
                    persistentDirectory = string.Empty;
                }
            }

            private void SaveGeneratedFrames(int startFrame, int count)
            {
                if (string.IsNullOrEmpty(persistentDirectory) || frames == null || count <= 0)
                    return;

                PreparePersistentWrite();
                if (string.IsNullOrEmpty(persistentDirectory))
                    return;

                for (int i = 0; i < count; i++)
                {
                    int frameIndex = startFrame + i;
                    if (frameIndex < 0 || frameIndex >= frames.Length || frames[frameIndex] == null)
                        continue;

                    try
                    {
                        byte[] png = frames[frameIndex].EncodeToPNG();
                        if (png != null && png.Length > 0)
                            File.WriteAllBytes(GetFramePath(persistentDirectory, frameIndex), png);
                    }
                    catch (Exception ex)
                    {
                        error = "写入磁盘帧失败 " + ex.GetType().Name;
                        persistentDirectory = string.Empty;
                        return;
                    }
                }
            }

            private void WritePersistentManifest()
            {
                if (string.IsNullOrEmpty(persistentDirectory) || string.IsNullOrEmpty(manifestContent))
                    return;

                try
                {
                    Directory.CreateDirectory(persistentDirectory);
                    File.WriteAllText(GetManifestPath(persistentDirectory), manifestContent, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    error = "写入帧清单失败 " + ex.GetType().Name;
                }
            }
        }
    }

    internal sealed class ESAssetPackageAnimationPreviewPlayer : IDisposable
    {
        private const string PrefKeyAutoPlay = "ES.AssetPackage.AnimationPreview.AutoPlay";
        private const string PrefKeyFollow = "ES.AssetPackage.AnimationPreview.Follow";
        private const string PrefKeyRenderScale = "ES.AssetPackage.AnimationPreview.RenderScale";
        private const string PrefKeyYaw = "ES.AssetPackage.AnimationPreview.Yaw";
        private const string PrefKeyPitch = "ES.AssetPackage.AnimationPreview.Pitch";
        private const string PrefKeyZoom = "ES.AssetPackage.AnimationPreview.Zoom";
        private const string PrefKeyPlaybackSpeed = "ES.AssetPackage.AnimationPreview.PlaybackSpeed";
        private const string PrefKeyBaselinePlatform = "ES.AssetPackage.AnimationPreview.BaselinePlatform";
        private readonly ESAssetPackagePreviewSceneContext previewContext = new ESAssetPackagePreviewSceneContext(usePreviewScene: false);
        private GameObject previewInstance;
        private string playingPath;
        private AnimationClip playingClip;
        private UnityEngine.Object playingModel;
        private UnityEngine.Object instantiatedModel;
        private AnimationClip instantiatedClip;
        private Avatar instantiatedAvatar;
        private double startTime;
        private PlayableGraph playableGraph;
        private AnimationMixerPlayable previewMixer;
        private AnimationClipPlayable clipPlayable;
        private AnimationClipPlayable basePosePlayable;
        private Animator previewAnimator;
        private AnimationClip basePoseClip;
        private int clipInputIndex = -1;
        private int basePoseInputIndex = -1;
        private double lastPlayableGraphTime = -1d;
        private HumanPoseHandler humanPoseHandler;
        private HumanPoseCurveSet humanPoseCurveSet;
        private Avatar humanPoseAvatar;
        private Transform humanPoseRoot;
        private AnimationClip humanPoseClip;
        private HumanPose humanPoseBase;
        private Material instantiatedFallbackMaterial;
        private bool ownsAnimationMode;
        private bool settingsLoaded;
        private bool autoPlay = true;
        private bool followAnimatedBounds = true;
        private float renderScale = 2.5f;
        private float playbackSpeed = 1f;
        private ESAssetPackagePreviewBaselinePlatform baselinePlatform = ESAssetPackagePreviewBaselinePlatform.Desktop;
        private float orbitYaw = 180f;
        private float orbitPitch = 8f;
        private float zoomMultiplier = 1f;
        private string userStoppedPath;
        private float lastEvaluatedTime;
        private Bounds lastBounds;
        private Vector3 stableCenter;
        private bool hasStableCenter;
        private string lastInstanceError = string.Empty;
        private string lastPlayableError = string.Empty;
        private string lastHumanPoseError = string.Empty;
        private string lastInitializationReport = string.Empty;
        private string lastLoggedInitializationKey = string.Empty;
        private int lastHumanPoseActiveMuscles;
        private float lastHumanPoseMaxMuscleAbs;
        private int lastHumanPoseChangedBones;
        private float lastHumanPoseMaxBoneAngle;
        private string lastHumanPoseChangedBoneSamples = string.Empty;
        private string lastSampleError = string.Empty;
        private string lastSamplingMode = string.Empty;
        private string lastDriverConflict = string.Empty;
        private int lastSampleTargetInstanceId;
        private string lastSampleTargetName = string.Empty;
        private string lastSampleTargetScene = string.Empty;
        private bool lastAnimationModeAlreadyActive;
        private bool lastSampleTargetIsAnimatorObject;
        private int animationModeSampleCount;
        private int lastAnimationModeChangedBones;
        private float lastAnimationModeMaxBoneAngle;
        private string lastAnimationModeChangedBoneSamples = string.Empty;
        private Dictionary<HumanBodyBones, Quaternion> restPoseRotations = new Dictionary<HumanBodyBones, Quaternion>();
        private int lastRestPoseChangedBones;
        private float lastRestPoseMaxBoneAngle;
        private string lastRestPoseChangedBoneSamples = string.Empty;
        private static readonly Dictionary<int, bool> HumanoidBodyCurveCache = new Dictionary<int, bool>();
        private float lastProbeSampleTime;
        private int lastProbeChangedBones;
        private float lastProbeMaxBoneAngle;
        private string lastProbeChangedBoneSamples = string.Empty;
        private string lastProbeStatus = string.Empty;
        private float lastRequestedSampleTime;
        private double lastSampleEditorTime;
        private bool previewInputEnabled = true;
        private double repaintInterval;
        private double lastRepaintTime;
        private int targetPreviewFps = 60;
        private bool editorUpdateRegistered;
        private static MethodInfo samplePlayableGraphMethod;
        public Action RepaintOwner;
        public string LastStatus { get; private set; } = "未播放";
        public bool HasPreviewInstance => previewInstance != null;

        public bool IsPlaying(string path)
        {
            return !string.IsNullOrEmpty(path) && playingPath == path && playingClip != null;
        }

        public void ReloadSettings()
        {
            settingsLoaded = false;
            LoadSettings();
        }

        public void ConfigureGridPreview(float slowSpeed)
        {
            ConfigureGridPreview(slowSpeed, 180f);
        }

        public void ConfigureGridPreview(float slowSpeed, float yaw)
        {
            settingsLoaded = true;
            autoPlay = true;
            followAnimatedBounds = false;
            renderScale = 2.5f;
            playbackSpeed = Mathf.Clamp(slowSpeed, 0.1f, 1f);
            baselinePlatform = ESAssetPackagePreviewBaselinePlatform.Desktop;
            orbitYaw = yaw;
            orbitPitch = 8f;
            zoomMultiplier = 1f;
            previewInputEnabled = false;
            targetPreviewFps = 24;
            repaintInterval = 0.04d;
        }

        public void ConfigureExpandedPreviewQuality()
        {
            LoadSettings();
            previewInputEnabled = true;
            baselinePlatform = ESAssetPackagePreviewBaselinePlatform.Desktop;
            renderScale = 4f;
            targetPreviewFps = 60;
            repaintInterval = 1d / targetPreviewFps;
        }

        public void AutoStart(string path, AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar)
        {
            LoadSettings();
            if (!autoPlay || clip == null || model == null || IsPlaying(path) || string.Equals(userStoppedPath, path, StringComparison.Ordinal))
                return;

            StartPlayback(path, clip, model, fallbackMaterial, overrideAvatar);
        }

        public void Toggle(string path, AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar)
        {
            if (IsPlaying(path))
            {
                Stop(true);
                return;
            }

            userStoppedPath = null;
            StartPlayback(path, clip, model, fallbackMaterial, overrideAvatar);
        }

        private void StartPlayback(string path, AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar)
        {
            Stop(false);
            playingPath = path;
            playingClip = clip;
            playingModel = model;
            startTime = EditorApplication.timeSinceStartup;
            EnsurePreviewUtility();
            EnsureInstance(model, fallbackMaterial, clip, overrideAvatar);
            DestroyPlayableGraph();
            DestroyHumanPoseSampler();
            LastStatus = "播放中（AnimationMode主采样）";
            RegisterEditorUpdate();
        }

        public void Stop()
        {
            Stop(false);
        }

        public void Stop(bool userRequested)
        {
            if (userRequested && !string.IsNullOrEmpty(playingPath))
                userStoppedPath = playingPath;

            DestroyPlayableGraph();
            playingPath = null;
            playingClip = null;
            playingModel = null;
            DestroyInstance();
            StopOwnAnimationMode();
            UnregisterEditorUpdate();
            LastStatus = "已停止";
        }

        public void ResumeRepaint(Action repaintOwner)
        {
            RepaintOwner = repaintOwner;
            if (playingClip != null)
                RegisterEditorUpdate();
        }

        public void SuspendRepaint()
        {
            RepaintOwner = null;
            UnregisterEditorUpdate();
        }

        public void Draw(Rect rect, string path, AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar)
        {
            LoadSettings();
            EditorGUI.DrawRect(rect, new Color(0.06f, 0.065f, 0.075f, 1f));
            if (clip == null || model == null)
            {
                GUI.Label(rect, "选择动画和预览模型后点击播放", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            EnsurePreviewUtility();
            EnsureInstance(model, fallbackMaterial, clip, overrideAvatar);

            if (previewInstance == null)
            {
                LastStatus = "无法创建模型预览实例";
                GUI.Label(rect, "无法创建模型预览实例", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            float speed = Mathf.Max(0f, playbackSpeed);
            float time = IsPlaying(path)
                ? (float)(((EditorApplication.timeSinceStartup - startTime) * speed) % Mathf.Max(0.01f, clip.length))
                : 0f;
            if (!SampleClip(previewInstance, clip, time) && !EvaluatePlayableClip(clip, time) && !EvaluateHumanPoseClip(clip, time))
                LastStatus = "动画采样失败，查看 Debug";

            Bounds bounds = ESEditorPreviewUtility.CalculateBounds(previewInstance);
            lastBounds = bounds;
            if (!hasStableCenter)
            {
                stableCenter = bounds.center;
                hasStableCenter = true;
            }

            Vector3 center = followAnimatedBounds ? bounds.center : stableCenter;
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);

            if (previewInputEnabled)
                HandlePreviewInput(rect);
            RenderPreviewCamera(rect, center, radius);
        }

        public Texture2D RenderSnapshot(AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar, float time, int pixels)
        {
            return RenderSnapshot(clip, model, fallbackMaterial, overrideAvatar, time, pixels, orbitYaw);
        }

        public Texture2D RenderSnapshot(AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar, float time, int pixels, float yaw)
        {
            EnsurePreviewUtility();
            EnsureInstance(model, fallbackMaterial, clip, overrideAvatar);
            if (previewInstance == null || clip == null || model == null)
                return null;

            return TryRenderSnapshotWithPlayableGraph(clip, time, pixels, yaw);
        }

        public int RenderSnapshotBatch(AnimationClip clip, UnityEngine.Object model, Material fallbackMaterial, Avatar overrideAvatar, int pixels, float yaw, int startFrame, int totalFrames, int maxFrames, Texture2D[] destination)
        {
            EnsurePreviewUtility();
            EnsureInstance(model, fallbackMaterial, clip, overrideAvatar);
            if (previewInstance == null || clip == null || model == null || destination == null || totalFrames <= 0)
                return 0;

            GameObject sampleTarget = ResolveAnimationModeSampleTarget(previewInstance);
            if (sampleTarget == null)
                return 0;

            int generated = 0;
            try
            {
                int endFrame = Mathf.Min(totalFrames, startFrame + Mathf.Max(1, maxFrames));
                for (int frameIndex = startFrame; frameIndex < endFrame; frameIndex++)
                {
                    float normalized = (frameIndex + 0.5f) / (float)Mathf.Max(1, totalFrames);
                    float time = Mathf.Max(0.001f, clip.length) * normalized;
                    lastEvaluatedTime = time;
                    lastRequestedSampleTime = time;
                    lastSampleEditorTime = EditorApplication.timeSinceStartup;
                    lastSampleTargetInstanceId = sampleTarget.GetInstanceID();
                    lastSampleTargetName = sampleTarget.name;
                    lastSampleTargetScene = sampleTarget.scene.IsValid() ? sampleTarget.scene.name : "<invalid>";
                    lastSampleTargetIsAnimatorObject = previewAnimator != null && previewAnimator.gameObject == sampleTarget;
                    lastAnimationModeAlreadyActive = AnimationMode.InAnimationMode();
                    lastDriverConflict = "小格独占Player：PlayableGraph按指定时间采样";

                    if (!EvaluatePlayableClip(clip, time) || !IsSnapshotPoseUseful(clip))
                    {
                        LastStatus = "小格批量帧跳过：PlayableGraph未得到有效身体姿态";
                        break;
                    }

                    Texture2D snapshot = RenderCurrentSnapshot(pixels, yaw);
                    if (snapshot == null)
                        break;

                    destination[frameIndex] = snapshot;
                    generated++;
                }
            }
            catch (Exception ex)
            {
                lastSampleError = "BatchSnapshot " + ex.GetType().Name + ": " + ex.Message;
            }

            return generated;
        }

        private Texture2D TryRenderSnapshotWithPlayableGraph(AnimationClip clip, float time, int pixels, float yaw)
        {
            if (clip == null || previewInstance == null)
                return null;

            if (!EvaluatePlayableClip(clip, time) || !IsSnapshotPoseUseful(clip))
            {
                LastStatus = "小格帧跳过：PlayableGraph未得到有效身体姿态";
                return null;
            }

            return RenderCurrentSnapshot(pixels, yaw);
        }

        private Texture2D TryRenderSnapshotWithAtomicAnimationMode(AnimationClip clip, float time, int pixels, float yaw)
        {
            if (clip == null || previewInstance == null)
                return null;

            GameObject sampleTarget = ResolveAnimationModeSampleTarget(previewInstance);
            if (sampleTarget == null)
                return null;

            bool beganSampling = false;
            bool startedAnimationMode = false;
            try
            {
                lastEvaluatedTime = time;
                lastRequestedSampleTime = time;
                lastSampleEditorTime = EditorApplication.timeSinceStartup;
                lastSampleTargetInstanceId = sampleTarget.GetInstanceID();
                lastSampleTargetName = sampleTarget.name;
                lastSampleTargetScene = sampleTarget.scene.IsValid() ? sampleTarget.scene.name : "<invalid>";
                lastSampleTargetIsAnimatorObject = previewAnimator != null && previewAnimator.gameObject == sampleTarget;
                lastAnimationModeAlreadyActive = AnimationMode.InAnimationMode();
                lastDriverConflict = "小格独占Player：不清理内部驱动，仅使用AnimationMode按指定时间采样";

                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    ownsAnimationMode = true;
                    startedAnimationMode = true;
                }

                Dictionary<HumanBodyBones, Quaternion> beforeRotations = CaptureHumanoidBoneRotations();
                AnimationMode.BeginSampling();
                beganSampling = true;
                AnimationMode.SampleAnimationClip(sampleTarget, clip, time);

                animationModeSampleCount++;
                MeasureAnimationModeBoneRotationDelta(beforeRotations);
                lastSamplingMode = "Atomic SampleAnimationClip -> CameraCapture -> EndSampling";
                lastSampleError = string.Empty;
                CommitAnimatorPose(false);

                if (!IsSnapshotPoseUseful(clip))
                {
                    LastStatus = "小格帧跳过：指定时间采样后仍接近T Pose";
                    AnimationMode.EndSampling();
                    beganSampling = false;
                    return null;
                }

                Texture2D snapshot = RenderCurrentSnapshot(pixels, yaw);
                AnimationMode.EndSampling();
                beganSampling = false;
                if (startedAnimationMode)
                    StopOwnAnimationMode();
                return snapshot;
            }
            catch (Exception ex)
            {
                lastSampleError = "AtomicSnapshot " + ex.GetType().Name + ": " + ex.Message;
                return null;
            }
            finally
            {
                if (beganSampling)
                {
                    try
                    {
                        AnimationMode.EndSampling();
                    }
                    catch
                    {
                    }
                }

                if (startedAnimationMode)
                    StopOwnAnimationMode();
            }
        }

        private Texture2D RenderCurrentSnapshot(int pixels, float yaw)
        {
            if (previewInstance == null)
                return null;

            Bounds bounds = ESEditorPreviewUtility.CalculateBounds(previewInstance);
            if (!hasStableCenter)
            {
                stableCenter = bounds.center;
                hasStableCenter = true;
            }

            Vector3 center = followAnimatedBounds ? bounds.center : stableCenter;
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            return previewContext.RenderSnapshot(
                Mathf.Clamp(pixels, 64, 1024),
                Mathf.Clamp(pixels, 64, 1024),
                center,
                radius,
                yaw,
                8f,
                1f,
                baselinePlatform);
        }

        private bool TrySampleSnapshotPose(AnimationClip clip, float time)
        {
            if (clip == null || previewInstance == null)
                return false;

            if (SampleClip(previewInstance, clip, time) && IsSnapshotPoseUseful(clip))
                return true;

            if (clip.humanMotion && EvaluateHumanPoseClip(clip, time) && IsHumanPoseUsefulForSnapshot(clip))
                return true;

            if (!clip.humanMotion && EvaluatePlayableClip(clip, time))
                return true;

            LastStatus = "小格帧跳过：采样后仍接近T Pose";
            return false;
        }

        private bool IsSnapshotPoseUseful(AnimationClip clip)
        {
            if (!ClipNeedsBodyPoseValidation(clip))
                return true;

            return IsCurrentPoseDifferentFromRest();
        }

        private bool IsHumanPoseUsefulForSnapshot(AnimationClip clip)
        {
            if (!ClipNeedsBodyPoseValidation(clip))
                return true;

            return IsCurrentPoseDifferentFromRest();
        }

        private bool IsCurrentPoseDifferentFromRest()
        {
            MeasureRestPoseDelta();
            return lastRestPoseChangedBones >= 2 || lastRestPoseMaxBoneAngle > 0.5f;
        }

        private static bool ClipNeedsBodyPoseValidation(AnimationClip clip)
        {
            return clip != null && clip.humanMotion && HasHumanoidBodyCurves(clip);
        }

        private static bool HasHumanoidBodyCurves(AnimationClip clip)
        {
            if (clip == null)
                return false;

            int id = clip.GetInstanceID();
            if (HumanoidBodyCurveCache.TryGetValue(id, out bool cached))
                return cached;

            bool hasBodyCurves = false;
            Dictionary<string, int> muscleIndexByName = HumanPoseCurveSet.BuildMuscleIndexByName();
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < bindings.Length; i++)
            {
                EditorCurveBinding binding = bindings[i];
                if (binding.type != typeof(Animator))
                    continue;

                if (muscleIndexByName.ContainsKey(binding.propertyName ?? string.Empty))
                {
                    hasBodyCurves = true;
                    break;
                }
            }

            HumanoidBodyCurveCache[id] = hasBodyCurves;
            return hasBodyCurves;
        }

        public void DrawPreviewOptions()
        {
            LoadSettings();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool newAutoPlay = GUILayout.Toggle(autoPlay, "自动播放", EditorStyles.toolbarButton, GUILayout.Width(72));
                if (newAutoPlay != autoPlay)
                {
                    autoPlay = newAutoPlay;
                    if (autoPlay)
                        userStoppedPath = null;
                    SaveSettings();
                }

                bool newFollow = GUILayout.Toggle(followAnimatedBounds, "相机跟随", EditorStyles.toolbarButton, GUILayout.Width(72));
                if (newFollow != followAnimatedBounds)
                {
                    followAnimatedBounds = newFollow;
                    SaveSettings();
                }

                EditorGUILayout.LabelField("画质", GUILayout.Width(32));
                float newScale = GUILayout.HorizontalSlider(renderScale, 1f, 4f, GUILayout.Width(110));
                newScale = Mathf.Round(newScale * 10f) / 10f;
                EditorGUILayout.LabelField(newScale.ToString("F1") + "x", GUILayout.Width(34));
                if (!Mathf.Approximately(newScale, renderScale))
                {
                    renderScale = newScale;
                    SaveSettings();
                }

                EditorGUILayout.LabelField("速度", GUILayout.Width(32));
                float newPlaybackSpeed = GUILayout.HorizontalSlider(playbackSpeed, 0.1f, 3f, GUILayout.Width(96));
                newPlaybackSpeed = Mathf.Round(newPlaybackSpeed * 10f) / 10f;
                EditorGUILayout.LabelField(newPlaybackSpeed.ToString("F1") + "x", GUILayout.Width(34));
                if (!Mathf.Approximately(newPlaybackSpeed, playbackSpeed))
                {
                    double now = EditorApplication.timeSinceStartup;
                    playbackSpeed = newPlaybackSpeed;
                    startTime = playbackSpeed > 0.0001f ? now - lastEvaluatedTime / playbackSpeed : now;
                    SaveSettings();
                }

                string[] platformNames = { "高质量", "移动端", "快速" };
                int newPlatform = EditorGUILayout.Popup((int)baselinePlatform, platformNames, GUILayout.Width(76));
                if (newPlatform != (int)baselinePlatform)
                {
                    baselinePlatform = (ESAssetPackagePreviewBaselinePlatform)newPlatform;
                    ApplyBaselinePlatformDefaults();
                    SaveSettings();
                }

                if (GUILayout.Button("重置视角", EditorStyles.toolbarButton, GUILayout.Width(74)))
                    ResetView();

                GUILayout.FlexibleSpace();
                GUILayout.Label(targetPreviewFps + " FPS", EditorStyles.miniLabel, GUILayout.Width(52));
            }
        }

        public string GetDebugReport(AnimationClip clip, UnityEngine.Object model, string previewKey)
        {
            GameObject resolvedModel = ResolvePreviewGameObjectForPlayer(model);
            RunDebugProbe(clip);
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== 动画预览 Debug ===");
            sb.AppendLine("CODE_VERSION: " + ESAssetPackageBakeWindow.CodeVersion);
            sb.AppendLine("状态: " + LastStatus);
            sb.AppendLine("正在播放: " + (IsPlaying(previewKey) ? "是" : "否"));
            sb.AppendLine("自动播放: " + (autoPlay ? "是" : "否"));
            sb.AppendLine("相机跟随: " + (followAnimatedBounds ? "是" : "否"));
            sb.AppendLine("画质倍率: " + renderScale.ToString("F1") + "x");
            sb.AppendLine("播放速度: " + playbackSpeed.ToString("F1") + "x");
            sb.AppendLine("基准平台: " + baselinePlatform);
            sb.AppendLine("预览隔离: " + (previewContext.IsReady ? (previewContext.UsePreviewScene ? "已启用 PreviewScene + Layer31" : "已启用普通编辑器场景隐藏对象 + Layer31") : "未创建"));
            sb.AppendLine("预览上下文: " + previewContext.LastStatus);
            sb.AppendLine("隔离细节: " + previewContext.IsolationReport);
            sb.AppendLine("ES清理标记: " + (previewContext.CleanupMarkerAvailable ? "已接入" : "未接入/未创建"));
            sb.AppendLine("ES清理标记详情: " + previewContext.LastMarkerStatus);
            sb.AppendLine("ES清理标记数量: " + previewContext.MarkedObjectCount);
            sb.AppendLine("预览对象流: " + previewContext.LastObjectFlowStatus);
            sb.AppendLine("最近采样时间: " + lastEvaluatedTime.ToString("F3"));
            sb.AppendLine("采样方式: " + (string.IsNullOrEmpty(lastSamplingMode) ? "<未采样>" : lastSamplingMode));
            sb.AppendLine("AnimationMode采样次数: " + animationModeSampleCount);
            sb.AppendLine("AnimationMode骨骼变化: " + lastAnimationModeChangedBones + " | 最大角度: " + lastAnimationModeMaxBoneAngle.ToString("F2"));
            if (!string.IsNullOrEmpty(lastAnimationModeChangedBoneSamples))
                sb.AppendLine("AnimationMode变化骨骼示例: " + lastAnimationModeChangedBoneSamples);
            sb.AppendLine("RestPose差异: " + lastRestPoseChangedBones + " | 最大角度: " + lastRestPoseMaxBoneAngle.ToString("F2"));
            if (!string.IsNullOrEmpty(lastRestPoseChangedBoneSamples))
                sb.AppendLine("RestPose差异示例: " + lastRestPoseChangedBoneSamples);
            sb.AppendLine("Debug中段探针: " + lastProbeStatus);
            sb.AppendLine("Debug中段时间: " + lastProbeSampleTime.ToString("F3"));
            sb.AppendLine("Debug中段骨骼变化: " + lastProbeChangedBones + " | 最大角度: " + lastProbeMaxBoneAngle.ToString("F2"));
            if (!string.IsNullOrEmpty(lastProbeChangedBoneSamples))
                sb.AppendLine("Debug中段变化骨骼示例: " + lastProbeChangedBoneSamples);
            sb.AppendLine("请求采样时间: " + lastRequestedSampleTime.ToString("F3"));
            sb.AppendLine("采样Editor时间: " + lastSampleEditorTime.ToString("F3"));
            sb.AppendLine("采样目标InstanceID: " + lastSampleTargetInstanceId);
            sb.AppendLine("采样目标对象: " + (string.IsNullOrEmpty(lastSampleTargetName) ? "<无>" : lastSampleTargetName));
            sb.AppendLine("采样目标场景: " + (string.IsNullOrEmpty(lastSampleTargetScene) ? "<无>" : lastSampleTargetScene));
            sb.AppendLine("采样目标是否Animator对象: " + (lastSampleTargetIsAnimatorObject ? "是" : "否"));
            sb.AppendLine("采样前AnimationMode已存在: " + (lastAnimationModeAlreadyActive ? "是" : "否"));
            sb.AppendLine("驱动冲突检查: " + lastDriverConflict);
            sb.AppendLine("PlayableGraph 有效: " + (playableGraph.IsValid() ? "是" : "否"));
            sb.AppendLine("ClipPlayable 有效: " + (clipPlayable.IsValid() ? "是" : "否"));
            sb.AppendLine("HumanPose 采样: " + (humanPoseHandler != null ? "已启用" : "未启用"));
            if (humanPoseCurveSet != null)
                sb.AppendLine("HumanPose 肌肉曲线命中: " + humanPoseCurveSet.MuscleCurveCount + "/" + HumanTrait.MuscleCount);
            sb.AppendLine("HumanPose 当前有效肌肉: " + lastHumanPoseActiveMuscles + " | 最大值: " + lastHumanPoseMaxMuscleAbs.ToString("F3"));
            sb.AppendLine("HumanPose 骨骼变化: " + lastHumanPoseChangedBones + " | 最大角度: " + lastHumanPoseMaxBoneAngle.ToString("F2"));
            if (!string.IsNullOrEmpty(lastHumanPoseChangedBoneSamples))
                sb.AppendLine("HumanPose 变化骨骼示例: " + lastHumanPoseChangedBoneSamples);
            sb.AppendLine("AnimatorController 采样: 已禁用（UnityEditor.Graphs 临时 Controller 风险）");
            sb.AppendLine("AnimationClipPlayable 主路线: " + (playableGraph.IsValid() ? "已启用" : "未启用"));
            if (!string.IsNullOrEmpty(lastInstanceError))
                sb.AppendLine("实例化异常: " + lastInstanceError);
            if (!string.IsNullOrEmpty(lastPlayableError))
                sb.AppendLine("Playable 异常: " + lastPlayableError);
            if (!string.IsNullOrEmpty(lastHumanPoseError))
                sb.AppendLine("HumanPose 采样异常: " + lastHumanPoseError);
            if (!string.IsNullOrEmpty(lastSampleError))
                sb.AppendLine("采样异常: " + lastSampleError);
            if (!string.IsNullOrEmpty(lastInitializationReport))
            {
                sb.AppendLine();
                sb.AppendLine("[初始化链路]");
                sb.Append(lastInitializationReport);
            }

            AppendClipDebug(sb, clip);
            AppendModelDebug(sb, model, resolvedModel);
            AppendBindingMatchDebug(sb, clip, resolvedModel);

            if (previewInstance != null)
            {
                sb.AppendLine("预览实例: 已创建");
                sb.AppendLine("预览 Renderer 数: " + previewInstance.GetComponentsInChildren<Renderer>(true).Length);
                AppendPreviewMaterialDebug(sb, previewInstance);
                sb.AppendLine("目标模型原始 Avatar: " + FormatAvatar(FindAnimatorAvatarOnModel(model)));
                sb.AppendLine("临时 Animator Avatar: " + FormatAvatar(previewAnimator != null ? previewAnimator.avatar : null));
                sb.AppendLine("预览 Bounds: center " + FormatVector(lastBounds.center) + " size " + FormatVector(lastBounds.size));
            }
            else
            {
                sb.AppendLine("预览实例: 未创建");
            }

            sb.AppendLine();
            sb.AppendLine("判断提示:");
            if (clip == null)
                sb.AppendLine("- 没有 AnimationClip，先检查资源导入设置是否生成 Clip。");
            else if (clip.empty || clip.length <= 0.001f)
                sb.AppendLine("- Clip 为空或长度接近 0，播放当然看不到变化。");
            else if (model == null)
                sb.AppendLine("- 没有预览模型，需要拖一个带骨骼/网格的模型或 Prefab。");
            else if (model is GameObject go)
            {
                Animator animator = go.GetComponentInChildren<Animator>(true);
                if (clip.humanMotion && (animator == null || animator.avatar == null || !animator.avatar.isValid))
                    sb.AppendLine("- 这是 Humanoid 动画，但模型没有有效 Avatar，高概率不会动。");

                int totalPaths;
                int matchedPaths = CountMatchedBindingPaths(clip, go, out totalPaths, null);
                if (totalPaths > 0 && matchedPaths == 0 && !clip.humanMotion)
                    sb.AppendLine("- Clip 曲线路径和模型层级 0 匹配，高概率是动画和模型不是同一套骨架。");

                if (go.GetComponentsInChildren<Renderer>(true).Length == 0)
                    sb.AppendLine("- 模型没有 Renderer，即使动画在动也看不到。");
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            Stop();
            previewContext.Dispose();
        }

        private void OnEditorUpdate()
        {
            if (repaintInterval > 0d)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now - lastRepaintTime < repaintInterval)
                    return;

                lastRepaintTime = now;
            }

            RepaintOwner?.Invoke();
        }

        private void RegisterEditorUpdate()
        {
            if (editorUpdateRegistered)
                return;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            editorUpdateRegistered = true;
        }

        private void UnregisterEditorUpdate()
        {
            if (!editorUpdateRegistered)
                return;

            EditorApplication.update -= OnEditorUpdate;
            editorUpdateRegistered = false;
        }

        private void EnsurePreviewUtility()
        {
            previewContext.Ensure();
        }

        private void RenderPreviewCamera(Rect rect, Vector3 center, float radius)
        {
            previewContext.Render(rect, center, radius, renderScale, orbitYaw, orbitPitch, zoomMultiplier, baselinePlatform, repaintInterval);
        }

        private void HandlePreviewInput(Rect rect)
        {
            Event evt = Event.current;
            if (evt == null || !rect.Contains(evt.mousePosition))
                return;

            if (evt.type == EventType.MouseDown && evt.clickCount == 2)
            {
                ResetView();
                RepaintOwner?.Invoke();
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && (evt.button == 0 || evt.button == 1))
            {
                orbitYaw += evt.delta.x * 0.45f;
                orbitPitch = Mathf.Clamp(orbitPitch - evt.delta.y * 0.35f, -65f, 75f);
                SaveSettings();
                RepaintOwner?.Invoke();
                evt.Use();
                return;
            }

            if (evt.type == EventType.ScrollWheel)
            {
                zoomMultiplier = Mathf.Clamp(zoomMultiplier + evt.delta.y * 0.045f, 0.35f, 3.5f);
                SaveSettings();
                RepaintOwner?.Invoke();
                evt.Use();
            }
        }

        private void LoadSettings()
        {
            if (settingsLoaded)
                return;

            autoPlay = EditorPrefs.GetBool(PrefKeyAutoPlay, true);
            followAnimatedBounds = EditorPrefs.GetBool(PrefKeyFollow, true);
            renderScale = Mathf.Clamp(EditorPrefs.GetFloat(PrefKeyRenderScale, 2.5f), 1f, 4f);
            playbackSpeed = Mathf.Clamp(EditorPrefs.GetFloat(PrefKeyPlaybackSpeed, 1f), 0.1f, 3f);
            int platform = EditorPrefs.GetInt(PrefKeyBaselinePlatform, (int)ESAssetPackagePreviewBaselinePlatform.Desktop);
            baselinePlatform = Enum.IsDefined(typeof(ESAssetPackagePreviewBaselinePlatform), platform)
                ? (ESAssetPackagePreviewBaselinePlatform)platform
                : ESAssetPackagePreviewBaselinePlatform.Desktop;
            orbitYaw = EditorPrefs.GetFloat(PrefKeyYaw, 180f);
            orbitPitch = Mathf.Clamp(EditorPrefs.GetFloat(PrefKeyPitch, 8f), -65f, 75f);
            zoomMultiplier = Mathf.Clamp(EditorPrefs.GetFloat(PrefKeyZoom, 1f), 0.35f, 3.5f);
            settingsLoaded = true;
        }

        private void SaveSettings()
        {
            EditorPrefs.SetBool(PrefKeyAutoPlay, autoPlay);
            EditorPrefs.SetBool(PrefKeyFollow, followAnimatedBounds);
            EditorPrefs.SetFloat(PrefKeyRenderScale, renderScale);
            EditorPrefs.SetFloat(PrefKeyPlaybackSpeed, playbackSpeed);
            EditorPrefs.SetInt(PrefKeyBaselinePlatform, (int)baselinePlatform);
            EditorPrefs.SetFloat(PrefKeyYaw, orbitYaw);
            EditorPrefs.SetFloat(PrefKeyPitch, orbitPitch);
            EditorPrefs.SetFloat(PrefKeyZoom, zoomMultiplier);
        }

        private void ResetView()
        {
            orbitYaw = 180f;
            orbitPitch = 8f;
            zoomMultiplier = 1f;
            hasStableCenter = false;
            SaveSettings();
        }

        private void ApplyBaselinePlatformDefaults()
        {
            switch (baselinePlatform)
            {
                case ESAssetPackagePreviewBaselinePlatform.Desktop:
                    renderScale = Mathf.Max(renderScale, 2.5f);
                    break;
                case ESAssetPackagePreviewBaselinePlatform.Mobile:
                    renderScale = Mathf.Clamp(renderScale, 1.5f, 2.5f);
                    break;
                case ESAssetPackagePreviewBaselinePlatform.Fast:
                    renderScale = Mathf.Min(renderScale, 1.25f);
                    break;
            }
        }

        private void EnsureInstance(UnityEngine.Object model, Material fallbackMaterial, AnimationClip clip, Avatar overrideAvatar)
        {
            Avatar resolvedAvatarForCache = ResolveAvatar(model, clip, overrideAvatar);
            if (previewInstance != null &&
                instantiatedModel == model &&
                instantiatedFallbackMaterial == fallbackMaterial &&
                instantiatedClip == clip &&
                instantiatedAvatar == resolvedAvatarForCache)
                return;

            DestroyInstance();
            instantiatedModel = model;
            instantiatedFallbackMaterial = fallbackMaterial;
            instantiatedClip = clip;
            instantiatedAvatar = resolvedAvatarForCache;

            GameObject go = ResolvePreviewGameObjectForPlayer(model);
            if (go != null)
            {
                try
                {
                    lastInstanceError = string.Empty;
                    previewInstance = InstantiatePreviewGameObject(go);
                    if (previewInstance == null)
                    {
                        LastStatus = "无法实例化预览模型";
                        lastInstanceError = "Instantiate 返回空或返回对象不是 GameObject/Component。模型路径: " + AssetDatabase.GetAssetPath(go);
                        return;
                    }

                    previewContext.PreparePreviewObject(previewInstance);
                    previewInstance.transform.position = previewContext.GroupOrigin;
                    previewInstance.transform.rotation = Quaternion.identity;
                    previewInstance.transform.localScale = Vector3.one;
                    DisableRuntimeComponents(previewInstance);
                    ESAssetPackagePreviewUtility.ApplyPreviewFallbackMaterials(previewInstance, fallbackMaterial);
                    EnsurePreviewRenderers(previewInstance);
                    EnsureAnimator(model, clip, resolvedAvatarForCache);
                    restPoseRotations = CaptureHumanoidBoneRotations();
                    RecordInitializationReport(model, clip, resolvedAvatarForCache, fallbackMaterial);
                }
                catch (Exception ex)
                {
                    lastInstanceError = ex.GetType().Name + ": " + ex.Message + " | 模型路径: " + AssetDatabase.GetAssetPath(go);
                    LastStatus = "预览模型实例化失败";
                    DestroyInstance();
                }
            }
            else
            {
                LastStatus = "预览模型无法解析为 GameObject";
                lastInstanceError = "模型类型: " + (model != null ? model.GetType().Name : "<空>") + " | 路径: " + (model != null ? AssetDatabase.GetAssetPath(model) : "<空>");
            }
        }

        private static GameObject ResolvePreviewGameObjectForPlayer(UnityEngine.Object source)
        {
            if (source is GameObject go)
                return go;

            string path = source == null ? string.Empty : AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(path))
                return null;

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject InstantiatePreviewGameObject(GameObject source)
        {
            if (source == null)
                return null;

            UnityEngine.Object instanceObject = null;
            try
            {
                instanceObject = UnityEngine.Object.Instantiate((UnityEngine.Object)source);
            }
            catch (InvalidCastException)
            {
                instanceObject = PrefabUtility.InstantiatePrefab(source);
            }

            if (instanceObject is GameObject go)
                return go;

            if (instanceObject is Component component)
                return component.gameObject;

            if (instanceObject != null)
                UnityEngine.Object.DestroyImmediate(instanceObject);

            return null;
        }

        private void RecordInitializationReport(UnityEngine.Object sourceModel, AnimationClip clip, Avatar resolvedAvatar, Material fallbackMaterial)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("时间: " + DateTime.Now.ToString("HH:mm:ss.fff"));
            sb.AppendLine("Clip: " + FormatAssetLine(clip));
            sb.AppendLine("模型: " + FormatAssetLine(sourceModel));
            sb.AppendLine("预览实例: " + (previewInstance != null ? previewInstance.name : "<无>"));
            sb.AppendLine("Animator: " + (previewAnimator != null ? "存在" : "无"));
            sb.AppendLine("Animator Avatar: " + FormatAvatar(previewAnimator != null ? previewAnimator.avatar : null));
            sb.AppendLine("解析 Avatar: " + FormatAvatar(resolvedAvatar));
            sb.AppendLine("解析 Avatar 路径: " + FormatAssetPath(resolvedAvatar));
            sb.AppendLine("StateMachineConfig.previewModel: " + FormatAssetLine(StateMachineConfig.Instance != null ? StateMachineConfig.Instance.previewModel : null));
            sb.AppendLine("StateMachineConfig.previewAvatar: " + FormatAssetLine(StateMachineConfig.Instance != null ? StateMachineConfig.Instance.previewAvatar : null));
            sb.AppendLine("Fallback 材质: " + (fallbackMaterial != null ? fallbackMaterial.name + " / " + (fallbackMaterial.shader != null ? fallbackMaterial.shader.name : "<无Shader>") : "<无>"));
            sb.AppendLine("Transform 数: " + (previewInstance != null ? previewInstance.GetComponentsInChildren<Transform>(true).Length : 0));
            sb.AppendLine("Renderer 数: " + (previewInstance != null ? previewInstance.GetComponentsInChildren<Renderer>(true).Length : 0));
            sb.AppendLine("SkinnedMeshRenderer 数: " + (previewInstance != null ? previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length : 0));
            sb.AppendLine("采样策略:");
            sb.AppendLine("- 小格采样: 每个小格独占PlayableGraph，按指定时间Evaluate后相机截图");
            sb.AppendLine("- AnimatorController: 已彻底移除，避免 UnityEditor.Graphs.Edge.WakeUp");
            sb.AppendLine("- PlayableGraph: 小格主采样路线；AnimationMode仅保留为大面板/Debug旧兜底");
            sb.AppendLine("- 网格动画: 已停用，不再生成假动作帧");
            sb.AppendLine("预览场景: " + previewContext.IsolationReport);
            sb.AppendLine("[当前修正] AnimationClipPlayable 是主采样路线；HumanPose 只作为失败兜底；不创建 AnimatorController。");
            lastInitializationReport = sb.ToString();

            string key = AssetDatabase.GetAssetPath(clip) + "|" + clip?.name + "|" + AssetDatabase.GetAssetPath(sourceModel);
            if (!string.Equals(lastLoggedInitializationKey, key, StringComparison.Ordinal))
            {
                lastLoggedInitializationKey = key;
                Debug.Log("[ES资产动作预览 初始化]\n" + lastInitializationReport);
            }
        }

        private static string FormatAssetLine(UnityEngine.Object asset)
        {
            if (asset == null)
                return "<无>";

            string path = AssetDatabase.GetAssetPath(asset);
            return asset.name + " | " + asset.GetType().Name + " | " + (string.IsNullOrEmpty(path) ? "<内存对象>" : path);
        }

        private static string FormatAssetPath(UnityEngine.Object asset)
        {
            if (asset == null)
                return "<无>";

            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? "<内存对象>" : path;
        }

        private void EnsureAnimator(UnityEngine.Object sourceModel, AnimationClip clip, Avatar overrideAvatar)
        {
            previewAnimator = previewInstance == null ? null : previewInstance.GetComponentInChildren<Animator>(true);
            if (previewAnimator == null && previewInstance != null)
                previewAnimator = previewInstance.AddComponent<Animator>();

            if (previewAnimator != null)
            {
                if (clip != null && clip.humanMotion)
                {
                    Avatar resolvedAvatar = ResolveAvatar(sourceModel, clip, overrideAvatar);
                    if (resolvedAvatar != null && previewAnimator.avatar != resolvedAvatar)
                        previewAnimator.avatar = resolvedAvatar;
                }

                previewAnimator.enabled = true;
                previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                previewAnimator.updateMode = AnimatorUpdateMode.Normal;
                previewAnimator.applyRootMotion = false;
                previewAnimator.Rebind();
                previewAnimator.Update(0f);
            }
        }

        private void BuildPlayableGraph(AnimationClip clip)
        {
            DestroyPlayableGraph();
            DestroyHumanPoseSampler();
            lastPlayableError = string.Empty;
            if (previewAnimator == null || clip == null)
            {
                LastStatus = previewAnimator == null ? "预览模型缺少 Animator，已尝试自动添加但失败" : "动画片段为空";
                return;
            }

            playableGraph = PlayableGraph.Create("ESAssetPackageAnimationPreview");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(playableGraph, "Animation", previewAnimator);
            basePoseClip = ResolvePreviewBasePoseClip(clip);
            bool hasBasePose = basePoseClip != null;
            previewMixer = AnimationMixerPlayable.Create(playableGraph, hasBasePose ? 2 : 1);
            clipInputIndex = hasBasePose ? 1 : 0;
            basePoseInputIndex = hasBasePose ? 0 : -1;
            if (hasBasePose)
            {
                basePosePlayable = AnimationClipPlayable.Create(playableGraph, basePoseClip);
                basePosePlayable.SetApplyFootIK(false);
                basePosePlayable.SetApplyPlayableIK(false);
                basePosePlayable.SetSpeed(0d);
                basePosePlayable.SetTime(0d);
                playableGraph.Connect(basePosePlayable, 0, previewMixer, basePoseInputIndex);
                previewMixer.SetInputWeight(basePoseInputIndex, 0f);
            }

            clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            clipPlayable.SetApplyFootIK(true);
            clipPlayable.SetApplyPlayableIK(true);
            clipPlayable.SetSpeed(0d);
            playableGraph.Connect(clipPlayable, 0, previewMixer, clipInputIndex);
            previewMixer.SetInputWeight(clipInputIndex, 1f);
            output.SetSourcePlayable(previewMixer);
            playableGraph.Play();
            lastPlayableGraphTime = -1d;
            LastStatus = "Playable 已创建";
        }

        private static Avatar ResolveAvatar(UnityEngine.Object sourceModel, AnimationClip clip, Avatar overrideAvatar)
        {
            Avatar avatar = FindAnimatorAvatarOnModel(sourceModel);
            if (avatar != null)
                return avatar;

            if (IsValidHumanoidAvatar(overrideAvatar))
                return overrideAvatar;

            avatar = FindSourceAvatarAtPath(AssetDatabase.GetAssetPath(sourceModel));
            if (avatar != null)
                return avatar;

            avatar = FindValidAvatarAtPath(AssetDatabase.GetAssetPath(sourceModel));
            if (avatar != null)
                return avatar;

            avatar = FindSourceAvatarAtPath(AssetDatabase.GetAssetPath(clip));
            if (avatar != null)
                return avatar;

            return FindValidAvatarAtPath(AssetDatabase.GetAssetPath(clip));
        }

        private static Avatar FindAnimatorAvatarOnModel(UnityEngine.Object sourceModel)
        {
            GameObject go = ResolvePreviewGameObjectForPlayer(sourceModel);
            if (go == null)
                return null;

            Animator animator = go.GetComponentInChildren<Animator>(true);
            return animator != null && IsValidHumanoidAvatar(animator.avatar) ? animator.avatar : null;
        }

        private static Avatar FindSourceAvatarAtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
                return null;

            return IsValidHumanoidAvatar(importer.sourceAvatar) ? importer.sourceAvatar : null;
        }

        private static Avatar FindValidAvatarAtPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar && IsValidHumanoidAvatar(avatar))
                    return avatar;
            }

            return null;
        }

        private static AnimationClip ResolvePreviewBasePoseClip(AnimationClip playingClip)
        {
            StateMachineConfig config = StateMachineConfig.Instance;
            AnimationClip idleClip = config != null ? config.previewIdleClip : null;
            if (idleClip == null || idleClip == playingClip)
                return null;

            return idleClip;
        }

        private static bool IsValidHumanoidAvatar(Avatar avatar)
        {
            return avatar != null && avatar.isValid && avatar.isHuman;
        }

        private sealed class HumanPoseCurveSet
        {
            private readonly AnimationCurve[] muscleCurves;
            private AnimationCurve rootTX;
            private AnimationCurve rootTY;
            private AnimationCurve rootTZ;
            private AnimationCurve rootQX;
            private AnimationCurve rootQY;
            private AnimationCurve rootQZ;
            private AnimationCurve rootQW;

            public int MuscleCurveCount { get; private set; }

            private HumanPoseCurveSet()
            {
                muscleCurves = new AnimationCurve[HumanTrait.MuscleCount];
            }

            public static HumanPoseCurveSet Build(AnimationClip clip)
            {
                if (clip == null || !clip.humanMotion)
                    return null;

                var result = new HumanPoseCurveSet();
                Dictionary<string, int> muscleIndexByName = BuildMuscleIndexByName();
                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                for (int i = 0; i < bindings.Length; i++)
                {
                    EditorCurveBinding binding = bindings[i];
                    if (binding.type != typeof(Animator))
                        continue;

                    string propertyName = binding.propertyName ?? string.Empty;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve == null)
                        continue;

                    if (muscleIndexByName.TryGetValue(propertyName, out int muscleIndex))
                    {
                        result.muscleCurves[muscleIndex] = curve;
                        result.MuscleCurveCount++;
                        continue;
                    }

                    result.AssignRootCurve(propertyName, curve);
                }

                return result;
            }

            public void Sample(float time, ref HumanPose pose, out int activeMuscles, out float maxMuscleAbs)
            {
                activeMuscles = 0;
                maxMuscleAbs = 0f;
                for (int i = 0; i < muscleCurves.Length; i++)
                {
                    AnimationCurve curve = muscleCurves[i];
                    if (curve == null)
                        continue;

                    float value = curve.Evaluate(time);
                    pose.muscles[i] = value;
                    float abs = Mathf.Abs(value);
                    if (abs > 0.001f)
                        activeMuscles++;
                    if (abs > maxMuscleAbs)
                        maxMuscleAbs = abs;
                }

            }

            public void ApplyRootMotion(float time, GameObject target, Vector3 groupOrigin)
            {
                if (target == null)
                    return;

                Vector3 rootOffset = Vector3.zero;
                bool hasRootPosition = false;
                if (rootTX != null)
                {
                    rootOffset.x = rootTX.Evaluate(time);
                    hasRootPosition = true;
                }
                if (rootTY != null)
                {
                    rootOffset.y = rootTY.Evaluate(time);
                    hasRootPosition = true;
                }
                if (rootTZ != null)
                {
                    rootOffset.z = rootTZ.Evaluate(time);
                    hasRootPosition = true;
                }

                bool hasRootRotation = rootQX != null || rootQY != null || rootQZ != null || rootQW != null;
                Quaternion rootRotation = Quaternion.identity;
                if (hasRootRotation)
                {
                    rootRotation = NormalizeQuaternion(new Quaternion(
                        rootQX != null ? rootQX.Evaluate(time) : 0f,
                        rootQY != null ? rootQY.Evaluate(time) : 0f,
                        rootQZ != null ? rootQZ.Evaluate(time) : 0f,
                        rootQW != null ? rootQW.Evaluate(time) : 1f));
                }

                if (hasRootPosition)
                    target.transform.position = groupOrigin + rootOffset;
                if (hasRootRotation)
                    target.transform.rotation = rootRotation;
            }

            private void AssignRootCurve(string propertyName, AnimationCurve curve)
            {
                switch (propertyName)
                {
                    case "RootT.x":
                        rootTX = curve;
                        break;
                    case "RootT.y":
                        rootTY = curve;
                        break;
                    case "RootT.z":
                        rootTZ = curve;
                        break;
                    case "RootQ.x":
                        rootQX = curve;
                        break;
                    case "RootQ.y":
                        rootQY = curve;
                        break;
                    case "RootQ.z":
                        rootQZ = curve;
                        break;
                    case "RootQ.w":
                        rootQW = curve;
                        break;
                }
            }

            public static Dictionary<string, int> BuildMuscleIndexByName()
            {
                var result = new Dictionary<string, int>(HumanTrait.MuscleCount, StringComparer.Ordinal);
                for (int i = 0; i < HumanTrait.MuscleCount; i++)
                    result[HumanTrait.MuscleName[i]] = i;
                return result;
            }

            private static Quaternion NormalizeQuaternion(Quaternion value)
            {
                float length = Mathf.Sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
                if (length <= 0.0001f)
                    return Quaternion.identity;

                float inverse = 1f / length;
                return new Quaternion(value.x * inverse, value.y * inverse, value.z * inverse, value.w * inverse);
            }
        }

        private bool EvaluateHumanPoseClip(AnimationClip clip, float time)
        {
            if (!EnsureHumanPoseSampler(clip))
                return false;

            try
            {
                DestroyPlayableGraph();

                HumanPose pose = humanPoseBase;
                if (pose.muscles == null || pose.muscles.Length != HumanTrait.MuscleCount)
                    pose.muscles = new float[HumanTrait.MuscleCount];
                else
                    pose.muscles = (float[])pose.muscles.Clone();

                Dictionary<HumanBodyBones, Quaternion> beforeRotations = CaptureHumanoidBoneRotations();
                humanPoseCurveSet.Sample(time, ref pose, out lastHumanPoseActiveMuscles, out lastHumanPoseMaxMuscleAbs);
                humanPoseHandler.SetHumanPose(ref pose);
                humanPoseCurveSet.ApplyRootMotion(time, previewInstance, previewContext.GroupOrigin);
                MeasureHumanoidBoneRotationDelta(beforeRotations);
                lastEvaluatedTime = time;
                lastHumanPoseError = string.Empty;
                lastSamplingMode = "HumanPoseHandler.SetHumanPose";
                LastStatus = "播放中";
                CommitAnimatorPose(false);
                return true;
            }
            catch (Exception ex)
            {
                lastHumanPoseError = ex.GetType().Name + ": " + ex.Message;
                DestroyHumanPoseSampler();
                return false;
            }
        }

        private bool EnsureHumanPoseSampler(AnimationClip clip)
        {
            if (previewAnimator == null || clip == null || !clip.humanMotion)
                return false;

            Avatar avatar = previewAnimator.avatar;
            if (!IsValidHumanoidAvatar(avatar))
                return false;

            Transform root = previewAnimator.transform;
            if (humanPoseHandler != null && humanPoseClip == clip && humanPoseAvatar == avatar && humanPoseRoot == root)
                return true;

            DestroyHumanPoseSampler();

            try
            {
                humanPoseCurveSet = HumanPoseCurveSet.Build(clip);
                if (humanPoseCurveSet == null || humanPoseCurveSet.MuscleCurveCount <= 0)
                {
                    lastHumanPoseError = "Clip 没有命中 Unity HumanTrait 肌肉曲线。";
                    return false;
                }

                humanPoseHandler = new HumanPoseHandler(avatar, root);
                humanPoseAvatar = avatar;
                humanPoseRoot = root;
                humanPoseClip = clip;
                humanPoseBase = new HumanPose();
                humanPoseHandler.GetHumanPose(ref humanPoseBase);
                if (humanPoseBase.muscles == null || humanPoseBase.muscles.Length != HumanTrait.MuscleCount)
                    humanPoseBase.muscles = new float[HumanTrait.MuscleCount];

                lastHumanPoseError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                lastHumanPoseError = ex.GetType().Name + ": " + ex.Message;
                DestroyHumanPoseSampler();
                return false;
            }
        }

        private Dictionary<HumanBodyBones, Quaternion> CaptureHumanoidBoneRotations()
        {
            var result = new Dictionary<HumanBodyBones, Quaternion>();
            if (previewAnimator == null)
                return result;

            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                HumanBodyBones bone = (HumanBodyBones)i;
                Transform transform = null;
                try
                {
                    transform = previewAnimator.GetBoneTransform(bone);
                }
                catch
                {
                }

                if (transform != null)
                    result[bone] = transform.localRotation;
            }

            return result;
        }

        private void MeasureHumanoidBoneRotationDelta(Dictionary<HumanBodyBones, Quaternion> beforeRotations)
        {
            lastHumanPoseChangedBones = 0;
            lastHumanPoseMaxBoneAngle = 0f;
            lastHumanPoseChangedBoneSamples = string.Empty;
            if (previewAnimator == null || beforeRotations == null || beforeRotations.Count == 0)
                return;

            var samples = new List<string>();
            foreach (KeyValuePair<HumanBodyBones, Quaternion> pair in beforeRotations)
            {
                Transform transform = null;
                try
                {
                    transform = previewAnimator.GetBoneTransform(pair.Key);
                }
                catch
                {
                }

                if (transform == null)
                    continue;

                float angle = Quaternion.Angle(pair.Value, transform.localRotation);
                if (angle <= 0.05f)
                    continue;

                lastHumanPoseChangedBones++;
                if (angle > lastHumanPoseMaxBoneAngle)
                    lastHumanPoseMaxBoneAngle = angle;
                if (samples.Count < 6)
                    samples.Add(pair.Key + ":" + angle.ToString("F1"));
            }

            lastHumanPoseChangedBoneSamples = string.Join(", ", samples);
        }

        private bool EvaluatePlayableClip(AnimationClip clip, float time)
        {
            if ((!playableGraph.IsValid() || !clipPlayable.IsValid()) && previewAnimator != null && clip != null)
                BuildPlayableGraph(clip);

            if (!playableGraph.IsValid() || !clipPlayable.IsValid() || clip == null)
                return false;

            try
            {
                lastEvaluatedTime = time;
                clipPlayable.SetDuration(Mathf.Max(0.01f, clip.length));
                double targetTime = Mathf.Clamp(time, 0f, Mathf.Max(0.001f, clip.length));
                clipPlayable.SetTime(targetTime);

                if (previewMixer.IsValid())
                {
                    if (basePoseInputIndex >= 0 && basePosePlayable.IsValid() && basePoseClip != null)
                    {
                        basePosePlayable.SetTime(Mathf.Repeat((float)targetTime, Mathf.Max(0.0001f, basePoseClip.length)));
                        previewMixer.SetInputWeight(basePoseInputIndex, 0f);
                    }

                    if (clipInputIndex >= 0)
                        previewMixer.SetInputWeight(clipInputIndex, 1f);
                }

                playableGraph.Evaluate(0.0001f);
                lastPlayableGraphTime = targetTime;
                CommitAnimatorPose(false);
                lastSamplingMode = "AnimationMixerPlayable + AnimationClipPlayable(absolute seek)";
                lastPlayableError = string.Empty;
                LastStatus = "播放中";
                return true;
            }
            catch (Exception ex)
            {
                lastPlayableError = ex.GetType().Name + ": " + ex.Message;
                DestroyPlayableGraph();
                LastStatus = "Playable 播放失败，已尝试采样兜底";
                return false;
            }
        }

        private bool TrySamplePlayableGraph(float time)
        {
            MethodInfo method = GetSamplePlayableGraphMethod();
            if (method == null || !playableGraph.IsValid())
                return false;

            bool beganSampling = false;
            try
            {
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    ownsAnimationMode = true;
                }

                AnimationMode.BeginSampling();
                beganSampling = true;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 3)
                    method.Invoke(null, new object[] { playableGraph, 0, time });
                else if (parameters.Length == 2)
                    method.Invoke(null, new object[] { playableGraph, time });
                else
                    return false;

                lastSamplingMode = "AnimationMode.SamplePlayableGraph";
                return true;
            }
            catch (Exception ex)
            {
                lastPlayableError = "SamplePlayableGraph " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                if (beganSampling)
                {
                    try
                    {
                        AnimationMode.EndSampling();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void CommitAnimatorPose(bool updateAnimator = true)
        {
            if (previewAnimator == null || previewInstance == null)
                return;

            if (updateAnimator)
            {
                try
                {
                    previewAnimator.Update(0f);
                }
                catch (Exception ex)
                {
                    lastSampleError = "Animator.Update " + ex.GetType().Name + ": " + ex.Message;
                }
            }

            SkinnedMeshRenderer[] skinnedRenderers = previewInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                if (skinnedRenderers[i] == null)
                    continue;

                skinnedRenderers[i].updateWhenOffscreen = true;
                Bounds bounds = skinnedRenderers[i].localBounds;
                if (bounds.size.sqrMagnitude < 0.0001f)
                    skinnedRenderers[i].localBounds = new Bounds(Vector3.zero, Vector3.one * 3f);
            }
        }

        private static MethodInfo GetSamplePlayableGraphMethod()
        {
            if (samplePlayableGraphMethod != null)
                return samplePlayableGraphMethod;

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods = typeof(AnimationMode).GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "SamplePlayableGraph")
                    continue;

                ParameterInfo[] parameters = methods[i].GetParameters();
                if (parameters.Length >= 2 && parameters[0].ParameterType == typeof(PlayableGraph))
                {
                    samplePlayableGraphMethod = methods[i];
                    return samplePlayableGraphMethod;
                }
            }

            return null;
        }

        private void DestroyPlayableGraph()
        {
            if (playableGraph.IsValid())
                playableGraph.Destroy();
            previewMixer = default;
            clipPlayable = default;
            basePosePlayable = default;
            basePoseClip = null;
            clipInputIndex = -1;
            basePoseInputIndex = -1;
            lastPlayableGraphTime = -1d;
        }

        private void DestroyHumanPoseSampler()
        {
            if (humanPoseHandler != null)
            {
                try
                {
                    humanPoseHandler.Dispose();
                }
                catch
                {
                }
            }

            humanPoseHandler = null;
            humanPoseCurveSet = null;
            humanPoseAvatar = null;
            humanPoseRoot = null;
            humanPoseClip = null;
            humanPoseBase = default;
            lastHumanPoseActiveMuscles = 0;
            lastHumanPoseMaxMuscleAbs = 0f;
            lastHumanPoseChangedBones = 0;
            lastHumanPoseMaxBoneAngle = 0f;
            lastHumanPoseChangedBoneSamples = string.Empty;
        }

        private GameObject ResolveAnimationModeSampleTarget(GameObject root)
        {
            if (previewAnimator != null && previewAnimator.gameObject != null)
                return previewAnimator.gameObject;

            return root;
        }

        private void MeasureAnimationModeBoneRotationDelta(Dictionary<HumanBodyBones, Quaternion> beforeRotations)
        {
            lastAnimationModeChangedBones = 0;
            lastAnimationModeMaxBoneAngle = 0f;
            lastAnimationModeChangedBoneSamples = string.Empty;
            if (previewAnimator == null || beforeRotations == null || beforeRotations.Count == 0)
                return;

            var samples = new List<string>();
            foreach (KeyValuePair<HumanBodyBones, Quaternion> pair in beforeRotations)
            {
                Transform transform = null;
                try
                {
                    transform = previewAnimator.GetBoneTransform(pair.Key);
                }
                catch
                {
                }

                if (transform == null)
                    continue;

                float angle = Quaternion.Angle(pair.Value, transform.localRotation);
                if (angle <= 0.05f)
                    continue;

                lastAnimationModeChangedBones++;
                if (angle > lastAnimationModeMaxBoneAngle)
                    lastAnimationModeMaxBoneAngle = angle;
                if (samples.Count < 6)
                    samples.Add(pair.Key + ":" + angle.ToString("F1"));
            }

            lastAnimationModeChangedBoneSamples = string.Join(", ", samples);
        }

        private void MeasureRestPoseDelta()
        {
            lastRestPoseChangedBones = 0;
            lastRestPoseMaxBoneAngle = 0f;
            lastRestPoseChangedBoneSamples = string.Empty;
            if (previewAnimator == null || restPoseRotations == null || restPoseRotations.Count == 0)
                return;

            var samples = new List<string>();
            foreach (KeyValuePair<HumanBodyBones, Quaternion> pair in restPoseRotations)
            {
                Transform transform = null;
                try
                {
                    transform = previewAnimator.GetBoneTransform(pair.Key);
                }
                catch
                {
                }

                if (transform == null)
                    continue;

                float angle = Quaternion.Angle(pair.Value, transform.localRotation);
                if (angle <= 0.10f)
                    continue;

                lastRestPoseChangedBones++;
                if (angle > lastRestPoseMaxBoneAngle)
                    lastRestPoseMaxBoneAngle = angle;
                if (samples.Count < 6)
                    samples.Add(pair.Key + ":" + angle.ToString("F1"));
            }

            lastRestPoseChangedBoneSamples = string.Join(", ", samples);
        }

        private void RunDebugProbe(AnimationClip clip)
        {
            lastProbeSampleTime = 0f;
            lastProbeChangedBones = 0;
            lastProbeMaxBoneAngle = 0f;
            lastProbeChangedBoneSamples = string.Empty;
            lastProbeStatus = "未执行";

            if (playingClip != null)
            {
                lastProbeStatus = "播放中，使用实时采样结果";
                return;
            }

            if (previewInstance == null || previewAnimator == null || clip == null || clip.length <= 0.001f)
            {
                lastProbeStatus = "缺少预览实例/Animator/Clip";
                return;
            }

            GameObject sampleTarget = ResolveAnimationModeSampleTarget(previewInstance);
            if (sampleTarget == null)
            {
                lastProbeStatus = "采样目标为空";
                return;
            }

            bool beganSampling = false;
            try
            {
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    ownsAnimationMode = true;
                }

                AnimationMode.BeginSampling();
                beganSampling = true;
                AnimationMode.SampleAnimationClip(sampleTarget, clip, 0f);
                AnimationMode.EndSampling();
                beganSampling = false;

                Dictionary<HumanBodyBones, Quaternion> beforeRotations = CaptureHumanoidBoneRotations();
                lastProbeSampleTime = Mathf.Clamp(clip.length * 0.5f, 0.033f, Mathf.Max(0.033f, clip.length));

                AnimationMode.BeginSampling();
                beganSampling = true;
                AnimationMode.SampleAnimationClip(sampleTarget, clip, lastProbeSampleTime);
                AnimationMode.EndSampling();
                beganSampling = false;

                MeasureProbeBoneRotationDelta(beforeRotations);
                lastProbeStatus = "已执行";
            }
            catch (Exception ex)
            {
                lastProbeStatus = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                if (beganSampling)
                {
                    try
                    {
                        AnimationMode.EndSampling();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void MeasureProbeBoneRotationDelta(Dictionary<HumanBodyBones, Quaternion> beforeRotations)
        {
            if (previewAnimator == null || beforeRotations == null || beforeRotations.Count == 0)
                return;

            var samples = new List<string>();
            foreach (KeyValuePair<HumanBodyBones, Quaternion> pair in beforeRotations)
            {
                Transform transform = null;
                try
                {
                    transform = previewAnimator.GetBoneTransform(pair.Key);
                }
                catch
                {
                }

                if (transform == null)
                    continue;

                float angle = Quaternion.Angle(pair.Value, transform.localRotation);
                if (angle <= 0.05f)
                    continue;

                lastProbeChangedBones++;
                if (angle > lastProbeMaxBoneAngle)
                    lastProbeMaxBoneAngle = angle;
                if (samples.Count < 6)
                    samples.Add(pair.Key + ":" + angle.ToString("F1"));
            }

            lastProbeChangedBoneSamples = string.Join(", ", samples);
        }

        private bool SampleClip(GameObject target, AnimationClip clip, float time)
        {
            if (target == null || clip == null)
                return false;

            GameObject sampleTarget = ResolveAnimationModeSampleTarget(target);
            if (sampleTarget == null)
                return false;

            bool hadPlayableDriver = playableGraph.IsValid() || clipPlayable.IsValid() || previewMixer.IsValid();
            bool hadHumanPoseDriver = humanPoseHandler != null;
            if (hadPlayableDriver || hadHumanPoseDriver)
            {
                lastDriverConflict = "采样前清理后备驱动: Playable=" + hadPlayableDriver + ", HumanPose=" + hadHumanPoseDriver;
                DestroyPlayableGraph();
                DestroyHumanPoseSampler();
            }
            else
            {
                lastDriverConflict = "无";
            }

            animationModeSampleCount++;
            lastSampleTargetInstanceId = sampleTarget.GetInstanceID();
            lastSampleTargetName = sampleTarget.name;
            lastSampleTargetScene = sampleTarget.scene.IsValid() ? sampleTarget.scene.name : "<invalid>";
            lastSampleTargetIsAnimatorObject = previewAnimator != null && previewAnimator.gameObject == sampleTarget;
            lastRequestedSampleTime = time;
            lastSampleEditorTime = EditorApplication.timeSinceStartup;
            lastAnimationModeAlreadyActive = AnimationMode.InAnimationMode();

            bool sampledByAnimationMode = false;
            bool beganSampling = false;
            try
            {
                lastEvaluatedTime = time;
                if (!AnimationMode.InAnimationMode())
                {
                    AnimationMode.StartAnimationMode();
                    ownsAnimationMode = true;
                }

                Dictionary<HumanBodyBones, Quaternion> beforeRotations = CaptureHumanoidBoneRotations();
                AnimationMode.BeginSampling();
                beganSampling = true;
                AnimationMode.SampleAnimationClip(sampleTarget, clip, time);
                MeasureAnimationModeBoneRotationDelta(beforeRotations);
                sampledByAnimationMode = true;
                lastSamplingMode = "AnimationMode.SampleAnimationClip";
                lastSampleError = string.Empty;
                SceneView.RepaintAll();
            }
            catch (Exception ex)
            {
                lastSampleError = "AnimationMode " + ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                if (beganSampling)
                {
                    try
                    {
                        AnimationMode.EndSampling();
                    }
                    catch
                    {
                    }
                }
            }

            if (!sampledByAnimationMode)
            {
                try
                {
                    clip.SampleAnimation(target, time);
                    sampledByAnimationMode = true;
                    lastSamplingMode = "AnimationClip.SampleAnimation";
                    lastSampleError = string.Empty;
                }
                catch (Exception ex)
                {
                    lastSampleError = "SampleAnimation " + ex.GetType().Name + ": " + ex.Message;
                }
            }

            CommitAnimatorPose(false);
            return sampledByAnimationMode;
        }

        private static void AppendClipDebug(StringBuilder sb, AnimationClip clip)
        {
            sb.AppendLine();
            sb.AppendLine("[Clip]");
            if (clip == null)
            {
                sb.AppendLine("Clip: 空");
                return;
            }

            string clipPath = AssetDatabase.GetAssetPath(clip);
            EditorCurveBinding[] curveBindings = AnimationUtility.GetCurveBindings(clip);
            EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            sb.AppendLine("名称: " + clip.name);
            sb.AppendLine("路径: " + (string.IsNullOrEmpty(clipPath) ? "<内存或子资源>" : clipPath));
            AppendModelImporterDebug(sb, clipPath, "Clip 所在 FBX 导入器");
            sb.AppendLine("长度: " + clip.length.ToString("F3") + " 秒");
            sb.AppendLine("帧率: " + clip.frameRate.ToString("F1"));
            sb.AppendLine("为空: " + (clip.empty ? "是" : "否"));
            sb.AppendLine("Legacy: " + (clip.legacy ? "是" : "否"));
            sb.AppendLine("Humanoid Motion: " + (clip.humanMotion ? "是" : "否"));
            sb.AppendLine("曲线数量: " + curveBindings.Length);
            sb.AppendLine("对象引用曲线: " + objectBindings.Length);

            CountHumanoidCurveKinds(curveBindings, out int rootOrIkCurves, out int poseCurves);
            sb.AppendLine("Root/IK 曲线数: " + rootOrIkCurves);
            sb.AppendLine("身体姿态曲线数: " + poseCurves);
            if (clip.humanMotion && rootOrIkCurves > 0 && poseCurves == 0)
                sb.AppendLine("提示: 当前 Clip 更像 RootMotion/IK 片段，本身可能不包含身体姿态。");

            for (int i = 0; i < Mathf.Min(8, curveBindings.Length); i++)
            {
                EditorCurveBinding binding = curveBindings[i];
                sb.AppendLine("曲线[" + i + "]: path='" + binding.path + "' type=" + binding.type.Name + " prop=" + binding.propertyName);
            }
        }

        private static void CountHumanoidCurveKinds(EditorCurveBinding[] bindings, out int rootOrIkCurves, out int poseCurves)
        {
            rootOrIkCurves = 0;
            poseCurves = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                string property = bindings[i].propertyName ?? string.Empty;
                if (ESAssetPackagePreviewUtility.IsRootOrIkHumanoidPropertyForDebug(property))
                    rootOrIkCurves++;
                else
                    poseCurves++;
            }
        }

        private static void AppendModelDebug(StringBuilder sb, UnityEngine.Object model, GameObject resolvedModel)
        {
            sb.AppendLine();
            sb.AppendLine("[模型]");
            if (model == null)
            {
                sb.AppendLine("模型: 空");
                return;
            }

            sb.AppendLine("名称: " + model.name);
            sb.AppendLine("路径: " + AssetDatabase.GetAssetPath(model));
            sb.AppendLine("原始引用类型: " + model.GetType().Name);
            if (resolvedModel == null)
            {
                sb.AppendLine("解析结果: 无法从该引用解析出 GameObject。");
                return;
            }

            GameObject go = resolvedModel;
            sb.AppendLine("解析 GameObject: " + go.name);

            string modelPath = AssetDatabase.GetAssetPath(model);
            AppendModelImporterDebug(sb, modelPath, "模型 FBX 导入器");
            AppendAvatarAssetDebug(sb, modelPath);

            Transform[] transforms = go.GetComponentsInChildren<Transform>(true);
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            SkinnedMeshRenderer[] skinnedRenderers = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Animator[] animators = go.GetComponentsInChildren<Animator>(true);
            Animation[] animations = go.GetComponentsInChildren<Animation>(true);
            sb.AppendLine("Transform 数: " + transforms.Length);
            sb.AppendLine("Renderer 数: " + renderers.Length);
            sb.AppendLine("SkinnedMeshRenderer 数: " + skinnedRenderers.Length);
            sb.AppendLine("Animator 数: " + animators.Length);
            sb.AppendLine("Animation 组件数: " + animations.Length);

            Animator animator = go.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                sb.AppendLine("Animator: 无，预览器会临时添加一个 Animator。");
            }
            else
            {
                sb.AppendLine("Animator.enabled: " + animator.enabled);
                sb.AppendLine("Animator.avatar: " + (animator.avatar != null ? animator.avatar.name : "<无>"));
                if (animator.avatar != null)
                {
                    sb.AppendLine("Avatar 有效: " + (animator.avatar.isValid ? "是" : "否"));
                    sb.AppendLine("Avatar Human: " + (animator.avatar.isHuman ? "是" : "否"));
                }
                sb.AppendLine("Controller: " + (animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "<无>"));
            }

            sb.AppendLine("前 12 个骨骼/节点路径:");
            HashSet<string> paths = BuildTransformPathLookup(go);
            int shown = 0;
            foreach (string path in paths.OrderBy(x => x.Length).ThenBy(x => x))
            {
                sb.AppendLine("- " + (string.IsNullOrEmpty(path) ? "<Root>" : path));
                shown++;
                if (shown >= 12)
                    break;
            }
        }

        private static void AppendPreviewMaterialDebug(StringBuilder sb, GameObject root)
        {
            if (root == null)
                return;

            int materialCount = 0;
            int riskyCount = 0;
            var riskyNames = new List<string>();
            var shaderSamples = new List<string>();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    materialCount++;
                    Material material = materials[m];
                    if (shaderSamples.Count < 8)
                        shaderSamples.Add((material != null ? material.name : "<空材质>") + " / " + (material != null && material.shader != null ? material.shader.name : "<无Shader>"));

                    if (ESAssetPackagePreviewUtility.IsProblematicPreviewMaterial(material))
                    {
                        riskyCount++;
                        if (riskyNames.Count < 8)
                            riskyNames.Add((material != null ? material.name : "<空材质>") + " / " + (material != null && material.shader != null ? material.shader.name : "<无Shader>"));
                    }
                }
            }

            sb.AppendLine("预览材质数: " + materialCount);
            sb.AppendLine("仍有粉色风险材质数: " + riskyCount);
            for (int i = 0; i < shaderSamples.Count; i++)
                sb.AppendLine("- 材质Shader: " + shaderSamples[i]);
            for (int i = 0; i < riskyNames.Count; i++)
                sb.AppendLine("- 风险材质: " + riskyNames[i]);
        }

        private static void AppendAvatarAssetDebug(StringBuilder sb, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            Avatar[] avatars = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Avatar>().ToArray();
            sb.AppendLine("FBX Avatar 候选数: " + avatars.Length);
            for (int i = 0; i < Mathf.Min(4, avatars.Length); i++)
            {
                Avatar avatar = avatars[i];
                sb.AppendLine("Avatar[" + i + "]: " + avatar.name +
                              " valid=" + (avatar.isValid ? "是" : "否") +
                              " human=" + (avatar.isHuman ? "是" : "否"));
            }
        }

        private static void AppendModelImporterDebug(StringBuilder sb, string assetPath, string title)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
                return;

            sb.AppendLine(title + ":");
            sb.AppendLine("- animationType: " + importer.animationType);
            sb.AppendLine("- avatarSetup: " + importer.avatarSetup);
            sb.AppendLine("- sourceAvatar: " + FormatAvatar(importer.sourceAvatar));
        }

        private static string FormatAvatar(Avatar avatar)
        {
            if (avatar == null)
                return "<无>";

            return avatar.name + " valid=" + (avatar.isValid ? "是" : "否") + " human=" + (avatar.isHuman ? "是" : "否");
        }

        private static void AppendBindingMatchDebug(StringBuilder sb, AnimationClip clip, GameObject model)
        {
            sb.AppendLine();
            sb.AppendLine("[曲线路径匹配]");
            if (clip == null || model == null)
            {
                sb.AppendLine("无法检查：Clip 或模型为空。");
                return;
            }

            var unmatched = new List<string>();
            int totalPaths;
            int matchedPaths = CountMatchedBindingPaths(clip, model, out totalPaths, unmatched);
            sb.AppendLine("Clip 唯一路径数: " + totalPaths);
            sb.AppendLine("模型匹配路径数: " + matchedPaths);
            sb.AppendLine("匹配率: " + (totalPaths == 0 ? "无路径曲线" : ((matchedPaths * 100f) / totalPaths).ToString("F1") + "%"));

            if (unmatched.Count > 0)
            {
                sb.AppendLine("未匹配路径示例:");
                for (int i = 0; i < Mathf.Min(12, unmatched.Count); i++)
                    sb.AppendLine("- " + unmatched[i]);
            }
        }

        private static int CountMatchedBindingPaths(AnimationClip clip, GameObject model, out int totalPaths, List<string> unmatched)
        {
            totalPaths = 0;
            if (clip == null || model == null)
                return 0;

            HashSet<string> modelPaths = BuildTransformPathLookup(model);
            HashSet<string> bindingPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                bindingPaths.Add(binding.path ?? string.Empty);
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                bindingPaths.Add(binding.path ?? string.Empty);

            totalPaths = bindingPaths.Count;
            int matched = 0;
            foreach (string path in bindingPaths)
            {
                if (modelPaths.Contains(path))
                    matched++;
                else
                    unmatched?.Add(string.IsNullOrEmpty(path) ? "<Root>" : path);
            }

            return matched;
        }

        private static HashSet<string> BuildTransformPathLookup(GameObject root)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            if (root == null)
                return paths;

            Transform rootTransform = root.transform;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
                paths.Add(GetRelativeTransformPath(rootTransform, transforms[i]));
            return paths;
        }

        private static string GetRelativeTransformPath(Transform root, Transform current)
        {
            if (root == null || current == null || current == root)
                return string.Empty;

            var names = new Stack<string>();
            Transform t = current;
            while (t != null && t != root)
            {
                names.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("F3") + ", " + value.y.ToString("F3") + ", " + value.z.ToString("F3") + ")";
        }

        private void StopOwnAnimationMode()
        {
            if (!ownsAnimationMode)
                return;

            try
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
            }
            catch
            {
            }
            finally
            {
                ownsAnimationMode = false;
            }
        }

        private static void DisableRuntimeComponents(GameObject root)
        {
            Behaviour[] behaviours = root.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is Animator || behaviours[i] is Animation)
                    continue;

                behaviours[i].enabled = false;
            }
        }

        private static void EnsurePreviewRenderers(GameObject root)
        {
            if (root == null)
                return;

            SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
                skinnedRenderers[i].updateWhenOffscreen = true;
        }

        private void DestroyInstance()
        {
            DestroyHumanPoseSampler();

            if (previewInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(previewInstance);
                previewInstance = null;
            }

            previewAnimator = null;
            instantiatedModel = null;
            instantiatedClip = null;
            instantiatedAvatar = null;
            hasStableCenter = false;
            instantiatedFallbackMaterial = null;
            restPoseRotations = new Dictionary<HumanBodyBones, Quaternion>();
            lastRestPoseChangedBones = 0;
            lastRestPoseMaxBoneAngle = 0f;
            lastRestPoseChangedBoneSamples = string.Empty;
        }

    }
}
