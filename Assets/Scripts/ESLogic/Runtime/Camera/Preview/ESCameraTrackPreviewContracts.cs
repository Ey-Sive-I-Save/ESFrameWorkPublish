using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// 相机轨道预览的 Runtime 纯契约。它只描述轨道、目标与稳定 Definition 引用，
    /// 不引用 UnityEditor、TrackView 窗口、Cinemachine Editor 或任何场景 VCam。
    /// Editor/Camera 中的 Bootstrap 在域加载时注册唯一 Factory。
    /// </summary>
    public interface ICameraTrackPreviewFactory
    {
        List<IEditorTimeSampler> CreateSamplers(in ESCameraTrackPreviewRequest request);
    }

    /// <summary>相机轨道片段的编辑器预览目标语义，与运行时 Clip 保持一致。</summary>
    public enum ESCameraTrackPreviewTargetSource : byte
    {
        SkillUser = 0,
        MainTarget = 1,
    }

    /// <summary>单个相机片段的不可变预览描述。</summary>
    public readonly struct ESCameraTrackPreviewClip
    {
        public readonly ITrackClip sourceClip;
        public readonly ESCameraDefinitionReference definition;
        public readonly string viewKey;
        public readonly int priority;
        public readonly ESCameraTrackPreviewTargetSource targetSource;

        public ESCameraTrackPreviewClip(
            ITrackClip sourceClip,
            ESCameraDefinitionReference definition,
            string viewKey,
            int priority,
            ESCameraTrackPreviewTargetSource targetSource)
        {
            this.sourceClip = sourceClip;
            this.definition = definition;
            this.viewKey = viewKey;
            this.priority = priority;
            this.targetSource = targetSource;
        }

        public bool IsValid => sourceClip != null && definition.IsConfigured;
    }

    /// <summary>
    /// Factory 的输入。EditorTarget 的所有权显式传递：Factory 创建的 Track Sampler
    /// 必须在预览停止时归还 ownsEditorTarget 对应的池化 Target。
    /// </summary>
    public readonly struct ESCameraTrackPreviewRequest
    {
        public readonly ITrackSequence sequence;
        public readonly ITrackItem track;
        public readonly ESRuntimeTargetPack editorTarget;
        public readonly bool ownsEditorTarget;
        public readonly IReadOnlyList<ESCameraTrackPreviewClip> clips;

        public ESCameraTrackPreviewRequest(
            ITrackSequence sequence,
            ITrackItem track,
            ESRuntimeTargetPack editorTarget,
            bool ownsEditorTarget,
            IReadOnlyList<ESCameraTrackPreviewClip> clips)
        {
            this.sequence = sequence;
            this.track = track;
            this.editorTarget = editorTarget;
            this.ownsEditorTarget = ownsEditorTarget;
            this.clips = clips;
        }
    }

    /// <summary>
    /// Runtime 与 Editor 之间唯一的预览注册点。它不是相机运行时全局服务；正常游戏
    /// 路径绝不会读取这里。Install/Clear 只允许 Editor Bootstrap 在域加载/卸载时调用。
    /// </summary>
    public static class ESCameraTrackPreviewFactoryRegistry
    {
        private static ICameraTrackPreviewFactory factory;

        public static ICameraTrackPreviewFactory Factory => factory;

#if UNITY_EDITOR
        public static void Install(ICameraTrackPreviewFactory newFactory)
        {
            factory = newFactory;
        }

        public static void Clear(ICameraTrackPreviewFactory expectedFactory)
        {
            if (ReferenceEquals(factory, expectedFactory))
                factory = null;
        }
#endif
    }
}
