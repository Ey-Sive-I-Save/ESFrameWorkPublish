using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static partial class ESDesignUtility
    {
        /// <summary>
        /// 【可用】✅ 颜色选择器 - 提供丰富的颜色库和便捷的颜色访问方式
        /// 支持：
        /// - 100+种中英文颜色名称查询
        /// - 枚举和字符串两种访问方式
        /// - 编辑器专用颜色
        /// - 自定义调色板
        /// </summary>
        public static class ColorSelector
        {
            public static void Testing()
            {

                var color = ESDesignUtility.ColorSelector.咖啡;
                var color2 = ESDesignUtility.ColorSelector.GetColor("天蓝");
                var color3 = ESDesignUtility.ColorSelector.GetColor(ColorSelector.ColorName.天蓝);
                var color4 = ESDesignUtility.ColorSelector.Color_03;
                var colorNames = ESDesignUtility.ColorSelector.GetAllColorName();
                if (ESDesignUtility.ColorSelector.TryGetColor(ColorName.夜棕灰, out var color6))
                {

                }
            }

            /// <summary>
            /// 获取所有支持的颜色名（中英文，按字典Key顺序）
            /// </summary>
            public static List<string> GetAllColorName()
            {
                return new List<string>(normalColors.Keys);
            }

            /// <summary>
            /// 支持的颜色名枚举（部分常用，建议按需扩展）
            /// 注意：枚举名称应与颜色字典中的键保持一致
            /// </summary>
            public enum ColorName
            {
                红, 绿, 蓝, 黄, 青, 紫, 橙, 粉, 棕, 灰, 白, 黑, 金, 银, 米, 米白,
                深红, 深绿, 深蓝, 深黄, 深紫, 深灰, 浅红, 浅绿, 浅蓝, 浅黄, 浅紫, 浅灰,
                橄榄, 藏青, 天蓝, 湖蓝, 宝石蓝, 孔雀蓝, 藏蓝, 天青, 碧绿, 翠绿, 草绿, 橄榄绿, 墨绿, 苹果绿, 青绿, 翡翠, 石青, 天紫,
                玫红, 桃红, 樱花粉, 胭脂, 酒红, 砖红, 赭石, 咖啡, 巧克力, 琥珀, 米黄, 象牙白, 奶油, 鹅黄, 杏黄, 柠檬黄, 金黄, 银白,
                铅灰, 铁灰, 深棕, 浅棕, 米棕, 象牙, 珍珠白, 雪青, 雾蓝, 雾紫, 雾粉, 雾绿, 雾黄, 雾橙, 雾棕, 雾白, 雾黑,
                夜蓝, 夜紫, 夜绿, 夜红, 夜灰, 夜白, 夜金, 夜银, 夜棕, 夜橙, 夜粉, 夜蓝灰, 夜紫灰, 夜绿灰, 夜黄灰, 夜橙灰, 夜棕灰,
                夜雾白, 夜雾灰, 夜雾蓝, 夜雾紫, 夜雾粉, 夜雾绿, 夜雾黄, 夜雾橙, 夜雾棕, 夜雾黑
            }

            // 预缓存枚举到颜色的映射，避免运行时字符串转换
            private static readonly Dictionary<ColorName, Color> enumColorCache = new Dictionary<ColorName, Color>();

            /// <summary>
            /// 静态构造函数，初始化枚举颜色缓存
            /// </summary>
            static ColorSelector()
            {
                InitializeEnumColorCache();
            }

            /// <summary>
            /// 初始化枚举颜色映射缓存
            /// </summary>
            private static void InitializeEnumColorCache()

            {
                foreach (ColorName colorName in Enum.GetValues(typeof(ColorName)))
                {
                    string colorKey = colorName.ToString();
                    if (normalColors.TryGetValue(colorKey, out Color color))
                    {
                        enumColorCache[colorName] = color;
                    }
                }
            }



            /// <summary>
            /// 通过ColorName枚举获取Color（高性能版本，使用预缓存）
            /// </summary>
            /// <param name="colorName">颜色枚举值</param>
            /// <returns>对应的颜色，未找到返回白色</returns>
            public static Color GetColor(ColorName colorName)
            {
                if (enumColorCache.TryGetValue(colorName, out Color color))
                {
                    return color;
                }
                var str = colorName.ToString();
                if (normalColors.TryGetValue(str, out Color c))
                {
                    normalColors[str] = c;
                    return c;
                }
                return Color.white;
            }

            /// <summary>
            /// 尝试通过ColorName枚举获取Color
            /// </summary>
            /// <param name="colorName">颜色枚举值</param>
            /// <param name="color">输出颜色</param>
            /// <returns>是否找到该颜色</returns>
            public static bool TryGetColor(ColorName colorName, out Color color)
            {
                return enumColorCache.TryGetValue(colorName, out color);
            }

            /// <summary>
            /// 获取随机颜色（从枚举颜色中随机选择）
            /// </summary>
            /// <returns>随机颜色</returns>
            public static Color GetRandomColor()
            {
                var values = Enum.GetValues(typeof(ColorName));
                ColorName randomName = (ColorName)values.GetValue(UnityEngine.Random.Range(0, values.Length));
                return GetColor(randomName);
            }

            /// <summary>
            /// 获取随机颜色（从中英文颜色字典中随机选择）
            /// </summary>
            /// <returns>随机颜色</returns>
            public static Color GetRandomColorFromAll()
            {
                var keys = new List<string>(normalColors.Keys);
                string randomKey = keys[UnityEngine.Random.Range(0, keys.Count)];
                return normalColors[randomKey];
            }

            /// <summary>
            /// 调整颜色的亮度
            /// </summary>
            /// <param name="color">原始颜色</param>
            /// <param name="factor">亮度因子（0-2，1为不变）</param>
            /// <returns>调整后的颜色</returns>
            public static Color AdjustBrightness(Color color, float factor)
            {
                Color.RGBToHSV(color, out float h, out float s, out float v);
                v = Mathf.Clamp01(v * factor);
                return Color.HSVToRGB(h, s, v);
            }

            /// <summary>
            /// 获取颜色的互补色
            /// </summary>
            /// <param name="color">原始颜色</param>
            /// <returns>互补色</returns>
            public static Color GetComplementaryColor(Color color)
            {
                Color.RGBToHSV(color, out float h, out float s, out float v);
                h = (h + 0.5f) % 1f; // 色相偏移180度
                return Color.HSVToRGB(h, s, v);
            }

            /// <summary>
            /// 颜色混合 - 根据比例混合两种颜色
            /// </summary>
            /// <param name="color1">第一种颜色</param>
            /// <param name="color2">第二种颜色</param>
            /// <param name="ratio">混合比例（0-1，0为color1，1为color2）</param>
            /// <returns>混合后的颜色</returns>
            public static Color BlendColors(Color color1, Color color2, float ratio)
            {
                return Color.Lerp(color1, color2, ratio);
            }

            /// <summary>
            /// 计算两种颜色的相似度距离（0-1，0为完全相同，1为完全不同）
            /// </summary>
            /// <param name="a">第一种颜色</param>
            /// <param name="b">第二种颜色</param>
            /// <returns>颜色距离（0-1）</returns>
            public static float ColorDistance(Color a, Color b)
            {
                // 使用欧几里得距离计算RGB空间中的距离
                float deltaR = a.r - b.r;
                float deltaG = a.g - b.g;
                float deltaB = a.b - b.b;
                float deltaA = a.a - b.a;

                // 归一化到0-1范围
                return Mathf.Sqrt(deltaR * deltaR + deltaG * deltaG + deltaB * deltaB + deltaA * deltaA) / Mathf.Sqrt(4f);
            }

            /// <summary>
            /// 生成颜色主题 - 基于基础颜色生成一系列相关颜色
            /// </summary>
            /// <param name="baseColor">基础颜色</param>
            /// <param name="count">生成的颜色数量</param>
            /// <returns>颜色数组</returns>
            public static Color[] GenerateColorPalette(Color baseColor, int count)
            {
                if (count <= 0) return new Color[0];
                if (count == 1) return new Color[] { baseColor };

                Color[] palette = new Color[count];
                palette[0] = baseColor;

                // 将基础颜色转换为HSV
                Color.RGBToHSV(baseColor, out float baseH, out float baseS, out float baseV);

                for (int i = 1; i < count; i++)
                {
                    float ratio = (float)i / (count - 1);

                    // 色相变化：生成类似色或互补色变体
                    float h = (baseH + ratio * 0.3f) % 1f; // 色相偏移最多30%

                    // 饱和度变化：保持一定饱和度
                    float s = Mathf.Lerp(baseS, 0.7f, ratio * 0.3f);

                    // 亮度变化：生成从暗到亮的渐变
                    float v = Mathf.Lerp(0.3f, 0.9f, ratio);

                    palette[i] = Color.HSVToRGB(h, s, v);
                }

                return palette;
            }

            /// <summary>
            /// 尝试将字符串转为ColorName枚举
            /// </summary>
            /// <param name="name">颜色名称字符串</param>
            /// <param name="colorName">输出枚举值</param>
            /// <returns>是否成功转换</returns>
            public static bool TryParseColorName(string name, out ColorName colorName)
            {
                return Enum.TryParse(name, out colorName);
            }

            /// <summary>
            /// 通过颜色名（支持中英文）获取Color，未找到返回false。
            /// </summary>
            /// <param name="name">颜色名（如 "红"、"red"、"天蓝"）</param>
            /// <param name="color">输出颜色</param>
            /// <returns>是否找到该颜色</returns>
            public static bool TryGetColor(string name, out Color color)
            {
                if (string.IsNullOrEmpty(name))
                {
                    color = default;
                    return false;
                }
                return normalColors.TryGetValue(name, out color);
            }

            /// <summary>
            /// 通过颜色名（支持中英文）获取Color，未找到返回白色。
            /// </summary>
            /// <param name="name">颜色名（如 "红"、"red"、"天蓝"）</param>
            /// <returns>查找到的颜色，未找到返回 Color.white</returns>
            public static Color GetColor(string name)
            {
                if (TryGetColor(name, out var c))
                    return c;
                return Color.white;
            }


            //使用方法↓
            //GUIColor("@ESDesignUtility.ColorSelector.Color_03")
            #region 常用自己调色
            public static Color Color_01 = new Color(0.588f, 0.758f, 0.763f, 1);
            public static Color Color_02 = new Color(0.9988f, 0.958f, 0.163f, 1);
            public static Color Color_03 = new Color(0.9988f, 0.958f, 0f, 1);//黄色
            public static Color Color_04 = new Color(0.1588f, 0.958f, 0.9f, 1);//色
            public static Color Color_05 = new Color(0.7588f, 0.758f, 0.25f, 1);//色
            public static Color Color_06 = new Color(0.4588f, 0.758f, 0.45f, 1);//色
            #endregion

            #region  给编辑器用的
            public static Color ColorForDes = new Color(0.682f, 0.8392f, 0.945f);//备注信息  --偏白
            public static Color ColorForPlayerReadMe = new Color(0.49f, 0.2353f, 0.596f);//播放器注释信息  --偏白
            public static Color ColorForCaster = new Color(0.365f, 0.6784f, 0.886f);//投射器 --偏蓝
            public static Color ColorForCatcher = new Color(0.8314f, 0.6745f, 0.051f);//抓取器   --偏橙色
            public static Color ColorForESValue = new Color(0.153f, 0.682f, 0.376f);//ES值    --偏绿
            public static Color ColorForUpdating = new Color(0.804f, 0.67843f, 0);//更新中    --偏绿

            public static Color ColorForBinding = new Color(0, 0.97f, 1);//绑定色
            public static Color ColorForSearch = new Color(0.4f, 0.804f, 0.667f);//选择色
            public static Color ColorForApply = new Color(0, 0.804f, 0);//应用色
            #endregion

            // 常用颜色字典（含中英文名，忽略大小写）
            public static readonly Dictionary<string, Color> normalColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
            {
                { "red", Color.red },
                { "green", Color.green },
                { "blue", Color.blue },
                { "yellow", Color.yellow },
                { "cyan", Color.cyan },
                { "magenta", Color.magenta },
                { "black", Color.black },
                { "white", Color.white },
                { "gray", Color.gray },
                { "grey", Color.grey },
                { "clear", Color.clear },
                // 中文常用色
                { "红", new Color(1f, 0f, 0f) },
                { "绿", new Color(0f, 1f, 0f) },
                { "蓝", new Color(0f, 0f, 1f) },
                { "黄", new Color(1f, 1f, 0f) },
                { "青", new Color(0f, 1f, 1f) },
                { "紫", new Color(0.5f, 0f, 0.5f) },
                { "橙", new Color(1f, 0.5f, 0f) },
                { "粉", new Color(1f, 0.75f, 0.8f) },
                { "棕", new Color(0.6f, 0.4f, 0.2f) },
                { "灰", new Color(0.5f, 0.5f, 0.5f) },
                { "白", new Color(1f, 1f, 1f) },
                { "黑", new Color(0f, 0f, 0f) },
                { "金", new Color(1f, 0.84f, 0f) },
                { "银", new Color(0.75f, 0.75f, 0.75f) },
                { "米", new Color(1f, 0.94f, 0.84f) },
                { "米白", new Color(1f, 0.98f, 0.92f) },
                { "深红", new Color(0.7f, 0f, 0f) },
                { "深绿", new Color(0f, 0.39f, 0f) },
                { "深蓝", new Color(0f, 0f, 0.55f) },
                { "深黄", new Color(0.8f, 0.7f, 0.1f) },
                { "深紫", new Color(0.29f, 0f, 0.51f) },
                { "深灰", new Color(0.25f, 0.25f, 0.25f) },
                { "浅红", new Color(1f, 0.6f, 0.6f) },
                { "浅绿", new Color(0.6f, 1f, 0.6f) },
                { "浅蓝", new Color(0.6f, 0.8f, 1f) },
                { "浅黄", new Color(1f, 1f, 0.6f) },
                { "浅紫", new Color(0.8f, 0.6f, 1f) },
                { "浅灰", new Color(0.8f, 0.8f, 0.8f) },
                { "橄榄", new Color(0.5f, 0.5f, 0f) },
                { "藏青", new Color(0.1f, 0.2f, 0.4f) },
                { "天蓝", new Color(0.53f, 0.81f, 0.98f) },
                { "湖蓝", new Color(0.25f, 0.88f, 0.82f) },
                { "宝石蓝", new Color(0.08f, 0.38f, 0.74f) },
                { "孔雀蓝", new Color(0.2f, 0.63f, 0.79f) },
                { "藏蓝", new Color(0.09f, 0.09f, 0.44f) },
                { "天青", new Color(0.38f, 0.72f, 0.88f) },
                { "碧绿", new Color(0.18f, 0.8f, 0.44f) },
                { "翠绿", new Color(0.0f, 0.78f, 0.34f) },
                { "草绿", new Color(0.48f, 0.99f, 0.0f) },
                { "橄榄绿", new Color(0.33f, 0.42f, 0.18f) },
                { "墨绿", new Color(0.0f, 0.39f, 0.0f) },
                { "苹果绿", new Color(0.55f, 0.71f, 0.0f) },
                { "青绿", new Color(0.0f, 0.5f, 0.5f) },
                { "翡翠", new Color(0.0f, 0.85f, 0.52f) },
                { "石青", new Color(0.45f, 0.62f, 0.8f) },
                { "天紫", new Color(0.58f, 0.44f, 0.86f) },
                { "玫红", new Color(0.86f, 0.08f, 0.24f) },
                { "桃红", new Color(1f, 0.18f, 0.33f) },
                { "樱花粉", new Color(1f, 0.72f, 0.77f) },
                { "胭脂", new Color(0.86f, 0.08f, 0.24f) },
                { "酒红", new Color(0.55f, 0.0f, 0.13f) },
                { "砖红", new Color(0.8f, 0.25f, 0.33f) },
                { "赭石", new Color(0.8f, 0.47f, 0.13f) },
                { "咖啡", new Color(0.44f, 0.26f, 0.08f) },
                { "巧克力", new Color(0.82f, 0.41f, 0.12f) },
                { "琥珀", new Color(1f, 0.75f, 0.29f) },
                { "米黄", new Color(1f, 0.94f, 0.75f) },
                { "象牙白", new Color(1f, 1f, 0.94f) },
                { "奶油", new Color(1f, 1f, 0.82f) },
                { "鹅黄", new Color(1f, 0.98f, 0.8f) },
                { "杏黄", new Color(1f, 0.77f, 0.36f) },
                { "柠檬黄", new Color(1f, 0.97f, 0.0f) },
                { "金黄", new Color(1f, 0.84f, 0.0f) },
                { "银白", new Color(0.97f, 0.97f, 0.97f) },
                { "铅灰", new Color(0.42f, 0.48f, 0.55f) },
                { "铁灰", new Color(0.27f, 0.28f, 0.31f) },
                { "深棕", new Color(0.36f, 0.25f, 0.2f) },
                { "浅棕", new Color(0.82f, 0.71f, 0.55f) },
                { "米棕", new Color(0.87f, 0.72f, 0.53f) },
                { "象牙", new Color(1f, 1f, 0.94f) },
                { "珍珠白", new Color(0.98f, 0.98f, 0.98f) },
                { "雪青", new Color(0.8f, 0.85f, 0.95f) },
                { "雾蓝", new Color(0.7f, 0.8f, 0.9f) },
                { "雾紫", new Color(0.8f, 0.7f, 0.9f) },
                { "雾粉", new Color(0.9f, 0.8f, 0.9f) },
                { "雾绿", new Color(0.8f, 0.9f, 0.8f) },
                { "雾黄", new Color(0.95f, 0.95f, 0.8f) },
                { "雾橙", new Color(0.95f, 0.85f, 0.7f) },
                { "雾棕", new Color(0.85f, 0.8f, 0.7f) },
                { "雾白", new Color(0.98f, 0.98f, 0.98f) },
                { "雾黑", new Color(0.2f, 0.2f, 0.2f) },
                { "夜蓝", new Color(0.1f, 0.1f, 0.3f) },
                { "夜紫", new Color(0.2f, 0.1f, 0.3f) },
                { "夜绿", new Color(0.1f, 0.3f, 0.1f) },
                { "夜红", new Color(0.3f, 0.1f, 0.1f) },
                { "夜灰", new Color(0.2f, 0.2f, 0.2f) },
                { "夜白", new Color(0.9f, 0.9f, 0.9f) },
                { "夜金", new Color(0.8f, 0.7f, 0.2f) },
                { "夜银", new Color(0.8f, 0.8f, 0.8f) },
                { "夜棕", new Color(0.3f, 0.2f, 0.1f) },
                { "夜橙", new Color(0.8f, 0.5f, 0.2f) },
                { "夜粉", new Color(0.8f, 0.6f, 0.7f) },
                { "夜蓝灰", new Color(0.3f, 0.4f, 0.5f) },
                { "夜紫灰", new Color(0.4f, 0.3f, 0.5f) },
                { "夜绿灰", new Color(0.3f, 0.5f, 0.3f) },
                { "夜黄灰", new Color(0.5f, 0.5f, 0.3f) },
                { "夜橙灰", new Color(0.5f, 0.4f, 0.3f) },
                { "夜棕灰", new Color(0.4f, 0.3f, 0.3f) },
                { "夜雾白", new Color(0.95f, 0.95f, 0.98f) },
                { "夜雾灰", new Color(0.7f, 0.7f, 0.8f) },
                { "夜雾蓝", new Color(0.6f, 0.7f, 0.8f) },
                { "夜雾紫", new Color(0.7f, 0.6f, 0.8f) },
                { "夜雾粉", new Color(0.8f, 0.7f, 0.8f) },
                { "夜雾绿", new Color(0.7f, 0.8f, 0.7f) },
                { "夜雾黄", new Color(0.8f, 0.8f, 0.7f) },
                { "夜雾橙", new Color(0.8f, 0.75f, 0.7f) },
                { "夜雾棕", new Color(0.75f, 0.7f, 0.7f) },
                { "夜雾黑", new Color(0.1f, 0.1f, 0.1f) }
            };

            #region 显式颜色静态字段（自动生成）
            // 以下字段通过代码生成工具自动生成，避免手动维护
            // 如需添加新颜色，请在normalColors字典中添加，然后重新生成

            // 基础颜色
            public static readonly Color 红 = new Color(1f, 0f, 0f);
            public static readonly Color 绿 = new Color(0f, 1f, 0f);
            public static readonly Color 蓝 = new Color(0f, 0f, 1f);
            public static readonly Color 黄 = new Color(1f, 1f, 0f);
            public static readonly Color 青 = new Color(0f, 1f, 1f);
            public static readonly Color 紫 = new Color(0.5f, 0f, 0.5f);
            public static readonly Color 橙 = new Color(1f, 0.5f, 0f);
            public static readonly Color 粉 = new Color(1f, 0.75f, 0.8f);
            public static readonly Color 棕 = new Color(0.6f, 0.4f, 0.2f);
            public static readonly Color 灰 = new Color(0.5f, 0.5f, 0.5f);
            public static readonly Color 白 = new Color(1f, 1f, 1f);
            public static readonly Color 黑 = new Color(0f, 0f, 0f);
            public static readonly Color 金 = new Color(1f, 0.84f, 0f);
            public static readonly Color 银 = new Color(0.75f, 0.75f, 0.75f);
            public static readonly Color 米 = new Color(1f, 0.94f, 0.84f);
            public static readonly Color 米白 = new Color(1f, 0.98f, 0.92f);

            // 深色系
            public static readonly Color 深红 = new Color(0.7f, 0f, 0f);
            public static readonly Color 深绿 = new Color(0f, 0.39f, 0f);
            public static readonly Color 深蓝 = new Color(0f, 0f, 0.55f);
            public static readonly Color 深黄 = new Color(0.8f, 0.7f, 0.1f);
            public static readonly Color 深紫 = new Color(0.29f, 0f, 0.51f);
            public static readonly Color 深灰 = new Color(0.25f, 0.25f, 0.25f);

            // 浅色系
            public static readonly Color 浅红 = new Color(1f, 0.6f, 0.6f);
            public static readonly Color 浅绿 = new Color(0.6f, 1f, 0.6f);
            public static readonly Color 浅蓝 = new Color(0.6f, 0.8f, 1f);
            public static readonly Color 浅黄 = new Color(1f, 1f, 0.6f);
            public static readonly Color 浅紫 = new Color(0.8f, 0.6f, 1f);
            public static readonly Color 浅灰 = new Color(0.8f, 0.8f, 0.8f);

            // 其他常用颜色（示例）
            public static readonly Color 天蓝 = new Color(0.53f, 0.81f, 0.98f);
            public static readonly Color 湖蓝 = new Color(0.25f, 0.88f, 0.82f);
            public static readonly Color 宝石蓝 = new Color(0.08f, 0.38f, 0.74f);
            public static readonly Color 孔雀蓝 = new Color(0.2f, 0.63f, 0.79f);
            public static readonly Color 藏蓝 = new Color(0.09f, 0.09f, 0.44f);
            public static readonly Color 天青 = new Color(0.38f, 0.72f, 0.88f);
            public static readonly Color 碧绿 = new Color(0.18f, 0.8f, 0.44f);
            public static readonly Color 翠绿 = new Color(0.0f, 0.78f, 0.34f);
            public static readonly Color 草绿 = new Color(0.48f, 0.99f, 0.0f);
            public static readonly Color 橄榄绿 = new Color(0.33f, 0.42f, 0.18f);
            public static readonly Color 墨绿 = new Color(0.0f, 0.39f, 0.0f);
            public static readonly Color 苹果绿 = new Color(0.55f, 0.71f, 0.0f);
            public static readonly Color 青绿 = new Color(0.0f, 0.5f, 0.5f);
            public static readonly Color 翡翠 = new Color(0.0f, 0.85f, 0.52f);
            public static readonly Color 石青 = new Color(0.45f, 0.62f, 0.8f);
            public static readonly Color 天紫 = new Color(0.58f, 0.44f, 0.86f);
            public static readonly Color 玫红 = new Color(0.86f, 0.08f, 0.24f);
            public static readonly Color 桃红 = new Color(1f, 0.18f, 0.33f);
            public static readonly Color 樱花粉 = new Color(1f, 0.72f, 0.77f);
            public static readonly Color 胭脂 = new Color(0.86f, 0.08f, 0.24f);
            public static readonly Color 酒红 = new Color(0.55f, 0.0f, 0.13f);
            public static readonly Color 砖红 = new Color(0.8f, 0.25f, 0.33f);
            public static readonly Color 赭石 = new Color(0.8f, 0.47f, 0.13f);
            public static readonly Color 咖啡 = new Color(0.44f, 0.26f, 0.08f);
            public static readonly Color 巧克力 = new Color(0.82f, 0.41f, 0.12f);
            public static readonly Color 琥珀 = new Color(1f, 0.75f, 0.29f);
            public static readonly Color 米黄 = new Color(1f, 0.94f, 0.75f);
            public static readonly Color 象牙白 = new Color(1f, 1f, 0.94f);
            public static readonly Color 奶油 = new Color(1f, 1f, 0.82f);
            public static readonly Color 鹅黄 = new Color(1f, 0.98f, 0.8f);
            public static readonly Color 杏黄 = new Color(1f, 0.77f, 0.36f);
            public static readonly Color 柠檬黄 = new Color(1f, 0.97f, 0.0f);
            public static readonly Color 金黄 = new Color(1f, 0.84f, 0.0f);
            public static readonly Color 银白 = new Color(0.97f, 0.97f, 0.97f);
            public static readonly Color 铅灰 = new Color(0.42f, 0.48f, 0.55f);
            public static readonly Color 铁灰 = new Color(0.27f, 0.28f, 0.31f);
            public static readonly Color 深棕 = new Color(0.36f, 0.25f, 0.2f);
            public static readonly Color 浅棕 = new Color(0.82f, 0.71f, 0.55f);
            public static readonly Color 米棕 = new Color(0.87f, 0.72f, 0.53f);
            public static readonly Color 象牙 = new Color(1f, 1f, 0.94f);
            public static readonly Color 珍珠白 = new Color(0.98f, 0.98f, 0.98f);
            public static readonly Color 雪青 = new Color(0.8f, 0.85f, 0.95f);
            public static readonly Color 雾蓝 = new Color(0.7f, 0.8f, 0.9f);
            public static readonly Color 雾紫 = new Color(0.8f, 0.7f, 0.9f);
            public static readonly Color 雾粉 = new Color(0.9f, 0.8f, 0.9f);
            public static readonly Color 雾绿 = new Color(0.8f, 0.9f, 0.8f);
            public static readonly Color 雾黄 = new Color(0.95f, 0.95f, 0.8f);
            public static readonly Color 雾橙 = new Color(0.95f, 0.85f, 0.7f);
            public static readonly Color 雾棕 = new Color(0.85f, 0.8f, 0.7f);
            public static readonly Color 雾白 = new Color(0.98f, 0.98f, 0.98f);
            public static readonly Color 雾黑 = new Color(0.2f, 0.2f, 0.2f);
            public static readonly Color 夜蓝 = new Color(0.1f, 0.1f, 0.3f);
            public static readonly Color 夜紫 = new Color(0.2f, 0.1f, 0.3f);
            public static readonly Color 夜绿 = new Color(0.1f, 0.3f, 0.1f);
            public static readonly Color 夜红 = new Color(0.3f, 0.1f, 0.1f);
            public static readonly Color 夜灰 = new Color(0.2f, 0.2f, 0.2f);
            public static readonly Color 夜白 = new Color(0.9f, 0.9f, 0.9f);
            public static readonly Color 夜金 = new Color(0.8f, 0.7f, 0.2f);
            public static readonly Color 夜银 = new Color(0.8f, 0.8f, 0.8f);
            public static readonly Color 夜棕 = new Color(0.3f, 0.2f, 0.1f);
            public static readonly Color 夜橙 = new Color(0.8f, 0.5f, 0.2f);
            public static readonly Color 夜粉 = new Color(0.8f, 0.6f, 0.7f);
            public static readonly Color 夜蓝灰 = new Color(0.3f, 0.4f, 0.5f);
            public static readonly Color 夜紫灰 = new Color(0.4f, 0.3f, 0.5f);
            public static readonly Color 夜绿灰 = new Color(0.3f, 0.5f, 0.3f);
            public static readonly Color 夜黄灰 = new Color(0.5f, 0.5f, 0.3f);
            public static readonly Color 夜橙灰 = new Color(0.5f, 0.4f, 0.3f);
            public static readonly Color 夜棕灰 = new Color(0.4f, 0.3f, 0.3f);
            public static readonly Color 夜雾白 = new Color(0.95f, 0.95f, 0.98f);
            public static readonly Color 夜雾灰 = new Color(0.7f, 0.7f, 0.8f);
            public static readonly Color 夜雾蓝 = new Color(0.6f, 0.7f, 0.8f);
            public static readonly Color 夜雾紫 = new Color(0.7f, 0.6f, 0.8f);
            public static readonly Color 夜雾粉 = new Color(0.8f, 0.7f, 0.8f);
            public static readonly Color 夜雾绿 = new Color(0.7f, 0.8f, 0.7f);
            public static readonly Color 夜雾黄 = new Color(0.8f, 0.8f, 0.7f);
            public static readonly Color 夜雾橙 = new Color(0.8f, 0.75f, 0.7f);
            public static readonly Color 夜雾棕 = new Color(0.75f, 0.7f, 0.7f);
            public static readonly Color 夜雾黑 = new Color(0.1f, 0.1f, 0.1f);
            #endregion


        }
    }
}

