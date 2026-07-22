using UnityEngine;
using ES;

namespace ES.Samples{
    /// <summary>
    /// ColorSelector API 婕旂ず - 棰滆壊閫夋嫨鍣ㄥ伐鍏?
    /// 鎻愪緵100+绉嶉瀹氫箟棰滆壊鍜屼究鎹风殑棰滆壊璁块棶鏂瑰紡
    /// </summary>
    public class Example_ColorSelector : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== ColorSelector API 婕旂ず ===");

            // 1. 閫氳繃鏋氫妇鑾峰彇棰滆壊
            Color red = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.绾?;
            Debug.Log($"绾㈣壊 RGB: {red}");

            Color skyBlue = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.澶╄摑);
            Debug.Log($"澶╄摑鑹?RGB: {skyBlue}");

            // 2. 閫氳繃瀛楃涓插悕绉拌幏鍙栭鑹诧紙鏀寔涓枃锛?
            Color colorByName = ESDesignUtility.ColorSelector.GetColor("绾?);
            Debug.Log($"閫氳繃涓枃鍚嶇О鑾峰彇: {colorByName}");

            Color colorByString = ESDesignUtility.ColorSelector.GetColor("澶╄摑");
            Debug.Log($"閫氳繃瀛楃涓茶幏鍙栧ぉ钃? {colorByString}");

            // 3. 灏濊瘯鑾峰彇棰滆壊锛堝畨鍏ㄦ柟寮忥級
            if (ESDesignUtility.ColorSelector.TryGetColor(ESDesignUtility.ColorSelector.ColorName.閲? out Color gold))
            {
                Debug.Log($"瀹夊叏鑾峰彇閲戣壊: {gold}");
            }

            // 4. 蹇嵎璁块棶棰勫畾涔夐鑹?
            Color color01 = ESDesignUtility.ColorSelector.Color_01;
            Color color02 = ESDesignUtility.ColorSelector.Color_02;
            Color color03 = ESDesignUtility.ColorSelector.Color_03;
            Debug.Log($"棰勫畾涔夐鑹? Color_01={color01}, Color_02={color02}");

            // 5. 鑾峰彇鎵€鏈夊彲鐢ㄩ鑹插悕绉?
            var allColorNames = ESDesignUtility.ColorSelector.GetAllColorName();
            Debug.Log($"鎬诲叡鏈?{allColorNames.Count} 绉嶉鑹插彲鐢?);
            Debug.Log($"鍓?涓鑹? {string.Join(", ", allColorNames.GetRange(0, System.Math.Min(5, allColorNames.Count)))}");

            // 6. 瀹為檯搴旂敤绀轰緥锛氱粰GameObject璁剧疆鏉愯川棰滆壊
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "ColoredCube";
            Renderer renderer = cube.GetComponent<Renderer>();
            renderer.material.color = ESDesignUtility.ColorSelector.GetColor(ESDesignUtility.ColorSelector.ColorName.閲戦粍);
            Debug.Log("鍒涘缓浜嗕竴涓噾榛勮壊绔嬫柟浣?);

            // 7. 闅忔満棰滆壊
            Color randomColor = ESDesignUtility.ColorSelector.GetRandomColor();
            Debug.Log($"闅忔満棰滆壊: {randomColor}");

            // 8. 棰滆壊璋冩暣锛氫寒搴?
            Color brightRed = ESDesignUtility.ColorSelector.AdjustBrightness(red, 1.5f);
            Debug.Log($"鍙樹寒鐨勭孩鑹? {brightRed}");

            // 9. 浜掕ˉ鑹?
            Color complementary = ESDesignUtility.ColorSelector.GetComplementaryColor(red);
            Debug.Log($"绾㈣壊鐨勪簰琛ヨ壊: {complementary}");

            // 10. 娣峰悎棰滆壊
            Color blended = ESDesignUtility.ColorSelector.BlendColors(red, skyBlue, 0.5f);
            Debug.Log($"绾㈣壊鍜屽ぉ钃濇贩鍚? {blended}");

            // 11. 鐢熸垚璋冭壊鏉?
            Color[] palette = ESDesignUtility.ColorSelector.GenerateColorPalette(red, 5);
            Debug.Log($"鐢熸垚浜?{palette.Length} 鑹茶皟鑹叉澘");

            // 12. 浣跨敤缂栬緫鍣ㄤ笓鐢ㄩ鑹?
            Color colorForDes = ESDesignUtility.ColorSelector.ColorForDes;
            Color colorForBinding = ESDesignUtility.ColorSelector.ColorForBinding;
            Debug.Log($"缂栬緫鍣ㄥ娉ㄨ壊: {colorForDes}, 缁戝畾鑹? {colorForBinding}");
        }
    }
}

