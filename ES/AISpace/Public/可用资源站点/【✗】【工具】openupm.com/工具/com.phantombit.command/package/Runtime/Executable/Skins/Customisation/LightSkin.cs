namespace WellFired.Command.Skins.Customisation
{
    internal class LightSkin : DarkSkin
    {
        public override Color MainColor { get; } = new Color(255, 255, 255);
        public override Color SecondaryColor { get; } = new Color(200, 200, 200);
        public override Color ButtonColor { get; } = new Color(255, 255, 255);
        
        public override Color GeneralLabelFontColor { get; } = new Color(60, 60, 60);
        public override Color MainFontColor { get; } = new Color(60, 60, 60);
        public override Color TextEntryFontColor { get; } = new Color(60, 60, 60);

        public override Color EntryInfoColor { get; } = new Color(60, 60, 60);
        public override Color EntryWarningColor { get; } = new Color(255, 194, 0);
        public override Color EntryErrorColor { get; } = new Color(209, 76, 38);
        public override Color EntryExceptionColor { get; } = new Color(209, 76, 38);
    }
}