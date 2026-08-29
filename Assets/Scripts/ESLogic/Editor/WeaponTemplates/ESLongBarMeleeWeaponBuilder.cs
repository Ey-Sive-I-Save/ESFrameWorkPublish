using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// Creates the first concrete weapon slice: a simple long-bar melee weapon.
    /// Authoring only; combat execution remains in EntityBasicCombatModule/Action.
    /// </summary>
    public static class ESLongBarMeleeWeaponBuilder
    {
        public const string ItemKey = "item.weapon.melee.long_bar";
        public const string WeaponKey = "weapon.melee.long_bar";
        public const string WeaponPrefabAssetKey = "prefab.weapon.melee.long_bar";
        public const string WeaponPrefabPath = "Assets/ESNormalAssets/WeaponPrototypes/大长条.prefab";
        private const string WeaponPrefabRebuildPath = "Assets/ESNormalAssets/WeaponPrototypes/大长条_Rebuild.prefab";
        private const string WeaponBindingScriptPath = "Assets/Scripts/ESLogic/Runtime/Entity/Entity/Domains/Equipment/EntityWeaponBinding.cs";
        public const string WeaponInfoPath = "Assets/ESNormalAssets/Data/Group/Item/大长条_ItemDataInfo.asset";
        public const string WeaponGroupPath = "Assets/ESNormalAssets/Data/Group/Item/大长条_WeaponDataGroup.asset";
        public const string AssetLibraryPath = "Assets/ESNormalAssets/Data/AssetLibrary/基本库.asset";
        private const string PrimaryAttackActionKey = "melee.attack";
        private const string WeaponDisplayName = "大长条";
        private const string WeaponDesignNote = "首个近战垂直切片；只提供作者结构与挂点，攻击执行走 Entity Combat/Action 执行链。";

        [MenuItem("【ES】/内容制作/武器/创建近战武器/大长条", false, 140)]
        public static void CreateLongBarMenu()
        {
            GameObject prefab = CreateLongBarWeapon();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[ES Weapon] 已创建或验证近战武器：" + WeaponKey, prefab);
        }

        [MenuItem("【ES】/内容制作/武器/升级近战武器/大长条结构与绑定", false, 141)]
        public static void UpgradeLongBarMenu()
        {
            GameObject prefab = UpgradeLongBarWeaponAuthoring();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[ES Weapon] 已升级大长条作者结构与绑定：" + WeaponKey, prefab);
        }

        public static GameObject CreateLongBarWeapon()
        {
            EnsureAuthoringIsSafe();
            EnsureFolder("Assets/ESNormalAssets/WeaponPrototypes");
            EnsureFolder("Assets/ESNormalAssets/Data/Group/Item");

            UnityEngine.Object infoAsset = AssetDatabase.LoadMainAssetAtPath(WeaponInfoPath);
            if (infoAsset != null && !(infoAsset is ItemDataInfo))
                throw new System.InvalidOperationException("大长条定义路径已被其他资产类型占用，拒绝覆盖：" + WeaponInfoPath);

            ItemDataInfo info = infoAsset as ItemDataInfo;
            ValidateDefinitionOwnership(info);
            ValidateGroupOwnership(info);

            UnityEngine.Object prefabAsset = AssetDatabase.LoadMainAssetAtPath(WeaponPrefabPath);
            if (prefabAsset != null && !(prefabAsset is GameObject))
                throw new System.InvalidOperationException("大长条 Prefab 路径已被其他资产类型占用，拒绝覆盖：" + WeaponPrefabPath);
            if (info == null && prefabAsset != null)
                throw new System.InvalidOperationException("大长条定义缺失但固定 Prefab 已存在，无法证明资产所有权，拒绝自动接管：" + WeaponPrefabPath);

            if (info == null)
            {
                info = ScriptableObject.CreateInstance<ItemDataInfo>();
                ConfigureDefinition(info);
                try
                {
                    AssetDatabase.CreateAsset(info, WeaponInfoPath);
                }
                catch
                {
                    if (info != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(info)))
                        UnityEngine.Object.DestroyImmediate(info);
                    throw;
                }
            }

            ESItemDataValidationCode validationCode = info.ValidateConfiguration();
            if (validationCode != ESItemDataValidationCode.Valid)
                throw new System.InvalidOperationException("大长条 WeaponDataInfo 验证失败：" + info.GetValidationMessage(validationCode));
            ValidateWeaponDefinition(info);

            EditorUtility.SetDirty(info);

            GameObject prefab = prefabAsset as GameObject;
            if (prefab == null)
            {
                GameObject root = BuildWeaponRoot(info);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, WeaponPrefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                if (prefab == null)
                    throw new System.InvalidOperationException("创建大长条 Prefab 失败：" + WeaponPrefabPath);
            }
            else
            {
                ValidateExistingPrefab(prefab, info);
            }

            RegisterPrefabAndBindKey(info, prefab);
            ValidatePrefabAssetKey(info, prefab);
            EditorUtility.SetDirty(info);
            EnsureGroup(info);
            AssetDatabase.SaveAssetIfDirty(info);
            AssetDatabase.Refresh();

            return prefab;
        }

        private static void EnsureAuthoringIsSafe()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Unity 正在 PlayMode 或准备切换 PlayMode，禁止生成/修改大长条作者资产。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new System.InvalidOperationException("Unity 正在编译、域重载或导入资产，禁止生成/修改大长条作者资产。");
        }

        private static void ValidateDefinitionOwnership(ItemDataInfo info)
        {
            if (info == null)
                return;

            if (!string.IsNullOrEmpty(info.KeyName) && !string.Equals(info.KeyName, WeaponKey, System.StringComparison.Ordinal))
                throw new System.InvalidOperationException("大长条定义路径已属于其他业务 Key，拒绝覆盖：" + info.KeyName);

            if (info.baseConfig != null && info.baseConfig.kind != ItemKind.None && info.baseConfig.kind != ItemKind.Weapon)
                throw new System.InvalidOperationException("大长条定义路径已属于其他 ItemKind，拒绝转换：" + info.baseConfig.kind);

            if (info.itemKey != null
                && info.itemKey.IsConfigured
                && !string.Equals(info.itemKey.StringKey, ItemKey, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("大长条定义内的 Item Key 已属于其他物品，拒绝覆盖：" + info.itemKey.StringKey);
            }

            ItemWeaponDataBlock weapon = info.kindData as ItemWeaponDataBlock;
            if (weapon != null
                && weapon.key != null
                && weapon.key.IsConfigured
                && !string.Equals(weapon.key.StringKey, WeaponKey, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("大长条定义内的 Weapon Key 已属于其他武器，拒绝覆盖：" + weapon.key.StringKey);
            }
        }

        private static void ValidateGroupOwnership(ItemDataInfo info)
        {
            UnityEngine.Object groupAsset = AssetDatabase.LoadMainAssetAtPath(WeaponGroupPath);
            if (groupAsset != null && !(groupAsset is ItemDataGroup))
                throw new System.InvalidOperationException("大长条数据组路径已被其他资产类型占用，拒绝覆盖：" + WeaponGroupPath);

            ItemDataGroup group = groupAsset as ItemDataGroup;
            if (group == null || group.Infos == null || !group.Infos.TryGetValue(WeaponKey, out ItemDataInfo existing))
                return;

            if (info == null || existing != info)
                throw new System.InvalidOperationException("大长条数据组中的稳定 Key 已指向其他定义，拒绝覆盖：" + WeaponKey);
        }

        private static void ValidateExistingPrefab(GameObject prefab, ItemDataInfo info)
        {
            ValidateWeaponDefinition(info);
            ValidatePrefabOwnership(prefab, info);

            ESWeaponSceneTemplate template = prefab.GetComponent<ESWeaponSceneTemplate>();
            EntityWeaponBinding binding = prefab.GetComponent<EntityWeaponBinding>();
            if (template == null || binding == null)
                throw new System.InvalidOperationException("大长条 Prefab 缺少 ESWeaponSceneTemplate 或 EntityWeaponBinding。");

            if (template.identity == null
                || !string.Equals(template.identity.weaponId, WeaponKey, System.StringComparison.Ordinal)
                || !string.Equals(template.identity.displayName, WeaponDisplayName, System.StringComparison.Ordinal)
                || template.identity.fireKind != ESWeaponTemplateFireKind.Custom)
            {
                throw new System.InvalidOperationException("大长条 Prefab 的模板身份必须明确为近战 Custom，不能保留通用步枪默认值。");
            }

            if (!template.HasRequiredAuthoringSockets())
                throw new System.InvalidOperationException("大长条 Prefab 缺少右手/左手/瞄准/枪口/射线等标准作者挂点。");

            Transform[] requiredTemplateReferences =
            {
                template.mount?.mountRoot,
                template.mount?.rightHandGrip,
                template.mount?.leftHandGrip,
                template.mount?.aimReference,
                template.mount?.recoilPivot,
                template.ballistic?.ballisticRoot,
                template.ballistic?.muzzle,
                template.ballistic?.shellEject,
                template.ballistic?.magazine,
                template.ballistic?.chamber,
                template.ballistic?.rayOrigin,
                template.ballistic?.shotSpawn,
                template.presentation?.presentationRoot,
                template.presentation?.modelRoot,
                template.presentation?.colliderRoot,
                template.presentation?.vfxRoot,
                template.presentation?.audioRoot,
                template.presentation?.animationRoot,
                template.debug?.debugRoot,
            };
            for (int i = 0; i < requiredTemplateReferences.Length; i++)
            {
                if (!IsOwnedTransform(requiredTemplateReferences[i], prefab.transform))
                    throw new System.InvalidOperationException("大长条 Prefab 的标准模板引用缺失或指向 Prefab 外部，索引=" + i);
            }

            if (template.runtimeBridge == null || template.runtimeBridge.itemRoot != prefab.GetComponent<Item>())
                throw new System.InvalidOperationException("大长条 Prefab 的 RuntimeBridge.ItemRoot 未绑定根 Item。");

            if (!binding.twoHanded
                || !IsOwnedTransform(binding.GripPivot, prefab.transform)
                || !IsOwnedTransform(binding.OffHandGrip, prefab.transform)
                || !IsOwnedTransform(binding.Muzzle, prefab.transform)
                || !IsOwnedTransform(binding.AimReference, prefab.transform)
                || binding.PresentationRoot == null
                || !IsOwnedTransform(binding.PresentationRoot.transform, prefab.transform))
            {
                throw new System.InvalidOperationException("大长条 EntityWeaponBinding 的 GripPivot、OffHandGrip、Muzzle、AimReference 或 PresentationRoot 不完整。");
            }

            if (prefab.GetComponentInChildren<Rigidbody>(true) != null
                || prefab.GetComponentInChildren<Rigidbody2D>(true) != null
                || prefab.GetComponentInChildren<Collider2D>(true) != null)
            {
                throw new System.InvalidOperationException("大长条 Prefab 检测到额外物理/2D 后端；近战执行必须继续由现有 Entity Combat/Action 链路负责。");
            }
        }

        private static void ValidateWeaponDefinition(ItemDataInfo info)
        {
            if (info == null || info.baseConfig == null || info.baseConfig.kind != ItemKind.Weapon)
                throw new System.InvalidOperationException("大长条必须是正式 ItemKind.Weapon 定义。");

            if (!string.Equals(info.KeyName, WeaponKey, System.StringComparison.Ordinal))
                throw new System.InvalidOperationException("大长条 Item 定义 Key 不匹配：" + info.KeyName);

            if (info.itemKey == null
                || !info.itemKey.IsConfigured
                || !string.Equals(info.itemKey.StringKey, ItemKey, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("大长条缺少匹配的正式 Item Key：" + ItemKey);
            }

            ItemWeaponDataBlock weapon = info.kindData as ItemWeaponDataBlock;
            if (weapon == null
                || weapon.key == null
                || !weapon.key.IsConfigured
                || !string.Equals(weapon.key.StringKey, WeaponKey, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("大长条缺少匹配的正式 Weapon Key。");
            }

            ItemWeaponSharedData shared = weapon.sharedData;
            if (shared == null)
                throw new System.InvalidOperationException("大长条缺少正式 WeaponDefinition。");
            if (!shared.ValidateDefinition(out string validationError))
                throw new System.InvalidOperationException("大长条 WeaponDefinition 无效：" + validationError);

            if (shared.weaponKind != ItemWeaponKind.Melee)
                throw new System.InvalidOperationException("大长条正式武器类型必须是 Melee。");

            if (shared.primaryAttackAction == null
                || !shared.primaryAttackAction.IsConfigured
                || !string.Equals(
                    shared.primaryAttackAction.StringKey,
                    PrimaryAttackActionKey,
                    System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("大长条必须显式绑定近战普攻 Action：" + PrimaryAttackActionKey);
            }

            if ((shared.defaultShot != null && shared.defaultShot.IsConfigured)
                || shared.fire == null
                || shared.fire.enabled
                || shared.recoil == null
                || shared.recoil.enabled)
            {
                throw new System.InvalidOperationException("大长条近战定义不得启用 Shot、WeaponFire 或枪械后坐力。");
            }
        }

        private static void ValidatePrefabOwnership(GameObject prefab, ItemDataInfo info)
        {
            ValidatePrefabDefinitionOwnership(prefab, info);

            if (CountMissingScriptsRecursive(prefab != null ? prefab.transform : null) > 0
                || prefab.GetComponentsInChildren<Item>(true).Length != 1
                || prefab.GetComponentsInChildren<ESWeaponSceneTemplate>(true).Length != 1
                || prefab.GetComponentsInChildren<EntityWeaponBinding>(true).Length != 1)
            {
                throw new System.InvalidOperationException("大长条 Prefab 必须且只能包含一套 Item、ESWeaponSceneTemplate 与 EntityWeaponBinding。");
            }
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

        private static void ValidatePrefabDefinitionOwnership(GameObject prefab, ItemDataInfo info)
        {
            Item item = prefab.GetComponent<Item>();
            if (item == null || item.prefabDefinition != info)
                throw new System.InvalidOperationException("大长条 Prefab 不属于当前 ItemDataInfo，拒绝复用或改绑：" + WeaponPrefabPath);
        }

        internal static void ValidateLongBarPrefabForAuthoring(GameObject prefab)
        {
            ItemDataInfo info = AssetDatabase.LoadAssetAtPath<ItemDataInfo>(WeaponInfoPath);
            if (info == null)
                throw new System.InvalidOperationException("缺少大长条 ItemDataInfo：" + WeaponInfoPath);
            ValidateExistingPrefab(prefab, info);
        }

        private static GameObject UpgradeLongBarWeaponAuthoring()
        {
            EnsureAuthoringIsSafe();
            ItemDataInfo info = AssetDatabase.LoadAssetAtPath<ItemDataInfo>(WeaponInfoPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath);
            if (info == null || prefab == null)
                throw new System.InvalidOperationException("升级大长条前必须先存在定义与 Prefab。");

            ValidateDefinitionOwnership(info);
            // 开发期硬重建负责替换旧作者结构。这里只证明固定资产仍属于
            // 当前 ItemDataInfo；保存后再执行完整组件与引用校验。
            ValidatePrefabDefinitionOwnership(prefab, info);
            ConfigureDefinition(info);
            RegisterPrefabAndBindKey(info, prefab);
            ValidateWeaponDefinition(info);
            ValidatePrefabAssetKey(info, prefab);
            EditorUtility.SetDirty(info);

            GameObject root = BuildWeaponRoot(info);
            string rebuildPath = AssetDatabase.GenerateUniqueAssetPath(WeaponPrefabRebuildPath);
            string backupPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ESLongBarPrefab-" + System.Guid.NewGuid().ToString("N") + ".prefab");
            bool backupCreated = false;
            bool replacementCommitted = false;
            bool backupRecoveryFailed = false;
            try
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, rebuildPath);
                if (saved == null)
                    throw new System.InvalidOperationException("保存临时重建的大长条 Prefab 失败：" + rebuildPath);

                ValidateExistingPrefab(saved, info);
                if (Selection.activeObject != null
                    && string.Equals(
                    AssetDatabase.GetAssetPath(Selection.activeObject),
                        WeaponPrefabPath,
                        System.StringComparison.Ordinal))
                {
                    Selection.activeObject = null;
                }
                string targetAbsolutePath = System.IO.Path.GetFullPath(WeaponPrefabPath);
                string backupAbsolutePath = System.IO.Path.GetFullPath(backupPath);
                if (System.IO.File.Exists(targetAbsolutePath))
                {
                    System.IO.File.Copy(targetAbsolutePath, backupAbsolutePath, true);
                    backupCreated = true;
                }
                FileUtil.ReplaceFile(
                    System.IO.Path.GetFullPath(rebuildPath),
                    targetAbsolutePath);
                AssetDatabase.ImportAsset(
                    WeaponPrefabPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath);
                ValidateExistingPrefab(imported, info);
                replacementCommitted = true;
            }
            catch
            {
                if (backupCreated)
                {
                    try
                    {
                        System.IO.File.Copy(
                            System.IO.Path.GetFullPath(backupPath),
                            System.IO.Path.GetFullPath(WeaponPrefabPath),
                            true);
                        AssetDatabase.ImportAsset(
                            WeaponPrefabPath,
                            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    }
                    catch (Exception restoreException)
                    {
                        backupRecoveryFailed = true;
                        Debug.LogException(new InvalidOperationException("大长条 Prefab 升级失败，旧文件恢复也失败。", restoreException));
                    }
                }
                throw;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (!AssetDatabase.DeleteAsset(rebuildPath)
                    && AssetDatabase.LoadMainAssetAtPath(rebuildPath) != null)
                {
                    Debug.LogWarning("大长条 Prefab 临时重建资产未能清理，请手动检查：" + rebuildPath);
                }
                if (!backupRecoveryFailed && backupCreated && System.IO.File.Exists(System.IO.Path.GetFullPath(backupPath)))
                {
                    try
                    {
                        System.IO.File.Delete(System.IO.Path.GetFullPath(backupPath));
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogWarning(
                            "大长条 Prefab 已完成处理，但临时备份未能清理，请手动检查："
                            + backupPath
                            + "\n"
                            + cleanupException.Message);
                    }
                }
            }

            AssetDatabase.SaveAssetIfDirty(info);
            AssetDatabase.Refresh();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPrefabPath);
            ValidateExistingPrefab(prefab, info);
            if (!replacementCommitted)
                throw new System.InvalidOperationException("大长条 Prefab 替换未完成。");
            return prefab;
        }

        private static void ConfigureDefinition(ItemDataInfo info)
        {
            info.name = "大长条_ItemDataInfo";
            info.SetKey(WeaponKey);
            info.itemKey = new ESItemConfigKey { stringKey = ItemKey };
            info.baseConfig ??= new ItemBaseConfig();
            info.baseConfig.kind = ItemKind.Weapon;
            info.baseConfig.displayName = WeaponDisplayName;
            info.baseConfig.prefabKey ??= new ESAssetReferPrefabConfigKey();
            info.baseConfig.iconKey ??= new ESAssetReferSpriteConfigKey();
            info.interactConfig ??= new ItemInteractConfig();
            info.logicConfig ??= new ItemLogicConfig();
            info.moveConfig ??= new ItemMoveConfig();

            ItemWeaponDataBlock weapon = info.kindData as ItemWeaponDataBlock;
            if (weapon == null)
                weapon = ItemWeaponDataBlock.Default;

            weapon.key = new ESWeaponConfigKey { stringKey = WeaponKey };
            weapon.sharedData ??= ItemWeaponSharedData.Default;
            weapon.sharedData.weaponKind = ItemWeaponKind.Melee;
            weapon.sharedData.primaryAttackAction = new ESActionConfigKey { stringKey = PrimaryAttackActionKey };
            weapon.sharedData.defaultShot = new ESShotConfigKey();
            weapon.sharedData.hitRadius = 0.65f;
            weapon.sharedData.cooldown = 0.35f;
            weapon.sharedData.fire ??= WeaponFireDefinitionData.Default;
            weapon.sharedData.fire.enabled = false;
            weapon.sharedData.recoil ??= WeaponRecoilDefinitionData.Default;
            weapon.sharedData.recoil.enabled = false;
            weapon.initialState = ItemWeaponVariableData.Default;
            weapon.initialState.durability = 1f;
            info.kindData = weapon;
        }

        private static void RegisterPrefabAndBindKey(ItemDataInfo info, GameObject prefab)
        {
            if (info == null || prefab == null)
                throw new System.InvalidOperationException("绑定大长条资源键时定义或 Prefab 为空。");
            if (AssetDatabase.LoadAssetAtPath<ESAssetLibrary>(AssetLibraryPath) == null)
                throw new System.InvalidOperationException("缺少大长条目标 AssetLibrary：" + AssetLibraryPath);
            if (!ESAssetPage.TryGetAssetIdentityEditor(prefab, out string guid, out long localFileId))
                throw new System.InvalidOperationException("无法读取大长条 Prefab 的稳定 GUID/LocalFileId。");

            info.baseConfig ??= new ItemBaseConfig();
            info.baseConfig.prefabKey ??= new ESAssetReferPrefabConfigKey();
            ESAssetReferPrefabConfigKey key = info.baseConfig.prefabKey;
            if (key.IsConfigured
                && (!string.Equals(key.StringKey, WeaponPrefabAssetKey, System.StringComparison.Ordinal)
                    || (!string.IsNullOrEmpty(key.guid)
                        && !string.Equals(key.guid, guid, System.StringComparison.OrdinalIgnoreCase))))
            {
                throw new System.InvalidOperationException("大长条定义已绑定其他 Prefab AssetKey，拒绝静默改绑。");
            }

            var request = new ESContentRegistrationRequest
            {
                action = ESContentRegistrationAction.RegisterAsset,
                commit = false,
                assetPath = WeaponPrefabPath,
                libraryPath = AssetLibraryPath,
                expectedLocalFileId = localFileId,
                keyMode = ESContentStableKeyMode.StringOnly,
                stringKey = WeaponPrefabAssetKey,
                assetKind = ESAssetReferKind.Prefab.ToString()
            };
            ESContentRegistrationResult preview = ESContentRegistrationAuthoring.Execute(request);
            if (!preview.success)
                throw new System.InvalidOperationException("大长条 Prefab 注册预检失败：" + preview.status + "，" + preview.message);

            request.requestId = preview.requestId;
            request.commit = true;
            request.expectedGuid = preview.guid;
            request.expectedLocalFileId = preview.localFileId;
            request.expectedLibraryRevision = preview.targetRevision;
            ESContentRegistrationResult result = ESContentRegistrationAuthoring.Execute(request);
            if (!result.success)
                throw new System.InvalidOperationException("大长条 Prefab 注册失败：" + result.status + "，" + result.message);

            key.stringKey = WeaponPrefabAssetKey;
            key.SetAssetAuthority(guid, localFileId, typeof(GameObject).FullName, WeaponPrefabPath);
        }

        private static void ValidatePrefabAssetKey(ItemDataInfo info, GameObject prefab)
        {
            ESAssetReferPrefabConfigKey key = info?.baseConfig?.prefabKey;
            if (prefab == null
                || key == null
                || !key.IsConfigured
                || !string.Equals(key.StringKey, WeaponPrefabAssetKey, System.StringComparison.Ordinal)
                || !ESAssetPage.TryGetAssetIdentityEditor(prefab, out string guid, out long localFileId)
                || !string.Equals(key.guid, guid, System.StringComparison.OrdinalIgnoreCase)
                || key.localFileId != localFileId)
            {
                throw new System.InvalidOperationException("大长条定义缺少与 Prefab 一致的正式 AssetKey 绑定。");
            }

        }

        private static GameObject BuildWeaponRoot(ItemDataInfo info)
        {
            GameObject root = new GameObject("大长条");
            Item item = root.AddComponent<Item>();
            item.prefabDefinition = info;
            ESWeaponSceneTemplate template = root.AddComponent<ESWeaponSceneTemplate>();
            EntityWeaponBinding binding = AddWeaponBinding(root);
            ConfigureTemplateIdentity(template);

            Transform runtimeRoot = CreateChild(root.transform, ESWeaponSceneTemplate.RuntimeRootName);
            Transform mountRoot = CreateChild(root.transform, ESWeaponSceneTemplate.MountRootName);
            Transform ballisticRoot = CreateChild(root.transform, ESWeaponSceneTemplate.BallisticRootName);
            Transform presentationRoot = CreateChild(root.transform, ESWeaponSceneTemplate.PresentationRootName);
            Transform debugRoot = CreateChild(root.transform, ESWeaponSceneTemplate.DebugRootName);

            Transform rightHandGrip = CreateChild(mountRoot, "RightHandGrip", new Vector3(0.03f, -0.03f, -0.08f));
            Transform leftHandGrip = CreateChild(mountRoot, "LeftHandGrip", new Vector3(-0.03f, -0.02f, 0.18f));
            Transform aimReference = CreateChild(mountRoot, "AimReference", new Vector3(0f, 0.05f, 0.9f));
            Transform recoilPivot = CreateChild(mountRoot, "RecoilPivot", new Vector3(0f, 0.02f, 0.2f));
            Transform muzzle = CreateChild(ballisticRoot, "Muzzle", new Vector3(0f, 0f, 1.75f));
            Transform rayOrigin = CreateChild(ballisticRoot, "RayOrigin", new Vector3(0f, 0f, 1.2f));
            Transform shotSpawn = CreateChild(ballisticRoot, "ShotSpawn", new Vector3(0f, 0f, 1.75f));
            Transform shellEject = CreateChild(ballisticRoot, "ShellEject");
            Transform magazine = CreateChild(ballisticRoot, "Magazine");
            Transform chamber = CreateChild(ballisticRoot, "Chamber");

            Transform modelRoot = CreateChild(presentationRoot, "ModelRoot");
            CreateChild(presentationRoot, "ColliderRoot");
            CreateChild(presentationRoot, "VFXRoot");
            CreateChild(presentationRoot, "AudioRoot");
            CreateChild(presentationRoot, "AnimationRoot");

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "大长条_模型";
            blade.transform.SetParent(modelRoot, false);
            blade.transform.localPosition = new Vector3(0f, 0f, 0.85f);
            blade.transform.localRotation = Quaternion.identity;
            blade.transform.localScale = new Vector3(0.16f, 0.16f, 1.7f);
            UnityEngine.Object.DestroyImmediate(blade.GetComponent<Collider>());

            // Weapon-local grip/aim references are presentation data; hand/holster roots
            // are resolved by EntityWeaponBinding from the owning Entity.
            binding.twoHanded = true;
            binding.ConfigureReferences(
                rightHandGrip,
                leftHandGrip,
                muzzle,
                aimReference,
                presentationRoot.gameObject);

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
            template.presentation.colliderRoot = presentationRoot.Find("ColliderRoot");
            template.presentation.vfxRoot = presentationRoot.Find("VFXRoot");
            template.presentation.audioRoot = presentationRoot.Find("AudioRoot");
            template.presentation.animationRoot = presentationRoot.Find("AnimationRoot");
            template.debug.debugRoot = debugRoot;
            return root;
        }

        private static EntityWeaponBinding AddWeaponBinding(GameObject root)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(WeaponBindingScriptPath);
            System.Type scriptType = script != null ? script.GetClass() : null;
            if (scriptType != typeof(EntityWeaponBinding))
            {
                throw new System.InvalidOperationException(
                    "EntityWeaponBinding MonoScript 未解析到当前正式类型：" + WeaponBindingScriptPath);
            }

            EntityWeaponBinding binding = root.AddComponent(scriptType) as EntityWeaponBinding;
            if (binding == null || MonoScript.FromMonoBehaviour(binding) != script)
            {
                throw new System.InvalidOperationException(
                    "EntityWeaponBinding 组件未绑定到正式 MonoScript，拒绝生成 Missing Component。");
            }
            return binding;
        }

        private static void EnsureTemplateSections(ESWeaponSceneTemplate template)
        {
            template.identity ??= new ESWeaponSceneTemplate.IdentitySection();
            template.runtimeBridge ??= new ESWeaponSceneTemplate.RuntimeBridgeSection();
            template.mount ??= new ESWeaponSceneTemplate.MountSection();
            template.ballistic ??= new ESWeaponSceneTemplate.BallisticSection();
            template.presentation ??= new ESWeaponSceneTemplate.PresentationSection();
            template.debug ??= new ESWeaponSceneTemplate.DebugSection();
        }

        private static void ConfigureTemplateIdentity(ESWeaponSceneTemplate template)
        {
            EnsureTemplateSections(template);
            template.identity.weaponId = WeaponKey;
            template.identity.displayName = WeaponDisplayName;
            template.identity.fireKind = ESWeaponTemplateFireKind.Custom;
            template.identity.designNote = WeaponDesignNote;
        }

        private static void ConfigureBinding(EntityWeaponBinding binding, ESWeaponSceneTemplate template)
        {
            binding.twoHanded = true;
            binding.ConfigureReferences(
                template.mount.rightHandGrip,
                template.mount.leftHandGrip,
                template.ballistic.muzzle,
                template.mount.aimReference,
                template.presentation.presentationRoot != null
                    ? template.presentation.presentationRoot.gameObject
                    : throw new System.InvalidOperationException("正式武器缺少作者化 PresentationRoot。"));
        }

        private static bool IsOwnedTransform(Transform target, Transform root)
        {
            return target != null && (target == root || target.IsChildOf(root));
        }

        private static void EnsureGroup(ItemDataInfo info)
        {
            ItemDataGroup group = AssetDatabase.LoadAssetAtPath<ItemDataGroup>(WeaponGroupPath);
            if (group == null)
            {
                group = ScriptableObject.CreateInstance<ItemDataGroup>();
                group.name = "大长条_WeaponDataGroup";
                try
                {
                    AssetDatabase.CreateAsset(group, WeaponGroupPath);
                }
                catch
                {
                    if (group != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(group)))
                        UnityEngine.Object.DestroyImmediate(group);
                    throw;
                }
            }

            group.Infos ??= new Dictionary<string, ItemDataInfo>();
            if (group.Infos.TryGetValue(WeaponKey, out ItemDataInfo existing) && existing != info)
                throw new System.InvalidOperationException("大长条数据组中的稳定 Key 已指向其他定义，拒绝覆盖：" + WeaponKey);

            group.Infos[WeaponKey] = info;
            EditorUtility.SetDirty(group);
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

        private static void EnsureFolder(string folderPath)
        {
            string normalized = folderPath.Replace('\\', '/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                current += "/" + parts[i];
                if (AssetDatabase.IsValidFolder(current))
                    continue;

                string parent = current.Substring(0, current.LastIndexOf('/'));
                AssetDatabase.CreateFolder(parent, parts[i]);
            }
        }
    }
}
