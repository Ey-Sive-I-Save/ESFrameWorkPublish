using JetBrains.Annotations;

namespace WellFired.Command.Skins
{
    public class Skin
    {
        [PublicAPI]
        public SkinData Data { get; }

        private Skin(SkinData skinData)
        {
            Data = skinData;
        }

        public static Skin From(SkinData skinData)
        {
            return new Skin(skinData);
        }
    }
}