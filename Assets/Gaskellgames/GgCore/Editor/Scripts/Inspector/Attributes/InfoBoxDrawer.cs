#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(InfoBoxAttribute), true)]
    public class InfoBoxDrawer : GgPropertyDrawer
    {
        #region Variables

        private static readonly int boxHeight = 20;
        private static readonly int boxHeightIcon = boxHeight + 5;

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region GgPropertyHeight
        
        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            return GetConditionalResult(AttributeAsType<InfoBoxAttribute>(), property) ? propertyHeight + GetBoxHeight(Screen.width) : propertyHeight;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI
        
        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            float height = GetBoxHeight(position.width);
            Rect infoBoxPosition = new Rect(position.xMin, position.yMin, position.width, height);
            Rect propertyPosition = new Rect(position.xMin, position.yMin, position.width, position.height);
            InfoBoxAttribute attributeAsType = AttributeAsType<InfoBoxAttribute>();

            if (GetConditionalResult(attributeAsType, property))
            {
                propertyPosition.yMin += height + standardSpacing;
                switch (attributeAsType.messageType)
                {
                    case InfoMessageType.None:
                        EditorGUI.HelpBox(infoBoxPosition, attributeAsType.message, MessageType.None);
                        break;
                    case InfoMessageType.Info:
                        EditorGUI.HelpBox(infoBoxPosition, attributeAsType.message, MessageType.Info);
                        break;
                    case InfoMessageType.Warning:
                        EditorGUI.HelpBox(infoBoxPosition, attributeAsType.message, MessageType.Warning);
                        break;
                    case InfoMessageType.Error:
                        EditorGUI.HelpBox(infoBoxPosition, attributeAsType.message, MessageType.Error);
                        break;
                }
            }
            
            GgGUI.CustomPropertyField(propertyPosition, property, label);
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private float GetBoxHeight(float positionWidth)
        {
            InfoBoxAttribute attributeAsType = AttributeAsType<InfoBoxAttribute>();
            
            bool iconHidden = attributeAsType.messageType == InfoMessageType.None;
            float minHeight = iconHidden ? boxHeight : boxHeightIcon;
            float wrapHeight = StringExtensions.GetWrappedStringHeight(attributeAsType.message, iconHidden ? positionWidth : positionWidth + boxHeightIcon);
            return Mathf.Max(minHeight, wrapHeight);
        }
        
        /// <summary>
        /// Calculates the logic gate result of all comparisons
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private bool GetConditionalResult(InfoBoxAttribute condition, SerializedProperty property)
        {
            // skip checks
            InfoBoxAttribute attributeAsType = AttributeAsType<InfoBoxAttribute>();
            if (!attributeAsType.isConditional) { return true; }
            
            // get the logic result from the first condition
            bool[] results = new bool[condition.conditions.Length];
            
            // logic gate logic on the 'previous' condition along with the current condition
            for (var i = 0; i < condition.conditions.Length; i++)
            {
                results[i] = SerializedPropertyExtensions.Equals(property.GetField(condition.conditions[i].field), condition.conditions[i].comparison);
            }
            return GgMaths.LogicGateOutputValue(results, condition.LogicGate);
        }

        #endregion

    } // class end
}

#endif