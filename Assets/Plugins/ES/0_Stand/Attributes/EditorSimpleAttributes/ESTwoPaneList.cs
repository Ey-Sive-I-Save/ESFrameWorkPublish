using System;

namespace ES
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ESTwoPaneListAttribute : Attribute
    {
        public readonly string itemLabelMember;
        public readonly float leftWidth;
        public readonly float minHeight;
        public readonly bool showIndex;
        public readonly string leftTitle;
        public readonly string rightTitle;
        public readonly bool searchable;

        public ESTwoPaneListAttribute(
            string itemLabelMember = "",
            float leftWidth = 220f,
            float minHeight = 360f,
            bool showIndex = false,
            string leftTitle = "列表",
            string rightTitle = "详情",
            bool searchable = true)
        {
            this.itemLabelMember = itemLabelMember;
            this.leftWidth = leftWidth;
            this.minHeight = minHeight;
            this.showIndex = showIndex;
            this.leftTitle = leftTitle;
            this.rightTitle = rightTitle;
            this.searchable = searchable;
        }
    }
}
