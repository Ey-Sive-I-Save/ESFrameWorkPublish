#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class GaskellgamesHub : GgEditorWindow
    {
        #region Variables
        
        private static readonly int windowWidth = 725;
        private static readonly int windowHeight = 500;
        private readonly int infoHeight = 100;
        private readonly int logoSize = 75;
        private Vector2 scrollPosL;
        private Vector2 scrollPosR;
        private float scrollHeight => windowHeight - (toolbarHeight + bannerHeight + infoHeight + 50);
        private float packagesWidth => pageWidth * 0.58f;
        private float settingsWidth => pageWidth - (packagesWidth + (standardSpacing * 4));
        private float settingsLabelWidth => settingsWidth * 0.7f;

        // package paths - downloadable
        private const string packageRefName_GgCore = "GgCore";
        private const string relativePath_GgCore = "/Editor/Icons/";
        
        private const string packageRefName_AudioSystem = "AudioSystem";
        private const string packageRefName_CameraSystem = "CameraSystem";
        private const string packageRefName_CharacterController = "CharacterController";
        private const string packageRefName_FolderSystem = "FolderSystem";
        private const string packageRefName_InputEventSystem = "InputEventSystem";
        private const string packageRefName_MenuSystem = "MenuSystem";
        private const string packageRefName_PlatformController = "PlatformController";
        private const string packageRefName_PoolingSystem = "PoolingSystem";
        private const string packageRefName_SceneController = "SceneController";
        private const string packageRefName_SplineSystem = "SplineSystem";
        private const string packageRefName_TagSystem = "TagSystem";
        private const string packageRefName_TriggerSystem = "TriggerSystem";
        
        private const string relativePath = "/Editor/Icons/";
        
        // package icons - helper
        private Texture icon_GgCore;
        private Texture icon_AudioSystem;
        private Texture icon_CameraSystem;
        private Texture icon_CharacterController;
        private Texture icon_FolderSystem;
        private Texture icon_InputEventSystem;
        private Texture icon_MenuSystem;
        private Texture icon_PlatformController;
        private Texture icon_PoolingSystem;
        private Texture icon_SceneController;
        private Texture icon_SplineSystem;
        private Texture icon_TagSystem;
        private Texture icon_TriggerSystem;
        
        // settings - helper
        private Editor settingsEditor;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Menu Item
        
        [MenuItem(MenuItemUtility.Hub_ToolsMenu_Path, false, MenuItemUtility.Hub_Priority)]
        private static void OpenWindow_ToolsMenu()
        {
            OpenWindow_WindowMenu();
        }

        [MenuItem(MenuItemUtility.Hub_WindowMenu_Path, false, MenuItemUtility.Hub_Priority)]
        public static void OpenWindow_WindowMenu()
        {
            OpenWindow<GaskellgamesHub>("Gaskellgames Hub", windowWidth, windowHeight, true);
        }
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Overriding Functions

        protected override void OnInitialise()
        {
            InitialiseSettings();
        }

        protected override void OnFocusChange(bool hasFocus)
        {
            if (!hasFocus) { return; }
            InitialiseSettings();
        }

        protected override List<GgToolbarItem> LeftToolbar()
        {
            string copyright = isWindowWide ? "Copyright \u00a9 2022-2025 Gaskellgames. All rights reserved." : "\u00a9 Gaskellgames.";
            List<GgToolbarItem> leftToolbar = new List<GgToolbarItem>
            {
                new (null, new GUIContent(copyright)),
            };

            return leftToolbar;
        }

        protected override List<GgToolbarItem> RightToolbar()
        {
            bool hasVersion = GgPackageRef.TryGetVersion(packageRefName_GgCore, out version);
            string versionAsString = isWindowWide
                ? hasVersion ? version.GetVersionLong() : "Version ?.?.?"
                : hasVersion ? version.GetVersionShort() : "v?.?.?";
            List<GgToolbarItem> leftToolbar = new List<GgToolbarItem>
            {
                new (null, new GUIContent(versionAsString)),
            };

            return leftToolbar;
        }

        protected override GenericMenu OptionsToolbar()
        {
            GenericMenu toolsMenu = new GenericMenu();
            toolsMenu.AddItem(new GUIContent("Gaskellgames Unity Page"), false, OnSupport_AssetStoreLink);
            toolsMenu.AddItem(new GUIContent("Gaskellgames Discord"), false, OnSupport_DiscordLink);
            toolsMenu.AddItem(new GUIContent("Gaskellgames Website"), false, OnSupport_WebsiteLink);
            return toolsMenu;
        }
        
        protected override void OnPageGUI()
        {
            // banner
            EditorWindowUtility.TryDrawBanner(banner, "Gaskellgames Hub", true, true);
            
            // info
            DrawInfoMessage();
            
            // start content...
            EditorExtensions.DrawInspectorLine(InspectorExtensions.backgroundSeperatorColor, 4, 0);
            EditorGUILayout.BeginHorizontal();
            // ... packages
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(packagesWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Downloaded Packages:", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            scrollPosL = EditorGUILayout.BeginScrollView(scrollPosL, GUILayout.Height(scrollHeight));
            DrawDownloadedPackages();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            // ... packages
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(settingsWidth), GUILayout.ExpandHeight(true));
            EditorGUILayout.LabelField("Settings:", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            scrollPosR = EditorGUILayout.BeginScrollView(scrollPosR, GUILayout.Height(scrollHeight));
            DrawSettings();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            // ... end content
            EditorGUILayout.EndHorizontal();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions
        
        private void InitialiseSettings()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            settingsEditor = GaskellgamesSettings_SO.Instance ? Editor.CreateEditor(GaskellgamesSettings_SO.Instance) : null;
            LoadPackageIcons();
        }

        private void LoadPackageIcons()
        {
            if (GgPackageRef.TryGetFullFilePath(packageRefName_GgCore, relativePath_GgCore, out string filePath))
                icon_GgCore = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_GgCore.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_AudioSystem, relativePath, out filePath))
                icon_AudioSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_AudioSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_CameraSystem, relativePath, out filePath))
                icon_CameraSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_CameraSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_CharacterController, relativePath, out filePath))
                icon_CharacterController = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_CharacterController.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_FolderSystem, relativePath, out filePath))
                icon_FolderSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_HierarchyFolderSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_InputEventSystem, relativePath, out filePath))
                icon_InputEventSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_InputEventSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_MenuSystem, relativePath, out filePath))
                icon_MenuSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_MenuSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_PlatformController, relativePath, out filePath))
                icon_PlatformController = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_PlatformController.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_PoolingSystem, relativePath, out filePath))
                icon_PoolingSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_PoolingSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_SceneController, relativePath, out filePath))
                icon_SceneController = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_SceneController.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_SplineSystem, relativePath, out filePath))
                icon_SplineSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_SplineSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_TagSystem, relativePath, out filePath))
                icon_TagSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_TagSystem.png", typeof(Texture));
            
            if (GgPackageRef.TryGetFullFilePath(packageRefName_TriggerSystem, relativePath, out filePath))
                icon_TriggerSystem = (Texture)AssetDatabase.LoadAssetAtPath(filePath + "Logo_TriggerSystem.png", typeof(Texture));
        }

        private void DrawInfoMessage()
        {
            GUI.enabled = false;
            float defaultHeight = EditorStyles.textField.fixedHeight;
            EditorStyles.textField.fixedHeight = infoHeight;
            EditorGUILayout.TextArea("Thank you for installing a Gaskellgames asset, and welcome to the settings hub!\n\n" +
                                     "Any settings options you choose here will be applied to all relevant Gaskellgames packages.\n\n" +
                                     "Links to the Unity Asset Store page, Gaskellgames Discord and Gaskellgames Website are available via the 'options' dropdown\n" +
                                     "menu above. (Note: Please read through each packages documentation pdf before contacting Gaskellgames with any queries.)");
            EditorStyles.textField.fixedHeight = defaultHeight;
            GUI.enabled = true;
        }
        
        private void DrawDownloadedPackages()
        {
            EditorGUILayout.BeginHorizontal();
            
            float xMin = standardSpacing;
            xMin = DrawPackageLogo(icon_GgCore, "Gg Core", true, xMin);
            xMin = DrawPackageLogo(icon_AudioSystem, "Audio Controller", true, xMin);
            xMin = DrawPackageLogo(icon_CameraSystem, "Camera System", true, xMin);
            xMin = DrawPackageLogo(icon_CharacterController, "Character Controller", true, xMin);
            xMin = DrawPackageLogo(icon_FolderSystem, "Folder System", true, xMin);
            xMin = DrawPackageLogo(icon_InputEventSystem, "Input Event System", true, xMin);
            xMin = DrawPackageLogo(icon_MenuSystem, "Menu System", true, xMin);
            xMin = DrawPackageLogo(icon_PlatformController, "Platform Controller", true, xMin);
            xMin = DrawPackageLogo(icon_PoolingSystem, "Pooling System", true, xMin);
            xMin = DrawPackageLogo(icon_SceneController, "Scene Group System", true, xMin);
            xMin = DrawPackageLogo(icon_SplineSystem, "Spline System", true, xMin);
            xMin = DrawPackageLogo(icon_TagSystem, "Tag System", true, xMin);
            xMin = DrawPackageLogo(icon_TriggerSystem, "Trigger System", true, xMin);
            
            EditorGUILayout.EndHorizontal();
        }

        private float DrawPackageLogo(Texture packageLogo, string toolTip, bool autoWrap = false, float xMin = 0)
        {
            if (!packageLogo) { return xMin; }
            
            // handle auto wrap
            if (autoWrap && (packagesWidth) < (xMin + logoSize + standardSpacing))
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                xMin = standardSpacing;
            }
            
            // draw package logo
            GUIContent label = new GUIContent(packageLogo, toolTip);
            GUILayout.Box(label, GUILayout.Width(logoSize), GUILayout.Height(logoSize));
            
            return xMin + logoSize + standardSpacing;
        }

        private void DrawSettings()
        {
            float defaultLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = settingsLabelWidth;
            if (settingsEditor && settingsEditor.target) { settingsEditor.OnInspectorGUI(true); }
            else { EditorGUILayout.HelpBox("Error: Settings asset not found.", MessageType.Error); }
            EditorGUIUtility.labelWidth = defaultLabelWidth;
        }

        #endregion
        
    } // class end
}

#endif