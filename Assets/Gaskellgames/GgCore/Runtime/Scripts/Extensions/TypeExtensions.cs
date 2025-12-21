using System;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class TypeExtensions
    {
        /// <summary>
        /// Returns true if the comparisonType is the same as or a subclass of a base class.
        /// </summary>
        /// <param name="comparisonType"></param>
        /// <param name="baseClass"></param>
        /// <returns></returns>
        public static bool IsSameOrSubclass(Type comparisonType, Type baseClass)
        {
            return comparisonType.IsSubclassOf(baseClass) || comparisonType == baseClass;
        }

        /// <summary>
        /// Returns true if the parent type is generic and the child type implements it.
        /// </summary>
        public static bool IsGenericSubclass(Type parent, Type child)
        {
            if (!parent.IsGenericType)
            {
                return false;
            }

            Type currentType = child;
            bool Subclass = false;
            while (!Subclass && currentType != null)
            {
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == parent.GetGenericTypeDefinition())
                {
                    Subclass = true;
                    break;
                }
                currentType = currentType.BaseType;
            }
            return Subclass;
        }

    } // class end
}