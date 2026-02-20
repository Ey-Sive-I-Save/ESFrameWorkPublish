using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 攀爬模块 — 完整的墙壁攀爬/翻越/攀上系统
    /// 
    /// 核心机制：
    /// 1. 射线检测 ClimbableSurface 进入攀爬（影响范围内）
    /// 2. 攀爬时输入沿墙面法线分解（上下=世界Y轴 / 左右=沿墙面切线）
    /// 3. 到达顶部 → ClimbOver（MatchTarget 对齐到顶部边缘）
    /// 4. 检测矮墙 → Vault（MatchTarget 翻越）
    /// 5. 攀爬中跳跃 → ClimbJump（法线弹出 + 跳跃）
    /// 6. 每帧设置 SupportFlags = Climbing，驱动 KCC 使用攀爬速度分支
    /// </summary>
    [Serializable, TypeRegistryItem("基础攀爬模块")]
    public class EntityBasicClimbModule : EntityBasicModuleBase, IEntitySupportMotion
    {
        // ===== 开关 =====
        [Title("开关")]
        [LabelText("启用攀爬")]
        public bool enableClimb = true;

        [Title("功能禁用")]
        [LabelText("禁用普通攀爬")]
        public bool disableNormalClimb = false;

        [LabelText("禁用攀爬跳跃")]
        public bool disableClimbJump = false;

        [LabelText("禁用翻越")]
        public bool disableVault = false;

        [LabelText("禁用攀爬翻上")]
        public bool disableClimbOver = false;

        [LabelText("禁用贴墙吸附")]
        public bool disableWallSnap = false;

        [LabelText("禁用靠近拉拽")]
        public bool disableApproachPull = false;

        [LabelText("禁用攀爬输入速度")]
        public bool disableClimbInputVelocity = false;

        [LabelText("攀爬使用原始轴")]
        public bool useRawClimbInput = true;

        [LabelText("调试日志"), Tooltip("启用后在Console输出攀爬全流程日志")]
        public bool debugClimb = false;

        // ===== 检测配置 =====
        [Title("检测")]
        [LabelText("可攀爬层级"), Tooltip("射线碰撞检测的Layer")]
        public LayerMask climbableLayerMask = ~0;

        [LabelText("前方射线距离"), Tooltip("从角色前方检测攀爬面的最大距离")]
        public float forwardDetectDistance = 1.5f;

        [LabelText("射线起点高度偏移")]
        public float rayHeightOffset = 1.0f;

        [LabelText("多射线扫描数"), Tooltip("垂直方向发出的射线数量（越多越精确）")]
        [Range(1, 5)]
        public int verticalRayCount = 3;

        [LabelText("多射线间距(米)")]
        public float verticalRaySpacing = 0.4f;

        [LabelText("墙面检测间隔(帧)"), Tooltip("攀爬中墙面射线检测的最小间隔，降低无意义探空")]
        [Range(1, 10)]
        public int wallContactCheckInterval = 3;

        [LabelText("MatchTarget失败超时(秒)"), Tooltip("超时未激活MatchTarget则自动回退，避免卡死")]
        public float matchTargetFailTimeout = 0.2f;

        // ===== 状态引用 =====
        [Title("状态名")]
        [LabelText("攀爬状态名")]
        public string Climb_StateName = "攀爬";

        [LabelText("攀上状态名"), Tooltip("到顶部后的攀上/翻身动作状态")]
        public string ClimbOver_StateName = "攀爬翻上";

        [LabelText("翻越状态名"), Tooltip("翻越矮墙的动作状态")]
        public string Vault_StateName = "翻越";

        [LabelText("高翻越状态名"), Tooltip("更高墙体的翻越动作状态(可选)")]
        public string VaultHigh_StateName = "";

        [LabelText("攀爬跳跃状态名"), Tooltip("攀爬中跳跃的专用状态（可选，留空则使用现有逻辑）")]
        public string ClimbJump_StateName = "";

        // ===== 攀爬参数 =====
        [Title("攀爬参数")]
        [InlineProperty, HideLabel]
        public ClimbParams climbParams = ClimbParams.Default;

        [Title("攀爬速度波动")]
        [LabelText("垂直速度波动幅度"), Tooltip("上下攀爬速度的起伏比例，比如0.2=±20%")]
        public float climbVerticalSpeedWaveAmplitude = 0.2f;

        [LabelText("垂直速度波动频率(Hz)")]
        public float climbVerticalSpeedWaveFrequency = 1.5f;

        [Title("侧向速度")]
        [LabelText("侧向攀爬速度倍率"), Tooltip("左右攀爬速度整体变慢的倍率")]
        public float climbLateralSpeedMultiplier = 0.7f;

        [Title("攀爬跳跃侧向")]
        [LabelText("侧向跳跃速度"), Tooltip("攀爬跳跃时左右侧向速度")]
        public float climbJumpLateralSpeed = 3f;

        [LabelText("攀爬跳跃允许自由转向")]
        public bool allowClimbJumpFreeRotation = true;

        [LabelText("攀爬跳跃允许切换墙面")]
        public bool allowClimbJumpSwitchSurface = true;

        // ===== MatchTarget配置 =====
        [Title("MatchTarget配置")]
        [LabelText("攀上-身体部位")]
        public AvatarTarget climbOverBodyPart = AvatarTarget.LeftHand;

        [LabelText("攀上-开始时间"), Range(0f, 1f)]
        public float climbOverStartTime = 0.1f;

        [LabelText("攀上-结束时间"), Range(0f, 1f)]
        public float climbOverEndTime = 0.8f;

        [LabelText("攀上-位置权重")]
        public Vector3 climbOverPosWeight = new Vector3(0f, 1f, 1f);

        [LabelText("攀上-旋转权重"), Range(0f, 1f)]
        public float climbOverRotWeight = 0.5f;

        [LabelText("翻越-身体部位")]
        public AvatarTarget vaultBodyPart = AvatarTarget.Root;

        [LabelText("翻越-开始时间"), Range(0f, 1f)]
        public float vaultStartTime = 0.1f;

        [LabelText("翻越-结束时间"), Range(0f, 1f)]
        public float vaultEndTime = 0.9f;

        [LabelText("翻越-位置权重")]
        public Vector3 vaultPosWeight = new Vector3(0f, 1f, 1f);

        [LabelText("翻越-旋转权重"), Range(0f, 1f)]
        public float vaultRotWeight = 0f;

        [Title("登顶判定")]
        [LabelText("登顶高度偏移(米)")]
        public float climbTopCheckHeightOffset = 1.4f;

        [Title("翻越退出")]
        [LabelText("翻越最小持续时间(秒)")]
        public float vaultMinDuration = 0.3f;

        [LabelText("翻越最大持续时间(秒)")]
        public float vaultMaxDuration = 1.2f;

        [LabelText("翻越退出距离(米)"), Tooltip("距离目标点足够近时自动退出")]
        public float vaultExitDistance = 0.15f;

        [Title("高翻越判定")]
        [LabelText("高翻越最大高度(米)"), Tooltip("墙高低于此值时可触发高翻越")]
        public float vaultHighMaxHeight = 2.0f;

        [Title("登顶退出")]
        [LabelText("登顶最小持续时间(秒)")]
        public float climbOverMinDuration = 0.2f;

        [LabelText("登顶最大持续时间(秒)")]
        public float climbOverMaxDuration = 1.2f;

        [LabelText("登顶退出距离(米)")]
        public float climbOverExitDistance = 0.15f;

        [LabelText("登顶退出进度"), Range(0f, 1f)]
        public float climbOverExitNormalized = 0.95f;

        [LabelText("登顶接地退出延迟(秒)")]
        public float climbOverGroundedExitDelay = 0.1f;

        [Title("登顶落脚检测")]
        [LabelText("落脚探测高度偏移")]
        public float climbOverGroundProbeHeightOffset = 0.1f;

        [LabelText("落脚探测距离")]
        public float climbOverGroundProbeDistance = 0.6f;

        [LabelText("落脚探测半径")]
        public float climbOverGroundProbeRadius = 0.25f;

        [Title("登顶收敛")]
        [LabelText("登顶前向收敛速度")]
        public float climbOverSettleSpeed = 1.2f;

        [LabelText("登顶前向推进距离")]
        public float climbOverForwardAdvanceDistance = 0.6f;

        [LabelText("登顶向上最大速度")]
        public float climbOverUpMaxSpeed = 2.4f;

        [LabelText("登顶向上最小速度")]
        public float climbOverUpMinSpeed = 0.6f;

        [LabelText("登顶向前最大速度")]
        public float climbOverForwardMaxSpeed = 2.0f;

        [LabelText("登顶向前最小速度")]
        public float climbOverForwardMinSpeed = 0.4f;

        [LabelText("登顶快推进距离")]
        public float climbOverFastDistance = 0.6f;

        [LabelText("低于墙顶上推增益")]
        public float climbOverBelowTopUpBoost = 2.2f;

        [LabelText("墙顶判定偏移")]
        public float climbOverTopHeightOffset = 0.05f;

        [Title("翻越前向推进")]
        [LabelText("翻越前向推进速度")]
        public float vaultForwardPushSpeed = 1.2f;

        [LabelText("翻越前向推进距离")]
        public float vaultForwardPushDistance = 0.5f;

        [Title("翻越落脚检测")]
        [LabelText("翻越落脚探测高度偏移")]
        public float vaultGroundProbeHeightOffset = 0.1f;

        [LabelText("翻越落脚探测距离")]
        public float vaultGroundProbeDistance = 0.6f;

        [LabelText("翻越落脚探测半径")]
        public float vaultGroundProbeRadius = 0.25f;

        [LabelText("翻越前后落脚检测距离")]
        public float vaultFrontBackProbeDistance = 0.25f;

        [LabelText("翻越起点过远退出距离")]
        public float vaultFarExitDistance = 1.5f;

        [Title("攀上-阶段2(脚)"), LabelText("启用二阶段对齐")]
        public bool enableClimbOverFootPhase = true;

        [LabelText("阶段2触发进度"), Range(0f, 1f), Tooltip("归一化进度到达该值后切换到脚部对齐")]
        public float climbOverPhase2Trigger = 0.6f;

        [LabelText("攀上-脚部身体部位")]
        public AvatarTarget climbOverFootBodyPart = AvatarTarget.LeftFoot;

        [LabelText("攀上-脚部开始时间"), Range(0f, 1f)]
        public float climbOverFootStartTime = 0.6f;

        [LabelText("攀上-脚部结束时间"), Range(0f, 1f)]
        public float climbOverFootEndTime = 0.95f;

        [LabelText("攀上-脚部位置权重")]
        public Vector3 climbOverFootPosWeight = new Vector3(0f, 1f, 1f);

        [LabelText("攀上-脚部旋转权重"), Range(0f, 1f)]
        public float climbOverFootRotWeight = 0.5f;

        [Title("逼近配置")]
        [LabelText("持续逼近目标")]
        public bool enableContinuousApproach = true;

        [LabelText("逼近速度系数"), Tooltip("距离越远越快")]
        public float approachSpeed = 6f;

        [LabelText("逼近最大速度")]
        public float approachMaxSpeed = 3f;

        [LabelText("逼近停止距离")]
        public float approachStopDistance = 0.05f;

        [LabelText("重应用间隔")]
        public float matchTargetReapplyInterval = 0.05f;

        [LabelText("重应用距离阈值")]
        public float matchTargetReapplyMinDistance = 0.02f;

        [LabelText("重应用角度阈值")]
        public float matchTargetReapplyMinAngle = 2f;

        // ===== 运行时状态（只读调试） =====
        [Title("运行时状态")]
        [ShowInInspector, ReadOnly, LabelText("子状态")]
        public ClimbSubState subState { get; private set; } = ClimbSubState.None;

        [ShowInInspector, ReadOnly, LabelText("当前攀爬面")]
        public ClimbableSurface currentSurface { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("墙面法线")]
        public Vector3 currentWallNormal { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("贴壁目标点")]
        public Vector3 currentSnapTarget { get; private set; }

        // ===== 内部缓存 =====
        private StateBase _climbState;
        private StateBase _climbOverState;
        private StateBase _vaultState;
        private StateBase _vaultHighState;
        private StateBase _activeVaultState;
        private StateBase _climbJumpState;
        private StateMachine sm;
        private Collider[] _overlapBuffer = new Collider[16];

        [NonSerialized] public Vector3 climbVelocityRequest;
        [NonSerialized] public bool climbJumpRequested;

        [NonSerialized] private float _lastWallRayDistance;
        [NonSerialized] private int _wallContactFrameCounter;
        [NonSerialized] private float _exitClimbTimestamp = -999f;
        [NonSerialized] private float _climbEnterTime = -999f;
        [NonSerialized] private float _groundedSince = -1f;
        [NonSerialized] private float _climbJumpStartTime = -999f;
        [NonSerialized] private float _matchTargetStartTime = -999f;
        [NonSerialized] private bool _climbOverPhase2Active;
        [NonSerialized] private float _climbOverGroundedSince = -1f;
        [NonSerialized] private Vector3 _vaultStartPosition;

        private const float ClimbJumpUpMultiplier = 1f;
        private const float ClimbJumpNormalMultiplier = 0.4f;
        private const float ClimbJumpAirControlSpeed = 4f;
        private const float ClimbJumpAirControlSharpness = 6f;
        private const float ClimbJumpReturnMinTime = 0.5f;

        [Title("冷却")]
        [LabelText("退出攀爬冷却(秒)"), Tooltip("退出攀爬后多久内禁止自动重新进入")]
        public float exitClimbCooldown = 1f;

        public override void Start()
        {
            base.Start();
            if (MyCore?.stateDomain?.stateMachine != null)
            {
                sm = MyCore.stateDomain.stateMachine;
                _climbState = sm.GetStateByString(Climb_StateName);
                _climbOverState = sm.GetStateByString(ClimbOver_StateName);
                _vaultState = sm.GetStateByString(Vault_StateName);
                if (!string.IsNullOrEmpty(VaultHigh_StateName))
                    _vaultHighState = sm.GetStateByString(VaultHigh_StateName);
                if (!string.IsNullOrEmpty(ClimbJump_StateName))
                    _climbJumpState = sm.GetStateByString(ClimbJump_StateName);
            }
            if (MyCore != null)
            {
                MyCore.kcc.climbModule = this;
            }
        }

        protected override void Update()
        {
            if (MyCore == null || !enableClimb) return;

            if (debugClimb)
            {
                Debug.Log($"[登顶] Update: subState={subState}, surface={(currentSurface != null ? currentSurface.name : "null")}");
            }

            bool skipGuard = subState == ClimbSubState.None
                || (subState == ClimbSubState.ClimbJump && _climbJumpState == null);

            if (!skipGuard)
            {
                bool anyClimbStateRunning = false;

                switch (subState)
                {
                    case ClimbSubState.Approach:
                    case ClimbSubState.Climbing:
                        anyClimbStateRunning = _climbState != null && _climbState.baseStatus == StateBaseStatus.Running;
                        break;
                    case ClimbSubState.ClimbOver:
                        anyClimbStateRunning = _climbOverState != null && _climbOverState.baseStatus == StateBaseStatus.Running;
                        break;
                    case ClimbSubState.Vault:
                        var vaultState = _activeVaultState ?? _vaultState ?? _vaultHighState;
                        anyClimbStateRunning = vaultState != null && vaultState.baseStatus == StateBaseStatus.Running;
                        break;
                    case ClimbSubState.ClimbJump:
                        anyClimbStateRunning = _climbJumpState != null && _climbJumpState.baseStatus == StateBaseStatus.Running;
                        break;
                }

                if (!anyClimbStateRunning)
                {
                    if (subState == ClimbSubState.ClimbJump && _climbJumpState != null)
                    {
                        if (TryReturnToClimbAfterJump())
                        {
                            return;
                        }
                    }

                    if (debugClimb)
                    {
                        Debug.LogWarning($"[Climb] 状态机攀爬状态已被外部打断! subState={subState} 但对应状态未Running → 重置为None");
                    }
                    ResetClimbState();
                    _exitClimbTimestamp = Time.time;
                }
            }

            switch (subState)
            {
                case ClimbSubState.None:
                    UpdateProximityDetection();
                    break;

                case ClimbSubState.Approach:
                    UpdateApproach();
                    break;

                case ClimbSubState.Climbing:
                    if (debugClimb) Debug.Log("[登顶] 进入UpdateClimbing");
                    UpdateClimbing();
                    break;

                case ClimbSubState.ClimbOver:
                case ClimbSubState.Vault:
                    if (debugClimb)
                    {
                        Debug.Log($"[登顶] 进入UpdateMatchTargetPhase: subState={subState}");
                    }
                    UpdateMatchTargetPhase();
                    break;

                case ClimbSubState.ClimbJump:
                    UpdateClimbJump();
                    break;
            }
        }

        public void ToggleClimb()
        {
            if (debugClimb)
            {
                Debug.Log($"[Climb] ToggleClimb调用! subState={subState}, detectedSurface={(detectedSurface != null ? detectedSurface.name : "null")}");
            }

            if (subState == ClimbSubState.None)
            {
                if (detectedSurface != null)
                {
                    EnterClimbing(detectedSurface, _detectedHitNormal, _detectedHitPoint);
                }
                else
                {
                    TryStartClimb();
                }
            }
            else
            {
                ForceExitClimb();
            }
        }

        public bool TryStartClimb()
        {
            if (!enableClimb || MyCore == null || sm == null) return false;
            if (subState != ClimbSubState.None) return false;

            if (!disableVault && TryDetectVault())
                return true;

            if (!disableNormalClimb && TryDetectClimbableSurface(out var surface, out var hitNormal, out var hitPoint))
            {
                return EnterClimbing(surface, hitNormal, hitPoint);
            }

            return false;
        }

        public void RequestClimbJump()
        {
            if (subState != ClimbSubState.Climbing && subState != ClimbSubState.Approach) return;
            if (disableClimbJump)
            {
                if (debugClimb) Debug.LogWarning("[攀爬] 攀爬跳跃已禁用，忽略请求");
                return;
            }

            if (currentSurface != null)
            {
                Vector3 charPos = MyCore.kcc.motor.TransientPosition;
                currentWallNormal = currentSurface.GetDynamicNormal(charPos);
            }

            if (_climbJumpState != null)
            {
                if (!sm.TryActivateState(_climbJumpState))
                {
                    if (debugClimb) Debug.LogWarning($"[攀爬] 激活攀爬跳跃状态失败: {ClimbJump_StateName}");
                    return;
                }
                ExitClimbingState(false);
            }

            subState = ClimbSubState.ClimbJump;
            climbJumpRequested = true;
            _climbJumpStartTime = Time.time;

            if (debugClimb)
            {
                Debug.Log($"[Climb] 攀爬跳跃请求! wallNormal={currentWallNormal}, " +
                    $"jumpSpeed={climbParams.climbJumpSpeed}, normalForce={climbParams.climbJumpNormalForce}" +
                    $", 专用状态={(_climbJumpState != null ? ClimbJump_StateName : "无")}");
            }
        }

        public void ForceExitClimb()
        {
            if (subState == ClimbSubState.None) return;

            ExitClimbingState();
            ResetClimbState();
            _exitClimbTimestamp = Time.time;
        }

        [Title("近距离检测")]
        [LabelText("启用接近提示"), Tooltip("接近可攀爬面时自动检测并提供状态提示")]
        public bool enableProximityDetect = true;

        [LabelText("检测间隔(帧)"), Tooltip("每N帧执行一次近距离扫描，节省性能")]
        [Range(1, 10)]
        public int proximityCheckInterval = 8;

        [LabelText("自动攀爬(无需按键)"), Tooltip("接近可攀爬面且面朝墙壁时自动进入攀爬")]
        public bool autoClimbOnProximity = false;

        [ShowInInspector, ReadOnly, LabelText("检测到的攀爬面")]
        public ClimbableSurface detectedSurface { get; private set; }

        [NonSerialized] private int _proximityFrameCounter;
        [NonSerialized] private Vector3 _detectedHitNormal;
        [NonSerialized] private Vector3 _detectedHitPoint;

        private void UpdateProximityDetection()
        {
            if (!enableProximityDetect) return;

            if (Time.time - _exitClimbTimestamp < exitClimbCooldown) return;

            _proximityFrameCounter++;
            if (_proximityFrameCounter < proximityCheckInterval) return;
            _proximityFrameCounter = 0;

            if (TryDetectClimbableSurface(out var surface, out var hitNormal, out var hitPoint))
            {
                detectedSurface = surface;
                _detectedHitNormal = hitNormal;
                _detectedHitPoint = hitPoint;

                if (autoClimbOnProximity)
                {
                    if (disableNormalClimb) return;
                    Vector3 charFwd = MyCore.kcc.motor.CharacterForward;
                    float facingDot = Vector3.Dot(charFwd, -hitNormal);
                    if (facingDot > 0.5f)
                    {
                        EnterClimbing(surface, hitNormal, hitPoint);
                    }
                }
            }
            else
            {
                detectedSurface = null;
            }
        }

        private bool TryDetectClimbableSurface(out ClimbableSurface surface, out Vector3 hitNormal, out Vector3 hitPoint)
        {
            surface = null;
            hitNormal = Vector3.zero;
            hitPoint = Vector3.zero;

            if (MyCore.kcc.motor == null) return false;

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            Vector3 charFwd = MyCore.kcc.motor.CharacterForward;

            for (int i = 0; i < verticalRayCount; i++)
            {
                float heightOff = rayHeightOffset + (i - (verticalRayCount - 1) * 0.5f) * verticalRaySpacing;
                Vector3 rayOrigin = charPos + Vector3.up * heightOff;

                if (Physics.Raycast(rayOrigin, charFwd, out RaycastHit hit, forwardDetectDistance, climbableLayerMask))
                {
                    var climbable = hit.collider.GetComponentInParent<ClimbableSurface>();
                    if (climbable != null)
                    {
                        Vector3 closest = climbable.GetClosestPointInArea(charPos);
                        float distToArea = Vector3.Distance(charPos, closest);
                        bool inAreaRange = distToArea <= forwardDetectDistance;
                        bool inHeight = climbable.IsInHeightRange(charPos.y);

                        if (debugClimb)
                        {
                            Debug.Log($"[Climb] Detect射线{i}: hit={hit.collider.name}, surface={climbable.name}, " +
                                $"dist={distToArea:F2}/{forwardDetectDistance:F2}(范围{(inAreaRange ? "OK" : "OUT")}), " +
                                $"inHeight={inHeight}, hitPoint={hit.point}, hitNormal={hit.normal}");
                        }

                        if (inAreaRange && inHeight)
                        {
                            surface = climbable;
                            hitNormal = hit.normal;
                            hitPoint = hit.point;
                            return true;
                        }
                    }
                    else if (debugClimb)
                    {
                        Debug.Log($"[Climb] Detect射线{i}: hit={hit.collider.name} 但无ClimbableSurface组件");
                    }
                }
            }

            return false;
        }

        private bool TryDetectVault()
        {
            if (disableVault) return false;
            if (MyCore.kcc.motor == null) return false;

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            Vector3 charFwd = MyCore.kcc.motor.CharacterForward;

            Vector3 lowRayOrigin = charPos + Vector3.up * 0.3f;
            if (!Physics.Raycast(lowRayOrigin, charFwd, out RaycastHit lowHit, forwardDetectDistance, climbableLayerMask))
            {
                if (debugClimb) Debug.Log("[Climb] TryDetectVault: 低处射线未命中");
                return false;
            }

            var climbable = lowHit.collider.GetComponentInParent<ClimbableSurface>();
            if (climbable == null)
            {
                if (debugClimb) Debug.Log($"[Climb] TryDetectVault: 命中{lowHit.collider.name}但无ClimbableSurface组件");
                return false;
            }

            if (climbable.surfaceType != ClimbableSurfaceType.LowWall && climbable.surfaceType != ClimbableSurfaceType.Ledge)
            {
                if (debugClimb) Debug.Log($"[Climb] TryDetectVault: surface={climbable.name} 类型={climbable.surfaceType}，非可翻越类型");
                return false;
            }

            Vector3 closest = climbable.GetClosestPointInArea(charPos);
            float distToArea = Vector3.Distance(charPos, closest);
            if (distToArea > forwardDetectDistance)
            {
                if (debugClimb) Debug.Log($"[Climb] TryDetectVault: 距离{distToArea:F2} > 前方射线距离{forwardDetectDistance:F2}");
                return false;
            }

            float probeHeight = Mathf.Max(climbParams.vaultMaxHeight, vaultHighMaxHeight) + 0.5f;
            Vector3 probeStart = lowHit.point + charFwd * 0.05f + Vector3.up * probeHeight;
            float wallTopY;
            if (Physics.Raycast(probeStart, Vector3.down, out RaycastHit topHit, probeHeight + 0.5f, climbableLayerMask))
            {
                wallTopY = topHit.point.y;
            }
            else
            {
                wallTopY = climbable.transform.position.y + climbable.wallHeight;
            }

            float relativeWallHeight = wallTopY - charPos.y;

            if (debugClimb)
            {
                Debug.Log($"[Climb] TryDetectVault: surface={climbable.name}, relWallH={relativeWallHeight:F2}, " +
                    $"vaultMaxH={climbParams.vaultMaxHeight:F2}, wallTopY={wallTopY:F2}");
            }

            if (relativeWallHeight < 0.2f)
            {
                return false;
            }

            bool canLowVault = relativeWallHeight <= climbParams.vaultMaxHeight;
            bool canHighVault = relativeWallHeight > climbParams.vaultMaxHeight
                && relativeWallHeight <= Mathf.Max(climbParams.vaultMaxHeight, vaultHighMaxHeight);

            Vector3 highRayOrigin = charPos + Vector3.up * (relativeWallHeight + 0.3f);
            bool topClear = !Physics.Raycast(highRayOrigin, charFwd, forwardDetectDistance, climbableLayerMask);
            if (!topClear)
            {
                if (debugClimb) Debug.Log("[Climb] TryDetectVault: 墙顶上方有阻挡，无法翻越");
                return false;
            }

            if (canLowVault)
            {
                return EnterVault(climbable, lowHit.normal, false);
            }

            if (canHighVault)
            {
                return EnterVault(climbable, lowHit.normal, true);
            }

            return false;
        }

        private bool EnterClimbing(ClimbableSurface surface, Vector3 wallNormal, Vector3 hitPoint)
        {
            if (disableNormalClimb)
            {
                if (debugClimb) Debug.LogWarning("[攀爬] 普通攀爬已禁用，拒绝进入");
                return false;
            }
            if (_climbState == null)
            {
                if (debugClimb) Debug.LogWarning("[Climb] EnterClimbing失败: _climbState为null! 检查状态机中是否存在名为\"" + Climb_StateName + "\"的状态");
                return false;
            }

            if (!sm.TryActivateState(_climbState))
            {
                if (debugClimb) Debug.LogWarning($"[Climb] EnterClimbing失败: TryActivateState返回false (状态={Climb_StateName}, baseStatus={_climbState.baseStatus})");
                return false;
            }

            currentSurface = surface;
            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            currentWallNormal = surface.GetDynamicNormal(charPos);
            currentSnapTarget = hitPoint + currentWallNormal * climbParams.wallOffset;
            subState = ClimbSubState.Approach;
            _climbEnterTime = Time.time;

            MyCore.kcc.motor.ForceUnground(0.1f);

            if (debugClimb)
            {
                Debug.Log($"[Climb] ★ 进入攀爬成功! surface={surface.name}, type={surface.surfaceType}, " +
                    $"wallNormal={wallNormal}, hitPoint={hitPoint}, snapTarget={currentSnapTarget}");
            }
            return true;
        }

        private bool EnterVault(ClimbableSurface surface, Vector3 wallNormal, bool useHighVault)
        {
            _activeVaultState = useHighVault && _vaultHighState != null ? _vaultHighState : _vaultState;

            if (_activeVaultState == null)
            {
                if (debugClimb) Debug.LogWarning($"[Climb] EnterVault失败: vaultState为null! 检查状态机中是否存在名为\"{Vault_StateName}\"或\"{VaultHigh_StateName}\"的状态");
                return false;
            }

            if (!sm.TryActivateState(_activeVaultState))
            {
                if (debugClimb) Debug.LogWarning($"[Climb] EnterVault失败: TryActivateState返回false (状态={_activeVaultState.strKey}, baseStatus={_activeVaultState.baseStatus})");
                return false;
            }

            currentSurface = surface;
            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            currentWallNormal = surface.GetDynamicNormal(charPos);
            subState = ClimbSubState.Vault;

            _vaultStartPosition = charPos;

            _climbOverPhase2Active = false;

            _matchTargetStartTime = Time.time;

            if (enableContinuousApproach)
            {
                _activeVaultState.allowMatchTargetReapply = true;
                _activeVaultState.matchTargetReapplyInterval = matchTargetReapplyInterval;
                _activeVaultState.matchTargetReapplyMinDistance = matchTargetReapplyMinDistance;
                _activeVaultState.matchTargetReapplyMinAngle = matchTargetReapplyMinAngle;
            }

            Vector3 targetPos = surface.GetMatchTargetPosition(charPos);
            Quaternion targetRot = surface.GetMatchTargetRotation(charPos);
            var animConfig = _activeVaultState?.stateSharedData?.animationConfig;
            if (animConfig != null && animConfig.enableMatchTarget)
            {
                var preset = animConfig.matchTargetPreset;
                _activeVaultState.StartMatchTarget(
                    targetPos,
                    targetRot,
                    preset.bodyPart,
                    preset.startNormalizedTime,
                    preset.endNormalizedTime,
                    preset.positionWeight,
                    preset.rotationWeight
                );
            }
            else
            {
                _activeVaultState.StartMatchTarget(
                    targetPos,
                    targetRot,
                    vaultBodyPart,
                    vaultStartTime,
                    vaultEndTime,
                    vaultPosWeight,
                    vaultRotWeight
                );
            }

            if (debugClimb && !_activeVaultState.IsMatchTargetActive)
            {
                Debug.LogWarning("[Climb] Vault MatchTarget未激活，请检查状态配置或Animator");
            }

            if (debugClimb)
            {
                Debug.Log($"[Climb] ★ 进入Vault翻越! surface={surface.name}, targetPos={targetPos}, " +
                    $"targetRot={targetRot.eulerAngles}, bodyPart={vaultBodyPart}, " +
                    $"timeRange=[{vaultStartTime:F2},{vaultEndTime:F2}] state={_activeVaultState.strKey}");
            }
            return true;
        }

        private void EnterClimbOver()
        {
            if (disableClimbOver)
            {
                if (debugClimb) Debug.LogWarning("[攀爬] 攀爬翻上已禁用，忽略触发");
                return;
            }
            if (debugClimb)
            {
                Debug.Log("[登顶] EnterClimbOver: 准备进入登顶状态");
            }
            if (_climbOverState == null || currentSurface == null)
            {
                if (debugClimb) Debug.LogWarning($"[Climb] EnterClimbOver失败: _climbOverState={((_climbOverState != null) ? ClimbOver_StateName : "null")}, surface={(currentSurface != null ? currentSurface.name : "null")}");
                return;
            }

            if (_climbState != null && _climbState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(Climb_StateName);
            }

            if (!sm.TryActivateState(_climbOverState))
            {
                if (debugClimb) Debug.LogWarning($"[Climb] EnterClimbOver失败: TryActivateState返回false (状态={ClimbOver_StateName}, baseStatus={_climbOverState.baseStatus})");
                return;
            }
            if (debugClimb)
            {
                Debug.Log($"[登顶] EnterClimbOver: 状态激活成功 name={ClimbOver_StateName}");
            }

            subState = ClimbSubState.ClimbOver;

            _climbOverPhase2Active = false;

            _matchTargetStartTime = Time.time;

            if (enableContinuousApproach)
            {
                _climbOverState.allowMatchTargetReapply = true;
                _climbOverState.matchTargetReapplyInterval = matchTargetReapplyInterval;
                _climbOverState.matchTargetReapplyMinDistance = matchTargetReapplyMinDistance;
                _climbOverState.matchTargetReapplyMinAngle = matchTargetReapplyMinAngle;
            }

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            Vector3 targetPos = currentSurface.GetMatchTargetPosition(charPos);
            Quaternion targetRot = currentSurface.GetMatchTargetRotation(charPos);

            var animConfig = _climbOverState?.stateSharedData?.animationConfig;
            if (animConfig != null && animConfig.enableMatchTarget)
            {
                var preset = animConfig.matchTargetPreset;
                _climbOverState.StartMatchTarget(
                    targetPos,
                    targetRot,
                    preset.bodyPart,
                    preset.startNormalizedTime,
                    preset.endNormalizedTime,
                    preset.positionWeight,
                    preset.rotationWeight
                );
            }
            else
            {
                _climbOverState.StartMatchTarget(
                    targetPos,
                    targetRot,
                    climbOverBodyPart,
                    climbOverStartTime,
                    climbOverEndTime,
                    climbOverPosWeight,
                    climbOverRotWeight
                );
            }

            if (debugClimb && !_climbOverState.IsMatchTargetActive)
            {
                Debug.LogWarning("[Climb] ClimbOver MatchTarget未激活，请检查状态配置或Animator");
            }

            if (debugClimb)
            {
                Debug.Log($"[Climb] ★ 进入ClimbOver! targetPos={targetPos}, targetRot={targetRot.eulerAngles}, " +
                    $"bodyPart={climbOverBodyPart}, timeRange=[{climbOverStartTime:F2},{climbOverEndTime:F2}], " +
                    $"posWeight={climbOverPosWeight}, rotWeight={climbOverRotWeight}");
            }
        }

        private void UpdateApproach()
        {
            if (currentSurface == null) { ForceExitClimb(); return; }

            if (!disableVault && TryDetectVault())
            {
                return;
            }

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            currentWallNormal = currentSurface.GetDynamicNormal(charPos);
            Vector3 targetOnWall = currentSurface.GetClosestClimbPoint(charPos);
            currentSnapTarget = targetOnWall + currentWallNormal * climbParams.wallOffset;

            RefreshWallContact(true, true);
            if (subState != ClimbSubState.Approach) return;

            float dist = Vector3.Distance(charPos, currentSnapTarget);
            Debug.Log(dist+"  "+charPos+"  "+ currentSnapTarget );
            if (dist < 0.1f)
            {
                subState = ClimbSubState.Climbing;
            }

            Vector3 moveDir = (currentSnapTarget - charPos).normalized;
            float approachSpeed = Mathf.Min(climbParams.snapSharpness * dist, climbParams.maxSnapSpeed * 2f);
            climbVelocityRequest = disableApproachPull ? Vector3.zero : (moveDir * approachSpeed);

            if (debugClimb)
            {
                Debug.LogWarning($"[攀爬-靠近] 角色位置={charPos} 吸附点={currentSnapTarget} 距离={dist:F3} 速度={climbVelocityRequest}");
            }
        }

        private void UpdateClimbing()
        {
            if (currentSurface == null) { ForceExitClimb(); return; }

            bool hasInputRaw = MyCore.kcc.moveInput.sqrMagnitude > 0.001f || Mathf.Abs(MyCore.kcc.verticalInput) > 0.01f;

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            Vector3 wallNormal = currentSurface.GetDynamicNormal(charPos);
            currentWallNormal = wallNormal;

            if (MyCore.kcc.monitor.isStableOnGround)
            {
                if (_groundedSince < 0f) _groundedSince = Time.time;
                if (Time.time - _groundedSince >= climbParams.groundedExitDelay)
                {
                    if (debugClimb) Debug.LogWarning($"[攀爬][自动退出] 持续着地超过阈值: groundedFor={(Time.time - _groundedSince):F2}s >= {climbParams.groundedExitDelay:F2}s");
                    ForceExitClimb();
                    return;
                }
            }
            else
            {
                _groundedSince = -1f;
            }

            if (hasInputRaw && _lastWallRayDistance > climbParams.maxWallDistance)
            {
                if (debugClimb)
                    Debug.LogWarning($"[攀爬][自动退出] 离墙过远: rayDist={_lastWallRayDistance:F2} > maxWallDist={climbParams.maxWallDistance:F2}");
                ForceExitClimb();
                return;
            }

            Vector3 wallRight = Vector3.Cross(wallNormal, Vector3.up).normalized;
            Vector3 wallUp = Vector3.up;

            Vector3 moveInput = MyCore.kcc.moveInput;
            float horizontal = 0f;
            float vertical = 0f;
            bool hasRawAxis = false;
            if (!disableClimbInputVelocity && useRawClimbInput && TryGetModule(out EntityAIInputSystemModule inputModule))
            {
                Vector2 rawMove = inputModule.Move;
                if (rawMove.sqrMagnitude > 0.0001f)
                {
                    horizontal = Mathf.Clamp(rawMove.x, -1f, 1f);
                    vertical = Mathf.Clamp(rawMove.y, -1f, 1f);
                    hasInputRaw = true;
                    hasRawAxis = true;
                }
            }

            if (!disableClimbInputVelocity && (!useRawClimbInput || !hasRawAxis))
            {
                Vector3 localInput = Quaternion.Inverse(MyCore.kcc.motor.TransientRotation) * moveInput;
                horizontal = Mathf.Clamp(localInput.x, -1f, 1f);
                vertical = Mathf.Clamp(localInput.z, -1f, 1f);
                if (localInput.sqrMagnitude > 0.0001f)
                {
                    hasInputRaw = true;
                }
            }

            if (Mathf.Abs(MyCore.kcc.verticalInput) > 0.01f)
            {
                vertical = MyCore.kcc.verticalInput;
                hasInputRaw = true;
            }

            RefreshWallContact(hasInputRaw, hasInputRaw);
            if (subState != ClimbSubState.Climbing) return;

            Vector3 climbDir = wallRight * horizontal + wallUp * vertical;
            climbDir = Vector3.ClampMagnitude(climbDir, 1f);

            float timeSinceEnter = Time.time - _climbEnterTime;

            float effectiveSpeed = currentSurface.GetEffectiveSpeed(climbParams.climbSpeed);
            float lateralScale = Mathf.Clamp01(climbLateralSpeedMultiplier);
            float wave = 1f;
            if (Mathf.Abs(vertical) > 0.01f)
            {
                float phase = Time.time * (Mathf.PI * 2f) * Mathf.Max(0.01f, climbVerticalSpeedWaveFrequency);
                wave = Mathf.Max(0f, 1f + climbVerticalSpeedWaveAmplitude * Mathf.Sin(phase));
            }
            Vector3 scaledClimbDir = (wallRight * horizontal * lateralScale) + (wallUp * vertical * wave);
            scaledClimbDir = Vector3.ClampMagnitude(scaledClimbDir, 1f);

            Vector3 inputVelocity = (disableClimbInputVelocity || !hasInputRaw) ? Vector3.zero : (scaledClimbDir * effectiveSpeed);
            climbVelocityRequest = inputVelocity;

            if (sm != null)
            {
                sm.SetClimbInput(horizontal, vertical);
            }

            float checkY = (charPos + MyCore.kcc.motor.CharacterUp * climbTopCheckHeightOffset).y;
            bool nearTop = currentSurface.IsNearTop(checkY, climbParams.topReachThreshold);
            bool inRange = currentSurface.IsInHeightRange(checkY);

            float distError = _lastWallRayDistance - climbParams.wallOffset;
            Vector3 snapForce = Vector3.zero;
            bool allowSnap = !disableWallSnap
                && (timeSinceEnter <= climbParams.snapDuration || !hasInputRaw || nearTop);
            if (allowSnap && Mathf.Abs(distError) > 0.01f)
            {
                float snapVel = -distError * climbParams.snapSharpness;
                snapVel = Mathf.Clamp(snapVel, -climbParams.maxSnapSpeed, climbParams.maxSnapSpeed);
                snapForce = wallNormal * snapVel;
                climbVelocityRequest += snapForce;
            }

            if (debugClimb)
            {
                Debug.LogWarning(
                    $"[攀爬-贴墙时长] timeSinceEnter={timeSinceEnter:F2} snapDuration={climbParams.snapDuration:F2} allowSnap={allowSnap} distError={distError:F3}");
            }

            if (debugClimb)
            {
                Vector3 charFwd = MyCore.kcc.motor.CharacterForward;
                Vector3 charRight = Vector3.Cross(Vector3.up, charFwd).normalized;
                Vector3 charUp = Vector3.up;

                Vector3 inputLocal = new Vector3(
                    Vector3.Dot(inputVelocity, charRight),
                    Vector3.Dot(inputVelocity, charUp),
                    Vector3.Dot(inputVelocity, charFwd));
                Vector3 snapLocal = new Vector3(
                    Vector3.Dot(snapForce, charRight),
                    Vector3.Dot(snapForce, charUp),
                    Vector3.Dot(snapForce, charFwd));
                Vector3 totalLocal = new Vector3(
                    Vector3.Dot(climbVelocityRequest, charRight),
                    Vector3.Dot(climbVelocityRequest, charUp),
                    Vector3.Dot(climbVelocityRequest, charFwd));

                Debug.LogWarning(
                    $"[攀爬-速度] === 输入拆解 ===\n" +
                    $"  移动输入={moveInput} 墙法线={currentWallNormal:F3} 墙右方={wallRight:F3}\n" +
                    $"  左右={horizontal:F3} 上下={vertical:F3} 攀爬方向={climbDir}\n" +
                    $"=== 世界坐标 ===\n" +
                    $"  输入速度={inputVelocity} (大小={inputVelocity.magnitude:F3})\n" +
                    $"  贴墙力={snapForce} (射线距离={_lastWallRayDistance:F3} 目标距离={climbParams.wallOffset:F3} 偏差={distError:F3})\n" +
                    $"  总输出={climbVelocityRequest} (大小={climbVelocityRequest.magnitude:F3})\n" +
                    $"=== 角色自身坐标(右/上/前) ===\n" +
                    $"  输入={inputLocal:F3}\n" +
                    $"  贴墙={snapLocal:F3}\n" +
                    $"  总输出={totalLocal:F3}");
            }

            if (debugClimb)
            {
                float relY = charPos.y - currentSurface.transform.position.y;
                float topY = currentSurface.GetTopWorldY();
                float thresholdY = topY - climbParams.topReachThreshold;
                Debug.Log($"[登顶] 高度检查: charY={charPos.y:F2}, checkY={checkY:F2}, surfaceY={currentSurface.transform.position.y:F2}, " +
                    $"relY={relY:F2}, topY={topY:F2}, thresholdY={thresholdY:F2}, " +
                    $"nearTop={nearTop}(阈值={climbParams.topReachThreshold}), inRange={inRange}");
                Debug.Log($"[登顶] Surface参数: name={currentSurface.name}, areaCenter={currentSurface.areaCenter:F3}, " +
                    $"areaSize={currentSurface.areaSize:F3}, topOffset={currentSurface.topHeightOffset:F3}, " +
                    $"bottomOffset={currentSurface.bottomHeightOffset:F3}, lossyScale={currentSurface.transform.lossyScale:F3}");
            }

            if (debugClimb)
            {
                Debug.Log($"[登顶] 射线检查: hasInput={hasInputRaw}, lastWallDist={_lastWallRayDistance:F2}, maxWallDist={climbParams.maxWallDistance:F2}");
            }

            if (hasInputRaw && _lastWallRayDistance > climbParams.maxWallDistance)
            {
                if (nearTop)
                {
                    if (debugClimb)
                    {
                        Debug.LogWarning("[登顶] 近顶时失去墙面射线，允许继续登顶判断");
                    }
                }
                else
                {
                    if (debugClimb)
                        Debug.LogWarning($"[登顶][自动退出] 离墙过远: rayDist={_lastWallRayDistance:F2} > maxWallDist={climbParams.maxWallDistance:F2}");
                    ForceExitClimb();
                    return;
                }
            }

            if (nearTop)
            {
                if (!disableClimbOver)
                {
                    if (debugClimb) Debug.Log("[登顶] ★ 到达顶部，触发ClimbOver!");
                    EnterClimbOver();
                    return;
                }
                if (debugClimb) Debug.Log("[登顶] 到达顶部，但翻上被禁用，继续保持攀爬");
            }

            if (!inRange)
            {
                if (debugClimb) Debug.LogWarning("[攀爬][自动退出] 超出攀爬高度范围(inRange=false)");
                ForceExitClimb();
            }
        }

        private void UpdateMatchTargetPhase()
        {
            climbVelocityRequest = Vector3.zero;

            StateBase activeState = (subState == ClimbSubState.ClimbOver) ? _climbOverState : (_activeVaultState ?? _vaultState);

            if (enableContinuousApproach && currentSurface != null)
            {
                Vector3 charPos = MyCore.kcc.motor.TransientPosition;
                Vector3 targetPos;
                Vector3 moveTargetPos;
                Quaternion targetRot = currentSurface.GetMatchTargetRotation(charPos);

                bool isVault = subState == ClimbSubState.Vault;
                if (isVault)
                {
                    targetPos = _climbOverPhase2Active
                        ? currentSurface.GetLandingPosition(charPos)
                        : currentSurface.GetMatchTargetPosition(charPos);
                }
                else
                {
                    targetPos = _climbOverPhase2Active
                        ? currentSurface.GetLandingPosition(charPos)
                        : currentSurface.GetMatchTargetPosition(charPos);
                }
                Vector3 toTarget = targetPos - charPos;
                float dist = toTarget.magnitude;
                float landingDist = dist;
                Vector3 landingPos = targetPos;
                moveTargetPos = targetPos;
                if (subState == ClimbSubState.ClimbOver)
                {
                    landingPos = currentSurface.GetLandingPosition(charPos);
                    landingDist = Vector3.Distance(charPos, landingPos);
                    Vector3 forward = MyCore.kcc.motor.CharacterForward;
                    if (forward.sqrMagnitude < 0.0001f)
                    {
                        forward = -currentSurface.GetDynamicNormal(charPos);
                    }
                    moveTargetPos = charPos + forward.normalized * Mathf.Max(0.01f, climbOverForwardAdvanceDistance);
                }

                Vector3 toMoveTarget = moveTargetPos - charPos;
                float moveDist = toMoveTarget.magnitude;
                if (moveDist > approachStopDistance)
                {
                    float speed = Mathf.Min(approachSpeed * moveDist, approachMaxSpeed);
                    climbVelocityRequest = toMoveTarget / moveDist * speed;
                }

                if (activeState != null)
                {
                    activeState.UpdateMatchTargetTarget(targetPos, targetRot);
                }

                if (!_climbOverPhase2Active)
                {
                    StateBase phaseState = subState == ClimbSubState.Vault ? (_activeVaultState ?? _vaultState) : _climbOverState;
                    var animConfig = phaseState?.stateSharedData?.animationConfig;
                    bool useStateConfig = animConfig != null && animConfig.enableMatchTargetPhase2;
                    bool useModuleConfig = subState == ClimbSubState.ClimbOver && enableClimbOverFootPhase && !useStateConfig;

                    float trigger = useStateConfig ? animConfig.matchTargetPhase2Trigger : climbOverPhase2Trigger;
                    bool canTrigger = phaseState != null && phaseState.normalizedProgress >= trigger;

                    if ((useStateConfig || useModuleConfig) && canTrigger)
                    {
                        Vector3 phase2TargetPos = currentSurface.GetLandingPosition(charPos);
                        if (useStateConfig)
                        {
                            var preset = animConfig.matchTargetPresetPhase2;
                            phaseState.StartMatchTarget(
                                phase2TargetPos,
                                targetRot,
                                preset.bodyPart,
                                preset.startNormalizedTime,
                                preset.endNormalizedTime,
                                preset.positionWeight,
                                preset.rotationWeight
                            );
                        }
                        else
                        {
                            phaseState.StartMatchTarget(
                                phase2TargetPos,
                                targetRot,
                                climbOverFootBodyPart,
                                climbOverFootStartTime,
                                climbOverFootEndTime,
                                climbOverFootPosWeight,
                                climbOverFootRotWeight
                            );
                        }
                        _matchTargetStartTime = Time.time;
                        _climbOverPhase2Active = true;
                    }
                }

                if (debugClimb)
                {
                    if (subState == ClimbSubState.ClimbOver)
                    {
                        Debug.Log($"[登顶] 逼近目标: pos={charPos:F3} target={targetPos:F3} dist={dist:F3} vel={climbVelocityRequest:F3}");
                    }
                }

                if (subState == ClimbSubState.ClimbOver)
                {
                    float elapsed = Time.time - _matchTargetStartTime;
                    bool progressDone = _climbOverState != null && _climbOverState.normalizedProgress >= climbOverExitNormalized;
                    bool reached = landingDist <= climbOverExitDistance;

                    bool probedGrounded = CheckClimbOverGroundProbe(charPos);

                    if (MyCore.kcc.monitor.isStableOnGround)
                    {
                        if (_climbOverGroundedSince < 0f) _climbOverGroundedSince = Time.time;
                    }
                    else if (probedGrounded)
                    {
                        if (_climbOverGroundedSince < 0f) _climbOverGroundedSince = Time.time;
                    }
                    else
                    {
                        _climbOverGroundedSince = -1f;
                    }

                    bool groundedReady = _climbOverGroundedSince >= 0f
                        && Time.time - _climbOverGroundedSince >= climbOverGroundedExitDelay;

                    if (!groundedReady && landingDist > climbOverExitDistance)
                    {
                        Vector3 forward = MyCore.kcc.motor.CharacterForward;
                        if (forward.sqrMagnitude < 0.0001f)
                        {
                            forward = -currentSurface.GetDynamicNormal(charPos);
                        }
                        Vector3 up = MyCore.kcc.motor.CharacterUp;
                        Vector3 toLanding = landingPos - charPos;
                        float upGap = Mathf.Max(0f, Vector3.Dot(toLanding, up));
                        float forwardGap = Mathf.Abs(Vector3.Dot(toLanding, forward.normalized));

                        float upT = Mathf.Clamp01(upGap / Mathf.Max(0.01f, climbOverFastDistance));
                        float forwardT = Mathf.Clamp01(forwardGap / Mathf.Max(0.01f, climbOverFastDistance));

                        float upSpeed = Mathf.Lerp(climbOverUpMinSpeed, climbOverUpMaxSpeed, upT);
                        if (currentSurface != null)
                        {
                            float topY = currentSurface.GetTopWorldY() + climbOverTopHeightOffset;
                            if (charPos.y < topY)
                            {
                                upSpeed *= Mathf.Max(1f, climbOverBelowTopUpBoost);
                            }
                        }
                        float forwardSpeed = Mathf.Lerp(climbOverForwardMinSpeed, climbOverForwardMaxSpeed, forwardT);
                        float settleSpeed = Mathf.Max(climbVelocityRequest.magnitude, climbOverSettleSpeed);

                        Vector3 push = forward.normalized * forwardSpeed + up * upSpeed;
                        if (push.sqrMagnitude < 0.0001f)
                        {
                            push = forward.normalized * settleSpeed;
                        }
                        climbVelocityRequest = push;
                    }

                    bool canExit = elapsed >= climbOverMinDuration && (reached || progressDone || groundedReady);
                    bool timeout = elapsed >= climbOverMaxDuration;

                    if (canExit || timeout)
                    {
                        if (debugClimb)
                        {
                            string reason = timeout ? "timeout" : (groundedReady ? "grounded" : (reached ? "distance" : "progress"));
                            Debug.LogWarning($"[登顶][自动退出] reason={reason} elapsed={elapsed:F2} min={climbOverMinDuration:F2} max={climbOverMaxDuration:F2} landingDist={landingDist:F3} progress={_climbOverState?.normalizedProgress:F2} probedGrounded={probedGrounded}");
                        }
                        ExitClimbingState();
                        ResetClimbState();
                        _exitClimbTimestamp = Time.time;
                        return;
                    }
                }

                if (subState == ClimbSubState.Vault)
                {
                    if (dist > vaultExitDistance)
                    {
                        Vector3 forward = MyCore.kcc.motor.CharacterForward;
                        if (forward.sqrMagnitude < 0.0001f)
                        {
                            forward = -currentSurface.GetDynamicNormal(charPos);
                        }
                        float pushT = Mathf.Clamp01(dist / Mathf.Max(0.01f, vaultForwardPushDistance));
                        Vector3 up = MyCore.kcc.motor.CharacterUp;
                        Vector3 currentUp = Vector3.Project(climbVelocityRequest, up);
                        Vector3 forwardPlanar = Vector3.ProjectOnPlane(forward.normalized, up).normalized;
                        float pushSpeed = Mathf.Lerp(vaultForwardPushSpeed * 0.5f, vaultForwardPushSpeed, pushT);
                        float finalSpeed = Mathf.Max(Vector3.ProjectOnPlane(climbVelocityRequest, up).magnitude, pushSpeed);
                        climbVelocityRequest = currentUp + forwardPlanar * finalSpeed;
                    }

                    float elapsed = Time.time - _matchTargetStartTime;
                    bool probedGrounded = CheckVaultGroundProbe(charPos);
                    bool frontBackGrounded = CheckVaultFrontBackGround(charPos);
                    bool farFromStart = Vector3.Distance(charPos, _vaultStartPosition) >= vaultFarExitDistance;
                    bool allowExit = elapsed >= vaultMinDuration && (!_climbOverPhase2Active || dist <= vaultExitDistance);
                    bool allowImmediateExit = elapsed >= vaultMinDuration;
                    bool canExitByDistance = allowExit && dist <= vaultExitDistance && probedGrounded;
                    if ((allowImmediateExit && frontBackGrounded) || farFromStart || canExitByDistance || elapsed >= vaultMaxDuration)
                    {
                        if (debugClimb)
                        {
                            string reason = frontBackGrounded ? "frontBackGround" : (farFromStart ? "farFromStart" : (elapsed >= vaultMaxDuration ? "timeout" : (dist <= vaultExitDistance ? "distance" : "phase2")));
                            Debug.LogWarning($"[翻越][自动退出] reason={reason} elapsed={elapsed:F2} min={vaultMinDuration:F2} max={vaultMaxDuration:F2} dist={dist:F3} phase2={_climbOverPhase2Active} probedGrounded={probedGrounded} frontBack={frontBackGrounded} far={farFromStart}");
                        }
                        ExitClimbingState();
                        ResetClimbState();
                        _exitClimbTimestamp = Time.time;
                        return;
                    }
                }
            }

            if (subState == ClimbSubState.Vault && currentSurface != null && !enableContinuousApproach)
            {
                Vector3 charPos = MyCore.kcc.motor.TransientPosition;
                Vector3 targetPos = _climbOverPhase2Active
                    ? currentSurface.GetLandingPosition(charPos)
                    : currentSurface.GetMatchTargetPosition(charPos);
                float dist = Vector3.Distance(charPos, targetPos);
                float elapsed = Time.time - _matchTargetStartTime;
                bool probedGrounded = CheckVaultGroundProbe(charPos);
                bool frontBackGrounded = CheckVaultFrontBackGround(charPos);
                bool farFromStart = Vector3.Distance(charPos, _vaultStartPosition) >= vaultFarExitDistance;
                bool allowExit = elapsed >= vaultMinDuration && (!_climbOverPhase2Active || dist <= vaultExitDistance);
                bool allowImmediateExit = elapsed >= vaultMinDuration;
                bool canExitByDistance = allowExit && dist <= vaultExitDistance && probedGrounded;
                if ((allowImmediateExit && frontBackGrounded) || farFromStart || canExitByDistance || elapsed >= vaultMaxDuration)
                {
                    if (debugClimb)
                    {
                        string reason = frontBackGrounded ? "frontBackGround" : (farFromStart ? "farFromStart" : (elapsed >= vaultMaxDuration ? "timeout" : (dist <= vaultExitDistance ? "distance" : "phase2")));
                        Debug.LogWarning($"[翻越][自动退出] reason={reason} elapsed={elapsed:F2} min={vaultMinDuration:F2} max={vaultMaxDuration:F2} dist={dist:F3} phase2={_climbOverPhase2Active} probedGrounded={probedGrounded} frontBack={frontBackGrounded} far={farFromStart} (noApproach)");
                    }
                    ExitClimbingState();
                    ResetClimbState();
                    _exitClimbTimestamp = Time.time;
                    return;
                }
            }
            if (activeState != null && !activeState.IsMatchTargetActive && Time.time - _matchTargetStartTime > matchTargetFailTimeout)
            {
                if (debugClimb)
                {
                    Debug.LogWarning($"[攀爬][自动退出] MatchTarget未激活: elapsed={(Time.time - _matchTargetStartTime):F2} > failTimeout={matchTargetFailTimeout:F2}");
                }
                ResetClimbState();
                _exitClimbTimestamp = Time.time;
                return;
            }
            if (activeState == null || activeState.baseStatus != StateBaseStatus.Running)
            {
                if (debugClimb)
                {
                    Debug.LogWarning($"[攀爬][自动退出] activeState无效或未Running: subState={subState} active={(activeState != null ? activeState.strKey : "null")}");
                }
                ResetClimbState();
                _exitClimbTimestamp = Time.time;
            }
        }

        private void UpdateClimbJump()
        {
            float jumpElapsed = Time.time - _climbJumpStartTime;
            if (jumpElapsed >= climbParams.climbJumpMaxDuration)
            {
                if (debugClimb) Debug.LogWarning($"[攀爬跳跃][自动退出] 超时: elapsed={jumpElapsed:F2} >= {climbParams.climbJumpMaxDuration:F2}");
                ForceExitClimb();
                return;
            }

            float reattachDelay = Mathf.Max(climbParams.climbJumpReattachDelay, ClimbJumpReturnMinTime);
            if (jumpElapsed >= reattachDelay)
            {
                if (TryReturnToClimbAfterJump()) return;
            }

            if (_climbJumpState != null && _climbJumpState.baseStatus != StateBaseStatus.Running)
            {
                if (jumpElapsed >= reattachDelay)
                {
                    if (TryReturnToClimbAfterJump()) return;
                }
            }

            if (MyCore.kcc.monitor.isStableOnGround)
            {
                if (debugClimb) Debug.LogWarning("[攀爬跳跃][自动退出] 落地");
                if (_climbJumpState != null && _climbJumpState.baseStatus == StateBaseStatus.Running)
                {
                    sm.TryDeactivateState(ClimbJump_StateName);
                    if (_climbJumpState.baseStatus == StateBaseStatus.Running)
                        sm.ForceExitState(_climbJumpState);
                }
                ExitClimbingState();
                ResetClimbState();
                _exitClimbTimestamp = Time.time;
            }
        }

        private bool TryReturnToClimbAfterJump()
        {
            if (disableNormalClimb) return false;
            if (TryDetectClimbableSurface(out var surface, out var hitNormal, out var hitPoint))
            {
                if (currentSurface == null || allowClimbJumpSwitchSurface || surface == currentSurface)
                {
                    return EnterClimbing(surface, hitNormal, hitPoint);
                }
            }

            if (currentSurface == null) return false;

            RefreshWallContact(false, true);
            if (subState == ClimbSubState.None) return false;

            if (_lastWallRayDistance > climbParams.maxWallDistance) return false;

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            if (!currentSurface.IsInHeightRange(charPos.y)) return false;

            Vector3 reattachHitPoint = charPos + (-currentWallNormal) * _lastWallRayDistance;
            return EnterClimbing(currentSurface, currentWallNormal, reattachHitPoint);
        }

        private void RefreshWallContact(bool allowExit, bool forceRaycast)
        {
            if (MyCore.kcc.motor == null) return;

            _wallContactFrameCounter++;
            if (!forceRaycast && !allowExit && _wallContactFrameCounter < wallContactCheckInterval)
            {
                return;
            }
            _wallContactFrameCounter = 0;

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            Vector3 baseNormal = (currentSurface != null) ? currentSurface.GetDynamicNormal(charPos) : currentWallNormal;
            Vector3 dirToWall = -baseNormal;

            float rayLen = Mathf.Max(climbParams.maxWallDistance, forwardDetectDistance);
            if (Physics.Raycast(charPos + Vector3.up * rayHeightOffset, dirToWall, out RaycastHit hit, rayLen, climbableLayerMask))
            {
                currentWallNormal = baseNormal.normalized;
                _lastWallRayDistance = hit.distance;
            }
            else
            {
                _lastWallRayDistance = rayLen + 0.01f;
                if (allowExit)
                {
                    if (subState == ClimbSubState.Climbing || subState == ClimbSubState.Approach)
                    {
                        if (debugClimb) Debug.LogWarning("[攀爬][自动退出] 丢失墙面接触(射线未命中)");
                        ForceExitClimb();
                    }
                    else if (debugClimb)
                    {
                        Debug.LogWarning($"[攀爬][忽略退出] 丢失墙面接触但处于{subState}");
                    }
                }
            }
        }

        private void ExitClimbingState(bool includeJumpState = true)
        {
            if (_climbState != null && _climbState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(Climb_StateName);
                if (_climbState.baseStatus == StateBaseStatus.Running)
                    sm.ForceExitState(_climbState);
            }
            if (_climbOverState != null && _climbOverState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(ClimbOver_StateName);
                if (_climbOverState.baseStatus == StateBaseStatus.Running)
                    sm.ForceExitState(_climbOverState);
            }
            if (_vaultState != null && _vaultState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(Vault_StateName);
                if (_vaultState.baseStatus == StateBaseStatus.Running)
                    sm.ForceExitState(_vaultState);
            }
            if (_vaultHighState != null && _vaultHighState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(VaultHigh_StateName);
                if (_vaultHighState.baseStatus == StateBaseStatus.Running)
                    sm.ForceExitState(_vaultHighState);
            }
            if (includeJumpState && _climbJumpState != null && _climbJumpState.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(ClimbJump_StateName);
                if (_climbJumpState.baseStatus == StateBaseStatus.Running)
                    sm.ForceExitState(_climbJumpState);
            }
        }

        private void ResetClimbState()
        {
            subState = ClimbSubState.None;
            currentSurface = null;
            currentWallNormal = Vector3.zero;
            currentSnapTarget = Vector3.zero;
            climbVelocityRequest = Vector3.zero;
            climbJumpRequested = false;
            _climbEnterTime = -999f;
            _groundedSince = -1f;
            _climbJumpStartTime = -999f;
            _climbOverPhase2Active = false;
            _climbOverGroundedSince = -1f;
            _activeVaultState = null;
            _vaultStartPosition = Vector3.zero;

            if (sm != null)
            {
                sm.SetFloat(StateDefaultFloatParameter.ClimbHorizontal, 0f);
                sm.SetFloat(StateDefaultFloatParameter.ClimbVertical, 0f);
            }
        }

        private bool CheckClimbOverGroundProbe(Vector3 characterPosition)
        {
            if (MyCore?.kcc?.motor == null)
            {
                return false;
            }

            Vector3 up = MyCore.kcc.motor.CharacterUp;
            Vector3 fwd = MyCore.kcc.motor.CharacterForward;
            Vector3 right = Vector3.Cross(up, fwd).normalized;
            Vector3 originBase = characterPosition + up * climbOverGroundProbeHeightOffset;
            float r = Mathf.Max(0.01f, climbOverGroundProbeRadius);
            LayerMask mask = MyCore.kcc.motor.StableGroundLayers;

            Vector3[] offsets =
            {
                Vector3.zero,
                fwd * r,
                -fwd * r,
                right * r,
                -right * r
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 origin = originBase + offsets[i];
                if (Physics.Raycast(origin, -up, out _, climbOverGroundProbeDistance, mask))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckVaultGroundProbe(Vector3 characterPosition)
        {
            if (MyCore?.kcc?.motor == null)
            {
                return false;
            }

            Vector3 up = MyCore.kcc.motor.CharacterUp;
            Vector3 fwd = MyCore.kcc.motor.CharacterForward;
            Vector3 right = Vector3.Cross(up, fwd).normalized;
            Vector3 originBase = characterPosition + up * vaultGroundProbeHeightOffset;
            float r = Mathf.Max(0.01f, vaultGroundProbeRadius);
            LayerMask mask = MyCore.kcc.motor.StableGroundLayers;

            Vector3[] offsets =
            {
                Vector3.zero,
                fwd * r,
                -fwd * r,
                right * r,
                -right * r
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 origin = originBase + offsets[i];
                if (Physics.Raycast(origin, -up, out _, vaultGroundProbeDistance, mask))
                {
                    return true;
                }
            }

            return false;
        }

        private bool CheckVaultFrontBackGround(Vector3 characterPosition)
        {
            if (MyCore?.kcc?.motor == null)
            {
                return false;
            }

            Vector3 up = MyCore.kcc.motor.CharacterUp;
            Vector3 fwd = MyCore.kcc.motor.CharacterForward;
            if (fwd.sqrMagnitude < 0.0001f)
            {
                fwd = -currentWallNormal;
            }
            Vector3 originBase = characterPosition + up * vaultGroundProbeHeightOffset;
            float d = Mathf.Max(0.01f, vaultFrontBackProbeDistance);
            LayerMask mask = MyCore.kcc.motor.StableGroundLayers;

            Vector3 originFwd = originBase + fwd.normalized * d;
            Vector3 originBack = originBase - fwd.normalized * d;

            bool hitFwd = Physics.Raycast(originFwd, -up, out _, vaultGroundProbeDistance, mask);
            bool hitBack = Physics.Raycast(originBack, -up, out _, vaultGroundProbeDistance, mask);

            return hitFwd && hitBack;
        }

        public bool BeforeCharacterUpdate(Entity owner, EntityKCCData kcc, float deltaTime)
        {
            if (!enableClimb || kcc == null || kcc.motor == null)
            {
                return false;
            }

            kcc.motor.ForceUnground(0.1f);
            return true;
        }

        public bool UpdateRotation(Entity owner, EntityKCCData kcc, ref Quaternion currentRotation, float deltaTime)
        {
            if (!enableClimb || kcc == null || kcc.motor == null)
            {
                return false;
            }

            if ((subState == ClimbSubState.ClimbJump && allowClimbJumpFreeRotation)
                || subState == ClimbSubState.Vault)
            {
                return false;
            }

            Vector3 wallNormal = currentWallNormal;
            if (currentSurface != null)
            {
                wallNormal = currentSurface.GetDynamicNormal(kcc.motor.TransientPosition);
            }

            if (wallNormal.sqrMagnitude <= 0.01f)
            {
                return true;
            }

            Vector3 faceDir = -wallNormal;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(faceDir.normalized, kcc.motor.CharacterUp);
                float maxTurn = climbParams.climbMaxTurnSpeed;
                if (maxTurn > 0f)
                {
                    currentRotation = Quaternion.RotateTowards(currentRotation, targetRot, maxTurn * deltaTime);
                }
                else
                {
                    currentRotation = targetRot;
                }
            }

            if (debugClimb)
            {
                Debug.LogWarning(
                    $"[攀爬-朝向] 墙法线={wallNormal:F3} 面朝目标={(-wallNormal):F3} " +
                    $"角色前方={kcc.motor.CharacterForward:F3} 是否贴地={kcc.motor.GroundingStatus.IsStableOnGround}");
            }

            return true;
        }

        public bool UpdateVelocity(Entity owner, EntityKCCData kcc, ref Vector3 currentVelocity, float deltaTime)
        {
            if (!enableClimb || kcc == null || kcc.motor == null)
            {
                return false;
            }

            if (subState == ClimbSubState.ClimbJump)
            {
                if (climbJumpRequested)
                {
                    climbJumpRequested = false;
                    kcc.motor.ForceUnground(0.1f);
                    Vector3 jumpNormal = currentWallNormal;
                    if (currentSurface != null)
                    {
                        jumpNormal = currentSurface.GetDynamicNormal(kcc.motor.TransientPosition);
                    }
                    if (jumpNormal.sqrMagnitude < 0.0001f)
                    {
                        jumpNormal = -kcc.motor.CharacterForward;
                    }
                    Vector3 wallRight = Vector3.Cross(jumpNormal, kcc.motor.CharacterUp).normalized;
                    Vector3 inputPlanar = Vector3.ProjectOnPlane(kcc.moveInput, kcc.motor.CharacterUp);
                    float lateralInput = Mathf.Clamp(Vector3.Dot(inputPlanar, wallRight), -1f, 1f);
                    Vector3 jumpVel = kcc.motor.CharacterUp * (climbParams.climbJumpSpeed * ClimbJumpUpMultiplier)
                                      + jumpNormal.normalized * (climbParams.climbJumpNormalForce * ClimbJumpNormalMultiplier)
                                      + wallRight * (lateralInput * climbJumpLateralSpeed);
                    currentVelocity = jumpVel;
                }
                else
                {
                    Vector3 inputPlanar = Vector3.ProjectOnPlane(kcc.moveInput, kcc.motor.CharacterUp);
                    if (inputPlanar.sqrMagnitude > 0.0001f)
                    {
                        Vector3 targetPlanarVel = inputPlanar.normalized * ClimbJumpAirControlSpeed;
                        Vector3 currentPlanarVel = Vector3.ProjectOnPlane(currentVelocity, kcc.motor.CharacterUp);
                        Vector3 planarDiff = targetPlanarVel - currentPlanarVel;
                        currentVelocity += planarDiff * ClimbJumpAirControlSharpness * deltaTime;
                    }
                    currentVelocity += kcc.gravity_ * deltaTime;
                }
                return true;
            }

            Vector3 targetMovementVelocity = climbVelocityRequest;

            if (climbJumpRequested)
            {
                climbJumpRequested = false;
                kcc.motor.ForceUnground(0.1f);
                Vector3 jumpNormal = currentWallNormal;
                if (currentSurface != null)
                {
                    jumpNormal = currentSurface.GetDynamicNormal(kcc.motor.TransientPosition);
                }
                if (jumpNormal.sqrMagnitude < 0.0001f)
                {
                    jumpNormal = -kcc.motor.CharacterForward;
                }
                Vector3 wallRight = Vector3.Cross(jumpNormal, kcc.motor.CharacterUp).normalized;
                Vector3 inputPlanar = Vector3.ProjectOnPlane(kcc.moveInput, kcc.motor.CharacterUp);
                float lateralInput = Mathf.Clamp(Vector3.Dot(inputPlanar, wallRight), -1f, 1f);
                Vector3 jumpVel = kcc.motor.CharacterUp * (climbParams.climbJumpSpeed * ClimbJumpUpMultiplier)
                                  + jumpNormal.normalized * (climbParams.climbJumpNormalForce * ClimbJumpNormalMultiplier)
                                  + wallRight * (lateralInput * climbJumpLateralSpeed);
                currentVelocity = jumpVel;
                return true;
            }

            Vector3 velDiff = targetMovementVelocity - currentVelocity;
            currentVelocity += velDiff * climbParams.climbAcceleration * deltaTime;

            if (debugClimb)
            {
                Debug.LogWarning(
                    $"[攀爬-KCC速度] 目标={targetMovementVelocity:F3} 当前={currentVelocity:F3} " +
                    $"加速度={climbParams.climbAcceleration} 重力倍率={climbParams.gravityScale} " +
                    $"是否贴地={kcc.motor.GroundingStatus.IsStableOnGround}");
            }

            if (climbParams.gravityScale > 0f)
            {
                currentVelocity += kcc.gravity_ * (climbParams.gravityScale * deltaTime);
            }

            return true;
        }

        public override void OnDestroy()
        {
            if (subState != ClimbSubState.None)
            {
                ForceExitClimb();
            }
            if (MyCore != null && MyCore.kcc.climbModule == this)
            {
                MyCore.kcc.climbModule = null;
            }
            base.OnDestroy();
        }

        private bool TryGetModule<T>(out T module) where T : class
        {
            if (MyCore != null && MyCore.ModuleTables.TryGetValue(typeof(T), out var m))
            {
                module = m as T;
                return module != null;
            }
            module = null;
            return false;
        }

#if UNITY_EDITOR
        [Title("调试")]
        [Button("尝试攀爬(Editor)")]
        private void DebugTryClimb()
        {
            if (Application.isPlaying)
            {
                bool ok = TryStartClimb();
                Debug.Log($"[Climb] TryStartClimb => {ok}");
            }
        }

#endif
    }
}
