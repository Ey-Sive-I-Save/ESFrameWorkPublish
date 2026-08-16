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
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("placementId"), new GUIContent("Placement ID"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueGraphKey"), new GUIContent("对话图 Key"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dialogueGraphAssetGuid"), new GUIContent("对话图 GUID"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("entryNodeId"), new GUIContent("入口节点"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("mapAssetGuid"), new GUIContent("地图 GUID"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sceneObjectKey"), new GUIContent("Scene 对象 Key"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("placementSpace"), new GUIContent("空间模式"));
            }
            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("打开对话工作台", GUILayout.Height(28f)))
            {
                ESWorldDialogueAnchor anchor = (ESWorldDialogueAnchor)target;
                string path = AssetDatabase.GUIDToAssetPath(anchor.mapAssetGuid);
                ESWorldMapAsset map = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<ESWorldMapAsset>(path);
                string graphPath = AssetDatabase.GUIDToAssetPath(anchor.dialogueGraphAssetGuid);
                ESWorldDialogueGraphAsset graph = string.IsNullOrEmpty(graphPath) ? null : AssetDatabase.LoadAssetAtPath<ESWorldDialogueGraphAsset>(graphPath);
                if (graph != null) ESWorldDialogueWorkbenchWindow.OpenFor(graph, map);
                else ESWorldDialogueWorkbenchWindow.OpenFor(map);
            }
        }
    }
}
#endif
