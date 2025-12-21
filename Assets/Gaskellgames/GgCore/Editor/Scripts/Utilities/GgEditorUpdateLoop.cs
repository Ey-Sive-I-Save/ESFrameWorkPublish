#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames.EditorOnly
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com: https://github.com/Gaskellgames/Unity_IEditorUpdate
    /// </remarks>
    
    [InitializeOnLoad]
    public static class GgEditorUpdateLoop
    {
        internal static List<IEditorUpdate> iEditorUpdateList = new List<IEditorUpdate>();
        internal static event Action OnIEditorUpdateListUpdated;
        
        /// <summary>
        /// Constructor will be called during initialisation
        /// </summary>
        static GgEditorUpdateLoop()
        {
            // clear any previous listeners
            AssemblyReloadEvents.afterAssemblyReload -= HandleIEditorUpdateCallbacks;
            EditorApplication.hierarchyChanged -= HandleIEditorUpdateCallbacks;
            
            // assign listeners
            AssemblyReloadEvents.afterAssemblyReload += HandleIEditorUpdateCallbacks;
            EditorApplication.hierarchyChanged += HandleIEditorUpdateCallbacks;
        }
        
        /// <summary>
        /// Force update the editorUpdateList for IEditorUpdate
        /// </summary>
        internal static void ForceUpdateComponentList()
        {
            HandleIEditorUpdateCallbacks();
        }
        
        /// <summary>
        /// Handle subscribing to or from the editor update loop
        /// </summary>
        private static void HandleIEditorUpdateCallbacks()
        {
            // playmode check
            if (Application.isPlaying)
            {
                // unsubscribe all existing IEditorUpdate components from the editor update loop
                if (0 < iEditorUpdateList.Count) { UnsubscribeFromEditorUpdateLoop(); }
                return;
            }

            // Handle IEditorUpdate
            if (ComponentListUpdated(ref iEditorUpdateList, out List<IEditorUpdate> addedComponents, out List<IEditorUpdate> removedComponents))
            {
                foreach (IEditorUpdate removedComponent in removedComponents)
                {
                    EditorApplication.update -= removedComponent.HandleEditorUpdate;
                }
                foreach (IEditorUpdate addedComponent in addedComponents)
                {
                    EditorApplication.update += addedComponent.HandleEditorUpdate;
                }
                OnIEditorUpdateListUpdated?.Invoke();
            }
        }

        /// <summary>
        /// Unsubscribe a list of components with IEditorUpdate from the editor update loop
        /// </summary>
        private static void UnsubscribeFromEditorUpdateLoop()
        {
            foreach (IEditorUpdate editorUpdate in iEditorUpdateList)
            {
                if (!(Object)editorUpdate) { continue; }
                EditorApplication.update -= editorUpdate.HandleEditorUpdate;
            }
            iEditorUpdateList.Clear();
            OnIEditorUpdateListUpdated?.Invoke();
        }

        /// <summary>
        /// Checks to see if the list of all IEditorUpdate has changed
        /// </summary>
        /// <returns>True if changed, False if not</returns>
        private static bool ComponentListUpdated<T>(ref List<T> comparisonList, out List<T> addedComponents, out List<T> removedComponents)
        {
            // get all 'component type' in open scenes
            List<T> newComponentList = new List<T>();
            List<GameObject> rootObjects = SceneExtensions.GetAllRootGameObjects();
            foreach(GameObject root in rootObjects)
            {
                T[] componentsArray = root.GetComponentsInChildren<T>(true);
                foreach (T debugEvent in componentsArray)
                {
                    newComponentList.Add(debugEvent);
                }
            }
        
            // check to see if the list has been updated
            bool updated = false;
            addedComponents = new List<T>();
            removedComponents = new List<T>();
            foreach (T component in comparisonList)
            {
                if (newComponentList.Contains(component)) { continue; }
                removedComponents.Add(component);
                updated = true;
            }
            foreach (T component in newComponentList)
            {
                if (comparisonList.Contains(component)) { continue; }
                addedComponents.Add(component);
                updated = true;
            }

            if (updated) { comparisonList = newComponentList; }
            return updated;
        }

    } // class end
}
#endif
