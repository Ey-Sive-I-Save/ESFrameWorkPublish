using UnityEngine;
using System;

namespace ES
{
    /// <summary>
    /// 负责把 StateMachine 计算出的最终 IK Pose 驱动到 FinalIK(BipedIK)。
    /// - LateUpdate 执行：确保动画图(PlayableGraph)已在 Update 里 Evaluate
    /// - 手动调用 BipedIK.UpdateBipedIK：消除脚本执行顺序不确定
    /// - 热插拔：运行时添加/移除 BipedIK 组件可自动重新绑定
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public sealed class StateFinalIKDriver : MonoBehaviour
    {
        [Header("FinalIK 绑定策略")]
        [Tooltip("true: 当BipedIK.references缺失时，尝试自动识别(仅在缺失时执行，不会覆盖已填写引用)。")]
        [SerializeField] private bool autoDetectReferencesIfMissing = true;

        [Tooltip("true: 当finalIKPose已有权重但BipedIK不可用时，输出节流警告，帮助定位‘写了pose但没表现’的问题。")]
        [SerializeField] private bool warnWhenPoseHasWeightButNoIK = true;

        [Tooltip("绑定/重试的节流间隔(秒)，避免每帧反复TryBind。")]
        [SerializeField] private float rebindInterval = 0.5f;

        [Header("IK 输出兜底(稳定优先)")]
        [Range(0f, 1f)]
        [Tooltip("脚部旋转权重倍率。0=脚只驱动位置不驱动旋转(最稳，商业常用兜底，避免走路扭曲/自旋)。\n建议：先用0确认稳定，再逐步调到0.1~0.3，最后到1。")]
        [SerializeField] private float footRotationWeightMultiplier = 0.2f;

        [Header("目标Transform(可视化/手动调试)")]
        [Tooltip("true: 每次输入pose变化时，把pose写入目标Transform（用于可视化）。false: 不覆盖目标Transform，让你手动拖动它们作为输入。")]
        [SerializeField] private bool driveGoalTargetsFromPose = true;

        private StateMachine _stateMachine;
        private Animator _animator;
        private readonly FinalIKBipedIKBridge _bridge = new FinalIKBipedIKBridge();

        // 性能优化：
        // - Apply() 会做大量 SetIK* 写入；这些写入只有在目标/权重变化时才需要。
        // - 但 UpdateSolver()（UpdateBipedIK）在 IK 激活时必须每帧跑：因为动画每帧会覆盖骨骼，
        //   如果不每帧求解，当前帧就会回到纯动画姿态。
        private StateIKPose _lastAppliedPose;
        private bool _hasLastAppliedPose;
        private bool _wasBound;

        private float _lastWarnTime;
        private const float WarnInterval = 2.0f;

        private float _lastBindTryTime;
        private int _bindTryCount;
        private int _bindSuccessCount;
        private int _applyCount;
        private int _solverUpdateCount;
        private float _lastApplyTime;
        private float _lastSolverUpdateTime;

        private Transform _leftHandHint;
        private Transform _rightHandHint;
        private Transform _leftFootHint;
        private Transform _rightFootHint;

        private Transform _leftHandTarget;
        private Transform _rightHandTarget;
        private Transform _leftFootTarget;
        private Transform _rightFootTarget;

        private bool _targetsInitialized;

        private Vector3 _lastLeftHandTargetPos;
        private Quaternion _lastLeftHandTargetRot;
        private Vector3 _lastRightHandTargetPos;
        private Quaternion _lastRightHandTargetRot;
        private Vector3 _lastLeftFootTargetPos;
        private Quaternion _lastLeftFootTargetRot;
        private Vector3 _lastRightFootTargetPos;
        private Quaternion _lastRightFootTargetRot;

        private void Awake()
        {
            // 约定：必须由 StateMachine.BindToAnimator 显式 Bind 后才运行。
            // 默认禁用，避免因执行顺序导致的“未绑定就 LateUpdate”。
            enabled = false;
        }

        internal void Bind(StateMachine machine, Animator animator)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (machine == null) throw new ArgumentNullException(nameof(machine));
            if (animator == null) throw new ArgumentNullException(nameof(animator));
#endif

            _stateMachine = machine;
            _animator = animator;

            enabled = true;

            EnsureHintTransforms();
            EnsureGoalTargetTransformsIfNeeded();

            _bridge.AutoDetectReferencesIfMissing = autoDetectReferencesIfMissing;
            _bridge.FootRotationWeightMultiplier = footRotationWeightMultiplier;
            _bridge.TryBind(_animator);

            _bindTryCount++;
            if (_bridge.IsReady) _bindSuccessCount++;

            _wasBound = _bridge.IsBound;
            _hasLastAppliedPose = false;
        }

