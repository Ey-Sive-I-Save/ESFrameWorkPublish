using UnityEngine;
using ES;
using System.Collections.Generic;

namespace ES.Examples
{
    /// <summary>
    /// TransformSetter API 演示 - Transform操作工具
    /// 提供父级设置、批量操作、位置旋转缩放初始化等功能
    /// </summary>
    public class Example_TransformSetter : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== TransformSetter API 演示 ===");

            // 1. 创建测试对象
            GameObject parent = new GameObject("ParentObject");
            GameObject child1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child1.name = "Child1";
            GameObject child2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child2.name = "Child2";

            // 2. 仅设置父级（保留当前位置）
            child1.transform.position = new Vector3(5, 5, 5);
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child1.transform,
                parent: parent.transform,
                localRot0: true,    // 重置旋转
                localScale0: true   // 重置缩放
            );
            Debug.Log($"Child1 世界位置保留: {child1.transform.position}");

            // 3. 设置父级并指定位置（世界坐标）
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child2.transform,
                parent: parent.transform,
                pos: new Vector3(0, 2, 0),  // 指定世界位置
                atWorldPos: true,
                localRot0: true,
                localScale0: true
            );
            Debug.Log($"Child2 设置世界位置: {child2.transform.position}");

            // 4. 设置父级并指定本地位置
            GameObject child3 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            child3.name = "Child3";
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child3.transform,
                parent: parent.transform,
                pos: new Vector3(1, 0, 0),  // 本地位置
                atWorldPos: false,
                localRot0: true,
                localScale0: false  // 不重置缩放
            );
            Debug.Log($"Child3 本地位置: {child3.transform.localPosition}");

            // 5. pos为null时不修改位置
            GameObject child4 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            child4.name = "Child4";
            child4.transform.position = new Vector3(10, 10, 10);
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: child4.transform,
                parent: parent.transform,
                pos: null,  // 不修改位置
                atWorldPos: true,
                localRot0: false,  // 不重置旋转
                localScale0: false // 不重置缩放
            );
            Debug.Log($"Child4 位置未改变: {child4.transform.position}");

            // 6. 批量操作多个Transform
            List<Transform> children = new List<Transform>
            {
                child1.transform,
                child2.transform,
                child3.transform
            };

            GameObject batchParent = new GameObject("BatchParent");
            ESDesignUtility.TransformSetter.HandleTransformsAtParent(
                transforms: children,
                parent: batchParent.transform,
                pos: Vector3.zero,
                atWorldPos: false,
                localRot0: true,
                localScale0: true
            );
            Debug.Log("批量处理完成：3个子对象已移动到BatchParent");

            // 7. 典型应用场景：UI元素初始化
            GameObject canvas = new GameObject("Canvas");
            GameObject uiPanel = new GameObject("UIPanel");
            
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: uiPanel.transform,
                parent: canvas.transform,
                pos: Vector3.zero,
                atWorldPos: false,  // UI使用本地坐标
                localRot0: true,
                localScale0: true
            );
            Debug.Log("UI Panel 已初始化到Canvas下");

            // 8. 对象池回收示例
            GameObject pooledObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pooledObj.transform.position = new Vector3(100, 200, 300);
            pooledObj.transform.rotation = Quaternion.Euler(45, 90, 0);
            pooledObj.transform.localScale = new Vector3(2, 2, 2);

            GameObject poolRoot = new GameObject("PoolRoot");
            ESDesignUtility.TransformSetter.HandleTransformAtParent(
                me: pooledObj.transform,
                parent: poolRoot.transform,
                pos: Vector3.zero,
                atWorldPos: false,
                localRot0: true,   // 重置旋转
                localScale0: true  // 重置缩放
            );
            Debug.Log("对象池对象已重置并回收");
        }
    }
}
