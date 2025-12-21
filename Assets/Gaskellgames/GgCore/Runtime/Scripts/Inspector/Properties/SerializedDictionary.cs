using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    #region SerializedDictionaryBase

    public abstract class ASerializedDictionary
    {
#if UNITY_EDITOR
        
        private bool isSortedAscending;
        private int sortBy;

        internal bool IsSortedAscending
        {
            get => isSortedAscending;
            set
            {
                isSortedAscending = value;
                switch (sortBy)
                {
                    case 0:
                        EditorInternal_SortByKey();
                        break;
                    
                    case 1:
                        EditorInternal_SortByValue();
                        break;
                }
            }
        }

        internal int SortBy
        {
            get => sortBy;
            set
            {
                sortBy = value;
                switch (sortBy)
                {
                    case 0:
                        EditorInternal_SortByKey();
                        break;
                    
                    case 1:
                        EditorInternal_SortByValue();
                        break;
                }
            }
        }
        
        internal abstract bool EditorInternal_IsNullOrDuplicate(int index);
        
        internal abstract void EditorInternal_AddEmpty();
        
        internal abstract void EditorInternal_RemoveAt(int index);
        
        internal abstract void EditorInternal_SortByKey();
        
        internal abstract void EditorInternal_SortByValue();
#endif
        
        public abstract void Initialise();
        
    }

    #endregion
    
    [Serializable]
    public class SerializedDictionary<TKey, TValue> : ASerializedDictionary
    {
        #region Variables
        
        [SerializeField]
        private List<SerializedKeyValuePair<TKey, TValue>> serializedDictionary;
        
        private Dictionary<TKey, TValue> dictionary;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Constructors

        /// <summary>
        /// Construct a new blank serializedDictionary
        /// </summary>
        public SerializedDictionary()
        {
            dictionary = new Dictionary<TKey, TValue>();
            serializedDictionary = new List<SerializedKeyValuePair<TKey, TValue>>();
        }
        
        /// <summary>
        /// Construct this SerializedDictionary from another dictionary of the same key value type
        /// </summary>
        /// <param name="otherDictionary"></param>
        public SerializedDictionary(Dictionary<TKey, TValue> otherDictionary)
        {
            // get key values from otherDictionary
            List<TValue> allValues = otherDictionary.Values.ToList();
            List<TKey> allKeys = otherDictionary.Keys.ToList();
            
            // check valid data
            if (allValues.Count != allKeys.Count) { return; }
            
            // convert otherDictionary to serializedDictionary
            serializedDictionary = new List<SerializedKeyValuePair<TKey, TValue>>();
            for (int i = 0; i < allKeys.Count; i++)
            {
                TryAdd(allKeys[i], allValues[i]);
            }
            Initialise();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Internal Functions

#if UNITY_EDITOR
        internal override bool EditorInternal_IsNullOrDuplicate(int index)
        {
            // out of range check
            if (index < 0 || serializedDictionary.Count - 1 < index) { return false; }
            
            // comparison null checks
            TKey comparisonKey = serializedDictionary[index].key;
            if (comparisonKey == null) { return true; }
            if (comparisonKey is string stringKey && (string.IsNullOrEmpty(stringKey) || string.IsNullOrWhiteSpace(stringKey))) { return true; }
            
            // duplicate check
            for (int i = 0; i < serializedDictionary.Count; i++)
            {
                TKey selectedKey = serializedDictionary[i].key;
                if (selectedKey == null) { continue; }
                if (i == index || !selectedKey.Equals(comparisonKey)) { continue; }
                return true;
            }

            return false;
        }
        
        internal override void EditorInternal_AddEmpty()
        {
            // add 'empty' item to list
            serializedDictionary.Add(new SerializedKeyValuePair<TKey, TValue>(default , default));
            
            // force initialise dictionary
            Initialise();
        }
        
        internal override void EditorInternal_RemoveAt(int index)
        {
            // remove item at index from list
            serializedDictionary.RemoveAt(index);
            
            // force initialise dictionary
            Initialise();
        }
        
        internal override void EditorInternal_SortByKey()
        {
            serializedDictionary = IsSortedAscending
                ? serializedDictionary.OrderBy(serializedKeyValuePair => serializedKeyValuePair.key.ToString()).ToList()
                : serializedDictionary.OrderByDescending(serializedKeyValuePair => serializedKeyValuePair.key.ToString()).ToList();
        }

        internal override void EditorInternal_SortByValue()
        {
            serializedDictionary = IsSortedAscending
                ? serializedDictionary.OrderBy(serializedKeyValuePair => serializedKeyValuePair.key.ToString()).ToList()
                : serializedDictionary.OrderByDescending(serializedKeyValuePair => serializedKeyValuePair.value.ToString()).ToList();
        }
#endif
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region private Functions
        
        private void InitialiseIfRequired()
        {
            if (dictionary != null && dictionary.Count == serializedDictionary.Count) { return; }
            Initialise();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Public Functions
        
        /// <summary>
        /// Initialise the dictionary from the serialized key values
        /// </summary>
        public sealed override void Initialise()
        {
            dictionary = new Dictionary<TKey, TValue>();
            serializedDictionary ??= new List<SerializedKeyValuePair<TKey, TValue>>();
            
            foreach (SerializedKeyValuePair<TKey, TValue> keyValue in serializedDictionary)
            {
                if (keyValue.key == null) { continue; }
                if (keyValue.key is string stringKey && (string.IsNullOrEmpty(stringKey) || string.IsNullOrWhiteSpace(stringKey))) { continue; }
                dictionary.TryAdd(keyValue.key, keyValue.value);
            }
        }
        
        /// <summary>
        /// Gets the IEqualityComparer&lt;<see cref="TKey"/>&gt; that is used to determine equality of keys for the dictionary.
        /// </summary>
        public IEqualityComparer<TKey> Comparer
        {
            get
            {
                InitialiseIfRequired();
                return dictionary.Comparer;
            }
        }
        
        /// <summary>
        /// Gets the value at a specified key using indexing.
        /// </summary>
        /// <param name="key"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public TValue this[TKey key]
        {
            get
            {
                InitialiseIfRequired();
                return dictionary.GetValueOrDefault(key);
            }
            set
            {
                InitialiseIfRequired();
                if (dictionary.ContainsKey(key))
                {
                    dictionary[key] = value;
                }

                if (Application.isEditor)
                {
                    foreach (SerializedKeyValuePair<TKey, TValue> keyValue in serializedDictionary)
                    {
                        if (keyValue.key == null) { continue; }
                        if (keyValue.key is string stringKey && (string.IsNullOrEmpty(stringKey) || string.IsNullOrWhiteSpace(stringKey))) { continue; }
                        if (!keyValue.key.Equals(key)) { continue; }
                        
                        keyValue.value = value;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the number of valid key/value pairs contained in the dictionary.
        /// </summary>
        public int Count
        {
            get
            {
                InitialiseIfRequired();
                return dictionary.Count;
            }
        }

        /// <summary>
        /// Gets a collection containing the valid keys in the dictionary.
        /// </summary>
        public Dictionary<TKey, TValue>.KeyCollection Keys
        {
            get
            {
                InitialiseIfRequired();
                return dictionary.Keys;
            }
        }
        
        /// <summary>
        /// Gets a collection containing the values in the dictionary.
        /// </summary>
        public Dictionary<TKey, TValue>.ValueCollection Values
        {
            get
            {
                InitialiseIfRequired();
                return dictionary.Values;
            }
        }
        
        /// <summary>
        /// Convert to a standard none-serialized dictionary.
        /// </summary>
        /// <returns></returns>
        public Dictionary<TKey, TValue> ToDictionary()
        {
            InitialiseIfRequired();
            return dictionary;
        }
        
        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(TKey key, TValue value)
        {
            InitialiseIfRequired();
            if (!dictionary.TryAdd(key, value)) { return; }

            // handle serialize list to allow visualisation in editor
            if (Application.isEditor)
            {
                serializedDictionary.Add(new SerializedKeyValuePair<TKey, TValue>(key , value));
            }
        }
        
        /// <summary>
        /// Attempts to add the specified key and value to the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>True if key and value successfully added to the dictionary</returns>
        public bool TryAdd(TKey key, TValue value)
        {
            InitialiseIfRequired();
            if (!dictionary.TryAdd(key, value)) { return false; }
            
            // handle serialize list to allow visualisation in editor
            if (Application.isEditor)
            {
                serializedDictionary.Add(new SerializedKeyValuePair<TKey, TValue>(key , value));
            }
            return true;
        }
        
        /// <summary>
        /// Determines whether the dictionary contains a specific key.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool ContainsKey(TKey key)
        {
            InitialiseIfRequired();
            return dictionary.ContainsKey(key);
        }
        
        /// <summary>
        /// Determines whether the dictionary contains a specific value.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool ContainsValue(TValue value)
        {
            InitialiseIfRequired();
            return dictionary.ContainsValue(value);
        }
        
        /// <summary>
        /// Removes all keys and values from the dictionary.
        /// </summary>
        public void Clear()
        {
            InitialiseIfRequired();
            
            // handle dictionary
            dictionary.Clear();
            
            // handle serialize list to allow visualisation in editor
            if (Application.isEditor)
            {
                serializedDictionary.Clear();
            }
        }
        
        /// <summary>
        /// Removes all invalid key-values from the dictionary.
        /// </summary>
        public bool ClearInvalidEntries()
        {
            bool anyEntryRemoved = false;
            for (int i = serializedDictionary.Count - 1; i >= 0; i--)
            {
                SerializedKeyValuePair<TKey, TValue> keyValue = serializedDictionary[i];
                if (!keyValue.IsValidValue())
                {
                    serializedDictionary.RemoveAt(i);
                    anyEntryRemoved = true;
                }
                if (!keyValue.IsValidKey())
                {
                    serializedDictionary.RemoveAt(i);
                    anyEntryRemoved = true;
                }
            }

            if (!anyEntryRemoved) { return false; }
            Initialise();
            return true;
        }
        
        /// <summary>
        /// Ensures that the dictionary can hold up to a specified number of entries without any further expansion of its backing storage.
        /// </summary>
        /// <param name="capacity"></param>
        public void EnsureCapacity(int capacity)
        {
            InitialiseIfRequired();
            dictionary.EnsureCapacity(capacity);
        }
        
        /// <summary>
        /// Returns an enumerator that iterates through the dictionary.
        /// </summary>
        /// <returns></returns>
        public Dictionary<TKey,TValue>.Enumerator GetEnumerator()
        {
            InitialiseIfRequired();
            return dictionary.GetEnumerator();
        }
        
        /// <summary>
        /// Tries to get the value associated with the specified key in the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public TValue GetValueOrDefault(TKey key)
        {
            InitialiseIfRequired();
            return dictionary.GetValueOrDefault(key);
        }
        
        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public bool TryGetValue(TKey key, out TValue value)
        {
            InitialiseIfRequired();
            return dictionary.TryGetValue(key, out value);
        }
        
        /// <summary>
        /// Try to get all keys used for a specific value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="keys"></param>
        /// <returns>Returns true if at least one key exists for a valid value, false otherwise</returns>
        public bool TryGetKeysForValue(TValue value, out List<TKey> keys)
        {
            List<TValue> allValues = Values.ToList();
            List<TKey> allKeys = Keys.ToList();
            
            // should always be true!
            if (allValues.Count != allKeys.Count && 0 < allValues.Count)
            {
                keys = new List<TKey>();
                return false;
            }
            
            keys = new List<TKey>();
            for (int i = 0; i < allValues.Count; i++)
            {
                if (value.Equals(allValues[i]))
                {
                    keys.Add(allKeys[i]);
                }
            }
            return true;
        }
        
        /// <summary>
        /// Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries.
        /// </summary>
        public void TrimExcess()
        {
            InitialiseIfRequired();
            dictionary.TrimExcess();
        }
        
        /// <summary>
        /// Removes the value with the specified key from the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>True if key and value successfully removed from the dictionary</returns>
        public bool Remove(TKey key)
        {
            InitialiseIfRequired();
            
            // handle dictionary
            if (!dictionary.Remove(key)) { return false; }
            
            // handle serialize list to allow visualisation in editor
            if (Application.isEditor)
            {
                for (int i = serializedDictionary.Count - 1; i >= 0; i--)
                {
                    SerializedKeyValuePair<TKey, TValue> keyValue = serializedDictionary[i];
                    if (!keyValue.IsKey(key)) { continue; }
                    serializedDictionary.Remove(keyValue);
                }
            }
            return true;
        }
        
        /// <summary>
        /// Removes the value with the specified key from the dictionary.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>True if key and value successfully removed from the dictionary</returns>
        public bool Remove(TKey key, out TValue value)
        {
            InitialiseIfRequired();
            
            // handle dictionary
            if (!dictionary.Remove(key, out value)) { return false; }
            
            // handle serialize list to allow visualisation in editor
            if (Application.isEditor)
            {
                for (int i = serializedDictionary.Count - 1; i >= 0; i--)
                {
                    SerializedKeyValuePair<TKey, TValue> keyValue = serializedDictionary[i];
                    if (!keyValue.IsKey(key)) { continue; }
                    serializedDictionary.Remove(keyValue);
                }
            }
            return true;
        }

        #endregion
        
    } // class end
}