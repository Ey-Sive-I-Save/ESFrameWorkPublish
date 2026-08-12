using ES.EditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

namespace ES
{
    /// <summary>
    /// TrackView 的语义化界面令牌。轨道类型可保留自己的业务强调色，
    /// 但窗口表面、正文、分隔线和通用按钮必须从 ES Presentation 派生。
    /// </summary>
    internal static class ESTrackViewTheme
    {
        internal static Color WindowBackground => ESEditorPresentation.GetDepthBackground(3);
        internal static Color ToolbarBackground => ESEditorPresentation.GetDepthBackground(1);
        internal static Color SecondarySurface => ESEditorPresentation.GetDepthBackground(2);
        internal static Color CanvasBackground => ESEditorPresentation.GetDepthBackground(3);
        internal static Color Text => ESEditorPresentation.SectionTextColor;
        internal static Color MutedText => ESEditorPresentation.SectionMutedTextColor;
        internal static Color SelectedText => ESEditorPresentation.SectionSelectedTextColor;
        internal static Color Divider => ESEditorPresentation.DividerColor;
        internal static Color Accent => ESEditorPresentation.GetDepthAccent(0);
        internal static Color StatusNeutral => ESEditorPresentation.GetStatusAccent(0, ESStatusKind.None);
        internal static Color StatusReady => ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Ready);
        internal static Color StatusModified => ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Modified);
        internal static Color StatusWarning => ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Warning);
        internal static Color StatusError => ESEditorPresentation.GetStatusAccent(0, ESStatusKind.Error);
        internal static Color StatusReadOnly => ESEditorPresentation.GetStatusAccent(0, ESStatusKind.ReadOnly);
        internal static Color AccentSoft => Blend(SecondarySurface, Accent, ESEditorPresentation.IsProSkin ? 0.18f : 0.10f);
        internal static Color ButtonBackground => Blend(ToolbarBackground, SecondarySurface, 0.68f);
        internal static Color ButtonHoverBackground => Blend(ButtonBackground, Accent, 0.16f);
        internal static Color PlayBackground => Blend(ButtonBackground, Accent, ESEditorPresentation.IsProSkin ? 0.12f : 0.07f);
        internal static Color HoverOverlay => WithAlpha(Text, ESEditorPresentation.IsProSkin ? 0.045f : 0.035f);
        internal static Color ActiveAccent => StatusReady;
        internal static Color EditingAccent => StatusModified;
        internal static Color Transparent => new Color(0f, 0f, 0f, 0f);
        internal static Color PlayheadAccent => Blend(StatusError, Accent, 0.08f);
        internal static Color PlayheadHandle => WithAlpha(Blend(StatusError, Accent, 0.14f), 0.76f);
        internal static Color TrackInsertAccent => StatusModified;
        internal static Color RulerMinorTick => WithAlpha(MutedText, ESEditorPresentation.IsProSkin ? 0.40f : 0.52f);
        internal static Color RulerMajorTick => WithAlpha(Text, ESEditorPresentation.IsProSkin ? 0.70f : 0.78f);
        internal static Color RulerMinorGrid => WithAlpha(Divider, ESEditorPresentation.IsProSkin ? 0.07f : 0.12f);
        internal static Color RulerMajorGrid => WithAlpha(Divider, ESEditorPresentation.IsProSkin ? 0.16f : 0.22f);
        internal static Color RulerText => WithAlpha(Text, 0.92f);
        internal static Color SplitterHoverBackground => WithAlpha(Accent, ESEditorPresentation.IsProSkin ? 0.34f : 0.22f);
        internal static Color InspectorSummarySurface => Blend(SecondarySurface, Accent, ESEditorPresentation.IsProSkin ? 0.14f : 0.08f);
        internal static Color ClipDraggingSurface(Color background) => Blend(background, Accent, 0.24f);
        internal static Color ClipResizingSurface(Color background) => Blend(background, ActiveAccent, 0.18f);

        internal static Color ResolveBusinessAccent(Color source)
        {
            bool missingColor = source.a <= 0.001f
                                && source.r <= 0.001f
                                && source.g <= 0.001f
                                && source.b <= 0.001f;
            if (missingColor)
                return Accent;

            // TrackItem 的历史默认值是低 Alpha 纯黄。它没有业务分类含义，不能继续伪装成警告色。
            bool legacyDefaultYellow = source.r >= 0.95f
                                       && source.g >= 0.95f
                                       && source.b <= 0.12f
                                       && source.a <= 0.25f;
            if (legacyDefaultYellow)
                return Accent;

            source = SanitizeAccent(source);
            Color result = Color.Lerp(source, Accent, ESEditorPresentation.IsProSkin ? 0.08f : 0.14f);
            result.a = 0.92f;
            return result;
        }

        internal static Color SanitizeAccent(Color source)
        {
            Color fallback = Accent;
            bool invalidAlpha = float.IsNaN(source.a) || float.IsInfinity(source.a);
            bool transparentBlack = source.a <= 0.001f
                                    && source.r <= 0.001f
                                    && source.g <= 0.001f
                                    && source.b <= 0.001f;
            if (invalidAlpha || transparentBlack)
                source = fallback;

            return new Color(
                SanitizeChannel(source.r, fallback.r),
                SanitizeChannel(source.g, fallback.g),
                SanitizeChannel(source.b, fallback.b),
                0.92f);
        }

        internal static Color TrackHeaderSurface(Color accent, bool selected, bool protectedTrack)
        {
            Color result = Blend(SecondarySurface, accent, protectedTrack ? 0.08f : 0.035f);
            return selected ? Blend(result, Accent, ESEditorPresentation.IsProSkin ? 0.15f : 0.09f) : result;
        }

        internal static Color TrackCanvasSurface(Color accent)
        {
            return Blend(CanvasBackground, accent, ESEditorPresentation.IsProSkin ? 0.045f : 0.025f);
        }

        internal static Color IconBackground(Color accent)
        {
            return Blend(SecondarySurface, accent, ESEditorPresentation.IsProSkin ? 0.34f : 0.22f);
        }

        internal static Color ClipSurface(Color accent)
        {
            return Blend(CanvasBackground, accent, ESEditorPresentation.IsProSkin ? 0.34f : 0.20f);
        }

        internal static Color ClipDisabledSurface(Color accent)
        {
            Color neutralized = Blend(CanvasBackground, StatusReadOnly, ESEditorPresentation.IsProSkin ? 0.10f : 0.07f);
            return Blend(neutralized, accent, 0.035f);
        }

        internal static Color ClipWarningSurface(bool enabled)
        {
            return Blend(
                CanvasBackground,
                StatusWarning,
                enabled
                    ? (ESEditorPresentation.IsProSkin ? 0.24f : 0.13f)
                    : (ESEditorPresentation.IsProSkin ? 0.11f : 0.07f));
        }

        internal static Color StateBadgeSurface(Color status)
        {
            return Blend(SecondarySurface, status, ESEditorPresentation.IsProSkin ? 0.22f : 0.12f);
        }

        internal static Color SelectionFrame(bool primary)
        {
            return WithAlpha(Accent, primary ? 1f : 0.68f);
        }

        internal static Color SelectionFill(bool primary)
        {
            return WithAlpha(Accent, primary ? 0.18f : 0.09f);
        }

        internal static Color SubduedAccent(Color accent)
        {
            return WithAlpha(Blend(StatusReadOnly, accent, 0.18f), 0.72f);
        }

        internal static void ApplyStandardButton(Button button)
        {
            if (button == null)
                return;

            button.style.color = Text;
            button.style.backgroundColor = ButtonBackground;
            button.style.borderLeftColor = Divider;
            button.style.borderTopColor = Divider;
            button.style.borderRightColor = Divider;
            button.style.borderBottomColor = Divider;
        }

        internal static void ApplyAccentButton(Button button)
        {
            ApplyStandardButton(button);
            if (button == null)
                return;

            button.style.color = SelectedText;
            button.style.backgroundColor = AccentSoft;
            button.style.borderLeftColor = Accent;
            button.style.borderTopColor = Accent;
        }

        internal static Color Blend(Color background, Color foreground, float amount)
        {
            Color result = Color.Lerp(background, foreground, Mathf.Clamp01(amount));
            result.a = background.a;
            return result;
        }

        internal static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static float SanitizeChannel(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? Mathf.Clamp01(fallback)
                : Mathf.Clamp01(value);
        }
    }
}
