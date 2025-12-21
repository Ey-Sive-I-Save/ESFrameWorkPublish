#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    [CustomPropertyDrawer(typeof(AudioClipData))]
    public class AudioClipDataDrawer : PropertyDrawer
    {
        #region Variables

        private SerializedProperty audioClip;
        private SerializedProperty fileName;
        private SerializedProperty assetPath;

        private SerializedProperty forceToMono;
        private SerializedProperty loadInBackground;
        private SerializedProperty ambisonic;

        private SerializedProperty loadType;
        private SerializedProperty preloadAudioData;
        private SerializedProperty compressionFormat;
        private SerializedProperty quality;
        private SerializedProperty sampleRateSetting;

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region GetPropertyHeight

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            CacheSerializedProperties(property);
            CacheAudioClipData(audioClip.objectReferenceValue as AudioClip);
            
            if (!property.isExpanded)
            {
                return GgGUI.singleLineHeight + (GgGUI.standardSpacing * 2);
            }
            
            return (audioClip.objectReferenceValue == null
                ? (GgGUI.singleLineHeight + GgGUI.standardSpacing) * 3
                : ((GgGUI.singleLineHeight + GgGUI.standardSpacing) * 11) + GgGUI.standardSpacing) + GgGUI.standardSpacing;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region OnGUI
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // open property and get reference to instance
            EditorGUI.BeginProperty(position, label, property);
            CacheSerializedProperties(property);
            bool guiEnabled = GUI.enabled;
            
            // draw header
            Rect dropdownRect = new Rect(position.x, position.y, position.width, position.height - GgGUI.standardSpacing);
            GgGUI.GetFoldoutPositionRects(dropdownRect, label, out GgFoldoutPositions foldoutPositions);
            GgGUI.DrawDropdownHeader(foldoutPositions, property, label, false, true);
            
            // draw object field
            bool changed = GgGUI.ObjectField(foldoutPositions.field, GUIContent.none, audioClip.objectReferenceValue, out Object outputValue, typeof(AudioClip), property.hasMultipleDifferentValues, false);
            AudioClip audioClipRef = outputValue as AudioClip;
            
            // cache AudioClipData if AudioClip updated in inspector
            if (changed)
            {
                audioClip.objectReferenceValue = audioClipRef;
                CacheAudioClipData(audioClipRef);
            }
            
            // if not expanded: hide extra info & close property
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }
            
            // show extra info ...
            if (audioClip.objectReferenceValue)
            {
                // ... content
                GUI.enabled = false;
                EditorGUI.indentLevel++;
                
                GUIContent moreInfoLabel = new GUIContent("File Name", "Runtime reference to the fileName of the audioClip.");
                EditorGUI.TextField(foldoutPositions.content, moreInfoLabel, fileName.stringValue);
                
                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("AudioClip File Path", "Runtime reference to the assetPath of the audioClip.");
                EditorGUI.TextField(foldoutPositions.content, moreInfoLabel, assetPath.stringValue);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("Force To Mono", "Runtime reference to the forceToMono value of the audioClip.");
                EditorGUI.Toggle(foldoutPositions.content, moreInfoLabel, forceToMono.boolValue);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("Load In Background", "Runtime reference to the loadInBackground value of the audioClip.");
                EditorGUI.Toggle(foldoutPositions.content, moreInfoLabel, loadInBackground.boolValue);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("Ambisonic", "Runtime reference to the ambisonic value of the audioClip.");
                EditorGUI.Toggle(foldoutPositions.content, moreInfoLabel, ambisonic.boolValue);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("Load Type", "Runtime reference to the loadType value of the audioClip.");
                EditorGUI.TextField(foldoutPositions.content, moreInfoLabel, loadType.enumNames[loadType.enumValueIndex]);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("Preload Audio Data", "Runtime reference to the preloadAudioData value of the audioClip.");
                EditorGUI.Toggle(foldoutPositions.content, moreInfoLabel, preloadAudioData.boolValue);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("Compression Format", "Runtime reference to the compressionFormat value of the audioClip.");
                EditorGUI.TextField(foldoutPositions.content, moreInfoLabel, compressionFormat.enumNames[compressionFormat.enumValueIndex]);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("Quality", "Runtime reference to the quality value of the audioClip.");
                EditorGUI.FloatField(foldoutPositions.content, moreInfoLabel, quality.floatValue);

                foldoutPositions.content.y += GgGUI.singleLineHeight + GgGUI.standardSpacing;
                moreInfoLabel = new GUIContent("SampleRateSetting", "Runtime reference to the sampleRateSetting value of the audioClip.");
                EditorGUI.TextField(foldoutPositions.content, moreInfoLabel, sampleRateSetting.enumNames[sampleRateSetting.enumValueIndex]);
                    
                EditorGUI.indentLevel--;
                GUI.enabled = guiEnabled;
            }
            else
            {
                // ... warning
                Rect thisContent = foldoutPositions.content;
                thisContent.height = GgGUI.singleLineHeight * 2;
                EditorGUI.HelpBox(thisContent, "Warning: Reference object asset is null.", MessageType.Warning);
            }
            
            // close property
            EditorGUI.EndProperty();
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions

        private void CacheSerializedProperties(SerializedProperty property)
        {
            audioClip = property.FindPropertyRelative("audioClip");
            fileName = property.FindPropertyRelative("fileName");
            assetPath = property.FindPropertyRelative("assetPath");
            forceToMono = property.FindPropertyRelative("forceToMono");
            loadInBackground = property.FindPropertyRelative("loadInBackground");
            ambisonic = property.FindPropertyRelative("ambisonic");
            loadType = property.FindPropertyRelative("loadType");
            preloadAudioData = property.FindPropertyRelative("preloadAudioData");
            compressionFormat = property.FindPropertyRelative("compressionFormat");
            quality = property.FindPropertyRelative("quality");
            sampleRateSetting = property.FindPropertyRelative("sampleRateSetting");
        }
        
        private void CacheAudioClipData(AudioClip audioClipRef)
        {
            if (AudioClipData.Editor_CreateAudioClipData(audioClipRef, out AudioClipData audioClipData))
            {
                fileName.stringValue = audioClipData.FileName;
                assetPath.stringValue = audioClipData.AssetPath;
                forceToMono.boolValue = audioClipData.ForceToMono;
                loadInBackground.boolValue = audioClipData.LoadInBackground;
                ambisonic.boolValue = audioClipData.Ambisonic;
                loadType.enumValueIndex = audioClipData.LoadType.ToInt();
                preloadAudioData.boolValue = audioClipData.PreloadAudioData;
                compressionFormat.enumValueIndex = audioClipData.CompressionFormat.ToInt();
                quality.floatValue = audioClipData.Quality;
                sampleRateSetting.intValue = audioClipData.Editor_SampleRateSetting.ToInt();
            }
            else
            {
                fileName.stringValue = "";
                assetPath.stringValue = "";
                forceToMono.boolValue = false;
                loadInBackground.boolValue = false;
                ambisonic.boolValue = false;
                loadType.enumValueIndex = 0;
                preloadAudioData.boolValue = false;
                compressionFormat.enumValueIndex = 0;
                quality.floatValue = 100;
                sampleRateSetting.intValue = 0;
            }
        }

        #endregion

    } // class end
}
#endif