using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtensionForGameObject 
    {
        // 41. 检查GameObject是否有特定组件
        public static bool _HasComponent<T>(this GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() != null;
        }

        // 42. 获取或添加组件
        public static T _GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        public static Component[] _GetAllComponent(this GameObject gameObject)
        {
          return gameObject.GetComponents<Component>();
        }

        // 43. 设置GameObject的激活状态
        public static void _SetActive(this GameObject gameObject, bool active)
        {
            if (gameObject != null && gameObject.activeSelf != active)
                gameObject.SetActive(active);
        }

        // 44. 切换GameObject的激活状态
        public static void _ToggleActive(this GameObject gameObject)
        {
            if (gameObject != null)
                gameObject.SetActive(!gameObject.activeSelf);
        }

        // 45. 销毁GameObject
        public static void _Destroy(this GameObject gameObject, float delay = 0f)
        {
            if (gameObject != null)
                UnityEngine.Object.Destroy(gameObject, delay);
        }

        // 46. 立即销毁GameObject
        public static void _DestroyImmediate(this GameObject gameObject)
        {
            if (gameObject != null)
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        // 47. 设置GameObject的层
        public static void _SetLayer(this GameObject gameObject, int layer, bool includeChildren = false)
        {
            if (gameObject == null) return;

            if (includeChildren)
            {
                Transform[] children = gameObject.GetComponentsInChildren<Transform>();
                foreach (Transform child in children)
                    child.gameObject.layer = layer;
            }
            else
            {
                gameObject.layer = layer;
            }
        }

        // 48. 检查GameObject是否在特定层
        public static bool _IsInLayer(this GameObject gameObject, int layer)
        {
            return gameObject != null && gameObject.layer == layer;
        }
        public static bool _IsInLayerMask(this GameObject gameObject, LayerMask mask)
        {
            return gameObject != null &&(1<<gameObject.layer & mask)>0;
        }
        // 49. 检查GameObject是否在特定标签
        public static bool _IsInTag(this GameObject gameObject, string tag)
        {
            return gameObject != null && gameObject.CompareTag(tag);
        }

        // 50. 查找子物体(包含非激活物体)
        public static Transform _FindChildRecursive(this GameObject parent, string name)
        {
            if (parent == null) return null;

            foreach (Transform child in parent.transform)
            {
                if (child.name == name) return child;
                Transform found = _FindChildRecursive(child.gameObject, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}

