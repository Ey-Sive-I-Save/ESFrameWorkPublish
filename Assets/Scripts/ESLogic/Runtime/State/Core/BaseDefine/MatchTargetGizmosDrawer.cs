#if UNITY_EDITOR
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// ============================================================================
// 文件：MatchTargetGizmosDrawer.cs（Editor Only）
// 作用：将 StateBase.ProcessMatchTarget 每帧计算的 MatchTarget 数据
//       以 Gizmos 形式渲染在 Scene 视图中，方便调试对齐效果。
//
// 使用方式：
//   Play Mode 下自动创建单例 GameObject，无需手动挂载。
//   StateBase.ProcessMatchTarget 在 #if UNITY_EDITOR 块里调用
//   MatchTargetGizmosDrawer.Submit(key, data) 即可。
//
// Gizmos 图例（简化版默认）：
//   ■ 深蓝球    Root 的目标点（effectiveTargetPos）
//   ■ 绿球      非 Root 的目标点（effectiveTargetPos）
//   ■ 青球      当前帧实际位置（newPos）
//   ■ 黑球      骨骼目标点 mt.position（仅 bodyPart≠Root）
//   ─ 黄线      当前位置 → 终点
//   T 标签      默认关闭；可按需开启
// ============================================================================

namespace ES
{
    [AddComponentMenu("")]   // 不在 Add Component 菜单中显示
    public class MatchTargetGizmosDrawer : MonoBehaviour
    {
        // ── 单例 ────────────────────────────────────────────────────────────
        private static MatchTargetGizmosDrawer _instance;

        public static MatchTargetGizmosDrawer Instance
        {
            get
            {
                if (_instance == null) EnsureInstance();
                return _instance;
            }
        }

