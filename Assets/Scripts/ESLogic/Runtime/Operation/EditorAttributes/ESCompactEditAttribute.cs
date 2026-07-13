using System;
using Sirenix.OdinInspector;

namespace ES
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ESCompactEditAttribute : Attribute
    {
        public readonly string title;

        public ESCompactEditAttribute(string title = null)
        {
            this.title = title;
        }
    }
}
