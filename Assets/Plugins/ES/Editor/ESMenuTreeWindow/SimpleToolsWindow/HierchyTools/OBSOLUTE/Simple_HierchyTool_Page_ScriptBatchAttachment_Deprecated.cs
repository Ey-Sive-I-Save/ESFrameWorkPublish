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

    #region 脚本批量挂载工具
    [Serializable]
    [Obsolete("This tool is deprecated and will be removed in future versions.")]
    public class Page_ScriptBatchAttachment : ESWindowPageBase
    {
        [Title("脚本批量挂载工具", "批量为GameObject添加脚本组件", bold: true, titleAlignment: TitleAlignments.Centered)]

        [DisplayAsString(fontSize: 30), HideLabel, GUIColor("@ESDesignUtility.ColorSelector.Color_01")]
        public string readMe = "选择GameObject，\n输入脚本类型名称，\n点击挂载按钮批量添加";

        [LabelText("包含子对象"), Space(5)] 
        public bool includeChildren = true;

        [LabelText("脚本类型名称（含命名空间）"), Space(5)]
        public string scriptTypeName = "";

        [LabelText("跳过已有相同组件的对象"), Space(5)]
        public bool skipExisting = true;

        [Button("挂载脚本", ButtonHeight = 50), GUIColor("@ESDesignUtility.ColorSelector.Color_03")]
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

            var allObjects = new List<GameObject>();
            foreach (var obj in selectedObjects)
            {
                allObjects.Add(obj);
                if (includeChildren)
                {
                    allObjects.AddRange(obj.GetComponentsInChildren<Transform>().Select(t => t.gameObject));
                }
            }

            int addedCount = 0;
            foreach (var obj in allObjects)
            {
                if (skipExisting && obj.GetComponent(scriptType) != null)
                {
                    continue;
                }

                Undo.AddComponent(obj, scriptType);
                addedCount++;
            }

            EditorUtility.DisplayDialog("成功", $"成功为 {addedCount} 个对象添加脚本组件！", "确定");
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