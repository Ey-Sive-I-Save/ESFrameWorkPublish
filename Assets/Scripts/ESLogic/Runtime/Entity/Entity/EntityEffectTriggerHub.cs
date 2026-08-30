using System;
using Unity.Profiling;
using UnityEngine;

namespace ES
{
    public enum ESEffectTriggerKind : byte
    {
        Attack = 0,
        Damage = 1,
        Movement = 2,
        Spawn = 3,
        Count = 4
    }

    /// <summary>
    /// 统一效果观察结果。它只汇聚权威域已经完成的结果，不执行伤害、位移或生成。
    /// </summary>
    public readonly struct ESEffectTriggerEvent
    {
        public readonly ESEffectTriggerKind kind;
        /// <summary>Per-Entity monotonic correlation token; zero means not yet published.</summary>
        public readonly ulong traceId;
        /// <summary>Trace token of the effect currently dispatching this event, or zero for a root.</summary>
        public readonly ulong parentTraceId;
        public readonly Entity source;
        public readonly EntityPrimaryAttackEvent attack;
        public readonly ESEntityDamageRequest damageRequest;
        public readonly ESEntityDamageResult damageResult;
        public readonly ESEntityMovementResult movement;
        public readonly ESShotLifecycleEvent spawn;

        public ESEffectTriggerEvent(Entity source, in EntityPrimaryAttackEvent attack)
            : this(0, 0, source, in attack)
        {
        }

        private ESEffectTriggerEvent(ulong traceId, ulong parentTraceId, Entity source, in EntityPrimaryAttackEvent attack)
        {
            kind = ESEffectTriggerKind.Attack;
            this.traceId = traceId;
            this.parentTraceId = parentTraceId;
            this.source = source;
            this.attack = attack;
            damageRequest = default;
            damageResult = default;
            movement = default;
            spawn = default;
        }

        public ESEffectTriggerEvent(
            Entity source,
            in ESEntityDamageRequest damageRequest,
            in ESEntityDamageResult damageResult)
            : this(0, 0, source, in damageRequest, in damageResult)
        {
        }

        private ESEffectTriggerEvent(
            ulong traceId,
            ulong parentTraceId,
            Entity source,
            in ESEntityDamageRequest damageRequest,
            in ESEntityDamageResult damageResult)
        {
            kind = ESEffectTriggerKind.Damage;
            this.traceId = traceId;
            this.parentTraceId = parentTraceId;
            this.source = source;
            attack = default;
            this.damageRequest = damageRequest;
            this.damageResult = damageResult;
            movement = default;
            spawn = default;
        }

        public ESEffectTriggerEvent(Entity source, in ESEntityMovementResult movement)
            : this(0, 0, source, in movement)
        {
        }

        private ESEffectTriggerEvent(ulong traceId, ulong parentTraceId, Entity source, in ESEntityMovementResult movement)
        {
            kind = ESEffectTriggerKind.Movement;
            this.traceId = traceId;
            this.parentTraceId = parentTraceId;
            this.source = source;
            attack = default;
            damageRequest = default;
            damageResult = default;
            this.movement = movement;
            spawn = default;
        }

        public ESEffectTriggerEvent(Entity source, in ESShotLifecycleEvent spawn)
            : this(0, 0, source, in spawn)
        {
        }

        private ESEffectTriggerEvent(ulong traceId, ulong parentTraceId, Entity source, in ESShotLifecycleEvent spawn)
        {
            kind = ESEffectTriggerKind.Spawn;
            this.traceId = traceId;
            this.parentTraceId = parentTraceId;
            this.source = source;
            attack = default;
            damageRequest = default;
            damageResult = default;
            movement = default;
            this.spawn = spawn;
        }

        internal ESEffectTriggerEvent WithTrace(ulong value, ulong parent)
        {
            switch (kind)
            {
                case ESEffectTriggerKind.Attack:
                    return new ESEffectTriggerEvent(value, parent, source, in attack);
                case ESEffectTriggerKind.Damage:
                    return new ESEffectTriggerEvent(value, parent, source, in damageRequest, in damageResult);
                case ESEffectTriggerKind.Movement:
                    return new ESEffectTriggerEvent(value, parent, source, in movement);
                case ESEffectTriggerKind.Spawn:
                    return new ESEffectTriggerEvent(value, parent, source, in spawn);
                default:
                    return this;
            }
        }
    }

