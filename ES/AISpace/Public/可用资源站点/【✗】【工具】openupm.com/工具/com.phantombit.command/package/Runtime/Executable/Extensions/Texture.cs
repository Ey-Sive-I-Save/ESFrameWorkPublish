using UnityEngine;

namespace WellFired.Command.Unity.Runtime.Extensions
{
    public static class Texture
    {
        public static Texture2D Texture2D(UnityEngine.Color color, int size)
        {
            var pix = new UnityEngine.Color[size * size];
            for (var i = 0; i < pix.Length; i++)
                pix[i] = color;

            var result = new Texture2D(size, size)
            {
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            result.SetPixels(pix);
            result.Apply();
			
            return result;
        }
    }
}