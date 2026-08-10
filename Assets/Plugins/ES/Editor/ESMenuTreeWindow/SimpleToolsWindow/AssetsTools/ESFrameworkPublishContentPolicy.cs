using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

[assembly: InternalsVisibleTo("ES_Design.ConfigKey.Tests")]

namespace ES
{
    internal enum ESFrameworkPublishPathDisposition
    {
        RequiredPackage,
        GeneratedOnDemand,
        ProjectOwnedState,
        OptionalHeavyContent,
        Unknown
    }

    internal sealed class ESFrameworkPublishHardcodedPathAudit
    {
        public readonly List<string> unknownPaths = new List<string>();
        public int requiredCount;
        public int generatedCount;
        public int projectOwnedCount;
        public int optionalHeavyCount;
    }

    /// <summary>
    /// Formal UnityPackage content policy. It runs only from explicit closure/publish actions.
    /// </summary>
    internal static class ESFrameworkPublishContentPolicy
    {
        private const string InstallerDownloadsRoot = "Assets/Plugins/ES/Editor/Installer/Downloads";
        private const string GeneratedAssetGuideFileName = "ESGlobalProjectAssetGuideData.HardcodedCommonScripts.cs";

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly Regex AssetPathLiteralRegex = new Regex(
            "\\\"(Assets/ESNormalAssets(?:/[^\\\"\\r\\n]*)?)\\\"",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly string[] RequiredPublishRoots =
            ESGlobalEditorDefaultConfi.CreateDefaultPackagePublishAssetPaths().ToArray();

        private static readonly string[] RequiredIndividualAssets =
            ESGlobalEditorDefaultConfi.CreateDefaultPackagePublishRequiredAssetPaths().ToArray();

        private static readonly string[] GeneratedOnDemandRoots =
        {
            "Assets/ESNormalAssets/Data/GlobalData/CmdAgent",
            "Assets/ESNormalAssets/Data/GlobalData/Input",
            "Assets/ESNormalAssets/Data/GlobalData/SoTable",
            "Assets/ESNormalAssets/Data/GlobalData/StateMachineConfig",
            "Assets/ESNormalAssets/Data/Action",
            "Assets/ESNormalAssets/Data/AgentAuthoring",
            "Assets/ESNormalAssets/Data/Graphs",
            "Assets/ESNormalAssets/Data/Skill",
            "Assets/ESNormalAssets/ESValidation",
            "Assets/ESNormalAssets/Fonts"
        };

        private static readonly string[] ProjectOwnedStateRoots =
        {
            "Assets/ESNormalAssets/Data/AssetLibrary",
            "Assets/ESNormalAssets/Data/AssetPackageBake",
            "Assets/ESNormalAssets/Data/CharacterVariants",
            "Assets/ESNormalAssets/Data/GlobalData/Location",
            "Assets/ESNormalAssets/Data/GlobalData/ProjectAssetGuide",
            "Assets/ESNormalAssets/Data/GlobalData/SceneManage",
            "Assets/ESNormalAssets/Data/Group",
            "Assets/ESNormalAssets/Data/Legacy",
            "Assets/ESNormalAssets/Data/Normal",
            "Assets/ESNormalAssets/Data/Pack",
            "Assets/ESNormalAssets/Scenes"
        };

        private static readonly string[] OptionalHeavyContentRoots =
        {
            "Assets/ESNormalAssets/3DControlAssets",
            "Assets/ESNormalAssets/Audio",
            "Assets/ESNormalAssets/CharacterTemplates",
            "Assets/ESNormalAssets/CharacterVariants",
            "Assets/ESNormalAssets/EditorTools",
            "Assets/ESNormalAssets/Materials",
            "Assets/ESNormalAssets/Prefabs",
            "Assets/ESNormalAssets/Sprites",
            "Assets/ESNormalAssets/Textures",
            "Assets/ESNormalAssets/VehiclePrototypes"
        };

        internal static IReadOnlyList<string> BuiltInRequiredPublishRoots => RequiredPublishRoots;
        internal static IReadOnlyList<string> BuiltInRequiredIndividualAssets => RequiredIndividualAssets;

        internal static bool TryValidateConfiguration(
            IReadOnlyList<string> publishRoots,
            IReadOnlyList<string> requiredAssetPaths,
            IReadOnlyList<string> exclusions,
            out string error)
        {
            error = string.Empty;
            publishRoots ??= Array.Empty<string>();
            requiredAssetPaths ??= Array.Empty<string>();
            exclusions ??= Array.Empty<string>();

            for (int i = 0; i < RequiredPublishRoots.Length; i++)
            {
                string requiredRoot = RequiredPublishRoots[i];
                if (!IsCovered(requiredRoot, publishRoots, requiredAssetPaths))
                {
                    error = "正式发布配置缺少内置必需目录：" + requiredRoot;
                    return false;
                }
            }

            for (int i = 0; i < RequiredIndividualAssets.Length; i++)
            {
                string requiredAsset = RequiredIndividualAssets[i];
                if (!IsCovered(requiredAsset, publishRoots, requiredAssetPaths))
                {
                    error = "正式发布配置缺少内置必需资产：" + requiredAsset;
                    return false;
                }
            }

            for (int i = 0; i < requiredAssetPaths.Count; i++)
            {
                string requiredPath = Normalize(requiredAssetPaths[i]);
                if (string.IsNullOrEmpty(requiredPath))
                {
                    error = "正式发布必需资产路径为空。";
                    return false;
                }

                if (IsAtOrUnder(requiredPath, InstallerDownloadsRoot))
                {
                    error = "正式发布必需资产不能位于 Installer/Downloads：" + requiredPath;
                    return false;
                }

                bool exists = AssetDatabase.IsValidFolder(requiredPath)
                    || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(requiredPath));
                if (!exists)
                {
                    error = "正式发布必需资产不存在：" + requiredPath;
                    return false;
                }

                for (int exclusionIndex = 0; exclusionIndex < exclusions.Count; exclusionIndex++)
                {
                    string exclusion = Normalize(exclusions[exclusionIndex]);
                    if (IsAtOrUnder(requiredPath, exclusion) || IsAtOrUnder(exclusion, requiredPath))
                    {
                        error = "正式发布必需资产与排除路径冲突：" + requiredPath + " <-> " + exclusion;
                        return false;
                    }
                }
            }

            return TryRejectReparsePoints(publishRoots.Concat(requiredAssetPaths), out error);
        }

