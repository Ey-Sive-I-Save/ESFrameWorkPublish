using System;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Marks a method whose steady-state execution must satisfy the ES hot-path contract.
    /// The attribute is an analysis boundary; it does not change runtime dispatch.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ESHotPathAttribute : Attribute
    {
    }

    internal static class ESShotColliderOwnerRegistry
    {
        private const int InitialColliderCapacity = 1024;
        private static readonly Dictionary<int, Entity> OwnersByColliderId =
            new Dictionary<int, Entity>(InitialColliderCapacity);

        internal static void Internal_Register(Entity owner, List<Collider> colliders)
        {
            if (owner == null || colliders == null)
                return;

            for (int index = 0; index < colliders.Count; index++)
            {
                Collider collider = colliders[index];
                if (collider == null)
                    continue;

                OwnersByColliderId[collider.GetInstanceID()] = owner;
            }
        }

        internal static void Internal_Unregister(Entity owner, List<Collider> colliders)
        {
            if (owner == null || colliders == null)
                return;

            for (int index = 0; index < colliders.Count; index++)
            {
                Collider collider = colliders[index];
                if (collider == null)
                    continue;

                int colliderId = collider.GetInstanceID();
                if (OwnersByColliderId.TryGetValue(colliderId, out Entity registered)
                    && registered == owner)
                    OwnersByColliderId.Remove(colliderId);
            }
        }

        [ESHotPath]
        internal static bool TryResolveEntity(Collider collider, out Entity entity)
        {
            entity = null;
            return collider != null
                   && OwnersByColliderId.TryGetValue(collider.GetInstanceID(), out entity)
                   && entity != null;
        }
    }

    public static class ESShotHotPathDiagnostics
    {
        public static int TagEligibilityFailureCount { get; private set; }

        internal static void Internal_RecordTagEligibilityFailure()
        {
            unchecked
            {
                TagEligibilityFailureCount++;
            }
        }
    }

    public enum ESShotHitDecision : byte
    {
        Ignore = 0,
        Stop = 1,
        Pierce = 2,
        Bounce = 3
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
        public readonly bool publishesAttackFinish;
        public readonly ESAttackSpecialInfo specialInfo;

        public ESShotLaunchContext(
            int attackId,
            Entity owner,
            ESInstanceHandle sourceItemHandle,
            ESWeaponConfigKey sourceWeaponKey,
            EntityPrimaryAttackSelection attackSelection,
            Transform target = null,
            IESShotHitResolver hitResolver = null,
            Action<ESShotLifecycleEvent> lifecycleObserver = null,
            bool publishesAttackFinish = true,
            ESAttackSpecialInfo specialInfo = default)
        {
            this.attackId = attackId;
            this.owner = owner;
            this.sourceItemHandle = sourceItemHandle;
            this.sourceWeaponKey = sourceWeaponKey;
            this.attackSelection = attackSelection;
            this.target = target;
            this.hitResolver = hitResolver;
            this.lifecycleObserver = lifecycleObserver;
            this.publishesAttackFinish = publishesAttackFinish;
            this.specialInfo = specialInfo;
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

        [ESHotPath]
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

            if (!ESShotColliderOwnerRegistry.TryResolveEntity(collider, out Entity target))
                return blockMode == ShotBlockMode.AnyBlocker
                    ? ESShotHitDecision.Stop
                    : ESShotHitDecision.Ignore;

            if (definition?.hitTagEligibility != null)
            {
                if (!definition.hitTagEligibility.TryAllows(
                        context.owner,
                        target,
                        out ESHitTagEligibilityResult eligibility,
                        out _))
                {
                    ESShotHotPathDiagnostics.Internal_RecordTagEligibilityFailure();
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

    internal readonly struct ESShotPreparedSpawn
    {
        internal readonly ESShotRuntimeData runtimeData;
        internal readonly int runtimeKey;
        internal readonly ItemShotSharedData definition;
        internal readonly ESGameObjectPoolModule pool;
        internal readonly GameObject prefab;

        internal ESShotPreparedSpawn(
            ESShotRuntimeData runtimeData,
            int runtimeKey,
            ItemShotSharedData definition,
            ESGameObjectPoolModule pool,
            GameObject prefab)
        {
            this.runtimeData = runtimeData;
            this.runtimeKey = runtimeKey;
            this.definition = definition;
            this.pool = pool;
            this.prefab = prefab;
        }

        internal bool IsValid => runtimeData != null
                                 && runtimeKey > 0
                                 && definition != null
                                 && pool != null
                                 && prefab != null;

        internal bool RequiresTarget(in ItemShotVariableData variableData)
        {
            return definition.aimMode == ShotAimMode.Target
                   || definition.aimMode == ShotAimMode.MustHit
                   || (variableData.forceMustHit && definition.allowMustHit);
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
            if (!Internal_TryPrepareSpawn(shotKey, out ESShotPreparedSpawn prepared, out error))
                return false;

            ItemShotVariableData variableData = prepared.runtimeData.PreparedDefaultVariableData;
            if (prepared.RequiresTarget(variableData) && context.target == null)
            {
                error = "Target/MustHit Shot 必须提供有效目标。";
                return false;
            }

            return Internal_TrySpawnPrepared(
                prepared,
                origin,
                direction,
                variableData,
                context,
                out shot,
                out error);
        }

        public static bool TrySpawnWithVariable(
            ESShotConfigKey shotKey,
            Vector3 origin,
            Vector3 direction,
            in ItemShotVariableData variableData,
            in ESShotLaunchContext context,
            out ItemShotModule shot,
            out string error)
        {
            shot = null;
            error = null;
            if (!variableData.ValidateDefinition(out _))
            {
                error = "Shot VariableData 无效。";
                return false;
            }
            if (!Internal_TryPrepareSpawn(shotKey, out ESShotPreparedSpawn prepared, out error))
                return false;
            if (prepared.RequiresTarget(variableData) && context.target == null)
            {
                error = "Target/MustHit Shot 必须提供有效目标。";
                return false;
            }

            return Internal_TrySpawnPrepared(
                prepared,
                origin,
                direction,
                variableData,
                context,
                out shot,
                out error);
        }

        [ESHotPath]
        internal static bool Internal_TryPrepareSpawn(
            ESShotConfigKey shotKey,
            out ESShotPreparedSpawn prepared,
            out string error)
        {
            prepared = default;
            if (shotKey == null || !shotKey.IsConfigured
                || !ESRuntimeDataGameCore.Shots.TryGetRuntimeKey(shotKey, out int runtimeKey)
                || !ESRuntimeDataGameCore.Shots.TryGet(runtimeKey, out ESShotRuntimeData runtimeData)
                || runtimeData == null
                || !runtimeData.Ready
                || runtimeData.PreparedSharedData == null)
            {
                error = "Shot Key 未解析到可用的 ESShotRuntimeData。";
                return false;
            }
            if (!ESGameManager.TryGetModule(out ESGameObjectPoolModule pool) || pool == null)
            {
                error = "GameObject Pool 模块不可用。";
                return false;
            }
            if (!runtimeData.TryGetPreparedPrefabIdentity(out ESAssetIdentity prefabIdentity)
                || !ESAssets.TryGetActivePlanAsset(prefabIdentity, out GameObject prefabAsset))
            {
                error = "Shot Prefab 尚未由 ResourcePlan 预热或不在 ActivePlan 借用表中。";
                return false;
            }

            prepared = new ESShotPreparedSpawn(
                runtimeData,
                runtimeKey,
                runtimeData.PreparedSharedData,
                pool,
                prefabAsset);
            error = null;
            return true;
        }

        [ESHotPath]
        internal static bool Internal_TrySpawnPrepared(
            in ESShotPreparedSpawn prepared,
            Vector3 origin,
            Vector3 direction,
            in ItemShotVariableData variableData,
            in ESShotLaunchContext context,
            out ItemShotModule shot,
            out string error)
        {
            shot = null;
            error = null;
            if (!prepared.IsValid)
            {
                error = "Shot Prepared Spawn 上下文无效。";
                return false;
            }

            GameObject instance = null;
            try
            {
                Vector3 useDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
                instance = prepared.pool.Internal_GetInPool(
                    prepared.prefab,
                    origin,
                    Quaternion.LookRotation(useDirection, Vector3.up),
                    null,
                    false,
                    0f,
                    out MonoBehaviour poolRootLifecycle);
                if (instance == null)
                {
                    error = "对象池拒绝创建 Shot 实例。";
                    return false;
                }

                Item item = poolRootLifecycle as Item;
                shot = item != null ? item.Internal_ShotModule : null;
                if (item == null || shot == null)
                {
                    error = "Shot Prefab 必须在根节点提供 Item 和 ItemShotModule。";
                    prepared.pool.PushToPool(instance);
                    shot = null;
                    return false;
                }

                shot.Internal_InitializeSpawn(
                    prepared.runtimeData,
                    prepared.runtimeKey,
                    variableData,
                    context);
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
                    prepared.pool.PushToPool(instance);
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
        public static readonly ItemShotAlwaysTickPolicy Instance = new ItemShotAlwaysTickPolicy();

        public bool ShouldTick(in ShotMotionState state, int frameCount)
        {
            return state.launched;
        }
    }

    public sealed class ItemShotPhysicsHitSolver : IItemShotHitSolver
    {
        private const int MaximumPhysicsHitCapacity = 256;
        private const float MinimumFallbackAdvance = 0.001f;
        private RaycastHit[] _hitBuffer;

        public bool IsOverflow { get; private set; }

        public ItemShotPhysicsHitSolver(int capacity)
        {
            EnsureCapacity(capacity);
        }

        [ESHotPath]
        public int Query(in ItemShotHitQuery query, ShotHitCandidate[] results, int maxResults)
        {
            IsOverflow = false;
            if (results == null || maxResults <= 0)
                return 0;

            maxResults = Mathf.Min(maxResults, results.Length);
            if (maxResults <= 0)
                return 0;

            Vector3 delta = query.to - query.from;
            float distance = delta.magnitude;
            if (distance <= 0.0001f)
                return 0;

            LayerMask hitLayers = ESPhysicsLayers.GetShotHitMask(query.hitLayers);
            ESSpaceProbe spaceProbe = ESGameManager.SpaceProbe;
            ESPhysicsQueryModule physicsQuery = ESGameManager.PhysicsQueryModule;
            int count;
            if (spaceProbe != null)
            {
                count = spaceProbe.Cast(
                    query.from,
                    query.to,
                    query.radius,
                    hitLayers,
                    _hitBuffer,
                    query.triggerInteraction);
            }
            else if (physicsQuery != null)
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

            if (count <= 0)
                return 0;

            IsOverflow = count > maxResults || count >= _hitBuffer.Length;
            if (count >= _hitBuffer.Length)
            {
                return QuerySaturatedNearest(
                    query,
                    results,
                    maxResults,
                    delta / distance,
                    distance,
                    hitLayers);
            }

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

        [ESHotPath]
        private static int QuerySaturatedNearest(
            in ItemShotHitQuery query,
            ShotHitCandidate[] results,
            int maxResults,
            Vector3 direction,
            float distance,
            LayerMask hitLayers)
        {
            int written = 0;
            int stepCount = 0;
            int maximumSteps = Mathf.Min(MaximumPhysicsHitCapacity, Mathf.Max(4, maxResults * 4));
            float travelled = 0f;
            Vector3 origin = query.from;

            while (written < maxResults
                   && stepCount < maximumSteps
                   && travelled < distance)
            {
                float remainingDistance = distance - travelled;
                bool hasHit = query.radius > 0.0001f
                    ? Physics.SphereCast(
                        origin,
                        query.radius,
                        direction,
                        out RaycastHit nearest,
                        remainingDistance,
                        hitLayers,
                        query.triggerInteraction)
                    : Physics.Raycast(
                        origin,
                        direction,
                        out nearest,
                        remainingDistance,
                        hitLayers,
                        query.triggerInteraction);
                if (!hasHit)
                    break;

                stepCount++;
                Collider collider = nearest.collider;
                int colliderId = collider != null ? collider.GetInstanceID() : 0;
                if (colliderId != 0 && !ContainsCollider(results, written, colliderId))
                {
                    results[written++] = new ShotHitCandidate
                    {
                        collider = collider,
                        point = nearest.point,
                        normal = nearest.normal,
                        incomingVelocity = Vector3.zero,
                        distance = travelled + nearest.distance,
                        layer = collider.gameObject.layer,
                        isTrigger = collider.isTrigger
                    };
                }

                float advance = Mathf.Max(MinimumFallbackAdvance, nearest.distance + MinimumFallbackAdvance);
                travelled += advance;
                origin += direction * advance;
            }

            return written;
        }

        private static bool ContainsCollider(ShotHitCandidate[] results, int count, int colliderId)
        {
            for (int index = 0; index < count; index++)
            {
                Collider collider = results[index].collider;
                if (collider != null && collider.GetInstanceID() == colliderId)
                    return true;
            }

            return false;
        }

        private sealed class RaycastHitDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
                => left.distance.CompareTo(right.distance);
        }

        private void EnsureCapacity(int capacity)
        {
            int useCapacity = Mathf.Clamp(capacity, 1, MaximumPhysicsHitCapacity);
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

        [NonSerialized] private bool isPrepared;
        [NonSerialized] private bool hasAttackerCondition;
        [NonSerialized] private bool hasTargetCondition;
        [NonSerialized] private ESTagConditionRuntime attackerRuntime;
        [NonSerialized] private ESTagConditionRuntime targetRuntime;

        public bool IsPrepared => isPrepared;

        public bool TryPrepare(out string error)
        {
            isPrepared = false;
            hasAttackerCondition = attackerCondition != null && !attackerCondition.IsEmpty;
            hasTargetCondition = targetCondition != null && !targetCondition.IsEmpty;
            attackerRuntime = default;
            targetRuntime = default;

            if (hasAttackerCondition
                && !attackerCondition.TryCompile(out attackerRuntime, out error))
                return false;

            if (hasTargetCondition
                && !targetCondition.TryCompile(out targetRuntime, out error))
                return false;

            error = null;
            isPrepared = true;
            return true;
        }

        internal bool Internal_TryCreatePreparedCopy(
            out ESHitTagEligibility prepared,
            out string error)
        {
            prepared = null;
            bool preparedAttacker = attackerCondition != null && !attackerCondition.IsEmpty;
            bool preparedTarget = targetCondition != null && !targetCondition.IsEmpty;
            ESTagConditionRuntime compiledAttacker = default;
            ESTagConditionRuntime compiledTarget = default;

            if (preparedAttacker
                && !attackerCondition.TryCompile(out compiledAttacker, out error))
                return false;
            if (preparedTarget
                && !targetCondition.TryCompile(out compiledTarget, out error))
                return false;

            prepared = new ESHitTagEligibility
            {
                attackerCondition = new ESTagConditionConfig(),
                targetCondition = new ESTagConditionConfig(),
                isPrepared = true,
                hasAttackerCondition = preparedAttacker,
                hasTargetCondition = preparedTarget,
                attackerRuntime = CloneRuntime(compiledAttacker),
                targetRuntime = CloneRuntime(compiledTarget)
            };
            error = null;
            return true;
        }

        private static ESTagConditionRuntime CloneRuntime(in ESTagConditionRuntime source)
        {
            return new ESTagConditionRuntime
            {
                RequiredHotMask = source.RequiredHotMask,
                RequiredAnyHotMask = source.RequiredAnyHotMask,
                ForbiddenHotMask = source.ForbiddenHotMask,
                RequiredSparse = source.RequiredSparse != null
                    ? (int[])source.RequiredSparse.Clone()
                    : null,
                RequiredAnySparse = source.RequiredAnySparse != null
                    ? (int[])source.RequiredAnySparse.Clone()
                    : null,
                ForbiddenSparse = source.ForbiddenSparse != null
                    ? (int[])source.ForbiddenSparse.Clone()
                    : null,
                SchemaHash = source.SchemaHash,
                RuntimeLayoutHash = source.RuntimeLayoutHash
            };
        }

        [ESHotPath]
        public bool TryAllows(Entity attacker, Entity target, out ESHitTagEligibilityResult result, out string error)
        {
            error = null;
            if (!isPrepared)
            {
                result = ESHitTagEligibilityResult.AttackerTagDenied;
                error = "Hit Tag eligibility was not prepared before entering the hit hot path.";
                return false;
            }

            if (hasAttackerCondition)
            {
                if (attacker == null)
                {
                    result = ESHitTagEligibilityResult.MissingAttacker;
                    return true;
                }

                if (!attacker.Tags.TryMatches(attackerRuntime, out bool attackerMatches, out error))
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

            if (hasTargetCondition)
            {
                if (target == null)
                {
                    result = ESHitTagEligibilityResult.MissingTarget;
                    return true;
                }

                if (!target.Tags.TryMatches(targetRuntime, out bool targetMatches, out error))
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
