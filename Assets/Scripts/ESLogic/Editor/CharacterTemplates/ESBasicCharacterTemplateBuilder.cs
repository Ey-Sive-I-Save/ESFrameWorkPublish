using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES
{
    /// <summary>
    /// 统一角色模板工具。
    /// 第一次构建前模板用于继续替换模型、补充角色领域能力；完整通用架构由前者生成，
    /// 自动移除 Editor/Debug 节点，但不负责 AssetBundle 标记或 AssetLibrary 注册。
    /// </summary>
    public static class ESBasicCharacterTemplateBuilder
    {
        public const string TemplateFolder = "Assets/ESNormalAssets/CharacterTemplates";
        public const string TemplatePath = TemplateFolder + "/ES基础角色模板.prefab";
        public const string CompleteTemplatePath = TemplateFolder + "/ES通用角色完整架构.prefab";

        private const string DefaultStatePackPath =
            "Assets/ESNormalAssets/Data/Pack/StateAni/新建动画状态数据包24724.asset";

        private const string DefaultStateMachineConfigPath =
            "Assets/ESNormalAssets/Data/GlobalData/StateMachineConfig/默认动画器配置.asset";

        private const string DebugRootPath = "09_调试诊断_Debug";

        private static readonly string[] AuthoringTopLevelOrder =
        {
            "01_运行时逻辑_Runtime",
            "02_运动物理扩展_MotionPhysics",
            "03_模型表现_Presentation",
            "04_动画辅助_AnimationSupport",
            "05_检测碰撞_Detection",
            "06_装备_Equipment",
            "07_特效音频_Effects",
            "08_相机参考_CameraReferences",
            DebugRootPath,
            "10_运行时生成_RuntimeGenerated",
        };

        private static readonly string[] CompleteTopLevelOrder =
        {
            "01_运行时逻辑_Runtime",
            "02_运动物理扩展_MotionPhysics",
            "03_模型表现_Presentation",
            "04_动画辅助_AnimationSupport",
            "05_检测碰撞_Detection",
            "06_装备_Equipment",
            "07_特效音频_Effects",
            "08_相机参考_CameraReferences",
            "10_运行时生成_RuntimeGenerated",
        };

        private static readonly DefaultTransformKey[] RequiredDefaultMappings =
        {
            DefaultTransformKey.Root,
            DefaultTransformKey.Head,
            DefaultTransformKey.Chest,
            DefaultTransformKey.Hip,
            DefaultTransformKey.LeftHand,
            DefaultTransformKey.RightHand,
            DefaultTransformKey.LeftFoot,
            DefaultTransformKey.RightFoot,
            DefaultTransformKey.Weapon,
            DefaultTransformKey.Camera,
        };

        private static readonly string[] RequiredDynamicMappings =
        {
            "CameraTarget",
            "CameraAimTarget",
            "CameraLookTarget",
            "LockOnTarget",
            "IK_LeftHandTarget",
            "IK_RightHandTarget",
            "IK_LeftFootTarget",
            "IK_RightFootTarget",
            "IK_LookTarget",
            "IK_AimTarget",
            "MatchTarget_Root",
            "MatchTarget_Body",
            "MatchTarget_LeftHand",
            "MatchTarget_RightHand",
            "MatchTarget_LeftFoot",
            "MatchTarget_RightFoot",
            "HitVFXPoints",
            "SensorsRoot",
            "HitBoxesRoot",
            "HurtBoxesRoot",
            "InteractionProbe",
            "EquipmentRoot",
            "EquipmentVisuals",
            "VFXRoot",
            "AudioRoot",
            "TemporaryEffectsRoot",
            "RuntimeAttachmentsRoot",
            "RuntimeGeneratedRoot",
            "WeaponSocket",
        };

        private static readonly string[] SharedRequiredPaths =
        {
            "01_运行时逻辑_Runtime/运行时组件_RuntimeComponents",
            "02_运动物理扩展_MotionPhysics/环境影响接口_EnvironmentInfluence",
            "02_运动物理扩展_MotionPhysics/布娃娃预留_Ragdoll",
            "03_模型表现_Presentation/ModelOffset/ModelRoot",
            "04_动画辅助_AnimationSupport/IKTargets/LeftHandTarget",
            "04_动画辅助_AnimationSupport/IKTargets/RightHandTarget",
            "04_动画辅助_AnimationSupport/IKTargets/LeftFootTarget",
            "04_动画辅助_AnimationSupport/IKTargets/RightFootTarget",
            "04_动画辅助_AnimationSupport/IKTargets/LookTarget",
            "04_动画辅助_AnimationSupport/IKTargets/AimTarget",
            "04_动画辅助_AnimationSupport/Anchors/HeadAnchor",
            "04_动画辅助_AnimationSupport/Anchors/ChestAnchor",
            "04_动画辅助_AnimationSupport/Anchors/HipAnchor",
            "04_动画辅助_AnimationSupport/MatchTargets/RootMatchTarget",
            "04_动画辅助_AnimationSupport/MatchTargets/BodyMatchTarget",
            "04_动画辅助_AnimationSupport/MatchTargets/LeftHandMatchTarget",
            "04_动画辅助_AnimationSupport/MatchTargets/RightHandMatchTarget",
            "04_动画辅助_AnimationSupport/MatchTargets/LeftFootMatchTarget",
            "04_动画辅助_AnimationSupport/MatchTargets/RightFootMatchTarget",
            "04_动画辅助_AnimationSupport/HitVFXPoints",
            "05_检测碰撞_Detection/Sensors",
            "05_检测碰撞_Detection/HitBoxes",
            "05_检测碰撞_Detection/HurtBoxes",
            "05_检测碰撞_Detection/InteractionProbes/InteractionProbe",
            "06_装备_Equipment/WeaponSlots",
            "06_装备_Equipment/ArmorSlots",
            "06_装备_Equipment/EquipmentVisuals",
            "07_特效音频_Effects/VFX",
            "07_特效音频_Effects/Audio",
            "08_相机参考_CameraReferences/CameraTarget",
            "08_相机参考_CameraReferences/AimTarget",
            "08_相机参考_CameraReferences/LookTarget",
            "08_相机参考_CameraReferences/LockOnTarget",
            "10_运行时生成_RuntimeGenerated/TemporaryEffects",
            "10_运行时生成_RuntimeGenerated/RuntimeAttachments",
        };

        [MenuItem("【ES】/内容制作/角色模板/创建或重建首次构建基础模板", false, 100)]
        public static void BuildBasicCharacterTemplate()
        {
            GameObject prefab = BuildAuthoringTemplateInternal();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[角色模板] 已生成第一次构建前基础模板：{TemplatePath}", prefab);
        }

        [MenuItem("【ES】/内容制作/角色模板/从基础模板生成完整通用角色", false, 101)]
        public static void BuildCompleteCharacterTemplate()
        {
            GameObject prefab = BuildCompleteTemplateInternal();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"[角色模板] 已生成完整通用角色架构：{CompleteTemplatePath}", prefab);
        }

        [MenuItem("【ES】/内容制作/角色模板/创建并验证全部角色模板", false, 102)]
        public static void BuildAndValidateAllTemplates()
        {
            BuildAllAndValidate();
        }

        [MenuItem("【ES】/内容制作/角色模板/验证全部角色模板", false, 103)]
        public static void ValidateAllTemplatesMenu()
        {
            bool authoringValid = ValidateBasicCharacterTemplate(out string authoringReport);
            bool completeValid = ValidateCompleteCharacterTemplate(out string completeReport);
            if (!authoringValid || !completeValid)
            {
                Debug.LogError(authoringReport + "\n" + completeReport);
                return;
            }

            Debug.Log(authoringReport + "\n" + completeReport,
                AssetDatabase.LoadAssetAtPath<GameObject>(CompleteTemplatePath));
        }

        [MenuItem("【ES】/内容制作/角色模板/审计项目角色基础模块", false, 104)]
        public static void ValidateAllCharacterPrefabModulesMenu()
        {
            if (!ESCharacterTemplateReleaseGate.ValidateAllCharacterPrefabModuleContracts(out string report))
            {
                Debug.LogError(report);
                return;
            }

            Debug.Log(report);
        }

        [MenuItem("【ES】/内容制作/角色模板/运行完整角色运行态烟雾测试", false, 105)]
        public static void RunCharacterTemplateRuntimeSelfTestMenu()
        {
            RunCharacterTemplateRuntimeSelfTest();
        }

        // Unity 命令行批处理入口。保留原入口名称，当前语义是生成并验证两个模板。
        public static void BuildBasicCharacterTemplateBatch()
        {
            BuildAllAndValidate();
        }

        public static void RunCharacterTemplateRuntimeSelfTestBatch()
        {
            RunCharacterTemplateRuntimeSelfTest();
        }

        public static bool ValidateBasicCharacterTemplate(out string report)
        {
            return ValidateTemplate(TemplatePath, expectDebugRoot: true, out report);
        }

        public static bool ValidateCompleteCharacterTemplate(out string report)
        {
            return ValidateTemplate(CompleteTemplatePath, expectDebugRoot: false, out report);
        }

        private static void BuildAllAndValidate()
        {
            BuildAuthoringTemplateInternal();
            BuildCompleteTemplateInternal();

            bool authoringValid = ValidateBasicCharacterTemplate(out string authoringReport);
            bool completeValid = ValidateCompleteCharacterTemplate(out string completeReport);
            if (!authoringValid || !completeValid)
                throw new InvalidOperationException(authoringReport + "\n" + completeReport);

            Debug.Log(authoringReport);
            Debug.Log(completeReport);
        }

        private static GameObject BuildAuthoringTemplateInternal()
        {
            EnsureAssetFolder(TemplateFolder);

            GameObject root = CreateTemplateRoot("ES基础角色模板_首次构建前", includeDebugRoot: true);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TemplatePath);
                if (prefab == null)
                    throw new InvalidOperationException($"保存第一次构建前基础模板失败：{TemplatePath}");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildCompleteTemplateInternal()
        {
            EnsureAssetFolder(TemplateFolder);

            GameObject authoringPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            if (authoringPrefab == null)
                authoringPrefab = BuildAuthoringTemplateInternal();

            GameObject root = PrefabUtility.InstantiatePrefab(authoringPrefab) as GameObject;
            if (root == null)
                throw new InvalidOperationException($"无法实例化第一次构建前基础模板：{TemplatePath}");

            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        root,
                        PrefabUnpackMode.OutermostRoot,
                        InteractionMode.AutomatedAction);
                }

                root.name = "ES通用角色完整架构";
                StripEditorAndDebugContent(root);
                ConfigureRuntimeReadyState(root);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CompleteTemplatePath);
                if (prefab == null)
                    throw new InvalidOperationException($"保存完整通用角色架构失败：{CompleteTemplatePath}");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateTemplateRoot(string rootName, bool includeDebugRoot)
        {
            GameObject root = new GameObject(rootName);
            root.layer = ESPhysicsLayers.EntityBody;

            Transform runtimeRoot = CreateNode(root.transform, "01_运行时逻辑_Runtime");
            CreateNode(runtimeRoot, "运行时组件_RuntimeComponents");

            Transform motionRoot = CreateNode(root.transform, "02_运动物理扩展_MotionPhysics");
            CreateNode(motionRoot, "环境影响接口_EnvironmentInfluence");
            CreateNode(motionRoot, "布娃娃预留_Ragdoll");

            Transform presentationRoot = CreateNode(root.transform, "03_模型表现_Presentation");
            Transform modelOffset = CreateNode(presentationRoot, "ModelOffset");
            Transform modelRoot = CreateNode(modelOffset, "ModelRoot");

            Transform animationSupport = CreateNode(root.transform, "04_动画辅助_AnimationSupport");
            Transform ikTargets = CreateNode(animationSupport, "IKTargets");
            Transform leftHandTarget = CreateNode(ikTargets, "LeftHandTarget", new Vector3(-0.35f, 1.2f, 0.35f));
            Transform rightHandTarget = CreateNode(ikTargets, "RightHandTarget", new Vector3(0.35f, 1.2f, 0.35f));
            Transform leftFootTarget = CreateNode(ikTargets, "LeftFootTarget", new Vector3(-0.15f, 0.05f, 0.05f));
            Transform rightFootTarget = CreateNode(ikTargets, "RightFootTarget", new Vector3(0.15f, 0.05f, 0.05f));
            Transform ikLookTarget = CreateNode(ikTargets, "LookTarget", new Vector3(0f, 1.6f, 2f));
            Transform ikAimTarget = CreateNode(ikTargets, "AimTarget", new Vector3(0f, 1.45f, 4f));

            Transform anchors = CreateNode(animationSupport, "Anchors");
            Transform headAnchor = CreateNode(anchors, "HeadAnchor", new Vector3(0f, 1.65f, 0f));
            Transform chestAnchor = CreateNode(anchors, "ChestAnchor", new Vector3(0f, 1.25f, 0f));
            Transform hipAnchor = CreateNode(anchors, "HipAnchor", new Vector3(0f, 0.9f, 0f));

            Transform matchTargets = CreateNode(animationSupport, "MatchTargets");
            Transform rootMatchTarget = CreateNode(matchTargets, "RootMatchTarget");
            Transform bodyMatchTarget = CreateNode(matchTargets, "BodyMatchTarget", new Vector3(0f, 0.9f, 0f));
            Transform leftHandMatchTarget = CreateNode(matchTargets, "LeftHandMatchTarget", new Vector3(-0.35f, 1.2f, 0.35f));
            Transform rightHandMatchTarget = CreateNode(matchTargets, "RightHandMatchTarget", new Vector3(0.35f, 1.2f, 0.35f));
            Transform leftFootMatchTarget = CreateNode(matchTargets, "LeftFootMatchTarget", new Vector3(-0.15f, 0f, 0.1f));
            Transform rightFootMatchTarget = CreateNode(matchTargets, "RightFootMatchTarget", new Vector3(0.15f, 0f, 0.1f));
            Transform hitVfxPoints = CreateNode(animationSupport, "HitVFXPoints");

            Transform detectionRoot = CreateNode(root.transform, "05_检测碰撞_Detection");
            Transform sensors = CreateNode(detectionRoot, "Sensors");
            Transform hitBoxes = CreateNode(detectionRoot, "HitBoxes");
            Transform hurtBoxes = CreateNode(detectionRoot, "HurtBoxes");
            Transform interactionProbes = CreateNode(detectionRoot, "InteractionProbes");
            Transform interactionProbe = CreateNode(interactionProbes, "InteractionProbe", new Vector3(0f, 1f, 0.6f));

            Transform equipmentRoot = CreateNode(root.transform, "06_装备_Equipment");
            Transform weaponSlots = CreateNode(equipmentRoot, "WeaponSlots");
            Transform weaponSocket = CreateNode(weaponSlots, "WeaponSocket", new Vector3(0.25f, 1.15f, 0.2f));
            CreateNode(equipmentRoot, "ArmorSlots");
            Transform equipmentVisuals = CreateNode(equipmentRoot, "EquipmentVisuals");

            Transform effectsRoot = CreateNode(root.transform, "07_特效音频_Effects");
            Transform vfxRoot = CreateNode(effectsRoot, "VFX");
            Transform audioRoot = CreateNode(effectsRoot, "Audio");

            Transform cameraRoot = CreateNode(root.transform, "08_相机参考_CameraReferences");
            Transform cameraTarget = CreateNode(cameraRoot, "CameraTarget", new Vector3(0f, 1.55f, 0f));
            Transform cameraAimTarget = CreateNode(cameraRoot, "AimTarget", new Vector3(0f, 1.45f, 0.25f));
            Transform cameraLookTarget = CreateNode(cameraRoot, "LookTarget", new Vector3(0f, 1.6f, 0.3f));
            Transform lockOnTarget = CreateNode(cameraRoot, "LockOnTarget", new Vector3(0f, 1.2f, 0f));

            Transform debugRoot = null;
            if (includeDebugRoot)
            {
                debugRoot = CreateNode(root.transform, DebugRootPath);
                debugRoot.gameObject.tag = "EditorOnly";
                CreateNode(debugRoot, "Gizmos");
                CreateNode(debugRoot, "DevelopmentOnly");
            }

            Transform generatedRoot = CreateNode(root.transform, "10_运行时生成_RuntimeGenerated");
            Transform temporaryEffects = CreateNode(generatedRoot, "TemporaryEffects");
            Transform runtimeAttachments = CreateNode(generatedRoot, "RuntimeAttachments");

            StateMachineConfig stateMachineConfig =
                AssetDatabase.LoadAssetAtPath<StateMachineConfig>(DefaultStateMachineConfigPath);
            Animator animator = CreateModelAndAnimator(modelRoot, stateMachineConfig);
            StateFinalIKDriver ikDriver = EnsureIKDriver(animator);
            BakeHumanoidBinding(ikDriver, animator);
            ConfigureFinalIKBaseline(ikDriver);

            Entity entity = root.AddComponent<Entity>();
            entity.EnsureEntityStructure();
            entity.animator = animator;
            entity.kcc.Initialize(entity);
            ConfigureKcc(entity);
            ConfigureDomains(entity, cameraAimTarget);

            EntityTransformMapping mapping = root.AddComponent<EntityTransformMapping>();
            ConfigureMapping(
                mapping,
                root.transform,
                animator,
                headAnchor,
                chestAnchor,
                hipAnchor,
                weaponSocket,
                cameraTarget,
                cameraAimTarget,
                cameraLookTarget,
                lockOnTarget,
                leftHandTarget,
                rightHandTarget,
                leftFootTarget,
                rightFootTarget,
                ikLookTarget,
                ikAimTarget,
                rootMatchTarget,
                bodyMatchTarget,
                leftHandMatchTarget,
                rightHandMatchTarget,
                leftFootMatchTarget,
                rightFootMatchTarget,
                hitVfxPoints,
                sensors,
                hitBoxes,
                hurtBoxes,
                interactionProbe,
                equipmentRoot,
                equipmentVisuals,
                vfxRoot,
                audioRoot,
                temporaryEffects,
                runtimeAttachments,
                generatedRoot,
                debugRoot);

            EntityCharacterIdentity profile = root.AddComponent<EntityCharacterIdentity>();
            profile.ConfigureBuildInput();
            mapping.RebuildRuntimeCache();

            return root;
        }

        private static Animator CreateModelAndAnimator(Transform modelRoot, StateMachineConfig config)
        {
            GameObject modelAsset = config != null ? config.previewModel as GameObject : null;
            GameObject modelInstance = null;
            if (modelAsset != null)
            {
                modelInstance = PrefabUtility.InstantiatePrefab(modelAsset, modelRoot) as GameObject;
            }

            if (modelInstance == null)
            {
                modelInstance = new GameObject("占位模型_Placeholder");
                modelInstance.transform.SetParent(modelRoot, false);
            }
            else
            {
                modelInstance.name = "占位模型_GlobalPreview";
                ResetLocalTransform(modelInstance.transform);
            }

            Animator animator = modelInstance.GetComponentInChildren<Animator>(true);
            if (animator == null)
                animator = modelInstance.AddComponent<Animator>();

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (config != null && config.previewAvatar != null)
                animator.avatar = config.previewAvatar;
            return animator;
        }

        private static StateFinalIKDriver EnsureIKDriver(Animator animator)
        {
            StateFinalIKDriver driver = animator.GetComponent<StateFinalIKDriver>();
            if (driver == null)
                driver = animator.gameObject.AddComponent<StateFinalIKDriver>();
            return driver;
        }

        private static void BakeHumanoidBinding(StateFinalIKDriver driver, Animator animator)
        {
            driver?.ConfigureHumanoidBinding(animator);
        }

        private static void ConfigureKcc(Entity entity)
        {
            entity.kcc.standingCapsuleHeight = 1.8f;
            entity.kcc.crouchedCapsuleHeight = 1f;
            entity.kcc.motor.SetCapsuleDimensions(0.35f, 1.8f, 0.9f);
            entity.kcc.motor.MaxStableSlopeAngle = 50f;
            entity.kcc.motor.MaxStepHeight = 0.4f;
            entity.kcc.motor.StableGroundLayers = ESPhysicsLayers.GroundProbeMask;
            entity.kcc.maxStableMoveSpeed = 3.5f;
            entity.kcc.maxAirMoveSpeed = 6f;
            entity.kcc.airAccelerationSpeed = 5f;
            entity.kcc.drag = 0.1f;
            entity.kcc.jumpSpeed = 8f;
            entity.kcc.orientationSharpness = 10f;
            entity.kcc.gravity_ = new Vector3(0f, -9.81f, 0f);
            entity.kcc.useRootMotion = false;
            entity.kcc.debugMonitor = false;
        }

        private static void ConfigureDomains(Entity entity, Transform aimTarget)
        {
            entity.basicDomain.MyModules.Add(new EntityBasicMoveRotateModule());
            entity.basicDomain.MyModules.ApplyBuffers(true);

            entity.aiDomain.turnMode = TurnMode.MoveDirection;
            entity.aiDomain.enableCameraLook = false;
            entity.aiDomain.driveAimIK = false;
            entity.aiDomain.aimTransform = aimTarget;
            entity.aiDomain.debugCamera = false;
            entity.aiDomain.MyModules.ApplyBuffers(true);

            entity.stateDomain.stateAniDataPack =
                AssetDatabase.LoadAssetAtPath<StateAniDataPack>(DefaultStatePackPath);
            entity.stateDomain.stateMachine.Config =
                AssetDatabase.LoadAssetAtPath<StateMachineConfig>(DefaultStateMachineConfigPath);
            entity.stateDomain.defaultStateKey = "站立移动";
            entity.stateDomain.initialStateName = string.Empty;
        }

        private static void ConfigureMapping(
            EntityTransformMapping mapping,
            Transform root,
            Animator animator,
            Transform headFallback,
            Transform chestFallback,
            Transform hipFallback,
            Transform weaponSocket,
            Transform cameraTarget,
            Transform cameraAimTarget,
            Transform cameraLookTarget,
            Transform lockOnTarget,
            Transform leftHandTarget,
            Transform rightHandTarget,
            Transform leftFootTarget,
            Transform rightFootTarget,
            Transform ikLookTarget,
            Transform ikAimTarget,
            Transform rootMatchTarget,
            Transform bodyMatchTarget,
            Transform leftHandMatchTarget,
            Transform rightHandMatchTarget,
            Transform leftFootMatchTarget,
            Transform rightFootMatchTarget,
            Transform hitVfxPoints,
            Transform sensors,
            Transform hitBoxes,
            Transform hurtBoxes,
            Transform interactionProbe,
            Transform equipmentRoot,
            Transform equipmentVisuals,
            Transform vfxRoot,
            Transform audioRoot,
            Transform temporaryEffects,
            Transform runtimeAttachments,
            Transform generatedRoot,
            Transform debugRoot)
        {
            mapping.Set(DefaultTransformKey.Root, root);
            mapping.Set(DefaultTransformKey.Head, GetHumanBone(animator, HumanBodyBones.Head) ?? headFallback);
            mapping.Set(DefaultTransformKey.Chest, ResolveChest(animator) ?? chestFallback);
            mapping.Set(DefaultTransformKey.Hip, GetHumanBone(animator, HumanBodyBones.Hips) ?? hipFallback);
            mapping.Set(DefaultTransformKey.LeftHand, GetHumanBone(animator, HumanBodyBones.LeftHand) ?? leftHandTarget);
            mapping.Set(DefaultTransformKey.RightHand, GetHumanBone(animator, HumanBodyBones.RightHand) ?? rightHandTarget);
            mapping.Set(DefaultTransformKey.LeftFoot, GetHumanBone(animator, HumanBodyBones.LeftFoot) ?? leftFootTarget);
            mapping.Set(DefaultTransformKey.RightFoot, GetHumanBone(animator, HumanBodyBones.RightFoot) ?? rightFootTarget);
            // Weapon 是制作好的挂载 Socket；RightHand 则始终保留为骨骼语义。
            // 这样每个角色可在 Socket 上处理手型、偏移和双手武器辅助，而不会把业务挂载混入 Humanoid 骨骼。
            mapping.Set(DefaultTransformKey.Weapon, weaponSocket ?? GetHumanBone(animator, HumanBodyBones.RightHand));
            mapping.Set(DefaultTransformKey.Camera, cameraTarget);

            mapping.Set("CameraTarget", cameraTarget);
            mapping.Set("CameraAimTarget", cameraAimTarget);
            mapping.Set("CameraLookTarget", cameraLookTarget);
            mapping.Set("LockOnTarget", lockOnTarget);
            mapping.Set("IK_LeftHandTarget", leftHandTarget);
            mapping.Set("IK_RightHandTarget", rightHandTarget);
            mapping.Set("IK_LeftFootTarget", leftFootTarget);
            mapping.Set("IK_RightFootTarget", rightFootTarget);
            mapping.Set("IK_LookTarget", ikLookTarget);
            mapping.Set("IK_AimTarget", ikAimTarget);
            mapping.Set("MatchTarget_Root", rootMatchTarget);
            mapping.Set("MatchTarget_Body", bodyMatchTarget);
            mapping.Set("MatchTarget_LeftHand", leftHandMatchTarget);
            mapping.Set("MatchTarget_RightHand", rightHandMatchTarget);
            mapping.Set("MatchTarget_LeftFoot", leftFootMatchTarget);
            mapping.Set("MatchTarget_RightFoot", rightFootMatchTarget);
            mapping.Set("HitVFXPoints", hitVfxPoints);
            mapping.Set("SensorsRoot", sensors);
            mapping.Set("HitBoxesRoot", hitBoxes);
            mapping.Set("HurtBoxesRoot", hurtBoxes);
            mapping.Set("InteractionProbe", interactionProbe);
            mapping.Set("EquipmentRoot", equipmentRoot);
            mapping.Set("EquipmentVisuals", equipmentVisuals);
            mapping.Set("VFXRoot", vfxRoot);
            mapping.Set("AudioRoot", audioRoot);
            mapping.Set("TemporaryEffectsRoot", temporaryEffects);
            mapping.Set("RuntimeAttachmentsRoot", runtimeAttachments);
            mapping.Set("RuntimeGeneratedRoot", generatedRoot);
            mapping.Set("WeaponSocket", weaponSocket);
            if (debugRoot != null)
                mapping.Set("DebugRoot", debugRoot);
        }

        /// <summary>
        /// 基础与通用池模板不携带 FinalIK Solver。明确关闭功能，避免 Driver 因默认开关而静默退化。
        /// 正式角色 Variant 只有在挂齐对应 Solver 后才能重新打开功能。
        /// </summary>
        private static void ConfigureFinalIKBaseline(StateFinalIKDriver driver)
        {
            driver?.ConfigureSolverFreeTemplateBaseline();
        }

        private static void StripEditorAndDebugContent(GameObject root)
        {
            Transform debugRoot = root.transform.Find(DebugRootPath);
            if (debugRoot != null)
                UnityEngine.Object.DestroyImmediate(debugRoot.gameObject);

            EntityTransformMapping mapping = root.GetComponent<EntityTransformMapping>();
            mapping?.Remove("DebugRoot");
        }

        private static void ConfigureRuntimeReadyState(GameObject root)
        {
            Entity entity = root.GetComponent<Entity>();
            if (entity == null)
                throw new InvalidOperationException("完整通用角色缺少根 Entity。");

            entity.EnsureEntityStructure();
            entity.kcc.debugMonitor = false;

            Animator animator = entity.animator != null
                ? entity.animator
                : root.GetComponentInChildren<Animator>(true);
            if (animator == null)
                throw new InvalidOperationException("完整通用角色缺少 Animator。");

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            entity.animator = animator;
            StateFinalIKDriver driver = EnsureIKDriver(animator);
            BakeHumanoidBinding(driver, animator);
            ConfigureFinalIKBaseline(driver);

            EntityCharacterIdentity profile = root.GetComponent<EntityCharacterIdentity>();
            if (profile == null)
                throw new InvalidOperationException("完整通用角色缺少 EntityCharacterIdentity。");
            profile.ConfigureRuntimePoolTemplate();

            entity.aiDomain.debugCamera = false;
        }

        private static bool ValidateTemplate(string path, bool expectDebugRoot, out string report)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report = $"[角色模板检查] 未找到模板：{path}";
                return false;
            }

            Entity[] entities = prefab.GetComponentsInChildren<Entity>(true);
            KinematicCharacterController.KinematicCharacterMotor[] motors =
                prefab.GetComponentsInChildren<KinematicCharacterController.KinematicCharacterMotor>(true);
            CapsuleCollider[] capsules = prefab.GetComponentsInChildren<CapsuleCollider>(true);
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            EntityTransformMapping[] mappings = prefab.GetComponentsInChildren<EntityTransformMapping>(true);
            EntityCharacterIdentity[] profiles = prefab.GetComponentsInChildren<EntityCharacterIdentity>(true);
            EntityWeaponBinding[] weaponBindings = prefab.GetComponentsInChildren<EntityWeaponBinding>(true);
            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            StateFinalIKDriver[] ikDrivers = prefab.GetComponentsInChildren<StateFinalIKDriver>(true);

            Entity entity = prefab.GetComponent<Entity>();
            KinematicCharacterController.KinematicCharacterMotor motor =
                prefab.GetComponent<KinematicCharacterController.KinematicCharacterMotor>();
            CapsuleCollider capsule = prefab.GetComponent<CapsuleCollider>();
            Rigidbody rootRigidbody = prefab.GetComponent<Rigidbody>();
            EntityTransformMapping mapping = prefab.GetComponent<EntityTransformMapping>();
            EntityCharacterIdentity profile = prefab.GetComponent<EntityCharacterIdentity>();
            Animator animator = animators.Length == 1 ? animators[0] : null;
            StateFinalIKDriver ikDriver = animator != null ? animator.GetComponent<StateFinalIKDriver>() : null;

            bool componentCountValid = entities.Length == 1
                && motors.Length == 1
                && capsules.Length == 1
                && colliders.Length == 1
                && mappings.Length == 1
                && profiles.Length == 1
                && animators.Length == 1
                && ikDrivers.Length == 1
                && weaponBindings.Length == 0;

            bool rootTransformValid = Approximately(prefab.transform.localPosition, Vector3.zero)
                && Approximately(prefab.transform.localRotation, Quaternion.identity)
                && Approximately(prefab.transform.localScale, Vector3.one);

            bool rootAuthorityValid = componentCountValid
                && rootTransformValid
                && entity != null
                && motor != null
                && capsule != null
                && rootRigidbody == null
                && mapping != null
                && profile != null
                && entity.kcc != null
                && entity.kcc.motor == motor
                && motor.Capsule == capsule
                && Approximately(capsule.radius, 0.35f)
                && Approximately(capsule.height, entity.kcc.standingCapsuleHeight)
                && Approximately(capsule.center, new Vector3(0f, entity.kcc.standingCapsuleHeight * 0.5f, 0f))
                && Approximately(entity.kcc.standingCapsuleHeight, 1.8f)
                && entity.kcc.crouchedCapsuleHeight > capsule.radius * 2f
                && entity.kcc.crouchedCapsuleHeight < entity.kcc.standingCapsuleHeight;

            bool animationValid = animator != null
                && entity != null
                && entity.animator == animator
                && animator.runtimeAnimatorController == null
                && !animator.applyRootMotion
                && animator.cullingMode == AnimatorCullingMode.AlwaysAnimate
                && animator.avatar != null
                && animator.avatar.isValid
                && ikDriver != null
                && ValidateIKBoneBinding(ikDriver, animator)
                && ikDriver.ValidateEnabledSolverContract(out _)
                && ValidateFinalIKTemplateBaseline(ikDriver)
                && entity.stateDomain != null
                && entity.stateDomain.stateMachine != null
                && entity.stateDomain.stateMachine.Config != null
                && entity.stateDomain.stateAniDataPack != null;

            bool hierarchyValid = true;
            for (int i = 0; i < SharedRequiredPaths.Length; i++)
            {
                if (!HasChild(prefab.transform, SharedRequiredPaths[i]))
                {
                    hierarchyValid = false;
                    break;
                }
            }

            bool hasDebugRoot = HasChild(prefab.transform, DebugRootPath);
            hierarchyValid &= hasDebugRoot == expectDebugRoot;
            hierarchyValid &= ValidateTopLevelOrder(
                prefab.transform,
                expectDebugRoot ? AuthoringTopLevelOrder : CompleteTopLevelOrder);

            int moveCount = CountBasicModule<EntityBasicMoveRotateModule>(entity);
            int playerWriterCount = CountAiModule<EntityPlayerInputWriteModule>(entity);
            int optionalMotionCount = CountOptionalMotionModules(entity);
            bool modulesValid = moveCount == 1
                && playerWriterCount == 0
                && optionalMotionCount == 0
                && entity != null
                && entity.aiDomain != null;

            bool mappingValid = ValidateAllMappings(mapping, prefab.transform, expectDebugRoot);
            EntityCharacterPrefabRole expectedProfileRole = expectDebugRoot
                ? EntityCharacterPrefabRole.BuildInput
                : EntityCharacterPrefabRole.RuntimePoolTemplate;
            string profileError = profile == null ? "缺少 EntityCharacterIdentity" : string.Empty;
            bool profileValid = profile != null && profile.ValidateTemplateRole(expectedProfileRole, out profileError);
            bool stripValid = ValidateEditorOnlyPolicy(prefab.transform, expectDebugRoot)
                && ValidateRuntimeGeneratedIsEmpty(prefab.transform)
                && ValidateRigidbodyPolicy(prefab.transform)
                && ValidateAssetDependenciesAndBundleLabel(path, expectDebugRoot);

            int missingScripts = CountMissingScriptsRecursive(prefab.transform);
            bool valid = rootAuthorityValid
                && animationValid
                && hierarchyValid
                && modulesValid
                && mappingValid
                && profileValid
                && stripValid
                && missingScripts == 0;

            string stage = expectDebugRoot ? "第一次构建前基础模板" : "完整通用角色架构";
            report = valid
                ? $"[{stage}检查] 通过：底盘组件唯一性、无武器内容组件、KCC胶囊契约、Entity四域、Playable动画、轻量禁用IK、全量Mapping、十区顺序、递归Missing Script、Rigidbody和运行时剥离规则完整。"
                : $"[{stage}检查] 未通过 | Root={rootAuthorityValid} | Animation={animationValid} | "
                  + $"Components={componentCountValid} | Hierarchy={hierarchyValid} | Mapping={mappingValid} | Profile={profileValid}({profileError}) | Strip={stripValid} | MissingScripts={missingScripts} | "
                  + $"Move={moveCount}, DomainExecutor={(entity != null && entity.aiDomain != null ? "有效" : "缺失")}, PlayerWriter={playerWriterCount}, OptionalMotion={optionalMotionCount}";
            return valid;
        }

        private static void RunCharacterTemplateRuntimeSelfTest()
        {
            if (Application.isPlaying)
                throw new InvalidOperationException("角色模板编辑器烟雾测试只能在非 PlayMode 下运行。");

            if (!ValidateCompleteCharacterTemplate(out string staticReport))
                throw new InvalidOperationException(staticReport);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CompleteTemplatePath);
            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            StateMachine stateMachine = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException("运行态烟雾测试无法实例化完整角色模板。");

                SceneManager.MoveGameObjectToScene(instance, previewScene);
                Entity entity = instance.GetComponent<Entity>();
                Require(entity != null, "缺少根 Entity");
                entity._DoAwake();
                InitializeKccMotorForEditorSmokeTest(entity.kcc.motor);

                stateMachine = entity.stateDomain.stateMachine;
                Require(entity.Domains.Count == 4, $"Domain 注册数量错误：{entity.Domains.Count}");
                Require(ContainsDomain(entity, entity.basicDomain)
                    && ContainsDomain(entity, entity.aiDomain)
                    && ContainsDomain(entity, entity.buffDomain)
                    && ContainsDomain(entity, entity.stateDomain), "四个标准 Domain 未完整注册");
                Require(ReferenceEquals(entity.kcc.motor.CharacterController, entity),
                    "KCC CharacterController 未绑定 Entity");
                Require(stateMachine != null && stateMachine.isInitialized && stateMachine.isRunning,
                    "StateMachine 未完成初始化并启动");
                Require(stateMachine.HostEntity == entity && stateMachine.BoundAnimator == entity.animator,
                    "StateMachine 的 Entity/Animator 绑定错误");

                StateFinalIKDriver driver = entity.animator.GetComponent<StateFinalIKDriver>();
                Require(driver != null && driver.BoundEntity == entity && driver.BoundAnimator == entity.animator,
                    "StateFinalIKDriver 未绑定当前 Entity/Animator");
                Require(HasUniqueRuntimeModule<EntityBasicMoveRotateModule>(entity)
                    && entity.aiDomain != null,
                    "运行时未形成唯一基础移动入口或 AI 域执行器缺失");

                entity.SetMoveInput(new Vector3(3f, 4f, 0f));
                entity.SetLookInput(new Vector3(0f, 0f, 8f));
                entity.SetVerticalInput(2f);
                Require(entity.kcc.moveInput.sqrMagnitude <= 1.0001f
                    && Approximately(entity.kcc.lookInput, Vector3.forward)
                    && Approximately(entity.kcc.verticalInput, 1f), "输入 Clamp/Normalize 失败");

                CapsuleCollider capsule = entity.kcc.motor.Capsule;
                entity.SetCrouch(true);
                entity.BeforeCharacterUpdate(0.02f);
                Require(Approximately(capsule.height, entity.kcc.crouchedCapsuleHeight)
                    && Approximately(capsule.center.y, entity.kcc.crouchedCapsuleHeight * 0.5f),
                    "下蹲胶囊尺寸错误");
                entity.SetCrouch(false);
                entity.BeforeCharacterUpdate(0.02f);
                Require(Approximately(capsule.height, 1.8f)
                    && Approximately(capsule.height, entity.kcc.standingCapsuleHeight)
                    && Approximately(capsule.center.y, 0.9f), "恢复站立后胶囊未回到 1.8m");

                Vector3 firstTarget = new Vector3(1.25f, 0.5f, -2f);
                Quaternion firstRotation = Quaternion.Euler(0f, 35f, 0f);
                entity.kcc.QueueMatchTargetPose(firstTarget, firstRotation, releaseAfterApply: true);
                Require(entity.kcc.TryGetPendingMatchTargetPose(out Vector3 pendingPosition, out Quaternion pendingRotation)
                    && Approximately(pendingPosition, firstTarget)
                    && Approximately(pendingRotation, firstRotation), "MatchTarget 待应用位姿读取错误");
                entity.BeforeCharacterUpdate(0.02f);
                Require(Approximately(entity.kcc.motor.TransientPosition, firstTarget)
                    && Approximately(entity.kcc.motor.TransientRotation, firstRotation)
                    && !entity.kcc.HasPendingMatchTargetPose, "MatchTarget 未在 KCC 边界一次性应用");
                entity.BeforeCharacterUpdate(0.02f);
                Require(!entity.kcc.IsMatchTargetMotionLocked, "一次性 MatchTarget 在下一物理 Tick 后仍错误锁定");

                Vector3 supersededTarget = new Vector3(-3f, 0.25f, 1f);
                Vector3 latestTarget = new Vector3(-2f, 0.75f, 4f);
                Quaternion latestRotation = Quaternion.Euler(0f, 120f, 0f);
                entity.kcc.QueueMatchTargetPose(supersededTarget, Quaternion.identity, releaseAfterApply: false);
                entity.kcc.QueueMatchTargetPose(latestTarget, latestRotation, releaseAfterApply: false);
                entity.BeforeCharacterUpdate(0.02f);
                Require(Approximately(entity.kcc.motor.TransientPosition, latestTarget)
                    && Approximately(entity.kcc.motor.TransientRotation, latestRotation)
                    && entity.kcc.IsMatchTargetMotionLocked,
                    "多 Update 合并或持续 MatchTarget 锁定语义错误");
                entity.kcc.ClearMatchTargetPose();
                entity.BeforeCharacterUpdate(0.02f);
                Require(!entity.kcc.IsMatchTargetMotionLocked && !entity.kcc.HasPendingMatchTargetPose,
                    "MatchTarget 提前退出后未完整释放");

                int domainCount = entity.Domains.Count;
                int moduleCount = entity.ModuleTables.Count;
                entity._DoAwake();
                Require(entity.Domains.Count == domainCount && entity.ModuleTables.Count == moduleCount,
                    "重复初始化产生重复 Domain 或 Module 注册");

                InvokeEntityDestroyForEditorSmokeTest(entity);
                Require(!stateMachine.isRunning && !stateMachine.isInitialized
                    && driver.BoundEntity == null && driver.BoundAnimator == null,
                    "销毁生命周期未完整停止 StateMachine 或解绑 IK Driver");

                Debug.Log("[角色模板运行态烟雾测试] 通过：四域初始化、状态机/IK绑定、输入约束、蹲起胶囊、MatchTarget多更新合并/消费/退出、重复初始化和销毁释放均符合契约。", prefab);
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
                if (previewScene.IsValid())
                    EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static bool ValidateIKBoneBinding(StateFinalIKDriver driver, Animator animator)
        {
            return driver != null && driver.MatchesHumanoidBinding(animator);
        }

        /// <summary>
        /// 模板只保留状态到 IK 的表现桥，不携带 Solver 或自动补组件行为。
        /// 正式 Variant 必须按实际能力显式挂 Solver，不能把通用模板变成无模型也有 IK 负担的角色。
        /// </summary>
        private static bool ValidateFinalIKTemplateBaseline(StateFinalIKDriver driver)
        {
            return driver != null && driver.IsSolverFreeTemplateBaseline(out _);
        }

        private static bool ValidateAllMappings(
            EntityTransformMapping mapping,
            Transform hierarchyRoot,
            bool expectDebugRoot)
        {
            if (mapping == null || mapping.defaultMap == null || mapping.dynamicMap == null)
                return false;

            for (int i = 0; i < RequiredDefaultMappings.Length; i++)
            {
                Transform value = mapping.Resolve(RequiredDefaultMappings[i]);
                if (value == null || (value != hierarchyRoot && !value.IsChildOf(hierarchyRoot)))
                    return false;
            }

            for (int i = 0; i < RequiredDynamicMappings.Length; i++)
            {
                Transform value = mapping.Resolve(RequiredDynamicMappings[i]);
                if (value == null || (value != hierarchyRoot && !value.IsChildOf(hierarchyRoot)))
                    return false;
            }

            foreach (KeyValuePair<DefaultTransformKey, Transform> pair in mapping.defaultMap)
            {
                if (pair.Value == null)
                    return false;
            }

            foreach (KeyValuePair<string, Transform> pair in mapping.dynamicMap)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                    return false;
            }

            Transform debugRoot = mapping.Resolve("DebugRoot");
            return expectDebugRoot
                ? debugRoot != null && debugRoot == hierarchyRoot.Find(DebugRootPath)
                : debugRoot == null && !mapping.dynamicMap.ContainsKey("DebugRoot");
        }

        private static bool ValidateTopLevelOrder(Transform root, string[] expectedOrder)
        {
            if (root == null || expectedOrder == null || root.childCount != expectedOrder.Length)
                return false;

            for (int i = 0; i < expectedOrder.Length; i++)
            {
                if (!string.Equals(root.GetChild(i).name, expectedOrder[i], StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static int CountMissingScriptsRecursive(Transform root)
        {
            if (root == null)
                return 0;

            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root.gameObject);
            for (int i = 0; i < root.childCount; i++)
                count += CountMissingScriptsRecursive(root.GetChild(i));
            return count;
        }

        private static bool ValidateEditorOnlyPolicy(Transform root, bool expectDebugRoot)
        {
            int editorOnlyCount = CountTaggedObjectsRecursive(root, "EditorOnly");
            Transform debugRoot = root.Find(DebugRootPath);
            return expectDebugRoot
                ? debugRoot != null && debugRoot.CompareTag("EditorOnly") && editorOnlyCount == 1
                : debugRoot == null && editorOnlyCount == 0;
        }

        private static int CountTaggedObjectsRecursive(Transform root, string tag)
        {
            if (root == null)
                return 0;

            int count = root.CompareTag(tag) ? 1 : 0;
            for (int i = 0; i < root.childCount; i++)
                count += CountTaggedObjectsRecursive(root.GetChild(i), tag);
            return count;
        }

        private static bool ValidateRuntimeGeneratedIsEmpty(Transform root)
        {
            Transform generated = root.Find("10_运行时生成_RuntimeGenerated");
            if (generated == null || generated.childCount != 2)
                return false;

            Transform temporary = generated.Find("TemporaryEffects");
            Transform attachments = generated.Find("RuntimeAttachments");
            return temporary != null && temporary.childCount == 0
                && attachments != null && attachments.childCount == 0;
        }

        private static bool ValidateRigidbodyPolicy(Transform root)
        {
            if (root == null || root.GetComponent<Rigidbody>() != null)
                return false;

            Transform ragdollRoot = root.Find("02_运动物理扩展_MotionPhysics/布娃娃预留_Ragdoll");
            if (ragdollRoot == null)
                return false;

            Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody body = rigidbodies[i];
                if (body == null || body.transform == root || !body.transform.IsChildOf(ragdollRoot) || !body.isKinematic)
                    return false;
            }
            return true;
        }

        private static bool ValidateAssetDependenciesAndBundleLabel(string path, bool expectDebugRoot)
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null || !string.IsNullOrEmpty(importer.assetBundleName))
                return false;

            if (expectDebugRoot)
                return true;

            string[] dependencies = AssetDatabase.GetDependencies(path, true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (string.Equals(dependencies[i], TemplatePath, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static bool ContainsDomain(Entity entity, IDomain expected)
        {
            for (int i = 0; i < entity.Domains.Count; i++)
            {
                if (ReferenceEquals(entity.Domains[i], expected))
                    return true;
            }
            return false;
        }

        private static void InitializeKccMotorForEditorSmokeTest(
            KinematicCharacterController.KinematicCharacterMotor motor)
        {
            Require(motor != null, "缺少 KCC Motor");
            MethodInfo awakeMethod = typeof(KinematicCharacterController.KinematicCharacterMotor).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(awakeMethod != null, "无法定位 KCC Motor.Awake，测试工具与 KCC 版本不匹配");
            awakeMethod.Invoke(motor, null);
        }

        private static void InvokeEntityDestroyForEditorSmokeTest(Entity entity)
        {
            Require(entity != null, "销毁测试缺少 Entity");
            MethodInfo destroyMethod = typeof(Entity).GetMethod(
                "OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(destroyMethod != null, "无法定位 Entity.OnDestroy，测试工具与 Entity 生命周期不匹配");
            destroyMethod.Invoke(entity, null);
        }

        private static bool HasUniqueRuntimeModule<T>(Entity entity) where T : class, IModule
        {
            if (entity == null || entity.ModuleTables == null
                || !entity.ModuleTables.TryGetValue(typeof(T), out IModule module)
                || module is not T)
                return false;

            int count = 0;
            foreach (KeyValuePair<Type, IModule> pair in entity.ModuleTables)
            {
                if (pair.Value is T)
                    count++;
            }
            return count == 1;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[角色模板运行态烟雾测试] " + message);
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= 0.000001f;
        }

        private static bool Approximately(Quaternion a, Quaternion b)
        {
            return Mathf.Abs(Quaternion.Dot(a, b)) >= 0.99999f;
        }

        private static int CountBasicModule<T>(Entity entity) where T : EntityBasicModuleBase
        {
            if (entity?.basicDomain?.MyModules?.ValuesNow == null)
                return 0;

            int count = 0;
            List<EntityBasicModuleBase> modules = entity.basicDomain.MyModules.ValuesNow;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is T)
                    count++;
            }
            return count;
        }

        private static int CountAiModule<T>(Entity entity) where T : EntityAIModuleBase
        {
            if (entity?.aiDomain?.MyModules?.ValuesNow == null)
                return 0;

            int count = 0;
            List<EntityAIModuleBase> modules = entity.aiDomain.MyModules.ValuesNow;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is T)
                    count++;
            }
            return count;
        }

        private static int CountOptionalMotionModules(Entity entity)
        {
            if (entity?.basicDomain?.MyModules?.ValuesNow == null)
                return 0;

            int count = 0;
            List<EntityBasicModuleBase> modules = entity.basicDomain.MyModules.ValuesNow;
            for (int i = 0; i < modules.Count; i++)
            {
                EntityBasicModuleBase module = modules[i];
                if (module is EntityBasicFlyModule
                    || module is EntityBasicSwimModule
                    || module is EntityBasicClimbModule
                    || module is EntityBasicMountModule
                    || module is EntityBasicRootMotionModule)
                {
                    count++;
                }
            }
            return count;
        }

        private static Transform ResolveChest(Animator animator)
        {
            Transform upperChest = GetHumanBone(animator, HumanBodyBones.UpperChest);
            if (upperChest != null)
                return upperChest;

            Transform chest = GetHumanBone(animator, HumanBodyBones.Chest);
            return chest != null ? chest : GetHumanBone(animator, HumanBodyBones.Spine);
        }

        private static Transform GetHumanBone(Animator animator, HumanBodyBones bone)
        {
            return animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
        }

        private static Transform CreateNode(Transform parent, string name, Vector3 localPosition = default)
        {
            GameObject child = new GameObject(name);
            Transform transform = child.transform;
            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            return transform;
        }

        private static void ResetLocalTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static bool HasChild(Transform root, string path)
        {
            return root != null && root.Find(path) != null;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
