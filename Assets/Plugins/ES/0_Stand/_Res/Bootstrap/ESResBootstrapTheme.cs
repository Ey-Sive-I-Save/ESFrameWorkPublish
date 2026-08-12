using UnityEngine;
using TMPro;

namespace ES
{
    [CreateAssetMenu(menuName = "【ES】/资源管线/运行时配置/启动界面主题", fileName = "ESResBootstrapTheme")]
    public sealed class ESResBootstrapTheme : ScriptableObject
    {
        [Header("Brand")]
        public string productName = "ES RESOURCE CENTER";
        [TextArea(1, 2)] public string productSubtitle = "安全验证 · 平滑过渡 · 即刻启程";
        [TextArea(2, 4)] public string localModeNotice = "本地资源模式：模拟验证，不请求服务端";
        [TextArea(2, 5)] public string announcement = "欢迎回来。资源验证完成后即可进入游戏。";
        public Sprite logo;
        public TMP_FontAsset titleFont;
        public TMP_FontAsset bodyFont;

        [Header("Replaceable Artwork")]
        public Sprite background;
        public Sprite loadingPanel;
        public Sprite progressTrack;
        public Sprite progressFill;
        public Sprite primaryButton;

        [Header("Palette")]
        public Color backgroundTint = new Color(0.025f, 0.07f, 0.13f, 1f);
        public Color panelTint = new Color(0.035f, 0.1f, 0.18f, 0.92f);
        public Color accentColor = new Color(0.94f, 0.71f, 0.27f, 1f);
        public Color bodyColor = new Color(0.71f, 0.79f, 0.9f, 1f);
        public Color announcementColor = new Color(0.8f, 0.85f, 0.92f, 0.9f);
        public Color buttonTextColor = new Color(0.012f, 0.018f, 0.035f, 1f);

        [Header("Layout")]
        [Min(12)] public int titleFontSize = 48;
        [Min(12)] public int bodyFontSize = 19;
        [Min(12)] public int statusFontSize = 27;
        [Min(12)] public int buttonFontSize = 19;
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    }
}