        internal static bool TryAuditHardcodedAssetPaths(
            IReadOnlyList<string> sourceRoots,
            out ESFrameworkPublishHardcodedPathAudit audit,
            out string error)
        {
            audit = new ESFrameworkPublishHardcodedPathAudit();
            error = string.Empty;
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript", sourceRoots?.ToArray() ?? Array.Empty<string>());
                for (int i = 0; i < scriptGuids.Length; i++)
                {
                    string scriptPath = Normalize(AssetDatabase.GUIDToAssetPath(scriptGuids[i]));
                    if (string.IsNullOrEmpty(scriptPath)
                        || scriptPath.IndexOf("/Obsolete/", StringComparison.OrdinalIgnoreCase) >= 0
                        || scriptPath.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0
                        || scriptPath.EndsWith("/" + GeneratedAssetGuideFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string fullPath = AssetPathToFullPath(scriptPath);
                    if (!File.Exists(fullPath))
                        continue;

                    string source;
                    try
                    {
                        source = StrictUtf8.GetString(File.ReadAllBytes(fullPath));
                    }
                    catch (DecoderFallbackException exception)
                    {
                        error = "发布源码不是严格 UTF-8：" + scriptPath + "；" + exception.Message;
                        return false;
                    }
                    MatchCollection matches = AssetPathLiteralRegex.Matches(source);
                    for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                    {
                        string assetPath = Normalize(matches[matchIndex].Groups[1].Value);
                        if (!string.IsNullOrEmpty(assetPath))
                            uniquePaths.Add(assetPath);
                    }
                }
            }
            catch (Exception exception)
            {
                error = "扫描源码中的硬编码资产路径失败：" + exception.Message;
                return false;
            }

            foreach (string assetPath in uniquePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                switch (Classify(assetPath))
                {
                    case ESFrameworkPublishPathDisposition.RequiredPackage:
                        audit.requiredCount++;
                        break;
                    case ESFrameworkPublishPathDisposition.GeneratedOnDemand:
                        audit.generatedCount++;
                        break;
                    case ESFrameworkPublishPathDisposition.ProjectOwnedState:
                        audit.projectOwnedCount++;
                        break;
                    case ESFrameworkPublishPathDisposition.OptionalHeavyContent:
                        audit.optionalHeavyCount++;
                        break;
                    default:
                        audit.unknownPaths.Add(assetPath);
                        break;
                }
            }

            if (audit.unknownPaths.Count == 0)
                return true;

            error = "发现未分类的 ESNormalAssets 硬编码路径，正式发布已拒绝。"
                + "\n必须明确标记为随包、按需生成、项目状态或可选重内容：\n"
                + string.Join("\n", audit.unknownPaths.Take(40));
            return false;
        }

        internal static ESFrameworkPublishPathDisposition Classify(string assetPath)
        {
            string normalized = Normalize(assetPath);
            if (RequiredPublishRoots.Any(root => IsAtOrUnder(normalized, root))
                || RequiredIndividualAssets.Any(path => IsAtOrUnder(normalized, path)))
                return ESFrameworkPublishPathDisposition.RequiredPackage;

            if (string.Equals(normalized, "Assets/ESNormalAssets", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Assets/ESNormalAssets/Data", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Assets/ESNormalAssets/Data/GlobalData", StringComparison.OrdinalIgnoreCase))
                return ESFrameworkPublishPathDisposition.GeneratedOnDemand;

            if (GeneratedOnDemandRoots.Any(root => IsExactOrAtOrUnder(normalized, root)))
                return ESFrameworkPublishPathDisposition.GeneratedOnDemand;

            if (ProjectOwnedStateRoots.Any(root => IsExactOrAtOrUnder(normalized, root)))
                return ESFrameworkPublishPathDisposition.ProjectOwnedState;

            if (OptionalHeavyContentRoots.Any(root => IsExactOrAtOrUnder(normalized, root)))
                return ESFrameworkPublishPathDisposition.OptionalHeavyContent;

            return ESFrameworkPublishPathDisposition.Unknown;
        }

        internal static bool TryMeasureSourceBytes(
            IReadOnlyList<string> assetPaths,
            out long sourceBytes,
            out string error)
        {
            sourceBytes = 0;
            error = string.Empty;
            try
            {
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    string fullPath = AssetPathToFullPath(assetPaths[i]);
                    if (!File.Exists(fullPath))
                        continue;

                    sourceBytes = checked(sourceBytes + new FileInfo(fullPath).Length);
                    string metaPath = fullPath + ".meta";
                    if (File.Exists(metaPath))
                        sourceBytes = checked(sourceBytes + new FileInfo(metaPath).Length);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "计算正式发布源文件体积失败：" + exception.Message;
                return false;
            }
        }

        internal static bool TryValidateExportedUnityPackage(
            string packagePath,
            IReadOnlyList<string> expectedAssetPaths,
            out int packagedPathCount,
            out string error)
        {
            packagedPathCount = 0;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                error = "导出的 UnityPackage 不存在：" + packagePath;
                return false;
            }

            var expected = new HashSet<string>(
                (expectedAssetPaths ?? Array.Empty<string>()).Select(Normalize),
                StringComparer.OrdinalIgnoreCase);
            expected.RemoveWhere(string.IsNullOrEmpty);
            if (expected.Count == 0)
            {
                error = "导出后校验缺少预期资源清单。";
                return false;
            }

            if (!TryReadUnityPackagePathnames(packagePath, out HashSet<string> packaged, out error))
                return false;

            packagedPathCount = packaged.Count;
            List<string> missing = expected
                .Where(path => !packaged.Contains(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missing.Count > 0)
            {
                error = "UnityPackage 实际内容缺少 " + missing.Count + " 个计划资源：\n"
                    + string.Join("\n", missing.Take(40));
                return false;
            }

            List<string> unexpected = packaged
                .Where(path => !expected.Contains(path) && !IsExpectedFolderAncestor(path, expected))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unexpected.Count > 0)
            {
                error = "UnityPackage 实际内容包含 " + unexpected.Count + " 个计划外资源：\n"
                    + string.Join("\n", unexpected.Take(40));
                return false;
            }

            return true;
        }

