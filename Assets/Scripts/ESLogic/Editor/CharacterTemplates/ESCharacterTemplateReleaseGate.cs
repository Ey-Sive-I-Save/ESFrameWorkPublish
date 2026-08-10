using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 发布层面的角色模板门禁。
    /// 基础模板和 GlobalPreview 只允许作为制作/构建输入，不能经场景或 AssetBundle 进入正式内容。
    /// </summary>
    internal static class ESCharacterTemplateReleaseGate
    {
        private const string BuildInputTemplatePath = ESBasicCharacterTemplateBuilder.TemplatePath;
        private const string RuntimePoolTemplatePath = ESBasicCharacterTemplateBuilder.CompleteTemplatePath;
        private const string PreviewModelPath = "Assets/ESNormalAssets/EditorTools/预览专用.prefab";
        private const string EditorPreviewFolder = "Assets/ESNormalAssets/EditorTools";

        internal static void RunAndThrowIfErrors(string stage)
        {
            var errors = new List<string>(16);
            ValidateForbiddenAssetLabels(errors);
            ValidateBuildSceneDependencies(errors);
            ValidateAssetBundleDependencies(errors);

            if (errors.Count == 0)
                return;

            throw new InvalidOperationException(
                "[角色模板发布门禁] " + stage + " 被阻止：\n- " + string.Join("\n- ", errors));
        }

        /// <summary>
        /// 扫描项目内所有声明了 EntityCharacterIdentity 的 Prefab。
        /// 这是制作期审计入口；发布期仍由依赖闭包检查只验证实际会进入内容的 Prefab。
        /// </summary>
        internal static bool ValidateAllCharacterPrefabModuleContracts(out string report)
        {
            var errors = new List<string>(16);
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            Array.Sort(prefabGuids, StringComparer.Ordinal);

            int profileCount = 0;
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                    continue;

                EntityCharacterIdentity[] profiles = prefab.GetComponentsInChildren<EntityCharacterIdentity>(true);
                for (int j = 0; j < profiles.Length; j++)
                {
                    EntityCharacterIdentity profile = profiles[j];
                    profileCount++;
                    Entity entity = profile != null ? profile.GetComponent<Entity>() : null;
                    if (!ValidateCharacterPrefabModuleContract(profile, entity, out string error))
                        errors.Add(prefabPath + " | " + error);
                    else if (!ValidateMountedStateContract(entity, out string mountError))
                        errors.Add(prefabPath + " | 骑乘状态配置不合格：" + mountError);
                    else if (!ValidateClimbingStateContract(entity, out string climbError))
                        errors.Add(prefabPath + " | 攀爬状态配置不合格：" + climbError);
                }
            }

            report = errors.Count == 0
                ? "[角色基础模块审计] 通过：已检查 " + profileCount
                  + " 个 EntityCharacterIdentity。移动、输入调度和玩家输入写入契约完整。"
                : "[角色基础模块审计] 未通过：\n- " + string.Join("\n- ", errors);
            return errors.Count == 0;
        }

        /// <summary>供正式 Variant 构建器在保存后立即调用的单资产门禁。</summary>
        internal static bool ValidateFormalCharacterPrefab(string prefabPath, out string report)
        {
            var errors = new List<string>(8);
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                report = "[正式角色检查] Prefab 路径为空。";
                return false;
            }

            ValidateFormalCharacterProfile(prefabPath, "正式角色 Prefab " + prefabPath, errors);
            report = errors.Count == 0
                ? "[正式角色检查] 通过：" + prefabPath
                : "[正式角色检查] 未通过：\n- " + string.Join("\n- ", errors);
            return errors.Count == 0;
        }

        private static void ValidateForbiddenAssetLabels(List<string> errors)
        {
            ValidateNoAssetBundleLabel(BuildInputTemplatePath, "基础模板", errors);
            ValidateNoAssetBundleLabel(RuntimePoolTemplatePath, "通用池模板", errors);
            ValidateNoAssetBundleLabel(PreviewModelPath, "预览模型", errors);

            string[] previewGuids = AssetDatabase.FindAssets("t:Prefab", new[] { EditorPreviewFolder });
            for (int i = 0; i < previewGuids.Length; i++)
            {
                string previewPath = AssetDatabase.GUIDToAssetPath(previewGuids[i]);
                ValidateNoAssetBundleLabel(previewPath, "EditorTools 预览资源", errors);
            }
        }

        private static void ValidateNoAssetBundleLabel(string path, string displayName, List<string> errors)
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null && !string.IsNullOrEmpty(importer.assetBundleName))
                errors.Add(displayName + "禁止设置 AssetBundle 标签：" + path);
        }

        private static void ValidateBuildSceneDependencies(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                EditorBuildSettingsScene scene = scenes[i];
                if (scene == null || !scene.enabled || string.IsNullOrEmpty(scene.path))
                    continue;

                ValidateDependencyClosure(scene.path, "Player 场景 " + scene.path, errors);
            }
        }

        private static void ValidateAssetBundleDependencies(List<string> errors)
        {
            string[] bundleNames = AssetDatabase.GetAllAssetBundleNames();
            for (int i = 0; i < bundleNames.Length; i++)
            {
                string bundleName = bundleNames[i];
                string[] roots = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
                for (int j = 0; j < roots.Length; j++)
                    ValidateDependencyClosure(roots[j], "AssetBundle " + bundleName + " 的根资源 " + roots[j], errors);
            }
        }

        private static void ValidateDependencyClosure(string rootPath, string context, List<string> errors)
        {
            string[] dependencies = AssetDatabase.GetDependencies(rootPath, true);
            bool hasBuildInput = false;
            bool hasRuntimePoolTemplate = false;
            var previewDependencies = new HashSet<string>(StringComparer.Ordinal);
            var visitedPrefabs = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependency = dependencies[i];
                if (string.Equals(dependency, BuildInputTemplatePath, StringComparison.Ordinal))
                    hasBuildInput = true;
                if (string.Equals(dependency, RuntimePoolTemplatePath, StringComparison.Ordinal))
                    hasRuntimePoolTemplate = true;
                if (IsEditorPreviewPrefab(dependency))
                    previewDependencies.Add(dependency);

                if (dependency.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                    && visitedPrefabs.Add(dependency))
                {
                    ValidateFormalCharacterProfile(dependency, context, errors);
                }
            }

            if (hasBuildInput)
                errors.Add(context + "直接或间接依赖基础角色构建模板：" + BuildInputTemplatePath);
            if (hasRuntimePoolTemplate)
                errors.Add(context + "直接或间接依赖通用角色池模板；必须替换为正式 CharacterVariant：" + RuntimePoolTemplatePath);
            foreach (string previewDependency in previewDependencies)
                errors.Add(context + "直接或间接依赖 EditorTools 预览 Prefab：" + previewDependency);
        }

        private static bool IsEditorPreviewPrefab(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path.StartsWith(EditorPreviewFolder + "/", StringComparison.Ordinal)
                   && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateFormalCharacterProfile(string prefabPath, string context, List<string> errors)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                return;

            Entity[] entities = prefab.GetComponentsInChildren<Entity>(true);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                if (!ValidateMountedStateContract(entity, out string mountError))
                    errors.Add(context + "中的骑乘状态配置不合格：" + prefabPath + " | " + mountError);
                if (!ValidateClimbingStateContract(entity, out string climbError))
                    errors.Add(context + "中的攀爬状态配置不合格：" + prefabPath + " | " + climbError);

                EntityCharacterIdentity profile = entity != null ? entity.GetComponent<EntityCharacterIdentity>() : null;
                if (profile == null)
                {
                    errors.Add(context + "中的 Entity 缺少根 EntityCharacterIdentity：" + prefabPath);
                    continue;
                }

                switch (profile.prefabRole)
                {
                    case EntityCharacterPrefabRole.CharacterVariant:
                        if (!profile.ValidateFormalCharacter(out string formalError))
                            errors.Add(context + "中的正式角色 Prefab 不合格：" + prefabPath + " | " + formalError);
                        else if (!ValidateFormalCharacterDefinitionKind(profile, out string definitionKindError))
                            errors.Add(context + "中的正式角色定义类型不合格：" + prefabPath + " | " + definitionKindError);
                        else if (!ValidateCharacterPrefabModuleContract(profile, entity, out string moduleError))
                            errors.Add(context + "中的正式角色基础模块不合格：" + prefabPath + " | " + moduleError);
                        else if (!ValidateFormalCharacterPresentation(entity, out string presentationError))
                            errors.Add(context + "中的正式角色模型/动画配置不合格：" + prefabPath + " | " + presentationError);
                        else if (!ValidateFormalCharacterPhysics(entity, out string physicsError))
                            errors.Add(context + "中的正式角色物理/挂点配置不合格：" + prefabPath + " | " + physicsError);
                        else if (!ValidateFormalCharacterCameraMapping(entity, out string cameraMappingError))
                            errors.Add(context + "中的正式角色相机挂点配置不合格：" + prefabPath + " | " + cameraMappingError);
                        else if (!ValidateFormalCharacterIk(entity, out string ikError))
                            errors.Add(context + "中的正式角色 FinalIK 配置不合格：" + prefabPath + " | " + ikError);
                        break;

                    case EntityCharacterPrefabRole.BuildInput:
                        if (!string.Equals(prefabPath, BuildInputTemplatePath, StringComparison.Ordinal))
                            errors.Add(context + "中的 Entity 被错误标记为基础构建输入，必须改为正式 CharacterVariant：" + prefabPath);
                        break;

                    case EntityCharacterPrefabRole.RuntimePoolTemplate:
                        if (!string.Equals(prefabPath, RuntimePoolTemplatePath, StringComparison.Ordinal))
                            errors.Add(context + "中的 Entity 被错误标记为通用池模板，必须改为正式 CharacterVariant：" + prefabPath);
                        break;

                    default:
                        errors.Add(context + "中的 Entity 使用了未识别的 Prefab 角色类型：" + prefabPath);
                        break;
                }
            }
        }

        /// <summary>
        /// 基础能力在序列化模块表中显式声明，不能依赖 AI 域的运行时自动补齐。
        /// BuildInput 和 RuntimePoolTemplate 不携带本地玩家输入；只有 Player 阵营的正式角色可携带它。
        /// </summary>
        private static bool ValidateCharacterPrefabModuleContract(
            EntityCharacterIdentity profile,
            Entity entity,
            out string error)
        {
            if (profile == null || entity == null || profile.gameObject != entity.gameObject)
            {
                error = "EntityCharacterIdentity 必须与 Entity 同挂在角色根节点。";
                return false;
            }

            if (entity.basicDomain == null || entity.aiDomain == null)
            {
                error = "缺少基础域或 AI 域。";
                return false;
            }

            int moveCount = CountBasicModule<EntityBasicMoveRotateModule>(entity);
            int playerWriterCount = CountAiModule<EntityPlayerInputWriteModule>(entity);
            if (moveCount != 1 || entity.aiDomain == null)
            {
                error = "必须有唯一的 EntityBasicMoveRotateModule，且 EntityAIDomain 必须提供输入执行器"
                        + "（当前 Move=" + moveCount + "，AIDomain=" + (entity.aiDomain != null ? "有效" : "缺失") + "）。";
                return false;
            }

            bool requiresPlayerWriter = profile.prefabRole == EntityCharacterPrefabRole.CharacterVariant
                                      && profile.faction == EntityCharacterFaction.Player;
            int expectedPlayerWriterCount = requiresPlayerWriter ? 1 : 0;
            if (playerWriterCount != expectedPlayerWriterCount)
            {
                error = requiresPlayerWriter
                    ? "玩家正式角色必须有唯一的 EntityPlayerInputWriteModule（当前=" + playerWriterCount + "）。"
                    : "非玩家角色、构建模板和通用池模板不得挂 EntityPlayerInputWriteModule（当前="
                      + playerWriterCount + "）。";
                return false;
            }

            if (requiresPlayerWriter)
            {
                if (!profile.defaultCameraDefinition.IsConfigured
                    || !string.Equals(profile.defaultCameraViewKey, ESCameraViewId.Main.Key, StringComparison.Ordinal))
                {
                    error = "玩家正式角色必须配置 MainView 的默认 Camera Definition；相机只由 SceneBinding → Director 驱动。";
                    return false;
                }

                int mountCount = CountBasicModule<EntityBasicMountModule>(entity);
                int climbCount = CountBasicModule<EntityBasicClimbModule>(entity);
                if (mountCount != 1 || climbCount != 1)
                {
                    error = "玩家正式角色必须各有唯一的 EntityBasicMountModule 与 EntityBasicClimbModule"
                            + "（当前 Mount=" + mountCount + "，Climb=" + climbCount + "）。";
                    return false;
                }

                EntityBasicMountModule mount = GetBasicModule<EntityBasicMountModule>(entity);
                EntityBasicClimbModule climb = GetBasicModule<EntityBasicClimbModule>(entity);
                if (mount == null || !mount.enableMount || climb == null || !climb.enableClimb)
                {
                    error = "玩家正式角色的骑乘与攀爬模块必须启用。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        internal static bool ValidateFormalCharacterDefinitionKind(
            EntityCharacterIdentity profile,
            out string error)
        {
            if (profile == null)
            {
                error = "缺少 EntityCharacterIdentity。";
                return false;
            }

            if (profile.faction == EntityCharacterFaction.Player)
            {
                if (profile.definitionSource != EntityCharacterDefinitionSource.Actor
                    || profile.actorDefinition == null)
                {
                    error = "Player 正式角色必须使用 ActorDataInfo。";
                    return false;
                }

                if (profile.actorDefinition.actorKind != ActorDataKind.Player)
                {
                    error = "Player 正式角色必须指向 ActorDataKind.Player，当前="
                            + profile.actorDefinition.actorKind + "。";
                    return false;
                }
            }
            else if (profile.definitionSource == EntityCharacterDefinitionSource.Actor
                     && profile.actorDefinition != null
                     && profile.actorDefinition.actorKind == ActorDataKind.Player)
            {
                error = "非 Player 正式角色不能使用 ActorDataKind.Player 的 ActorDataInfo。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static int CountBasicModule<T>(Entity entity) where T : EntityBasicModuleBase
        {
            if (entity?.basicDomain?.MyModules?.ValuesNow == null)
                return 0;

            int count = 0;
            for (int i = 0; i < entity.basicDomain.MyModules.ValuesNow.Count; i++)
            {
                if (entity.basicDomain.MyModules.ValuesNow[i] is T)
                    count++;
            }
            return count;
        }

        private static T GetBasicModule<T>(Entity entity) where T : EntityBasicModuleBase
        {
            if (entity?.basicDomain?.MyModules?.ValuesNow == null)
                return null;

            for (int i = 0; i < entity.basicDomain.MyModules.ValuesNow.Count; i++)
            {
                if (entity.basicDomain.MyModules.ValuesNow[i] is T module)
                    return module;
            }
            return null;
        }

        private static int CountAiModule<T>(Entity entity) where T : EntityAIModuleBase
        {
            if (entity?.aiDomain?.MyModules?.ValuesNow == null)
                return 0;

            int count = 0;
            for (int i = 0; i < entity.aiDomain.MyModules.ValuesNow.Count; i++)
            {
                if (entity.aiDomain.MyModules.ValuesNow[i] is T)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 角色模型只有一个 Animator；Entity、Driver 和骨骼映射全部引用该唯一表现根。
        /// 非 Humanoid 角色可以不使用 Driver 骨骼绑定，但 Humanoid 不允许静默丢失绑定。
        /// </summary>
        private static bool ValidateFormalCharacterPresentation(Entity entity, out string error)
        {
            Animator[] animators = entity.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1 || animators[0] == null)
            {
                error = "正式角色必须且只能有一个 Animator（当前=" + animators.Length + "）。";
                return false;
            }

            Animator animator = animators[0];
            if (entity.animator != animator)
            {
                error = "Entity.animator 必须指向角色唯一的 Animator。";
                return false;
            }

            StateFinalIKDriver[] drivers = entity.GetComponentsInChildren<StateFinalIKDriver>(true);
            if (drivers.Length != 1 || drivers[0] == null || drivers[0].gameObject != animator.gameObject)
            {
                error = "正式角色必须在唯一 Animator 同一对象上配置唯一 StateFinalIKDriver（当前=" + drivers.Length + "）。";
                return false;
            }

            if (animator.isHuman && !drivers[0].MatchesHumanoidBinding(animator))
            {
                error = "Humanoid Animator 的 StateFinalIKDriver 骨骼绑定必须与当前模型一致。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 正式角色只使用 KCC 主身体和标准 Collider 子节点表达物理职责。
        /// 这里是编辑器门禁，不把 Collider 清单塞回任何运行时 Profile 组件。
        /// </summary>
        private static bool ValidateFormalCharacterPhysics(Entity entity, out string error)
        {
            if (entity == null || entity.kcc == null || entity.kcc.motor == null)
            {
                error = "缺少 Entity KCC Motor。";
                return false;
            }

            CapsuleCollider body = entity.kcc.motor.Capsule;
            if (body == null || body.transform != entity.transform || body.isTrigger
                || body.gameObject.layer != ESPhysicsLayers.EntityBody)
            {
                error = "根 KCC CapsuleCollider 必须在 EntityBody Layer 且非 Trigger。";
                return false;
            }

            EntityTransformMapping mapping = entity.GetComponent<EntityTransformMapping>();
            Transform weaponSocket = mapping != null ? mapping.Resolve(DefaultTransformKey.Weapon) : null;
            Transform namedWeaponSocket = mapping != null ? mapping.Resolve("WeaponSocket") : null;
            if (weaponSocket == null || namedWeaponSocket == null || weaponSocket != namedWeaponSocket
                || !weaponSocket.IsChildOf(entity.transform))
            {
                error = "必须在 EntityTransformMapping 中以 Weapon 和 WeaponSocket 指向同一角色内挂点。";
                return false;
            }

            bool hasHurtBox = false;
            Collider[] colliders = entity.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || collider == body)
                    continue;

                if (collider.gameObject.layer == ESPhysicsLayers.EntityHurtbox)
                {
                    if (!collider.isTrigger)
                    {
                        error = "所有 EntityHurtbox Collider 必须是 Trigger。";
                        return false;
                    }
                    hasHurtBox = true;
                }
                else if (collider.gameObject.layer == ESPhysicsLayers.Interaction && !collider.isTrigger)
                {
                    error = "所有 Interaction Collider 必须是 Trigger。";
                    return false;
                }
            }

            if (!hasHurtBox)
            {
                error = "正式角色至少需要一个 EntityHurtbox Trigger Collider。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal static bool ValidateFormalCharacterCameraMapping(Entity entity, out string error)
        {
            if (entity == null)
            {
                error = "缺少 Entity。";
                return false;
            }

            EntityTransformMapping mapping = entity.GetComponent<EntityTransformMapping>();
            Transform cameraTarget = mapping != null ? mapping.Resolve("CameraTarget") : null;
            if (cameraTarget == null && mapping != null)
                cameraTarget = mapping.Resolve(DefaultTransformKey.Camera);

            if (cameraTarget == null || !cameraTarget.IsChildOf(entity.transform))
            {
                error = "必须在 EntityTransformMapping 中解析 CameraTarget 或 DefaultTransformKey.Camera 到角色内挂点。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateFormalCharacterIk(Entity entity, out string error)
        {
            StateFinalIKDriver[] drivers = entity.GetComponentsInChildren<StateFinalIKDriver>(true);
            for (int i = 0; i < drivers.Length; i++)
            {
                StateFinalIKDriver driver = drivers[i];
                if (driver != null && !driver.ValidateEnabledSolverContract(out string driverError))
                {
                    error = "Driver '" + driver.name + "' 的 FinalIK 契约不满足：" + driverError;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>
        /// 攀爬状态同样必须是明确的 Climbing 环境，不能只挂模块而遗漏状态资产。
        /// 基础三段为攀爬、攀上和翻越；高翻越与攀爬跳跃只有填写状态名时才是必需项。
        /// </summary>
        private static bool ValidateClimbingStateContract(Entity entity, out string error)
        {
            if (entity == null || entity.basicDomain == null || entity.basicDomain.MyModules == null
                || entity.basicDomain.MyModules.ValuesNow == null)
            {
                error = string.Empty;
                return true;
            }

            int count = entity.basicDomain.MyModules.ValuesNow.Count;
            for (int i = 0; i < count; i++)
            {
                if (!(entity.basicDomain.MyModules.ValuesNow[i] is EntityBasicClimbModule climb)
                    || !climb.enableClimb)
                    continue;

                if (!climb.disableNormalClimb
                    && !ValidateClimbingState(entity.stateDomain, climb.Climb_StateName, "攀爬", out error))
                    return false;
                if (!climb.disableClimbOver
                    && !ValidateClimbingState(entity.stateDomain, climb.ClimbOver_StateName, "攀爬翻上", out error))
                    return false;
                if (!climb.disableVault
                    && !ValidateClimbingState(entity.stateDomain, climb.Vault_StateName, "翻越", out error))
                    return false;
                if (!climb.disableVault && !string.IsNullOrWhiteSpace(climb.VaultHigh_StateName)
                    && !ValidateClimbingState(entity.stateDomain, climb.VaultHigh_StateName, "高翻越", out error))
                    return false;
                if (!climb.disableClimbJump && !string.IsNullOrWhiteSpace(climb.ClimbJump_StateName)
                    && !ValidateClimbJumpState(entity.stateDomain, climb.ClimbJump_StateName, out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateClimbingState(
            EntityStateDomain domain,
            string stateName,
            string actionName,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                error = actionName + " 未配置状态名。";
                return false;
            }

            StateBasicConfig config = FindStateConfig(domain, stateName);
            if (config == null)
            {
                error = "未在角色状态包中找到" + actionName + "状态 '" + stateName + "'。";
                return false;
            }

            if (!EntityBasicClimbModule.ValidateClimbingStateConfig(config, out error))
            {
                error = actionName + "状态 '" + stateName + "' 配置不合格：" + error;
                return false;
            }

            return true;
        }

        private static bool ValidateClimbJumpState(EntityStateDomain domain, string stateName, out string error)
        {
            StateBasicConfig config = FindStateConfig(domain, stateName);
            if (config == null)
            {
                error = "未在角色状态包中找到攀爬跳跃状态 '" + stateName + "'。";
                return false;
            }

            if (!EntityBasicClimbModule.ValidateClimbJumpStateConfig(config, out error))
            {
                error = "攀爬跳跃状态 '" + stateName + "' 配置不合格：" + error;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 骑乘是状态环境切换，不是输入模块的局部开关。这里直接读取公开的状态数据资产，
        /// 不依赖运行时初始化，也不读取任何私有序列化字段。
        /// </summary>
        private static bool ValidateMountedStateContract(Entity entity, out string error)
        {
            if (entity == null || entity.basicDomain == null || entity.basicDomain.MyModules == null
                || entity.basicDomain.MyModules.ValuesNow == null)
            {
                error = string.Empty;
                return true;
            }

            int count = entity.basicDomain.MyModules.ValuesNow.Count;
            bool hasMountModule = false;
            for (int i = 0; i < count; i++)
            {
                if (!(entity.basicDomain.MyModules.ValuesNow[i] is EntityBasicMountModule mountModule))
                    continue;

                hasMountModule = true;

                StateBasicConfig config = FindStateConfig(entity.stateDomain, mountModule.Mount_StateName);
                if (config == null)
                {
                    error = "未在角色状态包中找到骑乘状态 '" + mountModule.Mount_StateName + "'。";
                    return false;
                }

                if (!EntityBasicMountModule.ValidateMountedStateConfig(config, out error))
                    return false;
            }

            if (hasMountModule && !ValidateMountedActionExitPolicy(entity.stateDomain, out error))
                return false;

            error = string.Empty;
            return true;
        }

        private static bool ValidateMountedActionExitPolicy(EntityStateDomain domain, out string error)
        {
            var visited = new HashSet<StateAniDataPack>();
            if (!ValidateMountedActionExitPolicy(domain != null ? domain.stateAniDataPack : null, visited, out error)
                || !ValidateMountedActionExitPolicy(domain != null ? domain.gunStateAniDataPack : null, visited, out error))
                return false;

            if (domain != null && domain.additionalStateAniDataPacks != null)
            {
                for (int i = 0; i < domain.additionalStateAniDataPacks.Count; i++)
                {
                    if (!ValidateMountedActionExitPolicy(domain.additionalStateAniDataPacks[i], visited, out error))
                        return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateMountedActionExitPolicy(
            StateAniDataPack pack,
            HashSet<StateAniDataPack> visited,
            out string error)
        {
            if (pack == null || !visited.Add(pack) || pack.Infos == null)
            {
                error = string.Empty;
                return true;
            }

            foreach (StateAniDataInfo info in pack.Infos.Values)
            {
                StateBasicConfig config = info != null && info.sharedData != null ? info.sharedData.basicConfig : null;
                if (config == null)
                    continue;

                if (!EntityBasicMountModule.ValidateMountedActionConfig(config, out error))
                    return false;
            }

            error = string.Empty;
            return true;
        }

        private static StateBasicConfig FindStateConfig(EntityStateDomain domain, string stateName)
        {
            if (domain == null || string.IsNullOrWhiteSpace(stateName))
                return null;

            var visited = new HashSet<StateAniDataPack>();
            StateBasicConfig config = FindStateConfig(domain.stateAniDataPack, stateName, visited);
            if (config != null)
                return config;

            config = FindStateConfig(domain.gunStateAniDataPack, stateName, visited);
            if (config != null)
                return config;

            if (domain.additionalStateAniDataPacks == null)
                return null;

            for (int i = 0; i < domain.additionalStateAniDataPacks.Count; i++)
            {
                config = FindStateConfig(domain.additionalStateAniDataPacks[i], stateName, visited);
                if (config != null)
                    return config;
            }

            return null;
        }

        private static StateBasicConfig FindStateConfig(
            StateAniDataPack pack,
            string stateName,
            HashSet<StateAniDataPack> visited)
        {
            if (pack == null || !visited.Add(pack) || pack.Infos == null)
                return null;

            foreach (StateAniDataInfo info in pack.Infos.Values)
            {
                StateBasicConfig config = info != null && info.sharedData != null ? info.sharedData.basicConfig : null;
                if (config != null && string.Equals(config.stateName, stateName, StringComparison.Ordinal))
                    return config;
            }

            return null;
        }
    }
}
