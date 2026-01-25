using ES;
using ES.ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
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
        //Index_库
        public class Page_Index_Library : ESWindowPageBase
        {
            [HideInInspector]
            public TLib library;
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            public string createText = "--编辑库--";

            private ReorderableList REForBooks;
            private ReorderableList REForPages;
            private TBook book;
            private TPage page;

            private ESAreaSolver area = new ESAreaSolver();
            private ESDragAtSolver dragAt = new ESDragAtSolver();
            [OnInspectorGUI]
            public void DrawSelfAndBooks()
            {
                SirenixEditorGUI.BeginBox();
                library.Name = EditorGUILayout.TextField("【库】命名", library.Name);
                var preFolderName = library.LibFolderName;
                library.LibFolderName = EditorGUILayout.TextField("库文件夹名", library.LibFolderName);
                if (preFolderName != library.LibFolderName)
                {
                    Debug.Log("尝试修改库文件夹名");
                    library.Refresh();
                }
                EditorGUILayout.LabelField("↓库描述↓");
                library.Desc = EditorGUILayout.TextArea(library.Desc, GUILayout.Height(50));
                SirenixEditorGUI.EndBox();

                REForBooks.DoLayoutList();
            }
            [OnInspectorGUI]
            public void DrawBookAndPages()
            {
                if (book == null) return;
                if (REForPages == null)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    REForPages = new ReorderableList(book.pages, typeof(TPage))
                    {
                        draggable = true,      // 允许拖拽排序
                        displayAdd = true, // 显示添加按钮
                        displayRemove = true, // 显示移除按钮
                    };
                    SetupPagesCallBack();
                }
                REForPages.list = book.pages;
                SirenixEditorGUI.BeginBox();
                book.Name = EditorGUILayout.TextField("【册】命名", book.Name);
                EditorGUILayout.LabelField("↓册描述↓");
                book.Desc = EditorGUILayout.TextArea(book.Desc, GUILayout.Height(50));
                SirenixEditorGUI.EndBox();
                area.UpdateAtFisrt();
                REForPages.DoLayoutList();
                dragAt.normalColor.a = 0.02f;
                if (dragAt.Update(out var gs, area.TargetArea, Event.current))
                {
                    if (gs != null)
                    {
                        book.EditorOnly_DragAtArea(gs);
                        EditorUtility.SetDirty(library);
                        AssetDatabase.SaveAssets();
                    }
                }
                area.UpdateAtLast();
            }
            [OnInspectorGUI]
            public void DrawPage()
            {
                if (book == null || page == null || !book.pages.Contains(page)) return;
                SirenixEditorGUI.BeginBox();
                page.Name = EditorGUILayout.TextField("Page命名", page.Name);
                SirenixEditorGUI.EndBox();

                if (page.Draw())
                {
                    EditorUtility.SetDirty(library);
                    AssetDatabase.SaveAssets();
                }
            }
            public override ESWindowPageBase ES_Refresh()
            {
                createText = $"--编辑库【{library.GetSTR()}】--";
                REForBooks = new ReorderableList(library.Books, typeof(TBook))
                {
                    draggable = true,      // 允许拖拽排序
                    displayAdd = true, // 显示添加按钮
                    displayRemove = true, // 显示移除按钮
                };
                SetupBooksCallBack();
                return base.ES_Refresh();
            }
            private void SetupBooksCallBack()
            {
                //Debug.Log("重建");
                REForBooks.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "包含Book");
                };

                REForBooks.onChangedCallback += (ReorderableList list) =>
                {
                    Undo.RecordObject(library, "");
                    EditorUtility.SetDirty(library);
                    AssetDatabase.SaveAssets();
                };

                REForBooks.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var book_ = library.Books[index];
                    var color = isActive ? Color.yellow : (isFocused ? Color.white : Color.white);
                    if (isActive)
                    {
                        book = library.Books[index];
                    }
                    GUIHelper.PushColor(color);
                    EditorGUI.LabelField(rect, book_.Name);
                    GUIHelper.PopColor();
                };


            }
            private void SetupPagesCallBack()
            {
                REForPages.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "包含Page");
                };

                REForPages.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (book == null) return;
                    var page_ = book.pages[index];
                    var color = isActive ? Color.yellow : (isFocused ? Color.white : Color.white);
                    if (isActive)
                    {
                        page = book.pages[index];
                    }
                    GUIHelper.PushColor(color);
                    EditorGUI.LabelField(rect, page_.Name);
                    GUIHelper.PopColor();
                };

                REForPages.onChangedCallback += (ReorderableList list) =>
                {
                    Undo.RecordObject(library, "");
                    AssetDatabase.SaveAssets();
                    EditorUtility.SetDirty(library);
                };
            }
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
                        package.ConsumerLibFolders.RemoveAt(i);
                        EditorUtility.SetDirty(package);
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
                                package.ConsumerLibFolders.Add(lib);
                                EditorUtility.SetDirty(package);
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
                foreach (var i in libs)
                {
                    if (i != null)
                    {
                        while (strings.Contains(i.Name))
                        {
                            i.Name += "_re";
                            EditorUtility.SetDirty(i);
                            AssetDatabase.SaveAssets();
                        }
                        strings.Add(i.Name);
                        tree.Add(menuName + $"/库：{i.Name}", new Page_Index_Library() { library = i }.ES_Refresh(), SdfIconType.Cart);
                    }
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
                            i.Name += "_re";
                            EditorUtility.SetDirty(i);
                            AssetDatabase.SaveAssets();
                        }
                        strings.Add(i.Name);
                        tree.Add("Consumer" + $"/包：{i.Name}", new Page_Index_Consumer() { package = i }.ES_Refresh(), SdfIconType.Box);
                    }
                }
            }
        }
    }
}
