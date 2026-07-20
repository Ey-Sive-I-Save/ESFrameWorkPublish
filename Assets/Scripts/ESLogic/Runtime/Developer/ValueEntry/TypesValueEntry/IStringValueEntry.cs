namespace ES
{
    public interface IStringValueEntry : IValueEntry<string, ValueEntryStringKey>
    {
    }

    public sealed class Example_IMessageStringvalueEntryEasy : IStringValueEntry
    {
        public string displayName = "示例名称";
        public string description = "示例描述";

        public bool TryGetValueEntry(
            ref string back,
            ValueEntryStringKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear)
        {
            lan.ToClear();

            switch (key)
            {
                case ValueEntryStringKey.Name:
                case ValueEntryStringKey.DefaultValue:
                    back = displayName;
                    return true;
                case ValueEntryStringKey.Description:
                case ValueEntryStringKey.Content:
                    back = description;
                    return true;
                default:
                    return false;
            }
        }
    }
}
