#if UNITY_EDITOR
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomPropertyDrawer(typeof(UnitAttribute), true)]
    internal class UnitDrawer : GgPropertyDrawer
    {
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
            GgGUI.CustomPropertyField(position, property, label);
            DrawUnitOverlay(position, GetUnitLabel(AttributeAsType<UnitAttribute>().unit), property.propertyType, property.isExpanded);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions

        private GUIContent GetUnitLabel(Units units)
        {
            GUIContent label = new GUIContent("", units.ToString());
            
            switch (units)
            {
                case Units.None:
                    label.text = "";
                    break;
                
                case Units.Milliseconds:
                    label.text = "ms";
                    break;
                case Units.Seconds:
                    label.text = "s";
                    break;
                case Units.Minutes:
                    label.text = "m";
                    break;
                case Units.Hours:
                    label.text = "h";
                    break;
                case Units.Days:
                    label.text = "d";
                    break;
                
                case Units.Millimeters:
                    label.text = "mm";
                    break;
                case Units.Centimeters:
                    label.text = "cm";
                    break;
                case Units.Meters:
                    label.text = "m";
                    break;
                
                case Units.Bytes:
                    label.text = "B";
                    break;
                case Units.Kilobytes:
                    label.text = "kB";
                    break;
                case Units.Megabytes:
                    label.text = "MB";
                    break;
                
                case Units.Grams:
                    label.text = "g";
                    break;
                case Units.Kilograms:
                    label.text = "kg";
                    break;
                
                case Units.Nutons:
                    label.text = "N";
                    break;
                
                case Units.Units:
                    label.text = "u";
                    break;
                case Units.Multiplier:
                    label.text = "x";
                    break;
                case Units.Percentage:
                    label.text = "%";
                    break;
                case Units.Degrees:
                    label.text = "\u00B0";
                    break;
                case Units.Radians:
                    label.text = "rad";
                    break;
                case Units.Frames:
                    label.text = "frames";
                    break;
                
                case Units.PerSecond:
                    label.text = "/s";
                    break;
                case Units.UnitsPerSecond:
                    label.text = "u/s";
                    break;
                case Units.DegreesPerSecond:
                    label.text = "\u00B0/s";
                    break;
                case Units.RadiansPerSecond:
                    label.text = "rad/s";
                    break;
                case Units.FramesPerSecond:
                    label.text = "fps";
                    break;
                
                default:
                    return null;
            }

            return label;
        }

        private void DrawUnitOverlay(Rect position, GUIContent label, SerializedPropertyType propertyType, bool isExpanded)
        {
            Rect pos = position;
            if (propertyType == SerializedPropertyType.Vector2 || propertyType == SerializedPropertyType.Vector3)
            {
                // vector properties get broken down into two lines when there's not enough space
                if (EditorGUIUtility.wideMode)
                {
                    pos.xMin += EditorGUIUtility.labelWidth;
                    pos.width /= 3;
                }
                else
                {
                    pos.xMin += 12;
                    pos.yMin = pos.yMax - EditorGUIUtility.singleLineHeight;
                    pos.width /= (propertyType == SerializedPropertyType.Vector2) ? 2 : 3;
                }

                pos.height = EditorGUIUtility.singleLineHeight;
                DrawUnits(pos, label);
                pos.x += pos.width;
                DrawUnits(pos, label);
                if (propertyType == SerializedPropertyType.Vector3)
                {
                    pos.x += pos.width;
                    DrawUnits(pos, label);
                }
            }
            else if (propertyType == SerializedPropertyType.Vector4)
            {
                if (isExpanded)
                {
                    pos.yMin = pos.yMax - 4 * EditorGUIUtility.singleLineHeight -
                               3 * EditorGUIUtility.standardVerticalSpacing;
                    pos.height = EditorGUIUtility.singleLineHeight;
                    for (int i = 0; i < 4; ++i)
                    {
                        DrawUnits(pos, label);
                        pos.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    }
                }
            }
            else
            {
                pos.height = EditorGUIUtility.singleLineHeight;
                DrawUnits(pos, label);
            }
        }

        private void DrawUnits(Rect position, GUIContent label)
        {
            GUIStyle style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                contentOffset = new Vector2(-2, 0),
                normal = { textColor = new Color32(000, 179, 223, 255) },
                hover = { textColor = new Color32(223, 179, 000, 255) },
            };
            
            GUI.Label(position, label, style);
        }

        #endregion
        
    } // class end
}
#endif