using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace ES
{

    #region 粒子系统批量调整工具
    [Serializable]
    [ESSimpleToolsLayout]
    public class Page_ParticleSystemAdjustment : ESWindowPageBase
    {
        private enum ParticleLookPreset
        {
            Custom,
            Fire,
            Smoke,
            Sparks,
            Dust,
            Magic
        }


        [HideInInspector]
        public string readMe = "选择包含粒子系统（ParticleSystem）的对象，先在窗口内独立预览，再明确应用到场景；场景粒子控制不会写入参数。";

        [HideInInspector]
        public bool includeChildren = true;

        [HideInInspector]
        public float duration = 5f;

        [HideInInspector]
        public bool looping = true;

        [HideInInspector]
        public float startLifetime = 5f;

        [HideInInspector]
        public float startSpeed = 5f;

        [HideInInspector]
        public float startSize = 1f;

        [HideInInspector]
        public Color startColor = Color.white;

        [HideInInspector]
        public float emissionRate = 10f;

        [HideInInspector]
        public ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.Local;

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string particleSearch = "";
        private int particlePreviewPageIndex;
        private const int ParticlePreviewPageSize = 12;
        private const float MinimumDuration = 0.05f;
        private const float PreviewTimelineMaximum = 10f;
        private const float PreviewFrameStep = 1f / 30f;
        private const float PreviewViewportHeight = 300f;
        private const double PreviewBoundsRefreshInterval = 0.15d;
        private const double PreviewTimelineRefreshInterval = 1d / 24d;
        private const double PreviewSettingsRefreshInterval = 1d / 12d;
        private const float PreviewOrbitSensitivity = 1.35f;
        private const float PreviewPanSensitivity = 1.35f;
        private const int PreviewConfirmationThreshold = 64;
        private const int OperationConfirmationThreshold = 64;
        private static readonly ESEditorPreviewRenderOptions PreviewMotionRenderOptions =
            new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.Fast, 1f, 1d / 30d);
        private static readonly ESEditorPreviewRenderOptions PreviewInspectionRenderOptions =
            new ESEditorPreviewRenderOptions(ESEditorPreviewQuality.Balanced, 1f, 0d);
        private float previewTime;
        private int previewRandomSeed = 12345;
        private bool previewTimeNeedsSimulation;
        private double previewLastTimelineSimulationTime;
        private bool previewSettingsNeedsRefresh;
        private double previewLastSettingsRefreshTime;
        [NonSerialized] private ESEditorParticlePreviewSession particlePreviewSession;
        [NonSerialized] private ESEditorPreviewOrbitView previewView;
        [NonSerialized] private ESEditorPreviewIMGUIOrbitInput previewInput;
        private bool showOneMeterScaleReference;
        private double previewLastBoundsRefreshTime;
        private bool previewAutomaticFraming = true;
        private readonly List<ParticleSystem> particleTargetSnapshot = new List<ParticleSystem>();
        private readonly List<ParticleSystem> filteredParticleTargetSnapshot = new List<ParticleSystem>();
        private bool particleTargetSnapshotInitialized;
        private bool selectionChangedRegistered;
        private bool particleTargetsTruncated;
        private int particleSelectionCount;
        private ParticleLookPreset quickLookPreset = ParticleLookPreset.Custom;
        private float quickIntensity = 1f;
        private float quickMotion = 1f;
        private float quickScale = 1f;
        private bool showAdvancedParameters;
        private bool showSceneParticleControls;
        private bool showObjectDetails;
        private bool showEffectDiscovery;
        private bool showPersistencePanel;
        private bool showAdditionalTools;
        private GameObject selectedProjectPrefab;
        private int quickSourcePageIndex;
        private string quickSourceSearch = string.Empty;
        private string projectParticleFolderScope = string.Empty;
        private bool projectParticleFoldersLoaded;
        [NonSerialized] private QuickParticleCandidate activeQuickCandidate;
        private readonly List<string> configuredProjectParticleFolders = new List<string>();
        private string[] projectParticleFolderScopeLabels = Array.Empty<string>();
        private const int QuickSourcePageSize = 8;
        private const string ProjectParticleFoldersPrefKey = "ES.SimpleTools.ParticlePreview.ProjectFolders.v1";

        private sealed class QuickParticleCandidate
        {
            public string displayName;
            public string sourcePath;
            public GameObject root;
            public int systemCount;
            public bool isProjectAsset;
        }

        private readonly List<QuickParticleCandidate> quickParticleCandidates = new List<QuickParticleCandidate>();
        private readonly List<int> filteredQuickParticleCandidateIndices = new List<int>();
        private string quickSourceSummary = "先扫描一个来源，快速选择可用 Particle。";
        private string quickSourceDetail = "自动排除 Sub Emitters，并按名称去重；扫描只在点击按钮后执行。";

        private ESEditorPreviewOrbitView PreviewView
        {
            get
            {
                if (previewView == null)
                    previewView = new ESEditorPreviewOrbitView();
                return previewView;
            }
        }

        private ESEditorPreviewIMGUIOrbitInput PreviewInput
        {
            get
            {
                if (previewInput == null)
                    previewInput = new ESEditorPreviewIMGUIOrbitInput();
                return previewInput;
            }
        }
        private static readonly string[] ParticleLookPresetLabels =
        {
            "自定义",
            "火焰",
            "烟雾",
            "火花",
            "尘土",
            "魔法"
        };
        private static readonly string[] SimulationSpaceLabels =
        {
            "局部空间",
            "世界空间",
            "自定义空间"
        };
        private static readonly ESAdvancedDialogChoiceOption[] SceneExampleTemplateOptions =
        {
            new ESAdvancedDialogChoiceOption("current", "当前面板设置"),
            new ESAdvancedDialogChoiceOption("fire", "火焰"),
            new ESAdvancedDialogChoiceOption("smoke", "烟雾"),
            new ESAdvancedDialogChoiceOption("sparks", "火花"),
            new ESAdvancedDialogChoiceOption("dust", "尘土"),
            new ESAdvancedDialogChoiceOption("magic", "魔法")
        };

        public bool IsIndependentPreviewActive =>
            particlePreviewSession != null && particlePreviewSession.IsReady;
        public bool HasIndependentPreviewSession =>
            particlePreviewSession != null;

        private int PreviewParticleSystemCount =>
            particlePreviewSession != null ? particlePreviewSession.ParticleSystemCount : 0;

        private bool PreviewIsPlaying =>
            particlePreviewSession != null && particlePreviewSession.IsPlaying;

        private ESEditorPreviewRenderContext PreviewRenderContext =>
            particlePreviewSession != null ? particlePreviewSession.RenderContext : null;

        [OnInspectorGUI, PropertyOrder(100)]
        private void DrawResultPanel()
        {
            EnsureParticleTargetSnapshot();
            if (IsIndependentPreviewActive && !particlePreviewSession.MatchesControlledSources(particleTargetSnapshot))
                RestorePreviewSession();
            int targetCount = particleTargetSnapshot.Count;
            SimpleToolsPanelUtility.DrawToolHeader(
                "Particle 快速预览与调整",
                "从当前场景或项目 Prefab 选择 Particle，在独立预览中调整参数，再决定是否应用到场景。",
                SimpleToolsMaturity.Upgrading,
                "窗口预览使用独立 PreviewScene；只有“应用到场景”会写入组件，场景控制仅改变原粒子的播放状态。");
            SimpleToolsPanelUtility.DrawLargeListGuard(targetCount, "粒子系统");
            if (particleTargetsTruncated)
                SimpleToolsPanelUtility.DrawWarning("目标数量达到工具软上限，本次只显示和处理已收集的部分。");
            DrawEffectDiscoveryPanel();
            DrawQuickTuningPanel();
            DrawIndependentPreviewPanel();
            DrawAdditionalToolsPanel();
            FlushPendingPreviewSettingsRefresh();
            SimpleToolsPanelUtility.DrawResultSummary("最近粒子操作", lastResultSummary, lastResultDetail);
        }

        private void DrawAdditionalToolsPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle(
                "更多工具（可选）",
                "精确参数、对象明细、场景播放控制与保存链路默认收起，不干扰快速预览主流程。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                showAdditionalTools = EditorGUILayout.Foldout(
                    showAdditionalTools,
                    showAdditionalTools ? "收起更多工具" : "展开更多工具",
                    true);
                if (!showAdditionalTools)
                    return;
            }

            DrawParticleSettingsPanel();
            DrawParticlePreviewPanel();
            DrawSceneParticleControlPanel();
            DrawPersistencePanel();
        }

        private void DrawEffectDiscoveryPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle(
                "快速选择 Particle",
                "先选来源，再直接预览；只显示可作为主效果入口的 Particle，自动排除 Sub Emitters 和同名项。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("当前场景", SimpleToolsActionTone.Primary, 28, GUILayout.MinWidth(112)))
                        ScanCurrentSceneEffects();
                    if (SimpleToolsPanelUtility.DrawActionButton("当前项目", SimpleToolsActionTone.Neutral, 28, GUILayout.MinWidth(112)))
                        ScanProjectEffectAssets();
                    GUILayout.FlexibleSpace();
                }

                DrawProjectParticleFolderScope();

                EditorGUILayout.LabelField(quickSourceSummary, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(quickSourceDetail, EditorStyles.wordWrappedMiniLabel);

                EditorGUI.BeginChangeCheck();
                quickSourceSearch = EditorGUILayout.TextField("筛选名称/路径", quickSourceSearch);
                if (EditorGUI.EndChangeCheck())
                    quickSourcePageIndex = 0;
                RebuildQuickSourceFilter();

                showEffectDiscovery = EditorGUILayout.Foldout(
                    showEffectDiscovery,
                    showEffectDiscovery ? "收起 Particle 列表" : "展开 Particle 列表",
                    true);
                if (!showEffectDiscovery)
                    return;

                if (quickParticleCandidates.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("暂无 Particle。点击“当前场景”或“当前项目”扫描。");
                    return;
                }
                if (filteredQuickParticleCandidateIndices.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("没有符合当前筛选的 Particle。清空筛选词后重试。");
                    return;
                }

                int pageCount = Mathf.Max(1, Mathf.CeilToInt(filteredQuickParticleCandidateIndices.Count / (float)QuickSourcePageSize));
                quickSourcePageIndex = Mathf.Clamp(quickSourcePageIndex, 0, pageCount - 1);
                int start = quickSourcePageIndex * QuickSourcePageSize;
                int end = Mathf.Min(start + QuickSourcePageSize, filteredQuickParticleCandidateIndices.Count);
                for (int i = start; i < end; i++)
                {
                    QuickParticleCandidate candidate = quickParticleCandidates[filteredQuickParticleCandidateIndices[i]];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                        {
                            EditorGUILayout.LabelField(
                                $"{candidate.displayName} · {candidate.systemCount} 个 Particle · {(candidate.isProjectAsset ? "项目" : "场景")}",
                                EditorStyles.miniBoldLabel);
                            EditorGUILayout.LabelField(candidate.sourcePath, EditorStyles.wordWrappedMiniLabel);
                        }
                        if (SimpleToolsPanelUtility.DrawActionButton("预览", SimpleToolsActionTone.Primary, 24, GUILayout.Width(54)))
                            SelectQuickCandidate(candidate);
                    }
                }
                SimpleToolsPanelUtility.DrawPager(ref quickSourcePageIndex, filteredQuickParticleCandidateIndices.Count, QuickSourcePageSize);
            }
        }

        private void RebuildQuickSourceFilter()
        {
            filteredQuickParticleCandidateIndices.Clear();
            string keyword = string.IsNullOrWhiteSpace(quickSourceSearch) ? null : quickSourceSearch.Trim();
            for (int i = 0; i < quickParticleCandidates.Count; i++)
            {
                QuickParticleCandidate candidate = quickParticleCandidates[i];
                if (candidate == null)
                    continue;
                if (keyword == null ||
                    (!string.IsNullOrEmpty(candidate.displayName) && candidate.displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrEmpty(candidate.sourcePath) && candidate.sourcePath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    filteredQuickParticleCandidateIndices.Add(i);
                }
            }
        }

        private void DrawProjectParticleFolderScope()
        {
            EnsureProjectParticleFoldersLoaded();
            using (new EditorGUILayout.HorizontalScope())
            {
                int currentIndex = GetProjectParticleFolderScopeIndex();
                int nextIndex = EditorGUILayout.Popup(
                    "项目资产范围",
                    currentIndex,
                    projectParticleFolderScopeLabels,
                    GUILayout.MinWidth(220f));
                if (nextIndex != currentIndex)
                {
                    projectParticleFolderScope = nextIndex <= 0 || nextIndex >= configuredProjectParticleFolders.Count + 1
                        ? string.Empty
                        : configuredProjectParticleFolders[nextIndex - 1];
                    quickSourcePageIndex = 0;
                }

                if (SimpleToolsPanelUtility.DrawActionButton("配置项目文件夹", SimpleToolsActionTone.Neutral, 26, GUILayout.MinWidth(112)))
                    OpenProjectParticleFolderDialog();
            }
            EditorGUILayout.LabelField(
                string.IsNullOrWhiteSpace(projectParticleFolderScope)
                    ? "全部资产：扫描项目内所有 Prefab。"
                    : "当前范围：" + projectParticleFolderScope,
                EditorStyles.wordWrappedMiniLabel);
        }

        private void EnsureProjectParticleFoldersLoaded()
        {
            if (projectParticleFoldersLoaded)
                return;

            projectParticleFoldersLoaded = true;
            configuredProjectParticleFolders.Clear();
            string raw = EditorPrefs.GetString(ProjectParticleFoldersPrefKey, string.Empty);
            string[] values = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < values.Length; i++)
            {
                string folder = NormalizeProjectParticleFolder(values[i]);
                if (!string.IsNullOrEmpty(folder) && !configuredProjectParticleFolders.Contains(folder))
                    configuredProjectParticleFolders.Add(folder);
            }
            RefreshProjectParticleFolderScopeLabels();
        }

        private void RefreshProjectParticleFolderScopeLabels()
        {
            projectParticleFolderScopeLabels = new string[configuredProjectParticleFolders.Count + 1];
            projectParticleFolderScopeLabels[0] = "全部资产";
            for (int i = 0; i < configuredProjectParticleFolders.Count; i++)
                projectParticleFolderScopeLabels[i + 1] = configuredProjectParticleFolders[i];
        }

        private int GetProjectParticleFolderScopeIndex()
        {
            if (string.IsNullOrWhiteSpace(projectParticleFolderScope))
                return 0;
            int index = configuredProjectParticleFolders.IndexOf(projectParticleFolderScope);
            return index >= 0 ? index + 1 : 0;
        }

        private static string NormalizeProjectParticleFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return string.Empty;

            string normalized = folder.Trim().Replace('\\', '/');
            string projectRoot = Application.dataPath.Replace('\\', '/');
            if (Path.IsPathRooted(normalized) && normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                normalized = "Assets" + normalized.Substring(projectRoot.Length);
            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                return string.Empty;
            if (!AssetDatabase.IsValidFolder(normalized))
                return string.Empty;
            return normalized.TrimEnd('/');
        }

        private static string CaptureSelectedProjectParticleFolder()
        {
            UnityEngine.Object selected = Selection.activeObject;
            if (selected == null)
                return "Assets";

            string assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrWhiteSpace(assetPath))
                return "Assets";
            if (AssetDatabase.IsValidFolder(assetPath))
                return assetPath;

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            return string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent) ? "Assets" : parent;
        }

        private void OpenProjectParticleFolderDialog()
        {
            EnsureProjectParticleFoldersLoaded();
            var options = new List<ESAdvancedDialogChoiceOption>
            {
                new ESAdvancedDialogChoiceOption("all", "全部资产")
            };
            for (int i = 0; i < configuredProjectParticleFolders.Count; i++)
                options.Add(new ESAdvancedDialogChoiceOption("folder_" + i, configuredProjectParticleFolders[i]));
            string initialFolder = string.IsNullOrWhiteSpace(projectParticleFolderScope)
                ? CaptureSelectedProjectParticleFolder()
                : projectParticleFolderScope;

            var request = new ESAdvancedDialogRequest
            {
                dialogId = "simple-tools.particle.project-folder-scope",
                title = "配置 Particle 项目资产范围",
                subtitle = "ES 高级下拉菜单来源",
                message = "顶部固定保留“全部资产”，其余选项来自你保存的项目文件夹。",
                detail = "可以直接捕获当前 Project 选中资源所在文件夹，也可以在文件夹字段中选择新路径。",
                confirmText = "保存配置",
                cancelText = "取消",
                tone = ESDialogTone.Info,
                preferredSize = new Vector2(600f, 500f),
                owner = SimpleToolsWindow.UsingWindow,
                duplicatePolicy = ESDialogDuplicatePolicy.FocusExisting
            };
            request.AddChoiceOptions("scope", "默认扫描范围", options, GetProjectParticleFolderScopeIndex() == 0
                ? "all"
                : "folder_" + (GetProjectParticleFolderScopeIndex() - 1));
            request.AddFolderPath("folder", "捕获/新增项目文件夹", initialFolder);
            request.AddToggle("saveFolder", "保存该文件夹到下拉菜单", true);
            request.AddToggle("removeFolder", "移除该文件夹", false);
            request.validateDetailed = values =>
            {
                string folder = NormalizeProjectParticleFolder(values.GetString("folder"));
                if (values.GetToggle("saveFolder") || values.GetToggle("removeFolder"))
                    return string.IsNullOrEmpty(folder)
                        ? new ESAdvancedDialogValidation("请选择有效的 Assets 项目文件夹。", "folder")
                        : null;
                return null;
            };
            request.completed = result =>
            {
                if (result == null || !result.accepted || result.values == null)
                    return;

                string folder = NormalizeProjectParticleFolder(result.values.GetString("folder"));
                string selectedScope = result.values.GetString("scope", "all");
                string selectedScopePath = string.Empty;
                if (selectedScope.StartsWith("folder_", StringComparison.Ordinal)
                    && int.TryParse(selectedScope.Substring("folder_".Length), out int selectedScopeIndex)
                    && selectedScopeIndex >= 0 && selectedScopeIndex < configuredProjectParticleFolders.Count)
                    selectedScopePath = configuredProjectParticleFolders[selectedScopeIndex];

                if (result.values.GetToggle("removeFolder"))
                    configuredProjectParticleFolders.Remove(folder);
                else if (result.values.GetToggle("saveFolder") && !string.IsNullOrEmpty(folder)
                    && !configuredProjectParticleFolders.Contains(folder))
                    configuredProjectParticleFolders.Add(folder);

                configuredProjectParticleFolders.Sort(StringComparer.OrdinalIgnoreCase);
                EditorPrefs.SetString(ProjectParticleFoldersPrefKey, string.Join("\n", configuredProjectParticleFolders));
                RefreshProjectParticleFolderScopeLabels();

                if (string.Equals(selectedScope, "all", StringComparison.Ordinal))
                    projectParticleFolderScope = string.Empty;
                else if (!string.IsNullOrEmpty(selectedScopePath)
                    && configuredProjectParticleFolders.Contains(selectedScopePath))
                    projectParticleFolderScope = selectedScopePath;
                else if (!string.IsNullOrEmpty(folder) && configuredProjectParticleFolders.Contains(folder))
                    projectParticleFolderScope = folder;
                else
                    projectParticleFolderScope = string.Empty;

                SimpleToolsWindow.UsingWindow?.Repaint();
            };
            ESDialogService.Show(request);
        }

        private void ScanCurrentSceneEffects()
        {
            quickParticleCandidates.Clear();
            activeQuickCandidate = null;
            quickSourceSearch = string.Empty;
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                quickSourceSummary = "扫描失败：当前没有已加载场景。";
                quickSourceDetail = "请先打开目标场景，再重新扫描。";
                showEffectDiscovery = true;
                return;
            }

            ParticleSystem[] systems = UnityEngine.Object.FindObjectsOfType<ParticleSystem>(true);
            HashSet<int> subEmitterIds = CollectSubEmitterIds(systems);
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null || system.gameObject.scene != activeScene || subEmitterIds.Contains(system.GetInstanceID()))
                    continue;
                if (!names.Add(system.gameObject.name))
                    continue;
                quickParticleCandidates.Add(new QuickParticleCandidate
                {
                    displayName = system.gameObject.name,
                    sourcePath = SimpleToolsSafetyUtility.GetHierarchyPath(system.gameObject),
                    root = system.gameObject,
                    systemCount = CountRootParticleSystems(system.gameObject, subEmitterIds),
                    isProjectAsset = false
                });
            }

            quickParticleCandidates.Sort((left, right) =>
            {
                int nameOrder = string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase);
                return nameOrder != 0
                    ? nameOrder
                    : string.Compare(left.sourcePath, right.sourcePath, StringComparison.OrdinalIgnoreCase);
            });
            quickSourceSummary = $"当前场景：找到 {quickParticleCandidates.Count} 个可直接预览入口。";
            quickSourceDetail = "已排除 Sub Emitters 和同名对象；点击列表项对应的“预览”后只创建临时副本。";
            quickSourcePageIndex = 0;
            showEffectDiscovery = true;
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private void ScanProjectEffectAssets()
        {
            quickParticleCandidates.Clear();
            activeQuickCandidate = null;
            quickSourceSearch = string.Empty;
            if (!string.IsNullOrWhiteSpace(projectParticleFolderScope)
                && !AssetDatabase.IsValidFolder(projectParticleFolderScope))
            {
                quickSourceSummary = "扫描失败：当前项目文件夹范围已失效。";
                quickSourceDetail = "请重新打开“配置项目文件夹”，修正或移除失效路径。";
                showEffectDiscovery = true;
                return;
            }

            string[] prefabGuids = string.IsNullOrWhiteSpace(projectParticleFolderScope)
                ? AssetDatabase.FindAssets("t:Prefab")
                : AssetDatabase.FindAssets("t:Prefab", new[] { projectParticleFolderScope });
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int inspected = 0;
            for (int i = 0; i < prefabGuids.Length && inspected < 2048; i++, inspected++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;
                ParticleSystem[] systems = prefab.GetComponentsInChildren<ParticleSystem>(true);
                HashSet<int> subEmitterIds = CollectSubEmitterIds(systems);
                ParticleSystem primary = systems.FirstOrDefault(x => x != null && !subEmitterIds.Contains(x.GetInstanceID()));
                if (primary == null || !names.Add(prefab.name))
                    continue;
                quickParticleCandidates.Add(new QuickParticleCandidate
                {
                    displayName = prefab.name,
                    sourcePath = path,
                    root = prefab,
                    systemCount = systems.Count(x => x != null && !subEmitterIds.Contains(x.GetInstanceID())),
                    isProjectAsset = true
                });
            }

            quickParticleCandidates.Sort((left, right) =>
            {
                int nameOrder = string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase);
                return nameOrder != 0
                    ? nameOrder
                    : string.Compare(left.sourcePath, right.sourcePath, StringComparison.OrdinalIgnoreCase);
            });
            quickSourceSummary = string.IsNullOrWhiteSpace(projectParticleFolderScope)
                ? $"当前项目：找到 {quickParticleCandidates.Count} 个可直接预览的 Particle Prefab。"
                : $"当前文件夹：{projectParticleFolderScope}，找到 {quickParticleCandidates.Count} 个可直接预览的 Particle Prefab。";
            quickSourceDetail = prefabGuids.Length > inspected
                ? $"已按 Prefab 名称去重并排除 Sub Emitters；为保持响应速度，本次只检查前 {inspected} 个 Prefab。项目资产仅允许预览。"
                : "已按 Prefab 名称去重并排除 Sub Emitters；项目资产仅允许预览，不能直接覆盖原始资产。";
            quickSourcePageIndex = 0;
            showEffectDiscovery = true;
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private static HashSet<int> CollectSubEmitterIds(IList<ParticleSystem> systems)
        {
            var ids = new HashSet<int>();
            if (systems == null)
                return ids;
            for (int i = 0; i < systems.Count; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                    continue;
                ParticleSystem.SubEmittersModule subEmitters = system.subEmitters;
                for (int subIndex = 0; subIndex < subEmitters.subEmittersCount; subIndex++)
                {
                    ParticleSystem subEmitter = subEmitters.GetSubEmitterSystem(subIndex);
                    if (subEmitter != null)
                        ids.Add(subEmitter.GetInstanceID());
                }
            }
            return ids;
        }

        private static int CountRootParticleSystems(GameObject root, HashSet<int> subEmitterIds)
        {
            if (root == null)
                return 0;
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            int count = 0;
            for (int i = 0; i < systems.Length; i++)
                if (systems[i] != null && (subEmitterIds == null || !subEmitterIds.Contains(systems[i].GetInstanceID())))
                    count++;
            return count;
        }

        private void SelectQuickCandidate(QuickParticleCandidate candidate)
        {
            if (candidate == null || candidate.root == null)
                return;
            if (HasIndependentPreviewSession)
                RestorePreviewSession();
            activeQuickCandidate = candidate;
            selectedProjectPrefab = candidate.isProjectAsset ? candidate.root : null;
            if (!candidate.isProjectAsset)
                Selection.activeGameObject = candidate.root;
            else
                EditorGUIUtility.PingObject(candidate.root);
            RebuildParticleTargetSnapshot();
            if (!StartIndependentPreview())
                return;
            lastResultSummary = $"正在预览 Particle：{candidate.displayName}";
            lastResultDetail = candidate.isProjectAsset
                ? "项目 Prefab 正在独立 PreviewScene 中播放，仅允许只读预览；如需落地，请创建场景案例或另存为新变体。"
                : "当前场景对象正在独立 PreviewScene 中播放；确认结果后再决定是否应用到场景。";
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private void DrawPersistencePanel()
        {
            Scene scene = SceneManager.GetActiveScene();
            SimpleToolsPanelUtility.DrawSectionTitle("保存与落地（可选）", "只在需要保存场景或进入 AssetPackage 复用链路时展开。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                showPersistencePanel = EditorGUILayout.Foldout(
                    showPersistencePanel,
                    showPersistencePanel ? "收起保存与落地" : "展开保存与落地",
                    true);
                if (!showPersistencePanel)
                    return;

                if (selectedProjectPrefab != null)
                {
                    SimpleToolsPanelUtility.DrawWarning("当前是项目 Prefab 只读预览。本页不会覆盖源资产；需要落地时请创建场景案例，或在正式资产工作流中另存为新变体。");
                    return;
                }

                if (!scene.IsValid() || !scene.isLoaded)
                {
                    SimpleToolsPanelUtility.DrawWarning("当前没有可保存的已加载场景。");
                    return;
                }

                string sceneState = string.IsNullOrEmpty(scene.path)
                    ? "未命名场景（只能先另存）"
                    : scene.isDirty ? "场景有未保存变更" : "场景已保存";
                EditorGUILayout.LabelField("场景状态", sceneState, EditorStyles.wordWrappedMiniLabel);
                if (GetParticleTargets().Count > 0 && GetParticleTargets().Any(WillParticleSettingsChange))
                    SimpleToolsPanelUtility.DrawWarning("推荐：先独立预览并检查材质/局部坐标，再点击“应用到场景”；应用后仍需显式保存场景。");
                else
                    EditorGUILayout.LabelField("推荐：当前没有待应用参数。若刚刚应用过修改，请保存场景或通过 Ctrl+Z 回退。", EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(scene.path) || !scene.isDirty))
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("保存当前场景", SimpleToolsActionTone.Success, 28, GUILayout.MinWidth(118)))
                        SaveActiveScene();
                }
                EditorGUILayout.LabelField("资产包推荐：需要批量复用时，在 AssetPackage 窗口执行“扫描/分析资产可用性”，确认推荐用途、依赖和风险后再导出；本页不会绕过 AssetDatabase 写运行时资源。", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void SaveActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
            {
                lastResultSummary = "场景未保存：当前场景没有有效路径。";
                lastResultDetail = "请先使用 Unity 的“另存为”命名场景，再回到这里保存。";
                return;
            }

            bool saved = EditorSceneManager.SaveScene(scene);
            lastResultSummary = saved ? "场景已保存。" : "场景保存失败。";
            lastResultDetail = saved ? scene.path : "请检查 Console 和文件写权限；当前对象修改仍可通过 Ctrl+Z 回退。";
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private void DrawTargetOverviewPanel()
        {
            var targets = GetParticleTargets();
            int changedCount = targets.Count(WillParticleSettingsChange);
            string previewState = !IsIndependentPreviewActive
                ? "未预览"
                : PreviewIsPlaying ? "预览播放中" : "预览已暂停";

            SimpleToolsPanelUtility.DrawSummary(
                $"选择: {particleSelectionCount}",
                $"命中: {targets.Count}",
                $"待修改: {changedCount}",
                previewState);

            EditorGUI.BeginChangeCheck();
            bool nextIncludeChildren = EditorGUILayout.Toggle("包含子对象", includeChildren);
            if (EditorGUI.EndChangeCheck())
            {
                if (HasIndependentPreviewSession)
                    RestorePreviewSession();
                includeChildren = nextIncludeChildren;
                particlePreviewPageIndex = 0;
                RebuildParticleTargetSnapshot();
                SimpleToolsWindow.UsingWindow?.Repaint();
            }

            if (targets.Count == 0)
                SimpleToolsPanelUtility.DrawEmptyState("请选择带有粒子系统（ParticleSystem）的对象；粒子位于子层级时保持“包含子对象”开启。");
            else if (!string.IsNullOrWhiteSpace(lastResultSummary))
                EditorGUILayout.LabelField(lastResultSummary, EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawSceneParticleControlPanel()
        {
            var targets = GetParticleTargets();
            int loopingCount = targets.Count(ps => ps != null && ps.main.loop);
            int worldSpaceCount = targets.Count(ps => ps != null && ps.main.simulationSpace == ParticleSystemSimulationSpace.World);

            SimpleToolsPanelUtility.DrawSectionTitle("场景播放控制（可选）", "直接控制原场景中的粒子播放状态，不会应用上方尚未写入的参数。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                showSceneParticleControls = EditorGUILayout.Foldout(
                    showSceneParticleControls,
                    showSceneParticleControls ? "收起场景控制" : "展开场景控制",
                    true);
                if (!showSceneParticleControls)
                    return;

                SimpleToolsPanelUtility.DrawSummary(
                    $"命中: {targets.Count}",
                    $"Loop: {loopingCount}",
                    $"World Space: {worldSpaceCount}");

                using (new EditorGUI.DisabledScope(targets.Count == 0))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (SimpleToolsPanelUtility.DrawActionButton("播放场景粒子", SimpleToolsActionTone.Success, 30, GUILayout.MinWidth(104)))
                            PlayAllParticleSystems();
                        if (SimpleToolsPanelUtility.DrawActionButton("停止场景粒子", SimpleToolsActionTone.Neutral, 30, GUILayout.MinWidth(104)))
                            StopAllParticleSystems();
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (SimpleToolsPanelUtility.DrawActionButton("清空当前粒子", SimpleToolsActionTone.Danger, 30, GUILayout.MinWidth(104)))
                            ClearAllParticleSystems();
                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private void DrawParticleSettingsPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle("高级参数", "需要精确控制时再展开；这些参数只有点击“应用到场景”后才会写入组件。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                SimpleToolsPanelUtility.DrawSummary(
                    $"持续 {duration:0.##}s",
                    $"寿命 {startLifetime:0.##}s",
                    $"速度 {startSpeed:0.##}",
                    $"大小 {startSize:0.##}",
                    $"发射 {emissionRate:0.##}/s");
                showAdvancedParameters = EditorGUILayout.Foldout(
                    showAdvancedParameters,
                    showAdvancedParameters ? "收起完整参数" : "展开完整参数",
                    true);
                if (!showAdvancedParameters)
                    return;

                EditorGUI.BeginChangeCheck();
                duration = EditorGUILayout.Slider("持续时间", duration, MinimumDuration, 10f);
                looping = EditorGUILayout.Toggle("循环播放", looping);
                startLifetime = EditorGUILayout.Slider("开始生命周期", startLifetime, 0f, 10f);
                startSpeed = EditorGUILayout.Slider("开始速度", startSpeed, 0f, 100f);
                startSize = EditorGUILayout.Slider("开始大小", startSize, 0f, 10f);
                startColor = EditorGUILayout.ColorField("开始颜色", startColor);
                emissionRate = EditorGUILayout.Slider("发射速率", emissionRate, 0f, 1000f);
                int simulationSpaceIndex = Mathf.Clamp((int)simulationSpace, 0, SimulationSpaceLabels.Length - 1);
                simulationSpace = (ParticleSystemSimulationSpace)EditorGUILayout.Popup(
                    "模拟空间",
                    simulationSpaceIndex,
                    SimulationSpaceLabels);
                if (EditorGUI.EndChangeCheck() && HasIndependentPreviewSession)
                    RequestActivePreviewSettingsRefresh();

                FlushPendingPreviewSettingsRefresh();
            }
        }

        private void RequestActivePreviewSettingsRefresh()
        {
            if (!HasIndependentPreviewSession)
                return;

            bool dragging = Event.current != null && Event.current.type == EventType.MouseDrag;
            double now = EditorApplication.timeSinceStartup;
            if (!dragging || now - previewLastSettingsRefreshTime >= PreviewSettingsRefreshInterval)
            {
                previewSettingsNeedsRefresh = false;
                previewLastSettingsRefreshTime = now;
                RefreshActivePreviewSettings();
            }
            else
            {
                previewSettingsNeedsRefresh = true;
                SimpleToolsWindow.UsingWindow?.Repaint();
            }
        }

        private void FlushPendingPreviewSettingsRefresh()
        {
            if (!previewSettingsNeedsRefresh || !HasIndependentPreviewSession)
                return;

            double now = EditorApplication.timeSinceStartup;
            bool mouseReleased = Event.current != null && Event.current.type == EventType.MouseUp;
            if (mouseReleased || now - previewLastSettingsRefreshTime >= PreviewSettingsRefreshInterval)
            {
                previewSettingsNeedsRefresh = false;
                previewLastSettingsRefreshTime = now;
                RefreshActivePreviewSettings();
            }
        }

        private void RefreshActivePreviewSettings()
        {
            try
            {
                previewTime = particlePreviewSession.CurrentTime;
                particlePreviewSession.Seek(previewTime);
                UpdateAutomaticPreviewFraming(force: false);
                SimpleToolsWindow.UsingWindow?.Repaint();
            }
            catch (Exception exception)
            {
                FailPreviewOperation("实时参数预览", exception);
            }
        }

        private void EnsureParticleTargetSnapshot()
        {
            if (!selectionChangedRegistered)
            {
                Selection.selectionChanged -= OnUnitySelectionChanged;
                Selection.selectionChanged += OnUnitySelectionChanged;
                selectionChangedRegistered = true;
            }

            if (particleTargetSnapshotInitialized)
                return;

            RebuildParticleTargetSnapshot();
        }

        private void OnUnitySelectionChanged()
        {
            if (selectedProjectPrefab != null && Selection.activeObject != selectedProjectPrefab)
            {
                selectedProjectPrefab = null;
            }
            particleTargetSnapshotInitialized = false;
            if (activeQuickCandidate != null
                && Selection.activeObject != activeQuickCandidate.root
                && selectedProjectPrefab != activeQuickCandidate.root)
                activeQuickCandidate = null;
            particlePreviewPageIndex = 0;
            if (HasIndependentPreviewSession)
                RestorePreviewSession();
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private void RebuildParticleTargetSnapshot()
        {
            particleTargetSnapshot.Clear();
            if (selectedProjectPrefab != null)
            {
                particleSelectionCount = 1;
                ParticleSystem[] projectSystems = selectedProjectPrefab.GetComponentsInChildren<ParticleSystem>(true);
                HashSet<int> subEmitterIds = CollectSubEmitterIds(projectSystems);
                for (int i = 0; i < projectSystems.Length; i++)
                {
                    ParticleSystem system = projectSystems[i];
                    if (system != null && !subEmitterIds.Contains(system.GetInstanceID()))
                        particleTargetSnapshot.Add(system);
                }
                RebuildFilteredParticleSnapshot();
                particleTargetsTruncated = false;
                particleTargetSnapshotInitialized = true;
                return;
            }
            particleSelectionCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
            bool truncated;
            var objects = SimpleToolsSafetyUtility.CollectTargets(
                Selection.gameObjects,
                includeChildren,
                true,
                SimpleToolsSafetyUtility.DefaultCollectSoftLimit,
                out truncated);

            for (int i = 0; i < objects.Count; i++)
            {
                GameObject obj = objects[i];
                ParticleSystem particleSystem = obj != null ? obj.GetComponent<ParticleSystem>() : null;
                if (particleSystem != null)
                    particleTargetSnapshot.Add(particleSystem);
            }

            RebuildFilteredParticleSnapshot();

            particleTargetsTruncated = truncated;
            particleTargetSnapshotInitialized = true;
        }

        private void RebuildFilteredParticleSnapshot()
        {
            filteredParticleTargetSnapshot.Clear();
            string keyword = string.IsNullOrWhiteSpace(particleSearch) ? null : particleSearch.Trim();
            for (int i = 0; i < particleTargetSnapshot.Count; i++)
            {
                ParticleSystem ps = particleTargetSnapshot[i];
                if (string.IsNullOrEmpty(keyword) || ParticleMatchesSearch(ps, keyword))
                    filteredParticleTargetSnapshot.Add(ps);
            }
        }

        private List<ParticleSystem> CollectParticleTargets(out bool truncated)
        {
            return SimpleToolsSafetyUtility.CollectTargets(
                    Selection.gameObjects,
                    includeChildren,
                    true,
                    SimpleToolsSafetyUtility.DefaultCollectSoftLimit,
                    out truncated)
                .Select(obj => obj != null ? obj.GetComponent<ParticleSystem>() : null)
                .Where(ps => ps != null)
                .ToList();
        }

        private List<ParticleSystem> GetParticleTargets()
        {
            return particleTargetSnapshot;
        }

        private void DrawQuickTuningPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle(
                "Particle 模板参数",
                "选择模板并直接调整发射数量、初始速度和粒子大小；这里只改预览参数，不会写入场景。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                EditorGUI.BeginChangeCheck();
                quickLookPreset = (ParticleLookPreset)EditorGUILayout.Popup(
                    "效果模板",
                    (int)quickLookPreset,
                    ParticleLookPresetLabels);
                if (EditorGUI.EndChangeCheck() && quickLookPreset != ParticleLookPreset.Custom)
                    ApplyQuickLookPreset();

                EditorGUILayout.LabelField(GetQuickLookDescription(quickLookPreset), EditorStyles.wordWrappedMiniLabel);
                EditorGUI.BeginChangeCheck();
                quickIntensity = EditorGUILayout.Slider("发射数量", quickIntensity, 0.1f, 3f);
                quickMotion = EditorGUILayout.Slider("初始速度", quickMotion, 0.1f, 3f);
                quickScale = EditorGUILayout.Slider("粒子大小", quickScale, 0.1f, 3f);
                if (EditorGUI.EndChangeCheck() && quickLookPreset != ParticleLookPreset.Custom)
                    ApplyQuickLookPreset();

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(quickLookPreset == ParticleLookPreset.Custom))
                    {
                        if (SimpleToolsPanelUtility.DrawActionButton("重新生成", SimpleToolsActionTone.Neutral, 26, GUILayout.Width(82)))
                            ApplyQuickLookPreset();
                    }
                    if (SimpleToolsPanelUtility.DrawActionButton("切换手调", SimpleToolsActionTone.Neutral, 26, GUILayout.Width(82)))
                    {
                        quickLookPreset = ParticleLookPreset.Custom;
                        lastResultSummary = "已切换为手动调参；当前参数保持不变。";
                        lastResultDetail = "可以展开高级参数继续调整，场景对象仍未修改。";
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void ApplyQuickLookPreset()
        {
            if (HasIndependentPreviewSession)
                RestorePreviewSession();

            if (quickLookPreset == ParticleLookPreset.Custom)
            {
                lastResultSummary = "快速调参：自定义模式";
                lastResultDetail = "请选择一个目标效果后生成建议参数，或直接使用下方的完整参数。";
                return;
            }

            float baseDuration;
            float baseLifetime;
            float baseSpeed;
            float baseSize;
            float baseEmissionRate;
            Color baseColor;
            ParticleSystemSimulationSpace baseSpace;
            bool baseLoop;
            switch (quickLookPreset)
            {
                case ParticleLookPreset.Fire:
                    baseDuration = 1.5f;
                    baseLifetime = 0.9f;
                    baseSpeed = 2.4f;
                    baseSize = 0.7f;
                    baseEmissionRate = 48f;
                    baseColor = new Color(1f, 0.38f, 0.08f, 1f);
                    baseSpace = ParticleSystemSimulationSpace.Local;
                    baseLoop = true;
                    break;
                case ParticleLookPreset.Smoke:
                    baseDuration = 4f;
                    baseLifetime = 3.6f;
                    baseSpeed = 0.65f;
                    baseSize = 1.7f;
                    baseEmissionRate = 14f;
                    baseColor = new Color(0.42f, 0.42f, 0.42f, 0.7f);
                    baseSpace = ParticleSystemSimulationSpace.World;
                    baseLoop = true;
                    break;
                case ParticleLookPreset.Sparks:
                    baseDuration = 0.8f;
                    baseLifetime = 0.75f;
                    baseSpeed = 7.5f;
                    baseSize = 0.14f;
                    baseEmissionRate = 1.5f;
                    baseColor = new Color(1f, 0.74f, 0.18f, 1f);
                    baseSpace = ParticleSystemSimulationSpace.World;
                    baseLoop = false;
                    break;
                case ParticleLookPreset.Dust:
                    baseDuration = 3f;
                    baseLifetime = 2.5f;
                    baseSpeed = 0.55f;
                    baseSize = 0.55f;
                    baseEmissionRate = 18f;
                    baseColor = new Color(0.62f, 0.5f, 0.34f, 0.65f);
                    baseSpace = ParticleSystemSimulationSpace.World;
                    baseLoop = true;
                    break;
                default:
                    baseDuration = 2.2f;
                    baseLifetime = 1.8f;
                    baseSpeed = 1.4f;
                    baseSize = 0.45f;
                    baseEmissionRate = 28f;
                    baseColor = new Color(0.38f, 0.56f, 1f, 1f);
                    baseSpace = ParticleSystemSimulationSpace.Local;
                    baseLoop = true;
                    break;
            }

            duration = Mathf.Clamp(baseDuration, MinimumDuration, 10f);
            looping = baseLoop;
            startLifetime = Mathf.Clamp(baseLifetime, 0f, 10f);
            startSpeed = Mathf.Clamp(baseSpeed * quickMotion, 0f, 100f);
            startSize = Mathf.Clamp(baseSize * quickScale, 0f, 10f);
            startColor = baseColor;
            emissionRate = Mathf.Clamp(baseEmissionRate * quickIntensity, 0f, 1000f);
            simulationSpace = baseSpace;
            particlePreviewPageIndex = 0;
            lastResultSummary = "效果模板已生成；参数尚未写入场景。";
            lastResultDetail = $"发射数量 {quickIntensity:0.0}× | 初始速度 {quickMotion:0.0}× | 粒子大小 {quickScale:0.0}×。预览会同时使用发射形状、爆发、生命周期曲线、速度、噪声、重力、旋转和拖尾配置。";
        }

        private void ApplyProceduralLook(ParticleSystem particleSystem, ParticleLookPreset preset)
        {
            if (particleSystem == null || preset == ParticleLookPreset.Custom)
                return;

            ResetProceduralLookModules(particleSystem);
            switch (preset)
            {
                case ParticleLookPreset.Fire:
                    ConfigureFireLook(particleSystem);
                    break;
                case ParticleLookPreset.Smoke:
                    ConfigureSmokeLook(particleSystem);
                    break;
                case ParticleLookPreset.Sparks:
                    ConfigureSparksLook(particleSystem);
                    break;
                case ParticleLookPreset.Dust:
                    ConfigureDustLook(particleSystem);
                    break;
                case ParticleLookPreset.Magic:
                    ConfigureMagicLook(particleSystem);
                    break;
            }
        }

        private static void ResetProceduralLookModules(ParticleSystem particleSystem)
        {
            var main = particleSystem.main;
            main.gravityModifier = 0f;

            var emission = particleSystem.emission;
            emission.SetBursts(new ParticleSystem.Burst[0]);

            var shape = particleSystem.shape;
            shape.enabled = true;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = false;

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = false;
            sizeOverLifetime.separateAxes = false;

            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = false;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;

            var noise = particleSystem.noise;
            noise.enabled = false;
            noise.separateAxes = false;

            var rotationOverLifetime = particleSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = false;
            rotationOverLifetime.separateAxes = false;

            var trails = particleSystem.trails;
            trails.enabled = false;

            var lights = particleSystem.lights;
            lights.enabled = false;

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sortingFudge = 0f;
            }
        }

        private void ConfigureFireLook(ParticleSystem particleSystem)
        {
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 17f;
            shape.radius = 0.14f * quickScale;
            shape.radiusThickness = 0.75f;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateGradient(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.92f, 0.45f), 0f),
                    new GradientColorKey(new Color(1f, 0.28f, 0.025f), 0.45f),
                    new GradientColorKey(new Color(0.32f, 0.025f, 0.01f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.35f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0f, 1f)
                });

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = CreateCurve(
                new Keyframe(0f, 0.18f),
                new Keyframe(0.16f, 1f),
                new Keyframe(0.65f, 0.72f),
                new Keyframe(1f, 0f));

            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.16f * quickMotion, 0.16f * quickMotion);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.16f * quickMotion, 0.16f * quickMotion);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0.35f * quickMotion, 0.85f * quickMotion);

            var noise = particleSystem.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = 0.24f * quickMotion;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.8f * quickMotion;
            noise.damping = true;
            noise.octaveCount = 1;

            var rotationOverLifetime = particleSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.6f * quickMotion, 1.6f * quickMotion);

            var main = particleSystem.main;
            main.gravityModifier = -0.08f * quickMotion;

            var lights = particleSystem.lights;
            if (lights.light != null)
            {
                lights.enabled = true;
                lights.ratio = Mathf.Clamp01(0.08f * quickIntensity);
                lights.intensityMultiplier = 0.8f * quickIntensity;
                lights.rangeMultiplier = 0.7f * quickScale;
                lights.maxLights = 8;
            }
        }

        private void ConfigureSmokeLook(ParticleSystem particleSystem)
        {
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 29f;
            shape.radius = 0.3f * quickScale;
            shape.radiusThickness = 1f;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateGradient(
                new[]
                {
                    new GradientColorKey(new Color(0.62f, 0.59f, 0.55f), 0f),
                    new GradientColorKey(new Color(0.31f, 0.32f, 0.34f), 0.55f),
                    new GradientColorKey(new Color(0.17f, 0.18f, 0.2f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.05f, 0f),
                    new GradientAlphaKey(0.55f, 0.18f),
                    new GradientAlphaKey(0.32f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = CreateCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.35f, 0.85f),
                new Keyframe(1f, 1.55f));

            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.14f * quickMotion, 0.14f * quickMotion);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.14f * quickMotion, 0.14f * quickMotion);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0.18f * quickMotion, 0.5f * quickMotion);

            var noise = particleSystem.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = 0.52f * quickMotion;
            noise.frequency = 0.28f;
            noise.scrollSpeed = 0.24f * quickMotion;
            noise.damping = true;
            noise.octaveCount = 2;

            var rotationOverLifetime = particleSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.42f * quickMotion, 0.42f * quickMotion);

            var main = particleSystem.main;
            main.gravityModifier = -0.025f * quickMotion;
        }

        private void ConfigureSparksLook(ParticleSystem particleSystem)
        {
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 34f;
            shape.radius = 0.07f * quickScale;
            shape.radiusThickness = 1f;

            var emission = particleSystem.emission;
            int burstCount = Mathf.Clamp(Mathf.RoundToInt(22f * quickIntensity), 1, short.MaxValue);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateGradient(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.75f, 0.12f), 0.18f),
                    new GradientColorKey(new Color(1f, 0.16f, 0.015f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = CreateCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.28f, 0.72f),
                new Keyframe(1f, 0f));

            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.65f * quickMotion, 0.65f * quickMotion);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.1f * quickMotion, 0.55f * quickMotion);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.65f * quickMotion, 0.65f * quickMotion);

            var main = particleSystem.main;
            main.gravityModifier = 1.15f * quickMotion;

            var trails = particleSystem.trails;
            trails.enabled = true;
            trails.ratio = 0.78f;
            trails.lifetime = 0.16f;
            trails.dieWithParticles = false;
            trails.sizeAffectsWidth = true;
            trails.widthOverTrail = CreateCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f));
            trails.colorOverLifetime = colorOverLifetime.color;
            ReuseParticleMaterialForTrails(particleSystem);
        }

        private void ConfigureDustLook(ParticleSystem particleSystem)
        {
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.42f * quickScale;
            shape.radiusThickness = 0.75f;

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateGradient(
                new[]
                {
                    new GradientColorKey(new Color(0.77f, 0.64f, 0.44f), 0f),
                    new GradientColorKey(new Color(0.46f, 0.34f, 0.21f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.5f, 0.2f),
                    new GradientAlphaKey(0.22f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = CreateCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.45f, 0.9f),
                new Keyframe(1f, 1.25f));

            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.32f * quickMotion, 0.32f * quickMotion);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.32f * quickMotion, 0.32f * quickMotion);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0.05f * quickMotion, 0.28f * quickMotion);

            var noise = particleSystem.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.2f * quickMotion;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.18f * quickMotion;
            noise.damping = true;
            noise.octaveCount = 1;

            var rotationOverLifetime = particleSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.55f * quickMotion, 0.55f * quickMotion);

            var main = particleSystem.main;
            main.gravityModifier = 0.08f * quickMotion;
        }

        private void ConfigureMagicLook(ParticleSystem particleSystem)
        {
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.38f * quickScale;
            shape.radiusThickness = 0.7f;

            var emission = particleSystem.emission;
            int burstCount = Mathf.Clamp(Mathf.RoundToInt(8f * quickIntensity), 1, short.MaxValue);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateGradient(
                new[]
                {
                    new GradientColorKey(new Color(0.55f, 0.95f, 1f), 0f),
                    new GradientColorKey(new Color(0.29f, 0.38f, 1f), 0.48f),
                    new GradientColorKey(new Color(0.72f, 0.16f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.75f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });

            var sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = CreateCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.48f, 0.55f),
                new Keyframe(0.72f, 1.05f),
                new Keyframe(1f, 0f));

            var velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.45f * quickMotion, 0.45f * quickMotion);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.18f * quickMotion, 0.42f * quickMotion);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.45f * quickMotion, 0.45f * quickMotion);

            var noise = particleSystem.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = 0.32f * quickMotion;
            noise.frequency = 0.55f;
            noise.scrollSpeed = 0.45f * quickMotion;
            noise.damping = true;
            noise.octaveCount = 2;

            var rotationOverLifetime = particleSystem.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.1f * quickMotion, 1.1f * quickMotion);

            var trails = particleSystem.trails;
            trails.enabled = true;
            trails.ratio = 0.45f;
            trails.lifetime = 0.32f;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.widthOverTrail = CreateCurve(
                new Keyframe(0f, 0.8f),
                new Keyframe(1f, 0f));
            trails.colorOverLifetime = colorOverLifetime.color;
            ReuseParticleMaterialForTrails(particleSystem);
        }

        private static ParticleSystem.MinMaxCurve CreateCurve(params Keyframe[] keys)
        {
            return new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(keys));
        }

        private static ParticleSystem.MinMaxGradient CreateGradient(
            GradientColorKey[] colorKeys,
            GradientAlphaKey[] alphaKeys)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void ReuseParticleMaterialForTrails(ParticleSystem particleSystem)
        {
            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && renderer.trailMaterial == null && renderer.sharedMaterial != null)
                renderer.trailMaterial = renderer.sharedMaterial;
        }

        private static string GetQuickLookDescription(ParticleLookPreset preset)
        {
            switch (preset)
            {
                case ParticleLookPreset.Fire: return "锥形上升、暖色渐变、收缩曲线与中频扰动；有现成 Light 引用时才启用粒子灯光。";
                case ParticleLookPreset.Smoke: return "宽锥上升、灰色淡出、持续膨胀与低频双层噪声。";
                case ParticleLookPreset.Sparks: return "启动爆发、低连续发射、高速抛射、重力下坠与短拖尾。";
                case ParticleLookPreset.Dust: return "半球扩散、土色淡出、轻微上浮与低成本扰动。";
                case ParticleLookPreset.Magic: return "球形漂浮、冷色脉冲、双层噪声与中等拖尾。";
                default: return "保留当前手动参数";
            }
        }

        private void DrawIndependentPreviewPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle(
                "预览与应用",
                "在窗口内检查独立临时副本，确认后再把参数应用到场景对象。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                bool useSideNavigator = EditorGUIUtility.currentViewWidth >= 760f;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(360f), GUILayout.ExpandWidth(true)))
                    {
                        DrawIndependentPreviewViewport();
                    }
                    if (useSideNavigator)
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(228f)))
                        {
                            DrawPreviewCandidateNavigator();
                        }
                    }
                }
                if (!useSideNavigator)
                    DrawPreviewCandidateNavigator();

                using (new EditorGUI.DisabledScope(GetParticleTargets().Count == 0))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string restartLabel = !IsIndependentPreviewActive ? "开始预览" : "重新开始预览";
                        if (SimpleToolsPanelUtility.DrawActionButton(restartLabel, SimpleToolsActionTone.Primary, 30, GUILayout.MinWidth(118)))
                            StartIndependentPreview();
                        using (new EditorGUI.DisabledScope(!IsIndependentPreviewActive))
                        {
                            string playbackLabel = PreviewIsPlaying ? "暂停预览" : "继续预览";
                            if (SimpleToolsPanelUtility.DrawActionButton(playbackLabel, SimpleToolsActionTone.Neutral, 30, GUILayout.MinWidth(84)))
                            {
                                if (PreviewIsPlaying)
                                    PausePreview();
                                else
                                    ResumePreview();
                            }
                        }
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(selectedProjectPrefab != null))
                        {
                            if (SimpleToolsPanelUtility.DrawActionButton("应用到场景", SimpleToolsActionTone.Warning, 30, GUILayout.MinWidth(100)))
                                ApplyParticleSystemSettings();
                            if (SimpleToolsPanelUtility.DrawActionButton("应用并保存", SimpleToolsActionTone.Success, 30, GUILayout.MinWidth(100)))
                                ApplyAndSaveParticleSystemSettings();
                        }
                        using (new EditorGUI.DisabledScope(!HasIndependentPreviewSession))
                        {
                            if (SimpleToolsPanelUtility.DrawActionButton("结束预览", SimpleToolsActionTone.Neutral, 30, GUILayout.MinWidth(84)))
                                RestorePreviewSession();
                        }
                    }
                }

                if (selectedProjectPrefab != null)
                    EditorGUILayout.HelpBox("项目 Prefab 当前为只读临时预览，不会覆盖源资产。需要修改时请先创建场景案例，或在正式资产工作流中另存为新变体。", MessageType.Info);

                if (SimpleToolsPanelUtility.DrawActionButton(
                        "创建场景案例",
                        SimpleToolsActionTone.Primary,
                        30,
                        GUILayout.MinWidth(118)))
                {
                    ShowCreateSceneExampleDialog();
                }

                using (new EditorGUI.DisabledScope(GetParticleTargets().Count == 0))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("预览时间", EditorStyles.miniBoldLabel, GUILayout.Width(56));
                        EditorGUI.BeginChangeCheck();
                        float nextPreviewTime = EditorGUILayout.Slider(previewTime, 0f, PreviewTimelineMaximum);
                        if (EditorGUI.EndChangeCheck())
                        {
                            particlePreviewSession?.Pause();
                            previewTime = nextPreviewTime;
                            bool draggingTimeline = Event.current != null && Event.current.type == EventType.MouseDrag;
                            double now = EditorApplication.timeSinceStartup;
                            bool refreshNow = !draggingTimeline
                                || now - previewLastTimelineSimulationTime >= PreviewTimelineRefreshInterval;
                            previewTimeNeedsSimulation = !refreshNow;
                            if (refreshNow)
                            {
                                previewLastTimelineSimulationTime = now;
                                SimulatePreviewAtCurrentTime();
                            }
                            else
                            {
                                SimpleToolsWindow.UsingWindow?.Repaint();
                            }
                        }
                        if (SimpleToolsPanelUtility.DrawActionButton("单帧", SimpleToolsActionTone.Neutral, 22, GUILayout.Width(48)))
                        {
                            previewTime = Mathf.Min(PreviewTimelineMaximum, previewTime + PreviewFrameStep);
                            SimulatePreviewAtCurrentTime();
                        }
                    }
                    EditorGUI.BeginChangeCheck();
                    previewRandomSeed = EditorGUILayout.IntField("随机种子", previewRandomSeed);
                    if (EditorGUI.EndChangeCheck() && IsIndependentPreviewActive)
                    {
                        particlePreviewSession.SetRandomSeed(previewRandomSeed);
                        particlePreviewSession.Seek(previewTime);
                    }
                    bool nextScaleReference = EditorGUILayout.ToggleLeft(
                        "显示 1m 立方体参照",
                        showOneMeterScaleReference);
                    if (nextScaleReference != showOneMeterScaleReference)
                    {
                        showOneMeterScaleReference = nextScaleReference;
                        PreviewRenderContext?.SetScaleReferenceVisible(showOneMeterScaleReference);
                        if (IsIndependentPreviewActive)
                            ResetPreviewView();
                    }
                }

                if (previewTimeNeedsSimulation && Event.current != null && Event.current.type == EventType.MouseUp)
                {
                    previewTimeNeedsSimulation = false;
                    previewLastTimelineSimulationTime = EditorApplication.timeSinceStartup;
                    SimulatePreviewAtCurrentTime();
                }

                string status = !IsIndependentPreviewActive
                    ? "尚未创建窗口预览副本"
                    : PreviewIsPlaying
                        ? $"窗口预览播放中，共 {PreviewParticleSystemCount} 个临时粒子系统"
                        : $"窗口预览已暂停，共 {PreviewParticleSystemCount} 个临时粒子系统";
                if (IsIndependentPreviewActive)
                {
                    status += $" | {particlePreviewSession.EffectiveSampleRate:0} Hz"
                        + $" | 容量 {particlePreviewSession.SourceParticleCapacity:N0}"
                        + (PreviewIsPlaying ? " | 流畅模式" : " | 细节模式");
                }
                EditorGUILayout.LabelField(status, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawPreviewCandidateNavigator()
        {
            QuickParticleCandidate current = GetCurrentQuickCandidate();
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("当前播放 Particle", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    IsIndependentPreviewActive
                        ? (PreviewIsPlaying ? "状态：播放中" : "状态：已暂停")
                        : "状态：未创建预览副本",
                    EditorStyles.wordWrappedMiniLabel);
                if (current == null)
                {
                    EditorGUILayout.LabelField(
                        "尚未从快速列表选择 Particle。",
                        EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField(
                        $"{current.displayName} · {current.systemCount} 个 Particle",
                        EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField(
                        current.sourcePath,
                        EditorStyles.wordWrappedMiniLabel);
                }

                RebuildQuickSourceFilter();
                bool hasCandidates = filteredQuickParticleCandidateIndices.Count > 0;
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!hasCandidates))
                    {
                        if (SimpleToolsPanelUtility.DrawActionButton("上一个", SimpleToolsActionTone.Neutral, 26, GUILayout.MinWidth(62)))
                            NavigateQuickCandidate(-1);
                        if (SimpleToolsPanelUtility.DrawActionButton("下一个", SimpleToolsActionTone.Neutral, 26, GUILayout.MinWidth(62)))
                            NavigateQuickCandidate(1);
                    }
                    using (new EditorGUI.DisabledScope(current == null))
                    {
                        if (SimpleToolsPanelUtility.DrawActionButton("重播当前", SimpleToolsActionTone.Primary, 26, GUILayout.MinWidth(74)))
                            StartIndependentPreview();
                    }
                }

                if (!hasCandidates)
                {
                    EditorGUILayout.LabelField(
                        "先扫描当前场景或当前项目，再使用上一个/下一个快速检查。",
                        EditorStyles.wordWrappedMiniLabel);
                }
            }
        }

        private QuickParticleCandidate GetCurrentQuickCandidate()
        {
            if (activeQuickCandidate != null && quickParticleCandidates.Contains(activeQuickCandidate))
                return activeQuickCandidate;

            UnityEngine.Object currentRoot = selectedProjectPrefab != null
                ? selectedProjectPrefab
                : Selection.activeGameObject;
            if (currentRoot == null)
                return null;

            for (int i = 0; i < quickParticleCandidates.Count; i++)
            {
                QuickParticleCandidate candidate = quickParticleCandidates[i];
                if (candidate != null && candidate.root == currentRoot)
                    return candidate;
            }
            return null;
        }

        private void NavigateQuickCandidate(int direction)
        {
            RebuildQuickSourceFilter();
            int count = filteredQuickParticleCandidateIndices.Count;
            if (count == 0)
                return;

            QuickParticleCandidate current = GetCurrentQuickCandidate();
            int currentFilteredIndex = -1;
            if (current != null)
            {
                for (int i = 0; i < count; i++)
                {
                    if (quickParticleCandidates[filteredQuickParticleCandidateIndices[i]] == current)
                    {
                        currentFilteredIndex = i;
                        break;
                    }
                }
            }

            if (currentFilteredIndex < 0)
                currentFilteredIndex = direction < 0 ? 0 : -1;
            int nextFilteredIndex = (currentFilteredIndex + direction) % count;
            if (nextFilteredIndex < 0)
                nextFilteredIndex += count;
            SelectQuickCandidate(quickParticleCandidates[filteredQuickParticleCandidateIndices[nextFilteredIndex]]);
        }

        private void DrawIndependentPreviewViewport()
        {
            Rect previewRect = GUILayoutUtility.GetRect(
                1f,
                PreviewViewportHeight,
                GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(previewRect, new Color(0.16f, 0.18f, 0.21f, 1f));

            ESEditorPreviewRenderContext previewRenderContext = PreviewRenderContext;
            bool hasPreview = IsIndependentPreviewActive && previewRenderContext != null;
            if (hasPreview)
            {
                previewRenderContext.SetScaleReferenceVisible(showOneMeterScaleReference);
                ESEditorPreviewCameraPose pose = PreviewView.CreateCameraPose(previewRenderContext);
                ESEditorPreviewRenderOptions renderOptions = PreviewIsPlaying
                    ? PreviewMotionRenderOptions
                    : PreviewInspectionRenderOptions;
                if (!previewRenderContext.RenderGUI(
                        previewRect,
                        pose,
                        renderOptions))
                {
                    GUI.Label(previewRect, "预览渲染失败，请重新开始", EditorStyles.centeredGreyMiniLabel);
                }
            }
            else
            {
                GUI.Label(previewRect, "选择粒子后开始预览", EditorStyles.centeredGreyMiniLabel);
            }

            Rect badgeRect = new Rect(previewRect.x + 8f, previewRect.y + 8f, 88f, 22f);
            EditorGUI.DrawRect(badgeRect, new Color(0.01f, 0.015f, 0.02f, 0.82f));
            GUI.Label(new Rect(badgeRect.x + 7f, badgeRect.y + 2f, badgeRect.width - 12f, 18f), "独立预览", EditorStyles.miniLabel);

            if (hasPreview)
            {
                DrawPreviewZoomControls(previewRect);
                ESEditorPreviewGizmos.DrawAxis(previewRect, previewRenderContext.Camera);
                ESEditorPreviewGizmos.DrawWorldAxes(
                    previewRect,
                    previewRenderContext.Camera,
                    previewRenderContext.GroupOrigin,
                    Mathf.Clamp(PreviewView.Radius * 0.42f, 0.35f, 4f));
                Rect resetRect = new Rect(previewRect.xMax - 34f, previewRect.y + 7f, 26f, 24f);
                GUIContent resetContent = EditorGUIUtility.IconContent("Refresh", "|恢复推荐观察视角");
                if (GUI.Button(resetRect, resetContent, EditorStyles.miniButton))
                    ResetPreviewView();
                // 滚轮缩放覆盖整个预览框；按钮本身消费 MouseDown 后不会触发轨道拖拽。
                HandlePreviewViewportInput(previewRect);
            }
        }

        private void DrawPreviewZoomControls(Rect previewRect)
        {
            float farClip = PreviewRenderContext?.Camera != null
                ? PreviewRenderContext.Camera.farClipPlane
                : 80f;
            Rect zoomOutRect = new Rect(previewRect.xMax - 164f, previewRect.y + 7f, 22f, 24f);
            Rect sliderRect = new Rect(previewRect.xMax - 132f, previewRect.y + 11f, 72f, 16f);
            Rect zoomInRect = new Rect(previewRect.xMax - 56f, previewRect.y + 7f, 22f, 24f);
            float aspect = previewRect.width / Mathf.Max(1f, previewRect.height);
            float cameraDistance = PreviewView.GetCameraDistance(
                aspect,
                PreviewRenderContext?.Camera != null ? PreviewRenderContext.Camera.fieldOfView : 30f);
            GUI.Label(new Rect(previewRect.xMax - 218f, previewRect.y + 10f, 56f, 18f),
                $"缩放 {cameraDistance:0.0}m", EditorStyles.miniLabel);

            if (GUI.Button(zoomOutRect, new GUIContent("−", "缩小预览"), EditorStyles.miniButton))
            {
                PreviewView.ZoomByFactor(1.18f, farClip);
                previewAutomaticFraming = false;
            }

            float normalizedZoom = PreviewView.GetNormalizedMagnification(farClip);
            GUI.Label(new Rect(previewRect.xMax - 276f, previewRect.y + 10f, 52f, 18f),
                string.Format("{0:0}%", normalizedZoom * 100f), EditorStyles.miniLabel);
            float nextNormalizedZoom = GUI.HorizontalSlider(sliderRect, normalizedZoom, 0f, 1f);
            if (!Mathf.Approximately(nextNormalizedZoom, normalizedZoom))
            {
                PreviewView.SetNormalizedMagnification(nextNormalizedZoom, farClip);
                previewAutomaticFraming = false;
            }

            if (GUI.Button(zoomInRect, new GUIContent("+", "放大预览"), EditorStyles.miniButton))
            {
                PreviewView.ZoomByFactor(0.84f, farClip);
                previewAutomaticFraming = false;
            }
        }

        private void HandlePreviewViewportInput(Rect previewRect)
        {
            ESEditorPreviewViewportInputResult result = PreviewInput.Handle(
                previewRect,
                PreviewView,
                requireModifierForWheelZoom: false,
                orbitSensitivity: PreviewOrbitSensitivity,
                panSensitivity: PreviewPanSensitivity,
                farClipPlane: PreviewRenderContext?.Camera != null ? PreviewRenderContext.Camera.farClipPlane : 80f);
            if (result == ESEditorPreviewViewportInputResult.Pan
                || result == ESEditorPreviewViewportInputResult.Zoom)
                previewAutomaticFraming = false;
            if (result != ESEditorPreviewViewportInputResult.None)
                SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private void ReleasePreviewMouseControl()
        {
            previewInput?.Release();
        }

        private void ShowCreateSceneExampleDialog()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                lastResultSummary = "无法创建场景案例：请先退出运行模式（Play Mode）。";
                lastResultDetail = "案例创建会写入当前编辑场景，并注册撤销记录（Undo）。";
                return;
            }

            ParticleLookPreset defaultPreset = quickLookPreset == ParticleLookPreset.Custom
                ? ParticleLookPreset.Fire
                : quickLookPreset;
            string defaultTemplateId = GetSceneExampleTemplateId(defaultPreset);
            var request = new ESAdvancedDialogRequest
            {
                dialogId = "simple-tools.particle.create-scene-example",
                title = "创建粒子场景案例",
                subtitle = "使用当前粒子工具的同一套程序化模板",
                message = "创建一个可撤销、可直接播放的真实场景粒子系统（ParticleSystem）。",
                detail = "关闭“立即保存场景”时只创建对象并标记场景为已修改；不会创建预制体（Prefab）、材质、贴图或其他资产。",
                confirmText = "创建案例",
                cancelText = "取消",
                tone = ESDialogTone.Success,
                preferredSize = new Vector2(560f, 440f),
                initialFocusFieldId = "exampleName",
                owner = SimpleToolsWindow.UsingWindow,
                duplicatePolicy = ESDialogDuplicatePolicy.FocusExisting
            };
            request.AddText(
                "exampleName",
                "案例名称",
                "ES粒子案例_" + ParticleLookPresetLabels[(int)defaultPreset],
                true).help = "创建为当前场景的根对象；同名时自动生成唯一名称。";
            request.AddChoiceOptions(
                "template",
                 "基于模板",
                SceneExampleTemplateOptions,
                defaultTemplateId).help = "“当前面板设置”保留当前发射数量、初始速度、粒子大小和手调参数；其他选项会先切换并生成对应模板。";
            request.AddToggle("saveScene", "创建后立即保存当前场景", false).help =
                "只保存当前已命名场景；未命名场景可取消勾选，创建后再使用 Unity 的“场景另存为”。";
            request.validateDetailed = values => ValidateSceneExampleRequest(values);
            request.completed = result =>
            {
                if (result == null || !result.accepted || result.values == null)
                    return;

                CreateSceneExample(
                    result.values.GetString("exampleName"),
                    result.values.GetString("template", defaultTemplateId),
                    result.values.GetToggle("saveScene"));
            };
            ESDialogService.Show(request);
        }

        private static ESAdvancedDialogValidation ValidateSceneExampleRequest(ESAdvancedDialogValues values)
        {
            string exampleName = values?.GetString("exampleName")?.Trim() ?? string.Empty;
            if (exampleName.Length == 0)
                return new ESAdvancedDialogValidation("案例名称不能为空。", "exampleName");
            if (exampleName.Length > 64)
                return new ESAdvancedDialogValidation("案例名称最多 64 个字符。", "exampleName");
            if (exampleName.Any(char.IsControl))
                return new ESAdvancedDialogValidation("案例名称不能包含控制字符。", "exampleName");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return new ESAdvancedDialogValidation("当前没有可写入的已加载场景。", "exampleName");
            if (values.GetToggle("saveScene") && string.IsNullOrWhiteSpace(scene.path))
            {
                return new ESAdvancedDialogValidation(
                    "当前场景尚未命名。请取消立即保存，或先通过 Unity 将场景另存。",
                    "saveScene");
            }

            return null;
        }

        private void CreateSceneExample(string requestedName, string templateId, bool saveScene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                lastResultSummary = "案例未创建：编辑器已进入运行模式（Play Mode）。";
                lastResultDetail = "退出运行模式后重新打开创建对话框。";
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                lastResultSummary = "案例未创建：当前没有可写入的已加载场景。";
                lastResultDetail = "请先打开目标场景。";
                return;
            }

            bool useCurrentPanelSettings = string.Equals(templateId, "current", StringComparison.Ordinal);
            ParticleLookPreset preset = useCurrentPanelSettings
                ? quickLookPreset
                : ResolveSceneExamplePreset(templateId);
            if (!useCurrentPanelSettings)
            {
                quickLookPreset = preset;
                ApplyQuickLookPreset();
            }

            if (HasIndependentPreviewSession)
                RestorePreviewSession();

            GameObject exampleObject = null;
            GameObject groundObject = null;
            try
            {
                string trimmedName = requestedName?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedName))
                    trimmedName = "ES粒子案例_" + ParticleLookPresetLabels[(int)preset];
                string uniqueName = GameObjectUtility.GetUniqueNameForSibling(null, trimmedName);
                exampleObject = new GameObject(uniqueName, typeof(ParticleSystem));
                Undo.RegisterCreatedObjectUndo(exampleObject, "创建 ES 粒子场景案例");
                if (exampleObject.scene != activeScene)
                    SceneManager.MoveGameObjectToScene(exampleObject, activeScene);
                exampleObject.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.Euler(-90f, 0f, 0f));

                groundObject = CreateSceneExampleGroundPlane(activeScene, uniqueName + "_预览平板");

                ParticleSystem particleSystem = exampleObject.GetComponent<ParticleSystem>();
                ApplyPanelSettingsToParticleSystem(particleSystem, preset);
                particleSystem.useAutoRandomSeed = false;
                particleSystem.randomSeed = unchecked((uint)previewRandomSeed);
                particleSystem.Play(false);

                EditorUtility.SetDirty(particleSystem);
                ParticleSystemRenderer renderer = exampleObject.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                    EditorUtility.SetDirty(renderer);
                EditorSceneManager.MarkSceneDirty(activeScene);

                bool saved = saveScene && EditorSceneManager.SaveScene(activeScene);
                Selection.activeGameObject = exampleObject;
                EditorGUIUtility.PingObject(exampleObject);
                SceneView.lastActiveSceneView?.FrameSelected();
                SceneView.RepaintAll();
                RebuildParticleTargetSnapshot();

                string presetName = ParticleLookPresetLabels[(int)preset];
                lastResultSummary = saveScene
                    ? saved
                        ? $"已创建并保存案例：{uniqueName}"
                        : $"已创建案例，但场景保存失败：{uniqueName}"
                    : $"已创建案例：{uniqueName}";
                lastResultDetail = $"模板：{presetName}｜位置：(0, 0, 0)｜已附带半透明预览平板｜可按 Ctrl+Z 撤销。"
                    + (saveScene && !saved ? "\n对象仍保留在场景中，请检查场景路径或控制台（Console）后手动保存。" : string.Empty);
            }
            catch (Exception exception)
            {
                if (exampleObject != null)
                    UnityEngine.Object.DestroyImmediate(exampleObject);
                if (groundObject != null)
                    UnityEngine.Object.DestroyImmediate(groundObject);
                lastResultSummary = "粒子场景案例创建失败。";
                lastResultDetail = exception.GetType().Name + ": " + exception.Message;
                Debug.LogException(exception);
            }
        }

        private static ParticleLookPreset ResolveSceneExamplePreset(string templateId)
        {
            switch (templateId)
            {
                case "fire": return ParticleLookPreset.Fire;
                case "smoke": return ParticleLookPreset.Smoke;
                case "sparks": return ParticleLookPreset.Sparks;
                case "dust": return ParticleLookPreset.Dust;
                case "magic": return ParticleLookPreset.Magic;
                default: return ParticleLookPreset.Custom;
            }
        }

        private static string GetSceneExampleTemplateId(ParticleLookPreset preset)
        {
            switch (preset)
            {
                case ParticleLookPreset.Fire: return "fire";
                case ParticleLookPreset.Smoke: return "smoke";
                case ParticleLookPreset.Sparks: return "sparks";
                case ParticleLookPreset.Dust: return "dust";
                case ParticleLookPreset.Magic: return "magic";
                default: return "current";
            }
        }

        private bool EnsurePreviewSession()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("无法预览", "请退出运行模式（Play Mode）后使用窗口独立预览。", "确定");
                return false;
            }

            if (particleTargetSnapshot.Count == 0)
            {
                EditorUtility.DisplayDialog("无法预览", "当前选区没有可预览的 ParticleSystem。", "确定");
                return false;
            }

            if (IsIndependentPreviewActive && particlePreviewSession.MatchesControlledSources(particleTargetSnapshot))
                return true;

            RestorePreviewSession();
            if (particleTargetSnapshot.Count >= PreviewConfirmationThreshold &&
                !SimpleToolsPanelUtility.ConfirmHeavyOperation(
                    "确认粒子预览",
                    particleTargetSnapshot.Count,
                    $"预览当前命中的 {particleTargetSnapshot.Count} 个粒子系统。",
                    "公共粒子预览底层会复制完整选中根对象，并在独立 PreviewScene 中运行；原场景对象不会播放或修改。"))
                return false;

            particlePreviewSession = new ESEditorParticlePreviewSession(
                "ES SimpleTools Particle Preview",
                () =>
                {
                    previewTime = particlePreviewSession != null ? particlePreviewSession.CurrentTime : previewTime;
                    UpdateAutomaticPreviewFraming(force: false);
                    SimpleToolsWindow.UsingWindow?.Repaint();
                });

            GameObject[] selectedRoots = selectedProjectPrefab != null
                ? new[] { selectedProjectPrefab }
                : Selection.gameObjects ?? Array.Empty<GameObject>();
            if (!particlePreviewSession.Rebuild(
                    selectedRoots,
                    particleTargetSnapshot,
                    ConfigurePreviewParticleSystem,
                    previewRandomSeed,
                    PreviewTimelineMaximum,
                    shouldLoop: true,
                    out string error))
            {
                particlePreviewSession.Dispose();
                particlePreviewSession = null;
                lastResultSummary = "粒子预览创建失败";
                lastResultDetail = error;
                EditorUtility.DisplayDialog("粒子预览失败", error, "确定");
                return false;
            }

            ResetPreviewView();
            return true;
        }

        private void ConfigurePreviewParticleSystem(
            ParticleSystem source,
            ParticleSystem preview,
            bool usesToolSettings)
        {
            if (preview == null || !usesToolSettings)
                return;
            ApplyPanelSettingsToParticleSystem(preview, quickLookPreset);
        }

        private void ResetPreviewView()
        {
            previewAutomaticFraming = true;
            previewLastBoundsRefreshTime = 0d;
            // ES 推荐观察姿态：略俯视、三分之四角度，避免切换资源时出现贴脸或完全侧视。
            PreviewView.ResetRecommended(Vector3.zero, 1.6f);
            PreviewRenderContext?.SetScaleReferenceVisible(showOneMeterScaleReference);

            if (!UpdateAutomaticPreviewFraming(force: true))
                SetFallbackPreviewView();

            PreviewView.ClampZoom(PreviewRenderContext?.Camera != null
                ? PreviewRenderContext.Camera.farClipPlane
                : 80f);
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private static GameObject CreateSceneExampleGroundPlane(Scene activeScene, string requestedName)
        {
            const float groundSize = 25f;
            const float groundThickness = 0.02f;
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = requestedName;
            if (ground.scene != activeScene)
                SceneManager.MoveGameObjectToScene(ground, activeScene);
            ground.transform.SetPositionAndRotation(Vector3.down * (groundThickness * 0.5f), Quaternion.identity);
            ground.transform.localScale = new Vector3(groundSize, groundThickness, groundSize);
            Undo.RegisterCreatedObjectUndo(ground, "创建 ES 粒子案例预览平板");

            Collider collider = ground.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);

            Renderer renderer = ground.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Legacy Shaders/Transparent/Diffuse")
                ?? Shader.Find("Unlit/Color");
            if (renderer != null && shader != null)
            {
                Material material = new Material(shader)
                {
                    name = requestedName + "_Material",
                    color = new Color(0.28f, 0.34f, 0.40f, 0.24f),
                    renderQueue = 3000
                };
                ESEditorPreviewUtility.ConfigureDoubleSidedTransparent(material, material.color);
                renderer.receiveShadows = false;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
            }

            EditorUtility.SetDirty(ground);
            return ground;
        }

        private bool UpdateAutomaticPreviewFraming(bool force)
        {
            if (!previewAutomaticFraming)
                return false;

            double now = EditorApplication.timeSinceStartup;
            if (!force && now - previewLastBoundsRefreshTime < PreviewBoundsRefreshInterval)
                return false;
            previewLastBoundsRefreshTime = now;

            if (!TryCalculatePreviewBounds(out Bounds currentBounds))
                return false;

            ESEditorPreviewRenderContext previewRenderContext = PreviewRenderContext;
            if (previewRenderContext == null)
                return false;
            Vector3 localCenter = previewRenderContext.WorldToPreviewLocalPoint(currentBounds.center);
            previewRenderContext.ConfigureGroundPlane(
                localCenter,
                Mathf.Max(25f, Mathf.Max(currentBounds.size.x, currentBounds.size.z) * 1.35f));
            PreviewView.FrameRecommendedWorldBounds(previewRenderContext, currentBounds, 1.6f, 500f);
            // 推荐视角只在资源切换/用户重置时取景一次。播放中的粒子 Bounds 会持续变化，
            // 若逐帧追踪会导致相机中心和距离漂移，破坏上一个/下一个的快速检查体验。
            previewAutomaticFraming = false;
            return true;
        }

        private bool TryCalculatePreviewBounds(out Bounds combinedBounds)
        {
            combinedBounds = default;
            if (particlePreviewSession == null)
                return false;

            bool hasBounds = particlePreviewSession.TryCalculateRepresentativeBounds(out combinedBounds);
            if (!hasBounds)
                return false;

            ESEditorPreviewRenderContext context = PreviewRenderContext;
            Vector3 groupOrigin = context != null ? context.GroupOrigin : Vector3.zero;
            if (!IsUsablePreviewBounds(combinedBounds, groupOrigin))
                return false;

            // 推荐视角以特效本体为权威；可选的 1m 参照物摆到本体旁边后再参与构图。
            // 原点三轴由 HUD/世界轴单独表达，不允许错误 Pivot 把镜头拉到空白处。
            if (context != null && showOneMeterScaleReference)
            {
                context.PositionScaleReferenceBesideWorldBounds(combinedBounds);
                if (context.TryGetScaleReferenceBounds(out Bounds referenceBounds))
                    combinedBounds.Encapsulate(referenceBounds);
            }
            return true;
        }

        private void SetFallbackPreviewView()
        {
            int rootCount = particlePreviewSession != null
                ? particlePreviewSession.SourceRootCount
                : 0;
            PreviewView.ResetRecommended(
                Vector3.zero,
                Mathf.Clamp(1.6f + Mathf.Sqrt(Mathf.Max(0, rootCount - 1)) * 1.25f, 1.6f, 18f));
        }

        private static bool IsUsablePreviewBounds(Bounds bounds, Vector3 groupOrigin)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            bool finite = !float.IsNaN(center.x) && !float.IsInfinity(center.x) &&
                          !float.IsNaN(center.y) && !float.IsInfinity(center.y) &&
                          !float.IsNaN(center.z) && !float.IsInfinity(center.z) &&
                          !float.IsNaN(extents.x) && !float.IsInfinity(extents.x) &&
                          !float.IsNaN(extents.y) && !float.IsInfinity(extents.y) &&
                          !float.IsNaN(extents.z) && !float.IsInfinity(extents.z);
            return finite &&
                   extents.sqrMagnitude > 0.0001f &&
                   (center - groupOrigin).sqrMagnitude < 1000000f &&
                   extents.sqrMagnitude < 250000f;
        }

        private void ApplyPanelSettingsToParticleSystem(
            ParticleSystem particleSystem,
            ParticleLookPreset preset)
        {
            if (particleSystem == null)
                return;

            var main = particleSystem.main;
            main.duration = Mathf.Max(MinimumDuration, duration);
            main.loop = looping;
            main.startLifetime = startLifetime;
            main.startSpeed = startSpeed;
            main.startSize = startSize;
            main.startColor = startColor;
            main.simulationSpace = simulationSpace;

            var emission = particleSystem.emission;
            emission.rateOverTime = emissionRate;

            ApplyProceduralLook(particleSystem, preset);
        }

        private void SimulatePreviewAtCurrentTime()
        {
            if (!EnsurePreviewSession())
                return;

            try
            {
                particlePreviewSession.SetRandomSeed(previewRandomSeed);
                particlePreviewSession.Seek(previewTime);
                UpdateAutomaticPreviewFraming(force: false);
                SimpleToolsWindow.UsingWindow?.Repaint();
                lastResultSummary = $"预览定格: {previewTime:0.00}s | {PreviewParticleSystemCount} 个粒子系统";
                lastResultDetail = BuildPreviewIsolationDetail();
            }
            catch (Exception exception)
            {
                FailPreviewOperation("时间轴模拟", exception);
            }
        }

        public bool StartIndependentPreview()
        {
            RebuildParticleTargetSnapshot();
            return PlayPreviewFromBeginning();
        }

        public bool StopIndependentPreview()
        {
            bool hadPreview = HasIndependentPreviewSession;
            RestorePreviewSession();
            return hadPreview;
        }

        private bool PlayPreviewFromBeginning()
        {
            if (!EnsurePreviewSession())
                return false;

            try
            {
                previewTime = 0f;
                particlePreviewSession.SetRandomSeed(previewRandomSeed);
                particlePreviewSession.SetPlayback(PreviewTimelineMaximum, shouldLoop: true);
                particlePreviewSession.PlayFromBeginning();
                ResetPreviewView();
                lastResultSummary = $"预览播放: {PreviewParticleSystemCount} 个粒子系统";
                lastResultDetail = BuildPreviewIsolationDetail();
                return true;
            }
            catch (Exception exception)
            {
                FailPreviewOperation("开始播放", exception);
                return false;
            }
        }

        private void FailPreviewOperation(string action, Exception exception)
        {
            DisposeParticlePreviewSession();
            previewTimeNeedsSimulation = false;
            lastResultSummary = $"粒子预览失败: {action}";
            lastResultDetail = exception == null
                ? "未知错误。临时预览对象已清理。"
                : $"{exception.GetType().Name}: {exception.Message}\n临时预览对象已清理，原场景组件未修改。";
            if (exception != null)
                Debug.LogException(exception);
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private void PausePreview()
        {
            particlePreviewSession?.Pause();
            previewTime = particlePreviewSession != null ? particlePreviewSession.CurrentTime : previewTime;
            SimpleToolsWindow.UsingWindow?.Repaint();
            lastResultSummary = $"预览已暂停: {previewTime:0.00}s";
            lastResultDetail = "窗口内粒子保持在当前画面，可继续播放、单帧检查或拖动时间轴。";
        }

        private void ResumePreview()
        {
            if (!IsIndependentPreviewActive)
            {
                StartIndependentPreview();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                DisposeParticlePreviewSession();
                EditorUtility.DisplayDialog("无法预览", "请退出运行模式（Play Mode）后使用窗口独立预览。", "确定");
                return;
            }

            if (!particlePreviewSession.MatchesControlledSources(particleTargetSnapshot))
            {
                StartIndependentPreview();
                return;
            }

            try
            {
                particlePreviewSession.Resume();
                SimpleToolsWindow.UsingWindow?.Repaint();
                lastResultSummary = $"继续预览: {previewTime:0.00}s";
                lastResultDetail = "窗口内粒子从暂停位置继续推进，原场景粒子没有播放或修改。";
            }
            catch (Exception exception)
            {
                FailPreviewOperation("继续播放", exception);
            }
        }

        private void RestorePreviewSession()
        {
            if (!HasIndependentPreviewSession)
                return;

            int restoredCount = PreviewParticleSystemCount;
            DisposeParticlePreviewSession();
            previewTimeNeedsSimulation = false;
            lastResultSummary = $"预览已结束: 已清理 {restoredCount} 个临时粒子系统";
            lastResultDetail = "独立 PreviewScene 已释放；原场景粒子在预览期间没有播放或修改。";
            SimpleToolsWindow.UsingWindow?.Repaint();
        }

        private string BuildPreviewIsolationDetail()
        {
            if (particlePreviewSession == null)
                return "预览会话已释放。";
            if (particlePreviewSession.UnresolvedReferenceCount == 0 &&
                particlePreviewSession.SkippedComponentCount == 0)
                return "完整选中根对象已复制到独立 PreviewScene，并使用固定随机种子；原场景对象没有播放或修改。";

            return "独立 PreviewScene 已隔离运行；断开外部组件引用 "
                + particlePreviewSession.UnresolvedReferenceCount
                + " 个，跳过业务脚本或不安全组件 "
                + particlePreviewSession.SkippedComponentCount
                + " 个。原场景对象没有播放、执行或修改。";
        }

        private void DisposeParticlePreviewSession()
        {
            ReleasePreviewMouseControl();
            previewAutomaticFraming = true;
            previewSettingsNeedsRefresh = false;
            ESEditorParticlePreviewSession session = particlePreviewSession;
            particlePreviewSession = null;
            session?.Dispose();
        }

        private void DrawParticlePreviewPanel()
        {
            var targets = GetFilteredParticleTargets();
            SimpleToolsPanelUtility.DrawSectionTitle("对象明细（可选）", "按需展开，复核每个粒子系统的路径、当前状态和待应用参数。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                showObjectDetails = EditorGUILayout.Foldout(
                    showObjectDetails,
                    showObjectDetails ? $"收起对象明细 ({targets.Count})" : $"展开对象明细 ({targets.Count})",
                    true);
                if (!showObjectDetails)
                    return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                    EditorGUI.BeginChangeCheck();
                    particleSearch = EditorGUILayout.TextField(particleSearch);
                    if (EditorGUI.EndChangeCheck())
                    {
                        particlePreviewPageIndex = 0;
                        RebuildFilteredParticleSnapshot();
                    }
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                    {
                        particleSearch = string.Empty;
                        particlePreviewPageIndex = 0;
                        RebuildFilteredParticleSnapshot();
                    }
                }

                if (targets.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前选区没有命中的粒子系统。请先选择带 ParticleSystem 的对象，或开启包含子对象。");
                    return;
                }

                int particlePreviewStart;
                int particlePreviewEnd;
                SimpleToolsPanelUtility.GetPageRange(
                    targets,
                    ref particlePreviewPageIndex,
                    ParticlePreviewPageSize,
                    out _,
                    out particlePreviewStart,
                    out particlePreviewEnd);
                for (int i = particlePreviewStart; i < particlePreviewEnd; i++)
                    DrawParticlePreviewRow(targets[i]);

                SimpleToolsPanelUtility.DrawPager(ref particlePreviewPageIndex, targets.Count, ParticlePreviewPageSize);
            }
        }

        private List<ParticleSystem> GetFilteredParticleTargets()
        {
            return filteredParticleTargetSnapshot;
        }

        private static bool ParticleMatchesSearch(ParticleSystem ps, string keyword)
        {
            if (ps == null)
                return true;

            GameObject obj = ps.gameObject;
            string path = SimpleToolsSafetyUtility.GetHierarchyPath(obj);
            string space = ps.main.simulationSpace.ToString();
            return ContainsIgnoreCase(obj.name, keyword) ||
                   ContainsIgnoreCase(path, keyword) ||
                   ContainsIgnoreCase(space, keyword);
        }

        private static bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   !string.IsNullOrEmpty(keyword) &&
                   source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawParticlePreviewRow(ParticleSystem ps)
        {
            if (ps == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("<对象已失效>", EditorStyles.miniBoldLabel);
                    using (new EditorGUI.DisabledScope(true))
                        GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44));
                }
                EditorGUILayout.LabelField("该对象在本次 GUI 事件期间失效，将在下一次布局时移除。", EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(3f);
                return;
            }

            GameObject obj = ps.gameObject;
            var main = ps.main;
            var emission = ps.emission;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(SimpleToolsSafetyUtility.GetHierarchyPath(obj), EditorStyles.miniBoldLabel);
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    Selection.activeGameObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
            }
            EditorGUILayout.LabelField(
                $"Duration {main.duration:0.##}  |  Loop {(main.loop ? "是" : "否")}  |  Rate {emission.rateOverTime.constant:0.##}  |  {main.simulationSpace}  |  变更: {BuildParticleChangeSummary(ps)}",
                EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(3f);
        }

        private bool WillParticleSettingsChange(ParticleSystem ps)
        {
            if (ps == null)
                return false;

            if (quickLookPreset != ParticleLookPreset.Custom)
                return true;

            var main = ps.main;
            var emission = ps.emission;
            return !Mathf.Approximately(main.duration, Mathf.Max(MinimumDuration, duration)) ||
                   main.loop != looping ||
                   !CurveMatchesConstant(main.startLifetime, startLifetime) ||
                   !CurveMatchesConstant(main.startSpeed, startSpeed) ||
                   !CurveMatchesConstant(main.startSize, startSize) ||
                   !GradientMatchesColor(main.startColor, startColor) ||
                   !CurveMatchesConstant(emission.rateOverTime, emissionRate) ||
                   main.simulationSpace != simulationSpace;
        }

        private static bool CurveMatchesConstant(ParticleSystem.MinMaxCurve curve, float value)
        {
            return curve.mode == ParticleSystemCurveMode.Constant && Mathf.Approximately(curve.constant, value);
        }

        private static bool GradientMatchesColor(ParticleSystem.MinMaxGradient gradient, Color value)
        {
            return gradient.mode == ParticleSystemGradientMode.Color && gradient.color == value;
        }

        private string BuildParticleChangeSummary(ParticleSystem ps)
        {
            if (ps == null)
                return "无";

            var changes = new List<string>(10);
            var main = ps.main;
            var emission = ps.emission;
            if (quickLookPreset != ParticleLookPreset.Custom) changes.Add("模板模块");
            if (!Mathf.Approximately(main.duration, Mathf.Max(MinimumDuration, duration))) changes.Add("Duration");
            if (main.loop != looping) changes.Add("Loop");
            if (!CurveMatchesConstant(main.startLifetime, startLifetime)) changes.Add("Life");
            if (!CurveMatchesConstant(main.startSpeed, startSpeed)) changes.Add("Speed");
            if (!CurveMatchesConstant(main.startSize, startSize)) changes.Add("Size");
            if (!GradientMatchesColor(main.startColor, startColor)) changes.Add("Color");
            if (!CurveMatchesConstant(emission.rateOverTime, emissionRate)) changes.Add("Rate");
            if (main.simulationSpace != simulationSpace) changes.Add("Space");
            return changes.Count == 0 ? "不变" : string.Join("/", changes);
        }

        private bool ConfirmParticleOperation(string title, string action, List<ParticleSystem> targets)
        {
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return false;
            }

            if (targets.Count < OperationConfirmationThreshold)
                return true;

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(ps => ps != null ? ps.gameObject.name : null), 10);
            return SimpleToolsPanelUtility.ConfirmHeavyOperation(
                title,
                targets.Count,
                $"{action} {targets.Count} 个粒子系统。\n\n{preview}",
                "会批量影响选区内命中的 ParticleSystem。请确认包含子对象选项和命中清单。");
        }

        public void ApplyParticleSystemSettings()
        {
            if (selectedProjectPrefab != null)
            {
                lastResultSummary = "未写入：项目 Prefab 仅支持只读预览。";
                lastResultDetail = "请使用“创建场景案例”，或在正式资产工作流中另存为新变体；本页不会覆盖源资产。";
                return;
            }

            if (HasIndependentPreviewSession)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择 GameObject。", "确定");
                return;
            }

            bool truncated;
            var targets = CollectParticleTargets(out truncated);
            var changedTargets = targets.Where(WillParticleSettingsChange).ToList();
            if (changedTargets.Count == 0)
            {
                lastResultSummary = targets.Count == 0 ? "没有命中的粒子系统。" : "参数未变化，无需写入。";
                lastResultDetail = targets.Count == 0 ? "请检查当前选择和“包含子对象”选项。" : "当前命中对象已经使用这些参数。";
                return;
            }

            if (!ConfirmParticleOperation("确认应用粒子设置", "修改", changedTargets))
                return;

            int modifiedCount = 0;
            var renderers = quickLookPreset == ParticleLookPreset.Custom
                ? new List<ParticleSystemRenderer>()
                : changedTargets
                    .Select(ps => ps != null ? ps.GetComponent<ParticleSystemRenderer>() : null)
                    .Where(renderer => renderer != null)
                    .Distinct()
                    .ToList();
            var objectsToRecord = changedTargets.Cast<UnityEngine.Object>()
                .Concat(renderers.Cast<UnityEngine.Object>())
                .ToArray();
            Undo.RecordObjects(objectsToRecord, "Modify Particle Systems");
            foreach (var ps in changedTargets)
            {
                if (ps != null)
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ApplyPanelSettingsToParticleSystem(ps, quickLookPreset);

                    EditorUtility.SetDirty(ps);
                    if (PrefabUtility.IsPartOfPrefabInstance(ps))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
                    modifiedCount++;
                }
            }

            foreach (ParticleSystemRenderer renderer in renderers)
            {
                EditorUtility.SetDirty(renderer);
                if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }

            MarkScenesDirty(changedTargets);
            lastResultSummary = $"已应用参数: {modifiedCount} 个 | 命中: {targets.Count}" + (truncated ? " | 已按软上限截断" : string.Empty);
            string moduleDetail = quickLookPreset == ParticleLookPreset.Custom
                ? "仅应用基础参数。"
                : "已应用 Shape、Burst、生命周期曲线、Velocity、Gravity、Noise、Rotation、Trail 与 Renderer 模块。";
            lastResultDetail = moduleDetail + "\n" + SimpleToolsSafetyUtility.JoinPreview(changedTargets.Select(ps => ps.gameObject.name), 10);
        }

        private void ApplyAndSaveParticleSystemSettings()
        {
            if (selectedProjectPrefab != null)
            {
                lastResultSummary = "未保存：项目 Prefab 仅支持只读预览。";
                lastResultDetail = "请使用“创建场景案例”，或在正式资产工作流中另存为新变体；本页不会覆盖源资产。";
                return;
            }

            ApplyParticleSystemSettings();
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded && !string.IsNullOrEmpty(scene.path) && scene.isDirty)
            {
                SaveActiveScene();
            }
            else if (scene.IsValid() && scene.isLoaded && string.IsNullOrEmpty(scene.path))
            {
                lastResultSummary = "参数已应用，但场景未保存：当前场景没有路径。";
                lastResultDetail = "请先使用 Unity 的“另存为”命名场景，再点击“保存当前场景”。";
            }
        }

        public void PlayAllParticleSystems()
        {
            if (HasIndependentPreviewSession)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择 GameObject。", "确定");
                return;
            }

            bool truncated;
            var targets = CollectParticleTargets(out truncated);
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return;
            }

            int playedCount = 0;
            foreach (var ps in targets)
            {
                if (ps != null)
                {
                    ps.Play(false);
                    playedCount++;
                }
            }

            SceneView.RepaintAll();

            lastResultSummary = $"已播放: {playedCount} 个粒子系统" + (truncated ? " | 已按软上限截断" : string.Empty);
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(targets.Select(ps => ps.gameObject.name), 10);
        }

        public void StopAllParticleSystems()
        {
            if (HasIndependentPreviewSession)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择 GameObject。", "确定");
                return;
            }

            bool truncated;
            var targets = CollectParticleTargets(out truncated);
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return;
            }

            int stoppedCount = 0;
            foreach (var ps in targets)
            {
                if (ps != null)
                {
                    ps.Stop(false);
                    stoppedCount++;
                }
            }

            SceneView.RepaintAll();
            lastResultSummary = $"已停止: {stoppedCount} 个粒子系统" + (truncated ? " | 已按软上限截断" : string.Empty);
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(targets.Select(ps => ps.gameObject.name), 10);
        }

        public void ClearAllParticleSystems()
        {
            if (HasIndependentPreviewSession)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            bool truncated;
            var targets = CollectParticleTargets(out truncated);
            if (!ConfirmParticleOperation("确认清空粒子", "清空", targets))
                return;

            int clearedCount = 0;
            foreach (var ps in targets)
            {
                if (ps != null)
                {
                    ps.Clear(false);
                    clearedCount++;
                }
            }

            SceneView.RepaintAll();
            lastResultSummary = $"已清空: {clearedCount} 个粒子系统" + (truncated ? " | 已按软上限截断" : string.Empty);
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(targets.Select(ps => ps.gameObject.name), 10);
        }

        private void MarkScenesDirty(IEnumerable<ParticleSystem> targets)
        {
            if (targets == null)
                return;

            foreach (var scene in targets
                .Where(ps => ps != null && ps.gameObject.scene.IsValid())
                .Select(ps => ps.gameObject.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        public override void OnPageDisable()
        {
            DisposeParticlePreviewSession();
            if (selectionChangedRegistered)
            {
                Selection.selectionChanged -= OnUnitySelectionChanged;
                selectionChangedRegistered = false;
            }
            base.OnPageDisable();
        }
    }
    #endregion

}
