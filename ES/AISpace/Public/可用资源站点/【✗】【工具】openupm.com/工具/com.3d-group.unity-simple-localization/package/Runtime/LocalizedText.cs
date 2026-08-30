using UnityEngine;
using UnityEngine.UI;

namespace Group3d.Localization
{
    [RequireComponent(typeof(Text))]
    public class LocalizedText : MonoBehaviour
    {
#pragma warning disable CS0649
        [SerializeField] private string localizationKey;
#pragma warning restore CS0649

        private Text textComponent;

        private void Awake()
        {
            textComponent = GetComponent<Text>();
            TranslateText();
        }

#if DEBUG
        private void Start()
        {
            if (string.IsNullOrWhiteSpace(localizationKey)) Debug.LogError("Forgot to set localization key?", this);
        }
#endif

        public void TranslateText()
        {
            var translation = Localization.Translate(localizationKey);
            if (translation != null)
            {
                textComponent.text = translation;
            }
        }
    }
}
