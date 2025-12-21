using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public class Sample_Interface_IEditorUpdate : MonoBehaviour, IEditorUpdate
    {
        [SerializeField]
        private Vector3 position = new Vector3();

        /// <summary>
        /// IEditorUpdate callback
        /// </summary>
        public void EditorUpdate()
        {
            position = transform.position;
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawSphere(position, 0.2f);
        }
        
    } // class end
}
