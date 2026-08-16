#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using ES.EditorInternal;
namespace ES
{
    public enum ESWorldWorkbenchModule : byte
    {
        Foundation,
        Overview,
        Terrain,
        Material,
        Vegetation,
        Prefab,
        Navigation,
        WaterWeather,
        Streaming,
        Collision,
        UGC
    }

    /// <summary>
    /// ES 世界构建工作台：统一承载地图、地形、对象散布、导航、环境、流式和 UGC 构建配置。
    /// 它是内容工作台底座，不替代资源管线或运行时 WorldDomain。
    /// </summary>
    public sealed class ESWorldBuilderWorkbenchWindow : ESWorkbenchWindowBase<ESWorldBuilderWorkbenchWindow, ESWorldMapAsset, ESWorldWorkbenchModule>
    {
        private sealed class WorldPersistenceAdapter : IESWorkbenchPersistenceAdapter<ESWorldMapAsset>
        {
            public bool TrySave(ESWorldMapAsset asset, SerializedObject serializedObject, out string message)
            {
                ESWorldMapSaveResult result = ESWorldMapAuthoringUtility.Save(asset, serializedObject);
                message = result.success ? (result.contentChanged ? "地图资产已保存，内容签名已更新。" : "地图资产已保存。") : result.error;
                return result.success;
            }
        }

        private static readonly IESWorkbenchPersistenceAdapter<ESWorldMapAsset> PersistenceAdapter = new WorldPersistenceAdapter();
        internal enum BuildStage { NotStarted, Preflight, Pending, Succeeded, Failed, Cancelled }
        private BuildStage buildStage;
        private ESContentRegistrationResult lastRegistrationResult;
        private ESContentRegistrationResult collectPreviewResult;
        private string collectRequestId = string.Empty;
        private string bakeRequestId = string.Empty;
        private string bakeRunId = string.Empty;
        private ESWorldEditSession editSession;
        private ESWorldAuthoringTool authoringTool = ESWorldAuthoringTool.Select;
        private float authoringBrushHeight = 0.5f;
        private ESWorldMapAsset mapAsset => editSession?.Draft ?? ESWorkbench_Asset;
        internal ESWorldMapAsset ESWorld_Draft => mapAsset;
        private SerializedObject serializedAsset => ESWorkbench_SerializedAsset;
        protected override IESWorkbenchPersistenceAdapter<ESWorldMapAsset> ESWorkbench_PersistenceAdapter => PersistenceAdapter;
        protected override string ESWorkbench_WorkbenchId => "world";
        protected override bool ESWorkbench_IncludeDefaultViewports => false;
        protected override bool ESWorkbench_IncludeDefaultTools => false;
        protected override string ESWorkbench_ValidateMutation(
            ESWorkbenchMutationKind kind,
            ESWorkbenchSelection target,
            ESWorkbenchObjectDescriptor item)
        {
            string reason = base.ESWorkbench_ValidateMutation(kind, target, item);
            if (!string.IsNullOrEmpty(reason)) return reason;
            return kind == ESWorkbenchMutationKind.Create && ESWorkbench_IsHierarchyLocked("world.map")
                ? "世界根节点已锁定，不能创建新的作者对象。"
                : string.Empty;
        }
        protected override void ESWorkbench_RegisterDomainContributions()
        {
            EnsureContributionsRegistered();
        }
        protected override void ESWorkbench_BeforeLoadContributions()
        {
            ESWorkbench_RestoreBoundAsset();
            TryBindSelection();
        }
        protected override ESWorldMapAsset ESWorkbench_ResolveEditingAsset(ESWorldMapAsset asset)
        {
            editSession?.Dispose();
            editSession = ESWorldEditSession.Open(asset);
            return editSession?.Draft;
        }

        protected override void ESWorkbench_OnAssetBound(ESWorldMapAsset asset)
        {
            base.ESWorkbench_OnAssetBound(asset);
            ESWorkbench_ClearDirty();
            if (editSession?.IsDirty == true)
                ESWorkbench_MarkDirty("world.recovered-draft", ESWorkbenchDirtyFlags.Authoring);
            ESWorldMapDefinition definition = editSession?.Draft?.Definition;
            if (definition == null) return;
            ESWorkbench_Selection.Select(new ESWorkbenchSelection(
                "world.map",
                "world.map",
                asset,
                definition));
        }

