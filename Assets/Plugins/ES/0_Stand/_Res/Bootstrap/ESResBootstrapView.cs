using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ES
{
    [DisallowMultipleComponent]
    public sealed class ESResBootstrapView : MonoBehaviour
    {
        private static bool warnedMissingDefaultFont;

        private ESResBootstrapTheme theme;
        private TextMeshProUGUI titleText, subtitleText, announcementText, statusText, detailText, percentText, actionText;
        private Image backgroundImage, logoImage, panelImage, trackImage, progressFill, actionImage;
        private Button actionButton;

        private Color Background => theme != null ? theme.backgroundTint : new Color(0.025f, 0.07f, 0.13f, 1f);
        private Color Panel => theme != null ? theme.panelTint : new Color(0.035f, 0.1f, 0.18f, 0.92f);
        private Color Accent => theme != null ? theme.accentColor : new Color(0.94f, 0.71f, 0.27f, 1f);
        private Color Body => theme != null ? theme.bodyColor : new Color(0.71f, 0.79f, 0.9f, 1f);
        private Color ButtonText => theme != null ? theme.buttonTextColor : new Color(0.012f, 0.018f, 0.035f, 1f);

        private void Awake() => BuildCanvas();

        public void ApplyTheme(ESResBootstrapTheme value)
        {
            theme = value;
            if (titleText == null) return;
            titleText.text = theme != null ? theme.productName : "ES RESOURCE CENTER";
            subtitleText.text = theme != null ? theme.productSubtitle : "安全验证 · 平滑过渡 · 即刻启程";
            ApplyFont(titleText, theme != null ? theme.titleFont : null);
            ApplyFont(subtitleText, theme != null ? theme.bodyFont : null);
            ApplyFont(announcementText, theme != null ? theme.bodyFont : null);
            ApplyFont(statusText, theme != null ? theme.bodyFont : null);
            ApplyFont(detailText, theme != null ? theme.bodyFont : null);
            ApplyFont(percentText, theme != null ? theme.titleFont : null);
            ApplyFont(actionText, theme != null ? theme.bodyFont : null);
            titleText.color = Accent; titleText.fontSize = theme != null ? theme.titleFontSize : 48;
            subtitleText.color = Body; subtitleText.fontSize = theme != null ? theme.bodyFontSize : 19;
            announcementText.text = theme != null ? theme.announcement : "欢迎回来。资源验证完成后即可进入游戏。";
            announcementText.color = theme != null ? theme.announcementColor : Body;
            announcementText.fontSize = theme != null ? theme.bodyFontSize : 18;
            statusText.fontSize = theme != null ? theme.statusFontSize : 27;
            detailText.color = Body; detailText.fontSize = theme != null ? theme.bodyFontSize : 18;
            percentText.color = Accent; actionText.color = ButtonText; actionText.fontSize = theme != null ? theme.buttonFontSize : 19;
            backgroundImage.color = Background; backgroundImage.sprite = theme != null ? theme.background : null;
            panelImage.color = Panel; panelImage.sprite = theme != null ? theme.loadingPanel : null;
            trackImage.sprite = theme != null ? theme.progressTrack : null;
            progressFill.color = Accent; progressFill.sprite = theme != null ? theme.progressFill : null;
            actionImage.color = Accent; actionImage.sprite = theme != null ? theme.primaryButton : null;
            logoImage.sprite = theme != null ? theme.logo : null; logoImage.gameObject.SetActive(logoImage.sprite != null);
        }

        public void SetProgress(float progress, string status, string detail)
        {
            progressFill.fillAmount = Mathf.Clamp01(progress);
            statusText.text = status ?? string.Empty;
            detailText.text = detail ?? string.Empty;
            percentText.text = Mathf.RoundToInt(progressFill.fillAmount * 100f) + "%";
        }

        public void SetAction(Action action, string label)
        {
            actionButton.onClick.RemoveAllListeners();
            actionButton.gameObject.SetActive(action != null);
            if (action == null) return;
            actionText.text = label ?? string.Empty;
            actionButton.onClick.AddListener(() => action());
        }

        /// <summary>资源准备完成后隐藏启动 UI；Manager 本身保持存活以支持后续更新检查。</summary>
        public void SetVisible(bool visible)
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = visible;
        }

        private void BuildCanvas()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
                DontDestroyOnLoad(new GameObject("ESResBootstrapEventSystem", typeof(EventSystem), typeof(StandaloneInputModule)));
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = short.MaxValue;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = theme != null ? theme.referenceResolution : new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            backgroundImage = Image("Background", transform, Background); Stretch(backgroundImage.rectTransform);
            var shade = Image("LowerShade", backgroundImage.transform, new Color(0.012f, 0.018f, 0.035f, .72f)); Rect(shade.rectTransform, Vector2.zero, new Vector2(1f, .52f));
            var line = Image("BrandLine", backgroundImage.transform, Accent); Rect(line.rectTransform, new Vector2(.12f, .67f), new Vector2(.12f, .67f), Vector2.zero, new Vector2(6f, 150f));
            logoImage = Image("Logo", backgroundImage.transform, Color.white); Rect(logoImage.rectTransform, new Vector2(.14f, .83f), new Vector2(.26f, .95f)); logoImage.preserveAspect = true;
            titleText = Text("Title", backgroundImage.transform, 48, Accent, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); Rect(titleText.rectTransform, new Vector2(.14f, .73f), new Vector2(.78f, .83f));
            subtitleText = Text("Subtitle", backgroundImage.transform, 19, Body, TextAlignmentOptions.MidlineLeft, FontStyles.Normal); Rect(subtitleText.rectTransform, new Vector2(.14f, .67f), new Vector2(.7f, .72f));
            announcementText = Text("Announcement", backgroundImage.transform, 18, Body, TextAlignmentOptions.MidlineLeft, FontStyles.Normal); Rect(announcementText.rectTransform, new Vector2(.14f, .50f), new Vector2(.72f, .61f));
            panelImage = Image("LoadingPanel", backgroundImage.transform, Panel); Rect(panelImage.rectTransform, new Vector2(.14f, .18f), new Vector2(.86f, .43f));
            statusText = Text("Status", panelImage.transform, 27, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold); Rect(statusText.rectTransform, new Vector2(.06f, .61f), new Vector2(.78f, .88f));
            detailText = Text("Detail", panelImage.transform, 18, Body, TextAlignmentOptions.MidlineLeft, FontStyles.Normal); Rect(detailText.rectTransform, new Vector2(.06f, .39f), new Vector2(.88f, .61f));
            percentText = Text("Percent", panelImage.transform, 25, Accent, TextAlignmentOptions.MidlineRight, FontStyles.Bold); Rect(percentText.rectTransform, new Vector2(.79f, .61f), new Vector2(.94f, .88f));
            trackImage = Image("ProgressTrack", panelImage.transform, new Color(.01f, .025f, .06f, 1f)); Rect(trackImage.rectTransform, new Vector2(.06f, .2f), new Vector2(.94f, .3f));
            progressFill = Image("ProgressFill", trackImage.transform, Accent); progressFill.type = UnityEngine.UI.Image.Type.Filled; progressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; progressFill.fillOrigin = 0; Stretch(progressFill.rectTransform);
            actionButton = Image("Action", panelImage.transform, Accent).gameObject.AddComponent<Button>(); actionImage = actionButton.GetComponent<Image>(); Rect(actionButton.GetComponent<RectTransform>(), new Vector2(.72f, -.34f), new Vector2(.94f, -.08f));
            actionText = Text("Label", actionButton.transform, 19, ButtonText, TextAlignmentOptions.Center, FontStyles.Bold); Stretch(actionText.rectTransform); actionButton.targetGraphic = actionImage;
            ApplyTheme(theme); actionButton.gameObject.SetActive(false);
            Debug.Log("[ESRes][Bootstrap][View] 启动界面初始化完成。", this);
        }

        private static Image Image(string name, Transform parent, Color color)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            result.transform.SetParent(parent, false);
            result.color = color;
            return result;
        }
        private static TextMeshProUGUI Text(string name, Transform parent, int size, Color color, TextAlignmentOptions align, FontStyles style) { var result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>(); result.transform.SetParent(parent, false); result.fontSize = size; result.color = color; result.alignment = align; result.fontStyle = style; result.enableWordWrapping = false; result.overflowMode = TextOverflowModes.Overflow; return result; }
        private static void ApplyFont(TextMeshProUGUI text, TMP_FontAsset font)
        {
            if (text == null) return;

            if (font != null)
            {
                text.font = font;
                return;
            }

            TMP_FontAsset fallback = null;
            Exception failure = null;
            try
            {
                fallback = TMP_Settings.defaultFontAsset;
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            if (fallback != null)
            {
                text.font = fallback;
                return;
            }

            if (warnedMissingDefaultFont) return;
            warnedMissingDefaultFont = true;
            string reason = failure == null
                ? "TMP Settings 未配置默认字体。"
                : $"读取 TMP 默认字体失败：{failure.GetType().Name}: {failure.Message}";
            Debug.LogWarning($"[ESRes][Bootstrap][TMP] {reason} 启动流程将继续；请在启动主题中指定字体，或修复 TMP Settings。", text);
        }
        private static void Stretch(RectTransform transform) => Rect(transform, Vector2.zero, Vector2.one);
        private static void Rect(RectTransform transform, Vector2 min, Vector2 max, Vector2 offsetMin = default, Vector2 offsetMax = default) { transform.anchorMin = min; transform.anchorMax = max; transform.offsetMin = offsetMin; transform.offsetMax = offsetMax; }
    }
}
