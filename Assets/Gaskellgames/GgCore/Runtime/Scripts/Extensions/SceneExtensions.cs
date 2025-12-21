using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class SceneExtensions
    {
        /// <summary>
        /// Get a list of all currently open scenes
        /// </summary>
        /// <returns></returns>
        public static List<Scene> GetAllOpenScenes()
        {
            // create new list of open scenes
            List<Scene> loadedScenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                loadedScenes.Add(scene);
            }

            return loadedScenes;
        }
        
        /// <summary>
        /// Get a list of all root gameObjects in all open scenes
        /// </summary>
        /// <returns></returns>
        public static List<GameObject> GetAllRootGameObjects()
        {
            List<Scene> loadedScenes = GetAllOpenScenes();
            List<GameObject> rootGameObjects = new List<GameObject>();

            foreach (Scene scene in loadedScenes)
            {
                if (!scene.isLoaded) { continue; }
                GameObject[] rootObjects = scene.GetRootGameObjects();
                rootGameObjects.AddRange(rootObjects);
            }

            return rootGameObjects;
        }

    } // class end
}
