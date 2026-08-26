#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ES
{
    [CustomEditor(typeof(ESWorldDialogueAnchor))]
    public sealed class ESWorldDialogueAnchorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("这是 Scene 中的 3D 对话入口投影。正式内容仍由对话图资产和地图放置记录共同决定。", MessageType.Info);
            SerializedProperty placementId = serializedObject.FindProperty("placementId");
            SerializedProperty dialogueGraphKey = serializedObject.FindProperty("dialogueGraphKey");
            SerializedProperty dialogueGraphAssetGuid = serializedObject.FindProperty("dialogueGraphAssetGuid");
            SerializedProperty entryNodeId = serializedObject.FindProperty("entryNodeId");
            SerializedProperty mapAssetGuid = serializedObject.FindProperty("mapAssetGuid");
            SerializedProperty sceneObjectKey = serializedObject.FindProperty("sceneObjectKey");
            SerializedProperty placementSpace = serializedObject.FindProperty("placementSpace");
            if (placementId == null || dialogueGraphKey == null || dialogueGraphAssetGuid == null
                || entryNodeId == null || mapAssetGuid == null || sceneObjectKey == null
                || placementSpace == null)
            {
                EditorGUILayout.HelpBox("对话锚点序列化结构不完整，已停止编辑器绘制。", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(placementId, new GUIContent("Placement ID"));
                EditorGUILayout.PropertyField(dialogueGraphKey, new GUIContent("对话图 Key"));
                EditorGUILayout.PropertyField(dialogueGraphAssetGuid, new GUIContent("对话图 GUID"));
                EditorGUILayout.PropertyField(entryNodeId, new GUIContent("入口节点"));
                EditorGUILayout.PropertyField(mapAssetGuid, new GUIContent("地图 GUID"));
                EditorGUILayout.PropertyField(sceneObjectKey, new GUIContent("Scene 对象 Key"));
                EditorGUILayout.PropertyField(placementSpace, new GUIContent("空间模式"));
            }
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("打开对话编辑器", GUILayout.Height(28f)))
            {
                ESWorldDialogueAnchor anchor = (ESWorldDialogueAnchor)target;
                string path = AssetDatabase.GUIDToAssetPath(anchor.mapAssetGuid);
                ESWorldMapAsset map = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(path);
                string graphPath = AssetDatabase.GUIDToAssetPath(anchor.dialogueGraphAssetGuid);
                ESWorldDialogueGraphAsset graph = string.IsNullOrEmpty(graphPath) ? null : AssetDatabase.LoadAssetAtPath<ESWorldDialogueGraphAsset>(graphPath);
                if (graph != null) ESWorldDialogueEditorWindow.OpenFor(graph, map);
                else ESWorldDialogueEditorWindow.OpenFor(map);
            }
        }
    }
}
#endif
