using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class InspectorExtensions
    {
        #region Static Variables

        // blank
        public static readonly Color32 blankColor = new Color32(000, 000, 000, 000);
        public static readonly Color32 cyanColor = new Color32(000, 179, 223, 255);
        public static readonly Color32 yellowColor = new Color32(223, 179, 000, 255);
        public static readonly Color32 redColor = new Color32(223, 050, 050, 255);
        public static readonly Color32 greenColor = new Color32(079, 179, 079, 255);
        
        public static readonly Color32 yellowColorDark = new Color32(128, 079, 000, 255);
        public static readonly Color32 redColorDark = new Color32(128, 050, 050, 255);
        public static readonly Color32 greenColorDark = new Color32(050, 128, 050, 255);
        
        // background
        public static readonly Color32 backgroundNormalColorLight = new Color32(056, 056, 056, 255);
        public static readonly Color32 backgroundNormalColor = new Color32(051, 051, 051, 255);
        public static readonly Color32 backgroundNormalColorDark = new Color32(045, 045, 045, 255);
        public static readonly Color32 backgroundActiveColor = new Color32(044, 093, 135, 255);
        public static readonly Color32 backgroundHoverColor = new Color32(056, 056, 056, 255);
        
        public static readonly Color32 backgroundShadowColor = new Color32(042, 042, 042, 255);
        public static readonly Color32 backgroundInfoBoxColor = new Color32(064, 064, 064, 255);
        
        public static readonly Color32 backgroundSeperatorColor = new Color32(079, 079, 079, 255);
        public static readonly Color32 backgroundSeperatorColorDark = new Color32(035, 035, 035, 255);
        
        // button - normal, hover, active, selected
        public static readonly Color32 buttonNormalColorDark = new Color32(150, 150, 150, 255);
        public static readonly Color32 buttonHoverColor = new Color32(099, 099, 099, 255);
        public static readonly Color32 buttonHoverBorderColor = new Color32(028, 028, 028, 255);
        public static readonly Color32 buttonSelectedColor = new Color32(128, 179, 223, 255);
        public static readonly Color32 buttonActiveColor = new Color32(000, 128, 223, 255);
        public static readonly Color32 buttonActiveBorderColor = new Color32(010, 010, 010, 255);
        
        // text
        public static readonly Color32 textNormalColor = new Color32(179, 179, 179, 255);
        public static readonly Color32 textDisabledColor = new Color32(113, 113, 113, 255);

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Textures
        
        public static Texture2D CreateTexture(int width, int height, int border, bool isRounded, Color32 backgroundColor, Color32 borderColor)
        {
            Color[] pixels = new Color[width * height];
            int pixelIndex = 0;

            if (isRounded)
            {
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        // if at corner add corner color
                        if ((i < border || i >= width - border) && (j < border || j >= width - border))
                        {
                            pixels[pixelIndex] = blankColor;
                        }
                        // otherwise if on border... 
                        else if ((i < border || i >= width - border || j < border || j >= width - border)
                                 || ((i < border*2 || i >= width - border*2) && (j < border*2 || j >= width - border*2)))
                        {
                            // ... add border color
                            pixels[pixelIndex] = borderColor;
                        }
                        else
                        {
                            // ... otherwise add background color
                            pixels[pixelIndex] = backgroundColor;
                        }

                        pixelIndex++;
                    }
                }
            }
            else
            {
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        // if on border... 
                        if (i < border || i >= width - border || j < border || j >= width - border)
                        {
                            // ... add border color
                            pixels[pixelIndex] = borderColor;
                        }
                        else
                        {
                            // ... otherwise add background color
                            pixels[pixelIndex] = backgroundColor;
                        }

                        pixelIndex++;
                    }
                }
            }
   
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }
        
        public static Texture2D TintTexture(Texture2D texture2D, Color tint)
        {
            // null check
            if (!texture2D) { return null; }
            
            int width = texture2D.width;
            int height = texture2D.height;
            Color[] pixels = new Color[width * height];
            int pixelIndex = 0;

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    pixels[pixelIndex] = texture2D.GetPixel(j, i) * tint;
                    pixelIndex++;
                }
            }
   
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }
        
        public static Texture2D AddBorderToTexture(Texture2D texture2D, Color borderColor, int borderThickness)
        {
            // null check
            if (!texture2D) { return null; }
            
            int width = texture2D.width;
            int height = texture2D.height;
            Color[] pixels = new Color[width * height];
            int pixelIndex = 0;
            
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    // if on border... 
                    if (i < borderThickness || i >= width - borderThickness || j < borderThickness || j >= width - borderThickness)
                    {
                        // ... add border color
                        pixels[pixelIndex] = borderColor;
                    }
                    else
                    {
                        // ... otherwise get pixel color
                        pixels[pixelIndex] = pixels[pixelIndex] = texture2D.GetPixel(j, i);
                    }
                    
                    pixelIndex++;
                }
            }
   
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pixels);
            result.Apply();
            return result;
        }

        #endregion
        
    } // class end
}
