#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class MenuItemUtility
    {
        #region Menu Items
        
        // Extensions
        
        public const string GameObjectMenu_Path = "GameObject";
        public const int GameObjectMenu_Priority = 0;
        
        // Gaskellgames
        private const string GgToolsMenu = "Tools/Gaskellgames";
        private const string GgWindowMenu = "Window/Gaskellgames";
        private const string GgGameObjectMenu = "GameObject/Gaskellgames";
        public const int GgGameObjectMenu_Priority = 10;
        
        // GgCore: Gaskellgames Hub
        private const string Hub = "/Gaskellgames Hub";
        
        public const int Hub_Priority = 100;
        public const string Hub_ToolsMenu_Path = GgToolsMenu + Hub;
        public const string Hub_WindowMenu_Path = GgWindowMenu + Hub;
        
        // GgCore: GgTask Tracker
        public const int GgTaskTracker_Priority = 101;
        public const string GgTaskTracker_ToolsMenu_Path = GgToolsMenu + "/GgTask Tracker";
        public const string GgTaskTracker_WindowMenu_Path = GgWindowMenu + "/GgTask Tracker";
        
        // GgCore: Samples
        public const int GgCoreSamples_Priority = 102;
        public const string GgCoreSamples_ToolsMenu_Path = GgToolsMenu + "/GgCore Samples";
        public const string GgCoreSamples_WindowMenu_Path = GgWindowMenu + "/GgCore Samples";
        private const string GgCore_GameObjectMenu_Path = GgGameObjectMenu + "/GgCore";
        
        // Audio Controller
        public const int AudioController_Priority = 125;
        public const string AudioController_ToolsMenu_Path = GgToolsMenu + "/Audio Manager";
        public const string AudioController_WindowMenu_Path = GgWindowMenu + "/Audio Manager";
        public const string AudioController_GameObjectMenu_Path = GgGameObjectMenu + "/Audio Controller";
        
        // Camera System
        public const string CameraSystem_GameObjectMenu_Path = GgGameObjectMenu + "/Camera System";
        
        // Character Controller
        public const string CharacterController_GameObjectMenu_Path = GgGameObjectMenu + "/Character Controller";
        
        // Folder System
        public const int FolderSystem_Priority = 126;
        public const string FolderSystem_ToolsMenu_Path = GgToolsMenu + "/Folder Manager";
        public const string FolderSystem_WindowMenu_Path = GgWindowMenu + "/Folder Manager";
        public const string FolderSystem_GameObjectMenu_Path = GameObjectMenu_Path;
        
        // Icon Finder
        public const int IconFinder_Priority = 127;
        public const string IconFinder_ToolsMenu_Path = GgToolsMenu + "/Icon Finder";
        public const string IconFinder_WindowMenu_Path = GgWindowMenu + "/Icon Finder";
        
        // Input Event System
        public const string InputEventSystem_GameObjectMenu_Path = GgGameObjectMenu + "/Input Event System";
        
        // Menu System
        public const string MenuSystem_GameObjectMenu_Path = GgGameObjectMenu + "/Menu System";
        
        // Platform Controller
        public const string PlatformController_GameObjectMenu_Path = GgGameObjectMenu + "/Platform Controller";
        
        // Pooling System
        public const string PoolingSystem_GameObjectMenu_Path = GgGameObjectMenu + "/Pooling System";
        
        // Spline System
        public const string SplineSystem_GameObjectMenu_Path = GgGameObjectMenu + "/Spline System";
        
        // Scene Group System
        public const int SceneController_Priority = 128;
        public const string SceneController_ToolsMenu_Path = GgToolsMenu + "/Scene Manager";
        public const string SceneController_WindowMenu_Path = GgWindowMenu + "/Scene Manager";
        public const string SceneController_GameObjectMenu_Path = GgGameObjectMenu + "/Scene Controller";
        
        // Tag System
        public const string TagSystem_GameObjectMenu_Path = GgGameObjectMenu + "/Tag System";
        
        // Trigger System
        public const string TriggerSystem_GameObjectMenu_Path = GgGameObjectMenu + "/Trigger System";
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Helper Functions

        public static GameObject SetupMenuItemInContext(MenuCommand menuCommand, string gameObjectName)
        {
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = new GameObject(gameObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            GameObject context = (GameObject)menuCommand.context;
            if (context != null) { go.transform.SetParent(context.transform); }
            EditorExtensions.GetSceneViewLookAt(out Vector3 point);
            go.transform.position = point;

            GgLogs.Log(null, GgLogType.Info, "'{0}' created.", gameObjectName);
            
            return go;
        }

        public static GameObject AddChildItemInContext(Transform parent, string gameObjectName)
        {
            GameObject go = new GameObject(gameObjectName);
            go.transform.parent = parent;
            go.transform.localPosition = Vector3.zero;
            go.transform.localEulerAngles = Vector3.zero;
            
            return go;
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Gameobject Menu
        
        [MenuItem(GgCore_GameObjectMenu_Path + "/Comment", false, GgGameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_Comment(MenuCommand menuCommand)
        {
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "Comment");
            
            // add scripts & components
            go.AddComponent<Comment>();
            
            // select newly created gameObject
            Selection.activeObject = go;
        }

        #endregion
        
    } // class end
}
#endif