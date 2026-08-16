using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class ESGameCoreRegistrationAuthoring
    {
        internal static ESContentRegistrationResult Execute(ESContentRegistrationRequest request)
        {
            string sourcePath = ESContentRegistrationAuthoring.NormalizeAssetPath(request.dataInfoPath);
            string groupPath = ESContentRegistrationAuthoring.NormalizeAssetPath(request.groupPath);
            string consumerPath = ESContentRegistrationAuthoring.NormalizeAssetPath(request.consumerPath);
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(groupPath) || string.IsNullOrEmpty(consumerPath))
                return ESContentRegistrationResult.Failure(request, "invalid_request", "dataInfoPath、groupPath、consumerPath 都必须是 Assets/ 下的项目路径。");
            if (!ESContentStringKeyRules.TryValidateStringKey(request.groupKey, out string groupKeyError))
                return ESContentRegistrationResult.Failure(request, "invalid_group_key", "Group 组织 Key 无效：" + groupKeyError);

            ScriptableObject source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(sourcePath);
            ScriptableObject groupAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(groupPath);
            ESAssetLibraryConsumer consumer = AssetDatabase.LoadAssetAtPath<ESAssetLibraryConsumer>(consumerPath);
            if (source == null
                || !(source is ISoDataInfo dataInfo)
                || ESScriptableObjectClassification.GetClass(source) != ESScriptableObjectClass.GameCore)
            {
                return ESContentRegistrationResult.Failure(request, "unsupported_gamecore", "源资产必须是当前分类为 GameCore 的 SoDataInfo：" + sourcePath);
            }
            if (groupAsset == null
                || !(groupAsset is ISoDataGroup group)
                || ESScriptableObjectClassification.GetClass(groupAsset) != ESScriptableObjectClass.GameCore)
            {
                return ESContentRegistrationResult.Failure(request, "unsupported_group", "目标必须是当前分类为 GameCore 的正式 SoDataGroup：" + groupPath);
            }
            if (consumer == null)
                return ESContentRegistrationResult.Failure(request, "not_found", "找不到 ESAssetLibraryConsumer：" + consumerPath);
            if (!group.GetSOInfoType().IsAssignableFrom(source.GetType()))
                return ESContentRegistrationResult.Failure(request, "type_mismatch", "DataInfo 类型不属于目标 Group：" + group.GetSOInfoType().FullName);

            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            string groupGuid = AssetDatabase.AssetPathToGUID(groupPath);
            string consumerGuid = AssetDatabase.AssetPathToGUID(consumerPath);
            bool sourceGuidValid = ESContentRegistrationAuthoring.TryRequireGuid("DataInfo", request.expectedSourceGuid, sourceGuid, request.commit, out string sourceGuidError);
            bool groupGuidValid = ESContentRegistrationAuthoring.TryRequireGuid("Group", request.expectedGroupGuid, groupGuid, request.commit, out string groupGuidError);
            bool consumerGuidValid = ESContentRegistrationAuthoring.TryRequireGuid("Consumer", request.expectedConsumerGuid, consumerGuid, request.commit, out string consumerGuidError);
            if (!sourceGuidValid || !groupGuidValid || !consumerGuidValid)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "identity_conflict",
                    FirstNotEmpty(sourceGuidError, groupGuidError, consumerGuidError));
            }

            string sourceRevision = ESContentRegistrationAuthoring.GetAssetRevision(sourcePath);
            string groupRevision = ESContentRegistrationAuthoring.GetAssetRevision(groupPath);
            string consumerRevision = ESContentRegistrationAuthoring.GetAssetRevision(consumerPath);
            bool sourceRevisionValid = ESContentRegistrationAuthoring.TryRequireRevision("DataInfo", request.expectedSourceRevision, sourceRevision, request.commit, out string sourceRevisionError);
            bool groupRevisionValid = ESContentRegistrationAuthoring.TryRequireRevision("Group", request.expectedGroupRevision, groupRevision, request.commit, out string groupRevisionError);
            bool consumerRevisionValid = ESContentRegistrationAuthoring.TryRequireRevision("Consumer", request.expectedConsumerRevision, consumerRevision, request.commit, out string consumerRevisionError);
            if (!sourceRevisionValid || !groupRevisionValid || !consumerRevisionValid)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "concurrency_conflict",
                    FirstNotEmpty(sourceRevisionError, groupRevisionError, consumerRevisionError));
            }
            bool sourceClean = ESContentRegistrationAuthoring.TryRequireCleanTarget("DataInfo", source, request.commit, out string sourceDirtyError);
            bool groupClean = ESContentRegistrationAuthoring.TryRequireCleanTarget("Group", groupAsset, request.commit, out string groupDirtyError);
            bool consumerClean = ESContentRegistrationAuthoring.TryRequireCleanTarget("Consumer", consumer, request.commit, out string consumerDirtyError);
            if (!sourceClean || !groupClean || !consumerClean)
            {
                return ESContentRegistrationResult.Failure(
                    request,
                    "target_dirty",
                    FirstNotEmpty(sourceDirtyError, groupDirtyError, consumerDirtyError));
            }

            var result = ESContentRegistrationResult.Create(request);
            result.assetPath = sourcePath;
            result.guid = sourceGuid;
            result.sourceGuid = sourceGuid;
            result.groupGuid = groupGuid;
            result.consumerGuid = consumerGuid;
            result.localFileId = 0;
            result.groupKey = request.groupKey;
            result.sourceRevision = sourceRevision;
            result.groupRevision = groupRevision;
            result.consumerRevision = consumerRevision;

            ItemKeyEdit itemEdit = null;
            if (source is ItemDataInfo item)
            {
                if (!TryPrepareItemKeyEdit(item, sourceGuid, request, out itemEdit, out string itemError))
                    return ESContentRegistrationResult.Failure(request, "invalid_gamecore_key", itemError);
                result.enumKey = itemEdit.enumKey;
                result.stringKey = itemEdit.stringKey;
                result.currentEnumKey = itemEdit.oldEnumKey;
                result.currentStringKey = itemEdit.oldStringKey;
                result.itemEnumKey = itemEdit.itemEnumKey;
                result.itemStringKey = itemEdit.itemStringKey;
                result.currentItemEnumKey = itemEdit.oldItemEnumKey;
                result.currentItemStringKey = itemEdit.oldItemStringKey;
                result.assetKind = itemEdit.route;
                if (!ValidateItemWithProposedKey(item, itemEdit, out string validationError))
                    return ESContentRegistrationResult.Failure(request, "definition_invalid", validationError);
                if (!TryValidateItemAssetReferences(item, out string assetReferenceError))
                    return ESContentRegistrationResult.Failure(request, "asset_reference_invalid", assetReferenceError);
                if (!TryValidateItemKeyUniqueness(item, itemEdit, out string uniquenessError))
                    return ESContentRegistrationResult.Failure(request, "key_conflict", uniquenessError);
            }
            else
            {
                if (request.enumKey != 0 || !string.IsNullOrEmpty(request.stringKey)
                    || request.keyMode != ESContentStableKeyMode.Auto)
                {
                    return ESContentRegistrationResult.Failure(
                        request,
                        "adapter_required",
                        "该 GameCore 类型尚无强类型 Key Adapter；注册器不会用反射猜字段或写入请求中的 Key。");
                }
                result.warnings.Add("该类型仅执行已有定义的 Group/Consumer 注册；领域 StringKey 仍由其强类型作者入口负责。");
            }

            if (!TryValidateGroupMembership(groupAsset, group, source, dataInfo, request.groupKey, out bool groupAlreadyLinked, out string groupError))
                return ESContentRegistrationResult.Failure(request, "group_conflict", groupError);
            bool consumerAlreadyLinked = ConsumerContainsIdentity(consumer, groupGuid, 0);
            bool sourceNeedsKeyChange = itemEdit != null && itemEdit.RequiresChange;
            bool sourceNeedsGroupKey = !string.Equals(dataInfo.GetKey(), request.groupKey, StringComparison.Ordinal);
            bool wouldChange = sourceNeedsKeyChange || sourceNeedsGroupKey || !groupAlreadyLinked || !consumerAlreadyLinked;

            if (!request.commit)
            {
                result.success = true;
                result.changed = wouldChange;
                result.status = wouldChange ? "validated" : "already_registered";
                result.idempotent = !wouldChange;
                result.message = wouldChange
                    ? "GameCore 预检通过；commit 将分阶段写入 DataInfo、Group 与 Consumer，并重建 Consumer 快照。"
                    : "DataInfo、Group、Consumer 与稳定 Key 已一致。";
                return result;
            }

            if (!RevisionsStillMatch(sourcePath, sourceRevision, groupPath, groupRevision, consumerPath, consumerRevision))
                return ESContentRegistrationResult.Failure(request, "concurrency_conflict", "GameCore 目标在预检后发生变化，拒绝写入。");

            if (!wouldChange)
            {
                result.success = true;
                result.idempotent = true;
                result.dryRun = false;
                result.status = "already_registered";
                result.message = "GameCore 注册已存在且完全一致。";
                return result;
            }

            string oldGroupKey = dataInfo.GetKey();
            List<ESAssetReferBase> oldGameCoreAssets = consumer.GameCoreAssets != null
                ? new List<ESAssetReferBase>(consumer.GameCoreAssets)
                : null;
            List<string> oldValidationErrors = consumer.GameCoreValidationErrors != null
                ? new List<string>(consumer.GameCoreValidationErrors)
                : null;
            string oldConsumerId = consumer.ConsumerId;
            bool groupAdded = false;
            bool manualAdded = false;
            try
            {
                Undo.RecordObjects(new UnityEngine.Object[] { source, groupAsset, consumer }, "Register ES GameCore Content");
                itemEdit?.Apply();
                if (!string.Equals(oldGroupKey, request.groupKey, StringComparison.Ordinal))
                    dataInfo.SetKey(request.groupKey);
                if (!groupAlreadyLinked)
                {
                    group._TryAddInfoToDic(request.groupKey, source);
                    groupAdded = ReferenceEquals(group.GetInfoByKey(request.groupKey), dataInfo);
                    if (!groupAdded)
                        throw new InvalidOperationException("Group 拒绝新增 DataInfo；可能存在并发 Key 冲突。");
                }

                if (!consumerAlreadyLinked)
                {
                    int before = consumer.ManualGameCoreAssets?.Count ?? 0;
                    if (!ESAssetConsumerReferenceAuthoring.TryAddManualGameCoreAsset(consumer, groupAsset))
                        throw new InvalidOperationException("Consumer 拒绝添加 GameCore Group 根引用。");
                    manualAdded = (consumer.ManualGameCoreAssets?.Count ?? 0) > before;
                }

                List<ESAssetLibrary> libraries = ESEditorSO.GetGroupOfType<ESAssetLibrary>()
                    ?.Where(entry => entry != null).ToList() ?? new List<ESAssetLibrary>();
                List<string> errors = ESAssetReferenceBaker.BuildConsumerGameCoreSnapshot(
                    consumer,
                    libraries,
                    out List<ESAssetReferBase> generated);
                if (errors.Count > 0)
                    throw new InvalidOperationException("Consumer GameCore 闭包验证失败：\n" + string.Join("\n", errors));

                consumer.GameCoreAssets = generated
                    .Where(entry => entry != null && entry.IsValid)
                    .GroupBy(entry => entry.AssetIdentity)
                    .Select(entries => entries.First())
                    .ToList();
                consumer.GameCoreValidationErrors = new List<string>();
                consumer.EnsureStableIdentity();

                EditorUtility.SetDirty(source);
                EditorUtility.SetDirty(groupAsset);
                EditorUtility.SetDirty(consumer);
                AssetDatabase.SaveAssetIfDirty(source);
                AssetDatabase.SaveAssetIfDirty(groupAsset);
                AssetDatabase.SaveAssetIfDirty(consumer);

                if (!ReferenceEquals(group.GetInfoByKey(request.groupKey), dataInfo)
                    || !ConsumerContainsIdentity(consumer, groupGuid, 0))
                {
                    throw new InvalidOperationException("GameCore 写入后的 Group/Consumer 后置条件失败；可使用同一请求重新预检后重试。");
                }
            }
            catch (Exception exception)
            {
                itemEdit?.Restore();
                dataInfo.SetKey(oldGroupKey);
                if (groupAdded)
                    group._RemoveInfoFromDic(request.groupKey);
                if (manualAdded)
                    RemoveConsumerManualIdentity(consumer, groupGuid, 0);
                consumer.GameCoreAssets = oldGameCoreAssets;
                consumer.GameCoreValidationErrors = oldValidationErrors;
                consumer.ConsumerId = oldConsumerId;
                EditorUtility.SetDirty(source);
                EditorUtility.SetDirty(groupAsset);
                EditorUtility.SetDirty(consumer);
                string rollbackError = TryPersistRollback(source, groupAsset, consumer);
                string message = exception.Message;
                if (!string.IsNullOrEmpty(rollbackError))
                    message += " 回滚落盘失败：" + rollbackError;
                return ESContentRegistrationResult.Failure(request, "commit_failed", message);
            }

            result.success = true;
            result.changed = true;
            result.dryRun = false;
            result.status = "committed";
            result.sourceRevision = ESContentRegistrationAuthoring.GetAssetRevision(sourcePath);
            result.groupRevision = ESContentRegistrationAuthoring.GetAssetRevision(groupPath);
            result.consumerRevision = ESContentRegistrationAuthoring.GetAssetRevision(consumerPath);
            result.changedPaths.Add(sourcePath);
            result.changedPaths.Add(groupPath);
            result.changedPaths.Add(consumerPath);
            result.message = "GameCore 已接入正式 Group 与 Consumer 快照；RuntimeTable 注入仍需通过启动/PlayMode 验证。";
            return result;
        }

        private static bool TryPrepareItemKeyEdit(
            ItemDataInfo item,
            string sourceGuid,
            ESContentRegistrationRequest request,
            out ItemKeyEdit edit,
            out string error)
        {
            edit = null;
            ESItemConfigKey currentItemKey = item.itemKey;
            if (currentItemKey == null)
            {
                error = "Item 缺少基础 ESItemConfigKey 实例。";
                return false;
            }

            string route;
            IESConfigKey currentProjectionKey = null;
            if (item.kindData is ItemWeaponDataBlock weapon)
            {
                route = "item.weapon";
                currentProjectionKey = weapon.key;
            }
            else if (item.kindData is ItemShotDataBlock shot)
            {
                route = "item.shot";
                currentProjectionKey = shot.key;
            }
            else
            {
                route = "item";
            }

            if (route != "item" && currentProjectionKey == null)
            {
                error = route + " 缺少专项强类型 ConfigKey 实例。";
                return false;
            }

            if (!RouteMatches(request.gameCoreRoute, route))
            {
                error = "gameCoreRoute 与 Item 类型不一致，期望 " + route + "。";
                return false;
            }

            int desiredItemEnum = route == "item" ? request.enumKey : request.itemEnumKey;
            string desiredItemString = route == "item" ? request.stringKey : request.itemStringKey;
            bool hasExpectedItemKey = route == "item" ? request.hasExpectedCurrentKey : request.hasExpectedCurrentItemKey;
            int expectedItemEnum = route == "item" ? request.expectedCurrentEnumKey : request.expectedCurrentItemEnumKey;
            string expectedItemString = route == "item" ? request.expectedCurrentStringKey : request.expectedCurrentItemStringKey;
            if (!TryResolveDesiredKey(
                    currentItemKey,
                    desiredItemEnum,
                    desiredItemString,
                    request.keyMode,
                    hasExpectedItemKey,
                    expectedItemEnum,
                    expectedItemString,
                    "基础 Item",
                    out desiredItemEnum,
                    out desiredItemString,
                    out error))
            {
                return false;
            }

            int desiredProjectionEnum = 0;
            string desiredProjectionString = string.Empty;
            if (currentProjectionKey != null
                && !TryResolveDesiredKey(
                    currentProjectionKey,
                    request.enumKey,
                    request.stringKey,
                    request.keyMode,
                    request.hasExpectedCurrentKey,
                    request.expectedCurrentEnumKey,
                    request.expectedCurrentStringKey,
                    route,
                    out desiredProjectionEnum,
                    out desiredProjectionString,
                    out error))
            {
                return false;
            }

            if (route == "item.weapon")
                edit = ItemKeyEdit.ForWeapon(currentItemKey, (ESWeaponConfigKey)currentProjectionKey, desiredItemEnum, desiredItemString, desiredProjectionEnum, desiredProjectionString, sourceGuid, item.GetType().FullName);
            else if (route == "item.shot")
                edit = ItemKeyEdit.ForShot(currentItemKey, (ESShotConfigKey)currentProjectionKey, desiredItemEnum, desiredItemString, desiredProjectionEnum, desiredProjectionString, sourceGuid, item.GetType().FullName);
            else
                edit = ItemKeyEdit.ForItem(currentItemKey, desiredItemEnum, desiredItemString, sourceGuid, item.GetType().FullName);

            if (edit.HasForeignDefinitionGuid(sourceGuid, out string foreignRoute))
            {
                error = foreignRoute + " ConfigKey.definitionGuid 已指向另一 DataInfo；拒绝覆盖定义身份。";
                edit = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryResolveDesiredKey(
            IESConfigKey current,
            int requestedEnum,
            string requestedString,
            ESContentStableKeyMode keyMode,
            bool hasExpectedCurrent,
            int expectedCurrentEnum,
            string expectedCurrentString,
            string label,
            out int desiredEnum,
            out string desiredString,
            out string error)
        {
            desiredEnum = requestedEnum;
            desiredString = requestedString ?? string.Empty;
            if (desiredEnum == 0 && string.IsNullOrEmpty(desiredString) && current.IsConfigured())
            {
                desiredEnum = current.EnumKeyInt;
                desiredString = current.StringKey ?? string.Empty;
            }
            if (!ESContentStringKeyRules.TryValidateStableKey(keyMode, desiredEnum, desiredString, out _, out error))
                return false;
            if (hasExpectedCurrent
                && (current.EnumKeyInt != expectedCurrentEnum
                    || !string.Equals(current.StringKey ?? string.Empty, expectedCurrentString ?? string.Empty, StringComparison.Ordinal)))
            {
                error = label + " 当前 Key 与 expectedCurrent 不一致。";
                return false;
            }
            if (current.IsConfigured()
                && (current.EnumKeyInt != desiredEnum
                    || !string.Equals(current.StringKey ?? string.Empty, desiredString, StringComparison.Ordinal)))
            {
                error = label + " 已有不同稳定 Key；注册入口禁止承担 Key 改名/迁移。";
                return false;
            }
            return true;
        }

        private static bool ValidateItemWithProposedKey(ItemDataInfo item, ItemKeyEdit edit, out string error)
        {
            edit.Apply();
            try
            {
                ESItemDataValidationCode code = item.ValidateConfiguration(includeEditorMetadata: false);
                if (code != ESItemDataValidationCode.Valid)
                {
                    error = "Item GameCore 定义无效：" + item.GetValidationMessage(code);
                    return false;
                }
                error = string.Empty;
                return true;
            }
            finally
            {
                edit.Restore();
            }
        }

        private static bool TryValidateItemAssetReferences(ItemDataInfo item, out string error)
        {
            ItemBaseConfig baseConfig = item?.baseConfig;
            if (baseConfig == null)
            {
                error = "Item 缺少 BaseConfig。";
                return false;
            }

            ESAssetReferPrefabConfigKey prefabKey = baseConfig.prefabKey;
            bool requiresPrefab = baseConfig.kind == ItemKind.Weapon || baseConfig.kind == ItemKind.Shot;
            if (!ESAssetRegistrationAuthoring.TryValidateRegisteredAssetReference(
                    "Item Prefab",
                    ESAssetReferKind.Prefab,
                    prefabKey?.EnumKeyInt ?? 0,
                    prefabKey?.StringKey,
                    prefabKey?.guid,
                    prefabKey?.localFileId ?? 0,
                    requiresPrefab,
                    out error))
            {
                return false;
            }

            ESAssetReferSpriteConfigKey iconKey = baseConfig.iconKey;
            return ESAssetRegistrationAuthoring.TryValidateRegisteredAssetReference(
                "Item Icon",
                ESAssetReferKind.Sprite,
                iconKey?.EnumKeyInt ?? 0,
                iconKey?.StringKey,
                iconKey?.guid,
                iconKey?.localFileId ?? 0,
                false,
                out error);
        }

        private static bool TryValidateItemKeyUniqueness(ItemDataInfo source, ItemKeyEdit edit, out string error)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(ItemDataInfo)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ItemDataInfo other = AssetDatabase.LoadAssetAtPath<ItemDataInfo>(path);
                if (other == null || ReferenceEquals(other, source))
                    continue;

                ESItemConfigKey otherItemKey = other.itemKey;
                if (otherItemKey != null)
                {
                    if (edit.itemEnumKey != 0 && otherItemKey.EnumKeyInt == edit.itemEnumKey)
                    {
                        error = "基础 Item EnumKey 已由其他定义占用：" + path;
                        return false;
                    }
                    if (!string.IsNullOrEmpty(edit.itemStringKey)
                        && string.Equals(otherItemKey.StringKey, edit.itemStringKey, StringComparison.Ordinal))
                    {
                        error = "基础 Item StringKey 已由其他定义占用：" + path;
                        return false;
                    }
                }

                if (!edit.HasProjection
                    || !other.TryGetGameCoreKey(out IESConfigKey otherKey)
                    || otherKey == null)
                {
                    continue;
                }
                bool sameRoute = (source.kindData is ItemWeaponDataBlock && other.kindData is ItemWeaponDataBlock)
                                 || (source.kindData is ItemShotDataBlock && other.kindData is ItemShotDataBlock);
                if (!sameRoute)
                    continue;
                if (edit.enumKey != 0 && otherKey.EnumKeyInt == edit.enumKey)
                {
                    error = "GameCore EnumKey 已由其他定义占用：" + path;
                    return false;
                }
                if (!string.IsNullOrEmpty(edit.stringKey)
                    && string.Equals(otherKey.StringKey, edit.stringKey, StringComparison.Ordinal))
                {
                    error = "GameCore StringKey 已由其他定义占用：" + path;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateGroupMembership(
            ScriptableObject targetGroupAsset,
            ISoDataGroup targetGroup,
            ScriptableObject source,
            ISoDataInfo dataInfo,
            string groupKey,
            out bool alreadyLinked,
            out string error)
        {
            alreadyLinked = false;
            ISoDataInfo existing = targetGroup.GetInfoByKey(groupKey);
            if (existing != null && !ReferenceEquals(existing, dataInfo))
            {
                error = "目标 GroupKey 已由另一 DataInfo 占用。";
                return false;
            }
            foreach (string key in targetGroup.AllKeys ?? new List<string>())
            {
                if (ReferenceEquals(targetGroup.GetInfoByKey(key), dataInfo))
                {
                    if (!string.Equals(key, groupKey, StringComparison.Ordinal))
                    {
                        error = "DataInfo 已在目标 Group 的另一组织 Key 下；注册入口禁止静默重命名。";
                        return false;
                    }
                    alreadyLinked = true;
                }
            }
            string currentGroupKey = dataInfo.GetKey() ?? string.Empty;
            if (!string.IsNullOrEmpty(currentGroupKey) && !string.Equals(currentGroupKey, groupKey, StringComparison.Ordinal))
            {
                error = "DataInfo.KeyName 已是其他组织 Key；它不是 Runtime StringKey，仍需独立迁移。";
                return false;
            }

            string groupTypeFilter = "t:" + targetGroupAsset.GetType().Name;
            foreach (string guid in AssetDatabase.FindAssets(groupTypeFilter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ScriptableObject otherAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (otherAsset == null || ReferenceEquals(otherAsset, targetGroupAsset) || !(otherAsset is ISoDataGroup otherGroup))
                    continue;
                foreach (ISoDataInfo otherInfo in otherGroup.AllInfos ?? Array.Empty<ISoDataInfo>())
                {
                    if (ReferenceEquals(otherInfo, dataInfo))
                    {
                        error = "DataInfo 已属于另一正式 Group：" + path;
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool ConsumerContainsIdentity(ESAssetLibraryConsumer consumer, string guid, long localFileId)
        {
            IEnumerable<ESAssetReferBase> all = (consumer.GameCoreAssets ?? new List<ESAssetReferBase>())
                .Concat(consumer.ManualGameCoreAssets ?? new List<ESAssetReferBase>());
            return all.Any(entry => entry != null
                                    && string.Equals(entry.GUID, guid, StringComparison.OrdinalIgnoreCase)
                                    && entry.LocalFileId == localFileId);
        }

        private static void RemoveConsumerManualIdentity(ESAssetLibraryConsumer consumer, string guid, long localFileId)
        {
            consumer.ManualGameCoreAssets?.RemoveAll(entry => entry != null
                && string.Equals(entry.GUID, guid, StringComparison.OrdinalIgnoreCase)
                && entry.LocalFileId == localFileId);
        }

        private static string TryPersistRollback(params UnityEngine.Object[] targets)
        {
            var errors = new List<string>();
            foreach (UnityEngine.Object target in targets)
            {
                try
                {
                    AssetDatabase.SaveAssetIfDirty(target);
                }
                catch (Exception exception)
                {
                    errors.Add((target != null ? target.name : "<null>") + "：" + exception.Message);
                }
            }
            return string.Join("；", errors);
        }

        private static bool RevisionsStillMatch(
            string sourcePath,
            string sourceRevision,
            string groupPath,
            string groupRevision,
            string consumerPath,
            string consumerRevision)
        {
            return string.Equals(ESContentRegistrationAuthoring.GetAssetRevision(sourcePath), sourceRevision, StringComparison.Ordinal)
                && string.Equals(ESContentRegistrationAuthoring.GetAssetRevision(groupPath), groupRevision, StringComparison.Ordinal)
                && string.Equals(ESContentRegistrationAuthoring.GetAssetRevision(consumerPath), consumerRevision, StringComparison.Ordinal);
        }

        private static bool RouteMatches(string requested, string actual)
            => string.IsNullOrWhiteSpace(requested)
               || string.Equals(requested, "auto", StringComparison.OrdinalIgnoreCase)
               || string.Equals(requested, actual, StringComparison.OrdinalIgnoreCase);

        private static string FirstNotEmpty(params string[] values)
            => values.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? string.Empty;

        private sealed class ItemKeyEdit
        {
            private readonly ESItemConfigKey itemKey;
            private readonly ESWeaponConfigKey weaponKey;
            private readonly ESShotConfigKey shotKey;
            public readonly int enumKey;
            public readonly string stringKey;
            public readonly int itemEnumKey;
            public readonly string itemStringKey;
            public readonly string route;
            public readonly int oldEnumKey;
            public readonly string oldStringKey;
            public readonly int oldItemEnumKey;
            public readonly string oldItemStringKey;
            private readonly string oldItemDefinitionGuid;
            private readonly long oldItemDefinitionLocalFileId;
            private readonly string oldItemDefinitionTypeName;
            private readonly string oldProjectionDefinitionGuid;
            private readonly long oldProjectionDefinitionLocalFileId;
            private readonly string oldProjectionDefinitionTypeName;
            private readonly string definitionGuid;
            private readonly string definitionTypeName;

            public bool HasProjection => weaponKey != null || shotKey != null;

            public bool RequiresChange => oldItemEnumKey != itemEnumKey
                                          || !string.Equals(oldItemStringKey, itemStringKey, StringComparison.Ordinal)
                                          || !string.Equals(oldItemDefinitionGuid, definitionGuid, StringComparison.OrdinalIgnoreCase)
                                          || oldItemDefinitionLocalFileId != 0
                                          || !string.Equals(oldItemDefinitionTypeName, definitionTypeName, StringComparison.Ordinal)
                                          || (HasProjection
                                              && (oldEnumKey != enumKey
                                                  || !string.Equals(oldStringKey, stringKey, StringComparison.Ordinal)
                                                  || !string.Equals(oldProjectionDefinitionGuid, definitionGuid, StringComparison.OrdinalIgnoreCase)
                                                  || oldProjectionDefinitionLocalFileId != 0
                                                  || !string.Equals(oldProjectionDefinitionTypeName, definitionTypeName, StringComparison.Ordinal)));

            private ItemKeyEdit(
                ESItemConfigKey itemKey,
                ESWeaponConfigKey weaponKey,
                ESShotConfigKey shotKey,
                int itemEnumKey,
                string itemStringKey,
                int enumKey,
                string stringKey,
                string route,
                string definitionGuid,
                string definitionTypeName)
            {
                this.itemKey = itemKey;
                this.weaponKey = weaponKey;
                this.shotKey = shotKey;
                this.itemEnumKey = itemEnumKey;
                this.itemStringKey = itemStringKey ?? string.Empty;
                this.enumKey = enumKey;
                this.stringKey = stringKey ?? string.Empty;
                this.route = route;
                this.definitionGuid = definitionGuid;
                this.definitionTypeName = definitionTypeName;
                oldItemEnumKey = itemKey.EnumKeyInt;
                oldItemStringKey = itemKey.StringKey ?? string.Empty;
                oldItemDefinitionGuid = itemKey.definitionGuid;
                oldItemDefinitionLocalFileId = itemKey.definitionLocalFileId;
                oldItemDefinitionTypeName = itemKey.definitionTypeName;

                IESConfigKey projectionKey = (IESConfigKey)weaponKey ?? shotKey;
                oldEnumKey = projectionKey?.EnumKeyInt ?? itemKey.EnumKeyInt;
                oldStringKey = projectionKey?.StringKey ?? itemKey.StringKey ?? string.Empty;
                oldProjectionDefinitionGuid = weaponKey != null ? weaponKey.definitionGuid : shotKey?.definitionGuid;
                oldProjectionDefinitionLocalFileId = weaponKey != null ? weaponKey.definitionLocalFileId : shotKey?.definitionLocalFileId ?? 0;
                oldProjectionDefinitionTypeName = weaponKey != null ? weaponKey.definitionTypeName : shotKey?.definitionTypeName;
            }

            public static ItemKeyEdit ForItem(ESItemConfigKey itemKey, int enumKey, string stringKey, string guid, string typeName)
                => new ItemKeyEdit(itemKey, null, null, enumKey, stringKey, enumKey, stringKey, "item", guid, typeName);

            public static ItemKeyEdit ForWeapon(ESItemConfigKey itemKey, ESWeaponConfigKey key, int itemEnumKey, string itemStringKey, int enumKey, string stringKey, string guid, string typeName)
                => new ItemKeyEdit(itemKey, key, null, itemEnumKey, itemStringKey, enumKey, stringKey, "item.weapon", guid, typeName);

            public static ItemKeyEdit ForShot(ESItemConfigKey itemKey, ESShotConfigKey key, int itemEnumKey, string itemStringKey, int enumKey, string stringKey, string guid, string typeName)
                => new ItemKeyEdit(itemKey, null, key, itemEnumKey, itemStringKey, enumKey, stringKey, "item.shot", guid, typeName);

            public bool HasForeignDefinitionGuid(string expectedGuid, out string foreignRoute)
            {
                if (!string.IsNullOrEmpty(oldItemDefinitionGuid)
                    && !string.Equals(oldItemDefinitionGuid, expectedGuid, StringComparison.OrdinalIgnoreCase))
                {
                    foreignRoute = "基础 Item";
                    return true;
                }
                if (HasProjection
                    && !string.IsNullOrEmpty(oldProjectionDefinitionGuid)
                    && !string.Equals(oldProjectionDefinitionGuid, expectedGuid, StringComparison.OrdinalIgnoreCase))
                {
                    foreignRoute = route;
                    return true;
                }
                foreignRoute = string.Empty;
                return false;
            }

            public void Apply()
            {
                itemKey.enumKey = (ESItemEnumKey)(ushort)itemEnumKey;
                itemKey.stringKey = itemStringKey;
                itemKey.definitionGuid = definitionGuid;
                itemKey.definitionLocalFileId = 0;
                itemKey.definitionTypeName = definitionTypeName;

                if (weaponKey != null)
                {
                    weaponKey.enumKey = (ESWeaponEnumKey)(ushort)enumKey;
                    weaponKey.stringKey = stringKey;
                    weaponKey.definitionGuid = definitionGuid;
                    weaponKey.definitionLocalFileId = 0;
                    weaponKey.definitionTypeName = definitionTypeName;
                }
                else if (shotKey != null)
                {
                    shotKey.enumKey = (ESShotEnumKey)(ushort)enumKey;
                    shotKey.stringKey = stringKey;
                    shotKey.definitionGuid = definitionGuid;
                    shotKey.definitionLocalFileId = 0;
                    shotKey.definitionTypeName = definitionTypeName;
                }
            }

            public void Restore()
            {
                itemKey.enumKey = (ESItemEnumKey)(ushort)oldItemEnumKey;
                itemKey.stringKey = oldItemStringKey;
                itemKey.definitionGuid = oldItemDefinitionGuid;
                itemKey.definitionLocalFileId = oldItemDefinitionLocalFileId;
                itemKey.definitionTypeName = oldItemDefinitionTypeName;

                if (weaponKey != null)
                {
                    weaponKey.enumKey = (ESWeaponEnumKey)(ushort)oldEnumKey;
                    weaponKey.stringKey = oldStringKey;
                    weaponKey.definitionGuid = oldProjectionDefinitionGuid;
                    weaponKey.definitionLocalFileId = oldProjectionDefinitionLocalFileId;
                    weaponKey.definitionTypeName = oldProjectionDefinitionTypeName;
                }
                else if (shotKey != null)
                {
                    shotKey.enumKey = (ESShotEnumKey)(ushort)oldEnumKey;
                    shotKey.stringKey = oldStringKey;
                    shotKey.definitionGuid = oldProjectionDefinitionGuid;
                    shotKey.definitionLocalFileId = oldProjectionDefinitionLocalFileId;
                    shotKey.definitionTypeName = oldProjectionDefinitionTypeName;
                }
            }
        }
    }

    internal static class ESContentRegistrationConfigKeyExtensions
    {
        public static bool IsConfigured(this IESConfigKey key)
            => key != null && ESConfigKeyMatch.IsConfigured(key.EnumKeyInt, key.StringKey);
    }
}
