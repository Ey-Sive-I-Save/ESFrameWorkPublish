#if UNITY_EDITOR
using UnityEditor;

namespace Gaskellgames.FolderSystem.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [InitializeOnLoad]
    public class DefineSymbols_FolderSystem : AssetModificationProcessor
    {
        #region Variables
        
        private static readonly string[] ExtraScriptingDefineSymbols = new string[] { "GASKELLGAMES_FOLDERSYSTEM" };
        private static readonly string ThisFileName = "DefineSymbols_FolderSystem.cs";
        
        private static readonly string error_failedRemoveSymbols = $"{link_GgCore} not detected: FolderSystem package failed to automatically remove ScriptingDefineSymbols from the project settings!";

        private static readonly string link_GgCore = "<a href=\"https://assetstore.unity.com/packages/tools/utilities/ggcore-gaskellgames-304325\">GgCore</a>";
        private static readonly string error = $"{link_GgCore} not detected: The FolderSystem package from Gaskellgames requires {link_GgCore}. Please add the package from the package manager, or claim it for FREE from the Unity Asset Store using the same account that has the Folder System asset licence.";
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region AssetModificationProcessor

        static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
        {
            // If, and only if, the filename matches this filename, remove ScriptingDefineSymbols
            string[] pathSplit = path.Split("/");
            if (pathSplit[^1] == ThisFileName)
            {
#if GASKELLGAMES
                Gaskellgames.EditorOnly.DefineSymbols_GgCore.RemoveExtraScriptingDefineSymbols(ExtraScriptingDefineSymbols);
#else
                UnityEngine.Debug.LogError(error_failedRemoveSymbols);
#endif
            }
            
            // return of 'DidNotDelete' tells the Unity internal implementation that this 'OnWillDeleteAsset' callback
            // did not delete the asset: so the asset will instead be deleted by the Unity internal implementation.
            return AssetDeleteResult.DidNotDelete;
        }

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Constructors

        static DefineSymbols_FolderSystem()
        {
#if GASKELLGAMES
            Gaskellgames.EditorOnly.DefineSymbols_GgCore.AddExtraScriptingDefineSymbols(ExtraScriptingDefineSymbols);
#else
            UnityEngine.Debug.LogError(error);
#endif
        }
        
        #endregion
        
    } // class end
}

#endif