#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomPropertyDrawer(typeof(EnumButtonsAttribute), true)]
    public class EnumButtonsDrawer : GgPropertyDrawer
    {
        #region Variables

        private bool isFlags;
        
        private int rows;
        private int columns;
        private int enumCount;
        
        private List<int> nicifyEnumValues;
        private List<string> nicifyDisplayNames;
        private int nicifyAllValue;
        
        #endregion

        //----------------------------------------------------------------------------------------------------
        
        #region GgPropertyHeight

        protected override float GgPropertyHeight(SerializedProperty property, float propertyHeight, float approxFieldWidth)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                return propertyHeight;
            }
            
            // cache values here as we need to use them in both height and GUI
            isFlags = AttributeAsType<EnumButtonsAttribute>().enumFlags;
            if (isFlags)
            {
                object target = property.serializedObject.targetObject;
                object targetEnum = fieldInfo.GetValue(target);
                Type targetEnumType = targetEnum.GetType();
                
                NicifyEnumFlagValues(Enum.GetValues(targetEnumType), Enum.GetNames(targetEnumType), out nicifyAllValue, out nicifyEnumValues, out nicifyDisplayNames);
                enumCount = nicifyEnumValues.Count;
            }
            else
            {
                enumCount = property.enumNames.Length;
            }
            columns = Mathf.FloorToInt(approxFieldWidth / (miniFieldWidth + standardSpacing));
            rows = Mathf.CeilToInt((float)enumCount / (float)columns);
            
            // return custom height
            return (singleLineHeight + standardSpacing) * Mathf.Max(1, rows);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGgGUI

        protected override void OnGgGUI(Rect position, SerializedProperty property, GUIContent label, GgGUIDefaults defaultCache)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                GgGUI.CustomPropertyField(position, property, label);
                return;
            }
            
            Rect labelPosition = GetLabelPosition(position, label);
            Rect fieldPosition = GetFieldPosition(position, label);

            // draw label
            if (0 < labelPosition.width)
            {
                GgGUI.PrefixLabelField(labelPosition, label, property.hasMultipleDifferentValues);
            }
            
            // draw enum buttons
            if (isFlags) { DrawEnumButtonFlags(fieldPosition, property); }
            else { DrawEnumButtons(fieldPosition, property); }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private void DrawEnumButtons(Rect position, SerializedProperty property)
        {
            // cache values
            float buttonWidth = position.width / Mathf.Min(columns, enumCount);
            bool propertyHasMultipleDifferentValues = property.hasMultipleDifferentValues;
            
            // draw all options with min button width
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                { 
                    // enum index target
                    int enumIndex = column + (row * columns);
                    if (enumCount <= enumIndex) { break; }
                    
                    // get button position
                    float positionX = position.x + (buttonWidth * column);
                    float positionY = position.y + (row * (singleLineHeight + standardSpacing));
                    Rect buttonPos = new Rect(positionX, positionY, buttonWidth, singleLineHeight);
                    
                    // draw button
                    bool previousState = property.intValue == enumIndex && !propertyHasMultipleDifferentValues;
                    bool newState = GUI.Toggle(buttonPos, previousState, property.enumDisplayNames[enumIndex], EditorStyles.miniButton);
                    if (newState != previousState)
                    {
                        property.intValue = enumIndex;
                    }
                }
            }
        }
        
        private void DrawEnumButtonFlags(Rect position, SerializedProperty property)
        {
            // cache values
            float buttonWidth = position.width / Mathf.Min(columns, enumCount);
            bool propertyHasMultipleDifferentValues = property.hasMultipleDifferentValues;
            
            // draw all options with min button width
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                { 
                    // enum index target
                    int enumIndex = column + (row * columns);
                    if (enumCount <= enumIndex) { break; }
                    
                    // get button position
                    float positionX = position.x + (buttonWidth * column);
                    float positionY = position.y + (row * (singleLineHeight + standardSpacing));
                    Rect buttonPos = new Rect(positionX, positionY, buttonWidth, singleLineHeight);
                    
                    // get current button state
                    bool isAll = property.intValue.IsFlagAll() || property.intValue == nicifyAllValue;
                    bool isNone = property.intValue.IsFlagNone();
                    int indexFlagValue = GetEnumValueFlag(enumIndex, nicifyEnumValues);
                    bool currentButtonState;
                    switch (enumIndex)
                    {
                        case 0:
                            currentButtonState = isNone && !propertyHasMultipleDifferentValues;
                            break;
                        
                        case 1:
                            currentButtonState = isAll && !propertyHasMultipleDifferentValues;
                            break;
                        
                        default:
                            currentButtonState = property.intValue.HasAllFlags(indexFlagValue) || isAll && !propertyHasMultipleDifferentValues;
                            break;
                    }
                    
                    // draw button
                    bool newButtonState = GUI.Toggle(buttonPos, currentButtonState, nicifyDisplayNames[enumIndex], EditorStyles.miniButton);
                    if (newButtonState != currentButtonState)
                    {
                        switch (enumIndex)
                        {
                            case 0:
                                property.intValue = 0;
                                break;
                        
                            case 1:
                                property.intValue = ~0;
                                break;
                        
                            default:
                                int tempEnumValueFlag = isAll ? nicifyAllValue : property.intValue;
                                tempEnumValueFlag.ToggleFlag(indexFlagValue);
                                if (tempEnumValueFlag == nicifyAllValue) { tempEnumValueFlag = ~0; }
                                property.intValue = tempEnumValueFlag;
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Convert the propertyEnumValueFlag to a 'nicify' version (i.e. with 'all' state not being -1.)
        /// </summary>
        /// <param name="enumValues">All values of the enum.</param>
        /// <returns></returns>
        private int NicifyEnumFlagValueAll(Array enumValues)
        {
            int enumFlagValue = 0;
            for (int i = 0; i < enumValues.Length; i++)
            {
                int thisValue = (int)enumValues.GetValue(i);
                if (thisValue.IsFlagNone()) { continue; }
                if (thisValue.IsFlagAll()) { continue; }
                
                // add all other states to the new EnumFlagValue
                enumFlagValue |= thisValue;
            }
            return enumFlagValue;
        }
        
        /// <summary>
        /// Get Nicify versions of all flag values in an array
        /// </summary>
        /// <param name="enumValues">All values of the enum.</param>
        /// <param name="enumNames">All display names of the enum.</param>
        /// <param name="nicifyAllValue">Int32 representation of an enum with 'nicify' value (i.e. with 'all' state not being -1.)</param>
        /// <param name="nicifyEnumValues">Output values of the enum, with 'none' and 'all' states in index 0 and 1 respectively.</param>
        /// <param name="nicifyDisplayNames">Display-friendly names for each index of the nicifyEnumValues.</param>
        private void NicifyEnumFlagValues(Array enumValues, Array enumNames, out int nicifyAllValue, out List<int> nicifyEnumValues, out List<string> nicifyDisplayNames)
        {
            // create new array with 'none' and 'all' states in index 0 and 1
            nicifyEnumValues = new List<int>() { 0, ~0 };
            nicifyDisplayNames = new List<string>() { "None", "All" };
            nicifyAllValue = NicifyEnumFlagValueAll(enumValues);
            
            // used to temp hold all multi-value flags to add them after single values
            List<int> multiValues = new List<int>();
            List<string> multiValueNames = new List<string>();
            
            // add enumValues and displayNames to new lists, for any state that isn't 'none' or 'all' 
            for (int i = 0; i < enumValues.Length; i++)
            {
                int thisValue = (int)enumValues.GetValue(i);
                string thisName = (string)enumNames.GetValue(i);
                
                if (thisValue.IsFlagNone() || thisValue == 0)
                {
                    // override name, if one exists, for 'none'
                    nicifyDisplayNames[0] = thisName.NicifyName();
                }
                else if (thisValue.IsFlagAll() || thisValue == nicifyAllValue || thisValue == -1)
                {
                    // override name, if one exists, for 'all'
                    nicifyDisplayNames[1] = thisName.NicifyName();
                }
                else
                {
                    if (1 < thisValue.FlagCount())
                    {
                        // add all multi flag states to the temp holding lists
                        multiValues.Add(thisValue);
                        multiValueNames.Add(thisName.NicifyName());
                    }
                    else
                    {
                        // add all single flag states to the new lists
                        nicifyEnumValues.Add(thisValue);
                        nicifyDisplayNames.Add(thisName.NicifyName());
                    }
                }
            }

            for (int i = 0; i < multiValues.Count; i++)
            {
                // add all multi flag states to the new lists
                nicifyEnumValues.Add(multiValues[i]);
                nicifyDisplayNames.Add(multiValueNames[i]);
            }
        }
        
        /// <summary>
        /// Convert a specified index of enumValues to the value of it's EnumValueFlag
        /// </summary>
        /// <param name="index"></param>
        /// <param name="enumValues"></param>
        /// <returns>Int32 representation of an enum index with Mixed Values.</returns>
        private int GetEnumValueFlag(int index, IReadOnlyList<int> enumValues)
        {
            return enumValues[index];
        }

        #endregion

    } // class end
}

#endif