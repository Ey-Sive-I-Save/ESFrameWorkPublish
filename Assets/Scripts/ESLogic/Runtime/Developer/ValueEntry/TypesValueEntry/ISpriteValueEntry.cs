using UnityEngine;

namespace ES
{
    public interface ISpriteValueEntry : IValueEntry<Sprite, ValueEntrySpriteKey>
    {
    }

    public sealed class Example_IMessageSpritevalueEntryEasy : ISpriteValueEntry
    {
        public Sprite icon;
        public Sprite highlightedIcon;
        public Sprite selectedIcon;

        public bool TryGetValueEntry(
            ref Sprite back,
            ValueEntrySpriteKey key,
            object help = null,
            EnumCollect.Envir_LanguageType lan = EnumCollect.Envir_LanguageType.NotClear)
        {
            switch (key)
            {
                case ValueEntrySpriteKey.Icon:
                case ValueEntrySpriteKey.DefaultValue:
                    back = icon;
                    return back != null;
                case ValueEntrySpriteKey.Highlighted:
                    back = highlightedIcon != null ? highlightedIcon : icon;
                    return back != null;
                case ValueEntrySpriteKey.Selected:
                    back = selectedIcon != null ? selectedIcon : highlightedIcon != null ? highlightedIcon : icon;
                    return back != null;
                default:
                    return false;
            }
        }
    }
}
