using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 基础台阶脚贴合模块（FinalIK输出链路）
    ///
    /// 目标：
    /// - 加入该模块后，在常态移动（走路/跑步）时，左右脚自动贴合台阶/地面高度与法线。
    /// - 不破坏原有功能：不修改任何状态配置；仅在 finalIKPose 输出阶段做“可选后处理”。
    ///
    /// 工作方式：
    /// - 订阅 StateMachine.OnFinalIKPosePostProcess
    /// - 当当前状态没有主动驱动脚IK时（pose.leftFoot/rightFoot 权重≈0），本模块才写入脚IK
    /// - 使用 SphereCast/Raycast 探测脚下地面点与法线，生成脚的IK position/rotation
    ///
    /// 注意：
    /// - 本模块不会强行覆盖其它状态（例如攀爬/跳跃/特殊技能）对脚IK的控制。
    /// - 若你希望在某些状态中仍然启用脚贴合，可把 overrideExistingFootIK 打开。
    /// </summary>
    [Serializable, TypeRegistryItem("基础台阶脚贴合模块")]
    public sealed class EntityBasicFootPlacementModule : EntityBasicModuleBase
    {
        private const string GroupName = "台阶贴合";

        public bool IsSubscribedForDebug => _subscribed;
        public int DebugPostProcessCalls => _debugPostProcessCalls;
        public int DebugAppliedCount => _debugAppliedCount;
        public float DebugLastCallbackTime => _debugLastCallbackTime;
        public string DebugLastSkipReason => _debugLastSkipReason;

        [BoxGroup(GroupName), Title("运行时状态"), LabelText("调试运行时状态"), Tooltip("开启后：记录该模块是否真正进入回调、是否写入pose、以及最后一次跳过原因（仅用于排查）。")]
        public bool debugRuntimeState = false;

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("已订阅回调")]
        private bool Debug_IsSubscribed => _subscribed;

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("回调次数")]
        private int Debug_PostProcessCalls => _debugPostProcessCalls;

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("写入次数")]
        private int Debug_AppliedCount => _debugAppliedCount;

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("最后回调时间")]
        private float Debug_LastCallbackTime => _debugLastCallbackTime;

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("最后跳过原因")]
        private string Debug_LastSkipReason => _debugLastSkipReason;

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("存在FinalIK驱动")]
        private bool Debug_HasFinalIKDriver
        {
            get
            {
                var a = _animator;
                if (a == null) return false;
                return a.GetComponent<StateFinalIKDriver>() != null;
            }
        }

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("存在BipedIK组件")]
        private bool Debug_HasBipedIK
        {
            get
            {
                var a = _animator;
                if (a == null) return false;
                return a.GetComponent<RootMotion.FinalIK.BipedIK>() != null;
            }
        }

        [BoxGroup(GroupName), ShowInInspector, ReadOnly, LabelText("BipedIK引用齐全")]
        private bool Debug_BipedIKReferencesFilled
        {
            get
            {
                var a = _animator;
                if (a == null) return false;
                var b = a.GetComponent<RootMotion.FinalIK.BipedIK>();
                return b != null && b.references != null && b.references.isFilled;
            }
        }

        [Title("开关")]
        [BoxGroup(GroupName)]
        public bool enableFootPlacement = true;

        [LabelText("仅在地面"), Tooltip("开启后：只有角色稳定在地面时才启用脚贴合（推荐）")]
        [BoxGroup(GroupName)]
        public bool onlyWhenGrounded = true;

        [LabelText("仅在移动"), Tooltip("开启后：只有角色水平速度超过阈值时才启用脚贴合")]
        [BoxGroup(GroupName)]
        public bool onlyWhenMoving = false;

        [LabelText("移动阈值"), Tooltip("onlyWhenMoving=true 时生效：水平速度小于该值则不贴合")]
        [BoxGroup(GroupName)]
        public float minMoveSpeed = 0.15f;

        [LabelText("使用角色Up轴"), Tooltip("开启后：cast方向/速度平面/台阶高度判断都基于角色自身Up轴（更好支持斜坡与角色倾斜）。")]
        [BoxGroup(GroupName)]
        [HideInInspector]
        public bool useCharacterUpAxis = true;

        [LabelText("斜坡支持(用地面法线)"), Tooltip("开启后：移动速度阈值判断使用地面法线的切平面（对斜坡更准确），与角色是否倾斜无关。")]
        [BoxGroup(GroupName)]
        public bool useGroundNormalPlaneForMoveCheck = true;

        [Title("检测")]
        [LabelText("地面层")]
        [BoxGroup(GroupName)]
        public LayerMask groundLayers = ~0;

        [LabelText("向上偏移"), Tooltip("从脚骨位置向上抬多少再往下cast，避免从地面内部起射")]
        [BoxGroup(GroupName)]
        public float castUp = 0.25f;

        [LabelText("向下距离"), Tooltip("向下检测的最大距离（决定能贴合多高台阶/多深落差）")]
        [BoxGroup(GroupName)]
        public float castDown = 0.7f;

        [LabelText("球半径"), Tooltip("SphereCast 半径（0=Raycast）。大一点更稳定，但可能贴到边缘")]
        [BoxGroup(GroupName)]
        public float sphereRadius = 0.06f;

        [Title("台阶限制")]
        [LabelText("最大上台阶"), Tooltip("命中点相对脚骨的最大上抬高度，超出则不贴合（避免突然抬到很高的平台）")]
        [BoxGroup(GroupName)]
        public float maxStepUp = 0.35f;

        [LabelText("最大下落差"), Tooltip("命中点相对脚骨的最大下落高度，超出则不贴合（避免贴到很远的下层地面）")]
        [BoxGroup(GroupName)]
        public float maxStepDown = 0.55f;

        [Title("输出")]
        [Range(0f, 1f)]
        [LabelText("IK权重"), Tooltip("脚贴合的最终输出权重（0=不贴合，1=完全贴合）")]
        [BoxGroup(GroupName)]
        public float footIKWeight = 1f;

        [Title("动画驱动(支撑脚/相位)")]
        [LabelText("支撑share参数名"), Tooltip("从StateMachine读取支撑脚share参数：0=右脚支撑，1=左脚支撑。取不到(<0)则回退纯自动。")]
        [BoxGroup(GroupName)]
        public string supportShareParamName = "FootSupportShare";

        [Range(0f, 1f)]
        [LabelText("支撑share混合"), Tooltip("0=纯自动(高点优先/命中优先)，1=纯动画/状态机share驱动。")]
        [BoxGroup(GroupName)]
        public float supportShareBlend = 0f;

        [LabelText("share变化率限制"), Tooltip("0=不限制；>0 则限制最终share每秒最大变化量，避免相位抖动导致左右权重来回跳。")]
        [BoxGroup(GroupName)]
        public float supportShareMaxDeltaPerSec = 0f;

        [LabelText("高点优先范围"), Tooltip("当左右脚都命中时，命中点高度差达到该范围(米)会明显偏向更高的那只脚。越小越敏感(可能抖动)，越大越保守。")]
        [BoxGroup(GroupName)]
        public float higherFootPreferenceRange = 0.12f;

        [LabelText("脚抬离地"), Tooltip("命中点沿法线抬起的偏移（防止脚穿地）")]
        [BoxGroup(GroupName)]
        public float footSurfaceOffset = 0.02f;

        [LabelText("对齐旋转"), Tooltip("开启后：脚旋转会对齐地面法线；关闭则只贴高度")]
        [BoxGroup(GroupName)]
        public bool alignRotationToGround = true;

        [LabelText("覆盖已有脚IK"), Tooltip("开启后：即便其它状态已经在控制脚IK，本模块也会覆盖（不推荐，默认关闭）")]
        [BoxGroup(GroupName)]
        public bool overrideExistingFootIK = false;

        [Title("平滑")]
        [LabelText("位置平滑"), Tooltip("越小越跟脚，越大越稳定")]
        [BoxGroup(GroupName)]
        public float positionSmoothTime = 0.06f;

        [LabelText("旋转跟随"), Tooltip("旋转插值速度（越大越跟脚）")]
        [BoxGroup(GroupName)]
        public float rotationLerpSpeed = 18f;

        [LabelText("权重平滑"), Tooltip("贴合权重的SmoothDamp时间")]
        [BoxGroup(GroupName)]
        public float weightSmoothTime = 0.05f;

        [Title("摆腿抑制（防止抬腿时硬贴）")]
        [LabelText("启用抑制")]
        [BoxGroup(GroupName)]
        public bool suppressWhenFootRising = true;

        [LabelText("上抬速度阈值"), Tooltip("脚骨的Y速度超过该值则认为在抬腿，临时降低贴合")]
        [BoxGroup(GroupName)]
        public float risingVelocityThreshold = 0.6f;

        [LabelText("抬腿高度阈值"), Tooltip("脚骨高出命中点超过该值则认为在摆腿，临时降低贴合")]
        [BoxGroup(GroupName)]
        public float risingHeightThreshold = 0.08f;

        [NonSerialized] private StateMachine _sm;
        [NonSerialized] private Animator _animator;
        [NonSerialized] private bool _subscribed;

        [NonSerialized] private Transform _leftFootBone;
        [NonSerialized] private Transform _rightFootBone;

        [NonSerialized] private Vector3 _leftPosVel;
        [NonSerialized] private Vector3 _rightPosVel;

        [NonSerialized] private Vector3 _leftTargetPos;
        [NonSerialized] private Vector3 _rightTargetPos;

        [NonSerialized] private Quaternion _leftTargetRot = Quaternion.identity;
        [NonSerialized] private Quaternion _rightTargetRot = Quaternion.identity;

        // 权重采用“互补”策略：leftWeight + rightWeight = totalWeight
        [NonSerialized] private float _totalWeight;
        [NonSerialized] private float _totalWeightVel;
        [NonSerialized] private float _leftShare = 0.5f;
        [NonSerialized] private float _leftShareVel;

        [NonSerialized] private float _leftWeight;
        [NonSerialized] private float _rightWeight;

        [NonSerialized] private Vector3 _leftFootLastBonePos;
        [NonSerialized] private Vector3 _rightFootLastBonePos;
        [NonSerialized] private bool _hasLastBonePos;

        [NonSerialized] private float _leftLastHeightUp;
        [NonSerialized] private float _rightLastHeightUp;
        [NonSerialized] private bool _hasLeftLastHeightUp;
        [NonSerialized] private bool _hasRightLastHeightUp;

        // ===== 统计与曲线（仅用于调试可观测性） =====
        private const int DebugHistoryCapacity = 180;

        [NonSerialized] private int _debugHistoryHead;
        [NonSerialized] private int _debugHistoryCount;

        [NonSerialized] private float[] _debugHistTotal;
        [NonSerialized] private float[] _debugHistShare;
        [NonSerialized] private float[] _debugHistLeft;
        [NonSerialized] private float[] _debugHistRight;
        [NonSerialized] private float[] _debugHistAnimShare;
        [NonSerialized] private float[] _debugHistUsedAnim;

        [NonSerialized] private float _debugLastAnimShareInput = -1f;
        [NonSerialized] private bool _debugLastUsedAnimShare;

        [NonSerialized] private int _debugRejectNoHit;
        [NonSerialized] private int _debugRejectStepUp;
        [NonSerialized] private int _debugRejectStepDown;
        [NonSerialized] private int _debugRejectNotGrounded;
        [NonSerialized] private int _debugRejectNotMoving;
        [NonSerialized] private int _debugRejectSuppressedLeft;
        [NonSerialized] private int _debugRejectSuppressedRight;
        [NonSerialized] private int _debugRejectExistingIK;
        [NonSerialized] private string _debugLastRejectReason;

        public int DebugHistoryCount => _debugHistoryCount;
        public int DebugHistoryCapacityValue => DebugHistoryCapacity;
        public int DebugHistoryHead => _debugHistoryHead;
        public float[] DebugHistTotal => _debugHistTotal;
        public float[] DebugHistShare => _debugHistShare;
        public float[] DebugHistLeft => _debugHistLeft;
        public float[] DebugHistRight => _debugHistRight;
        public float[] DebugHistAnimShare => _debugHistAnimShare;
        public float[] DebugHistUsedAnim => _debugHistUsedAnim;
        public float DebugLastAnimShareInput => _debugLastAnimShareInput;
        public bool DebugLastUsedAnimShare => _debugLastUsedAnimShare;
        public int DebugRejectNoHit => _debugRejectNoHit;
        public int DebugRejectStepUp => _debugRejectStepUp;
        public int DebugRejectStepDown => _debugRejectStepDown;
        public int DebugRejectNotGrounded => _debugRejectNotGrounded;
        public int DebugRejectNotMoving => _debugRejectNotMoving;
        public int DebugRejectSuppressedLeft => _debugRejectSuppressedLeft;
        public int DebugRejectSuppressedRight => _debugRejectSuppressedRight;
        public int DebugRejectExistingIK => _debugRejectExistingIK;
        public string DebugLastRejectReason => _debugLastRejectReason;

        public void DebugResetInstrumentation()
        {
            _debugHistoryHead = 0;
            _debugHistoryCount = 0;

            _debugLastAnimShareInput = -1f;
            _debugLastUsedAnimShare = false;

            _debugRejectNoHit = 0;
            _debugRejectStepUp = 0;
            _debugRejectStepDown = 0;
            _debugRejectNotGrounded = 0;
            _debugRejectNotMoving = 0;
            _debugRejectSuppressedLeft = 0;
            _debugRejectSuppressedRight = 0;
            _debugRejectExistingIK = 0;
            _debugLastRejectReason = string.Empty;
        }

        [NonSerialized] private int _debugPostProcessCalls;
        [NonSerialized] private int _debugAppliedCount;
        [NonSerialized] private float _debugLastCallbackTime;
        [NonSerialized] private string _debugLastSkipReason;

        [Button("应用推荐参数"), PropertyOrder(-100)]
        [BoxGroup(GroupName)]
        public void ApplyRecommendedDefaults()
        {
            // 你提出的“常用参数建议”作为默认推荐：
            enableFootPlacement = true;
            onlyWhenGrounded = true;
            onlyWhenMoving = false;
            minMoveSpeed = 0.15f;

            // 动画/状态机驱动（默认不改变现有行为：blend=0 纯自动）
            supportShareParamName = "FootSupportShare";
            supportShareBlend = 0f;
            supportShareMaxDeltaPerSec = 0f;

            sphereRadius = 0.06f;
            maxStepUp = 0.35f;
            maxStepDown = 0.55f;

            positionSmoothTime = 0.06f;
            rotationLerpSpeed = 18f;
            footSurfaceOffset = 0.02f;
        }

        public override void Start()
        {
            base.Start();
            TryBind();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            TryBind();
        }

        protected override void OnDisable()
        {
            Unbind();
            base.OnDisable();
        }

        public override void OnDestroy()
        {
            Unbind();
            base.OnDestroy();
        }

        protected override void Update()
        {
            // 模块可能在运行时启用/热插拔：确保绑定稳定
            if (!_subscribed)
            {
                TryBind();
            }
        }

        private void TryBind()
        {
            if (MyCore == null) return;

            // StateMachine 由状态域直接持有：这里直接取即可，不需要额外“访问模块”。
            _sm = MyCore.stateDomain != null ? MyCore.stateDomain.stateMachine : null;
            if (_sm == null) return;

            _animator = _sm.BoundAnimator != null ? _sm.BoundAnimator : MyCore.animator;
            if (_animator == null) return;

            CacheBonesIfNeeded();

            if (!_subscribed)
            {
                _sm.OnFinalIKPosePostProcess += OnFinalIKPosePostProcess;
                _subscribed = true;
            }
        }

        private void Unbind()
        {
            if (_subscribed && _sm != null)
            {
                _sm.OnFinalIKPosePostProcess -= OnFinalIKPosePostProcess;
            }

            _subscribed = false;
            _sm = null;
            _animator = null;
        }

        private void CacheBonesIfNeeded()
        {
            if (_animator == null) return;
            if (_leftFootBone != null && _rightFootBone != null) return;
            if (!_animator.isHuman) return;

            _leftFootBone = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightFootBone = _animator.GetBoneTransform(HumanBodyBones.RightFoot);

            if (_leftFootBone != null)
            {
                _leftTargetPos = _leftFootBone.position;
                _leftTargetRot = _leftFootBone.rotation;
            }

            if (_rightFootBone != null)
            {
                _rightTargetPos = _rightFootBone.position;
                _rightTargetRot = _rightFootBone.rotation;
            }

            _hasLastBonePos = false;
        }

        private void OnFinalIKPosePostProcess(StateMachine machine, ref StateIKPose pose, float deltaTime)
        {
            Debug_OnCallbackEnter();
            if (!enableFootPlacement) { Debug_Skip("开关关闭"); return; }
            if (MyCore == null) { Debug_Skip("MyCore为空（未绑定实体核心）"); return; }
            if (machine == null || machine.BoundAnimator == null) { Debug_Skip("StateMachine或Animator为空"); return; }

            if (_animator != machine.BoundAnimator)
            {
                _animator = machine.BoundAnimator;
                _leftFootBone = null;
                _rightFootBone = null;
                CacheBonesIfNeeded();
            }

            if (_leftFootBone == null || _rightFootBone == null) { Debug_Skip("未找到脚骨（非Humanoid或骨骼缺失）"); return; }

            if (onlyWhenGrounded && !IsGroundedForFootPlacement())
            {
                FadeOutWeights(deltaTime);
                Debug_OnRejectNotGrounded();
                Debug_Skip("未接触地面");
                return;
            }

            if (onlyWhenMoving)
            {
                var vel = MyCore.kcc != null ? MyCore.kcc.monitor.velocity : Vector3.zero;
                var planeNormal = GetMovePlaneNormal();
                vel = Vector3.ProjectOnPlane(vel, planeNormal);
                if (vel.magnitude < minMoveSpeed)
                {
                    FadeOutWeights(deltaTime);
                    Debug_OnRejectNotMoving();
                    Debug_Skip("未达到移动阈值");
                    return;
                }
            } 

            // Debug：避免在运行时刷屏
            if (debugRuntimeState)
            {
                // 仅用于排查参数是否合理
                // Debug.Log($"[FootPlacement] params castUp={castUp} castDown={castDown} sphereRadius={sphereRadius} stepUp={maxStepUp} stepDown={maxStepDown}");
            }
            // 避免破坏其它状态对脚IK的控制：仅当当前 pose 没有脚IK时才写入
            bool canWriteLeft = overrideExistingFootIK || pose.leftFoot.weight <= 0.001f;
            bool canWriteRight = overrideExistingFootIK || pose.rightFoot.weight <= 0.001f;
            if (!canWriteLeft && !canWriteRight)
            {
                Debug_OnRejectExistingIK();
                Debug_Skip("当前状态已在控制脚IK（未允许覆盖）");
                return;
            }

            // 计算脚下命中
            bool leftHit = SampleFootTarget(_leftFootBone, out var leftPos, out var leftNormal, out float leftDist, out var leftReject);
            bool rightHit = SampleFootTarget(_rightFootBone, out var rightPos, out var rightNormal, out float rightDist, out var rightReject);

            if (!leftHit) Debug_OnFootSampleReject(isLeft: true, leftReject);
            if (!rightHit) Debug_OnFootSampleReject(isLeft: false, rightReject);
    
            if (!leftHit && !rightHit)
            {
                Debug_Skip("未命中地面（cast失败或超出台阶限制）");
            }
        
            // 摆腿检测
            Vector3 leftBonePos = _leftFootBone.position;
            Vector3 rightBonePos = _rightFootBone.position;

            Vector3 leftVel = Vector3.zero;
            Vector3 rightVel = Vector3.zero;
            if (deltaTime > 0.0001f && _hasLastBonePos)
            {
                leftVel = (leftBonePos - _leftFootLastBonePos) / deltaTime;
                rightVel = (rightBonePos - _rightFootLastBonePos) / deltaTime;
            }
           
            _leftFootLastBonePos = leftBonePos;
            _rightFootLastBonePos = rightBonePos;
            _hasLastBonePos = true;
        
            // ===== 权重分配（互补 + 高点优先） =====
            float desiredTotal = (leftHit || rightHit) ? Mathf.Clamp01(footIKWeight) : 0f;
            float desiredLeftShareAuto;
            if (leftHit && !rightHit)
            {
                desiredLeftShareAuto = 1f;
            }
            else if (!leftHit && rightHit)
            {
                desiredLeftShareAuto = 0f;
            }
            else if (leftHit && rightHit)
            {
                float diffUp = Vector3.Dot(leftPos - rightPos, Vector3.up);
                float range = Mathf.Max(0.001f, higherFootPreferenceRange);
                float t = Mathf.Clamp01(0.5f + (diffUp / (2f * range)));
                desiredLeftShareAuto = Mathf.SmoothStep(0f, 1f, t);
            }
            else
            {
                desiredLeftShareAuto = _leftShare;
            }

            float desiredLeftShare = desiredLeftShareAuto;
            float animShareInput = -1f;
            bool usedAnimShare = false;
            if (supportShareBlend > 0.0001f && machine != null && !string.IsNullOrEmpty(supportShareParamName))
            {
                animShareInput = machine.GetFloat(supportShareParamName, -1f);
                if (animShareInput >= 0f)
                {
                    usedAnimShare = true;
                    float animShare = Mathf.Clamp01(animShareInput);
                    desiredLeftShare = Mathf.Lerp(desiredLeftShareAuto, animShare, Mathf.Clamp01(supportShareBlend));
                }
            }

            // share 护栏：变化率限制（先限制目标，再走SmoothDamp）
            if (supportShareMaxDeltaPerSec > 0.0001f && deltaTime > 0.0001f)
            {
                float maxDelta = supportShareMaxDeltaPerSec * deltaTime;
                desiredLeftShare = Mathf.Clamp(desiredLeftShare, _leftShare - maxDelta, _leftShare + maxDelta);
            }

            if (suppressWhenFootRising)
            {
                if (leftHit)
                {
                    // 斜坡关键修复：上坡走路时脚/根骨整体会有正Y速度，
                    // 不能用 worldY 速度判断抬腿，否则会把权重永久压到0。
                    // 改为判断“脚骨到命中点的垂直高度差(heightUp)是否在增长”。
                    float heightUp = Vector3.Dot(leftBonePos - leftPos, Vector3.up);
                    float heightUpVel = 0f;
                    if (_hasLeftLastHeightUp && deltaTime > 0.0001f)
                    {
                        heightUpVel = (heightUp - _leftLastHeightUp) / deltaTime;
                    }

                    bool rising = heightUpVel > risingVelocityThreshold;
                    if (!rising && risingHeightThreshold > 0.0001f)
                    {
                        rising = heightUpVel > 0.05f && heightUp > risingHeightThreshold;
                    }

                    _leftLastHeightUp = heightUp;
                    _hasLeftLastHeightUp = true;
                    if (rising)
                    {
                        // 左脚抬起：把权重让给右脚
                        desiredLeftShare = 0f;
                        desiredTotal = rightHit ? Mathf.Clamp01(footIKWeight) : 0f;
                        Debug_OnRejectSuppressedLeft();
                        Debug_Skip("抬腿抑制(左脚)");
                    }
                }
                else
                {
                    // 没命中时不更新高度缓存，避免下一次命中出现异常瞬间速度
                    _hasLeftLastHeightUp = false;
                }

                if (rightHit)
                {
                    float heightUp = Vector3.Dot(rightBonePos - rightPos, Vector3.up);
                    float heightUpVel = 0f;
                    if (_hasRightLastHeightUp && deltaTime > 0.0001f)
                    {
                        heightUpVel = (heightUp - _rightLastHeightUp) / deltaTime;
                    }

                    bool rising = heightUpVel > risingVelocityThreshold;
                    if (!rising && risingHeightThreshold > 0.0001f)
                    {
                        rising = heightUpVel > 0.05f && heightUp > risingHeightThreshold;
                    }

                    _rightLastHeightUp = heightUp;
                    _hasRightLastHeightUp = true;
                    if (rising)
                    {
                        // 右脚抬起：把权重让给左脚
                        desiredLeftShare = 1f;
                        desiredTotal = leftHit ? Mathf.Clamp01(footIKWeight) : 0f;
                        Debug_OnRejectSuppressedRight();
                        Debug_Skip("抬腿抑制(右脚)");
                    }
                }
                else
                {
                    _hasRightLastHeightUp = false;
                }
            }
    
            // Smooth 权重（保持互补关系）
            _totalWeight = Mathf.SmoothDamp(_totalWeight, desiredTotal, ref _totalWeightVel, Mathf.Max(0.001f, weightSmoothTime), float.MaxValue, deltaTime);
            _leftShare = Mathf.SmoothDamp(_leftShare, Mathf.Clamp01(desiredLeftShare), ref _leftShareVel, Mathf.Max(0.001f, weightSmoothTime), float.MaxValue, deltaTime);
            _leftWeight = _totalWeight * _leftShare;
            _rightWeight = _totalWeight * (1f - _leftShare);

            Debug_RecordHistory(animShareInput, usedAnimShare);

            // Smooth 位置/旋转
            if (leftHit)
            {
                _leftTargetPos = Vector3.SmoothDamp(_leftTargetPos, leftPos, ref _leftPosVel, Mathf.Max(0.001f, positionSmoothTime), float.MaxValue, deltaTime);
                _leftTargetRot = SmoothRotate(_leftTargetRot, ComputeFootRotation(leftNormal, _leftTargetRot), rotationLerpSpeed, deltaTime);
            }

            if (rightHit)
            {
                _rightTargetPos = Vector3.SmoothDamp(_rightTargetPos, rightPos, ref _rightPosVel, Mathf.Max(0.001f, positionSmoothTime), float.MaxValue, deltaTime);
                _rightTargetRot = SmoothRotate(_rightTargetRot, ComputeFootRotation(rightNormal, _rightTargetRot), rotationLerpSpeed, deltaTime);
            }

            // 如需日志请开启 debugRuntimeState 并使用节流（避免刷屏影响性能/表现）
        
            // 写入 pose（最终输出给 FinalIK）
            if (canWriteLeft && _leftWeight > 0.001f)
            {
                pose.leftFoot.weight = _leftWeight;
                pose.leftFoot.position = _leftTargetPos;
                pose.leftFoot.rotation = alignRotationToGround ? _leftTargetRot : _leftFootBone.rotation;
                pose.leftFoot.hintPosition = Vector3.zero;
                Debug_OnApplied();
            }
        
            if (canWriteRight && _rightWeight > 0.001f)
            {
                pose.rightFoot.weight = _rightWeight;
                pose.rightFoot.position = _rightTargetPos;
                pose.rightFoot.rotation = alignRotationToGround ? _rightTargetRot : _rightFootBone.rotation;
                pose.rightFoot.hintPosition = Vector3.zero;
                Debug_OnApplied();
            }

        }

        private void Debug_OnCallbackEnter()
        {
            if (!debugRuntimeState) return;
            _debugPostProcessCalls++;
            _debugLastCallbackTime = Time.time;
            _debugLastSkipReason = string.Empty;
        }

        private void Debug_Skip(string reason)
        {
            if (!debugRuntimeState) return;
            _debugLastSkipReason = reason;
        }

        private void Debug_OnApplied()
        {
            if (!debugRuntimeState) return;
            _debugAppliedCount++;
        }

        private void FadeOutWeights(float deltaTime)
        {
            _totalWeight = Mathf.SmoothDamp(_totalWeight, 0f, ref _totalWeightVel, Mathf.Max(0.001f, weightSmoothTime), float.MaxValue, deltaTime);
            _leftWeight = _totalWeight * _leftShare;
            _rightWeight = _totalWeight * (1f - _leftShare);
        }

        private enum FootSampleReject
        {
            None = 0,
            NoHit = 1,
            StepUpExceeded = 2,
            StepDownExceeded = 3
        }

        private bool SampleFootTarget(Transform foot, out Vector3 targetPos, out Vector3 normal, out float hitDistance, out FootSampleReject reject)
        {
            targetPos = default;
            normal = Vector3.up;
            hitDistance = 0f;
            reject = FootSampleReject.None;

            if (foot == null) return false;

            // 斜坡/台阶检测使用重力方向（世界Up/Down），避免角色倾斜影响探测。
            Vector3 origin = foot.position + Vector3.up * castUp;
            float maxDist = castUp + castDown;

            RaycastHit hit;
            bool hasHit;
            if (sphereRadius > 0.0001f)
            {
                hasHit = Physics.SphereCast(origin, sphereRadius, Vector3.down, out hit, maxDist, groundLayers, QueryTriggerInteraction.Ignore);
            }
            else
            {
                hasHit = Physics.Raycast(origin, Vector3.down, out hit, maxDist, groundLayers, QueryTriggerInteraction.Ignore);
            }

            if (!hasHit)
            {
                reject = FootSampleReject.NoHit;
                return false;
            }

            // 台阶限制：基于世界重力Up的“垂直高度差”（斜坡不应该受角色倾斜影响）
            float dUp = Vector3.Dot(hit.point - foot.position, Vector3.up);
            if (dUp > maxStepUp)
            {
                reject = FootSampleReject.StepUpExceeded;
                return false;
            }
            if (dUp < -maxStepDown)
            {
                reject = FootSampleReject.StepDownExceeded;
                return false;
            }

            normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
            targetPos = hit.point + normal * footSurfaceOffset;
            hitDistance = hit.distance;
            return true;
        }

        private void Debug_EnsureHistoryBuffers()
        {
            if (_debugHistTotal != null) return;

            _debugHistTotal = new float[DebugHistoryCapacity];
            _debugHistShare = new float[DebugHistoryCapacity];
            _debugHistLeft = new float[DebugHistoryCapacity];
            _debugHistRight = new float[DebugHistoryCapacity];
            _debugHistAnimShare = new float[DebugHistoryCapacity];
            _debugHistUsedAnim = new float[DebugHistoryCapacity];

            _debugHistoryHead = 0;
            _debugHistoryCount = 0;
        }

        private void Debug_RecordHistory(float animShareInput, bool usedAnimShare)
        {
            if (!debugRuntimeState) return;

            Debug_EnsureHistoryBuffers();

            _debugLastAnimShareInput = animShareInput;
            _debugLastUsedAnimShare = usedAnimShare;

            _debugHistTotal[_debugHistoryHead] = Mathf.Clamp01(_totalWeight);
            _debugHistShare[_debugHistoryHead] = Mathf.Clamp01(_leftShare);
            _debugHistLeft[_debugHistoryHead] = Mathf.Clamp01(_leftWeight);
            _debugHistRight[_debugHistoryHead] = Mathf.Clamp01(_rightWeight);
            _debugHistAnimShare[_debugHistoryHead] = animShareInput;
            _debugHistUsedAnim[_debugHistoryHead] = usedAnimShare ? 1f : 0f;

            _debugHistoryHead = (_debugHistoryHead + 1) % DebugHistoryCapacity;
            if (_debugHistoryCount < DebugHistoryCapacity) _debugHistoryCount++;
        }

        private void Debug_OnFootSampleReject(bool isLeft, FootSampleReject reject)
        {
            if (!debugRuntimeState) return;
            if (reject == FootSampleReject.None) return;

            switch (reject)
            {
                case FootSampleReject.NoHit:
                    _debugRejectNoHit++;
                    _debugLastRejectReason = (isLeft ? "Left" : "Right") + " 未命中地面";
                    break;
                case FootSampleReject.StepUpExceeded:
                    _debugRejectStepUp++;
                    _debugLastRejectReason = (isLeft ? "Left" : "Right") + " 超 maxStepUp";
                    break;
                case FootSampleReject.StepDownExceeded:
                    _debugRejectStepDown++;
                    _debugLastRejectReason = (isLeft ? "Left" : "Right") + " 超 maxStepDown";
                    break;
            }
        }

        private void Debug_OnRejectNotGrounded()
        {
            if (!debugRuntimeState) return;
            _debugRejectNotGrounded++;
            _debugLastRejectReason = "onlyWhenGrounded gate";
        }

        private void Debug_OnRejectNotMoving()
        {
            if (!debugRuntimeState) return;
            _debugRejectNotMoving++;
            _debugLastRejectReason = "onlyWhenMoving gate";
        }

        private void Debug_OnRejectSuppressedLeft()
        {
            if (!debugRuntimeState) return;
            _debugRejectSuppressedLeft++;
            _debugLastRejectReason = "抬腿抑制(左脚)";
        }

        private void Debug_OnRejectSuppressedRight()
        {
            if (!debugRuntimeState) return;
            _debugRejectSuppressedRight++;
            _debugLastRejectReason = "抬腿抑制(右脚)";
        }

        private void Debug_OnRejectExistingIK()
        {
            if (!debugRuntimeState) return;
            _debugRejectExistingIK++;
            _debugLastRejectReason = "已有脚IK且不允许覆盖";
        }

        private Vector3 GetMovePlaneNormal()
        {
            if (!useGroundNormalPlaneForMoveCheck) return Vector3.up;

            // 优先使用KCC的地面法线（稳定在地面时最可靠）
            if (MyCore != null && MyCore.kcc != null && MyCore.kcc.motor != null)
            {
                var gs = MyCore.kcc.motor.GroundingStatus;
                if (gs.IsStableOnGround || gs.FoundAnyGround)
                {
                    var n = gs.GroundNormal;
                    if (n.sqrMagnitude > 0.0001f) return n.normalized;
                }
            }

            return Vector3.up;
        }

        private bool IsGroundedForFootPlacement()
        {
            if (MyCore == null || MyCore.kcc == null) return false;

            // 斜坡“可用”策略：只要角色与地面有接触(FountAnyGround)就认为可贴合。
            // IsStableOnGround 在坡度过大/边缘/台阶时可能为 false，但脚贴合仍应工作。
            if (MyCore.kcc.motor != null)
            {
                var gs = MyCore.kcc.motor.GroundingStatus;
                if (gs.IsStableOnGround) return true;
                if (gs.FoundAnyGround) return true;
            }

            // 兜底：没有 motor 时才用 monitor
            return MyCore.kcc.monitor != null && MyCore.kcc.monitor.isStableOnGround;
        }

        private Quaternion ComputeFootRotation(Vector3 groundNormal, Quaternion lastTargetRot)
        {
            if (!alignRotationToGround)
            {
                return Quaternion.identity;
            }

            // 直观方案：脚朝向 = 玩家本体面向（投影到地面平面），脚Up = 地面法线。
            // 这样不会因为左右脚动画的局部轴/骨骼差异导致“抽象旋转”。
            Vector3 n = groundNormal.sqrMagnitude > 0.0001f ? groundNormal.normalized : Vector3.up;
            Vector3 desiredFwd = _animator != null ? _animator.transform.forward : Vector3.forward;

            Vector3 fwd = Vector3.ProjectOnPlane(desiredFwd, n);
            if (fwd.sqrMagnitude < 0.0001f)
            {
                // 投影退化时：用上次目标的forward兜底，避免突然翻转
                fwd = Vector3.ProjectOnPlane(lastTargetRot * Vector3.forward, n);
                if (fwd.sqrMagnitude < 0.0001f)
                {
                    fwd = Vector3.ProjectOnPlane(Vector3.forward, n);
                }
            }
            fwd.Normalize();

            Quaternion desired = Quaternion.LookRotation(fwd, n);

            // 夹紧最大倾斜角，避免法线异常/边缘命中时把脚拧到极限
            const float MaxTiltAngle = 45f;
            float tiltAngle = Vector3.Angle(Vector3.up, n);
            if (tiltAngle > MaxTiltAngle && tiltAngle > 0.001f)
            {
                // 只夹紧倾斜量，不改变yaw：在玩家forward周围回插
                float t = MaxTiltAngle / tiltAngle;
                Vector3 clampedN = Vector3.Slerp(Vector3.up, n, t);
                desired = Quaternion.LookRotation(fwd, clampedN);
            }

            return desired;
        }

        private static Quaternion SmoothRotate(Quaternion current, Quaternion target, float speed, float deltaTime)
        {
            if (speed <= 0.001f) return target;

            // 指数插值：t = 1 - e^(-k*dt)
            float t = 1f - Mathf.Exp(-speed * Mathf.Max(0f, deltaTime));
            return Quaternion.Slerp(current, target, t);
        }
    }
}
