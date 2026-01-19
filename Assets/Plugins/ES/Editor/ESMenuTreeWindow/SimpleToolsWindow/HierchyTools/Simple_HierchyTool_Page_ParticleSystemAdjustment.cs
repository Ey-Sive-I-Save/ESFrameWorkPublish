using ES;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;


namespace ES
{

    #region 粒子系统批量调整工具
    [Serializable]
    public class Page_ParticleSystemAdjustment : ESWindowPageBase
    {
        [Title("粒子系统批量调整工具", "批量调整粒子系统参数", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择带有ParticleSystem的GameObject，\n设置粒子参数，\n点击应用按钮批量修改";

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

        [Button("应用粒子系统设置", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void ApplyParticleSystemSettings()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            // 统计将被修改的粒子系统数量
            int particleSystemCount = allObjects.Count(obj => obj.GetComponent<ParticleSystem>() != null);
            if (particleSystemCount == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到粒子系统！", "确定");
                return;
            }

            // 弹出确认面板
            bool confirm = EditorUtility.DisplayDialog("确认应用", $"将修改 {particleSystemCount} 个粒子系统，确认应用？", "确认", "取消");
            if (!confirm)
            {
                return;
            }

            int modifiedCount = 0;
            foreach (var obj in allObjects)
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

            // EditorUtility.DisplayDialog("成功", $"成功修改 {modifiedCount} 个粒子系统！", "确定");
        }

        [Button("批量播放粒子系统", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void PlayAllParticleSystems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            int playedCount = 0;
            var objectsToSelect = new List<GameObject>();
            foreach (var obj in allObjects)
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

            // EditorUtility.DisplayDialog("成功", $"成功播放 {playedCount} 个粒子系统！", "确定");
        }

        [Button("批量停止粒子系统", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_05")]
        public void StopAllParticleSystems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            int stoppedCount = 0;
            foreach (var obj in allObjects)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop();
                    stoppedCount++;
                }
            }

            // EditorUtility.DisplayDialog("成功", $"成功停止 {stoppedCount} 个粒子系统！", "确定");
        }

        [Button("批量清空粒子", ButtonHeight = 40), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
        public void ClearAllParticleSystems()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            int clearedCount = 0;
            foreach (var obj in allObjects)
            {
                var ps = obj.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Clear();
                    clearedCount++;
                }
            }

            // EditorUtility.DisplayDialog("成功", $"成功清空 {clearedCount} 个粒子系统！", "确定");
        }
    }
    #endregion

}