using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Examples
{
    /// <summary>
    /// Matcher API 演示 - 序列化与类型转换工具
    /// 提供JSON/XML/Binary序列化、类型转换、PlayerPrefs封装等功能
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
            Debug.Log("=== Matcher API 演示 ===");

            // ========== 类型转换 ==========
            Debug.Log("--- 类型转换 ---");

            // 1. 字符串转数字
            int intValue = ESDesignUtility.Matcher.SystemObjectToT<int>("123", defaultValue: 0);
            Debug.Log($"字符串 '123' 转 int: {intValue}");

            float floatValue = ESDesignUtility.Matcher.SystemObjectToT<float>("45.67", defaultValue: 0f);
            Debug.Log($"字符串 '45.67' 转 float: {floatValue}");

            // 2. 转换失败返回默认值
            int failed = ESDesignUtility.Matcher.SystemObjectToT<int>("not_a_number", defaultValue: 999);
            Debug.Log($"转换失败返回默认值: {failed}");

            // 3. bool转换
            bool boolValue = ESDesignUtility.Matcher.SystemObjectToT<bool>("true", defaultValue: false);
            Debug.Log($"字符串 'true' 转 bool: {boolValue}");

            // ========== JSON 序列化（Unity原生）==========
            Debug.Log("--- Unity JSON 序列化 ---");

            PlayerData player = new PlayerData("Hero", 10, 100f);

            // 4. 对象转JSON
            string json = ESDesignUtility.Matcher.ToJson(player);
            Debug.Log($"JSON: {json}");

            // 5. JSON转对象
            PlayerData restored = ESDesignUtility.Matcher.FromJson<PlayerData>(json);
            Debug.Log($"还原: {restored.playerName}, Lv.{restored.level}, HP={restored.health}");

            // 6. JSON格式化输出
            string prettyJson = ESDesignUtility.Matcher.ToJson(player, prettyPrint: true);
            Debug.Log($"格式化JSON:\n{prettyJson}");

            // ========== Odin JSON 序列化（支持多态）==========
            Debug.Log("--- Odin JSON 序列化 ---");

            // 7. 使用Odin序列化（支持接口、继承等）
            string odinJson = ESDesignUtility.Matcher.ToOdinJson(player);
            Debug.Log($"Odin JSON: {odinJson}");

            // 8. Odin反序列化
            PlayerData odinRestored = ESDesignUtility.Matcher.FromOdinJson<PlayerData>(odinJson);
            Debug.Log($"Odin还原: {odinRestored.playerName}");

            // ========== 二进制序列化 ==========
            Debug.Log("--- 二进制序列化 ---");

            // 9. 对象转二进制
            byte[] binaryData = ESDesignUtility.Matcher.ToBinary(player);
            Debug.Log($"二进制数据大小: {binaryData.Length} 字节");

            // 10. 二进制转对象
            PlayerData binaryRestored = ESDesignUtility.Matcher.FromBinary<PlayerData>(binaryData);
            Debug.Log($"二进制还原: {binaryRestored.playerName}, Lv.{binaryRestored.level}");

            // ========== XML 序列化 ==========
            Debug.Log("--- XML 序列化 ---");

            // 11. 创建XML示例数据
            ESDesignUtility.ExampleXML xmlData = new ESDesignUtility.ExampleXML(
                "XMLPlayer",  // playerName
                5,            // level  
                80f,          // health
                1001          // id
            );

            // 12. 对象转XML字符串
            string xml = ESDesignUtility.Matcher.ToXmlString(xmlData);
            Debug.Log($"XML:\n{xml}");

            // 13. XML字符串转对象
            ESDesignUtility.ExampleXML xmlRestored = ESDesignUtility.Matcher.FromXmlString<ESDesignUtility.ExampleXML>(xml);
            Debug.Log($"XML还原: {xmlRestored.playerName}, Lv.{xmlRestored.level}");

            // ========== PlayerPrefs 封装 ==========
            Debug.Log("--- PlayerPrefs 封装 ---");

            // 14. 保存对象到PlayerPrefs
            ESDesignUtility.Matcher.SaveToPlayerPrefs("player_data", player);
            Debug.Log("数据已保存到PlayerPrefs");

            // 15. 从PlayerPrefs加载对象
            PlayerData prefsLoaded = ESDesignUtility.Matcher.LoadFromPlayerPrefs<PlayerData>("player_data");
            if (prefsLoaded != null)
            {
                Debug.Log($"PlayerPrefs加载: {prefsLoaded.playerName}, Lv.{prefsLoaded.level}");
            }

            // ========== 文件操作 ==========
            Debug.Log("--- 文件操作 ---");

            string testPath = Application.persistentDataPath + "/test_data.json";

            // 16. 保存到文件（使用异步版本）
            string json2 = ESDesignUtility.Matcher.ToJson(player);
            System.IO.File.WriteAllText(testPath, json2);
            Debug.Log($"已保存到文件: {testPath}");

            // 17. 从文件加载
            if (System.IO.File.Exists(testPath))
            {
                string loadedJson = System.IO.File.ReadAllText(testPath);
                PlayerData fileLoaded = ESDesignUtility.Matcher.FromJson<PlayerData>(loadedJson);
                if (fileLoaded != null)
                {
                    Debug.Log($"文件加载: {fileLoaded.playerName}, Lv.{fileLoaded.level}");
                }
            }

            // 18. XML文件操作
            string xmlPath = Application.persistentDataPath + "/test_data.xml";
            ESDesignUtility.Matcher.ToXmlFile(xmlData, xmlPath);
            Debug.Log($"XML已保存到: {xmlPath}");

            ESDesignUtility.ExampleXML xmlFileLoaded = ESDesignUtility.Matcher.FromXmlFile<ESDesignUtility.ExampleXML>(xmlPath);
            if (xmlFileLoaded != null)
            {
                Debug.Log($"XML文件加载: {xmlFileLoaded.playerName}");
            }
        }
    }
}
