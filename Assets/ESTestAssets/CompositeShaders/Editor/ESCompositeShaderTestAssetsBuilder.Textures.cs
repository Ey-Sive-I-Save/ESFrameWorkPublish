using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ES.TestAssets.Editor
{
    internal static partial class ESCompositeShaderTestAssetsBuilder
    {
        private static GeneratedTextures CreateTextures()
        {
            string iconPath = TextureRoot + "/TestIcon_RGBA.png";
            string noisePath = TextureRoot + "/TestNoise_Grayscale.png";
            string flowPath = TextureRoot + "/TestFlow_Directional.png";
            string sequencePath = TextureRoot + "/TestSequence_4x4.png";

            WritePng(iconPath, CreateIconTexture());
            WritePng(noisePath, CreateNoiseTexture());
            WritePng(flowPath, CreateFlowTexture());
            WritePng(sequencePath, CreateSequenceTexture());
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ConfigureTextureImporter(iconPath, true, true, false, FilterMode.Bilinear, TextureWrapMode.Clamp);
            ConfigureTextureImporter(noisePath, false, false, true, FilterMode.Bilinear, TextureWrapMode.Repeat);
            ConfigureTextureImporter(flowPath, false, false, true, FilterMode.Bilinear, TextureWrapMode.Repeat);
            ConfigureTextureImporter(sequencePath, false, true, false, FilterMode.Point, TextureWrapMode.Clamp);

            var textures = new GeneratedTextures
            {
                Icon = LoadRequired<Texture2D>(iconPath),
                Noise = LoadRequired<Texture2D>(noisePath),
                Flow = LoadRequired<Texture2D>(flowPath),
                Sequence = LoadRequired<Texture2D>(sequencePath),
                IconSprite = LoadRequired<Sprite>(iconPath),
            };
            return textures;
        }

        private static Texture2D CreateIconTexture()
        {
            const int size = 256;
            var texture = NewTexture(size, size, "ESTest Icon");
            var pixels = new Color[size * size];
            Vector2 center = Vector2.one * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    Vector2 p = uv - center;
                    float circle = p.magnitude;
                    float diamond = Mathf.Abs(p.x) + Mathf.Abs(p.y);
                    float outerAlpha = 1f - Mathf.SmoothStep(0.43f, 0.48f, diamond);
                    float innerRing = Mathf.SmoothStep(0.31f, 0.29f, Mathf.Abs(circle - 0.24f));
                    float diagonal = Mathf.SmoothStep(0.055f, 0.01f, Mathf.Abs(p.y - p.x * 0.55f));
                    float grid = ((Mathf.Floor(uv.x * 8f) + Mathf.Floor(uv.y * 8f)) % 2f) * 0.12f;
                    Color cold = new Color(0.04f, 0.35f, 0.92f, 1f);
                    Color warm = new Color(1f, 0.22f, 0.08f, 1f);
                    Color color = Color.Lerp(cold, warm, Mathf.Clamp01(uv.x * 0.7f + uv.y * 0.3f));
                    color += new Color(grid, grid, grid, 0f);
                    color = Color.Lerp(color, Color.white, Mathf.Clamp01(innerRing + diagonal) * 0.72f);
                    color.a = outerAlpha;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateNoiseTexture()
        {
            const int size = 256;
            var texture = NewTexture(size, size, "ESTest Noise");
            var pixels = new Color[size * size];
            var random = new System.Random(0x455354);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coarse = Mathf.PerlinNoise(x / 37.1f + 3.7f, y / 41.3f + 8.9f);
                    float fine = Mathf.PerlinNoise(x / 9.7f + 12.4f, y / 11.9f + 2.1f);
                    float randomValue = (float)random.NextDouble();
                    float value = Mathf.Clamp01(coarse * 0.58f + fine * 0.3f + randomValue * 0.12f);
                    pixels[y * size + x] = new Color(value, value, value, value);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateFlowTexture()
        {
            const int size = 256;
            var texture = NewTexture(size, size, "ESTest Flow");
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float wave = Mathf.Sin((u * 7f + v * 2f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    float streak = Mathf.Pow(wave, 5f);
                    float bend = Mathf.Sin(v * Mathf.PI * 4f) * 0.12f;
                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(0.5f + bend),
                        Mathf.Clamp01(0.5f + streak * 0.45f),
                        streak,
                        Mathf.Clamp01(0.18f + streak * 0.82f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateSequenceTexture()
        {
            const int tile = 64;
            const int count = 4;
            int size = tile * count;
            var texture = NewTexture(size, size, "ESTest Sequence 4x4");
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int frameX = x / tile;
                    int frameY = y / tile;
                    int frame = frameY * count + frameX;
                    float u = (x % tile + 0.5f) / tile;
                    float v = (y % tile + 0.5f) / tile;
                    float radius = 0.1f + frame * 0.018f;
                    float ring = 1f - Mathf.SmoothStep(0.035f, 0.075f, Mathf.Abs(Vector2.Distance(new Vector2(u, v), Vector2.one * 0.5f) - radius));
                    float core = 1f - Mathf.SmoothStep(0.04f, 0.12f, Vector2.Distance(new Vector2(u, v), Vector2.one * 0.5f));
                    Color color = Color.HSVToRGB(frame / 16f, 0.72f, 1f);
                    float alpha = Mathf.Clamp01(ring + core * 0.35f);
                    pixels[y * size + x] = new Color(color.r * alpha, color.g * alpha, color.b * alpha, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D NewTexture(int width, int height, string name)
        {
            return new Texture2D(width, height, UnityEngine.TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        private static void WritePng(string assetPath, Texture2D texture)
        {
            try
            {
                string absolutePath = Path.GetFullPath(assetPath);
                byte[] bytes = texture.EncodeToPNG();
                if (bytes == null || bytes.Length == 0)
                    throw new InvalidOperationException("PNG 编码失败：" + assetPath);
                File.WriteAllBytes(absolutePath, bytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ConfigureTextureImporter(
            string assetPath,
            bool sprite,
            bool sRgbTexture,
            bool mipmapEnabled,
            FilterMode filterMode,
            TextureWrapMode wrapMode)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("无法读取纹理导入器：" + assetPath);

            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = sprite ? SpriteImportMode.Single : SpriteImportMode.None;
            importer.spritePixelsPerUnit = 128f;
            importer.alphaIsTransparency = sprite || sRgbTexture;
            importer.mipmapEnabled = mipmapEnabled;
            importer.sRGBTexture = sRgbTexture;
            importer.filterMode = filterMode;
            importer.wrapMode = wrapMode;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new InvalidOperationException("无法加载生成资产：" + path);
            return asset;
        }
    }
}
