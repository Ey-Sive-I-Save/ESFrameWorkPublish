using System;

namespace ES
{
    /// <summary>Strongly typed reference to a built-in float parameter.</summary>
    public readonly struct StateFloatParameter : IEquatable<StateFloatParameter>
    {
        internal readonly StateDefaultFloatParameter id;
        internal StateFloatParameter(StateDefaultFloatParameter id) { this.id = id; }
        public bool IsValid => id != StateDefaultFloatParameter.None;
        public bool Equals(StateFloatParameter other) => id == other.id;
        public override bool Equals(object obj) => obj is StateFloatParameter other && Equals(other);
        public override int GetHashCode() => (int)id;
    }

    /// <summary>Strongly typed reference to a built-in int parameter.</summary>
    public readonly struct StateIntParameter : IEquatable<StateIntParameter>
    {
        internal readonly StateDefaultIntParameter id;
        internal StateIntParameter(StateDefaultIntParameter id) { this.id = id; }
        public bool IsValid => id != StateDefaultIntParameter.None;
        public bool Equals(StateIntParameter other) => id == other.id;
        public override bool Equals(object obj) => obj is StateIntParameter other && Equals(other);
        public override int GetHashCode() => (int)id;
    }

    /// <summary>Strongly typed reference to a built-in bool parameter.</summary>
    public readonly struct StateBoolParameter : IEquatable<StateBoolParameter>
    {
        internal readonly StateDefaultBoolParameter id;
        internal StateBoolParameter(StateDefaultBoolParameter id) { this.id = id; }
        public bool IsValid => id != StateDefaultBoolParameter.None;
        public bool Equals(StateBoolParameter other) => id == other.id;
        public override bool Equals(object obj) => obj is StateBoolParameter other && Equals(other);
        public override int GetHashCode() => (int)id;
    }

    /// <summary>
    /// Complete built-in parameter surface of StateMachine. New gameplay code must use these
    /// typed keys; runtime string keys are legacy compatibility only.
    /// </summary>
    public static class StateCoreParams
    {
        public static readonly StateFloatParameter MoveX = new StateFloatParameter(StateDefaultFloatParameter.MoveX);
        public static readonly StateFloatParameter MoveZ = new StateFloatParameter(StateDefaultFloatParameter.MoveZ);
        public static readonly StateFloatParameter VerticalSpeed = new StateFloatParameter(StateDefaultFloatParameter.VerticalSpeed);
        public static readonly StateFloatParameter AimYaw = new StateFloatParameter(StateDefaultFloatParameter.AimYaw);
        public static readonly StateFloatParameter AimPitch = new StateFloatParameter(StateDefaultFloatParameter.AimPitch);
        public static readonly StateFloatParameter ClimbX = new StateFloatParameter(StateDefaultFloatParameter.ClimbX);
        public static readonly StateFloatParameter ClimbY = new StateFloatParameter(StateDefaultFloatParameter.ClimbY);
        public static readonly StateFloatParameter WeaponEquipWeight = new StateFloatParameter(StateDefaultFloatParameter.WeaponEquipWeight);
        public static readonly StateFloatParameter UpperBodyWeight = new StateFloatParameter(StateDefaultFloatParameter.UpperBodyWeight);
        public static readonly StateFloatParameter WeaponFirePulse = new StateFloatParameter(StateDefaultFloatParameter.WeaponFirePulse);
        public static readonly StateFloatParameter WeaponInHandWeight = new StateFloatParameter(StateDefaultFloatParameter.WeaponInHandWeight);
        public static readonly StateFloatParameter FootSupportShare = new StateFloatParameter(StateDefaultFloatParameter.FootSupportShare);

        public static readonly StateBoolParameter IsGrounded = new StateBoolParameter(StateDefaultBoolParameter.IsGrounded);
        public static readonly StateBoolParameter WantsSprint = new StateBoolParameter(StateDefaultBoolParameter.WantsSprint);
        public static readonly StateBoolParameter IsAiming = new StateBoolParameter(StateDefaultBoolParameter.IsAiming);
        public static readonly StateIntParameter Stance = new StateIntParameter(StateDefaultIntParameter.Stance);
        public static readonly StateIntParameter LocomotionMode = new StateIntParameter(StateDefaultIntParameter.LocomotionMode);
        public static readonly StateIntParameter ActionPhase = new StateIntParameter(StateDefaultIntParameter.ActionPhase);
        public static readonly StateIntParameter WeaponSlot = new StateIntParameter(StateDefaultIntParameter.WeaponSlot);
    }
}
