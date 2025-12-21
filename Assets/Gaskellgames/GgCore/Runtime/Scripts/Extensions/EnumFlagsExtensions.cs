using System.Collections.Generic;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public static class EnumFlagsExtensions
    {
        #region ToggleFlag

        /// <summary>
        /// Toggle the value of a flag within an enumValueFlag
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <param name="flagToSet"></param>
        public static void ToggleFlag(this ref int enumValueFlag, int flagToSet)
        {
            // handle special case: all
            if (flagToSet.IsFlagNone())
            {
                enumValueFlag = 0;
                return;
            }
            
            // handle special case: none
            if (flagToSet.IsFlagAll())
            {
                enumValueFlag = ~0;
                return;
            }

            // handle other cases
            if (enumValueFlag.HasAllFlags(flagToSet))
            {
                // unset flag
                enumValueFlag &= (~flagToSet);
            }
            else
            {
                // set flag
                enumValueFlag |= flagToSet;
            }
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region SetFlag

        /// <summary>
        /// Set the value of a flag within an enumValueFlag to true
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <param name="flagToSet"></param>
        public static void SetFlag(this ref int enumValueFlag, int flagToSet)
        {
            // handle special case: all
            if (flagToSet.IsFlagAll())
            {
                enumValueFlag = ~0;
                return;
            }
            
            // handle special case: none
            if (flagToSet.IsFlagNone())
            {
                enumValueFlag = 0;
                return;
            }
            
            // handle other cases
            enumValueFlag |= flagToSet;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region UnsetFlag

        /// <summary>
        /// Set the value of a flag within an enumValueFlag to false
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <param name="flagToUnset"></param>
        public static void UnsetFlag(this ref int enumValueFlag, int flagToUnset)
        {
            // handle special case: all
            if (flagToUnset.IsFlagAll())
            {
                enumValueFlag = 0;
                return;
            }
            
            // handle special case: none
            if (flagToUnset.IsFlagNone())
            {
                enumValueFlag = ~0;
                return;
            }
            
            // handle other cases
            enumValueFlag &= (~flagToUnset);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region HasAnyFlag

        /// <summary>
        /// Get whether an enumValueFlag contains any flag from another enumValueFlag
        /// </summary>
        /// <param name="enumValueFlagA"></param>
        /// <param name="enumValueFlagB"></param>
        /// <returns></returns>
        public static bool HasAnyFlag(this int enumValueFlagA, int enumValueFlagB)
        {
            // handle other cases
            return !(enumValueFlagA & enumValueFlagB).Equals(0);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region HasAllFlags

        /// <summary>
        /// Get whether an enumValueFlag contains all flags from another enumValueFlag
        /// </summary>
        /// <param name="enumValueFlagA"></param>
        /// <param name="enumValueFlagB"></param>
        /// <returns></returns>
        public static bool HasAllFlags(this int enumValueFlagA, int enumValueFlagB)
        {
            int and = enumValueFlagA & enumValueFlagB;
            return and == enumValueFlagB;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region IsFlag
        
        /// <summary>
        /// Get whether an enumValueFlag represents the 'none' enumValueFlag (0)
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <returns></returns>
        public static bool IsFlagNone(this int enumValueFlag)
        {
            return enumValueFlag.Equals(0);
        }
        
        /// <summary>
        /// Get whether an enumValueFlag represents the 'all' enumValueFlag
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <returns></returns>
        public static bool IsFlagAll(this int enumValueFlag)
        {
            return enumValueFlag.Equals(~0);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region FlagCount
        
        /// <summary>
        /// Get the number of flags an enumValueFlag represents
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <returns></returns>
        public static int FlagCount(this int enumValueFlag)
        {
            int count = 0;
            while (enumValueFlag != 0)
            {
                enumValueFlag &= (enumValueFlag - 1);
                count++;
            }
            return count;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region FlagAsIndexes
        
        /// <summary>
        /// Get the index values that an enumValueFlag represents
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <returns></returns>
        public static List<int> FlagAsIndexes(this int enumValueFlag)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                if (GgMaths.GetBitfieldValue(enumValueFlag, i))
                {
                    indexes.Add(i);
                }
            }
            
            return indexes;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region SetFlagAtIndex
        
        /// <summary>
        /// Set the value of an enumValueFlag at a set index
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <param name="index"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static void SetFlagAtIndex(this int enumValueFlag, int index, bool value)
        {
            GgMaths.SetBitfieldValue(ref enumValueFlag, index, value);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region HasFlagAtIndex
        
        /// <summary>
        /// Get whether an enumValueFlag contains a set index
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static bool HasFlagAtIndex(this int enumValueFlag, int index)
        {
            return 0 <= index && index < 32 ? GgMaths.GetBitfieldValue(enumValueFlag, index) : false;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region FlagAsString

        /// <summary>
        /// Convert an enumValueFlag to it's string value
        /// </summary>
        /// <param name="enumValueFlag"></param>
        /// <returns></returns>
        public static string FlagAsString(this int enumValueFlag)
        {
            return GgMaths.BitfieldAsString(enumValueFlag);
        }

        #endregion
        
    } // class end
}