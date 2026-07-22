using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    [Serializable, TypeRegistryItem("AI域模块基类")]
    public abstract class EntityAIModuleBase : Module<Entity, EntityAIDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    [Serializable]
    public sealed class EntityInputState
    {
        [Title("开关")]
        [LabelText("是否启用输入")]
        public bool enableInput = true;

        [Title("输出状态")]
        [ShowInInspector, ReadOnly]
        public EntityMotionInputState motion;

        [ShowInInspector, ReadOnly]
        public EntityActionInputPulse action;

        public Vector2 Move => motion.move;
        public Vector2 Look => motion.look;

        public bool ConsumeAttack() => action.ConsumeAttack();
        public bool ConsumeHeavyAttack() => action.ConsumeHeavyAttack();
        public bool ConsumeBlock() => action.ConsumeBlock();
        public bool ConsumeSlide() => action.ConsumeSlide();
        public bool ConsumeSwitchWeapon() => action.ConsumeSwitchWeapon();
        public bool ConsumeEquipWeapon() => action.ConsumeEquipWeapon();
        public bool ConsumeHolsterWeapon() => action.ConsumeHolsterWeapon();
        public bool ConsumeWeaponSlot1() => action.ConsumeWeaponSlot1();
        public bool ConsumeWeaponSlot2() => action.ConsumeWeaponSlot2();
        public bool ConsumeWeaponSlot3() => action.ConsumeWeaponSlot3();
        public bool ConsumeWeaponSlot4() => action.ConsumeWeaponSlot4();
        public bool ConsumeWeaponSlot5() => action.ConsumeWeaponSlot5();
        public bool ConsumeAim() => action.ConsumeAim();
        public bool ConsumeSkill1() => action.ConsumeSkill1();
        public bool ConsumeSkill2() => action.ConsumeSkill2();
        public bool ConsumeSkill3() => action.ConsumeSkill3();
        public bool ConsumeJump() => action.ConsumeJump();
        public bool ConsumeCrouchToggle() => action.ConsumeCrouchToggle();
        public bool ConsumeFlyToggle() => action.ConsumeFlyToggle();
        public bool ConsumeMountToggle() => action.ConsumeMountToggle();
        public bool ConsumeClimbToggle() => action.ConsumeClimbToggle();
        public bool ConsumeInteract() => action.ConsumeInteract();
        public void ClearOneShot() => action.Clear();
        public bool EyeHold => motion.eyeHold;
        public bool PeekLeftHold => motion.peekLeftHold;
        public bool PeekRightHold => motion.peekRightHold;
        public float AimPeek => motion.AimPeek;
        public float FlyVertical => motion.flyVertical;

        public void ClearAll()
        {
            motion.Clear();
            action.Clear();
        }

        public void SetMotion(Vector2 move, Vector2 look, float flyVertical = 0f)
        {
            motion.move = move;
            motion.look = look;
            motion.flyVertical = flyVertical;
            motion.frameIndex = Time.frameCount;
        }

        public void SetMotionHold(bool eyeHold, bool peekLeftHold, bool peekRightHold)
        {
            motion.eyeHold = eyeHold;
            motion.peekLeftHold = peekLeftHold;
            motion.peekRightHold = peekRightHold;
            motion.frameIndex = Time.frameCount;
        }

        public void PulseAttack()
        {
            action.PulseAttack(Time.frameCount);
        }

        public void PulseInteract()
        {
            action.PulseInteract(Time.frameCount);
        }

        public void PulseSkill(int slot)
        {
            switch (slot)
            {
                case 1:
                    action.PulseSkill1(Time.frameCount);
                    break;
                case 2:
                    action.PulseSkill2(Time.frameCount);
                    break;
                case 3:
                    action.PulseSkill3(Time.frameCount);
                    break;
            }
        }
    }

    [Serializable, TypeRegistryItem("Entity玩家输入写入模块")]
    public class EntityPlayerInputWriteModule : EntityAIModuleBase
    {
        [LabelText("启用玩家输入写入")]
        public bool enablePlayerInput = true;

        protected override void Update()
        {
            if (!enablePlayerInput)
                return;

            EntityInputState state = MyDomain.inputState;
            if (state == null || !state.enableInput)
                return;

            ESInputModule input = ESGameManager.InputModule;
            if (input == null)
            {
                state.ClearAll();
                return;
            }

            state.motion.frameIndex = Time.frameCount;
            state.motion.move = input.ReadVector2(ESInputActionId.Move);
            state.motion.look = input.ReadVector2(ESInputActionId.Look);
            state.motion.flyVertical = input.ReadAxis(ESInputActionId.FlyVertical);
            state.motion.peekLeftHold = input.IsHeld(ESInputActionId.PeekLeft);
            state.motion.peekRightHold = input.IsHeld(ESInputActionId.PeekRight);

            int frame = Time.frameCount;
            if (input.ConsumePressed(ESInputActionId.Attack)) state.action.PulseAttack(frame);
            if (input.ConsumePressed(ESInputActionId.HeavyAttack)) state.action.PulseHeavyAttack(frame);
            if (input.ConsumePressed(ESInputActionId.Block)) state.action.PulseBlock(frame);
            if (input.ConsumePressed(ESInputActionId.Slide)) state.action.PulseSlide(frame);
            if (input.ConsumePressed(ESInputActionId.SwitchWeapon)) state.action.PulseSwitchWeapon(frame);
            if (input.ConsumePressed(ESInputActionId.EquipWeapon)) state.action.PulseEquipWeapon(frame);
            if (input.ConsumePressed(ESInputActionId.HolsterWeapon)) state.action.PulseHolsterWeapon(frame);
            if (input.ConsumePressed(ESInputActionId.WeaponSlot1)) state.action.PulseWeaponSlot1(frame);
            if (input.ConsumePressed(ESInputActionId.WeaponSlot2)) state.action.PulseWeaponSlot2(frame);
            if (input.ConsumePressed(ESInputActionId.WeaponSlot3)) state.action.PulseWeaponSlot3(frame);
            if (input.ConsumePressed(ESInputActionId.WeaponSlot4)) state.action.PulseWeaponSlot4(frame);
            if (input.ConsumePressed(ESInputActionId.WeaponSlot5)) state.action.PulseWeaponSlot5(frame);
            if (input.ConsumePressed(ESInputActionId.Aim)) state.action.PulseAim(frame);
            if (input.ConsumePressed(ESInputActionId.Skill1)) state.action.PulseSkill1(frame);
            if (input.ConsumePressed(ESInputActionId.Skill2)) state.action.PulseSkill2(frame);
            if (input.ConsumePressed(ESInputActionId.Skill3)) state.action.PulseSkill3(frame);
            if (input.ConsumePressed(ESInputActionId.Jump)) state.action.PulseJump(frame);
            if (input.ConsumePressed(ESInputActionId.Crouch)) state.action.PulseCrouchToggle(frame);
            if (input.ConsumePressed(ESInputActionId.Fly)) state.action.PulseFlyToggle(frame);
            if (input.ConsumePressed(ESInputActionId.Mount)) state.action.PulseMountToggle(frame);
            if (input.ConsumePressed(ESInputActionId.Climb)) state.action.PulseClimbToggle(frame);
            if (input.ConsumePressed(ESInputActionId.Interact)) state.action.PulseInteract(frame);
        }
    }

    // =================================================================================================
    // 输入调度模块（AI 域）
    // - 读取 Entity 输入状态
    // - 驱动 Basic 域的“实际生效模块”
    // =================================================================================================
    [Serializable, TypeRegistryItem("AI输入调度模块")]
    public class EntityAIInputDispatchModule : EntityAIModuleBase
    {
        [Title("引用")]
        [LabelText("相机（可选）")]
        public Transform cameraTransform;

        [LabelText("主相机(可选)")]
        public Camera mainCamera;

        [LabelText("移动缩放")]
        public float moveScale = 1f;

        [Title("转身模式")]
        public TurnMode turnMode = TurnMode.FreeLook;

        [LabelText("转身速率")]
        public float turnSpeed = 12f;

        [LabelText("视角平滑")]
        public float lookSmooth = 10f;

        [Title("移动平滑")]
        [LabelText("移动平滑")]
        public float moveSmooth = 12f;

        [LabelText("无输入时立即停下")]
        public bool stopMoveWhenNoInput = true;

        [LabelText("移动死区")]
        public float moveDeadZone = 0.05f;

        [LabelText("攀爬时禁用移动平滑")]
        public bool disableMoveSmoothWhileClimbing = true;

        [Title("相机控制")]
        [LabelText("启用相机上下视角")]
        public bool enableCameraLook = true;

        [LabelText("驱动Cinemachine轴")]
        public bool driveCinemachineAxes = true;

        [LabelText("AIM(可选)")]
        public Transform aimTransform;

        [Title("瞄准驱动")]
        [LabelText("驱动 AimIK")]
        public bool driveAimIK = true;

        [LabelText("AimIK 权重"), Range(0f, 1f)]
        public float aimIKWeight = 0f;

        [LabelText("瞄准目标距离")]
        public float aimTargetDistance = 30f;

        [LabelText("无相机时瞄准高度")]
        public float fallbackAimHeight = 1.5f;

        [LabelText("相机旋转目标(可选)")]
        public Transform cameraPivot;

        [LabelText("相机Yaw速率")]
        public float cameraYawSpeed = 220f;

        [LabelText("相机Pitch速率")]
        public float cameraPitchSpeed = 90f;

        [LabelText("水平旋转倍率")]
        public float yawMultiplier = 1f;

        [LabelText("竖直旋转倍率")]
        public float pitchMultiplier = 1f;

        [LabelText("相机Pitch限制")]
        public Vector2 cameraPitchLimit = new Vector2(-80f, 80f);

        [LabelText("Pitch软限制范围")]
        public float cameraPitchSoftZone = 12f;

        [LabelText("Pitch越界矫正速率")]
        public float cameraPitchCorrectionSpeed = 12f;

        [LabelText("相机旋转平滑")]
        public float cameraLookSmooth = 12f;

        [Title("小眼睛")]
        [LabelText("小眼睛视角倍率")]
        public float eyeLookScale = 1f;

        [LabelText("小眼睛回正速率")]
        public float eyeReturnSpeed = 10f;

        [LabelText("相机调试")]
        public bool debugCamera;

        [LabelText("骑乘调试")]
        public bool debugMount;

        private Vector3 _lastLookWorld = Vector3.forward;
        private Vector3 _smoothedMoveWorld;
        private float _freeLookYaw;
        private bool _freeLookInited;
        private Camera _cachedMainCamera;
        private CameraSource _lastCameraSource = CameraSource.None;

        private float _cameraYaw;
        private float _cameraPitch;
        private float _cameraYawCurrent;
        private float _cameraPitchCurrent;
        private bool _cameraAnglesInited;

        private float _aimYaw;
        private float _aimPitch;
        private float _aimYawCurrent;
        private float _aimPitchCurrent;
        private bool _aimAnglesInited;

        private float _eyeYawOffset;
        private float _eyePitchOffset;

        private float _eyePitchVel;

        private bool _wasClimbing;

        private float _totalMoveTime;
        private float _last1sSpeedSum;
        private readonly Queue<SpeedSample> _speedSamples = new Queue<SpeedSample>(64);
        private Transform _runtimeAimTarget;

        [ShowInInspector, ReadOnly, LabelText("总运动时长")]
        public float totalMoveTime;

        [ShowInInspector, ReadOnly, LabelText("最近1秒平均速率")]
        public float avgSpeedLast1s;

        [ShowInInspector, ReadOnly, LabelText("最近跳跃输入帧")]
        public int lastJumpInputFrame;

        protected override void Update()
        {
            if (MyCore == null || MyDomain == null) return;

            var input = MyDomain.inputState;
            if (input == null || !input.enableInput) return;

            var cam = ResolveCameraTransform();

            bool hasClimbModule = TryGetModule(out EntityBasicClimbModule climbModule);
            bool isClimbing = hasClimbModule && climbModule.subState != ClimbSubState.None;

            HandleClimbEnter(isClimbing);
            DispatchCameraLook(input, cam);
            bool hasMoveModule = DispatchGroundMove(input, cam, isClimbing);
            DispatchFly(input);
            DispatchMount(input, cam, isClimbing, hasMoveModule);
            UpdateMoveStats(_smoothedMoveWorld);
            DispatchClimb(input, climbModule, hasClimbModule);
            DispatchInteraction(input);
            DispatchCombat(input, cam);
            DispatchSkill(input);

            input.ClearOneShot();
            _wasClimbing = isClimbing;
        }

        #region 调度流程

        private void HandleClimbEnter(bool isClimbing)
        {
            if (!isClimbing || _wasClimbing)
                return;

            _smoothedMoveWorld = Vector3.zero;
            MyCore.SetMoveInput(Vector3.zero);
        }

        private void DispatchCameraLook(EntityInputState input, Transform cam)
        {
            if (enableCameraLook)
                ApplyCameraLook(input.Look, input.EyeHold ? eyeLookScale : 1f, cam, input.EyeHold);
        }

        private bool DispatchGroundMove(EntityInputState input, Transform cam, bool isClimbing)
        {
            if (!TryGetModule(out EntityBasicMoveRotateModule moveModule))
                return false;

            ApplyMoveAndLook(input, cam, isClimbing);

            if (input.ConsumeJump())
            {
                lastJumpInputFrame = Time.frameCount;
                moveModule.RequestJump();
            }

            if (input.ConsumeCrouchToggle())
                moveModule.ToggleCrouch();

            return true;
        }

        private void DispatchFly(EntityInputState input)
        {
            if (!TryGetModule(out EntityBasicFlyModule flyModule))
                return;

            if (input.ConsumeFlyToggle())
                flyModule.ToggleFly();

            flyModule.SetVerticalInput(input.FlyVertical);
        }

        private void DispatchMount(EntityInputState input, Transform cam, bool isClimbing, bool hasMoveModule)
        {
            if (!TryGetModule(out global::ES.EntityBasicMountModule mountModule))
                return;

            if (!hasMoveModule)
                ApplyMoveAndLook(input, cam, isClimbing);

            if (input.ConsumeMountToggle())
            {
                if (debugMount)
                    Debug.Log($"[EntityAIInputDispatch] MountToggle consumed | module={mountModule.GetType().Name}");

                mountModule.ToggleMount();
            }
            else if (debugMount)
            {
                Debug.Log("[EntityAIInputDispatch] MountToggle not triggered");
            }
        }

        private void DispatchClimb(EntityInputState input, EntityBasicClimbModule climbModule, bool hasClimbModule)
        {
            if (hasClimbModule && input.ConsumeClimbToggle())
                climbModule.ToggleClimb();
        }

        private void DispatchInteraction(EntityInputState input)
        {
            if (TryGetModule(out EntityBasicInteractionModule interactionModule) && input.ConsumeInteract())
                interactionModule.RequestInteract();
        }

        private void DispatchCombat(EntityInputState input, Transform cam)
        {
            if (!TryGetModule(out EntityBasicCombatModule combatModule))
                return;

            if (input.ConsumeAttack()) combatModule.TriggerAttack();
            if (input.ConsumeHeavyAttack()) combatModule.TriggerHeavyAttack();
            if (input.ConsumeBlock()) combatModule.SetBlock(true);
            if (input.ConsumeSlide()) combatModule.SetSlide(true);
            DispatchWeaponAction(input, combatModule);

            if (input.ConsumeAim())
                combatModule.SetAim(!combatModule.isAiming);

            combatModule.SetAimPeek(input.AimPeek);

            var ikDriver = ResolveIKDriver();
            if (ikDriver != null)
                ApplyCombatAimAndPeek(ikDriver, combatModule, cam);
        }

        private void DispatchWeaponAction(EntityInputState input, EntityBasicCombatModule combatModule)
        {
            if (input.ConsumeWeaponSlot1()) { combatModule.SwitchWeaponTo(0); return; }
            if (input.ConsumeWeaponSlot2()) { combatModule.SwitchWeaponTo(1); return; }
            if (input.ConsumeWeaponSlot3()) { combatModule.SwitchWeaponTo(2); return; }
            if (input.ConsumeWeaponSlot4()) { combatModule.SwitchWeaponTo(3); return; }
            if (input.ConsumeWeaponSlot5()) { combatModule.SwitchWeaponTo(4); return; }
            if (input.ConsumeSwitchWeapon()) { combatModule.SwitchWeaponNext(); return; }
            if (input.ConsumeEquipWeapon()) { combatModule.EquipCurrentWeapon(); return; }
            if (input.ConsumeHolsterWeapon()) combatModule.HolsterCurrentWeapon();
        }

        private void DispatchSkill(EntityInputState input)
        {
            if (!TryGetModule(out EntityBasicSkillModule skillModule))
                return;

            if (input.ConsumeSkill1()) skillModule.TriggerSkill(1);
            if (input.ConsumeSkill2()) skillModule.TriggerSkill(2);
            if (input.ConsumeSkill3()) skillModule.TriggerSkill(3);
        }

        private void ApplyMoveAndLook(EntityInputState input, Transform cam, bool isClimbing)
        {
            Vector3 moveWorld = GetMoveWorld(input.Move, cam, _lastLookWorld) * moveScale;
            if (stopMoveWhenNoInput && input.Move.sqrMagnitude <= moveDeadZone * moveDeadZone)
            {
                _smoothedMoveWorld = Vector3.zero;
                moveWorld = Vector3.zero;
            }

            if (disableMoveSmoothWhileClimbing && isClimbing)
            {
                _smoothedMoveWorld = moveWorld;
                MyCore.SetMoveInput(moveWorld);
            }
            else
            {
                _smoothedMoveWorld = SmoothMove(_smoothedMoveWorld, moveWorld, moveSmooth);
                MyCore.SetMoveInput(_smoothedMoveWorld);
            }

            if (input.EyeHold)
                return;

            Vector3 targetLook = GetLookWorld(input.Look, cam, _smoothedMoveWorld, turnMode);
            _lastLookWorld = SmoothLook(_lastLookWorld, targetLook, lookSmooth);
            MyCore.SetLookInput(_lastLookWorld);
        }

        #endregion

        private bool TryGetModule<T>(out T module) where T : class
        {
            if (MyCore.ModuleTables.TryGetValue(typeof(T), out var m))
            {
                module = m as T;
                return module != null;
            }
            module = null;
            return false;
        }

        private StateFinalIKDriver ResolveIKDriver()
        {
            if (MyCore == null)
                return null;

            StateMachine stateMachine = MyCore.stateDomain != null ? MyCore.stateDomain.stateMachine : null;
            Animator stateAnimator = stateMachine != null && stateMachine.BoundAnimator != null ? stateMachine.BoundAnimator : MyCore.animator;
            return stateAnimator != null ? stateAnimator.GetComponent<StateFinalIKDriver>() : null;
        }

        private void ApplyCombatAimAndPeek(StateFinalIKDriver ikDriver, EntityBasicCombatModule combatModule, Transform cam)
        {
            ikDriver.IKSetPeekViewReference(cam);

            if (!combatModule.isAiming)
            {
                ikDriver.IKClearPeek();
                ikDriver.IKStopAim();
                return;
            }

            if (!driveAimIK)
            {
                ikDriver.IKSetAimTargetWeight(aimIKWeight);
                ikDriver.IKSetPeek(combatModule.aimPeek);
                return;
            }

            var aimTarget = ResolveRuntimeAimTarget(cam);
            ikDriver.IKAimAt(aimTarget, aimIKWeight);
            ikDriver.IKSetPeek(combatModule.aimPeek);
        }

        private Transform ResolveRuntimeAimTarget(Transform cam)
        {
            if (MyCore == null)
                return null;

            EnsureRuntimeAimTarget();

            Vector3 origin;
            Vector3 forward;

            if (cam != null)
            {
                origin = cam.position;
                forward = cam.forward;
            }
            else
            {
                origin = aimTransform != null
                    ? aimTransform.position
                    : MyCore.transform.position + Vector3.up * fallbackAimHeight;

                forward = _lastLookWorld.sqrMagnitude > 0.0001f
                    ? _lastLookWorld.normalized
                    : MyCore.transform.forward;
            }

            if (forward.sqrMagnitude <= 0.0001f)
                forward = Vector3.forward;

            _runtimeAimTarget.SetPositionAndRotation(
                origin + forward.normalized * Mathf.Max(0.1f, aimTargetDistance),
                Quaternion.LookRotation(forward.normalized, Vector3.up));

            return _runtimeAimTarget;
        }

        private void EnsureRuntimeAimTarget()
        {
            if (_runtimeAimTarget != null || MyCore == null)
                return;

            var go = new GameObject("__EntityAIAimTarget");
            go.hideFlags = HideFlags.HideAndDontSave;
            _runtimeAimTarget = go.transform;
        }

        private static Vector3 GetMoveWorld(Vector2 move, Transform cam)
        {
            Vector3 moveWorld = new Vector3(move.x, 0f, move.y);
            if (cam == null) return moveWorld;

            Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            if (right.sqrMagnitude <= 0.0001f)
            {
                right = Vector3.right;
            }
            return forward * move.y + right * move.x;
        }

        private Vector3 GetMoveWorld(Vector2 move, Transform cam, Vector3 fallbackForward)
        {
            Vector3 moveWorld = new Vector3(move.x, 0f, move.y);
            if (cam == null)
            {
                if (fallbackForward.sqrMagnitude > 0.0001f)
                {
                    Vector3 fallbackPlanarForward = Vector3.ProjectOnPlane(fallbackForward, Vector3.up).normalized;
                    Vector3 fallbackRight = Vector3.Cross(Vector3.up, fallbackPlanarForward).normalized;
                    return fallbackPlanarForward * move.y + fallbackRight * move.x;
                }
                return moveWorld;
            }
            Vector3 camForwardPlanar = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (camForwardPlanar.sqrMagnitude <= 0.0001f)
            {
                camForwardPlanar = Vector3.ProjectOnPlane(fallbackForward, Vector3.up);
            }

            Vector3 forward = camForwardPlanar.sqrMagnitude > 0.0001f ? camForwardPlanar.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            return forward * move.y + right * move.x;
        }

        private Vector3 GetLookWorld(Vector2 look, Transform cam, Vector3 moveWorld, TurnMode mode)
        {
            Vector3 cameraForward = Vector3.zero;
            if (cam != null)
            {
                cameraForward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            }

            switch (mode)
            {
                case TurnMode.AimToCamera:
                    if (cameraForward.sqrMagnitude > 0.0001f) return cameraForward;
                    break;

                case TurnMode.MoveDirection:
                    if (moveWorld.sqrMagnitude > 0.0001f) return moveWorld.normalized;
                    if (cameraForward.sqrMagnitude > 0.0001f) return cameraForward;
                    break;

                case TurnMode.FreeLook:
                    return GetFreeLookDirection(look, cameraForward);
            }

            return _lastLookWorld.sqrMagnitude > 0.0001f ? _lastLookWorld : Vector3.forward;
        }

        private Vector3 GetFreeLookDirection(Vector2 look, Vector3 cameraForward)
        {
            Vector3 baseForward = cameraForward.sqrMagnitude > 0.0001f ? cameraForward : (_lastLookWorld.sqrMagnitude > 0.0001f ? _lastLookWorld : Vector3.forward);

            if (!_freeLookInited)
            {
                _freeLookYaw = Mathf.Atan2(baseForward.x, baseForward.z) * Mathf.Rad2Deg;
                _freeLookInited = true;
            }

            if (look.sqrMagnitude > 0.0001f)
            {
                _freeLookYaw += look.x * turnSpeed * Time.deltaTime;
            }

            Quaternion rot = Quaternion.AngleAxis(_freeLookYaw, Vector3.up);
            return (rot * Vector3.forward).normalized;
        }

        private static Vector3 SmoothLook(Vector3 current, Vector3 target, float smooth)
        {
            if (smooth <= 0f || target.sqrMagnitude <= 0.0001f) return target;
            float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            return Vector3.Slerp(current, target, t).normalized;
        }

        private static Vector3 SmoothMove(Vector3 current, Vector3 target, float smooth)
        {
            if (smooth <= 0f) return target;
            float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            return Vector3.Lerp(current, target, t);
        }

        private void ApplyCameraLook(Vector2 lookInput, float scale, Transform camTransform, bool eyeHold)
        {
            if (aimTransform != null && MyCore != null)
            {
                ApplyAimLook(lookInput, scale, eyeHold);
                return;
            }

            if (driveCinemachineAxes && TryDriveCinemachine(lookInput, scale, camTransform))
            {
                return;
            }

            Transform pivot = cameraPivot != null ? cameraPivot : cameraTransform;
            if (pivot == null) return;

            if (!_cameraAnglesInited)
            {
                Vector3 euler = pivot.rotation.eulerAngles;
                _cameraYaw = euler.y;
                _cameraPitch = NormalizePitch(euler.x);
                _cameraYawCurrent = _cameraYaw;
                _cameraPitchCurrent = _cameraPitch;
                _cameraAnglesInited = true;
            }

            if (lookInput.sqrMagnitude > 0.0001f)
            {
                _cameraYaw += lookInput.x * cameraYawSpeed * yawMultiplier * scale * Time.deltaTime;
                float pitchDelta = -lookInput.y * cameraPitchSpeed * pitchMultiplier * scale * Time.deltaTime;
                _cameraPitch = ApplySoftPitch(_cameraPitch, pitchDelta, cameraPitchLimit, cameraPitchSoftZone, cameraPitchCorrectionSpeed);
            }

            float t = cameraLookSmooth <= 0f ? 1f : (1f - Mathf.Exp(-cameraLookSmooth * Time.deltaTime));
            _cameraYawCurrent = Mathf.LerpAngle(_cameraYawCurrent, _cameraYaw, t);
            _cameraPitchCurrent = Mathf.Lerp(_cameraPitchCurrent, _cameraPitch, t);

            pivot.rotation = Quaternion.Euler(_cameraPitchCurrent, _cameraYawCurrent, 0f);
        }

        private void ApplyAimLook(Vector2 lookInput, float scale, bool eyeHold)
        {
            if (aimTransform == null || MyCore == null) return;

            if (!_aimAnglesInited)
            {
                _aimYaw = MyCore.transform.rotation.eulerAngles.y;
                _aimPitch = NormalizePitch(aimTransform.localRotation.eulerAngles.x);
                _aimYawCurrent = _aimYaw;
                _aimPitchCurrent = _aimPitch;
                _aimAnglesInited = true;
            }

            if (lookInput.sqrMagnitude > 0.0001f)
            {
                float yawDelta = lookInput.x * cameraYawSpeed * yawMultiplier * scale * Time.deltaTime;
                float pitchDelta = -lookInput.y * cameraPitchSpeed * pitchMultiplier * scale * Time.deltaTime;

                if (eyeHold)
                {
                    _eyeYawOffset += yawDelta;
                    _eyePitchOffset += pitchDelta;
                }
                else
                {
                    _aimYaw += yawDelta;
                    _aimPitch = ApplySoftPitch(_aimPitch, pitchDelta, cameraPitchLimit, cameraPitchSoftZone, cameraPitchCorrectionSpeed);
                }
            }

            if (eyeHold)
            {
                ClampEyeOffsetToLimits();
            }
            else
            {
                float tReturn = 1f - Mathf.Exp(-eyeReturnSpeed * Time.deltaTime);
                _eyeYawOffset = Mathf.LerpAngle(_eyeYawOffset, 0f, tReturn);
                _eyePitchOffset = Mathf.Lerp(_eyePitchOffset, 0f, tReturn);
            }

            float t = cameraLookSmooth <= 0f ? 1f : (1f - Mathf.Exp(-cameraLookSmooth * Time.deltaTime));
            _aimYawCurrent = Mathf.LerpAngle(_aimYawCurrent, _aimYaw, t);
            _aimPitchCurrent = Mathf.Lerp(_aimPitchCurrent, _aimPitch, t);

            if (!eyeHold)
            {
                MyCore.transform.rotation = Quaternion.Euler(0f, _aimYawCurrent, 0f);
            }

            aimTransform.localRotation = Quaternion.Euler(_aimPitchCurrent + _eyePitchOffset, _eyeYawOffset, 0f);

            if (debugCamera)
            {
                Debug.Log($"[EntityAIInputDispatch] AimPitch={_aimPitchCurrent:F2}, EyePitch={_eyePitchOffset:F2}, TotalPitch={_aimPitchCurrent + _eyePitchOffset:F2}");
            }
        }

        private static float NormalizePitch(float pitch)
        {
            pitch %= 360f;
            if (pitch > 180f) pitch -= 360f;
            return pitch;
        }

        private static float ApplySoftPitch(float current, float delta, Vector2 limit, float softZone, float correctionSpeed)
        {
            float min = limit.x;
            float max = limit.y;
            float target = current;

            if (delta > 0f && current > max - softZone)
            {
                float t = Mathf.Clamp01((max - current) / Mathf.Max(softZone, 0.001f));
                delta *= t;
            }
            else if (delta < 0f && current < min + softZone)
            {
                float t = Mathf.Clamp01((current - min) / Mathf.Max(softZone, 0.001f));
                delta *= t;
            }

            target += delta;
            if (target > max)
            {
                float corrected = Mathf.Lerp(target, max, 1f - Mathf.Exp(-correctionSpeed * Time.deltaTime));
                target = corrected;
            }
            if (target < min)
            {
                float corrected = Mathf.Lerp(target, min, 1f - Mathf.Exp(-correctionSpeed * Time.deltaTime));
                target = corrected;
            }
            return target;
        }

        private void ClampEyeOffsetToLimits()
        {
            float min = cameraPitchLimit.x;
            float max = cameraPitchLimit.y;
            float total = _aimPitchCurrent + _eyePitchOffset;
            if (total > max)
            {
                float target = max - _aimPitchCurrent;
                _eyePitchOffset = Mathf.SmoothDamp(_eyePitchOffset, target, ref _eyePitchVel, 0.5f / Mathf.Max(cameraPitchCorrectionSpeed, 0.01f));
            }
            else if (total < min)
            {
                float target = min - _aimPitchCurrent;
                _eyePitchOffset = Mathf.SmoothDamp(_eyePitchOffset, target, ref _eyePitchVel, 0.5f / Mathf.Max(cameraPitchCorrectionSpeed, 0.01f));
            }

        }

        private bool TryDriveCinemachine(Vector2 lookInput, float scale, Transform camTransform)
        {
#if CINEMACHINE
            if (camTransform == null) return false;

            var vcam = camTransform.GetComponent<Cinemachine.CinemachineVirtualCameraBase>();
            if (vcam == null) return false;

            if (vcam is Cinemachine.CinemachineFreeLook freeLook)
            {
                freeLook.m_XAxis.Value += lookInput.x * cameraYawSpeed * scale * Time.deltaTime;
                freeLook.m_YAxis.Value -= lookInput.y * cameraPitchSpeed * scale * Time.deltaTime;
                freeLook.m_YAxis.Value = Mathf.Clamp(freeLook.m_YAxis.Value, freeLook.m_YAxis.m_MinValue, freeLook.m_YAxis.m_MaxValue);

                if (debugCamera) Debug.Log("[EntityAIInputDispatch] Drive Cinemachine FreeLook axes");
                return true;
            }

            var pov = vcam.GetCinemachineComponent<Cinemachine.CinemachinePOV>();
            if (pov != null)
            {
                pov.m_HorizontalAxis.Value += lookInput.x * cameraYawSpeed * scale * Time.deltaTime;
                pov.m_VerticalAxis.Value -= lookInput.y * cameraPitchSpeed * scale * Time.deltaTime;
                pov.m_VerticalAxis.Value = Mathf.Clamp(pov.m_VerticalAxis.Value, cameraPitchLimit.x, cameraPitchLimit.y);

                if (debugCamera) Debug.Log("[EntityAIInputDispatch] Drive Cinemachine POV axes");
                return true;
            }

            if (debugCamera) Debug.LogWarning("[EntityAIInputDispatch] Cinemachine camera found, but no POV/FreeLook to drive.");
            return false;
#else
            return false;
#endif
        }

        private void UpdateMoveStats(Vector3 moveWorld)
        {
            float speed = moveWorld.magnitude;
            if (speed > 0.001f)
            {
                _totalMoveTime += Time.deltaTime;
            }
            totalMoveTime = _totalMoveTime;

            float now = Time.time;
            _speedSamples.Enqueue(new SpeedSample(now, speed));
            _last1sSpeedSum += speed;

            while (_speedSamples.Count > 0 && now - _speedSamples.Peek().time > 1f)
            {
                var s = _speedSamples.Dequeue();
                _last1sSpeedSum -= s.speed;
            }

            avgSpeedLast1s = _speedSamples.Count > 0 ? _last1sSpeedSum / _speedSamples.Count : 0f;
        }

        private Transform ResolveCameraTransform()
        {
            if (cameraTransform != null)
            {
                LogCameraDetail("Direct", cameraTransform);
                return cameraTransform;
            }

            if (mainCamera != null)
            {
                LogCameraSource(CameraSource.MainCamera);
                LogCameraDetail("MainCamera", mainCamera.transform);
                return mainCamera.transform;
            }

            if (_cachedMainCamera == null)
            {
                _cachedMainCamera = Camera.main;
            }

            if (_cachedMainCamera != null)
            {
                LogCameraSource(CameraSource.MainCamera);
                LogCameraDetail("MainCamera", _cachedMainCamera.transform);
                return _cachedMainCamera.transform;
            }

            LogCameraSource(CameraSource.None);
            LogCameraNull("MainCameraNull", null);
            return null;
        }

        private void LogCameraSource(CameraSource source)
        {
            if (!debugCamera || _lastCameraSource == source) return;
            _lastCameraSource = source;
            Debug.Log($"[EntityAIInputDispatch] CameraSource={source}");
        }

        private void LogCameraDetail(string stage, Transform t)
        {
            if (!debugCamera) return;
            if (t == null)
            {
                Debug.LogWarning($"[EntityAIInputDispatch] Camera {stage} is null");
                return;
            }
            Debug.Log($"[EntityAIInputDispatch] Camera {stage}: name={t.name}, pos={t.position}, fwd={t.forward}");
        }

        private void LogCameraNull(string reason, EntityBasicCameraModule module)
        {
            if (!debugCamera) return;
            string moduleName = module != null ? module.GetType().Name : "null";
            Debug.LogWarning($"[EntityAIInputDispatch] Camera null reason={reason}, module={moduleName}");
        }

        public override void OnDestroy()
        {
            if (_runtimeAimTarget != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_runtimeAimTarget.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_runtimeAimTarget.gameObject);

                _runtimeAimTarget = null;
            }

            base.OnDestroy();
        }
    }

    public enum TurnMode
    {
        AimToCamera,
        MoveDirection,
        FreeLook
    }

    public enum CameraSource
    {
        None,
        Module,
        MainCamera
    }

    [Serializable]
    public struct EntityMotionInputState
    {
        [LabelText("帧")]
        public int frameIndex;

        [LabelText("移动")]
        public Vector2 move;

        [LabelText("视角")]
        public Vector2 look;

        [LabelText("飞行垂直")]
        public float flyVertical;

        [LabelText("小眼睛(按住)")]
        public bool eyeHold;

        [LabelText("左探头(按住)")]
        public bool peekLeftHold;

        [LabelText("右探头(按住)")]
        public bool peekRightHold;

        public float AimPeek => peekLeftHold == peekRightHold ? 0f : (peekRightHold ? 1f : -1f);

        public void Clear()
        {
            frameIndex = 0;
            move = Vector2.zero;
            look = Vector2.zero;
            flyVertical = 0f;
            eyeHold = false;
            peekLeftHold = false;
            peekRightHold = false;
        }
    }

    [Serializable]
    public struct EntityActionInputPulse
    {
        [LabelText("帧")]
        public int frameIndex;

        [LabelText("攻击")]
        public bool attack;

        [LabelText("重击")]
        public bool heavyAttack;

        [LabelText("格挡")]
        public bool block;

        [LabelText("滑行")]
        public bool slide;

        [LabelText("切换武器")]
        public bool switchWeapon;

        [LabelText("拿枪")]
        public bool equipWeapon;

        [LabelText("收枪")]
        public bool holsterWeapon;

        [LabelText("切到武器槽1")]
        public bool weaponSlot1;

        [LabelText("切到武器槽2")]
        public bool weaponSlot2;

        [LabelText("切到武器槽3")]
        public bool weaponSlot3;

        [LabelText("切到武器槽4")]
        public bool weaponSlot4;

        [LabelText("切到武器槽5")]
        public bool weaponSlot5;

        [LabelText("瞄准")]
        public bool aim;

        [LabelText("技能1")]
        public bool skill1;

        [LabelText("技能2")]
        public bool skill2;

        [LabelText("技能3")]
        public bool skill3;

        [LabelText("跳跃")]
        public bool jump;

        [LabelText("下蹲(切换指令)")]
        public bool crouchToggle;

        [LabelText("飞行(切换指令)")]
        public bool flyToggle;

        [LabelText("骑乘(切换指令)")]
        public bool mountToggle;

        [LabelText("攀爬(切换指令)")]
        public bool climbToggle;

        [LabelText("交互")]
        public bool interact;

        public void Clear()
        {
            this = default;
        }

        public bool ConsumeAttack() => Consume(ref attack);
        public bool ConsumeHeavyAttack() => Consume(ref heavyAttack);
        public bool ConsumeBlock() => Consume(ref block);
        public bool ConsumeSlide() => Consume(ref slide);
        public bool ConsumeSwitchWeapon() => Consume(ref switchWeapon);
        public bool ConsumeEquipWeapon() => Consume(ref equipWeapon);
        public bool ConsumeHolsterWeapon() => Consume(ref holsterWeapon);
        public bool ConsumeWeaponSlot1() => Consume(ref weaponSlot1);
        public bool ConsumeWeaponSlot2() => Consume(ref weaponSlot2);
        public bool ConsumeWeaponSlot3() => Consume(ref weaponSlot3);
        public bool ConsumeWeaponSlot4() => Consume(ref weaponSlot4);
        public bool ConsumeWeaponSlot5() => Consume(ref weaponSlot5);
        public bool ConsumeAim() => Consume(ref aim);
        public bool ConsumeSkill1() => Consume(ref skill1);
        public bool ConsumeSkill2() => Consume(ref skill2);
        public bool ConsumeSkill3() => Consume(ref skill3);
        public bool ConsumeJump() => Consume(ref jump);
        public bool ConsumeCrouchToggle() => Consume(ref crouchToggle);
        public bool ConsumeFlyToggle() => Consume(ref flyToggle);
        public bool ConsumeMountToggle() => Consume(ref mountToggle);
        public bool ConsumeClimbToggle() => Consume(ref climbToggle);
        public bool ConsumeInteract() => Consume(ref interact);

        public void PulseAttack(int frame) => Pulse(ref attack, frame);
        public void PulseHeavyAttack(int frame) => Pulse(ref heavyAttack, frame);
        public void PulseBlock(int frame) => Pulse(ref block, frame);
        public void PulseSlide(int frame) => Pulse(ref slide, frame);
        public void PulseSwitchWeapon(int frame) => Pulse(ref switchWeapon, frame);
        public void PulseEquipWeapon(int frame) => Pulse(ref equipWeapon, frame);
        public void PulseHolsterWeapon(int frame) => Pulse(ref holsterWeapon, frame);
        public void PulseWeaponSlot1(int frame) => Pulse(ref weaponSlot1, frame);
        public void PulseWeaponSlot2(int frame) => Pulse(ref weaponSlot2, frame);
        public void PulseWeaponSlot3(int frame) => Pulse(ref weaponSlot3, frame);
        public void PulseWeaponSlot4(int frame) => Pulse(ref weaponSlot4, frame);
        public void PulseWeaponSlot5(int frame) => Pulse(ref weaponSlot5, frame);
        public void PulseAim(int frame) => Pulse(ref aim, frame);
        public void PulseSkill1(int frame) => Pulse(ref skill1, frame);
        public void PulseSkill2(int frame) => Pulse(ref skill2, frame);
        public void PulseSkill3(int frame) => Pulse(ref skill3, frame);
        public void PulseJump(int frame) => Pulse(ref jump, frame);
        public void PulseCrouchToggle(int frame) => Pulse(ref crouchToggle, frame);
        public void PulseFlyToggle(int frame) => Pulse(ref flyToggle, frame);
        public void PulseMountToggle(int frame) => Pulse(ref mountToggle, frame);
        public void PulseClimbToggle(int frame) => Pulse(ref climbToggle, frame);
        public void PulseInteract(int frame) => Pulse(ref interact, frame);

        private static bool Consume(ref bool value)
        {
            if (!value) return false;
            value = false;
            return true;
        }

        private void Pulse(ref bool value, int frame)
        {
            value = true;
            frameIndex = frame;
        }
    }

    internal readonly struct SpeedSample
    {
        public readonly float time;
        public readonly float speed;

        public SpeedSample(float time, float speed)
        {
            this.time = time;
            this.speed = speed;
        }
    }

}
