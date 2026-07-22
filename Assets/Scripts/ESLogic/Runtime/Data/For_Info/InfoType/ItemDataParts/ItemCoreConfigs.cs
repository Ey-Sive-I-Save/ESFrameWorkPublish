using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable]
    public sealed class ItemBaseConfig
    {
        [LabelText("物品类型")]
        public ItemKind kind = ItemKind.Prop;

        [LabelText("运行预制体")]
        public GameObject prefab;

        [LabelText("显示名称")]
        public string displayName;

        [LabelText("图标")]
        public Sprite icon;

        [LabelText("标签")]
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public sealed class ItemInteractConfig
    {
        [LabelText("可交互")]
        public bool canInteract;

        [ShowIf(nameof(canInteract))]
        [LabelText("使用方式")]
        public ItemUseMode useMode = ItemUseMode.Interact;

        [ShowIf(nameof(canInteract))]
        [LabelText("交互提示")]
        public string prompt;

        [ShowIf(nameof(canInteract))]
        [LabelText("交互距离")]
        public float distance = 1.5f;

        [ShowIf(nameof(canInteract))]
        [LabelText("需要面向")]
        public bool requireFacing = true;

        [ShowIf(nameof(canInteract))]
        [LabelText("交互条件")]
        [SerializeReference]
        public ESGetBoolExpression condition;
    }

    [Serializable]
    public sealed class ItemLogicConfig
    {
        [LabelText("生成时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("生成时")]
        public ESOutputOp onSpawn;

        [LabelText("使用时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("使用时")]
        public ESOutputOp onUse;

        [LabelText("命中时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("命中时")]
        public ESOutputOp onHit;

        [LabelText("过期时")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("过期时")]
        public ESOutputOp onExpire;

        [LabelText("销毁前")]
        [SerializeReference, HideReferenceObjectPicker]
        [ESCompactEdit("销毁前")]
        public ESOutputOp onDestroy;
    }

    [Serializable]
    public sealed class ItemMoveConfig
    {
        [LabelText("移动方式")]
        public ItemMoveMode mode = ItemMoveMode.None;

        [ShowIf(nameof(UsesMoveSpeed))]
        [LabelText("移动速度")]
        public float speed = 1f;

        [ShowIf(nameof(UsesRotateSpeed))]
        [LabelText("旋转速度")]
        public float rotateSpeed = 90f;

        [ShowIf(nameof(UsesDistance))]
        [LabelText("移动距离/角度")]
        public float amount = 1f;

        [ShowIf(nameof(UsesPath))]
        [LabelText("路径点")]
        public List<Vector3> points = new List<Vector3>();

        [ShowIf(nameof(UsesLoop))]
        [LabelText("循环")]
        public bool loop = true;

        [ShowIf(nameof(UsesBackAndForth))]
        [LabelText("往返")]
        public bool backAndForth = true;

        [ShowIf(nameof(UsesGravity))]
        [LabelText("使用重力")]
        public bool useGravity = true;

        private bool UsesMoveSpeed()
        {
            return mode == ItemMoveMode.Line
                || mode == ItemMoveMode.Path
                || mode == ItemMoveMode.Platform
                || mode == ItemMoveMode.Drop
                || mode == ItemMoveMode.Follow
                || mode == ItemMoveMode.Zone;
        }

        private bool UsesRotateSpeed()
        {
            return mode == ItemMoveMode.Rotate || mode == ItemMoveMode.Swing;
        }

        private bool UsesDistance()
        {
            return mode == ItemMoveMode.Line
                || mode == ItemMoveMode.Swing
                || mode == ItemMoveMode.Platform
                || mode == ItemMoveMode.Zone;
        }

        private bool UsesPath() => mode == ItemMoveMode.Path;

        private bool UsesLoop()
        {
            return mode == ItemMoveMode.Path
                || mode == ItemMoveMode.Rotate
                || mode == ItemMoveMode.Zone;
        }

        private bool UsesBackAndForth()
        {
            return mode == ItemMoveMode.Platform || mode == ItemMoveMode.Swing;
        }

        private bool UsesGravity() => mode == ItemMoveMode.Drop;
    }
}
