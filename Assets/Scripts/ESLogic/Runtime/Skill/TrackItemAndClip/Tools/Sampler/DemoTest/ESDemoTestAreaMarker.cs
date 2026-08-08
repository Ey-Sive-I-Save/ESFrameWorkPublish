using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public enum ESDemoTestAreaStatus
    {
        Ready = 0,
        Planned = 1,
        Blocked = 2,
    }

    /// <summary>
    /// Demo/Test 场景中的显式测试区域标识。
    /// 仅保存区域说明并绘制 Gizmo，不参与运行时调度或每帧更新。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ESDemoTestAreaMarker : MonoBehaviour
    {
        [SerializeField, Min(1)] private int areaNumber = 1;
        [SerializeField] private string areaId = "area";
        [SerializeField] private string areaTitle = "测试区域";
        [SerializeField] private string category = "General";
        [SerializeField] private ESDemoTestAreaStatus status;
        [SerializeField, TextArea(2, 5)] private string description;
        [SerializeField] private Color markerColor = new Color(0.28f, 0.82f, 1f, 1f);
        [SerializeField] private Vector3 localBounds = new Vector3(8f, 2f, 8f);
        [SerializeField] private bool showGizmo = true;

        public int AreaNumber => areaNumber;
        public string AreaId => areaId;
        public string AreaTitle => areaTitle;
        public string Category => category;
        public ESDemoTestAreaStatus Status => status;
        public string Description => description;
        public Color MarkerColor => markerColor;

        public void ConfigureForAuthoring(
            int number,
            string id,
            string title,
            string areaCategory,
            ESDemoTestAreaStatus areaStatus,
            string areaDescription,
            Color color,
            Vector3 bounds)
        {
            areaNumber = Mathf.Max(1, number);
            areaId = id ?? string.Empty;
            areaTitle = title ?? string.Empty;
            category = areaCategory ?? string.Empty;
            status = areaStatus;
            description = areaDescription ?? string.Empty;
            markerColor = color;
            localBounds = new Vector3(
                Mathf.Max(0.1f, Mathf.Abs(bounds.x)),
                Mathf.Max(0.1f, Mathf.Abs(bounds.y)),
                Mathf.Max(0.1f, Mathf.Abs(bounds.z)));
        }

        private void OnValidate()
        {
            areaNumber = Mathf.Max(1, areaNumber);
            localBounds = new Vector3(
                Mathf.Max(0.1f, Mathf.Abs(localBounds.x)),
                Mathf.Max(0.1f, Mathf.Abs(localBounds.y)),
                Mathf.Max(0.1f, Mathf.Abs(localBounds.z)));
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showGizmo)
                return;

            Color fill = markerColor;
            fill.a = 0.08f;
            Gizmos.color = fill;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(Vector3.up * localBounds.y * 0.5f, localBounds);

            Color wire = markerColor;
            wire.a = 0.9f;
            Gizmos.color = wire;
            Gizmos.DrawWireCube(Vector3.up * localBounds.y * 0.5f, localBounds);
            Gizmos.matrix = Matrix4x4.identity;

            Handles.color = wire;
            Handles.Label(
                transform.position + Vector3.up * (localBounds.y + 0.8f),
                $"{areaNumber:00}. {areaTitle} [{status}]",
                EditorStyles.boldLabel);
        }
#endif
    }
}