        private static bool TryReadUnityPackagePathnames(
            string packagePath,
            out HashSet<string> packagedPaths,
            out string error)
        {
            const int tarBlockSize = 512;
            const int maxEntryCount = 100000;
            const int maxPathnameBytes = 16384;
            packagedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            error = string.Empty;

            try
            {
                using var file = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var gzip = new GZipStream(file, CompressionMode.Decompress, false);
                var header = new byte[tarBlockSize];
                var skipBuffer = new byte[8192];
                int entryCount = 0;

                while (true)
                {
                    int headerBytes = ReadBlock(gzip, header, tarBlockSize);
                    if (headerBytes == 0)
                        break;
                    if (headerBytes != tarBlockSize)
                        throw new InvalidDataException("tar 头部被截断。");
                    if (header.All(value => value == 0))
                        break;
                    if (++entryCount > maxEntryCount)
                        throw new InvalidDataException("tar 条目数量超过安全上限。");

                    string entryName = ReadTarText(header, 0, 100);
                    string prefix = ReadTarText(header, 345, 155);
                    if (!string.IsNullOrEmpty(prefix))
                        entryName = prefix + "/" + entryName;
                    long entrySize = ReadTarOctal(header, 124, 12);
                    if (entrySize < 0)
                        throw new InvalidDataException("tar 条目长度无效：" + entryName);

                    bool isPathname = entryName.EndsWith("/pathname", StringComparison.Ordinal);
                    if (isPathname)
                    {
                        if (entrySize <= 0 || entrySize > maxPathnameBytes)
                            throw new InvalidDataException("pathname 条目长度无效：" + entryName);
                        var bytes = new byte[(int)entrySize];
                        if (ReadBlock(gzip, bytes, bytes.Length) != bytes.Length)
                            throw new InvalidDataException("pathname 条目被截断：" + entryName);

                        string pathname = Normalize(StrictUtf8.GetString(bytes).TrimEnd('\0', '\r', '\n'));
                        if (!IsSafePackagedAssetPath(pathname))
                            throw new InvalidDataException("UnityPackage 包含非法 pathname：" + pathname);
                        if (!packagedPaths.Add(pathname))
                            throw new InvalidDataException("UnityPackage 包含重复 pathname：" + pathname);
                    }
                    else
                    {
                        SkipExactly(gzip, entrySize, skipBuffer);
                    }

                    long padding = (tarBlockSize - (entrySize % tarBlockSize)) % tarBlockSize;
                    SkipExactly(gzip, padding, skipBuffer);
                }

                if (packagedPaths.Count == 0)
                    throw new InvalidDataException("UnityPackage 中没有 pathname 条目。");
                return true;
            }
            catch (Exception exception)
            {
                packagedPaths.Clear();
                error = "读取 UnityPackage 实际内容失败：" + exception.Message;
                return false;
            }
        }

