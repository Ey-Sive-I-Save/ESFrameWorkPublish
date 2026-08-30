using System.Collections;
using System.Collections.Generic;
using Group3d.Localization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class LocalizationTests
    {
        private GameObject localizationGameObject;
        private Localization localization;

        [SetUp]
        public void Setup()
        {
            localizationGameObject = new GameObject("Localization");
            localization = localizationGameObject.AddComponent<Localization>();
            localization.SetSupportedLanguages(new List<SupportedLanguage>
            {
                new SupportedLanguage
                {
                    languageCode = "en-US",
                    systemLanguage = SystemLanguage.English,
                    translationFile = new TextAsset("{}"),
                }
            });
        }

        [TearDown]
        public void Teardown()
        {
            Object.Destroy(localizationGameObject);
        }

        [UnityTest]
        public IEnumerator Translate_TranslatingKey_KeyTranslated()
        {
            // Arrange
            localization.SetSupportedLanguages(new List<SupportedLanguage>
            {
                new SupportedLanguage
                {
                    languageCode = "en-US",
                    systemLanguage = SystemLanguage.English,
                    translationFile = new TextAsset("{ \"TRANSLATION_KEY\": \"translation value\" }"),
                }
            });
            localization.SelectNextLanguage();

            // Act
            yield return null;
            var translatedValue = Localization.Translate("TRANSLATION_KEY");

            // Assert
            Assert.AreEqual("translation value", translatedValue);
        }
    }
}
