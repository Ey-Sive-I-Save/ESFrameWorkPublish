using UnityEngine;
using RootMotion.FinalIK;
using Sirenix.OdinInspector;

namespace ES
{
    public sealed partial class StateFinalIKDriver
    {
        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(0)]
        [BoxGroup("DriverLayout/公共部分/运行控制盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/运行控制盒/运行控制", BoldTitle = true)]
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

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(1)]
        [BoxGroup("DriverLayout/公共部分/运行控制盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/运行控制盒/运行控制")]
        [Button("应用初始参数到就绪的 IK", ButtonSizes.Medium)]
        [InfoBox("仅运行时有效。可在调节参数后点击重新应用，无需重启游戏。", InfoMessageType.Info)]
        private void ApplyIKInitialSettingsFromInspector()
        {
            ApplyIKInitialSettings();
        }

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(40)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [InfoBox("点击公共应用前，可先勾选本次要写回哪些 IK 组件。这样可以只更新某一条配置，而不影响其他已手调内容。", InfoMessageType.None)]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/应用选项行1", LabelWidth = 96)]
        [LabelText("写入 BipedIK")]
        [SerializeField] private bool applyToBipedIKFromInspector = true;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(40)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/应用选项行1", LabelWidth = 96)]
        [LabelText("写入 FullBody")]
        [SerializeField] private bool applyToFullBodyBipedIKFromInspector = true;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(40)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/应用选项行1", LabelWidth = 96)]
        [LabelText("写入 LookAt")]
        [SerializeField] private bool applyToLookAtIKFromInspector = true;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(40)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/应用选项行2", LabelWidth = 96)]
        [LabelText("写入 AimIK")]
        [SerializeField] private bool applyToAimIKFromInspector = true;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(40)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/应用选项行2", LabelWidth = 96)]
        [LabelText("写入 HitReaction")]
        [SerializeField] private bool applyToHitReactionFromInspector = true;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(40)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/应用选项行2", LabelWidth = 96)]
        [LabelText("写入 Recoil")]
        [SerializeField] private bool applyToRecoilFromInspector = true;

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(41)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/操作")]
        [Button("从 Animator 自动填充", ButtonSizes.Medium)]
        private void AutoFillDriverBoneBinding()
        {
            var animator = GetBindingAnimator();
            if (animator == null)
            {
                Debug.LogWarning("[StateFinalIKDriver] 未找到 Animator，无法自动填充骨骼绑定。", this);
                return;
            }

            useDriverBoneBinding = true;
            bindingRoot = animator.transform;
            bindingPelvis = animator.GetBoneTransform(HumanBodyBones.Hips);
            bindingSpine = animator.GetBoneTransform(HumanBodyBones.Spine);
            var upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            var chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            bindingChest = upperChest != null ? upperChest : chest;
            bindingNeck = animator.GetBoneTransform(HumanBodyBones.Neck);
            bindingHead = animator.GetBoneTransform(HumanBodyBones.Head);
            bindingLeftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            bindingRightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            bindingLeftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            bindingLeftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            bindingLeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            bindingRightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            bindingRightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            bindingRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            bindingLeftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            bindingLeftCalf = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            bindingLeftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            bindingRightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            bindingRightCalf = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            bindingRightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            SyncAimChainFromUnifiedBinding(animator);
        }

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(42)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/操作")]
        [Button("应用到已挂载 IK", ButtonSizes.Medium)]
        private void ApplyDriverBoneBindingFromInspector()
        {
            SyncAimChainFromUnifiedBinding(GetBindingAnimator());
            ApplyDriverBoneBindingsToConfiguredIK();

            if (!Application.isPlaying) return;

            _animator = GetBindingAnimator();
            if (enableBipedIK && _animator != null)
                TryRebindBipedIK();

            _refs.lookAtIK = _refs.lookAtIK ?? presetLookAtIK ?? GetComponent<LookAtIK>();
            _refs.aimIK = _refs.aimIK ?? presetAimIK ?? GetComponent<AimIK>();

            InitLookAtIK();
            InitAimIK();
            InitHitReactionAndRecoil();
            ApplyIKInitialSettings();
        }

        [TabGroup("DriverLayout", "公共部分")]
        [PropertyOrder(43)]
        [BoxGroup("DriverLayout/公共部分/统一骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定")]
        [HorizontalGroup("DriverLayout/公共部分/统一骨骼绑定盒/统一骨骼绑定/操作")]
        [Button("清空骨骼绑定", ButtonSizes.Medium)]
        private void ClearDriverBoneBinding()
        {
            useDriverBoneBinding = false;
            bindingRoot = null;
            bindingPelvis = null;
            bindingSpine = null;
            bindingChest = null;
            bindingNeck = null;
            bindingHead = null;
            bindingLeftEye = null;
            bindingRightEye = null;
            bindingLeftUpperArm = null;
            bindingLeftForearm = null;
            bindingLeftHand = null;
            bindingRightUpperArm = null;
            bindingRightForearm = null;
            bindingRightHand = null;
            bindingLeftThigh = null;
            bindingLeftCalf = null;
            bindingLeftFoot = null;
            bindingRightThigh = null;
            bindingRightCalf = null;
            bindingRightFoot = null;
        }

        [PropertyOrder(50)]
        [BoxGroup("【瞄准IK】（AimIK）/骨骼绑定盒", ShowLabel = false)]
        [TitleGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定")]
        [HorizontalGroup("【瞄准IK】（AimIK）/骨骼绑定盒/骨骼绑定/操作")]
        [Button("应用 AimIK 设置", ButtonSizes.Medium)]
        private void ApplyDriverAimChainFromInspector()
        {
            var aimIK = _refs.aimIK ?? presetAimIK ?? GetComponent<AimIK>();
            if (aimIK == null)
            {
                Debug.LogWarning("[StateFinalIKDriver] 当前未找到 AimIK 组件，无法应用骨链。", this);
                return;
            }

            if (!ApplyDriverAimChain(aimIK))
                return;

            if (Application.isPlaying)
            {
                _refs.aimIK = aimIK;
                InitAimIK();
                ApplyIKInitialSettings();
            }
        }

        [PropertyOrder(41)]
        [BoxGroup("【受击反馈】（HitReaction）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【受击反馈】（HitReaction）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【受击反馈】（HitReaction）/Driver配置盒/Driver配置/操作")]
        [Button("从当前向下识别骨骼盒", ButtonSizes.Medium)]
        private void AutoFillDriverHitReactionColliders()
        {
            if (bindingPelvis == null && bindingHead == null && bindingLeftForearm == null && bindingRightForearm == null)
            {
                var animator = GetBindingAnimator();
                if (animator != null)
                    AutoFillDriverBoneBinding();
            }

            useDriverHitReactionSetup = true;
            var result = transform.FindHitReactionBoneBoxesDownward(new DriverBoneBoxSearchContext
            {
                bodyPrimary = bindingChest,
                bodySecondary = bindingSpine,
                bodyTertiary = bindingPelvis,
                headPrimary = bindingHead,
                headSecondary = bindingNeck,
                leftArmPrimary = bindingLeftForearm,
                leftArmSecondary = bindingLeftUpperArm,
                leftArmTertiary = bindingLeftHand,
                rightArmPrimary = bindingRightForearm,
                rightArmSecondary = bindingRightUpperArm,
                rightArmTertiary = bindingRightHand,
                leftLegPrimary = bindingLeftCalf,
                leftLegSecondary = bindingLeftThigh,
                leftLegTertiary = bindingLeftFoot,
                rightLegPrimary = bindingRightCalf,
                rightLegSecondary = bindingRightThigh,
                rightLegTertiary = bindingRightFoot,
            });

            hitBodyCollider = result.body;
            hitHeadCollider = result.head;
            hitLeftArmCollider = result.leftArm;
            hitRightArmCollider = result.rightArm;
            hitLeftLegCollider = result.leftLeg;
            hitRightLegCollider = result.rightLeg;

            if (result.MatchedCount <= 0)
            {
                Debug.LogWarning("[StateFinalIKDriver] 从当前节点向下未识别到任何骨骼盒。请确认角色层级下存在 Collider，并优先先填充统一骨骼绑定。", this);
                return;
            }

            Debug.Log($"[StateFinalIKDriver] 已从当前节点向下识别骨骼盒，匹配数量：{result.MatchedCount}/6。", this);
        }

        [PropertyOrder(42)]
        [BoxGroup("【受击反馈】（HitReaction）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【受击反馈】（HitReaction）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【受击反馈】（HitReaction）/Driver配置盒/Driver配置/操作")]
        [Button("应用 Driver 受击配置", ButtonSizes.Medium)]
        private void ApplyDriverHitReactionFromInspector()
        {
            var hitReaction = _refs.hitReaction ?? presetHitReaction ?? GetComponent<HitReaction>();
            if (hitReaction == null)
            {
                Debug.LogWarning("[StateFinalIKDriver] 当前未找到 HitReaction 组件，无法应用 Driver 受击配置。", this);
                return;
            }

            ApplyDriverHitReaction(hitReaction);
            _refs.hitReaction = hitReaction;

            if (!Application.isPlaying) return;

            InitHitReactionAndRecoil();
        }

        [PropertyOrder(43)]
        [BoxGroup("【受击反馈】（HitReaction）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【受击反馈】（HitReaction）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【受击反馈】（HitReaction）/Driver配置盒/Driver配置/操作")]
        [Button("清空 Driver 受击配置", ButtonSizes.Medium)]
        private void ClearDriverHitReactionSetup()
        {
            useDriverHitReactionSetup = false;
            hitBodyCollider = null;
            hitHeadCollider = null;
            hitLeftArmCollider = null;
            hitRightArmCollider = null;
            hitLeftLegCollider = null;
            hitRightLegCollider = null;
        }

        [PropertyOrder(41)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/操作")]
        [Button("应用 Driver 后坐力配置", ButtonSizes.Medium)]
        private void ApplyDriverRecoilFromInspector()
        {
            var recoil = _refs.recoil ?? presetRecoil ?? GetComponent<Recoil>();
            if (recoil == null)
            {
                Debug.LogWarning("[StateFinalIKDriver] 当前未找到 Recoil 组件，无法应用 Driver 后坐力配置。", this);
                return;
            }

            ApplyDriverRecoil(recoil);
            _refs.recoil = recoil;

            if (!Application.isPlaying) return;

            InitHitReactionAndRecoil();
        }

        [PropertyOrder(42)]
        [BoxGroup("【后坐力】（Recoil）/Driver配置盒", ShowLabel = false)]
        [TitleGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置")]
        [HorizontalGroup("【后坐力】（Recoil）/Driver配置盒/Driver配置/操作")]
        [Button("清空 Driver 后坐力配置", ButtonSizes.Medium)]
        private void ClearDriverRecoilSetup()
        {
            useDriverRecoilSetup = false;
            driverRecoilWeight = 1f;
            driverRecoilHandedness = Recoil.Handedness.Right;
            driverRecoilTwoHanded = true;
            driverRecoilDuration = 0.18f;
            driverRecoilBlendTime = 0.08f;
            driverRecoilMagnitudeRandom = 0.08f;
            driverRecoilPrimaryOffset = new Vector3(0f, 0.02f, -0.06f);
            driverRecoilSecondaryOffset = new Vector3(0f, 0.01f, -0.035f);
            driverRecoilBodyOffset = new Vector3(0f, 0f, -0.015f);
            driverRecoilHandRotationOffset = new Vector3(-8f, 0f, 0f);
            driverRecoilRotationRandom = new Vector3(1.5f, 0.8f, 0.8f);
        }

        private void QuickAddComp_BipedIK()         => QuickAddCompInternal<BipedIK>("BipedIK");
        private void QuickAddComp_GrounderBipedIK() => QuickAddCompInternal<GrounderBipedIK>("GrounderBipedIK");
        private void QuickAddComp_LookAtIK()        => QuickAddCompInternal<LookAtIK>("LookAtIK");
        private void QuickAddComp_AimIK()           => QuickAddCompInternal<AimIK>("AimIK");
        private void QuickAddComp_FullBodyBipedIK() => QuickAddCompInternal<FullBodyBipedIK>("FullBodyBipedIK");
        private void QuickAddComp_HitReaction()     => QuickAddCompInternal<HitReaction>("HitReaction");
        private void QuickAddComp_Recoil()          => QuickAddCompInternal<Recoil>("Recoil");

        [TabGroup("DriverLayout", "示例脚本", Order = 90)]
        [PropertyOrder(0)]
        [BoxGroup("DriverLayout/示例脚本/快捷添加盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/示例脚本/快捷添加盒/快捷添加", BoldTitle = true)]
        [InfoBox("将对应示例脚本挂载到当前 GameObject。\nEntity / Driver 依赖字段需添加后在 Inspector 中手动指定。", InfoMessageType.None)]
        [Button("Example_IKAimController  —  AimIK 对准目标", ButtonSizes.Medium)]
        private void QuickAddExample_AimController()     => QuickAddCompInternal<ES.Examples.Example_IKAimController>("Example_IKAimController");

        [TabGroup("DriverLayout", "示例脚本")]
        [PropertyOrder(1)]
        [BoxGroup("DriverLayout/示例脚本/快捷添加盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/示例脚本/快捷添加盒/快捷添加")]
        [Button("Example_IKHitAndRecoil  —  受击 + 后坐力", ButtonSizes.Medium)]
        private void QuickAddExample_HitAndRecoil()      => QuickAddCompInternal<ES.Examples.Example_IKHitAndRecoil>("Example_IKHitAndRecoil");

        [TabGroup("DriverLayout", "示例脚本")]
        [PropertyOrder(2)]
        [BoxGroup("DriverLayout/示例脚本/快捷添加盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/示例脚本/快捷添加盒/快捷添加")]
        [Button("Example_IKGrounderController  —  自动接地", ButtonSizes.Medium)]
        private void QuickAddExample_GrounderController() => QuickAddCompInternal<ES.Examples.Example_IKGrounderController>("Example_IKGrounderController");

        [TabGroup("DriverLayout", "示例脚本")]
        [PropertyOrder(3)]
        [BoxGroup("DriverLayout/示例脚本/快捷添加盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/示例脚本/快捷添加盒/快捷添加")]
        [Button("Example_IKPoseHook  —  LookAt 事件钩子", ButtonSizes.Medium)]
        private void QuickAddExample_PoseHook()          => QuickAddCompInternal<ES.Examples.Example_IKPoseHook>("Example_IKPoseHook");

        [TabGroup("DriverLayout", "示例脚本")]
        [PropertyOrder(4)]
        [BoxGroup("DriverLayout/示例脚本/快捷添加盒", ShowLabel = false)]
        [TitleGroup("DriverLayout/示例脚本/快捷添加盒/快捷添加")]
        [Button("Example_IKStateBase  —  右手抓握 IK", ButtonSizes.Medium)]
        private void QuickAddExample_StateBase()         => QuickAddCompInternal<ES.Examples.Example_IKStateBase>("Example_IKStateBase");

        private void QuickAddCompInternal<T>(string label) where T : MonoBehaviour
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[StateFinalIKDriver] 快捷添加仅在编辑模式下有效。");
                return;
            }
            if (GetComponent<T>() != null)
            {
                Debug.Log($"[StateFinalIKDriver] {label} 已存在，无需重复添加。", this);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.Undo.AddComponent<T>(gameObject);
#else
            gameObject.AddComponent<T>();
#endif
            Debug.Log($"[StateFinalIKDriver] 已挂载 {label}。", this);
        }

    private static void RecordDrivenComponentBeforeEdit(Object target, string undoLabel)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || target == null) return;
        UnityEditor.Undo.RecordObject(target, undoLabel);
#endif
    }

    private static void FinalizeDrivenComponentEdit(Object target)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || target == null) return;

        UnityEditor.EditorUtility.SetDirty(target);
        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);

        if (target is Component component && component.gameObject.scene.IsValid())
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
#endif
    }

        private Animator GetBindingAnimator()
        {
            if (_animator != null) return _animator;
            return GetComponent<Animator>();
        }

        private Transform GetBindingRoot()
        {
            if (bindingRoot != null) return bindingRoot;
            var animator = GetBindingAnimator();
            return animator != null ? animator.transform : transform;
        }

        private static Transform[] CollectAssignedTransforms(params Transform[] candidates)
        {
            var list = new System.Collections.Generic.List<Transform>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null)
                    list.Add(candidates[i]);
            }
            return list.ToArray();
        }

        private static IKSolverLookAt.LookAtBone[] BuildLookAtBones(params Transform[] transforms)
        {
            var bones = new IKSolverLookAt.LookAtBone[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
                bones[i] = new IKSolverLookAt.LookAtBone { transform = transforms[i] };
            return bones;
        }

        private static AnimationCurve BuildPeakCurve(float duration, float peakValue)
        {
            float peakTime = Mathf.Max(0.01f, duration * 0.18f);
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(peakTime, peakValue),
                new Keyframe(duration, 0f));
        }

        private static HitReaction.HitPointEffector.EffectorLink CreateHitEffectorLink(FullBodyBipedEffector effector, float weight)
        {
            return new HitReaction.HitPointEffector.EffectorLink
            {
                effector = effector,
                weight = weight,
            };
        }

        private static Recoil.RecoilOffset.EffectorLink CreateRecoilEffectorLink(FullBodyBipedEffector effector, float weight)
        {
            return new Recoil.RecoilOffset.EffectorLink
            {
                effector = effector,
                weight = weight,
            };
        }

        private HitReaction.HitPointEffector CreateDriverHitEffectorPoint(string name, Collider collider, float forcePeak, float upPeak, params HitReaction.HitPointEffector.EffectorLink[] effectorLinks)
        {
            return new HitReaction.HitPointEffector
            {
                name = name,
                collider = collider,
                offsetInForceDirection = BuildPeakCurve(driverHitReactionDuration, forcePeak),
                offsetInUpDirection = BuildPeakCurve(driverHitReactionDuration, upPeak),
                effectorLinks = effectorLinks,
            };
        }

        private void ApplyDriverHitReaction(HitReaction hitReaction)
        {
            if (hitReaction == null) return;

            RecordDrivenComponentBeforeEdit(hitReaction, "Apply StateFinalIKDriver HitReaction Setup");

            var fullBodyBipedIK = _refs.fullBodyBipedIK ?? presetFullBodyBipedIK ?? GetComponent<FullBodyBipedIK>();
            if (fullBodyBipedIK != null)
            {
                hitReaction.ik = fullBodyBipedIK;
                _refs.fullBodyBipedIK = fullBodyBipedIK;
            }

            hitReaction.weight = Mathf.Clamp01(driverHitReactionWeight);

            var effectorHitPoints = new System.Collections.Generic.List<HitReaction.HitPointEffector>(5);

            if (hitBodyCollider != null)
            {
                effectorHitPoints.Add(CreateDriverHitEffectorPoint(
                    "Body",
                    hitBodyCollider,
                    0.1f,
                    driverHitReactionUpForce * 0.3f,
                    CreateHitEffectorLink(FullBodyBipedEffector.Body, 0.35f),
                    CreateHitEffectorLink(FullBodyBipedEffector.LeftShoulder, 0.15f),
                    CreateHitEffectorLink(FullBodyBipedEffector.RightShoulder, 0.15f)));
            }

            if (hitLeftArmCollider != null)
            {
                effectorHitPoints.Add(CreateDriverHitEffectorPoint(
                    "LeftArm",
                    hitLeftArmCollider,
                    0.16f,
                    driverHitReactionUpForce,
                    CreateHitEffectorLink(FullBodyBipedEffector.LeftHand, 0.9f),
                    CreateHitEffectorLink(FullBodyBipedEffector.LeftShoulder, 0.25f),
                    CreateHitEffectorLink(FullBodyBipedEffector.Body, 0.12f)));
            }

            if (hitRightArmCollider != null)
            {
                effectorHitPoints.Add(CreateDriverHitEffectorPoint(
                    "RightArm",
                    hitRightArmCollider,
                    0.16f,
                    driverHitReactionUpForce,
                    CreateHitEffectorLink(FullBodyBipedEffector.RightHand, 0.9f),
                    CreateHitEffectorLink(FullBodyBipedEffector.RightShoulder, 0.25f),
                    CreateHitEffectorLink(FullBodyBipedEffector.Body, 0.12f)));
            }

            if (hitLeftLegCollider != null)
            {
                effectorHitPoints.Add(CreateDriverHitEffectorPoint(
                    "LeftLeg",
                    hitLeftLegCollider,
                    0.12f,
                    driverHitReactionUpForce * 0.5f,
                    CreateHitEffectorLink(FullBodyBipedEffector.LeftFoot, 0.8f),
                    CreateHitEffectorLink(FullBodyBipedEffector.LeftThigh, 0.35f),
                    CreateHitEffectorLink(FullBodyBipedEffector.Body, 0.08f)));
            }

            if (hitRightLegCollider != null)
            {
                effectorHitPoints.Add(CreateDriverHitEffectorPoint(
                    "RightLeg",
                    hitRightLegCollider,
                    0.12f,
                    driverHitReactionUpForce * 0.5f,
                    CreateHitEffectorLink(FullBodyBipedEffector.RightFoot, 0.8f),
                    CreateHitEffectorLink(FullBodyBipedEffector.RightThigh, 0.35f),
                    CreateHitEffectorLink(FullBodyBipedEffector.Body, 0.08f)));
            }

            hitReaction.effectorHitPoints = effectorHitPoints.ToArray();

            var boneHitPoints = new System.Collections.Generic.List<HitReaction.HitPointBone>(1);
            if (hitHeadCollider != null && bindingHead != null)
            {
                boneHitPoints.Add(new HitReaction.HitPointBone
                {
                    name = "Head",
                    collider = hitHeadCollider,
                    aroundCenterOfMass = BuildPeakCurve(driverHitReactionDuration, driverHitReactionHeadAngle),
                    boneLinks = new[]
                    {
                        new HitReaction.HitPointBone.BoneLink
                        {
                            bone = bindingHead,
                            weight = 1f,
                        }
                    }
                });
            }

            hitReaction.boneHitPoints = boneHitPoints.ToArray();

            if (hitReaction.effectorHitPoints.Length == 0 && hitReaction.boneHitPoints.Length == 0)
            {
                Debug.LogWarning("[StateFinalIKDriver] Driver 受击配置未找到可用碰撞体，HitReaction 已挂载但不会产生命中反馈。", this);
            }

            FinalizeDrivenComponentEdit(hitReaction);
        }

        private void ApplyDriverRecoil(Recoil recoil)
        {
            if (recoil == null) return;

            RecordDrivenComponentBeforeEdit(recoil, "Apply StateFinalIKDriver Recoil Setup");

            var fullBodyBipedIK = _refs.fullBodyBipedIK ?? presetFullBodyBipedIK ?? GetComponent<FullBodyBipedIK>();
            if (fullBodyBipedIK != null)
            {
                recoil.ik = fullBodyBipedIK;
                _refs.fullBodyBipedIK = fullBodyBipedIK;
            }

            var aimIK = enableAimIK ? (_refs.aimIK ?? presetAimIK ?? GetComponent<AimIK>()) : null;
            if (aimIK != null)
                _refs.aimIK = aimIK;

            recoil.weight = Mathf.Clamp01(driverRecoilWeight);
            recoil.aimIK = aimIK;
            recoil.headIK = null;
            recoil.aimIKSolvedLast = false;
            recoil.handedness = driverRecoilHandedness;
            recoil.twoHanded = driverRecoilTwoHanded;
            recoil.recoilWeight = BuildPeakCurve(driverRecoilDuration, 1f);
            recoil.magnitudeRandom = driverRecoilMagnitudeRandom;
            recoil.rotationRandom = driverRecoilRotationRandom;
            recoil.handRotationOffset = driverRecoilHandRotationOffset;
            recoil.blendTime = driverRecoilBlendTime;

            FullBodyBipedEffector primaryHand = driverRecoilHandedness == Recoil.Handedness.Right ? FullBodyBipedEffector.RightHand : FullBodyBipedEffector.LeftHand;
            FullBodyBipedEffector secondaryHand = driverRecoilHandedness == Recoil.Handedness.Right ? FullBodyBipedEffector.LeftHand : FullBodyBipedEffector.RightHand;
            FullBodyBipedEffector primaryShoulder = driverRecoilHandedness == Recoil.Handedness.Right ? FullBodyBipedEffector.RightShoulder : FullBodyBipedEffector.LeftShoulder;
            FullBodyBipedEffector secondaryShoulder = driverRecoilHandedness == Recoil.Handedness.Right ? FullBodyBipedEffector.LeftShoulder : FullBodyBipedEffector.RightShoulder;

            var offsets = new System.Collections.Generic.List<Recoil.RecoilOffset>(3)
            {
                new Recoil.RecoilOffset
                {
                    offset = driverRecoilPrimaryOffset,
                    additivity = 0.65f,
                    maxAdditiveOffsetMag = Mathf.Max(0.05f, driverRecoilPrimaryOffset.magnitude * 1.5f),
                    effectorLinks = new[]
                    {
                        CreateRecoilEffectorLink(primaryHand, 1f),
                        CreateRecoilEffectorLink(primaryShoulder, 0.22f),
                    }
                },
                new Recoil.RecoilOffset
                {
                    offset = driverRecoilBodyOffset,
                    additivity = 0.35f,
                    maxAdditiveOffsetMag = Mathf.Max(0.02f, driverRecoilBodyOffset.magnitude * 1.5f),
                    effectorLinks = new[]
                    {
                        CreateRecoilEffectorLink(FullBodyBipedEffector.Body, 0.5f),
                        CreateRecoilEffectorLink(primaryShoulder, 0.2f),
                        CreateRecoilEffectorLink(secondaryShoulder, 0.12f),
                    }
                }
            };

            if (driverRecoilTwoHanded)
            {
                offsets.Add(new Recoil.RecoilOffset
                {
                    offset = driverRecoilSecondaryOffset,
                    additivity = 0.5f,
                    maxAdditiveOffsetMag = Mathf.Max(0.03f, driverRecoilSecondaryOffset.magnitude * 1.5f),
                    effectorLinks = new[]
                    {
                        CreateRecoilEffectorLink(secondaryHand, 0.75f),
                        CreateRecoilEffectorLink(secondaryShoulder, 0.15f),
                    }
                });
            }

            recoil.offsets = offsets.ToArray();

            FinalizeDrivenComponentEdit(recoil);
        }

        private RootMotion.BipedReferences BuildDriverBipedReferences()
        {
            return new RootMotion.BipedReferences
            {
                root = GetBindingRoot(),
                pelvis = bindingPelvis,
                leftThigh = bindingLeftThigh,
                leftCalf = bindingLeftCalf,
                leftFoot = bindingLeftFoot,
                rightThigh = bindingRightThigh,
                rightCalf = bindingRightCalf,
                rightFoot = bindingRightFoot,
                leftUpperArm = bindingLeftUpperArm,
                leftForearm = bindingLeftForearm,
                leftHand = bindingLeftHand,
                rightUpperArm = bindingRightUpperArm,
                rightForearm = bindingRightForearm,
                rightHand = bindingRightHand,
                head = bindingHead,
                spine = CollectAssignedTransforms(bindingSpine, bindingChest, bindingNeck),
                eyes = CollectAssignedTransforms(bindingLeftEye, bindingRightEye),
            };
        }

        private Transform GetPreferredFullBodyRootNode(RootMotion.BipedReferences references)
        {
            if (references.spine != null && references.spine.Length > 0)
                return references.spine[references.spine.Length - 1];
            return references.pelvis;
        }

        private void ApplyDriverBoneBindingsToConfiguredIK()
        {
            if (useDriverBoneBinding && (applyToBipedIKFromInspector || applyToFullBodyBipedIKFromInspector || applyToLookAtIKFromInspector || applyToAimIKFromInspector))
            {
                SyncAimChainFromUnifiedBinding(GetBindingAnimator());

                var references = BuildDriverBipedReferences();
                bool bipedBindingValid = references.isFilled;

                var bipedIK = _refs.bipedIK ?? presetBipedIK ?? GetComponent<BipedIK>();
                if (applyToBipedIKFromInspector && bipedBindingValid && bipedIK != null)
                {
                    RecordDrivenComponentBeforeEdit(bipedIK, "Apply StateFinalIKDriver BipedIK References");
                    bipedIK.references = references;
                    FinalizeDrivenComponentEdit(bipedIK);
                    _refs.bipedIK = bipedIK;
                }

                var fullBodyBipedIK = _refs.fullBodyBipedIK ?? presetFullBodyBipedIK ?? GetComponent<FullBodyBipedIK>();
                if (applyToFullBodyBipedIKFromInspector && bipedBindingValid && fullBodyBipedIK != null)
                {
                    RecordDrivenComponentBeforeEdit(fullBodyBipedIK, "Apply StateFinalIKDriver FullBodyBipedIK References");
                    if (Application.isPlaying)
                        fullBodyBipedIK.SetReferences(references, GetPreferredFullBodyRootNode(references));
                    else
                        fullBodyBipedIK.references = references;
                    FinalizeDrivenComponentEdit(fullBodyBipedIK);
                    _refs.fullBodyBipedIK = fullBodyBipedIK;
                }

                if (!bipedBindingValid && ((applyToBipedIKFromInspector && enableBipedIK) || (applyToFullBodyBipedIKFromInspector && enableFullBodyBipedIK)))
                {
                    Debug.LogWarning("[StateFinalIKDriver] Driver 骨骼绑定未完整，已跳过 BipedIK / FullBodyBipedIK 的 references 覆盖。请至少补齐四肢、骨盆和 Root。", this);
                }

                var lookAtIK = _refs.lookAtIK ?? presetLookAtIK ?? GetComponent<LookAtIK>();
                if (applyToLookAtIKFromInspector && lookAtIK != null)
                {
                    ApplyDriverLookAtBinding(lookAtIK);
                    _refs.lookAtIK = lookAtIK;
                }
            }

            if (applyToAimIKFromInspector)
            {
                var aimIK = _refs.aimIK ?? presetAimIK ?? GetComponent<AimIK>();
                if (aimIK != null)
                {
                    ApplyDriverAimChain(aimIK);
                    _refs.aimIK = aimIK;
                }
            }

            if (applyToHitReactionFromInspector && useDriverHitReactionSetup)
            {
                var hitReaction = _refs.hitReaction ?? presetHitReaction ?? GetComponent<HitReaction>();
                if (hitReaction != null)
                {
                    ApplyDriverHitReaction(hitReaction);
                    _refs.hitReaction = hitReaction;
                }
            }

            if (applyToRecoilFromInspector && useDriverRecoilSetup)
            {
                var recoil = _refs.recoil ?? presetRecoil ?? GetComponent<Recoil>();
                if (recoil != null)
                {
                    ApplyDriverRecoil(recoil);
                    _refs.recoil = recoil;
                }
            }
        }

        private void ApplyDriverLookAtBinding(LookAtIK lookAtIK)
        {
            if (lookAtIK == null) return;

            RecordDrivenComponentBeforeEdit(lookAtIK, "Apply StateFinalIKDriver LookAtIK Binding");

            if (bindingHead != null)
                lookAtIK.solver.head.transform = bindingHead;

            var spine = CollectAssignedTransforms(bindingSpine, bindingChest, bindingNeck);
            if (spine.Length > 0)
                lookAtIK.solver.spine = BuildLookAtBones(spine);

            var eyes = CollectAssignedTransforms(bindingLeftEye, bindingRightEye);
            if (eyes.Length > 0)
                lookAtIK.solver.eyes = BuildLookAtBones(eyes);

            if (Application.isPlaying)
                lookAtIK.solver.SetDirty();

            FinalizeDrivenComponentEdit(lookAtIK);
        }

        private bool ApplyDriverAimChain(AimIK aimIK)
        {
            if (aimIK == null) return false;

            if (!TryResolveValidatedAimChain(GetBindingAnimator(), out var chain, out var validationError, out var reorderMessage))
            {
                _aimIKError = validationError;
                Debug.LogError($"[StateFinalIKDriver] {validationError}", this);
                return false;
            }

            if (!string.IsNullOrEmpty(reorderMessage))
                Debug.LogWarning($"[StateFinalIKDriver] {reorderMessage}", this);

            if (!IsValidAimControlledTransform(aimControlledTransform, chain))
            {
                _aimIKError = "AimIK 缺少合法的瞄准方向节点。请在 Driver 中绑定玩家自定义的枪口/枪身方向节点，且不要直接使用躯干骨骼。";
                Debug.LogError($"[StateFinalIKDriver] {_aimIKError}", this);
                return false;
            }

            RecordDrivenComponentBeforeEdit(aimIK, "Apply StateFinalIKDriver AimIK Chain");

            if (Application.isPlaying)
            {
                aimIK.solver.SetChain(chain, GetBindingRoot());
            }
            else
            {
                aimIK.solver.bones = new IKSolver.Bone[chain.Length];
                for (int i = 0; i < chain.Length; i++)
                    aimIK.solver.bones[i] = new IKSolver.Bone { transform = chain[i] };
            }

            aimIK.solver.transform = aimControlledTransform;
            aimIK.solver.poleTarget = aimPoleTarget;
            if (aimPoleAxis != Vector3.zero)
                aimIK.solver.poleAxis = aimPoleAxis.normalized;
            aimIK.solver.poleWeight = aimPoleWeight;

            _aimIKError = string.Empty;
            FinalizeDrivenComponentEdit(aimIK);
            return true;
        }

        private void TryPopulateAimChainFromDriverBinding()
        {
            if (!enableAimIK) return;
            SyncAimChainFromUnifiedBinding(GetBindingAnimator());
        }

        private bool SyncAimChainFromUnifiedBinding(Animator animator)
        {
            if (!enableAimIK)
                return false;

            if (!TryResolveValidatedAimChain(animator, out var chain, out _, out _))
                return false;

            useDriverAimBoneChain = true;
            ApplyAimChainToDriverFields(chain);
            return true;
        }

        private Transform[] BuildPreferredAimBodyChain(Animator animator = null)
        {
            Transform pelvis = bindingPelvis;
            Transform spine = bindingSpine;
            Transform chest = bindingChest;
            Transform neck = bindingNeck;
            Transform head = bindingHead;

            if (animator != null)
            {
                if (pelvis == null)
                    pelvis = animator.GetBoneTransform(HumanBodyBones.Hips);

                if (spine == null)
                    spine = animator.GetBoneTransform(HumanBodyBones.Spine);

                if (chest == null)
                {
                    var upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                    var chestBone = animator.GetBoneTransform(HumanBodyBones.Chest);
                    chest = upperChest != null ? upperChest : chestBone;
                }

                if (neck == null)
                    neck = animator.GetBoneTransform(HumanBodyBones.Neck);

                if (head == null)
                    head = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            var root = animator != null ? animator.transform : GetBindingRoot();
            return BuildStableAimBodyChain(pelvis, spine, chest, neck, head, root);
        }

        private void ApplyAimChainToDriverFields(Transform[] chain)
        {
            chain = NormalizeAimChainParentToChild(chain);
            useDriverAimBoneChain = chain.Length == 4;
            aimChainBone1 = chain.Length > 0 ? chain[0] : null;
            aimChainBone2 = chain.Length > 1 ? chain[1] : null;
            aimChainBone3 = chain.Length > 2 ? chain[2] : null;
            aimChainBone4 = chain.Length > 3 ? chain[3] : null;
        }

        private bool TryResolveValidatedAimChain(Animator animator, out Transform[] chain, out string validationError, out string reorderMessage)
        {
            chain = System.Array.Empty<Transform>();
            validationError = string.Empty;
            reorderMessage = string.Empty;

            if (!useDriverBoneBinding)
            {
                useDriverAimBoneChain = false;
                ApplyAimChainToDriverFields(chain);
                validationError = "AimIK 已收口到总面板统一骨骼绑定。请先启用“Driver 骨骼绑定”，再应用 AimIK。";
                return false;
            }

            var rawChain = BuildPreferredAimBodyChain(animator);
            var normalizedChain = NormalizeAimChainParentToChild(rawChain);
            bool reordered = !AreSameOrderedTransforms(rawChain, normalizedChain);
            ApplyAimChainToDriverFields(normalizedChain);

            Transform resolvedNeck = ResolveAimNeck(animator);
            if (resolvedNeck == null)
            {
                validationError = "AimIK 无法确定第 4 节 neck。请在统一骨骼绑定中补齐 Neck，或确保 Animator 的 Humanoid Neck 可解析。";
                return false;
            }

            if (normalizedChain.Length != 4)
            {
                validationError = $"AimIK 需要 4 节父到子骨链，当前只能派生 {normalizedChain.Length} 节。请检查统一骨骼绑定中的 Spine / Chest / Neck 是否完整且位于同一躯干链。";
                return false;
            }

            if (normalizedChain[3] != resolvedNeck)
            {
                validationError = $"AimIK 第 4 节必须是 neck。当前第 4 节为 {(normalizedChain[3] != null ? normalizedChain[3].name : "<null>")}，解析出的 neck 为 {resolvedNeck.name}，已拒绝应用。";
                return false;
            }

            if (!IsStrictParentToChildChain(normalizedChain))
            {
                validationError = "AimIK 骨链无法整理为严格父到子链。Driver 已尝试重排，但统一骨骼绑定仍不合法，已拒绝应用。";
                return false;
            }

            if (reordered)
            {
                reorderMessage = "AimIK 骨链检测到顺序不规范，已自动重排为父到子顺序后再应用。请优先修正统一骨骼绑定，避免重复触发该提示。";
            }

            chain = normalizedChain;
            return true;
        }

        private Transform ResolveAimNeck(Animator animator)
        {
            Transform neck = bindingNeck;
            if (neck == null && animator != null)
                neck = animator.GetBoneTransform(HumanBodyBones.Neck);

            if (neck != null)
                return neck;

            Transform root = animator != null ? animator.transform : GetBindingRoot();
            return FindClosestAncestor(bindingHead, root, bindingPelvis, bindingSpine, bindingChest);
        }

        private static bool AreSameOrderedTransforms(Transform[] left, Transform[] right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static bool IsStrictParentToChildChain(Transform[] chain)
        {
            if (chain == null || chain.Length == 0)
                return false;

            for (int i = 1; i < chain.Length; i++)
            {
                if (chain[i - 1] == null || chain[i] == null)
                    return false;

                if (!IsAncestorOf(chain[i - 1], chain[i]))
                    return false;
            }

            return true;
        }

        private string BuildAimDerivedChainSummary()
        {
            var chain = BuildPreferredAimBodyChain(GetBindingAnimator());
            if (chain == null || chain.Length == 0)
                return "未派生到有效骨链";

            return string.Join(" -> ", System.Array.ConvertAll(chain, bone => bone != null ? bone.name : "<null>"));
        }

        private string GetAimBindingValidationSummary()
        {
            if (!enableAimIK)
                return "AimIK 未启用";

            if (!TryResolveValidatedAimChain(GetBindingAnimator(), out var chain, out var error, out _))
                return $"未通过：{error}";

            if (aimControlledTransform == null)
                return "未通过：缺少瞄准方向节点";

            return IsValidAimControlledTransform(aimControlledTransform, chain)
                ? "通过：统一骨骼绑定与 AimIK 控制节点均有效"
                : "未通过：瞄准方向节点不能直接使用躯干骨骼";
        }

        private Color GetAimBindingSummaryColor()
        {
            return GetAimBindingValidationSummary().StartsWith("通过")
                ? new Color(0.55f, 0.85f, 0.55f)
                : new Color(1f, 0.45f, 0.45f);
        }

        private bool ValidateAimControlledTransform(Transform controlledTransform)
        {
            if (!enableAimIK)
                return true;

            if (controlledTransform == null)
                return false;

            if (!TryResolveValidatedAimChain(GetBindingAnimator(), out var chain, out _, out _))
                return true;

            return IsValidAimControlledTransform(controlledTransform, chain);
        }

        private static bool IsValidAimControlledTransform(Transform aimTransform, Transform[] chain)
        {
            if (aimTransform == null)
                return false;

            if (chain == null)
                return true;

            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] == aimTransform)
                    return false;
            }

            return true;
        }

        private static Transform[] NormalizeAimChainParentToChild(Transform[] chain)
        {
            if (chain == null || chain.Length <= 1)
                return chain ?? System.Array.Empty<Transform>();

            var unique = new System.Collections.Generic.List<Transform>(chain.Length);
            for (int i = 0; i < chain.Length; i++)
            {
                var candidate = chain[i];
                if (candidate != null && !unique.Contains(candidate))
                    unique.Add(candidate);
            }

            unique.Sort(CompareAimChainTransforms);

            var normalized = new System.Collections.Generic.List<Transform>(unique.Count);
            for (int i = 0; i < unique.Count; i++)
            {
                var candidate = unique[i];
                if (normalized.Count == 0)
                {
                    normalized.Add(candidate);
                    continue;
                }

                if (IsAncestorOf(normalized[normalized.Count - 1], candidate))
                    normalized.Add(candidate);
            }

            return normalized.ToArray();
        }

        private static int CompareAimChainTransforms(Transform left, Transform right)
        {
            if (left == right)
                return 0;

            if (left == null)
                return 1;

            if (right == null)
                return -1;

            if (IsAncestorOf(left, right))
                return -1;

            if (IsAncestorOf(right, left))
                return 1;

            int depthCompare = GetTransformDepth(left).CompareTo(GetTransformDepth(right));
            if (depthCompare != 0)
                return depthCompare;

            return string.CompareOrdinal(left.name, right.name);
        }

        private static int GetTransformDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

        private static Transform[] BuildStableAimBodyChain(
            Transform pelvis,
            Transform spine,
            Transform chest,
            Transform neck,
            Transform head,
            Transform root)
        {
            if (neck == null)
                neck = FindClosestAncestor(head, root, pelvis, spine, chest);

            if (neck == null)
                return CollectAssignedTransforms(spine, chest, neck);

            var ancestors = GetAncestorChain(root, neck);
            var selected = new System.Collections.Generic.HashSet<Transform>();

            TryAddAimPrefixCandidate(selected, spine, neck);
            TryAddAimPrefixCandidate(selected, chest, neck);
            TryAddAimPrefixCandidate(selected, pelvis, neck);

            for (int i = ancestors.Count - 1; i >= 0 && selected.Count < 3; i--)
                TryAddAimPrefixCandidate(selected, ancestors[i], neck);

            var prefix = new System.Collections.Generic.List<Transform>(3);
            for (int i = 0; i < ancestors.Count; i++)
            {
                var candidate = ancestors[i];
                if (candidate != null && selected.Contains(candidate))
                    prefix.Add(candidate);
            }

            if (prefix.Count > 3)
                prefix.RemoveRange(0, prefix.Count - 3);

            while (prefix.Count < 3)
            {
                Transform fallback = FindAdditionalAimPrefixCandidate(ancestors, prefix, neck);
                if (fallback == null)
                    break;

                prefix.Insert(0, fallback);
            }

            prefix.Add(neck);
            return prefix.ToArray();
        }

        private static Transform FindClosestAncestor(Transform origin, Transform root, params Transform[] preferred)
        {
            Transform directParent = origin != null ? origin.parent : null;
            if (directParent != null && directParent != root)
                return directParent;

            for (int i = 0; i < preferred.Length; i++)
            {
                var candidate = preferred[i];
                if (candidate != null && candidate != origin && IsAncestorOf(candidate, origin))
                    return candidate;
            }

            return null;
        }

        private static System.Collections.Generic.List<Transform> GetAncestorChain(Transform root, Transform leaf)
        {
            var chain = new System.Collections.Generic.List<Transform>(8);
            Transform current = leaf != null ? leaf.parent : null;
            while (current != null && current != root)
            {
                chain.Add(current);
                current = current.parent;
            }

            chain.Reverse();
            return chain;
        }

        private static bool IsAncestorOf(Transform ancestor, Transform leaf)
        {
            if (ancestor == null || leaf == null || ancestor == leaf)
                return false;

            Transform current = leaf.parent;
            while (current != null)
            {
                if (current == ancestor)
                    return true;

                current = current.parent;
            }

            return false;
        }

        private static void TryAddAimPrefixCandidate(
            System.Collections.Generic.HashSet<Transform> selected,
            Transform candidate,
            Transform neck)
        {
            if (selected == null || candidate == null || candidate == neck)
                return;

            if (!IsAncestorOf(candidate, neck))
                return;

            selected.Add(candidate);
        }

        private static Transform FindAdditionalAimPrefixCandidate(
            System.Collections.Generic.List<Transform> ancestors,
            System.Collections.Generic.List<Transform> excludes,
            Transform neck)
        {
            for (int i = ancestors.Count - 1; i >= 0; i--)
            {
                var candidate = ancestors[i];
                if (candidate != null && candidate != neck && (excludes == null || !excludes.Contains(candidate)))
                    return candidate;
            }

            return null;
        }
    }
}