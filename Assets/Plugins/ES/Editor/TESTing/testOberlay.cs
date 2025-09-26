using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;

[InitializeOnLoad]
public class FullHierarchyOverlay
{
    static FullHierarchyOverlay()
    {
        // 延迟调用以确保编辑器完全启动
        EditorApplication.delayCall += InitializeOverlay;
    }

    static void InitializeOverlay()
    {
        // 获取所有已打开的 Hierarchy 窗口
        var hierarchyWindows = Resources.FindObjectsOfTypeAll(
            Assembly.Load("UnityEditor.CoreModule").GetType("UnityEditor.SceneHierarchyWindow")
        );

        foreach (EditorWindow window in hierarchyWindows)
        {
            // 移除已有的绘制委托，避免重复添加
            var onGUI = window.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
            if (onGUI != null)
            {
                // 这里通常需要更复杂的 Hook 技术来注入自定义绘制代码
                // 例如，使用 Detour 或创建装饰器窗口。
                // 下面是一种更简单但非侵入式的思路：
            }
        }

        // 更实际的做法：订阅每帧更新，然后在世界空间(GUI)中根据窗口位置绘制
        EditorApplication.update += OnEditorUpdate;
    }

    static void OnEditorUpdate()
    {
        // 查找Hierarchy窗口
        var hierarchyWindow = GetHierarchyWindow();
        if (hierarchyWindow != null)
        {
            // 强制重绘该窗口，这会间接导致我们的绘制代码被执行
            hierarchyWindow.Repaint();
        }
    }

    // 一个更可行的方案：创建一个透明的、无边框的窗口，覆盖在Hierarchy窗口上
    public class OverlayWindow : EditorWindow
    {
        void OnGUI()
        {
            // 获取底层Hierarchy窗口的位置
            EditorWindow hierarchyWindow = GetHierarchyWindow();
            if (hierarchyWindow == null) return;

            // 使这个覆盖窗口的位置和尺寸与Hierarchy窗口完全一致
            this.position = hierarchyWindow.position;

            // 开始在整个窗口区域进行绘制
            // 例如：在右下角绘制一个按钮
            GUILayout.BeginArea(new Rect(position.width - 100, position.height - 30, 80, 20));
            if (GUILayout.Button("全局按钮"))
            {
                Debug.Log("点击了全局按钮！");
            }
            GUILayout.EndArea();

            // 例如：绘制一个半透明的背景水印
            if (Event.current.type == EventType.Repaint)
            {
                Rect fullWindowRect = new Rect(0, 0, position.width, position.height);
                EditorGUI.DrawRect(fullWindowRect, new Color(0.1f, 0.1f, 0.3f, 0.03f)); // 非常浅的蓝色背景
            }
        }
    }

    // 辅助方法：获取Hierarchy窗口实例[3](@ref)
    static EditorWindow GetHierarchyWindow()
    {
        var windowType = Assembly.Load("UnityEditor.CoreModule").GetType("UnityEditor.SceneHierarchyWindow");
        return (EditorWindow)Resources.FindObjectsOfTypeAll(windowType)?.FirstOrDefault();
    }

  
}