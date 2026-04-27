using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 按墙高范围定义不同的攀上动作配置。
    /// 适用于同一角色面对不同高度墙壁时自动切换攀上动画与 MatchTarget 参数。
    ///
    /// 使用方式：
    ///   在 EntityBasicClimbModule.heightRangePresets 数组中填写多条配置，
    ///   按 maxWallHeight 升序排列；运行时自动选第一个满足"实际墙高 ≤ maxWallHeight"的项。
    /// </summary>
    [Serializable]
    public class ClimbOverHeightRange
    {
        [LabelText("墙高上限(米)"), Tooltip("实际墙高(顶面Y - 角色脚Y) ≤ 此值时命中此配置\n填写时请按升序排列，如：1.2 / 1.8 / 2.5")]
        public float maxWallHeight = 1.5f;

        [LabelText("动画状态名"), Tooltip("对应状态机中攀上动作状态的名称，留空则跳过此配置")]
        public string stateName = "";

        [LabelText("身体部位")]
        public AvatarTarget bodyPart = AvatarTarget.LeftHand;

        [Range(0f, 1f), LabelText("开始归一时间")]
        public float startTime = 0.1f;

        [Range(0f, 1f), LabelText("结束归一时间")]
        public float endTime = 0.8f;

        [LabelText("逼近速度(单位/秒)")]
        public float approachSpeed = 3f;

        [LabelText("旋转速度(度/秒)")]
        public float angleSpeed = 180f;

        /// <summary>运行时状态缓存（Start 阶段自动填充，不参与序列化）</summary>
        [NonSerialized] public StateBase _cachedState;
    }

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
        public bool debugClimb_ = true;

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
        [HideLabel]
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

        [LabelText("攀上-逼近速度(单位/秒)")]
        public float climbOverApproachSpeed = 3f;

        [LabelText("攀上-旋转速度(度/秒)")]
        public float climbOverAngleSpeed = 180f;

        [LabelText("翻越-身体部位")]
        public AvatarTarget vaultBodyPart = AvatarTarget.Root;

        [LabelText("翻越-开始时间"), Range(0f, 1f)]
        public float vaultStartTime = 0.1f;

        [LabelText("翻越-结束时间"), Range(0f, 1f)]
        public float vaultEndTime = 0.9f;

        [LabelText("翻越-逼近速度(单位/秒)")]
        public float vaultApproachSpeed = 5f;

        [LabelText("翻越-旋转速度(度/秒)")]
        public float vaultAngleSpeed = 0f;

        [Title("翻越落地阶段")]
        [LabelText("翻越-落地身体部位"), Tooltip("第二段 MatchTarget（对面落点）使用的身体部位")]
        public AvatarTarget vaultLandingBodyPart = AvatarTarget.Root;

        [LabelText("翻越-落地开始时间"), Range(0f, 1f), Tooltip("落地阶段在动画归一时间中的起点，应在第一段结束时间之后")]
        public float vaultLandingStartTime = 0.5f;

        [LabelText("翻越-落地结束时间"), Range(0f, 1f)]
        public float vaultLandingEndTime = 0.95f;

        [LabelText("翻越-落地逼近速度(单位/秒)")]
        public float vaultLandingApproachSpeed = 3f;

        [LabelText("翻越墙顶点使用入场快照"), Tooltip("开启后，Phase0 对齐点固定为进入翻越时的墙顶点，避免每帧重算导致目标抖动")]
        public bool vaultFreezeWallTopSnapshot = true;

        [LabelText("翻越Phase1共享目标切到落点"), Tooltip("开启后，进入落地阶段时共享目标也切到落点，避免时间线配置异常时骨骼仍被墙顶目标牵制")]
        public bool vaultUseLandingAsSharedTargetInPhase1 = true;

        [LabelText("翻越最高点贴合入场位置"), Tooltip("开启后，Phase0 墙顶目标按翻越起点附近计算，不使用固定中心点")]
        public bool vaultTopUseEntryAlignedPoint = true;

        [LabelText("翻越最高点忽略固定锚点"), Tooltip("开启后，Vault Phase0 不使用 ClimbableSurface.matchTargetAnchor，改为按入场位置动态求顶点")]
        public bool vaultTopIgnoreMatchTargetAnchor = true;

        [LabelText("翻越入场锁定落点"), Tooltip("开启后仅在 EnterVault 时按玩家入场位置计算一次落点，后续不再动态刷新")]
        public bool vaultLockLandingPointAtEnter = true;

        [Title("登顶判定")]
        [LabelText("登顶高度偏移(米)")]
        public float climbTopCheckHeightOffset = 1.4f;

        [Title("翻越退出")]
        [LabelText("翻越最小持续时间(秒)")]
        public float vaultMinDuration = 0.3f;

        [LabelText("翻越最大持续时间(秒)")]
        public float vaultMaxDuration = 1.2f;

        [LabelText("翻越Phase0超时宽限(秒)"), Tooltip("当尚未进入落地阶段(phase1)时，允许在最大持续时间基础上额外等待，避免因动画前段偏慢导致提前自动退出")]
        public float vaultPhase0TimeoutGrace = 0.6f;

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

        [LabelText("攀上目标点Y额外抬高"), Tooltip("在 GetMatchTargetPosition 结果基础上额外向上偏移，防止骨骼偏移计算后脚踝卡入地表。\n0.1~0.2 为常用范围；若已在 MatchTargetPreset 配置 positionOffset 则此值可归零。")]
        public float climbOverMatchTargetYOffset = 0.1f;

        [Title("最小攀上高度")]
        [LabelText("最小攀上墙高(米)"), Tooltip("实际墙高(顶面Y - 角色脚 Y) 小于此值时不触发 ClimbOver，避免矮台阶误触发。建议 0.5~0.8")]
        public float minClimbOverWallHeight = 0.5f;

        [Title("高度自适应攀上")]
        [LabelText("启用高度分级"), Tooltip("开启后根据实际墙高自动匹配对应攀上动画状态；关闭时一律使用默认 ClimbOver 状态")]
        public bool enableHeightAdaptive = false;

        [LabelText("高度分级列表"), ShowIf("enableHeightAdaptive"),
         Tooltip("按 maxWallHeight 升序填写；运行时自动选第一个满足(实际墙高 ≤ maxWallHeight)的配置。\n示例: 1.2m 矮墙 / 1.8m 普通墙 / 2.5m 高墙")]
        public ClimbOverHeightRange[] heightRangePresets = new ClimbOverHeightRange[0];

        [Title("天花板净空检测")]
        [LabelText("启用净空检测"), Tooltip("攀上前向目标点正上方射线检查天花板，防止角色卡入桥底或低顶棚。默认开启")]
        public bool enableCeilingCheck = true;

        [LabelText("所需净空高度(米)"), ShowIf("enableCeilingCheck"), Tooltip("目标点上方需要多少米的净空才允许攀上。建议 1.8~2.2")]
        public float requiredCeilingClearance = 1.8f;

        [LabelText("净空检测层级"), ShowIf("enableCeilingCheck"), Tooltip("建议包含天花板、地板、实体物体层级；默认 ~0 全层")]
        public LayerMask ceilingCheckLayerMask = ~0;

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
        // 本次攀上实际使用的状态（可能是高度分级状态，与 _climbOverState 不同）
        private StateBase _activeClimbOverState;
        private StateBase _climbJumpState;
        private StateMachine sm;
        private StateLifecycleTracker _climbLifecycle = new StateLifecycleTracker();
        private StateLifecycleTracker _climbOverLifecycle = new StateLifecycleTracker();
        private StateLifecycleTracker _vaultLifecycle = new StateLifecycleTracker();
        private StateLifecycleTracker _climbJumpLifecycle = new StateLifecycleTracker();

        [NonSerialized] public Vector3 climbVelocityRequest;
        [NonSerialized] public bool climbJumpRequested;

        [NonSerialized] private float _lastWallRayDistance;
        [NonSerialized] private int _wallContactFrameCounter;
        [NonSerialized] private float _exitClimbTimestamp = -999f;
        [NonSerialized] private float _climbEnterTime = -999f;
        [NonSerialized] private float _groundedSince = -1f;
        [NonSerialized] private float _climbJumpStartTime = -999f;
        [NonSerialized] private float _matchTargetStartTime = -999f;
        [NonSerialized] private float _climbOverGroundedSince = -1f;
        [NonSerialized] private Vector3 _vaultStartPosition;
        // 翻越入场时（角色在入口侧）捕获一次，避免越过墙后GetDynamicNormal反转导致落点错误
        [NonSerialized] private Vector3 _vaultLandingPos;
        // 入场时捕获的墙顶对齐点（共享目标/Phase0）
        [NonSerialized] private Vector3 _vaultWallTopPos;
        // 仅用于调试可视化：false=逼近墙顶；true=逼近落点
        [NonSerialized] private bool _vaultPhase1Active;

        private const int VaultIndependentTargetLandingIndex0 = 0;
        private const int VaultIndependentTargetLandingIndex1 = 1;
        private const int VaultIndependentTargetLandingIndex2 = 2;

