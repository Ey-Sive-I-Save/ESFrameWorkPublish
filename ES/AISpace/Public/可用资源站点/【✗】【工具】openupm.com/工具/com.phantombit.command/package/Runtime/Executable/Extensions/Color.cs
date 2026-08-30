namespace WellFired.Command.Unity.Runtime.Extensions
{
    public static class Color
    {
        public static UnityEngine.Color ToColor(this Skins.Color color)
        {
            return new UnityEngine.Color(color.Red / 255.0f, color.Green / 255.0f, color.Blue / 255.0f);
        }
    }
}