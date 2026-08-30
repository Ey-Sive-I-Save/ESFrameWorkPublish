using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WellFired.Command.Unity.Runtime.Loaders;

namespace WellFired.Command.Unity.Editor
{   
    public static class Preferences
    {
        private static Settings _currentSettings;

        [SettingsProvider]
        // ReSharper disable once InconsistentNaming
        public static SettingsProvider PreferencesGUI()
        {
            var provider = new SettingsProvider("WellFired/.Command", SettingsScope.User)
            {
                guiHandler = _ =>
                {
                       if (_currentSettings == null)
                           _currentSettings = SettingsData.Load();

                       _currentSettings.theme = (Theme)EditorGUILayout.EnumPopup("Theme", _currentSettings.theme);

                       if (GUI.changed) 
                           SettingsData.Save(_currentSettings);
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "WellFired", "Command", "Console", "Theme" })
            };

            return provider;
        }
    }
}