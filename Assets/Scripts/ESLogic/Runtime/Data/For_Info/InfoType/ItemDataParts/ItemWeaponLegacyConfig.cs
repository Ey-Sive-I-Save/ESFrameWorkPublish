using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ItemWeaponConfig
    {
        [LabelText("启用旧武器配置")]
        public bool enabled;

        [ShowIf(nameof(enabled))]
        [LabelText("武器类型")]
        public ItemWeaponKind weaponKind = ItemWeaponKind.Ranged;

        [ShowIf(nameof(enabled))]
        [LabelText("装备槽")]
        public string equipSlot;

        [ShowIf(nameof(enabled))]
        [LabelText("基础伤害")]
        public float baseDamage = 10f;

        [ShowIf(nameof(enabled))]
        [LabelText("攻击间隔")]
        public float attackInterval = 0.3f;

        [ShowIf(nameof(enabled))]
        [LabelText("发射飞行物")]
        public ItemDataInfo shotItem;

        [ShowIf(nameof(enabled))]
        [LabelText("使用时逻辑")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("使用时逻辑")]
        public ESOutputOp onUse;
    }
}
