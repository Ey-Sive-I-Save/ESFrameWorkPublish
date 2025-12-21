#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public abstract class GgPropertyDrawer : PropertyDrawer
    {
        #region Variables
        
        protected float singleLineHeight => GgGUI.singleLineHeight;
        protected float standardSpacing => GgGUI.standardSpacing;
        protected float labelWidth => GgGUI.labelWidth;
        
        protected float miniFieldWidth = GgGUI.miniFieldWidth;
        protected float currentIndent => GgGUI.currentIndent;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Height

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float propertyHeight = property == null ? 0 : PropertyDrawerExtensions.TryFindCustomPropertyDrawer(property, out PropertyDrawer customDrawer)
                ? customDrawer.GetPropertyHeight(property, label)
                : EditorGUI.GetPropertyHeight(property, label);
            
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float thisLabelWidth = hideLabel ? 0 : labelWidth;
            float approxFieldWidth = EditorGUIUtility.currentViewWidth - (currentIndent + thisLabelWidth);
            
            return GgPropertyHeight(property, propertyHeight, approxFieldWidth);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGUI
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // open property and get reference to attribute instance
            EditorGUI.BeginProperty(position, label, property);
            
            GgGUIDefaults defaultCache = new GgGUIDefaults();
            OnGgGUI(position, property, label, defaultCache);
            defaultCache.ResetDefaults();
            
            // close property
            EditorGUI.EndProperty();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GgPropertyDrawer
        
        protected abstract float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth);

        protected abstract void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache);
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Helper Functions
        
        protected T AttributeAsType<T>() where T : PropertyAttribute
        {
            return attribute as T;
        }
        
        // ForceArray code from Jamora & vexe : https://answers.unity.com/questions/603882/serializedproperty-isnt-being-detected-as-an-array.html
        // Updated by Gaskellgames
        protected bool IsListOrArray(SerializedProperty property)
        {
            string path = property.propertyPath;
            int indexOfDot = path.IndexOf('.');
            if (indexOfDot == -1) { return false; }
            
            string propName = path.Substring(0, indexOfDot);
            SerializedProperty serializedProperty = property.serializedObject.FindProperty(propName);
            return serializedProperty.isArray;
        }
        
        /// <summary>
        /// Get all serialized properties from inside an Array/List SerializedProperty.
        /// </summary>
        /// <param name="arrayProperty"></param>
        /// <param name="enterVisibleGrandchildren"></param>
        /// <returns>Number of elements at index 0, followed by all elements in order.</returns>
        protected IEnumerable<SerializedProperty> GetSerializedPropertiesInArray(SerializedProperty arrayProperty, bool enterVisibleGrandchildren)
        {
            arrayProperty = arrayProperty.Copy();
            string startPath = arrayProperty.propertyPath;
            bool enterVisibleChildren = true;
            while (arrayProperty.NextVisible(enterVisibleChildren) && arrayProperty.propertyPath.StartsWith(startPath))
            {
                yield return arrayProperty;
                enterVisibleChildren = enterVisibleGrandchildren;
            }
        }
        
        protected Rect GetLabelPosition(Rect position, GUIContent label)
        {
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float thisLabelWidth = hideLabel ? 0 : labelWidth;
            
            return new Rect(position.x, position.y, thisLabelWidth, singleLineHeight);
        }
        
        protected Rect GetFieldPosition(Rect position, GUIContent label)
        {
            bool hideLabel = string.IsNullOrEmpty(label.text);
            float thisFieldWidth = hideLabel
                ? position.width
                : position.width - (GetLabelPosition(position, label).width + standardSpacing);
            
            return new Rect(position.xMax - thisFieldWidth, position.y, thisFieldWidth, position.height);
        }

        #endregion
        
    } // class end
}

#endif