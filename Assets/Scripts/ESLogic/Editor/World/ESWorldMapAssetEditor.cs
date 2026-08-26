#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ES
{
    [CustomEditor(typeof(ESWorldMapAsset))]
    public sealed class ESWorldMapAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty definitionProperty;

        private void OnEnable()
        {
            definitionProperty = serializedObject.FindProperty("definition");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("地图资产是脱离 Scene 的权威定义。Scene、Prefab 或随机生成结果都只能作为内容来源，不直接成为运行时状态。", MessageType.Info);

            if (definitionProperty == null)
            {
                EditorGUILayout.HelpBox("地图序列化字段 definition 缺失，已停止编辑器绘制。", MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }
            EditorGUILayout.PropertyField(definitionProperty, new GUIContent("地图定义"), true);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("验证地图", GUILayout.Height(24f)))
                {
                    ESWorldMapAsset asset = (ESWorldMapAsset)target;
                    if (asset.Validate(out string error))
                        Debug.Log("[ES] 地图定义验证通过：" + asset.name, asset);
                    else
                        Debug.LogError("[ES] 地图定义验证失败：" + error, asset);
                }

                if (GUILayout.Button("填充默认空间模板", GUILayout.Height(24f)))
                {
                    SerializedProperty template = definitionProperty.FindPropertyRelative("spaceTemplate");
                    if (template == null)
                    {
                        Debug.LogError("[ES] 地图空间模板字段缺失，已取消填充。", target);
                        return;
                    }
                    Undo.RecordObject(target, "填充地图空间模板");
                    SerializedProperty templateId = template.FindPropertyRelative("templateId");
                    SerializedProperty gridWidth = template.FindPropertyRelative("gridWidth");
                    SerializedProperty gridHeight = template.FindPropertyRelative("gridHeight");
                    SerializedProperty cellSize = template.FindPropertyRelative("cellSize");
                    SerializedProperty sceneFreeAuthoring = template.FindPropertyRelative("sceneFreeAuthoring");
                    if (templateId == null || gridWidth == null || gridHeight == null
                        || cellSize == null || sceneFreeAuthoring == null)
                    {
                        Debug.LogError("[ES] 地图空间模板结构不完整，已取消填充。", target);
                        return;
                    }
                    templateId.stringValue = "default-space";
                    gridWidth.intValue = 16;
                    gridHeight.intValue = 16;
                    cellSize.floatValue = 16f;
                    sceneFreeAuthoring.boolValue = true;
                    serializedObject.ApplyModifiedProperties();
                    ESWorldMapAuthoringUtility.MarkChanged((ESWorldMapAsset)target);
                }
                if (GUILayout.Button("保存地图资产", GUILayout.Height(24f)))
                {
                    ESWorldMapSaveResult result = ESWorldMapAuthoringUtility.Save((ESWorldMapAsset)target, serializedObject);
                    if (!result.success) Debug.LogError("[ES] 地图资产保存失败：" + result.error, target);
                    else Debug.Log("[ES] 地图资产已保存，内容版本 " + result.contentVersion + "。", target);
                }
            }

            EditorGUILayout.HelpBox("默认后端为 Unity Terrain，ES Heightfield 作为地图数据源和降级路径；Voxel 是后续可选提供器。运行时只消费已构建的地形数据，不扫描 AssetDatabase。", MessageType.None);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
