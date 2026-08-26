using UnityEngine;

namespace ES.UI
{
    /// <summary>
    /// Reviewable semantic metadata emitted by the AI UI materializer.
    /// It carries no business state and is safe to keep on visual prefabs.
    /// </summary>
    public sealed class ESUIComponentSemantic : MonoBehaviour
    {
        public string componentType;
        public string visualVariant;
        public string colorToken;
        public string typographyRole;
        public string layerRole;
        public int siblingOrder = -1;
        public string[] assetSlots;
        public float numericValue;
        public bool hasNumericValue;
        public string inputIntent;
        public int interactionTargetWidth;
        public int interactionTargetHeight;
    }
}
