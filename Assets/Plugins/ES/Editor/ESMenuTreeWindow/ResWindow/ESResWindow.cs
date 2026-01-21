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
    public class ESResWindow : ESMenuTreeWindowAB<ESResWindow> //OdinMenuEditorWindow
    {
        [MenuItem(MenuItemPathDefine.EDITOR_TOOLS_PATH + "ES资源窗口", false, 4)]
        public static void TryOpenWindow()
        {
            OpenWindow();
        }


        #region 数据缓存
        public const string MenuNameForLibraryRoot = "资源库";
        
        public ESLibraryWindowMenuTemplate<ResLibrary,ResBook,ResPage> menuTemplate=new ESLibraryWindowMenuTemplate<ResLibrary,ResBook,ResPage>();
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
            menuTemplate.ApplyTemplateToMenuTree(this,tree,MenuNameForLibraryRoot);
        }

        void PartPage_Setting(OdinMenuTree tree)
        {
            QuickBuildRootMenu(tree, "设置与构建", ref page_root_GlobalSettings, EditorIcons.SettingsCog);
        }

        void PartPage_Build(OdinMenuTree tree)
        {
          //  QuickBuildRootMenu(tree, "构建", ref page_index_Build, SdfIconType.Building);
        }

        [Title("全局设置与构建", "配置整体资源路径与构建选项", bold: true, titleAlignment: TitleAlignments.Centered)]

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
                libs = ESEditorSO.SOS.GetNewGroupOfType<ResLibrary>();
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
