#if UNITY_EDITOR
using UnityEngine;
using ES.EditorInternal;
namespace ES
{
    /// <summary>地图编辑器统一视觉与轻量控件入口。颜色由 ESGlobalEditorTheme 提供。</summary>
    internal static class ESWorldMapEditorPresentation
    {
        public static Color TerrainBase => ESEditorPresentation.MapTerrainBaseColor;
        public static Color Grid => ESEditorPresentation.MapGridColor;
        public static Color Region => ESEditorPresentation.MapRegionColor;
        public static Color Poi => ESEditorPresentation.MapPoiColor;
        public static Color Selection => ESEditorPresentation.MapSelectionColor;
        public static Color HeightLow => ESEditorPresentation.MapHeightLowColor;
        public static Color HeightHigh => ESEditorPresentation.MapHeightHighColor;

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }
    }
}
#endif
