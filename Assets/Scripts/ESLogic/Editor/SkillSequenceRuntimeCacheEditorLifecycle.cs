#if UNITY_EDITOR
using UnityEditor;

namespace ES
{
    [InitializeOnLoad]
    internal static class SkillSequenceRuntimeCacheEditorLifecycle
    {
        static SkillSequenceRuntimeCacheEditorLifecycle()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.projectChanged -= OnGlobalAuthoringChanged;
            EditorApplication.projectChanged += OnGlobalAuthoringChanged;
            Undo.undoRedoPerformed -= OnGlobalAuthoringChanged;
            Undo.undoRedoPerformed += OnGlobalAuthoringChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                SkillSequenceRuntimeCache.ClearAll();
        }

        private static void OnGlobalAuthoringChanged()
        {
            SkillSequenceRuntimeCache.MarkAllDirty();
        }
    }
}
#endif
