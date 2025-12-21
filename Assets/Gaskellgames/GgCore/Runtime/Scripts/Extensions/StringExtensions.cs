using System;
using System.Text;
using UnityEngine;

namespace Gaskellgames
{
    /// <summary>
    /// Code created by Gaskellgames: https://gaskellgames.com, unless otherwise stated
    /// </summary>
    
    public static class StringExtensions
    {
        #region Original code by ErnSur

        // https://gist.github.com/ErnSur/842d72606159fdd865979c4d9db21a18
        
        /// <summary>
        /// This function will insert spaces before capital letters and remove optional m_, _ or k followed by uppercase letter in front of the name.
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static string NicifyName(this string stringValue)
        {
            StringBuilder result = new StringBuilder(stringValue.Length * 2);

            bool prevIsLetter = false;
            bool prevIsLetterUpper = false;
            bool prevIsDigit = false;
            bool prevIsStartOfWord = false;
            bool prevIsNumberWord = false;

            int firstCharIndex = 0;
            if (stringValue.StartsWith('_'))
            {
                firstCharIndex = 1;
            }
            else if (stringValue.StartsWith("m_"))
            {
                firstCharIndex = 2;
            }

            for (int i = stringValue.Length - 1; i >= firstCharIndex; i--)
            {
                char currentChar = stringValue[i];
                bool currIsLetter = char.IsLetter(currentChar);
                if (i == firstCharIndex && currIsLetter)
                {
                    currentChar = char.ToUpper(currentChar);
                }
                bool currIsLetterUpper = char.IsUpper(currentChar);
                bool currIsDigit = char.IsDigit(currentChar);
                bool currIsSpacer = currentChar == ' ' || currentChar == '_';

                bool addSpace = (currIsLetter && !currIsLetterUpper && prevIsLetterUpper) ||
                                (currIsLetter && prevIsLetterUpper && prevIsStartOfWord) ||
                                (currIsDigit && prevIsStartOfWord) ||
                                (!currIsDigit && prevIsNumberWord) ||
                                (currIsLetter && !currIsLetterUpper && prevIsDigit);

                if (!currIsSpacer && addSpace)
                {
                    result.Insert(0, ' ');
                }

                result.Insert(0, currentChar);
                prevIsStartOfWord = currIsLetter && currIsLetterUpper && prevIsLetter && !prevIsLetterUpper;
                prevIsNumberWord = currIsDigit && prevIsLetter && !prevIsLetterUpper;
                prevIsLetterUpper = currIsLetter && currIsLetterUpper;
                prevIsLetter = currIsLetter;
                prevIsDigit = currIsDigit;
            }

            return result.ToString();
        }

        #endregion

        //----------------------------------------------------------------------------------------------------
        
        /// <summary>
        /// Get the width and height of a string using 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static Vector2 GetStringBounds(string stringValue, int fontSize = 0)
        {
            GUIStyle guiStyle = GUI.skin.GetStyle("Box");
            guiStyle.fontSize = fontSize;
    
            return guiStyle.CalcSize(new GUIContent(stringValue));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static float GetStringWidth(string stringValue, int fontSize = 0)
        {
            return GetStringBounds(stringValue, fontSize).x;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static float GetStringHeight(string stringValue, int fontSize = 0)
        {
            return GetStringBounds(stringValue, fontSize).y;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stringValue"></param>
        /// <returns></returns>
        public static float GetWrappedStringHeight(string stringValue, float maxWidth, int fontSize = 0)
        {
            GUIStyle guiStyle = GUI.skin.GetStyle("Box");
            guiStyle.fontSize = fontSize;
    
            return guiStyle.CalcHeight(new GUIContent(stringValue), maxWidth);
        }
        
        /// <summary>
        /// Return a three digit int as a 'nicified' string value for the number.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>0-9 will be returned as 000-009, 10-99 will be returned as 010-099</returns>
        public static string NicifyNumberAsString(int value)
        {
            // negatives
            if (value < 0) { return value.ToString(); }

            // 000-009
            if (value < 10) { return $"00{value}"; }

            // 010-099
            if (value < 100) { return $"0{value}"; }

            // 100+
            return value.ToString();
        }
        
        /// <summary>
        /// Get substring upto a set character or full string, whichever comes first.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="stopAt"></param>
        /// <returns></returns>
        public static string GetUntilOrEmpty(this string text, string stopAt)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                int charLocation = text.IndexOf(stopAt, StringComparison.Ordinal);

                if (charLocation > 0)
                {
                    return text.Substring(0, charLocation);
                }
            }

            return string.Empty;
        }

    } // class end
}