        /// <summary>确保单例存在（Play Mode 首帧自动调用）。</summary>
        public static void EnsureInstance()
        {
            if (_instance != null) return;
            if (!Application.isPlaying) return;
            var go = new GameObject("[MatchTargetGizmosDrawer]") { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            _instance = go.AddComponent<MatchTargetGizmosDrawer>();
        }

        // ── 每帧数据结构 ─────────────────────────────────────────────────────
        public struct FrameData
        {
            /// <summary>状态名 + bodyPart，用于 Label 显示。</summary>
            public string       label;
            /// <summary>窗口首帧 motor.TransientPosition 快照。</summary>
            public Vector3      snapshotPos;
            /// <summary>有效终点位置（Root=live，非Root=首帧固定快照）。</summary>
            public Vector3      effectiveTargetPos;
            /// <summary>当前帧写入 motor 的位置。</summary>
            public Vector3      currentPos;
            /// <summary>有效终点旋转。</summary>
            public Quaternion   effectiveTargetRot;
            /// <summary>当前帧写入 motor 的旋转。</summary>
            public Quaternion   currentRot;
            /// <summary>mt.position（骨骼目标点，仅 bodyPart≠Root 时与 effectiveTargetPos 不同）。</summary>
            public Vector3      boneTargetPos;
            /// <summary>liveOffset = entityTrs.position - bone.position（Root时为零）。</summary>
            public Vector3      liveOffset;
            /// <summary>当前帧 bone.position（骨骼实际世界坐标，bodyPart=Root 时为零）。</summary>
            public Vector3      boneWorldPos;
            /// <summary>entityTrs.position（Entity 视觉渲染位置，非 TransientPosition）。</summary>
            public Vector3      entityTrsPos;
            public AvatarTarget bodyPart;
            /// <summary>归一化进度 [0,1]。</summary>
            public float        t;
            public float        posErr;
            public float        rotErr;
            /// <summary>提交时的帧编号，用于过期剔除。</summary>
            public int          submitFrame;
        }

        // key = 状态名_stateId，防止多状态同帧数据互相覆盖
        private readonly Dictionary<string, FrameData> _dataMap  = new Dictionary<string, FrameData>();
        private readonly List<string>                  _toRemove = new List<string>();

        // ── Inspector 配置 ────────────────────────────────────────────────
        [Header("显示开关")]
        [LabelText("起始快照球")]
        public bool showSnapshotSphere  = false;
        [LabelText("终点球")]
        public bool showTargetSphere    = true;
        [LabelText("当前位置球")]
        public bool showCurrentSphere   = true;
        [LabelText("连线")]
        public bool showLines           = true;
        [LabelText("旋转箭头")]
        public bool showRotationArrows  = false;
        [LabelText("骨骼目标点")]
        public bool showBoneTarget      = true;
        [LabelText("骨骼实际位置")]
        public bool showBoneWorldPos    = false;
        [LabelText("实体渲染位置")]
        public bool showEntityTrsPos    = false;
        [LabelText("文字标签")]
        public bool showLabels          = false;

        [Header("颜色")]
        [LabelText("起始快照（灰）")]
        public Color colorSnapshot    = new Color(0.55f, 0.55f, 0.55f, 0.85f);
        [LabelText("终点（绿）")]
        public Color colorTarget      = new Color(0.15f, 1.00f, 0.15f, 0.95f);
        [LabelText("Root终点（深蓝）")]
        public Color colorRootTarget  = new Color(0.08f, 0.16f, 0.45f, 0.95f);
        [LabelText("当前位置（青）")]
        public Color colorCurrent     = new Color(0.15f, 0.80f, 1.00f, 0.95f);
        [LabelText("目标旋转（橙）")]
        public Color colorTargetRot   = new Color(1.00f, 0.45f, 0.05f, 0.95f);
        [LabelText("当前旋转（浅蓝）")]
        public Color colorCurrentRot  = new Color(0.45f, 0.80f, 1.00f, 0.80f);
        [LabelText("起→终连线（灰）")]
        public Color colorLineSnap    = new Color(0.55f, 0.55f, 0.55f, 0.50f);
        [LabelText("当前→终连线（黄）")]
        public Color colorLineCur     = new Color(1.00f, 1.00f, 0.00f, 0.70f);
        [LabelText("骨骼目标点（黑）")]
        public Color colorBoneTarget  = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        [LabelText("骨骼实际位置（金）")]
        public Color colorBoneWorld   = new Color(1.00f, 0.85f, 0.10f, 0.95f);
        [LabelText("实体渲染位置（白）")]
        public Color colorEntityTrs   = new Color(0.95f, 0.95f, 0.95f, 0.90f);
        [LabelText("偏移向量（红）")]
        public Color colorLiveOffset  = new Color(1.00f, 0.20f, 0.20f, 0.70f);

        [Header("大小")]
        [LabelText("球体半径")]
        public float sphereRadius  = 0.07f;
        [LabelText("箭头长度")]
        public float arrowLength   = 0.55f;

        [Header("可见性增强")]
        [LabelText("数据保留帧数"), Min(1)]
        [Tooltip("超过该帧数未更新的数据会被剔除。增大可减少偶发闪烁。")]
        public int staleFrameRetention = 20;

        [LabelText("启用屏幕自适应尺寸")]
        [Tooltip("根据 Scene 相机距离自动放大/缩小 Gizmo 尺寸，远处也能看清。")]
        public bool useAdaptiveScale = true;

        [LabelText("最小显示缩放"), Range(0.5f, 3f)]
        public float minScale = 1f;

        [LabelText("最大显示缩放"), Range(1f, 8f)]
        public float maxScale = 4f;

        [LabelText("标签穿透显示(X-Ray)")]
        [Tooltip("开启后标签不受遮挡，始终可见。")]
        public bool xrayLabels = true;

        [LabelText("显示详细标签")]
        [Tooltip("显示 target/current/snapshot/liveOffset 等完整信息。")]
        public bool showVerboseLabels = false;

        // ── 对外提交接口（由 StateBase.ProcessMatchTarget 调用）──────────────
        /// <summary>
        /// 提交本帧 MatchTarget 数据。同一 key 每帧覆盖旧数据。<br/>
        /// <paramref name="key"/> 建议使用 "状态名_stateId" 保证唯一。
        /// </summary>
        public static void Submit(string key, FrameData data)
        {
            if (_instance == null) EnsureInstance();
            if (_instance == null) return;
            data.submitFrame = Time.frameCount;
            _instance._dataMap[key] = data;
        }

        // ── Unity 生命周期 ────────────────────────────────────────────────
        private void Awake()
        {
            bool isUtilityHost = gameObject.name == "[MatchTargetGizmosDrawer]"
                                 && (gameObject.hideFlags & HideFlags.DontSave) != 0;

            // 防误挂：如果该组件被挂到玩家/场景对象上，立即转移为独立隐藏实例，避免把宿主错误常驻。
            if (!isUtilityHost)
            {
                if (_instance == this) _instance = null;
                EnsureInstance();
                Destroy(this);
                return;
            }

            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnDrawGizmos()
        {
            // 剔除超过 8 帧未更新的条目（对应状态已退出或 MatchTarget 已完成）
            _toRemove.Clear();
            foreach (var kv in _dataMap)
                if (Time.frameCount - kv.Value.submitFrame > staleFrameRetention)
                    _toRemove.Add(kv.Key);
            foreach (var k in _toRemove) _dataMap.Remove(k);

            foreach (var kv in _dataMap)
                DrawEntry(kv.Value);
        }

        private void DrawEntry(in FrameData d)
        {
            float scale = ResolveScale(d.effectiveTargetPos);
            float drawSphereRadius = sphereRadius * scale;
            float drawArrowLength = arrowLength * scale;

            // ── 起始快照（灰球）───────────────────────────────────────────
            if (showSnapshotSphere)
            {
                Gizmos.color = colorSnapshot;
                Gizmos.DrawSphere(d.snapshotPos, drawSphereRadius * 0.75f);
            }

            // ── 有效终点（绿球 + 橙色旋转箭头）──────────────────────────
            if (showTargetSphere)
            {
                Gizmos.color = d.bodyPart == AvatarTarget.Root ? colorRootTarget : colorTarget;
                Gizmos.DrawSphere(d.effectiveTargetPos, drawSphereRadius);
            }

            if (showRotationArrows)
            {
                // 目标旋转 forward（橙）
                Gizmos.color = colorTargetRot;
                Vector3 tgtFwd = d.effectiveTargetRot * Vector3.forward;
                Gizmos.DrawRay(d.effectiveTargetPos, tgtFwd * drawArrowLength);
            }

            // ── 当前位置（青球 + 浅蓝旋转箭头）──────────────────────────
            if (showCurrentSphere)
            {
                Gizmos.color = colorCurrent;
                Gizmos.DrawSphere(d.currentPos, drawSphereRadius * 0.60f);

                if (showRotationArrows)
                {
                    Gizmos.color = colorCurrentRot;
                    Vector3 curFwd = d.currentRot * Vector3.forward;
                    Gizmos.DrawRay(d.currentPos, curFwd * (drawArrowLength * 0.65f));
                }
            }

            // ── 连线：起→终（灰），当前→终（黄）──────────────────────────
            if (showLines)
            {
                Gizmos.color = colorLineCur;
                Gizmos.DrawLine(d.currentPos, d.effectiveTargetPos);
            }

            // ── 原始目标点（mt.position）及 liveOffset 向量 ────────────────
            // 无论 Root 还是非 Root，都绘制 raw target，避免“看不到目标点本体”。
            if (showBoneTarget && d.bodyPart != AvatarTarget.Root)
            {
                // boneTargetPos = mt.position（原始 MatchTarget 目标点）
                Gizmos.color = colorBoneTarget;
                Gizmos.DrawSphere(d.boneTargetPos, drawSphereRadius * 0.55f);
                if (showLabels)
                    DrawLabel(d.boneTargetPos + Vector3.up * (drawSphereRadius * 1.6f), "rawTarget(mt.position)");

                // liveOffset 向量：raw target -> effective target（仅非Root且有偏移时绘制）
                if (d.bodyPart != AvatarTarget.Root && d.liveOffset.sqrMagnitude > 1e-6f)
                    Gizmos.DrawLine(d.boneTargetPos, d.effectiveTargetPos);
            }

            // ── bone.position（当前骨骼实际世界坐标，金球）──────────────────
            if (showBoneWorldPos && d.bodyPart != AvatarTarget.Root && d.boneWorldPos != Vector3.zero)
            {
                Gizmos.color = colorBoneWorld;
                Gizmos.DrawSphere(d.boneWorldPos, drawSphereRadius * 0.65f);
                Gizmos.DrawWireSphere(d.boneWorldPos, drawSphereRadius * 1.0f);
                if (showLabels)
                    DrawLabel(d.boneWorldPos + Vector3.up * (drawSphereRadius * 1.8f), "骨骼位置");
            }

            // ── entityTrs.position（Entity 视觉渲染坐标，白球）──────────────
            if (showEntityTrsPos)
            {
                Gizmos.color = colorEntityTrs;
                Gizmos.DrawSphere(d.entityTrsPos, drawSphereRadius * 0.55f);
                if (showLabels)
                    DrawLabel(d.entityTrsPos + Vector3.up * (drawSphereRadius * 1.8f), "实体渲染位置");

                // 红线：entityTrs.position → bone.position（liveOffset 的反向，直观显示偏移量）
                if (d.bodyPart != AvatarTarget.Root && d.boneWorldPos != Vector3.zero)
                {
                    Gizmos.color = colorLiveOffset;
                    Gizmos.DrawLine(d.entityTrsPos, d.boneWorldPos);
                }
            }

            // ── 文字标签 ────────────────────────────────────────────────
            if (showLabels)
            {
                string txt;
                if (showVerboseLabels)
                {
                    txt =
                        $"{d.label}\n" +
                        $"部位: {d.bodyPart}  进度={d.t:F2}\n" +
                        $"rawTarget={d.boneTargetPos:F3}\n" +
                        $"target={d.effectiveTargetPos:F3}\n" +
                        $"current={d.currentPos:F3}\n" +
                        $"snapshot={d.snapshotPos:F3}\n" +
                        $"liveOffset={d.liveOffset:F3}  |off|={d.liveOffset.magnitude:F3}\n" +
                        $"posErr={d.posErr:F4}m  rotErr={d.rotErr:F2}°";
                }
                else
                {
                    txt =
                        $"{d.label}\n" +
                        $"部位: {d.bodyPart}\n" +
                        $"进度 = {d.t:F2}\n" +
                        $"位置误差 = {d.posErr:F4} m\n" +
                        $"旋转误差 = {d.rotErr:F2}°";
                }

                DrawLabel(d.effectiveTargetPos + Vector3.up * (drawSphereRadius * 2.8f), txt);
            }
        }

        private float ResolveScale(Vector3 worldPos)
        {
            if (!useAdaptiveScale)
                return 1f;

            Camera cam = SceneView.lastActiveSceneView != null
                ? SceneView.lastActiveSceneView.camera
                : Camera.current;

            if (cam == null)
                return 1f;

            float size = HandleUtility.GetHandleSize(worldPos);
            if (size <= 0.0001f)
                return 1f;

            return Mathf.Clamp(size, minScale, maxScale);
        }

        private void DrawLabel(Vector3 worldPos, string text)
        {
            if (xrayLabels)
            {
                var old = Handles.zTest;
                Handles.zTest = CompareFunction.Always;
                Handles.Label(worldPos, text, EditorStyles.whiteMiniLabel);
                Handles.zTest = old;
                return;
            }

            Handles.Label(worldPos, text, EditorStyles.whiteMiniLabel);
        }
    }
}
#endif
