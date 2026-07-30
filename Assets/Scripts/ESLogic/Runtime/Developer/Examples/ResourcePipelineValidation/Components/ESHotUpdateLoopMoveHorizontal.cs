using UnityEngine;

namespace ES
{
    /// <summary>代码热更新验收：在较小水平范围内缓慢往返。</summary>
    public sealed class ESHotUpdateLoopMoveHorizontal : MonoBehaviour
    {
        private const float SpeedMultiplier = 3.5f;
        private const float MoveRange = 0.65f;
        private Vector3 origin;
        private float phase;

        private void Awake()
        {
            origin = transform.localPosition;
            phase = origin.x * 0.31f + origin.z * 0.17f;
        }

        private void Update()
        {
            Vector3 position = origin;
            position.x += Mathf.Sin(Time.time * SpeedMultiplier + phase) * MoveRange;
            transform.localPosition = position;
        }
    }
}
