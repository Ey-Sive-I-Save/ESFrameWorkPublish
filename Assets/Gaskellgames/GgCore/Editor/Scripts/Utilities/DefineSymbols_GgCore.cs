#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    [InitializeOnLoad]
    public class DefineSymbols_GgCore : AssetModificationProcessor
    {
        #region Variables

        private static readonly string[] ExtraScriptingDefineSymbols = new string[] { "GASKELLGAMES" };
        private static readonly string ThisFileName = "DefineSymbols_GgCore.cs";

        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region AssetModificationProcessor

        static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions opt)
        {
            // If, and only if, the filename matches this filename, remove ScriptingDefineSymbols
            string[] pathSplit = path.Split("/");
            if (pathSplit[^1] == ThisFileName)
            {
                RemoveExtraScriptingDefineSymbols(ExtraScriptingDefineSymbols);
            }
            
            // return of 'DidNotDelete' tells the Unity internal implementation that this 'OnWillDeleteAsset' callback
            // did not delete the asset: so the asset will instead be deleted by the Unity internal implementation.
            return AssetDeleteResult.DidNotDelete;
        }

        #endregion

        //----------------------------------------------------------------------------------------------------

        #region Constructors

        static DefineSymbols_GgCore()
        {
            AddExtraScriptingDefineSymbols(ExtraScriptingDefineSymbols);
        }
        
        #endregion
        
        //----------------------------------------------------------------------------------------------------
        
        #region Private Functions
        
        /// <summary>
        /// Add any new ScriptingDefineSymbols in extraScriptingDefineSymbols to the current ScriptingDefineSymbols in build settings
        /// </summary>
        /// <param name="extraScriptingDefineSymbols"></param>
        public static void AddExtraScriptingDefineSymbols(string[] extraScriptingDefineSymbols)
        {
            // add any new ScriptingDefineSymbols from ExtraScriptingDefineSymbols
            List<string> scriptingDefineSymbols = GetScriptDefineSymbols();
            
            // add range
            int oldCount = scriptingDefineSymbols.Count;
            scriptingDefineSymbols.AddRange(extraScriptingDefineSymbols.Except(scriptingDefineSymbols));
            
            // update scripting define symbols
            if (oldCount != scriptingDefineSymbols.Count)
            {
                SetScriptDefineSymbols(scriptingDefineSymbols);
            }
        }
        
        /// <summary>
        /// Remove any ScriptingDefineSymbols in extraScriptingDefineSymbols from the current ScriptingDefineSymbols in build settings
        /// </summary>
        /// <param name="extraScriptingDefineSymbols"></param>
        public static void RemoveExtraScriptingDefineSymbols(string[] extraScriptingDefineSymbols)
        {
            // remove any ScriptingDefineSymbols from ExtraScriptingDefineSymbols
            List<string> scriptingDefineSymbols = GetScriptDefineSymbols();
            
            // remove range
            int oldCount = scriptingDefineSymbols.Count;
            foreach (string defineSymbol in extraScriptingDefineSymbols)
            {
                scriptingDefineSymbols.Remove(defineSymbol);
            }
            
            // update scripting define symbols
            if (oldCount != scriptingDefineSymbols.Count)
            {
                SetScriptDefineSymbols(scriptingDefineSymbols);
            }
        }

        /// <summary>
        /// Get all ScriptingDefineSymbols from build settings.
        /// </summary>
        /// <returns></returns>
        private static List<string> GetScriptDefineSymbols()
        {
#if UNITY_6000_0_OR_NEWER
            UnityEditor.Build.NamedBuildTarget selectedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string scriptingDefineSymbolsForGroup = PlayerSettings.GetScriptingDefineSymbols(selectedBuildTarget);
#else
            string scriptingDefineSymbolsForGroup = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
#endif
            return scriptingDefineSymbolsForGroup.Split(';').ToList();
        }

        /// <summary>
        /// Set the ScriptingDefineSymbols in build settings.
        /// </summary>
        /// <param name="scriptingDefineSymbols"></param>
        private static void SetScriptDefineSymbols(List<string> scriptingDefineSymbols)
        {
            string newScriptingDefineSymbols = string.Join(";", scriptingDefineSymbols.ToArray());
            
#if UNITY_6000_0_OR_NEWER
            UnityEditor.Build.NamedBuildTarget selectedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.SetScriptingDefineSymbols(selectedBuildTarget, newScriptingDefineSymbols);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, newScriptingDefineSymbols);
#endif
        }
        
        #endregion
        
    } // class end
}

#endif