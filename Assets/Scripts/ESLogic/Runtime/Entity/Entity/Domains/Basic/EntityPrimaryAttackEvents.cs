using UnityEngine;

namespace ES
{
    public enum EntityPrimaryAttackEventKind
    {
        None = 0,
        Started = 1,
        HitResolved = 2,
        Finished = 3,
    }

    /// <summary>
    /// 一次普攻的统一生命周期通知，只描述开始、命中解析与结束，并提供稳定 attackId 和攻击来源。
    /// 具体玩法系统是否订阅、记录什么状态以及产生什么效果，均不属于 Combat 职责。
    /// </summary>
    public readonly struct EntityPrimaryAttackEvent
    {
        public readonly EntityPrimaryAttackEventKind kind;
        public readonly int attackId;
        public readonly EntityPrimaryAttackSelection selection;
        public readonly ESActionConfigKey actionKey;
        public readonly ESWeaponConfigKey primaryWeaponKey;
        public readonly ESWeaponConfigKey secondaryWeaponKey;
        public readonly UnityEngine.Object target;
        public readonly Vector3 hitPoint;
        public readonly bool hasHitPoint;
        public readonly ESActionHitResult actionHitResult;

        public EntityPrimaryAttackEvent(
            EntityPrimaryAttackEventKind kind,
            int attackId,
            EntityPrimaryAttackSelection selection,
            ESActionConfigKey actionKey,
            ESWeaponConfigKey primaryWeaponKey,
            ESWeaponConfigKey secondaryWeaponKey,
            UnityEngine.Object target = null,
            Vector3 hitPoint = default,
            bool hasHitPoint = false,
            ESActionHitResult actionHitResult = default)
        {
            this.kind = kind;
            this.attackId = attackId;
            this.selection = selection;
            this.actionKey = actionKey;
            this.primaryWeaponKey = primaryWeaponKey;
            this.secondaryWeaponKey = secondaryWeaponKey;
            this.target = target;
            this.hitPoint = hitPoint;
            this.hasHitPoint = hasHitPoint;
            this.actionHitResult = actionHitResult;
        }
    }
}
