using System;
using Sirenix.OdinInspector;
using Unity.Profiling;
using UnityEngine;

namespace ES
{
    public readonly struct ESEntityDamageRequest
    {
        public readonly Entity source;
        public readonly float amount;
        public readonly float impactStrength;
        public readonly Collider hitCollider;
        public readonly Vector3 hitPoint;
        public readonly Vector3 forceDirection;
        public readonly int attackId;

        public ESEntityDamageRequest(
            Entity source,
            float amount,
            float impactStrength,
            Collider hitCollider,
            Vector3 hitPoint,
            Vector3 forceDirection,
            int attackId)
        {
            this.source = source;
            this.amount = amount;
            this.impactStrength = impactStrength;
            this.hitCollider = hitCollider;
            this.hitPoint = hitPoint;
            this.forceDirection = forceDirection;
            this.attackId = attackId;
        }
    }

    public readonly struct ESEntityDamageResult
    {
        public readonly bool applied;
        public readonly bool killed;
        public readonly float previousHealth;
        public readonly float currentHealth;

        public ESEntityDamageResult(
            bool applied,
            bool killed,
            float previousHealth,
            float currentHealth)
        {
            this.applied = applied;
            this.killed = killed;
            this.previousHealth = previousHealth;
            this.currentHealth = currentHealth;
        }
    }

    [Serializable, TypeRegistryItem("基础生命与伤害效果模块")]
    public sealed class EntityBasicHealthModule : EntityBasicModuleBase
    {
        private static readonly ProfilerMarker ApplyDamageMarker =
            new ProfilerMarker("ES.Weapon.Damage.Apply");

        [MinValue(0.01f), LabelText("最大生命")]
        public float maxHealth = 100f;

        [LabelText("出生时满血")]
        public bool resetToFullOnSpawn = true;

        [LabelText("无敌")]
        public bool invulnerable;

        [ShowInInspector, ReadOnly, NonSerialized, LabelText("当前生命")]
        private float currentHealth;

        [ShowInInspector, ReadOnly, NonSerialized, LabelText("最近攻击 ID")]
        private int lastAttackId;

        [ShowInInspector, ReadOnly, NonSerialized, LabelText("最近冲击强度")]
        private float lastImpactStrength;

        [NonSerialized] private StateFinalIKDriver hitReactionDriver;
        [NonSerialized] private Animator hitReactionAnimator;
        [NonSerialized] private Action<ESEntityDamageResult> damageApplied;
        [NonSerialized] private Delegate[] damageAppliedSnapshot = Array.Empty<Delegate>();

        public float CurrentHealth => currentHealth;
        public float MaxHealth => Mathf.Max(0.01f, maxHealth);
        public bool IsDead => currentHealth <= 0f;
        public int LastAttackId => lastAttackId;
        public float LastImpactStrength => lastImpactStrength;

        public event Action<ESEntityDamageResult> DamageApplied
        {
            add
            {
                if (value == null)
                    return;
                damageApplied += value;
                damageAppliedSnapshot = damageApplied.GetInvocationList();
            }
            remove
            {
                if (value == null || damageApplied == null)
                    return;

                Action<ESEntityDamageResult> before = damageApplied;
                Action<ESEntityDamageResult> after =
                    (Action<ESEntityDamageResult>)Delegate.Remove(before, value);
                if (ReferenceEquals(before, after))
                    return;

                damageApplied = after;
                damageAppliedSnapshot = after != null
                    ? after.GetInvocationList()
                    : Array.Empty<Delegate>();
            }
        }

        public override void Start()
        {
            base.Start();
            MyCore?.Internal_BindBasicHealth(this);
            if (currentHealth <= 0f)
                currentHealth = MaxHealth;
            CacheHitReactionDriver();
        }

        public void OnPoolSpawned()
        {
            MyCore?.Internal_BindBasicHealth(this);
            if (resetToFullOnSpawn || currentHealth <= 0f)
                currentHealth = MaxHealth;
            lastAttackId = 0;
            lastImpactStrength = 0f;
            CacheHitReactionDriver();
        }

        public void OnPoolDespawned()
        {
            MyCore?.Internal_UnbindBasicHealth(this);
            lastAttackId = 0;
            lastImpactStrength = 0f;
            hitReactionDriver = null;
            hitReactionAnimator = null;
            damageApplied = null;
            damageAppliedSnapshot = Array.Empty<Delegate>();
        }

        public override void OnDestroy()
        {
            MyCore?.Internal_UnbindBasicHealth(this);
            damageApplied = null;
            damageAppliedSnapshot = Array.Empty<Delegate>();
            base.OnDestroy();
        }

        [ESHotPath]
        public bool TryApplyDamage(
            in ESEntityDamageRequest request,
            out ESEntityDamageResult result)
        {
            using (ApplyDamageMarker.Auto())
            {
                float amount = request.amount;
                if (invulnerable
                    || IsDead
                    || amount <= 0f
                    || float.IsNaN(amount)
                    || float.IsInfinity(amount))
                {
                    result = new ESEntityDamageResult(false, false, currentHealth, currentHealth);
                    return false;
                }

                float previous = currentHealth;
                currentHealth = Mathf.Max(0f, previous - amount);
                lastAttackId = request.attackId;
                lastImpactStrength = Mathf.Max(0f, request.impactStrength);
                bool killed = previous > 0f && currentHealth <= 0f;
                result = new ESEntityDamageResult(true, killed, previous, currentHealth);

                StateFinalIKDriver driver = ResolveHitReactionDriver();
                if (driver != null && request.hitCollider != null && lastImpactStrength > 0f)
                {
                    Vector3 direction = request.forceDirection.sqrMagnitude > 0.0001f
                        ? request.forceDirection.normalized
                        : Vector3.up;
                    driver.IKHit(
                        request.hitCollider,
                        direction * lastImpactStrength,
                        request.hitPoint);
                }

                Delegate[] invocationList = damageAppliedSnapshot;
                for (int index = 0; index < invocationList.Length; index++)
                {
                    try
                    {
                        ((Action<ESEntityDamageResult>)invocationList[index]).Invoke(result);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, MyCore);
                    }
                }
                return true;
            }
        }

        public void Internal_SetCurrentHealth(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return;
            currentHealth = Mathf.Clamp(value, 0f, MaxHealth);
        }

        private StateFinalIKDriver ResolveHitReactionDriver()
        {
            Animator animator = MyCore != null ? MyCore.animator : null;
            return animator != null && hitReactionAnimator == animator
                ? hitReactionDriver
                : null;
        }

        private void CacheHitReactionDriver()
        {
            hitReactionAnimator = MyCore != null ? MyCore.animator : null;
            hitReactionDriver = hitReactionAnimator != null
                ? hitReactionAnimator.GetComponent<StateFinalIKDriver>()
                : null;
        }
    }
}
