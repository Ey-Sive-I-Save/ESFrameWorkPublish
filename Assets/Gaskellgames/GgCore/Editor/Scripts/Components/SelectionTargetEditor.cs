#if UNITY_EDITOR
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomEditor(typeof(SelectionTarget)), CanEditMultipleObjects]
    public class SelectionTargetEditor : GgEditor
    {
        #region Serialized Properties / OnEnable
        
        private readonly string note = "When selecting any child GameObject in the scene view,\nthis GameObject will be selected instead.";
        private readonly float singleLine = EditorGUIUtility.singleLineHeight;

        private const string packageRefName = "GgCore";
        private Texture banner;
        
        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            SelectionTarget selectionTarget = (SelectionTarget)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(SelectionTarget).NicifyName());
            
            // draw inspector
            bool defaultGui = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.TextArea(note);
            GUI.enabled = defaultGui;
            
            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        
    } // class end
}
#endif