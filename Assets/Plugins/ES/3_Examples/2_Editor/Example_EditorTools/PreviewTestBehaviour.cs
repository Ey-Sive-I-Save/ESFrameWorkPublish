using UnityEngine;
using ES;

namespace ES.EditorTools
{
    /// <summary>
    /// 测试预览面板的 Mono 案例脚本。
    /// 挂载到场景物体后，无需任何额外配置，即可在 Inspector 底部看到预览效果。
    /// 此脚本可安全放置在普通（非 Editor）文件夹下运行。
    /// </summary>
    public class PreviewTestBehaviour : MonoBehaviour, IPreviewElement
    {
        // ----- 实现 IPreviewElement 接口 -----

        /// <summary> 是否允许预览 </summary>
        public bool CanPreview => true;

        /// <summary> 是否独占预览区域（设为 false，使其进入常规折叠区） </summary>
        public bool IsSingleArea => false;

        /// <summary>
        /// 游戏运行时的预览 GUI 绘制
        /// </summary>
        public void DrawPreviewGUIPlaying()
        {
            // 使用 #if 包裹仅存在于 Unity Editor 中的类型（如 EditorStyles）
#if UNITY_EDITOR
            GUILayout.Label($"<b>[ 运行时测试 ]</b>", UnityEditor.EditorStyles.boldLabel);
#else
            GUILayout.Label("[ 运行时测试 ]");
#endif
            
            GUILayout.Label($"当前运行时间: {Time.time:F2} 秒");

            if (GUILayout.Button("点我触发测试日志"))
            {
                Debug.Log("✅ 预览面板的运行时按钮被点击了！");
            }
        }

        /// <summary>
        /// 编辑器非运行时的预览 GUI 绘制
        /// </summary>
        public void EditorPreviewDrawPreviewGUINonPlay()
        {
#if UNITY_EDITOR
            GUILayout.Label($"<b>[ 编辑器测试 ]</b>", UnityEditor.EditorStyles.boldLabel);
#else
            GUILayout.Label("[ 编辑器测试 ]");
#endif
            GUILayout.Label("此时游戏未运行，你可以在这里展示配置信息或静态数据。");
        }
    }
}