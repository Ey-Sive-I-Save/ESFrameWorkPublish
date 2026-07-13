using UnityEngine;
using ES;
using System.Collections.Generic;
using System.Linq;

namespace ES.Examples
{
    /// <summary>
    /// Function API 演示 - 数学/容器/字符串操作工具
    /// 提供数值计算、列表操作、字符串处理等功能
    /// </summary>
    public class Example_Function : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== Function API 演示 ===");

            // ========== 数学运算 ==========
            Debug.Log("--- 数学运算 ---");

            // 1. 两数运算
            float result1 = ESDesignUtility.Function.HandleTwoFloat(10f, 3f, EnumCollect.HandleTwoNumber.Add);
            Debug.Log($"10 + 3 = {result1}");

            float result2 = ESDesignUtility.Function.HandleTwoFloat(10f, 3f, EnumCollect.HandleTwoNumber.Divide);
            Debug.Log($"10 / 3 = {result2}");

            float result3 = ESDesignUtility.Function.HandleTwoFloat(10f, 3f, EnumCollect.HandleTwoNumber.Model);
            Debug.Log($"10 % 3 = {result3}");

            // 2. 两数比较
            bool isGreater = ESDesignUtility.Function.CompareTwoFloat(10f, 5f, EnumCollect.CompareTwoNumber.Greater);
            Debug.Log($"10 > 5 = {isGreater}");

            bool isEqual = ESDesignUtility.Function.CompareTwoFloat(5f, 5f, EnumCollect.CompareTwoNumber.Equal);
            Debug.Log($"5 == 5 = {isEqual}");

            // ========== 容器操作 ==========
            Debug.Log("--- 容器操作 ---");

            List<int> numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

            // 3. 从列表选择几个（使用GetSome）
            int lastIdx = 0;
            List<int> firstThree = ESDesignUtility.Function.GetSome(
                values: numbers,
                selectSomeType: EnumCollect.SelectSome.StartSome,
                lastIndex: ref lastIdx
            );
            Debug.Log($"前几个: {string.Join(", ", firstThree)}");

            // 4. 选择一个元素（使用GetOne）
            lastIdx = 0;
            int selected = ESDesignUtility.Function.GetOne(
                values: numbers,
                selectOneType: EnumCollect.SelectOne.NotNullFirst,
                lastIndex: ref lastIdx
            );
            Debug.Log($"选择第一个: {selected}");

            // 5. 随机选择
            lastIdx = 0;
            int randomOne = ESDesignUtility.Function.GetOne(
                values: numbers,
                selectOneType: EnumCollect.SelectOne.RandomOnly,
                lastIndex: ref lastIdx
            );
            Debug.Log($"随机选择: {randomOne}");

            // 6. 简单筛选示例
            List<int> filtered = numbers.Where(n => n > 5).ToList();
            Debug.Log($"大于5的数: {string.Join(", ", filtered)}");

            // ========== 字符串操作 ==========
            Debug.Log("--- 字符串操作 ---");

            // 7. 字符串操作（直接使用C#内置功能演示）
            string str1 = "Hello";
            string str2 = " World";
            string combined = str1 + str2;
            Debug.Log($"字符串拼接: {combined}");

            // 8. 字符串规范化
            string input = "test_string_name";
            string normalized = ESDesignUtility.Function.NormalizeString(
                input, 
                trim: true, 
                handleType: EnumCollect.HandleIndentStringName.StartToUpper
            );
            Debug.Log($"字符串规范化: {input} -> {normalized}");

            // 9. 转换为合法标识符名
            string identifier = ESDesignUtility.Function.HandleStringToIndentName(
                "my variable name", 
                EnumCollect.HandleIndentStringName.StartToUpper
            );
            Debug.Log($"标识符转换: {identifier}");

            // ========== 实用功能 ==========
            Debug.Log("--- 实用功能 ---");

            // 10. 使用LINQ进行排序和选择（更直观）
            int maximum = numbers.Max();
            int minimum = numbers.Min();
            Debug.Log($"最大值: {maximum}, 最小值: {minimum}");

            // 11. 列表操作实用示例
            var firstFive = numbers.Take(5).ToList();
            var lastFive = numbers.Skip(numbers.Count - 5).ToList();
            Debug.Log($"前5个: {string.Join(", ", firstFive)}");
            Debug.Log($"后5个: {string.Join(", ", lastFive)}");
        }
    }
}
