namespace WellFired.Command.Skins.Customisation
{
    internal class PurpleSkin : DarkSkin
    {
        public override Color MainColor { get; } = new Color(100, 100, 100);
        public override Color MainFontColor { get; } = new Color(100, 100, 100);
        public override Color SecondaryColor { get; } = new Color(75, 81, 158);
        public override Color ButtonColor { get; } = new Color(255, 255, 255);
        public override Color TextEntryFontColor { get; } = new Color(75, 81, 158);
    }
}