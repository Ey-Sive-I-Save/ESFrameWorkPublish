using UnityEngine;
using ES;
using System.Reflection;

namespace ES.Samples{
    /// <summary>
    /// Reflection API 婕旂ず - 鍙嶅皠宸ュ叿
    /// 鎻愪緵瀛楁/灞炴€?鏂规硶鐨勮鍙栥€佽缃€佽皟鐢ㄧ瓑鍙嶅皠鎿嶄綔
    /// </summary>
    public class Example_Reflection : MonoBehaviour
    {
        public class TestClass
        {
            public string publicField = "鍏紑瀛楁";
            private int privateField = 42;
            public string PublicProperty { get; set; } = "鍏紑灞炴€?;
            private string PrivateProperty { get; set; } = "绉佹湁灞炴€?;

            public void PublicMethod()
            {
                Debug.Log("鍏紑鏂规硶琚皟鐢?);
            }

            private void PrivateMethod(string message)
            {
                Debug.Log($"绉佹湁鏂规硶琚皟鐢? {message}");
            }

            public int Add(int a, int b)
            {
                return a + b;
            }

            public string GetInfo()
            {
                return $"Field={privateField}, Prop={PrivateProperty}";
            }
        }

        private void Start()
        {
            Debug.Log("=== Reflection API 婕旂ず ===");

            TestClass obj = new TestClass();

            // ========== 瀛楁鎿嶄綔 ==========
            Debug.Log("--- 瀛楁鎿嶄綔 ---");

            // 1. 鑾峰彇鍏紑瀛楁
            object publicValue = ESDesignUtility.Reflection.GetField(obj, "publicField");
            Debug.Log($"鍏紑瀛楁鍊? {publicValue}");

            // 2. 鑾峰彇绉佹湁瀛楁
            object privateValue = ESDesignUtility.Reflection.GetField(obj, "privateField");
            Debug.Log($"绉佹湁瀛楁鍊? {privateValue}");

            // 3. 璁剧疆鍏紑瀛楁
            ESDesignUtility.Reflection.SetField(obj, "publicField", "鏂扮殑鍏紑瀛楁鍊?);
            Debug.Log($"淇敼鍚? {obj.publicField}");

            // 4. 璁剧疆绉佹湁瀛楁
            ESDesignUtility.Reflection.SetField(obj, "privateField", 999);
            object newPrivateValue = ESDesignUtility.Reflection.GetField(obj, "privateField");
            Debug.Log($"淇敼鍚庣殑绉佹湁瀛楁: {newPrivateValue}");

            // 5. 娉涘瀷鏂瑰紡鑾峰彇瀛楁
            string fieldValue = ESDesignUtility.Reflection.GetField<string>(obj, "publicField");
            Debug.Log($"娉涘瀷鑾峰彇瀛楁: {fieldValue}");

            // 6. 娉涘瀷鏂瑰紡璁剧疆瀛楁
            ESDesignUtility.Reflection.SetField<string>(obj, "publicField", "娉涘瀷璁剧疆");
            Debug.Log($"娉涘瀷璁剧疆鍚? {obj.publicField}");

            // 7. 灏濊瘯鑾峰彇瀛楁锛堜笉鎵撴棩蹇楋級
            if (ESDesignUtility.Reflection.TryGetField(obj, "privateField", out object tryValue))
            {
                Debug.Log($"TryGet鎴愬姛: {tryValue}");
            }

            // ========== 灞炴€ф搷浣?==========
            Debug.Log("--- 灞炴€ф搷浣?---");

            // 8. 鑾峰彇鍏紑灞炴€?
            object publicProp = ESDesignUtility.Reflection.GetProperty(obj, "PublicProperty");
            Debug.Log($"鍏紑灞炴€у€? {publicProp}");

            // 9. 鑾峰彇绉佹湁灞炴€?
            object privateProp = ESDesignUtility.Reflection.GetProperty(obj, "PrivateProperty");
            Debug.Log($"绉佹湁灞炴€у€? {privateProp}");

            // 10. 璁剧疆灞炴€?
            ESDesignUtility.Reflection.SetProperty(obj, "PublicProperty", "鏂板睘鎬у€?);
            Debug.Log($"淇敼鍚庣殑灞炴€? {obj.PublicProperty}");

            // 11. 娉涘瀷鏂瑰紡鎿嶄綔灞炴€?
            string propValue = ESDesignUtility.Reflection.GetProperty<string>(obj, "PublicProperty");
            Debug.Log($"娉涘瀷鑾峰彇灞炴€? {propValue}");

            ESDesignUtility.Reflection.SetProperty(obj, "PrivateProperty", "娉涘瀷璁剧疆绉佹湁灞炴€?);
            string newPropValue = ESDesignUtility.Reflection.GetProperty<string>(obj, "PrivateProperty");
            Debug.Log($"娉涘瀷璁剧疆绉佹湁灞炴€у悗: {newPropValue}");

            // ========== 鏂规硶璋冪敤 ==========
            Debug.Log("--- 鏂规硶璋冪敤 ---");

            // 12. 璋冪敤鏃犲弬鏂规硶
            ESDesignUtility.Reflection.InvokeMethod(obj, "PublicMethod");

            // 13. 璋冪敤甯﹀弬鏁扮殑绉佹湁鏂规硶
            ESDesignUtility.Reflection.InvokeMethod(obj, "PrivateMethod", "Hello from reflection!");

            // 14. 璋冪敤鏈夎繑鍥炲€肩殑鏂规硶
            object result = ESDesignUtility.Reflection.InvokeMethod(obj, "Add", 10, 20);
            if (result is int addResult)
            {
                Debug.Log($"Add(10, 20) = {addResult}");
            }

            // 15. 璋冪敤杩斿洖瀛楃涓茬殑鏂规硶
            object infoResult = ESDesignUtility.Reflection.InvokeMethod(obj, "GetInfo");
            if (infoResult is string info)
            {
                Debug.Log($"GetInfo(): {info}");
            }

            // 16. 浣跨敤TryInvokeMethodReturn瀹夊叏璋冪敤
            object[] parameters = new object[] { 5, 15 };
            if (ESDesignUtility.Reflection.TryInvokeMethodReturn(obj, "Add", out object tryResult, parameters))
            {
                Debug.Log($"TryInvoke鎴愬姛: {tryResult}");
            }

            // ========== 绫诲瀷淇℃伅鑾峰彇 ==========
            Debug.Log("--- 绫诲瀷淇℃伅 ---");

            // 17. 鑾峰彇鎵€鏈夊瓧娈碉紙浣跨敤鍙嶅皠API锛?
            var type = typeof(TestClass);
            var fields = type.GetFields(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance
            );
            Debug.Log($"绫绘湁 {fields.Length} 涓瓧娈?);

            // 18. 鑾峰彇鎵€鏈夊睘鎬?
            var properties = type.GetProperties(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance
            );
            Debug.Log($"绫绘湁 {properties.Length} 涓睘鎬?);

            // 19. 鑾峰彇鎵€鏈夋柟娉?
            var methods = type.GetMethods(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.DeclaredOnly
            );
            Debug.Log($"绫绘湁 {methods.Length} 涓柟娉?);
        }
    }
}

