using System;
using UnityEngine;
using Debug = WellFired.Command.Log.Debug;

namespace WellFired.Command.Unity.Runtime.Loaders
{
    public static class SettingsData
    {
        private static string Serialize(Settings settings)
        {
            try
            {
                return JsonUtility.ToJson(settings);
            }
            catch (Exception)
            {
                Debug.LogWarning("Failed to serialize settings, your preferences have been reset.");
                return "";
            }
        }

        private static Settings Deserialize(string data)
        {
            try
            {
                var settings = JsonUtility.FromJson<Settings>(data);
                if (settings == null)
                    throw new NullReferenceException();
                return settings;
            }
            catch (Exception)
            {
                Debug.LogWarning("Failed to deserialize settings, your preferences have been reset.");
                var settings = new Settings();
                Serialize(settings);
                return settings;
            }
        }

        private const string PrefKey = "wellfired.command.preferences";
        public static Settings Load()
        {
            Settings settings;
            if (!PlayerPrefs.HasKey(PrefKey))
            {
                settings = new Settings();
                Save(settings);
            }
            else
            {
                settings = Deserialize(PlayerPrefs.GetString(PrefKey));
            }
            
            settings.LoadSkinData();
            return settings;
        }

        public static void Save(Settings settings)
        {
            var preferenceData = Serialize(settings);
            PlayerPrefs.SetString(PrefKey, preferenceData);
        }
    }
}