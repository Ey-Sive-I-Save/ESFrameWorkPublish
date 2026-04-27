using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Gaskellgames;
using Sirenix.OdinInspector.Editor;

namespace ES.EditorTools
{
    [CustomEditor(typeof(Core), true)]
    public class CorePreviewEditor : OdinEditor
    {
        
        public override bool HasPreviewGUI()
        {
            return true;
        }

        // 独占区块收集
        private static List<object> singleAreaList = new List<object>();
        private static bool foldoutSingleArea = false;

        // 折叠状态缓存
        private static bool foldoutDomain = true;
        private static bool foldoutModules = true;

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var core = target as Core;
            if (core == null)
            {
                EditorGUI.LabelField(r, "未找到 Core 实例");
                return;
            }

            singleAreaList.Clear();

            int offset = 20;
            var areaRect = new Rect(r.x, r.y + offset, r.width, r.height - offset);
            GUILayout.BeginArea(areaRect);
            GUILayout.Label($"【Core 运行时预览】", EditorStyles.boldLabel);
            GUILayout.Label($"类型: {core.GetType().Name}");

            // 通用模板：Play/非Play分离
            if (Application.isPlaying)
            {
                // Domain 区域
                foldoutDomain = EditorGUILayout.Foldout(foldoutDomain, "Domain_ 预览", true);
                if (foldoutDomain)
                {
                    var domainField = core.GetType().GetField("stateDomain", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var domain = domainField?.GetValue(core);
                    DrawPreviewIfSupported(domain, "Domain_");
                }

                // Modules 区域
                foldoutModules = EditorGUILayout.Foldout(foldoutModules, "Modules_ 预览", true);
                if (foldoutModules)
                {
                    var moduleTablesField = core.GetType().GetField("ModuleTables", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var moduleTables = moduleTablesField?.GetValue(core) as System.Collections.IDictionary;
                    if (moduleTables != null)
                    {
                        foreach (System.Collections.DictionaryEntry entry in moduleTables)
                        {
                            var module = entry.Value;
                            DrawPreviewIfSupported(module, module?.GetType().Name + "_");
                        }
                    }
                    else
                    {
                        GUILayout.Label("无 ModuleTables");
                    }
                }
            }
            else
            {
                // 非Play模式也支持独立的NonPlay预览
                // Domain 区域
                foldoutDomain = EditorGUILayout.Foldout(foldoutDomain, "Domain_ 预览", true);
                if (foldoutDomain)
                {
                    var domainField = core.GetType().GetField("stateDomain", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var domain = domainField?.GetValue(core);
                    DrawPreviewIfSupported(domain, "Domain_");
                }

                // Modules 区域
                foldoutModules = EditorGUILayout.Foldout(foldoutModules, "Modules_ 预览", true);
                if (foldoutModules)
                {
                    var moduleTablesField = core.GetType().GetField("ModuleTables", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var moduleTables = moduleTablesField?.GetValue(core) as System.Collections.IDictionary;
                    if (moduleTables != null)
                    {
                        foreach (System.Collections.DictionaryEntry entry in moduleTables)
                        {
                            var module = entry.Value;
                            DrawPreviewIfSupported(module, module?.GetType().Name + "_");
                        }
                    }
                    else
                    {
                        GUILayout.Label("无 ModuleTables");
                    }
                }
            }


            // 独占区块统一绘制
            if (singleAreaList.Count > 0)
            {
                GUILayout.Space(8);
                foldoutSingleArea = EditorGUILayout.Foldout(foldoutSingleArea, "SingleArea_ 独占区块", false);
                if (foldoutSingleArea)
                {
                    foreach (var obj in singleAreaList)
                    {
                        var type = obj.GetType();
                        bool isPlaying = Application.isPlaying;
                        string drawMethodName = isPlaying ? "EditorPreviewDrawPreviewGUI" : "EditorPreviewDrawPreviewGUINonPlay";
                        var method = type.GetMethod(drawMethodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        GUILayout.BeginVertical("box");
                        GUILayout.Label(type.Name + "_", EditorStyles.boldLabel);
                        if (method != null)
                        {
                            method.Invoke(obj, null);
                        }
                        else
                        {
                            GUILayout.Label($"{type.Name}_（未实现 {drawMethodName}）");
                        }
                        GUILayout.EndVertical();
                    }
                }
            }

            GUILayout.EndArea();
        }

        // 通用模板：自动调用 EditorPreview 前缀扩展方法
        private void DrawPreviewIfSupported(object obj, string label)
        {
            if (obj == null) return;
            var type = obj.GetType();
            // Play/非Play分离
            bool isPlaying = Application.isPlaying;
            string canPreviewName = isPlaying ? "EditorPreviewCanPreview" : "EditorPreviewCanPreviewNonPlay";
            string drawMethodName = isPlaying ? "EditorPreviewDrawPreviewGUI" : "EditorPreviewDrawPreviewGUINonPlay";

            // 检查 EditorPreviewCanPreview
            var canPreviewProp = type.GetProperty(canPreviewName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            bool canPreview = true;
            if (canPreviewProp != null)
            {
                canPreview = (bool)canPreviewProp.GetValue(obj);
            }
            if (!canPreview) return;

            // 检查是否有独占区块
            var singleAreaProp = type.GetProperty("EditorPreviewIsSingleArea", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            bool isSingleArea = false;
            if (singleAreaProp != null)
            {
                isSingleArea = (bool)singleAreaProp.GetValue(obj);
            }
            if (isSingleArea)
            {
                if (!singleAreaList.Contains(obj))
                    singleAreaList.Add(obj);
                return; // 独占区块不在此处绘制
            }

            // 查找 EditorPreviewDrawPreviewGUI
            var method = type.GetMethod(drawMethodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            GUILayout.BeginVertical("box");
            GUILayout.Label(label, EditorStyles.boldLabel);
            if (method != null)
            {
                method.Invoke(obj, null);
            }
            else
            {
                GUILayout.Label($"{label}（未实现 {drawMethodName}）");
            }
            GUILayout.EndVertical();
        }


        public override void OnPreviewSettings()
        {
            // 可选：添加预览栏右上角设置
        }
    }
}
