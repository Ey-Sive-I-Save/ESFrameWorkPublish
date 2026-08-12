using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ES.MenuTree.Editor.Tests")]

namespace ES
{
    public enum ESWindowActionScope : byte
    {
        System,
        Global,
        Window
    }

    /// <summary>
    /// ES 窗口三域动作宿主。宿主的具体行列、换行和折叠布局完全由窗口拥有；
    /// 基础层只校验归属，并向 System 域接入窗口生命周期动作。
    /// </summary>
    public sealed class ESWindowActionHosts
    {
        public VisualElement System { get; }
        public VisualElement Global { get; }
        public VisualElement Window { get; }

        public ESWindowActionHosts(
            VisualElement system = null,
            VisualElement global = null,
            VisualElement window = null)
        {
            System = system;
            Global = global;
            Window = window;
        }

        public VisualElement Get(ESWindowActionScope scope)
        {
            switch (scope)
            {
                case ESWindowActionScope.System: return System;
                case ESWindowActionScope.Global: return Global;
                case ESWindowActionScope.Window: return Window;
                default: throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
            }
        }

        public T Add<T>(ESWindowActionScope scope, T element) where T : VisualElement
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            VisualElement host = Get(scope)
                ?? throw new InvalidOperationException("当前窗口没有声明 " + scope + " 动作宿主。");
            host.Add(element);
            return element;
        }

        public Button AddButton(
            ESWindowActionScope scope,
            string text,
            string tooltip,
            Action action)
        {
            return AddButton(scope, null, text, tooltip, action);
        }

        public Button AddButton(
            ESWindowActionScope scope,
            Texture icon,
            string text,
            string tooltip,
            Action action)
        {
            Button button = EditorInternal.ESWindowPresentation.CreateHeaderActionButton(
                icon,
                text,
                tooltip,
                action);
            return Add(scope, button);
        }

        internal void ValidateOwnership(VisualElement root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            ValidateDistinctScopes();
            ValidateHostOwnership(root, System, nameof(System));
            ValidateHostOwnership(root, Global, nameof(Global));
            ValidateHostOwnership(root, Window, nameof(Window));
        }

        private void ValidateDistinctScopes()
        {
            ValidateDistinctScopes(System, nameof(System), Global, nameof(Global));
            ValidateDistinctScopes(System, nameof(System), Window, nameof(Window));
            ValidateDistinctScopes(Global, nameof(Global), Window, nameof(Window));
        }

        private static void ValidateDistinctScopes(
            VisualElement first,
            string firstName,
            VisualElement second,
            string secondName)
        {
            if (first != null && ReferenceEquals(first, second))
            {
                throw new InvalidOperationException(
                    "ESWindowActionHosts." + firstName + " 与 " + secondName
                    + " 不能复用同一个动作宿主；System、Global、Window 必须各自拥有布局位置。");
            }
        }

        private static void ValidateHostOwnership(
            VisualElement root,
            VisualElement host,
            string hostName)
        {
            if (host == null)
                return;
            for (VisualElement current = host; current != null; current = current.parent)
                if (current == root)
                    return;
            throw new InvalidOperationException(
                "ESWindowActionHosts." + hostName + " 必须属于当前 EditorWindow.rootVisualElement。");
        }
    }

    /// <summary>所有 ES EditorWindow 接入共享 Presentation 与三域动作合同的公开入口。</summary>
    public static class ESWindowFoundation
    {
        public static void Bind(
            EditorWindow window,
            ESWindowActionHosts actionHosts = null,
            bool allowSemiSleep = true)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));
            actionHosts?.ValidateOwnership(window.rootVisualElement);
            EditorInternal.ESEditorPresentation.BindWindow(window, allowSemiSleep, actionHosts);
        }

        public static void Unbind(EditorWindow window, bool windowClosing = false)
        {
            EditorInternal.ESEditorPresentation.UnbindWindow(window, windowClosing);
        }
    }
}

namespace ES.EditorInternal
{
    internal static class ESWindowActivationMotion
    {
        internal const float Duration = 0.64f;

        private const float AnticipationPoint = 0.08f;
        private const float PrimaryOvershootPoint = 0.50f;
        private const float RecoilPoint = 0.70f;
        private const float SecondaryOvershootPoint = 0.84f;
        private const float OpacitySettlePoint = 0.40f;
        private const float TranslationSettlePoint = 0.68f;

        internal static void Apply(VisualElement element, float progress, float intensity)
        {
            if (element == null)
                return;
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float scale = EvaluateScale(normalized, strength);
            element.style.opacity = EvaluateOpacity(normalized, strength);
            element.style.translate = new Translate(
                0f,
                EvaluateTranslateY(normalized, strength),
                0f);
            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        internal static void ApplyWithFrameScale(
            VisualElement element,
            float progress,
            float intensity)
        {
            Apply(element, progress, intensity);
            if (element == null)
                return;
            float scale = ESWindowFrameActivation.EvaluateFrameScale(progress, intensity);
            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        internal static float EvaluateScale(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float start = Mathf.Lerp(1f, 0.735f, strength);
            float anticipation = Mathf.Lerp(1f, 0.70f, strength);
            float primaryOvershoot = Mathf.Lerp(1f, 1.095f, strength);
            float recoil = Mathf.Lerp(1f, 0.968f, strength);
            float secondaryOvershoot = Mathf.Lerp(1f, 1.018f, strength);
            if (normalized <= AnticipationPoint)
            {
                float phase = normalized / AnticipationPoint;
                return Mathf.Lerp(start, anticipation, SmoothStep(phase));
            }

            if (normalized <= PrimaryOvershootPoint)
            {
                float phase = (normalized - AnticipationPoint)
                    / (PrimaryOvershootPoint - AnticipationPoint);
                return Mathf.Lerp(anticipation, primaryOvershoot, EaseOutQuart(phase));
            }

            if (normalized <= RecoilPoint)
            {
                float phase = (normalized - PrimaryOvershootPoint)
                    / (RecoilPoint - PrimaryOvershootPoint);
                return Mathf.Lerp(primaryOvershoot, recoil, SmoothStep(phase));
            }

            if (normalized <= SecondaryOvershootPoint)
            {
                float phase = (normalized - RecoilPoint)
                    / (SecondaryOvershootPoint - RecoilPoint);
                return Mathf.Lerp(recoil, secondaryOvershoot, SmoothStep(phase));
            }

            float settle = (normalized - SecondaryOvershootPoint)
                / (1f - SecondaryOvershootPoint);
            return Mathf.Lerp(secondaryOvershoot, 1f, SmootherStep(settle));
        }

        internal static float EvaluateOpacity(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float phase = Mathf.Clamp01(normalized / OpacitySettlePoint);
            float start = Mathf.Lerp(1f, 0.015f, strength);
            return Mathf.Lerp(start, 1f, EaseOutCubic(phase));
        }

        internal static float EvaluateTranslateY(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float start = 26f * strength;
            float anticipation = 30f * strength;
            float lift = -2f * strength;
            if (normalized <= AnticipationPoint)
            {
                float phase = normalized / AnticipationPoint;
                return Mathf.Lerp(start, anticipation, SmoothStep(phase));
            }

            if (normalized <= PrimaryOvershootPoint)
            {
                float phase = (normalized - AnticipationPoint)
                    / (PrimaryOvershootPoint - AnticipationPoint);
                return Mathf.Lerp(anticipation, lift, EaseOutQuart(phase));
            }

            if (normalized <= TranslationSettlePoint)
            {
                float phase = (normalized - PrimaryOvershootPoint)
                    / (TranslationSettlePoint - PrimaryOvershootPoint);
                return Mathf.Lerp(lift, 0f, SmoothStep(phase));
            }

            return 0f;
        }

        private static float EaseOutCubic(float value)
        {
            float clamped = Mathf.Clamp01(value);
            float inverse = 1f - clamped;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseOutQuart(float value)
        {
            float clamped = Mathf.Clamp01(value);
            float inverse = 1f - clamped;
            float square = inverse * inverse;
            return 1f - square * square;
        }

        private static float SmoothStep(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - 2f * clamped);
        }

        private static float SmootherStep(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * clamped
                * (clamped * (clamped * 6f - 15f) + 10f);
        }
    }

    internal static class ESWindowFrameActivation
    {
        private sealed class RunningAnimation
        {
            internal int WindowId;
            internal EditorWindow Window;
            internal VisualElement Root;
            internal VisualElement Gate;
            internal readonly List<VisualElement> HiddenContent = new List<VisualElement>();
            internal readonly List<StyleEnum<DisplayStyle>> HiddenContentDisplays =
                new List<StyleEnum<DisplayStyle>>();
            internal Rect Target;
            internal Vector2 OriginalMinSize;
            internal float Intensity;
            internal double StartedAt;
            internal IVisualElementScheduledItem Schedule;
        }

        internal const string NativeFrameClass = "es-window-native-frame-activation";
        private static readonly Dictionary<int, RunningAnimation> Running =
            new Dictionary<int, RunningAnimation>();
        private static readonly Dictionary<VisualElement, RunningAnimation> RunningByRoot =
            new Dictionary<VisualElement, RunningAnimation>();

        internal static Rect EvaluateFrame(Rect target, float progress, float intensity)
        {
            float scale = EvaluateFrameScale(progress, intensity);
            float width = Mathf.Max(1f, target.width * scale);
            float height = Mathf.Max(1f, target.height * scale);
            return new Rect(
                target.center.x - width * 0.5f,
                target.center.y - height * 0.5f,
                width,
                height);
        }

        internal static float EvaluateFrameScale(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            float strength = Mathf.Clamp01(intensity);
            float start = Mathf.Lerp(1f, 0.34f, strength);
            float anticipation = Mathf.Lerp(1f, 0.32f, strength);
            float primaryOvershoot = Mathf.Lerp(1f, 1.04f, strength);
            float recoil = Mathf.Lerp(1f, 0.982f, strength);
            float secondaryOvershoot = Mathf.Lerp(1f, 1.012f, strength);
            if (normalized <= 0.08f)
                return Mathf.Lerp(start, anticipation, SmoothStep(normalized / 0.08f));
            if (normalized <= 0.50f)
            {
                float phase = (normalized - 0.08f) / 0.42f;
                return Mathf.Lerp(anticipation, primaryOvershoot, EaseOutQuart(phase));
            }
            if (normalized <= 0.70f)
            {
                float phase = (normalized - 0.50f) / 0.20f;
                return Mathf.Lerp(primaryOvershoot, recoil, SmoothStep(phase));
            }
            if (normalized <= 0.84f)
            {
                float phase = (normalized - 0.70f) / 0.14f;
                return Mathf.Lerp(recoil, secondaryOvershoot, SmoothStep(phase));
            }

            return Mathf.Lerp(
                secondaryOvershoot,
                1f,
                SmootherStep((normalized - 0.84f) / 0.16f));
        }

        internal static void Play(EditorWindow window, Rect target)
        {
            if (window == null)
                return;
            if (Running.ContainsKey(window.GetInstanceID()))
                return;
            Stop(window);
            if (window.rootVisualElement == null
                || !ESEditorPresentation.MotionEnabled
                || ESEditorPresentation.MotionIntensity <= 0.001f
                || target.width <= 1f
                || target.height <= 1f)
                return;

            var running = new RunningAnimation
            {
                WindowId = window.GetInstanceID(),
                Window = window,
                Root = window.rootVisualElement,
                Target = target,
                OriginalMinSize = window.minSize,
                Intensity = ESEditorPresentation.MotionIntensity,
                StartedAt = EditorApplication.timeSinceStartup
            };
            Running[running.WindowId] = running;
            RunningByRoot[running.Root] = running;
            running.Root.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
            try
            {
                running.Gate = CreateOpeningGate(running);
                window.minSize = new Vector2(
                    Mathf.Min(running.OriginalMinSize.x, Mathf.Max(240f, target.width * 0.28f)),
                    Mathf.Min(running.OriginalMinSize.y, Mathf.Max(180f, target.height * 0.28f)));
                window.position = EvaluateFrame(target, 0f, running.Intensity);
                window.Repaint();
                running.Schedule = running.Root.schedule
                    .Execute(() => Update(running))
                    .Every(16);
            }
            catch (Exception exception)
            {
                Complete(running, true);
                Debug.LogException(exception);
            }
        }

        private static void OnRootDetached(DetachFromPanelEvent evt)
        {
            if (evt.currentTarget is VisualElement root
                && RunningByRoot.TryGetValue(root, out RunningAnimation running))
                Complete(running, false);
        }

        internal static void Stop(EditorWindow window, bool restoreWindow = true)
        {
            if (window == null)
                return;
            if (Running.TryGetValue(window.GetInstanceID(), out RunningAnimation running))
                Complete(running, restoreWindow);
        }

        private static void Update(RunningAnimation running)
        {
            if (running == null
                || !Running.TryGetValue(running.WindowId, out RunningAnimation current)
                || !ReferenceEquals(current, running))
            {
                running?.Schedule?.Pause();
                return;
            }

            try
            {
                if (running.Window == null
                    || running.Root == null
                    || running.Root.panel == null)
                {
                    Complete(running, true);
                    return;
                }

                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup
                    - running.StartedAt) / ESWindowActivationMotion.Duration));
                running.Window.position = EvaluateFrame(
                    running.Target,
                    progress,
                    running.Intensity);
                running.Window.Repaint();
                if (progress >= 1f)
                    Complete(running, true);
            }
            catch (Exception exception)
            {
                Complete(running, true);
                Debug.LogException(exception);
            }
        }

        private static void Complete(RunningAnimation running, bool restoreWindow)
        {
            if (running == null)
                return;
            running.Schedule?.Pause();
            if (Running.TryGetValue(running.WindowId, out RunningAnimation current)
                && ReferenceEquals(current, running))
                Running.Remove(running.WindowId);
            if (running.Root != null
                && RunningByRoot.TryGetValue(running.Root, out RunningAnimation rootAnimation)
                && ReferenceEquals(rootAnimation, running))
                RunningByRoot.Remove(running.Root);
            running.Root?.UnregisterCallback<DetachFromPanelEvent>(OnRootDetached);
            try
            {
                if (restoreWindow && running.Root != null)
                    ESWindowOpeningSweep.Stop(running.Root);
                RestoreOpeningGate(running);
                if (restoreWindow)
                    RestoreWindow(running.Window, running.Root, running.Target, running.OriginalMinSize);
            }
            finally
            {
                running.Schedule = null;
                running.Gate = null;
                running.HiddenContent.Clear();
                running.HiddenContentDisplays.Clear();
                running.Root = null;
                running.Window = null;
            }
        }

        private static VisualElement CreateOpeningGate(RunningAnimation running)
        {
            VisualElement root = running?.Root;
            if (root == null)
                return null;

            for (int i = 0; i < root.childCount; i++)
            {
                VisualElement child = root[i];
                if (child == null)
                    continue;
                running.HiddenContent.Add(child);
                running.HiddenContentDisplays.Add(child.style.display);
                child.style.display = DisplayStyle.None;
            }

            var gate = new VisualElement
            {
                name = "ESWindowOpeningGate",
                pickingMode = PickingMode.Position,
                focusable = true,
                viewDataKey = null
            };
            gate.style.position = Position.Absolute;
            gate.style.left = 0f;
            gate.style.right = 0f;
            gate.style.top = 0f;
            gate.style.bottom = 0f;
            gate.style.alignItems = Align.Center;
            gate.style.justifyContent = Justify.Center;
            gate.style.backgroundColor = ESEditorPresentation.WindowSurfaceColor;

            var content = new VisualElement { name = "ESWindowOpeningGateContent" };
            content.style.alignItems = Align.Center;
            content.style.justifyContent = Justify.Center;
            content.style.width = Length.Percent(100f);
            content.style.maxWidth = 520f;
            content.style.paddingLeft = 18f;
            content.style.paddingRight = 18f;

            var brand = new Label("ES") { name = "ESWindowOpeningGateBrand" };
            brand.AddToClassList("es-brand-title");
            brand.style.fontSize = 26f;
            brand.style.unityFontStyleAndWeight = FontStyle.Bold;
            brand.style.unityTextAlign = TextAnchor.MiddleCenter;
            brand.style.color = ESEditorPresentation.SelectedTextColor;
            content.Add(brand);

            var title = new Label(ResolveOpeningTitle(running.Window, root))
            {
                name = "ESWindowOpeningGateTitle"
            };
            title.style.marginTop = 5f;
            title.style.fontSize = 13f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.color = ESEditorPresentation.SectionSelectedTextColor;
            content.Add(title);

            var function = new Label(ResolveOpeningFunction(root))
            {
                name = "ESWindowOpeningGateFunction"
            };
            function.style.marginTop = 4f;
            function.style.fontSize = 10f;
            function.style.unityTextAlign = TextAnchor.MiddleCenter;
            function.style.whiteSpace = WhiteSpace.Normal;
            function.style.color = ESEditorPresentation.SectionMutedTextColor;
            content.Add(function);

            gate.Add(content);
            root.Add(gate);
            gate.BringToFront();
            gate.Focus();
            return gate;
        }

        private static void RestoreOpeningGate(RunningAnimation running)
        {
            if (running == null)
                return;
            running.Gate?.RemoveFromHierarchy();
            int count = Mathf.Min(
                running.HiddenContent.Count,
                running.HiddenContentDisplays.Count);
            for (int i = 0; i < count; i++)
            {
                VisualElement child = running.HiddenContent[i];
                if (child != null)
                    child.style.display = running.HiddenContentDisplays[i];
            }
        }

        private static string ResolveOpeningTitle(EditorWindow window, VisualElement root)
        {
            string title = window?.titleContent?.text;
            if (string.IsNullOrWhiteSpace(title))
                title = root?.Q<Label>("ESWindowTitle")?.text;
            return string.IsNullOrWhiteSpace(title) ? "ES 功能窗口" : title.Trim();
        }

        private static string ResolveOpeningFunction(VisualElement root)
        {
            string status = root?.Q<Label>("ESWindowStatus")?.text;
            const string currentPagePrefix = "当前页面：";
            if (!string.IsNullOrWhiteSpace(status)
                && status.StartsWith(currentPagePrefix, StringComparison.Ordinal))
                return status.Substring(currentPagePrefix.Length).Trim();

            string subtitle = root?.Q<Label>("ESWindowSubtitle")?.text;
            return string.IsNullOrWhiteSpace(subtitle)
                ? "正在准备功能界面"
                : subtitle.Trim();
        }

