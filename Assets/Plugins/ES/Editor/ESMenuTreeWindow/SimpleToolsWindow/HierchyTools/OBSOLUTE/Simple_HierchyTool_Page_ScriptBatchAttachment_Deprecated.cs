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

    #region 脚本批量挂载工具
    [Serializable]
    [Obsolete("This tool is deprecated and will be removed in future versions.")]
    public class Page_ScriptBatchAttachment : ESWindowPageBase
    {
        [Title("脚本批量挂载工具", "批量为GameObject添加脚本组件", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 13), HideLabel, GUIColor(0.72f, 0.86f, 0.86f)]
        public string readMe = "选择GameObject，\n输入脚本类型名称，\n点击挂载按钮批量添加";

        [LabelText("包含子对象"), Space(5)] 
        public bool includeChildren = true;

        [LabelText("脚本类型名称（含命名空间）"), Space(5)]
        public string scriptTypeName = "";

        [LabelText("跳过已有相同组件的对象"), Space(5)]
        public bool skipExisting = true;

        [Button("挂载脚本", ButtonHeight = 34), GUIColor(0.75f, 0.58f, 0.25f)]
        public void AttachScripts()
        {
            if (string.IsNullOrEmpty(scriptTypeName))
            {
                EditorUtility.DisplayDialog("错误", "请输入脚本类型名称！", "确定");
                return;
            }

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("错误", "请先选择GameObject！", "确定");
                return;
            }

            // 查找类型
            Type scriptType = null;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                scriptType = assembly.GetType(scriptTypeName);
                if (scriptType != null) break;
            }

            if (scriptType == null || !typeof(Component).IsAssignableFrom(scriptType))
            {
                EditorUtility.DisplayDialog("错误", $"未找到类型 '{scriptTypeName}' 或它不是Component类型！", "确定");
                return;
            }

            var allObjects = SimpleToolsSafetyUtility.CollectTargets(selectedObjects, includeChildren);
            var targets = allObjects
                .Where(obj => obj != null && (!skipExisting || obj.GetComponent(scriptType) == null))
                .ToList();

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog("无需挂载", "当前选区没有需要添加该组件的对象。", "知道了");
                return;
            }

            string preview = SimpleToolsSafetyUtility.JoinPreview(targets.Select(obj => obj.name), 10);
            if (!EditorUtility.DisplayDialog("确认批量挂载脚本",
                $"将为 {targets.Count} 个对象添加组件：\n{scriptType.FullName}\n\n{preview}\n\n支持 Ctrl+Z 撤销。继续吗？",
                "开始挂载", "取消"))
                return;

            int addedCount = 0;
            foreach (var obj in targets)
            {
                var component = Undo.AddComponent(obj, scriptType);
                EditorUtility.SetDirty(component);
                addedCount++;
            }

            MarkScenesDirty(targets);
            EditorUtility.DisplayDialog("成功", $"成功为 {addedCount} 个对象添加脚本组件！", "确定");
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

        [LabelText("常用脚本快捷添加"), Space(10)]
        [InfoBox("点击下方按钮快速添加常用组件", InfoMessageType.Info)]
        public bool commonScriptsSection;

        [Button("添加Rigidbody", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void AddRigidbody()
        {
            scriptTypeName = "UnityEngine.Rigidbody";
            AttachScripts();
        }

        [Button("添加BoxCollider", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void AddBoxCollider()
        {
            scriptTypeName = "UnityEngine.BoxCollider";
            AttachScripts();
        }

        [Button("添加AudioSource", ButtonHeight = 30), GUIColor("@ESDesignUtility.ColorSelector.Color_04")]
        public void AddAudioSource()
        {
            scriptTypeName = "UnityEngine.AudioSource";
            AttachScripts();
        }
    }
    #endregion

}
