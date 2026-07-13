using System;
using UnityEngine;

namespace ES
{
    // 示例：演示 ExtForCompoent.cs 中方法
    public class Example_Ext_Compoent : MonoBehaviour
    {
        void Start()
        {
            // 来源：Assets/Plugins/ES/1_Design/Extension/EX_SimpleExtension/ExtForCompoent.cs
            // 本脚本展示常用 Component/Transform 扩展方法的用例与输出，便于在场景中验证行为。

            var comp = this as Component;

            // 1) 获取父级组件（排除自身）
            var parentTransform = comp._GetCompoentInParentExcludeSelf<Transform>();
            Debug.Log($"_GetCompoentInParentExcludeSelf<Transform>() -> {parentTransform}");

            // 2) 获取子孙组件（不包含自身）
            var childTransforms = comp._GetCompoentsInChildExcludeSelf<Transform>(includeInactive: true);
            Debug.Log($"_GetCompoentsInChildExcludeSelf count: {childTransforms.Count}");

            // 3) 距离与范围判断（如果场景中存在另一个对象，请替换 targetObject）
            var targetObj = GameObject.FindWithTag("Player");
            if (targetObj != null)
            {
                var targetComp = targetObj.GetComponent<Transform>();
                float d = comp._DistanceTo(targetComp);
                Debug.Log($"_DistanceTo Player: {d}");
                Debug.Log($"_IsInRange(5f): {comp._IsInRange(targetComp, 5f)}");
            }

            // 4) 获取屏幕坐标
            Vector3 screen = comp._GetScreenPosition(Camera.main);
            Debug.Log($"_GetScreenPosition -> {screen}");

            // 5) 获取实现指定接口的组件示例
            var handlers = comp._GetInterfaces<IDisposable>();
            Debug.Log($"_GetInterfaces<IDisposable> -> {handlers.Count} items");

            // 6) 获取或添加组件
            var rb = comp._GetOrAddComponent<Rigidbody>();
            Debug.Log($"_GetOrAddComponent<Rigidbody> -> {rb}");

            // 7) Transform 专属：重置 / 位置设置
            transform._Reset();
            Debug.Log("_Reset called on transform");

            transform._SetPositionY(2f);
            Debug.Log($"_SetPositionY -> {transform.position}");

            // 8) 获取第一层子物体
            var firstLayer = transform._GetChildrensOneLayer();
            Debug.Log($"_GetChildrensOneLayer -> count: {firstLayer.Length}");

            // 9) 销毁所有子物体（示例：注释掉以避免误删场景对象）
            // transform._DestroyAllChildren();
            // Debug.Log("_DestroyAllChildren executed (commented out by default)");

            // 10) Fast 变体演示（不做 null 检查，仅用作性能参考）
            var fastChildren = transform._GetChildrensOneLayerFast();
            Debug.Log($"_GetChildrensOneLayerFast -> count: {fastChildren.Length}");
        }
    }
}