    /// <summary>Entity 专属的长期交互事实流；仅保存有界环形窗口，不承担分发职责。</summary>
    [Serializable]
    public sealed class ESEntityInteractionStream
    {
        public const int DefaultCapacity = 64;
        private readonly ESEffectTriggerEvent[] buffer;
        private int head;
        private int count;
        private ulong firstSequence;

        public ESEntityInteractionStream(int capacity = DefaultCapacity)
        {
            buffer = new ESEffectTriggerEvent[Mathf.Clamp(capacity, 1, 1024)];
        }

        public int Count => count;
        public int Capacity => buffer.Length;
        public ulong FirstSequence => firstSequence;

        internal void Append(in ESEffectTriggerEvent value)
        {
            int index = (head + count) % buffer.Length;
            if (count == buffer.Length)
            {
                head = (head + 1) % buffer.Length;
                index = (head + count - 1) % buffer.Length;
                firstSequence++;
            }
            else count++;
            buffer[index] = value;
        }

        public bool TryRead(int offset, out ESEffectTriggerEvent value)
        {
            if ((uint)offset >= (uint)count) { value = default; return false; }
            value = buffer[(head + offset) % buffer.Length];
            return true;
        }

        public void Clear()
        {
            Array.Clear(buffer, 0, buffer.Length);
            head = 0; count = 0; firstSequence = 0;
        }
    }

    /// <summary>
    /// 已完成交互链的结果汇总。安装阶段创建，Consume/Reset 阶段不分配托管内存。
    /// </summary>
    [Serializable]
    public sealed class ESEffectTriggerSummary
    {
        public int AttackCount { get; private set; }
        public int DamageCount { get; private set; }
        public int MovementCount { get; private set; }
        public int SpawnCount { get; private set; }
        public float AppliedDamage { get; private set; }
        public Vector3 TotalDisplacement { get; private set; }
        public ulong LastTraceId { get; private set; }
        public ulong RootTraceId { get; private set; }

        [ESHotPath]
        public void Consume(in ESEffectTriggerEvent evt)
        {
            if (RootTraceId == 0 && evt.parentTraceId == 0)
                RootTraceId = evt.traceId;
            LastTraceId = evt.traceId;
            switch (evt.kind)
            {
                case ESEffectTriggerKind.Attack:
                    AttackCount++;
                    break;
                case ESEffectTriggerKind.Damage:
                    DamageCount++;
                    if (evt.damageResult.applied)
                        AppliedDamage += evt.damageResult.previousHealth - evt.damageResult.currentHealth;
                    break;
                case ESEffectTriggerKind.Movement:
                    MovementCount++;
                    TotalDisplacement += evt.movement.displacement;
                    break;
                case ESEffectTriggerKind.Spawn:
                    SpawnCount++;
                    break;
            }
        }

        public void Reset()
        {
            AttackCount = 0;
            DamageCount = 0;
            MovementCount = 0;
            SpawnCount = 0;
            AppliedDamage = 0f;
            TotalDisplacement = Vector3.zero;
            LastTraceId = 0;
            RootTraceId = 0;
        }
    }

    /// <summary>唯一的 EntityGameplayInteractionHub：负责交互链分发、追踪与订阅生命周期。</summary>
    [Serializable]
    public sealed class EntityGameplayInteractionHub
    {
        /// <summary>
        /// 唯一运行时效果观察总线定义。Hub 只负责事件分发、注册生命周期和追踪，
        /// 不拥有伤害、位移、生成或表现资源，也不替代这些领域的权威执行器。
        /// </summary>
        public const string ContractId = "es.entity.effect-trigger-hub.v1";
        public const int CurrentSchemaVersion = 1;

