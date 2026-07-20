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
            if (string.IsNullOrEmpty(filePath))
                filePath = GetDefaultProfilePath();

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

            if (string.IsNullOrEmpty(filePath))
                filePath = GetDefaultProfilePath();

            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string json = ToJson(profile, true);
            File.WriteAllText(filePath, json, Encoding.UTF8);
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
    }
}
