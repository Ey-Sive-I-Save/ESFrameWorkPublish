#if UNITY_EDITOR
using Gaskellgames.EditorOnly;
using UnityEditor;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public class MenuItem_Transform : MenuItemUtility
    {
        #region GameObject Menu

        [MenuItem(GameObjectMenu_Path + "/Reset Origin (ParentOnly)", true, GameObjectMenu_Priority)]
        private static bool Gaskellgames_GameObjectMenu_HierarchyResetOrigin()
        {
            return Selection.activeTransform;
        }
        
        [MenuItem(GameObjectMenu_Path + "/Reset Origin (ParentOnly)", false, GameObjectMenu_Priority)]
        private static void Gaskellgames_GameObjectMenu_HierarchyResetOrigin(MenuCommand menuCommand)
        {
            // cache selected object and child values
            Object[] objects = Selection.objects;
            Transform[] transforms = Selection.transforms;

            // stop menu command running more than once when multiple items are selected
            if (objects[0] != menuCommand.context) { return; }
            
            // reset origin and then offset child objects
            foreach (Transform parent in transforms)
            {
                Vector3 positionOffset = parent.position;
                Undo.RecordObject(parent, "ResetOrigin " + parent.name);
                parent.position = Vector3.zero;
                
                foreach (Transform child in parent)
                {
                    Undo.RecordObject(child, "ResetOrigin " + child.name);
                    child.position += positionOffset;
                }
            }
        }

        #endregion
        
    } // class end
}

#endif