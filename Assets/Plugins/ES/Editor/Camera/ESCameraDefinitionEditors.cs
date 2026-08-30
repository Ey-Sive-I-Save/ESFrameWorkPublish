using UnityEditor;
using UnityEngine;
using Cinemachine;

namespace ES.Editor
{
    [CustomEditor(typeof(ESCameraViewDefinition))]
    internal sealed class ESCameraViewDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            RecordUndoOnEditGesture("编辑相机 ViewDefinition");
            serializedObject.Update();
            EditorGUILayout.HelpBox("策划只配置镜头差异。输入、避障和安全规则由全局相机策略统一维护。", MessageType.Info);
            Draw("definition", "稳定镜头身份");
            Draw("rigKey", "Rig Key");
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("构图差异", EditorStyles.boldLabel);
            Draw("baseFieldOfView", "视场角");
            Draw("baseDistanceScale", "距离倍率");
            Draw("baseShoulderOffset", "肩部偏移");
            Draw("baseShakeAmplitude", "基础震动");
            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string propertyName, string label, bool includeChildren = true)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }

        private void RecordUndoOnEditGesture(string label)
        {
            if (GUI.enabled && Event.current != null && Event.current.type == EventType.MouseDown && targets != null && targets.Length > 0)
                Undo.RecordObjects(targets, label);
        }
    }

    [CustomEditor(typeof(ESCameraGlobalPolicy))]
    internal sealed class ESCameraGlobalPolicyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            RecordUndoOnEditGesture("编辑全局相机策略");
            serializedObject.Update();
            EditorGUILayout.HelpBox("全游戏唯一相机基础策略。修改前请运行相机内容验证；ViewDefinition 不应重复这些字段。", MessageType.Info);

            EditorGUILayout.LabelField("全局输入", EditorStyles.boldLabel);
            Draw("povLookSensitivity", "POV 灵敏度");
            Draw("freeLookSensitivity", "FreeLook 灵敏度");
            Draw("pointerLookScale", "指针缩放");
            Draw("maxPovLookRate", "POV 最大转速");
            Draw("maxFreeLookRate", "FreeLook 最大转速");
            Draw("invertVerticalLook", "垂直反转");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("全局避障", EditorStyles.boldLabel);
            Draw("enableObstruction", "启用避障");
            using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("enableObstruction").boolValue))
            {
                Draw("obstructionMask", "遮挡层");
                Draw("obstructionCameraRadius", "探针半径");
                Draw("obstructionMinimumDistance", "最小距离");
                Draw("obstructionMaximumEffort", "最大查询次数");
                Draw("obstructionDamping", "普通阻尼");
                Draw("obstructionDampingWhenOccluded", "遮挡阻尼");
            }

            if (!((ESCameraGlobalPolicy)target).TryValidate(out string policyError))
                EditorGUILayout.HelpBox("当前全局策略无效：" + policyError, MessageType.Error);

            if (GUILayout.Button("恢复商业默认策略")
                && EditorUtility.DisplayDialog("恢复全局相机策略", "将覆盖当前全局输入和避障参数，是否继续？", "恢复", "取消"))
            {
                Undo.RecordObjects(targets, "恢复全局相机策略");
                foreach (Object item in targets)
                {
                    ESCameraGlobalPolicy policy = item as ESCameraGlobalPolicy;
                    if (policy == null)
                        continue;

                    policy.ResetToCommercialDefaults();
                    EditorUtility.SetDirty(policy);
                }
                serializedObject.Update();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void Draw(string propertyName, string label)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, new GUIContent(label));
        }

        private void RecordUndoOnEditGesture(string label)
        {
            if (GUI.enabled && Event.current != null && Event.current.type == EventType.MouseDown && targets != null && targets.Length > 0)
                Undo.RecordObjects(targets, label);
        }
    }

    [CustomEditor(typeof(ESCameraRigCatalog))]
    internal sealed class ESCameraRigCatalogEditor : UnityEditor.Editor
    {
        private bool showAdvanced;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("美术只维护 Rig Key 与 Prefab。相机组件数量和基础结构由 Catalog 合同统一验证。", MessageType.Info);

            SerializedProperty entries = serializedObject.FindProperty("entries");
            if (entries != null)
                EditorGUILayout.PropertyField(entries, new GUIContent("Rig 列表"), true);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("验证 Rig 组件合同"))
            {
                ESCameraRigCatalog catalog = (ESCameraRigCatalog)target;
                if (catalog.IsValid)
                    Debug.Log("[ESCamera] Rig Catalog 验证通过：" + catalog.EntryCount + " 个 Rig。", catalog);
                else
                    Debug.LogError("[ESCamera] Rig Catalog 验证失败：" + catalog.BuildError, catalog);
                ReportRigDetails(catalog, entries);
            }

            ESCameraRigCatalog current = (ESCameraRigCatalog)target;
            if (!current.IsValid)
                EditorGUILayout.HelpBox(current.BuildError ?? "Rig Catalog 无效。", MessageType.Error);

            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "高级调试信息");
            if (showAdvanced)
            {
                EditorGUILayout.LabelField("条目数", current.EntryCount.ToString());
                EditorGUILayout.LabelField("状态", current.IsValid ? "Valid" : "Invalid");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void ReportRigDetails(ESCameraRigCatalog catalog, SerializedProperty entries)
        {
            if (entries == null)
                return;

            int errors = 0;
            int warnings = 0;
            int infos = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("rigKey");
                SerializedProperty prefab = entry.FindPropertyRelative("rigPrefab");
                string rigKey = key != null ? key.stringValue : string.Empty;
                GameObject rigPrefab = prefab != null ? prefab.objectReferenceValue as GameObject : null;
                string label = string.IsNullOrWhiteSpace(rigKey) ? "<empty>" : rigKey;

                if (!catalog.TryValidateEntry(i, out ESCameraRigValidationSeverity severity, out string message))
                {
                    errors++;
                    Debug.LogError("[ESCamera][Error] Rig '" + label + "'：" + message + " 修复：补齐 RigKey、Prefab 和根节点 VCam。", rigPrefab != null ? rigPrefab : catalog);
                    continue;
                }

                if (severity == ESCameraRigValidationSeverity.Warning)
                {
                    warnings++;
                    Debug.LogWarning("[ESCamera][Warning] Rig '" + label + "'：" + message + " 修复：添加 CameraOffset。", rigPrefab);
                }
                else
                {
                    infos++;
                    Debug.Log("[ESCamera][Info] Rig '" + label + "'：" + message, rigPrefab);
                }
            }

            Debug.Log("[ESCamera] Rig 诊断汇总：Error=" + errors + ", Warning=" + warnings + ", Info=" + infos + "."
                + (errors > 0 ? " 必须先修复 Error。" : warnings > 0 ? " 可在进入运行时前处理 Warning。" : " 所有条目通过基础结构检查。"), entries.serializedObject.targetObject);
        }
    }
}
