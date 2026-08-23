#if UNITY_2019_4_OR_NEWER
using UnityEngine;

namespace ES.UI
{
    /// <summary>
    /// Visual-only responsive layout switch. It selects one authored profile root by aspect ratio.
    /// It does not own business state or window lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESUIAdaptiveLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform wideProfile;
        [SerializeField] private RectTransform narrowProfile;
        [SerializeField, Min(0.1f)] private float narrowAspectThreshold = 1.15f;

        private RectTransform cachedRect;
        private bool lastNarrow;

        private void Awake()
        {
            cachedRect = transform as RectTransform;
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

        private void Apply()
        {
            if (cachedRect == null) cachedRect = transform as RectTransform;
            if (cachedRect == null || wideProfile == null || narrowProfile == null) return;

            float width = cachedRect.rect.width;
            float height = cachedRect.rect.height;
            if (width <= 0f || height <= 0f) return;

            bool narrow = width / height < narrowAspectThreshold;
            if (narrow == lastNarrow && (wideProfile.gameObject.activeSelf == !narrow)) return;
            lastNarrow = narrow;
            wideProfile.gameObject.SetActive(!narrow);
            narrowProfile.gameObject.SetActive(narrow);
        }

        public void Configure(RectTransform wide, RectTransform narrow, float threshold)
        {
            wideProfile = wide;
            narrowProfile = narrow;
            narrowAspectThreshold = Mathf.Max(0.1f, threshold);
            cachedRect = transform as RectTransform;
            Apply();
        }
    }
}
#endif
