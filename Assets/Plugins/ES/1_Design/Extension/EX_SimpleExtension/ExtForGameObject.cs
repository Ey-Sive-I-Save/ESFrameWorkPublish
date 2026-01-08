using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{

    public static class ExtForGameObject
    {
        /// <summary>
        /// 获取或者添加组件
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static T _GetOrAddComponent<T>(this GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        /// <summary>
        /// 获取全部组件[]数组
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static Component[] _GetAllComponents(this GameObject gameObject)
        {
            return gameObject.GetComponents<Component>();
        }

        /// <summary>
        /// 安全地进行活动状态设置
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="active">活动状态</param>
        public static void _SafeSetActive(this GameObject gameObject, bool active)
        {
            if (gameObject != null && gameObject.activeSelf != active)
                gameObject.SetActive(active);
        }

        /// <summary>
        /// 安全地进行活动状态切换
        /// </summary>
        /// <param name="gameObject">活动状态</param>
        public static void _SafeToggleActive(this GameObject gameObject)
        {
            if (gameObject != null)
                gameObject.SetActive(!gameObject.activeSelf);
        }

        /// <summary>
        /// 安全销毁
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="delay"></param>
        public static void _SafeDestroy(this GameObject gameObject, float delay = 0f)
        {
            if (gameObject != null)
                UnityEngine.Object.Destroy(gameObject, delay);
        }

        /// <summary>
        /// 安全立刻销毁
        /// </summary>
        /// <param name="gameObject"></param>
        public static void _SafeDestroyImmediate(this GameObject gameObject)
        {
            if (gameObject != null)
                UnityEngine.Object.DestroyImmediate(gameObject);
        }

        /// <summary>
        /// 安全设置层级
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="layer"></param>
        /// <param name="includeChildren"></param>
        public static void _SafeSetLayer(this GameObject gameObject, int layer, bool includeChildren = false)
        {
            if (gameObject == null) return;

            if (includeChildren)
            {
                Transform[] children = gameObject.GetComponentsInChildren<Transform>();
                for(int i = 0; i < children.Length; i++)
                {
                    children[i].gameObject.layer = layer;
                }
            } 
            else
            {
                gameObject.layer = layer;
            }
        }
        /// <summary>
        /// 判断是否在一个LaerMask下
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="mask"></param>
        /// <returns></returns>
        public static bool _IsInLayerMask(this GameObject gameObject, LayerMask mask)
        {
            return (1 << gameObject.layer & mask) > 0;
        }

    }
}

