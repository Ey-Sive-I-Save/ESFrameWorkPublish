#if UNITY_EDITOR
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [CustomEditor(typeof(LockToLayer)), CanEditMultipleObjects]
    public class LockToLayerEditor : GgEditor
    {
        #region Serialized Properties / OnEnable
        
        private SerializedProperty layerLock;
        
        private const string packageRefName = "GgCore";
        private Texture banner;
        
        private void OnEnable()
        {
            layerLock = serializedObject.FindProperty("layerLock");
            banner = EditorWindowUtility.LoadInspectorBanner();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            LockToLayer lockToLayer = (LockToLayer)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(LockToLayer).NicifyName());
            
            // draw inspector
            EditorGUILayout.PropertyField(layerLock);
            
            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        
    } // class end
}
#endif