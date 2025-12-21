#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    [Serializable]
    public class GgGUIDefaults
    {
        public bool guiEnabled = GUI.enabled;
        public Color32 guiBackgroundColor = GUI.backgroundColor;
        public Color32 guiContentColor = GUI.contentColor;
        public Color32 guiColor = GUI.color;
        public float defaultLabelWidth = EditorGUIUtility.labelWidth;

        public void ResetDefaults()
        {
            GUI.enabled = guiEnabled;
            GUI.backgroundColor = guiBackgroundColor;
            GUI.contentColor = guiContentColor;
            GUI.color = guiColor;
            EditorGUIUtility.labelWidth = defaultLabelWidth;
        }
        
    } // class end
}
#endif