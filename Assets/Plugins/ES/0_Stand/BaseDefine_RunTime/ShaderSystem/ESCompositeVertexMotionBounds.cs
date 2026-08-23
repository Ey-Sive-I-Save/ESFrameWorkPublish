using UnityEngine;

namespace ES
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("【ES】/相机与表现/Composite Vertex Motion Bounds")]
    public sealed class ESCompositeVertexMotionBounds : MonoBehaviour
    {
        [SerializeField] private Vector3 padding = new Vector3(0.25f, 0.25f, 0.05f);
        [SerializeField] private bool updateEveryFrame;

        private Renderer targetRenderer;

        public Vector3 Padding => padding;
        public bool UpdateEveryFrame => updateEveryFrame;

        private void OnEnable()
        {
            RefreshBounds();
        }

        private void LateUpdate()
        {
            if (updateEveryFrame) RefreshBounds();
        }

        private void OnDisable()
        {
            if (targetRenderer != null) targetRenderer.ResetLocalBounds();
        }

        private void OnValidate()
        {
            padding = new Vector3(
                Mathf.Max(0f, padding.x),
                Mathf.Max(0f, padding.y),
                Mathf.Max(0f, padding.z));
            if (isActiveAndEnabled) RefreshBounds();
        }

        public void Configure(Vector3 boundsPadding, bool refreshEveryFrame = false)
        {
            padding = new Vector3(
                Mathf.Max(0f, boundsPadding.x),
                Mathf.Max(0f, boundsPadding.y),
                Mathf.Max(0f, boundsPadding.z));
            updateEveryFrame = refreshEveryFrame;
            if (isActiveAndEnabled) RefreshBounds();
        }

        public void RefreshBounds()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null) return;

            targetRenderer.ResetLocalBounds();
            Bounds bounds = targetRenderer.localBounds;
            bounds.Expand(padding * 2f);
            targetRenderer.localBounds = bounds;
        }
    }
}
