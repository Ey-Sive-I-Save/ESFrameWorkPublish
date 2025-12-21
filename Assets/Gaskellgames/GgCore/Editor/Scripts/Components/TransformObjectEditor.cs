#if UNITY_EDITOR
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [CustomEditor(typeof(TransformObject)), CanEditMultipleObjects]
    public class TransformObjectEditor : GgEditor
    {
        #region Serialized Properties / OnEnable
        
        private SerializedProperty gizmosOnSelected;
        private SerializedProperty targetObject;
        private SerializedProperty updateMethod;
        private SerializedProperty start;
        private SerializedProperty end;
        private SerializedProperty lerpValue;
        private SerializedProperty autoLerpSpeed;
        private SerializedProperty rotationSpeed;
        private SerializedProperty canUpdate;

        private const string packageRefName = "GgCore";
        private Texture banner;
        
        private void OnEnable()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            
            gizmosOnSelected = serializedObject.FindProperty("gizmosOnSelected");
            targetObject = serializedObject.FindProperty("targetObject");
            updateMethod = serializedObject.FindProperty("updateMethod");
            start = serializedObject.FindProperty("start");
            end = serializedObject.FindProperty("end");
            lerpValue = serializedObject.FindProperty("lerpValue");
            autoLerpSpeed = serializedObject.FindProperty("autoLerpSpeed");
            rotationSpeed = serializedObject.FindProperty("rotationSpeed");
            canUpdate = serializedObject.FindProperty("canUpdate");
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnInspectorGUI

        public override void OnInspectorGUI()
        {
            // get & update references
            TransformObject transformObject = (TransformObject)target;
            serializedObject.Update();

            // draw banner if turned on in Gaskellgames settings
            EditorWindowUtility.TryDrawBanner(banner, nameof(TransformObject).NicifyName());
            
            // draw inspector
            EditorGUILayout.PropertyField(gizmosOnSelected);
            EditorGUILayout.PropertyField(targetObject);
            EditorGUILayout.PropertyField(updateMethod);
            EditorGUILayout.Space();

            if (updateMethod.enumValueIndex == TransformObject.UpdateMethod.ManualLerp.ToInt())
            {
                EditorGUILayout.PropertyField(start);
                EditorGUILayout.PropertyField(end);
                EditorGUILayout.PropertyField(lerpValue);
            }
            if (updateMethod.enumValueIndex == TransformObject.UpdateMethod.AutoLerp.ToInt())
            {
                EditorGUILayout.PropertyField(start);
                EditorGUILayout.PropertyField(end);
                EditorGUILayout.PropertyField(lerpValue);
                EditorGUILayout.PropertyField(autoLerpSpeed);
                EditorGUILayout.PropertyField(canUpdate);
            }
            if (updateMethod.enumValueIndex == TransformObject.UpdateMethod.AutoRotate.ToInt())
            {
                EditorGUILayout.PropertyField(rotationSpeed);
                EditorGUILayout.PropertyField(canUpdate);
            }
            
            // apply reference changes
            serializedObject.ApplyModifiedProperties();
        }

        #endregion
        
    } // class end
}
#endif