using System;
using System.Linq;
using UnityEngine;

// 示例：演示 ExtForGameObject.cs 中常用 API 的用法
// 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtForGameObject.cs
// 把此脚本挂到场景中的任意 GameObject 上，Start 会执行一系列安全示例。
namespace ES
{
    public class Example_Ext_GameObject : MonoBehaviour
    {
        void Start()
        {
            // 创建一个测试根对象
            GameObject root = new GameObject("Example_GameObjectRoot");

            // 1) GetOrAddComponent
            var rb = root._GetOrAddComponent<Rigidbody>();
            Debug.Log($"_GetOrAddComponent<Rigidbody> -> {rb}");

            // 2) GetAllComponents (包含子物体示例)
            var childA = new GameObject("ChildA");
            childA.transform.SetParent(root.transform, worldPositionStays: true);
            childA._GetOrAddComponent<BoxCollider>();
          
            // 3) Safe set active / toggle
            root._SafeSetActive(false);
            Debug.Log("root set inactive via _SafeSetActive(false)");
            root._SafeToggleActive();
            Debug.Log("root toggled active state via _SafeToggleActive()");

            // 4) Safe destroy with delay and immediate (注意：立即销毁对场景有影响，示例注释化)
            var temp = new GameObject("TempToDestroy");
            temp._SafeDestroy(2f); // 2 秒后销毁
            Debug.Log("TempToDestroy scheduled for _SafeDestroy(2f)");

            // 5) Safe set layer / IsInLayerMask
            int testLayer = 8; // 示例层
            root._SafeSetLayer(testLayer, includeChildren: true);
            LayerMask mask = (1 << testLayer);
            Debug.Log($"root in mask? {root._IsInLayerMask(mask)}");

            // 6) Set parent keep world / FindOrCreateChild
            var newParent = new GameObject("NewParent");
            var childB = new GameObject("ChildB");
            childB.transform.position = new Vector3(1, 2, 3);
            childB._SetParentKeepWorld(newParent.transform, keepScale: true);
            Debug.Log($"ChildB parent changed, world pos preserved: {childB.transform.position}");

            var found = root._FindOrCreateChild("FoundChild");
            Debug.Log($"_FindOrCreateChild -> {found.name}");

            // 7) CopyTransform / ApplyTransform（示例：复制并应用部分变换）
            var src = new GameObject("SrcTrans");
            src.transform.position = new Vector3(5, 5, 5);
            src.transform.rotation = Quaternion.Euler(10, 20, 30);
            src.transform.localScale = Vector3.one * 2f;

            var dst = new GameObject("DstTrans");
            dst.transform.SetParent(root.transform);
            dst.transform._CopyTransform(src.transform, TransformCopyFlags.PositionLocal | TransformCopyFlags.RotationLocal );
            Debug.Log($"DstTrans position: {dst.transform.position}, rotation: {dst.transform.rotation.eulerAngles}");

            // 8) Destroy children (危险操作：示例仅打印并注释掉真实销毁)
            var parentToClear = new GameObject("ParentToClear");
            parentToClear._FindOrCreateChild("Child1");
            parentToClear._FindOrCreateChild("Child2");
            Debug.Log($"Children count before destroy: {parentToClear.transform.childCount}");
            // parentToClear._DestroyChildren(); // <- 真实销毁，留作手动测试（注释以避免数据丢失）

            // 9) Set active recursive
            var top = new GameObject("TopRecursive");
            var sub = top._FindOrCreateChild("Sub");
            top._SetActiveRecursive(false);
            Debug.Log("_SetActiveRecursive(false) applied on TopRecursive");

            // 10) Hierarchy path / GetScenePath (UnityEditor 会提供更多信息，运行时安全调用)
            Debug.Log($"Hierarchy path of dst: {dst._GetHierarchyPath()}");

            // 清理示例生成的根（延时销毁以便观察）
            root._SafeDestroy(10f);
            newParent._SafeDestroy(10f);
            src._SafeDestroy(10f);
            dst._SafeDestroy(10f);
            parentToClear._SafeDestroy(10f);
            top._SafeDestroy(10f);
        }
    }
}
