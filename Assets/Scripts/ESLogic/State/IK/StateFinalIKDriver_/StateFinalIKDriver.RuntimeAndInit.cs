using UnityEngine;
using System;
using RootMotion.FinalIK;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        // ════════════════════════════════════════════════════════════════════════
        // 生命周期
        // ════════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // 初始保持激活，但在 Bind 完成前仅安全空转，不参与任何有效 IK 驱动。
            _driverEnableStateSummary = "Enabled | Awake: 初始激活，等待 Bind 完成";
            _runtimeBindingReady = false;
            _aimPeekRuntimeReference = transform;
            _goalTargetsReady = false;
            _hintTargetsReady = false;
        }

        private void Start()
        {
            if (HasRuntimeBinding())
            {
                _runtimeBindingReady = true;
                _driverEnableStateSummary =
                    $"Enabled | Start: 绑定引用已就绪 | Animator={_animator.name} | StateMachineKey={_stateMachine.stateMachineKey}";
                return;
            }

            _runtimeBindingReady = false;
            SetDriverEnabled(false, "Start: 绑定引用未就绪，暂停驱动直到 Bind");
        }

        private void OnEnable()
        {
            if (_isInternalEnableStateChange)
                _isInternalEnableStateChange = false;
        }

        private void OnDisable()
        {
            if (_isInternalEnableStateChange)
            {
                _isInternalEnableStateChange = false;
                return;
            }

            if (!Application.isPlaying)
                return;

            if (_stateMachine != null)
            {
                Debug.LogWarning(
                    $"[StateFinalIKDriver] 组件被外部禁用。当前已绑定 StateMachine，LateUpdate 将停止。\n" +
                    $"最近状态：{_driverEnableStateSummary}",
                    this);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // 绑定 / 解绑
        // ════════════════════════════════════════════════════════════════════════

        internal void Bind(StateMachine machine, Animator animator)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            if (animator == null) throw new ArgumentNullException(nameof(animator));
#endif
            _stateMachine  = machine;
            _animator      = animator;
            _presentButBad = FinalIKCapabilityFlags.None;
            _caps          = FinalIKCapabilityFlags.None;

            // ── 一次性扫描：只查询已启用功能的组件，禁用功能零 GetComponent 开销 ──
            var want = FinalIKCapabilityFlags.None;
            if (enableBipedIK)                                        want |= FinalIKCapabilityFlags.BipedIK;
            if (enableLookAtIK)                                       want |= FinalIKCapabilityFlags.LookAtIK;
            if (enableAimIK)                                          want |= FinalIKCapabilityFlags.AimIK;
            if (enableGrounderBipedIK)                                want |= FinalIKCapabilityFlags.GrounderBipedIK;
            if (enableFullBodyBipedIK)
            {
                want |= FinalIKCapabilityFlags.FullBodyBipedIK;
                if (enableHitReaction)  want |= FinalIKCapabilityFlags.HitReaction;
                if (enableRecoil)       want |= FinalIKCapabilityFlags.Recoil;
            }
            _refs.Scan(animator, want);

            // ── 预置引用覆盖（Inspector 提前拖入的组件优先于自动扫描结果） ──────
            if (presetBipedIK         != null) _refs.bipedIK         = presetBipedIK;
            if (presetLookAtIK        != null) _refs.lookAtIK        = presetLookAtIK;
            if (presetAimIK           != null) _refs.aimIK           = presetAimIK;
            if (presetGrounderBipedIK != null) _refs.grounderBipedIK = presetGrounderBipedIK;
            if (presetFullBodyBipedIK != null) _refs.fullBodyBipedIK = presetFullBodyBipedIK;
            if (presetHitReaction     != null) _refs.hitReaction     = presetHitReaction;
            if (presetRecoil          != null) _refs.recoil          = presetRecoil;

            // ── 自动补全缺失组件（启用但未挂载时自动处理）────────────────────────
            AutoAddMissingComponents();

            // ── Driver 统一骨骼绑定 / Aim 骨链（用于脱离 FinalIK 原生面板） ─────
            ApplyDriverBoneBindingsToConfiguredIK();

            // ── 各功能组初始化（顺序有依赖：Grounder 需 BipedIK 先就绪） ──────
            InitBipedIK();
            InitLookAtIK();
            InitAimIK();
            InitGrounder();
            InitHitReactionAndRecoil();

            // ── 辅助 Transform（Hint/Goal） ────────────────────────────────────
            EnsureHintTransforms();
            EnsureGoalTargetTransforms();
            SeedGoalTargetsFromCurrentRigPose();

            // ── 缺失组件提示（Info 级别，可选） ───────────────────────────────
            if (logMissingComponentHints) LogMissingHints();

            // ── 初始参数应用（就绪的 IK 组件按 Inspector 配置进行一次性设置）────
            ApplyIKInitialSettings();

            _wasBipedIKBound = _bipedIKReady;
            _hasLastPose     = false;
            _runtimeBindingReady = true;
            SetDriverEnabled(true, $"Bind 完成 | Animator={animator.name} | StateMachineKey={machine.stateMachineKey}");
        }

        internal void Unbind()
        {
            // Grounder 需要显式关闭（OnDisable 会清零 BipedIK 脚步权重）
            if (_grounderBipedIK != null) _grounderBipedIK.enabled = false;

            _bipedIKReady      = false;  _bipedIK      = null;
            _lookAtIKReady     = false;  _lookAtIK     = null;
            _aimIKReady        = false;  _aimIK        = null;
            _aimIKError        = string.Empty;
            _aimIKLastHeartbeatTime = -1f;
            _aimIKDecaying          = false;
            _grounderReady     = false;  _grounderBipedIK = null;
            _hitReactionReady  = false;  _hitReaction  = null;
            _recoilReady       = false;  _recoil       = null;

            _refs.Clear();
            _caps          = FinalIKCapabilityFlags.None;
            _presentButBad = FinalIKCapabilityFlags.None;
            _stateMachine  = null;
            _animator      = null;
            _hasLastPose   = false;
            _wasBipedIKBound  = false;
            _goalTargetsInit  = false;
            _fullLookAtCompare = true;
            _runtimeBindingReady = false;
            _lookAtSpineBones = null;
            _lookAtSpineBoneCount = 0;
            _aimPeekRuntimeReference = transform;
            _goalTargetsReady = false;
            _hintTargetsReady = false;
            ResetRuntimeLerpingRates();
            ResetRuntimeLimbWeights();
            SetDriverEnabled(false, "Unbind: StateMachine 已释放或解绑");
        }

        // ════════════════════════════════════════════════════════════════════════
        // LateUpdate — 主驱动帧循环（order -1，在所有 FinalIK 组件之前）
        // ════════════════════════════════════════════════════════════════════════

        // 契约：Start/Bind 一次性确认运行态引用可用后才进入有效驱动；
        //      LateUpdate 热路径不再做 StateMachine/Animator 判空。
        private void LateUpdate()
        {
            if (!_runtimeBindingReady || !_stateMachine.isRunning) return;

            float now = Time.unscaledTime;
            _ikWeightSmoothDeltaTime = Time.deltaTime;
            UpdateLerpingRateRecovery();

            if (!_bipedIKReady)
            {
                if (rebindInterval <= 0f || (now - _lastBindTryTime) >= rebindInterval)
                {
                    _lastBindTryTime = now;
                    TryRebindBipedIK();
                }
            }

            if (_aimIKReady)
            {
                _aimIK.solver.target = ResolveAimTarget(_aimSourceTarget);
                UpdateAimIKHeartbeat(now);
                UpdateAimIKWeightSmoothing();
            }

            if (_grounderReady)
                UpdateGrounderWeightSmoothing();

            var pose = _stateMachine.stateGeneralFinalIKDriverPose;
            if (enableRealtimeWeightTest)
                ApplyRealtimeWeightTest(ref pose);

            bool bipedRuntimeReady = _bipedIKReady && _goalTargetsReady && _hintTargetsReady;
            bool hasActiveLookAt = pose.lookAtWeight > 0.001f || HasActiveLookAtWeightSmoothing();
            bool requiresBipedFallbackLookAt = !_lookAtIKReady && hasActiveLookAt;
            bool requiresBipedSolve = bipedRuntimeReady && (pose.HasLimbWeight || HasActiveBipedWeightSmoothing() || requiresBipedFallbackLookAt);

            if (!bipedRuntimeReady)
            {
                ApplyLookAt(in pose);

                if (warnWhenPoseHasWeightButNoIK && pose.HasLimbWeight
                    && (now - _lastWarnTime) >= WarnInterval)
                {
                    _lastWarnTime = now;
                    var err = string.IsNullOrEmpty(_bipedIKError) ? "原因未知" : _bipedIKError;
                    Debug.LogWarning(
                        $"[StateFinalIKDriver] stateGeneralFinalIKDriverPose 有权重，但 Biped 运行条件未满足：{err}\n" +
                        "→ 检查 BipedIK 组件、References，以及运行时目标/Hint Transform 是否已正确建立。",
                        _animator);
                }

                return;
            }

            if (!requiresBipedSolve)
            {
                ApplyLookAt(in pose);
                if (_hasLastPose || HasActiveBipedWeightSmoothing())
                {
                    _bipedIK.SetToDefaults();
                    ResetRuntimeLimbWeights();
                    _hasLastPose = false;
                }
                return;
            }

            if (!_wasBipedIKBound)
            {
                _hasLastPose     = false;
                _wasBipedIKBound = true;
            }

            bool dirty = !_hasLastPose || !PoseApproxEqual(in _lastPose, in pose);
            if (!driveGoalTargetsFromPose && AreGoalTargetsDirty()) dirty = true;

            if (dirty)
            {
                InitGoalTargetsOnce(in pose);
                if (driveGoalTargetsFromPose) SyncGoalTargets(in pose);

                _lastPose    = pose;
                _hasLastPose = true;
                _applyCount++;
                _lastApplyTime = now;
                CacheGoalTargetSnapshot();
            }

            if (!pose.HasAnyWeight)
            {
                if (!HasActiveBipedWeightSmoothing())
                {
                    _bipedIK.SetToDefaults();
                    ResetRuntimeLimbWeights();
                }
                else
                {
                    ApplyGoal(in pose.leftHand,  AvatarIKGoal.LeftHand,  _lhHint, _lhTarget);
                    ApplyGoal(in pose.rightHand, AvatarIKGoal.RightHand, _rhHint, _rhTarget);
                    ApplyGoal(in pose.leftFoot,  AvatarIKGoal.LeftFoot,  _lfHint, _lfTarget);
                    ApplyGoal(in pose.rightFoot, AvatarIKGoal.RightFoot, _rfHint, _rfTarget);
                }
            }
            else
            {
                ApplyGoal(in pose.leftHand,  AvatarIKGoal.LeftHand,  _lhHint, _lhTarget);
                ApplyGoal(in pose.rightHand, AvatarIKGoal.RightHand, _rhHint, _rhTarget);
                ApplyGoal(in pose.leftFoot,  AvatarIKGoal.LeftFoot,  _lfHint, _lfTarget);
                ApplyGoal(in pose.rightFoot, AvatarIKGoal.RightFoot, _rfHint, _rfTarget);
            }

            ApplyLookAt(in pose);

            _bipedIK.UpdateBipedIK();
            _solverUpdateCount++;
            _lastSolverUpdateTime = now;
        }

        // ════════════════════════════════════════════════════════════════════════
        // 公共 API — 外部驱动入口
        // ════════════════════════════════════════════════════════════════════════

        public bool HandleAim(Transform target, float weight = 1f)
        {
            if (!_aimIKReady) return false;
            _aimSourceTarget               = target;
            _aimIK.solver.target           = ResolveAimTarget(target);
            SetAimRuntimeWeights(target != null ? Mathf.Clamp01(weight) : 0f);
            RecordAimIKHeartbeat();
            return true;
        }

        public bool HandleAim()
        {
            if (!_aimIKReady) return false;
            _aimIK.solver.target = ResolveAimTarget(_aimSourceTarget);
            RecordAimIKHeartbeat();
            return true;
        }

        public bool HandleAim(float weight)
        {
            if (!_aimIKReady) return false;
            _aimIK.solver.target = ResolveAimTarget(_aimSourceTarget);
            SetAimRuntimeWeights(Mathf.Clamp01(weight));
            RecordAimIKHeartbeat();
            return true;
        }

        public bool HandleAimTarget(Transform target)
        {
            if (!_aimIKReady) return false;
            _aimSourceTarget = target;
            _aimIK.solver.target = ResolveAimTarget(target);
            if (target == null)
                SetAimRuntimeWeights(0f);
            else
                SetAimRuntimeWeights(_aimWeightTarget);
            RecordAimIKHeartbeat();
            return true;
        }

        private void SetAimLerpingRate(float rate)
        {
            _aimLerpingRate = NormalizeLerpingRate(rate);
        }

        public bool SetAimPeek(float normalizedPeek)
        {
            if (!_aimIKReady) return false;
            float clampedPeek = Mathf.Clamp(normalizedPeek, -1f, 1f);
            if (Mathf.Abs(_aimPeek - clampedPeek) <= 0.0001f)
                return true;

            _aimPeek = clampedPeek;
            _aimIK.solver.target = ResolveAimTarget(_aimSourceTarget);
            return true;
        }

        public bool SetAimPeekViewReference(Transform viewTransform)
        {
            if (_aimPeekViewReference == viewTransform)
                return _aimIKReady;

            _aimPeekViewReference = viewTransform;
            RefreshAimPeekRuntimeReference();
            if (!_aimIKReady) return false;
            _aimIK.solver.target = ResolveAimTarget(_aimSourceTarget);
            return true;
        }

        public bool SetAimPeekLeft(float intensity = 1f)
        {
            return SetAimPeek(-Mathf.Clamp01(intensity));
        }

        public bool SetAimPeekRight(float intensity = 1f)
        {
            return SetAimPeek(Mathf.Clamp01(intensity));
        }

        public bool ClearAimPeek()
        {
            return SetAimPeek(0f);
        }

        private float ResolveInitialAimWeight()
        {
            return useInitAimWeightOnBind ? Mathf.Clamp01(initAimWeight) : 0f;
        }

        public void ApplyIKInitialSettings()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[StateFinalIKDriver] ApplyIKInitialSettings 仅在运行时有效。");
                return;
            }

            if (_lookAtIKReady)
            {
                var s = _lookAtIK.solver;
                _lookAtWeightCurrent = _lookAtWeightTarget = initLookAtWeight;
                _lookAtHeadWeightCurrent = _lookAtHeadWeightTarget = initLookAtHeadWeight;
                _lookAtEyesWeightCurrent = _lookAtEyesWeightTarget = initLookAtEyesWeight;
                _lookAtBodyWeightCurrent = _lookAtBodyWeightTarget = initLookAtSpineWeight;
                _lookAtClampWeightCurrent = _lookAtClampWeightTarget = initLookAtClampWeight;
                s.IKPositionWeight = _lookAtWeightCurrent;
                s.head.weight      = _lookAtHeadWeightCurrent;
                s.eyesWeight       = _lookAtEyesWeightCurrent;
                s.clampWeight      = _lookAtClampWeightCurrent;
                if (s.spine != null)
                    for (int i = 0; i < s.spine.Length; i++)
                        s.spine[i].weight = _lookAtBodyWeightCurrent;
            }

            if (_aimIKReady)
            {
                var s = _aimIK.solver;
                _aimWeightCurrent = _aimWeightTarget = ResolveInitialAimWeight();
                _aimPoleWeightCurrent = _aimPoleWeightTarget = ResolveAimPoleWeight(_aimWeightCurrent);
                s.IKPositionWeight = _aimWeightCurrent;
                s.poleWeight = _aimPoleWeightCurrent;
                s.clampWeight = initAimClampWeight;
                if (initAimAxis != Vector3.zero)
                    s.axis = initAimAxis.normalized;
            }

            if (_grounderReady)
            {
                _grounderWeightCurrent = _grounderWeightTarget = Mathf.Clamp01(initGrounderWeight);
                _grounderBipedIK.weight = _grounderWeightCurrent;
                _grounderBipedIK.enabled = _grounderWeightCurrent > 0.001f;
                var s = _grounderBipedIK.solver;
                s.maxStep  = initGrounderMaxStep;
                s.footSpeed = initGrounderSpeed;
            }

            if (_bipedIKReady)
            {
                ResetRuntimeLimbWeights();
                ApplyCurrentPoseToBipedIK();
                _bipedIK.UpdateBipedIK();
            }
        }

        public void HandleStopAim()
        {
            if (!_aimIKReady) return;
            SetAimRuntimeWeights(0f);
            _aimIKLastHeartbeatTime        = -1f;
            _aimIKDecaying                 = false;
        }

        public bool HandleHit(Collider collider, Vector3 force, Vector3 point)
        {
            if (!_hitReactionReady) return false;
            _hitReaction.Hit(collider, force, point);
            return true;
        }

        public bool HandleRecoil(float magnitude = 1f)
        {
            if (!_recoilReady) return false;
            _recoil.Fire(magnitude);
            return true;
        }

        public bool HandleGrounder(bool active)
        {
            if (!_grounderReady) return false;
            _grounderWeightTarget = active ? Mathf.Clamp01(initGrounderWeight) : 0f;
            _grounderBipedIK.enabled = active || _grounderWeightCurrent > 0.001f;
            return true;
        }

        // ════════════════════════════════════════════════════════════════════════
        // AimIK 心跳衰减（私有）
        // ════════════════════════════════════════════════════════════════════════

        private void RecordAimIKHeartbeat()
        {
            _aimIKLastHeartbeatTime = Time.unscaledTime;
            _aimIKDecaying          = false;
        }

        private void UpdateAimIKHeartbeat(float now)
        {
            if (aimIKHeartbeatTimeout <= 0f)   return;
            if (_aimIKLastHeartbeatTime < 0f)  return;

            if (_aimWeightCurrent <= 0.001f && _aimWeightTarget <= 0.001f)
            {
                _aimIKLastHeartbeatTime = -1f;
                _aimIKDecaying          = false;
                return;
            }

            if (!_aimIKDecaying)
            {
                if ((now - _aimIKLastHeartbeatTime) < aimIKHeartbeatTimeout) return;
                _aimIKDecaying       = true;
                _aimIKDecayStartTime = now;
                SetAimLerpingRate(ResolveLerpingRateForDuration(aimIKDecayDuration));
                SetAimRuntimeWeights(0f);
            }

            if (_aimWeightCurrent <= 0.001f && _aimWeightTarget <= 0.001f)
            {
                _aimSourceTarget               = null;
                _aimIK.solver.target           = null;
                _aimIKLastHeartbeatTime        = -1f;
                _aimIKDecaying                 = false;
            }
        }

        private void UpdateAimIKWeightSmoothing()
        {
            if (!_aimIKReady) return;

            bool shouldStayEnabled = _aimWeightCurrent > 0.001f || _aimWeightTarget > 0.001f || _aimPoleWeightCurrent > 0.001f || _aimPoleWeightTarget > 0.001f;
            if (_aimIK.enabled != shouldStayEnabled)
                _aimIK.enabled = shouldStayEnabled;

            if (!shouldStayEnabled)
            {
                _aimIK.solver.IKPositionWeight = 0f;
                _aimIK.solver.poleWeight = 0f;
                _aimIK.solver.target = null;
                return;
            }

            _aimWeightCurrent = SmoothIKWeight(_aimWeightCurrent, _aimWeightTarget, ResolveAimWeightSmoothTime(), _aimLerpingRate);
            _aimPoleWeightCurrent = SmoothIKWeight(_aimPoleWeightCurrent, _aimPoleWeightTarget, ResolveAimWeightSmoothTime(), _aimLerpingRate);
            _aimIK.solver.IKPositionWeight = _aimWeightCurrent;
            _aimIK.solver.poleWeight = _aimPoleWeightCurrent;

            if (_aimWeightTarget <= 0.001f && _aimWeightCurrent <= 0.001f)
            {
                _aimWeightCurrent = 0f;
                _aimPoleWeightCurrent = 0f;
                _aimIK.solver.IKPositionWeight = 0f;
                _aimIK.solver.poleWeight = 0f;
                if (!_aimIKDecaying)
                {
                    _aimSourceTarget = null;
                    _aimIK.solver.target = null;
                }
            }
        }

        private void UpdateGrounderWeightSmoothing()
        {
            if (!_grounderReady) return;

            _grounderWeightCurrent = SmoothIKWeight(_grounderWeightCurrent, _grounderWeightTarget, ResolveGrounderWeightSmoothTime(), _grounderLerpingRate);
            _grounderBipedIK.weight = _grounderWeightCurrent;

            bool shouldStayEnabled = _grounderWeightCurrent > 0.001f || _grounderWeightTarget > 0.001f;
            if (_grounderBipedIK.enabled != shouldStayEnabled)
                _grounderBipedIK.enabled = shouldStayEnabled;
        }

        private float SmoothIKWeight(float current, float target, float smoothTime, float lerpingRate = 1f)
        {
            smoothTime = Mathf.Max(0f, smoothTime);
            if (smoothTime <= 0.0001f)
                return target;

            float safeLerpingRate = Mathf.Max(0.01f, lerpingRate);
            smoothTime /= safeLerpingRate;

            float deltaTime = Mathf.Max(0f, _ikWeightSmoothDeltaTime);
            if (deltaTime <= 0f)
                return current;

            float t = 1f - Mathf.Exp(-deltaTime / smoothTime);
            return Mathf.Lerp(current, target, t);
        }

        private float ResolveLerpingRateForDuration(float duration)
        {
            float smoothTime = Mathf.Max(0.0001f, ResolveAimWeightSmoothTime());
            float safeDuration = Mathf.Max(0.0001f, duration);
            return NormalizeLerpingRate(smoothTime / safeDuration);
        }

        // ════════════════════════════════════════════════════════════════════════
        // 初始化：各功能组（只在 Bind 时调用一次）
        // ════════════════════════════════════════════════════════════════════════

        #region Init — BipedIK

        private void InitBipedIK()
        {
            _bipedIKReady = false;
            if (!enableBipedIK)
            {
                _bipedIKError = "功能未启用（enableBipedIK = false）";
                return;
            }
            _bipedIK      = _refs.bipedIK;
            if (_bipedIK == null)
            {
                _bipedIKError = "GameObject 上未挂载 BipedIK 组件";
                return;
            }

            if (autoDetectReferencesIfMissing
                && (_bipedIK.references == null || !_bipedIK.references.isFilled))
            {
                RootMotion.BipedReferences.AutoDetectReferences(
                    ref _bipedIK.references,
                    _bipedIK.transform,
                    new RootMotion.BipedReferences.AutoDetectParams(false, true));
            }

            if (_bipedIK.references == null || !_bipedIK.references.isFilled)
            {
                _bipedIKError  = autoDetectReferencesIfMissing
                    ? "BipedIK.references 未配置且自动识别失败（检查是否为 Humanoid、骨骼结构是否正常）"
                    : "BipedIK.references 未配置（已关闭自动识别）";
                _presentButBad |= FinalIKCapabilityFlags.BipedIK;
                return;
            }

            string setupErr = string.Empty;
            if (RootMotion.BipedReferences.SetupError(_bipedIK.references, ref setupErr))
            {
                _bipedIKError  = setupErr;
                _presentButBad |= FinalIKCapabilityFlags.BipedIK;
                return;
            }

            _bipedIK.enabled = false;
            _bipedIK.InitiateBipedIK();
            _bipedIKError  = string.Empty;
            _bipedIKReady  = true;
            _caps         |= FinalIKCapabilityFlags.BipedIK;
            _bindTryCount++;
            _bindSuccessCount++;
        }

        private void TryRebindBipedIK()
        {
            _bindTryCount++;
            _bipedIK = _animator.GetComponent<BipedIK>();
            if (_bipedIK == null) { _bipedIKError = "未找到 BipedIK 组件"; return; }

            string err = string.Empty;
            if ((_bipedIK.references == null || !_bipedIK.references.isFilled) && autoDetectReferencesIfMissing)
                RootMotion.BipedReferences.AutoDetectReferences(
                    ref _bipedIK.references, _bipedIK.transform,
                    new RootMotion.BipedReferences.AutoDetectParams(false, true));

            if (_bipedIK.references == null || !_bipedIK.references.isFilled
                || RootMotion.BipedReferences.SetupError(_bipedIK.references, ref err))
            {
                _bipedIKError = string.IsNullOrEmpty(err) ? "BipedIK.references 不完整" : err;
                return;
            }

            _bipedIK.enabled  = false;
            _bipedIK.InitiateBipedIK();
            _refs.bipedIK     = _bipedIK;
            _bipedIKError     = string.Empty;
            _bipedIKReady     = true;
            _wasBipedIKBound  = false;
            _caps            |= FinalIKCapabilityFlags.BipedIK;
            _bindSuccessCount++;

            if (enableGrounderBipedIK)
                InitGrounder();

            ApplyIKInitialSettings();
        }

        #endregion

        #region Init — LookAtIK

        private void InitLookAtIK()
        {
            _lookAtIKReady = false;
            _lookAtSpineBones = null;
            _lookAtSpineBoneCount = 0;
            if (!enableLookAtIK) return;
            _lookAtIK      = _refs.lookAtIK;
            if (_lookAtIK == null) return;

            if (useDriverBoneBinding)
                ApplyDriverLookAtBinding(_lookAtIK);

            _lookAtIK.enabled = false;
            _lookAtIK.solver.IKPositionWeight = 0f;
            _lookAtIK.solver.head.weight = 0f;
            _lookAtIK.solver.eyesWeight = 0f;
            _lookAtIK.solver.clampWeight = 0f;

            _lookAtIKReady    = true;
            _lookAtSpineBones = _lookAtIK.solver.spine;
            _lookAtSpineBoneCount = _lookAtSpineBones != null ? _lookAtSpineBones.Length : 0;
            _caps            |= FinalIKCapabilityFlags.LookAtIK;
            _fullLookAtCompare = false;
        }

        #endregion

        #region Init — AimIK

        private void InitAimIK()
        {
            _aimIKReady = false;
            if (!enableAimIK)
            {
                _aimIKError = "功能未启用（enableAimIK = false）";
                return;
            }
            _aimIK      = _refs.aimIK;
            if (_aimIK == null)
            {
                _aimIKError = "GameObject 上未挂载 AimIK 组件";
                return;
            }

            TryPopulateAimChainFromDriverBinding();
            if (!ApplyDriverAimChain(_aimIK))
            {
                _presentButBad |= FinalIKCapabilityFlags.AimIK;
                return;
            }

            _aimSourceTarget = null;
            _aimPeek = 0f;
            _aimIK.solver.target = null;
            RefreshAimPeekRuntimeReference();
            _aimWeightCurrent = _aimWeightTarget = ResolveInitialAimWeight();
            _aimPoleWeightCurrent = _aimPoleWeightTarget = ResolveAimPoleWeight(_aimWeightCurrent);
            _aimIK.enabled = _aimWeightCurrent > 0.001f || _aimWeightTarget > 0.001f;
            _aimIK.solver.IKPositionWeight = _aimWeightCurrent;
            _aimIK.solver.poleWeight = _aimPoleWeightCurrent;
            _aimIKError = string.Empty;
            _aimIKReady = true;
            _caps      |= FinalIKCapabilityFlags.AimIK;
        }

        #endregion

        #region Init — Grounder

        private void InitGrounder()
        {
            _grounderReady   = false;
            if (!enableGrounderBipedIK) return;
            _grounderBipedIK = _refs.grounderBipedIK;
            if (_grounderBipedIK == null) return;

            if (!_bipedIKReady)
            {
                Debug.LogWarning(
                    "[StateFinalIKDriver] GrounderBipedIK 已挂载，但 BipedIK 未就绪，接地系统跳过。\n" +
                    "→ 确保同 GameObject 上的 BipedIK 组件 References 已正确配置。",
                    _animator);
                return;
            }

            _grounderBipedIK.enabled = true;
            _grounderWeightCurrent = _grounderWeightTarget = Mathf.Clamp01(initGrounderWeight);
            _grounderReady = true;
            _caps         |= FinalIKCapabilityFlags.GrounderBipedIK;
        }

        #endregion

        #region Init — HitReaction & Recoil

        private void InitHitReactionAndRecoil()
        {
            _hitReactionReady = false;
            _recoilReady      = false;

            if (!enableFullBodyBipedIK) return;

            _hitReaction = enableHitReaction ? _refs.hitReaction : null;
            _recoil      = enableRecoil      ? _refs.recoil      : null;

            _refs.fullBodyBipedIK = _refs.fullBodyBipedIK ?? presetFullBodyBipedIK ?? GetComponent<FullBodyBipedIK>();
            bool fbbikOk = _refs.fullBodyBipedIK != null;

            if (_hitReaction != null)
            {
                if (useDriverHitReactionSetup)
                    ApplyDriverHitReaction(_hitReaction);

                if (fbbikOk) { _hitReactionReady = true; _caps |= FinalIKCapabilityFlags.HitReaction; }
                else Debug.LogWarning(
                    "[StateFinalIKDriver] HitReaction 已挂载，但缺少 FullBodyBipedIK，HitReaction 未激活。\n" +
                    "→ 在同 GameObject 上添加 RootMotion.FinalIK.FullBodyBipedIK 组件。", _animator);
            }

            if (_recoil != null)
            {
                if (useDriverRecoilSetup)
                    ApplyDriverRecoil(_recoil);

                if (fbbikOk) { _recoilReady = true; _caps |= FinalIKCapabilityFlags.Recoil; }
                else Debug.LogWarning(
                    "[StateFinalIKDriver] Recoil 已挂载，但缺少 FullBodyBipedIK，Recoil 未激活。\n" +
                    "→ 在同 GameObject 上添加 RootMotion.FinalIK.FullBodyBipedIK 组件。", _animator);
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════════════════
        // 运行时 Apply — BipedIK 单肢（热路径，零分配）
        // ════════════════════════════════════════════════════════════════════════

        private void ApplyGoal(in IKGoalPose goal, AvatarIKGoal avatarGoal,
                                Transform hint, Transform target)
        {
            float w     = Mathf.Clamp01(goal.weight);
            bool  isFoot = avatarGoal == AvatarIKGoal.LeftFoot || avatarGoal == AvatarIKGoal.RightFoot;
            float rotW  = isFoot ? w * Mathf.Clamp01(footRotationWeightMultiplier) : w;

            ReadLimbRuntimeWeights(avatarGoal,
                out float positionCurrent, out float positionTarget,
                out float rotationCurrent, out float rotationTarget,
                out float bendCurrent, out float bendTarget,
                out float limbLerpingRate);

            if (Mathf.Abs(goal.lerpingRate - 1f) > 0.0001f)
                limbLerpingRate = NormalizeLerpingRate(goal.lerpingRate);

            float effectiveLimbLerpingRate = NormalizeLerpingRate(limbLerpingRate * _limbLerpingRate);

            positionTarget = w;
            rotationTarget = rotW;
            bendTarget = goal.hintPosition != Vector3.zero ? w : 1f;

            float limbSmoothTime = ResolveLimbWeightSmoothTime();
            positionCurrent = SmoothIKWeight(positionCurrent, positionTarget, limbSmoothTime, effectiveLimbLerpingRate);
            rotationCurrent = SmoothIKWeight(rotationCurrent, rotationTarget, limbSmoothTime, effectiveLimbLerpingRate);
            bendCurrent = SmoothIKWeight(bendCurrent, bendTarget, limbSmoothTime, effectiveLimbLerpingRate);

            WriteLimbRuntimeWeights(avatarGoal,
                positionCurrent, positionTarget,
                rotationCurrent, rotationTarget,
                bendCurrent, bendTarget,
                limbLerpingRate);

            _bipedIK.SetIKPositionWeight(avatarGoal, positionCurrent);
            _bipedIK.SetIKRotationWeight(avatarGoal, rotationCurrent);

            var limb = _bipedIK.GetGoalIK(avatarGoal);
            if (limb.target != target) limb.target = target;

            if (goal.hintPosition != Vector3.zero)
            {
                if ((hint.position - goal.hintPosition).sqrMagnitude > 0.000001f)
                    hint.position = goal.hintPosition;

                limb.bendGoal           = hint;
                limb.bendModifier       = IKSolverLimb.BendModifier.Goal;
                limb.bendModifierWeight = bendCurrent;
            }
            else
            {
                limb.bendModifier       = IKSolverLimb.BendModifier.Animation;
                limb.bendModifierWeight = bendCurrent;
                limb.bendGoal           = null;
            }
        }

        private void ApplyCurrentPoseToBipedIK()
        {
            if (!_runtimeBindingReady || !_bipedIKReady || !_goalTargetsReady || !_hintTargetsReady) return;

            ref var pose = ref _stateMachine.stateGeneralFinalIKDriverPose;

            if (!pose.HasAnyWeight)
            {
                _bipedIK.SetToDefaults();
                ResetRuntimeLimbWeights();
                ApplyLookAt(in pose);
                _hasLastPose = false;
                return;
            }

            InitGoalTargetsOnce(in pose);
            if (driveGoalTargetsFromPose)
                SyncGoalTargets(in pose);

            ApplyGoal(in pose.leftHand,  AvatarIKGoal.LeftHand,  _lhHint, _lhTarget);
            ApplyGoal(in pose.rightHand, AvatarIKGoal.RightHand, _rhHint, _rhTarget);
            ApplyGoal(in pose.leftFoot,  AvatarIKGoal.LeftFoot,  _lfHint, _lfTarget);
            ApplyGoal(in pose.rightFoot, AvatarIKGoal.RightFoot, _rfHint, _rfTarget);
            ApplyLookAt(in pose);

            _lastPose = pose;
            _hasLastPose = true;
            CacheGoalTargetSnapshot();
        }

        private void ApplyRealtimeWeightTest(ref StateGeneralFinalIKDriverPose pose)
        {
            SeedGoalTargetsFromCurrentRigPose();
            bool goalTargetsReady = _goalTargetsReady;

            pose.leftHand.weight = Mathf.Clamp01(realtimeLeftHandWeight);
            pose.rightHand.weight = Mathf.Clamp01(realtimeRightHandWeight);
            pose.leftFoot.weight = Mathf.Clamp01(realtimeLeftFootWeight);
            pose.rightFoot.weight = Mathf.Clamp01(realtimeRightFootWeight);

            if (goalTargetsReady && pose.leftHand.weight > 0.001f)
            {
                pose.leftHand.position = _lhTarget.position;
                pose.leftHand.rotation = _lhTarget.rotation;
            }
            if (goalTargetsReady && pose.rightHand.weight > 0.001f)
            {
                pose.rightHand.position = _rhTarget.position;
                pose.rightHand.rotation = _rhTarget.rotation;
            }
            if (goalTargetsReady && pose.leftFoot.weight > 0.001f)
            {
                pose.leftFoot.position = _lfTarget.position;
                pose.leftFoot.rotation = _lfTarget.rotation;
            }
            if (goalTargetsReady && pose.rightFoot.weight > 0.001f)
            {
                pose.rightFoot.position = _rfTarget.position;
                pose.rightFoot.rotation = _rfTarget.rotation;
            }

            pose.lookAtWeight = Mathf.Clamp01(realtimeLookAtWeight);
            if (realtimeLookAtTarget != null)
                pose.lookAtPosition = realtimeLookAtTarget.position;

            if (_aimIKReady)
            {
                _aimSourceTarget = realtimeAimTarget;
                _aimIK.solver.target = ResolveAimTarget(realtimeAimTarget);
                SetAimRuntimeWeights(realtimeAimTarget != null ? Mathf.Clamp01(realtimeAimWeight) : 0f);
                if (realtimeAimTarget != null && realtimeAimWeight > 0.001f)
                    RecordAimIKHeartbeat();
            }
        }

        private Transform ResolveAimTarget(Transform sourceTarget)
        {
            if (sourceTarget == null)
                return null;

            if (TryResolveAimPeekAnchorTarget(sourceTarget, out var anchorTarget))
                return anchorTarget;

            Vector3 localOffset = GetCurrentAimPeekLocalOffset();
            if (localOffset == Vector3.zero)
                return sourceTarget;

            EnsureAimTargetProxy();

            Vector3 worldOffset = _aimPeekRuntimeReference.TransformVector(localOffset);
            _aimTargetProxy.SetPositionAndRotation(sourceTarget.position + worldOffset, sourceTarget.rotation);
            return _aimTargetProxy;
        }

        private bool TryResolveAimPeekAnchorTarget(Transform sourceTarget, out Transform anchorTarget)
        {
            anchorTarget = null;

            if (Mathf.Abs(_aimPeek) <= 0.001f)
                return false;

            var anchor = GetCurrentAimPeekAnchor();
            if (anchor == null)
                return false;

            var viewReference = _aimPeekViewReference != null ? _aimPeekViewReference : anchor;
            Vector3 forward = viewReference.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = anchor.forward;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = transform.forward;

            forward.Normalize();

            float projectedDistance = Vector3.Dot(sourceTarget.position - viewReference.position, forward);
            float distance = projectedDistance > 0.05f ? projectedDistance : Vector3.Distance(anchor.position, sourceTarget.position);
            distance = Mathf.Max(distance, 0.5f);

            EnsureAimTargetProxy();
            _aimTargetProxy.SetPositionAndRotation(
                anchor.position + forward * distance,
                Quaternion.LookRotation(forward, viewReference.up));

            anchorTarget = _aimTargetProxy;
            return true;
        }

        private Vector3 GetCurrentAimPeekLocalOffset()
        {
            if (_aimPeek <= -0.001f)
                return aimPeekLeftLocalOffset * Mathf.Abs(_aimPeek);

            if (_aimPeek >= 0.001f)
                return aimPeekRightLocalOffset * Mathf.Abs(_aimPeek);

            return Vector3.zero;
        }

        private Transform GetCurrentAimPeekAnchor()
        {
            if (_aimPeek <= -0.001f)
                return aimPeekLeftAnchor;

            if (_aimPeek >= 0.001f)
                return aimPeekRightAnchor;

            return null;
        }

        private void RefreshAimPeekRuntimeReference()
        {
            if (aimPeekReferenceTransform != null)
            {
                _aimPeekRuntimeReference = aimPeekReferenceTransform;
                return;
            }

            if (aimControlledTransform != null)
            {
                _aimPeekRuntimeReference = aimControlledTransform;
                return;
            }

            if (_aimIK != null && _aimIK.solver.transform != null)
            {
                _aimPeekRuntimeReference = _aimIK.solver.transform;
                return;
            }

            _aimPeekRuntimeReference = transform;
        }

        private void EnsureAimTargetProxy()
        {
            if (_aimTargetProxy != null) return;
            _aimTargetProxy = CreateAux("__AimIKTargetProxy", hidden: true);
        }

        // ════════════════════════════════════════════════════════════════════════
        // 运行时 Apply — LookAt（热路径）
        // ════════════════════════════════════════════════════════════════════════

        private void ApplyLookAt(in StateGeneralFinalIKDriverPose pose)
        {
            if (Mathf.Abs(pose.lookAtLerpingRate - 1f) > 0.0001f)
                _lookAtLerpingRate = NormalizeLerpingRate(pose.lookAtLerpingRate);

            if (pose.lookAtWeight <= 0.001f && !HasActiveLookAtWeightSmoothing())
            {
                if (_lookAtIKReady && _lookAtIK.enabled)
                    _lookAtIK.enabled = false;
                if (_bipedIKReady)
                    _bipedIK.SetLookAtWeight(0f, 0f, 0f, 0f, 0f, 0f, 0f);
                return;
            }

            _lookAtWeightTarget = pose.lookAtWeight;
            _lookAtBodyWeightTarget = pose.lookAtBodyWeight;
            _lookAtHeadWeightTarget = pose.lookAtHeadWeight;
            _lookAtEyesWeightTarget = pose.lookAtEyesWeight;
            _lookAtClampWeightTarget = pose.lookAtClampWeight;

            float lookAtSmoothTime = ResolveLookAtWeightSmoothTime();
            _lookAtWeightCurrent = SmoothIKWeight(_lookAtWeightCurrent, _lookAtWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
            _lookAtBodyWeightCurrent = SmoothIKWeight(_lookAtBodyWeightCurrent, _lookAtBodyWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
            _lookAtHeadWeightCurrent = SmoothIKWeight(_lookAtHeadWeightCurrent, _lookAtHeadWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
            _lookAtEyesWeightCurrent = SmoothIKWeight(_lookAtEyesWeightCurrent, _lookAtEyesWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
            _lookAtClampWeightCurrent = SmoothIKWeight(_lookAtClampWeightCurrent, _lookAtClampWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);

            bool activeLookAt = HasActiveLookAtWeightSmoothing();

            if (_lookAtIKReady)
            {
                if (_lookAtIK.enabled != activeLookAt)
                    _lookAtIK.enabled = activeLookAt;

                if (!activeLookAt)
                {
                    if (_bipedIKReady)
                        _bipedIK.SetLookAtWeight(0f, 0f, 0f, 0f, 0f, 0f, 0f);
                    return;
                }

                _lookAtIK.solver.IKPosition       = pose.lookAtPosition;
                _lookAtIK.solver.IKPositionWeight = _lookAtWeightCurrent;
                _lookAtIK.solver.head.weight      = _lookAtHeadWeightCurrent;
                _lookAtIK.solver.eyesWeight       = _lookAtEyesWeightCurrent;
                _lookAtIK.solver.clampWeight      = _lookAtClampWeightCurrent;
                for (int i = 0; i < _lookAtSpineBoneCount; i++)
                    _lookAtSpineBones[i].weight = _lookAtBodyWeightCurrent;
                if (_bipedIKReady)
                    _bipedIK.SetLookAtWeight(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }
            else if (_bipedIKReady && pose.lookAtWeight > 0.001f)
            {
                _bipedIK.SetLookAtPosition(pose.lookAtPosition);
                _bipedIK.SetLookAtWeight(
                    _lookAtWeightCurrent,
                    _lookAtBodyWeightCurrent,
                    _lookAtHeadWeightCurrent,
                    _lookAtEyesWeightCurrent,
                    _lookAtClampWeightCurrent,
                    _lookAtClampWeightCurrent,
                    _lookAtClampWeightCurrent);
            }
            else if (_bipedIKReady)
            {
                _lookAtWeightTarget = 0f;
                _lookAtBodyWeightTarget = bipedLookAtDefaultBodyWeight;
                _lookAtHeadWeightTarget = bipedLookAtDefaultHeadWeight;
                _lookAtEyesWeightTarget = bipedLookAtDefaultEyesWeight;
                _lookAtClampWeightTarget = bipedLookAtDefaultClampWeight;
                _lookAtWeightCurrent = SmoothIKWeight(_lookAtWeightCurrent, _lookAtWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
                _lookAtBodyWeightCurrent = SmoothIKWeight(_lookAtBodyWeightCurrent, _lookAtBodyWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
                _lookAtHeadWeightCurrent = SmoothIKWeight(_lookAtHeadWeightCurrent, _lookAtHeadWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
                _lookAtEyesWeightCurrent = SmoothIKWeight(_lookAtEyesWeightCurrent, _lookAtEyesWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
                _lookAtClampWeightCurrent = SmoothIKWeight(_lookAtClampWeightCurrent, _lookAtClampWeightTarget, lookAtSmoothTime, _lookAtLerpingRate);
                _bipedIK.SetLookAtWeight(
                    _lookAtWeightCurrent,
                    _lookAtBodyWeightCurrent,
                    _lookAtHeadWeightCurrent,
                    _lookAtEyesWeightCurrent,
                    _lookAtClampWeightCurrent,
                    _lookAtClampWeightCurrent,
                    _lookAtClampWeightCurrent);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Goal Transform 管理
        // ════════════════════════════════════════════════════════════════════════

        private void EnsureHintTransforms()
        {
            if (_lhHint == null) _lhHint = preHintLH != null ? preHintLH : CreateAux("__IKHint_LH", hidden: true);
            if (_rhHint == null) _rhHint = preHintRH != null ? preHintRH : CreateAux("__IKHint_RH", hidden: true);
            if (_lfHint == null) _lfHint = preHintLF != null ? preHintLF : CreateAux("__IKHint_LF", hidden: true);
            if (_rfHint == null) _rfHint = preHintRF != null ? preHintRF : CreateAux("__IKHint_RF", hidden: true);
            _hintTargetsReady = _lhHint != null && _rhHint != null && _lfHint != null && _rfHint != null;
        }

        private void EnsureGoalTargetTransforms()
        {
            if (_lhTarget == null) _lhTarget = preGoalLH != null ? preGoalLH : CreateAux("__IKTarget_LH", hidden: false);
            if (_rhTarget == null) _rhTarget = preGoalRH != null ? preGoalRH : CreateAux("__IKTarget_RH", hidden: false);
            if (_lfTarget == null) _lfTarget = preGoalLF != null ? preGoalLF : CreateAux("__IKTarget_LF", hidden: false);
            if (_rfTarget == null) _rfTarget = preGoalRF != null ? preGoalRF : CreateAux("__IKTarget_RF", hidden: false);
            _goalTargetsReady = _lhTarget != null && _rhTarget != null && _lfTarget != null && _rfTarget != null;
            _goalTargetsInit = false;
            if (_goalTargetsReady)
                CacheGoalTargetSnapshot();
        }

        private void SeedGoalTargetsFromCurrentRigPose()
        {
            if (_goalTargetsInit) return;
            if (!_goalTargetsReady) return;

            SeedGoalTargetFromCurrentRigPose(_lhTarget, preGoalLH, _bipedIKReady ? _bipedIK.references.leftHand : null, HumanBodyBones.LeftHand);
            SeedGoalTargetFromCurrentRigPose(_rhTarget, preGoalRH, _bipedIKReady ? _bipedIK.references.rightHand : null, HumanBodyBones.RightHand);
            SeedGoalTargetFromCurrentRigPose(_lfTarget, preGoalLF, _bipedIKReady ? _bipedIK.references.leftFoot : null, HumanBodyBones.LeftFoot);
            SeedGoalTargetFromCurrentRigPose(_rfTarget, preGoalRF, _bipedIKReady ? _bipedIK.references.rightFoot : null, HumanBodyBones.RightFoot);

            _goalTargetsInit = true;
            CacheGoalTargetSnapshot();
        }

        private void SeedGoalTargetFromCurrentRigPose(Transform target, Transform presetTarget, Transform runtimeBone, HumanBodyBones fallbackBone)
        {
            if (target == null || presetTarget != null) return;

            var source = runtimeBone;
            if (source == null && _animator != null && _animator.isHuman)
                source = _animator.GetBoneTransform(fallbackBone);

            if (source == null) return;

            target.SetPositionAndRotation(source.position, source.rotation);
        }

        private void InitGoalTargetsOnce(in StateGeneralFinalIKDriverPose pose)
        {
            if (_goalTargetsInit) return;
            WriteTargetFromGoal(_lhTarget, in pose.leftHand);
            WriteTargetFromGoal(_rhTarget, in pose.rightHand);
            WriteTargetFromGoal(_lfTarget, in pose.leftFoot);
            WriteTargetFromGoal(_rfTarget, in pose.rightFoot);
            _goalTargetsInit = true;
        }

        private void SyncGoalTargets(in StateGeneralFinalIKDriverPose pose)
        {
            WriteTargetFromGoal(_lhTarget, in pose.leftHand);
            WriteTargetFromGoal(_rhTarget, in pose.rightHand);
            WriteTargetFromGoal(_lfTarget, in pose.leftFoot);
            WriteTargetFromGoal(_rfTarget, in pose.rightFoot);
        }

        private static void WriteTargetFromGoal(Transform t, in IKGoalPose goal)
        {
            if (goal.weight <= 0.001f) return;
            if ((t.position - goal.position).sqrMagnitude > 0.000001f) t.position = goal.position;
            if ((1f - Mathf.Abs(Quaternion.Dot(t.rotation, goal.rotation))) > 3.8e-7f) t.rotation = goal.rotation;
        }

        private bool AreGoalTargetsDirty()
        {
            const float P    = 0.000001f;
            const float RDot = 3.8e-7f;
            if ((_lhTarget.position - _sn_lhP).sqrMagnitude > P) return true;
            if ((1f - Mathf.Abs(Quaternion.Dot(_lhTarget.rotation, _sn_lhR))) > RDot) return true;
            if ((_rhTarget.position - _sn_rhP).sqrMagnitude > P) return true;
            if ((1f - Mathf.Abs(Quaternion.Dot(_rhTarget.rotation, _sn_rhR))) > RDot) return true;
            if ((_lfTarget.position - _sn_lfP).sqrMagnitude > P) return true;
            if ((1f - Mathf.Abs(Quaternion.Dot(_lfTarget.rotation, _sn_lfR))) > RDot) return true;
            if ((_rfTarget.position - _sn_rfP).sqrMagnitude > P) return true;
            if ((1f - Mathf.Abs(Quaternion.Dot(_rfTarget.rotation, _sn_rfR))) > RDot) return true;
            return false;
        }

        private void CacheGoalTargetSnapshot()
        {
            _sn_lhP = _lhTarget.position; _sn_lhR = _lhTarget.rotation;
            _sn_rhP = _rhTarget.position; _sn_rhR = _rhTarget.rotation;
            _sn_lfP = _lfTarget.position; _sn_lfR = _lfTarget.rotation;
            _sn_rfP = _rfTarget.position; _sn_rfR = _rfTarget.rotation;
        }

        private bool HasRuntimeBinding()
        {
            return _stateMachine != null && _animator != null;
        }

        private void SetDriverEnabled(bool active, string reason)
        {
            if (active && !HasRuntimeBinding())
            {
                _runtimeBindingReady = false;
                active = false;
                reason = $"拒绝启用：运行态引用未就绪 | {reason}";
            }
            else
            {
                _runtimeBindingReady = active;
            }

            _driverEnableStateSummary = active ? $"Enabled | {reason}" : $"Disabled | {reason}";

            if (Application.isPlaying)
            {
                if (active)
                {
                    Debug.Log($"[StateFinalIKDriver] 组件启用：{reason}", this);
                }
                else
                {
                    Debug.LogWarning($"[StateFinalIKDriver] 组件禁用：{reason}", this);
                }
            }

            _isInternalEnableStateChange = true;
            enabled = active;
        }

        private void ResetRuntimeLerpingRates()
        {
            _aimLerpingRate = 1f;
            _lookAtLerpingRate = 1f;
            _lhWeights.lerpingRate = 1f;
            _rhWeights.lerpingRate = 1f;
            _lfWeights.lerpingRate = 1f;
            _rfWeights.lerpingRate = 1f;
            _limbLerpingRate = 1f;
            _grounderLerpingRate = 1f;
        }

        private void UpdateLerpingRateRecovery()
        {
            _aimLerpingRate = RecoverLerpingRateToOne(_aimLerpingRate, ResolveAimLerpingRateRecoverTime());
            _lookAtLerpingRate = RecoverLerpingRateToOne(_lookAtLerpingRate, ResolveLookAtLerpingRateRecoverTime());
            _lhWeights.lerpingRate = RecoverLerpingRateToOne(_lhWeights.lerpingRate, ResolveLimbLerpingRateRecoverTime());
            _rhWeights.lerpingRate = RecoverLerpingRateToOne(_rhWeights.lerpingRate, ResolveLimbLerpingRateRecoverTime());
            _lfWeights.lerpingRate = RecoverLerpingRateToOne(_lfWeights.lerpingRate, ResolveLimbLerpingRateRecoverTime());
            _rfWeights.lerpingRate = RecoverLerpingRateToOne(_rfWeights.lerpingRate, ResolveLimbLerpingRateRecoverTime());
            _limbLerpingRate = RecoverLerpingRateToOne(_limbLerpingRate, ResolveLimbLerpingRateRecoverTime());
            _grounderLerpingRate = RecoverLerpingRateToOne(_grounderLerpingRate, ResolveGrounderLerpingRateRecoverTime());
        }

        private float RecoverLerpingRateToOne(float rate, float recoverTime)
        {
            float normalized = NormalizeLerpingRate(rate);
            recoverTime = Mathf.Max(0f, recoverTime);
            if (recoverTime <= 0.0001f)
                return 1f;

            float deltaTime = Mathf.Max(0f, _ikWeightSmoothDeltaTime);
            if (deltaTime <= 0f)
                return normalized;

            float t = 1f - Mathf.Exp(-deltaTime / recoverTime);
            return Mathf.Lerp(normalized, 1f, t);
        }

        private float ResolveAimWeightSmoothTime()
        {
            return aimWeightSmoothTime;
        }

        private float ResolveLookAtWeightSmoothTime()
        {
            return lookAtWeightSmoothTime;
        }

        private float ResolveLimbWeightSmoothTime()
        {
            return limbWeightSmoothTime;
        }

        private float ResolveGrounderWeightSmoothTime()
        {
            return grounderWeightSmoothTime;
        }

        private float ResolveAimLerpingRateRecoverTime()
        {
            return aimLerpingRateRecoverTime;
        }

        private float ResolveLookAtLerpingRateRecoverTime()
        {
            return lookAtLerpingRateRecoverTime;
        }

        private float ResolveLimbLerpingRateRecoverTime()
        {
            return limbLerpingRateRecoverTime;
        }

        private float ResolveGrounderLerpingRateRecoverTime()
        {
            return grounderLerpingRateRecoverTime;
        }

        private static float NormalizeLerpingRate(float rate)
        {
            return Mathf.Clamp(rate, 0.05f, 8f);
        }

        private float ResolveAimPoleWeight(float aimWeight)
        {
            return Mathf.Clamp01(aimPoleWeight) * Mathf.Clamp01(aimWeight);
        }

        private void SetAimRuntimeWeights(float aimWeight)
        {
            _aimWeightTarget = Mathf.Clamp01(aimWeight);
            _aimPoleWeightTarget = ResolveAimPoleWeight(_aimWeightTarget);
        }

        private void ResetRuntimeLimbWeights()
        {
            _lhWeights.Reset();
            _rhWeights.Reset();
            _lfWeights.Reset();
            _rfWeights.Reset();
        }

        private bool HasActiveBipedWeightSmoothing()
        {
            return HasActiveLimbWeightSmoothing(_lhWeights)
                || HasActiveLimbWeightSmoothing(_rhWeights)
                || HasActiveLimbWeightSmoothing(_lfWeights)
                || HasActiveLimbWeightSmoothing(_rfWeights);
        }

        private bool HasActiveLookAtWeightSmoothing()
        {
            return _lookAtWeightCurrent > 0.001f
                || _lookAtWeightTarget > 0.001f
                || _lookAtBodyWeightCurrent > 0.001f
                || _lookAtBodyWeightTarget > 0.001f
                || _lookAtHeadWeightCurrent > 0.001f
                || _lookAtHeadWeightTarget > 0.001f
                || _lookAtEyesWeightCurrent > 0.001f
                || _lookAtEyesWeightTarget > 0.001f
                || _lookAtClampWeightCurrent > 0.001f
                || _lookAtClampWeightTarget > 0.001f;
        }

        private static bool HasActiveLimbWeightSmoothing(LimbRuntimeWeights weights)
        {
            return weights.positionCurrent > 0.001f
                || weights.positionTarget > 0.001f
                || weights.rotationCurrent > 0.001f
                || weights.rotationTarget > 0.001f
                || Mathf.Abs(weights.bendCurrent - weights.bendTarget) > 0.001f;
        }

        private void ReadLimbRuntimeWeights(
            AvatarIKGoal avatarGoal,
            out float positionCurrent, out float positionTarget,
            out float rotationCurrent, out float rotationTarget,
            out float bendCurrent, out float bendTarget,
            out float lerpingRate)
        {
            LimbRuntimeWeights weights = avatarGoal switch
            {
                AvatarIKGoal.LeftHand => _lhWeights,
                AvatarIKGoal.RightHand => _rhWeights,
                AvatarIKGoal.LeftFoot => _lfWeights,
                _ => _rfWeights,
            };

            positionCurrent = weights.positionCurrent;
            positionTarget = weights.positionTarget;
            rotationCurrent = weights.rotationCurrent;
            rotationTarget = weights.rotationTarget;
            bendCurrent = weights.bendCurrent;
            bendTarget = weights.bendTarget;
            lerpingRate = weights.lerpingRate;
        }

        private void WriteLimbRuntimeWeights(
            AvatarIKGoal avatarGoal,
            float positionCurrent, float positionTarget,
            float rotationCurrent, float rotationTarget,
            float bendCurrent, float bendTarget,
            float lerpingRate)
        {
            var weights = new LimbRuntimeWeights
            {
                positionCurrent = positionCurrent,
                positionTarget = positionTarget,
                rotationCurrent = rotationCurrent,
                rotationTarget = rotationTarget,
                bendCurrent = bendCurrent,
                bendTarget = bendTarget,
                lerpingRate = lerpingRate,
            };

            switch (avatarGoal)
            {
                case AvatarIKGoal.LeftHand:
                    _lhWeights = weights;
                    break;
                case AvatarIKGoal.RightHand:
                    _rhWeights = weights;
                    break;
                case AvatarIKGoal.LeftFoot:
                    _lfWeights = weights;
                    break;
                default:
                    _rfWeights = weights;
                    break;
            }
        }

        private Transform CreateAux(string name, bool hidden)
        {
            var go = new GameObject(name)
            {
                hideFlags = hidden
                    ? HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
                    : HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor
            };
            go.transform.SetParent(transform, worldPositionStays: !hidden);
            return go.transform;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Pose 脏检测（值类型比较，无 GC）
        // ════════════════════════════════════════════════════════════════════════

        private bool PoseApproxEqual(in StateGeneralFinalIKDriverPose a, in StateGeneralFinalIKDriverPose b)
        {
            const float W = 0.0001f;
            const float P = 0.000001f;
            const float R = 0.000001f;
            if (!GoalApproxEqual(in a.leftHand,  in b.leftHand,  W, P, R)) return false;
            if (!GoalApproxEqual(in a.rightHand, in b.rightHand, W, P, R)) return false;
            if (!GoalApproxEqual(in a.leftFoot,  in b.leftFoot,  W, P, R)) return false;
            if (!GoalApproxEqual(in a.rightFoot, in b.rightFoot, W, P, R)) return false;
            if (Mathf.Abs(a.lookAtWeight     - b.lookAtWeight)     > W) return false;
            if ((a.lookAtPosition - b.lookAtPosition).sqrMagnitude > P) return false;
            if (_fullLookAtCompare)
            {
                if (Mathf.Abs(a.lookAtBodyWeight - b.lookAtBodyWeight)  > W) return false;
                if (Mathf.Abs(a.lookAtHeadWeight - b.lookAtHeadWeight)  > W) return false;
                if (Mathf.Abs(a.lookAtEyesWeight - b.lookAtEyesWeight)  > W) return false;
                if (Mathf.Abs(a.lookAtClampWeight- b.lookAtClampWeight) > W) return false;
            }
            return true;
        }

        private static bool GoalApproxEqual(in IKGoalPose a, in IKGoalPose b,
                                             float wE, float pE, float rE)
        {
            if (Mathf.Abs(a.weight - b.weight) > wE) return false;
            if (a.weight <= 0.001f && b.weight <= 0.001f)
            {
                return (a.hintPosition - b.hintPosition).sqrMagnitude <= pE;
            }
            if ((a.position - b.position).sqrMagnitude                    > pE) return false;
            if ((1f - Mathf.Abs(Quaternion.Dot(a.rotation, b.rotation))) > rE) return false;
            if ((a.hintPosition - b.hintPosition).sqrMagnitude            > pE) return false;
            return true;
        }
    }
}