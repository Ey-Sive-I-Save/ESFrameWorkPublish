using System;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public static class ColorExtensions
    {
        #region Hex Conversions

        /// <summary>
        /// Converts RGB color values to Hex value.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static string RGBToHex(this Color color)
        {
            return RGBToHex((Color32)color);
        }
        
        /// <summary>
        /// Converts RGB color32 values to Hex value.
        /// </summary>
        /// <param name="color32"></param>
        /// <returns></returns>
        public static string RGBToHex(this Color32 color32)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color32.r, color32.g, color32.b);
        }
        
        /// <summary>
        /// Converts Hex color values to RGB value.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static Color32 HexToRGB(this string hex)
        {
            string subString_Check = hex.Substring(0, 7);

            if (subString_Check != hex)
            {
                GgLogs.Log(null, GgLogType.Error, "Invalid string hex code: {0}", hex);
                return new Color32();
            }
            
            return new Color32(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16),
                255
            );
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Hexa Conversions
        
        /// <summary>
        /// Converts RGBA color values to Hex value.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public static string RGBAToHexa(this Color color)
        {
            return RGBAToHexa((Color32)color);
        }
        
        /// <summary>
        /// Converts RGBA color32 values to Hex value.
        /// </summary>
        /// <param name="color32"></param>
        /// <returns></returns>
        public static string RGBAToHexa(this Color32 color32)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}", color32.r, color32.g, color32.b, color32.a);
        }
        
        /// <summary>
        /// Converts Hex color values to RGBA value.
        /// </summary>
        /// <param name="hexa"></param>
        /// <returns></returns>
        public static Color32 HexaToRGBA(this string hexa)
        {
            string subString_Check = hexa.Substring(0, 9);

            if (subString_Check != hexa)
            {
                GgLogs.Log(null, GgLogType.Error, "Invalid string hexa code: {0}", hexa);
                return new Color32();
            }
            
            return new Color32(
                Convert.ToByte(hexa.Substring(1, 2), 16),
                Convert.ToByte(hexa.Substring(3, 2), 16),
                Convert.ToByte(hexa.Substring(5, 2), 16),
                Convert.ToByte(hexa.Substring(7, 2), 16)
                );
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region HDR Conversions

        private const byte k_MaxByteForOverexposedColor = 191; //internal Unity const
        
        /// <summary>
        /// Convert a color value to a HDRColor.
        /// </summary>
        /// <param name="baseColor"></param>
        /// <returns>HDR Color</returns>
        public static Color ColorToHDRColor(this Color baseColor)
        {    
            float maxColorComponent = baseColor.maxColorComponent;
            float scaleFactor = k_MaxByteForOverexposedColor / maxColorComponent;
            float exposure = Mathf.Log(255f / scaleFactor) / Mathf.Log(2f);
            float multiplier = 1 / exposure;
            
            float hdrRed = baseColor.r * multiplier;
            float hdrGreen = baseColor.g * multiplier;
            float hdrBlue = baseColor.b * multiplier;
            
            return new Color(hdrRed, hdrGreen, hdrBlue, baseColor.a);
        }
        
        /// <summary>
        /// Split a HDRColor value into it's base color and exposure values.
        /// </summary>
        /// <param name="hdrColor"></param>
        /// <param name="baseColor"></param>
        /// <param name="exposure"></param>
        public static void DecomposeHDRColor(this Color hdrColor, out Color32 baseColor, out float exposure)
        {
            baseColor = hdrColor;
            float maxColorComponent = hdrColor.maxColorComponent;
            
            // replicate Photoshops's decomposition behaviour
            if (maxColorComponent == 0f || maxColorComponent <= 1f && maxColorComponent >= 1 / 255f)
            {
                exposure = 0f;
                baseColor.r = (byte)Mathf.RoundToInt(hdrColor.r * 255f);
                baseColor.g = (byte)Mathf.RoundToInt(hdrColor.g * 255f);
                baseColor.b = (byte)Mathf.RoundToInt(hdrColor.b * 255f);
            }
            else
            {
                // calibrate exposure to the max float color component
                float scaleFactor = k_MaxByteForOverexposedColor / maxColorComponent;
                exposure = Mathf.Log(255f / scaleFactor) / Mathf.Log(2f);

                // maintain max integrity of byte values: prevents off-by-one errors when scaling up a color one channel at a time
                baseColor.r = Math.Min(k_MaxByteForOverexposedColor, (byte)Mathf.CeilToInt(scaleFactor * hdrColor.r));
                baseColor.g = Math.Min(k_MaxByteForOverexposedColor, (byte)Mathf.CeilToInt(scaleFactor * hdrColor.g));
                baseColor.b = Math.Min(k_MaxByteForOverexposedColor, (byte)Mathf.CeilToInt(scaleFactor * hdrColor.b));
            }
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Intensity Packing
        
        /// <summary>
        /// Packs an intensity value [0-1] into a base color32 value.
        /// </summary>
        /// <param name="baseColor">Base color32 value to pack intensity into.</param>
        /// <param name="intensity">Intensity value [0-1]</param>
        /// <returns>New Color32 value based on the base color and intensity values.</returns>
        /// <remarks>WARNING: This is a destructive function! The base color and intensity cannot be extracted from the resultant value!</remarks>
        public static Color32 PackIntensity(this Color32 baseColor, float intensity)
        {
            float t = Mathf.Clamp(intensity, 0f, 1f);
            return new Color32((byte)(baseColor.r * t), (byte)(baseColor.g * t), (byte)(baseColor.b * t), baseColor.a);
        }
        
        /// <summary>
        /// Packs an intensity value [0-1] into a base color value.
        /// </summary>
        /// <param name="baseColor">Base color value to pack intensity into.</param>
        /// <param name="intensity">Intensity value [0-1]</param>
        /// <returns>New Color value based on the base color and intensity values.</returns>
        /// <remarks>WARNING: This is a destructive function! The base color and intensity cannot be extracted from the resultant value!</remarks>
        public static Color PackIntensity(this Color baseColor, float intensity)
        {
            float t = Mathf.Clamp(intensity, 0f, 1f);
            return new Color(baseColor.r * t, baseColor.g * t, baseColor.b * t, baseColor.a);
        }
        
        #endregion
        
    } // class end
}