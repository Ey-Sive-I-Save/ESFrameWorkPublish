using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 可攀爬表面类型
    /// </summary>
    public enum ClimbableSurfaceType
    {
        [InspectorName("普通墙壁（可持续攀爬）")]
        Wall,

        [InspectorName("矮墙台阶（可直接翻越）")]
        LowWall,

        [InspectorName("边缘顶部（可攀上）")]
        Ledge,
    }

    /// <summary>
    /// 可攀爬表面定义脚本 - 挂载在场景中可攀爬的物体上
    /// 
    /// 功能：
    /// 1. 定义该物体为可攀爬目标，被攀爬模块检测
    /// 2. 默认使用本地长方体区域作为可攀爬范围（Gizmo 可视化）
    /// 3. 定义攀爬面类型（持续攀爬/翻越/攀上）
    /// 4. 可选的 MatchTarget 锚点（翻越/攀上的对齐目标点）
    /// 5. 支持双面攀爬（自动检测角色接近方向）
    /// 6. 攀爬速度乘数、粗糙度影响
    /// </summary>
    [AddComponentMenu("ES/Climbing/Climbable Surface")]
    public class ClimbableSurface : MonoBehaviour
    {
        // ===== 基础配置 =====

        [Title("表面配置")]
        [LabelText("表面类型")]
        public ClimbableSurfaceType surfaceType = ClimbableSurfaceType.Wall;

        [Title("攀爬区域")]
        [LabelText("区域中心(本地)")]
        public Vector3 areaCenter = new Vector3(0f, 1f, 0f);

        [LabelText("区域尺寸(本地)"), MinValue(0.1f)]
        public Vector3 areaSize = new Vector3(2f, 2f, 0.5f);

        [Title("法线方向")]
        [LabelText("启用双面"), Tooltip("默认开启；基于玩家位置自动翻转法线")]
        public bool enableBilateral = true;

        [LabelText("使用自定义法线"), Tooltip("启用后使用自定义本地法线，否则使用模拟法线")]
        public bool useManualNormal = false;

        [LabelText("模拟法线(本地空间)"), Tooltip("本地空间；默认使用 -transform.forward")]
        [ShowIf("@useManualNormal == true")]
        public Vector3 customLocalNormal = Vector3.back; // 本地空间，默认-Z = 法线朝forward反方向

        [LabelText("双面判定中心"), Tooltip("双面墙判断玩家在哪一侧用的参考点；不填则使用物体中心")]
        public Transform normalCenter;

        [LabelText("中心偏移(本地)"), Tooltip("未设置中心时使用该偏移作为参考点")]
        public Vector3 normalCenterOffset = Vector3.zero;

        [LabelText("双面翻转阈值(度)"), Tooltip("基准中心→玩家方向与法线夹角超过该值则翻转；建议30-90")]
        [Range(0f, 180f)]
        public float bilateralFlipAngle = 90f;

        [LabelText("允许攀爬高度范围"), Tooltip("保留字段：默认由长方体区域决定")]
        public Vector2 climbHeightRange = new Vector2(0f, 20f);

        [LabelText("顶部偏移(本地Y)"), Tooltip("正值向上，负值向下")]
        public float topHeightOffset = 0f;

        [LabelText("底部偏移(本地Y)"), Tooltip("正值向下，负值向上")]
        public float bottomHeightOffset = 0f;

        // ===== 速度/表面特性 =====

        [Title("表面特性")]
        [LabelText("攀爬速度乘数"), Tooltip("在此表面攀爬时的速度倍率（0.5=半速, 1=正常, 1.5=加速）")]
        [Range(0.1f, 3f)]
        public float speedMultiplier = 1f;

        [LabelText("表面粗糙度"), Tooltip("0=光滑, 1=极度粗糙；影响攀爬速度波动和滑动抵抗")]
        [Range(0f, 1f)]
        public float roughness = 0f;

        // ===== 翻越/攀上配置 =====

        [Title("翻越/攀上设置"), ShowIf("@surfaceType != ClimbableSurfaceType.Wall")]
        [LabelText("MatchTarget锚点"), Tooltip("翻越/攀上时角色对齐到的目标点(手部位置)")]
        public Transform matchTargetAnchor;

        [ShowIf("@surfaceType != ClimbableSurfaceType.Wall")]
        [LabelText("翻越后着地点"), Tooltip("翻越完成后角色的落脚位置")]
        public Transform landingPoint;

        [ShowIf("@surfaceType == ClimbableSurfaceType.LowWall")]
        [LabelText("矮墙高度(米)"), Tooltip("实际矮墙高度，用于判断是否可以直接翻越")]
        public float wallHeight = 1.2f;

        [ShowIf("@surfaceType != ClimbableSurfaceType.Wall")]
        [LabelText("攀上目标点内缩深度(米)"),
         Tooltip("GetMatchTargetPosition 计算时将目标点从墙面外边缘向内（平台内侧）收缩的距离。\n" +
                 "默认 0.3m：确保目标在平台顶面而非悬挂边缘，骨骼对齐后脚部踩实地面。\n" +
                 "若使用 matchTargetAnchor 则此值无效。")]
        public float matchTargetTopInset = 0.3f;

        private const float DefaultLandingExtraOffset = 0.2f;

        // MatchTarget 射线探测：缓存本物体及父物体链的碰撞体（两个方向都要找），避免每帧分配
        [NonSerialized] private Collider[] _cachedSurfaceColliders;
        // Physics.RaycastNonAlloc 复用缓冲区（Unity 单线程，static 共享安全）
        private static readonly RaycastHit[] _sharedRayBuffer = new RaycastHit[32];

        private void EnsureSurfaceColliders()
        {
            if (_cachedSurfaceColliders != null) return;
            var set = new System.Collections.Generic.HashSet<Collider>();
            // 子物体（含自身）
            foreach (var c in GetComponentsInChildren<Collider>(true))
                set.Add(c);
            // 父物体链：ClimbableSurface 常挂在空的触发子节点上，实体 Collider 在父物体
            foreach (var c in GetComponentsInParent<Collider>(true))
                set.Add(c);
            _cachedSurfaceColliders = new Collider[set.Count];
            set.CopyTo(_cachedSurfaceColliders);
        }

        // ===== 运行时接口 =====

        /// <summary>
        /// 获取模拟法线（世界空间）
        /// 用于无物理信息时的基础法线
        /// </summary>
        public Vector3 GetSimulatedNormal()
        {
            if (useManualNormal)
            {
                return transform.TransformDirection(customLocalNormal).normalized;
            }
            return -transform.forward;
        }

        /// <summary>
        /// 判断世界点是否在攀爬区域内
        /// </summary>
        public bool IsPointInside(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector3 half = GetLocalHalfSize();
            return Mathf.Abs(local.x - areaCenter.x) <= half.x
                && Mathf.Abs(local.y - areaCenter.y) <= half.y
                && Mathf.Abs(local.z - areaCenter.z) <= half.z;
        }

        /// <summary>
        /// 获取攀爬区域的最近点（世界坐标）
        /// </summary>
        public Vector3 GetClosestPointInArea(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector3 half = GetLocalHalfSize();
            Vector3 clamped = new Vector3(
                Mathf.Clamp(local.x, areaCenter.x - half.x, areaCenter.x + half.x),
                Mathf.Clamp(local.y, areaCenter.y - half.y, areaCenter.y + half.y),
                Mathf.Clamp(local.z, areaCenter.z - half.z, areaCenter.z + half.z));
            return transform.TransformPoint(clamped);
        }

        /// <summary>
        /// 根据角色位置获取动态法线（支持双面）
        /// 双面模式下选择与“基准中心→玩家方向”夹角更小的法线
        /// </summary>
        public Vector3 GetDynamicNormal(Vector3 characterPosition)
        {
            Vector3 baseNormal = GetSimulatedNormal();
            if (baseNormal.sqrMagnitude < 0.0001f)
            {
                baseNormal = -transform.forward;
            }
            baseNormal = baseNormal.normalized;

            if (enableBilateral)
            {
                // 角色在墙的哪一侧？以基准中心为参考，并对齐玩家高度，减少上下偏差
                Vector3 center = normalCenter != null ? normalCenter.position : transform.TransformPoint(normalCenterOffset);
                center.y = characterPosition.y;

                Vector3 toChar = characterPosition - center;
                toChar.y = 0f;
                if (toChar.sqrMagnitude < 0.0001f)
                {
                    return baseNormal;
                }

                Vector3 dir = toChar.normalized;
                Vector3 n = baseNormal;
                Vector3 nInv = -baseNormal;

                // 同一坐标系下比较“方向”与法线/反法线的夹角，选夹角更小的
                float angleN = Vector3.Angle(dir, n);
                float angleInv = Vector3.Angle(dir, nInv);
                return angleN <= angleInv ? n : nInv;
            }

            return baseNormal;
        }

        /// <summary>
        /// 获取攀爬面上的最近点（使用长方体区域 + 动态法线的墙面）
        /// </summary>
        public Vector3 GetClosestClimbPoint(Vector3 characterPosition)
        {
            Vector3 normal = GetDynamicNormal(characterPosition);
            Vector3 localNormal = transform.InverseTransformDirection(normal).normalized;
            Vector3 local = transform.InverseTransformPoint(characterPosition);
            Vector3 half = GetLocalHalfSize();

            Vector3 clamped = new Vector3(
                Mathf.Clamp(local.x, areaCenter.x - half.x, areaCenter.x + half.x),
                Mathf.Clamp(local.y, areaCenter.y - half.y, areaCenter.y + half.y),
                Mathf.Clamp(local.z, areaCenter.z - half.z, areaCenter.z + half.z));

            int axis = GetPrimaryAxis(localNormal);
            if (axis == 0)
            {
                clamped.x = areaCenter.x + Mathf.Sign(localNormal.x) * half.x;
            }
            else if (axis == 1)
            {
                clamped.y = areaCenter.y + Mathf.Sign(localNormal.y) * half.y;
            }
            else
            {
                clamped.z = areaCenter.z + Mathf.Sign(localNormal.z) * half.z;
            }

            return transform.TransformPoint(clamped);
        }

        /// <summary>
        /// 获取MatchTarget的目标位置
        /// 优先使用锚点Transform，否则使用物体顶部
        /// </summary>
        public Vector3 GetMatchTargetPosition()
        {
            if (matchTargetAnchor != null)
                return matchTargetAnchor.position;
            Vector3 local = areaCenter;
            Vector3 half = GetLocalHalfSize();
            local.y = areaCenter.y + half.y;
            return transform.TransformPoint(local);
        }

        /// <summary>
        /// 获取 MatchTarget 的目标位置（双面感知 + 物理顶面精确版）。
        ///
        /// 核心思路：
        ///  1. XZ = GetClosestClimbPoint → 角色投影到墙面贴合点，切线轴夹紧到区域范围
        ///  2. Y  = Physics.RaycastNonAlloc 向下找物理顶面（不受"起点在碰撞体内"的限制）
        ///         备选：遍历每个 Collider bounds 中心 XZ 再射
        ///         兜底：逻辑区域顶部 Y
        /// </summary>
        public Vector3 GetMatchTargetPosition(Vector3 characterPosition)
        {
            if (matchTargetAnchor != null)
                return matchTargetAnchor.position;

            EnsureSurfaceColliders();

            // ── Step 1: XZ ─────────────────────────────────────────────────────────
            Vector3 facePoint = GetClosestClimbPoint(characterPosition);
            // 可选：沿法线向内缩进（0 = 不缩进；正值将骨骼目标拉进平台内侧）
            if (matchTargetTopInset > 0.001f)
                facePoint -= GetDynamicNormal(characterPosition).normalized * matchTargetTopInset;

            // ── Step 2: Y ──────────────────────────────────────────────────────────
            float topY = GetTopWorldY();
            float scanY = topY + 2f;
            foreach (var col in _cachedSurfaceColliders)
                if (col != null && !col.isTrigger)
                    scanY = Mathf.Max(scanY, col.bounds.max.y + 0.5f);

            float rayLen = scanY - topY + 3f;

            // 首选：facePoint XZ 向下
            if (TryRaycastOwnSurface(new Vector3(facePoint.x, scanY, facePoint.z), Vector3.down, rayLen, out RaycastHit hit))
            {
#if UNITY_EDITOR
                Debug.Log($"[ClimbableSurface] GetMatchTarget 命中 point={hit.point:F3} col={hit.collider.name}");
#endif
                return hit.point;
            }

            // 备选：每个碰撞体 bounds 中心 XZ（应对 areaConfig 与碰撞体位置不匹配）
            foreach (var col in _cachedSurfaceColliders)
            {
                if (col == null || col.isTrigger) continue;
                Vector3 bc = col.bounds.center;
                if (TryRaycastOwnSurface(new Vector3(bc.x, scanY, bc.z), Vector3.down, rayLen, out hit))
                {
#if UNITY_EDITOR
                    Debug.Log($"[ClimbableSurface] GetMatchTarget 备选命中(bounds中心) point={hit.point:F3} col={hit.collider.name}");
#endif
                    return hit.point;
                }
            }

            // 兜底
#if UNITY_EDITOR
            Debug.LogWarning($"[ClimbableSurface] GetMatchTarget 全部未命中，兜底逻辑顶Y！\n" +
                $"  name={name}  facePoint={facePoint:F3}  topY={topY:F3}  colliders={_cachedSurfaceColliders.Length}\n" +
                $"  请检查: 1)Collider非Trigger 2)Collider在子/父节点 3)areaCenter/areaSize与碰撞体对齐");
#endif
            return new Vector3(facePoint.x, topY, facePoint.z);
        }

        /// <summary>
        /// 用 Physics.RaycastNonAlloc 射线，筛选属于本物体（含子/父）的最近命中点。
        /// Physics.RaycastNonAlloc 不受"起点在碰撞体内部=无命中"的限制（Collider.Raycast有此问题）。
        /// </summary>
        private bool TryRaycastOwnSurface(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit bestHit)
        {
            EnsureSurfaceColliders();
            int count = Physics.RaycastNonAlloc(origin, direction, _sharedRayBuffer, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            bestHit = default;
            float closestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _sharedRayBuffer[i];
                foreach (var col in _cachedSurfaceColliders)
                {
                    if (col == h.collider && h.distance < closestDist)
                    {
                        closestDist = h.distance;
                        bestHit = h;
                        found = true;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// 返回平台顶部 Y 与角色脚部 Y 的高度差（米）。
        /// 供攀爬模块据此选择不同高度段的攀上动画/MatchTarget 配置。
        /// </summary>
        public float GetWallHeightAboveCharacter(Vector3 characterPosition)
        {
            return GetTopWorldY() - characterPosition.y;
        }

        /// <summary>
        /// 检查指定世界点正上方是否具备足够净空（防止攀上时角色撞头）。
        /// </summary>
        /// <param name="originWorld">检测起点（通常为攀上目标手部位置）</param>
        /// <param name="requiredHeight">所需净空高度（米），建议 1.8~2.2</param>
        /// <param name="layerMask">碰撞检测层级</param>
        /// <returns>true = 净空充足，可以攀上；false = 被天花板遮挡，应阻止攀上</returns>
        public bool HasCeilingClearance(Vector3 originWorld, float requiredHeight, LayerMask layerMask)
        {
            return !Physics.Raycast(originWorld, Vector3.up, requiredHeight, layerMask);
        }

        /// <summary>
        /// 获取MatchTarget的目标旋转（面朝攀爬面法线反方向 = 面朝墙壁）
        /// </summary>
        public Quaternion GetMatchTargetRotation()
        {
            if (matchTargetAnchor != null)
                return matchTargetAnchor.rotation;
            Vector3 faceDir = -GetSimulatedNormal();
            if (faceDir.sqrMagnitude < 0.001f) faceDir = Vector3.forward;
            return Quaternion.LookRotation(faceDir, Vector3.up);
        }

        /// <summary>
        /// 获取MatchTarget的目标旋转（双面感知版本）
        /// </summary>
        public Quaternion GetMatchTargetRotation(Vector3 characterPosition)
        {
            if (matchTargetAnchor != null)
                return matchTargetAnchor.rotation;
            Vector3 faceDir = -GetDynamicNormal(characterPosition);
            if (faceDir.sqrMagnitude < 0.001f) faceDir = Vector3.forward;
            return Quaternion.LookRotation(faceDir, Vector3.up);
        }

        /// <summary>
        /// 获取翻越后着地位置
        /// </summary>
        public Vector3 GetLandingPosition()
        {
            if (landingPoint != null)
                return landingPoint.position;
            // 默认：沿法线反方向偏移一个身位
            return transform.position - GetSimulatedNormal() * 1f;
        }

        /// <summary>
        /// 获取翻越后着地位置（双面感知版本：角色从哪侧翻越，就落向另一侧）
        /// </summary>
        public Vector3 GetLandingPosition(Vector3 characterPosition)
        {
            if (landingPoint != null)
                return landingPoint.position;
            // 双面：基于动态法线，落到墙体另一侧（自动映射，无需额外配置）
            Vector3 approachNormal = GetDynamicNormal(characterPosition).normalized;
            Vector3 localNormal = transform.InverseTransformDirection(approachNormal).normalized;
            Vector3 half = GetLocalHalfSize();
            int axis = GetPrimaryAxis(localNormal);
            float halfThickness = axis == 0 ? half.x : (axis == 1 ? half.y : half.z);

            Vector3 surfacePoint = GetClosestClimbPoint(characterPosition);
            float travel = halfThickness * 2f + DefaultLandingExtraOffset;
            return surfacePoint - approachNormal * travel;
        }

        /// <summary>
        /// 检查角色位置是否在高度范围内
        /// </summary>
        public bool IsInHeightRange(float characterY)
        {
            Vector3 half = GetLocalHalfSize();
            Vector3 top = transform.TransformPoint(areaCenter + Vector3.up * (half.y + topHeightOffset));
            Vector3 bottom = transform.TransformPoint(areaCenter - Vector3.up * (half.y + bottomHeightOffset));
            float minY = Mathf.Min(top.y, bottom.y);
            float maxY = Mathf.Max(top.y, bottom.y);
            bool inRange = characterY >= minY && characterY <= maxY;
            Debug.Log(string.Format(
                "[ClimbableSurface] IsInHeightRange -> inRange={0}, characterY={1:F3}, minY={2:F3}, maxY={3:F3}, areaCenterLocal={4}, areaSizeLocal={5}, halfLocal={6}, topOffset={7:F3}, bottomOffset={8:F3}, topWorld={9}, bottomWorld={10}",
                inRange,
                characterY,
                minY,
                maxY,
                areaCenter,
                areaSize,
                half,
                topHeightOffset,
                bottomHeightOffset,
                top,
                bottom));
            return inRange;
        }

        /// <summary>
        /// 检查角色是否接近攀爬面顶部（可以攀上）
        /// </summary>
        public bool IsNearTop(float characterY, float threshold = 0.5f)
        {
            Vector3 half = GetLocalHalfSize();
            Vector3 top = transform.TransformPoint(areaCenter + Vector3.up * (half.y + topHeightOffset));
            return characterY >= (top.y - threshold);
        }

        /// <summary>
        /// 获取攀爬区域顶部的世界Y坐标
        /// </summary>
        public float GetTopWorldY()
        {
            Vector3 half = GetLocalHalfSize();
            Vector3 top = transform.TransformPoint(areaCenter + Vector3.up * (half.y + topHeightOffset));
            return top.y;
        }

        private Vector3 GetLocalHalfSize()
        {
            Vector3 size = areaSize;
            size.x = Mathf.Max(0.1f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.1f, Mathf.Abs(size.y));
            size.z = Mathf.Max(0.1f, Mathf.Abs(size.z));
            return size * 0.5f;
        }

        private int GetPrimaryAxis(Vector3 localNormal)
        {
            float ax = Mathf.Abs(localNormal.x);
            float ay = Mathf.Abs(localNormal.y);
            float az = Mathf.Abs(localNormal.z);
            if (ax >= ay && ax >= az) return 0;
            if (ay >= ax && ay >= az) return 1;
            return 2;
        }

        /// <summary>
        /// 获取在此表面的有效攀爬速度
        /// 综合考虑 speedMultiplier 和 roughness
        /// roughness 会在基础速度上叠加正弦波动（模拟凹凸不平的摸索感）
        /// </summary>
        /// <param name="baseSpeed">模块的基础攀爬速度</param>
        /// <returns>实际攀爬速度</returns>
        public float GetEffectiveSpeed(float baseSpeed)
        {
            float speed = baseSpeed * speedMultiplier;

            if (roughness > 0.01f)
            {
                // 粗糙度越高，波动越大，平均速度越低
                // 幅度： roughness * 0.4 → 最大降低 40%
                // 頻率：用 Time.time * 随机偏移
                float wave = Mathf.Sin(Time.time * (3f + roughness * 7f)) * roughness * 0.4f;
                // 保证最低不低于 30%
                float factor = Mathf.Max(0.3f, 1f - roughness * 0.3f + wave);
                speed *= factor;
            }

            return speed;
        }

#if UNITY_EDITOR
        [Title("调试")]
        [LabelText("显示Gizmo")]
        public bool showGizmo = true;

        [LabelText("显示登顶线"), ShowIf("showGizmo")]
        public bool showTopLine = true;

        [LabelText("登顶阈值(米)"), ShowIf("showTopLine"), Tooltip("仅用于Gizmo显示")]
        public float topReachThreshold = 0.5f;

        [LabelText("打印登顶计算"), ShowIf("showTopLine")]
        public bool logTopCalc = false;

        private void OnDrawGizmosSelected()
        {
            if (!showGizmo) return;

            // 攀爬区域
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(areaCenter, areaSize);
            Gizmos.matrix = oldMatrix;

            // 表面法线
            Vector3 normal = GetSimulatedNormal();
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, normal * 2f);

            // 双面模式：同时画反向法线
            if (enableBilateral)
            {
                Gizmos.color = new Color(0f, 0.8f, 0.8f, 0.5f);
                Gizmos.DrawRay(transform.position, -normal * 2f);
            }

            // MatchTarget锚点
            if (matchTargetAnchor != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(matchTargetAnchor.position, 0.15f);
            }

            // 着地点
            if (landingPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(landingPoint.position, 0.15f);
            }

            if (showTopLine)
            {
                Vector3 center = transform.TransformPoint(areaCenter);
                Vector3 half = GetLocalHalfSize();
                Vector3 lineDir = transform.right * half.x;

                float topY = GetTopWorldY();
                float thresholdY = topY - topReachThreshold;

                Gizmos.color = new Color(1f, 0.85f, 0f, 0.8f);
                Vector3 topCenter = new Vector3(center.x, topY, center.z);
                Gizmos.DrawLine(topCenter - lineDir, topCenter + lineDir);

                Gizmos.color = new Color(1f, 0.4f, 0f, 0.8f);
                Vector3 thresholdCenter = new Vector3(center.x, thresholdY, center.z);
                Gizmos.DrawLine(thresholdCenter - lineDir, thresholdCenter + lineDir);

                if (logTopCalc)
                {
                    Debug.Log($"[登顶] GizmoTop: name={name} center={center:F3} areaCenter={areaCenter:F3} areaSize={areaSize:F3} " +
                        $"topY={topY:F3} thresholdY={thresholdY:F3} topOffset={topHeightOffset:F3} bottomOffset={bottomHeightOffset:F3} " +
                        $"lossyScale={transform.lossyScale:F3}");
                }
            }
        }
#endif
    }
}
