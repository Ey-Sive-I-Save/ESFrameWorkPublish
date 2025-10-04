using ES;
using ES.ES;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace ES
{
    public class ESResWindow : ESMenuTreeWindowAB<ESResWindow> //OdinMenuEditorWindow
    {
        [MenuItem("Tools/ES工具/ES资源窗口")]
        public static void TryOpenWindow()
        {
            OpenWindow();
        }


        #region 数据缓存
        public const string MenuNameForLibraryRoot = "资源库";
        public Page_Root_Library page_root_Library;
        public Page_Root_GlobalSetting page_root_GlobalSettings;
       // public Page_Root_Build page_index_Build;
        #endregion

        public override void ES_SaveData()
        {
            base.ES_SaveData();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        protected override void ES_OnBuildMenuTree(OdinMenuTree tree)
        {
            base.ES_OnBuildMenuTree(tree);
            PartPage_Library(tree);
            PartPage_Setting(tree);
            PartPage_Build(tree);
        }
        void PartPage_Library(OdinMenuTree tree)
        {
            QuickBuildRootMenu(tree, MenuNameForLibraryRoot, ref page_root_Library, Sirenix.OdinInspector.SdfIconType.KeyboardFill);
            var libs = ESEditorSO.SOS.GetGroup<ResLibrary>();
            if (libs != null)
            {
                List<string> strings = new List<string>(3);
                foreach (var i in libs)
                {
                    if (i != null)
                    {
                        while (strings.Contains(i.Name))
                        {
                            i.Name += "_r";
                            EditorUtility.SetDirty(i);
                            AssetDatabase.SaveAssets();
                        }
                        strings.Add(i.Name);
                        tree.Add(MenuNameForLibraryRoot + $"/库：{i.Name}", new Page_Index_Library() { library = i }.ES_Refresh(), SdfIconType.Cart);
                    }
                }
            }
        }

        void PartPage_Setting(OdinMenuTree tree)
        {
            QuickBuildRootMenu(tree, "设置与构建", ref page_root_GlobalSettings, EditorIcons.SettingsCog);
        }

        void PartPage_Build(OdinMenuTree tree)
        {
          //  QuickBuildRootMenu(tree, "构建", ref page_index_Build, SdfIconType.Building);
        }

        //Root_库
        public class Page_Root_Library : ESWindowPageBase
        {
            [Title("新建资源库！", "每个库可以获得专属的文件夹", bold: true, titleAlignment: TitleAlignments.Centered)]
            [HorizontalGroup("总组")]
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createText = "--创建新的资源库--";

            [InfoBox("请修改一下文件名否则会分配随机数字后缀", VisibleIf = "@!hasChange", InfoMessageType = InfoMessageType.Warning)]
            [VerticalGroup("总组/数据"), ESBackGround("yellow", 0.2f), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04"), OnValueChanged("OnValueChanged_ChangeHappen")]
            [LabelText("新建资源库名")]
            public string LibName = "新建资源库";
            [TextArea(3, 7)]
            [LabelText("描述")]
            public string LibDESC = "描述：这是一个做啥的库";
            private bool hasChange = false;
            private void OnValueChanged_ChangeHappen()
            {
                hasChange = true;
            }
            [FolderPath]
            [VerticalGroup("总组/数据"), LabelText("保存到文件夹"), Space(5), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
            public string FolderPath_ = "Assets/Resources/Data";
            public override ESWindowPageBase ES_Refresh()
            {
                FolderPath_ = ESGlobalResSetting.Instance.Path_ResLibraryFolder;
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return base.ES_Refresh();

            }
            [HorizontalGroup("总组", width: 100)]
            [VerticalGroup("总组/按钮")]
            [PropertySpace(15)]
            [Button(ButtonHeight = 30, Name = "新建资源库", IconAlignment = IconAlignment.RightEdge), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
            public void CreateNewLibrary()
            {
                var create = ESDesignUtility.SafeEditor.CreateSOAsset(typeof(ResLibrary), FolderPath_, LibName, true, hasChange, beforeSave);
                void beforeSave(ScriptableObject so)
                {
                    if (so is ResLibrary lib)
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

        }
        //Index_库
        public class Page_Index_Library : ESWindowPageBase
        {
            [HideInInspector]
            public ResLibrary library;
            [HorizontalGroup("总组")]
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createText = "--编辑库--";

            private ReorderableList REForBooks;
            private ReorderableList REForPages;
            private ResBook book;
            private ResPage page;

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
                    REForPages = new ReorderableList(book.pages, typeof(ResPage))
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
                        foreach (var i in gs)
                        {   
                            book.pages.Add(new ResPage() { Name = i.name, OB = i });
                            EditorUtility.SetDirty(library);
                            AssetDatabase.SaveAssets();
                        }
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

                if (page.Draw()) {
                    EditorUtility.SetDirty(library);
                    AssetDatabase.SaveAssets();
                };


            }
            public override ESWindowPageBase ES_Refresh()
            {
                createText = $"--编辑库【{library.GetSTR()}】--";
                REForBooks = new ReorderableList(library.Books, typeof(ResBook))
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

                    EditorGUI.LabelField(rect, "包含资源Book");

                };

                REForBooks.onChangedCallback += (ReorderableList list) =>
                {
                    Undo.RecordObject(library,"");
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
                    EditorGUI.LabelField(rect, "包含资源Page");
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

        public class Page_Root_GlobalSetting : ESWindowPageBase
        {
            /*
             直接绘制本体了哈 ESGlobalResSetting
             */
            private OdinEditor editor;
            [HorizontalGroup("设置"),PropertyOrder(-1)]
            [OnInspectorGUI]
            public void Draw()
            {
                editor ??= OdinEditor.CreateEditor(ESGlobalResSetting.Instance, typeof(OdinEditor)) as OdinEditor;
                if (editor != null)
                {
                    editor.DrawDefaultInspector();
                }
            }
            [PropertySpace(20,30)]
            [HorizontalGroup("总组")]
            [DisplayAsString(fontSize: 30, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/数据")]
            public string createText = "--构建流程--";
            private ReorderableList REForLibs;
            private List<ResLibrary> libs;
            [HorizontalGroup("总组/数据/分", Width = 355)]
            [OnInspectorGUI]
            public void DrawLibs()
            {
                SirenixEditorGUI.BeginBox();
                if (REForLibs != null) REForLibs.DoLayoutList();
                SirenixEditorGUI.EndBox();

            }
            public override ESWindowPageBase ES_Refresh()
            {
                libs = ESEditorSO.SOS.GetGroup<ResLibrary>();
                if (libs != null)
                {
                    REForLibs = new ReorderableList(libs, typeof(ResLibrary))
                    {
                        draggable = false,      // 允许拖拽排序
                        displayAdd = false, // 显示添加按钮
                        displayRemove = false, // 显示移除按钮
                    };
                    SetupCallBackLibs();
                }

                return base.ES_Refresh();

            }
            private static Color colorBL = Color.blue._WithAlpha(0.05f);
            private void SetupCallBackLibs()
            {
                REForLibs.drawHeaderCallback = (Rect rect) =>
                {

                    EditorGUI.LabelField(rect, "全部库");

                };


                REForLibs.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (libs == null) return;
                    var lib = libs[index];
                    var color = isActive ? Color.yellow : (isFocused ? Color.white : Color.white);

                    GUIHelper.PushColor(color);
                    EditorGUILayout.BeginHorizontal();
                    Rect left = new Rect(rect.x, rect.y, rect.width * 0.2f, rect.height);
                    lib.ContainsBuild = EditorGUI.ToggleLeft(left, "构建", lib.ContainsBuild);
                    Rect left2 = new Rect(rect.x + 0.25f * rect.width, rect.y, rect.width * 0.2f, rect.height);
                    lib.IsNet = EditorGUI.ToggleLeft(left2, "远端", lib.IsNet);
                    Rect right = new Rect(rect.x + 0.5f * rect.width, rect.y, rect.width * 0.45f, rect.height);
                    Rect rightOFF = right;
                    rightOFF.x -= 10;
                    SirenixEditorGUI.DrawBorders(rightOFF, (int)(rect.width * 0.45f), 0, (int)rect.height, 0, colorBL);
                    EditorGUI.LabelField(right, lib.Name._AddPreAndLast("【", "】"));

                    SirenixEditorGUI.DrawBorders(rect, 2);

                    EditorGUILayout.EndHorizontal();
                    GUIHelper.PopColor();
                };
            }

            [HorizontalGroup("总组/数据/分", MinWidth = 100)]
            [OnInspectorGUI()]
            public void Click_AssetPathingDetect()
            {
                if (GUILayout.Button("资源分析与去向生成", GUILayout.Height(50)))
                {
                    ESEditorHandle.AddSimpleHanldeTask(() =>
                    {
                        if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始-资源分析与去向生成", "开始分配资源去向，旧的手动地址可能失效！！", "直接来吧", "取消"))
                        {
                            ESEditorRes.Build_PrepareAnalyzeAssetsBundles();

                        }
                        else
                        {
                            Debug.LogWarning("放弃-<资源去向生成>");
                        }


                    });
                };
                SirenixEditorGUI.InfoMessageBox("资源去向生成用于生成全部资源的去向，在这之后资源才能被保证正确加载,否则可能出现冲突问题等");

            }

            //代码生成--废案
            /*[HorizontalGroup("总组/数据/分", MinWidth = 100)]
            [OnInspectorGUI()]
            public void Click_ABHelperCode()
            {
                if (GUILayout.Button("协助代码生成", GUILayout.Height(50)))
                {
                    ESEditorHandle.AddSimpleHanldeTask(() =>
                    {
                        if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始-生成协助代码", "旧的协助代码可能失效！！这可能相当危险", "直接来吧", "取消"))
                        {


                        }
                        else
                        {
                            Debug.LogWarning("放弃-<协助代码生成>");
                        }


                    });
                };

                SirenixEditorGUI.InfoMessageBox("手动输入代码过于麻烦，这里提供生成一个大型寻资源的代码生成协助，但是注意新的命名会导致的之前的错误");

            }*/

            [HorizontalGroup("总组/数据/分", MinWidth = 100)]
            [OnInspectorGUI()]
            public void Click_Build()
            {
                if (GUILayout.Button("构建AB与依赖", GUILayout.Height(50)))
                {
                    ESEditorHandle.AddSimpleHanldeTask(() =>
                    {
                        if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始-构建AB与依赖", "这是最重要的一步！！", "直接来吧", "取消"))
                        {

                            ESEditorRes.Build_BuildAB();

                            ESResMaster.JsonData_CreateHashAndDependence();
                        }
                        else
                        {
                            Debug.LogWarning("放弃-<构建AB与依赖>");
                        }


                    });
                };


                SirenixEditorGUI.InfoMessageBox("开始构建AB包和依赖关系这是发布模式下必须进行的一步");

            }

            [HorizontalGroup("总组/数据/分", MinWidth = 100)]
            [OnInspectorGUI()]
            public void Click_Server()
            {
                if (GUILayout.Button("上传到服务器", GUILayout.Height(50)))
                {
                    ESEditorHandle.AddSimpleHanldeTask(() =>
                    {
                        if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始-上传到服务器", "开始上传到服务器，需要保证已经完成基础配置并且支持！！", "直接来吧", "取消"))
                        {


                        }
                        else
                        {
                            Debug.LogWarning("放弃-<上传到服务器>");
                        }


                    });
                };


                SirenixEditorGUI.InfoMessageBox("经过配置后可用");

            }

            [HorizontalGroup("总组/数据/分", MinWidth = 100)]
            [OnInspectorGUI()]
            public void Click_ALL()
            {
                if (GUILayout.Button("一键完成", GUILayout.Height(50)))
                {
                    ESEditorHandle.AddSimpleHanldeTask(() =>
                    {
                        if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始-一键完成全部流程", "从资源去向分配开始完成全部工作", "直接来吧", "取消"))
                        {


                        }
                        else
                        {
                            Debug.LogWarning("放弃-<一键完成>");
                        }


                    });
                };


                SirenixEditorGUI.InfoMessageBox("前面的步骤一次性完成");

            }

        }

  
    }
}
