using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础飞行模块")]
    public class EntityBasicFlyModule : EntityBasicModuleBase, IEntitySupportMotion
    {
        [Title("开关")]
        public bool enableFly = true;

        [Title("状态")]
        [ReadOnly] public bool flyHold;

        [LabelText("飞行状态名")]
        public string Fly_StateName = "飞行";

        private StateBase _flyState;
        private StateMachine sm;

        [Title("应用策略")]
        [LabelText("启用时应用参数")]
        public bool applyOnEnable = true;

        [LabelText("每帧持续应用参数")]
        public bool applyEveryFrame;

        [Title("飞行参数")]
        [InlineProperty, HideLabel]
        public FlyParams fly = FlyParams.Default;

        [LabelText("飞行上升倍率")]
        public float flyAscendMultiplier = 1.5f;

        [LabelText("飞行下降倍率")]
        public float flyDescendMultiplier = 1f;

        [LabelText("飞行时强制离地时长")]
        public float flyUngroundTime = 0.1f;

        [Title("输入")]
        [LabelText("垂直输入")]
        public float verticalInput;

        public void SetVerticalInput(float input)
        {
            verticalInput = Mathf.Clamp(input, -1f, 1f);
        }

        public void SetFly(bool enable)
        {
            if (!enableFly || _flyState == null) return;

            if (enable)
            {
                if (_flyState.baseStatus != StateBaseStatus.Running)
                {
                    sm.TryActivateState(_flyState);
                }
            }
            else
            {
                ExitFly();
            }

            flyHold = _flyState.baseStatus == StateBaseStatus.Running;
        }

        public void ToggleFly()
        {
            if (!enableFly || _flyState == null) return;

            if (_flyState.baseStatus == StateBaseStatus.Running)
            {
                ExitFly();
            }
            else
            {
                sm.TryActivateState(_flyState);
            }

            flyHold = _flyState.baseStatus == StateBaseStatus.Running;
        }

        private void ExitFly()
        {
            if (_flyState == null) return;

            if (_flyState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(Fly_StateName);
                if (_flyState.baseStatus == StateBaseStatus.Running)
                {
                    sm.ForceExitState(_flyState);
                }
            }

            flyHold = _flyState.baseStatus == StateBaseStatus.Running;
            if (!flyHold && MyCore != null)
            {
                MyCore.SetLocomotionSupportFlags(StateSupportFlags.Grounded);
                MyCore.SetVerticalInput(0f);
            }
        }

        public override void Start()
        {
            base.Start();
            if (MyCore != null && MyCore.stateDomain != null && MyCore.stateDomain.stateMachine != null)
            {
                sm = MyCore.stateDomain.stateMachine;
                _flyState = sm.GetStateByString(Fly_StateName);
            }
            if (MyCore != null)
            {
                MyCore.kcc.flyModule = this;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (applyOnEnable)
            {
                ApplyParams();
            }
        }

        protected override void Update()
        {
            if (MyCore == null || !enableFly) return;

            flyHold = _flyState != null && _flyState.baseStatus == StateBaseStatus.Running;
            if (!flyHold) return;

            MyCore.SetLocomotionSupportFlags(StateSupportFlags.Flying);
            MyCore.SetVerticalInput(verticalInput);

            if (applyEveryFrame)
            {
                ApplyParams();
            }
        }

        [Button("应用参数")]
        public void ApplyParams()
        {
            if (MyCore == null) return;
        }

        public bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, float deltaTime)
        {
            if (!enableFly || !flyHold || kcc == null || kcc.motor == null) return false;
            if (flyUngroundTime > 0f)
            {
                kcc.motor.ForceUnground(flyUngroundTime);
            }
            return true;
        }

        public bool UpdateRotation(Entity owner, EntityKCCData kcc, ref Quaternion currentRotation, float deltaTime)
        {
            return false;
        }

        public bool UpdateVelocity(Entity owner, EntityKCCData kcc, ref Vector3 currentVelocity, float deltaTime)
        {
            if (!enableFly || kcc == null || kcc.motor == null) return false;

            Vector3 up = kcc.motor.CharacterUp;
            float vertical = kcc.verticalInput;
            if (vertical > 0f)
            {
                vertical *= Mathf.Max(0f, flyAscendMultiplier);
            }
            else if (vertical < 0f)
            {
                vertical *= Mathf.Max(0f, flyDescendMultiplier);
            }
            Vector3 input = kcc.moveInput + up * vertical;
            input = Vector3.ClampMagnitude(input, 1f);
            Vector3 targetMovementVelocity = input * fly.flyMaxSpeed;

            Vector3 velocityDiff = targetMovementVelocity - currentVelocity;
            currentVelocity += velocityDiff * fly.flyAcceleration * deltaTime;
            if (fly.flyGravityScale > 0f)
            {
                currentVelocity += kcc.gravity_ * (fly.flyGravityScale * deltaTime);
            }
            currentVelocity *= (1f / (1f + (fly.flyDrag * deltaTime)));
            return true;
        }

        public override void OnDestroy()
        {
            if (MyCore != null && MyCore.kcc.flyModule == this)
            {
                MyCore.kcc.flyModule = null;
            }
            base.OnDestroy();
        }
    }

    [Serializable, TypeRegistryItem("基础游泳模块")]
    public class EntityBasicSwimModule : EntityBasicModuleBase, IEntitySupportMotion
    {
        [Title("开关")]
        public bool enableSwim = true;

        [Title("应用策略")]
        [LabelText("启用时应用参数")]
        public bool applyOnEnable = true;

        [LabelText("每帧持续应用参数")]
        public bool applyEveryFrame;

        [Title("游泳参数")]
        [InlineProperty, HideLabel]
        public SwimParams swim = SwimParams.Default;

        [Title("输入")]
        [LabelText("垂直输入")]
        public float verticalInput;

        public void SetVerticalInput(float input)
        {
            verticalInput = Mathf.Clamp(input, -1f, 1f);
        }

        public override void Start()
        {
            base.Start();
            if (MyCore != null)
            {
                MyCore.kcc.swimModule = this;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (applyOnEnable)
            {
                ApplyParams();
            }
        }

        protected override void Update()
        {
            if (MyCore == null || !enableSwim) return;

            MyCore.SetLocomotionSupportFlags(StateSupportFlags.Swimming);
            MyCore.SetVerticalInput(verticalInput);

            if (applyEveryFrame)
            {
                ApplyParams();
            }
        }

        [Button("应用参数")]
        public void ApplyParams()
        {
            if (MyCore == null) return;
        }

        public bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, float deltaTime)
        {
            return false;
        }

        public bool UpdateRotation(Entity owner, EntityKCCData kcc, ref Quaternion currentRotation, float deltaTime)
        {
            return false;
        }

        public bool UpdateVelocity(Entity owner, EntityKCCData kcc, ref Vector3 currentVelocity, float deltaTime)
        {
            if (!enableSwim || kcc == null || kcc.motor == null) return false;

            Vector3 up = kcc.motor.CharacterUp;
            Vector3 input = kcc.moveInput + up * kcc.verticalInput;
            input = Vector3.ClampMagnitude(input, 1f);
            Vector3 targetMovementVelocity = input * swim.swimMaxSpeed;

            Vector3 velocityDiff = targetMovementVelocity - currentVelocity;
            currentVelocity += velocityDiff * swim.swimAcceleration * deltaTime;
            currentVelocity += up * swim.swimBuoyancy * deltaTime;
            currentVelocity *= (1f / (1f + (swim.swimDrag * deltaTime)));
            return true;
        }

        public override void OnDestroy()
        {
            if (MyCore != null && MyCore.kcc.swimModule == this)
            {
                MyCore.kcc.swimModule = null;
            }
            base.OnDestroy();
        }
    }
}
