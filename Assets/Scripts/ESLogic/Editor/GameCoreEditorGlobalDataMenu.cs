using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class GameCoreEditorGlobalDataMenu
    {
        private const string AssetFolder = "Assets/ESNormalAssets/Data/GlobalData/GameCore";
        private const string AssetPath = AssetFolder + "/GameCoreEditorGlobalData.asset";
        [MenuItem("【ES】/项目设置/GameCore/打开或创建GameCore编辑器全局数据", priority = 20)]
        public static void OpenOrCreateGameCoreEditorGlobalData()
        {
            GameCoreEditorGlobalData data = AssetDatabase.LoadAssetAtPath<GameCoreEditorGlobalData>(AssetPath);
            if (data == null)
                data = CreateGameCoreEditorGlobalData();

            if (data == null)
                return;

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        [MenuItem("【ES】/项目设置/GameCore/重置GameCore编辑器推荐规则", priority = 21)]
        public static void ResetGameCoreEditorDefaultRules()
        {
            GameCoreEditorGlobalData data = AssetDatabase.LoadAssetAtPath<GameCoreEditorGlobalData>(AssetPath);
            if (data == null)
                data = CreateGameCoreEditorGlobalData();

            if (data == null)
                return;

            data.ResetDefaultRules();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        [MenuItem("【ES】/项目设置/GameCore/补齐缺失的GameTag规则", priority = 22)]
        public static void EnsureGameTagRules()
        {
            GameCoreEditorGlobalData data = AssetDatabase.LoadAssetAtPath<GameCoreEditorGlobalData>(AssetPath);
            if (data == null)
                data = CreateGameCoreEditorGlobalData();

            if (data == null)
                return;

            data.EnsureGameTagRules();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        [MenuItem("【ES】/项目设置/GameCore/验证GameTag规则", priority = 23)]
        public static void ValidateGameTagRules()
        {
            GameCoreEditorGlobalData data = AssetDatabase.LoadAssetAtPath<GameCoreEditorGlobalData>(AssetPath);
            if (data == null)
            {
                Debug.LogError($"[GameCoreTag] 找不到 {AssetPath}。");
                return;
            }

            List<string> errors = new List<string>();
            data.EnsureTagDefinitions();
            if (!TryBuildTagEntries(data.tagDefinitions, out _, out string definitionError))
                errors.Add("tagDefinitions 无效：" + definitionError);
            Dictionary<ESGameTag, GameCoreTagRule> byTag = new Dictionary<ESGameTag, GameCoreTagRule>();
            if (data.gameTags == null)
            {
                errors.Add("gameTags 为空。");
            }
            else
            {
                for (int i = 0; i < data.gameTags.Count; i++)
                {
                    GameCoreTagRule rule = data.gameTags[i];
                    if (rule == null)
                    {
                        errors.Add($"gameTags[{i}] 为空。");
                        continue;
                    }

                    if (byTag.ContainsKey(rule.tag))
                        errors.Add($"GameTag {rule.tag} 被重复定义。");
                    else
                        byTag.Add(rule.tag, rule);
                    if (string.IsNullOrWhiteSpace(rule.group) || string.IsNullOrWhiteSpace(rule.meaning) || string.IsNullOrWhiteSpace(rule.ownerSystem))
                        errors.Add($"GameTag {rule.tag} 缺少分组、语义或归属系统。");
                    if ((ESGameTagCatalog.IsDefinedCore(rule.tag) || ESGameTagCatalog.IsReserved(rule.tag))
                        && rule.usagePolicy != ESGameTagCatalog.GetUsagePolicy(rule.tag))
                    {
                        errors.Add($"GameTag {rule.tag} 的使用策略为 {rule.usagePolicy}，应为 {ESGameTagCatalog.GetUsagePolicy(rule.tag)}。");
                    }
                }
            }

            if (!byTag.ContainsKey(ESGameTag.None))
                errors.Add("缺少 None 规则。");
            for (ushort value = ESGameTagCatalog.FirstDefinedValue; value <= ESGameTagCatalog.LastDefinedValue; value++)
            {
                ESGameTag tag = (ESGameTag)value;
                if (!byTag.ContainsKey(tag))
                    errors.Add($"缺少核心 GameTag 规则：{tag}({value})。");
            }

            string[] tagCatalogGuids = AssetDatabase.FindAssets("t:ESTagBakeTable");
            if (tagCatalogGuids == null || tagCatalogGuids.Length != 1)
            {
                errors.Add("稳定传输范围校验要求项目存在唯一 ESTagBakeTable。当前数量=" + (tagCatalogGuids == null ? 0 : tagCatalogGuids.Length) + "。");
            }
            else
            {
                string tagCatalogPath = AssetDatabase.GUIDToAssetPath(tagCatalogGuids[0]);
                ESTagBakeTable tagCatalog = AssetDatabase.LoadAssetAtPath<ESTagBakeTable>(tagCatalogPath);
                if (tagCatalog == null)
                {
                    errors.Add("ESTagBakeTable 无法加载：" + tagCatalogPath);
                }
                else if (!tagCatalog.TryValidate(out string catalogError))
                {
                    errors.Add("ESTagBakeTable 无效：" + catalogError);
                }
                else
                {
                    for (ushort value = ESGameTagCatalog.FirstDefinedValue; value <= ESGameTagCatalog.LastDefinedValue; value++)
                    {
                        ESGameTag tag = (ESGameTag)value;
                        if (!byTag.TryGetValue(tag, out GameCoreTagRule rule)
                            || !tagCatalog.TryGetEntry(ESTagId.FromInt32(value), out ESTagBakeTable.Entry entry))
                        {
                            continue;
                        }

                        if (rule.stableTransferScopes != entry.stableTransferScopes)
                        {
                            errors.Add($"GameTag {tag} 的稳定传输范围为 {rule.stableTransferScopes}，与 ESTagBakeTable 的 {entry.stableTransferScopes} 不一致。");
                        }
                    }
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log($"[GameCoreTag] 验证通过：{byTag.Count} 条规则，核心 Tag 1–{ESGameTagCatalog.LastDefinedValue} 均已定义。", data);
                return;
            }

            StringBuilder builder = new StringBuilder("[GameCoreTag] 验证失败：\n- ");
            builder.Append(string.Join("\n- ", errors));
            Debug.LogError(builder.ToString(), data);
        }

        [MenuItem("【ES】/项目设置/GameCore/Bake并应用GameTag Catalog", priority = 24)]
        public static void BakeAndApplyGameTagCatalog()
        {
            GameCoreEditorGlobalData data = AssetDatabase.LoadAssetAtPath<GameCoreEditorGlobalData>(AssetPath);
            if (data == null)
            {
                Debug.LogError("[GameCoreTag] 找不到 " + AssetPath + "。");
                return;
            }

            data.EnsureTagDefinitions();
            if (!TryBuildTagEntries(data.tagDefinitions, out List<ESTagBakeTable.Entry> entries, out string error))
            {
                Debug.LogError("[GameCoreTag] Bake 被拒绝：" + error, data);
                return;
            }

            string[] tableGuids = AssetDatabase.FindAssets("t:ESTagBakeTable");
            string[] rootGuids = AssetDatabase.FindAssets("t:ESTagCatalogGameCore");
            if (tableGuids == null || tableGuids.Length != 1 || rootGuids == null || rootGuids.Length != 1)
            {
                Debug.LogError("[GameCoreTag] Bake 要求项目存在唯一 ESTagBakeTable 与唯一 ESTagCatalogGameCore。");
                return;
            }

            ESTagBakeTable table = AssetDatabase.LoadAssetAtPath<ESTagBakeTable>(AssetDatabase.GUIDToAssetPath(tableGuids[0]));
            ESTagCatalogGameCore root = AssetDatabase.LoadAssetAtPath<ESTagCatalogGameCore>(AssetDatabase.GUIDToAssetPath(rootGuids[0]));
            if (table == null || root == null || !table.TryReplaceEntriesAndBake(entries, out error))
            {
                Debug.LogError("[GameCoreTag] Bake 失败：" + (error ?? "无法加载 Catalog 根。"), data);
                return;
            }

            root.SetBakedCatalog(table);
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            ESTagEditorCatalogCache.Invalidate();
            Debug.Log("[GameCoreTag] Bake 完成：" + entries.Count + " 条声明，SchemaHash=" + table.SchemaHash + "。", data);
        }

        private static bool TryBuildTagEntries(List<GameCoreTagDefinition> definitions, out List<ESTagBakeTable.Entry> entries, out string error)
        {
            entries = new List<ESTagBakeTable.Entry>();
            error = null;
            if (definitions == null || definitions.Count == 0)
            {
                error = "tagDefinitions 为空。请在 GameCore 的 GameTag定义中声明 Tag。";
                return false;
            }

            var enumKeys = new HashSet<string>();
            var stringKeys = new HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < definitions.Count; i++)
            {
                GameCoreTagDefinition definition = definitions[i];
                if (definition == null || definition.stableReference.IsEmpty)
                {
                    error = "tagDefinitions[" + i + "] 缺少稳定身份。";
                    return false;
                }

                ESTagStableReference reference = definition.stableReference;
                if (reference.HasEnumKey && !enumKeys.Add(reference.enumGroup + ":" + reference.enumValue))
                {
                    error = "重复 EnumKey：" + reference.enumGroup + ":" + reference.enumValue + "。";
                    return false;
                }
                if (reference.HasStringKey && !stringKeys.Add(reference.stringKey))
                {
                    error = "重复 StringKey：" + reference.stringKey + "。";
                    return false;
                }

                entries.Add(new ESTagBakeTable.Entry
                {
                    key = reference.stringKey,
                    enumGroup = reference.enumGroup,
                    enumValue = reference.enumValue,
                    storageTier = definition.storageTier,
                    availability = definition.availability,
                    deprecatedReplacement = definition.deprecatedReplacement,
                    stableTransferScopes = definition.stableTransferScopes
                });
            }
            return true;
        }

        [MenuItem("【ES】/项目设置/GameCore/运行GameTag核心自检", priority = 25)]
        public static void RunGameTagSelfTest()
        {
            try
            {
                int checks = STATIC_ESGameTagSelfTest.RunOrThrow();
                Debug.Log($"[GameCoreTag] 核心自检通过：{checks} 项。\n"
                          + "提示：此自检覆盖编号、引用计数和条件匹配；Buff 生命周期需在运行场景中结合具体 Buff 验证。");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("【ES】/项目设置/GameCore/验证全部Buff的GameTag配置", priority = 25)]
        public static void ValidateAllBuffGameTagConfigurations()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            HashSet<int> visited = new HashSet<int>();
            List<string> errors = new List<string>();
            int checkedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int assetIndex = 0; assetIndex < assets.Length; assetIndex++)
                {
                    BuffDefinitionDataInfo buff = assets[assetIndex] as BuffDefinitionDataInfo;
                    if (buff == null || !visited.Add(buff.GetInstanceID()))
                        continue;

                    checkedCount++;
                    if (buff.SharedData == null)
                    {
                        errors.Add(buff.name + " [" + path + "]：缺少 SharedData。");
                        continue;
                    }

                    if (!buff.SharedData.TryValidateGameTagConfiguration(out string error))
                        errors.Add(buff.name + " [" + path + "]：" + error);
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log("[GameCoreTag] Buff GameTag 配置验证通过：已检查 " + checkedCount
                          + " 个 Buff 定义。所有 Buff 仅授予 RuntimeFact，施加目标条件均可编译。");
                return;
            }

            StringBuilder builder = new StringBuilder("[GameCoreTag] Buff GameTag 配置验证失败：\n- ");
            builder.Append(string.Join("\n- ", errors));
            Debug.LogError(builder.ToString());
        }

        [MenuItem("【ES】/项目设置/GameCore/验证运行时Key Catalog Schema", priority = 26)]
        public static void ValidateRuntimeKeyCatalogSchemas()
        {
            List<string> errors = new List<string>();
            StringBuilder report = new StringBuilder("[ESKeyCatalog] Runtime catalog schema report:\n");

            AppendTableSchema(report, errors, "GameCore.Buff", ESRuntimeDataGameCore.Buffs);
            AppendTableSchema(report, errors, "GameCore.Shot", ESRuntimeDataGameCore.Shots);
            AppendTableSchema(report, errors, "GameCore.Monster", ESRuntimeDataGameCore.Monsters);
            AppendTableSchema(report, errors, "GameCore.Npc", ESRuntimeDataGameCore.Npcs);
            AppendTableSchema(report, errors, "GameCore.Weapon", ESRuntimeDataGameCore.Weapons);
            AppendTableSchema(report, errors, "GameCore.Skill", ESRuntimeDataGameCore.Skills);

            ESSuperAttributeTable characterAttributes = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            if (characterAttributes.TryBuildCatalog(out ESSuperAttributeCatalog attributeCatalog, out string attributeError))
                report.Append("Attribute.Character schema=").Append(attributeCatalog.SchemaHash).AppendLine();
            else
                errors.Add("Attribute.Character: " + attributeError);

            string[] tagCatalogGuids = AssetDatabase.FindAssets("t:ESTagBakeTable");
            for (int i = 0; i < tagCatalogGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(tagCatalogGuids[i]);
                ESTagBakeTable tagCatalog = AssetDatabase.LoadAssetAtPath<ESTagBakeTable>(path);
                if (tagCatalog == null)
                    continue;
                if (tagCatalog.TryValidate(out string tagError))
                    report.Append("Tag ").Append(path).Append(" schema=").Append(tagCatalog.SchemaHash).AppendLine();
                else
                    errors.Add("Tag " + path + ": " + tagError);
            }

            if (errors.Count == 0)
            {
                Debug.Log(report.ToString());
                return;
            }

            report.Append("Errors:\n- ").Append(string.Join("\n- ", errors));
            Debug.LogError(report.ToString());
        }

        [MenuItem("【ES】/项目设置/GameCore/审计项目稳定Key治理", priority = 27)]
        public static void AuditProjectStableKeyGovernance()
        {
            ESKeyGovernanceAudit.RunAndLog();
        }

        private static void AppendTableSchema<TData>(
            StringBuilder report,
            List<string> errors,
            string expectedScope,
            ESConfigKeyTable<TData> table)
            where TData : class
        {
            if (table == null)
            {
                errors.Add(expectedScope + ": table is null.");
                return;
            }
            if (table.IsBuilding)
            {
                errors.Add(expectedScope + ": table is building; retry after the current resource transaction.");
                return;
            }
            if (!string.Equals(expectedScope, table.KeyScope, System.StringComparison.Ordinal))
            {
                errors.Add(expectedScope + ": actual scope is " + table.KeyScope + ".");
                return;
            }

            ESKeyCatalogHandshake handshake = table.CreateSchemaHandshake();
            report.Append(expectedScope)
                .Append(" count=").Append(table.Count)
                .Append(" schema=").Append(handshake.schemaHash)
                .Append(" conflicts=").Append(table.ConflictCount)
                .AppendLine();
            if (table.ConflictCount > 0)
                errors.Add(expectedScope + ": " + table.GetConflictReport());
        }

        private static GameCoreEditorGlobalData CreateGameCoreEditorGlobalData()
        {
            EnsureFolder("Assets/ESNormalAssets", "Data");
            EnsureFolder("Assets/ESNormalAssets/Data", "GlobalData");
            EnsureFolder("Assets/ESNormalAssets/Data/GlobalData", "GameCore");

            GameCoreEditorGlobalData data = ScriptableObject.CreateInstance<GameCoreEditorGlobalData>();
            data.ResetDefaultRules();
            AssetDatabase.CreateAsset(data, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return data;
        }

        private static void EnsureFolder(string parent, string folder)
        {
            string path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
