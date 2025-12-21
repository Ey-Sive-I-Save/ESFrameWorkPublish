#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [InitializeOnLoad]
    public static class EditorWindowUtility
    {
        #region Variables
        
        public static ShaderAutoUpdater_SO shaderAutoUpdater;
        
        private const string bannerPackageRefName = "GgCore";
        private const string bannerRelativePath = "/Editor/Icons/Banner_GgMonoBehaviour.png";
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Constructor

        static EditorWindowUtility()
        {
            Initialisation();
            EditorApplication.update += RunOnceOnStartup;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions

        private static void Initialisation()
        {
            shaderAutoUpdater = EditorExtensions.GetAssetByType<ShaderAutoUpdater_SO>();
        }
        
        private static void RunOnceOnStartup()
        {
            if (!shaderAutoUpdater) { Initialisation(); }
            if (SessionState.GetBool("EditorWindowUtilityFirstInit", false)) { return; }

            if (GaskellgamesSettings_SO.Instance && GaskellgamesSettings_SO.Instance.ShowHubOnStartup)
            {
                GaskellgamesHub.OpenWindow_WindowMenu();
            }

            if (shaderAutoUpdater)
            {
                shaderAutoUpdater.UpdateMaterialsForCurrentTargetPipeline();
            }
            
            SessionState.SetBool("EditorWindowUtilityFirstInit", true);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Public Functions

        public static Texture LoadInspectorBanner()
        {
            if (GgPackageRef.TryGetFullFilePath(bannerPackageRefName, bannerRelativePath, out string fullFilepath))
            {
                return (Texture)AssetDatabase.LoadAssetAtPath(fullFilepath, typeof(Texture));
            }

            return null;
        }
        
        public static bool TryDrawBanner(Texture banner, string textOverlay, bool editorWindow = false, bool forceShow = false)
        {
            // null and condition check
            if (!GaskellgamesSettings_SO.Instance) { return false; }
            if (!forceShow)
            {
                if (!GaskellgamesSettings_SO.Instance.ShowPackageBanners) { return false; }
                if (editorWindow && !GaskellgamesSettings_SO.Instance.ShowEditorWindowBanner) { return false; }
                if (!editorWindow && !GaskellgamesSettings_SO.Instance.ShowComponentBanner) { return false; }
            }
            if (!banner) { return false; }
            
            float imageWidth = EditorGUIUtility.currentViewWidth;
            float imageHeight = imageWidth * banner.height / banner.width;
            Rect rect = GUILayoutUtility.GetRect(imageWidth, imageHeight);
            
            // adjust rect to account for offsets in inspectors
            if (!editorWindow)
            {
                float paddingTop = -4;
                float paddingLeft = -18;
                float paddingRight = -4;
                
                // calculate rect size
                float xMin = rect.x + paddingLeft;
                float yMin = rect.y + paddingTop;
                float width = rect.width - (paddingLeft + paddingRight);
                float height = rect.height;

                rect = new Rect(xMin, yMin, width, height);
            }
            
            // draw banner
            GUI.DrawTexture(rect, banner, ScaleMode.ScaleToFit);
            
            // draw label
            float labelSize = rect.height * 0.5f;
            Rect textRect = new Rect(rect.x + (editorWindow ? 0 : labelSize * 0.5f), rect.y, rect.width, rect.height);
            GUIStyle headerLabelStyle = new GUIStyle
            {
                normal = { textColor = InspectorExtensions.textNormalColor },
                fontSize = (int)labelSize,
                fontStyle = FontStyle.Bold,
                alignment = editorWindow ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
            };
            GUI.Label(textRect, textOverlay, headerLabelStyle);
            
            return true;
        }

        #endregion
        
    } // class end
}

#endif