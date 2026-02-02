using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ES
{
    [Serializable, TypeRegistryItem("AI域模块基类")]
    public abstract class EntityAIModuleBase : Module<Entity, EntityAIDomain>
    {
        public sealed override Type TableKeyType => GetType();
    }

    // =================================================================================================
    // InputSystem 输入采集模块（AI 域）
    // - 只采集输入并生成快照
    // - 触发类输入通过 Consume 清空，避免一直为 true
    // =================================================================================================
    [Serializable, TypeRegistryItem("AI输入采集模块")]
    public class EntityAIInputSystemModule : EntityAIModuleBase
    {
        [Title("开关")]
        [LabelText("是否启用输入")]
        public bool enableInput = true;

#if ENABLE_INPUT_SYSTEM
        [Title("输入绑定（支持直接配置或引用）")]
        [LabelText("移动")]
        public InputActionProperty moveAction;

        [LabelText("视角")]
        public InputActionProperty lookAction;

        [LabelText("攻击")]
        public InputActionProperty attackAction;

        [LabelText("重击")]
        public InputActionProperty heavyAttackAction;

        [LabelText("格挡")]
        public InputActionProperty blockAction;

        [LabelText("滑行")]
        public InputActionProperty slideAction;

        [LabelText("切换武器")]
        public InputActionProperty switchWeaponAction;

        [LabelText("瞄准")]
        public InputActionProperty aimAction;

        [LabelText("技能1")]
        public InputActionProperty skill1Action;

        [LabelText("技能2")]
        public InputActionProperty skill2Action;

        [LabelText("技能3")]
        public InputActionProperty skill3Action;

        [LabelText("跳跃")]
        public InputActionProperty jumpAction;

        [LabelText("下蹲")]
        public InputActionProperty crouchAction;

        [LabelText("小眼睛(视角附加)")]
        public InputActionProperty eyeAction;

        [Title("快速初始化")]
        [LabelText("快捷初始化预设")]
        public InputQuickPreset quickPreset = InputQuickPreset.Default;

        [LabelText("按键方案")]
        public InputSchemeKey schemeKey = InputSchemeKey.Default;

        [LabelText("单项内置键")]
        public InputActionKey singleKey = InputActionKey.Move;

        [Button("一键清空全部输入"), GUIColor(0.6f, 0.6f, 0.6f)]
        public void InitEmpty()
        {
            moveAction = default;
            lookAction = default;
            attackAction = default;
            heavyAttackAction = default;
            blockAction = default;
            slideAction = default;
            switchWeaponAction = default;
            aimAction = default;
            skill1Action = default;
            skill2Action = default;
            skill3Action = default;
            jumpAction = default;
            crouchAction = default;
        }

        [Button("一键内置默认输入"), GUIColor(0.3f, 0.8f, 0.3f)]
        public void InitBuiltin()
        {
            EntityInputQuickInit.ApplyPreset(this, quickPreset, schemeKey);
        }

        [Button("内置单项输入"), GUIColor(0.2f, 0.6f, 0.9f)]
        public void InitSingle()
        {
            EntityInputQuickInit.ApplySingle(this, singleKey, schemeKey);
        }
#else
        [InfoBox("未启用 Unity Input System。请在 Project Settings > Player > Active Input Handling 中启用。")]
        [LabelText("占位")]
        public bool inputSystemNotEnabled;
#endif

        [Title("输出状态")]
        [ShowInInspector, ReadOnly]
        public InputSnapshot snapshot;

        public Vector2 Move => snapshot.move;
        public Vector2 Look => snapshot.look;

        public bool ConsumeAttack() => snapshot.ConsumeAttack();
        public bool ConsumeHeavyAttack() => snapshot.ConsumeHeavyAttack();
        public bool ConsumeBlock() => snapshot.ConsumeBlock();
        public bool ConsumeSlide() => snapshot.ConsumeSlide();
        public bool ConsumeSwitchWeapon() => snapshot.ConsumeSwitchWeapon();
        public bool ConsumeAim() => snapshot.ConsumeAim();
        public bool ConsumeSkill1() => snapshot.ConsumeSkill1();
        public bool ConsumeSkill2() => snapshot.ConsumeSkill2();
        public bool ConsumeSkill3() => snapshot.ConsumeSkill3();
        public bool ConsumeJump() => snapshot.ConsumeJump();
        public void ClearOneShot() => snapshot.ClearOneShot();
        public bool CrouchHold => snapshot.crouchHold;
        public bool EyeHold => snapshot.eyeHold;

#if ENABLE_INPUT_SYSTEM
        private InputAction _move;
        private InputAction _look;
        private InputAction _attack;
        private InputAction _heavyAttack;
        private InputAction _block;
        private InputAction _slide;
        private InputAction _switchWeapon;
        private InputAction _aim;
        private InputAction _skill1;
        private InputAction _skill2;
        private InputAction _skill3;
        private InputAction _jump;
        private InputAction _crouch;
        private InputAction _eye;
#endif

        protected override void OnEnable()
        {
            base.OnEnable();
            BindActions();
        }

        protected override void OnDisable()
        {
            UnbindActions();
            base.OnDisable();
        }

        protected override void Update()
        {
            if (!enableInput) return;
            snapshot.frameIndex = Time.frameCount;
        }

        private void BindActions()
        {
#if ENABLE_INPUT_SYSTEM
            _move = moveAction.action;
            _look = lookAction.action;
            _attack = attackAction.action;
            _heavyAttack = heavyAttackAction.action;
            _block = blockAction.action;
            _slide = slideAction.action;
            _switchWeapon = switchWeaponAction.action;
            _aim = aimAction.action;
            _skill1 = skill1Action.action;
            _skill2 = skill2Action.action;
            _skill3 = skill3Action.action;
            _jump = jumpAction.action;
            _crouch = crouchAction.action;
            _eye = eyeAction.action;

            RegisterAxis(_move, OnMove);
            RegisterAxis(_look, OnLook);

            RegisterButton(_attack, OnAttack);
            RegisterButton(_heavyAttack, OnHeavyAttack);
            RegisterButton(_block, OnBlock);
            RegisterButton(_slide, OnSlide);
            RegisterButton(_switchWeapon, OnSwitchWeapon);
            RegisterButton(_aim, OnAim);
            RegisterButton(_skill1, OnSkill1);
            RegisterButton(_skill2, OnSkill2);
            RegisterButton(_skill3, OnSkill3);
            RegisterButton(_jump, OnJump);
            RegisterAxis(_crouch, OnCrouch);
            RegisterAxis(_eye, OnEye);
#endif
        }

        private void UnbindActions()
        {
#if ENABLE_INPUT_SYSTEM
            UnregisterAxis(_move, OnMove);
            UnregisterAxis(_look, OnLook);

            UnregisterButton(_attack, OnAttack);
            UnregisterButton(_heavyAttack, OnHeavyAttack);
            UnregisterButton(_block, OnBlock);
            UnregisterButton(_slide, OnSlide);
            UnregisterButton(_switchWeapon, OnSwitchWeapon);
            UnregisterButton(_aim, OnAim);
            UnregisterButton(_skill1, OnSkill1);
            UnregisterButton(_skill2, OnSkill2);
            UnregisterButton(_skill3, OnSkill3);
            UnregisterButton(_jump, OnJump);
            UnregisterAxis(_crouch, OnCrouch);
            UnregisterAxis(_eye, OnEye);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static void RegisterAxis(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null) return;
            action.performed += callback;
            action.canceled += callback;
            if (!action.enabled) action.Enable();
        }

        private static void RegisterButton(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null) return;
            action.performed += callback;
            if (!action.enabled) action.Enable();
        }

        private static void UnregisterAxis(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null) return;
            action.performed -= callback;
            action.canceled -= callback;
        }

        private static void UnregisterButton(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            if (action == null) return;
            action.performed -= callback;
        }

        private void OnMove(InputAction.CallbackContext ctx)
        {
            snapshot.move = ctx.ReadValue<Vector2>();
        }

        private void OnLook(InputAction.CallbackContext ctx)
        {
            snapshot.look = ctx.ReadValue<Vector2>();
        }

        private void OnAttack(InputAction.CallbackContext ctx)
        {
            snapshot.attack = true;
        }

        private void OnHeavyAttack(InputAction.CallbackContext ctx)
        {
            snapshot.heavyAttack = true;
        }

        private void OnBlock(InputAction.CallbackContext ctx)
        {
            snapshot.block = true;
        }

        private void OnSlide(InputAction.CallbackContext ctx)
        {
            snapshot.slide = true;
        }

        private void OnSwitchWeapon(InputAction.CallbackContext ctx)
        {
            snapshot.switchWeapon = true;
        }

        private void OnAim(InputAction.CallbackContext ctx)
        {
            snapshot.aim = true;
        }

        private void OnSkill1(InputAction.CallbackContext ctx)
        {
            snapshot.skill1 = true;
        }

        private void OnSkill2(InputAction.CallbackContext ctx)
        {
            snapshot.skill2 = true;
        }

        private void OnSkill3(InputAction.CallbackContext ctx)
        {
            snapshot.skill3 = true;
        }

        private void OnJump(InputAction.CallbackContext ctx)
        {
            snapshot.jump = true;
        }

        private void OnCrouch(InputAction.CallbackContext ctx)
        {
            snapshot.crouchHold = ctx.ReadValue<float>() > 0.5f;
        }

        private void OnEye(InputAction.CallbackContext ctx)
        {
            snapshot.eyeHold = ctx.ReadValue<float>() > 0.5f;
        }
