#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Gaskellgames.EditorOnly
{
    /// <summary>
    /// Code updated by Gaskellgames: https://github.com/Gaskellgames
    /// Original code created by ghysc: https://github.com/ghysc/SwitchAttribute
    /// </summary>
	
    [CustomPropertyDrawer(typeof(DisableIfAttribute), true)]
    public class DisableIfDrawer : GgPropertyDrawer
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
            GUI.enabled = !GetConditionalResult(AttributeAsType<DisableIfAttribute>(), property);
            GgGUI.CustomPropertyField(position, property, label);
        }
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        /// <summary>
        /// Calculates the logic gate result of all comparisons
        /// </summary>
        /// <param name="condition"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        private bool GetConditionalResult(DisableIfAttribute condition, SerializedProperty property)
        {
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