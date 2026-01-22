using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Examples
{
    /// <summary>
    /// Creator API 演示 - 深拷贝与数据创建工具
    /// 提供对象深拷贝、集合克隆等功能
    /// </summary>
    public class Example_Creator : MonoBehaviour
    {
        [System.Serializable]
        public class TestData
        {
            public string name;
            public int value;
            public List<int> numbers;

            public TestData(string name, int value)
            {
                this.name = name;
                this.value = value;
                this.numbers = new List<int> { 1, 2, 3 };
            }
        }

        private void Start()
        {
            Debug.Log("=== Creator API 演示 ===");

            // 1. 深拷贝基础对象
            TestData original = new TestData("原始数据", 100);
            TestData cloned = ESDesignUtility.Creator.DeepClone(original);
            
            cloned.name = "克隆数据";
            cloned.value = 200;
            cloned.numbers.Add(4);

            Debug.Log($"原始: {original.name}, 值={original.value}, 列表长度={original.numbers.Count}");
            Debug.Log($"克隆: {cloned.name}, 值={cloned.value}, 列表长度={cloned.numbers.Count}");

            // 2. 深拷贝List集合
            List<int> originalList = new List<int> { 1, 2, 3, 4, 5 };
            List<int> clonedList = ESDesignUtility.Creator.DeepClone(originalList);
            
            clonedList.Add(6);
            Debug.Log($"原始列表长度: {originalList.Count}, 克隆列表长度: {clonedList.Count}");

            // 3. 深拷贝Dictionary
            Dictionary<string, int> originalDict = new Dictionary<string, int>
            {
                { "apple", 1 },
                { "banana", 2 }
            };
            Dictionary<string, int> clonedDict = ESDesignUtility.Creator.DeepClone(originalDict);
            
            clonedDict["orange"] = 3;
            Debug.Log($"原始字典: {originalDict.Count} 项, 克隆字典: {clonedDict.Count} 项");

            // 4. 深拷贝数组
            int[] originalArray = new int[] { 10, 20, 30 };
            int[] clonedArray = ESDesignUtility.Creator.DeepClone(originalArray);
            
            clonedArray[0] = 999;
            Debug.Log($"原始数组[0]={originalArray[0]}, 克隆数组[0]={clonedArray[0]}");

            // 5. 使用DeepCloneAnyObject（更底层的接口）
            object anyObj = new TestData("任意对象", 42);
            object clonedAnyObj = ESDesignUtility.Creator.DeepCloneAnyObject(anyObj, HardUnityObject: false);
            
            if (clonedAnyObj is TestData td)
            {
                Debug.Log($"任意对象克隆成功: {td.name}, 值={td.value}");
            }

            // 6. 深拷贝嵌套结构
            TestData nested = new TestData("嵌套", 1);
            nested.numbers = new List<int> { 100, 200, 300 };
            
            TestData nestedClone = ESDesignUtility.Creator.DeepClone(nested);
            nestedClone.numbers[0] = 999;
            
            Debug.Log($"原始嵌套列表[0]={nested.numbers[0]}, 克隆嵌套列表[0]={nestedClone.numbers[0]}");
        }
    }
}
