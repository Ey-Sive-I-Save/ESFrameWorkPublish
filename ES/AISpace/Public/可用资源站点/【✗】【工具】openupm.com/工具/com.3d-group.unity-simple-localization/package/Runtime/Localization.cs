using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Group3d.Localization
{
    // Inspired by this awesome blog post: https://medium.com/lonely-vertex-development/translating-a-unity-game-51de1aae015b
    /// <summary>
    /// Localization / Translation manager.
    /// </summary>
    public class Localization : MonoBehaviour
    {
        [SerializeField] private List<SupportedLanguage> supportedLanguages;

        public static bool Ready { get; private set; }
        public static string LoadedLanguageCode { get; private set; }
        
        // How many seconds to load translations before timeout
        private const float LoadingTimeoutInSeconds = 5f;
        private const string LanguageSelectionKey = "languageSelection";

        private static Dictionary<string, string> localizedDictionary;
        private string loadedJsonText = "";
        private static bool instantiated;

        private SupportedLanguage DefaultLanguage => supportedLanguages[0];

        /// <summary>
        /// Translates given key with currently selected <see cref="SupportedLanguage"/>.
        /// </summary>
        /// <param name="key">Translation key.</param>
        /// <returns>Matching translation or key if not found.</returns>
        public static string Translate(string key)
        {
            if (localizedDictionary == null)
            {
                Debug.LogError($"You are missing {nameof(Localization)} in the scene. Either add it and remove it before commit or run the app from loading screen.");
                return key; // Fallback to key.
            }

            if (localizedDictionary.ContainsKey(key))
            {
                return localizedDictionary[key];
            }

            Debug.LogError($"Missing translation for key: {key} with language: {LoadedLanguageCode}.");

            return key; // Fallback to key.
        }

        /// <summary>
        /// Translates given key with currently selected <see cref="SupportedLanguage"/>.
        /// </summary>
        /// <param name="key">Translation key.</param>
        /// <param name="formatArgs">String format args.</param>
        /// <returns>Matching translation or key if not found.</returns>
        public static string Translate(string key, params object[] formatArgs)
        {
            var translation = Translate(key);

            try
            {
                var result = string.Format(translation, formatArgs);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return translation;
            }
        }

        /// <summary>
        /// Selects the next <see cref="SupportedLanguage"/> available.
        /// Loops through all <see cref="SupportedLanguage"/>s.
        /// </summary>
        /// <returns>Currently selected <see cref="SupportedLanguage"/>.</returns>
        public SupportedLanguage SelectNextLanguage()
        {
            Ready = false;

            var currentLanguageIndex = 0;

            if (PlayerPrefs.HasKey(LanguageSelectionKey))
            {
                currentLanguageIndex = PlayerPrefs.GetInt(LanguageSelectionKey);
            }

            currentLanguageIndex++;

            if (currentLanguageIndex < 0 || currentLanguageIndex >= supportedLanguages.Count) currentLanguageIndex = 0;

            PlayerPrefs.SetInt(LanguageSelectionKey, currentLanguageIndex);

            var lang = supportedLanguages[currentLanguageIndex];
            StartCoroutine(LoadJsonLanguageData(lang));
            
            return lang;
        }

        /// <summary>s
        /// Converts Unity's Application.systemLanguage to a supported country code.
        /// If not found, returns a default instead.
        /// </summary>
        public SupportedLanguage CurrentSupportedLanguage()
        {
            var language = Application.systemLanguage;

            try
            {
                if (PlayerPrefs.HasKey(LanguageSelectionKey))
                {
                    var lang = supportedLanguages[PlayerPrefs.GetInt(LanguageSelectionKey)];
                    return lang;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }

            foreach (var lang in supportedLanguages)
            {
                if (lang.systemLanguage == language)
                {
                    return lang;
                }
            }

            return DefaultLanguage;
        }

        public static IEnumerator WaitUntilReady()
        {
            const float pollInterval = 0.016f;
            var totalWaitTime = 0f;

            while (!Ready && totalWaitTime < LoadingTimeoutInSeconds)
            {
                totalWaitTime += pollInterval;
                yield return new WaitForSeconds(pollInterval);
            }

            if (!Ready && totalWaitTime >= LoadingTimeoutInSeconds)
            {
                Debug.LogError($"Timeout (of {LoadingTimeoutInSeconds:0.0}s) occured while loading translations!");
            }
        }

        private void OnDestroy()
        {
            localizedDictionary = null;
        }

        private IEnumerator Start()
        {
            if (instantiated)
            {
                yield break;
            }

            DontDestroyOnLoad(this);
            instantiated = true;

            yield return LoadJsonLanguageData(CurrentSupportedLanguage());
        }

        private IEnumerator LoadJsonLanguageData(SupportedLanguage lang)
        {
            loadedJsonText = lang.translationFile.text;

            if (string.IsNullOrEmpty(loadedJsonText))
            {
                // Try to get default language instead.
                if (lang == DefaultLanguage)
                {
                    Debug.LogError($"Missing file for default language ({DefaultLanguage}).", this);
                    yield break;
                }

                yield return LoadJsonLanguageData(DefaultLanguage);
            }
            else
            {
                LoadedLanguageCode = lang.languageCode;

                try
                {
                    localizedDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(loadedJsonText);
                }
                catch (Exception e)
                {
                    Debug.LogError(e, this);
                }
            }

            // To update existing game objects in the scene.
            foreach (var txt in FindObjectsOfType<TranslateTextOnStart>())
            {
                txt.TranslateText();
            }

            Ready = true;
            loadedJsonText = null;
        }

#if UNITY_EDITOR && UNITY_ASSERTIONS
        /// <summary>
        /// This is just to ease testing hence setting private serialized variables is rather painful.
        /// </summary>
        public void SetSupportedLanguages(List<SupportedLanguage> setSupportedLanguages)
        {
            supportedLanguages = setSupportedLanguages;
        }
#endif
    }
}
