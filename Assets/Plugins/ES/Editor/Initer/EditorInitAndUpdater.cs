using ES;
using UnityEditor;
using UnityEngine;

public class EditorInitAndUpdater : EditorInvoker_Level0
{
    public static Event etc;
    public static string FocusedWindowTitle { get; private set; }

    public override void InitInvoke()
    {
        EditorApplication.update += Update;
    }

    private void Update()
    {
        etc = Event.current;
        WindowFocus();
    }

    private static void WindowFocus()
    {
        var windowF = EditorWindow.focusedWindow;
        if (windowF == null) return;
        FocusedWindowTitle = windowF.titleContent.text;
    }
}

public class EditorInit1 : EditorInvoker_Level1
{
    public override void InitInvoke()
    {
        EditorApplication.update += Update;
    }

    private void Update()
    {
    }
}

public class EditorInit2 : EditorInvoker_Level2
{
    public override void InitInvoke()
    {
        EditorApplication.update += Update;
    }

    private void Update()
    {
    }
}
