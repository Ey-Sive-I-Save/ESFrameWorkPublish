using UnityEngine;

#if UNITY_EDITOR
using Gaskellgames.EditorOnly;
using UnityEditor;
#endif

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>

    //[CreateAssetMenu(fileName = "GaskellgamesSettings", menuName = "Gaskellgames/GaskellgamesSettings")]
    public class GaskellgamesSettings_SO : ScriptableObject
    {
        #region Variables
        
        [SerializeField]
        [Tooltip("Show/hide this window when loading into a project.")]
        private bool showHubOnStartup = true;
        
        [SerializeField, HideInLine, HideInInspector]
        [Tooltip("Toggle whether tracked tasks are in debug mode.")]
        private bool debugGgTrackedTasks = true;
        
        [Title(subHeading: "Banners")]
        [SerializeField, OnValueChanged(nameof(OnValueChanged_AllEditors))]
        [Tooltip("Show or hide the Gaskellgames package headers.")]
        private bool showPackageBanners = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_EditorWindows)), ShowIf(nameof(showPackageBanners)), Indent]
        [Tooltip("Show or hide the Gaskellgames package header in editor windows.")]
        private bool editorWindowBanner = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Editors)), ShowIf(nameof(showPackageBanners)), Indent]
        [Tooltip("Show or hide the Gaskellgames package header for component scripts.")]
        private bool componentBanner = true;
        
        [Title(subHeading: "Hierarchy")]
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Hierarchy))]
        [Tooltip("Show or hide the hierarchy Breadcrumbs.")]
        private bool showHierarchyBreadcrumbs = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Hierarchy))]
        [Tooltip("Show or hide the hierarchy icons for Gaskellgames components.")]
        private bool showHierarchyIcons = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Hierarchy)), ShowIf(nameof(showHierarchyIcons)), Indent]
        [Tooltip("Toggle whether the number of icons shown in the hierarchy is limited to a certain number. If true, the number of icons shown is limited to the value of 'Max Icon Count'. If false, all icons are shown.")]
        private bool limitIconCount = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Hierarchy)), ShowIf(new []{nameof(showHierarchyIcons), nameof(limitIconCount)}, new object[] { true, true }), Indent, MinMax(1, 10)]
        [Tooltip("If 'Limit Icon Count' is true, this value is the maximum number of icons shown in the hierarchy. If 'Limit Icon Count' is false, this value is ignored.")]
        private int maxIconCount = 3;
        
        [Title(subHeading: "Transform Tools")]
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Editors))]
        [Tooltip("Enable or Disable the Gaskellgames transform utilities extension.")]
        private bool showTransformTools = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Editors)), ShowIf(nameof(showTransformTools)), Indent]
        [Tooltip("Toggle whether to show the 'Alignment' buttons in the transform inspector.")]
        private bool alignTools = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Editors)), ShowIf(nameof(showTransformTools)), Indent]
        [Tooltip("Toggle whether to show the 'Reset' buttons in the transform inspector.")]
        private bool resetButtons = true;
        
        [SerializeField, OnValueChanged(nameof(OnValueChanged_Editors)), ShowIf(nameof(showTransformTools)), Indent]
        [Tooltip("Toggle whether to show the 'Utilities' in the transform inspector.")]
        private bool transformUtilities = true;
        
        [Title(subHeading: "Logs")]
        [SerializeField]
        [Tooltip("If show logs is true: Gaskellgames logs will be displayed in the console, otherwise they will be hidden.")]
        private bool showGaskellgamesLogs = true;
        
        [SerializeField, ShowIf(nameof(showGaskellgamesLogs)), Indent]
        [Tooltip("Toggle whether info logs are shown for Gaskellgames scripts.")]
        private bool infoLogs = true;
        
        [SerializeField, ShowIf(nameof(showGaskellgamesLogs)), Indent]
        [Tooltip("Toggle whether warning logs are shown for Gaskellgames scripts.")]
        private bool warningLogs = true;
        
        [SerializeField, ShowIf(nameof(showGaskellgamesLogs)), Indent]
        [Tooltip("Toggle whether error logs are shown for Gaskellgames scripts.")]
        private bool errorLogs = true;
        
        [SerializeField, ShowIf(nameof(showGaskellgamesLogs)), Indent]
        [Tooltip("Toggle whether debugging logs are shown for Gaskellgames scripts. (Note: these logs will likely spam the console.)")]
        private bool debugLogs = false;
        
        #endregion
        
    	//----------------------------------------------------------------------------------------------------
        
        #region Internal Getter/Setter

        internal bool ShowHubOnStartup
        {
            get => showHubOnStartup;
            set => showHubOnStartup = value;
        }
        
        internal bool DebugGgTrackedTasks
        {
            get => debugGgTrackedTasks;
            set => debugGgTrackedTasks = value;
        }
        
        internal bool ShowLogs
        {
            get => showGaskellgamesLogs;
            set => showGaskellgamesLogs = value;
        }
        
        internal bool ShowInfoLogs
        {
            get => showGaskellgamesLogs && infoLogs;
            set => infoLogs = value;
        }
        
        internal bool ShowWarningLogs
        {
            get => showGaskellgamesLogs && warningLogs;
            set => warningLogs = value;
        }
        
        internal bool ShowErrorLogs
        {
            get => showGaskellgamesLogs && errorLogs;
            set => errorLogs = value;
        }
        
        internal bool ShowDebugLogs
        {
            get => showGaskellgamesLogs && debugLogs;
            set => debugLogs = value;
        }
        
        internal bool ShowPackageBanners
        {
            get => showPackageBanners;
            set => showPackageBanners = value;
        }
        
        internal bool ShowEditorWindowBanner
        {
            get => showPackageBanners && editorWindowBanner;
            set
            {
                if (value)
                {
                    showPackageBanners = true;
                    editorWindowBanner = true;
                    
                }
                else
                {
                    if (!componentBanner) { showPackageBanners = false; }
                    editorWindowBanner = false;
                }
            }
        }
        
        internal bool ShowComponentBanner
        {
            get => showPackageBanners && componentBanner;
            set
            {
                if (value)
                {
                    showPackageBanners = true;
                    componentBanner = true;
                    
                }
                else
                {
                    if (!editorWindowBanner) { showPackageBanners = false; }
                    componentBanner = false;
                }
            }
        }
        
        internal bool ShowHierarchyBreadcrumbs
        {
            get => showHierarchyBreadcrumbs;
            set => showHierarchyBreadcrumbs = value;
        }
        
        internal bool ShowHierarchyIcons
        {
            get => showHierarchyIcons;
            set => showHierarchyIcons = value;
        }
        
        internal bool LimitIconCount
        {
            get => limitIconCount;
            set => limitIconCount = value;
        }
        
        internal int MaxIconCount
        {
            get => showHierarchyIcons ? Mathf.Clamp(maxIconCount, 1, 10) : 0;
            set
            {
                showHierarchyIcons = true;
                limitIconCount = true;
                maxIconCount = Mathf.Clamp(value, 1, 10);
            }
        }
        
        internal bool ShowTransformTools
        {
            get => showTransformTools;
            set => showTransformTools = value;
        }
        
        internal bool ShowAlignTools
        {
            get => showTransformTools && alignTools;
            set
            {
                if (value)
                {
                    showTransformTools = true;
                    alignTools = true;
                    
                }
                else
                {
                    if (!resetButtons && !transformUtilities) { showTransformTools = false; }
                    alignTools = false;
                }
            }
        }
        
        internal bool ShowResetButtons
        {
            get => showTransformTools && resetButtons;
            set
            {
                if (value)
                {
                    showTransformTools = true;
                    resetButtons = true;
                    
                }
                else
                {
                    if (!alignTools && !transformUtilities) { showTransformTools = false; }
                    resetButtons = false;
                }
            }
        }
        
        internal bool ShowTransformUtilities
        {
            get => showTransformTools && transformUtilities;
            set
            {
                if (value)
                {
                    showTransformTools = true;
                    transformUtilities = true;
                    
                }
                else
                {
                    if (!alignTools && !resetButtons) { showTransformTools = false; }
                    transformUtilities = false;
                }
            }
        }
        
        #endregion
        
    	//----------------------------------------------------------------------------------------------------
        
        #region Internal Functions
        
        private static GaskellgamesSettings_SO instance;
        
        internal static GaskellgamesSettings_SO Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GaskellgamesSettings_SO>("GaskellgamesSettings");
  
                    if (instance == null)
                    { 
                        // create a temp asset so there are no null pointer errors...
                        instance = ScriptableObject.CreateInstance<GaskellgamesSettings_SO>();
#if UNITY_EDITOR
                        // ...and if editor then save it to disk
                        string properPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Resources");
                        if (!System.IO.Directory.Exists(properPath))
                        {
                            UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
                        }
                        string fullPath = System.IO.Path.Combine("Assets", "Resources", "GaskellgamesSettings.asset");
                        UnityEditor.AssetDatabase.CreateAsset(instance, fullPath);
#endif
                    }
                }
    
                return instance;
            }
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Private Functions
        
        /// <summary>
        /// Handle OnValueChanged callback for inspector editor and editor window settings.
        /// </summary>
        private void OnValueChanged_AllEditors()
        {
            OnValueChanged_EditorWindows();
            OnValueChanged_Editors();
        }
        
        /// <summary>
        /// Handle OnValueChanged callback for editor window settings.
        /// </summary>
        private void OnValueChanged_EditorWindows()
        {
#if UNITY_EDITOR
            GgEditorWindow[] editorWindows = Resources.FindObjectsOfTypeAll<GgEditorWindow>();
            foreach (GgEditorWindow ggEditorWindow in editorWindows)
            {
                ggEditorWindow.Repaint();
            }
#endif
        }
        
        /// <summary>
        /// Handle OnValueChanged callback for inspector editor settings.
        /// </summary>
        private void OnValueChanged_Editors()
        {
#if UNITY_EDITOR
            GgEditor[] editors = Resources.FindObjectsOfTypeAll<GgEditor>();
            foreach (GgEditor ggEditor in editors)
            {
                ggEditor.Repaint();
            }
#endif
        }
        
        /// <summary>
        /// Handle OnValueChanged callback for hierarchy window settings.
        /// </summary>
        private void OnValueChanged_Hierarchy()
        {
#if UNITY_EDITOR
            EditorApplication.RepaintHierarchyWindow();
#endif
        }
        
        /// <summary>
        /// Handle OnValueChanged callback for project window settings.
        /// </summary>
        private void OnValueChanged_Project()
        {
#if UNITY_EDITOR
            EditorApplication.RepaintProjectWindow();
#endif
        }

        #endregion
        
    } // class end
}