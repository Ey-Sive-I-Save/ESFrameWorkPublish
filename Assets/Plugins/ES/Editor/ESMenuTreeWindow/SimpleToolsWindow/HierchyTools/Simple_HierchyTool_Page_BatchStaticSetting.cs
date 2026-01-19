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

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择GameObject，\n设置静态标记选项，\n点击应用按钮批量设置";

        [LabelText("包含子对象"), Space(5)]
        public bool includeChildren = true;

        private static readonly Color EnabledColor = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color DisabledColor = new Color(0.8f, 0.8f, 0.8f);

        [InfoBox("启用此选项后，该对象会影响场景中的全局光照计算，例如光线反射和间接光照。适合需要参与光照效果的静态物体，例如地面、墙壁等。依赖于光照贴图的生成。", InfoMessageType.Info)]
        [LabelText("【贡献全局光照】"), GUIColor("@contributeGI ? EnabledColor : DisabledColor")]
        public bool contributeGI = false;

        [InfoBox("启用此选项后，该对象会在遮挡剔除过程中被视为静态物体，从而减少渲染时的计算量，提升性能。适合用于大型静态物体，例如建筑物。依赖于遮挡剔除系统的设置。", InfoMessageType.Info)]
        [LabelText("【遮挡剔除静态】"), GUIColor("@occluderStatic ? EnabledColor : DisabledColor")]
        public bool occluderStatic = false;

        [InfoBox("启用此选项后，多个静态对象会被合并为一个批次进行渲染，从而减少渲染调用次数，提升性能。适合用于小型重复的静态物体，例如树木、石头等。依赖于静态批处理功能的开启。", InfoMessageType.Info)]
        [LabelText("【批处理静态】"), GUIColor("@batchingStatic ? EnabledColor : DisabledColor")]
        public bool batchingStatic = false;

        [InfoBox("启用此选项后，该对象会在导航网格生成时被视为静态障碍物，适合需要参与路径规划的物体，例如墙壁、障碍物等。依赖于导航网格系统的生成。", InfoMessageType.Info)]
        [LabelText("【导航静态】"), GUIColor("@navigationStatic ? EnabledColor : DisabledColor")]
        public bool navigationStatic = false;

        [InfoBox("启用此选项后，该对象会在反射探针的计算中被视为静态物体，从而提升反射效果的质量。适合需要高质量反射效果的物体，例如镜子、水面等。依赖于反射探针的设置。", InfoMessageType.Info)]
        [LabelText("【反射探针静态】"), GUIColor("@reflectionProbeStatic ? EnabledColor : DisabledColor")]
        public bool reflectionProbeStatic = false;

        [Button("应用静态设置", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
        public void ApplyStaticSettings()
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

            Undo.RecordObjects(allObjects.ToArray(), "Batch Static Setting");

            foreach (var obj in allObjects)
            {
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(obj);

                // 常规标志（直接使用枚举成员，现代Unity仍支持）
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

                if (reflectionProbeStatic)
                    flags |= StaticEditorFlags.ReflectionProbeStatic;
                else
                    flags &= ~StaticEditorFlags.ReflectionProbeStatic;

                GameObjectUtility.SetStaticEditorFlags(obj, flags);
            }

            EditorUtility.DisplayDialog("成功", $"成功设置 {allObjects.Count} 个对象的静态标记！", "确定");
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

        [Button("清除所有静态标记", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_02")]
        public void ClearAllStaticFlags()
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

            Undo.RecordObjects(allObjects.ToArray(), "Clear Static Flags");

            foreach (var obj in allObjects)
            {
                GameObjectUtility.SetStaticEditorFlags(obj, 0);
            }

            EditorUtility.DisplayDialog("成功", $"成功清除 {allObjects.Count} 个对象的静态标记！", "确定");
        }
    }
    #endregion

}