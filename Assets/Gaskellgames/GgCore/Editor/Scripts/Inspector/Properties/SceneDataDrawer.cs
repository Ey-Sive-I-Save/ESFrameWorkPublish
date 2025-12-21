#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(SceneData))]
    public class SceneDataDrawer : PropertyDrawer
    {
        #region Variables

        private SerializedProperty sceneAsset;
        private SerializedProperty guid;
        private SerializedProperty buildIndex;
        private SerializedProperty sceneName;
        private SerializedProperty sceneFilePath;

        private string tooltipSuffix = "EditorOnly References:\n- SceneAsset\n\nRuntime references:\n- Scene (no data to show)\n- buildIndex\n- sceneName\n- sceneFilePath\n- GUID (Set in editor, or via SceneDataList)";
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GetPropertyHeight

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return GgGUI.singleLineHeight + (GgGUI.standardSpacing * 2);
            }
            
            sceneAsset = property.FindPropertyRelative("sceneAsset");
            
            return (sceneAsset.objectReferenceValue == null
                ? (GgGUI.singleLineHeight + GgGUI.standardSpacing) * 3
                : ((GgGUI.singleLineHeight + GgGUI.standardSpacing) * 5) + GgGUI.standardSpacing) + GgGUI.standardSpacing;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGUI
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);
            
            sceneAsset = property.FindPropertyRelative("sceneAsset");
            buildIndex = property.FindPropertyRelative("buildIndex");
            sceneName = property.FindPropertyRelative("sceneName");
            sceneFilePath = property.FindPropertyRelative("sceneFilePath");
            guid = property.FindPropertyRelative("guid");
            label.tooltip = string.IsNullOrEmpty(label.tooltip) ? tooltipSuffix : label.tooltip + "\n\n" + tooltipSuffix;
            bool guiEnabled = GUI.enabled;
            
            // draw header
            Rect dropdownRect = new Rect(position.x, position.y, position.width, position.height - GgGUI.standardSpacing);
            GgGUI.GetFoldoutPositionRects(dropdownRect, label, out GgFoldoutPositions foldoutPositions);
            GgGUI.DrawDropdownHeader(foldoutPositions, property, label, false, true);
            
            // draw object field
            bool changed = GgGUI.ObjectField(foldoutPositions.field, GUIContent.none, sceneAsset.objectReferenceValue, out Object outputValue, typeof(SceneAsset), property.hasMultipleDifferentValues, false);
            SceneAsset sceneAssetRef = outputValue as SceneAsset;
            
            // cache SceneData if SceneAsset updated in inspector
            if (changed)
            {
                sceneAsset.objectReferenceValue = sceneAssetRef;
                if (sceneAsset.objectReferenceValue != null)
                {
                    buildIndex.intValue = SceneUtility.GetBuildIndexByScenePath(sceneFilePath.stringValue);
                    sceneName.stringValue = sceneAsset.objectReferenceValue.name;
                    sceneFilePath.stringValue = AssetDatabase.GetAssetOrScenePath(sceneAsset.objectReferenceValue);
                    guid.stringValue = AssetDatabase.AssetPathToGUID(sceneFilePath.stringValue);
                }
                else
                {
                    buildIndex.intValue = -1;
                    sceneName.stringValue = null;
                    sceneFilePath.stringValue = null;
                    guid.stringValue = null;
                }
            }
            
            // if not expanded: hide extra info & close property
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }
            
            // show extra info ...
            if (sceneAsset.objectReferenceValue)
            {
                // ... content
                GUI.enabled = false;
                EditorGUI.indentLevel++;
                
                GUIContent sceneInfoLabel = new GUIContent("Build Index", "Runtime reference to the BuildIndex of the scene.");
                EditorGUI.IntField(foldoutPositions.content, sceneInfoLabel, buildIndex.intValue);
                
                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                sceneInfoLabel = new GUIContent("Scene Name", "Runtime reference to the SceneName of the scene.");
                EditorGUI.TextField(foldoutPositions.content, sceneInfoLabel, sceneName.stringValue);
                
                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                sceneInfoLabel = new GUIContent("Scene File Path", "Runtime reference to the SceneFilePath of the scene.");
                EditorGUI.TextField(foldoutPositions.content, sceneInfoLabel, sceneFilePath.stringValue);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                sceneInfoLabel = new GUIContent("GUID", "Runtime reference to the GUID of the scene.");
                EditorGUI.TextField(foldoutPositions.content, sceneInfoLabel, guid.stringValue);
                
                EditorGUI.indentLevel--;
                GUI.enabled = guiEnabled;
            }
            else
            {
                // ... warning
                Rect thisContent = foldoutPositions.content;
                thisContent.height = GgGUI.singleLineHeight * 2;
                EditorGUI.HelpBox(thisContent, "Warning: Reference object asset is null.", MessageType.Warning);
            }

            // close property
            EditorGUI.EndProperty();
        }
        
        #endregion

    } // class end
}
#endif