        private static bool IsExpectedFolderAncestor(string packagedPath, HashSet<string> expectedPaths)
        {
            if (!AssetDatabase.IsValidFolder(packagedPath))
                return false;

            string prefix = packagedPath.TrimEnd('/') + "/";
            return expectedPaths.Any(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSafePackagedAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !path.StartsWith("Assets/", StringComparison.Ordinal)
                || path.IndexOf('\\') >= 0
                || path.Any(char.IsControl))
                return false;

            string[] segments = path.Split('/');
            return segments.All(segment => !string.IsNullOrWhiteSpace(segment)
                && segment != "."
                && segment != ".."
                && segment == segment.Trim());
        }

        private static int ReadBlock(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0)
                    break;
                offset += read;
            }
            return offset;
        }

        private static void SkipExactly(Stream stream, long count, byte[] buffer)
        {
            while (count > 0)
            {
                int chunk = (int)Math.Min(count, buffer.Length);
                int read = stream.Read(buffer, 0, chunk);
                if (read <= 0)
                    throw new EndOfStreamException("tar 条目被截断。");
                count -= read;
            }
        }

        private static string ReadTarText(byte[] header, int offset, int count)
        {
            int length = 0;
            while (length < count && header[offset + length] != 0)
                length++;
            return Encoding.ASCII.GetString(header, offset, length);
        }

        private static long ReadTarOctal(byte[] header, int offset, int count)
        {
            string value = Encoding.ASCII.GetString(header, offset, count).Trim('\0', ' ');
            if (string.IsNullOrEmpty(value))
                return 0;

            long result = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character < '0' || character > '7')
                    throw new InvalidDataException("tar 条目长度不是八进制。");
                result = checked(result * 8 + character - '0');
            }
            return result;
        }

        private static bool IsCovered(
            string requiredPath,
            IReadOnlyList<string> publishRoots,
            IReadOnlyList<string> requiredAssetPaths)
        {
            string normalizedRequired = Normalize(requiredPath);
            return publishRoots.Any(path => IsAtOrUnder(normalizedRequired, Normalize(path)))
                || requiredAssetPaths.Any(path => IsAtOrUnder(normalizedRequired, Normalize(path)));
        }

        private static bool TryRejectReparsePoints(IEnumerable<string> assetPaths, out string error)
        {
            error = string.Empty;
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                error = "无法解析项目根目录。";
                return false;
            }

            foreach (string assetPath in assetPaths)
            {
                string normalized = Normalize(assetPath);
                string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
                string current = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : fullPath;
                while (!string.IsNullOrEmpty(current)
                    && current.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(current)
                        && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        error = "正式发布路径包含 junction/symlink，已拒绝：" + normalized;
                        return false;
                    }

                    if (string.Equals(current, projectRoot, StringComparison.OrdinalIgnoreCase))
                        break;
                    current = Path.GetDirectoryName(current);
                }
            }

            return true;
        }

        private static bool IsExactOrAtOrUnder(string path, string root)
        {
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                || IsAtOrUnder(path, root);
        }

        private static bool IsAtOrUnder(string path, string root)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
                return false;

            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim().TrimEnd('/');
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("无法解析项目根目录。");
            string normalized = Normalize(assetPath);
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
            string normalizedProjectRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string requiredPrefix = normalizedProjectRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("资产路径越出项目根目录：" + assetPath);
            return fullPath;
        }
    }
}
