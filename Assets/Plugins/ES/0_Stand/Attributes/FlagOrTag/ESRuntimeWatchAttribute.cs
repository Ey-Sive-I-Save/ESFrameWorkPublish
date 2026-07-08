using System;

namespace ES
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ESRuntimeWatchAttribute : Attribute
    {
        public string Group;
        public string Label;

        public ESRuntimeWatchAttribute(string group = "Default", string label = null)
        {
            Group = string.IsNullOrEmpty(group) ? "Default" : group;
            Label = label;
        }
    }
}
