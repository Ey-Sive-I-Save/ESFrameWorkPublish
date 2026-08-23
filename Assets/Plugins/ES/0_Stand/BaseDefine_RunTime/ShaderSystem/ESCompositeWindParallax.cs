using UnityEngine;

namespace ES
{
    /// <summary>
    /// 把对象世界 X 写入风相位，避免多个对象以完全相同的相位摆动。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("【ES】/相机与表现/Composite Wind Parallax")]
    public sealed class ESCompositeWindParallax : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private bool followMovement;
        [SerializeField] private float phaseScale = 1f;

        private MaterialPropertyBlock propertyBlock;
        private float lastPhase = float.NaN;

        private void Reset()
        {
            ResolveTarget();
        }

        private void OnEnable()
        {
            ResolveTarget();
            Refresh();
        }

        private void OnDisable()
        {
            WritePhase(0f);
            lastPhase = float.NaN;
        }

        private void LateUpdate()
        {
            if (followMovement)
                Refresh();
        }

        public void Refresh()
        {
            float phase = transform.position.x * phaseScale;
            if (Mathf.Approximately(phase, lastPhase))
                return;
            WritePhase(phase);
            lastPhase = phase;
        }

        private void WritePhase(float phase)
        {
            if (targetRenderer == null)
                return;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            ESCompositeURPProperties.SetWindPhaseOffset(propertyBlock, phase);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void ResolveTarget()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
        }
    }
}