        internal void Unbind()
        {
            _stateMachine = null;
            _animator = null;
            _hasLastAppliedPose = false;
            _wasBound = false;
            _targetsInitialized = false;
            enabled = false;
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_stateMachine == null || _animator == null)
            {
                throw new InvalidOperationException("[StateFinalIKDriver] 未绑定就进入 LateUpdate：请通过 StateMachine.BindToAnimator 调用 Bind()，或先禁用该组件。");
            }
#else
            if (_stateMachine == null || _animator == null)
            {
                // Release：避免刷异常/刷日志，直接停掉驱动。
                enabled = false;
                return;
            }
#endif
            if (!_stateMachine.isRunning) return;

            // 热插拔：BipedIK 可能在运行时被加上/移除
            if (!_bridge.IsReady)
            {
                if (rebindInterval <= 0f || (Time.unscaledTime - _lastBindTryTime) >= rebindInterval)
                {
                    _lastBindTryTime = Time.unscaledTime;
                    _bridge.AutoDetectReferencesIfMissing = autoDetectReferencesIfMissing;
                    _bridge.FootRotationWeightMultiplier = footRotationWeightMultiplier;
                    _bridge.TryBind(_animator);
                    _bindTryCount++;
                    if (_bridge.IsReady) _bindSuccessCount++;
                }
            }

            ref var pose = ref _stateMachine.finalIKPose;

            if (!_bridge.IsReady)
            {
                // pose 已经有权重但没法输出到FinalIK：给出明确提示（节流，避免刷屏）
                if (warnWhenPoseHasWeightButNoIK && pose.HasAnyWeight && (Time.unscaledTime - _lastWarnTime) >= WarnInterval)
                {
                    _lastWarnTime = Time.unscaledTime;
                    var err = _bridge.LastBindError;
                    if (string.IsNullOrEmpty(err)) err = "FinalIK(BipedIK) 未就绪";
                    Debug.LogWarning($"[StateFinalIKDriver] finalIKPose已有权重，但无法驱动FinalIK：{err}. 解决：在Animator同物体上添加 BipedIK 组件，并确保References可用。", _animator);
                }
                return;
            }

            // 如果是“新绑定/重新绑定”，强制认为输入脏一次（需要重新 Apply 一遍）
            if (!_wasBound && _bridge.IsReady)
            {
                _hasLastAppliedPose = false;
            }
            _wasBound = _bridge.IsReady;

            if (!pose.HasAnyWeight)
            {
                // 从“有IK”切到“无IK”：只需要清一次 defaults（避免每帧重复 SetToDefaults）。
                if (_hasLastAppliedPose)
                {
                    _bridge.ResetToDefaults();
                    _hasLastAppliedPose = false;
                }

                return;
            }

            // IK 激活时：仅当输入变化时才 Apply（写 SetIK*），但每帧仍要求解覆盖动画。
            bool dirty = !_hasLastAppliedPose || !PoseApproximatelyEqual(in _lastAppliedPose, in pose);

            // “总体使用Transform”模式：当你手动拖动目标点时，即便pose没变也要重新Apply
            if (!driveGoalTargetsFromPose)
            {
                EnsureGoalTargetTransformsIfNeeded();
                if (AreGoalTargetsDirty())
                {
                    dirty = true;
                }
            }
            if (dirty)
            {
                EnsureGoalTargetTransformsIfNeeded();
                // 安全初始化：即便你想手动拖动目标点，也要先把它们放到当前pose上，避免一开始在原点导致“瞬移”。
                if (!_targetsInitialized)
                {
                    SetTargetFromPose(_leftHandTarget, pose.leftHand);
                    SetTargetFromPose(_rightHandTarget, pose.rightHand);
                    SetTargetFromPose(_leftFootTarget, pose.leftFoot);
                    SetTargetFromPose(_rightFootTarget, pose.rightFoot);
                    _targetsInitialized = true;
                }

                if (driveGoalTargetsFromPose)
                {
                    SetTargetFromPose(_leftHandTarget, pose.leftHand);
                    SetTargetFromPose(_rightHandTarget, pose.rightHand);
                    SetTargetFromPose(_leftFootTarget, pose.leftFoot);
                    SetTargetFromPose(_rightFootTarget, pose.rightFoot);
                }

                _bridge.Apply(
                    _animator,
                    pose,
                    _leftHandHint,
                    _rightHandHint,
                    _leftFootHint,
                    _rightFootHint,
                    _leftHandTarget,
                    _rightHandTarget,
                    _leftFootTarget,
                    _rightFootTarget);
                _lastAppliedPose = pose;
                _hasLastAppliedPose = true;
                _applyCount++;
                _lastApplyTime = Time.unscaledTime;

                CacheGoalTargetsSnapshot();
            }

