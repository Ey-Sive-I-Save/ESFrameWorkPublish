using ES;
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
    [InitializeOnLoad]
    public class ESResWindow : ESMenuTreeWindowAB<ESResWindow> //OdinMenuEditorWindow
    {
        static ESResWindow()
        {
            ESAssetReferEditorBridge.OpenRegistryPage = OpenAndSelectAssetPage;
        }

        private static void OpenAndSelectAssetPage(ESAssetPage page)
        {
            if (page == null)
                return;

            OpenWindow();
            EditorApplication.delayCall += () =>
            {
                if (menuTree == null)
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
        }

        [MenuItem(MenuItemPathDefine.RESOURCE_WINDOW_PATH, false, 0)]
        public static void TryOpenWindow()
        {
            OpenWindow();
        }

        [MenuItem(MenuItemPathDefine.QUICK_WINDOWS_PATH + "资产管理窗口", false, 0)]
        public static void TryOpenWindowFromQuickWindows()
        {
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
            EnsureConsumerConfiguration();
            menuTemplate.ApplyTemplateToMenuTree(this, tree, MenuNameForLibraryRoot);
        }

        private static void EnsureConsumerConfiguration()
        {
            List<ESAssetLibraryConsumer> consumers = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibraryConsumer>()
                ?.Where(item => item != null)
                .ToList() ?? new List<ESAssetLibraryConsumer>();
            bool changed = false;

            if (consumers.Count == 0)
            {
                var consumer = ScriptableObject.CreateInstance<ESAssetLibraryConsumer>();
                consumer.Name = "DefaultConsumer";
                consumer.Desc = "自动创建的默认资源消费入口。";
                consumer.IsTotalConsumer = true;
                consumer.Channel = "default";
                consumer.EnsureStableIdentity();
                List<ESAssetLibrary> libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>();
                if (libraries != null)
                    consumer.ConsumerLibFolders.AddRange(libraries.Where(item => item != null && item.ContainsBuild));

                string basePath = ESGlobalEditorDefaultConfi.Instance.Path_AllLibraryFolder_;
                if (!AssetDatabase.IsValidFolder(basePath))
                    ESDesignUtility.SafeEditor.Quick_CreateAssetFolder(basePath);
                string consumerFolder = basePath + "/Consumer";
                if (!AssetDatabase.IsValidFolder(consumerFolder))
                    AssetDatabase.CreateFolder(basePath, "Consumer");
                string path = AssetDatabase.GenerateUniqueAssetPath(consumerFolder + "/DefaultConsumer.asset");
                AssetDatabase.CreateAsset(consumer, path);
                consumers.Add(consumer);
                changed = true;
                Debug.Log("[ESResWindow] 未发现 Consumer，已自动创建 DefaultConsumer 并设为总入口。");
            }

            if (!consumers.Any(item => item.IsTotalConsumer))
            {
                Undo.RecordObject(consumers[0], "Assign Default Total Consumer");
                consumers[0].IsTotalConsumer = true;
                EditorUtility.SetDirty(consumers[0]);
                changed = true;
            }

            foreach (ESAssetLibraryConsumer consumer in consumers)
            {
                bool consumerChanged = consumer.EnsureStableIdentity();
                if (!consumerChanged) continue;
                EditorUtility.SetDirty(consumer);
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();
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
            private const string ResourcePipelineTaskKey = "ES.ResourcePipeline";
            /*
             直接绘制本体了哈 ESGlobalResSetting
             */
            private OdinEditor editor;
            [HorizontalGroup("设置"), PropertyOrder(-1)]
            [OnInspectorGUI]
            public void Draw()
            {
                editor ??= OdinEditor.CreateEditor(ESGlobalResSetting.Instance, typeof(OdinEditor)) as OdinEditor;
                if (editor != null)
                {
                    editor.DrawDefaultInspector();
                }
            }

            public override void OnPageDisable()
            {
                base.OnPageDisable();
                if (editor != null)
                {
                    UnityEngine.Object.DestroyImmediate(editor);
                    editor = null;
                }
            }
            [PropertySpace(12, 18)]
            // 根组先纵向排标题和内容；只有“内容”子组需要左右分栏。
            [VerticalGroup("总组")]
            [DisplayAsString(fontSize: 24, Alignment = TextAlignment.Center), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
            [VerticalGroup("总组/标题")]
            public string createText = "--构建流程--";
            private ReorderableList reorderableListForLibraries;
            private List<ESAssetLibrary> libraries;
            // “内容”是横向容器：左侧库列表，右侧依次显示四个流程步骤。
            // 子组必须使用不同路径，不能让 HorizontalGroup 与 VerticalGroup 共用同一路径。
            [HorizontalGroup("总组/内容")]
            [HorizontalGroup("总组/内容/库", Width = 285)]
            [OnInspectorGUI]
            public void DrawLibs()
            {
                SirenixEditorGUI.BeginBox();

                if (reorderableListForLibraries != null) reorderableListForLibraries.DoLayoutList();
                SirenixEditorGUI.EndBox();

            }
            public override ESWindowPageBase ES_Refresh()
            {
                libraries = ESEditorSO.SOS.GetNewGroupOfType<ESAssetLibrary>();
                if (libraries != null)
                {
                    reorderableListForLibraries = new ReorderableList(libraries, typeof(ESAssetLibrary))
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
                reorderableListForLibraries.drawHeaderCallback = (Rect rect) =>
                {

                    EditorGUI.LabelField(rect, "全部库");


                };


                reorderableListForLibraries.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (libraries == null) return;
                    var lib = libraries[index];
                    if (lib == null) return;
                    var color = isActive ? Color.yellow : (isFocused ? Color.white : Color.white);

                    GUIHelper.PushColor(color);
                    EditorGUILayout.BeginHorizontal();
                    Rect left = new Rect(rect.x, rect.y, rect.width * 0.2f, rect.height);
                    bool containsBuild = EditorGUI.ToggleLeft(left, "参与构建", lib.ContainsBuild);
                    if (containsBuild != lib.ContainsBuild)
                    {
                        Undo.RecordObject(lib, "Change Library Build Inclusion");
                        lib.ContainsBuild = containsBuild;
                        EditorUtility.SetDirty(lib);
                    }
                    Rect right = new Rect(rect.x + 0.22f * rect.width, rect.y, rect.width * 0.73f, rect.height);
                    Rect rightOFF = right;
                    rightOFF.x -= 10;
                    SirenixEditorGUI.DrawBorders(rightOFF, (int)(rect.width * 0.73f), 0, (int)rect.height, 0, colorBL);
                    EditorGUI.LabelField(right, lib.Name._AddPreAndLast("【", "】"));

                    SirenixEditorGUI.DrawBorders(rect, 2);

                    EditorGUILayout.EndHorizontal();
                    GUIHelper.PopColor();
                };
            }

            [HorizontalGroup("总组/内容/操作")]
            [OnInspectorGUI()]
            public void AnalyzeAndAssignAssetPaths()
            {
                bool pipelineBusy = ESEditorHandle.IsSimpleTaskKeyActive(ResourcePipelineTaskKey) || ESEditorHandle.IsLongTaskKeyActive(ResourcePipelineTaskKey);
                EditorGUI.BeginDisabledGroup(pipelineBusy);
                if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "1. 烘焙引用", GUILayout.Height(42)))
                {
                    ESEditorHandle.AddSimpleHandleTask(() =>
                    {
                        if (ESDesignUtility.SafeEditor.Wrap_DisplayDialog("开始-烘焙资产引用", "只分析资产身份与引用关系，不修改 AB 标签。", "开始", "取消"))
                        {
                            ESAssetReferenceBaker.Bake();
                        }
                        else
                        {
                            Debug.LogWarning("放弃-<资源去向生成>");
                        }


                    }, key: ResourcePipelineTaskKey);
                }
                EditorGUI.EndDisabledGroup();
                ;
                SirenixEditorGUI.InfoMessageBox("生成 Catalog 与引用图；不改标签。");

            }

            [HorizontalGroup("总组/内容/操作")]
            [OnInspectorGUI()]
            public void BuildAssetBundlesAndDependencies()
            {
                bool pipelineBusy = ESEditorHandle.IsSimpleTaskKeyActive(ResourcePipelineTaskKey) || ESEditorHandle.IsLongTaskKeyActive(ResourcePipelineTaskKey);
                EditorGUI.BeginDisabledGroup(pipelineBusy);
                if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "2. 规划并标记", GUILayout.Height(42)))
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
                EditorGUI.EndDisabledGroup();
                ;


                SirenixEditorGUI.InfoMessageBox("生成计划并写入 ES 管理的 AB 标签。");

            }

            [HorizontalGroup("总组/内容/操作")]
            [OnInspectorGUI()]
            public void Click_Server()
            {
                bool pipelineBusy = ESEditorHandle.IsSimpleTaskKeyActive(ResourcePipelineTaskKey) || ESEditorHandle.IsLongTaskKeyActive(ResourcePipelineTaskKey);
                EditorGUI.BeginDisabledGroup(pipelineBusy);
                if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "3. 构建资源包", GUILayout.Height(42)))
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
                EditorGUI.EndDisabledGroup();
                ;


                SirenixEditorGUI.InfoMessageBox("执行 Unity AB 构建，输出到暂存目录。");

            }

            [HorizontalGroup("总组/内容/操作")]
            [OnInspectorGUI()]
            public void Click_ALL()
            {
                bool pipelineBusy = ESEditorHandle.IsSimpleTaskKeyActive(ResourcePipelineTaskKey) || ESEditorHandle.IsLongTaskKeyActive(ResourcePipelineTaskKey);
                EditorGUI.BeginDisabledGroup(pipelineBusy);
                if (GUILayout.Button(pipelineBusy ? "任务执行中…" : "4. 发布资源包", GUILayout.Height(42)))
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
                EditorGUI.EndDisabledGroup();
                ;


                SirenixEditorGUI.InfoMessageBox("校验并发布，最后写入根清单。");

            }

        }
    }
}