        public const int MaxDispatchDepth = 32;
        private static readonly ProfilerMarker PublishMarker =
            new ProfilerMarker("ES.Entity.EffectTrigger.Publish");
        [NonSerialized] private Entity owner;
        [NonSerialized] private Action<ESEffectTriggerEvent> resolved;
        [NonSerialized] private Delegate[] resolvedSnapshot = Array.Empty<Delegate>();
        [NonSerialized] private Action<ESEffectTriggerEvent>[] routed =
            new Action<ESEffectTriggerEvent>[(int)ESEffectTriggerKind.Count];
        [NonSerialized] private Delegate[][] routedSnapshots =
            new Delegate[(int)ESEffectTriggerKind.Count][];
        [NonSerialized] private int totalPublished;
        [NonSerialized] private int[] publishedByKind = new int[(int)ESEffectTriggerKind.Count];
        [NonSerialized] private uint nextTraceSequence;
        [NonSerialized] private int dispatchDepth;
        [NonSerialized] private long dispatchDepthRejectCount;
        [NonSerialized] private ulong currentTraceId;
        [NonSerialized] private ESEntityInteractionStream stream;

        internal EntityGameplayInteractionHub()
        {
        }

        internal EntityGameplayInteractionHub(Entity owner)
        {
            this.owner = owner;
        }

        internal void BindOwner(Entity value)
        {
            if (owner == null)
                owner = value;
        }

        /// <summary>Lifecycle-only warmup for dispatch channels; the optional Entity stream is lazy.</summary>
        internal void Warmup()
        {
            EnsureChannels();
        }

        public int TotalPublished => totalPublished;
        public long DispatchDepthRejectCount => dispatchDepthRejectCount;
        public int SchemaVersion => CurrentSchemaVersion;
        public bool IsBound => owner != null;
        /// <summary>按需创建固定容量流；未被消费的 Entity 不承担长期缓冲内存。</summary>
        public ESEntityInteractionStream Stream => stream ??= new ESEntityInteractionStream();

        /// <summary>检查 Hub 是否满足当前唯一契约；不产生分配，也不执行副作用。</summary>
        public bool Validate(out string error)
        {
            if (owner == null)
            {
                error = "EffectTriggerHub 尚未绑定 Entity owner。";
                return false;
            }

            if (SchemaVersion != CurrentSchemaVersion)
            {
                error = "EffectTriggerHub schemaVersion 不受支持：" + SchemaVersion;
                return false;
            }

            error = string.Empty;
            return true;
        }

        public int GetPublishedCount(ESEffectTriggerKind kind)
        {
            return IsValidKind(kind) && publishedByKind != null
                ? publishedByKind[(int)kind]
                : 0;
        }

        /// <summary>
        /// 全量兼容通道。消费者应在全量通道与按类型通道之间二选一，避免重复处理同一结果。
        /// </summary>
        public event Action<ESEffectTriggerEvent> Resolved
        {
            add
            {
                if (value == null)
                    return;
                resolved += value;
                resolvedSnapshot = resolved.GetInvocationList();
            }
            remove
            {
                if (value == null || resolved == null)
                    return;

                Action<ESEffectTriggerEvent> before = resolved;
                Action<ESEffectTriggerEvent> after =
                    (Action<ESEffectTriggerEvent>)Delegate.Remove(before, value);
                if (ReferenceEquals(before, after))
                    return;

                resolved = after;
                resolvedSnapshot = after != null ? after.GetInvocationList() : Array.Empty<Delegate>();
            }
        }

