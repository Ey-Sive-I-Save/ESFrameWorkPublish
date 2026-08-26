using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// TrackView 的相机输出面板。窗口本身不创建 Camera Request、VCam 或 Session；它只从
    /// 当前 EditorSequencePlayer 发现 CameraTrackEditorSampler，并渲染该 Sampler 已拥有的
    /// 独立 Preview View。因此关闭面板不会影响轨道播放，停止轨道才会销毁全部预览资源。
    /// </summary>
    [ESWindowSleepContract(ESWindowSleepMode.Full, ESWindowSurfaceKind.Preview)]
    public sealed class ESCameraTrackPreviewWindow : ESSinglePageIMGUIWindow<ESCameraTrackPreviewWindow>
    {
        private readonly List<CameraTrackEditorSampler> samplers = new List<CameraTrackEditorSampler>(4);
        private const double SamplerRefreshIntervalSeconds = 0.1d;
        private string[] samplerNames = new string[0];
        private int selectedIndex;
        private double nextRepaintTime;
        private double nextSamplerRefreshTime;

        [MenuItem("【ES】/内容制作/相机/打开轨道相机预览", false, 141)]
        private static void OpenFromMenu()
        {
            ESTrackViewWindow owner = ESTrackViewWindow.window;
            if (owner == null)
            {
                ESTrackViewWindow.OpenWindow();
                owner = ESTrackViewWindow.window;
            }

            if (owner == null)
            {
                Debug.LogWarning("[轨道相机预览] 无法打开：轨道编辑器窗口未能建立。");
                return;
            }

            Open(owner);
        }

        public static void Open(ESTrackViewWindow owner)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));

            ESCameraTrackPreviewWindow window = GetWindow<ESCameraTrackPreviewWindow>();
            window.ESWindow_SetSleepOwnerOverride(owner);
            ESWindowFoundation.SetSleepOwner(
                window,
                owner,
                ESWindowSleepLinkMode.FollowOwner);
            window.titleContent = new GUIContent("轨道相机预览");
            window.minSize = new Vector2(360f, 220f);
            window.maxSize = new Vector2(1400f, 1000f);
            window.Show();
            window.ForceMenuTreeRebuild();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("轨道相机预览", "查看当前 TrackView 相机轨道拥有的独立预览输出");
        }
        public override string ESWindow_PresentationShortTitle => "相机";

        protected override string ESWindow_Subtitle => "TrackView 相机输出";
        protected override ESWindowSleepLinkMode ESWindow_SleepLinkMode
            => ESWindowSleepLinkMode.FollowOwner;
        protected override EditorWindow ESWindow_SleepOwner
            => ESTrackViewWindow.window;
        protected override string ESWindow_SleepOwnerKey => ESTrackViewWindow.SleepOwnerKey;
        protected override Vector2 ESWindow_MinSize => new Vector2(360f, 220f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(760f, 520f);
        protected override string ESWindow_PageStableId => "camera.track-preview";
        protected override string ESWindow_PageTitle => "相机预览";
        protected override string ESWindow_PageKeywords => "TrackView 相机 轨道 预览";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "camera.open-track-view",
                    "轨道编辑器",
                    "打开 TrackView 轨道编辑器。",
                    _ => ESTrackViewWindow.OpenWindow())
                .WithUnityIcon("TimelineAsset Icon")
                .WithPriority(100));
        }

        protected override void ESWindow_OnHostEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        protected override void ESWindow_OnHostDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            samplers.Clear();
            nextRepaintTime = 0d;
            nextSamplerRefreshTime = 0d;
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            CollectSamplers();
            if (samplers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前 TrackView 预览没有活动的相机轨道。打开轨道编辑器、选择包含“相机轨道”的技能序列，然后点击预览。",
                    MessageType.Info);
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, samplers.Count - 1);
            if (samplers.Count > 1)
            {
                if (samplerNames.Length != samplers.Count)
                    samplerNames = new string[samplers.Count];
                for (int i = 0; i < samplers.Count; i++)
                    samplerNames[i] = samplers[i].Track != null ? samplers[i].Track.DisplayName : "相机轨道";
                selectedIndex = EditorGUILayout.Popup("相机轨道", selectedIndex, samplerNames);
            }

            ESCameraTrackPreviewSession session = samplers[selectedIndex].Session;
            if (session == null || !session.IsReady)
            {
                EditorGUILayout.HelpBox("相机轨道预览正在重建或已停止。", MessageType.Warning);
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(16f, Mathf.Max(160f, position.height - 45f), GUILayout.ExpandWidth(true));
            if (!session.RenderGUI(rect, ESEditorPreviewRenderOptions.Balanced))
                EditorGUI.HelpBox(rect, "相机预览渲染失败。请检查 DefinitionKey、RigCatalog 与预览目标 Mapping。", MessageType.Warning);
        }

        private void OnEditorUpdate()
        {
            bool hasActiveSequence = EditorTimelinePlayer.Instance.ActiveSequence != null;
            if (!hasFocus && !hasActiveSequence)
                return;

            double now = EditorApplication.timeSinceStartup;
            double interval = hasFocus ? 1d / 30d : 0.2d;
            if (now < nextRepaintTime)
                return;
            nextRepaintTime = now + interval;
            Repaint();
        }

        private void CollectSamplers()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextSamplerRefreshTime)
                return;
            nextSamplerRefreshTime = now + SamplerRefreshIntervalSeconds;
            samplers.Clear();
            ITrackSequence sequence = ESTrackViewWindow.Sequence;
            EditorSequencePlayer player = EditorTimelinePlayer.Instance.ActiveSequence;
            if (sequence == null || player == null || sequence.Tracks == null)
                return;

            foreach (ITrackItem track in sequence.Tracks)
            {
                if (track == null || !player.TryGetTrackEditorSampler(track, out TrackEditorSampler sampler))
                    continue;

                if (sampler is CameraTrackEditorSampler cameraSampler && cameraSampler.Session != null)
                    samplers.Add(cameraSampler);
            }
        }
    }
}
