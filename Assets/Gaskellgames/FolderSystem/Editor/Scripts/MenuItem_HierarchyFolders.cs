#if UNITY_EDITOR
#if GASKELLGAMES
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.FolderSystem.EditorOnly
{
    /// <summary>
    /// Code updated by Gaskellgames
    /// </summary>
    
    public class MenuItem_HierarchyFolders : MenuItemUtility
    {
        #region GameObject Menu
        
        [MenuItem(FolderSystem_GameObjectMenu_Path + "/Create Folder", false, GameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_HierarchyFolder(MenuCommand menuCommand)
        {
            // create a custom gameObject, register in the undo system, parent and set position relative to context
            GameObject go = SetupMenuItemInContext(menuCommand, "FOLDER:");
            go.transform.localPosition = Vector3.zero;
            
            // add scripts & components
            go.AddComponent<HierarchyFolders>();
            
            // select newly created gameObject
            Selection.activeObject = go;
        }

        [MenuItem(FolderSystem_GameObjectMenu_Path + "/Create Folder Parent", true, GameObjectMenu_Priority)]
        private static bool Gaskellgames_GameObjectMenu_HierarchyFolderParent()
        {
            return Selection.activeTransform;
        }
        
        [MenuItem(FolderSystem_GameObjectMenu_Path + "/Create Folder Parent", false, GameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_HierarchyFolderParent(MenuCommand menuCommand)
        {
            // cache selected objects
            Object[] objects = Selection.objects;
            Transform[] transforms = Selection.transforms;
            Transform active = Selection.activeTransform;
            
            if (objects[0] == menuCommand.context)
            {
                // create a custom gameObject & register the creation in the undo system
                GameObject go = new GameObject("FOLDER:");
                Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
                
                // parent and set position relative to context
                go.transform.SetParent(active.parent);
                go.transform.localPosition = Vector3.zero;

                // parent selected to newly created gameObject
                foreach (Transform t in transforms)
                {
                    t.SetParent(go.transform);
                }
                
                // add scripts & components
                go.AddComponent<HierarchyFolders>();
                
                // select newly created gameObject
                Selection.activeObject = go;
            }
        }
        
        #endregion
        
    } // class end
}

#endif
#endif