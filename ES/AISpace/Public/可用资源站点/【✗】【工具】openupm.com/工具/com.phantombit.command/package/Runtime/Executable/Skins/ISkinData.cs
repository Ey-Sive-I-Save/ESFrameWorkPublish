namespace WellFired.Command.Skins
{
    public interface ISkinData
    {
        Color MainColor { get; }
        Color SecondaryColor { get; }
        Color MainFontColor { get; }
        Color TextEntryFontColor { get; }
        Color TextEntryColor { get; }
        Color ButtonColor { get; }
        Color ButtonHoverColor { get; }
        Color EntryExceptionColor { get; }
        Color EntryErrorColor { get; }
        Color EntryWarningColor { get; }
        Color EntryInfoColor { get; }
        Color DetailedLogMessageBackgroundColor { get; }
        Color DetailedLogMessageColor { get; }
        Color GeneralLabelFontColor { get; }
        
        Color PlaceholderTextEntryColor { get; set; }
        Color PlaceholderTextEntryFontColor { get; set; }
        
        int FontSize { get; }
        int ButtonSpacing { get; }
        int ButtonSpacingTouch { get; }
        int EntryHeight { get; }
        int ButtonPaddingKeyboard { get; }
        int ButtonPaddingTouch { get; }
        int HeaderPaddingKeyboard { get; }
        int HeaderPaddingTouch { get; }
    }
}