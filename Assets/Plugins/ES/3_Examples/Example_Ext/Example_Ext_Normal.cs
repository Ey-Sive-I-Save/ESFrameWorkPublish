using System;
using System.Collections.Generic;
using UnityEngine;

// 示例：演示 ExtNormal.cs 中的通用工具方法
// 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtNormal.cs
namespace ES
{
    public class Example_Ext_Normal : MonoBehaviour
    {
        void Start()
        {
            // Swap 示例
            int a = 10, b = 20;
            Debug.Log($"Before swap: a={a}, b={b}");
            ExtNormal._Swap(ref a, ref b);
            Debug.Log($"After swap: a={a}, b={b}");

            // AsListOnlySelf / AsArrayOnlySelf
            var singleList = 42._AsListOnlySelf();
            Debug.Log($"AsListOnlySelf count: {singleList.Count}, first: {singleList[0]}");

            var singleArray = "hello"._AsArrayOnlySelf();
            Debug.Log($"AsArrayOnlySelf length: {singleArray.Length}, val: {singleArray[0]}");

            // GetTypeDisplayName（带线程安全缓存）
            Debug.Log($"Type display name for GameObject: {typeof(GameObject)._GetTypeDisplayName()}");

            // 边界与泛型示例
            var emptyList = ((string)null)._AsListOnlySelf();
            Debug.Log($"Null AsListOnlySelf count (expected 1 with null entry): {emptyList.Count}");

            // 使用 Swap 泛型（字符串）
            string s1 = "one", s2 = "two";
            Debug.Log($"Before swap strings: {s1}, {s2}");
            ExtNormal._Swap(ref s1, ref s2);
            Debug.Log($"After swap strings: {s1}, {s2}");

            // 将值包装为列表并迭代
            var listFromInt = 99._AsListOnlySelf();
            foreach (var v in listFromInt)
            {
                Debug.Log($"Iterated value from _AsListOnlySelf: {v}");
            }
        }
    }
}
