#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ES
{
    [CustomEditor(typeof(ESWorldDialogueGraphAsset))]
    public sealed class ESWorldDialogueGraphAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty definitionProperty;

        private void OnEnable()
        {
            definitionProperty = serializedObject.FindProperty("definition");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("对话图资产是节点、端口和边的唯一权威。窗口、Scene 锚点和预览都只能引用稳定 ID。", MessageType.Info);
            if (definitionProperty != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(definitionProperty.FindPropertyRelative("graphId"), new GUIContent("Graph ID"));
                    EditorGUILayout.PropertyField(definitionProperty.FindPropertyRelative("schemaVersion"), new GUIContent("Schema"));
                    EditorGUILayout.PropertyField(definitionProperty.FindPropertyRelative("contentVersion"), new GUIContent("内容版本"));
                    EditorGUILayout.PropertyField(definitionProperty.FindPropertyRelative("contentHash"), new GUIContent("内容 Hash"));
                    EditorGUILayout.PropertyField(definitionProperty.FindPropertyRelative("entryNodeId"), new GUIContent("入口节点"));
                }
                SerializedProperty nodes = definitionProperty.FindPropertyRelative("nodes");
                SerializedProperty edges = definitionProperty.FindPropertyRelative("edges");
                EditorGUILayout.LabelField("节点 / 数据流", nodes.arraySize + " / " + edges.arraySize);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("打开对话工作台", GUILayout.Height(26f))) ESWorldDialogueWorkbenchWindow.OpenFor((ESWorldDialogueGraphAsset)target);
                if (GUILayout.Button("验证", GUILayout.Height(26f)))
                {
                    ESWorldDialogueGraphAsset asset = (ESWorldDialogueGraphAsset)target;
                    if (asset.Validate(out string error)) Debug.Log("[ES] 对话图验证通过：" + asset.name, asset);
                    else Debug.LogError("[ES] 对话图验证失败：" + error, asset);
                }
                if (GUILayout.Button("保存", GUILayout.Height(26f)))
                {
                    ESWorldDialogueSaveResult result = ESWorldDialogueAuthoringUtility.Save((ESWorldDialogueGraphAsset)target, serializedObject);
                    if (!result.success) Debug.LogError("[ES] 对话图保存失败：" + result.error, target);
                    else Debug.Log("[ES] 对话图已保存，内容版本 " + result.contentVersion + "。", target);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
