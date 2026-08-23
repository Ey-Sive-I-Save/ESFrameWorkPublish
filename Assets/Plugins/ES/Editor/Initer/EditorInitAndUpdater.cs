using ES;

public class EditorInitAndUpdater : EditorInvoker_Level0
{
    public override void InitInvoke()
    {
        // This Level0 entry intentionally has no permanent editor update work.
        // Focus state belongs to the window workflow that consumes it.
    }
}

public class EditorInit1 : EditorInvoker_Level1
{
    public override void InitInvoke()
    {
        // 保留阶段占位，不能注册空的全局 update 回调。
    }
}

public class EditorInit2 : EditorInvoker_Level2
{
    public override void InitInvoke()
    {
        // 保留阶段占位，不能注册空的全局 update 回调。
    }
}