        protected override void ESWorkbench_Save()
        {
            if (editSession == null)
            {
                ESWorkbench_SetStatus("未绑定世界地图草稿。", MessageType.Warning);
                return;
            }
            ESWorldEditCommitResult result = editSession.TryCommit();
            ESWorkbench_SetStatus(result.message,
                result.success ? MessageType.Info : result.conflict ? MessageType.Warning : MessageType.Error);
            if (!result.success)
            {
                if (result.conflict) ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.DataChanged);
                return;
            }
            ESWorkbench_ClearDirty();
            serializedAsset?.Update();
            ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.DataChanged);
        }

        protected override void ESWorkbench_OnHostCleanup()
        {
            base.ESWorkbench_OnHostCleanup();
            editSession?.Dispose();
            editSession = null;
        }
        protected override List<ESWorldWorkbenchModule> ESWorkbench_DefaultModules => new List<ESWorldWorkbenchModule>
        {
            ESWorldWorkbenchModule.Foundation,
            ESWorldWorkbenchModule.Overview,
            ESWorldWorkbenchModule.Terrain,
            ESWorldWorkbenchModule.Material,
            ESWorldWorkbenchModule.Vegetation,
            ESWorldWorkbenchModule.Prefab,
            ESWorldWorkbenchModule.Navigation,
            ESWorldWorkbenchModule.WaterWeather,
            ESWorldWorkbenchModule.Streaming,
            ESWorldWorkbenchModule.Collision,
            ESWorldWorkbenchModule.UGC
        };

        protected override void ESWorkbench_AdjustModules(List<ESWorldWorkbenchModule> modules)
        {
            // World 默认保留全部标准模块；派生 World 工作台可在这里 Remove、Add 或 Sort。
        }

        protected override string ESWorkbench_GetModuleDisplayName(ESWorldWorkbenchModule module)
        {
            switch (module)
            {
                case ESWorldWorkbenchModule.Foundation: return "基础作者能力";
                case ESWorldWorkbenchModule.Overview: return "总览";
                case ESWorldWorkbenchModule.Terrain: return "地形";
                case ESWorldWorkbenchModule.Material: return "材质层";
                case ESWorldWorkbenchModule.Vegetation: return "植被 / 细节";
                case ESWorldWorkbenchModule.Prefab: return "Prefab 散布";
                case ESWorldWorkbenchModule.Navigation: return "导航 / AI";
                case ESWorldWorkbenchModule.WaterWeather: return "水体 / 天气";
                case ESWorldWorkbenchModule.Streaming: return "地形块流式";
                case ESWorldWorkbenchModule.Collision: return "碰撞 / 物理";
                case ESWorldWorkbenchModule.UGC: return "构建 / UGC";
                default: return module.ToString();
            }
        }

        [MenuItem("【ES】/内容制作/环境/世界构建工作台", false, 120)]
        public static void Open()
        {
            ESWorldBuilderWorkbenchWindow window = GetWindow<ESWorldBuilderWorkbenchWindow>();
            window.titleContent = new GUIContent("ES 世界构建工作台");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
            if (Selection.activeObject is ESWorldMapAsset selected && selected != window.ESWorkbench_Asset)
                window.ESWorkbench_BindAsset(selected);
        }

        [MenuItem("【ES】/内容制作/环境/世界配置与构建", false, 122)]
        public static void OpenConfigurationWorkbench()
        {
            Open();
        }

        public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("ES 世界构建工作台", "统一编辑地图、地形、对象散布、导航、环境和 UGC 构建配置。");
        protected override string ESWindow_Subtitle => "ES 专属世界内容底板 · 地图到 UGC 构建配置";
        protected override Vector2 ESWindow_MinSize => new Vector2(980f, 620f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1280f, 820f);
        protected override string ESWindow_PageStableId => "world.builder-workbench";
        protected override string ESWindow_PageTitle => "世界构建工作台";
        protected override string ESWindow_PageKeywords => "地图 地形 材质 植被 Prefab 导航 水体 天气 流式 碰撞 构建 UGC";

        protected override void ESWindow_BuildPageActions(ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction("world-builder.sample-registration", "加载示例注册", "向当前地图资产补齐可见的中文工作台样例配置。", _ => InjectSampleContent()).WithUnityIcon("Refresh").WithPriority(110));
            actions.Add(new ESMenuTreePageAction("world-builder.validate", "验证地图", "执行地图定义与 UGC 配额校验。", _ => ValidateMap()).WithUnityIcon("TestPassed").WithPriority(100));
            actions.Add(new ESMenuTreePageAction("world-builder.save", "保存资产", "保存当前地图资产。", _ => ESWorkbench_Save()).WithUnityIcon("SaveAs").WithPriority(90));
            actions.Add(new ESMenuTreePageAction("world-builder.locate", "定位资产", "在 Project 窗口定位当前地图资产。", _ => ESWorkbench_Locate()).WithUnityIcon("Project").When(() => mapAsset != null).WithPriority(80));
        }

        protected override void ESWindow_OnHostEnable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            base.ESWindow_OnHostEnable();
        }

        protected override void ESWindow_OnHostDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            base.ESWindow_OnHostDisable();
        }

        internal void InitializeForTest() => ESWindow_OnHostEnable();
        internal void DisableForTest() => ESWindow_OnHostDisable();
        internal int ContributionLoadCountForTest => ESWorkbench_ContributionLoadCount;

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            DrawToolbar();
            if (mapAsset == null)
            {
                ESWorkbench_DrawEmptyState(
                    "还没有绑定世界地图",
                    "选择已有 ESWorldMapAsset，或创建一张新的地图定义。工作台不会自动修改正式 Scene。",
                    "创建地图资产",
                    CreateAsset);
                return;
            }

            if (serializedAsset == null || serializedAsset.targetObject != mapAsset)
                ESWorkbench_BindAsset(mapAsset);
            serializedAsset.Update();

            ESWorldMapDefinition topDefinition = mapAsset.Definition;
            ESWorkbench_DrawHero(
                string.IsNullOrWhiteSpace(topDefinition.mapId) ? "未命名世界" : topDefinition.mapId,
                "统一世界内容工作台 · 地图、地形、散布、导航与 UGC 构建",
                ESWorkbench_IsDirty ? "有未保存变更" : "已保存");
            using (new EditorGUILayout.HorizontalScope())
            {
                ESWorkbench_DrawMetric("来源模式", GetSourceModeDisplayName(topDefinition.sourceMode));
                ESWorkbench_DrawMetric("地形后端", GetTerrainModeDisplayName(topDefinition.terrainMode));
                ESWorkbench_DrawMetric("区域 / POI", (topDefinition.regions?.Count ?? 0) + " / " + (topDefinition.pois?.Count ?? 0));
                ESWorkbench_DrawMetric("UGC Prefab", topDefinition.ugcLimits == null ? "未配置" : topDefinition.ugcLimits.maxPrefabInstances.ToString("N0"), ESStatusKind.Ready);
            }

            ESWorkbench_DrawStandardLayout(DrawInspectorPanel);
            bool serializedChanged = serializedAsset.hasModifiedProperties;
            serializedAsset.ApplyModifiedProperties();
            if (serializedChanged) ESWorkbench_MarkSelectedPageDirty();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(ESEditorPresentation.ToolbarStyle))
            {
                ESWorldMapAsset next = (ESWorldMapAsset)EditorGUILayout.ObjectField("地图资产", mapAsset, typeof(ESWorldMapAsset), false, GUILayout.MinWidth(230f), GUILayout.MaxWidth(420f));
                if (next != mapAsset) ESWorkbench_BindAsset(next);
                if (GUILayout.Button("创建", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(58f))) CreateAsset();
                if (GUILayout.Button("填充默认", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(78f))) InitializeDefaults();
                if (GUILayout.Button("加载示例", ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(78f))) InjectSampleContent();
                GUILayout.FlexibleSpace();
                GUILayout.Label(ESWorkbench_Status, ESEditorPresentation.MetaStyle, GUILayout.MaxWidth(360f));
            }
        }

        private void DrawInspectorPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width < 1120f ? 215f : 255f)))
            {
                GUILayout.Label("工作台状态", ESEditorPresentation.HeaderStyle);
                EditorGUILayout.HelpBox(ESWorkbench_Status, ESWorkbench_StatusType);
                ESWorldMapDefinition definition = mapAsset.Definition;
                if (definition != null)
                {
                    EditorGUILayout.LabelField("地图 ID", definition.mapId);
                    EditorGUILayout.LabelField("来源", GetSourceModeDisplayName(definition.sourceMode));
                    EditorGUILayout.LabelField("地形后端", GetTerrainModeDisplayName(definition.terrainMode));
                    EditorGUILayout.LabelField("范围", (definition.worldMax - definition.worldMin).ToString());
                    EditorGUILayout.LabelField("高度场", definition.heightfield == null ? "缺失" : definition.heightfield.width + " x " + definition.heightfield.height);
                    EditorGUILayout.LabelField("示例注册", HasSampleRegistration(definition) ? "已加载" : "未加载");
                    EditorGUILayout.LabelField("材质 / 植被 / 散布", (definition.materialLayers?.Count ?? 0) + " / " + (definition.vegetationLayers?.Count ?? 0) + " / " + (definition.scatterLayers?.Count ?? 0));
                    EditorGUILayout.LabelField("对话入口", (definition.dialoguePlacements?.Count ?? 0).ToString());
                    EditorGUILayout.LabelField("UGC 预算", definition.ugcLimits == null ? "缺失" : definition.ugcLimits.maxPrefabInstances.ToString("N0") + " Prefab 实例");
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("打开对话工作台", GUILayout.Height(28f))) ESWorldDialogueWorkbenchWindow.OpenFor(mapAsset, this);
                if (GUILayout.Button("打开空间编辑器", GUILayout.Height(28f))) ESWorldMapSpaceEditorWindow.OpenFor(mapAsset);
                if (GUILayout.Button("加载中文示例注册", GUILayout.Height(28f))) InjectSampleContent();
                if (GUILayout.Button("验证地图", GUILayout.Height(28f))) ValidateMap();
                if (GUILayout.Button("保存资产", GUILayout.Height(28f))) ESWorkbench_Save();
            }
        }

        private void DrawOverview()
        {
            GUILayout.Label("世界构建总览", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox("这里是 ES 的世界内容底板：同一张地图资产统一承载地形、材质、植被、Prefab、导航、环境、流式、碰撞和 UGC 构建配置。", MessageType.Info);
            DrawProperty("definition.mapId");
            DrawProperty("definition.contentVersion");
            DrawProperty("definition.contentHash");
            DrawProperty("definition.sourceMode");
            DrawProperty("definition.terrainMode");
            DrawProperty("definition.worldMin");
            DrawProperty("definition.worldMax");
            DrawProperty("definition.chunkSize");
            DrawProperty("definition.dialoguePlacements");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("当前注入的工作台贡献", ESEditorPresentation.HeaderStyle);
                if (ESWorkbench_ContributionEntries.Count == 0)
                {
                    EditorGUILayout.HelpBox("尚未注入模块贡献。请重新打开工作台或检查贡献注册诊断。", MessageType.Warning);
                    return;
                }
                for (int i = 0; i < ESWorkbench_ContributionEntries.Count; i++)
                {
                    ESWorkbenchContributionEntry entry = ESWorkbench_ContributionEntries[i];
                    EditorGUILayout.LabelField(
                        entry.DisplayName,
                        entry.Category + " · " + entry.Owner + " · " + entry.ContributionId);
                }
            }
        }

        private static string GetSourceModeDisplayName(ESWorldMapSourceMode mode)
        {
            switch (mode)
            {
                case ESWorldMapSourceMode.Scene: return "自建子场景";
                case ESWorldMapSourceMode.Prefab: return "自建预制件";
                default: return "纯随机生成";
            }
        }

        private static string GetTerrainModeDisplayName(ESWorldMapTerrainMode mode)
        {
            switch (mode)
            {
                case ESWorldMapTerrainMode.Heightfield: return "ES 高度场";
                case ESWorldMapTerrainMode.Voxel: return "Voxel 扩展后端";
                case ESWorldMapTerrainMode.None: return "无地形";
                default: return "Unity Terrain";
            }
        }

        private void DrawTerrain()
        {
            GUILayout.Label("真实地形后端", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox("默认 Unity Terrain。Heightfield 是 ES 数据后端和降级路径；Voxel 保留为可选扩展。", MessageType.Info);
            DrawProperty("definition.terrainMode");
            DrawProperty("definition.terrainAssetKey");
            DrawProperty("definition.heightmapAssetKey");
            DrawProperty("definition.terrainMaterialSetKey");
            DrawProperty("definition.terrainHeightScale");
            DrawProperty("definition.maxWalkableSlope");
            DrawProperty("definition.defaultSurfaceTag");
            DrawProperty("definition.heightfield", true);
        }

        private void DrawMaterialLayers()
        {
            GUILayout.Label("地形材质层", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox("从 Project 拖入 Material，先执行注册预检，再提交到 ES 资源库并绑定到材质层。预览结果不会替代正式注册。", MessageType.Info);
            DrawProperty("definition.materialLayers", true);
            if (mapAsset == null || mapAsset.Definition == null || mapAsset.Definition.materialLayers == null)
                return;
            ESWorldMapMaterialLayer layer = FindMaterialLayer("material.grass");
            if (layer == null)
            {
                EditorGUILayout.HelpBox("当前地图没有 ground 材质层，请先加载示例注册或在列表中新增。", MessageType.Warning);
                return;
            }
            int index = mapAsset.Definition.materialLayers.IndexOf(layer);
            if (!ESWorkbench_TryGetContributionSlot("world.material." + index, out ESWorkbenchAssetRegistrationSlot slot))
                slot = ESWorldMapWorkbenchSlots.Material(GetResourceLibraryPath(), index);
            ESWorkbench_DrawRegistrationSlot(slot);
            EditorGUILayout.LabelField("当前绑定 Key", string.IsNullOrEmpty(layer.materialKey) ? "未绑定" : layer.materialKey);
        }

        private void DrawVegetationLayers()
        {
            GUILayout.Label("植被 / 细节层", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox("拖入植被 Prefab 作为散布资源源。提交后只写入稳定 prefabSetKey，不会把临时预览对象误认为正式场景内容。", MessageType.Info);
            DrawProperty("definition.vegetationLayers", true);
            ESWorldMapVegetationLayer layer = FindVegetationLayer("vegetation.trees");
            if (layer == null)
            {
                EditorGUILayout.HelpBox("当前地图没有 vegetation 植被层，请先加载示例注册或在列表中新增。", MessageType.Warning);
                return;
            }
            int index = mapAsset.Definition.vegetationLayers.IndexOf(layer);
            if (!ESWorkbench_TryGetContributionSlot("world.vegetation." + index, out ESWorkbenchAssetRegistrationSlot slot))
                slot = ESWorldMapWorkbenchSlots.Vegetation(GetResourceLibraryPath(), index);
            ESWorkbench_DrawRegistrationSlot(slot);
            EditorGUILayout.LabelField("当前绑定 Key", string.IsNullOrEmpty(layer.prefabSetKey) ? "未绑定" : layer.prefabSetKey);
        }

        private void DrawPrefabLayers()
        {
            GUILayout.Label("Prefab 散布层", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox("拖入可散布 Prefab，提交注册后绑定到 scatter.landmarks。后续空间编辑器可在 PreviewScene 中拖入、移动、旋转和删除，再由明确动作落盘。", MessageType.Info);
            DrawProperty("definition.scatterLayers", true);
            ESWorldMapPrefabScatterLayer layer = FindScatterLayer("scatter.landmarks");
            if (layer == null)
            {
                EditorGUILayout.HelpBox("当前地图没有 landmarks 散布层，请先加载示例注册或在列表中新增。", MessageType.Warning);
                return;
            }
            int index = mapAsset.Definition.scatterLayers.IndexOf(layer);
            if (!ESWorkbench_TryGetContributionSlot("world.scatter." + index, out ESWorkbenchAssetRegistrationSlot slot))
                slot = ESWorldMapWorkbenchSlots.Scatter(GetResourceLibraryPath(), index);
            ESWorkbench_DrawRegistrationSlot(slot);
            EditorGUILayout.LabelField("当前绑定 Key", string.IsNullOrEmpty(layer.prefabSetKey) ? "未绑定" : layer.prefabSetKey);
        }

        private ESWorldMapMaterialLayer FindMaterialLayer(string layerId)
        {
            for (int i = 0; i < mapAsset.Definition.materialLayers.Count; i++)
                if (mapAsset.Definition.materialLayers[i] != null && mapAsset.Definition.materialLayers[i].layerId == layerId)
                    return mapAsset.Definition.materialLayers[i];
            return mapAsset.Definition.materialLayers.Count > 0 ? mapAsset.Definition.materialLayers[0] : null;
        }

        private ESWorldMapVegetationLayer FindVegetationLayer(string layerId)
        {
            for (int i = 0; i < mapAsset.Definition.vegetationLayers.Count; i++)
                if (mapAsset.Definition.vegetationLayers[i] != null && mapAsset.Definition.vegetationLayers[i].layerId == layerId)
                    return mapAsset.Definition.vegetationLayers[i];
            return mapAsset.Definition.vegetationLayers.Count > 0 ? mapAsset.Definition.vegetationLayers[0] : null;
        }

        private ESWorldMapPrefabScatterLayer FindScatterLayer(string layerId)
        {
            for (int i = 0; i < mapAsset.Definition.scatterLayers.Count; i++)
                if (mapAsset.Definition.scatterLayers[i] != null && mapAsset.Definition.scatterLayers[i].layerId == layerId)
                    return mapAsset.Definition.scatterLayers[i];
            return mapAsset.Definition.scatterLayers.Count > 0 ? mapAsset.Definition.scatterLayers[0] : null;
        }

        private string GetResourceLibraryPath()
        {
            return mapAsset == null || mapAsset.Definition == null || mapAsset.Definition.build == null
                ? string.Empty
                : mapAsset.Definition.build.resourceLibraryPath;
        }

        private void DrawBuildUgc()
        {
            GUILayout.Label("构建与 UGC 安全", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.HelpBox("构建配置负责生成运行时数据；UGC 配额负责限制尺寸、层数、Prefab 数量、采样量、构建时长和资源体积。", MessageType.Info);
            DrawProperty("definition.build", true);
            DrawProperty("definition.ugcLimits", true);
            EditorGUILayout.Space(8f);
            GUILayout.Label("阶段流程", ESEditorPresentation.HeaderStyle);
            EditorGUILayout.LabelField("当前阶段", buildStage.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                ESWorkbench_DrawActionButton("1. 验证", "验证地图定义与 UGC 配额", ValidateBuildStage, true, true);
                ESWorkbench_DrawActionButton("2. 收集预检", "只读检查资源注册输入", PreviewCollect);
                ESWorkbench_DrawActionButton("3. Bake 预检", "只读检查资源 Bake 请求", PreviewBake);
            }
            GUI.enabled = collectPreviewResult != null && collectPreviewResult.success && !string.IsNullOrEmpty(collectRequestId);
            using (new EditorGUILayout.HorizontalScope())
            {
                ESWorkbench_DrawActionButton("提交资源收集", "提交当前预检对应的资源注册请求", CommitCollect, GUI.enabled, true);
                GUI.enabled = !string.IsNullOrEmpty(bakeRequestId) && string.IsNullOrEmpty(bakeRunId);
                ESWorkbench_DrawActionButton("提交受管构建", "向现有资源管线提交当前预检对应的 Bake 请求", CommitBake, GUI.enabled, true);
                GUI.enabled = true;
                ESWorkbench_DrawActionButton("刷新状态", "查询当前 Bake Run 状态", RefreshBakeStatus, true);
            }
            EditorGUILayout.Space(6f);
            ESWorkbench_DrawActionButton(
                "生成正式 Terrain / Scene / NavMesh",
                "显式执行带未保存场景保护、备份、重读验证和失败回滚的正式 World 输出事务",
                BuildFormalWorldOutputs,
                mapAsset != null,
                true);
            GUI.enabled = true;
            if (lastRegistrationResult != null)
            {
                EditorGUILayout.HelpBox(lastRegistrationResult.status + "：" + lastRegistrationResult.message, lastRegistrationResult.success ? MessageType.Info : MessageType.Error);
                if (!string.IsNullOrEmpty(lastRegistrationResult.runId)) EditorGUILayout.LabelField("RunId", lastRegistrationResult.runId);
            }
        }

        private void ValidateBuildStage()
        {
            buildStage = BuildStage.Preflight;
            ValidateMap();
            if (ESWorkbench_StatusType == MessageType.Error) { buildStage = BuildStage.Failed; return; }
            buildStage = BuildStage.Succeeded;
        }

        private void PreviewCollect()
        {
            if (!ValidateMapForAction()) return;
            string libraryPath = mapAsset.Definition.build.resourceLibraryPath;
            if (string.IsNullOrWhiteSpace(libraryPath))
            {
                ESWorkbench_SetStatus("资源收集预检需要先在 BuildSettings.resourceLibraryPath 配置 ESAssetLibrary 项目路径。", MessageType.Warning);
                buildStage = BuildStage.Failed;
                return;
            }
            string assetPath = AssetDatabase.GetAssetPath(mapAsset);
            ESContentRegistrationResult result = ESWorkbenchContentRegistration.Preview(
                mapAsset, mapAsset.Definition.build.outputKey, libraryPath);
            lastRegistrationResult = result;
            collectPreviewResult = result;
            collectRequestId = result?.requestId ?? string.Empty;
            buildStage = result != null && result.success ? BuildStage.Succeeded : BuildStage.Failed;
            ESWorkbench_RecordTask(
                "world.collect." + (string.IsNullOrEmpty(collectRequestId) ? "preflight" : collectRequestId),
                buildStage.ToString(), result?.message ?? "资源收集预检无结果。",
                AssetDatabase.GetAssetPath(mapAsset));
            ESWorkbench_SetStatus(result?.message ?? "资源收集预检无结果。", result != null && result.success ? MessageType.Info : MessageType.Error);
        }

        private void CommitCollect()
        {
            if (collectPreviewResult == null || string.IsNullOrEmpty(collectRequestId))
            {
                ESWorkbench_SetStatus("请先执行资源收集预检。", MessageType.Warning);
                return;
            }
            ESWorldMapBuildSettings build = mapAsset.Definition.build;
            lastRegistrationResult = ESWorkbenchContentRegistration.Commit(
                mapAsset, build.outputKey, build.resourceLibraryPath, collectPreviewResult);
            ESWorkbench_RecordTask(
                "world.collect." + collectRequestId,
                lastRegistrationResult != null && lastRegistrationResult.success ? "Succeeded" : "Failed",
                lastRegistrationResult?.message ?? "资源收集提交失败。",
                AssetDatabase.GetAssetPath(mapAsset));
            if (lastRegistrationResult == null || !lastRegistrationResult.success)
                ESWorkbench_SetStatus(lastRegistrationResult?.message ?? "资源收集提交失败。", MessageType.Error);
            else
                ESWorkbench_SetStatus("资源收集已提交；后续 Bake 仍需单独执行并查询结果。", MessageType.Info);
        }

        private void PreviewBake()
        {
            if (!ValidateMapForAction()) return;
            lastRegistrationResult = ESWorkbench_PreviewBake();
            bakeRequestId = lastRegistrationResult?.requestId ?? string.Empty;
            buildStage = lastRegistrationResult != null && lastRegistrationResult.success ? BuildStage.Preflight : BuildStage.Failed;
            ESWorkbench_RecordTask(
                "world.bake." + (string.IsNullOrEmpty(bakeRequestId) ? "preflight" : bakeRequestId),
                buildStage.ToString(), lastRegistrationResult?.message ?? "Bake 预检无结果。");
            ESWorkbench_SetStatus(lastRegistrationResult?.message ?? "Bake 预检无结果。", lastRegistrationResult != null && lastRegistrationResult.success ? MessageType.Info : MessageType.Error);
        }

        private void CommitBake()
        {
            if (string.IsNullOrEmpty(bakeRequestId)) { ESWorkbench_SetStatus("请先执行 Bake 预检。", MessageType.Warning); return; }
            lastRegistrationResult = ESWorkbench_CommitBake(bakeRequestId);
            bakeRunId = lastRegistrationResult?.runId ?? string.Empty;
            buildStage = lastRegistrationResult != null && lastRegistrationResult.success ? BuildStage.Pending : BuildStage.Failed;
            ESWorkbench_RecordTask(
                "world.bake." + bakeRequestId,
                buildStage.ToString(), lastRegistrationResult?.message ?? "Bake 提交无结果。",
                bakeRunId);
            ESWorkbench_SetStatus(lastRegistrationResult?.message ?? "Bake 提交无结果。", lastRegistrationResult != null && lastRegistrationResult.success ? MessageType.Info : MessageType.Error);
        }

        private void RefreshBakeStatus()
        {
            if (string.IsNullOrEmpty(bakeRunId)) { ESWorkbench_SetStatus("当前没有可查询的 Bake Run。", MessageType.Info); return; }
            lastRegistrationResult = ESWorkbench_QueryBake(bakeRequestId, bakeRunId);
            if (lastRegistrationResult != null)
            {
                buildStage = ResolveBuildStage(lastRegistrationResult);
                ESWorkbench_RecordTask(
                    "world.bake." + bakeRequestId,
                    buildStage.ToString(), lastRegistrationResult.message, bakeRunId);
            }
        }

        internal static BuildStage ResolveBuildStage(ESContentRegistrationResult result)
        {
            if (result == null) return BuildStage.Failed;
            if (result.success || string.Equals(result.status, "Succeeded", StringComparison.OrdinalIgnoreCase))
                return BuildStage.Succeeded;
            if (string.Equals(result.status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return BuildStage.Cancelled;
            if (string.Equals(result.status, "Queued", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.status, "Running", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.status, "Pending", StringComparison.OrdinalIgnoreCase))
                return BuildStage.Pending;
            return BuildStage.Failed;
        }

        private bool ValidateMapForAction()
        {
            if (mapAsset == null) { ESWorkbench_SetStatus("未绑定地图资产。", MessageType.Warning); return false; }
            if (!mapAsset.Validate(out string error)) { ESWorkbench_SetStatus(error, MessageType.Error); buildStage = BuildStage.Failed; return false; }
            return true;
        }

        private void DrawObject(string title, string propertyName)
        {
            GUILayout.Label(title, ESEditorPresentation.HeaderStyle);
            DrawProperty("definition." + propertyName, true);
        }

        private void DrawList(string title, string propertyName, string elementType)
        {
            GUILayout.Label(title, ESEditorPresentation.HeaderStyle);
            DrawProperty("definition." + propertyName, true);
            EditorGUILayout.HelpBox("当前列表已注册到地图资产。sample.* Key 是中文示例绑定入口；资源未解析前不会伪造 TerrainLayer、Prefab 实例或植被对象。", MessageType.None);
            if (mapAsset != null && HasSampleRegistration(mapAsset.Definition))
                EditorGUILayout.LabelField("注册状态", "已加载示例配置 · 等待资源绑定/构建");
        }

        private void DrawProperty(string path, bool includeChildren = false)
        {
            if (serializedAsset == null) return;
            SerializedProperty property = serializedAsset.FindProperty(path);
            if (property != null) EditorGUILayout.PropertyField(property, includeChildren);
        }

        private void TryBindSelection()
        {
            ESWorldMapAsset selected = Selection.activeObject as ESWorldMapAsset;
            if (selected != null && selected != ESWorkbench_Asset) ESWorkbench_BindAsset(selected);
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeObject is ESWorldMapAsset selected && selected != ESWorkbench_Asset)
                ESWorkbench_BindAsset(selected);
            Repaint();
        }

        private void InitializeDefaults()
        {
            if (mapAsset == null) { CreateAsset(); if (mapAsset == null) return; }
            if (ESWorkbench_IsHierarchyLocked("world.map"))
            {
                ESWorkbench_SetStatus("世界根节点已锁定，不能填充默认配置。", MessageType.Warning);
                return;
            }
            Undo.RecordObject(mapAsset, "初始化 ES 世界构建配置");
            ESWorldMapDefinition definition = mapAsset.Definition;
            EnsureDefinitionContainers(definition);
            definition.mapId = string.IsNullOrWhiteSpace(definition.mapId)
                ? (ESWorkbench_Asset != null ? ESWorkbench_Asset.name : mapAsset.name)
                : definition.mapId;
            definition.contentHash = string.IsNullOrWhiteSpace(definition.contentHash) ? "editor-draft" : definition.contentHash;
            definition.sourceMode = ESWorldMapSourceMode.Procedural;
            definition.generatorKey = string.IsNullOrWhiteSpace(definition.generatorKey) ? "es.editor.grid" : definition.generatorKey;
            definition.terrainMode = ESWorldMapTerrainMode.UnityTerrain;
            definition.worldMin = Vector2.zero;
            definition.worldMax = new Vector2(256f, 256f);
            definition.chunkSize = 64f;
            definition.heightfield.EnsureSamples();
            definition.build.outputKey = string.IsNullOrWhiteSpace(definition.build.outputKey) ? "world.map.baked" : definition.build.outputKey;
            definition.build.runtimeOutputPath = string.IsNullOrWhiteSpace(definition.build.runtimeOutputPath) ? "ES/ResourcePipeline/Baked/world" : definition.build.runtimeOutputPath;
            ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
            serializedAsset?.Update();
            ESWorkbench_SetStatus("默认世界构建配置已初始化；示例内容仍需显式加载。", MessageType.Info);
            ESWorkbench_MarkDirty("world.defaults", ESWorkbenchDirtyFlags.Authoring);
        }

        private void InjectSampleContent()
        {
            if (mapAsset == null) { ESWorkbench_SetStatus("请先选择或创建一张地图资产。", MessageType.Warning); return; }
            if (ESWorkbench_IsHierarchyLocked("world.map"))
            {
                ESWorkbench_SetStatus("世界根节点已锁定，不能注入示例内容。", MessageType.Warning);
                return;
            }
            Undo.RecordObject(mapAsset, "加载 ES 世界工作台中文示例注册");
            EnsureDefinitionBaseline(mapAsset, ESWorkbench_Asset != null ? ESWorkbench_Asset.name : null);
            EnsureDefinitionContainers(mapAsset.Definition);
            PopulateSampleContent(mapAsset.Definition);
            ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
            ESWorkbench_SetStatus("中文示例注册已加载：材质、植被、Prefab、导航、天气、水体、流式、碰撞、区域和 POI 均已注入。", MessageType.Info);
            ESWorkbench_MarkDirty("world.sample-registration", ESWorkbenchDirtyFlags.Authoring);
        }

        private static bool HasSampleRegistration(ESWorldMapDefinition definition)
        {
            if (definition == null) return false;
            bool hasMaterial = definition.materialLayers != null && definition.materialLayers.Exists(item => item != null && item.layerId == "material.grass");
            bool hasVegetation = definition.vegetationLayers != null && definition.vegetationLayers.Exists(item => item != null && item.layerId == "vegetation.trees");
            bool hasScatter = definition.scatterLayers != null && definition.scatterLayers.Exists(item => item != null && item.layerId == "scatter.landmarks");
            bool hasPoi = definition.pois != null && definition.pois.Exists(item => item != null && item.poiId == "poi.spawn");
            return hasMaterial && hasVegetation && hasScatter && hasPoi;
        }

        private static void EnsureDefinitionContainers(ESWorldMapDefinition definition)
        {
            if (definition == null) return;
            if (definition.surfaces == null) definition.surfaces = new List<ESWorldMapSurfaceDefinition>();
            if (definition.materialLayers == null) definition.materialLayers = new List<ESWorldMapMaterialLayer>();
            if (definition.vegetationLayers == null) definition.vegetationLayers = new List<ESWorldMapVegetationLayer>();
            if (definition.scatterLayers == null) definition.scatterLayers = new List<ESWorldMapPrefabScatterLayer>();
            if (definition.navigation == null) definition.navigation = new ESWorldMapNavigationSettings();
            if (definition.waterWeather == null) definition.waterWeather = new ESWorldMapWaterWeatherSettings();
            if (definition.streaming == null) definition.streaming = new ESWorldMapStreamingSettings();
            if (definition.collision == null) definition.collision = new ESWorldMapCollisionSettings();
            if (definition.build == null) definition.build = new ESWorldMapBuildSettings();
            if (definition.ugcLimits == null) definition.ugcLimits = new ESWorldMapUgcLimits();
            if (definition.heightfield == null) definition.heightfield = new ESWorldMapHeightfield();
            if (definition.spaceTemplate == null) definition.spaceTemplate = new ESWorldMapSpaceTemplate();
            if (definition.regions == null) definition.regions = new List<ESWorldMapRegionDefinition>();
            if (definition.pois == null) definition.pois = new List<ESWorldMapPoiDefinition>();
        }

        private static void EnsureDefinitionBaseline(ESWorldMapAsset asset, string defaultMapId = null)
        {
            if (asset == null || asset.Definition == null) return;
            ESWorldMapDefinition definition = asset.Definition;
            definition.mapId = string.IsNullOrWhiteSpace(definition.mapId)
                ? (string.IsNullOrWhiteSpace(defaultMapId) ? asset.name : defaultMapId)
                : definition.mapId;
            definition.contentHash = string.IsNullOrWhiteSpace(definition.contentHash) ? "editor-draft" : definition.contentHash;
            definition.generatorKey = string.IsNullOrWhiteSpace(definition.generatorKey) ? "es.editor.grid" : definition.generatorKey;
            definition.generatorVersion = Mathf.Max(1, definition.generatorVersion);
            definition.terrainMode = definition.terrainMode == ESWorldMapTerrainMode.None ? ESWorldMapTerrainMode.UnityTerrain : definition.terrainMode;
            if (definition.worldMax.x <= definition.worldMin.x || definition.worldMax.y <= definition.worldMin.y)
            {
                definition.worldMin = Vector2.zero;
                definition.worldMax = new Vector2(256f, 256f);
            }
            if (definition.chunkSize <= 0f) definition.chunkSize = 64f;
            EnsureDefinitionContainers(definition);
            definition.heightfield.EnsureSamples();
            definition.build.outputKey = string.IsNullOrWhiteSpace(definition.build.outputKey) ? "world.map.baked" : definition.build.outputKey;
            definition.build.runtimeOutputPath = string.IsNullOrWhiteSpace(definition.build.runtimeOutputPath) ? "ES/ResourcePipeline/Baked/world" : definition.build.runtimeOutputPath;
        }

        /// <summary>
        /// 为新地图提供可见的跨模块样例数据。该方法只写入地图资产定义，不创建
        /// Prefab 实例、NavMesh、TerrainLayer 或水体 GameObject；这些 Key 是后续
        /// 资源绑定和构建阶段的明确入口。重复执行按稳定 ID 去重，不覆盖用户已有配置。
        /// </summary>
        private static void PopulateSampleContent(ESWorldMapDefinition definition)
        {
            if (definition == null) return;

            EnsureSurface(definition.surfaces, "Ground", "地面");
            EnsureSurface(definition.surfaces, "Rock", "岩石");
            EnsureSurface(definition.surfaces, "Mud", "泥地");

            EnsureMaterialLayer(definition.materialLayers, new ESWorldMapMaterialLayer
            {
                layerId = "material.grass",
                materialKey = "sample.material.grass",
                surfaceTag = "Ground",
                minHeight = 0f,
                maxHeight = 0.58f,
                maxSlope = 34f
            });
            EnsureMaterialLayer(definition.materialLayers, new ESWorldMapMaterialLayer
            {
                layerId = "material.rock",
                materialKey = "sample.material.rock",
                surfaceTag = "Rock",
                minHeight = 0.42f,
                maxHeight = 1f,
                maxSlope = 90f
            });
            EnsureMaterialLayer(definition.materialLayers, new ESWorldMapMaterialLayer
            {
                layerId = "material.mud",
                materialKey = "sample.material.mud",
                surfaceTag = "Mud",
                minHeight = 0.2f,
                maxHeight = 0.72f,
                maxSlope = 18f
            });

            EnsureVegetationLayer(definition.vegetationLayers, new ESWorldMapVegetationLayer
            {
                layerId = "vegetation.trees",
                prefabSetKey = "sample.prefabset.trees",
                biomeTag = "Forest",
                density = 180,
                minScale = 0.85f,
                maxScale = 1.2f,
                alignToTerrain = true
            });
            EnsureVegetationLayer(definition.vegetationLayers, new ESWorldMapVegetationLayer
            {
                layerId = "vegetation.grass",
                prefabSetKey = "sample.prefabset.grass",
                biomeTag = "Meadow",
                density = 420,
                minScale = 0.75f,
                maxScale = 1.1f,
                alignToTerrain = true
            });

            EnsureScatterLayer(definition.scatterLayers, new ESWorldMapPrefabScatterLayer
            {
                layerId = "scatter.landmarks",
                prefabSetKey = "sample.prefabset.landmarks",
                seed = definition.seed + 101,
                count = 12,
                minSpacing = 10f,
                maxSlope = 28f
            });
            EnsureScatterLayer(definition.scatterLayers, new ESWorldMapPrefabScatterLayer
            {
                layerId = "scatter.rocks",
                prefabSetKey = "sample.prefabset.rocks",
                seed = definition.seed + 202,
                count = 36,
                minSpacing = 4f,
                maxSlope = 42f
            });

            definition.navigation.enabled = true;
            definition.navigation.bakeProfileKey = string.IsNullOrWhiteSpace(definition.navigation.bakeProfileKey) ? "sample.navmesh.default" : definition.navigation.bakeProfileKey;
            definition.navigation.maxSlope = Mathf.Clamp(definition.navigation.maxSlope, 0f, 45f);
            definition.waterWeather.waterEnabled = true;
            definition.waterWeather.waterProfileKey = string.IsNullOrWhiteSpace(definition.waterWeather.waterProfileKey) ? "sample.water.lake" : definition.waterWeather.waterProfileKey;
            definition.waterWeather.weatherEnabled = true;
            definition.waterWeather.weatherProfileKey = string.IsNullOrWhiteSpace(definition.waterWeather.weatherProfileKey) ? "sample.weather.day" : definition.waterWeather.weatherProfileKey;
            definition.waterWeather.ambientWetness = Mathf.Clamp01(definition.waterWeather.ambientWetness);
            definition.streaming.enabled = true;
            definition.streaming.chunkRadius = Mathf.Max(2, definition.streaming.chunkRadius);
            definition.streaming.maxLoadedChunks = Mathf.Max(16, definition.streaming.maxLoadedChunks);
            definition.streaming.loadCollisionFirst = true;
            definition.streaming.unloadFarChunks = true;
            definition.collision.terrainCollider = true;
            definition.collision.physicsMaterialEnabled = true;
            definition.collision.physicsMaterialKey = string.IsNullOrWhiteSpace(definition.collision.physicsMaterialKey) ? "sample.physics.ground" : definition.collision.physicsMaterialKey;
            definition.collision.generateTriggerVolume = true;

            if (definition.regions == null) definition.regions = new List<ESWorldMapRegionDefinition>();
            EnsureRegion(definition.regions, new ESWorldMapRegionDefinition
            {
                regionId = "region.spawn",
                displayName = "出生营地",
                semanticTag = "Spawn",
                min = new Vector2(16f, 16f),
                max = new Vector2(96f, 96f),
                priority = 10
            });
            EnsureRegion(definition.regions, new ESWorldMapRegionDefinition
            {
                regionId = "region.lake",
                displayName = "中央湖区",
                semanticTag = "Water",
                min = new Vector2(112f, 96f),
                max = new Vector2(224f, 208f),
                priority = 20
            });

            if (definition.pois == null) definition.pois = new List<ESWorldMapPoiDefinition>();
            EnsurePoi(definition.pois, new ESWorldMapPoiDefinition
            {
                poiId = "poi.spawn",
                displayName = "出生点",
                category = "Spawn",
                regionId = "region.spawn",
                position = new Vector2(48f, 48f),
                discoverable = true
            });
            EnsurePoi(definition.pois, new ESWorldMapPoiDefinition
            {
                poiId = "poi.lake",
                displayName = "湖心码头",
                category = "Landmark",
                regionId = "region.lake",
                position = new Vector2(168f, 152f),
                discoverable = true
            });

            if (definition.heightfield != null)
            {
                definition.heightfield.EnsureSamples();
                bool flat = true;
                float first = definition.heightfield.Get(0, 0);
                for (int y = 0; y < definition.heightfield.height && flat; y++)
                    for (int x = 0; x < definition.heightfield.width; x++)
                        if (Mathf.Abs(definition.heightfield.Get(x, y) - first) > 0.001f) { flat = false; break; }
                if (flat)
                {
                    for (int y = 0; y < definition.heightfield.height; y++)
                        for (int x = 0; x < definition.heightfield.width; x++)
                        {
                            float u = x / (float)(definition.heightfield.width - 1);
                            float v = y / (float)(definition.heightfield.height - 1);
                            float hill = Mathf.Clamp01(0.28f + 0.22f * Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI));
                            definition.heightfield.Set(x, y, hill);
                        }
                }
            }
        }

        private static void EnsureSurface(List<ESWorldMapSurfaceDefinition> list, string id, string displayName)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].surfaceTag == id) return;
            list.Add(new ESWorldMapSurfaceDefinition { surfaceTag = id, displayName = displayName });
        }

        private static void EnsureMaterialLayer(List<ESWorldMapMaterialLayer> list, ESWorldMapMaterialLayer value)
        {
            if (list == null || value == null) return;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].layerId == value.layerId) return;
            list.Add(value);
        }

        private static void EnsureVegetationLayer(List<ESWorldMapVegetationLayer> list, ESWorldMapVegetationLayer value)
        {
            if (list == null || value == null) return;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].layerId == value.layerId) return;
            list.Add(value);
        }

        private static void EnsureScatterLayer(List<ESWorldMapPrefabScatterLayer> list, ESWorldMapPrefabScatterLayer value)
        {
            if (list == null || value == null) return;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].layerId == value.layerId) return;
            list.Add(value);
        }

        private static void EnsureRegion(List<ESWorldMapRegionDefinition> list, ESWorldMapRegionDefinition value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].regionId == value.regionId) return;
            list.Add(value);
        }

        private static void EnsurePoi(List<ESWorldMapPoiDefinition> list, ESWorldMapPoiDefinition value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].poiId == value.poiId) return;
            list.Add(value);
        }

        private static void EnsureContributionsRegistered()
        {
            RegisterAuthoringContribution();
            RegisterPageContribution("overview", "总览", "地图身份与整体状态", ESWorldWorkbenchModule.Overview, ESWorkbenchContributionCategory.General, window => window.DrawOverview, ESWorkbenchDirtyFlags.Authoring);
            RegisterPageContribution("terrain", "地形", "Unity Terrain / 高度场", ESWorldWorkbenchModule.Terrain, ESWorkbenchContributionCategory.Terrain, window => window.DrawTerrain, ESWorkbenchDirtyFlags.Authoring);
            RegisterPageContribution("materials", "地形材质层", "材质与地表规则", ESWorldWorkbenchModule.Material, ESWorkbenchContributionCategory.Material, window => window.DrawMaterialLayers, ESWorkbenchDirtyFlags.Authoring,
                (context, window) =>
                {
                    if (window.mapAsset?.Definition?.materialLayers == null) return;
                    ESWorldMapMaterialLayer layer = window.FindMaterialLayer("material.grass");
                    if (layer != null) context.RegisterAssetSlot(ESWorldMapWorkbenchSlots.Material(window.GetResourceLibraryPath(), window.mapAsset.Definition.materialLayers.IndexOf(layer)));
                });
            RegisterPageContribution("vegetation", "植被 / 细节", "植被层与生物群落", ESWorldWorkbenchModule.Vegetation, ESWorkbenchContributionCategory.Vegetation, window => window.DrawVegetationLayers, ESWorkbenchDirtyFlags.Authoring,
                (context, window) =>
                {
                    if (window.mapAsset?.Definition?.vegetationLayers == null) return;
                    ESWorldMapVegetationLayer layer = window.FindVegetationLayer("vegetation.trees");
                    if (layer != null) context.RegisterAssetSlot(ESWorldMapWorkbenchSlots.Vegetation(window.GetResourceLibraryPath(), window.mapAsset.Definition.vegetationLayers.IndexOf(layer)));
                });
            RegisterPageContribution("prefabs", "Prefab 散布", "批量对象布局", ESWorldWorkbenchModule.Prefab, ESWorkbenchContributionCategory.Prefab, window => window.DrawPrefabLayers, ESWorkbenchDirtyFlags.Authoring,
                (context, window) =>
                {
                    if (window.mapAsset?.Definition?.scatterLayers == null) return;
                    ESWorldMapPrefabScatterLayer layer = window.FindScatterLayer("scatter.landmarks");
                    if (layer != null) context.RegisterAssetSlot(ESWorldMapWorkbenchSlots.Scatter(window.GetResourceLibraryPath(), window.mapAsset.Definition.scatterLayers.IndexOf(layer)));
                });
            RegisterPageContribution("navigation", "导航 / AI 烘焙", "可行走坡度与烘焙参数", ESWorldWorkbenchModule.Navigation, ESWorkbenchContributionCategory.Navigation, window => () => window.DrawObject("导航 / AI 烘焙", "navigation"), ESWorkbenchDirtyFlags.Authoring);
            RegisterPageContribution("water-weather", "水体 / 天气", "环境与湿度", ESWorldWorkbenchModule.WaterWeather, ESWorkbenchContributionCategory.WaterWeather, window => () => window.DrawObject("水体 / 天气", "waterWeather"), ESWorkbenchDirtyFlags.Authoring);
            RegisterPageContribution("streaming", "地形块流式", "区块半径与加载策略", ESWorldWorkbenchModule.Streaming, ESWorkbenchContributionCategory.Streaming, window => () => window.DrawObject("地形块流式", "streaming"), ESWorkbenchDirtyFlags.Authoring);
            RegisterPageContribution("collision", "碰撞 / 物理", "碰撞器与物理材质", ESWorldWorkbenchModule.Collision, ESWorkbenchContributionCategory.Collision, window => () => window.DrawObject("碰撞 / 物理", "collision"), ESWorkbenchDirtyFlags.Authoring);
            RegisterPageContribution("build-ugc", "构建 / UGC", "导出、预算和安全配额", ESWorldWorkbenchModule.UGC, ESWorkbenchContributionCategory.UGC, window => window.DrawBuildUgc, ESWorkbenchDirtyFlags.Build);
        }

        private static void RegisterAuthoringContribution()
        {
            ESWorkbenchContributionRegistry<ESWorldWorkbenchModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<ESWorldWorkbenchModule>(
                    "world",
                    "authoring-core",
                    "世界可视作者能力",
                    ESWorldWorkbenchModule.Foundation,
                    ESWorkbenchContributionCategory.General,
                    context =>
                    {
                        ESWorldBuilderWorkbenchWindow window = context.Window as ESWorldBuilderWorkbenchWindow;
                        if (window == null) throw new InvalidOperationException("World 作者能力缺少窗口上下文。");
                        context.RegisterPresentation(new ESWorkbenchHostPresentationDescriptor(
                            "world.presentation",
                            "ES 世界工作台",
                            "世界地图",
                            "世界视图",
                            "二维地图、三维世界与游戏构图视图",
                            "世界检查器"));
                        context.RegisterBottomPanel(new ESWorkbenchBottomPanelDescriptor(
                            "world.status",
                            "世界状态",
                            _ => new ESWorkbenchBottomPanelContent(window.CreateWorldStatusPanel()),
                            "当前世界草稿、事务与构建状态",
                            450,
                            _ => window.mapAsset != null));
                        window.RegisterWorldAuthoringCapabilities(context);
                        return null;
                    },
                    "注册世界视口、对象库、层级、Inspector、工具和快捷命令。",
                    "ES.World",
                    1000,
                    1),
                out string message);
            if (!string.IsNullOrEmpty(message) && !message.StartsWith("忽略旧版本", StringComparison.Ordinal))
                Debug.LogWarning("[ESWorkbench] " + message);
        }

        private void RegisterWorldAuthoringCapabilities(ESWorkbenchContributionContext context)
        {
            context.RegisterViewport(new ESWorkbenchViewportDescriptor(
                "world.canvas-2d", "2D 地图", ESWorkbenchViewportKind.Canvas2D,
                viewportContext => new ESWorldWorkbenchViewportAdapter(this, viewportContext, ESWorkbenchViewportKind.Canvas2D),
                "高度场、区域、POI 与 Prefab 放置俯视作者区", priority: 100));
            context.RegisterViewport(new ESWorkbenchViewportDescriptor(
                "world.scene-3d", "3D 世界", ESWorkbenchViewportKind.Scene3D,
                viewportContext => new ESWorldWorkbenchViewportAdapter(this, viewportContext, ESWorkbenchViewportKind.Scene3D),
                "Terrain 与 Prefab 三维草稿视口", priority: 90));
            context.RegisterViewport(new ESWorkbenchViewportDescriptor(
                "world.game", "游戏视图", ESWorkbenchViewportKind.Game,
                viewportContext => new ESWorldWorkbenchViewportAdapter(this, viewportContext, ESWorkbenchViewportKind.Game),
                "使用运行时透视构图检查世界草稿；该视图不写入作者数据", priority: 80));

            RegisterWorldTools(context);
            RegisterWorldCommands(context);
            context.RegisterObjectSource(new ESWorkbenchCollectionSource<ESWorkbenchObjectDescriptor>(
                "world.registered-assets", _ => QueryWorldPalette(), 100));
            context.RegisterHierarchySource(new ESWorkbenchCollectionSource<ESWorkbenchHierarchyDescriptor>(
                "world.authoring-hierarchy", _ => QueryWorldHierarchy(), 100));
            context.RegisterIssueSource(new ESWorkbenchCollectionSource<ESWorkbenchIssueDescriptor>(
                "world.production-issues", _ => QueryWorldIssues(), 100));
            context.RegisterAuthoringAdapter(CreateWorldAuthoringAdapter());
            context.RegisterInspector(new ESWorkbenchInspectorDescriptor(
                "world.context-inspector",
                selection => selection != null && selection.Kind.StartsWith("world.", StringComparison.Ordinal),
                (actions, selection) => CreateWorldInspector(selection),
                1000));
        }

        private VisualElement CreateWorldStatusPanel()
        {
            VisualElement root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            string assetName = ESWorkbench_Asset == null ? "未绑定" : ESWorkbench_Asset.name;
            string draftState = editSession == null ? "未创建草稿"
                : editSession.IsDirty ? "存在未提交修改" : "草稿与基线一致";
            string buildState = GetBuildStageDisplayName(buildStage);
            root.Add(new Label("世界地图：" + assetName));
            root.Add(new Label("作者事务：" + draftState));
            root.Add(new Label("构建状态：" + buildState));
            return root;
        }

        private static string GetBuildStageDisplayName(BuildStage stage)
        {
            switch (stage)
            {
                case BuildStage.Preflight: return "预检中";
                case BuildStage.Pending: return "处理中";
                case BuildStage.Succeeded: return "已成功";
                case BuildStage.Failed: return "已失败";
                case BuildStage.Cancelled: return "已取消";
                default: return "未开始";
            }
        }

        private void RegisterWorldTools(ESWorkbenchContributionContext context)
        {
            context.RegisterTool(CreateWorldTool("world.select", "选择", ESWorldAuthoringTool.Select, KeyCode.Q, 500));
            context.RegisterTool(CreateWorldTool("world.terrain", "地形笔刷", ESWorldAuthoringTool.Terrain, KeyCode.W, 490));
            context.RegisterTool(CreateWorldTool("world.region", "区域", ESWorldAuthoringTool.Region, KeyCode.E, 480));
            context.RegisterTool(CreateWorldTool("world.poi", "POI", ESWorldAuthoringTool.Poi, KeyCode.R, 470));
            context.RegisterTool(CreateWorldTool("world.prefab", "Prefab 放置", ESWorldAuthoringTool.Prefab, KeyCode.T, 460));
        }

        private ESWorkbenchToolDescriptor CreateWorldTool(
            string id,
            string title,
            ESWorldAuthoringTool tool,
            KeyCode shortcut,
            int priority)
        {
            return new ESWorkbenchToolDescriptor(id, title, actions =>
            {
                authoringTool = tool;
                actions.SetStatus("当前世界工具：" + title, MessageType.Info);
            }, title, priority: priority, isAvailable: _ => mapAsset != null,
                shortcut: new ESWorkbenchShortcut(shortcut));
        }

        private void RegisterWorldCommands(ESWorkbenchContributionContext context)
        {
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.validate", "验证", _ => ValidateMap(), "验证当前世界草稿",
                EditorGUIUtility.IconContent("d_TestPassed").image, 500,
                new ESWorkbenchShortcut(KeyCode.V, EventModifiers.Control),
                _ => mapAsset != null));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.reload-source", "重载正式资产", _ => ReloadWorldFromSource(),
                "放弃当前草稿并从正式地图建立新基线",
                EditorGUIUtility.IconContent("d_Refresh").image, 495,
                canExecute: _ => editSession != null));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.revert", "回退草稿", _ => RevertWorldDraft(), "回退到当前编辑会话基线",
                EditorGUIUtility.IconContent("d_Refresh").image, 490,
                canExecute: _ => editSession != null && editSession.IsDirty
                    && !ESWorkbench_IsHierarchyLocked("world.map")));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.brush", "笔刷", actions => OpenBrushPopup(actions), "设置世界地形笔刷参数",
                EditorGUIUtility.IconContent("d_TerrainInspector.TerrainToolSculpt").image, 480,
                canExecute: _ => mapAsset != null));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.build-preflight", "构建预检", _ => PreviewBake(), "验证构建输入并生成可提交的 Bake 请求",
                EditorGUIUtility.IconContent("d_PreMatCube").image, 470,
                canExecute: _ => mapAsset != null));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.build", "构建", _ => CommitBake(), "提交已经通过预检的 World Bake 请求",
                EditorGUIUtility.IconContent("d_BuildSettings.Editor.Small").image, 460,
                canExecute: _ => mapAsset != null && !string.IsNullOrEmpty(bakeRequestId)));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.formal-output", "正式输出", _ => BuildFormalWorldOutputs(),
                "生成带事务保护的 TerrainData、Scene、碰撞和 NavMeshData",
                EditorGUIUtility.IconContent("d_SceneAsset Icon").image, 450,
                canExecute: _ => mapAsset != null));
        }

        private void BuildFormalWorldOutputs()
        {
            if (!ValidateMapForAction()) return;
            if (ESWorkbench_IsDirty)
            {
                ESWorkbench_Save();
                if (ESWorkbench_IsDirty)
                {
                    ESWorkbench_SetStatus("世界草稿未成功保存，正式输出已取消。", MessageType.Error);
                    return;
                }
            }
            ESWorldMapAsset source = ESWorkbench_Asset;
            ESWorldMapDefinition definition = source?.Definition;
            if (definition == null)
            {
                ESWorkbench_SetStatus("正式世界资产无效。", MessageType.Error);
                return;
            }
            string safeMapId = ResolveSafeFileName(definition.mapId);
            string root = "Assets/ESWorldGenerated/" + safeMapId;
            string terrainPath = string.IsNullOrWhiteSpace(definition.terrainDataAssetPath)
                ? root + "/" + safeMapId + "_Terrain.asset"
                : definition.terrainDataAssetPath.Replace('\\', '/');
            string scenePath = string.IsNullOrWhiteSpace(definition.build?.formalSceneAssetPath)
                ? root + "/" + safeMapId + ".unity"
                : definition.build.formalSceneAssetPath.Replace('\\', '/');
            if (!EditorUtility.DisplayDialog(
                    "提交正式 World 输出",
                    "将生成或更新：\n" + terrainPath + "\n" + scenePath
                    + "\n\n启用导航时还会生成 NavMeshData。已有目标会先备份，失败会回滚；存在未保存 Scene 时事务拒绝启动。",
                    "确认生成",
                    "取消"))
                return;

            string taskId = "world.formal-output." + safeMapId;
            ESWorkbench_RecordTask(taskId, "Running", "正在生成正式 World 输出。", scenePath);
            bool success = ESWorldMapTerrainEditorFacade.TryBakePersistent(
                source, terrainPath, scenePath, out string error);
            if (success)
            {
                ESWorkbench_RecordTask(taskId, "Succeeded",
                    "TerrainData、Scene、碰撞与导航输出已提交并重读验证。", scenePath);
                ESWorkbench_RecordLog("正式 World 输出完成：" + scenePath);
                ReloadWorldFromSource();
                ESWorkbench_SetStatus("正式 World 输出已完成并通过重读验证。", MessageType.Info);
            }
            else
            {
                ESWorkbench_RecordTask(taskId, "Failed", error, scenePath);
                ESWorkbench_RecordLog(error, MessageType.Error);
                ESWorkbench_SetStatus(error, MessageType.Error);
            }
        }

        private static string ResolveSafeFileName(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "World" : value.Trim();
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            var result = new System.Text.StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
                result.Append(Array.IndexOf(invalid, source[i]) >= 0 ? '_' : source[i]);
            return result.ToString();
        }

        private ESWorkbenchAuthoringAdapterDescriptor CreateWorldAuthoringAdapter()
        {
            return new ESWorkbenchAuthoringAdapterDescriptor(
                "world.authoring",
                IsMutableWorldSelection,
                item => item?.Source is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab),
                CreateWorldPlacementMutation,
                MoveWorldSelectionMutation,
                DuplicateWorldSelectionMutation,
                DeleteWorldSelectionMutation,
                _ => editSession?.Draft == null
                    ? Array.Empty<UnityEngine.Object>()
                    : new UnityEngine.Object[] { editSession.Draft },
                null,
                1000,
                _ => editSession?.Draft?.Definition != null,
                canRotate: IsWorldPrefabSelection,
                rotate: RotateWorldSelectionMutation,
                canScale: IsWorldPrefabSelection,
                scale: ScaleWorldSelectionMutation);
        }

        private IEnumerable<ESWorkbenchObjectDescriptor> QueryWorldPalette()
        {
            IReadOnlyList<ESAssetPage> pages = ESAssetRegistry.Pages;
            for (int i = 0; i < pages.Count; i++)
            {
                ESAssetPage page = pages[i];
                if (!(page?.OB is GameObject prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab)) continue;
                string key = page.EffectiveStringKey;
                if (string.IsNullOrWhiteSpace(key)) key = page.AssetGuid;
                if (string.IsNullOrWhiteSpace(key)) continue;
                string category = string.IsNullOrWhiteSpace(page.SourceBook) ? page.Kind.ToString() : page.SourceBook;
                yield return new ESWorkbenchObjectDescriptor(
                    "world.asset." + key,
                    page.OB.name,
                    ResolveWorldAssetCategory(page, category),
                    page.OB,
                    page,
                    AssetPreview.GetAssetPreview(page.OB) ?? AssetPreview.GetMiniThumbnail(page.OB),
                    key,
                    100,
                    AssetDatabase.GetAssetPath(page.OB),
                    "Prefab");
            }
        }

        private static string ResolveWorldAssetCategory(ESAssetPage page, string fallback)
        {
            string source = ((page?.OB == null ? string.Empty : AssetDatabase.GetAssetPath(page.OB))
                + "/" + (page?.OB == null ? string.Empty : page.OB.name)
                + "/" + fallback).ToLowerInvariant();
            if (source.Contains("terrain") || source.Contains("ground") || source.Contains("rock")) return "环境/地形";
            if (source.Contains("building") || source.Contains("house") || source.Contains("wall") || source.Contains("architecture")) return "环境/建筑";
            if (source.Contains("tree") || source.Contains("grass") || source.Contains("plant") || source.Contains("vegetation")) return "环境/植被";
            if (source.Contains("character") || source.Contains("npc") || source.Contains("player")) return "角色";
            if (source.Contains("vfx") || source.Contains("effect") || source.Contains("particle")) return "特效";
            if (source.Contains("prop") || source.Contains("item") || source.Contains("furniture")) return "道具";
            return string.IsNullOrWhiteSpace(fallback) ? "其他" : "资源库/" + fallback;
        }

        internal IReadOnlyList<ESWorkbenchViewportStatusDescriptor> GetViewportStatusSnapshot(
            ESWorkbenchViewportKind kind)
        {
            ESWorldMapDefinition definition = mapAsset?.Definition;
            ESWorkbenchSelection selected = ESWorkbench_Selection.Current;
            ESWorkbenchHierarchyDescriptor hierarchyItem = selected == null ? null
                : ESWorkbench_Hierarchy.FirstOrDefault(value => value != null && value.ItemId == selected.StableId);
            string coordinates = hierarchyItem?.Spatial == null
                ? "--"
                : hierarchyItem.Spatial.Position.x.ToString("0.##") + ", "
                    + hierarchyItem.Spatial.Position.y.ToString("0.##") + ", "
                    + hierarchyItem.Spatial.Position.z.ToString("0.##");
            string camera = kind == ESWorkbenchViewportKind.Canvas2D ? "正交俯视"
                : kind == ESWorkbenchViewportKind.Game ? "运行时透视预览" : "透视作者相机";
            string gizmo = authoringTool.ToString();
            string collision = definition?.collision == null ? "未配置"
                : definition.collision.terrainCollider ? "Terrain 开" : "Terrain 关";
            string navigation = definition?.navigation == null ? "未配置"
                : definition.navigation.enabled
                    ? "启用 / 坡度 " + definition.navigation.maxSlope.ToString("0.#") + "°"
                    : "关闭";
            return new[]
            {
                new ESWorkbenchViewportStatusDescriptor("world.coordinates", "坐标", coordinates,
                    "当前层级选择的世界坐标", 500),
                new ESWorkbenchViewportStatusDescriptor("world.camera", "相机", camera,
                    "当前视口的相机投影", 400),
                new ESWorkbenchViewportStatusDescriptor("world.gizmo", "Gizmo", gizmo,
                    "当前世界作者工具", 300),
                new ESWorkbenchViewportStatusDescriptor("world.collision", "碰撞", collision,
                    "正式输出的 Terrain Collider 状态", 200),
                new ESWorkbenchViewportStatusDescriptor("world.navigation", "导航", navigation,
                    "NavMesh 输出配置状态", 100)
            };
        }

        private IEnumerable<ESWorkbenchIssueDescriptor> QueryWorldIssues()
        {
            ESWorldMapAsset draft = editSession?.Draft;
            ESWorldMapDefinition definition = draft?.Definition;
            if (draft == null || definition == null)
            {
                yield return new ESWorkbenchIssueDescriptor(
                    "world.asset.missing",
                    "尚未绑定世界地图",
                    ESWorkbenchIssueSeverity.Blocker,
                    description: "选择或创建 ESWorldMapAsset 后才能进行可视作者、验证和构建。",
                    priority: 1000);
                yield break;
            }

            if (editSession.RefreshExternalConflict())
                yield return new ESWorkbenchIssueDescriptor(
                    "world.source.external-conflict",
                    "正式地图已在外部变化",
                    ESWorkbenchIssueSeverity.Blocker,
                    description: "当前草稿禁止提交。检查后可放弃草稿并从正式地图重新建立基线。",
                    targetStableId: "world.map",
                    actionLabel: "检查并重载",
                    action: _ => ReloadWorldFromSource(),
                    priority: 1100);

            if (!draft.Validate(out string validationError))
                yield return new ESWorkbenchIssueDescriptor(
                    "world.validation.definition",
                    "世界草稿未通过定义验证",
                    ESWorkbenchIssueSeverity.Blocker,
                    description: validationError,
                    targetStableId: "world.map",
                    actionLabel: "验证",
                    action: _ => ValidateMap(),
                    priority: 1000);

            if (string.IsNullOrWhiteSpace(definition.contentHash))
                yield return new ESWorkbenchIssueDescriptor(
                    "world.validation.content-hash",
                    "作者内容尚未生成有效签名",
                    ESWorkbenchIssueSeverity.Error,
                    description: "保存世界草稿后才能把当前作者态作为构建输入。",
                    targetStableId: "world.map",
                    actionLabel: "保存草稿",
                    action: _ => ESWorkbench_Save(),
                    priority: 900);

            if (definition.terrainMode == ESWorldMapTerrainMode.UnityTerrain
                && string.IsNullOrWhiteSpace(definition.terrainDataAssetPath))
                yield return new ESWorkbenchIssueDescriptor(
                    "world.build.terrain-data-missing",
                    "尚未指定正式 TerrainData 输出",
                    ESWorkbenchIssueSeverity.Warning,
                    ESWorkbenchIssueChannel.Build,
                    "当前 3D 视口仅是作者预览；没有 TerrainData 路径就不能把预览冒充正式地形产物。",
                    "world.map",
                    priority: 800);

            int prefabCount = definition.prefabPlacements?.Count ?? 0;
            int prefabLimit = definition.ugcLimits?.maxPrefabInstances ?? 0;
            if (prefabLimit > 0 && prefabCount >= Mathf.CeilToInt(prefabLimit * 0.8f))
                yield return new ESWorkbenchIssueDescriptor(
                    "world.performance.prefab-budget",
                    "Prefab 放置接近 UGC 配额",
                    prefabCount > prefabLimit ? ESWorkbenchIssueSeverity.Blocker : ESWorkbenchIssueSeverity.Warning,
                    ESWorkbenchIssueChannel.Performance,
                    "当前 " + prefabCount.ToString("N0") + " / 上限 " + prefabLimit.ToString("N0") + "。",
                    "world.map",
                    priority: 700);

            int layerCount = (definition.materialLayers?.Count ?? 0)
                + (definition.vegetationLayers?.Count ?? 0)
                + (definition.scatterLayers?.Count ?? 0);
            int layerLimit = definition.ugcLimits?.maxLayers ?? 0;
            if (layerLimit > 0 && layerCount >= Mathf.CeilToInt(layerLimit * 0.8f))
                yield return new ESWorkbenchIssueDescriptor(
                    "world.performance.layer-budget",
                    "世界层数量接近 UGC 配额",
                    layerCount > layerLimit ? ESWorkbenchIssueSeverity.Blocker : ESWorkbenchIssueSeverity.Warning,
                    ESWorkbenchIssueChannel.Performance,
                    "当前 " + layerCount + " / 上限 " + layerLimit + "。",
                    "world.map",
                    priority: 680);

            if (definition.pois == null || definition.pois.Count == 0)
                yield return new ESWorkbenchIssueDescriptor(
                    "world.validation.no-poi",
                    "世界中没有 POI",
                    ESWorkbenchIssueSeverity.Warning,
                    description: "可视作者可以继续，但探索、出生或任务定位通常需要至少一个明确 POI。",
                    targetStableId: "world.map",
                    priority: 400);

            if (buildStage == BuildStage.Pending)
                yield return new ESWorkbenchIssueDescriptor(
                    "world.build.pending",
                    "世界构建任务正在等待结果",
                    ESWorkbenchIssueSeverity.Information,
                    ESWorkbenchIssueChannel.Build,
                    "可以刷新任务状态；等待不代表构建已经成功。",
                    actionLabel: "刷新状态",
                    action: _ => RefreshBakeStatus(),
                    priority: 600);
            else if (buildStage == BuildStage.Failed || buildStage == BuildStage.Cancelled)
                yield return new ESWorkbenchIssueDescriptor(
                    "world.build.failed",
                    buildStage == BuildStage.Cancelled ? "世界构建任务已取消" : "世界构建任务失败",
                    ESWorkbenchIssueSeverity.Error,
                    ESWorkbenchIssueChannel.Build,
                    lastRegistrationResult?.message ?? "构建任务没有返回可读错误。",
                    actionLabel: "重新预检",
                    action: _ => PreviewBake(),
                    priority: 900);

            yield return new ESWorkbenchIssueDescriptor(
                "world.system.authority-boundary",
                "当前权威层：ES 世界作者草稿",
                ESWorkbenchIssueSeverity.Information,
                ESWorkbenchIssueChannel.System,
                "2D/3D 视口编辑同一草稿，游戏视图只读；TerrainData、正式 Scene、NavMeshData 与资源管线发布请求分别构建并验收。",
                "world.map",
                priority: -100);
        }

        private IEnumerable<ESWorkbenchHierarchyDescriptor> QueryWorldHierarchy()
        {
            ESWorldMapDefinition definition = mapAsset?.Definition;
            if (definition == null) yield break;
            yield return new ESWorkbenchHierarchyDescriptor(
                "world.map", string.IsNullOrWhiteSpace(definition.mapId) ? mapAsset.name : definition.mapId,
                kind: "world.map", unityObject: ESWorkbench_Asset, payload: definition, order: int.MinValue);
            if (definition.regions != null)
                for (int i = 0; i < definition.regions.Count; i++)
                {
                    ESWorldMapRegionDefinition item = definition.regions[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.regionId)) continue;
                    Vector2 center = (item.min + item.max) * 0.5f;
                    Vector2 size = item.max - item.min;
                    yield return new ESWorkbenchHierarchyDescriptor(
                        "world.region." + item.regionId,
                        string.IsNullOrWhiteSpace(item.displayName) ? item.regionId : item.displayName,
                        "world.map", "world.region", payload: item.regionId, order: 1000 + i,
                        spatial: new ESWorkbenchSpatialDescriptor(
                            new Vector3(center.x, 0f, center.y),
                            new Vector3(size.x, 1f, size.y),
                            shape: ESWorkbenchSpatialShape.Rectangle,
                            color: ESWorldMapEditorPresentation.Region));
                }
            if (definition.pois != null)
                for (int i = 0; i < definition.pois.Count; i++)
                {
                    ESWorldMapPoiDefinition item = definition.pois[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.poiId)) continue;
                    yield return new ESWorkbenchHierarchyDescriptor(
                        "world.poi." + item.poiId,
                        string.IsNullOrWhiteSpace(item.displayName) ? item.poiId : item.displayName,
                        "world.map", "world.poi", payload: item.poiId, order: 2000 + i,
                        spatial: new ESWorkbenchSpatialDescriptor(
                            new Vector3(item.position.x, 0f, item.position.y),
                            Vector3.one,
                            shape: ESWorkbenchSpatialShape.Point,
                            color: ESWorldMapEditorPresentation.Poi));
                }
            if (definition.prefabPlacements != null)
                for (int i = 0; i < definition.prefabPlacements.Count; i++)
                {
                    ESWorldMapPrefabPlacement item = definition.prefabPlacements[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.placementId)) continue;
                    string prefabPath = string.IsNullOrWhiteSpace(item.editorPrefabGuid)
                        ? string.Empty
                        : AssetDatabase.GUIDToAssetPath(item.editorPrefabGuid);
                    GameObject prefab = string.IsNullOrEmpty(prefabPath)
                        ? null
                        : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    yield return new ESWorkbenchHierarchyDescriptor(
                        "world.prefab." + item.placementId,
                        item.placementId,
                        "world.map", "world.prefab", unityObject: prefab, payload: item.placementId, order: 3000 + i,
                        spatial: new ESWorkbenchSpatialDescriptor(
                            item.position,
                            item.scale,
                            item.rotationEuler,
                            ESWorkbenchSpatialShape.Object,
                            ESWorldMapEditorPresentation.Poi));
                }
        }

        private static void RegisterPageContribution(
            string contributionId,
            string title,
            string tooltip,
            ESWorldWorkbenchModule module,
            ESWorkbenchContributionCategory category,
            System.Func<ESWorldBuilderWorkbenchWindow, System.Action> draw,
            ESWorkbenchDirtyFlags dirtyFlags,
            System.Action<ESWorkbenchContributionContext, ESWorldBuilderWorkbenchWindow> prepare = null)
        {
            ESWorkbenchContributionRegistry<ESWorldWorkbenchModule>.RegisterOrUpdate(
                new ESWorkbenchContributionDescriptor<ESWorldWorkbenchModule>(
                    "world",
                    contributionId,
                    title,
                    module,
                    category,
                    context =>
                    {
                        ESWorldBuilderWorkbenchWindow window = context.Window as ESWorldBuilderWorkbenchWindow;
                        if (window == null) throw new InvalidOperationException("World 贡献缺少窗口上下文：" + contributionId);
                        prepare?.Invoke(context, window);
                        Action drawPage = draw(window);
                        context.RegisterPage(new ESWorkbenchPageDefinition(
                            contributionId,
                            title,
                            tooltip,
                            dirtyFlags,
                            () =>
                            {
                                bool locked = window.ESWorkbench_IsHierarchyLocked("world.map");
                                if (locked)
                                    EditorGUILayout.HelpBox(
                                        "世界根节点已锁定，本页面处于只读状态。解锁后才能修改作者草稿。",
                                        MessageType.Warning);
                                using (new EditorGUI.DisabledScope(locked)) drawPage?.Invoke();
                            }));
                        return null;
                    },
                    tooltip,
                    "ES.World",
                    100,
                    1),
                out string message);
            if (!string.IsNullOrEmpty(message) && !message.StartsWith("忽略旧版本", System.StringComparison.Ordinal))
                Debug.LogWarning("[ESWorkbench] " + message);
        }

        internal void HandleAuthoringPoint(
            Vector3 worldPoint,
            Func<string, bool> isVisible = null,
            Func<string, bool> isLocked = null)
        {
            if (editSession?.Draft?.Definition == null) return;
            if (isLocked != null && isLocked("world.map"))
            {
                ESWorkbench_SetStatus("世界根节点已锁定，当前作者工具不能修改草稿。", MessageType.Warning);
                return;
            }
            switch (authoringTool)
            {
                case ESWorldAuthoringTool.Terrain:
                    PaintWorldHeight(worldPoint);
                    break;
                case ESWorldAuthoringTool.Region:
                    AddWorldRegion(worldPoint);
                    break;
                case ESWorldAuthoringTool.Poi:
                    AddWorldPoi(worldPoint);
                    break;
                case ESWorldAuthoringTool.Prefab:
                    ESWorkbenchObjectDescriptor selected = ESWorkbench_Selection.Current?.Payload as ESWorkbenchObjectDescriptor;
                    if (selected == null)
                        ESWorkbench_SetStatus("请先在对象库选择一个已注册 Prefab。", MessageType.Warning);
                    else
                        ESWorkbench_Actions?.Authoring.TryCreate(selected, worldPoint, out _);
                    break;
                default:
                    SelectNearestWorldItem(worldPoint, isVisible);
                    break;
            }
        }

        private ESWorkbenchMutationResult CreateWorldPlacementMutation(ESWorkbenchMutationContext context)
        {
            if (editSession?.Draft?.Definition == null)
                return ESWorkbenchMutationResult.Failure("当前世界草稿无效。");
            ESWorkbenchObjectDescriptor descriptor = context?.Item;
            if (!(descriptor?.Source is GameObject prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                return ESWorkbenchMutationResult.Failure("放置源必须是 Project 中的 Prefab。");
            if (!ESWorkbenchContentRegistration.TryResolveRegisteredAsset(prefab, ESAssetReferKind.Prefab,
                    out ESAssetPage page, out string error))
                return ESWorkbenchMutationResult.Failure(error);
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            if (definition.prefabPlacements == null) definition.prefabPlacements = new List<ESWorldMapPrefabPlacement>();
            string id = NextStableId("placement", value => definition.prefabPlacements.Exists(item => item != null && item.placementId == value));
            definition.prefabPlacements.Add(new ESWorldMapPrefabPlacement
            {
                placementId = id,
                prefabKey = page.EffectiveStringKey,
                editorPrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)),
                position = ClampToWorld(context.WorldPosition, definition),
                scale = Vector3.one,
                enabled = true
            });
            return ESWorkbenchMutationResult.Success(
                "Prefab 已写入世界草稿：" + page.EffectiveStringKey,
                new ESWorkbenchSelection("world.prefab." + id, "world.prefab", null, id),
                "definition.prefabPlacements");
        }

        private void PaintWorldHeight(Vector3 worldPoint)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            Undo.RecordObject(editSession.Draft, "绘制世界高度");
            if (!ESWorldMapTerrainEditorFacade.TryPaintHeight(definition,
                    new Vector2(worldPoint.x, worldPoint.z), definition.worldMin, definition.worldMax,
                    authoringBrushHeight, out string error))
            {
                ESWorkbench_SetStatus(error, MessageType.Error);
                return;
            }
            NotifyWorldDraftChanged("definition.heightfield", false);
            ESWorkbench_SetStatus("世界高度场已更新。", MessageType.Info);
        }

        private void AddWorldRegion(Vector3 worldPoint)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            Undo.RecordObject(editSession.Draft, "添加世界区域");
            if (definition.regions == null) definition.regions = new List<ESWorldMapRegionDefinition>();
            string id = NextStableId("region", value => definition.regions.Exists(item => item != null && item.regionId == value));
            float half = Mathf.Max(2f, definition.chunkSize * 0.5f);
            Vector3 clamped = ClampToWorld(worldPoint, definition);
            definition.regions.Add(new ESWorldMapRegionDefinition
            {
                regionId = id,
                displayName = "区域 " + (definition.regions.Count + 1),
                semanticTag = "Default",
                min = new Vector2(Mathf.Max(definition.worldMin.x, clamped.x - half), Mathf.Max(definition.worldMin.y, clamped.z - half)),
                max = new Vector2(Mathf.Min(definition.worldMax.x, clamped.x + half), Mathf.Min(definition.worldMax.y, clamped.z + half)),
                priority = definition.regions.Count
            });
            NotifyWorldDraftChanged("definition.regions", true);
            ESWorkbench_Selection.Select(new ESWorkbenchSelection("world.region." + id, "world.region", null, id));
            ESWorkbench_SetStatus("已添加世界区域。", MessageType.Info);
        }

        private void AddWorldPoi(Vector3 worldPoint)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            Undo.RecordObject(editSession.Draft, "添加世界 POI");
            if (definition.pois == null) definition.pois = new List<ESWorldMapPoiDefinition>();
            string id = NextStableId("poi", value => definition.pois.Exists(item => item != null && item.poiId == value));
            Vector3 clamped = ClampToWorld(worldPoint, definition);
            definition.pois.Add(new ESWorldMapPoiDefinition
            {
                poiId = id,
                displayName = "POI " + (definition.pois.Count + 1),
                category = "PointOfInterest",
                position = new Vector2(clamped.x, clamped.z)
            });
            NotifyWorldDraftChanged("definition.pois", true);
            ESWorkbench_Selection.Select(new ESWorkbenchSelection("world.poi." + id, "world.poi", null, id));
            ESWorkbench_SetStatus("已添加世界 POI。", MessageType.Info);
        }

        private void SelectNearestWorldItem(Vector3 point, Func<string, bool> isVisible = null)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            float threshold = Mathf.Max(3f, definition.chunkSize * 0.2f);
            float best = float.MaxValue;
            ESWorkbenchSelection selection = new ESWorkbenchSelection("world.map", "world.map", ESWorkbench_Asset, definition);
            if (definition.pois != null)
                for (int i = 0; i < definition.pois.Count; i++)
                {
                    ESWorldMapPoiDefinition item = definition.pois[i];
                    if (item == null || (isVisible != null && !isVisible("world.poi." + item.poiId))) continue;
                    float distance = Vector2.Distance(item.position, new Vector2(point.x, point.z));
                    if (distance < best && distance <= threshold)
                    {
                        best = distance;
                        selection = new ESWorkbenchSelection("world.poi." + item.poiId, "world.poi", null, item.poiId);
                    }
                }
            if (definition.prefabPlacements != null)
                for (int i = 0; i < definition.prefabPlacements.Count; i++)
                {
                    ESWorldMapPrefabPlacement item = definition.prefabPlacements[i];
                    if (item == null || !item.enabled
                        || (isVisible != null && !isVisible("world.prefab." + item.placementId))) continue;
                    float distance = Vector2.Distance(new Vector2(item.position.x, item.position.z), new Vector2(point.x, point.z));
                    if (distance < best && distance <= threshold)
                    {
                        best = distance;
                        selection = new ESWorkbenchSelection("world.prefab." + item.placementId, "world.prefab", null, item.placementId);
                    }
                }
            if (best == float.MaxValue && definition.regions != null)
                for (int i = definition.regions.Count - 1; i >= 0; i--)
                {
                    ESWorldMapRegionDefinition item = definition.regions[i];
                    if (item != null && (isVisible == null || isVisible("world.region." + item.regionId))
                        && item.Contains(new Vector2(point.x, point.z)))
                    {
                        selection = new ESWorkbenchSelection("world.region." + item.regionId, "world.region", null, item.regionId);
                        break;
                    }
                }
            ESWorkbench_Selection.Select(selection);
        }

        private VisualElement CreateWorldInspector(ESWorkbenchSelection selection)
        {
            var root = new VisualElement { name = "ESWorldContextInspector" };
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 10f;
            if (serializedAsset == null || editSession?.Draft?.Definition == null)
            {
                root.Add(ESWindowPresentation.CreateEmptyState("尚未绑定世界草稿", "选择或创建地图资产后显示作者属性。", null, null));
                return root;
            }
            serializedAsset.Update();
            SerializedProperty definition = serializedAsset.FindProperty("definition");
            SerializedProperty target = ResolveWorldSelectionProperty(selection, definition);
            if (target == null) target = definition;
            Label title = new Label(ResolveWorldInspectorTitle(selection));
            title.AddToClassList("es-brand-title");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6f;
            root.Add(title);
            Label identity = new Label(selection?.StableId ?? "world.map");
            identity.style.fontSize = 9f;
            identity.style.color = ESEditorPresentation.SectionMutedTextColor;
            identity.style.marginBottom = 8f;
            identity.tooltip = identity.text;
            root.Add(identity);
            bool locked = ESWorkbench_IsHierarchyLocked(selection?.StableId ?? "world.map");
            if (locked)
            {
                Label lockNotice = new Label("只读 · 当前对象或其父级已锁定");
                lockNotice.style.whiteSpace = WhiteSpace.Normal;
                lockNotice.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
                lockNotice.style.marginBottom = 7f;
                root.Add(lockNotice);
            }
            if (target != null)
            {
                var fields = new VisualElement { name = "ESWorldInspectorFields" };
                fields.style.minWidth = 0f;
                AddWorldInspectorFields(fields, target, selection?.Kind);
                fields.SetEnabled(!locked);
                root.Add(fields);
                root.Bind(serializedAsset);
                root.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                {
                    if (evt.changedProperty == null) return;
                    ESWorkbench_MarkDirty(evt.changedProperty.propertyPath, ESWorkbenchDirtyFlags.Authoring);
                });
                root.RegisterCallback<DetachFromPanelEvent>(_ => root.Unbind());
            }
            if (selection != null && selection.Kind != "world.map")
            {
                VisualElement actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.marginTop = 8f;
                Button duplicate = ESWindowPresentation.CreateToolbarButton(
                    "复制",
                    "复制当前世界作者对象",
                    () => ESWorkbench_Actions?.Authoring.TryDuplicate(selection, out _));
                duplicate.style.flexGrow = 1f;
                duplicate.SetEnabled(ESWorkbench_Actions?.Authoring.CanDuplicate(selection) == true);
                actions.Add(duplicate);
                Button remove = ESWindowPresentation.CreateToolbarButton(
                    "删除所选",
                    "从世界草稿删除当前对象",
                    () => ESWorkbench_Actions?.Authoring.TryDelete(selection, out _));
                remove.style.flexGrow = 1f;
                remove.SetEnabled(ESWorkbench_Actions?.Authoring.CanDelete(selection) == true);
                actions.Add(remove);
                root.Add(actions);
            }
            VisualElement validation = new VisualElement();
            validation.style.borderTopWidth = 1f;
            validation.style.borderTopColor = ESEditorPresentation.DividerColor;
            validation.style.marginTop = 10f;
            validation.style.paddingTop = 7f;
            bool valid = editSession.Draft.Validate(out string validationError);
            Label validationLabel = new Label(valid ? "验证 · 当前对象属于有效世界草稿" : "验证 · " + validationError);
            validationLabel.style.whiteSpace = WhiteSpace.Normal;
            validationLabel.style.color = valid
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
            validation.Add(validationLabel);
            root.Add(validation);
            return root;
        }

        private static void AddWorldInspectorFields(VisualElement root, SerializedProperty target, string kind)
        {
            if (root == null || target == null) return;
            if (kind == "world.map" || string.IsNullOrWhiteSpace(kind))
            {
                AddWorldInspectorSection(root, "概览", target,
                    "mapId", "sourceMode", "generatorKey", "generatorVersion", "seed",
                    "sceneAssetKey", "layoutAssetKey", "prefabSetKey");
                AddWorldInspectorSection(root, "世界空间", target,
                    "worldMin", "worldMax", "chunkSize");
                AddWorldInspectorSection(root, "地形", target,
                    "terrainMode", "terrainDataAssetPath", "heightmapAssetKey",
                    "terrainHeightScale", "maxWalkableSlope");
                AddWorldInspectorSection(root, "构建与 UGC", target, "build", "ugcLimits");
                return;
            }
            if (kind == "world.region")
            {
                AddWorldInspectorSection(root, "概览", target,
                    "regionId", "displayName", "semanticTag", "priority");
                AddWorldInspectorSection(root, "空间范围", target, "min", "max");
                return;
            }
            if (kind == "world.poi")
            {
                AddWorldInspectorSection(root, "概览", target,
                    "poiId", "displayName", "category", "regionId", "discoverable");
                AddWorldInspectorSection(root, "空间位置", target, "position");
                return;
            }
            if (kind == "world.prefab")
            {
                AddWorldInspectorSection(root, "概览", target,
                    "placementId", "prefabKey", "editorPrefabGuid", "regionId", "enabled");
                AddWorldInspectorSection(root, "Transform", target,
                    "position", "rotationEuler", "scale");
                return;
            }
            int childDepth = target.depth + 1;
            SerializedProperty iterator = target.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            bool enterChildren = true;
            int fieldCount = 0;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != childDepth) continue;
                var field = new PropertyField(iterator.Copy());
                field.style.minWidth = 0f;
                field.style.marginBottom = 3f;
                root.Add(field);
                fieldCount++;
            }
            if (fieldCount > 0) return;
            var fallback = new PropertyField(target.Copy());
            fallback.style.minWidth = 0f;
            root.Add(fallback);
        }

        private static void AddWorldInspectorSection(
            VisualElement root,
            string title,
            SerializedProperty target,
            params string[] propertyNames)
        {
            var heading = new Label(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginTop = 7f;
            heading.style.marginBottom = 4f;
            heading.style.paddingBottom = 3f;
            heading.style.borderBottomWidth = 1f;
            heading.style.borderBottomColor = ESEditorPresentation.DividerColor;
            root.Add(heading);
            for (int i = 0; i < propertyNames.Length; i++)
            {
                SerializedProperty property = target.FindPropertyRelative(propertyNames[i]);
                if (property == null) continue;
                var field = new PropertyField(property.Copy());
                field.style.minWidth = 0f;
                field.style.marginBottom = 3f;
                root.Add(field);
            }
        }

        private SerializedProperty ResolveWorldSelectionProperty(ESWorkbenchSelection selection, SerializedProperty definition)
        {
            if (selection == null || selection.Kind == "world.map") return definition;
            string stableId = selection.Payload as string;
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            string collectionName = selection.Kind == "world.region" ? "regions"
                : selection.Kind == "world.poi" ? "pois"
                : selection.Kind == "world.prefab" ? "prefabPlacements" : string.Empty;
            string identityName = selection.Kind == "world.region" ? "regionId"
                : selection.Kind == "world.poi" ? "poiId" : "placementId";
            SerializedProperty collection = definition?.FindPropertyRelative(collectionName);
            if (collection == null || !collection.isArray) return null;
            for (int i = 0; i < collection.arraySize; i++)
            {
                SerializedProperty element = collection.GetArrayElementAtIndex(i);
                SerializedProperty identity = element.FindPropertyRelative(identityName);
                if (identity != null && identity.stringValue == stableId) return element;
            }
            return null;
        }

        private ESWorkbenchMutationResult DeleteWorldSelectionMutation(ESWorkbenchMutationContext context)
        {
            ESWorkbenchSelection selection = context?.Selection;
            if (selection == null || editSession?.Draft?.Definition == null)
                return ESWorkbenchMutationResult.Failure("当前世界草稿或选择无效。");
            string id = selection.Payload as string;
            if (string.IsNullOrWhiteSpace(id)) return ESWorkbenchMutationResult.Failure("所选对象缺少稳定 ID。");
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            string path;
            int removed;
            if (selection.Kind == "world.region")
            {
                removed = definition.regions?.RemoveAll(item => item != null && item.regionId == id) ?? 0;
                path = "definition.regions";
            }
            else if (selection.Kind == "world.poi")
            {
                removed = definition.pois?.RemoveAll(item => item != null && item.poiId == id) ?? 0;
                path = "definition.pois";
            }
            else if (selection.Kind == "world.prefab")
            {
                removed = definition.prefabPlacements?.RemoveAll(item => item != null && item.placementId == id) ?? 0;
                path = "definition.prefabPlacements";
            }
            else return ESWorkbenchMutationResult.Failure("当前选择不是可删除的世界作者对象。");
            if (removed == 0) return ESWorkbenchMutationResult.Failure("所选世界作者对象已经不存在。");
            return ESWorkbenchMutationResult.Success(
                "已删除世界作者对象。",
                new ESWorkbenchSelection("world.map", "world.map", ESWorkbench_Asset, definition),
                path);
        }

        private static bool IsMutableWorldSelection(ESWorkbenchSelection selection)
        {
            return selection != null && !selection.IsEmpty
                && (selection.Kind == "world.region" || selection.Kind == "world.poi" || selection.Kind == "world.prefab");
        }

        private static bool IsWorldPrefabSelection(ESWorkbenchSelection selection)
        {
            return selection != null && !selection.IsEmpty && selection.Kind == "world.prefab";
        }

        private ESWorkbenchMutationResult DuplicateWorldSelectionMutation(ESWorkbenchMutationContext context)
        {
            ESWorkbenchSelection selection = context?.Selection;
            if (!IsMutableWorldSelection(selection) || editSession?.Draft?.Definition == null)
                return ESWorkbenchMutationResult.Failure("当前世界选择不可复制。");
            string id = selection.Payload as string;
            if (string.IsNullOrWhiteSpace(id)) return ESWorkbenchMutationResult.Failure("所选对象缺少稳定 ID。");
            ESWorldMapDefinition definition = editSession.Draft.Definition;

            if (selection.Kind == "world.region")
            {
                ESWorldMapRegionDefinition source = definition.regions?.Find(value => value != null && value.regionId == id);
                if (source == null) return ESWorkbenchMutationResult.Failure("所选区域已经不存在。");
                ESWorldMapRegionDefinition copy = JsonUtility.FromJson<ESWorldMapRegionDefinition>(JsonUtility.ToJson(source));
                copy.regionId = NextStableId("region", value => definition.regions.Exists(item => item != null && item.regionId == value));
                copy.displayName = (string.IsNullOrWhiteSpace(source.displayName) ? source.regionId : source.displayName) + " 副本";
                Vector2 offset = Vector2.one * Mathf.Max(1f, definition.chunkSize * 0.25f);
                OffsetRegionWithinWorld(copy.min, copy.max, offset, definition.worldMin, definition.worldMax,
                    out copy.min, out copy.max);
                definition.regions.Add(copy);
                return ESWorkbenchMutationResult.Success(
                    "已复制世界区域。",
                    new ESWorkbenchSelection("world.region." + copy.regionId, "world.region", null, copy.regionId),
                    "definition.regions");
            }

            if (selection.Kind == "world.poi")
            {
                ESWorldMapPoiDefinition source = definition.pois?.Find(value => value != null && value.poiId == id);
                if (source == null) return ESWorkbenchMutationResult.Failure("所选 POI 已经不存在。");
                ESWorldMapPoiDefinition copy = JsonUtility.FromJson<ESWorldMapPoiDefinition>(JsonUtility.ToJson(source));
                copy.poiId = NextStableId("poi", value => definition.pois.Exists(item => item != null && item.poiId == value));
                copy.displayName = (string.IsNullOrWhiteSpace(source.displayName) ? source.poiId : source.displayName) + " 副本";
                copy.position = ClampToWorld2D(copy.position + Vector2.one * Mathf.Max(1f, definition.chunkSize * 0.25f), definition);
                definition.pois.Add(copy);
                return ESWorkbenchMutationResult.Success(
                    "已复制世界 POI。",
                    new ESWorkbenchSelection("world.poi." + copy.poiId, "world.poi", null, copy.poiId),
                    "definition.pois");
            }

            ESWorldMapPrefabPlacement placement = definition.prefabPlacements?.Find(value => value != null && value.placementId == id);
            if (placement == null) return ESWorkbenchMutationResult.Failure("所选 Prefab 放置已经不存在。");
            ESWorldMapPrefabPlacement placementCopy = JsonUtility.FromJson<ESWorldMapPrefabPlacement>(JsonUtility.ToJson(placement));
            placementCopy.placementId = NextStableId("placement", value => definition.prefabPlacements.Exists(item => item != null && item.placementId == value));
            placementCopy.position = ClampToWorld(placementCopy.position
                + new Vector3(Mathf.Max(1f, definition.chunkSize * 0.25f), 0f, Mathf.Max(1f, definition.chunkSize * 0.25f)), definition);
            definition.prefabPlacements.Add(placementCopy);
            return ESWorkbenchMutationResult.Success(
                "已复制世界 Prefab 放置。",
                new ESWorkbenchSelection(
                    "world.prefab." + placementCopy.placementId, "world.prefab", null, placementCopy.placementId),
                "definition.prefabPlacements");
        }

        private ESWorkbenchMutationResult MoveWorldSelectionMutation(ESWorkbenchMutationContext context)
        {
            ESWorkbenchSelection selection = context?.Selection;
            if (!IsMutableWorldSelection(selection) || editSession?.Draft?.Definition == null)
                return ESWorkbenchMutationResult.Failure("当前世界选择不可移动。");
            string id = selection.Payload as string;
            if (string.IsNullOrWhiteSpace(id)) return ESWorkbenchMutationResult.Failure("所选对象缺少稳定 ID。");
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            Vector3 target = ClampToWorld(context.WorldPosition, definition);

            if (selection.Kind == "world.region")
            {
                ESWorldMapRegionDefinition region = definition.regions?.Find(value => value != null && value.regionId == id);
                if (region == null) return ESWorkbenchMutationResult.Failure("所选区域已经不存在。");
                Vector2 center = (region.min + region.max) * 0.5f;
                Vector2 desiredCenter = new Vector2(target.x, target.z);
                OffsetRegionWithinWorld(region.min, region.max, desiredCenter - center,
                    definition.worldMin, definition.worldMax, out region.min, out region.max);
                return ESWorkbenchMutationResult.Success("已移动世界区域。", selection, "definition.regions");
            }

            if (selection.Kind == "world.poi")
            {
                ESWorldMapPoiDefinition poi = definition.pois?.Find(value => value != null && value.poiId == id);
                if (poi == null) return ESWorkbenchMutationResult.Failure("所选 POI 已经不存在。");
                poi.position = new Vector2(target.x, target.z);
                return ESWorkbenchMutationResult.Success("已移动世界 POI。", selection, "definition.pois");
            }

            ESWorldMapPrefabPlacement placement = definition.prefabPlacements?.Find(value => value != null && value.placementId == id);
            if (placement == null) return ESWorkbenchMutationResult.Failure("所选 Prefab 放置已经不存在。");
            placement.position = target;
            return ESWorkbenchMutationResult.Success("已移动世界 Prefab 放置。", selection, "definition.prefabPlacements");
        }

        private ESWorkbenchMutationResult RotateWorldSelectionMutation(ESWorkbenchMutationContext context)
        {
            ESWorkbenchSelection selection = context?.Selection;
            if (!IsWorldPrefabSelection(selection) || editSession?.Draft?.Definition == null)
                return ESWorkbenchMutationResult.Failure("当前选择不支持旋转。");
            string id = selection.Payload as string;
            ESWorldMapPrefabPlacement placement = editSession.Draft.Definition.prefabPlacements?
                .Find(value => value != null && value.placementId == id);
            if (placement == null) return ESWorkbenchMutationResult.Failure("所选 Prefab 放置已经不存在。");
            Vector3 value = context.RotationEuler;
            if (!IsFinite(value)) return ESWorkbenchMutationResult.Failure("旋转值包含无效数字。");
            placement.rotationEuler = new Vector3(
                NormalizeAngle(value.x), NormalizeAngle(value.y), NormalizeAngle(value.z));
            return ESWorkbenchMutationResult.Success(
                "已旋转世界 Prefab 放置。", selection, "definition.prefabPlacements");
        }

        private ESWorkbenchMutationResult ScaleWorldSelectionMutation(ESWorkbenchMutationContext context)
        {
            ESWorkbenchSelection selection = context?.Selection;
            if (!IsWorldPrefabSelection(selection) || editSession?.Draft?.Definition == null)
                return ESWorkbenchMutationResult.Failure("当前选择不支持缩放。");
            string id = selection.Payload as string;
            ESWorldMapPrefabPlacement placement = editSession.Draft.Definition.prefabPlacements?
                .Find(value => value != null && value.placementId == id);
            if (placement == null) return ESWorkbenchMutationResult.Failure("所选 Prefab 放置已经不存在。");
            Vector3 value = context.Scale;
            if (!IsFinite(value)) return ESWorkbenchMutationResult.Failure("缩放值包含无效数字。");
            placement.scale = new Vector3(
                Mathf.Max(0.01f, value.x),
                Mathf.Max(0.01f, value.y),
                Mathf.Max(0.01f, value.z));
            return ESWorkbenchMutationResult.Success(
                "已缩放世界 Prefab 放置。", selection, "definition.prefabPlacements");
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static float NormalizeAngle(float value)
        {
            value = Mathf.Repeat(value + 180f, 360f) - 180f;
            return Mathf.Approximately(value, -180f) ? 180f : value;
        }

        protected override void ESWorkbench_OnDirtyStateChanged(
            string dirtyKey,
            ESWorkbenchDirtyFlags flags)
        {
            base.ESWorkbench_OnDirtyStateChanged(dirtyKey, flags);
            if ((flags & ESWorkbenchDirtyFlags.Authoring) != 0 && editSession != null)
                editSession.NotifyDraftChanged(string.IsNullOrWhiteSpace(dirtyKey) ? "definition" : dirtyKey);
        }

        protected override void ESWorkbench_OnUndoRedo()
        {
            base.ESWorkbench_OnUndoRedo();
            if (editSession == null) return;

            editSession.SynchronizeDraftAfterUndoRedo();
            ESWorkbench_SetDirtyStateWithoutNotification(
                editSession.IsDirty,
                "world.undo-redo",
                ESWorkbenchDirtyFlags.Authoring);
            ESWorkbench_SetStatus(
                editSession.IsDirty ? "撤销/重做已同步，世界草稿仍有未提交变更。" : "撤销/重做已同步，世界草稿与基线一致。",
                editSession.IsDirty ? MessageType.Warning : MessageType.Info);
        }

        private void NotifyWorldDraftChanged(string path, bool hierarchyChanged)
        {
            ESWorkbench_MarkDirty(path, ESWorkbenchDirtyFlags.Authoring);
        }

        private void RevertWorldDraft()
        {
            if (editSession == null || !editSession.IsDirty) return;
            if (ESWorkbench_IsHierarchyLocked("world.map"))
            {
                ESWorkbench_SetStatus("世界根节点已锁定，不能回退当前草稿。", MessageType.Warning);
                return;
            }
            if (!EditorUtility.DisplayDialog("回退世界草稿", "放弃当前会话中的未提交世界变更？", "回退", "取消")) return;
            editSession.RevertDraft();
            serializedAsset?.Update();
            ESWorkbench_ClearDirty();
            ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.DataChanged);
            ESWorkbench_SetStatus("世界草稿已回退到会话基线。", MessageType.Info);
        }

        private void ReloadWorldFromSource()
        {
            if (editSession == null) return;
            if (editSession.IsDirty
                && !EditorUtility.DisplayDialog(
                    "重新载入正式地图",
                    "当前草稿有未提交变更。重新载入会放弃这些变更，并以正式地图建立新基线。",
                    "检查并重载",
                    "取消"))
                return;

            editSession.ReloadFromSource();
            serializedAsset?.Update();
            ESWorkbench_ClearDirty();
            ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.DataChanged);
            ESWorkbench_SetStatus("已从正式地图重新建立草稿基线。", MessageType.Info);
        }

        private void OpenBrushPopup(ESWorkbenchActionContext actions)
        {
            var request = new ESWorkbenchPopupRequest("地形笔刷", new Vector2(320f, 150f), _ =>
            {
                var content = new VisualElement();
                Label title = new Label("地形笔刷");
                title.AddToClassList("es-brand-title");
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                content.Add(title);
                var slider = new Slider("目标高度", 0f, 1f) { value = authoringBrushHeight, showInputField = true };
                slider.RegisterValueChangedCallback(evt => authoringBrushHeight = evt.newValue);
                content.Add(slider);
                return content;
            });
            Rect anchor = new Rect(position.center, Vector2.one);
            actions.ShowPopup(request, anchor);
        }

        private static string ResolveWorldInspectorTitle(ESWorkbenchSelection selection)
        {
            switch (selection?.Kind)
            {
                case "world.region": return "区域";
                case "world.poi": return "POI";
                case "world.prefab": return "Prefab 放置";
                default: return "世界地图";
            }
        }

        private static string NextStableId(string prefix, Func<string, bool> exists)
        {
            int index = 1;
            string candidate;
            do candidate = prefix + "_" + index++; while (exists(candidate));
            return candidate;
        }

        private static Vector3 ClampToWorld(Vector3 value, ESWorldMapDefinition definition)
        {
            value.x = Mathf.Clamp(value.x, definition.worldMin.x, definition.worldMax.x);
            value.z = Mathf.Clamp(value.z, definition.worldMin.y, definition.worldMax.y);
            return value;
        }

        private static Vector2 ClampToWorld2D(Vector2 value, ESWorldMapDefinition definition)
        {
            return new Vector2(
                Mathf.Clamp(value.x, definition.worldMin.x, definition.worldMax.x),
                Mathf.Clamp(value.y, definition.worldMin.y, definition.worldMax.y));
        }

        internal static void OffsetRegionWithinWorld(
            Vector2 first,
            Vector2 second,
            Vector2 offset,
            Vector2 worldMin,
            Vector2 worldMax,
            out Vector2 resultMin,
            out Vector2 resultMax)
        {
            Vector2 sourceMin = Vector2.Min(first, second);
            Vector2 sourceMax = Vector2.Max(first, second);
            Vector2 worldSize = Vector2.Max(Vector2.zero, worldMax - worldMin);
            Vector2 size = Vector2.Min(sourceMax - sourceMin, worldSize);
            Vector2 desiredMin = sourceMin + offset;
            resultMin = new Vector2(
                Mathf.Clamp(desiredMin.x, worldMin.x, worldMax.x - size.x),
                Mathf.Clamp(desiredMin.y, worldMin.y, worldMax.y - size.y));
            resultMax = resultMin + size;
        }

        private void ValidateMap()
        {
            if (mapAsset == null) return;
            if (!mapAsset.Validate(out string error)) { ESWorkbench_SetStatus(error, MessageType.Error); return; }
            ESWorkbench_SetStatus("地图与 UGC 配额验证通过。可继续执行资源收集预检。", MessageType.Info);
        }

        private void CreateAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建 ES 世界地图资产", "ESWorldMap", "asset", "选择地图资产保存位置");
            if (string.IsNullOrWhiteSpace(path)) return;
            ESWorldMapAsset asset = ScriptableObject.CreateInstance<ESWorldMapAsset>();
            EnsureDefinitionBaseline(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            ESWorkbench_BindAsset(asset);
            Selection.activeObject = asset;
            ESWorkbench_SetStatus("已创建空白地图基线；可按需填充默认配置或显式加载示例内容。", MessageType.Info);
        }
    }
}
#endif
