namespace WellFired.Command.Skins.Customisation
{
    internal class DarkOlive : DarkSkin
    {
        public override Color MainColor { get; } = new Color(100, 100, 100);
        public override Color MainFontColor { get; } = new Color(100, 100, 100);
        public override Color SecondaryColor { get; } = new Color(85, 75, 38);
        public override Color ButtonColor { get; } = new Color(255, 255, 255);
        public override Color TextEntryFontColor { get; } = new Color(85, 75, 38);
    }
}