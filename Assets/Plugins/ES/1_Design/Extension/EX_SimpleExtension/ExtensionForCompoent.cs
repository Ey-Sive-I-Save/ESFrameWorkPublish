using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace ES
{

    public static class ExtensionForCompoent 
    {
        #region 常规脚本
        /// <summary>
        /// 获得父级不包括自己的脚本
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <param name="includeInactive">包含禁用的?</param>
        /// <returns></returns>
        public static T _GetCompoentInParentExcludeSelf<T>(this Component self,bool includeInactive=true) where T : Component
        {
            if (self == null || self.transform.parent == null) return null;
            return self.transform.parent.GetComponentInParent<T>(includeInactive);
        }
        /// <summary>
        /// 获得父级不包括自己的脚本
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="self"></param>
        /// <param name="includeInactive">包含禁用的?</param>
        /// <returns></returns>
        public static List<T> _GetCompoentsInChildExcludeSelf<T>(this Component self, bool includeInactive = true) where T : Component
        {
            if (self == null) return new List<T>();
            List<T> result = new List<T>(self.transform.childCount);
            for(int i = 0; i < self.transform.childCount; i++)
            {
                result.AddRange(self.transform.GetChild(i).GetComponentsInChildren<T>(includeInactive));
            }
            return result;
        }

        /// <summary>
        /// 到某个脚本对象的距离
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static float _DistanceTo(this Component self, Component other)
        {
            if (self == null || other == null) return -1f;
            return Vector3.Distance(self.transform.position, other.transform.position);
        }
        /// <summary>
        /// 计算与一个Vector3之间的距离
        /// </summary>
        public static float _DistanceTo(this Component self, Vector3 position)
        {
            if (self == null) return -1f;
            return Vector3.Distance(self.transform.position, position);
        }

        /// <summary>
        /// 判断另一个脚本是否在指定范围内
        /// </summary>
        /// <param name="self"></param>
        /// <param name="other">另一个脚本</param>
        /// <param name="range">范围</param>
        /// <returns></returns>
        public static bool _IsInRange(this Component self, Component other, float range)
        {
            if (self == null || other == null) return false;
            return self._DistanceTo(other) <= range;
        }

        /// <summary>
        /// 获取在屏幕上的位置
        /// </summary>
        public static Vector3 _GetScreenPosition(this Component self, Camera camera = null)
        {
            if (self == null) return Vector3.zero;

            if (camera == null) camera = Camera.main;
            if (camera == null) return Vector3.zero;

            return camera.WorldToScreenPoint(self.transform.position);
        }

        /// <summary>
        ///获得实现特定接口的Mono脚本(仅本脚本同级)
        /// </summary>
        public static List<T> _GetInterfaces<T>(this Component component)
        {
            if (component == null) return new List<T>();

            MonoBehaviour[] scripts = component.GetComponents<MonoBehaviour>();
            List<T> interfaces = new List<T>();

            foreach (MonoBehaviour script in scripts)
            {
                if (script is T interfaceObj)
                {
                    interfaces.Add(interfaceObj);
                }
            }
            return interfaces;
        }

        /// <summary>
        /// 获取或添加组件
        /// </summary>
        public static T _GetOrAddComponent<T>(this Component component) where T : Component
        {
            if (component == null) return null;
            T c = component.gameObject.GetComponent<T>();
            if (c == null) return component.gameObject.AddComponent<T>();
            return c;
        }
        #endregion

        #region Transform 专属的
        /// <summary>
        /// 重置Transform的位置、旋转和缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void _Reset(this Transform transform)
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 重置局部位置、旋转和缩放
        /// </summary>
        /// <param name="transform"></param>
        public static void _ResetLocal(this Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 设置X位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        public static void _SetPositionX(this Transform transform, float x)
        {
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
        }

        /// <summary>
        /// 设置Y位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="y"></param>
        public static void _SetPositionY(this Transform transform, float y)
        {
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        /// <summary>
        /// 设置Z位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="z"></param>
        public static void _SetPositionZ(this Transform transform, float z)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, z);
        }

        /// <summary>
        /// 设置局部X位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="x"></param>
        public static void _SetLocalPositionX(this Transform transform, float x)
        {
            transform.localPosition = new Vector3(x, transform.localPosition.y, transform.localPosition.z);
        }

        /// <summary>
        /// 设置局部Y位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="y"></param>
        public static void _SetLocalPositionY(this Transform transform, float y)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, y, transform.localPosition.z);
        }

        /// <summary>
        /// 设置局部Z位置
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="z"></param>
        public static void _SetLocalPositionZ(this Transform transform, float z)
        {
            transform.localPosition = new Vector3(transform.localPosition.x, transform.localPosition.y, z);
        }

        /// <summary>
        /// 获取所有一层的子物体
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static Transform[] _GetChildrensOneLayer(this Transform transform)
        {
            Transform[] children = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                children[i] = transform.GetChild(i);
            return children;
        }

        /// <summary>
        /// 销毁所有子物体
        /// </summary>
        /// <param name="transform"></param>
        public static void _DestroyAllChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(transform.GetChild(i).gameObject);
        }
        #endregion
    }
}

