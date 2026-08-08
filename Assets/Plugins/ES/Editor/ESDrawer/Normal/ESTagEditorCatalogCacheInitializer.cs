using ES;

/// <summary>
/// Registers the ESTag editor cache lifecycle through the project's AssemblyStream.
/// </summary>
public sealed class ESTagEditorCatalogCacheInitializer : EditorInvoker_Level0
{
    public override void InitInvoke()
    {
        ESTagEditorCatalogCache.InitializeEditorEvents();
    }
}
