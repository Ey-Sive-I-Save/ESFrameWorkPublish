using System.Collections.Generic;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [System.Serializable]
    internal class SerializedKeyValuePair<TKey, TValue>
    {
        [SerializeField]
        internal TKey key;
        
        [SerializeField]
        internal TValue value;
        
        internal SerializedKeyValuePair(TKey key, TValue value)
        {
            this.key =  key;
            this.value = value;
        }
        
        internal bool IsValidValue()
        {
            if (value is not Object valueAsObject) { return true; }
            return valueAsObject != null;
        }

        internal bool IsValidKey()
        {
            if (key is not Object keyAsObject) { return true; }
            return keyAsObject != null;
        }

        internal bool IsValue(TValue value)
        {
            return EqualityComparer<TValue>.Default.Equals(this.value, value);
        }

        internal bool IsKey(TKey key)
        {
            return EqualityComparer<TKey>.Default.Equals(this.key, key);
        }

        internal void Deconstruct(out TKey key, out TValue value)
        {
            key = this.key;
            value = this.value;
        }
        
    } // class end
}