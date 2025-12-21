#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(ProgressBarAttribute), true)]
    public class ProgressBarDrawer : GgPropertyDrawer
    {
        #region Variables
        
        private int controlID = -1;
        
        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            return propertyHeight;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI

        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            ProgressBarAttribute attributeAsType = AttributeAsType<ProgressBarAttribute>();
            bool isReadOnly = !GUI.enabled || attributeAsType.readOnly;
            
            // set label
            string barLabel = attributeAsType.label;
            if (barLabel == "")
            {
                barLabel = ObjectNames.NicifyVariableName(label.ToString());
            }
            
            // cache defaults
            float width = position.width - (miniFieldWidth + standardSpacing);
            Rect barBackground = new Rect(position.x, position.yMin, width, position.height);
            Rect floatRect = new Rect(position.xMax - miniFieldWidth, position.yMin, miniFieldWidth, position.height);
            Color32 barColor = new Color32(attributeAsType.R, attributeAsType.G, attributeAsType.B, attributeAsType.A);
            
            // draw bar
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                if (!isReadOnly)
                {
                    if (HandleSliderInteraction(out float clickValue, barBackground, property, attributeAsType))
                    {
                        property.intValue = (int)GgMaths.RoundFloat(attributeAsType.maxValue * clickValue, 3);
                    }
                }
                int value = property.intValue;
                float barValue = value / attributeAsType.maxValue;
                if (barValue < 0) { barValue = 0; }
                else if (1 < barValue) { barValue = 1; }
                DrawProgressBar(barBackground, barValue, barLabel, barColor, attributeAsType, property.hasMultipleDifferentValues);
                if (isReadOnly) { GUI.enabled = false; }
                if (GgGUI.IntField(floatRect, GUIContent.none, value, out value, property.hasMultipleDifferentValues))
                {
                    property.intValue = value;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Float)
            {
                if (!isReadOnly)
                {
                    if (HandleSliderInteraction(out float clickValue, barBackground, property, attributeAsType))
                    {
                        property.floatValue = GgMaths.RoundFloat(attributeAsType.maxValue * clickValue, 3);
                    }
                }
                float value = property.floatValue;
                float barValue = value / attributeAsType.maxValue;
                if (barValue < 0) { barValue = 0; }
                else if (1 < barValue) { barValue = 1; }
                DrawProgressBar(barBackground, barValue, barLabel, barColor, attributeAsType, property.hasMultipleDifferentValues);
                if (isReadOnly) { GUI.enabled = false; }
                if (GgGUI.FloatField(floatRect, GUIContent.none, value, out value, property.hasMultipleDifferentValues))
                {
                    property.floatValue = value;
                }
            }
            else
            {
                GgGUI.CustomPropertyField(position, property, label);
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private bool HandleSliderInteraction(out float value, Rect barPosition, SerializedProperty property, ProgressBarAttribute attributeAsType)
        {
            // update mouse style while inside bar background
            EditorGUIUtility.AddCursorRect(barPosition, MouseCursor.ResizeHorizontal);
            Event currentEvent = Event.current;
            
            // check if mouse clicked (inside the graph)
            if (currentEvent.rawType == EventType.MouseDown && currentEvent.button == 0 && barPosition.Contains(currentEvent.mousePosition))
            {
                int hash = property.name.GetHashCode();
                controlID = GUIUtility.GetControlID(hash, FocusType.Passive, barPosition);
                GUIUtility.hotControl = controlID;
                currentEvent.Use();
            }

            // check if mouse un-clicked
            if (currentEvent.rawType == EventType.MouseUp && currentEvent.button == 0)
            {
                controlID = -1;
                GUIUtility.hotControl = 0;
                currentEvent.Use();
            }
            
            // set values
            if (GUIUtility.hotControl == controlID)
            {
                if (currentEvent.isMouse && currentEvent.button == 0 && GUI.enabled)
                {
                    // calculate x value
                    if (currentEvent.mousePosition.x < barPosition.xMin) { value = 0; }
                    else if (barPosition.xMax < currentEvent.mousePosition.x) { value = 1; }
                    else { value = (currentEvent.mousePosition.x - barPosition.xMin) / barPosition.width; }
					
                    currentEvent.Use();
                    return true;
                }
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    value = property.floatValue / attributeAsType.maxValue;
                    return false;
                
                case SerializedPropertyType.Integer:
                    value = property.intValue / attributeAsType.maxValue;
                    return false;
                
                default:
                    value = 0;
                    return false;
            }
        }
        
        private void DrawProgressBar(Rect barPosition, float fillPercent, string label, Color barColor, ProgressBarAttribute attributeAsType, bool propertyHasMultipleDifferentValues)
        {
            // create inner barPosition
            int border = 1;
            Rect barPositionInner = new Rect(barPosition.x + border, barPosition.y + border, barPosition.width - (border * 2), barPosition.height - (border * 2));
            
            // draw background
            EditorGUI.DrawRect(barPosition, new Color32(028, 028, 028, 255));
            EditorGUI.DrawRect(barPositionInner, new Color32(045, 045, 045, 255));
            
            // draw bar
            if (propertyHasMultipleDifferentValues)
            {
                float multiSelectPositionX = barPositionInner.x + standardSpacing;
                float multiSelectPositionY = barPositionInner.y + (barPositionInner.height * 0.5f);
                float multiSelectPositionWidth = 12;
                float multiSelectPositionHeight = standardSpacing * 0.5f;
                
                Rect fillRect = new Rect(multiSelectPositionX, multiSelectPositionY, multiSelectPositionWidth, multiSelectPositionHeight);
                EditorGUI.DrawRect(fillRect, InspectorExtensions.textDisabledColor);
            }
            else
            {
                Rect fillRect = new Rect(barPositionInner.xMin, barPositionInner.yMin, barPositionInner.width * fillPercent, barPositionInner.height);
                EditorGUI.DrawRect(fillRect, barColor);
                fillRect = new Rect(barPositionInner.xMin, barPositionInner.yMin, barPositionInner.width * fillPercent, 2);
                EditorGUI.DrawRect(fillRect, barColor * 1.1f);
                fillRect = new Rect(barPositionInner.xMin, barPositionInner.yMax - 2 , barPositionInner.width * fillPercent, 2);
                EditorGUI.DrawRect(fillRect, barColor * 0.9f);
            }

            // set label alignment
            TextAnchor defaultAlignment = GUI.skin.label.alignment;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;

            // draw label
            bool defaultGui = GUI.enabled;
            Rect labelRect = new Rect(barPositionInner.xMin, barPositionInner.yMin, barPositionInner.width, barPositionInner.height);
            GUI.enabled = !attributeAsType.readOnly && defaultGui;
            GUI.Label(labelRect, label);
            GUI.enabled = defaultGui;

            // reset label alignment
            GUI.skin.label.alignment = defaultAlignment;
        }

        #endregion
        
    } // class end
}

#endif