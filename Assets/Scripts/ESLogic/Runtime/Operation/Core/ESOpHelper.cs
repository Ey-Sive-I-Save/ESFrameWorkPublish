using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Operation 辅助方法。
    /// 当前主要提供从运行上下文中读取常用值的统一入口。
    /// </summary>
    public static class ESOpHelper
    {
        #region Context 数据源

        /// <summary>从 Context 读取 float。</summary>
        public static float GetFloatFromContext(string key, ESRuntimeTargetPack target, ESOpSupport support, float defaultValue = 0f)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetFloat(key, defaultValue);
        }

        /// <summary>从 Context 读取 int。</summary>
        public static int GetIntFromContext(string key, ESRuntimeTargetPack target, ESOpSupport support, int defaultValue = 0)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetInt(key, defaultValue);
        }

        /// <summary>从 Context 读取 bool。</summary>
        public static bool GetBoolFromContext(string key, ESRuntimeTargetPack target, ESOpSupport support, bool defaultValue = false)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetBool(key, defaultValue);
        }

        /// <summary>从 Context 读取 string。</summary>
        public static string GetStringFromContext(string key, ESRuntimeTargetPack target, ESOpSupport support, string defaultValue = "")
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetString(key, defaultValue);
        }

        /// <summary>从 Context 读取 Vector3。</summary>
        public static Vector3 GetVector3FromContext(string key, ESRuntimeTargetPack target, ESOpSupport support, Vector3 defaultValue = default)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetVector(key, defaultValue);
        }

        #endregion

        #region CacherPool 数据源

        /// <summary>从缓存读取 float。当前实现暂时复用 Context。</summary>
        public static float GetFloatFromCache(string key, ESRuntimeTargetPack target, ESOpSupport support, float defaultValue = 0f)
        {
            return GetFloatFromContext(key, target, support, defaultValue);
        }

        /// <summary>从缓存读取 int。当前实现暂时复用 Context。</summary>
        public static int GetIntFromCache(string key, ESRuntimeTargetPack target, ESOpSupport support, int defaultValue = 0)
        {
            return GetIntFromContext(key, target, support, defaultValue);
        }

        #endregion
    }
}
