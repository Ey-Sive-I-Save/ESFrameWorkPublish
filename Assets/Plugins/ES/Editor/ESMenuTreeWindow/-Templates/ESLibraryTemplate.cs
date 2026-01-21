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
    public class ESLibraryWindowMenuTemplate<TLib, TBook, TPage>
    where TPage : PageBase, new()
    where TBook : BookBase<TPage>
    where TLib : LibrarySoBase<TBook>
    
    {
        public Page_Root_Library page_root_Library;

        public class Page_Root_Library : ESWindowPageBase
        {
            [Title("新建Lib库！", "每个库可以获得专属的资产", bold: true, titleAlignment: TitleAlignments.Centered, Title = "GetLibTypeName_NewCreate")]
            [HorizontalGroup("总组")]
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createText = "--创建新的Library库--";

            [InfoBox("请修改一下文件名否则会分配随机数字后缀", VisibleIf = "@!hasChange", InfoMessageType = InfoMessageType.Warning)]
            [VerticalGroup("总组/数据"), ESBackGround("yellow", 0.2f), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), OnValueChanged("OnValueChanged_ChangeHappen")]
            [LabelText("新建资源库名")]
            public string LibName = "新建Library库";
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
            [VerticalGroup("总组/数据"), LabelText("保存到文件夹"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public string FolderPath_ = "Assets/Resources/Data";
            public override ESWindowPageBase ES_Refresh()
            {
                LibName = GetLibTypeName_NewCreate();
                FolderPath_ = ESGlobalResSetting.Instance.Path_AllLibraryFolder + "/" + typeof(TLib).Name;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return base.ES_Refresh();

            }
            [HorizontalGroup("总组", width: 100)]
            [VerticalGroup("总组/按钮")]
            [PropertySpace(15)]
            [Button(ButtonHeight = 30, Name = "创建一个库", IconAlignment = IconAlignment.RightEdge), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void CreateNewLibrary()
            {
                var create = ESDesignUtility.SafeEditor.CreateSOAsset(typeof(TLib), FolderPath_, LibName, true, hasChange, beforeSave);
                void beforeSave(ScriptableObject so)
                {
                    if (so is TLib lib)
                    {
                        lib.SetSTR(lib.name);
                        lib.Desc = LibDESC;
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
            [HorizontalGroup("总组")]
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createText = "--编辑库--";

            private ReorderableList REForBooks;
            private ReorderableList REForPages;
            private TBook book;
            private TPage page;

            private ESAreaSolver area = new ESAreaSolver();
            private ESDragAtSolver dragAt = new ESDragAtSolver();
            [HorizontalGroup("总组/数据/本体与组", Width = 255)]
            [OnInspectorGUI]
            public void DrawSelfAndBooks()
            {
                SirenixEditorGUI.BeginBox();
                library.Name = EditorGUILayout.TextField("【库】命名", library.Name);
                EditorGUILayout.LabelField("↓库描述↓");
                library.Desc = EditorGUILayout.TextArea(library.Desc, GUILayout.Height(50));
                SirenixEditorGUI.EndBox();

                REForBooks.DoLayoutList();
            }
            [HorizontalGroup("总组/数据/本体与组", Width = 255, MarginLeft = 25)]
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
            [HorizontalGroup("总组/数据/本体与组", MinWidth = 400, MaxWidth = 700, MarginLeft = 25)]
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
                Debug.Log("重建");
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


        public void ApplyTemplateToMenuTree<T>(ESMenuTreeWindowAB<T> from, OdinMenuTree tree, string menuName)
        where T : ESMenuTreeWindowAB<T>
        {
            from.QuickBuildRootMenu(tree, menuName, ref page_root_Library, Sirenix.OdinInspector.SdfIconType.KeyboardFill);

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
        }
    }
}
