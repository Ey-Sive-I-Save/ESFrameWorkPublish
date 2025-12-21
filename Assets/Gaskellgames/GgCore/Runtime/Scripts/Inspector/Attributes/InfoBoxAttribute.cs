using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class InfoBoxAttribute : PropertyAttribute
    {
        public string message;
        public InfoMessageType messageType;
        
        public bool isConditional;
        public LogicGate LogicGate;
        public (string field, object comparison)[] conditions;
        
        /// <summary>
        /// Shows an InfoBox in the inspector, with a specified messageType.
        /// If no messageType is specified, then an 'Info' type will be shown.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageType"></param>
        public InfoBoxAttribute(string message, InfoMessageType messageType = InfoMessageType.Info)
        {
            this.message = message;
            this.messageType = messageType;
            
            this.isConditional = false;
            LogicGate = LogicGate.AND;
            conditions = new (string, object)[] { };
        }
        
        /// <summary>
        /// Shows an InfoBox in the inspector if a specified fields value is true.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="field"></param>
        public InfoBoxAttribute(string message, string field)
        {
            this.message = message;
            this.messageType = InfoMessageType.Info;
            
            this.isConditional = true;
            LogicGate = LogicGate.AND;
			
            if (field == null)
            {
                throw new NullReferenceException("Field cannot be null");
            }
            
            conditions = new (string, object)[] { (field, true) };
        }
        
        /// <summary>
        /// Shows an InfoBox in the inspector, with a specified messageType, if a specified fields value is true.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageType"></param>
        /// <param name="field"></param>
        public InfoBoxAttribute(string message, InfoMessageType messageType, string field)
        {
            this.message = message;
            this.messageType = messageType;
            
            this.isConditional = true;
            LogicGate = LogicGate.AND;
			
            if (field == null)
            {
                throw new NullReferenceException("Field cannot be null");
            }
            
            conditions = new (string, object)[] { (field, true) };
        }
        
        /// <summary>
        /// Shows an InfoBox in the inspector, with a specified messageType, if all specified field values
        /// are equal to their comparison object's value.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageType"></param>
        /// <param name="field"></param>
        /// <param name="comparison"></param>
        public InfoBoxAttribute(string message, InfoMessageType messageType, string field, object comparison)
        {
            this.message = message;
            this.messageType = messageType;
            
            this.isConditional = true;
            LogicGate = LogicGate.AND;
			
            if (field == null)
            {
                throw new NullReferenceException("Field cannot be null");
            }
            if (comparison == null)
            {
                throw new NullReferenceException("Comparison cannot be null");
            }
            
            conditions = new (string, object)[] { (field, comparison) };
        }
		
        /// <summary>
        /// Shows an InfoBox in the inspector, with a specified messageType, if all specified field values
        /// are equal to their comparison object's value.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="fields"></param>
        /// <param name="comparisons"></param>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public InfoBoxAttribute(string message, string[] fields, object[] comparisons)
        {
            this.message = message;
            this.messageType = InfoMessageType.Info;
            
            this.isConditional = true;
            this.LogicGate = LogicGate.AND;
			
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
		
        /// <summary>
        /// Shows an InfoBox in the inspector, with a specified messageType, if all specified field values
        /// are equal to their comparison object's value.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageType"></param>
        /// <param name="fields"></param>
        /// <param name="comparisons"></param>
        /// <param name="logicGate"></param>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public InfoBoxAttribute(string message, InfoMessageType messageType, string[] fields, object[] comparisons, LogicGate logicGate = LogicGate.AND)
        {
            this.message = message;
            this.messageType = messageType;
            
            this.isConditional = true;
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
