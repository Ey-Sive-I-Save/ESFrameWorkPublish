#if UNITY_EDITOR
using UnityEditor;

namespace ES
{
    /// <summary>通过 ES AssemblyStream 注册编辑器缓存生命周期，避免普通业务挂接 Unity 全局初始化入口。</summary>
    internal sealed class SkillSequenceRuntimeCacheEditorLifecycle : EditorInvoker_Level1
    {
        public override void InitInvoke()
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