            _bridge.UpdateSolver();
            _solverUpdateCount++;
            _lastSolverUpdateTime = Time.unscaledTime;
        }

        // ===== 监控用只读信息（给Inspector/Editor面板用） =====
        public bool IsReady => _bridge.IsReady;
        public bool IsBound => _bridge.IsBound;
        public string LastBindError => _bridge.LastBindError;
        public int BindTryCount => _bindTryCount;
        public int BindSuccessCount => _bindSuccessCount;
        public int ApplyCount => _applyCount;
        public int SolverUpdateCount => _solverUpdateCount;
        public float LastApplyTime => _lastApplyTime;
        public float LastSolverUpdateTime => _lastSolverUpdateTime;

        public bool HasPoseWeight
        {
            get
            {
                if (_stateMachine == null) return false;
                return _stateMachine.finalIKPose.HasAnyWeight;
            }
        }

        public StateIKPose CurrentPose
        {
            get
            {
                if (_stateMachine == null) return default;
                return _stateMachine.finalIKPose;
            }
        }

        private static bool PoseApproximatelyEqual(in StateIKPose a, in StateIKPose b)
        {
            const float WeightEps = 0.0001f;
            const float PosEpsSqr = 0.000001f; // 1mm^2
            const float RotDotEps = 0.000001f;

            if (!GoalApproximatelyEqual(in a.leftHand, in b.leftHand, WeightEps, PosEpsSqr, RotDotEps)) return false;
            if (!GoalApproximatelyEqual(in a.rightHand, in b.rightHand, WeightEps, PosEpsSqr, RotDotEps)) return false;
            if (!GoalApproximatelyEqual(in a.leftFoot, in b.leftFoot, WeightEps, PosEpsSqr, RotDotEps)) return false;
            if (!GoalApproximatelyEqual(in a.rightFoot, in b.rightFoot, WeightEps, PosEpsSqr, RotDotEps)) return false;

            if (Mathf.Abs(a.lookAtWeight - b.lookAtWeight) > WeightEps) return false;
            if ((a.lookAtPosition - b.lookAtPosition).sqrMagnitude > PosEpsSqr) return false;
            if (Mathf.Abs(a.lookAtBodyWeight - b.lookAtBodyWeight) > WeightEps) return false;
            if (Mathf.Abs(a.lookAtHeadWeight - b.lookAtHeadWeight) > WeightEps) return false;
            if (Mathf.Abs(a.lookAtEyesWeight - b.lookAtEyesWeight) > WeightEps) return false;
            if (Mathf.Abs(a.lookAtClampWeight - b.lookAtClampWeight) > WeightEps) return false;

            return true;
        }

        private static bool GoalApproximatelyEqual(
            in IKGoalPose a,
            in IKGoalPose b,
            float weightEps,
            float posEpsSqr,
            float rotDotEps)
        {
            if (Mathf.Abs(a.weight - b.weight) > weightEps) return false;

            // 权重接近 0 时，不必比较 position/rotation（求解器不会用）
            if (a.weight <= 0.001f && b.weight <= 0.001f)
            {
                // 但 hintPosition 会影响 bend goal（有些情况下权重很小仍可能被用到），这里仍做轻量比较
                if ((a.hintPosition - b.hintPosition).sqrMagnitude > posEpsSqr) return false;
                return true;
            }

            if ((a.position - b.position).sqrMagnitude > posEpsSqr) return false;
            if (!QuaternionApproximatelyEqual(a.rotation, b.rotation, rotDotEps)) return false;
            if ((a.hintPosition - b.hintPosition).sqrMagnitude > posEpsSqr) return false;
            return true;
        }

        private static bool QuaternionApproximatelyEqual(Quaternion a, Quaternion b, float dotEps)
        {
            // q 与 -q 表示同一旋转，所以取 abs(dot)
            float dot = Mathf.Abs(Quaternion.Dot(a, b));
            return (1f - dot) <= dotEps;
        }

        private void EnsureHintTransforms()
        {
            if (_leftHandHint != null) return;

            _leftHandHint = CreateHint("__FinalIKHint_LeftHand");
            _rightHandHint = CreateHint("__FinalIKHint_RightHand");
            _leftFootHint = CreateHint("__FinalIKHint_LeftFoot");
            _rightFootHint = CreateHint("__FinalIKHint_RightFoot");
        }

        private void EnsureGoalTargetTransformsIfNeeded()
        {
            if (_leftHandTarget != null) return;

            _leftHandTarget = CreateTarget("__FinalIKTarget_LeftHand");
            _rightHandTarget = CreateTarget("__FinalIKTarget_RightHand");
            _leftFootTarget = CreateTarget("__FinalIKTarget_LeftFoot");
            _rightFootTarget = CreateTarget("__FinalIKTarget_RightFoot");

            _targetsInitialized = false;
            CacheGoalTargetsSnapshot();
        }

        private bool AreGoalTargetsDirty()
        {
            // 仅在目标Transform存在时比较
            if (_leftHandTarget == null || _rightHandTarget == null || _leftFootTarget == null || _rightFootTarget == null) return false;

            const float PosEpsSqr = 0.000001f;
            const float RotAngleEps = 0.1f;

            if ((_leftHandTarget.position - _lastLeftHandTargetPos).sqrMagnitude > PosEpsSqr) return true;
            if (Quaternion.Angle(_leftHandTarget.rotation, _lastLeftHandTargetRot) > RotAngleEps) return true;

            if ((_rightHandTarget.position - _lastRightHandTargetPos).sqrMagnitude > PosEpsSqr) return true;
            if (Quaternion.Angle(_rightHandTarget.rotation, _lastRightHandTargetRot) > RotAngleEps) return true;

            if ((_leftFootTarget.position - _lastLeftFootTargetPos).sqrMagnitude > PosEpsSqr) return true;
            if (Quaternion.Angle(_leftFootTarget.rotation, _lastLeftFootTargetRot) > RotAngleEps) return true;

            if ((_rightFootTarget.position - _lastRightFootTargetPos).sqrMagnitude > PosEpsSqr) return true;
            if (Quaternion.Angle(_rightFootTarget.rotation, _lastRightFootTargetRot) > RotAngleEps) return true;

            return false;
        }

        private void CacheGoalTargetsSnapshot()
        {
            if (_leftHandTarget != null)
            {
                _lastLeftHandTargetPos = _leftHandTarget.position;
                _lastLeftHandTargetRot = _leftHandTarget.rotation;
            }
            if (_rightHandTarget != null)
            {
                _lastRightHandTargetPos = _rightHandTarget.position;
                _lastRightHandTargetRot = _rightHandTarget.rotation;
            }
            if (_leftFootTarget != null)
            {
                _lastLeftFootTargetPos = _leftFootTarget.position;
                _lastLeftFootTargetRot = _leftFootTarget.rotation;
            }
            if (_rightFootTarget != null)
            {
                _lastRightFootTargetPos = _rightFootTarget.position;
                _lastRightFootTargetRot = _rightFootTarget.rotation;
            }
        }

        private void SetTargetFromPose(Transform target, IKGoalPose goal)
        {
            if (target == null) return;
            if (goal.weight <= 0.001f) return;

            // 只在变化明显时才写，避免编辑器下Transform刷写过多
            if ((target.position - goal.position).sqrMagnitude > 0.000001f)
            {
                target.position = goal.position;
            }

            if (Quaternion.Angle(target.rotation, goal.rotation) > 0.1f)
            {
                target.rotation = goal.rotation;
            }
        }

        private Transform CreateHint(string name)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            go.transform.SetParent(transform, worldPositionStays: false);
            return go.transform;
        }

        private Transform CreateTarget(string name)
        {
            var go = new GameObject(name);
            // 目标点需要能在Hierarchy里被选中/拖动，所以不HideInHierarchy
            go.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            go.transform.SetParent(transform, worldPositionStays: true);
            return go.transform;
        }

        // 给Editor面板/外部调试读取
        public bool DriveGoalTargetsFromPose => driveGoalTargetsFromPose;
        public Transform LeftHandTarget => _leftHandTarget;
        public Transform RightHandTarget => _rightHandTarget;
        public Transform LeftFootTarget => _leftFootTarget;
        public Transform RightFootTarget => _rightFootTarget;
    }
}
