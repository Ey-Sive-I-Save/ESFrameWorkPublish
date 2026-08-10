using System;
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
        [Title("输出状态")]
        [ShowInInspector, ReadOnly]
        public EntityMotionInputState motion;

        [ShowInInspector, ReadOnly]
        public EntityActionInputPulse action;

        public void ClearAll()
        {
            motion.Clear();
            action.Clear();
        }
    }

    [Serializable, TypeRegistryItem("Entity玩家输入写入模块")]
    public class EntityPlayerInputWriteModule : EntityAIModuleBase
    {
        [LabelText("启用玩家输入写入")]
        public bool enablePlayerInput = true;

        [LabelText("声明本地控制权")]
        [Tooltip("仅显式声明的实体可绑定到全局输入/UI RuntimeMode 投影。多人或观战实体应由控制权系统调用 ESGameManager.LocalControl.SetControlledEntity。")]
        public bool claimLocalControl;

        [Title("Tag Gate")]
        [LabelText("输入写入 Tag 条件")]
        [Tooltip("为空时不限制。条件不匹配时清空本帧输入，避免被禁用状态残留旧输入。")]
        public ESTagConditionConfig inputTagCondition = new ESTagConditionConfig();

        protected override void Update()
        {
            EntityInputState state = MyDomain.inputState;

            // This writer owns the local player's previous frame. Clear it when it is
            // disabled so the dispatcher cannot keep driving the entity from stale intent.
            if (!enablePlayerInput)
            {
                state.ClearAll();
                return;
            }

            if (inputTagCondition != null
                && !inputTagCondition.IsEmpty
                && (MyCore == null || !MyCore.Tags.Matches(inputTagCondition)))
            {
                state.ClearAll();
                return;
            }

            ESInputModule input = ESGameManager.InputModule;
            if (input == null)
            {
                state.ClearAll();
                return;
            }

            ESLocalControlService localControl = ESGameManager.LocalControl;
            if (localControl == null)
            {
                state.ClearAll();
                return;
            }

            if (claimLocalControl && !localControl.TryClaim(MyCore, input.ModeService))
            {
                state.ClearAll();
                return;
            }

            // claimLocalControl only controls whether this prefab declares control ownership during
            // initialization. It never grants permission to read the global input stream.
            if (!MyDomain.CanPlayerWriteInput())
            {
                state.ClearAll();
                return;
            }

            state.motion.move = input.ReadVector2(ESInputActionId.Move);
            state.motion.look = input.ReadVector2(ESInputActionId.Look);
            state.motion.flyVertical = input.ReadAxis(ESInputActionId.FlyVertical);
            state.motion.blockHold = input.IsHeld(ESInputActionId.Block);
            state.motion.peekLeftHold = input.IsHeld(ESInputActionId.PeekLeft);
            state.motion.peekRightHold = input.IsHeld(ESInputActionId.PeekRight);

            if (input.ConsumePressed(ESInputActionId.Attack)) state.action.PulseAttack();
            if (input.ConsumePressed(ESInputActionId.HeavyAttack)) state.action.PulseHeavyAttack();
            // Block is a hold, not a one-frame toggle. Consume the press so it remains
            // exclusively owned by the local player writer; the dispatcher reads blockHold.
            input.ConsumePressed(ESInputActionId.Block);
            if (input.ConsumePressed(ESInputActionId.Slide)) state.action.PulseSlide();
            if (input.ConsumePressed(ESInputActionId.SwitchWeapon)) state.action.PulseSwitchWeapon();
            if (input.ConsumePressed(ESInputActionId.EquipWeapon)) state.action.PulseEquipWeapon();
            if (input.ConsumePressed(ESInputActionId.HolsterWeapon)) state.action.PulseHolsterWeapon();
            if (input.ConsumePressed(ESInputActionId.WeaponSlot1)) state.action.PulseWeaponSlot1();
            if (input.ConsumePressed(ESInputActionId.WeaponSlot2)) state.action.PulseWeaponSlot2();
            if (input.ConsumePressed(ESInputActionId.WeaponSlot3)) state.action.PulseWeaponSlot3();
            if (input.ConsumePressed(ESInputActionId.WeaponSlot4)) state.action.PulseWeaponSlot4();
            if (input.ConsumePressed(ESInputActionId.WeaponSlot5)) state.action.PulseWeaponSlot5();
            if (input.ConsumePressed(ESInputActionId.Aim)) state.action.PulseAim();
            if (input.ConsumePressed(ESInputActionId.Skill1)) state.action.PulseSkill1();
            if (input.ConsumePressed(ESInputActionId.Skill2)) state.action.PulseSkill2();
            if (input.ConsumePressed(ESInputActionId.Skill3)) state.action.PulseSkill3();
            if (input.ConsumePressed(ESInputActionId.Jump)) state.action.PulseJump();
            if (input.ConsumePressed(ESInputActionId.Crouch)) state.action.PulseCrouchToggle();
            if (input.ConsumePressed(ESInputActionId.Fly)) state.action.PulseFlyToggle();
            if (input.ConsumePressed(ESInputActionId.Mount)) state.action.PulseMountToggle();
            if (input.ConsumePressed(ESInputActionId.Climb)) state.action.PulseClimbToggle();
            if (input.ConsumePressed(ESInputActionId.Interact)) state.action.PulseInteract();
        }

        protected override void OnDisable()
        {
            if (claimLocalControl)
                ESGameManager.LocalControl?.Release(MyCore);
            MyDomain?.inputState?.ClearAll();
            base.OnDisable();
        }
    }

    // =================================================================================================
    // AI 域输入执行器
    // - 读取 Entity 输入状态
    // - 驱动 Basic 域的“实际生效模块”
    // =================================================================================================
    public partial class EntityAIDomain
    {
        private Vector3 _lastLookWorld = Vector3.forward;
        private float _freeLookYaw;
        private bool _freeLookInited;
        // Driver is stable for the lifetime of a bound Animator. Cache the positive
        // lookup because combat input dispatch runs every frame.
        [NonSerialized] private StateFinalIKDriver _cachedIKDriver;
        [NonSerialized] private Animator _cachedIKDriverAnimator;

        private float _aimYaw;
        private float _aimPitch;
        private float _aimYawCurrent;
        private float _aimPitchCurrent;
        private bool _aimAnglesInited;

        private bool _wasClimbing;

        private Transform _runtimeAimTarget;

        private void UpdateInputDispatch()
        {
            if (MyCore == null) return;

            var input = inputState;
            if (IsControlBlocked)
            {
                input.ClearAll();
                ResetCharacterMotionInput();
                ClearMountedDriverInput();
                ClearCombatInputLatches();
                return;
            }
            if (dispatchTagCondition != null
                && !dispatchTagCondition.IsEmpty
                && !MyCore.Tags.Matches(dispatchTagCondition))
            {
                input.ClearAll();
                ResetCharacterMotionInput();
                ClearMountedDriverInput();
                ClearCombatInputLatches();
                return;
            }

            var cam = ResolveCameraTransform();

            bool hasClimbModule = TryGetModule(out EntityBasicClimbModule climbModule);
            bool isClimbing = hasClimbModule && climbModule.subState != ClimbSubState.None;

            bool hasMountModule = TryGetModule(out global::ES.EntityBasicMountModule mountModule);
            if (hasMountModule && input.action.ConsumeMountToggle())
            {
                mountModule.ToggleMount();
            }

            HandleClimbEnter(isClimbing);
            DispatchCameraLook(input);

            // Mounted is an input-routing boundary. The rider may still control a driver
            // seat, but no character action can be dispatched past this point.
            if (hasMountModule && mountModule.IsMounted)
            {
                DispatchMountedControl(input, cam, mountModule);
                input.action.Clear();
                _wasClimbing = false;
                return;
            }

            DispatchGroundMove(input, cam);
            DispatchFly(input);
            DispatchClimb(input, climbModule, hasClimbModule);
            DispatchInteraction(input);
            DispatchCombat(input, cam);
            DispatchSkill(input);

            input.action.Clear();
            _wasClimbing = isClimbing;
        }

        #region 调度流程

        private void HandleClimbEnter(bool isClimbing)
        {
            if (!isClimbing || _wasClimbing)
                return;

            MyCore.SetMoveInput(Vector3.zero);
        }

        private void ResetCharacterMotionInput()
        {
            MyCore?.ResetKCCInputs();
        }

        private void ClearMountedDriverInput()
        {
            if (TryGetModule(out global::ES.EntityBasicMountModule mountModule))
                mountModule.ClearDriverInput();
        }

        private void ClearCombatInputLatches()
        {
            if (!TryGetModule(out EntityBasicCombatModule combatModule))
                return;

            combatModule.SetBlock(false);
            combatModule.SetSlide(false);
        }

        private void DispatchCameraLook(EntityInputState input)
        {
            if (!enableCameraLook)
                return;

            // 只有当前本地控制实体可以把 Look 交给 Camera 模块。普通 AI 仍可保留
            // 自己的瞄准/骨骼意图，但绝不能参与 MainView 的输入或仲裁。
            if (MyCore != null
                && ESGameManager.LocalControl != null
                && ESGameManager.LocalControl.IsLocallyControlled(MyCore))
            {
                MyCore.SubmitCameraLook(input.motion.look);
            }

            // Aim 是角色骨骼/IK 意图，保留在角色域；它不拥有或驱动相机实例。
            if (aimTransform != null)
                ApplyAimLook(input.motion.look);
        }

        private void DispatchGroundMove(EntityInputState input, Transform cam)
        {
            if (!TryGetModule(out EntityBasicMoveRotateModule moveModule))
                return;

            ApplyMoveAndLook(input, cam);

            if (input.action.ConsumeJump())
            {
                moveModule.RequestJump();
            }

            if (input.action.ConsumeCrouchToggle())
                moveModule.ToggleCrouch();

        }

        private void DispatchFly(EntityInputState input)
        {
            if (!TryGetModule(out EntityBasicFlyModule flyModule))
                return;

            if (input.action.ConsumeFlyToggle())
                flyModule.ToggleFly();

            flyModule.SetVerticalInput(input.motion.flyVertical);
        }

        private void DispatchMountedControl(EntityInputState input, Transform cam, global::ES.EntityBasicMountModule mountModule)
        {
            // Keep using the same camera-relative conversion as foot movement, but route
            // the resolved intent only to the current driver seat.
            ApplyMoveAndLook(input, cam);
            MyCore.SetVerticalInput(input.motion.flyVertical);

            EntityMountable mountable = mountModule.currentMount;
            if (mountable != null)
            {
                mountable.SubmitDriverInput(
                    MyCore,
                    MyCore.kcc.moveInput,
                    MyCore.kcc.lookInput,
                    MyCore.kcc.verticalInput);
            }

            ClearCombatInputLatches();
        }

        private void DispatchClimb(EntityInputState input, EntityBasicClimbModule climbModule, bool hasClimbModule)
        {
            if (hasClimbModule && input.action.ConsumeClimbToggle())
                climbModule.ToggleClimb();
        }

        private void DispatchInteraction(EntityInputState input)
        {
            if (TryGetModule(out EntityBasicInteractionModule interactionModule) && input.action.ConsumeInteract())
                interactionModule.RequestInteract();
        }

        private void DispatchCombat(EntityInputState input, Transform cam)
        {
            if (!TryGetModule(out EntityBasicCombatModule combatModule))
                return;

            if (input.action.ConsumeAttack()
                && !combatModule.TrySubmitMeleeAttack(out bool meleeActionRegistered)
                && !meleeActionRegistered)
                combatModule.TriggerAttack();
            if (input.action.ConsumeHeavyAttack()
                && !combatModule.TrySubmitHeavyAttack(out bool heavyActionRegistered)
                && !heavyActionRegistered)
                combatModule.TriggerHeavyAttack();
            combatModule.SetBlock(input.motion.blockHold);
            combatModule.SetSlide(input.action.ConsumeSlide());
            DispatchWeaponAction(input, combatModule);

            if (input.action.ConsumeAim())
                combatModule.SetAim(!combatModule.isAiming);

            combatModule.SetAimPeek(input.motion.AimPeek);

            var ikDriver = ResolveIKDriver();
            if (ikDriver != null)
                ApplyCombatAimAndPeek(ikDriver, combatModule, cam);
        }

        private void DispatchWeaponAction(EntityInputState input, EntityBasicCombatModule combatModule)
        {
            if (input.action.ConsumeWeaponSlot1()) { combatModule.SwitchWeaponTo(0); return; }
            if (input.action.ConsumeWeaponSlot2()) { combatModule.SwitchWeaponTo(1); return; }
            if (input.action.ConsumeWeaponSlot3()) { combatModule.SwitchWeaponTo(2); return; }
            if (input.action.ConsumeWeaponSlot4()) { combatModule.SwitchWeaponTo(3); return; }
            if (input.action.ConsumeWeaponSlot5()) { combatModule.SwitchWeaponTo(4); return; }
            if (input.action.ConsumeSwitchWeapon()) { combatModule.SwitchWeaponNext(); return; }
            if (input.action.ConsumeEquipWeapon()) { combatModule.EquipCurrentWeapon(); return; }
            if (input.action.ConsumeHolsterWeapon()) combatModule.HolsterCurrentWeapon();
        }

        private void DispatchSkill(EntityInputState input)
        {
            if (!TryGetModule(out EntityBasicSkillModule skillModule))
                return;

            if (input.action.ConsumeSkill1()) skillModule.TriggerSkill(1);
            if (input.action.ConsumeSkill2()) skillModule.TriggerSkill(2);
            if (input.action.ConsumeSkill3()) skillModule.TriggerSkill(3);
        }

        private void ApplyMoveAndLook(EntityInputState input, Transform cam)
        {
            Vector2 move = input.motion.move;
            Vector3 moveWorld = GetMoveWorld(move, cam, _lastLookWorld);
            if (stopMoveWhenNoInput && move.sqrMagnitude <= moveDeadZone * moveDeadZone)
            {
                moveWorld = Vector3.zero;
            }

            // AI Domain 只负责把相机相对输入解析成世界意图；KCC 是唯一的运动响应层。
            // 这样起步、松手和反向不会经过两套串联低通，响应由 KCC 的速度模型统一决定。
            MyCore.SetMoveInput(moveWorld);

            // 相机 Look 只通过 SubmitCameraLook 驱动镜头。角色本体朝向严格由 turnMode
            // 解析，避免拥有 AimTransform 的第三人称角色在转动视角时被强制转身。
            Vector3 targetLook = GetLookWorld(input.motion.look, cam, moveWorld, turnMode);
            // KCC 是唯一的角色转身响应层；Domain 不再对朝向做第二次 Slerp。
            _lastLookWorld = targetLook;
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
            if (stateAnimator == null)
            {
                _cachedIKDriver = null;
                _cachedIKDriverAnimator = null;
                return null;
            }

            if (_cachedIKDriver != null && _cachedIKDriverAnimator == stateAnimator)
                return _cachedIKDriver;

            _cachedIKDriverAnimator = stateAnimator;
            _cachedIKDriver = stateAnimator.GetComponent<StateFinalIKDriver>();
            return _cachedIKDriver;
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

        private void ApplyAimLook(Vector2 lookInput)
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
                float yawDelta = lookInput.x * cameraYawSpeed * yawMultiplier * Time.deltaTime;
                float pitchDelta = -lookInput.y * cameraPitchSpeed * pitchMultiplier * Time.deltaTime;

                _aimYaw += yawDelta;
                _aimPitch = ApplySoftPitch(_aimPitch, pitchDelta, cameraPitchLimit, cameraPitchSoftZone, cameraPitchCorrectionSpeed);
            }

            float t = cameraLookSmooth <= 0f ? 1f : (1f - Mathf.Exp(-cameraLookSmooth * Time.deltaTime));
            _aimYawCurrent = Mathf.LerpAngle(_aimYawCurrent, _aimYaw, t);
            _aimPitchCurrent = Mathf.Lerp(_aimPitchCurrent, _aimPitch, t);

            aimTransform.localRotation = Quaternion.Euler(_aimPitchCurrent, 0f, 0f);

            if (debugCamera)
            {
                Debug.Log($"[EntityAIDomain] AimPitch={_aimPitchCurrent:F2}");
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

        private Transform ResolveCameraTransform()
        {
            ESCameraModule camera = ESGameManager.Camera;
            if (camera != null && camera.TryGetOutputTransform(ESCameraViewId.Main, out Transform output))
            {
                LogCameraDetail("DirectorOutput", output);
                return output;
            }

            LogCameraNull("DirectorOutputUnavailable");
            return null;
        }

        private void LogCameraDetail(string stage, Transform t)
        {
            if (!debugCamera) return;
            if (t == null)
            {
                Debug.LogWarning($"[EntityAIDomain] Camera {stage} is null");
                return;
            }
            Debug.Log($"[EntityAIDomain] Camera {stage}: name={t.name}, pos={t.position}, fwd={t.forward}");
        }

        private void LogCameraNull(string reason)
        {
            if (!debugCamera) return;
            Debug.LogWarning($"[EntityAIDomain] Camera unavailable: {reason}");
        }

        private void ResetInputDispatchForDisable()
        {
            MyCore?.ResetKCCInputs();
        }

        private void DestroyInputDispatchRuntime()
        {
            _cachedIKDriver = null;
            _cachedIKDriverAnimator = null;
            if (_runtimeAimTarget != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(_runtimeAimTarget.gameObject);
                else
                    UnityEngine.Object.DestroyImmediate(_runtimeAimTarget.gameObject);

                _runtimeAimTarget = null;
            }
        }
    }

    public enum TurnMode
    {
        AimToCamera,
        MoveDirection,
        FreeLook
    }

    [Serializable]
    public struct EntityMotionInputState
    {
        [LabelText("移动")]
        public Vector2 move;

        [LabelText("视角")]
        public Vector2 look;

        [LabelText("飞行垂直")]
        public float flyVertical;

        [LabelText("格挡(按住)")]
        public bool blockHold;

        [LabelText("左探头(按住)")]
        public bool peekLeftHold;

        [LabelText("右探头(按住)")]
        public bool peekRightHold;

        public float AimPeek => peekLeftHold == peekRightHold ? 0f : (peekRightHold ? 1f : -1f);

        public void Clear()
        {
            this = default;
        }
    }

    [Serializable]
    public struct EntityActionInputPulse
    {
        [LabelText("攻击")]
        public bool attack;

        [LabelText("重击")]
        public bool heavyAttack;

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

        public void PulseAttack() => Pulse(ref attack);
        public void PulseHeavyAttack() => Pulse(ref heavyAttack);
        public void PulseSlide() => Pulse(ref slide);
        public void PulseSwitchWeapon() => Pulse(ref switchWeapon);
        public void PulseEquipWeapon() => Pulse(ref equipWeapon);
        public void PulseHolsterWeapon() => Pulse(ref holsterWeapon);
        public void PulseWeaponSlot1() => Pulse(ref weaponSlot1);
        public void PulseWeaponSlot2() => Pulse(ref weaponSlot2);
        public void PulseWeaponSlot3() => Pulse(ref weaponSlot3);
        public void PulseWeaponSlot4() => Pulse(ref weaponSlot4);
        public void PulseWeaponSlot5() => Pulse(ref weaponSlot5);
        public void PulseAim() => Pulse(ref aim);
        public void PulseSkill1() => Pulse(ref skill1);
        public void PulseSkill2() => Pulse(ref skill2);
        public void PulseSkill3() => Pulse(ref skill3);
        public void PulseJump() => Pulse(ref jump);
        public void PulseCrouchToggle() => Pulse(ref crouchToggle);
        public void PulseFlyToggle() => Pulse(ref flyToggle);
        public void PulseMountToggle() => Pulse(ref mountToggle);
        public void PulseClimbToggle() => Pulse(ref climbToggle);
        public void PulseInteract() => Pulse(ref interact);

        private static bool Consume(ref bool value)
        {
            if (!value) return false;
            value = false;
            return true;
        }

        private static void Pulse(ref bool value)
        {
            value = true;
        }
    }

}
