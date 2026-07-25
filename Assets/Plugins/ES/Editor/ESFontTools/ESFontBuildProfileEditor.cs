using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ES
{
    public static class ESFontBuildProfileEditor
    {
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
                    if (EditorUtility.DisplayCancelableProgressBar("ES Font Update", $"Updating profile: {profile.profileId}", (float)index / Math.Max(1, profiles.Count))) break;
                    try { Build(profile); }
                    catch (Exception exception) { failures.Add($"{AssetDatabase.GetAssetPath(profile)}: {exception.Message}"); }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            if (failures.Count > 0) throw new InvalidOperationException("Some font profiles could not be updated:\n" + string.Join("\n", failures));
            return profiles.Count;
        }

        public static string CollectCharacters(ESFontLanguageBuildEntry entry)
        {
            var characters = new SortedSet<char>();
            if (entry != null)
            {
                Add(characters, entry.additionalCharacters);
                foreach (var source in CollectTextSources(entry)) Add(characters, source.text);
            }
            return new string(characters.ToArray());
        }

        public static string CollectCharacters(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry)
        {
            var characters = new SortedSet<char>();
            Add(characters, CollectCharacters(entry));
            if (profile != null)
                foreach (var localizedText in CollectLocalizationTexts(profile, entry)) Add(characters, localizedText);
            return new string(characters.ToArray());
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
            EnsureAssetFolder(outputFolder);
            var report = new StringBuilder();
            var entries = profile.languages.Where(item => item != null).ToList();
            Validate(profile, entries);
            try
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    var entry = entries[index];
                    if (EditorUtility.DisplayCancelableProgressBar("ES Font Build", $"Building {entry.usage}/{entry.languageCode}", (float)index / entries.Count))
                        throw new OperationCanceledException("Font build cancelled by user.");

                    Font sourceFont = ResolveSourceFont(profile, entry);
                    string chars = CollectCharacters(profile, entry);
                    string inputHash = ComputeInputHash(profile, entry, chars);
                    string assetPath = GetOutputPath(profile, entry, outputFolder);
                    if (entry.outputFont != null && entry.lastInputHash == inputHash && AssetDatabase.GetAssetPath(entry.outputFont) == assetPath)
                    {
                        report.AppendLine($"[{entry.usage}/{entry.languageCode}] unchanged: {assetPath}");
                        continue;
                    }

                    // TMP only permits TryAddCharacters on a dynamic atlas. The completed asset is switched
                    // back to Static immediately before it is saved, so players never mutate the atlas.
                    var fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont, profile.samplingPointSize, profile.atlasPadding, profile.renderMode, profile.AtlasWidth, profile.AtlasHeight, AtlasPopulationMode.Dynamic, profile.enableMultiAtlasSupport);
                    if (fontAsset == null) throw new InvalidOperationException($"TMP could not create a font asset for {entry.usage}/{entry.languageCode}.");
                    if (!fontAsset.TryAddCharacters(chars, out string missing)) report.AppendLine($"[{entry.usage}/{entry.languageCode}] source font missing glyphs: {missing}");
                    fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
                    ReplaceGeneratedAsset(assetPath, fontAsset);
                    entry.outputFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                    entry.lastInputHash = inputHash;
                    report.AppendLine($"[{entry.usage}/{entry.languageCode}] built {chars.Length} unique characters from {CollectTextSources(entry).Count} TXT files and {CollectLocalizationTexts(profile, entry).Count} localization entries: {assetPath}");
                }
                BuildFallbacks(profile, report);
                profile.lastBuildReport = report.ToString();
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        public static void BuildFallbacks(ESFontBuildProfile profile)
        {
            var report = new StringBuilder();
            BuildFallbacks(profile, report);
            profile.lastBuildReport = report.ToString();
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static void BuildFallbacks(ESFontBuildProfile profile, StringBuilder report)
        {
            var entries = profile.languages.Where(item => item != null && item.outputFont != null).ToList();
            ValidateFallbackGraph(profile, entries);
            foreach (var entry in entries)
            {
                var configured = entry.fallbackOverride != null && entry.fallbackOverride.Count > 0 ? entry.fallbackOverride : profile.fallbackOrder;
                var ordered = configured.Where(item => item != null && item != entry.outputFont).Distinct().ToList();
                entry.outputFont.fallbackFontAssetTable = ordered.Where(font => font != entry.outputFont).ToList();
                EditorUtility.SetDirty(entry.outputFont);
                report.AppendLine($"[{entry.usage}/{entry.languageCode}] fallback count: {ordered.Count}.");
                ReportFallbackCoverage(profile, entry, report);
            }
        }

        public static string Preview(ESFontBuildProfile profile)
        {
            if (profile == null) return "No profile selected.";
            var report = new StringBuilder();
            var entries = profile.languages.Where(item => item != null).ToList();
            try { Validate(profile, entries); }
            catch (Exception exception) { report.AppendLine("Configuration error: " + exception.Message); }
            foreach (var entry in entries)
            {
                string characters = CollectCharacters(profile, entry);
                report.AppendLine($"[{entry.usage}/{entry.languageCode}] {CollectTextSources(entry).Count} TXT files, {CollectLocalizationTexts(profile, entry).Count} localization entries, {characters.Length} unique characters, output: {GetOutputPath(profile, entry, NormalizeFolder(profile.outputFolder))}");
            }
            return report.ToString();
        }

        private static void Add(ISet<char> destination, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            foreach (char character in value) if (!char.IsControl(character)) destination.Add(character);
        }

        private static string NormalizeFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("Font output folder must be under Assets/.");
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private static void Validate(ESFontBuildProfile profile, IReadOnlyCollection<ESFontLanguageBuildEntry> entries)
        {
            if (entries.Count == 0) throw new InvalidOperationException("Add at least one language/font entry.");
            if (entries.Any(entry => ResolveSourceFont(profile, entry) == null)) throw new InvalidOperationException("Every entry needs a source font. Assign one directly, or put exactly one licensed Font asset in the profile sourceFontFolder.");
            if (entries.Any(entry => string.IsNullOrEmpty(CollectCharacters(profile, entry)))) throw new InvalidOperationException("Every enabled entry must collect at least one character from TXT folders/files, Localization, or additional characters.");
            var duplicatePaths = entries.GroupBy(entry => GetOutputPath(profile, entry, NormalizeFolder(profile.outputFolder)), StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).ToList();
            if (duplicatePaths.Count > 0) throw new InvalidOperationException("Two entries resolve to the same output font path. Set different usage, language, or outputName.");
        }

        private static void ValidateFallbackGraph(ESFontBuildProfile profile, IReadOnlyCollection<ESFontLanguageBuildEntry> entries)
        {
            var generated = new HashSet<TMP_FontAsset>(entries.Select(entry => entry.outputFont));
            var graph = new Dictionary<TMP_FontAsset, List<TMP_FontAsset>>();
            foreach (var entry in entries)
            {
                var configured = entry.fallbackOverride != null && entry.fallbackOverride.Count > 0 ? entry.fallbackOverride : profile.fallbackOrder;
                graph[entry.outputFont] = configured.Where(font => font != null && font != entry.outputFont && generated.Contains(font)).Distinct().ToList();
            }
            var visiting = new HashSet<TMP_FontAsset>();
            var visited = new HashSet<TMP_FontAsset>();
            foreach (var font in graph.Keys) Visit(font, graph, visiting, visited);
        }

        private static void Visit(TMP_FontAsset font, IReadOnlyDictionary<TMP_FontAsset, List<TMP_FontAsset>> graph, ISet<TMP_FontAsset> visiting, ISet<TMP_FontAsset> visited)
        {
            if (visited.Contains(font)) return;
            if (!visiting.Add(font)) throw new InvalidOperationException("Fallback chain contains a cycle among generated font assets.");
            if (graph.TryGetValue(font, out var children)) foreach (var child in children) Visit(child, graph, visiting, visited);
            visiting.Remove(font);
            visited.Add(font);
        }

        private static void ReportFallbackCoverage(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry, StringBuilder report)
        {
            int missingCount = 0;
            var sample = new StringBuilder();
            foreach (char character in CollectCharacters(profile, entry))
            {
                if (entry.outputFont.HasCharacter(character, true, false)) continue;
                missingCount++;
                if (sample.Length < 80) sample.Append(character);
            }
            report.AppendLine(missingCount == 0
                ? $"[{entry.usage}/{entry.languageCode}] fallback coverage: complete."
                : $"[{entry.usage}/{entry.languageCode}] fallback coverage: {missingCount} unresolved glyphs, sample: {sample}");
        }

        private static string GetOutputPath(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry, string outputFolder)
        {
            string name = string.IsNullOrWhiteSpace(entry.outputName)
                ? SafeName(profile.profileId) + "_" + SafeName(entry.usage.ToString()) + "_" + SafeName(entry.languageCode)
                : SafeName(entry.outputName);
            return outputFolder + "/" + SafeName(profile.profileId) + "/" + SafeName(entry.usage.ToString()) + "/" + SafeName(entry.languageCode) + "/" + name + ".asset";
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
                // across a rebuild; only the generated atlas and material sub-assets are replaced.
                foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    if (subAsset != null && subAsset != existing) UnityEngine.Object.DestroyImmediate(subAsset, true);
                }
                EditorUtility.CopySerialized(fontAsset, existing);
                existing.name = fontAsset.name;
                storedAsset = existing;
            }
            else if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                throw new InvalidOperationException($"Refusing to replace non-font asset: {assetPath}");
            }
            else
            {
                AssetDatabase.CreateAsset(fontAsset, assetPath);
                storedAsset = fontAsset;
            }
            foreach (var texture in storedAsset.atlasTextures.Where(texture => texture != null))
            {
                texture.name = storedAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, storedAsset);
            }
            if (storedAsset.material != null)
            {
                storedAsset.material.name = storedAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(storedAsset.material, storedAsset);
            }
            EditorUtility.SetDirty(storedAsset);
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
            if (entry.sourceFont != null) return entry.sourceFont;
            if (profile == null || !profile.autoUseSingleSourceFont || profile.sourceFontFolder == null) return null;
            string folder = AssetDatabase.GetAssetPath(profile.sourceFontFolder);
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            var fonts = AssetDatabase.FindAssets("t:Font", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Font>)
                .Where(font => font != null)
                .OrderBy(font => AssetDatabase.GetAssetPath(font), StringComparer.Ordinal)
                .ToList();
            return fonts.Count == 1 ? fonts[0] : null;
        }

        // Unity Localization is intentionally read by reflection: this Editor-only tool must work in projects
        // that have not installed com.unity.localization. When installed, StringTableCollection.StringTables
        // exposes locale tables whose LocalizedValue values are added to the matching language entry.
        private static IReadOnlyList<string> CollectLocalizationTexts(ESFontBuildProfile profile, ESFontLanguageBuildEntry entry)
        {
            if (profile == null || entry == null || profile.localizationTableCollections == null) return Array.Empty<string>();
            var result = new List<string>();
            foreach (var collection in profile.localizationTableCollections.Where(item => item != null))
            {
                foreach (var tableItem in EnumerateMember(collection, "StringTables", "Tables"))
                {
                    object table = GetMember(tableItem, "Table") ?? tableItem;
                    string localeCode = GetLocaleCode(table);
                    if (!string.IsNullOrEmpty(localeCode) && !string.Equals(localeCode, entry.languageCode, StringComparison.OrdinalIgnoreCase)) continue;
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
                + "|" + profile.samplingPointSize + "|" + profile.atlasPadding + "|" + profile.AtlasWidth + "|" + profile.AtlasHeight + "|" + profile.renderMode + "|" + profile.enableMultiAtlasSupport;
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
}
