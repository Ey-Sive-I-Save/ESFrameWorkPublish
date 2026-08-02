using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Animations;


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


        [DisplayAsString(fontSize: 13), HideLabel]
        public string readMe = "选择包含 ParticleSystem 的对象，按需包含子对象。应用会修改参数；播放/停止只发送预览指令；清空会先确认。";

        [HideInInspector]
        private string TargetSummary
        {
            get
            {
                int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                int targetCount = GetParticleTargets().Count;
                return $"当前选择: {selectedCount} 个对象 | 命中粒子系统: {targetCount} 个 | 包含子对象: {(includeChildren ? "是" : "否")}";
            }
        }

        [LabelText("包含子对象"), Space(5)]
        public bool includeChildren = true;

        [LabelText("持续时间"), Range(0f, 10f), Space(5)]
        public float duration = 5f;

        [LabelText("循环播放"), Space(5)]
        public bool looping = true;

        [LabelText("开始生命周期"), Range(0f, 10f), Space(5)]
        public float startLifetime = 5f;

        [LabelText("开始速度"), Range(0f, 100f), Space(5)]
        public float startSpeed = 5f;

        [LabelText("开始大小"), Range(0f, 10f), Space(5)]
        public float startSize = 1f;

        [LabelText("开始颜色"), Space(5)]
        public Color startColor = Color.white;

        [LabelText("发射速率"), Range(0f, 1000f), Space(5)]
        public float emissionRate = 10f;

        [LabelText("模拟空间"), Space(5)]
        public ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.Local;

        private string lastResultSummary = "";
        private string lastResultDetail = "";
        private string particleSearch = "";
        private int particlePreviewPageIndex;
        private const int ParticlePreviewPageSize = 12;
        private const float PreviewTimelineMaximum = 10f;
        private const float PreviewFrameStep = 1f / 30f;
        private const int PreviewConfirmationThreshold = 64;
        private float previewTime;
        private int previewRandomSeed = 12345;
        private bool previewIsPlaying;
        private readonly List<ParticlePreviewState> previewStates = new List<ParticlePreviewState>();
        private ParticleLookPreset quickLookPreset = ParticleLookPreset.Custom;
        private float quickIntensity = 1f;
        private float quickMotion = 1f;
        private float quickScale = 1f;

        private sealed class ParticlePreviewState
        {
            public ParticleSystem particleSystem;
            public bool useAutoRandomSeed;
            public uint randomSeed;
            public bool wasPlaying;
            public bool wasPaused;
            public float time;
        }

        [OnInspectorGUI, PropertyOrder(100)]
        private void DrawResultPanel()
        {
            int targetCount = GetParticleTargets().Count;
            SimpleToolsPanelUtility.DrawToolHeader(
                "粒子系统批量调整",
                "粒子系统批量调整",
                SimpleToolsMaturity.Upgrading,
                "应用参数和清空会直接影响场景对象；播放/停止只是发送编辑器播放指令，不代表已经预览应用后的参数效果。");
            SimpleToolsPanelUtility.DrawLargeListGuard(targetCount, "粒子系统");
            DrawQuickTuningPanel();
            DrawSceneViewPreviewPanel();
            DrawParticleActionPanel();
            DrawParticlePreviewPanel();
            SimpleToolsPanelUtility.DrawResultSummary("最近粒子操作", lastResultSummary, lastResultDetail);
        }

        private void DrawParticleActionPanel()
        {
            var targets = GetParticleTargets();
            int loopingCount = targets.Count(obj => obj != null && obj.GetComponent<ParticleSystem>() != null && obj.GetComponent<ParticleSystem>().main.loop);
            int worldSpaceCount = targets.Count(obj => obj != null && obj.GetComponent<ParticleSystem>() != null && obj.GetComponent<ParticleSystem>().main.simulationSpace == ParticleSystemSimulationSpace.World);
            int changedCount = targets.Count(WillParticleSettingsChange);

            SimpleToolsPanelUtility.DrawSectionTitle("核心流程", "先看参数变更预览，再选择写入参数或发送播放/停止指令。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                SimpleToolsPanelUtility.DrawSummary(
                    $"命中: {targets.Count}",
                    $"命中: {targets.Count}",
                    $"Loop: {loopingCount}",
                    $"WorldSpace: {worldSpaceCount}",
                    $"写入参数: Duration/Loop/Lifetime/Speed/Size/Color/Rate/Space");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("应用参数", SimpleToolsActionTone.Warning, 30, GUILayout.Width(92)))
                        ApplyParticleSystemSettings();
                    if (SimpleToolsPanelUtility.DrawActionButton("清空粒子", SimpleToolsActionTone.Danger, 30, GUILayout.Width(92)))
                        ClearAllParticleSystems();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private List<GameObject> GetParticleTargets()
        {
            bool truncated;
            var targets = SimpleToolsSafetyUtility.CollectTargets(
                    Selection.gameObjects,
                    includeChildren,
                    true,
                    SimpleToolsSafetyUtility.DefaultCollectSoftLimit,
                    out truncated)
                .Where(obj => obj != null && obj.GetComponent<ParticleSystem>() != null)
                .ToList();

            if (truncated)
                Debug.LogWarning("[SimpleTools] 粒子系统目标收集达到软上限，已截断预览/操作范围。");

            return targets;
        }

        private void DrawQuickTuningPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle(
                "用于批量统一粒子系统的主模块、发射速率、模拟空间，并快速播放/停止/清空选区内粒子。",
                "应用参数和清空会直接影响场景对象；播放/停止只是发送编辑器播放指令，不代表已经预览应用后的参数效果。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    quickLookPreset = (ParticleLookPreset)EditorGUILayout.EnumPopup("目标效果", quickLookPreset, GUILayout.Width(180));
                    if (SimpleToolsPanelUtility.DrawActionButton("生成建议参数", SimpleToolsActionTone.Primary, 26, GUILayout.Width(98)))
                        ApplyQuickLookPreset();
                    if (SimpleToolsPanelUtility.DrawActionButton("设为自定义", SimpleToolsActionTone.Neutral, 26, GUILayout.Width(88)))
                        quickLookPreset = ParticleLookPreset.Custom;
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(GetQuickLookDescription(quickLookPreset), EditorStyles.miniLabel, GUILayout.MinWidth(150));
                }

                EditorGUI.BeginChangeCheck();
                quickIntensity = EditorGUILayout.Slider("强度（发射量）", quickIntensity, 0.1f, 3f);
                quickMotion = EditorGUILayout.Slider("运动感（速度）", quickMotion, 0.1f, 3f);
                quickScale = EditorGUILayout.Slider("体积感（尺寸）", quickScale, 0.1f, 3f);
                if (EditorGUI.EndChangeCheck() && quickLookPreset != ParticleLookPreset.Custom)
                    ApplyQuickLookPreset();
            }
        }

        private void ApplyQuickLookPreset()
        {
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
                    baseEmissionRate = 36f;
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

            duration = Mathf.Clamp(baseDuration, 0f, 10f);
            looping = baseLoop;
            startLifetime = Mathf.Clamp(baseLifetime, 0f, 10f);
            startSpeed = Mathf.Clamp(baseSpeed * quickMotion, 0f, 100f);
            startSize = Mathf.Clamp(baseSize * quickScale, 0f, 10f);
            startColor = baseColor;
            emissionRate = Mathf.Clamp(baseEmissionRate * quickIntensity, 0f, 1000f);
            simulationSpace = baseSpace;
            particlePreviewPageIndex = 0;
            lastResultSummary = "快速调参已更新；参数尚未写入场景。";
            lastResultDetail = $"强度 {quickIntensity:0.0}× | 运动 {quickMotion:0.0}× | 体积 {quickScale:0.0}×。参数尚未写入场景，可先查看变更预览。";
        }

        private static string GetQuickLookDisplayName(ParticleLookPreset preset)
        {
            switch (preset)
            {
                case ParticleLookPreset.Fire: return "火焰";
                case ParticleLookPreset.Smoke: return "烟雾";
                case ParticleLookPreset.Sparks: return "火花";
                case ParticleLookPreset.Dust: return "飘尘";
                case ParticleLookPreset.Magic: return "魔法光点";
                default: return "自定义";
            }
        }

        private static string GetQuickLookDescription(ParticleLookPreset preset)
        {
            switch (preset)
            {
                case ParticleLookPreset.Fire: return "短寿命、高频、局部空间";
                case ParticleLookPreset.Smoke: return "长寿命、低速、世界空间";
                case ParticleLookPreset.Sparks: return "短促爆发、高速、世界空间";
                case ParticleLookPreset.Dust: return "轻量飘散、低速、世界空间";
                case ParticleLookPreset.Magic: return "中寿命、局部循环、冷色";
                default: return "保留当前手动参数";
            }
        }

        private void DrawSceneViewPreviewPanel()
        {
            SimpleToolsPanelUtility.DrawSectionTitle(
                "SceneView 真实预览",
                "应用参数和清空会直接影响场景对象；播放/停止只是发送编辑器播放指令，不代表已经预览应用后的参数效果。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("预览时间", EditorStyles.miniBoldLabel, GUILayout.Width(56));
                    EditorGUI.BeginChangeCheck();
                    float nextPreviewTime = EditorGUILayout.Slider(previewTime, 0f, PreviewTimelineMaximum);
                    if (EditorGUI.EndChangeCheck())
                    {
                        previewTime = nextPreviewTime;
                        SimulatePreviewAtCurrentTime();
                    }

                    EditorGUILayout.LabelField("随机种子", EditorStyles.miniBoldLabel, GUILayout.Width(56));
                    previewRandomSeed = EditorGUILayout.IntField(previewRandomSeed, GUILayout.Width(72));
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("从头预览", SimpleToolsActionTone.Primary, 28, GUILayout.Width(88)))
                    {
                        previewTime = 0f;
                        SimulatePreviewAtCurrentTime();
                    }
                    if (SimpleToolsPanelUtility.DrawActionButton(previewIsPlaying ? "暂停并定格" : "继续播放", SimpleToolsActionTone.Success, 28, GUILayout.Width(88)))
                    {
                        if (previewIsPlaying)
                            PausePreview();
                        else
                            PlayPreview();
                    }
                    if (SimpleToolsPanelUtility.DrawActionButton("前进一步", SimpleToolsActionTone.Neutral, 28, GUILayout.Width(76)))
                    {
                        previewTime = Mathf.Min(PreviewTimelineMaximum, previewTime + PreviewFrameStep);
                        SimulatePreviewAtCurrentTime();
                    }
                    if (SimpleToolsPanelUtility.DrawActionButton("结束并恢复", SimpleToolsActionTone.Warning, 28, GUILayout.Width(88)))
                        RestorePreviewSession();

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(previewStates.Count > 0 ? $"预览中 {previewStates.Count}" : "未启动预览", EditorStyles.miniLabel);
                }
            }
        }

        private bool EnsurePreviewSession()
        {
            var particleSystems = GetPreviewParticleSystems();
            if (particleSystems.Count == 0)
            {
                EditorUtility.DisplayDialog("无法预览", "当前选区没有可预览的 ParticleSystem。", "确定");
                return false;
            }

            if (previewStates.Count > 0 && !IsSamePreviewSession(particleSystems))
                RestorePreviewSession();

            if (previewStates.Count == 0)
            {
                if (particleSystems.Count >= PreviewConfirmationThreshold &&
                    !SimpleToolsPanelUtility.ConfirmHeavyOperation(
                        "确认粒子预览",
                        particleSystems.Count,
                "用于批量统一粒子系统的主模块、发射速率、模拟空间，并快速播放/停止/清空选区内粒子。",
                        "预览不会写入参数；结束预览会恢复随机种子和播放状态。"))
                    return false;

                foreach (var particleSystem in particleSystems)
                {
                    previewStates.Add(new ParticlePreviewState
                    {
                        particleSystem = particleSystem,
                        useAutoRandomSeed = particleSystem.useAutoRandomSeed,
                        randomSeed = particleSystem.randomSeed,
                        wasPlaying = particleSystem.isPlaying,
                        wasPaused = particleSystem.isPaused,
                        time = particleSystem.time
                    });
                }
            }

            uint seed = unchecked((uint)previewRandomSeed);
            foreach (var state in previewStates)
            {
                if (state.particleSystem == null)
                    continue;

                state.particleSystem.useAutoRandomSeed = false;
                state.particleSystem.randomSeed = seed;
            }

            return true;
        }

        private List<ParticleSystem> GetPreviewParticleSystems()
        {
            var systems = new List<ParticleSystem>();
            var seenInstanceIds = new HashSet<int>();
            foreach (var target in GetParticleTargets())
            {
                if (target == null)
                    continue;

                foreach (var particleSystem in target.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (particleSystem != null && seenInstanceIds.Add(particleSystem.GetInstanceID()))
                        systems.Add(particleSystem);
                }
            }

            return systems;
        }

        private List<ParticleSystem> GetPreviewRootParticleSystems()
        {
            var systems = previewStates
                .Select(state => state.particleSystem)
                .Where(particleSystem => particleSystem != null)
                .ToList();
            var systemIds = new HashSet<int>(systems.Select(item => item.GetInstanceID()));
            return systems.Where(particleSystem =>
            {
                Transform parent = particleSystem.transform.parent;
                while (parent != null)
                {
                    var parentParticleSystem = parent.GetComponent<ParticleSystem>();
                    if (parentParticleSystem != null && systemIds.Contains(parentParticleSystem.GetInstanceID()))
                        return false;

                    parent = parent.parent;
                }

                return true;
            }).ToList();
        }

        private bool IsSamePreviewSession(List<ParticleSystem> particleSystems)
        {
            if (particleSystems.Count != previewStates.Count)
                return false;

            var currentIds = new HashSet<int>(particleSystems.Select(item => item.GetInstanceID()));
            return previewStates.All(state => state.particleSystem != null && currentIds.Contains(state.particleSystem.GetInstanceID()));
        }

        private void SimulatePreviewAtCurrentTime()
        {
            if (!EnsurePreviewSession())
                return;

            previewIsPlaying = false;
            foreach (var particleSystem in GetPreviewRootParticleSystems())
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Simulate(previewTime, true, true, true);
                particleSystem.Pause(true);
            }

            SceneView.RepaintAll();
            lastResultSummary = $"预览定格: {previewTime:0.00}s | {previewStates.Count} 个粒子系统";
            lastResultDetail = "使用固定随机种子在 SceneView 中模拟当前参数；点击“结束并恢复”可恢复预览前的随机设置和播放状态。";
        }

        private void PlayPreview()
        {
            if (!EnsurePreviewSession())
                return;

            foreach (var particleSystem in GetPreviewRootParticleSystems())
            {
                particleSystem.Play(true);
            }

            previewIsPlaying = true;
            SceneView.RepaintAll();
            lastResultSummary = $"预览播放: {previewStates.Count} 个粒子系统";
            lastResultDetail = "播放使用固定随机种子。可暂停定格、拖动时间轴，或结束并恢复。";
        }

        private void PausePreview()
        {
            foreach (var particleSystem in GetPreviewRootParticleSystems())
            {
                particleSystem.Pause(true);
            }

            previewIsPlaying = false;
            SceneView.RepaintAll();
        }

        private void RestorePreviewSession()
        {
            if (previewStates.Count == 0)
                return;

            foreach (var state in previewStates)
            {
                var particleSystem = state.particleSystem;
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.useAutoRandomSeed = state.useAutoRandomSeed;
                particleSystem.randomSeed = state.randomSeed;
            }

            foreach (var particleSystem in GetPreviewRootParticleSystems())
            {
                var state = previewStates.FirstOrDefault(item => item.particleSystem == particleSystem);
                if (state == null)
                    continue;

                particleSystem.Simulate(Mathf.Max(0f, state.time), true, true, true);
                if (state.wasPlaying)
                    particleSystem.Play(true);
                else if (state.wasPaused)
                    particleSystem.Pause(true);
            }

            int restoredCount = previewStates.Count;
            previewStates.Clear();
            previewIsPlaying = false;
            SceneView.RepaintAll();
            lastResultSummary = $"预览已结束并恢复: {restoredCount} 个粒子系统";
            lastResultDetail = "随机种子和预览前播放状态已恢复。";
        }

        private void DrawParticlePreviewPanel()
        {
            var targets = GetFilteredParticleTargets();
            SimpleToolsPanelUtility.DrawSectionTitle("参数变更预览", "按对象名、路径、模拟空间搜索；表格显示当前参数和应用后是否会变。这里不修改场景。");
            using (SimpleToolsPanelUtility.BeginContentSection())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("搜索", EditorStyles.miniBoldLabel, GUILayout.Width(36));
                    particleSearch = EditorGUILayout.TextField(particleSearch);
                    if (GUILayout.Button("清空", EditorStyles.miniButton, GUILayout.Width(48)))
                    {
                        particleSearch = string.Empty;
                        particlePreviewPageIndex = 0;
                    }
                }

                if (targets.Count == 0)
                {
                    SimpleToolsPanelUtility.DrawEmptyState("当前选区没有命中的粒子系统。请先选择带 ParticleSystem 的对象，或开启包含子对象。");
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("对象路径", EditorStyles.miniBoldLabel, GUILayout.MinWidth(180));
                    EditorGUILayout.LabelField("Duration", EditorStyles.miniBoldLabel, GUILayout.Width(64));
                    EditorGUILayout.LabelField("Loop", EditorStyles.miniBoldLabel, GUILayout.Width(42));
                    EditorGUILayout.LabelField("Rate", EditorStyles.miniBoldLabel, GUILayout.Width(52));
                    EditorGUILayout.LabelField("Space", EditorStyles.miniBoldLabel, GUILayout.Width(72));
                    EditorGUILayout.LabelField("变更", EditorStyles.miniBoldLabel, GUILayout.Width(140));
                    GUILayout.Space(48);
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

        private List<GameObject> GetFilteredParticleTargets()
        {
            var targets = GetParticleTargets();
            if (string.IsNullOrWhiteSpace(particleSearch))
                return targets;

            string keyword = particleSearch.Trim();
            return targets.Where(obj =>
            {
                if (obj == null)
                    return false;

                ParticleSystem ps = obj.GetComponent<ParticleSystem>();
                string path = SimpleToolsSafetyUtility.GetHierarchyPath(obj);
                string space = ps != null ? ps.main.simulationSpace.ToString() : string.Empty;
                return ContainsIgnoreCase(obj.name, keyword) ||
                       ContainsIgnoreCase(path, keyword) ||
                       ContainsIgnoreCase(space, keyword);
            }).ToList();
        }

        private static bool ContainsIgnoreCase(string source, string keyword)
        {
            return !string.IsNullOrEmpty(source) &&
                   !string.IsNullOrEmpty(keyword) &&
                   source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawParticlePreviewRow(GameObject obj)
        {
            ParticleSystem ps = obj != null ? obj.GetComponent<ParticleSystem>() : null;
            if (ps == null)
                return;

            var main = ps.main;
            var emission = ps.emission;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(SimpleToolsSafetyUtility.GetHierarchyPath(obj), EditorStyles.miniLabel, GUILayout.MinWidth(180));
                EditorGUILayout.LabelField(main.duration.ToString("0.##"), EditorStyles.miniLabel, GUILayout.Width(64));
                EditorGUILayout.LabelField(main.loop ? "是" : "否", EditorStyles.miniLabel, GUILayout.Width(42));
                EditorGUILayout.LabelField(emission.rateOverTime.constant.ToString("0.##"), EditorStyles.miniLabel, GUILayout.Width(52));
                EditorGUILayout.LabelField(main.simulationSpace.ToString(), EditorStyles.miniLabel, GUILayout.Width(72));
                EditorGUILayout.LabelField(BuildParticleChangeSummary(ps), EditorStyles.miniLabel, GUILayout.Width(140));
                if (GUILayout.Button("定位", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    Selection.activeGameObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
            }
        }

        private bool WillParticleSettingsChange(GameObject obj)
        {
            ParticleSystem ps = obj != null ? obj.GetComponent<ParticleSystem>() : null;
            return ps != null && WillParticleSettingsChange(ps);
        }

        private bool WillParticleSettingsChange(ParticleSystem ps)
        {
            var main = ps.main;
            var emission = ps.emission;
            return !Mathf.Approximately(main.duration, duration) ||
                   main.loop != looping ||
                   !Mathf.Approximately(main.startLifetime.constant, startLifetime) ||
                   !Mathf.Approximately(main.startSpeed.constant, startSpeed) ||
                   !Mathf.Approximately(main.startSize.constant, startSize) ||
                   main.startColor.color != startColor ||
                   !Mathf.Approximately(emission.rateOverTime.constant, emissionRate) ||
                   main.simulationSpace != simulationSpace;
        }

        private string BuildParticleChangeSummary(ParticleSystem ps)
        {
            if (ps == null)
                return "无";

            var changes = new List<string>(4);
            var main = ps.main;
            var emission = ps.emission;
            if (!Mathf.Approximately(main.duration, duration)) changes.Add("Duration");
            if (main.loop != looping) changes.Add("Loop");
            if (!Mathf.Approximately(main.startLifetime.constant, startLifetime)) changes.Add("Life");
            if (!Mathf.Approximately(main.startSpeed.constant, startSpeed)) changes.Add("Speed");
            if (!Mathf.Approximately(main.startSize.constant, startSize)) changes.Add("Size");
            if (main.startColor.color != startColor) changes.Add("Color");
            if (!Mathf.Approximately(emission.rateOverTime.constant, emissionRate)) changes.Add("Rate");
            if (main.simulationSpace != simulationSpace) changes.Add("Space");
            return changes.Count == 0 ? "不变" : string.Join("/", changes);
        }

        private bool ConfirmParticleOperation(string title, string action, List<GameObject> targets)
        {
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return false;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            return SimpleToolsPanelUtility.ConfirmHeavyOperation(
                title,
                targets.Count,
                $"{action} {targets.Count} 个粒子系统。\n\n{preview}",
                "会批量影响选区内命中的 ParticleSystem。请确认包含子对象选项和命中清单。");
        }

        [FoldoutGroup("旧按钮入口", Expanded = false)]
        [Button("应用参数到选中粒子", ButtonHeight = 34)]
        public void ApplyParticleSystemSettings()
        {
            if (previewStates.Count > 0)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择 GameObject。", "确定");
                return;
            }

            var targets = GetParticleTargets();

            // 统计将被修改的粒子系统数量。
            if (!ConfirmParticleOperation("确认应用粒子设置", "修改", targets))
                return;

            int modifiedCount = 0;
            foreach (var obj in targets)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    Undo.RecordObject(ps, "Modify Particle System");

                    // Stop the particle system before modifying duration
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                    var main = ps.main;
                    main.duration = duration;
                    main.loop = looping;
                    main.startLifetime = startLifetime;
                    main.startSpeed = startSpeed;
                    main.startSize = startSize;
                    main.startColor = startColor;
                    main.simulationSpace = simulationSpace;

                    var emission = ps.emission;
                    emission.rateOverTime = emissionRate;

                    EditorUtility.SetDirty(ps);
                    modifiedCount++;
                }
            }

            MarkScenesDirty(targets);
            lastResultSummary = $"已应用参数: {modifiedCount} 个粒子系统 | 目标: {targets.Count}";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            EditorUtility.DisplayDialog("粒子参数已应用", $"已修改 {modifiedCount} 个粒子系统。", "完成");
        }

        [FoldoutGroup("旧按钮入口")]
        [Button("播放选中粒子", ButtonHeight = 32)]
        public void PlayAllParticleSystems()
        {
            if (previewStates.Count > 0)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择 GameObject。", "确定");
                return;
            }

            var targets = GetParticleTargets();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return;
            }

            int playedCount = 0;
            var objectsToSelect = new List<GameObject>();
            foreach (var obj in targets)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    
                    ps.Play();
                    
                    playedCount++;
                    objectsToSelect.Add(obj);
                }
            }

            // 选中所有播放的粒子系统 GameObject
            Selection.objects = objectsToSelect.ToArray();

            // 刷新 Scene 视图以确保粒子播放可见。
            UnityEditor.SceneView.RepaintAll();

            lastResultSummary = $"已发送播放: {playedCount} 个粒子系统 | 目标: {targets.Count}";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(objectsToSelect.Select(obj => obj.name), 10);
            EditorUtility.DisplayDialog("粒子播放已发送", $"已播放 {playedCount} 个粒子系统。", "完成");
        }

        [FoldoutGroup("旧按钮入口")]
        [Button("停止选中粒子", ButtonHeight = 32)]
        public void StopAllParticleSystems()
        {
            if (previewStates.Count > 0)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择 GameObject。", "确定");
                return;
            }

            var targets = GetParticleTargets();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return;
            }

            int stoppedCount = 0;
            foreach (var obj in targets)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop();
                    stoppedCount++;
                }
            }

            lastResultSummary = $"已发送停止: {stoppedCount} 个粒子系统 | 目标: {targets.Count}";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            EditorUtility.DisplayDialog("粒子停止已发送", $"已停止 {stoppedCount} 个粒子系统。", "完成");
        }

        [FoldoutGroup("旧按钮入口")]
        [Button("清空选中粒子", ButtonHeight = 32)]
        public void ClearAllParticleSystems()
        {
            if (previewStates.Count > 0)
                RestorePreviewSession();

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var targets = GetParticleTargets();
            if (!ConfirmParticleOperation("确认清空粒子", "清空", targets))
                return;

            int clearedCount = 0;
            foreach (var obj in targets)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Clear();
                    clearedCount++;
                }
            }

            MarkScenesDirty(targets);
            lastResultSummary = $"已清空: {clearedCount} 个粒子系统 | 目标: {targets.Count}";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            EditorUtility.DisplayDialog("粒子已清空", $"已清空 {clearedCount} 个粒子系统。", "完成");
        }

        private void MarkScenesDirty(IEnumerable<GameObject> targets)
        {
            if (targets == null)
                return;

            foreach (var scene in targets
                .Where(obj => obj != null && obj.scene.IsValid())
                .Select(obj => obj.scene)
                .Distinct())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        public override void OnPageDisable()
        {
            if (previewStates.Count > 0)
                RestorePreviewSession();
            base.OnPageDisable();
        }
    }
    #endregion

}
