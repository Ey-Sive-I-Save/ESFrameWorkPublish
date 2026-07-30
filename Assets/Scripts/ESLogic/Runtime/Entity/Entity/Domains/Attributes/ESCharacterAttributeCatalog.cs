using System.Collections.Generic;

namespace ES
{
    /// <summary>
    /// Fixed, high-frequency float attributes owned by a character. The explicit numeric values are
    /// part of the runtime ABI: append new members before Count, never reorder existing members.
    /// </summary>
    public enum ESCharacterFloatAttributeId : byte
    {
        GroundMaxMoveSpeed = 0,
        AirMaxMoveSpeed = 1,
        GroundMovementSharpness = 2,
        AirAcceleration = 3,
        Drag = 4,
        JumpSpeed = 5,
        JumpSpeedMultiplier = 6,
        JumpApexGravityMultiplier = 7,
        JumpFallGravityMultiplier = 8,
        CrouchSpeedMultiplier = 9,
        OrientationSharpness = 10,
        RootMotionScale = 11,
        Count = 12
    }

    /// <summary>
    /// Fixed, high-frequency permission attributes owned by a character. Append-only for the same
    /// reason as <see cref="ESCharacterFloatAttributeId"/>.
    /// </summary>
    public enum ESCharacterPermitAttributeId : byte
    {
        Move = 0,
        Jump = 1,
        Rotate = 2,
        Count = 3
    }

    /// <summary>
    /// Stable configuration identity for the character's built-in attributes. This is distinct
    /// from the compact float/permit HotSlot enums above: it is serialized with StringKey and
    /// resolved by ESSuperAttributeCatalog before a runtime process assigns dense keys.
    /// </summary>
    public enum ESCharacterAttributeEnumKey : ushort
    {
        None = 0,
        GroundMaxMoveSpeed = 1,
        AirMaxMoveSpeed = 2,
        GroundMovementSharpness = 3,
        AirAcceleration = 4,
        Drag = 5,
        JumpSpeed = 6,
        JumpSpeedMultiplier = 7,
        JumpApexGravityMultiplier = 8,
        JumpFallGravityMultiplier = 9,
        CrouchSpeedMultiplier = 10,
        OrientationSharpness = 11,
        RootMotionScale = 12,
        CanMove = 101,
        CanJump = 102,
        CanRotate = 103
    }

    /// <summary>Value family used by the key catalog. It prevents a Float key from becoming a Permit set by mistake.</summary>
    public enum ESCharacterAttributeValueKind : byte
    {
        Float = 0,
        Permit = 1
    }

    /// <summary>
    /// Backward-compatible serialized keys. New runtime code should use the typed IDs above;
    /// strings remain the data/configuration boundary and custom sparse-attribute boundary.
    /// </summary>
    public static class ESCharacterSuperAttributeKeys
    {
        public const string GroundMaxMoveSpeed = "Character.Movement.GroundMaxSpeed";
        public const string AirMaxMoveSpeed = "Character.Movement.AirMaxSpeed";
        public const string GroundMovementSharpness = "Character.Movement.GroundSharpness";
        public const string AirAcceleration = "Character.Movement.AirAcceleration";
        public const string Drag = "Character.Movement.Drag";
        public const string JumpSpeed = "Character.Movement.JumpSpeed";
        public const string JumpSpeedMultiplier = "Character.Movement.JumpSpeedMultiplier";
        public const string JumpApexGravityMultiplier = "Character.Movement.JumpApexGravityMultiplier";
        public const string JumpFallGravityMultiplier = "Character.Movement.JumpFallGravityMultiplier";
        public const string CrouchSpeedMultiplier = "Character.Movement.CrouchSpeedMultiplier";
        public const string OrientationSharpness = "Character.Movement.OrientationSharpness";
        public const string RootMotionScale = "Character.Movement.RootMotionScale";

        public const string CanMove = "Character.Permit.Move";
        public const string CanJump = "Character.Permit.Jump";
        public const string CanRotate = "Character.Permit.Rotate";
    }

