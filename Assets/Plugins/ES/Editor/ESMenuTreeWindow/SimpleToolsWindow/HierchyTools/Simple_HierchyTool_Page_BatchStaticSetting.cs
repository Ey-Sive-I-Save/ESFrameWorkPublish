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

// 抑制私有字段未使用警告
#pragma warning disable CS0414
// 抑制无法访问的代码警告（提前return）
#pragma warning disable CS0162

namespace ES
{

    #region 批量静态设置工具
    [Serializable]
    public class Page_BatchStaticSetting : ESWindowPageBase
    {
        [Title("批量静态设置工具", "批量设置GameObject的静态标记", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择GameObject，\n设置静态标记选项，\n点击应用按钮批量设置";

        [ShowInInspector, ReadOnly, DisplayAsString, HideLabel, PropertyOrder(-10)]
        private string PanelSummary
        {
            get
            {
                int selectedCount = Selection.gameObjects != null ? Selection.gameObjects.Length : 0;
                int targetCount = SimpleToolsSafetyUtility.CollectTargets(Selection.gameObjects, includeChildren).Count;
                return $"当前选择: {selectedCount} 个对象 | 实际目标: {targetCount} 个 | 包含子对象: {(includeChildren ? "是" : "否")}";
            }
        }

        [InfoBox("按当前开关覆盖目标对象的 Static Flags。常用组合：场景静态物体勾选批处理/遮挡/反射，参与烘焙的物体再勾选全局光照。", InfoMessageType.Info)]
        [LabelText("包含子对象"), Space(5)]
        public bool includeChildren = true;

        private static readonly Color EnabledColor = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color DisabledColor = new Color(0.8f, 0.8f, 0.8f);

        [Tooltip("参与全局光照和光照贴图烘焙。")]
        [LabelText("贡献全局光照"), GUIColor("@contributeGI ? EnabledColor : DisabledColor")]
        public bool contributeGI = false;

        [Tooltip("作为遮挡剔除系统中的静态遮挡物。")]
        [LabelText("遮挡剔除静态"), GUIColor("@occluderStatic ? EnabledColor : DisabledColor")]
        public bool occluderStatic = false;

        [Tooltip("允许 Unity 静态批处理减少渲染批次。")]
        [LabelText("批处理静态"), GUIColor("@batchingStatic ? EnabledColor : DisabledColor")]
        public bool batchingStatic = false;

        [Tooltip("兼容旧 Unity 导航静态标记。新项目通常由 NavMesh 工作流单独管理。")]
        [LabelText("导航静态(旧)"), GUIColor("@navigationStatic ? EnabledColor : DisabledColor")]
        public bool navigationStatic = false;

        [Tooltip("参与反射探针烘焙和静态反射采样。")]
        [LabelText("反射探针静态"), GUIColor("@reflectionProbeStatic ? EnabledColor : DisabledColor")]
        public bool reflectionProbeStatic = false;

        private string lastResultSummary = "";
        private string lastResultDetail = "";

        [OnInspectorGUI]
        private void DrawResultPanel()
        {
            SimpleToolsPanelUtility.DrawResultSummary("最近静态标记操作", lastResultSummary, lastResultDetail);
        }

        [Button("应用静态设置", ButtonHeight = 34), GUIColor(0.28f, 0.52f, 0.85f)]
        public void ApplyStaticSettings()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("需要选择对象", "先在层级窗口选中要处理的 GameObject。", "知道了");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            if (allObjects.Count == 0)
                return;

            string preview = SimpleToolsSafetyUtility.JoinPreview(allObjects.Select(obj => obj.name), 10);
            if (!EditorUtility.DisplayDialog("确认应用静态标记",
                $"将按当前开关设置 {allObjects.Count} 个对象的 Static Flags。\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始应用", "取消"))
                return;

            Undo.RecordObjects(allObjects.ToArray(), "Batch Static Setting");

            int changedCount = 0;
            var changedObjects = new List<GameObject>();
            foreach (var obj in allObjects)
            {
                StaticEditorFlags oldFlags = GameObjectUtility.GetStaticEditorFlags(obj);
                StaticEditorFlags flags = oldFlags;

                if (contributeGI)
                    flags |= StaticEditorFlags.ContributeGI;
                else
                    flags &= ~StaticEditorFlags.ContributeGI;

                if (occluderStatic)
                    flags |= StaticEditorFlags.OccluderStatic;
                else
                    flags &= ~StaticEditorFlags.OccluderStatic;

                if (batchingStatic)
                    flags |= StaticEditorFlags.BatchingStatic;
                else
                    flags &= ~StaticEditorFlags.BatchingStatic;

                // Unity 已将 NavigationStatic 标记为旧入口；这里保留开关用于兼容老场景静态标记。
#pragma warning disable CS0618
                if (navigationStatic)
                    flags |= StaticEditorFlags.NavigationStatic;
                else
                    flags &= ~StaticEditorFlags.NavigationStatic;
#pragma warning restore CS0618

                if (reflectionProbeStatic)
                    flags |= StaticEditorFlags.ReflectionProbeStatic;
                else
                    flags &= ~StaticEditorFlags.ReflectionProbeStatic;

                if (oldFlags != flags)
                {
                    GameObjectUtility.SetStaticEditorFlags(obj, flags);
                    EditorUtility.SetDirty(obj);
                    changedCount++;
                    changedObjects.Add(obj);
                }
            }

            MarkScenesDirty(changedObjects);
            lastResultSummary = $"应用完成: 检查 {allObjects.Count} 个对象 | 实际修改 {changedCount} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(changedObjects.Select(obj => obj.name), 10);
            EditorUtility.DisplayDialog("静态标记已应用", $"检查 {allObjects.Count} 个对象，实际修改 {changedCount} 个。", "完成");
        }

        [Button("重置静态标记设置", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
        public void ResetStaticSettings()
        {
            contributeGI = false;
            occluderStatic = false;
            batchingStatic = false;
            navigationStatic = false;
            reflectionProbeStatic = false;
        }

        [Button("清除所有静态标记", ButtonHeight = 30), GUIColor(0.82f, 0.38f, 0.30f)]
        public void ClearAllStaticFlags()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            if (allObjects.Count == 0)
                return;

            var targets = allObjects.Where(obj => GameObjectUtility.GetStaticEditorFlags(obj) != 0).ToList();
            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("无需清除", "当前选区没有带静态标记的对象。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            if (!EditorUtility.DisplayDialog("确认清除静态标记",
                $"将清除 {targets.Count} 个对象的所有 Static Flags。\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始清除", "取消"))
                return;

            Undo.RecordObjects(targets.ToArray(), "Clear Static Flags");

            int changedCount = 0;
            foreach (var obj in targets)
            {
                if (GameObjectUtility.GetStaticEditorFlags(obj) != 0)
                {
                    GameObjectUtility.SetStaticEditorFlags(obj, 0);
                    EditorUtility.SetDirty(obj);
                    changedCount++;
                }
            }

            MarkScenesDirty(targets);
            lastResultSummary = $"清除完成: 检查 {allObjects.Count} 个对象 | 实际清除 {changedCount} 个";
            lastResultDetail = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            EditorUtility.DisplayDialog("静态标记已清除", $"检查 {allObjects.Count} 个对象，实际清除 {changedCount} 个。", "完成");
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
