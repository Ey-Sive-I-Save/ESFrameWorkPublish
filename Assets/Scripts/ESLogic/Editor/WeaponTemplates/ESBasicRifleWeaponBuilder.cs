using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>Creates the baseline ranged weapon content used to validate the Weapon -> Shot pipeline.</summary>
    public static class ESBasicRifleWeaponBuilder
    {
        public const string WeaponItemKey = "item.weapon.ranged.basic_rifle";
        public const string WeaponKey = "weapon.ranged.basic_rifle";
        public const string WeaponPrefabAssetKey = "prefab.weapon.ranged.basic_rifle";
        public const string ShotItemKey = "item.shot.basic_rifle.round";
        public const string ShotKey = "shot.basic_rifle.round";
        public const string ShotPrefabAssetKey = "prefab.shot.basic_rifle.round";
        public const string WeaponPrefabPath = "Assets/ESNormalAssets/WeaponPrototypes/基础步枪.prefab";
        public const string ShotPrefabPath = "Assets/ESNormalAssets/ProjectilePrototypes/基础步枪弹丸.prefab";
        public const string WeaponInfoPath = "Assets/ESNormalAssets/Data/Group/Item/基础步枪_ItemDataInfo.asset";
        public const string ShotInfoPath = "Assets/ESNormalAssets/Data/Group/Item/基础步枪弹丸_ItemDataInfo.asset";
        public const string AssetLibraryPath = "Assets/ESNormalAssets/Data/AssetLibrary/基本库.asset";

        private const string WeaponBindingScriptPath =
            "Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityWeaponBinding.cs";
        [MenuItem("【ES】/内容制作/武器/创建远程武器/基础步枪与弹丸", false, 142)]
        public static void CreateBasicRifleMenu()
        {
            GameObject weaponPrefab = CreateBasicRifleContent();
            Selection.activeObject = weaponPrefab;
            EditorGUIUtility.PingObject(weaponPrefab);
            Debug.Log("[ES Weapon] 已创建或验证基础步枪与弹丸 Definition/Prefab：" + WeaponKey, weaponPrefab);
        }

        public static GameObject CreateBasicRifleContent()
        {
            var shotRequest = new ESItemPrefabAuthoringRequest
            {
                label = "基础步枪弹丸",
                definitionPath = ShotInfoPath,
                prefabPath = ShotPrefabPath,
                prefabAssetKey = ShotPrefabAssetKey,
                configureNewDefinition = ConfigureShotDefinition,
                validateDefinitionOwnership = ValidateShotDefinitionOwnership,
                validateDefinitionBeforePrefab = ValidateShotDefinitionBeforePrefab,
                validateDefinition = ValidateShotDefinition,
                buildNewPrefab = BuildShotRoot,
                validatePrefab = ValidateShotPrefab
            };
            var weaponRequest = new ESItemPrefabAuthoringRequest
            {
                label = "基础步枪",
                definitionPath = WeaponInfoPath,
                prefabPath = WeaponPrefabPath,
                prefabAssetKey = WeaponPrefabAssetKey,
                configureNewDefinition = ConfigureWeaponDefinition,
                validateDefinitionOwnership = ValidateWeaponDefinitionOwnership,
                validateDefinitionBeforePrefab = ValidateWeaponDefinitionBeforePrefab,
                validateDefinition = ValidateWeaponDefinition,
                buildNewPrefab = BuildWeaponRoot,
                validatePrefab = ValidateWeaponPrefab
            };

            ESItemPrefabAuthoringResult[] results = ESItemPrefabAuthoring.CreateOrValidate(
                AssetLibraryPath,
                shotRequest,
                weaponRequest);
            return results[1].prefab;
        }

        private static void ConfigureShotDefinition(ItemDataInfo info)
        {
            info.name = "基础步枪弹丸_ItemDataInfo";
            info.SetKey(ShotKey);
            info.itemKey = new ESItemConfigKey { stringKey = ShotItemKey };
            info.baseConfig = new ItemBaseConfig
            {
                kind = ItemKind.Shot,
                displayName = "基础步枪弹丸",
                prefabKey = new ESAssetReferPrefabConfigKey(),
                iconKey = new ESAssetReferSpriteConfigKey()
            };
            info.interactConfig = new ItemInteractConfig();
            info.logicConfig = new ItemLogicConfig();
            info.moveConfig = new ItemMoveConfig();

            ItemShotSharedData shared = ItemShotSharedData.Default;
            shared.enabled = true;
            shared.aimMode = ShotAimMode.Free;
            shared.blockMode = ShotBlockMode.AnyBlocker;
            shared.launchDelay = 0f;
            shared.warmupTime = 0f;
            shared.speed = 80f;
            shared.acceleration = 0f;
            shared.maxSpeed = 80f;
            shared.trackingStartTime = 0f;
            shared.trackingDuration = 0f;
            shared.turnSpeed = 720f;
            shared.lifeTime = 2.5f;
            shared.radius = 0.04f;
            shared.hitLayers = ESPhysicsLayers.ShotHitMask;
            shared.useGravity = false;
            shared.orientToVelocity = true;
            shared.allowMustHit = false;

            info.kindData = new ItemShotDataBlock
            {
                key = new ESShotConfigKey { stringKey = ShotKey },
                sharedData = shared,
                initialState = ItemShotVariableData.Default
            };
        }

        private static void ConfigureWeaponDefinition(ItemDataInfo info)
        {
            info.name = "基础步枪_ItemDataInfo";
            info.SetKey(WeaponKey);
            info.itemKey = new ESItemConfigKey { stringKey = WeaponItemKey };
            info.baseConfig = new ItemBaseConfig
            {
                kind = ItemKind.Weapon,
                displayName = "基础步枪",
                prefabKey = new ESAssetReferPrefabConfigKey(),
                iconKey = new ESAssetReferSpriteConfigKey()
            };
            info.interactConfig = new ItemInteractConfig();
            info.logicConfig = new ItemLogicConfig();
            info.moveConfig = new ItemMoveConfig();

            ItemWeaponSharedData shared = ItemWeaponSharedData.Default;
            shared.weaponKind = ItemWeaponKind.Ranged;
            shared.deliveryMode = WeaponAttackDeliveryMode.Shot;
            shared.firePolicy = WeaponFirePolicy.Automatic;
            shared.primaryAttackAction = new ESActionConfigKey();
            shared.defaultShot = new ESShotConfigKey { stringKey = ShotKey };
            shared.hitRadius = 0.04f;
            shared.cooldown = 0.1f;
            shared.fire.enabled = true;
            shared.fire.interval = 0.1f;
            shared.fire.distance = 150f;
            shared.fire.hitMask = ESPhysicsLayers.ShotHitMask;
            shared.fire.triggerInteraction = QueryTriggerInteraction.Ignore;
            shared.fire.requiresAiming = false;
            shared.fire.ammoCost = 0;
            shared.fire.durabilityCost = 0f;
            shared.fire.heatPerUse = 0f;
            shared.fire.maxHeat = 0f;
            shared.fire.heatDissipationPerSecond = 0f;
            shared.recoil.enabled = true;
            shared.recoil.baseMagnitude = 0.65f;
            shared.recoil.onlyWhenAiming = false;

            ItemWeaponVariableData initialState = ItemWeaponVariableData.Default;
            initialState.durability = 1f;
            initialState.ammo = 0;
            info.kindData = new ItemWeaponDataBlock
            {
                key = new ESWeaponConfigKey { stringKey = WeaponKey },
                sharedData = shared,
                initialState = initialState
            };
        }

        private static void ValidateShotDefinitionOwnership(ItemDataInfo info)
        {
            if (info == null
                || !string.Equals(info.KeyName, ShotKey, System.StringComparison.Ordinal)
                || info.baseConfig == null
                || info.baseConfig.kind != ItemKind.Shot
                || info.itemKey == null
                || !string.Equals(info.itemKey.StringKey, ShotItemKey, System.StringComparison.Ordinal)
                || !(info.kindData is ItemShotDataBlock shot)
                || shot.key == null
                || !string.Equals(shot.key.StringKey, ShotKey, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("基础步枪弹丸缺少匹配的 Item/Shot 稳定身份。");
            }
        }

        private static void ValidateShotDefinitionBeforePrefab(ItemDataInfo info)
        {
            ValidateShotDefinitionOwnership(info);
            ItemShotDataBlock shot = (ItemShotDataBlock)info.kindData;
            string definitionError = "缺少 SharedData。";
            if (shot.sharedData == null || !shot.sharedData.ValidateDefinition(out definitionError))
                throw new System.InvalidOperationException("基础步枪弹丸 ShotDefinition 无效：" + definitionError);
            if (!shot.initialState.ValidateDefinition(out string stateError))
                throw new System.InvalidOperationException("基础步枪弹丸 ShotVariable 无效：" + stateError);
            if (shot.initialState.forceMustHit && !shot.sharedData.allowMustHit)
                throw new System.InvalidOperationException("基础步枪弹丸初始状态要求必中，但 SharedData 禁止必中。");
        }

        private static void ValidateShotDefinition(ItemDataInfo info)
        {
            ValidateShotDefinitionBeforePrefab(info);
            ESItemDataValidationCode code = info.ValidateConfiguration();
            if (code != ESItemDataValidationCode.Valid)
                throw new System.InvalidOperationException("基础步枪弹丸配置无效：" + info.GetValidationMessage(code));
        }

        private static void ValidateWeaponDefinitionOwnership(ItemDataInfo info)
        {
            if (info == null
                || !string.Equals(info.KeyName, WeaponKey, System.StringComparison.Ordinal)
                || info.baseConfig == null
                || info.baseConfig.kind != ItemKind.Weapon
                || info.itemKey == null
                || !string.Equals(info.itemKey.StringKey, WeaponItemKey, System.StringComparison.Ordinal)
                || !(info.kindData is ItemWeaponDataBlock weapon)
                || weapon.key == null
                || !string.Equals(weapon.key.StringKey, WeaponKey, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("基础步枪缺少匹配的 Item/Weapon 稳定身份。");
            }
        }

        private static void ValidateWeaponDefinitionBeforePrefab(ItemDataInfo info)
        {
            ValidateWeaponDefinitionOwnership(info);
            ItemWeaponDataBlock weapon = (ItemWeaponDataBlock)info.kindData;
            ItemWeaponSharedData shared = weapon.sharedData;
            string definitionError = "缺少 SharedData。";
            if (shared == null || !shared.ValidateDefinition(out definitionError))
                throw new System.InvalidOperationException("基础步枪 WeaponDefinition 无效：" + definitionError);
            if (shared.weaponKind != ItemWeaponKind.Ranged
                || shared.deliveryMode != WeaponAttackDeliveryMode.Shot
                || shared.firePolicy != WeaponFirePolicy.Automatic
                || shared.defaultShot == null
                || !string.Equals(shared.defaultShot.StringKey, ShotKey, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("基础步枪必须使用 Automatic Shot 路由并绑定默认弹丸。");
            }
            if (!shared.ValidateInitialState(weapon.initialState, out string stateError))
                throw new System.InvalidOperationException("基础步枪 WeaponVariable 无效：" + stateError);
        }

        private static void ValidateWeaponDefinition(ItemDataInfo info)
        {
            ValidateWeaponDefinitionBeforePrefab(info);
            ESItemDataValidationCode code = info.ValidateConfiguration();
            if (code != ESItemDataValidationCode.Valid)
                throw new System.InvalidOperationException("基础步枪配置无效：" + info.GetValidationMessage(code));
        }

        private static GameObject BuildShotRoot(ItemDataInfo info)
        {
            GameObject root = new GameObject("基础步枪弹丸");
            try
            {
                Item item = root.AddComponent<Item>();
                item.prefabDefinition = info;
                item.basicDomain._Editor_RegisterAllButOnlyCreateRelationship(item);
                item.RegisterDomain(item.basicDomain);
                ItemShotModule shot = item.GetMoudle<ItemShotModule>();
                item.basicDomain.MyModules.ApplyBuffers(true);
                if (shot == null || item.basicDomain.FindMyModule<ItemShotModule>() == null)
                    throw new System.InvalidOperationException("无法为基础步枪弹丸创建 ItemShotModule。");

                GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                visual.name = "弹丸模型";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localScale = Vector3.one * 0.08f;
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static GameObject BuildWeaponRoot(ItemDataInfo info)
        {
            GameObject root = new GameObject("基础步枪");
            try
            {
                Item item = root.AddComponent<Item>();
                item.prefabDefinition = info;
                ESWeaponSceneTemplate template = root.AddComponent<ESWeaponSceneTemplate>();
                EntityWeaponBinding binding = AddWeaponBinding(root);

            Transform runtimeRoot = CreateChild(root.transform, ESWeaponSceneTemplate.RuntimeRootName);
            Transform mountRoot = CreateChild(root.transform, ESWeaponSceneTemplate.MountRootName);
            Transform ballisticRoot = CreateChild(root.transform, ESWeaponSceneTemplate.BallisticRootName);
            Transform presentationRoot = CreateChild(root.transform, ESWeaponSceneTemplate.PresentationRootName);
            Transform debugRoot = CreateChild(root.transform, ESWeaponSceneTemplate.DebugRootName);

            Transform rightHandGrip = CreateChild(mountRoot, "RightHandGrip", new Vector3(0f, -0.03f, -0.18f));
            Transform leftHandGrip = CreateChild(mountRoot, "LeftHandGrip", new Vector3(0f, -0.03f, 0.32f));
            Transform aimReference = CreateChild(mountRoot, "AimReference", new Vector3(0f, 0.06f, 0.85f));
            Transform recoilPivot = CreateChild(mountRoot, "RecoilPivot", new Vector3(0f, 0f, 0.1f));
            Transform muzzle = CreateChild(ballisticRoot, "Muzzle", new Vector3(0f, 0.02f, 1.05f));
            Transform rayOrigin = CreateChild(ballisticRoot, "RayOrigin", new Vector3(0f, 0.04f, 0.35f));
            Transform shotSpawn = CreateChild(ballisticRoot, "ShotSpawn", new Vector3(0f, 0.02f, 1.05f));
            Transform shellEject = CreateChild(ballisticRoot, "ShellEject", new Vector3(0.12f, 0.08f, 0.2f));
            Transform magazine = CreateChild(ballisticRoot, "Magazine", new Vector3(0f, -0.2f, 0.05f));
            Transform chamber = CreateChild(ballisticRoot, "Chamber", new Vector3(0f, 0.02f, 0.2f));

            Transform modelRoot = CreateChild(presentationRoot, "ModelRoot");
            Transform colliderRoot = CreateChild(presentationRoot, "ColliderRoot");
            Transform vfxRoot = CreateChild(presentationRoot, "VFXRoot");
            Transform audioRoot = CreateChild(presentationRoot, "AudioRoot");
            Transform animationRoot = CreateChild(presentationRoot, "AnimationRoot");
            BuildRifleModel(modelRoot);

            template.identity.weaponId = WeaponKey;
            template.identity.displayName = "基础步枪";
            template.identity.fireKind = ESWeaponTemplateFireKind.Shot;
            template.identity.designNote = "基础远程武器垂直切片；开火、弹丸和命中统一走 Weapon/Shot 正式运行链。";
            template.runtimeBridge.itemRoot = item;
            template.mount.mountRoot = mountRoot;
            template.mount.rightHandGrip = rightHandGrip;
            template.mount.leftHandGrip = leftHandGrip;
            template.mount.aimReference = aimReference;
            template.mount.recoilPivot = recoilPivot;
            template.ballistic.ballisticRoot = ballisticRoot;
            template.ballistic.muzzle = muzzle;
            template.ballistic.shellEject = shellEject;
            template.ballistic.magazine = magazine;
            template.ballistic.chamber = chamber;
            template.ballistic.rayOrigin = rayOrigin;
            template.ballistic.shotSpawn = shotSpawn;
            template.presentation.presentationRoot = presentationRoot;
            template.presentation.modelRoot = modelRoot;
            template.presentation.colliderRoot = colliderRoot;
            template.presentation.vfxRoot = vfxRoot;
            template.presentation.audioRoot = audioRoot;
            template.presentation.animationRoot = animationRoot;
            template.debug.debugRoot = debugRoot;

                binding.twoHanded = true;
                binding.ConfigureReferences(rightHandGrip, leftHandGrip, muzzle, aimReference, presentationRoot.gameObject);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void BuildRifleModel(Transform parent)
        {
            CreatePrimitiveModel(parent, PrimitiveType.Cube, "机匣", new Vector3(0f, 0f, 0.22f), new Vector3(0.18f, 0.18f, 0.62f));
            CreatePrimitiveModel(parent, PrimitiveType.Cube, "枪托", new Vector3(0f, 0.01f, -0.32f), new Vector3(0.2f, 0.22f, 0.45f));
            CreatePrimitiveModel(parent, PrimitiveType.Cylinder, "枪管", new Vector3(0f, 0.02f, 0.72f), new Vector3(0.055f, 0.34f, 0.055f), new Vector3(90f, 0f, 0f));
            CreatePrimitiveModel(parent, PrimitiveType.Cube, "弹匣", new Vector3(0f, -0.18f, 0.08f), new Vector3(0.13f, 0.3f, 0.18f), new Vector3(12f, 0f, 0f));
            CreatePrimitiveModel(parent, PrimitiveType.Cube, "握把", new Vector3(0f, -0.18f, -0.16f), new Vector3(0.12f, 0.28f, 0.13f), new Vector3(-15f, 0f, 0f));
        }

        private static void CreatePrimitiveModel(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler = default)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localEulerAngles = localEuler;
            part.transform.localScale = localScale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static void ValidateShotPrefab(GameObject prefab, ItemDataInfo info)
        {
            Item item = prefab != null ? prefab.GetComponent<Item>() : null;
            if (item == null || item.prefabDefinition != info)
                throw new System.InvalidOperationException("基础步枪弹丸 Prefab 未绑定自己的 ItemDataInfo。");
            ValidateNoMissingScripts(prefab, "基础步枪弹丸");
            if (prefab.GetComponentsInChildren<Item>(true).Length != 1)
                throw new System.InvalidOperationException("基础步枪弹丸 Prefab 必须且只能包含一个 Item 根。");
            if (item.basicDomain == null || item.basicDomain.FindMyModule<ItemShotModule>() == null)
                throw new System.InvalidOperationException("基础步枪弹丸 Prefab 根节点缺少 ItemShotModule。");
            if (prefab.GetComponentInChildren<Rigidbody>(true) != null
                || prefab.GetComponentInChildren<Rigidbody2D>(true) != null
                || prefab.GetComponentInChildren<Collider>(true) != null
                || prefab.GetComponentInChildren<Collider2D>(true) != null)
            {
                throw new System.InvalidOperationException("基础步枪弹丸不得附带第二套 Rigidbody/Collider 运动后端。");
            }
        }

        private static void ValidateWeaponPrefab(GameObject prefab, ItemDataInfo info)
        {
            Item item = prefab != null ? prefab.GetComponent<Item>() : null;
            ESWeaponSceneTemplate template = prefab != null ? prefab.GetComponent<ESWeaponSceneTemplate>() : null;
            EntityWeaponBinding binding = prefab != null ? prefab.GetComponent<EntityWeaponBinding>() : null;
            if (item == null || item.prefabDefinition != info || template == null || binding == null)
                throw new System.InvalidOperationException("基础步枪 Prefab 缺少 Item、Template、Binding 或 Definition 绑定。");
            ValidateNoMissingScripts(prefab, "基础步枪");
            if (prefab.GetComponentsInChildren<Item>(true).Length != 1
                || prefab.GetComponentsInChildren<ESWeaponSceneTemplate>(true).Length != 1
                || prefab.GetComponentsInChildren<EntityWeaponBinding>(true).Length != 1)
            {
                throw new System.InvalidOperationException("基础步枪 Prefab 必须且只能包含一套 Item、Template 与 Binding。");
            }
            if (template.identity == null
                || !string.Equals(template.identity.weaponId, WeaponKey, System.StringComparison.Ordinal)
                || template.identity.fireKind != ESWeaponTemplateFireKind.Shot
                || !template.HasRequiredAuthoringSockets())
            {
                throw new System.InvalidOperationException("基础步枪 Prefab 的身份、交付模式或作者挂点无效。");
            }
            if (template.runtimeBridge == null || template.runtimeBridge.itemRoot != item
                || template.mount == null
                || template.ballistic == null
                || template.presentation == null
                || binding.Muzzle != template.ballistic.muzzle
                || binding.GripPivot != template.mount.rightHandGrip
                || binding.OffHandGrip != template.mount.leftHandGrip
                || binding.AimReference != template.mount.aimReference
                || binding.PresentationRoot == null
                || binding.PresentationRoot.transform != template.presentation.presentationRoot)
            {
                throw new System.InvalidOperationException("基础步枪运行桥接或武器挂点没有完整绑定。");
            }

            Transform[] ownedReferences =
            {
                template.mount.mountRoot,
                template.mount.rightHandGrip,
                template.mount.leftHandGrip,
                template.mount.aimReference,
                template.mount.recoilPivot,
                template.ballistic.ballisticRoot,
                template.ballistic.muzzle,
                template.ballistic.shellEject,
                template.ballistic.magazine,
                template.ballistic.chamber,
                template.ballistic.rayOrigin,
                template.ballistic.shotSpawn,
                template.presentation.presentationRoot,
                template.presentation.modelRoot,
                template.presentation.colliderRoot,
                template.presentation.vfxRoot,
                template.presentation.audioRoot,
                template.presentation.animationRoot,
                template.debug?.debugRoot
            };
            for (int i = 0; i < ownedReferences.Length; i++)
            {
                if (!IsOwnedTransform(ownedReferences[i], prefab.transform))
                    throw new System.InvalidOperationException("基础步枪作者引用缺失或指向 Prefab 外部，索引=" + i);
            }
        }

        private static void ValidateNoMissingScripts(GameObject prefab, string label)
        {
            if (prefab != null && CountMissingScriptsRecursive(prefab.transform) > 0)
                throw new System.InvalidOperationException(label + " Prefab 子树包含 Missing Script。");
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

        private static bool IsOwnedTransform(Transform target, Transform root)
        {
            return target != null && (target == root || target.IsChildOf(root));
        }

        private static EntityWeaponBinding AddWeaponBinding(GameObject root)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(WeaponBindingScriptPath);
            System.Type scriptType = script != null ? script.GetClass() : null;
            if (scriptType != typeof(EntityWeaponBinding))
                throw new System.InvalidOperationException("EntityWeaponBinding MonoScript 未解析到正式类型。");
            EntityWeaponBinding binding = root.AddComponent(scriptType) as EntityWeaponBinding;
            if (binding == null || MonoScript.FromMonoBehaviour(binding) != script)
                throw new System.InvalidOperationException("EntityWeaponBinding 组件没有绑定到正式 MonoScript。");
            return binding;
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition = default)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child.transform;
        }

    }
}
