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

        [OnInspectorGUI]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawResultSummary("最近粒子操作", lastResultSummary, lastResultDetail);
        }

        private List<GameObject> GetParticleTargets()
        {
            return SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren)
                .Where(obj => obj != null && obj.GetComponent<ParticleSystem>() != null)
                .ToList();
        }

        private bool ConfirmParticleOperation(string title, string action, List<GameObject> targets)
        {
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return false;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            return EditorUtility.DisplayDialog(title,
                $"将{action} {targets.Count} 个粒子系统。\n\n{preview}\n\n继续吗？",
                "确认", "取消");
        }

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

        [Button("播放选中粒子", ButtonHeight = 32), GUIColor(0.25f, 0.62f, 0.45f)]
        public void PlayAllParticleSystems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var targets = allObjects
                .Where(obj => obj != null && obj.GetComponent<ParticleSystem>() != null)
                .ToList();
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

        [Button("停止选中粒子", ButtonHeight = 32), GUIColor(0.75f, 0.58f, 0.25f)]
        public void StopAllParticleSystems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var targets = allObjects
                .Where(obj => obj != null && obj.GetComponent<ParticleSystem>() != null)
                .ToList();
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
