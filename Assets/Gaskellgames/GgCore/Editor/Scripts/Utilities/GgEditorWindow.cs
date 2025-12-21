#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public abstract class GgEditorWindow : EditorWindow
    {
        #region Variables
        
        protected readonly float toolbarHeight = 21.0f;  // the vertical size in pixels of the toolbar
        protected readonly float ShadowHeight = 2.0f;    // the vertical size in pixels of the shadow
        protected readonly float shadowBuffer = 3.0f;    // the vertical size in pixels of the buffer zone around a shadow
        protected readonly float scrollBarOffset = 6.0f; // the vertical and horizontal offset added when scroll view is active
        
        protected static EditorWindow editorWindow;
        protected Texture banner;
        protected SemanticVersion version;
        
        protected float pageHeight => Screen.height - (toolbarHeight + shadowBuffer);
        protected float pageWidth => EditorGUIUtility.currentViewWidth;
        protected float bannerWidth => pageWidth;
        protected float bannerHeight => bannerWidth * banner.height / banner.width;
        protected bool isWindowWide => 500 < Screen.width;
        protected int singleLineHeight => (int)EditorGUIUtility.singleLineHeight;
        protected int standardSpacing => (int)EditorGUIUtility.standardVerticalSpacing;

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region VerboseLogs

        protected bool VerboseLogs => GaskellgamesSettings_SO.Instance.ShowLogs;
        
        /// <summary>
        /// If verbose logs is true: info message logs will be displayed in the console alongside warning and error message logs.
        /// </summary>
        /// <param name="logType">Type of log to show in the console.</param>
        /// <param name="format">String format of the log to be shown.</param>
        /// <param name="args">Arguments to be injected to the string format.</param>
        protected void Log(GgLogType logType, string format, params object[] args)
        {
            if (logType == GgLogType.Info && !VerboseLogs) return;
            GgLogs.Log(this, logType, format, args);
        }
        
        /// <summary>
        /// If verbose logs is true: info message logs will be displayed in the console alongside warning and error message logs.
        /// </summary>
        /// <param name="messageColor">Color of the message to display in the console</param>
        /// <param name="logType">Type of log to show in the console.</param>
        /// <param name="format">String format of the log to be shown.</param>
        /// <param name="args">Arguments to be injected to the string format.</param>
        protected void Log(Color32 messageColor, GgLogType logType, string format, params object[] args)
        {
            if (logType == GgLogType.Info && !VerboseLogs) return;
            GgLogs.Log(messageColor, this, logType, format, args);
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Menu Item [To be implemented in child]
        
        /*
        [MenuItem(MenuItemUtility.NAME_ToolsMenu_Path, false, MenuItemUtility.NAME_ToolsMenu_Priority)]
        private static void OpenWindow_ToolsMenu()
        {
            OpenWindow<NAMEEditorWindow>();
        }

        [MenuItem(MenuItemUtility.NAME_WindowMenu_Path, false, MenuItemUtility.NAME_WindowMenu_Priority)]
        public static void OpenWindow_WindowMenu()
        {
            OpenWindow<NAMEEditorWindow>();
        }
        */
        
        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Editor Loop

        protected virtual void OnBecameVisible()
        {
            OnInitialise();
        }

        protected virtual void OnFocus()
        {
            OnFocusChange(true);
        }

        protected virtual void OnLostFocus()
        {
            OnFocusChange(false);
        }

        private void OnGUI()
        {
            OnGUI_Toolbar();
            OnPageGUI();
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Toolbar Tree
        
        private void OnGUI_Toolbar()
        {
            // draw page background
            EditorGUI.DrawRect(new Rect(0, toolbarHeight, Screen.width, Screen.height - toolbarHeight), InspectorExtensions.backgroundNormalColorDark);
            EditorGUI.DrawRect(new Rect(0, toolbarHeight, Screen.width, ShadowHeight), InspectorExtensions.backgroundShadowColor);
            
            // draw toolbar
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawSubToolbar(LeftToolbar());
            GUILayout.FlexibleSpace();
            DrawSubToolbar(RightToolbar());
            DrawOptions(OptionsToolbar());
            GUILayout.EndHorizontal();
        }

        private void DrawSubToolbar(List<GgToolbarItem> toolbarItems)
        {
            // list null check
            if (toolbarItems == null) { return; }
            if (toolbarItems.Count <= 0) { return; }
            
            Color32 defaultContent = GUI.contentColor;
            bool defaultGui = GUI.enabled;
            foreach (var item in toolbarItems)
            {
                // item null check
                if (item == null) { continue; }
                GUI.enabled = item.toolbarMethod != null;
                GUI.contentColor = item.guiColor;
                
                // draw item in toolbar
                if (GUILayout.Button(item.guiContent, EditorStyles.toolbarButton))
                {
                    if (item.toolbarMethod == null) { continue; }
                    item.toolbarMethod();
                }
                GUI.contentColor = defaultContent;
                GUI.enabled = defaultGui;
            }
        }

        private void DrawOptions(GenericMenu toolsMenu)
        {
            // null check
            if (toolsMenu == null) { return; }
            if (toolsMenu.GetItemCount() <= 0) { return; }
            
            // cache variables
            int pixelHeight = 16;
            int dropdownOffset = 5;
            int dropDownWidth = 65;
            
            if (GUILayout.Button("Options", EditorStyles.toolbarDropDown, GUILayout.Width(dropDownWidth)))
            {
                // offset menu from right of editor window
                toolsMenu.DropDown(new Rect(Screen.width - dropDownWidth, dropdownOffset, 0, pixelHeight));
                EditorGUIUtility.ExitGUI();
            }
        }

        protected void OnSupport_AssetStoreLink()
        {
            Help.BrowseURL("https://assetstore.unity.com/publishers/75563");
        }

        protected void OnSupport_DiscordLink()
        {
            Help.BrowseURL("https://discord.gg/nzRQ87GGbD");
        }

        protected void OnSupport_WebsiteLink()
        {
            Help.BrowseURL("https://gaskellgames.com");
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Virtual Functions

        /// <summary>
        /// Used to initialise any values in the editor window.
        /// Called from base class during <see cref="OnBecameVisible"/>
        /// </summary>
        protected abstract void OnInitialise();
        
        /// <summary>
        /// Used to re-initialise any values in the editor window, when re-focusing on/off the window.
        /// Called from base class during &lt;see cref="OnFocus"/&gt; and &lt;see cref="OnLostFocus"/&gt;
        /// </summary>
        /// <param name="hasFocus"></param>
        protected abstract void OnFocusChange(bool hasFocus);
        
        /// <summary>
        /// Used to add items to the left had side of the toolbar menu.
        /// Called from base class during <see cref="OnGUI"/>.
        /// </summary>
        /// <returns></returns>
        protected abstract List<GgToolbarItem> LeftToolbar();

        /// <summary>
        /// Used to add items to the right had side of the toolbar menu.
        /// Called from base class during <see cref="OnGUI"/>.
        /// </summary>
        /// <returns></returns>
        protected abstract List<GgToolbarItem> RightToolbar();

        /// <summary>
        /// Used to add items to the options menu of the toolbar menu.
        /// Called from base class during <see cref="OnGUI"/>.
        /// </summary>
        /// <returns></returns>
        protected abstract GenericMenu OptionsToolbar();

        /// <summary>
        /// Used to draw the page content of the window.
        /// Called from base class during <see cref="OnGUI"/>.
        /// </summary>
        protected abstract void OnPageGUI();

        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Protected Functions

        protected enum Placement
        {
            Console,
            GameView,
            Hierarchy,
            InspectorWindow,
            SceneView,
        }
        
        /// <summary>
        /// Auto setup editor window using set parameters
        /// </summary>
        /// <param name="editorWindowTitle"></param>
        /// <param name="utilityWindow"></param>
        /// <param name="openDocked"></param>
        /// <param name="placement"></param>
        /// <typeparam name="T"></typeparam>
        protected static void OpenWindow<T>(string editorWindowTitle, int width = 750, int height = 500, bool utilityWindow = false, bool openDocked = false, Placement placement = Placement.GameView) where T : EditorWindow
        {
            if (utilityWindow)
            {
                // Create new utility window from menu item
                editorWindow = GetWindow<T>(true);
                editorWindow.titleContent = new GUIContent(editorWindowTitle);

                // Limit size of the window
                editorWindow.minSize = new Vector2(width, height);
                editorWindow.maxSize = editorWindow.minSize;
            }
            else
            {
                // use reflection to get type of game window, to dock this window next to it (in order of preference)
                Type desiredDockNextTo;
                switch (placement)
                {
                    case Placement.Console:
                        desiredDockNextTo = Type.GetType("UnityEditor.ConsoleWindow, UnityEditor.dll");
                        break;
                    case Placement.GameView:
                        desiredDockNextTo = Type.GetType("UnityEditor.GameView, UnityEditor.dll");
                        break;
                    case Placement.Hierarchy:
                        desiredDockNextTo = Type.GetType("UnityEditor.HierarchyWindow, UnityEditor.dll");
                        break;
                    case Placement.InspectorWindow:
                        desiredDockNextTo = Type.GetType("UnityEditor.InspectorWindow, UnityEditor.dll");
                        break;
                    case Placement.SceneView:
                        desiredDockNextTo = Type.GetType("UnityEditor.SceneView, UnityEditor.dll");
                        break;
                    default:
                        desiredDockNextTo = null;
                        break;
                }

                editorWindow = openDocked
                    ? GetWindow<T>(editorWindowTitle, true, desiredDockNextTo)
                    : GetWindow<T>(editorWindowTitle, true);
                editorWindow.minSize = new Vector2(750, 500);
                editorWindow.Show();
            }
        }
        
        protected void PlaceHolderPageText(string pageName = "")
        {
            // set style
            GUIStyle myStyle = new GUIStyle();
            myStyle.alignment = TextAnchor.MiddleCenter;
            myStyle.normal.textColor = InspectorExtensions.textDisabledColor;
            
            // draw placeholder window
            if (pageName == "") { EditorGUILayout.LabelField("Window is empty", myStyle); }
            else { EditorGUILayout.LabelField(pageName + " is empty", myStyle); }
        }

        #endregion
        
    } // class end
}

#endif