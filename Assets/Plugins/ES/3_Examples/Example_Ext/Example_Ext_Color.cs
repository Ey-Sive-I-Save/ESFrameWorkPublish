using System;
using UnityEngine;

namespace ES
{
    // 示例：演示 ExtForColor.cs 中常用颜色扩展
    public class Example_Ext_Color : MonoBehaviour
    {
        void Start()
        {
            // 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtForColor.cs
            // 这个示例脚本展示了常用的 Color 扩展用法 —— 非就地版本优先使用以免引入 ref 复杂度。

            Color a = Color.red;
            Color b = new Color(0.2f, 0.5f, 0.3f, 0.8f);

            // 1) 基础通道修改（返回新 Color）
            Color aR = a._WithR(0.5f); // 改变红色分量
            Debug.Log($"_WithR: {a} -> {aR}");

            Color bRGB = b._WithRGB(0.9f, 0.1f, 0.2f);
            Debug.Log($"_WithRGB: {b} -> {bRGB}");

            // 2) 透明度操作
            Color aHalf = a._WithAlpha(0.5f);
            Debug.Log($"_WithAlpha: {a} -> {aHalf}");

            Color multiplied = a._AlphaMultiply(0.5f);
            Debug.Log($"_AlphaMultiply: {a} -> {multiplied}");

            // 3) 预乘 alpha 和反转
            Color premult = b._RGBMultiAlpha();
            Debug.Log($"_RGBMultiAlpha: {b} -> {premult}");

            Color inv = b._Invert();
            Debug.Log($"_Invert: {b} -> {inv}");

            // 4) 十六进制转换
            string hex = a._ToHex16String();
            Debug.Log($"_ToHex16String: {a} -> {hex}");

            string hexA = b._ToHex16String(true);
            Debug.Log($"_ToHex16String(includeAlpha): {b} -> {hexA}");

            Color fromHex = "#FF00FF"._ToColorFromHex();
            Debug.Log($"_ToColorFromHex('#FF00FF') -> {fromHex}");

            if ("#abc"._TryToColorFromHex(out var shortHex))
            {
                Debug.Log($"_TryToColorFromHex('#abc') -> {shortHex}");
            }

            // 5) 灰度与亮度
            float gray = b._GetGrayscale();
            Debug.Log($"_GetGrayscale({b}) = {gray}");

            Color grayColor = b._AsGrayscale();
            Debug.Log($"_AsGrayscale: {b} -> {grayColor}");

            // 6) 近似比较
            var nearly = new Color(b.r + 0.0003f, b.g, b.b, b.a);
            bool approxEach = b._IsApproximatelyEachRGBA(nearly, 0.001f);
            bool approxTogether = b._IsApproximatelyTogether(nearly, 0.01f);
            Debug.Log($"_IsApproximatelyEachRGBA -> {approxEach}, _IsApproximatelyTogether -> {approxTogether}");

            // 7) 亮度缩放（新颜色与就地）
            Color darker = b._WithRGBMulti(0.5f);
            Debug.Log($"_WithRGBMulti: {b} -> {darker}");

            Color temp = b;
            ExtForColor._SetRGBMulti(ref temp, 0.8f); // 就地修改示例（调用静态方法以清晰展示）
            Debug.Log($"_SetRGBMulti (inplace): original {b} -> modified {temp}");

            // 8) 随机颜色（就地 ref 版本演示）
            Color rnd = Color.white;
            ExtForColor._RandomColorRef(ref rnd, randomAlpha: true);
            Debug.Log($"_RandomColorRef -> {rnd}");

            // 9) 组合示例：将一个颜色转为 hex，再解析回来并比较
            var original = new Color(0.1f, 0.6f, 0.3f, 0.9f);
            var toHex = original._ToHex16String(true);
            var parsed = toHex._ToColorFromHex();
            Debug.Log($"Roundtrip hex: {original} -> {toHex} -> {parsed}");

            // 结束：将结果显示在场景中（可选）
            // 例如：将本对象的 SpriteRenderer/Graphic 或 Camera 背景色设置为这些值以观察效果。
        }
    }
}
