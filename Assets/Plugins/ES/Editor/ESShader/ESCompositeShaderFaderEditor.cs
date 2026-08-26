using System;
using System.Collections.Generic;
using ES;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ES.EditorInternal
{
    [CustomEditor(typeof(ESCompositeShaderFader)), CanEditMultipleObjects]
    public sealed class ESCompositeShaderFaderEditor : UnityEditor.Editor
    {
        private const int MaxVisibleDiagnostics = 6;

        private Material fromMaterial;
        private Material toMaterial;
        private readonly List<string> diagnostics = new List<string>();
        private readonly HashSet<string> diagnosticMessages = new HashSet<string>(StringComparer.Ordinal);
        private bool diagnosticsDirty = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            if (serializedObject.ApplyModifiedProperties())
                diagnosticsDirty = true;

            EditorGUILayout.Space();
            DrawMaterialCopyPanel();
            DrawTargetActions();
            DrawDiagnostics();
        }

        private void DrawMaterialCopyPanel()
        {
            EditorGUILayout.LabelField("材质端点", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "从材质读取只更新同名 Float、Range、Vector 或 Color 轨道，不复制纹理与材质渲染状态。",
                MessageType.Info);

            fromMaterial = (Material)EditorGUILayout.ObjectField("起点材质", fromMaterial, typeof(Material), false);
            using (new EditorGUI.DisabledScope(fromMaterial == null))
            {
                if (GUILayout.Button("从材质读取起点"))
                    CopyEndpointToSelection(fromMaterial, true);
            }

            toMaterial = (Material)EditorGUILayout.ObjectField("终点材质", toMaterial, typeof(Material), false);
            using (new EditorGUI.DisabledScope(toMaterial == null))
            {
                if (GUILayout.Button("从材质读取终点"))
                    CopyEndpointToSelection(toMaterial, false);
            }
        }

        private void DrawTargetActions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("目标", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUIContent refresh = EditorGUIUtility.IconContent("d_Refresh");
            refresh.tooltip = "重新收集 Renderer 与 Graphic";
            if (GUILayout.Button(refresh, GUILayout.Width(32f), GUILayout.Height(22f)))
            {
                Undo.RecordObjects(targets, "刷新 ES Shader Fader 目标");
                for (int i = 0; i < targets.Length; i++)
                {
                    var fader = targets[i] as ESCompositeShaderFader;
                    if (fader == null) continue;
                    PrepareGraphicInstancesWithUndo(fader);
                    fader.RefreshTargets();
                    EditorUtility.SetDirty(fader);
                }
                diagnosticsDirty = true;
            }
            if (GUILayout.Button("应用当前进度"))
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    var fader = targets[i] as ESCompositeShaderFader;
                    if (fader != null) fader.Apply();
                }
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("刷新诊断"))
                diagnosticsDirty = true;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDiagnostics()
        {
            if (diagnosticsDirty)
                RefreshDiagnostics();
            if (diagnostics.Count == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("轨道诊断", EditorStyles.boldLabel);
            int visibleCount = Mathf.Min(diagnostics.Count, MaxVisibleDiagnostics);
            for (int i = 0; i < visibleCount; i++)
                EditorGUILayout.HelpBox(diagnostics[i], MessageType.Warning);
            if (diagnostics.Count > visibleCount)
                EditorGUILayout.HelpBox("另有 " + (diagnostics.Count - visibleCount) + " 项未显示。", MessageType.Warning);
        }

        private void CopyEndpointToSelection(Material material, bool copyFrom)
        {
            if (material == null)
                return;

            Undo.RecordObjects(targets, copyFrom ? "读取 ES Shader Fader 起点" : "读取 ES Shader Fader 终点");
            int copied = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                var fader = targets[i] as ESCompositeShaderFader;
                if (fader == null) continue;
                copied += CopyMaterialToTracks(fader, material, copyFrom);
                EditorUtility.SetDirty(fader);
            }

            serializedObject.Update();
            diagnosticsDirty = true;
            ShowNotification(copied > 0
                ? "已读取 " + copied + " 条轨道。"
                : "没有可读取的同名数值轨道。");
        }

        public static int CopyMaterialToTracks(
            ESCompositeShaderFader fader,
            Material material,
            bool copyFrom)
        {
            if (fader == null || material == null || material.shader == null)
                return 0;

            using (var serializedFader = new SerializedObject(fader))
            {
                serializedFader.Update();
                SerializedProperty tracks = serializedFader.FindProperty("tracks");
                if (tracks == null)
                    return 0;
                int copied = 0;
                for (int i = 0; i < tracks.arraySize; i++)
                {
                    SerializedProperty track = tracks.GetArrayElementAtIndex(i);
                    SerializedProperty propertyName = track.FindPropertyRelative("propertyName");
                    int shaderPropertyIndex = material.shader.FindPropertyIndex(propertyName.stringValue);
                    if (shaderPropertyIndex < 0)
                        continue;

                    ShaderPropertyType propertyType = material.shader.GetPropertyType(shaderPropertyIndex);
                    SerializedProperty valueType = track.FindPropertyRelative("valueType");
                    string endpointPrefix = copyFrom ? "from" : "to";
                    switch (propertyType)
                    {
                        case ShaderPropertyType.Color:
                            valueType.enumValueIndex = (int)ESCompositeShaderFadeValueType.Color;
                            track.FindPropertyRelative(endpointPrefix + "Color").colorValue =
                                material.GetColor(propertyName.stringValue);
                            copied++;
                            break;
                        case ShaderPropertyType.Vector:
                            valueType.enumValueIndex = (int)ESCompositeShaderFadeValueType.Vector;
                            track.FindPropertyRelative(endpointPrefix + "Vector").vector4Value =
                                material.GetVector(propertyName.stringValue);
                            copied++;
                            break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            valueType.enumValueIndex = (int)ESCompositeShaderFadeValueType.Float;
                            track.FindPropertyRelative(endpointPrefix + "Float").floatValue =
                                material.GetFloat(propertyName.stringValue);
                            copied++;
                            break;
                    }
                }
                serializedFader.ApplyModifiedProperties();
                return copied;
            }
        }

        private static void PrepareGraphicInstancesWithUndo(ESCompositeShaderFader fader)
        {
            using (var serializedFader = new SerializedObject(fader))
            {
                serializedFader.Update();
                var graphics = new List<Graphic>();
                SerializedProperty collectChildren = serializedFader.FindProperty("collectChildren");
                if (collectChildren == null)
                    return;
                if (collectChildren.boolValue)
                {
                    SerializedProperty includeInactiveProperty = serializedFader.FindProperty("includeInactive");
                    bool includeInactive = includeInactiveProperty != null && includeInactiveProperty.boolValue;
                    graphics.AddRange(fader.GetComponentsInChildren<Graphic>(includeInactive));
                }
                else
                {
                    SerializedProperty graphicProperties = serializedFader.FindProperty("graphics");
                    if (graphicProperties == null)
                        return;
                    for (int i = 0; i < graphicProperties.arraySize; i++)
                    {
                        var graphic = graphicProperties.GetArrayElementAtIndex(i).objectReferenceValue as Graphic;
                        if (graphic != null) graphics.Add(graphic);
                    }
                }

                for (int i = 0; i < graphics.Count; i++)
                {
                    Graphic graphic = graphics[i];
                    if (!ESCompositeMaterialInstance.IsCompositeMaterial(graphic.material))
                        continue;
                    ESCompositeMaterialInstance instance = graphic.GetComponent<ESCompositeMaterialInstance>();
                    if (instance == null)
                        instance = Undo.AddComponent<ESCompositeMaterialInstance>(graphic.gameObject);
                    else
                        Undo.RecordObject(instance, "配置 ES Composite Material Instance");
                    instance.Configure(graphic);
                    EditorUtility.SetDirty(instance);
                }
            }
        }

        private void RefreshDiagnostics()
        {
            diagnostics.Clear();
            diagnosticMessages.Clear();
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                var fader = targets[targetIndex] as ESCompositeShaderFader;
                if (fader == null) continue;
                CollectDiagnostics(fader, diagnosticMessages, diagnostics);
            }
            diagnosticsDirty = false;
        }

        private static void CollectDiagnostics(
            ESCompositeShaderFader fader,
            HashSet<string> unique,
            List<string> result)
        {
            using (var serializedFader = new SerializedObject(fader))
            {
                serializedFader.Update();
                SerializedProperty tracks = serializedFader.FindProperty("tracks");
                if (tracks == null)
                    return;
                List<Material> materials = CollectTargetMaterials(serializedFader);

                for (int trackIndex = 0; trackIndex < tracks.arraySize; trackIndex++)
                {
                    SerializedProperty track = tracks.GetArrayElementAtIndex(trackIndex);
                    string propertyName = track.FindPropertyRelative("propertyName").stringValue;
                    if (string.IsNullOrWhiteSpace(propertyName))
                    {
                        AddDiagnostic("轨道 " + (trackIndex + 1) + " 没有属性名。", unique, result);
                        continue;
                    }

                    int valueType = track.FindPropertyRelative("valueType").enumValueIndex;
                    for (int materialIndex = 0; materialIndex < materials.Count; materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        int propertyIndex = material.shader.FindPropertyIndex(propertyName);
                        if (propertyIndex < 0)
                        {
                            AddDiagnostic(material.name + " 缺少轨道属性 " + propertyName + "。", unique, result);
                            continue;
                        }

                        ShaderPropertyType shaderType = material.shader.GetPropertyType(propertyIndex);
                        if (!MatchesTrackType(shaderType, valueType))
                            AddDiagnostic(material.name + " 的 " + propertyName + " 类型与轨道不一致。", unique, result);
                    }
                }
            }
        }

        private static List<Material> CollectTargetMaterials(SerializedObject serializedFader)
        {
            var result = new List<Material>();
            var unique = new HashSet<Material>();
            SerializedProperty renderers = serializedFader.FindProperty("renderers");
            if (renderers != null)
            {
                for (int i = 0; i < renderers.arraySize; i++)
                {
                    var renderer = renderers.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                    if (renderer == null) continue;
                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                        AddCompositeMaterial(materials[materialIndex], unique, result);
                }
            }

            SerializedProperty graphics = serializedFader.FindProperty("graphics");
            if (graphics != null)
            {
                for (int i = 0; i < graphics.arraySize; i++)
                {
                    var graphic = graphics.GetArrayElementAtIndex(i).objectReferenceValue as Graphic;
                    if (graphic != null) AddCompositeMaterial(graphic.material, unique, result);
                }
            }
            return result;
        }

        private static void AddCompositeMaterial(Material material, HashSet<Material> unique, List<Material> result)
        {
            if (!ESCompositeMaterialInstance.IsCompositeMaterial(material) || !unique.Add(material))
                return;
            result.Add(material);
        }

        private static bool MatchesTrackType(ShaderPropertyType shaderType, int trackType)
        {
            if (shaderType == ShaderPropertyType.Color)
                return trackType == (int)ESCompositeShaderFadeValueType.Color;
            if (shaderType == ShaderPropertyType.Vector)
                return trackType == (int)ESCompositeShaderFadeValueType.Vector;
            if (shaderType == ShaderPropertyType.Float || shaderType == ShaderPropertyType.Range)
                return trackType == (int)ESCompositeShaderFadeValueType.Float;
            return false;
        }

        private static void AddDiagnostic(string message, HashSet<string> unique, List<string> result)
        {
            if (unique.Add(message)) result.Add(message);
        }

        private void ShowNotification(string message)
        {
            if (EditorWindow.focusedWindow != null)
                EditorWindow.focusedWindow.ShowNotification(new GUIContent(message));
        }
    }
}
