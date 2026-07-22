using UnityEngine;
using ES;

namespace ES.Samples{
    /// <summary>
    /// SafeEditor API 婕旂ず - 缂栬緫鍣ㄥ姛鑳藉皝瑁呭伐鍏?
    /// 鎻愪緵鍦ㄨ繍琛屾椂涔熷彲瀹夊叏璋冪敤鐨勭紪杈戝櫒鍔熻兘
    /// 娉ㄦ剰锛氬ぇ閮ㄥ垎鍔熻兘浠呭湪Unity Editor涓嬫湁鏁堬紝杩愯鏃朵細瀹夊叏杩斿洖榛樿鍊?
    /// </summary>
    public class Example_SafeEditor : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== SafeEditor API 婕旂ず ===");
            Debug.Log("娉ㄦ剰锛氬ぇ閮ㄥ垎鍔熻兘浠呭湪Editor妯″紡涓嬫湁鏁?);

            // ========== 鑾峰彇鐗规畩鏁版嵁 ==========
            Debug.Log("--- 鑾峰彇鐗规畩鏁版嵁 ---");

            // 1. 鑾峰彇鎵€鏈夋爣绛?
            string[] tags = ESDesignUtility.SafeEditor.GetAllTags();
            Debug.Log($"绯荤粺鏍囩鏁? {tags.Length}");
            if (tags.Length > 0)
            {
                Debug.Log($"绗竴涓爣绛? {tags[0]}");
            }

            // 2. 鑾峰彇鎵€鏈夊眰绾?
            var layers = ESDesignUtility.SafeEditor.GetAllLayers();
            Debug.Log($"绯荤粺灞傜骇鏁? {layers.Count}");
            foreach (var layer in layers)
            {
                Debug.Log($"  Layer {layer.Key}: {layer.Value}");
            }

            // 3. 娣诲姞鏍囩锛堜粎Editor妯″紡锛?
            ESDesignUtility.SafeEditor.AddTag("CustomTag");
            Debug.Log("灏濊瘯娣诲姞鑷畾涔夋爣绛撅紙浠匛ditor鏈夋晥锛?);

            // ========== 瀵硅瘽妗嗗皝瑁?==========
            Debug.Log("--- 瀵硅瘽妗嗗皝瑁?---");

            // 4. 鏄剧ず瀵硅瘽妗嗭紙浠匛ditor妯″紡锛?
            bool dialogResult = ESDesignUtility.SafeEditor.Wrap_DisplayDialog(
                title: "鎻愮ず",
                message: "杩欐槸涓€涓祴璇曞璇濇",
                ok: "纭畾",
                cancel: "鍙栨秷"
            );
            Debug.Log($"瀵硅瘽妗嗙粨鏋? {dialogResult}锛堣繍琛屾椂濮嬬粓杩斿洖true锛?);

            // ========== 鏂囦欢澶归€夋嫨 ==========
            Debug.Log("--- 鏂囦欢澶归€夋嫨 ---");

            // 5. 鎵撳紑鏂囦欢澶归€夋嫨鍣紙浠匛ditor妯″紡锛?
            string selectedPath = ESDesignUtility.SafeEditor.Wrap_OpenSelectorFolderPanel(
                targetPath: "Assets",
                title: "閫夋嫨鏂囦欢澶?
            );
            Debug.Log($"閫夋嫨鐨勮矾寰? {selectedPath}");

            // 6. 楠岃瘉鏂囦欢澶规槸鍚︽湁鏁堬紙浠匛ditor妯″紡锛?
            bool isValid = ESDesignUtility.SafeEditor.Wrap_IsValidFolder("Assets", IfPlayerRuntime: false);
            Debug.Log($"'Assets'鏂囦欢澶规湁鏁? {isValid}");

            // ========== SetDirty 鎿嶄綔 ==========
            Debug.Log("--- SetDirty 鎿嶄綔 ---");

            // 7. 鏍囪瀵硅薄涓鸿剰锛堜粎Editor妯″紡锛?
            ESDesignUtility.SafeEditor.Wrap_SetDirty(this.gameObject, saveAndRefresh: false);
            Debug.Log("GameObject宸叉爣璁颁负dirty锛堜粎Editor鏈夋晥锛?);

            // 8. 鍟嗕笟绾ц涔夌増鏈紙鏄惧紡鎺у埗SaveAssets/Refresh锛?
            ESDesignUtility.SafeEditor.Wrap_SetDirty(
                which: this.gameObject,
                saveAssets: false,
                refresh: false
            );
            Debug.Log("浣跨敤鍟嗕笟绾etDirty锛堜粎Editor鏈夋晥锛?);

            // ========== 璧勪骇鏌ヨ绀轰緥 ==========
            Debug.Log("--- 璧勪骇鏌ヨ绀轰緥 ---");

            // 娉ㄦ剰锛氫互涓嬪姛鑳戒富瑕佺敤浜嶦ditor鑴氭湰锛岃繍琛屾椂浼氳繑鍥炵┖

#if UNITY_EDITOR
            // 9. 鏌ユ壘鎵€鏈塖criptableObject璧勪骇
            // var allSOs = ESDesignUtility.SafeEditor.FindAllSOAssets<ScriptableObject>();
            // Debug.Log($"鎵惧埌 {allSOs.Count} 涓猄O璧勪骇");

            // 10. 鏌ユ壘璧勪骇璺緞
            // string assetPath = ESDesignUtility.SafeEditor.GetAssetPath(someObject);
            // Debug.Log($"璧勪骇璺緞: {assetPath}");
#endif

            // ========== 蹇嵎鍔熻兘绀轰緥 ==========
            Debug.Log("--- 蹇嵎鍔熻兘绀轰緥 ---");

            // 11. 鎵撳紑鏂囦欢澶癸紙浠匛ditor妯″紡锛?
            string assetsPath = Application.dataPath;
            ESDesignUtility.SafeEditor.Quick_OpenInSystemFolder(assetsPath);
            Debug.Log($"灏濊瘯鎵撳紑绯荤粺鏂囦欢澶? {assetsPath}锛堜粎Editor鏈夋晥锛?);

            // 12. 鍒涘缓Unity璧勪骇鏂囦欢澶癸紙浠匛ditor妯″紡锛?
            string newAssetFolderPath = "Assets/TestFolder";
            ESDesignUtility.SafeEditor.Quick_CreateAssetFolder(newAssetFolderPath, refresh: false);
            Debug.Log($"灏濊瘯鍒涘缓Unity璧勪骇鏂囦欢澶? {newAssetFolderPath}锛堜粎Editor鏈夋晥锛?);
            
            // 13. 鍒涘缓绯荤粺瀹屾暣璺緞鏂囦欢澶癸紙浠匛ditor妯″紡锛?
            string systemFolderPath = Application.persistentDataPath + "/MyData";
            var result = ESDesignUtility.SafeEditor.Quick_System_CreateDirectory(systemFolderPath);
            Debug.Log($"灏濊瘯鍒涘缓绯荤粺鏂囦欢澶? {systemFolderPath}, 缁撴灉: {result.Message}");

            // ========== 瀹炵敤鎻愮ず ==========
            Debug.Log("\n=== 浣跨敤鎻愮ず ===");
            Debug.Log("鈥?SafeEditor鐨勪富瑕佷紭鍔挎槸鍙互鍦ㄤ换浣曞湴鏂硅皟鐢紝涓嶉渶瑕?if UNITY_EDITOR鍖呰９");
            Debug.Log("鈥?杩愯鏃惰皟鐢ㄤ細瀹夊叏杩斿洖锛屼笉浼氭姤閿?);
            Debug.Log("鈥?澶ч儴鍒嗗姛鑳戒富瑕佺敤浜嶦ditor宸ュ叿鑴氭湰");
            Debug.Log("鈥?璧勪骇鍒涘缓銆佹煡鎵剧瓑鍔熻兘浠呭湪Editor妯″紡涓嬫湁鏁?);
            Debug.Log("鈥?Quick_CreateAssetFolder: Unity璧勪骇璺緞锛圓ssets/...锛?);
            Debug.Log("鈥?Quick_System_CreateDirectory: 绯荤粺瀹屾暣璺緞锛團:/...鎴朇:\\...锛?);
        }
    }
}

