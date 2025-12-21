#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(WrapAttribute), true)]
    public class WrapDrawer : GgPropertyDrawer
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
            WrapAttribute attributeAsType = AttributeAsType<WrapAttribute>();
            if (property.propertyType == SerializedPropertyType.Float)
            {
                float limitedValue = property.floatValue;
                if (attributeAsType.max < property.floatValue)
                {
                    limitedValue = attributeAsType.min;
                }
                else if (property.floatValue < attributeAsType.min)
                {
                    limitedValue = attributeAsType.max;
                }

                if (GgGUI.FloatField(position, label, limitedValue, out float outputValue, property.hasMultipleDifferentValues))
                {
                    property.floatValue = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Integer)
            {
                int limitedValue = property.intValue;
                if ((int)attributeAsType.max < property.intValue)
                {
                    limitedValue = (int)attributeAsType.min;
                }
                else if (property.intValue < (int)attributeAsType.min)
                {
                    limitedValue = (int)attributeAsType.max;
                }

                if (GgGUI.IntField(position, label, limitedValue, out int outputValue, property.hasMultipleDifferentValues))
                {
                    property.intValue = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Vector2)
            {
                Vector2 limitedValue = new Vector2(property.vector2Value.x, property.vector2Value.y);
                if (attributeAsType.max < property.vector2Value.x)
                {
                    limitedValue.x = attributeAsType.min;
                }
                else if (property.vector2Value.x < attributeAsType.min)
                {
                    limitedValue.x = attributeAsType.max;
                }
                if (attributeAsType.max < property.vector2Value.y)
                {
                    limitedValue.y = attributeAsType.min;
                }
                else if (property.vector2Value.y < attributeAsType.min)
                {
                    limitedValue.y = attributeAsType.max;
                }

                if (GgGUI.Vector2Field(position, label, limitedValue, out Vector2 outputValue, property.hasMultipleDifferentValues))
                {
                    property.vector2Value = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Vector2Int)
            {
                Vector2Int limitedValue = new Vector2Int(property.vector2IntValue.x, property.vector2IntValue.y);
                if (attributeAsType.max < property.vector2IntValue.x)
                {
                    limitedValue.x = (int)attributeAsType.min;
                }
                else if (property.vector2IntValue.x < attributeAsType.min)
                {
                    limitedValue.x = (int)attributeAsType.max;
                }
                if (attributeAsType.max < property.vector2IntValue.y)
                {
                    limitedValue.y = (int)attributeAsType.min;
                }
                else if (property.vector2IntValue.y < attributeAsType.min)
                {
                    limitedValue.y = (int)attributeAsType.max;
                }

                if (GgGUI.Vector2IntField(position, label, limitedValue, out Vector2Int outputValue, property.hasMultipleDifferentValues))
                {
                    property.vector2IntValue = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Vector3)
            {
                Vector3 limitedValue = new Vector3(property.vector3Value.x, property.vector3Value.y);
                if (attributeAsType.max < property.vector3Value.x)
                {
                    limitedValue.x = attributeAsType.min;
                }
                else if (property.vector3Value.x < attributeAsType.min)
                {
                    limitedValue.x = attributeAsType.max;
                }
                if (attributeAsType.max < property.vector3Value.y)
                {
                    limitedValue.y = attributeAsType.min;
                }
                else if (property.vector3Value.y < attributeAsType.min)
                {
                    limitedValue.y = attributeAsType.max;
                }
                if (attributeAsType.max < property.vector3Value.z)
                {
                    limitedValue.z = attributeAsType.min;
                }
                else if (property.vector3Value.z < attributeAsType.min)
                {
                    limitedValue.z = attributeAsType.max;
                }

                if (GgGUI.Vector3Field(position, label, limitedValue, out Vector3 outputValue, property.hasMultipleDifferentValues))
                {
                    property.vector3Value = outputValue;
                }
            }
            else if (property.propertyType == SerializedPropertyType.Vector3Int)
            {
                Vector3Int limitedValue = new Vector3Int(property.vector3IntValue.x, property.vector3IntValue.y);
                if (attributeAsType.max < property.vector3IntValue.x)
                {
                    limitedValue.x = (int)attributeAsType.min;
                }
                else if (property.vector3IntValue.x < attributeAsType.min)
                {
                    limitedValue.x = (int)attributeAsType.max;
                }
                if (attributeAsType.max < property.vector3IntValue.y)
                {
                    limitedValue.y = (int)attributeAsType.min;
                }
                else if (property.vector3IntValue.y < attributeAsType.min)
                {
                    limitedValue.y = (int)attributeAsType.max;
                }
                if (attributeAsType.max < property.vector3IntValue.z)
                {
                    limitedValue.z = (int)attributeAsType.min;
                }
                else if (property.vector3IntValue.z < attributeAsType.min)
                {
                    limitedValue.z = (int)attributeAsType.max;
                }

                if (GgGUI.Vector3IntField(position, label, limitedValue, out Vector3Int outputValue, property.hasMultipleDifferentValues))
                {
                    property.vector3IntValue = outputValue;
                }
            }
            else
            {
                GgGUI.CustomPropertyField(position, property, label);
            }
        }

        #endregion

    } // class end
}

#endif