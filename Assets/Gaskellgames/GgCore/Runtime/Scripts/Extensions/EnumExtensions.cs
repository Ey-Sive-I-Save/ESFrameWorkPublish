using System;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com, unless otherwise stated
    /// </summary>
    
    public static class EnumExtensions
    {
        #region Original code by 'Basheer AL-MOMANI' and 'ShawnFeatherly'

        // https://stackoverflow.com/questions/15388072/how-to-add-extension-methods-to-enums
        
        /// <summary>
        /// Returns the total number of options in the enum
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public static int Count<T>(this T source) where T : IConvertible // enum
        {
            // safety check
            if (!typeof(T).IsEnum) { throw new ArgumentException("T must be an enumerated type"); }
            
            return Enum.GetNames(typeof(T)).Length;
        }
        
        /// <summary>
        /// Returns the int value of the currently selected enum value
        /// </summary>
        /// <param name="source"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static int ToInt<T>(this T source) where T : IConvertible // enum
        {
            // safety check
            if (!typeof(T).IsEnum) { throw new ArgumentException("T must be an enumerated type"); }
            
            IConvertible convertibleValue = source;
            int selected = (int)convertibleValue;
            
            return selected;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Returns the string value of the currently selected enum value
        /// </summary>
        /// <param name="source"></param>
        /// <param name="nicifyName"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ToString<T>(this T source, bool nicifyName = false) where T : IConvertible // enum
        {
            // safety check
            if (!typeof(T).IsEnum) { throw new ArgumentException("T must be an enumerated type"); }

            IConvertible convertibleValue = source;
            string stringName = convertibleValue.ToString();
            
            return nicifyName ? stringName.NicifyName() : stringName;
        }
        
    } // class end
}