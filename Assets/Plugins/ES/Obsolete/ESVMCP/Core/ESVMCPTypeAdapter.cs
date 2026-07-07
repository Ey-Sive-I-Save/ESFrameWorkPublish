using UnityEngine;
using System;

namespace ES.VMCP
{
    /// <summary>
    /// 智能类型适配器 - 自动处理GameObject和Component之间的转换
    /// </summary>
    public static class TypeAdapter
    {
        /// <summary>
        /// 智能转换为GameObject
        /// 支持：GameObject、Component、Transform等
        /// </summary>
        public static GameObject ToGameObject(object obj)
        {
            if (obj == null)
                return null;

            // 已经是GameObject
            if (obj is GameObject go)
                return go;

            // Component类型（包括Transform）
            if (obj is Component component)
                return component.gameObject;

            // MonoBehaviour
            if (obj is MonoBehaviour monoBehaviour)
                return monoBehaviour.gameObject;

            // 尝试从Transform
            if (obj is Transform transform)
                return transform.gameObject;

            Debug.LogWarning($"[TypeAdapter] 无法将类型 {obj.GetType().Name} 转换为GameObject");
            return null;
        }

        /// <summary>
        /// 智能转换为Component
        /// </summary>
        public static T ToComponent<T>(object obj) where T : Component
        {
            if (obj == null)
                return null;

            // 已经是目标类型
            if (obj is T component)
                return component;

            // 从GameObject获取
            if (obj is GameObject go)
                return go.GetComponent<T>();

            // 从其他Component获取
            if (obj is Component otherComponent)
                return otherComponent.GetComponent<T>();

            Debug.LogWarning($"[TypeAdapter] 无法将类型 {obj.GetType().Name} 转换为{typeof(T).Name}");
            return null;
        }

        /// <summary>
        /// 智能转换为Transform
        /// </summary>
        public static Transform ToTransform(object obj)
        {
            if (obj == null)
                return null;

            // 已经是Transform
            if (obj is Transform transform)
                return transform;

            // 从GameObject获取
            if (obj is GameObject go)
                return go.transform;

            // 从Component获取
            if (obj is Component component)
                return component.transform;

            Debug.LogWarning($"[TypeAdapter] 无法将类型 {obj.GetType().Name} 转换为Transform");
            return null;
        }

        /// <summary>
        /// 获取指定类型的Component（智能转换）
        /// </summary>
        public static Component GetComponent(object obj, Type componentType)
        {
            if (obj == null || componentType == null)
                return null;

            GameObject go = ToGameObject(obj);
            if (go == null)
                return null;

            return go.GetComponent(componentType);
        }

        /// <summary>
        /// 检查对象是否包含指定组件
        /// </summary>
        public static bool HasComponent<T>(object obj) where T : Component
        {
            GameObject go = ToGameObject(obj);
            return go != null && go.GetComponent<T>() != null;
        }

        /// <summary>
        /// 检查对象是否包含指定组件（按类型）
        /// </summary>
        public static bool HasComponent(object obj, Type componentType)
        {
            GameObject go = ToGameObject(obj);
            return go != null && go.GetComponent(componentType) != null;
        }

        /// <summary>
        /// 验证对象类型（用于参数验证）
        /// </summary>
        public static bool IsValidUnityObject(object obj)
        {
            if (obj == null)
                return false;

            return obj is GameObject || obj is Component || obj is Transform;
        }

        /// <summary>
        /// 获取对象的实例ID（用于记忆和跟踪）
        /// </summary>
        public static int GetInstanceID(object obj)
        {
            if (obj is UnityEngine.Object unityObj)
                return unityObj.GetInstanceID();

            return 0;
        }

        /// <summary>
        /// 获取对象的名称（用于日志和调试）
        /// </summary>
        public static string GetName(object obj)
        {
            if (obj == null)
                return "null";

            if (obj is GameObject go)
                return go.name;

            if (obj is Component component)
                return component.gameObject.name;

            if (obj is UnityEngine.Object unityObj)
                return unityObj.name;

            return obj.ToString();
        }

        /// <summary>
        /// 尝试自动修复类型不匹配问题
        /// 例如：需要GameObject但提供了Component
        /// </summary>
        public static object AutoFix(object obj, Type expectedType)
        {
            if (obj == null || expectedType == null)
                return obj;

            // 类型匹配，无需修复
            if (expectedType.IsInstanceOfType(obj))
                return obj;

            try
            {
                // 需要GameObject
                if (expectedType == typeof(GameObject))
                    return ToGameObject(obj);

                // 需要Transform
                if (expectedType == typeof(Transform))
                    return ToTransform(obj);

                // 需要Component
                if (expectedType.IsSubclassOf(typeof(Component)))
                    return GetComponent(obj, expectedType);

                Debug.LogWarning($"[TypeAdapter] 无法自动修复类型不匹配: {obj.GetType().Name} → {expectedType.Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TypeAdapter] 类型转换失败: {ex.Message}");
            }

            return obj;
        }

        /// <summary>
        /// 批量转换为GameObject
        /// </summary>
        public static GameObject[] ToGameObjects(object[] objects)
        {
            if (objects == null || objects.Length == 0)
                return new GameObject[0];

            GameObject[] results = new GameObject[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                results[i] = ToGameObject(objects[i]);
            }

            return results;
        }
    }
}