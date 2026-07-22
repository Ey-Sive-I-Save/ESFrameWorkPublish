using UnityEngine;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ES
{
    public sealed partial class  StateFinalIKDriver
    {
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
            if ((_caps & flag) != 0) return;

            bool badInit = (_presentButBad & flag) != 0;
            if (badInit)
            {
                string error = GetCapabilityErrorMessage(flag);
                Debug.LogWarning(
                    $"[StateFinalIKDriver] {featureName} 组件存在但初始化失败。\n原因：{error}",
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

        private string GetCapabilityErrorMessage(FinalIKCapabilityFlags flag)
        {
            switch (flag)
            {
                case FinalIKCapabilityFlags.BipedIK:
                    return _bipedIKError;
                case FinalIKCapabilityFlags.AimIK:
                    return _aimIKError;
                default:
                    return string.Empty;
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

        internal void AutoAddMissingComponents()
        {
            if (_animator == null) return;
            var go = _animator.gameObject;

            if (enableBipedIK && autoAddBipedIK && _refs.bipedIK == null)
            {
                _refs.bipedIK = go.AddComponent<BipedIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 BipedIK，InitBipedIK 阶段将执行 AutoDetectReferences。", go);
            }

            if (enableLookAtIK && autoAddLookAtIK && _refs.lookAtIK == null)
            {
                var comp = go.AddComponent<LookAtIK>();
                if (_animator.isHuman)
                {
                    var head = _animator.GetBoneTransform(HumanBodyBones.Head);
                    if (head != null) comp.solver.head.transform = head;

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

            if (enableAimIK && autoAddAimIK && _refs.aimIK == null)
            {
                _refs.aimIK = go.AddComponent<AimIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 AimIK。注意：需手动配置 solver.bones 骨链和 solver.axis 瞄准轴。", go);
            }

            if (enableGrounderBipedIK && autoAddGrounderBipedIK && _refs.grounderBipedIK == null)
            {
                _refs.grounderBipedIK = go.AddComponent<GrounderBipedIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 GrounderBipedIK。", go);
            }

            if (enableFullBodyBipedIK && autoAddFullBodyBipedIK && _refs.fullBodyBipedIK == null)
            {
                _refs.fullBodyBipedIK = go.AddComponent<FullBodyBipedIK>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 FullBodyBipedIK。InitializeFullBodyBipedIK 阶段将进一步配置。", go);
            }

            if (enableHitReaction && autoAddHitReaction && _refs.hitReaction == null)
            {
                _refs.hitReaction = go.AddComponent<HitReaction>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 HitReaction。可在 Driver 中启用受击默认配置，或继续手动配置 hitPoints 列表。", go);
            }

            if (enableRecoil && autoAddRecoil && _refs.recoil == null)
            {
                _refs.recoil = go.AddComponent<Recoil>();
                Debug.Log("[StateFinalIKDriver] 已自动添加 Recoil。可在 Driver 中启用后坐力默认配置，或继续手动配置 recoil offsets。", go);
            }
        }

        [TabGroup("DriverLayout", "诊断", Order = 300)]
        [PropertyOrder(0)]
        [TitleGroup("DriverLayout/诊断/状态概览", BoldTitle = true)]
        [ShowInInspector, ReadOnly, LabelText("已就绪功能集")]
        public FinalIKCapabilityFlags Capabilities => _caps;

        [PropertyOrder(0)]
        [TitleGroup("DriverLayout/诊断/状态概览", BoldTitle = true)]
        [ShowInInspector, ReadOnly, LabelText("组件启用状态")]
        public string DriverEnableStateSummary => _driverEnableStateSummary;

        [PropertyOrder(0)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("IK 求解顺序")]
        public string FinalIKScheduleSummary => _finalIKScheduleSummaryCache;

        [PropertyOrder(0)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("商业顺序契约")]
        public string FinalIKCommercialOrderSummary => _finalIKCommercialOrderSummaryCache;

        [PropertyOrder(0)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("调度阻断")]
        public string FinalIKScheduleBlockSummary => _finalIKScheduleBlockSummaryCache;

        [ShowInInspector, ReadOnly, LabelText("调度模式")]
        public FinalIKDriverScheduleMode FinalIKScheduleMode => _scheduleMode;

        [ShowInInspector, ReadOnly, LabelText("调度阻断标记")]
        public FinalIKDriverBlockFlags FinalIKScheduleBlockFlags => _scheduleBlockFlags;

        [PropertyOrder(1)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("四肢IK (BipedIK)")]
        public bool IsBipedIKReady => _bipedIKReady;

        [PropertyOrder(2)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("地形接地 (Grounder)")]
        public bool IsGrounderReady => _grounderReady;

        [PropertyOrder(3)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("多骨骼注视 (LookAtIK)")]
        public bool IsLookAtIKReady => _lookAtIKReady;

        [PropertyOrder(4)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("骨链瞄准 (AimIK)")]
        public bool IsAimIKReady => _aimIKReady;

        [PropertyOrder(5)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("全身IK (FBBIK)")]
        public bool IsFullBodyBipedIKReady => _fullBodyBipedIKReady;

        [PropertyOrder(5)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("受击反应 (HitReaction)")]
        public bool IsHitReactionReady => _hitReactionReady;

        [PropertyOrder(6)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("后坐力 (Recoil)")]
        public bool IsRecoilReady => _recoilReady;

        [PropertyOrder(6)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("后坐力可用性摘要")]
        public string RecoilAvailabilitySummary
        {
            get
            {
                if (!enableRecoil)
                    return "未启用 Recoil 开关";

                if (!enableFullBodyBipedIK)
                    return "未启用 FullBodyBipedIK，Recoil 不可用";

                Recoil resolvedRecoil = null;
                if (_refs != null)
                {
                    resolvedRecoil = _refs.recoil;
                }
                if (resolvedRecoil == null)
                {
                    resolvedRecoil = presetRecoil;
                }
                if (resolvedRecoil == null)
                {
                    resolvedRecoil = GetComponent<Recoil>();
                }
                if (resolvedRecoil == null)
                    return "缺少 Recoil 组件";

                FullBodyBipedIK resolvedFullBodyBipedIK = null;
                if (_refs != null)
                {
                    resolvedFullBodyBipedIK = _refs.fullBodyBipedIK;
                }
                if (resolvedFullBodyBipedIK == null)
                {
                    resolvedFullBodyBipedIK = presetFullBodyBipedIK;
                }
                if (resolvedFullBodyBipedIK == null)
                {
                    resolvedFullBodyBipedIK = GetComponent<FullBodyBipedIK>();
                }
                if (resolvedFullBodyBipedIK == null)
                    return "缺少 FullBodyBipedIK 组件";

                if (!_recoilReady)
                    return "组件已找到，但尚未完成初始化";

                return "可用";
            }
        }

        [PropertyOrder(6)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("后坐力已绑 FBBIK")]
        public bool RecoilHasFullBodyIKLink => _recoil != null && _recoil.ik != null;

        [PropertyOrder(6)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [HorizontalGroup("DriverLayout/诊断/状态概览/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("后坐力已绑 AimIK")]
        public bool RecoilHasAimIKLink => _recoil != null && _recoil.aimIK != null;

        [PropertyOrder(7)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("BipedIK 错误原因")]
        [HideIf("IsBipedIKReady")]
        public string BipedIKError => _bipedIKError;

        [PropertyOrder(7)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("BipedIK（运行时连接）")]
        public BipedIK RuntimeResolvedBipedIK => _bipedIK;

        [PropertyOrder(8)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("AimIK 错误原因")]
        [HideIf("IsAimIKReady")]
        public string AimIKError => _aimIKError;

        [PropertyOrder(9)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("Biped 运行门控原因")]
        public string BipedRuntimeGateReason => BuildBipedRuntimeGateReason();

        [PropertyOrder(9)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("GoalTarget 就绪")]
        public bool GoalTargetReady => _goalTargetsReady;

        [PropertyOrder(9)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [ShowInInspector, ReadOnly, LabelText("缺失 GoalTarget")]
        [HideIf("GoalTargetReady")]
        public string MissingGoalTargets => GetMissingGoalTargetNames();

    #if UNITY_EDITOR
        [PropertyOrder(9)]
        [TitleGroup("DriverLayout/诊断/状态概览")]
        [Button("打开交互运行时面板", ButtonSizes.Medium)]
        private void OpenInteractionRuntimePanel()
        {
            EditorApplication.ExecuteMenuItem(MenuItemPathDefine.INTERACTION_RUNTIME_PANEL_PATH);
        }
    #endif

        [PropertyOrder(10)]
        [TitleGroup("DriverLayout/诊断/运行统计", BoldTitle = true)]
        [HorizontalGroup("DriverLayout/诊断/运行统计/绑定", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("尝试绑定次数")]
        public int BindTryCount => _bindTryCount;

        [PropertyOrder(11)]
        [TitleGroup("DriverLayout/诊断/运行统计")]
        [HorizontalGroup("DriverLayout/诊断/运行统计/绑定", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("成功绑定次数")]
        public int BindSuccessCount => _bindSuccessCount;

        [PropertyOrder(12)]
        [TitleGroup("DriverLayout/诊断/运行统计")]
        [HorizontalGroup("DriverLayout/诊断/运行统计/求解", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("Pose 写入次数")]
        public int ApplyCount => _applyCount;

        [PropertyOrder(13)]
        [TitleGroup("DriverLayout/诊断/运行统计")]
        [HorizontalGroup("DriverLayout/诊断/运行统计/求解", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("Solver 更新次数")]
        public int SolverUpdateCount => _solverUpdateCount;

        [PropertyOrder(14)]
        [TitleGroup("DriverLayout/诊断/运行统计")]
        [HorizontalGroup("DriverLayout/诊断/运行统计/时间", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("上次写入时刻")]
        public float LastApplyTime => _lastApplyTime;

        [PropertyOrder(15)]
        [TitleGroup("DriverLayout/诊断/运行统计")]
        [HorizontalGroup("DriverLayout/诊断/运行统计/时间", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("上次求解时刻")]
        public float LastSolverUpdateTime => _lastSolverUpdateTime;

        [PropertyOrder(20)]
        [TitleGroup("DriverLayout/诊断/当前数据", BoldTitle = true)]
        [ShowInInspector, ReadOnly, LabelText("有激活 IK 权重")]
        public bool HasActiveIK => _stateMachine != null && _stateMachine.stateGeneralFinalIKDriverPose.HasAnyWeight;

        [PropertyOrder(21)]
        [TitleGroup("DriverLayout/诊断/当前数据")]
        [ShowInInspector, ReadOnly, LabelText("当前 Driver Pose")]
        public StateGeneralFinalIKDriverPose CurrentPose => _stateMachine != null ? _stateMachine.stateGeneralFinalIKDriverPose : default;

#if UNITY_EDITOR
        [PropertyOrder(21)]
        [TitleGroup("DriverLayout/诊断/当前数据")]
        [ShowInInspector, ReadOnly, MultiLineProperty(8), LabelText("状态 IK 贡献明细")]
        public string StateIKContributions => _stateMachine != null ? _stateMachine.stateGeneralFinalIKContributionSummary : "状态机未绑定";

        [PropertyOrder(21)]
        [TitleGroup("DriverLayout/诊断/当前数据")]
        [Button("刷新IK贡献明细", ButtonSizes.Medium)]
        private void RefreshStateIKContributions()
        {
            if (_stateMachine != null)
                _stateMachine.RequestIKContributionSummaryRefresh();
        }
#endif

        [PropertyOrder(22)]
        [TitleGroup("DriverLayout/诊断/当前数据")]
        [HorizontalGroup("DriverLayout/诊断/当前数据/手部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("左手")]
        public Transform LeftHandTarget => _lhTarget;

        [PropertyOrder(23)]
        [TitleGroup("DriverLayout/诊断/当前数据")]
        [HorizontalGroup("DriverLayout/诊断/当前数据/手部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("右手")]
        public Transform RightHandTarget => _rhTarget;

        [PropertyOrder(24)]
        [TitleGroup("DriverLayout/诊断/当前数据")]
        [HorizontalGroup("DriverLayout/诊断/当前数据/脚部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("左脚")]
        public Transform LeftFootTarget => _lfTarget;

        [PropertyOrder(25)]
        [TitleGroup("DriverLayout/诊断/当前数据")]
        [HorizontalGroup("DriverLayout/诊断/当前数据/脚部", LabelWidth = 60)]
        [ShowInInspector, ReadOnly, LabelText("右脚")]
        public Transform RightFootTarget => _rfTarget;

        [PropertyOrder(30)]
        [TitleGroup("DriverLayout/诊断/已解析求解器", BoldTitle = true)]
        [HorizontalGroup("DriverLayout/诊断/已解析求解器/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("四肢IK (BipedIK)")]
        private BipedIK DBG_BipedIK => _bipedIK;

        [PropertyOrder(31)]
        [TitleGroup("DriverLayout/诊断/已解析求解器")]
        [HorizontalGroup("DriverLayout/诊断/已解析求解器/四肢IK", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("地形接地 (GrounderBipedIK)")]
        private GrounderBipedIK DBG_Grounder => _grounderBipedIK;

        [PropertyOrder(32)]
        [TitleGroup("DriverLayout/诊断/已解析求解器")]
        [HorizontalGroup("DriverLayout/诊断/已解析求解器/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("多骨骼注视 (LookAtIK)")]
        private LookAtIK DBG_LookAtIK => _lookAtIK;

        [PropertyOrder(33)]
        [TitleGroup("DriverLayout/诊断/已解析求解器")]
        [HorizontalGroup("DriverLayout/诊断/已解析求解器/注视瞄准", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("骨链瞄准 (AimIK)")]
        private AimIK DBG_AimIK => _aimIK;

        [PropertyOrder(34)]
        [TitleGroup("DriverLayout/诊断/已解析求解器")]
        [HorizontalGroup("DriverLayout/诊断/已解析求解器/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("全身IK (FullBodyBipedIK)")]
        private FullBodyBipedIK DBG_FullBodyBipedIK
        {
            get
            {
                return _refs != null ? _refs.fullBodyBipedIK : null;
            }
        }

        [PropertyOrder(35)]
        [TitleGroup("DriverLayout/诊断/已解析求解器")]
        [HorizontalGroup("DriverLayout/诊断/已解析求解器/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("受击反应 (HitReaction)")]
        private HitReaction DBG_HitReaction => _hitReaction;

        [PropertyOrder(36)]
        [TitleGroup("DriverLayout/诊断/已解析求解器")]
        [HorizontalGroup("DriverLayout/诊断/已解析求解器/程序动画", LabelWidth = 100)]
        [ShowInInspector, ReadOnly, LabelText("后坐力 (Recoil)")]
        private Recoil DBG_Recoil => _recoil;

        private string BuildFinalIKScheduleSummary()
        {
            bool aim = (_finalIKScheduleFlags & IKScheduleFrameFlags.Aim) != 0;
            bool biped = (_finalIKScheduleFlags & IKScheduleFrameFlags.Biped) != 0;
            bool lookAt = (_finalIKScheduleFlags & IKScheduleFrameFlags.LookAt) != 0;
            bool grounder = (_finalIKScheduleFlags & IKScheduleFrameFlags.Grounder) != 0;
            bool fullBody = (_finalIKScheduleFlags & IKScheduleFrameFlags.FullBody) != 0;

            string mode = _scheduleMode == FinalIKDriverScheduleMode.DriverCoreManualProceduralDelegates
                ? "核心手动 + 程序委托"
                : "仅核心手动";

            if (!_runtimeBindingReady)
                return $"{mode}：等待运行时绑定";

            if (!aim && !biped && !lookAt && !grounder && !fullBody)
                return $"{mode}：本帧无 IK 求解";

            string grounderNode = grounder ? "BipedIK(+Grounder)" : "BipedIK";
            string fullBodyNode = _scheduleMode == FinalIKDriverScheduleMode.DriverCoreManualProceduralDelegates
                ? "FBBIK(+HitReaction/Recoil)"
                : "FBBIK";

            if (aim && biped && lookAt && fullBody)
                return $"{mode}：AimIK -> {grounderNode} -> LookAtIK -> {fullBodyNode}";
            if (aim && biped && lookAt)
                return $"{mode}：AimIK -> {grounderNode} -> LookAtIK";
            if (aim && biped && fullBody)
                return $"{mode}：AimIK -> {grounderNode} -> {fullBodyNode}";
            if (biped && lookAt && fullBody)
                return $"{mode}：{grounderNode} -> LookAtIK -> {fullBodyNode}";
            if (biped && fullBody)
                return $"{mode}：{grounderNode} -> {fullBodyNode}";
            if (lookAt && fullBody)
                return $"{mode}：LookAtIK -> {fullBodyNode}";
            if (aim && biped)
                return $"{mode}：AimIK -> {grounderNode}";
            if (biped && lookAt)
                return $"{mode}：{grounderNode} -> LookAtIK";
            if (aim && fullBody)
                return $"{mode}：AimIK -> {fullBodyNode}";
            if (fullBody)
                return $"{mode}：{fullBodyNode}";
            if (lookAt)
                return $"{mode}：LookAtIK";
            if (aim)
                return $"{mode}：AimIK";
            if (biped)
                return $"{mode}：{grounderNode}";
            return $"{mode}：本帧无 IK 求解";
        }

        private string BuildFinalIKScheduleBlockSummary()
        {
            if (_scheduleBlockFlags == FinalIKDriverBlockFlags.None)
                return "无阻断";

            var flags = _scheduleBlockFlags;
            string manualOff = string.Empty;
            string dependency = string.Empty;
            string mode = string.Empty;

            AppendScheduleBlock(ref manualOff, flags, FinalIKDriverBlockFlags.BipedAutoLateUpdateDisabled, "BipedIK");
            AppendScheduleBlock(ref manualOff, flags, FinalIKDriverBlockFlags.AimAutoLateUpdateDisabled, "AimIK");
            AppendScheduleBlock(ref manualOff, flags, FinalIKDriverBlockFlags.LookAtAutoLateUpdateDisabled, "LookAtIK");
            AppendScheduleBlock(ref manualOff, flags, FinalIKDriverBlockFlags.FullBodyAutoLateUpdateDisabled, "FBBIK");

            AppendScheduleBlock(ref dependency, flags, FinalIKDriverBlockFlags.GrounderBlockedNoBiped, "Grounder 依赖 BipedIK");
            AppendScheduleBlock(ref dependency, flags, FinalIKDriverBlockFlags.GrounderWaitingForWeight, "Grounder 等待权重");
            AppendScheduleBlock(ref dependency, flags, FinalIKDriverBlockFlags.HitReactionBlockedNoFullBody, "HitReaction 依赖 FBBIK");
            AppendScheduleBlock(ref dependency, flags, FinalIKDriverBlockFlags.RecoilBlockedNoFullBody, "Recoil 依赖 FBBIK");

            if ((flags & FinalIKDriverBlockFlags.ProceduralDelegatesDisabledByMode) != 0)
                mode = "程序委托已关闭";

            System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
            if (!string.IsNullOrEmpty(manualOff))
                sb.Append("自动求解已关: ").Append(manualOff);
            if (!string.IsNullOrEmpty(dependency))
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append("依赖/等待: ").Append(dependency);
            }
            if (!string.IsNullOrEmpty(mode))
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(mode);
            }
            return sb.ToString();
        }

        private static void AppendScheduleBlock(ref string summary, FinalIKDriverBlockFlags flags, FinalIKDriverBlockFlags target, string text)
        {
            if ((flags & target) == 0)
                return;

            if (!string.IsNullOrEmpty(summary))
                summary += "；";
            summary += text;
        }

        private void RefreshFinalIKScheduleDiagnosticsCache()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _finalIKScheduleSummaryCache = BuildFinalIKScheduleSummary();
            _finalIKCommercialOrderSummaryCache = "Grounder -> Aim -> LookAt -> FBBIK -> Recoil -> HitReaction";
            _finalIKScheduleBlockSummaryCache = BuildFinalIKScheduleBlockSummary();
#endif
        }

        private string BuildBipedRuntimeGateReason()
        {
            switch (_bipedRuntimeGateCode)
            {
                case BipedRuntimeGateCode.RuntimeBindingNotReady:
                    return "Driver 未完成运行时绑定（_runtimeBindingReady=false）";
                case BipedRuntimeGateCode.StateMachineNotRunning:
                    return "StateMachine 未运行（isRunning=false）";
                case BipedRuntimeGateCode.BipedIKNotReady:
                    return string.IsNullOrEmpty(_bipedIKError) ? "BipedIK 未就绪" : $"BipedIK 未就绪：{_bipedIKError}";
                case BipedRuntimeGateCode.GoalTargetsNotReady:
                {
                    string missing = GetMissingGoalTargetNames();
                    return string.IsNullOrEmpty(missing)
                        ? "运行时 Goal Target 未就绪（_goalTargetsReady=false）"
                        : $"运行时 Goal Target 未就绪（缺失：{missing}）";
                }
                case BipedRuntimeGateCode.BipedRuntimeNotReady:
                    return "Biped 运行条件未满足（bipedRuntimeReady=false）";
                case BipedRuntimeGateCode.LookAtHandledByLookAtIK:
                    return "仅存在 LookAt 权重（由 LookAtIK 处理，不走 Biped 四肢求解）";
                case BipedRuntimeGateCode.NoSolvableLimbWeight:
                    return "当前无可求解四肢权重（pose.HasLimbWeight=false 且无平滑残留）";
                case BipedRuntimeGateCode.BipedSolveNotRequired:
                    return "当前帧无需触发 Biped 求解（requiresBipedSolve=false）";
                case BipedRuntimeGateCode.ReadyToSolve:
                    return "Biped 运行正常（可求解）";
                default:
                    return "未评估";
            }
        }
    }
}
