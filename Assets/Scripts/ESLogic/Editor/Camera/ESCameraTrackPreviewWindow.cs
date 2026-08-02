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
    public sealed class ESCameraTrackPreviewWindow : EditorWindow
    {
        private readonly List<CameraTrackEditorSampler> samplers = new List<CameraTrackEditorSampler>(4);
        private int selectedIndex;

        [MenuItem("【ES】/内容制作/相机/打开轨道相机预览", false, 141)]
        public static void Open()
        {
            ESCameraTrackPreviewWindow window = GetWindow<ESCameraTrackPreviewWindow>();
            window.titleContent = new GUIContent("轨道相机预览");
            window.minSize = new Vector2(360f, 220f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnGUI()
        {
            CollectSamplers();
            if (samplers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前 TrackView 预览没有活动的相机轨道。打开轨道编辑器、选择包含“相机轨道”的技能序列，然后点击预览。",
                    MessageType.Info);
                if (GUILayout.Button("打开轨道编辑器"))
                    ESTrackViewWindow.OpenWindow();
                return;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, samplers.Count - 1);
            if (samplers.Count > 1)
            {
                string[] names = new string[samplers.Count];
                for (int i = 0; i < samplers.Count; i++)
                    names[i] = samplers[i].Track != null ? samplers[i].Track.DisplayName : "相机轨道";
                selectedIndex = EditorGUILayout.Popup("相机轨道", selectedIndex, names);
            }

            ESCameraTrackPreviewSession session = samplers[selectedIndex].Session;
            if (session == null || !session.IsReady)
            {
                EditorGUILayout.HelpBox("相机轨道预览正在重建或已停止。", MessageType.Warning);
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(16f, Mathf.Max(160f, position.height - 45f), GUILayout.ExpandWidth(true));
            if (!session.RenderGUI(rect, ESEditorPreviewRenderOptions.Balanced))
                EditorGUI.HelpBox(rect, "相机预览渲染失败。请检查 ProfileKey、RigCatalog 与预览目标 Mapping。", MessageType.Warning);
        }

        private void OnEditorUpdate()
        {
            if (hasFocus || EditorTimelinePlayer.Instance.ActiveSequence != null)
                Repaint();
        }

        private void CollectSamplers()
        {
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
