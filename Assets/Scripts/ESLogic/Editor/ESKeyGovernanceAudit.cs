using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ES
{
    internal struct ESKeyGovernanceAuditResult
    {
        public readonly string reportPath;
        public readonly int warnings;
        public readonly int errors;

        public bool Succeeded => errors == 0;

        public ESKeyGovernanceAuditResult(string reportPath, int warnings, int errors)
        {
            this.reportPath = reportPath;
            this.warnings = warnings;
            this.errors = errors;
        }
    }

    /// <summary>
    /// Editor-only governance audit. It combines authoritative loaded catalogs with a deliberately
    /// conservative source scan. The source scan reports direct dotted string literals as review
    /// candidates; it never treats Unity paths, GUIDs, instance ids, or local dictionary names as
    /// stable business identities.
    /// </summary>
    internal static class ESKeyGovernanceAudit
    {
        private const string ReportPath = "Documentation/KEY_AUDIT_REPORT.md";
        private static readonly Regex DottedStringLiteral = new Regex(
            "\\\"(?<key>[A-Za-z_][A-Za-z0-9_]*(?:\\.[A-Za-z_][A-Za-z0-9_]*)+)\\\"",
            RegexOptions.Compiled);
        private static readonly Regex StringDictionaryDeclaration = new Regex(
            "(?<type>Dictionary|IDictionary|ConcurrentDictionary|SortedDictionary)\\s*<\\s*string\\s*,",
            RegexOptions.Compiled);

        private enum SourceUsageKind : byte
        {
            Declared,
            Read,
            Write,
            Review
        }

        private sealed class SourceUsage
        {
            public readonly List<string> declaredBy = new List<string>();
            public readonly List<string> readBy = new List<string>();
            public readonly List<string> writtenBy = new List<string>();
            public readonly List<string> reviewBy = new List<string>();
        }

        private struct StringContainerUsage
        {
            public string owner;
            public string classification;
        }

        public static void RunAndLog()
        {
            ESKeyGovernanceAuditResult result = RunAndWriteReport(true);
            if (result.errors > 0)
                Debug.LogError("[ESKeyAudit] 完成，发现 " + result.errors + " 个错误、" + result.warnings + " 个待审查项。报告：" + ReportPath);
            else if (result.warnings > 0)
                Debug.LogWarning("[ESKeyAudit] 完成，发现 " + result.warnings + " 个待审查项。报告：" + ReportPath);
            else
                Debug.Log("[ESKeyAudit] 通过。报告：" + ReportPath);
        }

        public static ESKeyGovernanceAuditResult RunAndWriteReport(bool refreshAssetDatabase)
        {
            string report = BuildReport(out int warnings, out int errors);
            string absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportPath));
            string directory = Path.GetDirectoryName(absolutePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            ESManagedFileIO.WriteTextAtomic(absolutePath, report, new UTF8Encoding(false), projectRoot);
            if (refreshAssetDatabase)
                AssetDatabase.Refresh();
            return new ESKeyGovernanceAuditResult(absolutePath, warnings, errors);
        }

        public static void RunAndThrowIfErrors(string phase)
        {
            ESKeyGovernanceAuditResult result = RunAndWriteReport(false);
            if (result.errors > 0)
            {
                throw new BuildFailedException("[ESKeyAudit] " + phase + " 已拒绝：发现 " + result.errors
                                               + " 个稳定 Key 治理错误。报告：" + ReportPath);
            }

            if (result.warnings > 0)
                Debug.LogWarning("[ESKeyAudit] " + phase + " 通过，但有 " + result.warnings + " 个待审查项。报告：" + ReportPath);
        }

        private static string BuildReport(out int warnings, out int errors)
        {
            StringBuilder builder = new StringBuilder(32 * 1024);
            HashSet<string> declaredStrings = new HashSet<string>(StringComparer.Ordinal);
            warnings = 0;
            errors = 0;

            builder.AppendLine("# ES Key Governance Audit");
            builder.AppendLine();
            builder.AppendLine("This report separates authoritative catalog entries from source-level review candidates. RuntimeKey is shown only as a process-local diagnostic and is never a persistence or network contract.");
            builder.AppendLine();

            builder.AppendLine("## Loaded GameCore Config Catalogs");
            AppendConfigTable(builder, "GameCore.Buff", ESRuntimeDataGameCore.Buffs, declaredStrings, ref errors);
            AppendConfigTable(builder, "GameCore.Shot", ESRuntimeDataGameCore.Shots, declaredStrings, ref errors);
            AppendConfigTable(builder, "GameCore.Monster", ESRuntimeDataGameCore.Monsters, declaredStrings, ref errors);
            AppendConfigTable(builder, "GameCore.Npc", ESRuntimeDataGameCore.Npcs, declaredStrings, ref errors);
            AppendConfigTable(builder, "GameCore.Weapon", ESRuntimeDataGameCore.Weapons, declaredStrings, ref errors);
            AppendConfigTable(builder, "GameCore.Skill", ESRuntimeDataGameCore.Skills, declaredStrings, ref errors);
            builder.AppendLine();

            builder.AppendLine("## Attribute Catalogs");
            ESSuperAttributeTable characterTable = ESCharacterAttributeCatalog.CreateDefaultSuperAttributeTable();
            if (characterTable.TryBuildCatalog(out ESSuperAttributeCatalog characterCatalog, out string attributeError))
            {
                List<ESKeyCatalogUsageSnapshot> usage = new List<ESKeyCatalogUsageSnapshot>();
                characterCatalog.CopyUsageSnapshots(usage);
                builder.AppendLine("- `" + characterCatalog.Scope + "` schema=`" + characterCatalog.SchemaHash + "`");
                for (int i = 0; i < usage.Count; i++)
                {
                    ESKeyCatalogUsageSnapshot snapshot = usage[i];
                    if (!string.IsNullOrEmpty(snapshot.entry.key.stringKey))
                        declaredStrings.Add(snapshot.entry.key.stringKey);
                    AppendCatalogUsage(builder, snapshot, ref warnings);
                }
            }
            else
            {
                errors++;
                builder.AppendLine("- ERROR Attribute.Character: " + attributeError);
            }
            builder.AppendLine();

            builder.AppendLine("## Input Configuration Catalogs");
            AppendInputActionCatalogs(builder, declaredStrings, ref warnings, ref errors);
            builder.AppendLine();

            builder.AppendLine("## Fixed Local Runtime Registries");
            AppendStateParameterRegistry(builder);
            builder.AppendLine();

            builder.AppendLine("## Tag Catalogs");
            AppendTagCatalogs(builder, declaredStrings, ref errors);
            builder.AppendLine();

            Dictionary<string, SourceUsage> sourceUsage = ScanSourceUsage();
            builder.AppendLine("## Source Direct-Key Review");
            builder.AppendLine("Only dotted string literals inside C# source are listed here. `Review` means the scanner cannot prove the literal is a stable business key; inspect it before promoting it to a Catalog.");
            if (sourceUsage.Count == 0)
            {
                builder.AppendLine("- No direct dotted string literals found.");
            }
            else
            {
                List<string> keys = new List<string>(sourceUsage.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i];
                    SourceUsage usage = sourceUsage[key];
                    bool declared = declaredStrings.Contains(key) || usage.declaredBy.Count > 0;
                    if (!declared && (usage.readBy.Count > 0 || usage.writtenBy.Count > 0))
                        warnings++;

                    builder.Append("- `").Append(key).Append("` status=")
                        .Append(declared ? "declared-or-source-defined" : "UNCLASSIFIED")
                        .Append(" declared=").Append(Join(usage.declaredBy))
                        .Append(" read=").Append(Join(usage.readBy))
                        .Append(" write=").Append(Join(usage.writtenBy))
                        .Append(" review=").Append(Join(usage.reviewBy))
                        .AppendLine();
                }
            }
            builder.AppendLine();

            List<StringContainerUsage> containers = ScanStringKeyContainers();
            builder.AppendLine("## String-Key Container Classification");
            builder.AppendLine("This section prevents the false rule that every string dictionary must become a global catalog. `REVIEW` requires an explicit ownership decision before the container carries persistent or cross-system business identity.");
            for (int i = 0; i < containers.Count; i++)
            {
                StringContainerUsage container = containers[i];
                if (container.classification == "REVIEW")
                    warnings++;
                builder.AppendLine("- `" + container.owner + "` classification=" + container.classification);
            }
            builder.AppendLine();

            builder.AppendLine("## Required Follow-up");
            builder.AppendLine("- Every `UNCLASSIFIED` entry used for configuration, persistence, cross-version data, network payloads, DLC, or mods must become a scoped Catalog declaration or be explicitly documented as local-only.");
            builder.AppendLine("- `unused` attribute entries are review signals, not automatic deletion candidates. Confirm external/asset-driven consumers before removal.");
            builder.AppendLine("- RuntimeKey values in this report must not be copied into serialized assets, saves, manifests, or packets.");
            builder.AppendLine();
            builder.AppendLine("## Result");
            builder.AppendLine("- Blocking errors: `" + errors + "`");
            builder.AppendLine("- Review warnings: `" + warnings + "`");
            builder.AppendLine("- Player builds and resource bakes are accepted only when blocking errors are zero.");
            return builder.ToString();
        }

        private static void AppendConfigTable<TData>(
            StringBuilder builder,
            string expectedScope,
            ESConfigKeyTable<TData> table,
            HashSet<string> declaredStrings,
            ref int errors)
            where TData : class
        {
            if (table == null)
            {
                errors++;
                builder.AppendLine("- ERROR `" + expectedScope + "`: table is null.");
                return;
            }

            if (table.IsBuilding)
            {
                errors++;
                builder.AppendLine("- ERROR `" + expectedScope + "`: table is building.");
                return;
            }

            if (!string.Equals(expectedScope, table.KeyScope, StringComparison.Ordinal))
            {
                errors++;
                builder.AppendLine("- ERROR `" + expectedScope + "`: actual scope is `" + table.KeyScope + "`.");
                return;
            }

            List<ESConfigKeyTableEntry> entries = new List<ESConfigKeyTableEntry>();
            table.CopyEntries(entries);
            builder.AppendLine("- `" + expectedScope + "` schema=`" + table.SchemaHash + "` entries=" + entries.Count + " conflicts=" + table.ConflictCount);
            for (int i = 0; i < entries.Count; i++)
            {
                ESConfigKeyTableEntry entry = entries[i];
                if (!string.IsNullOrEmpty(entry.stringKey))
                    declaredStrings.Add(entry.stringKey);
                builder.AppendLine("  - Enum=`" + entry.enumKey + "` String=`" + (entry.stringKey ?? string.Empty)
                                   + "` Runtime=`" + entry.runtimeKey + "` declaredBy=`" + (entry.debugName ?? string.Empty) + "`");
            }

            if (table.ConflictCount > 0)
            {
                errors++;
                builder.AppendLine("  - ERROR " + table.GetConflictReport());
            }
        }

        private static void AppendCatalogUsage(StringBuilder builder, ESKeyCatalogUsageSnapshot snapshot, ref int warnings)
        {
            ESKeyCatalogEntry entry = snapshot.entry;
            builder.Append("  - Enum=`").Append(entry.key.enumKey)
                .Append("` String=`").Append(entry.key.stringKey ?? string.Empty)
                .Append("` Runtime=`").Append(entry.runtimeKey)
                .Append("` Type=`").Append(entry.valueKind)
                .Append("` Storage=`").Append(entry.storagePolicy)
                .Append("` declared=`").Append(snapshot.declaredBy)
                .Append("` read=`").Append(snapshot.readBy)
                .Append("` write=`").Append(snapshot.writtenBy).Append('`');
            if (snapshot.IsUnused)
            {
                warnings++;
                builder.Append(" unused");
            }
            builder.AppendLine();
        }

        private static void AppendTagCatalogs(StringBuilder builder, HashSet<string> declaredStrings, ref int errors)
        {
            string[] tableGuids = AssetDatabase.FindAssets("t:ESTagBakeTable");
            if (tableGuids == null || tableGuids.Length != 1)
            {
                errors++;
                builder.AppendLine("- ERROR Expected exactly one formal `ESTagBakeTable`, found `"
                                   + (tableGuids == null ? 0 : tableGuids.Length) + "`.");
                return;
            }

            string tablePath = AssetDatabase.GUIDToAssetPath(tableGuids[0]);
            ESTagBakeTable table = AssetDatabase.LoadAssetAtPath<ESTagBakeTable>(tablePath);
            if (table == null)
            {
                errors++;
                builder.AppendLine("- ERROR Formal Tag Catalog cannot be loaded: `" + tablePath + "`.");
                return;
            }
            if (!table.TryValidate(out string error))
            {
                errors++;
                builder.AppendLine("- ERROR `" + tablePath + "`: " + error);
                return;
            }

            builder.AppendLine("- `" + tablePath + "` schema=`" + table.SchemaHash + "` layout=`"
                               + table.RuntimeLayoutHash + "` entries=" + table.Count);
            IReadOnlyList<ESTagBakeTable.Entry> entries = table.Entries;
            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                ESTagBakeTable.Entry entry = entries[entryIndex];
                if (!string.IsNullOrEmpty(entry.key))
                    declaredStrings.Add(entry.key);
                builder.AppendLine("  - Storage=`" + entry.storageTier + "` Availability=`" + entry.availability
                                   + "` Replacement=`" + entry.deprecatedReplacement + "` Transfer=`" + entry.stableTransferScopes + "` Enum=`" + entry.enumGroup + ":" + entry.enumValue + "` String=`" + (entry.key ?? string.Empty)
                                   + "` Runtime=`" + entry.bakedId + "`");
            }

            string[] rootGuids = AssetDatabase.FindAssets("t:ESTagCatalogGameCore");
            if (rootGuids == null || rootGuids.Length != 1)
            {
                errors++;
                builder.AppendLine("- ERROR Expected exactly one `ESTagCatalogGameCore`, found `"
                                   + (rootGuids == null ? 0 : rootGuids.Length) + "`.");
                return;
            }

            string rootPath = AssetDatabase.GUIDToAssetPath(rootGuids[0]);
            ESTagCatalogGameCore root = AssetDatabase.LoadAssetAtPath<ESTagCatalogGameCore>(rootPath);
            if (root == null || root.TagCatalog != table)
            {
                errors++;
                builder.AppendLine("- ERROR `" + rootPath + "` must reference the sole formal `ESTagBakeTable`.");
                return;
            }
            if (!root.TryValidate(out error))
            {
                errors++;
                builder.AppendLine("- ERROR `" + rootPath + "`: " + error);
                return;
            }

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(root, out string rootGuid, out long rootLocalFileId);
            // ESAssetRefer uses zero for a main asset and its concrete local file id only for
            // a sub-asset. AssetDatabase returns 11400000 for the same main asset.
            if (AssetDatabase.LoadMainAssetAtPath(rootPath) == root)
                rootLocalFileId = 0;
            int consumerCount = CountConsumersPreloadingGameCore(rootGuid, rootLocalFileId);
            builder.AppendLine("- Root `" + rootPath + "` expectedSchema=`" + root.ExpectedSchemaHash
                               + "` consumerPreloads=`" + consumerCount + "`");
            if (consumerCount == 0)
            {
                errors++;
                builder.AppendLine("  - ERROR Tag Catalog root is not in any Consumer GameCoreAssets preload list.");
            }
        }

        private static int CountConsumersPreloadingGameCore(string targetGuid, long targetLocalFileId)
        {
            int count = 0;
            string[] consumerGuids = AssetDatabase.FindAssets("t:ESAssetLibraryConsumer");
            for (int i = 0; i < consumerGuids.Length; i++)
            {
                ESAssetLibraryConsumer consumer = AssetDatabase.LoadAssetAtPath<ESAssetLibraryConsumer>(
                    AssetDatabase.GUIDToAssetPath(consumerGuids[i]));
                if (consumer == null || consumer.GameCoreAssets == null)
                    continue;

                for (int entryIndex = 0; entryIndex < consumer.GameCoreAssets.Count; entryIndex++)
                {
                    ESAssetReferBase entry = consumer.GameCoreAssets[entryIndex];
                    if (entry != null
                        && string.Equals(entry.GUID, targetGuid, StringComparison.Ordinal)
                        && entry.LocalFileId == targetLocalFileId)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private static void AppendInputActionCatalogs(
            StringBuilder builder,
            HashSet<string> declaredStrings,
            ref int warnings,
            ref int errors)
        {
            string[] guids = AssetDatabase.FindAssets("t:ESInputConfig");
            if (guids == null || guids.Length == 0)
            {
                builder.AppendLine("- No ESInputConfig assets found.");
                return;
            }

            Array.Sort(guids, StringComparer.Ordinal);
            HashSet<string> configIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ESInputConfig input = AssetDatabase.LoadAssetAtPath<ESInputConfig>(path);
                if (input == null)
                    continue;
                if (!input.TryCreateSchemaHandshake(out ESInputConfigSchemaHandshake handshake, out string error))
                {
                    errors++;
                    builder.AppendLine("- ERROR `" + path + "`: " + error);
                    continue;
                }

                if (!configIds.Add(handshake.configId))
                {
                    errors++;
                    builder.AppendLine("- ERROR `" + path + "`: duplicate Input.Config StringKey `"
                                       + handshake.configId + "`.");
                    continue;
                }

                declaredStrings.Add(handshake.configId);
                builder.AppendLine("- `" + path + "` Config=`" + handshake.configId
                                   + "` SchemeSchema=`" + handshake.schemeSchemaHash
                                   + "` ActionSchema=`" + handshake.actionSchemaHash + "`");

                if (!input.TryBuildSchemeCatalog(out ESInputSchemeCatalog schemeCatalog, out error))
                {
                    errors++;
                    builder.AppendLine("  - ERROR Input.Scheme: " + error);
                    continue;
                }
                builder.AppendLine("  - `Input.Scheme` entries=" + schemeCatalog.Entries.Count);
                List<ESKeyCatalogUsageSnapshot> schemeUsage = new List<ESKeyCatalogUsageSnapshot>();
                schemeCatalog.CopyUsageSnapshots(schemeUsage);
                for (int usageIndex = 0; usageIndex < schemeUsage.Count; usageIndex++)
                {
                    ESKeyCatalogUsageSnapshot snapshot = schemeUsage[usageIndex];
                    if (!string.IsNullOrEmpty(snapshot.entry.key.stringKey))
                        declaredStrings.Add(snapshot.entry.key.stringKey);
                    AppendCatalogUsage(builder, snapshot, ref warnings);
                }

                if (!input.TryBuildActionCatalog(out ESInputActionCatalog catalog, out error))
                {
                    errors++;
                    builder.AppendLine("  - ERROR Input.Action: " + error);
                    continue;
                }

                builder.AppendLine("  - `Input.Action` entries=" + catalog.Entries.Count);
                List<ESKeyCatalogUsageSnapshot> usage = new List<ESKeyCatalogUsageSnapshot>();
                catalog.CopyUsageSnapshots(usage);
                for (int usageIndex = 0; usageIndex < usage.Count; usageIndex++)
                {
                    ESKeyCatalogUsageSnapshot snapshot = usage[usageIndex];
                    if (!string.IsNullOrEmpty(snapshot.entry.key.stringKey))
                        declaredStrings.Add(snapshot.entry.key.stringKey);
                    AppendCatalogUsage(builder, snapshot, ref warnings);
                }
            }
        }

        private static void AppendStateParameterRegistry(StringBuilder builder)
        {
            builder.AppendLine("- `State.Default.Int` stable enums=" + StateDefaultNumericParameterCatalog.IntRuntimeKeyCount + " runtime slots");
            Array intValues = Enum.GetValues(typeof(StateDefaultIntParameter));
            for (int i = 0; i < intValues.Length; i++)
            {
                StateDefaultIntParameter parameter = (StateDefaultIntParameter)intValues.GetValue(i);
                if (StateDefaultNumericParameterCatalog.TryGetName(parameter, out string name)
                    && StateDefaultNumericParameterCatalog.TryGetIndex(parameter, out int runtimeKey))
                    builder.AppendLine("  - Enum=`" + (int)parameter + "` String=`" + name + "` Runtime=`" + runtimeKey + "`");
            }

            builder.AppendLine("- `State.Default.Bool` stable enums=" + StateDefaultNumericParameterCatalog.BoolRuntimeKeyCount + " runtime slots");
            Array boolValues = Enum.GetValues(typeof(StateDefaultBoolParameter));
            for (int i = 0; i < boolValues.Length; i++)
            {
                StateDefaultBoolParameter parameter = (StateDefaultBoolParameter)boolValues.GetValue(i);
                if (StateDefaultNumericParameterCatalog.TryGetName(parameter, out string name)
                    && StateDefaultNumericParameterCatalog.TryGetIndex(parameter, out int runtimeKey))
                    builder.AppendLine("  - Enum=`" + (int)parameter + "` String=`" + name + "` Runtime=`" + runtimeKey + "`");
            }

            builder.AppendLine("- `State.Default.Float` uses compiled field HotSlots; its enum/name aliases are source-defined and local to StateMachineContext.");
        }

        private static Dictionary<string, SourceUsage> ScanSourceUsage()
        {
            Dictionary<string, SourceUsage> result = new Dictionary<string, SourceUsage>(StringComparer.Ordinal);
            List<string> files = new List<string>(ESManagedFileIO.EnumerateFilesSafely(Application.dataPath, "*.cs"));
            files.Sort(StringComparer.Ordinal);
            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
            {
                string fullPath = files[fileIndex];
                string normalizedPath = fullPath.Replace('\\', '/');
                if (normalizedPath.IndexOf("/Obsolete/", StringComparison.OrdinalIgnoreCase) >= 0
                    || normalizedPath.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(fullPath, Encoding.UTF8);
                }
                catch (IOException)
                {
                    continue;
                }

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    MatchCollection matches = DottedStringLiteral.Matches(line);
                    for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                    {
                        string key = matches[matchIndex].Groups["key"].Value;
                        if (IsExcludedLiteral(key))
                            continue;

                        if (!result.TryGetValue(key, out SourceUsage usage))
                        {
                            usage = new SourceUsage();
                            result.Add(key, usage);
                        }

                        string owner = ToAssetRelativePath(fullPath) + ":" + (lineIndex + 1);
                        AddUsage(usage, ClassifyLine(line), owner);
                    }
                }
            }

            return result;
        }

        private static List<StringContainerUsage> ScanStringKeyContainers()
        {
            List<StringContainerUsage> result = new List<StringContainerUsage>();
            List<string> files = new List<string>(ESManagedFileIO.EnumerateFilesSafely(Application.dataPath, "*.cs"));
            files.Sort(StringComparer.Ordinal);
            for (int fileIndex = 0; fileIndex < files.Count; fileIndex++)
            {
                string fullPath = files[fileIndex];
                string normalizedPath = fullPath.Replace('\\', '/');
                if (!IsProjectOwnedSource(normalizedPath))
                    continue;

                string[] lines;
                try
                {
                    lines = File.ReadAllLines(fullPath, Encoding.UTF8);
                }
                catch (IOException)
                {
                    continue;
                }

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    if (!StringDictionaryDeclaration.IsMatch(line))
                        continue;

                    result.Add(new StringContainerUsage
                    {
                        owner = ToAssetRelativePath(fullPath) + ":" + (lineIndex + 1),
                        classification = ClassifyStringContainer(line)
                    });
                }
            }
            return result;
        }

        private static SourceUsageKind ClassifyLine(string line)
        {
            if (line.IndexOf("const string", StringComparison.Ordinal) >= 0
                || line.IndexOf("static readonly string", StringComparison.Ordinal) >= 0)
                return SourceUsageKind.Declared;
            if (line.IndexOf("Set", StringComparison.Ordinal) >= 0
                || line.IndexOf("Add", StringComparison.Ordinal) >= 0
                || line.IndexOf("Remove", StringComparison.Ordinal) >= 0
                || line.IndexOf("Bake", StringComparison.Ordinal) >= 0
                || line.IndexOf("Register", StringComparison.Ordinal) >= 0
                || line.IndexOf("Inject", StringComparison.Ordinal) >= 0)
                return SourceUsageKind.Write;
            if (line.IndexOf("Get", StringComparison.Ordinal) >= 0
                || line.IndexOf("TryGet", StringComparison.Ordinal) >= 0
                || line.IndexOf("Has", StringComparison.Ordinal) >= 0
                || line.IndexOf("Contains", StringComparison.Ordinal) >= 0)
                return SourceUsageKind.Read;
            return SourceUsageKind.Review;
        }

        private static void AddUsage(SourceUsage usage, SourceUsageKind kind, string owner)
        {
            List<string> destination;
            switch (kind)
            {
                case SourceUsageKind.Declared:
                    destination = usage.declaredBy;
                    break;
                case SourceUsageKind.Read:
                    destination = usage.readBy;
                    break;
                case SourceUsageKind.Write:
                    destination = usage.writtenBy;
                    break;
                default:
                    destination = usage.reviewBy;
                    break;
            }

            if (destination.Count < 8)
                destination.Add(owner);
        }

        private static bool IsExcludedLiteral(string value)
        {
            return value.StartsWith("Assets.", StringComparison.Ordinal)
                   || value.StartsWith("Packages.", StringComparison.Ordinal)
                   || value.StartsWith("Unity.", StringComparison.Ordinal)
                   || value.StartsWith("System.", StringComparison.Ordinal)
                   || value.IndexOf("..", StringComparison.Ordinal) >= 0;
        }

        private static bool IsProjectOwnedSource(string normalizedPath)
        {
            if (normalizedPath.IndexOf("/Obsolete/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/Generated/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return normalizedPath.IndexOf("/Assets/Scripts/ESLogic/", StringComparison.OrdinalIgnoreCase) >= 0
                   || normalizedPath.IndexOf("/Assets/Plugins/ES/0_Stand/", StringComparison.OrdinalIgnoreCase) >= 0
                   || normalizedPath.IndexOf("/Assets/Plugins/ES/1_Design/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ClassifyStringContainer(string line)
        {
            string lower = line.ToLowerInvariant();
            if (lower.Contains("catalog") || lower.Contains("configkey") || lower.Contains("tag"))
                return "Catalog implementation";
            if (lower.Contains("guid") || lower.Contains("path") || lower.Contains("address") || lower.Contains("hash"))
                return "Resource/location identity";
            if (lower.Contains("cache") || lower.Contains("temporary") || lower.Contains("context")
                || lower.Contains("pool") || lower.Contains("state") || lower.Contains("column") || lower.Contains("row"))
                return "Local runtime/editor container";
            return "REVIEW";
        }

        private static string ToAssetRelativePath(string fullPath)
        {
            string assetsPath = Application.dataPath.Replace('\\', '/');
            string normalized = fullPath.Replace('\\', '/');
            return normalized.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase)
                ? "Assets" + normalized.Substring(assetsPath.Length)
                : normalized;
        }

        private static string Join(List<string> values)
        {
            return values == null || values.Count == 0 ? "-" : string.Join(", ", values);
        }
    }
}
