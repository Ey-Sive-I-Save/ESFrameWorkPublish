using UnityEngine;
using ES;
using System.Collections.Generic;
using System.Linq;

namespace ES.Samples{
    /// <summary>
    /// Function API 婕旂ず - 鏁板/瀹瑰櫒/瀛楃涓叉搷浣滃伐鍏?
    /// 鎻愪緵鏁板€艰绠椼€佸垪琛ㄦ搷浣溿€佸瓧绗︿覆澶勭悊绛夊姛鑳?
    /// </summary>
    public class Example_Function : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== Function API 婕旂ず ===");

            // ========== 鏁板杩愮畻 ==========
            Debug.Log("--- 鏁板杩愮畻 ---");

            // 1. 涓ゆ暟杩愮畻
            float result1 = ESDesignUtility.Function.HandleTwoFloat(10f, 3f, EnumCollect.HandleTwoNumber.Add);
            Debug.Log($"10 + 3 = {result1}");

            float result2 = ESDesignUtility.Function.HandleTwoFloat(10f, 3f, EnumCollect.HandleTwoNumber.Divide);
            Debug.Log($"10 / 3 = {result2}");

            float result3 = ESDesignUtility.Function.HandleTwoFloat(10f, 3f, EnumCollect.HandleTwoNumber.Model);
            Debug.Log($"10 % 3 = {result3}");

            // 2. 涓ゆ暟姣旇緝
            bool isGreater = ESDesignUtility.Function.CompareTwoFloat(10f, 5f, EnumCollect.CompareTwoNumber.Greater);
            Debug.Log($"10 > 5 = {isGreater}");

            bool isEqual = ESDesignUtility.Function.CompareTwoFloat(5f, 5f, EnumCollect.CompareTwoNumber.Equal);
            Debug.Log($"5 == 5 = {isEqual}");

            // ========== 瀹瑰櫒鎿嶄綔 ==========
            Debug.Log("--- 瀹瑰櫒鎿嶄綔 ---");

            List<int> numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            // 3. 浠庡垪琛ㄩ€夋嫨鍑犱釜锛堜娇鐢℅etSome锛?
            int lastIdx = 0;
            List<int> firstThree = ESDesignUtility.Function.GetSome(
                values: numbers,
                selectSomeType: EnumCollect.SelectSome.StartSome,
                lastIndex: ref lastIdx
            );
            Debug.Log($"鍓嶅嚑涓? {string.Join(", ", firstThree)}");

            // 4. 閫夋嫨涓€涓厓绱狅紙浣跨敤GetOne锛?
            lastIdx = 0;
            int selected = ESDesignUtility.Function.GetOne(
                values: numbers,
                selectOneType: EnumCollect.SelectOne.NotNullFirst,
                lastIndex: ref lastIdx
            );
            Debug.Log($"閫夋嫨绗竴涓? {selected}");

            // 5. 闅忔満閫夋嫨
            lastIdx = 0;
            int randomOne = ESDesignUtility.Function.GetOne(
                values: numbers,
                selectOneType: EnumCollect.SelectOne.RandomOnly,
                lastIndex: ref lastIdx
            );
            Debug.Log($"闅忔満閫夋嫨: {randomOne}");

            // 6. 绠€鍗曠瓫閫夌ず渚?
            List<int> filtered = numbers.Where(n => n > 5).ToList();
            Debug.Log($"澶т簬5鐨勬暟: {string.Join(", ", filtered)}");

            // ========== 瀛楃涓叉搷浣?==========
            Debug.Log("--- 瀛楃涓叉搷浣?---");

            // 7. 瀛楃涓叉搷浣滐紙鐩存帴浣跨敤C#鍐呯疆鍔熻兘婕旂ず锛?
            string str1 = "Hello";
            string str2 = " World";
            string combined = str1 + str2;
            Debug.Log($"瀛楃涓叉嫾鎺? {combined}");

            // 8. 瀛楃涓茶鑼冨寲
            string input = "test_string_name";
            string normalized = ESDesignUtility.Function.NormalizeString(
                input, 
                trim: true, 
                handleType: EnumCollect.HandleIndentStringName.StartToUpper
            );
            Debug.Log($"瀛楃涓茶鑼冨寲: {input} -> {normalized}");

            // 9. 杞崲涓哄悎娉曟爣璇嗙鍚?
            string identifier = ESDesignUtility.Function.HandleStringToIndentName(
                "my variable name", 
                EnumCollect.HandleIndentStringName.StartToUpper
            );
            Debug.Log($"鏍囪瘑绗﹁浆鎹? {identifier}");

            // ========== 瀹炵敤鍔熻兘 ==========
            Debug.Log("--- 瀹炵敤鍔熻兘 ---");

            // 10. 浣跨敤LINQ杩涜鎺掑簭鍜岄€夋嫨锛堟洿鐩磋锛?
            int maximum = numbers.Max();
            int minimum = numbers.Min();
            Debug.Log($"鏈€澶у€? {maximum}, 鏈€灏忓€? {minimum}");

            // 11. 鍒楄〃鎿嶄綔瀹炵敤绀轰緥
            var firstFive = numbers.Take(5).ToList();
            var lastFive = numbers.Skip(numbers.Count - 5).ToList();
            Debug.Log($"鍓?涓? {string.Join(", ", firstFive)}");
            Debug.Log($"鍚?涓? {string.Join(", ", lastFive)}");
        }
    }
}

