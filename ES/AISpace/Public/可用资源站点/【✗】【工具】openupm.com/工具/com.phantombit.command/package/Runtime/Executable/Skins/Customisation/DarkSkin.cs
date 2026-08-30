namespace WellFired.Command.Skins.Customisation
{
    public class DarkSkin : ISkinData
    {
        public virtual Color ButtonColor { get; } = new Color(60, 60, 60);
        public virtual Color ButtonHoverColor { get; } = new Color(80, 80, 80);
        public virtual Color MainColor { get; } = new Color(40, 40, 40);
        public virtual Color SecondaryColor { get; } = new Color(90, 90, 90);
        public virtual Color MainFontColor { get; } = new Color(255, 255, 255);
        public virtual Color TextEntryColor { get; } = new Color(255, 255, 255);
        public virtual Color TextEntryFontColor { get; } = new Color(0, 0, 0);
        public virtual Color EntryExceptionColor { get; } = new Color(209, 76, 38);
        public virtual Color EntryErrorColor { get; } = new Color(209, 76, 38);
        public virtual Color EntryWarningColor { get; } = new Color(236, 227, 2);
        public virtual Color EntryInfoColor { get; } = new Color(255, 255, 255);
        public virtual Color DetailedLogMessageBackgroundColor { get; } = new Color(255, 255, 255);
        public virtual Color DetailedLogMessageColor { get; } = new Color(0, 0, 0);
        public virtual Color GeneralLabelFontColor { get; } = new Color(255, 255, 255);
        public virtual Color PlaceholderTextEntryColor { get; set; } = new Color(255, 255, 255);
        public virtual Color PlaceholderTextEntryFontColor { get; set; } = new Color(159, 159, 159);

        public virtual int FontSize { get; } = 24;
        
        public virtual int ButtonSpacing { get; } = 5;
        public virtual int ButtonSpacingTouch { get; } = 10;

        public virtual int EntryHeight { get; } = 28;

        public virtual int ButtonPaddingKeyboard { get; } = 10;
        public virtual int ButtonPaddingTouch { get; } = 20;

        public virtual int HeaderPaddingKeyboard { get; } = 5;
        public virtual int HeaderPaddingTouch { get; } = 5;
    }
}