using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [AddComponentMenu("Gaskellgames/GgCore/Lock To Layer")]
    public class LockToLayer : GgMonoBehaviour, IEditorUpdate
    {
        [SerializeField]
        private LayerDropdown layerLock;

        private void LateUpdate()
        {
            HandleLayerLock();
        }

        public void EditorUpdate()
        {
            HandleLayerLock();
        }

        private void HandleLayerLock()
        {
            if (gameObject.layer != layerLock.value)
            {
                gameObject.layer = layerLock.value;
            }
        }
        
    } // class end 
}
