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
    public class Page_ParticleSystemAdjustment : ESWindowPageBase
    {
        [Title("粒子系统批量调整工具", "批量调整粒子系统参数", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择带 ParticleSystem 的对象，按需包含子对象。应用会修改参数；播放/停止只发送预览指令；清空会先确认。";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-5)]
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

        [OnInspectorGUI, PropertyOrder(-200)]
        private void DrawResultPanel()
        {
            int targetCount = GetParticleTargets().Count;
            SimpleToolsPanelUtility.DrawToolHeader(
                "粒子系统批量调整",
                "用于批量统一粒子系统的主模块、发射速率、模拟空间，并快速播放/停止/清空选区内粒子。",
                SimpleToolsMaturity.Upgrading,
                "应用参数和清空会直接影响场景对象；播放/停止只是发送编辑器播放指令，不代表已经预览应用后的参数效果。");
            SimpleToolsPanelUtility.DrawLargeListGuard(targetCount, "粒子系统");
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
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                SimpleToolsPanelUtility.DrawSummary(
                    $"命中: {targets.Count}",
                    $"将变更: {changedCount}",
                    $"Loop: {loopingCount}",
                    $"WorldSpace: {worldSpaceCount}",
                    $"写入参数: Duration/Loop/Lifetime/Speed/Size/Color/Rate/Space");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (SimpleToolsPanelUtility.DrawActionButton("应用参数", SimpleToolsActionTone.Warning, 30, GUILayout.Width(92)))
                        ApplyParticleSystemSettings();
                    if (SimpleToolsPanelUtility.DrawActionButton("播放当前", SimpleToolsActionTone.Success, 30, GUILayout.Width(92)))
                        PlayAllParticleSystems();
                    if (SimpleToolsPanelUtility.DrawActionButton("停止当前", SimpleToolsActionTone.Neutral, 30, GUILayout.Width(92)))
                        StopAllParticleSystems();
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

        private void DrawParticlePreviewPanel()
        {
            var targets = GetFilteredParticleTargets();
            SimpleToolsPanelUtility.DrawSectionTitle("参数变更预览", "按对象名、路径、模拟空间搜索；表格显示当前参数和应用后是否会变。这里不修改场景。");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
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

                foreach (var obj in SimpleToolsPanelUtility.PageItems(targets, ref particlePreviewPageIndex, ParticlePreviewPageSize, out _))
                    DrawParticlePreviewRow(obj);

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

        [FoldoutGroup("4. 旧按钮入口", Expanded = false)]
        [Button("应用参数到选中粒子", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void ApplyParticleSystemSettings()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var targets = GetParticleTargets();

            // 统计将被修改的粒子系统数量
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

        [FoldoutGroup("4. 旧按钮入口")]
        [Button("播放选中粒子", ButtonHeight = 32), GUIColor(0.25f, 0.62f, 0.45f)]
        public void PlayAllParticleSystems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
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

            // 刷新 Scene 视图以确保粒子播放可见
            UnityEditor.SceneView.RepaintAll();

            lastResultSummary = $"已发送播放: {playedCount} 个粒子系统 | 目标: {targets.Count}";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(objectsToSelect.Select(obj => obj.name), 10);
            EditorUtility.DisplayDialog("粒子播放已发送", $"已播放 {playedCount} 个粒子系统。", "完成");
        }

        [FoldoutGroup("4. 旧按钮入口")]
        [Button("停止选中粒子", ButtonHeight = 32), GUIColor(0.75f, 0.58f, 0.25f)]
        public void StopAllParticleSystems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
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

        [FoldoutGroup("4. 旧按钮入口")]
        [Button("清空选中粒子", ButtonHeight = 32), GUIColor(0.82f, 0.38f, 0.30f)]
        public void ClearAllParticleSystems()
        {
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
    }
    #endregion

}
