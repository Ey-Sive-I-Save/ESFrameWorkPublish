using UnityEngine;

namespace ES
{
    /// <summary>代码热更新验收：在较小垂直范围内缓慢浮动。</summary>
    public sealed class ESHotUpdateLoopMoveVertical : MonoBehaviour
    {
        private const float SpeedMultiplier = 3f;
        private const float MoveRange = 0.42f;
        private Vector3 origin;
        private float phase;

        private void Awake()
        {
            origin = transform.localPosition;
            phase = origin.x * 0.23f + origin.z * 0.29f;
        }

        private void Update()
        {
            Vector3 position = origin;
            position.y += Mathf.Sin(Time.time * SpeedMultiplier + phase) * MoveRange;
            transform.localPosition = position;
        }
    }
}
