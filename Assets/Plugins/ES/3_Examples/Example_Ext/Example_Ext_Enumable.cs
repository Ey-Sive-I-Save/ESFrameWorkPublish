using System;
using System.Collections.Generic;
using UnityEngine;

// 示例：演示 ExtForEnumable.cs 中集合/枚举扩展
// 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtForEnumable.cs
namespace ES
{
    public class Example_Ext_Enumable : MonoBehaviour
    {
        void Start()
        {
            // 基本数据
            var arr = new string[] { "apple", "banana", "cherry" };
            var list = new List<int> { 1, 2, 3, 4 };

            // 1) RandomItem / RandomShuffle
            Debug.Log($"Random from array: {arr._RandomItem("none")}");
            Debug.Log($"Random from list: {list._RandomItem(-1)}");

            list._RandomShuffle();
            Debug.Log($"Shuffled list: {string.Join(",", list)}");

            // 2) TryRandomItem 与 空情况
            var empty = new int[0];
            if (!empty._TryRandomItem(out var notFound))
            {
                Debug.Log("TryRandomItem on empty: returned false as expected");
            }

            // 3) 使用指定 RNG（可复现）
            var seed = 12345;
            var rng = new System.Random(seed);
            Debug.Log($"Random with seed from array: {arr._RandomItem(rng, "none")}");

            // 4) RandomSample / RandomShuffle 多样用法
            var sampleList = new List<int> { 10, 20, 30, 40, 50 };
            sampleList._RandomShuffle();
            Debug.Log($"SampleList shuffled: {string.Join(",", sampleList)}");

            var sample = sampleList._RandomSample(3);
            Debug.Log($"RandomSample(3): {string.Join(",", sample)}");

            var sampleWithReplacement = sampleList._RandomSample(3, withReplacement: true);
            Debug.Log($"RandomSample(3, withReplacement): {string.Join(",", sampleWithReplacement)}");

            // 5) WeightedRandomIndex 示例（权重为正数）
            var weights = new List<float> { 0.1f, 0.2f, 0.7f };
            var weights2 = new float[] { 0.1f, 0.2f, 0.7f };
            int picked = weights._WeightedRandomIndex();
            int picked2 = weights2._WeightedRandomIndex();

            Debug.Log($"WeightedRandomIndex picked: {picked} (weight {weights[picked]})");

            // 6) 获取随机索引集合（不重复）
           // var indices = sampleList._GetRandomIndices(3);
           // Debug.Log($"Random indices: {string.Join(",", indices)}");

            // 7) Bulk _RandomShuffle on array
            var arrNums = new int[] { 1, 2, 3, 4, 5, 6 };
            arrNums._RandomShuffle();
            Debug.Log($"Array shuffled: {string.Join(",", arrNums)}");

            // 8) TryRandomItem 的成功示例
            if (arr._TryRandomItem(out var pick))
            {
                Debug.Log($"TryRandomItem returned: {pick}");
            }
        }
    }
}
