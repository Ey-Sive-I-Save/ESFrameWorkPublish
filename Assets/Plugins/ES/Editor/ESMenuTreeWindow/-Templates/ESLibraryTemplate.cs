using ES;
using ES.ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
            [ESBackGround("yellow", 0.2f), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), OnValueChanged("OnValueChanged_ChangeHappen")]
            [LabelText("是否包含在主包中")]
            public bool IsMainInClude = true;


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
                FolderPath_ = ESGlobalEditorDefaultConfi.Instance.Path_AllLibraryFolder_ + "/" + typeof(TLib).Name;
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
                        lib.IsMainInClude = IsMainInClude;
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
            private static float bookListScrollY = 0f;
            private const float ALIGNMENT_THRESHOLD = 150f;  // 超过150px才开始偏移
            private const float MAX_OFFSET = 400f;  // 最大偏移400px

            // 剪切功能的静态存储
            private static TBook cutBook;
            private static TPage cutPage;
            private static TLib cutBookSourceLibrary;
            private static TBook cutPageSourceBook;
            private static TLib cutPageSourceLibrary;

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
                EditorGUILayout.LabelField("↓库描述↓");
                var newDesc = EditorGUILayout.TextArea(library.Desc, GUILayout.Height(50));
                if (newDesc != library.Desc)
                {
                    Undo.RecordObject(library, "Edit Library Description");
                    library.Desc = newDesc;
                    MarkDirtyDeferred();
                }

                // 收集配置按钮
                if (GUILayout.Button("收集配置", GUILayout.Height(25)))
                {
                    ShowCollectionConfigMenu();
                }

                bookAreaWidth = EditorGUILayout.GetControlRect().width;
                SirenixEditorGUI.EndBox();

                // Books拖拽区域
                area.UpdateAtFisrt();

                // 绘制自定义Books
                REForBooks_SelfDefine.DoLayoutList();

                // 绘制默认Books（合并到同一个竖直列表）
                DrawDefaultBooksInline();

                // 在Books列表下方添加拖拽区域提示
                GUILayout.Label("↓ 拖入资产到此处自动分配到合适的DefaultBook ↓", EditorStyles.centeredGreyMiniLabel);
                dragAtForBooks.normalColor.a = 0.02f;
                if (dragAtForBooks.Update(out var booksAssets, area.TargetArea, Event.current))
                {
                    if (booksAssets != null && booksAssets.Length > 0)
                    {
                        Undo.RecordObject(library, "Drag Assets to Library Books");
                        library.EditorOnly_DragAssetsToBooks(booksAssets);
                        SaveAssetsImmediate();  // 拖拽资源需立即保存
                    }
                }
                area.UpdateAtLast();
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
                        draggable = true,
                        displayAdd = true,
                        displayRemove = true,
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
                        Undo.RecordObject(library, "Drag Assets to Book");
                        book.EditorOnly_DragAtArea(gs);
                        SaveAssetsImmediate();  // 拖拽资源需立即保存
                    }
                }
                area.UpdateAtLast();
            }
            [OnInspectorGUI]
            [HorizontalGroup("水平布局")]
            public void DrawPage()
            {
                // Debug: 检查DrawPage是否被调用
                Debug.Log($"[DrawPage] Called - book: {book?.Name ?? "null"}, page: {page?.Name ?? "null"}");

                if (book == null || page == null || !book.pages.Contains(page))
                {
                    Debug.Log($"[DrawPage] Early return - book null: {book == null}, page null: {page == null}, contains: {book?.pages.Contains(page) ?? false}");
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


                // 始终创建Space以避免Layout/Repaint控件数量不匹配
                Debug.Log($"[DrawPage] Dynamic Offset: {dynamicOffset}");



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
                if (page is ResPage resPage && resPage.OB != null)
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
                EditorGUILayout.LabelField("默认Books【自动收集，不可删改】", EditorStyles.boldLabel);

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
                    draggable = true,      // 允许拖拽排序
                    displayAdd = true, // 显示添加按钮
                    displayRemove = true, // 显示移除按钮
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
                        Debug.Log($"[Pages] Selected page: {page.Name}, Y position: {rect.y}");
                    }

                    // 获取Page关联的资源对象
                    UnityEngine.Object pageAsset = null;
                    if (page_ is ResPage resPage)
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

            // 封装Book粘贴逻辑
            private void PasteBookToLibrary(TLib targetLibrary, int insertIndex)
            {
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

            /// <summary>
            /// 显示收集配置菜单
            /// </summary>
            private void ShowCollectionConfigMenu()
            {
                if (library == null)
                {
                    Debug.LogError("[CollectionConfig] Library为null，无法显示配置菜单");
                    return;
                }

                var menu = new GenericMenu();

                // 获取所有资产类别（除了All）
                var categories = System.Enum.GetValues(typeof(ESAssetCategory)).Cast<ESAssetCategory>().Where(c => c != ESAssetCategory.All).ToArray();

                // "总体优先级"菜单项
                AddPriorityMenuItems(menu, "总体优先级", ESAssetCategory.All);

                menu.AddSeparator("");

                // 为每个资产类别添加菜单项
                foreach (var category in categories)
                {
                    string categoryName = GetCategoryDisplayName(category);
                    AddPriorityMenuItems(menu, categoryName, category);
                }

                menu.ShowAsContext();
            }

            /// <summary>
            /// 为指定类别添加优先级菜单项
            /// </summary>
            private void AddPriorityMenuItems(GenericMenu menu, string categoryName, ESAssetCategory category)
            {
                var priorities = System.Enum.GetValues(typeof(ESAssetCollectionPriority)).Cast<ESAssetCollectionPriority>().ToArray();
                var currentPriority = library.collectionConfig.GetPriority(category);

                foreach (var priority in priorities)
                {
                    string priorityName = GetPriorityDisplayName(priority);
                    string menuPath = $"{categoryName}/{priorityName}";
                    bool isSelected = (priority == currentPriority);

                    menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                    {
                        Undo.RecordObject(library, $"Set Collection Priority: {library.Name} - {category} - {priority}");
                        library.collectionConfig.SetPriority(category, priority);
                        EditorUtility.SetDirty(library);
                        AssetDatabase.SaveAssets();
                        Debug.Log($"[CollectionConfig] 设置 [{library.Name}] 的 [{categoryName}] 优先级为 [{priorityName}]");
                    });
                }

                menu.AddSeparator($"{categoryName}/");
            }

            /// <summary>
            /// 获取资产类别显示名称
            /// </summary>
            private string GetCategoryDisplayName(ESAssetCategory category)
            {
                switch (category)
                {
                    case ESAssetCategory.All: return "总体";
                    case ESAssetCategory.Prefab: return "预制体";
                    case ESAssetCategory.Scene: return "场景";
                    case ESAssetCategory.Material: return "材质";
                    case ESAssetCategory.Texture: return "纹理";
                    case ESAssetCategory.Model: return "模型";
                    case ESAssetCategory.Audio: return "音频";
                    case ESAssetCategory.Animation: return "动画";
                    case ESAssetCategory.Script: return "SO";
                    case ESAssetCategory.Shader: return "着色器";
                    case ESAssetCategory.Font: return "字体";
                    case ESAssetCategory.Video: return "视频";
                    case ESAssetCategory.Other: return "其他";
                    default: return category.ToString();
                }
            }

            /// <summary>
            /// 获取优先级显示名称
            /// </summary>
            private string GetPriorityDisplayName(ESAssetCollectionPriority priority)
            {
                switch (priority)
                {
                    case ESAssetCollectionPriority.Disabled: return "❌ 禁用";
                    case ESAssetCollectionPriority.Lowest: return "1. 最低";
                    case ESAssetCollectionPriority.Low: return "2. 较低";
                    case ESAssetCollectionPriority.Medium: return "3. 中等";
                    case ESAssetCollectionPriority.High: return "4. 较高";
                    case ESAssetCollectionPriority.Highest: return "5. 最高";
                    default: return priority.ToString();
                }
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
                        if (page is ResPage resPage && resPage.OB != null)
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
                        if (page is ResPage resPage && resPage.OB != null)
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
                var allLibraries = ESEditorSO.SOS.GetNewGroupOfType<TLib>();

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
                                if (page is ResPage resPage && resPage.OB != null)
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
                if (book?.pages == null) return;

                Undo.RecordObject(library, "Remove Empty Pages");

                int removedCount = 0;
                for (int i = book.pages.Count - 1; i >= 0; i--)
                {
                    var page = book.pages[i];
                    if (page is ResPage resPage && resPage.OB == null)
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

                string basePath = ESGlobalEditorDefaultConfi.Instance.Path_AllLibraryFolder_ + "/" + typeof(TLib).Name;
                if (!AssetDatabase.IsValidFolder(basePath))
                {
                    AssetDatabase.CreateFolder(ESGlobalEditorDefaultConfi.Instance.Path_AllLibraryFolder_, typeof(TLib).Name);
                }
                string consumerFolder = basePath + "/Consumer";
                if (!AssetDatabase.IsValidFolder(consumerFolder))
                {
                    AssetDatabase.CreateFolder(basePath, "Consumer");
                }
                string path = consumerFolder + "/" + ConsumerName + ".asset";
                AssetDatabase.CreateAsset(consumer, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("Consumer created: " + path);
            }
        }

        public class Page_Index_Consumer : ESWindowPageBase
        {
            [HideInInspector]
            public TConsumer package;
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string createText = "--编辑Consumer--";

            [OnInspectorGUI]
            public void DrawPackage()
            {
                SirenixEditorGUI.BeginBox();
                package.Name = EditorGUILayout.TextField("Consumer名", package.Name);
                package.Version = EditorGUILayout.TextField("版本号", package.Version);
                package.Desc = EditorGUILayout.TextArea("描述", package.Desc, GUILayout.Height(50));
                SirenixEditorGUI.EndBox();

                // 绘制Libraries列表
                EditorGUILayout.LabelField("包含的库:");
                for (int i = 0; i < package.ConsumerLibFolders.Count; i++)
                {
                    var lib = package.ConsumerLibFolders[i];
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.ObjectField(lib, typeof(TLib), false);
                    if (GUILayout.Button("移除", GUILayout.Width(50)))
                    {
                        Undo.RecordObject(package, "Remove Library from Consumer");
                        package.ConsumerLibFolders.RemoveAt(i);
                        EditorUtility.SetDirty(package);
                        // 移除操作需要立即保存
                        AssetDatabase.SaveAssets();
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("添加库"))
                {
                    // 弹出选择库的窗口或列表
                    var allLibs = ESEditorSO.SOS.GetNewGroupOfType<TLib>();
                    var menu = new GenericMenu();
                    foreach (var lib in allLibs)
                    {
                        if (!package.ConsumerLibFolders.Contains(lib))
                        {
                            menu.AddItem(new GUIContent(lib.Name), false, () =>
                            {
                                Undo.RecordObject(package, "Add Library to Consumer");
                                package.ConsumerLibFolders.Add(lib);
                                EditorUtility.SetDirty(package);
                                // 添加操作需要立即保存
                                AssetDatabase.SaveAssets();
                            });
                        }
                    }
                    menu.ShowAsContext();
                }
            }

            public override ESWindowPageBase ES_Refresh()
            {
                createText = $"--编辑Consumer【{package.Name}】--";
                return base.ES_Refresh();
            }
        }


        public void ApplyTemplateToMenuTree<T>(ESMenuTreeWindowAB<T> from, OdinMenuTree tree, string menuName)
        where T : ESMenuTreeWindowAB<T>
        {
            from.QuickBuildRootMenu(tree, menuName, ref page_root_Library, Sirenix.OdinInspector.SdfIconType.KeyboardFill);
            from.QuickBuildRootMenu(tree, "Consumer", ref page_root_Consumer, SdfIconType.Box);

            var libs = ESEditorSO.SOS.GetNewGroupOfType<TLib>();
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

            var consumers = ESEditorSO.SOS.GetNewGroupOfType<TConsumer>();
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

