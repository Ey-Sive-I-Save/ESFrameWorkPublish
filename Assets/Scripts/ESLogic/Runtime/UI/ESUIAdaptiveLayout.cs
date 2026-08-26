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
        private bool hasProfileOverride;
        private bool overrideNarrow;

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

            // Fixture capture may request a deterministic profile while it changes
            // the canvas dimensions repeatedly. Keep that explicit choice stable
            // until the driver clears the override; runtime screens remain fully
            // aspect-ratio driven when no override is active.
            if (hasProfileOverride)
            {
                lastNarrow = overrideNarrow;
                wideProfile.gameObject.SetActive(!overrideNarrow);
                narrowProfile.gameObject.SetActive(overrideNarrow);
                return;
            }

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

        /// <summary>Locks one authored profile for deterministic fixture capture.</summary>
        public void SetProfileOverride(bool enabled, bool narrow)
        {
            hasProfileOverride = enabled;
            overrideNarrow = narrow;
            if (enabled)
            {
                cachedRect = transform as RectTransform;
                Apply();
            }
        }
    }
}
#endif
