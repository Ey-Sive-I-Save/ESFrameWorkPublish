using System;
using System.Collections.Generic;
using System.Reflection;
using ES.EditorInternal;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>Editor-only trimmed-Cue preview using Unity's native preview audio service.</summary>
    public sealed class ESAudioCueTrimPreviewWindow : ESSinglePageIMGUIWindow<ESAudioCueTrimPreviewWindow>
    {
        private const float MinimumWidth = 420f;
        private const float MinimumHeight = 340f;

        private static readonly Type AudioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        private static readonly BindingFlags AudioUtilFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static MethodInfo playPreviewMethod;
        private static MethodInfo stopPreviewMethod;
        private static GUIStyle rightMiniLabel;

        private ESAudioCueInfo cue;
        private int variantIndex;
        private ESAudioCueInfo resolvedCue;
        private int resolvedVariantIndex = -1;
        private int resolvedPreviewSignature;
        private AudioClip resolvedClip;
        private int resolvedStartSample;
        private int resolvedEndSample;
        private string resolveError;
        private AudioClip playingClip;
        private int playingStartSample;
        private int playingEndSample;
        private bool repeatPreview;
        private bool previewing;
        private double previewStartedAt;
        private double nextPreviewStopAt;
        private string status;

        [MenuItem("【ES】/内容制作/音频/Cue 播放窗口预览")]
        private static new void OpenWindow()
        {
            var window = GetWindow<ESAudioCueTrimPreviewWindow>("音频 Cue 预览");
            window.TryUseSelectedCue();
            window.Show();
        }

        [MenuItem("Assets/【ES】/内容制作/音频/预览 Cue 播放窗口", true)]
        private static bool ValidatePreviewSelectedCue()
        {
            return Selection.activeObject is ESAudioCueInfo;
        }

        [MenuItem("Assets/【ES】/内容制作/音频/预览 Cue 播放窗口")]
        private static void PreviewSelectedCue()
        {
            OpenWindow();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("音频 Cue 预览", "试听 Cue 变体及其裁剪播放窗口");
        }
        public override string ESWindow_PresentationShortTitle => "音频";

        protected override string ESWindow_Subtitle => "播放窗口与变体试听";
        protected override Vector2 ESWindow_MinSize => new Vector2(MinimumWidth, MinimumHeight);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(760f, 620f);
        protected override string ESWindow_PageStableId => "audio.cue-trim-preview";
        protected override string ESWindow_PageTitle => "Cue 播放预览";
        protected override string ESWindow_PageKeywords => "音频 Audio Cue 变体 裁剪 播放 预览";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "audio.use-selection",
                    "使用选中 Cue",
                    "使用 Project 当前选中的 ESAudioCueInfo。",
                    context =>
                    {
                        TryUseSelectedCue();
                        context.RefreshPageActions();
                        context.SetStatus(cue != null ? "已使用当前选中 Cue" : "当前选择不是音频 Cue",
                            cue != null ? ESMenuTreePageStatus.Info : ESMenuTreePageStatus.Warning);
                    })
                .WithUnityIcon("Linked")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "audio.play",
                    "播放",
                    "试听当前 Cue 变体的有效播放窗口。",
                    context =>
                    {
                        StartPreview();
                        context.RefreshPageActions();
                        context.SetStatus(previewing ? "正在预览音频 Cue" : status,
                            previewing ? ESMenuTreePageStatus.Info : ESMenuTreePageStatus.Warning);
                    })
                .WhenVisible(() => cue != null && !previewing)
                .WithUnityIcon("PlayButton")
                .WithPriority(90));
            actions.Add(new ESMenuTreePageAction(
                    "audio.stop",
                    "停止",
                    "停止当前音频预览。",
                    context =>
                    {
                        StopPreview();
                        context.RefreshPageActions();
                        context.SetStatus("音频预览已停止");
                    })
                .WhenVisible(() => previewing)
                .WithUnityIcon("PauseButton")
                .WithPriority(90));
        }

        protected override void ESWindow_OnHostEnable()
        {
            minSize = new Vector2(MinimumWidth, MinimumHeight);
            titleContent = new GUIContent("音频 Cue 预览");
            TryUseSelectedCue();
        }

        protected override void ESWindow_OnHostDisable()
        {
            StopPreview(repaint: false);
        }

        private void OnSelectionChange()
        {
            if (cue == null)
                TryUseSelectedCue();
            ESWindow_CurrentPageContext?.RefreshPageActions();
            Repaint();
        }

        private void OnProjectChange()
        {
            StopPreview();
            InvalidateResolvedPreview();
            ESWindow_CurrentPageContext?.RefreshPageActions();
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            DrawHeader();
            GUILayout.Space(8f);
            DrawCueSelector();

            if (cue == null)
            {
                DrawEmptyState();
                return;
            }

            int variantCount = cue.variants != null ? cue.variants.Count : 0;
            if (variantCount <= 0)
            {
                EditorGUILayout.HelpBox("此 Cue 没有可预览的变体。", MessageType.Warning);
                return;
            }

            variantIndex = Mathf.Clamp(variantIndex, 0, variantCount - 1);
            DrawCueSummary();
            DrawVariantSelector(variantCount);

            bool previewReady = TryGetPreviewData(out AudioClip clip, out int startSample, out int endSample, out string error);
            if (previewReady)
            {
                DrawClipSummary(clip, startSample, endSample);
                DrawTimeline(clip, startSample, endSample);
            }
            else
                EditorGUILayout.HelpBox(error, MessageType.Error);

            DrawPlaybackControls(previewReady);
            if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, previewing ? MessageType.Info : MessageType.None);
        }

        private void DrawHeader()
        {
            Rect rect = GUILayoutUtility.GetRect(0f, 58f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.16f, 1f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), new Color(0.26f, 0.66f, 0.91f, 1f));

            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 150f, 20f), "音频 Cue 预览", EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 30f, rect.width - 150f, 17f), "播放窗口与变体试听", EditorStyles.miniLabel);

            string state = previewing ? "预览中" : cue == null ? "未选择" : "就绪";
            Color stateColor = previewing
                ? new Color(0.22f, 0.66f, 0.40f, 1f)
                : cue == null ? new Color(0.42f, 0.42f, 0.42f, 1f) : new Color(0.25f, 0.53f, 0.76f, 1f);
            DrawBadge(new Rect(rect.xMax - 76f, rect.y + 17f, 62f, 22f), state, stateColor);
        }

        private void DrawCueSelector()
        {
            EditorGUILayout.LabelField("预览目标", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            ESAudioCueInfo selectedCue = (ESAudioCueInfo)EditorGUILayout.ObjectField("Cue", cue, typeof(ESAudioCueInfo), false);
            if (EditorGUI.EndChangeCheck())
                SetCue(selectedCue);
        }

        private static void DrawEmptyState()
        {
            GUILayout.Space(28f);
            EditorGUILayout.HelpBox("从 Project 中选择一个 Audio Cue，或在此处指定 Cue。", MessageType.Info);
        }

        private void DrawCueSummary()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Cue 设置", EditorStyles.boldLabel);
            DrawInfoRow("Cue Key", cue.key == null
                ? "未配置"
                : ESConfigKeyMatch.Describe(cue.key.EnumKeyInt, cue.key.StringKey));
            DrawInfoRow("分类", cue.category + "  ·  " + (cue.spatialMode == ESAudioSpatialMode.ThreeD ? "3D" : "2D"));
            DrawInfoRow("播放模式", cue.loop ? "按 Cue 循环" : "单次播放");
        }

        private void DrawVariantSelector(int variantCount)
        {
            string[] labels = new string[variantCount];
            for (int i = 0; i < variantCount; i++)
            {
                ESAudioCueVariant variant = cue.variants[i];
                labels[i] = "变体 " + (i + 1) + "  ·  " + DescribeClipKey(variant?.clipKey);
            }

            EditorGUI.BeginChangeCheck();
            int nextVariantIndex = EditorGUILayout.Popup("Clip 变体", variantIndex, labels);
            if (EditorGUI.EndChangeCheck())
            {
                variantIndex = nextVariantIndex;
                StopPreview();
                InvalidateResolvedPreview();
            }
        }

        private static void DrawInfoRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.miniLabel, GUILayout.Width(76f));
                GUILayout.Label(value, EditorStyles.label);
            }
        }

        private static string DescribeClipKey(ESAssetReferAudioClipConfigKey key)
        {
            return key == null || !key.IsConfigured
                ? "未配置"
                : ESConfigKeyMatch.Describe(key.EnumKeyInt, key.StringKey);
        }

        private void DrawClipSummary(AudioClip clip, int startSample, int endSample)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("有效播放区间", EditorStyles.boldLabel);
            DrawInfoRow("AudioClip", clip.name);
            DrawInfoRow("播放窗口", DescribePlaybackWindow(cue));
            DrawInfoRow("实际时长", FormatSeconds((endSample - startSample) / (double)clip.frequency));
        }

        private void DrawTimeline(AudioClip clip, int startSample, int endSample)
        {
            double clipDuration = clip.samples / (double)clip.frequency;
            double startSeconds = startSample / (double)clip.frequency;
            double endSeconds = endSample / (double)clip.frequency;
            float startRatio = clipDuration > 0d ? Mathf.Clamp01((float)(startSeconds / clipDuration)) : 0f;
            float endRatio = clipDuration > 0d ? Mathf.Clamp01((float)(endSeconds / clipDuration)) : 1f;

            Rect rect = GUILayoutUtility.GetRect(0f, 68f, GUILayout.ExpandWidth(true));
            Rect titleRect = new Rect(rect.x, rect.y, rect.width * 0.5f, 17f);
            Rect rangeRect = new Rect(rect.x + rect.width * 0.5f, rect.y, rect.width * 0.5f, 17f);
            GUI.Label(titleRect, "Clip  " + FormatSeconds(clipDuration), EditorStyles.miniLabel);
            GUI.Label(rangeRect, FormatSeconds(startSeconds) + " - " + FormatSeconds(endSeconds), RightMiniLabel);

            Rect trackRect = new Rect(rect.x + 8f, rect.y + 23f, Mathf.Max(1f, rect.width - 16f), 22f);
            EditorGUI.DrawRect(trackRect, new Color(0.09f, 0.10f, 0.12f, 1f));
            float rangeX = trackRect.x + trackRect.width * startRatio;
            float rangeWidth = Mathf.Max(2f, trackRect.width * (endRatio - startRatio));
            Rect range = new Rect(rangeX, trackRect.y, rangeWidth, trackRect.height);
            EditorGUI.DrawRect(range, new Color(0.20f, 0.52f, 0.78f, 0.78f));
            EditorGUI.DrawRect(new Rect(range.x, trackRect.y - 2f, 2f, trackRect.height + 4f), new Color(0.57f, 0.86f, 1f, 1f));
            EditorGUI.DrawRect(new Rect(range.xMax - 2f, trackRect.y - 2f, 2f, trackRect.height + 4f), new Color(0.57f, 0.86f, 1f, 1f));

            if (previewing && playingClip == clip)
            {
                float progress = GetPreviewProgress();
                float cursorX = Mathf.Lerp(range.x, range.xMax, progress);
                EditorGUI.DrawRect(new Rect(cursorX - 1f, trackRect.y - 4f, 2f, trackRect.height + 8f), Color.white);
            }

            GUI.Label(new Rect(trackRect.x, trackRect.yMax + 4f, trackRect.width * 0.5f, 16f), "0 s", EditorStyles.miniLabel);
            GUI.Label(new Rect(trackRect.x + trackRect.width * 0.5f, trackRect.yMax + 4f, trackRect.width * 0.5f, 16f), FormatSeconds(clipDuration), RightMiniLabel);
        }

        private void DrawPlaybackControls(bool previewReady)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(!previewReady);
                if (GUILayout.Button(CreateIconContent("PlayButton", "播放", "预览当前播放窗口"), EditorStyles.toolbarButton, GUILayout.Width(38f), GUILayout.Height(28f)))
                    StartPreview();
                EditorGUI.EndDisabledGroup();

                GUILayout.Label(cue != null && cue.loop ? "循环" : "单次", EditorStyles.miniLabel, GUILayout.Width(38f));
                GUILayout.FlexibleSpace();
                EditorGUI.BeginDisabledGroup(!previewing);
                if (GUILayout.Button(new GUIContent("停止", "停止预览"), EditorStyles.toolbarButton, GUILayout.Width(52f), GUILayout.Height(28f)))
                    StopPreview();
                EditorGUI.EndDisabledGroup();
            }
        }

        private void StartPreview()
        {
            if (!TryGetPreviewData(out AudioClip clip, out int startSample, out int endSample, out string error))
            {
                status = error;
                return;
            }

            StopPreview();
            if (!TryPlayPreviewClip(clip, startSample))
            {
                status = "当前 Unity 版本不支持从指定 Sample 预览 Audio Clip。";
                return;
            }

            playingClip = clip;
            playingStartSample = startSample;
            playingEndSample = endSample;
            repeatPreview = cue.loop;
            previewing = true;
            previewStartedAt = EditorApplication.timeSinceStartup;
            nextPreviewStopAt = previewStartedAt + (endSample - startSample) / (double)clip.frequency;
            status = "正在预览";
            EditorApplication.update += UpdatePreview;
            ESWindow_CurrentPageContext?.RefreshPageActions();
        }

        private void UpdatePreview()
        {
            if (!previewing)
                return;

            double now = EditorApplication.timeSinceStartup;
            Repaint();
            if (now < nextPreviewStopAt)
                return;

            StopNativePreview();
            if (!repeatPreview || playingClip == null || !TryPlayPreviewClip(playingClip, playingStartSample))
            {
                StopPreview();
                return;
            }

            previewStartedAt = now;
            nextPreviewStopAt = now + (playingEndSample - playingStartSample) / (double)playingClip.frequency;
        }

        private void StopPreview(bool repaint = true)
        {
            EditorApplication.update -= UpdatePreview;
            if (previewing)
                StopNativePreview();
            playingClip = null;
            previewing = false;
            repeatPreview = false;
            previewStartedAt = 0d;
            nextPreviewStopAt = 0d;
            if (string.IsNullOrEmpty(status) || status == "正在预览")
                status = string.Empty;
            if (repaint)
            {
                ESWindow_CurrentPageContext?.RefreshPageActions();
                Repaint();
            }
        }

        private void SetCue(ESAudioCueInfo selectedCue)
        {
            if (cue == selectedCue)
                return;

            StopPreview();
            cue = selectedCue;
            variantIndex = 0;
            status = string.Empty;
            InvalidateResolvedPreview();
        }

        private void InvalidateResolvedPreview()
        {
            resolvedCue = null;
            resolvedVariantIndex = -1;
            resolvedPreviewSignature = 0;
            resolvedClip = null;
            resolvedStartSample = 0;
            resolvedEndSample = 0;
            resolveError = null;
        }

        private bool TryGetPreviewData(out AudioClip clip, out int startSample, out int endSample, out string error)
        {
            int signature = GetPreviewSignature();
            if (resolvedCue != cue || resolvedVariantIndex != variantIndex || resolvedPreviewSignature != signature)
            {
                if (previewing)
                    StopPreview();
                ResolvePreviewData(signature);
            }

            clip = resolvedClip;
            startSample = resolvedStartSample;
            endSample = resolvedEndSample;
            error = resolveError;
            return clip != null && string.IsNullOrEmpty(error);
        }

        private void ResolvePreviewData(int signature)
        {
            resolvedCue = cue;
            resolvedVariantIndex = variantIndex;
            resolvedPreviewSignature = signature;
            resolvedClip = null;
            resolvedStartSample = 0;
            resolvedEndSample = 0;
            resolveError = null;
            if (!TryResolveClip(out AudioClip clip, out string error))
            {
                resolveError = error;
                return;
            }
            if (!cue.TryResolvePlaybackSampleRange(clip, out int startSample, out int endSample, out error))
            {
                resolveError = error;
                return;
            }

            resolvedClip = clip;
            resolvedStartSample = startSample;
            resolvedEndSample = endSample;
        }

        private int GetPreviewSignature()
        {
            if (cue == null)
                return 0;

            unchecked
            {
                int signature = cue.GetInstanceID();
                signature = signature * 31 + variantIndex;
                signature = signature * 31 + (cue.loop ? 1 : 0);
                signature = signature * 31 + (cue.usePlaybackWindow ? 1 : 0);
                signature = signature * 31 + cue.playbackStartSeconds.GetHashCode();
                signature = signature * 31 + cue.playbackEndSeconds.GetHashCode();
                signature = signature * 31 + (cue.variants?.Count ?? 0);

                if (cue.variants != null && variantIndex >= 0 && variantIndex < cue.variants.Count)
                {
                    ESAssetReferAudioClipConfigKey key = cue.variants[variantIndex]?.clipKey;
                    signature = signature * 31 + (key?.EnumKeyInt ?? 0);
                    signature = signature * 31 + (key?.StringKey?.GetHashCode() ?? 0);
                }
                return signature;
            }
        }

        private bool TryResolveClip(out AudioClip clip, out string error)
        {
            clip = null;
            error = null;
            if (variantIndex < 0 || cue.variants == null || variantIndex >= cue.variants.Count)
            {
                error = "请选择有效的 Audio Cue 变体。";
                return false;
            }

            ESAudioCueVariant variant = cue.variants[variantIndex];
            ESAssetReferAudioClipConfigKey key = variant?.clipKey;
            if (key == null || !key.IsConfigured
                || !ESAssetCatalogKeyResolver.TryResolveAsset(
                    ESAssetReferKind.AudioClip,
                    key.EnumKeyInt,
                    key.StringKey,
                    out UnityEngine.Object asset))
            {
                error = "此变体的 AudioClip ConfigKey 无法在当前资源库中解析。";
                return false;
            }

            clip = asset as AudioClip;
            if (clip != null)
                return true;

            error = "此变体解析出的资源不是 AudioClip。";
            return false;
        }

        private void TryUseSelectedCue()
        {
            if (Selection.activeObject is ESAudioCueInfo selectedCue)
                SetCue(selectedCue);
        }

        private float GetPreviewProgress()
        {
            if (!previewing || playingClip == null || playingEndSample <= playingStartSample)
                return 0f;

            double duration = (playingEndSample - playingStartSample) / (double)playingClip.frequency;
            if (duration <= 0d)
                return 0f;
            return Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - previewStartedAt) / duration));
        }

        private static bool TryPlayPreviewClip(AudioClip clip, int startSample)
        {
            playPreviewMethod ??= AudioUtilType?.GetMethod(
                "PlayPreviewClip",
                AudioUtilFlags,
                null,
                new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null)
                ?? AudioUtilType?.GetMethod(
                    "PlayClip",
                    AudioUtilFlags,
                    null,
                    new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                    null);
            if (playPreviewMethod == null)
                return false;

            playPreviewMethod.Invoke(null, new object[] { clip, startSample, false });
            return true;
        }

        private static void StopNativePreview()
        {
            stopPreviewMethod ??= AudioUtilType?.GetMethod("StopAllPreviewClips", AudioUtilFlags)
                ?? AudioUtilType?.GetMethod("StopAllClips", AudioUtilFlags);
            stopPreviewMethod?.Invoke(null, null);
        }

        private static string DescribePlaybackWindow(ESAudioCueInfo source)
        {
            if (!source.usePlaybackWindow)
                return "完整 Clip";
            string end = source.playbackEndSeconds > 0f
                ? source.playbackEndSeconds.ToString("0.###") + " s"
                : "Clip 末尾";
            return source.playbackStartSeconds.ToString("0.###") + " s - " + end;
        }

        private static string FormatSeconds(double seconds)
        {
            return seconds.ToString(seconds < 10d ? "0.000 s" : "0.00 s");
        }

        private static GUIContent CreateIconContent(string iconName, string fallbackText, string tooltip)
        {
            var content = new GUIContent(EditorGUIUtility.IconContent(iconName)) { tooltip = tooltip };
            if (content.image == null)
                content.text = fallbackText;
            return content;
        }

        private static GUIStyle RightMiniLabel
        {
            get
            {
                if (rightMiniLabel == null)
                    rightMiniLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
                return rightMiniLabel;
            }
        }

        private static void DrawBadge(Rect rect, string label, Color color)
        {
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.22f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), color);
            GUI.Label(rect, label, RightMiniLabel);
        }
    }
}
