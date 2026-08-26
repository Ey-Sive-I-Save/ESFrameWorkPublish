using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using TMPro;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ES
{
    internal static class ESPresentationAssetPipelineStatus
    {
        internal static string Describe(UnityEngine.Object asset, ESAssetLibraryConsumer consumer)
        {
            if (asset == null)
                return "未生成运行时目录。";
            ESPipelineAssetIdentity identity = ESAssetPipelineIO.GetIdentity(asset);
            if (!identity.IsValid)
                return "资产缺少稳定 GUID/LocalFileId。";

            ESAssetPage page = null;
            foreach (ESAssetReferKind kind in Enum.GetValues(typeof(ESAssetReferKind)))
            {
                if (kind == ESAssetReferKind.None)
                    continue;
                if (ESAssetRegistry.TryGetByAssetIdentity(kind, identity.guid, identity.localFileId, out page))
                    break;
            }
            if (page == null)
                return "尚未登记到 AssetLibrary。";
            if (consumer == null)
                return "已登记到 AssetLibrary；尚未选择启动 Consumer。";
            bool resident = (consumer.ResidentAssets ?? new List<ESAssetReferBase>())
                .Any(item => item != null
                    && item.AssetIdentity.Equals(new ESAssetIdentity(identity.guid, identity.localFileId)));
            return resident
                ? "已登记到 AssetLibrary 和 Consumer 启动常驻；仍需完成 Bake、Plan、Build 与 Publish。"
                : "已登记到 AssetLibrary；尚未加入当前 Consumer 启动常驻。";
        }
    }

    public sealed class ESFontBuildPlanEntry
    {
        public EnumCollect.Envir_LanguageType Language { get; internal set; }
        public ESFontUsage Usage { get; internal set; }
        public ESFontScriptGroup ScriptGroup { get; internal set; }
        public Font SourceFont { get; internal set; }
        public string OutputPath { get; internal set; }
        public int UnicodeScalarCount { get; internal set; }
        public int TextSourceCount { get; internal set; }
        public int LocalizationTextCount { get; internal set; }
        public string InputHash { get; internal set; }
    }

    public sealed class ESFontBuildPlan
    {
        private readonly List<ESFontBuildPlanEntry> entries = new List<ESFontBuildPlanEntry>();

        public string ProfileId { get; internal set; }
        public string OutputFolder { get; internal set; }
        public IReadOnlyList<ESFontBuildPlanEntry> Entries => entries;

        internal void Add(ESFontBuildPlanEntry entry) => entries.Add(entry);
    }

    public static class ESFontBuildProfileEditor
    {
        public static void ApplyStandardTenLanguageTemplate(ESFontBuildProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            Undo.RecordObject(profile, "应用 ES 十语言字体方案");
            profile.enabledLanguages = new List<EnumCollect.Envir_LanguageType>();
            for (int index = 0; index < ESLocaleIdentity.SupportedLanguageCount; index++)
                profile.enabledLanguages.Add(ESLocaleIdentity.GetSupportedLanguageAt(index));
            if (profile.enabledUsages == null || profile.enabledUsages.Count == 0)
                profile.enabledUsages = new List<ESFontUsage> { ESFontUsage.Body };
            profile.fontFamily = profile.fontFamily ?? new ESFontFamilyDefinition();
            EnsureStandardFontSources(profile.fontFamily);
            SynchronizeLanguageEntries(profile);
            EditorUtility.SetDirty(profile);
        }

        public static string BindManagedSourceFonts(ESFontBuildProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (profile.sourceFontFolder == null)
                throw new InvalidOperationException("请先绑定受管源字体目录。");
            string folder = AssetDatabase.GetAssetPath(profile.sourceFontFolder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("受管源字体目录必须是项目 Assets/ 内的文件夹。");

            var byName = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (font == null) continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (!byName.TryAdd(name, font))
                    throw new InvalidOperationException("受管源字体目录存在同名字体，无法确定性绑定：" + name);
            }

            profile.fontFamily = profile.fontFamily ?? new ESFontFamilyDefinition();
            EnsureStandardFontSources(profile.fontFamily);
            var defaultAssignments = new Dictionary<ESFontScriptSource, Font>();
            var roleAssignments = new List<Tuple<ESFontScriptSource, ESFontUsage, Font>>();
            foreach (ESFontScriptSource source in profile.fontFamily.sources.Where(item => item != null))
            {
                Font defaultFont = FindManagedFont(byName,
                    "ESFont_" + source.scriptGroup,
                    source.scriptGroup.ToString());
                if (defaultFont != null) defaultAssignments[source] = defaultFont;
                foreach (ESFontUsage usage in Enum.GetValues(typeof(ESFontUsage)))
                {
                    Font roleFont = FindManagedFont(byName,
                        "ESFont_" + source.scriptGroup + "_" + usage,
                        source.scriptGroup + "_" + usage);
                    if (roleFont != null) roleAssignments.Add(Tuple.Create(source, usage, roleFont));
                }
            }

            Undo.RecordObject(profile, "绑定 ES 受管源字体");
            foreach (KeyValuePair<ESFontScriptSource, Font> pair in defaultAssignments)
                pair.Key.defaultFont = pair.Value;
            foreach (Tuple<ESFontScriptSource, ESFontUsage, Font> assignment in roleAssignments)
                SetRoleSource(assignment.Item1, assignment.Item2, assignment.Item3);
            EditorUtility.SetDirty(profile);
            int unbound = profile.fontFamily.sources.Count(source => source != null && source.defaultFont == null);
            return "已绑定 " + defaultAssignments.Count + " 个文字类型默认字体和 "
                + roleAssignments.Count + " 个角色专用字体；仍有 " + unbound
                + " 个文字类型没有默认字体，预检会指出实际需要补齐的项。";
        }

        public static void SynchronizeLanguageEntries(ESFontBuildProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            IReadOnlyList<EnumCollect.Envir_LanguageType> enabledLanguages = GetEnabledLanguages(profile);
            IReadOnlyList<ESFontUsage> enabledUsages = GetEnabledUsages(profile);
            var existing = new Dictionary<int, ESFontLanguageBuildEntry>();
            foreach (ESFontLanguageBuildEntry entry in profile.languages ?? new List<ESFontLanguageBuildEntry>())
            {
                if (entry == null) continue;
                if (!string.IsNullOrWhiteSpace(entry.legacyLanguageCode)
                    || entry.legacySourceFont != null
                    || entry.legacyFallbackOverride != null && entry.legacyFallbackOverride.Count > 0)
                {
                    throw new InvalidOperationException("字体方案仍包含旧 TMP/Locale 配置，请先点击“迁移旧字体配置”。");
                }
                int key = MakeEntryKey(entry.language, entry.usage);
                if (!existing.TryAdd(key, entry))
                    throw new InvalidOperationException("字体语言与角色配置重复：" + GetEntryIdentity(entry));
            }

            var synchronized = new List<ESFontLanguageBuildEntry>(enabledLanguages.Count * enabledUsages.Count);
            foreach (EnumCollect.Envir_LanguageType language in enabledLanguages)
            {
                foreach (ESFontUsage usage in enabledUsages)
                {
                    int key = MakeEntryKey(language, usage);
                    if (!existing.TryGetValue(key, out ESFontLanguageBuildEntry entry))
                        entry = new ESFontLanguageBuildEntry { language = language, usage = usage };
                    synchronized.Add(entry);
                }
            }
            profile.languages = synchronized;
        }

        public static string MigrateLegacyConfiguration(ESFontBuildProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var resolvedLanguages = new Dictionary<ESFontLanguageBuildEntry, EnumCollect.Envir_LanguageType>();
            var plannedSources = new Dictionary<int, Font>();
            foreach (ESFontLanguageBuildEntry entry in profile.languages ?? new List<ESFontLanguageBuildEntry>())
            {
                if (entry == null) continue;
                EnumCollect.Envir_LanguageType language = entry.language;
                if (!string.IsNullOrWhiteSpace(entry.legacyLanguageCode)
                    && !ESLocaleIdentity.TryParse(entry.legacyLanguageCode, out language))
                    throw new InvalidOperationException("旧字体语言代码无法迁移：" + entry.legacyLanguageCode);
                if (!ESLocalizationRuntime.IsConcreteLanguage(language))
                    throw new InvalidOperationException("旧字体条目缺少可迁移的具体语言。");
                resolvedLanguages.Add(entry, language);
                if (entry.legacySourceFont == null) continue;
                int sourceKey = MakeSourceKey(ResolveScriptGroup(language, entry.usage), entry.usage);
                if (plannedSources.TryGetValue(sourceKey, out Font existing)
                    && existing != entry.legacySourceFont)
                    throw new InvalidOperationException("旧条目为同一文字类型/角色配置了不同源字体，无法自动合并："
                        + ESLocaleIdentity.GetDisplayName(language) + "/" + entry.usage);
                plannedSources[sourceKey] = entry.legacySourceFont;
            }

            Undo.RecordObject(profile, "迁移旧 ES 字体配置");
            profile.fontFamily = profile.fontFamily ?? new ESFontFamilyDefinition();
            EnsureStandardFontSources(profile.fontFamily);
            foreach (KeyValuePair<ESFontLanguageBuildEntry, EnumCollect.Envir_LanguageType> pair in resolvedLanguages)
            {
                ESFontLanguageBuildEntry entry = pair.Key;
                entry.language = pair.Value;
                entry.legacyLanguageCode = string.Empty;
                entry.legacySourceFont = null;
                entry.legacyFallbackOverride?.Clear();
            }
            foreach (KeyValuePair<int, Font> pair in plannedSources)
            {
                ESFontScriptGroup group = (ESFontScriptGroup)(pair.Key >> 8);
                ESFontUsage usage = (ESFontUsage)(pair.Key & 0xFF);
                ESFontScriptSource source = GetRequiredScriptSource(profile.fontFamily, group);
                SetRoleSource(source, usage, pair.Value);
            }
            profile.legacyFallbackOrder?.Clear();
            profile.legacyAutoUseSingleSourceFont = false;
            profile.enabledLanguages = resolvedLanguages.Values.Distinct().ToList();
            profile.enabledUsages = resolvedLanguages.Keys.Select(entry => entry.usage).Distinct().ToList();
            SynchronizeLanguageEntries(profile);
            EditorUtility.SetDirty(profile);
            return "旧 Locale、源字体与 TMP Fallback 配置已迁入 ES 字体族；Fallback 将由语言身份自动生成。";
        }

        public static ESFontBuildPlan CreateBuildPlan(ESFontBuildProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string outputFolder = NormalizeFolder(profile.outputFolder);
            var entries = (profile.languages ?? new List<ESFontLanguageBuildEntry>())
                .Where(item => item != null).ToList();
            Validate(profile, entries);
            var plan = new ESFontBuildPlan { ProfileId = profile.profileId, OutputFolder = outputFolder };
            foreach (ESFontLanguageBuildEntry entry in entries)
            {
                string characters = CollectCharacters(profile, entry);
                plan.Add(new ESFontBuildPlanEntry
                {
                    Language = entry.language,
                    Usage = entry.usage,
                    ScriptGroup = ResolveScriptGroup(entry.language, entry.usage),
                    SourceFont = ResolveSourceFont(profile, entry),
                    OutputPath = GetOutputPath(profile, entry, outputFolder),
                    UnicodeScalarCount = CountUnicodeScalars(characters),
                    TextSourceCount = CollectTextSources(entry).Count,
                    LocalizationTextCount = CollectLocalizationTexts(profile, entry).Count,
                    InputHash = ComputeInputHash(profile, entry, characters),
                });
            }
            return plan;
        }
        private sealed class FontBuildSnapshot
        {
            public readonly List<AssetFileSnapshot> files = new List<AssetFileSnapshot>();
            public readonly Dictionary<ESFontLanguageBuildEntry, EntryState> entries =
                new Dictionary<ESFontLanguageBuildEntry, EntryState>();
            public ESRuntimeFontCatalog runtimeCatalog;
            public string lastBuildReport;
        }

        private sealed class AssetFileSnapshot
        {
            public string assetPath;
            public bool exists;
            public byte[] bytes;
            public string bytesHash;
            public bool metaExists;
            public byte[] metaBytes;
            public string metaHash;
        }

        private sealed class EntryState
        {
            public TMP_FontAsset outputFont;
            public string lastInputHash;
            public string lastMissingCharacters;
        }

        private static FontBuildSnapshot CaptureSnapshot(
            ESFontBuildProfile profile,
            IReadOnlyCollection<ESFontLanguageBuildEntry> entries,
            string outputFolder)
        {
            var snapshot = new FontBuildSnapshot
            {
                runtimeCatalog = profile.runtimeCatalog,
                lastBuildReport = profile.lastBuildReport
            };
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ESFontLanguageBuildEntry entry in entries)
            {
                snapshot.entries[entry] = new EntryState
                {
                    outputFont = entry.outputFont,
                    lastInputHash = entry.lastInputHash,
                    lastMissingCharacters = entry.lastMissingCharacters
                };
                paths.Add(GetOutputPath(profile, entry, outputFolder));
            }
            paths.Add(outputFolder + "/" + SafeName(profile.profileId) + "_RuntimeFontCatalog.asset");
            foreach (string path in paths)
                snapshot.files.Add(CaptureAssetFile(path));
            return snapshot;
        }

        private static AssetFileSnapshot CaptureAssetFile(string assetPath)
        {
            string absolutePath = GetProjectAssetAbsolutePath(assetPath);
            string metaPath = absolutePath + ".meta";
            byte[] bytes = File.Exists(absolutePath) ? File.ReadAllBytes(absolutePath) : null;
            byte[] metaBytes = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : null;
            return new AssetFileSnapshot
            {
                assetPath = assetPath,
                exists = bytes != null,
                bytes = bytes,
                bytesHash = ComputeSha256(bytes),
                metaExists = metaBytes != null,
                metaBytes = metaBytes,
                metaHash = ComputeSha256(metaBytes)
            };
        }

        private static Exception RestoreSnapshot(ESFontBuildProfile profile, FontBuildSnapshot snapshot)
        {
            try
            {
                foreach (AssetFileSnapshot file in snapshot.files)
                {
                    string absolutePath = GetProjectAssetAbsolutePath(file.assetPath);
                    string metaPath = absolutePath + ".meta";
                    if (file.exists)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
                        File.WriteAllBytes(absolutePath, file.bytes ?? Array.Empty<byte>());
                    }
                    else if (File.Exists(absolutePath))
                    {
                        File.Delete(absolutePath);
                    }
                    if (file.metaExists)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(metaPath));
                        File.WriteAllBytes(metaPath, file.metaBytes ?? Array.Empty<byte>());
                    }
                    else if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (AssetFileSnapshot file in snapshot.files)
                {
                    string absolutePath = GetProjectAssetAbsolutePath(file.assetPath);
                    string metaPath = absolutePath + ".meta";
                    if (File.Exists(absolutePath) != file.exists || File.Exists(metaPath) != file.metaExists)
                        throw new InvalidOperationException("恢复后文件存在性与原始快照不一致：" + file.assetPath);
                    if (file.exists && !string.Equals(ComputeSha256File(absolutePath), file.bytesHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("恢复后资产 SHA-256 与原始快照不一致：" + file.assetPath);
                    if (file.metaExists && !string.Equals(ComputeSha256File(metaPath), file.metaHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("恢复后 .meta SHA-256 与原始快照不一致：" + file.assetPath);
                }

                foreach (KeyValuePair<ESFontLanguageBuildEntry, EntryState> pair in snapshot.entries)
                {
                    string outputPath = GetOutputPath(profile, pair.Key, NormalizeFolder(profile.outputFolder));
                    TMP_FontAsset rebound = pair.Value.outputFont == null
                        ? null
                        : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath);
                    if (pair.Value.outputFont != null && rebound == null)
                        throw new InvalidOperationException("恢复后无法重新绑定字体资产：" + outputPath);
                    pair.Key.outputFont = rebound;
                    pair.Key.lastInputHash = pair.Value.lastInputHash;
                    pair.Key.lastMissingCharacters = pair.Value.lastMissingCharacters;
                }
                string catalogPath = NormalizeFolder(profile.outputFolder) + "/" + SafeName(profile.profileId) + "_RuntimeFontCatalog.asset";
                profile.runtimeCatalog = snapshot.runtimeCatalog == null
                    ? null
                    : AssetDatabase.LoadAssetAtPath<ESRuntimeFontCatalog>(catalogPath);
                if (snapshot.runtimeCatalog != null && profile.runtimeCatalog == null)
                    throw new InvalidOperationException("恢复后无法重新绑定 Runtime Font Catalog：" + catalogPath);
                profile.lastBuildReport = snapshot.lastBuildReport;
                EditorUtility.SetDirty(profile);
                return null;
            }
            catch (Exception recoveryException)
            {
                Debug.LogError("ES 字体构建恢复失败，生成结果可能不完整：" + recoveryException);
                return recoveryException;
            }
        }

        private static string GetProjectAssetAbsolutePath(string assetPath)
        {
            string normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("字体构建恢复路径必须位于 Assets/ 下：" + assetPath);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string absolutePath = Path.GetFullPath(
                Path.Combine(projectRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
            string assetsPrefix = assetsRoot + Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("字体构建恢复路径越出项目 Assets/ 根目录：" + assetPath);
            return absolutePath;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes ?? Array.Empty<byte>())).Replace("-", string.Empty);
        }

        private static string ComputeSha256File(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty);
        }

        public static int UpdateAllProfiles()
        {
            var profiles = AssetDatabase.FindAssets("t:ESFontBuildProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ESFontBuildProfile>)
                .Where(profile => profile != null)
                .OrderBy(profile => AssetDatabase.GetAssetPath(profile), StringComparer.Ordinal)
                .ToList();
            var failures = new List<string>();
            try
            {
                for (int index = 0; index < profiles.Count; index++)
                {
                    var profile = profiles[index];
                    if (EditorUtility.DisplayCancelableProgressBar("ES 字体更新", $"正在更新方案：{profile.profileId}", (float)index / Math.Max(1, profiles.Count))) break;
                    try { Build(profile); }
                    catch (Exception exception) { failures.Add($"{AssetDatabase.GetAssetPath(profile)}: {exception.Message}"); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            if (failures.Count > 0) throw new InvalidOperationException("部分字体方案更新失败：\n" + string.Join("\n", failures));
            return profiles.Count;
        }

        public static string CollectCharacters(ESFontLanguageBuildEntry entry)
        {
            var characters = new SortedSet<uint>();
            if (entry != null)
            {
                AddUnicodeScalars(characters, entry.additionalCharacters);
                foreach (var source in CollectTextSources(entry)) AddUnicodeScalars(characters, source.text);
            }
            return BuildUnicodeString(characters);
        }

        public static string CollectCharacters(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry)
        {
            var characters = new SortedSet<uint>();
            AddUnicodeScalars(characters, CollectCharacters(entry));
            if (profile != null)
                foreach (var localizedText in CollectLocalizationTexts(profile, entry)) AddUnicodeScalars(characters, localizedText);
            return BuildUnicodeString(characters);
        }

        public static IReadOnlyList<TextAsset> CollectTextSources(ESFontLanguageBuildEntry entry)
        {
            if (entry == null) return Array.Empty<TextAsset>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in entry.textSources.Where(item => item != null))
                paths.Add(AssetDatabase.GetAssetPath(source));

            var folders = entry.textFolders
                .Where(folder => folder != null)
                .Select(AssetDatabase.GetAssetPath)
                .Where(AssetDatabase.IsValidFolder)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (folders.Length > 0)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", folders))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
                }
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<TextAsset>)
                .Where(asset => asset != null)
                .ToArray();
        }

        public static void Build(ESFontBuildProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string outputFolder = NormalizeFolder(profile.outputFolder);
            var report = new StringBuilder();
            var entries = (profile.languages ?? new List<ESFontLanguageBuildEntry>()).Where(item => item != null).ToList();
            Validate(profile, entries);
            string expectedCatalogPath = outputFolder + "/" + SafeName(profile.profileId) + "_RuntimeFontCatalog.asset";
            UnityEngine.Object existingCatalogAsset = AssetDatabase.LoadMainAssetAtPath(expectedCatalogPath);
            if (existingCatalogAsset != null && !(existingCatalogAsset is ESRuntimeFontCatalog))
                throw new InvalidOperationException("运行时字体目录路径已被其他资产占用：" + expectedCatalogPath);
            if (profile.runtimeCatalog != null
                && !string.Equals(AssetDatabase.GetAssetPath(profile.runtimeCatalog), expectedCatalogPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("方案的运行时字体目录必须保持在：" + expectedCatalogPath);
            FontBuildSnapshot snapshot = CaptureSnapshot(profile, entries, outputFolder);
            try
            {
                EnsureAssetFolder(outputFolder);
                for (int index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (EditorUtility.DisplayCancelableProgressBar("ES 字体构建", "正在构建 " + GetEntryIdentity(entry), (float)index / entries.Count))
                        throw new OperationCanceledException("用户已取消字体构建。");

                    Font sourceFont = ResolveSourceFont(profile, entry);
                    string chars = CollectCharacters(profile, entry);
                    string inputHash = ComputeInputHash(profile, entry, chars);
                    string assetPath = GetOutputPath(profile, entry, outputFolder);
                    if (entry.outputFont != null && entry.lastInputHash == inputHash && AssetDatabase.GetAssetPath(entry.outputFont) == assetPath)
                    {
                        report.AppendLine("[" + GetEntryIdentity(entry) + "] 输入未变化：" + assetPath);
                        continue;
                    }

                    // TMP only permits TryAddCharacters on a dynamic atlas. The completed asset is switched
                    // back to Static immediately before it is saved, so players never mutate the atlas.
                    var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, profile.SamplingPointSize,
                        profile.AtlasPadding, profile.RenderMode, profile.AtlasWidth, profile.AtlasHeight,
                        AtlasPopulationMode.Dynamic, profile.MultiAtlasSupport);
                    if (fontAsset == null) throw new InvalidOperationException("字体引擎无法为 " + GetEntryIdentity(entry) + " 创建字体资产。");
                    if (!fontAsset.TryAddCharacters(chars, out string missing))
                    {
                        entry.lastMissingCharacters = missing ?? string.Empty;
                        report.AppendLine("[" + GetEntryIdentity(entry) + "] 源字体缺少字形：" + missing);
                    }
                    else
                    {
                        entry.lastMissingCharacters = string.Empty;
                    }
                    fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
                    ReplaceGeneratedAsset(assetPath, fontAsset);
                    entry.outputFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                    entry.lastInputHash = inputHash;
                    report.AppendLine("[" + GetEntryIdentity(entry) + "] 已从 "
                        + CollectTextSources(entry).Count + " 个 TXT 和 "
                        + CollectLocalizationTexts(profile, entry).Count + " 条本地化文本构建 "
                        + CountUnicodeScalars(chars) + " 个唯一 Unicode 标量：" + assetPath);
                }
                BuildFallbacks(profile, report);
                BuildRuntimeCatalog(profile, entries, outputFolder, report);
                profile.lastBuildReport = report.ToString();
                SaveBuildAssets(profile, entries);
            }
            catch (Exception buildException)
            {
                Exception recoveryException = RestoreSnapshot(profile, snapshot);
                if (recoveryException != null)
                    throw new AggregateException("ES 字体构建失败且恢复不完整。", buildException, recoveryException);
                throw;
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static void BuildFallbacks(ESFontBuildProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string outputFolder = NormalizeFolder(profile.outputFolder);
            var entries = (profile.languages ?? new List<ESFontLanguageBuildEntry>()).Where(item => item != null).ToList();
            FontBuildSnapshot snapshot = CaptureSnapshot(profile, entries, outputFolder);
            var report = new StringBuilder();
            try
            {
                BuildFallbacks(profile, report);
                profile.lastBuildReport = report.ToString();
                SaveBuildAssets(profile, entries);
            }
            catch (Exception buildException)
            {
                Exception recoveryException = RestoreSnapshot(profile, snapshot);
                if (recoveryException != null)
                    throw new AggregateException("ES 字体 Fallback 更新失败且恢复不完整。", buildException, recoveryException);
                throw;
            }
        }

        private static void BuildRuntimeCatalog(
            ESFontBuildProfile profile,
            IReadOnlyCollection<ESFontLanguageBuildEntry> entries,
            string outputFolder,
            StringBuilder report)
        {
            string catalogPath = outputFolder + "/" + SafeName(profile.profileId) + "_RuntimeFontCatalog.asset";
            ESRuntimeFontCatalog catalog = profile.runtimeCatalog;
            if (catalog == null)
                catalog = AssetDatabase.LoadAssetAtPath<ESRuntimeFontCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ESRuntimeFontCatalog>();
                try
                {
                    AssetDatabase.CreateAsset(catalog, catalogPath);
                }
                catch
                {
                    if (catalog != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(catalog)))
                        UnityEngine.Object.DestroyImmediate(catalog);
                    throw;
                }
            }
            else if (!string.Equals(AssetDatabase.GetAssetPath(catalog), catalogPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("运行时字体目录必须位于配置的输出目录内：" + AssetDatabase.GetAssetPath(catalog));
            }

            var bindings = new List<ESRuntimeFontBinding>();
            foreach (ESFontLanguageBuildEntry entry in entries)
            {
                if (entry.outputFont == null)
                    throw new InvalidOperationException("生成运行时目录前，字体条目尚无输出：" + GetEntryIdentity(entry));
                if (entry.usage == ESFontUsage.Custom)
                    continue;
                bindings.Add(new ESRuntimeFontBinding
                {
                    language = entry.language,
                    role = ConvertRole(entry.usage),
                    font = entry.outputFont
                });
            }

            catalog.catalogId = profile.profileId;
            catalog.formatVersion = ESRuntimeFontCatalog.CurrentFormatVersion;
            catalog.bindings = bindings;
            IReadOnlyList<string> errors = catalog.Validate();
            if (errors.Count > 0)
                throw new InvalidOperationException("生成的运行时字体目录无效：\n" + string.Join("\n", errors));
            profile.runtimeCatalog = catalog;
            EditorUtility.SetDirty(catalog);
            report.AppendLine("运行时字体目录：" + catalogPath + "（" + bindings.Count + " 个绑定）");
        }

        private static void SaveBuildAssets(
            ESFontBuildProfile profile,
            IReadOnlyCollection<ESFontLanguageBuildEntry> entries)
        {
            if (profile != null)
                AssetDatabase.SaveAssetIfDirty(profile);
            foreach (ESFontLanguageBuildEntry entry in entries ?? Array.Empty<ESFontLanguageBuildEntry>())
                if (entry != null && entry.outputFont != null)
                    AssetDatabase.SaveAssetIfDirty(entry.outputFont);
            if (profile != null)
                AssetDatabase.SaveAssetIfDirty(profile.runtimeCatalog);
        }

        private static ESRuntimeFontRole ConvertRole(ESFontUsage usage)
        {
            switch (usage)
            {
                case ESFontUsage.Title: return ESRuntimeFontRole.Title;
                case ESFontUsage.Number: return ESRuntimeFontRole.Number;
                case ESFontUsage.Icon: return ESRuntimeFontRole.Icon;
                default: return ESRuntimeFontRole.Body;
            }
        }

        private static void BuildFallbacks(ESFontBuildProfile profile, StringBuilder report)
        {
            var entries = (profile.languages ?? new List<ESFontLanguageBuildEntry>()).Where(item => item != null && item.outputFont != null).ToList();
            var byBinding = entries
                .Where(entry => entry.usage != ESFontUsage.Custom)
                .ToDictionary(entry => MakeEntryKey(entry.language, entry.usage), entry => entry);
            var generatedGraph = new Dictionary<TMP_FontAsset, List<TMP_FontAsset>>();
            foreach (var entry in entries)
            {
                var ordered = new List<TMP_FontAsset>();
                foreach (EnumCollect.Envir_LanguageType fallbackLanguage in GetFontFallbackLanguages(entry.language))
                {
                    if (byBinding.TryGetValue(MakeEntryKey(fallbackLanguage, entry.usage), out ESFontLanguageBuildEntry fallbackEntry)
                        && fallbackEntry.outputFont != null && fallbackEntry.outputFont != entry.outputFont
                        && !ordered.Contains(fallbackEntry.outputFont))
                        ordered.Add(fallbackEntry.outputFont);
                }
                generatedGraph[entry.outputFont] = ordered;
            }
            ValidateFallbackGraph(generatedGraph);
            foreach (var entry in entries)
            {
                List<TMP_FontAsset> ordered = generatedGraph[entry.outputFont];
                entry.outputFont.fallbackFontAssetTable = new List<TMP_FontAsset>(ordered);
                EditorUtility.SetDirty(entry.outputFont);
                report.AppendLine("[" + GetEntryIdentity(entry) + "] 自动回退字体数量：" + ordered.Count + "。");
            }
            foreach (var entry in entries)
            {
                int unresolved = ReportFallbackCoverage(profile, entry, report);
                if (unresolved > 0)
                    throw new InvalidOperationException("[" + GetEntryIdentity(entry) + "] 仍有 " + unresolved + " 个字形无法由主字体或自动回退链解析。");
            }
        }

        public static string Preview(ESFontBuildProfile profile)
        {
            if (profile == null) return "尚未选择字体构建方案。";
            var report = new StringBuilder();
            ESFontBuildPlan plan;
            try { plan = CreateBuildPlan(profile); }
            catch (Exception exception)
            {
                return "配置阻断：" + exception.Message;
            }
            report.AppendLine("状态：预检通过，可生成 " + plan.Entries.Count + " 个字体资产。");
            foreach (ESFontBuildPlanEntry entry in plan.Entries)
                report.AppendLine("[" + ESLocaleIdentity.GetDisplayName(entry.Language) + "/" + entry.Usage + "] "
                    + entry.ScriptGroup + "，" + entry.TextSourceCount + " 个 TXT，"
                    + entry.LocalizationTextCount + " 条本地化文本，" + entry.UnicodeScalarCount
                    + " 个 Unicode 标量，输出：" + entry.OutputPath);
            return report.ToString();
        }

        public static void AddUnicodeScalars(ISet<uint> destination, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                uint scalar;
                if (char.IsHighSurrogate(current))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                        throw new InvalidOperationException("字体源文本在索引 " + index + " 处包含未配对的 UTF-16 高代理项。");
                    scalar = (uint)char.ConvertToUtf32(current, value[++index]);
                }
                else if (char.IsLowSurrogate(current))
                {
                    throw new InvalidOperationException("字体源文本在索引 " + index + " 处包含未配对的 UTF-16 低代理项。");
                }
                else
                {
                    if (char.IsControl(current)) continue;
                    scalar = current;
                }
                destination.Add(scalar);
            }
        }

        public static string BuildUnicodeString(IEnumerable<uint> scalars)
        {
            if (scalars == null) return string.Empty;
            var builder = new StringBuilder();
            foreach (uint scalar in scalars)
            {
                if (scalar > 0x10FFFF || scalar >= 0xD800 && scalar <= 0xDFFF)
                    throw new ArgumentOutOfRangeException(nameof(scalars), scalar, "该值不是有效 Unicode 标量。");
                if (scalar <= 0xFFFF)
                {
                    builder.Append((char)scalar);
                    continue;
                }
                uint value = scalar - 0x10000;
                builder.Append((char)(0xD800 + (value >> 10)));
                builder.Append((char)(0xDC00 + (value & 0x3FF)));
            }
            return builder.ToString();
        }

        public static int CountUnicodeScalars(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            var scalars = new HashSet<uint>();
            AddUnicodeScalars(scalars, value);
            return scalars.Count;
        }

        private static string NormalizeFolder(string path)
        {
            string candidate = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
            if (!ESAssetPackagePathSafety.TryNormalizeProjectAssetPath(candidate, out string normalized))
                throw new InvalidOperationException("字体输出目录必须是 Assets/ 下的安全项目路径，不能包含绝对路径或 ..。");
            return normalized;
        }

        private static void Validate(ESFontBuildProfile profile, IReadOnlyCollection<ESFontLanguageBuildEntry> entries)
        {
            if (string.IsNullOrWhiteSpace(profile.profileId)
                || profile.profileId.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new InvalidOperationException("字体方案 ID 必须是稳定名称，且不能包含路径分隔符。");
            if (!Enum.IsDefined(typeof(ESFontGenerationQuality), profile.generationQuality))
                throw new InvalidOperationException("字体生成质量包含未声明的枚举值。");
            ValidateLocalizationCatalogs(profile);
            if (entries.Count == 0) throw new InvalidOperationException("至少需要一个语言与字体角色条目。");
            if (entries.Any(entry => !string.IsNullOrWhiteSpace(entry.legacyLanguageCode)
                || entry.legacySourceFont != null
                || entry.legacyFallbackOverride != null && entry.legacyFallbackOverride.Count > 0)
                || profile.legacyFallbackOrder != null && profile.legacyFallbackOrder.Count > 0
                || profile.legacyAutoUseSingleSourceFont)
                throw new InvalidOperationException("字体方案包含旧 Locale、源字体或 TMP Fallback 数据，请先执行显式迁移。");
            IReadOnlyList<EnumCollect.Envir_LanguageType> enabledLanguages = GetEnabledLanguages(profile);
            IReadOnlyList<ESFontUsage> enabledUsages = GetEnabledUsages(profile);
            int expectedEntryCount = enabledLanguages.Count * enabledUsages.Count;
            if (entries.Count != expectedEntryCount)
                throw new InvalidOperationException("语言与角色配置未同步：期望 " + expectedEntryCount
                    + " 项，实际 " + entries.Count + " 项。请点击“同步十语言方案”。");
            var expectedBindings = new HashSet<int>(enabledLanguages
                .SelectMany(language => enabledUsages.Select(usage => MakeEntryKey(language, usage))));
            var actualBindings = new HashSet<int>(entries.Select(entry => MakeEntryKey(entry.language, entry.usage)));
            if (!expectedBindings.SetEquals(actualBindings))
                throw new InvalidOperationException("语言与角色配置身份已经漂移，请点击“同步语言与角色”重建准确组合。");
            var duplicateBindings = entries
                .Where(entry => entry.usage != ESFontUsage.Custom)
                .GroupBy(entry => MakeEntryKey(entry.language, entry.usage))
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateBindings.Count > 0)
                throw new InvalidOperationException("运行时字体绑定重复：" + string.Join(", ", duplicateBindings));
            ValidateFontFamily(profile.fontFamily);
            ESFontLanguageBuildEntry missingSource = entries.FirstOrDefault(entry => ResolveSourceFont(profile, entry) == null);
            if (missingSource != null)
                throw new InvalidOperationException("字体族缺少源字体：" + GetEntryIdentity(missingSource)
                    + " 需要“" + ResolveScriptGroup(missingSource.language, missingSource.usage) + "”字体。");
            if (entries.Any(entry => string.IsNullOrEmpty(CollectCharacters(profile, entry))))
                throw new InvalidOperationException("每个启用条目都必须从 TXT、ES 本地化目录、可选 Unity Localization 表或补充字符中收集到至少一个字符。");
            var duplicatePaths = entries.GroupBy(entry => GetOutputPath(profile, entry, NormalizeFolder(profile.outputFolder)), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).ToList();
            if (duplicatePaths.Count > 0) throw new InvalidOperationException("多个条目解析到了同一输出字体路径，请调整角色、语言或输出名称。");
        }

        private static void ValidateLocalizationCatalogs(ESFontBuildProfile profile)
        {
            var seen = new HashSet<ESLocalizationCatalog>();
            foreach (ESLocalizationCatalog catalog in profile.localizationCatalogs
                ?? new List<ESLocalizationCatalog>())
            {
                if (catalog == null)
                    throw new InvalidOperationException("ES 本地化目录列表中存在空引用。");
                if (!seen.Add(catalog))
                    throw new InvalidOperationException("ES 本地化目录重复绑定：" + AssetDatabase.GetAssetPath(catalog));
                IReadOnlyList<string> errors = catalog.Validate();
                if (errors.Count > 0)
                    throw new InvalidOperationException("ES 本地化目录未通过校验："
                        + AssetDatabase.GetAssetPath(catalog) + "\n" + string.Join("\n", errors));
            }
        }

        private static void ValidateFallbackGraph(
            IReadOnlyDictionary<TMP_FontAsset, List<TMP_FontAsset>> generatedGraph)
        {
            var graph = new Dictionary<TMP_FontAsset, List<TMP_FontAsset>>();
            foreach (KeyValuePair<TMP_FontAsset, List<TMP_FontAsset>> pair in generatedGraph)
            {
                graph[pair.Key] = pair.Value.Where(font => font != null && font != pair.Key).Distinct().ToList();
                foreach (TMP_FontAsset fallback in graph[pair.Key])
                    AddExternalFallbackGraph(fallback, graph);
            }
            var visiting = new HashSet<TMP_FontAsset>();
            var visited = new HashSet<TMP_FontAsset>();
            foreach (var font in graph.Keys) Visit(font, graph, visiting, visited);
        }

        private static void AddExternalFallbackGraph(
            TMP_FontAsset font,
            IDictionary<TMP_FontAsset, List<TMP_FontAsset>> graph)
        {
            if (font == null || graph.ContainsKey(font)) return;
            List<TMP_FontAsset> children = font.fallbackFontAssetTable == null
                ? new List<TMP_FontAsset>()
                : font.fallbackFontAssetTable.Where(item => item != null && item != font).Distinct().ToList();
            graph.Add(font, children);
            foreach (TMP_FontAsset child in children)
                AddExternalFallbackGraph(child, graph);
        }

        private static void Visit(TMP_FontAsset font, IReadOnlyDictionary<TMP_FontAsset, List<TMP_FontAsset>> graph, ISet<TMP_FontAsset> visiting, ISet<TMP_FontAsset> visited)
        {
            if (visited.Contains(font)) return;
            if (!visiting.Add(font)) throw new InvalidOperationException("生成字体之间的 Fallback 链存在循环。");
            if (graph.TryGetValue(font, out var children)) foreach (var child in children) Visit(child, graph, visiting, visited);
            visiting.Remove(font);
            visited.Add(font);
        }

        private static int ReportFallbackCoverage(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry, StringBuilder report)
        {
            int missingCount = 0;
            var sample = new StringBuilder();
            var visited = new HashSet<TMP_FontAsset>();
            var scalars = new SortedSet<uint>();
            AddUnicodeScalars(scalars, CollectCharacters(profile, entry));
            foreach (uint scalar in scalars)
            {
                visited.Clear();
                if (CanResolveUnicodeScalar(entry.outputFont, scalar, visited)) continue;
                missingCount++;
                if (sample.Length < 80) sample.Append(BuildUnicodeString(new[] { scalar }));
            }
            report.AppendLine(missingCount == 0
                ? "[" + GetEntryIdentity(entry) + "] 自动回退链覆盖完整。"
                : "[" + GetEntryIdentity(entry) + "] 自动回退后仍有 " + missingCount + " 个字形无法解析，示例：" + sample);
            return missingCount;
        }

        public static bool CanResolveUnicodeScalar(TMP_FontAsset font, uint scalar, ISet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font)) return false;
            if (font.characterLookupTable != null && font.characterLookupTable.ContainsKey(scalar)) return true;
            if (font.fallbackFontAssetTable == null) return false;
            foreach (TMP_FontAsset fallback in font.fallbackFontAssetTable)
                if (CanResolveUnicodeScalar(fallback, scalar, visited)) return true;
            return false;
        }

        private static string GetOutputPath(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry, string outputFolder)
        {
            string languageCode = ESLocaleIdentity.GetCode(entry.language);
            string name = string.IsNullOrWhiteSpace(entry.outputName)
                ? SafeName(profile.profileId) + "_" + SafeName(entry.usage.ToString()) + "_" + SafeName(languageCode)
                : SafeName(entry.outputName);
            return outputFolder + "/" + SafeName(profile.profileId) + "/" + SafeName(entry.usage.ToString()) + "/" + SafeName(languageCode) + "/" + name + ".asset";
        }

        private static void ReplaceGeneratedAsset(string assetPath, TMP_FontAsset fontAsset)
        {
            EnsureAssetFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));
            fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            TMP_FontAsset storedAsset;
            if (existing != null)
            {
                // Keep the main .asset and its GUID stable. Runtime/theme references therefore remain valid
                // across a rebuild. Existing sub-assets are retained until the new generated objects have
                // been attached successfully, so a failed rebuild cannot destroy the last good atlas.
                string existingSnapshot = EditorJsonUtility.ToJson(existing);
                TMP_FontAsset generatedAsset = fontAsset;
                bool generatedSubAssetsAttached = false;
                try
                {
                    EditorUtility.CopySerialized(generatedAsset, existing);
                    existing.name = generatedAsset.name;
                    storedAsset = existing;
                    AttachGeneratedSubAssets(storedAsset);
                    generatedSubAssetsAttached = true;

                    foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    {
                        if (subAsset != null && subAsset != storedAsset
                            && !IsGeneratedSubAsset(storedAsset, subAsset))
                            UnityEngine.Object.DestroyImmediate(subAsset, true);
                    }
                }
                catch
                {
                    EditorJsonUtility.FromJsonOverwrite(existingSnapshot, existing);
                    EditorUtility.SetDirty(existing);
                    throw;
                }
                finally
                {
                    if (generatedAsset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(generatedAsset)))
                        UnityEngine.Object.DestroyImmediate(generatedAsset, !generatedSubAssetsAttached);
                }
                return;
            }
            else if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                throw new InvalidOperationException($"拒绝替换非字体资产：{assetPath}");
            }
            else
            {
                try
                {
                    AssetDatabase.CreateAsset(fontAsset, assetPath);
                }
                catch
                {
                    if (fontAsset != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(fontAsset)))
                        UnityEngine.Object.DestroyImmediate(fontAsset, true);
                    throw;
                }
                storedAsset = fontAsset;
            }
            AttachGeneratedSubAssets(storedAsset);
        }

        private static void AttachGeneratedSubAssets(TMP_FontAsset storedAsset)
        {
            var attached = new List<UnityEngine.Object>();
            try
            {
                foreach (var texture in storedAsset.atlasTextures.Where(texture => texture != null))
                {
                    texture.name = storedAsset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(texture, storedAsset);
                    attached.Add(texture);
                }
                if (storedAsset.material != null)
                {
                    storedAsset.material.name = storedAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(storedAsset.material, storedAsset);
                    attached.Add(storedAsset.material);
                }
                EditorUtility.SetDirty(storedAsset);
            }
            catch
            {
                for (int index = attached.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object subAsset = attached[index];
                    if (subAsset == null) continue;
                    try { AssetDatabase.RemoveObjectFromAsset(subAsset); } catch { }
                    try { UnityEngine.Object.DestroyImmediate(subAsset, true); } catch { }
                }
                throw;
            }
        }

        private static bool IsGeneratedSubAsset(TMP_FontAsset storedAsset, UnityEngine.Object candidate)
        {
            if (candidate == null || storedAsset == null) return false;
            if (storedAsset.material == candidate) return true;
            return storedAsset.atlasTextures != null && storedAsset.atlasTextures.Any(texture => texture == candidate);
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            var parts = assetFolder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        private static Font ResolveSourceFont(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry)
        {
            if (profile?.fontFamily == null || entry == null) return null;
            ESFontScriptGroup group = ResolveScriptGroup(entry.language, entry.usage);
            ESFontScriptSource source = profile.fontFamily.sources?
                .FirstOrDefault(item => item != null && item.scriptGroup == group);
            if (source == null) return null;
            ESFontRoleSourceOverride roleOverride = source.roleOverrides?
                .FirstOrDefault(item => item != null && item.usage == entry.usage);
            return roleOverride?.sourceFont != null ? roleOverride.sourceFont : source.defaultFont;
        }

        public static ESFontScriptGroup ResolveScriptGroup(
            EnumCollect.Envir_LanguageType language,
            ESFontUsage usage)
        {
            if (usage == ESFontUsage.Icon) return ESFontScriptGroup.Symbols;
            if (usage == ESFontUsage.Number) return ESFontScriptGroup.Latin;
            switch (language)
            {
                case EnumCollect.Envir_LanguageType.ChineseSimplified: return ESFontScriptGroup.ChineseSimplified;
                case EnumCollect.Envir_LanguageType.ChineseTraditional: return ESFontScriptGroup.ChineseTraditional;
                case EnumCollect.Envir_LanguageType.Japanese: return ESFontScriptGroup.Japanese;
                case EnumCollect.Envir_LanguageType.Korean: return ESFontScriptGroup.Korean;
                case EnumCollect.Envir_LanguageType.Russian: return ESFontScriptGroup.Cyrillic;
                default: return ESFontScriptGroup.Latin;
            }
        }

        public static string GetEntryIdentity(ESFontLanguageBuildEntry entry)
        {
            return entry == null
                ? "空字体条目"
                : ESLocaleIdentity.GetDisplayName(entry.language) + "（"
                    + ESLocaleIdentity.GetCode(entry.language) + "）/" + entry.usage;
        }

        private static IReadOnlyList<EnumCollect.Envir_LanguageType> GetEnabledLanguages(
            ESFontBuildProfile profile)
        {
            var result = (profile.enabledLanguages ?? new List<EnumCollect.Envir_LanguageType>()).ToList();
            if (result.Count == 0)
                throw new InvalidOperationException("字体方案至少需要启用一种语言。");
            if (result.Any(language => !ESLocalizationRuntime.IsConcreteLanguage(language)))
                throw new InvalidOperationException("启用语言只能包含 ES 已声明的具体语言，不能使用 NotClear。");
            if (result.Distinct().Count() != result.Count)
                throw new InvalidOperationException("启用语言存在重复身份。");
            return result;
        }

        private static IReadOnlyList<ESFontUsage> GetEnabledUsages(ESFontBuildProfile profile)
        {
            var result = (profile.enabledUsages ?? new List<ESFontUsage>()).ToList();
            if (result.Count == 0)
                throw new InvalidOperationException("字体方案至少需要启用一个字体角色。");
            if (result.Any(usage => !Enum.IsDefined(typeof(ESFontUsage), usage)))
                throw new InvalidOperationException("启用字体角色包含未声明的枚举值。");
            if (result.Distinct().Count() != result.Count)
                throw new InvalidOperationException("启用字体角色存在重复身份。");
            return result;
        }

        private static void ValidateFontFamily(ESFontFamilyDefinition family)
        {
            if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                throw new InvalidOperationException("字体方案缺少稳定的 ES 字体族 ID。");
            if (!string.Equals(family.familyId, family.familyId.Trim(), StringComparison.Ordinal)
                || family.familyId.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new InvalidOperationException("ES 字体族 ID 不能包含首尾空白或路径分隔符。");
            var seenGroups = new HashSet<ESFontScriptGroup>();
            foreach (ESFontScriptSource source in family.sources ?? new List<ESFontScriptSource>())
            {
                if (source == null)
                    throw new InvalidOperationException("ES 字体族包含空文字类型配置。");
                if (!seenGroups.Add(source.scriptGroup))
                    throw new InvalidOperationException("ES 字体族文字类型重复：" + source.scriptGroup);
                var seenUsages = new HashSet<ESFontUsage>();
                foreach (ESFontRoleSourceOverride role in source.roleOverrides ?? new List<ESFontRoleSourceOverride>())
                {
                    if (role == null || role.sourceFont == null)
                        throw new InvalidOperationException("ES 字体族角色专用字体包含空配置：" + source.scriptGroup);
                    if (!seenUsages.Add(role.usage))
                        throw new InvalidOperationException("ES 字体族角色专用字体重复：" + source.scriptGroup + "/" + role.usage);
                    ValidateSourceFontPath(role.sourceFont);
                }
                if (source.defaultFont != null) ValidateSourceFontPath(source.defaultFont);
            }
        }

        private static void ValidateSourceFontPath(Font font)
        {
            string path = AssetDatabase.GetAssetPath(font).Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("源字体必须位于项目 Assets/ 内，不能依赖 PackageCache、系统字体或项目外路径：" + path);
        }

        private static void EnsureStandardFontSources(ESFontFamilyDefinition family)
        {
            family.sources = family.sources ?? new List<ESFontScriptSource>();
            var seen = new HashSet<ESFontScriptGroup>();
            foreach (ESFontScriptSource source in family.sources.Where(item => item != null))
                if (!seen.Add(source.scriptGroup))
                    throw new InvalidOperationException("ES 字体族文字类型重复：" + source.scriptGroup);
            foreach (ESFontScriptGroup group in Enum.GetValues(typeof(ESFontScriptGroup)))
            {
                if (seen.Contains(group)) continue;
                family.sources.Add(new ESFontScriptSource { scriptGroup = group });
            }
        }

        private static ESFontScriptSource GetRequiredScriptSource(
            ESFontFamilyDefinition family,
            ESFontScriptGroup group)
        {
            ESFontScriptSource source = family.sources.FirstOrDefault(item => item != null && item.scriptGroup == group);
            if (source == null)
                throw new InvalidOperationException("ES 字体族缺少文字类型配置：" + group);
            return source;
        }

        private static void SetRoleSource(ESFontScriptSource source, ESFontUsage usage, Font font)
        {
            source.roleOverrides = source.roleOverrides ?? new List<ESFontRoleSourceOverride>();
            ESFontRoleSourceOverride role = source.roleOverrides.FirstOrDefault(item => item != null && item.usage == usage);
            if (role == null)
            {
                role = new ESFontRoleSourceOverride { usage = usage };
                source.roleOverrides.Add(role);
            }
            role.sourceFont = font;
        }

        private static Font FindManagedFont(
            IReadOnlyDictionary<string, Font> byName,
            params string[] stableNames)
        {
            Font selected = null;
            foreach (string stableName in stableNames)
            {
                if (!byName.TryGetValue(stableName, out Font candidate)) continue;
                if (selected != null && selected != candidate)
                    throw new InvalidOperationException("同一字体槽位存在多个稳定命名候选："
                        + string.Join("、", stableNames));
                selected = candidate;
            }
            return selected;
        }

        private static IEnumerable<EnumCollect.Envir_LanguageType> GetFontFallbackLanguages(
            EnumCollect.Envir_LanguageType language)
        {
            if (language == EnumCollect.Envir_LanguageType.English)
                yield break;
            if (language == EnumCollect.Envir_LanguageType.ChineseTraditional)
            {
                yield return EnumCollect.Envir_LanguageType.ChineseSimplified;
                yield return EnumCollect.Envir_LanguageType.English;
                yield break;
            }
            yield return EnumCollect.Envir_LanguageType.English;
            if (language != EnumCollect.Envir_LanguageType.ChineseSimplified)
                yield return EnumCollect.Envir_LanguageType.ChineseSimplified;
        }

        private static int MakeEntryKey(EnumCollect.Envir_LanguageType language, ESFontUsage usage)
            => ((int)language << 8) | (int)usage;

        private static int MakeSourceKey(ESFontScriptGroup group, ESFontUsage usage)
            => ((int)group << 8) | (int)usage;

        // Unity Localization is intentionally read by reflection: this Editor-only tool must work in projects
        // that have not installed com.unity.localization. When installed, StringTableCollection.StringTables
        // exposes locale tables whose LocalizedValue values are added to the matching language entry.
        private static IReadOnlyList<string> CollectLocalizationTexts(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry)
        {
            if (profile == null || entry == null) return Array.Empty<string>();
            var result = new List<string>();
            if (ESLocalizationRuntime.IsConcreteLanguage(entry.language))
            {
                foreach (ESLocalizationCatalog catalog in (profile.localizationCatalogs
                    ?? new List<ESLocalizationCatalog>()).Where(item => item != null))
                {
                    foreach (ESLocalizationCatalogEntry localizedEntry in catalog.entries
                        ?? new List<ESLocalizationCatalogEntry>())
                    {
                        if (localizedEntry != null && localizedEntry.language == entry.language
                            && !string.IsNullOrEmpty(localizedEntry.value))
                            result.Add(localizedEntry.value);
                    }
                }
            }
            foreach (var collection in (profile.localizationTableCollections
                ?? new List<UnityEngine.Object>()).Where(item => item != null))
            {
                foreach (var tableItem in EnumerateMember(collection, "StringTables", "Tables"))
                {
                    object table = GetMember(tableItem, "Table") ?? tableItem;
                    string localeCode = GetLocaleCode(table);
                    if (!string.IsNullOrEmpty(localeCode)
                        && !MatchesLocale(localeCode, ESLocaleIdentity.GetCode(entry.language))) continue;
                    foreach (var entryItem in EnumerateMember(table, "Values", "TableEntries", "Entries"))
                    {
                        object localizedEntry = GetMember(entryItem, "Value") ?? entryItem;
                        string value = GetMember(localizedEntry, "LocalizedValue") as string
                            ?? GetMember(localizedEntry, "Value") as string
                            ?? GetMember(localizedEntry, "Text") as string;
                        if (!string.IsNullOrEmpty(value)) result.Add(value);
                    }
                }
            }
            return result;
        }

        private static bool MatchesLocale(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return true;
            return ESLocaleIdentity.TryParse(left, out EnumCollect.Envir_LanguageType leftLanguage)
                && ESLocaleIdentity.TryParse(right, out EnumCollect.Envir_LanguageType rightLanguage)
                && leftLanguage == rightLanguage;
        }

        private static IEnumerable<object> EnumerateMember(object target, params string[] memberNames)
        {
            foreach (var memberName in memberNames)
            {
                object value = GetMember(target, memberName);
                if (value is IEnumerable enumerable && !(value is string))
                {
                    foreach (var item in enumerable) if (item != null) yield return item;
                    yield break;
                }
            }
        }

        private static string GetLocaleCode(object table)
        {
            object localeIdentifier = GetMember(table, "LocaleIdentifier") ?? GetMember(table, "Locale");
            return GetMember(localeIdentifier, "Code") as string ?? localeIdentifier?.ToString();
        }

        private static object GetMember(object target, string memberName)
        {
            if (target == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
            var type = target.GetType();
            var property = type.GetProperty(memberName, flags);
            if (property != null && property.GetIndexParameters().Length == 0) return property.GetValue(target);
            var field = type.GetField(memberName, flags);
            return field?.GetValue(target);
        }

        private static string ComputeInputHash(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry, string characters)
        {
            Font sourceFont = ResolveSourceFont(profile, entry);
            string sourcePath = AssetDatabase.GetAssetPath(sourceFont);
            string input = sourcePath + "|" + AssetDatabase.GetAssetDependencyHash(sourcePath) + "|" + string.Join("|", CollectTextSources(entry).Select(AssetDatabase.GetAssetPath)) + "|" + characters
                + "|" + profile.SamplingPointSize + "|" + profile.AtlasPadding + "|" + profile.AtlasWidth
                + "|" + profile.AtlasHeight + "|" + profile.RenderMode + "|" + profile.MultiAtlasSupport;
            using (var hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(input))).Replace("-", string.Empty);
            }
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Font";
            return string.Concat(value.Select(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_'));
        }
    }

    /// <summary>
    /// Luban text-table adapter. Kept Editor-only and dependency-free at runtime: the generated
    /// ScriptableObject is the only runtime input, so no JSON or AssetDatabase access leaks into ES_Logic.
    /// </summary>
    public static class ESLocalizationCatalogEditor
    {
        private const string LubanTextTablePath = "Assets/Plugins/ES/Generated/Luban/Json/es_tbtext.json";
        private const string CatalogFolder = "Assets/ESNormalAssets/Localization";
        private const string CatalogPath = CatalogFolder + "/ESLocalizationCatalog.asset";

        public static IReadOnlyList<string> ValidateSource(ESLocalizationCatalog catalog)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("本地化目录为空。");
                return errors;
            }
            string sourceId = (catalog.sourceId ?? string.Empty).Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                errors.Add("本地化目录缺少源表 sourceId，无法确认是否脱离生成源。");
                return errors;
            }
            if (!sourceId.StartsWith("Assets/", StringComparison.Ordinal)
                || sourceId.Contains("../", StringComparison.Ordinal)
                || sourceId.Contains("\\..", StringComparison.Ordinal))
            {
                errors.Add("本地化目录 sourceId 必须是项目内 Assets/ 相对路径：" + sourceId);
                return errors;
            }
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, sourceId.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                errors.Add("本地化目录源表不存在：" + sourceId);
                return errors;
            }
            string actualHash = ComputeSha256(File.ReadAllBytes(absolutePath));
            if (!string.Equals(actualHash, catalog.sourceHash, StringComparison.OrdinalIgnoreCase))
                errors.Add("本地化目录源表已漂移：" + sourceId + "（目录 Hash 与当前源表不一致）");
            return errors;
        }

        [MenuItem("【ES】/资源与发布/多语言/从 Luban 文本表生成目录", priority = 120)]
        public static void BuildLubanTextCatalog()
        {
            if (!File.Exists(LubanTextTablePath))
                throw new InvalidOperationException("找不到 Luban 文本表：" + LubanTextTablePath);

            JArray source = JArray.Parse(File.ReadAllText(LubanTextTablePath, Encoding.UTF8));
            var candidate = new List<ESLocalizationCatalogEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken token in source)
            {
                string key = token.Value<string>("key");
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("Luban 文本表存在空 key。");
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.ChineseSimplified,
                    GetLocaleValue(token, "zhCN", "zhHans", "zh-CN", "zh-Hans"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.English,
                    GetLocaleValue(token, "enUS", "en", "en-US"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.Japanese,
                    GetLocaleValue(token, "jaJP", "ja", "ja-JP"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.ChineseTraditional,
                    GetLocaleValue(token, "zhTW", "zhHant", "zh-TW", "zh-Hant"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.Korean,
                    GetLocaleValue(token, "koKR", "ko", "ko-KR"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.French,
                    GetLocaleValue(token, "frFR", "fr", "fr-FR"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.German,
                    GetLocaleValue(token, "deDE", "de", "de-DE"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.Spanish,
                    GetLocaleValue(token, "esES", "es", "es-ES"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.PortugueseBrazil,
                    GetLocaleValue(token, "ptBR", "pt", "pt-BR"));
                AddLocale(candidate, seen, key, EnumCollect.Envir_LanguageType.Russian,
                    GetLocaleValue(token, "ruRU", "ru", "ru-RU"));
            }
            if (candidate.Count == 0)
                throw new InvalidOperationException("Luban 文本表没有可生成的本地化条目。");

            string sourceHash = ComputeSha256(File.ReadAllBytes(LubanTextTablePath));
            var validationCatalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
            validationCatalog.catalogId = "luban_text";
            validationCatalog.formatVersion = ESLocalizationCatalog.CurrentFormatVersion;
            validationCatalog.defaultLanguage = EnumCollect.Envir_LanguageType.ChineseSimplified;
            validationCatalog.sourceId = LubanTextTablePath;
            validationCatalog.sourceHash = sourceHash;
            validationCatalog.entries = candidate;
            IReadOnlyList<string> validationErrors = validationCatalog.Validate();
            UnityEngine.Object.DestroyImmediate(validationCatalog);
            if (validationErrors.Count > 0)
                throw new InvalidOperationException("生成的本地化目录无效：\n" + string.Join("\n", validationErrors));

            EnsureAssetFolder(CatalogFolder);
            ESLocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<ESLocalizationCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ESLocalizationCatalog>();
                try
                {
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }
                catch
                {
                    if (catalog != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(catalog)))
                        UnityEngine.Object.DestroyImmediate(catalog);
                    throw;
                }
            }

            Undo.RecordObject(catalog, "生成 ES 本地化目录");
            catalog.catalogId = "luban_text";
            catalog.formatVersion = ESLocalizationCatalog.CurrentFormatVersion;
            catalog.defaultLanguage = EnumCollect.Envir_LanguageType.ChineseSimplified;
            catalog.sourceId = LubanTextTablePath;
            catalog.sourceHash = sourceHash;
            catalog.entries = candidate;
            catalog.InvalidateIndex();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
            Selection.activeObject = catalog;
            EditorUtility.DisplayDialog("ES 本地化目录", "已生成：" + CatalogPath + "\n条目数：" + candidate.Count, "确定");
        }

        private static string GetLocaleValue(JToken token, params string[] propertyNames)
        {
            if (token == null || propertyNames == null) return null;
            foreach (string propertyName in propertyNames)
            {
                JToken value = token[propertyName];
                if (value != null && value.Type != JTokenType.Null)
                {
                    string text = value.Value<string>();
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
            return null;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes ?? Array.Empty<byte>())).Replace("-", string.Empty);
        }

        private static void AddLocale(
            ICollection<ESLocalizationCatalogEntry> destination,
            ISet<string> seen,
            string key,
            EnumCollect.Envir_LanguageType language,
            string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            string identity = key.Trim() + "|" + language;
            if (!seen.Add(identity))
                throw new InvalidOperationException("Luban 文本表存在重复条目：" + identity);
            destination.Add(new ESLocalizationCatalogEntry
            {
                textKey = key.Trim(),
                language = language,
                value = value
            });
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
