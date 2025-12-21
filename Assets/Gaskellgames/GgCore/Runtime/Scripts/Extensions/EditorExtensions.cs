#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class EditorExtensions
    {
        #region Variables

        public static float singleLine => EditorGUIUtility.singleLineHeight;
        
        public static float standardSpacing = EditorGUIUtility.standardVerticalSpacing;
        
        public static float labelWidthForIndentLevel => EditorGUIUtility.labelWidth - (EditorGUI.indentLevel * singleLine);
        public static float currentIndent => EditorGUI.indentLevel * singleLine;

        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region Get Assets

        /// <summary>
        /// Get the first found asset of a set type from the project files
        /// </summary>
        /// <param name="folderPaths">Optional parameter for searching in specific folders</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetAssetByType<T>(string[] folderPaths = null) where T : Object
        {
            List<T> assets = GetAllAssetsByType<T>(folderPaths, false);
            return assets.Count <= 0 ? null : assets[0];
        }
        
        /// <summary>
        /// Get all assets of a set type from the project files
        /// </summary>
        /// <param name="folderPaths">Optional parameter for searching in specific folders</param>
        /// <param name="alphabetical">Optional parameter for sorting the list alphabetically</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static List<T> GetAllAssetsByType<T>(string[] folderPaths = null, bool alphabetical = false) where T : Object
        {
            List<T> assets = new List<T>();
            
            // check for special case (where Unity changes the type via import settings)
            if (typeof(T).FullName == typeof(Texture).FullName || typeof(T).FullName == typeof(Texture2D).FullName)
            {
                return GetAllTextures<T>(folderPaths, alphabetical);
            }

            // handle generic cases
            string[] guids = folderPaths == null
                ? AssetDatabase.FindAssets($"t:{typeof(T).FullName}")
                : AssetDatabase.FindAssets($"t:{typeof(T).FullName}", folderPaths);
            
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>( assetPath );
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }
            
            return alphabetical ? assets.OrderBy(x => x.name).ToList() : assets;
        }

        /// <summary>
        /// Get all assets of a set type from the project files
        /// </summary>
        /// <param name="folderPaths">Optional parameter for searching in specific folders</param>
        /// <param name="alphabetical">Optional parameter for sorting the list alphabetically</param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static List<T> GetAllTextures<T>(string[] folderPaths = null, bool alphabetical = false) where T : Object
        {
            // type check
            if (typeof(T).FullName != typeof(Texture).FullName && typeof(T).FullName != typeof(Texture2D).FullName)
            {
                GgLogs.Log(null, GgLogType.Error, "GetAllTextures cannot get fie types of {0}", typeof(T).FullName);
                return new List<T>();
            }
            
            // generate search pattern list
            List<string> searchPatterns = new List<string>()
            {
                "*.psd",
                "*.tiff",
                "*.jpg",
                "*.tga",
                "*.png",
                "*.gif",
                "*.bmp",
                "*.iff",
                "*.pict"
            };
            
            // search through each folder, getting all files that match the search pattern types
            List<T> assets = new List<T>();
            folderPaths ??= new[] { $"{Application.dataPath}/" };
            foreach (string folderPath in folderPaths)
            {
                List<string> files = new List<string>();
                foreach (string searchPattern in searchPatterns)
                {
                    string[] results = Directory.GetFiles(folderPath, searchPattern, SearchOption.TopDirectoryOnly);
                    if (0 < results.Length)
                    {
                        files.AddRange(results.ToList());
                    }
                }
                foreach (string file in files)
                {
                    T asset = AssetDatabase.LoadAssetAtPath(file, typeof(T)) as T;
                    if (asset != null)
                    {
                        assets.Add(asset);
                    }
                }
            }
            
            return alphabetical ? assets.OrderBy(x => x.name).ToList() : assets;
        }
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Scene View

        /// <summary>
        /// Get the raycast target for the scene view camera
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public static bool GetSceneViewLookAt(out Vector3 point)
        {
            Camera camera = SceneView.lastActiveSceneView.camera;
            Vector3 position = camera.transform.position;
            Vector3 forward = camera.transform.forward;

            if (Physics.Raycast(position, forward, out RaycastHit hit, Mathf.Infinity))
            {
                point = hit.point;
                return true;
            }
            else
            {
                point = position + (forward * 2);
                return false;
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region CustomFields
        
        public static Color32 ColorFieldSquare(GUIContent giuContent, Color32 colorValue, bool showEyeDropper = false, bool showAlpha = false, bool hdr = false)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = EditorGUIUtility.singleLineHeight - (spacing + spacing);
            float width = showEyeDropper ? height * 2 : height;
            GUILayoutOption[] parameters = new [] { GUILayout.Width(width), GUILayout.Height(height) };
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(giuContent);
            if (!string.IsNullOrEmpty(giuContent.text)) { GUILayout.Space(2); }
            EditorGUILayout.BeginVertical();
            GUILayout.Space(spacing);
            colorValue = EditorGUILayout.ColorField(new GUIContent("", giuContent.tooltip), colorValue, showEyeDropper, showAlpha, hdr, parameters);
            GUILayout.Space(spacing);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            return colorValue;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Component Sorting

        /// <summary>
        /// Get the component count of a specified gameObject.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static int GetComponentCount(GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents(typeof(Component));
            return components.Length;
        }

        /// <summary>
        /// Get the component index of a specified component.
        /// </summary>
        /// <param name="component"></param>
        /// <returns>Component index if exists, -1 otherwise.</returns>
        public static int GetComponentIndex(Component component)
        {
            GameObject gameObject = component.gameObject;
            Component[] components = gameObject.GetComponents(typeof(Component));
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == component)
                {
                    return i;
                }
            }

            return -1;
        }
        
        /// <summary>
        /// Move a specified component to the top in the inspector.
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        public static bool MoveComponentToTop(Component component)
        {
            return TryMoveComponentToIndex(component, 1);
        }
        
        /// <summary>
        /// Move a specified component to the bottom in the inspector.
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        public static bool MoveComponentToBottom(Component component)
        {
            int count = GetComponentCount(component.gameObject);
            return TryMoveComponentToIndex(component, count - 1);
        }
        
        /// <summary>
        /// Move a specified component up in the inspector.
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        public static bool MoveComponentUp(Component component)
        {
            return UnityEditorInternal.ComponentUtility.MoveComponentUp(component);
        }
        
        /// <summary>
        /// Move a specified component down in the inspector.
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns>
        public static bool MoveComponentDown(Component component)
        {
            return UnityEditorInternal.ComponentUtility.MoveComponentDown(component);
        }
        
        /// <summary>
        /// Move a specified component to a specified position index in the inspector.
        /// </summary>
        /// <param name="component"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static bool TryMoveComponentToIndex(Component component, int index)
        {
            // get the current index of the component and check it is not already at that index
            int currentIndex = GetComponentIndex(component);
            if (currentIndex == index) { return true; }
            
            // clamp index to valid value
            int count = GetComponentCount(component.gameObject);
            if (index <= 0) { index = 1; }
            else if (count <= index) { index = count - 1; }

            // move to index
            int infiniteLoopCheck = 0;
            while (currentIndex != index)
            {
                if (index < currentIndex) { MoveComponentUp(component); }
                else if (currentIndex < index) { MoveComponentDown(component); }
                currentIndex = GetComponentIndex(component);
                
                // safety check
                infiniteLoopCheck++;
                if (1000 < infiniteLoopCheck) { return false; }
            }
            
            return true;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Inspector Functions
        
        /// <summary>
        /// Draws a string as a tag in the inspector
        /// </summary>
        /// <param name="stringValue"></param>
        public static void DrawAsTag(string stringValue, string tooltip = "")
        {
            // cache default values
            bool guiDefault = GUI.enabled;
            Color defaultBackground = GUI.backgroundColor;
            
            // set values
            GUI.enabled = false;
            GUI.backgroundColor = new Color32(179, 179, 179, 255);
            
            if (string.IsNullOrEmpty(stringValue))
            {
                // draw 'null'
                EditorGUILayout.LabelField("n/a");
            }
            else
            {
                // draw tag
                float labelWidth = StringExtensions.GetStringWidth(stringValue) + 6;
                GUILayout.Button(new GUIContent(stringValue, tooltip), GUILayout.Width(labelWidth));
            }
                
            // reset default
            GUI.enabled = guiDefault;
            GUI.backgroundColor = defaultBackground;
        }
        
        /// <summary>
        /// Draw the OnInspectorGUI for a specified Editor, with the options to hide the ScriptField.
        /// </summary>
        /// <param name="editor"></param>
        /// <param name="hideScriptField"></param>
        /// <returns>Whether the specified Editor has any updates/changes (ChangeCheck)</returns>
        public static bool OnInspectorGUI(this Editor editor, bool hideScriptField)
        {
            if (!editor) { return false; }
            return OnInspectorGUI(editor, hideScriptField, false);
        }
        
        internal static bool OnInspectorGUI(this Editor editor, bool hideScriptField, bool forceAllowInLine)
        {
            if (!editor) { return false; }
            if (hideScriptField)
            {
                EditorGUI.BeginChangeCheck();
                editor.serializedObject.Update();
                SerializedProperty iterator = editor.serializedObject.GetIterator();
                iterator.NextVisible(true);
                while (iterator.NextVisible(false))
                {
                    // check for hide in line
                    HideInLineAttribute[] hideInLineAttributes = iterator.GetAttributes<HideInLineAttribute>(true);
                    if (hideInLineAttributes != null && 0 < hideInLineAttributes.Length) { continue; }
                    
                    // check for in line editor
                    InLineEditorAttribute[] inLineEditorAttributes = iterator.GetAttributes<InLineEditorAttribute>(true);
                    if (!forceAllowInLine && inLineEditorAttributes != null && 0 < inLineEditorAttributes.Length)
                    {
                        if (iterator.TryGetObjectType(out Type type))
                        {
                            EditorGUILayout.BeginHorizontal();
                            bool defaultShowMixedValue = EditorGUI.showMixedValue;
                            EditorGUI.showMixedValue = iterator.hasMultipleDifferentValues;
                            EditorGUILayout.PrefixLabel(GgGUI.TryGetMultiEditIcon(new GUIContent(iterator.displayName, iterator.tooltip), iterator.hasMultipleDifferentValues));
                            GUILayout.Space(2);
                            iterator.objectReferenceValue = EditorGUILayout.ObjectField(GUIContent.none, iterator.objectReferenceValue, type, true);
                            EditorGUI.showMixedValue = defaultShowMixedValue;
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.PropertyField(iterator,true);
                        }
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(iterator,true);
                    }
                }
                editor.serializedObject.ApplyModifiedProperties();
                return EditorGUI.EndChangeCheck();
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                editor.OnInspectorGUI();
                return EditorGUI.EndChangeCheck();
            }
        }

        /// <summary>
        /// Returns attributes of type <typeparamref name="TAttribute"/> on <paramref name="serializedProperty"/>.
        /// </summary>
        public static TAttribute[] GetAttributes<TAttribute>(this SerializedProperty serializedProperty, bool inherit) where TAttribute : Attribute
        {
            if (serializedProperty == null) { return new TAttribute[] {}; }
            Type targetObjectType = serializedProperty.serializedObject.targetObject.GetType();
            foreach (var pathSegment in serializedProperty.propertyPath.Split('.'))
            {
                FieldInfo fieldInfo = targetObjectType.GetField(pathSegment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    return (TAttribute[])fieldInfo.GetCustomAttributes<TAttribute>(inherit);
                }

                PropertyInfo propertyInfo = targetObjectType.GetProperty(pathSegment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (propertyInfo != null)
                {
                    return (TAttribute[])propertyInfo.GetCustomAttributes<TAttribute>(inherit);
                }
            }
            return new TAttribute[] {};
        }
        
        public static void DrawSubLabels(Rect position, string minLabel, string maxLabel)
        {
            GUIStyle subLabelStyle = GgGUI.SubLabelStyle;
            GUI.Label(position, minLabel, subLabelStyle);
            subLabelStyle.alignment = TextAnchor.UpperRight;
            GUI.Label(position, maxLabel, subLabelStyle);
        }
        
        /// <summary>
        /// start custom inspector background with a defined color and padding.
        /// Default values of padding are set to account for inspector rect offsets:
        /// paddingTop = -4, paddingBottom = -15, paddingLeft = -18, paddingRight = -4
        /// </summary>
        /// <param name="backgroundColor"></param>
        public static void BeginCustomInspectorBackground(Color32 backgroundColor, float paddingTop = -4, float paddingBottom = -15, float paddingLeft = -18, float paddingRight = -4)
        {
            // cache variables
            Rect screenRect = GUILayoutUtility.GetRect(1, 1);
            Rect verticalRect = EditorGUILayout.BeginVertical();
            
            // calculate rect size
            float xMin = screenRect.x + paddingLeft;
            float yMin = screenRect.y + paddingTop;
            float width = screenRect.width - (paddingLeft + paddingRight);
            float height = verticalRect.height - (paddingTop + paddingBottom);
            
            // draw background rect
            EditorGUI.DrawRect(new Rect(xMin, yMin, width, height), backgroundColor);
        }

        /// <summary>
        /// end custom inspector background
        /// </summary>
        public static void EndCustomInspectorBackground()
        {
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Force repaint the inspector for a targetObject via a SerializedProperty reference
        /// </summary>
        /// <param name="property"></param>
        public static void RepaintInspector(SerializedProperty property)
        {
            // EditorUtility.SetDirty(property.serializedObject.targetObject);
            
            foreach (var item in ActiveEditorTracker.sharedTracker.activeEditors)
            {
                if (item.serializedObject != property.serializedObject) { continue; }
                
                item.Repaint();
                return;
            }
        }
        
        /// <summary>
        /// Start a foldout group (nestable) with an object field in the foldout header
        /// </summary>
        /// <param name="label">Label used for label text and tooltip</param>
        /// <param name="isOpen">Reference to a bool to be used to store if the foldout group is open</param>
        /// <param name="style">The style to be used for the vertical group (i.e "Box")</param>
        /// <returns></returns>
        public static bool BeginFoldoutObjectGroupNestable<T>(SerializedProperty property, GUIContent label, bool isOpen, GUIStyle style = null, int paddingTop = 0, int paddingBottom = 0)
        {
            // default style
            if (style == null) { style = EditorStyles.helpBox; }
            
            EditorGUILayout.BeginVertical(style);
            GUILayout.Space(paddingTop);
            
            string icon = "d_IN_foldout";
            if (isOpen) { icon = "d_IN_foldout_on"; }
            Texture iconTexture = EditorGUIUtility.IconContent(icon).image;
            
            bool defaultState = GUI.enabled;
            GUILayout.BeginHorizontal();
            GUI.enabled = true;
            EditorGUILayout.BeginVertical(GUILayout.Width(GgGUI.labelWidth - standardSpacing));
            //GUILayout.Space(standardSpacing);
            if (GUILayout.Button(new GUIContent(label.text, iconTexture, label.tooltip), GgGUI.DropdownButtonStyleBold))
            {
                isOpen = !isOpen;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            GUILayout.Space(-1);
            EditorGUILayout.ObjectField(property, typeof(T), GUIContent.none);
            EditorGUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUI.enabled = defaultState;
            
            GUILayout.Space(paddingBottom - 1);
            
            return isOpen;
        }
        
        /// <summary>
        /// Start a foldout group (nestable)
        /// </summary>
        /// <param name="label">Label used for label text and tooltip</param>
        /// <param name="isOpen">Reference to a bool to be used to store if the foldout group is open</param>
        /// <param name="style">The style to be used for the vertical group (i.e "Box")</param>
        /// <returns></returns>
        public static bool BeginFoldoutGroupNestable(GUIContent label, bool isOpen, GUIStyle style = null, int paddingTop = 0, int paddingBottom = 0)
        {
            // default style
            if (style == null) { style = EditorStyles.helpBox; }
            
            EditorGUILayout.BeginVertical(style);
            GUILayout.Space(paddingTop);
            
            string icon = "d_IN_foldout";
            if (isOpen) { icon = "d_IN_foldout_on"; }
            Texture iconTexture = EditorGUIUtility.IconContent(icon).image;
            
            bool defaultState = GUI.enabled;
            GUI.enabled = true;
            if (GUILayout.Button(new GUIContent(label.text, iconTexture, label.tooltip), GgGUI.DropdownButtonStyleBold, GUILayout.ExpandWidth(true)))
            {
                isOpen = !isOpen;
            }
            GUI.enabled = defaultState;
            
            GUILayout.Space(paddingBottom);
            
            return isOpen;
        }

        /// <summary>
        /// End a foldout group (nestable)
        /// </summary>
        public static void EndFoldoutGroupNestable()
        {
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// Start a foldout group (nestable)
        /// </summary>
        /// <param name="label">Label used for label text and tooltip</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <returns></returns>
        public static bool BeginCustomFoldoutGroup(GUIContent label, SerializedProperty property)
        {
            BeginCustomInspectorBackground(InspectorExtensions.backgroundSeperatorColorDark, 1, -2);
            EndCustomInspectorBackground();
            BeginCustomInspectorBackground(InspectorExtensions.backgroundNormalColor, 1, -3);
            property.isExpanded = BeginFoldoutGroupNestable(label, property.isExpanded, new GUIStyle(), 3);
            EndFoldoutGroupNestable();
            EndCustomInspectorBackground();
            if (property.isExpanded) { GUILayout.Space(2); }
            return property.isExpanded;
        }

        /// <summary>
        /// End a custom foldout group (nestable)
        /// </summary>
        public static void EndCustomFoldoutGroup()
        {
            BeginCustomInspectorBackground(InspectorExtensions.backgroundSeperatorColorDark, 2, -3);
            EndCustomInspectorBackground();
        }
        
        /// <summary>
        /// Draw a horizontal line across the inspector
        /// </summary>
        /// <param name="lineColor"></param>
        /// <param name="spaceBefore"></param>
        /// <param name="spaceAfter"></param>
        public static void DrawInspectorLine(Color32 lineColor, int spaceBefore = 0, int spaceAfter = 0)
        {
            // add space before?
            GUILayout.Space(spaceBefore);
            
            // draw line and reset gui color
            Color defaultGUIColor = GUI.color;
            GUI.color = lineColor;
            GUIStyle horizontalLine = new GUIStyle
            {
                normal = { background = EditorGUIUtility.whiteTexture },
                margin = new RectOffset( 0, 0, 4, 4 ),
                fixedHeight = 1
            };
            
            GUILayout.Box(GUIContent.none, horizontalLine);
            GUI.color = defaultGUIColor;
            
            // add space after?
            GUILayout.Space(spaceAfter);
        }
        
        /// <summary>
        /// Draw a horizontal line across the whole of the inspector
        /// </summary>
        /// <param name="lineColor"></param>
        /// <param name="spaceBefore"></param>
        /// <param name="spaceAfter"></param>
        public static void DrawInspectorLineFull(Color32 lineColor, int spaceBefore = 0, int spaceAfter = 0)
        {
            GUILayout.Space(spaceBefore);
            BeginCustomInspectorBackground(lineColor, 2, -3);
            EndCustomInspectorBackground();
            GUILayout.Space(spaceAfter);
        }

        /// <summary>
        /// Get all editor labels for an object
        /// </summary>
        /// <param name="objectReference"></param>
        /// <returns></returns>
        public static string[] GetAllObjectLabels(Object objectReference)
        {
            return AssetDatabase.GetLabels(objectReference);
        }
        
        #endregion

    } // class end
}

#endif