    /// <summary>
    /// The single mapping between serialized character keys and fixed runtime slots.
    /// There is intentionally no Dictionary here: KCC and other hot callers use numeric IDs.
    /// </summary>
    public static class ESCharacterAttributeCatalog
    {
        private static readonly string[] FloatKeys =
        {
            ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed,
            ESCharacterSuperAttributeKeys.AirMaxMoveSpeed,
            ESCharacterSuperAttributeKeys.GroundMovementSharpness,
            ESCharacterSuperAttributeKeys.AirAcceleration,
            ESCharacterSuperAttributeKeys.Drag,
            ESCharacterSuperAttributeKeys.JumpSpeed,
            ESCharacterSuperAttributeKeys.JumpSpeedMultiplier,
            ESCharacterSuperAttributeKeys.JumpApexGravityMultiplier,
            ESCharacterSuperAttributeKeys.JumpFallGravityMultiplier,
            ESCharacterSuperAttributeKeys.CrouchSpeedMultiplier,
            ESCharacterSuperAttributeKeys.OrientationSharpness,
            ESCharacterSuperAttributeKeys.RootMotionScale
        };

        private static readonly string[] PermitKeys =
        {
            ESCharacterSuperAttributeKeys.CanMove,
            ESCharacterSuperAttributeKeys.CanJump,
            ESCharacterSuperAttributeKeys.CanRotate
        };

        private static readonly ushort[] FloatEnumKeys =
        {
            (ushort)ESCharacterAttributeEnumKey.GroundMaxMoveSpeed,
            (ushort)ESCharacterAttributeEnumKey.AirMaxMoveSpeed,
            (ushort)ESCharacterAttributeEnumKey.GroundMovementSharpness,
            (ushort)ESCharacterAttributeEnumKey.AirAcceleration,
            (ushort)ESCharacterAttributeEnumKey.Drag,
            (ushort)ESCharacterAttributeEnumKey.JumpSpeed,
            (ushort)ESCharacterAttributeEnumKey.JumpSpeedMultiplier,
            (ushort)ESCharacterAttributeEnumKey.JumpApexGravityMultiplier,
            (ushort)ESCharacterAttributeEnumKey.JumpFallGravityMultiplier,
            (ushort)ESCharacterAttributeEnumKey.CrouchSpeedMultiplier,
            (ushort)ESCharacterAttributeEnumKey.OrientationSharpness,
            (ushort)ESCharacterAttributeEnumKey.RootMotionScale
        };

        private static readonly ushort[] PermitEnumKeys =
        {
            (ushort)ESCharacterAttributeEnumKey.CanMove,
            (ushort)ESCharacterAttributeEnumKey.CanJump,
            (ushort)ESCharacterAttributeEnumKey.CanRotate
        };

        public static int FloatCount => (int)ESCharacterFloatAttributeId.Count;
        public static int PermitCount => (int)ESCharacterPermitAttributeId.Count;

        /// <summary>Migrates the former generic default scope used by serialized character data.</summary>
        public static void EnsureCharacterScope(ESSuperAttributeTable table)
        {
            if (table == null
                || (!string.IsNullOrEmpty(table.catalogScope)
                    && !string.Equals(table.catalogScope, ESSuperAttributeCatalog.DefaultScope, System.StringComparison.Ordinal)))
                return;

            table.catalogScope = "Attribute.Character";
            table.InvalidateCache();
        }

