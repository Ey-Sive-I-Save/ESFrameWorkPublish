using System;
using UnityEngine;

// 示例：演示 ExtForUnityObject.cs 中常用方法
namespace ES
{
    public class Example_Ext_UnityObject : MonoBehaviour
    {
        public UnityEngine.Object exampleAsset;

        void Start()
        {
            // TryUse：如果对象存在则返回对象，否则 null
            var maybe = exampleAsset._TryUse();
            if (maybe != null)
            {
                Debug.Log("资源可用: " + maybe.name);
            }

            // 获取场景路径（针对 GameObject/Component）
            GameObject go = new GameObject("UO_Test");
            Debug.Log(go._GetScenePath());

            // IsInResources（编辑器判断）
            bool inRes = exampleAsset._IsInResources();
            Debug.Log("在 Resources 中: " + inRes);

            // SafeDestroy
            go._SafeDestroy();
        }
    }
}