#endif
    }

    // =================================================================================================
    // 输入调度模块（AI 域）
    // - 读取 InputSystem 快照
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

        [Title("相机控制")]
        [LabelText("启用相机上下视角")]
        public bool enableCameraLook = true;

        [LabelText("驱动Cinemachine轴")]
        public bool driveCinemachineAxes = true;

        [LabelText("AIM(可选)")]
        public Transform aimTransform;

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

        private float _totalMoveTime;
        private float _last1sSpeedSum;
        private readonly Queue<SpeedSample> _speedSamples = new Queue<SpeedSample>(64);

        [ShowInInspector, ReadOnly, LabelText("总运动时长")]
        public float totalMoveTime;

        [ShowInInspector, ReadOnly, LabelText("最近1秒平均速率")]
        public float avgSpeedLast1s;

        protected override void Update()
        {
            if (MyCore == null || MyDomain == null) return;

            var input = MyDomain.FindMyModule<EntityAIInputSystemModule>();
            if (input == null) return;

            var cam = ResolveCameraTransform();

            if (enableCameraLook)
            {
                ApplyCameraLook(input.Look, input.EyeHold ? eyeLookScale : 1f, cam, input.EyeHold);
            }

            if (TryGetModule(out EntityBasicMoveRotateModule moveModule))
            {
                Vector3 moveWorld = GetMoveWorld(input.Move, cam, _lastLookWorld) * moveScale;
                _smoothedMoveWorld = SmoothMove(_smoothedMoveWorld, moveWorld, moveSmooth);
                moveModule.SetMoveWorld(_smoothedMoveWorld);

                if (!input.EyeHold)
                {
                    Vector3 targetLook = GetLookWorld(input.Look, cam, _smoothedMoveWorld, turnMode);
                    _lastLookWorld = SmoothLook(_lastLookWorld, targetLook, lookSmooth);
                    moveModule.SetLookWorld(_lastLookWorld);
                }

                if (input.ConsumeJump()) moveModule.RequestJump();
                moveModule.SetCrouch(input.CrouchHold);
            }

            UpdateMoveStats(_smoothedMoveWorld);

            if (TryGetModule(out EntityBasicCombatModule combatModule))
            {
                if (input.ConsumeAttack()) combatModule.TriggerAttack();
                if (input.ConsumeHeavyAttack()) combatModule.TriggerHeavyAttack();
                if (input.ConsumeBlock()) combatModule.SetBlock(true);
                if (input.ConsumeSlide()) combatModule.SetSlide(true);
                if (input.ConsumeSwitchWeapon()) combatModule.SwitchWeaponNext();
                if (input.ConsumeAim()) combatModule.SetAim(true);
            }

            if (TryGetModule(out EntityBasicSkillModule skillModule))
            {
                if (input.ConsumeSkill1()) skillModule.TriggerSkill(1);
                if (input.ConsumeSkill2()) skillModule.TriggerSkill(2);
                if (input.ConsumeSkill3()) skillModule.TriggerSkill(3);
            }

            input.ClearOneShot();
        }

        private bool TryGetModule<T>(out T module) where T : class, IModule
        {
            if (MyCore.ModuleTables.TryGetValue(typeof(T), out var m))
            {
                module = m as T;
                return module != null;
            }
            module = null;
            return false;
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
    public struct InputSnapshot
    {
        [LabelText("帧")]
        public int frameIndex;

        [LabelText("移动")]
        public Vector2 move;

        [LabelText("视角")]
        public Vector2 look;

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

        [LabelText("下蹲(按住)")]
        public bool crouchHold;

        [LabelText("小眼睛(按住)")]
        public bool eyeHold;

        public void ClearOneShot()
        {
            attack = false;
            heavyAttack = false;
            block = false;
            slide = false;
            switchWeapon = false;
            aim = false;
            skill1 = false;
            skill2 = false;
            skill3 = false;
            jump = false;
        }

        public bool ConsumeAttack() => Consume(ref attack);
        public bool ConsumeHeavyAttack() => Consume(ref heavyAttack);
        public bool ConsumeBlock() => Consume(ref block);
        public bool ConsumeSlide() => Consume(ref slide);
        public bool ConsumeSwitchWeapon() => Consume(ref switchWeapon);
        public bool ConsumeAim() => Consume(ref aim);
        public bool ConsumeSkill1() => Consume(ref skill1);
        public bool ConsumeSkill2() => Consume(ref skill2);
        public bool ConsumeSkill3() => Consume(ref skill3);
        public bool ConsumeJump() => Consume(ref jump);

        public bool CrouchHold => crouchHold;
        public bool EyeHold => eyeHold;

        private static bool Consume(ref bool value)
        {
            if (!value) return false;
            value = false;
            return true;
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

    public enum InputQuickPreset
    {
        Default,
        Empty
    }

    public enum InputSchemeKey
    {
        Default,
        KeyboardMouse,
        Gamepad,
        Both
    }

    public enum InputActionKey
    {
        Move,
        Look,
        Attack,
        HeavyAttack,
        Block,
        Slide,
        SwitchWeapon,
        Aim,
        Skill1,
        Skill2,
        Skill3,
        Jump,
        Crouch
    }

#if ENABLE_INPUT_SYSTEM
    public static class EntityInputQuickInit
    {
        public static void ApplyPreset(EntityAIInputSystemModule module, InputQuickPreset preset, InputSchemeKey schemeKey)
        {
            if (module == null) return;

            if (preset == InputQuickPreset.Empty)
            {
                module.InitEmpty();
                return;
            }

            ApplySingle(module, InputActionKey.Move, schemeKey);
            ApplySingle(module, InputActionKey.Look, schemeKey);
            ApplySingle(module, InputActionKey.Attack, schemeKey);
            ApplySingle(module, InputActionKey.HeavyAttack, schemeKey);
            ApplySingle(module, InputActionKey.Block, schemeKey);
            ApplySingle(module, InputActionKey.Slide, schemeKey);
            ApplySingle(module, InputActionKey.SwitchWeapon, schemeKey);
            ApplySingle(module, InputActionKey.Aim, schemeKey);
            ApplySingle(module, InputActionKey.Skill1, schemeKey);
            ApplySingle(module, InputActionKey.Skill2, schemeKey);
            ApplySingle(module, InputActionKey.Skill3, schemeKey);
            ApplySingle(module, InputActionKey.Jump, schemeKey);
            ApplySingle(module, InputActionKey.Crouch, schemeKey);
        }

        public static void ApplySingle(EntityAIInputSystemModule module, InputActionKey key, InputSchemeKey schemeKey)
        {
            if (module == null) return;

            switch (key)
            {
                case InputActionKey.Move:
                    module.moveAction = CreateMoveAction(key.ToString(), schemeKey);
                    break;
                case InputActionKey.Look:
                    module.lookAction = CreateLookAction(key.ToString(), schemeKey);
                    break;
                case InputActionKey.Attack:
                    module.attackAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.HeavyAttack:
                    module.heavyAttackAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Block:
                    module.blockAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Slide:
                    module.slideAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.SwitchWeapon:
                    module.switchWeaponAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Aim:
                    module.aimAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Skill1:
                    module.skill1Action = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Skill2:
                    module.skill2Action = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Skill3:
                    module.skill3Action = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Jump:
                    module.jumpAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
                case InputActionKey.Crouch:
                    module.crouchAction = CreateButtonAction(key.ToString(), GetBindings(key, schemeKey));
                    break;
            }
        }

        private static InputActionProperty CreateMoveAction(string name, InputSchemeKey schemeKey)
        {
            var action = new InputAction(name, InputActionType.Value) { expectedControlType = "Vector2" };

            if (schemeKey == InputSchemeKey.KeyboardMouse || schemeKey == InputSchemeKey.Both || schemeKey == InputSchemeKey.Default)
            {
                action.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a")
                    .With("Right", "<Keyboard>/d");
            }

            if (schemeKey == InputSchemeKey.Gamepad || schemeKey == InputSchemeKey.Both || schemeKey == InputSchemeKey.Default)
            {
                action.AddBinding("<Gamepad>/leftStick");
            }

            return new InputActionProperty(action);
        }

        private static InputActionProperty CreateLookAction(string name, InputSchemeKey schemeKey)
        {
            var action = new InputAction(name, InputActionType.Value) { expectedControlType = "Vector2" };

            if (schemeKey == InputSchemeKey.KeyboardMouse || schemeKey == InputSchemeKey.Both || schemeKey == InputSchemeKey.Default)
            {
                action.AddBinding("<Mouse>/delta");
            }

            if (schemeKey == InputSchemeKey.Gamepad || schemeKey == InputSchemeKey.Both || schemeKey == InputSchemeKey.Default)
            {
                action.AddBinding("<Gamepad>/rightStick");
            }

            return new InputActionProperty(action);
        }

        private static string[] GetBindings(InputActionKey key, InputSchemeKey schemeKey)
        {
            var list = new List<string>(4);

            bool useKbm = schemeKey == InputSchemeKey.KeyboardMouse || schemeKey == InputSchemeKey.Both || schemeKey == InputSchemeKey.Default;
            bool usePad = schemeKey == InputSchemeKey.Gamepad || schemeKey == InputSchemeKey.Both || schemeKey == InputSchemeKey.Default;

            if (useKbm)
            {
                switch (key)
                {
                    case InputActionKey.Attack: list.Add("<Mouse>/leftButton"); break;
                    case InputActionKey.HeavyAttack: list.Add("<Mouse>/rightButton"); break;
                    case InputActionKey.Block: list.Add("<Keyboard>/leftShift"); break;
                    case InputActionKey.Slide: list.Add("<Keyboard>/leftCtrl"); break;
                    case InputActionKey.SwitchWeapon: list.Add("<Keyboard>/tab"); break;
                    case InputActionKey.Aim: list.Add("<Mouse>/rightButton"); break;
                    case InputActionKey.Skill1: list.Add("<Keyboard>/1"); break;
                    case InputActionKey.Skill2: list.Add("<Keyboard>/2"); break;
                    case InputActionKey.Skill3: list.Add("<Keyboard>/3"); break;
                    case InputActionKey.Jump: list.Add("<Keyboard>/space"); break;
                    case InputActionKey.Crouch: list.Add("<Keyboard>/c"); break;
                }
            }

            if (usePad)
            {
                switch (key)
                {
                    case InputActionKey.Attack: list.Add("<Gamepad>/rightTrigger"); break;
                    case InputActionKey.HeavyAttack: list.Add("<Gamepad>/leftTrigger"); break;
                    case InputActionKey.Block: list.Add("<Gamepad>/leftShoulder"); break;
                    case InputActionKey.Slide: list.Add("<Gamepad>/rightShoulder"); break;
                    case InputActionKey.SwitchWeapon: list.Add("<Gamepad>/dpad/right"); break;
                    case InputActionKey.Aim: list.Add("<Gamepad>/leftTrigger"); break;
                    case InputActionKey.Skill1: list.Add("<Gamepad>/dpad/up"); break;
                    case InputActionKey.Skill2: list.Add("<Gamepad>/dpad/left"); break;
                    case InputActionKey.Skill3: list.Add("<Gamepad>/dpad/down"); break;
                    case InputActionKey.Jump: list.Add("<Gamepad>/buttonSouth"); break;
                    case InputActionKey.Crouch: list.Add("<Gamepad>/rightStickPress"); break;
                }
            }

            return list.ToArray();
        }

        private static InputActionProperty CreateButtonAction(string name, params string[] bindings)
        {
            var action = new InputAction(name, InputActionType.Button);
            for (int i = 0; i < bindings.Length; i++)
            {
                action.AddBinding(bindings[i]);
            }
            return new InputActionProperty(action);
        }
    }
#endif
}
