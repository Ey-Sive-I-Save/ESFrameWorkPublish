using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ES.EditorInternal
{
    /// <summary>
    /// One explicit authoring migration step for ESGenericProfile.
    /// Implementations may edit only the supplied SerializedObject. The transaction service
    /// owns Undo, schema advancement, ApplyModifiedProperties, validation, dirty state and rollback.
    /// </summary>
    public interface IESGenericProfileMigrator
    {
        string MigrationId { get; }
        int FromVersion { get; }
        int ToVersion { get; }

        bool TryMigrate(
            ESGenericProfile profile,
            SerializedObject serializedProfile,
            out string error);
    }

    public sealed class ESGenericProfileMigrationReport
    {
        private static readonly IReadOnlyList<string> EmptyOperations =
            Array.AsReadOnly(Array.Empty<string>());

        public bool Success { get; }
        public bool Changed { get; }
        public int MigratedProfileCount { get; }
        public string Error { get; }
        public IReadOnlyList<string> Operations { get; }

        internal ESGenericProfileMigrationReport(
            bool success,
            bool changed,
            int migratedProfileCount,
            string error,
            IReadOnlyList<string> operations)
        {
            Success = success;
            Changed = changed;
            MigratedProfileCount = migratedProfileCount;
            Error = error;
            Operations = operations ?? EmptyOperations;
        }
    }

    /// <summary>
    /// Explicit, user-triggered and atomic Generic Profile authoring migration.
    /// It performs: detect -> build complete chain -> one Undo transaction -> execute -> validate
    /// -> commit, or reverts every selected Profile when any step or validation fails.
    /// </summary>
    public static class ESGenericProfileMigrationService
    {
        private const string UndoName = "迁移 ES Generic Profile 配置";
        private const int MaxMigrationSteps = 32;
        private const string HeaderPath = "header";
        private const string HeaderSchemaVersionPath = "schemaVersion";

        private static readonly ReadOnlyCollection<IESGenericProfileMigrator> BuiltInMigrators =
            Array.AsReadOnly<IESGenericProfileMigrator>(
                new IESGenericProfileMigrator[]
                {
                    new ESGenericProfileUnversionedToV1Migrator()
                });

        public static IReadOnlyList<IESGenericProfileMigrator> DefaultMigrators => BuiltInMigrators;

        public static bool TryMigrate(
            IReadOnlyList<ESGenericProfile> profiles,
            out ESGenericProfileMigrationReport report)
        {
            return TryMigrate(profiles, BuiltInMigrators, out report);
        }

        public static bool TryMigrate(
            IReadOnlyList<ESGenericProfile> profiles,
            IReadOnlyList<IESGenericProfileMigrator> migrators,
            out ESGenericProfileMigrationReport report)
        {
            List<ProfilePlan> plans;
            try
            {
                if (!TryBuildPlans(profiles, migrators, out plans, out string error))
                {
                    report = Failure(error);
                    return false;
                }
            }
            catch (Exception exception)
            {
                report = Failure("迁移预检失败：" + exception.Message);
                return false;
            }

            int migrationCount = 0;
            for (int index = 0; index < plans.Count; index++)
            {
                if (plans[index].Steps.Count > 0)
                    migrationCount++;
            }

            if (migrationCount == 0)
            {
                report = new ESGenericProfileMigrationReport(
                    true,
                    false,
                    0,
                    null,
                    null);
                return true;
            }

            var undoTargets = new UnityEngine.Object[migrationCount];
            var rollbackSnapshots = new ProfileRollbackSnapshot[migrationCount];
            int undoTargetIndex = 0;
            try
            {
                for (int index = 0; index < plans.Count; index++)
                {
                    if (plans[index].Steps.Count == 0)
                        continue;

                    ESGenericProfile profile = plans[index].Profile;
                    undoTargets[undoTargetIndex] = profile;
                    rollbackSnapshots[undoTargetIndex] = ProfileRollbackSnapshot.Capture(profile);
                    undoTargetIndex++;
                }
            }
            catch (Exception exception)
            {
                report = Failure("迁移前状态指纹生成失败：" + exception.Message);
                return false;
            }

            var operations = new List<string>();
            int undoGroup = -1;
            try
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                Undo.RegisterCompleteObjectUndo(undoTargets, UndoName);

                for (int index = 0; index < plans.Count; index++)
                {
                    ProfilePlan plan = plans[index];
                    if (plan.Steps.Count == 0)
                        continue;

                    ExecutePlan(plan, operations);
                    ValidateMigratedProfile(plan.Profile);
                }

                for (int index = 0; index < plans.Count; index++)
                {
                    ProfilePlan plan = plans[index];
                    if (plan.Steps.Count == 0)
                        continue;

                    EditorUtility.SetDirty(plan.Profile);
                    if (PrefabUtility.IsPartOfPrefabInstance(plan.Profile))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(plan.Profile);
                }

                Undo.CollapseUndoOperations(undoGroup);
                report = new ESGenericProfileMigrationReport(
                    true,
                    true,
                    migrationCount,
                    null,
                    operations.AsReadOnly());
                return true;
            }
            catch (Exception exception)
            {
                if (undoGroup >= 0)
                {
                    try
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                    }
                    catch (Exception rollbackException)
                    {
                        report = Failure(
                            exception.Message + "\n回滚失败，Profile 可能仍有修改："
                            + rollbackException.Message,
                            operations,
                            true);
                        return false;
                    }
                }

                if (!TryVerifyRollback(rollbackSnapshots, out string rollbackVerificationError))
                {
                    report = Failure(
                        exception.Message + "\n回滚复核失败，Profile 可能仍有修改："
                        + rollbackVerificationError,
                        operations,
                        true);
                    return false;
                }

                report = Failure(exception.Message, operations, false);
                return false;
            }
        }

        private static void ExecutePlan(ProfilePlan plan, List<string> operations)
        {
            var serializedProfile = new SerializedObject(plan.Profile);
            for (int index = 0; index < plan.Steps.Count; index++)
            {
                IESGenericProfileMigrator migrator = plan.Steps[index];
                serializedProfile.UpdateIfRequiredOrScript();
                if (!migrator.TryMigrate(plan.Profile, serializedProfile, out string error))
                {
                    throw new InvalidOperationException(
                        "Profile “" + plan.Profile.name + "” 迁移器 " + migrator.MigrationId
                        + " 执行失败：" + NormalizeError(error));
                }

                SerializedProperty schemaVersion = FindHeaderSchemaVersion(serializedProfile);
                schemaVersion.intValue = migrator.ToVersion;
                serializedProfile.ApplyModifiedProperties();
                operations.Add(
                    plan.Profile.name + "：" + migrator.MigrationId
                    + " v" + migrator.FromVersion + " → v" + migrator.ToVersion);
            }
        }

        private static void ValidateMigratedProfile(ESGenericProfile profile)
        {
            var issues = new List<string>();
            if (profile.ValidateProfile(issues))
                return;

            throw new InvalidOperationException(
                "Profile “" + profile.name + "” 迁移后校验失败：\n- "
                + string.Join("\n- ", issues));
        }

        private static bool TryBuildPlans(
            IReadOnlyList<ESGenericProfile> profiles,
            IReadOnlyList<IESGenericProfileMigrator> migrators,
            out List<ProfilePlan> plans,
            out string error)
        {
            plans = null;
            error = null;
            if (profiles == null || profiles.Count == 0)
            {
                error = "没有可迁移的 Profile。";
                return false;
            }

            if (!TryValidateEditorMode(
                    EditorApplication.isPlayingOrWillChangePlaymode,
                    out error))
            {
                return false;
            }

            if (!ValidateMigratorSet(migrators, out error))
                return false;

            var uniqueProfiles = new HashSet<ESGenericProfile>();
            plans = new List<ProfilePlan>(profiles.Count);
            for (int index = 0; index < profiles.Count; index++)
            {
                ESGenericProfile profile = profiles[index];
                if (profile == null)
                {
                    error = "迁移目标第 " + (index + 1) + " 项为空。";
                    return false;
                }

                if (!uniqueProfiles.Add(profile))
                {
                    error = "迁移目标重复：" + profile.name + "。";
                    return false;
                }

                if (!TryValidateProfileEditable(profile, out error))
                    return false;

                var serializedProfile = new SerializedObject(profile);
                serializedProfile.UpdateIfRequiredOrScript();
                int sourceVersion = FindHeaderSchemaVersion(serializedProfile).intValue;
                if (!TryBuildPlan(
                        profile,
                        sourceVersion,
                        migrators,
                        out ProfilePlan plan,
                        out error))
                {
                    return false;
                }

                plans.Add(plan);
            }

            return true;
        }

        private static bool TryValidateEditorMode(
            bool isPlayingOrWillChangePlaymode,
            out string error)
        {
            if (isPlayingOrWillChangePlaymode)
            {
                error = "PlayMode 或 PlayMode 切换期间禁止迁移 Profile。";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryValidateProfileEditable(
            ESGenericProfile profile,
            out string error)
        {
            error = null;
            if (EditorUtility.IsPersistent(profile))
            {
                string assetPath = AssetDatabase.GetAssetPath(profile);
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    error = "Profile “" + profile.name + "” 是持久化对象，但无法解析资产路径。";
                    return false;
                }

                if (!AssetDatabase.IsOpenForEdit(
                        assetPath,
                        out string assetMessage,
                        StatusQueryOptions.ForceUpdate))
                {
                    error = "Profile “" + profile.name + "” 所在资产不可编辑："
                            + assetPath + NormalizeEditMessage(assetMessage);
                    return false;
                }
            }

            Scene scene = profile.gameObject.scene;
            if (scene.IsValid()
                && !string.IsNullOrWhiteSpace(scene.path)
                && !AssetDatabase.IsOpenForEdit(
                    scene.path,
                    out string sceneMessage,
                    StatusQueryOptions.ForceUpdate))
            {
                error = "Profile “" + profile.name + "” 所在场景不可编辑："
                        + scene.path + NormalizeEditMessage(sceneMessage);
                return false;
            }

            return true;
        }

        private static string NormalizeEditMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message) ? "。" : "（" + message.Trim() + "）";
        }

        private static bool TryVerifyRollback(
            IReadOnlyList<ProfileRollbackSnapshot> snapshots,
            out string error)
        {
            error = null;
            var mismatches = new List<string>();
            for (int index = 0; index < snapshots.Count; index++)
            {
                ProfileRollbackSnapshot snapshot = snapshots[index];
                if (snapshot == null)
                {
                    mismatches.Add("回滚快照第 " + (index + 1) + " 项为空。");
                    continue;
                }

                if (!snapshot.MatchesCurrentState(out string mismatch))
                    mismatches.Add(snapshot.Profile.name + "：" + mismatch);
            }

            if (mismatches.Count == 0)
                return true;

            error = string.Join("；", mismatches);
            return false;
        }

        private static bool TryBuildPlan(
            ESGenericProfile profile,
            int sourceVersion,
            IReadOnlyList<IESGenericProfileMigrator> migrators,
            out ProfilePlan plan,
            out string error)
        {
            plan = null;
            error = null;
            int targetVersion = ESProfileHeader.CurrentSchemaVersion;
            if (sourceVersion < 0)
            {
                error = "Profile “" + profile.name + "” SchemaVersion 无效：" + sourceVersion + "。";
                return false;
            }

            if (sourceVersion > targetVersion)
            {
                error = "Profile “" + profile.name + "” 使用未来 SchemaVersion "
                        + sourceVersion + "，当前只支持到 " + targetVersion + "。";
                return false;
            }

            var steps = new List<IESGenericProfileMigrator>();
            int currentVersion = sourceVersion;
            while (currentVersion < targetVersion)
            {
                if (steps.Count >= MaxMigrationSteps)
                {
                    error = "Profile “" + profile.name + "” 迁移链超过 "
                            + MaxMigrationSteps + " 步，已阻止执行。";
                    return false;
                }

                IESGenericProfileMigrator selected = null;
                for (int index = 0; index < migrators.Count; index++)
                {
                    IESGenericProfileMigrator candidate = migrators[index];
                    if (candidate.FromVersion != currentVersion)
                        continue;

                    if (selected != null)
                    {
                        error = "Profile Schema v" + currentVersion
                                + " 存在多个迁移器：" + selected.MigrationId
                                + "、" + candidate.MigrationId + "。";
                        return false;
                    }

                    selected = candidate;
                }

                if (selected == null)
                {
                    error = "Profile “" + profile.name + "” 缺少迁移链：v"
                            + currentVersion + " → v" + targetVersion + "。";
                    return false;
                }

                if (selected.ToVersion > targetVersion)
                {
                    error = "迁移器 " + selected.MigrationId + " 会越过当前目标版本 v"
                            + targetVersion + "。";
                    return false;
                }

                steps.Add(selected);
                currentVersion = selected.ToVersion;
            }

            plan = new ProfilePlan(profile, steps);
            return true;
        }

        private static bool ValidateMigratorSet(
            IReadOnlyList<IESGenericProfileMigrator> migrators,
            out string error)
        {
            error = null;
            if (migrators == null)
            {
                error = "Migrator 集合不能为空。";
                return false;
            }

            var migrationIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < migrators.Count; index++)
            {
                IESGenericProfileMigrator migrator = migrators[index];
                if (migrator == null)
                {
                    error = "Migrator 集合第 " + (index + 1) + " 项为空。";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(migrator.MigrationId))
                {
                    error = "Migrator 集合第 " + (index + 1) + " 项缺少稳定 MigrationId。";
                    return false;
                }

                if (!migrationIds.Add(migrator.MigrationId))
                {
                    error = "MigrationId 重复：" + migrator.MigrationId + "。";
                    return false;
                }

                if (migrator.FromVersion < 0 || migrator.ToVersion <= migrator.FromVersion)
                {
                    error = "Migrator " + migrator.MigrationId + " 版本区间无效：v"
                            + migrator.FromVersion + " → v" + migrator.ToVersion + "。";
                    return false;
                }
            }

            return true;
        }

        private static SerializedProperty FindHeaderSchemaVersion(SerializedObject serializedProfile)
        {
            SerializedProperty header = serializedProfile.FindProperty(HeaderPath);
            SerializedProperty schemaVersion = header?.FindPropertyRelative(HeaderSchemaVersionPath);
            if (schemaVersion == null || schemaVersion.propertyType != SerializedPropertyType.Integer)
            {
                throw new InvalidOperationException(
                    "无法定位 ESGenericProfile.header.schemaVersion 序列化字段。");
            }

            return schemaVersion;
        }

        private static ESGenericProfileMigrationReport Failure(
            string error,
            List<string> operations = null,
            bool changed = false)
        {
            return new ESGenericProfileMigrationReport(
                false,
                changed,
                0,
                NormalizeError(error),
                operations?.AsReadOnly());
        }

        private static string NormalizeError(string error)
        {
            return string.IsNullOrWhiteSpace(error) ? "未提供错误信息。" : error.Trim();
        }

        private sealed class ProfilePlan
        {
            public readonly ESGenericProfile Profile;
            public readonly List<IESGenericProfileMigrator> Steps;

            public ProfilePlan(
                ESGenericProfile profile,
                List<IESGenericProfileMigrator> steps)
            {
                Profile = profile;
                Steps = steps;
            }
        }

        private sealed class ProfileRollbackSnapshot
        {
            private readonly string serializedHash;
            private readonly string prefabOverrideHash;
            private readonly bool objectDirty;
            private readonly bool sceneValid;
            private readonly bool sceneDirty;

            public ESGenericProfile Profile { get; }

            private ProfileRollbackSnapshot(
                ESGenericProfile profile,
                string serializedHash,
                string prefabOverrideHash,
                bool objectDirty,
                bool sceneValid,
                bool sceneDirty)
            {
                Profile = profile;
                this.serializedHash = serializedHash;
                this.prefabOverrideHash = prefabOverrideHash;
                this.objectDirty = objectDirty;
                this.sceneValid = sceneValid;
                this.sceneDirty = sceneDirty;
            }

            public static ProfileRollbackSnapshot Capture(ESGenericProfile profile)
            {
                if (profile == null)
                    throw new ArgumentNullException(nameof(profile));

                Scene scene = profile.gameObject.scene;
                bool hasScene = scene.IsValid();
                return new ProfileRollbackSnapshot(
                    profile,
                    CaptureSerializedHash(profile),
                    CapturePrefabOverrideHash(profile),
                    EditorUtility.IsDirty(profile),
                    hasScene,
                    hasScene && scene.isDirty);
            }

            public bool MatchesCurrentState(out string mismatch)
            {
                mismatch = null;
                if (Profile == null)
                {
                    mismatch = "目标对象已丢失。";
                    return false;
                }

                ProfileRollbackSnapshot current = Capture(Profile);
                var differences = new List<string>(4);
                if (!string.Equals(serializedHash, current.serializedHash, StringComparison.Ordinal))
                    differences.Add("序列化内容");
                if (!string.Equals(
                        prefabOverrideHash,
                        current.prefabOverrideHash,
                        StringComparison.Ordinal))
                {
                    differences.Add("Prefab Override");
                }
                if (objectDirty != current.objectDirty)
                    differences.Add("对象 Dirty 状态");
                if (sceneValid != current.sceneValid || sceneDirty != current.sceneDirty)
                    differences.Add("Scene Dirty 状态");

                if (differences.Count == 0)
                    return true;

                mismatch = string.Join("、", differences) + " 未恢复到迁移前状态。";
                return false;
            }

            private static string CaptureSerializedHash(ESGenericProfile profile)
            {
                var builder = new StringBuilder(EditorJsonUtility.ToJson(profile, false));
                var serializedProfile = new SerializedObject(profile);
                serializedProfile.UpdateIfRequiredOrScript();
                SerializedProperty iterator = serializedProfile.GetIterator();
                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    enterChildren = true;
                    if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                    {
                        builder.Append("\nmanaged:")
                            .Append(iterator.propertyPath)
                            .Append('|')
                            .Append(iterator.managedReferenceFullTypename)
                            .Append('|')
                            .Append(iterator.managedReferenceId);
                    }
                    else if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        builder.Append("\nobject:")
                            .Append(iterator.propertyPath)
                            .Append('|')
                            .Append(iterator.objectReferenceInstanceIDValue);
                    }
                }

                return ComputeSha256(builder.ToString());
            }

            private static string CapturePrefabOverrideHash(ESGenericProfile profile)
            {
                PropertyModification[] modifications =
                    PrefabUtility.GetPropertyModifications(profile);
                if (modifications == null || modifications.Length == 0)
                    return ComputeSha256(string.Empty);

                var entries = new List<string>(modifications.Length);
                for (int index = 0; index < modifications.Length; index++)
                {
                    PropertyModification modification = modifications[index];
                    entries.Add(
                        GetObjectIdentity(modification.target) + "|"
                        + modification.propertyPath + "|"
                        + modification.value + "|"
                        + GetObjectIdentity(modification.objectReference));
                }

                entries.Sort(StringComparer.Ordinal);
                return ComputeSha256(string.Join("\n", entries));
            }

            private static string GetObjectIdentity(UnityEngine.Object target)
            {
                return target == null
                    ? "null"
                    : target.GetType().AssemblyQualifiedName + "#" + target.GetInstanceID();
            }

            private static string ComputeSha256(string value)
            {
                using SHA256 sha256 = SHA256.Create();
                byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
                return Convert.ToBase64String(sha256.ComputeHash(bytes));
            }
        }

        private sealed class ESGenericProfileUnversionedToV1Migrator
            : IESGenericProfileMigrator
        {
            public string MigrationId => "es.generic-profile.unversioned-to-v1";
            public int FromVersion => 0;
            public int ToVersion => 1;

            public bool TryMigrate(
                ESGenericProfile profile,
                SerializedObject serializedProfile,
                out string error)
            {
                error = null;
                SerializedProperty settings = serializedProfile.FindProperty("settings");
                SerializedProperty extensions = settings?.FindPropertyRelative("extensions");
                if (extensions == null || !extensions.isArray)
                {
                    error = "无法定位 Generic Profile Extension List。";
                    return false;
                }

                for (int index = 0; index < extensions.arraySize; index++)
                {
                    SerializedProperty element = extensions.GetArrayElementAtIndex(index);
                    if (element == null
                        || element.propertyType != SerializedPropertyType.ManagedReference
                        || element.managedReferenceValue == null)
                    {
                        error = "Extensions[" + index
                                + "] 为空或类型缺失，不能把未版本化 Profile 升级为 V1。";
                        return false;
                    }

                    if (!(element.managedReferenceValue is ESGenericProfileExtensionSettings extension))
                    {
                        error = "Extensions[" + index + "] 不是合法 Generic Profile Extension。";
                        return false;
                    }

                    if (extension.SchemaVersion != extension.SupportedSchemaVersion)
                    {
                        error = extension.TypeId + " 当前 SchemaVersion=" + extension.SchemaVersion
                                + "，要求 " + extension.SupportedSchemaVersion
                                + "；Extension 迁移必须由后续 Profile Registry 提供。";
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
