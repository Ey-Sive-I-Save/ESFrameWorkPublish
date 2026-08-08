using System.IO;
using System.Text;
using ES;
using UnityEngine;

namespace ES.Internal
{
    public static class ESInputProfileIO
    {
        public const string DefaultFolderName = "Input";
        public const string DefaultFileName = "input_profile.json";

        public static ESInputBindingProfile LoadOrCreateDefault(string filePath = null)
        {
            filePath = ResolveProfilePath(filePath);

            if (!File.Exists(filePath))
                return CreateDefaultProfile();

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrEmpty(json))
                return CreateDefaultProfile();

            return FromJsonOrDefault(json);
        }

        public static void Save(ESInputBindingProfile profile, string filePath = null)
        {
            if (profile == null)
                profile = CreateDefaultProfile();

            profile.Normalize();

            filePath = ResolveProfilePath(filePath);

            string json = ToJson(profile, true);
            ESManagedFileIO.WriteTextAtomic(filePath, json, Encoding.UTF8, Application.persistentDataPath);
        }

        public static string ToJson(ESInputBindingProfile profile, bool prettyPrint = false)
        {
            if (profile == null)
                profile = CreateDefaultProfile();

            profile.Normalize();
            return JsonUtility.ToJson(profile, prettyPrint);
        }

        public static ESInputBindingProfile FromJsonOrDefault(string json)
        {
            if (string.IsNullOrEmpty(json))
                return CreateDefaultProfile();

            try
            {
                ESInputBindingProfile profile = JsonUtility.FromJson<ESInputBindingProfile>(json);
                if (profile == null)
                    return CreateDefaultProfile();

                profile.Normalize();
                return profile;
            }
            catch
            {
                return CreateDefaultProfile();
            }
        }

        public static ESInputBindingProfile CreateDefaultProfile()
        {
            ESInputBindingProfile profile = new ESInputBindingProfile
            {
                schemaVersion = ESInputBindingProfile.CurrentSchemaVersion,
                profileId = "Default",
                displayName = "默认键位",
                activeSchemeId = ESInputSchemeIds.KeyboardMouse
            };
            profile.Normalize();
            return profile;
        }

        public static string GetDefaultProfilePath()
        {
            return Path.Combine(Application.persistentDataPath, DefaultFolderName, DefaultFileName);
        }

        private static string ResolveProfilePath(string filePath)
        {
            string root = Path.GetFullPath(Application.persistentDataPath);
            if (string.IsNullOrWhiteSpace(filePath))
                return GetDefaultProfilePath();

            string input = filePath.Trim();
            if (input.Contains("://") || input.StartsWith("jar:", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("输入档案路径不能使用 URI，已回退到默认路径。");
                return GetDefaultProfilePath();
            }

            string candidate;
            try
            {
                candidate = Path.IsPathRooted(input)
                    ? Path.GetFullPath(input)
                    : Path.GetFullPath(Path.Combine(root, input.Replace('\\', Path.DirectorySeparatorChar)));
            }
            catch (System.Exception)
            {
                Debug.LogWarning("输入档案路径格式无效，已回退到默认路径：" + filePath);
                return GetDefaultProfilePath();
            }

            if (!IsPathWithinRoot(root, candidate))
            {
                Debug.LogWarning("输入档案路径必须位于 persistentDataPath 内，已回退到默认路径：" + filePath);
                return GetDefaultProfilePath();
            }

            if (ContainsExistingReparsePoint(root, candidate))
            {
                Debug.LogWarning("输入档案路径不能穿过 junction/symlink，已回退到默认路径：" + filePath);
                return GetDefaultProfilePath();
            }

            return candidate;
        }

        private static bool IsPathWithinRoot(string root, string candidate)
        {
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string normalizedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(normalizedRoot, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsExistingReparsePoint(string root, string candidate)
        {
            string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string current = rootFull;
            string relative = candidate.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (int i = 0; i < segments.Length; i++)
            {
                if (string.IsNullOrEmpty(segments[i])) continue;
                current = Path.Combine(current, segments[i]);
                if (!File.Exists(current) && !Directory.Exists(current)) break;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
            }

            return false;
        }
    }
}
