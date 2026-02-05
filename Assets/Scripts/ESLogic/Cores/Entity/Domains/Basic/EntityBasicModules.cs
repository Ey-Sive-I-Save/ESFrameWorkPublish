using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("基础域模块基类")]
    public abstract class EntityBasicModuleBase : Module<Entity, EntityBasicDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    [Serializable, TypeRegistryItem("基础移动旋转模块")]
    public class EntityBasicMoveRotateModule : EntityBasicModuleBase
    {
        [Title("输入（世界空间）")]
        public Vector3 moveWorld;
        public Vector3 lookWorld = Vector3.forward;

        [Title("应用开关")]
        public bool applyMove = true;
        public bool applyLook = true;

        [Title("跳跃/下蹲")]
        [ReadOnly] public bool crouchHold;
        [ReadOnly] public bool jumpRequested;

        public void SetMoveWorld(Vector3 move)
        {
            moveWorld = move;
        }

        public void SetLookWorld(Vector3 look)
        {
            if (look.sqrMagnitude > 0f)
            {
                lookWorld = look.normalized;
            }
        }

        public void RequestJump()
        {
            jumpRequested = true;
        }

        public void SetCrouch(bool enable)
        {
            crouchHold = enable;
        }

        protected override void Update()
        {
            if (MyCore == null) return;
            if (applyMove) MyCore.SetMoveInput(moveWorld);
            if (applyLook) MyCore.SetLookInput(lookWorld);
            if (jumpRequested)
            {
                MyCore.RequestJump();
                jumpRequested = false;
            }
            MyCore.SetCrouch(crouchHold);
            
            // ★ 自动更新StateMachine的SpeedX和SpeedZ参数（移动模块的核心职责）
            // 通过Entity的stateDomain访问stateMachine，实现Basic域与State域的联动
            if (applyMove && MyCore.stateDomain != null && MyCore.stateDomain.stateMachine != null)
            {
                var context = MyCore.stateDomain.stateMachine.stateContext;
                if (context != null)
                {
                    // 将世界空间的移动向量转换为角色局部空间（相对于角色朝向）
                    Vector3 moveLocal = MyCore.transform.InverseTransformDirection(moveWorld);
                    // 使用局部坐标系设置SpeedX（右）和SpeedZ（前）
                    context.SpeedX = moveLocal.x;
                    context.SpeedZ = moveLocal.z;
                }
            }
        }
    }

    [Serializable, TypeRegistryItem("基础战斗模块")]
    public class EntityBasicCombatModule : EntityBasicModuleBase
    {
        [Title("状态")]
        [ReadOnly] public bool isBlocking;
        [ReadOnly] public bool isSliding;
        [ReadOnly] public bool isAiming;
        [ReadOnly] public int weaponIndex;

        [Title("最近触发")]
        [ReadOnly] public float lastAttackTime;
        [ReadOnly] public float lastHeavyAttackTime;

        public void TriggerAttack()
        {
            lastAttackTime = Time.time;
        }

        public void TriggerHeavyAttack()
        {
            lastHeavyAttackTime = Time.time;
        }

        public void SetBlock(bool enable)
        {
            isBlocking = enable;
        }

        public void SetSlide(bool enable)
        {
            isSliding = enable;
        }

        public void SetAim(bool enable)
        {
            isAiming = enable;
        }

        public void SwitchWeaponNext()
        {
            weaponIndex++;
        }
    }

    [Serializable]
    public struct FlyParams
    {
        [LabelText("最大飞行速度")]
        public float flyMaxSpeed;

        [LabelText("飞行加速")]
        public float flyAcceleration;

        [LabelText("飞行阻力")]
        public float flyDrag;

        public static FlyParams Default => new FlyParams
        {
            flyMaxSpeed = 10f,
            flyAcceleration = 12f,
            flyDrag = 0.2f
        };
    }

    [Serializable]
    public struct SwimParams
    {
        [LabelText("最大游泳速度")]
        public float swimMaxSpeed;

        [LabelText("游泳加速")]
        public float swimAcceleration;

        [LabelText("水阻力")]
        public float swimDrag;

        [LabelText("浮力")]
        public float swimBuoyancy;

        public static SwimParams Default => new SwimParams
        {
            swimMaxSpeed = 6f,
            swimAcceleration = 8f,
            swimDrag = 0.5f,
            swimBuoyancy = 6f
        };
    }

    [Serializable, TypeRegistryItem("基础飞行模块")]
    public class EntityBasicFlyModule : EntityBasicModuleBase
    {
        [Title("开关")]
        public bool enableFly = true;

        [Title("应用策略")]
        [LabelText("启用时应用参数")]
        public bool applyOnEnable = true;

        [LabelText("每帧持续应用参数")]
        public bool applyEveryFrame;

        [Title("飞行参数")]
        [InlineProperty, HideLabel]
        public FlyParams fly = FlyParams.Default;

        [Title("输入")]
        [LabelText("垂直输入")]
        public float verticalInput;

        public void SetVerticalInput(float input)
        {
            verticalInput = Mathf.Clamp(input, -1f, 1f);
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
            var kcc = MyCore.kcc;
            kcc.flyMaxSpeed = fly.flyMaxSpeed;
            kcc.flyAcceleration = fly.flyAcceleration;
            kcc.flyDrag = fly.flyDrag;
        }
    }

    [Serializable, TypeRegistryItem("基础游泳模块")]
    public class EntityBasicSwimModule : EntityBasicModuleBase
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
            var kcc = MyCore.kcc;
            kcc.swimMaxSpeed = swim.swimMaxSpeed;
            kcc.swimAcceleration = swim.swimAcceleration;
            kcc.swimDrag = swim.swimDrag;
            kcc.swimBuoyancy = swim.swimBuoyancy;
        }
    }

    [Serializable, TypeRegistryItem("基础技能模块")]
    public class EntityBasicSkillModule : EntityBasicModuleBase
    {
        [Title("最近触发")]
        [ReadOnly] public int lastSkillIndex = -1;
        [ReadOnly] public float lastSkillTime;

        [Title("技能效果")]
        [LabelText("启用技能效果")]
        public bool enableSkillEffects = true;

        [LabelText("技能效果列表")]
        public System.Collections.Generic.List<SkillEffectConfig> skillEffects = new System.Collections.Generic.List<SkillEffectConfig>();

        [NonSerialized] private bool _hasActiveEffect;
        [NonSerialized] private SkillEffectConfig _activeEffect;
        [NonSerialized] private float _activeEffectEndTime;

        public void TriggerSkill(int index)
        {
            lastSkillIndex = index;
            lastSkillTime = Time.time;

            if (!enableSkillEffects) return;
            var effect = FindEffect(index);
            if (effect != null)
            {
                ActivateEffect(effect);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!_hasActiveEffect) return;
            if (Time.time < _activeEffectEndTime) return;

            DeactivateEffect();
        }

        private SkillEffectConfig FindEffect(int index)
        {
            if (skillEffects == null) return null;
            for (int i = 0; i < skillEffects.Count; i++)
            {
                var effect = skillEffects[i];
                if (effect != null && effect.skillIndex == index)
                {
                    return effect;
                }
            }
            return null;
        }

        private void ActivateEffect(SkillEffectConfig effect)
        {
            if (effect == null) return;

            if (_hasActiveEffect)
            {
                DeactivateEffect();
            }

            _activeEffect = effect;
            _hasActiveEffect = true;
            _activeEffectEndTime = Time.time + Mathf.Max(0f, effect.duration);

            if (MyCore != null)
            {
                MyCore.SetSpeedMultiplier(effect.speedMultiplier);
                MyCore.SetSpeedLimit(effect.speedLimit);
            }

            if (effect.useDynamicCamera && TryGetCameraModule(out var camModule))
            {
                camModule.SetDynamicCameraSlot(effect.skillCamera, effect.autoActivateCamera);
            }
        }

        private void DeactivateEffect()
        {
            if (!_hasActiveEffect) return;

            if (MyCore != null)
            {
                MyCore.ResetSpeedModifiers();
            }

            if (_activeEffect != null && _activeEffect.useDynamicCamera && TryGetCameraModule(out var camModule))
            {
                camModule.ClearDynamicCamera(_activeEffect.autoActivateCamera);
            }

            _activeEffect = null;
            _hasActiveEffect = false;
        }

        private bool TryGetCameraModule(out EntityBasicCameraModule module)
        {
            module = null;
            if (MyCore == null) return false;
            if (MyCore.ModuleTables.TryGetValue(typeof(EntityBasicCameraModule), out var m))
            {
                module = m as EntityBasicCameraModule;
            }
            return module != null;
        }
    }

    [Serializable]
    public class SkillEffectConfig
    {
        [Title("触发条件")]
        [LabelText("技能索引")]
        public int skillIndex = 1;

        [Title("持续时间")]
        [LabelText("持续时长(秒)")]
        public float duration = 0.25f;

        [Title("速度影响")]
        [LabelText("速度倍率")]
        public float speedMultiplier = 1f;

        [LabelText("速度上限(<=0 不限制)")]
        public float speedLimit = 0f;

        [Title("技能专属相机")]
        [LabelText("使用动态相机")]
        public bool useDynamicCamera = false;

        [LabelText("自动启用相机组件")]
        public bool autoActivateCamera = true;

        [InlineProperty, HideLabel]
        public VirtualCameraSlot skillCamera = new VirtualCameraSlot();
    }

    [Serializable, TypeRegistryItem("基础相机模块")]
    public class EntityBasicCameraModule : EntityBasicModuleBase
    {
        [Title("模式")]
        public CameraMode mode = CameraMode.ThirdPerson;

        [Title("第一人称")]
        [InlineProperty, HideLabel]
        public CameraRig firstPerson = new CameraRig();

        [Title("第三人称")]
        [InlineProperty, HideLabel]
        public CameraRig thirdPerson = new CameraRig();

        [Title("当前激活")]
        [ShowInInspector, ReadOnly]
        public Transform activeCameraTransform;

        public void SetMode(CameraMode newMode)
        {
            mode = newMode;
            RefreshActiveCamera();
        }

        public void SetDynamicCamera(Transform camTransform)
        {
            GetRig(mode).dynamicSlot.Set(camTransform);
            RefreshActiveCamera();
        }

        public void SetDynamicCameraSlot(VirtualCameraSlot slot, bool activate = true)
        {
            if (slot == null)
            {
                ClearDynamicCamera(activate);
                return;
            }

            var rig = GetRig(mode);
            rig.dynamicSlot.CopyFrom(slot);
            if (activate)
            {
                rig.dynamicSlot.SetActive(true);
            }
            RefreshActiveCamera();
        }

        public void ClearDynamicCamera(bool deactivate = true)
        {
            var rig = GetRig(mode);
            if (deactivate)
            {
                rig.dynamicSlot.SetActive(false);
            }
            rig.dynamicSlot.Clear();
            RefreshActiveCamera();
        }

        public Transform GetActiveCameraTransform()
        {
            RefreshActiveCamera();
            return activeCameraTransform;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshActiveCamera();
        }

        private void RefreshActiveCamera()
        {
            var rig = GetRig(mode);
            activeCameraTransform = rig.GetBestTransform();
        }

        private CameraRig GetRig(CameraMode m)
        {
            return m == CameraMode.FirstPerson ? firstPerson : thirdPerson;
        }
    }

    public enum CameraMode
    {
        FirstPerson,
        ThirdPerson
    }

    [Serializable]
    public class CameraRig
    {
        [Title("Main")]
        [InlineProperty, HideLabel]
        public VirtualCameraSlot main = new VirtualCameraSlot();

        [Title("扩展槽位 A")]
        [InlineProperty, HideLabel]
        public VirtualCameraSlot slotA = new VirtualCameraSlot();

        [Title("扩展槽位 B")]
        [InlineProperty, HideLabel]
        public VirtualCameraSlot slotB = new VirtualCameraSlot();

        [Title("Dynamic")]
        [InlineProperty, HideLabel]
        public VirtualCameraSlot dynamicSlot = new VirtualCameraSlot();

        public Transform GetBestTransform()
        {
            return dynamicSlot.Transform ?? main.Transform ?? slotA.Transform ?? slotB.Transform;
        }
    }

    [Serializable]
    public class VirtualCameraSlot
    {
        [LabelText("Virtual Camera")]
        public Component virtualCamera;

        [LabelText("备用 Transform")]
        public Transform fallbackTransform;

        public Transform Transform => virtualCamera != null ? virtualCamera.transform : fallbackTransform;

        public void Set(Transform transform)
        {
            fallbackTransform = transform;
        }

        public void CopyFrom(VirtualCameraSlot other)
        {
            if (other == null) return;
            virtualCamera = other.virtualCamera;
            fallbackTransform = other.fallbackTransform;
        }

        public void Clear()
        {
            virtualCamera = null;
            fallbackTransform = null;
        }

        public void SetActive(bool active)
        {
            if (virtualCamera is Behaviour behaviour)
            {
                behaviour.enabled = active;
            }
        }
    }

    [Serializable, TypeRegistryItem("基础根运动模块")]
    public class EntityBasicRootMotionModule : EntityBasicModuleBase
    {
        [Title("开关")]
        public bool enableRootMotion = true;

        [Title("参数")]
        public float minDeltaTime = 0.0001f;

        [NonSerialized] private Animator _cachedAnimator;

        public override void Start()
        {
            if (MyCore != null)
            {
                _cachedAnimator = MyCore.animator;
            }
        }

        protected override void Update()
        {
            if (!enableRootMotion || MyCore == null) return;
            if (_cachedAnimator == null)
            {
                _cachedAnimator = MyCore.animator;
            }
            if (_cachedAnimator == null) return;

            float dt = Time.deltaTime;
            if (dt <= minDeltaTime) return;

            Vector3 velocity = _cachedAnimator.deltaPosition / dt;
            MyCore.SetRootMotionVelocity(velocity);
        }

        public override void OnDestroy()
        {
            if (MyCore != null)
            {
                MyCore.ClearRootMotionVelocity();
            }
            base.OnDestroy();
        }
    }
}
