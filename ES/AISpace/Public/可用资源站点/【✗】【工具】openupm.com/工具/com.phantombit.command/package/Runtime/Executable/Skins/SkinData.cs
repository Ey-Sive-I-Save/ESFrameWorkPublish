using JetBrains.Annotations;
using WellFired.Command.Extensions;

namespace WellFired.Command.Skins
{
    public class SkinData : ISkinData
    {
        public Color MainColor { get; [UsedImplicitly] set; }
        public Color SecondaryColor { get; [UsedImplicitly] set; }
        public Color MainFontColor { get; [UsedImplicitly] set; }
        public Color TextEntryFontColor { get; [UsedImplicitly] set; }
        public Color TextEntryColor { get; [UsedImplicitly] set; }
        public Color ButtonColor { get; [UsedImplicitly] set; }
        public Color ButtonHoverColor { get; [UsedImplicitly] set; }
        public Color EntryExceptionColor { get; [UsedImplicitly] set; }
        public Color EntryErrorColor { get; [UsedImplicitly] set; }
        public Color EntryWarningColor { get; [UsedImplicitly] set; }
        public Color EntryInfoColor { get; [UsedImplicitly] set; }
        public Color DetailedLogMessageBackgroundColor { get; [UsedImplicitly] set; }
        public Color DetailedLogMessageColor { get; [UsedImplicitly] set; }
        public Color GeneralLabelFontColor { get; [UsedImplicitly] set; }
        public Color PlaceholderTextEntryColor { get; [UsedImplicitly] set; }
        public Color PlaceholderTextEntryFontColor { get; [UsedImplicitly] set; }
        public int FontSize { get; [UsedImplicitly] set; }
        public int ButtonSpacing { get; [UsedImplicitly] set; }
        public int ButtonSpacingTouch { get; [UsedImplicitly] set; }
        public int EntryHeight { get; [UsedImplicitly] set; }
        public int ButtonPaddingKeyboard { get; [UsedImplicitly] set; }
        public int ButtonPaddingTouch { get; [UsedImplicitly] set; }
        public int HeaderPaddingKeyboard { get; [UsedImplicitly] set; }
        public int HeaderPaddingTouch { get; [UsedImplicitly] set; }

        public static SkinData From(ISkinData from)
        {
            var to = new SkinData();
            
            // We have to leave this here, Unity's il2cpp compiler cannot autodetect this. :(
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            from.CopyProperties(to);
            
            return to;
        }
    }
}