        /// <summary>
        /// 角色领域的默认属性 Schema。表本身是通用 <see cref="ESSuperAttributeTable"/>；
        /// 只有角色消费者选择了本 Catalog，才会得到这些运动与控制键。
        /// </summary>
        public static ESSuperAttributeTable CreateDefaultSuperAttributeTable()
        {
            return new ESSuperAttributeTable
            {
                catalogScope = "Attribute.Character",
                floatAttributes = new List<ESSuperFloatAttributeDefinition>
                {
                    Float(ESCharacterAttributeEnumKey.GroundMaxMoveSpeed, ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed, "地面最大速度"),
                    Float(ESCharacterAttributeEnumKey.AirMaxMoveSpeed, ESCharacterSuperAttributeKeys.AirMaxMoveSpeed, "空中最大速度"),
                    Float(ESCharacterAttributeEnumKey.GroundMovementSharpness, ESCharacterSuperAttributeKeys.GroundMovementSharpness, "地面响应"),
                    Float(ESCharacterAttributeEnumKey.AirAcceleration, ESCharacterSuperAttributeKeys.AirAcceleration, "空中加速度"),
                    Float(ESCharacterAttributeEnumKey.Drag, ESCharacterSuperAttributeKeys.Drag, "阻力"),
                    Float(ESCharacterAttributeEnumKey.JumpSpeed, ESCharacterSuperAttributeKeys.JumpSpeed, "跳跃速度"),
                    Float(ESCharacterAttributeEnumKey.JumpSpeedMultiplier, ESCharacterSuperAttributeKeys.JumpSpeedMultiplier, "跳跃倍率"),
                    Float(ESCharacterAttributeEnumKey.JumpApexGravityMultiplier, ESCharacterSuperAttributeKeys.JumpApexGravityMultiplier, "上升重力倍率"),
                    Float(ESCharacterAttributeEnumKey.JumpFallGravityMultiplier, ESCharacterSuperAttributeKeys.JumpFallGravityMultiplier, "下落重力倍率"),
                    Float(ESCharacterAttributeEnumKey.CrouchSpeedMultiplier, ESCharacterSuperAttributeKeys.CrouchSpeedMultiplier, "下蹲速度倍率"),
                    Float(ESCharacterAttributeEnumKey.OrientationSharpness, ESCharacterSuperAttributeKeys.OrientationSharpness, "转向响应"),
                    Float(ESCharacterAttributeEnumKey.RootMotionScale, ESCharacterSuperAttributeKeys.RootMotionScale, "根运动倍率")
                },
                permitAttributes = new List<ESSuperPermitAttributeDefinition>
                {
                    Permit(ESCharacterAttributeEnumKey.CanMove, ESCharacterSuperAttributeKeys.CanMove, "允许移动"),
                    Permit(ESCharacterAttributeEnumKey.CanJump, ESCharacterSuperAttributeKeys.CanJump, "允许跳跃"),
                    Permit(ESCharacterAttributeEnumKey.CanRotate, ESCharacterSuperAttributeKeys.CanRotate, "允许转向")
                }
            };
        }

        public static bool IsValid(ESCharacterFloatAttributeId id)
        {
            return (uint)id < (uint)ESCharacterFloatAttributeId.Count;
        }

        public static bool IsValid(ESCharacterPermitAttributeId id)
        {
            return (uint)id < (uint)ESCharacterPermitAttributeId.Count;
        }

        public static string GetKey(ESCharacterFloatAttributeId id)
        {
            return IsValid(id) ? FloatKeys[(int)id] : null;
        }

        public static ushort GetEnumKey(ESCharacterFloatAttributeId id)
        {
            return IsValid(id) ? FloatEnumKeys[(int)id] : (ushort)0;
        }

        public static string GetKey(ESCharacterPermitAttributeId id)
        {
            return IsValid(id) ? PermitKeys[(int)id] : null;
        }

        public static ushort GetEnumKey(ESCharacterPermitAttributeId id)
        {
            return IsValid(id) ? PermitEnumKeys[(int)id] : (ushort)0;
        }

        public static bool TryGetFloatId(ushort enumKey, out ESCharacterFloatAttributeId id)
        {
            for (int i = 0; i < FloatEnumKeys.Length; i++)
            {
                if (FloatEnumKeys[i] == enumKey)
                {
                    id = (ESCharacterFloatAttributeId)i;
                    return true;
                }
            }

            id = default;
            return false;
        }

        public static bool TryGetPermitId(ushort enumKey, out ESCharacterPermitAttributeId id)
        {
            for (int i = 0; i < PermitEnumKeys.Length; i++)
            {
                if (PermitEnumKeys[i] == enumKey)
                {
                    id = (ESCharacterPermitAttributeId)i;
                    return true;
                }
            }

            id = default;
            return false;
        }

