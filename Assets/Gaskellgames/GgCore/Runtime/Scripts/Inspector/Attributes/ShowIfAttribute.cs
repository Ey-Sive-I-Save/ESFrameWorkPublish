using UnityEngine;
using System;

namespace Gaskellgames
{
    /// <summary>
    /// Code updated by Gaskellgames: https://github.com/Gaskellgames
    /// Original code created by ghysc: https://github.com/ghysc/SwitchAttribute
    /// </summary>

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public LogicGate LogicGate;
		
        public (string field, object comparison)[] conditions;
		
        /// <summary>
        /// Hides the property in the inspector if the specified field's value is equal to the comparison object's value.
        /// </summary>
        /// <param name="field"></param>
        public ShowIfAttribute(string field)
        {
            conditions = new (string, object)[] { (field, true) };
            LogicGate = LogicGate.AND;
        }
		
        /// <summary>
        /// Hides the property in the inspector if the specified field's value is equal to the comparison object's value.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="comparison"></param>
        public ShowIfAttribute(string field, object comparison)
        {
            conditions = new (string, object)[] { (field, comparison) };
            LogicGate = LogicGate.AND;
        }
		
        /// <summary>
        /// Hides the property in the inspector if all specified field values are equal to their comparison object's value.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="comparisons"></param>
        /// <param name="logicGate"></param>
        public ShowIfAttribute(string[] fields, object[] comparisons, LogicGate logicGate = LogicGate.AND)
        {
            this.LogicGate = logicGate;
			
            if (fields == null)
            {
                throw new NullReferenceException("Fields[] cannot be null");
            }
            if (comparisons == null)
            {
                throw new NullReferenceException("Comparisons[] cannot be null");
            }
            if (fields.Length != comparisons.Length)
            {
                throw new ArgumentException("Field and comparison arrays must be same length!");
            }

            conditions = new (string, object)[fields.Length];

            for (int i = 0; i < fields.Length; i++)
            {
                conditions[i] = (fields[i], comparisons[i]);
            }
        }

    } // class end
}