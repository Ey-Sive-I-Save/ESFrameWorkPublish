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
                EntityCharacterProfile profile = entity != null ? entity.GetComponent<EntityCharacterProfile>() : null;
                if (profile == null)
                {
                    errors.Add(context + "中的 Entity 缺少根 EntityCharacterProfile：" + prefabPath);
                    continue;
                }

                switch (profile.prefabRole)
                {
                    case EntityCharacterPrefabRole.CharacterVariant:
                        if (!profile.ValidateFormalCharacter(out string formalError))
                            errors.Add(context + "中的正式角色 Prefab 不合格：" + prefabPath + " | " + formalError);
                        else if (!ValidateFormalCharacterPhysics(entity, out string physicsError))
                            errors.Add(context + "中的正式角色物理/挂点配置不合格：" + prefabPath + " | " + physicsError);
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
    }
}
