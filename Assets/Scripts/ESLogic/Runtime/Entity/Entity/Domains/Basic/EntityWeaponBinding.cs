using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 每把武器自带的挂载与状态配置。
    /// 挂在 weaponRoot 上，由 EntityBasicCombatModule 自动读取。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityWeaponBinding : MonoBehaviour
    {
        [Title("挂点")]
        [LabelText("手持挂点策略")]
        [InfoBox("优先级：武器显式挂点（默认）→ 角色 WeaponSocket → Combat 回退挂点。不要再把武器业务偏移直接写到 Humanoid 右手骨。")]
        public EntityWeaponHandMountPolicy handMountPolicy = EntityWeaponHandMountPolicy.ExplicitThenCharacterSocket;

        [LabelText("武器显式手持挂点")]
        public Transform handMount;

        [LabelText("应用手持局部偏移")]
        public bool applyHandMountLocalOffset;

        [ShowIf(nameof(applyHandMountLocalOffset)), LabelText("手持局部位置")]
        public Vector3 handMountLocalPosition;

        [ShowIf(nameof(applyHandMountLocalOffset)), LabelText("手持局部旋转")]
        public Vector3 handMountLocalEulerAngles;

        [Title("双手武器")]
        [LabelText("双手握持")]
        public bool twoHanded;

        [ShowIf(nameof(twoHanded)), LabelText("副手握点")]
        [Tooltip("供左手 IK / 切枪辅助使用；武器根仍只挂在手持 Socket。")]
        public Transform offHandGripTarget;

        [LabelText("身上挂点")]
        public Transform holsterMount;

        [LabelText("默认挂点索引")]
        [Tooltip("仅当 holsterMount 为空时生效；会使用 Combat.defaultHolsterMounts[index]。")]
        [MinValue(-1)]
        public int holsterMountIndex = -1;

        [Title("枪械关键点")]
        [LabelText("枪口/开火点")]
        public Transform fireOrigin;

        [LabelText("瞄准目标")]
        public Transform aimTarget;

        [Title("切枪IK辅助")]
        [LabelText("左手辅助目标")]
        public Transform switchAssistLeftHandTarget;

        [LabelText("右手辅助目标")]
        public Transform switchAssistRightHandTarget;

        [Title("状态键覆盖")]
        [LabelText("拿枪状态键")]
        public string equipStateKey;

        [LabelText("拿枪状态 AniInfo")]
        public StateAniDataInfo equipStateInfo;

        [LabelText("收枪状态键")]
        public string holsterStateKey;

        [LabelText("收枪状态 AniInfo")]
        public StateAniDataInfo holsterStateInfo;

        [LabelText("切枪状态键")]
        public string switchStateKey;

        [LabelText("切枪状态 AniInfo")]
        public StateAniDataInfo switchStateInfo;

        [LabelText("开火状态键")]
        public string fireStateKey;

        [LabelText("开火状态 AniInfo")]
        public StateAniDataInfo fireStateInfo;

        [Title("Tag")]
        [LabelText("手持时添加")]
        [Tooltip("只有该武器实际手持时才持有；收枪、切枪或实体销毁会精确释放这一组 Lease。")]
        public List<ESTagStableReference> equippedTags = new List<ESTagStableReference>();

        [Title("调试")]
        [ReadOnly, ShowInInspector, LabelText("武器名")]
        public string WeaponName => gameObject.name;

        public Transform ResolveHandMount(Entity owner, Transform combatFallback)
        {
            if (handMountPolicy == EntityWeaponHandMountPolicy.LegacyCombatFallback)
                return handMount != null ? handMount : combatFallback;

            if (handMountPolicy != EntityWeaponHandMountPolicy.CharacterSocketOnly && handMount != null)
                return handMount;

            if (owner != null)
            {
                if (owner.TryResolveTransform("WeaponSocket", out Transform weaponSocket))
                    return weaponSocket;
                if (owner.TryResolveTransform(DefaultTransformKey.Weapon, out Transform weaponFallback))
                    return weaponFallback;
            }

            if (handMount != null)
                return handMount;

            return combatFallback;
        }

        public void ApplyHandMountLocalPose(Transform weaponRoot)
        {
            if (!applyHandMountLocalOffset || weaponRoot == null)
                return;

            weaponRoot.localPosition = handMountLocalPosition;
            weaponRoot.localRotation = Quaternion.Euler(handMountLocalEulerAngles);
        }
    }

    public enum EntityWeaponHandMountPolicy
    {
        [InspectorName("显式挂点 → 角色 WeaponSocket")]
        ExplicitThenCharacterSocket = 0,

        [InspectorName("只使用角色 WeaponSocket")]
        CharacterSocketOnly = 1,

        [InspectorName("旧 Combat 回退优先")]
        LegacyCombatFallback = 2,
    }
}
