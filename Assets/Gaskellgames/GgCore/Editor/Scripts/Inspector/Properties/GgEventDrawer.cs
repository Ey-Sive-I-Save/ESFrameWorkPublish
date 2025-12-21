#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    [CustomPropertyDrawer(typeof(GgEventBase), true)]
    public class GgEventDrawer : PropertyDrawer
    {
        #region variables

        private SerializedProperty instanceName;
        private SerializedProperty delay;
        private SerializedProperty verboseLogs;
        private SerializedProperty logColor;
        private SerializedProperty unityEvent;
        
        private float eventHeight;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Property Height
        
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            unityEvent = property.FindPropertyRelative("unityEvent");
            eventHeight = unityEvent == null ? 0 : PropertyDrawerExtensions.TryFindCustomPropertyDrawer(unityEvent, out PropertyDrawer customDrawer)
                ? customDrawer.GetPropertyHeight(unityEvent, label)
                : EditorGUI.GetPropertyHeight(unityEvent, label);

            float header = GgGUI.singleLineHeight + GgGUI.standardSpacing;
            float subHeader = GgGUI.singleLineHeight + GgGUI.standardSpacing;
            float content = property.isExpanded ? eventHeight - GgGUI.singleLineHeight : GgGUI.standardSpacing;
            return header + subHeader + content + GgGUI.standardSpacing;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGUI

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);
            GgEventBase ggEvent = property.GetValue<GgEventBase>();
            Type[] argTypes = ggEvent.GetArgTypes();

            // get reference to SerializeFields
            instanceName = property.FindPropertyRelative("instanceName");
            delay = property.FindPropertyRelative("delay");
            verboseLogs = property.FindPropertyRelative("verboseLogs");
            logColor = property.FindPropertyRelative("logColor");
            unityEvent = property.FindPropertyRelative("unityEvent");
            
            // cache defaults
            string headerText = argTypes == null ? $"{label.text}<>" : $"{label.text}<{GetArgTypesAsString(argTypes)}>";
            instanceName.stringValue = headerText;
            GUIContent headerLabel = new GUIContent(headerText, label.tooltip);
            GUIContent eventLabel = new GUIContent(label.text, label.tooltip);
            
            // draw property
            Rect dropdownRect = new Rect(position.x, position.y, position.width, position.height - GgGUI.standardSpacing);
            GgGUI.GetFoldoutPositionRects(dropdownRect, headerLabel, out GgFoldoutPositions foldoutPositions, true);
            GgGUI.DrawDropdownHeader(foldoutPositions, property, headerLabel, true, true, true);
            
            // draw content?
            if (property.isExpanded)
            {
                // draw events
                EditorGUI.PropertyField(foldoutPositions.subHeader, unityEvent, eventLabel);

                // draw footnote
                float footNoteX = foldoutPositions.subHeader.x + GgGUI.standardSpacing;
                float footNoteY = (foldoutPositions.subHeader.y + eventHeight) - (GgGUI.singleLineHeight - GgGUI.standardSpacing);
                float footNoteWidth = 60;
                Rect footNote = new Rect(footNoteX, footNoteY, footNoteWidth, GgGUI.singleLineHeight);
                string tooltip = "The number of persistent listeners for this event. This does not include listeners added at runtime.";
                EditorGUI.LabelField(footNote, new GUIContent($"Count: {ggEvent.ListenerCount()}", tooltip), GgGUI.SubLabelStyle);
            }
            
            // get subHeader rects
            float debugWidth = StringExtensions.GetStringWidth("Debug") + GgGUI.singleLineHeight;
            float debugColorWidth = GgGUI.singleLineHeight - (GgGUI.standardSpacing * 2);
            float debugColorLabelWidth = StringExtensions.GetStringWidth("Color");
            float invokeButtonWidth = StringExtensions.GetStringWidth("Invoke") + GgGUI.singleLineHeight;
            float delayLabelWidth = StringExtensions.GetStringWidth("Delay");
            float delayWidth = delayLabelWidth + GgGUI.miniFieldWidth;
            Rect debugToggle = new Rect(foldoutPositions.subHeader.x + GgGUI.standardSpacing, foldoutPositions.subHeader.y, debugWidth, foldoutPositions.subHeader.height);
            Rect debugColor = new Rect(debugToggle.xMax + GgGUI.standardSpacing, foldoutPositions.subHeader.y + GgGUI.standardSpacing, debugColorWidth, foldoutPositions.subHeader.height - (GgGUI.standardSpacing * 2));
            Rect debugColorLabel = new Rect(debugColor.xMax + GgGUI.standardSpacing, foldoutPositions.subHeader.y, debugColorLabelWidth, foldoutPositions.subHeader.height);
            Rect invokeButton = new Rect(foldoutPositions.subHeader.xMax - invokeButtonWidth, foldoutPositions.subHeader.y, invokeButtonWidth, foldoutPositions.subHeader.height);
            Rect delayField = new Rect(invokeButton.x - (GgGUI.standardSpacing + delayWidth), foldoutPositions.subHeader.y, delayWidth, foldoutPositions.subHeader.height);
            
            // draw subHeader
            EditorGUI.DrawRect(foldoutPositions.subHeaderBackground, InspectorExtensions.backgroundNormalColor);
            GUIContent toggleLabel = new GUIContent("Debug", verboseLogs.tooltip);
            verboseLogs.boolValue = EditorGUI.ToggleLeft(debugToggle, toggleLabel, verboseLogs.boolValue);
            if (verboseLogs.boolValue)
            {
                logColor.colorValue = EditorGUI.ColorField(debugColor, new GUIContent("", logColor.tooltip), logColor.colorValue, false, false, false);
                EditorGUI.LabelField(debugColorLabel, new GUIContent("Color", logColor.tooltip));
            }
            
            float defaultLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = delayLabelWidth;
            EditorGUI.PropertyField(delayField, delay, new GUIContent("Delay", delay.tooltip));
            EditorGUIUtility.labelWidth = defaultLabelWidth;
            
            GUIContent buttonLabel = new GUIContent("Invoke", $"Invoke event after {delay.floatValue} seconds using default args.\n\n[Note: the inspector invoke button will pass default argument values to listeners.]");
            if (GUI.Button(invokeButton, buttonLabel))
            {
                if (Application.isPlaying)
                {
                    ggEvent?.InvokeEventWithDefaultArgs();
                }
                else
                {
                    GgLogs.Log(null, GgLogType.Warning, "Cannot call '{0}' event for '{1}' while Application is not playing.", instanceName.stringValue, property.serializedObject.targetObject.name);
                }
            }
            
            // close property
            EditorGUI.EndProperty();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions

        private string GetArgTypesAsString(Type[] argTypes)
        {
            if (argTypes == null)
            {
                return string.Empty;
            }
            
            List<string> argTypesAsString = new List<string>();
            foreach (Type type in argTypes)
            {
                argTypesAsString.Add(type.Name);
            }
            
            string returnString = argTypesAsString[0];
            for (int i = 1; i < argTypesAsString.Count; i++)
            {
                returnString += $", {argTypesAsString[i]}";
            }
            return returnString;
        }

        #endregion
        
    } // class end
}
#endif
