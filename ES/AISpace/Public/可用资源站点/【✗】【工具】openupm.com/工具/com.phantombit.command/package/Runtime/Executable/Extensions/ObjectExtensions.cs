using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace WellFired.Command.Extensions
{
    public static class ExtensionMethods
    {
        public static void CopyProperties<TFrom, TTo>(this TFrom @from, TTo to)
        {
            var pFrom = from prop in typeof(TFrom).GetProperties() where prop.CanRead select prop;
            var pTo = (from prop in typeof(TTo).GetProperties() where prop.CanWrite select prop).ToArray();

            foreach (var prop in pFrom)
            {
                var matching = FindMatchingProperty(pTo, prop);
                matching?.SetValue(to, prop.GetValue(@from, null), null);
            }
        }

        private static PropertyInfo FindMatchingProperty(IEnumerable<PropertyInfo> pTo, PropertyInfo prop)
        {
            return pTo.FirstOrDefault(o => o.Name == prop.Name);
        }
    }
}