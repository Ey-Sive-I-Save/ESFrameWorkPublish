using UnityEngine;
using System;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;

namespace ES
{
    // ============================================================================
    // 文件：StateFinalIKDriver.cs
    //
    // 职责：把 StateMachine 计算出的最终 IK Pose 驱动到 FinalIK 所有已就绪的求解器。
    //
    // ┌─ 功能分组（按 GameObject 上实际挂载的 FinalIK 组件自动激活）──────────────┐
    // │ [BipedIK]        四肢 IK（左/右 手脚 位置+旋转）+ 内置 LookAt 兜底       │
    // │ [LookAtIK]       独立多骨骼注视（比 BipedIK 内置更精细，有则优先）        │
    // │ [AimIK]          骨链瞄准（武器持握 / 身体对准目标点），外部 API 驱动     │
    // │ [GrounderBipedIK]配合 BipedIK 的地形自适应脚步接地（事件委托驱动）        │
    // │ [HitReaction]    受击程序动画（需要 FullBodyBipedIK），外部 API 驱动      │
    // │ [Recoil]         武器后坐力程序动画（需要 FullBodyBipedIK），外部 API 驱动│
    // └──────────────────────────────────────────────────────────────────────────┘
    //
    // 初始化契约：
    //   StateMachine.BindToAnimator → Bind() → FinalIKComponentRefs.Scan() 一次扫描
    //   → 各组 Init 方法；失败则 _xxxReady = false 并记录原因。
    //   运行时热路径只检查 _xxxReady bool，零 null check、零 GetComponent、零 GC。
    //
    // 执行顺序：
    //   Driver LateUpdate（order -1）→ 设置 LookAtIK/AimIK solver 参数
    //   → FinalIK 组件 LateUpdate（order 0）→ LookAtIK/AimIK 自动求解
    //   BipedIK 已 disabled，由 Driver 手动调用 UpdateBipedIK()。
    //
    // Bridge 删除说明（原 FinalIKBipedIKBridge）：
    //   - Bridge 是 internal sealed，Driver 唯一使用者，无重用价值。
    //   - JIT 对 private sealed 方法实施内联，间接调用性能等同直接调用。
    //   - 扩展到多求解器后 Bridge 模式会产生大量小类，#region 分组更清晰。
    // ============================================================================

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1)]
    public sealed class StateFinalIKDriver : MonoBehaviour
    {
        // ════════════════════════════════════════════════════════════════════════
        // Inspector 参数
        // ════════════════════════════════════════════════════════════════════════

        // ── Tab: 初始化 ───────────────────────────────────────────────────────

        [TabGroup("IKDriver", "初始化", Order = 0,
            TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾蓝\")")]
        [BoxGroup("IKDriver/初始化/全局策略", ShowLabel = true)]
        [LabelText("自动识别骨骼引用")]
        [Tooltip("BipedIK.references 缺失时自动识别骨骼引用（Humanoid 骨架）。在 Bind 阶段执行。")]
        [SerializeField] private bool autoDetectReferencesIfMissing = true;

        [TabGroup("IKDriver", "初始化")]
        [BoxGroup("IKDriver/初始化/全局策略")]
        [LabelText("输出缺失组件提示")]
        [Tooltip("Bind 时对未挂载的可选功能组件输出 Info 级别日志提示。")]
        [SerializeField] private bool logMissingComponentHints = false;

        [TabGroup("IKDriver", "初始化")]
        [BoxGroup("IKDriver/初始化/全局策略")]
        [LabelText("有权重无 IK 时警告")]
        [Tooltip("finalIKPose 有权重但 BipedIK 未就绪时，每隔 2 秒输出一次节流 Warning。")]
        [SerializeField] private bool warnWhenPoseHasWeightButNoIK = true;

        [TabGroup("IKDriver", "初始化")]
        [BoxGroup("IKDriver/初始化/全局策略")]
        [LabelText("热插拔重试间隔 (秒)")]
        [Tooltip("运行时动态挂载 BipedIK 后的重绑轮询间隔。0 = 每帧重试（不推荐）。")]
        [SerializeField] private float rebindInterval = 0.5f;

        // ── Tab: 功能开关 ─────────────────────────────────────────────────────

        [TabGroup("IKDriver", "功能开关", Order = 1,
            TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾绿\")")]
        [BoxGroup("IKDriver/功能开关/启用功能", ShowLabel = true)]
        [LabelText("BipedIK  |  四肢 IK + LookAt 兜底")]
        [Tooltip("BipedIK：驱动左/右手脚四肢位置+旋转，含内置 LookAt 兜底。ES IK 主驱动，Driver 手动调用 UpdateBipedIK()。\n禁用后彻底跳过，性能零损失。")]
        [SerializeField] public bool enableBipedIK = true;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/启用功能")]
        [LabelText("GrounderBipedIK  |  地形脚步接地")]
        [Tooltip("GrounderBipedIK：地形自适应脚步接地（需同时启用 BipedIK）。通过 BipedIK 求解器事件委托驱动。")]
        [EnableIf("enableBipedIK")]
        [SerializeField] internal bool enableGrounderBipedIK = true;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/启用功能")]
        [LabelText("LookAtIK  |  独立多骨骼注视")]
        [Tooltip("LookAtIK：头/颈/脊椎分层注视，比 BipedIK 内置 LookAt 更平滑。存在时自动覆盖兜底；不存在则自动降级。\n禁用后不初始化，性能零损失。")]
        [SerializeField] internal bool enableLookAtIK = true;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/启用功能")]
        [LabelText("AimIK  |  骨链瞄准")]
        [Tooltip("AimIK：将骨链末端对准目标点（武器持握/身体对准）。通过 SetAimTarget() / SetAimWeight() 外部驱动，权重 0 时不影响动画。")]
        [SerializeField] internal bool enableAimIK = true;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/启用功能")]
        [LabelText("FullBodyBipedIK  |  全身 IK（HitReaction/Recoil 前提）")]
        [Tooltip("FullBodyBipedIK：全身 IK 系统，HitReaction / Recoil 的前提依赖。禁用后两者均不可用。")]
        [SerializeField] internal bool enableFullBodyBipedIK = true;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/启用功能")]
        [LabelText("HitReaction  |  受击程序动画")]
        [Tooltip("HitReaction：受击程序动画（需 FullBodyBipedIK）。禁用后 TriggerHitReaction() 调用无效。")]
        [EnableIf("enableFullBodyBipedIK")]
        [SerializeField] internal bool enableHitReaction = true;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/启用功能")]
        [LabelText("Recoil  |  后坐力程序动画")]
        [Tooltip("Recoil：武器后坐力程序动画（需 FullBodyBipedIK）。禁用后 TriggerRecoil() 调用无效。")]
        [EnableIf("enableFullBodyBipedIK")]
        [SerializeField] internal bool enableRecoil = true;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/自动添加组件", ShowLabel = true)]
        [InfoBox("Bind 时若目标组件缺失则自动 AddComponent。部分组件（AimIK / HitReaction / Recoil）仍需手动补充参数配置。", InfoMessageType.None)]
        [LabelText("BipedIK")]
        [Tooltip("Bind 时若未找到 BipedIK，自动 AddComponent<BipedIK>()。Humanoid 骨架支持 AutoDetectReferences，零配置可用。")]
        [EnableIf("enableBipedIK")]
        [SerializeField] private bool autoAddBipedIK = false;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/自动添加组件")]
        [LabelText("GrounderBipedIK")]
        [Tooltip("Bind 时若未找到 GrounderBipedIK，自动 AddComponent<GrounderBipedIK>()。OnEnable 时自动扫描 BipedIK solver，可安全自动添加。")]
        [EnableIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] private bool autoAddGrounderBipedIK = false;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/自动添加组件")]
        [LabelText("LookAtIK")]
        [Tooltip("Bind 时若未找到 LookAtIK，自动 AddComponent<LookAtIK>()，Humanoid 骨架自动填充 solver.head 和 solver.spine。")]
        [EnableIf("enableLookAtIK")]
        [SerializeField] private bool autoAddLookAtIK = false;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/自动添加组件")]
        [LabelText("AimIK")]
        [Tooltip("Bind 时若未找到 AimIK，自动 AddComponent<AimIK>()。\n注意：仍需手动配置 solver.bones 骨链和 solver.axis 瞄准轴。")]
        [EnableIf("enableAimIK")]
        [SerializeField] private bool autoAddAimIK = false;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/自动添加组件")]
        [LabelText("FullBodyBipedIK")]
        [Tooltip("Bind 时若未找到 FullBodyBipedIK，自动 AddComponent<FullBodyBipedIK>() 并调用 AutoDetectReferences。")]
        [EnableIf("enableFullBodyBipedIK")]
        [SerializeField] private bool autoAddFullBodyBipedIK = false;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/自动添加组件")]
        [LabelText("HitReaction")]
        [Tooltip("Bind 时若未找到 HitReaction，自动 AddComponent<HitReaction>()。\n注意：仍需手动配置 hitPoints 列表（被击部位 Collider + 权重曲线）。")]
        [EnableIf("@enableFullBodyBipedIK && enableHitReaction")]
        [SerializeField] private bool autoAddHitReaction = false;

        [TabGroup("IKDriver", "功能开关")]
        [BoxGroup("IKDriver/功能开关/自动添加组件")]
        [LabelText("Recoil")]
        [Tooltip("Bind 时若未找到 Recoil，自动 AddComponent<Recoil>()。\n注意：仍需手动配置 recoilOffset 列表及 positionOffset / rotationOffset 曲线。")]
        [EnableIf("@enableFullBodyBipedIK && enableRecoil")]
        [SerializeField] private bool autoAddRecoil = false;

        // ── Tab: 组件引用 ─────────────────────────────────────────────────────

        [TabGroup("IKDriver", "组件引用", Order = 2,
            TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾紫\")")]
        [BoxGroup("IKDriver/组件引用/四肢 IK", ShowLabel = true)]
        [LabelText("BipedIK")]
        [Tooltip("提前拖入 BipedIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal BipedIK presetBipedIK;

        [TabGroup("IKDriver", "组件引用")]
        [BoxGroup("IKDriver/组件引用/四肢 IK")]
        [LabelText("GrounderBipedIK")]
        [Tooltip("提前拖入 GrounderBipedIK 组件。留空则自动查找。")]
        [ShowIf("@enableBipedIK && enableGrounderBipedIK")]
        [SerializeField] internal GrounderBipedIK presetGrounderBipedIK;

        [TabGroup("IKDriver", "组件引用")]
        [BoxGroup("IKDriver/组件引用/注视·瞄准", ShowLabel = true)]
        [LabelText("LookAtIK")]
        [Tooltip("提前拖入 LookAtIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableLookAtIK")]
        [SerializeField] internal LookAtIK presetLookAtIK;

        [TabGroup("IKDriver", "组件引用")]
        [BoxGroup("IKDriver/组件引用/注视·瞄准")]
        [LabelText("AimIK")]
        [Tooltip("提前拖入 AimIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableAimIK")]
        [SerializeField] internal AimIK presetAimIK;

        [TabGroup("IKDriver", "组件引用")]
        [BoxGroup("IKDriver/组件引用/全身 IK·程序动画", ShowLabel = true)]
        [LabelText("FullBodyBipedIK")]
        [Tooltip("提前拖入 FullBodyBipedIK 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("enableFullBodyBipedIK")]
        [SerializeField] internal FullBodyBipedIK presetFullBodyBipedIK;

        [TabGroup("IKDriver", "组件引用")]
        [BoxGroup("IKDriver/组件引用/全身 IK·程序动画")]
        [LabelText("HitReaction")]
        [Tooltip("提前拖入 HitReaction 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("@enableFullBodyBipedIK && enableHitReaction")]
        [SerializeField] internal HitReaction presetHitReaction;

        [TabGroup("IKDriver", "组件引用")]
        [BoxGroup("IKDriver/组件引用/全身 IK·程序动画")]
        [LabelText("Recoil")]
        [Tooltip("提前拖入 Recoil 组件。留空则自动查找；仍缺失时根据自动添加开关处理。")]
        [ShowIf("@enableFullBodyBipedIK && enableRecoil")]
        [SerializeField] internal Recoil presetRecoil;

        // ── Tab: BipedIK 参数 ─────────────────────────────────────────────────

        [TabGroup("IKDriver", "BipedIK 参数", Order = 8,
            TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾棕\")")]
        [BoxGroup("IKDriver/BipedIK 参数/输出设置", ShowLabel = true)]
        [LabelText("脚部旋转权重倍率")]
        [Tooltip("脚部旋转权重倍率。0=只驱动位置（最稳）。建议从 0 开始逐步调至 0.2~1。")]
        [Range(0f, 1f)]
        [SerializeField] private float footRotationWeightMultiplier = 0.2f;

        [TabGroup("IKDriver", "BipedIK 参数")]
        [BoxGroup("IKDriver/BipedIK 参数/输出设置")]
        [LabelText("从 Pose 驱动目标点")]
        [Tooltip("true: pose 变化时将目标写入 Goal Transform（可视化）。\n"
               + "false: 不覆盖，让你手动拖动目标点作为 IK 输入。")]
        [SerializeField] private bool driveGoalTargetsFromPose = true;

        // ── Tab: 空闲注视 ─────────────────────────────────────────────────────
        // 角色无注视目标时，BipedIK LookAt solver 仍激活，
        // 这些权重控制骨骼保持自然前视姿态的程度（0=完全放松，1=强制朝前）。

        [TabGroup("IKDriver", "空闲注视", Order = 9,
            TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾灰\")")]
        [BoxGroup("IKDriver/空闲注视/无目标时骨骼权重", ShowLabel = true)]
        [LabelText("身体权重")]
        [Tooltip("无注视目标时，身体骨骼跟随量。建议 0.3~0.6，过高会让身体持续前倾。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultBodyWeight = 0.5f;

        [TabGroup("IKDriver", "空闲注视")]
        [BoxGroup("IKDriver/空闲注视/无目标时骨骼权重")]
        [HorizontalGroup("IKDriver/空闲注视/无目标时骨骼权重/HW", LabelWidth = 80)]
        [LabelText("头部权重")]
        [Tooltip("无注视目标时，头部骨骼跟随量。建议 0.8~1.0。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultHeadWeight = 1.0f;

        [TabGroup("IKDriver", "空闲注视")]
        [BoxGroup("IKDriver/空闲注视/无目标时骨骼权重")]
        [HorizontalGroup("IKDriver/空闲注视/无目标时骨骼权重/HW", LabelWidth = 80)]
        [LabelText("眼部权重")]
        [Tooltip("无注视目标时，眼部骨骼跟随量。建议 0.8~1.0。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultEyesWeight = 1.0f;

        [TabGroup("IKDriver", "空闲注视")]
        [BoxGroup("IKDriver/空闲注视/无目标时骨骼权重")]
        [LabelText("限制权重 (ClampWeight)")]
        [Tooltip("限制骨骼最大偏转角度（0=不限，1=完全锁死朝前）。建议 0.4~0.6。")]
        [Range(0f, 1f)]
        [SerializeField] private float bipedLookAtDefaultClampWeight = 0.5f;

        // ── Tab: 辅助变换 ─────────────────────────────────────────────────────

        [TabGroup("IKDriver", "辅助变换", Order = 10,
            TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾黄\")")]
        [BoxGroup("IKDriver/辅助变换/目标点 (空则自动创建)", ShowLabel = true)]
        [HorizontalGroup("IKDriver/辅助变换/目标点 (空则自动创建)/手", LabelWidth = 60)]
        [LabelText("左手")]
        [Tooltip("左手 IK 目标点。留空则自动创建（在层级可见，可在 Scene 视图拖动调试）。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalLH;

        [TabGroup("IKDriver", "辅助变换")]
        [BoxGroup("IKDriver/辅助变换/目标点 (空则自动创建)")]
        [HorizontalGroup("IKDriver/辅助变换/目标点 (空则自动创建)/手", LabelWidth = 60)]
        [LabelText("右手")]
        [Tooltip("右手 IK 目标点。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalRH;

        [TabGroup("IKDriver", "辅助变换")]
        [BoxGroup("IKDriver/辅助变换/目标点 (空则自动创建)")]
        [HorizontalGroup("IKDriver/辅助变换/目标点 (空则自动创建)/脚", LabelWidth = 60)]
        [LabelText("左脚")]
        [Tooltip("左脚 IK 目标点。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalLF;

        [TabGroup("IKDriver", "辅助变换")]
        [BoxGroup("IKDriver/辅助变换/目标点 (空则自动创建)")]
        [HorizontalGroup("IKDriver/辅助变换/目标点 (空则自动创建)/脚", LabelWidth = 60)]
        [LabelText("右脚")]
        [Tooltip("右脚 IK 目标点。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preGoalRF;

        [TabGroup("IKDriver", "辅助变换")]
        [BoxGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)", ShowLabel = true)]
        [HorizontalGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)/肘", LabelWidth = 60)]
        [LabelText("左肘")]
        [Tooltip("左肘弯曲方向提示（Hint），控制肘部朝向。留空则自动创建（HideInHierarchy）。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintLH;

        [TabGroup("IKDriver", "辅助变换")]
        [BoxGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)")]
        [HorizontalGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)/肘", LabelWidth = 60)]
        [LabelText("右肘")]
        [Tooltip("右肘弯曲方向提示（Hint），控制肘部朝向。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintRH;

        [TabGroup("IKDriver", "辅助变换")]
        [BoxGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)")]
        [HorizontalGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)/膝", LabelWidth = 60)]
        [LabelText("左膝")]
        [Tooltip("左膝弯曲方向提示（Hint），控制膝盖朝向。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintLF;

        [TabGroup("IKDriver", "辅助变换")]
        [BoxGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)")]
        [HorizontalGroup("IKDriver/辅助变换/弯曲提示点 (空则自动创建，层级隐藏)/膝", LabelWidth = 60)]
        [LabelText("右膝")]
        [Tooltip("右膝弯曲方向提示（Hint），控制膝盖朝向。")]
        [ShowIf("enableBipedIK")]
        [SerializeField] internal Transform preHintRF;

        // ════════════════════════════════════════════════════════════════════════
        // 核心绑定状态
        // ════════════════════════════════════════════════════════════════════════

        private StateMachine           _stateMachine;
        private Animator               _animator;
        private readonly FinalIKComponentRefs _refs = new FinalIKComponentRefs();
        private FinalIKCapabilityFlags _caps;          // 已就绪的功能集
        private FinalIKCapabilityFlags _presentButBad; // 组件存在但初始化失败

        // ════════════════════════════════════════════════════════════════════════
        // Feature Group: BipedIK — 四肢 IK（左/右 手脚）+ LookAt 兜底
        // 驱动方式：disabled 后由 Driver 手动调 UpdateBipedIK()
        // ════════════════════════════════════════════════════════════════════════

        private BipedIK _bipedIK;
        private bool    _bipedIKReady;            // 热路径唯一守卫
        private string  _bipedIKError = string.Empty;

        // Goal Transform（Inspector 可视化 + "手动拖目标点"模式输入）
        private Transform _lhTarget, _rhTarget, _lfTarget, _rfTarget;
        private bool      _goalTargetsInit;

        // Hint Transform（膝/肘弯曲方向辅助，HideInHierarchy）
        private Transform _lhHint, _rhHint, _lfHint, _rfHint;

        // Pose 脏检测（只在 pose 变化时写入 SetIK*，避免无效写入）
        private StateIKPose _lastPose;
        private bool        _hasLastPose;
        private bool        _wasBipedIKBound;

        // Goal Transform 快照（"手动拖目标点"模式下的脏检测）
        private Vector3    _sn_lhP, _sn_rhP, _sn_lfP, _sn_rfP;
        private Quaternion _sn_lhR, _sn_rhR, _sn_lfR, _sn_rfR;

        // ════════════════════════════════════════════════════════════════════════
        // Feature Group: LookAtIK — 独立多骨骼注视
        // 驱动方式：保持 enabled，Driver 在 LateUpdate(-1) 写 solver 参数，
        //           LookAtIK.LateUpdate(0) 自动求解（执行顺序保证在 Driver 之后）
        // ════════════════════════════════════════════════════════════════════════

        private LookAtIK _lookAtIK;
        private bool     _lookAtIKReady;

        // ════════════════════════════════════════════════════════════════════════
        // Feature Group: AimIK — 骨链瞄准（武器 / 身体对准）
        // 驱动方式：同 LookAtIK（保持 enabled，Driver 写参数，组件自求解）
        // ════════════════════════════════════════════════════════════════════════

        private AimIK  _aimIK;
        private bool   _aimIKReady;

        // ════════════════════════════════════════════════════════════════════════
        // Feature Group: GrounderBipedIK — 地形自适应脚步接地
        // 驱动方式：通过 BipedIK 求解器事件委托（OnPreUpdate/OnPostUpdate）驱动，
        //           与 BipedIK enabled 状态无关，UpdateBipedIK() 时自动触发
        // ════════════════════════════════════════════════════════════════════════

        private GrounderBipedIK _grounderBipedIK;
        private bool            _grounderReady;

        // ════════════════════════════════════════════════════════════════════════
        // Feature Group: HitReaction + Recoil — 程序动画工具（需要 FBBIK）
        // 驱动方式：外部通过 TriggerHitReaction / TriggerRecoil 一次性触发
        // ════════════════════════════════════════════════════════════════════════

        private HitReaction _hitReaction;
        private bool        _hitReactionReady;
        private Recoil      _recoil;
        private bool        _recoilReady;

        // ════════════════════════════════════════════════════════════════════════
        // 诊断计数器
        // ════════════════════════════════════════════════════════════════════════

        private float _lastBindTryTime;
        private float _lastWarnTime;
        private int   _bindTryCount;
        private int   _bindSuccessCount;
        private int   _applyCount;
        private int   _solverUpdateCount;
        private float _lastApplyTime;
        private float _lastSolverUpdateTime;

        private const float WarnInterval = 2.0f;

        // ════════════════════════════════════════════════════════════════════════
        // Inspector 操作按钮
        // ════════════════════════════════════════════════════════════════════════

        [TabGroup("IKDriver", "初始化")]
        [BoxGroup("IKDriver/初始化/全局策略")]
        [Button("手动重新绑定 BipedIK", ButtonSizes.Medium)]
        [InfoBox("仅运行时有效。由 StateMachine.BindToAnimator 自动调用；BipedIK 组件热插拔后也可点此手动触发重绑。", InfoMessageType.Info)]
        private void ManualRebindBipedIK()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[StateFinalIKDriver] 手动重绑仅在运行时有效。");
                return;
            }
            if (_animator == null)
            {
                Debug.LogWarning("[StateFinalIKDriver] Animator 未绑定，请先通过 StateMachine.BindToAnimator 完成初始化。");
                return;
            }
            TryRebindBipedIK();
            Debug.Log($"[StateFinalIKDriver] 手动重绑结果：BipedIKReady={_bipedIKReady}  Error={_bipedIKError}", this);
        }

        // ════════════════════════════════════════════════════════════════════════
        // 生命周期
        // ════════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            // 必须由 StateMachine.BindToAnimator 调用 Bind() 后才启用
            enabled = false;
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

            // ── 各功能组初始化（顺序有依赖：Grounder 需 BipedIK 先就绪） ──────
            InitBipedIK();
            InitLookAtIK();
            InitAimIK();
            InitGrounder();
            InitHitReactionAndRecoil();

            // ── 辅助 Transform（Hint/Goal） ────────────────────────────────────
            EnsureHintTransforms();
            EnsureGoalTargetTransforms();

            // ── 缺失组件提示（Info 级别，可选） ───────────────────────────────
            if (logMissingComponentHints) LogMissingHints();

            _wasBipedIKBound = _bipedIKReady;
            _hasLastPose     = false;
            enabled          = true;
        }

        internal void Unbind()
        {
            // Grounder 需要显式关闭（OnDisable 会清零 BipedIK 脚步权重）
            if (_grounderBipedIK != null) _grounderBipedIK.enabled = false;

            _bipedIKReady      = false;  _bipedIK      = null;
            _lookAtIKReady     = false;  _lookAtIK     = null;
            _aimIKReady        = false;  _aimIK        = null;
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
            enabled           = false;
        }

        // ════════════════════════════════════════════════════════════════════════
        // LateUpdate — 主驱动帧循环（order -1，在所有 FinalIK 组件之前）
        // ════════════════════════════════════════════════════════════════════════

        private void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_stateMachine == null || _animator == null)
                throw new InvalidOperationException(
                    "[StateFinalIKDriver] 未绑定就进入 LateUpdate。" +
                    "请通过 StateMachine.BindToAnimator 调用 Bind()。");
#else
            if (_stateMachine == null || _animator == null) { enabled = false; return; }
#endif
            if (!_stateMachine.isRunning) return;

            // ── 热插拔：BipedIK 在运行时被动态添加 ─────────────────────────────
            if (!_bipedIKReady)
            {
                if (rebindInterval <= 0f || (Time.unscaledTime - _lastBindTryTime) >= rebindInterval)
                {
                    _lastBindTryTime = Time.unscaledTime;
                    TryRebindBipedIK();
                }
            }

            ref var pose = ref _stateMachine.finalIKPose;

            // ── BipedIK 未就绪时发出节流警告 ────────────────────────────────────
            if (!_bipedIKReady)
            {
                if (warnWhenPoseHasWeightButNoIK && pose.HasAnyWeight
                    && (Time.unscaledTime - _lastWarnTime) >= WarnInterval)
                {
                    _lastWarnTime = Time.unscaledTime;
                    var err = string.IsNullOrEmpty(_bipedIKError) ? "原因未知" : _bipedIKError;
                    Debug.LogWarning(
                        $"[StateFinalIKDriver] finalIKPose 有权重，但 BipedIK 不可用：{err}\n" +
                        "→ 在 Animator 同 GameObject 上添加 RootMotion.FinalIK.BipedIK 组件并确保 References 已填写。",
                        _animator);
                }

                // LookAtIK/AimIK 不依赖 BipedIK，仍可继续自行求解（它们已 enabled）
                return;
            }

            // ── 重新绑定后强制 dirty 一次 ───────────────────────────────────────
            if (!_wasBipedIKBound)
            {
                _hasLastPose     = false;
                _wasBipedIKBound = true;
            }

            // ── 无权重：重置一次后提前返回 ─────────────────────────────────────
            if (!pose.HasAnyWeight)
            {
                if (_hasLastPose)
                {
                    _bipedIK.SetToDefaults();
                    // 关闭 LookAtIK 权重（如有）；它的 LateUpdate 后续会挂 0 权重求解
                    if (_lookAtIKReady) _lookAtIK.solver.IKPositionWeight = 0f;
                    _hasLastPose = false;
                }
                // BipedIK 求解仍需每帧跑：动画每帧覆盖骨骼
                _bipedIK.UpdateBipedIK();
                _solverUpdateCount++;
                _lastSolverUpdateTime = Time.unscaledTime;
                return;
            }

            // ── 脏检测：pose 变化 OR 手动拖目标点时才写入 SetIK* ───────────────
            bool dirty = !_hasLastPose || !PoseApproxEqual(in _lastPose, in pose);
            if (!driveGoalTargetsFromPose)
            {
                EnsureGoalTargetTransforms();
                if (AreGoalTargetsDirty()) dirty = true;
            }

            if (dirty)
            {
                EnsureGoalTargetTransforms();
                InitGoalTargetsOnce(in pose);                   // 首次放到 pose 位置，避免从原点"瞬移"
                if (driveGoalTargetsFromPose) SyncGoalTargets(in pose);

                // BipedIK 四肢 IK
                ApplyGoal(in pose.leftHand,  AvatarIKGoal.LeftHand,  _lhHint, _lhTarget);
                ApplyGoal(in pose.rightHand, AvatarIKGoal.RightHand, _rhHint, _rhTarget);
                ApplyGoal(in pose.leftFoot,  AvatarIKGoal.LeftFoot,  _lfHint, _lfTarget);
                ApplyGoal(in pose.rightFoot, AvatarIKGoal.RightFoot, _rfHint, _rfTarget);

                // LookAt（LookAtIK 组件优先，否则回退到 BipedIK 内置）
                ApplyLookAt(in pose);

                _lastPose    = pose;
                _hasLastPose = true;
                _applyCount++;
                _lastApplyTime = Time.unscaledTime;
                CacheGoalTargetSnapshot();
            }

            // ── 每帧求解 ────────────────────────────────────────────────────────
            // BipedIK：手动驱动（已 disabled）。
            // GrounderBipedIK：通过 BipedIK 求解器事件委托在 UpdateBipedIK 内自动触发。
            // LookAtIK/AimIK：保持 enabled，其 LateUpdate(order 0) 自动在我们之后运行。
            _bipedIK.UpdateBipedIK();
            _solverUpdateCount++;
            _lastSolverUpdateTime = Time.unscaledTime;
        }

        // ════════════════════════════════════════════════════════════════════════
        // 公共 API：功能组外部驱动入口
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// [AimIK] 设置瞄准目标并启用。target=null 则关闭瞄准。
        /// 需要在 Animator 同 GameObject 上挂载 <see cref="RootMotion.FinalIK.AimIK"/>。
        /// </summary>
        public void SetAimTarget(Transform target)
        {
            if (!_aimIKReady)
            {
                LogFeatureMissing("AimIK", "RootMotion.FinalIK.AimIK");
                return;
            }
            _aimIK.solver.target            = target;
            _aimIK.solver.IKPositionWeight  = target != null ? 1f : 0f;
        }

        /// <summary>[AimIK] 设置瞄准权重（0=关闭，1=完全）。</summary>
        public void SetAimWeight(float weight)
        {
            if (!_aimIKReady) { LogFeatureMissing("AimIK", "RootMotion.FinalIK.AimIK"); return; }
            _aimIK.solver.IKPositionWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// [HitReaction] 触发受击程序动画。
        /// 需要：<see cref="RootMotion.FinalIK.HitReaction"/> + FullBodyBipedIK。
        /// </summary>
        /// <param name="collider">被击中的 Collider（与 HitReaction Inspector 中配置的 HitPoint.collider 对应）。</param>
        /// <param name="force">打击力方向和大小（世界空间）。</param>
        /// <param name="point">打击点位置（世界空间）。</param>
        public void TriggerHitReaction(Collider collider, Vector3 force, Vector3 point)
        {
            if (!_hitReactionReady)
            {
                LogFeatureMissing("HitReaction", "RootMotion.FinalIK.HitReaction + FullBodyBipedIK");
                return;
            }
            _hitReaction.Hit(collider, force, point);
        }

        /// <summary>
        /// [Recoil] 触发武器后坐力程序动画。
        /// 需要：<see cref="RootMotion.FinalIK.Recoil"/> + FullBodyBipedIK。
        /// </summary>
        /// <param name="magnitude">后坐力强度倍率（建议范围 0~2，实际效果由 Recoil Inspector 中的 recoilWeight 曲线和 offsets 决定）。</param>
        public void TriggerRecoil(float magnitude)
        {
            if (!_recoilReady)
            {
                LogFeatureMissing("Recoil", "RootMotion.FinalIK.Recoil + FullBodyBipedIK");
                return;
            }
            _recoil.Fire(magnitude);
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
                return;   // 完全跳过，热路径零消耗
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

            // 关闭 BipedIK 自身 LateUpdate，交由 Driver 手动调 UpdateBipedIK()
            _bipedIK.enabled = false;
            _bipedIK.InitiateBipedIK();
            _bipedIKError  = string.Empty;
            _bipedIKReady  = true;
            _caps         |= FinalIKCapabilityFlags.BipedIK;
            _bindTryCount++;
            _bindSuccessCount++;
        }

        /// <summary>热插拔路径：节流重试，BipedIK 被运行时动态挂上时调用。</summary>
        private void TryRebindBipedIK()
        {
            _bindTryCount++;
            _bipedIK = _animator.GetComponent<BipedIK>();
            if (_bipedIK == null) { _bipedIKError = "未找到 BipedIK 组件"; return; }

            string err = string.Empty;
            if (!_bipedIK.references.isFilled && autoDetectReferencesIfMissing)
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
            _bipedIKReady     = true;
            _wasBipedIKBound  = false;  // 触发下一帧强制 dirty
            _caps            |= FinalIKCapabilityFlags.BipedIK;
            _bindSuccessCount++;
        }

        #endregion

        #region Init — LookAtIK

        private void InitLookAtIK()
        {
            _lookAtIKReady = false;
            if (!enableLookAtIK) return;   // 未启用，零消耗跳过
            _lookAtIK      = _refs.lookAtIK;
            if (_lookAtIK == null) return;

            // LookAtIK 保持 enabled，通过自身 LateUpdate(order 0) 求解
            // Driver LateUpdate(order -1) 设好 solver 参数后，组件自行完成求解
            _lookAtIKReady = true;
            _caps         |= FinalIKCapabilityFlags.LookAtIK;
        }

        #endregion

        #region Init — AimIK

        private void InitAimIK()
        {
            _aimIKReady = false;
            if (!enableAimIK) return;   // 未启用，零消耗跳过
            _aimIK      = _refs.aimIK;
            if (_aimIK == null) return;

            // 同 LookAtIK：保持 enabled，Driver 只负责设置 solver.target / IKPositionWeight
            _aimIK.solver.IKPositionWeight = 0f; // 默认关闭，由 SetAimTarget()/SetAimWeight() 开启
            _aimIKReady = true;
            _caps      |= FinalIKCapabilityFlags.AimIK;
        }

        #endregion

        #region Init — Grounder

        private void InitGrounder()
        {
            _grounderReady   = false;
            if (!enableGrounderBipedIK) return;   // 未启用，零消耗跳过
            _grounderBipedIK = _refs.grounderBipedIK;
            if (_grounderBipedIK == null) return;

            if (!_bipedIKReady)
            {
                // Grounder 依赖 BipedIK 求解器事件，BipedIK 不可用则 Grounder 无意义
                Debug.LogWarning(
                    "[StateFinalIKDriver] GrounderBipedIK 已挂载，但 BipedIK 未就绪，接地系统跳过。\n" +
                    "→ 确保同 GameObject 上的 BipedIK 组件 References 已正确配置。",
                    _animator);
                return;
            }

            // Grounder 通过 BipedIK.solvers.spine.OnPreUpdate 和 rightFoot.OnPostUpdate 委托驱动，
            // 与 BipedIK.enabled 状态无关。只需保持 Grounder enabled，它会在 BipedIK 求解器内自动触发。
            _grounderBipedIK.enabled = true;
            _grounderReady = true;
            _caps         |= FinalIKCapabilityFlags.GrounderBipedIK;
        }

        #endregion

        #region Init — HitReaction & Recoil

        private void InitHitReactionAndRecoil()
        {
            _hitReactionReady = false;
            _recoilReady      = false;

            // FullBodyBipedIK 整体未启用则跳过两个子系统
            if (!enableFullBodyBipedIK) return;

            // 各子功能未启用时也跳过，不读取引用
            _hitReaction = enableHitReaction ? _refs.hitReaction : null;
            _recoil      = enableRecoil      ? _refs.recoil      : null;

            // HitReaction / Recoil 都需要 FullBodyBipedIK（FBBIK）
            bool fbbikOk = _refs.fullBodyBipedIK != null;

            if (_hitReaction != null)
            {
                if (fbbikOk) { _hitReactionReady = true; _caps |= FinalIKCapabilityFlags.HitReaction; }
                else Debug.LogWarning(
                    "[StateFinalIKDriver] HitReaction 已挂载，但缺少 FullBodyBipedIK，HitReaction 未激活。\n" +
                    "→ 在同 GameObject 上添加 RootMotion.FinalIK.FullBodyBipedIK 组件。", _animator);
            }

            if (_recoil != null)
            {
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

        /// <summary>把单个 IKGoalPose 写入 BipedIK 的对应肢体求解器。</summary>
        private void ApplyGoal(in IKGoalPose goal, AvatarIKGoal avatarGoal,
                                Transform hint, Transform target)
        {
            float w     = Mathf.Clamp01(goal.weight);
            bool  isFoot = avatarGoal == AvatarIKGoal.LeftFoot || avatarGoal == AvatarIKGoal.RightFoot;
            float rotW  = isFoot ? w * Mathf.Clamp01(footRotationWeightMultiplier) : w;

            _bipedIK.SetIKPositionWeight(avatarGoal, w);
            _bipedIK.SetIKRotationWeight(avatarGoal, rotW);

            // 把目标 Transform 挂到 solver（Inspector 可视化 + 手动拖动输入兼容）
            var limb = _bipedIK.GetGoalIK(avatarGoal);
            if (limb != null && limb.target != target) limb.target = target;

            if (w <= 0.001f) return;

            // 有 target Transform 时由 solver 直接读取，不再额外 SetIKPosition/Rotation，
            // 两套输入并存会互相覆盖导致抖动
            if (target == null)
            {
                _bipedIK.SetIKPosition(avatarGoal, goal.position);
                _bipedIK.SetIKRotation(avatarGoal, goal.rotation);
            }

            // Hint → 弯曲目标（膝盖/肘部朝向）
            if (hint != null && goal.hintPosition != Vector3.zero)
            {
                // 只在变化明显时写（VR/高频场景减少 Transform 写入开销）
                if ((hint.position - goal.hintPosition).sqrMagnitude > 0.000001f)
                    hint.position = goal.hintPosition;

                if (limb != null)
                {
                    limb.bendGoal           = hint;
                    limb.bendModifier       = IKSolverLimb.BendModifier.Goal;
                    limb.bendModifierWeight = w;
                }
            }
            else if (limb != null)
            {
                limb.bendModifier       = IKSolverLimb.BendModifier.Animation;
                limb.bendModifierWeight = 1f;
                limb.bendGoal           = null;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // 运行时 Apply — LookAt（热路径）
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// LookAtIK 组件存在时优先使用（更精细的多骨骼注视）；
        /// 否则回退到 BipedIK 内置 LookAt。
        /// </summary>
        private void ApplyLookAt(in StateIKPose pose)
        {
            if (_lookAtIKReady)
            {
                // 数据写入 LookAtIK solver；LookAtIK 自身 LateUpdate(order 0) 完成求解
                _lookAtIK.solver.IKPosition       = pose.lookAtPosition;
                _lookAtIK.solver.IKPositionWeight = pose.lookAtWeight;
                // BipedIK 内置 LookAt 关闭，避免两套 LookAt 互相干扰
                _bipedIK.SetLookAtWeight(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            }
            else if (pose.lookAtWeight > 0.001f)
            {
                // 回退：BipedIK 内置 LookAt
                _bipedIK.SetLookAtPosition(pose.lookAtPosition);
                _bipedIK.SetLookAtWeight(
                    pose.lookAtWeight,
                    pose.lookAtBodyWeight,
                    pose.lookAtHeadWeight,
                    pose.lookAtEyesWeight,
                    pose.lookAtClampWeight,
                    pose.lookAtClampWeight,
                    pose.lookAtClampWeight);
            }
            else
            {
                // 无注视目标：保持空闲注视姿态（参数在 空闲注视 Tab 中配置）
                _bipedIK.SetLookAtWeight(
                    0f,
                    bipedLookAtDefaultBodyWeight,
                    bipedLookAtDefaultHeadWeight,
                    bipedLookAtDefaultEyesWeight,
                    bipedLookAtDefaultClampWeight,
                    bipedLookAtDefaultClampWeight,
                    bipedLookAtDefaultClampWeight);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Goal Transform 管理
        // ════════════════════════════════════════════════════════════════════════

        private void EnsureHintTransforms()
        {
            if (_lhHint != null) return;
            // 优先使用 Inspector 预置的变换，缺失时自动创建（HideInHierarchy，不污染层级）
            _lhHint = preHintLH != null ? preHintLH : CreateAux("__IKHint_LH", hidden: true);
            _rhHint = preHintRH != null ? preHintRH : CreateAux("__IKHint_RH", hidden: true);
            _lfHint = preHintLF != null ? preHintLF : CreateAux("__IKHint_LF", hidden: true);
            _rfHint = preHintRF != null ? preHintRF : CreateAux("__IKHint_RF", hidden: true);
        }

        private void EnsureGoalTargetTransforms()
        {
            if (_lhTarget != null) return;
            // 优先使用 Inspector 预置的变换，缺失时自动创建（可见于层级，便于 Scene 视图拖动调试）
            _lhTarget = preGoalLH != null ? preGoalLH : CreateAux("__IKTarget_LH", hidden: false);
            _rhTarget = preGoalRH != null ? preGoalRH : CreateAux("__IKTarget_RH", hidden: false);
            _lfTarget = preGoalLF != null ? preGoalLF : CreateAux("__IKTarget_LF", hidden: false);
            _rfTarget = preGoalRF != null ? preGoalRF : CreateAux("__IKTarget_RF", hidden: false);
            _goalTargetsInit = false;
            CacheGoalTargetSnapshot();
        }

        // 首次 Apply 时把目标 Transform 放到 pose 位置，避免从原点"瞬移"
        private void InitGoalTargetsOnce(in StateIKPose pose)
        {
            if (_goalTargetsInit) return;
            WriteTargetFromGoal(_lhTarget, in pose.leftHand);
            WriteTargetFromGoal(_rhTarget, in pose.rightHand);
            WriteTargetFromGoal(_lfTarget, in pose.leftFoot);
            WriteTargetFromGoal(_rfTarget, in pose.rightFoot);
            _goalTargetsInit = true;
        }

        private void SyncGoalTargets(in StateIKPose pose)
        {
            WriteTargetFromGoal(_lhTarget, in pose.leftHand);
            WriteTargetFromGoal(_rhTarget, in pose.rightHand);
            WriteTargetFromGoal(_lfTarget, in pose.leftFoot);
            WriteTargetFromGoal(_rfTarget, in pose.rightFoot);
        }

        private static void WriteTargetFromGoal(Transform t, in IKGoalPose goal)
        {
            if (t == null || goal.weight <= 0.001f) return;
            if ((t.position - goal.position).sqrMagnitude > 0.000001f) t.position = goal.position;
            if (Quaternion.Angle(t.rotation, goal.rotation)            > 0.1f)      t.rotation = goal.rotation;
        }

        private bool AreGoalTargetsDirty()
        {
            if (_lhTarget == null) return false;
            const float P = 0.000001f;
            const float R = 0.1f;
            if ((_lhTarget.position - _sn_lhP).sqrMagnitude > P) return true;
            if (Quaternion.Angle(_lhTarget.rotation, _sn_lhR)   > R) return true;
            if ((_rhTarget.position - _sn_rhP).sqrMagnitude > P) return true;
            if (Quaternion.Angle(_rhTarget.rotation, _sn_rhR)   > R) return true;
            if ((_lfTarget.position - _sn_lfP).sqrMagnitude > P) return true;
            if (Quaternion.Angle(_lfTarget.rotation, _sn_lfR)   > R) return true;
            if ((_rfTarget.position - _sn_rfP).sqrMagnitude > P) return true;
            if (Quaternion.Angle(_rfTarget.rotation, _sn_rfR)   > R) return true;
            return false;
        }

        private void CacheGoalTargetSnapshot()
        {
            if (_lhTarget != null) { _sn_lhP = _lhTarget.position; _sn_lhR = _lhTarget.rotation; }
            if (_rhTarget != null) { _sn_rhP = _rhTarget.position; _sn_rhR = _rhTarget.rotation; }
            if (_lfTarget != null) { _sn_lfP = _lfTarget.position; _sn_lfR = _lfTarget.rotation; }
            if (_rfTarget != null) { _sn_rfP = _rfTarget.position; _sn_rfR = _rfTarget.rotation; }
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

        private static bool PoseApproxEqual(in StateIKPose a, in StateIKPose b)
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
            if (Mathf.Abs(a.lookAtBodyWeight - b.lookAtBodyWeight)  > W) return false;
            if (Mathf.Abs(a.lookAtHeadWeight - b.lookAtHeadWeight)  > W) return false;
            if (Mathf.Abs(a.lookAtEyesWeight - b.lookAtEyesWeight)  > W) return false;
            if (Mathf.Abs(a.lookAtClampWeight- b.lookAtClampWeight) > W) return false;
            return true;
        }

        private static bool GoalApproxEqual(in IKGoalPose a, in IKGoalPose b,
                                             float wE, float pE, float rE)
        {
            if (Mathf.Abs(a.weight - b.weight) > wE) return false;
            if (a.weight <= 0.001f && b.weight <= 0.001f)
            {
                // 权重极小时只需比较 hintPosition（bendGoal 可能仍被 BipedIK 使用）
                return (a.hintPosition - b.hintPosition).sqrMagnitude <= pE;
            }
            if ((a.position - b.position).sqrMagnitude                    > pE) return false;
            if ((1f - Mathf.Abs(Quaternion.Dot(a.rotation, b.rotation))) > rE) return false;
            if ((a.hintPosition - b.hintPosition).sqrMagnitude            > pE) return false;
            return true;
        }

        // ════════════════════════════════════════════════════════════════════════
        // 缺失组件提示（仅 Bind 时触发一次，Info 级别）
        // ════════════════════════════════════════════════════════════════════════

        private void LogMissingHints()
        {
            LogHint(FinalIKCapabilityFlags.BipedIK,
                "BipedIK（四肢 IK 核心，必要）",
                "RootMotion.FinalIK.BipedIK");
            LogHint(FinalIKCapabilityFlags.LookAtIK,
                "LookAtIK（精细多骨骼注视，可选，有则优先于 BipedIK 内置 LookAt）",
                "RootMotion.FinalIK.LookAtIK");
            LogHint(FinalIKCapabilityFlags.AimIK,
                "AimIK（骨链瞄准，可选，用于武器持握 / 身体对准目标）",
                "RootMotion.FinalIK.AimIK");
            LogHint(FinalIKCapabilityFlags.GrounderBipedIK,
                "GrounderBipedIK（地形自适应脚步接地，可选）",
                "RootMotion.FinalIK.GrounderBipedIK");
            LogHint(FinalIKCapabilityFlags.HitReaction,
                "HitReaction（受击程序动画，可选，需要 FullBodyBipedIK）",
                "RootMotion.FinalIK.HitReaction + FullBodyBipedIK");
            LogHint(FinalIKCapabilityFlags.Recoil,
                "Recoil（后坐力程序动画，可选，需要 FullBodyBipedIK）",
                "RootMotion.FinalIK.Recoil + FullBodyBipedIK");
        }

        private void LogHint(FinalIKCapabilityFlags flag, string featureName, string requiredComponents)
        {
            if ((_caps & flag) != 0) return; // 已就绪，不提示

            bool badInit = (_presentButBad & flag) != 0;
            if (badInit)
            {
                Debug.LogWarning(
                    $"[StateFinalIKDriver] {featureName} 组件存在但初始化失败。\n原因：{_bipedIKError}",
                    _animator);
            }
            else
            {
                Debug.Log(
                    $"[StateFinalIKDriver] {featureName} 未激活（组件未挂载）。\n" +
                    $"→ 如需启用，请在 Animator 同 GameObject 上添加：{requiredComponents}",
                    _animator);
            }
        }

        private static void LogFeatureMissing(string feature, string required)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[StateFinalIKDriver] 调用了 {feature} 但该功能未就绪。\n" +
                $"→ 请在 Animator 同 GameObject 上添加：{required}");
#endif
        }

        // ════════════════════════════════════════════════════════════════════════
        // 自动补全组件（Bind 时调用；Editor "验证并补全" 按钮也可调用）
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 检查已启用功能的组件是否存在并处理缺失情况：
        /// <list type="bullet">
        /// <item>BipedIK：检测缺失时 <b>自动添加</b>（支持 AutoDetectReferences，可零配置使用）。</item>
        /// <item>其他组件：仅输出引导日志，不自动添加（需手动设置参数后再挂载）。</item>
        /// </list>
        /// </summary>
        public void AutoAddMissingComponents()
        {
            if (_animator == null) return;
            var go = _animator.gameObject;

            // BipedIK：支持 AutoDetectReferences，Humanoid 骨架安全自动添加
            if (enableBipedIK && autoAddBipedIK && _refs.bipedIK == null)
            {
                _refs.bipedIK = go.AddComponent<BipedIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 BipedIK，InitBipedIK 阶段将执行 AutoDetectReferences。", go);
            }

            // LookAtIK：Humanoid 时自动填充 solver.head 和 solver.spine（LookAtBone[]）
            if (enableLookAtIK && autoAddLookAtIK && _refs.lookAtIK == null)
            {
                var comp = go.AddComponent<LookAtIK>();
                if (_animator.isHuman)
                {
                    // 头部
                    var head = _animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) comp.solver.head.transform = head;

                    // 脊椎骨链（UpperChest → Chest → Spine，从上到下）
                    var spineList = new System.Collections.Generic.List<IKSolverLookAt.LookAtBone>();
                    var upperChest = _animator.GetBoneTransform(HumanBodyBones.UpperChest);
                    var chest      = _animator.GetBoneTransform(HumanBodyBones.Chest);
                    var spine      = _animator.GetBoneTransform(HumanBodyBones.Spine);
                    if (upperChest != null) spineList.Add(new IKSolverLookAt.LookAtBone { transform = upperChest });
                    if (chest      != null) spineList.Add(new IKSolverLookAt.LookAtBone { transform = chest });
                    if (spine      != null) spineList.Add(new IKSolverLookAt.LookAtBone { transform = spine });
                    if (spineList.Count > 0) comp.solver.spine = spineList.ToArray();

                    Debug.Log("[StateFinalIKDriver] 已自动添加 LookAtIK，并填充 Humanoid solver.head 和 solver.spine。", go);
                }
                else
                {
                    Debug.Log("[StateFinalIKDriver] 已自动添加 LookAtIK。非 Humanoid 骨架，请手动配置 solver.head 和 solver.spine。", go);
                }
                _refs.lookAtIK = comp;
            }

            // AimIK：需手动配置骨链和瞄准轴
            if (enableAimIK && autoAddAimIK && _refs.aimIK == null)
            {
                _refs.aimIK = go.AddComponent<AimIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 AimIK。注意：需手动配置 solver.bones 骨链和 solver.axis 瞄准轴。", go);
            }

            // GrounderBipedIK：OnEnable 自动扫描 BipedIK solver，安全自动添加
            if (enableGrounderBipedIK && autoAddGrounderBipedIK && _refs.grounderBipedIK == null)
            {
                _refs.grounderBipedIK = go.AddComponent<GrounderBipedIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 GrounderBipedIK。", go);
            }

            // FullBodyBipedIK：支持 AutoDetectReferences，Humanoid 骨架安全自动添加
            if (enableFullBodyBipedIK && autoAddFullBodyBipedIK && _refs.fullBodyBipedIK == null)
            {
                _refs.fullBodyBipedIK = go.AddComponent<FullBodyBipedIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 FullBodyBipedIK。InitializeFullBodyBipedIK 阶段将进一步配置。", go);
            }

            // HitReaction：需手动配置 hitPoints 列表
            if (enableHitReaction && autoAddHitReaction && _refs.hitReaction == null)
            {
                _refs.hitReaction = go.AddComponent<HitReaction>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 HitReaction。注意：需手动配置 hitPoints 列表（被击部位 Collider + 权重曲线）。", go);
            }

            // Recoil：需手动配置 recoilOffset 曲线
            if (enableRecoil && autoAddRecoil && _refs.recoil == null)
            {
                _refs.recoil = go.AddComponent<Recoil>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 Recoil。注意：需手动配置 recoilOffset 列表及 positionOffset / rotationOffset 曲线。", go);
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Inspector / 外部查询（只读）
        // ════════════════════════════════════════════════════════════════════════

        // 功能就绪状态
        [TabGroup("IKDriver", "诊断", Order = 6,
            TextColor = "@ESDesignUtility.ColorSelector.GetColor(\"雾橙\")")]
        [BoxGroup("IKDriver/诊断/就绪状态", ShowLabel = true)]
        [ShowInInspector, ReadOnly, LabelText("已就绪功能集")]
        public FinalIKCapabilityFlags Capabilities     => _caps;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/就绪状态")]
        [HorizontalGroup("IKDriver/诊断/就绪状态/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("四肢IK (BipedIK)")]
        public bool IsBipedIKReady      => _bipedIKReady;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/就绪状态")]
        [HorizontalGroup("IKDriver/诊断/就绪状态/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("地形接地 (Grounder)")]
        public bool IsGrounderReady     => _grounderReady;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/就绪状态")]
        [HorizontalGroup("IKDriver/诊断/就绪状态/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("多骨骼注视 (LookAtIK)")]
        public bool IsLookAtIKReady     => _lookAtIKReady;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/就绪状态")]
        [HorizontalGroup("IKDriver/诊断/就绪状态/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("骨链瞄准 (AimIK)")]
        public bool IsAimIKReady        => _aimIKReady;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/就绪状态")]
        [HorizontalGroup("IKDriver/诊断/就绪状态/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("受击反应 (HitReaction)")]
        public bool IsHitReactionReady  => _hitReactionReady;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/就绪状态")]
        [HorizontalGroup("IKDriver/诊断/就绪状态/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("后坐力 (Recoil)")]
        public bool IsRecoilReady       => _recoilReady;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/就绪状态")]
        [ShowInInspector, ReadOnly, LabelText("BipedIK 错误原因")]
        [HideIf("IsBipedIKReady")]
        public string BipedIKError      => _bipedIKError;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/帧计数", ShowLabel = true)]
        [HorizontalGroup("IKDriver/诊断/帧计数/绑定", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("尝试绑定次数")]
        public int    BindTryCount         => _bindTryCount;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/帧计数")]
        [HorizontalGroup("IKDriver/诊断/帧计数/绑定", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("成功绑定次数")]
        public int    BindSuccessCount     => _bindSuccessCount;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/帧计数")]
        [HorizontalGroup("IKDriver/诊断/帧计数/求解", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("Pose 写入次数")]
        public int    ApplyCount           => _applyCount;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/帧计数")]
        [HorizontalGroup("IKDriver/诊断/帧计数/求解", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("Solver 更新次数")]
        public int    SolverUpdateCount    => _solverUpdateCount;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/帧计数")]
        [HorizontalGroup("IKDriver/诊断/帧计数/时间", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("上次写入时刻")]
        public float  LastApplyTime        => _lastApplyTime;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/帧计数")]
        [HorizontalGroup("IKDriver/诊断/帧计数/时间", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("上次求解时刻")]
        public float  LastSolverUpdateTime => _lastSolverUpdateTime;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/当前 Pose", ShowLabel = true)]
        [ShowInInspector, ReadOnly, LabelText("Pose 有权重")]
        public bool HasPoseWeight
            => _stateMachine != null && _stateMachine.finalIKPose.HasAnyWeight;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/当前 Pose")]
        [ShowInInspector, ReadOnly, LabelText("当前 IK Pose")]
        public StateIKPose CurrentPose
            => _stateMachine != null ? _stateMachine.finalIKPose : default;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/目标点 Transform", ShowLabel = true)]
        [HorizontalGroup("IKDriver/诊断/目标点 Transform/手部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("左手")]
        public Transform LeftHandTarget  => _lhTarget;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/目标点 Transform")]
        [HorizontalGroup("IKDriver/诊断/目标点 Transform/手部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("右手")]
        public Transform RightHandTarget => _rhTarget;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/目标点 Transform")]
        [HorizontalGroup("IKDriver/诊断/目标点 Transform/脚部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("左脚")]
        public Transform LeftFootTarget  => _lfTarget;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/目标点 Transform")]
        [HorizontalGroup("IKDriver/诊断/目标点 Transform/脚部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("右脚")]
        public Transform RightFootTarget => _rfTarget;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/已解析求解器", ShowLabel = true)]
        [HorizontalGroup("IKDriver/诊断/已解析求解器/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("四肢IK (BipedIK)")]
        private BipedIK         DBG_BipedIK         => _bipedIK;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/已解析求解器")]
        [HorizontalGroup("IKDriver/诊断/已解析求解器/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("地形接地 (GrounderBipedIK)")]
        private GrounderBipedIK DBG_Grounder         => _grounderBipedIK;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/已解析求解器")]
        [HorizontalGroup("IKDriver/诊断/已解析求解器/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("多骨骼注视 (LookAtIK)")]
        private LookAtIK        DBG_LookAtIK         => _lookAtIK;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/已解析求解器")]
        [HorizontalGroup("IKDriver/诊断/已解析求解器/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("骨链瞄准 (AimIK)")]
        private AimIK           DBG_AimIK            => _aimIK;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/已解析求解器")]
        [HorizontalGroup("IKDriver/诊断/已解析求解器/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("全身IK (FullBodyBipedIK)")]
        private FullBodyBipedIK DBG_FullBodyBipedIK  => _refs?.fullBodyBipedIK;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/已解析求解器")]
        [HorizontalGroup("IKDriver/诊断/已解析求解器/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("受击反应 (HitReaction)")]
        private HitReaction     DBG_HitReaction      => _hitReaction;

        [TabGroup("IKDriver", "诊断")]
        [BoxGroup("IKDriver/诊断/已解析求解器")]
        [HorizontalGroup("IKDriver/诊断/已解析求解器/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("后坐力 (Recoil)")]
        private Recoil          DBG_Recoil           => _recoil;

        // Legacy compat（原 Bridge 透传，保持外部代码不破坏）
        public bool   IsReady       => _bipedIKReady;
        public bool   IsBound       => _bipedIK != null;
        public string LastBindError => _bipedIKError;
        public bool   DriveGoalTargetsFromPose => driveGoalTargetsFromPose;

        // 功能启用状态（供 Editor 读取）
        public bool FeatureEnabledBipedIK         => enableBipedIK;
        public bool FeatureEnabledLookAtIK        => enableLookAtIK;
        public bool FeatureEnabledAimIK           => enableAimIK;
        public bool FeatureEnabledGrounderBipedIK => enableGrounderBipedIK;
        public bool FeatureEnabledFullBodyBipedIK => enableFullBodyBipedIK;
        public bool FeatureEnabledHitReaction     => enableHitReaction;
        public bool FeatureEnabledRecoil          => enableRecoil;
    }
}
