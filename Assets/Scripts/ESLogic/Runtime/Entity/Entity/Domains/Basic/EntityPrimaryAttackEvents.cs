using UnityEngine;

namespace ES
{
    /// <summary>
    /// Optional fixed-size attack metadata. A schema owner defines the meaning of the slots;
    /// keeping the payload value-only makes it safe for hot-path events, replay and networking.
    /// </summary>
    [System.Serializable]
    public readonly struct ESAttackSpecialInfo
    {
        public readonly int schemaId;
        public readonly uint flags;
        public readonly int int0;
        public readonly int int1;
        public readonly float float0;
        public readonly float float1;
        public readonly Vector3 vector;

        public ESAttackSpecialInfo(
            int schemaId,
            uint flags = 0,
            int int0 = 0,
            int int1 = 0,
            float float0 = 0f,
            float float1 = 0f,
            Vector3 vector = default)
        {
            this.schemaId = schemaId;
            this.flags = flags;
            this.int0 = int0;
            this.int1 = int1;
            this.float0 = float0;
            this.float1 = float1;
            this.vector = vector;
        }

        public bool IsValid => schemaId != 0;
    }

    /// <summary>单次攻击的值语义复合载荷；不分配托管内存，可在攻击链各阶段原样转发。</summary>
    [System.Serializable]
    public readonly struct ESAttackInteractionPayload
    {
        public readonly int attackId;
        public readonly ESActionConfigKey actionKey;
        public readonly ESWeaponConfigKey primaryWeaponKey;
        public readonly ESWeaponConfigKey secondaryWeaponKey;
        public readonly ESAttackSpecialInfo specialInfo;

        public ESAttackInteractionPayload(
            int attackId,
            ESActionConfigKey actionKey,
            ESWeaponConfigKey primaryWeaponKey,
            ESWeaponConfigKey secondaryWeaponKey,
            ESAttackSpecialInfo specialInfo = default)
        {
            this.attackId = attackId;
            this.actionKey = actionKey;
            this.primaryWeaponKey = primaryWeaponKey;
            this.secondaryWeaponKey = secondaryWeaponKey;
            this.specialInfo = specialInfo;
        }
    }

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
        public readonly ESAttackSpecialInfo specialInfo;
        public readonly ESAttackInteractionPayload payload;

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
            ESActionHitResult actionHitResult = default,
            ESAttackSpecialInfo specialInfo = default,
            ESAttackInteractionPayload payload = default)
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
            this.specialInfo = specialInfo;
            this.payload = payload.attackId != 0 || payload.specialInfo.IsValid
                ? payload
                : new ESAttackInteractionPayload(attackId, actionKey, primaryWeaponKey, secondaryWeaponKey, specialInfo);
        }
    }
}
