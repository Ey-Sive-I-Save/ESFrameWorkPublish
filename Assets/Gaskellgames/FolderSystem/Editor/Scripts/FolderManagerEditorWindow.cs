#if UNITY_EDITOR
#if GASKELLGAMES
using System;
using System.Collections.Generic;
using System.Linq;
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.FolderSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public class FolderManagerEditorWindow : GgEditorWindow
    {
        #region Variables
        
        private const string packageRefName = "FolderSystem";
        private const string iconsRelativePath = "/Editor/Icons/";
        private const string textureRelativePath = "/Editor/Textures/";
        private bool doSearch => !string.IsNullOrWhiteSpace(searchBarText) && searchBarText != "";
        private string searchBarText = "";
        private string newLink = "";
        
        private int iconSize = 50;
        private float leftPageWidth = Screen.width * 0.75f;
        private float rightPageWidth = Screen.width * 0.25f;
        private Vector2 leftPageScrollPos;
        private Vector2 rightPageScrollPos;
        
        private static int selectedIconIndex;
        private static List<GUIContent> folderIcons = new List<GUIContent>();
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Menu Item
        
        [MenuItem(MenuItemUtility.FolderSystem_ToolsMenu_Path, false, MenuItemUtility.FolderSystem_Priority)]
        private static void OpenWindow_ToolsMenu()
        {
            OpenWindow_WindowMenu();
        }

        [MenuItem(MenuItemUtility.FolderSystem_WindowMenu_Path, false, MenuItemUtility.FolderSystem_Priority)]
        public static void OpenWindow_WindowMenu()
        {
            OpenWindow<FolderManagerEditorWindow>("Folder Manager", 500, 750, false, true, Placement.GameView);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Overrides

        protected override void OnInitialise()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, iconsRelativePath, out string filePath)) { return; }
            banner = EditorWindowUtility.LoadInspectorBanner();
            RefreshAssetReferences();
        }

        protected override void OnFocusChange(bool hasFocus) { }

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
            bool hasVersion = GgPackageRef.TryGetVersion(packageRefName, out version);
            string versionAsString = isWindowWide
                ? hasVersion ? version.GetVersionLong() : "Version ?.?.?"
                : hasVersion ? version.GetVersionShort() : "v?.?.?";
            Texture refreshTexture = EditorGUIUtility.IconContent("d_Refresh").image;
            Texture clearTexture = EditorGUIUtility.IconContent("d_TreeEditor.Trash").image;
            
            List<GgToolbarItem> leftToolbar = new List<GgToolbarItem>
            {
                new (ClearUserPreferences, new GUIContent(clearTexture, "Clear User Preferences (Resets 'UserGenerated' folder name links)"), InspectorExtensions.redColorDark),
                new (RefreshAssetReferences, new GUIContent(refreshTexture, "Refresh Asset references")),
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
            if (!FolderSystemSettings_SO.Instance)
            {
                // error message
                string errorMessage = "No folder settings found! Please re-import package.";
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
                return;
            }
            
            // page top (options)
            bool drawBanner = EditorWindowUtility.TryDrawBanner(banner, "Folder Manager", true);
            EditorGUILayout.BeginHorizontal();
            OnPageGUI_DictionarySelection();
            OnPageGUI_SearchBar();
            OnPageGUI_DefaultIcons();
            EditorGUILayout.EndHorizontal();
            
            float optionsHeight = (singleLineHeight + standardSpacing) * 3;
            float remainingPageHeight = (drawBanner ? pageHeight - bannerHeight : pageHeight) - optionsHeight;
            EditorGUILayout.BeginHorizontal();
            
            // page left (icons)
            leftPageWidth = (pageWidth * 0.7f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(leftPageWidth), GUILayout.MaxHeight(remainingPageHeight));
            OnLeftPageGUI();
            EditorGUILayout.EndVertical();
            
            // page right (names)
            rightPageWidth = (pageWidth * 0.2f);
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MaxWidth(rightPageWidth), GUILayout.MaxHeight(remainingPageHeight));
            OnRightPageGUI();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region private functions
        
        private void ClearUserPreferences()
        {
            FolderSystemSettings_SO.Instance.ResetUserGeneratedLinks();
            RefreshAssetReferences();
        }
        
        private void RefreshAssetReferences()
        {
            // update auto-generated dictionary
            ProjectFolderIcons.AutoGenerateIconDictionary();
            
            // update user-generated dictionary
            folderIcons = new List<GUIContent>();
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, textureRelativePath, out string filePath)) { return; }
            List<Texture> folderTextures = EditorExtensions.GetAllAssetsByType<Texture>(new []{ filePath }, true);
            foreach (Texture texture in folderTextures)
            {
                GUIContent folderIcon = new GUIContent(texture, $"{texture.name}");
                folderIcons.TryAdd(folderIcon);
            }
            
            // rebuild user generated icon dictionary
            FolderSystemSettings_SO.Instance.CleanUserGeneratedLinks();
            
            // update list with new entries (missing texture icons)
            foreach (GUIContent guiContent in folderIcons)
            {
                if (FolderSystemSettings_SO.Instance.TryAddToUserGeneratedLinks(guiContent.tooltip, guiContent.image))
                {
                    EditorUtility.SetDirty(FolderSystemSettings_SO.Instance);
                }
            }
            
            // update in-use dictionary
            FolderSystemSettings_SO.Instance.CreateFolderIconDictionary();
        }
        
        private void OnPageGUI_DictionarySelection()
        {
            GUIContent label = new GUIContent("", "Select whether to use auto generated or user generated folder icon links.");
            FolderSystemSettings_SO.SelectedState selected = (FolderSystemSettings_SO.SelectedState)EditorGUILayout.EnumPopup(label, FolderSystemSettings_SO.Instance.ProjectFolderState, GUILayout.Width(125));
            if (FolderSystemSettings_SO.Instance.ProjectFolderState != selected)
            {
                FolderSystemSettings_SO.Instance.ProjectFolderState = selected;
                EditorUtility.SetDirty(FolderSystemSettings_SO.Instance);
            }
        }
        
        private void OnPageGUI_SearchBar()
        {
            GUILayout.BeginHorizontal();
            searchBarText = EditorGUILayout.TextField(searchBarText, EditorStyles.toolbarSearchField);
            Texture iconTexture = EditorGUIUtility.IconContent("d_clear").image;
            if (GUILayout.Button(new GUIContent("", iconTexture, "Clear Search"), EditorStyles.toolbarButton, GUILayout.Width(25)))
            {
                searchBarText = string.Empty;
            }
            GUILayout.EndHorizontal();
        }
        
        private void OnPageGUI_DefaultIcons()
        {
            Texture iconTexture = EditorGUIUtility.IconContent(FolderSystemSettings_SO.Instance.defaultFolderIcons ? "Folder Icon" : "FolderEmpty Icon").image;
            bool selected = GUILayout.Toggle(FolderSystemSettings_SO.Instance.defaultFolderIcons, new GUIContent(" Default ", iconTexture, "Use default folder icon?"), EditorStyles.toolbarButton, GUILayout.Width(75));
            if (selected != FolderSystemSettings_SO.Instance.defaultFolderIcons)
            {
                FolderSystemSettings_SO.Instance.defaultFolderIcons = selected;
                EditorUtility.SetDirty(FolderSystemSettings_SO.Instance);
            }
        }

        private void OnLeftPageGUI()
        {
            // cache defaults
            Color32 defaultBackground = GUI.backgroundColor;
            bool defaultEnabled = GUI.enabled;
            
            List<GUIContent> iconList = doSearch ? folderIcons.Where(x => x.tooltip.ToLower().Contains(searchBarText.ToLower())).ToList() : folderIcons;
            int totalItems = iconList.Count;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Folder Icons:", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUI.enabled = false;
            string iconTotal = $"Showing {totalItems} of {folderIcons.Count} Icons";
            EditorGUILayout.LabelField(iconTotal, GUILayout.Width(StringExtensions.GetStringWidth(iconTotal)));
            GUI.enabled = defaultEnabled;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            if (totalItems <= 0)
            {
                // error message
                string errorMessage = "No icons found.";
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
                return;
            }
            
            leftPageScrollPos = EditorGUILayout.BeginScrollView(leftPageScrollPos);
            
            // cache content variables
            int maxColumns = Mathf.FloorToInt((leftPageWidth - (scrollBarOffset + (standardSpacing * 4))) / (iconSize + (standardSpacing * 2)));
            int totalColumns = Mathf.Max(1, totalItems < maxColumns ? totalItems : maxColumns);
            int totalRows = (int)(totalItems / (float)totalColumns);
            totalRows += ((totalItems % totalColumns) != 0) ? 1 : 0;
            
            // iterate through items
            GUI.backgroundColor = InspectorExtensions.buttonNormalColorDark;
            int index = 0;
            for (int row = 0; row < totalRows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int column = 0; column < totalColumns; column++)
                {
                    if (totalItems <= index) { break; }
                    GUIContent currentIcon = iconList[index];
                    if (!currentIcon.image) { continue; }
                    
                    if (GUILayout.Button(currentIcon, GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                    {
                        selectedIconIndex = index;
                    }
                    index++;
                }

                if (totalItems == index)
                {
                    Texture addTexture = EditorGUIUtility.IconContent("d_CreateAddNew").image;
                    if (GUILayout.Button(new GUIContent(addTexture, "Add new image"), GUILayout.Width(iconSize), GUILayout.Height(iconSize)))
                    {
                        if (!GgPackageRef.TryGetFullFilePath(packageRefName, textureRelativePath, out string filePath)) { return; }
                        Object obj = EditorExtensions.GetAssetByType<Texture>(new []{ filePath });
                        EditorGUIUtility.PingObject(obj);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = defaultBackground;
            
            EditorGUILayout.EndScrollView();
        }

        private void OnRightPageGUI()
        {
            // cache defaults
            Color32 defaultBackground = GUI.backgroundColor;
            bool defaultEnabled = GUI.enabled;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Selected Icon:", EditorStyles.boldLabel);
            GUI.backgroundColor = InspectorExtensions.buttonNormalColorDark;
            Texture iconTexture = EditorGUIUtility.IconContent("d_P4_DeletedLocal").image;
            if (GUILayout.Button(new GUIContent(iconTexture, "Close selected"), GUILayout.Width(25), GUILayout.Height(25)))
            {
                selectedIconIndex = -1;
            }
            GUI.backgroundColor = defaultBackground;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            if (0 <= selectedIconIndex && selectedIconIndex < folderIcons.Count)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = InspectorExtensions.buttonNormalColorDark;
                GUILayout.Button(folderIcons[selectedIconIndex], GUILayout.Width(iconSize), GUILayout.Height(iconSize));
                GUI.backgroundColor = defaultBackground;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.enabled = false;
                string stringValue = "No icon selected...";
                EditorGUILayout.LabelField(stringValue, GUILayout.Width(StringExtensions.GetStringWidth(stringValue)));
                GUI.enabled = defaultEnabled;
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(iconSize - singleLineHeight);
            }
            EditorGUILayout.Space();
            if (selectedIconIndex < 0 || folderIcons.Count <= selectedIconIndex) { return; }
            EditorGUILayout.LabelField("Folder Names:", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // show / edit links
            rightPageScrollPos = EditorGUILayout.BeginScrollView(rightPageScrollPos);
            
            if (TryGetKeysForValue(folderIcons[selectedIconIndex].image, out List<string> keys, out bool showAdd))
            {
                GUI.enabled = false;
                EditorGUILayout.TextField(keys[0]);
                GUI.enabled = defaultEnabled;
                List<string> keyToRemove = new List<string>();
                for (int i = 1; i < keys.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUI.enabled = false;
                    EditorGUILayout.TextField(keys[i]);
                    GUI.enabled = defaultEnabled;
                    iconTexture = EditorGUIUtility.IconContent("Toolbar Minus").image;
                    if (GUILayout.Button(new GUIContent(iconTexture, "Remove folder name"), GUILayout.Width(20)))
                    {
                        keyToRemove.TryAdd(keys[i]);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                foreach (string key in keyToRemove)
                {
                    Undo.RecordObject (FolderSystemSettings_SO.Instance, "Folder Link Removed");
                    if (FolderSystemSettings_SO.Instance.RemoveFromUserGeneratedLinks(key))
                    {
                        EditorUtility.SetDirty(FolderSystemSettings_SO.Instance);
                    }
                }
            }

            if (showAdd)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Add Folder Link:", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                Undo.RecordObject (FolderSystemSettings_SO.Instance, "Folder Link Added");
                newLink = EditorGUILayout.TextField(newLink);
                iconTexture = EditorGUIUtility.IconContent("Toolbar Plus").image;
                if (GUILayout.Button(new GUIContent(iconTexture, "Add Folder Link"), GUILayout.Width(20)))
                {
                    if (FolderSystemSettings_SO.Instance.TryAddToUserGeneratedLinks(newLink, folderIcons[selectedIconIndex].image))
                    {
                        EditorUtility.SetDirty(FolderSystemSettings_SO.Instance);
                    }
                    ClearEntry();
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private async void ClearEntry()
        {
            newLink = "";
            if (await GgTask.WaitUntilNextFrame() != TaskResultType.Complete) { return; }
            newLink = "";
        }
        
        private bool TryGetKeysForValue(Texture image, out List<string> keys, out bool showAdd)
        {
            showAdd = FolderSystemSettings_SO.Instance.ProjectFolderState == FolderSystemSettings_SO.SelectedState.UserGenerated;
            if (showAdd)
            {
                return FolderSystemSettings_SO.Instance.TryGetKeysForValue(image, out keys);
            }
            
            keys = new List<string> { folderIcons[selectedIconIndex].tooltip };
            return true;
        }

        #endregion

    } // class end
}
#endif
#endif