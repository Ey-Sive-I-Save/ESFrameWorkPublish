#if UNITY_EDITOR
using System.Collections.Generic;
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    public class GgTaskTrackerEditorWindow : GgEditorWindow
    {
        #region Variables
        
        private const string packageRefName_GgCore = "GgCore";
        
        private Vector2 scrollPos;
        private MultiColumnHeader columnHeader;
        private MultiColumnHeaderState.Column[] columns;
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Menu Item
        
        [MenuItem(MenuItemUtility.GgTaskTracker_ToolsMenu_Path, false, MenuItemUtility.GgTaskTracker_Priority)]
        private static void OpenWindow_ToolsMenu()
        {
            OpenWindow_WindowMenu();
        }
        
        [MenuItem(MenuItemUtility.GgTaskTracker_WindowMenu_Path, false, MenuItemUtility.GgTaskTracker_Priority)]
        public static void OpenWindow_WindowMenu()
        {
            OpenWindow<GgTaskTrackerEditorWindow>("GgTask Tracker", 800, 500, false, false);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Overriding Functions
        
        protected override void OnInitialise()
        {
            InitialiseSettings();
            GgTask.trackedTasksUpdated += TrackedTasksUpdated;
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
            EditorWindowUtility.TryDrawBanner(banner, "GgTask Tracker", true);
            
            // draw table
            DrawInfo();
            EditorExtensions.DrawInspectorLineFull(InspectorExtensions.backgroundSeperatorColor, 0, 6);
            DrawTable();
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions
        
        private void OnDestroy()
        {
            GgTask.DebugMode = false;
        }
        
        private void InitialiseSettings()
        {
            banner = EditorWindowUtility.LoadInspectorBanner();
            TrackedTasksUpdated();
            InitialiseTable();
        }

        private void InitialiseTable()
        {
            // int columnCount = 8;
            // float averageColumnWidth = (position.width - (scrollBarOffset + (standardSpacing * (columnCount * 3) + 2))) / columnCount;
            
            columns = new MultiColumnHeaderState.Column[]
            {
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Called From", "The script, method and line that the task was called from."),
                    width = 250,
                    minWidth = 100,
                    autoResize = true,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Type", "The type of GgTask."),
                    width = 125,
                    minWidth = 125,
                    autoResize = false,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Status", "The current status of the task."),
                    width = 90,
                    minWidth = 90,
                    autoResize = false,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Timeout", "The timeout duration, in seconds, of the task."),
                    width = 60,
                    minWidth = 60,
                    autoResize = false,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Frequency", "The update frequency, in milliseconds, of the task."),
                    width = 70,
                    minWidth = 70,
                    autoResize = false,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Time", "The system time when the task was called."),
                    width = 60,
                    minWidth = 60,
                    autoResize = false,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Frame", "The frame the task was called on."),
                    width = 50,
                    minWidth = 50,
                    autoResize = false,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
                new MultiColumnHeaderState.Column()
                {
                    headerContent = new GUIContent("Thread", "The thread ID used to manage the task."),
                    width = 50,
                    minWidth = 50,
                    autoResize = false,
                    canSort = true,
                    headerTextAlignment = TextAlignment.Left,
                    sortingArrowAlignment = TextAlignment.Right
                },
            };
            columnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = singleLineHeight + standardSpacing,
            };
            columnHeader.ResizeToFit();
            columnHeader.sortingChanged -= OnSortingChanged;
            columnHeader.sortingChanged += OnSortingChanged;
        }

        private void OnSortingChanged(MultiColumnHeader multiColumnHeader)
        {
            GgTask.SortBy = (GgTask.SortingType)multiColumnHeader.sortedColumnIndex;
            GgTask.IsSortedAscending = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);
        }

        private void TrackedTasksUpdated()
        {
            Repaint();
        }

        private void DrawInfo()
        {
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.LabelField(isWindowWide ? "Tracked Tasks: " : "Tasks: " + GgTask.trackedTasks.Count, GUILayout.Width(isWindowWide ? GgGUI.labelWidth : 75));
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(" Resize Table ", "Resizes the table width to fit the window width."), EditorStyles.miniButtonLeft))
            {
                InitialiseTable();
            }
            if (GUILayout.Button(new GUIContent(" Cancel All ", "Cancel all 'InProgress' GgTasks."), EditorStyles.miniButtonMid))
            {
                GgTask.CancelAllTasks();
            }
            Texture iconTexture = EditorGUIUtility.IconContent(GgTask.DebugMode ? "d_DebuggerAttached" : "d_debug").image;
            bool changeCheck = GgTask.DebugMode;
            if (GUILayout.Toggle(GgTask.DebugMode, new GUIContent(" Debug ", iconTexture, "Keep a record of all tasks for debugging."), EditorStyles.miniButtonRight))
            {
                if (!changeCheck)
                {
                    GgTask.DebugMode = true;
                }
            }
            else
            {
                if (changeCheck)
                {
                    GgTask.ClearTrackedTasks();
                    GgTask.DebugMode = false;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawTable()
        {
            // calculate the window visible rect
            float infoHeight = singleLineHeight + 8;
            float yPosition = toolbarHeight + shadowBuffer + infoHeight + (GaskellgamesSettings_SO.Instance.ShowEditorWindowBanner ? bannerHeight : 0);
            Rect headerRect = new Rect(standardSpacing, yPosition, pageWidth - standardSpacing - scrollBarOffset, columnHeader.height);
            
            // draw the column headers
            columnHeader.OnGUI(headerRect, scrollPos.x);
            
            // used to offset EditorGUILayout to compensate for header being GUILayout
            GUILayout.Space(columnHeader.height);
            
            // draw tasks
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (int i = GgTask.trackedTasks.Count - 1; i >= 0; i--)
            {
                if (GgTask.trackedTasks[i] == null) { continue; }
                DrawTrackedTask(GgTask.trackedTasks[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTrackedTask(GgTrackedTask trackedTask)
        {
            string calledFrom = $"{trackedTask.callerScript}: {trackedTask.callerMemberName}: {trackedTask.callerLineNumber}";
            string calledFromLong = $"Script: {trackedTask.callerScript}\nMethod: {trackedTask.callerMemberName}\nLine: {trackedTask.callerLineNumber}";
            float offset = standardSpacing * 2;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(new GUIContent(calledFrom, calledFromLong), GUILayout.Width(columns[0].width - offset));
            GUI.enabled = false;
            EditorGUILayout.TextField(GUIContent.none, trackedTask.taskType, GUILayout.Width(columns[1].width - offset));
            EditorGUILayout.EnumPopup(GUIContent.none, trackedTask.Status, GUILayout.Width(columns[2].width - offset));
            EditorGUILayout.TextField(GUIContent.none, trackedTask.timeout <= 0 ? "Infinite" : trackedTask.timeout.ToString(), GUILayout.Width(columns[3].width - offset));
            EditorGUILayout.TextField(GUIContent.none, trackedTask.frequency <= 0 ? "n/a" : trackedTask.frequency.ToString(), GUILayout.Width(columns[4].width - offset));
            EditorGUILayout.TextField(GUIContent.none, trackedTask.datetime.ToString("T"), GUILayout.Width(columns[5].width - offset));
            EditorGUILayout.TextField(GUIContent.none, trackedTask.frameCount.ToString(), GUILayout.Width(columns[6].width - offset));
            EditorGUILayout.TextField(GUIContent.none, trackedTask.managedThreadId.ToString(), GUILayout.Width(columns[7].width - offset));
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        #endregion
        
    } // class end
}
#endif