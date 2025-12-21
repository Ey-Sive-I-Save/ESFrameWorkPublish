using System.Collections.Generic;
using UnityEngine;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class GameObjectExtensions
    {
        /// <summary>
        /// /Destroys all child objects under <paramref name="gameObject"/>.
        /// </summary>
        /// <param name="gameObject"></param>
        public static void DestroyAllChildObjects(this GameObject gameObject)
        {
            for (int i = gameObject.transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(gameObject.transform.GetChild(i).gameObject);
            }
        }
    
        /// <summary>
        /// Get a list of all child gameObjects of <paramref name="root"/>
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public static List<GameObject> GetAllChildGameObjects(GameObject root)
        {
            List<GameObject> childGameObjects = new List<GameObject>();
            foreach (Transform childTransform in root.transform.GetComponentsInChildren<Transform>())
            {
                childGameObjects.Add(childTransform.gameObject);
            }
            return childGameObjects;
        }
    
    } // class end
}
