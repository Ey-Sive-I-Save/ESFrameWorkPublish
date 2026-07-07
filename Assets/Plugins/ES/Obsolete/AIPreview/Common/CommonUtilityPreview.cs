using UnityEngine;

namespace ES.AIPreview.Common
{
    /// <summary>
    /// 预览用通用方法草案：
    /// - 总结项目中常见的判空、日志等模式；
    /// - 不直接被现有代码引用，只作为未来重构的目标形态。
    /// </summary>
    public static class CommonUtilityPreview
    {
        /// <summary>
        /// 统一的 UnityEngine.Object 判空方案：
        /// 来源示例：Link 容器遍历中经常需要判断接收者是否已被销毁。
        /// 建议统一改为调用此方法，避免重复样板代码。
        /// </summary>
        public static bool IsUnityObjectAlive(object obj)
        {
            if (obj == null) return false;
            if (obj is Object uo)
            {
                return uo != null; // 利用 Unity 重载的 null
            }
            return true;
        }

        /// <summary>
        /// 统一的调试日志包装：
        /// 来源示例：ESTrackViewWindow, DevManagement 等使用 Debug.Log/LogWarning/LogError。
        /// 未来可在此集中控制日志开关、前缀等。
        /// </summary>
        public static void Log(string category, string message)
        {
            Debug.Log($"[ES:{category}] {message}");
        }
    }
}