        private static void RestoreWindow(
            EditorWindow window,
            VisualElement root,
            Rect target,
            Vector2 originalMinSize)
        {
            if (window == null)
                return;
            try
            {
                window.minSize = originalMinSize;
                if (!window.docked && root != null && root.panel != null)
                    window.position = target;
            }
            catch (MissingReferenceException)
            {
            }
            catch (NullReferenceException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static float SmoothStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        private static float SmootherStep(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float EaseOutQuart(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse * inverse;
        }
    }

    internal static class ESWindowOpeningSweep
    {
        private sealed class RunningSweep
        {
            internal VisualElement Root;
            internal VisualElement Host;
            internal VisualElement Beam;
            internal float Width;
            internal float Intensity;
            internal double StartedAt;
            internal IVisualElementScheduledItem Schedule;
        }

        internal const float Duration = 0.72f;
        private const string PlayedClass = "es-window-opening-sweep-played";
        private static readonly Dictionary<VisualElement, RunningSweep> Running =
            new Dictionary<VisualElement, RunningSweep>();

        internal static float EvaluateOpacity(float progress, float intensity)
        {
            float normalized = Mathf.Clamp01(progress);
            return Mathf.Sin(normalized * Mathf.PI)
                * Mathf.Clamp01(intensity)
                * 0.42f;
        }

        internal static float EvaluatePosition(float progress, float width)
        {
            float normalized = Mathf.Clamp01(progress);
            float inverse = 1f - normalized;
            float eased = 1f - inverse * inverse * inverse;
            return Mathf.Lerp(-190f, Mathf.Max(1f, width) + 50f, eased);
        }

        internal static void Play(VisualElement root)
        {
            if (root == null
                || root.panel == null
                || root.ClassListContains(PlayedClass)
                || !ESEditorPresentation.MotionEnabled
                || ESEditorPresentation.MotionIntensity <= 0.001f)
                return;

            Stop(root);
            root.AddToClassList(PlayedClass);
            var host = new VisualElement
            {
                name = "ESWindowOpeningSweep",
                pickingMode = PickingMode.Ignore,
                viewDataKey = null
            };
            host.style.position = Position.Absolute;
            host.style.left = 0f;
            host.style.right = 0f;
            host.style.top = 0f;
            host.style.bottom = 0f;
            host.style.overflow = Overflow.Hidden;

            var beam = new VisualElement
            {
                name = "ESWindowOpeningSweepBeam",
                pickingMode = PickingMode.Ignore,
                viewDataKey = null
            };
            beam.style.position = Position.Absolute;
            beam.style.top = -120f;
            beam.style.bottom = -120f;
            beam.style.width = 150f;
            beam.style.flexDirection = FlexDirection.Row;
            beam.style.rotate = new Rotate(new Angle(-11f, AngleUnit.Degree));
            AddSweepBand(beam, 0.10f, 34f);
            AddSweepBand(beam, 0.28f, 82f);
            AddSweepBand(beam, 0.08f, 34f);
            host.Add(beam);
            root.Add(host);
            host.BringToFront();

            float width = root.resolvedStyle.width;
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 1f)
                width = 1200f;
            var running = new RunningSweep
            {
                Root = root,
                Host = host,
                Beam = beam,
                Width = width,
                Intensity = ESEditorPresentation.MotionIntensity,
                StartedAt = EditorApplication.timeSinceStartup
            };
            Running[root] = running;
            root.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
            running.Schedule = host.schedule.Execute(() => Update(running)).Every(16);
        }

        internal static void Stop(VisualElement root)
        {
            if (root != null && Running.TryGetValue(root, out RunningSweep running))
                Complete(running);
        }

        internal static void Replay(VisualElement root)
        {
            if (root == null)
                return;
            Stop(root);
            root.RemoveFromClassList(PlayedClass);
            Play(root);
        }

        private static void OnRootDetached(DetachFromPanelEvent evt)
        {
            Stop(evt.currentTarget as VisualElement);
        }

        private static void Update(RunningSweep running)
        {
            if (running == null
                || running.Root == null
                || !Running.TryGetValue(running.Root, out RunningSweep current)
                || !ReferenceEquals(current, running))
            {
                running?.Schedule?.Pause();
                return;
            }

            try
            {
                if (running.Root.panel == null
                    || running.Host == null
                    || running.Host.panel == null)
                {
                    Complete(running);
                    return;
                }

                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup
                    - running.StartedAt) / Duration));
                running.Beam.style.left = EvaluatePosition(progress, running.Width);
                running.Beam.style.opacity = EvaluateOpacity(progress, running.Intensity);
                if (progress >= 1f)
                    Complete(running);
            }
            catch (Exception exception)
            {
                Complete(running);
                Debug.LogException(exception);
            }
        }

        private static void Complete(RunningSweep running)
        {
            if (running == null)
                return;
            running.Schedule?.Pause();
            if (running.Root != null
                && Running.TryGetValue(running.Root, out RunningSweep current)
                && ReferenceEquals(current, running))
                Running.Remove(running.Root);
            running.Root?.UnregisterCallback<DetachFromPanelEvent>(OnRootDetached);
            try
            {
                running.Host?.RemoveFromHierarchy();
            }
            catch (NullReferenceException)
            {
            }
            running.Schedule = null;
            running.Beam = null;
            running.Host = null;
            running.Root = null;
        }

        private static void AddSweepBand(VisualElement beam, float alpha, float width)
        {
            var band = new VisualElement { pickingMode = PickingMode.Ignore };
            Color color = ESEditorPresentation.ActiveColor;
            color.a = alpha;
            band.style.width = width;
            band.style.backgroundColor = color;
            beam.Add(band);
        }
    }

    /// <summary>
    /// 已解析的 ES 字段呈现信息。字段反射和新旧 Attribute 合并只执行一次，
    /// GraphView 后续创建卡片与详情面板时直接读取缓存结果。
    /// </summary>
    internal readonly struct ESFieldPresentationMetadata
    {
        public readonly FieldInfo Field;
        public readonly bool IsDefined;
        public readonly ESFieldLevel Level;
        public readonly bool Required;
        public readonly string Hint;

        public ESFieldPresentationMetadata(FieldInfo field, bool isDefined,
            ESFieldLevel level, bool required, string hint)
        {
            Field = field;
            IsDefined = isDefined;
            Level = level;
            Required = required;
            Hint = hint;
        }
    }

    /// <summary>
    /// 按 Payload 类型缓存 ES 字段元数据。缓存随 Unity 域重载自然释放，
    /// 不持有资产、窗口或序列化对象，因此不会形成编辑器生命周期泄漏。
    /// </summary>
    internal static class ESFieldPresentationMetadataCache
    {
        private sealed class TypeMetadata
        {
            public readonly Dictionary<string, ESFieldPresentationMetadata> Fields;
            public readonly ESFieldPresentationMetadata[] SummaryFields;

            public TypeMetadata(Dictionary<string, ESFieldPresentationMetadata> fields,
                ESFieldPresentationMetadata[] summaryFields)
            {
                Fields = fields;
                SummaryFields = summaryFields;
            }
        }

        private static readonly object CacheGate = new object();
        private static readonly Dictionary<Type, TypeMetadata> Cache
            = new Dictionary<Type, TypeMetadata>();

        public static bool TryGet(Type payloadType, string fieldName,
            out ESFieldPresentationMetadata metadata)
        {
            if (payloadType == null || string.IsNullOrWhiteSpace(fieldName))
            {
                metadata = default;
                return false;
            }

            return GetOrCreate(payloadType).Fields.TryGetValue(fieldName, out metadata)
                   && metadata.IsDefined;
        }

        public static IReadOnlyList<ESFieldPresentationMetadata> GetSummaryFields(Type payloadType)
        {
            return payloadType == null
                ? Array.Empty<ESFieldPresentationMetadata>()
                : GetOrCreate(payloadType).SummaryFields;
        }

        private static TypeMetadata GetOrCreate(Type payloadType)
        {
            lock (CacheGate)
            {
                if (Cache.TryGetValue(payloadType, out TypeMetadata cached))
                    return cached;

                TypeMetadata created = Build(payloadType);
                Cache.Add(payloadType, created);
                return created;
            }
        }

        private static TypeMetadata Build(Type payloadType)
        {
            FieldInfo[] fields = payloadType.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var byName = new Dictionary<string, ESFieldPresentationMetadata>(
                fields.Length, StringComparer.Ordinal);
            var summary = new List<ESFieldPresentationMetadata>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                ESFieldAttribute current = field.GetCustomAttribute<ESFieldAttribute>(true);
                ESFieldPolicyAttribute oldPolicy = field.GetCustomAttribute<ESFieldPolicyAttribute>(true);
                ESFieldHintAttribute oldHint = field.GetCustomAttribute<ESFieldHintAttribute>(true);
                bool defined = current != null || oldPolicy != null || oldHint != null;
                ESFieldLevel level = current?.Level
                    ?? (oldPolicy?.Requirement == ESFieldRequirement.Recommended
                        ? ESFieldLevel.Important
                        : oldPolicy?.Requirement == ESFieldRequirement.Required
                            ? ESFieldLevel.Core
                            : ESFieldLevel.Normal);
                bool required = current?.Required == true
                                || oldPolicy?.Requirement == ESFieldRequirement.Required;
                string hint = NormalizeHint(current?.Hint ?? oldHint?.Text);
                var metadata = new ESFieldPresentationMetadata(
                    field, defined, level, required, hint);
                byName[field.Name] = metadata;
                if (field.IsPublic && defined && level != ESFieldLevel.Normal)
                    summary.Add(metadata);
            }

            return new TypeMetadata(byName, summary.ToArray());
        }

        private static string NormalizeHint(string hint)
        {
            return string.IsNullOrWhiteSpace(hint) ? null : hint.Trim();
        }
    }

    /// <summary>
    /// Shared visual primitives for ES editor drawing.
    ///
    /// The class deliberately contains no serialized or runtime state. All objects are lazily
    /// created once per editor skin and are reused by Section, polymorphic and future ES drawers.
    /// This keeps the IMGUI repaint path free from style/texture allocations.
    /// </summary>
    internal static class ESEditorPresentation
    {
        private static bool skinInitialized;
        private static bool cachedProSkin;
        private static int skinGeneration;
        private static int globalEditorSkinGeneration;
        private static GUIStyle surfaceStyle;
        private static GUIStyle headerStyle;
        private static GUIStyle subtitleStyle;
        private static GUIStyle metaStyle;
        private static GUIStyle compactCollectionTitleStyle;
        private static GUIStyle compactCollectionMetaStyle;
        private static GUIStyle compactCollectionBodyStyle;
        private static Texture2D surfaceTexture;
        private static Texture2D compactCollectionBodyTexture;
        private static ESGlobalEditorTheme theme;
        private static bool themeInitialized;
        private static int themeGeneration;
        private static readonly Dictionary<int, WindowBinding> windowBindings =
            new Dictionary<int, WindowBinding>(32);
        private static readonly Dictionary<VisualElement, WindowBinding> windowBindingsByRoot =
            new Dictionary<VisualElement, WindowBinding>(32);
        private const string BrandFontResourcePath = "ESPresentation/Fonts/ESBrandSansSC";
        private const string BrandTypographyStyleSheetPath =
            "Assets/Plugins/ES/Editor/ESPresentation/Styles/ESBrandTypography.uss";
        private static Font brandFont;
        private static bool brandFontLoadAttempted;
        private static StyleSheet brandTypographyStyleSheet;

        private sealed class WindowBinding
        {
            public EditorWindow window;
            public VisualElement root;
            public VisualElement host;
            public VisualElement accentLine;
            public VisualElement sweep;
            public VisualElement semiSleepOverlay;
            public VisualElement semiSleepControls;
            public ES.ESWindowActionHosts actionHosts;
            public Button semiSleepToggleButton;
            public Button semiSleepAllowedButton;
            public Button semiSleepPinButton;
            public ToolbarMenu semiSleepOverflowMenu;
            public IVisualElementScheduledItem animation;
            public bool activationPending;
            public double pulseStartedAt;
            public ESStatusKind pulseStatus;
            public float pulseDuration;
            public bool allowSemiSleep;
            public bool supportsSemiSleep;
            public bool semiSleeping;
            public bool semiSleepTarget;
            public bool semiSleepAnimating;
            public double focusLostAt = -1d;
            public double semiSleepStartedAt;
            public Rect awakeBounds;
            public Rect semiSleepFromBounds;
            public Rect semiSleepToBounds;
            public Vector2 awakeMinSize;
            public Vector2 awakeMaxSize;
            public int semiSleepSlot = -1;
            public bool hasSemiSleepDockBounds;
            public Rect semiSleepDockBounds;
            public int semiSleepDragPointerId = -1;
            public Vector2 semiSleepDragLastPointerPosition;
            public Rect semiSleepDragWindowStart;
            public bool semiSleepDragging;
            public bool semiSleepManualHold;
            public bool pinned;
            public int busyCount;
            public ESWindowActivityState activityState;
            public string activityMessage;
            public string activityPageId;
            public string activityContext;
            public bool focusModeForcedSleep;
        }

        private const float GlobalAccentLineHeight = 2f;
        private const float GlobalSweepDuration = 0.52f;
        internal const float SemiSleepSize = 100f;
        internal const float SemiSleepDelay = 1.6f;
        internal const float SemiSleepDuration = 0.56f;
        private const float SemiSleepTrayGap = 8f;
        private const float SemiSleepTrayMargin = 12f;
        private const float SemiSleepDragThreshold = 4f;
        private const float SemiSleepMaxPointerDelta = 160f;
        private const string SemiSleepPreferenceKey = "ES.EditorPresentation.SemiSleep.Enabled";
        private static bool globalEditorAdaptersInstalled;
        private static bool globalEditorAdapterLifecycleInstalled;
        private static bool deepSkinSyncQueued;
        private static bool? semiSleepEnabledCache;
        private static bool semiSleepUpdateSubscribed;
        private static bool semiSleepAnyAnimating;
        private static double nextSemiSleepIdleCheckAt;

        private const string WorkspaceSessionKeyPrefix = "ES.EditorPresentation.Workspace.";
        private static int focusModeWindowId;

        [Serializable]
        private sealed class WorkspaceSnapshot
        {
            public List<WorkspaceWindowSnapshot> windows = new List<WorkspaceWindowSnapshot>();
        }

        [Serializable]
        private sealed class WorkspaceWindowSnapshot
        {
            public string typeName;
            public int typeIndex;
            public Rect bounds;
            public bool pinned;
            public bool allowSemiSleep;
            public string pageId;
            public int focusOrder;
        }

        private sealed class EmptyWindowLease : IDisposable
        {
            internal static readonly EmptyWindowLease Instance = new EmptyWindowLease();
            public void Dispose() { }
        }

        private sealed class WindowBusyLease : IDisposable
        {
            private EditorWindow window;

            internal WindowBusyLease(EditorWindow window)
            {
                this.window = window;
            }

            public void Dispose()
            {
                EditorWindow current = window;
                window = null;
                if (current == null
                    || !windowBindings.TryGetValue(current.GetInstanceID(), out WindowBinding binding)
                    || binding == null)
                    return;
                binding.busyCount = Mathf.Max(0, binding.busyCount - 1);
                if (binding.busyCount == 0)
                {
                    binding.activityState = ESWindowActivityState.Active;
                    binding.activityMessage = null;
                    binding.activityPageId = null;
                    PulseWindow(current, ESStatusKind.Ready);
                }
                RefreshSemiSleepUpdateSubscription();
            }
        }

        /// <summary>
        /// 安装 ES 对 Unity 原生 Inspector/SceneView 的轻量表现适配。只订阅官方绘制回调，
        /// 不枚举 EditorWindow、不扫描资产、不修改 Unity 控件布局或业务数据。
        /// </summary>
        public static void InstallGlobalEditorAdapters()
        {
            if (!globalEditorAdapterLifecycleInstalled)
            {
                EditorApplication.playModeStateChanged -= OnGlobalPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnGlobalPlayModeStateChanged;
                globalEditorAdapterLifecycleInstalled = true;
            }

            if (!GlobalEditorShellEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            InstallGlobalEditorAdapterCallbacks();
            QueueDeepSkinSynchronization();
        }

        /// <summary>全局 ES 外观是否由主题启用，且当前处于编辑模式。</summary>
        public static bool GlobalEditorShellEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return (current == null || current.enableGlobalEditorShell)
                    && !EditorApplication.isPlayingOrWillChangePlaymode;
            }
        }

        /// <summary>主题/皮肤缓存世代，供已接入窗口判断是否需要重建样式。</summary>
        internal static int ThemeGeneration => themeGeneration;

        private static void InstallGlobalEditorAdapterCallbacks()
        {
            if (globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawGlobalInspectorHeader;
            SceneView.duringSceneGui -= DrawGlobalSceneViewChrome;
            SceneView.duringSceneGui += DrawGlobalSceneViewChrome;
            globalEditorAdaptersInstalled = true;
        }

        /// <summary>卸载全局适配，供测试、域重载和受控关闭路径使用。</summary>
        public static void UninstallGlobalEditorAdapters()
        {
            UninstallGlobalEditorAdapterCallbacks();
            EditorApplication.playModeStateChanged -= OnGlobalPlayModeStateChanged;
            globalEditorAdapterLifecycleInstalled = false;
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            deepSkinSyncQueued = false;
            ESGlobalEditorSkinExperiment.Restore();
            UnbindAllWindowBindings();
        }

        private static void UninstallGlobalEditorAdapterCallbacks()
        {
            if (!globalEditorAdaptersInstalled)
                return;

            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawGlobalInspectorHeader;
            SceneView.duringSceneGui -= DrawGlobalSceneViewChrome;
            globalEditorAdaptersInstalled = false;
        }

        private static void OnGlobalPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                UninstallGlobalEditorAdapterCallbacks();
                SuspendWindowBindings();
                ESGlobalEditorSkinExperiment.Restore();
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                InstallGlobalEditorAdapters();
                ResumeWindowBindings();
                EditorApplication.RepaintHierarchyWindow();
                EditorApplication.RepaintProjectWindow();
                SceneView.RepaintAll();
            }
        }

        private static void QueueDeepSkinSynchronization()
        {
            if (deepSkinSyncQueued)
                return;

            deepSkinSyncQueued = true;
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            EditorApplication.delayCall += SynchronizeDeepSkinWithTheme;
        }

        private static void SynchronizeDeepSkinWithTheme()
        {
            EditorApplication.delayCall -= SynchronizeDeepSkinWithTheme;
            deepSkinSyncQueued = false;

            ESGlobalEditorTheme current = CurrentTheme;
            bool shouldApply = GlobalEditorShellEnabled
                && current != null
                && current.enableDeepEditorSkin;
            ESGlobalEditorSkinExperiment.Synchronize(shouldApply);
        }

        private static void UnbindAllWindowBindings()
        {
            if (windowBindings.Count == 0)
                return;

            List<EditorWindow> windows = new List<EditorWindow>(windowBindings.Count);
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && binding.window != null)
                    windows.Add(binding.window);

            for (int i = 0; i < windows.Count; i++)
                UnbindWindow(windows[i]);
        }

        private static void UnregisterWindowCallbacks(WindowBinding binding)
        {
            if (binding == null || binding.root == null)
                return;

            binding.root.UnregisterCallback<FocusInEvent>(OnWindowFocusIn, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
            binding.root.UnregisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            binding.root.UnregisterCallback<DetachFromPanelEvent>(OnWindowRootDetached);
            binding.semiSleepOverlay?.UnregisterCallback<PointerDownEvent>(OnSemiSleepOverlayPointerDown);
            binding.semiSleepOverlay?.UnregisterCallback<PointerMoveEvent>(OnSemiSleepOverlayPointerMove);
            binding.semiSleepOverlay?.UnregisterCallback<PointerUpEvent>(OnSemiSleepOverlayPointerUp);
            binding.semiSleepOverlay?.UnregisterCallback<PointerCancelEvent>(OnSemiSleepOverlayPointerCancel);
            binding.semiSleepOverlay?.UnregisterCallback<PointerCaptureOutEvent>(OnSemiSleepOverlayPointerCaptureOut);
            windowBindingsByRoot.Remove(binding.root);
        }

        private static void SuspendWindowBindings()
        {
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null)
                    continue;
                RestoreSemiSleep(binding, true);
                binding.animation?.Pause();
                ESWindowFrameActivation.Stop(binding.window);
                ESWindowOpeningSweep.Stop(binding.root);
                UnregisterWindowCallbacks(binding);
                binding.host?.RemoveFromHierarchy();
                binding.semiSleepOverlay?.RemoveFromHierarchy();
                RemoveSemiSleepControls(binding);
                RemoveBrandTypography(binding.root);
                binding.root = null;
            }
            semiSleepAnyAnimating = false;
            RefreshSemiSleepUpdateSubscription();
        }

        private static void ResumeWindowBindings()
        {
            if (!GlobalEditorShellEnabled)
                return;

            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null || binding.window == null || binding.window.rootVisualElement == null)
                    continue;
                AttachWindowOverlay(binding);
            }
            RefreshSemiSleepUpdateSubscription();
        }

        private static void DrawGlobalInspectorHeader(UnityEditor.Editor editor)
        {
            if (editor == null || (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint))
                return;

            Rect rect = EditorGUILayout.GetControlRect(false, 2f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, GetDepthAccent(0));
        }

        private static void DrawGlobalSceneViewChrome(SceneView sceneView)
        {
            if (sceneView == null || Event.current.type != EventType.Repaint)
                return;

            Color previousGuiColor = GUI.color;
            Matrix4x4 previousGuiMatrix = GUI.matrix;
            bool previousGuiEnabled = GUI.enabled;
            Handles.BeginGUI();
            Rect rect = new Rect(0f, 0f, sceneView.position.width, 2f);
            EditorGUI.DrawRect(rect, GetDepthAccent(0));
            Handles.EndGUI();
            GUI.color = previousGuiColor;
            GUI.matrix = previousGuiMatrix;
            GUI.enabled = previousGuiEnabled;
        }

        public static GUIStyle SurfaceStyle
        {
            get
            {
                EnsureSkin();
                if (surfaceStyle == null)
                {
                    surfaceStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, Metric(2f), Metric(2f)),
                        padding = new RectOffset(Metric(9f), Metric(9f), Metric(7f), Metric(8f)),
                        border = new RectOffset(1, 1, 1, 1)
                    };
                    surfaceStyle.normal.background = SurfaceTexture;
                }

                return surfaceStyle;
            }
        }

        internal static int SkinGeneration
        {
            get { return skinGeneration + globalEditorSkinGeneration * 100000; }
        }

        internal static void NotifyGlobalEditorSkinChanged()
        {
            globalEditorSkinGeneration++;
        }

        public static GUIStyle HeaderStyle
        {
            get
            {
                EnsureSkin();
                if (headerStyle == null)
                {
                    headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(0, 0, 0, Metric(2f))
                    };
                    headerStyle.normal.textColor = cachedProSkin
                        ? new Color(0.83f, 0.85f, 0.88f, 1f)
                        : new Color(0.16f, 0.18f, 0.21f, 1f);
                    ApplyBrandFont(headerStyle);
                }

                return headerStyle;
            }
        }

        public static GUIStyle SubtitleStyle
        {
            get
            {
                EnsureSkin();
                if (subtitleStyle == null)
                {
                    subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        wordWrap = true,
                        padding = new RectOffset(0, 0, Metric(1f), Metric(3f))
                    };
                    subtitleStyle.normal.textColor = cachedProSkin
                        ? new Color(0.72f, 0.76f, 0.82f, 1f)
                        : new Color(0.39f, 0.42f, 0.45f, 1f);
                }

                return subtitleStyle;
            }
        }

        public static GUIStyle MetaStyle
        {
            get
            {
                EnsureSkin();
                if (metaStyle == null)
                {
                    metaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, Metric(1f))
                    };
                    metaStyle.normal.textColor = cachedProSkin
                        ? new Color(0.70f, 0.74f, 0.80f, 1f)
                        : new Color(0.37f, 0.40f, 0.44f, 1f);
                }

                return metaStyle;
            }
        }

        /// <summary>
        /// Compact feedback-card primitives used by optional collection drawers. The visual
        /// language is intentionally generic and has no dependency on third-party editor code.
        /// </summary>
        public static float CompactCollectionHeaderHeight
        {
            get { return Mathf.Max(34f, Mathf.Round(36f * Density)); }
        }

        public static GUIStyle CompactCollectionTitleStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionTitleStyle == null)
                {
                    compactCollectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                    compactCollectionTitleStyle.normal.textColor = cachedProSkin
                        ? new Color(0.88f, 0.90f, 0.93f, 1f)
                        : new Color(0.15f, 0.17f, 0.20f, 1f);
                    ApplyBrandFont(compactCollectionTitleStyle);
                }

                return compactCollectionTitleStyle;
            }
        }

        public static GUIStyle CompactCollectionMetaStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionMetaStyle == null)
                {
                    compactCollectionMetaStyle = new GUIStyle(EditorStyles.miniLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        clipping = TextClipping.Clip,
                        padding = new RectOffset(0, 0, 0, 0)
                    };
                    compactCollectionMetaStyle.normal.textColor = cachedProSkin
                        ? new Color(0.58f, 0.62f, 0.68f, 1f)
                        : new Color(0.38f, 0.41f, 0.45f, 1f);
                }

                return compactCollectionMetaStyle;
            }
        }

        public static GUIStyle CompactCollectionBodyStyle
        {
            get
            {
                EnsureSkin();
                if (compactCollectionBodyStyle == null)
                {
                    compactCollectionBodyStyle = new GUIStyle
                    {
                        margin = new RectOffset(0, 0, 0, Metric(2f)),
                        padding = new RectOffset(Metric(9f), Metric(7f), Metric(6f), Metric(7f))
                    };
                    compactCollectionBodyStyle.normal.background = CompactCollectionBodyTexture;
                }

                return compactCollectionBodyStyle;
            }
        }

        public static Color DividerColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.30f, 0.32f, 0.35f, 1f)
                    : new Color(0.72f, 0.74f, 0.76f, 1f);
            }
        }

        /// <summary>
        /// Low-priority category accents borrowed from the FolderSystem ES_Logic artwork.
        /// They are for module identity only and must not replace semantic status colors.
        /// </summary>
        public static Color LogicSteelBlue
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.29f, 0.51f, 0.66f, 0.96f)
                    : new Color(0.24f, 0.46f, 0.62f, 0.96f);
            }
        }

        public static Color LogicGold
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.78f, 0.69f, 0.14f, 0.96f)
                    : new Color(0.68f, 0.49f, 0.06f, 0.96f);
            }
        }

        public static bool IsProSkin
        {
            get
            {
                EnsureSkin();
                return cachedProSkin;
            }
        }

        public static bool ShowSectionSubtitle
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.showSectionSubtitle;
            }
        }

        public static float Density
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null ? 1f : Mathf.Clamp(current.density, 0.85f, 1.20f);
            }
        }

        /// <summary>
        /// Whether optional ES feedback motion is enabled. Motion is presentation-only;
        /// disabling it never hides status text, icons or validation information.
        /// </summary>
        public static bool MotionEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.enableMotion;
            }
        }

        /// <summary>
        /// 用户显式开启的浮动 ES 工具窗口半休眠。偏好只保存开关，不保存窗口引用或矩形。
        /// </summary>
        public static bool SemiSleepEnabled
        {
            get
            {
                if (!semiSleepEnabledCache.HasValue)
                    semiSleepEnabledCache = EditorPrefs.GetBool(SemiSleepPreferenceKey, false);
                return semiSleepEnabledCache.Value;
            }
        }

        public static void SetSemiSleepEnabled(bool enabled)
        {
            if (SemiSleepEnabled != enabled)
            {
                semiSleepEnabledCache = enabled;
                EditorPrefs.SetBool(SemiSleepPreferenceKey, enabled);
                if (!enabled)
                    RestoreAutomaticSemiSleepWindows();
            }
            RefreshAllSemiSleepControls();
            RefreshSemiSleepUpdateSubscription();
        }

        /// <summary>
        /// 绑定一个 ES 编辑器窗口到共享 Presentation 层。绑定只持有当前域内的活动窗口，
        /// 不会扫描全部 EditorWindow，也不会把窗口引用写入资产或 SessionState。
        /// </summary>
        public static void BindWindow(
            EditorWindow window,
            bool allowSemiSleep = true,
            ES.ESWindowActionHosts actionHosts = null)
        {
            if (!GlobalEditorShellEnabled || window == null || window.rootVisualElement == null)
                return;
            actionHosts?.ValidateOwnership(window.rootVisualElement);

            int id = window.GetInstanceID();
            WindowBinding binding;
            if (!windowBindings.TryGetValue(id, out binding) || binding == null || binding.window != window)
            {
                if (binding != null)
                    UnbindWindow(binding.window);

                binding = new WindowBinding
                {
                    window = window,
                    allowSemiSleep = allowSemiSleep,
                    supportsSemiSleep = allowSemiSleep,
                    activationPending = true,
                    pulseStatus = ESStatusKind.None,
                    pulseDuration = GlobalSweepDuration
                };
                windowBindings[id] = binding;
            }
            else
            {
                bool wasSupported = binding.supportsSemiSleep;
                binding.supportsSemiSleep = allowSemiSleep;
                if (wasSupported && !allowSemiSleep)
                {
                    binding.allowSemiSleep = false;
                    RestoreSemiSleep(binding, true);
                }
            }

            if (actionHosts != null && binding.actionHosts != actionHosts)
            {
                RemoveSemiSleepControls(binding);
                binding.actionHosts = actionHosts;
            }

            if (binding.host == null || binding.host.parent == null)
                AttachWindowOverlay(binding);
            else if (binding.supportsSemiSleep && binding.semiSleepControls == null)
                AttachSemiSleepControls(binding);
            else if (!binding.supportsSemiSleep)
                RemoveSemiSleepControls(binding);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        /// <summary>运行时调整当前 ES 窗口是否参与半休眠，不需要重建窗口内容。</summary>
        public static void SetWindowSemiSleepAllowed(EditorWindow window, bool allowed)
        {
            if (window == null
                || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return;
            binding.allowSemiSleep = binding.supportsSemiSleep && allowed;
            binding.focusLostAt = -1d;
            if (!binding.allowSemiSleep)
                RestoreSemiSleep(binding, true);
            if (binding.supportsSemiSleep && binding.semiSleepControls == null)
                AttachSemiSleepControls(binding);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        /// <summary>
        /// 为当前域内的已绑定窗口指定半休眠落点。它只影响本次窗口实例，
        /// 不写入 EditorPrefs、SessionState 或业务资产。
        /// </summary>
        public static bool SetWindowSemiSleepDockBounds(EditorWindow window, Rect bounds)
        {
            if (window == null || bounds.width < 1f || bounds.height < 1f)
                return false;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
            {
                BindWindow(window);
                windowBindings.TryGetValue(window.GetInstanceID(), out binding);
            }
            if (binding == null)
                return false;
            binding.hasSemiSleepDockBounds = true;
            binding.semiSleepDockBounds = ClampSemiSleepDockBounds(
                bounds,
                GetSemiSleepTrayBounds(binding.window.position));
            RefreshSemiSleepControls(binding);
            return true;
        }

        /// <summary>立即请求一个符合条件的浮动 ES 窗口进入半休眠。</summary>
        public static bool RequestWindowSemiSleep(EditorWindow window)
        {
            if (window == null || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return false;
            if (!CanEnterSemiSleep(binding, false))
                return false;
            binding.focusLostAt = EditorApplication.timeSinceStartup - SemiSleepDelay;
            binding.semiSleepManualHold = true;
            BeginSemiSleepTransition(binding, true);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            return true;
        }

        /// <summary>查询窗口此刻是否满足立即休眠的硬条件；不受全局自动开关和固定策略影响。</summary>
        public static bool CanWindowEnterSemiSleep(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && CanEnterSemiSleep(binding, false);
        }

        /// <summary>显式唤醒当前休眠窗口；不会改动参与资格、固定状态或全局自动策略。</summary>
        public static bool RequestWindowWake(EditorWindow window)
        {
            if (window == null
                || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                || !binding.semiSleeping && !binding.semiSleepAnimating)
                return false;
            binding.semiSleepManualHold = false;
            window.Focus();
            BeginSemiSleepTransition(binding, false);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            return true;
        }

        public static bool IsWindowSemiSleeping(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && (binding.semiSleeping || binding.semiSleepAnimating && binding.semiSleepTarget);
        }

        /// <summary>固定窗口，固定期间不会自动进入半休眠。</summary>
        public static void SetWindowPinned(EditorWindow window, bool pinned)
        {
            if (window == null || !windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding))
                return;
            binding.pinned = pinned;
            if (pinned && !binding.semiSleepManualHold)
                RestoreSemiSleep(binding, true);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        public static bool IsWindowPinned(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding.pinned;
        }

        public static bool IsWindowSemiSleepAllowed(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding.supportsSemiSleep
                && binding.allowSemiSleep;
        }

        public static bool IsWindowBound(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                && binding != null
                && binding.window == window;
        }

        /// <summary>进入忙碌状态。支持嵌套 Lease，Dispose 顺序不影响最终状态。</summary>
        public static IDisposable BeginWindowBusy(EditorWindow window, string message = null, string pageId = null)
        {
            if (window == null)
                return EmptyWindowLease.Instance;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding) || binding == null)
            {
                BindWindow(window);
                windowBindings.TryGetValue(window.GetInstanceID(), out binding);
            }
            if (binding == null)
                return EmptyWindowLease.Instance;
            binding.busyCount++;
            binding.activityState = ESWindowActivityState.Busy;
            binding.activityMessage = message;
            binding.activityPageId = pageId;
            if (!string.IsNullOrWhiteSpace(message))
                window.ShowNotification(new GUIContent(message.Trim()), 1.5f);
            RestoreSemiSleep(binding, true);
            PulseWindow(window, ESStatusKind.Info);
            RefreshSemiSleepUpdateSubscription();
            return new WindowBusyLease(window);
        }

        /// <summary>向窗口和可选页面上下文发送一次结果提示，并唤醒目标窗口。</summary>
        public static void NotifyWindow(
            EditorWindow window,
            string message,
            ESStatusKind status = ESStatusKind.Info,
            string pageId = null,
            string context = null,
            bool focus = true)
        {
            if (window == null)
                return;
            if (!windowBindings.ContainsKey(window.GetInstanceID()))
                BindWindow(window);
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding) || binding == null)
                return;
            binding.activityState = status == ESStatusKind.Error || status == ESStatusKind.Warning
                ? ESWindowActivityState.Attention
                : ESWindowActivityState.Background;
            binding.activityMessage = message;
            binding.activityPageId = pageId;
            binding.activityContext = context;
            if (!string.IsNullOrWhiteSpace(message))
                window.ShowNotification(new GUIContent(message.Trim()), 2.5f);
            RestoreSemiSleep(binding, true);
            if (!string.IsNullOrEmpty(pageId)
                && window is ES.IESWindowPageContextHost pageHost)
                pageHost.ESWindow_TrySelectPage(pageId, true);
            if (focus)
                window.Focus();
            PulseWindow(window, status);
            window.Repaint();
        }

        public static string GetWindowActivityMessage(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                ? binding.activityMessage ?? string.Empty
                : string.Empty;
        }

        public static ESWindowActivityState GetWindowActivityState(EditorWindow window)
        {
            return window != null
                && windowBindings.TryGetValue(window.GetInstanceID(), out WindowBinding binding)
                ? binding.activityState
                : ESWindowActivityState.None;
        }

        /// <summary>保存当前已绑定窗口的轻量工作区快照，仅写入当前 Editor 会话。</summary>
        public static void SaveWorkspaceSnapshot(string workspaceId)
        {
            string normalized = NormalizeWorkspaceId(workspaceId);
            var snapshot = new WorkspaceSnapshot();
            var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int focusOrder = 0;
            var orderedBindings = new List<WindowBinding>(windowBindings.Values);
            orderedBindings.Sort(CompareWindowBindings);
            for (int bindingIndex = 0; bindingIndex < orderedBindings.Count; bindingIndex++)
            {
                WindowBinding binding = orderedBindings[bindingIndex];
                if (binding?.window == null || binding.window.docked)
                    continue;
                string typeName = binding.window.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(typeName))
                    continue;
                typeCounts.TryGetValue(typeName, out int typeIndex);
                typeCounts[typeName] = typeIndex + 1;
                snapshot.windows.Add(new WorkspaceWindowSnapshot
                {
                    typeName = typeName,
                    typeIndex = typeIndex,
                    bounds = binding.semiSleeping || binding.semiSleepAnimating
                        ? binding.awakeBounds
                        : binding.window.position,
                    pinned = binding.pinned,
                    allowSemiSleep = binding.allowSemiSleep,
                    pageId = binding.window is ES.IESWindowPageContextHost pageHost
                        ? pageHost.ESWindow_SelectedPageId
                        : string.Empty,
                    focusOrder = ReferenceEquals(EditorWindow.focusedWindow, binding.window)
                        ? int.MaxValue
                        : focusOrder++
                });
            }
            SessionState.SetString(WorkspaceSessionKeyPrefix + normalized, JsonUtility.ToJson(snapshot));
        }

        /// <summary>恢复当前仍存在的窗口，不会自动创建窗口或恢复 Unity 对象引用。</summary>
        public static int RestoreWorkspaceSnapshot(string workspaceId, bool focusLast = true)
        {
            string normalized = NormalizeWorkspaceId(workspaceId);
            string json = SessionState.GetString(WorkspaceSessionKeyPrefix + normalized, string.Empty);
            if (string.IsNullOrEmpty(json))
                return 0;
            WorkspaceSnapshot snapshot;
            try
            {
                snapshot = JsonUtility.FromJson<WorkspaceSnapshot>(json);
            }
            catch (ArgumentException)
            {
                return 0;
            }
            if (snapshot?.windows == null)
                return 0;

            var liveByType = new Dictionary<string, List<WindowBinding>>(StringComparer.Ordinal);
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding?.window == null)
                    continue;
                string typeName = binding.window.GetType().AssemblyQualifiedName;
                if (string.IsNullOrEmpty(typeName))
                    continue;
                if (!liveByType.TryGetValue(typeName, out List<WindowBinding> bindings))
                {
                    bindings = new List<WindowBinding>();
                    liveByType.Add(typeName, bindings);
                }
                bindings.Add(binding);
            }
            foreach (List<WindowBinding> bindings in liveByType.Values)
                bindings.Sort(CompareWindowBindings);

            int restored = 0;
            EditorWindow focusWindow = null;
            int bestFocusOrder = int.MinValue;
            for (int i = 0; i < snapshot.windows.Count; i++)
            {
                WorkspaceWindowSnapshot saved = snapshot.windows[i];
                if (saved == null
                    || string.IsNullOrEmpty(saved.typeName)
                    || !liveByType.TryGetValue(saved.typeName, out List<WindowBinding> matches)
                    || saved.typeIndex < 0
                    || saved.typeIndex >= matches.Count)
                    continue;
                WindowBinding binding = matches[saved.typeIndex];
                RestoreSemiSleep(binding, true);
                binding.pinned = saved.pinned;
                binding.allowSemiSleep = saved.allowSemiSleep;
                if (!binding.window.docked && saved.bounds.width > 1f && saved.bounds.height > 1f)
                    binding.window.position = saved.bounds;
                if (!string.IsNullOrEmpty(saved.pageId)
                    && binding.window is ES.IESWindowPageContextHost pageHost)
                    pageHost.ESWindow_TrySelectPage(saved.pageId, true);
                binding.window.Repaint();
                restored++;
                if (saved.focusOrder > bestFocusOrder)
                {
                    bestFocusOrder = saved.focusOrder;
                    focusWindow = binding.window;
                }
            }
            if (focusLast)
                focusWindow?.Focus();
            RefreshSemiSleepUpdateSubscription();
            return restored;
        }

        public static bool HasWorkspaceSnapshot(string workspaceId)
        {
            return SessionState.GetString(
                WorkspaceSessionKeyPrefix + NormalizeWorkspaceId(workspaceId),
                string.Empty).Length > 0;
        }

        public static bool SetFocusMode(EditorWindow window, bool enabled)
        {
            if (!enabled)
            {
                ExitFocusMode();
                RefreshSemiSleepUpdateSubscription();
                return true;
            }
            if (window == null || !windowBindings.ContainsKey(window.GetInstanceID()))
                return false;
            ExitFocusMode();
            focusModeWindowId = window.GetInstanceID();
            RestoreSemiSleep(windowBindings[focusModeWindowId], true);
            window.Focus();
            RefreshSemiSleepUpdateSubscription();
            return true;
        }

        public static bool IsFocusMode(EditorWindow window)
        {
            return window != null && focusModeWindowId == window.GetInstanceID();
        }

        private static string NormalizeWorkspaceId(string workspaceId)
        {
            return string.IsNullOrWhiteSpace(workspaceId) ? "default" : workspaceId.Trim();
        }

        private static int CompareWindowBindings(WindowBinding left, WindowBinding right)
        {
            int leftId = left?.window == null ? int.MaxValue : left.window.GetInstanceID();
            int rightId = right?.window == null ? int.MaxValue : right.window.GetInstanceID();
            return leftId.CompareTo(rightId);
        }

        private static void ExitFocusMode()
        {
            focusModeWindowId = 0;
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (binding == null || !binding.focusModeForcedSleep)
                    continue;
                binding.focusModeForcedSleep = false;
                RestoreSemiSleep(binding, true);
            }
        }

        /// <summary>
        /// 解除窗口绑定并停止所有局部调度。运行中解绑会恢复开场动画目标尺寸；
        /// 关闭生命周期则保留当前原生窗口几何，避免关闭瞬间反向拉伸。
        /// </summary>
        public static void UnbindWindow(EditorWindow window, bool windowClosing = false)
        {
            if (window == null)
                return;

            ESWindowFrameActivation.Stop(window, !windowClosing);

            int id = window.GetInstanceID();
            WindowBinding binding;
            if (!windowBindings.TryGetValue(id, out binding))
                return;

            RestoreSemiSleep(binding, true);
            if (binding.animation != null)
                binding.animation.Pause();
            ESWindowOpeningSweep.Stop(binding.root);
            UnregisterWindowCallbacks(binding);
            RemoveBrandTypography(binding.root);
            if (binding.host != null)
            {
                binding.host.RemoveFromHierarchy();
            }
            binding.semiSleepOverlay?.RemoveFromHierarchy();
            binding.semiSleepOverlay = null;
            RemoveSemiSleepControls(binding);
            if (focusModeWindowId == id)
                ExitFocusMode();
            windowBindings.Remove(id);
            RefreshSemiSleepUpdateSubscription();
        }

        /// <summary>
        /// 播放一次统一 ES 操作反馈。只在反馈持续期间请求当前窗口局部刷新。
        /// </summary>
        public static void PulseWindow(EditorWindow window, ESStatusKind status = ESStatusKind.Modified)
        {
            if (window == null || !GlobalEditorShellEnabled || !MotionEnabled)
                return;

            WindowBinding binding;
            if (!windowBindings.TryGetValue(window.GetInstanceID(), out binding) || binding == null)
            {
                BindWindow(window);
                if (!windowBindings.TryGetValue(window.GetInstanceID(), out binding) || binding == null)
                    return;
            }

            BeginWindowPulse(binding, status);
        }

        private static void BeginWindowPulse(WindowBinding binding, ESStatusKind status)
        {
            if (binding?.window == null)
                return;
            binding.pulseStatus = status;
            binding.pulseStartedAt = EditorApplication.timeSinceStartup;
            binding.pulseDuration = GlobalSweepDuration;
            binding.animation?.Resume();
            binding.window.Repaint();
        }

        private static void AttachWindowOverlay(WindowBinding binding)
        {
            VisualElement root = binding.window.rootVisualElement;
            if (root == null)
                return;
            UnregisterWindowCallbacks(binding);
            binding.root = root;
            windowBindingsByRoot[root] = binding;
            ApplyBrandTypography(root);
            ESWindowPresentation.ApplySemanticTheme(root);

            if (binding.host != null)
                binding.host.RemoveFromHierarchy();
            if (binding.semiSleepOverlay != null)
                binding.semiSleepOverlay.RemoveFromHierarchy();

            binding.host = new VisualElement
            {
                name = "ESGlobalPresentationOverlay",
                pickingMode = PickingMode.Ignore,
                viewDataKey = null
            };
            binding.host.style.position = Position.Absolute;
            binding.host.style.left = 0f;
            binding.host.style.right = 0f;
            binding.host.style.top = 0f;
            binding.host.style.height = GlobalAccentLineHeight;
            binding.host.style.backgroundColor = GetDepthAccent(0);

            binding.accentLine = new VisualElement { name = "ESGlobalPresentationAccent" };
            binding.accentLine.pickingMode = PickingMode.Ignore;
            binding.accentLine.style.flexGrow = 1f;
            binding.accentLine.style.backgroundColor = GetDepthAccent(0);
            binding.host.Add(binding.accentLine);

            binding.sweep = new VisualElement { name = "ESGlobalPresentationSweep" };
            binding.sweep.pickingMode = PickingMode.Ignore;
            binding.sweep.style.position = Position.Absolute;
            binding.sweep.style.top = 0f;
            binding.sweep.style.bottom = 0f;
            binding.sweep.style.width = 0f;
            binding.sweep.style.backgroundColor = GetStatusAccent(0, ESStatusKind.Modified);
            binding.host.Add(binding.sweep);

            binding.semiSleepOverlay = CreateSemiSleepOverlay(binding);
            root.Add(binding.semiSleepOverlay);
            AttachSemiSleepControls(binding);

            root.RegisterCallback<FocusInEvent>(OnWindowFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnWindowPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback<GeometryChangedEvent>(OnWindowGeometryChanged);
            root.RegisterCallback<DetachFromPanelEvent>(OnWindowRootDetached);
            root.Add(binding.host);
            binding.host.BringToFront();
            binding.semiSleepOverlay.BringToFront();
            BringSemiSleepControlsToFront(binding);

            if (binding.activationPending)
                binding.host.schedule.Execute(() => BeginWindowActivation(binding));

            binding.animation = MotionEnabled
                ? binding.host.schedule.Execute(() => UpdateWindowOverlay(binding)).Every(33)
                : null;
            binding.animation?.Pause();
        }

        private static void BeginWindowActivation(WindowBinding binding)
        {
            if (!IsWindowOverlayAttached(binding) || !binding.activationPending)
                return;

            binding.activationPending = false;
            ESWindowOpeningSweep.Play(binding.root);
            if (binding.window != null && !binding.window.docked)
                ESWindowFrameActivation.Play(binding.window, binding.window.position);
        }

        private static void OnWindowFocusIn(FocusInEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding != null)
            {
                if (focusModeWindowId != 0
                    && focusModeWindowId != binding.window.GetInstanceID())
                    ExitFocusMode();
                binding.focusLostAt = -1d;
                if (binding.activityState == ESWindowActivityState.Attention
                    || binding.activityState == ESWindowActivityState.Background)
                {
                    binding.activityState = ESWindowActivityState.Active;
                    binding.activityMessage = null;
                    binding.activityPageId = null;
                    binding.activityContext = null;
                }
                binding.root?.schedule.Execute(() => RestoreFocusedSemiSleepAfterPointerRouting(binding));
                PulseWindow(binding.window, ESStatusKind.Modified);
            }
        }

        private static void RestoreFocusedSemiSleepAfterPointerRouting(WindowBinding binding)
        {
            if (binding?.window == null
                || !ReferenceEquals(EditorWindow.focusedWindow, binding.window)
                || binding.semiSleepManualHold
                || binding.semiSleepDragPointerId >= 0
                || !binding.semiSleeping && !binding.semiSleepAnimating)
                return;
            BeginSemiSleepTransition(binding, false);
        }

        private static void OnWindowPointerDown(PointerDownEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding != null && evt.button == 0)
                PulseWindow(binding.window, ESStatusKind.Modified);
        }

        private static void OnWindowRootDetached(DetachFromPanelEvent evt)
        {
            WindowBinding binding = FindBindingByRoot(evt.currentTarget as VisualElement);
            if (binding == null)
                return;
            RestoreSemiSleep(binding, true);
            ESWindowOpeningSweep.Stop(binding.root);
        }

        internal static Rect EvaluateSemiSleepTarget(Rect awakeBounds)
        {
            return EvaluateSemiSleepTarget(awakeBounds, 0);
        }

        internal static Rect EvaluateSemiSleepFrame(
            Rect from,
            Rect to,
            float progress,
            bool restoring,
            float intensity)
        {
            float t = Mathf.Clamp01(progress);
            float eased = t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
            float strength = Mathf.Clamp01(intensity);
            float accent = Mathf.Sin(t * Mathf.PI) * strength;
            float width = Mathf.Lerp(from.width, to.width, eased);
            float height = Mathf.Lerp(from.height, to.height, eased);
            float overshoot = restoring ? 0.026f : -0.045f;
            width = Mathf.Max(1f, width + to.width * overshoot * accent);
            height = Mathf.Max(1f, height + to.height * overshoot * accent);
            float anchorX = Mathf.Lerp(from.xMax, to.xMax, eased);
            float top = Mathf.Lerp(from.y, to.y, eased);
            return new Rect(anchorX - width, top, width, height);
        }

        private static VisualElement CreateSemiSleepOverlay(WindowBinding binding)
        {
            var overlay = new VisualElement
            {
                name = "ESSemiSleepOverlay",
                pickingMode = PickingMode.Position,
                userData = binding
            };
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.right = 0f;
            overlay.style.top = 0f;
            overlay.style.bottom = 0f;
            overlay.style.display = DisplayStyle.None;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.backgroundColor = WindowRaisedSurfaceColor;
            overlay.style.borderLeftWidth = 1f;
            overlay.style.borderRightWidth = 1f;
            overlay.style.borderTopWidth = 1f;
            overlay.style.borderBottomWidth = 1f;
            overlay.style.borderLeftColor = ActiveColor;
            overlay.style.borderRightColor = ActiveColor;
            overlay.style.borderTopColor = ActiveColor;
            overlay.style.borderBottomColor = ActiveColor;

            var monogram = new Label("ES");
            monogram.style.fontSize = 20f;
            monogram.style.unityFontStyleAndWeight = FontStyle.Bold;
            monogram.style.color = SelectedTextColor;
            overlay.Add(monogram);

            string title = binding.window?.titleContent?.text;
            var titleLabel = new Label(string.IsNullOrWhiteSpace(title) ? "工具窗口" : title.Trim());
            titleLabel.style.maxWidth = 82f;
            titleLabel.style.marginTop = 3f;
            titleLabel.style.fontSize = 9f;
            titleLabel.style.color = SectionMutedTextColor;
            titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            titleLabel.style.overflow = Overflow.Hidden;
            titleLabel.style.textOverflow = TextOverflow.Ellipsis;
            overlay.Add(titleLabel);
            overlay.RegisterCallback<PointerDownEvent>(OnSemiSleepOverlayPointerDown);
            overlay.RegisterCallback<PointerMoveEvent>(OnSemiSleepOverlayPointerMove);
            overlay.RegisterCallback<PointerUpEvent>(OnSemiSleepOverlayPointerUp);
            overlay.RegisterCallback<PointerCancelEvent>(OnSemiSleepOverlayPointerCancel);
            overlay.RegisterCallback<PointerCaptureOutEvent>(OnSemiSleepOverlayPointerCaptureOut);
            return overlay;
        }

        private static void OnSemiSleepOverlayPointerDown(PointerDownEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            VisualElement overlay = evt.currentTarget as VisualElement;
            if (binding?.window == null || overlay == null || evt.button != 0)
                return;
            binding.semiSleepDragPointerId = evt.pointerId;
            binding.semiSleepDragLastPointerPosition = new Vector2(evt.position.x, evt.position.y)
                + binding.window.position.position;
            binding.semiSleepDragWindowStart = binding.window.position;
            binding.semiSleepDragging = false;
            overlay.CapturePointer(evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerMove(PointerMoveEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            if (binding?.window == null || binding.semiSleepDragPointerId != evt.pointerId)
                return;

            Vector2 pointerScreenPosition = new Vector2(evt.position.x, evt.position.y)
                + binding.window.position.position;
            Vector2 delta = pointerScreenPosition - binding.semiSleepDragLastPointerPosition;
            if (!IsFinite(delta))
                return;
            if (!binding.semiSleepDragging
                && delta.sqrMagnitude < SemiSleepDragThreshold * SemiSleepDragThreshold)
                return;

            binding.semiSleepDragging = true;
            Rect target = EvaluateSemiSleepDragFrame(
                binding.window.position,
                delta,
                GetSemiSleepTrayBounds(binding.awakeBounds));
            binding.window.position = target;
            binding.semiSleepToBounds = target;
            binding.semiSleepDragLastPointerPosition = pointerScreenPosition;
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerUp(PointerUpEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            VisualElement overlay = evt.currentTarget as VisualElement;
            if (binding?.window == null
                || overlay == null
                || binding.semiSleepDragPointerId != evt.pointerId)
                return;

            bool dragged = binding.semiSleepDragging;
            if (overlay.HasPointerCapture(evt.pointerId))
                overlay.ReleasePointer(evt.pointerId);
            ResetSemiSleepDrag(binding);
            if (dragged)
            {
                binding.semiSleepManualHold = true;
                Rect dockBounds = ClampSemiSleepDockBounds(
                    binding.window.position,
                    GetSemiSleepTrayBounds(binding.awakeBounds));
                binding.window.position = dockBounds;
                binding.semiSleepToBounds = dockBounds;
                binding.semiSleepDockBounds = dockBounds;
                binding.hasSemiSleepDockBounds = true;
                ShowSemiSleepOverlay(binding, true, 1f);
                RefreshSemiSleepControls(binding);
            }
            else
            {
                binding.semiSleepManualHold = false;
                binding.window.Focus();
                BeginSemiSleepTransition(binding, false);
            }
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerCancel(PointerCancelEvent evt)
        {
            CancelSemiSleepDrag(evt.currentTarget as VisualElement, evt.pointerId);
            evt.StopImmediatePropagation();
        }

        private static void OnSemiSleepOverlayPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            WindowBinding binding = (evt.currentTarget as VisualElement)?.userData as WindowBinding;
            if (binding != null && binding.semiSleepDragPointerId == evt.pointerId)
                ResetSemiSleepDrag(binding);
        }

        private static void CancelSemiSleepDrag(VisualElement overlay, int pointerId)
        {
            WindowBinding binding = overlay?.userData as WindowBinding;
            if (binding == null || binding.semiSleepDragPointerId != pointerId)
                return;
            if (binding.semiSleepDragging && binding.window != null)
            {
                binding.window.position = binding.semiSleepDragWindowStart;
                binding.semiSleepToBounds = binding.semiSleepDragWindowStart;
            }
            if (overlay.HasPointerCapture(pointerId))
                overlay.ReleasePointer(pointerId);
            ResetSemiSleepDrag(binding);
        }

        private static void ResetSemiSleepDrag(WindowBinding binding)
        {
            if (binding == null)
                return;
            binding.semiSleepDragPointerId = -1;
            binding.semiSleepDragging = false;
        }

        private static void AttachSemiSleepControls(WindowBinding binding)
        {
            RemoveSemiSleepControls(binding);
            if (binding?.root == null || binding.window == null || !binding.supportsSemiSleep)
                return;

            VisualElement toolbar = FindDeclaredSystemActionHost(binding);
            if (toolbar == null)
                return;

            var controls = new VisualElement
            {
                name = "ESWindowSystemActions",
                tooltip = "系统：窗口生命周期与休眠控制"
            };
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.alignItems = Align.Center;
            controls.style.flexShrink = 0f;

            binding.semiSleepAllowedButton = ESWindowPresentation.CreateHeaderActionButton(
                null,
                "允许",
                "允许此窗口参与休眠；关闭后立即恢复并禁用休眠命令。",
                () => SetWindowSemiSleepAllowed(binding.window, !binding.allowSemiSleep));
            binding.semiSleepToggleButton = ESWindowPresentation.CreateHeaderActionButton(
                null,
                "休眠",
                "立即收起到休眠托盘；休眠后单击恢复，拖动可修改下次收纳位置。",
                () => ToggleSemiSleepFromHeader(binding));
            binding.semiSleepPinButton = ESWindowPresentation.CreateHeaderActionButton(
                null,
                "自动",
                "自动：失去焦点后休眠。固定：保持展开且不会自动休眠。",
                () => ToggleSemiSleepPinFromHeader(binding));
            controls.Add(binding.semiSleepAllowedButton);
            controls.Add(binding.semiSleepToggleButton);
            controls.Add(binding.semiSleepPinButton);
            binding.semiSleepOverflowMenu = CreateSemiSleepOverflowMenu(binding);
            controls.Add(binding.semiSleepOverflowMenu);

            controls.style.marginRight = 4f;
            VisualElement systemActions = toolbar.Q<VisualElement>("ESMenuTreeSystemActions");
            if (systemActions != null)
                systemActions.Add(controls);
            else
                toolbar.Insert(0, controls);

            binding.semiSleepControls = controls;
            RefreshSemiSleepControls(binding);
        }

        internal static bool HasDeclaredSystemActionHost(ES.ESWindowActionHosts actionHosts)
        {
            return actionHosts?.System != null;
        }

        private static VisualElement FindDeclaredSystemActionHost(WindowBinding binding)
        {
            if (binding?.root == null)
                return null;
            VisualElement declared = binding.actionHosts?.System;
            if (IsDescendantOf(declared, binding.root))
                return declared;
            return null;
        }

        private static bool IsDescendantOf(VisualElement element, VisualElement root)
        {
            for (VisualElement current = element; current != null; current = current.parent)
                if (current == root)
                    return true;
            return false;
        }

        internal static bool ShouldCompactSystemActions(float rootWidth)
        {
            return rootWidth > 0f && rootWidth < 760f;
        }

        private static ToolbarMenu CreateSemiSleepOverflowMenu(WindowBinding binding)
        {
            var menu = new ToolbarMenu
            {
                name = "ESWindowSystemActionsOverflow",
                text = "窗口",
                tooltip = "窗口生命周期与休眠控制"
            };
            menu.AddToClassList("es-window-header-action-button");
            menu.style.height = 26f;
            menu.style.minHeight = 26f;
            menu.style.minWidth = 52f;
            menu.style.marginLeft = 2f;
            menu.style.color = SectionSelectedTextColor;
            menu.style.backgroundColor = ControlSurfaceColor;
            menu.menu.AppendAction(
                "允许参与休眠",
                _ => SetWindowSemiSleepAllowed(binding.window, !binding.allowSemiSleep),
                _ => binding.supportsSemiSleep && binding.allowSemiSleep
                    ? DropdownMenuAction.Status.Checked
                    : DropdownMenuAction.Status.Normal);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction(
                "立即休眠",
                _ => RequestWindowSemiSleep(binding.window),
                _ => CanUseSleepCommand(binding) && !IsSleepingOrTargetingSleep(binding)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendAction(
                "立即唤醒",
                _ => RequestWindowWake(binding.window),
                _ => CanUseSleepCommand(binding) && IsSleepingOrTargetingSleep(binding)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction(
                "自动模式",
                _ => SetWindowPinned(binding.window, false),
                _ => GetPinModeStatus(binding, false));
            menu.menu.AppendAction(
                "固定展开",
                _ => SetWindowPinned(binding.window, true),
                _ => GetPinModeStatus(binding, true));
            return menu;
        }

        private static bool CanUseSleepCommand(WindowBinding binding)
        {
            return binding?.window != null && binding.allowSemiSleep && !binding.window.docked;
        }

        private static bool IsSleepingOrTargetingSleep(WindowBinding binding)
        {
            return binding != null
                && (binding.semiSleeping || binding.semiSleepAnimating && binding.semiSleepTarget);
        }

        private static DropdownMenuAction.Status GetPinModeStatus(
            WindowBinding binding,
            bool pinned)
        {
            if (!CanUseSleepCommand(binding))
                return DropdownMenuAction.Status.Disabled;
            return binding.pinned == pinned
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal;
        }

        private static void RemoveSemiSleepControls(WindowBinding binding)
        {
            if (binding == null)
                return;
            binding.semiSleepControls?.RemoveFromHierarchy();
            binding.semiSleepControls = null;
            binding.semiSleepAllowedButton = null;
            binding.semiSleepToggleButton = null;
            binding.semiSleepPinButton = null;
            binding.semiSleepOverflowMenu = null;
        }

        private static void BringSemiSleepControlsToFront(WindowBinding binding)
        {
            // Declared toolbar hosts own layout and paint order. System actions never escape them.
        }

        private static void OnWindowGeometryChanged(GeometryChangedEvent evt)
        {
            VisualElement root = evt.currentTarget as VisualElement;
            if (root != null && windowBindingsByRoot.TryGetValue(root, out WindowBinding binding))
                RefreshSemiSleepControls(binding);
        }

        private static void ToggleSemiSleepFromHeader(WindowBinding binding)
        {
            if (binding?.window == null)
                return;
            if (binding.semiSleeping || binding.semiSleepAnimating && binding.semiSleepTarget)
            {
                RequestWindowWake(binding.window);
            }
            else
            {
                RequestWindowSemiSleep(binding.window);
            }
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        private static void ToggleSemiSleepPinFromHeader(WindowBinding binding)
        {
            if (binding?.window == null)
                return;
            SetWindowPinned(binding.window, !binding.pinned);
        }

        private static void RefreshAllSemiSleepControls()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                RefreshSemiSleepControls(binding);
        }

        private static void RefreshSemiSleepControls(WindowBinding binding)
        {
            if (binding?.semiSleepControls == null)
                return;
            bool sleeping = binding.semiSleeping || binding.semiSleepAnimating && binding.semiSleepTarget;
            bool visible = binding.supportsSemiSleep && binding.window != null;
            binding.semiSleepControls.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible)
                return;
            bool docked = binding.window.docked;
            bool commandsEnabled = binding.allowSemiSleep && !docked;
            bool compact = ShouldCompactSystemActions(binding.root?.resolvedStyle.width ?? 0f);
            binding.semiSleepAllowedButton.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            binding.semiSleepToggleButton.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            binding.semiSleepPinButton.style.display = compact ? DisplayStyle.None : DisplayStyle.Flex;
            if (binding.semiSleepOverflowMenu != null)
            {
                binding.semiSleepOverflowMenu.style.display = compact
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
                binding.semiSleepOverflowMenu.tooltip = docked
                    ? "停靠窗口保持展开；拖出后可使用休眠控制。"
                    : sleeping
                        ? "窗口正在休眠；打开菜单可立即唤醒或调整模式。"
                        : "窗口生命周期与休眠控制";
            }

            if (binding.semiSleepAllowedButton != null)
            {
                SetHeaderActionButtonText(
                    binding.semiSleepAllowedButton,
                    binding.allowSemiSleep ? "允许" : "禁用");
                binding.semiSleepAllowedButton.tooltip = binding.allowSemiSleep
                    ? "此窗口允许参与休眠。单击禁用并立即恢复窗口。"
                    : "此窗口已禁用休眠。单击重新允许休眠命令与自动策略。";
                binding.semiSleepAllowedButton.style.backgroundColor = binding.allowSemiSleep
                    ? ControlSurfaceColor
                    : GetStatusAccent(0, ESStatusKind.Warning);
            }

            if (binding.semiSleepToggleButton != null)
            {
                SetHeaderActionButtonText(
                    binding.semiSleepToggleButton,
                    docked ? "停靠" : sleeping ? "唤醒" : "休眠");
                binding.semiSleepToggleButton.SetEnabled(commandsEnabled);
                binding.semiSleepToggleButton.tooltip = !binding.allowSemiSleep
                    ? "此窗口已禁用休眠；先使用“禁用”按钮重新允许。"
                    : docked
                    ? "停靠窗口保持展开；拖出为浮动窗口后可使用休眠模式。"
                    : sleeping
                    ? "恢复窗口；也可单击休眠块恢复，拖动休眠块修改收纳位置。"
                    : "立即收起到休眠托盘；休眠后单击恢复，拖动可修改下次收纳位置。";
                binding.semiSleepToggleButton.style.backgroundColor = sleeping
                    ? ActiveColor
                    : ControlSurfaceColor;
            }
            if (binding.semiSleepPinButton != null)
            {
                string modeText = binding.pinned ? "固定" : "自动";
                SetHeaderActionButtonText(binding.semiSleepPinButton, modeText);
                binding.semiSleepPinButton.SetEnabled(commandsEnabled);
                if (!binding.allowSemiSleep)
                    binding.semiSleepPinButton.tooltip = "此窗口已禁用休眠；自动策略暂不可用。";
                else if (docked)
                    binding.semiSleepPinButton.tooltip = "停靠窗口不参与半休眠。";
                else if (!SemiSleepEnabled)
                    binding.semiSleepPinButton.tooltip = binding.pinned
                        ? "固定模式；全局自动休眠当前关闭。单击解除固定，不会改动全局开关。"
                        : "自动模式已就绪，但全局自动休眠当前关闭。此按钮不会改动全局开关。";
                else if (binding.pinned)
                    binding.semiSleepPinButton.tooltip = "固定模式：窗口不会自动休眠。单击切换为自动。";
                else
                    binding.semiSleepPinButton.tooltip = "自动模式：失去焦点后进入休眠。单击固定窗口。";
                binding.semiSleepPinButton.style.backgroundColor = binding.pinned
                    ? GetStatusAccent(0, ESStatusKind.Warning)
                    : ControlSurfaceColor;
            }
        }

        private static void SetHeaderActionButtonText(Button button, string text)
        {
            if (button == null)
                return;
            Label label = button.Q<Label>();
            if (label != null)
                label.text = text ?? string.Empty;
            else
                button.text = text ?? string.Empty;
        }

        private static void RefreshSemiSleepUpdateSubscription()
        {
            bool shouldSubscribe = (SemiSleepEnabled || focusModeWindowId != 0 || HasSemiSleepRuntimeState())
                && GlobalEditorShellEnabled
                && HasSemiSleepCandidates();
            if (shouldSubscribe == semiSleepUpdateSubscribed)
                return;
            semiSleepUpdateSubscribed = shouldSubscribe;
            EditorApplication.update -= UpdateSemiSleepWindows;
            AssemblyReloadEvents.beforeAssemblyReload -= RestoreAllSemiSleepWindows;
            EditorApplication.quitting -= RestoreAllSemiSleepWindows;
            if (!shouldSubscribe)
            {
                semiSleepAnyAnimating = false;
                return;
            }
            nextSemiSleepIdleCheckAt = 0d;
            EditorApplication.update += UpdateSemiSleepWindows;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAllSemiSleepWindows;
            EditorApplication.quitting += RestoreAllSemiSleepWindows;
        }

        private static bool HasSemiSleepCandidates()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && binding.allowSemiSleep && binding.window != null)
                    return true;
            return false;
        }

        private static bool HasSemiSleepRuntimeState()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && binding.semiSleepAnimating)
                    return true;
            return false;
        }

        private static bool IsSemiSleepEligible(WindowBinding binding)
        {
            return CanEnterSemiSleep(binding, true);
        }

        private static bool CanEnterSemiSleep(WindowBinding binding, bool requireAutomaticPolicy)
        {
            if (binding == null
                || !binding.supportsSemiSleep
                || !binding.allowSemiSleep
                || binding.busyCount > 0
                || binding.window == null
                || focusModeWindowId == binding.window.GetInstanceID()
                || binding.window.docked
                || binding.root == null
                || binding.root.panel == null)
                return false;
            if (requireAutomaticPolicy && (!SemiSleepEnabled || binding.pinned))
                return false;
            string typeName = binding.window.GetType().Name;
            return typeName.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) < 0
                && typeName.IndexOf("Popup", StringComparison.OrdinalIgnoreCase) < 0
                && typeName.IndexOf("Picker", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsTransientFocusWindow(EditorWindow window)
        {
            if (window == null)
                return false;
            string typeName = window.GetType().Name;
            return typeName.IndexOf("ObjectSelector", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Dialog", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Popup", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Picker", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void UpdateSemiSleepWindows()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!semiSleepAnyAnimating && now < nextSemiSleepIdleCheckAt)
                return;

            bool hasAnimation = false;
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (!CanEnterSemiSleep(binding, false))
                {
                    RestoreSemiSleep(binding, true);
                    continue;
                }

                bool focused = ReferenceEquals(EditorWindow.focusedWindow, binding.window);
                if (focused)
                {
                    binding.focusLostAt = -1d;
                }
                else if (IsSemiSleepEligible(binding)
                    && IsTransientFocusWindow(EditorWindow.focusedWindow))
                {
                    binding.focusLostAt = now;
                }
                else if (IsSemiSleepEligible(binding)
                    && !binding.semiSleeping
                    && !binding.semiSleepAnimating)
                {
                    binding.awakeBounds = binding.window.position;
                    if (binding.focusLostAt < 0d)
                        binding.focusLostAt = now;
                    else if (focusModeWindowId != 0 || now - binding.focusLostAt >= SemiSleepDelay)
                    {
                        if (focusModeWindowId != 0)
                            binding.focusModeForcedSleep = true;
                        binding.semiSleepManualHold = false;
                        BeginSemiSleepTransition(binding, true);
                    }
                }

                if (binding.semiSleepAnimating)
                {
                    UpdateSemiSleepTransition(binding, now);
                    hasAnimation = hasAnimation || binding.semiSleepAnimating;
                }
            }

            semiSleepAnyAnimating = hasAnimation;
            nextSemiSleepIdleCheckAt = now + (hasAnimation ? 0.016d : 0.10d);
        }

        private static void BeginSemiSleepTransition(WindowBinding binding, bool sleep)
        {
            if (binding?.window == null || binding.semiSleepTarget == sleep && binding.semiSleepAnimating)
                return;
            if (!sleep && !binding.semiSleeping && !binding.semiSleepAnimating)
                return;

            if (sleep && !binding.semiSleeping)
            {
                binding.awakeBounds = binding.window.position;
                binding.awakeMinSize = binding.window.minSize;
                binding.awakeMaxSize = binding.window.maxSize;
                binding.window.minSize = new Vector2(80f, 80f);
                binding.semiSleepSlot = AcquireSemiSleepSlot(binding);
            }

            binding.semiSleepTarget = sleep;
            binding.semiSleepAnimating = MotionEnabled;
            semiSleepAnyAnimating = semiSleepAnyAnimating || binding.semiSleepAnimating;
            binding.semiSleepStartedAt = EditorApplication.timeSinceStartup;
            binding.semiSleepFromBounds = binding.window.position;
            binding.semiSleepToBounds = sleep
                ? binding.hasSemiSleepDockBounds
                    ? ClampSemiSleepDockBounds(
                        binding.semiSleepDockBounds,
                        GetSemiSleepTrayBounds(binding.awakeBounds))
                    : EvaluateSemiSleepTarget(
                        GetSemiSleepTrayBounds(binding.awakeBounds),
                        binding.semiSleepSlot)
                : binding.awakeBounds;
            ShowSemiSleepOverlay(binding, sleep || binding.semiSleeping, sleep ? 0f : 1f);
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
            if (!binding.semiSleepAnimating)
                CompleteSemiSleepTransition(binding);
        }

        private static void UpdateSemiSleepTransition(WindowBinding binding, double now)
        {
            float progress = Mathf.Clamp01((float)((now - binding.semiSleepStartedAt) / SemiSleepDuration));
            try
            {
                binding.window.position = EvaluateSemiSleepFrame(
                    binding.semiSleepFromBounds,
                    binding.semiSleepToBounds,
                    progress,
                    !binding.semiSleepTarget,
                    MotionIntensity);
                float overlayOpacity = binding.semiSleepTarget
                    ? Mathf.Clamp01((progress - 0.36f) / 0.64f)
                    : 1f - Mathf.Clamp01(progress / 0.42f);
                ShowSemiSleepOverlay(binding, true, overlayOpacity);
                binding.window.Repaint();
                if (progress >= 1f)
                    CompleteSemiSleepTransition(binding);
            }
            catch (Exception exception) when (
                exception is MissingReferenceException
                || exception is NullReferenceException
                || exception is InvalidOperationException)
            {
                RestoreSemiSleep(binding, false);
            }
        }

        private static void CompleteSemiSleepTransition(WindowBinding binding)
        {
            if (binding?.window == null)
                return;
            binding.window.position = binding.semiSleepToBounds;
            binding.semiSleeping = binding.semiSleepTarget;
            binding.semiSleepAnimating = false;
            if (binding.semiSleeping)
            {
                ShowSemiSleepOverlay(binding, true, 1f);
            }
            else
            {
                binding.semiSleepSlot = -1;
                binding.window.minSize = binding.awakeMinSize;
                binding.window.maxSize = binding.awakeMaxSize;
                ShowSemiSleepOverlay(binding, false, 0f);
                ESWindowOpeningSweep.Replay(binding.root);
                BeginWindowPulse(binding, ESStatusKind.Ready);
            }
            RefreshSemiSleepControls(binding);
            RefreshSemiSleepUpdateSubscription();
        }

        private static void ShowSemiSleepOverlay(
            WindowBinding binding,
            bool visible,
            float opacity)
        {
            if (binding?.semiSleepOverlay == null)
                return;
            binding.semiSleepOverlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            binding.semiSleepOverlay.style.opacity = Mathf.Clamp01(opacity);
            if (visible)
                binding.semiSleepOverlay.BringToFront();
        }

        private static void RestoreSemiSleep(WindowBinding binding, bool restoreBounds)
        {
            if (binding == null)
                return;
            bool hadState = binding.semiSleeping || binding.semiSleepAnimating;
            VisualElement dragOverlay = binding.semiSleepOverlay;
            int dragPointerId = binding.semiSleepDragPointerId;
            if (dragOverlay != null
                && dragPointerId >= 0
                && dragOverlay.HasPointerCapture(dragPointerId))
                dragOverlay.ReleasePointer(dragPointerId);
            binding.semiSleeping = false;
            binding.semiSleepAnimating = false;
            binding.semiSleepTarget = false;
            binding.semiSleepManualHold = false;
            binding.semiSleepSlot = -1;
            binding.focusLostAt = -1d;
            ResetSemiSleepDrag(binding);
            ShowSemiSleepOverlay(binding, false, 0f);
            RefreshSemiSleepControls(binding);
            if (!hadState || binding.window == null)
                return;
            try
            {
                binding.window.minSize = binding.awakeMinSize;
                binding.window.maxSize = binding.awakeMaxSize;
                if (restoreBounds && !binding.window.docked)
                    binding.window.position = binding.awakeBounds;
            }
            catch (Exception exception) when (
                exception is MissingReferenceException
                || exception is NullReferenceException
                || exception is InvalidOperationException)
            {
            }
        }

        private static void RestoreAllSemiSleepWindows()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                RestoreSemiSleep(binding, true);
            semiSleepAnyAnimating = false;
        }

        private static void RestoreAutomaticSemiSleepWindows()
        {
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null && !binding.semiSleepManualHold)
                    RestoreSemiSleep(binding, true);
            semiSleepAnyAnimating = HasSemiSleepRuntimeState();
        }

        private static int AcquireSemiSleepSlot(WindowBinding requested)
        {
            var used = new HashSet<int>();
            foreach (WindowBinding binding in windowBindings.Values)
                if (binding != null
                    && binding != requested
                    && (binding.semiSleeping || binding.semiSleepAnimating && binding.semiSleepTarget)
                    && binding.semiSleepSlot >= 0)
                    used.Add(binding.semiSleepSlot);
            int slot = 0;
            while (used.Contains(slot))
                slot++;
            return slot;
        }

        internal static Rect EvaluateSemiSleepTarget(Rect trayBounds, int slot)
        {
            int safeSlot = Mathf.Max(0, slot);
            float availableWidth = Mathf.Max(SemiSleepSize, trayBounds.width - SemiSleepTrayMargin * 2f);
            int columns = Mathf.Max(
                1,
                Mathf.FloorToInt((availableWidth + SemiSleepTrayGap)
                    / (SemiSleepSize + SemiSleepTrayGap)));
            int column = safeSlot % columns;
            int row = safeSlot / columns;
            return new Rect(
                trayBounds.xMax - SemiSleepTrayMargin - SemiSleepSize
                    - column * (SemiSleepSize + SemiSleepTrayGap),
                trayBounds.yMax - SemiSleepTrayMargin - SemiSleepSize
                    - row * (SemiSleepSize + SemiSleepTrayGap),
                SemiSleepSize,
                SemiSleepSize);
        }

        internal static Rect ClampSemiSleepDockBounds(Rect bounds, Rect trayBounds)
        {
            float width = Mathf.Min(Mathf.Max(1f, bounds.width), Mathf.Max(1f, trayBounds.width));
            float height = Mathf.Min(Mathf.Max(1f, bounds.height), Mathf.Max(1f, trayBounds.height));
            float minX = trayBounds.xMin;
            float maxX = Mathf.Max(minX, trayBounds.xMax - width);
            float minY = trayBounds.yMin;
            float maxY = Mathf.Max(minY, trayBounds.yMax - height);
            return new Rect(
                Mathf.Clamp(bounds.x, minX, maxX),
                Mathf.Clamp(bounds.y, minY, maxY),
                width,
                height);
        }

        internal static Rect EvaluateSemiSleepDragFrame(
            Rect current,
            Vector2 pointerDelta,
            Rect trayBounds)
        {
            if (!IsFinite(pointerDelta)
                || !IsFinite(current.position)
                || !IsFinite(current.size)
                || !IsFinite(trayBounds.position)
                || !IsFinite(trayBounds.size))
                return current;

            Vector2 safeDelta = Vector2.ClampMagnitude(pointerDelta, SemiSleepMaxPointerDelta);
            Rect target = current;
            target.position += safeDelta;
            return ClampSemiSleepDockBounds(target, trayBounds);
        }

        private static bool IsFinite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }

        private static Rect GetSemiSleepTrayBounds(Rect fallback)
        {
            try
            {
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                if (main.width >= SemiSleepSize && main.height >= SemiSleepSize)
                    return main;
            }
            catch (Exception exception) when (
                exception is MissingMethodException
                || exception is InvalidOperationException)
            {
            }
            return fallback.width >= SemiSleepSize && fallback.height >= SemiSleepSize
                ? fallback
                : new Rect(fallback.position, new Vector2(SemiSleepSize, SemiSleepSize));
        }

        private static WindowBinding FindBindingByRoot(VisualElement element)
        {
            if (element == null)
                return null;
            return windowBindingsByRoot.TryGetValue(element, out WindowBinding binding)
                ? binding
                : null;
        }

        private static void UpdateWindowOverlay(WindowBinding binding)
        {
            if (!IsWindowOverlayAttached(binding))
            {
                binding?.animation?.Pause();
                return;
            }

            try
            {
                float pulse = EvaluatePulse(binding.pulseStartedAt, binding.pulseDuration);
                if (pulse <= 0f)
                {
                    binding.animation?.Pause();
                    binding.sweep.style.width = 0f;
                    binding.accentLine.style.backgroundColor =
                        binding.activityState == ESWindowActivityState.Attention
                            ? GetStatusAccent(0, binding.pulseStatus)
                            : GetDepthAccent(0);
                    return;
                }

                Color accent = GetStatusAccent(0, binding.pulseStatus);
                accent.a = Mathf.Clamp01(0.62f + pulse * 0.34f);
                binding.accentLine.style.backgroundColor = accent;

                float progress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - binding.pulseStartedAt) / binding.pulseDuration));
                float hostWidth = binding.host.resolvedStyle.width;
                if (float.IsNaN(hostWidth) || float.IsInfinity(hostWidth) || hostWidth <= 0f)
                    hostWidth = 180f;
                float width = Mathf.Clamp(hostWidth * 0.16f, 24f, 180f);
                binding.sweep.style.width = width;
                binding.sweep.style.left = Mathf.Lerp(-width, hostWidth, progress);
                Color sweepColor = accent;
                sweepColor.a = Mathf.Clamp01(0.10f + pulse * 0.24f);
                binding.sweep.style.backgroundColor = sweepColor;
                binding.window.Repaint();
            }
            catch (NullReferenceException)
            {
                // Unity may invalidate an internal InlineStyleAccess between DetachFromPanel and
                // the scheduled callback. Stop this local animation; the window content remains intact.
                binding.animation?.Pause();
            }
        }

        private static bool IsWindowOverlayAttached(WindowBinding binding)
        {
            return binding != null
                && binding.window != null
                && binding.root != null
                && binding.root.panel != null
                && binding.host != null
                && binding.host.panel != null
                && ReferenceEquals(binding.host.parent, binding.root)
                && binding.accentLine != null
                && binding.sweep != null;
        }

        /// <summary>Normalized global motion strength used by all ES editor surfaces.</summary>
        public static float MotionIntensity
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null ? 0.78f : Mathf.Clamp01(current.motionIntensity);
            }
        }

        /// <summary>
        /// Returns a one-shot, allocation-free pulse value. Callers can request a repaint only
        /// while this value is non-zero; no global per-frame repaint loop is installed here.
        /// </summary>
        public static float EvaluatePulse(double startedAt, float duration = 0.42f)
        {
            if (!MotionEnabled || startedAt <= 0d || duration <= 0.001f)
                return 0f;

            double elapsed = EditorApplication.timeSinceStartup - startedAt;
            if (elapsed <= 0d || elapsed >= duration)
                return 0f;

            float normalized = Mathf.Clamp01((float)(elapsed / duration));
            return Mathf.Sin(normalized * Mathf.PI) * MotionIntensity;
        }

        /// <summary>
        /// Returns a subtle looping breath value for a focused/selected surface. It is intended
        /// for a single local highlight, never for animating an entire large editor tree.
        /// </summary>
        public static float EvaluateBreath(double now = -1d, float period = 1.6f)
        {
            if (!MotionEnabled || period <= 0.05f)
                return 0f;

            if (now < 0d)
                now = EditorApplication.timeSinceStartup;

            float phase = Mathf.Repeat((float)(now / period), 1f) * Mathf.PI * 2f;
            return (0.5f + 0.5f * Mathf.Sin(phase)) * MotionIntensity;
        }

        /// <summary>Blends a base color toward an ES accent without changing semantic status.</summary>
        public static Color GetMotionColor(Color baseColor, Color accent, float amount)
        {
            float strength = Mathf.Clamp01(amount) * MotionIntensity;
            return Color.Lerp(baseColor, accent, strength);
        }

        /// <summary>
        /// Draws a one-shot feedback frame for IMGUI surfaces. Returns true while the effect is
        /// active so the owning window can schedule a local repaint.
        /// </summary>
        public static bool DrawFeedbackFrame(
            Rect rect,
            ESStatusKind status,
            int depth,
            double startedAt,
            float duration = 0.42f,
            float thickness = 1f)
        {
            float pulse = EvaluatePulse(startedAt, duration);
            if (pulse <= 0f || Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return false;

            Color accent = GetStatusAccent(depth, status);
            accent.a = Mathf.Clamp01(0.28f + pulse * 0.52f);
            DrawFrame(rect, accent, thickness);
            return true;
        }

        /// <summary>
        /// Draws a restrained horizontal sweep used for save/preview/selection feedback. The
        /// caller owns the animation start time and should repaint only the local view.
        /// </summary>
        public static bool DrawFeedbackSweep(
            Rect rect,
            Color accent,
            double startedAt,
            float duration = 0.60f,
            float widthRatio = 0.18f)
        {
            float pulse = EvaluatePulse(startedAt, duration);
            if (pulse <= 0f || Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return false;

            float sweepProgress = Mathf.Clamp01((float)((EditorApplication.timeSinceStartup - startedAt) / duration));
            float sweepWidth = Mathf.Clamp(rect.width * widthRatio, 6f, 96f);
            float x = Mathf.Lerp(rect.x - sweepWidth, rect.xMax, sweepProgress);
            Color sweepColor = accent;
            sweepColor.a = Mathf.Clamp01(0.06f + pulse * 0.18f);
            EditorGUI.DrawRect(new Rect(x, rect.y, sweepWidth, rect.height), sweepColor);
            return true;
        }

        public static Color SectionSelectedFill
        {
            get
            {
                EnsureSkin();
                return GetSelectionFill(cachedProSkin);
            }
        }

        public static Color GetSelectionFill(bool proSkin)
        {
            return proSkin
                ? new Color(0.18f, 0.32f, 0.46f, 0.34f)
                : new Color(0.72f, 0.84f, 0.96f, 0.55f);
        }

        public static Color SectionTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.88f, 0.91f, 0.96f, 1f)
                    : new Color(0.28f, 0.30f, 0.33f, 1f);
            }
        }

        public static Color SectionSelectedTextColor
        {
            get { return GetDepthAccent(0); }
        }

        public static Color SectionMutedTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.64f, 0.69f, 0.77f, 1f)
                    : new Color(0.50f, 0.52f, 0.55f, 1f);
            }
        }

        public static Color SectionMarkerColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.42f, 0.45f, 0.49f, 1f)
                    : new Color(0.54f, 0.57f, 0.60f, 1f);
            }
        }

        public static Color WarningBackground
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.33f, 0.22f, 0.16f, 0.90f)
                    : new Color(1f, 0.92f, 0.84f, 1f);
            }
        }

        public static Color NeutralSelectorBackground
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.25f, 0.26f, 0.28f, 0.90f)
                    : new Color(0.88f, 0.89f, 0.90f, 1f);
            }
        }

        public static Color NeutralHoverColor
        {
            get { return new Color(0.48f, 0.51f, 0.55f, 1f); }
        }

        public static Color WarningTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(1f, 0.66f, 0.35f, 1f)
                    : new Color(0.72f, 0.29f, 0.05f, 1f);
            }
        }

        public static Color EmptyTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.74f, 0.78f, 0.85f, 1f)
                    : new Color(0.38f, 0.41f, 0.45f, 1f);
            }
        }

        public static Color SelectedTextColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.62f, 0.80f, 1f, 1f)
                    : new Color(0.06f, 0.31f, 0.61f, 1f);
            }
        }

        public static Color SelectorArrowColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.59f, 0.62f, 0.66f, 1f)
                    : new Color(0.39f, 0.42f, 0.46f, 1f);
            }
        }

        public static Color ClearActionColor
        {
            get
            {
                EnsureSkin();
                return cachedProSkin
                    ? new Color(0.65f, 0.48f, 0.48f, 1f)
                    : new Color(0.62f, 0.28f, 0.28f, 1f);
            }
        }

        // Semantic surface and interaction tokens shared by ES windows. Consumers should use
        // these accessors instead of embedding graph-specific RGB values in their view code.
        public static Color WindowSurfaceColor => GetDepthBackground(0);
        public static Color WindowRaisedSurfaceColor => GetDepthBackground(1);
        public static Color WindowInsetSurfaceColor => GetDepthBackground(2);
        public static Color CanvasSurfaceColor => GetDepthBackground(3);
        public static Color ToolbarSurfaceColor => GetSelectorBackground(0);
        public static Color ControlSurfaceColor => GetSelectorBackground(1);
        public static Color SelectionColor => GetDepthAccent(0);
        public static Color ActiveColor => GetStatusAccent(0, ESStatusKind.Ready);
        public static Color DisabledColor => GetStatusAccent(0, ESStatusKind.ReadOnly);
        public static Color WarningColor => GetStatusAccent(0, ESStatusKind.Warning);
        public static Color ErrorColor => GetStatusAccent(0, ESStatusKind.Error);
        public static Color NodeBorderColor => GetStatusFrameColor(1, ESStatusKind.None);
        public static Color NodeSelectedBorderColor => GetStatusFrameColor(0, ESStatusKind.Modified);

        public static Color GetSemanticAccent(int paletteIndex)
        {
            Color color;
            switch (paletteIndex)
            {
                case 1: color = new Color(0.25f, 0.55f, 0.96f); break;
                case 2: color = new Color(0.30f, 0.72f, 0.46f); break;
                case 3: color = new Color(0.82f, 0.38f, 0.38f); break;
                case 4: color = new Color(0.32f, 0.74f, 0.45f); break;
                case 5: color = new Color(0.86f, 0.34f, 0.36f); break;
                case 6: color = new Color(0.95f, 0.63f, 0.22f); break;
                case 7: color = new Color(0.28f, 0.72f, 0.72f); break;
                case 8: color = new Color(0.35f, 0.62f, 0.90f); break;
                case 9: color = new Color(0.48f, 0.58f, 0.86f); break;
                case 10: color = new Color(0.28f, 0.75f, 0.72f); break;
                case 11: color = new Color(0.95f, 0.63f, 0.22f); break;
                case 12: color = new Color(0.65f, 0.43f, 0.94f); break;
                case 13: color = new Color(0.83f, 0.39f, 0.72f); break;
                case 14: color = new Color(0.35f, 0.78f, 0.43f); break;
                default: color = new Color(0.42f, 0.48f, 0.58f); break;
            }

            Color themeAccent = GetDepthAccent(Mathf.Abs(paletteIndex) % 3);
            color = Color.Lerp(color, themeAccent, 0.12f);
            color.a = 1f;
            return color;
        }

        public static Color NormalizeSemanticAccent(Color requested, int fallbackPaletteIndex)
        {
            if (requested.a <= 0f)
                return GetSemanticAccent(fallbackPaletteIndex);
            Color normalized = Color.Lerp(requested, GetDepthAccent(0), 0.12f);
            normalized.a = 1f;
            return normalized;
        }

        public static Color GetSemanticChannelColor(int channel)
        {
            switch (channel)
            {
                case 1: return GetSemanticAccent(0);
                case 2: return Color.Lerp(DisabledColor, SectionTextColor, 0.45f);
                case 3: return Color.Lerp(WarningColor, ErrorColor, 0.38f);
                case 4: return GetSemanticAccent(4);
                case 5: return GetSemanticAccent(8);
                case 6: return GetSemanticAccent(12);
                case 7: return GetSemanticAccent(1);
                case 8: return GetSemanticAccent(6);
                case 9: return GetSemanticAccent(13);
                default: return Color.Lerp(DisabledColor, SectionMutedTextColor, 0.48f);
            }
        }

        public static Texture2D SurfaceTexture
        {
            get
            {
                EnsureSkin();
                if (surfaceTexture == null)
                {
                    Color borderColor = cachedProSkin
                        ? new Color(0.28f, 0.34f, 0.42f, 1f)
                        : new Color(0.58f, 0.61f, 0.64f, 1f);
                    Color fillColor = cachedProSkin
                        ? new Color(0.13f, 0.17f, 0.23f, 1f)
                        : new Color(0.91f, 0.92f, 0.93f, 1f);

                    // Keep the constructor version-neutral. Unity versions differ in the
                    // TextureFormat/creation-flags overloads available to editor assemblies.
                    surfaceTexture = new Texture2D(3, 3)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = "ESEditorPresentationSurface"
                    };

                    for (int y = 0; y < 3; y++)
                    {
                        for (int x = 0; x < 3; x++)
                            surfaceTexture.SetPixel(x, y, x == 1 && y == 1 ? fillColor : borderColor);
                    }

                    surfaceTexture.Apply(false, true);
                }

                return surfaceTexture;
            }
        }

        private static Texture2D CompactCollectionBodyTexture
        {
            get
            {
                EnsureSkin();
                if (compactCollectionBodyTexture == null)
                {
                    compactCollectionBodyTexture = new Texture2D(1, 1)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        name = "ESEditorPresentationCompactCollectionBody"
                    };
                    compactCollectionBodyTexture.SetPixel(
                        0,
                        0,
                        cachedProSkin
                            ? new Color(0.16f, 0.17f, 0.19f, 0.96f)
                            : new Color(0.94f, 0.945f, 0.95f, 1f));
                    compactCollectionBodyTexture.Apply(false, true);
                }

                return compactCollectionBodyTexture;
            }
        }

        public static float GetDepthProgress(int depth)
        {
            if (depth <= 0)
                return 0f;

            // The first nested level must be visually obvious; later levels converge quickly
            // so deep data remains readable instead of becoming nearly black.
            return Mathf.Clamp01(0.28f + (depth - 1) * 0.38f);
        }

        public static Color GetDepthAccent(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            ESGlobalEditorTheme current = CurrentTheme;
            Color start = current != null && current.useCustomPalette
                ? (cachedProSkin ? current.darkAccentStart : current.lightAccentStart)
                : cachedProSkin
                    ? new Color(0.48f, 0.78f, 1f, 0.92f)
                    : new Color(0.12f, 0.46f, 0.82f, 0.92f);
            Color end = current != null && current.useCustomPalette
                ? (cachedProSkin ? current.darkAccentEnd : current.lightAccentEnd)
                : cachedProSkin
                    ? new Color(0.13f, 0.42f, 0.72f, 0.96f)
                    : new Color(0.04f, 0.24f, 0.56f, 0.96f);
            return Color.Lerp(start, end, progress);
        }

        public static Color GetDepthBackground(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            return cachedProSkin
                ? Color.Lerp(
                    new Color(0.12f, 0.15f, 0.19f, 0.98f),
                    new Color(0.025f, 0.055f, 0.10f, 0.99f),
                    progress)
                : Color.Lerp(
                    new Color(0.96f, 0.96f, 0.97f, 1f),
                    new Color(0.76f, 0.86f, 0.95f, 1f),
                    progress);
        }

        public static Color GetSelectorBackground(int depth)
        {
            EnsureSkin();
            float progress = GetDepthProgress(depth);
            return cachedProSkin
                ? Color.Lerp(
                    new Color(0.08f, 0.14f, 0.22f, 0.94f),
                    new Color(0.04f, 0.08f, 0.13f, 0.96f),
                    progress)
                : Color.Lerp(
                    new Color(0.88f, 0.93f, 0.98f, 1f),
                    new Color(0.70f, 0.82f, 0.94f, 1f),
                    progress);
        }

        public static Color GetStatusFrameColor(int depth, ESStatusKind status)
        {
            EnsureSkin();
            if (status == ESStatusKind.Error)
            {
                Color error = GetStatusAccent(depth, status);
                error.a = 0.78f;
                return error;
            }

            if (status == ESStatusKind.Warning)
            {
                Color warning = GetStatusAccent(depth, status);
                warning.a = 0.82f;
                return warning;
            }

            if (status == ESStatusKind.Empty || status == ESStatusKind.None)
                return cachedProSkin
                    ? new Color(0.43f, 0.45f, 0.49f, 0.72f)
                    : new Color(0.62f, 0.65f, 0.69f, 0.78f);

            Color accent = GetStatusAccent(depth, status);
            accent.a = cachedProSkin ? 0.72f : 0.64f;
            return accent;
        }

        public static Color GetFieldLevelAccent(ESFieldLevel level)
        {
            EnsureSkin();
            switch (level)
            {
                case ESFieldLevel.Core:
                    return GetDepthAccent(0);
                case ESFieldLevel.Important:
                    return GetDepthAccent(1);
                default:
                    return GetStatusFrameColor(0, ESStatusKind.None);
            }
        }

        public static void StyleField(VisualElement field, Label label, ESFieldLevel level,
            bool required, bool empty, string hint)
        {
            if (field == null)
                return;
            string levelText = level == ESFieldLevel.Core ? "核心"
                : level == ESFieldLevel.Important ? "重点" : string.Empty;
            if (label != null)
            {
                string clean = label.text ?? string.Empty;
                if (!string.IsNullOrEmpty(levelText)
                    && !clean.StartsWith(levelText + " · ", StringComparison.Ordinal))
                    clean = levelText + " · " + clean;
                if (required && !clean.EndsWith(" *", StringComparison.Ordinal))
                    clean += " *";
                if (!string.Equals(label.text, clean, StringComparison.Ordinal))
                    label.text = clean;
                if (level != ESFieldLevel.Normal)
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            Color accent = required && empty
                ? GetStatusAccent(0, ESStatusKind.Error)
                : GetFieldLevelAccent(level);
            if (level != ESFieldLevel.Normal || required && empty)
            {
                field.style.borderLeftWidth = level == ESFieldLevel.Core ? 3f : 2f;
                field.style.borderLeftColor = accent;
                field.style.paddingLeft = 4f;
                Color background = accent;
                background.a = EditorGUIUtility.isProSkin ? 0.075f : 0.045f;
                field.style.backgroundColor = background;
            }

            string metaText = (string.IsNullOrEmpty(levelText) ? string.Empty : levelText)
                + (required ? (string.IsNullOrEmpty(levelText) ? "必填" : " · 必填") : string.Empty);
            string tooltip = BuildFieldTooltip(metaText, hint, field.tooltip);
            if (!string.Equals(field.tooltip, tooltip, StringComparison.Ordinal))
                field.tooltip = tooltip;
        }

        private static string BuildFieldTooltip(string metaText, string hint, string existing)
        {
            string result = existing ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(hint))
            {
                string normalizedHint = hint.Trim();
                if (!ContainsTooltipLine(result, normalizedHint))
                    result = string.IsNullOrEmpty(result)
                        ? normalizedHint
                        : normalizedHint + "\n" + result;
            }

            if (!string.IsNullOrEmpty(metaText) && !ContainsTooltipLine(result, metaText))
                result = string.IsNullOrEmpty(result) ? metaText : metaText + "\n" + result;
            return result;
        }

        private static bool ContainsTooltipLine(string tooltip, string line)
        {
            if (string.IsNullOrEmpty(tooltip) || string.IsNullOrEmpty(line))
                return false;
            int searchFrom = 0;
            while (searchFrom <= tooltip.Length - line.Length)
            {
                int index = tooltip.IndexOf(line, searchFrom, StringComparison.Ordinal);
                if (index < 0)
                    return false;
                int end = index + line.Length;
                bool startsAtLine = index == 0 || tooltip[index - 1] == '\n';
                bool endsAtLine = end == tooltip.Length || tooltip[end] == '\n' || tooltip[end] == '\r';
                if (startsAtLine && endsAtLine)
                    return true;
                searchFrom = index + 1;
            }
            return false;
        }

        public static Color GetStatusAccent(int depth, ESStatusKind status)
        {
            EnsureSkin();
            ESGlobalEditorTheme current = CurrentTheme;
            if (status == ESStatusKind.Error)
                return current != null && current.useCustomPalette
                    ? (cachedProSkin ? current.darkError : current.lightError)
                    : new Color(0.92f, 0.40f, 0.24f, 0.96f);

            if (status == ESStatusKind.Warning)
                return current != null && current.useCustomPalette
                    ? (cachedProSkin ? current.darkWarning : current.lightWarning)
                    : new Color(0.90f, 0.68f, 0.24f, 0.96f);

            if (status == ESStatusKind.Modified)
                return new Color(0.36f, 0.70f, 0.98f, 0.96f);

            if (status == ESStatusKind.ReadOnly)
                return cachedProSkin
                    ? new Color(0.50f, 0.54f, 0.60f, 0.86f)
                    : new Color(0.45f, 0.49f, 0.54f, 0.86f);

            if (status == ESStatusKind.Empty || status == ESStatusKind.None)
                return cachedProSkin
                    ? new Color(0.43f, 0.45f, 0.49f, 0.72f)
                    : new Color(0.62f, 0.65f, 0.69f, 0.78f);

            return GetDepthAccent(depth);
        }

        public static void DrawCompactCollectionHeaderBackground(
            Rect rect,
            int depth,
            ESStatusKind status,
            bool expanded)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            EnsureSkin();
            Color background = cachedProSkin
                ? new Color(0.205f, 0.215f, 0.235f, 0.98f)
                : new Color(0.885f, 0.895f, 0.91f, 1f);
            background = Color.Lerp(background, GetDepthBackground(depth), 0.22f);

            Color accent = status == ESStatusKind.Error || status == ESStatusKind.Warning
                ? GetStatusAccent(depth, status)
                : GetDepthAccent(depth);
            if (status == ESStatusKind.Error || status == ESStatusKind.Warning)
                background = Color.Lerp(background, accent, cachedProSkin ? 0.14f : 0.09f);

            EditorGUI.DrawRect(rect, background);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, Metric(4f), rect.height), accent);

            Color edge = GetStatusFrameColor(depth, status);
            edge.a = cachedProSkin ? 0.74f : 0.66f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), edge);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), edge);

            if (!expanded)
                return;

            Color openEdge = accent;
            openEdge.a = cachedProSkin ? 0.48f : 0.38f;
            EditorGUI.DrawRect(new Rect(rect.x + Metric(4f), rect.yMax - 2f, rect.width - Metric(4f), 1f), openEdge);
        }

        public static void DrawFrame(Rect rect, Color color, float thickness = 1f)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0f || rect.height <= 0f)
                return;

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        public static void DrawDivider(Rect rect)
        {
            if (Event.current.type == EventType.Repaint && rect.width > 0f && rect.height > 0f)
                EditorGUI.DrawRect(rect, DividerColor);
        }

        private static bool BrandTypographyEnabled
        {
            get
            {
                ESGlobalEditorTheme current = CurrentTheme;
                return current == null || current.enableBrandTypography;
            }
        }

        private static void ApplyBrandFont(GUIStyle style)
        {
            if (style == null || !BrandTypographyEnabled)
                return;

            if (!brandFontLoadAttempted)
            {
                brandFontLoadAttempted = true;
                brandFont = Resources.Load<Font>(BrandFontResourcePath);
            }
            if (brandFont != null)
                style.font = brandFont;
        }

        private static void ApplyBrandTypography(VisualElement root)
        {
            if (root == null)
                return;

            if (brandTypographyStyleSheet == null)
                brandTypographyStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(BrandTypographyStyleSheetPath);
            if (brandTypographyStyleSheet != null && !root.styleSheets.Contains(brandTypographyStyleSheet))
                root.styleSheets.Add(brandTypographyStyleSheet);
            root.EnableInClassList("es-brand-typography", BrandTypographyEnabled);
        }

        private static void RemoveBrandTypography(VisualElement root)
        {
            if (root == null)
                return;
            root.RemoveFromClassList("es-brand-typography");
            if (brandTypographyStyleSheet != null && root.styleSheets.Contains(brandTypographyStyleSheet))
                root.styleSheets.Remove(brandTypographyStyleSheet);
        }

        public static void InvalidateSkinCache()
        {
            skinInitialized = false;
        }

        public static void InvalidateTheme()
        {
            themeGeneration++;
            themeInitialized = false;
            theme = null;
            InvalidateSkinCache();
            if (GlobalEditorShellEnabled)
                InstallGlobalEditorAdapters();
            else
            {
                UninstallGlobalEditorAdapterCallbacks();
                UnbindAllWindowBindings();
            }
            QueueDeepSkinSynchronization();
            foreach (WindowBinding binding in windowBindings.Values)
            {
                if (!IsWindowOverlayAttached(binding))
                    continue;

                try
                {
                    ApplyBrandTypography(binding.root);
                    ESWindowPresentation.ApplySemanticTheme(binding.root);
                    Color accent = GetDepthAccent(0);
                    binding.host.style.backgroundColor = accent;
                    binding.accentLine.style.backgroundColor = accent;
                    binding.window.Repaint();
                }
                catch (NullReferenceException)
                {
                    binding.animation?.Pause();
                }
            }
            SceneView.RepaintAll();
            EditorApplication.RepaintHierarchyWindow();
            EditorApplication.RepaintProjectWindow();
        }

        private static ESGlobalEditorTheme CurrentTheme
        {
            get
            {
                if (!themeInitialized)
                {
                    theme = ESGlobalEditorTheme.Instance;
                    themeInitialized = true;
                }

                return theme;
            }
        }

        private static int Metric(float value)
        {
            return Mathf.Max(0, Mathf.RoundToInt(value * Density));
        }

        private static void EnsureSkin()
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            if (skinInitialized && cachedProSkin == proSkin)
                return;

            cachedProSkin = proSkin;
            skinInitialized = true;
            skinGeneration++;
            surfaceStyle = null;
            headerStyle = null;
            subtitleStyle = null;
            metaStyle = null;
            compactCollectionTitleStyle = null;
            compactCollectionMetaStyle = null;
            compactCollectionBodyStyle = null;

            if (surfaceTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(surfaceTexture);
                surfaceTexture = null;
            }

            if (compactCollectionBodyTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(compactCollectionBodyTexture);
                compactCollectionBodyTexture = null;
            }
        }
    }

    /// <summary>
    /// Small UI Toolkit shell shared by ES windows. It intentionally owns only presentation
    /// elements; the content area remains under the caller's scroll and data lifecycle.
    /// </summary>
    internal sealed class ESWindowShell
    {
        internal readonly VisualElement Root;
        internal readonly VisualElement Header;
        internal readonly VisualElement HeaderToolbar;
        internal readonly VisualElement Toolbar;
        internal readonly VisualElement Content;
        internal readonly VisualElement StatusBar;
        internal readonly Label StatusLabel;

        private ESStatusKind status;
        internal ESWindowShell(string title, string subtitle, bool animateOnAttach = true)
        {
            Root = new VisualElement { name = "ESWindowShell" };
            Root.style.flexGrow = 1f;
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.backgroundColor = ESEditorPresentation.WindowSurfaceColor;
            Root.style.transformOrigin = new TransformOrigin(
                Length.Percent(50f),
                Length.Percent(50f),
                0f);

            Header = new VisualElement { name = "ESWindowHeader" };
            Header.style.flexShrink = 0f;
            Header.style.paddingLeft = 14f;
            Header.style.paddingRight = 14f;
            Header.style.paddingTop = 10f;
            Header.style.paddingBottom = 8f;
            Header.style.backgroundColor = ESEditorPresentation.WindowRaisedSurfaceColor;
            Header.style.borderBottomWidth = 1f;
            Header.style.borderBottomColor = ESEditorPresentation.DividerColor;

            VisualElement titleRow = new VisualElement { name = "ESWindowTitleRow" };
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            Label titleLabel = new Label(title ?? "ES 窗口") { name = "ESWindowTitle" };
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.flexGrow = 1f;
            titleLabel.style.minWidth = 0f;
            titleLabel.style.fontSize = 15f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.SectionSelectedTextColor;
            titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            titleLabel.style.overflow = Overflow.Hidden;
            titleLabel.style.textOverflow = TextOverflow.Ellipsis;
            titleRow.Add(titleLabel);

            HeaderToolbar = new VisualElement { name = "ESWindowHeaderToolbar" };
            HeaderToolbar.style.flexShrink = 0f;
            HeaderToolbar.style.flexDirection = FlexDirection.Row;
            HeaderToolbar.style.alignItems = Align.Center;
            HeaderToolbar.style.justifyContent = Justify.FlexEnd;
            HeaderToolbar.style.marginLeft = 10f;
            HeaderToolbar.style.minHeight = 26f;
            titleRow.Add(HeaderToolbar);
            Header.Add(titleRow);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                Label subtitleLabel = new Label(subtitle.Trim()) { name = "ESWindowSubtitle" };
                subtitleLabel.style.marginTop = 2f;
                subtitleLabel.style.fontSize = 10f;
                subtitleLabel.style.color = ESEditorPresentation.SectionMutedTextColor;
                subtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
                subtitleLabel.style.overflow = Overflow.Hidden;
                subtitleLabel.style.textOverflow = TextOverflow.Ellipsis;
                Header.Add(subtitleLabel);
            }

            Root.Add(Header);

            Toolbar = new VisualElement { name = "ESWindowToolbar" };
            Toolbar.style.flexShrink = 0f;
            Toolbar.style.flexDirection = FlexDirection.Row;
            Toolbar.style.flexWrap = Wrap.Wrap;
            Toolbar.style.alignItems = Align.Center;
            Toolbar.style.paddingLeft = 10f;
            Toolbar.style.paddingRight = 10f;
            Toolbar.style.paddingTop = 5f;
            Toolbar.style.paddingBottom = 5f;
            Toolbar.style.backgroundColor = ESEditorPresentation.ToolbarSurfaceColor;
            Toolbar.style.borderBottomWidth = 1f;
            Toolbar.style.borderBottomColor = ESEditorPresentation.DividerColor;
            Root.Add(Toolbar);

            Content = new VisualElement { name = "ESWindowContent" };
            Content.style.flexGrow = 1f;
            Content.style.flexShrink = 1f;
            Content.style.minWidth = 0f;
            Content.style.minHeight = 0f;
            Root.Add(Content);

            StatusBar = new VisualElement { name = "ESWindowStatusBar" };
            StatusBar.style.flexShrink = 0f;
            StatusBar.style.flexDirection = FlexDirection.Row;
            StatusBar.style.alignItems = Align.Center;
            StatusBar.style.minHeight = 24f;
            StatusBar.style.paddingLeft = 10f;
            StatusBar.style.paddingRight = 10f;
            StatusBar.style.backgroundColor = ESEditorPresentation.WindowInsetSurfaceColor;
            StatusBar.style.borderTopWidth = 1f;
            StatusBar.style.borderTopColor = ESEditorPresentation.DividerColor;
            StatusLabel = new Label { name = "ESWindowStatus" };
            StatusLabel.style.flexGrow = 1f;
            StatusLabel.style.minWidth = 0f;
            StatusLabel.style.fontSize = 10f;
            StatusBar.Add(StatusLabel);
            Root.Add(StatusBar);
            SetStatus("就绪", ESStatusKind.Ready);
        }

        internal void SetStatus(string message, ESStatusKind nextStatus)
        {
            status = nextStatus;
            StatusLabel.text = string.IsNullOrWhiteSpace(message) ? "就绪" : message.Trim();
            StatusLabel.style.color = ESEditorPresentation.GetStatusAccent(0, status);
            StatusBar.style.borderLeftWidth = status == ESStatusKind.Error || status == ESStatusKind.Warning ? 3f : 0f;
            StatusBar.style.borderLeftColor = ESEditorPresentation.GetStatusAccent(0, status);
        }
    }

    internal static class ESWindowPresentation
    {
        internal static Button CreateToolbarButton(string text, string tooltip, Action action, bool primary = false)
        {
            Button button = new Button(action) { text = text ?? string.Empty, tooltip = tooltip ?? string.Empty };
            button.AddToClassList("es-window-toolbar-button");
            if (primary)
                button.AddToClassList("primary");
            button.style.minHeight = 24f;
            button.style.marginRight = 4f;
            button.style.marginBottom = 2f;
            button.style.paddingLeft = 10f;
            button.style.paddingRight = 10f;
            button.style.color = ESEditorPresentation.SectionSelectedTextColor;
            button.style.backgroundColor = primary
                ? ESEditorPresentation.SelectionColor
                : ESEditorPresentation.ControlSurfaceColor;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftColor = ESEditorPresentation.DividerColor;
            button.style.borderRightColor = ESEditorPresentation.DividerColor;
            button.style.borderTopColor = ESEditorPresentation.DividerColor;
            button.style.borderBottomColor = ESEditorPresentation.DividerColor;
            return button;
        }

        internal static Button CreateHeaderIconButton(string symbol, string tooltip, Action action)
        {
            Button button = new Button(action)
            {
                text = symbol ?? string.Empty,
                tooltip = tooltip ?? string.Empty
            };
            button.AddToClassList("es-window-header-icon-button");
            button.style.width = 26f;
            button.style.minWidth = 26f;
            button.style.height = 26f;
            button.style.minHeight = 26f;
            button.style.marginLeft = 2f;
            button.style.paddingLeft = 0f;
            button.style.paddingRight = 0f;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            button.style.fontSize = 14f;
            button.style.color = ESEditorPresentation.SectionSelectedTextColor;
            button.style.backgroundColor = ESEditorPresentation.ControlSurfaceColor;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftColor = ESEditorPresentation.DividerColor;
            button.style.borderRightColor = ESEditorPresentation.DividerColor;
            button.style.borderTopColor = ESEditorPresentation.DividerColor;
            button.style.borderBottomColor = ESEditorPresentation.DividerColor;
            return button;
        }

        internal static Button CreateHeaderActionButton(
            Texture icon,
            string text,
            string tooltip,
            Action action)
        {
            Button button = new Button(action) { tooltip = tooltip ?? string.Empty };
            button.AddToClassList("es-window-header-action-button");
            button.style.height = 26f;
            button.style.minHeight = 26f;
            button.style.minWidth = string.IsNullOrEmpty(text) ? 26f : 32f;
            button.style.flexDirection = FlexDirection.Row;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.style.marginLeft = 2f;
            button.style.paddingLeft = 6f;
            button.style.paddingRight = 6f;
            button.style.color = ESEditorPresentation.SectionSelectedTextColor;
            button.style.backgroundColor = ESEditorPresentation.ControlSurfaceColor;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftColor = ESEditorPresentation.DividerColor;
            button.style.borderRightColor = ESEditorPresentation.DividerColor;
            button.style.borderTopColor = ESEditorPresentation.DividerColor;
            button.style.borderBottomColor = ESEditorPresentation.DividerColor;

            if (icon != null)
            {
                Image image = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.style.width = 15f;
                image.style.height = 15f;
                image.style.flexShrink = 0f;
                button.Add(image);
            }

            if (!string.IsNullOrEmpty(text))
            {
                Label label = new Label(text) { pickingMode = PickingMode.Ignore };
                label.style.marginLeft = icon == null ? 0f : 4f;
                label.style.whiteSpace = WhiteSpace.NoWrap;
                label.style.color = ESEditorPresentation.SectionSelectedTextColor;
                button.Add(label);
            }
            return button;
        }

        internal static VisualElement CreateEmptyState(string title, string detail, string actionText, Action action)
        {
            VisualElement empty = new VisualElement { name = "ESEmptyState" };
            empty.style.flexGrow = 1f;
            empty.style.alignItems = Align.Center;
            empty.style.justifyContent = Justify.Center;
            empty.style.paddingLeft = 24f;
            empty.style.paddingRight = 24f;
            empty.style.paddingTop = 24f;
            empty.style.paddingBottom = 24f;
            Label titleLabel = new Label(title ?? "暂无内容") { name = "ESEmptyStateTitle" };
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.fontSize = 14f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.SectionSelectedTextColor;
            empty.Add(titleLabel);
            if (!string.IsNullOrWhiteSpace(detail))
            {
                Label detailLabel = new Label(detail.Trim()) { name = "ESEmptyStateDetail" };
                detailLabel.style.marginTop = 5f;
                detailLabel.style.color = ESEditorPresentation.EmptyTextColor;
                detailLabel.style.whiteSpace = WhiteSpace.Normal;
                detailLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.Add(detailLabel);
            }
            if (!string.IsNullOrWhiteSpace(actionText) && action != null)
            {
                Button actionButton = CreateToolbarButton(actionText, actionText, action, true);
                actionButton.style.marginTop = 12f;
                empty.Add(actionButton);
            }
            return empty;
        }

        internal static VisualElement CreateErrorState(
            string title,
            string cause,
            string impact,
            string recovery,
            string actionText,
            Action action)
        {
            VisualElement error = new VisualElement { name = "ESErrorState" };
            error.style.flexGrow = 1f;
            error.style.alignItems = Align.Center;
            error.style.justifyContent = Justify.Center;
            error.style.paddingLeft = 28f;
            error.style.paddingRight = 28f;
            error.style.paddingTop = 24f;
            error.style.paddingBottom = 24f;

            Label titleLabel = new Label(title ?? "操作失败") { name = "ESErrorStateTitle" };
            titleLabel.AddToClassList("es-brand-title");
            titleLabel.style.fontSize = 14f;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = ESEditorPresentation.ErrorColor;
            error.Add(titleLabel);

            AddErrorLine(error, "原因", cause);
            AddErrorLine(error, "影响", impact);
            AddErrorLine(error, "恢复", recovery);

            if (!string.IsNullOrWhiteSpace(actionText) && action != null)
            {
                Button actionButton = CreateToolbarButton(actionText, actionText, action, true);
                actionButton.style.marginTop = 12f;
                error.Add(actionButton);
            }
            return error;
        }

        private static void AddErrorLine(VisualElement parent, string label, string value)
        {
            if (parent == null || string.IsNullOrWhiteSpace(value))
                return;

            VisualElement line = new VisualElement { name = "ESErrorStateLine" };
            line.style.flexDirection = FlexDirection.Row;
            line.style.maxWidth = 640f;
            line.style.marginTop = 6f;

            Label key = new Label(label + "：") { name = "ESErrorStateKey" };
            key.style.width = 42f;
            key.style.flexShrink = 0f;
            key.style.color = ESEditorPresentation.SectionSelectedTextColor;
            key.style.unityFontStyleAndWeight = FontStyle.Bold;
            line.Add(key);

            Label detail = new Label(value.Trim()) { name = "ESErrorStateDetail" };
            detail.style.flexGrow = 1f;
            detail.style.minWidth = 0f;
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.color = ESEditorPresentation.EmptyTextColor;
            line.Add(detail);
            parent.Add(line);
        }

        internal static void ApplySemanticTheme(VisualElement root)
        {
            if (root == null)
                return;

            root.style.backgroundColor = ESEditorPresentation.WindowSurfaceColor;
            root.style.color = ESEditorPresentation.SectionTextColor;
            StyleClass(root, "es-agent-header", ESEditorPresentation.WindowRaisedSurfaceColor);
            StyleClass(root, "es-agent-sidebar", ESEditorPresentation.WindowInsetSurfaceColor);
            StyleClass(root, "es-agent-conversation", ESEditorPresentation.WindowSurfaceColor);
            StyleClass(root, "es-agent-context-panel", ESEditorPresentation.WindowRaisedSurfaceColor);
            StyleClass(root, "es-agent-composer-shell", ESEditorPresentation.WindowInsetSurfaceColor);
            StyleClass(root, "es-agent-empty", ESEditorPresentation.CanvasSurfaceColor);
            StyleClass(root, "es-agent-header-button", ESEditorPresentation.ControlSurfaceColor);
            StyleClass(root, "es-agent-secondary-button", ESEditorPresentation.ControlSurfaceColor);

            VisualElement brandMark = FindClass(root, "es-agent-brand-mark");
            if (brandMark != null)
            {
                brandMark.style.backgroundColor = ESEditorPresentation.ControlSurfaceColor;
                SetBorderColor(brandMark, ESEditorPresentation.SelectionColor);
                brandMark.style.color = ESEditorPresentation.SectionSelectedTextColor;
            }

            root.Query<VisualElement>(className: "es-agent-primary-button").ForEach(primary =>
            {
                primary.style.backgroundColor = ESEditorPresentation.SelectionColor;
                SetBorderColor(primary, ESEditorPresentation.SelectionColor);
                primary.style.color = ESEditorPresentation.SectionTextColor;
            });

            root.Query<VisualElement>(className: "es-agent-link-button").ForEach(link =>
                link.style.color = ESEditorPresentation.SectionSelectedTextColor);

            VisualElement composer = FindClass(root, "es-agent-composer");
            if (composer != null)
                SetBorderColor(composer, ESEditorPresentation.DividerColor);
        }

        internal static void StyleStatusPill(VisualElement pill, ESStatusKind status)
        {
            if (pill == null)
                return;
            Color accent = ESEditorPresentation.GetStatusAccent(0, status);
            Color surface = accent;
            surface.a = EditorGUIUtility.isProSkin ? 0.18f : 0.12f;
            pill.style.backgroundColor = surface;
            pill.style.color = accent;
            pill.style.borderLeftColor = accent;
        }

        private static VisualElement FindClass(VisualElement root, string className)
        {
            return root?.Q<VisualElement>(className: className);
        }

        private static void StyleClass(VisualElement root, string className, Color background)
        {
            root.Query<VisualElement>(className: className).ForEach(element =>
            {
                element.style.backgroundColor = background;
                SetBorderColor(element, ESEditorPresentation.DividerColor);
                element.style.color = ESEditorPresentation.SectionTextColor;
            });
        }

        private static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
        }
    }

    /// <summary>
    /// Reversible Unity 2022.3 deep-skin layer. The project theme explicitly opts in; application
    /// performs one bounded EditorStyles reflection pass and one enumeration of open EditorWindow
    /// instances. No asset scan, global window polling or per-frame skin work is used.
    /// </summary>
    internal static class ESGlobalEditorSkinExperiment
    {
        private enum StyleRole
        {
            None,
            Text,
            InteractiveText,
            Toolbar,
            Button,
            Input,
            Header,
            Help,
            Selection
        }

        private enum SkinTone
        {
            Surface,
            Raised,
            Hover,
            Active,
            Focused,
            Input,
            Toolbar,
            Help
        }

        private sealed class StateSnapshot
        {
            public GUIStyleState state;
            public Color textColor;
            public Texture2D background;
            public Texture2D[] scaledBackgrounds;
        }

        private sealed class StyleSnapshot
        {
            public GUIStyle style;
            public StateSnapshot[] states;
        }

        private sealed class RootSnapshot
        {
            public VisualElement root;
        }

        private sealed class SourcePixels
        {
            public Color32[] pixels;
        }

        private const string GlobalStyleSheetPath =
            "Assets/Plugins/ES/Editor/ESPresentation/Styles/ESGlobalEditorDeepSkin.uss";
        private const string RootClass = "es-global-editor-skin";
        private const string DarkRootClass = "es-global-editor-skin--dark";
        private const string LightRootClass = "es-global-editor-skin--light";
        private const int MaxTintTexturePixels = 262144;
        private const int MaxCreatedTextureCount = 64;
        private const long MaxCreatedTextureBytes = 16L * 1024L * 1024L;
        private const int MaxEditorStylesInitializationRetries = 8;

        private static readonly List<StyleSnapshot> snapshots = new List<StyleSnapshot>(96);
        private static readonly HashSet<GUIStyle> styledStyles = new HashSet<GUIStyle>();
        private static readonly List<RootSnapshot> rootSnapshots = new List<RootSnapshot>(32);
        private static readonly HashSet<VisualElement> styledRoots = new HashSet<VisualElement>();
        private static readonly Dictionary<long, Texture2D> themedTextureCache =
            new Dictionary<long, Texture2D>(96);
        private static readonly List<Texture2D> createdTextures = new List<Texture2D>(96);
        private static readonly Dictionary<int, SourcePixels> sourcePixelsCache =
            new Dictionary<int, SourcePixels>(32);
        private static readonly Dictionary<Type, FieldInfo[]> styleFieldsByType =
            new Dictionary<Type, FieldInfo[]>(2);
        private static readonly FieldInfo currentEditorStylesField = typeof(EditorStyles).GetField(
            "s_Current",
            BindingFlags.Static | BindingFlags.NonPublic);
        private static long createdTextureBytes;
        private static bool applied;
        private static object appliedEditorStyles;
        private static bool appliedProSkin;
        private static bool editorStylesInitializationPending;
        private static bool initializationRetryQueued;
        private static bool rootRefreshQueued;
        private static int initializationRetryCount;
        private static StyleSheet globalStyleSheet;

        public static bool IsApplied => applied;
        public static int StyledWindowCount => rootSnapshots.Count;

        public static bool TryApply(out string message)
        {
            if (applied)
            {
                QueueOpenWindowRootRefresh();
                message = BuildAppliedMessage();
                return true;
            }

            if (Application.isBatchMode)
            {
                message = "BatchMode 不加载 ES 深度皮肤，未改变 Unity 原生样式。";
                return false;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                message = "PlayMode 中不会启用 ES 深度皮肤，请返回 EditMode 后重试。";
                return false;
            }

            if (!Application.unityVersion.StartsWith("2022.3.", StringComparison.Ordinal))
            {
                message = "当前 Unity 版本不是 2022.3，深度皮肤已拒绝运行。";
                return false;
            }

            snapshots.Clear();
            styledStyles.Clear();
            rootSnapshots.Clear();
            styledRoots.Clear();
            themedTextureCache.Clear();
            DestroyCreatedTextures();

            if (!TryGetCurrentEditorStyles(out object currentStyles, out message))
            {
                if (editorStylesInitializationPending)
                    QueueInitializationRetry();
                return false;
            }

            try
            {
                ApplyEditorStyles(currentStyles);
            }
            finally
            {
                sourcePixelsCache.Clear();
            }
            RefreshOpenWindowRoots();
            if (snapshots.Count == 0 && rootSnapshots.Count == 0)
            {
                Restore();
                message = "没有找到可安全调整的 Unity 编辑器表面，未改变原生样式。";
                return false;
            }

            applied = true;
            appliedEditorStyles = currentStyles;
            appliedProSkin = EditorGUIUtility.isProSkin;
            ESEditorPresentation.NotifyGlobalEditorSkinChanged();
            CancelInitializationRetry();
            QueueOpenWindowRootRefresh();
            InternalEditorUtility.RepaintAllViews();
            message = BuildAppliedMessage();
            return true;
        }

        public static void Restore()
        {
            RestoreInternal(true, true);
        }

        private static void RestoreInternal(bool notifyPresentation, bool repaintAllViews)
        {
            bool wasApplied = applied;
            bool hadState = wasApplied || snapshots.Count > 0 || rootSnapshots.Count > 0
                || createdTextures.Count > 0;
            CancelInitializationRetry();
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            rootRefreshQueued = false;

            for (int i = 0; i < snapshots.Count; i++)
                RestoreStyle(snapshots[i]);

            for (int i = 0; i < rootSnapshots.Count; i++)
                RestoreRoot(rootSnapshots[i]);

            snapshots.Clear();
            styledStyles.Clear();
            rootSnapshots.Clear();
            styledRoots.Clear();
            themedTextureCache.Clear();
            sourcePixelsCache.Clear();
            DestroyCreatedTextures();
            globalStyleSheet = null;
            applied = false;
            appliedEditorStyles = null;
            if (wasApplied && notifyPresentation)
                ESEditorPresentation.NotifyGlobalEditorSkinChanged();
            if (repaintAllViews && hadState)
                InternalEditorUtility.RepaintAllViews();
        }

        public static void Synchronize(bool shouldApply)
        {
            if (!shouldApply || EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
            {
                Restore();
                return;
            }

            if (applied)
            {
                QueueOpenWindowRootRefresh();
                return;
            }

            TryApply(out _);
        }

        public static bool Refresh(out string message)
        {
            if (!applied)
                return TryApply(out message);
            if (!TryGetCurrentEditorStyles(out object currentStyles, out message))
            {
                if (editorStylesInitializationPending)
                    QueueInitializationRetry();
                return false;
            }

            if (ReferenceEquals(appliedEditorStyles, currentStyles)
                && appliedProSkin == EditorGUIUtility.isProSkin)
            {
                RefreshOpenWindowRoots();
                InternalEditorUtility.RepaintAllViews();
                message = BuildAppliedMessage() + " 本次仅增量同步窗口，未重建 IMGUI 纹理。";
                return true;
            }

            RestoreInternal(false, false);
            return TryApply(out message);
        }

        private static bool TryGetCurrentEditorStyles(out object currentStyles, out string message)
        {
            editorStylesInitializationPending = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity 正在编译或导入资源，ES 全局皮肤将在 Editor 空闲后重试。";
                return false;
            }

            // Force common lazy properties to initialize before the bounded field pass.
            GUIStyle currentLabel;
            GUIStyle currentToolbar;
            GUIStyle currentTextField;
            GUIStyle currentButton;
            GUIStyle currentHelpBox;
            try
            {
                currentLabel = EditorStyles.label;
                currentToolbar = EditorStyles.toolbar;
                currentTextField = EditorStyles.textField;
                currentButton = EditorStyles.miniButton;
                currentHelpBox = EditorStyles.helpBox;
            }
            catch (NullReferenceException)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity EditorStyles 正在初始化，ES 全局皮肤将在下一次 Editor 回调中重试。";
                return false;
            }
            if (currentLabel == null || currentToolbar == null || currentTextField == null
                || currentButton == null || currentHelpBox == null)
            {
                currentStyles = null;
                editorStylesInitializationPending = true;
                message = "Unity 2022.3 的 EditorStyles 尚未准备完成，ES 全局皮肤将延迟重试。";
                return false;
            }

            currentStyles = currentEditorStylesField?.GetValue(null);
            if (currentStyles == null)
            {
                editorStylesInitializationPending = true;
                message = "Unity 2022.3 的 EditorStyles 当前容器尚未初始化，ES 全局皮肤将延迟重试。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static void QueueInitializationRetry()
        {
            if (initializationRetryQueued
                || initializationRetryCount >= MaxEditorStylesInitializationRetries
                || EditorApplication.isPlayingOrWillChangePlaymode
                || Application.isBatchMode)
                return;

            initializationRetryQueued = true;
            initializationRetryCount++;
            EditorApplication.delayCall -= RetryInitialization;
            EditorApplication.delayCall += RetryInitialization;
        }

        private static void RetryInitialization()
        {
            EditorApplication.delayCall -= RetryInitialization;
            initializationRetryQueued = false;

            ESGlobalEditorTheme current = ESGlobalEditorTheme.Instance;
            bool shouldApply = current != null
                && current.enableGlobalEditorShell
                && current.enableDeepEditorSkin
                && !EditorApplication.isPlayingOrWillChangePlaymode;
            if (!shouldApply)
            {
                CancelInitializationRetry();
                return;
            }

            TryApply(out _);
        }

        private static void CancelInitializationRetry()
        {
            EditorApplication.delayCall -= RetryInitialization;
            initializationRetryQueued = false;
            initializationRetryCount = 0;
            editorStylesInitializationPending = false;
        }

        private static void ApplyEditorStyles(object currentStyles)
        {
            Type stylesType = currentStyles.GetType();
            if (!styleFieldsByType.TryGetValue(stylesType, out FieldInfo[] fields))
            {
                FieldInfo[] allFields = stylesType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var supportedFields = new List<FieldInfo>(allFields.Length);
                for (int i = 0; i < allFields.Length; i++)
                {
                    FieldInfo candidate = allFields[i];
                    if (candidate.FieldType == typeof(GUIStyle) && !candidate.IsLiteral)
                        supportedFields.Add(candidate);
                }
                fields = supportedFields.ToArray();
                styleFieldsByType[stylesType] = fields;
            }
            Color normalText = EditorGUIUtility.isProSkin
                ? new Color(0.84f, 0.88f, 0.92f, 1f)
                : new Color(0.15f, 0.18f, 0.22f, 1f);
            Color interactiveText = Color.Lerp(normalText, ESEditorPresentation.LogicSteelBlue,
                EditorGUIUtility.isProSkin ? 0.34f : 0.28f);
            Color selectedText = EditorGUIUtility.isProSkin
                ? new Color(0.94f, 0.97f, 1f, 1f)
                : new Color(0.06f, 0.13f, 0.19f, 1f);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                try
                {
                    GUIStyle style = field.GetValue(currentStyles) as GUIStyle;
                    if (style == null || ContainsStyle(style))
                        continue;

                    StyleRole role = ClassifyStyle(field.Name, style.name);
                    if (role == StyleRole.None)
                        continue;

                    StyleSnapshot snapshot = CaptureStyle(style);
                    snapshots.Add(snapshot);
                    styledStyles.Add(style);
                    ApplyStyle(style, role, normalText, interactiveText, selectedText);
                }
                catch
                {
                    // Unity 内部字段逐项隔离；不可访问字段不会中断其余可逆样式。
                }
            }

            // Unity 内置 Inspector/Scene GUISkin 是跨窗口共享对象。修改它会污染所有
            // Editor 页面，因此深度皮肤只处理已识别的 EditorStyles 文本语义。
        }

        private static void ApplyBuiltInSkin(
            EditorSkin editorSkin,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            GUISkin skin;
            try
            {
                skin = EditorGUIUtility.GetBuiltinSkin(editorSkin);
            }
            catch
            {
                return;
            }
            if (skin == null)
                return;

            ApplyKnownStyle(skin.label, StyleRole.Text, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.button, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.box, StyleRole.Header, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.toggle, StyleRole.InteractiveText, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.textField, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.textArea, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.window, StyleRole.Header, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalSlider, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalSliderThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalSlider, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalSliderThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalScrollbar, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.horizontalScrollbarThumb, StyleRole.Button, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalScrollbar, StyleRole.Input, normalText, interactiveText, selectedText);
            ApplyKnownStyle(skin.verticalScrollbarThumb, StyleRole.Button, normalText, interactiveText, selectedText);

            GUIStyle[] customStyles = skin.customStyles;
            if (customStyles == null)
                return;
            for (int i = 0; i < customStyles.Length; i++)
            {
                GUIStyle style = customStyles[i];
                if (style == null)
                    continue;
                StyleRole role = ClassifyStyle(style.name, style.name);
                ApplyKnownStyle(style, role, normalText, interactiveText, selectedText);
            }
        }

        private static void ApplyKnownStyle(
            GUIStyle style,
            StyleRole role,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            if (style == null || role == StyleRole.None || ContainsStyle(style))
                return;
            try
            {
                StyleSnapshot snapshot = CaptureStyle(style);
                snapshots.Add(snapshot);
                styledStyles.Add(style);
                ApplyStyle(style, role, normalText, interactiveText, selectedText);
            }
            catch
            {
                // 内置皮肤逐样式隔离，保留已捕获快照供完整恢复。
            }
        }

        private static StyleSnapshot CaptureStyle(GUIStyle style)
        {
            return new StyleSnapshot
            {
                style = style,
                states = new[]
                {
                    CaptureState(style.normal),
                    CaptureState(style.hover),
                    CaptureState(style.active),
                    CaptureState(style.focused),
                    CaptureState(style.onNormal),
                    CaptureState(style.onHover),
                    CaptureState(style.onActive),
                    CaptureState(style.onFocused)
                }
            };
        }

        private static StateSnapshot CaptureState(GUIStyleState state)
        {
            Texture2D[] scaled = state.scaledBackgrounds;
            return new StateSnapshot
            {
                state = state,
                textColor = state.textColor,
                background = state.background,
                scaledBackgrounds = scaled == null ? null : (Texture2D[])scaled.Clone()
            };
        }

        private static void ApplyStyle(
            GUIStyle style,
            StyleRole role,
            Color normalText,
            Color interactiveText,
            Color selectedText)
        {
            Color baseText = role == StyleRole.Text ? normalText : interactiveText;
            ApplyState(style.normal, baseText, GetNormalTone(role));
            ApplyState(style.hover, selectedText, SkinTone.Hover);
            ApplyState(style.active, selectedText, SkinTone.Active);
            ApplyState(style.focused, selectedText, SkinTone.Focused);
            ApplyState(style.onNormal, selectedText, SkinTone.Active);
            ApplyState(style.onHover, selectedText, SkinTone.Hover);
            ApplyState(style.onActive, selectedText, SkinTone.Active);
            ApplyState(style.onFocused, selectedText, SkinTone.Focused);
        }

        private static void ApplyState(GUIStyleState state, Color textColor, SkinTone tone)
        {
            // background == null 在 Unity IMGUI 中通常表示透明宿主表面，不能替换为
            // 不透明纯色纹理。仅对已有背景保留透明度与形状后做 ES 色调染色。
            state.textColor = textColor;
            if (state.background == null)
                return;

            state.background = GetThemedTexture(state.background, tone);
            Texture2D[] scaled = state.scaledBackgrounds;
            if (scaled == null || scaled.Length == 0)
                return;

            Texture2D[] themedScaled = new Texture2D[scaled.Length];
            for (int i = 0; i < scaled.Length; i++)
                themedScaled[i] = scaled[i] == null ? null : GetThemedTexture(scaled[i], tone);
            state.scaledBackgrounds = themedScaled;
        }

        private static SkinTone GetNormalTone(StyleRole role)
        {
            switch (role)
            {
                case StyleRole.Toolbar:
                    return SkinTone.Toolbar;
                case StyleRole.Input:
                    return SkinTone.Input;
                case StyleRole.Help:
                    return SkinTone.Help;
                case StyleRole.Button:
                case StyleRole.Header:
                    return SkinTone.Raised;
                case StyleRole.Selection:
                    return SkinTone.Active;
                default:
                    return SkinTone.Surface;
            }
        }

        private static bool ProvidesBackground(StyleRole role)
        {
            return role == StyleRole.Toolbar
                || role == StyleRole.Button
                || role == StyleRole.Input
                || role == StyleRole.Header
                || role == StyleRole.Help
                || role == StyleRole.Selection;
        }

        private static StyleRole ClassifyStyle(string fieldName, string styleName)
        {
            string name = (fieldName ?? string.Empty) + " " + (styleName ?? string.Empty);
            if (ContainsIgnoreCase(name, "toolbar"))
                return StyleRole.Toolbar;
            if (ContainsIgnoreCase(name, "helpbox") || ContainsIgnoreCase(name, "notification"))
                return StyleRole.Help;
            if (ContainsIgnoreCase(name, "textfield") || ContainsIgnoreCase(name, "textarea")
                || ContainsIgnoreCase(name, "numberfield") || ContainsIgnoreCase(name, "objectfield")
                || ContainsIgnoreCase(name, "colorfield") || ContainsIgnoreCase(name, "searchfield")
                || ContainsIgnoreCase(name, "popup") || ContainsIgnoreCase(name, "dropdown")
                || ContainsIgnoreCase(name, "layermask"))
                return StyleRole.Input;
            if (ContainsIgnoreCase(name, "button"))
                return StyleRole.Button;
            if (ContainsIgnoreCase(name, "titlebar") || ContainsIgnoreCase(name, "header"))
                return StyleRole.Header;
            if (ContainsIgnoreCase(name, "selection") || ContainsIgnoreCase(name, "selected"))
                return StyleRole.Selection;
            if (ContainsIgnoreCase(name, "foldout") || ContainsIgnoreCase(name, "toggle")
                || ContainsIgnoreCase(name, "radio"))
                return StyleRole.InteractiveText;
            if (ContainsIgnoreCase(name, "label") || ContainsIgnoreCase(name, "link"))
                return StyleRole.Text;
            return StyleRole.None;
        }

        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture2D GetThemedTexture(Texture2D source, SkinTone tone)
        {
            if (source == null)
                return null;

            int sourceId = source.GetInstanceID();
            long key = ((long)sourceId << 8) ^ (int)tone;
            if (themedTextureCache.TryGetValue(key, out Texture2D cached))
                return cached;

            Texture2D themed = CreateTintedTexture(source, tone);
            if (themed == null)
                themed = source;
            themedTextureCache[key] = themed;
            return themed;
        }

        private static Texture2D CreateTintedTexture(Texture2D source, SkinTone tone)
        {
            long pixelCount = source == null ? 0L : (long)source.width * source.height;
            long requiredBytes = pixelCount * 4L;
            if (source == null || source.width <= 0 || source.height <= 0
                || pixelCount > MaxTintTexturePixels || !CanCreateTexture(requiredBytes))
                return source;

            Texture2D output = null;
            try
            {
                SourcePixels sourcePixels = GetSourcePixels(source);
                if (sourcePixels == null || sourcePixels.pixels == null)
                    return source;

                output = new Texture2D(source.width, source.height, UnityEngine.TextureFormat.RGBA32, false)
                {
                    name = "ES Deep Skin " + source.name + " " + tone,
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode
                };
                Color32[] pixels = new Color32[sourcePixels.pixels.Length];
                Color target = GetToneColor(tone);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color original = sourcePixels.pixels[i];
                    float luminance = original.r * 0.2126f + original.g * 0.7152f + original.b * 0.0722f;
                    float brightness = Mathf.Lerp(0.72f, 1.18f, luminance);
                    Color tinted = new Color(
                        Mathf.Clamp01(target.r * brightness),
                        Mathf.Clamp01(target.g * brightness),
                        Mathf.Clamp01(target.b * brightness),
                        original.a);
                    Color blended = Color.Lerp(original, tinted, 0.76f);
                    blended.a = original.a;
                    pixels[i] = blended;
                }

                output.SetPixels32(pixels);
                output.Apply(false, true);
                createdTextures.Add(output);
                createdTextureBytes += requiredBytes;
                return output;
            }
            catch
            {
                if (output != null)
                    UnityEngine.Object.DestroyImmediate(output);
                return source;
            }
        }

        private static SourcePixels GetSourcePixels(Texture2D source)
        {
            int sourceId = source.GetInstanceID();
            if (sourcePixelsCache.TryGetValue(sourceId, out SourcePixels cached))
                return cached;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = null;
            Texture2D readableCopy = null;
            try
            {
                Color32[] pixels;
                try
                {
                    pixels = source.GetPixels32();
                }
                catch (UnityException)
                {
                    temporary = RenderTexture.GetTemporary(
                        source.width,
                        source.height,
                        0,
                        RenderTextureFormat.ARGB32,
                        RenderTextureReadWrite.Default);
                    Graphics.Blit(source, temporary);
                    RenderTexture.active = temporary;
                    readableCopy = new Texture2D(
                        source.width,
                        source.height,
                        UnityEngine.TextureFormat.RGBA32,
                        false);
                    readableCopy.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                    pixels = readableCopy.GetPixels32();
                }

                var result = new SourcePixels
                {
                    pixels = pixels
                };
                sourcePixelsCache[sourceId] = result;
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (readableCopy != null)
                    UnityEngine.Object.DestroyImmediate(readableCopy);
                if (temporary != null)
                    RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Color GetToneColor(SkinTone tone)
        {
            if (!EditorGUIUtility.isProSkin)
            {
                switch (tone)
                {
                    case SkinTone.Toolbar: return new Color(0.78f, 0.82f, 0.86f, 1f);
                    case SkinTone.Input: return new Color(0.86f, 0.89f, 0.92f, 1f);
                    case SkinTone.Raised: return new Color(0.80f, 0.84f, 0.88f, 1f);
                    case SkinTone.Hover: return new Color(0.63f, 0.76f, 0.86f, 1f);
                    case SkinTone.Active: return new Color(0.36f, 0.62f, 0.80f, 1f);
                    case SkinTone.Focused: return new Color(0.48f, 0.70f, 0.84f, 1f);
                    case SkinTone.Help: return new Color(0.76f, 0.83f, 0.88f, 1f);
                    default: return new Color(0.83f, 0.86f, 0.89f, 1f);
                }
            }

            switch (tone)
            {
                case SkinTone.Toolbar: return new Color(0.105f, 0.13f, 0.16f, 1f);
                case SkinTone.Input: return new Color(0.13f, 0.17f, 0.205f, 1f);
                case SkinTone.Raised: return new Color(0.17f, 0.215f, 0.255f, 1f);
                case SkinTone.Hover: return new Color(0.18f, 0.30f, 0.38f, 1f);
                case SkinTone.Active: return new Color(0.12f, 0.38f, 0.55f, 1f);
                case SkinTone.Focused: return new Color(0.14f, 0.32f, 0.44f, 1f);
                case SkinTone.Help: return new Color(0.16f, 0.22f, 0.27f, 1f);
                default: return new Color(0.135f, 0.165f, 0.195f, 1f);
            }
        }

        private static void QueueOpenWindowRootRefresh()
        {
            if (rootRefreshQueued || !applied)
                return;
            rootRefreshQueued = true;
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            EditorApplication.delayCall += RefreshOpenWindowRoots;
        }

        private static void RefreshOpenWindowRoots()
        {
            EditorApplication.delayCall -= RefreshOpenWindowRoots;
            rootRefreshQueued = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isBatchMode)
                return;

            if (globalStyleSheet == null)
                globalStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(GlobalStyleSheetPath);
            if (globalStyleSheet == null)
                return;

            for (int i = rootSnapshots.Count - 1; i >= 0; i--)
            {
                RootSnapshot snapshot = rootSnapshots[i];
                VisualElement staleRoot = snapshot == null ? null : snapshot.root;
                if (staleRoot != null && staleRoot.panel != null)
                    continue;
                RestoreRoot(snapshot);
                if (staleRoot != null)
                    styledRoots.Remove(staleRoot);
                rootSnapshots.RemoveAt(i);
            }

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow window = windows[i];
                VisualElement root = window == null ? null : window.rootVisualElement;
                if (root == null || root.panel == null || ContainsRoot(root))
                    continue;

                if (!root.styleSheets.Contains(globalStyleSheet))
                    root.styleSheets.Add(globalStyleSheet);
                root.AddToClassList(RootClass);
                root.EnableInClassList(DarkRootClass, EditorGUIUtility.isProSkin);
                root.EnableInClassList(LightRootClass, !EditorGUIUtility.isProSkin);
                rootSnapshots.Add(new RootSnapshot { root = root });
                styledRoots.Add(root);
                root.MarkDirtyRepaint();
            }
        }

        private static void RestoreStyle(StyleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.style == null || snapshot.states == null)
                return;

            for (int i = 0; i < snapshot.states.Length; i++)
            {
                StateSnapshot state = snapshot.states[i];
                if (state == null || state.state == null)
                    continue;
                try
                {
                    state.state.textColor = state.textColor;
                    state.state.background = state.background;
                    state.state.scaledBackgrounds = state.scaledBackgrounds;
                }
                catch
                {
                    // 恢复逐状态隔离；一个 Unity 内部状态失效不阻断其余样式恢复。
                }
            }
        }

        private static void RestoreRoot(RootSnapshot snapshot)
        {
            VisualElement root = snapshot == null ? null : snapshot.root;
            if (root == null)
                return;
            root.RemoveFromClassList(RootClass);
            root.RemoveFromClassList(DarkRootClass);
            root.RemoveFromClassList(LightRootClass);
            if (globalStyleSheet != null && root.styleSheets.Contains(globalStyleSheet))
                root.styleSheets.Remove(globalStyleSheet);
            root.MarkDirtyRepaint();
        }

        private static void DestroyCreatedTextures()
        {
            for (int i = 0; i < createdTextures.Count; i++)
                if (createdTextures[i] != null)
                    UnityEngine.Object.DestroyImmediate(createdTextures[i]);
            createdTextures.Clear();
            createdTextureBytes = 0L;
        }

        private static bool CanCreateTexture(long requiredBytes)
        {
            return requiredBytes > 0L
                && createdTextures.Count < MaxCreatedTextureCount
                && createdTextureBytes + requiredBytes <= MaxCreatedTextureBytes;
        }

        private static string BuildAppliedMessage()
        {
            return "ES 全局深度皮肤已覆盖 " + snapshots.Count + " 个 IMGUI 文字样式和 "
                + rootSnapshots.Count + " 个 UI Toolkit 窗口；纯色只应用到安全内容容器，"
                + "原生窗口根节点与透明绘制层保持不变。进入 PlayMode 自动停用，可随时恢复原生样式。";
        }

        private static bool ContainsStyle(GUIStyle style)
        {
            return style != null && styledStyles.Contains(style);
        }

        private static bool ContainsRoot(VisualElement root)
        {
            return root != null && styledRoots.Contains(root);
        }

    }
}
