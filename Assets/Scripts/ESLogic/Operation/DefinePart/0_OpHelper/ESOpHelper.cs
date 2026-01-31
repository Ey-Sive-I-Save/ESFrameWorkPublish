using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// ES框架 - 操作辅助工具 (ESOpHelper)
    /// 【数值获取和操作的统一接口】
    ///
    /// 【核心功能】
    /// 提供统一的数值获取接口，支持从多种数据源获取数值：
    /// Context上下文、CacherPool缓存池、实体对象等
    ///
    /// 【设计优势】
    /// • 统一接口：所有数值获取都通过静态方法调用
    /// • 类型安全：泛型方法确保类型正确性
    /// • 智能默认值：根据数据类型和使用场景提供合理的默认值
    /// • 扩展性：易于添加新的数据源
    /// • 性能优化：集中管理减少重复代码
    ///
    /// 【使用模式】
    /// <code>
    /// // 从Context获取float值（使用智能默认值）
    /// float health = ESOpHelper.GetFloatFromContext("health", target, support);
    ///
    /// // 从Context获取int值（可覆盖默认值）
    /// int level = ESOpHelper.GetIntFromContext("level", target, support, 1);
    ///
    /// // 从实体获取属性值
    /// float attack = ESOpHelper.GetFloatFromEntity(entity, "attack");
    /// </code>
    /// </summary>
    public static class ESOpHelper
    {
        #region Context数据源

        /// <summary>
        /// 从Context获取float值
        /// 【上下文数据】从操作支持器的上下文获取数值
        /// 【智能默认值】float类型默认值为0f，适合大多数数值计算
        /// </summary>
        public static float GetFloatFromContext(string key, ESRuntimeTarget target, IOpSupporter support, float defaultValue = 0f)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetFloat(key, defaultValue);
        }

        /// <summary>
        /// 从Context获取int值
        /// 【上下文数据】从操作支持器的上下文获取整数值
        /// 【智能默认值】int类型默认值为0，适合等级、数量等整数属性
        /// </summary>
        public static int GetIntFromContext(string key, ESRuntimeTarget target, IOpSupporter support, int defaultValue = 0)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetInt(key, defaultValue);
        }

        /// <summary>
        /// 从Context获取bool值
        /// 【上下文数据】从操作支持器的上下文获取布尔值
        /// 【智能默认值】bool类型默认值为false，适合状态标志
        /// </summary>
        public static bool GetBoolFromContext(string key, ESRuntimeTarget target, IOpSupporter support, bool defaultValue = false)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetBool(key, defaultValue);
        }

        /// <summary>
        /// 从Context获取string值
        /// 【上下文数据】从操作支持器的上下文获取字符串值
        /// 【智能默认值】string类型默认值为空字符串，避免null引用
        /// </summary>
        public static string GetStringFromContext(string key, ESRuntimeTarget target, IOpSupporter support, string defaultValue = "")
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetString(key, defaultValue);
        }

        /// <summary>
        /// 从Context获取Vector3值
        /// 【上下文数据】从操作支持器的上下文获取向量值
        /// 【智能默认值】Vector3类型默认值为Vector3.zero，适合位置、方向等向量数据
        /// </summary>
        public static Vector3 GetVector3FromContext(string key, ESRuntimeTarget target, IOpSupporter support, Vector3 defaultValue = default)
        {
            if (support?.Context == null)
                return defaultValue;

            return support.Context.GetVector(key, defaultValue);
        }

        #endregion


        #region CacherPool数据源

        /// <summary>
        /// 从CacherPool获取float值
        /// 【缓存数据】从缓存池中获取数值，支持高性能访问
        /// 【智能默认值】float类型默认值为0f，与Context保持一致
        /// </summary>
        public static float GetFloatFromCache(string key, ESRuntimeTarget target, IOpSupporter support, float defaultValue = 0f)
        {
            // 这里需要根据实际的CacherPool实现来调整
            // 暂时使用Context作为示例
            return GetFloatFromContext(key, target, support, defaultValue);
        }

        /// <summary>
        /// 从CacherPool获取int值
        /// 【缓存数据】从缓存池中获取整数值，支持高性能访问
        /// 【智能默认值】int类型默认值为0，与Context保持一致
        /// </summary>
        public static int GetIntFromCache(string key, ESRuntimeTarget target, IOpSupporter support, int defaultValue = 0)
        {
            // 这里需要根据实际的CacherPool实现来调整
            // 暂时使用Context作为示例
            return GetIntFromContext(key, target, support, defaultValue);
        }

        #endregion




    }
}