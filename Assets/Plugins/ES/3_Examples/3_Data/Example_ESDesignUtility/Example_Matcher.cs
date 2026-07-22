using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Samples{
    /// <summary>
    /// Matcher API 婕旂ず - 搴忓垪鍖栦笌绫诲瀷杞崲宸ュ叿
    /// 鎻愪緵JSON/XML/Binary搴忓垪鍖栥€佺被鍨嬭浆鎹€丳layerPrefs灏佽绛夊姛鑳?
    /// </summary>
    public class Example_Matcher : MonoBehaviour
    {
        [System.Serializable]
        public class PlayerData
        {
            public string playerName;
            public int level;
            public float health;
            public List<string> inventory;

            public PlayerData(string name, int lvl, float hp)
            {
                playerName = name;
                level = lvl;
                health = hp;
                inventory = new List<string> { "Sword", "Shield", "Potion" };
            }
        }

        private void Start()
        {
            Debug.Log("=== Matcher API 婕旂ず ===");

            // ========== 绫诲瀷杞崲 ==========
            Debug.Log("--- 绫诲瀷杞崲 ---");

            // 1. 瀛楃涓茶浆鏁板瓧
            int intValue = ESDesignUtility.Matcher.SystemObjectToT<int>("123", defaultValue: 0);
            Debug.Log($"瀛楃涓?'123' 杞?int: {intValue}");

            float floatValue = ESDesignUtility.Matcher.SystemObjectToT<float>("45.67", defaultValue: 0f);
            Debug.Log($"瀛楃涓?'45.67' 杞?float: {floatValue}");

            // 2. 杞崲澶辫触杩斿洖榛樿鍊?
            int failed = ESDesignUtility.Matcher.SystemObjectToT<int>("not_a_number", defaultValue: 999);
            Debug.Log($"杞崲澶辫触杩斿洖榛樿鍊? {failed}");

            // 3. bool杞崲
            bool boolValue = ESDesignUtility.Matcher.SystemObjectToT<bool>("true", defaultValue: false);
            Debug.Log($"瀛楃涓?'true' 杞?bool: {boolValue}");

            // ========== JSON 搴忓垪鍖栵紙Unity鍘熺敓锛?=========
            Debug.Log("--- Unity JSON 搴忓垪鍖?---");

            PlayerData player = new PlayerData("Hero", 10, 100f);

            // 4. 瀵硅薄杞琂SON
            string json = ESDesignUtility.Matcher.ToJson(player);
            Debug.Log($"JSON: {json}");

            // 5. JSON杞璞?
            PlayerData restored = ESDesignUtility.Matcher.FromJson<PlayerData>(json);
            Debug.Log($"杩樺師: {restored.playerName}, Lv.{restored.level}, HP={restored.health}");

            // 6. JSON鏍煎紡鍖栬緭鍑?
            string prettyJson = ESDesignUtility.Matcher.ToJson(player, prettyPrint: true);
            Debug.Log($"鏍煎紡鍖朖SON:\n{prettyJson}");

            // ========== Odin JSON 搴忓垪鍖栵紙鏀寔澶氭€侊級==========
            Debug.Log("--- Odin JSON 搴忓垪鍖?---");

            // 7. 浣跨敤Odin搴忓垪鍖栵紙鏀寔鎺ュ彛銆佺户鎵跨瓑锛?
            string odinJson = ESDesignUtility.Matcher.ToOdinJson(player);
            Debug.Log($"Odin JSON: {odinJson}");

            // 8. Odin鍙嶅簭鍒楀寲
            PlayerData odinRestored = ESDesignUtility.Matcher.FromOdinJson<PlayerData>(odinJson);
            Debug.Log($"Odin杩樺師: {odinRestored.playerName}");

            // ========== 浜岃繘鍒跺簭鍒楀寲 ==========
            Debug.Log("--- 浜岃繘鍒跺簭鍒楀寲 ---");

            // 9. 瀵硅薄杞簩杩涘埗
            byte[] binaryData = ESDesignUtility.Matcher.ToBinary(player);
            Debug.Log($"浜岃繘鍒舵暟鎹ぇ灏? {binaryData.Length} 瀛楄妭");

            // 10. 浜岃繘鍒惰浆瀵硅薄
            PlayerData binaryRestored = ESDesignUtility.Matcher.FromBinary<PlayerData>(binaryData);
            Debug.Log($"浜岃繘鍒惰繕鍘? {binaryRestored.playerName}, Lv.{binaryRestored.level}");

            // ========== XML 搴忓垪鍖?==========
            Debug.Log("--- XML 搴忓垪鍖?---");

            // 11. 鍒涘缓XML绀轰緥鏁版嵁
            ESDesignUtility.ExampleXML xmlData = new ESDesignUtility.ExampleXML(
                "XMLPlayer",  // playerName
                5,            // level  
                80f,          // health
                1001          // id
            );

            // 12. 瀵硅薄杞琗ML瀛楃涓?
            string xml = ESDesignUtility.Matcher.ToXmlString(xmlData);
            Debug.Log($"XML:\n{xml}");

            // 13. XML瀛楃涓茶浆瀵硅薄
            ESDesignUtility.ExampleXML xmlRestored = ESDesignUtility.Matcher.FromXmlString<ESDesignUtility.ExampleXML>(xml);
            Debug.Log($"XML杩樺師: {xmlRestored.playerName}, Lv.{xmlRestored.level}");

            // ========== PlayerPrefs 灏佽 ==========
            Debug.Log("--- PlayerPrefs 灏佽 ---");

            // 14. 淇濆瓨瀵硅薄鍒癙layerPrefs
            ESDesignUtility.Matcher.SaveToPlayerPrefs("player_data", player);
            Debug.Log("鏁版嵁宸蹭繚瀛樺埌PlayerPrefs");

            // 15. 浠嶱layerPrefs鍔犺浇瀵硅薄
            PlayerData prefsLoaded = ESDesignUtility.Matcher.LoadFromPlayerPrefs<PlayerData>("player_data");
            if (prefsLoaded != null)
            {
                Debug.Log($"PlayerPrefs鍔犺浇: {prefsLoaded.playerName}, Lv.{prefsLoaded.level}");
            }

            // ========== 鏂囦欢鎿嶄綔 ==========
            Debug.Log("--- 鏂囦欢鎿嶄綔 ---");

            string testPath = Application.persistentDataPath + "/test_data.json";

            // 16. 淇濆瓨鍒版枃浠讹紙浣跨敤寮傛鐗堟湰锛?
            string json2 = ESDesignUtility.Matcher.ToJson(player);
            System.IO.File.WriteAllText(testPath, json2);
            Debug.Log($"宸蹭繚瀛樺埌鏂囦欢: {testPath}");

            // 17. 浠庢枃浠跺姞杞?
            if (System.IO.File.Exists(testPath))
            {
                string loadedJson = System.IO.File.ReadAllText(testPath);
                PlayerData fileLoaded = ESDesignUtility.Matcher.FromJson<PlayerData>(loadedJson);
                if (fileLoaded != null)
                {
                    Debug.Log($"鏂囦欢鍔犺浇: {fileLoaded.playerName}, Lv.{fileLoaded.level}");
                }
            }

            // 18. XML鏂囦欢鎿嶄綔
            string xmlPath = Application.persistentDataPath + "/test_data.xml";
            ESDesignUtility.Matcher.ToXmlFile(xmlData, xmlPath);
            Debug.Log($"XML宸蹭繚瀛樺埌: {xmlPath}");

            ESDesignUtility.ExampleXML xmlFileLoaded = ESDesignUtility.Matcher.FromXmlFile<ESDesignUtility.ExampleXML>(xmlPath);
            if (xmlFileLoaded != null)
            {
                Debug.Log($"XML鏂囦欢鍔犺浇: {xmlFileLoaded.playerName}");
            }
        }
    }
}