        public static bool TryGetFloatId(string key, out ESCharacterFloatAttributeId id)
        {
            switch (key)
            {
                case ESCharacterSuperAttributeKeys.GroundMaxMoveSpeed: id = ESCharacterFloatAttributeId.GroundMaxMoveSpeed; return true;
                case ESCharacterSuperAttributeKeys.AirMaxMoveSpeed: id = ESCharacterFloatAttributeId.AirMaxMoveSpeed; return true;
                case ESCharacterSuperAttributeKeys.GroundMovementSharpness: id = ESCharacterFloatAttributeId.GroundMovementSharpness; return true;
                case ESCharacterSuperAttributeKeys.AirAcceleration: id = ESCharacterFloatAttributeId.AirAcceleration; return true;
                case ESCharacterSuperAttributeKeys.Drag: id = ESCharacterFloatAttributeId.Drag; return true;
                case ESCharacterSuperAttributeKeys.JumpSpeed: id = ESCharacterFloatAttributeId.JumpSpeed; return true;
                case ESCharacterSuperAttributeKeys.JumpSpeedMultiplier: id = ESCharacterFloatAttributeId.JumpSpeedMultiplier; return true;
                case ESCharacterSuperAttributeKeys.JumpApexGravityMultiplier: id = ESCharacterFloatAttributeId.JumpApexGravityMultiplier; return true;
                case ESCharacterSuperAttributeKeys.JumpFallGravityMultiplier: id = ESCharacterFloatAttributeId.JumpFallGravityMultiplier; return true;
                case ESCharacterSuperAttributeKeys.CrouchSpeedMultiplier: id = ESCharacterFloatAttributeId.CrouchSpeedMultiplier; return true;
                case ESCharacterSuperAttributeKeys.OrientationSharpness: id = ESCharacterFloatAttributeId.OrientationSharpness; return true;
                case ESCharacterSuperAttributeKeys.RootMotionScale: id = ESCharacterFloatAttributeId.RootMotionScale; return true;
                default: id = default; return false;
            }
        }

        public static bool TryGetPermitId(string key, out ESCharacterPermitAttributeId id)
        {
            switch (key)
            {
                case ESCharacterSuperAttributeKeys.CanMove: id = ESCharacterPermitAttributeId.Move; return true;
                case ESCharacterSuperAttributeKeys.CanJump: id = ESCharacterPermitAttributeId.Jump; return true;
                case ESCharacterSuperAttributeKeys.CanRotate: id = ESCharacterPermitAttributeId.Rotate; return true;
                default: id = default; return false;
            }
        }

        public static bool TryGetValueKind(string key, out ESCharacterAttributeValueKind kind)
        {
            if (TryGetFloatId(key, out _))
            {
                kind = ESCharacterAttributeValueKind.Float;
                return true;
            }

            if (TryGetPermitId(key, out _))
            {
                kind = ESCharacterAttributeValueKind.Permit;
                return true;
            }

            kind = default;
            return false;
        }

        public static bool TryGetValueKind(ushort enumKey, out ESCharacterAttributeValueKind kind)
        {
            if (TryGetFloatId(enumKey, out _))
            {
                kind = ESCharacterAttributeValueKind.Float;
                return true;
            }

            if (TryGetPermitId(enumKey, out _))
            {
                kind = ESCharacterAttributeValueKind.Permit;
                return true;
            }

            kind = default;
            return false;
        }

        private static ESSuperFloatAttributeDefinition Float(ESCharacterAttributeEnumKey enumKey, string key, string displayName)
        {
            return new ESSuperFloatAttributeDefinition
            {
                enumKey = (ushort)enumKey,
                key = key,
                displayName = displayName,
                storagePolicy = ESKeyStoragePolicy.HotSlot
            };
        }

        private static ESSuperPermitAttributeDefinition Permit(ESCharacterAttributeEnumKey enumKey, string key, string displayName)
        {
            return new ESSuperPermitAttributeDefinition
            {
                enumKey = (ushort)enumKey,
                key = key,
                displayName = displayName,
                fallbackValue = true,
                storagePolicy = ESKeyStoragePolicy.HotSlot
            };
        }
    }
}
