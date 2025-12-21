#if UNITY_EDITOR
#if GASKELLGAMES
using System;
using System.Linq;
using System.Reflection;
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.FolderSystem.EditorOnly
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// </summary>
    
    [InitializeOnLoad]
    public class HierarchyFolderIcons
    {
        #region Static Variables

        // icons
        private static Texture2D icon_FolderEmpty_Active;
        private static Texture2D icon_FolderEmpty_Disabled;
        
        private static Texture2D icon_FolderOpen_Active;
        private static Texture2D icon_FolderOpen_Disabled;
        
        private static Texture2D icon_FolderClosed_Active;
        private static Texture2D icon_FolderClosed_Disabled;
        
        private static Texture2D icon_HierarchyHighlight;

        private const string packageRefName = "FolderSystem";
        private const string relativePath = "/Editor/Icons/";
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Contructors

        static HierarchyFolderIcons()
        {
            GgEditorCallbacks.OnSafeInitialize -= CreateHierarchyIcon_Folder;
            GgEditorCallbacks.OnSafeInitialize += CreateHierarchyIcon_Folder;
            
            // subscribe to inspector updates
            EditorApplication.hierarchyWindowItemOnGUI -= EditorApplication_hierarchyWindowItemOnGUI;
            EditorApplication.hierarchyWindowItemOnGUI += EditorApplication_hierarchyWindowItemOnGUI;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private static void CreateHierarchyIcon_Folder()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            
            // load base icons
            Texture2D icon_FolderOpen = AssetDatabase.LoadAssetAtPath(filePath + "Icon_FolderOpen.png", typeof(Texture2D)) as Texture2D;
            Texture2D icon_FolderClosed = AssetDatabase.LoadAssetAtPath(filePath + "Icon_FolderClosed.png", typeof(Texture2D)) as Texture2D;
            Texture2D icon_FolderEmpty = AssetDatabase.LoadAssetAtPath(filePath + "Icon_FolderEmpty.png", typeof(Texture2D)) as Texture2D;
            
            // create custom icons
            icon_FolderOpen_Active = InspectorExtensions.TintTexture(icon_FolderOpen, InspectorExtensions.textNormalColor);
            icon_FolderOpen_Disabled = InspectorExtensions.TintTexture(icon_FolderOpen, InspectorExtensions.textDisabledColor);
            
            icon_FolderClosed_Active = InspectorExtensions.TintTexture(icon_FolderClosed, InspectorExtensions.textNormalColor);
            icon_FolderClosed_Disabled = InspectorExtensions.TintTexture(icon_FolderClosed, InspectorExtensions.textDisabledColor);
            
            icon_FolderEmpty_Active = InspectorExtensions.TintTexture(icon_FolderEmpty, InspectorExtensions.textNormalColor);
            icon_FolderEmpty_Disabled = InspectorExtensions.TintTexture(icon_FolderEmpty, InspectorExtensions.textDisabledColor);
        }

        private static void CreateHierarchyIcon_Highlight()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            
            icon_HierarchyHighlight = AssetDatabase.LoadAssetAtPath(filePath + "Icon_HierarchyHighlight.png", typeof(Texture2D)) as Texture2D;
        }
        
        private static void EditorApplication_hierarchyWindowItemOnGUI(int instanceID, Rect position)
        {
            // check for valid draw
            if (Event.current.type != EventType.Repaint) { return; }
            
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject != null)
            {
                HierarchyFolders component = gameObject.GetComponent<HierarchyFolders>();
                if (component != null)
                {
                    // cache values
                    int hierarchyPixelHeight = 16;
                    bool isSelected = Selection.instanceIDs.Contains(instanceID);
                    bool isActive = component.isActiveAndEnabled;
                    bool isEmpty = (0 == component.transform.childCount);
                    Color32 defaultContentColor = GUI.contentColor;
                    Color32 textColor;
                    Texture2D folderIcon;
                    
                    // check icons exist
                    if (!icon_FolderClosed_Active || !icon_FolderClosed_Disabled || !icon_FolderEmpty_Active || !icon_FolderEmpty_Disabled)
                    {
                        CreateHierarchyIcon_Folder();
                    }
                    
                    if (isActive || isSelected)
                    {
                        // text
                        textColor = component.customText ? component.textColor : InspectorExtensions.textNormalColor;

                        // enabled/selected icon
                        if (isEmpty)
                        {
                            // empty icon
                            folderIcon = component.customIcon ? InspectorExtensions.TintTexture(icon_FolderEmpty_Active, component.iconColor) : icon_FolderEmpty_Active;
                        }
                        else if (IsExpanded(gameObject))
                        {
                            // open icon
                            folderIcon = component.customIcon ? InspectorExtensions.TintTexture(icon_FolderOpen_Active, component.iconColor) : icon_FolderOpen_Active;
                        }
                        else
                        {
                            // closed icon
                            folderIcon = component.customIcon ? InspectorExtensions.TintTexture(icon_FolderClosed_Active, component.iconColor) : icon_FolderClosed_Active;
                        }
                    }
                    else
                    {
                        // text
                        textColor = component.customText ? (Color)component.textColor * 0.6f : InspectorExtensions.textDisabledColor;

                        // disabled icon
                        if (isEmpty)
                        {
                            // empty icon
                            folderIcon = component.customIcon ? InspectorExtensions.TintTexture(icon_FolderEmpty_Disabled, component.iconColor) : icon_FolderEmpty_Disabled;
                        }
                        else if (IsExpanded(gameObject))
                        {
                            // open icon
                            folderIcon = component.customIcon ? InspectorExtensions.TintTexture(icon_FolderOpen_Disabled, component.iconColor) : icon_FolderOpen_Disabled;
                        }
                        else
                        {
                            // closed icon
                            folderIcon = component.customIcon ? InspectorExtensions.TintTexture(icon_FolderClosed_Disabled, component.iconColor) : icon_FolderClosed_Disabled;
                        }
                    }

                    // draw background
                    Color32 backgroundColor = isSelected ? InspectorExtensions.backgroundActiveColor : InspectorExtensions.backgroundNormalColorLight;
                    Rect backgroundPosition = new Rect(position.xMin, position.yMin, position.width + hierarchyPixelHeight, position.height);
                    EditorGUI.DrawRect(backgroundPosition, backgroundColor);
                    
                    // check icon exists
                    if (!icon_HierarchyHighlight)
                    {
                        CreateHierarchyIcon_Highlight();
                    }
                    
                    // draw highlight
                    if (component.customHighlight)
                    {
                        GUI.contentColor = component.highlightColor;
                        Rect iconPosition = new Rect(position.xMin, position.yMin, icon_HierarchyHighlight.width, icon_HierarchyHighlight.height);
                        GUIContent iconGUIContent = new GUIContent(icon_HierarchyHighlight);
                        EditorGUI.LabelField(iconPosition, iconGUIContent);
                        GUI.contentColor = defaultContentColor;
                    }
                    
                    // draw icon
                    if (folderIcon != null)
                    {
                        EditorGUIUtility.SetIconSize(new Vector2(hierarchyPixelHeight, hierarchyPixelHeight));
                        Rect iconPosition = new Rect(position.xMin, position.yMin, hierarchyPixelHeight, hierarchyPixelHeight);
                        GUIContent iconGUIContent = new GUIContent(folderIcon);
                        EditorGUI.LabelField(iconPosition, iconGUIContent);
                    }
                    
                    // draw text
                    GUIStyle hierarchyText = new GUIStyle() { };
                    hierarchyText.normal = new GUIStyleState() { textColor = textColor };
                    hierarchyText.fontStyle = component.textStyle;
                    int indentX;
                    if (component.textAlignment == HierarchyFolders.TextAlignment.Center)
                    {
                        hierarchyText.alignment = TextAnchor.MiddleCenter;
                        indentX = 0;
                    }
                    else
                    {
                        hierarchyText.alignment = TextAnchor.MiddleLeft;
                        indentX = hierarchyPixelHeight + 2;
                    }
                    Rect textOffset = new Rect(position.xMin + indentX, position.yMin, position.width, position.height);
                    EditorGUI.LabelField(textOffset, component.name, hierarchyText);
                }
            }
        }
        
        /// <summary>
        /// Check if a gameObject in the hierarchy is expanded
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        private static bool IsExpanded(GameObject gameObject)
        {
            // using reflection, we get the internal method for 'GetExpandedIDs'
            Type sceneHierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            MethodInfo getExpandedIDs = sceneHierarchyWindowType.GetMethod("GetExpandedIDs", BindingFlags.NonPublic | BindingFlags.Instance);
            PropertyInfo lastInteractedHierarchyWindow = sceneHierarchyWindowType.GetProperty("lastInteractedHierarchyWindow", BindingFlags.Public | BindingFlags.Static);
            if (lastInteractedHierarchyWindow == null) { return false; }
            if (getExpandedIDs == null) { return false; }
            
            // get array of expanded id's
            int[] expandedIDs = getExpandedIDs.Invoke(lastInteractedHierarchyWindow.GetValue(null), null) as int[];
            if (expandedIDs == null) { return false; }
            
            // check if referenced gameObject is expanded
            return expandedIDs.Contains(gameObject.GetInstanceID());
        }

        #endregion
        
    } // class end
}

#endif
#endif