#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(FilePathAttribute), true)]
    public class FilePathDrawer : GgPropertyDrawer
    {
        #region Variables
        
        private Texture iconTexture;
        private GUIStyle iconButtonStyle = new GUIStyle();

        private bool changedByPopUp;
        private string selectedPath;
        private int warningHeight = 25;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GgPropertyHeight
        
        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            if (property.propertyType != SerializedPropertyType.String) { return propertyHeight; }
            
            if (string.IsNullOrEmpty(selectedPath) || (selectedPath != property.stringValue && !changedByPopUp))
            {
                selectedPath = property.stringValue;
            }
            CreateButton();
            return propertyHeight + (FileExtensions.IsFilePathValid(selectedPath) || property.hasMultipleDifferentValues
                ? 0
                : warningHeight + standardSpacing);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI
        
        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                GgGUI.CustomPropertyField(position, property, label);
                return;
            }
            
            // calculate positions
            float iconWidth = singleLineHeight;
            Rect fieldPosition = new Rect(position.xMin, position.yMin, position.width - (iconWidth + standardSpacing), position.height);
            Rect iconPosition = new Rect(fieldPosition.xMax + standardSpacing, position.yMin, iconWidth, position.height);

            // try draw warning
            if (!FileExtensions.IsFilePathValid(selectedPath) && !property.hasMultipleDifferentValues)
            {
                Rect warningPosition = new Rect(position.xMin, position.yMin, position.width, warningHeight);
                EditorGUI.HelpBox(warningPosition, "File path does not exist", MessageType.Warning);

                fieldPosition.yMin += warningHeight + standardSpacing;
                iconPosition.yMin += warningHeight + standardSpacing;
            }
            
            // draw text field and 'open panel' button
            if (GUI.Button(iconPosition, iconTexture, iconButtonStyle))
            {
                selectedPath = EditorUtility.OpenFilePanel("Select a file", "Assets", "");
                if (selectedPath.Contains(Application.dataPath))
                {
                    selectedPath = selectedPath.Substring(Application.dataPath.Length + 1);
                    changedByPopUp = true;
                }
                else
                {
                    GgLogs.Log(null, GgLogType.Error, "The file path must be in the Assets folder.");
                }
                GUIUtility.ExitGUI();
            }
                
            // set value
            if (GgGUI.TextField(fieldPosition, label, selectedPath, out selectedPath, property.hasMultipleDifferentValues) || changedByPopUp)
            {
                property.stringValue = selectedPath;
                changedByPopUp = false;
            }
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions
        
        private void CreateButton()
        {
            iconTexture = EditorGUIUtility.IconContent("d_FolderOpened Icon").image;
            
            iconButtonStyle.fontSize = 9;
            iconButtonStyle.alignment = TextAnchor.MiddleCenter;
            iconButtonStyle.normal.textColor = InspectorExtensions.textNormalColor;
            iconButtonStyle.hover.textColor = InspectorExtensions.textNormalColor;
            iconButtonStyle.active.textColor = InspectorExtensions.textNormalColor;
            iconButtonStyle.normal.background = InspectorExtensions.CreateTexture(20, 20, 1, true, InspectorExtensions.blankColor, InspectorExtensions.blankColor);
            iconButtonStyle.hover.background = InspectorExtensions.CreateTexture(20, 20, 1, true, InspectorExtensions.buttonHoverColor, InspectorExtensions.blankColor);
            iconButtonStyle.active.background = InspectorExtensions.CreateTexture(20, 20, 1, true, InspectorExtensions.buttonActiveColor, InspectorExtensions.buttonActiveBorderColor);
        }

        #endregion

    } // class end
}
#endif