using System;
using System.Collections.Generic;
using System.Linq;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class ListExtensions
    {
        /// <summary>
        /// Increases or decrease the number of items in a list to a specified count.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="count"></param>
        public static void SetLength<T>(this IList<T> list, int count)
        {
            // null check
            if (list == null) { return; }
            if (count < 0) { return; }
            
            if (list.GetType().IsArray)
            {
                // update array length
                if (list.Count == count) { return; }
                T[] array = (T[]) list;
                Array.Resize<T>(ref array, count);
                list = (IList<T>) array;
            }
            else
            {
                // update list count
                while (list.Count < count)
                {
                    list.Add(default (T));
                }
                while (list.Count > count)
                {
                    list.RemoveAt(list.Count - 1);
                }
            }
        }
        
        /// <summary>
        /// Add an object to a list without duplication
        /// </summary>
        /// <param name="list"></param>
        /// <param name="item"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if item is successfully added; otherwise false. Note: this method also returns false if item was already found in the list.</returns>
        public static bool TryAdd<T>(this List<T> list, T item)
        {
            if (list.Contains(item)) { return false; }
            list.Add(item);
            return true;
        }
        
        /// <summary>
        /// Add a range of objects to a list without duplication
        /// </summary>
        /// <param name="list"></param>
        /// <param name="range"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if any item is successfully added; otherwise false.</returns>
        public static bool TryAddRange<T>(this List<T> list, List<T> range)
        {
            int oldCount = list.Count;
            list.AddRange(range.Except(list));
            return oldCount != list.Count;
        }
        
        /// <summary>
        /// Remove a range of objects from a list.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="range"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if any item is successfully removed; otherwise false.</returns>
        public static bool TryRemoveRange<T>(this List<T> list, List<T> range)
        {
            int oldCount = list.Count;
            foreach (T obj in range)
            {
                list.Remove(obj);
            }
            return oldCount != list.Count;
        }
        
        /// <summary>
        /// Remove all Null references in the list.
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if any element was removed.</returns>
        public static bool RemoveNulls<T>(this List<T> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] != null) { continue; }
                list.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove all duplicate references in the list.
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>True if any element was removed.</returns>
        public static bool RemoveDuplicates<T>(this List<T> list)
        {
            List<T> newList = new HashSet<T>(list).ToList();
            
            if (list.Count == newList.Count) { return false; }
            list.Clear();
            list.AddRange(newList);
            return true;
        }

    } // class end
}
