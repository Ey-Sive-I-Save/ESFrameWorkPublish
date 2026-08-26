#if UNITY_2019_4_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace ES.UI
{
    /// <summary>
    /// Visual-only Sprite cover crop with an authored normalized focal point.
    /// It owns no input, screen state, or business data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESUIFocalCropRawImage : RawImage
    {
        [SerializeField] private Vector2 focalPoint = new Vector2(0.5f, 0.5f);
        [SerializeField] private Rect sourceUv = new Rect(0f, 0f, 1f, 1f);
        [SerializeField] private Rect safeCropInsetsNormalized = new Rect(0f, 0f, 0f, 0f);
        [SerializeField] private bool configured;
        [SerializeField] private bool safeCropSatisfied = true;

        public Vector2 FocalPoint => focalPoint;
        public Rect SourceUv => sourceUv;
        public Rect AppliedUv => uvRect;
        public Rect SafeCropInsetsNormalized => safeCropInsetsNormalized;
        public bool SafeCropSatisfied => safeCropSatisfied;
        public float SourceAspectRatio => texture == null || sourceUv.height <= 0f
            ? 0f
            : sourceUv.width * texture.width / (sourceUv.height * texture.height);

        public void Configure(Sprite sprite, Vector2 authoredFocalPoint, Rect authoredSafeInsets)
        {
            if (sprite == null || sprite.texture == null)
            {
                configured = false;
                texture = null;
                return;
            }

            texture = sprite.texture;
            Rect textureRect = sprite.textureRect;
            float textureWidth = Mathf.Max(1f, sprite.texture.width);
            float textureHeight = Mathf.Max(1f, sprite.texture.height);
            sourceUv = new Rect(
                textureRect.x / textureWidth,
                textureRect.y / textureHeight,
                textureRect.width / textureWidth,
                textureRect.height / textureHeight);
            focalPoint = new Vector2(Mathf.Clamp01(authoredFocalPoint.x), Mathf.Clamp01(authoredFocalPoint.y));
            safeCropInsetsNormalized = new Rect(
                Mathf.Clamp01(authoredSafeInsets.x),
                Mathf.Clamp01(authoredSafeInsets.y),
                Mathf.Clamp01(authoredSafeInsets.width),
                Mathf.Clamp01(authoredSafeInsets.height));
            configured = true;
            color = Color.white;
            raycastTarget = false;
            RecalculateCrop();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RecalculateCrop();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            RecalculateCrop();
        }

        private void RecalculateCrop()
        {
            if (!configured || texture == null) return;
            RectTransform target = rectTransform;
            float targetWidth = Mathf.Abs(target.rect.width);
            float targetHeight = Mathf.Abs(target.rect.height);
            float sourceWidth = sourceUv.width * texture.width;
            float sourceHeight = sourceUv.height * texture.height;
            if (targetWidth <= 0.01f || targetHeight <= 0.01f || sourceWidth <= 0.01f || sourceHeight <= 0.01f) return;

            float targetAspect = targetWidth / targetHeight;
            float sourceAspect = sourceWidth / sourceHeight;
            Rect result = sourceUv;
            float focalX = sourceUv.x + sourceUv.width * focalPoint.x;
            float focalY = sourceUv.y + sourceUv.height * focalPoint.y;
            if (targetAspect > sourceAspect)
            {
                float visibleHeight = sourceUv.width * sourceAspect / targetAspect;
                result.height = Mathf.Min(sourceUv.height, visibleHeight);
                result.y = Mathf.Clamp(focalY - result.height * 0.5f, sourceUv.y, sourceUv.yMax - result.height);
            }
            else if (targetAspect < sourceAspect)
            {
                float visibleWidth = sourceUv.height * targetAspect / sourceAspect;
                result.width = Mathf.Min(sourceUv.width, visibleWidth);
                result.x = Mathf.Clamp(focalX - result.width * 0.5f, sourceUv.x, sourceUv.xMax - result.width);
            }

            Rect protectedRegion = new Rect(
                sourceUv.x + sourceUv.width * safeCropInsetsNormalized.x,
                sourceUv.y + sourceUv.height * safeCropInsetsNormalized.y,
                sourceUv.width * (1f - safeCropInsetsNormalized.x - safeCropInsetsNormalized.width),
                sourceUv.height * (1f - safeCropInsetsNormalized.y - safeCropInsetsNormalized.height));
            if (protectedRegion.width >= 0f && protectedRegion.height >= 0f)
            {
                result.x = ShiftCropToContain(result.x, result.width, sourceUv.x, sourceUv.xMax, protectedRegion.xMin, protectedRegion.xMax);
                result.y = ShiftCropToContain(result.y, result.height, sourceUv.y, sourceUv.yMax, protectedRegion.yMin, protectedRegion.yMax);
            }

            uvRect = result;
            safeCropSatisfied = protectedRegion.width >= 0f && protectedRegion.height >= 0f
                && result.xMin <= protectedRegion.xMin + 0.0001f
                && result.yMin <= protectedRegion.yMin + 0.0001f
                && result.xMax >= protectedRegion.xMax - 0.0001f
                && result.yMax >= protectedRegion.yMax - 0.0001f;
            SetVerticesDirty();
        }

        private static float ShiftCropToContain(float cropStart, float cropSize, float sourceStart, float sourceEnd, float protectedStart, float protectedEnd)
        {
            if (cropSize + 0.0001f < protectedEnd - protectedStart) return cropStart;
            float minimumStart = Mathf.Max(sourceStart, protectedEnd - cropSize);
            float maximumStart = Mathf.Min(sourceEnd - cropSize, protectedStart);
            return minimumStart <= maximumStart ? Mathf.Clamp(cropStart, minimumStart, maximumStart) : cropStart;
        }
    }
}
#endif
