#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(SerializedDictionary<,>))]
    public class SerializedDictionaryDrawer : GgPropertyDrawer
    {
        #region variables

        private SerializedProperty cachedProperty;
        private SerializedProperty serializedDictionary;
        
        private float columnHeaderHeight => singleLineHeight + standardSpacing;
        private float keyValueHeight;
        private bool keyError;
        private Vector2 scrollPos;
        private MultiColumnHeader columnHeader;
        private MultiColumnHeaderState.Column[] columns;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            cachedProperty = property;
            
            // show just header if not expanded
            if (!property.isExpanded) { return singleLineHeight + standardSpacing; }
            
            // if expanded we get the custom height of the serializedDictionary SerializedField instead of using the whole property
            serializedDictionary = property.FindPropertyRelative("serializedDictionary");
            if (serializedDictionary.arraySize <= 0)
            {
                // header + subheader + line
                return singleLineHeight + columnHeaderHeight + singleLineHeight + (standardSpacing * 2);
            }
            
            SerializedProperty arrayElement = serializedDictionary.GetArrayElementAtIndex(0);
            SerializedProperty key = arrayElement.FindPropertyRelative("key");
            SerializedProperty value = arrayElement.FindPropertyRelative("value");

            // get key/value heights
            float keyHeight = key == null ? 0 : PropertyDrawerExtensions.TryFindCustomPropertyDrawer(key, out PropertyDrawer keyDrawer)
                    ? keyDrawer.GetPropertyHeight(key, GUIContent.none)
                    : EditorGUI.GetPropertyHeight(key, GUIContent.none, true);
            float valueHeight = value == null ? 0 : PropertyDrawerExtensions.TryFindCustomPropertyDrawer(value, out PropertyDrawer valueDrawer)
                    ? valueDrawer.GetPropertyHeight(value, GUIContent.none)
                    : EditorGUI.GetPropertyHeight(value, GUIContent.none, true);
            keyValueHeight = Mathf.Max(keyHeight, valueHeight) + standardSpacing;

            // header + subheader + (arraySize * keyValueHeight)
            return singleLineHeight
                   + columnHeaderHeight
                   + (serializedDictionary.arraySize * (keyValueHeight + standardSpacing))
                   + (standardSpacing * 2.5f);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI

        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            // adjust position
            position.y += standardSpacing * 0.5f;
            position.yMax -= standardSpacing * 0.5f;
            
            // get reference to SerializeFields
            ASerializedDictionary target = property.GetValue<ASerializedDictionary>();
            serializedDictionary = property.FindPropertyRelative("serializedDictionary");
            
            // initialise if required
            if (columns == null)
            { 
                InitialiseTable(position, label);
            }
            else
            {
                // draw outline box
                float offset = standardSpacing * 0.5f;
                position = new Rect(position.x, position.y - standardSpacing, position.width, position.height + offset);
                EditorGUI.DrawRect(position, InspectorExtensions.backgroundShadowColor);
                position = new Rect(position.x + offset, position.y + offset, position.width - standardSpacing, position.height - standardSpacing);
                EditorGUI.DrawRect(position, InspectorExtensions.backgroundInfoBoxColor);
                
                // calculate position rects
                Rect headerRect = new Rect(position.x + standardSpacing, position.y + offset, position.width - standardSpacing, singleLineHeight);
                Rect labelRect = new Rect(headerRect.x, headerRect.y, headerRect.width - (55 + ((singleLineHeight + standardSpacing) * 2)), singleLineHeight);
                Rect errorRect = new Rect(labelRect.xMax + standardSpacing, headerRect.y + standardSpacing, singleLineHeight, singleLineHeight);
                Rect countRect = new Rect(errorRect.xMax + standardSpacing, headerRect.y, 55, singleLineHeight);
                Rect addRect = new Rect(headerRect.xMax - singleLineHeight, headerRect.y, singleLineHeight, singleLineHeight);
                Rect subHeaderRect = new Rect(position.x, headerRect.yMax + offset, position.width, columnHeader.height);
                Rect emptyLabelRect = new Rect(position.x + standardSpacing, subHeaderRect.yMax, position.width - standardSpacing, singleLineHeight);
                Rect contentRect = new Rect(position.x + standardSpacing, subHeaderRect.yMax + standardSpacing, position.width - standardSpacing, singleLineHeight);
                
                // draw header: get key and value types and append to label
                Type keyType = fieldInfo.FieldType.GetGenericArguments()[0];
                Type valueType = fieldInfo.FieldType.GetGenericArguments()[1];
                
                // label header content
                string icon = "d_IN_foldout";
                if (property.isExpanded) { icon = "d_IN_foldout_on"; }
                Texture iconTexture = EditorGUIUtility.IconContent(icon).image;
                GUIContent headerLabel = new GUIContent($"{label.text}<{keyType.Name},{valueType.Name}>", iconTexture, label.tooltip);
                GUI.enabled = true;
                if (GUI.Button(labelRect, headerLabel, GgGUI.DropdownButtonStyleBold))
                {
                    property.isExpanded = !property.isExpanded;
                }
                
                // key error warning
                if (keyError)
                {
                    GUI.enabled = false;
                    Texture errorTexture = EditorGUIUtility.IconContent("CollabConflict").image;
                    GUIContent errorLabel = new GUIContent("", errorTexture, "One or more keys are null or duplicated.");
                    GUI.Button(errorRect, errorLabel, EditorStyles.iconButton);
                    GUI.enabled = defaultCache.guiEnabled;
                }
                
                // count header content
                GUI.enabled = false;
                GUI.Button(countRect, property.hasMultipleDifferentValues ? "- Items" : $"{serializedDictionary.arraySize} Items", GgGUI.SmallTextStyle);
                GUI.enabled = defaultCache.guiEnabled;
                
                // add element header content
                Texture plusTexture = EditorGUIUtility.IconContent("Toolbar Plus").image;
                GUIContent addLabel = new GUIContent("", plusTexture, GUI.enabled ? "Add new empty/default entry." : "");
                if (GUI.Button(addRect, addLabel, EditorStyles.iconButton))
                {
                    target.EditorInternal_AddEmpty();
                    InitialiseDictionaryNextFrame(property);
                }

                // initialise key error check
                keyError = false;
                
                // hide content if not expanded (but still check for warnings)
                if (!property.isExpanded)
                {
                    for (int i = 0; i < serializedDictionary.arraySize; i++)
                    {
                        // key error check
                        bool thisKeyError = target.EditorInternal_IsNullOrDuplicate(i);
                        keyError = keyError || thisKeyError;
                    }
                    return;
                }
                
                // draw the column headers
                GUI.enabled = true;
                columnHeader.OnGUI(subHeaderRect, 0);
                GUI.enabled = defaultCache.guiEnabled;
                
                // draw elements: empty 
                if (serializedDictionary.arraySize <= 0)
                {
                    GUI.enabled = false;
                    GgGUI.StringField(emptyLabelRect, GUIContent.none, "SerializedDictionary is Empty", property.hasMultipleDifferentValues);
                    GUI.enabled = defaultCache.guiEnabled;
                    return;
                }
                
                // draw elements: filled
                SerializedProperty arrayElement = serializedDictionary.GetArrayElementAtIndex(0);
                for (int i = 0; i < serializedDictionary.arraySize; i++)
                {
                    // key error check
                    bool thisKeyError = target.EditorInternal_IsNullOrDuplicate(i);
                    keyError = keyError || thisKeyError;
                    
                    // get reference to key/value SerializeFields
                    SerializedProperty key = arrayElement.FindPropertyRelative("key");
                    SerializedProperty value = arrayElement.FindPropertyRelative("value");
                
                    // get key/value rects
                    float keyHeight = key == null ? 0 : PropertyDrawerExtensions.TryFindCustomPropertyDrawer(key, out PropertyDrawer keyDrawer)
                        ? keyDrawer.GetPropertyHeight(key, GUIContent.none)
                        : EditorGUI.GetPropertyHeight(key, GUIContent.none, true);
                    float valueHeight = value == null ? 0 : PropertyDrawerExtensions.TryFindCustomPropertyDrawer(value, out PropertyDrawer valueDrawer)
                        ? valueDrawer.GetPropertyHeight(value, GUIContent.none)
                        : EditorGUI.GetPropertyHeight(value, GUIContent.none, true);
                    Rect keyRect = new Rect(contentRect.x, contentRect.y, columns[0].width - (standardSpacing * 1.5f), keyHeight);
                    Rect valueRect = new Rect(keyRect.xMax + standardSpacing, contentRect.y, columns[1].width - (standardSpacing * 0.5f), valueHeight);
                    Rect removeRect = new Rect(contentRect.xMax - (singleLineHeight + standardSpacing), contentRect.y + standardSpacing, singleLineHeight, singleLineHeight);
                    
                    // draw key/value properties (highlighting duplicates)
                    GUI.backgroundColor = thisKeyError ? InspectorExtensions.redColor : defaultCache.guiBackgroundColor;
                    EditorGUI.BeginChangeCheck();
                    GgGUI.VarField(keyRect, key, GUIContent.none);
                    if (EditorGUI.EndChangeCheck()) { InitialiseDictionaryNextFrame(property); }
                    GUI.backgroundColor = defaultCache.guiBackgroundColor;
                    EditorGUI.BeginChangeCheck();
                    GgGUI.VarField(valueRect, value, GUIContent.none);
                    if (EditorGUI.EndChangeCheck()) { InitialiseDictionaryNextFrame(property); }
                    
                    // remove button
                    Texture removeTexture = EditorGUIUtility.IconContent("P4_DeletedLocal").image;
                    GUIContent removeLabel = new GUIContent("", removeTexture, GUI.enabled ? "Remove entry." : "");
                    if (GUI.Button(removeRect, removeLabel, EditorStyles.iconButton))
                    {
                        target.EditorInternal_RemoveAt(i);
                        InitialiseDictionaryNextFrame(property);
                    }
                    
                    // update rect and move to next if not last item
                    contentRect.y += keyValueHeight + standardSpacing;
                    if (i == serializedDictionary.arraySize - 1) { continue; }
                    arrayElement.Next(false);
                }
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions

        private async void InitialiseDictionaryNextFrame(SerializedProperty property)
        {
            if (await GgTask.WaitUntilNextFrame() != TaskResultType.Complete) { return; }

            if (property == null) { return; }
            ASerializedDictionary target = property.GetValue<ASerializedDictionary>();
            target?.Initialise();
        }
        
        private void InitialiseTable(Rect position, GUIContent label)
        {
            Rect labelPosition = GetLabelPosition(position, label);
            Rect fieldPosition = GetFieldPosition(position, label);
            
            columns = new []
            {
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Key", "The key in the key/value pair."),
                    width = labelPosition.width,
                    minWidth = labelPosition.width * 0.5f,
                    maxWidth = labelPosition.width * 1.5f,
                    autoResize = false,
                    canSort = GUI.enabled,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Value", "The value in the key/value pair."),
                    width = fieldPosition.width - singleLineHeight,
                    autoResize = true,
                    canSort = GUI.enabled,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = GUIContent.none,
                    width = singleLineHeight * 0.5f,
                    minWidth = singleLineHeight * 0.5f,
                    maxWidth = singleLineHeight * 0.5f,
                    autoResize = false,
                    canSort = false,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                }
            };
            columnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = columnHeaderHeight,
            };
            
            // initialise size
            columnHeader.ResizeToFit();
            
            // callbacks
            columnHeader.columnSettingsChanged -= OnColumnSettingsChanged;
            columnHeader.columnSettingsChanged += OnColumnSettingsChanged;
            columnHeader.sortingChanged -= OnSortingChanged;
            columnHeader.sortingChanged += OnSortingChanged;
        }
        
        private void OnColumnSettingsChanged(int columnIndex)
        {
            columnHeader.ResizeToFit();
        }
        
        private void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            if (cachedProperty == null) { return; }
            ASerializedDictionary target = cachedProperty.GetValue<ASerializedDictionary>();
            target.SortBy = multiColumnHeader.sortedColumnIndex;
            target.IsSortedAscending = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);
        }

        #endregion
        
    } // class end
}
#endif
