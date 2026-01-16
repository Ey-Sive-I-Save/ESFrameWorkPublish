using UnityEngine;

namespace ES.AIPreview.Utils
{
    /// <summary>
    /// 小工具脚本合集原型：
    /// - 仅提供少量与调试/开发相关的静态方法示例；
    /// - 后续可根据项目需求扩展为独立工具集。
    /// </summary>
    public static class ESToolbox
    {
        /// <summary>
        /// 在场景中快速创建一个带有指定组件的 GameObject，用于测试。
        /// </summary>
        public static T CreateTestObject<T>(string name = null) where T : Component
        {
            var go = new GameObject(string.IsNullOrEmpty(name) ? typeof(T).Name : name);
            return go.AddComponent<T>();
        }

        /// <summary>
        /// 在 Scene 视图中绘制一个简单的调试射线。
        /// </summary>
        public static void DrawDebugRay(Vector3 origin, Vector3 dir, Color color, float duration = 1f)
        {
            Debug.DrawRay(origin, dir, color, duration);
        }
    }
}
