using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 项目 3D 物理 Layer 的唯一数值定义。
    /// 名称与碰撞矩阵规则由 GameCoreEditorGlobalData 和编辑器同步工具维护。
    /// </summary>
    public static class ESPhysicsLayers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int EntityBody = 6;
        public const int Ground = 8;
        public const int Wall = 9;
        public const int WorldDynamic = 10;
        public const int EntityHurtbox = 11;
        public const int ItemBody = 12;
        public const int Interaction = 13;
        public const int TriggerZone = 14;
        public const int Shot = 15;
        public const int CameraBlocker = 16;
        public const int Sensor = 17;

        public const int DefaultMask = 1 << Default;
        public const int IgnoreRaycastMask = 1 << IgnoreRaycast;
        public const int WaterMask = 1 << Water;
        public const int UIMask = 1 << UI;
        public const int EntityBodyMask = 1 << EntityBody;
        public const int GroundMask = 1 << Ground;
        public const int WallMask = 1 << Wall;
        public const int WorldDynamicMask = 1 << WorldDynamic;
        public const int EntityHurtboxMask = 1 << EntityHurtbox;
        public const int ItemBodyMask = 1 << ItemBody;
        public const int InteractionMask = 1 << Interaction;
        public const int TriggerZoneMask = 1 << TriggerZone;
        public const int ShotMask = 1 << Shot;
        public const int CameraBlockerMask = 1 << CameraBlocker;
        public const int SensorMask = 1 << Sensor;

        public const int WorldBlockerMask = GroundMask | WallMask | WorldDynamicMask | ItemBodyMask;
        public const int MovementMask = EntityBodyMask | WorldBlockerMask;
        public const int GroundProbeMask = GroundMask | WorldDynamicMask | ItemBodyMask;
        public const int ShotHitMask = GroundMask | WallMask | WorldDynamicMask | EntityHurtboxMask | ItemBodyMask;
        public const int MeleeHitMask = EntityHurtboxMask | ItemBodyMask;
        public const int InteractionProbeMask = InteractionMask;
        public const int TriggerZoneProbeMask = WaterMask | TriggerZoneMask;
        public const int CameraObstacleMask = WorldBlockerMask | CameraBlockerMask;
        public const int AIVisibilityMask = WorldBlockerMask;
        public const int AITargetMask = EntityHurtboxMask;
        public const int ClimbProbeMask = WallMask | WorldDynamicMask;
        public const int MountProbeMask = InteractionMask;
        public const int FootIKMask = GroundMask | WorldDynamicMask | ItemBodyMask;

        /// <summary>
        /// 兼容历史数据中的 ~0：飞行物绝不能因为“全层”默认值扫描到自身、交互盒或纯表现 Collider。
        /// 需要更窄命中范围时可在具体 Shot 数据中填写明确 LayerMask。
        /// </summary>
        public static LayerMask GetShotHitMask(LayerMask configuredMask)
        {
            return configuredMask.value == ~0 ? ShotHitMask : configuredMask;
        }

        public static LayerMask GetQueryMask(GameCorePhysicsQueryRole roles)
        {
            int mask = 0;

            if ((roles & GameCorePhysicsQueryRole.Movement) != 0)
                mask |= MovementMask;
            if ((roles & GameCorePhysicsQueryRole.GroundProbe) != 0)
                mask |= GroundProbeMask;
            if ((roles & GameCorePhysicsQueryRole.ShotHit) != 0)
                mask |= ShotHitMask;
            if ((roles & GameCorePhysicsQueryRole.MeleeHit) != 0)
                mask |= MeleeHitMask;
            if ((roles & GameCorePhysicsQueryRole.InteractionProbe) != 0)
                mask |= InteractionProbeMask;
            if ((roles & GameCorePhysicsQueryRole.TriggerZoneProbe) != 0)
                mask |= TriggerZoneProbeMask;
            if ((roles & GameCorePhysicsQueryRole.CameraObstacle) != 0)
                mask |= CameraObstacleMask;
            if ((roles & GameCorePhysicsQueryRole.AIVisibility) != 0)
                mask |= AIVisibilityMask;
            if ((roles & GameCorePhysicsQueryRole.AITarget) != 0)
                mask |= AITargetMask;
            if ((roles & GameCorePhysicsQueryRole.ClimbProbe) != 0)
                mask |= ClimbProbeMask;
            if ((roles & GameCorePhysicsQueryRole.MountProbe) != 0)
                mask |= MountProbeMask;
            if ((roles & GameCorePhysicsQueryRole.FootIK) != 0)
                mask |= FootIKMask;

            return mask;
        }
    }

    [Serializable]
    public struct ESPhysicsQueryStats
    {
        public int raycastCount;
        public int sphereCastCount;
        public int boxCastCount;
        public int capsuleCastCount;
        public int overlapSphereCount;
        public int overlapBoxCount;
        public int overlapCapsuleCount;
        public int overflowCount;

        public void Clear()
        {
            raycastCount = 0;
            sphereCastCount = 0;
            boxCastCount = 0;
            capsuleCastCount = 0;
            overlapSphereCount = 0;
            overlapBoxCount = 0;
            overlapCapsuleCount = 0;
            overflowCount = 0;
        }
    }

    [Serializable]
    public sealed class ESPhysicsLayerConfig
    {
        [Title("通用")]
        [LabelText("场景阻挡")]
        public LayerMask worldBlockLayers = ESPhysicsLayers.WorldBlockerMask;

        [LabelText("角色身体")]
        public LayerMask entityBodyLayers = ESPhysicsLayers.EntityBodyMask;

        [LabelText("角色受击")]
        public LayerMask entityHurtboxLayers = ESPhysicsLayers.EntityHurtboxMask;

        [LabelText("Item物体")]
        public LayerMask itemBodyLayers = ESPhysicsLayers.ItemBodyMask;

        [LabelText("交互")]
        public LayerMask interactionLayers = ESPhysicsLayers.InteractionMask;

        [LabelText("陷阱/区域")]
        public LayerMask triggerZoneLayers = ESPhysicsLayers.TriggerZoneMask;

        [Title("组合")]
        [LabelText("运动阻挡")]
        public LayerMask movementLayers = ESPhysicsLayers.MovementMask;

        [LabelText("地面探测")]
        public LayerMask groundProbeLayers = ESPhysicsLayers.GroundProbeMask;

        [LabelText("飞行物命中")]
        public LayerMask shotHitLayers = ESPhysicsLayers.ShotHitMask;

        [LabelText("近战命中")]
        public LayerMask meleeHitLayers = ESPhysicsLayers.MeleeHitMask;

        [LabelText("交互探测")]
        public LayerMask interactionProbeLayers = ESPhysicsLayers.InteractionProbeMask;

        [LabelText("区域/水体探测")]
        public LayerMask triggerZoneProbeLayers = ESPhysicsLayers.TriggerZoneProbeMask;

        [LabelText("相机避障")]
        public LayerMask cameraObstacleLayers = ESPhysicsLayers.CameraObstacleMask;

        [LabelText("AI 视线遮挡")]
        public LayerMask aiVisibilityLayers = ESPhysicsLayers.AIVisibilityMask;

        [LabelText("AI 目标")]
        public LayerMask aiTargetLayers = ESPhysicsLayers.AITargetMask;

        [LabelText("攀爬探测")]
        public LayerMask climbProbeLayers = ESPhysicsLayers.ClimbProbeMask;

        [LabelText("骑乘探测")]
        public LayerMask mountProbeLayers = ESPhysicsLayers.MountProbeMask;

        [LabelText("脚部 IK")]
        public LayerMask footIKLayers = ESPhysicsLayers.FootIKMask;
    }

    [Serializable]
    [TypeRegistryItem("物理查询模块")]
    public sealed class ESPhysicsQueryModule : ESRuntimeModule
    {
        [Title("配置")]
        [HideLabel]
        public ESPhysicsLayerConfig layers = new ESPhysicsLayerConfig();

        [LabelText("默认Trigger策略")]
        public QueryTriggerInteraction defaultTriggerInteraction = QueryTriggerInteraction.Collide;

        [LabelText("共享Ray缓存容量")]
        [MinValue(1)]
        public int sharedRaycastCapacity = 32;

        [LabelText("共享Collider缓存容量")]
        [MinValue(1)]
        public int sharedColliderCapacity = 64;

        [Title("运行统计")]
        [ShowInInspector, ReadOnly]
        public ESPhysicsQueryStats stats;

        private RaycastHit[] sharedRaycastHits;
        private Collider[] sharedColliders;

        public RaycastHit[] SharedRaycastHits
        {
            get
            {
                EnsureRaycastBuffer();
                return sharedRaycastHits;
            }
        }

        public Collider[] SharedColliders
        {
            get
            {
                EnsureColliderBuffer();
                return sharedColliders;
            }
        }

        public override void Start()
        {
            EnsureBuffers();
        }

        public int Raycast(Vector3 origin, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0 || direction.sqrMagnitude <= 0.0001f)
                return 0;

            stats.raycastCount++;
            int count = Physics.RaycastNonAlloc(origin, direction.normalized, results, Mathf.Max(0f, distance), layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int Raycast(Vector3 origin, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results)
        {
            return Raycast(origin, direction, distance, layerMask, results, defaultTriggerInteraction);
        }

        public int RaycastShared(Vector3 origin, Vector3 direction, float distance, LayerMask layerMask)
        {
            EnsureRaycastBuffer();
            return Raycast(origin, direction, distance, layerMask, sharedRaycastHits, defaultTriggerInteraction);
        }

        public int SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0 || direction.sqrMagnitude <= 0.0001f)
                return 0;

            stats.sphereCastCount++;
            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(0f, radius), direction.normalized, results, Mathf.Max(0f, distance), layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask layerMask, RaycastHit[] results)
        {
            return SphereCast(origin, radius, direction, distance, layerMask, results, defaultTriggerInteraction);
        }

        public int SphereCastShared(Vector3 origin, float radius, Vector3 direction, float distance, LayerMask layerMask)
        {
            EnsureRaycastBuffer();
            return SphereCast(origin, radius, direction, distance, layerMask, sharedRaycastHits, defaultTriggerInteraction);
        }

        public int OverlapSphere(Vector3 center, float radius, LayerMask layerMask, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0)
                return 0;

            stats.overlapSphereCount++;
            int count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0f, radius), results, layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int OverlapSphere(Vector3 center, float radius, LayerMask layerMask, Collider[] results)
        {
            return OverlapSphere(center, radius, layerMask, results, defaultTriggerInteraction);
        }

        public int OverlapSphereShared(Vector3 center, float radius, LayerMask layerMask)
        {
            EnsureColliderBuffer();
            return OverlapSphere(center, radius, layerMask, sharedColliders, defaultTriggerInteraction);
        }

        public int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, LayerMask layerMask, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0)
                return 0;

            stats.overlapBoxCount++;
            Vector3 safeHalfExtents = new Vector3(
                Mathf.Max(0f, halfExtents.x),
                Mathf.Max(0f, halfExtents.y),
                Mathf.Max(0f, halfExtents.z));
            int count = Physics.OverlapBoxNonAlloc(center, safeHalfExtents, results, orientation, layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int OverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, LayerMask layerMask, Collider[] results)
        {
            return OverlapBox(center, halfExtents, orientation, layerMask, results, defaultTriggerInteraction);
        }

        public int OverlapBoxShared(Vector3 center, Vector3 halfExtents, Quaternion orientation, LayerMask layerMask)
        {
            EnsureColliderBuffer();
            return OverlapBox(center, halfExtents, orientation, layerMask, sharedColliders, defaultTriggerInteraction);
        }

        public int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, LayerMask layerMask,
            Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0)
                return 0;

            stats.overlapCapsuleCount++;
            int count = Physics.OverlapCapsuleNonAlloc(point0, point1, Mathf.Max(0f, radius), results,
                layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int BoxCast(Vector3 origin, Vector3 halfExtents, Vector3 direction, float distance,
            Quaternion orientation, LayerMask layerMask, RaycastHit[] results,
            QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0 || direction.sqrMagnitude <= 0.0001f)
                return 0;

            stats.boxCastCount++;
            int count = Physics.BoxCastNonAlloc(origin, halfExtents, results, direction.normalized,
                orientation, Mathf.Max(0f, distance), layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int CapsuleCast(Vector3 point0, Vector3 point1, float radius, Vector3 direction,
            float distance, LayerMask layerMask, RaycastHit[] results,
            QueryTriggerInteraction triggerInteraction)
        {
            if (results == null || results.Length == 0 || direction.sqrMagnitude <= 0.0001f)
                return 0;

            stats.capsuleCastCount++;
            int count = Physics.CapsuleCastNonAlloc(point0, point1, Mathf.Max(0f, radius),
                direction.normalized, results, Mathf.Max(0f, distance), layerMask, triggerInteraction);
            TrackOverflow(count, results.Length);
            return count;
        }

        public int ShotCast(Vector3 from, Vector3 to, float radius, LayerMask layerMask, RaycastHit[] results, QueryTriggerInteraction triggerInteraction)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return 0;

            return radius > 0.0001f
                ? SphereCast(from, radius, delta, distance, layerMask, results, triggerInteraction)
                : Raycast(from, delta, distance, layerMask, results, triggerInteraction);
        }

        public int ShotCast(Vector3 from, Vector3 to, float radius, LayerMask layerMask, RaycastHit[] results)
        {
            return ShotCast(from, to, radius, layerMask, results, defaultTriggerInteraction);
        }

        public bool TryGetNearestShotHit(Vector3 from, Vector3 to, float radius, LayerMask layerMask, RaycastHit[] buffer, QueryTriggerInteraction triggerInteraction, out RaycastHit nearestHit)
        {
            nearestHit = default;
            int count = ShotCast(from, to, radius, layerMask, buffer, triggerInteraction);
            return TrySelectNearestHit(buffer, count, out nearestHit);
        }

        public bool TryFindBestInteraction(Vector3 origin, Vector3 forward, float radius, float maxAngle, LayerMask layerMask, Collider[] buffer, QueryTriggerInteraction triggerInteraction, out Collider bestCollider)
        {
            bestCollider = null;
            int count = OverlapSphere(origin, radius, layerMask, buffer, triggerInteraction);
            if (count <= 0)
                return false;

            bool useAngle = forward.sqrMagnitude > 0.0001f && maxAngle > 0f && maxAngle < 180f;
            Vector3 forwardNormal = useAngle ? forward.normalized : Vector3.forward;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < count && i < buffer.Length; i++)
            {
                Collider candidate = buffer[i];
                if (candidate == null)
                    continue;

                Vector3 candidatePoint = candidate.ClosestPoint(origin);
                Vector3 toCandidate = candidatePoint - origin;
                float distance = toCandidate.magnitude;
                if (distance > radius)
                    continue;

                float angle = 0f;
                if (useAngle && toCandidate.sqrMagnitude > 0.0001f)
                {
                    angle = Vector3.Angle(forwardNormal, toCandidate);
                    if (angle > maxAngle)
                        continue;
                }

                float score = distance + angle * 0.01f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestCollider = candidate;
                }
            }

            return bestCollider != null;
        }

        public int TrapOverlapSphere(Vector3 center, float radius, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            return OverlapSphere(center, radius, layers.triggerZoneLayers, results, triggerInteraction);
        }

        public int TrapOverlapBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, Collider[] results, QueryTriggerInteraction triggerInteraction)
        {
            return OverlapBox(center, halfExtents, orientation, layers.triggerZoneLayers, results, triggerInteraction);
        }

        public void ClearStats()
        {
            stats.Clear();
        }

        public void EnsureBuffers()
        {
            EnsureRaycastBuffer();
            EnsureColliderBuffer();
        }

        private void EnsureRaycastBuffer()
        {
            int capacity = Mathf.Max(1, sharedRaycastCapacity);
            if (sharedRaycastHits == null || sharedRaycastHits.Length != capacity)
                sharedRaycastHits = new RaycastHit[capacity];
        }

        private void EnsureColliderBuffer()
        {
            int capacity = Mathf.Max(1, sharedColliderCapacity);
            if (sharedColliders == null || sharedColliders.Length != capacity)
                sharedColliders = new Collider[capacity];
        }

        private void TrackOverflow(int count, int capacity)
        {
            if (count >= capacity)
                stats.overflowCount++;
        }

        private static bool TrySelectNearestHit(RaycastHit[] hits, int count, out RaycastHit nearestHit)
        {
            nearestHit = default;
            if (hits == null || count <= 0)
                return false;

            float nearestDistance = float.PositiveInfinity;
            int safeCount = Mathf.Min(count, hits.Length);
            for (int i = 0; i < safeCount; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.distance >= nearestDistance)
                    continue;

                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            return nearestHit.collider != null;
        }
    }
}
