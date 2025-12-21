using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames/Unity_IEditorUpdate
    /// </remarks>
    
    public interface IEditorUpdate
    {
#if UNITY_EDITOR
        /// <summary>
        /// Calling method for 'EditorUpdate' from an EditorOnly script.
        /// </summary>
        public void HandleEditorUpdate()
        {
            // handle auto unsubscription if null
            if ((Object)this) { EditorUpdate(); }
            else { EditorApplication.update -= HandleEditorUpdate; }
        }
        
        /// <summary>
        /// EditorUpdate is called infrequently, but will try to run multiple times per second.
        /// </summary>
        public void EditorUpdate();
#endif

    } // class end
}
