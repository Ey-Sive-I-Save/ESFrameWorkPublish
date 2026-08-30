using UnityEngine;

namespace Group3d.Localization
{
    [System.Serializable]
    public class SupportedLanguage
    {
        public TextAsset translationFile;
        public string languageCode;
        public SystemLanguage systemLanguage;
    }
}
