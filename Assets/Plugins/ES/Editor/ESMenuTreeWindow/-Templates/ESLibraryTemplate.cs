using ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ES.EditorInternal;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace ES
{
    public class ESLibraryWindowMenuTemplate<TConsumer, TLib, TBook, TPage>
    where TConsumer : LibConsumer<TLib>, new()
    where TPage : PageBase, new()
    where TBook : BookBase<TPage>
    where TLib : LibrarySoBase<TBook>

    {
        public Page_Root_Library page_root_Library;
        public Page_Root_Consumer page_root_Consumer;

        public class Page_Root_Library : ESWindowPageBase
        {
            [Title("新建Lib库！", "每个库可以获得专属的资产", bold: true, titleAlignment: TitleAlignments.Centered, Title = "@GetLibTypeName_NewCreate()")]
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string createText = "--创建新的Library库--";

            [InfoBox("请修改一下文件名否则会分配随机数字后缀", VisibleIf = "@!hasChange", InfoMessageType = InfoMessageType.Warning)]
            [ESBackGround("yellow", 0.2f), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), OnValueChanged("OnValueChanged_ChangeHappen")]
            [LabelText("新建库名(展示用)")]
            public string LibName = "新建Library库";
            [ESBackGround("yellow", 0.2f), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), OnValueChanged("OnValueChanged_ChangeHappen")]
            [LabelText("库文件夹名(文件夹用)")]
            public string LibFolderName = IESLibrary.DefaultLibFolderName;
            [TextArea(3, 7)]
            [LabelText("描述")]
            public string LibDESC = "描述：这是一个做啥的库";

            #region  HasChange
            private bool hasChange = false;
            private void OnValueChanged_ChangeHappen()
            {
                hasChange = true;
            }
            #endregion

            [FolderPath]
            [LabelText("保存到文件夹"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public string FolderPath_ = "Assets/Resources/Data";
            public override ESWindowPageBase ES_Refresh()
            {
                LibName = GetLibTypeName_NewCreate();
                FolderPath_ = ESGlobalEditorDefaultConfi.Instance.Path_AllLibraryFolder_;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return base.ES_Refresh();

            }
            [PropertySpace(15)]
            [Button(ButtonHeight = 30, Name = "创建一个库", IconAlignment = IconAlignment.RightEdge), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void CreateNewLibrary()
            {
                string libFolder = FolderPath_ + "/" + LibName;
                if (!AssetDatabase.IsValidFolder(libFolder))
                {
                    AssetDatabase.CreateFolder(FolderPath_, LibName);
                }
                var create = ESDesignUtility.SafeEditor.CreateSOAsset(typeof(TLib), libFolder, LibName, true, hasChange, beforeSave);
                void beforeSave(ScriptableObject so)
                {
                    if (so is TLib lib)
                    {
                        lib.SetSTR(lib.name);
                        lib.LibFolderName = LibFolderName;
                        lib.Desc = LibDESC;
                        lib.Refresh();
                    }
                    else
                    {
                        Debug.LogError("非法文件夹路径或者类型错误！！");
                    }
                }
            }


            #region  命名补充
            private string GetBookTypeName()
            {
                return typeof(TBook)._GetTypeDisplayName();
            }

            private string GetLibTypeName()
            {
                return typeof(TLib)._GetTypeDisplayName();
            }

            private string GetLibTypeName_NewCreate()
            {
                return "新建" + GetLibTypeName();
            }
            #endregion
        }
        /// <summary>
        /// 视图显示模式
        /// </summary>
        public enum ViewMode
        {
            CompactList,        // 紧凑列表
            ThumbnailView       // 缩略图显示
        }

        //Index_库
        public class Page_Index_Library : ESWindowPageBase
        {
            #region 常量定义
            private const float COMPACT_ROW_HEIGHT = 20f;
            private const float GRID_ROW_HEIGHT = 32f;      // 行高降到32px
            private const float THUMBNAIL_SIZE = 24f;       // 缩略图24px，更紧凑
            private const float PREVIEW_THUMBNAIL_SIZE = 128f;  // 详情面板缩略图128px
            private const float SELECTION_BORDER_WIDTH = 2f;
            private static readonly Color SELECTION_BORDER_COLOR = new Color(0.3f, 0.6f, 1f, 1f);
            private const int MAX_THUMBNAIL_CACHE_SIZE = 100;  // 缩略图缓存上限
            #endregion

            #region 字段
            [HideInInspector]
            public TLib library;
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string createText = "--编辑库--";

            private static GUIStyle buttonStyle;
            private static Texture2D buttonBackground;

            // 静态样式缓存，避免频繁修改GUI.skin
            private static GUIStyle _smallLabelStyle;
            private static GUIStyle SmallLabelStyle => _smallLabelStyle ?? (_smallLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 });
            private static GUIStyle _smallButtonStyle;
            private static GUIStyle SmallButtonStyle => _smallButtonStyle ?? (_smallButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 10 });

            // 视图模式
            private static ViewMode currentViewMode = ViewMode.CompactList;

            // 缩略图缓存（带LRU）
            private Dictionary<UnityEngine.Object, Texture2D> thumbnailCache = new Dictionary<UnityEngine.Object, Texture2D>();
            private LinkedList<UnityEngine.Object> thumbnailCacheOrder = new LinkedList<UnityEngine.Object>();

            // 延迟保存
            private bool pendingSave = false;

            // 动态对齐 - 记录选中Book和Page的窗口位置
            private static float selectedBookWindowY = 0f;
            private static float selectedPageWindowY = 0f;
#pragma warning disable CS0414
            private static float bookListScrollY = 0f;
#pragma warning restore CS0414
            private const float ALIGNMENT_THRESHOLD = 150f;  // 超过150px才开始偏移
            private const float MAX_OFFSET = 400f;  // 最大偏移400px

            // 剪切功能的静态存储
            private static TBook cutBook;
            private static TPage cutPage;
            private static TLib cutBookSourceLibrary;
            private static TBook cutPageSourceBook;
            private static TLib cutPageSourceLibrary;
            private static TBook copiedBook;
            private static TPage copiedPage;

            private ReorderableList REForBooks_SelfDefine;
            private ReorderableList REForPages;
            private TBook book;
            private TPage page;

            private ESAreaSolver area = new ESAreaSolver();
            private ESDragAtSolver dragAtForBooks = new ESDragAtSolver();
            private ESDragAtSolver dragAtForPages = new ESDragAtSolver();
            #endregion

            /// <summary>
            /// 重写OnPageDisable，在窗口关闭时执行延迟保存
            /// </summary>
            public override void OnPageDisable()
            {
                base.OnPageDisable();

                string libName = library?.Name ?? "null";
                Debug.Log($"[Page_Index_Library] OnPageDisable调用 - Library: {libName}, pendingSave: {pendingSave}");

                // 窗口关闭时执行延迟保存
                if (pendingSave && library != null)
                {
                    Debug.Log("[Page_Index_Library] 检测到未保存的修改，执行立即保存");
                    SaveAssetsImmediate();
                    Debug.Log("[Page_Index_Library] 保存完成");
                }
                else if (!pendingSave)
                {
                    Debug.Log("[Page_Index_Library] 无待保存的修改，跳过保存");
                }
                else
                {
                    Debug.LogWarning("[Page_Index_Library] Library为null，无法保存");
                }
            }

            #region UI绘制
            [OnInspectorGUI]
            [HorizontalGroup("水平布局")]
            public void DrawSelfAndBooks()
            {
                SirenixEditorGUI.BeginBox();
                var newName = EditorGUILayout.TextField("【库】命名", library.Name);
                if (newName != library.Name)
                {
                    Undo.RecordObject(library, "Rename Library");
                    library.Name = newName;
                    MarkDirtyDeferred();
                }

                var preFolderName = library.LibFolderName;
                library.LibFolderName = EditorGUILayout.TextField("库文件夹名", library.LibFolderName);
                if (preFolderName != library.LibFolderName)
                {
                    Debug.Log("尝试修改库文件夹名");
                    library.Refresh();
                    SaveAssetsImmediate();  // 文件夹改名需立即保存
                }

                if (library is ESAssetLibrary assetLibrary)
                {
                    string previousBundleCode = assetLibrary.AssetBundleCode;
                    EditorGUILayout.BeginHorizontal();
                    string nextBundleCode = EditorGUILayout.TextField("AB 短码", previousBundleCode);
                    if (!string.Equals(nextBundleCode, previousBundleCode, StringComparison.Ordinal))
                    {
                        Undo.RecordObject(assetLibrary, "Edit AssetBundle Code");
                        assetLibrary.AssetBundleCode = nextBundleCode.Trim().ToLowerInvariant();
                        MarkDirtyDeferred();
                    }
                    if (string.IsNullOrWhiteSpace(assetLibrary.AssetBundleCode)
                        && GUILayout.Button("显式生成", GUILayout.Width(72f)))
                    {
                        string libraryGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(assetLibrary));
                        if (string.IsNullOrWhiteSpace(libraryGuid))
                            Debug.LogError("Library 缺少稳定 Asset GUID，无法生成 AB 短码。", assetLibrary);
                        else
                        {
                            Undo.RecordObject(assetLibrary, "Generate AssetBundle Code");
                            assetLibrary.AssetBundleCode = ESAssetBundleUtility.CreateAutomaticLibraryCode(assetLibrary.Name, libraryGuid);
                            MarkDirtyDeferred();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    if (string.IsNullOrWhiteSpace(assetLibrary.AssetBundleCode))
                        EditorGUILayout.HelpBox("正式烘焙不会自动修改 Library；请先显式生成或填写 AB 短码。", MessageType.Warning);
                    else if (!ESAssetBundleUtility.IsValidLibraryCode(assetLibrary.AssetBundleCode))
                        EditorGUILayout.HelpBox("AB 短码必须为 2~12 位，只能包含 a-z、0-9、_。", MessageType.Error);
                    else
                        EditorGUILayout.HelpBox("正式发布后修改此短码会让该 Library 的 BundleKey 全部变化。", MessageType.Warning);
                }

                DrawContentRegistrationTargetPanel();

                EditorGUILayout.LabelField("↓库描述↓");
                var newDesc = EditorGUILayout.TextArea(library.Desc, GUILayout.Height(50));
                if (newDesc != library.Desc)
                {
                    Undo.RecordObject(library, "Edit Library Description");
                    library.Desc = newDesc;
                    MarkDirtyDeferred();
                }

                bookAreaWidth = EditorGUILayout.GetControlRect().width;
                SirenixEditorGUI.EndBox();

                // Books拖拽区域
                area.UpdateAtFisrt();

                // 绘制自定义Books
                REForBooks_SelfDefine.DoLayoutList();

                // 绘制默认Books（合并到同一个竖直列表）
                DrawDefaultBooksInline();

                string dragHint = library is ESAssetLibrary
                    ? "↓ 拖入资产以打开统一内容注册 ↓"
                    : "↓ 拖入资产并按类别分配到 DefaultBook ↓";
                GUILayout.Label(dragHint, EditorStyles.centeredGreyMiniLabel);
                dragAtForBooks.normalColor.a = 0.02f;
                if (dragAtForBooks.Update(out var booksAssets, area.TargetArea, Event.current))
                {
                    if (booksAssets != null && booksAssets.Length > 0)
                    {
                        if (library is ESAssetLibrary targetAssetLibrary)
                            OpenDraggedAssetsInRegistration(booksAssets, targetAssetLibrary);
                        else
                        {
                            Undo.RecordObject(library, "Drag Assets to Library Books");
                            library.EditorOnly_DragAssetsToBooks(booksAssets);
                            SaveAssetsImmediate();
                        }
                    }
                }
                area.UpdateAtLast();
            }

            private void DrawContentRegistrationTargetPanel()
            {
                if (library is not ESAssetLibrary resLibrary)
                {
                    return;
                }

                var config = ESGlobalResToolsSupportConfig.Instance;
                bool isActive = ESGlobalResToolsSupportConfig.ActiveCollectLibrary == resLibrary;

                SirenixEditorGUI.BeginBox();
                SirenixEditorGUI.BeginBoxHeader();
                EditorGUILayout.LabelField("统一内容注册默认目标", EditorStyles.boldLabel);
                SirenixEditorGUI.EndBoxHeader();

                EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
                EditorGUILayout.LabelField("当前 Library", GUILayout.Width(110), GUILayout.Height(22));

                var previousColor = GUI.color;
                GUI.color = isActive ? new Color(0.25f, 1f, 0.45f, 1f) : new Color(1f, 0.45f, 0.3f, 1f);
                GUILayout.Box(isActive ? "是" : "否", GUILayout.Width(42), GUILayout.Height(22));
                GUI.color = previousColor;

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(isActive))
                {
                    if (GUILayout.Button(isActive ? "已是当前" : "设为当前", GUILayout.Width(90), GUILayout.Height(20)))
                    {
                        ESGlobalResToolsSupportConfig.SetActiveCollectLibrary(resLibrary);
                        Debug.Log($"[内容注册] 默认目标 Library 已设置为: {resLibrary.Name}", resLibrary);
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (isActive)
                {
                    SirenixEditorGUI.InfoMessageBox("统一内容注册窗口会把此 Library 作为默认目标；实际写入仍需预检和显式提交。");
                }
                else if (config != null && config.activeCollectLibrary != null)
                {
                    EditorGUILayout.LabelField("当前目标", config.activeCollectLibrary.Name);
                }

                SirenixEditorGUI.EndBox();
            }

            [HorizontalGroup("水平布局")]
            [OnInspectorGUI]
            public void DrawBookAndPages()
            {
                if (book == null) return;

                // 动态对齐：根据选中Book的位置添加顶部空白
                float dynamicOffset = CalculateDynamicOffset(selectedBookWindowY);
                if (dynamicOffset > 0)
                {
                    GUILayout.Space(dynamicOffset);
                }

                // 优化：仅在book变化或REForPages为null时重建
                if (REForPages == null || REForPages.list != book.pages)
                {
                    REForPages = new ReorderableList(book.pages, typeof(TPage))
                    {
                        draggable = library is not ESAssetLibrary,
                        displayAdd = library is not ESAssetLibrary,
                        displayRemove = library is not ESAssetLibrary,
                    };
                    SetupPagesCallBack();
                }

                REForPages.list = book.pages;
                SirenixEditorGUI.BeginBox();
                if (book.WritableDefaultMessageOnEditor)
                {
                    var newName = EditorGUILayout.TextField("【册】命名", book.Name);
                    if (newName != book.Name)
                    {
                        Undo.RecordObject(library, "Rename Book");
                        book.Name = newName;
                        MarkDirtyDeferred();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("【册】命名", book.Name);
                }
                EditorGUILayout.LabelField("↓册描述↓");
                if (book.WritableDefaultMessageOnEditor)
                {
                    var newDesc = EditorGUILayout.TextArea(book.Desc, GUILayout.Height(50));
                    if (newDesc != book.Desc)
                    {
                        Undo.RecordObject(library, "Edit Book Description");
                        book.Desc = newDesc;
                        MarkDirtyDeferred();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(book.Desc);
                }
                SirenixEditorGUI.EndBox();
                area.UpdateAtFisrt();
                REForPages.DoLayoutList();
                dragAtForPages.normalColor.a = 0.02f;
                if (dragAtForPages.Update(out var gs, area.TargetArea, Event.current))
                {
                    if (gs != null)
                    {
                        if (library is ESAssetLibrary targetAssetLibrary)
                            OpenDraggedAssetsInRegistration(gs, targetAssetLibrary);
                        else
                        {
                            Undo.RecordObject(library, "Drag Assets to Book");
                            book.EditorOnly_DragAtArea(gs);
                            SaveAssetsImmediate();
                        }
                    }
                }
                area.UpdateAtLast();
            }
            [OnInspectorGUI]
            [HorizontalGroup("水平布局")]
            public void DrawPage()
            {
                // Debug: 检查DrawPage是否被调用
              //  Debug.Log($"[DrawPage] Called - book: {book?.Name ?? "null"}, page: {page?.Name ?? "null"}");

                if (book == null || page == null || !book.pages.Contains(page))
                {
                  //  Debug.Log($"[DrawPage] Early return - book null: {book == null}, page null: {page == null}, contains: {book?.pages.Contains(page) ?? false}");
                    return;
                }
                // 动态对齐：根据选中Book的位置添加顶部空白
                float dynamicOffset = CalculateDynamicOffset(selectedBookWindowY);
                if (dynamicOffset > 0)
                {
                    GUILayout.Space(dynamicOffset);
                }
                // 动态对齐：根据选中Page的位置添加顶部空白
                //  float dynamicOffset = CalculateDynamicOffset(selectedPageWindowY);

                SirenixEditorGUI.BeginBox();


                var newName = EditorGUILayout.TextField("Page命名", page.Name);
                if (newName != page.Name)
                {
                    Undo.RecordObject(library, "Rename Page");
                    page.Name = newName;
                    MarkDirtyDeferred();
                }
                SirenixEditorGUI.EndBox();

                if (page.Draw())
                {
                    MarkDirtyDeferred();
                }

                // 在Draw之后显示缩略图预览
                if (page is ESAssetPage resPage && resPage.OB != null)
                {
                    EditorGUILayout.Space(10);
                    SirenixEditorGUI.BeginBox();
                    EditorGUILayout.LabelField("资源预览", EditorStyles.boldLabel);

                    // 使用缓存获取缩略图
                    var thumbnail = GetThumbnailFromCache(resPage.OB);
                    if (thumbnail != null)
                    {
                        var rect = GUILayoutUtility.GetRect(PREVIEW_THUMBNAIL_SIZE, PREVIEW_THUMBNAIL_SIZE, GUILayout.ExpandWidth(false));
                        GUI.DrawTexture(rect, thumbnail, ScaleMode.ScaleToFit);
                    }
                    else
                    {
                        // 显示默认图标
                        var icon = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(resPage.OB));
                        if (icon != null)
                        {
                            var rect = GUILayoutUtility.GetRect(PREVIEW_THUMBNAIL_SIZE, PREVIEW_THUMBNAIL_SIZE, GUILayout.ExpandWidth(false));
                            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
                        }
                    }

                    SirenixEditorGUI.EndBox();
                }
            }

            /// <summary>
            /// 内联绘制默认Books（不使用独立的HorizontalGroup）
            /// </summary>
            private void DrawDefaultBooksInline()
            {
                if (library.DefaultBooks == null || library.DefaultBooks.Count() == 0)
                {
                    return;
                }

                // 绘制分隔线和标题
                GUILayout.Space(5);
                string defaultBooksTitle = library is ESAssetLibrary
                    ? "默认 Books【统一内容注册分类，不可删改】"
                    : "默认 Books【按类别分配，不可删改】";
                EditorGUILayout.LabelField(defaultBooksTitle, EditorStyles.boldLabel);

                foreach (var b in library.DefaultBooks)
                {
                    if (b == null)
                    {
                        continue;
                    }
                    if (buttonStyle == null)
                    {
                        buttonStyle = new GUIStyle(GUI.skin.button);
                        buttonStyle.alignment = TextAnchor.MiddleLeft;
                        buttonStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f); // 稍暗的浅灰色
                        if (buttonBackground == null)
                        {
                            buttonBackground = new Texture2D(1, 1);
                            buttonBackground.hideFlags = HideFlags.HideAndDontSave;
                            buttonBackground.SetPixel(0, 0, Color.black);
                            buttonBackground.Apply();
                        }
                        buttonStyle.normal.background = buttonBackground;
                    }
                    var color = book == b ? Color.yellow : GetColorFromLabel(b.ColorTag);
                    GUIHelper.PushColor(color);
                    // Debug.Log($"绘制默认Book按钮：{b.Name}{b.pages.Count}{bookAreaWidth}");

                    var buttonRect = EditorGUILayout.GetControlRect(GUILayout.Height(20));

                    // 获取图标内容
                    GUIContent buttonContent;
                    if (b.CustomIcon != null)
                    {
                        buttonContent = new GUIContent($"- 【{b.Name}】 ({b.pages.Count} 页)", b.CustomIcon);
                    }
                    else
                    {
                        buttonContent = EditorIconSupport.CreateContent($"- 【{b.Name}】 ({b.pages.Count} 页)", b.Icon);
                    }

                    // 添加警告前缀（如果Book为空）
                    if (b.pages == null || b.pages.Count == 0)
                    {
                        buttonContent.text = "⚠ " + buttonContent.text;
                    }

                    // 先处理右键菜单，避免与Button冲突
                    bool isRightClick = Event.current.type == EventType.MouseDown && Event.current.button == 1 && buttonRect.Contains(Event.current.mousePosition);

                    if (isRightClick)
                    {
                        GenericMenu menu = new GenericMenu();

                        // 默认Book不能被剪切，但可以接受粘贴
                        menu.AddDisabledItem(new GUIContent("剪切（默认Book不可剪切）"));

                        menu.AddItem(new GUIContent("复制Book"), false, () =>
                        {
                            CopyBookToClipboard(b);
                        });

                        if (cutBook != null)
                        {
                            menu.AddItem(new GUIContent("粘贴Book到Library此位置"), false, () =>
                            {
                                PasteBookToLibrary(library, library.Books.Count);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴Book到Library此位置"));
                        }

                        // 添加"全部Pages移动到"子菜单
                        if (copiedBook != null)
                        {
                            menu.AddItem(new GUIContent("粘贴复制Book到Library末尾"), false, () =>
                            {
                                PasteCopiedBookToLibrary(library, library.Books.Count);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴复制Book到Library末尾"));
                        }

                        var allTargetBooks = new List<TBook>();
                        // 收集自定义Books
                        if (library.Books != null)
                        {
                            foreach (var targetBook in library.Books)
                            {
                                if (targetBook != null && targetBook != b)
                                {
                                    allTargetBooks.Add(targetBook);
                                }
                            }
                        }
                        // 收集其他默认Books
                        if (library.DefaultBooks != null)
                        {
                            foreach (var targetBook in library.DefaultBooks)
                            {
                                if (targetBook != null && targetBook != b)
                                {
                                    allTargetBooks.Add(targetBook);
                                }
                            }
                        }

                        if (allTargetBooks.Count > 0 && b.pages != null && b.pages.Count > 0)
                        {
                            for (int i = 0; i < allTargetBooks.Count; i++)
                            {
                                var targetBook = allTargetBooks[i];
                                menu.AddItem(new GUIContent($"全部Pages移动到/{i + 1}. {targetBook.Name}"), false, () =>
                                {
                                    MoveAllPagesToBook(b, targetBook);
                                });
                            }
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("全部Pages移动到/（无可用目标或无Pages）"));
                        }

                        menu.ShowAsContext();
                        Event.current.Use();
                    }
                    else if (GUI.Button(buttonRect, buttonContent, buttonStyle))
                    {
                        book = b;
                        // 记录选中Book的窗口位置
                        selectedBookWindowY = buttonRect.y;
                    }

                    GUIHelper.PopColor();
                }
            }

            public override ESWindowPageBase ES_Refresh()
            {
                createText = $"--编辑库【{library.GetSTR()}】--";
                REForBooks_SelfDefine = new ReorderableList(library.Books, typeof(TBook))
                {
                    draggable = library is not ESAssetLibrary,
                    displayAdd = library is not ESAssetLibrary,
                    displayRemove = library is not ESAssetLibrary,
                };
                SetupBooksCallBack();
                return base.ES_Refresh();
            }
            private static float bookAreaWidth = 250f;
            private void SetupBooksCallBack()
            {
                //Debug.Log("重建");
                REForBooks_SelfDefine.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "包含自定义Books");

                    // 标题右键菜单
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
                    {
                        GenericMenu menu = new GenericMenu();
                        if (cutBook != null)
                        {
                            menu.AddItem(new GUIContent("粘贴到末尾"), false, () =>
                            {
                                PasteBookToLibrary(library, library.Books.Count);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴到末尾"));
                        }
                        if (copiedBook != null)
                        {
                            menu.AddItem(new GUIContent("粘贴复制Book到末尾"), false, () =>
                            {
                                PasteCopiedBookToLibrary(library, library.Books.Count);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴复制Book到末尾"));
                        }
                        menu.ShowAsContext();
                        Event.current.Use();
                    }
                };

                REForBooks_SelfDefine.onChangedCallback += (ReorderableList list) =>
                {
                    Undo.RecordObject(library, "Reorder Books");
                    MarkDirtyDeferred();  // 使用延迟保存
                };

                REForBooks_SelfDefine.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var book_ = library.Books[index];
                    var color = book == book_ ? Color.yellow : Color.white;
                    if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                    {
                        book = library.Books[index];

                        // 记录选中Book的窗口位置（用于动态对齐）
                        selectedBookWindowY = rect.y;

                        // 右键菜单
                        if (Event.current.button == 1)
                        {
                            GenericMenu menu = new GenericMenu();
                            menu.AddItem(new GUIContent("剪切"), false, () =>
                            {
                                cutBook = book_;
                                cutBookSourceLibrary = library;
                                cutPage = null;
                                cutPageSourceBook = null;
                            });
                            menu.AddItem(new GUIContent("复制"), false, () =>
                            {
                                CopyBookToClipboard(book_);
                            });

                            if (cutBook != null)
                            {
                                menu.AddItem(new GUIContent("粘贴到此处"), false, () =>
                                {
                                    PasteBookToLibrary(library, index);
                                });
                            }
                            else
                            {
                                menu.AddDisabledItem(new GUIContent("粘贴到此处"));
                            }

                            // 添加"全部Pages移动到"子菜单
                            if (copiedBook != null)
                            {
                                menu.AddItem(new GUIContent("粘贴复制到此处"), false, () =>
                                {
                                    PasteCopiedBookToLibrary(library, index);
                                });
                            }
                            else
                            {
                                menu.AddDisabledItem(new GUIContent("粘贴复制到此处"));
                            }

                            var allTargetBooks = new List<TBook>();
                            // 收集自定义Books
                            if (library.Books != null)
                            {
                                foreach (var b in library.Books)
                                {
                                    if (b != null && b != book_)
                                    {
                                        allTargetBooks.Add(b);
                                    }
                                }
                            }
                            // 收集默认Books
                            if (library.DefaultBooks != null)
                            {
                                foreach (var b in library.DefaultBooks)
                                {
                                    if (b != null && b != book_)
                                    {
                                        allTargetBooks.Add(b);
                                    }
                                }
                            }

                            if (allTargetBooks.Count > 0 && book_.pages != null && book_.pages.Count > 0)
                            {
                                for (int i = 0; i < allTargetBooks.Count; i++)
                                {
                                    var targetBook = allTargetBooks[i];
                                    menu.AddItem(new GUIContent($"全部Pages移动到/{i + 1}. {targetBook.Name}"), false, () =>
                                    {
                                        MoveAllPagesToBook(book_, targetBook);
                                    });
                                }
                            }
                            else
                            {
                                menu.AddDisabledItem(new GUIContent("全部Pages移动到/（无可用目标）"));
                            }

                            menu.AddSeparator("");

                            // 颜色标记子菜单
                            AddColorTagMenu(menu, "设置颜色标签", (colorTag) =>
                            {
                                Undo.RecordObject(library, "Set Book Color");
                                book_.ColorTag = colorTag;
                                MarkDirtyDeferred();
                            });

                            // 自定义图标
                            menu.AddItem(new GUIContent("自定义图标"), false, () =>
                            {
                                ShowCustomIconPicker(book_);
                            });

                            menu.ShowAsContext();
                            Event.current.Use();
                        }
                    }

                    // 应用颜色标记
                    var displayColor = book == book_ ? Color.yellow : GetColorFromLabel(book_.ColorTag);

                    // 绘制选中边框
                    if (book == book_)
                    {
                        DrawSelectionBorder(rect);
                    }

                    GUIHelper.PushColor(displayColor);

                    // 显示警告图标（如果Book为空）
                    var bookContent = book_.Name;
                    if (book_.pages == null || book_.pages.Count == 0)
                    {
                        bookContent = "⚠ " + bookContent;
                    }

                    EditorGUI.LabelField(rect, bookContent);
                    GUIHelper.PopColor();
                };


            }
            private void SetupPagesCallBack()
            {
                // 动态设置行高
                REForPages.elementHeight = currentViewMode == ViewMode.ThumbnailView ? GRID_ROW_HEIGHT : COMPACT_ROW_HEIGHT;

                REForPages.drawHeaderCallback = (Rect rect) =>
                {
                    var labelRect = new Rect(rect.x, rect.y, rect.width - 120, rect.height);

                    // 使用静态样式避免频繁修改GUI.skin
                    EditorGUI.LabelField(labelRect, "包含Page", SmallLabelStyle);

                    // 视图模式切换按钮
                    var buttonRect = new Rect(rect.x + rect.width - 115, rect.y, 55, rect.height - 2);
                    if (GUI.Button(buttonRect, currentViewMode == ViewMode.CompactList ? "缩略图" : "列表", SmallButtonStyle))
                    {
                        currentViewMode = currentViewMode == ViewMode.CompactList ? ViewMode.ThumbnailView : ViewMode.CompactList;
                        REForPages.elementHeight = currentViewMode == ViewMode.ThumbnailView ? GRID_ROW_HEIGHT : COMPACT_ROW_HEIGHT;
                    }

                    // 检测重复按钮
                    var detectButtonRect = new Rect(rect.x + rect.width - 55, rect.y, 55, rect.height - 2);
                    if (GUI.Button(detectButtonRect, "检测重复", SmallButtonStyle))
                    {
                        DetectAllDuplicates(library);
                    }

                    // 标题右键菜单
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
                    {
                        GenericMenu menu = new GenericMenu();
                        if (cutPage != null && book != null)
                        {
                            menu.AddItem(new GUIContent("粘贴到末尾"), false, () =>
                            {
                                PastePageToBook(book, book.pages.Count);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴到末尾"));
                        }

                        if (copiedPage != null && book != null)
                        {
                            menu.AddItem(new GUIContent("粘贴复制Page到末尾"), false, () =>
                            {
                                PasteCopiedPageToBook(book, book.pages.Count);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴复制Page到末尾"));
                        }

                        menu.AddSeparator("");

                        // 批量操作
                        menu.AddItem(new GUIContent("清除所有空Pages"), false, () =>
                        {
                            RemoveEmptyPages(book);
                        });

                        menu.ShowAsContext();
                        Event.current.Use();
                    }
                };

                REForPages.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (book == null) return;
                    var page_ = book.pages[index];

                    // 应用颜色标记
                    var color = isActive ? Color.yellow : GetColorFromLabel(page_.ColorTag);
                    if (isActive)
                    {
                        page = book.pages[index];
                        // 记录选中Page的窗口位置（用于动态对齐）
                        selectedPageWindowY = rect.y;
                    }

                    // 获取Page关联的资源对象
                    UnityEngine.Object pageAsset = null;
                    if (page_ is ESAssetPage resPage)
                    {
                        pageAsset = resPage.OB;
                    }

                    // 右键菜单
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
                    {
                        GenericMenu menu = new GenericMenu();

                        // 基础操作
                        menu.AddItem(new GUIContent("剪切"), false, () =>
                        {
                            cutPage = page_;
                            cutPageSourceBook = book;
                            cutPageSourceLibrary = library;
                            cutBook = null;
                            cutBookSourceLibrary = null;
                        });
                        menu.AddItem(new GUIContent("复制"), false, () =>
                        {
                            CopyPageToClipboard(page_);
                        });

                        if (cutPage != null)
                        {
                            menu.AddItem(new GUIContent("粘贴到此处"), false, () =>
                            {
                                PastePageToBook(book, index);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴到此处"));
                        }

                        menu.AddSeparator("");

                        // 在Project中定位
                        if (copiedPage != null)
                        {
                            menu.AddItem(new GUIContent("粘贴复制Page到此处"), false, () =>
                            {
                                PasteCopiedPageToBook(book, index);
                            });
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("粘贴复制Page到此处"));
                        }

                        if (pageAsset != null)
                        {
                            menu.AddItem(new GUIContent("在Project中定位"), false, () =>
                            {
                                EditorGUIUtility.PingObject(pageAsset);
                                Selection.activeObject = pageAsset;
                            });

                            // 显示引用此资源的所有Pages
                            menu.AddItem(new GUIContent("显示所有引用"), false, () =>
                            {
                                ShowAssetReferences(pageAsset, library);
                            });

                            // 检测重复资源
                            var duplicates = FindDuplicatePages(pageAsset, library);
                            if (duplicates.Count > 1)
                            {
                                menu.AddItem(new GUIContent($"检测到{duplicates.Count}个重复引用"), false, () =>
                                {
                                    ShowDuplicateDialog(pageAsset, duplicates, library);
                                });
                            }
                            else
                            {
                                menu.AddDisabledItem(new GUIContent("无重复引用"));
                            }
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("在Project中定位（无资源）"));
                            menu.AddDisabledItem(new GUIContent("显示所有引用（无资源）"));
                        }

                        menu.AddSeparator("");

                        // 颜色标记子菜单
                        AddColorTagMenu(menu, "设置颜色标签", (colorTag) =>
                        {
                            Undo.RecordObject(library, "Set Page Color");
                            page_.ColorTag = colorTag;
                            MarkDirtyDeferred();
                        });

                        menu.AddSeparator("");

                        // 添加"移动到"子菜单
                        var allTargetBooks = new List<TBook>();
                        // 收集自定义Books
                        if (library.Books != null)
                        {
                            foreach (var targetBook in library.Books)
                            {
                                if (targetBook != null && targetBook != book)
                                {
                                    allTargetBooks.Add(targetBook);
                                }
                            }
                        }
                        // 收集默认Books
                        if (library.DefaultBooks != null)
                        {
                            foreach (var targetBook in library.DefaultBooks)
                            {
                                if (targetBook != null && targetBook != book)
                                {
                                    allTargetBooks.Add(targetBook);
                                }
                            }
                        }

                        if (allTargetBooks.Count > 0)
                        {
                            for (int i = 0; i < allTargetBooks.Count; i++)
                            {
                                var targetBook = allTargetBooks[i];
                                menu.AddItem(new GUIContent($"移动到/{i + 1}. {targetBook.Name}"), false, () =>
                                {
                                    MovePageToBook(page_, book, targetBook);
                                });
                            }
                        }
                        else
                        {
                            menu.AddDisabledItem(new GUIContent("移动到/（无可用目标）"));
                        }

                        menu.ShowAsContext();
                        Event.current.Use();
                    }

                    // 绘制带颜色标记、警告图标和选中边框的列表项

                    // 1. 绘制选中边框
                    if (isActive)
                    {
                        DrawSelectionBorder(rect);
                    }

                    GUIHelper.PushColor(color);

                    // 2. 显示警告图标（如果Page为空）
                    if (pageAsset == null)
                    {
                        var iconRect = new Rect(rect.x + 2, rect.y + 2, 16, 16);
                        GUI.Label(iconRect, EditorGUIUtility.IconContent("console.warnicon"));
                        rect.x += 18;
                        rect.width -= 18;
                    }

                    // 3. 根据视图模式绘制
                    if (currentViewMode == ViewMode.ThumbnailView && pageAsset != null)
                    {
                        DrawPageInGridMode(rect, page_, pageAsset);
                    }
                    else
                    {
                        EditorGUI.LabelField(rect, page_.Name);
                    }

                    GUIHelper.PopColor();
                };

                REForPages.onChangedCallback += (ReorderableList list) =>
                {
                    Undo.RecordObject(library, "Reorder Pages");
                    MarkDirtyDeferred();  // 使用延迟保存
                };
            }

            #region Book/Page复制粘贴

            private void CopyBookToClipboard(TBook sourceBook)
            {
                copiedBook = CloneBookForPaste(sourceBook);
                if (copiedBook == null)
                {
                    Debug.LogWarning("[CopyBook] 当前Book类型暂不支持复制。");
                    return;
                }

                cutBook = null;
                cutBookSourceLibrary = null;
            }

            private void CopyPageToClipboard(TPage sourcePage)
            {
                copiedPage = ClonePageForPaste(sourcePage);
                if (copiedPage == null)
                {
                    Debug.LogWarning("[CopyPage] 当前Page类型暂不支持复制。");
                    return;
                }

                cutPage = null;
                cutPageSourceBook = null;
                cutPageSourceLibrary = null;
            }

            private void PasteCopiedBookToLibrary(TLib targetLibrary, int insertIndex)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (copiedBook == null || targetLibrary?.Books == null)
                {
                    Debug.LogWarning("[PasteCopiedBook] 复制板或目标Library为空。");
                    return;
                }

                var clonedBook = CloneBookForPaste(copiedBook);
                if (clonedBook == null)
                {
                    Debug.LogWarning("[PasteCopiedBook] 当前Book类型暂不支持粘贴复制。");
                    return;
                }

                Undo.RecordObject(targetLibrary, "Paste Copied Book");
                insertIndex = Mathf.Clamp(insertIndex, 0, targetLibrary.Books.Count);
                targetLibrary.Books.Insert(insertIndex, clonedBook);
                SaveAssetsImmediate();
            }

            private void PasteCopiedPageToBook(TBook targetBook, int insertIndex)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (copiedPage == null || targetBook?.pages == null)
                {
                    Debug.LogWarning("[PasteCopiedPage] 复制板或目标Book为空。");
                    return;
                }

                var clonedPage = ClonePageForPaste(copiedPage);
                if (clonedPage == null)
                {
                    Debug.LogWarning("[PasteCopiedPage] 当前Page类型暂不支持粘贴复制。");
                    return;
                }

                Undo.RecordObject(library, "Paste Copied Page");
                insertIndex = Mathf.Clamp(insertIndex, 0, targetBook.pages.Count);
                targetBook.pages.Insert(insertIndex, clonedPage);
                SaveAssetsImmediate();
            }

            private static TBook CloneBookForPaste(TBook sourceBook)
            {
                if (sourceBook == null)
                {
                    return null;
                }

                if (sourceBook is ESAssetBook sourceESAssetBook)
                {
                    var cloned = new ESAssetBook
                    {
                        Name = sourceESAssetBook.Name,
                        WritableDefaultMessageOnEditor = sourceESAssetBook.WritableDefaultMessageOnEditor,
                        PreferredAssetCategory = sourceESAssetBook.PreferredAssetCategory,
                        CustomIcon = sourceESAssetBook.CustomIcon,
                        ColorTag = sourceESAssetBook.ColorTag,
                        Desc = sourceESAssetBook.Desc,
                        pages = new List<ESAssetPage>()
                    };

                    if (sourceESAssetBook.pages != null)
                    {
                        for (int i = 0; i < sourceESAssetBook.pages.Count; i++)
                        {
                            var sourcePage = sourceESAssetBook.pages[i];
                            cloned.pages.Add(sourcePage != null ? sourcePage.CloneForPaste() : null);
                        }
                    }

                    return cloned as TBook;
                }

                return null;
            }

            private static TPage ClonePageForPaste(TPage sourcePage)
            {
                if (sourcePage == null)
                {
                    return null;
                }

                if (sourcePage is ESAssetPage sourceESAssetPage)
                {
                    return sourceESAssetPage.CloneForPaste() as TPage;
                }

                return new TPage
                {
                    Name = sourcePage.Name,
                    ColorTag = sourcePage.ColorTag
                };
            }

            #endregion

            // 封装Book粘贴逻辑
            private void PasteBookToLibrary(TLib targetLibrary, int insertIndex)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (cutBook == null || cutBookSourceLibrary == null || targetLibrary?.Books == null)
                {
                    Debug.LogWarning("[PasteBook] 无效的粘贴操作：剪切板或目标为空");
                    return;
                }

                Undo.RecordObject(targetLibrary, "Paste Book");
                if (cutBookSourceLibrary != targetLibrary)
                {
                    Undo.RecordObject(cutBookSourceLibrary, "Paste Book");
                }

                // 从源Library移除
                cutBookSourceLibrary.Books?.Remove(cutBook);
                // 插入到目标位置
                targetLibrary.Books.Insert(insertIndex, cutBook);

                // 清空剪切板
                cutBook = null;
                cutBookSourceLibrary = null;

                SaveAssetsImmediate();  // 跨Library操作需立即保存
            }

            // 封装Page粘贴逻辑
            private void PastePageToBook(TBook targetBook, int insertIndex)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (cutPage == null || cutPageSourceBook == null || targetBook?.pages == null)
                {
                    Debug.LogWarning("[PastePage] 无效的粘贴操作：剪切板或目标为空");
                    return;
                }

                Undo.RecordObject(library, "Paste Page");
                if (cutPageSourceLibrary != null && cutPageSourceLibrary != library)
                {
                    Undo.RecordObject(cutPageSourceLibrary, "Paste Page");
                }

                // 从源Book移除
                cutPageSourceBook.pages?.Remove(cutPage);
                // 插入到目标位置
                targetBook.pages.Insert(insertIndex, cutPage);

                // 清空剪切板
                cutPage = null;
                cutPageSourceBook = null;
                cutPageSourceLibrary = null;

                SaveAssetsImmediate();  // 跨Book操作需立即保存
            }

            // 将Book的所有Pages移动到目标Book
            private void MoveAllPagesToBook(TBook sourceBook, TBook targetBook)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (sourceBook?.pages == null || targetBook?.pages == null)
                {
                    Debug.LogWarning("[MoveAllPages] 无效的移动操作：源或目标为空");
                    return;
                }

                if (sourceBook.pages.Count == 0)
                {
                    Debug.Log("[MoveAllPages] 源Book为空，无需移动");
                    return;
                }

                Undo.RecordObject(library, "Move All Pages");

                // 复制所有Pages到目标Book
                var pagesToMove = new List<TPage>(sourceBook.pages);
                foreach (var page in pagesToMove)
                {
                    if (page != null)
                    {
                        targetBook.pages.Add(page);
                    }
                }

                // 清空源Book的Pages
                sourceBook.pages.Clear();

                SaveAssetsImmediate();  // 批量移动需立即保存
                Debug.Log($"已将 {pagesToMove.Count} 个Pages从 [{sourceBook.Name}] 移动到 [{targetBook.Name}]");
            }

            // 将单个Page移动到目标Book
            private void MovePageToBook(TPage page, TBook sourceBook, TBook targetBook)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (page == null || sourceBook?.pages == null || targetBook?.pages == null)
                {
                    Debug.LogWarning("[MovePage] 无效的移动操作：页面或Book为空");
                    return;
                }

                Undo.RecordObject(library, "Move Page");

                // 从源Book移除
                sourceBook.pages.Remove(page);
                // 添加到目标Book
                targetBook.pages.Add(page);

                MarkDirtyDeferred();  // 单个Page移动使用延迟保存
                Debug.Log($"已将Page [{page.Name}] 从 [{sourceBook.Name}] 移动到 [{targetBook.Name}]");
            }
            #endregion

            #region 延迟保存和缓存管理

            /// <summary>
            /// 标记为脏数据，延迟保存
            /// </summary>
            private void MarkDirtyDeferred()
            {
                if (library != null)
                {
                    EditorUtility.SetDirty(library);
                    pendingSave = true;
                    Debug.Log($"[Page_Index_Library] MarkDirtyDeferred - 标记为待保存状态，Library: {library.Name}");
                }
            }

            /// <summary>
            /// 立即保存（用于关键操作）
            /// </summary>
            private void SaveAssetsImmediate()
            {
                string libName = library?.Name ?? "null";
                Debug.Log($"[Page_Index_Library] SaveAssetsImmediate - 执行立即保存，Library: {libName}");
                if (library != null)
                {
                    EditorUtility.SetDirty(library);
                }
                AssetDatabase.SaveAssets();
                pendingSave = false;
                Debug.Log("[Page_Index_Library] SaveAssetsImmediate - 保存完成，pendingSave已重置为false");
            }

            /// <summary>
            /// 带LRU缓存的缩略图获取
            /// </summary>
            private Texture2D GetThumbnailFromCache(UnityEngine.Object asset)
            {
                if (asset == null) return null;

                // 检查缓存
                if (thumbnailCache.TryGetValue(asset, out var cachedThumbnail) && cachedThumbnail != null)
                {
                    // 更新LRU顺序
                    thumbnailCacheOrder.Remove(asset);
                    thumbnailCacheOrder.AddLast(asset);
                    return cachedThumbnail;
                }

                // 获取新缩略图
                var thumbnail = AssetPreview.GetAssetPreview(asset);
                if (thumbnail != null)
                {
                    // 检查缓存上限
                    if (thumbnailCache.Count >= MAX_THUMBNAIL_CACHE_SIZE)
                    {
                        // 移除最久未使用的
                        var oldest = thumbnailCacheOrder.First?.Value;
                        if (oldest != null)
                        {
                            thumbnailCache.Remove(oldest);
                            thumbnailCacheOrder.RemoveFirst();
                        }
                    }

                    thumbnailCache[asset] = thumbnail;
                    thumbnailCacheOrder.AddLast(asset);
                }

                return thumbnail;
            }

            #endregion

            #region 动态对齐

            /// <summary>
            /// 计算动态偏移量：当选中Book在下方时，右侧面板向上偏移
            /// </summary>
            private float CalculateDynamicOffset(float bookY)
            {
                // 如果Book在阈值以上，开始计算偏移
                if (bookY > ALIGNMENT_THRESHOLD)
                {
                    // 线性插值，但限制最大值
                    float offset = Mathf.Min(bookY - ALIGNMENT_THRESHOLD, MAX_OFFSET);
                    return offset;
                }
                return 0f;
            }

            #endregion

            #region 颜色和样式

            /// <summary>
            /// 根据颜色标签获取颜色
            /// </summary>
            private static Color GetColorFromLabel(ColorLabel label)
            {
                switch (label)
                {
                    case ColorLabel.Red: return new Color(1f, 0.3f, 0.3f);
                    case ColorLabel.Orange: return new Color(1f, 0.6f, 0.2f);
                    case ColorLabel.Yellow: return new Color(1f, 0.9f, 0.3f);
                    case ColorLabel.Green: return new Color(0.3f, 0.9f, 0.3f);
                    case ColorLabel.Blue: return new Color(0.3f, 0.6f, 1f);
                    case ColorLabel.Purple: return new Color(0.7f, 0.3f, 1f);
                    case ColorLabel.Pink: return new Color(1f, 0.3f, 0.7f);
                    case ColorLabel.Gray: return new Color(0.6f, 0.6f, 0.6f);
                    default: return Color.white;
                }
            }

            /// <summary>
            /// 添加颜色标签菜单
            /// </summary>
            private static void AddColorTagMenu(GenericMenu menu, string menuPath, System.Action<ColorLabel> onSelected)
            {
                menu.AddItem(new GUIContent($"{menuPath}/无颜色"), false, () => onSelected(ColorLabel.None));
                menu.AddItem(new GUIContent($"{menuPath}/红色"), false, () => onSelected(ColorLabel.Red));
                menu.AddItem(new GUIContent($"{menuPath}/橙色"), false, () => onSelected(ColorLabel.Orange));
                menu.AddItem(new GUIContent($"{menuPath}/黄色"), false, () => onSelected(ColorLabel.Yellow));
                menu.AddItem(new GUIContent($"{menuPath}/绿色"), false, () => onSelected(ColorLabel.Green));
                menu.AddItem(new GUIContent($"{menuPath}/蓝色"), false, () => onSelected(ColorLabel.Blue));
                menu.AddItem(new GUIContent($"{menuPath}/紫色"), false, () => onSelected(ColorLabel.Purple));
                menu.AddItem(new GUIContent($"{menuPath}/粉色"), false, () => onSelected(ColorLabel.Pink));
                menu.AddItem(new GUIContent($"{menuPath}/灰色"), false, () => onSelected(ColorLabel.Gray));
            }

            #endregion

            #region 资源引用追踪

            /// <summary>
            /// 显示资源引用
            /// </summary>
            private void ShowAssetReferences(UnityEngine.Object asset, TLib lib)
            {
                var assetPath = AssetDatabase.GetAssetPath(asset);
                var references = new List<string>();

                // 统一遍历所有Books（包含自定义和默认）
                foreach (var book in lib.GetAllUseableBooks())
                {
                    if (book?.pages == null) continue;
                    foreach (var page in book.pages)
                    {
                        if (page is ESAssetPage resPage && resPage.OB != null)
                        {
                            var pagePath = AssetDatabase.GetAssetPath(resPage.OB);
                            if (pagePath == assetPath)
                            {
                                references.Add($"Book: {book.Name} > Page: {page.Name}");
                            }
                        }
                    }
                }

                var message = references.Count > 0
                    ? $"资源 '{asset.name}' 被以下 {references.Count} 个位置引用：\n\n" + string.Join("\n", references)
                    : $"资源 '{asset.name}' 没有被任何Page引用。";

                EditorUtility.DisplayDialog("资源引用追踪", message, "确定");
            }

            /// <summary>
            /// 查找重复的Pages
            /// </summary>
            private List<(TBook book, TPage page)> FindDuplicatePages(UnityEngine.Object asset, TLib lib)
            {
                var duplicates = new List<(TBook, TPage)>();
                var assetPath = AssetDatabase.GetAssetPath(asset);

                // 统一遍历所有Books（包含自定义和默认）
                foreach (var book in lib.GetAllUseableBooks())
                {
                    if (book?.pages == null) continue;
                    foreach (var page in book.pages)
                    {
                        if (page is ESAssetPage resPage && resPage.OB != null)
                        {
                            var pagePath = AssetDatabase.GetAssetPath(resPage.OB);
                            if (pagePath == assetPath)
                            {
                                duplicates.Add((book, page));
                            }
                        }
                    }
                }

                return duplicates;
            }

            /// <summary>
            /// 显示重复资源对话框
            /// </summary>
            private void ShowDuplicateDialog(UnityEngine.Object asset, List<(TBook book, TPage page)> duplicates, TLib lib)
            {
                var locations = duplicates.Select(d => $"  • {d.book.Name} > {d.page.Name}").ToArray();
                var message = $"资源 '{asset.name}' 在以下 {duplicates.Count} 个位置重复：\n\n" + string.Join("\n", locations) + "\n\n是否合并为一个Page？";

                if (EditorUtility.DisplayDialog("检测到重复资源", message, "合并", "取消"))
                {
                    MergeDuplicatePages(duplicates, lib);
                }
            }

            /// <summary>
            /// 合并重复的Pages
            /// </summary>
            private void MergeDuplicatePages(List<(TBook book, TPage page)> duplicates, TLib lib)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (duplicates.Count <= 1) return;

                // 保留第一个，删除其他
                var keepPage = duplicates[0];
                for (int i = 1; i < duplicates.Count; i++)
                {
                    var (book, page) = duplicates[i];
                    book.pages.Remove(page);
                }

                EditorUtility.SetDirty(lib);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("合并完成", $"已合并 {duplicates.Count - 1} 个重复Page，保留在 {keepPage.book.Name}", "确定");
            }

            #endregion

            #region 资源管理功能

            /// <summary>
            /// 检测所有重复资源（全项目范围）
            /// </summary>
            private void DetectAllDuplicates(TLib currentLib)
            {
                var assetToPages = new Dictionary<string, List<(string libName, string bookName, string pageName)>>();

                // 获取所有同类型Library
                var allLibraries = ESEditorSO.GetGroupOfType<TLib>();

                // 遍历所有Library收集资源引用
                if (allLibraries != null)
                {
                    foreach (var lib in allLibraries)
                    {
                        if (lib == null) continue;

                        // 统一遍历所有Books（包含自定义和默认）
                        foreach (var book in lib.GetAllUseableBooks())
                        {
                            if (book?.pages == null) continue;
                            foreach (var page in book.pages)
                            {
                                if (page is ESAssetPage resPage && resPage.OB != null)
                                {
                                    var path = AssetDatabase.GetAssetPath(resPage.OB);
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        if (!assetToPages.ContainsKey(path))
                                        {
                                            assetToPages[path] = new List<(string, string, string)>();
                                        }
                                        assetToPages[path].Add((lib.Name, book.Name, page.Name));
                                    }
                                }
                            }
                        }
                    }
                }

                // 找出重复项
                var duplicates = assetToPages.Where(kvp => kvp.Value.Count > 1).ToList();

                if (duplicates.Count == 0)
                {
                    EditorUtility.DisplayDialog("全项目检测完成", "未发现重复资源引用。", "确定");
                }
                else
                {
                    var message = $"全项目发现 {duplicates.Count} 个资源有重复引用：\n\n";
                    foreach (var dup in duplicates.Take(10))
                    {
                        var assetName = System.IO.Path.GetFileName(dup.Key);
                        message += $"• {assetName} ({dup.Value.Count}次引用)\n";

                        // 显示前3个引用位置
                        foreach (var loc in dup.Value.Take(3))
                        {
                            message += $"  - {loc.libName} > {loc.bookName} > {loc.pageName}\n";
                        }
                        if (dup.Value.Count > 3)
                        {
                            message += $"  ... 还有 {dup.Value.Count - 3} 个引用\n";
                        }
                        message += "\n";
                    }
                    if (duplicates.Count > 10)
                    {
                        message += $"...还有 {duplicates.Count - 10} 个重复资源";
                    }

                    message += "\n\n提示：请手动清理跨Library的重复引用";
                    EditorUtility.DisplayDialog("全项目重复检测结果", message, "确定");
                }
            }

            /// <summary>
            /// 清除空Pages
            /// </summary>
            private void RemoveEmptyPages(TBook book)
            {
                if (RejectAssetLibraryStructureMutation()) return;
                if (book?.pages == null) return;

                Undo.RecordObject(library, "Remove Empty Pages");

                int removedCount = 0;
                for (int i = book.pages.Count - 1; i >= 0; i--)
                {
                    var page = book.pages[i];
                    if (page is ESAssetPage resPage && resPage.OB == null)
                    {
                        book.pages.RemoveAt(i);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    SaveAssetsImmediate();  // 清理操作需立即保存
                    EditorUtility.DisplayDialog("清理完成", $"已清除 {removedCount} 个空Page", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("清理完成", "没有发现空Page", "确定");
                }
            }

            #endregion

            private void OpenDraggedAssetsInRegistration(UnityEngine.Object[] assets, ESAssetLibrary targetLibrary)
            {
                UnityEngine.Object[] valid = assets?.Where(item => item != null).ToArray() ?? Array.Empty<UnityEngine.Object>();
                if (valid.Length == 0)
                    return;
                if (valid.Length > 1)
                {
                    EditorUtility.DisplayDialog(
                        "统一内容注册",
                        "一次拖入了 " + valid.Length + " 个资产。当前事务面板一次只提交一个资产，将先打开第一个；其余资产请逐项拖入，确保每项都有独立预检、revision 和 requestId。",
                        "打开第一个");
                }
                ESResourceCollectionWorkflowWindow.OpenForAssetRegistration(valid[0], targetLibrary);
            }

            private bool RejectAssetLibraryStructureMutation()
            {
                if (library is not ESAssetLibrary)
                    return false;
                EditorUtility.DisplayDialog(
                    "操作已禁用",
                    "ESAssetLibrary 的新增与改 Key 必须走统一内容注册事务；移除、移动、复制和合并尚未定义对应事务，当前禁止直接改写。",
                    "确定");
                return true;
            }

            #region 工具方法

            /// <summary>
            /// 显示自定义图标选择器
            /// </summary>
            private void ShowCustomIconPicker(TBook book)
            {
                var path = EditorUtility.OpenFilePanel("选择图标", "Assets", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path))
                {
                    // 转换为相对路径
                    if (path.StartsWith(Application.dataPath))
                    {
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (icon != null)
                    {
                        Undo.RecordObject(library, "Set Custom Icon");
                        book.CustomIcon = icon;
                        MarkDirtyDeferred();
                    }
                }
            }

            /// <summary>
            /// 绘制选中项的边框
            /// </summary>
            private void DrawSelectionBorder(Rect rect)
            {
                var borderRect = new Rect(rect.x, rect.y, rect.width, rect.height);
                EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, borderRect.width, SELECTION_BORDER_WIDTH), SELECTION_BORDER_COLOR);
                EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.yMax - SELECTION_BORDER_WIDTH, borderRect.width, SELECTION_BORDER_WIDTH), SELECTION_BORDER_COLOR);
                EditorGUI.DrawRect(new Rect(borderRect.x, borderRect.y, SELECTION_BORDER_WIDTH, borderRect.height), SELECTION_BORDER_COLOR);
                EditorGUI.DrawRect(new Rect(borderRect.xMax - SELECTION_BORDER_WIDTH, borderRect.y, SELECTION_BORDER_WIDTH, borderRect.height), SELECTION_BORDER_COLOR);
            }

            /// <summary>
            /// 在缩略图模式下绘制Page（缩略图+名称）
            /// </summary>
            private void DrawPageInGridMode(Rect rect, TPage page, UnityEngine.Object asset)
            {
                // 获取或生成缩略图
                if (!thumbnailCache.TryGetValue(asset, out var thumbnail) || thumbnail == null)
                {
                    thumbnail = AssetPreview.GetAssetPreview(asset);
                    if (thumbnail != null)
                    {
                        thumbnailCache[asset] = thumbnail;
                    }
                }

                // 布局：左侧缩略图（垂直居中），右侧名称
                float yOffset = (rect.height - THUMBNAIL_SIZE) * 0.5f;  // 垂直居中
                var thumbRect = new Rect(rect.x + 6, rect.y + yOffset, THUMBNAIL_SIZE, THUMBNAIL_SIZE);
                var nameRect = new Rect(rect.x + THUMBNAIL_SIZE + 12, rect.y, rect.width - THUMBNAIL_SIZE - 16, rect.height);

                // 绘制缩略图
                if (thumbnail != null)
                {
                    GUI.DrawTexture(thumbRect, thumbnail, ScaleMode.ScaleToFit);
                }
                else
                {
                    // 缩略图加载中，显示默认图标
                    var icon = AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(asset));
                    if (icon != null)
                    {
                        GUI.DrawTexture(thumbRect, icon, ScaleMode.ScaleToFit);
                    }
                }

                // 显示名称，垂直居中
                EditorGUI.LabelField(nameRect, page.Name);
            }

            #endregion

        }

        public class Page_Root_Consumer : ESWindowPageBase
        {
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string createText = "--创建新的Consumer--";

            [LabelText("新建Consumer名")]
            public string ConsumerName = "新建Consumer";

            [LabelText("描述")]
            [TextArea(3, 5)]
            public string ConsumerDesc = "描述：这个Consumer包含哪些库";

            [LabelText("选择包含的库")]
            public List<TLib> selectedLibraries = new List<TLib>();

            [Button(ButtonHeight = 30, Name = "创建Consumer")]
            public void CreateNewConsumer()
            {
                var consumer = ScriptableObject.CreateInstance<TConsumer>();
                consumer.Name = ConsumerName;
                consumer.Desc = ConsumerDesc;
                consumer.ConsumerLibFolders.AddRange(selectedLibraries);
                if (consumer is ESAssetLibraryConsumer resourceConsumer)
                {
                    resourceConsumer.EnsureStableIdentity();
                    var allConsumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>();
                    resourceConsumer.IsTotalConsumer = allConsumers == null || !allConsumers.Any(item => item != null && item.IsTotalConsumer);
                }

                string basePath = ESGlobalEditorDefaultConfi.Instance.Path_AllLibraryFolder_;
                if (!AssetDatabase.IsValidFolder(basePath))
                {
                    ESDesignUtility.SafeEditor.Quick_CreateAssetFolder(basePath);
                }
                string consumerFolder = basePath + "/Consumer";
                if (!AssetDatabase.IsValidFolder(consumerFolder))
                {
                    AssetDatabase.CreateFolder(basePath, "Consumer");
                }
                string path = AssetDatabase.GenerateUniqueAssetPath(consumerFolder + "/" + ConsumerName + ".asset");
                AssetDatabase.CreateAsset(consumer, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("Consumer created: " + path);
            }
        }

        public class Page_Index_Consumer : ESWindowPageBase
        {
            private static readonly ESEditorSectionNavigatorItem[] ConsumerSections =
            {
                new ESEditorSectionNavigatorItem("basic", "基础与备注", "Consumer 身份、GameCore、常驻资产与制作备注。"),
                new ESEditorSectionNavigatorItem("libraries", "Library 配置", "启动必需与可选下载 Library。"),
                new ESEditorSectionNavigatorItem("publish", "发布关系", "总入口、渠道、依赖 Consumer 与构建版本。"),
                new ESEditorSectionNavigatorItem("code", "代码与文件", "代码热更、附加文件与发布准备。")
            };

            [HideInInspector]
            public TConsumer package;
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string createText = "--编辑Consumer--";

            private int selectedTab;

            [OnInspectorGUI]
            public void DrawPackage()
            {
                if (package == null)
                {
                    EditorGUILayout.HelpBox("Consumer 资产已丢失，请刷新资源面板。", MessageType.Error);
                    return;
                }

                DrawConsumerSummary();

                string assetPath = AssetDatabase.GetAssetPath(package);
                string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                string navigatorKey = "ES.ConsumerEditor."
                    + (string.IsNullOrEmpty(assetGuid) ? package.GetInstanceID().ToString() : assetGuid);
                string currentId = selectedTab == 1 ? "libraries"
                    : selectedTab == 2 ? "publish"
                    : selectedTab == 3 ? "code"
                    : "basic";
                string selectedId = ESEditorSectionNavigatorIMGUI.Draw(
                    navigatorKey,
                    currentId,
                    ConsumerSections);
                selectedTab = string.Equals(selectedId, "libraries", StringComparison.Ordinal) ? 1
                    : string.Equals(selectedId, "publish", StringComparison.Ordinal) ? 2
                    : string.Equals(selectedId, "code", StringComparison.Ordinal) ? 3
                    : 0;
                EditorGUILayout.Space(8);
                switch (selectedTab)
                {
                    case 0:
                        DrawBasicInfo();
                        break;
                    case 1:
                        DrawLibraries();
                        break;
                    case 2:
                        DrawPublishSettings();
                        break;
                    default:
                        DrawCodePackages();
                        break;
                }
            }

            private void DrawConsumerSummary()
            {
                SimpleToolsPanelUtility.DrawSectionTitle(
                    "Consumer 概览",
                    "身份、入口与运行版本，先确认当前资产再进入配置。");
                using (new EditorGUILayout.VerticalScope())
                {
                    string stableId = package is ESAssetLibraryConsumer resourceConsumer
                        ? (string.IsNullOrWhiteSpace(resourceConsumer.ConsumerId) ? "未生成" : resourceConsumer.ConsumerId)
                        : string.Empty;
                    string totalText = package is ESAssetLibraryConsumer totalConsumer
                        ? (totalConsumer.IsTotalConsumer ? "总入口" : "普通 Consumer")
                        : "普通 Consumer";
                    SimpleToolsPanelUtility.DrawSummary(
                        "名称: " + package.Name,
                        "版本: " + package.Version,
                        "必需库: " + (package.ConsumerLibFolders?.Count ?? 0).ToString(),
                        "入口: " + totalText,
                        string.IsNullOrEmpty(stableId) ? null : "ID: " + stableId);
                }
            }

            private void DrawBasicInfo()
            {
                SimpleToolsPanelUtility.DrawSectionTitle("基础与备注", "先确认名称、版本、GameCore 与常驻资产，再进入发布配置。");
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField("Consumer 基础信息", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.DelayedTextField("Consumer 名", package.Name);
                string version = EditorGUILayout.DelayedTextField("业务版本", package.Version);
                EditorGUILayout.LabelField("描述");
                string description = EditorGUILayout.TextArea(package.Desc, GUILayout.MinHeight(70));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(package, "Edit Consumer Basic Info");
                    package.Name = name;
                    package.Version = version;
                    package.Desc = description;
                    MarkPackageDirty();
                }
                SirenixEditorGUI.EndBox();

                if (package is ESAssetLibraryConsumer resourceConsumer)
                    DrawResourceNotes(resourceConsumer);

                if (package is ESAssetLibraryConsumer gameCoreConsumer)
                {
                    DrawGameCoreAssets(gameCoreConsumer);
                    DrawResidentAssets(gameCoreConsumer);
                }
            }

            private void DrawGameCoreAssets(ESAssetLibraryConsumer consumer)
            {
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField("GameCore 启动核心", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("已收集", (consumer.GameCoreAssets?.Count ?? 0) + " 个");
                EditorGUILayout.LabelField("手动补充", (consumer.ManualGameCoreAssets?.Count ?? 0) + " 个");
                EditorGUILayout.HelpBox("同步和手动补充都通过统一内容注册事务执行。", MessageType.Info);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("同步并检查"))
                    ESResourceCollectionWorkflowWindow.OpenForConsumerSynchronization(consumer);
                using (new EditorGUI.DisabledScope(true))
                    GUILayout.Button(new GUIContent("清空手动补充", "尚未定义带 revision/CAS/回滚的批量移除事务。"));
                EditorGUILayout.EndHorizontal();

                Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "拖入 IGameCoreSO 以手动补充");
                Event current = Event.current;
                if (dropArea.Contains(current.mousePosition) && current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    current.Use();
                }
                else if (dropArea.Contains(current.mousePosition) && current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    ScriptableObject[] sources = DragAndDrop.objectReferences.OfType<ScriptableObject>().ToArray();
                    if (sources.Length == 0)
                        Debug.LogWarning("[ESRes][Register] 仅允许选择 ScriptableObject GameCore 根资产。");
                    else
                    {
                        if (sources.Length > 1)
                            Debug.LogWarning("[ESRes][Register] GameCore Root 事务一次只接收一个资产，本次先打开第一个。其余资产请逐项提交。");
                        ESResourceCollectionWorkflowWindow.OpenForGameCoreRootRegistration(sources[0], consumer);
                    }
                    current.Use();
                }

                if (consumer.ManualGameCoreAssets != null)
                for (int index = 0; index < consumer.ManualGameCoreAssets.Count; index++)
                {
                    ESAssetReferBase refer = consumer.ManualGameCoreAssets[index];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(refer == null ? "<Missing>" : refer.GUID, EditorStyles.miniLabel);
                    using (new EditorGUI.DisabledScope(true))
                        GUILayout.Button(new GUIContent("移除", "尚未定义带 revision/CAS/回滚的 GameCore 移除事务。"), GUILayout.Width(55));
                    EditorGUILayout.EndHorizontal();
                }
                if (consumer.GameCoreValidationErrors != null && consumer.GameCoreValidationErrors.Count > 0)
                    EditorGUILayout.HelpBox(string.Join("\n", consumer.GameCoreValidationErrors), MessageType.Error);
                else
                    EditorGUILayout.HelpBox("GameCore 依赖检查通过。", MessageType.Info);
                SirenixEditorGUI.EndBox();
            }

            private void DrawResidentAssets(ESAssetLibraryConsumer consumer)
            {
                consumer.ResidentAssets ??= new List<ESAssetReferBase>();
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField("启动常驻资产", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("拖入已注册到 AssetLibrary 的普通资产。Consumer 初始化时自动加载，场景切换不释放；GameCore、Scene、脚本和 EditorOnly 资产不允许加入。", MessageType.Info);

                Rect dropArea = GUILayoutUtility.GetRect(0, 42, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "拖入普通资产作为启动常驻资产");
                Event current = Event.current;
                if (dropArea.Contains(current.mousePosition) && current.type == EventType.DragUpdated)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    current.Use();
                }
                else if (dropArea.Contains(current.mousePosition) && current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    Undo.RecordObject(consumer, "Add Consumer Resident Assets");
                    foreach (UnityEngine.Object asset in DragAndDrop.objectReferences)
                        if (!ESAssetConsumerReferenceAuthoring.TryAddResidentAsset(consumer, asset, out string error))
                            Debug.LogWarning("[ESRes][Resident] " + asset.name + "：" + error, asset);
                    MarkPackageDirty();
                    current.Use();
                }

                for (int index = 0; index < consumer.ResidentAssets.Count; index++)
                {
                    ESAssetReferBase refer = consumer.ResidentAssets[index];
                    string path = refer == null ? string.Empty : AssetDatabase.GUIDToAssetPath(refer.GUID);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(path) ? "<Missing>" : path, EditorStyles.miniLabel);
                    if (!string.IsNullOrEmpty(path) && GUILayout.Button("定位", GUILayout.Width(48)))
                    {
                        UnityEngine.Object target = AssetDatabase.LoadMainAssetAtPath(path);
                        Selection.activeObject = target;
                        EditorGUIUtility.PingObject(target);
                    }
                    if (GUILayout.Button("移除", GUILayout.Width(48)))
                    {
                        Undo.RecordObject(consumer, "Remove Consumer Resident Asset");
                        consumer.ResidentAssets.RemoveAt(index--);
                        MarkPackageDirty();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (consumer.ResidentAssets.Count == 0)
                    EditorGUILayout.LabelField("当前没有启动常驻资产。", EditorStyles.centeredGreyMiniLabel);
                SirenixEditorGUI.EndBox();
            }

            private void DrawResourceNotes(ESAssetLibraryConsumer resourceConsumer)
            {
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField("制作与版本备注", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                string maintainer = EditorGUILayout.DelayedTextField("维护负责人", resourceConsumer.Maintainer);
                EditorGUILayout.LabelField("对外版本说明");
                string releaseNotes = EditorGUILayout.TextArea(resourceConsumer.ReleaseNotes, GUILayout.MinHeight(65));
                EditorGUILayout.LabelField("内部备注（不写入发布清单）");
                string internalNotes = EditorGUILayout.TextArea(resourceConsumer.InternalNotes, GUILayout.MinHeight(65));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(resourceConsumer, "Edit Consumer Notes");
                    resourceConsumer.Maintainer = maintainer;
                    resourceConsumer.ReleaseNotes = releaseNotes;
                    resourceConsumer.InternalNotes = internalNotes;
                    MarkPackageDirty();
                }

                resourceConsumer.Tags ??= new List<string>();
                EditorGUILayout.LabelField("标签", EditorStyles.boldLabel);
                for (int index = 0; index < resourceConsumer.Tags.Count; index++)
                {
                    EditorGUILayout.BeginHorizontal();
                    string tag = EditorGUILayout.DelayedTextField(resourceConsumer.Tags[index]);
                    if (!string.Equals(tag, resourceConsumer.Tags[index], StringComparison.Ordinal))
                    {
                        Undo.RecordObject(resourceConsumer, "Edit Consumer Tag");
                        resourceConsumer.Tags[index] = tag.Trim();
                        MarkPackageDirty();
                    }
                    if (GUILayout.Button("移除", GUILayout.Width(55)))
                    {
                        Undo.RecordObject(resourceConsumer, "Remove Consumer Tag");
                        resourceConsumer.Tags.RemoveAt(index--);
                        MarkPackageDirty();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("添加标签"))
                {
                    Undo.RecordObject(resourceConsumer, "Add Consumer Tag");
                    resourceConsumer.Tags.Add(string.Empty);
                    MarkPackageDirty();
                }
                SirenixEditorGUI.EndBox();
            }

            private void DrawLibraries()
            {
                SimpleToolsPanelUtility.DrawSectionTitle("Library 配置", "启动必需 Library 会进入启动包；可选下载 Library 按需拉取。");
                DrawLibraryList("启动必需 Library", package.ConsumerLibFolders, "Required");
                if (package is ESAssetLibraryConsumer resourceConsumer)
                    DrawResourceLibraryList("可选下载 Library", resourceConsumer.OptionalLibFolders, "Optional");
            }

            private void DrawPublishSettings()
            {
                if (!(package is ESAssetLibraryConsumer resourceConsumer))
                {
                    EditorGUILayout.HelpBox("当前 Consumer 类型没有发布扩展配置。", MessageType.Info);
                    return;
                }

                SimpleToolsPanelUtility.DrawSectionTitle("发布关系", "总入口必须唯一；稳定 ID、渠道与依赖 Consumer 是发布清单权威。");
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField("发布身份", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("稳定 ID", string.IsNullOrEmpty(resourceConsumer.ConsumerId) ? "未生成" : resourceConsumer.ConsumerId);
                if (string.IsNullOrEmpty(resourceConsumer.ConsumerId) && GUILayout.Button("生成稳定 ID"))
                {
                    Undo.RecordObject(resourceConsumer, "Generate Consumer Stable Id");
                    resourceConsumer.EnsureStableIdentity();
                    MarkPackageDirty();
                }

                EditorGUI.BeginChangeCheck();
                bool hasOtherTotalConsumer = HasOtherTotalConsumer(resourceConsumer);
                EditorGUI.BeginDisabledGroup(resourceConsumer.IsTotalConsumer && !hasOtherTotalConsumer);
                bool isTotal = EditorGUILayout.ToggleLeft("总 Consumer（唯一启动入口）", resourceConsumer.IsTotalConsumer);
                EditorGUI.EndDisabledGroup();
                string channel = EditorGUILayout.DelayedTextField("发布渠道", resourceConsumer.Channel);
                if (EditorGUI.EndChangeCheck())
                {
                    if (isTotal && !resourceConsumer.IsTotalConsumer)
                        ClearOtherTotalConsumers(resourceConsumer);
                    Undo.RecordObject(resourceConsumer, "Edit Consumer Publish Settings");
                    resourceConsumer.IsTotalConsumer = isTotal;
                    resourceConsumer.Channel = string.IsNullOrWhiteSpace(channel) ? "default" : channel.Trim();
                    MarkPackageDirty();
                }
                if (resourceConsumer.IsTotalConsumer && hasOtherTotalConsumer)
                {
                    EditorGUILayout.HelpBox("检测到多个总 Consumer，请将当前项修复为唯一启动入口。", MessageType.Error);
                    if (GUILayout.Button("将当前项设为唯一总 Consumer"))
                    {
                        ClearOtherTotalConsumers(resourceConsumer);
                        MarkPackageDirty();
                    }
                }
                else if (resourceConsumer.IsTotalConsumer)
                {
                    EditorGUILayout.HelpBox("若要更换启动入口，请在目标 Consumer 上开启“总 Consumer”。", MessageType.Info);
                }
                EditorGUILayout.LabelField("构建修订", resourceConsumer.BuildRevision.ToString());
                EditorGUILayout.LabelField("运行时版本", resourceConsumer.RuntimeVersion);
                EditorGUILayout.LabelField("最后构建 UTC", string.IsNullOrEmpty(resourceConsumer.LastBuildUtc) ? "尚未构建" : resourceConsumer.LastBuildUtc);
                SirenixEditorGUI.EndBox();

                DrawRequiredConsumers(resourceConsumer);
            }

            private void DrawCodePackages()
            {
                if (!(package is ESAssetLibraryConsumer resourceConsumer))
                {
                    EditorGUILayout.HelpBox("当前 Consumer 不支持代码更新。", MessageType.Info);
                    return;
                }

                SimpleToolsPanelUtility.DrawSectionTitle("代码与文件", "代码热更与附加文件只在这里显式配置；发布前必须完成 Consumer 代码包准备。");
                resourceConsumer.CodePackages ??= new List<ESConsumerCodePackageConfig>();
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField("代码更新", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                bool enableCodeHotUpdate = EditorGUILayout.ToggleLeft("启用代码热更", resourceConsumer.EnableCodeHotUpdate);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(resourceConsumer, "Toggle Consumer Code Hot Update");
                    ESCodeModuleEditorIntegration.SetConsumerHotUpdateEnabled(resourceConsumer, enableCodeHotUpdate);
                    MarkPackageDirty();
                }
                if (resourceConsumer.EnableCodeHotUpdate)
                {
                    AssemblyDefinitionAsset currentDefinition = ESCodeModuleEditorIntegration.GetConsumerAssemblyDefinition(resourceConsumer);
                    EditorGUI.BeginChangeCheck();
                    AssemblyDefinitionAsset selectedDefinition = (AssemblyDefinitionAsset)EditorGUILayout.ObjectField(
                        "代码模块", currentDefinition, typeof(AssemblyDefinitionAsset), false);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(resourceConsumer, "Set Consumer Hot Update Assembly");
                        ESCodeModuleEditorIntegration.SetConsumerAssemblyDefinition(resourceConsumer, selectedDefinition);
                        MarkPackageDirty();
                    }
                    if (selectedDefinition == null)
                        EditorGUILayout.HelpBox("请选择该 Consumer 使用的代码模块。", MessageType.Warning);
                    EditorGUILayout.HelpBox("选择后，ES 会自动完成编译、发布和启动加载。", MessageType.Info);
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(selectedDefinition == null))
                    {
                        if (GUILayout.Button("打开代码目录")) ESCodeModuleEditorIntegration.OpenConsumerCodeFolder(resourceConsumer);
                        if (GUILayout.Button("检查配置"))
                        {
                            try { EditorUtility.DisplayDialog("配置检查", ESCodeModuleEditorIntegration.ValidateConsumerInEditor(resourceConsumer), "确定"); }
                            catch (Exception exception) { EditorUtility.DisplayDialog("配置检查未通过", exception.Message, "确定"); }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                SirenixEditorGUI.EndBox();
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("附加文件", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("仅在需要额外代码、调试符号或数据文件时添加。自动生成的运行文件不会显示在这里。", MessageType.Info);
                for (int index = 0; index < resourceConsumer.CodePackages.Count; index++)
                {
                    ESConsumerCodePackageConfig config = resourceConsumer.CodePackages[index];
                    if (config == null)
                    {
                        Undo.RecordObject(resourceConsumer, "Repair Consumer Code Package");
                        config = new ESConsumerCodePackageConfig();
                        resourceConsumer.CodePackages[index] = config;
                        MarkPackageDirty();
                    }
                    if (config.ManagedByHybridCLR)
                        continue;

                    SirenixEditorGUI.BeginBox();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(config.PackageKey) ? "未命名文件" : config.PackageKey, EditorStyles.boldLabel);
                    if (GUILayout.Button("移除", GUILayout.Width(55)))
                    {
                        Undo.RecordObject(resourceConsumer, "Remove Consumer Code Package");
                        resourceConsumer.CodePackages.RemoveAt(index--);
                        MarkPackageDirty();
                        EditorGUILayout.EndHorizontal();
                        SirenixEditorGUI.EndBox();
                        continue;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.Toggle("启用", config.Enabled);
                    string packageKey = EditorGUILayout.DelayedTextField("名称", config.PackageKey);
                    ESConsumerCodePackageKind kind = DrawAdditionalFileKind(config.Kind);
                    EditorGUILayout.BeginHorizontal();
                    string sourcePath = EditorGUILayout.DelayedTextField("文件", config.SourcePath);
                    if (GUILayout.Button("选择", GUILayout.Width(55)))
                    {
                        string selectedPath = EditorUtility.OpenFilePanel("选择附加文件", ResolveCodePackageDirectory(config.SourcePath), string.Empty);
                        if (!string.IsNullOrEmpty(selectedPath))
                            sourcePath = MakeProjectRelativePath(selectedPath);
                    }
                    EditorGUILayout.EndHorizontal();
                    bool requiredAtBoot = EditorGUILayout.Toggle("启动时准备", config.RequiredAtBoot);
                    int loadOrder = EditorGUILayout.IntField("优先级", config.LoadOrder);
                    EditorGUILayout.LabelField("备注");
                    string notes = EditorGUILayout.TextArea(config.Notes, GUILayout.MinHeight(45));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(resourceConsumer, "Edit Consumer Code Package");
                        config.Enabled = enabled;
                        config.PackageKey = packageKey.Trim();
                        config.Kind = kind;
                        config.SourcePath = sourcePath.Trim();
                        config.RequiredAtBoot = requiredAtBoot;
                        config.LoadOrder = loadOrder;
                        config.Notes = notes;
                        MarkPackageDirty();
                    }
                    SirenixEditorGUI.EndBox();
                }

                if (GUILayout.Button("添加附加文件", GUILayout.Height(28)))
                {
                    Undo.RecordObject(resourceConsumer, "Add Consumer Code Package");
                    resourceConsumer.CodePackages.Add(new ESConsumerCodePackageConfig
                    {
                        PackageKey = "file_" + (resourceConsumer.CodePackages.Count + 1),
                        Kind = ESConsumerCodePackageKind.RawBinary
                    });
                    MarkPackageDirty();
                }

                IEnumerable<string> duplicateKeys = resourceConsumer.CodePackages
                    .Where(item => item != null && !item.ManagedByHybridCLR && item.Enabled && !string.IsNullOrWhiteSpace(item.PackageKey))
                    .GroupBy(item => item.PackageKey.Trim(), StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key);
                string duplicates = string.Join(", ", duplicateKeys);
                if (!string.IsNullOrEmpty(duplicates))
                    EditorGUILayout.HelpBox("附加文件名称重复：" + duplicates, MessageType.Error);
            }

            private static ESConsumerCodePackageKind DrawAdditionalFileKind(ESConsumerCodePackageKind current)
            {
                ESConsumerCodePackageKind[] values =
                {
                    ESConsumerCodePackageKind.HotUpdateAssembly,
                    ESConsumerCodePackageKind.Symbols,
                    ESConsumerCodePackageKind.ManagedData,
                    ESConsumerCodePackageKind.RawBinary
                };
                string[] labels = { "附加代码模块", "调试文件", "数据文件", "其他文件" };
                int index = Array.IndexOf(values, current);
                if (index < 0) index = values.Length - 1;
                return values[EditorGUILayout.Popup("类型", index, labels)];
            }

            private static string ResolveCodePackageDirectory(string sourcePath)
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                    return Directory.GetParent(Application.dataPath).FullName;
                string fullPath = Path.IsPathRooted(sourcePath)
                    ? sourcePath
                    : Path.Combine(Directory.GetParent(Application.dataPath).FullName, sourcePath);
                return File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : Directory.GetParent(Application.dataPath).FullName;
            }

            private static string MakeProjectRelativePath(string fullPath)
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/').TrimEnd('/');
                string normalized = (fullPath ?? string.Empty).Replace('\\', '/');
                return normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                    ? normalized.Substring(projectRoot.Length + 1)
                    : normalized;
            }

            private void DrawLibraryList(string title, List<TLib> list, string undoPrefix)
            {
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                for (int index = 0; index < list.Count; index++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(list[index], typeof(TLib), false);
                    if (GUILayout.Button("移除", GUILayout.Width(55)))
                    {
                        Undo.RecordObject(package, "Remove " + undoPrefix + " Library");
                        list.RemoveAt(index--);
                        MarkPackageDirty();
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("添加 " + title))
                    ShowLibraryMenu(GUILayoutUtility.GetLastRect(), list, undoPrefix);
                SirenixEditorGUI.EndBox();
            }

            private void ShowLibraryMenu(Rect anchorRect, List<TLib> list, string undoPrefix)
            {
                var entries = new List<ESSearchDropdown.Entry>();
                var allLibraries = ESEditorSO.GetGroupOfType<TLib>() ?? new List<TLib>();
                foreach (TLib library in allLibraries.Where(item => item != null && !list.Contains(item)))
                {
                    TLib captured = library;
                    string assetPath = AssetDatabase.GetAssetPath(captured);
                    entries.Add(ESSearchDropdown.Entry.Item(
                        captured.Name,
                        () =>
                        {
                            Undo.RecordObject(package, "Add " + undoPrefix + " Library");
                            list.Add(captured);
                            MarkPackageDirty();
                        },
                        captured.GetType().Name,
                        AssetPreview.GetMiniThumbnail(captured),
                        subtitle: assetPath,
                        badge: "Library"));
                }
                if (entries.Count == 0)
                    entries.Add(ESSearchDropdown.Entry.Disabled("没有可添加的 Library"));
                ESSearchDropdown.Open(anchorRect, "添加 " + undoPrefix + " Library", entries);
            }

            private void DrawResourceLibraryList(string title, List<ESAssetLibrary> list, string undoPrefix)
            {
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                for (int index = 0; index < list.Count; index++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(list[index], typeof(ESAssetLibrary), false);
                    if (GUILayout.Button("移除", GUILayout.Width(55)))
                    {
                        Undo.RecordObject(package, "Remove " + undoPrefix + " Library");
                        list.RemoveAt(index--);
                        MarkPackageDirty();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("添加 " + title))
                {
                    Rect anchorRect = GUILayoutUtility.GetLastRect();
                    var entries = new List<ESSearchDropdown.Entry>();
                    var allLibraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>() ?? new List<ESAssetLibrary>();
                    foreach (ESAssetLibrary library in allLibraries.Where(item => item != null && !list.Contains(item)))
                    {
                        ESAssetLibrary captured = library;
                        string assetPath = AssetDatabase.GetAssetPath(captured);
                        entries.Add(ESSearchDropdown.Entry.Item(
                            captured.Name,
                            () =>
                            {
                                Undo.RecordObject(package, "Add " + undoPrefix + " Library");
                                list.Add(captured);
                                MarkPackageDirty();
                            },
                            string.IsNullOrWhiteSpace(captured.AssetBundleCode) ? "未设置短码" : captured.AssetBundleCode,
                            AssetPreview.GetMiniThumbnail(captured),
                            subtitle: assetPath,
                            badge: captured.ContainsBuild ? "参与构建" : "不构建"));
                    }
                    if (entries.Count == 0)
                        entries.Add(ESSearchDropdown.Entry.Disabled("没有可添加的 Library"));
                    ESSearchDropdown.Open(anchorRect, "添加资源 Library", entries);
                }
                SirenixEditorGUI.EndBox();
            }

            private void DrawRequiredConsumers(ESAssetLibraryConsumer resourceConsumer)
            {
                SirenixEditorGUI.BeginBox();
                EditorGUILayout.LabelField("依赖 Consumer", EditorStyles.boldLabel);
                for (int index = 0; index < resourceConsumer.RequiredConsumers.Count; index++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(resourceConsumer.RequiredConsumers[index], typeof(ESAssetLibraryConsumer), false);
                    if (GUILayout.Button("移除", GUILayout.Width(55)))
                    {
                        Undo.RecordObject(resourceConsumer, "Remove Required Consumer");
                        resourceConsumer.RequiredConsumers.RemoveAt(index--);
                        MarkPackageDirty();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (GUILayout.Button("添加依赖 Consumer"))
                {
                    Rect anchorRect = GUILayoutUtility.GetLastRect();
                    var entries = new List<ESSearchDropdown.Entry>();
                    var allConsumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>() ?? new List<ESAssetLibraryConsumer>();
                    foreach (ESAssetLibraryConsumer consumer in allConsumers.Where(item => item != null && item != resourceConsumer && !resourceConsumer.RequiredConsumers.Contains(item)))
                    {
                        ESAssetLibraryConsumer captured = consumer;
                        string assetPath = AssetDatabase.GetAssetPath(captured);
                        entries.Add(ESSearchDropdown.Entry.Item(
                            captured.Name,
                            () =>
                            {
                                Undo.RecordObject(resourceConsumer, "Add Required Consumer");
                                resourceConsumer.RequiredConsumers.Add(captured);
                                MarkPackageDirty();
                            },
                            string.IsNullOrWhiteSpace(captured.Channel) ? "默认渠道" : captured.Channel,
                            AssetPreview.GetMiniThumbnail(captured),
                            subtitle: assetPath,
                            badge: captured.IsTotalConsumer ? "总入口" : "Consumer"));
                    }
                    if (entries.Count == 0)
                        entries.Add(ESSearchDropdown.Entry.Disabled("没有可添加的 Consumer"));
                    ESSearchDropdown.Open(anchorRect, "添加依赖 Consumer", entries);
                }
                SirenixEditorGUI.EndBox();
            }

            private static void ClearOtherTotalConsumers(ESAssetLibraryConsumer current)
            {
                var consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>();
                if (consumers == null) return;
                foreach (ESAssetLibraryConsumer consumer in consumers)
                {
                    if (consumer == null || consumer == current || !consumer.IsTotalConsumer) continue;
                    Undo.RecordObject(consumer, "Change Total Consumer");
                    consumer.IsTotalConsumer = false;
                    EditorUtility.SetDirty(consumer);
                }
            }

            private static bool HasOtherTotalConsumer(ESAssetLibraryConsumer current)
            {
                var consumers = ESEditorSO.GetGroupOfType<ESAssetLibraryConsumer>();
                return consumers != null && consumers.Any(consumer => consumer != null && consumer != current && consumer.IsTotalConsumer);
            }

            private void MarkPackageDirty()
            {
                EditorUtility.SetDirty(package);
            }

            public override ESWindowPageBase ES_Refresh()
            {
                if (package is ESAssetLibraryConsumer resourceConsumer && resourceConsumer.EnsureStableIdentity())
                    EditorUtility.SetDirty(resourceConsumer);
                createText = $"--编辑Consumer【{package.Name}】--";
                return base.ES_Refresh();
            }

            public override void OnPageDisable()
            {
                base.OnPageDisable();
                if (package != null && EditorUtility.IsDirty(package))
                    AssetDatabase.SaveAssets();
            }
        }


        public void ApplyTemplateToMenuTree<T>(ESOdinMenuTreeWindow<T> from, OdinMenuTree tree, string menuName)
        where T : ESOdinMenuTreeWindow<T>
        {
            from.QuickBuildRootMenu(tree, menuName, ref page_root_Library, Sirenix.OdinInspector.SdfIconType.KeyboardFill);
            from.QuickBuildRootMenu(tree, "Consumer", ref page_root_Consumer, SdfIconType.Box);

            var libs = ESEditorSO.GetGroupOfType<TLib>();
            if (libs != null)
            {
                List<string> strings = new List<string>(3);
                bool hasModified = false;
                foreach (var i in libs)
                {
                    if (i != null)
                    {
                        while (strings.Contains(i.Name))
                        {
                            Undo.RecordObject(i, "Rename Library");
                            i.Name += "_re";
                            EditorUtility.SetDirty(i);
                            hasModified = true;
                        }
                        strings.Add(i.Name);
                        from.RegisterAndAddPage(tree, menuName + $"/库：{i.Name}", new Page_Index_Library() { library = i }.ES_Refresh(), SdfIconType.Cart);
                    }
                }
                // 批量修改后保存
                if (hasModified)
                {
                    AssetDatabase.SaveAssets();
                }
            }

            var consumers = ESEditorSO.GetGroupOfType<TConsumer>();
            if (consumers != null)
            {
                List<string> strings = new List<string>(3);
                foreach (var i in consumers)
                {
                    if (i != null)
                    {
                        while (strings.Contains(i.Name))
                        {
                            Undo.RecordObject(i, "Rename Consumer");
                            i.Name += "_re";
                            EditorUtility.SetDirty(i);
                        }
                        strings.Add(i.Name);
                        from.RegisterAndAddPage(tree, "Consumer" + $"/包：{i.Name}", new Page_Index_Consumer() { package = i }.ES_Refresh(), SdfIconType.Box);
                    }
                }

                // 批量修改后保存
                if (strings.Count > 0)
                {
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}

