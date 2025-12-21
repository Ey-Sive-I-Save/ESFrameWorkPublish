#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [InitializeOnLoad]
    public class HierarchyIcon_GgCore
    {
        #region Variables

        private const string icon_Comment = "Icon_Comment";
        private const string icon_LockToLayer = "Icon_LayerLock";
        private const string icon_SelectionTarget = "Icon_SelectionTarget";
        private const string icon_OnObjectEvents = "Icon_OnObjectEvents";
        private const string icon_TransformObject = "Icon_TransformObject";

        private const string packageRefName = "GgCore";
        private const string relativePath = "/Editor/Icons/";
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------

        #region Editor Loop
        
        static HierarchyIcon_GgCore()
        {
            HierarchyUtility.onCacheHierarchyIcons -= CacheHierarchyIcons;
            HierarchyUtility.onCacheHierarchyIcons += CacheHierarchyIcons;
        }

        private static void CacheHierarchyIcons()
        {
            if (!GgPackageRef.TryGetFullFilePath(packageRefName, relativePath, out string filePath)) { return; }
            
            HierarchyUtility.TryAddHierarchyIcon(typeof(Comment), icon_Comment, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_Comment}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(LockToLayer), icon_LockToLayer, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_LockToLayer}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(SelectionTarget), icon_SelectionTarget, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_SelectionTarget}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(OnObjectEvents), icon_OnObjectEvents, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_OnObjectEvents}.png", typeof(Texture)) as Texture);
            HierarchyUtility.TryAddHierarchyIcon(typeof(TransformObject), icon_TransformObject, AssetDatabase.LoadAssetAtPath(filePath + $"{icon_TransformObject}.png", typeof(Texture)) as Texture);
        }
        
        #endregion
        
    } // class end
}

#endif