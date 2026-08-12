using System;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 将 EditorTools 中的大黑塔视觉样例迁移为新版通用角色底盘上的正式玩家 Variant。
    /// 旧样例只作为视觉来源，不能作为正式内容资产。
    /// </summary>
    public static class ESFormalHertaPlayerVariantBuilder
    {
        private const float PlayerGroundMovementSharpness = 20f;
        private const float PlayerOrientationSharpness = 18f;

        public const string VariantFolder = "Assets/ESNormalAssets/CharacterVariants";
        public const string VariantPath = VariantFolder + "/大黑塔.prefab";
        public const string DataFolder = "Assets/ESNormalAssets/Data/CharacterVariants";
        public const string DefinitionPath = DataFolder + "/大黑塔_ActorData.asset";
        public const string PlayerStatePackPath = DataFolder + "/大黑塔_PlayerStatePack.asset";

        private const string LegacyPreviewPath = "Assets/ESNormalAssets/EditorTools/大黑塔.prefab";
        private const string ModelRootPath = "03_模型表现_Presentation/ModelOffset/ModelRoot";
        private const string HurtBoxRootPath = "05_检测碰撞_Detection/HurtBoxes";
        private const string WeaponSocketPath = "06_装备_Equipment/WeaponSlots/WeaponSocket";
        private const string EquipmentSocketRootPath = "06_装备_Equipment/WeaponSlots/";
        private const string EquipmentVisualsPath = "06_装备_Equipment/EquipmentVisuals";
        private const string LongBarWeaponPrefabPath = ESLongBarMeleeWeaponBuilder.WeaponPrefabPath;
        private const string LongBarWeaponKey = ESLongBarMeleeWeaponBuilder.WeaponKey;
        private const string CameraTargetPath = "08_相机参考_CameraReferences/CameraTarget";
        private const string HeadAnchorPath = "04_动画辅助_AnimationSupport/Anchors/HeadAnchor";
        private const string ChestAnchorPath = "04_动画辅助_AnimationSupport/Anchors/ChestAnchor";
        private const string HipAnchorPath = "04_动画辅助_AnimationSupport/Anchors/HipAnchor";

        private static readonly string[] VehiclePrototypePaths =
        {
            "Assets/ESNormalAssets/VehiclePrototypes/BlockCar.prefab",
            "Assets/ESNormalAssets/VehiclePrototypes/BlockBicycle.prefab",
            "Assets/ESNormalAssets/VehiclePrototypes/BlockHelicopter.prefab",
        };

        private static readonly string[] LegacyPreviewPlaceholderCubeNames =
        {
            "Cube1",
            "Cube1 (1)",
            "Cube1 (2)",
        };

        [MenuItem("【ES】/内容制作/角色模板/重建正式玩家 Variant/大黑塔（新版通用模板）", false, 120)]
        public static void RebuildHertaPlayerVariantMenu()
        {
            GameObject prefab = RebuildHertaPlayerVariant();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[角色 Variant] 已从新版通用模板重建正式玩家大黑塔：" + VariantPath, prefab);
        }

        [MenuItem("【ES】/内容制作/角色模板/升级方块载具骑乘探针", false, 121)]
        public static void UpgradeVehicleMountProbesMenu()
        {
            UpgradeVehicleMountProbes();
            Debug.Log("[载具] 已升级方块载具的 WorldDynamic 车体与 Interaction 座位探针。");
        }

        /// <summary>
        /// 命令行批处理入口；只重建正式玩家 Variant。
        /// 载具升级属于测试场景准备流程，不能由单一角色资产构建偷偷触发。
        /// </summary>
        public static void RebuildHertaPlayerVariantBatch()
        {
            RebuildHertaPlayerVariant();
        }

        public static GameObject RebuildHertaPlayerVariant()
        {
            EnsureVariantAuthoringIsSafe();
            EnsureAssetFolder(VariantFolder);
            EnsureAssetFolder(DataFolder);
            ESCameraDefaultContentBuilder.EnsureDefaultPlayerCameraContent();

            ActorDataInfo definition = EnsureHertaDefinition();
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(ESBasicCharacterTemplateBuilder.CompleteTemplatePath);
            if (template == null)
                throw new InvalidOperationException("缺少新版通用角色模板：" + ESBasicCharacterTemplateBuilder.CompleteTemplatePath);

            GameObject legacy = PrefabUtility.LoadPrefabContents(LegacyPreviewPath);
            GameObject variant = PrefabUtility.LoadPrefabContents(ESBasicCharacterTemplateBuilder.CompleteTemplatePath);
            try
            {
                variant.name = "大黑塔";
                Entity entity = variant.GetComponent<Entity>();
                EntityCharacterIdentity profile = variant.GetComponent<EntityCharacterIdentity>();
                EntityTransformMapping mapping = variant.GetComponent<EntityTransformMapping>();
                if (entity == null || profile == null || mapping == null)
                    throw new InvalidOperationException("新版通用角色模板缺少 Entity、EntityCharacterIdentity 或 EntityTransformMapping。");

                Animator animator = ReplaceTemplateVisual(variant, legacy);
                ConfigureVariantIdentity(profile, definition);
                ConfigurePlayerStatePack(entity);
                ConfigurePlayerModules(entity, mapping);
                ConfigurePlayerKcc(entity);
                ConfigureFormalHurtBox(variant.transform);
                RebuildHumanoidMappings(variant.transform, mapping, animator);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(variant, VariantPath);
                if (saved == null)
                    throw new InvalidOperationException("保存正式玩家 Variant 失败：" + VariantPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!ESCharacterTemplateReleaseGate.ValidateFormalCharacterPrefab(VariantPath, out string report))
                    throw new InvalidOperationException(report);

                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(variant);
                PrefabUtility.UnloadPrefabContents(legacy);
            }
        }

        private static void EnsureVariantAuthoringIsSafe()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Unity 正在 PlayMode 或准备切换 PlayMode，禁止重建正式玩家 Variant。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Unity 正在编译、域重载或导入资产，禁止重建正式玩家 Variant。");
        }

        private static ActorDataInfo EnsureHertaDefinition()
        {
            ActorDataInfo definition = AssetDatabase.LoadAssetAtPath<ActorDataInfo>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ActorDataInfo>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.name = "大黑塔_ActorData";
            definition.SetKey("player.herta");
            definition.actorKind = ActorDataKind.Player;
            definition.displayName = "大黑塔";
            definition.description = "新版通用角色模板生成的正式玩家定义。";
            definition.motionShared = EntityMotionSharedData.Default;
            definition.motionShared.enableClimb = true;
            definition.motionShared.enableMount = true;
            definition.motionShared.stableMovementSharpness = PlayerGroundMovementSharpness;
            definition.motionShared.orientationSharpness = PlayerOrientationSharpness;
            definition.motionVariable = EntityMotionVariableData.Default;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Animator ReplaceTemplateVisual(GameObject variant, GameObject legacy)
        {
            Entity legacyEntity = legacy.GetComponent<Entity>();
            Animator legacyAnimator = legacyEntity != null ? legacyEntity.animator : null;
            if (legacyAnimator == null)
                legacyAnimator = legacy.GetComponentInChildren<Animator>(true);
            if (legacyAnimator == null)
                throw new InvalidOperationException("旧大黑塔预览 Prefab 缺少 Animator，无法迁移视觉模型。");

            Transform modelRoot = FindRequired(variant.transform, ModelRootPath);
            for (int i = modelRoot.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(modelRoot.GetChild(i).gameObject);

            // 旧样例把 Animator 与旧 Entity 同挂在根上。克隆整个视觉层级后必须剥离
            // 旧的运行时底盘，不能把旧 Entity、KCC 或模块带入正式 Variant。
            GameObject visual = UnityEngine.Object.Instantiate(legacyAnimator.gameObject);
            StripLegacyNonPresentationComponents(visual);
            DisableLegacyPreviewPlaceholderCubes(visual);
            if (visual.GetComponentInChildren<Entity>(true) != null)
            {
                UnityEngine.Object.DestroyImmediate(visual);
                throw new InvalidOperationException("旧大黑塔 Animator 层级包含 Entity，不能作为纯视觉模型迁移。");
            }

            visual.name = "大黑塔_Model";
            visual.transform.SetParent(modelRoot, false);
            visual.transform.localPosition = legacy.transform.InverseTransformPoint(legacyAnimator.transform.position);
            visual.transform.localRotation = Quaternion.Inverse(legacy.transform.rotation) * legacyAnimator.transform.rotation;
            visual.transform.localScale = legacyAnimator.transform.lossyScale;

            Animator animator = visual.GetComponent<Animator>();
            if (animator == null)
                animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null)
                throw new InvalidOperationException("迁移后的大黑塔视觉模型缺少 Animator。");

            // 通用模板允许带一个无模型的占位 Animator 以便制作期查看结构。Variant 迁入
            // 真实表现树后必须移除它及任何附带表现 Animator，正式角色只保留迁入模型的唯一 Animator。
            Animator[] animators = variant.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null && animators[i] != animator)
                    UnityEngine.Object.DestroyImmediate(animators[i]);
            }

            animators = variant.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1 || animators[0] != animator)
                throw new InvalidOperationException("正式玩家 Variant 必须且只能保留迁入模型的唯一 Animator。");

            StateFinalIKDriver driver = animator.GetComponent<StateFinalIKDriver>();
            if (driver == null)
                driver = animator.gameObject.AddComponent<StateFinalIKDriver>();

            StateFinalIKDriver[] drivers = variant.GetComponentsInChildren<StateFinalIKDriver>(true);
            for (int i = 0; i < drivers.Length; i++)
            {
                if (drivers[i] != null && drivers[i] != driver)
                    UnityEngine.Object.DestroyImmediate(drivers[i]);
            }

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (!driver.ConfigureHumanoidBinding(animator))
                throw new InvalidOperationException("大黑塔视觉模型必须使用有效的 Humanoid Animator，无法生成统一骨骼绑定。");
            driver.ConfigureSolverFreeTemplateBaseline();

            Entity entity = variant.GetComponent<Entity>();
            entity.animator = animator;
            return animator;
        }

        private static void DisableLegacyPreviewPlaceholderCubes(GameObject visual)
        {
            if (visual == null)
                throw new ArgumentNullException(nameof(visual));

            for (int i = 0; i < LegacyPreviewPlaceholderCubeNames.Length; i++)
            {
                Transform placeholder = visual.transform.Find(LegacyPreviewPlaceholderCubeNames[i]);
                if (placeholder == null)
                    continue;

                if (placeholder.GetComponent<MeshFilter>() == null || placeholder.GetComponent<MeshRenderer>() == null)
                {
                    throw new InvalidOperationException(
                        "旧预览占位对象名称与真实模型冲突，拒绝自动禁用：" + placeholder.name);
                }

                placeholder.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 旧预览 Prefab 不是模型资源，而是完整的角色试验场。迁移时仅保留可渲染、
        /// 动画和纯表现组件，任何脚本、物理或导航组件都不能进入正式角色表现树。
        /// </summary>
        private static void StripLegacyNonPresentationComponents(GameObject visual)
        {
            Transform[] transforms = visual.GetComponentsInChildren<Transform>(true);
            // KinematicCharacterMotor requires its CapsuleCollider. Remove controller scripts
            // before colliders so a legacy test character cannot leave dependent physics behind.
            for (int phase = 0; phase < 2; phase++)
            {
                bool removeColliders = phase == 1;
                for (int i = 0; i < transforms.Length; i++)
                {
                    Component[] components = transforms[i].GetComponents<Component>();
                    for (int j = 0; j < components.Length; j++)
                    {
                        Component component = components[j];
                        if (component == null || IsPresentationComponent(component))
                            continue;

                        if ((component is Collider) != removeColliders)
                            continue;

                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }
            }
        }

        private static bool IsPresentationComponent(Component component)
        {
            return component is Transform
                   || component is Animator
                   || component is Renderer
                   || component is MeshFilter
                   || component is ParticleSystem
                   || component is AudioSource
                   || component is Light
                   || component is LODGroup;
        }

        private static void ConfigureVariantIdentity(EntityCharacterIdentity profile, ActorDataInfo definition)
        {
            profile.prefabRole = EntityCharacterPrefabRole.CharacterVariant;
            profile.faction = EntityCharacterFaction.Player;
            profile.definitionSource = EntityCharacterDefinitionSource.Actor;
            profile.actorDefinition = definition;
            profile.monsterDefinition = null;
            profile.npcDefinition = null;
            profile.defaultCameraDefinition = ESCameraDefaultContentBuilder.PlayerThirdPersonDefinition;
            profile.defaultCameraViewKey = ESCameraViewId.Main.Key;
            profile.defaultCameraPriority = 0;
        }

        /// <summary>
        /// 正式玩家不得反向修改通用底盘共用的状态包。这里复制模板状态包，随后将骑乘的
        /// 环境切换契约固化到 Variant 自己的内容资产：非 Mounted 状态会在骑乘时退出，
        /// 也不能在骑乘环境中被业务代码直接重新激活。
        /// </summary>
        private static void ConfigurePlayerStatePack(Entity entity)
        {
            if (entity == null || entity.stateDomain == null || entity.stateDomain.stateAniDataPack == null)
                throw new InvalidOperationException("新版通用角色模板缺少默认动画状态包，无法生成正式玩家状态配置。");

            StateAniDataPack source = entity.stateDomain.stateAniDataPack;
            StateAniDataPack playerPack = AssetDatabase.LoadAssetAtPath<StateAniDataPack>(PlayerStatePackPath);
            if (playerPack == null)
            {
                string sourcePath = AssetDatabase.GetAssetPath(source);
                if (string.IsNullOrWhiteSpace(sourcePath)
                    || !AssetDatabase.CopyAsset(sourcePath, PlayerStatePackPath))
                {
                    throw new InvalidOperationException(
                        "无法复制正式玩家专用状态包。源=" + sourcePath + "，目标=" + PlayerStatePackPath);
                }

                playerPack = AssetDatabase.LoadAssetAtPath<StateAniDataPack>(PlayerStatePackPath);
                if (playerPack == null)
                    throw new InvalidOperationException("复制后的正式玩家状态包无法读取：" + PlayerStatePackPath);
            }

            if (playerPack.Infos == null || playerPack.Infos.Count == 0)
                throw new InvalidOperationException("正式玩家状态包为空，无法建立骑乘环境门禁：" + PlayerStatePackPath);

            foreach (var pair in playerPack.Infos)
            {
                StateBasicConfig config = pair.Value != null && pair.Value.sharedData != null
                    ? pair.Value.sharedData.basicConfig
                    : null;
                if (config == null)
                    throw new InvalidOperationException("正式玩家状态包存在缺少 StateBasicConfig 的状态：" + pair.Key);

                if (string.Equals(config.stateName, "骑乘", StringComparison.Ordinal))
                {
                    config.stateSupportFlag = StateSupportFlags.Mounted;
                    config.ignoreSupportFlag = false;
                    config.resetSupportFlagOnEnter = true;
                    config.deactivateOnSupportFlagSwitching = true;
                    continue;
                }

                // 显式声明可在 Mounted 中运行的状态（例如车载瞄准）由内容制作方保留。
                if ((config.stateSupportFlag & StateSupportFlags.Mounted) != 0)
                    continue;

                if (config.stateSupportFlag == StateSupportFlags.None)
                {
                    throw new InvalidOperationException(
                        "正式玩家状态 '" + config.stateName + "' 未声明环境；请先在通用状态包中指定环境，"
                        + "或显式声明其支持 Mounted。状态键=" + pair.Key);
                }

                config.ignoreSupportFlag = false;
                config.disableActiveOnSupportFlagSwitching = true;
                config.deactivateOnSupportFlagSwitching = true;
            }

            entity.stateDomain.stateAniDataPack = playerPack;
            EditorUtility.SetDirty(playerPack);
        }

        private static void ConfigurePlayerModules(Entity entity, EntityTransformMapping mapping)
        {
            entity.EnsureEntityStructure();

            entity.aiDomain.turnMode = TurnMode.MoveDirection;
            entity.aiDomain.enableCameraLook = true;
            entity.aiDomain.driveAimIK = false;
            entity.aiDomain.aimTransform = mapping.Resolve("CameraAimTarget");
            entity.aiDomain.debugCamera = false;

            EntityPlayerInputWriteModule input = GetOrAddAiModule(entity, () => new EntityPlayerInputWriteModule());
            input.enablePlayerInput = true;
            input.claimLocalControl = true;

            EntityBasicMountModule mount = GetOrAddBasicModule(entity, () => new EntityBasicMountModule());
            mount.enableMount = true;
            mount.debugMount = false;
            mount.rayOrigin = mapping.Resolve("InteractionProbe");
            mount.mountDistance = 2.25f;
            mount.mountLayerMask = ESPhysicsLayers.MountProbeMask;
            mount.mountQuery = QueryTriggerInteraction.Collide;
            mount.Mount_StateName = "骑乘";

            EntityBasicClimbModule climb = GetOrAddBasicModule(entity, () => new EntityBasicClimbModule());
            climb.enableClimb = true;
            climb.debugClimb_ = false;
            climb.climbableLayerMask = ESPhysicsLayers.ClimbProbeMask;
            climb.ceilingCheckLayerMask = ESPhysicsLayers.WorldBlockerMask;
            climb.Climb_StateName = "攀爬";
            climb.ClimbOver_StateName = "攀爬翻上";
            climb.Vault_StateName = "翻越";
            climb.VaultHigh_StateName = string.Empty;
            climb.ClimbJump_StateName = "攀爬跳跃";

            EntityBasicCombatModule combat = GetOrAddBasicModule(entity, () => new EntityBasicCombatModule());
            EntityEquipmentAttachmentModule attachment = GetOrAddBasicModule(
                entity,
                () => new EntityEquipmentAttachmentModule());
            attachment.allowEntityRootFallback = false;
            ConfigureLongBarMeleeWeapon(combat, mapping);

            entity.basicDomain.MyModules.ApplyBuffers(true);
            entity.aiDomain.MyModules.ApplyBuffers(true);
        }

        private static void ConfigurePlayerKcc(Entity entity)
        {
            entity.kcc.maxStableMoveSpeed = 8f;
            entity.kcc.stableMovementSharpness = PlayerGroundMovementSharpness;
            entity.kcc.maxAirMoveSpeed = 8f;
            entity.kcc.airAccelerationSpeed = 5f;
            entity.kcc.jumpSpeed = 8f;
            entity.kcc.orientationSharpness = PlayerOrientationSharpness;
            entity.kcc.debugMonitor = false;

        }

        private static void ConfigureLongBarMeleeWeapon(EntityBasicCombatModule combat, EntityTransformMapping mapping)
        {
            if (combat == null || mapping == null)
                throw new InvalidOperationException("大长条近战切片缺少 Combat Module 或 TransformMapping。");

            GameObject weaponPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LongBarWeaponPrefabPath);
            if (weaponPrefab == null)
                throw new InvalidOperationException("缺少大长条武器 Prefab：" + LongBarWeaponPrefabPath);
            ESLongBarMeleeWeaponBuilder.ValidateLongBarPrefabForAuthoring(weaponPrefab);

            Transform equipmentVisuals = mapping.Resolve(EquipmentVisualsPath.Substring(EquipmentVisualsPath.LastIndexOf('/') + 1));
            if (equipmentVisuals == null)
                equipmentVisuals = FindRequired(mapping.transform, EquipmentVisualsPath);

            combat.weaponSlots ??= new System.Collections.Generic.List<EntityBasicCombatModule.WeaponSlot>();
            int longBarSlotIndex = -1;
            for (int i = 0; i < combat.weaponSlots.Count; i++)
            {
                EntityBasicCombatModule.WeaponSlot candidate = combat.weaponSlots[i];
                if (candidate == null
                    || candidate.weaponKey == null
                    || !string.Equals(candidate.weaponKey.StringKey, LongBarWeaponKey, StringComparison.Ordinal))
                    continue;

                if (longBarSlotIndex >= 0)
                    throw new InvalidOperationException("正式玩家 Variant 中出现重复的大长条 Weapon Key：" + LongBarWeaponKey);

                longBarSlotIndex = i;
            }

            Transform old = longBarSlotIndex >= 0 ? combat.weaponSlots[longBarSlotIndex].weaponRoot : null;
            if (old != null)
            {
                if (old.parent != equipmentVisuals)
                    throw new InvalidOperationException("大长条槽位指向 EquipmentVisuals 之外的对象，拒绝跨层级删除：" + old.name);

                for (int i = 0; i < combat.weaponSlots.Count; i++)
                {
                    if (i != longBarSlotIndex && combat.weaponSlots[i] != null && combat.weaponSlots[i].weaponRoot == old)
                        throw new InvalidOperationException("大长条与其他 Weapon Slot 共享同一个 weaponRoot，拒绝删除共享对象：" + old.name);
                }

                UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            else
            {
                Transform nameCollision = equipmentVisuals.Find("大长条_WeaponSlot");
                if (nameCollision != null)
                    throw new InvalidOperationException("EquipmentVisuals 已有同名对象但没有稳定 Weapon Key 所有权，拒绝覆盖：" + nameCollision.name);
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(weaponPrefab, equipmentVisuals) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("实例化大长条武器 Prefab 失败：" + LongBarWeaponPrefabPath);

            instance.name = "大长条_WeaponSlot";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            combat.enableWeaponFusion = true;
            // Combat 保留通用武器能力；具体是否射击由当前 WeaponDefinition.fire.enabled 决定。
            // 大长条自身关闭 fire，因此不会走 Hitscan，同时未来枪械槽位无需改角色结构。
            combat.enableGunFire = true;
            combat.fireOnAttackInput = true;
            combat.autoAddWeaponBindingIfMissing = false;
            combat.startWithWeaponInHand = false;

            var longBarSlot = new EntityBasicCombatModule.WeaponSlot
            {
                displayName = "大长条",
                weaponKey = new ESWeaponConfigKey { stringKey = LongBarWeaponKey },
                weaponRoot = instance.transform
            };

            if (longBarSlotIndex >= 0)
                combat.weaponSlots[longBarSlotIndex] = longBarSlot;
            else
            {
                longBarSlotIndex = combat.weaponSlots.Count;
                combat.weaponSlots.Add(longBarSlot);
            }

            combat.startWeaponIndex = longBarSlotIndex;
        }

        private static void ConfigureFormalHurtBox(Transform root)
        {
            Transform hurtBoxRoot = FindRequired(root, HurtBoxRootPath);
            Transform hurtBox = hurtBoxRoot.Find("MainHurtBox");
            if (hurtBox == null)
            {
                var hurtBoxObject = new GameObject("MainHurtBox");
                hurtBox = hurtBoxObject.transform;
                hurtBox.SetParent(hurtBoxRoot, false);
            }

            hurtBox.gameObject.layer = ESPhysicsLayers.EntityHurtbox;
            CapsuleCollider collider = hurtBox.GetComponent<CapsuleCollider>();
            if (collider == null)
                collider = hurtBox.gameObject.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.direction = 1;
            collider.radius = 0.34f;
            collider.height = 1.7f;
            collider.center = new Vector3(0f, 0.85f, 0f);
        }

        private static void RebuildHumanoidMappings(Transform root, EntityTransformMapping mapping, Animator animator)
        {
            Transform head = GetHumanBone(animator, HumanBodyBones.Head) ?? FindRequired(root, HeadAnchorPath);
            Transform chest = GetHumanBone(animator, HumanBodyBones.Chest)
                              ?? GetHumanBone(animator, HumanBodyBones.Spine)
                              ?? FindRequired(root, ChestAnchorPath);
            Transform hip = GetHumanBone(animator, HumanBodyBones.Hips) ?? FindRequired(root, HipAnchorPath);
            Transform weaponSocket = FindRequired(root, WeaponSocketPath);

            mapping.Set(DefaultTransformKey.Root, root);
            mapping.Set(DefaultTransformKey.Head, head);
            mapping.Set(DefaultTransformKey.Chest, chest);
            mapping.Set(DefaultTransformKey.Hip, hip);
            mapping.Set(DefaultTransformKey.LeftHand, GetHumanBone(animator, HumanBodyBones.LeftHand));
            mapping.Set(DefaultTransformKey.RightHand, GetHumanBone(animator, HumanBodyBones.RightHand));
            mapping.Set(DefaultTransformKey.LeftFoot, GetHumanBone(animator, HumanBodyBones.LeftFoot));
            mapping.Set(DefaultTransformKey.RightFoot, GetHumanBone(animator, HumanBodyBones.RightFoot));
            if (!mapping.Set(DefaultTransformKey.Weapon, EntityEquipmentSocketKeys.WeaponSocket, weaponSocket, out EntityTransformMap.Conflict weaponConflict))
                throw new InvalidOperationException("正式角色武器挂点映射失败：" + weaponConflict.Message);
            SetRequiredStringMapping(mapping, root, EntityEquipmentSocketKeys.MainHandSocket);
            SetRequiredStringMapping(mapping, root, EntityEquipmentSocketKeys.OffHandSocket);
            SetRequiredStringMapping(mapping, root, EntityEquipmentSocketKeys.PrimaryBackSocket);
            SetRequiredStringMapping(mapping, root, EntityEquipmentSocketKeys.SecondaryBackSocket);
            SetRequiredStringMapping(mapping, root, EntityEquipmentSocketKeys.HipSocket);
            SetRequiredStringMapping(mapping, root, EntityEquipmentSocketKeys.TemporaryHandSocket);

            Transform cameraTarget = FindRequired(root, CameraTargetPath);
            if (!mapping.Set(DefaultTransformKey.Camera, "CameraTarget", cameraTarget, out EntityTransformMap.Conflict cameraConflict))
                throw new InvalidOperationException("正式角色相机挂点映射失败：" + cameraConflict.Message);

            mapping.RebuildRuntimeCache();
        }

        private static void SetRequiredStringMapping(
            EntityTransformMapping mapping,
            Transform root,
            string key)
        {
            Transform socket = FindRequired(root, EquipmentSocketRootPath + key);
            if (!mapping.Set(key, socket, out EntityTransformMap.Conflict conflict))
                throw new InvalidOperationException("正式角色装备挂点映射失败（" + key + "）：" + conflict.Message);
        }

        private static T GetOrAddBasicModule<T>(Entity entity, Func<T> create) where T : EntityBasicModuleBase
        {
            T module = FindBasicModule<T>(entity, out int count);
            if (count > 1)
                throw new InvalidOperationException("新版通用角色底盘出现重复基础模块：" + typeof(T).Name);
            if (module != null)
                return module;

            module = create();
            entity.basicDomain.MyModules.Add(module);
            return module;
        }

        private static T GetOrAddAiModule<T>(Entity entity, Func<T> create) where T : EntityAIModuleBase
        {
            T module = FindAiModule<T>(entity, out int count);
            if (count > 1)
                throw new InvalidOperationException("新版通用角色底盘出现重复 AI 模块：" + typeof(T).Name);
            if (module != null)
                return module;

            module = create();
            entity.aiDomain.MyModules.Add(module);
            return module;
        }

        private static T FindBasicModule<T>(Entity entity, out int count) where T : EntityBasicModuleBase
        {
            count = 0;
            T result = null;
            if (entity?.basicDomain?.MyModules?.ValuesNow == null)
                return null;

            for (int i = 0; i < entity.basicDomain.MyModules.ValuesNow.Count; i++)
            {
                if (entity.basicDomain.MyModules.ValuesNow[i] is T module)
                {
                    count++;
                    result = module;
                }
            }
            return result;
        }

        private static T FindAiModule<T>(Entity entity, out int count) where T : EntityAIModuleBase
        {
            count = 0;
            T result = null;
            if (entity?.aiDomain?.MyModules?.ValuesNow == null)
                return null;

            for (int i = 0; i < entity.aiDomain.MyModules.ValuesNow.Count; i++)
            {
                if (entity.aiDomain.MyModules.ValuesNow[i] is T module)
                {
                    count++;
                    result = module;
                }
            }
            return result;
        }

        private static Transform FindRequired(Transform root, string path)
        {
            Transform result = root.Find(path);
            if (result == null)
                throw new InvalidOperationException("新版通用角色模板缺少固定节点：" + path);
            return result;
        }

        private static Transform GetHumanBone(Animator animator, HumanBodyBones bone)
        {
            if (animator == null || animator.avatar == null || !animator.avatar.isValid)
                return null;
            return animator.GetBoneTransform(bone);
        }

        /// <summary>
        /// 将方块载具原型升级到骑乘查询约定。
        /// 这是显式资产迁移 API，供菜单和“一键准备测试场景”流程调用。
        /// </summary>
        public static void UpgradeVehicleMountProbes()
        {
            ESCameraDefaultContentBuilder.EnsureDefaultPlayerCameraContent();
            for (int i = 0; i < VehiclePrototypePaths.Length; i++)
                UpgradeVehicleMountProbe(VehiclePrototypePaths[i]);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void UpgradeVehicleMountProbe(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                EntityMountable mountable = root.GetComponent<EntityMountable>();
                Collider bodyCollider = root.GetComponent<Collider>();
                VehicleController controller = root.GetComponent<VehicleController>();
                if (mountable == null || mountable.matchPoint == null || bodyCollider == null || controller == null)
                    throw new InvalidOperationException("载具缺少 EntityMountable、VehicleController、DriverSeat 或根 Collider：" + path);

                root.layer = ESPhysicsLayers.WorldDynamic;
                bodyCollider.isTrigger = false;
                controller.driverCameraDefinition = ESCameraDefaultContentBuilder.VehicleChaseDefinition;
                controller.driverCameraViewKey = ESCameraViewId.Main.Key;
                controller.driverCameraPriority = 10;
                controller.driverCameraFollow = root.transform;
                controller.driverCameraLookAt = null;

                Transform seat = mountable.matchPoint;
                Transform probe = seat.Find("MountInteractionProbe");
                if (probe == null)
                {
                    var probeObject = new GameObject("MountInteractionProbe");
                    probe = probeObject.transform;
                    probe.SetParent(seat, false);
                }

                probe.gameObject.layer = ESPhysicsLayers.Interaction;
                SphereCollider collider = probe.GetComponent<SphereCollider>();
                if (collider == null)
                    collider = probe.gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 0.65f;
                collider.center = Vector3.zero;

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] segments = folder.Split('/');
            if (segments.Length == 0 || !string.Equals(segments[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("只能在 Assets 下创建角色内容目录：" + folder);

            string current = "Assets";
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }
    }
}