#if UNITY_EDITOR
        [NonSerialized] private GameObject _debugMatchTargetMarker;
#endif

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

                _climbLifecycle.Bind(sm, _climbState, ResolveStateKeyForLifecycle(_climbState, Climb_StateName));
                _climbOverLifecycle.Bind(sm, _climbOverState, ResolveStateKeyForLifecycle(_climbOverState, ClimbOver_StateName));
                _vaultLifecycle.Bind(sm, _vaultState, ResolveStateKeyForLifecycle(_vaultState, Vault_StateName));
                _climbJumpLifecycle.Bind(sm, _climbJumpState, ResolveStateKeyForLifecycle(_climbJumpState, ClimbJump_StateName));

                // ── 高度分级状态缓存 ──────────────────────────────────────────────────
                if (enableHeightAdaptive && heightRangePresets != null)
                {
                    foreach (var preset in heightRangePresets)
                    {
                        if (!string.IsNullOrEmpty(preset.stateName))
                            preset._cachedState = sm.GetStateByString(preset.stateName);
                    }
                }
            }
            if (MyCore != null)
            {
                MyCore.kcc.climbModule = this;
            }
        }

        protected override void Update()
        {
            if (MyCore == null || !enableClimb) return;

            

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
                        var effectiveClimbOver = _activeClimbOverState ?? _climbOverState;
                        anyClimbStateRunning = effectiveClimbOver != null && effectiveClimbOver.baseStatus == StateBaseStatus.Running;
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

                    if (debugClimb_)
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
                    if (debugClimb_) Debug.Log("[登顶] 进入UpdateClimbing");
                    UpdateClimbing();
                    break;

                case ClimbSubState.ClimbOver:
                case ClimbSubState.Vault:
                    if (debugClimb_)
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
            if (debugClimb_)
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
                if (debugClimb_) Debug.LogWarning("[攀爬] 攀爬跳跃已禁用，忽略请求");
                return;
            }

            if (currentSurface != null)
            {
                Vector3 charPos = MyCore.kcc.motor.TransientPosition;
                currentWallNormal = currentSurface.GetDynamicNormal(charPos);
            }

            if (_climbJumpState != null)
            {
                if (!TryActivateTrackedState(_climbJumpLifecycle, _climbJumpState, ClimbJump_StateName))
                {
                    if (debugClimb_) Debug.LogWarning($"[攀爬] 激活攀爬跳跃状态失败: {ClimbJump_StateName}");
                    return;
                }
                ExitClimbingState(false);
            }

            subState = ClimbSubState.ClimbJump;
            climbJumpRequested = true;
            _climbJumpStartTime = Time.time;

            if (debugClimb_)
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

                        if (debugClimb_)
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
                    else if (debugClimb_)
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
                if (debugClimb_) Debug.Log("[Climb] TryDetectVault: 低处射线未命中");
                return false;
            }

            var climbable = lowHit.collider.GetComponentInParent<ClimbableSurface>();
            if (climbable == null)
            {
                if (debugClimb_) Debug.Log($"[Climb] TryDetectVault: 命中{lowHit.collider.name}但无ClimbableSurface组件");
                return false;
            }

            if (climbable.surfaceType != ClimbableSurfaceType.LowWall && climbable.surfaceType != ClimbableSurfaceType.Ledge)
            {
                if (debugClimb_) Debug.Log($"[Climb] TryDetectVault: surface={climbable.name} 类型={climbable.surfaceType}，非可翻越类型");
                return false;
            }

            Vector3 closest = climbable.GetClosestPointInArea(charPos);
            float distToArea = Vector3.Distance(charPos, closest);
            if (distToArea > forwardDetectDistance)
            {
                if (debugClimb_) Debug.Log($"[Climb] TryDetectVault: 距离{distToArea:F2} > 前方射线距离{forwardDetectDistance:F2}");
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

            if (debugClimb_)
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
                if (debugClimb_) Debug.Log("[Climb] TryDetectVault: 墙顶上方有阻挡，无法翻越");
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
                if (debugClimb_) Debug.LogWarning("[攀爬] 普通攀爬已禁用，拒绝进入");
                return false;
            }
            if (_climbState == null)
            {
                if (debugClimb_) Debug.LogWarning("[Climb] EnterClimbing失败: _climbState为null! 检查状态机中是否存在名为\"" + Climb_StateName + "\"的状态");
                return false;
            }

            if (!TryActivateTrackedState(_climbLifecycle, _climbState, Climb_StateName))
            {
                if (debugClimb_) Debug.LogWarning($"[Climb] EnterClimbing失败: TryActivateState返回false (状态={Climb_StateName}, baseStatus={_climbState.baseStatus})");
                return false;
            }

            currentSurface = surface;
            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            currentWallNormal = surface.GetDynamicNormal(charPos);
            currentSnapTarget = hitPoint + currentWallNormal * climbParams.wallOffset;
            subState = ClimbSubState.Approach;
            _climbEnterTime = Time.time;

            MyCore.kcc.motor.ForceUnground(0.1f);

            if (debugClimb_)
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
                if (debugClimb_) Debug.LogWarning($"[Climb] EnterVault失败: vaultState为null! 检查状态机中是否存在名为\"{Vault_StateName}\"或\"{VaultHigh_StateName}\"的状态");
                return false;
            }

            if (!TryActivateTrackedState(_vaultLifecycle, _activeVaultState, _activeVaultState != null ? _activeVaultState.strKey : Vault_StateName))
            {
                if (debugClimb_) Debug.LogWarning($"[Climb] EnterVault失败: TryActivateState返回false (状态={_activeVaultState.strKey}, baseStatus={_activeVaultState.baseStatus})");
                return false;
            }

            currentSurface = surface;
            Vector3 charPos = MyCore.kcc.motor.TransientPosition;
            Vector3 entryNormal = surface.GetDynamicNormal(charPos);
            if (entryNormal.sqrMagnitude < 0.0001f)
            {
                entryNormal = wallNormal.sqrMagnitude > 0.0001f ? wallNormal.normalized : Vector3.back;
            }
            Vector3 up = (MyCore?.kcc?.motor != null) ? MyCore.kcc.motor.CharacterUp : Vector3.up;
            Vector3 planarEntryNormal = Vector3.ProjectOnPlane(entryNormal, up);
            if (planarEntryNormal.sqrMagnitude > 0.0001f)
            {
                entryNormal = planarEntryNormal.normalized;
            }

            // 让法线明确指向“角色所在入口侧”，避免符号反转导致落点回到原侧。
            Vector3 facePoint = surface.GetClosestClimbPoint(charPos);
            Vector3 toChar = Vector3.ProjectOnPlane(charPos - facePoint, up);
            if (toChar.sqrMagnitude > 0.0001f && Vector3.Dot(entryNormal, toChar) < 0f)
            {
                entryNormal = -entryNormal;
            }

            currentWallNormal = entryNormal;
            subState = ClimbSubState.Vault;

            _vaultStartPosition = charPos;
            // 仅在翻越入场时依据玩家当前位置计算一次“墙体另一侧最近落点”。
            // 之后默认锁定，避免第二段被动态重算拉回起点侧。
            Vector3 landingA = surface.GetLandingPositionFromEntry(charPos, currentWallNormal, true);
            float sideA = Vector3.Dot(landingA - _vaultStartPosition, -currentWallNormal);
            if (sideA <= 0.02f)
            {
                Vector3 flipped = -currentWallNormal;
                Vector3 landingB = surface.GetLandingPositionFromEntry(charPos, flipped, true);
                float sideB = Vector3.Dot(landingB - _vaultStartPosition, -flipped);
                if (sideB > sideA)
                {
                    currentWallNormal = flipped;
                    landingA = landingB;
                    sideA = sideB;
                }
            }
            _vaultLandingPos = landingA;
            _vaultPhase1Active = false;

            _matchTargetStartTime = Time.time;

            // enableContinuousApproach: 重施加策略已迁移至 StateMachineConfig.matchTargetReapply 全局配置，无需在此显式设置

            Vector3 targetPos = vaultTopUseEntryAlignedPoint
                ? surface.GetTopPositionNearEntry(charPos, vaultTopIgnoreMatchTargetAnchor)
                : surface.GetMatchTargetPosition(charPos);
            _vaultWallTopPos = targetPos; // 入场时捕获 Phase0 对齐点
            Quaternion targetRot = surface.GetMatchTargetRotation(charPos);

            // 新版独立目标点：仅将最终落点写入索引0。
            // 墙顶仍使用共享目标（Phase0），第二段由时间线 Step0 自动读取 Index0。
            UpdateVaultIndependentTargets(charPos);

            // ★ 优先采用 Inspector matchTargetPreset；Inspector 未启用时退回模块级字段（向后兼容）
            bool startedFromConfig = _activeVaultState.StartMatchTargetFromConfig(targetPos, targetRot);
            if (!startedFromConfig)
            {
                _activeVaultState.StartMatchTarget(
                    targetPos, targetRot,
                    vaultBodyPart, vaultStartTime, vaultEndTime,
                    vaultApproachSpeed, vaultAngleSpeed);
            }

            if (debugClimb_ && !_activeVaultState.IsMatchTargetActive)
            {
                Debug.LogWarning("[Climb] Vault MatchTarget未激活，请检查状态配置或Animator");
            }

            if (debugClimb_)
            {
                Debug.Log($"[Climb] ★ 进入Vault翻越! surface={surface.name}, targetPos={targetPos}, " +
                    $"targetRot={targetRot.eulerAngles}, bodyPart={vaultBodyPart}, " +
                    $"timeRange=[{vaultStartTime:F2},{vaultEndTime:F2}] state={_activeVaultState.strKey}");
                float landingSideDot = Vector3.Dot(_vaultLandingPos - _vaultStartPosition, -currentWallNormal);
                Debug.Log($"[翻越][入场落点] lockAtEnter={vaultLockLandingPointAtEnter} start={_vaultStartPosition:F3} normal={currentWallNormal:F3} landing={_vaultLandingPos:F3} sideDot={landingSideDot:F3}");
            }
            return true;
        }

        private void EnterClimbOver()
        {
            if (disableClimbOver)
            {
                if (debugClimb_) Debug.LogWarning("[攀爬] 攀爬翻上已禁用，忽略触发");
                return;
            }
            if (debugClimb_) Debug.Log("[登顶] EnterClimbOver: 准备进入登顶状态");

            if (currentSurface == null)
            {
                if (debugClimb_) Debug.LogWarning("[Climb] EnterClimbOver失败: surface=null");
                return;
            }

            Vector3 charPos = MyCore.kcc.motor.TransientPosition;

            // ── 最小墙高检测 ────────────────────────────────────────────────────────
            float wallHeight = currentSurface.GetWallHeightAboveCharacter(charPos);
            if (wallHeight < minClimbOverWallHeight)
            {
                if (debugClimb_) Debug.LogWarning($"[Climb] EnterClimbOver取消: 墙高={wallHeight:F2}m < 最小要求={minClimbOverWallHeight:F2}m，障碍过矮不触发攀上");
                return;
            }

            // ── 计算 MatchTarget 目标点 ─────────────────────────────────────────────
            Vector3 targetPos = currentSurface.GetMatchTargetPosition(charPos);
            targetPos.y += climbOverMatchTargetYOffset;
            Quaternion targetRot = currentSurface.GetMatchTargetRotation(charPos);

            // ── 天花板净空检测 ───────────────────────────────────────────────────────
            if (enableCeilingCheck && !currentSurface.HasCeilingClearance(targetPos, requiredCeilingClearance, ceilingCheckLayerMask))
            {
                if (debugClimb_) Debug.LogWarning($"[Climb] EnterClimbOver阻止: 天花板净空不足 targetPos={targetPos:F3} required={requiredCeilingClearance:F2}m，跳过攀上");
                return;
            }

            // ── 高度分级状态选择 ─────────────────────────────────────────────────────
            StateBase chosenState = _climbOverState;
            AvatarTarget chosenBodyPart = climbOverBodyPart;
            float chosenStartTime = climbOverStartTime;
            float chosenEndTime = climbOverEndTime;
            float chosenApproachSpeed = climbOverApproachSpeed;
            float chosenAngleSpeed = climbOverAngleSpeed;

            if (enableHeightAdaptive && heightRangePresets != null && heightRangePresets.Length > 0)
            {
                ClimbOverHeightRange matched = null;
                float bestMax = float.MaxValue;
                foreach (var p in heightRangePresets)
                {
                    // 在所有"墙高上限 >= 实际墙高"的配置中，选上限最小的（最精确匹配）
                    if (wallHeight <= p.maxWallHeight && p.maxWallHeight < bestMax
                        && p._cachedState != null && !string.IsNullOrEmpty(p.stateName))
                    {
                        bestMax = p.maxWallHeight;
                        matched = p;
                    }
                }
                if (matched != null)
                {
                    chosenState = matched._cachedState;
                    chosenBodyPart = matched.bodyPart;
                    chosenStartTime = matched.startTime;
                    chosenEndTime = matched.endTime;
                    chosenApproachSpeed = matched.approachSpeed;
                    chosenAngleSpeed = matched.angleSpeed;
                    if (debugClimb_) Debug.Log($"[登顶] 高度分级命中: wallHeight={wallHeight:F2}m ≤ maxWallHeight={matched.maxWallHeight:F2}m → state={matched.stateName}");
                }
                else if (debugClimb_)
                {
                    Debug.Log($"[登顶] 高度分级未命中(wallHeight={wallHeight:F2}m)，回退默认攀上状态");
                }
            }

            if (chosenState == null)
            {
                if (debugClimb_) Debug.LogWarning($"[Climb] EnterClimbOver失败: 目标状态为null! enableHeightAdaptive={enableHeightAdaptive}, wallHeight={wallHeight:F2}m, default={((_climbOverState != null) ? ClimbOver_StateName : "null")}");
                return;
            }

            // ── 退出当前攀爬状态 ─────────────────────────────────────────────────────
            if (_climbState != null && _climbState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateTrackedState(_climbLifecycle, _climbState, Climb_StateName, false);
            }

            // ── 激活选中的攀上状态 ──────────────────────────────────────────────────
            if (!TryActivateTrackedState(_climbOverLifecycle, chosenState, chosenState != null ? chosenState.strKey : ClimbOver_StateName))
            {
                if (debugClimb_) Debug.LogWarning($"[Climb] EnterClimbOver失败: TryActivateState返回false (状态={chosenState.strKey}, baseStatus={chosenState.baseStatus})");
                return;
            }
            if (debugClimb_) Debug.Log($"[登顶] EnterClimbOver: 状态激活成功 name={chosenState.strKey}, wallHeight={wallHeight:F2}m");

            _activeClimbOverState = chosenState;
            subState = ClimbSubState.ClimbOver;
            _matchTargetStartTime = Time.time;

            // ★ 优先采用 Inspector matchTargetPreset；Inspector 未启用时退回模块级字段（向后兼容）
            if (!chosenState.StartMatchTargetFromConfig(targetPos, targetRot))
            {
                chosenState.StartMatchTarget(
                    targetPos, targetRot,
                    chosenBodyPart, chosenStartTime, chosenEndTime,
                    chosenApproachSpeed, chosenAngleSpeed);
            }

            if (debugClimb_ && !chosenState.IsMatchTargetActive)
            {
                Debug.LogWarning("[Climb] ClimbOver MatchTarget未激活，请检查状态配置或Animator");
            }

            if (debugClimb_)
            {
                Debug.Log($"[Climb] ★ 进入ClimbOver! targetPos={targetPos}, targetRot={targetRot.eulerAngles}, " +
                    $"bodyPart={chosenBodyPart}, timeRange=[{chosenStartTime:F2},{chosenEndTime:F2}], " +
                    $"approachSpeed={chosenApproachSpeed:F2}单位/秒, angleSpeed={chosenAngleSpeed:F1}度/秒, wallHeight={wallHeight:F2}m");
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
            if (dist < 0.1f)
            {
                subState = ClimbSubState.Climbing;
            }

            Vector3 moveDir = (currentSnapTarget - charPos).normalized;
            float approachSpeed = Mathf.Min(climbParams.snapSharpness * dist, climbParams.maxSnapSpeed * 2f);
            climbVelocityRequest = disableApproachPull ? Vector3.zero : (moveDir * approachSpeed);

            if (debugClimb_)
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
                    if (debugClimb_) Debug.LogWarning($"[攀爬][自动退出] 持续着地超过阈值: groundedFor={(Time.time - _groundedSince):F2}s >= {climbParams.groundedExitDelay:F2}s");
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
                if (debugClimb_)
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

            if (debugClimb_)
            {
                Debug.LogWarning(
                    $"[攀爬-贴墙时长] timeSinceEnter={timeSinceEnter:F2} snapDuration={climbParams.snapDuration:F2} allowSnap={allowSnap} distError={distError:F3}");
            }

            if (debugClimb_)
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

            if (debugClimb_)
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

            if (debugClimb_)
            {
                Debug.Log($"[登顶] 射线检查: hasInput={hasInputRaw}, lastWallDist={_lastWallRayDistance:F2}, maxWallDist={climbParams.maxWallDistance:F2}");
            }

            if (hasInputRaw && _lastWallRayDistance > climbParams.maxWallDistance)
            {
                if (nearTop)
                {
                    if (debugClimb_)
                    {
                        Debug.LogWarning("[登顶] 近顶时失去墙面射线，允许继续登顶判断");
                    }
                }
                else
                {
                    if (debugClimb_)
                        Debug.LogWarning($"[登顶][自动退出] 离墙过远: rayDist={_lastWallRayDistance:F2} > maxWallDist={climbParams.maxWallDistance:F2}");
                    ForceExitClimb();
                    return;
                }
            }

            if (nearTop)
            {
                if (!disableClimbOver)
                {
                    if (debugClimb_) Debug.Log("[登顶] ★ 到达顶部，触发ClimbOver!");
                    EnterClimbOver();
                    return;
                }
                if (debugClimb_) Debug.Log("[登顶] 到达顶部，但翻上被禁用，继续保持攀爬");
            }

            if (!inRange)
            {
                if (debugClimb_) Debug.LogWarning("[攀爬][自动退出] 超出攀爬高度范围(inRange=false)");
                ForceExitClimb();
            }
        }

        private void UpdateMatchTargetPhase()
        {
            climbVelocityRequest = Vector3.zero;

            StateBase activeState = (subState == ClimbSubState.ClimbOver)
                ? (_activeClimbOverState ?? _climbOverState)
                : (_activeVaultState ?? _vaultState);

            if (enableContinuousApproach && currentSurface != null)
            {
                Vector3 charPos = MyCore.kcc.motor.TransientPosition;
                Vector3 targetPos;
                Vector3 sharedTargetPos;
                Vector3 moveTargetPos;
                Quaternion targetRot = currentSurface.GetMatchTargetRotation(charPos);

                bool isVault = subState == ClimbSubState.Vault;
                if (isVault)
                {
                    float vaultElapsed = Time.time - _matchTargetStartTime;
                    UpdateVaultIndependentTargets(charPos);
                    _vaultPhase1Active = IsVaultLandingPhaseActive(vaultElapsed);

                    // 共享目标严格保持墙顶（Phase0）。
                    // 落地阶段由时间线步骤读取独立目标槽位0，不回写共享目标，避免语义混淆。
                    sharedTargetPos = (vaultUseLandingAsSharedTargetInPhase1 && _vaultPhase1Active)
                        ? _vaultLandingPos
                        : _vaultWallTopPos;
                    targetPos = _vaultPhase1Active ? _vaultLandingPos : _vaultWallTopPos;
                }
                else
                {
                    targetPos = currentSurface.GetMatchTargetPosition(charPos);
                    targetPos.y += climbOverMatchTargetYOffset;
                    sharedTargetPos = targetPos;
                }
#if UNITY_EDITOR
                if (debugClimb_)
                {
                    if (_debugMatchTargetMarker == null)
                    {
                        _debugMatchTargetMarker = new GameObject("[DEBUG] ClimbMatchTargetPos")
                        {
                            hideFlags = UnityEngine.HideFlags.DontSave
                        };
                    }
                    _debugMatchTargetMarker.transform.position = targetPos;
                    _debugMatchTargetMarker.transform.rotation = targetRot;

                    // ── 打印完整诊断：Target位置/旋转/与角色关系 ─────────────────────────
                    Vector3 charFeet = charPos; // KCC TransientPosition 即脚部位置
                    Vector3 delta    = targetPos - charFeet;
                    Debug.Log(
                        $"[MatchTarget诊断] ══════════════════════════\n" +
                        $"  subState      = {subState}\n" +
                        $"  charPos(脚)   = {charFeet:F3}\n" +
                        $"  targetPos     = {targetPos:F3}  (含YOffset={climbOverMatchTargetYOffset:F3})\n" +
                        $"  targetRot     = {targetRot.eulerAngles:F1}\n" +
                        $"  Δpos(目标-角色) = {delta:F3}  距离={delta.magnitude:F3}\n" +
                        $"  surface       = {(currentSurface != null ? currentSurface.name : "null")}\n" +
                        $"  surfaceTopY   = {(currentSurface != null ? currentSurface.GetTopWorldY().ToString("F3") : "N/A")}\n" +
                        $"  climbOverMatchTargetYOffset = {climbOverMatchTargetYOffset:F3}\n" +
                        $"════════════════════════════════════════════");
                }
#endif
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
                    // ★ 改用 SetMatchTargetTargetWithConfigOffset：
                    //   原 SetMatchTargetTarget(targetPos, targetRot) 每帧直接写入 raw 位置，
                    //   会覆盖 StartMatchTargetFromConfig 在启动时叠加的 Inspector positionOffset/ rotationOffsetEuler。
                    //   新方法自动从当前激活阶段的 Inspector 配置读取偏移并叠加，确保 Offset 在追踪期间始终生效。
                    activeState.SetMatchTargetTargetWithConfigOffset(sharedTargetPos, targetRot);
                }

                if (debugClimb_)
                {
                    if (subState == ClimbSubState.ClimbOver)
                    {
                        Debug.Log($"[登顶] 逼近目标: pos={charPos:F3} target={targetPos:F3} dist={dist:F3} vel={climbVelocityRequest:F3}");
                    }
                }

                if (subState == ClimbSubState.ClimbOver)
                {
                    float elapsed = Time.time - _matchTargetStartTime;
                    bool progressDone = (_activeClimbOverState ?? _climbOverState) != null
                        && (_activeClimbOverState ?? _climbOverState).normalizedProgress >= climbOverExitNormalized;
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
                        if (debugClimb_)
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
                    float elapsed = Time.time - _matchTargetStartTime;
                    // Phase 0→1 已在 targetPos 计算前处理，此处仅执行退出判断

                    float vaultLandingDist = Vector3.Distance(charPos, _vaultLandingPos);
                    bool reachedLanding = elapsed >= vaultMinDuration && vaultLandingDist <= vaultExitDistance;
                    bool exitFar = elapsed >= vaultMinDuration
                        && _vaultPhase1Active
                        && Vector3.Distance(charPos, _vaultStartPosition) >= vaultFarExitDistance
                        && !reachedLanding;
                    bool timeout = IsVaultTimeout(elapsed);

                    if (reachedLanding || timeout || exitFar)
                    {
                        if (debugClimb_)
                        {
                            float progress = _activeVaultState != null ? _activeVaultState.normalizedProgress : -1f;
                            string reason = reachedLanding
                                ? "landingReached"
                                : (timeout ? (_vaultPhase1Active ? "timeout" : "timeoutBeforePhase1") : "farFromStart");
                            Debug.LogWarning($"[翻越][自动退出] reason={reason} elapsed={elapsed:F2} min={vaultMinDuration:F2} max={vaultMaxDuration:F2} grace={vaultPhase0TimeoutGrace:F2} landingDist={vaultLandingDist:F3} phase1={_vaultPhase1Active} progress={progress:F2} landingStart={vaultLandingStartTime:F2}");
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
                float elapsed = Time.time - _matchTargetStartTime;
                UpdateVaultIndependentTargets(charPos);
                _vaultPhase1Active = IsVaultLandingPhaseActive(elapsed);

                float vaultLandingDist = Vector3.Distance(charPos, _vaultLandingPos);
                bool reachedLanding = elapsed >= vaultMinDuration && vaultLandingDist <= vaultExitDistance;
                bool exitFar = elapsed >= vaultMinDuration
                    && _vaultPhase1Active
                    && Vector3.Distance(charPos, _vaultStartPosition) >= vaultFarExitDistance
                    && !reachedLanding;
                bool timeout = IsVaultTimeout(elapsed);

                if (reachedLanding || timeout || exitFar)
                {
                    if (debugClimb_)
                    {
                        float progress = _activeVaultState != null ? _activeVaultState.normalizedProgress : -1f;
                        string reason = reachedLanding
                            ? "landingReached"
                            : (timeout ? (_vaultPhase1Active ? "timeout" : "timeoutBeforePhase1") : "farFromStart");
                        Debug.LogWarning($"[翻越][自动退出] reason={reason} elapsed={elapsed:F2} min={vaultMinDuration:F2} max={vaultMaxDuration:F2} grace={vaultPhase0TimeoutGrace:F2} landingDist={vaultLandingDist:F3} phase1={_vaultPhase1Active} progress={progress:F2} landingStart={vaultLandingStartTime:F2} (noApproach)");
                    }
                    ExitClimbingState();
                    ResetClimbState();
                    _exitClimbTimestamp = Time.time;
                    return;
                }
            }
            // Vault 由超时 / 过远统一退出，不用 failTimeout（两段 MT 之间有正常间隙，会误触发）
            if (subState != ClimbSubState.Vault
                && activeState != null && !activeState.IsMatchTargetActive && Time.time - _matchTargetStartTime > matchTargetFailTimeout)
            {
                if (debugClimb_)
                {
                    Debug.LogWarning($"[攀爬][自动退出] MatchTarget未激活: elapsed={(Time.time - _matchTargetStartTime):F2} > failTimeout={matchTargetFailTimeout:F2}");
                }
                ResetClimbState();
                _exitClimbTimestamp = Time.time;
                return;
            }
            if (activeState == null || activeState.baseStatus != StateBaseStatus.Running)
            {
                if (debugClimb_)
                {
                    Debug.LogWarning($"[攀爬][自动退出] activeState无效或未Running: subState={subState} active={(activeState != null ? activeState.strKey : "null")}");
                }
                ResetClimbState();
                _exitClimbTimestamp = Time.time;
            }
        }

        private bool IsVaultTimeout(float elapsed)
        {
            if (elapsed < vaultMaxDuration)
            {
                return false;
            }

            if (_vaultPhase1Active)
            {
                return true;
            }

            float progress = _activeVaultState != null ? _activeVaultState.normalizedProgress : 0f;
            bool stillInPhase0 = progress + 0.0001f < vaultLandingStartTime;
            if (stillInPhase0 && elapsed < vaultMaxDuration + Mathf.Max(0f, vaultPhase0TimeoutGrace))
            {
                return false;
            }

            return true;
        }

        private bool IsVaultLandingPhaseActive(float elapsed)
        {
            float progress = _activeVaultState != null ? _activeVaultState.normalizedProgress : 0f;
            if (progress + 0.0001f >= vaultLandingStartTime)
            {
                return true;
            }

            // 兜底：若状态机进度异常未更新，按持续时间估算进入第二段，避免角色长期卡在墙顶阶段。
            float fallbackElapsed = Mathf.Lerp(vaultMinDuration, vaultMaxDuration, Mathf.Clamp01(vaultLandingStartTime));
            return elapsed >= fallbackElapsed;
        }

        private void UpdateClimbJump()
        {
            float jumpElapsed = Time.time - _climbJumpStartTime;
            if (jumpElapsed >= climbParams.climbJumpMaxDuration)
            {
                if (debugClimb_) Debug.LogWarning($"[攀爬跳跃][自动退出] 超时: elapsed={jumpElapsed:F2} >= {climbParams.climbJumpMaxDuration:F2}");
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
                if (debugClimb_) Debug.LogWarning("[攀爬跳跃][自动退出] 落地");
                if (_climbJumpState != null && _climbJumpState.baseStatus == StateBaseStatus.Running)
                {
                    TryDeactivateTrackedState(_climbJumpLifecycle, _climbJumpState, ClimbJump_StateName, true);
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
                        if (debugClimb_) Debug.LogWarning("[攀爬][自动退出] 丢失墙面接触(射线未命中)");
                        ForceExitClimb();
                    }
                    else if (debugClimb_)
                    {
                        Debug.LogWarning($"[攀爬][忽略退出] 丢失墙面接触但处于{subState}");
                    }
                }
            }
        }

        private void UpdateVaultIndependentTargets(Vector3 charPos)
        {
            if (currentSurface == null || _activeVaultState == null)
            {
                return;
            }

            // Vault 落点固定为入场计算结果；这里不再按运行时位置重算，避免第二段目标回跳。

            if (!vaultFreezeWallTopSnapshot)
            {
                Vector3 topSamplePos = vaultTopUseEntryAlignedPoint ? _vaultStartPosition : charPos;
                _vaultWallTopPos = vaultTopUseEntryAlignedPoint
                    ? currentSurface.GetTopPositionNearEntry(topSamplePos, vaultTopIgnoreMatchTargetAnchor)
                    : currentSurface.GetMatchTargetPosition(topSamplePos);
            }
            Quaternion landingRot = currentSurface.GetMatchTargetRotation(_vaultLandingPos);

            // 为了兼容不同时间线配置（例如存在多个独立步骤），将落地点镜像写入 0/1/2 槽位。
            // Vault 业务语义仍是“墙顶共享、落地独立”，这里仅做运行时容错，避免配置顺序导致第二段失效。
            _activeVaultState.SetMatchTargetIndependentTarget(VaultIndependentTargetLandingIndex0, _vaultLandingPos, landingRot, true);
            _activeVaultState.SetMatchTargetIndependentTarget(VaultIndependentTargetLandingIndex1, _vaultLandingPos, landingRot, true);
            _activeVaultState.SetMatchTargetIndependentTarget(VaultIndependentTargetLandingIndex2, _vaultLandingPos, landingRot, true);
        }

        private void ClearVaultIndependentTargets(StateBase vaultState)
        {
            if (vaultState == null)
            {
                return;
            }

            vaultState.DisableMatchTargetIndependentTarget(VaultIndependentTargetLandingIndex0);
            vaultState.DisableMatchTargetIndependentTarget(VaultIndependentTargetLandingIndex1);
            vaultState.DisableMatchTargetIndependentTarget(VaultIndependentTargetLandingIndex2);
        }

        private void ExitClimbingState(bool includeJumpState = true)
        {
            ClearVaultIndependentTargets(_activeVaultState);
            ClearVaultIndependentTargets(_vaultState);
            ClearVaultIndependentTargets(_vaultHighState);

            if (_climbState != null && _climbState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateTrackedState(_climbLifecycle, _climbState, Climb_StateName, true);
            }
            if (_climbOverState != null && _climbOverState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateTrackedState(_climbOverLifecycle, _climbOverState, ClimbOver_StateName, true);
            }
            // 高度自适应：若本次使用了不同于默认的攀上状态，也要确保退出
            if (_activeClimbOverState != null && _activeClimbOverState != _climbOverState
                && _activeClimbOverState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateTrackedState(_climbOverLifecycle, _activeClimbOverState, _activeClimbOverState.strKey, true);
            }
            if (_vaultState != null && _vaultState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateTrackedState(_vaultLifecycle, _vaultState, Vault_StateName, true);
            }
            if (_vaultHighState != null && _vaultHighState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateTrackedState(_vaultLifecycle, _vaultHighState, VaultHigh_StateName, true);
            }
            if (includeJumpState && _climbJumpState != null && _climbJumpState.baseStatus == StateBaseStatus.Running)
            {
                TryDeactivateTrackedState(_climbJumpLifecycle, _climbJumpState, ClimbJump_StateName, true);
            }
        }

        private bool TryActivateTrackedState(StateLifecycleTracker lifecycle, StateBase state, string fallbackKey)
        {
            if (lifecycle == null || sm == null || state == null)
                return false;

            lifecycle.Bind(sm, state, ResolveStateKeyForLifecycle(state, fallbackKey));
            bool activated = state.baseStatus == StateBaseStatus.Running || sm.TryActivateState(state);
            if (!activated)
                return false;

            if (lifecycle.TryEnter(true))
                return true;

            return lifecycle.IsActive;
        }

        private void TryDeactivateTrackedState(StateLifecycleTracker lifecycle, StateBase state, string fallbackKey, bool forceIfStillRunning)
        {
            if (sm == null || state == null)
                return;

            if (lifecycle != null)
            {
                lifecycle.Bind(sm, state, ResolveStateKeyForLifecycle(state, fallbackKey));
                if (!lifecycle.RequestExit() && state.baseStatus == StateBaseStatus.Running)
                    sm.TryDeactivateState(ResolveStateKeyForLifecycle(state, fallbackKey));
            }
            else if (state.baseStatus == StateBaseStatus.Running)
            {
                sm.TryDeactivateState(ResolveStateKeyForLifecycle(state, fallbackKey));
            }

            if (forceIfStillRunning && state.baseStatus == StateBaseStatus.Running)
                sm.ForceExitState(state);
        }

        private string ResolveStateKeyForLifecycle(StateBase state, string fallbackKey)
        {
            if (state != null && !string.IsNullOrEmpty(state.strKey))
                return state.strKey;

            return string.IsNullOrEmpty(fallbackKey) ? string.Empty : fallbackKey;
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
            _climbOverGroundedSince = -1f;
            _activeVaultState = null;
            _activeClimbOverState = null;
            _vaultStartPosition = Vector3.zero;
            _vaultLandingPos = Vector3.zero;
            _vaultWallTopPos = Vector3.zero;
            _vaultPhase1Active = false;

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

            if (debugClimb_)
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

            if (debugClimb_)
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