        /// <summary>
        /// 按效果类型订阅，避免消费者在统一通道中承担无关事件过滤成本。
        /// 与 Resolved 全量通道是两种互斥消费模式。
        /// </summary>
        public void Subscribe(ESEffectTriggerKind kind, Action<ESEffectTriggerEvent> handler)
        {
            if (handler == null || !IsValidKind(kind))
                return;

            EnsureChannels();
            int index = (int)kind;
            routed[index] += handler;
            routedSnapshots[index] = routed[index].GetInvocationList();
        }

        public void Unsubscribe(ESEffectTriggerKind kind, Action<ESEffectTriggerEvent> handler)
        {
            if (handler == null || !IsValidKind(kind))
                return;

            EnsureChannels();
            int index = (int)kind;
            Action<ESEffectTriggerEvent> before = routed[index];
            if (before == null)
                return;

            Action<ESEffectTriggerEvent> after =
                (Action<ESEffectTriggerEvent>)Delegate.Remove(before, handler);
            if (ReferenceEquals(before, after))
                return;

            routed[index] = after;
            routedSnapshots[index] = after != null
                ? after.GetInvocationList()
                : Array.Empty<Delegate>();
        }

        [ESHotPath]
        internal void Publish(in ESEffectTriggerEvent evt)
        {
            using (PublishMarker.Auto())
            {
                EnsureChannels();
                if (!IsValidKind(evt.kind))
                    return;
                if (owner != null && !ReferenceEquals(owner, evt.source))
                    return;
                if (dispatchDepth >= MaxDispatchDepth)
                {
                    dispatchDepthRejectCount++;
                    return;
                }

                dispatchDepth++;
                ulong traceId = ((ulong)(uint)(owner != null ? owner.GetInstanceID() : 0) << 32)
                    | ++nextTraceSequence;
                ulong parentTraceId = currentTraceId;
                ESEffectTriggerEvent publishedEvent = evt.WithTrace(traceId, parentTraceId);
                stream?.Append(publishedEvent);
                currentTraceId = traceId;
                try
                {
                    totalPublished++;
                    publishedByKind[(int)evt.kind]++;
                    Delegate[] invocationList = resolvedSnapshot;
                    for (int index = 0; index < invocationList.Length; index++)
                    {
                        try
                        {
                            ((Action<ESEffectTriggerEvent>)invocationList[index]).Invoke(publishedEvent);
                        }
                        catch (Exception exception)
                        {
                            UnityEngine.Debug.LogException(exception);
                        }
                    }

                    invocationList = routedSnapshots[(int)evt.kind];
                    if (invocationList == null)
                        return;
                    for (int index = 0; index < invocationList.Length; index++)
                    {
                        try
                        {
                            ((Action<ESEffectTriggerEvent>)invocationList[index]).Invoke(publishedEvent);
                        }
                        catch (Exception exception)
                        {
                            UnityEngine.Debug.LogException(exception);
                        }
                    }
                }
                finally
                {
                    currentTraceId = parentTraceId;
                    dispatchDepth--;
                }
            }
        }

        public void Clear()
        {
            EnsureChannels();
            resolved = null;
            resolvedSnapshot = Array.Empty<Delegate>();
            totalPublished = 0;
            nextTraceSequence = 0;
            dispatchDepth = 0;
            dispatchDepthRejectCount = 0;
            currentTraceId = 0;
            stream?.Clear();
            Array.Clear(publishedByKind, 0, publishedByKind.Length);
            for (int index = 0; index < routed.Length; index++)
            {
                routed[index] = null;
                routedSnapshots[index] = Array.Empty<Delegate>();
            }
        }

        private static bool IsValidKind(ESEffectTriggerKind kind)
        {
            return (int)kind >= 0 && (int)kind < (int)ESEffectTriggerKind.Count;
        }

        private void EnsureChannels()
        {
            routed ??= new Action<ESEffectTriggerEvent>[(int)ESEffectTriggerKind.Count];
            routedSnapshots ??= new Delegate[(int)ESEffectTriggerKind.Count][];
            publishedByKind ??= new int[(int)ESEffectTriggerKind.Count];
        }
    }
}
