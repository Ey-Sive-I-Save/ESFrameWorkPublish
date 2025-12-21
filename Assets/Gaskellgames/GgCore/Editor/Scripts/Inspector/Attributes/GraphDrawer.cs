#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using System.Reflection;
using UnityEngine.Rendering;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code updated by Gaskellgames: https://github.com/Gaskellgames
    /// Original code from vertxxyz: https://gist.github.com/vertxxyz/5a00dbca58aee033b35be2e227e80f8d
    /// </summary>
	
    [CustomPropertyDrawer(typeof(GraphAttribute), true)]
    public class GraphAttributeDrawer : GgPropertyDrawer
    {
        #region Variables

        private readonly Color graphDivisionColour = new Color32(079, 079, 079, 179);
        private readonly Color xAxiscolour = new Color32(050, 179, 050, 179);
        private readonly Color yAxiscolour = new Color32(223, 050, 050, 179);
        private readonly Color positionColour = new Color32(000, 179, 223, 255);
		
        private const float graphSize = 90f;
        private const float graphSizeQuarter = graphSize * 0.25f;
        private const float graphSizeHalf = graphSize * 0.5f;
        private const float graphSizeThreeQuarters = graphSize * 0.75f;
		
        private int controlID = -1;
		
        private MethodInfo applyWireMaterial;
        private MethodInfo ApplyWireMaterial
        {
            get
            {
                if (applyWireMaterial == null)
                {
                    string name = "ApplyWireMaterial";
                    BindingFlags bindingAttr = BindingFlags.NonPublic | BindingFlags.Static;
                    Type[] types = new[] { typeof(CompareFunction) };
					
                    applyWireMaterial = typeof(HandleUtility).GetMethod(name, bindingAttr, null, types, null);
                }

                return applyWireMaterial;
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GgPropertyHeight
        
        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            return property.propertyType != SerializedPropertyType.Vector2 ? propertyHeight : graphSize;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI
        
        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                GgGUI.CustomPropertyField(position, property, label);
                return;
            }
            
            HandleGraph(position, property);
            HandleLabelsAndSliders(position, property, label, defaultCache.guiEnabled);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions

        private void HandleGraph(Rect position, SerializedProperty property)
        {
            GraphAttribute attributeAsType = AttributeAsType<GraphAttribute>();
            Rect indentPosition = new Rect(position.xMin + EditorExtensions.currentIndent, position.yMin, position.width - EditorExtensions.currentIndent, position.height);
            
            // cache values
            Rect graphRect = new Rect(indentPosition.xMin, indentPosition.yMin, graphSize, graphSize);
            Event currentEvent = Event.current;
			
            EditorGUIUtility.AddCursorRect(graphRect, MouseCursor.MoveArrow);
			
            // check if mouse clicked (inside the graph)
            if (currentEvent.rawType == EventType.MouseDown)
            {
                if (graphRect.Contains(currentEvent.mousePosition))
                {
                    int hash = property.name.GetHashCode();
                    controlID = GUIUtility.GetControlID(hash, FocusType.Passive, graphRect);
                    GUIUtility.hotControl = controlID;
                    currentEvent.Use();
                }
            }

            // check if left mouse un-clicked (anywhere) and right click menu
            if (currentEvent.isMouse && currentEvent.rawType == EventType.MouseUp)
            {
                switch (currentEvent.button)
                {
                    case 0: // left mouse
                        controlID = -1;
                        GUIUtility.hotControl = 0;
                        currentEvent.Use();
                        break;
                    case 1: // right click
                        if (!graphRect.Contains(currentEvent.mousePosition)) { break; }
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Center"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(0.5f, 0.5f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Center Left"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(0.0f, 0.5f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Center Right"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(1.0f, 0.5f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Top"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(0.5f, 1.0f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Top Left"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(0.0f, 1.0f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Top Right"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(1.0f, 1.0f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Bottom"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(0.5f, 0.0f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Bottom Left"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(0.0f, 0.0f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.AddItem(new GUIContent("Bottom Right"), false, () =>
                        {
                            property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, new Vector2(1.0f, 0.0f));
                            property.serializedObject.ApplyModifiedProperties();
                        });
                        menu.ShowAsContext();
                        currentEvent.Use();
                        break;
                }
            }
			
            // set values
            if (GUIUtility.hotControl == controlID)
            {
                if (currentEvent.isMouse && currentEvent.button == 0 && GUI.enabled)
                {
                    // calculate x value
                    Vector2 value = new Vector2();
                    if (currentEvent.mousePosition.x < graphRect.xMin) { value.x = 0; }
                    else if (graphRect.xMax < currentEvent.mousePosition.x) { value.x = 1; }
                    else { value.x = (currentEvent.mousePosition.x - graphRect.xMin) / graphRect.width; }
				
                    // calculate y value
                    if (currentEvent.mousePosition.y < graphRect.yMin) { value.y = 1; }
                    else if (graphRect.yMax < currentEvent.mousePosition.y) { value.y = 0; }
                    else { value.y = 1 - (currentEvent.mousePosition.y - graphRect.yMin) / graphRect.height; }
					
                    // apply value
                    property.vector2Value = GgMaths.LerpVector2(attributeAsType.min, attributeAsType.max, value);
                    currentEvent.Use();
                }
            }
		
            // draw graph
            using (new GUI.GroupScope(graphRect, EditorStyles.helpBox))
            {
                if (currentEvent.type != EventType.Repaint) { return; }
                // draw axis lines
                GL.Begin(Application.platform == RuntimePlatform.WindowsEditor ? GL.QUADS : GL.LINES);
                ApplyWireMaterial?.Invoke(null, new object[] {CompareFunction.Always});
                GLExtensions.GL_DrawLine(new Vector2(graphSizeQuarter, 1), new Vector2(graphSizeQuarter, graphSize - 2), graphDivisionColour);
                GLExtensions.GL_DrawLine(new Vector2(graphSizeQuarter, 1), new Vector2(graphSizeQuarter, graphSize - 2), graphDivisionColour);
                GLExtensions.GL_DrawLine(new Vector2(graphSizeThreeQuarters, 1), new Vector2(graphSizeThreeQuarters, graphSize - 2), graphDivisionColour);
                GLExtensions.GL_DrawLine(new Vector2(1, graphSizeQuarter), new Vector2(graphSize - 2, graphSizeQuarter), graphDivisionColour);
                GLExtensions.GL_DrawLine(new Vector2(1, graphSizeThreeQuarters), new Vector2(graphSize - 2, graphSizeThreeQuarters), graphDivisionColour);
                GLExtensions.GL_DrawLine(new Vector2(graphSizeHalf, 1), new Vector2(graphSizeHalf, graphSize - 2), GUI.enabled ? xAxiscolour : xAxiscolour * 0.631f);
                GLExtensions.GL_DrawLine(new Vector2(1, graphSizeHalf), new Vector2(graphSize - 2, graphSizeHalf), GUI.enabled ? yAxiscolour : yAxiscolour * 0.631f);
                GL.End();

                // position circle
                if (property.hasMultipleDifferentValues) { return; }
                GL.Begin(Application.platform == RuntimePlatform.WindowsEditor ? GL.QUADS : GL.LINES);
                ApplyWireMaterial?.Invoke(null, new object[] {CompareFunction.Always});
                Vector2 circlePos = GgMaths.InverseLerpVector2(attributeAsType.min, attributeAsType.max, property.vector2Value);
                circlePos.y = 1 - circlePos.y;
                circlePos *= graphSize;
                GLExtensions.GL_DrawCircle(circlePos, graphSize * 0.03f, 2, GUI.enabled ? positionColour : positionColour * 0.631f);
                GL.End();
            }
        }

        private void HandleLabelsAndSliders(Rect position, SerializedProperty property, GUIContent label, bool defaultGuiEnabled)
        {
            GraphAttribute attributeAsType = AttributeAsType<GraphAttribute>();
            Rect indentPosition = new Rect(position.xMin + EditorExtensions.currentIndent, position.yMin, position.width - EditorExtensions.currentIndent, position.height);
            
            // cache values
            float labelSliderWidth = indentPosition.width - (graphSize + (standardSpacing * 3));
            float labelSliderXMin = indentPosition.xMin + (graphSize + (standardSpacing * 3));
            float labelSliderYMin = indentPosition.yMin + ((graphSize - (singleLineHeight * 5)) * 0.5f);
            Rect labelRect = new Rect(labelSliderXMin, labelSliderYMin, labelSliderWidth, singleLineHeight);
            Rect xLabelRect = new Rect(labelSliderXMin, labelRect.yMin + singleLineHeight, labelSliderWidth, singleLineHeight);
            Rect xSliderRect = new Rect(labelSliderXMin, xLabelRect.yMin + singleLineHeight, labelSliderWidth, singleLineHeight);
            Rect yLabelRect = new Rect(labelSliderXMin, xSliderRect.yMin + singleLineHeight, labelSliderWidth, singleLineHeight);
            Rect ySliderRect = new Rect(labelSliderXMin, yLabelRect.yMin + singleLineHeight, labelSliderWidth, singleLineHeight);
            
            // title, subtitles & sliders
            Vector2 propertyVector2Value = property.vector2Value;
            EditorGUI.LabelField(labelRect, label, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            GUI.enabled = false;
            EditorGUI.LabelField(xLabelRect, attributeAsType.xAxis);
            GUI.enabled = defaultGuiEnabled;
            propertyVector2Value.x = EditorGUI.Slider(xSliderRect, propertyVector2Value.x, attributeAsType.min.x, attributeAsType.max.x);
            GUI.enabled = false;
            EditorGUI.LabelField(yLabelRect, attributeAsType.yAxis);
            GUI.enabled = defaultGuiEnabled;
            propertyVector2Value.y = EditorGUI.Slider(ySliderRect, propertyVector2Value.y, attributeAsType.min.y, attributeAsType.max.y);
            if (EditorGUI.EndChangeCheck())
            {
                property.vector2Value = propertyVector2Value;
            }
        }

        #endregion
		
    } // class end
}
#endif