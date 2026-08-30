using WellFired.Command.Skins;
using WellFired.Command.Skins.Customisation;

namespace WellFired.Command
{
    public enum Theme
    {
        Default,
        Light,
        SkyBlue,
        Green,
        Purple,
        DarkOlive
    }
    
    [System.Serializable]
    public class Settings
    {
        public Theme theme = Theme.Default;

        [System.NonSerialized]
        public SkinData skinData;

        public void LoadSkinData()
        {
            switch (theme)
            {
                case Theme.Default:
                    skinData = SkinData.From(new DarkSkin());
                    break;
                case Theme.Light:
                    skinData = SkinData.From(new LightSkin());
                    break;
                case Theme.SkyBlue:
                    skinData = SkinData.From(new SkyBlueSkin());
                    break;
                case Theme.Green:
                    skinData = SkinData.From(new GreenSkin());
                    break;
                case Theme.Purple:
                    skinData = SkinData.From(new PurpleSkin());
                    break;
                case Theme.DarkOlive:
                    skinData = SkinData.From(new DarkOlive());
                    break;
                default:
                    skinData = SkinData.From(new DarkSkin());
                    break;
            }
        }
    }
}