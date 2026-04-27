using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;
using ES;
using System;

namespace ES.EditorTools
{
    [CustomEditor(typeof(Core), true)]
    public class CorePreviewEditor : OdinEditor
    {
        public override bool HasPreviewGUI() => true;

        private static readonly List<ICorePreviewProvider> singleAreaList = new List<ICorePreviewProvider>();
        private static bool foldoutSingleArea = true;
        private static bool foldoutDomain = false;
        private static bool foldoutModules = false;

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var core = target as Core;
            if (core == null)
            {
                EditorGUI.LabelField(r, "未找到 Core 实例");
                return;
            }

            singleAreaList.Clear();
            CollectSingleAreaObjects(core);

            int headerHeight = 30;
            int margin = 8;
            var areaRect = new Rect(r.x + margin, r.y + headerHeight, r.width - margin * 2, r.height - headerHeight);
            GUILayout.BeginArea(areaRect);

            DrawHeader(core);

            // 1. Domain 预览：遍历 Domains 列表
            DrawDomainsSection("Domain_ 预览", ref foldoutDomain, core.Domains);

            // 2. Modules 预览
            DrawModulesSection("Modules_ 预览", ref foldoutModules, core.ModuleTables);

            // 3. 独占区块
            if (singleAreaList.Count > 0)
            {
                GUILayout.Space(10);
                DrawSingleAreaSection();
            }

            GUILayout.EndArea();
        }

        private void CollectSingleAreaObjects(Core core)
        {
            // 检查 Domains 列表
            if (core.Domains != null)
            {
                foreach (var domain in core.Domains)
                {
                    if (domain is ICorePreviewProvider provider && provider.EditorPreviewIsSingleArea)
                        singleAreaList.Add(provider);
                }
            }

            // 检查 ModuleTables
            if (core.ModuleTables != null)
            {
                foreach (var module in core.ModuleTables.Values)
                {
                    if (module is ICorePreviewProvider provider && provider.EditorPreviewIsSingleArea)
                        singleAreaList.Add(provider);
                }
            }
        }

        private void DrawHeader(Core core)
        {
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"Core 运行时预览 | {core.GetType().Name}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var stateColor = Application.isPlaying ? new Color(0.3f, 1f, 0.3f) : new Color(0.7f, 0.7f, 0.7f);
            var style = new GUIStyle(GUI.skin.label) { normal = { textColor = stateColor }, fontStyle = FontStyle.Bold };
            GUILayout.Label(Application.isPlaying ? "● PLAY" : "○ STOP", style);
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        // 绘制 Domains 列表（可折叠）
        private void DrawDomainsSection(string title, ref bool foldout, IReadOnlyList<IDomain> domains)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            foldout = EditorGUILayout.Foldout(foldout, title, true);
            if (foldout)
            {
                GUILayout.Space(4);
                if (domains == null || domains.Count == 0)
                {
                    EditorGUILayout.HelpBox("暂无数据", MessageType.Info);
                }
                else
                {
                    foreach (var domain in domains)
                    {
                        DrawPreviewIfSupported(domain, domain?.GetType().Name ?? "Unknown");
                    }
                }
            }
            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        // 绘制 Modules 字典
        private void DrawModulesSection(string title, ref bool foldout, IDictionary<Type, IModule> modules)
        {
            GUILayout.BeginVertical(EditorStyles.helpBox);
            foldout = EditorGUILayout.Foldout(foldout, title, true);
            if (foldout)
            {
                GUILayout.Space(4);
                if (modules == null || modules.Count == 0)
                {
                    EditorGUILayout.HelpBox("暂无数据", MessageType.Info);
                }
                else
                {
                    foreach (var kv in modules)
                    {
                        DrawPreviewIfSupported(kv.Value, kv.Key.Name); // Type 类型转 string
                    }
                }
            }
            GUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private void DrawSingleAreaSection()
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 1f, 0.8f);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = originalColor;

            GUILayout.Label("★ 独占区块", EditorStyles.boldLabel);

            foreach (var provider in singleAreaList)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(provider.GetType().Name, EditorStyles.boldLabel);
                if (Application.isPlaying)
                    provider.EditorPreviewDrawPreviewGUI();
                else
                    provider.EditorPreviewDrawPreviewGUINonPlay();
                GUILayout.EndVertical();
                GUILayout.Space(4);
            }
            GUILayout.EndVertical();
        }

        private void DrawPreviewIfSupported(object obj, string label)
        {
            if (obj is not ICorePreviewProvider provider) return;
            if (!provider.EditorPreviewCanPreview) return;
            if (provider.EditorPreviewIsSingleArea) return;

            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(label, EditorStyles.boldLabel);
            if (Application.isPlaying)
                provider.EditorPreviewDrawPreviewGUI();
            else
                provider.EditorPreviewDrawPreviewGUINonPlay();
            GUILayout.EndVertical();
        }

        public override void OnPreviewSettings() { }
    }
}