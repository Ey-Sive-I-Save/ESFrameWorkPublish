using UnityEngine;

namespace ES
{
    /// <summary>
    /// Marks a temporary GameObject created by an editor preview.
    /// Keep this outside Editor folders because it is a MonoBehaviour component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EditorPreviewGameObjectSign : MonoBehaviour
    {
        [SerializeField]
        private string owner;

        [SerializeField]
        private string note;

        public string Owner => owner;
        public string Note => note;

        public void Setup(string ownerName, string noteText = null)
        {
            owner = ownerName;
            note = noteText;
        }
    }
}
