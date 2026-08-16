using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    public enum ESShotHitDecision : byte
    {
        Ignore = 0,
        Stop = 1,
        Pierce = 2
    }

    public enum ESShotLifecycleKind : byte
    {
        Launched = 0,
        Hit = 1,
        Arrived = 2,
        Expired = 3,
        Stopped = 4,
        Despawned = 5
    }

    public readonly struct ESShotLaunchContext
    {
        public readonly int attackId;
        public readonly Entity owner;
        public readonly ESInstanceHandle sourceItemHandle;
        public readonly ESWeaponConfigKey sourceWeaponKey;
        public readonly EntityPrimaryAttackSelection attackSelection;
        public readonly Transform target;
        public readonly IESShotHitResolver hitResolver;
        public readonly Action<ESShotLifecycleEvent> lifecycleObserver;

        public ESShotLaunchContext(
            int attackId,
            Entity owner,
            ESInstanceHandle sourceItemHandle,
            ESWeaponConfigKey sourceWeaponKey,
            EntityPrimaryAttackSelection attackSelection,
            Transform target = null,
            IESShotHitResolver hitResolver = null,
            Action<ESShotLifecycleEvent> lifecycleObserver = null)
        {
            this.attackId = attackId;
            this.owner = owner;
            this.sourceItemHandle = sourceItemHandle;
            this.sourceWeaponKey = sourceWeaponKey;
            this.attackSelection = attackSelection;
            this.target = target;
            this.hitResolver = hitResolver;
            this.lifecycleObserver = lifecycleObserver;
        }
    }

    public readonly struct ESShotLifecycleEvent
    {
        public readonly ESShotLifecycleKind kind;
        public readonly ItemShotModule shot;
        public readonly ESShotLaunchContext context;
        public readonly ShotMotionResult motion;
        public readonly ShotHitCandidate hit;
        public readonly ESShotHitDecision hitDecision;

        public ESShotLifecycleEvent(
            ESShotLifecycleKind kind,
            ItemShotModule shot,
            in ESShotLaunchContext context,
            in ShotMotionResult motion,
            in ShotHitCandidate hit = default,
            ESShotHitDecision hitDecision = ESShotHitDecision.Ignore)
        {
            this.kind = kind;
            this.shot = shot;
            this.context = context;
            this.motion = motion;
            this.hit = hit;
            this.hitDecision = hitDecision;
        }
    }

    public interface IESShotHitResolver
    {
        ESShotHitDecision Resolve(
            in ESShotLaunchContext context,
            ItemShotSharedData definition,
            in ShotHitCandidate candidate);
    }

    public sealed class ESDefaultShotHitResolver : IESShotHitResolver
    {
        public static readonly ESDefaultShotHitResolver Instance = new ESDefaultShotHitResolver();

        private ESDefaultShotHitResolver() { }

        public ESShotHitDecision Resolve(
            in ESShotLaunchContext context,
            ItemShotSharedData definition,
            in ShotHitCandidate candidate)
        {
            Collider collider = candidate.collider;
            if (collider == null)
                return ESShotHitDecision.Ignore;

            Transform hitTransform = collider.transform;
            if (context.owner != null
                && (hitTransform == context.owner.transform || hitTransform.IsChildOf(context.owner.transform)))
                return ESShotHitDecision.Ignore;

            ShotBlockMode blockMode = definition?.blockMode ?? ShotBlockMode.AnyBlocker;
            bool isWorldBlocker = (ESPhysicsLayers.WorldBlockerMask & (1 << candidate.layer)) != 0;
            if (isWorldBlocker)
                return blockMode == ShotBlockMode.None
                    ? ESShotHitDecision.Ignore
                    : ESShotHitDecision.Stop;

            Entity target = collider.GetComponentInParent<Entity>();
            if (target == null)
                return blockMode == ShotBlockMode.AnyBlocker
                    ? ESShotHitDecision.Stop
                    : ESShotHitDecision.Ignore;

            if (definition?.hitTagEligibility != null)
            {
                if (!definition.hitTagEligibility.TryAllows(
                        context.owner,
                        target,
                        out ESHitTagEligibilityResult eligibility,
                        out string error))
                {
                    Debug.LogError("[ItemShot] Hit Tag eligibility failed: " + error, collider);
                    return ESShotHitDecision.Ignore;
                }
                if (eligibility != ESHitTagEligibilityResult.Allowed)
                    return ESShotHitDecision.Ignore;
            }

            return blockMode == ShotBlockMode.AnyBlocker
                ? ESShotHitDecision.Stop
                : ESShotHitDecision.Pierce;
        }
    }

    public static class ESShotSpawner
    {
        public static bool TrySpawn(
            ESShotConfigKey shotKey,
            Vector3 origin,
            Vector3 direction,
            in ESShotLaunchContext context,
            out ItemShotModule shot,
            out string error)
        {
            shot = null;
            error = null;
            if (shotKey == null || !shotKey.IsConfigured
                || !ESRuntimeDataGameCore.Shots.TryGetRuntimeKey(shotKey, out int runtimeKey)
                || !ESRuntimeDataGameCore.Shots.TryGet(runtimeKey, out ESShotRuntimeData runtimeData)
                || runtimeData == null
                || !runtimeData.Ready
                || runtimeData.sharedData == null)
            {
                error = "Shot Key 未解析到可用的 ESShotRuntimeData。";
                return false;
            }
            if (runtimeData.prefabKey == null || !runtimeData.prefabKey.IsConfigured)
            {
                error = "Shot RuntimeData 缺少 Prefab Key。";
                return false;
            }
            bool requiresTarget = runtimeData.sharedData.aimMode == ShotAimMode.Target
                || runtimeData.sharedData.aimMode == ShotAimMode.MustHit
                || (runtimeData.defaultVariableData.forceMustHit && runtimeData.sharedData.allowMustHit);
            if (requiresTarget && context.target == null)
            {
                error = "Target/MustHit Shot 必须提供有效目标。";
                return false;
            }
            if (!ESGameManager.TryGetModule(out ESGameObjectPoolModule pool) || pool == null)
            {
                error = "GameObject Pool 模块不可用。";
                return false;
            }
            if (!ESGameManager.RuntimePrefabAssets.TryAcquireReady(
                    runtimeData.prefabKey,
                    out ESAssetConfigPayloadLease<GameObject> prefabLease))
            {
                error = "Shot Prefab 尚未由资源计划预热。";
                return false;
            }

            GameObject instance = null;
            try
            {
                Vector3 useDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
                instance = pool.GetInPool(
                    prefabLease.Asset,
                    origin,
                    Quaternion.LookRotation(useDirection, Vector3.up),
                    null,
                    false,
                    0f);
                if (instance == null)
                {
                    error = "对象池拒绝创建 Shot 实例。";
                    prefabLease.Dispose();
                    return false;
                }

                Item item = instance.GetComponent<Item>();
                shot = item != null ? item.basicDomain?.FindMyModule<ItemShotModule>() : null;
                if (item == null || shot == null)
                {
                    error = "Shot Prefab 必须在根节点提供 Item 和 ItemShotModule。";
                    pool.PushToPool(instance);
                    prefabLease.Dispose();
                    shot = null;
                    return false;
                }

                shot.Internal_InitializeSpawn(runtimeData, runtimeKey, prefabLease, context);
                bool launched = context.target != null
                    ? shot.LaunchTo(context.target)
                    : shot.Launch(useDirection);
                if (launched)
                    return true;

                error = "ItemShotModule 拒绝 Launch。";
                shot.Internal_Stop(false);
                shot = null;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                if (instance != null)
                    pool.PushToPool(instance);
                prefabLease.Dispose();
                shot = null;
                return false;
            }
        }
    }

    public struct ItemShotHitQuery
    {
        public Vector3 from;
        public Vector3 to;
        public float radius;
        public LayerMask hitLayers;
        public QueryTriggerInteraction triggerInteraction;
    }

    public interface IItemShotHitSolver
    {
        int Query(in ItemShotHitQuery query, ShotHitCandidate[] results, int maxResults);
        bool IsOverflow { get; }
    }

    public interface IItemShotTickPolicy
    {
        bool ShouldTick(in ShotMotionState state, int frameCount);
    }

    public sealed class ItemShotAlwaysTickPolicy : IItemShotTickPolicy
    {
        public bool ShouldTick(in ShotMotionState state, int frameCount)
        {
            return state.launched;
        }
    }

    public sealed class ItemShotPhysicsHitSolver : IItemShotHitSolver
    {
        private const int MaximumPhysicsHitCapacity = 256;
        private RaycastHit[] _hitBuffer;

        public bool IsOverflow { get; private set; }

        public ItemShotPhysicsHitSolver(int capacity)
        {
            EnsureCapacity(capacity);
        }

        public int Query(in ItemShotHitQuery query, ShotHitCandidate[] results, int maxResults)
        {
            IsOverflow = false;
            if (results == null || maxResults <= 0)
                return 0;

            maxResults = Mathf.Min(maxResults, results.Length);
            if (maxResults <= 0)
                return 0;

            EnsureCapacity(maxResults);

            Vector3 delta = query.to - query.from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return 0;

            LayerMask hitLayers = ESPhysicsLayers.GetShotHitMask(query.hitLayers);
            ESPhysicsQueryModule physicsQuery = ESGameManager.PhysicsQueryModule;
            int count;
            while (true)
            {
                if (physicsQuery != null)
                {
                    count = physicsQuery.ShotCast(
                        query.from,
                        query.to,
                        query.radius,
                        hitLayers,
                        _hitBuffer,
                        query.triggerInteraction);
                }
                else if (query.radius > 0.0001f)
                {
                    count = Physics.SphereCastNonAlloc(
                        query.from,
                        query.radius,
                        delta / distance,
                        _hitBuffer,
                        distance,
                        hitLayers,
                        query.triggerInteraction);
                }
                else
                {
                    count = Physics.RaycastNonAlloc(
                        query.from,
                        delta / distance,
                        _hitBuffer,
                        distance,
                        hitLayers,
                        query.triggerInteraction);
                }
                if (count < _hitBuffer.Length || _hitBuffer.Length >= MaximumPhysicsHitCapacity)
                    break;

                EnsureCapacity(Mathf.Min(_hitBuffer.Length * 2, MaximumPhysicsHitCapacity));
            }

            if (count <= 0)
                return 0;

            IsOverflow = count > maxResults || count >= _hitBuffer.Length;
            Array.Sort(_hitBuffer, 0, count, RaycastHitDistanceComparer.Instance);
            int written = 0;
            for (int i = 0; i < count && written < maxResults; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                results[written++] = new ShotHitCandidate
                {
                    collider = hit.collider,
                    point = hit.point,
                    normal = hit.normal,
                    incomingVelocity = Vector3.zero,
                    distance = hit.distance,
                    layer = hit.collider != null ? hit.collider.gameObject.layer : 0,
                    isTrigger = hit.collider != null && hit.collider.isTrigger
                };
            }

            return written;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
                => left.distance.CompareTo(right.distance);
        }

        private void EnsureCapacity(int capacity)
        {
            int useCapacity = Mathf.Max(1, capacity);
            if (_hitBuffer == null || _hitBuffer.Length < useCapacity)
                _hitBuffer = new RaycastHit[useCapacity];
        }
    }

    public enum ESHitTagEligibilityResult : byte
    {
        Allowed = 0,
        MissingAttacker = 1,
        MissingTarget = 2,
        AttackerTagDenied = 3,
        TargetTagDenied = 4
    }

    /// <summary>
    /// Tag-only eligibility contract for a HitResolver. Physics produces candidates and combat
    /// owns damage, faction, and final resolution; this policy answers only whether the current
    /// attacker/target facts permit the hit to continue.
    /// </summary>
    [System.Serializable]
    public sealed class ESHitTagEligibility
    {
        [Tooltip("Optional facts the attacker must satisfy before a hit can resolve.")]
        public ESTagConditionConfig attackerCondition = new ESTagConditionConfig();

        [Tooltip("Optional facts the target must satisfy before a hit can resolve, such as 可受击 and not 无敌.")]
        public ESTagConditionConfig targetCondition = new ESTagConditionConfig();

        public bool TryAllows(Entity attacker, Entity target, out ESHitTagEligibilityResult result, out string error)
        {
            error = null;
            if (attackerCondition != null && !attackerCondition.IsEmpty)
            {
                if (attacker == null)
                {
                    result = ESHitTagEligibilityResult.MissingAttacker;
                    return true;
                }

                if (!attacker.Tags.TryMatches(attackerCondition, out bool attackerMatches, out error))
                {
                    result = ESHitTagEligibilityResult.AttackerTagDenied;
                    return false;
                }

                if (!attackerMatches)
                {
                    result = ESHitTagEligibilityResult.AttackerTagDenied;
                    return true;
                }
            }

            if (targetCondition != null && !targetCondition.IsEmpty)
            {
                if (target == null)
                {
                    result = ESHitTagEligibilityResult.MissingTarget;
                    return true;
                }

                if (!target.Tags.TryMatches(targetCondition, out bool targetMatches, out error))
                {
                    result = ESHitTagEligibilityResult.TargetTagDenied;
                    return false;
                }

                if (!targetMatches)
                {
                    result = ESHitTagEligibilityResult.TargetTagDenied;
                    return true;
                }
            }

            result = ESHitTagEligibilityResult.Allowed;
            return true;
        }
    }
}
