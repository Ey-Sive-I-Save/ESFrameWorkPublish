using UnityEngine;
using ES;

namespace ES.Examples
{
    /// <summary>
    /// ColorSelector API 演示 - 颜色选择器工具
    /// 提供100+种预定义颜色和便捷的颜色访问方式
    /// </summary>
    public class Example_ColorSelector : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== ColorSelector API 演示 ===");

            // 1. 通过枚举获取颜色
            Color red = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.红);
            Debug.Log($"红色 RGB: {red}");

            Color skyBlue = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.天蓝);
            Debug.Log($"天蓝色 RGB: {skyBlue}");

            // 2. 通过字符串名称获取颜色（支持中文）
            Color colorByName = ESDesignUtility.ColorSelector.GetColor("红");
            Debug.Log($"通过中文名称获取: {colorByName}");

            Color colorByString = ESDesignUtility.ColorSelector.GetColor("天蓝");
            Debug.Log($"通过字符串获取天蓝: {colorByString}");

            // 3. 尝试获取颜色（安全方式）
            if (ESDesignUtility.ColorSelector.TryGetColor(ESDesignUtility.ColorSelector.ColorName.金, out Color gold))
            {
                Debug.Log($"安全获取金色: {gold}");
            }

            // 4. 快捷访问预定义颜色
            Color color01 = ESDesignUtility.ColorSelector.Color_01;
            Color color02 = ESDesignUtility.ColorSelector.Color_02;
            Color color03 = ESDesignUtility.ColorSelector.Color_03;
            Debug.Log($"预定义颜色: Color_01={color01}, Color_02={color02}");

            // 5. 获取所有可用颜色名称
            var allColorNames = ESDesignUtility.ColorSelector.GetAllColorName();
            Debug.Log($"总共有 {allColorNames.Count} 种颜色可用");
            Debug.Log($"前5个颜色: {string.Join(", ", allColorNames.GetRange(0, System.Math.Min(5, allColorNames.Count)))}");

            // 6. 实际应用示例：给GameObject设置材质颜色
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "ColoredCube";
            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.material.color = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.金黄);
            Debug.Log("创建了一个金黄色立方体");

            // 7. 随机颜色
            Color randomColor = ESDesignUtility.ColorSelector.GetRandomColor();
            Debug.Log($"随机颜色: {randomColor}");

            // 8. 颜色调整：亮度
            Color brightRed = ESDesignUtility.ColorSelector.AdjustBrightness(red, 1.5f);
            Debug.Log($"变亮的红色: {brightRed}");

            // 9. 互补色
            Color complementary = ESDesignUtility.ColorSelector.GetComplementaryColor(red);
            Debug.Log($"红色的互补色: {complementary}");

            // 10. 混合颜色
            Color blended = ESDesignUtility.ColorSelector.BlendColors(red, skyBlue, 0.5f);
            Debug.Log($"红色和天蓝混合: {blended}");

            // 11. 生成调色板
            Color[] palette = ESDesignUtility.ColorSelector.GenerateColorPalette(red, 5);
            Debug.Log($"生成了 {palette.Length} 色调色板");

            // 12. 使用编辑器专用颜色
            Color colorForDes = ESDesignUtility.ColorSelector.ColorForDes;
            Color colorForBinding = ESDesignUtility.ColorSelector.ColorForBinding;
            Debug.Log($"编辑器备注色: {colorForDes}, 绑定色: {colorForBinding}");
        }
    }
}
