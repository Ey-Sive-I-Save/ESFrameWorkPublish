#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [InitializeOnLoad]
    public static class GgGUI
    {
        #region Variables
        
        public static float singleLineHeight => EditorGUIUtility.singleLineHeight;
        public static float standardSpacing => EditorGUIUtility.standardVerticalSpacing;
        public static float labelWidth => EditorGUIUtility.labelWidth;
        public static float miniFieldWidth = EditorGUIUtility.fieldWidth;
        public static float currentIndent => EditorExtensions.currentIndent;
        public static float currentWidth => EditorGUIUtility.currentViewWidth;
        
        private const string packageRefName = "GgCore";
        private const string relativePath = "/Editor/Icons/CustomGUI";
        private static Dictionary<string, Texture> icons;
        
        /// <summary>
        /// Called during <see cref="GgEditorCallbacks.OnSafeInitialize"/> after GgCore CustomGUI icons have been cached.
        /// </summary>
        public static event Action onCacheGgGUIIcons;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region GUIStyle

        public static GUIStyle SubLabelStyle
        {
            get
            {
                GUIStyle subLabelStyle = new GUIStyle();
                subLabelStyle.normal.textColor = InspectorExtensions.textDisabledColor;
                subLabelStyle.fontSize = 10;
                subLabelStyle.alignment = TextAnchor.UpperLeft;

                return subLabelStyle;
            }
        }

        public static GUIStyle SmallTextStyle
        {
            get
            {
                GUIStyle smallTextStyle = new GUIStyle();
                smallTextStyle.normal.textColor = InspectorExtensions.textNormalColor;
                smallTextStyle.fontSize = 10;
                smallTextStyle.alignment = TextAnchor.MiddleCenter;

                return smallTextStyle;
            }
        }

        public static GUIStyle StealthButtonStyle
        {
            get
            {
                GUIStyle buttonStyle = new GUIStyle();
                buttonStyle.alignment = TextAnchor.MiddleCenter;
                buttonStyle.fontSize = 10;
                buttonStyle.normal.textColor = InspectorExtensions.textDisabledColor;
            
                return buttonStyle;
            }
        }
        
        public static GUIStyle DropdownButtonStyle
        {
            get
            {
                GUIStyle dropdownStyle = new GUIStyle();
                dropdownStyle.fontSize = 12;
                dropdownStyle.fontStyle = FontStyle.Normal;
                dropdownStyle.normal.textColor = InspectorExtensions.textNormalColor;
                dropdownStyle.hover.textColor = InspectorExtensions.textNormalColor;
                dropdownStyle.active.textColor = InspectorExtensions.textNormalColor;

                return dropdownStyle;
            }
        }
        
        public static GUIStyle DropdownButtonStyleBold
        {
            get
            {
                GUIStyle dropdownStyle = new GUIStyle();
                dropdownStyle.fontSize = 12;
                dropdownStyle.fontStyle = FontStyle.Bold;
                dropdownStyle.normal.textColor = InspectorExtensions.textNormalColor;
                dropdownStyle.hover.textColor = InspectorExtensions.textNormalColor;
                dropdownStyle.active.textColor = InspectorExtensions.textNormalColor;

                return dropdownStyle;
            }
        }
        
        public static GUIStyle ButtonStyle
        {
            get
            {
                GUIStyle buttonStyle = new GUIStyle();
        
                buttonStyle.fontSize = 9;
                buttonStyle.alignment = TextAnchor.MiddleCenter;
                buttonStyle.normal.textColor = InspectorExtensions.textNormalColor;
                buttonStyle.hover.textColor = InspectorExtensions.textNormalColor;
                buttonStyle.active.textColor = InspectorExtensions.textNormalColor;
                buttonStyle.normal.background = InspectorExtensions.CreateTexture(20, 20, 1, true, InspectorExtensions.blankColor, InspectorExtensions.blankColor);
                buttonStyle.hover.background = InspectorExtensions.CreateTexture(20, 20, 1, true, InspectorExtensions.buttonHoverColor, InspectorExtensions.buttonHoverBorderColor);
                buttonStyle.active.background = InspectorExtensions.CreateTexture(20, 20, 1, true, InspectorExtensions.buttonActiveColor, InspectorExtensions.buttonActiveBorderColor);
            
                return buttonStyle;
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Dropdown Header

        /// <summary>
        /// Get all position rects for a foldout header, from the property's position rect.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="label"></param>
        /// <param name="foldoutPositions"></param>
        public static void GetFoldoutPositionRects(Rect position, GUIContent label, out GgFoldoutPositions foldoutPositions, bool subHeader = false)
        {
            foldoutPositions = new GgFoldoutPositions();
            
            // outline & background
            foldoutPositions.outline = new Rect(position.x, position.y - (standardSpacing * 0.5f), position.width, position.height + standardSpacing);
            foldoutPositions.background = new Rect(position.x + (standardSpacing * 0.5f), position.y, position.width - standardSpacing, position.height);
            
            // full header
            foldoutPositions.header = new Rect(foldoutPositions.background.x + (standardSpacing * 0.5f), foldoutPositions.background.y + standardSpacing, foldoutPositions.background.width - standardSpacing, singleLineHeight);
            
            // split header
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float thisLabelWidth = hideLabel ? 0 : labelWidth - standardSpacing;
            float thisFieldWidth = hideLabel ? foldoutPositions.header.width : foldoutPositions.header.width - (thisLabelWidth + (standardSpacing * 2));
            foldoutPositions.label = new Rect(foldoutPositions.header.x, foldoutPositions.header.y, thisLabelWidth, foldoutPositions.header.height);
            foldoutPositions.field = new Rect(foldoutPositions.header.xMax - thisFieldWidth, foldoutPositions.header.y - (standardSpacing * 0.5f), thisFieldWidth, foldoutPositions.header.height);

            if (subHeader)
            {
                // subheader
                foldoutPositions.subHeader = new Rect(foldoutPositions.header.x, foldoutPositions.header.yMax + standardSpacing, foldoutPositions.header.width, singleLineHeight);
                foldoutPositions.subHeaderBackground = new Rect(foldoutPositions.background.x, foldoutPositions.subHeader.y - 2, foldoutPositions.background.width, foldoutPositions.subHeader.height + 4);
                
                // content
                foldoutPositions.content = new Rect(foldoutPositions.header.x, foldoutPositions.subHeader.yMax + standardSpacing, foldoutPositions.header.width, singleLineHeight);
            }
            else
            {
                // subheader
                foldoutPositions.subHeader = new Rect(foldoutPositions.header.x, foldoutPositions.header.yMax + standardSpacing, foldoutPositions.header.width, 0);
                foldoutPositions.subHeaderBackground = new Rect(foldoutPositions.background.x, foldoutPositions.subHeader.y - 2, foldoutPositions.background.width, foldoutPositions.subHeader.height + 4);
                
                // content
                foldoutPositions.content = new Rect(foldoutPositions.header.x, foldoutPositions.header.yMax + standardSpacing, foldoutPositions.header.width, singleLineHeight);
            }
        }
        
        /// <summary>
        /// Draws a foldout header given foldout position, a SerializedProperty and a label.
        /// </summary>
        /// <param name="foldoutPositions"></param>
        /// <param name="property"></param>
        /// <param name="label"></param>
        /// <param name="fullWidth"></param>
        /// <param name="bold"></param>
        public static void DrawDropdownHeader(GgFoldoutPositions foldoutPositions, SerializedProperty property, GUIContent label, bool fullWidth = true, bool bold = true, bool subHeader = false)
        {
            // draw background positions
            EditorGUI.DrawRect(foldoutPositions.outline, InspectorExtensions.backgroundShadowColor);
            EditorGUI.DrawRect(foldoutPositions.background, InspectorExtensions.backgroundInfoBoxColor);
            if (subHeader)
            {
                EditorGUI.DrawRect(foldoutPositions.subHeaderBackground, InspectorExtensions.backgroundNormalColor);
            }
            
            // get foldout label content
            string icon = "d_IN_foldout";
            if (property.isExpanded) { icon = "d_IN_foldout_on"; }
            Texture iconTexture = EditorGUIUtility.IconContent(icon).image;
            GUIContent iconLabel = new GUIContent(label.text, iconTexture, label.tooltip);
            
            // get label position rect
            Rect iconLabelPosition;
            if (fullWidth)
            {
                iconLabelPosition = foldoutPositions.header;
            }
            else
            {
                iconLabelPosition = foldoutPositions.label;
                iconLabelPosition.width += singleLineHeight;
            }
            
            // draw label as clickable button
            bool guiEnabled = GUI.enabled;
            GUI.enabled = true;
            if (GUI.Button(iconLabelPosition, iconLabel, bold ? DropdownButtonStyleBold : DropdownButtonStyle))
            {
                property.isExpanded = !property.isExpanded;
            }
            GUI.enabled = guiEnabled;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Custom Icons

        static GgGUI()
        {
            GgEditorCallbacks.OnSafeInitialize -= Initialisation;
            GgEditorCallbacks.OnSafeInitialize += Initialisation;
        }

        private static void Initialisation()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            
            // get all custom icons
            Dictionary<string, Texture> dictionary = new Dictionary<string, Texture>();
            List<Texture> fileIcons = EditorExtensions.GetAllAssetsByType<Texture>(new []{ filePath });
            foreach (Texture fileIcon in fileIcons)
            {
                dictionary.TryAdd(Path.GetFileNameWithoutExtension(fileIcon.name), fileIcon);
            }
            icons = new Dictionary<string, Texture>(dictionary);
            onCacheGgGUIIcons?.Invoke();
        }

        /// <summary>
        /// Try to add custom icons to the GgGUI icons list. For best results, subscribe to the GgGUI.<see cref="onCacheGgGUIIcons"/>
        /// action using a script that implements <see cref="InitializeOnLoadAttribute"/>.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="icon"></param>
        public static bool TryAddCustomIcon(string name, Texture icon)
        {
            if (icons == null) { Initialisation(); }
            icons ??= new Dictionary<string, Texture>();
            
            return icon != null && icons.TryAdd(name, icon);
        }

        /// <summary>
        /// Fetch the image texture of a GgGUI icon with the given name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Texture GetIcon(string name)
        {
            if (icons == null) { Initialisation(); }
            icons ??= new Dictionary<string, Texture>();
            
            return icons.TryGetValue(name, out Texture value) ? value : null;
        }

        /// <summary>
        /// Fetch the GUIContent version of a GgGUI icon with the given name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tooltip"></param>
        /// <returns></returns>
        public static GUIContent IconContent(string name, string tooltip = "")
        {
            return new GUIContent()
            {
                text = "",
                image = GetIcon(name),
                tooltip = tooltip,
            };
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region TryAddMultiEditIcon

        public static GUIContent TryGetMultiEditIcon(GUIContent label, bool propertyHasMultipleDifferentValues)
        {
            string labelText = propertyHasMultipleDifferentValues && !string.IsNullOrEmpty(label.text)
                ? label.text + " \u25E6"
                : label.text;
            
            return new GUIContent(labelText, label.tooltip);
        }
        
        private static void TryAddMultiEditIcon(ref GUIContent label, bool propertyHasMultipleDifferentValues)
        {
            // add icon if label text exists and property represents multiple values
            label.text = propertyHasMultipleDifferentValues && !string.IsNullOrEmpty(label.text)
                ? label.text + " \u25E6"
                : label.text;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region CustomPropertyField

        /// <summary>
        /// Draws a custom PropertyField if one exists
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void CustomPropertyField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (PropertyDrawerExtensions.TryFindCustomPropertyDrawer(property, out PropertyDrawer customDrawer))
            {
                customDrawer.OnGUI(position, property, label);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region VarField

        /// <summary>
        /// Creates a field of type var for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void VarField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null) { return; }
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    Toggle(position, property, label);
                    return;
                
                case SerializedPropertyType.Integer:
                    IntField(position, property, label);
                    return;
                
                case SerializedPropertyType.Float:
                    FloatField(position, property, label);
                    return;
                
                case SerializedPropertyType.String:
                    TextField(position, property, label);
                    return;
                
                case SerializedPropertyType.Hash128:
                    Hash128Field(position, property, label);
                    return;
                
                case SerializedPropertyType.ObjectReference:
                    ObjectField(position, property, label);
                    return;
                
                case SerializedPropertyType.Enum:
                    EnumField(position, property, label);
                    return;
                
                case SerializedPropertyType.LayerMask:
                    LayerField(position, property, label);
                    return;
                
                case SerializedPropertyType.Color:
                    ColorField(position, property, label);
                    return;
                
                case SerializedPropertyType.Gradient:
                    GradientField(position, property, label);
                    return;
                
                case SerializedPropertyType.AnimationCurve:
                    CurveField(position, property, label, new Color(000, 179, 000, 255));
                    return;
                
                case SerializedPropertyType.Vector2:
                    Vector2Field(position, property, label);
                    return;
                
                case SerializedPropertyType.Vector2Int:
                    Vector2IntField(position, property, label);
                    return;
                
                case SerializedPropertyType.Vector3:
                    Vector3Field(position, property, label);
                    return;
                
                case SerializedPropertyType.Vector3Int:
                    Vector3IntField(position, property, label);
                    return;
                
                case SerializedPropertyType.Vector4:
                    Vector4Field(position, property, label);
                    return;
                
                case SerializedPropertyType.Quaternion:
                    QuaternionField(position, property, label);
                    return;
                
                case SerializedPropertyType.Rect:
                    RectField(position, property, label);
                    return;
                
                case SerializedPropertyType.RectInt:
                    RectIntField(position, property, label);
                    return;
                
                case SerializedPropertyType.Bounds:
                    BoundsField(position, property, label);
                    return;
                
                case SerializedPropertyType.BoundsInt:
                    BoundsIntField(position, property, label);
                    return;
                
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.ExposedReference:
                case SerializedPropertyType.FixedBufferSize:
                case SerializedPropertyType.ManagedReference:
                default: // not supported: PropertyField used if not supported type
                    EditorGUI.PropertyField(position, property, label);
                    return;
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region PrefixLabelField

        /// <summary>
        /// Creates a prefix label field
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Prefix Label to display.</param>
        public static void PrefixLabelField(Rect position, SerializedProperty property, GUIContent label)
        {
            PrefixLabelField(position, label, property.hasMultipleDifferentValues);
        }
        
        /// <summary>
        /// Creates a string field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Prefix Label to display.</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        public static void PrefixLabelField(Rect position, GUIContent label, bool propertyHasMultipleDifferentValues = false)
        {
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            EditorGUI.PrefixLabel(position, label);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Toggle

        /// <summary>
        /// Creates a toggle field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void Toggle(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Toggle(position, label, property.boolValue, out bool newValue, property.hasMultipleDifferentValues))
            {
                property.boolValue = newValue;
            }
        }

        /// <summary>
        /// Creates a toggle field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Toggle(Rect position, GUIContent label, bool inputValue, out bool outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            
            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Toggle(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region ToggleLeft
        
        /// <summary>
        /// Creates a toggle left field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void ToggleLeft(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ToggleLeft(position, label, property.boolValue, out bool newValue, property.hasMultipleDifferentValues))
            {
                property.boolValue = newValue;
            }
        }

        /// <summary>
        /// Creates a toggle field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool ToggleLeft(Rect position, GUIContent label, bool inputValue, out bool outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.ToggleLeft(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region IntField

        /// <summary>
        /// Creates an int field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void IntField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (IntField(position, label, property.intValue, out int newValue, property.hasMultipleDifferentValues))
            {
                property.intValue = newValue;
            }
        }

        /// <summary>
        /// Creates an int field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool IntField(Rect position, GUIContent label, int inputValue, out int outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.IntField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region MaskField

        /// <summary>
        /// Creates a mask field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="displayNames">Names to display on the pop up.</param>
        public static void MaskField(Rect position, SerializedProperty property, GUIContent label, string[] displayNames)
        {
            if (MaskField(position, label, property.intValue, out int newValue, displayNames, property.hasMultipleDifferentValues))
            {
                property.intValue = newValue;
            }
        }

        /// <summary>
        /// Creates a mask field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="displayNames">Names to display on the pop up</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool MaskField(Rect position, GUIContent label, int inputValue, out int outputValue, string[] displayNames, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.MaskField(position, label, inputValue, displayNames, EditorStyles.popup);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region FloatField
        
        /// <summary>
        /// Creates a float field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void FloatField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (FloatField(position, label, property.floatValue, out float newValue, property.hasMultipleDifferentValues))
            {
                property.floatValue = newValue;
            }
        }

        /// <summary>
        /// Creates a float field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool FloatField(Rect position, GUIContent label, float inputValue, out float outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.FloatField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region IntSlider

        /// <summary>
        /// Creates a slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        public static void IntSlider(Rect position, SerializedProperty property, GUIContent label, Vector2Int minMax)
        {
            if (IntSlider(position, label, property.intValue, out int newValue, minMax, property.hasMultipleDifferentValues))
            {
                property.intValue = newValue;
            }
        }

        /// <summary>
        /// Creates a slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool IntSlider(Rect position, GUIContent label, int inputValue, out int outputValue, Vector2Int minMax, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.IntSlider(position, label, inputValue, minMax.x, minMax.y);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Slider

        /// <summary>
        /// Creates a slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        public static void Slider(Rect position, SerializedProperty property, GUIContent label, Vector2 minMax)
        {
            if (Slider(position, label, property.floatValue, out float newValue, minMax, property.hasMultipleDifferentValues))
            {
                property.floatValue = newValue;
            }
        }

        /// <summary>
        /// Creates a slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Slider(Rect position, GUIContent label, float inputValue, out float outputValue, Vector2 minMax, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Slider(position, label, inputValue, minMax.x, minMax.y);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region TextField
        
        /// <summary>
        /// Creates a text field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void TextField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (TextField(position, label, property.stringValue, out string newValue, property.hasMultipleDifferentValues))
            {
                property.stringValue = newValue;
            }
        }

        /// <summary>
        /// Creates a text field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool TextField(Rect position, GUIContent label, string inputValue, out string outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.TextField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region StringField
        
        /// <summary>
        /// Creates a string field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void StringField(Rect position, SerializedProperty property, GUIContent label)
        {
            StringField(position, label, property.stringValue, property.hasMultipleDifferentValues);
        }

        /// <summary>
        /// Creates a string field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="greyedOut">Optional value to show the string as readonly</param>
        public static void StringField(Rect position, GUIContent label, string inputValue, bool propertyHasMultipleDifferentValues = false, bool greyedOut = false)
        {
            // calculate positions
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float labelWidth = hideLabel ? 0 : EditorGUIUtility.labelWidth - (EditorExtensions.currentIndent - EditorGUIUtility.standardVerticalSpacing);
            float fieldWidth = hideLabel ? position.width : position.width - (labelWidth + EditorGUIUtility.standardVerticalSpacing);
            Rect labelPosition = new Rect(position.x, position.y, labelWidth, EditorGUIUtility.singleLineHeight);
            Rect fieldPosition = new Rect(position.xMax - fieldWidth, position.y, fieldWidth, position.height);
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            
            // draw string (greyed out?)
            if (!hideLabel) { EditorGUI.LabelField(labelPosition, label); }
            bool guiEnabled = GUI.enabled;
            GUI.enabled = guiEnabled && !greyedOut;
            EditorGUI.LabelField(fieldPosition, propertyHasMultipleDifferentValues ? "\u2014" : inputValue);
            GUI.enabled = guiEnabled;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region TagField
        
        /// <summary>
        /// Creates a tag field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void TagField(Rect position, SerializedProperty property, GUIContent label)
        {
            TagField(position, label, property.stringValue, property.hasMultipleDifferentValues);
        }

        /// <summary>
        /// Creates a tag field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        public static void TagField(Rect position, GUIContent label, string inputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // calculate positions
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float labelWidth = hideLabel ? 0 : EditorGUIUtility.labelWidth;
            float fieldWidth = hideLabel ? position.width : position.width - (labelWidth + EditorGUIUtility.standardVerticalSpacing);
            Rect labelPosition = new Rect(position.x, position.y, labelWidth, EditorGUIUtility.singleLineHeight);
            Rect fieldPosition = new Rect(position.xMax - fieldWidth, position.y, fieldWidth, position.height);
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            
            // draw prefix label
            if (!hideLabel) { EditorGUI.LabelField(labelPosition, label); }
            
            // draw tag
            GUI.enabled = false;
            if (propertyHasMultipleDifferentValues)
            {
                float tagWidth = StringExtensions.GetStringWidth("-") + 6;
                Rect tagPosition = new Rect(position.xMax - fieldWidth, position.y, Mathf.Min(fieldWidth, tagWidth), position.height);
                GUI.Button(tagPosition, new GUIContent("-", label.tooltip));
            }
            else if (string.IsNullOrEmpty(inputValue))
            {
                EditorGUI.LabelField(fieldPosition, "n/a");
            }
            else
            {
                float tagWidth = StringExtensions.GetStringWidth(inputValue) + 6;
                Rect tagPosition = new Rect(position.xMax - fieldWidth, position.y, Mathf.Min(fieldWidth, tagWidth), position.height);
                GUI.Button(tagPosition, new GUIContent(inputValue, label.tooltip));
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region TextArea
        
        /// <summary>
        /// Creates a text area field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void TextArea(Rect position, SerializedProperty property, GUIContent label)
        {
            if (TextArea(position, label, property.stringValue, out string newValue, property.hasMultipleDifferentValues))
            {
                property.stringValue = newValue;
            }
        }

        /// <summary>
        /// Creates a text field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool TextArea(Rect position, GUIContent label, string inputValue, out string outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;

            // calculate positions
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float labelWidth = hideLabel ? 0 : EditorGUIUtility.labelWidth;
            float textWidth = hideLabel ? position.width : position.width - (labelWidth + EditorGUIUtility.standardVerticalSpacing);
            Rect labelPosition = new Rect(position.x, position.y, labelWidth, EditorGUIUtility.singleLineHeight);
            Rect textPosition = new Rect(position.xMax - textWidth, position.y, textWidth, position.height);
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            
            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            if (!hideLabel) { EditorGUI.PrefixLabel(labelPosition, label); }
            outputValue = EditorGUI.TextArea(textPosition, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Hash128Field
        
        /// <summary>
        /// Creates a hash128 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void Hash128Field(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Hash128Field(position, label, property.hash128Value, out Hash128 newValue, property.hasMultipleDifferentValues))
            {
                property.hash128Value = newValue;
            }
        }

        /// <summary>
        /// Creates a hash128 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Hash128Field(Rect position, GUIContent label, Hash128 inputValue, out Hash128 outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = Hash128.Parse(EditorGUI.TextField(position, label, inputValue.ToString()));
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region ObjectField
        
        /// <summary>
        /// Creates a object field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="allowSceneObjects">Optional value: if true scene objects will be allowed.</param>
        public static void ObjectField(Rect position, SerializedProperty property, GUIContent label, bool allowSceneObjects = true)
        {
            // if type can be found ... 
            if (property.TryGetObjectType(out Type type))
            {
                // ... draw using GgGUI ObjectField
                if (ObjectField(position, label, property.objectReferenceValue, out Object newValue, type, property.hasMultipleDifferentValues, allowSceneObjects))
                {
                    property.objectReferenceValue = newValue;
                }
            }
            else
            {
                // ... else draw using default unity
                EditorGUI.PropertyField(position, property, label);
            }
        }

        /// <summary>
        /// Creates a object field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="objectType">The type of the objects that can be assigned</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="allowSceneObjects">Optional value: if true scene objects will be allowed.</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool ObjectField(Rect position, GUIContent label, Object inputValue, out Object outputValue, Type objectType, bool propertyHasMultipleDifferentValues = false, bool allowSceneObjects = true)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.ObjectField(position, label, inputValue, objectType, allowSceneObjects);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region EnumField
        
        /// <summary>
        /// Creates an enum field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void EnumField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (EnumField(position, label, property.intValue, out int newValue, property.enumDisplayNames, property.hasMultipleDifferentValues))
            {
                property.intValue = newValue;
            }
        }

        /// <summary>
        /// Creates an enum field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="displayNames">Display-friendly names of enumeration of an enum property</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool EnumField(Rect position, GUIContent label, int inputValue, out int outputValue, string[] displayNames, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            // extra step to convert to valid input
            GUIContent[] guiContents = new GUIContent[displayNames.Length];
            for (var index = 0; index < displayNames.Length; index++)
            {
                guiContents[index] = new GUIContent(displayNames[index]);
            }
            
            outputValue = Mathf.Max(0, Array.IndexOf(displayNames, inputValue));
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Popup(position, label, inputValue, guiContents);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region LayerField

        /// <summary>
        /// Creates a layer field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void LayerField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (LayerField(position, label, property.intValue, out LayerMask newValue, property.hasMultipleDifferentValues))
            {
                property.intValue = newValue;
            }
        }

        /// <summary>
        /// Creates a layer field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool LayerField(Rect position, GUIContent label, LayerMask inputValue, out LayerMask outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.LayerField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region ColorField
        
        /// <summary>
        /// Creates a color field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="showEyedropper">If true, the color picker should show the eyedropper control. If false, don't show it.</param>
        /// <param name="showAlpha">If true, allow the user to set an alpha value for the color. If false, hide the alpha component.</param>
        /// <param name="hdr">Optional value: if true, a hdr version will be shown</param>
        public static void ColorField(Rect position, SerializedProperty property, GUIContent label, bool showEyedropper = true, bool showAlpha = true, bool hdr = false)
        {
            if (ColorField(position, label, property.colorValue, out Color newValue, property.hasMultipleDifferentValues, showEyedropper, showAlpha, hdr))
            {
                property.colorValue = newValue;
            }
        }

        /// <summary>
        /// Creates a color field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="showEyedropper">If true, the color picker should show the eyedropper control. If false, don't show it.</param>
        /// <param name="showAlpha">If true, allow the user to set an alpha value for the color. If false, hide the alpha component.</param>
        /// <param name="hdr">Optional value: if true, a hdr version will be shown</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool ColorField(Rect position, GUIContent label, Color inputValue, out Color outputValue, bool propertyHasMultipleDifferentValues = false, bool showEyedropper = true, bool showAlpha = true, bool hdr = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.ColorField(position, label, inputValue, showEyedropper, showAlpha, hdr);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GradientField
        
        /// <summary>
        /// Creates a gradient field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="hdr">Optional value: if true, a hdr version will be shown</param>
        public static void GradientField(Rect position, SerializedProperty property, GUIContent label, bool hdr = false)
        {
            // extra step to get gradient value
            System.Reflection.PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty("gradientValue", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Gradient gradientValue = propertyInfo?.GetValue(property, null) as Gradient;
            
            if (GradientField(position, label, gradientValue, out Gradient newValue, property.hasMultipleDifferentValues, hdr))
            {
                propertyInfo?.SetValue(property, newValue);
            }
        }

        /// <summary>
        /// Creates a gradient field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="hdr">Optional value: if true, a hdr version will be shown</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool GradientField(Rect position, GUIContent label, Gradient inputValue, out Gradient outputValue, bool propertyHasMultipleDifferentValues = false, bool hdr = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            
            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.GradientField(position, label, inputValue, hdr);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region CurveField

        /// <summary>
        /// Creates a curve field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="lineColor">The color to show the curve with</param>
        /// <param name="ranges">Optional rectangle that the curve is restrained within</param>
        public static void CurveField(Rect position, SerializedProperty property, GUIContent label, Color lineColor, Rect ranges = new Rect())
        {
            if (CurveField(position, label, property.animationCurveValue, out AnimationCurve newValue, lineColor, ranges, property.hasMultipleDifferentValues))
            {
                property.animationCurveValue = newValue;
            }
        }

        /// <summary>
        /// Creates a curve field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="lineColor">The color to show the curve with</param>
        /// <param name="ranges">Optional rectangle that the curve is restrained within</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool CurveField(Rect position, GUIContent label, AnimationCurve inputValue, out AnimationCurve outputValue, Color lineColor, Rect ranges = new Rect(), bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.CurveField(position, label, inputValue, lineColor, ranges);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Vector2Field  [TODO - Individual fields]

        /// <summary>
        /// Creates a vector2 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void Vector2Field(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Vector2Field(position, label, property.vector2Value, out Vector2 newValue, property.hasMultipleDifferentValues))
            {
                property.vector2Value = newValue;
            }
        }

        /// <summary>
        /// Creates a vector2 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Vector2Field(Rect position, GUIContent label, Vector2 inputValue, out Vector2 outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Vector2Field(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Vector2IntField  [TODO - Individual fields]

        /// <summary>
        /// Creates a vector2Int field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void Vector2IntField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Vector2IntField(position, label, property.vector2IntValue, out Vector2Int newValue, property.hasMultipleDifferentValues))
            {
                property.vector2IntValue = newValue;
            }
        }

        /// <summary>
        /// Creates a vector2Int field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Vector2IntField(Rect position, GUIContent label, Vector2Int inputValue, out Vector2Int outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Vector2IntField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region MinMaxSlider

        /// <summary>
        /// Creates a min max slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        public static void MinMaxSlider(Rect position, SerializedProperty property, GUIContent label, Vector2 minMax)
        {
            if (MinMaxSlider(position, label, property.vector2Value, out Vector2 newValue, minMax, property.hasMultipleDifferentValues))
            {
                property.vector2Value = newValue;
            }
        }

        /// <summary>
        /// Creates a min max slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool MinMaxSlider(Rect position, GUIContent label, Vector2 inputValue, out Vector2 outputValue, Vector2 minMax, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            
            // calculate label/field positions
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float thisLabelWidth = hideLabel ? 0 : labelWidth;
            float fieldWidth = hideLabel ? position.width : position.width - (thisLabelWidth + standardSpacing);
            Rect labelPosition = new Rect(position.x, position.y, thisLabelWidth, singleLineHeight);
            Rect fieldPosition = new Rect(position.xMax - fieldWidth, position.y, fieldWidth, singleLineHeight);

            // calculate sub-field/slider positions
            float sliderPositionX = fieldPosition.x + (miniFieldWidth + standardSpacing);
            float sliderWidth = fieldPosition.width - ((miniFieldWidth + standardSpacing) * 2);
            Rect fieldAPosition = new Rect(fieldPosition.x, fieldPosition.y, miniFieldWidth, fieldPosition.height);
            Rect fieldBPosition = new Rect(fieldPosition.xMax - miniFieldWidth, fieldPosition.y, miniFieldWidth, fieldPosition.height);
            Rect sliderPosition = new Rect(sliderPositionX, fieldPosition.y, sliderWidth, fieldPosition.height);
            
            // label
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            if (!hideLabel) { EditorGUI.PrefixLabel(labelPosition, label); }
            
            // min max slider
            float offset = (minMax.y - minMax.x) * 0.3f;
            float min = propertyHasMultipleDifferentValues ? minMax.x + offset : inputValue.x;
            float max = propertyHasMultipleDifferentValues ? minMax.y - offset : inputValue.y;
            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(sliderPosition, new GUIContent(), ref min, ref max, minMax.x, minMax.y);
            bool sliderChanged = EditorGUI.EndChangeCheck();
            
            // input field: min
            bool minFieldChanged = FloatField(fieldAPosition, new GUIContent(), min, out min, propertyHasMultipleDifferentValues);
                
            // input field: max
            bool maxFieldChanged = FloatField(fieldBPosition, new GUIContent(), max, out max, propertyHasMultipleDifferentValues);
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            outputValue = new Vector2(min, max);
            return sliderChanged || minFieldChanged || maxFieldChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region MinMaxSliderInt

        /// <summary>
        /// Creates a min max slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        public static void MinMaxSliderInt(Rect position, SerializedProperty property, GUIContent label, Vector2 minMax)
        {
            if (MinMaxSliderInt(position, label, property.vector2IntValue, out Vector2Int newValue, minMax, property.hasMultipleDifferentValues))
            {
                property.vector2IntValue = newValue;
            }
        }

        /// <summary>
        /// Creates a min max slider field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <param name="minMax">The min and max values used for the slider range.</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool MinMaxSliderInt(Rect position, GUIContent label, Vector2Int inputValue, out Vector2Int outputValue, Vector2 minMax, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            
            // calculate label/field positions
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float thisLabelWidth = hideLabel ? 0 : labelWidth;
            float fieldWidth = hideLabel ? position.width : position.width - (thisLabelWidth + standardSpacing);
            Rect labelPosition = new Rect(position.x, position.y, thisLabelWidth, singleLineHeight);
            Rect fieldPosition = new Rect(position.xMax - fieldWidth, position.y, fieldWidth, singleLineHeight);

            // calculate sub-field/slider positions
            float sliderPositionX = fieldPosition.x + (miniFieldWidth + standardSpacing);
            float sliderWidth = fieldPosition.width - ((miniFieldWidth + standardSpacing) * 2);
            Rect fieldAPosition = new Rect(fieldPosition.x, fieldPosition.y, miniFieldWidth, fieldPosition.height);
            Rect fieldBPosition = new Rect(fieldPosition.xMax - miniFieldWidth, fieldPosition.y, miniFieldWidth, fieldPosition.height);
            Rect sliderPosition = new Rect(sliderPositionX, fieldPosition.y, sliderWidth, fieldPosition.height);
            
            // label
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);
            if (!hideLabel) { EditorGUI.PrefixLabel(labelPosition, label); }
            
            // min max slider
            float offset = (minMax.y - minMax.x) * 0.3f;
            float min = propertyHasMultipleDifferentValues ? minMax.x + offset : inputValue.x;
            float max = propertyHasMultipleDifferentValues ? minMax.y - offset : inputValue.y;
            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.MinMaxSlider(sliderPosition, new GUIContent(), ref min, ref max, minMax.x, minMax.y);
            bool sliderChanged = EditorGUI.EndChangeCheck();
            
            // input field: min
            int minInt = (int)min;
            bool minFieldChanged = IntField(fieldAPosition, new GUIContent(), minInt, out minInt, propertyHasMultipleDifferentValues);
                
            // input field: max
            int maxInt = (int)max;
            bool maxFieldChanged = IntField(fieldBPosition, new GUIContent(), maxInt, out maxInt, propertyHasMultipleDifferentValues);
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            outputValue = new Vector2Int(minInt, maxInt);
            return sliderChanged || minFieldChanged || maxFieldChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Vector3Field  [TODO - Individual fields]

        /// <summary>
        /// Creates a vector3 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void Vector3Field(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Vector3Field(position, label, property.vector3Value, out Vector3 newValue, property.hasMultipleDifferentValues))
            {
                property.vector3Value = newValue;
            }
        }

        /// <summary>
        /// Creates a vector3 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Vector3Field(Rect position, GUIContent label, Vector3 inputValue, out Vector3 outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Vector3Field(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Vector3IntField  [TODO - Individual fields]

        /// <summary>
        /// Creates a vector3Int field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void Vector3IntField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Vector3IntField(position, label, property.vector3IntValue, out Vector3Int newValue, property.hasMultipleDifferentValues))
            {
                property.vector3IntValue = newValue;
            }
        }

        /// <summary>
        /// Creates a vector3Int field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Vector3IntField(Rect position, GUIContent label, Vector3Int inputValue, out Vector3Int outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Vector3IntField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Vector4Field  [TODO - Individual fields]

        /// <summary>
        /// Creates a vector4 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void Vector4Field(Rect position, SerializedProperty property, GUIContent label)
        {
            if (Vector4Field(position, label, property.vector4Value, out Vector4 newValue, property.hasMultipleDifferentValues))
            {
                property.vector4Value = newValue;
            }
        }

        /// <summary>
        /// Creates a vector4 field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool Vector4Field(Rect position, GUIContent label, Vector4 inputValue, out Vector4 outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.Vector4Field(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region QuaternionField  [TODO - Individual fields]

        /// <summary>
        /// Creates a quaternion field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void QuaternionField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (QuaternionField(position, label, property.quaternionValue, out Quaternion newValue, property.hasMultipleDifferentValues))
            {
                property.quaternionValue = newValue;
            }
        }

        /// <summary>
        /// Creates a quaternion field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool QuaternionField(Rect position, GUIContent label, Quaternion inputValue, out Quaternion outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            Vector4 vector4Value = new Vector4(inputValue.x, inputValue.y, inputValue.z, inputValue.w);
            vector4Value = EditorGUI.Vector4Field(position, label, vector4Value);
            outputValue = new Quaternion(vector4Value.x, vector4Value.y, vector4Value.z, vector4Value.w);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region RectField  [TODO - Individual fields]

        /// <summary>
        /// Creates a rect field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void RectField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (RectField(position, label, property.rectValue, out Rect newValue, property.hasMultipleDifferentValues))
            {
                property.rectValue = newValue;
            }
        }

        /// <summary>
        /// Creates a rect field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool RectField(Rect position, GUIContent label, Rect inputValue, out Rect outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.RectField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region RectIntField  [TODO - Individual fields]

        /// <summary>
        /// Creates a rectInt field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void RectIntField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (RectIntField(position, label, property.rectIntValue, out RectInt newValue, property.hasMultipleDifferentValues))
            {
                property.rectIntValue = newValue;
            }
        }

        /// <summary>
        /// Creates a rectInt field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool RectIntField(Rect position, GUIContent label, RectInt inputValue, out RectInt outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.RectIntField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region BoundsField  [TODO - Individual fields]

        /// <summary>
        /// Creates a bounds field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void BoundsField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (BoundsField(position, label, property.boundsValue, out Bounds newValue, property.hasMultipleDifferentValues))
            {
                property.boundsValue = newValue;
            }
        }

        /// <summary>
        /// Creates a bounds field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool BoundsField(Rect position, GUIContent label, Bounds inputValue, out Bounds outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.BoundsField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region BoundsIntField [TODO - Individual fields]

        /// <summary>
        /// Creates a boundsInt field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the property field.</param>
        /// <param name="property">The SerializedProperty to make the custom GUI for.</param>
        /// <param name="label">Label to display in front of the field.</param>
        public static void BoundsIntField(Rect position, SerializedProperty property, GUIContent label)
        {
            if (BoundsIntField(position, label, property.boundsIntValue, out BoundsInt newValue, property.hasMultipleDifferentValues))
            {
                property.boundsIntValue = newValue;
            }
        }

        /// <summary>
        /// Creates a boundsInt field for entering a user defined value [supports multi editing]
        /// </summary>
        /// <param name="position">Rectangle on the screen to use for the value field.</param>
        /// <param name="label">Label to display in front of the field.</param>
        /// <param name="inputValue">Input value for the field</param>
        /// <param name="outputValue">Output value of the field</param>
        /// <param name="propertyHasMultipleDifferentValues">Does this property represent multiple different values due to multi-object editing?</param>
        /// <returns>True if the value has been changed, false otherwise</returns>
        public static bool BoundsIntField(Rect position, GUIContent label, BoundsInt inputValue, out BoundsInt outputValue, bool propertyHasMultipleDifferentValues = false)
        {
            // cache defaults
            bool defaultShowMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = propertyHasMultipleDifferentValues;
            TryAddMultiEditIcon(ref label, propertyHasMultipleDifferentValues);

            outputValue = inputValue;
            EditorGUI.BeginChangeCheck();
            outputValue = EditorGUI.BoundsIntField(position, label, inputValue);
            bool hasChanged = EditorGUI.EndChangeCheck();
            
            // reset defaults
            EditorGUI.showMixedValue = defaultShowMixedValue;

            return hasChanged;
        }

        #endregion

    } // class end
}
#endif