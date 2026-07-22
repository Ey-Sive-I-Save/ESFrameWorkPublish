using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Samples{
    /// <summary>
    /// Creator API 婕旂ず - 娣辨嫹璐濅笌鏁版嵁鍒涘缓宸ュ叿
    /// 鎻愪緵瀵硅薄娣辨嫹璐濄€侀泦鍚堝厠闅嗙瓑鍔熻兘
    /// </summary>
    public class Example_Creator : MonoBehaviour
    {
        [System.Serializable]
        public class TestData : IDeepClone<TestData>
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

            public void DeepCloneFrom(TestData t)
            {
                this.name = t.name;
                this.value = t.value;
                this.numbers = ESDesignUtility.Creator.DeepClone(t.numbers);
            }
        }

        private void Start()
        {
            Debug.Log("=== Creator API 婕旂ず ===");

            // 1. 娣辨嫹璐濆熀纭€瀵硅薄
            TestData original = new TestData("鍘熷鏁版嵁", 100);
            TestData cloned = ESDesignUtility.Creator.DeepClone(original);
            
            cloned.name = "鍏嬮殕鏁版嵁";
            cloned.value = 200;
            cloned.numbers.Add(4);

            Debug.Log($"鍘熷: {original.name}, 鍊?{original.value}, 鍒楄〃闀垮害={original.numbers.Count}");
            Debug.Log($"鍏嬮殕: {cloned.name}, 鍊?{cloned.value}, 鍒楄〃闀垮害={cloned.numbers.Count}");

            // 2. 娣辨嫹璐滾ist闆嗗悎
            List<int> originalList = new List<int> { 1, 2, 3, 4, 5 };
            List<int> clonedList = ESDesignUtility.Creator.DeepClone(originalList);
            
            clonedList.Add(6);
            Debug.Log($"鍘熷鍒楄〃闀垮害: {originalList.Count}, 鍏嬮殕鍒楄〃闀垮害: {clonedList.Count}");

            // 3. 娣辨嫹璐滵ictionary
            Dictionary<string, int> originalDict = new Dictionary<string, int>
            {
                { "apple", 1 },
                { "banana", 2 }
            };
            Dictionary<string, int> clonedDict = ESDesignUtility.Creator.DeepClone(originalDict);
            
            clonedDict["orange"] = 3;
            Debug.Log($"鍘熷瀛楀吀: {originalDict.Count} 椤? 鍏嬮殕瀛楀吀: {clonedDict.Count} 椤?);

            // 4. 娣辨嫹璐濇暟缁?
            int[] originalArray = new int[] { 10, 20, 30 };
            int[] clonedArray = ESDesignUtility.Creator.DeepClone(originalArray);
            
            clonedArray[0] = 999;
            Debug.Log($"鍘熷鏁扮粍[0]={originalArray[0]}, 鍏嬮殕鏁扮粍[0]={clonedArray[0]}");

            // 5. 浣跨敤DeepCloneAnyObject锛堟洿搴曞眰鐨勬帴鍙ｏ級
            object anyObj = new TestData("浠绘剰瀵硅薄", 42);
            object clonedAnyObj = ESDesignUtility.Creator.DeepCloneAnyObject(anyObj, HardUnityObject: false);
            
            if (clonedAnyObj is TestData td)
            {
                Debug.Log($"浠绘剰瀵硅薄鍏嬮殕鎴愬姛: {td.name}, 鍊?{td.value}");
            }

            // 6. 娣辨嫹璐濆祵濂楃粨鏋?
            TestData nested = new TestData("宓屽", 1);
            nested.numbers = new List<int> { 100, 200, 300 };
            
            TestData nestedClone = ESDesignUtility.Creator.DeepClone(nested);
            nestedClone.numbers[0] = 999;
            
            Debug.Log($"鍘熷宓屽鍒楄〃[0]={nested.numbers[0]}, 鍏嬮殕宓屽鍒楄〃[0]={nestedClone.numbers[0]}");
        }
    }
}

