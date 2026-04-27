using System;
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
        [LabelText("手持挂点")]
        public Transform handMount;

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

        [Title("调试")]
        [ReadOnly, ShowInInspector, LabelText("武器名")]
        public string WeaponName => gameObject.name;
    }
}
