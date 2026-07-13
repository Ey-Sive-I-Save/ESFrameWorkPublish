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
            // 1. _TryUse()：安全使用对象，链式调用示例
            exampleAsset._TryUse().name=" RenamedAsset"; // 链式调用示例
            // 2. _TryUse(Action)：对象存在时执行回调
            exampleAsset._TryUse(obj => Debug.Log($"_TryUse(Action): {obj.name}"));

            // 3. _GetGUID（仅编辑器有效）
            Debug.Log($"_GetGUID: {exampleAsset._GetGUID()} // 获取资产GUID");

            // 4. _IsAsset（仅编辑器有效）
            Debug.Log($"_IsAsset: {exampleAsset._IsAsset()} // 是否为项目资产");

            // 5. _IsNullOrDestroyed
            Debug.Log($"_IsNullOrDestroyed: {exampleAsset._IsNullOrDestroyed()} // 是否为null或已销毁");

            // 6. _GetScenePath（GameObject/Component/其它）
            GameObject go = new GameObject("UO_Test");
            Debug.Log($"_GetScenePath: {go._GetScenePath()} // 获取场景路径");
            Debug.Log($"_GetScenePath(Component): {go.transform._GetScenePath()} // 获取组件场景路径");
            Debug.Log($"_GetScenePath(Asset): {exampleAsset._GetScenePath()} // 资产对象名");

            // 7. _IsInResources（仅编辑器有效）
            Debug.Log($"_IsInResources: {exampleAsset._IsInResources()} // 是否在Resources目录");

            // 8. _SafeDestroy（安全销毁对象）
            go._SafeDestroy();
            Debug.Log("_SafeDestroy: 已销毁GameObject");

            // 9. _GetHierarchyPath（层级路径）
            var go2 = new GameObject("Parent");
            var child = new GameObject("Child");
            child.transform.parent = go2.transform;
            Debug.Log($"_GetHierarchyPath: {child._GetHierarchyPath()} // 获取层级路径");

            // 10. _IsPrefabAsset（仅编辑器有效）
            Debug.Log($"_IsPrefabAsset: {exampleAsset._IsPrefabAsset()} // 是否为Prefab资产");

            // 11. _IsPrefabInstance（仅编辑器有效）
            Debug.Log($"_IsPrefabInstance: {exampleAsset._IsPrefabInstance()} // 是否为Prefab实例");
        }
    }
}
