using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ES
{
    public enum ESEditorFeedbackSoundKind
    {
        Click,
        Success,
        Warning,
        Error,
        Open,
        Close,
        Navigate,
        Copy,
        Locate,
        Refresh,
        Scene,
        Confirm,
        Cancel,
        Type,
        AddComponent,
        RemoveComponent,
        PrefabOpen,
        PrefabDirty
    }

    /// <summary>
    /// 方案目录规范：EditorFeedback/Default/ 是标准兜底；
    /// 其他子目录都是可切换方案。自定义方案缺少某类 WAV 时自动回退到 Default。
    /// 可选 scheme.json：displayName 控制显示名，enabledKinds 控制启用的音效类型。
    /// </summary>
    public static class ESEditorFeedbackSound
    {
        public const string ClipFolder = "ES/EditorFeedback/";

        private const string EnabledKey = "ES.EditorFeedbackSound.Enabled";
        private const string SchemeKey = "ES.EditorFeedbackSound.Scheme";
        private const string DefaultScheme = "Default";
        private const string FeedbackMenuRoot =
            MenuItemPathDefine.PROJECT_CONFIGURATION_PATH + "编辑器体验/反馈音效/";
        private const string EnabledMenuPath = FeedbackMenuRoot + "启用全局编辑器音效";
        private const string EnhancedMenuPath = FeedbackMenuRoot + "启用增强反馈";
        private const string PlayModeMenuPath = FeedbackMenuRoot + "启用 PlayMode 进出反馈";
        private const string ManualRefreshMenuPath = FeedbackMenuRoot + "手动保存并刷新项目";
        private const string SchemeMenuPath = FeedbackMenuRoot + "切换音效方案...";
        private const string PreviewMenuPath = FeedbackMenuRoot + "一键试听全部音效";
        private const string UnmuteEditorMenuPath = FeedbackMenuRoot + "取消 Unity 编辑器静音";
        private const string EnhancedKey = "ES.EditorFeedbackSound.Enhanced";
        private const string PlayModeKey = "ES.EditorFeedbackSound.PlayMode";
        private const string UndoRedoVolumeKey = "ES.EditorFeedbackSound.UndoRedoVolume";
        private const string ManualSaveVolumeKey = "ES.EditorFeedbackSound.ManualSaveVolume";
        private const string CompilationSuccessVolumeKey =
            "ES.EditorFeedbackSound.CompilationSuccessVolume";
        private const string KindVolumeKeyPrefix = "ES.EditorFeedbackSound.Volume.";
        private const int MaxWavFileBytes = 4 * 1024 * 1024;
        private const float MaxWavDurationSeconds = 2f;
        private const double SchemePreviewIntervalSeconds = 0.28d;

        private static readonly ESEditorFeedbackSoundKind[] SchemePreviewKinds =
        {
            ESEditorFeedbackSoundKind.Click,
            ESEditorFeedbackSoundKind.Success,
            ESEditorFeedbackSoundKind.Warning,
            ESEditorFeedbackSoundKind.Error,
            ESEditorFeedbackSoundKind.Open,
            ESEditorFeedbackSoundKind.Close,
            ESEditorFeedbackSoundKind.Navigate,
            ESEditorFeedbackSoundKind.Copy,
            ESEditorFeedbackSoundKind.Locate,
            ESEditorFeedbackSoundKind.Refresh,
            ESEditorFeedbackSoundKind.Scene,
            ESEditorFeedbackSoundKind.Confirm,
            ESEditorFeedbackSoundKind.Cancel,
            ESEditorFeedbackSoundKind.Type,
            ESEditorFeedbackSoundKind.AddComponent,
            ESEditorFeedbackSoundKind.RemoveComponent,
            ESEditorFeedbackSoundKind.PrefabOpen,
            ESEditorFeedbackSoundKind.PrefabDirty
        };

        private static MethodInfo[] playClipMethods;
        private static double selectionSoundSuppressedUntil;
        private static string cachedScheme;
        private static bool schemeCacheValid;
        private static bool playbackMethodDebugLogged;
        private static bool playbackInvocationDebugLogged;
        private static bool editorAudioSourceDebugLogged;
        private static bool nativePlaybackDebugLogged;
        private static MethodInfo stopAllPreviewClipsMethod;
        private static MethodInfo isPreviewClipPlayingMethod;
        private static GameObject previewHost;
        private static AudioSource previewSource;
        private static string previewSchemeId;
        private static int previewKindIndex;
        private static double nextSchemePreviewAt;
        private static bool schemePreviewScheduled;
        internal static event Action SchemePreviewStateChanged;
        private static readonly Dictionary<string, AudioClip> ExternalClipCache =
            new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> WarnedDiagnostics =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, SchemeConfig> SchemeConfigCache =
            new Dictionary<string, SchemeConfig>(StringComparer.Ordinal);
        private static readonly Dictionary<ESEditorFeedbackSoundKind, double>
            LastKindPlaybackAt =
                new Dictionary<ESEditorFeedbackSoundKind, double>();
        private static readonly Dictionary<ESEditorFeedbackSoundKind, float>
            KindVolumeCache =
                new Dictionary<ESEditorFeedbackSoundKind, float>();

        private static string SchemeRoot
        {
            get
            {
                string dataPath = Directory.GetParent(Application.dataPath)?.FullName
                    ?? Application.dataPath;
                return Path.Combine(
                    dataPath,
                    ClipFolder.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        public static string GetSchemeRootPath()
        {
            return SchemeRoot;
        }

        public static void ClearClipCache()
        {
            foreach (KeyValuePair<string, AudioClip> pair in ExternalClipCache)
            {
                if (pair.Value != null)
                {
                    UnityEngine.Object.DestroyImmediate(pair.Value);
                }
            }

            ExternalClipCache.Clear();
            SchemeConfigCache.Clear();
            WarnedDiagnostics.Clear();
            LastKindPlaybackAt.Clear();
            KindVolumeCache.Clear();
            schemeCacheValid = false;
            StopSchemePreview();
            DestroyPreviewHost();
            StopNativePlayback();
            ESEditorFeedbackSoundHook.ResetState();
        }

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set
            {
                bool changed = Enabled != value;
                EditorPrefs.SetBool(EnabledKey, value);
                if (changed)
                {
                    ESEditorFeedbackSoundHook.ResetState();
                }
            }
        }

        public static bool EnhancedFeedbackEnabled
        {
            get => EditorPrefs.GetBool(EnhancedKey, false);
            set
            {
                bool changed = EnhancedFeedbackEnabled != value;
                EditorPrefs.SetBool(EnhancedKey, value);
                if (changed)
                {
                    ESEditorFeedbackSoundHook.ResetState();
                }
            }
        }

        public static bool PlayModeFeedbackEnabled
        {
            get => EditorPrefs.GetBool(PlayModeKey, false);
            set => EditorPrefs.SetBool(PlayModeKey, value);
        }

        public static float UndoRedoVolume
        {
            get => Mathf.Clamp01(EditorPrefs.GetFloat(UndoRedoVolumeKey, 0.18f));
            set => EditorPrefs.SetFloat(UndoRedoVolumeKey, Mathf.Clamp01(value));
        }

        public static float ManualSaveVolume
        {
            get => Mathf.Clamp01(EditorPrefs.GetFloat(ManualSaveVolumeKey, 0.12f));
            set => EditorPrefs.SetFloat(ManualSaveVolumeKey, Mathf.Clamp01(value));
        }

        public static float CompilationSuccessVolume
        {
            get => Mathf.Clamp01(
                EditorPrefs.GetFloat(CompilationSuccessVolumeKey, 0.30f));
            set => EditorPrefs.SetFloat(
                CompilationSuccessVolumeKey,
                Mathf.Clamp01(value));
        }

        public static float GetKindVolume(ESEditorFeedbackSoundKind kind)
        {
            if (KindVolumeCache.TryGetValue(kind, out float cached))
            {
                return cached;
            }

            float fallback = GetDefaultKindVolume(kind);
            float value = EditorPrefs.GetFloat(
                KindVolumeKeyPrefix + GetKindToken(kind),
                fallback);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }

            value = Mathf.Clamp01(value);
            KindVolumeCache[kind] = value;
            return value;
        }

        public static void SetKindVolume(
            ESEditorFeedbackSoundKind kind,
            float volume)
        {
            volume = Mathf.Clamp01(volume);
            EditorPrefs.SetFloat(
                KindVolumeKeyPrefix + GetKindToken(kind),
                volume);
            KindVolumeCache[kind] = volume;
        }

        public static void ResetKindVolumes()
        {
            for (int i = 0; i < SchemePreviewKinds.Length; i++)
            {
                EditorPrefs.DeleteKey(
                    KindVolumeKeyPrefix + GetKindToken(SchemePreviewKinds[i]));
            }

            EditorPrefs.DeleteKey(UndoRedoVolumeKey);
            EditorPrefs.DeleteKey(ManualSaveVolumeKey);
            EditorPrefs.DeleteKey(CompilationSuccessVolumeKey);
            KindVolumeCache.Clear();
        }

        public static string Scheme
        {
            get
            {
                if (schemeCacheValid)
                {
                    return cachedScheme;
                }

                string saved = EditorPrefs.GetString(SchemeKey, DefaultScheme);
                cachedScheme = IsValidSchemeName(saved) ? saved : DefaultScheme;
                schemeCacheValid = true;
                return cachedScheme;
            }
            set
            {
                string normalized = IsValidSchemeName(value) ? value : DefaultScheme;
                EditorPrefs.SetString(SchemeKey, normalized);
                cachedScheme = normalized;
                schemeCacheValid = true;
            }
        }

        [MenuItem(EnabledMenuPath, false, 110)]
        private static void ToggleEnabled()
        {
            Enabled = !Enabled;
            if (Enabled)
            {
                Play(ESEditorFeedbackSoundKind.Confirm);
            }
        }

        [MenuItem(EnabledMenuPath, true)]
        private static bool ValidateToggleEnabled()
        {
            Menu.SetChecked(EnabledMenuPath, Enabled);
            return true;
        }

        [MenuItem(EnhancedMenuPath, false, 115)]
        private static void ToggleEnhancedFeedback()
        {
            EnhancedFeedbackEnabled = !EnhancedFeedbackEnabled;
            if (EnhancedFeedbackEnabled && Enabled)
            {
                Play(ESEditorFeedbackSoundKind.Confirm);
            }
        }

        [MenuItem(EnhancedMenuPath, true)]
        private static bool ValidateToggleEnhancedFeedback()
        {
            Menu.SetChecked(EnhancedMenuPath, EnhancedFeedbackEnabled);
            return true;
        }

        [MenuItem(PlayModeMenuPath, false, 116)]
        private static void TogglePlayModeFeedback()
        {
            PlayModeFeedbackEnabled = !PlayModeFeedbackEnabled;
            if (PlayModeFeedbackEnabled && Enabled)
            {
                Play(ESEditorFeedbackSoundKind.Confirm);
            }
        }

        [MenuItem(PlayModeMenuPath, true)]
        private static bool ValidateTogglePlayModeFeedback()
        {
            Menu.SetChecked(PlayModeMenuPath, PlayModeFeedbackEnabled);
            return true;
        }

        [MenuItem(SchemeMenuPath, false, 120)]
        private static void OpenSchemeWindow()
        {
            ESEditorFeedbackSoundSchemeWindow.Open();
        }

        [MenuItem(PreviewMenuPath, false, 130)]
        private static void PreviewAllSoundKinds()
        {
            string schemeId = Scheme;
            int missing = 0;
            for (int i = 0; i < SchemePreviewKinds.Length; i++)
            {
                if (!File.Exists(GetClipPath(SchemePreviewKinds[i], schemeId)))
                {
                    missing++;
                }
            }

            if (EditorUtility.audioMasterMute)
            {
                Debug.LogWarning(
                    "[ES 编辑器音效] Unity 编辑器当前处于主静音状态，"
                    + "请先使用“取消 Unity 编辑器静音”菜单。");
            }

            Debug.Log(
                "[ES 编辑器音效] 开始一键试听全部 "
                + SchemePreviewKinds.Length
                + " 类音效，方案="
                + GetSchemeDisplayName(schemeId)
                + "，当前方案缺失 "
                + missing
                + " 个文件（缺失项回退 Default），预计 "
                + string.Format(
                    "{0:F1}",
                    SchemePreviewKinds.Length * SchemePreviewIntervalSeconds)
                + " 秒完成。");
            PreviewScheme(schemeId);
        }

        [MenuItem(ManualRefreshMenuPath, false, 132)]
        private static void ManualSaveAndRefresh()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Play(ESEditorFeedbackSoundKind.Refresh, ManualSaveVolume);
        }

        [MenuItem(UnmuteEditorMenuPath, false, 131)]
        private static void UnmuteUnityEditorAudio()
        {
            EditorUtility.audioMasterMute = false;
            Debug.Log("[ES 编辑器音效] 已取消 Unity 编辑器主静音，开始试听当前方案。");
            PreviewScheme(Scheme);
        }

        [MenuItem(UnmuteEditorMenuPath, true)]
        private static bool ValidateUnmuteUnityEditorAudio()
        {
            return EditorUtility.audioMasterMute;
        }

        public static void Play(ESEditorFeedbackSoundKind kind)
        {
            if (!Enabled || !ShouldPlay(kind, Scheme))
            {
                return;
            }

            PlayInternal(kind);
        }

        public static void Play(
            ESEditorFeedbackSoundKind kind,
            float volumeScale)
        {
            if (!Enabled || !ShouldPlay(kind, Scheme))
            {
                return;
            }

            PlayInternal(kind, Scheme, volumeScale);
        }

        /// <summary>
        /// ES 自有枚举控件的统一反馈入口。第三方 Inspector/Odin 控件不通过全局劫持接入。
        /// </summary>
        public static void NotifyEnumChanged()
        {
            if (!ESEditorFeedbackSoundHook.IsEditorAuthoringContext)
            {
                return;
            }

            Play(ESEditorFeedbackSoundKind.Navigate);
        }

        /// <summary>
        /// 显式试听：绕过总开关和方案启用列表，仍受播放节流约束。
        /// 配置动作应调用 Play，而不是 Preview。
        /// </summary>
        public static void Preview(ESEditorFeedbackSoundKind kind)
        {
            PlayInternal(kind);
        }

        public static void Preview(
            ESEditorFeedbackSoundKind kind,
            float volumeScale)
        {
            PlayInternal(kind, Scheme, volumeScale);
        }

        public static bool IsSchemePreviewing => schemePreviewScheduled;

        public static IReadOnlyList<ESEditorFeedbackSoundKind> StandardKinds =>
            SchemePreviewKinds;

        public static void PreviewScheme(string schemeId)
        {
            if (!IsValidSchemeName(schemeId))
            {
                return;
            }

            StopSchemePreview();
            previewSchemeId = schemeId;
            previewKindIndex = 0;
            nextSchemePreviewAt = EditorApplication.timeSinceStartup + 0.05d;
            schemePreviewScheduled = true;
            EditorApplication.update -= TickSchemePreview;
            EditorApplication.update += TickSchemePreview;
            SchemePreviewStateChanged?.Invoke();
        }

        public static void PreviewSchemeKind(
            string schemeId,
            ESEditorFeedbackSoundKind kind)
        {
            if (!IsValidSchemeName(schemeId))
            {
                return;
            }

            StopSchemePreview();
            PlayInternal(kind, schemeId);
        }

        public static void StopSchemePreview()
        {
            if (!schemePreviewScheduled)
            {
                EditorApplication.update -= TickSchemePreview;
                return;
            }

            schemePreviewScheduled = false;
            previewSchemeId = null;
            previewKindIndex = 0;
            nextSchemePreviewAt = 0d;
            EditorApplication.update -= TickSchemePreview;
            SchemePreviewStateChanged?.Invoke();
        }

        private static void TickSchemePreview()
        {
            if (!schemePreviewScheduled)
            {
                StopSchemePreview();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now < nextSchemePreviewAt)
            {
                return;
            }

            if (previewKindIndex >= SchemePreviewKinds.Length)
            {
                StopSchemePreview();
                return;
            }

            PlayInternal(SchemePreviewKinds[previewKindIndex], previewSchemeId);
            previewKindIndex++;
            nextSchemePreviewAt = now + SchemePreviewIntervalSeconds;
        }

        public static IReadOnlyList<string> GetSchemeIds()
        {
            var result = new List<string>();
            if (Directory.Exists(SchemeRoot))
            {
                string[] directories = Directory.GetDirectories(SchemeRoot);
                for (int i = 0; i < directories.Length; i++)
                {
                    string name = Path.GetFileName(directories[i]);
                    if (IsValidSchemeToken(name))
                    {
                        result.Add(name);
                    }
                }

                result.Sort(StringComparer.Ordinal);
                int defaultIndex = result.IndexOf(DefaultScheme);
                if (defaultIndex > 0)
                {
                    result.RemoveAt(defaultIndex);
                    result.Insert(0, DefaultScheme);
                }
            }

            if (result.Count == 0)
            {
                result.Add(DefaultScheme);
            }

            return result;
        }

        public static string GetSchemeDisplayName(string schemeId)
        {
            if (!IsValidSchemeToken(schemeId))
            {
                schemeId = DefaultScheme;
            }

            if (schemeId == DefaultScheme)
            {
                return "Default / 标准";
            }

            SchemeConfig config = TryLoadConfig(schemeId);
            return config != null && !string.IsNullOrWhiteSpace(config.displayName)
                ? config.displayName
                : schemeId;
        }

        public static bool IsValidSchemeName(string name)
        {
            return IsValidSchemeToken(name)
                && Directory.Exists(Path.Combine(SchemeRoot, name));
        }

        public static bool IsValidNewSchemeName(string name)
        {
            return IsValidSchemeToken(name)
                && !Directory.Exists(Path.Combine(SchemeRoot, name));
        }

        public static void CreateScheme(string name)
        {
            if (!IsValidNewSchemeName(name))
            {
                return;
            }

            Directory.CreateDirectory(Path.Combine(SchemeRoot, name));
            ClearClipCache();
            Scheme = name;
            Play(ESEditorFeedbackSoundKind.Click);
        }

        public static void SuppressSelectionSound()
        {
            selectionSoundSuppressedUntil = EditorApplication.timeSinceStartup + 0.25d;
        }

        public static bool IsSelectionSoundSuppressed =>
            EditorApplication.timeSinceStartup < selectionSoundSuppressedUntil;

        private static bool ShouldPlay(
            ESEditorFeedbackSoundKind kind,
            string schemeId)
        {
            if (IsEnhancedKind(kind) && !EnhancedFeedbackEnabled)
            {
                return false;
            }

            if (schemeId == DefaultScheme)
            {
                return true;
            }

            SchemeConfig config = TryLoadConfig(schemeId);
            if (config == null
                || config.enabledKinds == null
                || config.enabledKinds.Length == 0)
            {
                return true;
            }

            string kindName = GetKindToken(kind);
            for (int i = 0; i < config.enabledKinds.Length; i++)
            {
                if (string.Equals(
                    config.enabledKinds[i],
                    kindName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetKindToken(ESEditorFeedbackSoundKind kind)
        {
            switch (kind)
            {
                case ESEditorFeedbackSoundKind.Click:
                    return "click";
                case ESEditorFeedbackSoundKind.Success:
                    return "success";
                case ESEditorFeedbackSoundKind.Warning:
                    return "warning";
                case ESEditorFeedbackSoundKind.Error:
                    return "error";
                case ESEditorFeedbackSoundKind.Open:
                    return "open";
                case ESEditorFeedbackSoundKind.Close:
                    return "close";
                case ESEditorFeedbackSoundKind.Navigate:
                    return "navigate";
                case ESEditorFeedbackSoundKind.Copy:
                    return "copy";
                case ESEditorFeedbackSoundKind.Locate:
                    return "locate";
                case ESEditorFeedbackSoundKind.Refresh:
                    return "refresh";
                case ESEditorFeedbackSoundKind.Scene:
                    return "scene";
                case ESEditorFeedbackSoundKind.Confirm:
                    return "confirm";
                case ESEditorFeedbackSoundKind.Cancel:
                    return "cancel";
                case ESEditorFeedbackSoundKind.Type:
                    return "type";
                case ESEditorFeedbackSoundKind.AddComponent:
                    return "addcomponent";
                case ESEditorFeedbackSoundKind.RemoveComponent:
                    return "removecomponent";
                case ESEditorFeedbackSoundKind.PrefabOpen:
                    return "prefabopen";
                case ESEditorFeedbackSoundKind.PrefabDirty:
                    return "prefabdirty";
                default:
                    return kind.ToString().ToLowerInvariant();
            }
        }

        private static bool IsEnhancedKind(ESEditorFeedbackSoundKind kind)
        {
            switch (kind)
            {
                case ESEditorFeedbackSoundKind.Type:
                case ESEditorFeedbackSoundKind.AddComponent:
                case ESEditorFeedbackSoundKind.RemoveComponent:
                case ESEditorFeedbackSoundKind.PrefabOpen:
                case ESEditorFeedbackSoundKind.PrefabDirty:
                    return true;
                default:
                    return false;
            }
        }

        private static float GetDefaultKindVolume(ESEditorFeedbackSoundKind kind)
        {
            switch (kind)
            {
                case ESEditorFeedbackSoundKind.Click:
                    return 0.45f;
                case ESEditorFeedbackSoundKind.Success:
                    return 0.85f;
                case ESEditorFeedbackSoundKind.Warning:
                    return 0.75f;
                case ESEditorFeedbackSoundKind.Error:
                    return 0.90f;
                case ESEditorFeedbackSoundKind.Open:
                case ESEditorFeedbackSoundKind.Close:
                    return 0.55f;
                case ESEditorFeedbackSoundKind.Navigate:
                    return 0.30f;
                case ESEditorFeedbackSoundKind.Copy:
                    return 0.45f;
                case ESEditorFeedbackSoundKind.Locate:
                    return 0.40f;
                case ESEditorFeedbackSoundKind.Refresh:
                    return 0.45f;
                case ESEditorFeedbackSoundKind.Scene:
                    return 0.65f;
                case ESEditorFeedbackSoundKind.Confirm:
                case ESEditorFeedbackSoundKind.Cancel:
                    return 0.65f;
                case ESEditorFeedbackSoundKind.Type:
                    return 0.20f;
                case ESEditorFeedbackSoundKind.AddComponent:
                case ESEditorFeedbackSoundKind.RemoveComponent:
                    return 0.35f;
                case ESEditorFeedbackSoundKind.PrefabOpen:
                    return 0.40f;
                case ESEditorFeedbackSoundKind.PrefabDirty:
                    return 0.30f;
                default:
                    return 0.50f;
            }
        }

        private static void PlayInternal(ESEditorFeedbackSoundKind kind)
        {
            PlayInternal(kind, Scheme);
        }

        private static void PlayInternal(
            ESEditorFeedbackSoundKind kind,
            string schemeId)
        {
            PlayInternal(kind, schemeId, GetKindVolume(kind));
        }

        private static void PlayInternal(
            ESEditorFeedbackSoundKind kind,
            string schemeId,
            float volumeScale)
        {
            volumeScale = Mathf.Clamp01(volumeScale);
            if (volumeScale <= 0.001f)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - GetLastKindPlaybackAt(kind) < GetKindCooldown(kind))
            {
                return;
            }

            LastKindPlaybackAt[kind] = now;

            string playbackPath = GetClipPath(kind, schemeId);
            if (EditorUtility.audioMasterMute)
            {
                WarnEditorMasterMuteOnce(playbackPath);
                return;
            }

            AudioClip clip = LoadClip(kind, schemeId, out playbackPath);
            if (clip == null
                && !string.Equals(schemeId, DefaultScheme, StringComparison.Ordinal))
            {
                clip = LoadClip(kind, DefaultScheme, out playbackPath);
            }

            if (clip != null)
            {
                if (TryPlayEditorAudioSource(
                        clip,
                        volumeScale,
                        out string audioSourceFailureReason))
                {
                    return;
                }

                // WinMM 和 AudioUtil 无可靠的逐次音量参数。低音量请求宁可静默失败，
                // 也不能降级成突发的满音量播放或系统提示音。
                if (volumeScale < 0.999f)
                {
                    WarnPlaybackOnce(
                        playbackPath,
                        audioSourceFailureReason
                        + ";VolumeControlledFallbackSuppressed");
                    return;
                }

                if (TryPlayNativeWav(playbackPath, out string nativeFailureReason))
                {
                    return;
                }

                if (TryPlayPreviewClip(clip, out string playbackFailureReason))
                {
                    return;
                }

                WarnPlaybackOnce(
                    playbackPath,
                    audioSourceFailureReason + ";"
                    + nativeFailureReason + ";"
                    + playbackFailureReason);
                EditorApplication.Beep();
                return;
            }

            EditorApplication.Beep();
        }

        private static double GetLastKindPlaybackAt(ESEditorFeedbackSoundKind kind)
        {
            return LastKindPlaybackAt.TryGetValue(kind, out double value)
                ? value
                : double.NegativeInfinity;
        }

        private static double GetKindCooldown(ESEditorFeedbackSoundKind kind)
        {
            switch (kind)
            {
                case ESEditorFeedbackSoundKind.Click:
                    return 0.05d;
                case ESEditorFeedbackSoundKind.Success:
                    return 0.25d;
                case ESEditorFeedbackSoundKind.Warning:
                    return 0.30d;
                case ESEditorFeedbackSoundKind.Error:
                    return 0.35d;
                case ESEditorFeedbackSoundKind.Open:
                    return 0.12d;
                case ESEditorFeedbackSoundKind.Close:
                    return 0.12d;
                case ESEditorFeedbackSoundKind.Navigate:
                    return 0.06d;
                case ESEditorFeedbackSoundKind.Copy:
                    return 0.12d;
                case ESEditorFeedbackSoundKind.Locate:
                    return 0.10d;
                case ESEditorFeedbackSoundKind.Refresh:
                    return 0.20d;
                case ESEditorFeedbackSoundKind.Scene:
                    return 0.25d;
                case ESEditorFeedbackSoundKind.Confirm:
                    return 0.18d;
                case ESEditorFeedbackSoundKind.Cancel:
                    return 0.18d;
                case ESEditorFeedbackSoundKind.Type:
                    return 0.08d;
                case ESEditorFeedbackSoundKind.AddComponent:
                    return 0.18d;
                case ESEditorFeedbackSoundKind.RemoveComponent:
                    return 0.18d;
                case ESEditorFeedbackSoundKind.PrefabOpen:
                    return 0.25d;
                case ESEditorFeedbackSoundKind.PrefabDirty:
                    return 0.30d;
                default:
                    return 0.10d;
            }
        }

        private static AudioClip LoadClip(
            ESEditorFeedbackSoundKind kind,
            string schemeId,
            out string loadedPath)
        {
            string path = GetClipPath(kind, schemeId);
            loadedPath = path;
            if (ExternalClipCache.TryGetValue(path, out AudioClip cached)
                && cached != null)
            {
                return cached;
            }

            AudioClip clip = TryLoadExternalWav(path, out string failReason);
            if (clip == null)
            {
                WarnClipOnce(path, failReason);
                return null;
            }

            ExternalClipCache[path] = clip;
            return clip;
        }

        private static void WarnClipOnce(string path, string reason)
        {
            if (!TryBeginDiagnostic("Load", path, reason))
            {
                return;
            }

            Debug.LogWarning(
                "[ES 编辑器音效] 音效文件加载失败：" + path
                + "（" + reason + "），已回退 Default 或系统提示音。");
        }

        private static void WarnPlaybackOnce(string path, string reason)
        {
            if (!TryBeginDiagnostic("Playback", path, reason))
            {
                return;
            }

            Debug.LogWarning(
                "[ES 编辑器音效] 编辑器播放失败：" + path
                + "（" + reason + "），已回退系统提示音。");
        }

        private static void WarnEditorMasterMuteOnce(string path)
        {
            const string reason = "EditorAudioMasterMuted";
            if (!TryBeginDiagnostic("Playback", path, reason))
            {
                return;
            }

            Debug.LogWarning(
                "[ES 编辑器音效] Unity 编辑器当前处于主静音状态（"
                + reason + "）。请使用菜单“取消 Unity 编辑器静音”后重试。");
        }

        private static bool TryBeginDiagnostic(
            string category,
            string path,
            string reason)
        {
            return WarnedDiagnostics.Add(
                category + "\n" + path + "\n" + reason);
        }

        private static string GetClipPath(
            ESEditorFeedbackSoundKind kind,
            string schemeId)
        {
            return Path.Combine(
                SchemeRoot,
                schemeId,
                kind.ToString().ToLowerInvariant() + ".wav");
        }

        private static AudioClip TryLoadExternalWav(
            string path,
            out string failReason)
        {
            failReason = "Unknown";
            AudioClip clip = null;
            try
            {
                var fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                {
                    failReason = "FileNotFound";
                    return null;
                }

                if (fileInfo.Length < 44)
                {
                    failReason = "FileTooSmall";
                    return null;
                }

                if (fileInfo.Length > MaxWavFileBytes)
                {
                    failReason = "FileTooLarge";
                    return null;
                }

                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 44
                    || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF"
                    || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
                {
                    failReason = "NotRiffWave";
                    return null;
                }

                int riffSize = BitConverter.ToInt32(bytes, 4);
                long riffEnd = (long)riffSize + 8;
                if (riffSize < 0 || riffEnd != bytes.Length)
                {
                    failReason = "InvalidRiffLength";
                    return null;
                }

                int channels = 1;
                int sampleRate = 44100;
                int bitsPerSample = 16;
                int audioFormat = 1;
                int byteRate = 0;
                int declaredBlockAlign = 0;
                int dataStart = -1;
                int dataLength = 0;
                bool sawFormat = false;
                int position = 12;
                while (position + 8 <= riffEnd)
                {
                    string chunkId = Encoding.ASCII.GetString(bytes, position, 4);
                    int chunkSize = BitConverter.ToInt32(bytes, position + 4);
                    if (chunkSize < 0)
                    {
                        failReason = "NegativeChunkSize";
                        return null;
                    }

                    int chunkStart = position + 8;
                    long chunkEnd = (long)chunkStart + chunkSize;
                    if (chunkEnd > riffEnd)
                    {
                        failReason = "ChunkExceedsRiff";
                        return null;
                    }

                    if (chunkId == "fmt " && chunkSize < 16)
                    {
                        failReason = "InvalidFmtChunk";
                        return null;
                    }

                    if (chunkId == "fmt ")
                    {
                        sawFormat = true;
                        audioFormat = BitConverter.ToUInt16(bytes, chunkStart);
                        channels = BitConverter.ToUInt16(bytes, chunkStart + 2);
                        sampleRate = BitConverter.ToInt32(bytes, chunkStart + 4);
                        byteRate = BitConverter.ToInt32(bytes, chunkStart + 8);
                        declaredBlockAlign = BitConverter.ToUInt16(bytes, chunkStart + 12);
                        bitsPerSample = BitConverter.ToUInt16(bytes, chunkStart + 14);
                    }
                    else if (chunkId == "data")
                    {
                        dataStart = chunkStart;
                        dataLength = chunkSize;
                    }

                    long nextPosition = chunkEnd + (chunkSize & 1);
                    if (nextPosition > riffEnd)
                    {
                        failReason = "ChunkPaddingExceedsRiff";
                        return null;
                    }

                    if (nextPosition > int.MaxValue)
                    {
                        failReason = "ChunkPositionOverflow";
                        return null;
                    }

                    position = (int)nextPosition;
                }

                if (position != riffEnd)
                {
                    failReason = "InvalidRiffBoundary";
                    return null;
                }

                bool isPcm = audioFormat == 1
                    && (bitsPerSample == 16
                        || bitsPerSample == 24
                        || bitsPerSample == 32);
                bool isFloat = audioFormat == 3 && bitsPerSample == 32;
                if (!sawFormat
                    || !isPcm && !isFloat
                    || dataStart < 0
                    || dataLength <= 0
                    || channels <= 0
                    || channels > 2
                    || sampleRate <= 0
                    || sampleRate > 192000)
                {
                    failReason = "UnsupportedFormat";
                    return null;
                }

                int bytesPerSample = bitsPerSample / 8;
                int blockAlign = channels * bytesPerSample;
                if (blockAlign <= 0
                    || declaredBlockAlign != blockAlign
                    || byteRate != sampleRate * blockAlign
                    || dataLength % blockAlign != 0)
                {
                    failReason = "InvalidBlockAlignOrByteRate";
                    return null;
                }

                int frameCount = dataLength / blockAlign;
                if (frameCount > sampleRate * MaxWavDurationSeconds)
                {
                    failReason = "ExceedsMaxDuration";
                    return null;
                }

                var samples = new float[frameCount * channels];
                for (int frame = 0; frame < frameCount; frame++)
                {
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int sampleOffset = dataStart + (frame * channels + channel) * bytesPerSample;
                        float sample = ReadWavSample(
                            bytes,
                            sampleOffset,
                            bitsPerSample,
                            audioFormat);
                        if (isFloat && (float.IsNaN(sample) || float.IsInfinity(sample)))
                        {
                            failReason = "InvalidFloatSample";
                            return null;
                        }

                        samples[frame * channels + channel] = sample;
                    }
                }

                clip = AudioClip.Create(
                    Path.GetFileNameWithoutExtension(path),
                    frameCount,
                    channels,
                    sampleRate,
                    false);
                clip.hideFlags = HideFlags.HideAndDontSave;
                if (!clip.SetData(samples, 0))
                {
                    failReason = "SetDataFailed";
                    UnityEngine.Object.DestroyImmediate(clip);
                    clip = null;
                    return null;
                }

                return clip;
            }
            catch (Exception exception)
            {
                if (clip != null)
                {
                    UnityEngine.Object.DestroyImmediate(clip);
                    clip = null;
                }

                failReason = "Exception:" + exception.GetType().Name;
                return null;
            }
        }

        private static bool TryPlayNativeWav(
            string path,
            out string failureReason)
        {
#if UNITY_EDITOR_WIN
            failureReason = "NativePlaybackFailed";
            try
            {
                StopNativePlayback();
                if (!PlaySound(
                        path,
                        IntPtr.Zero,
                        SoundAsync | SoundFileName | SoundNoDefault))
                {
                    failureReason = "NativePlaySoundRejected";
                    return false;
                }

                if (!nativePlaybackDebugLogged)
                {
                    nativePlaybackDebugLogged = true;
                    Debug.Log(
                        "[ES 编辑器音效] Windows WAV 播放已提交：" + path
                        + "。该结果证明 winmm 接受了请求，不证明系统音量或输出设备实际可听。");
                }

                return true;
            }
            catch (Exception exception)
            {
                failureReason = "NativeException:" + exception.GetType().Name;
                return false;
            }
#else
            failureReason = "NativePlaybackUnavailable";
            return false;
#endif
        }

        // 与已验证的 AudioEditorSampler 保持同一条最小 Editor 预览链：
        // 隐藏对象、单个 AudioSource、直接设置 Clip 后 Play，不创建监听器。
        private static bool TryPlayEditorAudioSource(
            AudioClip clip,
            float volumeScale,
            out string failureReason)
        {
            failureReason = "EditorAudioSourcePlaybackFailed";
            try
            {
                if (clip == null)
                {
                    failureReason = "ClipNull";
                    return false;
                }

                if (previewHost == null || previewSource == null)
                {
                    previewHost = new GameObject(
                        "ES Editor Feedback Sound Preview")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    previewSource = previewHost.AddComponent<AudioSource>();
                    previewSource.playOnAwake = false;
                }

                previewSource.Stop();
                previewSource.clip = clip;
                previewSource.volume = Mathf.Clamp01(volumeScale);
                previewSource.time = 0f;
                previewSource.Play();
                EditorApplication.QueuePlayerLoopUpdate();

                if (!editorAudioSourceDebugLogged)
                {
                    editorAudioSourceDebugLogged = true;
                    Debug.Log(
                        "[ES 编辑器音效] AudioEditorSampler 兼容路径已提交："
                        + clip.name
                        + "。该结果仅表示 AudioSource.Play() 已调用。");
                }

                return true;
            }
            catch (Exception exception)
            {
                failureReason = "Exception:" + exception.GetType().Name;
                return false;
            }
        }

        private static void DestroyPreviewHost()
        {
            if (previewHost != null)
            {
                UnityEngine.Object.DestroyImmediate(previewHost);
            }

            previewHost = null;
            previewSource = null;
        }

        private static void StopNativePlayback()
        {
#if UNITY_EDITOR_WIN
            try
            {
                PlaySound(null, IntPtr.Zero, 0u);
            }
            catch (Exception)
            {
                // 清理失败不能阻断缓存释放或后续 AudioUtil 降级。
            }
#endif
        }

#if UNITY_EDITOR_WIN
        private const uint SoundAsync = 0x0001;
        private const uint SoundNoDefault = 0x0002;
        private const uint SoundFileName = 0x00020000;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PlaySound(
            string soundPath,
            IntPtr module,
            uint flags);
#endif

        private static float ReadWavSample(
            byte[] bytes,
            int offset,
            int bitsPerSample,
            int audioFormat)
        {
            if (audioFormat == 3 && bitsPerSample == 32)
            {
                return BitConverter.ToSingle(bytes, offset);
            }

            if (bitsPerSample == 16)
            {
                return BitConverter.ToInt16(bytes, offset) / 32768f;
            }

            if (bitsPerSample == 24)
            {
                int value = bytes[offset]
                    | (bytes[offset + 1] << 8)
                    | (bytes[offset + 2] << 16);
                if ((value & 0x800000) != 0)
                {
                    value |= unchecked((int)0xFF000000);
                }

                return value / 8388608f;
            }

            if (bitsPerSample == 32)
            {
                return BitConverter.ToInt32(bytes, offset) / 2147483648f;
            }

            return 0f;
        }

        private static bool IsValidSchemeToken(string name)
        {
            if (string.IsNullOrWhiteSpace(name)
                || name == "."
                || name == "..")
            {
                return false;
            }

            if (name.IndexOf('/') >= 0
                || name.IndexOf('\\') >= 0
                || name.IndexOf(':') >= 0)
            {
                return false;
            }

            return name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static SchemeConfig TryLoadConfig(string schemeId)
        {
            if (SchemeConfigCache.TryGetValue(schemeId, out SchemeConfig cached))
            {
                return cached;
            }

            string path = Path.Combine(
                SchemeRoot,
                schemeId,
                "scheme.json");
            SchemeConfig config = null;
            if (!File.Exists(path))
            {
                SchemeConfigCache[schemeId] = null;
                return null;
            }

            try
            {
                config = JsonUtility.FromJson<SchemeConfig>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                config = null;
            }

            SchemeConfigCache[schemeId] = config;
            return config;
        }

        private static bool TryPlayPreviewClip(
            AudioClip clip,
            out string failureReason)
        {
            failureReason = "AudioUtilInvocationFailed";
            try
            {
                if (playClipMethods == null)
                {
                    Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                    const BindingFlags flags =
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                    var candidates = new List<MethodInfo>();
                    MethodInfo playPreview = audioUtil?.GetMethod(
                            "PlayPreviewClip",
                            flags,
                            null,
                            new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                            null);
                    MethodInfo playClipWithStart = audioUtil?.GetMethod(
                            "PlayClip",
                            flags,
                            null,
                            new[] { typeof(AudioClip), typeof(int), typeof(bool) },
                            null);
                    MethodInfo playClipSimple = audioUtil?.GetMethod(
                            "PlayClip",
                            flags,
                            null,
                            new[] { typeof(AudioClip) },
                            null);
                    stopAllPreviewClipsMethod = audioUtil?.GetMethod(
                        "StopAllPreviewClips",
                        flags,
                        null,
                        Type.EmptyTypes,
                        null);
                    isPreviewClipPlayingMethod = audioUtil?.GetMethod(
                        "IsPreviewClipPlaying",
                        flags,
                        null,
                        Type.EmptyTypes,
                        null);
                    if (playPreview != null)
                    {
                        candidates.Add(playPreview);
                    }

                    if (playClipWithStart != null)
                    {
                        candidates.Add(playClipWithStart);
                    }

                    if (playClipSimple != null)
                    {
                        candidates.Add(playClipSimple);
                    }

                    playClipMethods = candidates.ToArray();
                }

                if (!playbackMethodDebugLogged)
                {
                    playbackMethodDebugLogged = true;
                    if (playClipMethods.Length > 0)
                    {
                        Debug.Log(
                            "[ES 编辑器音效] AudioUtil 播放候选："
                            + string.Join(", ", Array.ConvertAll(
                                playClipMethods,
                                DescribePlaybackMethod)));
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[ES 编辑器音效] 未解析到 AudioUtil 播放方法，将回退系统提示音。");
                    }
                }

                if (playClipMethods.Length == 0)
                {
                    failureReason = "NoCompatibleAudioUtilMethod";
                    return false;
                }

                if (stopAllPreviewClipsMethod != null)
                {
                    try
                    {
                        stopAllPreviewClipsMethod.Invoke(null, null);
                    }
                    catch (Exception)
                    {
                        // 停止旧预览失败时仍继续尝试播放，避免一次清理失败吞掉音效。
                    }
                }

                for (int i = 0; i < playClipMethods.Length; i++)
                {
                    MethodInfo method = playClipMethods[i];
                    try
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length == 3)
                        {
                            method.Invoke(null, new object[] { clip, 0, false });
                        }
                        else
                        {
                            method.Invoke(null, new object[] { clip });
                        }

                        if (isPreviewClipPlayingMethod != null)
                        {
                            object state = isPreviewClipPlayingMethod.Invoke(null, null);
                            if (!(state is bool isPlaying) || !isPlaying)
                            {
                                failureReason = "AudioUtilPreviewDidNotStart";
                                continue;
                            }
                        }

                        if (!playbackInvocationDebugLogged)
                        {
                            playbackInvocationDebugLogged = true;
                            Debug.Log(
                                "[ES 编辑器音效] AudioUtil 调用已提交："
                                + DescribePlaybackMethod(method)
                                + (isPreviewClipPlayingMethod != null
                                    ? "，且 Unity 报告预览已启动。"
                                    : "。当前 Unity 未提供播放状态查询，仅能确认反射调用未抛异常。")
                                + "该结果不证明系统音频设备实际出声。");
                        }

                        return true;
                    }
                    catch (Exception)
                    {
                        // 继续尝试下一个候选播放方法。
                    }
                }

                return false;
            }
            catch (Exception)
            {
                failureReason = "AudioUtilReflectionFailed";
                return false;
            }
        }

        private static string DescribePlaybackMethod(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var parameterNames = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterNames[i] = parameters[i].ParameterType.Name;
            }

            return method.Name + "(" + string.Join(", ", parameterNames) + ")";
        }

        [Serializable]
        private sealed class SchemeConfig
        {
            public string displayName = string.Empty;
            public string[] enabledKinds = Array.Empty<string>();
        }
    }

    public sealed class ESEditorFeedbackSoundSchemeWindow : ESSinglePageIMGUIWindow<ESEditorFeedbackSoundSchemeWindow>
    {
        private string newSchemeName = "MyScheme";
        private string auditionSchemeId;
        private ESEditorFeedbackSoundKind auditionKind =
            ESEditorFeedbackSoundKind.Click;
        private bool showVolumeSettings;
        private Vector2 volumeScroll;
        private Vector2 scroll;
        private string[] schemeLabels = Array.Empty<string>();

        public static void Open()
        {
            var window = GetWindow<ESEditorFeedbackSoundSchemeWindow>(
                true,
                "ES 编辑器音效方案");
            window.minSize = new Vector2(420f, 300f);
            window.maxSize = new Vector2(640f, 480f);
            window.ShowUtility();
        }

        public override GUIContent ESWindow_GetWindowGUIContent()
        {
            return new GUIContent("ES 编辑器音效方案", "配置、试听并管理编辑器交互反馈音效");
        }
        public override string ESWindow_PresentationShortTitle => "音效";

        protected override string ESWindow_Subtitle => "编辑器反馈音效与分类型音量";
        protected override Vector2 ESWindow_MinSize => new Vector2(420f, 300f);
        protected override Vector2 ESWindow_DefaultSize => new Vector2(600f, 470f);
        protected override string ESWindow_PageStableId => "editor.feedback-sound";
        protected override string ESWindow_PageTitle => "反馈音效方案";
        protected override string ESWindow_PageKeywords => "编辑器 音效 反馈 试听 方案 音量";

        protected override void ESWindow_BuildPageActions(
            ICollection<ESMenuTreePageAction> actions)
        {
            actions.Add(new ESMenuTreePageAction(
                    "feedback-sound.preview",
                    "试听方案",
                    "按顺序试听当前反馈音效方案。",
                    context =>
                    {
                        ESEditorFeedbackSound.PreviewScheme(ESEditorFeedbackSound.Scheme);
                        context.SetStatus("正在试听当前音效方案");
                    })
                .WhenVisible(() => !ESEditorFeedbackSound.IsSchemePreviewing)
                .WithUnityIcon("PlayButton")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "feedback-sound.stop",
                    "停止试听",
                    "停止当前方案试听。",
                    context =>
                    {
                        ESEditorFeedbackSound.StopSchemePreview();
                        context.SetStatus("音效方案试听已停止");
                    })
                .WhenVisible(() => ESEditorFeedbackSound.IsSchemePreviewing)
                .WithUnityIcon("PauseButton")
                .WithPriority(100));
            actions.Add(new ESMenuTreePageAction(
                    "feedback-sound.open-folder",
                    "打开目录",
                    "打开反馈音效方案目录。",
                    _ => EditorUtility.RevealInFinder(ESEditorFeedbackSound.GetSchemeRootPath()))
                .WithUnityIcon("Folder Icon")
                .WithPriority(80));
            actions.Add(new ESMenuTreePageAction(
                    "feedback-sound.refresh-cache",
                    "刷新缓存",
                    "清除反馈音效 Clip 缓存，后续播放会按需重载。",
                    context =>
                    {
                        ESEditorFeedbackSound.ClearClipCache();
                        context.SetStatus("反馈音效缓存已刷新");
                    })
                .WithUnityIcon("Refresh")
                .WithPriority(70));
        }

        protected override void ESWindow_DrawIMGUI(ESMenuTreePageContext context)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("当前方案", EditorStyles.boldLabel);
                GUILayout.Label(
                    ESEditorFeedbackSound.GetSchemeDisplayName(
                        ESEditorFeedbackSound.Scheme));

                EditorGUILayout.Space(4f);
                bool enhanced = EditorGUILayout.Toggle(
                    "增强反馈",
                    ESEditorFeedbackSound.EnhancedFeedbackEnabled);
                if (enhanced != ESEditorFeedbackSound.EnhancedFeedbackEnabled)
                {
                    ESEditorFeedbackSound.EnhancedFeedbackEnabled = enhanced;
                    if (enhanced && ESEditorFeedbackSound.Enabled)
                    {
                        ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    float undoRedoVolume = EditorGUILayout.Slider(
                        "撤销/重做音量",
                        ESEditorFeedbackSound.UndoRedoVolume,
                        0f,
                        1f);
                    if (!Mathf.Approximately(
                            undoRedoVolume,
                            ESEditorFeedbackSound.UndoRedoVolume))
                    {
                        ESEditorFeedbackSound.UndoRedoVolume = undoRedoVolume;
                    }

                    if (GUILayout.Button(
                        "试听",
                        EditorStyles.miniButton,
                        GUILayout.Width(52f)))
                    {
                        ESEditorFeedbackSound.Preview(
                            ESEditorFeedbackSoundKind.Navigate,
                            ESEditorFeedbackSound.UndoRedoVolume);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    float manualSaveVolume = EditorGUILayout.Slider(
                        "保存/刷新音量",
                        ESEditorFeedbackSound.ManualSaveVolume,
                        0f,
                        1f);
                    if (!Mathf.Approximately(
                            manualSaveVolume,
                            ESEditorFeedbackSound.ManualSaveVolume))
                    {
                        ESEditorFeedbackSound.ManualSaveVolume = manualSaveVolume;
                    }

                    if (GUILayout.Button(
                        "试听",
                        EditorStyles.miniButton,
                        GUILayout.Width(52f)))
                    {
                        ESEditorFeedbackSound.Preview(
                            ESEditorFeedbackSoundKind.Refresh,
                            ESEditorFeedbackSound.ManualSaveVolume);
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    float compilationSuccessVolume = EditorGUILayout.Slider(
                        "编译成功音量",
                        ESEditorFeedbackSound.CompilationSuccessVolume,
                        0f,
                        1f);
                    if (!Mathf.Approximately(
                            compilationSuccessVolume,
                            ESEditorFeedbackSound.CompilationSuccessVolume))
                    {
                        ESEditorFeedbackSound.CompilationSuccessVolume =
                            compilationSuccessVolume;
                    }

                    if (GUILayout.Button(
                        "试听",
                        EditorStyles.miniButton,
                        GUILayout.Width(52f)))
                    {
                        ESEditorFeedbackSound.Preview(
                            ESEditorFeedbackSoundKind.Success,
                            ESEditorFeedbackSound.CompilationSuccessVolume);
                    }
                }

                bool playModeFeedback = ESEditorFeedbackSound.PlayModeFeedbackEnabled;
                playModeFeedback = EditorGUILayout.Toggle(
                    "PlayMode 进出反馈",
                    playModeFeedback);
                if (playModeFeedback != ESEditorFeedbackSound.PlayModeFeedbackEnabled)
                {
                    ESEditorFeedbackSound.PlayModeFeedbackEnabled = playModeFeedback;
                    if (playModeFeedback && ESEditorFeedbackSound.Enabled)
                    {
                        ESEditorFeedbackSound.Play(
                            ESEditorFeedbackSoundKind.Confirm);
                    }
                }

                showVolumeSettings = EditorGUILayout.Foldout(
                    showVolumeSettings,
                    "音量设置（18 类）",
                    true);
                if (showVolumeSettings)
                {
                    volumeScroll = EditorGUILayout.BeginScrollView(
                        volumeScroll,
                        GUILayout.Height(150f));
                    IReadOnlyList<ESEditorFeedbackSoundKind> standardKinds =
                        ESEditorFeedbackSound.StandardKinds;
                    for (int i = 0; i < standardKinds.Count; i++)
                    {
                        ESEditorFeedbackSoundKind kind = standardKinds[i];
                        float current = ESEditorFeedbackSound.GetKindVolume(kind);
                        float next = EditorGUILayout.Slider(
                            kind.ToString(),
                            current,
                            0f,
                            1f);
                        if (!Mathf.Approximately(next, current))
                        {
                            ESEditorFeedbackSound.SetKindVolume(kind, next);
                        }
                    }

                    EditorGUILayout.EndScrollView();
                    if (GUILayout.Button(
                        "恢复全部默认音量",
                        EditorStyles.miniButton))
                    {
                        ESEditorFeedbackSound.ResetKindVolumes();
                        ESEditorFeedbackSound.Play(
                            ESEditorFeedbackSoundKind.Confirm);
                    }
                }

                EditorGUILayout.Space(10f);
                GUILayout.Label("方案目录", EditorStyles.boldLabel);
                scroll = EditorGUILayout.BeginScrollView(scroll);
                IReadOnlyList<string> schemes = ESEditorFeedbackSound.GetSchemeIds();
                for (int i = 0; i < schemes.Count; i++)
                {
                    string schemeId = schemes[i];
                    bool selected = string.Equals(
                        schemeId,
                        ESEditorFeedbackSound.Scheme,
                        StringComparison.Ordinal);
                    string label = ESEditorFeedbackSound.GetSchemeDisplayName(schemeId)
                        + (selected ? "（当前）" : string.Empty);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(label, GUILayout.MinWidth(150f));
                        if (GUILayout.Button(
                            "试听整套",
                            EditorStyles.miniButton,
                            GUILayout.Width(72f),
                            GUILayout.Height(24f)))
                        {
                            ESEditorFeedbackSound.PreviewScheme(schemeId);
                        }

                        using (new EditorGUI.DisabledScope(selected))
                        {
                            if (GUILayout.Button(
                                "应用",
                                EditorStyles.miniButton,
                                GUILayout.Width(56f),
                                GUILayout.Height(24f)))
                            {
                                ESEditorFeedbackSound.StopSchemePreview();
                                ESEditorFeedbackSound.ClearClipCache();
                                ESEditorFeedbackSound.Scheme = schemeId;
                                ESEditorFeedbackSound.Play(
                                    ESEditorFeedbackSoundKind.Click);
                                Repaint();
                            }
                        }
                    }
                }
                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(8f);
                GUILayout.Label("精细试听", EditorStyles.boldLabel);
                if (string.IsNullOrEmpty(auditionSchemeId)
                    || !ESEditorFeedbackSound.IsValidSchemeName(auditionSchemeId))
                {
                    auditionSchemeId = ESEditorFeedbackSound.Scheme;
                }

                if (schemeLabels.Length != schemes.Count)
                    schemeLabels = new string[schemes.Count];
                int auditionSchemeIndex = 0;
                for (int i = 0; i < schemes.Count; i++)
                {
                    schemeLabels[i] = ESEditorFeedbackSound.GetSchemeDisplayName(
                        schemes[i]);
                    if (string.Equals(
                        schemes[i],
                        auditionSchemeId,
                        StringComparison.Ordinal))
                    {
                        auditionSchemeIndex = i;
                    }
                }

                auditionSchemeIndex = EditorGUILayout.Popup(
                    "试听方案",
                    auditionSchemeIndex,
                    schemeLabels);
                auditionSchemeId = schemes[auditionSchemeIndex];
                ESEditorFeedbackSoundKind nextAuditionKind =
                    (ESEditorFeedbackSoundKind)EditorGUILayout.EnumPopup(
                        "音效类型",
                        auditionKind);
                if (nextAuditionKind != auditionKind)
                {
                    auditionKind = nextAuditionKind;
                    ESEditorFeedbackSound.NotifyEnumChanged();
                }
                if (GUILayout.Button(
                    "试听所选音效",
                    EditorStyles.miniButton,
                    GUILayout.Height(26f)))
                {
                    ESEditorFeedbackSound.PreviewSchemeKind(
                        auditionSchemeId,
                        auditionKind);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    newSchemeName = EditorGUILayout.TextField(
                        "新方案名",
                        newSchemeName);
                    if (GUILayout.Button("创建", GUILayout.Width(64f)))
                    {
                        if (ESEditorFeedbackSound.IsValidNewSchemeName(newSchemeName))
                        {
                            ESEditorFeedbackSound.CreateScheme(newSchemeName);
                            newSchemeName = "MyScheme";
                            Repaint();
                        }
                        else
                        {
                            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
                            EditorUtility.DisplayDialog(
                                "ES 音效方案",
                                "方案名无效或已存在。",
                                "确定");
                        }
                    }
                }

            }
        }

        protected override void ESWindow_OnHostEnable()
        {
            ESEditorFeedbackSound.SchemePreviewStateChanged -= OnSchemePreviewStateChanged;
            ESEditorFeedbackSound.SchemePreviewStateChanged += OnSchemePreviewStateChanged;
        }

        protected override void ESWindow_OnHostDisable()
        {
            ESEditorFeedbackSound.SchemePreviewStateChanged -= OnSchemePreviewStateChanged;
            ESEditorFeedbackSound.StopSchemePreview();
        }

        private void OnSchemePreviewStateChanged()
        {
            ESWindow_CurrentPageContext?.RefreshPageActions();
            Repaint();
        }
    }

    public static class ESEditorFeedbackSoundHook
    {
        private const int MaxComponentSnapshotCount = 2048;
        private const double SceneFeedbackDedupeSeconds = 0.75d;

        private static bool installed;
        private static int feedbackFrame = -1;
        private static readonly List<Component> ComponentBuffer =
            new List<Component>();
        private static readonly HashSet<int> ComponentAddedThisFrame =
            new HashSet<int>();
        private static readonly Dictionary<int, int> ComponentCountSnapshot =
            new Dictionary<int, int>();
        private static readonly Dictionary<int, string> GameObjectNameSnapshot =
            new Dictionary<int, string>();
        private static int pendingHierarchyCreated;
        private static int pendingHierarchyDestroyed;
        private static bool pendingHierarchyCopied;
        private static bool pendingHierarchyMoved;
        private static bool pendingHierarchyRenamed;
        private static bool hierarchyFeedbackScheduled;
        private static double hierarchyFeedbackAt;
        private static int pendingAssetImported;
        private static int pendingAssetDeleted;
        private static int pendingAssetMoved;
        private static bool assetFeedbackScheduled;
        private static double assetFeedbackAt;
        private static string lastSceneFeedbackPath;
        private static double lastSceneFeedbackAt = double.NegativeInfinity;
        private static string lastHierarchyCommand;
        private static double lastHierarchyCommandAt = double.NegativeInfinity;
        private static bool packageImportInProgress;

        internal static bool PackageImportInProgress => packageImportInProgress;
        internal static bool IsEditorAuthoringContext =>
            !EditorApplication.isPlaying
            && !EditorApplication.isPlayingOrWillChangePlaymode;

        public static void Install()
        {
            if (installed)
            {
                return;
            }

            installed = true;
            ResetState();
            AssemblyReloadEvents.beforeAssemblyReload -= ESEditorFeedbackSound.ClearClipCache;
            AssemblyReloadEvents.beforeAssemblyReload += ESEditorFeedbackSound.ClearClipCache;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorSceneManager.sceneOpened -= OnSceneOpenedForFeedback;
            EditorSceneManager.sceneOpened += OnSceneOpenedForFeedback;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChangedInEditModeForFeedback;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChangedInEditModeForFeedback;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished -= OnCompilationFinished;
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssetDatabase.importPackageCompleted -= OnPackageImportCompleted;
            AssetDatabase.importPackageCompleted += OnPackageImportCompleted;
            AssetDatabase.importPackageFailed -= OnPackageImportFailed;
            AssetDatabase.importPackageFailed += OnPackageImportFailed;
            AssetDatabase.importPackageStarted -= OnPackageImportStarted;
            AssetDatabase.importPackageStarted += OnPackageImportStarted;
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyWindowItemGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGUI;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
            ObjectFactory.componentWasAdded -= OnComponentAdded;
            ObjectFactory.componentWasAdded += OnComponentAdded;
            UnityEditor.ObjectChangeEvents.changesPublished -= OnObjectChangesPublished;
            UnityEditor.ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            PrefabStage.prefabStageDirtied -= OnPrefabStageDirtied;
            PrefabStage.prefabStageDirtied += OnPrefabStageDirtied;
        }

        public static void ResetState()
        {
            ComponentAddedThisFrame.Clear();
            ComponentCountSnapshot.Clear();
            GameObjectNameSnapshot.Clear();
            ComponentBuffer.Clear();
            feedbackFrame = -1;
            pendingHierarchyCreated = 0;
            pendingHierarchyDestroyed = 0;
            pendingHierarchyCopied = false;
            pendingHierarchyMoved = false;
            pendingHierarchyRenamed = false;
            pendingAssetImported = 0;
            pendingAssetDeleted = 0;
            pendingAssetMoved = 0;
            hierarchyFeedbackScheduled = false;
            assetFeedbackScheduled = false;
            packageImportInProgress = false;
            lastSceneFeedbackPath = null;
            lastSceneFeedbackAt = double.NegativeInfinity;
            EditorApplication.update -= FlushHierarchyFeedback;
            EditorApplication.update -= FlushAssetFeedback;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode
                || state == PlayModeStateChange.ExitingPlayMode)
            {
                ESEditorFeedbackSound.ClearClipCache();
            }

            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.PlayModeFeedbackEnabled)
            {
                return;
            }

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Close);
            }
        }

        private static void OnUndoRedoPerformed()
        {
            if (!IsEditorAuthoringContext
                || !ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled)
            {
                return;
            }

            ESEditorFeedbackSound.Play(
                ESEditorFeedbackSoundKind.Navigate,
                ESEditorFeedbackSound.UndoRedoVolume);
        }

        private static void OnSceneOpenedForFeedback(
            UnityEngine.SceneManagement.Scene scene,
            OpenSceneMode mode)
        {
            NotifySceneTransition(scene.path);
        }

        private static void OnActiveSceneChangedInEditModeForFeedback(
            UnityEngine.SceneManagement.Scene previousScene,
            UnityEngine.SceneManagement.Scene nextScene)
        {
            NotifySceneTransition(nextScene.path);
        }

        public static void NotifySceneTransition(string scenePath)
        {
            if (!ESEditorFeedbackSound.Enabled
                || !IsEditorAuthoringContext
                || string.IsNullOrWhiteSpace(scenePath))
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (string.Equals(
                    lastSceneFeedbackPath,
                    scenePath,
                    StringComparison.OrdinalIgnoreCase)
                && now - lastSceneFeedbackAt < SceneFeedbackDedupeSeconds)
            {
                return;
            }

            lastSceneFeedbackPath = scenePath;
            lastSceneFeedbackAt = now;
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Scene);
        }

        private static void OnCompilationFinished(object context)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
            }
            else
            {
                ESEditorFeedbackSound.Play(
                    ESEditorFeedbackSoundKind.Success,
                    ESEditorFeedbackSound.CompilationSuccessVolume);
            }
        }

        private static void OnPackageImportCompleted(string packageName)
        {
            packageImportInProgress = false;
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Success);
        }

        private static void OnPackageImportFailed(string packageName, string errorMessage)
        {
            packageImportInProgress = false;
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Error);
        }

        private static void OnPackageImportStarted(string packageName)
        {
            packageImportInProgress = true;
        }

        public static void NotifyBuildCompleted(bool succeeded)
        {
            ESEditorFeedbackSound.Play(
                succeeded
                    ? ESEditorFeedbackSoundKind.Success
                    : ESEditorFeedbackSoundKind.Error);
        }

        public static void NotifyPrefabApplied()
        {
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Confirm);
        }

        public static void NotifyPrefabReverted()
        {
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Cancel);
        }

        public static void NotifyManualSaveOrRefresh()
        {
            ESEditorFeedbackSound.Play(
                ESEditorFeedbackSoundKind.Refresh,
                ESEditorFeedbackSound.ManualSaveVolume);
        }

        public static void NotifyAssetChanges(
            int importedCount,
            int deletedCount,
            int movedCount)
        {
            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext
                || importedCount <= 0 && deletedCount <= 0 && movedCount <= 0)
            {
                return;
            }

            pendingAssetImported += importedCount;
            pendingAssetDeleted += deletedCount;
            pendingAssetMoved += movedCount;
            assetFeedbackAt = EditorApplication.timeSinceStartup + 0.18d;
            if (!assetFeedbackScheduled)
            {
                assetFeedbackScheduled = true;
                EditorApplication.update -= FlushAssetFeedback;
                EditorApplication.update += FlushAssetFeedback;
            }
        }

        private static void FlushAssetFeedback()
        {
            if (!assetFeedbackScheduled)
            {
                EditorApplication.update -= FlushAssetFeedback;
                return;
            }

            if (EditorApplication.timeSinceStartup < assetFeedbackAt)
            {
                return;
            }

            int imported = pendingAssetImported;
            int deleted = pendingAssetDeleted;
            int moved = pendingAssetMoved;
            pendingAssetImported = 0;
            pendingAssetDeleted = 0;
            pendingAssetMoved = 0;
            assetFeedbackScheduled = false;
            EditorApplication.update -= FlushAssetFeedback;

            if (moved > 0)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Refresh);
            }
            else if (deleted > 0)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Close);
            }
            else if (imported > 0)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
            }
        }

        private static void OnSelectionChanged()
        {
            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            RecordSelectionSnapshots();
            if (ESEditorFeedbackSound.IsSelectionSoundSuppressed)
            {
                return;
            }

            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Locate);
        }

        private static void OnComponentAdded(Component component)
        {
            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            ResetFrameState();
            if (component == null || component.gameObject == null)
            {
                return;
            }

            GameObject gameObject = component.gameObject;
            int instanceId = gameObject.GetInstanceID();
            ComponentAddedThisFrame.Add(instanceId);
            ComponentCountSnapshot[instanceId] = GetComponentCount(gameObject);
            TrimComponentSnapshot();
            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.AddComponent);
        }

        private static void OnObjectChangesPublished(
            ref UnityEditor.ObjectChangeEventStream stream)
        {
            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            ResetFrameState();
            try
            {
                bool addPlayed = false;
                bool removePlayed = false;
                int length = stream.length;
                for (int i = 0; i < length; i++)
                {
                    UnityEditor.ObjectChangeKind kind = stream.GetEventType(i);
                    if (kind == UnityEditor.ObjectChangeKind.CreateGameObjectHierarchy)
                    {
                        stream.GetCreateGameObjectHierarchyEvent(
                            i,
                            out UnityEditor.CreateGameObjectHierarchyEventArgs created);
                        GameObject createdObject = EditorUtility.InstanceIDToObject(
                            created.instanceId) as GameObject;
                        if (createdObject != null)
                        {
                            GameObjectNameSnapshot[created.instanceId] = createdObject.name;
                        }

                        QueueHierarchyDelta(created.instanceId, true, false);
                        continue;
                    }

                    if (kind == UnityEditor.ObjectChangeKind.DestroyGameObjectHierarchy)
                    {
                        stream.GetDestroyGameObjectHierarchyEvent(
                            i,
                            out UnityEditor.DestroyGameObjectHierarchyEventArgs destroyed);
                        ComponentCountSnapshot.Remove(destroyed.instanceId);
                        GameObjectNameSnapshot.Remove(destroyed.instanceId);
                        QueueHierarchyDelta(destroyed.instanceId, false, true);
                        continue;
                    }

                    if (kind == UnityEditor.ObjectChangeKind.ChangeGameObjectParent)
                    {
                        stream.GetChangeGameObjectParentEvent(
                            i,
                            out UnityEditor.ChangeGameObjectParentEventArgs parentChanged);
                        QueueHierarchyDelta(
                            parentChanged.instanceId,
                            false,
                            false,
                            false,
                            true);
                        continue;
                    }

                    if (kind == UnityEditor.ObjectChangeKind.ChangeGameObjectOrComponentProperties)
                    {
                        stream.GetChangeGameObjectOrComponentPropertiesEvent(
                            i,
                            out UnityEditor.ChangeGameObjectOrComponentPropertiesEventArgs changed);
                        UnityEngine.Object changedObject = EditorUtility.InstanceIDToObject(
                            changed.instanceId);
                        if (changedObject is GameObject changedGameObject)
                        {
                            bool renamed = GameObjectNameSnapshot.TryGetValue(
                                    changed.instanceId,
                                    out string previousName)
                                && !string.Equals(
                                    previousName,
                                    changedGameObject.name,
                                    StringComparison.Ordinal);
                            GameObjectNameSnapshot[changed.instanceId] = changedGameObject.name;
                            if (renamed)
                            {
                                QueueHierarchyDelta(
                                    changed.instanceId,
                                    false,
                                    false,
                                    false,
                                    false,
                                    true);
                            }
                        }
                        else if (changedObject is Transform transform)
                        {
                            QueueHierarchyDelta(
                                transform.gameObject.GetInstanceID(),
                                false,
                                false,
                                false,
                                true);
                        }

                        continue;
                    }

                    if (kind == UnityEditor.ObjectChangeKind.UpdatePrefabInstances)
                    {
                        ESEditorFeedbackSound.Play(
                            ESEditorFeedbackSoundKind.Confirm);
                        continue;
                    }

                    if (kind != UnityEditor.ObjectChangeKind.ChangeGameObjectStructure)
                    {
                        continue;
                    }

                    stream.GetChangeGameObjectStructureEvent(
                        i,
                        out UnityEditor.ChangeGameObjectStructureEventArgs structure);
                    int instanceId = structure.instanceId;
                    if (ComponentAddedThisFrame.Remove(instanceId))
                    {
                        continue;
                    }

                    GameObject gameObject =
                        EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                    int currentCount = gameObject == null
                        ? -1
                        : GetComponentCount(gameObject);
                    int previousCount;
                    bool hasPrevious = ComponentCountSnapshot.TryGetValue(
                        instanceId,
                        out previousCount);

                    if (gameObject == null)
                    {
                        ComponentCountSnapshot.Remove(instanceId);
                    }
                    else
                    {
                        ComponentCountSnapshot[instanceId] = currentCount;
                    }

                    if (!hasPrevious)
                    {
                        // 无历史基线时按移除兜底，覆盖首次删除既有组件；
                        // 脚本化添加仍由 ObjectFactory.componentWasAdded 负责。
                        if (gameObject != null && !removePlayed)
                        {
                            removePlayed = true;
                            ESEditorFeedbackSound.Play(
                                ESEditorFeedbackSoundKind.RemoveComponent);
                        }

                        continue;
                    }

                    if (currentCount > previousCount && !addPlayed)
                    {
                        addPlayed = true;
                        ESEditorFeedbackSound.Play(
                            ESEditorFeedbackSoundKind.AddComponent);
                    }
                    else if (currentCount < previousCount && !removePlayed)
                    {
                        removePlayed = true;
                        ESEditorFeedbackSound.Play(
                            ESEditorFeedbackSoundKind.RemoveComponent);
                    }
                }

                TrimComponentSnapshot();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                ComponentAddedThisFrame.Clear();
            }
        }

        private static void OnHierarchyWindowItemGUI(
            int instanceId,
            Rect selectionRect)
        {
            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            Event current = Event.current;
            if (current == null || current.type != EventType.ExecuteCommand)
            {
                return;
            }

            string command = current.commandName;
            if (!string.Equals(command, "Copy", StringComparison.Ordinal)
                && !string.Equals(command, "Paste", StringComparison.Ordinal)
                && !string.Equals(command, "Duplicate", StringComparison.Ordinal))
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (string.Equals(command, lastHierarchyCommand, StringComparison.Ordinal)
                && now - lastHierarchyCommandAt < 0.15d)
            {
                return;
            }

            lastHierarchyCommand = command;
            lastHierarchyCommandAt = now;
            QueueHierarchyDelta(instanceId, false, false, true);
        }

        private static void QueueHierarchyDelta(
            int instanceId,
            bool created,
            bool destroyed,
            bool copied = false,
            bool moved = false,
            bool renamed = false)
        {
            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            if (created)
            {
                pendingHierarchyCreated++;
            }

            if (destroyed)
            {
                pendingHierarchyDestroyed++;
            }

            pendingHierarchyCopied |= copied;
            pendingHierarchyMoved |= moved;
            pendingHierarchyRenamed |= renamed;
            hierarchyFeedbackAt = EditorApplication.timeSinceStartup + 0.12d;
            if (!hierarchyFeedbackScheduled)
            {
                hierarchyFeedbackScheduled = true;
                EditorApplication.update -= FlushHierarchyFeedback;
                EditorApplication.update += FlushHierarchyFeedback;
            }
        }

        private static void FlushHierarchyFeedback()
        {
            if (!hierarchyFeedbackScheduled)
            {
                EditorApplication.update -= FlushHierarchyFeedback;
                return;
            }

            if (EditorApplication.timeSinceStartup < hierarchyFeedbackAt)
            {
                return;
            }

            bool copied = pendingHierarchyCopied;
            bool destroyed = pendingHierarchyDestroyed > 0;
            bool created = pendingHierarchyCreated > 0;
            bool renamed = pendingHierarchyRenamed;
            bool moved = pendingHierarchyMoved;
            pendingHierarchyCreated = 0;
            pendingHierarchyDestroyed = 0;
            pendingHierarchyCopied = false;
            pendingHierarchyMoved = false;
            pendingHierarchyRenamed = false;
            hierarchyFeedbackScheduled = false;
            EditorApplication.update -= FlushHierarchyFeedback;

            if (copied)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Copy);
            }
            else if (destroyed)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Close);
            }
            else if (created)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Open);
            }
            else if (renamed)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Refresh);
            }
            else if (moved)
            {
                ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Navigate);
            }
        }

        private static void OnPrefabStageOpened(PrefabStage stage)
        {
            if (stage == null
                || !ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.PrefabOpen);
        }

        private static void OnPrefabStageClosing(PrefabStage stage)
        {
            if (stage == null
                || !ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.Close);
        }

        private static void OnPrefabStageDirtied(PrefabStage stage)
        {
            if (stage == null
                || !ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !IsEditorAuthoringContext)
            {
                return;
            }

            ESEditorFeedbackSound.Play(ESEditorFeedbackSoundKind.PrefabDirty);
        }

        private static void ResetFrameState()
        {
            int frame = Time.frameCount;
            if (feedbackFrame == frame)
            {
                return;
            }

            feedbackFrame = frame;
            ComponentAddedThisFrame.Clear();
        }

        private static int GetComponentCount(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return 0;
            }

            ComponentBuffer.Clear();
            gameObject.GetComponents<Component>(ComponentBuffer);
            return ComponentBuffer.Count;
        }

        private static void RecordSelectionSnapshots()
        {
            GameObject[] gameObjects = Selection.gameObjects;
            for (int i = 0; i < gameObjects.Length; i++)
            {
                GameObject gameObject = gameObjects[i];
                if (gameObject == null)
                {
                    continue;
                }

                ComponentCountSnapshot[gameObject.GetInstanceID()] =
                    GetComponentCount(gameObject);
                GameObjectNameSnapshot[gameObject.GetInstanceID()] = gameObject.name;
            }

            TrimComponentSnapshot();
        }

        private static void TrimComponentSnapshot()
        {
            if (ComponentCountSnapshot.Count <= MaxComponentSnapshotCount)
            {
                if (GameObjectNameSnapshot.Count <= MaxComponentSnapshotCount)
                {
                    return;
                }
            }

            ComponentCountSnapshot.Clear();
            GameObjectNameSnapshot.Clear();
        }
    }

    internal sealed class ESEditorFeedbackAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!ESEditorFeedbackSound.Enabled
                || !ESEditorFeedbackSound.EnhancedFeedbackEnabled
                || !ESEditorFeedbackSoundHook.IsEditorAuthoringContext
                || ESEditorFeedbackSoundHook.PackageImportInProgress)
            {
                return;
            }

            ESEditorFeedbackSoundHook.NotifyAssetChanges(
                CountUserFacingAssets(importedAssets, true),
                CountUserFacingAssets(deletedAssets, false),
                CountUserFacingAssets(movedAssets, false));
        }

        private static int CountUserFacingAssets(
            string[] paths,
            bool requireRecentCreation)
        {
            if (paths == null || paths.Length == 0)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < paths.Length; i++)
            {
                if (IsUserFacingAssetPath(paths[i])
                    && (!requireRecentCreation || WasRecentlyCreated(paths[i])))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool WasRecentlyCreated(string assetPath)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return false;
                }

                string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                return DateTime.UtcNow - File.GetCreationTimeUtc(fullPath)
                    <= TimeSpan.FromSeconds(10d);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsUserFacingAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return false;
            }

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".cs":
                case ".asmdef":
                case ".asmref":
                case ".dll":
                case ".meta":
                case ".json":
                case ".md":
                case ".txt":
                case ".shader":
                case ".hlsl":
                case ".cginc":
                case ".uxml":
                case ".uss":
                    return false;
                default:
                    return true;
            }
        }
    }

    public sealed class ESEditorFeedbackBuildPostprocessor
        : IPostprocessBuildWithReport
    {
        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            ESEditorFeedbackSoundHook.NotifyBuildCompleted(
                report != null
                && report.summary.result == BuildResult.Succeeded);
        }
    }

    public sealed class ESEditorFeedbackSoundAssemblyStreamInitializer : EditorInvoker_Level0
    {
        public override void InitInvoke()
        {
            ESEditorFeedbackSoundHook.Install();
        }
    }
}
