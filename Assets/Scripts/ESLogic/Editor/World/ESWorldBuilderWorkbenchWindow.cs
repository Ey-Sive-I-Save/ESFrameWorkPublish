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
        private sealed class WorldBrushPaletteTemplate
        {
            public WorldBrushPaletteTemplate(string id, string displayName, float normalizedHeight, string description,
                ESWorldTerrainBrushMode mode = ESWorldTerrainBrushMode.Flatten, float strength = -1f)
            {
                Id = id;
                DisplayName = displayName;
                NormalizedHeight = Mathf.Clamp01(normalizedHeight);
                Description = description ?? string.Empty;
                Mode = mode;
                Strength = strength < 0f ? -1f : Mathf.Clamp01(strength);
            }

            public string Id { get; }
            public string DisplayName { get; }
            public float NormalizedHeight { get; }
            public string Description { get; }
            public ESWorldTerrainBrushMode Mode { get; }
            public float Strength { get; }
        }

        private sealed class WorldRegionPaletteTemplate
        {
            public WorldRegionPaletteTemplate(string id, string displayName, string category, Vector2 size, string description)
            {
                Id = id;
                DisplayName = displayName;
                Category = category;
                Size = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
                Description = description ?? string.Empty;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string Category { get; }
            public Vector2 Size { get; }
            public string Description { get; }
        }

        private static readonly WorldBrushPaletteTemplate[] WorldBrushTemplates =
        {
            new WorldBrushPaletteTemplate("lowland", "低地塑形笔刷", 0.24f, "快速建立洼地、河谷与低海拔区域。"),
            new WorldBrushPaletteTemplate("midland", "中位塑形笔刷", 0.5f, "建立通用地表基准高度，适合道路与建筑平台。", ESWorldTerrainBrushMode.Flatten),
            new WorldBrushPaletteTemplate("highland", "高地塑形笔刷", 0.78f, "快速建立山脊、高台和视野制高点。"),
            new WorldBrushPaletteTemplate("raise", "连续抬高笔刷", 0.5f, "沿连续笔划逐步抬高当前地形。", ESWorldTerrainBrushMode.Raise, 0.45f),
            new WorldBrushPaletteTemplate("lower", "连续降低笔刷", 0.5f, "沿连续笔划逐步降低当前地形。", ESWorldTerrainBrushMode.Lower, 0.45f),
            new WorldBrushPaletteTemplate("smooth", "地形平滑笔刷", 0.5f, "削弱尖峰和断层，保持笔划结果对称稳定。", ESWorldTerrainBrushMode.Smooth, 0.55f)
        };

        private static readonly WorldRegionPaletteTemplate[] WorldRegionTemplates =
        {
            new WorldRegionPaletteTemplate("playable", "可游玩区域", "Gameplay", new Vector2(48f, 48f), "常规可行走与玩法承载区域。"),
            new WorldRegionPaletteTemplate("spawn", "出生区域", "Spawn", new Vector2(20f, 20f), "玩家或队伍出生与安全落点区域。"),
            new WorldRegionPaletteTemplate("restricted", "限制区域", "Restricted", new Vector2(32f, 32f), "用于边界、危险区或禁止通行区域。")
        };

        internal const string CommercialValidationAssetPath =
            "Assets/ESValidation/World/ESWorldCommercialValidationV2.asset";
        private const string CommercialValidationPrefabPath =
            "Assets/ESNormalAssets/Prefabs/Cube.prefab";
        private const string CommercialValidationAlternatePrefabPath =
            "Assets/ESNormalAssets/Prefabs/蓝色方块.prefab";
        private const int CommercialValidationReadinessFrameLimit = 120;
        internal const int CommercialValidationPreviewIterations = 240;
        internal const double CommercialValidationMinimumDurationSeconds = 30d;
        private static bool commercialValidationAcceptanceInProgress;
        private static ESWorldEditSession commercialValidationPeerSession;

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
        private static readonly ESWorkbenchResponsiveLayoutPolicy WorldLayoutPolicy =
            new ESWorkbenchResponsiveLayoutPolicy(
                minimumWindowWidth: 980f,
                minimumWindowHeight: 640f,
                wideBreakpoint: 1180f,
                narrowBreakpoint: 980f,
                minimumCenterWidth: 600f,
                minimumCenterHeight: 340f,
                minimumLeftPaneWidth: 280f,
                minimumInspectorPaneWidth: 280f,
                maximumLeftPaneRatio: 0.34f,
                maximumInspectorPaneRatio: 0.30f,
                maximumBottomDrawerRatio: 0.34f,
                preferredLeftPaneWidth: 320f,
                maximumLeftPaneWidth: 420f,
                preferredInspectorPaneWidth: 320f,
                maximumInspectorPaneWidth: 420f,
                collapsedBottomDrawerHeight: 32f,
                compactBottomDrawerHeight: 96f,
                minimumBottomDrawerHeight: 112f,
                preferredBottomDrawerHeight: 220f,
                maximumBottomDrawerHeight: 320f);
        internal enum BuildStage { NotStarted, Preflight, Pending, Succeeded, Failed, Cancelled }
        private BuildStage buildStage;
        private ESContentRegistrationResult lastRegistrationResult;
        private ESContentRegistrationResult collectPreviewResult;
        private string collectRequestId = string.Empty;
        private string bakeRequestId = string.Empty;
        private string bakeRunId = string.Empty;
        private string previewStressResult = string.Empty;
        private ESWorldWorkbenchAcceptanceResult? latestAcceptance;
        [SerializeField] private string editSessionOwnerId = string.Empty;
        private ESWorldEditSession editSession;
        private ESWorldAuthoringTool authoringTool = ESWorldAuthoringTool.Select;
        [SerializeField] private float authoringBrushHeight = 0.5f;
        [SerializeField] private float authoringBrushRadius = 8f;
        [SerializeField] private float authoringBrushStrength = 0.65f;
        [SerializeField] private float authoringBrushFalloff = 0.75f;
        [SerializeField] private ESWorldTerrainBrushMode authoringBrushMode = ESWorldTerrainBrushMode.Flatten;
        private bool terrainStrokeSessionOpen;
        private bool terrainStrokeUndoRecorded;
        private int terrainStrokeUndoGroup = -1;
        private bool terrainStrokeSnapshotValid;
        private bool terrainStrokeHadHeightfield;
        private bool terrainStrokeDraftSyncPending;
        private int terrainStrokeWidth;
        private int terrainStrokeHeight;
        private float terrainStrokeDefaultHeight;
        private List<float> terrainStrokeSamplesSnapshot;
        private float cachedBrushHeight = -1f;
        private float cachedBrushRadius = -1f;
        private float cachedBrushStrength = -1f;
        private float cachedBrushFalloff = -1f;
        private ESWorldTerrainBrushMode cachedBrushMode = ESWorldTerrainBrushMode.Flatten;
        private string cachedBrushSummary = string.Empty;
        [SerializeField] private string activeWorldContentId = string.Empty;
        [SerializeField] private string activeWorldContentPresetId = string.Empty;
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
            editSession = ESWorldEditSession.Open(asset, EnsureEditSessionOwnerId());
            return editSession?.Draft;
        }

        private string EnsureEditSessionOwnerId()
        {
            if (string.IsNullOrWhiteSpace(editSessionOwnerId))
                editSessionOwnerId = Guid.NewGuid().ToString("N");
            return editSessionOwnerId;
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
            window.minSize = WorldLayoutPolicy.ResolveAdaptiveMinimum(
                EditorGUIUtility.GetMainWindowPosition());
            window.Show();
            if (Selection.activeObject is ESWorldMapAsset selected && selected != window.ESWorkbench_Asset)
                window.ESWorkbench_BindAsset(selected);
        }

        [MenuItem("【ES】/内容制作/环境/创建或打开 World 商业验收样本", false, 121)]
        public static void OpenCommercialValidationSample()
        {
            ESWorldMapAsset asset = AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(
                CommercialValidationAssetPath);
            if (asset == null)
            {
                EnsureAssetFolder("Assets/ESValidation/World");
                asset = ScriptableObject.CreateInstance<ESWorldMapAsset>();
                asset.name = "ES World 商业验收样本";
                PopulateCommercialValidationSample(asset);
                AssetDatabase.CreateAsset(asset, CommercialValidationAssetPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                string before = EditorJsonUtility.ToJson(asset);
                PopulateCommercialValidationSample(asset);
                if (!string.Equals(before, EditorJsonUtility.ToJson(asset), StringComparison.Ordinal))
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Open();
        }

        [MenuItem("【ES】/内容制作/环境/运行 World 真实协作验收", false, 123)]
        public static void RunCommercialValidationAcceptance()
        {
            if (commercialValidationAcceptanceInProgress)
            {
                Debug.LogWarning("[ES World 商业验收] 已有真实协作验收正在等待窗口就绪。");
                return;
            }

            ESWorldBuilderWorkbenchWindow main = GetWindow<ESWorldBuilderWorkbenchWindow>();
            if (main == null
                || main.editSession == null
                || !string.Equals(
                    AssetDatabase.GetAssetPath(main.ESWorkbench_Asset),
                    CommercialValidationAssetPath,
                    StringComparison.Ordinal))
            {
                Debug.LogError("[ES World 商业验收] 请先打开并绑定固定商业验收样本。");
                return;
            }
            commercialValidationAcceptanceInProgress = true;

            main.editSession.ReloadFromSource();
            main.ESWorkbench_ClearDirty();
            ESWorldMapPrefabPlacement localPlacement = main.editSession.Draft.Definition
                .prefabPlacements?.FirstOrDefault(value =>
                    value != null && value.placementId == "placement.commercial-validation");
            string alternateGuid = AssetDatabase.AssetPathToGUID(
                CommercialValidationAlternatePrefabPath);
            if (localPlacement == null || string.IsNullOrWhiteSpace(alternateGuid))
            {
                Debug.LogError("[ES World 商业验收] 验收放置点或备用 Prefab GUID 不可用。");
                commercialValidationAcceptanceInProgress = false;
                return;
            }

            Undo.RecordObject(main.editSession.Draft, "ES World 真实协作验收：保留主窗口草稿");
            localPlacement.editorPrefabGuid = alternateGuid;
            localPlacement.prefabKey = "validation.prefab.alternate";
            main.editSession.NotifyDraftChanged("definition.prefabPlacements");
            main.ESWorkbench_MarkDirty(
                "world.live-conflict-local-draft",
                ESWorkbenchDirtyFlags.Authoring);

            ESWorldMapAsset source = main.ESWorkbench_Asset;
            ReleaseCommercialValidationPeerSession();
            commercialValidationPeerSession = ESWorldEditSession.Open(
                source,
                "world-commercial-validation-peer");
            ESWorldEditSession peer = commercialValidationPeerSession;
            if (peer == null)
            {
                FailCommercialValidationAcceptance("无法创建受管协作编辑会话。");
                return;
            }
            peer.ReloadFromSource();
            if (ESWorldEditSession.GetActiveSessionCount(source) < 2)
            {
                FailCommercialValidationAcceptance("主会话与协作会话未同时进入活动集合。");
                return;
            }
            Undo.RecordObject(peer.Draft, "ES World 真实协作验收：协作会话提交");
            peer.Draft.Definition.seed += 1;
            peer.NotifyDraftChanged("definition.seed");
            ESWorldEditCommitResult commit = peer.TryCommit();
            if (!commit.success)
            {
                FailCommercialValidationAcceptance("协作会话提交失败：" + commit.message);
                return;
            }

            WaitForCommercialValidationConflict(main, peer, source, 0);
        }

        private static void WaitForCommercialValidationConflict(
            ESWorldBuilderWorkbenchWindow main,
            ESWorldEditSession peer,
            ESWorldMapAsset source,
            int frame)
        {
            if (main == null
                || peer == null
                || !ReferenceEquals(peer, commercialValidationPeerSession)
                || source == null
                || main.editSession == null)
            {
                FailCommercialValidationAcceptance("等待冲突期间窗口或会话已失效。");
                return;
            }
            if (!main.editSession.RefreshExternalConflict())
            {
                if (frame >= CommercialValidationReadinessFrameLimit)
                {
                    FailCommercialValidationAcceptance("副窗口提交后主窗口未观察到外部冲突。");
                    return;
                }
                EditorApplication.delayCall += () =>
                    WaitForCommercialValidationConflict(main, peer, source, frame + 1);
                return;
            }

            main.ESWorkbench_SetStatus(
                "商业验收冲突已建立：协作会话已提交，当前窗口草稿必须保留并拒绝后写。",
                MessageType.Warning);
            main.Focus();
            try
            {
                main.RunWorldAcceptance(
                    CommercialValidationPreviewIterations,
                    true,
                    CommercialValidationMinimumDurationSeconds);
            }
            finally
            {
                commercialValidationAcceptanceInProgress = false;
                ReleaseCommercialValidationPeerSession();
            }
        }

        private static void FailCommercialValidationAcceptance(string message)
        {
            commercialValidationAcceptanceInProgress = false;
            ReleaseCommercialValidationPeerSession();
            Debug.LogError("[ES World 商业验收] " + message);
        }

        private static void ReleaseCommercialValidationPeerSession()
        {
            ESWorldEditSession peer = commercialValidationPeerSession;
            commercialValidationPeerSession = null;
            if (peer == null)
                return;
            try
            {
                peer.ClearRecoveryState();
            }
            finally
            {
                peer.Dispose();
            }
        }

        [MenuItem("【ES】/内容制作/环境/世界配置与构建", false, 122)]
        public static void OpenConfigurationWorkbench()
        {
            Open();
        }

        public override GUIContent ESWindow_GetWindowGUIContent() => new GUIContent("ES 世界构建工作台", "统一编辑地图、地形、对象散布、导航、环境和 UGC 构建配置。");
        public override string ESWindow_PresentationShortTitle => "世界";
        protected override string ESWindow_Subtitle => "ES 专属世界内容底板 · 地图到 UGC 构建配置";
        protected override Vector2 ESWindow_MinSize => WorldLayoutPolicy.ResolveAdaptiveMinimum(
            EditorGUIUtility.GetMainWindowPosition());
        protected override Vector2 ESWindow_DefaultSize => new Vector2(1440f, 900f);
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
        internal void BindAssetForTest(ESWorldMapAsset asset) => ESWorkbench_BindAsset(asset);
        internal ESWorldEditSession EditSessionForTest => editSession;
        internal ESWorldWorkbenchAcceptanceResult RunAcceptanceForTest(int previewIterations)
        {
            return ESWorldWorkbenchAcceptance.Execute(
                mapAsset,
                editSession,
                WorldLayoutPolicy.CreateCommercialVisualMatrix(),
                previewIterations,
                false,
                0d);
        }
        internal int ContributionLoadCountForTest => ESWorkbench_ContributionLoadCount;
        internal IReadOnlyList<ESWorkbenchCommandDescriptor> CommandsForTest => ESWorkbench_Commands;
        internal Vector2 MinimumSizeForTest => ESWindow_MinSize;
        internal Vector2 IdealMinimumSizeForTest => new Vector2(
            WorldLayoutPolicy.MinimumWindowWidth,
            WorldLayoutPolicy.MinimumWindowHeight);
        internal Vector2 DefaultSizeForTest => ESWindow_DefaultSize;
        internal ESWorkbenchResponsiveLayoutPolicy LayoutPolicyForTest => WorldLayoutPolicy;
        internal string ActiveWorldContentIdForTest => activeWorldContentId;
        internal string ActiveToolIdForTest => ESWorkbench_Actions?.Tools?.ActiveToolId ?? string.Empty;
        internal void ActivateToolForTest(string toolId) => ESWorkbench_Actions?.Tools?.Activate(toolId);
        internal float TerrainBrushRadiusForTest => authoringBrushRadius;
        internal float TerrainBrushStrengthForTest => authoringBrushStrength;
        internal int RegisteredDocumentCountForTest => ESWorkbench_Documents.Count;
        internal IReadOnlyList<string> AuthoringModeIdsForTest => ESWorkbench_AuthoringModes
            .Select(value => value.ModeId)
            .ToArray();

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
            if (serializedChanged) ESWorkbench_MarkSelectedDocumentDirty();
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
                if (GUILayout.Button("打开对话编辑器", GUILayout.Height(28f))) ESWorldDialogueEditorWindow.OpenFor(mapAsset, this);
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
            GUILayout.Label("地形塑形笔刷", ESEditorPresentation.HeaderStyle);
            DrawTerrainBrushModeButtons();
            if (authoringBrushMode == ESWorldTerrainBrushMode.Flatten)
                authoringBrushHeight = EditorGUILayout.Slider("目标高度", authoringBrushHeight, 0f, 1f);
            else
                EditorGUILayout.HelpBox(
                    GetTerrainBrushModeDisplayName(authoringBrushMode) + "模式不使用目标高度；当前强度决定每次采样的变化量。",
                    MessageType.None);
            authoringBrushRadius = EditorGUILayout.Slider("半径（米）", authoringBrushRadius, 0.5f, 64f);
            authoringBrushStrength = EditorGUILayout.Slider("强度", authoringBrushStrength, 0.05f, 1f);
            if (authoringBrushMode != ESWorldTerrainBrushMode.Smooth)
                authoringBrushFalloff = EditorGUILayout.Slider("边缘衰减", authoringBrushFalloff, 0f, 1f);
            EditorGUILayout.HelpBox(
                GetTerrainBrushSummary() + "\n在 2D 或 3D 视口按住左键连续塑形；一次连续笔划只生成一条撤销记录。",
                MessageType.Info);
            EditorGUILayout.Space(8f);
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

        private void DrawTerrainBrushModeButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTerrainBrushModeButton(ESWorldTerrainBrushMode.Flatten, "平整");
                DrawTerrainBrushModeButton(ESWorldTerrainBrushMode.Raise, "抬高");
                DrawTerrainBrushModeButton(ESWorldTerrainBrushMode.Lower, "降低");
                DrawTerrainBrushModeButton(ESWorldTerrainBrushMode.Smooth, "平滑");
            }
        }

        private void DrawTerrainBrushModeButton(ESWorldTerrainBrushMode mode, string title)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = authoringBrushMode == mode
                ? new Color(0.19f, 0.48f, 0.72f, 1f)
                : new Color(0.16f, 0.17f, 0.19f, 1f);
            if (GUILayout.Button(new GUIContent(title, "切换地形笔刷模式：" + title),
                    ESEditorPresentation.ToolbarButtonStyle, GUILayout.MinWidth(48f)))
            {
                authoringBrushMode = mode;
                ESWorkbench_SetStatus("当前地形笔刷模式：" + title, MessageType.Info);
                Repaint();
            }
            GUI.backgroundColor = previous;
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
            PopulateCommercialValidationSample(
                mapAsset,
                ESWorkbench_Asset != null ? ESWorkbench_Asset.name : null);
            ESWorldMapAuthoringUtility.MarkChanged(mapAsset);
            ESWorkbench_SetStatus("中文示例注册已加载：材质、植被、Prefab、导航、天气、水体、流式、碰撞、区域和 POI 均已注入。", MessageType.Info);
            ESWorkbench_MarkDirty("world.sample-registration", ESWorkbenchDirtyFlags.Authoring);
        }

        internal static void PopulateCommercialValidationSample(
            ESWorldMapAsset asset,
            string defaultMapId = "es.world.commercial-validation")
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            EnsureDefinitionBaseline(asset, defaultMapId);
            EnsureDefinitionContainers(asset.Definition);
            PopulateSampleContent(asset.Definition);
            EnsureRegion(asset.Definition.regions, new ESWorldMapRegionDefinition
            {
                regionId = "region.commercial-validation-long-cn",
                displayName = "商业验收超长中文区域名称：中央生态湖区、出生营地与远景地标联合检查区",
                semanticTag = "Validation",
                min = new Vector2(24f, 128f),
                max = new Vector2(104f, 232f),
                priority = 30
            });
            EnsurePoi(asset.Definition.pois, new ESWorldMapPoiDefinition
            {
                poiId = "poi.commercial-validation-long-cn",
                displayName = "商业验收超长中文兴趣点：多分辨率布局与检查器文本压力样例",
                category = "Validation",
                regionId = "region.commercial-validation-long-cn",
                position = new Vector2(64f, 176f),
                discoverable = true
            });
            if (asset.Definition.prefabPlacements == null)
                asset.Definition.prefabPlacements = new List<ESWorldMapPrefabPlacement>();
            if (!asset.Definition.prefabPlacements.Exists(value =>
                    value != null && value.placementId == "placement.commercial-validation"))
            {
                string prefabGuid = AssetDatabase.AssetPathToGUID(
                    CommercialValidationPrefabPath);
                if (!string.IsNullOrWhiteSpace(prefabGuid))
                    asset.Definition.prefabPlacements.Add(new ESWorldMapPrefabPlacement
                    {
                        placementId = "placement.commercial-validation",
                        prefabKey = "validation.prefab.primary",
                        editorPrefabGuid = prefabGuid,
                        regionId = "region.spawn",
                        position = new Vector3(48f, 24f, 48f),
                        rotationEuler = new Vector3(0f, 35f, 0f),
                        scale = new Vector3(6f, 6f, 6f),
                        enabled = true
                    });
            }
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
            RegisterDocumentContribution("overview", "世界总览", "地图身份与整体状态", ESWorldWorkbenchModule.Overview,
                ESWorkbenchContributionCategory.General, window => window.DrawOverview, ESWorkbenchDirtyFlags.Authoring);
            RegisterModeContribution("terrain", "地形", "Unity Terrain / 高度场", ESWorldWorkbenchModule.Terrain,
                ESWorkbenchContributionCategory.Terrain, new[] { "world.select", "world.terrain" },
                new[] { ESWorkbenchContentKind.Brush, ESWorkbenchContentKind.Terrain }, "world.terrain",
                window => window.DrawTerrain, 1000, true);
            RegisterModeContribution("material", "材质", "材质与地表规则", ESWorldWorkbenchModule.Material,
                ESWorkbenchContributionCategory.Material, new[] { "world.select", "world.terrain" },
                new[] { ESWorkbenchContentKind.Material, ESWorkbenchContentKind.Terrain }, "world.select",
                window => window.DrawMaterialLayers, 900, true,
                (context, window) =>
                {
                    if (window.mapAsset?.Definition?.materialLayers == null) return;
                    ESWorldMapMaterialLayer layer = window.FindMaterialLayer("material.grass");
                    if (layer != null) context.RegisterAssetSlot(ESWorldMapWorkbenchSlots.Material(window.GetResourceLibraryPath(), window.mapAsset.Definition.materialLayers.IndexOf(layer)));
                });
            RegisterModeContribution("vegetation", "植被", "植被层与生物群落", ESWorldWorkbenchModule.Vegetation,
                ESWorkbenchContributionCategory.Vegetation, new[] { "world.select" },
                new[] { ESWorkbenchContentKind.Vegetation }, "world.select", window => window.DrawVegetationLayers, 800, true,
                (context, window) =>
                {
                    if (window.mapAsset?.Definition?.vegetationLayers == null) return;
                    ESWorldMapVegetationLayer layer = window.FindVegetationLayer("vegetation.trees");
                    if (layer != null) context.RegisterAssetSlot(ESWorldMapWorkbenchSlots.Vegetation(window.GetResourceLibraryPath(), window.mapAsset.Definition.vegetationLayers.IndexOf(layer)));
                });
            RegisterModeContribution("prefab", "预制件", "批量对象布局", ESWorldWorkbenchModule.Prefab,
                ESWorkbenchContributionCategory.Prefab, new[] { "world.select", "world.prefab" },
                new[] { ESWorkbenchContentKind.Prefab }, "world.prefab", window => window.DrawPrefabLayers, 700, true,
                (context, window) =>
                {
                    if (window.mapAsset?.Definition?.scatterLayers == null) return;
                    ESWorldMapPrefabScatterLayer layer = window.FindScatterLayer("scatter.landmarks");
                    if (layer != null) context.RegisterAssetSlot(ESWorldMapWorkbenchSlots.Scatter(window.GetResourceLibraryPath(), window.mapAsset.Definition.scatterLayers.IndexOf(layer)));
                });
            RegisterModeContribution("navigation", "导航", "可行走坡度与烘焙参数", ESWorldWorkbenchModule.Navigation,
                ESWorkbenchContributionCategory.Navigation, new[] { "world.select" },
                new[] { ESWorkbenchContentKind.Navigation }, "world.select",
                window => () => window.DrawObject("导航 / AI 烘焙", "navigation"), 400);
            RegisterModeContribution("water-weather", "水体/天气", "环境与湿度", ESWorldWorkbenchModule.WaterWeather,
                ESWorkbenchContributionCategory.WaterWeather, new[] { "world.select" },
                new[] { ESWorkbenchContentKind.WaterWeather }, "world.select",
                window => () => window.DrawObject("水体 / 天气", "waterWeather"), 500);
            RegisterModeContribution("streaming", "流式检查", "区块半径与加载策略", ESWorldWorkbenchModule.Streaming,
                ESWorkbenchContributionCategory.Streaming, new[] { "world.select" },
                new[] { ESWorkbenchContentKind.Streaming }, "world.select",
                window => () => window.DrawObject("地形块流式", "streaming"), 200);
            RegisterModeContribution("collision", "碰撞", "碰撞器与物理材质", ESWorldWorkbenchModule.Collision,
                ESWorkbenchContributionCategory.Collision, new[] { "world.select" },
                new[] { ESWorkbenchContentKind.Collision }, "world.select",
                window => () => window.DrawObject("碰撞 / 物理", "collision"), 300);
            RegisterDocumentContribution("production", "生产与发布", "导出、预算和安全配额",
                ESWorldWorkbenchModule.UGC, ESWorkbenchContributionCategory.UGC,
                window => window.DrawBuildUgc, ESWorkbenchDirtyFlags.Build);
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
                        context.RegisterDocument(new ESWorkbenchDocumentDefinition(
                            "authoring", "世界创作", "持久世界视口与作者模式",
                            true, ESWorkbenchDirtyFlags.Authoring));
                        context.RegisterAuthoringMode(new ESWorkbenchAuthoringModeDefinition(
                            "region", "区域", "区域模板、边界与玩法承载范围",
                            new[] { "world.select", "world.region" },
                            new[] { ESWorkbenchContentKind.RegionTemplate },
                            "world.region", 600, true));
                        context.RegisterAuthoringMode(new ESWorkbenchAuthoringModeDefinition(
                            "poi", "POI", "兴趣点、出生点与玩法地标",
                            new[] { "world.select", "world.poi" },
                            new[] { ESWorkbenchContentKind.Gameplay },
                            "world.poi", 550, true));
                        context.RegisterPresentation(new ESWorkbenchHostPresentationDescriptor(
                            "world.presentation",
                            "ES 世界工作台",
                             "世界地图",
                             "世界视图",
                             "二维地图、三维世界与游戏构图视图",
                             "世界检查器",
                             WorldLayoutPolicy,
                             leftPanelTitle: "世界内容与层级",
                             workspaceTitle: "世界作者场景",
                             emptyState: new ESWorkbenchEmptyStateDescriptor(
                                 "创建或打开一张世界地图",
                                 "从空白作者资产开始，或打开内置商业验收样本检查内容库、层级、2D / 3D / 游戏构图、检查器与生产任务布局。",
                                 "world.create-map",
                                 "world.load-commercial-sample",
                                 "此启动面不会创建 PreviewScene，也不会修改正式 Scene、TerrainData、导航或发布产物。")));
                        context.RegisterBottomPanel(new ESWorkbenchBottomPanelDescriptor(
                            "diagnostics",
                            "诊断与验收",
                            _ => new ESWorkbenchBottomPanelContent(window.CreateWorldDiagnosticsHub()),
                            "世界状态、预览资源、事务证据和商业视觉验收",
                            600,
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
                "world.game", "游戏构图", ESWorkbenchViewportKind.Game,
                viewportContext => new ESWorldWorkbenchViewportAdapter(this, viewportContext, ESWorkbenchViewportKind.Game),
                "使用 PreviewScene 近似检查运行时透视构图；非 Unity Game View，不写入作者数据", priority: 80));

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

        private VisualElement CreateWorldDiagnosticsHub()
        {
            var root = new VisualElement { name = "ESWorldDiagnosticsHub" };
            root.style.flexGrow = 1f;
            var tabs = new VisualElement { name = "ESWorldDiagnosticsTabs" };
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.height = 30f;
            tabs.style.flexShrink = 0f;
            tabs.style.paddingLeft = 6f;
            tabs.style.paddingRight = 6f;
            var content = new VisualElement { name = "ESWorldDiagnosticsContent" };
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            Action<Func<VisualElement>> show = factory =>
            {
                content.Clear();
                VisualElement view = factory?.Invoke();
                if (view != null) content.Add(view);
            };
            var status = new Button(() => show(CreateWorldStatusPanel)) { text = "世界状态" };
            var resources = new Button(() => show(CreatePreviewResourcePanel)) { text = "预览资源" };
            var acceptance = new Button(() => show(CreateAcceptancePanel)) { text = "商业验收" };
            status.tooltip = "当前世界草稿、事务与构建状态";
            resources.tooltip = "预览场景、相机、渲染纹理、临时对象与清理统计";
            acceptance.tooltip = "Undo/Redo、多窗口冲突、视觉矩阵和预览长压证据";
            tabs.Add(status);
            tabs.Add(resources);
            tabs.Add(acceptance);
            root.Add(tabs);
            root.Add(content);
            show(CreateWorldStatusPanel);
            return root;
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
            if (editSession == null) return root;

            editSession.RefreshExternalConflict();
            ESWorldEditSessionConsistencySnapshot snapshot = editSession.CaptureConsistencySnapshot();
            root.Add(new Label("活动窗口：" + snapshot.ActiveOwnerSessionIds.Count
                + " · 当前 Owner " + AbbreviateIdentity(snapshot.OwnerSessionId)));
            root.Add(new Label("变更集：" + snapshot.ChangeCount
                + " · Draft " + AbbreviateHash(snapshot.DraftHash)
                + " · Source " + AbbreviateHash(snapshot.CurrentSourceHash)));
            Label consistency = new Label("一致性：" + snapshot.Summary);
            consistency.style.color = snapshot.Passed
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
            consistency.style.whiteSpace = WhiteSpace.Normal;
            root.Add(consistency);

            if (snapshot.HasExternalConflict)
            {
                Label conflict = new Label(
                    "冲突：正式 Source 已由其他窗口或工具改变。当前策略拒绝后写，不执行自动合并；"
                    + "冲突来源 " + AbbreviateIdentity(snapshot.ConflictOwnerSessionId)
                    + "。请复制诊断检查差异，或明确放弃本窗口草稿后重载正式资产。");
                conflict.style.whiteSpace = WhiteSpace.Normal;
                conflict.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
                conflict.style.marginTop = 5f;
                root.Add(conflict);
            }

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 6f;
            Button copy = new Button(() =>
            {
                EditorGUIUtility.systemCopyBuffer = snapshot.ToDiagnosticText();
                ESWorkbench_SetStatus("已复制世界会话一致性诊断。", MessageType.Info);
            })
            {
                text = "复制诊断",
                tooltip = "复制完整 Owner、Hash、ChangeSet、SessionState 和冲突一致性信息"
            };
            actions.Add(copy);
            Button verify = new Button(VerifyWorldSessionConsistency)
            {
                text = "验证一致性",
                tooltip = "重新计算 Draft Hash、ChangeSet、Dirty、SessionState 和外部 Source 冲突"
            };
            verify.style.marginLeft = 5f;
            actions.Add(verify);
            if (snapshot.HasExternalConflict || !snapshot.Passed)
            {
                Button reload = new Button(ReloadWorldFromSource)
                {
                    text = "重载正式资产",
                    tooltip = "放弃当前窗口草稿并从正式 Source 建立新基线；有未提交变更时会再次确认"
                };
                reload.style.marginLeft = 5f;
                actions.Add(reload);
            }
            root.Add(actions);
            return root;
        }

        private void VerifyWorldSessionConsistency()
        {
            if (editSession == null)
            {
                ESWorkbench_SetStatus("当前没有可验证的 World 编辑会话。", MessageType.Warning);
                return;
            }
            editSession.RefreshExternalConflict();
            ESWorldEditSessionConsistencySnapshot snapshot = editSession.CaptureConsistencySnapshot();
            ESWorkbench_SetStatus(
                "会话一致性：" + snapshot.Summary,
                snapshot.Passed ? MessageType.Info : MessageType.Warning);
            ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.Explicit);
        }

        private static string AbbreviateHash(string value)
        {
            return string.IsNullOrEmpty(value) ? "--" : value.Substring(0, Mathf.Min(10, value.Length));
        }

        private static string AbbreviateIdentity(string value)
        {
            return string.IsNullOrEmpty(value) ? "外部工具/未知窗口"
                : value.Substring(0, Mathf.Min(8, value.Length));
        }

        private VisualElement CreatePreviewResourcePanel()
        {
            ESEditorPreviewDiagnosticsSnapshot snapshot =
                ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
            VisualElement root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            Label summary = new Label(snapshot.ToSummary());
            summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            summary.style.whiteSpace = WhiteSpace.Normal;
            root.Add(summary);
            root.Add(new Label("临时对象 " + snapshot.ActiveTemporaryObjectCount
                + " · ResourceScope " + snapshot.ActiveResourceScopeCount
                + " · 累计注册/释放 " + snapshot.TotalScopeRegistrations
                + "/" + snapshot.TotalScopeReleases));
            root.Add(new Label("清理运行 " + snapshot.CleanupRunCount
                + " · 失败 " + snapshot.CleanupFailureCount
                + " · 最近原因 " + (string.IsNullOrEmpty(snapshot.LastCleanupReason)
                    ? "尚未执行全局清理" : snapshot.LastCleanupReason)));
            Label boundary = new Label(
                "这里是确定性生命周期与受控重复重建自检；它不替代 Unity Profiler、Memory Profiler、长时间驻留或目标硬件显存验收。");
            boundary.style.whiteSpace = WhiteSpace.Normal;
            boundary.style.color = ESEditorPresentation.SectionMutedTextColor;
            boundary.style.marginTop = 4f;
            root.Add(boundary);
            if (!string.IsNullOrEmpty(previewStressResult))
            {
                Label result = new Label(previewStressResult);
                result.style.whiteSpace = WhiteSpace.Normal;
                result.style.marginTop = 5f;
                result.style.color = previewStressResult.StartsWith("通过", StringComparison.Ordinal)
                    ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                    : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
                root.Add(result);
            }
            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 6f;
            actions.Add(new Button(() => ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.Explicit))
            {
                text = "刷新统计",
                tooltip = "重新读取当前预览生命周期统计"
            });
            Button stress = new Button(RunWorldPreviewStressCheck)
            {
                text = "运行 24 次快速生命周期检查",
                tooltip = "显式创建、重建、调整 RT 尺寸并释放一个隔离的 World 预览；快速检查不承担商业长压持续时间门禁"
            };
            stress.style.marginLeft = 5f;
            actions.Add(stress);
            Button longStress = new Button(RunWorldPreviewLongStressCheck)
            {
                text = "运行商业长压（至少 240 次 / 30 秒）",
                tooltip = "在当前地图草稿上重复重建 PreviewScene、临时对象和 RT；次数与真实持续时间必须同时达标，并记录过程趋势"
            };
            longStress.style.marginLeft = 5f;
            actions.Add(longStress);
            Button copy = new Button(() =>
            {
                ESEditorPreviewDiagnosticsSnapshot current =
                    ESEditorPreviewLifecycleHub.CaptureDiagnosticsSnapshot();
                EditorGUIUtility.systemCopyBuffer = current.ToSummary()
                    + "\nCleanupRuns=" + current.CleanupRunCount
                    + "\nCleanupFailures=" + current.CleanupFailureCount
                    + "\nLastCleanupReason=" + current.LastCleanupReason
                    + "\nStress=" + previewStressResult;
                ESWorkbench_SetStatus("已复制预览资源诊断。", MessageType.Info);
            })
            {
                text = "复制诊断",
                tooltip = "复制当前资源统计、清理信息和最近压力检查结果"
            };
            copy.style.marginLeft = 5f;
            actions.Add(copy);
            root.Add(actions);
            return root;
        }

        private VisualElement CreateAcceptancePanel()
        {
            VisualElement root = new VisualElement();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            Label title = new Label("World 专项商业验收");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
            Label boundary = new Label(
                "显式运行会验证当前会话一致性，并在隔离样本上执行真实 Undo/Redo、SessionState 恢复和双窗口拒写；"
                + "同时只有在两个真实窗口同源冲突已观察到、当前窗口真实 TryCommit 被守卫拒绝、"
                + "Source 未变且本地草稿保留时才允许签收；预览长压使用当前草稿。"
                + "不会修改正式 Source，也不替代人工交互或 Unity Game View；商业长压必须同时达到至少 240 次和 30 秒，"
                + "完成后会自动请求真实 Memory Profiler 快照。")
            {
                tooltip = "验收清单写入 Library/ESWorkbench/Acceptance/world，提供可重读 UTF-8 manifest。"
            };
            boundary.style.whiteSpace = WhiteSpace.Normal;
            boundary.style.color = ESEditorPresentation.SectionMutedTextColor;
            boundary.style.marginTop = 4f;
            root.Add(boundary);

            string currentSourcePath = ESWorkbench_Asset == null
                ? string.Empty : AssetDatabase.GetAssetPath(ESWorkbench_Asset);
            string currentSourceGuid = string.IsNullOrWhiteSpace(currentSourcePath)
                ? string.Empty : AssetDatabase.AssetPathToGUID(currentSourcePath);
            if (latestAcceptance.HasValue
                && (!string.Equals(
                        latestAcceptance.Value.SourceAssetGuid,
                        currentSourceGuid,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        latestAcceptance.Value.AssemblyModuleVersionId,
                        ESWorldWorkbenchAcceptance.CurrentAssemblyModuleVersionId,
                        StringComparison.OrdinalIgnoreCase)))
                latestAcceptance = null;
            if (!latestAcceptance.HasValue
                && ESWorldWorkbenchAcceptance.TryGetLatest(
                    ESWorkbench_Asset,
                    out ESWorldWorkbenchAcceptanceResult restored))
                latestAcceptance = restored;
            if (latestAcceptance.HasValue)
            {
                ESWorldWorkbenchAcceptanceResult latest = latestAcceptance.Value;
                Label verdict = new Label(latest.Message);
                verdict.style.whiteSpace = WhiteSpace.Normal;
                verdict.style.marginTop = 6f;
                verdict.style.color = latest.Accepted
                    ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                    : latest.AutomatedChecksPassed
                        ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning)
                        : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
                root.Add(verdict);
                Label path = new Label(latest.ManifestPath)
                {
                    tooltip = latest.ManifestPath
                };
                path.style.whiteSpace = WhiteSpace.Normal;
                path.style.marginTop = 3f;
                path.style.color = ESEditorPresentation.SectionMutedTextColor;
                root.Add(path);
            }

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 7f;
            bool memoryProfilerSupported = ESWorldWorkbenchAcceptance
                .IsMemoryProfilerCaptureSupported(out string memoryProfilerStatus);
            Label memoryProfiler = new Label(memoryProfilerStatus);
            memoryProfiler.style.whiteSpace = WhiteSpace.Normal;
            memoryProfiler.style.marginTop = 6f;
            memoryProfiler.style.color = memoryProfilerSupported
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
            root.Add(memoryProfiler);
            actions.Add(new Button(() => RunWorldAcceptance(24))
            {
                text = "运行专项快速验收（24 次）",
                tooltip = "运行会话、真实同源拒写与 24 次 PreviewScene/Camera/临时对象/RT 重建；不承担 30 秒商业长压门禁"
            });
            Button longRun = new Button(() => RunWorldAcceptance(
                CommercialValidationPreviewIterations,
                true,
                CommercialValidationMinimumDurationSeconds))
            {
                text = "运行商业长压验收（至少 240 次 / 30 秒）",
                tooltip = "运行完整专项并执行次数与持续时间双门禁的可取消预览长压；记录生命周期过程趋势并自动挂接真实 Memory Profiler 快照"
            };
            longRun.style.marginLeft = 5f;
            actions.Add(longRun);
            Button collaborationState = new Button(InspectCollaborationSessionState)
            {
                text = "检查协作状态",
                tooltip = "检查当前唯一 World 工作台的受管会话、外部冲突与草稿状态；不会创建第二个同类型窗口"
            };
            collaborationState.SetEnabled(ESWorkbench_Asset != null);
            collaborationState.style.marginLeft = 5f;
            actions.Add(collaborationState);
            if (latestAcceptance.HasValue)
            {
                ESWorldWorkbenchAcceptanceResult latest = latestAcceptance.Value;
                Button open = new Button(() =>
                {
                    if (!ESWorldWorkbenchAcceptance.TryOpenManifest(latest.ManifestPath))
                        ESWorkbench_SetStatus("验收清单未通过安全检查或已经不存在。", MessageType.Warning);
                })
                {
                    text = "打开清单",
                    tooltip = "用系统默认程序打开最近一次 UTF-8 验收清单"
                };
                open.style.marginLeft = 5f;
                actions.Add(open);
                Button directory = new Button(() =>
                {
                    if (!ESWorldWorkbenchAcceptance.TryRevealDirectory(latest.RunDirectory))
                        ESWorkbench_SetStatus("验收目录未通过安全检查或已经不存在。", MessageType.Warning);
                })
                {
                    text = "打开目录",
                    tooltip = "在文件管理器中显示最近一次验收运行"
                };
                directory.style.marginLeft = 5f;
                actions.Add(directory);
                Button copy = new Button(() =>
                {
                    EditorGUIUtility.systemCopyBuffer = latest.ManifestPath;
                    ESWorkbench_SetStatus("已复制验收清单绝对路径。", MessageType.Info);
                })
                {
                    text = "复制路径",
                    tooltip = "复制最近一次验收清单的绝对路径"
                };
                copy.style.marginLeft = 5f;
                actions.Add(copy);
                Button snapshot = new Button(() => CaptureWorldMemoryProfilerSnapshot(latest))
                {
                    text = "采集 Memory 快照",
                    tooltip = memoryProfilerSupported
                        ? "生成真实 .snap 并挂接到最近一次验收清单"
                        : "当前项目没有可用的 Memory Profiler 快照 API"
                };
                snapshot.SetEnabled(memoryProfilerSupported);
                snapshot.style.marginLeft = 5f;
                actions.Add(snapshot);
            }
            root.Add(actions);
            return root;
        }

        private void RunWorldAcceptance(
            int previewIterations,
            bool captureMemoryProfilerSnapshot = false,
            double previewMinimumDurationSeconds = 0d)
        {
            if (mapAsset == null || editSession == null)
            {
                ESWorkbench_SetStatus("未绑定 World 编辑会话，无法运行专项验收。", MessageType.Warning);
                return;
            }

            string taskId = "world.acceptance." + previewIterations;
            ESWorkbench_RecordTask(
                taskId,
                "Running",
                "正在运行 World 会话、冲突和预览长压专项验收。",
                AssetDatabase.GetAssetPath(editSession.Source));
            ESWorldWorkbenchAcceptanceResult result =
                ESWorldWorkbenchAcceptance.Execute(
                    mapAsset,
                    editSession,
                    WorldLayoutPolicy.CreateCommercialVisualMatrix(),
                    previewIterations,
                    true,
                    previewMinimumDurationSeconds);
            latestAcceptance = result;
            string status = result.Cancelled ? "Cancelled"
                : result.Success && result.AutomatedChecksPassed ? "Succeeded"
                : "Failed";
            ESWorkbench_RecordTask(
                taskId,
                status,
                result.Message,
                result.ManifestPath);
            ESWorkbench_RecordLog(
                "World 专项验收：" + result.Message,
                result.AutomatedChecksPassed ? MessageType.Info : MessageType.Warning);
            ESWorkbench_SetStatus(
                result.Message,
                result.Accepted ? MessageType.Info
                    : result.AutomatedChecksPassed ? MessageType.Warning : MessageType.Error);
            ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.Explicit);
            if (captureMemoryProfilerSnapshot
                && result.Success
                && result.AutomatedChecksPassed
                && !result.Cancelled)
                CaptureWorldMemoryProfilerSnapshot(result);
        }

        private void CaptureWorldMemoryProfilerSnapshot(
            ESWorldWorkbenchAcceptanceResult acceptance)
        {
            const string taskId = "world.acceptance.memory-profiler";
            ESWorkbench_RecordTask(
                taskId,
                "Running",
                "正在采集 World Memory Profiler 快照。",
                acceptance.ManifestPath);
            ESWorkbench_SetStatus("正在启动 Memory Profiler 快照，请等待完成回调。", MessageType.Info);
            bool started = ESWorldWorkbenchAcceptance.TryCaptureMemoryProfilerSnapshot(
                acceptance,
                (success, message, updated) =>
                {
                    latestAcceptance = updated;
                    ESWorkbench_RecordTask(
                        taskId,
                        success ? "Succeeded" : "Failed",
                        message,
                        success ? updated.RunDirectory : updated.ManifestPath);
                    ESWorkbench_RecordLog(message, success ? MessageType.Info : MessageType.Warning);
                    ESWorkbench_SetStatus(message, success ? MessageType.Info : MessageType.Warning);
                    ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.Explicit);
                });
            if (!started)
            {
                ESWorkbench_RecordTask(
                    taskId,
                    "Failed",
                    "Memory Profiler 快照未能启动。",
                    acceptance.ManifestPath);
                ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.Explicit);
            }
        }

        private void RunWorldPreviewStressCheck()
        {
            RunWorldPreviewStressCheck(24);
        }

        private void RunWorldPreviewLongStressCheck()
        {
            RunWorldPreviewStressCheck(
                CommercialValidationPreviewIterations,
                CommercialValidationMinimumDurationSeconds);
        }

        private void RunWorldPreviewStressCheck(
            int iterations,
            double minimumDurationSeconds = 0d)
        {
            ESWorldAcceptancePreviewEvidence evidence =
                ESWorldWorkbenchAcceptance.RunPreviewStress(
                    mapAsset,
                    iterations,
                    true,
                    minimumDurationSeconds);
            previewStressResult = (evidence.passed ? "通过" : evidence.cancelled ? "已取消" : "失败")
                + " · " + evidence.summary
                + " · 趋势 " + (evidence.lifecycleTrendStable ? "稳定" : "异常")
                + " · Scope " + evidence.activeScopeDelta
                + " · 临时对象 " + evidence.activeTemporaryObjectDelta
                + " · RT 字节差 " + evidence.estimatedRenderTextureByteDelta.ToString("N0")
                + " · 峰值 " + evidence.peakEstimatedRenderTextureBytes.ToString("N0") + " B"
                + " · Memory Profiler 未采集";
            ESWorkbench_SetStatus(
                previewStressResult,
                previewStressResult.StartsWith("通过", StringComparison.Ordinal)
                    ? MessageType.Info : MessageType.Error);
            ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.Explicit);
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
            context.RegisterTool(CreateWorldTool(
                "world.select", "选择", ESWorldAuthoringTool.Select, KeyCode.Q, 500, "d_ViewToolMove"));
            context.RegisterTool(CreateWorldTool(
                "world.terrain", "地形笔刷", ESWorldAuthoringTool.Terrain, KeyCode.W, 490, "Terrain Icon"));
            context.RegisterTool(CreateWorldTool(
                "world.region", "区域", ESWorldAuthoringTool.Region, KeyCode.E, 480, "RectTool"));
            context.RegisterTool(CreateWorldTool(
                "world.poi", "POI", ESWorldAuthoringTool.Poi, KeyCode.R, 470, "d_SceneViewOrtho"));
            context.RegisterTool(CreateWorldTool(
                "world.prefab", "预制件放置", ESWorldAuthoringTool.Prefab, KeyCode.T, 460, "Prefab Icon"));
        }

        private ESWorkbenchToolDescriptor CreateWorldTool(
            string id,
            string title,
            ESWorldAuthoringTool tool,
            KeyCode shortcut,
            int priority,
            string unityIconName)
        {
            return new ESWorkbenchToolDescriptor(id, title, actions =>
            {
                authoringTool = tool;
                actions.SetStatus("当前世界工具：" + title, MessageType.Info);
            }, title,
                icon: EditorGUIUtility.IconContent(unityIconName).image,
                priority: priority,
                isAvailable: _ => mapAsset != null,
                shortcut: new ESWorkbenchShortcut(shortcut),
                capabilities: tool == ESWorldAuthoringTool.Terrain
                    ? ESWorkbenchToolCapabilities.Paint
                    : tool == ESWorldAuthoringTool.Select
                        ? ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                    : tool == ESWorldAuthoringTool.Prefab
                        ? ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.GroundAction
                    : tool == ESWorldAuthoringTool.Region || tool == ESWorldAuthoringTool.Poi
                        ? ESWorkbenchToolCapabilities.Select | ESWorkbenchToolCapabilities.Move
                            | ESWorkbenchToolCapabilities.GroundAction
                        : ESWorkbenchToolCapabilities.None);
        }

        private void RegisterWorldCommands(ESWorkbenchContributionContext context)
        {
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.create-map", "创建世界地图", _ => CreateAsset(),
                "创建新的 ESWorldMapAsset 作者资产；不会创建正式 Scene 或 TerrainData。",
                priority: 1000,
                canExecute: _ => mapAsset == null,
                showInToolbar: false,
                role: ESWorkbenchCommandRole.Primary,
                visibility: ESWorkbenchCommandVisibility.Pinned,
                unityIconName: "CreateAddNew"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.initialize-defaults", "填充安全默认值", _ => InitializeDefaults(),
                "为当前世界草稿填充安全默认参数，不注入商业样例内容。",
                priority: 990,
                canExecute: _ => mapAsset != null,
                showInToolbar: false,
                role: ESWorkbenchCommandRole.Authoring,
                unityIconName: "Settings"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.load-commercial-sample", "打开商业验收样本",
                _ => OpenCommercialValidationSample(),
                "创建或刷新项目内固定验收样本并在当前单实例工作台中打开。",
                priority: 980,
                showInToolbar: false,
                role: ESWorkbenchCommandRole.Validation,
                visibility: ESWorkbenchCommandVisibility.Pinned,
                unityIconName: "SceneAsset Icon"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.validate", "验证", _ => ValidateMap(), "验证当前世界草稿",
                priority: 500,
                shortcut: new ESWorkbenchShortcut(KeyCode.V, EventModifiers.Control),
                canExecute: _ => mapAsset != null,
                role: ESWorkbenchCommandRole.Validation,
                visibility: ESWorkbenchCommandVisibility.Pinned,
                unityIconName: "TestPassed"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.reload-source", "重载正式资产", _ => ReloadWorldFromSource(),
                "放弃当前草稿并从正式地图建立新基线",
                priority: 495,
                canExecute: _ => editSession != null,
                role: ESWorkbenchCommandRole.Dangerous,
                unityIconName: "d_Refresh"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.inspect-collaboration-state", "检查协作状态", _ => InspectCollaborationSessionState(),
                "检查当前工作台的受管会话、外部冲突与草稿状态",
                priority: 492,
                canExecute: _ => ESWorkbench_Asset != null,
                role: ESWorkbenchCommandRole.Validation,
                unityIconName: "d_UnityEditor.InspectorWindow"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.revert", "回退草稿", _ => RevertWorldDraft(), "回退到当前编辑会话基线",
                priority: 490,
                canExecute: _ => editSession != null && editSession.IsDirty
                    && !ESWorkbench_IsHierarchyLocked("world.map"),
                role: ESWorkbenchCommandRole.Dangerous,
                unityIconName: "d_Refresh"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.brush", "笔刷", actions => OpenBrushPopup(actions), "设置世界地形笔刷参数",
                priority: 480,
                canExecute: _ => mapAsset != null,
                role: ESWorkbenchCommandRole.Authoring,
                unityIconName: "Terrain Icon"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.build-preflight", "构建预检", _ => PreviewBake(), "验证构建输入并生成可提交的 Bake 请求",
                priority: 470,
                canExecute: _ => mapAsset != null,
                role: ESWorkbenchCommandRole.Build,
                unityIconName: "d_PreMatCube"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.build", "构建", _ => CommitBake(), "提交已经通过预检的 World Bake 请求",
                priority: 460,
                canExecute: _ => mapAsset != null && !string.IsNullOrEmpty(bakeRequestId),
                role: ESWorkbenchCommandRole.Build,
                unityIconName: "BuildSettings.Editor.Small"));
            context.RegisterCommand(new ESWorkbenchCommandDescriptor(
                "world.formal-output", "正式输出（另行推进）",
                _ => ESWorkbench_SetStatus(
                    "正式 TerrainData、Scene、碰撞、导航与发布输出不属于本轮工作台强化范围，事务门禁保持关闭。",
                    MessageType.Warning),
                "本轮只完善作者工作台；正式 TerrainData、Scene、导航与发布事务另行验收后开放。",
                priority: 450,
                canExecute: _ => false,
                role: ESWorkbenchCommandRole.Dangerous,
                unityIconName: "d_SceneAsset Icon"));
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

        private void InspectCollaborationSessionState()
        {
            ESWorldMapAsset source = ESWorkbench_Asset;
            if (source == null)
            {
                ESWorkbench_SetStatus("未绑定正式 World Source，无法检查协作状态。", MessageType.Warning);
                return;
            }
            bool conflict = editSession != null && editSession.RefreshExternalConflict();
            int activeSessions = ESWorldEditSession.GetActiveSessionCount(source);
            ESWorkbench_SetStatus(
                "协作状态：活动会话 " + activeSessions
                + " 个，外部冲突 " + (conflict ? "已检测" : "无")
                + "，当前草稿 " + (editSession?.IsDirty == true ? "有未提交变更" : "干净") + "。",
                conflict ? MessageType.Warning : MessageType.Info);
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
                canScale: IsWorldScalableSelection,
                scale: ScaleWorldSelectionMutation);
        }

        private IEnumerable<ESWorkbenchObjectDescriptor> QueryWorldPalette()
        {
            for (int i = 0; i < WorldBrushTemplates.Length; i++)
            {
                WorldBrushPaletteTemplate brush = WorldBrushTemplates[i];
                yield return new ESWorkbenchObjectDescriptor(
                    "world.brush." + brush.Id,
                    brush.DisplayName,
                    "地形塑形",
                    null,
                    brush,
                    tooltip: brush.Description,
                    priority: 420 - i,
                    subtitle: GetBrushTemplateSubtitle(brush),
                    badge: "拖入绘制",
                    contentKind: ESWorkbenchContentKind.Brush,
                    dragMode: ESWorkbenchContentDragMode.ActivateTool,
                    selectionKind: "world.content.brush",
                    presets: CreateBrushPresets(brush));
            }

            for (int i = 0; i < WorldRegionTemplates.Length; i++)
            {
                WorldRegionPaletteTemplate region = WorldRegionTemplates[i];
                yield return new ESWorkbenchObjectDescriptor(
                    "world.region-template." + region.Id,
                    region.DisplayName,
                    "区域模板",
                    null,
                    region,
                    tooltip: region.Description,
                    priority: 380 - i,
                    subtitle: region.Size.x.ToString("0.#") + " × " + region.Size.y.ToString("0.#") + " 米",
                    badge: "拖入创建",
                    contentKind: ESWorkbenchContentKind.RegionTemplate,
                    dragMode: ESWorkbenchContentDragMode.CreateRegion,
                    selectionKind: "world.content.region",
                    presets: CreateRegionPresets(region));
            }

            IReadOnlyList<ESAssetPage> pages = ESAssetRegistry.Pages;
            for (int i = 0; i < pages.Count; i++)
            {
                ESAssetPage page = pages[i];
                if (page?.OB == null) continue;
                string key = page.EffectiveStringKey;
                if (string.IsNullOrWhiteSpace(key)) key = page.AssetGuid;
                if (string.IsNullOrWhiteSpace(key)) continue;
                string category = string.IsNullOrWhiteSpace(page.SourceBook) ? page.Kind.ToString() : page.SourceBook;
                if (page.OB is GameObject prefab && PrefabUtility.IsPartOfPrefabAsset(prefab))
                {
                    yield return new ESWorkbenchObjectDescriptor(
                        "world.asset." + key,
                        page.OB.name,
                        ResolveWorldAssetCategory(page, category),
                        page.OB,
                        page,
                        null,
                        key,
                        200,
                        AssetDatabase.GetAssetPath(page.OB),
                        "预制件",
                        ESWorkbenchContentKind.Prefab,
                        ESWorkbenchContentDragMode.Place,
                        "world.content.prefab");
                }
                else if (page.OB is SceneAsset)
                {
                    yield return new ESWorkbenchObjectDescriptor(
                        "world.scene-template." + key,
                        page.OB.name,
                        "注册场景",
                        page.OB,
                        page,
                        null,
                        "拖入后将当前地图来源切换为该注册场景；不会直接打开或改写正式 Scene。",
                        260,
                        AssetDatabase.GetAssetPath(page.OB),
                        "场景模板",
                        ESWorkbenchContentKind.SceneTemplate,
                        ESWorkbenchContentDragMode.ApplyTemplate,
                        "world.content.scene");
                }
            }
        }

        private static IReadOnlyList<ESWorkbenchContentPresetDescriptor> CreateBrushPresets(
            WorldBrushPaletteTemplate brush)
        {
            if (brush.Mode != ESWorldTerrainBrushMode.Flatten)
            {
                float standard = brush.Strength < 0f ? 0.5f : brush.Strength;
                return new[]
                {
                    CreateBrushStrengthPreset(brush, "gentle", "轻柔", Mathf.Clamp01(standard * 0.55f)),
                    CreateBrushStrengthPreset(brush, "standard", "标准", standard),
                    CreateBrushStrengthPreset(brush, "strong", "强力", Mathf.Clamp01(standard * 1.55f))
                };
            }
            float lower = Mathf.Clamp01(brush.NormalizedHeight - 0.12f);
            float higher = Mathf.Clamp01(brush.NormalizedHeight + 0.12f);
            return new[]
            {
                new ESWorkbenchContentPresetDescriptor(
                    "lower",
                    "较低目标",
                    "在当前笔刷语义下使用更低的目标高度。",
                    new WorldBrushPaletteTemplate(
                        brush.Id + ".lower", brush.DisplayName + " · 较低目标", lower, brush.Description, brush.Mode, brush.Strength),
                    overridePayload: true,
                    subtitle: "目标高度 " + Mathf.RoundToInt(lower * 100f) + "%",
                    badge: "较低"),
                new ESWorkbenchContentPresetDescriptor(
                    "standard",
                    "标准目标",
                    "使用模板定义的标准目标高度。",
                    brush,
                    overridePayload: true,
                    subtitle: "目标高度 " + Mathf.RoundToInt(brush.NormalizedHeight * 100f) + "%",
                    badge: "标准"),
                new ESWorkbenchContentPresetDescriptor(
                    "higher",
                    "较高目标",
                    "在当前笔刷语义下使用更高的目标高度。",
                    new WorldBrushPaletteTemplate(
                        brush.Id + ".higher", brush.DisplayName + " · 较高目标", higher, brush.Description, brush.Mode, brush.Strength),
                    overridePayload: true,
                    subtitle: "目标高度 " + Mathf.RoundToInt(higher * 100f) + "%",
                    badge: "较高")
            };
        }

        private static ESWorkbenchContentPresetDescriptor CreateBrushStrengthPreset(
            WorldBrushPaletteTemplate brush,
            string presetId,
            string displayName,
            float strength)
        {
            int percent = Mathf.RoundToInt(strength * 100f);
            return new ESWorkbenchContentPresetDescriptor(
                presetId,
                displayName + "强度",
                "使用" + displayName + "强度执行" + GetTerrainBrushModeDisplayName(brush.Mode) + "笔划。",
                new WorldBrushPaletteTemplate(
                    brush.Id + "." + presetId,
                    brush.DisplayName + " · " + displayName,
                    brush.NormalizedHeight,
                    brush.Description,
                    brush.Mode,
                    strength),
                overridePayload: true,
                subtitle: "强度 " + percent + "%",
                badge: displayName);
        }

        private static string GetBrushTemplateSubtitle(WorldBrushPaletteTemplate brush)
        {
            if (brush.Mode == ESWorldTerrainBrushMode.Flatten)
                return "平整 · 目标高度 " + Mathf.RoundToInt(brush.NormalizedHeight * 100f) + "%";
            float strength = brush.Strength < 0f ? 0.5f : brush.Strength;
            return GetTerrainBrushModeDisplayName(brush.Mode) + " · 强度 "
                + Mathf.RoundToInt(strength * 100f) + "%";
        }

        private static IReadOnlyList<ESWorkbenchContentPresetDescriptor> CreateRegionPresets(
            WorldRegionPaletteTemplate region)
        {
            return new[]
            {
                CreateRegionSizePreset(region, "small", "小型", 0.65f),
                CreateRegionSizePreset(region, "standard", "标准", 1f),
                CreateRegionSizePreset(region, "large", "大型", 1.5f)
            };
        }

        private static ESWorkbenchContentPresetDescriptor CreateRegionSizePreset(
            WorldRegionPaletteTemplate region,
            string presetId,
            string displayName,
            float scale)
        {
            Vector2 size = region.Size * scale;
            return new ESWorkbenchContentPresetDescriptor(
                presetId,
                displayName,
                "以“" + displayName + "”尺寸创建该区域模板。",
                new WorldRegionPaletteTemplate(
                    region.Id + "." + presetId,
                    region.DisplayName + " · " + displayName,
                    region.Category,
                    size,
                    region.Description),
                overridePayload: true,
                subtitle: size.x.ToString("0.#") + " × " + size.y.ToString("0.#") + " 米",
                badge: displayName);
        }

        internal IReadOnlyList<ESWorkbenchObjectDescriptor> QueryWorldPaletteForTest()
        {
            return QueryWorldPalette().ToArray();
        }

        internal bool CanUsePaletteItem(ESWorkbenchObjectDescriptor item, out string reason)
        {
            reason = string.Empty;
            if (item == null)
            {
                reason = "没有可用的世界内容。";
                return false;
            }
            if (editSession?.Draft?.Definition == null)
            {
                reason = "请先选择或创建世界地图，建立作者草稿后再使用内容。";
                return false;
            }
            if (ESWorkbench_IsHierarchyLocked("world.map"))
            {
                reason = "世界根节点已锁定，不能修改当前草稿。";
                return false;
            }
            switch (item.DragMode)
            {
                case ESWorkbenchContentDragMode.Place:
                    if (!(item.Source is GameObject prefab) || !PrefabUtility.IsPartOfPrefabAsset(prefab))
                    {
                        reason = "放置内容必须是 Project 中已注册的 Prefab。";
                        return false;
                    }
                    if (!(item.Payload is ESAssetPage prefabPage)
                        || string.IsNullOrWhiteSpace(prefabPage.EffectiveStringKey))
                    {
                        reason = "预制件内容缺少有效的注册页或稳定资源 Key。";
                        return false;
                    }
                    if (ESWorkbench_Actions?.Authoring?.CanCreate(item) != true)
                    {
                        reason = "当前作者适配器不接受该预制件。";
                        return false;
                    }
                    return true;
                case ESWorkbenchContentDragMode.ActivateTool:
                    if (!(item.Payload is WorldBrushPaletteTemplate))
                    {
                        reason = "笔刷内容缺少有效的世界笔刷模板。";
                        return false;
                    }
                    return true;
                case ESWorkbenchContentDragMode.CreateRegion:
                    if (!(item.Payload is WorldRegionPaletteTemplate))
                    {
                        reason = "区域内容缺少有效的区域模板。";
                        return false;
                    }
                    return true;
                case ESWorkbenchContentDragMode.ApplyTemplate:
                    if (!(item.Source is SceneAsset sceneAsset))
                    {
                        reason = "场景模板必须来自已注册的 Unity Scene 资产。";
                        return false;
                    }
                    if (!(item.Payload is ESAssetPage scenePage)
                        || string.IsNullOrWhiteSpace(scenePage.EffectiveStringKey))
                    {
                        reason = "场景内容缺少有效的注册页或稳定资源 Key。";
                        return false;
                    }
                    return true;
                default:
                    reason = "该内容当前仅支持查看，不能拖入作者视口。";
                    return false;
            }
        }

        internal Vector3 ResolvePalettePreviewSize(ESWorkbenchObjectDescriptor item)
        {
            ESWorldMapDefinition definition = editSession?.Draft?.Definition;
            if (item?.Payload is WorldRegionPaletteTemplate region)
                return new Vector3(
                    Mathf.Max(0.1f, region.Size.x),
                    Mathf.Max(1f, definition?.terrainHeightScale * 0.04f ?? 1f),
                    Mathf.Max(0.1f, region.Size.y));
            if (item?.ContentKind == ESWorkbenchContentKind.Brush)
            {
                float diameter = GetTerrainBrushRadius() * 2f;
                return new Vector3(diameter, 0.2f, diameter);
            }
            if (item?.ContentKind == ESWorkbenchContentKind.SceneTemplate && definition != null)
                return new Vector3(
                    Mathf.Max(1f, definition.worldMax.x - definition.worldMin.x),
                    Mathf.Max(1f, definition.terrainHeightScale),
                    Mathf.Max(1f, definition.worldMax.y - definition.worldMin.y));
            return Vector3.one * 2f;
        }

        internal bool TryUsePaletteItem(ESWorkbenchObjectDescriptor item, Vector3 worldPoint, out string message)
        {
            if (!CanUsePaletteItem(item, out message)) return false;
            activeWorldContentId = item.BaseObjectId;
            activeWorldContentPresetId = item.PresetId;
            switch (item.DragMode)
            {
                case ESWorkbenchContentDragMode.Place:
                    authoringTool = ESWorldAuthoringTool.Prefab;
                    ESWorkbench_Actions?.Tools?.Activate("world.prefab");
                    return ESWorkbench_Actions.Authoring.TryCreate(item, worldPoint, out message);
                case ESWorkbenchContentDragMode.ActivateTool:
                    WorldBrushPaletteTemplate brush = (WorldBrushPaletteTemplate)item.Payload;
                    authoringBrushHeight = brush.NormalizedHeight;
                    authoringBrushMode = brush.Mode;
                    if (brush.Strength >= 0f) authoringBrushStrength = brush.Strength;
                    // 卡片拖入是一次性笔刷操作，不应夺走当前区域/对象移动工具。
                    // 连续笔划测试和视口内笔刷仍由已激活的 world.terrain 工具驱动。
                    if (terrainStrokeSessionOpen)
                    {
                        authoringTool = ESWorldAuthoringTool.Terrain;
                        ESWorkbench_Actions?.Tools?.Activate("world.terrain");
                        return TryPaintWorldHeight(worldPoint, out message);
                    }
                    ESWorldAuthoringTool previousTool = authoringTool;
                    string previousToolId = ESWorkbench_Actions?.Tools?.ActiveToolId ?? string.Empty;
                    bool painted = TryPaintWorldHeight(worldPoint, out message);
                    authoringTool = previousTool;
                    if (ESWorkbench_Actions?.Tools != null)
                    {
                        if (string.IsNullOrWhiteSpace(previousToolId)) ESWorkbench_Actions.Tools.Clear();
                        else ESWorkbench_Actions.Tools.Activate(previousToolId);
                    }
                    return painted;
                case ESWorkbenchContentDragMode.CreateRegion:
                    authoringTool = ESWorldAuthoringTool.Region;
                    ESWorkbench_Actions?.Tools?.Activate("world.region");
                    return TryAddWorldRegion(worldPoint, (WorldRegionPaletteTemplate)item.Payload, out message);
                case ESWorkbenchContentDragMode.ApplyTemplate:
                    return TryApplyWorldSceneTemplate(item, out message);
                default:
                    message = "该内容没有可执行的作者动作。";
                    return false;
            }
        }

        private bool TryApplyWorldSceneTemplate(ESWorkbenchObjectDescriptor item, out string message)
        {
            if (!(item?.Source is SceneAsset sceneAsset))
            {
                message = "场景模板资产已经失效。";
                return false;
            }
            if (!ESWorkbenchContentRegistration.TryResolveRegisteredAsset(
                    sceneAsset,
                    ESAssetReferKind.Scene,
                    out ESAssetPage page,
                    out string error))
            {
                message = error;
                return false;
            }
            string key = page.EffectiveStringKey;
            if (string.IsNullOrWhiteSpace(key)) key = page.AssetGuid;
            if (string.IsNullOrWhiteSpace(key))
            {
                message = "注册场景缺少稳定资源 Key。";
                return false;
            }
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            Undo.RecordObject(editSession.Draft, "应用世界场景模板");
            definition.sourceMode = ESWorldMapSourceMode.Scene;
            definition.sceneAssetKey = key;
            NotifyWorldDraftChanged("definition.sceneAssetKey", false);
            ESWorkbench_Selection.Select(item.ToSelection());
            message = "场景模板已绑定到世界草稿：" + key + "。正式 Scene 未被打开或改写。";
            ESWorkbench_SetStatus(message, MessageType.Info);
            return true;
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
                : kind == ESWorkbenchViewportKind.Game ? "玩家 / 第三人称构图近似" : "透视作者相机";
            string gizmo = ResolveAuthoringToolDisplayName(authoringTool);
            string collision = definition?.collision == null ? "未配置"
                : definition.collision.terrainCollider ? "地形碰撞开启" : "地形碰撞关闭";
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
                    "正式输出的地形碰撞体状态", 200),
                new ESWorkbenchViewportStatusDescriptor("world.navigation", "导航", navigation,
                    "NavMesh 输出配置状态", 100)
            };
        }

        private static string ResolveAuthoringToolDisplayName(ESWorldAuthoringTool tool)
        {
            switch (tool)
            {
                case ESWorldAuthoringTool.Terrain: return "地形笔刷";
                case ESWorldAuthoringTool.Region: return "区域";
                case ESWorldAuthoringTool.Poi: return "兴趣点";
                case ESWorldAuthoringTool.Prefab: return "预制件放置";
                default: return "选择";
            }
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
                "2D/3D 视口编辑同一草稿，游戏构图预览只读且不等价 Unity Game View；TerrainData、正式 Scene、NavMeshData 与资源管线发布请求分别构建并验收。",
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

        private static void RegisterDocumentContribution(
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
                        Action drawDocument = draw(window);
                        context.RegisterDocument(new ESWorkbenchDocumentDefinition(
                            contributionId,
                            title,
                            tooltip,
                            false,
                            dirtyFlags,
                            draw: () =>
                            {
                                bool locked = window.ESWorkbench_IsHierarchyLocked("world.map");
                                if (locked)
                                    EditorGUILayout.HelpBox(
                                        "世界根节点已锁定，当前文档处于只读状态。解锁后才能修改作者草稿。",
                                        MessageType.Warning);
                                using (new EditorGUI.DisabledScope(locked)) drawDocument?.Invoke();
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

        private static void RegisterModeContribution(
            string modeId,
            string title,
            string tooltip,
            ESWorldWorkbenchModule module,
            ESWorkbenchContributionCategory category,
            IEnumerable<string> toolIds,
            IEnumerable<ESWorkbenchContentKind> contentKinds,
            string defaultToolId,
            Func<ESWorldBuilderWorkbenchWindow, Action> drawInspector,
            int priority,
            bool primary = false,
            Action<ESWorkbenchContributionContext, ESWorldBuilderWorkbenchWindow> prepare = null)
        {
            string contributionId = "mode." + modeId;
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
                        if (window == null) throw new InvalidOperationException("World 模式缺少窗口上下文：" + modeId);
                        prepare?.Invoke(context, window);
                        Action draw = drawInspector?.Invoke(window);
                        context.RegisterAuthoringMode(new ESWorkbenchAuthoringModeDefinition(
                            modeId,
                            title,
                            tooltip,
                            toolIds,
                            contentKinds,
                            defaultToolId,
                            priority,
                            primary,
                            createInspector: _ => window.CreateAuthoringModeInspector(modeId, draw)));
                        return null;
                    },
                    tooltip,
                    "ES.World",
                    priority,
                    1),
                out string message);
            if (!string.IsNullOrEmpty(message) && !message.StartsWith("忽略旧版本", StringComparison.Ordinal))
                Debug.LogWarning("[ESWorkbench] " + message);
        }

        private VisualElement CreateAuthoringModeInspector(string modeId, Action draw)
        {
            var container = new IMGUIContainer(() =>
            {
                ESWorkbench_SerializedAsset?.Update();
                bool locked = ESWorkbench_IsHierarchyLocked("world.map");
                if (locked)
                    EditorGUILayout.HelpBox(
                        "世界根节点已锁定，当前作者模式处于只读状态。解锁后才能修改作者草稿。",
                        MessageType.Warning);
                using (new EditorGUI.DisabledScope(locked)) draw?.Invoke();
                bool changed = ESWorkbench_SerializedAsset != null && ESWorkbench_SerializedAsset.hasModifiedProperties;
                ESWorkbench_SerializedAsset?.ApplyModifiedProperties();
                if (changed) ESWorkbench_MarkDirty(modeId, ESWorkbenchDirtyFlags.Authoring);
            })
            {
                name = "ESWorldAuthoringModeInspector_" + modeId
            };
            container.style.flexGrow = 1f;
            container.style.paddingLeft = 8f;
            container.style.paddingRight = 8f;
            container.style.paddingTop = 6f;
            return container;
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
                    if (ResolveActiveWorldContent()?.Payload is WorldBrushPaletteTemplate brush)
                    {
                        authoringBrushHeight = brush.NormalizedHeight;
                        authoringBrushMode = brush.Mode;
                        if (brush.Strength >= 0f) authoringBrushStrength = brush.Strength;
                    }
                    PaintWorldHeight(worldPoint);
                    break;
                case ESWorldAuthoringTool.Region:
                    WorldRegionPaletteTemplate regionTemplate = ResolveActiveWorldContent()?.Payload
                        as WorldRegionPaletteTemplate;
                    TryAddWorldRegion(worldPoint, regionTemplate, out _);
                    break;
                case ESWorldAuthoringTool.Poi:
                    AddWorldPoi(worldPoint);
                    break;
                case ESWorldAuthoringTool.Prefab:
                    ESWorkbenchObjectDescriptor selected = ResolveActiveWorldContent();
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
            TryPaintWorldHeight(worldPoint, out _);
        }

        private bool TryPaintWorldHeight(Vector3 worldPoint, out string message)
        {
            bool ownsStroke = !terrainStrokeSessionOpen;
            if (ownsStroke) BeginTerrainStroke();
            try
            {
                return TryPaintWorldHeightSample(worldPoint, out message);
            }
            finally
            {
                if (ownsStroke) EndTerrainStroke();
            }
        }

        private bool TryPaintWorldHeightSample(Vector3 worldPoint, out string message)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            if (definition?.heightfield == null)
            {
                message = "当前世界草稿缺少 Heightfield，不能使用地形笔刷。";
                ESWorkbench_SetStatus(message, MessageType.Error);
                return false;
            }
            if (worldPoint.x < definition.worldMin.x || worldPoint.x > definition.worldMax.x
                || worldPoint.z < definition.worldMin.y || worldPoint.z > definition.worldMax.y)
            {
                message = "地形笔刷落点不在地图范围内。";
                ESWorkbench_SetStatus(message, MessageType.Warning);
                return false;
            }
            if (!terrainStrokeUndoRecorded)
            {
                Undo.RecordObject(editSession.Draft, "绘制世界地形笔划");
                terrainStrokeUndoRecorded = true;
            }
            if (!ESWorldMapTerrainEditorFacade.TryPaintHeight(definition,
                    new Vector2(worldPoint.x, worldPoint.z), definition.worldMin, definition.worldMax,
                    authoringBrushHeight,
                    authoringBrushRadius,
                    authoringBrushStrength,
                    authoringBrushFalloff,
                    authoringBrushMode,
                    out string error))
            {
                message = error;
                ESWorkbench_SetStatus(message, MessageType.Error);
                return false;
            }
            if (terrainStrokeSessionOpen)
            {
                terrainStrokeDraftSyncPending = true;
                ESWorkbench_SetDirtyStateWithoutNotification(
                    true,
                    "definition.heightfield",
                    ESWorkbenchDirtyFlags.Authoring);
            }
            else
            {
                NotifyWorldDraftChanged("definition.heightfield", false);
            }
            message = "已使用" + GetTerrainBrushModeDisplayName(authoringBrushMode) + "更新世界高度场，目标高度 "
                + Mathf.RoundToInt(authoringBrushHeight * 100f) + "%，半径 "
                + authoringBrushRadius.ToString("0.#") + "m，强度 "
                + Mathf.RoundToInt(authoringBrushStrength * 100f) + "%。";
            ESWorkbench_SetStatus(message, MessageType.Info);
            return true;
        }

        internal void BeginTerrainStroke()
        {
            if (terrainStrokeSessionOpen) return;
            terrainStrokeSessionOpen = true;
            terrainStrokeUndoRecorded = false;
            terrainStrokeUndoGroup = -1;
            terrainStrokeSnapshotValid = false;
            terrainStrokeHadHeightfield = false;
            terrainStrokeDraftSyncPending = false;
            ESWorldMapHeightfield field = editSession?.Draft?.Definition?.heightfield;
            if (field != null)
            {
                terrainStrokeHadHeightfield = true;
                terrainStrokeWidth = field.width;
                terrainStrokeHeight = field.height;
                terrainStrokeDefaultHeight = field.defaultHeight;
                terrainStrokeSamplesSnapshot = field.samples == null ? null : new List<float>(field.samples);
                terrainStrokeSnapshotValid = true;
            }
            ESWorldMapTerrainEditorFacade.BeginPaintStroke();
            Undo.IncrementCurrentGroup();
            terrainStrokeUndoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("绘制世界地形笔划");
        }

        internal void EndTerrainStroke()
        {
            if (terrainStrokeDraftSyncPending && editSession != null)
            {
                editSession.NotifyDraftChanged("definition.heightfield");
                ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.DataChanged);
            }
            ESWorldMapTerrainEditorFacade.EndPaintStroke();
            if (terrainStrokeUndoGroup >= 0 && terrainStrokeUndoRecorded)
                Undo.CollapseUndoOperations(terrainStrokeUndoGroup);
            terrainStrokeSessionOpen = false;
            terrainStrokeUndoRecorded = false;
            terrainStrokeUndoGroup = -1;
            terrainStrokeSnapshotValid = false;
            terrainStrokeSamplesSnapshot = null;
            terrainStrokeDraftSyncPending = false;
        }

        internal void CancelTerrainStroke()
        {
            if (!terrainStrokeSessionOpen) return;
            if (terrainStrokeUndoGroup >= 0 && terrainStrokeUndoRecorded)
                Undo.RevertAllDownToGroup(terrainStrokeUndoGroup);

            ESWorldMapDefinition definition = editSession?.Draft?.Definition;
            if (definition != null && terrainStrokeSnapshotValid)
            {
                if (!terrainStrokeHadHeightfield)
                {
                    definition.heightfield = null;
                }
                else
                {
                    ESWorldMapHeightfield field = definition.heightfield ?? new ESWorldMapHeightfield();
                    field.width = terrainStrokeWidth;
                    field.height = terrainStrokeHeight;
                    field.defaultHeight = terrainStrokeDefaultHeight;
                    field.samples = terrainStrokeSamplesSnapshot == null
                        ? null
                        : new List<float>(terrainStrokeSamplesSnapshot);
                    definition.heightfield = field;
                }
                editSession.SynchronizeDraftAfterUndoRedo();
                bool conflict = editSession.RefreshExternalConflict();
                ESWorkbench_SetDirtyStateWithoutNotification(
                    editSession.IsDirty,
                    "world.terrain-cancel",
                    ESWorkbenchDirtyFlags.Authoring);
                serializedAsset?.Update();
                ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.DataChanged);
                ESWorkbench_SetStatus(
                    conflict ? "地形笔划已取消，但正式地图发生外部变化，请先检查冲突。" : "地形笔划已取消，草稿已恢复到笔划开始前。",
                    conflict ? MessageType.Warning : MessageType.Info);
            }
            terrainStrokeSessionOpen = false;
            ESWorldMapTerrainEditorFacade.EndPaintStroke();
            terrainStrokeUndoRecorded = false;
            terrainStrokeUndoGroup = -1;
            terrainStrokeSnapshotValid = false;
            terrainStrokeSamplesSnapshot = null;
            terrainStrokeDraftSyncPending = false;
        }

        internal float GetTerrainBrushRadius()
        {
            return Mathf.Clamp(authoringBrushRadius, 0.5f, 64f);
        }

        internal bool HandleTerrainBrushShortcut(KeyCode keyCode, EventModifiers modifiers)
        {
            if (keyCode != KeyCode.LeftBracket && keyCode != KeyCode.RightBracket)
                return false;
            float direction = keyCode == KeyCode.RightBracket ? 1f : -1f;
            if ((modifiers & EventModifiers.Shift) != 0)
            {
                authoringBrushStrength = Mathf.Clamp(
                    authoringBrushStrength + direction * 0.05f,
                    0.05f,
                    1f);
                ESWorkbench_SetStatus(
                    "地形笔刷强度：" + Mathf.RoundToInt(authoringBrushStrength * 100f) + "%",
                    MessageType.Info);
            }
            else
            {
                float step = Mathf.Max(0.5f, authoringBrushRadius * 0.1f);
                authoringBrushRadius = Mathf.Clamp(authoringBrushRadius + direction * step, 0.5f, 64f);
                ESWorkbench_SetStatus(
                    "地形笔刷半径：" + authoringBrushRadius.ToString("0.#") + "m",
                    MessageType.Info);
            }
            cachedBrushSummary = string.Empty;
            ESWorkbench_Actions?.Refresh(ESWorkbenchRefreshReason.SelectionChanged);
            Repaint();
            return true;
        }

        internal string GetTerrainBrushSummary()
        {
            float height = Mathf.Clamp01(authoringBrushHeight);
            float radius = GetTerrainBrushRadius();
            float strength = Mathf.Clamp01(authoringBrushStrength);
            float falloff = Mathf.Clamp01(authoringBrushFalloff);
            if (Mathf.Approximately(cachedBrushHeight, height)
                && Mathf.Approximately(cachedBrushRadius, radius)
                && Mathf.Approximately(cachedBrushStrength, strength)
                && Mathf.Approximately(cachedBrushFalloff, falloff)
                && cachedBrushMode == authoringBrushMode)
                return cachedBrushSummary;
            cachedBrushHeight = height;
            cachedBrushRadius = radius;
            cachedBrushStrength = strength;
            cachedBrushFalloff = falloff;
            cachedBrushMode = authoringBrushMode;
            cachedBrushSummary = GetTerrainBrushModeDisplayName(authoringBrushMode)
                + (authoringBrushMode == ESWorldTerrainBrushMode.Flatten
                    ? " · 目标 " + Mathf.RoundToInt(height * 100f) + "%"
                    : " · 增量强度 " + Mathf.RoundToInt(strength * 100f) + "%")
                + " · 半径 " + radius.ToString("0.#")
                + "m · 强度 " + Mathf.RoundToInt(strength * 100f)
                + "% · 衰减 " + Mathf.RoundToInt(falloff * 100f) + "%";
            return cachedBrushSummary;
        }

        private static string GetTerrainBrushModeDisplayName(ESWorldTerrainBrushMode mode)
        {
            switch (mode)
            {
                case ESWorldTerrainBrushMode.Raise: return "抬高";
                case ESWorldTerrainBrushMode.Lower: return "降低";
                case ESWorldTerrainBrushMode.Smooth: return "平滑";
                default: return "平整";
            }
        }

        private void AddWorldRegion(Vector3 worldPoint)
        {
            TryAddWorldRegion(worldPoint, null, out _);
        }

        private bool TryAddWorldRegion(
            Vector3 worldPoint,
            WorldRegionPaletteTemplate template,
            out string message)
        {
            ESWorldMapDefinition definition = editSession.Draft.Definition;
            Undo.RecordObject(editSession.Draft, "添加世界区域");
            if (definition.regions == null) definition.regions = new List<ESWorldMapRegionDefinition>();
            string id = NextStableId("region", value => definition.regions.Exists(item => item != null && item.regionId == value));
            Vector2 halfSize = template == null
                ? Vector2.one * Mathf.Max(2f, definition.chunkSize * 0.5f)
                : template.Size * 0.5f;
            Vector3 clamped = ClampToWorld(worldPoint, definition);
            OffsetRegionWithinWorld(
                new Vector2(clamped.x, clamped.z) - halfSize,
                new Vector2(clamped.x, clamped.z) + halfSize,
                Vector2.zero,
                definition.worldMin,
                definition.worldMax,
                out Vector2 regionMin,
                out Vector2 regionMax);
            definition.regions.Add(new ESWorldMapRegionDefinition
            {
                regionId = id,
                displayName = template?.DisplayName ?? "区域 " + (definition.regions.Count + 1),
                semanticTag = template?.Category ?? "Default",
                min = regionMin,
                max = regionMax,
                priority = definition.regions.Count
            });
            NotifyWorldDraftChanged("definition.regions", true);
            ESWorkbench_Selection.Select(new ESWorkbenchSelection("world.region." + id, "world.region", null, id));
            message = template == null
                ? "已添加世界区域。"
                : "已按模板创建区域：" + template.DisplayName + "。";
            ESWorkbench_SetStatus(message, MessageType.Info);
            return true;
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
            ESWorkbenchSelection selection = ESWorkbenchSelection.Empty;
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
            if (selection?.Payload is ESWorkbenchObjectDescriptor content)
                return CreateWorldContentInspector(content);
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
                    if (editSession == null || !editSession.HasUntrackedDraftMutation) return;
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

        private VisualElement CreateWorldContentInspector(ESWorkbenchObjectDescriptor content)
        {
            var root = new VisualElement { name = "ESWorldContentInspector" };
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 12f;

            Label kind = new Label("内容库 · " + content.ContentKindDisplayName);
            kind.style.fontSize = 9f;
            kind.style.color = ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
            root.Add(kind);
            Label title = new Label(content.DisplayName);
            title.AddToClassList("es-brand-title");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 15f;
            title.style.marginTop = 3f;
            title.style.marginBottom = 6f;
            root.Add(title);

            Texture preview = content.Icon;
            if (preview == null && content.Source != null)
                preview = AssetPreview.GetAssetPreview(content.Source) ?? AssetPreview.GetMiniThumbnail(content.Source);
            if (preview != null)
            {
                var image = new Image { image = preview, scaleMode = ScaleMode.ScaleToFit };
                image.style.height = 132f;
                image.style.marginBottom = 8f;
                image.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
                root.Add(image);
            }

            AddWorldContentInspectorLine(root, "分类", content.Category);
            AddWorldContentInspectorLine(root, "默认动作", content.DefaultDragHint);
            AddWorldContentInspectorLine(root, "稳定标识", content.BaseObjectId);
            if (!string.IsNullOrWhiteSpace(content.PresetId))
                AddWorldContentInspectorLine(root, "参数预设", content.PresetId);
            if (!string.IsNullOrWhiteSpace(content.Subtitle))
                AddWorldContentInspectorLine(root, "来源", content.Subtitle);
            if (!string.IsNullOrWhiteSpace(content.Tooltip))
            {
                Label description = new Label(content.Tooltip);
                description.style.whiteSpace = WhiteSpace.Normal;
                description.style.marginTop = 8f;
                description.style.marginBottom = 8f;
                root.Add(description);
            }

            if (content.Payload is WorldBrushPaletteTemplate brush)
            {
                AddWorldContentInspectorLine(root, "目标高度", Mathf.RoundToInt(brush.NormalizedHeight * 100f) + "%");
                AddWorldContentInspectorLine(root, "笔刷语义", GetTerrainBrushModeDisplayName(brush.Mode));
                if (brush.Strength >= 0f)
                    AddWorldContentInspectorLine(root, "预设强度", Mathf.RoundToInt(brush.Strength * 100f) + "%");
                AddWorldContentInspectorLine(root, "使用方式", "拖入 2D 或 3D 世界视口，在落点执行一次塑形并激活地形笔刷。");
            }
            else if (content.Payload is WorldRegionPaletteTemplate region)
            {
                AddWorldContentInspectorLine(root, "区域语义", region.Category);
                AddWorldContentInspectorLine(root, "默认尺寸", region.Size.x.ToString("0.#") + " × " + region.Size.y.ToString("0.#") + " 米");
            }
            else if (content.ContentKind == ESWorkbenchContentKind.SceneTemplate)
            {
                AddWorldContentInspectorLine(root, "应用边界", "只更新世界草稿的场景资源 Key，不直接打开、合并或保存正式 Scene。");
            }

            Button use = ESWindowPresentation.CreateToolbarButton(
                ResolveWorldContentPrimaryAction(content),
                "将该内容设为当前作者内容；真正修改只在视口拖放或明确操作后发生。",
                () => ActivateWorldPaletteItem(content));
            use.style.height = 32f;
            use.style.marginTop = 9f;
            root.Add(use);

            if (content.Source != null)
            {
                Button locate = ESWindowPresentation.CreateToolbarButton(
                    "在项目中定位",
                    "在 Project 中定位内容源资产",
                    () =>
                    {
                        Selection.activeObject = content.Source;
                        EditorGUIUtility.PingObject(content.Source);
                    });
                locate.style.height = 28f;
                locate.style.marginTop = 5f;
                root.Add(locate);
            }

            Label readiness = new Label(CanUsePaletteItem(content, out string reason)
                ? "可用 · 当前内容可以拖入 2D 或 3D 世界视口。"
                : "当前不可用 · " + reason);
            readiness.style.whiteSpace = WhiteSpace.Normal;
            readiness.style.marginTop = 10f;
            readiness.style.paddingTop = 7f;
            readiness.style.borderTopWidth = 1f;
            readiness.style.borderTopColor = ESEditorPresentation.DividerColor;
            readiness.style.color = CanUsePaletteItem(content, out _)
                ? ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready)
                : ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
            root.Add(readiness);
            return root;
        }

        private static void AddWorldContentInspectorLine(VisualElement root, string label, string value)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.marginBottom = 4f;
            Label key = new Label(label);
            key.style.width = 72f;
            key.style.flexShrink = 0f;
            key.style.color = ESEditorPresentation.SectionMutedTextColor;
            Label text = new Label(value ?? string.Empty);
            text.style.flexGrow = 1f;
            text.style.minWidth = 0f;
            text.style.whiteSpace = WhiteSpace.Normal;
            text.tooltip = text.text;
            line.Add(key);
            line.Add(text);
            root.Add(line);
        }

        private static string ResolveWorldContentPrimaryAction(ESWorkbenchObjectDescriptor content)
        {
            switch (content.ContentKind)
            {
                case ESWorkbenchContentKind.Brush: return "设为当前笔刷";
                case ESWorkbenchContentKind.RegionTemplate: return "设为当前区域模板";
                case ESWorkbenchContentKind.SceneTemplate: return "准备应用场景";
                default: return "设为当前放置内容";
            }
        }

        private void ActivateWorldPaletteItem(ESWorkbenchObjectDescriptor content)
        {
            if (content == null) return;
            activeWorldContentId = content.BaseObjectId;
            activeWorldContentPresetId = content.PresetId;
            if (content.Payload is WorldBrushPaletteTemplate brush)
            {
                authoringBrushHeight = brush.NormalizedHeight;
                authoringBrushMode = brush.Mode;
                if (brush.Strength >= 0f) authoringBrushStrength = brush.Strength;
                authoringTool = ESWorldAuthoringTool.Terrain;
                ESWorkbench_Actions?.Tools.Activate("world.terrain");
                ESWorkbench_SetStatus("当前笔刷：" + brush.DisplayName + "。拖入视口或在视口点击以绘制。", MessageType.Info);
                return;
            }
            if (content.Payload is WorldRegionPaletteTemplate region)
            {
                authoringTool = ESWorldAuthoringTool.Region;
                ESWorkbench_Actions?.Tools.Activate("world.region");
                ESWorkbench_SetStatus("当前区域模板：" + region.DisplayName + "。拖入视口创建实例。", MessageType.Info);
                return;
            }
            if (content.ContentKind == ESWorkbenchContentKind.Prefab)
            {
                authoringTool = ESWorldAuthoringTool.Prefab;
                ESWorkbench_Actions?.Tools.Activate("world.prefab");
                ESWorkbench_SetStatus("当前放置内容：" + content.DisplayName + "。拖入视口或在视口点击放置。", MessageType.Info);
                return;
            }
            ESWorkbench_SetStatus("场景模板已选中。拖入 2D 或 3D 世界视口后才会写入草稿。", MessageType.Info);
        }

        private ESWorkbenchObjectDescriptor ResolveActiveWorldContent()
        {
            if (!string.IsNullOrWhiteSpace(activeWorldContentId))
            {
                ESWorkbenchObjectDescriptor active = ESWorkbench_Objects.FirstOrDefault(value =>
                    value != null && string.Equals(value.BaseObjectId, activeWorldContentId, StringComparison.Ordinal));
                active ??= QueryWorldPalette().FirstOrDefault(value =>
                    value != null && string.Equals(value.BaseObjectId, activeWorldContentId, StringComparison.Ordinal));
                if (active != null)
                    return string.IsNullOrWhiteSpace(activeWorldContentPresetId)
                        ? active
                        : active.CreatePresetVariant(activeWorldContentPresetId);
                activeWorldContentId = string.Empty;
                activeWorldContentPresetId = string.Empty;
            }
            return ESWorkbench_Selection.Current?.Payload as ESWorkbenchObjectDescriptor;
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
                return;
            }
            if (kind == "world.poi")
            {
                AddWorldInspectorSection(root, "概览", target,
                    "poiId", "displayName", "category", "regionId", "discoverable");
                return;
            }
            if (kind == "world.prefab")
            {
                AddWorldInspectorSection(root, "概览", target,
                    "placementId", "prefabKey", "editorPrefabGuid", "regionId", "enabled");
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
                var field = new PropertyField(
                    property.Copy(),
                    ResolveWorldInspectorPropertyLabel(property.name));
                field.style.minWidth = 0f;
                field.style.marginBottom = 3f;
                root.Add(field);
            }
        }

        internal static string ResolveWorldInspectorPropertyLabel(string propertyName)
        {
            switch (propertyName)
            {
                case "mapId": return "地图 ID";
                case "sourceMode": return "来源模式";
                case "generatorKey": return "生成器 Key";
                case "generatorVersion": return "生成器版本";
                case "seed": return "种子";
                case "sceneAssetKey": return "场景资源 Key";
                case "layoutAssetKey": return "布局资源 Key";
                case "prefabSetKey": return "Prefab 集合 Key";
                case "worldMin": return "世界最小坐标";
                case "worldMax": return "世界最大坐标";
                case "chunkSize": return "区块尺寸";
                case "terrainMode": return "地形模式";
                case "terrainDataAssetPath": return "TerrainData 资产路径";
                case "heightmapAssetKey": return "高度图资源 Key";
                case "terrainHeightScale": return "地形高度缩放";
                case "maxWalkableSlope": return "最大可行走坡度";
                case "build": return "构建设置";
                case "ugcLimits": return "UGC 配额";
                case "regionId": return "区域 ID";
                case "displayName": return "显示名称";
                case "semanticTag": return "语义标签";
                case "priority": return "优先级";
                case "min": return "最小坐标";
                case "max": return "最大坐标";
                case "poiId": return "POI ID";
                case "category": return "分类";
                case "discoverable": return "可发现";
                case "position": return "位置";
                case "placementId": return "放置 ID";
                case "prefabKey": return "Prefab Key";
                case "editorPrefabGuid": return "编辑器 Prefab GUID";
                case "enabled": return "启用";
                case "rotationEuler": return "旋转角";
                case "scale": return "缩放";
                default: return ObjectNames.NicifyVariableName(propertyName ?? string.Empty);
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

        private static bool IsWorldScalableSelection(ESWorkbenchSelection selection)
        {
            return selection != null && !selection.IsEmpty
                && (selection.Kind == "world.prefab" || selection.Kind == "world.region");
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
            if (!IsWorldScalableSelection(selection) || editSession?.Draft?.Definition == null)
                return ESWorkbenchMutationResult.Failure("当前选择不支持缩放。");
            string id = selection.Payload as string;
            Vector3 value = context.Scale;
            if (!IsFinite(value)) return ESWorkbenchMutationResult.Failure("缩放值包含无效数字。");
            if (selection.Kind == "world.region")
            {
                ESWorldMapDefinition definition = editSession.Draft.Definition;
                ESWorldMapRegionDefinition region = definition.regions?
                    .Find(item => item != null && item.regionId == id);
                if (region == null) return ESWorkbenchMutationResult.Failure("所选区域已经不存在。");
                Vector2 center = (region.min + region.max) * 0.5f;
                ResizeRegionWithinWorld(
                    center,
                    new Vector2(value.x, value.z),
                    definition.worldMin,
                    definition.worldMax,
                    out region.min,
                    out region.max);
                return ESWorkbenchMutationResult.Success(
                    "已精确调整世界区域尺寸。", selection, "definition.regions");
            }
            ESWorldMapPrefabPlacement placement = editSession.Draft.Definition.prefabPlacements?
                .Find(item => item != null && item.placementId == id);
            if (placement == null) return ESWorkbenchMutationResult.Failure("所选 Prefab 放置已经不存在。");
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
            bool externalConflict = editSession.RefreshExternalConflict();
            ESWorkbench_SetDirtyStateWithoutNotification(
                editSession.IsDirty,
                "world.undo-redo",
                ESWorkbenchDirtyFlags.Authoring);
            ESWorkbench_SetStatus(
                externalConflict
                    ? "撤销/重做已同步，但正式地图已发生外部变化，请先检查冲突。"
                    : editSession.IsDirty
                        ? "撤销/重做已同步，世界草稿仍有未提交变更。"
                        : "撤销/重做已同步，世界草稿与基线一致。",
                externalConflict || editSession.IsDirty ? MessageType.Warning : MessageType.Info);
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
            var request = new ESWorkbenchPopupRequest("地形笔刷", new Vector2(360f, 250f), _ =>
            {
                var content = new VisualElement();
                Label title = new Label("地形笔刷");
                title.AddToClassList("es-brand-title");
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                content.Add(title);
                var mode = new EnumField("笔刷模式", authoringBrushMode);
                mode.RegisterValueChangedCallback(evt =>
                {
                    authoringBrushMode = (ESWorldTerrainBrushMode)evt.newValue;
                    Repaint();
                });
                content.Add(mode);
                if (authoringBrushMode == ESWorldTerrainBrushMode.Flatten)
                {
                    var slider = new Slider("目标高度", 0f, 1f) { value = authoringBrushHeight, showInputField = true };
                    slider.RegisterValueChangedCallback(evt => { authoringBrushHeight = evt.newValue; Repaint(); });
                    content.Add(slider);
                }
                var radius = new Slider("笔刷半径（米）", 0.5f, 64f) { value = authoringBrushRadius, showInputField = true };
                radius.RegisterValueChangedCallback(evt => { authoringBrushRadius = Mathf.Clamp(evt.newValue, 0.5f, 64f); Repaint(); });
                content.Add(radius);
                var strength = new Slider("笔刷强度", 0.05f, 1f) { value = authoringBrushStrength, showInputField = true };
                strength.RegisterValueChangedCallback(evt => { authoringBrushStrength = Mathf.Clamp01(evt.newValue); Repaint(); });
                content.Add(strength);
                if (authoringBrushMode != ESWorldTerrainBrushMode.Smooth)
                {
                    var falloff = new Slider("边缘衰减", 0f, 1f) { value = authoringBrushFalloff, showInputField = true };
                    falloff.RegisterValueChangedCallback(evt => { authoringBrushFalloff = Mathf.Clamp01(evt.newValue); Repaint(); });
                    content.Add(falloff);
                }
                content.Add(new Label("抬高、降低、平整、平滑均写入同一份 Draft；2D/3D 按住左键连续绘制，一次拖动只产生一条 Undo 记录。"));
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

        internal static void ResizeRegionWithinWorld(
            Vector2 center,
            Vector2 requestedSize,
            Vector2 worldMin,
            Vector2 worldMax,
            out Vector2 resultMin,
            out Vector2 resultMax)
        {
            Vector2 orderedWorldMin = Vector2.Min(worldMin, worldMax);
            Vector2 orderedWorldMax = Vector2.Max(worldMin, worldMax);
            Vector2 worldSize = orderedWorldMax - orderedWorldMin;
            Vector2 size = new Vector2(
                Mathf.Clamp(Mathf.Abs(requestedSize.x), 0f, worldSize.x),
                Mathf.Clamp(Mathf.Abs(requestedSize.y), 0f, worldSize.y));
            Vector2 half = size * 0.5f;
            Vector2 clampedCenter = new Vector2(
                Mathf.Clamp(center.x, orderedWorldMin.x + half.x, orderedWorldMax.x - half.x),
                Mathf.Clamp(center.y, orderedWorldMin.y + half.y, orderedWorldMax.y - half.y));
            resultMin = clampedCenter - half;
            resultMax = clampedCenter + half;
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

        private static void EnsureAssetFolder(string folderPath)
        {
            string normalized = (folderPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrWhiteSpace(normalized)
                || !normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("验证资产目录必须位于 Assets/ 下。", nameof(folderPath));
            if (AssetDatabase.IsValidFolder(normalized)) return;

            string[] segments = normalized.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
#endif
